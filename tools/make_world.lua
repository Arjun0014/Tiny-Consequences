-- Pre-spin a world (02 §2): worldgen + spin-up, saved at Year 0 so the game starts instantly.
--   luajit tools/make_world.lua --seed 3 --out assets/worlds/default.tcsave
-- The game loads it with require("sim.worlds").from_save_string(str). Regenerate whenever the
-- sim changes in a save-incompatible or behaviour-changing way (the unit test
-- test_worlds.default_world_matches_fresh_generation fails when the file is stale).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { seed = 3, out = "assets/worlds/default.tcsave" }
local i = 1
while arg[i] do
  if arg[i] == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif arg[i] == "--out" then i = i + 1; args.out = arg[i] end
  i = i + 1
end
local worlds = require("sim.worlds")
local world = require("sim.world")
local version = require("sim.version")
local t0 = os.clock()
local W = worlds.generate(args.seed)
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
print(string.format("seed %d -> %s: %d bytes, %d villages, %d people, %d tankers (%.1fs); state hash %s",
  args.seed, args.out, #str, nv, pop, #W.ent.ships, os.clock() - t0, tostring(world.state_hash(W))))
