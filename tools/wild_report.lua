-- Wild herds and packs over time (W4, D-070): groups and animals by species, the derived fields,
-- where herds are (distance to the nearest village), moves, and the ecology's events.
--   luajit tools/wild_report.lua <seed | world.tcsave> [years=50] [every=5] [--no-society]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local nosoc = false
for i = 1, #arg do if arg[i] == "--no-society" then nosoc = true end end
if nosoc then require("sim.worldgen.settle").run = function() end end
local cfgmod = require("sim.config")
local world = require("sim.world")
local W
if arg[1] and arg[1]:match("%.tcsave$") then
  local f = assert(io.open(arg[1], "rb")); local str = f:read("*a"); f:close()
  W = require("sim.worlds").from_save_string(str)
else
  W = require("sim.worldgen.init").generate(tonumber(arg[1]) or 22)
end
local years = tonumber(arg[2]) or 50
local every = tonumber(arg[3]) or 5
local cfg = cfgmod.WILD
local function report(y)
  local A = W.S.wild
  local hn, ha, pn, pa = {}, {}, {}, {}
  local moved = 0
  for i = 1, A.nh do
    local s = A.h_sp[i]
    hn[s] = (hn[s] or 0) + 1
    ha[s] = (ha[s] or 0) + A.h_n[i]
    if W.hour - A.h_moved[i] < 8064 then moved = moved + 1 end
  end
  for i = 1, A.np do
    local s = A.p_sp[i]
    pn[s] = (pn[s] or 0) + 1
    pa[s] = (pa[s] or 0) + A.p_n[i]
  end
  local parts = {}
  for s = 1, #cfg.grazers do
    if hn[s] then parts[#parts + 1] = string.format("%s %d herds/%d", cfg.grazers[s].name, hn[s], math.floor(ha[s] + 0.5)) end
  end
  for s = 1, #cfg.predators do
    if pn[s] then parts[#parts + 1] = string.format("%s %d packs/%d", cfg.predators[s].name, pn[s], math.floor(pa[s] + 0.5)) end
  end
  local F = W.F
  local sg, sp = 0, 0
  for _, c in ipairs(W.geo.land_cells) do sg = sg + F.grazers[c]; sp = sp + F.predators[c] end
  local pop = 0
  for _, v in ipairs(W.ent.villages or {}) do if v.alive then pop = pop + v.pop end end
  print(string.format("y%-4d %s | fields %.1f / %.2f | herds moved this year %d | people %d | co2 %.0f",
    y, table.concat(parts, ", "), sg, sp, moved, pop, W.G.co2))
end
report(0)
local c = {}
local last = W.E.next_id - 1
for y = 1, years do
  world.run_years(W, 1)
  for id = last + 1, W.E.next_id - 1 do local r = W.E.rec[id]; if r then c[r.kind] = (c[r.kind] or 0) + 1 end end
  last = W.E.next_id - 1
  if y % every == 0 or y == years then report(y) end
end
local ks = {}
for k, n in pairs(c) do
  if k:find("population") or k:find("extinct") or k:find("boom") or k:find("crash") or k:find("biome") then ks[#ks + 1] = k .. " " .. n end
end
table.sort(ks)
print("events: " .. table.concat(ks, ", "))
