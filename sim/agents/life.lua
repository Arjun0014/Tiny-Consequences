-- Life courses (docs/v3/10_AGENTS.md §7), run once a game day at the planet step: growing up
-- (apprentices find a mentor, adults a role), conception and births (a real infant entity),
-- deaths (age hazard, starvation, cold), courtship and marriage (a couple needs a home).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local events = require("sim.causality.events")

local M = {}
local floor, max, min, exp = math.floor, math.max, math.min, dmath.exp
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
  local n = { farmer = 0, woodcutter = 0, builder = 0, fisher = 0 }
  for i = 1, #s.members do
    local q = V.people[s.members[i]]
    if q and q.alive and n[q.role] then n[q.role] = n[q.role] + 1 end
  end
  -- (a village with a dock wants a fisher for every boat, D-087)
  local want = { farmer = #s.plots * 0.9, woodcutter = 2 + #s.sites * 0.5, builder = 1 + #s.sites * 0.7,
    fisher = s.dock and #(s.boats or {}) or 0 }
  local best, bd = "farmer", -1e9
  for _, r in ipairs({ "farmer", "woodcutter", "builder", "fisher" }) do
    local d = want[r] - n[r]
    if d > bd then best, bd = r, d end
  end
  return best
end

local function kill(W, s, p, cause, why)
  if p.at then
    -- (dying away from home: off the other village's list of visitors)
    local o = W.S.v3.settlements[p.at]
    if o and o.visitors then people.remove_id(o.visitors, p.id) end
    p.at = nil
  end
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
  if hh then
    people.remove_id(hh.members, p.id)
    if #hh.members == 0 then hh.last = p.id end
  end
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
  require("sim.agents.kin").give(W, baby, mother)
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
  -- (S16, R4) a man with nobody here he could marry goes courting at a neighbouring village
  for j = 1, #singles_m do
    local m = singles_m[j]
    if m.partner == 0 and not m.court and not m.trip and people.age(m, now) < 40 and u(W, m, 34, day) < c.court_far then
      local here = false
      for i = 1, #singles_f do
        local f = singles_f[i]
        if f.partner == 0 and not people.is_kin(W, f, m.id) and not f.move then here = true; break end
      end
      if not here then
        local o = M.court_village(W, s, m, day)
        if o then
          m.court = { to = o.id, t = now }
          people.remember(m, now, "Nobody to marry here: I will go courting at " .. o.name)
        end
      end
    end
  end
end

-- (S16) Children left in a household with no grown-up (orphaned, or left behind) are taken in:
-- by the household of their nearest grown-up kin in the village (a grandparent, an aunt or uncle,
-- a grown brother or sister), else by the household nearest their house with a woman at home.
function M.take_in(W, s, day)
  local V = W.S.v3
  local now = W.hour
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 then
      local grown = false
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        local st = q and q.alive and people.stage(q, now)
        if st == "adult" or st == "elder" then grown = true; break end
      end
      if not grown then
        -- (kin first: anyone grown in the village kin to one of the children, by affinity)
        local to, why, bs = nil, nil, -1e9
        local home = s.buildings[hh.home or 0]
        for _, mid in ipairs(s.members) do
          local q = V.people[mid]
          local st = q and q.alive and q.hh ~= hid and people.stage(q, now)
          if (st == "adult" or st == "elder") and V.households[q.hh] and V.households[q.hh].home ~= 0 then
            for _, id in ipairs(hh.members) do
              local k = V.people[id]
              local kin = k and (people.is_kin(W, k, q.id) or (V.people[k.mother or 0] and people.is_kin(W, V.people[k.mother], q.id))
                or (V.people[k.father or 0] and people.is_kin(W, V.people[k.father], q.id)))
              local sc = kin and (1 + people.affinity(k, q.id)) or -1
              if not kin and q.sex == "f" and home then
                local qh = s.buildings[V.households[q.hh].home]
                sc = qh and -(qh.dx - home.dx) * (qh.dx - home.dx) - (qh.dy - home.dy) * (qh.dy - home.dy) or -1e9
              end
              if sc > bs then to, bs, why = q, sc, kin and "kin" or "neighbour" end
            end
          end
        end
        if to then
          local dest = V.households[to.hh]
          local list = {}
          for i = 1, #hh.members do list[i] = hh.members[i] end
          for _, id in ipairs(list) do
            local k = V.people[id]
            if k and k.alive then
              people.join(W, k, dest)
              people.remember(k, now, "Taken in by " .. to.name .. (why == "kin" and ", our kin" or ""))
            end
          end
          people.remember(to, now, "Took in " .. #list .. (#list == 1 and " child" or " children") .. " left with no one to keep them")
          s.acc.taken_in = (s.acc.taken_in or 0) + #list
        end
      end
    end
  end
end

-- (S16, R4) The village a man with nobody to marry at home goes courting at: a road neighbour he
-- can reach in a day or so, the more trusted (help and trade between them) the likelier.
function M.court_village(W, s, m, day)
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  local best, bs = nil, -1e9
  for _, o in ipairs(trade.partners(W, s)) do
    local _, hrs = trade.road(W, s, o)
    -- (not where he found nobody to marry lately)
    local tried = false
    for _, e in ipairs(m.court_tried or {}) do
      if e[1] == o.id and day * 24 - e[2] < 24 * C().court_retry_days then tried = true end
    end
    if hrs <= C().court_road_h and #o.members > 0 and not tried then
      local sc = econ.rel(s, o).trust * 2 - hrs * 0.03 + u(W, m, 35 + o.id, day) * 0.5
      if sc > bs then best, bs = o, sc end
    end
  end
  return best
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
          local m = require("sim.agents.kin").pick_mentor(W, s, p, now)
          if m then
            p.mentor = m.id
            people.remember(p, now, "Began learning " .. m.role .. "'s work from " .. m.name)
          end
        elseif st == "child" and p.act == "carried" then
          -- walking now: set down for good
          require("sim.agents.kin").give(W, p, nil)
          p.act = "idle"
        elseif st == "adult" and p.role == "" then
          p.role = pick_role(W, s, p)
        end
        p.stage_was = st
      end
      -- an apprentice without a mentor (founders, or a mentor who died) finds one
      if st == "apprentice" then
        local m = V.people[p.mentor]
        if not (m and m.alive) then
          m = require("sim.agents.kin").pick_mentor(W, s, p, now)
          p.mentor = m and m.id or 0
          if m then people.remember(p, now, "Learning " .. m.role .. "'s work from " .. m.name) end
        end
      end
      -- the flux from foul water (D-107): a chance each day while the house drinks it; days of
      -- sickness (weak, losing health; the very young and the old may die of it); the household
      -- keeps to the well after it
      local fhome = people.home(W, s, p)
      if (p.flux or -1) >= day then
        p.health = max(0, p.health - c.flux_health)
        p.energy = min(p.energy, 0.5)
      elseif fhome and (fhome.foul or 0) > 0 and (fhome.stock.water or 0) > 0 and st ~= "infant"
        and u(W, p, 7, day) < fhome.foul * c.flux_k then
        p.flux = day + c.flux_days
        s.acc.flux = (s.acc.flux or 0) + 1
        local hh = V.households[p.hh]
        if hh then hh.river_bad = day end
        people.remember(p, now, "Sick with the flux: the river water")
      end
      -- shoes wear (D-104); barefoot in the cold costs health
      local sw = c.shoes_wear[st]
      if sw then
        p.shoes = max(0, (p.shoes or 0.7) - sw)
        if s.cold and p.shoes < 0.1 then
          p.health = max(0, p.health - 0.008)
          if (p.foot_day or -9) < day - 8 then p.foot_day = day; people.remember(p, now, "Barefoot in the cold: my shoes are worn through") end
        end
      end
      -- clothes wear (children outgrow theirs); rags in the cold cost health (D-088)
      local cw = c.clothes_wear[st]
      if cw then
        p.clothes = max(0, (p.clothes or 1) - cw)
        if s.cold and p.clothes < 0.2 then
          p.health = max(0, p.health - 0.015 * (1 - p.clothes / 0.2))
          if (p.cold_day or -9) < day - 6 then p.cold_day = day; people.remember(p, now, "Shivering: my clothes are in rags") end
        end
      end
      -- the household's diet (D-101): bread with no relish, or relish with no bread, for weeks on
      -- end weakens people and lets illness in
      local home = people.home(W, s, p)
      local poor = false
      if home and home.diet and st ~= "infant" then
        poor = home.diet < c.diet_lo or home.diet > c.diet_hi
        if poor then
          p.diet_cap = c.diet_weak
          if p.health > c.diet_weak then p.health = max(c.diet_weak, p.health - 0.03) end
          if (p.diet_day or -99) < day - 12 then
            p.diet_day = day
            people.remember(p, now, home.diet < c.diet_lo and "Bread and porridge and nothing with it, week after week" or "Fish and meat and hardly any bread, week after week")
          end
        else
          p.diet_cap = nil
        end
      end
      -- death
      local hz = hazard(a) / c.days_per_year
      if p.health < 0.4 then hz = hz + (0.4 - p.health) * 0.05 end
      if poor then hz = hz + c.diet_hz end
      if (p.flux or -1) >= day and (st == "child" or st == "elder") and a < 5 + (st == "elder" and 100 or 0) then hz = hz + c.flux_hz end
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
  M.take_in(W, s, day)
  if day % 4 == s.id % 4 then courtship(W, s, day) end
end

-- Lost at sea (D-102): a boat foundered on a trading voyage.
function M.drown(W, p, from, to)
  local s = W.S.v3.settlements[p.sett]
  local now = W.S.v3.now or W.hour
  if p.carry ~= "" then p.carry, p.camt = "", 0 end
  p.pack, p.riding, p.ride_boat, p.boat, p.trip = nil, nil, nil, nil, nil
  for _, id in ipairs(W.S.v3.households[p.hh] and W.S.v3.households[p.hh].members or {}) do
    local q = W.S.v3.people[id]
    if q and q.alive and q ~= p and people.stage(q, now) ~= "infant" then
      people.remember(q, now, sfmt("%s was lost at sea between %s and %s", p.name, from, to))
    end
  end
  kill(W, s, p, "drowned", "lost at sea")
end

return M
