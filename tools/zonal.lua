-- Zonal-mean diagnostics: precip/soil/temp/ice by 10-degree latitude band (land vs ocean).
package.path = "./?.lua;./?/init.lua;" .. package.path
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local seed = tonumber(arg[1] or 42)
local years = tonumber(arg[2] or 5)
local W = worldgen.generate(seed, {})
local B = {}
for b = 1, 18 do B[b] = { lp = 0, ln = 0, op = 0, on = 0, ls = 0, t = 0, n = 0, ice = 0 } end
for _ = 1, years * 48 do
  world.tick(W)
  local F = W.F
  for c = 1, W.n do
    local b = math.min(18, math.floor((W.grid.lat[c] / math.pi + 0.5) * 18) + 1)
    local x = B[b]
    x.n = x.n + 1; x.t = x.t + F.temp[c]; x.ice = x.ice + F.ice[c]
    if W.geo.ocean[c] == 1 then x.op = x.op + F.precip[c]; x.on = x.on + 1
    else x.lp = x.lp + F.precip[c]; x.ln = x.ln + 1; x.ls = x.ls + F.soil_water[c] end
  end
end
print("lat      T     ice   P_ocean  P_land  soil_land  (land cells/tick)")
for b = 18, 1, -1 do
  local x = B[b]
  print(string.format("%+4d  %6.1f  %5.2f  %7.3f  %7.3f  %6.2f  %d", (b - 10) * 10 + 5, x.t / x.n, x.ice / x.n,
    x.on > 0 and x.op / x.on or 0, x.ln > 0 and x.lp / x.ln or 0, x.ln > 0 and x.ls / x.ln or 0, x.ln / (years * 48)))
end
