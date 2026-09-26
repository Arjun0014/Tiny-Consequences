-- P06 (07_TESTING.md §4): no NaN/inf, every field within its declared range (fields.LIST),
-- checked yearly over the untouched world: quick = 1 seed x 200 years (shared with S01),
-- full = 2 seeds x 500 years.
local T = require("tests.sim.lib")
local B = require("tests.sim.support.baseline")
local M = { tests = {} }

M.tests[1] = { "p06_fields_finite_and_in_range", function()
  local parts = {}
  local seeds, years = { 1 }, 200
  if T.args.full then seeds, years = { 1, 2 }, 500 end
  for _, seed in ipairs(seeds) do
    local r = B.run(seed, years)
    local n = r.violations.count or 0
    if n > 0 then
      error(string.format("seed %d: %d violations, first: %s", seed, n, table.concat(r.violations, "; ")), 0)
    end
    local y = r.yearly[years]
    parts[#parts + 1] = string.format("s%d ok to y%d (forest %.2f gT %+.2f)", seed, years, y.forest, y.g_temp)
  end
  return table.concat(parts, ", ")
end }

return M
