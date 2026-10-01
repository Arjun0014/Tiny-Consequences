-- v3 save round trip: a fresh world vs the same world saved and restored, run side by side.
--   luajit tools/v3_save_check.lua [--seed 22] [--days 2]
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local worlds = require("sim.worlds")
local args = { seed = 22, days = 2 }
for i = 1, #arg do
  if arg[i] == "--seed" then args.seed = tonumber(arg[i + 1]) end
  if arg[i] == "--days" then args.days = tonumber(arg[i + 1]) end
end
local A = require("sim.agents.worldgen").generate(args.seed, {})
local str = worlds.year0_save_string(A, { seed = args.seed })
print("save bytes", #str)
local B = assert(worlds.from_save_string(str))
world.set_systems(require("sim.agents.worldgen").systems())
print("hash fresh", world.state_hash(A), "restored", world.state_hash(B))
for d = 1, args.days do
  world.advance(A, d * 24)
  world.advance(B, d * 24)
  print("day", d, world.state_hash(A), world.state_hash(B))
end
