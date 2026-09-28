-- The planet's carbon budget, year by year: where the CO2 comes from and where it goes.
--   luajit tools/carbon_budget.lua <seed | world.tcsave> [years=200] [every=10] [--no-society]
-- Columns (ppm per year, averaged over each interval): sources — natural fire, the society's
-- takings by kind (clearing, expansion, wood, brush, forage, grazing), volcanic outgassing; sinks —
-- vegetation's net growth (forest), weathering, ocean; and the state: CO2, g_temp, the land's
-- standing biomass (forest + grass as the vegetation counts it) in ppm-equivalents, forest cover.
-- --no-society: no villages are settled (the natural world's own balance).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local cfgmod = require("sim.config")
local nosoc = false
for i = 1, #arg do if arg[i] == "--no-society" then nosoc = true end end
if nosoc then require("sim.worldgen.settle").run = function() end end
local world = require("sim.world")
local carbon = require("sim.society.carbon")
local W
if arg[1] and arg[1]:match("%.tcsave$") then
  local f = assert(io.open(arg[1], "rb")); local str = f:read("*a"); f:close()
  W = require("sim.worlds").from_save_string(str)
else
  W = require("sim.worldgen.init").generate(tonumber(arg[1]) or 9)
end
local years = tonumber(arg[2]) or 200
local every = tonumber(arg[3]) or 10
local k = cfgmod.VEG.k_co2
local function biomass()
  local F = W.F
  local land = W.geo.land_cells
  local b, fo = 0, 0
  for i = 1, #land do
    local c = land[i]
    b = b + F.forest[c] + 0.5 * F.grass[c]
    fo = fo + F.forest[c]
  end
  return b * k, fo / #land
end
-- natural fire: wrap the fire system's co2 (G.co2_src grows by fires and the society; the
-- society's share is carbon.by)
local acc = { fire = 0, soc = 0, volc = 0, forest = 0, weather = 0, ocean = 0 }
local by0 = {}
local function snap_by() local t = {} for kk, x in pairs(carbon.by) do t[kk] = x end return t end
by0 = snap_by()
local soc_kinds = { "clearing", "expansion", "wood", "brush", "forage", "grazing" }
local sock = {}
for _, kk in ipairs(soc_kinds) do sock[kk] = 0 end
local b0, f0 = biomass()
print(string.format("start: co2 %.1f ppm, g_temp %+.2f, biomass %.1f ppm-eq, forest cover %.3f%s",
  W.G.co2, W.G.g_temp, b0, f0, nosoc and " (no society)" or ""))
print("  years   co2   gT  | fire  soc: clear expan  wood brush forag graze | volc | veg_sink weath ocean | net  | biomass forest")
local soc_prev = 0
for y = 1, years do
  -- the year's society release (carbon.by is cumulative biomass by kind)
  local before = snap_by()
  local co2_src_hook = 0
  world.run_years(W, 1)
  local lf = W.G.last_flux or {}
  local after = snap_by()
  local soc = 0
  for _, kk in ipairs(soc_kinds) do
    local d = ((after[kk] or 0) - (before[kk] or 0)) * k
    sock[kk] = sock[kk] + d
    soc = soc + d
  end
  local src = lf.src or 0
  local volc = cfgmod.CLIMATE.outgassing
  local fire = src - volc - soc
  acc.fire = acc.fire + fire
  acc.soc = acc.soc + soc
  acc.volc = acc.volc + volc
  acc.forest = acc.forest + (lf.forest or 0)
  acc.weather = acc.weather + (lf.weathering or 0)
  acc.ocean = acc.ocean + (lf.ocean or 0)
  if y % every == 0 or y == years then
    local b, fo = biomass()
    local n = every
    local net = (acc.fire + acc.soc + acc.volc - acc.forest - acc.weather - acc.ocean) / n
    print(string.format("  %4d  %5.1f %+5.2f | %5.2f      %5.2f %5.2f %5.2f %5.2f %5.2f %5.2f | %4.2f | %6.2f %6.2f %6.2f | %+5.2f | %6.1f  %.3f",
      y, W.G.co2, W.G.g_temp, acc.fire / n, sock.clearing / n, sock.expansion / n, sock.wood / n, sock.brush / n,
      sock.forage / n, sock.grazing / n, acc.volc / n, acc.forest / n, acc.weather / n, acc.ocean / n, net, b, fo))
    for kk in pairs(acc) do acc[kk] = 0 end
    for _, kk in ipairs(soc_kinds) do sock[kk] = 0 end
  end
end
