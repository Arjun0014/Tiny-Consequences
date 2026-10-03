-- Watch flood water on a village's map (S21, R6.5e): hour by hour the river's stage over its banks,
-- the tiles under water, the houses and fields in it, who noticed and what they did, beasts drowned;
-- an ASCII map at the flood's peak (~ water over the land, = the river, H a house, # another
-- building, f a field, . dry land, space sea).
--   luajit tools/v3_flood.lua [--load assets/worlds/v3.tcsave] [--premise flood|floodN] [--sid N]
--          [--hours 192] [--every 6] [--map]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave", premise = "flood", hours = 192, every = 6, sid = nil, map = false }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--premise" then i = i + 1; args.premise = arg[i]
  elseif a == "--sid" then i = i + 1; args.sid = tonumber(arg[i])
  elseif a == "--hours" then i = i + 1; args.hours = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i])
  elseif a == "--map" then args.map = true end
  i = i + 1
end
io.stdout:setvbuf("no")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local world = require("sim.world")
local mapm = require("sim.agents.map")
local premise = require("sim.agents.premise")
local V = W.S.v3
if args.premise ~= "none" then assert(premise.named(W, args.premise), "no premise " .. args.premise) end
local sid = args.sid or tonumber(args.premise:sub(6)) or 6
local s = V.settlements[sid]
print(string.format("premise %s, watching %s (cell %d)", args.premise, s.name, s.cell))

local function snapshot()
  local fd = mapm.FLOOD[s]
  local n = 0
  if fd then for k = 1, s.N * s.N do if fd[k] > 0 then n = n + 1 end end end
  local st = 0
  for _, v in ipairs(s.fl and s.fl.st or {}) do if v > st then st = v end end
  local wet_b, wet_h = 0, {}
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b and b.wet then wet_b = wet_b + 1; wet_h[#wet_h + 1] = string.format("%s%.2f", b.kind:sub(1, 3), b.wet) end
  end
  local fields = 0
  for _, pl in ipairs(s.plots) do if (pl.wet_h or 0) > 0 then fields = fields + 1 end end
  local al, fle, loo = 0, 0, 0
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive then
      if p.alarm and p.alarm.kind == "flood" then al = al + 1 end
      if p.objk == "flee_water" then fle = fle + 1 end
      if p.objk == "loose_flood" then loo = loo + 1 end
    end
  end
  return n, st, wet_b, table.concat(wet_h, " "), fields, al, fle, loo
end

local function draw()
  local fd = mapm.FLOOD[s]
  local N = s.N
  local rows = {}
  for iy = N, 1, -2 do
    local r = {}
    for ix = 1, N, 1 do
      local k = (iy - 1) * N + ix
      local ch = "."
      if s.land[k] == 0 then ch = " "
      elseif s.riv[k] > 0 then ch = "="
      elseif fd and fd[k] > 0 then ch = "~"
      elseif s.occ[k] > 0 then
        local b = s.buildings[s.occ[k]]
        ch = (b and (b.kind == "house" or b.kind == "hut")) and "H" or "#"
      elseif s.plot[k] > 0 then ch = "f" end
      r[#r + 1] = ch
    end
    rows[#rows + 1] = table.concat(r)
  end
  print(table.concat(rows, "\n"))
end

local peak, peak_t = -1, 0
local acc0 = {}
for k, v in pairs(s.acc) do acc0[k] = v end
for h = 1, args.hours do
  world.advance(W, W.hour + 1)
  local n, st, wb, wh, fields, al, fle, loo = snapshot()
  if n > peak then peak, peak_t = n, h end
  if h % args.every == 0 or (n > 0 and h % 2 == 0 and h < 72) then
    print(string.format("h%3d  stage %.2f m  tiles %4d  wet bld %2d  fields %2d  alarmed %2d  fleeing %d  pens %d   %s", h, st, n, wb, fields, al, fle, loo, wh))
  end
  if h == peak_t and n > 0 and s.fl then
    -- (the buildings' ground above the water at the peak)
    local flood = require("sim.agents.flood")
    local parts = {}
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b and b.state == "standing" and (b.kind == "house" or b.kind == "hut" or b.kind == "granary" or b.kind == "fold" or b.kind == "byre" or b.kind == "sty") then
        local dep, margin = flood.at(W, s, b.cx, b.cy)
        parts[#parts + 1] = string.format("%s:%.1f", b.kind:sub(1, 3), margin)
      end
    end
    PEAKB = table.concat(parts, " ")
  end
end
print("ground above the water at the peak (m): " .. (PEAKB or ""))
print(string.format("peak %d tiles at hour %d", peak, peak_t))
local keys = {}
for k in pairs(s.acc) do keys[#keys + 1] = k end
table.sort(keys)
for _, k in ipairs(keys) do
  if (k:find("flood") or k:find("drown") or k:find("fell") or k:find("loosed") or k:find("swims") or k:find("stock_died")) and s.acc[k] ~= acc0[k] then
    print(string.format("  %s %s -> %s", k, tostring(acc0[k] or 0), tostring(s.acc[k])))
  end
end
-- memories of the flood
local n = 0
for _, id in ipairs(s.members) do
  local p = V.people[id]
  for _, m in ipairs(p and p.mem or {}) do
    local txt = type(m) == "table" and (m.text or m[2]) or m
    if type(txt) == "string" and (txt:find("flood") or txt:find("river") or txt:find("water")) and n < 30 then
      n = n + 1
      print("  mem " .. p.name .. ": " .. txt)
    end
  end
end
if args.map then draw() end
