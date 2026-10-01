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
local cfgmod = require("sim.config")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local think = require("sim.agents.think")
local settle = require("sim.agents.settle")
-- (settle.lua cannot require econ.lua: econ -> trade -> wild -> stock -> settle; injected here)
settle.econ = require("sim.agents.econ")
local life = require("sim.agents.life")
local kin = require("sim.agents.kin")
local rng = require("sim.rng")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")
stock.person_pos = require("sim.agents.act").pos
local wild = require("sim.agents.wild")

local sys = { name = "agents" }
-- (act.lua cannot require this module: bob rejects circular requires; it calls through this hook)
act.interrupt_hook = function(W, p, at, reason) return sys.interrupt(W, p, at, reason) end
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
    local p = floor(i / 2)
    if T[p] < t or (T[p] == t and I[p] < id) then break end
    T[i], I[i], Vv[i] = T[p], I[p], Vv[p]
    i = p
  end
  T[i], I[i], Vv[i] = t, id, ver
end
wild.push = function(W, t, id, ver) return sys.push(W, t, id, ver) end

local function pop(R)
  local T, I, Vv = R.t, R.id, R.ver
  local t, id, ver = T[1], I[1], Vv[1]
  local n = R.n
  local lt, li, lv = T[n], I[n], Vv[n]
  T[n], I[n], Vv[n] = nil, nil, nil
  n = n - 1
  R.n = n
  if n > 0 then
    local i = 1
    while true do
      local l = i * 2
      if l > n then break end
      local r = l + 1
      local c = l
      if r <= n and (T[r] < T[l] or (T[r] == T[l] and I[r] < I[l])) then c = r end
      if T[c] > lt or (T[c] == lt and I[c] > li) then break end
      T[i], I[i], Vv[i] = T[c], I[c], Vv[c]
      i = c
    end
    T[i], I[i], Vv[i] = lt, li, lv
  end
  return t, id, ver
end

-- Interrupt p at time `at` (their reaction), with a reason they will remember in their why.
function sys.interrupt(W, p, at, reason)
  if not p.alive or at >= p.t1 then
    p.int_reason = reason
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
    pl[1] = { op = "work", kind = "wait", dur = 0.2 }
    p.pi = 1
    p.why = "Cannot find a way there"
    p.failed, p.failed_t = p.objk, now
    act.start(W, s, p, now)
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
    p.int_at, p.int_reason = nil, nil
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
      p.int_reason = nil
      think.decide(W, s, p, now, reason)
      start_plan(W, s, p, now)
    end
  end
  sys.push(W, p.t1, p.id, p.ver)
end

function sys.advance(W, t)
  local V = W.S.v3
  if not V then return end
  act.people = V.people
  act.skip_step = sys.skip_step
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
        local d = hb and math.sqrt((p.x - hb.dx) ^ 2 + (p.y - hb.dy) ^ 2) or 1
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
          stock.daily(W, s, ld, sys.push)
          wild.daily(W, s, ld, sys.push)
        end
      end
      wild.hourly(W, s, now)
      local changed, r = settle.hourly(W, s, now)
      if changed then rain_change(W, s, now, r) end
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
    for _, k in ipairs({ "granary", "wood_store", "well", "dock" }) do
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
