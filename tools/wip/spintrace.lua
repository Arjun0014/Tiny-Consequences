-- Wolves and herds on continent K through the worldgen spin-up, a line a year (session 7, D-073):
--   luajit tools/wip/spintrace.lua <seed> <continent> [extra years]
package.path = "./?.lua;./?/init.lua;" .. package.path
-- per spin-up year: wolves, packs, lone wolves, pack condition, herds/animals/condition on continent k
local seed, k = tonumber(arg[1]) or 5, tonumber(arg[2]) or 3
local extra = tonumber(arg[3]) or 0
local world = require("sim.world")
local config = require("sim.config")
local worldgen = require("sim.worldgen.init")
local climate_init = require("sim.worldgen.climate_init")
local settle = require("sim.worldgen.settle")
local function census(W, label)
  local A = W.S.wild
  if not A then return end
  local set = {}
  for c = 1, W.n do if W.geo.continent[c] == k then set[c] = true end end
  local wolves, packs, lone, pc, herds, animals, hc = 0, 0, 0, 0, 0, 0, 0
  for i = 1, A.np do if set[A.p_cell[i]] then wolves = wolves + A.p_n[i]; packs = packs + 1; pc = pc + A.p_cond[i] * A.p_n[i]; if A.p_n[i] == 1 then lone = lone + 1 end end end
  for i = 1, A.nh do if set[A.h_cell[i]] then herds = herds + 1; animals = animals + A.h_n[i]; hc = hc + A.h_n[i] * A.h_cond[i] end end
  local d = W.scratch.wl_dbg
  local flows = ""
  if d then
    flows = string.format(" | pups %d starved %d old %d left %d lived %d joined %d founded %d paired %d | fawns %.0f starve %.0f pred %.0f",
      d.pups, d.pstarve, d.pmort, d.left, d.live, d.joined, d.founded, d.paired or 0, d.born, d.hstarve, d.pred)
    for kk in pairs(d) do if type(d[kk]) == "number" then d[kk] = 0 end end
  else
    W.scratch.wl_dbg = { born = 0, hstarve = 0, pred = 0, hunt = 0, pups = 0, pstarve = 0, pmort = 0, pother = 0, left = 0,
      live = 0, joined = 0, founded = 0, newpacks = 0, gonepacks = 0, paired = 0, only = set }
  end
  print(string.format("%s packs %2d wolves %3d lone %2d cond %.2f | herds %3d animals %5.0f cond %.2f%s", label, packs, wolves, lone,
    pc / math.max(1, wolves), herds, animals, hc / math.max(1, animals), flows))
end
local W = worldgen.generate(seed, { no_spinup = true })
-- (worldgen.spinup, with a census a year)
W.record_events = false
W.G.calibrating = true
local years = config.SPINUP_YEARS
local ticks = years * 48
local veg_at = math.min(ticks, config.SPINUP_VEG_YEAR * 48)
local settle_at = math.min(ticks, config.SETTLE.year * 48)
for t = 1, ticks do
  world.tick(W)
  if t == veg_at then
    climate_init.vegetation(W)
    climate_init.fauna(W)
    require("sim.systems.biome").reset(W)
    census(W, "seeded ")
  end
  if t == settle_at then settle.run(W) end
  if t % 48 == 0 and t > veg_at then census(W, string.format("spin y%2d", t / 48)) end
end
W.G.calibrating = false
W.record_events = true
world.shift_ticks(W, -W.tick)
for y = 1, extra do
  world.run_years(W, 1)
  census(W, string.format("year %3d", y))
end
