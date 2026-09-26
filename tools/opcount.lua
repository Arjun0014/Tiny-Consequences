-- Deterministic cost measure (load-independent): Lua VM instructions per tick, per system.
--   luajit -joff tools/opcount.lua [--seed 42] [--ticks 480] [--level 3]
-- Counts come from a debug count hook (every 100 instructions, so each system's count is ±100 per
-- call, unbiased over many ticks). C functions (math.exp, ...) count as one call instruction.
-- Calibration (D-019): the HTML5 release tick of the pre-1.6 systems measured 4.6 ms for ≈ 314k
-- instructions, i.e. ≈ 15 ns per instruction in the browser, so instructions/tick × 15 ns ≈ browser
-- ms. Wall-clock timings on a busy machine are unreliable; these counts are exact.
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { seed = 42, ticks = 480 }
local i = 1
while arg[i] do
  if arg[i] == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif arg[i] == "--ticks" then i = i + 1; args.ticks = tonumber(arg[i])
  elseif arg[i] == "--level" then i = i + 1; args.level = tonumber(arg[i]) end
  i = i + 1
end
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local W = worldgen.generate(args.seed, { level = args.level })
world.run_years(W, 1)
local STEP = 100
local cnt = 0
W.profile = { clock = function() return cnt * STEP end, ms = {} }
debug.sethook(function() cnt = cnt + 1 end, "", STEP)
local n = args.ticks
local a = cnt
for _ = 1, n do world.tick(W) end
local total = (cnt - a) * STEP / n
debug.sethook()
local NS = 15e-6   -- ms per instruction in the HTML5 build (calibrated, see header)
print(string.format("level %d (%d cells), seed %d, %d ticks: %.0f instructions/tick (≈ %.2f ms in the browser)",
  W.level, W.n, args.seed, n, total, total * NS))
local sum = 0
for _, s in ipairs(world.systems) do
  local v = (W.profile.ms[s.name] or 0) / n
  sum = sum + v
  print(string.format("  %-14s %8.0f  (%.3f ms)", s.name, v, v * NS))
end
print(string.format("  %-14s %8.0f  (tick overhead: snaps, commands)", "other", total - sum))
