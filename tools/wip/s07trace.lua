-- S07 (invasive island) month by month (session 8): the introduced herds on the island (animals,
-- condition, crowding), its grass and forest against the untouched world, the veg ratio S07 scores.
--   luajit tools/wip/s07trace.lua <seed> [years=12] [every=3 months]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local seed, years, every = tonumber(arg[1]) or 7, tonumber(arg[2]) or 12, tonumber(arg[3]) or 3
require("sim.config").SOCIETY.soft_cap = 1200
local world = require("sim.world")
local L = require("tests.sim.scenarios.lib")
local Wa, Wb = L.fresh_world(seed), L.fresh_world(seed)
local tg
local function island(W)
  local A = W.S.wild
  local set = {}
  for _, c in ipairs(tg.cells) do set[c] = true end
  local n, h, cond, dd = 0, 0, 0, 0
  for i = 1, A.nh do if set[A.h_cell[i]] then n = n + A.h_n[i]; h = h + 1; cond = cond + A.h_cond[i] * A.h_n[i] end end
  local g, f = 0, 0
  local sdd = W.scratch.wl_dd
  for _, c in ipairs(tg.cells) do g = g + W.F.grass[c]; f = f + W.F.forest[c]; dd = dd + (sdd and sdd[c] or 0) end
  local k = #tg.cells
  return n, h, cond / math.max(1, n), g / k, f / k, dd / k
end
for t = 0, years * 48 - 1 do
  if t == 48 then
    tg = L.select(Wa, "island:no_predators")
    print(string.format("seed %d: %s, %d cells", seed, tg.label, #tg.cells))
    world.command(Wa, "power", { power = "introduce", species = "grazers", radius = 2, cell = tg.cell })
  end
  world.tick(Wa); world.tick(Wb)
  if tg and t % (4 * every) == 3 then
    local n, h, cond, g, f, dd = island(Wa)
    local _, _, _, gb, fb = island(Wb)
    print(string.format("y%5.2f  %3d herds %6.0f animals cond %.2f crowd %.2f | grass %.3f (%.3f) forest %.3f (%.3f) | veg x%.3f",
      t / 48, h, n, cond, dd, g, gb, f, fb, (g + f) / math.max(1e-9, gb + fb)))
  end
end
