-- Livestock as individual animals (roadmap v3 R3, D-089): a village's sheep. Each sheep is a
-- persistent micro-agent with its own position and action — grazing, a few steps to fresh grass,
-- resting, a lamb keeping to its mother, the walk to the fold at dusk — decided when its last
-- action ends, in the agents' own event queue (strict time order with the people). The flock
-- has no single destination of its own: by day each sheep keeps near the herder who drove it out
-- (or the pasture the herder left it on), at night near the fold, each at its own spot and pace.
--
-- s.flock = { sheep = { [k] = a }, fold (building id), herder (person id), out (bool), spot_x/y
--   (the day's pasture), hay (the stack, sheep-days of feed), rack (hay in the fold's rack),
--   grass = { [block] = 0..1 } (16 x 16 blocks of 8 x 8 tiles), next_k }
-- a = { k, sex, born, owner (household), mother (k or 0), x, y (at t0), tx, ty (at t1), t0, t1,
--   act ("graze" | "walk" | "rest" | "fold"), ver, hunger, health, wool, alive, lambed (year) }
-- Events: id = ID0 + settlement id x 100000 + k (people have smaller ids).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local nav = require("sim.agents.nav")
local clock = require("sim.agents.clock")

local M = {}
M.ID0 = 100000000
local floor, max, min, sqrt, abs = math.floor, math.max, math.min, math.sqrt, math.abs
local function C() return cfgmod.V3 end

function M.event_id(s, a) return M.ID0 + s.id * 100000 + a.k end
function M.decode(id)
  local r = id - M.ID0
  local sid = floor(r / 100000)
  return sid, r - sid * 100000
end

-- Position of sheep a at time t (a straight walk between t0 and t1, else where it stands).
function M.pos(a, t)
  if a.act ~= "walk" or a.t1 <= a.t0 then return a.tx, a.ty end
  local f = (t - a.t0) / (a.t1 - a.t0)
  if f <= 0 then return a.x, a.y elseif f >= 1 then return a.tx, a.ty end
  return a.x + (a.tx - a.x) * f, a.y + (a.ty - a.y) * f
end

local function u(W, s, a, now, k)
  return rng.float(W.seed, rng.S.V3_MAP, s.key * 7 + a.k, floor(now * 60) % 4294967296, 500 + k)
end

-- Pasture blocks -----------------------------------------------------------------------------
local BS = 8                         -- tiles a block side
local function block_of(s, x, y)
  local N = s.N
  local bn = N / BS
  local bx = floor((x / s.T + N / 2) / BS)
  local by = floor((y / s.T + N / 2) / BS)
  if bx < 0 or by < 0 or bx >= bn or by >= bn then return 0 end
  return by * bn + bx + 1
end
M.block_of = block_of

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

-- Daily: grass regrows with warmth and water (little in winter), up to what the ground allows.
function M.daily_grass(W, s, day)
  local F = s.flock
  if not F then return end
  local c = C()
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  local T = W.F.temp[s.cell] or 10
  local sw = W.F.soil_water[s.cell] or 0.5
  local warm = T > c.grow_tmin and min(1, (T - c.grow_tmin) / (c.grow_topt - c.grow_tmin)) or 0
  local g = c.grass_regrow * warm * min(1, 0.4 + sw * 1.2) * (doy >= 36 and 0.25 or 1)
  local nb = (s.N / BS) * (s.N / BS)
  for b = 1, nb do
    local cap = F.open[b] or 0
    local v = F.grass[b] or 0
    if v < cap then F.grass[b] = min(cap, v + g * cap) end
  end
end

-- The day's pasture: the grassiest open block within reach of the fold (not the one grazed bare).
function M.pick_spot(W, s, now)
  local F = s.flock
  local fold = s.buildings[F.fold]
  local N = s.N
  local bn = N / BS
  local best, bs = 0, -1e9
  for b = 1, bn * bn do
    local v = F.grass[b] or 0
    if v > 0.15 then
      local by = floor((b - 1) / bn)
      local bx = (b - 1) - by * bn
      local x, y = mapm.ixy_xy(s, bx * BS + BS / 2, by * BS + BS / 2)
      local d = sqrt((x - fold.cx) ^ 2 + (y - fold.cy) ^ 2)
      if d < C().pasture_reach then
        local sc = v - d * 0.6
        if sc > bs then best, bs = b, sc end
      end
    end
  end
  if best == 0 then return nil end
  local by = floor((best - 1) / bn)
  local bx = (best - 1) - by * bn
  local x, y = mapm.ixy_xy(s, bx * BS + BS / 2, by * BS + BS / 2)
  local fx, fy = mapm.free_spot(s, x, y, 4)
  return fx or x, fy or y, best
end

-- Deciding ---------------------------------------------------------------------------------

-- Is the flock's herder (a person) still at it?
function M.herder(W, s)
  local F = s.flock
  local p = W.S.v3.people[F.herder or 0]
  if p and p.alive and (p.objk == "herd" or p.objk == "herd_home") then return p end
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
    local path = nav.path(s, a.tx, a.ty, x, y, 1, a.mv)
    if not path or path.n < 2 then return false end
    a.mv = path
    x, y = path.x[2], path.y[2]
  end
  local d = sqrt((x - a.tx) ^ 2 + (y - a.ty) ^ 2)
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

-- The end of sheep a's action at time now: its effects, then the next action.
function M.step(W, s, a, now)
  local c = C()
  local F = s.flock
  local dt = now - a.t0
  -- the body: hunger rises; grazing (on the block's grass) and hay in the fold feed it
  a.hunger = min(1, a.hunger + c.sheep_hunger * dt)
  if a.act == "graze" then
    local b = block_of(s, a.tx, a.ty)
    local g = F.grass[b] or 0
    local eat = min(a.hunger, c.sheep_graze * dt * g)
    a.hunger = a.hunger - eat
    F.grass[b] = max(0, g - eat * c.grass_per_feed)
  elseif (a.act == "fold" or a.act == "rest") and a.hunger > 0.4 and (F.rack or 0) > 0 then
    -- hay from the fold's rack (in hunger units)
    local eat = min(a.hunger - 0.1, F.rack)
    a.hunger = a.hunger - eat
    F.rack = F.rack - eat
  end
  if a.hunger >= 1 then a.health = max(0, a.health - 0.02 * dt) elseif a.health < 1 then a.health = min(1, a.health + 0.01 * dt) end
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  local night = lh >= (s.sunset or 18) + 0.4 or lh < (s.sunrise or 6) - 0.3
  local fold = s.buildings[F.fold]
  local age = (now - a.born) / 1152
  -- a lamb keeps to its mother
  local mom = a.mother ~= 0 and F.sheep[a.mother]
  if age < c.lamb_years and mom and mom.alive then
    local mx, my = M.pos(mom, now)
    local d = sqrt((mx - a.tx) ^ 2 + (my - a.ty) ^ 2)
    if d > 0.03 then
      local ang = u(W, s, a, now, 1) * 6.2832
      local ox, oy = 0.012 * (ang < 3.14 and 1 or -1), 0.012 * ((ang % 3.14) < 1.57 and 1 or -1)
      if walk_to(W, s, a, now, mx + ox, my + oy, c.sheep_speed * 1.3) then return end
    end
    stand(a, now, (mom.act == "rest" or mom.act == "fold") and "rest" or "graze", 0.15 + 0.3 * u(W, s, a, now, 2))
    return
  end
  -- where the flock is kept now
  local p = M.herder(W, s)
  local cx, cy, r
  if p then
    cx, cy = require("sim.agents.act").pos(p, now)
    r = c.flock_radius
  elseif F.out and not night then
    cx, cy, r = F.spot_x or fold.cx, F.spot_y or fold.cy, c.flock_radius
  else
    cx, cy, r = fold.cx, fold.cy, fold.w * s.T * 0.42
    if F.out and night then F.out = false end    -- (no herder: they come home to the fold by themselves)
  end
  local d = sqrt((cx - a.tx) ^ 2 + (cy - a.ty) ^ 2)
  if d > r * 1.3 then
    -- drift toward the flock's ground: each to its own spot in it
    local ang = u(W, s, a, now, 3)
    local rr = r * (0.3 + 0.7 * u(W, s, a, now, 4))
    local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }
    local o = P8[floor(ang * 8) + 1]
    local tx, ty = cx + o[1] * rr, cy + o[2] * rr
    -- (a long way is walked in hops: they look up and follow the others)
    local hop = c.sheep_hop * (0.7 + 0.6 * u(W, s, a, now, 5))
    if d - rr > hop then
      local f = hop / max(1e-6, d)
      tx, ty = a.tx + (tx - a.tx) * f, a.ty + (ty - a.ty) * f
    end
    if walk_to(W, s, a, now, tx, ty, c.sheep_speed * (d > 0.4 and 1.4 or 1)) then return end
  end
  local inside = not p and not (F.out and not night)
  if night or (inside and lh < (s.sunrise or 6) + 1) then
    stand(a, now, "fold", 1 + 2 * u(W, s, a, now, 6))
    return
  end
  if inside then
    stand(a, now, "fold", 0.4 + 0.6 * u(W, s, a, now, 6))
    return
  end
  -- out on the pasture: graze, take a few steps to fresher grass, now and then lie down
  local v = u(W, s, a, now, 7)
  if v < 0.35 then
    local ang = u(W, s, a, now, 8) * 8
    local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }
    local o = P8[floor(ang) + 1]
    local st = 0.01 + 0.02 * u(W, s, a, now, 9)
    if walk_to(W, s, a, now, a.tx + o[1] * st, a.ty + o[2] * st, c.sheep_speed * 0.5) then return end
  end
  if v > 0.9 and a.hunger < 0.35 then
    stand(a, now, "rest", 0.4 + 0.8 * u(W, s, a, now, 10))
  else
    stand(a, now, "graze", 0.2 + 0.5 * u(W, s, a, now, 11))
  end
end

-- Daily round: grass, lambing in spring, wool, deaths --------------------------------------
function M.daily(W, s, day, push)
  local F = s.flock
  if not F then return end
  local c = C()
  M.daily_grass(W, s, day)
  local now = W.hour
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  if doy == c.slaughter[1] then s.acc.culled_plan = (s.acc.culled_plan or 0) + M.plan_cull(W, s) end
  local year = floor((day + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  local n = #F.sheep
  for k = 1, n do
    local a = F.sheep[k]
    if a.alive then
      local age = (now - a.born) / 1152
      a.wool = min(c.wool_max, (a.wool or 0) + c.wool_day * (age > c.lamb_years and 1 or 0.3))
      -- deaths: old age, hunger
      local hz = age > 7 and (age - 7) * 0.08 / 48 or 0.002 / 48
      if a.health < 0.3 then hz = hz + (0.3 - a.health) * 0.3 end
      local v = rng.float(W.seed, rng.S.V3_LIFE, s.key * 7 + k, day, 31)
      if v < hz then
        a.alive = false
        a.died = now
        s.acc.sheep_died = (s.acc.sheep_died or 0) + 1
      elseif a.sex == "f" and age >= 1 and a.lambed ~= year and doy >= c.lambing[1] and doy <= c.lambing[2] then
        -- lambing in spring (a ewe in good condition; twins now and then)
        local w = rng.float(W.seed, rng.S.V3_LIFE, s.key * 7 + k, day, 32)
        if w < c.lamb_chance and a.health > 0.5 then
          a.lambed = year
          local twins = rng.float(W.seed, rng.S.V3_LIFE, s.key * 7 + k, day, 33) < 0.25 and 2 or 1
          for t = 1, twins do
            local lamb = M.add(W, s, { sex = (rng.float(W.seed, rng.S.V3_LIFE, s.key * 7 + k, day, 34 + t) < 0.5) and "f" or "m",
              born = now, owner = a.owner, mother = k, x = a.tx + 0.01 * t, y = a.ty })
            push(W, lamb.t1, M.event_id(s, lamb), lamb.ver)
            s.acc.lambs = (s.acc.lambs or 0) + 1
          end
        end
      end
    end
  end
end

-- The beasts a household will kill this autumn (D-089): the flock wintered on the hay in the
-- stack (hay_days of winter feed a sheep), the household's share of what it cannot feed: the
-- rams beyond one, the old ewes, then this year's lambs. Marked a.cull = true.
function M.plan_cull(W, s)
  local F = s.flock
  local c = C()
  local now = W.hour
  local alive = {}
  for _, a in ipairs(F.sheep) do if a.alive and not a.cull then alive[#alive + 1] = a end end
  local per = c.hay_days * c.sheep_hunger * 24 * 0.6
  local keep = floor((F.hay or 0) / per)
  local over = #alive - keep
  if over <= 0 then return 0 end
  -- most dispensable first: old (> 6 y), rams but the best, then the young males, then lambs
  local rams = 0
  table.sort(alive, function(x, y)
    local ax, ay = (now - x.born) / 1152, (now - y.born) / 1152
    local sx = (ax > 6 and 3 or 0) + (x.sex == "m" and 2 or 0) + (ax < 1 and 1 or 0) + ax * 0.01
    local sy = (ay > 6 and 3 or 0) + (y.sex == "m" and 2 or 0) + (ay < 1 and 1 or 0) + ay * 0.01
    if sx ~= sy then return sx > sy end
    return x.k < y.k
  end)
  local n = 0
  for _, a in ipairs(alive) do
    if n >= over then break end
    if a.sex == "m" and rams == 0 and (now - a.born) / 1152 > 1 and (now - a.born) / 1152 < 6 then
      rams = 1      -- (one good ram is kept)
    else
      a.cull = true
      n = n + 1
    end
  end
  return n
end

-- A new sheep (founding, lambing).
function M.add(W, s, o)
  local F = s.flock
  local k = #F.sheep + 1
  local now = W.S.v3.now or W.hour
  local a = { k = k, sex = o.sex or "f", born = o.born or now, owner = o.owner or 0, mother = o.mother or 0,
    x = o.x, y = o.y, tx = o.x, ty = o.y, t0 = now, t1 = now + 0.05 + 0.1 * (k % 7) / 7, act = "fold", ver = 0,
    hunger = 0.3, health = 1, wool = o.wool or 0, alive = true, lambed = -1, mv = { x = {}, y = {}, cum = {} } }
  F.sheep[k] = a
  return a
end

-- Founding: a fold at the village's edge on open ground, a household's few sheep each, the hay
-- left from the last year (the season's), grass on the commons.
function M.found(W, s, households, u, doy0)
  local c = C()
  local N = s.N
  local c0 = floor(N / 2) + 1
  -- the open side: the quarter with the most open blocks near the village
  local best, bx, by = -1, c0, c0
  for _, dir in ipairs({ { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 } }) do
    for r = 12, 44, 3 do
      local ix, iy = c0 + dir[1] * r, c0 + dir[2] * r
      if mapm.rect_free(s, ix - 3, iy - 3, ix + 8, iy + 8, true) then
        local open = 0
        for jy = iy - 6, iy + 10, 2 do
          for jx = ix - 6, ix + 10, 2 do
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
  local settle = require("sim.agents.settle")
  local fold = settle.add_building(W, s, "fold", bx, by, "s", "standing")
  local F = { sheep = {}, fold = fold.id, herder = 0, out = false, grass = {}, open = {},
    hay = c.hay_found[floor(doy0 / 12) + 1], rack = 4 }
  s.flock = F
  local nb = (N / BS) * (N / BS)
  for b = 1, nb do
    F.open[b] = openness(s, b)
    F.grass[b] = F.open[b] * (0.5 + 0.3 * u())
  end
  for _, hh in ipairs(households) do
    local n = 2 + floor(u() * 3)
    for q = 1, n do
      local x = fold.cx + (u() - 0.5) * fold.w * s.T * 0.7
      local y = fold.cy + (u() - 0.5) * fold.h * s.T * 0.7
      M.add(W, s, { sex = q == 1 and u() < 0.3 and "m" or "f", born = W.hour - (1 + u() * 5) * 1152, owner = hh.id, x = x, y = y,
        wool = doy0 < 12 and 1.0 + u() * 0.5 or 0.3 + u() * 0.4 })
    end
  end
  return F
end

return M
