-- P02 (07_TESTING.md §4): save round trip on a real world with villages, people, ships and oil:
--   save at year 20 (mid-year, with commands issued since the snapshot), continue to year 40
--   == load + continue to year 40 (state hash).
-- P03: command-log replay — replaying the whole command log from the Year-0 snapshot
--   reproduces the live state (hash) after 10 years of test-hook actions.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local save = require("sim.save")
local M = { tests = {} }

M.tests[1] = { "p02_save_year20_continue_to_40", function()
  local W = worlds.fresh(2)
  save.enable_capture(W)
  world.run_ticks(W, 20 * 48 + 9)
  world.test_action(W, "sink_tanker", {})
  world.test_action(W, "burn_farms", { village = 1 })
  world.run_ticks(W, 11)
  local s = save.save_world(W, { cmdlog = W.cmdlog })
  world.run_ticks(W, 40 * 48 - W.tick)
  local h1 = world.state_hash(W)
  local W2 = save.restore(assert(save.decode(s)))
  T.eq(W2.tick, 20 * 48 + 20, "restored tick")
  world.run_ticks(W2, 40 * 48 - W2.tick)
  T.eq(world.state_hash(W2), h1, "state hash at year 40")
  local pop = 0
  for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop end end
  return string.format("save %d KB, %d villages ever, pop %d at y40", math.floor(#s / 1024), #W.ent.villages, pop)
end }

M.tests[2] = { "p03_command_log_replay", function()
  local W = worlds.fresh(3)
  local first
  W.on_year_boundary = function(w) if not first then first = save.capture(w) end end
  local scen = require("tests.sim.scenarios.lib")
  world.run_ticks(W, 60)
  world.test_action(W, "remove_predators", { continent = 1, fraction = 0.8 })
  world.run_ticks(W, 100)
  local tg = scen.select(W, "ocean_near:village:fishing")
  world.test_action(W, "oil", { cell = tg.cell, amount = 4 })
  world.run_ticks(W, 150)
  world.test_action(W, "burn_farms", {})
  world.test_action(W, "add_grazers", { cells = { W.geo.land_cells[7] }, amount = 0.3 })
  world.run_ticks(W, 10 * 48 - W.tick)
  local h1 = world.state_hash(W)
  T.ok(first ~= nil and first.tick == 0, "Year-0 snapshot")
  local doc = assert(save.decode(save.encode(first, W.cmdlog, W.tick, {})))
  local W2 = save.restore(doc)
  T.eq(W2.tick, W.tick, "replayed to the same tick")
  T.eq(world.state_hash(W2), h1, "replayed state hash")
  return string.format("%d commands replayed over %d ticks", #W.cmdlog, W.tick)
end }

return M
