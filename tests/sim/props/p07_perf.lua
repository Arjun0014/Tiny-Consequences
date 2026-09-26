-- P07 (07_TESTING.md §4): performance regression against tests/bench-baseline.json, measured in
-- Lua VM instructions per tick on a fixed world (deterministic; D-029), not wall time. Fails when
-- a system grows > 30% (and by > 2,000 instructions) or the whole tick > 15%.
-- Intentional changes: npm run bench -- --update-baseline (and a DECISIONS.md entry).
local perf = require("tools.perf")
local M = { tests = {} }

M.tests[1] = { "p07_instructions_per_tick_vs_baseline", function()
  local base = perf.load_baseline()
  if not base then error("tests/bench-baseline.json missing: npm run bench -- --update-baseline", 0) end
  local res = perf.measure(base.config)
  local ok, lines = perf.compare(res, base)
  local d = 100 * (res.total - base.instructions_per_tick) / base.instructions_per_tick
  local summary = string.format("%d instr/tick (%+.1f%% vs baseline %d, ~%.2f ms in the browser)",
    res.total, d, base.instructions_per_tick, res.total * 7.4e-6)
  if not ok then error(summary .. "; regressions: " .. table.concat(lines, "; "), 0) end
  return summary
end }

return M
