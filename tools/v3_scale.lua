-- v3 cost by population: N settlements of ~32 agents, D days, ms per game day (LuaJIT; run
-- with `luajit -joff` for the interpreter, closer to the browser's Lua).
--   luajit tools/v3_scale.lua [--settlements 6] [--days 3] [--seed 22]
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { settlements = 6, days = 3, seed = 22 }
for i = 1, #arg do
  if arg[i] == "--settlements" then args.settlements = tonumber(arg[i + 1]) end
  if arg[i] == "--days" then args.days = tonumber(arg[i + 1]) end
  if arg[i] == "--seed" then args.seed = tonumber(arg[i + 1]) end
end
local world = require("sim.world")
local nav = require("sim.agents.nav")
local W = require("sim.agents.worldgen").generate(args.seed, { settlements = args.settlements })
local V = W.S.v3
local n = 0
for _, s in ipairs(V.settlements) do n = n + #s.members end
-- warm-up: the first day (all decide at once at founding)
world.advance(W, 24)
local t0 = os.clock()
local e0 = nav.stats.expanded
world.advance(W, 24 * (1 + args.days))
local dt = os.clock() - t0
local alive = 0
for _, s in ipairs(V.settlements) do for _, id in ipairs(s.members) do if V.people[id].alive then alive = alive + 1 end end end
print(string.format("%d settlements, %d people (%d alive): %.1f ms per game day (%.2f ms per agent-day), A* expansions/day %d, jit %s",
  #V.settlements, n, alive, dt * 1000 / args.days, dt * 1000 / args.days / math.max(1, alive),
  (nav.stats.expanded - e0) / args.days, tostring(rawget(_G, "jit") and jit.status()))) -- lint:allow luajit-only
