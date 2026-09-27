-- P09 event rate (07_TESTING.md §4): untouched worlds make natural headlines at 0.2-3 a year on
-- average over the profile's seeds (neither dead nor spammy), and no seed above 3 a year.
local T = require("tests.sim.lib")
local B = require("tests.sim.support.baseline")
local scen = require("tests.sim.scenarios.lib")
local M = { tests = {} }

M.tests[1] = { "p09_headline_rate", function()
  local seeds = scen.SEEDS[T.args.full and "full" or "quick"]
  local total, rates = 0, {}
  for _, seed in ipairs(seeds) do
    local r = B.run(seed, 200)
    local W = r.W
    local rate = (W.E.headline_count or 0) / r.years
    rates[#rates + 1] = string.format("%.2f", rate)
    total = total + rate
    T.ok(rate <= 3, "seed " .. seed .. " spammy: " .. rate)
  end
  local mean = total / #seeds
  T.ok(mean >= 0.2 and mean <= 3, "mean headline rate " .. mean)
  return string.format("mean %.2f/y (%s)", mean, table.concat(rates, " "))
end }

return M
