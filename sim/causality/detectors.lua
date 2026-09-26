-- Detectors (03_CAUSALITY.md §4): threshold crossings → events. Runs last in the tick (slot 14).
--
-- Every detector emits with parent = the cause tag of the field that crossed (the dominant tag
-- over the cells involved) and then RE-TAGS those cells with the new event id, so everything
-- downstream carries the derived event and chains grow one link per detector.
-- State (rolling averages, hysteresis flags, cooldowns) lives in W.D and is saved. Averages are
-- sampled even during spin-up (so Year 0 starts warm); events are emitted only when
-- W.record_events is true.
--
--   every tick : flood (from hydrology's W.scratch.flood list; per-cell cooldown)
--   monthly    : population_boom / population_crash (land region mean density ×2 / ÷2 vs its
--                5-year EMA; entry/exit thresholds + cooldown), extinction_regional /
--                extinction_global, fish_collapse (connected fishing grounds whose fish fell
--                below 50% of their per-cell 5-year normal, D-014), oil_spreading (connected
--                slick ≥ 10 level-4-equivalent cells), oil_beached (oil ashore), drought (a
--                normally wetter region's land soil water < 0.2 and < 75% of its normal for a
--                season).
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local events = require("sim.causality.events")

local sys = { name = "detectors" }
local ceil, floor = math.ceil, math.floor

local SPECIES = { "grazers", "predators" }

-- Derived region tables from W.static.region + W.names.regions.
function sys.rebuild(W)
  local n = W.n
  local reg = W.static.region
  local recs = (W.names and W.names.regions) or {}
  local nr = #recs
  local area = W.grid.area
  local ocean = W.geo.ocean
  local R = { n = nr, cells = {}, lcells = {}, area = {}, larea = {}, is_land = {}, land_ids = {} }
  for r = 1, nr do
    R.cells[r], R.lcells[r], R.area[r], R.larea[r] = {}, {}, 0, 0
    local k = recs[r].kind
    R.is_land[r] = (k ~= "sea" and k ~= "ocean")
    if R.is_land[r] then R.land_ids[#R.land_ids + 1] = r end
  end
  for c = 1, n do
    local r = reg[c]
    if r > 0 and r <= nr then
      local l = R.cells[r]
      l[#l + 1] = c
      R.area[r] = R.area[r] + area[c]
      if ocean[c] == 0 then
        local ll = R.lcells[r]
        ll[#ll + 1] = c
        R.larea[r] = R.larea[r] + area[c]
      end
    end
  end
  W.regions = R
end

function sys.init(W)
  local n, D = W.n, W.D
  local nr = W.regions.n
  local f = fields.filled
  if not D.fauna then
    D.fauna = {}
    for _, sp in ipairs(SPECIES) do
      D.fauna[sp] = { avg = f(nr, -1), state = f(nr, 0), last = f(nr, -1000000), ext = f(nr, 0), gext = 0 }
    end
    D.soil = { avg = f(nr, -1), months = f(nr, 0), on = f(nr, 0) }
    D.fish_low = f(n, 0)
    D.flood_last = f(n, -1000000)
  end
  local sc = W.scratch
  sc.det_tk = sc.det_tk or {}
  sc.det_tc = sc.det_tc or {}
  sc.det_mark = sc.det_mark or f(n, 0)
  sc.det_stamp = sc.det_stamp or 0
  sc.det_q = sc.det_q or {}
  sc.det_new = sc.det_new or {}
end

function sys.shift_ticks(W, dt)
  local D = W.D
  if not D.fauna then return end
  for _, sp in ipairs(SPECIES) do
    local l = D.fauna[sp].last
    for r = 1, #l do l[r] = l[r] + dt end
  end
  local fl = D.flood_last
  for c = 1, #fl do fl[c] = fl[c] + dt end
end

-- Most frequent non-zero tag among cells[1..m] (from an array list `cells` or scratch queue),
-- if it covers ≥ share of them; else 0. Allocation-free (scratch arrays).
local function dominant(W, cells, m, tags, share)
  local tk, tc = W.scratch.det_tk, W.scratch.det_tc
  local k = 0
  for i = 1, m do
    local t = tags[cells[i]]
    if t ~= 0 then
      local found = false
      for j = 1, k do
        if tk[j] == t then tc[j] = tc[j] + 1; found = true; break end
      end
      if not found then k = k + 1; tk[k] = t; tc[k] = 1 end
    end
  end
  local best, bc = 0, 0
  for j = 1, k do
    if tc[j] > bc or (tc[j] == bc and tk[j] < best) then best, bc = tk[j], tc[j] end
  end
  if bc > 0 and bc >= share * m then return best end
  return 0
end
sys.dominant = dominant

local function kind_of(W, id)
  local r = id ~= 0 and W.E.rec[id]
  return r and r.kind or nil
end

-- Re-tag cells whose tag == parent with id (cells with a different cause keep theirs). With
-- parent 0 (a natural event) only cells where the field is present are claimed.
local function retag(cells, m, tags, idle, vals, parent, id)
  for i = 1, m do
    local c = cells[i]
    if tags[c] == parent and (parent ~= 0 or vals[c] > 0) then tags[c] = id; idle[c] = 0 end
  end
end

-- Species events: with a real parent, cells carrying it are re-tagged; for a natural event
-- (parent 0) only the cells that themselves crossed (value vs their own 5-year normal, kept by
-- the fauna system) are claimed, so a regional crash doesn't stamp every animal in the region.
-- dir: -1 crash, +1 boom, 0 extinction (any cell that had a normal population).
local function retag_species(W, cells, m, sp, parent, id, dir, cfg)
  local tags, idle, vals = W.C[sp], W.I[sp], W.F[sp]
  if parent ~= 0 then
    for i = 1, m do
      local c = cells[i]
      if tags[c] == parent then tags[c] = id; idle[c] = 0 end
    end
    return
  end
  local FS = W.S.fauna
  local norm = FS and (sp == "grazers" and FS.hnorm or FS.pnorm)
  if not norm then return end
  for i = 1, m do
    local c = cells[i]
    if tags[c] == 0 then
      local v, nv = vals[c], norm[c]
      local hit
      if dir < 0 then hit = nv > 0 and v < cfg.crash * nv
      elseif dir > 0 then hit = v > 0 and v > cfg.boom * nv
      else hit = nv > 0 end
      if hit then tags[c] = id; idle[c] = 0 end
    end
  end
end

local function emit_region(W, kind, r, parent, mag, data)
  local rec = W.names.regions[r]
  return events.emit(W, kind, rec and rec.centre or 0, parent, mag, data, { region = r })
end

-- Which living village fishes / lives near a cell (small loops: villages are few).
local function village_fishing(W, c)
  local vs = W.ent.villages
  if not vs then return nil end
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      local f = v.fishing
      for k = 1, #f do if f[k] == c then return v end end
    end
  end
  return nil
end

local function village_near(W, c)
  local vs = W.ent.villages
  if not vs or not W.vgeo then return nil end
  for i = 1, #vs do
    local v = vs[i]
    if v.alive and W.vgeo[i] and W.vgeo[i].near[c] then return v end
  end
  return nil
end

-- Local booms (D-025): a connected patch of land where a species exceeds boom_local × its own
-- 5-year normal (fauna's per-cell normals) — the boom can be real on part of a landmass while
-- hunting elsewhere holds the regional average down. Shares the regional state/cooldown.
local function local_boom(W, cfg, sp, record)
  local FS = W.S.fauna
  if not FS or not record then return end
  local R = W.regions
  local D = W.D.fauna[sp]
  local vals, tags, idle = W.F[sp], W.C[sp], W.I[sp]
  local norm = sp == "grazers" and FS.hnorm or FS.pnorm
  local minn = cfg.min_density[sp]
  local thr = cfg.boom_local
  local need = ceil(cfg.boom_cells / (W.sscale * W.sscale) - 0.01)
  if need < 2 then need = 2 end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local reg = W.static.region
  local sc = W.scratch
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  local tick = W.tick
  local land = W.geo.land_cells
  for i = 1, #land do
    local src = land[i]
    local r = reg[src]
    if mark[src] ~= stamp and norm[src] > minn and vals[src] > thr * norm[src]
      and D.state[r] == 0 and tick - D.last[r] >= cfg.cooldown then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      while head <= m do
        local c = q[head]; head = head + 1
        local b = c * 6 - 6
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if mark[j] ~= stamp and reg[j] == r and norm[j] > minn and vals[j] > thr * norm[j] then
            mark[j] = stamp; m = m + 1; q[m] = j
          end
        end
      end
      if m >= need then
        D.last[r] = tick
        local parent = dominant(W, q, m, tags, cfg.dominant_share)
        local sv, sn = 0, 0
        for k = 1, m do sv = sv + vals[q[k]]; sn = sn + norm[q[k]] end
        local ratio = sn > 0 and sv / sn or 0
        local rec = W.names.regions[r]
        local id = events.emit(W, "population_boom", src, parent, ratio,
          { species = sp, ratio = ratio, cells = m, ["local"] = true }, { region = r })
        if id ~= 0 then
          for k = 1, m do
            local c = q[k]
            if tags[c] == parent then tags[c] = id; idle[c] = 0 end
          end
        end
        if rec then rec = nil end
      end
    end
  end
end

-- Population boom / crash / regional + global extinction for one species.
local function fauna_species(W, cfg, sp, record)
  local R = W.regions
  local D = W.D.fauna[sp]
  local vals, tags, idle = W.F[sp], W.C[sp], W.I[sp]
  local area = W.grid.area
  local avg, state, last, ext = D.avg, D.state, D.last, D.ext
  local tick = W.tick
  local mind = cfg.min_density[sp]
  local share = cfg.dominant_share
  local am = cfg.avg_months
  local planet = 0
  local ids = R.land_ids
  for q = 1, #ids do
    local r = ids[q]
    local cells = R.lcells[r]
    local m = #cells
    local s = 0
    for i = 1, m do
      local c = cells[i]
      s = s + vals[c] * area[c]
    end
    planet = planet + s
    local la = R.larea[r]
    local cur = la > 0 and s / la or 0
    local a = avg[r]
    if a < 0 then a = cur end
    -- boom / crash with hysteresis + cooldown
    local st = state[r]
    if a >= mind then
      local ratio = cur / a
      if st == 0 then
        local kind, nst = nil, 0
        if ratio >= cfg.boom then kind, nst = "population_boom", 1
        elseif ratio <= cfg.crash then kind, nst = "population_crash", -1 end
        if kind and tick - last[r] >= cfg.cooldown then
          state[r] = nst
          last[r] = tick
          if record then
            local parent = dominant(W, cells, m, tags, share)
            local id = emit_region(W, kind, r, parent, ratio, { species = sp, ratio = ratio })
            if id ~= 0 then retag_species(W, cells, m, sp, parent, id, nst, cfg) end
          end
        end
      elseif st == 1 and ratio < cfg.boom_exit then state[r] = 0
      elseif st == -1 and ratio > cfg.crash_exit then state[r] = 0 end
    elseif st ~= 0 then
      state[r] = 0
    end
    -- regional extinction (permanent unless animals migrate back in)
    if ext[r] == 0 then
      if cur < cfg.extinct_density and a >= cfg.present_density then
        ext[r] = 1
        if record then
          local parent = dominant(W, cells, m, tags, share)
          local id = emit_region(W, "extinction_regional", r, parent, 1, { species = sp })
          if id ~= 0 then retag_species(W, cells, m, sp, parent, id, 0, cfg) end
        end
      end
    elseif cur > cfg.present_density then
      ext[r] = 0
    end
    avg[r] = a + (cur - a) / am
  end
  if D.gext == 0 and planet <= 0 then
    D.gext = 1
    if record then
      local land = W.geo.land_cells
      local parent = dominant(W, land, #land, tags, 0)
      events.emit(W, "extinction_global", 0, parent, 1, { species = sp })
    end
  elseif D.gext == 1 and planet > 0 then
    D.gext = 0   -- only possible if the player reintroduces the species
  end
end

-- Fish collapse on connected fishing grounds (per-cell normal = ocean's 5-year EMA).
local function fish_collapse(W, cfg, record)
  local S = W.S.ocean
  if not S then return end
  local fref = S.fish_ref
  local fish, cfish, ifish = W.F.fish, W.C.fish, W.I.fish
  local low = W.D.fish_low
  local oc = W.geo.ocean_cells
  local sc = W.scratch
  local newl = sc.det_new
  local nnew = 0
  local thr, ex, mref = cfg.fish_low, cfg.fish_exit, cfg.fish_min_ref
  for i = 1, #oc do
    local c = oc[i]
    local ref, f = fref[c], fish[c]
    if low[c] == 0 then
      if ref > mref and f < thr * ref then low[c] = 1; nnew = nnew + 1; newl[nnew] = c end
    elseif f > ex * ref or ref < mref * 0.5 then
      low[c] = 0
    end
  end
  if not record or nnew == 0 then return end
  local need = ceil(cfg.fish_cells / (W.sscale * W.sscale) - 0.01)
  if need < 2 then need = 2 end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local ocean = W.geo.ocean
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  for s = 1, nnew do
    local src = newl[s]
    if mark[src] ~= stamp then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      local sf, sr = 0, 0
      while head <= m do
        local c = q[head]; head = head + 1
        sf = sf + fish[c]; sr = sr + fref[c]
        local b = c * 6 - 6
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if ocean[j] == 1 and low[j] == 1 and mark[j] ~= stamp then
            mark[j] = stamp; m = m + 1; q[m] = j
          end
        end
      end
      if m >= need then
        local parent = dominant(W, q, m, cfish, cfg.dominant_share)
        local pk = kind_of(W, parent)
        if pk == "fish_collapse" or pk == "overfishing" then
          -- the same collapse spreading: extend its tag, no new event
          retag(q, m, cfish, ifish, fish, 0, parent)
        else
          local drop = sr > 0 and floor(100 * (1 - sf / sr) + 0.5) or 0
          -- a fishing ground: attribute to the village; with no other cause, it's overfishing
          local vil = nil
          for k = 1, m do
            vil = village_fishing(W, q[k])
            if vil then break end
          end
          local kind = (vil and parent == 0) and "overfishing" or "fish_collapse"
          local id = events.emit(W, kind, src, parent, drop, { cells = m, drop = drop, village = vil and vil.id or nil })
          if id ~= 0 then retag(q, m, cfish, ifish, fish, parent, id) end
        end
      end
    end
  end
end

-- Oil: connected slicks (oil_spreading) and oil ashore (oil_beached).
local function oil_detect(W, cfg)
  local S = W.S.ocean
  if not S or S.oil_n == 0 then return end
  local oil, coil, ioil = W.F.oil, W.C.oil, W.I.oil
  local ocean = W.geo.ocean
  local list, nl = S.oil, S.oil_n
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local sc = W.scratch
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  local smin = cfg.oil_spread_min
  local need = ceil(cfg.oil_spread_cells / (W.sscale * W.sscale) - 0.01)
  if need < 3 then need = 3 end
  for i = 1, nl do
    local src = list[i]
    if ocean[src] == 1 and oil[src] >= smin and mark[src] ~= stamp then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      while head <= m do
        local c = q[head]; head = head + 1
        local b = c * 6 - 6
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if ocean[j] == 1 and oil[j] >= smin and mark[j] ~= stamp then
            mark[j] = stamp; m = m + 1; q[m] = j
          end
        end
      end
      if m >= need then
        local parent = dominant(W, q, m, coil, cfg.dominant_share)
        if kind_of(W, parent) ~= "oil_spreading" then
          local id = events.emit(W, "oil_spreading", src, parent, m, { cells = m })
          if id ~= 0 then retag(q, m, coil, ioil, oil, parent, id) end
        end
      end
    end
  end
  local bmin = cfg.oil_beach_min
  for i = 1, nl do
    local c = list[i]
    if ocean[c] == 0 and oil[c] >= bmin then
      local t = coil[c]
      if t ~= 0 and kind_of(W, t) ~= "oil_beached" then
        local vil = village_near(W, c)
        local id = events.emit(W, "oil_beached", c, t, 1, vil and { village = vil.id } or nil)
        if id ~= 0 then coil[c] = id; ioil[c] = 0 end
      end
    end
  end
end

-- Villages (03 §4): crop_failure at harvest (< 50% of the 5-year average), famine (food
-- security < 1 month; exit > 2; cooldown), deaths_spike (≥ 3 non-age deaths in a month).
-- Each re-tags the village's cause. migration / village_founded / village_abandoned are
-- emitted by the village system when they happen.
local function village_detect(W, cfg, record)
  local vs = W.ent.villages
  if not vs then return end
  local vcfg = cfgmod.VILLAGE
  local vilsys = require("sim.systems.villages")
  local tick = W.tick
  local ccrop, icrop = W.C.crop, W.I.crop
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      if v.harvest_new then
        local h = v.harvests
        if #h >= 3 then
          local s = 0
          for k = 1, #h do s = s + h[k] end
          local avg = s / #h
          if avg > 0 and v.last_harvest < cfg.crop_failure * avg and record then
            local parent = v.harvest_tag
            local pct = floor(100 * (1 - v.last_harvest / avg) + 0.5)
            local id = events.emit(W, "crop_failure", v.cell, parent, pct,
              { village = v.id, harvest = v.last_harvest, avg = avg })
            if id ~= 0 then
              v.cause = id
              v.plenty = 0
              local farms = v.farms
              for k = 1, #farms do
                local c = farms[k]
                if ccrop[c] == parent then ccrop[c] = id; icrop[c] = 0 end
              end
            end
          end
        end
        vilsys.push_harvest(v)
      end
      local fs = v.food_security
      if not v.famine then
        if fs < vcfg.famine_fs and tick - v.famine_last >= cfg.famine_cooldown then
          v.famine = true
          v.famine_last = tick
          if record then
            local id = events.emit(W, "famine", v.cell, v.cause, v.pop, { village = v.id })
            if id ~= 0 then v.cause = id; v.plenty = 0 end
          end
        end
      elseif (v.fs_low or fs) > vcfg.famine_exit then
        v.famine = false   -- over only when the hungry gap has closed, not at the next harvest
      end
      if v.deaths >= cfg.deaths_spike and record then
        local parent = v.death_tag ~= 0 and v.death_tag or v.cause
        events.emit(W, "deaths_spike", v.cell, parent, v.deaths, { village = v.id })
      end
      v.deaths = 0
      v.death_tag = 0
    end
  end
end

-- Drought: a region's land soil water < drought_soil and < drought_anom × its normal for a season.
local function drought(W, cfg, record)
  local R = W.regions
  local D = W.D.soil
  local soil, cs, is = W.F.soil_water, W.C.soil_water, W.I.soil_water
  local area = W.grid.area
  local ids = R.land_ids
  for q = 1, #ids do
    local r = ids[q]
    local cells = R.lcells[r]
    local m = #cells
    local s = 0
    for i = 1, m do local c = cells[i]; s = s + soil[c] * area[c] end
    local la = R.larea[r]
    local cur = la > 0 and s / la or 0
    local a = D.avg[r]
    if a < 0 then a = cur end
    -- only regions that are normally wetter than the drought line (deserts aren't in drought)
    if cur < cfg.drought_soil and cur < cfg.drought_anom * a and a >= cfg.drought_soil then
      D.months[r] = D.months[r] + 1
    else
      D.months[r] = 0
    end
    if D.on[r] == 0 then
      if D.months[r] >= cfg.drought_months then
        D.on[r] = 1
        if record then
          local parent = dominant(W, cells, m, cs, cfg.dominant_share)
          local id = emit_region(W, "drought", r, parent, cur, { soil = cur })
          if id ~= 0 then retag(cells, m, cs, is, soil, parent, id) end
        end
      end
    elseif cur >= cfg.drought_exit or cur >= 0.9 * a then
      D.on[r] = 0
    end
    D.avg[r] = a + (cur - a) / cfg.avg_months
  end
end

function sys.update(W)
  local cfg = cfgmod.DETECT
  local record = W.record_events
  local tick = W.tick
  -- floods (every tick)
  local fl = W.scratch.flood
  if fl and #fl > 0 then
    local last = W.D.flood_last
    local cflow, iflow, cp = W.C.flow, W.I.flow, W.C.precip
    local cd = cfg.flood_cooldown
    for i = 1, #fl do
      local c = fl[i]
      if tick - last[c] >= cd then
        last[c] = tick
        if record then
          local parent = cflow[c]
          if parent == 0 then parent = cp[c] end
          local id = events.emit(W, "flood", c, parent, 1, nil)
          if id ~= 0 then cflow[c] = id; iflow[c] = 0 end
        end
      end
    end
  end
  if tick % 4 ~= 3 then return end
  -- monthly
  if W.regions.n > 0 then
    for i = 1, #SPECIES do fauna_species(W, cfg, SPECIES[i], record) end
    for i = 1, #SPECIES do local_boom(W, cfg, SPECIES[i], record) end
    drought(W, cfg, record)
  end
  if record then oil_detect(W, cfg) end
  fish_collapse(W, cfg, record)
  village_detect(W, cfg, record)
end

return sys
