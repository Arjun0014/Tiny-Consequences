-- Birds (S14, roadmap v3): flocks as agents. A flock has a kind, a home (its roost), a hunger, an
-- objective with a target and a reason, and a way of getting there (a curved flight); what it does
-- comes from the world around it - the dawn and the dusk, the weather, the boats hauling their
-- nets, a catch landed at the dock, fish on the drying racks, a plough turning up worms, a carcass,
-- the shore at hand - never from a loop drawn round a point. Circling happens only where there is
-- food to wheel over (a boat working its net). The members are drawn round the flock by the view
-- (game/view/agents_view.lua), each with its own place, phase and wing beat: one cheap agent, a
-- dozen birds.
-- The first kind (the vertical slice): the gulls of a coast.
--   s.birds = { flocks = { f } }, f = { k, kind, n, rx, ry (roost), act, next, t0, t1,
--     x0, y0, z0, x1, y1, z1, cx, cy (a flight's curve), fx, fy (an activity's place), tgt_kind,
--     tgt, hunger, obj, why, ver, alive }
-- Events: id = stock.ID0 + settlement id x 100000 + 4 x 20000 + 10000 + k (dogs are below 10000).
local dmath = require("sim.dmath")
local cfgmod = require("sim.config")
local mapm = require("sim.agents.map")
local clock = require("sim.agents.clock")
local rng = require("sim.rng")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end
M.HI, M.K0 = 4, 10000
local GOLD = 2.39996323

-- (set by sim/agents/init.lua: people and their positions; bob forbids the circular require)
M.people = nil
M.person_pos = function(p, t) return p.x, p.y end

function M.event_id(s, f) return 100000000 + s.id * 100000 + M.HI * 20000 + M.K0 + f.k end

function M.byk(s, k)
  local L = s.birds and s.birds.flocks
  if not L then return nil end
  local f = L[k]
  if f and f.k == k then return f end
  for i = 1, #L do if L[i].k == k then return L[i] end end
  return nil
end

function M.each(s, fn)
  local L = s.birds and s.birds.flocks
  if not L then return end
  for i = 1, #L do if L[i].alive then fn(L[i]) end end
end

local function u(W, s, f, now, k)
  return rng.float(W.seed, rng.S.V3_MAP, s.key * 31 + f.k * 7919 + 77, floor(now * 60) % 4294967296, 700 + k)
end

-- The boat a skipper rows (afloat: their position is the boat's).
local function boat_pos(q, t) return M.person_pos(q, t) end

-- Where the flock is at time t: x, y (local), z (height above the ground or the water, local
-- units) and its heading (hx, hy). Pure: the view calls it every frame.
function M.pos(f, t)
  local a = f.act
  if a == "fly" then
    local span = f.t1 - f.t0
    local v = span > 0 and (t - f.t0) / span or 1
    if v < 0 then v = 0 elseif v > 1 then v = 1 end
    -- a quadratic curve through the control point (cx, cy): a turn, not a ruler line
    local w0, w1, w2 = (1 - v) * (1 - v), 2 * v * (1 - v), v * v
    local x = w0 * f.x0 + w1 * f.cx + w2 * f.x1
    local y = w0 * f.y0 + w1 * f.cy + w2 * f.y1
    local hx = 2 * (1 - v) * (f.cx - f.x0) + 2 * v * (f.x1 - f.cx)
    local hy = 2 * (1 - v) * (f.cy - f.y0) + 2 * v * (f.y1 - f.cy)
    local z = f.z0 + (f.z1 - f.z0) * v + (f.climb or 0) * dmath.sin(3.14159265 * v)
    return x, y, z, hx, hy
  end
  if a == "follow" then
    -- wheeling low over the boat while its net comes up
    local q = M.people and M.people[f.tgt or 0]
    local bx, by = f.fx, f.fy
    if q and q.alive then bx, by = boat_pos(q, t) end
    local ang = (t - f.t0) * 42 + f.k
    local r = 0.07
    local x, y = bx + r * dmath.cos(ang), by + r * dmath.sin(ang)
    return x, y, 0.11, -dmath.sin(ang), dmath.cos(ang)
  end
  if a == "plough" then
    -- on the fresh furrow behind the ploughman
    local q = M.people and M.people[f.tgt or 0]
    if q and q.alive then
      local px, py, dx, dy = M.person_pos(q, t)
      local l = dx and sqrt(dx * dx + dy * dy) or 0
      if l > 1e-9 then dx, dy = dx / l, dy / l else dx, dy = 1, 0 end
      return px - dx * 0.05, py - dy * 0.05, 0, dx, dy
    end
    return f.fx, f.fy, 0, 1, 0
  end
  return f.fx, f.fy, (a == "raft" or a == "roost" or a == "shelter") and 0 or 0, 1, 0
end

-- The coast of a map: land tiles with the sea beside them (a runtime list, never saved).
local SHORE = setmetatable({}, { __mode = "k" })
local function shore(s)
  local L = SHORE[s]
  if L then return L end
  L = {}
  local N, land, riv, wc = s.N, s.land, s.riv, s.wcost
  for iy = 3, N - 2 do
    for ix = 3, N - 2 do
      local i = (iy - 1) * N + ix
      if land[i] == 1 and riv[i] <= 0 and (land[i - 1] == 0 or land[i + 1] == 0 or land[i - N] == 0 or land[i + N] == 0) then
        L[#L + 1] = i
      end
    end
  end
  SHORE[s] = L
  return L
end
M.shore = shore

-- (S23, D-185) The planet's oil on settlement s's coast: the most of it in the sea within two rings of its cell,
-- and the tag of that cell's oil (W.C.oil: the event whose spill it is). Reads only.
function M.coast_oil(W, s)
  local sites = require("sim.society.sites")
  local oil, tag = 0, 0
  local F, Cb = W.F.oil, W.C and W.C.oil
  if not F then return 0, 0 end
  for _, cc in ipairs(sites.rings(W, s.cell, 2)) do
    if W.geo.ocean[cc] == 1 and (F[cc] or 0) > oil then oil, tag = F[cc], Cb and Cb[cc] or 0 end
  end
  return oil, tag
end

-- (S23, D-185) Oil on the coast oils the gulls (they dive through it for the fish it kills, and cannot fly once
-- it is in their feathers): a share of the colony a day by how fouled the sea is, a deed with the oil's cause;
-- a clean sea brings birds back to what it feeds, one at a time.
local function oiled(W, s, f, day, push)
  local c = C()
  local oil, tag = M.coast_oil(W, s)
  local now = W.S.v3.now or W.hour
  if oil > c.oil_coast_min then
    if not f.alive then return end
    local k = min(1, oil / c.oil_coast_ref) * c.oil_gull_k
    local lost = min(f.n, max(1, floor(f.n * k + rng.float(W.seed, rng.S.V3_MAP, s.key, day, 931))))
    f.n = f.n - lost
    f.oiled = (f.oiled or 0) + lost   -- (the birds the oil took: only these come back - an untouched colony keeps no count)
    s.acc.gulls_oiled = (s.acc.gulls_oiled or 0) + lost
    local deeds = require("sim.agents.deeds")
    deeds.add(W, { kind = "oiled", t = now, at = s.id, x = f.rx, y = f.ry, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
      to_s = s.id, what = "gulls", n = lost, fu = 0, harm = false, event = tag ~= 0 and tag or nil })
    if f.n <= 0 then f.n, f.alive, f.ver = 0, false, (f.ver or 0) + 1 end
  elseif not f.alive then
    -- a clean sea again: a first bird back at the old roost
    if (f.oiled or 0) > 0 and day % c.gull_back == 0 then
      f.alive, f.n, f.ver, f.oiled = true, 1, (f.ver or 0) + 1, f.oiled - 1
      f.act, f.fx, f.fy, f.x0, f.y0, f.z0, f.x1, f.y1, f.z1, f.cx, f.cy = "roost", f.rx, f.ry, f.rx, f.ry, 0, f.rx, f.ry, 0, f.rx, f.ry
      f.t0, f.t1, f.obj, f.why, f.tgt_kind, f.tgt = now, now + 0.1, "Roost", "On the rocks", "roost", 0
      if push then push(W, f.t1, M.event_id(s, f), f.ver) end
    end
  elseif (f.oiled or 0) > 0 and day % c.gull_back == 0 then
    f.n, f.oiled = f.n + 1, f.oiled - 1
    if f.oiled == 0 then f.oiled = nil end
  end
end

-- A gull colony where the map has a coast: its roost on the shore farthest from the houses.
function M.daily(W, s, day, push)
  local c = C()
  s.birds = s.birds or { flocks = {} }
  local B = s.birds
  if #B.flocks > 0 then
    for _, f in ipairs(B.flocks) do if f.kind == "gull" then oiled(W, s, f, day, push) end end
    return
  end
  local L = shore(s)
  if #L < 12 then return end
  -- the roost: of the shore, far from the well and the dock (rocks the people leave alone)
  local wx, wy = s.well and s.well.dx or 0, s.well and s.well.dy or 0
  local best, bs = 0, -1
  for i = 1, #L, 3 do
    local x, y = mapm.tile_xy(s, L[i])
    local d = sqrt((x - wx) * (x - wx) + (y - wy) * (y - wy))
    if d > bs then best, bs = L[i], d end
  end
  local rx, ry = mapm.tile_xy(s, best)
  -- as many as the sea nearby feeds
  local fish = 0
  local sites = require("sim.society.sites")
  local nf = 0
  for _, cc in ipairs(sites.rings(W, s.cell, 2)) do
    if W.geo.ocean[cc] == 1 then fish = fish + (W.F.fish[cc] or 0); nf = nf + 1 end
  end
  fish = nf > 0 and fish / nf or 0
  local now = W.S.v3.now or W.hour
  local f = { k = 1, kind = "gull", n = max(5, min(c.gull_max, floor(5 + fish * c.gull_per_fish + 0.5))), rx = rx, ry = ry,
    act = "roost", fx = rx, fy = ry, x0 = rx, y0 = ry, z0 = 0, x1 = rx, y1 = ry, z1 = 0, cx = rx, cy = ry,
    t0 = now, t1 = now + 0.1, hunger = 0.5, ver = 0, alive = true, obj = "Roost", why = "On the rocks", tgt_kind = "roost", tgt = 0 }
  B.flocks[1] = f
  if push then push(W, f.t1, M.event_id(s, f), f.ver) end
end

-- Start an activity at (x, y) for dur hours; or, if it is far, fly there first.
local function begin(W, s, f, now, act, x, y, dur, tgt_kind, tgt, obj, why)
  local c = C()
  local x0, y0, z0 = M.pos(f, now)
  f.tgt_kind, f.tgt, f.obj, f.why = tgt_kind, tgt or 0, obj, why
  local dx, dy = x - x0, y - y0
  local d = sqrt(dx * dx + dy * dy)
  if d > 0.06 then
    -- a curved flight: out to one side by a share of the way, climbing in the middle
    local side = u(W, s, f, now, 1) < 0.5 and -1 or 1
    local bend = 0.12 + 0.18 * u(W, s, f, now, 2)
    f.act, f.next, f.next_dur = "fly", act, dur
    f.x0, f.y0, f.z0 = x0, y0, z0
    f.x1, f.y1 = x, y
    f.z1 = (act == "follow") and 0.11 or 0
    f.cx, f.cy = (x0 + x) * 0.5 - dy * bend * side, (y0 + y) * 0.5 + dx * bend * side
    f.climb = min(0.35, 0.08 + d * 0.25)
    f.t0, f.t1 = now, now + d * 1.08 / c.gull_speed + 0.01
    f.fx, f.fy = x, y
    return
  end
  f.act, f.next = act, nil
  f.fx, f.fy = x, y
  f.t0, f.t1 = now, now + max(0.05, dur)
end

-- What the world offers the flock now, the best by its hunger and the way there.
local function choose(W, s, f, now, x, y)
  local c = C()
  local V = W.S.v3
  local hunger = f.hunger
  local best, bs = nil, -1e9
  local function offer(score, act, px, py, dur, tk, tg, obj, why)
    local d = sqrt((px - x) * (px - x) + (py - y) * (py - y))
    local sc = score - d / c.gull_speed * 0.6
    if sc > bs then best, bs = { act, px, py, dur, tk, tg, obj, why }, sc end
  end
  -- boats on their grounds, the net coming up: discards and the small fish thrown back
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and not p.at and p.boat and p.act == "fish" then
      local bx, by = boat_pos(p, now)
      offer(1.0 + hunger * 0.7, "follow", bx, by, min(1.0, max(0.1, p.t1 - now)), "boat", p.id, "Follow " .. p.name .. "'s boat",
        p.name .. "'s boat is hauling its net: fish thrown back and the guts to be had")
    end
  end
  -- a catch landed at the dock lately
  local L = s.landing
  if L and now - L.t < c.gull_landing_h and s.dock then
    offer(0.9 + hunger * 0.6, "scavenge", s.dock.dx + 0.03, s.dock.dy + 0.02, 0.4, "dock", 0, "Scraps at the dock",
      string.format("%s landed %.0f FU of fish at the dock: scraps on the boards", L.who or "A boat", L.amt or 0))
  end
  -- fish hanging on a rack (people chase them off when near)
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b.kind == "rack" and b.state == "standing" and (b.stock.drying or 0) > 1 then
      offer(0.45 + hunger * 0.5, "scavenge", b.dx, b.dy, 0.3, "rack", b.id, "Raid the drying rack",
        string.format("%.0f FU of fish hang on a rack by the shore", b.stock.drying))
    end
  end
  -- a plough turning the ground: worms and grubs in the furrow
  local H = s.herds and s.herds.cattle
  if H then
    for _, a in ipairs(H.animals) do
      if a.alive and a.act == "yoked" then
        local q = V.people[a.yoke or 0]
        if q and q.alive and q.act == "till" then
          local qx, qy = M.person_pos(q, now)
          offer(0.55 + hunger * 0.55, "plough", qx, qy, min(0.8, max(0.1, q.t1 - now)), "plough", q.id, "Follow the plough",
            q.name .. "'s plough is turning the ground: worms in the furrow")
        end
      end
    end
  end
  -- a carcass where wolves or a hunter left it
  local Wd = s.wildlife
  for _, cc in ipairs(Wd and Wd.carcasses or {}) do
    if now - cc.t < 24 then
      offer(0.5 + hunger * 0.5, "scavenge", cc.x, cc.y, 0.5, "carcass", 0, "Pick at the carcass",
        "A " .. (cc.sp or "beast") .. " killed " .. (cc.by == "wolves" and "by wolves" or "lately") .. " lies on the ground")
    end
  end
  -- the shore: along the waterline, a stretch of it each time
  local SH = shore(s)
  if #SH > 0 then
    -- (the nearest of a few stretches: they work along the shore, not back and forth across it)
    local i, bd = 0, 1e18
    for q = 1, 4 do
      local j = SH[floor(u(W, s, f, now, 10 + q) * #SH) + 1]
      local jx, jy = mapm.tile_xy(s, j)
      local dd = (jx - x) * (jx - x) + (jy - y) * (jy - y)
      if dd < bd then i, bd = j, dd end
    end
    local sx, sy = mapm.tile_xy(s, i)
    offer(0.3 + hunger * 0.5, "forage", sx, sy, 0.4 + 0.5 * u(W, s, f, now, 4), "shore", i, "Forage along the shore",
      "Hungry: picking along the waterline for crabs and whatever the sea left")
  end
  -- rest: rafted on the water off the roost, or on the rocks, when not hungry
  offer(0.2 + (1 - hunger) * 0.75, "raft", f.rx + 0.02, f.ry + 0.03, 0.5 + 0.7 * u(W, s, f, now, 5), "roost", 0, "Rest on the water",
    "Fed: resting on the water off the rocks")
  return best
end

local EAT = { follow = "gull_eat_boat", scavenge = "gull_eat_scraps", plough = "gull_eat_plough", forage = "gull_eat_shore" }

function M.step(W, s, f, now)
  local c = C()
  local dt = max(0, now - f.t0)
  -- the body: hungrier with the hours, fed by what the activity found
  local eat = EAT[f.act] and c[EAT[f.act]] or 0
  if f.act == "follow" then
    local q = M.people and M.people[f.tgt or 0]
    if not (q and q.alive and q.act == "fish") then eat = 0 end
  end
  -- (asleep on the rocks they burn less)
  local burn = (f.act == "roost") and c.gull_hunger * 0.35 or c.gull_hunger
  local h = f.hunger + burn * dt - eat * dt
  f.hunger = h < 0 and 0 or (h > 1 and 1 or h)
  -- arrived: begin what the flight was for
  if f.act == "fly" and f.next and now >= f.t1 - 1e-9 then
    local nx = f.next
    f.act, f.next = nx, nil
    f.t0, f.t1 = now, now + max(0.05, f.next_dur or 0.3)
    if nx == "follow" or nx == "plough" then
      -- (the boat or the plough may have moved: still there? decide again soon)
      f.t1 = min(f.t1, now + 0.25)
    end
    return
  end
  local x, y = M.pos(f, now)
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  local sr, ss = s.sunrise or 6, s.sunset or 18
  local dark = lh >= ss + 0.3 or lh < sr - 0.4
  local storm = (s.rain_now or 0) >= 2
  local home = sqrt((x - f.rx) * (x - f.rx) + (y - f.ry) * (y - f.ry)) < 0.05
  if dark or storm then
    if home then
      if dark then
        local d = (sr - 0.35 + 0.3 * u(W, s, f, now, 6) - lh) % 24
        f.act, f.fx, f.fy, f.next = "roost", f.rx, f.ry, nil
        f.t0, f.t1 = now, now + max(0.1, d)
        f.obj, f.why, f.tgt_kind, f.tgt = "Roost", "Night: asleep on the rocks", "roost", 0
      else
        f.act, f.fx, f.fy, f.next = "shelter", f.rx, f.ry, nil
        f.t0, f.t1 = now, now + 0.4
        f.obj, f.why, f.tgt_kind, f.tgt = "Shelter", "A downpour: hunched on the rocks out of the wind", "roost", 0
      end
    else
      begin(W, s, f, now, dark and "roost" or "shelter", f.rx, f.ry, 0.4, "roost", 0, "Back to the roost",
        dark and "Dusk: back to the roost for the night" or "A downpour coming in: back to the shelter of the rocks")
    end
    return
  end
  local o = choose(W, s, f, now, x, y)
  if not o then
    f.act, f.t0, f.t1 = "raft", now, now + 0.5
    return
  end
  -- (no activity runs past dusk)
  local left = ss + 0.3 - lh
  local dur = min(o[4], max(0.1, left))
  begin(W, s, f, now, o[1], o[2], o[3], dur, o[5], o[6], o[7], o[8])
  if f.act ~= "fly" then f.t1 = min(f.t1, now + dur) end
end

-- A downpour began (sim/agents/init.lua rain_change): flocks out decide at once.
function M.weather(W, s, now, push)
  M.each(s, function(f)
    if f.act ~= "roost" and f.act ~= "shelter" and f.t1 > now + 0.01 then
      f.ver = f.ver + 1
      local x, y, z = M.pos(f, now)
      f.fx, f.fy = x, y
      if f.act == "fly" then f.act, f.next = "raft", nil; f.z0 = z end
      f.t0, f.t1 = now, now + 0.01
      push(W, f.t1, M.event_id(s, f), f.ver)
    end
  end)
end

return M
