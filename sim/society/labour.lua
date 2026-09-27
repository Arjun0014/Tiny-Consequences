-- Labour: the village's daily brain (docs/world/11_ECONOMY.md §6).
-- Weekly (and when a task finishes or a harvest ripens) the village lists what needs doing, in
-- person-hours, with priorities from real needs (Chayanov: needs, not maximums — when stocks are
-- at their targets fuel and fishing demands shrink and people get leisure), and assigns workers:
-- first by occupation (a fisher keeps their boat), then to the most urgent unmet need. Each day
-- the groups work the hours daylight, weather, travel and their fitness allow. The assignment is
-- stored on the people (task, site), so the view shows exactly what they do.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")
local sites = require("sim.society.sites")
local ents = require("sim.society.entities")
local farming = require("sim.society.farming")
local construction = require("sim.society.construction")

local M = {}

-- add x to the village's yearly flow account under key k
local function acc(v, k, x)
  local a = v.acc
  if not a then a = {}; v.acc = a end
  a[k] = (a[k] or 0) + x
end
local floor, min, max = math.floor, math.min, math.max
local FFOREST = fields.BY_NAME.forest
local FGRAZ = fields.BY_NAME.grazers

local FARM = { harvest = true, sow = true, tend = true, clear = true }
local LIGHT = { harvest = true, tend = true, fuel = true, sow = true, brush = true, forage = true }   -- children plant, elders glean
local AFFINITY = { farmer = FARM, fisher = { fish = true }, woodcutter = { fuel = true, timber = true, brush = true },
  builder = { build = true } }

-- Fuel a village burns in a day at temperature T (WU), by its households' houses.
function M.fuel_day(W, v, T)
  local cfg = cfgmod.SOCIETY
  local heat = cfg.fuel_comfort - T
  if heat < 0 then heat = 0 end
  if not v.fuel_n or v.fuel_pop ~= v.pop then M.fuel_households(W, v) end
  return v.fuel_n * cfg.fuel_cook + cfg.fuel_heat * heat * v.fuel_draught
end

-- Cache: how many households burn fuel, and the sum of their homes' draughtiness.
function M.fuel_households(W, v)
  local B, H = W.ent.buildings, W.ent.households
  local n, dr = 0, 0
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h and #h.members > 0 then
      local b = B[h.home]
      local cond = (b and b.state == "standing") and b.condition or 0.3
      n = n + 1
      dr = dr + (1.6 - 0.6 * cond)
    end
  end
  v.fuel_n, v.fuel_draught, v.fuel_pop = n, dr, v.pop
end

-- The best woodlot for a village: forest density x hours left after walking, within reach.
function M.woodlot(W, v, hours_day)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local G = W.vgeo[v.id]
  local cand = G.woods
  if not cand then
    cand = sites.rings(W, v.cell, cfg.wood_radius, function(j) return W.geo.ocean[j] == 0 end)
    G.woods = cand
  end
  local best, bs = nil, 0
  for i = 1, #cand do
    local c = cand[i]
    local f = F.forest[c]
    if f > 0.02 and F.fire[c] == 0 then
      local dc = G.walkd
      if not dc then dc = {}; G.walkd = dc end
      local walk = sites.dist_cached(W, dc, v.cell, c) * cfg.walk_h
      local h = hours_day - 2 * walk
      if h > 0.5 then
        local s = f / (f + cfg.wood_half) * h
        if s > bs then best, bs = c, s end
      end
    end
  end
  return best, bs
end

-- Where no woods are within reach, people burn brush, dung and straw: the best grass patch near
-- the village (fuel only — no timber). Returns cell, score (WU-equivalent hours).
function M.brush(W, v, hours_day)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local G = W.vgeo[v.id]
  local ring = G.ring
  local best, bs = nil, 0
  for i = 1, #ring do
    local c = ring[i]
    local g = F.grass[c]
    if W.geo.ocean[c] == 0 and g > 0.05 and F.fire[c] == 0 then
      local dc = G.walkd
      if not dc then dc = {}; G.walkd = dc end
      local walk = sites.dist_cached(W, dc, v.cell, c) * cfg.walk_h
      local h = hours_day - 2 * walk
      if h > 0.5 then
        local s = g / (g + cfg.brush_half) * h * cfg.brush_rate / cfg.wood_rate
        if s > bs then best, bs = c, s end
      end
    end
  end
  return best, bs
end

-- Wild food (roots, seeds, greens, nuts, fruit, shellfish at the shore): the famine food of every
-- farming people. The best patch near the village by vegetation after walking; the take comes out
-- of the patch's grass (roots and seeds). Returns cell, score (FU per day-hour equivalent).
function M.forage_cell(W, v, hours_day)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local G = W.vgeo[v.id]
  local ring = G.ring
  local lu = F.land_use
  local best, bs = nil, 0
  for i = 1, #ring do
    local c = ring[i]
    if W.geo.ocean[c] == 0 and lu[c] <= 0 and F.fire[c] == 0 then
      local veg = F.grass[c] + 0.6 * F.forest[c]
      if veg > 0.05 then
        local dc = G.walkd
        if not dc then dc = {}; G.walkd = dc end
        local h = hours_day - 2 * sites.dist_cached(W, dc, v.cell, c) * cfg.walk_h
        if h > 0.5 then
          local s = veg / (veg + cfg.forage_half) * h
          if s > bs then best, bs = c, s end
        end
      end
    end
  end
  return best, bs
end

-- Wild food gathered in `eff` effective hours at cell c (season: plants give little in the cold).
local function forage_yield(W, c, eff)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local veg = F.grass[c] + 0.6 * F.forest[c]
  local T = F.temp[c]
  local season = T <= 0 and 0.15 or (T >= 10 and 1 or 0.15 + 0.085 * T)
  return eff * cfg.forage_rate * veg / (veg + cfg.forage_half) * season
end

-- Worker capacity (0..1) and whether they can only do light work.
local function capacity(W, p, hour)
  local cfg = cfgmod.SOCIETY
  if p.away ~= 0 then return 0, false end
  local age = (hour - p.born) / 8064
  if age < cfg.light_work_age or age >= cfg.frail_age then return 0, false end
  local fit = (0.35 + 0.65 * p.nutrition) * min(1, p.health / 40)
  if p.health < 25 then return 0, false end
  if p.preg_due > 0 and p.preg_due - hour < 30 * 24 then return 0, false end
  local light = false
  if age < cfg.adult_age or age >= cfg.elder_age then fit = fit * 0.5; light = true end
  if p.nursing_until > hour then fit = fit * 0.5 end
  return fit, light
end
M.capacity = capacity

local D = {}   -- scratch demand list (entries reused)
local FD = {}
local LIST, FREE, FCAP, FLIGHT, POOL, REST = {}, {}, {}, {}, {}, {}
local CURSOR = { farmer = 1, fisher = 1, woodcutter = 1, builder = 1 }

local function by_prio(a, b)
  if a.prio ~= b.prio then return a.prio > b.prio end
  return a.ord < b.ord
end

local function site_prio(b)
  if b.state == "site" then return construction.PRIO[b.kind] or 30 end
  return construction.PRIO.repair
end
local function by_site(a, b)
  local pa, pb = site_prio(a), site_prio(b)
  if pa ~= pb then return pa > pb end
  return a.id < b.id
end

-- Person p (capacity c) takes demand d for the week.
local function take(Bo, p, c, d, week_h)
  p.task, p.site = d.kind, d.target
  d.got = d.got + c * week_h
  d.n = d.n + 1
  d.cap = d.cap + c
  if d.kind == "fish" then
    local bo = Bo[d.target]
    if bo then bo.crew[#bo.crew + 1] = p.id end
  elseif FARM[d.kind] then
    local n = d.nwho + 1
    d.nwho = n
    local who, wc = d.who, d.whocap
    if not who then who = {}; wc = {}; d.who, d.whocap = who, wc end
    who[n], wc[n] = p, c
  end
end

-- Weekly assignment. hours_day: expected working hours per day this week.
function M.assign(W, v, hours_day)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local B = W.ent.buildings
  local Bo = W.ent.boats
  local hour = W.hour
  local nd = 0
  local function demand(kind, target, prio, need, maxw)
    if need <= 0 then return end
    nd = nd + 1
    local d = D[nd]
    if not d then d = {}; D[nd] = d end
    d.kind, d.target, d.prio, d.need, d.maxw, d.got, d.n, d.cap = kind, target, prio, need, maxw or 999, 0, 0, 0
  end
  -- farming
  farming.demands(W, v, FD)
  local field_target = v.farms[1] or v.cell
  demand("harvest", field_target, 100, FD.harvest)
  demand("sow", field_target, 90, FD.sow)
  demand("tend", field_target, 50, FD.tend)
  demand("clear", field_target, 40, FD.clear)
  -- fuel: bring wood up to its target
  local T = W.F.temp[v.cell]
  local fday = M.fuel_day(W, v, T)
  local lwk = farming.local_week(W, v)
  local days = (lwk >= 30 or lwk < 6) and cfg.fuel_days_winter or cfg.fuel_days
  local wood_target = fday * days
  local lot, score = M.woodlot(W, v, hours_day)
  local fuel_kind = "fuel"
  if not lot or score < 1 then
    local bl, bsc = M.brush(W, v, hours_day)
    if bl and bsc > (score or 0) then lot, score, fuel_kind = bl, bsc, "brush" end
  end
  v.timber_near = (fuel_kind == "fuel") and lot ~= nil
  local rate = lot and cfg.wood_rate * score / max(0.5, hours_day) or 0
  -- timber for sites and repairs
  local timber = 0
  local sites_list = {}
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and (b.state == "site" or (b.state == "standing" and b.repair)) then
      local h, w = construction.remaining(b)
      timber = timber + w
      sites_list[#sites_list + 1] = b
    end
  end
  if lot and rate > 0 then
    local short = wood_target - v.stock.wood
    local prio = v.stock.wood < fday * 14 and 80 or 40
    demand(fuel_kind, lot, prio, short / rate + fday * 7 / rate, nil)
    local tshort = timber - max(0, v.stock.wood - wood_target * 0.5)
    if tshort > 0 then demand(fuel_kind == "fuel" and "timber" or "brush", lot, 60, tshort / rate) end
  end
  -- fishing: crews for the boats, sized to the fresh food the village wants
  if #v.boats > 0 and #v.fishing > 0 then
    local prio = v.food_days < 60 and 70 or 45
    -- crews for the boats the village has been sending out (and one more), not for idle hulls
    local want_boats = min(#v.boats, floor((v.boats_used or #v.boats) + 1.5))
    if v.food_days < 60 then want_boats = #v.boats end
    for i = 1, min(want_boats, #v.boats) do
      demand("fish", v.boats[i], prio, cfg.crew * hours_day * cfg.work_days, cfg.crew)
    end
  end
  -- building (only where wood exists or is coming)
  table.sort(sites_list, by_site)
  for i = 1, #sites_list do
    local b = sites_list[i]
    local h = construction.remaining(b)
    local prio = site_prio(b)
    if v.stock.wood > 1 or timber <= 0 then demand("build", b.id, prio, h, cfg.builders_per_site) end
  end
  -- foraging when food runs short: everyone who can walks out for wild food
  if v.food_days < cfg.forage_days and hours_day > 1 then
    local fc, fs = M.forage_cell(W, v, hours_day)
    if fc and fs > 0.5 then
      local rate = cfg.forage_rate * fs / max(0.5, hours_day)
      local share = v.food_days < 14 and 0.6 or 0.3
      demand("forage", fc, v.food_days < 14 and 75 or 62, v.need_day * 7 * share / max(0.05, rate), nil)
    end
  end
  -- hunting when food runs short (until the herds of W4)
  if v.food_days < 90 then demand("hunt", v.cell, v.food_days < 30 and 55 or 25, hours_day * 12, 4) end
  -- sort by priority (stable: insertion order breaks ties)
  for i = 1, nd do D[i].ord = i end
  local list = LIST
  for i = 1, nd do list[i] = D[i] end
  for i = #list, nd + 1, -1 do list[i] = nil end
  table.sort(list, by_prio)
  -- clear crews
  for i = 1, #v.boats do
    local bo = Bo[v.boats[i]]
    if bo then local cr = bo.crew; for k = #cr, 1, -1 do cr[k] = nil end end
  end
  local week_h = hours_day * cfg.work_days
  -- the able: people and their capacity (scratch arrays by position, no per-call allocation)
  local nf = 0
  local mem = v.members
  for i = 1, #mem do
    local p = P[mem[i]]
    p.task, p.site = "", 0
    local c, light = capacity(W, p, hour)
    if c > 0 then
      nf = nf + 1
      FREE[nf], FCAP[nf], FLIGHT[nf] = p, c, light
    end
  end
  for i = 1, nd do local d = list[i]; d.nwho = 0 end
  -- pass 0: sowing and harvest windows are short — everyone lends a hand (fishers too unless the
  -- village lives on the catch, woodcutters unless the fire is going out)
  local need_fish = v.food_days < 60
  local need_wood = v.stock.wood < fday * 14
  local np = 0
  for i = 1, nf do
    local p = FREE[i]
    local urgent = false
    if not (p.job == "fisher" and need_fish) and not (p.job == "woodcutter" and need_wood) then
      local light = FLIGHT[i]
      for k = 1, nd do
        local d = list[k]
        if d.prio < 85 then break end
        if d.got < d.need and d.n < d.maxw and (not light or LIGHT[d.kind]) then
          take(Bo, p, FCAP[i], d, week_h); urgent = true; break
        end
      end
    end
    if not urgent then np = np + 1; POOL[np] = i end
  end
  -- pass 1: occupations (a fisher keeps their boat, a farmer farms). Demands only ever fill up,
  -- so each trade keeps a cursor at its first open demand (no rescans: O(people + demands)).
  for k in pairs(CURSOR) do CURSOR[k] = 1 end
  local nr = 0
  for j = 1, np do
    local i = POOL[j]
    local p = FREE[i]
    local aff = AFFINITY[p.job]
    local done = false
    if aff and not FLIGHT[i] then
      local k = CURSOR[p.job] or 1
      while k <= nd do
        local d = list[k]
        if aff[d.kind] and d.got < d.need and d.n < d.maxw then take(Bo, p, FCAP[i], d, week_h); done = true; break end
        k = k + 1
      end
      CURSOR[p.job] = k
    end
    if not done then nr = nr + 1; REST[nr] = i end
  end
  -- pass 2: everyone else to the most urgent unmet need (a cursor for the able, one for light work)
  local ka, kl = 1, 1
  for j = 1, nr do
    local i = REST[j]
    local light = FLIGHT[i]
    local k = light and kl or ka
    while k <= nd do
      local d = list[k]
      if d.got < d.need and d.n < d.maxw and (not light or LIGHT[d.kind]) then
        take(Bo, FREE[i], FCAP[i], d, week_h); break
      end
      k = k + 1
    end
    if light then kl = k else ka = k end
  end
  -- groups for the daily execution (saved: reused in place); farm workers are sent to the
  -- fields their hours will go to
  local groups = v.plan.groups
  local ng = 0
  for k = 1, nd do
    local d = list[k]
    if d.n > 0 then
      ng = ng + 1
      local g = groups[ng]
      if not g then g = {}; groups[ng] = g end
      g.kind, g.target, g.cap, g.n = d.kind, d.target, d.cap, d.n
      if FARM[d.kind] then farming.field_sites(W, v, d.kind, d.who, d.whocap, d.nwho, week_h) end
    end
  end
  for k = #groups, ng + 1, -1 do groups[k] = nil end
  v.plan.assigned = hour
  v.replan = false
  -- workforce accounting (labour_tag: why the workforce is below normal)
  local cap = 0
  for i = 1, nf do cap = cap + FCAP[i] end
  for i = 1, nf do FREE[i] = nil end
  v.workforce = cap
  if (v.workforce_norm or 0) <= 0 then v.workforce_norm = cap end
  v.workforce_norm = v.workforce_norm + (cap - v.workforce_norm) / 104
end

-- Weather factor for outdoor work of `kind` today.
local function weather_factor(kind, wx)
  local cfg = cfgmod.SOCIETY
  if wx.storm then return 0 end
  if wx.snow then
    if FARM[kind] then return 0 end
    if kind == "build" then return 0.3 end
    return 0.6
  end
  if wx.wet then
    if FARM[kind] or kind == "build" then return cfg.weather_rain end
    return 0.75
  end
  return 1
end

-- Daily execution: each group works today's hours. hours_day = the village's working hours today.
function M.execute(W, v, hours_day, wx)
  local cfg = cfgmod.SOCIETY
  local F, C, I = W.F, W.C, W.I
  local B = W.ent.buildings
  local groups = v.plan.groups
  local short_area = 0
  for i = 1, #groups do
    local g = groups[i]
    local k = g.kind
    if k ~= "fish" then
      local wf = weather_factor(k, wx)
      local travel = 0
      if k ~= "build" then
        local G = W.vgeo[v.id]
        local dc = G.walkd
        if not dc then dc = {}; G.walkd = dc end
        travel = sites.dist_cached(W, dc, v.cell, g.target) * cfg.walk_h
      end
      local h = (hours_day - 2 * travel) * wf
      if h < 0 then h = 0 end
      local eff = h * g.cap
      acc(v, "h_" .. k, eff)
      if FARM[k] then
        local used = farming.work(W, v, k, eff)
        if eff > 0 and used < 1e-6 then v.replan = true end
      elseif k == "brush" then
        -- brush, dung and straw: fuel from the grass patch (the grass is cropped, not killed)
        local c = g.target
        local gr = F.grass[c]
        local wood = eff * cfg.brush_rate * gr / (gr + cfg.brush_half)
        local dg = wood / cfg.forest_wu * 0.5
        if dg > gr * 0.1 then dg = gr * 0.1 end
        F.grass[c] = gr - dg
        v.stock.wood = v.stock.wood + wood
        v.day_wood = (v.day_wood or 0) + wood
        acc(v, "brush", wood)
      elseif k == "fuel" or k == "timber" then
        local c = g.target
        local f = F.forest[c]
        local wood = eff * cfg.wood_rate * f / (f + cfg.wood_half)
        local df = wood / cfg.forest_wu
        if df > f then df = f; wood = f * cfg.forest_wu end
        if df > 0 then
          F.forest[c] = f - df
          W.P.forest[c] = F.forest[c]
          local vt = v.cause
          if vt ~= 0 or C.forest[c] ~= 0 then
            blame.step(C.forest, I.forest, c, -df, FFOREST.eps, FFOREST.hold, -df, vt, 0, 0, 0, 0, 0, 0)
          end
          v.stock.wood = v.stock.wood + wood
          v.day_wood = (v.day_wood or 0) + wood
          acc(v, "wood_cut", wood)
          v.logged = v.logged or {}
          v.logged[c] = W.hour
        end
        if F.forest[c] < 0.03 then v.replan = true end
      elseif k == "build" then
        local b = B[g.target]
        if b and (b.state == "site" or b.repair) then
          construction.work(W, v, b, eff)
          if b.state ~= "site" and not b.repair then v.replan = true end
        else
          v.replan = true
        end
      elseif k == "forage" then
        local c = g.target
        local food = forage_yield(W, c, eff)
        local gr = F.grass[c]
        local dg = food / cfg.forage_fu
        if dg > gr * 0.1 then dg = gr * 0.1; food = dg * cfg.forage_fu end
        F.grass[c] = gr - dg
        v.stock.fresh = v.stock.fresh + food
        v.day_fresh = (v.day_fresh or 0) + food
        acc(v, "forage", food)
      elseif k == "hunt" then
        -- the best grazer cell near the village (fauna densities until W4's herds)
        local ring = W.vgeo[v.id].ring
        local best, bg = nil, 0
        if ring then
          for j = 1, #ring do
            local c = ring[j]
            if W.geo.ocean[c] == 0 and F.grazers[c] > bg then best, bg = c, F.grazers[c] end
          end
        end
        v.hunt_cell = best
        if best and bg > 0.01 then
          local meat = eff * 1.6 * bg / (bg + 0.15)
          local d = meat / 16000
          if d > bg * 0.2 then d = bg * 0.2; meat = d * 16000 end
          F.grazers[best] = bg - d
          local vt = v.cause
          if vt ~= 0 or C.grazers[best] ~= 0 then
            blame.step(C.grazers, I.grazers, best, -d, FGRAZ.eps, FGRAZ.hold, -d, vt, 0, 0, 0, 0, 0, 0)
          end
          v.stock.fresh = v.stock.fresh + meat
          v.day_fresh = (v.day_fresh or 0) + meat
          acc(v, "meat", meat)
        end
      end
    end
  end
  return short_area
end

return M
