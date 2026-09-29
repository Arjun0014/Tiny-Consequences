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
--                slick ≥ 10 level-4-equivalent cells), oil_beached (oil ashore), drought
--                (connected land whose soil water has been below drought_anom × its own
--                seasonal 5-year normal for drought_months, D-036).
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
      D.fauna[sp] = { avg = f(nr, -1), state = f(nr, 0), last = f(nr, -1000000), ext = f(nr, 0), gext = 0,
        ev = f(nr, 0), news = f(nr, -1000000) }
    end
    -- drought: per-cell seasonal soil-water normals (5-year EMAs), dry-month counters, on flags
    D.soil = { n1 = f(n, -1), n2 = f(n, -1), n3 = f(n, -1), n4 = f(n, -1), dry = f(n, 0), on = f(n, 0) }
    D.fish_low = f(n, 0)
    D.flood_last = f(n, -1000000)
  end
  -- heatwave / cold snap: +1 / -1 while a cell is in one (added in Stage 2.2)
  D.heat_on = D.heat_on or f(n, 0)
  -- sea ice: 1 while a sea cell is frozen over (ice >= sea_ice_on)
  D.sea_ice = D.sea_ice or f(n, 0)
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
    local nw = D.fauna[sp].news
    if nw then for r = 1, #nw do nw[r] = nw[r] + dt end end
  end
  local fl = D.flood_last
  for c = 1, #fl do fl[c] = fl[c] + dt end
end

-- Most frequent non-zero tag among cells[1..m] (from an array list `cells` or scratch queue),
-- if it covers ≥ share of them; else 0. Allocation-free (scratch arrays).
-- wts (optional, by position in cells): each cell's vote weight (e.g. how far its density moved);
-- share is then a share of the total weight instead of the cell count.
local function dominant(W, cells, m, tags, share, wts)
  local tk, tc = W.scratch.det_tk, W.scratch.det_tc
  local k = 0
  local total = m
  if wts then
    total = 0
    for i = 1, m do total = total + wts[i] end
    if total <= 0 then wts, total = nil, m end
  end
  for i = 1, m do
    local t = tags[cells[i]]
    local w = wts and wts[i] or 1
    if t ~= 0 and w > 0 then
      local found = false
      for j = 1, k do
        if tk[j] == t then tc[j] = tc[j] + w; found = true; break end
      end
      if not found then k = k + 1; tk[k] = t; tc[k] = w end
    end
  end
  m = total
  local best, bc = 0, 0
  for j = 1, k do
    if tc[j] > bc or (tc[j] == bc and tk[j] < best) then best, bc = tk[j], tc[j] end
  end
  if bc > 0 and bc >= share * m then return best end
  -- many separate player actions (e.g. a Cull on every wolf pack) jointly caused it: credit
  -- the player's largest one when the player's tags together cover the share
  local ps, pb, pc = 0, 0, 0
  for j = 1, k do
    if W.E and events.is_player_rooted(W, tk[j]) then
      ps = ps + tc[j]
      if tc[j] > pc or (tc[j] == pc and tk[j] < pb) then pb, pc = tk[j], tc[j] end
    end
  end
  if pc > 0 and ps >= share * m then return pb end
  return 0
end
sys.dominant = dominant

local function kind_of(W, id)
  local r = id ~= 0 and W.E.rec[id]
  return r and r.kind or nil
end

-- Re-tag cells whose tag == parent with id (cells with a different cause keep theirs). With
-- parent 0 (a natural event) only cells where the field is present are claimed.
-- A player "volley": several root events of one power cast in the same tick (a Cull on every
-- wolf pack). An event parented to one of them claims the cells tagged by the others too.
local CUR_W = nil
local function same_volley(t, parent)
  local W = CUR_W
  if not W or t == 0 or parent == 0 or t == parent then return t == parent end
  local E = W.E
  if E.parent[t] ~= 0 or E.parent[parent] ~= 0 then return false end
  local a, b = E.rec[t], E.rec[parent]
  return a ~= nil and b ~= nil and a.actor == "player" and a.kind == b.kind and a.tick == b.tick
end

local function retag(cells, m, tags, idle, vals, parent, id)
  for i = 1, m do
    local c = cells[i]
    local t = tags[c]
    if (t == parent or same_volley(t, parent)) and (parent ~= 0 or vals[c] > 0) then tags[c] = id; idle[c] = 0 end
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
      local t = tags[c]
      if t == parent or same_volley(t, parent) then tags[c] = id; idle[c] = 0 end
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
-- Density and normal are both taken over the cell and its ring (D-071): the animals live in
-- herds and packs that move, and a herd walking onto new ground is not a boom — its numbers
-- growing is. A natural local boom is news at most once per news_years in its region, like a
-- regional one.
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
  local ocean = W.geo.ocean
  -- is a land cell's neighbourhood (cell + ring) above thr × its normal? (the sums of density and
  -- normal; asked only of cells whose own normal counts, in regions open to a boom)
  local function above(c)
    local v, nn = vals[c], norm[c]
    local b = c * 6 - 6
    for k = 1, cnt[c] do local j = nbr[b + k]; v = v + vals[j]; nn = nn + norm[j] end
    return v > thr * nn
  end
  local fresh = cfg.news_years * 48
  local state, last, cool = D.state, D.last, cfg.cooldown
  for i = 1, #land do
    local src = land[i]
    local r = reg[src]
    if mark[src] ~= stamp and norm[src] > minn and state[r] == 0 and tick - last[r] >= cool
      and above(src) then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      while head <= m do
        local c = q[head]; head = head + 1
        local b = c * 6 - 6
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if mark[j] ~= stamp and reg[j] == r and norm[j] > minn and ocean[j] == 0 and above(j) then
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
        local opts = { region = r }
        -- (nature's rhythm — a boom rooted in no one's hand, however many booms back — is news
        -- once per news_years here; the player's consequences always are)
        local natural = not events.is_player_rooted(W, parent)
        if natural and tick - D.news[r] < fresh then opts.headline = false end
        local id = events.emit(W, "population_boom", src, parent, ratio,
          { species = sp, ratio = ratio, cells = m, ["local"] = true }, opts)
        if id ~= 0 then
          local er = W.E.rec[id]
          if er and er.headline and natural then D.news[r] = tick end
          for k = 1, m do
            local c = q[k]
            if tags[c] == parent then tags[c] = id; idle[c] = 0 end
          end
        end
      end
    end
  end
end

-- A boom or crash the player's hand drives is news even while the region is in a natural event's
-- cooldown or already in a natural trough/peak (a Cull on wolves that were already scarce): if the
-- cells that moved most carry a player-rooted cause, a new event with that parent is recorded.
-- Returns the new event id or 0.
local function player_turn(W, cfg, sp, r, cells, m, a, ratio, kind, nst)
  local vals, tags = W.F[sp], W.C[sp]
  local area = W.grid.area
  local wts = W.scratch.det_w
  if not wts then wts = {}; W.scratch.det_w = wts end
  for i = 1, m do
    local c = cells[i]
    local dv = (nst == -1) and (a - vals[c]) or (vals[c] - a)
    wts[i] = dv > 0 and dv * area[c] or 0
  end
  local parent = dominant(W, cells, m, tags, cfg.dominant_share, wts)
  if parent == 0 or not events.is_player_rooted(W, parent) then return 0 end
  local rec = W.names.regions[r]
  local id = events.emit(W, kind, rec and rec.centre or 0, parent, ratio, { species = sp, ratio = ratio }, { region = r })
  if id ~= 0 then retag_species(W, cells, m, sp, parent, id, nst, cfg) end
  return id
end

-- Population boom / crash / regional + global extinction for one species.
local function fauna_species(W, cfg, sp, record)
  local R = W.regions
  local D = W.D.fauna[sp]
  local vals, tags, idle = W.F[sp], W.C[sp], W.I[sp]
  local area = W.grid.area
  local avg, state, last, ext, evs, news = D.avg, D.state, D.last, D.ext, D.ev, D.news
  local tick = W.tick
  local E = W.E
  -- news fatigue: a region's natural boom/crash (its predator-prey rhythm) makes the chronicle at
  -- most once per news_years; the events themselves are always recorded (chains, "Why?")
  local fresh = cfg.news_years * 48
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
          evs[r] = 0
          if record then
            -- the cells that moved most decide: a region crashing because a Cull emptied a
            -- few of its cells is the Cull's crash, not the tail of the last natural trough
            local wts = W.scratch.det_w
            if not wts then wts = {}; W.scratch.det_w = wts end
            for i = 1, m do
              local c = cells[i]
              local dv = (nst == -1) and (a - vals[c]) or (vals[c] - a)
              wts[i] = dv > 0 and dv * area[c] or 0
            end
            local parent = dominant(W, cells, m, tags, share, wts)
            local opts = { region = r }
            -- a player tag behind a sizeable share of the change is a contributing cause
            if W.E and not events.is_player_rooted(W, parent) then
              local tot, best, bw = 0, 0, 0
              for i = 1, m do tot = tot + wts[i] end
              if tot > 0 then
                local tk, tw = W.scratch.det_ptk or {}, W.scratch.det_ptw or {}
                W.scratch.det_ptk, W.scratch.det_ptw = tk, tw
                local n = 0
                for i = 1, m do
                  local t = tags[cells[i]]
                  if t ~= 0 and wts[i] > 0 and events.is_player_rooted(W, t) then
                    local f = false
                    for j = 1, n do if tk[j] == t then tw[j] = tw[j] + wts[i]; f = true; break end end
                    if not f then n = n + 1; tk[n], tw[n] = t, wts[i] end
                  end
                end
                for j = 1, n do if tw[j] > bw or (tw[j] == bw and tk[j] < best) then best, bw = tk[j], tw[j] end end
                if best ~= 0 and bw >= 0.15 * tot then opts.co = best end
              end
            end
            local natural = not events.is_player_rooted(W, parent)
            if natural and tick - news[r] < fresh then opts.headline = false end
            local rec = W.names.regions[r]
            local id = events.emit(W, kind, rec and rec.centre or 0, parent, ratio, { species = sp, ratio = ratio }, opts)
            if id ~= 0 then retag_species(W, cells, m, sp, parent, id, nst, cfg) end
            evs[r] = id
            local er = E.rec[id]
            if er and er.headline and natural then news[r] = tick end
          end
        elseif kind and record then
          -- (in a natural event's cooldown: the player's hand is still news)
          local id = player_turn(W, cfg, sp, r, cells, m, a, ratio, kind, nst)
          if id ~= 0 then state[r], last[r], evs[r] = nst, tick, id end
        end
      elseif st == 1 and ratio < cfg.boom_exit then state[r] = 0; evs[r] = 0
      elseif st == -1 and ratio > cfg.crash_exit then state[r] = 0; evs[r] = 0
      elseif evs[r] ~= 0 then
        -- while it lasts, the event records the peak / the trough; a deep one becomes a headline
        -- (an ordinary predator–prey trough stays a quiet event: 03 §5 headline minimums)
        local rec = E.rec[evs[r]]
        -- (a natural trough or peak the player deepens: the deepening is the player's)
        if record and rec and ((st == -1 and ratio < rec.mag * 0.8 and ratio <= cfg.crash)
            or (st == 1 and ratio > rec.mag * 1.25 and ratio >= cfg.boom))
            and not events.is_player_rooted(W, evs[r]) then
          local id = player_turn(W, cfg, sp, r, cells, m, a, ratio, st == -1 and "population_crash" or "population_boom", st)
          if id ~= 0 then last[r], evs[r] = tick, id; rec = E.rec[id] end
        end
        if rec and ((st == -1 and ratio < rec.mag) or (st == 1 and ratio > rec.mag)) then
          rec.mag = ratio
          if type(rec.data) == "table" then rec.data.ratio = ratio end
          local natural = not events.is_player_rooted(W, rec.parent)
          if not rec.headline and (not natural or tick - news[r] >= fresh) then
            events.update_headline(W, rec)
            if rec.headline and natural then news[r] = tick end
          end
        end
      end
    elseif st ~= 0 then
      state[r] = 0
      evs[r] = 0
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
  -- crop_failure, famine, deaths_spike, rationing, seed_eaten, wood_shortage ... are emitted by the
  -- settlement layer itself (sim/society/economy.lua, D-050)
end

-- Drought: a region's land soil water < drought_soil and < drought_anom × its normal for a season.
-- Heatwaves and cold snaps (03 §4): a connected patch where the local heat anomaly holds
-- beyond ±heat_anom for the month (fires, the player's Heat / Chill, eruptions). The event's
-- parent is the anomaly's dominant cause; the patch is re-tagged so what the heat does next
-- (crops cooked, soil dried, sea ice) chains to it.
local function heat_detect(W, cfg, record)
  local D = W.D
  local on = D.heat_on
  local ha, ch, ih = W.F.heat_anom, W.C.heat_anom, W.I.heat_anom
  local thr, exit_ = cfg.heat_anom, cfg.heat_exit
  local sc = W.scratch
  local newl = sc.det_new
  local nnew = 0
  local n = W.n
  for c = 1, n do
    local h = ha[c]
    local s = (h >= thr and 1) or (h <= -thr and -1) or 0
    if s ~= 0 and on[c] ~= s then nnew = nnew + 1; newl[nnew] = c
    elseif on[c] ~= 0 and (h < exit_ and h > -exit_) then on[c] = 0 end
  end
  if nnew == 0 then return end
  local need = ceil(cfg.heat_cells / (W.sscale * W.sscale) - 0.01)
  if need < 2 then need = 2 end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  for s = 1, nnew do
    local src = newl[s]
    local sign = ha[src] > 0 and 1 or -1
    if mark[src] ~= stamp and on[src] ~= sign then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      local sum = 0
      while head <= m do
        local c = q[head]; head = head + 1
        sum = sum + ha[c]
        local b = c * 6 - 6
        for kk = 1, cnt[c] do
          local j = nbr[b + kk]
          if mark[j] ~= stamp and ha[j] * sign >= thr then mark[j] = stamp; m = m + 1; q[m] = j end
        end
      end
      if m >= need then
        for kk = 1, m do on[q[kk]] = sign end
        if record then
          local parent = dominant(W, q, m, ch, cfg.dominant_share)
          local id = events.emit(W, sign > 0 and "heatwave" or "cold_snap", src, parent, floor(sum / m * 10 + 0.5) / 10,
            { cells = m })
          if id ~= 0 then retag(q, m, ch, ih, ha, parent, id) end
        end
      end
    end
  end
end

-- Sea ice forming / melting over a connected stretch of sea (03 §4: fishing grounds under
-- ice, ice bridges). Parent: the sea temperature's cause (a Chill, a cold era).
local function sea_ice_detect(W, cfg, record)
  local D = W.D
  local on = D.sea_ice
  local ice = W.F.ice
  local ocean_cells = W.geo.ocean_cells
  local thr, exit_ = cfg.sea_ice_on, cfg.sea_ice_off
  local sc = W.scratch
  local newl = sc.det_new
  local nnew = 0
  for i = 1, #ocean_cells do
    local c = ocean_cells[i]
    if on[c] == 0 and ice[c] >= thr then nnew = nnew + 1; newl[nnew] = c
    elseif on[c] == 1 and ice[c] < exit_ then on[c] = 0 end
  end
  if nnew == 0 then return end
  local need = ceil(cfg.sea_ice_cells / (W.sscale * W.sscale) - 0.01)
  if need < 2 then need = 2 end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local ocean = W.geo.ocean
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  local csst, isst = W.C.sst, W.I.sst
  local cice, iice = W.C.ice, W.I.ice
  for s = 1, nnew do
    local src = newl[s]
    if mark[src] ~= stamp and on[src] == 0 then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      while head <= m do
        local c = q[head]; head = head + 1
        local b = c * 6 - 6
        for kk = 1, cnt[c] do
          local j = nbr[b + kk]
          if mark[j] ~= stamp and ocean[j] == 1 and ice[j] >= thr then mark[j] = stamp; m = m + 1; q[m] = j end
        end
      end
      for kk = 1, m do on[q[kk]] = 1 end
      -- natural polar sea ice comes and goes every year: only a caused freeze is news
      if record and m >= need then
        local parent = dominant(W, q, m, csst, cfg.dominant_share)
        if parent == 0 then parent = dominant(W, q, m, cice, cfg.dominant_share) end
        if parent ~= 0 then
          local id = events.emit(W, "sea_ice_formed", src, parent, m * W.sscale * W.sscale, { cells = m })
          if id ~= 0 then retag(q, m, cice, iice, ice, 0, id); retag(q, m, cice, iice, ice, parent, id) end
        end
      end
    end
  end
end

local function drought(W, cfg, record)
  local D = W.D.soil
  local soil, cs, is = W.F.soil_water, W.C.soil_water, W.I.soil_water
  local ice = W.F.ice
  local land = W.geo.land_cells
  local season = floor((W.tick % 48) / 12) + 1
  local norm = season == 1 and D.n1 or season == 2 and D.n2 or season == 3 and D.n3 or D.n4
  local dry, on = D.dry, D.on
  local anom, exitk, minn, months = cfg.drought_anom, cfg.drought_exit_anom, cfg.drought_norm_min, cfg.drought_months
  local k = 1 / (cfg.avg_months / 4)      -- a season is sampled 3 months a year: ~5-year memory
  local sc = W.scratch
  local newl = sc.det_new
  local nnew = 0
  for i = 1, #land do
    local c = land[i]
    local s0, nm = soil[c], norm[c]
    if nm < 0 then nm = s0 end
    if nm >= minn and ice[c] < 0.5 and s0 < anom * nm then
      dry[c] = dry[c] + 1
      if on[c] == 0 and dry[c] >= months then nnew = nnew + 1; newl[nnew] = c end
    else
      dry[c] = 0
      if on[c] == 1 and s0 >= exitk * nm then on[c] = 0 end
    end
    norm[c] = nm + (s0 - nm) * k
  end
  if nnew == 0 then return end
  -- connected dry land (a drought has a size); cells join while dry for >= drought_months
  local need = ceil(cfg.drought_cells / (W.sscale * W.sscale) - 0.01)
  if need < 2 then need = 2 end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local ocean = W.geo.ocean
  local mark, q = sc.det_mark, sc.det_q
  local stamp = sc.det_stamp + 1
  sc.det_stamp = stamp
  for s = 1, nnew do
    local src = newl[s]
    if mark[src] ~= stamp and on[src] == 0 then
      mark[src] = stamp
      local m, head = 1, 1
      q[1] = src
      local ssum, nsum = 0, 0
      while head <= m do
        local c = q[head]; head = head + 1
        ssum = ssum + soil[c]; nsum = nsum + norm[c]
        local b = c * 6 - 6
        for kk = 1, cnt[c] do
          local j = nbr[b + kk]
          if ocean[j] == 0 and dry[j] >= months and mark[j] ~= stamp then
            mark[j] = stamp; m = m + 1; q[m] = j
          end
        end
      end
      if m >= need then
        for kk = 1, m do on[q[kk]] = 1 end
        if record then
          local parent = dominant(W, q, m, cs, cfg.dominant_share)
          local pct = nsum > 0 and floor(100 * (1 - ssum / nsum) + 0.5) or 0
          local id = events.emit(W, "drought", src, parent, m * W.sscale * W.sscale,
            { cells = m, drop = pct })
          if id ~= 0 then retag(q, m, cs, is, soil, parent, id) end
        end
      end
    end
  end
end

function sys.update(W)
  CUR_W = W
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
  heat_detect(W, cfg, record)
  sea_ice_detect(W, cfg, record)
  if record then oil_detect(W, cfg) end
  fish_collapse(W, cfg, record)
  village_detect(W, cfg, record)
end

return sys
