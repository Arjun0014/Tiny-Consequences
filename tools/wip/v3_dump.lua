-- Dump a v3 world's state at full precision (diff a fresh world against a restored one).
--   luajit tools/wip/v3_dump.lua fresh|load HOURS
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local worlds = require("sim.worlds")
local mode = arg[1]
local W
if mode == "fresh" then
  W = require("sim.agents.worldgen").generate(22, {})
  local f = io.open("out/s9/v3_22.tcsave", "wb"); f:write(worlds.year0_save_string(W, { seed = 22 })); f:close()
else
  local f = io.open("out/s9/v3_22.tcsave", "rb"); local s = f:read("*a"); f:close()
  W = assert(worlds.from_save_string(s))
end
if arg[2] then world.advance(W, tonumber(arg[2])) end
local out = {}
local function dump(prefix, t, depth)
  if depth > 7 then return end
  local nk, sk = world.sorted_keys(t)
  for _, k in ipairs(nk) do
    local v = t[k]
    if type(v) == "table" then dump(prefix .. "[" .. k .. "]", v, depth + 1)
    else out[#out + 1] = prefix .. "[" .. k .. "]=" .. (type(v) == "number" and string.format("%.17g", v) or tostring(v)) end
  end
  for _, k in ipairs(sk) do
    local v = t[k]
    if type(v) == "table" then dump(prefix .. "." .. k, v, depth + 1)
    else out[#out + 1] = prefix .. "." .. k .. "=" .. (type(v) == "number" and string.format("%.17g", v) or tostring(v)) end
  end
end
for _, k in ipairs(world.SAVE_KEYS) do
  if k ~= "cmdlog" and k ~= "pending" and k ~= "E" and type(W[k]) == "table" then dump(k, W[k], 0)
  elseif type(W[k]) ~= "table" then out[#out + 1] = k .. "=" .. tostring(W[k]) end
end
local f = io.open("out/s9/dump_" .. mode .. ".txt", "w"); f:write(table.concat(out, "\n")); f:close()
print(#out)
