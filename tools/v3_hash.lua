-- Prove a refactor leaves a v3 world identical (D-086, D-090 practice): load a world, advance it
-- day by day and print the full state hash each day (world.state_hash: everything saved, people,
-- animals, trees, stores, plans).
--   luajit tools/v3_hash.lua [--load assets/worlds/v3.tcsave] [--days 8] [--every 24]
-- Run before and after a change; the lines must match.
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", days = 8, every = 24 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i])
  end
  i = i + 1
end
local world = require("sim.world")
local f = assert(io.open(args.load, "rb"))
local data = f:read("*a")
f:close()
local W = assert(require("sim.worlds").from_save_string(data))
local t0 = W.hour
local c0 = os.clock()
for h = args.every, args.days * 24, args.every do
  world.advance(W, t0 + h)
  local V = W.S.v3
  local alive, sheep = 0, 0
  for _, p in pairs(V.people) do if p.alive then alive = alive + 1 end end
  for _, s in ipairs(V.settlements) do
    if s.flock then for _, a in ipairs(s.flock.sheep) do if a.alive then sheep = sheep + 1 end end end
  end
  print(string.format("hour %5d  hash %10s  people %d  sheep %d", h, tostring(world.state_hash(W)), alive, sheep))
end
print(string.format("(%.1f s)", os.clock() - c0))
