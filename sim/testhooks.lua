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
  local pd, P = W.F.predators, W.P.predators
  local cp, ip = W.C.predators, W.I.predators
  for i = 1, #cells do
    local c = cells[i]
    if pd[c] > 0 then
      local v = pd[c] * (1 - frac)
      if v < 1e-5 then v = 0 end
      pd[c] = v
      P[c] = v
    end
    blame.set(cp, ip, c, id)
  end
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
  local gz, P = W.F.grazers, W.P.grazers
  local cg, ig = W.C.grazers, W.I.grazers
  local ocean = W.geo.ocean
  for i = 1, #cells do
    local c = cells[i]
    if ocean[c] == 0 then
      local v = gz[c] + amount
      if v > 1 then v = 1 end
      gz[c] = v
      P[c] = v
      blame.set(cg, ig, c, id)
    end
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

world.COMMANDS.test_action = function(W, params)
  local fn = hooks[params.name]
  if not fn then error("unknown test hook " .. tostring(params.name)) end
  return fn(W, params)
end

return hooks
