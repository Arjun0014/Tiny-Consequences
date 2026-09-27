-- Scout seeds for a pre-spun world (D-027): how lively are the first years of each world?
-- npm run sim:scout -- --from 1 --to 20 [--years 60]
-- Per seed: villages (fishing, on tanker lanes), land shares, then over the first `years`:
-- headline count and kinds, first wildfire year, villages founded/abandoned, famines,
-- droughts, floods, population range.
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local worlds = require("sim.worlds")
local world = require("sim.world")
local metrics = require("sim.metrics")

local args = { from = 1, to = 10, years = 60 }
local i = 1
while arg[i] do
  if arg[i] == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif arg[i] == "--to" then i = i + 1; args.to = tonumber(arg[i])
  elseif arg[i] == "--years" then i = i + 1; args.years = tonumber(arg[i]) end
  i = i + 1
end

for seed = args.from, args.to do
  local W = worlds.generate(seed)
  local vs = W.ent.villages
  local nfish, lane_fish = 0, 0
  local lane_cells = {}
  for _, l in ipairs(W.static.lanes or {}) do for _, c in ipairs(l.cells) do lane_cells[c] = true end end
  local pop0 = 0
  for _, v in ipairs(vs) do
    pop0 = pop0 + v.pop
    if #v.fishing > 0 then
      nfish = nfish + 1
      local near = false
      for _, c in ipairs(W.vgeo[v.id].fishing) do if lane_cells[c] then near = true end end
      if near then lane_fish = lane_fish + 1 end
    end
  end
  local m0 = metrics.compute(W)
  local kinds, nh, first_wild = {}, 0, -1
  local pmin, pmax = math.huge, 0
  local e0 = W.E.next_id
  for y = 1, args.years do
    for _ = 1, 48 do world.tick(W) end
    local p = 0
    for _, v in ipairs(W.ent.villages) do if v.alive then p = p + v.pop end end
    if p < pmin then pmin = p end
    if p > pmax then pmax = p end
  end
  local counts = {}
  for id = e0, W.E.next_id - 1 do
    local r = W.E.rec[id]
    if r then
      counts[r.kind] = (counts[r.kind] or 0) + 1
      if r.headline then
        nh = nh + 1
        kinds[r.kind] = (kinds[r.kind] or 0) + 1
        if r.kind == "wildfire" and first_wild < 0 then first_wild = math.floor(r.tick / 48) end
      end
    end
  end
  local nk = 0
  local parts = {}
  local names = {}
  for k in pairs(kinds) do names[#names + 1] = k end
  table.sort(names)
  for _, k in ipairs(names) do nk = nk + 1; parts[#parts + 1] = k .. ":" .. kinds[k] end
  print(string.format("seed %2d | vil %d fish %d lane-fish %d pop %d | forest %.2f desert %.2f | %dy: H %d (%d kinds) wild@%d fam %d found %d aband %d drought %d flood %d pop %d-%d | %s",
    seed, #vs, nfish, lane_fish, pop0, m0.forest_nat or 0, m0.desert_nat or 0, args.years, nh, nk, first_wild,
    counts.famine or 0, counts.village_founded or 0, counts.village_abandoned or 0, counts.drought or 0,
    counts.flood or 0, pmin, pmax, table.concat(parts, " ")))
end
