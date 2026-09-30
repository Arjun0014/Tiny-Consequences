-- Wolves, herds, people and forest on each continent S01 scores, a line every N years, at the full
-- profile's soft cap (session 8):
--   luajit tools/wip/conttrace.lua <seed> [years=200] [every=5] [--cap N] [--only K]
-- With --only K the wild flows of continent K (pups, starved, dispersers; fawns, starvation,
-- predation, hunting) are summed over each interval.
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local pos, cap, only = {}, 2400, nil
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--cap" then cap = tonumber(arg[i + 1]); i = i + 1
  elseif a == "--only" then only = tonumber(arg[i + 1]); i = i + 1
  else pos[#pos + 1] = a end
  i = i + 1
end
local seed, years, every = tonumber(pos[1]) or 4, tonumber(pos[2]) or 200, tonumber(pos[3]) or 5
local config = require("sim.config")
config.SOCIETY.soft_cap = cap
local world = require("sim.world")
local B = require("tests.sim.support.baseline")
local W = require("sim.worldgen.init").generate(seed)
local conts = B.continents(W)
local cont = W.geo.continent
if only then
  local set = {}
  for c = 1, W.n do if cont[c] == only then set[c] = true end end
  W.scratch.wl_dbg = { born = 0, hstarve = 0, pred = 0, hunt = 0, pups = 0, pstarve = 0, pmort = 0, pother = 0, left = 0,
    live = 0, joined = 0, founded = 0, newpacks = 0, gonepacks = 0, paired = 0, only = set }
end
local function line(y)
  local A = W.S.wild
  local parts = {}
  for _, k in ipairs(conts) do
    local wolves, packs, herds, animals, hc, pc = 0, 0, 0, 0, 0, 0
    for j = 1, A.np do if cont[A.p_cell[j]] == k then wolves = wolves + A.p_n[j]; packs = packs + 1; pc = pc + A.p_n[j] * A.p_cond[j] end end
    for j = 1, A.nh do if cont[A.h_cell[j]] == k then animals = animals + A.h_n[j]; herds = herds + 1; hc = hc + A.h_n[j] * A.h_cond[j] end end
    local people, forest, land = 0, 0, 0
    for _, v in ipairs(W.ent.villages) do if v.alive and cont[v.cell] == k then people = people + v.pop end end
    for c = 1, W.n do if cont[c] == k and W.geo.ocean[c] == 0 then land = land + 1; forest = forest + W.F.forest[c] end end
    parts[#parts + 1] = string.format("c%d %2dp %3dw (%.2f) %3dh %5.0fa (%.2f) %4d ppl f%.2f", k, packs, wolves, pc / math.max(1, wolves),
      herds, animals, hc / math.max(1, animals), people, forest / math.max(1, land))
  end
  local d = W.scratch.wl_dbg
  local flows = ""
  if d then
    flows = string.format(" || pups %d starved %d old %d other %d left %d lived %d joined %d founded %d paired %d | fawns %.0f starve %.0f pred %.0f hunt %.0f",
      d.pups, d.pstarve, d.pmort, d.pother, d.left, d.live, d.joined, d.founded, d.paired or 0, d.born, d.hstarve, d.pred, d.hunt or 0)
    for kk, x in pairs(d) do if type(x) == "number" then d[kk] = 0 end end
  end
  print(string.format("y%3d | %s%s", y, table.concat(parts, " | "), flows))
end
line(0)
for y = 1, years do
  world.run_years(W, 1)
  if y % every == 0 then line(y) end
end
