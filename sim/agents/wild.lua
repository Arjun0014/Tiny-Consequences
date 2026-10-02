-- Wild animals on a settlement's map as individuals (roadmap v3 R3, D-093). The planet's herds
-- and packs (sim/systems/fauna.lua, W4) stay the ecology: how many there are, where their range
-- and dens lie, their condition, their births and deaths, their moves. On a settlement's map the
-- animals of those groups that are here now are individuals - each with its group (macro id), a
-- position, an action (graze, rest, walk, flee, chase, eat), its mother - walking in from the
-- map's edge on the side of their range when the group comes this way and out again when it
-- leaves, never appearing or vanishing in view.
--   herds   each herd's share on the map: its count x its time on the map's cells (its range's
--           weights x how much of each cell the map covers). By day the band keeps to its own
--           ground (a grassy block at the woods' edge, away from the village), each animal at its
--           place in a loose spread (larger than livestock's), the young at their mothers'; they
--           take flight from people who come close, and from wolves.
--   packs   hunt by night. At dusk a pack whose territory reaches the map may come (its time on
--           the map's cells, more when it is hungry and the village keeps beasts): its wolves
--           walk in from the side of their den, together, to prey - the weakest of a wild band,
--           or, when the pack is hungry and wild prey scarce, the beasts in a pen nobody watches -
--           chase one animal (it flees; young, weak, sick and penned ones are caught more often),
--           and kill it or give up. A kill is a real animal dead where it fell: a wild one is taken
--           from its herd at once (and counted as part of the herd's predation for the month, so
--           nothing is killed twice); a beast of the village feeds the pack and is its owner's loss.
--           The wolves eat at the carcass and are gone before dawn.
-- s.wildlife = { animals = { w }, carcasses = { c }, next_k, pack_night = { [pack id] = day } }
-- w = { k, sp, pred, gid, sex, born, mother, x, y, tx, ty, t0, t1, act, ver, alive, gone, lead,
--   prey ({ kind = "wild" | herd, k }), flee_from }
-- Events: id = stock.ID0 + settlement id x 100000 + 3 x 20000 + k.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local nav = require("sim.agents.nav")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")

local M = {}
M.HI = 3
local floor, max, min, sqrt, abs, cos, sin = math.floor, math.max, math.min, math.sqrt, math.abs, dmath.cos, dmath.sin
local function C() return cfgmod.V3 end
local function WP(sp) return cfgmod.V3.wild[sp] end
M.WP = WP
local HERB = { "deer", "reindeer", "antelope", "boar" }
local PRED = { "wolf", "cat" }
M.HERB, M.PRED = HERB, PRED
local GOLD = 2.39996323
local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }

function M.event_id(s, w) return stock.ID0 + s.id * 100000 + M.HI * 20000 + w.k end

-- The agents' event queue (set by sim/agents/init.lua: bob forbids the circular require).
M.push = function() end

-- Runtime caches (derived from the state; never saved).
local COVER = setmetatable({}, { __mode = "k" })
local BYK = setmetatable({}, { __mode = "k" })

-- The animal numbered k (an index by number over s.wildlife.animals, rebuilt on a miss).
local function byk(s, k)
  local Wd = s.wildlife
  local t = BYK[Wd]
  if t then
    local i = t[k]
    local w = i and Wd.animals[i]
    if w and w.k == k then return w end
  end
  t = {}
  for i, a in ipairs(Wd.animals) do t[a.k] = i end
  BYK[Wd] = t
  local i = t[k]
  return i and Wd.animals[i] or nil
end
M.byk = byk

-- How much of each planet cell lies on the map (0..1): its land tiles here / a cell's tiles.
local function cover(W, s)
  local cv = COVER[s]
  if cv then return cv end
  cv = {}
  local cnt = {}
  for i = 1, s.N * s.N do
    if s.land[i] == 1 then
      local c = s.cell_of[i]
      cnt[c] = (cnt[c] or 0) + 1
    end
  end
  local per = 4 * math.pi * mapm.R * mapm.R / W.n / (s.T * s.T)
  for c, n in pairs(cnt) do cv[c] = min(1, n / per) end
  COVER[s] = cv
  return cv
end
M.cover = cover

-- The map's cells and their neighbours (a group whose centre or den is elsewhere cannot range
-- here: herds range one ring, packs two, so dens are tested against the second ring too).
local NEAR = setmetatable({}, { __mode = "k" })
local function near(W, s)
  local nr = NEAR[s]
  if nr then return nr end
  local cv = cover(W, s)
  local g = W.grid
  local r1, r2 = {}, {}
  for c in pairs(cv) do r1[c] = true end
  for c in pairs(cv) do
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do r1[g.nbr[b + k]] = true end
  end
  for c in pairs(r1) do
    r2[c] = true
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do r2[g.nbr[b + k]] = true end
  end
  nr = { r1, r2 }
  NEAR[s] = nr
  return nr
end

-- A group's share of its time on the map: sum over its range of weight x cover.
local function presence(W, s, t)
  local cv = cover(W, s)
  local tw = t.w
  local sh = 0
  for k = 1, t.L do
    local x = cv[t[k]]
    if x then sh = sh + tw[k] * x end
  end
  return sh
end

-- The land tiles of the map's border ring (3 tiles in), walkable: where animals come and go.
local EDGE = setmetatable({}, { __mode = "k" })
local function edge_ring(s)
  local e = EDGE[s]
  if e then return e end
  e = {}
  local N = s.N
  for ix = 3, N - 2 do e[#e + 1] = 2 * N + ix; e[#e + 1] = (N - 3) * N + ix end
  for iy = 4, N - 3 do e[#e + 1] = (iy - 1) * N + 3; e[#e + 1] = (iy - 1) * N + N - 2 end
  local land = {}
  for _, i in ipairs(e) do
    if s.land[i] == 1 and s.cost[i] >= 0 and s.riv[i] <= 0 then land[#land + 1] = i end
  end
  EDGE[s] = land
  return land
end

-- Where animals of a group whose range or den lies at planet cell c come onto the map or leave
-- it: the border land nearest that direction (a map with no land at its border: the deepest woods
-- far from the village).
local function edge_toward(W, s, c)
  local g = W.grid
  local x, y = mapm.local_of(s, g.X[c], g.Y[c], g.Z[c])
  x, y = x or 1, y or 0
  local ring = edge_ring(s)
  local best, bd = 0, 1e18
  for q = 1, #ring do
    local tx, ty = mapm.tile_xy(s, ring[q])
    local d = dsq(tx - x) + dsq(ty - y)
    if d < bd then best, bd = ring[q], d end
  end
  if best > 0 then return mapm.tile_xy(s, best) end
  -- (no land at the border: out of the woods farthest from the square)
  local P = s.pasture
  local bn = s.N / stock.BS
  local bb, bs = 0, -1e9
  for b = 1, bn * bn do
    local bx, by = stock.block_xy(s, b)
    local sc = (P and P.wood and P.wood[b] or 0) + 0.3 * sqrt(bx * bx + by * by)
    if P and (P.open[b] or 0) > 0.05 and sc > bs then bb, bs = b, sc end
  end
  if bb == 0 then return 0, 0 end
  local bx, by = stock.block_xy(s, bb)
  local fx, fy = mapm.free_spot(s, bx, by, 6)
  return fx or bx, fy or by
end
M.edge_toward = edge_toward

-- The border land nearest local (x, y).
function M.border_near(s, x, y)
  local ring = edge_ring(s)
  local best, bd = 0, 1e18
  for q = 1, #ring do
    local tx, ty = mapm.tile_xy(s, ring[q])
    local d = dsq(tx - x) + dsq(ty - y)
    if d < bd then best, bd = ring[q], d end
  end
  if best > 0 then return mapm.tile_xy(s, best) end
  return x, y
end

local function u(W, s, w, now, k)
  local day = floor(now / 24)
  if w.rd ~= day then
    w.rd = day
    w.rb = rng.hash(W.seed, rng.S.V3_MAP, s.key * 7 + 60000 + w.k, day % 4294967296, 600)
  end
  return rng.fbase(w.rb, (floor(now * 60) % 1440) * 32 + k)
end

-- (actions that move: walking, flight, the chase, leaving, and grazing its way slowly along, S15)
local MOVES = { walk = true, flee = true, chase = true, leave = true, graze = true }
function M.pos(w, t)
  if w.t1 <= w.t0 or not MOVES[w.act] then return w.tx, w.ty end
  local f = (t - w.t0) / (w.t1 - w.t0)
  if f <= 0 then return w.x, w.y elseif f >= 1 then return w.tx, w.ty end
  return w.x + (w.tx - w.x) * f, w.y + (w.ty - w.y) * f
end

local function go_to(s, w, now, x, y, speed, act, around, here)
  local i = mapm.tile_of(s, x, y)
  if i == 0 or s.cost[i] < 0 then
    -- (a step about where it stands that runs into a wall or water is not taken: S15)
    if here then return false end
    local fx, fy = mapm.free_spot(s, x, y, 3)
    if not fx then return false end
    x, y = fx, fy
  end
  local i0, i1 = mapm.tile_of(s, w.tx, w.ty), mapm.tile_of(s, x, y)
  if i0 > 0 and i1 > 0 and i0 ~= i1 and not nav.los(s, i0, i1, 3.1) then
    -- (a river or a building in the way: only an animal leaving the map finds its way round;
    -- the others go half as far, or stay this side)
    if act == "leave" or around then
      w.mv = w.mv or { x = {}, y = {}, cum = {} }
      local path = nav.path(s, w.tx, w.ty, x, y, 0, w.mv)
      if not path or path.n < 2 then return false end
      x, y = path.x[2], path.y[2]
    else
      x, y = (w.tx + x) * 0.5, (w.ty + y) * 0.5
      i1 = mapm.tile_of(s, x, y)
      if i1 == 0 or s.cost[i1] < 0 or (i1 ~= i0 and not nav.los(s, i0, i1, 3.1)) then return false end
    end
  end
  local dx, dy = x - w.tx, y - w.ty
  local d = sqrt(dx * dx + dy * dy)
  if d < 1e-4 then return false end
  w.x, w.y = w.tx, w.ty
  w.tx, w.ty = x, y
  w.act, w.t0, w.t1 = act or "walk", now, now + d / speed
  return true
end

local function stand(w, now, act, dur)
  w.x, w.y = w.tx, w.ty
  w.act, w.t0, w.t1 = act, now, now + max(0.05, dur)
end

-- A new wild animal at (x, y) (on the nearest open ground).
local function add(W, s, o)
  local i = mapm.tile_of(s, o.x, o.y)
  if i == 0 or s.cost[i] < 0 or s.land[i] ~= 1 then
    local fx, fy = mapm.free_spot(s, o.x, o.y, 5)
    if fx then o.x, o.y = fx, fy end
  end
  local Wd = s.wildlife
  local k = Wd.next_k
  Wd.next_k = k + 1
  local now = W.S.v3.now or W.hour
  local w = { k = k, sp = o.sp, pred = o.pred or false, gid = o.gid, sex = o.sex or "f", born = o.born or now, mother = o.mother or 0,
    x = o.x, y = o.y, tx = o.x, ty = o.y, t0 = now, t1 = now + 0.02, act = "walk", ver = 0, alive = true, gone = false, lead = o.lead or 0 }
  Wd.animals[#Wd.animals + 1] = w
  return w
end

-- The band's ground today: a grassy block at the woods' edge, away from the village, on the side
-- of the herd's range (each herd its own).
local function band_ground(W, s, gid, cell, day)
  local P = s.pasture
  local bn = s.N / stock.BS
  local g = W.grid
  local cx, cy = mapm.local_of(s, g.X[cell], g.Y[cell], g.Z[cell])
  cx, cy = cx or 0, cy or 0
  local best, bs = 0, -1e9
  local jit = rng.base(W.seed, rng.S.V3_MAP, gid * 64 + day % 4096)
  for b = 1, bn * bn do
    local x, y = stock.block_xy(s, b)
    local r = sqrt(x * x + y * y)
    local open = P and P.open[b] or 0.5
    local wood = P and P.wood and P.wood[b] or 0.3
    if r > C().wild_keep and open > 0.1 then
      local grass = P and P.grass[b] or open
      local d = sqrt(dsq(x - cx) + dsq(y - cy))
      local sc = grass + 0.6 * min(wood, 1 - wood) - 0.25 * d + 0.3 * rng.fbase(jit, b)
      if sc > bs then best, bs = b, sc end
    end
  end
  if best == 0 then return nil end
  return stock.block_xy(s, best)
end

-- Daily (the settlement's midnight): which groups are on the map and how many of each.
function M.daily(W, s, day, push)
  local A = W.S.wild
  if not A then return end
  local c = C()
  s.wildlife = s.wildlife or { animals = {}, carcasses = {}, next_k = 1, pack_night = {}, ground = {} }
  local Wd = s.wildlife
  local fauna = require("sim.systems.fauna")
  local now = W.hour
  -- compact: the dead and departed are forgotten after a day
  local keep = {}
  for _, w in ipairs(Wd.animals) do
    if w.alive or now - (w.died or now) < 24 then keep[#keep + 1] = w end
  end
  Wd.animals = keep
  for i = #Wd.carcasses, 1, -1 do
    if now - Wd.carcasses[i].t > c.carcass_hours then table.remove(Wd.carcasses, i) end
  end
  -- herds here now, and how many of each (the share of their time on the map's cells)
  local local_n = {}
  for _, w in ipairs(Wd.animals) do
    if w.alive and not w.pred and w.act ~= "leave" then local_n[w.gid] = (local_n[w.gid] or 0) + 1 end
  end
  local want = {}
  local total = 0
  local nr1 = near(W, s)[1]
  for i = 1, A.nh do
    local sh = 0
    if nr1[A.h_cell[i]] then sh = presence(W, s, fauna.herd_range(W, A.h_cell[i])) end
    if sh > 0 then
      local e = A.h_n[i] * sh * c.wild_seen
      local n = floor(e + rng.float(W.seed, rng.S.V3_MAP, A.h_id[i], day, 610))
      if n > 0 then want[#want + 1] = { i = i, n = n, sh = sh }; total = total + n end
    end
  end
  -- (no more than the map's share of animals the pool allows: the most present herds first)
  table.sort(want, function(a, b) if a.sh ~= b.sh then return a.sh > b.sh end return A.h_id[a.i] < A.h_id[b.i] end)
  local room = c.wild_max
  local target = {}
  for _, q in ipairs(want) do
    local n = min(q.n, room)
    room = room - n
    target[A.h_id[q.i]] = { i = q.i, n = n }
  end
  -- arrivals walk in from the side of the herd's range; the surplus walks out that way (in the
  -- order above: the arrivals' keys must not depend on a hash table's order, which differs between
  -- LuaJIT and the browser's Lua, S14)
  for wi = 1, #want do
    local i = want[wi].i
    local gid = A.h_id[i]
    local tg = target[gid]
    local have = local_n[gid] or 0
    local sp = HERB[A.h_sp[i]] or "deer"
    local gx, gy = band_ground(W, s, gid, A.h_cell[i], day)
    Wd.ground[gid] = gx and { gx, gy, A.h_cell[i] } or nil
    -- (they come in at the border nearest their ground: on the side of their range, not through
    -- the village)
    local ex, ey
    if gx then ex, ey = M.border_near(s, gx, gy) else ex, ey = edge_toward(W, s, A.h_cell[i]) end
    for q = have + 1, tg.n do
      local key = gid * 131 + q
      local young = rng.float(W.seed, rng.S.V3_MAP, key, day, 611) < 0.25
      local w = add(W, s, { sp = sp, gid = gid, sex = rng.float(W.seed, rng.S.V3_MAP, key, day, 612) < 0.6 and "f" or "m",
        born = now - (young and 0.3 or 2 + 4 * rng.float(W.seed, rng.S.V3_MAP, key, day, 613)) * 1152,
        x = ex + (rng.float(W.seed, rng.S.V3_MAP, key, day, 614) - 0.5) * 0.1, y = ey + (rng.float(W.seed, rng.S.V3_MAP, key, day, 615) - 0.5) * 0.1 })
      w.t1 = now + 0.05 + 0.3 * rng.float(W.seed, rng.S.V3_MAP, key, day, 616)
      push(W, w.t1, M.event_id(s, w), w.ver)
      s.acc.wild_in = (s.acc.wild_in or 0) + 1
    end
  end
  for _, w in ipairs(Wd.animals) do
    if w.alive and not w.pred and w.act ~= "leave" then
      local tg = target[w.gid]
      local i = fauna.find_herd(W, w.gid)
      if not tg or not i then
        w.leaving = true
      elseif (local_n[w.gid] or 0) > tg.n then
        w.leaving = true
        local_n[w.gid] = local_n[w.gid] - 1
      end
    end
  end
end

-- At dusk: does a pack hunt on this map tonight? Its wolves walk in from the side of its den.
function M.hourly(W, s, now)
  local A = W.S.wild
  local Wd = s.wildlife
  if not A or not Wd then return end
  -- (wolves on the map this hour, for the dogs: D-105)
  local nw = 0
  for _, w in ipairs(Wd.animals) do if w.alive and w.pred then nw = nw + 1 end end
  s.wolves_n = nw
  local c = C()
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  local ss = s.sunset or 18
  if not (lh >= ss + 0.4 and lh < ss + 1.4) then return end
  local day = floor(now / 24)
  local fauna = require("sim.systems.fauna")
  local cv = cover(W, s)
  local beasts = 0
  stock.each(s, function() beasts = beasts + 1 end)
  local wildn = 0
  for _, w in ipairs(Wd.animals) do if w.alive and not w.pred then wildn = wildn + 1 end end
  local PN = W.S.v3.pack_night
  if not PN then PN = {}; W.S.v3.pack_night = PN end
  for i = 1, A.np do
    local id = A.p_id[i]
    if PN[id] ~= day and Wd.pack_night[id] ~= day and A.p_n[i] > 0 and near(W, s)[2][A.p_cell[i]] then
      local t = fauna.territory(W, A.p_cell[i])
      local sh = presence(W, s, t)
      if sh > 0 then
        Wd.pack_night[id] = day
        local cond = A.p_cond[i]
        -- (hungry wolves come to a village's beasts when the wild prey here is scarce)
        local lure = (cond < c.wolf_hungry and beasts > 0 and wildn < c.wolf_wild_scarce) and (1 + 2 * (c.wolf_hungry - cond) / c.wolf_hungry) or 1
        local p = min(0.9, sh * lure * c.wolf_visit)
        if rng.float(W.seed, rng.S.V3_MAP, id, day, 620) < p then
          PN[id] = day
          local sp = PRED[A.p_sp[i]] or "wolf"
          local ex, ey = edge_toward(W, s, A.p_cell[i])
          local n = min(A.p_n[i], c.pack_max)
          local lead = 0
          for q = 1, n do
            local w = add(W, s, { sp = sp, pred = true, gid = id, sex = q <= 2 and (q == 1 and "m" or "f") or "m",
              born = now - (1 + 3 * rng.float(W.seed, rng.S.V3_MAP, id * 7 + q, day, 621)) * 1152,
              x = ex + (q % 3 - 1) * 0.02, y = ey + (floor(q / 3) - 1) * 0.02, lead = lead })
            if q == 1 then lead = w.k; w.lead = 0 end
            w.why = cond < c.wolf_hungry and "Hungry: come down to hunt" or "Come to hunt"
            w.t1 = now + 0.02 * q
            M.push(W, w.t1, M.event_id(s, w), w.ver)
          end
          s.acc.wolf_nights = (s.acc.wolf_nights or 0) + 1
          s.wolves_n = (s.wolves_n or 0) + n
        end
      end
    end
  end
end

-- Interrupt wild animal w now (it was chased, or a hunter's shot): it decides again at once.
function M.interrupt(W, s, w, now)
  w.ver = w.ver + 1
  w.x, w.y = M.pos(w, now)
  w.tx, w.ty = w.x, w.y
  w.t0, w.t1 = now, now
  M.push(W, now, M.event_id(s, w), w.ver)
end

-- Someone (a person or a wolf) close enough to set a grazer running.
local function threat(W, s, w, now, k)
  local c = C()
  local Wd = s.wildlife
  k = k or 1
  -- (S15: the nearest within k times the distance that sets it running, and how near as a share
  -- of that distance squared: <= 1 runs, more is only watchful)
  local bx, by, best = nil, nil, k * k
  local wx, wy = w.tx, w.ty
  -- wolves on the hunt (none on the map: no look)
  if (s.wolves_n or 0) > 0 then
    local an = Wd.animals
    local ra = c.wild_alert_wolf * c.wild_alert_wolf
    for q = 1, #an do
      local o = an[q]
      if o.pred and o.alive and (o.act == "chase" or o.act == "walk") then
        local f = (dsq(o.tx - wx) + dsq(o.ty - wy)) / ra
        if f < best then bx, by, best = o.tx, o.ty, f end
      end
    end
  end
  -- people about in the woods and fields: where each began what they are doing (state: the
  -- same after a load), hunters where they are now
  local V = W.S.v3
  local r2 = c.wild_alert * c.wild_alert
  local mem = s.members
  for i = 1, #mem do
    local p = V.people[mem[i]]
    if p.alive and p.inside == 0 then
      local px, py = p.x, p.y
      if p.objk == "hunt" then
        px, py = stock.person_pos(p, now)
        -- (a hunter creeping up is seen later, the more skilled the later)
        local h = 1 - 0.5 * (p.sk_hunt or 0.2)
        local f = (dsq(px - wx) + dsq(py - wy)) / (r2 * h * h)
        if f < best then bx, by, best = px, py, f end
      elseif p.act ~= "carried" and p.act ~= "sleep" then
        local dx, dy = px - wx, py - wy
        local f = (dx * dx + dy * dy) / r2
        if f < best then bx, by, best = px, py, f end
      end
    end
  end
  return bx, by, best
end

-- (S15) The others of w's band round (x, y) now: its nearest (and where), how many within `sight`
-- and how many of those lie resting, the middle and count of the whole band here.
local BS_ = { nn = nil, nd = 0, nx = 0, ny = 0, ln = 0, lrest = 0, cx = 0, cy = 0, cn = 0 }
-- (runtime: each group's animals, from s.wildlife.animals, rebuilt when that list changes)
local GROUPS = setmetatable({}, { __mode = "k" })
local function group_of(s, gid)
  local an = s.wildlife.animals
  local g = GROUPS[an]
  if not g or g.n ~= #an then
    g = { n = #an }
    for i = 1, #an do
      local b = an[i]
      local L = g[b.gid]
      if not L then L = {}; g[b.gid] = L end
      L[#L + 1] = b
    end
    GROUPS[an] = g
  end
  return g[gid] or {}
end
M.group_of = group_of
local function band_scan(s, w, now, x, y, sight)
  local s2 = sight * sight
  local nn, nd2, nx, ny = nil, 1e18, 0, 0
  local ln, lr, cx, cy, cn = 0, 0, 0, 0, 0
  local an = group_of(s, w.gid)
  for i = 1, #an do
    local b = an[i]
    if b ~= w and b.alive and not b.leaving then
      local bx, by = b.tx, b.ty
      if b.act ~= "graze" and MOVES[b.act] and b.t1 > b.t0 then bx, by = M.pos(b, now) end
      cx, cy, cn = cx + bx, cy + by, cn + 1
      local dx, dy = bx - x, by - y
      local d2 = dx * dx + dy * dy
      if d2 < nd2 then nn, nd2, nx, ny = b, d2, bx, by end
      if d2 < s2 then
        ln = ln + 1
        if b.act == "rest" then lr = lr + 1 end
      end
    end
  end
  BS_.nn, BS_.nd, BS_.nx, BS_.ny = nn, sqrt(nd2), nx, ny
  BS_.ln, BS_.lrest, BS_.cx, BS_.cy, BS_.cn = ln, lr, cx, cy, cn
end

-- w decides again at `at` (later than now) instead of when its action would end: a walk is cut
-- where it will be then, along the same line at the same pace.
local function decide_at(W, s, w, now, at)
  if w.t1 <= at then return end
  if MOVES[w.act] and w.t1 > w.t0 then
    local x, y = M.pos(w, now)
    local xa, ya = M.pos(w, at)
    w.x, w.y, w.tx, w.ty, w.t0 = x, y, xa, ya, now
  end
  w.ver = w.ver + 1
  w.t1 = at
  M.push(W, at, M.event_id(s, w), w.ver)
end

-- (S15) One of the band takes flight from (fx, fy): those of its band near enough to see it run
-- take fright too, each when it notices (the alert sooner, the far later), and run from the same
-- danger; each in turn sets off those near it.
function M.alarm(W, s, w, now, fx, fy)
  local c = C()
  local S = WP(w.sp)
  local r = S.sep * c.wild_alarm_seps
  local r2 = r * r
  local x, y = M.pos(w, now)
  -- the band will not graze where it was frightened today: its ground moves off away from the
  -- danger (once a while, the first to run)
  local gr = s.wildlife.ground and s.wildlife.ground[w.gid]
  if gr and not w.pred and now - (gr.scared or -99) > 0.5 then
    gr.scared = now
    local dx, dy = gr[1] - fx, gr[2] - fy
    local d = sqrt(dx * dx + dy * dy)
    if d > 1e-6 then
      local nx, ny = gr[1] + dx / d * c.wild_shy, gr[2] + dy / d * c.wild_shy
      local i = mapm.tile_of(s, nx, ny)
      if i > 0 and s.land[i] == 1 and sqrt(nx * nx + ny * ny) > c.wild_keep then gr[1], gr[2] = nx, ny end
    end
  end
  local L = group_of(s, w.gid)
  for _, b in ipairs(L) do
    if b ~= w and b.alive and not b.pred and not b.leaving and b.act ~= "flee"
      and not b.flee_x and now - (b.fled or -99) > 0.3 then
      local bx, by = M.pos(b, now)
      local d2 = dsq(bx - x) + dsq(by - y)
      if d2 < r2 then
        b.flee_x, b.flee_y = fx, fy
        decide_at(W, s, b, now, now + 0.004 + 0.03 * (1 - stock.trait(b, stock.T_ALERT)) + 0.02 * sqrt(d2) / r)
      end
    end
  end
end

-- The prey a pack's leader picks: the weakest of a wild band (the young, the old), or a beast in a
-- pen nobody watches when the pack is hungry. Returns { kind = "wild" | herd kind, k } and where.
local function pick_prey(W, s, lead, now)
  local c = C()
  local A = W.S.wild
  local Wd = s.wildlife
  local best, bs, bx, by = nil, -1e9, 0, 0
  for _, w in ipairs(Wd.animals) do
    if w.alive and not w.pred and w.act ~= "leave" then
      local d = sqrt(dsq(w.tx - lead.tx) + dsq(w.ty - lead.ty))
      local weak = (now - w.born) / 1152 < 0.5 and 1 or 0
      local sc = 1 + weak - d * 0.8
      if sc > bs then best, bs, bx, by = { kind = "wild", k = w.k }, sc, w.tx, w.ty end
    end
  end
  local i = require("sim.systems.fauna").find_pack(W, lead.gid)
  local wildn = 0
  for _, w in ipairs(Wd.animals) do if w.alive and not w.pred then wildn = wildn + 1 end end
  local hungry = i and (A.p_cond[i] < c.wolf_starving or (A.p_cond[i] < c.wolf_hungry and wildn < c.wolf_wild_scarce))
  if hungry and s.herds then
    for _, kind in ipairs(stock.HERDS) do
      local H = s.herds[kind]
      if H and not H.out and not M.watched(W, s, H) then
        local pen = s.buildings[H.pen]
        -- (a dog lying at the pen, a watch asleep by it: the pack is warier there, D-105)
        local wary = 0
        for _, d in ipairs(s.dogs or {}) do
          if d.alive and dsq(d.tx - pen.dx) + dsq(d.ty - pen.dy) < 0.04 then wary = wary + 0.8 end
        end
        for gi = 1, #(s.guards or {}) do
          local g = W.S.v3.people[s.guards[gi] or 0]
          if g and g.alive and g.objk == "guard" and dsq(g.x - pen.cx) + dsq(g.y - pen.cy) < 0.06 then wary = wary + 0.8 end
        end
        for _, a in ipairs(H.animals) do
          if a.alive then
            local S = stock.SP(a.sp)
            local small = (a.sp == "cattle" and (now - a.born) / 1152 > S.young_years * 2) and -3 or 0
            local d = sqrt(dsq(pen.cx - lead.tx) + dsq(pen.cy - lead.ty))
            local sc = 1.2 + small + ((now - a.born) / 1152 < S.young_years and 0.5 or 0) - d * 0.5 + (1 - A.p_cond[i]) * 1.5 - wary
            if sc > bs then best, bs, bx, by = { kind = kind, k = a.k }, sc, a.tx, a.ty end
          end
        end
      end
    end
  end
  return best, bx, by
end

-- Is a pen watched (someone outdoors and awake close by)? A watch asleep by it is not: the wolves
-- may come, and wake them (D-105).
function M.watched(W, s, H)
  local pen = s.buildings[H.pen]
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.inside == 0 and p.act ~= "sleep" and not p.at then
      if dsq(p.x - pen.cx) + dsq(p.y - pen.cy) < 0.06 then return true end
    end
  end
  return false
end

-- The pack turns away (D-105): every wolf of it gives up the night's hunting here and leaves.
local function pack_flee(W, s, w, now, by)
  for _, q in ipairs(s.wildlife.animals) do
    if q.alive and q.pred and q.gid == w.gid then
      q.prey, q.done = nil, true
      M.interrupt(W, s, q, now)
    end
  end
  s.acc.wolves_driven = (s.acc.wolves_driven or 0) + 1
  if by and (by.drove_t or -99) < now - 6 then
    by.drove_t = now
    require("sim.agents.people").remember(by, now, "Drove the wolves away from the pen in the night")
  end
end
M.pack_flee = pack_flee

-- Who stands up to wolves at a pen now: a person awake and outdoors close to the wolf.
local function defender(W, s, w, now)
  local c = C()
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and not p.at and p.inside == 0 and p.act ~= "sleep" and p.act ~= "carried" then
      local px, py = stock.person_pos(p, now)
      if dsq(px - w.tx) + dsq(py - w.ty) < (c.wolf_fear * c.wolf_fear) then return p end
    end
  end
  return nil
end

local function prey_obj(s, pr)
  if not pr then return nil end
  if pr.kind == "wild" then return byk(s, pr.k) end
  local H = s.herds and s.herds[pr.kind]
  return H and H.animals[pr.k], H
end

-- The kill: the animal dead where it fell, a carcass; the macro herd loses it (banked as part of
-- its month's predation), or the village loses a beast (the pack is fed).
local function kill(W, s, wolf, pr, now)
  local c = C()
  local A = W.S.wild
  local fauna = require("sim.systems.fauna")
  local o, H = prey_obj(s, pr)
  if not o or not o.alive then return false end
  local x, y
  if pr.kind == "wild" then x, y = M.pos(o, now) else x, y = stock.pos(o, now) end
  o.alive, o.died, o.cause = false, now, "wolves"
  local meat
  if pr.kind == "wild" then
    o.ver = o.ver + 1
    local i = fauna.find_herd(W, o.gid)
    if i then fauna.take_from_herd(W, i, 1, true) end
    meat = WP(o.sp).meat
    s.acc.wild_killed_wolves = (s.acc.wild_killed_wolves or 0) + 1
  else
    meat = stock.SP(o.sp).meat
    local pi = fauna.find_pack(W, wolf.gid)
    if pi then fauna.feed_pack(W, pi, meat) end
    s.acc.stock_wolves = (s.acc.stock_wolves or 0) + 1
    s.acc["wolves_" .. o.sp] = (s.acc["wolves_" .. o.sp] or 0) + 1
    -- the owners remember it, and the village is wary of wolves for a while
    local hh = W.S.v3.households[o.owner]
    if hh then
      for _, id in ipairs(hh.members) do
        local q = W.S.v3.people[id]
        if q and q.alive then require("sim.agents.people").remember(q, now, "Wolves killed one of our " .. (o.sp == "cattle" and "calves" or o.sp .. (o.sp == "sheep" and "" or "s")) .. " in the night") end
      end
    end
    s.wolf_alarm = now
    s.wolf_alarm_hh = o.owner
  end
  s.wildlife.carcasses[#s.wildlife.carcasses + 1] = { x = x, y = y, sp = o.sp, t = now, meat = meat * c.carcass_left, by = "wolves" }
  return true, x, y
end

-- The end of wild animal w's action at time now: the next.
function M.step(W, s, w, now)
  local c = C()
  local S = WP(w.sp)
  local A = W.S.wild
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  local night = lh >= (s.sunset or 18) + 0.3 or lh < (s.sunrise or 6) - 0.3
  if w.act == "leave" then
    -- (out of the map's edge: gone from here, still alive in its group)
    w.alive, w.gone, w.died = false, true, now
    return
  end
  if w.pred then
    -- (a dog within hearing of a wolf wakes and barks: D-105)
    for _, dg in ipairs(s.dogs or {}) do
      if dg.alive and dg.act ~= "run" and dg.act ~= "bark" and dsq(dg.tx - w.tx) + dsq(dg.ty - w.ty) < (c.dog_alert * c.dog_alert) then
        M.dogs.interrupt(W, s, dg, now)
      end
    end
    -- wolves: hunt the night, eat, leave before dawn
    local lead = w.lead ~= 0 and byk(s, w.lead) or nil
    if lead and not lead.alive then
      if lead.gone then
        -- (the leader has gone off the map: the pack goes after it)
        w.done = true
        lead = nil
      else
        -- (the leader is dead: the next of the pack leads)
        lead = nil
        for _, q in ipairs(s.wildlife.animals) do
          if q.alive and q.pred and q.gid == w.gid and q.k < w.k and (not lead or q.k < lead.k) then lead = q end
        end
        w.lead = lead and lead.k or 0
      end
    end
    local dawn = lh >= (s.sunrise or 6) - 1.2 and lh < (s.sunset or 18)
    if (dawn and not (lead and lead.act == "leave")) or w.done then
      -- off the map toward the den: the leader first (the others follow it out), then each its own
      -- way to the edge (S15)
      if not lead or lead.gone or w.done then
        local i = require("sim.systems.fauna").find_pack(W, w.gid)
        local ex, ey = edge_toward(W, s, i and A.p_cell[i] or s.cell)
        if lead == nil and w.lead ~= 0 then
          local dx, dy = ex - w.tx, ey - w.ty
          local d = max(1e-6, sqrt(dx * dx + dy * dy))
          local side = (stock.trait(w, stock.T_SIDE) - 0.5) * 0.25
          ex, ey = ex - dy / d * side, ey + dx / d * side
        end
        w.why = dawn and "Away to the den before dawn" or "Nothing more here tonight: away to the den"
        if go_to(s, w, now, ex, ey, S.speed * (0.85 + 0.3 * stock.trait(w, stock.T_PACE)), "leave") then return end
        w.alive, w.gone, w.died = false, true, now
        return
      end
    end
    if w.act == "eat" then
      -- (a while at the carcass, then off)
      w.done = true
      w.why = "Fed: off soon"
      stand(w, now, "rest", 0.1)
      return
    end
    if not lead then
      -- the leader: find prey, close in, chase
      local pr = w.prey
      local o = prey_obj(s, pr)
      if not o or not o.alive then
        pr = pick_prey(W, s, w, now)
        w.prey = pr
        o = prey_obj(s, pr)
      end
      if not o then
        -- nothing to hunt here tonight
        w.done = true
        w.why = "Nothing to hunt here tonight"
        stand(w, now, "rest", 0.2)
        return
      end
      local ox, oy
      if pr.kind == "wild" then ox, oy = M.pos(o, now) else ox, oy = stock.pos(o, now) end
      local d = sqrt(dsq(ox - w.tx) + dsq(oy - w.ty))
      if pr.kind ~= "wild" then
        -- (at a village's pen: people awake drive the pack off; a barking dog may turn it; a
        -- watch asleep close by wakes, D-105)
        local p = defender(W, s, w, now)
        if p then pack_flee(W, s, w, now, p); stand(w, now, "rest", 0.05); return end
        local i = require("sim.systems.fauna").find_pack(W, w.gid)
        local starving = i and A.p_cond[i] < c.wolf_starving
        for _, dg in ipairs(s.dogs or {}) do
          if dg.alive and (dg.act == "bark" or dg.act == "run") and dsq(dg.tx - w.tx) + dsq(dg.ty - w.ty) < 0.03 then
            if not starving and u(W, s, w, now, 7) < c.dog_deter then pack_flee(W, s, w, now, nil); stand(w, now, "rest", 0.05); return end
          end
        end
        for gi = 1, #(s.guards or {}) do
          local g = W.S.v3.people[s.guards[gi] or 0]
          if g and g.alive and g.objk == "guard" and g.act == "sleep" and dsq(g.x - w.tx) + dsq(g.y - w.ty) < (c.guard_alert * c.guard_alert) then
            s.alarm = { t = now, x = g.x, y = g.y, hh = g.hh }
            if M.interrupt_hook then M.interrupt_hook(W, g, now + 0.02, "Wolves at the pen") end
          end
        end
      end
      if d < c.wolf_reach then
        -- caught up: the kill, or it got away (the odds by the prey's state)
        local young = (now - o.born) / 1152 < (pr.kind == "wild" and 0.5 or stock.SP(o.sp).young_years)
        local pk = pr.kind == "wild" and c.wolf_kill_wild or c.wolf_kill_pen
        if young then pk = pk + 0.25 end
        if (o.sick or 0) > 0 then pk = pk + 0.2 end
        local v = u(W, s, w, now, 1)
        if v < pk then
          local ok = kill(W, s, w, pr, now)
          if ok then
            -- the pack gathers to eat
            for _, q in ipairs(s.wildlife.animals) do
              if q.alive and q.pred and q.gid == w.gid then
                q.prey = nil
                if q ~= w then M.interrupt(W, s, q, now) end
              end
            end
            w.feed_x, w.feed_y = ox, oy
            w.why = "Eating at the kill"
            stand(w, now, "eat", c.wolf_eat_h * (0.8 + 0.4 * u(W, s, w, now, 2)))
            return
          end
        end
        -- it got away: the pack gives up on it
        w.prey = nil
        w.tries = (w.tries or 0) + 1
        if w.tries >= 3 then w.done = true end
        w.why = "It got away: catching breath"
        stand(w, now, "rest", 0.15)
        return
      end
      -- close in (a chase within reach: running; else trotting toward it)
      local chase = d < c.wolf_chase
      if chase and pr.kind == "wild" and o.act ~= "flee" then
        o.flee_from = w.k
        M.interrupt(W, s, o, now)
      end
      local step = min(d, chase and 0.12 or 0.25)
      local gx, gy = w.tx + (ox - w.tx) / d * step, w.ty + (oy - w.ty) / d * step
      local what = pr.kind == "wild" and o.sp or ((o.sp == "cattle" and "beast" or o.sp) .. " in the pen")
      w.why = chase and ("Chasing a " .. what) or ("Closing in on a " .. what)
      if go_to(s, w, now, gx, gy, chase and S.run or S.speed, chase and "chase" or "walk", true) then return end
      w.done = true
      w.why = "No way to the " .. what
      stand(w, now, "rest", 0.1)
      return
    end
    -- the others: with the leader (at the kill: eat round it)
    local fx, fy = lead.feed_x, lead.feed_y
    if lead.act == "eat" and fx then
      local d = sqrt(dsq(fx - w.tx) + dsq(fy - w.ty))
      if d > 0.04 then
        local ang = u(W, s, w, now, 3) * 6.2832
        w.why = "To the kill"
        if go_to(s, w, now, fx + 0.025 * cos(ang), fy + 0.025 * sin(ang), S.run, "walk") then return end
      end
      w.why = "Eating at the kill"
      stand(w, now, "eat", c.wolf_eat_h * (0.6 + 0.5 * u(W, s, w, now, 4)))
      return
    end
    -- the others (S15): each its own way after the leader. On the move, strung out behind it, each
    -- at its own distance and to its own side, stopping to sniff now and then and trotting to catch
    -- up; in a chase, running at the quarry from its own flank, so the pack spreads round it.
    local x, y = w.tx, w.ty
    local lx, ly = M.pos(lead, now)
    local pace = 0.85 + 0.3 * stock.trait(w, stock.T_PACE)
    local side = stock.trait(w, stock.T_SIDE) - 0.5
    local o = lead.act == "chase" and lead.prey and prey_obj(s, lead.prey)
    local gx, gy, sp, act
    if o and o.alive then
      local ox, oy
      if lead.prey.kind == "wild" then ox, oy = M.pos(o, now) else ox, oy = stock.pos(o, now) end
      local ux, uy = ox - lx, oy - ly
      local ul = sqrt(ux * ux + uy * uy)
      if ul < 1e-6 then ux, uy, ul = 1, 0, 1 end
      ux, uy = ux / ul, uy / ul
      local ang = side * 2.6
      local ca, sa = cos(ang), sin(ang)
      local fr = 0.04 + 0.04 * stock.trait(w, stock.T_BOLD)
      gx, gy = ox - (ux * ca - uy * sa) * fr, oy - (uy * ca + ux * sa) * fr
      sp, act = S.run * pace, "chase"
      w.why = "Running at the quarry from the flank"
    else
      local hx, hy = lead.tx - lead.x, lead.ty - lead.y
      local hl = sqrt(hx * hx + hy * hy)
      if hl < 1e-6 then hx, hy, hl = w.hx or 1, w.hy or 0, 1 end
      hx, hy = hx / hl, hy / hl
      local lag = 0.03 + 0.07 * stock.trait(w, stock.T_BOLD)
      gx, gy = lx - hx * lag - hy * side * 0.07, ly - hy * lag + hx * side * 0.07
      local d = sqrt(dsq(gx - x) + dsq(gy - y))
      if d < 0.06 and u(W, s, w, now, 5) < 0.2 then
        w.why = "Stopping to sniff the ground"
        stand(w, now, "rest", 0.02 + 0.05 * u(W, s, w, now, 6))
        return
      end
      sp, act = S.speed * pace * (d > 0.12 and 1.5 or 1), "walk"
      w.why = d > 0.12 and "Trotting to catch up with the pack" or "Following the leader"
    end
    local d = sqrt(dsq(gx - x) + dsq(gy - y))
    if d > 0.012 then
      local step = min(d, act == "chase" and 0.15 or 0.2)
      w.hx, w.hy = (gx - x) / d, (gy - y) / d
      if go_to(s, w, now, x + (gx - x) / d * step, y + (gy - y) / d * step, sp, act, true) then return end
    end
    if act ~= "chase" then w.why = "Waiting for the leader" end
    stand(w, now, "rest", 0.04 + 0.06 * u(W, s, w, now, 8))
    return
  end
  -- grazers ------------------------------------------------------------------------------
  if w.leaving then
    local i = require("sim.systems.fauna").find_herd(W, w.gid)
    local ex, ey = edge_toward(W, s, i and A.h_cell[i] or s.cell)
    if go_to(s, w, now, ex, ey, S.speed, "leave") then return end
    w.alive, w.gone, w.died = false, true, now
    return
  end
  -- flight from wolves or people close by
  local fx, fy
  if w.flee_from then
    local o = byk(s, w.flee_from)
    if o and o.alive then fx, fy = o.tx, o.ty end
    w.flee_from = nil
  end
  if w.flee_x then
    fx, fy = w.flee_x, w.flee_y
    w.flee_x, w.flee_y = nil, nil
  end
  local wary = false
  if not fx and w.act ~= "flee" then
    local tx_, ty_, f = threat(W, s, w, now, 2)
    if tx_ and f <= 1 then fx, fy = tx_, ty_ elseif tx_ then wary = true end
  end
  if fx then
    -- away from it, fanning out to its own side, at its own speed; those near it see it run (S15)
    local dx, dy = w.tx - fx, w.ty - fy
    local d = max(1e-6, sqrt(dx * dx + dy * dy))
    dx, dy = dx / d, dy / d
    local side = (stock.trait(w, stock.T_SIDE) - 0.5) * 1.2
    dx, dy = dx - dy * side, dy + dx * side
    local l = max(1e-6, sqrt(dx * dx + dy * dy))
    local run = c.wild_flee * (0.7 + 0.6 * u(W, s, w, now, 5))
    local pace = 0.85 + 0.3 * stock.trait(w, stock.T_PACE)
    dx, dy = dx / l, dy / l
    for q = 0, 4 do
      -- (water or a wall that way: veering off to one side, then the other)
      local ang = q == 0 and 0 or ((q % 2 == 1) and 1 or -1) * (q <= 2 and 1.0 or 2.0)
      local ca, sa = cos(ang), sin(ang)
      local ex, ey = dx * ca - dy * sa, dy * ca + dx * sa
      if go_to(s, w, now, w.tx + ex * run, w.ty + ey * run, S.run * pace, "flee") then
        w.fled = now
        w.hx, w.hy = ex, ey
        w.why = "Running from danger"
        M.alarm(W, s, w, now, fx, fy)
        return
      end
    end
  end
  -- the young keep to their mothers
  local mom = w.mother ~= 0 and byk(s, w.mother)
  if mom and mom.alive and (now - w.born) / 1152 < 0.5 then
    local mx, my = M.pos(mom, now)
    local d = sqrt(dsq(mx - w.tx) + dsq(my - w.ty))
    if d > S.sep * 0.6 then
      w.why = "After its mother"
      if go_to(s, w, now, mx + 0.01, my, S.speed * 1.3, "walk") then return end
    end
    w.why = "At its mother's side"
    stand(w, now, mom.act == "rest" and "rest" or "graze", 0.3 + 0.4 * u(W, s, w, now, 6))
    return
  end
  -- the band on its ground (S15): each animal within its own range of the band's ground (the bold
  -- farther out), back to it when it has strayed, making room, rejoining the others when it finds
  -- itself alone, grazing its own way (a heading it keeps, turning back at the edge of its range),
  -- watchful when people are about, lying down sooner when those about it lie down; by night the
  -- band beds down close together
  local gr = s.wildlife.ground[w.gid]
  local x, y = w.tx, w.ty
  local cx, cy = gr and gr[1] or x, gr and gr[2] or y
  -- (never among the houses: the band's ground is pushed out to the edge of the village's keep,
  -- and an animal that finds itself inside heads out)
  local keep = c.wild_keep
  local cr = sqrt(cx * cx + cy * cy)
  if cr < keep and cr > 1e-6 then cx, cy = cx / cr * keep, cy / cr * keep end
  local wr = sqrt(x * x + y * y)
  if wr < keep * 0.85 and wr > 1e-6 then
    local ox, oy = x / wr * (keep + 0.1), y / wr * (keep + 0.1)
    local k = min(1, 0.25 / max(1e-6, keep + 0.1 - wr))
    w.why = "Away from the village"
    if go_to(s, w, now, x + (ox - x) * k, y + (oy - y) * k, S.speed * 1.3, "walk") then return end
  end
  local n = #group_of(s, w.gid)
  local pace = 0.85 + 0.3 * stock.trait(w, stock.T_PACE)
  local R = S.sep * (1.1 * sqrt(n) + 1.5)
  local Ri = R * (0.45 + 0.9 * stock.trait(w, stock.T_BOLD))
  local dax, day = cx - x, cy - y
  local da = sqrt(dax * dax + day * day)
  local hx, hy = w.hx or 1, w.hy or 0
  if da > Ri * (night and 4 or 1.6) then
    -- strayed (or the band's ground moved on): toward it, to its own side (by night only when far
    -- from the band: the band beds down where it is and moves on at first light)
    local ux, uy = dax / da, day / da
    local side = stock.trait(w, stock.T_SIDE) - 0.5
    local tx = cx - ux * Ri * 0.6 - uy * side * Ri * 0.8
    local ty = cy - uy * Ri * 0.6 + ux * side * Ri * 0.8
    local dx, dy = tx - x, ty - y
    local d = sqrt(dx * dx + dy * dy)
    -- (a long way is walked in long stretches: there is nothing to decide on the way unless
    -- something frightens it, and a fright interrupts it)
    local hop = (0.15 + 0.2 * u(W, s, w, now, 7)) * pace
    if d > 0.6 then hop = hop * 1.8 end
    if d > hop then tx, ty = x + dx / d * hop, y + dy / d * hop end
    if d > 1e-6 then w.hx, w.hy = dx / d, dy / d end
    w.why = night and "To the band for the night" or "To the band's grazing"
    if go_to(s, w, now, tx, ty, S.speed * pace, "walk") then return end
  end
  band_scan(s, w, now, x, y, S.sep * 4)
  if BS_.nn and BS_.nd < S.sep * 0.35 then
    local ex, ey = x - BS_.nx, y - BS_.ny
    local el = sqrt(ex * ex + ey * ey)
    if el < 1e-6 then ex, ey, el = -hy, hx, 1 end
    local st = S.sep * (0.4 + 0.3 * u(W, s, w, now, 12))
    w.why = "Making room"
    if go_to(s, w, now, x + ex / el * st, y + ey / el * st, S.speed * pace * 0.6, "walk", nil, true) then return end
  end
  if BS_.cn > 0 and (BS_.ln == 0 or (night and BS_.nd > S.sep * 2)) then
    -- alone (or apart from the others at nightfall): to them
    local mx, my = BS_.cx / BS_.cn, BS_.cy / BS_.cn
    local dx, dy = mx - x, my - y
    local d = sqrt(dx * dx + dy * dy)
    if d > S.sep * 1.5 then
      local st = min(d - S.sep, 0.15 * pace)
      w.hx, w.hy = dx / d, dy / d
      w.why = night and "Close to the others for the night" or "Rejoining the band"
      if go_to(s, w, now, x + dx / d * st, y + dy / d * st, S.speed * pace, "walk") then return end
    end
  end
  local v = u(W, s, w, now, 8)
  local lying = BS_.ln > 0 and BS_.lrest / BS_.ln or 0
  if night then
    w.why = "Bedded down with the band"
    stand(w, now, "rest", 1.5 + u(W, s, w, now, 9))
    return
  end
  if v > 0.8 - 0.3 * lying then
    w.why = "Lying up, chewing the cud"
    stand(w, now, "rest", 0.6 + u(W, s, w, now, 9))
    return
  end
  -- its own way: the heading it keeps, turning a little, back toward the band at its range's edge
  local k = (u(W, s, w, now, 10) - 0.5) * 1.4
  if da > Ri and da > 1e-6 then hx, hy = hx + dax / da * 0.9, hy + day / da * 0.9 end
  if BS_.nn and BS_.nd < S.sep * 0.9 and BS_.nd > 1e-6 then
    -- (another close by: its way bends off from it)
    hx, hy = hx + (x - BS_.nx) / BS_.nd * 0.8, hy + (y - BS_.ny) / BS_.nd * 0.8
  end
  hx, hy = hx - hy * k, hy + hx * k
  local l = sqrt(hx * hx + hy * hy)
  if l > 1e-6 then hx, hy = hx / l, hy / l else hx, hy = 1, 0 end
  w.hx, w.hy = hx, hy
  if v < 0.25 then
    w.why = "Grazing its way along"
    if go_to(s, w, now, x + hx * S.sep * 0.6, y + hy * S.sep * 0.6, S.speed * 0.5 * pace, "walk", nil, true) then return end
  end
  -- grazing, drifting slowly as it crops; watchful (short bouts, head up between) when people
  -- are about within twice the distance that sets it running
  local dur = wary and (0.08 + 0.12 * u(W, s, w, now, 11)) or (0.5 + 0.6 * u(W, s, w, now, 11))
  w.why = wary and "Grazing warily: someone is about" or "Grazing with the band"
  local L = wary and 0 or S.sep * 0.5 * pace * dur
  w.x, w.y = x, y
  if L > 1e-5 then
    local tx, ty = x + hx * L, y + hy * L
    local i = mapm.tile_of(s, tx, ty)
    if i > 0 and s.cost[i] >= 0 then w.tx, w.ty = tx, ty end
  end
  w.act, w.t0, w.t1 = "graze", now, now + dur
end

-- A hunter's kill of wild animal w (sim/agents/act.lua): dead where it stands, a carcass to be
-- dressed and carried; its herd loses it.
function M.shot(W, s, w, now, by)
  local fauna = require("sim.systems.fauna")
  w.alive, w.died, w.cause = false, now, "hunted"
  w.ver = w.ver + 1
  local i = fauna.find_herd(W, w.gid)
  if i then fauna.take_from_herd(W, i, 1, false) end
  local x, y = M.pos(w, now)
  local cc = { x = x, y = y, sp = w.sp, t = now, meat = WP(w.sp).meat, hide = WP(w.sp).hide or 1, by = by or 0 }
  s.wildlife.carcasses[#s.wildlife.carcasses + 1] = cc
  s.acc.wild_hunted = (s.acc.wild_hunted or 0) + 1
  -- (the band sees one of it fall: those near take flight from the hunter, S15)
  local q = by and W.S.v3.people[by]
  if q then M.alarm(W, s, w, now, q.x, q.y) end
  return cc
end

-- Every living wild animal of the settlement: fn(w).
function M.each(s, fn)
  local Wd = s.wildlife
  if not Wd then return end
  for _, w in ipairs(Wd.animals) do if w.alive then fn(w) end end
end

return M
