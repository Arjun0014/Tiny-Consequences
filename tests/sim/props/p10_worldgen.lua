-- P10 (07_TESTING.md §4): worldgen constraints hold for 20 of 20 seeds — continents (2–3,
-- each ≥ 12%), islands (4–8), land 30–40%, villages (3–5 with 150–250 people, ≥ 1 fishing,
-- ≥ 1 inland, on ≥ 2 continents), volcano sites (5–10 arcs + 1–3 hotspots, ≥ 1 in the
-- ocean), tanker lanes (1–2 closed loops of adjacent ocean cells next to a village).
-- Village constraints are checked at the settle (spin-up year SETTLE.year), when they are made.
local T = require("tests.sim.lib")
local worldgen = require("sim.worldgen.init")
local worlds = require("tests.sim.support.worlds")
local cfgmod = require("sim.config")
local M = { tests = {} }

local function check(rep)
  local s = rep.settle
  local wc, sc = cfgmod.WORLDGEN, cfgmod.SETTLE
  local bad = {}
  local function need(cond, what) if not cond then bad[#bad + 1] = what end end
  need(rep.continents >= 2 and rep.continents <= 3, "continents " .. rep.continents)
  need(rep.islands >= wc.islands_min and rep.islands <= wc.islands_max, "islands " .. rep.islands)
  need(rep.land_frac >= wc.land_frac[1] and rep.land_frac <= wc.land_frac[2], string.format("land %.2f", rep.land_frac))
  need(s ~= nil, "no settle report")
  if s then
    need(s.villages >= sc.villages[1] and s.villages <= sc.villages[2], "villages " .. s.villages)
    need(s.people >= sc.people[1] and s.people <= sc.people[2], "people " .. s.people)
    need(s.fishing, "no fishing village")
    need(s.inland, "no inland village")
    need(s.continents >= 2, "villages on " .. s.continents .. " continent(s)")
    need(s.arcs >= sc.arcs[1] and s.arcs <= sc.arcs[2], "arcs " .. s.arcs)
    need(s.hotspots >= sc.hotspots[1] and s.hotspots <= sc.hotspots[2], "hotspots " .. s.hotspots)
    need(s.ocean_hotspot, "no ocean hotspot")
    need(s.lanes >= sc.lanes[1] and s.lanes <= sc.lanes[2], "lanes " .. s.lanes)
    need(s.lanes_ok, "lane not a closed ocean loop")
  end
  return bad
end

M.tests[1] = { "p10_worldgen_constraints_20_seeds", function()
  local fails = {}
  local tv, tp = 0, 0
  for seed = 1, 20 do
    local rep = worlds.reports[seed]
    if not rep then rep = worldgen.generate(seed).worldgen_report end
    local bad = check(rep)
    if #bad > 0 then fails[#fails + 1] = "seed " .. seed .. ": " .. table.concat(bad, ", ") end
    tv = tv + rep.settle.villages
    tp = tp + rep.settle.people
  end
  if #fails > 0 then error(table.concat(fails, "; "), 0) end
  return string.format("20/20 seeds; mean %.1f villages, %.0f people", tv / 20, tp / 20)
end }

return M
