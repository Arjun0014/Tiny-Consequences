-- Society cost by function (W6): Lua VM instructions per planet week for the society's main
-- functions, on a world run to a given year (the JIT off, a count hook: load-independent).
--   luajit tools/society_profile.lua [seed] [years] [weeks]
package.path = "./?.lua;./?/init.lua;" .. package.path
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local W = worldgen.generate(tonumber(arg[1]) or 22)
world.run_years(W, tonumber(arg[2]) or 30)
local cnt = 0
local STEP = 20
local acc, calls = {}, {}
local function wrap(mod, modname, fname)
  local f = mod[fname]
  mod[fname] = function(...)
    local a = cnt
    local r = { f(...) }
    local k = modname .. "." .. fname
    acc[k] = (acc[k] or 0) + (cnt - a) * STEP
    calls[k] = (calls[k] or 0) + 1
    return unpack(r, 1, 6)
  end
end
local labour = require("sim.society.labour")
local fishing = require("sim.society.fishing")
local economy = require("sim.society.economy")
local life = require("sim.society.life")
local construction = require("sim.society.construction")
local farming = require("sim.society.farming")
local mobility = require("sim.society.mobility")
local wday = require("sim.society.weather_day")
local daylight = require("sim.society.daylight")
local sites = require("sim.society.sites")
local crafts = require("sim.society.crafts")
local relations = require("sim.society.relations")
local status = require("sim.society.status")
local conflict = require("sim.society.conflict")
local schedule = require("sim.society.schedule")
for _, x in ipairs({
  { crafts, "crafts", { "weekly", "demands", "sources", "need_year" } },
  { relations, "relations", { "monthly", "trade", "market", "kin_monthly", "brides" } },
  { status, "status", { "monthly" } },
  { conflict, "conflict", { "monthly", "fade" } },
  { labour, "labour", { "execute", "assign", "fuel_day", "woodlot", "brush", "forage_cell", "fuel_households" } },
  { fishing, "fishing", { "plan", "settle", "rank" } },
  { economy, "economy", { "daily", "weekly", "monthly", "need_day" } },
  { life, "life", { "weekly", "births", "monthly", "memories", "households", "marry_waiting", "set_occupation", "retrain" } },
  { construction, "construction", { "plan", "decay", "work", "capacities", "housing_need" } },
  { farming, "farming", { "plan", "daily", "capacity", "days_to_harvest", "weekly", "work", "demands", "field_sites" } },
  { mobility, "mobility", { "monthly", "hour" } },
  { wday, "wday", { "get" } },
  { daylight, "daylight", { "sun_times", "rhythm" } },
  { sites, "sites", { "dist", "rings" } },
  { require("sim.society.herds"), "herds", { "weekly", "sync", "capacity" } },
  { require("sim.society.sea"), "sea", { "daily", "cost", "landing", "field", "site" } },
  { conflict, "conflict", { "cattle_target", "defence", "allies" } },
}) do
  for _, fn in ipairs(x[3]) do wrap(x[1], x[2], fn) end
end
if rawget(_G, "jit") then jit.off(); jit.flush() end -- lint:allow luajit-only
debug.sethook(function() cnt = cnt + 1 end, "", STEP)
local c0 = cnt
local weeks = tonumber(arg[3]) or 96
for _ = 1, weeks do world.tick(W) end
debug.sethook()
local total = (cnt - c0) * STEP / weeks
local pop = 0
for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop end end
print(string.format("total %.0f instr/week, people %d, villages alive %d", total, pop, (function() local n=0 for _,v in ipairs(W.ent.villages) do if v.alive then n=n+1 end end return n end)()))
local keys = {}
for k in pairs(acc) do keys[#keys + 1] = k end
table.sort(keys, function(a, b) return acc[a] > acc[b] end)
for _, k in ipairs(keys) do
  print(string.format("  %-28s %9.0f /week  %7d calls  %7.0f per call", k, acc[k] / weeks, calls[k], acc[k] / calls[k]))
end
