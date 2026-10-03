-- Animals and fire (S22 diagnostic): a fire set beside a herd at pasture, a wild band or a herd shut in
-- its pen, on a dry day; every member watched - its distance to the fire's footprint, how soon it reacts,
-- its heading as it runs, whether it comes back into danger, deaths, how far and how long it is displaced
-- (the measures: tests/sim/support/fire_audit.lua).
--   luajit tools/v3_fire_animals.lua [--load F] [--sett K] [--herd flock|cattle|swine|wild|pen:flock] [--fire N]
--          [--off 0.15] [--ang A] [--dry 0.15] [--hours 6] [--dt 0.02] [--day D] [--near 0.15] [--danger 0.07]
--          [--safe 0.3] [--trace k]   (--trace: every change of member k's action and why)
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local opts = { load = "assets/worlds/v3.tcsave" }
local i = 1
while arg[i] do
  local k = arg[i]:match("^%-%-(.+)$")
  if k and arg[i + 1] then
    i = i + 1
    opts[k] = tonumber(arg[i]) or arg[i]
  end
  i = i + 1
end
local f = assert(io.open(opts.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
opts.print = print
local audit = require("tests.sim.support.fire_audit")
local R = assert(audit.run(W, opts))
audit.print(R)
