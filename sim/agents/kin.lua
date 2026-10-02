-- Care across the generations (docs/v3/10_AGENTS.md §7, D-081): who holds an infant, feeding,
-- minders, and apprentices' mentors.
--
-- An infant (p.act "carried") is always held by somebody: p.carer (= p.carrier, the person whose
-- position and building they share); that person's q.babe is the infant. Infants get hungry
-- (faster awake than asleep); a hungry infant calls its nursing mother (an interrupt: she stops,
-- or comes to wherever the baby is) or, once weaned, whoever holds it feeds it porridge from the
-- home's meals. A carer holding a baby cannot fell, build or haul; to work she leaves the baby with
-- a minder at home (a grandparent, an older child, the father at home) and takes it back when she
-- is home again. A baby nobody feeds loses health.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local cfgmod = require("sim.config")
local people = require("sim.agents.people")

local M = {}
local min, max, sqrt = math.min, math.max, math.sqrt
local function C() return cfgmod.V3 end

-- What a person holding a baby can still do (fit multiplier by objective kind).
M.BABE_FIT = { fell = 0.08, build = 0.08, haul_logs = 0.1, deliver = 0.1, grub = 0.1, repair = 0.15, thresh = 0.2,
  till = 0.3, harvest = 0.45, haul_grain = 0.3, sow = 0.7, tend = 0.75, forage = 0.85, fetch_wood = 0.5,
  fetch_food = 0.8, fetch_water = 0.7, cook = 1.1, craft = 1.0, play = 0, fish = 0, mend_boat = 0.15, smoke = 1.0,
  spin = 1.0, sew = 0.9, weave = 0.7, dress = 0.5, mend_clothes = 1.0, haul_flax = 0.3, herd = 0.6, shear = 0.3, mow = 0.2,
  feed = 0.7 }

-- Objectives during which a mother at home takes her baby back from its minder.
M.HOME_KINDS = { sleep = true, rest = true, eat = true, cook = true, craft = true, sit_out = true, nurse = true,
  feed_babe = true, shelter = true, visit = true, smoke = true, spin = true, weave = true, sew = true, mend_clothes = true }

-- Infant ib is now held by q (or nobody).
function M.give(W, ib, q)
  local V = W.S.v3
  local old = V.people[ib.carer or 0]
  if old and old.babe == ib.id then old.babe = 0 end
  if q then
    -- (holding an older one too: both stay with them; the newest is the one in their arms)
    ib.carer, ib.carrier = q.id, q.id
    q.babe = ib.id
    ib.x, ib.y, ib.inside = q.x, q.y, q.inside
  else
    ib.carer, ib.carrier = 0, 0
  end
  return true
end

-- The infant p's mother can nurse it?
function M.nursing_mother(W, ib, now)
  local m = W.S.v3.people[ib.mother or 0]
  if m and m.alive and (m.nursing or 0) > now then return m end
  return nil
end

-- A minder for mother's baby at her home: a household member inside it (or by its door), awake
-- and not holding a baby: a grandparent first, then an older child (9+), then the father.
function M.minder(W, s, mother, home, now)
  if not home then return nil end
  local V = W.S.v3
  local hh = V.households[mother.hh]
  if not hh then return nil end
  local best, bs = nil, -1
  for i = 1, #hh.members do
    local q = V.people[hh.members[i]]
    if q and q.alive and q ~= mother and q.babe == 0 and q.act ~= "sleep" and q.act ~= "carried" then
      local near = q.inside == home.id or (q.inside == 0 and sqrt(dsq(q.x - home.dx) + dsq(q.y - home.dy)) < 0.06)
      if near then
        local st, a = people.stage(q, now)
        local sc = -1
        if st == "elder" then sc = 3
        elseif (st == "child" and a >= 9) or st == "apprentice" then sc = 2
        elseif st == "adult" then sc = 1 end
        if sc > bs then best, bs = q, sc end
      end
    end
  end
  return best
end

-- A mother at home takes her baby back from a minder in the same building.
function M.reclaim(W, s, p)
  local V = W.S.v3
  if p.inside == 0 then return end
  for i = 1, #p.kids do
    local ib = V.people[p.kids[i]]
    if ib and ib.alive and ib.act == "carried" and ib.carer ~= p.id and p.babe == 0 then
      local c = V.people[ib.carer or 0]
      if not c or not c.alive or c.inside == p.inside then
        M.give(W, ib, p)
        return ib
      end
    end
  end
end

-- An infant's half-hour (sim/agents/init.lua): hunger, and calling for food.
function M.infant_tick(W, s, ib, now, dt, interrupt, pos)
  local c = C()
  local V = W.S.v3
  local q = V.people[ib.carer or 0]
  if not q or not q.alive then
    -- the carer is gone: the mother, else whoever of the household is nearest
    q = V.people[ib.mother or 0]
    if not (q and q.alive) then
      q = nil
      local hh = V.households[ib.hh]
      if hh then
        for i = 1, #hh.members do
          local o = V.people[hh.members[i]]
          if o and o.alive and o ~= ib and o.act ~= "carried" and people.stage(o, now) ~= "child" then q = o; break end
        end
      end
    end
    if q then M.give(W, ib, q) end
  end
  if q then
    ib.x, ib.y = pos(q, now)
    ib.inside = q.inside
  end
  local asleep = q and q.act == "sleep"
  ib.hunger = min(1, ib.hunger + dt * (asleep and c.infant_hunger_night or c.infant_hunger))
  if ib.hunger >= 1 then ib.health = max(0, ib.health - c.infant_starve * dt) end
  if ib.hunger > c.infant_feed and (ib.called or -1) < now - 0.75 then
    ib.called = now
    local m = M.nursing_mother(W, ib, now)
    local who = m or q
    if not m and asleep then who = nil end   -- (a weaned toddler sleeps through; breakfast in the morning)
    if not m and q and who then
      -- weaned: only worth calling if there is something at home to feed them (or to cook)
      local home = people.home(W, s, q)
      if not (home and ((home.stock.meals or 0) >= c.porridge or require("sim.agents.people").pantry(home) >= 0.3)) then who = nil end
    end
    if who and who.act == "travel" then
      -- (S16: on the road the baby is fed as they walk - at the breast, or a toddler from the
      -- provisions - and the walk goes on)
      if m then
        ib.hunger = max(0, ib.hunger - 0.6)
      elseif (who.prov or 0) >= 0.1 then
        who.prov = who.prov - 0.1
        ib.hunger = max(0, ib.hunger - 0.5)
      end
      who = nil
    end
    -- (a weaned toddler waits for the cooking; a baby at the breast does not)
    if who and who.objk ~= "nurse" and who.objk ~= "feed_babe" and (who.objk ~= "cook" or m) then
      interrupt(W, who, now + 0.02 + 0.08 * ((ib.key % 97) / 97), ib.name .. " is hungry")
    end
  end
end

-- Mentors: the adult an apprentice learns beside: the parent of their sex with a craft, else the
-- other parent, else another adult of the household with a craft, else kin.
function M.pick_mentor(W, s, p, now)
  local V = W.S.v3
  local function ok(q) return q and q.alive and q.role ~= "" and people.stage(q, now) == "adult" end
  local a = V.people[p.sex == "m" and p.father or p.mother]
  if ok(a) then return a end
  local b = V.people[p.sex == "m" and p.mother or p.father]
  if ok(b) then return b end
  local hh = V.households[p.hh]
  if hh then
    for i = 1, #hh.members do
      local q = V.people[hh.members[i]]
      if q ~= p and ok(q) then return q end
    end
  end
  for i = 1, #s.members do
    local q = V.people[s.members[i]]
    if q ~= p and ok(q) and people.is_kin(W, p, q.id) then return q end
  end
  return nil
end

-- The target classes of jobs (an apprentice works beside their mentor on the same target).
M.DOMAIN = { till = "plot", sow = "plot", harvest = "plot", tend = "plot", haul_grain = "plot",
  build = "bld", deliver = "bld", repair = "bld", fell = "tree", haul_logs = "tree", grub = "tree", thresh = "granary",
  mend_boat = "boat" }

-- A grown man in household hh (a husband, a grown son) now.
function M.has_man(W, hh, now)
  local V = W.S.v3
  local people = require("sim.agents.people")
  for _, id in ipairs(hh.members) do
    local q = V.people[id]
    if q and q.alive and q.sex == "m" and people.stage(q, now) == "adult" then return true end
  end
  return false
end

return M
