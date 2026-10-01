-- v3 agent world, headless (docs/v3/10_AGENTS.md §10).
--   luajit tools/v3_run.lua [--seed 22] [--days 3] [--follow 5,12] [--all] [--quiet] [--load FILE] [--save FILE]
-- Prints: the settlement at founding; per-day summary (wake times spread, actions by hour,
-- food, wood, plots, buildings, births/deaths); each followed person's timeline (every step:
-- time, action, objective, destination, why). Timing: ms per game day.
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local clock = require("sim.agents.clock")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local think = require("sim.agents.think")
local nav = require("sim.agents.nav")

local args = { seed = 22, days = 3, follow = {}, spinup = nil }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--follow" then i = i + 1; for x in arg[i]:gmatch("%d+") do args.follow[#args.follow + 1] = tonumber(x) end
  elseif a == "--all" then args.all = true
  elseif a == "--quiet" then args.quiet = true
  elseif a == "--spinup" then i = i + 1; args.spinup = tonumber(arg[i])
  elseif a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--save" then i = i + 1; args.save = arg[i]
  elseif a == "--hist" then args.hist = true
  elseif a == "--step" then i = i + 1; args.step = tonumber(arg[i])
  end
  i = i + 1
end

io.stdout:setvbuf("no")
local t0 = os.clock()
local W
if args.load then
  local worlds = require("sim.worlds")
  local f = assert(io.open(args.load, "rb"))
  local data = f:read("*a")
  f:close()
  W = assert(worlds.from_save_string(data))
else
  W = require("sim.agents.worldgen").generate(args.seed, { spinup_years = args.spinup })
end
print(string.format("world seed %d ready in %.1f s", args.seed, os.clock() - t0))
if args.save then
  local save = require("sim.save")
  local s = save.to_string and save.to_string(W) or nil
  if s then local f = io.open(args.save, "wb"); f:write(s); f:close(); print("saved " .. args.save) end
end

local V = W.S.v3
local s = V.settlements[1]
local lon = W.grid.lon[s.cell]
print(string.format("%s at cell %d (lat %.1f, lon %.1f): %d people, %d households, %d buildings, %d plots, %d trees",
  s.name, s.cell, math.deg(W.grid.lat[s.cell]), math.deg(lon), #s.members, #s.households, #s.blist, #s.plots, s.trees.n))
for k = 1, #s.households do
  local hh = V.households[s.households[k]]
  local line = {}
  for j = 1, #hh.members do
    local p = V.people[hh.members[j]]
    local st, a = people.stage(p, W.hour)
    line[#line + 1] = string.format("#%d %s %s %d%s%s", p.id, p.name, p.sex, math.floor(a), p.role ~= "" and (" " .. p.role) or "",
      p.partner ~= 0 and (" ∞#" .. p.partner) or "")
  end
  print("  house " .. hh.id .. ": " .. table.concat(line, ", "))
end

local follow = {}
for _, id in ipairs(args.follow) do follow[id] = {} end
if args.all then for _, id in ipairs(V.order) do follow[id] = {} end end

local function hm(t)
  local lh = clock.local_hour(t, lon)
  return think.clock_str(lh)
end

-- hook: record each person's step starts by wrapping act.start
local orig_start = act.start
local starts_by_minute = {}
local wake_times = {}
act.start = function(W_, s_, p, now)
  local ok = orig_start(W_, s_, p, now)
  local f = follow[p.id]
  local st = p.plan[p.pi]
  if f then
    f[#f + 1] = string.format("%s d%d %-8s %-28s -> %-16s | %s%s", hm(now), clock.day(now), p.act, p.obj, p.dest,
      p.why, p.carry ~= "" and string.format(" [carrying %s %.1f]", p.carry, p.camt) or "")
  end
  local m = math.floor(now * 60)
  starts_by_minute[m] = (starts_by_minute[m] or 0) + 1
  if p.act ~= "sleep" and p.was_asleep then
    local d = clock.day(now)
    wake_times[d] = wake_times[d] or {}
    table.insert(wake_times[d], clock.local_hour(now, lon))
  end
  p.was_asleep = p.act == "sleep"
  return ok
end

local agents = require("sim.agents.init")
local step = args.step or 0.37
local stats_t = os.clock()
local day0 = clock.day(W.hour)
local hours_ms = {}
for d = 1, args.days do
  local ta = os.clock()
  local target = (day0 + d) * 24
  -- advance in odd-sized slices (as a host would), proving slicing does not matter
  local cur = (day0 + d - 1) * 24
  local hist = {}
  while cur < target do
    cur = math.min(target, cur + step)
    world.advance(W, cur)
    local hr = math.floor(clock.local_hour(cur, lon))
    if not hist[hr] then
      local h = {}
      for _, id in ipairs(s.members) do
        local p = V.people[id]
        if p.alive then
          local a = p.act
          if a == "walk" then a = (p.carry ~= "" and "carry" or "walk") end
          if p.inside ~= 0 and a ~= "walk" then a = "in:" .. a end
          h[a] = (h[a] or 0) + 1
        end
      end
      hist[hr] = h
    end
  end
  if args.hist then
    for hr = 0, 23 do
      local h = hist[hr]
      if h then
        local keys = {}
        for k in pairs(h) do keys[#keys + 1] = k end
        table.sort(keys, function(a, b) return h[a] > h[b] or (h[a] == h[b] and a < b) end)
        local parts = {}
        for _, k in ipairs(keys) do parts[#parts + 1] = k .. " " .. h[k] end
        print(string.format("   %02d  %s", hr, table.concat(parts, ", ")))
      end
    end
  end
  local ms = (os.clock() - ta) * 1000
  -- day summary
  local acts = {}
  local alive = 0
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.alive then alive = alive + 1; acts[p.act] = (acts[p.act] or 0) + 1 end
  end
  local sown, ripe, growing, sheaves = 0, 0, 0, 0
  for _, pl in ipairs(s.plots) do
    sown = sown + pl.sown
    if pl.stage == "ripe" then ripe = ripe + 1 end
    if pl.stage == "growing" then growing = growing + 1 end
    sheaves = sheaves + pl.sheaves
  end
  local felled, stumps = 0, 0
  for k = 1, s.trees.n do
    if s.trees.st[k] == 2 then felled = felled + 1 elseif s.trees.st[k] == 3 then stumps = stumps + 1 end
  end
  local wk = wake_times[day0 + d - 1] or {}
  table.sort(wk)
  local lanes, trails = 0, 0
  for ti = 1, s.N * s.N do if s.lane[ti] == 2 then lanes = lanes + 1 elseif s.lane[ti] == 1 then trails = trails + 1 end end
  local sites = {}
  for _, bid in ipairs(s.sites) do
    local b = s.buildings[bid]
    sites[#sites + 1] = string.format("%s logs %d/%d work %.0f/%d", b.label, b.logs_have, b.logs_need, b.work_done, b.work_need)
  end
  print(string.format("== %s  (%.0f ms)  people %d  granary %.0f FU (%.0f days)  woodstore %d logs  rain %s  temp %.1f",
    clock.label(target - 1), ms, alive, s.granary.stock.grain, s.need.food_days, s.wood_store.stock.logs,
    W.S.v3.settlements[1].wx and (W.S.v3.settlements[1].wx.wet and "yes" or "no") or "?", s.wx and s.wx.temp or 0))
  print(string.format("   rows sown %d, growing %d, ripe %d, sheaves %.0f; trees felled %d stumps %d; trails %d lanes %d; sites: %s",
    sown, growing, ripe, sheaves, felled, stumps, trails, lanes, #sites > 0 and table.concat(sites, "; ") or "none"))
  if #wk > 0 then
    print(string.format("   woke: first %s, median %s, last %s (%d wakings)", think.clock_str(wk[1]), think.clock_str(wk[math.floor(#wk / 2) + 1]),
      think.clock_str(wk[#wk]), #wk))
  end
  print(string.format("   acc: harvest %.0f, eaten %.1f, felled %d, built %d, births %d, deaths %d",
    s.acc.harvest or 0, s.acc.eaten or 0, s.acc.felled or 0, s.acc.built or 0, s.acc.births or 0, s.acc.deaths or 0))
end
-- synchronisation: the most step starts in a single minute
local maxm, maxn = 0, 0
for m, n in pairs(starts_by_minute) do if n > maxn then maxm, maxn = m, n end end
print(string.format("max step starts in one minute: %d (at %s)", maxn, hm(maxm / 60)))
print(string.format("nav: %d searches, %d expanded (%.0f per search), %d cache hits, %d fallbacks",
  nav.stats.searches, nav.stats.expanded, nav.stats.expanded / math.max(1, nav.stats.searches), nav.stats.cache_hits, nav.stats.fallbacks))
print(string.format("total sim time %.2f s for %d days", os.clock() - stats_t, args.days))
for id, lines in pairs(follow) do
  local p = V.people[id]
  if p and not args.quiet then
    local st, a = people.stage(p, W.hour)
    print(string.format("\n### #%d %s %s, %s, %d, %s, household %d%s", p.id, p.name, p.surname, p.sex, math.floor(a), p.role, p.hh,
      p.partner ~= 0 and (", partner #" .. p.partner) or ""))
    for _, l in ipairs(lines) do print("  " .. l) end
  end
end
