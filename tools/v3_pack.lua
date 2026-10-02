-- A pack (or band, or a herd's beasts) frame by frame (S16 animal audit): every member's action,
-- position and why at each change, so that moves begun together can be traced to their cause.
--   luajit tools/v3_pack.lua [--load W] [--from H] [--hours 48] [--dt 0.02] --sett K --gid G
--          [--herd flock|cattle|swine | --dogs] [--max 400]
-- --gid picks a wild group (pack or band) by its macro id; --herd a village herd instead.
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", from = 23.55, hours = 48, dt = 0.02, max = 400 }
local i = 1
while arg[i] do
  local a, v = arg[i], arg[i + 1]
  if a == "--load" then args.load = v
  elseif a == "--from" then args.from = tonumber(v)
  elseif a == "--hours" then args.hours = tonumber(v)
  elseif a == "--dt" then args.dt = tonumber(v)
  elseif a == "--sett" then args.sett = tonumber(v)
  elseif a == "--gid" then args.gid = tonumber(v)
  elseif a == "--herd" then args.herd = v
  elseif a == "--dogs" then args.dogs = true; i = i - 1
  elseif a == "--max" then args.max = tonumber(v)
  else i = i - 1 end
  i = i + 2
end
local world = require("sim.world")
local wild = require("sim.agents.wild")
local stock = require("sim.agents.stock")
local clock = require("sim.agents.clock")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local V = W.S.v3
if args.from > W.hour then world.advance(W, args.from) end
local s = V.settlements[args.sett]
local last, lines = {}, 0
for t = args.from + args.dt, args.from + args.hours, args.dt do
  world.advance(W, t)
  local L = {}
  if args.dogs then
    local dogs = require("sim.agents.dogs")
    for _, d in ipairs(s.dogs or {}) do if d.alive then local x, y = dogs.pos(d, t); L[#L + 1] = { d, "d" .. d.k, x, y } end end
  elseif args.herd then
    for _, a in ipairs(s.herds[args.herd].animals) do if a.alive then L[#L + 1] = { a, "b" .. a.k, stock.pos(a, t) } end end
  else
    for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
      if w.alive and w.gid == args.gid then
        local x, y = wild.pos(w, t)
        L[#L + 1] = { w, (w.lead == 0 and "L" or "w") .. w.k, x, y }
      end
    end
  end
  local changed = {}
  for _, e in ipairs(L) do
    local a = e[1]
    local sig = (a.act or "?") .. "|" .. (a.why or "") .. "|" .. string.format("%.3f", a.t0 or 0)
    if last[e[2]] ~= sig then
      last[e[2]] = sig
      changed[#changed + 1] = string.format("  %-5s %-6s (%6.3f,%6.3f) -> (%6.3f,%6.3f) %s", e[2], a.act or "?", e[3], e[4], a.tx or 0, a.ty or 0, a.why or "")
    end
  end
  if #changed > 0 then
    local lh = clock.local_hour(t, W.grid.lon[s.cell])
    print(string.format("h %7.2f (%05.2f) %d of %d changed", t, lh, #changed, #L))
    for _, c in ipairs(changed) do print(c) end
    lines = lines + #changed + 1
    if lines > args.max then break end
  end
end
