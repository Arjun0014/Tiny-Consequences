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

-- Everyone on map s now: its own people at home and the visitors there.
local OUT = {}
local function each_on_map(W, s, fn)
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and not p.at then fn(p) end
  end
  for i = 1, #(s.visitors or {}) do
    local p = V.people[s.visitors[i]]
    if p and p.alive and p.at == s.id then fn(p) end
  end
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

return M
