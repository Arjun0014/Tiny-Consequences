-- The settlement (docs/v3/10_AGENTS.md §2, §9): buildings, plots, stores, the hourly planner
-- (weather, the open work people can see: the board's summary), the daily round (crops, trees,
-- lanes, fuel, water) and growth (a couple without a home makes a house site).
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local clock = require("sim.agents.clock")
local weather = require("sim.agents.weather")
local people = require("sim.agents.people")
local events = require("sim.causality.events")

local M = {}
local floor, max, min, sqrt, abs, ceil = math.floor, math.max, math.min, math.sqrt, math.abs, math.ceil
local function C() return cfgmod.V3 end
local floods

M.KINDS = {
  house = { w = 4, h = 4, label = "house", logs = 18, work = 110 },
  granary = { w = 4, h = 4, label = "granary", logs = 22, work = 140 },
  woodstore = { w = 4, h = 3, label = "woodstore", logs = 8, work = 40 },
  well = { w = 1, h = 1, label = "well", logs = 0, work = 30 },
  dock = { w = 1, h = 1, label = "dock", logs = 6, work = 30 },
  boat = { w = 2, h = 1, label = "boat", logs = 5, work = 30 },   -- a boat on the stocks at the shore (D-087)
  fold = { w = 6, h = 6, label = "fold", logs = 10, work = 30 },  -- a fenced sheepfold (open ground, D-089)
  bridge = { w = 1, h = 1, label = "bridge", logs = 0, work = 0 },
  rack = { w = 2, h = 1, label = "drying rack", logs = 2, work = 8 },   -- poles for drying fish by the shore (D-096)   -- timber across a river (D-095; logs, work by its length)
  byre = { w = 7, h = 6, label = "byre yard", logs = 14, work = 40 },   -- a fenced cattle yard with a lean-to (D-092)
  sty = { w = 5, h = 4, label = "sty", logs = 8, work = 24 },     -- a pig sty: a fenced run and a low hut (D-092)
  tanpit = { w = 2, h = 2, label = "tanning pit", logs = 1, work = 10 },   -- a lined pit by the water where hides soak (D-104)
  saltpan = { w = 2, h = 2, label = "salt pans", logs = 3, work = 24 },   -- (S18) shallow pans and a fire pit on the shore
  -- (S17, R5) a founding household's camp: the ground cleared, a fire ring, a brush lean-to; its home
  -- and larder until a house stands (open ground: walked through, built on by nothing else)
  camp = { w = 5, h = 5, label = "camp", logs = 0, work = 2 },
  -- (S19) a rock outcrop: natural, built by nobody; stone is quarried at its face (sim/agents/stone.lua)
  outcrop = { w = 2, h = 2, label = "rock outcrop", logs = 0, work = 0 },
  -- (S19) iron: bog iron in wet ground (natural); the forge (bloomery and smithy) and a charcoal clamp
  bog = { w = 2, h = 2, label = "bog", logs = 0, work = 0 },
  forge = { w = 3, h = 3, label = "forge", logs = 10, work = 40 },
  clamp = { w = 2, h = 2, label = "charcoal clamp", logs = 0, work = 4 },
}
-- (S17) the kinds a household lives in
M.HOME = { house = true, camp = true }
local ENCLOSURE = { fold = true, byre = true, sty = true, camp = true }

function M.new(W, cell, opts)
  local V = W.S.v3
  local id = #V.settlements + 1
  local s = {
    id = id, cell = cell, name = opts.name or "?", culture = opts.culture or 1, founded = W.hour,
    key = rng.hash(W.seed, rng.S.V3_FOUND, cell, 0, 0) % 1000003,
    buildings = {}, blist = {}, plots = {}, members = {}, households = {}, sites = {}, felled = {},
    need = { food_days = 0, fell = 0, wood_short = 0, lying_trees = 0, site_logs = 0, sowing = false, sow_late = 0,
      clear_trees = 0, clear_stumps = 0, repairs = 0, food = 0 },
    acc = {}, alive = true, sqx = 0, sqy = 0, rain_now = 0, couples = {},
    -- (S17: where the stores will be; the founding's buildings set them)
    granary_x = 0, granary_y = 0, wood_x = 0, wood_y = 0,
  }
  V.settlements[id] = s
  mapm.build(W, s)
  return s
end

-- Buildings -----------------------------------------------------------------------------------
-- door: the tile in front of the middle of the footprint's side `side` ("s", "n", "e", "w").
function M.add_building(W, s, kind, ix0, iy0, side, state, opts)
  local V = W.S.v3
  local K = M.KINDS[kind]
  local w, h = (opts and opts.w) or K.w, (opts and opts.h) or K.h
  if side == "e" or side == "w" then w, h = h, w end
  local id = V.next_building
  V.next_building = id + 1
  local N = s.N
  local b = { id = id, kind = kind, label = K.label, ix0 = ix0, iy0 = iy0, w = w, h = h, side = side,
    state = state or "standing", logs_need = K.logs, logs_have = state == "site" and 0 or K.logs,
    work_need = K.work, work_done = state == "site" and 0 or K.work, hh = 0, stock = {}, prio = 0,
    started = W.hour, built = state == "site" and -1 or W.hour }
  if opts then for k, v in pairs(opts) do b[k] = v end end
  -- (S19) houses and granaries raised where stone is at hand stand on stone footings (not huts)
  if state == "site" and M.stone and not (opts and opts.hut) then
    local fs = M.stone.footing(W, s, kind)
    if fs > 0 then b.stone_need, b.stone_have = fs, 0 end
  end
  for iy = iy0, iy0 + h - 1 do
    for ix = ix0, ix0 + w - 1 do
      -- (a fold is fenced ground, walked in and out of: marked as an enclosure, not a footprint)
      if ENCLOSURE[kind] then s.plot[(iy - 1) * N + ix] = -2 else s.occ[(iy - 1) * N + ix] = id end
    end
  end
  mapm.occ_ver[s] = (mapm.occ_ver[s] or 0) + 1
  -- (S17: a camp needs no clearing; a lot with trees on it is cleared by felling first: M.plan_lot)
  if not (opts and opts.keep_trees) then mapm.clear_rect(s, ix0 - 1, iy0 - 1, ix0 + w, iy0 + h) end
  b.keep_trees = nil
  -- centre and door point
  local x0, y0 = mapm.ixy_xy(s, ix0, iy0)
  local x1, y1 = mapm.ixy_xy(s, ix0 + w - 1, iy0 + h - 1)
  b.cx, b.cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
  local T = s.T
  if side == "s" then b.dx, b.dy = b.cx, y0 - T
  elseif side == "n" then b.dx, b.dy = b.cx, y1 + T
  elseif side == "e" then b.dx, b.dy = x1 + T, b.cy
  else b.dx, b.dy = x0 - T, b.cy end
  if kind == "well" then b.dx, b.dy = b.cx, y0 - T end
  s.buildings[id] = b
  s.blist[#s.blist + 1] = id
  if state == "site" then s.sites[#s.sites + 1] = id end
  if kind == "granary" then s.granary = b; s.granary_x, s.granary_y = b.dx, b.dy end
  if kind == "dock" then
    -- boats moor on the water beside the dock, opposite its door (on the land side); (S18) a dock
    -- being built at a young place is not the dock until it stands
    if state == "standing" then s.dock = b end
    b.mx, b.my = 2 * b.cx - b.dx, 2 * b.cy - b.dy
  end
  if kind == "woodstore" then s.wood_store = b; s.wood_x, s.wood_y = b.dx, b.dy end
  if kind == "forge" then s.forge = b end
  if kind == "clamp" then s.clamp = b end
  if kind == "well" then s.well = b end
  mapm.rebuild_costs(s)
  return b
end

-- (S16) A parent moving into a new house brings their children still at home with them (a widow
-- remarrying takes her children), unless the children's other parent stays in the old house.
local function bring_children(W, a, old, hh)
  if not old or old == hh then return end
  local V = W.S.v3
  local list = {}
  for i = 1, #old.members do list[i] = old.members[i] end
  for _, id in ipairs(list) do
    local q = V.people[id]
    if q and q.alive and (q.mother == a.id or q.father == a.id) then
      local st = people.stage(q, W.hour)
      if st == "infant" or st == "child" or st == "apprentice" then
        local o = V.people[q.mother == a.id and q.father or q.mother]
        if not (o and o.alive and o.hh == old.id) then
          people.join(W, q, hh)
          people.remember(q, W.hour, "Moved with " .. a.name .. " into the new house")
        end
      end
    end
  end
end

-- A site's work and logs are complete: it stands. A house for a waiting couple becomes their
-- home and their household.
function M.complete_building(W, s, b)
  if b.state ~= "site" then return end
  -- (S19) a footing of stones under it when they were laid
  if (b.stone_need or 0) > 0 and (b.stone_have or 0) >= b.stone_need then b.footing = true end
  b.state = "standing"
  b.built = W.hour
  people.remove_id(s.sites, b.id)
  local V = W.S.v3
  if b.kind == "camp" then
    -- (S17, R5) a founding household's camp stands: its home and larder for now (what they carried
    -- was set down on its ground)
    local hh = V.households[b.for_hh or 0]
    if hh and #hh.members > 0 then
      hh.home, hh.camp, hh.camp_site = b.id, b.id, nil
      b.hh = hh.id
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive then people.remember(q, W.hour, "Made our camp at " .. s.name .. ": a fire, a lean-to, our packs set down") end
      end
    end
    if s.founding and not s.founding.settled then
      -- (the first fire: people live here now)
      s.founding.settled = W.hour
      W.F.land_use[s.cell] = -1
      W.lu_ver = (W.lu_ver or 0) + 1
      if W.record_events then events.emit(W, "settlement_founded", s.cell, 0, 0, { settlement = s.id }) end
    end
  end
  if b.kind == "house" and b.for_hh then
    -- (S16, R5) the house of a household come from elsewhere: theirs now (S17: or of founders living
    -- at their camp; what is at the camp is carried in)
    local hh = V.households[b.for_hh]
    if hh and #hh.members > 0 then
      hh.home, hh.lodge, hh.house_site = b.id, nil, nil
      -- (the camp they lived at is struck by their own hands: its things carried in; S18: the hut
      -- a house replaces too - and it stands empty for whoever needs a roof)
      if hh.camp then hh.old_camp, hh.camp = hh.camp, nil end
      local old = b.replaces and s.buildings[b.replaces]
      if old and old.hh == hh.id then
        hh.old_camp = old.id
        old.hh = 0
        old.for_name = "the old hut of the " .. ((V.people[hh.members[1] or 0] or {}).surname or "settler") .. "s"
      end
      b.hh = hh.id
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive then people.remember(q, W.hour, (b.hut and "Moved into our hut at " or "Moved into our own house at ") .. s.name) end
      end
    end
    b.stock.food, b.stock.meals, b.stock.water, b.stock.logs = 0, 0, 0, 0
  end
  if (b.kind == "fold" or b.kind == "byre" or b.kind == "sty") and b.herd and s.herds and s.herds[b.herd] then
    -- (S17) a pen for a herd kept at a camp until now
    local H = s.herds[b.herd]
    local was = s.buildings[H.pen or 0]
    if not was or was.kind == "camp" then H.pen = b.id end
    H.pen_planned = nil
  end
  if b.kind == "granary" and s.founding then s.granary_planned = nil end
  if b.kind == "saltpan" then s.saltpan = b end
  if b.kind == "dock" then
    -- (S18) the young place's dock stands: its boats moor there, and its sea ways are known
    s.dock = b
    for k, boat in ipairs(s.boats or {}) do boat.x, boat.y = b.mx + (k - 1) * 0.012, b.my end
    if M.trade then M.trade.forget(W) end
  end
  if b.kind == "woodstore" and s.founding then s.wood_planned = nil end
  if b.kind == "house" and b.for_a and M.leaving(W, s, V.people[b.for_a]) then
    -- (S19) the couple it was built for is leaving with its household (aboard a boat, on the road):
    -- the house stands empty for others (found: a couple carried off the boat to a new house, left
    -- behind on an island in a famine)
    local a = V.people[b.for_a]
    a.house_site = nil
    b.for_name = "the " .. (a.surname or "people") .. "s, who left"
    b.for_a, b.for_b = nil, nil
    b.stock.food, b.stock.meals, b.stock.water, b.stock.logs = 0, 0, 0, 0
    for i = #s.couples, 1, -1 do
      if s.couples[i] == a.id then table.remove(s.couples, i) end
    end
  elseif b.kind == "house" and b.for_a then
    local a, c = V.people[b.for_a], V.people[b.for_b or 0]
    local hh = people.new_household(W, s.id, b.id)
    s.households[#s.households + 1] = hh.id
    b.hh = hh.id
    for _, q in ipairs({ a, c }) do
      if q and q.alive then
        local old = V.households[q.hh]
        people.join(W, q, hh)
        people.remember(q, W.hour, "Moved into our new house")
        bring_children(W, q, old, hh)
      end
    end
    b.stock.food = 0
    b.stock.meals = 0
    b.stock.water = 0
    b.stock.logs = 0
    for i = #s.couples, 1, -1 do
      if s.couples[i] == b.for_a then table.remove(s.couples, i) end
    end
  end
  if b.kind == "boat" then
    -- launched: a new boat at the dock for the household that built it; the stocks are cleared
    s.boats = s.boats or {}
    local k = #s.boats + 1
    local d = s.dock
    s.boats[k] = { id = k, x = d and d.mx + (k - 1) * 0.012 or b.cx, y = d and d.my or b.cy, crew = 0, owner = b.for_hh or 0,
      cond = 1, trips = 0, built = W.hour }
    local hh = V.households[b.for_hh or 0]
    if hh then
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive then people.remember(q, W.hour, "Launched our new boat") end
      end
    end
    M.remove_building(W, s, b)
    s.acc.boats_built = (s.acc.boats_built or 0) + 1
    return
  end
  if b.kind == "bridge" then
    -- (D-095) its tiles carry the way whatever the river does
    s.bridge = s.bridge or {}
    for _, i in ipairs(b.tiles) do s.bridge[i] = b.id end
    mapm.rebuild_costs(s)
    s.acc.bridges = (s.acc.bridges or 0) + 1
    for _, id in ipairs(s.members) do
      local q = V.people[id]
      if q and q.alive and (q.objk == "build" or q.objk == "deliver") then people.remember(q, W.hour, "Finished the bridge over the river") end
    end
  end
  if W.record_events then
    events.emit(W, "house_built", s.cell, b.cause or 0, 1, { settlement = s.id, building = b.id, kind = b.kind })
  end
  s.acc.built = (s.acc.built or 0) + 1
end

-- Bridges (D-095): where crossings are wanted. Every trip whose straight way crosses the river
-- leaves its mark where it would cross (act.lua: one, plus three for each share of the way it had
-- to go round to a ford); the marks fade a sixth a day. When the crossings wanted near one place
-- pass `bridge_need`, the village lays out a bridge there across the river's narrowest line (a
-- site of logs and work like a house): built, it carries the way over flood and deep water.
local function plan_bridges(W, s)
  local c = C()
  local cr = s.cross
  if not cr then return end
  local N = s.N
  local best, bv = 0, 0
  local keys = {}
  for i in pairs(cr) do keys[#keys + 1] = i end
  table.sort(keys)
  for _, i in ipairs(keys) do
    local v = cr[i] * c.cross_keep
    if v < 0.2 then cr[i] = nil else cr[i] = v end
  end
  for _, b in ipairs(s.sites) do
    local bb = s.buildings[b]
    if bb and bb.kind == "bridge" then return end
  end
  if (s.acc.bridges or 0) - (s.acc.bridges_lost or 0) >= c.bridges_max then return end   -- (standing: D-106)
  -- (nobody lays out a bridge while the river is in flood: D-106)
  if (s.flood or 0) > c.flood_bridge then return end
  -- (the crossings wanted within three tiles of a place, summed)
  for _, i in ipairs(keys) do
    if cr[i] then
      local ix, iy = (i - 1) % N, floor((i - 1) / N)
      local v = 0
      for _, j in ipairs(keys) do
        local q = cr[j]
        if q then
          local jx, jy = (j - 1) % N, floor((j - 1) / N)
          if abs(jx - ix) <= 3 and abs(jy - iy) <= 3 then v = v + q end
        end
      end
      -- (not within five tiles of a bridge, built or being built)
      local near = false
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "bridge" then
          for _, t in ipairs(b.tiles) do
            local tx, ty = (t - 1) % N, floor((t - 1) / N)
            if abs(tx - ix) <= 5 and abs(ty - iy) <= 5 then near = true end
          end
        end
      end
      if v > bv and s.riv[i] > 0 and not near then best, bv = i, v end
    end
  end
  if best == 0 or bv < c.bridge_need then return end
  -- the narrowest straight line across through it, bank to bank (east-west or north-south: a
  -- diagonal span could not be stepped onto between deep water on either hand)
  local bt, bn, bdir, bends = nil, 99, 0, nil
  for d, o in ipairs({ { 1, 0 }, { 0, 1 } }) do
    local tiles = { best }
    local ok = true
    local ends = {}
    for _, sg in ipairs({ 1, -1 }) do
      local x, y = (best - 1) % N + 1, floor((best - 1) / N) + 1
      local found = false
      for _ = 1, c.bridge_max do
        x, y = x + o[1] * sg, y + o[2] * sg
        if x < 2 or y < 2 or x > N - 1 or y > N - 1 then break end
        local j = (y - 1) * N + x
        if s.riv[j] > 0 then
          tiles[#tiles + 1] = j
        else
          if s.land[j] == 1 and s.occ[j] == 0 then ends[#ends + 1] = j; found = true end
          break
        end
      end
      if not found then ok = false end
    end
    if ok and #tiles < bn then bt, bn, bdir, bends = tiles, #tiles, d, ends end
  end
  if not bt then s.cross[best] = nil; return end
  -- the site: logs and work by its length, the near bank its door
  local V = W.S.v3
  local id = V.next_building
  V.next_building = id + 1
  local e1, e2 = bends[1], bends[2]
  local x1, y1 = mapm.tile_xy(s, e1)
  local x2, y2 = mapm.tile_xy(s, e2)
  local near1 = x1 * x1 + y1 * y1 <= x2 * x2 + y2 * y2
  local cx, cy = mapm.tile_xy(s, best)
  local b = { id = id, kind = "bridge", label = "bridge", state = "site", ix0 = (best - 1) % N + 1, iy0 = floor((best - 1) / N) + 1,
    w = 1, h = 1, side = "s", cx = cx, cy = cy, dx = near1 and x1 or x2, dy = near1 and y1 or y2,
    ex = near1 and x2 or x1, ey = near1 and y2 or y1, tiles = bt, dir = bdir,
    logs_need = c.bridge_logs * #bt + 2, logs_have = 0, work_need = c.bridge_work * #bt, work_done = 0,
    builders = 0, stock = {}, prio = 2, for_name = "the village", hh = 0, founded = W.hour }
  s.buildings[id] = b
  s.blist[#s.blist + 1] = id
  s.sites[#s.sites + 1] = id
  s.acc.bridge_planned = (s.acc.bridge_planned or 0) + 1
  s.cross = {}
  for _, pid in ipairs(s.members) do
    local q = V.people[pid]
    if q and q.alive and people.stage(q, W.hour) == "adult" then
      people.remember(q, W.hour, "The village will bridge the river where we keep wading it")
    end
  end
end
M.plan_bridges = plan_bridges

-- A building taken down (a boat launched from its stocks): its ground is free again; the record
-- stays (plans in progress may still name it) but nothing lists it.
function M.remove_building(W, s, b)
  local N = s.N
  for iy = b.iy0, b.iy0 + b.h - 1 do
    for ix = b.ix0, b.ix0 + b.w - 1 do
      local i = (iy - 1) * N + ix
      if s.occ[i] == b.id then s.occ[i] = 0 end
      if ENCLOSURE[b.kind] and s.plot[i] == -2 then s.plot[i] = 0 end
    end
  end
  mapm.occ_ver[s] = (mapm.occ_ver[s] or 0) + 1
  people.remove_id(s.blist, b.id)
  people.remove_id(s.sites, b.id)
  b.state = "gone"
  mapm.rebuild_costs(s)
end

-- The food ledger (tools and tests): everything edible anywhere in the settlement (FU), and the
-- running sum of what came in less what went out. food_total(after) - food_total(before) equals
-- the change in food_flows over the same time (every new food stock or flow goes here).
local CARRY_FOOD = { seed = true, grain = true, food = true, sheaves = true, fish = true, meat = true, milk = true, carcass = true,
  dried = true }
function M.food_total(W, s)
  local V = W.S.v3
  local c = C()
  local tot = 0
  local g = s.granary
  if g then tot = tot + (g.stock.grain or 0) + (g.stock.sheaves or 0) + (g.stock.smoked or 0) end
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if M.HOME[b.kind] then
      local st = b.stock
      tot = tot + (st.food or 0) + (st.fish or 0) + (st.smoked or 0) + (st.meat or 0) + (st.seed or 0) + (st.meals or 0)
        + (st.milk or 0) + (st.cheese or 0)
    elseif b.kind == "rack" then
      tot = tot + (b.stock.drying or 0) + (b.stock.dried or 0)
    end
  end
  for k = 1, #s.plots do tot = tot + s.plots[k].sheaves end
  -- (cargo in the village's boats, wherever they are: D-102)
  for _, boat in ipairs(s.boats or {}) do
    local cg = boat.cargo
    if cg then tot = tot + (cg.grain or 0) + (cg.smoked or 0) + (cg.cheese or 0) end
  end
  for k = 1, #s.members do
    local p = V.people[s.members[k]]
    if p and p.alive then tot = tot + M.person_food(p) end
  end
  return tot
end

-- The food a person has on them (FU): in their hands or pack, their provisions, their lunch.
function M.person_food(p)
  local tot = 0
  if CARRY_FOOD[p.carry] or p.carry == "smoked" then tot = tot + p.camt
  elseif p.carry == "pack" and p.pack then
    tot = tot + (p.pack.grain or 0) + (p.pack.smoked or 0) + (p.pack.cheese or 0)
  end
  tot = tot + (p.prov or 0)
  if p.lunch then tot = tot + C().meal_fu end
  return tot
end

-- (S17) Person p (with what they carry) goes to live at o from s: the food on them leaves s's
-- ledger and enters o's.
function M.carry_over(s, o, p)
  local f = M.person_food(p)
  if f <= 0 then return end
  if s then s.acc.moved_out = (s.acc.moved_out or 0) + f end
  if o then o.acc.moved_in = (o.acc.moved_in or 0) + f end
end

function M.food_flows(s)
  local a = s.acc
  return (a.harvest or 0) + (a.foraged or 0) + (a.fished or 0) + (a.meat or 0) + (a.milk or 0) + (a.import or 0)
    - (a.eaten or 0) - (a.seed or 0) - (a.lunch or 0) - (a.spoiled or 0) - (a.whey or 0) - (a.export or 0)
    - (a.lost_sea or 0) + (a.moved_in or 0) - (a.moved_out or 0)
end

-- Plots ------------------------------------------------------------------------------------

function M.add_plot(W, s, ix0, iy0, hh, name)
  local c = C()
  local P = c.plot_tiles
  local N = s.N
  local k = #s.plots + 1
  local x0, y0 = mapm.ixy_xy(s, ix0, iy0)
  local T = s.T
  local pl = { id = k, ix0 = ix0, iy0 = iy0, w = P, h = P, rows = P, hh = hh or 0, name = name or ("plot " .. k),
    x0 = x0 - T * 0.5, y0 = y0 - T * 0.5, x1 = x0 - T * 0.5 + P * T, y1 = y0 - T * 0.5 + P * T,
    stage = "fallow", crop = "spring", fert = 1, row_st = {}, row_claim = {}, sown = 0, harvested = 0, growth = 0, weeds = 0, quality = 1,
    sheaves = 0, alive = true, sown_day = -1, ripe_day = -1, q_sum = 0, q_n = 0 }
  for r = 1, P do pl.row_st[r], pl.row_claim[r] = 0, 0 end
  -- the stook: where sheaves are stacked, at the corner nearest the village centre
  local cxs = (pl.x0 + pl.x1) * 0.5
  local cys = (pl.y0 + pl.y1) * 0.5
  pl.stx = cxs + (cxs > 0 and -1 or 1) * (P * T * 0.5 - T * 0.8)
  pl.sty = cys + (cys > 0 and -1 or 1) * (P * T * 0.5 - T * 0.8)
  mapm.clear_rect(s, ix0, iy0, ix0 + P - 1, iy0 + P - 1)
  for iy = iy0, iy0 + P - 1 do
    for ix = ix0, ix0 + P - 1 do s.plot[(iy - 1) * N + ix] = k end
  end
  s.plots[k] = pl
  return pl
end

-- Runtime indexes for the woods (never saved: rebuilt from the state, so a loaded world finds the
-- same trees). Trees never move and are only added at the founding.
local WOOD_IDX = setmetatable({}, { __mode = "k" })
local CLEAR_IDX = setmetatable({}, { __mode = "k" })

-- The trees off the village ground ordered by their distance from the woodstore (ties by index),
-- with those distances.
local function wood_index(s)
  local tr = s.trees
  local wx, wy = s.wood_x or 0, s.wood_y or 0
  local ix = WOOD_IDX[s]
  if ix and ix.n == tr.n and ix.wx == wx and ix.wy == wy then return ix end
  local O, D = {}, {}
  local m = 0
  -- (the village ground's trees are left standing; S17: a new place has no village ground yet - its
  -- first wood is cut about its camps, opening the ground)
  local keep = s.founding and -1 or 0.09
  for k = 1, tr.n do
    local x, y = tr.x[k], tr.y[k]
    local r2 = x * x + y * y
    if r2 > keep then
      local dx, dy = x - wx, y - wy
      m = m + 1
      O[m] = k
      D[k] = sqrt(dx * dx + dy * dy)
    end
  end
  table.sort(O, function(a, b)
    local da, db = D[a], D[b]
    if da ~= db then return da < db end
    return a < b
  end)
  local D1 = {}
  for j = 1, m do D1[j] = D[O[j]] end
  ix = { n = tr.n, wx = wx, wy = wy, O = O, D1 = D1, m = m }
  WOOD_IDX[s] = ix
  return ix
end

-- The trees (and stumps) on the ground being cleared - the new field's, and (S17) a building lot's
-- - by index.
function M.clearing_trees(s)
  local cl, lot = s.clearing, s.lot
  if not cl and not lot then return nil end
  local e = CLEAR_IDX[s]
  if e and e.cl == cl and e.lot == lot then return e.L end
  local L = {}
  local N, P = s.N, C().plot_tiles
  local function rect(x0, y0, x1, y1)
    for jy = max(1, y0), min(N, y1) do
      for jx = max(1, x0), min(N, x1) do
        local i = (jy - 1) * N + jx
        local k = s.tree[i]
        if k > 0 and s.plot[i] == -1 then L[#L + 1] = k end
      end
    end
  end
  if cl then rect(cl.ix0, cl.iy0, cl.ix0 + P - 1, cl.iy0 + P - 1) end
  if lot then rect(lot.ix0 - 1, lot.iy0 - 1, lot.ix0 + lot.w, lot.iy0 + lot.h) end
  table.sort(L)
  CLEAR_IDX[s] = { cl = cl, lot = lot, L = L }
  return L
end

-- Which tree should a woodcutter fell next? A tree on new field ground first (any size), else
-- the nearest grown tree to the woodstore (and to them), off the village ground. The woods are
-- walked outward from the woodstore and the walk stops once no farther tree could score better
-- (a tree's score is at least its distance from the woodstore less 0.15): the same tree as a
-- scan of every tree, at a fraction of the cost.
function M.next_tree(W, s, p)
  local tr = s.trees
  local st, claim, size, tile, plot = tr.st, tr.claim, tr.size, tr.tile, s.plot
  local X, Y = tr.x, tr.y
  local px, py = p.x, p.y
  local best, bs = 0, 1e9
  if s.clearing or s.lot then
    local L = M.clearing_trees(s)
    for j = 1, #L do
      local k = L[j]
      if st[k] == 1 and claim[k] == 0 and plot[tile[k]] == -1 then
        local dx, dy = X[k] - px, Y[k] - py
        local d = sqrt(dx * dx + dy * dy)
        if d < bs then best, bs = k, d end
      end
    end
    if best > 0 then return best, true end
  end
  local ix = wood_index(s)
  local O, D1 = ix.O, ix.D1
  for j = 1, ix.m do
    local d1 = D1[j]
    if d1 - 0.15 > bs then break end
    local k = O[j]
    if st[k] == 1 and claim[k] == 0 and size[k] >= 0.45 and plot[tile[k]] == 0 then
      local ex, ey = X[k] - px, Y[k] - py
      local d2 = sqrt(ex * ex + ey * ey)
      local sc = d1 + 0.5 * d2 - 0.15 * size[k]
      if sc < bs or (sc == bs and k < best) then best, bs = k, sc end
    end
  end
  return best
end

-- House sites: the free 4x4 lot (with a two-tile yard all round) nearest the square, not on a
-- plot, preferring lots beside a lane.
function M.find_lot(W, s)
  local N = s.N
  local c0 = floor(N / 2)
  local best, bx, by, bside = 1e9, 0, 0, "s"
  for r = 3, 26 do
    for iy = c0 - r, c0 + r do
      for ix = c0 - r, c0 + r do
        if (abs(ix - c0) == r or abs(iy - c0) == r) and (ix + iy) % 2 == 0 then
          if mapm.rect_free(s, ix - 2, iy - 2, ix + 5, iy + 5, true) then
            local x, y = mapm.ixy_xy(s, ix + 1.5, iy + 1.5)
            local d = sqrt(x * x + y * y)
            -- beside a lane?
            local lane = 0
            for dy = -3, 6 do
              for dx = -3, 6 do
                local i = (iy + dy - 1) * N + ix + dx
                if i >= 1 and i <= N * N and s.lane[i] > 0 then lane = 1 end
              end
            end
            local sc = d - 0.08 * lane
            if sc < best then
              best, bx, by = sc, ix, iy
              -- the door faces the square
              if abs(x) > abs(y) then bside = x > 0 and "w" or "e" else bside = y > 0 and "s" or "n" end
            end
          end
        end
      end
    end
    if best < 1e8 and r > 6 and best < (r - 4) * s.T then break end
  end
  if best >= 1e8 then return nil end
  return bx, by, bside
end

-- (S17) A lot for a building of `kind` near (nx, ny), at least min_r tiles from it: free ground
-- with a yard round it on the village's own bank, the fewest trees on it, the door toward the
-- square. A lot with trees on it is cleared first (s.lot: its tiles are new ground, plot -1, whose
-- trees the woodcutters fell and whose stumps are dug out; then the site is laid there:
-- finish_lot). One lot is cleared at a time. Returns the site (or true while its lot is cleared),
-- nil when there is no room.
function M.plan_lot(W, s, kind, opts, nx, ny, min_r)
  if s.lot then return nil end
  local K = M.KINDS[kind]
  local w, h = K.w, K.h
  local N = s.N
  local i0 = mapm.tile_of(s, nx or s.sqx or 0, ny or s.sqy or 0)
  if i0 == 0 then i0 = floor(N / 2) * N + floor(N / 2) + 1 end
  local ix0, iy0 = mapm.tile_ixy(s, i0)
  local yard = (kind == "well") and 1 or 2
  local best, bx, by, bs = false, 0, 0, 1e9
  for r = min_r or 3, 34 do
    for iy = iy0 - r, iy0 + r do
      for ix = ix0 - r, ix0 + r do
        if (abs(ix - ix0) == r or abs(iy - iy0) == r) and mapm.rect_free(s, ix - yard, iy - yard, ix + w - 1 + yard, iy + h - 1 + yard, true) then
          local trees = 0
          for jy = iy - 1, iy + h do
            for jx = ix - 1, ix + w do
              local k = s.tree[(jy - 1) * N + jx]
              if k > 0 and s.trees.st[k] ~= 0 then trees = trees + 1 end
            end
          end
          local sc = r + trees * 1.5
          if sc < bs then best, bx, by, bs = true, ix, iy, sc end
        end
      end
    end
    if best and bs <= r + 1 then break end
  end
  if not best then return nil end
  -- the door toward the square
  local x, y = mapm.ixy_xy(s, bx + (w - 1) / 2, by + (h - 1) / 2)
  local dx, dy = (s.sqx or 0) - x, (s.sqy or 0) - y
  local side = abs(dx) > abs(dy) and (dx > 0 and "e" or "w") or (dy > 0 and "n" or "s")
  if kind == "well" then side = "s" end
  local trees = 0
  for jy = by - 1, by + h do
    for jx = bx - 1, bx + w do
      local k = s.tree[(jy - 1) * N + jx]
      if k > 0 and s.trees.st[k] ~= 0 then trees = trees + 1 end
    end
  end
  if trees == 0 then
    local b = M.add_building(W, s, kind, bx, by, side, "site", opts)
    M.lot_placed(W, s, b)
    return b
  end
  local hw, hh = w, h
  if side == "e" or side == "w" then hw, hh = h, w end
  s.lot = { kind = kind, ix0 = bx, iy0 = by, w = hw, h = hh, side = side, opts = opts or {}, started = W.hour }
  for jy = by - 1, by + hh do
    for jx = bx - 1, bx + hw do
      local i = (jy - 1) * N + jx
      if s.plot[i] == 0 then s.plot[i] = -1 end
    end
  end
  s.tv = (s.tv or 0) + 1
  local o = opts or {}
  local V = W.S.v3
  if o.for_hh and V.households[o.for_hh] then V.households[o.for_hh].house_site = -1 end
  if o.for_a and V.people[o.for_a] then V.people[o.for_a].house_site = -1 end
  return true
end

-- (S17) The fields of a household that left village s go to the households staying, the one with the
-- fewest fields for its mouths first (as new ground is given).
function M.reassign_plots(W, s, hid)
  local V = W.S.v3
  M.leave_boats(W, s, hid)
  for k = 1, #s.plots do
    local pl = s.plots[k]
    if pl.hh == hid then
      local bh, br = nil, 1e9
      for i = 1, #s.households do
        local hh = V.households[s.households[i]]
        if hh and hh.id ~= hid and #hh.members > 0 and not hh.found and not hh.migrate then
          local n = 0
          for j = 1, #s.plots do if s.plots[j].hh == hh.id then n = n + 1 end end
          local r = n / #hh.members
          if r < br then bh, br = hh, r end
        end
      end
      pl.hh = bh and bh.id or 0
      local q = bh and V.people[bh.members[1]]
      if q then
        for _, id in ipairs(bh.members) do
          local m = V.people[id]
          if m and m.alive and people.stage(m, W.hour) == "adult" then people.remember(m, W.hour, "The field of those who left is ours to work now: " .. pl.name) end
        end
      end
    end
  end
end

-- (S17) A boat left behind by a household that went away: the village's (anyone's) now.
function M.leave_boats(W, s, hid)
  for _, boat in ipairs(s.boats or {}) do
    if boat.owner == hid then boat.owner = 0 end
  end
end

-- (S17) A camp or house with grain enough to sow a row (a young place with no granary), or nil.
function M.seed_store(W, s)
  local c = C()
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if M.HOME[b.kind] and b.state == "standing" and ((b.stock.food or 0) >= c.seed_row * 2 or (b.stock.seed or 0) >= c.seed_row) then return b end
  end
  return nil
end

-- (S17) The lot being cleared that tile i is on (or nil).
function M.on_lot(s, i)
  local lot = s.lot
  if not lot or i == 0 then return nil end
  local ix, iy = mapm.tile_ixy(s, i)
  if ix >= lot.ix0 - 1 and ix <= lot.ix0 + lot.w and iy >= lot.iy0 - 1 and iy <= lot.iy0 + lot.h then return lot end
  return nil
end

-- (S17) A site laid on its lot: whose it is.
function M.lot_placed(W, s, b)
  local V = W.S.v3
  if b.kind == "house" and b.for_hh and V.households[b.for_hh] then V.households[b.for_hh].house_site = b.id end
  if b.kind == "house" and b.for_a and V.people[b.for_a] then V.people[b.for_a].house_site = b.id end
  if W.record_events and b.kind == "house" then
    events.emit(W, "house_planned", s.cell, 0, 0, { settlement = s.id, building = b.id })
  end
end

-- (S17) A lot with no tree, stump or log left on it: the site is laid there.
function M.finish_lot(W, s)
  local lot = s.lot
  if not lot then return end
  local N = s.N
  local tr = s.trees
  for jy = lot.iy0 - 1, lot.iy0 + lot.h do
    for jx = lot.ix0 - 1, lot.ix0 + lot.w do
      local i = (jy - 1) * N + jx
      local k = s.tree[i]
      if k > 0 and tr.st[k] ~= 0 and s.plot[i] == -1 then return end
    end
  end
  for jy = lot.iy0 - 1, lot.iy0 + lot.h do
    for jx = lot.ix0 - 1, lot.ix0 + lot.w do
      local i = (jy - 1) * N + jx
      if s.plot[i] == -1 then s.plot[i] = 0 end
    end
  end
  s.tv = (s.tv or 0) + 1
  s.lot = nil
  local b = M.add_building(W, s, lot.kind, lot.ix0, lot.iy0, lot.side, "site", lot.opts)
  M.lot_placed(W, s, b)
end

-- Hourly ------------------------------------------------------------------------------------

-- Summary of the open work and the conditions everyone reads (the board). Returns true when the
-- rain state changed this hour (people outdoors react).
function M.hourly(W, s, now)
  local c = C()
  local V = W.S.v3
  local need = s.need
  -- weather
  local r0 = s.rain_now or 0
  local r1 = weather.rain(W, s, now)
  s.rain_now = r1
  if r1 > 0 then s.rain_day = (s.rain_day or 0) + 1 end
  local nc = weather.next_change(W, s, now)
  s.rain_soon = r1 == 0 and nc ~= nil and nc - now < 3
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  s.temp_now = weather.temp_at(W, s, now, lh)
  local wx = weather.today(W, s, now)
  s.cold = wx.temp < c.fuel_warm
  s.cold_k = s.cold and min(1, (c.fuel_warm - wx.temp) / 12) or 0
  -- food
  local pop, food = 0, 0
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive then pop = pop + 1 end
  end
  s.pop = pop
  food = s.granary and ((s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)) or 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if M.HOME[b.kind] and b.state == "standing" then food = food + people.pantry(b) + (b.stock.meals or 0) end
  end
  need.food = food
  need.food_days = pop > 0 and food / pop or 99
  -- sowing window
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  local new_day = s.doy ~= doy
  s.doy = doy
  -- sowing: the spring window, or the winter crop's in autumn (per plot: M.sow_open)
  -- (S17: a young place with no granary sows from the grain at its camps and houses)
  local seed = s.granary and (s.granary.stock.grain or 0) > c.seed_row
  if not s.granary then seed = M.seed_store(W, s) ~= nil end
  local w0, w1 = c.sow_season[1], c.sow_season[2]
  if doy >= c.sow_winter[1] and doy <= c.sow_winter[2] then w0, w1 = c.sow_winter[1], c.sow_winter[2] end
  need.sowing = seed and doy >= w0 and doy <= w1
  need.sow_late = need.sowing and (doy - w0) / max(1, w1 - w0) or 0
  -- (what to sow is decided at the start of each local day, before anyone hoes: D-100)
  if new_day then M.plan_sowing(W, s, true) end
  -- wood: what is wanted (sites + fuel reserve) against what is stored or lying cut
  local tr = s.trees
  local felled = s.felled
  -- (the woods are scanned again only when a tree, its logs or a claim on it changed: s.tv, D-086)
  local lying, chopping = s.lying or 0, s.chopping or 0
  if s.tv_seen ~= s.tv or not s.lying then
    s.tv_seen = s.tv
    for i = #felled, 1, -1 do felled[i] = nil end
    lying, chopping = 0, 0
    local ct, cs = 0, 0
    local plot = s.plot
    for k = 1, tr.n do
      local st = tr.st[k]
      if st == 2 and tr.logs[k] > 0 then
        felled[#felled + 1] = k
        lying = lying + tr.logs[k]
      elseif st == 1 and tr.claim[k] > 0 then
        chopping = chopping + 1
      end
      if st ~= 0 and plot[tr.tile[k]] == -1 then
        if st == 1 then ct = ct + 1 elseif st == 3 then cs = cs + 1 end
      end
    end
    s.lying, s.chopping, s.clear_t, s.clear_s = lying, chopping, ct, cs
  end
  need.lying_trees = #felled
  local site_logs, short_id, short_n = 0, nil, 0
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b then
      local m = b.logs_need - b.logs_have
      if m > 0 then
        site_logs = site_logs + m
        if m > short_n then short_id, short_n = b.id, m end
      end
    end
  end
  need.site_logs = site_logs
  s.site_short = short_id
  local nh = 0
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then nh = nh + 1 end
  end
  -- the reserve: a season of the hearths' burning (at least store_logs), and logs for repairs
  local burn = 0
  local repairs = 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if M.HOME[b.kind] and b.state == "standing" then
      if b.hh ~= 0 then burn = burn + (b.burn or c.fuel_cook) end
      if b.kind == "house" and (b.cond or 1) < c.repair_at then repairs = repairs + 1 end
    end
  end
  s.burn_day = burn
  need.repairs = repairs
  -- (autumn and winter: the wood for the cold months is laid in)
  local wdays = (s.doy or 0) >= 24 and c.winter_wood_days or 12
  local reserve = max(c.store_logs, floor(burn * wdays + 0.5)) + repairs * c.repair_logs
  local have = (s.wood_store and s.wood_store.stock.logs or 0) + lying
  if not s.wood_store then
    -- (S17: a young place with no woodstore keeps its wood in the households' woodpiles)
    for i = 1, #s.blist do
      local b = s.buildings[s.blist[i]]
      if M.HOME[b.kind] and b.state == "standing" then have = have + (b.stock.logs or 0) end
    end
  end
  local want = site_logs + reserve
  need.wood_short = want > 0 and max(0, min(1, (want - have) / want)) or 0
  s.wood_want, s.wood_have = want, have
  need.fell = max(0, ceil((want - have) / 3.5) - chopping)
  -- new field ground: trees to fell and stumps to dig out
  local cl = s.clearing
  need.clear_trees, need.clear_stumps = 0, 0
  if cl then need.clear_trees, need.clear_stumps = s.clear_t, s.clear_s end
  if site_logs > 0 then
    need.fell_why = sfmt("Logs are wanted for the %s (%d more) and the woodstore holds %d",
      s.buildings[short_id].label, site_logs, s.wood_store and s.wood_store.stock.logs or 0)
  else
    need.fell_why = sfmt("The woodstore holds %d logs; the hearths burn %.0f a day and the village wants %d in store",
      s.wood_store and s.wood_store.stock.logs or 0, burn, reserve)
  end
  -- who is gathered at the well
  local n = 0
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.act == "talk" then n = n + 1 end
  end
  s.at_well = n
  -- sun
  local lat = W.grid.lat[s.cell]
  s.sunrise, s.sunset = clock.sun_times(lat, now)
  -- (what more fish is worth here this hour: D-101)
  if s.dock then s.fish_worth = M.econ.fish_worth(W, s, now) end
  return r1 ~= r0, r1
end

-- Daily (at the planet step) ----------------------------------------------------------------

-- The sowing window of a plot's crop (local days of year).
local function window(c, crop)
  if crop == "winter" then return c.sow_winter[1], c.sow_winter[2] end
  return c.sow_season[1], c.sow_season[2]
end
M.window = window

-- May plot pl be sown (or hoed for sowing) on local day doy?
function M.sow_open(pl, doy)
  if pl.stage ~= "fallow" and pl.stage ~= "growing" then return false end
  if pl.sown >= pl.rows then return false end
  local a, b = window(C(), pl.crop)
  return doy >= a and doy <= b
end

-- Is it time to plough plot pl's fallow?
function M.plough_open(pl, doy)
  local c = C()
  return pl.stage == "rest" and doy >= c.plough_fallow[1] and doy <= c.plough_fallow[2]
end

-- Plots through the year (D-080): growing (weeds, the crop's growth from the cell's temperature
-- and soil water; a winter crop all but rests in winter), ripe (left standing it spoils), stubble
-- (once the sheaves are carried off: after a winter crop the plot waits for the spring sowing;
-- after a spring crop it rests a year), rest (the soil recovers; ploughed in summer; sown with the
-- winter crop in the autumn window).
local function grow_plots(W, s, day)
  local c = C()
  local F = W.F
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  local winter = doy >= 36
  for k = 1, #s.plots do
    local pl = s.plots[k]
    local cell = s.cell_of[(pl.iy0 + 3 - 1) * s.N + pl.ix0 + 3]
    local T = F.temp[cell] or 10
    local sw = F.soil_water[cell] or 0.5
    if pl.stage == "growing" then
      -- the crop develops with warmth (landraces suited to the climate: full rate from grow_topt;
      -- weeds hold it back a little); its yield is made by water and clean rows over the days it
      -- grows (a dry year ripens on time with a thin harvest, D-087)
      local gt = T > c.grow_tmin and min(1, (T - c.grow_tmin) / (c.grow_topt - c.grow_tmin)) or 0
      local wet = min(1, 0.35 + sw * 1.3)
      local d = gt * (1 - 0.25 * pl.weeds)
      local days = pl.crop == "flax" and c.grow_days_flax or c.grow_days
      if pl.crop == "winter" then
        days = c.grow_days_winter
        if winter then d = d * c.winter_dormant end
      end
      pl.growth = pl.growth + d / days
      pl.q_sum, pl.q_n = pl.q_sum + d * wet * (1 - 0.5 * pl.weeds), pl.q_n + d
      pl.weeds = min(1, pl.weeds + c.weeds_day * (0.5 + wet) * (winter and 0.3 or 1))
      if pl.growth >= 1 then
        pl.stage = "ripe"
        pl.ripe_day = day
        pl.ripe_for = 0
        pl.quality = max(0.25, pl.q_n > 0 and pl.q_sum / pl.q_n or 0.75) * (pl.fert or 1)
        s.q_avg = s.q_avg and (s.q_avg * 0.7 + pl.quality * 0.3) or pl.quality
        pl.spoil = 1
        if W.record_events then events.emit(W, "crop_ripe", s.cell, 0, 0, { settlement = s.id, plot = k }) end
      end
    elseif pl.stage == "ripe" then
      pl.ripe_for = (pl.ripe_for or 0) + 1
      if pl.ripe_for > c.ripe_spoil_days then pl.spoil = max(0, (pl.spoil or 1) - 0.2) end
      if pl.harvested >= pl.sown then
        pl.fert = max(0.3, (pl.fert or 1) - c.fert_crop)
        if pl.crop == "flax" then
          -- the pulled flax lies retting on the field before it is carried in (D-088)
          pl.stage, pl.ret_until = "retting", day + c.ret_days
        else
          pl.stage = "stubble"
        end
      end
    elseif pl.stage == "retting" then
      if day >= (pl.ret_until or day) then
        pl.stage = "stubble"
        pl.flax = (pl.flax or 0) + (pl.flax_ret or 0)
        pl.flax_ret = 0
      end
    elseif pl.stage == "stubble" then
      if pl.sheaves < 0.5 and (pl.flax or 0) < 0.05 then
        pl.sown, pl.harvested, pl.growth = 0, 0, 0
        pl.weeds = min(1, pl.weeds * 0.5 + 0.1)
        for r = 1, pl.rows do pl.row_claim[r] = 0 end
        pl.row_p, pl.win = nil, nil
        if pl.crop == "winter" then
          pl.stage, pl.crop = "fallow", "spring"    -- the spring crop follows next spring
        else
          pl.stage, pl.crop, pl.ploughed, pl.rest_from = "rest", "winter", 0, day   -- a year's rest
        end
      end
    elseif pl.stage == "rest" then
      pl.fert = min(c.fert_max, (pl.fert or 1) + c.fert_rest)
      pl.weeds = min(1, pl.weeds + c.weeds_day * 0.3)
      if doy >= c.sow_winter[1] and doy <= c.sow_winter[2] and day - (pl.rest_from or -99) >= 30 then pl.stage = "fallow" end
    end
    -- the end of the sowing window: what was sown grows; a plot nobody sowed waits for the next
    -- sowing (an unsown winter field is sown in spring, an unsown spring field rests)
    if pl.stage == "fallow" then
      local a, b = window(c, pl.crop)
      if doy >= a and doy <= b then pl.win = true
      elseif pl.win then
        pl.win = nil
        if pl.sown > 0 then pl.stage = "growing"
        elseif pl.crop == "winter" then pl.crop = "spring"
        else pl.stage, pl.crop, pl.ploughed, pl.rest_from = "rest", "winter", 0, day end
      end
    end
  end
end

local function grow_trees(W, s, day)
  local c = C()
  local tr = s.trees
  local hour = W.hour
  for k = 1, tr.n do
    local st = tr.st[k]
    if st == 1 then
      if tr.size[k] < 1 then tr.size[k] = min(1, tr.size[k] + c.tree_grow) end
    elseif st == 3 then
      -- a stump may sprout again after some days (on land not taken by the village)
      if (hour - tr.t_cut[k]) / 24 >= c.sapling_days and s.plot[tr.tile[k]] == 0 and s.occ[tr.tile[k]] == 0 then
        local u = rng.float(W.seed, rng.S.V3_MAP, s.key, k, day)
        if u < 0.06 then
          tr.st[k] = 1
          tr.size[k] = 0.05
          s.tv = (s.tv or 0) + 1
          s.dirty_costs = true
        end
      end
    end
  end
end

-- Hearths (D-080): every lived-in house cooks on its hearth each day (logs from its woodpile) and
-- burns more on a chilly day; a house with no wood is cold (and eats its meals cold). Water: a jar
-- a day. Houses weather and are repaired when worn. Food at home spoils slowly.
local function hearths(W, s, day)
  local c = C()
  local wx = weather.today(W, s, day * 24)
  local V = W.S.v3
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" then
      b.cond = max(0, (b.cond or 1) - c.wear_house * (b.footing and c.footing_wear or 1))
    end
    if M.HOME[b.kind] and b.state == "standing" and b.hh ~= 0 then
      local hh = V.households[b.hh]
      local n = hh and #hh.members or 0
      if n > 0 then
        b.stock.water = max(0, (b.stock.water or 0) - 1)
        local burn = c.fuel_cook * (0.7 + 0.06 * n)
        if wx.temp < c.fuel_warm then burn = burn + c.fuel_heat * min(1.6, (c.fuel_warm - wx.temp) / 10) end
        b.burn = burn
        local logs = b.stock.logs or 0
        if logs >= burn then
          b.stock.logs = logs - burn
          b.cold = false
          b.fire = true
        else
          b.stock.logs = 0
          b.cold = wx.temp < c.fuel_warm
          b.fire = logs > 0
        end
        -- seed kept at home after the sowing is grain for the pot
        if (b.stock.seed or 0) > 0 and not s.need.sowing then
          b.stock.food = (b.stock.food or 0) + b.stock.seed
          b.stock.seed = 0
        end
        local fd, ml, fi, sm, mt = b.stock.food or 0, b.stock.meals or 0, b.stock.fish or 0, b.stock.smoked or 0, b.stock.meat or 0
        local mk, ch = b.stock.milk or 0, b.stock.cheese or 0
        if fd > 0 or ml > 0 or fi > 0 or sm > 0 or mt > 0 or mk > 0 or ch > 0 then
          local lf, lm, li, ls, lt = fd * c.spoil_food, ml * c.spoil_meals, fi * c.spoil_fish, sm * c.spoil_smoked, mt * c.spoil_meat
          local lk, lc = mk * c.spoil_milk, ch * c.spoil_cheese
          b.stock.food, b.stock.meals, b.stock.fish, b.stock.smoked, b.stock.meat = fd - lf, ml - lm, fi - li, sm - ls, mt - lt
          b.stock.milk, b.stock.cheese = mk - lk, ch - lc
          local lost = lf + lm + li + ls + lt + lk + lc
          s.acc.spoiled = (s.acc.spoiled or 0) + lost
          s.acc.fish_spoiled = (s.acc.fish_spoiled or 0) + li
          -- (what turns goes to the pigs, if the village keeps any: D-092)
          if s.herds and s.herds.swine then b.stock.scraps = (b.stock.scraps or 0) + lost * c.scrap_share end
        end
      else
        b.fire = false
      end
    end
  end
  -- the granary: grain and sheaves keep, but mice and damp take a little every day
  local g = s.granary
  if g then
    local gr, sh = g.stock.grain or 0, g.stock.sheaves or 0
    local lost = gr * c.spoil_grain + sh * c.spoil_sheaves
    g.stock.grain = gr - gr * c.spoil_grain
    g.stock.sheaves = sh - sh * c.spoil_sheaves
    s.acc.spoiled = (s.acc.spoiled or 0) + lost
  end
end

-- Wild food at the woods' edge (D-080): it regrows on the standing trees of the edge band (more in
-- summer and autumn, little in winter) up to what the edge can hold; fruit not picked rots. Clearing
-- the woods shrinks it.
local function wild_food(W, s, day, fill)
  local c = C()
  local tr = s.trees
  local n = 0
  for k = 1, tr.n do
    if tr.st[k] == 1 then
      local x, y = tr.x[k], tr.y[k]
      local r2 = x * x + y * y
      if r2 > 0.25 and r2 < 1.69 then n = n + tr.size[k] end
    end
  end
  s.edge_trees = n
  local se = floor(clock.local_doy(day * 24, W.grid.lat[s.cell]) / 12) + 1
  local g = n * c.wild_regrow[se]
  local cap = n * c.wild_regrow[se] * c.wild_cap_days
  local w = fill and cap * fill or (s.wild or 0) + g
  if w > cap then w = cap + (w - cap) * 0.6 end
  s.wild = w
end
M.wild_food = wild_food

-- (S19) The shore's food (shellfish on the rocks, seaweed): regrowing along a coast's waterline
-- (shore_regrow FU a shore tile a day, at most shore_cap_days of it), gathered by the hungry at low
-- water - poor folk's food, and the coast's famine food.
local function shore_food(W, s)
  local c = C()
  local L = require("sim.agents.birds").shore(s)
  if #L == 0 then s.shore_food = nil; return end
  local g = #L * c.shore_regrow
  local f = (s.shore_food or g * c.shore_cap_days * 0.5) + g
  s.shore_food = min(f, g * c.shore_cap_days)
end

-- Food: will the fields feed everyone to the next harvest and leave a store? If not, new field
-- ground is staked out next to the fields (trees felled, stumps dug out, then it is a plot).
local function plan_fields(W, s, day)
  local c = C()
  if s.clearing or #s.plots >= c.max_plots then return end
  local pop = s.pop or 0
  if pop == 0 then return end
  local expect = (s.granary and ((s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)) or 0)
  -- (the yield to expect: the standing crop's own so far, else the village's lately: a dry site
  -- clears more land for the same bread, D-087)
  local qv = s.q_avg or 0.85
  for k = 1, #s.plots do
    local pl = s.plots[k]
    local standing = 0
    for r = 1, pl.rows do if pl.row_st[r] == 2 then standing = standing + 1 end end
    local q = (pl.stage == "growing" and pl.q_n > 0.5) and pl.q_sum / pl.q_n or qv
    local fy = pl.crop == "flax" and 0 or c.row_yield * (pl.fert or 1) * q
    expect = expect + standing * fy * 0.85 + pl.sheaves
    if pl.stage == "fallow" then expect = expect + (pl.rows - pl.sown) * fy * 0.6 end
    -- a resting field will bear the winter crop: part of the year ahead
    if pl.stage == "rest" then expect = expect + pl.rows * fy * 0.3 end
  end
  -- what the village eats (as they have eaten lately: meals and bread in the fields) for a year
  -- and a half, and the seed for every field (D-080)
  local rows = 0
  for k = 1, #s.plots do rows = rows + s.plots[k].rows end
  local want = (s.eat_avg or pop * 0.75) * c.days_per_year * (1 + c.store_years) + rows * c.seed_row
  s.food_expect, s.food_want = expect, want
  -- hands to spare (adults idle much of the day, measured: s.idle_avg) and open land: they clear
  -- more than they need (a bigger store against a bad year), up to half again (D-081)
  -- (with sowing to need (D-100), more ground only helps when the open fields were too few for
  -- the rows the village wanted)
  local spare = (s.idle_avg or 0) > c.idle_expand and s.land_short and expect < want * 1.6
  s.expand_why = (expect < want) and "short" or (spare and "spare" or nil)
  if expect >= want and not spare then return end
  -- the free lattice spot nearest the fields and the village
  local N, P = s.N, c.plot_tiles
  local best, bix, biy = 1e9, 0, 0
  for iy = 4, N - P - 3, P + 1 do
    for ix = 4, N - P - 3, P + 1 do
      if mapm.rect_free(s, ix, iy, ix + P - 1, iy + P - 1, true) then
        local x, y = mapm.ixy_xy(s, ix + P / 2, iy + P / 2)
        local d = sqrt(x * x + y * y)
        if d > 0.42 and d < 1.4 then
          local dp = 9
          for k = 1, #s.plots do
            local pl = s.plots[k]
            local ex, ey = (pl.x0 + pl.x1) * 0.5 - x, (pl.y0 + pl.y1) * 0.5 - y
            local e = sqrt(ex * ex + ey * ey)
            if e < dp then dp = e end
          end
          local trees = 0
          for jy = iy, iy + P - 1 do
            for jx = ix, ix + P - 1 do
              if s.tree[(jy - 1) * N + jx] > 0 then trees = trees + 1 end
            end
          end
          local sc = d * 0.6 + dp + trees * 0.012
          if sc < best then best, bix, biy = sc, ix, iy end
        end
      end
    end
  end
  if best >= 1e9 then
    -- (S17) no ground left to clear for the fields the village wants
    if expect < want then s.no_field_room = (s.no_field_room or 0) + 1 end
    return
  end
  s.no_field_room = 0
  s.clearing = { ix0 = bix, iy0 = biy, started = W.hour }
  for jy = biy, biy + P - 1 do
    for jx = bix, bix + P - 1 do s.plot[(jy - 1) * N + jx] = -1 end
  end
  s.tv = (s.tv or 0) + 1
  local x, y = mapm.ixy_xy(s, bix + P / 2, biy + P / 2)
  s.clearing.x, s.clearing.y = x, y
  if W.record_events then events.emit(W, "village_expanded", s.cell, 0, 0, { settlement = s.id, kind = "field" }) end
end

-- Sowing to need (D-100): the bread the village eats (the grain its households fetch from the
-- granary to eat, as measured) decides how many rows it hoes and sows: enough that the store, the
-- crop standing in the fields and what is sown now carry it a year with `store_years` to spare. The
-- fields not wanted this time rest (their soil recovers; the herds graze them). The best soil is
-- sown first; rows already hoed (the summer's ploughing of the fallow) are the first counted.
local SOW_OPEN = {}
local function plan_sowing(W, s, measure)
  local c = C()
  if measure or not s.grain_use then
    local f = s.acc.g_food or 0
    if s.gf_prev and s.grain_use then
      s.grain_use = s.grain_use + (f - s.gf_prev - s.grain_use) * c.grain_use_k
      s.gu_days = (s.gu_days or 0) + 1
    else
      s.grain_use = (s.eat_avg or (s.pop or 0) * 0.7) * 0.5
    end
    s.gf_prev = f
  end
  -- (a village new to its land sows for its whole appetite until a year of its own eating has
  -- told it how much of that is bread and how much fish, meat, milk and the woods)
  local use = max(s.grain_use, (s.eat_avg or (s.pop or 0) * 0.7) * max(0, 1 - (s.gu_days or 0) / c.days_per_year))
  local doy = s.doy or 0
  local g = s.granary
  local G = g and ((g.stock.grain or 0) + (g.stock.sheaves or 0)) or 0
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if M.HOME[b.kind] then G = G + (b.stock.food or 0) end
  end
  local q = s.q_avg or 0.85
  local R, rows_all = 0, 0
  local open = SOW_OPEN
  for k = #open, 1, -1 do open[k] = nil end
  for k = 1, #s.plots do
    local pl = s.plots[k]
    G = G + pl.sheaves
    if pl.crop ~= "flax" then
      rows_all = rows_all + pl.rows
      local standing = 0
      for r = 1, pl.rows do
        local rs = pl.row_st[r]
        if rs == 2 then standing = standing + 1 end
      end
      local qp = (pl.stage == "growing" and pl.q_n > 0.5) and pl.q_sum / pl.q_n or q
      R = R + standing * c.row_yield * (pl.fert or 1) * qp * 0.85
      if M.sow_open(pl, doy) then open[#open + 1] = pl end
    end
  end
  local A = use * c.days_per_year
  local T = A * (1 + c.store_years) + rows_all * c.seed_row
  local want = ceil((T - G - R) / (c.row_yield * q * 0.85))
  s.sow_want, s.grain_store, s.grain_target = want, G, T
  local left = want
  table.sort(open, function(a, b)
    local fa, fb = a.fert or 1, b.fert or 1
    if fa ~= fb then return fa > fb end
    return a.id < b.id
  end)
  for k = 1, #open do
    local pl = open[k]
    local free = 0
    for r = 1, pl.rows do
      local rs = pl.row_st[r]
      if rs ~= 2 then free = free + 1 end
    end
    local was = pl.sow_ok
    pl.sow_ok = left > 0
    if left > 0 then left = left - free end
    if was ~= false and not pl.sow_ok and pl.hh ~= 0 then
      -- (the owners know why their field lies idle)
      local hh = W.S.v3.households[pl.hh]
      local txt = nil
      for _, id in ipairs(hh and hh.members or {}) do
        local q = W.S.v3.people[id]
        if q and q.alive and people.stage(q, W.hour) == "adult" then
          txt = txt or sfmt("The village has bread for %.0f days in store and in the fields: %s rests this time", (G + R) / max(0.1, use), pl.name)
          people.remember(q, W.hour, txt)
        end
      end
    end
  end
  -- (the village wanted more rows than its open fields have: land is short)
  if #open > 0 then s.land_short = left > 0 end
end
M.plan_sowing = plan_sowing

-- New field ground with no tree, stump or log left on it becomes a plot.
local function finish_clearing(W, s)
  M.finish_lot(W, s)
  local cl = s.clearing
  if not cl then return end
  local c = C()
  local N, P = s.N, c.plot_tiles
  local tr = s.trees
  for jy = cl.iy0, cl.iy0 + P - 1 do
    for jx = cl.ix0, cl.ix0 + P - 1 do
      local k = s.tree[(jy - 1) * N + jx]
      if k > 0 and tr.st[k] ~= 0 then return end
    end
  end
  for jy = cl.iy0, cl.iy0 + P - 1 do
    for jx = cl.ix0, cl.ix0 + P - 1 do s.plot[(jy - 1) * N + jx] = 0 end
  end
  s.tv = (s.tv or 0) + 1
  local V = W.S.v3
  -- the household with the fewest fields per mouth takes it
  local bh, br = nil, 1e9
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then
      local n = 0
      for k = 1, #s.plots do if s.plots[k].hh == hh.id then n = n + 1 end end
      local r = n / #hh.members
      if r < br then bh, br = hh, r end
    end
  end
  local pl = M.add_plot(W, s, cl.ix0, cl.iy0, bh and bh.id or 0, "")
  local m = bh and V.people[bh.members[1]]
  pl.name = (m and m.surname or "New") .. " new field"
  pl.cleared = W.hour
  s.clearing = nil
  mapm.rebuild_costs(s)
end
M.finish_clearing = finish_clearing

-- (S18) Salt pans: a village on the sea whose households are short of salt lays them out on the
-- shore by its dock (a lot cleared, logs and work like any building).
local function plan_saltpan(W, s)
  if not s.dock or s.saltpan or s.lot then return end
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "saltpan" then return end
  end
  local econ = M.econ
  local V = W.S.v3
  local short = 0
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
    if home and home.kind == "house" then
      local have, want = econ.house_stock(W, hh, home, "salt")
      if have < want * 0.5 then short = short + 1 end
    end
  end
  if short == 0 then return end
  M.plan_lot(W, s, "saltpan", { prio = 0.5, for_name = "the village" }, s.dock.dx, s.dock.dy, 2)
end

-- Boats (D-087): they rot a little ashore; a household with a fisher and no sound boat of its own
-- lays down a new one on the stocks by the dock (logs and work, like a house).
local function plan_boats(W, s)
  if not s.dock then return end
  local c = C()
  local V = W.S.v3
  s.boats = s.boats or {}
  for _, boat in ipairs(s.boats) do
    if boat.crew == 0 and not boat.lost then
      boat.cond = max(0, (boat.cond or 1) - c.boat_rot)
      if boat.cond <= 0 then
        boat.lost = true    -- rotten through: broken up for firewood
        boat.owner = -1
      end
    end
  end
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "boat" then return end
  end
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then
      local fisher = nil
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive and q.role == "fisher" then fisher = q end
      end
      if fisher then
        local has = false
        for _, boat in ipairs(s.boats) do
          if boat.owner == hh.id and not boat.lost and (boat.cond or 1) > c.boat_unsafe then has = true end
        end
        if not has then
          -- the stocks: free shore land near the dock
          local d = s.dock
          local N = s.N
          local dix, diy = mapm.tile_ixy(s, mapm.tile_of(s, d.dx, d.dy))
          for r = 1, 10 do
            local done = false
            for jy = diy - r, diy + r do
              for jx = dix - r, dix + r do
                if not done and (abs(jx - dix) == r or abs(jy - diy) == r) and mapm.rect_free(s, jx - 1, jy - 1, jx + 2, jy + 1, true) then
                  local b = M.add_building(W, s, "boat", jx, jy, "s", "site",
                    { for_hh = hh.id, for_name = "the " .. fisher.surname .. "s' boat", prio = 0.4 })
                  people.remember(fisher, W.hour, "Laid down a new boat by the dock")
                  done = b ~= nil
                end
              end
            end
            if done then return end
          end
          return
        end
      end
    end
  end
end

-- Drying racks (D-096): a fishing household that has had fish to spare lays out a rack of poles
-- by the shore near the dock (logs and work like any site); on a rack, hung fish dries over a few
-- fair days (rain spoils some of what hangs).
local function plan_racks(W, s, day)
  if not s.dock then return end
  local c = C()
  local V = W.S.v3
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "rack" then return end
  end
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    local fisher = false
    if hh then
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive and q.role == "fisher" then fisher = true end
      end
    end
    if fisher and (hh.fish_spare or 0) >= c.rack_after then
      local has = false
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "rack" and b.hh == hh.id then has = true end
      end
      if not has then
        local d = s.dock
        local dix, diy = mapm.tile_ixy(s, mapm.tile_of(s, d.dx, d.dy))
        for r = 2, 10 do
          for jy = diy - r, diy + r do
            for jx = dix - r, dix + r do
              if (abs(jx - dix) == r or abs(jy - diy) == r) and mapm.rect_free(s, jx - 1, jy - 1, jx + 2, jy + 1, true) then
                local b = M.add_building(W, s, "rack", jx, jy, "s", "site", { for_hh = hh.id, for_name = "the household", prio = 0.3 })
                if b then b.hh = hh.id; return end
              end
            end
          end
        end
        return
      end
    end
  end
end

-- Tanning pits (D-104): a household with hides waiting lays out a pit by the water (the river's
-- bank, else the shore, else the village's edge): logs and work like any site.
local function plan_tanpits(W, s, day)
  local V = W.S.v3
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "tanpit" then return end
  end
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    local home = hh and hh.home ~= 0 and s.buildings[hh.home]
    if home and (home.stock.hides or 0) >= 2 then
      local has = false
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "tanpit" and b.hh == hh.id then has = true end
      end
      if not has then
        -- (by water: the nearest wet tile to the house, else the house itself)
        local N = s.N
        local hix, hiy = mapm.tile_ixy(s, mapm.tile_of(s, home.dx, home.dy))
        for r = 3, 14 do
          for jy = hiy - r, hiy + r do
            for jx = hix - r, hix + r do
              if (abs(jx - hix) == r or abs(jy - hiy) == r) and jx > 3 and jy > 3 and jx < N - 4 and jy < N - 4 then
                local near_water = false
                for dy = -2, 3 do
                  for dx = -2, 3 do
                    local k = (jy + dy - 1) * N + jx + dx
                    if s.riv[k] > 0 or s.land[k] == 0 then near_water = true end
                  end
                end
                if (near_water or r >= 12) and mapm.rect_free(s, jx, jy, jx + 1, jy + 1, true) then
                  local b = M.add_building(W, s, "tanpit", jx, jy, "s", "site", { for_hh = hh.id, for_name = "the household", prio = 0.25 })
                  if b then b.hh = hh.id; return end
                end
              end
            end
          end
        end
        return
      end
    end
  end
end

local function dry_racks(W, s, day)
  local c = C()
  local wet = (s.rain_day or 0) > 0
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "rack" and b.state == "standing" then
      local h = b.stock.drying or 0
      if h > 0 then
        if wet then
          local lost = h * c.dry_rain_spoil
          b.stock.drying = h - lost
          s.acc.spoiled = (s.acc.spoiled or 0) + lost
        else
          local d = h * c.dry_rate
          b.stock.drying = h - d
          b.stock.dried = (b.stock.dried or 0) + d
          s.acc.dried = (s.acc.dried or 0) + d
        end
      end
      b.stock.dried = (b.stock.dried or 0) * (1 - c.spoil_smoked)
    end
  end
end

-- Textiles (D-088): the village's linen, from flax field to cloth, in garments' worth.
function M.textile_pipeline(W, s)
  local c = C()
  local t = s.granary and ((s.granary.stock.flax or 0) + (s.granary.stock.wool or 0)) or 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" then t = t + (b.stock.flax or 0) + (b.stock.wool or 0) + (b.stock.fibre or 0) + (b.stock.yarn or 0) + (b.stock.cloth or 0) end
  end
  for k = 1, #s.plots do
    local pl = s.plots[k]
    if pl.crop == "flax" then
      t = t + (pl.flax or 0) + (pl.flax_ret or 0)
      if pl.stage == "growing" or pl.stage == "ripe" then t = t + (pl.sown - pl.harvested) * c.flax_row * 0.8 end
    end
  end
  return t
end

-- In the spring sowing window, a village short of linen sows one field with flax: one of the
-- fields waiting for the spring crop, its household's (the village's want: textile_want a person).
local function plan_flax(W, s, day)
  local c = C()
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  if doy < c.sow_season[1] or doy > c.sow_season[2] - 4 then return end
  local year = floor((day + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  if s.flax_year == year then return end
  local want = (s.pop or #s.members) * c.textile_want
  local have = M.textile_pipeline(W, s)
  -- (S17: bread before linen - a young place with few fields sows them all for grain)
  if #s.plots < 3 then return end
  s.flax_year = year
  -- (as many fields as the want takes, the most fertile waiting for the spring crop first)
  while have < want do
    local best = nil
    for k = 1, #s.plots do
      local pl = s.plots[k]
      if pl.stage == "fallow" and pl.crop == "spring" and pl.sown == 0 then
        if not best or (pl.fert or 1) > (best.fert or 1) then best = pl end
      end
    end
    if not best then return end
    best.crop = "flax"
    best.name = best.name:gsub(" field", " flax field", 1)
    local hh = W.S.v3.households[best.hh]
    if hh then
      for _, id in ipairs(hh.members) do
        local q = W.S.v3.people[id]
        if q and q.alive and people.stage(q, W.hour) == "adult" then
          people.remember(q, W.hour, sfmt("Our field goes to flax this year (the village has %.0f of the %.0f garments' linen it wants)", have, want))
        end
      end
    end
    have = have + best.rows * c.flax_row * 0.8
  end
end

-- Floods and bridges (D-106): today's river widths on the map's planet cells against what they
-- usually are (a slow running mean); a high water wears every bridge, and one worn through is swept
-- away: its tiles are river again, the ways round or through it change at once, and the crossings
-- (D-095) build up the need for a new one.
local MAPCELLS = setmetatable({}, { __mode = "k" })
function floods(W, s, wd, day)
  local c = C()
  local cells = MAPCELLS[s]
  if not cells then
    cells = {}
    local seen = {}
    for i = 1, s.N * s.N do
      local cl = s.cell_of[i]
      if cl and cl > 0 and not seen[cl] then seen[cl] = true; cells[#cells + 1] = cl end
    end
    table.sort(cells)
    MAPCELLS[s] = cells
  end
  s.rw = s.rw or {}
  local k = 0
  for _, cl in ipairs(cells) do
    local w = wd[cl] or 0
    if w > 0 then
      local a = s.rw[cl] or w
      if w / a > k then k = w / a end
      s.rw[cl] = a + (w - a) * c.flood_mean_k
    end
  end
  s.flood = k
  if k <= c.flood_bridge or not s.bridge then return end
  local V = W.S.v3
  for i = #s.blist, 1, -1 do
    local b = s.buildings[s.blist[i]]
    if b and b.kind == "bridge" and b.state == "standing" then
      b.cond = (b.cond or 1) - c.flood_wear * (k - c.flood_bridge)
      if b.cond <= 0 then
        for _, t in ipairs(b.tiles or {}) do if s.bridge[t] == b.id then s.bridge[t] = nil end end
        M.remove_building(W, s, b)
        s.acc.bridges_lost = (s.acc.bridges_lost or 0) + 1
        for _, id in ipairs(s.members) do
          local q = V.people[id]
          if q and q.alive and people.stage(q, W.hour) == "adult" then people.remember(q, W.hour, "The flood swept the bridge away") end
        end
        if W.record_events then events.emit(W, "bridge_lost", s.cell, 0, 1, { settlement = s.id, building = b.id }) end
      end
    end
  end
end

-- (S16) A house standing empty (its household gone away or died out), or nil.
local function empty_house(W, s)
  local V = W.S.v3
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.state == "standing" then
      local hh = V.households[b.hh or 0]
      if not hh or #hh.members == 0 or hh.home ~= b.id then return b end
    end
  end
  return nil
end

-- (S19) Is person q (of village s) leaving it - away, aboard a boat, or of a household moving or
-- making a new place?
function M.leaving(W, s, q)
  if not q or not q.alive then return true end
  if q.sett ~= s.id or q.at or (q.riding or 0) ~= 0 then return true end
  local hh = W.S.v3.households[q.hh]
  local mg, fd = hh and hh.migrate, hh and hh.found
  return (mg and not mg.arrived and not mg.given_up) or (fd and not fd.arrived and not fd.given_up) or false
end

-- Growth: couples without a home get a house site (S16: or a house standing empty); households
-- come from elsewhere and lodging with others get a site of their own.
local function plan_houses(W, s)
  local V = W.S.v3
  for i = #s.couples, 1, -1 do
    local a = V.people[s.couples[i]]
    if a and a.alive and not a.house_site and not M.leaving(W, s, a) then
      local b = empty_house(W, s)
      if not b then break end
      local c2 = V.people[a.partner or 0]
      local hh = people.new_household(W, s.id, b.id)
      s.households[#s.households + 1] = hh.id
      b.hh = hh.id
      for _, q in ipairs({ a, c2 }) do
        if q and q.alive then
          local old = V.households[q.hh]
          people.join(W, q, hh)
          people.remember(q, W.hour, "Moved into the empty house of the " .. (b.for_name or "people who left"))
          bring_children(W, q, old, hh)
        end
      end
      table.remove(s.couples, i)
    end
  end
  for _, hid in ipairs(s.waiting or {}) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.lodge and not hh.house_site then
      local b = empty_house(W, s)
      if b then
        b.hh = hh.id
        hh.home, hh.lodge = b.id, nil
        for _, id in ipairs(hh.members) do
          local q = V.people[id]
          if q and q.alive then people.remember(q, W.hour, "Moved into a house standing empty at " .. s.name) end
        end
      end
    end
  end
  local open = 0
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "house" then open = open + 1 end
  end
  local nolot = false
  for i = 1, #s.couples do
    if open >= 2 then break end
    local a = V.people[s.couples[i]]
    if a and a.alive and not a.house_site and not M.leaving(W, s, a) then
      local ix, iy, side = M.find_lot(W, s)
      if not ix then nolot = true end
      if ix then
        local b = M.add_building(W, s, "house", ix, iy, side, "site",
          { for_a = a.id, for_b = a.partner, for_name = "the " .. a.surname .. "s", prio = 1 })
        a.house_site = b.id
        open = open + 1
        if W.record_events then
          events.emit(W, "house_planned", s.cell, 0, 0, { settlement = s.id, building = b.id })
        end
      end
    end
  end
  -- (S17) days running a waiting couple found no ground left to build on
  s.no_lot = nolot and (s.no_lot or 0) + 1 or 0
  -- (S16, R5) a household come from elsewhere, lodging with another: a house of its own (S17: and
  -- founders living at their camp - a hut first, on a lot their own hands clear)
  for _, hid in ipairs(s.waiting or {}) do
    if open >= 2 then break end
    local hh = V.households[hid]
    local camp = hh and (hh.camp or hh.camp_site)
    if hh and #hh.members > 0 and (hh.lodge or camp) and not hh.house_site then
      local q = V.people[hh.members[1]]
      local opts = { for_hh = hh.id, for_name = "the " .. (q and q.surname or "newcomer") .. "s", prio = 1 }
      if s.founding then
        local c = C()
        opts.hut, opts.logs_need, opts.work_need, opts.label, opts.prio = true, c.hut_logs, c.hut_work, "hut", 2
        local cb = s.buildings[hh.camp or hh.camp_site or 0]
        local r = M.plan_lot(W, s, "house", opts, cb and cb.cx or s.sqx, cb and cb.cy or s.sqy, 4)
        if r then open = open + 1 else break end
      else
        local ix, iy, side = M.find_lot(W, s)
        if ix then
          local b = M.add_building(W, s, "house", ix, iy, side, "site", opts)
          hh.house_site = b.id
          open = open + 1
        end
      end
    end
  end
  if s.waiting then
    for i = #s.waiting, 1, -1 do
      local hh = V.households[s.waiting[i]]
      if not hh or #hh.members == 0 or not (hh.lodge or hh.camp or hh.camp_site) then table.remove(s.waiting, i) end
    end
  end
end

-- (S19) A household of s with nobody left in it (all dead, or its last members gone to live
-- elsewhere: a widow remarried away with her children): what it held goes on. Its fields to the
-- household with the fewest fields a mouth (reassign_plots), its boats and beasts to its kin here -
-- the households of its lineage (the same surname) or kin of its last member - else to those with
-- most of the kind. Once (hh.estate).
local function estates(W, s, now)
  local V = W.S.v3
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members == 0 and not hh.estate and not hh.found then
      hh.estate = now
      local last = V.people[hh.last or 0]
      -- the heir: a household of the same lineage, or with kin of the last member
      local heir, hs = nil, 0
      for _, h2id in ipairs(s.households) do
        local h2 = V.households[h2id]
        if h2 and h2id ~= hid and #h2.members > 0 and not h2.found and not (h2.migrate and not h2.migrate.arrived and not h2.migrate.given_up) then
          local sc = 0
          for _, qid in ipairs(h2.members) do
            local q = V.people[qid]
            if q and last then
              if q.surname == last.surname then sc = sc + 2 end
              if people.is_kin(W, last, qid) then sc = sc + 3 end
            end
          end
          if sc > hs then heir, hs = h2, sc end
        end
      end
      local hq = heir and V.people[heir.members[1] or 0]
      local why = sfmt("Came to the %ss when the %s household was no more", hq and hq.surname or "neighbour", last and last.surname or "old")
      for _, boat in ipairs(s.boats or {}) do
        if boat.owner == hid and not boat.moved then boat.owner = heir and heir.id or 0 end
      end
      M.reassign_plots(W, s, hid)
      local migrate = M.migrate
      for _, kind in ipairs({ "flock", "cattle", "swine" }) do
        local H = s.herds and s.herds[kind]
        for _, a in ipairs(H and H.animals or {}) do
          if a.alive and a.owner == hid then
            if heir then a.owner, a.why_owner = heir.id, why
            elseif migrate then migrate.leave_beast(W, s, hh, a, kind, now) end
          end
        end
      end
      s.acc.estates = (s.acc.estates or 0) + 1
    end
  end
end

function M.daily(W, s, day)
  estates(W, s, W.S.v3.now or W.hour)
  -- (S19) stone: the outcrops (once), querns set up, the stone the village wants
  if M.stone then M.stone.daily(W, s, W.S.v3.now or W.hour) end
  -- (S19) iron: deposits (once), what is known and wanted, the forge and clamp planned
  if M.iron then M.iron.daily(W, s, W.S.v3.now or W.hour) end
  -- (S19) fairs: called, heard of, households deciding to go
  if M.fair then M.fair.daily(W, s, W.S.v3.now or W.hour) end
  -- adults' idle share of the waking day, as measured (act.lua counts leisure hours)
  local lz, aw = s.acc.lz_h or 0, s.acc.aw_h or 0
  if s.aw_prev and aw - s.aw_prev > 1 then
    local share = (lz - s.lz_prev) / (aw - s.aw_prev)
    s.idle_avg = s.idle_avg and (s.idle_avg + (share - s.idle_avg) * 0.15) or share
  end
  s.lz_prev, s.aw_prev = lz, aw
  -- the village's daily eating, as measured (meals at home and bread in the fields)
  local ate = (s.acc.eaten or 0) + (s.acc.lunch or 0)
  if s.ate_prev and day > 0 then
    local d = ate - s.ate_prev
    s.eat_avg = s.eat_avg and (s.eat_avg + (d - s.eat_avg) * 0.1) or d
  end
  s.ate_prev = ate
  grow_plots(W, s, day)
  grow_trees(W, s, day)
  hearths(W, s, day)
  wild_food(W, s, day)
  shore_food(W, s)
  plan_houses(W, s)
  plan_boats(W, s)
  plan_saltpan(W, s)
  plan_racks(W, s, day)
  plan_tanpits(W, s, day)
  dry_racks(W, s, day)
  s.rain_day = 0
  plan_flax(W, s, day)
  -- (fields whose window opened today: decided before anyone sows them)
  plan_sowing(W, s, false)
  plan_fields(W, s, day)
  finish_clearing(W, s)
  mapm.daily_wear(s)
  -- the rivers as today's flow has them (a flood closes the fords, a drought opens more)
  local wd = M.today_widths(W)
  mapm.rivers(W, s, false, wd)
  floods(W, s, wd, day)
  mapm.rebuild_costs(s)
  s.dirty_costs = false
  -- (D-095) the river today: its fish (a stock by how much water there is, regrowing), and the
  -- crossings the village wants bridged
  local wet = mapm.wet_n(s)
  if not wet then
    wet = 0
    for i = 1, s.N * s.N do if s.riv[i] > 0 then wet = wet + 1 end end
  end
  s.has_river = wet > 0
  local c = C()
  s.river_fish_cap = wet * c.rfish_tile
  local rf = s.river_fish or s.river_fish_cap * 0.8
  if s.river_fish_cap > 0 then
    rf = rf + c.rfish_regrow * rf * (1 - rf / s.river_fish_cap) + 0.01 * s.river_fish_cap
  end
  s.river_fish = min(rf, s.river_fish_cap)
  -- (D-107) what drains into the river: the houses and pens near its banks (people's and beasts'
  -- dirt), the nearer the more
  local dirt = 0
  if s.has_river then
    for k = 1, #s.blist do
      local b = s.buildings[s.blist[k]]
      local wgt = (b.kind == "house" and b.hh ~= 0 and 1) or ((b.kind == "fold" or b.kind == "byre" or b.kind == "sty") and 2) or 0
      if wgt > 0 then
        local bi, bd = mapm.house_bank(s, b)
        if bi > 0 and bd < c.foul_reach then dirt = dirt + wgt * (1 - bd / c.foul_reach) end
      end
    end
  end
  s.river_dirt = dirt
  plan_bridges(W, s)
  -- (the day's best exchange with the neighbours, by what the village knows of them: D-101)
  M.econ.daily(W, s, day * 24)
  -- (S16, R5) households that would leave, and where to
  M.migrate.daily(W, s, day * 24)
  -- (S17, R5) households that would make a new place; a young place's own planning
  if M.founding then
    M.founding.daily(W, s, day * 24)
    M.founding.place_daily(W, s, W.S.v3.now or W.hour)
  end
end

-- The planet's river widths today (one computation a planet day for every settlement; runtime).
local WD = setmetatable({}, { __mode = "k" })
function M.today_widths(W)
  local e = WD[W]
  if e and e.tick == W.tick then return e.wd end
  local wd = require("sim.surface").river_widths(W, e and e.wd)
  WD[W] = { tick = W.tick, wd = wd }
  return wd
end

return M
