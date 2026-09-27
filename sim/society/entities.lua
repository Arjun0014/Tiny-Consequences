-- Settlement entities and their bookkeeping (docs/world/10 §7, 12). Creation, keys, membership,
-- death, moves, and the compact save form of people.
--
--   W.ent.villages[id]     array (ids = indices; abandoned villages stay with alive = false)
--   W.ent.people[id]       map id -> person        (W.ent.next_person)
--   W.ent.households[id]   map id -> household     (W.ent.next_household)
--   W.ent.buildings[id]    map id -> building      (W.ent.next_building)
--   W.ent.boats[id]        map id -> fishing boat  (W.ent.next_boat)
--   W.ent.parties[id]      map id -> travel party  (W.ent.next_party)
-- Keys (rng) come from where and when an entity was created, never from the id counters
-- (05 §5): a person = (village founding cell, birth hour, index), a household = (village key,
-- formation hour, index), a building = (village key, lot, start hour), a boat = (village key,
-- build hour, index).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local sites = require("sim.society.sites")
local layout = require("sim.society.layout")
local daylight = require("sim.society.daylight")
local ledger = require("sim.ledger")

local M = {}
local floor = math.floor

M.TRAITS = { "optimist", "doomsayer", "hoarder", "devout", "skeptic", "romantic", "grump" }

function M.init(W)
  local E = W.ent
  E.villages = E.villages or {}
  E.people = E.people or {}
  E.next_person = E.next_person or 1
  E.households = E.households or {}
  E.next_household = E.next_household or 1
  E.buildings = E.buildings or {}
  E.next_building = E.next_building or 1
  if type(E.boats) ~= "table" or (E.boats[1] and not E.boats[1].id) then E.boats = {} end
  E.next_boat = E.next_boat or 1
  E.parties = E.parties or {}
  E.next_party = E.next_party or 1
  E.dead_ids = E.dead_ids or {}
  W.S.society = W.S.society or { founded = 0, abandoned = 0, births = 0, deaths = 0, built = 0,
    migrated = 0, cap_hits = 0 }
  W.S.farm = W.S.farm or {}
  W.S.ruins = W.S.ruins or {}
end

local function vkey(v) return v.key_cell * 7919 + (v.key_hour % 100003) end
M.vkey = vkey

-- Age in whole years at hour h (default now).
function M.age(W, p, h)
  return floor(((h or W.hour) - p.born) / 8064)
end

function M.need_of(W, p)
  local cfg = cfgmod.SOCIETY
  local a = M.age(W, p)
  if a < cfg.adult_age then return cfg.eat.child end
  if a >= cfg.elder_age then return cfg.eat.elder end
  return cfg.eat.adult
end

-- Villages -----------------------------------------------------------------------------------

-- Found a village at `cell`. opts: name, culture, from. Clears the centre (forest -> CO2).
function M.found_village(W, cell, opts)
  local vs = W.ent.villages
  local id = #vs + 1
  local g = W.grid
  local v = {
    id = id, key_cell = cell, key_hour = W.hour, name = opts.name or "?", cell = cell,
    culture = opts.culture or 1, continent = W.geo.continent[cell], founded = W.hour, from = opts.from,
    alive = true, abandoned = false, ruin = false,
    bucket = daylight.midnight_bucket(g.lon[cell]),
    members = {}, pop = 0, households = {}, buildings = {}, boats = {}, farms = {}, land = { cell },
    stock = { grain = 0, fresh = 0, wood = 0, seed = 0 },
    foodtag = { 0, 0, 0 }, foodidle = { 0, 0, 0 },   -- cause tags of the food (1) and wood (3) stocks
    norm = { grain_in = 0, fresh_in = 0, wood_in = 0, food_out = 0, wood_out = 0, imports = 0 },
    ration = 1, ration_sum = 0, ration_n = 0, food_days = 60, wood_days = 60, food_security = 2,
    state = "new", cause = 0, labour_tag = 0, import_tag = 0,
    season = { phase = "winter", year = -1, week = 0, t_prev = 99 },
    harvests = {}, last_harvest = 0, harvest_tag = 0, harvest_new = false, harvest_acc = 0,
    births = 0, deaths = 0, death_tag = 0, famine = false, famine_last = -1000000, famine_weeks = 0,
    rationing = false, low_days = 0, seed_eaten_year = -1, workforce_norm = 0,
    ev_seen = W.E.next_id - 1, plan = { groups = {}, day = -1 }, need_day = 0, households_fuel = 0,
    fishing = {}, week_offset = id % 7, month_offset = id % 4,
    acc = {},   -- this year's flows: harvest, catch, meat, eaten, spoiled, wood cut/burned, hours by task
  }
  vs[id] = v
  local fish, mooring = sites.fishing_cells(W, cell)
  v.fishing, v.mooring = fish, mooring
  if mooring then v.pier_lot = layout.facing_lot(W, v, cell, mooring) end
  local F = W.F
  F.land_use[cell] = -id
  local fo = F.forest[cell]
  if fo > 0 then
    local co2 = fo * cfgmod.VEG.k_co2
    W.G.co2_src = (W.G.co2_src or 0) + co2
    ledger.add(W, "co2", co2, 0, "villagers")
    F.forest[cell] = 0
    W.P.forest[cell] = 0
  end
  if W.S.ruins[cell] then W.S.ruins[cell] = nil end
  if W.record_events then W.S.society.founded = W.S.society.founded + 1 end
  return v
end

-- Households ---------------------------------------------------------------------------------

function M.new_household(W, v, home)
  local E = W.ent
  local id = E.next_household
  E.next_household = id + 1
  local h = { id = id, key = rng.hash(W.seed, rng.S.HOUSEHOLD, vkey(v), W.hour, #v.households),
    village = v.id, home = home or 0, members = {}, formed = W.hour }
  E.households[id] = h
  v.households[#v.households + 1] = id
  if home and home ~= 0 then E.buildings[home].household = id end
  return h
end

-- Remove a household from its village's list (it is empty or merged); its home becomes vacant.
function M.drop_household(W, h)
  local E = W.ent
  local v = E.villages[h.village]
  if v then
    local l = v.households
    for i = #l, 1, -1 do if l[i] == h.id then table.remove(l, i) end end
  end
  if h.home ~= 0 and E.buildings[h.home] and E.buildings[h.home].household == h.id then
    E.buildings[h.home].household = 0
  end
  E.households[h.id] = nil
end

local function remove_id(list, id)
  for i = #list, 1, -1 do
    if list[i] == id then table.remove(list, i); return true end
  end
  return false
end
M.remove_id = remove_id

-- People -------------------------------------------------------------------------------------

function M.person_key(W, v, born, idx)
  return rng.hash(W.seed, rng.S.PERSON, v.key_cell, born, idx * 65536 + (v.key_hour % 65536))
end

-- Create a person in village v, household h. opts: sex, born (hour), mother, father, idx.
function M.new_person(W, v, h, opts)
  local E = W.ent
  local id = E.next_person
  E.next_person = id + 1
  local born = opts.born or W.hour
  local key = M.person_key(W, v, born, opts.idx or 0)
  local ctr = 0
  local seed = W.seed
  local function u()
    ctr = ctr + 1
    return rng.float(seed, rng.S.NAMES, key, ctr, 0)
  end
  local sex = opts.sex or (u() < 0.5 and "f" or "m")
  local mother = opts.mother and E.people[opts.mother]
  local father = opts.father and E.people[opts.father]
  local surname
  if father then surname = father.surname elseif mother then surname = mother.surname
  else surname = names.surname(u, v.culture) end
  local t1 = M.TRAITS[1 + floor(u() * #M.TRAITS)]
  local traits = { t1 }
  if u() < 0.4 then
    local t2 = M.TRAITS[1 + floor(u() * #M.TRAITS)]
    if t2 ~= t1 then traits[2] = t2 end
  end
  local p = {
    id = id, key = key, name = names.person(u, v.culture, sex), surname = surname, sex = sex,
    born = born, village = v.id, household = h and h.id or 0, job = "child", task = "", site = 0,
    nutrition = 1, health = 100, partner = 0, betrothed = 0,
    mother = opts.mother or 0, father = opts.father or 0, children = 0, traits = traits,
    mem = {}, mood = 0, preg_due = 0, nursing_until = 0, away = 0, homeless = 0,
    u = floor(rng.float(seed, rng.S.PERSON, key, 1, 0) * 1048576) / 1048576,
  }
  E.people[id] = p
  v.members[#v.members + 1] = id
  v.pop = #v.members
  if h then h.members[#h.members + 1] = id end
  if mother then mother.children = mother.children + 1 end
  if father then father.children = father.children + 1 end
  return p
end

-- Add a memory (event id, salience) keeping the most salient ones.
function M.remember(p, ev, sal)
  local mem = p.mem
  local n = #mem
  for i = 1, n, 2 do
    if mem[i] == ev then
      if sal > mem[i + 1] then mem[i + 1] = sal end
      return
    end
  end
  if n < 2 * cfgmod.PEOPLE.memories then
    mem[n + 1] = ev
    mem[n + 2] = sal
    return
  end
  local wi, ws = 0, sal
  for i = 1, n, 2 do
    if mem[i + 1] < ws then wi, ws = i, mem[i + 1] end
  end
  if wi > 0 then mem[wi] = ev; mem[wi + 1] = sal end
end

function M.fade_memories(p, f)
  local mem = p.mem
  local k = 0
  for i = 1, #mem, 2 do
    local s = mem[i + 1] * f
    if s >= 0.05 then
      k = k + 2
      mem[k - 1] = mem[i]
      mem[k] = s
    end
  end
  for i = #mem, k + 1, -1 do mem[i] = nil end
end

-- A person dies (cause, tag = the event behind it). Lists are compacted by the caller
-- (M.compact) — deaths happen inside loops over members.
function M.kill(W, p, cause, tag)
  if p.dead then return end
  local E = W.ent
  p.dead = W.hour
  p.died_of = cause
  p.death_tag = tag or 0
  if p.partner ~= 0 then
    local q = E.people[p.partner]
    if q and not q.dead then q.partner = 0 end
  end
  if p.betrothed ~= 0 then
    local q = E.people[p.betrothed]
    if q and not q.dead then q.betrothed = 0 end
  end
  local d = E.dead_ids
  d[#d + 1] = p.id
  local h = E.households[p.household]
  if h then remove_id(h.members, p.id) end
  -- a mother nursing a baby who dies stops nursing
  if p.mother ~= 0 and M.age(W, p) < 1 then
    local m = E.people[p.mother]
    if m and not m.dead then m.nursing_until = 0 end
  end
  local v = E.villages[p.village]
  if v then
    v.deaths = v.deaths + 1
    if tag and tag ~= 0 and cause ~= "age" then v.death_tag = tag end
    W.S.society.deaths = W.S.society.deaths + 1
  end
  -- family grief: a personal memory of the cause for the partner and parents
  if tag and tag ~= 0 then
    local sal = cfgmod.PEOPLE.grief_salience
    for _, rid in ipairs({ p.partner, p.mother, p.father }) do
      local q = rid ~= 0 and E.people[rid]
      if q and not q.dead then M.remember(q, tag, sal) end
    end
  end
end

-- Drop the dead and those who moved away from v.members (order kept).
function M.compact(W, v)
  local P = W.ent.people
  local m = v.members
  local k = 0
  local vid = v.id
  for i = 1, #m do
    local id = m[i]
    local p = P[id]
    if p and not p.dead and p.village == vid then k = k + 1; m[k] = id end
  end
  for i = #m, k + 1, -1 do m[i] = nil end
  v.pop = k
end

-- Move person p into village v2 (household h2, may be nil = homeless there for now).
function M.move_person(W, p, v2, h2)
  local E = W.ent
  local h = E.households[p.household]
  if h then remove_id(h.members, p.id) end
  local v = E.villages[p.village]
  if v and v ~= v2 then remove_id(v.members, p.id); v.pop = #v.members end
  if v ~= v2 then
    v2.members[#v2.members + 1] = p.id
    v2.pop = #v2.members
  end
  p.village = v2.id
  p.household = h2 and h2.id or 0
  if h2 then h2.members[#h2.members + 1] = p.id end
  p.task, p.site = "", 0
  if p.preg_due > 0 and v ~= v2 then
    local pl = v2.preg
    if not pl then pl = {}; v2.preg = pl end
    pl[#pl + 1] = p.id
  end
end

-- Buildings ----------------------------------------------------------------------------------

-- Occupied (cell, lot) pairs of a village, as a set keyed cell * 64 + lot.
function M.occupied_lots(W, v)
  local B = W.ent.buildings
  local used = {}
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state ~= "gone" then used[b.cell * 64 + b.lot] = true end
  end
  if v.pier_lot then used[v.cell * 64 + v.pier_lot] = true end
  return used
end

-- The next free lot (innermost first) on the village's land; claims a neighbouring land cell as
-- village land when all lots are taken. Returns cell, lot (nil when there is no room at all).
function M.free_lot(W, v, kind)
  if kind == "pier" and v.pier_lot then return v.cell, v.pier_lot end
  local used = M.occupied_lots(W, v)
  for i = 1, #v.land do
    local c = v.land[i]
    for lot = 1, layout.LOTS do
      if not used[c * 64 + lot] then return c, lot end
    end
  end
  -- expand: the best neighbouring free land cell next to any village cell
  local F = W.F
  local g = W.grid
  local best, bs = nil, -1e9
  for i = 1, #v.land do
    local c = v.land[i]
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local j = g.nbr[b + k]
      if W.geo.ocean[j] == 0 and F.land_use[j] == 0 and F.ice[j] < 0.5 then
        local s = F.fertility[j] * -0.3 - F.forest[j] * 0.5 - j * 1e-6
        if s > bs then best, bs = j, s end
      end
    end
  end
  if not best then return nil end
  v.land[#v.land + 1] = best
  F.land_use[best] = -v.id
  local fo = F.forest[best]
  if fo > 0 then
    -- the clearing's timber goes to the village store
    v.stock.wood = v.stock.wood + fo * cfgmod.SOCIETY.forest_wu * 0.4
    F.forest[best] = 0
    W.P.forest[best] = 0
  end
  v.expanded = W.hour
  return best, 1
end

-- New building (kind) on (cell, lot). state: "site" (progress 0) or "standing".
function M.new_building(W, v, kind, cell, lot, state)
  local E = W.ent
  local id = E.next_building
  E.next_building = id + 1
  local b = { id = id, key = rng.hash(W.seed, rng.S.BUILD, vkey(v), lot * 4096 + (cell % 4096), W.hour),
    village = v.id, kind = kind, cell = cell, lot = lot, state = state or "site",
    progress = state == "standing" and 1 or 0, wood_used = 0, condition = 1, household = 0,
    started = W.hour, built = state == "standing" and W.hour or -1 }
  E.buildings[id] = b
  v.buildings[#v.buildings + 1] = id
  return b
end

function M.capacity_of(b)
  local cfg = cfgmod.SOCIETY
  if b.kind == "house" then return cfg.house_capacity end
  if b.kind == "hut" then return cfg.hut_capacity end
  return 0
end

-- Boats --------------------------------------------------------------------------------------

function M.new_boat(W, v)
  local E = W.ent
  local id = E.next_boat
  E.next_boat = id + 1
  local b = { id = id, key = rng.hash(W.seed, rng.S.BOAT, vkey(v), W.hour, #v.boats), village = v.id,
    home = v.mooring or v.cell, state = "moored", crew = {}, trip = false, condition = 1,
    built = W.hour, catch = 0 }
  E.boats[id] = b
  v.boats[#v.boats + 1] = id
  return b
end

-- Save form of people: positional records, same data (field order is part of the save format).
local JOBS = { "child", "farmer", "fisher", "woodcutter", "builder", "elder" }
local JOB_ID = {}
for i = 1, #JOBS do JOB_ID[JOBS[i]] = i end

-- Fields stored positionally; any other key a person carries (wait_since, ...) travels in a trailing
-- map, so the save can never silently drop state (a dropped key breaks reload determinism).
local PACKED = { id = 1, key = 1, name = 1, surname = 1, sex = 1, born = 1, village = 1, job = 1, health = 1,
  nutrition = 1, partner = 1, mother = 1, father = 1, children = 1, traits = 1, mem = 1, mood = 1, u = 1,
  dead = 1, died_of = 1, death_tag = 1, household = 1, task = 1, site = 1, betrothed = 1, preg_due = 1,
  nursing_until = 1, away = 1, homeless = 1 }

function M.pack_people(map)
  local ids = {}
  for id in pairs(map) do ids[#ids + 1] = id end
  table.sort(ids)
  local out = {}
  for i = 1, #ids do
    local p = map[ids[i]]
    local extra = false
    for k, x in pairs(p) do
      if not PACKED[k] then
        extra = extra or {}
        extra[k] = x
      end
    end
    out[i] = { p.id, p.key, p.name, p.surname, p.sex == "f" and 1 or 0, p.born, p.village,
      JOB_ID[p.job] or 1, p.health, p.nutrition, p.partner, p.mother, p.father, p.children,
      table.concat(p.traits, ","), p.mem, p.mood, p.u, p.dead or false, p.died_of or "", p.death_tag or 0,
      p.household, p.task, p.site, p.betrothed, p.preg_due, p.nursing_until, p.away, p.homeless, extra }
  end
  return out
end

function M.unpack_people(list)
  local map = {}
  for i = 1, #list do
    local r = list[i]
    local traits = {}
    for t in r[15]:gmatch("[^,]+") do traits[#traits + 1] = t end
    local p = { id = r[1], key = r[2], name = r[3], surname = r[4], sex = r[5] == 1 and "f" or "m",
      born = r[6], village = r[7], job = JOBS[r[8]], health = r[9], nutrition = r[10], partner = r[11],
      mother = r[12], father = r[13], children = r[14], traits = traits, mem = r[16], mood = r[17],
      u = r[18], household = r[22], task = r[23], site = r[24], betrothed = r[25], preg_due = r[26],
      nursing_until = r[27], away = r[28], homeless = r[29] }
    if r[19] then p.dead = r[19]; p.died_of = r[20]; p.death_tag = r[21] end
    local extra = r[30]
    if extra then
      local keys = {}
      for k in pairs(extra) do keys[#keys + 1] = k end
      table.sort(keys)
      for k = 1, #keys do p[keys[k]] = extra[keys[k]] end
    end
    map[p.id] = p
  end
  return map
end

-- Yearly: prune the long dead (kept for family trees and the inspector).
function M.prune(W)
  local E = W.ent
  local d = E.dead_ids
  local keep = cfgmod.PEOPLE.keep_dead_years * 8064
  local k = 0
  for i = 1, #d do
    local p = E.people[d[i]]
    if p and W.hour - p.dead > keep then
      E.people[d[i]] = nil
    else
      k = k + 1
      d[k] = d[i]
    end
  end
  for i = #d, k + 1, -1 do d[i] = nil end
end

return M
