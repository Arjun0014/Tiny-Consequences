-- Landmasses and the society's reach over them: continents (size, villages, coast), villages with
-- moorings and boats, and over a run the parties whose route crosses the sea (and any that could
-- find no route). Usage (repo root): luajit tools/geo_report.lua [--seed N] [--years Y]
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
local worlds = require("sim.worlds")
local world = require("sim.world")

local args = { seed = 22, years = 40 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i]) end
  i = i + 1
end

local W = worlds.generate(args.seed)
local function landmasses()
  local geo = W.geo
  local by = {}
  for c = 1, W.n do
    local k = geo.continent[c]
    if k > 0 then
      local e = by[k]
      if not e then e = { cells = 0, coast = 0, vil = 0, pop = 0, moor = 0 }; by[k] = e end
      e.cells = e.cells + 1
      if geo.coast and geo.coast[c] == 1 then e.coast = e.coast + 1 end
    end
  end
  for _, v in ipairs(W.ent.villages) do
    if v.alive then
      local e = by[v.continent]
      if e then
        e.vil = e.vil + 1; e.pop = e.pop + v.pop
        if v.mooring then e.moor = e.moor + 1 end
      end
    end
  end
  local ks = {}
  for k in pairs(by) do ks[#ks + 1] = k end
  table.sort(ks)
  for _, k in ipairs(ks) do
    local e = by[k]
    if e.cells >= 3 or e.vil > 0 then
      print(string.format("  landmass %2d: %4d cells, %3d coast, %2d villages (%d with moorings), %d people", k, e.cells, e.coast, e.vil, e.moor, e.pop))
    end
  end
end
print(string.format("seed %d: %d cells, %d land", args.seed, W.n, W.geo.n_land))
landmasses()

local mob = require("sim.society.mobility")
local kinds, sea, none = {}, {}, {}
local dep0 = mob.depart
mob.depart = function(W_, v, members, to, site, kind, reason, food, seed, cargo, from_cell)
  local party = dep0(W_, v, members, to, site, kind, reason, food, seed, cargo, from_cell)
  if party then
    kinds[kind] = (kinds[kind] or 0) + 1
    if party.si then sea[kind] = (sea[kind] or 0) + 1 end
    if party.no_route then none[kind] = (none[kind] or 0) + 1 end
  end
  return party
end
-- (the society's modules hold their own references to depart: rewire them)
for _, m in ipairs({ "relations", "status", "conflict" }) do require("sim.society." .. m).depart = mob.depart end
require("sim.society.mobility").depart = mob.depart

for y = 1, args.years do
  world.run_years(W, 1)
  if y % 10 == 0 or y == args.years then
    local ks = {}
    for k in pairs(kinds) do ks[#ks + 1] = k end
    table.sort(ks)
    local parts = {}
    for _, k in ipairs(ks) do parts[#parts + 1] = string.format("%s %d (sea %d, no route %d)", k, kinds[k], sea[k] or 0, none[k] or 0) end
    print(string.format("y%d parties: %s", y, table.concat(parts, ", ")))
  end
end
landmasses()
