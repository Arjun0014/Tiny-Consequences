-- Grow a world under a raised population cap and watch what the size costs (W6 scale runs).
--   luajit tools/scale_run.lua <seed | world.tcsave> [years=120] [--cap N] [--villages N] [--out world.tcsave]
-- (a saved world continues where it was: grow it further under the same cap)
-- Prints a line a year: people, villages, households, the Lua heap (MB, after a full GC every
-- ten years), wall-clock ms per planet week (the JIT on: a rough guide; use tools/line_profile.lua
-- and tools/society_profile.lua on the saved world for the load-independent counts). Saves the
-- final world (a year-N save: `worlds.from_save_string`) and reports its size.
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local pos, cap, maxv, outp = {}, nil, nil, nil
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--cap" then cap = tonumber(arg[i + 1]); i = i + 1
  elseif a == "--villages" then maxv = tonumber(arg[i + 1]); i = i + 1
  elseif a == "--out" then outp = arg[i + 1]; i = i + 1
  else pos[#pos + 1] = a end
  i = i + 1
end
local from = pos[1] and pos[1]:match("%.tcsave$") and pos[1]
local seed, years = tonumber(pos[1]) or 22, tonumber(pos[2]) or 120
local config = require("sim.config")
if cap then config.SOCIETY.soft_cap = cap end
if maxv then config.SOCIETY.max_villages = maxv end
local world = require("sim.world")
local t0 = os.clock()
local W
if from then
  local f = assert(io.open(from, "rb")); local str = f:read("*a"); f:close()
  W = require("sim.worlds").from_save_string(str)
  seed = W.seed
else
  W = require("sim.worldgen.init").generate(seed)
end
print(string.format("seed %d, soft cap %d, max villages %d (worldgen %.1fs)", seed, config.SOCIETY.soft_cap,
  config.SOCIETY.max_villages, os.clock() - t0))
for y = 1, years do
  local t1 = os.clock()
  world.run_years(W, 1)
  local dt = os.clock() - t1
  local pop, nv, nh = 0, 0, 0
  for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop; nv = nv + 1; nh = nh + #v.households end end
  if y % 10 == 0 then collectgarbage("collect") end
  print(string.format("year %3d  people %5d  villages %3d  households %4d  heap %6.1f MB  %6.2f ms/week",
    y, pop, nv, nh, collectgarbage("count") / 1024, dt * 1000 / 48))
end
if outp then
  local str = require("sim.worlds").year0_save_string(W, { seed = seed, made_by = "tools/scale_run.lua" })
  local f = assert(io.open(outp, "wb")); f:write(str); f:close()
  print(string.format("saved %s: %d bytes", outp, #str))
end
