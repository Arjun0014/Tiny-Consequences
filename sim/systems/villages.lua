-- Villages (02_WORLD_MODEL.md §5.10, system slot 10). Village i updates once a month, on ticks
-- where (tick + i) % 4 == 0, so the cost is spread. People live in sim/systems/people.lua.
--
-- Entity schema (stable; the renderer and the inspector read it):
--   W.ent.villages   array in founding order; ids are indices and never change (abandoned
--                    villages stay in the array with alive = false)
--     id, key_cell, key_tick   rng key = founding cell + founding tick (never shifted)
--     name, cell, culture, continent (landmass id), founded (tick), from (id of the village
--                    its founders came from; nil for worldgen villages)
--     members        array of person ids (living residents, stable order)
--     pop            #members
--     food           store, person-months;  food_security: smoothed months of food
--     farms          array of farm cells (land_use == id; the centre cell has land_use == -id)
--     fishing        array of ocean cells fished (access)
--     boats, houses  (houses ≈ pop / 4, damaged by fire and floods)
--     state          "new" | "thriving" | "struggling" | "famine" | "abandoned"
--     cause          event id: the dominant reason its food fell (03 §3 Entities), 0 = none
--     alive          false once abandoned; abandoned = tick; ruin = ruins still standing
--     capacity       people its actual food production feeds with a comfortable margin
--     food_ratio     production / consumption, ~3-year averages (prod_avg, need_avg)
--     (internal: harvests, last_harvest, harvest_year, plant_year, catch, catch_avg, low_months,
--      famine, plenty, deaths, death_tag, ev_seen, jobs, fish_jobs, harvest_tag, harvest_new,
--      fs_low = depth of the hungry gap: drops with food security, recovers over a year)
--   W.ent.people     id -> person (see people.lua); W.ent.next_person; W.ent.dead_ids
--   W.S.ruins        cell -> tick abandoned (read by the biome classifier); decays after ~20 y
--   W.S.villages     counters: founded, abandoned (after Year 0), cap_hits
--   W.S.blight       hook (Chapter 2 Blight power): farm cell -> cause tag of the infection; blighted
--                    crops die back (spread between farms/villages arrives with the power)
-- Derived (not saved): W.vgeo[id] = { ring = cells (farm candidates, nearest first),
--   fishing = ocean cells, near = set of cells within the "affects the village" radius,
--   shift = ticks from the planet clock to the village's farming calendar (24 in the south) }
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local blame = require("sim.causality.blame")
local events = require("sim.causality.events")
local ledger = require("sim.ledger")
local fields = require("sim.fields")
local people = require("sim.systems.people")
local names = require("content.names")

local sys = { name = "villages" }
local floor, ceil, max, min = math.floor, math.ceil, math.max, math.min

local FCROP, FFISH = fields.BY_NAME.crop, fields.BY_NAME.fish

function sys.init(W)
  local E = W.ent
  E.villages = E.villages or {}
  E.people = E.people or {}
  E.next_person = E.next_person or 1
  E.dead_ids = E.dead_ids or {}
  W.S.villages = W.S.villages or { founded = 0, abandoned = 0, cap_hits = 0 }
  W.S.ruins = W.S.ruins or {}
  W.scratch.vil_tags = W.scratch.vil_tags or {}
end

local function radius(W, steps)
  local r = floor(steps / W.sscale + 0.5)
  if r < 1 then r = 1 end
  return r
end

-- Cells within r steps of c (BFS), ordered by distance then index. filter(j) optional.
local function rings(W, c, r)
  local g = W.grid
  local dist = { [c] = 0 }
  local list = { c }
  local head = 1
  while head <= #list do
    local x = list[head]; head = head + 1
    if dist[x] < r then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if dist[j] == nil then dist[j] = dist[x] + 1; list[#list + 1] = j end
      end
    end
  end
  table.sort(list, function(a, b2)
    if dist[a] ~= dist[b2] then return dist[a] < dist[b2] end
    return a < b2
  end)
  return list, dist
end
sys.rings = rings

-- Ticks to add to the planet clock to get a village's own farming calendar: the season labels
-- are northern (02 §1), so southern villages plant half a year later, in their own spring.
function sys.season_shift(W, cell)
  return W.grid.lat[cell] < 0 and 24 or 0
end

local function geometry(W, v)
  local cfg = cfgmod.VILLAGE
  local ocean = W.geo.ocean
  local cells = rings(W, v.cell, radius(W, cfg.ring))
  local ring, fishing = {}, {}
  for i = 1, #cells do
    local c = cells[i]
    if ocean[c] == 1 then fishing[#fishing + 1] = c
    elseif c ~= v.cell then ring[#ring + 1] = c end
  end
  local near = {}
  local nc = rings(W, v.cell, radius(W, cfg.near))
  for i = 1, #nc do near[nc[i]] = true end
  return { ring = ring, fishing = fishing, near = near, shift = sys.season_shift(W, v.cell) }
end

function sys.rebuild(W)
  W.vgeo = {}
  local vs = W.ent.villages
  if not vs then return end
  for i = 1, #vs do W.vgeo[i] = geometry(W, vs[i]) end
end

-- How warm a cell's growing season is for crops, 0..1: the crop temperature bell at summer
-- warmth (mean + 0.7 x the seasonal amplitude, in whichever hemisphere).
local function growing_warmth(W, c)
  local cfg = cfgmod.VILLAGE
  local T = W.temp_static
  local lo, hi = cfg.crop_t[1], cfg.crop_t[2]
  local ts = T.base[c] + T.lapse[c] + W.G.g_temp + T.amp[c] * 0.7
  local x = (ts - (lo + hi) * 0.5) / ((hi - lo) * 0.5)
  local tf = 1 - x * x
  if tf < 0 then tf = 0 end
  return min(1, tf * 1.6)
end
sys.growing_warmth = growing_warmth

-- How good a cell is to live on (worldgen settle, new villages). -1 = unusable. Soil, water,
-- open land, a coast or a river, scaled by how well crops grow there in summer.
function sys.site_score(W, c)
  local F = W.F
  local geo = W.geo
  if geo.ocean[c] == 1 or F.land_use[c] ~= 0 or F.ice[c] > 0.3 then return -1 end
  local T = W.temp_static
  local tm = T.base[c] + T.lapse[c] + W.G.g_temp
  if tm < 2 or tm > 27 then return -1 end
  local s = F.fertility[c] + 0.5 * min(F.soil_water[c], 0.8) + 0.4 * F.grass[c] - 0.2 * F.forest[c]
  if F.soil_water[c] < 0.12 then s = s - 0.5 end
  if geo.coast[c] == 1 then s = s + 0.25 end
  if (W.S.hydro and W.S.hydro.flow_mean[c] or 0) > cfgmod.HYDRO.river_threshold then s = s + 0.25 end
  if W.S.ruins[c] then s = s - 0.3 end
  if s > 0 then s = s * (0.4 + 0.6 * growing_warmth(W, c)) end
  return s
end

-- Far enough from every living village?
function sys.free_site(W, c)
  local g = W.grid
  local vs = W.ent.villages
  local lim = math.cos(cfgmod.VILLAGE.min_spacing * 0.07553)
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      local e = v.cell
      if g.X[c] * g.X[e] + g.Y[c] * g.Y[e] + g.Z[c] * g.Z[e] > lim then return false end
    end
  end
  return true
end

-- Found a village at `cell`. opts: name, culture. Clears the centre's forest (co2 -> villagers).
function sys.found(W, cell, opts)
  local cfg = cfgmod.VILLAGE
  local vs = W.ent.villages
  local id = #vs + 1
  local v = {
    id = id, key_cell = cell, key_tick = W.tick, name = opts.name or "?", cell = cell,
    culture = opts.culture or 1, continent = W.geo.continent[cell], founded = W.tick,
    members = {}, pop = 0, food = 0, food_security = cfg.start_food, farms = {}, fishing = {},
    boats = 0, houses = 0, state = "new", cause = 0, alive = true, abandoned = false, ruin = false,
    capacity = 0, harvests = {}, last_harvest = 0, harvest_year = -999999, plant_year = -999999,
    catch = 0, catch_avg = 0, low_months = 0, famine = false, plenty = 0, deaths = 0, death_tag = 0,
    ev_seen = W.E.next_id - 1, jobs = {}, fish_jobs = 0, harvest_tag = 0, harvest_new = false,
    famine_last = -1000000,
  }
  vs[id] = v
  W.vgeo = W.vgeo or {}
  W.vgeo[id] = geometry(W, v)
  for i = 1, #W.vgeo[id].fishing do v.fishing[i] = W.vgeo[id].fishing[i] end
  local F = W.F
  F.land_use[cell] = -id
  local fo = F.forest[cell]
  if fo > 0 then
    local co2 = fo * cfg.k_clear_co2
    W.G.co2_src = (W.G.co2_src or 0) + co2
    ledger.add(W, "co2", co2, 0, "villagers")
    F.forest[cell] = 0
    W.P.forest[cell] = 0
  end
  if W.S.ruins[cell] then W.S.ruins[cell] = nil end
  if W.record_events then W.S.villages.founded = W.S.villages.founded + 1 end
  return v
end

-- Estimate of the people the village's land and sea can feed (yearly).
local function capacity(W, v, geom)
  local cfg = cfgmod.VILLAGE
  local F = W.F
  local ss2 = W.sscale * W.sscale
  local food = 0
  local ring = geom.ring
  for i = 1, #ring do
    local c = ring[i]
    local lu = F.land_use[c]
    if (lu == 0 or lu == v.id) and F.ice[c] < 0.5 then
      local f = F.fertility[c]
      local tf = growing_warmth(W, c)
      food = food + cfg.yield * ss2 * 0.75 * tf * (0.3 + 0.7 * f) * min(1, F.soil_water[c] / 0.3)
    end
  end
  local fishing = v.fishing
  for i = 1, #fishing do
    food = food + 12 * F.fish[fishing[i]] * cfg.fish_q * ss2 * cfg.fishing_cap_share * 0.25
  end
  return food / (12 * 0.9 * cfg.comfort)
end

sys.capacity = capacity

-- Rough people-fed estimate for a would-be village at cell c (settle shares people by it).
function sys.site_capacity(W, c)
  local probe = { id = -1, cell = c }
  local geom = geometry(W, probe)
  probe.fishing = geom.fishing
  return capacity(W, probe, geom)
end

-- Move person p from village a to village b.
local function move(W, p, a, b)
  local m = b.members
  m[#m + 1] = p.id
  b.pop = #m
  p.village = b.id
  if p.job ~= "child" and p.job ~= "elder" then p.job = "child"; people.set_job(W, b, p) end
end

-- A family: an adult (from a village-month roll over the members, so not always the founders;
-- no older than max_age when given), their partner if here, and their children.
local function family(W, v, taken, max_age)
  local P = W.ent.people
  local m = v.members
  local cfg = cfgmod.PEOPLE
  local nm = #m
  local off = floor(rng.float(W.seed, rng.S.MIGRATION, v.key_cell, W.tick, 0) * nm)
  for ii = 1, nm do
    local i = (ii + off - 1) % nm + 1
    local p = P[m[i]]
    if not taken[p.id] and p.job ~= "child" and (not max_age or people.age(W, p) <= max_age) then
      local fam = { p }
      taken[p.id] = true
      if p.partner ~= 0 and P[p.partner] and P[p.partner].village == v.id and not taken[p.partner] then
        fam[#fam + 1] = P[p.partner]
        taken[p.partner] = true
      end
      for k = 1, #m do
        local q = m[k]
        local c = P[q]
        if not taken[q] and people.age(W, c) < cfg.adult_age and (c.mother == p.id or c.father == p.id
          or (p.partner ~= 0 and (c.mother == p.partner or c.father == p.partner))) then
          fam[#fam + 1] = c
          taken[q] = true
        end
      end
      return fam
    end
  end
  return nil
end

-- Best free site among land cells passing filter(c): the highest site score whose land and sea
-- could feed at least `min_people` (sys.site_capacity; checked lazily, best score first).
-- Falls back to the best-scoring site when none can (worldgen must place its villages).
function sys.pick_site(W, filter, min_people, fallback)
  local land = W.geo.land_cells
  local cand, score = {}, {}
  for i = 1, #land do
    local c = land[i]
    if filter(c) then
      local s = sys.site_score(W, c)
      if s > -1 and sys.free_site(W, c) then cand[#cand + 1] = c; score[c] = s end
    end
  end
  table.sort(cand, function(a, b)
    if score[a] ~= score[b] then return score[a] > score[b] end
    return a < b
  end)
  for i = 1, #cand do
    if sys.site_capacity(W, cand[i]) >= min_people then return cand[i] end
  end
  if fallback then return cand[1] end
  return nil
end

-- Best free site for migrants (same landmass, or any coastal site when they have boats).
function sys.best_site(W, continent, coastal)
  local geo = W.geo
  return sys.pick_site(W, function(c)
    return geo.continent[c] == continent or (coastal and geo.coast[c] == 1)
  end, cfgmod.VILLAGE.min_site_people)
end

local function emit(W, kind, cell, parent, mag, data)
  if not W.record_events then return 0 end
  return events.emit(W, kind, cell, parent, mag, data)
end

-- A group from village v founds a new village at `site`; `parent` is the migration event.
local function found_from(W, v, group, site, parent)
  local cfg = cfgmod.VILLAGE
  local vs = W.ent.villages
  local ctr = 0
  local key = rng.hash(W.seed, rng.S.NAMES, site, W.tick, 77)
  local function u()
    ctr = ctr + 1
    return rng.float(W.seed, rng.S.NAMES, key, ctr, 0)
  end
  local used = {}
  for i = 1, #vs do used[vs[i].name] = true end
  local name = names.unique(u, function(uu) return names.new_village(uu, v.culture, v.name) end, used)
  local nv = sys.found(W, site, { name = name, culture = v.culture })
  nv.from = v.id
  -- the founders bring what food they can carry
  local share = v.pop > 0 and v.food * #group / v.pop or 0
  v.food = v.food - share
  nv.food = share
  for i = 1, #group do move(W, group[i], v, nv) end
  people.compact(W, v)
  nv.food_security = cfg.start_food
  local fid = emit(W, "village_founded", site, parent, #group, { village = nv.id, from = v.id })
  nv.cause = 0
  if fid ~= 0 then nv.founded_event = fid end
  return nv
end

-- Famine-driven migration: one family per month leaves.
local function migrate(W, v)
  local cfg = cfgmod.VILLAGE
  local vs = W.ent.villages
  local taken = {}
  local fam = family(W, v, taken)
  if not fam then return end
  local coastal = #v.fishing > 0
  -- nearest village with food to spare (same landmass, or coast to coast)
  local g = W.grid
  local dest, bd = nil, -2
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u ~= v and u.food_security > cfg.migrate_to_fs
      and (u.continent == v.continent or (coastal and #u.fishing > 0)) then
      local d = g.X[u.cell] * g.X[v.cell] + g.Y[u.cell] * g.Y[v.cell] + g.Z[u.cell] * g.Z[v.cell]
      if d > bd then dest, bd = u, d end
    end
  end
  local parent = v.cause
  if dest then
    for i = 1, #fam do move(W, fam[i], v, dest) end
    people.compact(W, v)
    emit(W, "migration", v.cell, parent, #fam, { from = v.id, to = dest.id })
    return
  end
  -- nobody to take them in: gather a founding group (≤ half the village)
  local group = fam
  while #group < cfg.min_founders and #group < v.pop / 2 do
    local f2 = family(W, v, taken)
    if not f2 then break end
    for i = 1, #f2 do group[#group + 1] = f2[i] end
  end
  local site = #group >= cfg.min_founders and sys.best_site(W, v.continent, coastal) or nil
  local mid = emit(W, "migration", v.cell, parent, #group, { from = v.id, to = 0 })
  if site then
    found_from(W, v, group, site, mid)
    return
  end
  -- wanderers: too few to found anything; they rarely make it
  for i = 1, #group do
    local p = group[i]
    people.kill(W, v, p, "wander", mid ~= 0 and mid or parent)
  end
  people.compact(W, v)
end

-- Daughter villages: a big, well-fed village whose land is filling up (its food margin has
-- fallen to where births slow) sends young families to found a village on free land that can
-- feed them. The natural counterpart of abandonment: the human footprint spreads over time.
local function bud(W, v)
  local cfg = cfgmod.VILLAGE
  v.bud_last = W.tick
  local living = 0
  local vs = W.ent.villages
  for i = 1, #vs do if vs[i].alive then living = living + 1 end end
  if living >= cfg.max_villages then return end
  local site = sys.best_site(W, v.continent, #v.fishing > 0 and v.boats > 0)
  if not site then return end
  local want = floor(v.pop * cfg.bud_share)
  if want < cfg.min_founders then want = cfg.min_founders end
  local taken, group = {}, {}
  while #group < want do
    local f = family(W, v, taken, cfg.bud_age)
    if not f then break end
    for i = 1, #f do group[#group + 1] = f[i] end
  end
  if #group < cfg.min_founders then return end
  local mid = emit(W, "migration", v.cell, 0, #group, { from = v.id, to = 0, colonists = true })
  found_from(W, v, group, site, mid)
end

local function abandon(W, v)
  local F = W.F
  v.alive = false
  v.state = "abandoned"
  v.abandoned = W.tick
  v.ruin = true
  v.houses = 0
  v.boats = 0
  W.S.ruins[v.cell] = W.tick
  F.land_use[v.cell] = 0
  for i = 1, #v.farms do
    local c = v.farms[i]
    if F.land_use[c] == v.id then F.land_use[c] = 0; F.crop[c] = 0 end
  end
  v.farms = {}
  local FS = W.S.fauna
  if FS then
    local geom = W.vgeo[v.id]
    for i = 1, #geom.ring do
      local c = geom.ring[i]
      FS.hunt[c], FS.cull[c] = 0, 0
    end
    FS.hunt[v.cell], FS.cull[v.cell] = 0, 0
  end
  if W.record_events then W.S.villages.abandoned = W.S.villages.abandoned + 1 end
  emit(W, "village_abandoned", v.cell, v.cause, 1, { village = v.id })
end

local function farm_score(F, c)
  -- open grassland first: clearing forest is hard work (and costs the planet its trees)
  return F.fertility[c] + 0.5 * min(F.soil_water[c], 0.6) + 0.2 * F.grass[c] - 0.8 * F.forest[c]
end

-- Keep the farm list at the size the farmers can work (best free ring cells).
local function land_use(W, v, geom, farmers, need)
  local cfg = cfgmod.VILLAGE
  local F = W.F
  local lu, fert, soil, ice, forest, grass = F.land_use, F.fertility, F.soil_water, F.ice, F.forest, F.grass
  local ss2 = W.sscale * W.sscale
  local want = ceil(farmers * cfg.k_farm / ss2 - 0.01)
  -- hungry villages clear more fields (as far as their farmers can work them)
  local yc = v.yield_cell or 0
  if yc > 1 then
    local other = 12 * ((v.catch_avg or 0) + (v.meat or 0))
    local short = 12 * need * cfg.comfort - other
    local by_food = ceil(short / yc - 0.01)
    local by_labour = floor(farmers * cfg.farmer_max / yc)
    if by_food > by_labour then by_food = by_labour end
    if by_food > want then want = by_food end
  end
  -- fields within walking distance: at most max_farm_l4 level-4 cells of them
  local maxf = floor(cfg.max_farm_l4 / ss2 + 0.5)
  if maxf < 1 then maxf = 1 end
  if want > maxf then want = maxf end
  if farmers > 0 and want < 1 then want = 1 end
  local farms = v.farms
  -- drop farms that are no longer ours / usable
  local k = 0
  for i = 1, #farms do
    local c = farms[i]
    if lu[c] == v.id and ice[c] < 0.5 then k = k + 1; farms[k] = c end
  end
  for i = #farms, k + 1, -1 do farms[i] = nil end
  local ring = geom.ring
  local fresh = cfg.fallow_fert + 0.08
  -- shifting cultivation (between seasons): an exhausted field returns to the wild when a
  -- fresher one is free
  local toy = (W.tick + geom.shift) % 48
  if toy >= cfg.harvest_toy or toy < cfg.plant_toy then
    for i = #farms, 1, -1 do
      local c = farms[i]
      if fert[c] < cfg.fallow_fert then
        local alt = false
        for j = 1, #ring do
          local d = ring[j]
          if lu[d] == 0 and ice[d] < 0.5 and soil[d] > 0.08 and fert[d] >= fresh then alt = true; break end
        end
        if alt then
          lu[c] = 0
          F.crop[c] = 0
          table.remove(farms, i)
        end
      end
    end
  end
  if #farms == want then return end
  while #farms > want do
    local wi, ws = 1, math.huge
    for i = 1, #farms do
      local s = farm_score(F, farms[i])
      if s < ws then wi, ws = i, s end
    end
    local c = farms[wi]
    lu[c] = 0
    F.crop[c] = 0
    table.remove(farms, wi)
  end
  -- new fields: fertile ones; a hungry village also breaks poor ground
  local min_fert = v.food_security < cfg.fallow_fs and cfg.poor_fert or cfg.fallow_fert
  while #farms < want do
    local best, bs = nil, -math.huge
    for i = 1, #ring do
      local c = ring[i]
      if lu[c] == 0 and ice[c] < 0.5 and soil[c] > 0.08 and fert[c] >= min_fert then
        local s = farm_score(F, c)
        if s > bs then best, bs = c, s end
      end
    end
    if not best then break end
    lu[best] = v.id
    farms[#farms + 1] = best
    -- clearing the land for fields: forest -> co2 (villagers)
    local fo = forest[best]
    if fo > 0 then
      local co2 = fo * cfg.k_clear_co2
      W.G.co2_src = (W.G.co2_src or 0) + co2
      ledger.add(W, "co2", co2, 0, "villagers")
      forest[best] = 0
      W.P.forest[best] = 0
      blame.set(W.C.forest, W.I.forest, best, v.cause)
    end
  end
end

-- Crops: plant in spring, grow through summer, damage, harvest from harvest_toy.
local function crops(W, v, farmers)
  local cfg = cfgmod.VILLAGE
  local F, C, I = W.F, W.C, W.I
  local crop, temp, soil, fert, oil = F.crop, F.temp, F.soil_water, F.fertility, F.oil
  local ccrop, icrop = C.crop, I.crop
  -- the village's own calendar: southern villages plant in their spring, half a year later
  local tick = W.tick + W.vgeo[v.id].shift
  local toy = tick % 48
  local year = floor(tick / 48)
  local farms = v.farms
  local income = 0
  local blight = W.S.blight
  local flood_last = W.D.flood_last
  if toy < cfg.plant_toy then
    if v.plant_year ~= year then
      v.plant_year = year
      -- an exhausted field rests for a year, unless the store is too low to afford it
      local rest = v.food_security >= cfg.fallow_fs and cfg.fallow_fert or 0
      for i = 1, #farms do
        local c = farms[i]
        if fert[c] >= rest then
          crop[c] = cfg.crop_seed
          if ccrop[c] ~= 0 then ccrop[c] = 0; icrop[c] = 0 end
        else
          crop[c] = 0   -- fallow this year
          if ccrop[c] ~= 0 then ccrop[c] = 0; icrop[c] = 0 end
        end
      end
    end
  elseif toy < cfg.harvest_toy then
    if v.plant_year == year then
      local dt = 4
      local lo, hi = cfg.crop_t[1], cfg.crop_t[2]
      local mid, half = (lo + hi) * 0.5, (hi - lo) * 0.5
      local sh = cfg.crop_soil_half
      for i = 1, #farms do
        local c = farms[i]
        local c0 = crop[c]
        if c0 > 0 then
          local t = temp[c]
          local x = (t - mid) / half
          local tf = 1 - x * x
          if tf < 0 then tf = 0 end
          local s = soil[c]
          local wf = (s / (s + sh)) * (1 + sh)
          if wf > 1 then wf = 1 end
          local grow = dt * cfg.r_crop * tf * wf * (0.3 + 0.7 * fert[c]) * c0 * (1 - c0)
          local dry = 0
          if s < cfg.drought_soil then dry = dt * cfg.k_drought * c0 end
          local frost = 0
          if t < cfg.frost_temp then frost = dt * cfg.k_frost * c0 end
          local oilx = 0
          local o = oil[c]
          if o > 0 then oilx = dt * cfg.k_oil_crop * o * c0 end
          local to = C.oil[c]
          local bt = blight and blight[c]
          if bt then oilx = oilx + dt * cfg.k_blight * c0; to = bt end
          -- a river flood this month drowns part of the standing crop (the flood's tag)
          if flood_last and W.tick - flood_last[c] < 4 then oilx = oilx + cfg.k_flood_crop * c0; to = C.flow[c] end
          local c1 = c0 + grow - dry - frost - oilx
          if c1 < 0 then c1 = 0 elseif c1 > 1 then c1 = 1 end
          crop[c] = c1
          local ts, tt = C.soil_water[c], C.temp[c]
          if ccrop[c] ~= 0 or ts ~= 0 or tt ~= 0 or to ~= 0 then
            blame.step(ccrop, icrop, c, c1 - c0, FCROP.eps, FCROP.hold, grow, 0, -dry, ts, -frost, tt, -oilx, to)
          end
        end
      end
    end
  elseif v.harvest_year ~= year then
    v.harvest_year = year
    local ss2 = W.sscale * W.sscale
    local amount = 0
    local dom = W.scratch.vil_tags
    local ntag = 0
    for i = 1, #farms do
      local c = farms[i]
      local c0 = crop[c]
      amount = amount + c0 * cfg.yield * ss2
      local f = fert[c] - cfg.deplete * c0
      if f < 0 then f = 0 end
      fert[c] = f
      crop[c] = 0
      local t = ccrop[c]
      if t ~= 0 then ntag = ntag + 1; dom[ntag] = t end
    end
    if #farms > 0 then
      local yc = amount / #farms
      v.yield_cell = (v.yield_cell or yc) * 0.5 + yc * 0.5
    end
    local cap = farmers * cfg.farmer_max
    if amount > cap then amount = cap end
    income = amount
    -- the harvest's cause: the most frequent crop tag among the farms (fire, drought, frost, oil…)
    local best, bc = 0, 0
    for i = 1, ntag do
      local t = dom[i]
      local n = 0
      for j = 1, ntag do if dom[j] == t then n = n + 1 end end
      if n > bc or (n == bc and t < best) then best, bc = t, n end
    end
    v.harvest_tag = best
    v.last_harvest = amount
    v.harvest_new = true
  end
  return income
end

-- Fishing: catch = min(Σ fish × q, fishers × max); the catch comes out of the fish.
local function fishing(W, v, fishers)
  local cfg = cfgmod.VILLAGE
  local cells = v.fishing
  if #cells == 0 then return 0, 0 end
  local F, C, I, P = W.F, W.C, W.I, W.P
  local fish, cfish, ifish = F.fish, C.fish, I.fish
  local ss2 = W.sscale * W.sscale
  local stock = 0
  for i = 1, #cells do stock = stock + fish[cells[i]] end
  stock = stock * cfg.fish_q * ss2
  local want = fishers * cfg.fisher_max
  local catch = want < stock and want or stock
  v.fish_stock = stock
  if catch <= 0 then return 0, stock end
  local frac = catch / stock
  local tag = v.cause
  for i = 1, #cells do
    local c = cells[i]
    local f0 = fish[c]
    local d = f0 * cfg.fish_q * frac / cfg.fish_units
    local f1 = f0 - d
    if f1 < 0 then f1 = 0 end
    fish[c] = f1
    P.fish[c] = f1
    if tag ~= 0 or cfish[c] ~= 0 then blame.step(cfish, ifish, c, -d, FFISH.eps, FFISH.hold, -d, tag, 0, 0, 0, 0, 0, 0) end
  end
  return catch, stock
end

-- Hunting grazers and culling predators near the farms (through the fauna hooks).
local function hunting(W, v, geom, hunters)
  local cfg = cfgmod.VILLAGE
  local FS = W.S.fauna
  if not FS then return 0 end
  local F = W.F
  local gz, pd = F.grazers, F.predators
  local ss2 = W.sscale * W.sscale
  local ring = geom.ring
  local n = #ring + 1
  local hf = cfg.hunt * hunters / (n * ss2)
  if hf > 0.02 then hf = 0.02 end
  local meat = 0
  local tag = v.cause
  local pred = pd[v.cell]
  for i = 0, #ring do
    local c = i == 0 and v.cell or ring[i]
    FS.hunt[c] = hf
    FS.hunt_tag[c] = tag
    meat = meat + gz[c] * hf * 4 * cfg.meat_units * ss2
    pred = pred + (i > 0 and pd[c] or 0)
  end
  -- wolves near the fields get culled now and then
  local cull = 0
  if pred / n > cfg.cull_threshold and rng.float(W.seed, rng.S.CULL, v.key_cell, W.tick, 0) < cfg.cull_prob then
    cull = cfg.cull_rate
  end
  for i = 0, #ring do
    local c = i == 0 and v.cell or ring[i]
    FS.cull[c] = cull
    FS.cull_tag[c] = tag
  end
  return meat
end

-- New events near/about the village become memories of everyone living there.
local SAL_OWN = { famine = 0.9, crop_failure = 0.6, deaths_spike = 0.8, migration = 0.5,
  fish_collapse = 0.6, overfishing = 0.5, oil_beached = 0.7, village_founded = 0.5 }
local SAL_NEAR = { fire_started = 0.4, wildfire = 0.7, flood = 0.6, oil_beached = 0.7, tsunami = 1.0,
  eruption = 1.0, quake = 0.8, meteor_natural = 1.0, lightning_strike = 0.3, drought = 0.5 }
local function memories(W, v, geom)
  local E = W.E
  local last = E.next_id - 1
  if v.ev_seen >= last then return end
  local P = W.ent.people
  local m = v.members
  local near = geom.near
  for id = v.ev_seen + 1, last do
    local r = E.rec[id]
    if r then
      local sal = 0
      local d = r.data
      if type(d) == "table" and (d.village == v.id or d.from == v.id) then sal = SAL_OWN[r.kind] or 0.4
      elseif near[r.cell] then
        if r.actor == "player" and r.parent == 0 then sal = 0.8
        else sal = SAL_NEAR[r.kind] or 0 end
      end
      if sal > 0 then
        for i = 1, #m do people.remember(P[m[i]], id, sal) end
      end
    end
  end
  v.ev_seen = last
end

local function month(W, v, pop_total)
  local cfg = cfgmod.VILLAGE
  local pcfg = cfgmod.PEOPLE
  local F, C = W.F, W.C
  local tick = W.tick
  local geom = W.vgeo[v.id]
  local P = W.ent.people
  -- demography
  local m = v.members
  local adults, children, farmers, fishers, hunters = 0, 0, 0, 0, 0
  for i = 1, #m do
    local p = P[m[i]]
    local j = p.job
    if j == "child" then children = children + 1 else adults = adults + 1 end
    if j == "farmer" then farmers = farmers + 1
    elseif j == "fisher" then fishers = fishers + 1
    elseif j == "woodcutter" then hunters = hunters + 1 end
  end
  local need = adults * cfg.eat_adult + children * cfg.eat_child
  -- 1. land use and crops
  land_use(W, v, geom, farmers, need)
  local harvest = crops(W, v, farmers)
  -- 2. fishing (desired fishers follow the sustainable catch)
  local catch, stock = fishing(W, v, fishers)
  local working = adults
  local fj = floor((stock or 0) / cfg.fisher_max)
  local fmax = floor(working * cfg.fish_share)
  if fj > fmax then fj = fmax end
  v.fish_jobs = fj
  -- 3. hunting / culling
  local meat = hunting(W, v, geom, hunters)
  v.meat = meat
  -- 4. wood burning (co2, the slow human climate driver) and nearby forest use
  local t = F.temp[v.cell]
  local cold = -t / 10
  if cold < 0 then cold = 0 elseif cold > 2 then cold = 2 end
  local co2 = v.pop * cfg.k_burn * (1 + cold)
  W.G.co2_src = (W.G.co2_src or 0) + co2
  ledger.add(W, "co2", co2, 0, "villagers")
  do
    local ring = geom.ring
    local wood = cfg.wood * v.pop * 4 / (#ring + 1)
    local removed = 0
    for i = 0, #ring do
      local c = i == 0 and v.cell or ring[i]
      local fo = F.forest[c]
      if fo > 0 then
        local d = fo < wood and fo or wood
        F.forest[c] = fo - d
        removed = removed + d
      end
    end
    if removed > 0 then
      local k = removed * cfgmod.VEG.k_co2
      W.G.co2_src = W.G.co2_src + k
      ledger.add(W, "co2", k, 0, "villagers")
    end
  end
  -- village fires (hearths): rare, likelier when dry
  local fc = cfgmod.HYDRO.field_capacity
  local dry = 1 - F.soil_water[v.cell] / fc
  if dry > 0 and rng.float(W.seed, rng.S.VILLAGE, v.key_cell, tick, 1) < cfg.accident * v.pop * dry then
    local target = v.farms[1] or v.cell
    require("sim.systems.fire").ignite(W, target, 0, "village_accident")
  end
  -- 5. food: income, consumption, shortfall
  local food = v.food + harvest + catch + meat
  local granary = cfg.granary * need
  if food > granary then food = food - (food - granary) * cfg.spoil end
  v.catch = catch
  v.catch_avg = v.catch_avg + (catch - v.catch_avg) / 12
  local shortfall = 0
  if food >= need then food = food - need
  else
    shortfall = need > 0 and (need - food) / need or 0
    food = 0
  end
  v.food = food
  -- food history (D-025): what the village really produces vs what it eats, ~3-year EMAs.
  -- Their ratio is its food margin; capacity = the people that production feeds with comfort.
  local kh = 1 / cfg.hist_months
  local pa = v.prod_avg or need * cfg.comfort
  local na = v.need_avg or need
  pa = pa + (harvest + catch + meat - pa) * kh
  na = na + (need - na) * kh
  v.prod_avg, v.need_avg = pa, na
  local rho = pa / (na > 1 and na or 1)
  v.food_ratio = rho
  v.capacity = v.pop * rho / cfg.comfort
  -- cause of falling food: failed harvest or failing catch (the tag of the dominant loss)
  local cand = 0
  if v.harvest_new and v.harvest_tag ~= 0 then cand = v.harvest_tag end
  if fishers > 0 and catch < 0.75 * v.catch_avg then
    local cells = v.fishing
    local best, bn = 0, 0
    for i = 1, #cells do
      local tg = C.fish[cells[i]]
      if tg ~= 0 then
        local n = 0
        for j = 1, #cells do if C.fish[cells[j]] == tg then n = n + 1 end end
        if n > bn then best, bn = tg, n end
      end
    end
    if best ~= 0 then cand = best end
  end
  if cand ~= 0 and cand ~= v.cause and not blame.descends(W.E.parent, v.cause, cand) then
    v.cause = cand
    v.plenty = 0
  end
  -- 6. exposure (fire in the village, a flood this month)
  local exposure = nil
  if F.fire[v.cell] > 0 then
    exposure = { kind = "fire", p = 0.01 * F.fire[v.cell], tag = C.fire[v.cell] }
    v.houses = floor(v.houses * 0.7)
  elseif W.D.flood_last and tick - W.D.flood_last[v.cell] < 4 then
    exposure = { kind = "flood", p = 0.004, tag = C.flow[v.cell] }
    local h0 = v.houses
    v.houses = floor(v.houses * 0.85)
    -- the river comes through the village: news (a flood-prone village makes it every few years)
    if tick - (v.flooded or -1000000) >= cfg.flood_news_years * 48 then
      v.flooded = tick
      emit(W, "flooded_village", v.cell, C.flow[v.cell], h0 - v.houses, { village = v.id })
    end
  end
  -- 7. people: hunger, health, deaths, pairing, births
  -- births follow the real food margin (a floor: families keep having children in lean
  -- years; acute hunger — the hungry gap — stops them in people.month)
  local crowd = (rho - cfg.birth_rho[1]) / (cfg.birth_rho[2] - cfg.birth_rho[1])
  if crowd > 1 then crowd = 1 elseif crowd < 0 then crowd = 0 end
  crowd = cfg.birth_floor + (1 - cfg.birth_floor) * crowd
  local nonage, dtag = people.month(W, v, {
    shortfall = shortfall, starve_tag = v.cause, exposure = exposure, fs = v.fs_low or v.food_security,
    crowd = crowd, pop_total = pop_total,
  })
  v.deaths = v.deaths + nonage
  if nonage > 0 and dtag ~= 0 then v.death_tag = dtag end
  -- 8. food security and state
  local need2 = need > 1 and need or 1
  local fs = v.food_security + (food / need2 - v.food_security) * cfg.fs_smooth
  v.food_security = fs
  -- depth of the hungry gap: a minimum that drops at once and recovers over a year
  local lo = v.fs_low or fs
  if fs < lo then lo = fs else lo = lo + (fs - lo) / 12 end
  v.fs_low = lo
  if lo < cfg.famine_fs then v.low_months = v.low_months + 1 else v.low_months = 0 end
  if lo > cfg.famine_fs and not v.famine then v.plenty = v.plenty + 1 else v.plenty = 0 end
  if v.plenty >= cfg.cause_hold then v.cause = 0 end
  if v.famine then v.state = "famine"
  elseif tick - v.founded < 96 then v.state = "new"
  elseif fs < 3 or shortfall > 0 then v.state = "struggling"
  else v.state = "thriving" end
  -- houses ≈ pop / 4 (rebuilt one a month), boats ∝ fishers
  local hw = ceil(v.pop / cfg.houses_per)
  if v.houses < hw then v.houses = v.houses + 1 elseif v.houses > hw + 2 then v.houses = v.houses - 1 end
  v.boats = ceil(fishers / cfgmod.SHIPS.fishers_per_boat)
  -- 9. memories, migration, abandonment
  memories(W, v, geom)
  -- families leave in acute hunger (a deep hungry gap for months) or a chronic deficit
  if rho < cfg.migrate_rho and tick - v.founded > 3 * 48 then v.chronic = (v.chronic or 0) + 1 else v.chronic = 0 end
  if v.pop > 0 and (v.low_months >= cfg.migrate_months or v.chronic >= cfg.chronic_months) then
    migrate(W, v)
    v.chronic = 0
  end
  if v.pop == 0 then abandon(W, v) return end
  -- 10. daughter villages (a monthly roll while the village is big, fed and filling its land)
  if v.pop >= cfg.bud_pop and not v.famine and fs >= cfg.bud_fs and rho < cfg.bud_rho
    and tick - (v.bud_last or v.founded) >= cfg.bud_years * 48 and pop_total < cfg.bud_total
    and rng.float(W.seed, rng.S.MIGRATION, v.key_cell, tick, 3) < cfg.bud_prob then
    bud(W, v)
  end
end

-- Yearly housekeeping: capacities, harvest history, memories fade, ruins decay, prune the dead.
local function yearly(W)
  local cfg = cfgmod.VILLAGE
  local vs = W.ent.villages
  local P = W.ent.people
  local f = cfgmod.PEOPLE.memory_fade
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      local m = v.members
      for k = 1, #m do people.fade_memories(P[m[k]], f) end
    elseif v.ruin and W.tick - v.abandoned >= cfg.ruins_years * 48 then
      v.ruin = false
      if W.S.ruins[v.cell] == v.abandoned then W.S.ruins[v.cell] = nil end
    end
  end
  people.prune(W)
end

function sys.update(W)
  local vs = W.ent.villages
  local nv = #vs
  if nv == 0 then return end
  local tick = W.tick
  if tick % 48 == 0 then yearly(W) end
  local total = 0
  for i = 1, nv do
    local v = vs[i]
    if v.alive then total = total + v.pop end
  end
  for i = 1, nv do
    local v = vs[i]
    if v.alive and (tick + i) % 4 == 0 then month(W, v, total) end
  end
end

-- Year-boundary snap (before the save capture): people's continuous state is rounded.
function sys.snap(W)
  people.snap(W)
end

-- After the harvest was checked by the detectors: roll the 5-year history.
function sys.push_harvest(v)
  local h = v.harvests
  h[#h + 1] = v.last_harvest
  if #h > 5 then table.remove(h, 1) end
  v.harvest_new = false
end

function sys.shift_ticks(W, dt)
  local vs = W.ent.villages
  if not vs then return end
  local dy = floor(dt / 48)
  for i = 1, #vs do
    local v = vs[i]
    v.founded = v.founded + dt
    v.harvest_year = v.harvest_year + dy
    v.plant_year = v.plant_year + dy
    v.famine_last = v.famine_last + dt
    if v.abandoned then v.abandoned = v.abandoned + dt end
    if v.bud_last then v.bud_last = v.bud_last + dt end
    if v.flooded then v.flooded = v.flooded + dt end
  end
  local P = W.ent.people
  local ids = {}
  for id in pairs(P) do ids[#ids + 1] = id end
  table.sort(ids)
  for i = 1, #ids do
    local p = P[ids[i]]
    p.born = p.born + dt
    if p.dead then p.dead = p.dead + dt end
  end
  local ruins = W.S.ruins
  local cells = {}
  for c in pairs(ruins) do cells[#cells + 1] = c end
  table.sort(cells)
  for i = 1, #cells do ruins[cells[i]] = ruins[cells[i]] + dt end
end

return sys
