-- The v3 agent layer as a world system (docs/v3/10_AGENTS.md §1, D-078).
--
--   hour(W)        at the start of every game hour (after the planet step at a day's start):
--                  the daily round at hour 0 (life, crops, trees, lanes, hearths, house sites),
--                  then each settlement's hourly board; a change of rain sends reactions to the
--                  people outdoors (each after their own delay).
--   advance(W, t)  live through time up to t: every person's next event (the end of their step,
--                  or an interrupt) in strict (time, id) order. Nothing happens between events —
--                  a walking person's position is a function of time (act.pos).
-- State: W.S.v3 = { settlements, people, households, order, next_* }. The event heap is derived
-- (rebuilt from each person's t1 / pending interrupt), so a reloaded world continues exactly.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local cfgmod = require("sim.config")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local think = require("sim.agents.think")
local settle = require("sim.agents.settle")
-- (settle.lua cannot require econ.lua: econ -> trade -> wild -> stock -> settle; injected here)
settle.econ = require("sim.agents.econ")
settle.migrate = require("sim.agents.migrate")   -- (S16: bob forbids the circular require)
settle.founding = require("sim.agents.founding")   -- (S17, R5)
settle.trade = require("sim.agents.trade")   -- (S18: a young place's dock makes new sea ways)
settle.stone = require("sim.agents.stone")   -- (S19: stone.lua lays outcrops with settle.add_building)
settle.stone.settle = settle
settle.iron = require("sim.agents.iron")   -- (S19: iron.lua lays bogs and plans the forge with settle)
settle.iron.settle = settle
settle.fair = require("sim.agents.fair")   -- (S19: fairs)
local deeds = require("sim.agents.deeds")   -- (S20, D-148)
local danger = require("sim.agents.danger")
local conflict = require("sim.agents.conflict")
local blaze = require("sim.agents.blaze")   -- (S20, R6.5b)
local shake = require("sim.agents.shake")   -- (S20, R6.5f)
local volcano = require("sim.agents.volcano")
local snow = require("sim.agents.snow")   -- (S20, R6.5d)
local tsunami = require("sim.agents.tsunami")   -- (S20, R6.5f)
do
  -- (what one village learns of another: what its people know of places, and of fairs; S20: and
  -- the deeds they know that touch either village)
  local share, heard, dshare = settle.founding.share, settle.fair.heard, deeds.share
  settle.econ.on_learn = function(W, s, o, now) share(W, s, o, now); heard(W, s, o, now); dshare(W, s, o, now) end
end
act.fair = settle.fair
local life = require("sim.agents.life")
local kin = require("sim.agents.kin")
local rng = require("sim.rng")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")
stock.person_pos = require("sim.agents.act").pos
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")
dogs.wild = wild
wild.dogs = dogs
local birds = require("sim.agents.birds")
birds.person_pos = require("sim.agents.act").pos

local sys = { name = "agents" }
-- (act.lua cannot require this module: bob rejects circular requires; it calls through this hook)
act.interrupt_hook = function(W, p, at, reason) return sys.interrupt(W, p, at, reason) end
danger.interrupt = act.interrupt_hook
danger.person_pos = act.pos
-- (S20, R6.5b) a household whose house burnt lodges with others (as newcomers do); a death by lightning
blaze.lodge = function(W, s, hh, now, why) return settle.migrate.lodge(W, s, hh, now, why) end
blaze.kill = function(W, s, p, cause, now) return life.kill_now(W, s, p, cause) end
blaze.rouse_pen = function(W, s, b, now)
  local H = stock.herd_of_pen(s, b)
  if H then stock.rouse(W, s, H, now) end
end
shake.wave = tsunami.from_quake
volcano.wave = tsunami.raise
shake.lodge = blaze.lodge
shake.kill = blaze.kill
local floor, min, max = math.floor, math.min, math.max

-- Event heap (runtime) ------------------------------------------------------------------------
local function rt(W)
  local R = W.v3rt
  if not R then
    R = { t = {}, id = {}, ver = {}, n = 0 }
    W.v3rt = R
    -- rebuild from the people (id order) and the animals (D-089)
    local V = W.S.v3
    for i = 1, #V.order do
      local p = V.people[V.order[i]]
      if p and p.alive then
        local at = p.int_at and p.int_at < p.t1 and p.int_at or p.t1
        sys.push(W, at, p.id, p.ver)
      end
    end
    for _, s in ipairs(V.settlements) do
      stock.each(s, function(H, a) sys.push(W, a.t1, stock.event_id(s, H, a), a.ver) end)
      wild.each(s, function(w) sys.push(W, w.t1, wild.event_id(s, w), w.ver) end)
      birds.each(s, function(f) sys.push(W, f.t1, birds.event_id(s, f), f.ver) end)
    end
  end
  return R
end

local function less(R, i, j)
  local ti, tj = R.t[i], R.t[j]
  return ti < tj or (ti == tj and R.id[i] < R.id[j])
end

function sys.push(W, t, id, ver)
  local R = W.v3rt
  local n = R.n + 1
  R.n = n
  local T, I, Vv = R.t, R.id, R.ver
  local i = n
  while i > 1 do
    local p = (i - i % 2) * 0.5
    local tp = T[p]
    if tp < t or (tp == t and I[p] < id) then break end
    T[i], I[i], Vv[i] = tp, I[p], Vv[p]
    i = p
  end
  T[i], I[i], Vv[i] = t, id, ver
end
wild.push = function(W, t, id, ver) return sys.push(W, t, id, ver) end

-- (S14) Bottom-up: the hole left by the root goes down to a leaf along the smaller children (one
-- comparison a level), then the last entry is sifted up from there (it is large: few levels).
-- Pops come out in the same (time, id) order as any heap's (entries with equal keys differ only by
-- a stale version, which is skipped).
local function pop(R)
  local T, I, Vv = R.t, R.id, R.ver
  local t, id, ver = T[1], I[1], Vv[1]
  local n = R.n
  local lt, li, lv = T[n], I[n], Vv[n]
  T[n], I[n], Vv[n] = nil, nil, nil
  n = n - 1
  R.n = n
  if n > 0 then
    local i, l = 1, 2
    while l < n do
      local r = l + 1
      local tl, tr = T[l], T[r]
      if tr < tl or (tr == tl and I[r] < I[l]) then l = r; tl = tr end
      T[i], I[i], Vv[i] = tl, I[l], Vv[l]
      i = l
      l = i + i
    end
    if l == n then
      T[i], I[i], Vv[i] = T[l], I[l], Vv[l]
      i = l
    end
    while i > 1 do
      local p = (i - i % 2) * 0.5
      local tp = T[p]
      if tp < lt or (tp == lt and I[p] < li) then break end
      T[i], I[i], Vv[i] = tp, I[p], Vv[p]
      i = p
    end
    T[i], I[i], Vv[i] = lt, li, lv
  end
  return t, id, ver
end

-- Interrupt p at time `at` (their reaction), with a reason they will remember in their why.
function sys.interrupt(W, p, at, reason)
  if not p.alive then return end
  if at >= p.t1 then
    -- (S20) the reaction falls after the step under way ends: it interrupts whatever step comes next
    -- (before, the next step of a plan began and the alarm waited for the next decision - a man who
    -- saw a fire as he reached his door went to bed)
    p.int_reason = reason
    if not p.int_late or at < p.int_late then p.int_late = at end
    return
  end
  if p.int_at and p.int_at <= at then return end
  rt(W)
  p.ver = p.ver + 1
  p.int_at = at
  p.int_reason = reason
  sys.push(W, at, p.id, p.ver)
end

-- End p's current step now and go on with their plan (a skipper's wait when the mate is aboard,
-- a mate's ride when the boat lands: D-096).
function sys.skip_step(W, p)
  local now = W.S.v3.now or W.hour
  if p.t1 <= now then return end
  p.ver = p.ver + 1
  p.t1 = now
  p.int_at = nil
  sys.push(W, now, p.id, p.ver)
end

-- Start p's plan (set by think.decide); on failure (no way there) wait a little and re-think.
local function start_plan(W, s, p, now)
  if not act.start(W, s, p, now) then
    think.release(W, s, p)
    local pl = p.plan
    for i = #pl, 1, -1 do pl[i] = nil end
    p.stuck = (p.stuck or 0) + 1
    -- (S17) cut off from the village (a river risen behind a hunter, a ford gone): after trying
    -- every way for an hour they wade or swim across to the nearest ground the village stands on
    local wx, wy = nil, nil
    if p.stuck >= 6 and not p.at then wx, wy = act.way_back(W, s, p) end
    if wx then
      local d = math.sqrt(dsq(wx - p.x) + dsq(wy - p.y))
      pl[1] = { op = "work", kind = "wade", dur = 0.05 + d / 1.2, x1 = wx, y1 = wy, why = "Cut off from the village: wading across the water to get back" }
      p.stuck = 0
    else
      pl[1] = { op = "work", kind = "wait", dur = 0.2 }
      p.why = "Cannot find a way there"
    end
    p.pi = 1
    p.failed, p.failed_t = p.objk, now
    act.start(W, s, p, now)
  else
    p.stuck = nil
  end
end

local function step_person(W, s, p, now)
  local V = W.S.v3
  V.now = now
  if p.int_at and now >= p.int_at - 1e-9 and now < p.t1 - 1e-9 then
    -- interrupted mid-step
    local span = p.t1 - p.t0
    local f = span > 0 and (now - p.t0) / span or 1
    act.finish(W, s, p, now, f)
    p.pi = p.pi + 1
    s = W.S.v3.settlements[p.at or p.sett] or s
    think.release(W, s, p)
    local reason = p.int_reason
    p.int_at, p.int_reason, p.int_late = nil, nil, nil
    think.decide(W, s, p, now, reason)
    start_plan(W, s, p, now)
  else
    p.int_at = nil
    if p.act == "carried" then
      -- held by their carer; hungry, they call for food (sim/agents/kin.lua)
      kin.infant_tick(W, s, p, now, now - p.t0, sys.interrupt, act.pos)
    end
    act.finish(W, s, p, now, 1)
    p.pi = p.pi + 1
    -- (a road leg ends on another settlement's map: the next step is there, D-097)
    s = W.S.v3.settlements[p.at or p.sett] or s
    if p.plan[p.pi] then
      if not act.start(W, s, p, now) then
        think.release(W, s, p)
        think.decide(W, s, p, now, "The way was blocked")
        start_plan(W, s, p, now)
      end
    else
      local reason = p.int_reason
      p.int_reason, p.int_late = nil, nil
      think.decide(W, s, p, now, reason)
      start_plan(W, s, p, now)
    end
  end
  -- (S20) a reaction that fell after the step just ended interrupts the step begun now - unless that
  -- step takes them off the map (on the road or at sea, what was seen or heard there is behind them)
  local late = p.int_late
  if late and p.alive then
    if p.act == "travel" or p.act == "voyage" or (p.riding or 0) ~= 0 then
      p.int_late = nil
    else
      if late < now then late = now end
      if late < p.t1 then p.int_at, p.int_late = late, nil end
    end
  end
  sys.push(W, (p.int_at and p.int_at < p.t1) and p.int_at or p.t1, p.id, p.ver)
end

function sys.advance(W, t)
  local V = W.S.v3
  if not V then return end
  act.people = V.people
  stock.people = V.people
  birds.people = V.people
  act.skip_step = sys.skip_step
  dogs.interrupt_hook = function(W_, p, at, reason) return sys.interrupt(W_, p, at, reason) end
  wild.interrupt_hook = dogs.interrupt_hook
  dogs.push = sys.push
  stock.push = sys.push
  act.stock_push = function(W_, s_, a)
    local H = s_.herds and s_.herds.cattle
    if not H then return end
    local now = W_.S.v3.now or W_.hour
    a.x, a.y = stock.pos(a, now)
    a.tx, a.ty = a.x, a.y
    a.t0, a.t1 = now, now + 0.01
    sys.push(W_, a.t1, stock.event_id(s_, H, a), a.ver)
  end
  act.drown_hook = function(W_, p, from, to) return life.drown(W_, p, from, to) end
  local R = rt(W)
  local P = V.people
  local S = V.settlements
  while R.n > 0 and R.t[1] < t do
    local et, id, ver = pop(R)
    if id >= stock.ID0 then
      -- an animal's step (one queue with the people: strict time order whatever the slicing)
      local sid, kind, k = stock.decode(id)
      local s = S[sid]
      if kind == "wild" then
        local w = s and s.wildlife and wild.byk(s, k)
        if w and w.alive and w.ver == ver then
          V.now = et
          wild.step(W, s, w, et)
          if w.alive then sys.push(W, w.t1, id, w.ver) end
        end
      elseif kind == "dog" then
        local d = s and dogs.byk(s, k)
        if d and d.alive and d.ver == ver then
          V.now = et
          dogs.step(W, s, d, et)
          if d.alive then sys.push(W, d.t1, id, d.ver) end
        end
      elseif kind == "bird" then
        local f = s and birds.byk(s, k)
        if f and f.alive and f.ver == ver then
          V.now = et
          birds.step(W, s, f, et)
          if f.alive then sys.push(W, f.t1, id, f.ver) end
        end
      else
        local H = s and s.herds and s.herds[kind]
        local a = H and H.animals[k]
        if a and a.alive and a.ver == ver then
          V.now = et
          stock.step(W, s, H, a, et)
          sys.push(W, a.t1, id, a.ver)
        end
      end
    else
      local p = P[id]
      if p and p.alive and p.ver == ver then
        step_person(W, S[p.at or p.sett], p, et)
      end
    end
  end
  V.now = t
end

-- Rain began or stopped: reactions with delays (near home sooner; the diligent finish their row).
local function rain_change(W, s, now, r)
  local c = cfgmod.V3
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.act ~= "carried" and not p.at and p.act ~= "travel" and p.act ~= "voyage" and not p.riding then
      local u = rng.float(W.seed, rng.S.V3_THINK, p.key, floor(now) % 4294967296, 99)
      if r > 0 and p.inside == 0 and p.act ~= "sleep" then
        local hb = people.home(W, s, p)
        local d = hb and math.sqrt(dsq(p.x - hb.dx) + dsq(p.y - hb.dy)) or 1
        local hardy = r == 1 and p.dil >= c.rain_hardy and act.OUTDOOR_WORK[p.act]
        if not hardy then
          local lo, hi = c.react_far[1], c.react_far[2]
          if d < 0.25 then lo, hi = c.react_near[1], c.react_near[2] end
          if act.OUTDOOR_WORK[p.act] and r == 1 then lo, hi = lo + 0.1, hi + 0.25 end   -- finishing the row
          sys.interrupt(W, p, now + lo + (hi - lo) * u, r == 2 and "A downpour began" or "It began to rain")
        end
      elseif r == 0 and p.act == "shelter" then
        sys.interrupt(W, p, now + 0.05 + 0.4 * u, "The rain stopped")
      end
    end
  end
end

function sys.hour(W)
  local V = W.S.v3
  if not V then return end
  rt(W)
  local now = W.hour
  local day = floor(now / 24)
  -- (S20, R6.5f) the plates' boundaries gather stress at the planet's day
  if now % 24 == 0 then shake.daily(W, day); volcano.daily(W, day) end
  shake.aftershocks(W, now)   -- (S20)
  for i = 1, #V.settlements do
    local s = V.settlements[i]
    if s.alive then
      -- the daily round at the settlement's own local midnight (settlements round the planet do
      -- theirs at different hours; the planet's step is at UTC midnight)
      local lh0 = clock.local_hour(now, W.grid.lon[s.cell])
      if lh0 < 1 then
        local ld = floor((now - lh0 + 12) / 24)
        if s.day_done ~= ld then
          s.day_done = ld
          life.daily(W, s, ld)
          settle.daily(W, s, ld)
          conflict.missing(W, s, now)
          conflict.defence_daily(W, s, now)
          shake.trapped_daily(W, s, now)
          volcano.ash_daily(W, s, ld)
          snow.daily(W, s, ld, now)
          stock.daily(W, s, ld, sys.push)
          wild.daily(W, s, ld, sys.push)
          dogs.daily(W, s, ld, sys.push)
          birds.daily(W, s, ld, sys.push)
        end
      end
      wild.hourly(W, s, now)
      conflict.raid_call(W, s, now)   -- (S20: the party told when its hour is near)
      local changed, r = settle.hourly(W, s, now)
      snow.hourly(W, s, now)
      -- (S20, R6.5b) a thunderstorm's lightning; whatever burns
      blaze.natural(W, s, now)
      blaze.planet(W, s, now)
      blaze.hourly(W, s, now)
      tsunami.hourly(W, s, now)
      if changed then
        rain_change(W, s, now, r)
        if r == 2 then birds.weather(W, s, now, sys.push) end
      end
    end
  end
end

function sys.init(W)
  W.v3rt = nil
  -- a restored save holds copies, not aliases: point the settlement's shortcuts back at its
  -- buildings, and each person's motion back at their path buffer
  local V = W.S.v3
  if not V then return end
  for i = 1, #V.settlements do
    local s = V.settlements[i]
    for _, k in ipairs({ "granary", "wood_store", "well", "dock", "saltpan" }) do
      if s[k] then s[k] = s.buildings[s[k].id] end
    end
    do
      -- the closed border of the water's cost field (A* has no bounds checks, D-086)
      local N, wc = s.N, s.wcost
      for k = 1, N do wc[k], wc[(N - 1) * N + k], wc[(k - 1) * N + 1], wc[k * N] = -1, -1, -1, -1 end
    end
    for _, boat in ipairs(s.boats or {}) do
      -- (boats from before fisher households: anyone's, sound)
      if boat.owner == nil then boat.owner, boat.cond, boat.trips = 0, 1, 0 end
    end
    if not s.riv then
      -- (a save from before rivers on the local map, D-085)
      s.riv = {}
      for i = 1, s.N * s.N do s.riv[i] = 0 end
      require("sim.agents.map").rivers(W, s, true)
    end
  end
  for i = 1, #V.order do
    local p = V.people[V.order[i]]
    if p and p.mv and p.path_buf and p.mv ~= p.path_buf then p.path_buf = p.mv end
  end
end

-- Persons alive in settlement s, in id order.
function sys.members(W, s)
  local out = {}
  local V = W.S.v3
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive then out[#out + 1] = p end
  end
  return out
end

return sys
