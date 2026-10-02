-- Dogs (roadmap v3 R3, D-105). A household that keeps beasts keeps a dog. By day it goes out with
-- the household's herd and its herder (it runs round the edge of the flock, keeping it together),
-- else it stays about the house; by night it lies at the pen where its household's beasts are. It
-- hears and smells wolves before anyone sees them: one coming near sets it barking and running at
-- them, which wakes its people (they come out to drive the wolves off: think.lua `drive_off`); a
-- wolf finding a barking dog and people coming may turn away (wild.lua). Alone against a pack the
-- dog may be killed.
--   s.dogs = { { k, hh, born, x, y, tx, ty, t0, t1, act, alive, ver } }
-- Events: id = stock.ID0 + settlement id x 100000 + 4 x 20000 + k.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local cfgmod = require("sim.config")
local mapm = require("sim.agents.map")
local stock = require("sim.agents.stock")
local people = require("sim.agents.people")
local rng = require("sim.rng")

local M = {}
local floor, max, min, sqrt, cos, sin = math.floor, math.max, math.min, math.sqrt, dmath.cos, dmath.sin
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
  -- (S17: a pup comes from a litter in the village: there must be another dog to have had one; a
  -- village founded with the world came with its dogs)
  local any = #s.dogs == 0 and not s.founding
  for _, d in ipairs(s.dogs) do if d.alive then any = true end end
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.home ~= 0 and not have[hid] and herd_of(W, s, hid) and any and not hh.dog_with
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
      local dd = dsq(wx - x) + dsq(wy - y)
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

-- (S17) A household gone to live elsewhere with its dog: the dog comes onto the new map with them.
function M.come(W, o, hh, x, y, now)
  local dw = hh.dog_with
  if not dw then return end
  hh.dog_with = nil
  o.dogs = o.dogs or {}
  local k = #o.dogs + 1
  local d = { k = k, hh = hh.id, born = dw.born, x = x, y = y, tx = x, ty = y, t0 = now, t1 = now + 0.05,
    act = "sit", alive = true, ver = 0, why = "Come with its people" }
  o.dogs[k] = d
  if M.push then M.push(W, d.t1, M.event_id(o, d), d.ver) end
end

function M.step(W, s, d, now)
  local c = C()
  local V = W.S.v3
  local hh = V.households[d.hh]
  if not hh or #hh.members == 0 then
    d.alive = false
    return
  end
  -- (S17) its household leaving for good (moving away or founding a place): it keeps at their heels
  -- to the gate and goes with them when the last of them takes the road
  local mg = hh.found or hh.migrate
  if mg and not (hh.found and hh.found.arrived) and not (hh.migrate and hh.migrate.arrived) then
    local lead, ld = nil, 1e9
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and q.sett == s.id and not q.at and q.act ~= "travel" and q.act ~= "carried" then
        local qx, qy = stock.person_pos(q, now)
        local dd = dsq(qx - d.tx) + dsq(qy - d.ty)
        if (q.objk == "found" or q.objk == "migrate") and dd < ld then lead, ld = q, dd end
      end
    end
    local gone = true
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      -- (S18: aboard their boat is gone too: the dog jumps in after them)
      if q and q.alive and q.sett == s.id and not q.at and q.act ~= "travel" and q.act ~= "carried" and not q.boat
        and (q.riding or 0) == 0 then gone = false end
    end
    if gone and now >= (mg.depart or now) then
      d.alive, d.left = false, now
      hh.dog_with = { born = d.born }
      return
    end
    if lead then
      local qx, qy = stock.person_pos(lead, now)
      local dd = sqrt(ld)
      d.why = "At " .. lead.name .. "'s heels: the household is leaving"
      if dd > 0.04 and go_to(s, d, now, qx + 0.02, qy - 0.015, c.dog_trot, "walk") then return end
      stand(d, now, "sit", 0.2)
      return
    end
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
    local dd = sqrt(dsq(wx - d.tx) + dsq(wy - d.ty))
    if now - (d.barked or -99) > 0.5 then d.barked = now; alarm(W, s, d, w, now) end
    if dd < c.wolf_reach * 1.5 and now - (d.fought or -99) > 12 then
      d.fought = now
      local alone = true
      for _, id in ipairs(s.members) do
        local q = V.people[id]
        if q and q.alive and q.inside == 0 and q.act ~= "sleep" and dsq(q.x - d.tx) + dsq(q.y - d.ty) < 0.04 then alone = false end
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
      d.why = "Running at a wolf"
      if go_to(s, d, now, d.tx + (wx - d.tx) / dd * step, d.ty + (wy - d.ty) / dd * step, c.dog_run, "run") then return end
    end
    d.why = "Barking at a wolf"
    stand(d, now, "bark", 0.15)
    return
  end
  -- with the herd and its herder by day (S16): it fetches a beast that strays (running out beyond
  -- it, so that it turns back from the dog toward the herd) and keeps after that one until it is
  -- back; goes out to see off a wild animal that comes near the herd; when the herder moves the
  -- herd on it works from the far side of the herd from them (the balance point), on the side of
  -- it it is on; otherwise it keeps a post of its own at the herd's edge - where the herd spreads
  -- most - lying there watching, and now and then gets up and goes round the herd to look from
  -- another side. It goes round the herd, not through it.
  local herder = H and stock.herder(W, s, H)
  if H and H.out and herder and not night then
    local mx, my = stock.person_pos(herder, now)
    local far, fd, fx, fy = nil, 0, 0, 0
    local cx, cy, n = 0, 0, 0
    for _, a in ipairs(H.animals) do
      if a.alive and (a.led or 0) == 0 and a.act ~= "pen" and a.act ~= "held" and a.act ~= "yoked" then
        local ax, ay = stock.pos(a, now)
        cx, cy, n = cx + ax, cy + ay, n + 1
        local dd = dsq(ax - mx) + dsq(ay - my)
        if dd > fd then far, fd, fx, fy = a, dd, ax, ay end
      end
    end
    if n > 0 then
      cx, cy = cx / n, cy / n
      local S = stock.SP(far.sp)
      local R = stock.range(S, n)
      fd = sqrt(fd)
      -- the straggler it is fetching (kept until it is back with the others), or a new one
      local st = d.fetch and H.animals[d.fetch]
      if st and (not st.alive or st.act == "pen" or (st.led or 0) ~= 0) then st = nil end
      if st then
        fx, fy = stock.pos(st, now)
        fd = sqrt(dsq(fx - mx) + dsq(fy - my))
        if fd < R * 1.1 then st = nil end
      end
      if not st and fd > R * 1.6 then st = far end
      d.fetch = st and st.k or nil
      local dx0, dy0 = d.tx, d.ty
      if st then
        local ux, uy = (fx - mx) / max(1e-6, fd), (fy - my) / max(1e-6, fd)
        d.why = "Fetching a straggler back to the herd"
        if go_to(s, d, now, fx + ux * S.sep * 1.4, fy + uy * S.sep * 1.4, c.dog_run, "run") then return end
      end
      -- a wild animal near the herd: out to see it off (it will run from a dog running at it)
      local Wd = s.wildlife
      if Wd and now - (d.chased or -99) > 1.5 then
        local wild = M.wild
        local wr2 = (R + c.dog_see_off) * (R + c.dog_see_off)
        local tw, td = nil, wr2
        for _, w in ipairs(Wd.animals) do
          if w.alive and not w.pred and w.act ~= "leave" then
            local wx, wy = wild.pos(w, now)
            local dd = dsq(wx - cx) + dsq(wy - cy)
            if dd < td then tw, td = w, dd end
          end
        end
        if tw then
          local wx, wy = wild.pos(tw, now)
          local dd = sqrt(dsq(wx - dx0) + dsq(wy - dy0))
          if dd > 0.04 then
            local step = min(dd - 0.02, 0.2)
            d.why = "Seeing off a " .. tw.sp .. " near the herd"
            d.chasing = tw.k
            if go_to(s, d, now, dx0 + (wx - dx0) / dd * step, dy0 + (wy - dy0) / dd * step, c.dog_run, "run") then return end
          end
          d.chased = now
        end
      end
      d.chasing = nil
      local ox, oy = cx - mx, cy - my
      local ol = sqrt(ox * ox + oy * oy)
      local gx, gy, sp
      -- (where it is round the herd now: the angle from the herd's middle)
      local ra = dmath.atan2(dy0 - cy, dx0 - cx)
      local rd = sqrt(dsq(dx0 - cx) + dsq(dy0 - cy))
      if herder.act == "walk" and ol > 1e-6 then
        -- the herder moving them on: to the balance point, the far side of the herd from the
        -- herder, a little to the side of it the dog is on
        ox, oy = ox / ol, oy / ol
        local side = (ox * (dy0 - cy) - oy * (dx0 - cx)) >= 0 and 1 or -1
        local ba = dmath.atan2(oy, ox) + side * (0.25 + 0.35 * u(W, s, d, now, 5))
        gx, gy = cx + cos(ba) * (R * 0.7 + 0.05), cy + sin(ba) * (R * 0.7 + 0.05)
        sp = c.dog_trot * 1.2
        d.why = "Working behind the herd"
      else
        -- its post: where the herd spreads most when it chose it; it keeps it until the herd has
        -- drifted off it, or until it gets up to look from another side
        local px, py = d.px, d.py
        local pd = px and sqrt(dsq(px - cx) + dsq(py - cy)) or 0
        if not px or pd > R * 1.8 or pd < R * 0.6 or now > (d.pt or 0) then
          local wa, wd = ra, -1
          for _, a in ipairs(H.animals) do
            if a.alive and (a.led or 0) == 0 and a.act ~= "pen" and a.act ~= "held" and a.act ~= "yoked" then
              local ax, ay = a.tx, a.ty
              local dd = dsq(ax - cx) + dsq(ay - cy)
              if dd > wd then wa, wd = dmath.atan2(ay - cy, ax - cx), dd end
            end
          end
          -- (not between the herd and its herder)
          local ha = dmath.atan2(my - cy, mx - cx)
          local dh = (wa - ha + 9.42477796) % 6.28318531 - 3.14159265
          if dh > -0.6 and dh < 0.6 then wa = ha + (dh >= 0 and 0.6 or -0.6) end
          wa = wa + (u(W, s, d, now, 6) - 0.5) * 0.8
          -- (not where another dog of the herd already watches from: round to the other side of it)
          for _, o in ipairs(s.dogs) do
            if o ~= d and o.alive and o.hk == d.hk and o.px then
              local oa = dmath.atan2(o.py - cy, o.px - cx)
              local df = (wa - oa + 9.42477796) % 6.28318531 - 3.14159265
              if df > -0.8 and df < 0.8 then wa = oa + (df >= 0 and 1.2 or -1.2) end
            end
          end
          px, py = cx + cos(wa) * (R + 0.05), cy + sin(wa) * (R + 0.05)
          d.px, d.py = px, py
          d.pt = now + 0.8 + 2 * u(W, s, d, now, 7)
        end
        gx, gy = px, py
        sp = c.dog_trot
        d.why = "Watching the herd"
      end
      local dd = sqrt(dsq(gx - dx0) + dsq(gy - dy0))
      if dd > 0.03 then
        -- round the herd, not through it: the way there by its edge, a stretch at a time
        local ga = dmath.atan2(gy - cy, gx - cx)
        local da = (ga - ra + 9.42477796) % 6.28318531 - 3.14159265
        if rd < R * 1.2 or (da > 0.7 or da < -0.7) then
          local na = ra + max(-0.7, min(0.7, da))
          local nr = max(R + 0.03, min(rd, R * 1.6))
          local ex, ey = cx + cos(na) * nr, cy + sin(na) * nr
          if sqrt(dsq(ex - dx0) + dsq(ey - dy0)) > 0.01 then gx, gy = ex, ey end
          if d.why == "Watching the herd" then d.why = "Going round the herd" end
        end
        if go_to(s, d, now, gx, gy, sp, "walk") then return end
      end
      d.why = herder.act == "walk" and "Working behind the herd" or "Watching the herd"
      stand(d, now, u(W, s, d, now, 3) < 0.6 and "lie" or "sit", herder.act == "walk" and 0.05 or (0.15 + 0.3 * u(W, s, d, now, 4)))
      return
    end
  end
  -- by night at the pen of the household's beasts; else by the house
  d.px, d.py, d.fetch = nil, nil, nil
  local bx, by
  if night and H then
    local pen = s.buildings[H.pen]
    if pen then bx, by = pen.dx + 0.02 * cos(d.k), pen.dy + 0.02 * sin(d.k) end
  end
  if not bx and home then bx, by = home.dx + 0.02 * cos(d.k * 1.3), home.dy + 0.015 end
  d.why = (night and H) and "Lying at the pen for the night" or "About the house"
  if bx then
    local dd = dsq(bx - d.tx) + dsq(by - d.ty)
    if dd > 0.0004 then
      d.why = (night and H) and "To the pen for the night" or "Home to the house"
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
