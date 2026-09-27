-- Save/load determinism check: run a world, save it mid-year, restore the save, then step the live
-- and the restored world hour by hour and report the first hour and the exact state paths where
-- they diverge. Usage (repo root):
--   luajit tools/save_check.lua [--seed N] [--weeks W] [--hours H] [--max-diffs K]
--     --weeks  planet weeks to run before saving (default 58: a year and ten weeks)
--     --hours  hours to compare after the save (default 3000)
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local save = require("sim.save")

local args = { seed = 1, weeks = 58, hours = 3000, maxd = 25 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--weeks" then i = i + 1; args.weeks = tonumber(arg[i])
  elseif a == "--hours" then i = i + 1; args.hours = tonumber(arg[i])
  elseif a == "--max-diffs" then i = i + 1; args.maxd = tonumber(arg[i]) end
  i = i + 1
end

-- deep diff: collects up to `max` paths where a and b differ
local function diff(a, b, path, out, max, depth)
  if #out >= max then return end
  local ta, tb = type(a), type(b)
  if ta ~= tb then out[#out + 1] = path .. ": " .. tostring(a) .. " vs " .. tostring(b); return end
  if ta ~= "table" then
    if a ~= b and not (a ~= a and b ~= b) then out[#out + 1] = path .. ": " .. tostring(a) .. " vs " .. tostring(b) end
    return
  end
  if depth > 14 then return end
  local keys, seen = {}, {}
  for k in pairs(a) do keys[#keys + 1] = k; seen[k] = true end
  for k in pairs(b) do if not seen[k] then keys[#keys + 1] = k end end
  table.sort(keys, function(x, y)
    if type(x) == type(y) then return x < y end
    return type(x) == "number"
  end)
  for _, k in ipairs(keys) do
    diff(a[k], b[k], path .. "." .. tostring(k), out, max, depth + 1)
    if #out >= max then return end
  end
end

local t0 = os.clock()
local W = worldgen.generate(args.seed)
save.enable_capture(W)
print(string.format("generated seed %d in %.1fs", args.seed, os.clock() - t0))
world.run_ticks(W, args.weeks)
world.step(W); world.step(W); world.step(W)   -- save off the week boundary too
local s = save.save_world(W, { cmdlog = W.cmdlog })
local W2 = save.restore(assert(save.decode(s)))
print(string.format("saved at hour %d (%d bytes); restored hour %d", W.hour, #s, W2.hour))

local function compare(label)
  for _, k in ipairs(world.SAVE_KEYS) do
    if k ~= "cmdlog" and k ~= "pending" then
      local ha = world.state_hash(W, { k })
      local hb = world.state_hash(W2, { k })
      if ha ~= hb then
        local out = {}
        if k == "E" then diff({ rec = W.E.rec, next_id = W.E.next_id }, { rec = W2.E.rec, next_id = W2.E.next_id }, "E", out, args.maxd, 0)
        else diff(W[k], W2[k], k, out, args.maxd, 0) end
        print(string.format("%s: key %s differs", label, k))
        for j = 1, #out do print("   " .. out[j]) end
        return false
      end
    end
  end
  -- derived society caches that steer behaviour
  if W.vgeo and W2.vgeo then
    local out = {}
    for id, G in pairs(W.vgeo) do
      local G2 = W2.vgeo[id]
      if G2 then diff({ ring = G.ring, woods = G.woods }, { ring = G2.ring, woods = G2.woods }, "vgeo." .. id, out, args.maxd, 0) end
    end
    if #out > 0 then
      print(label .. ": derived vgeo differs")
      for j = 1, #out do print("   " .. out[j]) end
    end
  end
  return true
end

if not compare("at restore") then os.exit(1) end
-- compare once a simulated day (hashing the whole world every hour is slow)
for h = 1, args.hours do
  world.step(W)
  world.step(W2)
  if (h % 24 == 0 or h == args.hours) and world.state_hash(W) ~= world.state_hash(W2) then
    compare(string.format("hour %d (+%d after save, local %d)", W.hour, h, W.hour % 24))
    os.exit(1)
  end
end
print(string.format("identical for %d hours after the save (%.1fs)", args.hours, os.clock() - t0))
