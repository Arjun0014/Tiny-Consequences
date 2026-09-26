-- P02 on the real world: save mid-year (with oil, culled predators and live fauna/ocean state),
-- continue; reload + continue; state hashes must match.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local save = require("sim.save")
local M = {}

function M.real_world_round_trip_bit_identical()
  local W = scen.fresh_world(1)
  save.enable_capture(W)
  world.run_ticks(W, 48 + 10)
  local tg = scen.select(W, "ocean_near:coast:largest")
  world.test_action(W, "oil", { cell = tg.cell, amount = 3 })
  world.test_action(W, "remove_predators", { continent = 2, fraction = 0.9 })
  world.run_ticks(W, 48 + 7)
  local s = save.save_world(W, { cmdlog = W.cmdlog })
  world.run_ticks(W, 48 + 20)
  local h1 = world.state_hash(W)
  local W2 = save.restore(assert(save.decode(s)))
  T.eq(W2.tick, 96 + 17, "restored tick")
  world.run_ticks(W2, 48 + 20)
  T.eq(world.state_hash(W2), h1, "state hash after reload")
  return string.format("save %d bytes", #s)
end

return M
