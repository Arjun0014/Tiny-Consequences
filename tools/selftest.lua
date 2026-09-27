-- W06 reference: the determinism self-test under LuaJIT (sim/selftest.lua), printed as JSON so
-- the browser test can compare the HTML5 runtime's hashes with it.
--   luajit tools/selftest.lua            -> {"default":{...},"seed5":{...}}
package.path = "./?.lua;./?/init.lua;" .. package.path
local selftest = require("sim.selftest")
local json = require("tools.json")
local f = assert(io.open("assets/worlds/default.tcsave", "rb"))
local save = f:read("*a")
f:close()
local out = {
  default = selftest.run({ save = save, years = 2 }),
  seed5 = selftest.run({ seed = 5, years = 1 }),
}
print(json.encode(out))
