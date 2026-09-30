-- Packs, wolves, herds and animals on each continent S01 scores, at year Y (session 7, D-073):
--   luajit tools/wip/wolfcensus.lua <first seed> [years=0] [last seed]
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local B = require("tests.sim.support.baseline")
local years = tonumber(arg[2]) or 0
local s0, s1 = tonumber(arg[1]) or 1, tonumber(arg[3]) or tonumber(arg[1]) or 10
for seed = s0, s1 do
  local W = require("sim.worldgen.init").generate(seed)
  if years > 0 then world.run_years(W, years) end
  local conts = B.continents(W)
  local A = W.S.wild
  local parts = {}
  for _, k in ipairs(conts) do
    local wolves, packs, herds, animals, land = 0, 0, 0, 0, 0
    for i = 1, A.np do if W.geo.continent[A.p_cell[i]] == k then wolves = wolves + A.p_n[i]; packs = packs + 1 end end
    for i = 1, A.nh do if W.geo.continent[A.h_cell[i]] == k then animals = animals + A.h_n[i]; herds = herds + 1 end end
    for c = 1, W.n do if W.geo.continent[c] == k and W.geo.ocean[c] == 0 then land = land + 1 end end
    parts[#parts + 1] = string.format("c%d: %d land cells, %d packs %d wolves, %d herds %.0f animals", k, land, packs, wolves, herds, animals)
  end
  print(string.format("seed %d y%d | %s", seed, years, table.concat(parts, " | ")))
end
