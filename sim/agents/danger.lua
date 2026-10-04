-- Danger noticed (D-148, docs/v3/50_PRIMITIVES.md §3): a cue - something seen, a sound - at a place
-- on a map reaches the people there by distance and state: the awake and outdoors see within sight
-- (short at night), everyone hears within a sound's reach (half indoors), sleepers wake only to the
-- nearer part of it. Who notices gets `p.alarm` and is interrupted after their own reaction delay
-- with the reason; what they then do is their own choice (think.decide's urgent branch). The kind
-- of danger belongs to its own system (wolves, a stranger taking beasts, fire, water ...).
--   notice(W, s, x, y, now, cue) -> the people of map s who notice, each with how ("saw", "heard")
--   raise(W, p, cue, x, y, now, how) -> p.alarm set, p interrupted
-- cue = { kind, src (a person id or 0), sight (range by day; at night cue.night or sight x
--         night_k), sound (range, 0 none), why (the reason they will give), loud (wakes sleepers) }
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

-- (set by sim/agents/init.lua: bob forbids act/think requiring this and this requiring them)
M.interrupt = nil
M.person_pos = nil

local function night_at(W, s, now)
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  return lh < (s.sunrise or 6) - 0.5 or lh > (s.sunset or 18) + 0.5
end
M.night_at = night_at

-- Everyone on map s now: its own people at home and the visitors there. (S24) Over a snapshot of the lists: a death
-- inside fn takes the dead off s.members, and walking the live list skipped the one after each death (a quake, a
-- flood or a meteor that killed one spared the next).
local OUT = {}
local SNAP, depth = {}, 0
local function each_on_map(W, s, fn)
  local V = W.S.v3
  depth = depth + 1
  local b = SNAP[depth]
  if not b then b = {}; SNAP[depth] = b end
  local m, vis = s.members, s.visitors
  local n1 = #m
  for i = 1, n1 do b[i] = m[i] end
  local n2 = vis and #vis or 0
  for i = 1, n2 do b[n1 + i] = vis[i] end
  for i = 1, n1 + n2 do
    local p = V.people[b[i]]
    if p and p.alive then
      if i <= n1 then
        if not p.at then fn(p) end
      elseif p.at == s.id then
        fn(p)
      end
    end
  end
  depth = depth - 1
end
M.each_on_map = each_on_map

function M.notice(W, s, x, y, now, cue)
  local c = C()
  local out = {}
  local night = night_at(W, s, now)
  local sight = night and (cue.night or (cue.sight or 0) * c.night_sight_k) or (cue.sight or 0)
  local sound = cue.sound or 0
  each_on_map(W, s, function(p)
    if p.id == cue.skip or p.act == "carried" or p.act == "travel" or p.act == "voyage" or (p.riding or 0) ~= 0 then return end
    local px, py = M.person_pos(p, now)
    local d = sqrt((px - x) * (px - x) + (py - y) * (py - y))
    local how
    local asleep = p.act == "sleep"
    if not asleep and p.inside == 0 and d <= sight then how = "saw"
    elseif sound > 0 then
      local r = sound * (p.inside ~= 0 and 0.5 or 1) * (asleep and (cue.loud and c.wake_k or 0) or 1)
      if d <= r then how = "heard" end
    end
    if how then out[#out + 1] = { p = p, how = how, d = d } end
  end)
  return out
end

-- p notices the danger: their alarm, and an interrupt after their own reaction (sooner awake and
-- near, later asleep or far).
function M.raise(W, p, cue, x, y, now, how)
  local c = C()
  -- (S25, D-206) a meteor's strike is the danger for its first moments: the quake it makes and the fires its flash lights
  -- the same instant do not take its place (whoever is near it runs from it first)
  local a = p.alarm
  if a and a.kind == "impact" and cue.kind ~= "impact" and now - a.t < c.imp_alarm_h * 0.5 then return end
  p.alarm = { kind = cue.kind, x = x, y = y, t = now, src = cue.src or 0, how = how }
  local v = rng.float(W.seed, rng.S.V3_CONFLICT, p.key, floor(now * 60) % 4294967296, 7)
  local delay = (p.act == "sleep" and c.wake_delay or c.notice_delay) * (0.5 + v)
  if M.interrupt then M.interrupt(W, p, now + delay, cue.why) end
end

-- Notice and raise in one: returns the list of those who noticed.
function M.cue(W, s, x, y, now, cue)
  local L = M.notice(W, s, x, y, now, cue)
  for i = 1, #L do M.raise(W, L[i].p, cue, x, y, now, L[i].how) end
  return L
end

-- Is p's alarm still fresh (within `h` hours)?
function M.fresh(p, now, h)
  local a = p.alarm
  return a ~= nil and now - a.t <= (h or C().alarm_h)
end

-- (S22) Fire as a danger at a place, for anything that sees and smells it (beasts, wild animals, dogs):
-- the burning things of map s (blaze.lua's s.burn) by their own extent - a tree's crown, a building's
-- walls, a field's edges - never by a point at their middle.
local mapm = require("sim.agents.map")
function M.fire_edge(s, f, x, y)
  if f.kind == "bld" then
    local b = s.buildings[f.ref]
    if b and b.ix0 then
      local T = s.T
      local x0, y0 = mapm.ixy_xy(s, b.ix0, b.iy0)
      local ex = max(x0 - 0.5 * T - x, 0, x - (x0 + (b.w - 0.5) * T))
      local ey = max(y0 - 0.5 * T - y, 0, y - (y0 + (b.h - 0.5) * T))
      return sqrt(ex * ex + ey * ey)
    end
  elseif f.kind == "plot" then
    local pl = s.plots[f.ref]
    if pl then
      local ex = max(pl.x0 - x, 0, x - pl.x1)
      local ey = max(pl.y0 - y, 0, y - pl.y1)
      return sqrt(ex * ex + ey * ey)
    end
  end
  local dx, dy = f.x - x, f.y - y
  return max(0, sqrt(dx * dx + dy * dy) - C().fire_crown)
end

-- What burns within r of (x, y) by its edge: the nearest (and how far its edge is) and the way away
-- from all of it about (x, y) - each burning thing pushing by its heat, the nearer the harder - so the
-- way out of a fire front runs away from the front, and between two fires not into either. Nothing
-- burning within r: nil.
function M.fire_near(s, x, y, r)
  local B = s.burn
  if not B or #B == 0 then return nil end
  local best, bd = nil, r
  local px, py = 0, 0
  for i = 1, #B do
    local f = B[i]
    if not f.out and f.heat > 0.02 then
      local d = M.fire_edge(s, f, x, y)
      if d < r then
        if d < bd or not best then best, bd = f, d end
        local dx, dy = x - f.x, y - f.y
        local l = sqrt(dx * dx + dy * dy)
        if l > 1e-6 then
          local w = (0.3 + f.heat) * (1 - d / r) * (1 - d / r)
          px, py = px + dx / l * w, py + dy / l * w
        end
      end
    end
  end
  if not best then return nil end
  return best, bd, px, py
end

return M
