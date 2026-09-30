-- Big wildfires (the S01 kind: a connected burn of wildfire_cells) per 25 years, and the fires
-- started, on the untouched world at the full profile's soft cap (session 8):
--   luajit tools/wip/firecount.lua <seed> [years=200] [cap=2400]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local seed, years, cap = tonumber(arg[1]) or 3, tonumber(arg[2]) or 200, tonumber(arg[3]) or 2400
require("sim.config").SOCIETY.soft_cap = cap
local world = require("sim.world")
local W = require("tests.sim.support.worlds").fresh(seed)
local e0 = W.E.next_id
local wild, starts, sizes = {}, {}, {}
for y = 1, years do
  world.run_years(W, 1)
  for id = e0, W.E.next_id - 1 do
    local r = W.E.rec[id]
    if r then
      local b = math.floor((y - 1) / 25) + 1
      if r.kind == "wildfire" then wild[b] = (wild[b] or 0) + 1; sizes[#sizes + 1] = string.format("y%d:%d", y, r.mag) end
      if r.kind == "fire_started" then starts[b] = (starts[b] or 0) + 1 end
    end
  end
  e0 = W.E.next_id
end
local a, s = {}, {}
for b = 1, math.ceil(years / 25) do a[#a + 1] = tostring(wild[b] or 0); s[#s + 1] = tostring(starts[b] or 0) end
print(string.format("seed %d: wildfires per 25 y [%s], fires started [%s]; %s", seed, table.concat(a, " "), table.concat(s, " "), table.concat(sizes, " ")))
