-- S04 (tanker oil) month by month (session 8): the target village's food against the untouched
-- world — food security, the store, the catch, wild food (hunting and gathering), milk and meat of
-- its herds, relief and purchases.
--   luajit tools/wip/s04trace.lua <seed> [years=3]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local seed, years = tonumber(arg[1]) or 1, tonumber(arg[2]) or 3
require("sim.config").SOCIETY.soft_cap = 2400
local world = require("sim.world")
local L = require("tests.sim.scenarios.lib")
local Wa, Wb = L.fresh_world(seed), L.fresh_world(seed)
local tg
local function acc(v, k) return (v.acc and v.acc[k] or 0) end
local last = {}
local function row(W, tag)
  local v = W.ent.villages[tg.village]
  local k = tag
  local l = last[k] or {}
  local cur = { catch = acc(v, "catch"), wild = acc(v, "meat") + acc(v, "forage"), relief = acc(v, "relief_in"), bought = acc(v, "food_bought"),
    harvest = acc(v, "harvest"), milk = acc(v, "milk") }
  local d = {}
  for kk, x in pairs(cur) do d[kk] = x - (l[kk] or 0); if d[kk] < 0 then d[kk] = x end end
  last[k] = cur
  return string.format("%s fs %.2f store %5.0f pop %3d catch %5.0f wild %4.0f milk %4.0f harv %5.0f relief %4.0f bought %4.0f%s", tag,
    v.food_security or 0, v.stock.grain + v.stock.fresh, v.pop, d.catch, d.wild, d.milk, d.harvest, d.relief, d.bought, v.rationing and " RATION" or "")
end
for t = 0, years * 48 - 1 do
  if t == 48 then
    tg = L.select(Wa, "ocean_near:village:fishing")
    local v = Wa.ent.villages[tg.village]
    print(string.format("seed %d: village %s (%d people, %d boats), spill at %d", seed, v.name, v.pop, #v.boats, tg.cell))
    world.command(Wa, "power", { power = "oil", cell = tg.cell })
  end
  world.tick(Wa); world.tick(Wb)
  if tg and t % 4 == 3 then
    print(string.format("y%4.2f | %s | %s", t / 48, row(Wa, "A"), row(Wb, "B")))
  end
end
