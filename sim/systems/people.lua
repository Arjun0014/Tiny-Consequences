-- People (02_WORLD_MODEL.md §5.10): individuals, driven monthly by their village
-- (sim/systems/villages.lua calls people.month for the village whose turn it is).
--
-- Person record (W.ent.people[id]; stable schema, the renderer/inspector reads it):
--   id        lookup handle (global counter; never used as an rng key)
--   key       rng key = hash(village founding cell, birth tick, index among that village's births
--             that tick): all of a person's rolls are keyed by it, so an extra birth elsewhere
--             never reshuffles anyone's fate (05_ARCHITECTURE.md §5)
--   name, surname, sex ("f"|"m"), born (tick), village (id)
--   job       "child" | "farmer" | "fisher" | "woodcutter" | "elder"
--   health, hunger   0–100
--   partner, mother, father   person ids (0 = none); children = count
--   traits    { 1–2 of TRAITS }
--   mem       { event id, salience, event id, salience, ... } (≤ 6 pairs, most salient kept)
--   mood      −1 … 1 (derived monthly: hunger, health, bad memories, traits)
--   dead      tick of death (dead people are kept ~30 years for family trees, then pruned)
--   u         the person's fixed random fraction: monthly rolls are frac(village-month base +
--             u × φ^k) — a Weyl offset keyed by who they are, cheap enough for every month
--   died_of   "age" | "hunger" | "fire" | "flood" | "wander" | ...; death_tag = cause event id
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")

local people = {}

people.TRAITS = { "optimist", "doomsayer", "hoarder", "devout", "skeptic", "romantic", "grump" }
local TRAIT_MOOD = { optimist = 0.15, doomsayer = -0.12, hoarder = 0, devout = 0.05, skeptic = 0,
  romantic = 0.05, grump = -0.15 }

local floor = math.floor
local PHI, PHI2 = 1.6180339887498949, 2.6180339887498949
local SF, SM = {}, {}   -- scratch: single women / men this month (cleared after use)

function people.age(W, p) return floor((W.tick - p.born) / 48) end

-- rng key for a person born in village v at tick `born`, idx-th birth there that tick.
function people.key(W, v, born, idx)
  return rng.hash(W.seed, rng.S.PERSON, v.key_cell, born, idx * 65536 + (v.key_tick % 65536))
end

-- Create a person in village v. opts: sex, born, mother, father, idx (births that tick), job.
function people.create(W, v, opts)
  local E = W.ent
  local id = E.next_person
  E.next_person = id + 1
  local born = opts.born or W.tick
  local key = people.key(W, v, born, opts.idx or 0)
  local ctr = 0
  local seed = W.seed
  local function u()
    ctr = ctr + 1
    return rng.float(seed, rng.S.NAMES, key, ctr, 0)
  end
  local sex = opts.sex or (u() < 0.5 and "f" or "m")
  local surname
  local mother = opts.mother and E.people[opts.mother]
  local father = opts.father and E.people[opts.father]
  if father then surname = father.surname elseif mother then surname = mother.surname
  else surname = names.surname(u, v.culture) end
  local t1 = people.TRAITS[1 + floor(u() * #people.TRAITS)]
  local traits = { t1 }
  if u() < 0.4 then
    local t2 = people.TRAITS[1 + floor(u() * #people.TRAITS)]
    if t2 ~= t1 then traits[2] = t2 end
  end
  local p = {
    id = id, key = key, name = names.person(u, v.culture, sex), surname = surname, sex = sex,
    born = born, village = v.id, job = "child", health = 100, hunger = 0, partner = 0,
    mother = opts.mother or 0, father = opts.father or 0, children = 0, traits = traits,
    mem = {}, mood = 0, u = floor(rng.float(seed, rng.S.PERSON, key, 1, 0) * 1048576) / 1048576,
  }
  E.people[id] = p
  local m = v.members
  m[#m + 1] = id
  v.pop = #m
  if mother then mother.children = mother.children + 1 end
  if father then father.children = father.children + 1 end
  people.set_job(W, v, p)
  return p
end

-- Job from age and the village's needs (fishers only with fishing access).
function people.set_job(W, v, p)
  local age = people.age(W, p)
  local cfg = cfgmod.PEOPLE
  if age < cfg.adult_age then p.job = "child"; return end
  if age >= cfg.elder_age then p.job = "elder"; return end
  if p.job ~= "child" and p.job ~= "elder" and p.job ~= nil then return end
  local want_fish = v.fish_jobs or 0
  local c = v.jobs or {}
  if want_fish > (c.fisher or 0) then p.job = "fisher"
  elseif (c.woodcutter or 0) < cfg.woodcutter_share * (v.pop or 1) then p.job = "woodcutter"
  else p.job = "farmer" end
  c[p.job] = (c[p.job] or 0) + 1
  v.jobs = c
end

-- Add a memory (event id, salience) keeping the most salient MEMORIES.
function people.remember(p, ev, sal)
  local mem = p.mem
  local n = #mem
  for i = 1, n, 2 do
    if mem[i] == ev then
      if sal > mem[i + 1] then mem[i + 1] = sal end
      return
    end
  end
  if n < 2 * cfgmod.PEOPLE.memories then
    mem[n + 1] = ev
    mem[n + 2] = sal
    return
  end
  local wi, ws = 0, sal
  for i = 1, n, 2 do
    if mem[i + 1] < ws then wi, ws = i, mem[i + 1] end
  end
  if wi > 0 then mem[wi] = ev; mem[wi + 1] = sal end
end

-- Yearly: memories fade.
function people.fade_memories(p, f)
  local mem = p.mem
  local k = 0
  for i = 1, #mem, 2 do
    local s = mem[i + 1] * f
    if s >= 0.05 then
      k = k + 2
      mem[k - 1] = mem[i]
      mem[k] = s
    end
  end
  for i = #mem, k + 1, -1 do mem[i] = nil end
end

local function kill(W, v, p, cause, tag)
  p.dead = W.tick
  p.died_of = cause
  p.death_tag = tag or 0
  local E = W.ent
  if p.partner ~= 0 then
    local q = E.people[p.partner]
    if q and not q.dead then q.partner = 0 end
  end
  local d = E.dead_ids
  d[#d + 1] = p.id
  -- family grief: a personal memory for the partner, children and parents still alive
  -- (the death itself is not an event; memories point at the cause event when there is one)
  if tag and tag ~= 0 then
    local sal = cfgmod.PEOPLE.grief_salience
    if p.partner ~= 0 and E.people[p.partner] then people.remember(E.people[p.partner], tag, sal) end
    if p.mother ~= 0 and E.people[p.mother] and not E.people[p.mother].dead then people.remember(E.people[p.mother], tag, sal) end
    if p.father ~= 0 and E.people[p.father] and not E.people[p.father].dead then people.remember(E.people[p.father], tag, sal) end
  end
end
people.kill = kill

-- Drop the dead and those who moved away from v.members (order kept).
function people.compact(W, v)
  local P = W.ent.people
  local m = v.members
  local k = 0
  local vid = v.id
  for i = 1, #m do
    local id = m[i]
    local p = P[id]
    if not p.dead and p.village == vid then k = k + 1; m[k] = id end
  end
  for i = #m, k + 1, -1 do m[i] = nil end
  v.pop = k
end

-- One month of life in village v. ctx: { shortfall = 0..1 (share of food missing this month),
-- starve_tag, exposure = { kind = "fire"|"flood", p = death prob, tag } or nil, capacity,
-- fs (food security months), pop_total (planet), crowd }
-- Returns number of non-age deaths and their dominant tag.
function people.month(W, v, ctx)
  local cfg = cfgmod.PEOPLE
  local E = W.ent
  local P = E.people
  local tick = W.tick
  local seed = W.seed
  -- one random base per village-month and purpose; each person adds their fixed fraction
  local kt = v.key_tick % 65536
  local ud = rng.float(seed, rng.S.DEATH, v.key_cell, tick, kt)
  local ub = rng.float(seed, rng.S.BIRTH, v.key_cell, tick, kt)
  local up = rng.float(seed, rng.S.PAIRING, v.key_cell, tick, kt)
  local m = v.members
  local short = ctx.shortfall or 0
  local hunger_target = 100 * short
  local ndead = 0
  local nonage, dtag, dtag_n = 0, 0, 0
  -- jobs recount (for assignments this month)
  local jobs = v.jobs
  jobs.farmer, jobs.fisher, jobs.woodcutter = 0, 0, 0
  for i = 1, #m do
    local p = P[m[i]]
    local j = p.job
    if jobs[j] then jobs[j] = jobs[j] + 1 end
  end
  local singles_f, singles_m = SF, SM
  local nsf, nsm = 0, 0
  local nm = #m
  for i = 1, nm do
    local p = P[m[i]]
    local age = (tick - p.born) / 48
    age = age - age % 1
    -- jobs at coming of age and retirement
    if (p.job == "child" and age >= cfg.adult_age) or (p.job ~= "elder" and age >= cfg.elder_age) then
      local old = p.job
      if old ~= "child" and jobs[old] then jobs[old] = jobs[old] - 1 end
      p.job = "child"
      people.set_job(W, v, p)
    end
    -- hunger and health
    local h = p.hunger + (hunger_target - p.hunger) * cfg.hunger_rate
    if h < 0.5 then h = 0 end
    p.hunger = h
    local hp = p.health
    if h > cfg.hunger_hurts then hp = hp - (h - cfg.hunger_hurts) * cfg.health_loss
    else hp = hp + cfg.health_regen end
    if hp > 100 then hp = 100 elseif hp < 0 then hp = 0 end
    p.health = hp
    -- death
    local hz
    if age < 5 then hz = cfg.child_hazard else hz = cfg.adult_hazard end
    if age > cfg.old_age then hz = hz + cfg.old_hazard * math.exp((age - cfg.old_age) / cfg.old_scale) end
    local cause, tag = "age", 0
    if hp < cfg.frail then
      local ph = (cfg.frail - hp) * cfg.frail_hazard
      if ph > hz then cause, tag = "hunger", ctx.starve_tag or 0 end
      hz = hz + ph
    end
    local ex = ctx.exposure
    if ex and ex.p > 0 then
      if ex.p > hz then cause, tag = ex.kind, ex.tag or 0 end
      hz = hz + ex.p
    end
    if (ud + p.u) % 1 < hz then
      kill(W, v, p, cause, tag)
      ndead = ndead + 1
      if cause ~= "age" then
        nonage = nonage + 1
        if tag ~= 0 then
          if tag == dtag then dtag_n = dtag_n + 1
          elseif dtag_n == 0 then dtag, dtag_n = tag, 1
          else dtag_n = dtag_n - 1 end
        end
      end
    else
      -- pairing candidates and mood
      if p.partner == 0 and age >= cfg.pair_min and age <= cfg.pair_max then
        if p.sex == "f" then nsf = nsf + 1; singles_f[nsf] = p
        else nsm = nsm + 1; singles_m[nsm] = p end
      end
      local bad = 0
      local mem = p.mem
      for k = 2, #mem, 2 do bad = bad + mem[k] end
      local tm = 0
      local tr = p.traits
      for k = 1, #tr do tm = tm + (TRAIT_MOOD[tr[k]] or 0) end
      local mood = 0.8 * (1 - h / 100) + 0.4 * (hp / 100) - 0.15 * bad + tm - 0.4
      if mood > 1 then mood = 1 elseif mood < -1 then mood = -1 end
      p.mood = mood
    end
  end
  -- pairing: each single woman may meet a single man (both in member order)
  if nsf > 0 and nsm > 0 then
    local mi = 1
    for i = 1, nsf do
      local f = singles_f[i]
      if mi > nsm then break end
      if (up + f.u * PHI) % 1 < cfg.pair_rate then
        local mm = singles_m[mi]
        mi = mi + 1
        f.partner, mm.partner = mm.id, f.id
      end
    end
  end
  for i = 1, nsf do singles_f[i] = nil end
  for i = 1, nsm do singles_m[i] = nil end
  if ndead > 0 then people.compact(W, v) end
  -- births: partnered women of age, food security and crowding permitting; global hard cap
  local births = 0
  local fs_f = ((ctx.fs or 0) - cfg.birth_fs[1]) / (cfg.birth_fs[2] - cfg.birth_fs[1])
  if fs_f > 1 then fs_f = 1 elseif fs_f < 0 then fs_f = 0 end
  -- the planet's hard cap (02 §5.10) is approached softly: births taper over the last cap_soft
  local gcap = (cfg.global_cap - (ctx.pop_total or 0)) / (cfg.global_cap * cfg.cap_soft)
  if gcap > 1 then gcap = 1 elseif gcap < 0 then gcap = 0 end
  local pb = cfg.birth_rate * fs_f * (ctx.crowd or 1) * gcap
  if pb > 0 then
    local capped = (ctx.pop_total or 0) >= cfg.global_cap
    local n0 = #m
    for i = 1, n0 do
      local p = P[m[i]]
      if p.sex == "f" and p.partner ~= 0 then
        local age = floor((tick - p.born) / 48)
        if age >= cfg.birth_min and age <= cfg.birth_max and (ub + p.u * PHI2) % 1 < pb then
          if capped then
            W.S.villages.cap_hits = (W.S.villages.cap_hits or 0) + 1
          else
            people.create(W, v, { born = tick, mother = p.id, father = p.partner, idx = births })
            births = births + 1
          end
        end
      end
    end
  end
  v.pop = #m
  return nonage, dtag, births
end

-- Save form (D-026): people as positional records, same data. Field order is part of the save
-- format (bump SAVE_VERSION when it changes).
local JOBS = { "child", "farmer", "fisher", "woodcutter", "elder" }
local JOB_ID = {}
for i = 1, #JOBS do JOB_ID[JOBS[i]] = i end

function people.pack(map)
  local ids = {}
  for id in pairs(map) do ids[#ids + 1] = id end
  table.sort(ids)
  local out = {}
  for i = 1, #ids do
    local p = map[ids[i]]
    out[i] = { p.id, p.key, p.name, p.surname, p.sex == "f" and 1 or 0, p.born, p.village,
      JOB_ID[p.job] or 1, p.health, p.hunger, p.partner, p.mother, p.father, p.children,
      table.concat(p.traits, ","), p.mem, p.mood, p.u, p.dead or false, p.died_of or "", p.death_tag or 0 }
  end
  return out
end

function people.unpack(list)
  local map = {}
  for i = 1, #list do
    local r = list[i]
    local traits = {}
    for t in r[15]:gmatch("[^,]+") do traits[#traits + 1] = t end
    local p = { id = r[1], key = r[2], name = r[3], surname = r[4], sex = r[5] == 1 and "f" or "m",
      born = r[6], village = r[7], job = JOBS[r[8]], health = r[9], hunger = r[10], partner = r[11],
      mother = r[12], father = r[13], children = r[14], traits = traits, mem = r[16], mood = r[17],
      u = r[18] }
    if r[19] then p.dead = r[19]; p.died_of = r[20]; p.death_tag = r[21] end
    map[p.id] = p
  end
  return map
end

-- Year-boundary snap: round the continuous person state so saves are small (and a reloaded
-- world equals the live one, which is rounded the same way).
function people.snap(W)
  local P = W.ent.people
  if not P then return end
  local r = 0.01
  for _, p in pairs(P) do   -- independent per person: order-free
    p.health = floor(p.health / r + 0.5) * r
    p.hunger = floor(p.hunger / r + 0.5) * r
    p.mood = floor(p.mood / r + 0.5) * r
    local mem = p.mem
    for k = 2, #mem, 2 do mem[k] = floor(mem[k] * 1000 + 0.5) / 1000 end
  end
end

-- Yearly housekeeping: prune the long dead (kept for family trees / inspector).
function people.prune(W)
  local E = W.ent
  local d = E.dead_ids
  local keep = cfgmod.PEOPLE.keep_dead_years * 48
  local k = 0
  for i = 1, #d do
    local p = E.people[d[i]]
    if p and W.tick - p.dead > keep then
      E.people[d[i]] = nil
    else
      k = k + 1
      d[k] = d[i]
    end
  end
  for i = #d, k + 1, -1 do d[i] = nil end
end

return people
