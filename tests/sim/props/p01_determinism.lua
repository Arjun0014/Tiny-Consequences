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

return M
