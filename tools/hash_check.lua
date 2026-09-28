-- Run a saved world (or a seed) for N weeks and print the state hash every K weeks: a refactor
-- that is meant to change no behaviour (an optimisation) must print the same hashes before and
-- after. Also prints the wall-clock time (LuaJIT, the JIT on) for a rough speed comparison.
--   luajit tools/hash_check.lua <world.tcsave | seed> [weeks=24] [every=8]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local W
if arg[1] and arg[1]:match("%.tcsave$") then
  local f = assert(io.open(arg[1], "rb")); local str = f:read("*a"); f:close()
  W = require("sim.worlds").from_save_string(str)
else
  W = require("sim.worldgen.init").generate(tonumber(arg[1]) or 22)
end
local weeks = tonumber(arg[2]) or 24
local every = tonumber(arg[3]) or 8
local t0 = os.clock()
for wk = 1, weeks do
  world.tick(W)
  if wk % every == 0 or wk == weeks then
    local pop = 0
    for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop end end
    print(string.format("week %4d  hash %s  people %d  events %d", wk, tostring(world.state_hash(W)), pop, W.E.next_id - 1))
  end
end
print(string.format("%.1f s", os.clock() - t0))
