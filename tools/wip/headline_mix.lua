-- Headlines by kind over a run (session 8): luajit tools/wip/headline_mix.lua <seed> [years=40] [cap=2400]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
require("sim.config").SOCIETY.soft_cap = tonumber(arg[3]) or 2400
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local W = worlds.fresh(tonumber(arg[1]) or 1)
local years = tonumber(arg[2]) or 40
local e0 = W.E.next_id
world.run_years(W, years)
local k, all = {}, {}
for id = e0, W.E.next_id - 1 do
  local r = W.E.rec[id]
  if r then
    all[r.kind] = (all[r.kind] or 0) + 1
    if r.headline then k[r.kind] = (k[r.kind] or 0) + 1 end
  end
end
local t, n = {}, 0
for kind, c in pairs(k) do t[#t + 1] = { kind, c }; n = n + c end
table.sort(t, function(a, b) return a[2] > b[2] end)
print(string.format("%d headlines in %d years (%.2f/y)", n, years, n / years))
for i = 1, math.min(20, #t) do print(string.format("  %-24s %4d  (of %d events)", t[i][1], t[i][2], all[t[i][1]])) end
