-- Draw each v3 settlement's local map to a PNG (diagnostics, D-085): sea (dark blue; the boats'
-- open water lighter), rivers (blue, fords light blue), floodplain (olive), plots (brown), buildings
-- (red), standing trees (green), stumps/felled (grey), lanes and trails (tan), the dock (yellow),
-- people now (white dots).
--   luajit tools/v3_map.lua [--load assets/worlds/v3.tcsave | --seed 22 --settlements 6]
--                           [--days D] [--out out/v3map] [--scale 4]
package.path = "./?.lua;./?/init.lua;" .. package.path
local png = require("tools.png")

local args = { out = "out/v3map", scale = 4, days = 0, seed = 22 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--settlements" then i = i + 1; args.settlements = tonumber(arg[i])
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--out" then i = i + 1; args.out = arg[i]
  elseif a == "--scale" then i = i + 1; args.scale = tonumber(arg[i])
  end
  i = i + 1
end
os.execute((package.config:sub(1, 1) == "\\" and "mkdir \"" .. args.out:gsub("/", "\\") .. "\" 2>nul") or ("mkdir -p " .. args.out))

local W
if args.load then
  local f = assert(io.open(args.load, "rb"))
  local data = f:read("*a")
  f:close()
  W = assert(require("sim.worlds").from_save_string(data))
else
  W = require("sim.agents.worldgen").generate(args.seed, { settlements = args.settlements })
end
if args.days > 0 then
  local world = require("sim.world")
  world.advance(W, W.hour + args.days * 24)
end
local mapm = require("sim.agents.map")
local V = W.S.v3
for _, s in ipairs(V.settlements) do
  local N, sc = s.N, args.scale
  local tr = s.trees
  local ppl = {}
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive and p.inside == 0 then
      local x, y = require("sim.agents.act").pos(p, W.hour)
      local t = mapm.tile_of(s, x, y)
      if t > 0 then ppl[t] = true end
    end
  end
  local wet, ford, flood, sea, rows = 0, 0, 0, 0, 0
  for t = 1, N * N do
    local r = s.riv[t]
    if r > 0 then wet = wet + 1; if r <= 101 then ford = ford + 1 end elseif r < 0 then flood = flood + 1 end
    if s.land[t] == 0 then sea = sea + 1 end
  end
  local path = string.format("%s/%d_%s.png", args.out, s.id, s.name:gsub("%s", "_"))
  png.write(path, N * sc, N * sc, function(px, py)
    local ix = math.floor(px / sc) + 1
    local iy = N - math.floor(py / sc)          -- north up
    local t = (iy - 1) * N + ix
    if ppl[t] then return 255, 255, 255 end
    if s.land[t] == 0 then
      if s.wcost[t] > 0 then return 40, 80, 150 end
      return 25, 55, 110
    end
    local o = s.occ[t]
    if o > 0 then
      local b = s.buildings[o]
      if b and b.kind == "dock" then return 240, 210, 40 end
      if b and b.state == "site" then return 230, 140, 120 end
      return 190, 50, 40
    end
    local r = s.riv[t]
    if r > 0 then
      if r <= 101 then return 120, 190, 235 end
      return 50, 110, 210
    end
    local k = s.tree[t]
    if k > 0 then
      if tr.st[k] == 1 then return 30, math.floor(90 + 60 * tr.size[k]), 30 end
      if tr.st[k] >= 2 then return 120, 110, 100 end
    end
    if s.plot[t] > 0 then return 150, 115, 60 end
    if s.plot[t] < 0 then return 175, 150, 95 end
    if s.lane[t] == 2 then return 215, 190, 140 end
    if s.lane[t] == 1 then return 200, 190, 150 end
    if r < 0 then return 140, 150, 80 end
    return 120, 165, 80
  end)
  print(string.format("%s (cell %d): sea %d tiles, river %d wet (%d wadeable), floodplain %d, dock %s -> %s",
    s.name, s.cell, sea, wet, ford, flood, s.dock and "yes" or "no", path))
end
