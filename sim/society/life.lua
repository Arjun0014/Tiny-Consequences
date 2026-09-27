-- People's lives (docs/world/12_PEOPLE_VILLAGES.md §1–3): bodies (nutrition, health), mortality,
-- conception, pregnancy and birth, courtship, marriage when a home is available (Hajnal's
-- neolocal pattern: no home, no marriage, no births), households, occupations and memories.
-- Rolls are keyed by the person (a fixed random fraction u added to one hash per village-week:
-- a Weyl offset, cheap and local, D-021).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local construction = require("sim.society.construction")

local M = {}
local floor, min, max, exp = math.floor, math.min, math.max, math.exp
local PHI, PHI2, PHI3 = 1.6180339887498949, 2.6180339887498949, 4.2360679774997898

local function age_of(p, hour) return (hour - p.born) / 8064 end

-- Occupation for a new adult: whatever the village is shortest of.
function M.set_occupation(W, v, p)
  local P = W.ent.people
  local cfg = cfgmod.SOCIETY
  local n = { farmer = 0, fisher = 0, woodcutter = 0, builder = 0 }
  local adults = 0
  for i = 1, #v.members do
    local q = P[v.members[i]]
    if n[q.job] then n[q.job] = n[q.job] + 1; adults = adults + 1 end
  end
  local boats = #v.boats
  -- fishers for the boats the village actually sends out (plus one crew while the boats are busy)
  local used = v.boats_used or boats
  local want_fish = (#v.fishing > 0) and max(floor(used + 0.5) * cfg.crew, v.mooring and 2 or 0) or 0
  if #v.fishing > 0 and used >= boats * 0.8 and v.land_fields and #v.farms >= v.land_fields then
    want_fish = want_fish + cfg.crew
  end
  if #v.fishing > 0 and #v.farms == 0 then want_fish = adults + 1 end
  if n.fisher < want_fish then p.job = "fisher"
  elseif n.woodcutter < 1 + floor(adults / 12) then p.job = "woodcutter"
  elseif n.builder < floor(adults / 10) then p.job = "builder"
  else p.job = "farmer" end
end

-- Weekly body and demography for every member of village v.
function M.weekly(W, v)
  local cfg = cfgmod.SOCIETY
  local E = W.ent
  local P, H, B = E.people, E.households, E.buildings
  local hour = W.hour
  local week = floor(hour / 168)
  local kt = v.key_hour % 65536
  local ud = rng.float(W.seed, rng.S.DEATH, v.key_cell, week, kt)
  local ub = rng.float(W.seed, rng.S.BIRTH, v.key_cell, week, kt)
  local ration = v.ration_n > 0 and v.ration_sum / v.ration_n or v.ration
  v.ration_sum, v.ration_n = 0, 0
  local cold = v.cold_days or 0
  v.cold_days = 0
  local Tw = W.F.temp[v.cell]
  local nonage, dtag, dtag_n = 0, 0, 0
  local hsum = 0   -- the week's summed death hazard (expected deaths): the account's mortality
  local m = v.members
  local dead_any = false
  -- the planet's soft cap (performance guard): births taper to 0 at soft_cap
  local total = W.S.society.pop or 0
  local gcap = 1 - max(0, total - cfg.soft_cap * 0.8) / (cfg.soft_cap * 0.2)
  if gcap < 0 then gcap = 0 end
  for i = 1, #m do
    local p = P[m[i]]
    local a = age_of(p, hour)
    -- coming of age, retirement
    if p.job == "child" and a >= cfg.adult_age then M.set_occupation(W, v, p)
    elseif p.job ~= "elder" and p.job ~= "child" and a >= cfg.elder_age then p.job = "elder" end
    -- nutrition and health
    local n = p.nutrition + (ration - p.nutrition) * cfg.nutrition_rate
    p.nutrition = n
    local vul = (a < 5 or a >= cfg.elder_age) and cfg.vulnerable_mult or 1
    -- health follows what the diet can sustain: a thin diet wears people down to a weakened
    -- steady state (not to death); only a diet far below need makes them frail
    local hp = p.health
    local target = 100
    if n < cfg.fed_n then
      target = (n - cfg.starve_n + 0.05) / (cfg.fed_n - cfg.starve_n + 0.05) * 100
      if vul > 1 then target = target - 15 end
      if target < 0 then target = 0 end
    end
    if hp > target then
      hp = hp - cfg.hunger_health * vul
      if hp < target then hp = target end
    elseif hp < target then
      hp = hp + cfg.health_regen
      if hp > target then hp = target end
    end
    if cold > 0 and Tw < cfg.cold_temp then hp = hp - (cfg.cold_temp - Tw) * cfg.cold_health * vul * cold / 7 end
    if hp > 100 then hp = 100 elseif hp < 0 then hp = 0 end
    p.health = hp
    -- mortality
    local base
    if a < 1 then base = cfg.mort_infant elseif a < 5 then base = cfg.mort_child
    elseif a < 15 then base = cfg.mort_youth else base = cfg.mort_adult end
    if a > cfg.mort_old_age then base = base + cfg.mort_old * exp((a - cfg.mort_old_age) / cfg.mort_old_scale) end
    -- the malnourished catch what the fed shake off (famine kills mostly through disease)
    local mal = n < cfg.fed_n and 1 + cfg.malnutrition_mult * (cfg.fed_n - n) / cfg.fed_n or 1
    local hz = base * mal / 48
    local h = H[p.household]
    local home = h and B[h.home]
    local crowded = (not home or home.state ~= "standing") or (h and #h.members > ents.capacity_of(home))
    if crowded then hz = hz * cfg.crowd_hazard end
    local cause, tag = "age", 0
    if mal > 2 then cause, tag = "hunger", v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause end
    if hp < cfg.frail_health then
      local ph = (cfg.frail_health - hp) * cfg.frail_hazard * vul
      if ph > hz then
        if n < cfg.fed_n then cause, tag = "hunger", v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause
        else cause, tag = "cold", v.foodtag[3] end
      end
      hz = hz + ph
    end
    -- starvation: a body without food for weeks does not linger for months
    if n < cfg.starve_n then
      local sh = (cfg.starve_n - n) * cfg.starve_hazard
      if sh > hz * 0.5 then cause, tag = "hunger", v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause end
      hz = hz + sh
    end
    hsum = hsum + hz
    if (ud + p.u) % 1 < hz then
      ents.kill(W, p, cause, tag)
      dead_any = true
      if cause ~= "age" then
        nonage = nonage + 1
        if tag ~= 0 then
          if tag == dtag then dtag_n = dtag_n + 1 elseif dtag_n == 0 then dtag, dtag_n = tag, 1 else dtag_n = dtag_n - 1 end
        end
      end
    else
      -- conception: married women in their own home, fed, not nursing, not pregnant
      if p.sex == "f" and p.partner ~= 0 and p.preg_due == 0 and a >= cfg.fecund[1] and a <= cfg.fecund[2]
        and p.nursing_until <= hour and home and home.state == "standing" then
        local q = P[p.partner]
        if q and not q.dead and q.village == v.id then
          local af = 1
          if a > 30 then af = max(0.2, 1 - (a - 30) * 0.05) end
          if a > 40 then af = max(0.1, 0.5 - (a - 40) * 0.075) end
          local nf = (n - 0.55) / 0.3
          if nf > 1 then nf = 1 elseif nf < 0 then nf = 0 end
          local pday = cfg.conceive_day * af * nf * gcap
          local pweek = 1 - (1 - pday) ^ 7
          if (ub + p.u * PHI) % 1 < pweek then
            if (ub + p.u * PHI3) % 1 < cfg.fetal_loss then
              -- lost pregnancy: nothing, she may conceive again
            else
              p.preg_due = hour + cfg.preg_days * 24
              p.preg_father = q.id   -- the child's father even if he dies before the birth
              local pl = v.preg
              if not pl then pl = {}; v.preg = pl end
              pl[#pl + 1] = p.id
            end
          end
        end
      end
    end
  end
  local a = v.acc
  a.hz = (a.hz or 0) + hsum
  a.pw = (a.pw or 0) + #m
  if dead_any then ents.compact(W, v) end
  v.nonage = (v.nonage or 0) + nonage
  if dtag ~= 0 then v.death_tag = dtag end
  -- household housekeeping only when someone died, came or went, or lost a home
  if dead_any or v.hh_dirty then
    v.hh_dirty = false
    M.households(W, v)
  end
end

-- Monthly: moods (for dialogue and the inspector) from nutrition, health and memories.
local function moods(W, v)
  local P = W.ent.people
  local m = v.members
  for i = 1, #m do
    local p = P[m[i]]
    local bad = 0
    local mem = p.mem
    for k = 2, #mem, 2 do bad = bad + mem[k] end
    local mood = 0.8 * p.nutrition + 0.4 * (p.health / 100) - 0.15 * bad - 0.6
    if mood > 1 then mood = 1 elseif mood < -1 then mood = -1 end
    p.mood = mood
  end
end

-- Births due (checked at the village's day step).
function M.births(W, v)
  local cfg = cfgmod.SOCIETY
  local E = W.ent
  local P = E.people
  local hour = W.hour
  local pl = v.preg
  if not pl or #pl == 0 then return end
  local idx = 0
  for i = #pl, 1, -1 do
    local p = P[pl[i]]
    if not p or p.dead or p.village ~= v.id or p.preg_due == 0 then
      table.remove(pl, i)
    elseif p.preg_due <= hour + 24 then
      table.remove(pl, i)
      p.preg_due = 0
      local h = E.households[p.household]
      idx = idx + 1
      local father = p.preg_father or p.partner
      p.preg_father = nil
      local child = ents.new_person(W, v, h, { born = hour, mother = p.id, father = father, idx = idx })
      child.job = "child"
      p.nursing_until = hour + cfg.nursing_days * 24
      v.births = v.births + 1
      v.acc.births = (v.acc.births or 0) + 1
      W.S.society.births = W.S.society.births + 1
      if rng.float(W.seed, rng.S.BIRTH, p.key, hour, 3) < cfg.maternal_death then
        ents.kill(W, p, "childbirth", 0)
      end
    end
  end
  if idx > 0 then ents.compact(W, v) end
end

-- A house is ready: the neediest household moves in (homeless first), else a waiting couple
-- marries and moves in.
function M.house_ready(W, v, b)
  local E = W.ent
  local H = E.households
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h and (h.home == 0 or not E.buildings[h.home] or E.buildings[h.home].state ~= "standing") then
      h.home = b.id
      b.household = h.id
      return
    end
  end
  M.marry_waiting(W, v)
end

construction.on_house_ready = M.house_ready

-- Vacant homes of the village (standing, nobody in them).
local function vacant(W, v)
  local B = W.ent.buildings
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state == "standing" and (b.kind == "house" or b.kind == "hut") and b.household == 0 then return b end
  end
  return nil
end
M.vacant = vacant

-- Betrothed couples marry while vacant homes exist and the village can feed another household
-- (Hajnal: marriage needed a livelihood; oldest betrothal first: member order).
function M.marry_waiting(W, v)
  local E = W.ent
  local P = E.people
  local m = v.members
  if (v.capacity or 1e9) < v.pop * 1.05 then return end
  for i = 1, #m do
    local p = P[m[i]]
    if p.sex == "f" and p.betrothed ~= 0 then
      local q = P[p.betrothed]
      if not q or q.dead then
        p.betrothed = 0
      elseif q.village == v.id then
        local home = vacant(W, v)
        if not home then return end
        local h = ents.new_household(W, v, home.id)
        ents.move_person(W, p, v, h)
        ents.move_person(W, q, v, h)
        p.betrothed, q.betrothed = 0, 0
        p.partner, q.partner = q.id, p.id
        p.wait_since, q.wait_since = nil, nil
        v.acc.marriages = (v.acc.marriages or 0) + 1
      end
    end
  end
end

-- Household housekeeping: empty households dissolve; orphans go to kin; widowed elders living
-- alone join a child's household; the homeless look for a vacant house.
function M.households(W, v)
  local E = W.ent
  local P, H = E.people, E.households
  local hour = W.hour
  local cfg = cfgmod.SOCIETY
  for i = #v.households, 1, -1 do
    local h = H[v.households[i]]
    if h then
      -- drop members who died or left
      local k = 0
      for j = 1, #h.members do
        local p = P[h.members[j]]
        if p and not p.dead and p.household == h.id and p.village == v.id then k = k + 1; h.members[k] = h.members[j] end
      end
      for j = #h.members, k + 1, -1 do h.members[j] = nil end
      if k == 0 then
        ents.drop_household(W, h)
      else
        local adults = 0
        for j = 1, k do if age_of(P[h.members[j]], hour) >= cfg.adult_age then adults = adults + 1 end end
        if adults == 0 then
          -- orphans: to a grandparent's or relative's household, else the one with most room
          local dest = nil
          local c1 = P[h.members[1]]
          for _, rid in ipairs({ c1.mother, c1.father }) do
            local par = rid ~= 0 and P[rid]
            if par then
              for _, gid in ipairs({ par.mother, par.father }) do
                local gp = gid ~= 0 and P[gid]
                if gp and not gp.dead and gp.village == v.id and H[gp.household] then dest = H[gp.household] end
              end
            end
          end
          if not dest then
            local best = -1
            for j = 1, #v.households do
              local o = H[v.households[j]]
              if o and o ~= h and #o.members > 0 then
                local room = 7 - #o.members
                if room > best then best, dest = room, o end
              end
            end
          end
          if dest then
            for j = #h.members, 1, -1 do ents.move_person(W, P[h.members[j]], v, dest) end
            ents.drop_household(W, h)
          end
        elseif h.home == 0 then
          local b = vacant(W, v)
          if b then h.home = b.id; b.household = h.id end
        end
      end
    end
  end
  -- anyone without a household (arrivals) forms one
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.household == 0 or not H[p.household] then
      local h = ents.new_household(W, v, 0)
      ents.move_person(W, p, v, h)
      local b = vacant(W, v)
      if b then h.home = b.id; b.household = h.id end
    end
  end
end

-- Monthly courtship: singles court within the village (and linked villages later); a betrothed
-- couple marries when a home is free, otherwise they wait (and the village plans a house).
function M.monthly(W, v)
  local cfg = cfgmod.SOCIETY
  local E = W.ent
  local P = E.people
  local hour = W.hour
  local month = floor(hour / 672)
  local up = rng.float(W.seed, rng.S.PAIRING, v.key_cell, month, v.key_hour % 65536)
  local sf, sm = {}, {}
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.partner == 0 and p.betrothed == 0 and p.away == 0 then
      local a = age_of(p, hour)
      if a >= cfg.court_age[p.sex] and a <= cfg.court_max_age then
        if p.sex == "f" then sf[#sf + 1] = p else sm[#sm + 1] = p end
      end
    end
  end
  local mi = 1
  for i = 1, #sf do
    if mi > #sm then break end
    local f = sf[i]
    if (up + f.u * PHI2) % 1 < cfg.court_prob then
      local mm = sm[mi]
      mi = mi + 1
      -- no marriages between parents and children or siblings
      local kin = mm.mother ~= 0 and (mm.mother == f.mother or mm.father == f.father)
      if not kin then
        f.betrothed, mm.betrothed = mm.id, f.id
        f.wait_since, mm.wait_since = hour, hour
      end
    end
  end
  M.marry_waiting(W, v)
  M.retrain(W, v)
  moods(W, v)
end

-- A chronically overstaffed trade re-trains one person a month: fishers without boats to sail
-- take up farming (the youngest first: member order is age-agnostic, so pick by birth).
function M.retrain(W, v)
  if #v.fishing == 0 then return end
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local fishers, young = 0, nil
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.job == "fisher" then
      fishers = fishers + 1
      if not young or p.born > young.born then young = p end
    end
  end
  local want = floor((v.boats_used or #v.boats) + 0.5) * cfg.crew + cfg.crew
  if young and fishers > want then young.job = "farmer" end
end

-- New events near/about the village become memories of everyone living there.
local SAL_OWN = { famine = 0.9, crop_failure = 0.6, deaths_spike = 0.8, migration = 0.5,
  fish_collapse = 0.6, overfishing = 0.5, oil_beached = 0.7, village_founded = 0.5, rationing = 0.4,
  seed_eaten = 0.7, boat_lost = 0.6, shipment_lost = 0.6, wood_shortage = 0.4 }
local SAL_NEAR = { fire_started = 0.4, wildfire = 0.7, flood = 0.6, oil_beached = 0.7, tsunami = 1.0,
  eruption = 1.0, quake = 0.8, meteor_natural = 1.0, lightning_strike = 0.3, drought = 0.5 }
function M.memories(W, v)
  local E = W.E
  local last = E.next_id - 1
  if v.ev_seen >= last then return end
  local P = W.ent.people
  local m = v.members
  local near = W.vgeo[v.id] and W.vgeo[v.id].near
  for id = v.ev_seen + 1, last do
    local r = E.rec[id]
    if r then
      local sal = 0
      local d = r.data
      if type(d) == "table" and (d.village == v.id or d.from == v.id) then sal = SAL_OWN[r.kind] or 0.15
      elseif near and near[r.cell] then
        if r.actor == "player" and r.parent == 0 then sal = 0.8 else sal = SAL_NEAR[r.kind] or 0 end
      end
      if sal > 0 then
        for i = 1, #m do ents.remember(P[m[i]], id, sal) end
      end
    end
  end
  v.ev_seen = last
end

return M
