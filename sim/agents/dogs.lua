-- Dogs (roadmap v3 R3, D-105). A household that keeps beasts keeps a dog. By day it goes out with
-- the household's herd and its herder (it runs round the edge of the flock, keeping it together),
-- else it stays about the house; by night it lies at the pen where its household's beasts are. It
-- hears and smells wolves before anyone sees them: one coming near sets it barking and running at
-- them, which wakes its people (they come out to drive the wolves off: think.lua `drive_off`); a
-- wolf finding a barking dog and people coming may turn away (wild.lua). Alone against a pack the
-- dog may be killed.
--   s.dogs = { { k, hh, born, x, y, tx, ty, t0, t1, act, alive, ver } }
-- Events: id = stock.ID0 + settlement id x 100000 + 4 x 20000 + k.
local cfgmod = require("sim.config")
local mapm = require("sim.agents.map")
local stock = require("sim.agents.stock")
local people = require("sim.agents.people")
local rng = require("sim.rng")

local M = {}
local floor, max, min, sqrt, cos, sin = math.floor, math.max, math.min, math.sqrt, math.cos, math.sin
local function C() return cfgmod.V3 end
M.HI = 4

function M.event_id(s, d) return stock.ID0 + s.id * 100000 + M.HI * 20000 + d.k end

function M.byk(s, k)
  local L = s.dogs
  if not L then return nil end
  local d = L[k]
  if d and d.k == k then return d end
  for i = 1, #L do if L[i].k == k then return L[i] end end
  return nil
end

function M.pos(d, t)
  if d.t1 <= d.t0 or (d.act ~= "walk" and d.act ~= "run") then return d.tx, d.ty end
  local f = (t - d.t0) / (d.t1 - d.t0)
  if f <= 0 then return d.x, d.y elseif f >= 1 then return d.tx, d.ty end
  return d.x + (d.tx - d.x) * f, d.y + (d.ty - d.y) * f
end

local function go_to(s, d, now, x, y, speed, act)
  local i = mapm.tile_of(s, x, y)
  if i == 0 or s.cost[i] < 0 then
    local fx, fy = mapm.free_spot(s, x, y, 3)
    if not fx then return false end
    x, y = fx, fy
  end
  local dx, dy = x - d.tx, y - d.ty
  local L = sqrt(dx * dx + dy * dy)
  if L < 1e-4 then return false end
  d.x, d.y = d.tx, d.ty
  d.tx, d.ty = x, y
  d.act, d.t0, d.t1 = act or "walk", now, now + L / speed
  return true
end

local function stand(d, now, act, dur)
  d.x, d.y = d.tx, d.ty
  d.act, d.t0, d.t1 = act, now, now + max(0.05, dur)
end

local function u(W, s, d, now, k)
  return rng.float(W.seed, rng.S.V3_MAP, s.key + d.k * 7919, floor(now * 60) % 4294967296, 600 + k)
end

-- The herd a household's dog goes with: the household's beasts' herd (sheep and goats first).
local function herd_of(W, s, hh)
  if not s.herds then return nil end
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive and a.owner == hh then return H end
      end
    end
  end
  return nil
end
M.herd_of = herd_of

-- Every household that keeps beasts has a dog (founding, and a pup from a neighbour's litter some
-- days after its dog died).
function M.daily(W, s, day, push)
  local c = C()
  local V = W.S.v3
  if not s.herds then return end
  s.dogs = s.dogs or {}
  local have = {}
  for _, d in ipairs(s.dogs) do
    if d.alive then
      have[d.hh] = true
      local H = herd_of(W, s, d.hh)
      d.hk = H and H.kind or nil
    end
  end
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.home ~= 0 and not have[hid] and herd_of(W, s, hid)
      and day - (hh.dog_lost or -999) >= c.dog_pup_days then
      local home = s.buildings[hh.home]
      if home then
        local k = #s.dogs + 1
        local now = day * 24
        local Hh = herd_of(W, s, hid)
        local d = { k = k, hh = hid, born = now, x = home.dx, y = home.dy, tx = home.dx, ty = home.dy, t0 = now, t1 = now + 0.1,
          act = "sit", alive = true, ver = 0, hk = Hh and Hh.kind or nil }
        s.dogs[k] = d
        if push then push(W, d.t1, M.event_id(s, d), d.ver) end
      end
    end
  end
end

-- The wolf nearest (x, y) within r (on the hunt or at a kill), or nil.
local function wolf_near(s, x, y, r, now)
  local Wd = s.wildlife
  if not Wd then return nil end
  local wild = M.wild
  local best, bd = nil, r * r
  for _, w in ipairs(Wd.animals) do
    if w.alive and w.pred and w.act ~= "leave" then
      local wx, wy = wild.pos(w, now)
      local dd = (wx - x) ^ 2 + (wy - y) ^ 2
      if dd < bd then best, bd = w, dd end
    end
  end
  return best
end
M.wolf_near = wolf_near

-- The dog barks: its household (and anyone keeping the night's watch near) is woken (D-105).
local function alarm(W, s, d, w, now)
  local V = W.S.v3
  s.alarm = { t = now, x = d.tx, y = d.ty, hh = d.hh, dog = d.k }
  local hh = V.households[d.hh]
  for _, id in ipairs(hh and hh.members or {}) do
    local q = V.people[id]
    if q and q.alive and not q.at and people.stage(q, now) == "adult" and q.act == "sleep" and (q.drove_t or -99) < now - 1 then
      if M.interrupt_hook then M.interrupt_hook(W, q, now + 0.03 + 0.05 * u(W, s, d, now, q.id % 7), "The dog is barking at the pen") end
    end
  end
  s.acc.barks = (s.acc.barks or 0) + 1
end

function M.step(W, s, d, now)
  local c = C()
  local V = W.S.v3
  local hh = V.households[d.hh]
  if not hh or #hh.members == 0 then
    d.alive = false
    return
  end
  local home = s.buildings[hh.home or 0]
  local lh = require("sim.agents.clock").local_hour(now, W.grid.lon[s.cell])
  local night = lh < (s.sunrise or 6) - 0.5 or lh > (s.sunset or 18) + 0.5
  local H = d.hk and s.herds[d.hk] or nil
  -- wolves: noticed by ear and nose and barked at; the dog holds its post (the pen by night, the
  -- herd by day) and runs only at a wolf that comes close to it; it fights at most once a night
  local w = ((s.wolves_n or 0) > 0 or d.woken) and wolf_near(s, d.tx, d.ty, c.dog_alert, now) or nil
  d.woken = nil
  if w then
    local wx, wy = M.wild.pos(w, now)
    local dd = sqrt((wx - d.tx) ^ 2 + (wy - d.ty) ^ 2)
    if now - (d.barked or -99) > 0.5 then d.barked = now; alarm(W, s, d, w, now) end
    if dd < c.wolf_reach * 1.5 and now - (d.fought or -99) > 12 then
      d.fought = now
      local alone = true
      for _, id in ipairs(s.members) do
        local q = V.people[id]
        if q and q.alive and q.inside == 0 and q.act ~= "sleep" and (q.x - d.tx) ^ 2 + (q.y - d.ty) ^ 2 < 0.04 then alone = false end
      end
      if alone and u(W, s, d, now, 1) < c.wolf_kill_dog then
        d.alive, d.died = false, now
        hh.dog_lost = floor(now / 24)
        s.acc.dogs_killed = (s.acc.dogs_killed or 0) + 1
        for _, id in ipairs(hh.members) do
          local q = V.people[id]
          if q and q.alive then people.remember(q, now, "The wolves killed our dog at the pen") end
        end
        return
      end
    end
    if dd < c.dog_charge and dd > c.wolf_reach then
      local step = min(dd - c.wolf_reach, 0.1)
      if go_to(s, d, now, d.tx + (wx - d.tx) / dd * step, d.ty + (wy - d.ty) / dd * step, c.dog_run, "run") then return end
    end
    stand(d, now, "bark", 0.15)
    return
  end
  -- with the herd and its herder by day: round the edge of the flock
  local herder = H and stock.herder(W, s, H)
  if H and H.out and herder and not night then
    -- (round the flock, which spreads round its herder)
    local mx, my = stock.person_pos(herder, now)
    local r = 0.06 + 0.012 * sqrt(#H.animals)
    local ang = now * 1.7 + d.k * 2.1 + u(W, s, d, now, 2) * 0.6
    local gx, gy = mx + r * cos(ang), my + r * sin(ang)
    if go_to(s, d, now, gx, gy, c.dog_trot, "walk") then
      if d.t1 - now < 0.25 then d.t1 = now + 0.25 end
      return
    end
    stand(d, now, "sit", 0.4)
    return
  end
  -- by night at the pen of the household's beasts; else by the house
  local bx, by
  if night and H then
    local pen = s.buildings[H.pen]
    if pen then bx, by = pen.dx + 0.02 * cos(d.k), pen.dy + 0.02 * sin(d.k) end
  end
  if not bx and home then bx, by = home.dx + 0.02 * cos(d.k * 1.3), home.dy + 0.015 end
  if bx then
    local dd = (bx - d.tx) ^ 2 + (by - d.ty) ^ 2
    if dd > 0.0004 then
      if go_to(s, d, now, bx, by, c.dog_trot, "walk") then return end
    end
  end
  -- (by night a dog at the pen wakes at once to wolves: wild.lua interrupts it)
  stand(d, now, night and "lie" or (u(W, s, d, now, 3) < 0.5 and "lie" or "sit"), night and 1.5 or 0.8 + 0.8 * u(W, s, d, now, 4))
end

-- Interrupting a dog (a wolf came near): its next step now.
function M.interrupt(W, s, d, now)
  d.woken = true
  d.x, d.y = M.pos(d, now)
  d.tx, d.ty = d.x, d.y
  d.t0, d.t1 = now, now + 0.01
  d.act = "sit"
  d.ver = (d.ver or 0) + 1
  if M.push then M.push(W, d.t1, M.event_id(s, d), d.ver) end
end

return M
