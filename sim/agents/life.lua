-- Life courses (docs/v3/10_AGENTS.md §7), run once a game day at the planet step: growing up
-- (apprentices find a mentor, adults a role), conception and births (a real infant entity),
-- deaths (age hazard, starvation, cold), courtship and marriage (a couple needs a home).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local events = require("sim.causality.events")

local M = {}
local floor, max, min, exp = math.floor, math.max, math.min, math.exp
local function C() return cfgmod.V3 end

local function u(W, p, purpose, day)
  return rng.float(W.seed, rng.S.V3_LIFE, p.key, purpose, day)
end

-- Annual death hazard at age a (gentle; tiny villages are not erased by noise).
local function hazard(a)
  local m = C().mortality
  if a < 2 then return m.infant end
  if a < 15 then return m.child end
  if a < 45 then return m.adult end
  return m.old_a + m.old_b * exp((a - 45) / m.old_k)
end

local ROLES = { "farmer", "farmer", "woodcutter", "farmer", "builder", "woodcutter", "farmer", "builder" }

-- The role the settlement is shortest of (by the work it has: farms, wood, sites).
local function pick_role(W, s, p)
  local V = W.S.v3
  local n = { farmer = 0, woodcutter = 0, builder = 0 }
  for i = 1, #s.members do
    local q = V.people[s.members[i]]
    if q and q.alive and n[q.role] then n[q.role] = n[q.role] + 1 end
  end
  local want = { farmer = #s.plots * 0.9, woodcutter = 2 + #s.sites * 0.5, builder = 1 + #s.sites * 0.7 }
  local best, bd = "farmer", -1e9
  for _, r in ipairs({ "farmer", "woodcutter", "builder" }) do
    local d = want[r] - n[r]
    if d > bd then best, bd = r, d end
  end
  return best
end

local function kill(W, s, p, cause, why)
  p.alive = false
  p.died = W.hour
  p.died_of = cause
  p.act = "dead"
  p.ver = p.ver + 1
  local V = W.S.v3
  if p.partner ~= 0 then
    local q = V.people[p.partner]
    if q then q.partner = 0; q.widowed = W.hour; people.remember(q, W.hour, "Lost " .. p.name .. " (" .. why .. ")") end
  end
  local hh = V.households[p.hh]
  if hh then people.remove_id(hh.members, p.id) end
  people.remove_id(s.members, p.id)
  require("sim.agents.think").release(W, s, p)
  if W.record_events then events.emit(W, "death", s.cell, 0, 1, { settlement = s.id, person = p.id, cause = cause }) end
  s.acc.deaths = (s.acc.deaths or 0) + 1
end

local function birth(W, s, mother, now)
  local V = W.S.v3
  local father = V.people[mother.partner]
  local hh = V.households[mother.hh]
  local home = hh and s.buildings[hh.home]
  local x, y = mother.x, mother.y
  local baby = people.new(W, { born = now, mother = mother.id, father = father and father.id or 0,
    hh = 0, sett = s.id, surname = father and father.surname or mother.surname, culture = s.culture, x = x, y = y })
  if hh then people.join(W, baby, hh) end
  s.members[#s.members + 1] = baby.id
  baby.carer = mother.id
  baby.hunger, baby.energy = 0.1, 1
  baby.inside = mother.inside
  mother.kids[#mother.kids + 1] = baby.id
  if father then father.kids[#father.kids + 1] = baby.id end
  mother.preg_due = 0
  mother.nursing = now + 1152   -- a year of nursing
  people.remember(mother, now, "Gave birth to " .. baby.name)
  if father then people.remember(father, now, "A child, " .. baby.name .. ", was born") end
  if W.record_events then events.emit(W, "birth", s.cell, 0, 1, { settlement = s.id, person = baby.id }) end
  s.acc.births = (s.acc.births or 0) + 1
  return baby
end

-- Marriage within the settlement: single adults pair by affinity and age; the couple stays in
-- their households until a home is ready (the settlement plans a house).
local function courtship(W, s, day)
  local c = C()
  local V = W.S.v3
  local now = W.hour
  local singles_f, singles_m = {}, {}
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.partner == 0 then
      local st, a = people.stage(p, now)
      if st == "adult" and a < 45 then
        if p.sex == "f" and a >= c.marry_f then singles_f[#singles_f + 1] = p
        elseif p.sex == "m" and a >= c.marry_m then singles_m[#singles_m + 1] = p end
      end
    end
  end
  for i = 1, #singles_f do
    local f = singles_f[i]
    if u(W, f, 31, day) < 0.12 then
      local best, bs = nil, -1e9
      for j = 1, #singles_m do
        local m = singles_m[j]
        if m.partner == 0 and not people.is_kin(W, f, m.id) then
          local sc = people.affinity(f, m.id) * 2 - math.abs(people.age(f, now) + 2.5 - people.age(m, now)) * 0.08
            + u(W, m, 32, day) * 0.3
          if sc > bs then best, bs = m, sc end
        end
      end
      if best then
        f.partner, best.partner = best.id, f.id
        f.married, best.married = now, now
        people.remember(f, now, "Married " .. best.name)
        people.remember(best, now, "Married " .. f.name)
        -- the couple wants a home of their own (Hajnal): the settlement plans a house
        s.couples[#s.couples + 1] = f.id
        if W.record_events then events.emit(W, "marriage", s.cell, 0, 1, { settlement = s.id, a = f.id, b = best.id }) end
      end
    end
  end
end

function M.daily(W, s, day)
  local c = C()
  local V = W.S.v3
  local now = W.hour
  local mem = s.members
  -- (iterate a copy: deaths and births change the list)
  local list = {}
  for i = 1, #mem do list[i] = mem[i] end
  for i = 1, #list do
    local p = V.people[list[i]]
    if p and p.alive then
      local st, a = people.stage(p, now)
      -- growing up
      if st ~= p.stage_was then
        if p.stage_was then people.remember(p, now, "Became " .. (st == "apprentice" and "an apprentice" or (st == "adult" and "an adult" or (st == "elder" and "an elder" or "a child")))) end
        if st == "apprentice" then
          local par = V.people[p.sex == "m" and p.father or p.mother]
          if not (par and par.alive) then par = V.people[p.father] or V.people[p.mother] end
          if par and par.alive then p.mentor = par.id end
        elseif st == "adult" and p.role == "" then
          p.role = pick_role(W, s, p)
        end
        p.stage_was = st
      end
      -- death
      local hz = hazard(a) / c.days_per_year
      if p.health < 0.4 then hz = hz + (0.4 - p.health) * 0.05 end
      local home = people.home(W, s, p)
      if home and home.cold and (st == "infant" or st == "elder") then hz = hz + 0.002 end
      if u(W, p, 1, day) < hz then
        local cause = p.health < 0.25 and (p.hunger > 0.8 and "hunger" or "illness") or (a > 50 and "old age" or "illness")
        kill(W, s, p, cause, cause)
      elseif p.health <= 0 then
        kill(W, s, p, "hunger", "hunger")
      else
        -- births and conception
        if p.preg_due > 0 and now >= p.preg_due then
          birth(W, s, p, now)
        elseif p.sex == "f" and p.partner ~= 0 and p.preg_due == 0 and p.nursing < now and a >= c.fecund[1] and a <= c.fecund[2] then
          local hh = V.households[p.hh]
          local own = hh and hh.home ~= 0 and V.people[p.partner] and V.people[p.partner].hh == p.hh
          if own then
            local fed = p.hunger < 0.6 and 1 or 0.3
            local af = a < 30 and 1 or (a < 35 and 0.85 or (a < 40 and 0.5 or 0.2))
            if u(W, p, 2, day) < c.conceive_year / c.days_per_year * fed * af then
              p.preg_due = now + c.pregnancy_days * 24
              people.remember(p, now, "With child")
            end
          end
        end
        if p.nursing > 0 and now >= p.nursing then p.nursing = 0 end
      end
    end
  end
  if day % 4 == s.id % 4 then courtship(W, s, day) end
end

return M
