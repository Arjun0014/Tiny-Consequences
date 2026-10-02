-- Follow people step by step over a stretch of time (S14 watching): every change of their action,
-- objective, destination or map, with the time, local hour, where they are, what they carry and why.
--   luajit tools/v3_follow.lua --pid 52[,53] [--from H] [--to H] [--load assets/worlds/v3.tcsave] [--dt 0.05]
--          [--premise crowd:2] (S19: the same premises as tools/v3_chronicle.lua; --from-day D: D days after the start)
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", pids = {}, dt = 0.05, premise = {} }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--pid" then i = i + 1; for x in arg[i]:gmatch("%d+") do args.pids[#args.pids + 1] = tonumber(x) end
  elseif a == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif a == "--to" then i = i + 1; args.to = tonumber(arg[i])
  elseif a == "--dt" then i = i + 1; args.dt = tonumber(arg[i])
  elseif a == "--premise" then i = i + 1; args.premise[#args.premise + 1] = arg[i]
  elseif a == "--from-day" then i = i + 1; args.from_day = tonumber(arg[i])
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  end
  i = i + 1
end
local world = require("sim.world")
local clock = require("sim.agents.clock")
local act = require("sim.agents.act")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local V = W.S.v3
for _, pr in ipairs(args.premise) do
  local name, sid = pr:match("^([%w_]+):?(%d*)$")
  local s = V.settlements[tonumber(sid) or 1]
  local premise = require("sim.agents.premise")
  if name == "crowd" then premise.crowd(W, s, 12, 40) else premise[name](W, s) end
end
local from = args.from or (args.from_day and W.hour + args.from_day * 24) or W.hour
if from > W.hour then world.advance(W, from) end
local to = args.to or (from + 24 * (args.days or 1))
local last = {}
local function pack_str(p)
  local t = {}
  if p.carry and p.carry ~= "" then t[#t + 1] = string.format("carry %s %.1f", p.carry, p.camt or 0) end
  if p.pack then
    for _, g in ipairs(require("sim.agents.econ").ORDER) do
      if (p.pack[g] or 0) > 0 then t[#t + 1] = string.format("%s %.1f", g, p.pack[g]) end
    end
  end
  return table.concat(t, ", ")
end
for t = from, to, args.dt do
  world.advance(W, t)
  for _, pid in ipairs(args.pids) do
    local p = V.people[pid]
    if p then
      local s = V.settlements[p.at or p.sett]
      local key = table.concat({ tostring(p.alive), p.act or "", p.obj or "", p.dest or "", tostring(p.at or p.sett) }, "|")
      if key ~= last[pid] then
        last[pid] = key
        local lh = clock.local_hour(t, W.grid.lon[s.cell])
        local x, y = act.pos(p, t)
        print(string.format("h %8.2f %02d:%02d #%d %-9s @%-16s (%5.2f,%5.2f) %-10s | %s -> %s | %s | %s", t, math.floor(lh), math.floor(lh % 1 * 60),
          pid, p.name, s.name, x, y, p.act or "", p.obj or "", p.dest or "", pack_str(p), p.why or ""))
      end
    end
  end
end
