-- Follow the bird flocks of a v3 world (S14): every change of a flock's activity with the time,
-- local hour, where it is, its objective, target, hunger and why; and a day's tally of activities.
--   luajit tools/v3_birds.lua [--load assets/worlds/v3.tcsave] [--days 2] [--sett K] [--quiet]
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", days = 2 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--sett" then i = i + 1; args.sett = tonumber(arg[i])
  elseif a == "--quiet" then args.quiet = true
  end
  i = i + 1
end
local world = require("sim.world")
local clock = require("sim.agents.clock")
local birds = require("sim.agents.birds")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local V = W.S.v3
local t0 = W.hour
local last, tally = {}, {}
for t = t0, t0 + args.days * 24, 0.05 do
  world.advance(W, t)
  for _, s in ipairs(V.settlements) do
    if not args.sett or s.id == args.sett then
      birds.each(s, function(fl)
        local key = s.id * 100 + fl.k
        local x, y, z = birds.pos(fl, t)
        local tk = fl.act .. (fl.next and ("->" .. fl.next) or "")
        tally[s.name] = tally[s.name] or {}
        tally[s.name][fl.act] = (tally[s.name][fl.act] or 0) + 0.05
        local sig = tk .. "|" .. (fl.obj or "")
        if sig ~= last[key] then
          last[key] = sig
          if not args.quiet then
            local lh = clock.local_hour(t, W.grid.lon[s.cell])
            print(string.format("h %7.2f %02d:%02d %-14s %d gulls %-16s (%5.2f,%5.2f,%4.2f) hunger %.2f | %s | %s", t, math.floor(lh), math.floor(lh % 1 * 60),
              s.name, fl.n, tk, x, y, z, fl.hunger, fl.obj or "", fl.why or ""))
          end
        end
      end)
    end
  end
end
for name, tt in pairs(tally) do
  local parts = {}
  for k, v in pairs(tt) do parts[#parts + 1] = string.format("%s %.1f h", k, v) end
  table.sort(parts)
  print(name .. ": " .. table.concat(parts, ", "))
end
