-- Where and when to look (S14): scan a v3 world headless every `step` hours and list moments worth
-- watching in the browser - sea voyages, boats moored at another village, traders dealing door to
-- door, oxen at the plough, dogs barking at wolves, packs at the pens, floods, people driving
-- wolves off - with the absolute hour, settlement and person, so `scripts/shots_look.mjs --at H
-- --sett K [--pid P]` can show the same moment (the browser lives the same world, D-110).
--   luajit tools/v3_watch.lua [--load assets/worlds/v3.tcsave] [--days 12] [--step 0.5] [--max 6]
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", days = 12, step = 0.5, max = 6 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--step" then i = i + 1; args.step = tonumber(arg[i])
  elseif a == "--max" then i = i + 1; args.max = tonumber(arg[i])
  elseif a == "--from" then i = i + 1; args.from = tonumber(arg[i])
  end
  i = i + 1
end
local world = require("sim.world")
local wild = require("sim.agents.wild")
local clock = require("sim.agents.clock")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
if args.from and args.from > W.hour then world.advance(W, args.from) end
local V = W.S.v3
local seen, count, last = {}, {}, {}
local function note(cat, t, s, pid, text)
  count[cat] = (count[cat] or 0) + 1
  -- (one line a category and settlement every 6 hours at most)
  local key = cat .. "|" .. s.id .. "|" .. (pid or 0)
  if last[key] and t - last[key] < 6 then return end
  last[key] = t
  seen[cat] = seen[cat] or {}
  if #seen[cat] < args.max then
    local lh = clock.local_hour(t, W.grid.lon[s.cell])
    seen[cat][#seen[cat] + 1] = string.format("  h %8.2f (day %d, %02d:%02d local) %-20s #%-4s %s", t, math.floor(t / 24),
      math.floor(lh), math.floor((lh % 1) * 60), s.name, pid and tostring(pid) or "-", text or "")
  end
end
local t0 = args.from or W.hour
local PERSON = { voyage = "sea", travel = "road", deal = "deal", mind_boat = "mind_boat", drive_off = "drive_off",
  guard = "guard", ask = "ask", yoke = "ox", hunt = "hunt" }
for t = t0 + args.step, t0 + args.days * 24, args.step do
  world.advance(W, t)
  for _, id in ipairs(V.order) do
    local p = V.people[id]
    if p and p.alive then
      local s = V.settlements[p.at or p.sett]
      local cat = PERSON[p.act]
      if cat then
        local tr = p.trip
        note(cat, t, s, p.id, string.format("%s %s %s", p.name, p.obj or "", tr and tr.to and ("-> " .. V.settlements[tr.to].name) or ""))
      end
      if p.act == "till" and p.ox and p.ox ~= 0 then note("ox_plough", t, s, p.id, p.name .. " ploughing with the ox") end
      if p.at and p.at ~= p.sett then note("visiting", t, s, p.id, string.format("%s of %s, %s (%s)", p.name, V.settlements[p.sett].name, p.act, p.obj or "")) end
    end
  end
  for _, s in ipairs(V.settlements) do
    for bi, b in ipairs(s.boats or {}) do
      if b.away and not b.lost then note("boat_away", t, V.settlements[b.away], nil, string.format("boat %d of %s moored here", bi, s.name)) end
    end
    for _, d in ipairs(s.dogs or {}) do
      if d.alive and d.act == "bark" then note("dog_bark", t, s, nil, "a dog barking") end
    end
    local nw, nearpen = 0, 0
    wild.each(s, function(w)
      if w.pred and w.alive then
        nw = nw + 1
        for _, kind in ipairs({ "flock", "cattle", "swine" }) do
          local H = s.herds and s.herds[kind]
          local pen = H and s.buildings[H.pen]
          if pen and (w.tx - pen.cx) ^ 2 + (w.ty - pen.cy) ^ 2 < 0.09 then nearpen = nearpen + 1 end
        end
      end
    end)
    if nw > 0 then note("wolves_on_map", t, s, nil, nw .. " wolves") end
    if nearpen > 0 then note("wolves_at_pen", t, s, nil, nearpen .. " wolves near a pen") end
    if (s.flood or 0) > 1.35 then note("flood", t, s, nil, string.format("river at %.2f x its usual width", s.flood)) end
  end
end
local cats = {}
for k in pairs(count) do cats[#cats + 1] = k end
table.sort(cats)
for _, k in ipairs(cats) do
  print(string.format("%s: %d samples", k, count[k]))
  for _, l in ipairs(seen[k] or {}) do print(l) end
end
