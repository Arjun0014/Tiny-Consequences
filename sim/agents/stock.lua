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
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
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
local floor, max, min, sqrt, abs, cos, sin = math.floor, math.max, math.min, math.sqrt, math.abs, dmath.cos, dmath.sin
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
  local k = r - hi * 20000
  -- (dogs below 10000, bird flocks from 10000: sim/agents/birds.lua)
  if hi == 4 and k >= 10000 then return sid, "bird", k - 10000 end
  return sid, M.HERDS[hi + 1] or (hi == 4 and "dog" or "wild"), k
end

-- (set by sim/agents/init.lua each advance: the people, for a yoked ox's place; the event queue)
M.people = nil
M.push = function() end
-- (the ox's middle ahead of the ploughman's hands: the plough's handles and body, the traces to the
-- swingletree and half the beast: ~3.3 m at the villagers' scale, S14)
M.YOKE_AHEAD = 0.085

-- A yoked ox's ploughman begins a new step (sim/agents/act.lua): the team turns from where the ox
-- is now (its angle and distance from him), or, just yoked (`first`), from where it stands.
function M.yoke_turn(a, q, now, first)
  local qx, qy = M.person_pos(q, now)
  local ox, oy
  if first then ox, oy = a.tx, a.ty else ox, oy = M.pos(a, now) end
  local dx, dy = ox - qx, oy - qy
  a.yh0 = dmath.atan2(dy, dx)
  a.yd0 = sqrt(dx * dx + dy * dy)
  a.yt0 = now
  a.yth = a.yh0
end

-- Position of animal a at time t (a straight walk between t0 and t1, else where it stands). A yoked
-- ox (S14) walks just ahead of its ploughman along his heading wherever he goes: its place is his,
-- as a mate's in a boat is the skipper's.
-- (a turn of the team: the ox swings round the ploughman on an arc, and steps out to its place in
-- front when first yoked, over YOKE_TURN hours from the moment recorded by yoke_turn)
M.YOKE_TURN = 0.12
local PI2 = 6.283185307179586
local function yoke_heading(a, q, t)
  local _, _, dx, dy = M.person_pos(q, t)
  local l = dx and sqrt(dx * dx + dy * dy) or 0
  if l > 1e-9 then return dmath.atan2(dy, dx) end
  return a.yth or 0
end
function M.pos(a, t)
  if a.act == "yoked" then
    local q = M.people and M.people[a.yoke or 0]
    if q and q.alive then
      local qx, qy = M.person_pos(q, t)
      -- (pos never writes: the view calls it too; the turn's start is kept by the sim's step)
      local th, d = yoke_heading(a, q, t), M.YOKE_AHEAD
      local f = a.yt0 and (t - a.yt0) / M.YOKE_TURN or 1
      if f < 1 then
        if f < 0 then f = 0 end
        local dth = (th - (a.yh0 or th) + PI2 * 1.5) % PI2 - PI2 * 0.5
        th = (a.yh0 or th) + dth * f
        d = (a.yd0 or d) + (d - (a.yd0 or d)) * f
      end
      return qx + dmath.cos(th) * d, qy + dmath.sin(th) * d
    end
    return a.tx, a.ty
  end
  local ac = a.act
  if (ac ~= "walk" and ac ~= "graze") or a.t1 <= a.t0 then return a.tx, a.ty end
  -- (walking, or grazing its way slowly along: S15)
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

-- (block centres by map size and tile, worked out once: the scans of every block - the day's
-- pasture, a wild band's ground - were paying two divisions and a C call each, S14)
local BXY = {}
function M.block_xy(s, b)
  local key = s.N * 1e6 + s.T
  local t = BXY[key]
  if not t then
    t = { x = {}, y = {} }
    local bn = s.N / BS
    for q = 1, bn * bn do
      local by = floor((q - 1) / bn)
      local bx = (q - 1) - by * bn
      t.x[q], t.y[q] = mapm.ixy_xy(s, bx * BS + BS / 2, by * BS + BS / 2)
    end
    BXY[key] = t
  end
  return t.x[b], t.y[b]
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
      local d = sqrt(dsq(x - pen.cx) + dsq(y - pen.cy))
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
local function walk_to(W, s, a, now, x, y, speed, here)
  local i0, i1 = mapm.tile_of(s, a.tx, a.ty), mapm.tile_of(s, x, y)
  if i1 == 0 or s.cost[i1] < 0 then
    -- (a step about where it stands that runs into a wall or water is not taken: S15)
    if here then return false end
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

-- Grazing its way: a slow drift of L along (hx, hy) over the bout, head down (S15); where the
-- ground ahead is blocked it grazes where it stands.
local function graze_drift(s, a, now, hx, hy, L, dur, open_only)
  local x, y = a.tx, a.ty
  a.x, a.y = x, y
  if L > 1e-5 then
    local tx, ty = x + hx * L, y + hy * L
    local i = mapm.tile_of(s, tx, ty)
    if i > 0 and s.cost[i] >= 0 then
      local k = s.tree[i]
      if open_only and k > 0 and s.trees.st[k] == 1 then
        -- (grass grows in the open: a grazer turns away from the trees instead)
        a.hx, a.hy = -hy, hx
      else
        a.tx, a.ty = tx, ty
      end
    end
  end
  a.act, a.t0, a.t1 = "graze", now, now + max(0.05, dur)
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

-- A point inside the pen's enclosure (fractions fx, fy of its inner width and height).
local function pen_spot(s, pen, fx, fy)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  return x0 + (0.3 + fx * (pen.w - 1.6)) * T, y0 + (0.3 + fy * (pen.h - 1.6)) * T
end

local function inside_pen(s, pen, x, y)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  local fx, fy = (x - x0) / T, (y - y0) / T
  return fx > -0.6 and fy > -0.6 and fx < pen.w - 0.4 and fy < pen.h - 0.4
end

-- (S15) What makes each beast itself, from who it is (no state): how far from the herd's middle it
-- likes to keep (bold), its own pace, how soon it notices (alert), the side of the herd it keeps.
local function trait(a, m) return ((a.k + 0.5) * m) % 1 end
local T_BOLD, T_PACE, T_ALERT, T_SIDE = 0.6180339887, 0.4142135624, 0.7320508076, 0.2360679775
M.trait = trait
M.T_BOLD, M.T_PACE, M.T_ALERT, M.T_SIDE = T_BOLD, T_PACE, T_ALERT, T_SIDE

-- The others of the herd round a beast at (x, y) now (S15): its nearest (and where), the number,
-- middle and lying-down count of those within `sight`, the middle and count of all. Penned, held
-- and yoked beasts are not with the herd.
local SC = { nn = nil, nd = 0, nx = 0, ny = 0, ln = 0, lrest = 0, cx = 0, cy = 0, cn = 0 }
M.SC = SC
local function scan(s, H, a, now, x, y, sight)
  local s2 = sight * sight
  local nn, nd2, nx, ny = nil, 1e18, 0, 0
  local ln, lr, cx, cy, cn = 0, 0, 0, 0, 0
  local an = H.animals
  local inpen = a.act == "pen"
  for i = 1, #an do
    local b = an[i]
    if b ~= a and b.alive then
      local ba = b.act
      if ba ~= "held" and ba ~= "yoked" and (ba == "pen") == inpen then
        local bx, by = b.tx, b.ty
        -- (one grazing drifts a few hand-breadths a bout: where it is going is near enough)
        if ba == "walk" and b.t1 > b.t0 then
          local f = (now - b.t0) / (b.t1 - b.t0)
          if f < 1 then
            if f < 0 then f = 0 end
            bx, by = b.x + (bx - b.x) * f, b.y + (by - b.y) * f
          end
        end
        cx, cy, cn = cx + bx, cy + by, cn + 1
        local dx, dy = bx - x, by - y
        local d2 = dx * dx + dy * dy
        if d2 < nd2 then nn, nd2, nx, ny = b, d2, bx, by end
        if d2 < s2 then
          ln = ln + 1
          if ba == "rest" then lr = lr + 1 end
        end
      end
    end
  end
  SC.nn, SC.nd, SC.nx, SC.ny = nn, sqrt(nd2), nx, ny
  SC.ln, SC.lrest, SC.cx, SC.cy, SC.cn = ln, lr, cx, cy, cn
end
M.scan = scan

-- A dog of the village within r of (x, y) now: where it is.
local function dog_near(s, x, y, now, r)
  local L = s.dogs
  if not L then return nil end
  local r2 = r * r
  for i = 1, #L do
    local d = L[i]
    if d.alive then
      local dx, dy = d.tx, d.ty
      if (d.act == "walk" or d.act == "run") and d.t1 > d.t0 then
        local f = (now - d.t0) / (d.t1 - d.t0)
        if f < 1 then
          if f < 0 then f = 0 end
          dx, dy = d.x + (dx - d.x) * f, d.y + (dy - d.y) * f
        end
      end
      if dsq(dx - x) + dsq(dy - y) < r2 then return dx, dy end
    end
  end
  return nil
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
  -- yoked to the plough (D-108): ahead of the ploughman along the row, hungrier for the work
  if (a.yoke or 0) ~= 0 then
    local q = W.S.v3.people[a.yoke]
    if q and q.alive and q.ox == a.k and (q.objk == "till" or q.objk == "yoke") then
      a.hunger = min(1, a.hunger + C().ox_work_hunger * dt)
      -- (in the yoke: its place follows the ploughman's, M.pos; looked at again a little later)
      a.act = "yoked"
      a.yth = yoke_heading(a, q, now)
      a.tx, a.ty = M.pos(a, now)
      a.x, a.y = a.tx, a.ty
      a.t0, a.t1 = now, now + 0.25
      return
    end
    -- (the ploughman went off to something else: the ox stands where the yoke left it)
    if a.act == "yoked" then a.tx, a.ty = M.pos(a, now); a.x, a.y = a.tx, a.ty end
    a.yoke = 0
    if a.act == "yoked" then a.act = "rest" end
  end
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
    local d = sqrt(dsq(mx - a.tx) + dsq(my - a.ty))
    local near = S.sep * 0.6
    if d > near then
      local ang = u(W, s, H, a, now, 1) * 6.2832
      local ox, oy = near * 0.5 * cos(ang), near * 0.5 * sin(ang)
      a.why = "After its mother"
      if walk_to(W, s, a, now, mx + ox, my + oy, S.speed * 1.3) then return end
    end
    a.why = "At its mother's side"
    local ma = mom.act
    stand(a, now, (ma == "rest" or ma == "pen") and "rest" or (ma == "walk" and "graze" or ma == "held" and "graze" or ma),
      0.15 + 0.3 * u(W, s, H, a, now, 2))
    if a.act == "pen" then a.act = "rest" end
    return
  end
  -- where the herd is kept now: its herder (where they are now), the ground it was left on, the
  -- pigs' own rooting ground, else the pen
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
  local x, y = a.tx, a.ty
  local pace = 0.85 + 0.3 * trait(a, T_PACE)
  if inpen then
    local tx, ty
    local inside = inside_pen(s, pen, x, y)
    if night then
      -- the night: lying at its own place in the pen (the places shuffled every day)
      tx, ty = pen_place(s, pen, a)
    else
      -- by day in the pen: about the enclosure, never on top of another
      tx, ty = pen_spot(s, pen, u(W, s, H, a, now, 14), u(W, s, H, a, now, 15))
      scan(s, H, a, now, tx, ty, S.sep)
      if SC.nn and SC.nd < S.sep * 0.45 then tx, ty = pen_spot(s, pen, u(W, s, H, a, now, 16), u(W, s, H, a, now, 17)) end
    end
    local d = sqrt(dsq(tx - x) + dsq(ty - y))
    if d > s.T * 0.6 and (night or not inside or u(W, s, H, a, now, 12) < 0.35) then
      a.why = night and "To its place in the pen for the night" or (inside and "About the pen" or "Into the pen")
      if walk_to(W, s, a, now, tx, ty, S.speed * pace * (inside and 0.5 or 1.2)) then return end
    end
    a.why = night and "Lying in the pen for the night" or "Waiting in the pen"
    stand(a, now, "pen", (night and 1.8 or 0.4) + (night and 2.4 or 0.6) * u(W, s, H, a, now, 6))
    return
  end
  -- out with the herd. Each beast keeps within its own range of where the herd is kept (the bold
  -- at the edge, the timid near the middle), follows when it is left behind or the herder sets
  -- off, makes room when another is too close, rejoins the others when it finds itself alone,
  -- moves away from a dog, and otherwise grazes its own way (a heading it keeps, turning back at
  -- the edge of its range and away from bare ground) and lies down when fed (sooner when those
  -- about it are lying down).
  local n = H.n_alive or 1
  local R = S.sep * (0.9 * sqrt(n) + 1)
  local Ri = R * (0.45 + 0.9 * trait(a, T_BOLD))
  local dax, day = cx - x, cy - y
  local da = sqrt(dax * dax + day * day)
  local led = p and p.act == "walk"
  local hx, hy = a.hx or 1, a.hy or 0
  local sick = (a.sick or 0) > 0
  -- a dog close by: away from it, toward the herd (how a dog brings a straggler back)
  local gx, gy = dog_near(s, x, y, now, S.sep * 1.8)
  if gx then
    local ex, ey = x - gx, y - gy
    local el = sqrt(ex * ex + ey * ey)
    if el < 1e-6 then ex, ey, el = -dax, -day, max(1e-6, da) end
    ex, ey = ex / el, ey / el
    if da > 1e-6 then ex, ey = ex + dax / da * 0.6, ey + day / da * 0.6 end
    local l = max(1e-6, sqrt(ex * ex + ey * ey))
    ex, ey = ex / l, ey / l
    local st = S.sep * (0.9 + 0.6 * u(W, s, H, a, now, 18))
    a.hx, a.hy = ex, ey
    a.why = "Moving off from the dog"
    if walk_to(W, s, a, now, x + ex * st, y + ey * st, S.speed * pace * 1.6, true) then return end
  end
  if da > Ri * 1.5 or (led and da > Ri * 0.8) then
    -- left behind, or the herder is moving on: after them, to its own side of the herd
    local ux, uy = dax / da, day / da
    local side = trait(a, T_SIDE) - 0.5
    local stop = Ri * 0.6
    local tx = cx - ux * stop - uy * side * Ri * 0.8
    local ty = cy - uy * stop + ux * side * Ri * 0.8
    local dx, dy = tx - x, ty - y
    local d = sqrt(dx * dx + dy * dy)
    local hop = S.hop * pace * (0.8 + 0.5 * u(W, s, H, a, now, 5)) * (da > R * 2 and 1.6 or 1)
    if d > hop then tx, ty = x + dx / d * hop, y + dy / d * hop end
    if d > 1e-6 then a.hx, a.hy = dx / d, dy / d end
    a.why = led and "Following the herder" or (p and "Catching up with the herder" or "Back to the others")
    if walk_to(W, s, a, now, tx, ty, S.speed * pace * (da > Ri * 3 and 1.5 or 1.15)) then return end
  end
  scan(s, H, a, now, x, y, S.sep * 3)
  local nn = SC.nn
  if nn and SC.nd < S.sep * 0.35 and not sick then
    -- too close: room to graze (away from the nearest, a little along its own way)
    local ex, ey = x - SC.nx, y - SC.ny
    local el = sqrt(ex * ex + ey * ey)
    if el < 1e-6 then ex, ey, el = -hy, hx, 1 end
    ex, ey = ex / el + hx * 0.3, ey / el + hy * 0.3
    local l = max(1e-6, sqrt(ex * ex + ey * ey))
    local st = S.sep * (0.4 + 0.3 * u(W, s, H, a, now, 19))
    a.why = "Making room to graze"
    if walk_to(W, s, a, now, x + ex / l * st, y + ey / l * st, S.speed * pace * 0.6, true) then return end
  end
  if SC.ln == 0 and SC.cn > 0 then
    -- nobody in sight: toward the others
    local mx, my = SC.cx / SC.cn, SC.cy / SC.cn
    local dx, dy = mx - x, my - y
    local d = sqrt(dx * dx + dy * dy)
    if d > R * 0.5 then
      local st = min(d - R * 0.4, S.hop * pace)
      a.hx, a.hy = dx / d, dy / d
      a.why = "Rejoining the others"
      if walk_to(W, s, a, now, x + dx / d * st, y + dy / d * st, S.speed * pace * 1.1) then return end
    end
  end
  -- grazing (rooting) its own way
  local v = u(W, s, H, a, now, 7)
  local lying = SC.ln > 0 and SC.lrest / SC.ln or 0
  if sick or (not led and a.hunger < 0.35 and v > 1 - S.resting * (1 + 2.5 * lying)) then
    a.why = sick and "Sick: lying down" or (a.sp == "pig" and "Fed: lying down" or "Fed: lying down to chew the cud")
    stand(a, now, "rest", S.rest_h * (0.6 + 0.8 * u(W, s, H, a, now, 10)))
    return
  end
  -- its own way: the heading it keeps, turning a little as it goes, back toward the herd at the
  -- edge of its range, away from bare ground
  local k = (u(W, s, H, a, now, 8) - 0.5) * 1.4
  if da > Ri and da > 1e-6 then hx, hy = hx + dax / da * 0.9, hy + day / da * 0.9 end
  if nn and SC.nd < S.sep * 0.9 and SC.nd > 1e-6 then
    -- (another close by: its way bends off from it)
    hx, hy = hx + (x - SC.nx) / SC.nd * 0.8, hy + (y - SC.ny) / SC.nd * 0.8
  end
  if P then
    local here = block_of(s, x, y)
    local ahead = block_of(s, x + hx * S.sep * 3, y + hy * S.sep * 3)
    if ahead ~= here and ahead > 0 and here > 0 then
      local gh = a.sp == "pig" and (P.wood and P.wood[here] or 0) or (P.grass[here] or 0)
      local ga = a.sp == "pig" and (P.wood and P.wood[ahead] or 0) or (P.grass[ahead] or 0)
      if ga < gh * 0.6 then k = k + (k >= 0 and 1.5 or -1.5) end
    end
  end
  hx, hy = hx - hy * k, hy + hx * k
  local l = sqrt(hx * hx + hy * hy)
  if l > 1e-6 then hx, hy = hx / l, hy / l else hx, hy = 1, 0 end
  a.hx, a.hy = hx, hy
  if v < S.stepping * (led and 2.5 or 1) then
    -- a few steps on to fresh ground (a grazer not in under the trees)
    local st = S.sep * (0.3 + 0.6 * u(W, s, H, a, now, 9))
    if not S.browse and a.sp ~= "pig" then
      local i = mapm.tile_of(s, x + hx * st, y + hy * st)
      local k = i > 0 and s.tree[i] or 0
      if k > 0 and s.trees.st[k] == 1 then hx, hy = -hy, hx; a.hx, a.hy = hx, hy end
    end
    a.why = a.sp == "pig" and "Rooting its way along" or "Grazing its way along"
    if walk_to(W, s, a, now, x + hx * st, y + hy * st, S.speed * pace * 0.45, true) then return end
  end
  -- grazing (rooting), head down, drifting slowly along its way as it crops (S15)
  local dur = S.graze_h * (led and 0.25 or 1) * (0.5 + 0.8 * u(W, s, H, a, now, 11))
  a.why = a.sp == "pig" and "Rooting" or (S.browse and "Browsing" or "Grazing")
  graze_drift(s, a, now, hx, hy, sick and 0 or S.sep * (a.sp == "pig" and 0.35 or 0.6) * pace * dur, dur,
    not S.browse and a.sp ~= "pig")
end

-- The herder sets off (out of the gate, onward, or home) or the gate is opened: each beast out
-- with them notices in its own time - the alert sooner - and decides again then (S15).
function M.rouse(W, s, H, now)
  if not H then return end
  local push = M.push
  for _, a in ipairs(H.animals) do
    if a.alive and (a.held or 0) == 0 and (a.yoke or 0) == 0
      and (a.act == "graze" or a.act == "rest" or a.act == "pen") then
      local at = now + 0.015 + 0.09 * (1 - trait(a, T_ALERT)) + 0.04 * u(W, s, H, a, now, 20)
      if a.t1 > at then
        a.ver = a.ver + 1
        a.t1 = at
        push(W, at, M.event_id(s, H, a), a.ver)
      end
    end
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
  M.choose_oxen(W, s, day * 24)
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
  H.n_alive = m
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
-- Oxen (D-108): a household that farms and keeps cattle chooses a young bullock to train for the
-- plough; it is gelded (never a sire) and kept from the culls.
function M.choose_oxen(W, s, now)
  local H = s.herds and s.herds.cattle
  if not H then return end
  local V = W.S.v3
  local has = {}
  for _, a in ipairs(H.animals) do
    if a.alive and a.ox then has[a.owner] = true; a.worked = 0 end
  end
  local S = M.SP("cattle")
  for _, a in ipairs(H.animals) do
    local age = (now - a.born) / 1152
    if a.alive and not a.ox and a.sex == "m" and age >= S.young_years and age < S.old_years - 2 and not has[a.owner] then
      -- (not the herd's sire: the oldest grown bull stays a bull)
      local sire = false
      for _, b in ipairs(H.animals) do
        if b.alive and b ~= a and b.sex == "m" and not b.ox and (now - b.born) / 1152 >= S.adult_years then sire = true end
      end
      local farms = false
      for _, pl in ipairs(s.plots) do if pl.hh == a.owner then farms = true end end
      if sire and farms and V.households[a.owner] then
        a.ox, a.cull = true, nil
        has[a.owner] = true
        for _, id in ipairs(V.households[a.owner].members) do
          local q = V.people[id]
          if q and q.alive and require("sim.agents.people").stage(q, now) == "adult" then
            require("sim.agents.people").remember(q, now, "Gelded the young bullock to train him for the plough")
          end
        end
      end
    end
  end
end

function M.has_sire(H, a, now)
  local L = H.animals
  for i = 1, #L do
    local b = L[i]
    if b.alive and b.sex == "m" and not b.ox and b.sp == a.sp and (now - b.born) / 1152 >= SP(b.sp).adult_years then return true end
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
    if a.ox then
      -- (an ox is kept for the plough: D-108)
    elseif a.sex == "m" and not sires[a.sp] and age > S.adult_years and age < S.old_years - 1 then
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
  -- (D-108) a household with a cow and fields of its own brings a trained ox and its harness
  if cows > 0 then
    for _, hh in ipairs(households) do
      local cow = false
      for _, a in ipairs(s.herds.cattle.animals) do if a.owner == hh.id and a.sex == "f" then cow = true end end
      if cow then
        one(s.herds.cattle, "cattle", hh, false, 4 + u() * 4, { ox = true })
        hh.harness = true
      end
    end
  end
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
