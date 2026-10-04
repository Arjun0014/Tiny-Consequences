-- Livestock as individual animals (roadmap v3 R3; D-089 sheep, D-092 herds). A village keeps up to
-- three herds, each of individual animals owned by households:
--   flock  - sheep and goats together in the fold (one herder takes the small stock out),
--   cattle - cows, their calves and a bull in the byre yard,
--   swine  - sows, their litters and a boar in the sty, rooting at the woods' edge.
-- Each animal is a persistent micro-agent with its own position and action (graze or root, a few
-- steps to fresh ground, lie down to rest or chew the cud, walk, stand in the pen), decided when its
-- last action ends, in the agents' own event queue (strict time order with the people). A herd has
-- no single destination and no places in it (S15/S16, D-114, D-116): by day each animal keeps within
-- its own range of the herder who drove it out (or the ground the herder left it on) - its range,
-- pace, alertness and side its own - grazing its own way, making room, rejoining, following when the
-- herder moves on each in its own time; the young keep to their mothers; at night the pen. People do everything else: drive them out and home, milk the cows and goats, shear,
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
local weather = require("sim.agents.weather")
local danger = require("sim.agents.danger")
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local PENS = { fold = true, byre = true, sty = true }

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
-- D-092). (S24: 128 keys a minute - with 32, the keys above 31 were the next minutes' draws)
local function u(W, s, H, a, now, k)
  -- (S24: v - v % 1 is floor(v) for every finite v, bit for bit - and no C call in the browser's Lua, D-090)
  local day = now / 24
  day = day - day % 1
  if a.rd ~= day then
    a.rd = day
    a.rb = rng.hash(W.seed, rng.S.V3_MAP, s.key * 7 + M.HIDX[H.kind] * 20000 + a.k, day % 4294967296, 500)
  end
  local mn = now * 60
  return rng.fbase(a.rb, ((mn - mn % 1) % 1440) * 128 + k)
end
-- (S24) the seven headings a grazer weighs: q x 0.6 rad about its way
local CQ, SQ = {}, {}
for q = -3, 3 do CQ[q + 4], SQ[q + 4] = dmath.cos(q * 0.6), dmath.sin(q * 0.6) end
local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }

-- Pasture blocks -----------------------------------------------------------------------------
local BS = 8                         -- tiles a block side
M.BS = BS
local function block_of(s, x, y)
  local N = s.N
  local bn = N / BS
  local bx = (x / s.T + N / 2) / BS
  local by = (y / s.T + N / 2) / BS
  bx, by = bx - bx % 1, by - by % 1
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
  -- (S21, R6.5g) the grass browns in heat as the corn withers
  if T > c.heat_t then warm = warm * max(0, 1 - (T - c.heat_t) / c.heat_span) end
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

-- (S24, D-192) The pen of the beast being stepped (M.step sets it for its everyday moves): a fold's, a byre yard's or a
-- sty's hurdles are passed only at its gate - a beast inside bound out (or outside bound in) goes to the gate first, then
-- through it. A flock leaves its fold as a stream, one after another, not through its fence.
local GATE, GATE_IN = nil, false
local inside_pen
local function gate_points(s, pen, a)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  local cx, cy = x0 + (pen.w - 1) * 0.5 * T, y0 + (pen.h - 1) * 0.5 * T
  local dx, dy = pen.dx - cx, pen.dy - cy
  local l = sqrt(dx * dx + dy * dy)
  if l < 1e-6 then return pen.dx, pen.dy, cx, cy end
  dx, dy = dx / l, dy / l
  -- (each a little to its own side of the gate's width; out, a step beyond it - those coming out stood in the gate and
  -- jammed it for the rest)
  local sd = (((a.k + 0.5) * 0.2360679775) % 1 - 0.5) * T * 0.8
  return pen.dx + dx * T * 1.2 - dy * sd, pen.dy + dy * T * 1.2 + dx * sd, pen.dx - dx * T * 1.6 - dy * sd, pen.dy - dy * T * 1.6 + dx * sd
end

-- (S24) Does the straight way from (x1, y1) to (x2, y2) run through pen's ground (its hurdles crossed)?
local function through_pen(s, pen, x1, y1, x2, y2)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  local lo, hi = 0, 1
  local dx, dy = x2 - x1, y2 - y1
  local ax0, ax1, ay0, ay1 = x0 - 0.6 * T, x0 + (pen.w - 0.4) * T, y0 - 0.6 * T, y0 + (pen.h - 0.4) * T
  if dx * dx < 1e-18 then
    if x1 <= ax0 or x1 >= ax1 then return false end
  else
    local ta, tb = (ax0 - x1) / dx, (ax1 - x1) / dx
    if ta > tb then ta, tb = tb, ta end
    if ta > lo then lo = ta end
    if tb < hi then hi = tb end
    if lo >= hi then return false end
  end
  if dy * dy < 1e-18 then
    if y1 <= ay0 or y1 >= ay1 then return false end
  else
    local ta, tb = (ay0 - y1) / dy, (ay1 - y1) / dy
    if ta > tb then ta, tb = tb, ta end
    if ta > lo then lo = ta end
    if tb < hi then hi = tb end
    if lo >= hi then return false end
  end
  return true
end

-- Walk from where it stands to (x, y) if the straight way is open, else as near as it can.
local function walk_to(W, s, a, now, x, y, speed, here)
  local pen = GATE
  local gated = false
  if pen and pen.state == "standing" and pen.kind ~= "camp" and pen.dx then
    local fin, tin = inside_pen(s, pen, a.tx, a.ty), inside_pen(s, pen, x, y)
    -- (out with the herd its gate is shut behind it: only one going in for the night, or kept in, goes in)
    if tin and not fin and not GATE_IN then return false end
    if fin ~= tin then
      local ox, oy, ix, iy = gate_points(s, pen, a)
      local gx, gy = ix, iy
      if not fin then gx, gy = ox, oy end
      if dsq(a.tx - gx) + dsq(a.ty - gy) > dsq(s.T * 0.8) then
        x, y = gx, gy
        a.why = fin and "To the gate" or "In at the gate"
      elseif fin then
        x, y = ox, oy
      else
        x, y = ix, iy
      end
      gated = true
    elseif not fin and through_pen(s, pen, a.tx, a.ty, x, y) then
      -- (its way runs across the fold: round it, by the corner that makes the way shortest)
      local T = s.T
      local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
      local bx, by, bd = nil, nil, 1e18
      for q = 1, 4 do
        local qx = (q == 1 or q == 3) and x0 - 1.6 * T or x0 + (pen.w + 0.6) * T
        local qy = q <= 2 and y0 - 1.6 * T or y0 + (pen.h + 0.6) * T
        if not through_pen(s, pen, a.tx, a.ty, qx, qy) then
          local d = sqrt(dsq(qx - a.tx) + dsq(qy - a.ty)) + sqrt(dsq(x - qx) + dsq(y - qy))
          if d < bd then bx, by, bd = qx, qy, d end
        end
      end
      if not bx then return false end
      x, y = bx, by
    end
  else
    pen = nil
  end
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
    -- (S24: the people's way may cut over a fold; a beast's does not)
    if pen and not gated and (inside_pen(s, pen, x, y) ~= inside_pen(s, pen, a.tx, a.ty) or through_pen(s, pen, a.tx, a.ty, x, y)) then return false end
  end
  local dx, dy = x - a.tx, y - a.ty
  local d = sqrt(dx * dx + dy * dy)
  if d < 1e-4 then return false end
  a.x, a.y = a.tx, a.ty
  a.tx, a.ty = x, y
  if s.snow then speed = speed * weather.walk_k(s) end   -- (S20, R6.5d) wading through snow
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
    -- (S24) a pen's hurdles are passed only at its gate: a grazer does not drift through them
    local pen = GATE
    if pen and pen.state == "standing" and pen.kind ~= "camp" and inside_pen(s, pen, x, y) ~= inside_pen(s, pen, tx, ty) then i = 0 end
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

-- (S24, D-192) What a grazer finds to eat at (x, y): its pasture block's grass where the ground is open (a grazer finds
-- none under the trees - a browser takes their leaves, a pig roots the woods), less on a trodden lane, the stubble and
-- the fallow of a field but never a crop standing on it; -1 where it cannot stand.
local function forage_at(s, a, S, P, x, y)
  local i = mapm.tile_of(s, x, y)
  if i == 0 or s.cost[i] < 0 or s.land[i] ~= 1 then return -1 end
  local b = block_of(s, x, y)
  local g = a.sp == "pig" and (P.wood and P.wood[b] or 0) or (P.grass[b] or 0)
  local k = s.tree[i]
  if k > 0 and s.trees.st[k] == 1 and a.sp ~= "pig" then
    if S.browse then g = max(g * 0.3, S.browse * (P.wood and P.wood[b] or 0)) else g = g * 0.08 end
  end
  local pk = s.plot[i]
  if pk > 0 then
    local pl = s.plots[pk]
    if pl and (pl.stage == "growing" or pl.stage == "ripe") then return 0 end
    g = g * 0.7
  end
  local l = s.lane[i]
  if l == 2 then g = g * 0.3 elseif l == 1 then g = g * 0.65 end
  return g
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

function inside_pen(s, pen, x, y)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  local fx, fy = (x - x0) / T, (y - y0) / T
  return fx > -0.6 and fy > -0.6 and fx < pen.w - 0.4 and fy < pen.h - 0.4
end
M.inside_pen = inside_pen

-- (S22) Fire by its own extent (danger.fire_near: a tree's crown, a building's walls, a field's edges):
-- the nearest burning thing within r of (x, y), its edge's distance, and the way away from all of it.
local fire_near = danger.fire_near

-- The heat of pen b's own fire (0: not burning).
local function pen_heat(s, b)
  local B = s.burn
  for i = 1, #(B or {}) do
    local f = B[i]
    if f.kind == "bld" and f.ref == b.id and not f.out then return f.heat end
  end
  return 0
end
M.pen_heat = pen_heat

-- The herd kept in pen b, or nil.
function M.herd_of_pen(s, b)
  for _, kind in ipairs(M.HERDS) do
    local H = s.herds and s.herds[kind]
    if H and H.pen == b.id then return H end
  end
  return nil
end

-- Beasts crying out at a fire (once a half hour a herd): a sound that carries, waking those near.
local function cry(W, s, H, x, y, now, water)
  if (H.fire_cry or -9) > now - 0.5 then return end
  H.fire_cry = now
  if water then
    -- (S21) shut in a flooded pen
    danger.cue(W, s, x, y, now, { kind = "flood", src = 0, sight = 0, night = 0, sound = C().beast_cry, loud = true,
      why = H.kind == "cattle" and "The cattle are bellowing: the water is in the byre" or "The beasts are crying out: the water is in the pens" })
    return
  end
  danger.cue(W, s, x, y, now, { kind = "fire", src = 0, sight = 0, night = 0, sound = C().beast_cry, loud = true,
    why = H.kind == "cattle" and "The cattle are bellowing: fire at the byre" or "The beasts are crying out: fire by the pens" })
end

-- (S16) How far a herd of n spreads about where it is kept (its beasts grazing calmly keep
-- `stock_room` spacings at least between them and bend away within `stock_bend`).
function M.range(S, n)
  local r = C().stock_range
  return S.sep * (r[1] * sqrt(n) + r[2])
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
-- (S20, exact) The herd's living beasts in ascending order (runtime, never saved): a beast only ever
-- comes alive at the end of the list and a slot never comes back to life, so walking this list visits
-- exactly the living beasts in the order the whole list would - the same sums, the same nearest -
-- without walking every beast the herd ever had (dead entries are dropped now and then).
local LIVE = setmetatable({}, { __mode = "k" })
local function live_list(H)
  local an = H.animals
  local L = LIVE[H]
  if not L then L = { top = 0, dead = 0 }; LIVE[H] = L end
  if L.dead > 8 and L.dead * 4 > #L then
    local j = 0
    for i = 1, #L do
      local k = L[i]
      if an[k].alive then j = j + 1; L[j] = k end
    end
    for i = #L, j + 1, -1 do L[i] = nil end
    L.dead = 0
  end
  for k = L.top + 1, #an do
    if an[k].alive then L[#L + 1] = k end
  end
  L.top = #an
  return L
end
M.live_list = live_list

local function scan(s, H, a, now, x, y, sight, hx, hy, hda)
  local s2 = sight * sight
  local nn, nd2, nx, ny = nil, 1e18, 0, 0
  local ln, lr, cx, cy, cn = 0, 0, 0, 0, 0
  -- (S24, D-192) with (hx, hy, hda): the one ahead - the nearest in sight that is nearer (hx, hy) than hda by half a
  -- spacing (a beast following the herd follows it)
  local ld2, lead, lx, ly = 1e18, nil, 0, 0
  local sep = SP(a.sp).sep
  local ahead = hx and (hda - sep * 0.5) or nil
  -- (S24) the press of those closer than a spacing: away from each, the more the closer (a beast in a crowd makes room
  -- away from all of them, not only from its nearest - stepping from one into another kept the flock a packed ball)
  local sep2, rx, ry = sep * sep, 0, 0
  local an = H.animals
  local inpen = a.act == "pen"
  local grp = a.led or 0
  local L = live_list(H)
  local dead = 0
  for i = 1, #L do
    local b = an[L[i]]
    if not b.alive then dead = dead + 1 end
    if b ~= a and b.alive and (b.led or 0) == grp then
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
        if d2 < sep2 and d2 > 1e-12 then
          local d = sqrt(d2)
          local w = (sep - d) / (sep * d)
          rx, ry = rx - dx * w, ry - dy * w
        end
        if d2 < s2 then
          ln = ln + 1
          if ba == "rest" then lr = lr + 1 end
          if ahead and d2 < ld2 then
            local ex, ey = bx - hx, by - hy
            if ex * ex + ey * ey < ahead * ahead then ld2, lead, lx, ly = d2, b, bx, by end
          end
        end
      end
    end
  end
  if dead > L.dead then L.dead = dead end
  SC.nn, SC.nd, SC.nx, SC.ny = nn, sqrt(nd2), nx, ny
  SC.ln, SC.lrest, SC.cx, SC.cy, SC.cn = ln, lr, cx, cy, cn
  SC.lead, SC.lx, SC.ly = lead, lx, ly
  SC.rx, SC.ry = rx, ry
end
M.scan = scan

-- (S22) Beast a decides again at `at` instead of when its action would end: a walk or a grazing drift
-- is cut where it will be then, along the same line at the same pace (its body still lives the whole
-- time since its action began).
function M.decide_at(W, s, H, a, now, at)
  if a.t1 <= at or a.act == "held" or a.act == "yoked" then return end
  if (a.act == "walk" or a.act == "graze") and a.t1 > a.t0 then a.tx, a.ty = M.pos(a, at) end
  a.ver = a.ver + 1
  a.t1 = at
  M.push(W, at, M.event_id(s, H, a), a.ver)
end

-- (S22) Fire f has caught (or roared up): the beasts that see or smell it - within beast_notice of its
-- edge (more by night: its glow) - look up, each in its own time: the alert sooner, one lying down later,
-- the near sooner than the far. What each then does is its own (M.step).
function M.fire_alarm(W, s, f, now)
  local c = C()
  if not s.herds then return end
  local r = c.beast_notice * (danger.night_at(W, s, now) and 1.3 or 1)
  for _, kind in ipairs(M.HERDS) do
    local H = s.herds[kind]
    if H then
      local L = live_list(H)
      for i = 1, #L do
        local a = H.animals[L[i]]
        if a.alive and (a.led or 0) == 0 and (a.held or 0) == 0 and (a.yoke or 0) == 0 then
          local x, y = M.pos(a, now)
          local d = danger.fire_edge(s, f, x, y)
          if d < r then
            local lying = a.act == "rest" or a.act == "pen"
            local at = now + c.beast_notice_h * (0.3 + 0.9 * (1 - trait(a, T_ALERT))) * (lying and 1.6 or 1) * (0.5 + d / r)
              + 0.01 * u(W, s, H, a, now, 43)
            M.decide_at(W, s, H, a, now, at)
          end
        end
      end
    end
  end
end

-- (S24, D-189) A sudden blow at a point - a god's bolt, a meteor's blast: the flash seen and the crack heard at once by
-- every beast within r out of doors (or in an open fold). Each looks up within a breath or two (the alert first)
-- and bolts its own way from it (M.step); then stands with its head up and comes back to the others in its own time.
-- s.jolt = { { x, y, t, r } } (the last few; a beast answers each once: a.jt).
function M.jolt(W, s, x, y, now, r)
  local c = C()
  if not s.herds then return end
  s.jolt = s.jolt or {}
  local J = s.jolt
  J[#J + 1] = { x = x, y = y, t = now, r = r }
  while #J > 4 do table.remove(J, 1) end
  for _, kind in ipairs(M.HERDS) do
    local H = s.herds[kind]
    if H then
      local L = live_list(H)
      for i = 1, #L do
        local a = H.animals[L[i]]
        if a.alive and (a.held or 0) == 0 and (a.yoke or 0) == 0 then
          local ax, ay = M.pos(a, now)
          local d = sqrt(dsq(ax - x) + dsq(ay - y))
          if d < r then
            local at = now + c.jolt_look * (0.3 + 0.9 * (1 - trait(a, T_ALERT))) * (0.6 + 0.8 * d / r) + 0.0004 * u(W, s, H, a, now, 48)
            M.decide_at(W, s, H, a, now, at)
          end
        end
      end
    end
  end
end

-- (S24) The newest sudden blow beast a has not yet answered, within its reach and still fresh, or nil.
local function jolt_of(s, a, now)
  local J = s.jolt
  if not J then return nil end
  for q = #J, 1, -1 do
    local j = J[q]
    if now - j.t < C().jolt_h and (a.jt or -1) < j.t and dsq(a.tx - j.x) + dsq(a.ty - j.y) < j.r * j.r then return j end
  end
  return nil
end

-- (S22) Beast a of herd H dies (fire, lightning): a carcass where it fell; its owners on the map
-- remember it (`what` says how).
function M.dies(W, s, H, a, t, cause, what)
  local c = C()
  a.alive, a.died, a.cause = false, t, cause
  a.health = 0
  a.ver = a.ver + 1
  H.n_alive = max(0, (H.n_alive or 1) - 1)
  local x, y = M.pos(a, t)
  a.x, a.y, a.tx, a.ty = x, y, x, y
  s.acc.stock_died = (s.acc.stock_died or 0) + 1
  if cause == "fire" then s.acc.stock_burnt = (s.acc.stock_burnt or 0) + 1 else s.acc["stock_" .. cause] = (s.acc["stock_" .. cause] or 0) + 1 end
  s.wildlife = s.wildlife or { carcasses = {} }
  s.wildlife.carcasses = s.wildlife.carcasses or {}
  s.wildlife.carcasses[#s.wildlife.carcasses + 1] = { x = x, y = y, sp = a.sp, t = t, meat = (c.stock[a.sp] and c.stock[a.sp].meat or 5) * 0.5, by = cause }
  local hh = W.S.v3.households[a.owner or 0]
  local noun = a.sp == "cattle" and "cattle" or (a.sp == "sheep" and "sheep" or a.sp .. "s")
  for _, id in ipairs(hh and hh.members or {}) do
    local q = W.S.v3.people[id]
    if q and q.alive and not q.at and q.sett == s.id and (q.beast_lost or -1) ~= floor(t) then
      q.beast_lost = floor(t)
      require("sim.agents.people").remember(q, t, sfmt(what, noun))
    end
  end
end

-- (S22) The flames of burning thing f lived from t for dt: any beast within them (its edge's reach,
-- more as it burns hotter) - shut in a burning pen, trapped against a burning field - is burnt: health
-- lost by the heat, dead at nothing.
function M.scorch(W, s, f, t, dt)
  local c = C()
  if not s.herds then return end
  local reach = c.fire_scorch + c.fire_scorch_heat * f.heat
  local pad = reach + 0.6 + (f.r or 0)   -- (a quick test by the middle first: a field's or a house's size)
  for _, kind in ipairs(M.HERDS) do
    local H = s.herds[kind]
    if H then
      local L = live_list(H)
      for i = 1, #L do
        local a = H.animals[L[i]]
        if a.alive and a.act ~= "held" and dsq(a.tx - f.x) + dsq(a.ty - f.y) < pad * pad then
          local x, y = M.pos(a, t)
          if danger.fire_edge(s, f, x, y) < reach then
            a.health = (a.health or 1) - c.fire_beast * f.heat * dt
            if a.health <= 0 then
              local b = f.kind == "bld" and s.buildings[f.ref]
              M.dies(W, s, H, a, t, "fire", b and PENS[b.kind] and ("One of our %s burnt in the " .. b.kind) or "One of our %s was caught in the fire")
            end
          end
        end
      end
    end
  end
end

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

-- (S22) Is beast a shut in its pen - inside it with the gate closed? Not while its herd is out for the
-- day (the gate stands open: a beast grazing over the fold's ground is not penned) nor once someone has
-- opened it (H.loose).
local function shut_in(s, H, pen, a, now)
  return pen and pen.state == "standing" and pen.kind ~= "camp" and not H.out and inside_pen(s, pen, a.tx, a.ty)
    and (H.loose or -99) < now - C().loose_h
end

-- (S22) A beast's own safe distance from a fire (the timid farther).
function M.fire_safe(a) return C().beast_safe * (0.75 + 0.6 * (1 - trait(a, T_BOLD))) end

-- (S22) Running from the fire its own way: away from all that burns about it (danger.fire_near's way
-- out), bent to its own side and a little each time it looks again, a stretch at a time, never toward
-- another fire; at a run, at its own pace.
local function flee_fire(W, s, H, a, now, S, f, fd, px, py)
  local c = C()
  local pl = sqrt(px * px + py * py)
  local hx, hy
  if pl > 1e-9 then
    hx, hy = px / pl, py / pl
  else
    hx, hy = a.tx - f.x, a.ty - f.y
    local l = sqrt(hx * hx + hy * hy)
    if l < 1e-6 then hx, hy, l = 1, 0, 1 end
    hx, hy = hx / l, hy / l
  end
  local bend = (trait(a, T_SIDE) - 0.5) * c.beast_fan + (u(W, s, H, a, now, 40) - 0.5) * 0.5
  local run = min(M.fire_safe(a) - fd + S.sep * (1 + u(W, s, H, a, now, 41)), c.beast_flee_hop)
  for q = 0, 6 do
    local ang = bend + (q == 0 and 0 or ((q % 2 == 1) and 1 or -1) * 0.5 * floor((q + 1) / 2))
    local ca, sa = cos(ang), sin(ang)
    local ex, ey = hx * ca - hy * sa, hy * ca + hx * sa
    local tx, ty = a.tx + ex * run, a.ty + ey * run
    local _, d2 = fire_near(s, tx, ty, c.beast_fire_see)
    if not d2 or d2 > fd + run * 0.25 then
      a.hx, a.hy = ex, ey
      a.why = "Running from the fire"
      if walk_to(W, s, a, now, tx, ty, S.speed * c.beast_run * (0.85 + 0.3 * trait(a, T_PACE))) then return true end
    end
  end
  return false
end

-- (S22) Does the straight way from (x0, y0) to (x1, y1) pass within r of anything burning?
local function way_burns(s, x0, y0, x1, y1, r)
  for q = 1, 3 do
    local f = q * 0.25
    if fire_near(s, x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, r) then return true end
  end
  return false
end
M.way_burns = way_burns

-- (S22) A place in pen `pen` on its side away from fire f (each beast its own, about it).
local function pen_away(W, s, H, a, pen, f, now)
  local T = s.T
  local x0, y0 = mapm.ixy_xy(s, pen.ix0, pen.iy0)
  local mx, my = x0 + (pen.w - 1) * 0.5 * T, y0 + (pen.h - 1) * 0.5 * T
  local dx, dy = mx - f.x, my - f.y
  local l = sqrt(dx * dx + dy * dy)
  if l < 1e-6 then dx, dy, l = 1, 0, 1 end
  local fx = 0.5 + 0.4 * dx / l + (u(W, s, H, a, now, 46) - 0.5) * 0.35
  local fy = 0.5 + 0.4 * dy / l + (u(W, s, H, a, now, 47) - 0.5) * 0.35
  return pen_spot(s, pen, max(0, min(1, fx)), max(0, min(1, fy)))
end

-- The end of animal a's action at time now: its effects, then the next action.
function M.step(W, s, H, a, now)
  local c = C()
  local S = SP(a.sp)
  GATE, GATE_IN = nil, false
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
      local eat = min(a.hunger, (S.root * (0.6 + 0.4 * wood) + mast) * dt * (s.snow and weather.reach(s, "pig") or 1))
      a.hunger = a.hunger - eat
    else
      local g = P.grass[b] or 0
      if s.snow then g = g * weather.reach(s, a.sp) end   -- (S20, R6.5d) the grass under the snow
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
  if a.hunger >= 1 then a.health = max(0, a.health - c.stock_starve * dt)   -- (S20: days, not hours, to fail)
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
  -- one of a drove (S15, R4; S16 as an individual): it goes with its drover wherever they go on
  -- this map, by the same rules as a herd with its herder (its own distance, side and pace, after
  -- them when they move on, grazing about them when they stop, making room in the drove)
  local drover
  if (a.led or 0) ~= 0 then
    local q = W.S.v3.people[a.led]
    if q and q.alive and q.trip and (q.at or q.sett) == s.id then
      drover = q
    else
      -- (the drover is gone: it goes back to the herd by itself)
      a.led = 0
    end
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
  local gone = pen and pen.state ~= "standing"   -- (S20: burnt or fallen in: kept where it stood)
  local age = (now - a.born) / 1152
  -- (S21, R6.5e) flood water: a beast standing in it wades out toward ground above the water; one shut
  -- in its pen stays, bleating (it drowns there by the hour unless someone opens it: flood.lua)
  if s.fl and M.flood and not drover then
    local dep = M.flood.depth_i(s, mapm.tile_of(s, a.tx, a.ty))
    if dep > 0.08 then
      local shut = shut_in(s, H, pen, a, now)
      if shut then
        if dep > (c.fl_beast_d[a.sp] or 0.5) * 0.5 then cry(W, s, H, a.tx, a.ty, now, true) end
        a.why = "Shut in the flooded " .. pen.kind .. ", bleating"
        stand(a, now, "pen", 0.15)
        return
      end
      local x, y = M.flood.high_ground(W, s, a.tx, a.ty, 0.15)
      if x then
        a.why = "Wading out of the flood water"
        if walk_to(W, s, a, now, x, y, S.speed * 1.4) then return end
        -- (no way round the water: a beast wades and swims straight for the higher ground, a hop at a time)
        local dx, dy = x - a.tx, y - a.ty
        local d = sqrt(dx * dx + dy * dy)
        if d > 1e-4 then
          local hop = min(d, S.sep * 4)
          a.x, a.y = a.tx, a.ty
          a.tx, a.ty = a.tx + dx / d * hop, a.ty + dy / d * hop
          a.act, a.t0, a.t1 = "walk", now, now + hop / (S.speed * 0.6)
          a.why = "Swimming out of the flood water"
          return
        end
      end
    end
  end
  -- (S24) a sudden blow near (a bolt, a blast): away from it at a gallop, each its own way - the nearer and the more
  -- timid, the farther - then standing with its head up before it goes back to the others (a beast shut in a byre or
  -- a sty presses to the far side; one held, yoked or led stays where it is held)
  local jt = not drover and jolt_of(s, a, now)
  if jt then
    a.jt = jt.t
    local dx, dy = a.tx - jt.x, a.ty - jt.y
    local d = sqrt(dx * dx + dy * dy)
    if d < 1e-6 then dx, dy, d = (a.hx or 1), (a.hy or 0), 1 end
    local hx, hy = dx / d, dy / d
    local fright = (0.55 + 0.9 * (1 - trait(a, T_BOLD))) * (0.35 + 0.65 * (1 - d / jt.r))
    local run = c.jolt_run * fright * (0.8 + 0.4 * u(W, s, H, a, now, 49))
    local bend = (trait(a, T_SIDE) - 0.5) * c.beast_fan + (u(W, s, H, a, now, 50) - 0.5) * 0.7
    local pen0 = s.buildings[H.pen or 0]
    if not shut_in(s, H, pen0, a, now) then
      for q = 0, 6 do
        local ang = bend + (q == 0 and 0 or ((q % 2 == 1) and 1 or -1) * 0.5 * floor((q + 1) / 2))
        local ca, sa = cos(ang), sin(ang)
        local ex, ey = hx * ca - hy * sa, hy * ca + hx * sa
        a.hx, a.hy = ex, ey
        a.why = "Bolting from the blow"
        a.startled = now
        if walk_to(W, s, a, now, a.tx + ex * run, a.ty + ey * run, S.speed * c.jolt_speed * (0.85 + 0.3 * trait(a, T_PACE))) then
          cry(W, s, H, a.tx, a.ty, now, true)
          return
        end
      end
    end
    a.why = "Startled: head up, trembling"
    a.startled = now
    stand(a, now, a.act == "pen" and "pen" or "rest", 0.04 + 0.08 * u(W, s, H, a, now, 51))
    return
  end
  if a.act == "walk" and a.why == "Bolting from the blow" then
    -- (the run done: it stands a while, looking about, before anything else)
    a.why = "Startled: standing, looking about"
    stand(a, now, "rest", c.jolt_stand * (0.4 + 0.9 * (1 - trait(a, T_BOLD))) * (0.7 + 0.6 * u(W, s, H, a, now, 52)))
    return
  end
  -- (S20, R6.5b; S22) fire, seen and smelt by its own extent (a tree's crown, a building's walls, a
  -- field's edges): a beast out of its pen runs from it its own way until the fire is its own safe
  -- distance off; one shut in its pen stays (a fold's hurdles give way once the fold is well alight; a
  -- byre or a sty must be opened: H.loose), pressed to the side away from the fire, crying out - a sound
  -- that wakes people
  local burning = s.burn and #s.burn > 0 and not drover
  if burning then
    local f, fd, px, py = fire_near(s, a.tx, a.ty, c.beast_fire_see)
    local ph = pen and pen.state == "standing" and pen_heat(s, pen) or 0
    if f or ph > 0 then
      local shut = shut_in(s, H, pen, a, now) and not (pen.kind == "fold" and ph > 0.5)
      if f and fd < c.beast_cry_r then cry(W, s, H, a.tx, a.ty, now) end
      if not shut then
        if f and fd < M.fire_safe(a) and flee_fire(W, s, H, a, now, S, f, fd, px, py) then return end
        -- (beyond its own safe distance: it keeps away from the fire - below - where it got to)
      elseif ph > 0 then
        a.why = "Shut in the burning " .. pen.kind .. ", crying out"
        stand(a, now, "pen", 0.1)
        return
      elseif f and fd < c.beast_cry_r then
        local tx, ty = pen_away(W, s, H, a, pen, f, now)
        a.why = "Shut in the " .. pen.kind .. " with the fire near, crying out"
        if dsq(tx - a.tx) + dsq(ty - a.ty) > dsq(s.T * 0.4) and walk_to(W, s, a, now, tx, ty, S.speed * 0.8, true) then return end
        stand(a, now, "pen", 0.08 + 0.08 * u(W, s, H, a, now, 44))
        return
      end
    end
  end
  -- (S24) from here on its everyday moves: in and out of its pen at the gate
  GATE = not drover and pen or nil
  -- the young keep to their mothers
  local mom = not drover and a.mother ~= 0 and H.animals[a.mother]
  if age < S.young_years and mom and mom.alive then
    local mx, my = M.pos(mom, now)
    local d = sqrt(dsq(mx - a.tx) + dsq(my - a.ty))
    local near = S.sep * 0.6
    if d > near then
      local ang = u(W, s, H, a, now, 1) * 6.2832
      local ox, oy = near * 0.5 * cos(ang), near * 0.5 * sin(ang)
      a.why = "After its mother"
      -- (S24) where she goes it goes - in at the gate after her when she is in the pen for the night
      GATE_IN = true
      if walk_to(W, s, a, now, mx + ox, my + oy, S.speed * 1.3) then return end
      GATE_IN = false
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
  local p = drover or M.herder(W, s, H)
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
  -- (S22) keeping away from a fire: while anything burns within beast_keep of where it is kept (the
  -- herder, the ground it was left on, the pen) it does not go there - it stays about where it got to
  -- (lying down there for the night), grazing among the others there; once that place is clear of the
  -- fire it goes back to the herd
  local keep = false
  if burning and not shut_in(s, H, pen, a, now) then
    local kx, ky = cx, cy
    if inpen then kx, ky = pen.cx or pen.dx, pen.cy or pen.dy end
    if kx and (fire_near(s, kx, ky, c.beast_keep) or way_burns(s, a.tx, a.ty, kx, ky, c.beast_keep * 0.75)) then
      keep = true
      if not a.kx then a.kx, a.ky = a.tx, a.ty end
      inpen, p = false, nil
      cx, cy = a.kx, a.ky
      if night then
        a.why = "Lying out away from the fire"
        stand(a, now, "rest", 0.3 + 0.4 * u(W, s, H, a, now, 45))
        return
      end
    end
  end
  if not keep and a.kx then a.kx, a.ky = nil, nil end
  local x, y = a.tx, a.ty
  local pace = 0.85 + 0.3 * trait(a, T_PACE)
  if inpen then
    GATE_IN = true
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
      if pen.kind == "camp" then
        -- (S17: no fold yet at a new place: kept close by its people's camp)
        a.why = night and "To its place by the camp for the night: there is no fold yet" or "Kept close by the camp: there is no fold yet"
      else
        a.why = night and "To its place in the pen for the night" or (inside and "About the pen" or "Into the pen")
      end
      if walk_to(W, s, a, now, tx, ty, S.speed * pace * (inside and 0.5 or 1.2)) then return end
    end
    a.why = pen.kind == "camp" and (night and "Lying by the camp for the night" or "Kept by the camp: there is no fold yet")
      or (gone and "Kept where its pen stood: it is gone")
      or (night and "Lying in the pen for the night" or "Waiting in the pen")
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
  if drover then
    n = 0
    local L = drover.trip.led
    for i = 1, L and #L or 0 do
      local b = H.animals[L[i]]
      if b and b.alive and b.led == drover.id then n = n + 1 end
    end
    local qx, qy = M.person_pos(drover, now)
    if night and drover.act ~= "walk" and dsq(qx - a.tx) + dsq(qy - a.ty) < dsq(min(M.range(S, n) * 1.2, c.drove_gather * 0.6)) then
      -- (the drover stopped for the night: it lies down near them; S20: only when it is near them -
      -- one left behind in the dark comes up first)
      a.why = "Lying down for the night by the drover"
      stand(a, now, "rest", 1 + 1.5 * u(W, s, H, a, now, 10))
      return
    end
  end
  local R = M.range(S, n)
  local Ri = R * (0.45 + 0.9 * trait(a, T_BOLD))
  local dax, day = cx - x, cy - y
  local da = sqrt(dax * dax + day * day)
  local led = p and p.act == "walk"
  local hx, hy = a.hx or 1, a.hy or 0
  local sick = (a.sick or 0) > 0
  -- a dog close by: away from it, toward the herd (how a dog brings a straggler back)
  local gx, gy
  if not drover then gx, gy = dog_near(s, x, y, now, S.sep * 1.8) end
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
  -- (S24) still inside its fold with the herd out: it goes out after them by the gate (making room or grazing inside it
  -- held the ones at the gate back - the rest jammed behind them)
  local still_in = GATE and not GATE_IN and GATE.state == "standing" and GATE.kind ~= "camp" and GATE.dx and inside_pen(s, GATE, x, y)
  if da > Ri * 1.5 or (led and da > Ri * 0.45) or still_in then
    -- left behind, or the herder is moving on (a moving flock draws closer together than a grazing
    -- one): after them, to its own side of the herd. (S24, D-192) On the move it follows the one ahead of it - the
    -- nearest in sight nearer the herder - a spacing behind and a little to its own side: the flock strings out behind
    -- its leaders, each after another, the front ones after the herder (it moved as a ball before).
    local ux, uy = dax / da, day / da
    local side = trait(a, T_SIDE) - 0.5
    local stop = Ri * 0.6
    local tx = cx - ux * stop - uy * side * Ri * 0.8
    local ty = cy - uy * stop + ux * side * Ri * 0.8
    local trail = false
    if led and not drover then
      scan(s, H, a, now, x, y, S.sep * 5, cx, cy, da)
      local b = SC.lead
      if b then
        -- (after where the one ahead is going - not where it stands: one close behind it stepped back to keep its
        -- distance from it and the flock milled about instead of going on)
        local lx, ly = SC.lx, SC.ly
        if b.act == "walk" and b.t1 > now then lx, ly = b.tx, b.ty end
        local ex, ey = x - lx, y - ly
        local el = sqrt(ex * ex + ey * ey)
        local gap = S.sep * (0.9 + 0.7 * u(W, s, H, a, now, 60))
        if el > gap then
          ex, ey = ex / el, ey / el
          local sd = side * S.sep * 1.4
          tx, ty = lx + ex * gap - ey * sd, ly + ey * gap + ex * sd
          trail = true
        else
          -- at its heels already: a mouthful while the one ahead moves on
          a.why = "Waiting on the one ahead"
          graze_drift(s, a, now, hx, hy, 0, S.sep / max(1e-6, S.speed) * (0.6 + 0.6 * u(W, s, H, a, now, 61)), true)
          return
        end
      end
    end
    -- (still in the fold: out by its gate first - its way to the others, cut to a hop, ended inside the fold, and it walked
    -- up and down inside it)
    if still_in then tx, ty = gate_points(s, GATE, a); trail = false end
    local dx, dy = tx - x, ty - y
    local d = sqrt(dx * dx + dy * dy)
    local hop = S.hop * pace * (0.8 + 0.5 * u(W, s, H, a, now, 5)) * (da > R * 2 and 1.6 or 1)
    if d > hop and not still_in then tx, ty = x + dx / d * hop, y + dy / d * hop end
    if d > 1e-6 then a.hx, a.hy = dx / d, dy / d end
    if drover then
      a.why = led and ("Following the drover" .. (drover.trip.to ~= s.id and (" to " .. (W.S.v3.settlements[drover.trip.to] or s).name) or ""))
        or "Catching up with the drover"
    else
      a.why = led and (trail and "Following the one ahead" or "Following the herder") or (p and "Catching up with the herder" or "Back to the others")
    end
    if walk_to(W, s, a, now, tx, ty, S.speed * pace * (da > Ri * 3 and 1.5 or 1.15)) then return end
  end
  scan(s, H, a, now, x, y, S.sep * 4)
  local nn = SC.nn
  if nn and SC.nd < S.sep * c.stock_room and not sick then
    -- too close: room to graze (away from those pressing on it - S24: all of them, not only its nearest - a little
    -- along its own way)
    local ex, ey = SC.rx, SC.ry
    local el = sqrt(ex * ex + ey * ey)
    if el < 1e-6 then ex, ey = x - SC.nx, y - SC.ny; el = sqrt(ex * ex + ey * ey) end
    if el < 1e-6 then ex, ey, el = -hy, hx, 1 end
    ex, ey = ex / el + hx * 0.3, ey / el + hy * 0.3
    local l = max(1e-6, sqrt(ex * ex + ey * ey))
    local st = S.sep * (0.6 + 0.4 * u(W, s, H, a, now, 19))
    a.why = "Making room to graze"
    if walk_to(W, s, a, now, x + ex / l * st, y + ey / l * st, S.speed * pace * 0.6, true) then return end
  end
  if SC.ln == 0 and SC.cn > 0 and not (keep and fire_near(s, SC.cx / SC.cn, SC.cy / SC.cn, c.beast_keep)) then
    -- nobody in sight: toward the others (S22: never toward a fire)
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
  -- (S24, D-192) its own next mouthful: a few places about its way (ahead, to either side, a little behind) weighed by
  -- what grows there (forage_at: open grass, not under the trees, never a standing crop), not onto the nearest other,
  -- within its own range of where the herd is kept, and a little by its own whim - each beast to its own patch
  if da > 1e-6 and da > Ri then hx, hy = hx + dax / da * 0.6, hy + day / da * 0.6 end
  local l0 = sqrt(hx * hx + hy * hy)
  if l0 > 1e-6 then hx, hy = hx / l0, hy / l0 else hx, hy = 1, 0 end
  if P then
    local bsc, bx, by = -1e9, hx, hy
    local dmax = S.sep * 1.1
    for q = -3, 3 do
      -- (three draws from one hash's bits: ten for its turn, ten for its step, twelve for its whim; the turn's cos and sin by
      -- their series - |e| <= 0.175, exact to 1e-9 - plain arithmetic, the same in every runtime)
      local x0 = u(W, s, H, a, now, 64 + q)
      local b1 = x0 * 1024
      b1 = b1 - b1 % 1
      local b2 = x0 * 1048576
      b2 = (b2 - b2 % 1) % 1024
      local b3 = x0 * 4294967296
      b3 = (b3 - b3 % 1) % 4096
      local e = (b1 / 1024 - 0.5) * 0.35
      local e2 = e * e
      local ce = 1 - e2 * (0.5 - e2 * (1 / 24 - e2 / 720))
      local se = e * (1 - e2 * (1 / 6 - e2 * (1 / 120 - e2 / 5040)))
      local c0, s0 = CQ[q + 4], SQ[q + 4]
      local ca, sa = c0 * ce - s0 * se, s0 * ce + c0 * se
      local ex, ey = hx * ca - hy * sa, hy * ca + hx * sa
      local st = S.sep * (0.5 + 0.7 * (b2 / 1024))
      local px, py = x + ex * st, y + ey * st
      local g = forage_at(s, a, S, P, px, py)
      if g >= 0 then
        local sc = g + ca * 0.1 + (b3 / 4096) * 0.12 + (ex * SC.rx + ey * SC.ry) * 0.3
        if nn then
          local dn = sqrt(dsq(px - SC.nx) + dsq(py - SC.ny))
          if dn < dmax then sc = sc - (dmax - dn) / dmax * 1.4 end
        end
        local dc = sqrt(dsq(px - cx) + dsq(py - cy))
        if dc > Ri then sc = sc - (dc - Ri) / max(1e-6, Ri) * 1.2 end
        if sc > bsc then bsc, bx, by = sc, ex, ey end
      end
    end
    hx, hy = bx, by
  else
    local k = (u(W, s, H, a, now, 8) - 0.5) * 1.4
    hx, hy = hx - hy * k, hy + hx * k
    local l = sqrt(hx * hx + hy * hy)
    if l > 1e-6 then hx, hy = hx / l, hy / l else hx, hy = 1, 0 end
  end
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
  local dur = S.graze_h * ((led or drover) and 0.25 or 1) * (0.5 + 0.8 * u(W, s, H, a, now, 11))
  -- (S16: the reason as it stands for this beast - hungry, out at the edge on its own, or grazing
  -- among the others)
  if keep then
    a.why = a.sp == "pig" and "Rooting away from the fire" or "Grazing away from the fire"
  elseif a.hunger > 0.6 then
    a.why = a.sp == "pig" and "Hungry: rooting hard" or (S.browse and "Hungry: browsing whatever it can reach" or "Hungry: grazing hard, head down")
  elseif da > Ri then
    a.why = a.sp == "pig" and "Rooting its own way at the edge of the others" or "Grazing its own way at the edge of the herd, turning back"
  else
    a.why = a.sp == "pig" and "Rooting among the others" or (S.browse and "Browsing among the others" or "Grazing among the others")
  end
  if drover then a.why = a.why .. (led and " a mouthful on the way" or " while the drover is busy") end
  graze_drift(s, a, now, hx, hy, sick and 0 or S.sep * (a.sp == "pig" and 0.5 or 0.9) * pace * dur, dur,
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

-- Droving (S15, R4) ------------------------------------------------------------------------

-- Is beast a one household hh could sell alive now: its own, grown, sound, not at work, and one the
-- hay will not winter (marked for killing) and not too old to be worth more alive than dead.
function M.sellable(a, hh_id, now)
  if not a.alive or a.owner ~= hh_id or a.ox or (a.held or 0) ~= 0 or (a.yoke or 0) ~= 0 or (a.led or 0) ~= 0 then return false end
  local S = SP(a.sp)
  local age = (now - a.born) / 1152
  if age < S.young_years * 1.5 or age > S.old_years - 1 or a.health < 0.6 or (a.sick or 0) > 0 then return false end
  return a.cull == true
end

-- The beasts of species sp household hh would sell now, up to n, the youngest first.
function M.for_sale(H, hh_id, sp, now, n)
  local out = {}
  for _, a in ipairs(H.animals) do
    if a.sp == sp and M.sellable(a, hh_id, now) then out[#out + 1] = a end
  end
  table.sort(out, function(x, y) if x.born ~= y.born then return x.born > y.born end return x.k < y.k end)
  for i = #out, n + 1, -1 do out[i] = nil end
  return out
end

-- A drover takes beast a by the halter (S15) and lets it go again with a word and a stick: from now
-- on it is one of the drove and goes with them by itself (S16), noticing them move off in its
-- own time.
function M.halter(W, s, H, a, p, now)
  local tr = p.trip
  tr.led = tr.led or {}
  a.tx, a.ty = M.pos(a, now)
  a.x, a.y = a.tx, a.ty
  a.led, a.held = p.id, 0
  a.act = "rest"
  a.why = "Taken for the drove by " .. (p.name or "the drover")
  tr.led[#tr.led + 1] = a.k
  a.ver = a.ver + 1
  a.t0, a.t1 = now, now + 0.03 + 0.06 * (1 - trait(a, T_ALERT))
  M.push(W, a.t1, M.event_id(s, H, a), a.ver)
end

-- The drover sets off on a new step: each beast of the drove on map s notices in its own time.
function M.rouse_drove(W, s, p, kind, now)
  local L, H = M.led_by(s, p, kind)
  for _, a in ipairs(L) do
    if a.act == "graze" or a.act == "rest" then
      local at = now + 0.01 + 0.06 * (1 - trait(a, T_ALERT)) + 0.03 * u(W, s, H, a, now, 21)
      if a.t1 > at then
        a.ver = a.ver + 1
        a.t1 = at
        M.push(W, at, M.event_id(s, H, a), a.ver)
      end
    end
  end
end

-- How far the drove's last beast on map s is from its drover now.
function M.drove_spread(s, p, kind, now)
  local L = M.led_by(s, p, kind)
  local px, py = M.person_pos(p, now)
  local far = 0
  for _, a in ipairs(L) do
    local x, y = M.pos(a, now)
    local d = sqrt(dsq(x - px) + dsq(y - py))
    if d > far then far = d end
  end
  return far, #L
end

-- The beasts p leads on map s now (from the trip's list), and their herd.
function M.led_by(s, p, kind)
  local tr = p.trip
  local H = s.herds and s.herds[kind]
  local out = {}
  if not tr or not tr.led or not H then return out, H end
  for _, k in ipairs(tr.led) do
    local a = H.animals[k]
    if a and a.alive and a.led == p.id then out[#out + 1] = a end
  end
  return out, H
end

-- The drover takes the road: the beasts they lead leave map s with them (each the same beast, in
-- the trip's party until the road's end; a marker stays in the herd's list in its place).
function M.leave_map(W, s, p, kind)
  local L, H = M.led_by(s, p, kind)
  local tr = p.trip
  tr.party = tr.party or {}
  for _, a in ipairs(L) do
    H.animals[a.k] = { k = a.k, sp = a.sp, sex = a.sex, born = a.born, owner = a.owner, alive = false, away = p.id, ver = a.ver + 1,
      x = a.tx, y = a.ty, tx = a.tx, ty = a.ty, t0 = a.t0, t1 = a.t1, act = "away" }
    a.act, a.ver = "away", a.ver + 1
    tr.party[#tr.party + 1] = a
    H.n_alive = max(0, (H.n_alive or 1) - 1)
  end
  tr.led = {}
end

-- At the road's end, on map s at (gx, gy): the party's beasts join herd `kind` of s, still led.
function M.join_map(W, s, p, kind, gx, gy, now)
  local tr = p.trip
  local H = s.herds and s.herds[kind]
  if not tr or not tr.party or #tr.party == 0 then return end
  if not H then
    -- (no pen here for them: they stay with the party)
    return
  end
  tr.led = tr.led or {}
  for i, a in ipairs(tr.party) do
    local k = #H.animals + 1
    a.k, a.mother, a.last_young, a.slot = k, 0, nil, k - 1
    -- (off the road with the drover: about them at the map's edge, each where it came up)
    local ang = trait(a, T_SIDE) * 6.2832
    local r = SP(a.sp).sep * (0.6 + 1.2 * trait(a, T_BOLD))
    a.x, a.y = gx + r * cos(ang), gy + r * sin(ang)
    local ti = mapm.tile_of(s, a.x, a.y)
    if ti == 0 or s.cost[ti] < 0 then a.x, a.y = gx, gy end
    a.tx, a.ty = a.x, a.y
    a.act, a.led, a.held, a.yoke = "rest", p.id, 0, 0
    a.why = "Off the road with " .. (p.name or "the drover")
    a.ver = a.ver + 1
    a.t0, a.t1 = now, now + 0.02 + 0.05 * (1 - trait(a, T_ALERT))
    H.animals[k] = a
    H.n_alive = (H.n_alive or 0) + 1
    tr.led[#tr.led + 1] = k
    M.push(W, a.t1, M.event_id(s, H, a), a.ver)
  end
  tr.party = {}
end

-- Let a led beast go (sold, or home again): it stands where it is and goes to the herd by itself.
function M.release(W, s, H, a, now, why)
  a.tx, a.ty = M.pos(a, now)
  a.x, a.y = a.tx, a.ty
  a.led, a.act, a.why = 0, "rest", why
  a.ver = a.ver + 1
  a.t0, a.t1 = now, now + 0.03
  M.push(W, a.t1, M.event_id(s, H, a), a.ver)
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
  s.acc.culled_plan = (s.acc.culled_plan or 0) + M.plan_pig_keep(W, s)
  s.acc.culled_hunger = (s.acc.culled_hunger or 0) + M.plan_hunger_cull(W, s, now)
  s.acc.culled_failing = (s.acc.culled_failing or 0) + M.plan_failing(W, s, "failing", true)
  local year = floor((day + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  for hi = 1, #M.HERDS do
    local H = s.herds[M.HERDS[hi]]
    if H then M.herd_daily(W, s, H, day, doy, year, now, push) end
  end
  M.herd_worth(W, s)
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
  -- sickness spreads in a pen: the share already sick (and, at half, those carrying it unseen - S23: a
  -- beast catches it days before it shows, and is sold, droven or penned with others meanwhile), crowding,
  -- wet days. A sickness carries its cause from beast to beast (`sick_ev`: the god's, when the god seeded it)
  local sick, inc, tload, tev = 0, 0, 0, nil
  for i = 1, m do
    local a = L[ord[i]]
    if (a.sick or 0) > 0 then
      sick = sick + 1
      if a.sick_ev then tload, tev = tload + 1, tev or a.sick_ev end
    elseif (a.sick_inc or 0) > 0 then
      inc = inc + 1
      if a.sick_ev then tload, tev = tload + 0.5, tev or a.sick_ev end
    end
  end
  H.sick = sick
  local load = sick + 0.5 * inc
  local dead_ev
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
      -- sickness: catching it (the sick in the pen, crowding, wet, hunger), carrying it unseen for a few
      -- days, showing it, recovering or not
      local v = rng.float(W.seed, rng.S.V3_LIFE, key, day, 30)
      if (a.sick or 0) > 0 then
        a.sick = a.sick - 1
        if v < c.sick_recover then a.sick = 0 end
        if a.sick <= 0 then a.sick_ev = nil end
      elseif (a.sick_inc or 0) > 0 then
        a.sick_inc = a.sick_inc - 1
        if a.sick_inc <= 0 then
          a.sick_inc = nil
          a.sick = c.sick_days
          H.sick = H.sick + 1
          s.acc.stock_sick = (s.acc.stock_sick or 0) + 1
        end
      else
        local base = c.sick_base * (1 + crowd) * (1 + wet) * (a.hunger > 0.7 and 2 or 1)
        local pz = base + c.sick_spread * load / max(1, m)
        if v < pz then
          a.sick_inc = c.sick_inc[1] + floor((c.sick_inc[2] - c.sick_inc[1] + 1) * rng.float(W.seed, rng.S.V3_LIFE, key, day, 36))
          -- (caught from beasts that bear a cause, when they made more of the pressure than the land's own)
          if tev and c.sick_spread * tload / max(1, m) > base then a.sick_ev = tev end
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
        -- (S23) a death of a sickness that bears a cause: the herd's deed of the day (below)
        if a.cause == "sickness" and a.sick_ev then
          dead_ev = dead_ev or {}
          local e = dead_ev[a.sick_ev]
          if not e then e = { n = 0, owner = a.owner or 0, sp = a.sp, x = a.tx, y = a.ty }; dead_ev[a.sick_ev] = e; dead_ev[#dead_ev + 1] = a.sick_ev end
          e.n = e.n + 1
        end
        s.acc["died_" .. a.sp] = (s.acc["died_" .. a.sp] or 0) + 1
      elseif a.sex == "f" and not a.cull and age >= S.adult_years and a.bred ~= year and M.breeding(S, doy) and M.has_sire(H, a, now) then
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
  -- (S23) the beasts a caused sickness killed today: one deed of the herd for each cause (the owners' loss,
  -- the cause carried: the god's sickness, traced in the chain of what the god did)
  if dead_ev then
    local deeds = require("sim.agents.deeds")
    for i = 1, #dead_ev do
      local ev = dead_ev[i]
      local e = dead_ev[ev]
      local d = deeds.add(W, { kind = "sickness", t = now, at = s.id, x = e.x, y = e.y, by = 0, by_hh = 0, by_s = 0, to = 0,
        to_hh = e.owner, to_s = s.id, what = e.sp, n = e.n, fu = e.n * (SP(e.sp).meat or 5), harm = true, event = ev })
      deeds.learn(W, s, d, "the sickness in the herd", 0, 0, now)
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
-- (S20, R6.5d) The same reckoning while snow lies on the grass: the stack must feed every grazing
-- beast in full (`full` of a ration, not 0.6) for `days`; those marked `mark` ("snow").
-- `core`: the breeding dams are kept back (killed only if they fail: plan_failing).
function M.plan_cull(W, s, days, full, mark, core)
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
  local per = (days or c.hay_days) * c.sheep_hunger * 24 * (full or 0.6)
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
    elseif core and a.sex == "f" and age >= S.adult_years and age < S.old_years - 1 then
      -- (the dams are the herd to come)
    elseif a.sex == "m" and not sires[a.sp] and age > S.adult_years and age < S.old_years - 1 then
      sires[a.sp] = true      -- (one good sire of each kind is kept)
    else
      a.cull = mark or true
      over = over - S.se
      n = n + 1
    end
  end
  return n
end

-- (S20, R6) Each household's beasts (their meat on the hoof, FU, `hh.herd_now`) and those it lost
-- against its will (`hh.herd_lost`: sickness, hunger, wolves, fire, thieves - never a beast killed or
-- sold; fading herd_lost_fade a day): a household that has lost much of its herd has lost its living -
-- restocking is read from it (conflict.weigh_at).
local OWN = {}
local LOSS = { sickness = true, hunger = true, wolves = true, fire = true, sea = true, lightning = true }
function M.lose(W, hid, a)
  local hh = W.S.v3.households[hid or 0]
  if hh then hh.herd_lost = (hh.herd_lost or 0) + SP(a.sp).meat end
end
function M.herd_worth(W, s)
  local c = C()
  local V = W.S.v3
  local now = W.hour
  for _, hid in ipairs(s.households) do OWN[hid] = 0 end
  for hi = 1, #M.HERDS do
    local H = s.herds[M.HERDS[hi]]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive then
          if OWN[a.owner or 0] then OWN[a.owner] = OWN[a.owner] + SP(a.sp).meat end
        elseif not a.lost_n and a.died and now - a.died <= 30 and LOSS[a.cause or ""] then
          a.lost_n = true
          M.lose(W, a.owner, a)
        end
      end
    end
  end
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local v = OWN[hid]
    OWN[hid] = nil
    if hh then
      hh.herd_now = v
      if hh.herd_lost then
        hh.herd_lost = hh.herd_lost * c.herd_lost_fade
        if hh.herd_lost < 0.5 then hh.herd_lost = nil end
      end
    end
  end
end

-- (S20, R6.5d) The beasts still marked for killing for reason `mark` are kept after all.
function M.unmark(s, mark)
  for _, kind in ipairs({ "flock", "cattle" }) do
    local H = s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive and a.cull == mark then a.cull = nil end
      end
    end
  end
end

-- (S20, R6.5d) Beasts failing for want of feed (starving; for the grazers nothing left in the stack):
-- killed for the larder while they still have flesh on them, marked a.cull = mark. Returns how many.
-- (Run every day for every village: what keeps a herd to the feed there is; before S20 starving
-- beasts simply died within two days.)
-- `core`: the breeding stock is spared (a sow of each household and a boar; the grazers' dams) - the
-- herd to come is fed what there is.
function M.plan_failing(W, s, mark, core)
  local c = C()
  local now = W.hour
  local n = 0
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds[kind]
    if H and (kind == "swine" or (s.hay or 0) < 1) then
      if core and kind == "swine" then
        for k in pairs(M.pig_core(H, now)) do H.animals[k]._core = true end
      elseif core then
        for _, a in ipairs(H.animals) do
          if a.alive and not a.cull then
            local S, age = SP(a.sp), (now - a.born) / 1152
            if a.sex == "f" and age >= S.adult_years and age < S.old_years - 1 then a._core = true end
          end
        end
      end
      for _, a in ipairs(H.animals) do
        if a.alive and not a.cull and not a.ox and not a._core and a.hunger >= 0.9 and a.health < c.snow_fail_health then
          a.cull = mark
          n = n + 1
        end
        a._core = nil
      end
    end
  end
  return n
end

-- (S19) Hunger: a household whose larder would feed it under hunger_cull_days while its village's
-- stores have little to give kills one of its own beasts for the pot - the most dispensable first
-- (a pig, the old, males beyond a sire, the young; the plough ox last of all), one at a time.
-- Marked a.cull = "hunger". Returns how many were marked.
function M.plan_hunger_cull(W, s, now)
  local c = C()
  local V = W.S.v3
  -- (S22: the granary - what a household with an empty larder can draw on - not every household's
  -- larders together: the neighbours' food is not theirs)
  if (s.need and s.need.store_days or 99) >= c.hunger_cull_village then return 0 end
  local n = 0
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
    if home and not (hh.migrate and not hh.migrate.arrived and not hh.migrate.given_up) then
      local st = home.stock
      local food = (st.food or 0) + (st.meals or 0) + (st.smoked or 0) + (st.cheese or 0) + (st.meat or 0) + (st.fish or 0)
      if food < #hh.members * 0.75 * c.hunger_cull_days then
        local marked, best, bs = false, nil, -1e9
        for _, kind in ipairs(M.HERDS) do
          local H = s.herds and s.herds[kind]
          for _, a in ipairs(H and H.animals or {}) do
            if a.alive and a.owner == hid then
              if a.cull then marked = true end
              local S = SP(a.sp)
              local age = (now - a.born) / 1152
              local sc = (a.sp == "pig" and 3 or 0) + (age > S.old_years - 1 and 2 or 0) + (a.sex == "m" and 1.5 or 0)
                + (age < S.young_years and 0.5 or 0) - (a.ox and 10 or 0) + S.meat * 0.01 - a.k * 1e-6
              if (a.held or 0) == 0 and (a.led or 0) == 0 and (a.yoke or 0) == 0 and sc > bs then best, bs = a, sc end
            end
          end
        end
        if best and not marked then
          best.cull = "hunger"
          n = n + 1
        elseif not best and not marked then
          -- (S19) nothing of their own to kill: a neighbour with beasts - kin first, then whoever has
          -- most - kills one for them and brings them the meat
          local people = require("sim.agents.people")
          local donor, ds = nil, 0
          for _, h2 in ipairs(s.households) do
            local d = V.households[h2]
            local dh = d and h2 ~= hid and #d.members > 0 and s.buildings[d.home or 0]
            if dh then
              local own = 0
              for _, kind in ipairs(M.HERDS) do
                local H = s.herds and s.herds[kind]
                for _, a in ipairs(H and H.animals or {}) do
                  if a.alive and a.owner == h2 and not a.ox then own = own + 1 end
                  if a.alive and a.owner == h2 and a.cull_for == hid then own = -1e9 end
                end
              end
              if own > 0 then
                local kin = 0
                local q0 = V.people[hh.members[1]]
                for _, qid in ipairs(d.members) do
                  local q = V.people[qid]
                  if q and q0 and (q.surname == q0.surname or people.is_kin(W, q0, qid)) then kin = 1 end
                end
                local sc = kin * 10 + own - h2 * 1e-6
                if sc > ds then donor, ds = d, sc end
              end
            end
          end
          if donor then
            local db, dbs = nil, -1e9
            for _, kind in ipairs(M.HERDS) do
              local H = s.herds and s.herds[kind]
              for _, a in ipairs(H and H.animals or {}) do
                if a.alive and a.owner == donor.id and not a.cull and not a.ox and (a.held or 0) == 0 and (a.led or 0) == 0 then
                  local S = SP(a.sp)
                  local age = (now - a.born) / 1152
                  local sc = (a.sp == "pig" and 3 or 0) + (age > S.old_years - 1 and 2 or 0) + (a.sex == "m" and 1.5 or 0) + S.meat * 0.01 - a.k * 1e-6
                  if sc > dbs then db, dbs = a, sc end
                end
              end
            end
            if db then
              db.cull, db.cull_for = "hunger", hid
              n = n + 1
            end
          end
        end
      end
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
  local n = 0
  local core = M.pig_core(H, now)
  for _, a in ipairs(H.animals) do
    if a.alive and not a.cull and not core[a.k] and (now - a.born) / 1152 >= 0.3 then
      a.cull = true
      n = n + 1
    end
  end
  return n
end

-- (S20) A household fattens pig_keep growers at a time besides its breeding sow (and the village a
-- boar): a grown pig beyond them (0.3 years or more) is marked for killing - the late litter is not left
-- to become next year's sows. Returns how many were marked.
local KEEP = {}
-- (S20) The pigs kept to breed (a set of k): each household's sow (else its oldest young sow-to-be),
-- and one boar for the village (else the oldest young boar-to-be). Any of them marked for killing is
-- spared.
local CORE, CORE_BEST = {}, {}
function M.pig_core(H, now)
  for k in pairs(CORE) do CORE[k] = nil end
  for k in pairs(CORE_BEST) do CORE_BEST[k] = nil end
  local S = SP("pig")
  local boar, bage = nil, -1
  local yb, ybage = nil, -1
  for _, a in ipairs(H.animals) do
    if a.alive then
      local age = (now - a.born) / 1152
      local o = a.owner or 0
      if a.sex == "f" and age >= 0.3 and age < S.old_years - 1 then
        -- (a grown sow before any young one; among them the oldest)
        local score = (age >= S.adult_years and 10 or 0) + age
        local e = CORE_BEST[o]
        if not e or score > e.score or (score == e.score and a.k < e.k) then CORE_BEST[o] = { k = a.k, score = score } end
      elseif a.sex == "m" and age >= S.adult_years and age < S.old_years and age > bage then boar, bage = a.k, age
      elseif a.sex == "m" and age >= 0.3 and age < S.adult_years and age > ybage then yb, ybage = a.k, age end
    end
  end
  for _, a in ipairs(H.animals) do
    local e = a.alive and CORE_BEST[a.owner or 0]
    if e and e.k == a.k then CORE[a.k] = true end
  end
  local b = boar or yb
  if b then CORE[b] = true end
  for k in pairs(CORE) do
    local a = H.animals[k]
    if a.cull == true then a.cull = nil end
  end
  return CORE
end

function M.plan_pig_keep(W, s)
  local H = s.herds and s.herds.swine
  if not H then return 0 end
  local c = C()
  local now = W.hour
  local S = SP("pig")
  local n = 0
  local core = M.pig_core(H, now)
  -- (the growers, youngest first: the oldest beyond pig_keep are killed - they are fattest)
  for i = #H.animals, 1, -1 do
    local a = H.animals[i]
    if a.alive and not a.cull and not core[a.k] and (now - a.born) / 1152 >= 0.3 then
      local o = a.owner or 0
      KEEP[o] = (KEEP[o] or 0) + 1
      if KEEP[o] > c.pig_keep then a.cull = true; n = n + 1 end
    end
  end
  for _, a in ipairs(H.animals) do KEEP[a.owner or 0] = nil end
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
-- The map's grazing ground (16 x 16 blocks): how open each block is, its grass, its woods.
function M.pasture_init(W, s, u)
  local N = s.N
  local P = { grass = {}, open = {}, wood = {} }
  s.pasture = P
  local nb = (N / BS) * (N / BS)
  for b = 1, nb do
    P.open[b] = openness(s, b)
    P.grass[b] = P.open[b] * (0.5 + 0.3 * u())
    P.wood[b] = woods(s, b)
  end
  return P
end

function M.found(W, s, households, u, doy0)
  local c = C()
  local N = s.N
  s.herds = {}
  M.pasture_init(W, s, u)
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
