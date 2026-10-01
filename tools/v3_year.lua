-- A whole v3 year, watched headless (roadmap v3 R1): every step of every person is logged, then
-- the year is searched for what would read as artificial when following people.
--   luajit tools/v3_year.lua [--seed 22] [--days 54] [--load FILE] [--out out/s10/year]
--                            [--follow N] [--follow-days 2,14,26,38,50] [--ids 3,12]
-- Writes <out>/summary.txt (printed too) and <out>/follow.txt (the followed people's days).
-- Checks: action budgets by stage and season; walking share of the waking day; the longest
-- trips; crowding at work spots; people standing still while awake (stuck); interrupted and
-- failed plans; routines identical to the day before; wake/bed spread and step starts per
-- minute (synchrony); food conservation (every FU accounted for).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local clock = require("sim.agents.clock")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local think = require("sim.agents.think")
local cfgmod = require("sim.config")

local args = { seed = 22, days = 54, out = "out/s10/year", nfollow = 6, fdays = { 2, 14, 26, 38, 50 }, ids = nil }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--out" then i = i + 1; args.out = arg[i]
  elseif a == "--follow" then i = i + 1; args.nfollow = tonumber(arg[i])
  elseif a == "--follow-days" then i = i + 1; args.fdays = {}; for x in arg[i]:gmatch("%d+") do args.fdays[#args.fdays + 1] = tonumber(x) end
  elseif a == "--ids" then i = i + 1; args.ids = {}; for x in arg[i]:gmatch("%d+") do args.ids[#args.ids + 1] = tonumber(x) end
  elseif a == "--settlements" then i = i + 1; args.settlements = tonumber(arg[i])
  elseif a == "--sett" then i = i + 1; args.sett = tonumber(arg[i])
  end
  i = i + 1
end
os.execute((package.config:sub(1, 1) == "\\" and "mkdir \"" .. args.out:gsub("/", "\\") .. "\" 2>nul") or ("mkdir -p " .. args.out))

io.stdout:setvbuf("no")
local out_lines = {}
local function say(fmt, ...)
  local l = select("#", ...) > 0 and string.format(fmt, ...) or fmt
  out_lines[#out_lines + 1] = l
  print(l)
end

local t0 = os.clock()
local W
if args.load then
  local f = assert(io.open(args.load, "rb"))
  local data = f:read("*a")
  f:close()
  W = assert(require("sim.worlds").from_save_string(data))
else
  W = require("sim.agents.worldgen").generate(args.seed, { settlements = args.settlements })
end
local V = W.S.v3
say("world seed %d ready in %.1f s; %d settlement(s)", args.seed, os.clock() - t0, #V.settlements)
local C = cfgmod.V3

-- Logging --------------------------------------------------------------------------------------
local log = {}          -- per person id: list of { t, act, objk, obj, dest, why, x, y, carry, inside }
local starts_min = {}
local walk_len = {}      -- trips: { len, objk, why, id, t }
local interrupts = {}    -- per person-day
local fails = 0
local lunches = 0
local orig_start = act.start
act.start = function(W_, s_, p, now)
  local ok = orig_start(W_, s_, p, now)
  local L = log[p.id]
  if not L then L = {}; log[p.id] = L end
  local last = L[#L]
  if last and last.lunch and not p.lunch and p.alive then lunches = lunches + 1 end
  L[#L + 1] = { t = now, act = p.act, objk = p.objk, obj = p.obj, dest = p.dest, why = p.why, x = p.x, y = p.y,
    carry = p.carry, camt = p.camt, inside = p.inside, lunch = p.lunch, sett = p.sett }
  local m = math.floor(now * 60)
  if now > 0.01 then starts_min[m] = (starts_min[m] or 0) + 1 end   -- (t = 0: the founding moment)
  if p.act == "walk" and p.mv then
    walk_len[#walk_len + 1] = { len = p.mv.len, objk = p.objk, why = p.why, id = p.id, t = now, dest = p.dest }
  end
  if p.why == "Cannot find a way there" then fails = fails + 1 end
  return ok
end
local agents = require("sim.agents.init")
local orig_int = agents.interrupt
agents.interrupt = function(W_, p, at, reason)
  local d = clock.day(at)
  local k = p.id * 1000 + d
  interrupts[k] = (interrupts[k] or 0) + 1
  return orig_int(W_, p, at, reason)
end

-- Food: everything edible anywhere in the settlement (FU): the sim's own ledger (settle.food_total)
local settle_m = require("sim.agents.settle")
local function food_total(s) return settle_m.food_total(W, s) end

-- Run --------------------------------------------------------------------------------------------
local s1 = V.settlements[args.sett or 1]
say("watching %s (settlement %d of %d; --sett K for another)", s1.name, s1.id, #V.settlements)
local lon, lat = W.grid.lon[s1.cell], W.grid.lat[s1.cell]
local day0 = clock.day(W.hour)
local snaps = {}   -- every 10 game minutes: positions of everyone outdoors
local crowd = {}   -- worst crowding instances
local still = {}   -- per person: { since, x, y }
local stuck = {}
local food_prev = food_total(s1)
local acc_prev = { flows = settle_m.food_flows(s1) }
local function sown_rows(s)
  local n = 0
  for k = 1, #s.plots do n = n + s.plots[k].sown end
  return n
end
local sown_prev = sown_rows(s1)
local daily = {}
local tsim = os.clock()
for d = 1, args.days do
  local ta = os.clock()
  local base = (day0 + d - 1) * 24
  for m = 1, 144 do
    local t = base + m / 6
    world.advance(W, t)
    -- crowding: people outdoors and not walking, within 0.02 of each other (gatherings excluded)
    local pts = {}
    for _, s in ipairs(V.settlements) do
      for k = 1, #s.members do
        local p = V.people[s.members[k]]
        if p and p.alive and p.inside == 0 and p.act ~= "carried" then
          local x, y = act.pos(p, t)
          if p.act ~= "walk" and p.act ~= "talk" and p.act ~= "play" and p.act ~= "sleep" then
            pts[#pts + 1] = { p = p, x = x, y = y, s = s.id }
          end
          -- stuck: awake, outdoors, not working, not moving for long
          local st = still[p.id]
          local idle = p.act == "wait" or p.act == "idle" or p.act == "rest" or p.act == "shelter"
          if st and idle and math.abs(st.x - x) < 0.004 and math.abs(st.y - y) < 0.004 then
            if t - st.since > 3 and not st.flag then
              st.flag = true
              stuck[#stuck + 1] = string.format("#%d %s: %s at (%.2f,%.2f) since %s d%d | %s", p.id, p.name, p.act, x, y,
                think.clock_str(clock.local_hour(st.since, lon)), clock.day(st.since), p.why)
            end
          else
            still[p.id] = { since = t, x = x, y = y }
          end
        else
          still[p.id] = nil
        end
      end
    end
    for a = 1, #pts do
      local n, who = 1, { pts[a].p.act }
      for b = 1, #pts do
        if b ~= a and pts[b].s == pts[a].s then
          local dx, dy = pts[b].x - pts[a].x, pts[b].y - pts[a].y
          if dx * dx + dy * dy < 0.0004 then n = n + 1; who[#who + 1] = pts[b].p.act end
        end
      end
      if n >= 3 then
        crowd[#crowd + 1] = { n = n, t = t, what = table.concat(who, ","), id = pts[a].p.id }
      end
    end
  end
  local ms = (os.clock() - ta) * 1000
  -- day summary
  local s = s1
  local alive = 0
  for k = 1, #s.members do if V.people[s.members[k]].alive then alive = alive + 1 end end
  local f = food_total(s)
  local sown = sown_rows(s)
  local dh = (s.acc.harvest or 0) - (acc_prev.h or 0)
  local df = (s.acc.foraged or 0) + (s.acc.fished or 0) + (s.acc.meat or 0) + (s.acc.milk or 0) - (acc_prev.f or 0)
  local de = (s.acc.eaten or 0) - (acc_prev.e or 0)
  local dsp = (s.acc.spoiled or 0) - (acc_prev.spoil or 0)
  acc_prev.spoil = s.acc.spoiled or 0
  local flows = settle_m.food_flows(s)
  local expect = food_prev + flows - acc_prev.flows
  local resid = f - expect
  acc_prev.flows = flows
  acc_prev.h, acc_prev.f, acc_prev.e = s.acc.harvest or 0, (s.acc.foraged or 0) + (s.acc.fished or 0) + (s.acc.meat or 0) + (s.acc.milk or 0), s.acc.eaten or 0
  sown_prev, food_prev = sown, f
  local ripe, growing, sheaves = 0, 0, 0
  for _, pl in ipairs(s.plots) do
    if pl.stage == "ripe" then ripe = ripe + 1 end
    if pl.stage == "growing" then growing = growing + 1 end
    sheaves = sheaves + pl.sheaves
  end
  local wx = require("sim.agents.weather").today(W, s, base + 12)
  local season = clock.local_season(base + 12, lat)
  daily[d] = { food = f, resid = resid }
  local tilled = 0
  for _, pl in ipairs(s.plots) do for r = 1, pl.rows do if pl.row_st[r] >= 1 then tilled = tilled + 1 end end end
  local hlogs = 0
  for k = 1, #s.blist do local b = s.buildings[s.blist[k]]; if b.kind == "house" then hlogs = hlogs + (b.stock.logs or 0) end end
  say("d%02d %-6s %4.1fC %s pop %d food %4.0f (gran %4.0f+%3.0f sh, fld %3.0f) harv +%3.0f forage +%4.1f (wild %3.0f) eat -%4.1f spoil -%3.1f resid %+.1f | wood %d +%.0f home, felled %d | hoed %d sown %d grow %d ripe %d plots %d %s %.0f ms",
    d - 1, season, wx.temp, wx.wet and (wx.heavy[1] and "R!" or "r ") or "  ", alive, f, s.granary.stock.grain or 0,
    s.granary.stock.sheaves or 0, sheaves, dh, df, s.wild or 0, de, dsp, resid, s.wood_store.stock.logs or 0, hlogs, s.acc.felled or 0,
    tilled, sown, growing, ripe, #s.plots, #s.sites > 0 and ("sites " .. #s.sites) or "", ms)
end
say("lunches eaten (not in acc.eaten): %d (= %.1f FU)", lunches, lunches * C.meal_fu)
for _, s in ipairs(V.settlements) do
  local alive = 0
  for k = 1, #s.members do local p = V.people[s.members[k]]; if p.alive then alive = alive + 1 end end
  local a = s.acc
  say("  %-12s lat %5.1f  pop %3d  food %5.0f  harvest %5.0f  forage %4.0f  fish %4.0f  eaten %5.0f  felled %4d  built %d  plots %2d  households %d",
    s.name, math.deg(W.grid.lat[s.cell]), alive, food_total(s), a.harvest or 0, a.foraged or 0, a.fished or 0, a.eaten or 0,
    a.felled or 0, a.built or 0, #s.plots, #s.households)
  do
    -- textiles (D-088): the linen pipeline, garments made, clothes worn
    local fib, yarn, cloth, flax = 0, 0, 0, s.granary and (s.granary.stock.flax or 0) or 0
    for k = 1, #s.blist do
      local b = s.buildings[s.blist[k]]
      if b.kind == "house" then fib, yarn, cloth = fib + (b.stock.fibre or 0), yarn + (b.stock.yarn or 0), cloth + (b.stock.cloth or 0) end
    end
    local cs, cn, cmin, rags = 0, 0, 1, 0
    for k = 1, #s.members do
      local p = V.people[s.members[k]]
      if p.alive and p.act ~= "carried" then
        cs, cn = cs + (p.clothes or 1), cn + 1
        if (p.clothes or 1) < cmin then cmin = p.clothes or 1 end
        if (p.clothes or 1) < 0.2 then rags = rags + 1 end
      end
    end
    local fl = 0
    for _, pl in ipairs(s.plots) do if pl.crop == "flax" then fl = fl + 1 end end
    say("      linen: flax fields %d, pulled %.0f, granary flax %.1f, fibre %.1f, yarn %.1f, cloth %.1f | spun %.1f woven %.1f garments %d | clothes mean %.2f min %.2f, in rags %d",
      fl, a.flax or 0, flax, fib, yarn, cloth, a.spun or 0, a.woven or 0, a.garments or 0, cn > 0 and cs / cn or 0, cmin, rags)
  end
  if s.herds then
    local stock = require("sim.agents.stock")
    local per, hung, sick = {}, {}, 0
    stock.each(s, function(H, an)
      per[an.sp] = (per[an.sp] or 0) + 1
      hung[an.sp] = (hung[an.sp] or 0) + an.hunger
      if (an.sick or 0) > 0 then sick = sick + 1 end
    end)
    local parts = {}
    for _, sp in ipairs({ "sheep", "goat", "cattle", "pig" }) do
      if per[sp] then parts[#parts + 1] = string.format("%s %d (born %d, died %d, killed %d, hunger %.2f)", sp, per[sp], a["born_" .. sp] or 0,
        a["died_" .. sp] or 0, a["killed_" .. sp] or 0, hung[sp] / per[sp]) end
    end
    local manure = 0
    for _, kind in ipairs(stock.HERDS) do if s.herds[kind] then manure = manure + (s.herds[kind].manure or 0) end end
    say("      herds: %s | sick %d; meat %.0f FU, milk %.0f, cheese %.0f, wool %.1f, hay %.0f (mown %.0f), dung in pens %.0f, mucked %.0f, slops %.1f",
      table.concat(parts, ", "), sick, a.meat or 0, a.milk or 0, a.cheese or 0, a.wool or 0, s.hay or 0, a.hay or 0, manure, a.mucked or 0, a.slops or 0)
  end
  if s.boats then
    local bl = {}
    for _, b in ipairs(s.boats) do bl[#bl + 1] = string.format("#%d owner %d cond %.2f trips %d%s", b.id, b.owner or 0, b.cond or 1, b.trips or 0, b.lost and " LOST" or "") end
    say("      fish: smoked %.0f, fresh spoiled %.0f, boats mended %d, built %d | %s", a.smoked or 0, a.fish_spoiled or 0,
      a.boats_mended or 0, a.boats_built or 0, table.concat(bl, "; "))
  end
end
say("sim %.1f s for %d days (%.0f ms/day)", os.clock() - tsim, args.days, (os.clock() - tsim) * 1000 / args.days)

-- Analysis -------------------------------------------------------------------------------------
local function season_of(t) return clock.local_season(t, lat) end
-- per stage x season: hours by objective (awake) and walking share
local budget = {}
local walkshare = {}
local person_days = {}
for _, id in ipairs(V.order) do
  local L = log[id]
  local p = V.people[id]
  if L and p.sett == s1.id then
    for k = 1, #L do
      local e = L[k]
      local t1 = (L[k + 1] and L[k + 1].t) or (W.hour)
      local dur = t1 - e.t
      local st = people.stage(p, e.t)
      local se = season_of(e.t)
      local key = st .. "/" .. se
      local B = budget[key]
      if not B then B = { _tot = 0, _walk = 0 }; budget[key] = B end
      if e.act ~= "sleep" and e.act ~= "carried" then
        local ok = e.objk or e.act
        B[ok] = (B[ok] or 0) + dur
        B._tot = B._tot + dur
        if e.act == "walk" then B._walk = B._walk + dur end
        local pd = id * 1000 + clock.day(e.t)
        local P = person_days[pd]
        if not P then P = { awake = 0, walk = 0, seq = {}, id = id, day = clock.day(e.t) }; person_days[pd] = P end
        P.awake = P.awake + dur
        if e.act == "walk" then P.walk = P.walk + dur end
        if e.objk and P.seq[#P.seq] ~= e.objk then P.seq[#P.seq + 1] = e.objk end
      end
    end
  end
end
say("\n== Waking hours by stage and season (share of awake time; walking share)")
local order = { "child", "apprentice", "adult", "elder" }
for _, st in ipairs(order) do
  for _, se in ipairs(clock.SEASONS) do
    local B = budget[st .. "/" .. se]
    if B and B._tot > 0 then
      local keys = {}
      for k, v in pairs(B) do if k:sub(1, 1) ~= "_" then keys[#keys + 1] = k end end
      table.sort(keys, function(a, b) return B[a] > B[b] end)
      local parts = {}
      for j = 1, math.min(14, #keys) do parts[#parts + 1] = string.format("%s %.0f%%", keys[j], 100 * B[keys[j]] / B._tot) end
      say("  %-10s %-6s walk %2.0f%% | %s", st, se, 100 * B._walk / B._tot, table.concat(parts, ", "))
    end
  end
end
-- walking share per person-day
local hi_walk, n_pd, same, n_cmp = {}, 0, 0, 0
for pd, P in pairs(person_days) do
  n_pd = n_pd + 1
  if P.awake > 6 and P.walk / P.awake > 0.45 then hi_walk[#hi_walk + 1] = P end
  local prev = person_days[pd - 1]
  if prev and #P.seq >= 3 then
    n_cmp = n_cmp + 1
    if table.concat(prev.seq, ",") == table.concat(P.seq, ",") then same = same + 1 end
  end
end
table.sort(hi_walk, function(a, b) return a.walk / a.awake > b.walk / b.awake end)
say("\n== Walking: %d of %d person-days walk > 45%% of the waking day", #hi_walk, n_pd)
for j = 1, math.min(8, #hi_walk) do
  local P = hi_walk[j]
  local p = V.people[P.id]
  say("  #%d %s (%s, %s) day %d: %.0f%% of %.1f h | %s", P.id, p.name, people.stage(p, P.day * 24), p.role, P.day,
    100 * P.walk / P.awake, P.awake, table.concat(P.seq, " > "))
end
say("\n== Routines: %d of %d person-days repeat the previous day's sequence of objectives exactly", same, n_cmp)
-- longest trips by objective
table.sort(walk_len, function(a, b) return a.len > b.len end)
local by_obj = {}
for _, w in ipairs(walk_len) do
  local o = w.objk or "?"
  local B = by_obj[o]
  if not B then B = { n = 0, sum = 0, max = 0 }; by_obj[o] = B end
  B.n, B.sum = B.n + 1, B.sum + w.len
  if w.len > B.max then B.max = w.len end
end
local ok = {}
for k in pairs(by_obj) do ok[#ok + 1] = k end
table.sort(ok, function(a, b) return by_obj[a].sum > by_obj[b].sum end)
say("\n== Trips (walk legs) by objective: count, mean length, max (world units; the map is 3.2 across, walk 3.2/h)")
for _, k in ipairs(ok) do
  local B = by_obj[k]
  say("  %-12s %5d  mean %.2f  max %.2f  total %.0f", k, B.n, B.sum / B.n, B.max, B.sum)
end
say("  longest legs:")
for j = 1, math.min(10, #walk_len) do
  local w = walk_len[j]
  say("    %.2f #%d %s d%d -> %s | %s", w.len, w.id, w.objk or "?", clock.day(w.t), w.dest or "", w.why or "")
end
-- crowding
table.sort(crowd, function(a, b) return a.n > b.n end)
local seen = {}
say("\n== Crowding: %d samples with 3+ people working within 0.02 of each other", #crowd)
local shown = 0
for _, c in ipairs(crowd) do
  local k = c.what .. math.floor(c.t)
  if not seen[k] and shown < 8 then
    seen[k] = true
    shown = shown + 1
    say("  %d at %s d%d (#%d): %s", c.n, think.clock_str(clock.local_hour(c.t, lon)), clock.day(c.t), c.id, c.what)
  end
end
say("\n== Stuck (awake, outdoors, idle in one spot > 3 h): %d", #stuck)
for j = 1, math.min(10, #stuck) do say("  " .. stuck[j]) end
local nint, maxint, maxk = 0, 0, 0
for k, n in pairs(interrupts) do nint = nint + n; if n > maxint then maxint, maxk = n, k end end
say("\n== Interrupts: %d (most for one person-day: %d, #%d day %d); failed plans (no way there): %d", nint, maxint,
  math.floor(maxk / 1000), maxk % 1000, fails)
-- synchrony
local maxm, maxn = 0, 0
for m, n in pairs(starts_min) do if n > maxn then maxm, maxn = m, n end end
say("== Synchrony: most step starts in one minute: %d (%s d%d)", maxn, think.clock_str(clock.local_hour(maxm / 60, lon)), clock.day(maxm / 60))
-- wake / bed spread per season (from the log: first non-sleep start after a sleep; last sleep start)
local wakes, beds = {}, {}
for _, id in ipairs(V.order) do
  local L = log[id]
  local p = V.people[id]
  if L and p.sett == s1.id then
    for k = 2, #L do
      local a, b = L[k - 1], L[k]
      if a.act == "sleep" and b.act ~= "sleep" then
        local se = season_of(b.t)
        wakes[se] = wakes[se] or {}
        table.insert(wakes[se], clock.local_hour(b.t, lon))
      end
      if b.act == "sleep" and a.act ~= "sleep" and b.objk == "sleep" then
        local lh = clock.local_hour(b.t, lon)
        if lh > 12 then
          local se = season_of(b.t)
          beds[se] = beds[se] or {}
          table.insert(beds[se], lh)
        end
      end
    end
  end
end
for _, se in ipairs(clock.SEASONS) do
  local w, b = wakes[se] or {}, beds[se] or {}
  table.sort(w); table.sort(b)
  if #w > 0 and #b > 0 then
    say("  %-6s wake %s / %s / %s   bed %s / %s / %s  (10th pct / median / 90th)", se,
      think.clock_str(w[math.floor(#w * 0.1) + 1]), think.clock_str(w[math.floor(#w * 0.5) + 1]), think.clock_str(w[math.floor(#w * 0.9) + 1]),
      think.clock_str(b[math.floor(#b * 0.1) + 1]), think.clock_str(b[math.floor(#b * 0.5) + 1]), think.clock_str(b[math.floor(#b * 0.9) + 1]))
  end
end
-- food residuals
local worst, wd = 0, 0
for d, D in ipairs(daily) do if math.abs(D.resid) > math.abs(worst) then worst, wd = D.resid, d - 1 end end
say("== Food conservation: worst daily residual %+.2f FU (day %d); lunches account for -%.2f FU a lunch", worst, wd, C.meal_fu)

-- deaths, births
local s = s1
say("== Year: births %d, deaths %d, built %d, felled %d, harvest %.0f FU, foraged %.0f, eaten %.0f; plots %d; people %d",
  s.acc.births or 0, s.acc.deaths or 0, s.acc.built or 0, s.acc.felled or 0, s.acc.harvest or 0, s.acc.foraged or 0,
  s.acc.eaten or 0, #s.plots, #s.members)
for _, id in ipairs(V.order) do
  local p = V.people[id]
  if not p.alive then say("  died: #%d %s (%s) day %d of %s", p.id, p.name, people.stage(p, p.died), clock.day(p.died), p.died_of) end
end

local f = io.open(args.out .. "/summary.txt", "w")
f:write(table.concat(out_lines, "\n"), "\n")
f:close()

-- Followed people ------------------------------------------------------------------------------
local ids = args.ids
if not ids then
  ids = {}
  -- a spread of stages, picked deterministically: everyone in id order, every k-th
  local cand = {}
  for _, id in ipairs(V.order) do if V.people[id].sett == 1 then cand[#cand + 1] = id end end
  local step = math.max(1, math.floor(#cand / args.nfollow))
  for j = 1, #cand, step do ids[#ids + 1] = cand[j] end
end
local fo = io.open(args.out .. "/follow.txt", "w")
for _, id in ipairs(ids) do
  local p = V.people[id]
  local L = log[id] or {}
  for _, fd in ipairs(args.fdays) do
    local d = day0 + fd
    local st, age = people.stage(p, d * 24 + 12)
    fo:write(string.format("\n### #%d %s %s, %s %s %.0f, %s, household %d, day %d (%s)\n", p.id, p.name, p.surname, p.sex, st,
      age, p.role, p.hh, fd, season_of(d * 24 + 12)))
    for k = 1, #L do
      local e = L[k]
      if clock.day(e.t) == d then
        fo:write(string.format("  %s %-8s %-30s -> %-18s | %s%s\n", think.clock_str(clock.local_hour(e.t, lon)), e.act,
          e.obj or "", e.dest or "", e.why or "", e.carry ~= "" and string.format(" [%s %.1f]", e.carry, e.camt or 0) or ""))
      end
    end
  end
end
fo:close()
print("follow: " .. args.out .. "/follow.txt (" .. #ids .. " people)")
