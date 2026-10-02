-- luajit tools/mathcheck.lua [n]: sim/mathcheck.lua's values under LuaJIT, one line per function
-- (scripts/mathcheck.mjs compares them with the browser's).
package.path = "./?.lua;" .. package.path
local r = require("sim.mathcheck").run(tonumber(arg[1] or 20000))
for _, f in ipairs(r) do io.write(f.name, " ", table.concat(f.vals, " "), "\n") end
