-- P06 (07_TESTING.md §4): no NaN/inf, every field within its declared range (fields.LIST),
-- checked yearly over 500 years of the untouched world on 2 seeds.
local B = require("tests.sim.support.baseline")
local M = { tests = {} }

M.tests[1] = { "p06_fields_finite_and_in_range_500y", function()
  local parts = {}
  for _, seed in ipairs({ 1, 2 }) do
    local r = B.run(seed, 500)
    local n = r.violations.count or 0
    if n > 0 then
      error(string.format("seed %d: %d violations, first: %s", seed, n, table.concat(r.violations, "; ")), 0)
    end
    local y = r.yearly[500]
    parts[#parts + 1] = string.format("s%d ok (y500 forest %.2f gT %+.2f)", seed, y.forest, y.g_temp)
  end
  return table.concat(parts, ", ")
end }

return M
