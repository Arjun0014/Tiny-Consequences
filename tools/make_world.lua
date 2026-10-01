-- Pre-spin a world (02 §2): worldgen + spin-up, saved at Year 0 so the game starts instantly.
--   luajit tools/make_world.lua --seed 22 --out assets/worlds/default.tcsave  (D-047)
-- The game loads it with require("sim.worlds").from_save_string(str). Regenerate whenever the
-- sim changes in a save-incompatible or behaviour-changing way (the unit test
-- test_worlds.default_world_matches_fresh_generation fails when the file is stale).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { seed = 22, out = "assets/worlds/default.tcsave" }
local i = 1
while arg[i] do
  if arg[i] == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif arg[i] == "--out" then i = i + 1; args.out = arg[i]
  elseif arg[i] == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif arg[i] == "--v3" then args.v3 = true
  elseif arg[i] == "--settlements" then i = i + 1; args.settlements = tonumber(arg[i]) end
  i = i + 1
end
local worlds = require("sim.worlds")
local world = require("sim.world")
local version = require("sim.version")
local t0 = os.clock()
-- --v3: an agentic world (D-078): the planet with one settlement of agents
-- (R2: several small settlements, 15-30 founders each; the first keeps the full founding)
local W = args.v3 and require("sim.agents.worldgen").generate(args.seed, { settlements = args.settlements or 1,
  sizes = { 32, 24, 20, 18, 26, 22, 16, 28 } }) or worlds.generate(args.seed)
-- (dev: --years N saves the world N years on, for reviewing later worlds with ?world=NAME)
if args.years and args.years > 0 then world.run_years(W, args.years) end
local str = worlds.year0_save_string(W, { seed = args.seed, sim_version = version.SIM_VERSION,
  made_by = "tools/make_world.lua" })
local dir = args.out:match("^(.*)/[^/]*$")
if dir then
  if package.config:sub(1, 1) == "\\" then os.execute('mkdir "' .. dir:gsub("/", "\\") .. '" 2>nul')
  else os.execute('mkdir -p "' .. dir .. '"') end
end
local f = assert(io.open(args.out, "wb"))
f:write(str)
f:close()
local pop, nv = 0, 0
for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop; nv = nv + 1 end end
if W.v3 then for _, s in ipairs(W.S.v3.settlements) do nv = nv + 1; pop = pop + #s.members end end
print(string.format("seed %d -> %s: %d bytes, %d villages, %d people, %d tankers (%.1fs); state hash %s",
  args.seed, args.out, #str, nv, pop, #W.ent.ships, os.clock() - t0, tostring(world.state_hash(W))))
