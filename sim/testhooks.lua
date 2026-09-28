-- Test hooks (07_TESTING.md §3): direct effects standing in for the player's powers until they
-- exist (Chapter 2). Registered as the logged command `test_action` (world.test_action queues
-- it), dispatched on params.name. Each hook creates a root event `test_<name>` (actor player)
-- and writes its id into the cause tags of the fields it changes, exactly like a power will.
--
--   remove_predators { cells = {...} | continent = id, fraction = 0..1 }
--   ignite           { cells = {...} | cell = c }
--   oil              { cell = c, amount = cell-units (default 3) }
--   add_grazers      { cell = c | cells = {...}, amount = density added (default 0.5) }
--   kill_fish        { cells = {...} | cell = c, fraction = 0..1 (default 1) }
--   heat             { cells = {...} | cell = c, amount = °C of heat anomaly (default 10) }
--   burn_farms       { village = id | cells = {...} }  ignites the village's farm cells; crops
--                    that don't catch are destroyed outright (fuel may be too thin to burn)
--   sink_tanker      { tanker = ship id (default: the first afloat) }
--   empty_stores     { village = id }  the village's grain, fresh food and seed are lost (a store fire)
--   emigrate         { village = id, fraction = 0..1 }  that share of its households leaves for the
--                    nearest other village as a walking party (a labour loss with a cause)
local world = require("sim.world")
local events = require("sim.causality.events")
local blame = require("sim.causality.blame")

local hooks = {}

local function cells_of(W, p)
  if p.cells then return p.cells end
  if p.cell then return { p.cell } end
  if p.continent then
    local out = {}
    local cont = W.geo.continent
    for c = 1, W.n do if cont[c] == p.continent then out[#out + 1] = c end end
    return out
  end
  return {}
end

local function root(W, name, cells, mag, data)
  local c0 = cells[1] or 0
  return events.emit(W, "test_" .. name, c0, 0, mag or 1, data)
end

function hooks.remove_predators(W, p)
  local cells = cells_of(W, p)
  local frac = p.fraction or 1
  local id = root(W, "remove_predators", cells, #cells, { fraction = frac })
  -- (the packs on these cells: W4)
  require("sim.systems.fauna").kill_in(W, cells, "predators", frac, id)
  return id
end

function hooks.ignite(W, p)
  local cells = cells_of(W, p)
  local id = root(W, "ignite", cells, #cells)
  local fire = require("sim.systems.fire")
  for i = 1, #cells do fire.ignite(W, cells[i], id) end
  return id
end

function hooks.oil(W, p)
  local c = p.cell or (p.cells and p.cells[1])
  local amount = p.amount or 3
  local id = root(W, "oil", { c }, amount)
  require("sim.systems.ocean").spill(W, c, amount, id)
  return id
end

function hooks.add_grazers(W, p)
  local cells = cells_of(W, p)
  local amount = p.amount or 0.5
  local id = root(W, "add_grazers", cells, amount * #cells)
  -- (a herd per cell: W4)
  local wildlife = require("sim.systems.fauna")
  local per = require("sim.config").WILD.gpc
  for i = 1, #cells do
    if W.geo.ocean[cells[i]] == 0 then wildlife.spawn(W, cells[i], "grazers", amount * per, id) end
  end
  return id
end

function hooks.kill_fish(W, p)
  local cells = cells_of(W, p)
  local frac = p.fraction or 1
  local id = root(W, "kill_fish", cells, #cells)
  local fish, pf = W.F.fish, W.P.fish
  local cf, ifi = W.C.fish, W.I.fish
  local ocean = W.geo.ocean
  for i = 1, #cells do
    local c = cells[i]
    if ocean[c] == 1 then
      local v = fish[c] * (1 - frac)
      if v < 1e-5 then v = 0 end
      fish[c] = v
      pf[c] = v
      blame.set(cf, ifi, c, id)
    end
  end
  return id
end

function hooks.heat(W, p)
  local cells = cells_of(W, p)
  local amount = p.amount or 10
  local id = root(W, "heat", cells, amount)
  local ha, ch, ih = W.F.heat_anom, W.C.heat_anom, W.I.heat_anom
  for i = 1, #cells do
    local c = cells[i]
    ha[c] = ha[c] + amount
    blame.set(ch, ih, c, id)
  end
  return id
end

-- Stage 1.7: villages own farm cells (land_use > 0). Until then this ignites whatever farm
-- cells exist (none) so scenarios can already reference it.
function hooks.burn_farms(W, p)
  local cells = {}
  local lu = W.F.land_use
  if p.cells then
    cells = p.cells
  else
    for c = 1, W.n do
      if lu[c] > 0 and (p.village == nil or lu[c] == p.village) then cells[#cells + 1] = c end
    end
  end
  local id = root(W, "burn_farms", cells, #cells)
  local fire = require("sim.systems.fire")
  local crop, cc, ic = W.F.crop, W.C.crop, W.I.crop
  for i = 1, #cells do
    local c = cells[i]
    if not fire.ignite(W, c, id) and crop[c] > 0 then
      crop[c] = 0
      blame.set(cc, ic, c, id)
    end
  end
  return id
end

function hooks.sink_tanker(W, p)
  local ships = W.ent.ships or {}
  local t = p.tanker and ships[p.tanker]
  if not t then
    for i = 1, #ships do if ships[i].alive then t = ships[i]; break end end
  end
  local id = root(W, "sink_tanker", { t and t.cell or 0 }, 1, { tanker = t and t.id or 0 })
  if t then require("sim.systems.ships").sink(W, t, id) end
  return id
end

function hooks.empty_stores(W, p)
  local v = W.ent.villages[p.village]
  local id = root(W, "empty_stores", { v.cell }, 1, { village = v.id })
  v.stock.grain, v.stock.fresh, v.stock.seed = 0, 0, 0
  v.foodtag[1], v.foodidle[1] = id, 0
  v.cause = id
  return id
end

function hooks.emigrate(W, p)
  local v = W.ent.villages[p.village]
  local E = W.ent
  local id = root(W, "emigrate", { v.cell }, 1, { village = v.id })
  local dest, bd = nil, math.huge
  local sites = require("sim.society.sites")
  for i = 1, #E.villages do
    local u = E.villages[i]
    if u.alive and u ~= v and u.continent == v.continent then
      local d = sites.dist(W, v.cell, u.cell)
      if d < bd then dest, bd = u, d end
    end
  end
  local nh = math.floor(#v.households * (p.fraction or 0.5) + 0.5)
  local members = {}
  local P, H = E.people, E.households
  for i = 1, nh do
    local h = H[v.households[i]]
    if h then for j = 1, #h.members do members[#members + 1] = P[h.members[j]] end end
  end
  local mob = require("sim.society.mobility")
  local mid = events.emit(W, "migration", v.cell, id, #members, { from = v.id, to = dest and dest.id or 0 })
  if dest then
    mob.depart(W, v, members, dest.id, nil, "migrants", mid, 0, 0)
  else
    for i = 1, #members do require("sim.society.entities").kill(W, members[i], "test", id) end
    require("sim.society.entities").compact(W, v)
  end
  v.labour_candidate = mid
  return id
end

-- A shipment overseas (D-059): the largest village with free boats sends porters with `grain`
-- (default 150) to the nearest village on another landmass its boats reach (or p.from / p.to).
function hooks.voyage(W, p)
  local sea = require("sim.society.sea")
  local E = W.ent
  local vs = E.villages
  local best, bu, bc = nil, nil, 1e9
  for _, v in ipairs(vs) do
    if v.alive and (not p.from or v.id == p.from) and v.mooring and #sea.free_boats(W, v) > 0 then
      for _, u in ipairs(vs) do
        if u.alive and u.continent ~= v.continent and (not p.to or u.id == p.to) then
          local c = sea.cost(W, v, u.cell, 4)
          if c and c < bc then best, bu, bc = v, u, c end
        end
      end
    end
  end
  if not best then return 0 end
  hooks.last_probe = { from = best.id, to = bu.id }
  if p.probe then return 0 end
  local ps = {}
  for i = 1, #best.members do
    local q = E.people[best.members[i]]
    local age = (W.hour - q.born) / 8064
    if q.away == 0 and age >= 18 and age < 45 and #ps < 4 then ps[#ps + 1] = q end
  end
  if #ps < 2 then return 0 end
  local id = root(W, "voyage", { best.cell }, #ps, { from = best.id, to = bu.id })
  local grain = math.min(p.grain or 150, best.stock.grain)
  best.stock.grain = best.stock.grain - grain
  local party = require("sim.society.mobility").depart(W, best, ps, bu.id, nil, "porters", id, 0, 0,
    { gift = true, grain = grain, back_to = best.id })
  hooks.last_voyage = party.id
  return id
end

-- A cattle raid (D-060): the largest village with men to spare sends raiders against the nearest
-- herd on its landmass (or p.from / p.to).
function hooks.cattle_raid(W, p)
  local conflict = require("sim.society.conflict")
  local herds = require("sim.society.herds")
  local sites = require("sim.society.sites")
  local vs = W.ent.villages
  local best, bu, bd = nil, nil, 1e9
  for _, v in ipairs(vs) do
    if v.alive and (not p.from or v.id == p.from) and not v.raid_out and conflict.fighters(W, v) >= 8 then
      for _, u in ipairs(vs) do
        if u ~= v and u.alive and u.continent == v.continent and u.herd and herds.se(u.herd) >= 30
          and (not p.to or u.id == p.to) then
          local d = sites.dist(W, v.cell, u.cell)
          if d < bd then best, bu, bd = v, u, d end
        end
      end
    end
  end
  if not best then return 0 end
  hooks.last_probe = { from = best.id, to = bu.id }
  if p.probe then return 0 end
  local list = {}
  conflict.fighters(W, best, list)
  table.sort(list, function(a, b) if a.health ~= b.health then return a.health > b.health end return a.id < b.id end)
  local party = {}
  for i = 1, math.min(p.n or 6, #list) do party[i] = list[i] end
  local id = root(W, "cattle_raid", { best.cell }, #party, { from = best.id, to = bu.id })
  best.raid_out = true
  best.raid_last = W.hour
  local pa = require("sim.society.mobility").depart(W, best, party, bu.id, nil, "raid", id, 0, 0,
    { target = bu.id, home = best.id, pwin = 0.5, cattle = true })
  hooks.last_raid = pa.id
  return id
end

-- Submission (W9): village p.village (default: the smallest) submits to p.lord (default: its
-- nearest larger neighbour on its landmass) as if beaten twice — a polity for tests and pictures.
function hooks.submit(W, p)
  local sites = require("sim.society.sites")
  local vs = W.ent.villages
  local u = p.village and vs[p.village]
  if not u then
    for _, v in ipairs(vs) do if v.alive and not v.lord and (not u or v.pop < u.pop) then u = v end end
  end
  if not u then return 0 end
  local lord = p.lord and vs[p.lord]
  if not lord then
    local bd = 1e9
    for _, v in ipairs(vs) do
      if v.alive and v ~= u and v.continent == u.continent and v.pop > u.pop and v.lord ~= u.id then
        local d = sites.dist(W, u.cell, v.cell)
        if d < bd then lord, bd = v, d end
      end
    end
  end
  if not lord then return 0 end
  local id = root(W, "submit", { u.cell }, u.pop, { village = u.id, lord = lord.id })
  u.lord, u.lord_since = lord.id, W.hour
  u.lord_ev = events.emit(W, "submission", u.cell, id, u.pop, { village = u.id, lord = lord.id })
  return id
end

world.COMMANDS.test_action = function(W, params)
  local fn = hooks[params.name]
  if not fn then error("unknown test hook " .. tostring(params.name)) end
  return fn(W, params)
end

return hooks
