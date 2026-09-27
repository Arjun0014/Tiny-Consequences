-- P08 blame coverage (07_TESTING.md §4): over every scenario run in this session, >= 90% of the
-- headline events the actions added (versus the same world without them) chain back to the
-- player. Runs after the scenarios (last in tests/sim/scenarios/index.lua).
local L = require("tests.sim.scenarios.lib")
local M = { tests = {} }

M.tests[1] = { "p08_player_consequences_are_attributed", function()
  local s = L.P08
  if s.extra < 10 then return string.format("only %d added headlines (run the scenarios first)", s.extra) end
  local share = s.player / s.extra
  local txt = string.format("%d/%d added headlines chain to the player (%.0f%%)", s.player, s.extra, 100 * share)
  if share < 0.9 then error(txt .. "; e.g. " .. table.concat(s.misses, " | "), 0) end
  return txt
end }

return M
