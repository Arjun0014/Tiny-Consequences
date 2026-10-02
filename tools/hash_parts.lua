-- luajit tools/hash_parts.lua HOUR [key ...]: world.hash_parts of the shipped v3 world advanced
-- to absolute hour HOUR, at the given path, as JSON (scripts/hash_drill.mjs compares it with the browser's).
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local hour = tonumber(arg[1] or 0)
if hour > W.hour then world.advance(W, hour) end
local path = {}
for i = 2, #arg do path[#path + 1] = arg[i] end
local parts = world.hash_parts(W, path)
local out = {}
for _, x in ipairs(parts) do
  out[#out + 1] = string.format('{"k":%q,"h":%q,"t":%q}', x.k, x.h, x.t)
end
io.write("[" .. table.concat(out, ",") .. "]")
