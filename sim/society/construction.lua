-- Construction (docs/world/11_ECONOMY.md §5). Buildings go up on lots from timber and labour:
-- a site is a building with state "site" and progress 0..1; builders' hours advance it only while
-- wood is in the store (wood is drawn as the work proceeds). Demand comes from real needs:
-- households without a home, betrothed couples waiting for one, crowded households, a pier for a
-- fishing village, boats for idle fishers, repairs, storage. Houses decay and need repairs.
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")

local M = {}
local floor, min, max = math.floor, math.min, math.max

local PRIO = { house = 65, hut = 70, pier = 55, boat = 50, granary = 30, woodshed = 25, repair = 35 }
M.PRIO = PRIO

-- Housing need: households without a home + couples waiting + crowded households.
function M.housing_need(W, v)
  local E = W.ent
  local P, H, B = E.people, E.households, E.buildings
  local homeless, crowded = 0, 0
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then
      if h.home == 0 or not B[h.home] or B[h.home].state ~= "standing" then homeless = homeless + 1
      elseif #h.members > ents.capacity_of(B[h.home]) then crowded = crowded + 1 end
    end
  end
  local couples = 0
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.betrothed ~= 0 and p.sex == "f" then couples = couples + 1 end
  end
  return homeless, couples, crowded
end

local function count_kind(W, v, kind, state)
  local B = W.ent.buildings
  local n = 0
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.kind == kind and (not state or b.state == state) then n = n + 1 end
  end
  return n
end
M.count_kind = count_kind

local function vacant_houses(W, v)
  local B = W.ent.buildings
  local n = 0
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state == "standing" and (b.kind == "house" or b.kind == "hut") and b.household == 0 then n = n + 1 end
  end
  return n
end
M.vacant_houses = vacant_houses

-- Weekly: decide what to build and open sites (at most max_sites at once).
function M.plan(W, v)
  local cfg = cfgmod.SOCIETY
  local B = W.ent.buildings
  local sites_open = 0
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state == "site" then sites_open = sites_open + 1 end
  end
  local max_sites = cfg.max_sites_base + floor(v.pop / cfg.sites_per_pop)
  local function open(kind)
    if sites_open >= max_sites then return false end
    local cell, lot
    if kind == "boat" then
      if not v.pier_lot then return false end
      cell, lot = v.cell, 0   -- boats are built at the pier (lot 0 = the square side of it)
    else
      cell, lot = ents.free_lot(W, v, kind)
      if not cell then return false end
    end
    local b = ents.new_building(W, v, kind, cell, lot, "site")
    -- no woods within reach: houses are built of earth
    if kind == "house" and v.timber_near == false then b.material = "earth" end
    sites_open = sites_open + 1
    return b
  end
  -- 1. a pier for a fishing village
  if v.mooring and count_kind(W, v, "pier") == 0 then open("pier") end
  -- 2. homes: households without one, couples waiting, crowded households (vacant houses first)
  local homeless, couples, crowded = M.housing_need(W, v)
  if (v.capacity or 1e9) < v.pop * 1.05 then couples = 0 end
  local want = homeless + couples + floor(crowded * 0.5 + 0.5) - vacant_houses(W, v)
  local building = count_kind(W, v, "house", "site") + count_kind(W, v, "hut", "site")
  local guard = 0
  while want - building > 0 and guard < 4 do
    guard = guard + 1
    -- founders put up quick huts first when they have no roof at all
    local kind = (homeless > 0 and count_kind(W, v, "house", "standing") + count_kind(W, v, "hut", "standing") == 0) and "hut" or "house"
    if not open(kind) then break end
    building = building + 1
  end
  -- 3. boats for fishers without one (a pier is needed)
  if v.pier_lot and count_kind(W, v, "pier", "standing") > 0 then
    local fishers = 0
    local P = W.ent.people
    for i = 1, #v.members do if P[v.members[i]].job == "fisher" then fishers = fishers + 1 end end
    local boats = #v.boats + count_kind(W, v, "boat", "site")
    if (fishers > boats * cfg.crew or boats == 0) and #v.fishing > 0 then open("boat") end
  end
  -- 4. storage when stocks overflow (at most a granary per 40 people: surplus beyond that rots)
  local gcap, wcap = M.capacities(W, v)
  if v.stock.grain > gcap * 0.9 and count_kind(W, v, "granary", "site") == 0
    and count_kind(W, v, "granary") < 1 + floor(v.pop / 40) then open("granary") end
  if v.stock.wood > wcap * 0.9 and count_kind(W, v, "woodshed", "site") == 0
    and count_kind(W, v, "woodshed") < 1 + floor(v.pop / 60) then open("woodshed") end
end

-- Storage capacities from one pass over the buildings: grain, wood.
function M.capacities(W, v)
  local cfg = cfgmod.SOCIETY
  local B = W.ent.buildings
  local g, w = 200, 100
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state == "standing" then
      local k = b.kind
      if k == "house" then g = g + cfg.store_house_grain; w = w + cfg.store_house_wood
      elseif k == "hut" then g = g + cfg.store_house_grain * 0.5; w = w + cfg.store_house_wood
      elseif k == "granary" then g = g + cfg.store_granary
      elseif k == "woodshed" then w = w + cfg.store_woodshed end
    end
  end
  return g, w
end
function M.grain_capacity(W, v) local g = M.capacities(W, v); return g end
function M.wood_capacity(W, v) local _, w = M.capacities(W, v); return w end

-- Building spec: an earth house (sod / mud brick) needs little timber but more labour.
local EARTH = {}
function M.spec(b)
  local cfg = cfgmod.SOCIETY
  if b.material == "earth" then
    EARTH.wood, EARTH.h = cfg.earth_wood, cfg.earth_h
    return EARTH
  end
  return cfg.build[b.kind]
end

-- Work still needed on a site / repair: hours and wood.
function M.remaining(b)
  local cfg = cfgmod.SOCIETY
  if b.state == "site" then
    local spec = M.spec(b)
    return (1 - b.progress) * spec.h, (1 - b.progress) * spec.wood
  end
  if b.repair then
    local spec = cfg.build.repair
    return (1 - b.repair) * spec.h, (1 - b.repair) * spec.wood
  end
  return 0, 0
end

-- Builders' effective hours on building b: returns hours used. Wood comes from the store.
function M.work(W, v, b, hours)
  local cfg = cfgmod.SOCIETY
  local spec
  if b.state == "site" then spec = M.spec(b) elseif b.repair then spec = cfg.build.repair else return 0 end
  local done = b.state == "site" and b.progress or b.repair
  local h = min(hours, (1 - done) * spec.h)
  local wood_per_h = spec.wood / spec.h
  local wood = h * wood_per_h
  if wood > v.stock.wood then
    -- stalled: no timber in the store (the site waits; the shortage carries the wood tag)
    wood = max(0, v.stock.wood)
    h = wood / wood_per_h
    v.site_stalled = W.hour
  end
  v.stock.wood = v.stock.wood - wood
  v.day_wood_used = (v.day_wood_used or 0) + wood
  done = done + h / spec.h
  if b.state == "site" then
    b.progress = min(1, done)
    b.wood_used = b.wood_used + wood
    if b.progress >= 0.999 then M.complete(W, v, b) end
  else
    b.repair = done
    if done >= 0.999 then
      b.condition = min(1, b.condition + cfg.repair_gain)
      b.repair = nil
    end
  end
  return h
end

-- A building is finished.
function M.complete(W, v, b)
  b.progress = 1
  b.built = W.hour
  W.S.society.built = W.S.society.built + 1
  if b.kind == "boat" then
    -- boats are vessels, not buildings: launch it and clear the site
    ents.new_boat(W, v)
    b.state = "gone"
    ents.remove_id(v.buildings, b.id)
    W.ent.buildings[b.id] = nil
    return
  end
  b.state = "standing"
  b.condition = 1
  if b.kind == "house" or b.kind == "hut" then
    require("sim.society.life").house_ready(W, v, b)
    if W.record_events then events.emit(W, "house_built", b.cell, v.housing_tag or 0, 1, { village = v.id, building = b.id }) end
  end
end

-- Weekly: decay; repairs are requested below the threshold; a house at 0 collapses.
function M.decay(W, v)
  local cfg = cfgmod.SOCIETY
  local B = W.ent.buildings
  local H = W.ent.households
  for i = #v.buildings, 1, -1 do
    local b = B[v.buildings[i]]
    if b and b.state == "standing" then
      local rate = (b.kind == "hut" and cfg.hut_decay or cfg.house_decay) / 48
      b.condition = b.condition - rate
      if b.condition < cfg.repair_below and not b.repair then b.repair = 0 end
      if b.condition <= 0 then
        b.state = "ruin"
        b.ruined = W.hour
        b.repair = nil
        local h = H[b.household]
        if h then h.home = 0 end
        b.household = 0
      end
    end
  end
end

-- Damage (fire, flood, meteor): condition loss; destroyed buildings become ruins.
function M.damage(W, v, share, cell)
  local B = W.ent.buildings
  local H = W.ent.households
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and (b.state == "standing" or b.state == "site") and (not cell or b.cell == cell) then
      if b.state == "site" then b.progress = b.progress * (1 - share)
      else
        b.condition = b.condition - share
        if b.condition <= 0 then
          b.state = "ruin"
          b.ruined = W.hour
          local h = H[b.household]
          if h then h.home = 0 end
          b.household = 0
        end
      end
    end
  end
end

return M
