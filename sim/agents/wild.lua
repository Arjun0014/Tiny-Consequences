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
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local nav = require("sim.agents.nav")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")

local M = {}
M.HI = 3
local floor, max, min, sqrt, abs, cos, sin = math.floor, math.max, math.min, math.sqrt, math.abs, math.cos, math.sin
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
    local d = (tx - x) ^ 2 + (ty - y) ^ 2
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
    local d = (tx - x) ^ 2 + (ty - y) ^ 2
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

function M.pos(w, t)
  if w.t1 <= w.t0 or (w.act ~= "walk" and w.act ~= "flee" and w.act ~= "chase" and w.act ~= "leave") then return w.tx, w.ty end
  local f = (t - w.t0) / (w.t1 - w.t0)
  if f <= 0 then return w.x, w.y elseif f >= 1 then return w.tx, w.ty end
  return w.x + (w.tx - w.x) * f, w.y + (w.ty - w.y) * f
end

local function go_to(s, w, now, x, y, speed, act)
  local i = mapm.tile_of(s, x, y)
  if i == 0 or s.cost[i] < 0 then
    local fx, fy = mapm.free_spot(s, x, y, 3)
    if not fx then return false end
    x, y = fx, fy
  end
  local i0, i1 = mapm.tile_of(s, w.tx, w.ty), mapm.tile_of(s, x, y)
  if i0 > 0 and i1 > 0 and i0 ~= i1 and not nav.los(s, i0, i1, 3.1) then
    -- (a river or a building in the way: only an animal leaving the map finds its way round;
    -- the others go half as far, or stay this side)
    if act == "leave" then
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
      local d = sqrt((x - cx) ^ 2 + (y - cy) ^ 2)
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
  -- arrivals walk in from the side of the herd's range; the surplus walks out that way
  for gid, tg in pairs(target) do
    local i = tg.i
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
            w.t1 = now + 0.02 * q
            M.push(W, w.t1, M.event_id(s, w), w.ver)
          end
          s.acc.wolf_nights = (s.acc.wolf_nights or 0) + 1
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
local function threat(W, s, w, now)
  local c = C()
  local Wd = s.wildlife
  -- wolves on the hunt
  for _, o in ipairs(Wd.animals) do
    if o.alive and o.pred and (o.act == "chase" or o.act == "walk") then
      local d2 = (o.tx - w.tx) ^ 2 + (o.ty - w.ty) ^ 2
      if d2 < c.wild_alert_wolf ^ 2 then return o.tx, o.ty end
    end
  end
  -- people about in the woods and fields: where each began what they are doing (state: the
  -- same after a load), hunters where they are now
  local V = W.S.v3
  local r2 = c.wild_alert ^ 2
  local wx, wy = w.tx, w.ty
  local mem = s.members
  for i = 1, #mem do
    local p = V.people[mem[i]]
    if p.alive and p.inside == 0 then
      local px, py = p.x, p.y
      local dx, dy = px - wx, py - wy
      local d2 = dx * dx + dy * dy
      if p.objk == "hunt" then
        px, py = stock.person_pos(p, now)
        -- (a hunter creeping up is seen later, the more skilled the later)
        local k = 1 - 0.5 * (p.sk_hunt or 0.2)
        if (px - wx) ^ 2 + (py - wy) ^ 2 < r2 * k * k then return px, py end
      elseif d2 < r2 and p.act ~= "carried" and p.act ~= "sleep" then
        return px, py
      end
    end
  end
  return nil
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
      local d = sqrt((w.tx - lead.tx) ^ 2 + (w.ty - lead.ty) ^ 2)
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
        for _, a in ipairs(H.animals) do
          if a.alive then
            local S = stock.SP(a.sp)
            local small = (a.sp == "cattle" and (now - a.born) / 1152 > S.young_years * 2) and -3 or 0
            local d = sqrt((pen.cx - lead.tx) ^ 2 + (pen.cy - lead.ty) ^ 2)
            local sc = 1.2 + small + ((now - a.born) / 1152 < S.young_years and 0.5 or 0) - d * 0.5 + (1 - A.p_cond[i]) * 1.5
            if sc > bs then best, bs, bx, by = { kind = kind, k = a.k }, sc, a.tx, a.ty end
          end
        end
      end
    end
  end
  return best, bx, by
end

-- Is a pen watched (someone outdoors and awake close by, or keeping the night watch there)?
function M.watched(W, s, H)
  local pen = s.buildings[H.pen]
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.inside == 0 and (p.act ~= "sleep" or p.objk == "guard") then
      if (p.x - pen.cx) ^ 2 + (p.y - pen.cy) ^ 2 < 0.06 then return true end
    end
  end
  return false
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
    -- wolves: hunt the night, eat, leave before dawn
    local lead = w.lead ~= 0 and byk(s, w.lead) or nil
    if lead and not lead.alive then lead = nil end
    local dawn = lh >= (s.sunrise or 6) - 1.2 and lh < (s.sunset or 18)
    if dawn or w.done then
      local i = require("sim.systems.fauna").find_pack(W, w.gid)
      local ex, ey = edge_toward(W, s, i and A.p_cell[i] or s.cell)
      if go_to(s, w, now, ex, ey, S.speed, "leave") then return end
      w.alive, w.gone, w.died = false, true, now
      return
    end
    if w.act == "eat" then
      -- (a while at the carcass, then off)
      w.done = true
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
        stand(w, now, "rest", 0.2)
        return
      end
      local ox, oy
      if pr.kind == "wild" then ox, oy = M.pos(o, now) else ox, oy = stock.pos(o, now) end
      local d = sqrt((ox - w.tx) ^ 2 + (oy - w.ty) ^ 2)
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
            stand(w, now, "eat", c.wolf_eat_h * (0.8 + 0.4 * u(W, s, w, now, 2)))
            return
          end
        end
        -- it got away: the pack gives up on it
        w.prey = nil
        w.tries = (w.tries or 0) + 1
        if w.tries >= 3 then w.done = true end
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
      if go_to(s, w, now, gx, gy, chase and S.run or S.speed, chase and "chase" or "walk") then return end
      w.done = true
      stand(w, now, "rest", 0.1)
      return
    end
    -- the others: with the leader (at the kill: eat round it)
    local fx, fy = lead.feed_x, lead.feed_y
    if lead.act == "eat" and fx then
      local d = sqrt((fx - w.tx) ^ 2 + (fy - w.ty) ^ 2)
      if d > 0.04 then
        local ang = u(W, s, w, now, 3) * 6.2832
        if go_to(s, w, now, fx + 0.025 * cos(ang), fy + 0.025 * sin(ang), S.run, "walk") then return end
      end
      stand(w, now, "eat", c.wolf_eat_h * (0.6 + 0.5 * u(W, s, w, now, 4)))
      return
    end
    local i = w.k - lead.k
    local ang = i * GOLD + (now * 0.7) % 6.2832
    local r = 0.035 * sqrt(i)
    local gx, gy = lead.tx + r * cos(ang), lead.ty + r * sin(ang)
    local d = sqrt((gx - w.tx) ^ 2 + (gy - w.ty) ^ 2)
    local sp = lead.act == "chase" and S.run or S.speed
    if d > 0.015 then
      local step = min(d, 0.2)
      if go_to(s, w, now, w.tx + (gx - w.tx) / d * step, w.ty + (gy - w.ty) / d * step, sp, lead.act == "chase" and "chase" or "walk") then return end
    end
    stand(w, now, "rest", 0.08)
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
  if not fx and w.act ~= "flee" then fx, fy = threat(W, s, w, now) end
  if fx then
    local dx, dy = w.tx - fx, w.ty - fy
    local d = max(1e-6, sqrt(dx * dx + dy * dy))
    local run = c.wild_flee * (0.7 + 0.6 * u(W, s, w, now, 5))
    if go_to(s, w, now, w.tx + dx / d * run, w.ty + dy / d * run, S.run, "flee") then return end
  end
  -- the young keep to their mothers
  local mom = w.mother ~= 0 and byk(s, w.mother)
  if mom and mom.alive and (now - w.born) / 1152 < 0.5 then
    local mx, my = M.pos(mom, now)
    local d = sqrt((mx - w.tx) ^ 2 + (my - w.ty) ^ 2)
    if d > S.sep * 0.6 then
      if go_to(s, w, now, mx + 0.01, my, S.speed * 1.3, "walk") then return end
    end
    stand(w, now, mom.act == "rest" and "rest" or "graze", 0.3 + 0.4 * u(W, s, w, now, 6))
    return
  end
  -- its place in the band's loose spread on the band's ground (by night the band beds down)
  local gr = s.wildlife.ground[w.gid]
  local cx, cy = gr and gr[1] or w.tx, gr and gr[2] or w.ty
  local slot = (w.k * 7) % 61
  local turn = (floor(now / 24) * GOLD * 1.3 + w.gid) % 6.2832
  local r = S.sep * sqrt(slot + 0.5)
  local tx, ty = cx + r * cos(slot * GOLD + turn), cy + r * sin(slot * GOLD + turn)
  -- (never among the houses: a place inside the village's keep is pushed out to its edge, and an
  -- animal that finds itself inside heads out)
  local keep = c.wild_keep
  local tr = sqrt(tx * tx + ty * ty)
  if tr < keep and tr > 1e-6 then tx, ty = tx / tr * keep, ty / tr * keep end
  local wr = sqrt(w.tx * w.tx + w.ty * w.ty)
  if wr < keep * 0.85 and wr > 1e-6 then
    local ox, oy = w.tx / wr * (keep + 0.1), w.ty / wr * (keep + 0.1)
    if go_to(s, w, now, w.tx + (ox - w.tx) * min(1, 0.25 / max(1e-6, keep + 0.1 - wr)), w.ty + (oy - w.ty) * min(1, 0.25 / max(1e-6, keep + 0.1 - wr)), S.speed * 1.3, "walk") then return end
  end
  local d = sqrt((tx - w.tx) ^ 2 + (ty - w.ty) ^ 2)
  if d > S.sep * 1.5 then
    local hop = min(d, 0.12 + 0.1 * u(W, s, w, now, 7))
    if go_to(s, w, now, w.tx + (tx - w.tx) / d * hop, w.ty + (ty - w.ty) / d * hop, S.speed, "walk") then return end
  end
  local v = u(W, s, w, now, 8)
  if night or v > 0.8 then
    stand(w, now, "rest", (night and 1.5 or 0.6) + u(W, s, w, now, 9))
  elseif v < 0.25 then
    local o = P8[floor(u(W, s, w, now, 10) * 8) + 1]
    if not go_to(s, w, now, w.tx + o[1] * S.sep * 0.6, w.ty + o[2] * S.sep * 0.6, S.speed * 0.5, "walk") then
      stand(w, now, "graze", 0.5)
    end
  else
    stand(w, now, "graze", 0.5 + 0.6 * u(W, s, w, now, 11))
  end
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
  return cc
end

-- Every living wild animal of the settlement: fn(w).
function M.each(s, fn)
  local Wd = s.wildlife
  if not Wd then return end
  for _, w in ipairs(Wd.animals) do if w.alive then fn(w) end end
end

return M
