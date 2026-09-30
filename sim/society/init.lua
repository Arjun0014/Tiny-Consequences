-- The settlement and agent layer (docs/world/10_WORLD_SYSTEMS.md §3.4–3.10, §5). System slot 10
-- (replaces the old villages/people systems, D-050).
--
-- Rhythm:
--   hour(W)     every hour: villages whose local midnight falls in this UTC hour run their day
--               step; travelling parties arrive.
--   day step    settle the day that ended (yesterday's plan: work done, catches landed, births,
--               food and fuel consumed), then plan the new day (weather, daylight, trips).
--   village week (its weekday): bodies and demography, construction, field plan, stocks' causes,
--               labour assignment.   village month: courtship, marriages, migration.
--   update(W)   the planet week: crop growth from the week's temperatures (farming.weekly).
--   year(W)     memories fade, the long dead are pruned, ruins decay.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local daylight = require("sim.society.daylight")
local wday = require("sim.society.weather_day")
local sites = require("sim.society.sites")
local ents = require("sim.society.entities")
local farming = require("sim.society.farming")
local fishing = require("sim.society.fishing")
local labour = require("sim.society.labour")
local construction = require("sim.society.construction")
local economy = require("sim.society.economy")
local life = require("sim.society.life")
local mobility = require("sim.society.mobility")
local relations = require("sim.society.relations")
local crafts = require("sim.society.crafts")
local status = require("sim.society.status")
local herds = require("sim.society.herds")
local conflict = require("sim.society.conflict")
local events = require("sim.causality.events")

local sys = { name = "villages" }   -- keeps the old slot name (profiles, P07)
local floor, min, max = math.floor, math.min, math.max

-- Derived per-village geometry (not saved): farm ring, woodlot candidates, the "near" set.
function sys.geo(W, v)
  local G = W.vgeo[v.id]
  if not G then
    local cfg = cfgmod.SOCIETY
    local ring = sites.rings(W, v.cell, sites.radius(W, cfg.farm_radius))
    local near = {}
    local nc = sites.rings(W, v.cell, sites.radius(W, cfgmod.VILLAGE.near))
    for i = 1, #nc do near[nc[i]] = true end
    G = { ring = ring, near = near }
    W.vgeo[v.id] = G
  end
  return G
end

-- How many living villages reach each land cell with their fields (derived): free land that two
-- neighbours could both farm is shared between them in their capacity estimates.
function sys.ring_counts(W)
  local rc = {}
  local vs = W.ent.villages
  for i = 1, #vs do
    local v = vs[i]
    if v.alive and not v.placeholder then
      local ring = sys.geo(W, v).ring
      for k = 1, #ring do local c = ring[k]; rc[c] = (rc[c] or 0) + 1 end
    end
  end
  W.soc.ring_count = rc
end

function sys.register_village(W, v)
  local b = v.bucket
  local B = W.soc.buckets
  B[b] = B[b] or {}
  local l = B[b]
  for i = 1, #l do if l[i] == v.id then return end end
  l[#l + 1] = v.id
  table.sort(l)
  sys.geo(W, v)
  sys.ring_counts(W)
end

function sys.unregister_village(W, v)
  local l = W.soc.buckets[v.bucket]
  if l then ents.remove_id(l, v.id) end
  sys.ring_counts(W)
end

mobility.on_register = function(W, v) sys.register_village(W, v) end
relations.depart = mobility.depart
status.depart = mobility.depart
require("sim.society.sea").lose = fishing.lose
mobility.bride_terms = life.bride_terms
status.best_site = function(W, v)
  -- (a lineage leaving an island with no room left sails for another shore)
  return sites.near_site(W, v.cell, cfgmod.SOCIETY.bud_radius) or require("sim.society.sea").site(W, v, 12)
end
conflict.depart = mobility.depart
conflict.rel = relations
conflict.status = status
mobility.battle = conflict.battle
mobility.raiders_home = conflict.raiders_home
relations.honour_leader = status.honour_leader
relations.wrong = conflict.wrong
relations.temper = status.temper
farming.on_encroach = conflict.encroach
economy.on_harvest = conflict.pay_tribute
mobility.on_unregister = function(W, v) sys.unregister_village(W, v) end

function sys.init(W)
  ents.init(W)
  W.scratch.wx = W.scratch.wx or {}
end

function sys.rebuild(W)
  W.vgeo = {}
  W.vsea = {}
  W.soc = { buckets = {} }
  local vs = W.ent.villages
  if not vs then return end
  mobility.rebuild_list(W)
  status.ensure(W)
  for i = 1, #vs do
    local v = vs[i]
    if v.alive and not v.placeholder then sys.register_village(W, v) end
  end
  sys.ring_counts(W)
end

-- Fire and floods in the village (weekly): damage, burned stores, exposure deaths; hearth fires.
local function hazards(W, v)
  local F, C = W.F, W.C
  local cfg = cfgmod.VILLAGE
  local P = W.ent.people
  local fire, ftag = 0, 0
  for i = 1, #v.land do
    local c = v.land[i]
    if F.fire[c] > fire then fire, ftag = F.fire[c], C.fire[c] end
  end
  local exposure, etag = 0, 0
  if fire > 0 then
    construction.damage(W, v, 0.3 * fire)
    local burnt = min(cfg.granary_burn_max, cfg.granary_burn * fire)
    v.stock.grain = v.stock.grain * (1 - burnt)
    v.stock.wood = v.stock.wood * (1 - burnt)
    exposure, etag = 0.01 * fire, ftag
    if ftag ~= 0 then v.foodtag[1] = ftag; v.foodidle[1] = 0; v.cause = ftag end
  elseif W.D.flood_last and W.tick - W.D.flood_last[v.cell] <= 1 then
    construction.damage(W, v, 0.1)
    exposure, etag = 0.004, C.flow[v.cell]
    if W.hour - (v.flooded or -1e9) >= cfg.flood_news_years * 8064 then
      v.flooded = W.hour
      economy.emit(W, "flooded_village", v.cell, C.flow[v.cell], 1, { village = v.id })
    end
  end
  if exposure > 0 then
    local week = floor(W.hour / 168)
    local u0 = rng.float(W.seed, rng.S.VILLAGE, v.key_cell, week, 9)
    local dead = false
    for i = 1, #v.members do
      local p = P[v.members[i]]
      if (u0 + p.u * 1.618) % 1 < exposure then
        ents.kill(W, p, fire > 0 and "fire" or "flood", etag)
        v.nonage = (v.nonage or 0) + 1
        dead = true
      end
    end
    if dead then ents.compact(W, v) end
  end
  -- hearth fires: rare, likelier when dry and crowded
  local dry = 1 - F.soil_water[v.cell] / cfgmod.HYDRO.field_capacity
  if dry > 0 and rng.float(W.seed, rng.S.VILLAGE, v.key_cell, W.hour, 1) < cfg.accident * v.pop * dry * 0.25 then
    local target = v.farms[1] or v.cell
    require("sim.systems.fire").ignite(W, target, 0, "village_accident")
  end
end

local function village_week(W, v)
  life.weekly(W, v)
  economy.need_day(W, v)
  v.need_pop = v.pop
  labour.fuel_households(W, v)
  if v.pop == 0 then return end
  hazards(W, v)
  construction.decay(W, v)
  local S = v.season.phase
  if S == "winter" or S == "sowing" then
    -- the village plans with its working-age people (not today's fitness: the sick recover)
    local workers = 0
    local P = W.ent.people
    local cfg = cfgmod.SOCIETY
    for i = 1, #v.members do
      local a = (W.hour - P[v.members[i]].born) / 8064
      if a >= cfg.adult_age and a < cfg.elder_age and P[v.members[i]].away == 0 then workers = workers + 1 end
    end
    farming.plan(W, v, workers)
  end
  farming.capacity(W, v)
  construction.plan(W, v)
  if not v.herd then herds.seed(W, v, 1) end
  herds.weekly(W, v)
  require("sim.society.skills").weekly(W, v)
  crafts.weekly(W, v)
  economy.weekly(W, v)
  if v.boats[1] then fishing.rank(W, v, v.plan.hours or 9) end
  life.memories(W, v)
  v.replan = true
  -- the state label (inspector, dialogue)
  if v.famine then v.state = "famine"
  elseif v.rationing then v.state = "rationing"
  elseif W.hour - v.founded < 2 * 8064 then v.state = "new"
  elseif v.food_days < 60 or v.wood_days < 10 then v.state = "struggling"
  else v.state = "thriving" end
  v.houses = construction.count_kind(W, v, "house", "standing") + construction.count_kind(W, v, "hut", "standing")
end

local function village_month(W, v)
  life.monthly(W, v)
  economy.monthly(W, v)
  economy.dread(W, v)
  mobility.monthly(W, v)
  if v.alive and v.pop > 0 then
    status.monthly(W, v)
    relations.monthly(W, v)
    conflict.fade(W, v)
    conflict.rivals(W, v)
    conflict.monthly(W, v)
  end
  local cd = v.month_cold or 0
  local cm = v.cold_months or 0
  if cd > 5 then cm = min(12, cm + 1) elseif cm > 0 then cm = cm - 0.5 end
  v.cold_months, v.month_cold = cm, 0
end

-- The village's day step, at its local midnight (h0 = W.hour).
local function day_step(W, v)
  local h0 = W.hour
  local plan = v.plan
  -- 1. the day that ended: its work, its catches, its births, its meals and fuel
  if plan.day and plan.day >= 0 then
    v.day_grain, v.day_fresh, v.day_wood = 0, 0, 0
    local landed, ftag = fishing.settle(W, v)
    labour.execute(W, v, plan.hours or 0, plan)
    v.wk_grain = (v.wk_grain or 0) + v.day_grain
    v.wk_fresh = (v.wk_fresh or 0) + v.day_fresh + landed
    v.wk_catch = (v.wk_catch or 0) + landed
    -- (a lost boat explains a poor catch only until the village has as many boats again, or for
    -- two years: then it is the new normal)
    if v.boat_tag and v.boat_tag ~= 0 and (#v.boats >= (v.boats_before or 0)
      or h0 - (v.boat_tag_h or h0) > 2 * 8064) then v.boat_tag = 0 end
    if landed + v.day_fresh < v.norm.fresh_in / 7 * 0.5 then
      v.fresh_tag = ftag ~= 0 and ftag or (v.boat_tag or 0)
    elseif ftag ~= 0 then
      v.fresh_tag = ftag
    else
      v.fresh_tag = 0
    end
    life.births(W, v)
    economy.daily(W, v, plan)
    v.month_ration_sum = (v.month_ration_sum or 0) + v.ration
    v.month_ration_n = (v.month_ration_n or 0) + 1
    if (plan.temp or 10) < cfgmod.SOCIETY.cold_temp and v.stock.wood <= 0 then v.month_cold = (v.month_cold or 0) + 1 end
  end
  if v.pop == 0 then
    local away = false
    local P = W.ent.people
    local list = W.ent.party_list or {}
    for i = 1, #list do
      local party = W.ent.parties[list[i]]
      if party and party.to == v.id then away = true end
    end
    if not away then mobility.abandon(W, v) end
    return
  end
  -- 2. the new day
  local day = floor(h0 / 24)
  if day % 7 == v.week_offset then village_week(W, v) end
  if v.pop == 0 then return end
  if day % 28 == v.week_offset + 7 * v.month_offset then village_month(W, v) end
  if not v.alive or v.pop == 0 then return end
  local wx = wday.get(W, v.cell, h0, W.scratch.wx)
  local doy = floor((h0 + 12) / 24) % 336
  local sr, ss = daylight.sun_times(W.grid.lat[v.cell], doy)
  local wake, ws, we, sleep, hours = daylight.rhythm(sr, ss)
  plan.day, plan.h0 = day, h0
  plan.wake, plan.ws, plan.we, plan.sleep, plan.hours = wake, ws, we, sleep, hours
  plan.wet, plan.storm, plan.snow, plan.temp, plan.rain = wx.wet, wx.storm, wx.snow, wx.temp, wx.rain
  if v.replan or not plan.assigned then labour.assign(W, v, hours) end
  if v.boats[1] then require("sim.society.sea").daily(W, v) end
  fishing.plan(W, v, h0, ws, we, wx)
  farming.daily(W, v, wx.wet)
end

function sys.hour(W)
  local list = W.soc.buckets[W.hour % 24]
  if list then
    local vs = W.ent.villages
    for i = #list, 1, -1 do
      local v = vs[list[i]]
      if v and v.alive then day_step(W, v) end
    end
  end
  mobility.hour(W)
end

-- The planet week: crops grow with the week's temperatures; the planet's population total.
function sys.update(W)
  local vs = W.ent.villages
  local total = 0
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      total = total + v.pop
      if v.farms[1] then farming.weekly(W, v) else v.season.t_prev = W.F.temp[v.cell] end
    end
  end
  W.S.society.pop = total
end

function sys.year(W)
  local vs = W.ent.villages
  local P = W.ent.people
  local f = cfgmod.PEOPLE.memory_fade
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      for k = 1, #v.members do ents.fade_memories(P[v.members[k]], f) end
      -- last year's flows (harvest, catch, eaten, wood cut and burned, hours by task...) for the
      -- inspector and the tools; this year's account starts empty
      v.acc_last = v.acc
      v.acc = {}
    end
  end
  -- ruins decay; abandoned villages' ruins vanish after ruin_years
  local B = W.ent.buildings
  local cfg = cfgmod.SOCIETY
  for i = 1, #vs do
    local v = vs[i]
    for k = #v.buildings, 1, -1 do
      local b = B[v.buildings[k]]
      if b and b.state == "ruin" and W.hour - (b.ruined or W.hour) > cfg.ruin_years[2] * 8064 then
        b.state = "gone"
        B[b.id] = nil
        table.remove(v.buildings, k)
      elseif b and b.state == "gone" then
        B[b.id] = nil
        table.remove(v.buildings, k)
      end
    end
    if not v.alive and v.ruin and W.hour - v.abandoned > cfg.ruin_years[1] * 8064 then
      v.ruin = false
      if W.S.ruins[v.cell] then W.S.ruins[v.cell] = nil end
    end
  end
  ents.prune(W)
  mobility.trails_year(W)
end

-- Year-boundary snap: round continuous person state (smaller saves; live = reloaded).
function sys.snap(W)
  local P = W.ent.people
  local ids = {}
  for id in pairs(P) do ids[#ids + 1] = id end
  table.sort(ids)
  for i = 1, #ids do
    local p = P[ids[i]]
    p.health = floor(p.health * 100 + 0.5) / 100
    p.nutrition = floor(p.nutrition * 1000 + 0.5) / 1000
    p.mood = floor(p.mood * 100 + 0.5) / 100
    local mem = p.mem
    for k = 2, #mem, 2 do mem[k] = floor(mem[k] * 1000 + 0.5) / 1000 end
  end
end

-- Shift stored hours by dt weeks (after the worldgen spin-up).
function sys.shift_ticks(W, dt)
  local dh = dt * 168
  local E = W.ent
  for i = 1, #E.villages do
    local v = E.villages[i]
    v.founded = v.founded + dh
    v.famine_last = v.famine_last + dh
    if v.abandoned then v.abandoned = v.abandoned + dh end
    if v.bud_last then v.bud_last = v.bud_last + dh end
    if v.flooded then v.flooded = v.flooded + dh end
    if v.expanded then v.expanded = v.expanded + dh end
    if v.site_stalled then v.site_stalled = v.site_stalled + dh end
    if v.harvest_end then v.harvest_end = v.harvest_end + dh end
    local pl = v.plan
    if pl.h0 then pl.h0 = pl.h0 + dh end
    if pl.assigned then pl.assigned = pl.assigned + dh end
    if pl.day and pl.day >= 0 then pl.day = pl.day + dt * 7 end
    if v.logged then
      local cells = {}
      for c in pairs(v.logged) do cells[#cells + 1] = c end
      table.sort(cells)
      for k = 1, #cells do v.logged[cells[k]] = v.logged[cells[k]] + dh end
    end
  end
  local function shift_map(map, fn)
    local ids = {}
    for id in pairs(map) do ids[#ids + 1] = id end
    table.sort(ids)
    for k = 1, #ids do fn(map[ids[k]]) end
  end
  shift_map(E.people, function(p)
    p.born = p.born + dh
    if p.dead then p.dead = p.dead + dh end
    if p.preg_due > 0 then p.preg_due = p.preg_due + dh end
    if p.nursing_until > 0 then p.nursing_until = p.nursing_until + dh end
    if p.wait_since then p.wait_since = p.wait_since + dh end
  end)
  shift_map(E.households, function(h)
    h.formed = h.formed + dh
    if h.homeless_since then h.homeless_since = h.homeless_since + dh end
    if h.arrived then h.arrived = h.arrived + dh end
  end)
  shift_map(E.buildings, function(b)
    b.started = b.started + dh
    if b.built and b.built >= 0 then b.built = b.built + dh end
    if b.ruined then b.ruined = b.ruined + dh end
  end)
  shift_map(E.boats, function(b)
    b.built = b.built + dh
    if b.lost then b.lost = b.lost + dh end
    local t = b.trip
    if t then t.depart, t.at, t.leave, t.ret = t.depart + dh, t.at + dh, t.leave + dh, t.ret + dh end
  end)
  shift_map(E.parties, function(pa)
    pa.depart, pa.arrive = pa.depart + dh, pa.arrive + dh
  end)
  local ruins = W.S.ruins
  local cells = {}
  for c in pairs(ruins) do cells[#cells + 1] = c end
  table.sort(cells)
  for i = 1, #cells do ruins[cells[i]] = ruins[cells[i]] + dt end
end

return sys
