-- Livestock as individual animals (roadmap v3 R3; D-089 sheep, D-092 herds). A village keeps up to
-- three herds, each of individual animals owned by households:
--   flock  - sheep and goats together in the fold (one herder takes the small stock out),
--   cattle - cows, their calves and a bull in the byre yard,
--   swine  - sows, their litters and a boar in the sty, rooting at the woods' edge.
-- Each animal is a persistent micro-agent with its own position and action (graze or root, a few
-- steps to fresh ground, lie down to rest or chew the cud, walk, stand in the pen), decided when its
-- last action ends, in the agents' own event queue (strict time order with the people). A herd has
-- no single destination: by day each animal keeps to its own place around the herder who drove it
-- out (or the ground the herder left it on) - a place in a spread that grows with the herd (a
-- sunflower spiral of `sep` spacing, its order reshuffled every day: neighbours keep their distance
-- without anyone looking for them) - and wanders about it; the young keep to their mothers; at night
-- the pen. People do everything else: drive them out and home, milk the cows and goats, shear,
-- mow and rack the hay, carry the pens' dung to the fields, bring the kitchen's scraps to the pigs,
-- kill the beasts the hay cannot winter.
--
-- s.herds = { flock = H, cattle = H, swine = H }; s.pasture = { grass = {}, open = {} } (16 x 16
-- blocks of 8 x 8 tiles, shared by every grazer); s.hay (the stack by the fold, sheep-day units).
-- H = { kind, animals = { a }, pen (building id), herder (person id), out, spot_x/y, rack (feed in
--   the rack or trough), manure, sick }
-- a = { k, sp, sex, born, owner (household), mother, coat, x, y, tx, ty, t0, t1, act ("graze" |
--   "walk" | "rest" | "pen" | "held"), ver, hunger, health, sick (days left), wool, milk, lact (hour
--   lactation ends), slot, alive, bred (year), cull, held (person id) }
-- Events: id = ID0 + settlement id x 100000 + herd index x 20000 + k (people have smaller ids).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local nav = require("sim.agents.nav")
local clock = require("sim.agents.clock")

local M = {}
M.ID0 = 100000000
M.HERDS = { "flock", "cattle", "swine" }
M.HIDX = { flock = 0, cattle = 1, swine = 2 }
M.HERD_OF = { sheep = "flock", goat = "flock", cattle = "cattle", pig = "swine" }
M.PEN = { flock = "fold", cattle = "byre", swine = "sty" }
M.NAMES = { flock = "the sheep and goats", cattle = "the cattle", swine = "the pigs" }
local floor, max, min, sqrt, abs, cos, sin = math.floor, math.max, math.min, math.sqrt, math.abs, math.cos, math.sin
local function C() return cfgmod.V3 end
local function SP(sp) return cfgmod.V3.stock[sp] end
M.SP = SP

-- A person's position at a time (set by sim/agents/init.lua: act.pos; bob forbids the circular
-- require).
M.person_pos = function(p, t) return p.x, p.y end

function M.event_id(s, H, a) return M.ID0 + s.id * 100000 + M.HIDX[H.kind] * 20000 + a.k end
function M.decode(id)
  local r = id - M.ID0
  local sid = floor(r / 100000)
  r = r - sid * 100000
  local hi = floor(r / 20000)
  return sid, M.HERDS[hi + 1] or (hi == 4 and "dog" or "wild"), r - hi * 20000
end

-- Position of animal a at time t (a straight walk between t0 and t1, else where it stands).
function M.pos(a, t)
  if a.act ~= "walk" or a.t1 <= a.t0 then return a.tx, a.ty end
  local f = (t - a.t0) / (a.t1 - a.t0)
  if f <= 0 then return a.x, a.y elseif f >= 1 then return a.tx, a.ty end
  return a.x + (a.tx - a.x) * f, a.y + (a.ty - a.y) * f
end

function M.age(a, t) return (t - a.born) / 1152 end
function M.young(a, t) return M.age(a, t) < SP(a.sp).young_years end

-- An animal's draws: one hash per (animal, day) kept on the animal, then a single mixing round
-- per draw keyed by the minute of the day (the browser's Lua pays dearly for every hash, D-089,
-- D-092)
local function u(W, s, H, a, now, k)
  local day = floor(now / 24)
  if a.rd ~= day then
    a.rd = day
    a.rb = rng.hash(W.seed, rng.S.V3_MAP, s.key * 7 + M.HIDX[H.kind] * 20000 + a.k, day % 4294967296, 500)
  end
  return rng.fbase(a.rb, (floor(now * 60) % 1440) * 32 + k)
end
local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }
local GOLD = 2.39996323          -- the golden angle: the sunflower spiral of places in a herd

-- Pasture blocks -----------------------------------------------------------------------------
local BS = 8                         -- tiles a block side
M.BS = BS
local function block_of(s, x, y)
  local N = s.N
  local bn = N / BS
  local bx = floor((x / s.T + N / 2) / BS)
  local by = floor((y / s.T + N / 2) / BS)
  if bx < 0 or by < 0 or bx >= bn or by >= bn then return 0 end
  return by * bn + bx + 1
end
M.block_of = block_of

function M.block_xy(s, b)
  local bn = s.N / BS
  local by = floor((b - 1) / bn)
  local bx = (b - 1) - by * bn
  return mapm.ixy_xy(s, bx * BS + BS / 2, by * BS + BS / 2)
end

-- Open grazing ground in a block (land, no building, field or water; few trees), 0..1.
local function openness(s, b)
  local N = s.N
  local bn = N / BS
  local by = floor((b - 1) / bn)
  local bx = (b - 1) - by * bn
  local open, n = 0, 0
  for iy = by * BS + 1, by * BS + BS, 2 do
    for ix = bx * BS + 1, bx * BS + BS, 2 do
      local i = (iy - 1) * N + ix
      n = n + 1
      if s.land[i] == 1 and s.occ[i] == 0 and s.plot[i] == 0 and s.riv[i] <= 0 then
        open = open + (s.tree[i] > 0 and s.trees.st[s.tree[i]] == 1 and 0.3 or 1)
      end
    end
  end
  return open / n
end
M.openness = openness

-- Woods in a block (standing trees on dry land), 0..1: what pigs root in and goats browse.
local function woods(s, b)
  local N = s.N
  local bn = N / BS
  local by = floor((b - 1) / bn)
  local bx = (b - 1) - by * bn
  local w, n = 0, 0
  for iy = by * BS + 1, by * BS + BS, 2 do
    for ix = bx * BS + 1, bx * BS + BS, 2 do
      local i = (iy - 1) * N + ix
      n = n + 1
      if s.land[i] == 1 and s.tree[i] > 0 and s.trees.st[s.tree[i]] == 1 then w = w + 1 end
    end
  end
  return min(1, 2.5 * w / n)
end
M.woods = woods

-- Daily: grass regrows with warmth and water (little in winter), up to what the ground allows; the
-- woods' share of each block (trees are felled) for rooting and browsing.
function M.daily_pasture(W, s, day)
  local P = s.pasture
  if not P then return end
  local c = C()
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  local T = W.F.temp[s.cell] or 10
  local sw = W.F.soil_water[s.cell] or 0.5
  local warm = T > c.grow_tmin and min(1, (T - c.grow_tmin) / (c.grow_topt - c.grow_tmin)) or 0
  local g = c.grass_regrow * warm * min(1, 0.4 + sw * 1.2) * (doy >= 36 and 0.25 or 1)
  local nb = (s.N / BS) * (s.N / BS)
  P.wood = P.wood or {}
  for b = 1, nb do
    local cap = P.open[b] or 0
    local v = P.grass[b] or 0
    if v < cap then P.grass[b] = min(cap, v + g * cap) end
    if (day + b) % 4 == 0 or not P.wood[b] then P.wood[b] = woods(s, b) end
  end
end

-- The day's ground for a herd: the grassiest open block within reach of its pen (not the one
-- grazed bare); for the pigs the woodiest block (roots; mast in autumn).
function M.pick_spot(W, s, H, now, reach)
  local P = s.pasture
  local pen = s.buildings[H.pen]
  local c = C()
  local bn = s.N / BS
  local best, bs = 0, -1e9
  local swine = H.kind == "swine"
  reach = reach or (swine and c.pig_reach or c.pasture_reach)
  for b = 1, bn * bn do
    local v = swine and (P.wood and P.wood[b] or 0) or (P.grass[b] or 0)
    if v > 0.15 and (not swine or (P.open[b] or 0) > 0.2) then
      local x, y = M.block_xy(s, b)
      local d = sqrt((x - pen.cx) ^ 2 + (y - pen.cy) ^ 2)
      if d < reach then
        local sc = v - d * 0.6
        if sc > bs then best, bs = b, sc end
      end
    end
  end
  if best == 0 then return nil end
  local x, y = M.block_xy(s, best)
  local fx, fy = mapm.free_spot(s, x, y, 4)
  return fx or x, fy or y, best
end

-- Deciding ---------------------------------------------------------------------------------

-- Is the herd's herder (a person) still at it?
function M.herder(W, s, H)
  local p = W.S.v3.people[H.herder or 0]
  if p and p.alive and (p.objk == "herd" or p.objk == "herd_home") and p.herd_of == M.HIDX[H.kind] + 1 then return p end
  return nil
end

-- Walk from where it stands to (x, y) if the straight way is open, else as near as it can.
local function walk_to(W, s, a, now, x, y, speed)
  local i0, i1 = mapm.tile_of(s, a.tx, a.ty), mapm.tile_of(s, x, y)
  if i1 == 0 or s.cost[i1] < 0 then
    local fx, fy = mapm.free_spot(s, x, y, 3)
    if not fx then return false end
    x, y = fx, fy
    i1 = mapm.tile_of(s, x, y)
  end
  if i0 > 0 and i1 > 0 and i0 ~= i1 and not nav.los(s, i0, i1, 1.65) then
    -- blocked: a step along the way the people's paths would take (one hop of an A* route)
    a.mv = a.mv or { x = {}, y = {}, cum = {} }
    local path = nav.path(s, a.tx, a.ty, x, y, 1, a.mv)
    if not path or path.n < 2 then return false end
    x, y = path.x[2], path.y[2]
  end
  local dx, dy = x - a.tx, y - a.ty
  local d = sqrt(dx * dx + dy * dy)
  if d < 1e-4 then return false end
  a.x, a.y = a.tx, a.ty
  a.tx, a.ty = x, y
  a.act, a.t0, a.t1 = "walk", now, now + d / speed
  return true
end

local function stand(a, now, act, dur)
  a.x, a.y = a.tx, a.ty
  a.act, a.t0, a.t1 = act, now, now + max(0.05, dur)
end

-- Its own place in the herd's spread around (cx, cy): the sunflower spiral (spacing sep), turned
-- by the day.
local function place(H, a, cx, cy, sep, turn)
  local i = (a.slot or a.k) + 0.5
  local r = sep * sqrt(i)
  local ang = i * GOLD + turn
  return cx + r * cos(ang), cy + r * sin(ang), r
end

-- The pen's ground: a place in it for each animal (a jittered grid filling the enclosure).
local function pen_place(s, pen, a)
  local T = s.T
  local cols = max(1, pen.w - 1)
  local i = (a.slot or a.k)
  local rows = max(1, pen.h - 1)
  local cx = i % cols
  local cy = floor(i / cols) % rows
  local jx = ((a.k * 0.618034) % 1 - 0.5) * 0.6
  local jy = ((a.k * 0.381966) % 1 - 0.5) * 0.6
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  return x0 + (cx + 0.5 + jx) * T, y0 + (cy + 0.5 + jy) * T
end

-- The end of animal a's action at time now: its effects, then the next action.
function M.step(W, s, H, a, now)
  local c = C()
  local S = SP(a.sp)
  local P = s.pasture
  local dt = now - a.t0
  -- the body: hunger rises; grazing (the block's grass), rooting and browsing (the woods), the
  -- rack or trough at the pen feed it
  a.hunger = min(1, a.hunger + S.hunger * dt)
  if a.act == "graze" then
    local b = block_of(s, a.tx, a.ty)
    if a.sp == "pig" then
      local wood = P.wood and P.wood[b] or 0
      local doy = s.doy or 0
      local mast = (doy >= c.mast_days[1] and doy <= c.mast_days[2]) and S.mast * wood or 0
      local eat = min(a.hunger, (S.root * (0.6 + 0.4 * wood) + mast) * dt)
      a.hunger = a.hunger - eat
    else
      local g = P.grass[b] or 0
      local feed = g
      if S.browse and P.wood then feed = max(g, S.browse * (P.wood[b] or 0)) end
      local eat = min(a.hunger, S.graze * dt * feed)
      a.hunger = a.hunger - eat
      local fromg = feed > 0 and eat * g / feed or 0
      P.grass[b] = max(0, g - fromg * c.grass_per_feed * S.se)
    end
  elseif (a.act == "pen" or (a.act == "rest" and not H.out)) and a.hunger > 0.4 and (H.rack or 0) > 0 then
    -- hay from the rack (sheep-day units: a cow eats as much as several sheep), slops in the
    -- pigs' trough (a pig's own hunger units)
    local w = S.se > 0 and S.se or 1
    local eat = min(a.hunger - 0.1, H.rack / w)
    a.hunger = a.hunger - eat
    H.rack = H.rack - eat * w
  end
  if a.act == "pen" then H.manure = (H.manure or 0) + dt * c.manure_hour * max(0.5, S.se) end
  if (a.lact or 0) > now and S.milk then
    -- (a cow or goat in milk: the udder fills; what her young does not take is for the milking)
    local suck = M.young_of(H, a, now) and 0.5 or 1
    a.milk = min(S.milk * 0.75, (a.milk or 0) + S.milk / 24 * dt * suck * (a.hunger < 0.7 and 1 or 0.4))
  end
  if a.hunger >= 1 then a.health = max(0, a.health - 0.02 * dt)
  elseif (a.sick or 0) > 0 then a.health = max(0, a.health - c.sick_harm * dt)
  elseif a.health < 1 then a.health = min(1, a.health + 0.01 * dt) end
  -- held by someone (milked, shorn, slaughtered): it stands while they work
  if (a.held or 0) ~= 0 then
    local q = W.S.v3.people[a.held]
    if q and q.alive and q.tgt_kind == "animal" and q.tgt == a.k and q.tgt_herd == M.HIDX[H.kind] then
      stand(a, now, "held", 0.1)
      return
    end
    a.held = 0
  end
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  local night = lh >= (s.sunset or 18) + 0.4 or lh < (s.sunrise or 6) - 0.3
  local pen = s.buildings[H.pen]
  local age = (now - a.born) / 1152
  -- the young keep to their mothers
  local mom = a.mother ~= 0 and H.animals[a.mother]
  if age < S.young_years and mom and mom.alive then
    local mx, my = M.pos(mom, now)
    local d = sqrt((mx - a.tx) ^ 2 + (my - a.ty) ^ 2)
    local near = S.sep * 0.6
    if d > near then
      local ang = u(W, s, H, a, now, 1) * 6.2832
      local ox, oy = near * 0.5 * cos(ang), near * 0.5 * sin(ang)
      if walk_to(W, s, a, now, mx + ox, my + oy, S.speed * 1.3) then return end
    end
    local ma = mom.act
    stand(a, now, (ma == "rest" or ma == "pen") and "rest" or (ma == "walk" and "graze" or ma == "held" and "graze" or ma),
      0.15 + 0.3 * u(W, s, H, a, now, 2))
    if a.act == "pen" then a.act = "rest" end
    return
  end
  -- where the herd is kept now
  local p = M.herder(W, s, H)
  local cx, cy, inpen
  if p then
    cx, cy = M.person_pos(p, now)
  elseif H.out and not night then
    cx, cy = H.spot_x or pen.cx, H.spot_y or pen.cy
  elseif H.kind == "swine" and H.roam_x and not night and lh > (s.sunrise or 6) + 1.5 and lh < (s.sunset or 18) - 0.8
    and (s.rain_now or 0) < 2 and (a.hunger > 0.3 or a.act ~= "pen") then
    -- (pigs nobody takes out push out of the sty by day and root about the village's edge by
    -- themselves, home for the night)
    cx, cy = H.roam_x, H.roam_y
  else
    inpen = true
    if H.out and night then H.out = false end    -- (no herder: they come home to the pen by themselves)
  end
  local turn = (floor(now / 24) * GOLD * 3.7) % 6.2832
  local tx, ty, r
  if inpen then
    tx, ty = pen_place(s, pen, a)
    r = s.T * 0.6
  else
    tx, ty, r = place(H, a, cx, cy, S.sep, turn)
    if not S.browse and a.sp ~= "pig" then
      -- (grass grows in the open: a grazer's place is not under a tree; the next places round)
      for q = 1, 2 do
        local i = mapm.tile_of(s, tx, ty)
        local k = i > 0 and s.tree[i] or 0
        if i > 0 and s.cost[i] >= 0 and not (k > 0 and s.trees.st[k] == 1) then break end
        tx, ty = place(H, a, cx, cy, S.sep, turn + q * 2.1)
      end
    end
    r = max(S.sep * 0.7, r * 0.25)
  end
  local dx, dy = tx - a.tx, ty - a.ty
  local d = sqrt(dx * dx + dy * dy)
  if d > r then
    -- to its place: a long way is walked in hops (they look up and follow the others)
    local hop = S.hop * (0.7 + 0.6 * u(W, s, H, a, now, 5))
    local gx, gy = tx + (u(W, s, H, a, now, 3) - 0.5) * r, ty + (u(W, s, H, a, now, 4) - 0.5) * r
    if d > hop then
      local f = hop / d
      gx, gy = a.tx + (gx - a.tx) * f, a.ty + (gy - a.ty) * f
    end
    if walk_to(W, s, a, now, gx, gy, S.speed * (d > 0.4 and 1.4 or 1)) then return end
  end
  if inpen then
    -- the night in the pen (lying down), or waiting in it by day
    stand(a, now, "pen", (night and 1.8 or 0.5) + (night and 2.4 or 0.7) * u(W, s, H, a, now, 6))
    if not night and u(W, s, H, a, now, 12) < 0.3 and (a.sick or 0) == 0 then
      -- (restless by day: a few steps about the pen)
      local ax, ay = pen_place(s, pen, a)
      local ang = u(W, s, H, a, now, 13) * 8
      local o = P8[floor(ang) + 1]
      walk_to(W, s, a, now, ax + o[1] * s.T * 0.4, ay + o[2] * s.T * 0.4, S.speed * 0.4)
    end
    return
  end
  -- out: graze (root), a few steps to fresher ground, now and then lie down (cattle chew the cud)
  local v = u(W, s, H, a, now, 7)
  local sick = (a.sick or 0) > 0
  if v < S.stepping and not sick then
    local ang = u(W, s, H, a, now, 8) * 8
    local o = P8[floor(ang) + 1]
    local st = S.sep * (0.3 + 0.5 * u(W, s, H, a, now, 9))
    if walk_to(W, s, a, now, a.tx + o[1] * st, a.ty + o[2] * st, S.speed * 0.5) then return end
  end
  if sick or (v > 1 - S.resting and a.hunger < 0.35) then
    stand(a, now, "rest", S.rest_h * (0.6 + 0.8 * u(W, s, H, a, now, 10)))
  else
    stand(a, now, "graze", S.graze_h * (0.6 + 0.8 * u(W, s, H, a, now, 11)))
  end
end

-- Her youngest still at foot.
function M.young_of(H, a, now)
  local y = a.last_young
  if not y or y == 0 then return nil end
  local b = H.animals[y]
  if b and b.alive and (now - b.born) / 1152 < SP(b.sp).young_years then return b end
  return nil
end

-- Daily round: pasture, places in the spread, births, milk drying off, wool, sickness, deaths ----
function M.daily(W, s, day, push)
  if not s.herds then return end
  local c = C()
  M.daily_pasture(W, s, day)
  local now = W.hour
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  if doy == c.slaughter[1] then s.acc.culled_plan = (s.acc.culled_plan or 0) + M.plan_cull(W, s) end
  if doy == c.pig_slaughter[1] then s.acc.culled_plan = (s.acc.culled_plan or 0) + M.plan_pig_cull(W, s) end
  local year = floor((day + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  for hi = 1, #M.HERDS do
    local H = s.herds[M.HERDS[hi]]
    if H then M.herd_daily(W, s, H, day, doy, year, now, push) end
  end
end

function M.herd_daily(W, s, H, day, doy, year, now, push)
  local c = C()
  local L = H.animals
  local n = #L
  -- places in the spread: the living, in an order reshuffled every day
  local base = rng.base(W.seed, rng.S.V3_LIFE, day * 7 + M.HIDX[H.kind])
  local ord = H._ord or {}
  H._ord = ord
  local m = 0
  for k = 1, n do
    if L[k].alive then m = m + 1; ord[m] = k end
  end
  for k = m + 1, #ord do ord[k] = nil end
  for i = m, 2, -1 do
    local j = 1 + floor(rng.fbase(base, i) * i)
    ord[i], ord[j] = ord[j], ord[i]
  end
  for i = 1, m do L[ord[i]].slot = i - 1 end
  if H.kind == "swine" then
    -- where the pigs root by themselves today: the best ground near the sty
    local x, y = M.pick_spot(W, s, H, now, c.pig_roam)
    H.roam_x, H.roam_y = x, y
  end
  -- sickness spreads in a pen: the share already sick, crowding, wet days
  local sick = 0
  for i = 1, m do if (L[ord[i]].sick or 0) > 0 then sick = sick + 1 end end
  H.sick = sick
  local pen = s.buildings[H.pen]
  local crowd = pen and m / max(1, pen.w * pen.h * 0.8) or 0
  local wet = (s.rain_now or 0) > 0 and 1 or 0
  for k = 1, n do
    local a = L[k]
    if a.alive then
      local S = SP(a.sp)
      local key = s.key * 7 + M.HIDX[H.kind] * 20000 + k
      local age = (now - a.born) / 1152
      if S.wool then a.wool = min(c.wool_max, (a.wool or 0) + S.wool * (age > S.young_years and 1 or 0.3)) end
      -- in milk until she dries off; nothing left in the udder of a dry beast
      if (a.lact or 0) > 0 and a.lact <= now then a.lact = 0; a.milk = 0 end
      -- sickness: catching it (the sick in the pen, crowding, wet, hunger), recovering or not
      local v = rng.float(W.seed, rng.S.V3_LIFE, key, day, 30)
      if (a.sick or 0) > 0 then
        a.sick = a.sick - 1
        if v < c.sick_recover then a.sick = 0 end
      else
        local pz = c.sick_base * (1 + crowd) * (1 + wet) * (a.hunger > 0.7 and 2 or 1) + c.sick_spread * sick / max(1, m)
        if v < pz then
          a.sick = c.sick_days
          H.sick = H.sick + 1
          s.acc.stock_sick = (s.acc.stock_sick or 0) + 1
        end
      end
      -- deaths: old age, hunger, sickness
      local hz = age > S.old_years and (age - S.old_years) * 0.08 / 48 or 0.002 / 48
      if a.health < 0.3 then hz = hz + (0.3 - a.health) * 0.3 end
      local w = rng.float(W.seed, rng.S.V3_LIFE, key, day, 31)
      if w < hz then
        a.alive = false
        a.died = now
        a.cause = (a.sick or 0) > 0 and "sickness" or (a.hunger >= 0.9 and "hunger" or "age")
        s.acc.stock_died = (s.acc.stock_died or 0) + 1
        s.acc["died_" .. a.sp] = (s.acc["died_" .. a.sp] or 0) + 1
      elseif a.sex == "f" and age >= S.adult_years and a.bred ~= year and M.breeding(S, doy) and M.has_sire(H, a, now) then
        -- births in their season (a dam in good condition): a lamb, a kid, a calf, a litter
        local x = rng.float(W.seed, rng.S.V3_LIFE, key, day, 32)
        if x < S.birth_chance and a.health > 0.5 and a.hunger < 0.8 then
          a.bred = year
          local litter = S.litter
          if S.litter > 1 then
            litter = max(1, floor(S.litter * (0.6 + 0.8 * rng.float(W.seed, rng.S.V3_LIFE, key, day, 33)) + 0.5))
          elseif rng.float(W.seed, rng.S.V3_LIFE, key, day, 33) < S.twins then litter = 2 end
          for t = 1, litter do
            local sx = rng.float(W.seed, rng.S.V3_LIFE, key, day, 34 + t) < 0.5 and "f" or "m"
            local coat = a.coat
            if rng.float(W.seed, rng.S.V3_LIFE, key, day, 50 + t) < 0.25 then coat = M.coat(W, s, a.sp, key * 13 + t, day) end
            local y = M.add(W, s, H, { sp = a.sp, sex = sx, born = now, owner = a.owner, mother = k, coat = coat,
              x = a.tx + 0.01 * t, y = a.ty })
            a.last_young = y.k
            push(W, y.t1, M.event_id(s, H, y), y.ver)
            s.acc.born_stock = (s.acc.born_stock or 0) + 1
            s.acc["born_" .. a.sp] = (s.acc["born_" .. a.sp] or 0) + 1
          end
          if S.milk then a.lact = now + S.lact_days * 24; a.milk = 0 end
        end
      end
    end
  end
end

function M.breeding(S, doy)
  if doy >= S.birth[1] and doy <= S.birth[2] then return true end
  return S.birth2 ~= nil and doy >= S.birth2[1] and doy <= S.birth2[2]
end

-- A grown male of her kind in the herd (a ram, a billy, a bull, a boar).
function M.has_sire(H, a, now)
  local L = H.animals
  for i = 1, #L do
    local b = L[i]
    if b.alive and b.sex == "m" and b.sp == a.sp and (now - b.born) / 1152 >= SP(b.sp).adult_years then return true end
  end
  return false
end

-- A coat for a new animal of species sp (founding, or a lamb unlike its mother).
function M.coat(W, s, sp, key, day)
  local v = rng.float(W.seed, rng.S.V3_FOUND, key, day or 0, 77)
  if sp == "sheep" then return v < 0.14 and "dark" or "white"
  elseif sp == "goat" then return v < 0.4 and "pied" or "brown"
  elseif sp == "cattle" then return v < 0.3 and "pied" or "red"
  elseif sp == "pig" then return v < 0.35 and "dark" or "pink" end
  return "plain"
end

-- Winter feed (sheep-day units of hay) the grazing herds want: each beast's share.
function M.hay_want(s, now)
  local c = C()
  local want = 0
  for _, kind in ipairs({ "flock", "cattle" }) do
    local H = s.herds and s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive then want = want + SP(a.sp).se end
      end
    end
  end
  return want * c.hay_days * c.sheep_hunger * 24 * 0.6, want
end

-- The beasts the households will kill this autumn (D-089, D-092): the grazing herds wintered on
-- the hay in the stack - the most dispensable first: the old, males beyond one sire of each kind,
-- the young males, then this year's young - marked a.cull = true. Returns how many.
function M.plan_cull(W, s)
  local c = C()
  local now = W.hour
  local alive = {}
  local se = 0
  for _, kind in ipairs({ "flock", "cattle" }) do
    local H = s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive and not a.cull then alive[#alive + 1] = a; se = se + SP(a.sp).se end
      end
    end
  end
  local per = c.hay_days * c.sheep_hunger * 24 * 0.6
  local keep = (s.hay or 0) / per
  local over = se - keep
  if over <= 0 then return 0 end
  table.sort(alive, function(x, y)
    local ax, ay = (now - x.born) / 1152, (now - y.born) / 1152
    local ox, oy = SP(x.sp).old_years, SP(y.sp).old_years
    local sx = (ax > ox - 1 and 3 or 0) + (x.sex == "m" and 2 or 0) + (ax < 1 and 1 or 0) + ax * 0.01
    local sy = (ay > oy - 1 and 3 or 0) + (y.sex == "m" and 2 or 0) + (ay < 1 and 1 or 0) + ay * 0.01
    if sx ~= sy then return sx > sy end
    if x.sp ~= y.sp then return x.sp < y.sp end
    return x.k < y.k
  end)
  local sires = {}
  local n = 0
  for _, a in ipairs(alive) do
    if over <= 0 then break end
    local S = SP(a.sp)
    local age = (now - a.born) / 1152
    if a.sex == "m" and not sires[a.sp] and age > S.adult_years and age < S.old_years - 1 then
      sires[a.sp] = true      -- (one good sire of each kind is kept)
    else
      a.cull = true
      over = over - S.se
      n = n + 1
    end
  end
  return n
end

-- Pigs are killed at the start of winter, fattened on the autumn's mast: all but a sow for each
-- household that keeps pigs, and the boar.
function M.plan_pig_cull(W, s)
  local H = s.herds and s.herds.swine
  if not H then return 0 end
  local now = W.hour
  local kept, boar = {}, false
  local n = 0
  for _, a in ipairs(H.animals) do
    if a.alive and not a.cull then
      local age = (now - a.born) / 1152
      if a.sex == "f" and age >= 0.8 and not kept[a.owner] and age < SP("pig").old_years - 1 then kept[a.owner] = true
      elseif a.sex == "m" and age >= 0.8 and not boar then boar = true
      elseif age >= 0.3 then
        a.cull = true
        n = n + 1
      end
    end
  end
  return n
end

-- A new animal (founding, births).
function M.add(W, s, H, o)
  local k = #H.animals + 1
  local now = W.S.v3.now or W.hour
  local a = { k = k, sp = o.sp, sex = o.sex or "f", born = o.born or now, owner = o.owner or 0, mother = o.mother or 0,
    coat = o.coat or "plain", x = o.x, y = o.y, tx = o.x, ty = o.y, t0 = now, t1 = now + 0.05 + 0.1 * (k % 7) / 7,
    act = "pen", ver = 0, hunger = 0.3, health = 1, sick = 0, wool = o.wool or 0, milk = 0, lact = o.lact or 0,
    alive = true, bred = -1, held = 0, slot = k - 1 }
  H.animals[k] = a
  return a
end

-- Every living animal of the settlement (for the view, tools and tests): calls fn(H, a).
function M.each(s, fn)
  if not s.herds then return end
  for hi = 1, #M.HERDS do
    local H = s.herds[M.HERDS[hi]]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive then fn(H, a) end
      end
    end
  end
end

-- Founding ---------------------------------------------------------------------------------

-- A pen of `kind` at the village's edge on open ground (the side with the most open ground near).
local function found_pen(W, s, kind, w, h)
  local N = s.N
  local c0 = floor(N / 2) + 1
  local best, bx, by = -1, c0, c0
  for _, dir in ipairs({ { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 } }) do
    for r = 12, 44, 3 do
      local ix, iy = c0 + dir[1] * r, c0 + dir[2] * r
      if mapm.rect_free(s, ix - 3, iy - 3, ix + w + 2, iy + h + 2, true) then
        local open = 0
        for jy = iy - 6, iy + h + 4, 2 do
          for jx = ix - 6, ix + w + 4, 2 do
            local i = (jy - 1) * N + jx
            if jx >= 1 and jy >= 1 and jx <= N and jy <= N and s.land[i] == 1 and s.plot[i] == 0 and s.tree[i] == 0 then open = open + 1 end
          end
        end
        local sc = open - r * 0.2
        if sc > best then best, bx, by = sc, ix, iy end
        break
      end
    end
  end
  if best < 0 then return nil end
  return require("sim.agents.settle").add_building(W, s, kind, bx, by, "s", "standing")
end

-- Founding: the village's herds, each household's beasts by what its land favours (sheep
-- everywhere; goats where it is dry or rough; cattle on good grass; pigs where there are woods),
-- the pens, the hay left from last year (the season's), grass on the commons.
function M.found(W, s, households, u, doy0)
  local c = C()
  local N = s.N
  s.herds = {}
  local P = { grass = {}, open = {}, wood = {} }
  s.pasture = P
  local nb = (N / BS) * (N / BS)
  for b = 1, nb do
    P.open[b] = openness(s, b)
    P.grass[b] = P.open[b] * (0.5 + 0.3 * u())
    P.wood[b] = woods(s, b)
  end
  s.hay = c.hay_found[floor(doy0 / 12) + 1]
  local F = W.F
  local wet = F.soil_water and F.soil_water[s.cell] or 0.5
  local forest = F.forest and F.forest[s.cell] or 0.3
  local nh = #households
  local function herd(kind)
    local H = s.herds[kind]
    if H then return H end
    local w, h = c.pen_size[kind][1], c.pen_size[kind][2]
    local pen = found_pen(W, s, M.PEN[kind], w, h)
    if not pen then return nil end
    H = { kind = kind, animals = {}, pen = pen.id, herder = 0, out = false, rack = c.rack_found[kind], manure = 0, sick = 0 }
    s.herds[kind] = H
    return H
  end
  local function one(H, sp, hh, female, age, extra)
    local pen = s.buildings[H.pen]
    local x = pen.cx + (u() - 0.5) * pen.w * s.T * 0.7
    local y = pen.cy + (u() - 0.5) * pen.h * s.T * 0.7
    local o = { sp = sp, sex = female and "f" or "m", born = W.hour - age * 1152, owner = hh.id, x = x, y = y,
      coat = M.coat(W, s, sp, hh.id * 31 + #H.animals, 0) }
    for k2, v2 in pairs(extra or {}) do o[k2] = v2 end
    return M.add(W, s, H, o)
  end
  -- sheep: a few for every household (a ram among them)
  local H = herd("flock")
  if H then
    for i, hh in ipairs(households) do
      local n = 2 + floor(u() * 3)
      for q = 1, n do
        one(H, "sheep", hh, not (q == 1 and (i == 1 or u() < 0.3)), 1 + u() * 5,
          { wool = doy0 < 12 and 1.0 + u() * 0.5 or 0.3 + u() * 0.4 })
      end
      -- goats where the land is dry or rough (a nanny or two, a billy in the village)
      if u() < 0.25 + 0.5 * (1 - min(1, wet * 1.6)) then
        for q = 1, 1 + floor(u() * 2) do one(H, "goat", hh, true, 1 + u() * 4) end
      end
    end
    -- (a billy for the village if anyone keeps goats: the first goat-keeping household's)
    for _, a in ipairs(H.animals) do
      if a.sp == "goat" then
        local owner = households[1]
        for _, hh in ipairs(households) do if hh.id == a.owner then owner = hh end end
        one(H, "goat", owner, false, 2 + u() * 2)
        break
      end
    end
  end
  -- cattle on good grass: a cow (some in milk) in a share of the households, a bull
  local grassy = min(1, 0.4 + wet)
  local cows = 0
  for _, hh in ipairs(households) do
    if u() < c.cattle_share * grassy then
      local HC = herd("cattle")
      if HC then
        local inmilk = u() < 0.6
        one(HC, "cattle", hh, true, 2 + u() * 6, inmilk and { lact = W.hour + (0.2 + 0.6 * u()) * SP("cattle").lact_days * 24 } or nil)
        cows = cows + 1
      end
    end
  end
  if cows > 0 then one(s.herds.cattle, "cattle", households[1 + floor(u() * nh)], false, 3 + u() * 3) end
  -- pigs where there are woods: a sow (and a growing pig or two) for most households, a boar
  local sows = 0
  for _, hh in ipairs(households) do
    if u() < c.pig_share * min(1, 0.4 + forest * 1.5) then
      local HS = herd("swine")
      if HS then
        one(HS, "pig", hh, true, 1.5 + u() * 2)
        for q = 1, floor(u() * 3) do one(HS, "pig", hh, u() < 0.5, 0.3 + u() * 0.4) end
        sows = sows + 1
      end
    end
  end
  if sows > 0 then one(s.herds.swine, "pig", households[1 + floor(u() * nh)], false, 1.5 + u() * 2) end
  -- goats in milk at founding in spring and summer
  for _, kind in ipairs(M.HERDS) do
    local HH = s.herds[kind]
    if HH then
      for _, a in ipairs(HH.animals) do
        if a.sp == "goat" and a.sex == "f" and doy0 < 24 and u() < 0.6 then a.lact = W.hour + (0.3 + 0.5 * u()) * SP("goat").lact_days * 24 end
      end
    end
  end
  return s.herds
end

return M
