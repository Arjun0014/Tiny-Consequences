-- A v3 world (D-078): the v2 planet (terrain, climate spin-up, vegetation, herds and packs),
-- without the v2 society or the tanker lanes, switched to the v3 calendar (a planet step per
-- game day), with agent settlements founded on it.
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local found = require("sim.agents.found")

local M = {}

function M.systems()
  return {
    require("sim.powers"),
    require("sim.systems.globalclimate"),
    require("sim.systems.temperature"),
    require("sim.systems.moisture"),
    require("sim.systems.ice"),
    require("sim.systems.hydrology"),
    require("sim.systems.vegetation"),
    require("sim.systems.fire"),
    require("sim.systems.ocean"),
    require("sim.systems.fauna"),
    require("sim.agents.init"),
    require("sim.systems.biome"),
    require("sim.causality.detectors"),
  }
end

function M.empty_state(W)
  local E = W.ent
  E.villages = E.villages or {}
  E.people = E.people or {}
  E.households = E.households or {}
  E.buildings = E.buildings or {}
  E.boats = E.boats or {}
  E.parties = E.parties or {}
  E.ships = E.ships or {}
  W.S.v3 = W.S.v3 or { settlements = {}, people = {}, households = {}, order = {}, next_person = 1,
    next_household = 1, next_building = 1, now = W.hour }
end

-- opts: spinup_years, progress, site (cell), start_hour (local time of day to begin, default 0)
function M.generate(seed, opts)
  opts = opts or {}
  local W = worldgen.generate(seed, { systems = M.systems(), no_settle = true, progress = opts.progress,
    spinup_years = opts.spinup_years, level = opts.level })
  -- the v3 calendar: the clock restarts at day 0 (the first day of spring)
  W.v3 = true
  W.hpt = 24
  W.hour, W.tick = 0, 0
  M.empty_state(W)
  local cell = opts.site or found.pick_site(W)
  if not cell then error("v3: no site for a settlement on seed " .. tostring(seed)) end
  found.found(W, cell, opts)
  -- more settlements (opts.settlements): the best sites at least 4 cells from any other
  local used = { cell }
  for k = 2, opts.settlements or 1 do
    local c2 = found.pick_site(W, { dawn = false, avoid = used, min_steps = 4 })
    if not c2 then break end
    used[#used + 1] = c2
    found.found(W, c2, opts)
  end
  world.init_systems(W)
  return W
end

return M
