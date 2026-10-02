-- Animal movement audit (S15): are the animals of a herd, band or pack individuals, or a
-- synchronised blob? (the measures: tests/sim/support/animals_audit.lua)
--   luajit tools/v3_animals.lua [--load assets/worlds/v3.tcsave] [--from H] [--hours 48] [--dt 0.02]
--          [--lag 2] [--sett K] [--follow sett:herd:k | sett:wild:k | sett:dog:k]
-- --follow prints every change of the animal's action with its position, target, hunger and why
-- (herd = flock | cattle | swine).
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", hours = 48, dt = 0.02, lag = 2 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif a == "--hours" then i = i + 1; args.hours = tonumber(arg[i])
  elseif a == "--dt" then i = i + 1; args.dt = tonumber(arg[i])
  elseif a == "--lag" then i = i + 1; args.lag = tonumber(arg[i])
  elseif a == "--sett" then i = i + 1; args.sett = tonumber(arg[i])
  elseif a == "--follow" then i = i + 1; args.follow = arg[i]
  end
  i = i + 1
end
local clock = require("sim.agents.clock")
local audit = require("tests.sim.support.animals_audit")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local floor = math.floor
local fol = args.follow and { args.follow:match("^(%d+):(%a+):(%d+)$") } or nil
local last
local function on_sample(t, s, kind, a, x, y)
  if not (fol and tonumber(fol[1]) == s.id and fol[2] == kind and tonumber(fol[3]) == a.k) then return end
  local sig = (a.act or "?") .. "|" .. string.format("%.3f,%.3f", a.tx or 0, a.ty or 0) .. (a.why or "")
  if sig == last then return end
  last = sig
  local lh = clock.local_hour(t, W.grid.lon[s.cell])
  print(string.format("h %7.2f %02d:%02d %-6s %-6s at (%6.3f,%6.3f) -> (%6.3f,%6.3f) until %6.2f hunger %.2f | %s",
    t, floor(lh), floor(lh % 1 * 60), kind, a.act or "?", x, y, a.tx or 0, a.ty or 0, a.t1 or 0, a.hunger or 0, a.why or ""))
end
local rows = audit.run(W, { from = args.from or 23.55, hours = args.hours, dt = args.dt, lag = args.lag, sett = args.sett,
  on_sample = fol and on_sample or nil })
audit.print(rows)
print("")
audit.print_summary(audit.summary(rows))
