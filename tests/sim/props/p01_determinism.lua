local T = require("tests.sim.lib")
local world = require("sim.world")
local toy = require("tests.sim.support.toy")
local M = {}

function M.p01_trivial_world()
  local function run()
    local W = toy.make_world(99, 3)
    world.run_ticks(W, 48 * 5)
    world.command(W, "toy_spill", { cell = 40 })
    world.run_ticks(W, 48 * 5)
    return world.state_hash(W)
  end
  local a, b = run(), run()
  T.eq(a, b)
  -- a different seed differs
  local W = toy.make_world(100, 3)
  world.run_ticks(W, 48 * 10)
  T.ok(world.state_hash(W) ~= a)
end

-- The real world (all systems + test hooks): two runs from the same seed and commands agree.
function M.p01_real_world()
  local scen = require("tests.sim.scenarios.lib")
  local function run()
    local W = scen.fresh_world(2)
    world.run_ticks(W, 60)
    world.test_action(W, "remove_predators", { continent = 1, fraction = 1 })
    local tg = scen.select(W, "ocean_near:coast:largest")
    world.test_action(W, "oil", { cell = tg.cell, amount = 4 })
    world.run_years(W, 3)
    return world.state_hash(W), W.E.next_id
  end
  local h1, n1 = run()
  local h2, n2 = run()
  T.eq(h1, h2, "hash")
  T.eq(n1, n2, "event count")
  return string.format("%d events", n1 - 1)
end

return M
