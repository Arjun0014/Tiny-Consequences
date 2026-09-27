-- Village stocks, consumption and coping (docs/world/11_ECONOMY.md §2, §7; 16 §2–3).
-- Daily at the village day step: eat (fresh first, then grain; the seed reserve only when food is
-- nearly gone — the irreversible coping step), ration to last until the next harvest, burn fuel
-- (cold without it), spoil. Weekly: the food and wood stocks' cause tags (dominant driver of the
-- decline: a poor harvest, lost boats or ships, missing workers, more mouths), and the society
-- detectors: rationing, seed eaten, famine, wood shortage, crop failure, deaths spike.
local cfgmod = require("sim.config")
local blame = require("sim.causality.blame")
local events = require("sim.causality.events")
local ledger = require("sim.ledger")
local ents = require("sim.society.entities")
local cotag = require("sim.society.cotag")
local farming = require("sim.society.farming")
local labour = require("sim.society.labour")
local construction = require("sim.society.construction")

local M = {}

-- add x to the village's yearly flow account under key k
local function acc(v, k, x)
  local a = v.acc
  if not a then a = {}; v.acc = a end
  a[k] = (a[k] or 0) + x
end
local floor, min, max = math.floor, math.min, math.max
M.GRAIN, M.FRESH, M.WOOD = 1, 2, 3

local function emit(W, kind, cell, parent, mag, data, opts)
  if not W.record_events then return 0 end
  local v = data and data.village and W.ent.villages[data.village]
  local co = v and cotag.co_of(W, v)
  if co then
    opts = opts or {}
    opts.co = co
  end
  return events.emit(W, kind, cell, parent, mag, data, opts)
end
M.emit = emit

function M.need_day(W, v)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local hour = W.hour
  local need = 0
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.away == 0 then
      local a = (hour - p.born) / 8064
      if a < cfg.adult_age then need = need + cfg.eat.child
      elseif a >= cfg.elder_age then need = need + cfg.eat.elder
      else need = need + cfg.eat.adult end
    end
  end
  v.need_day = need
  return need
end

-- Daily consumption, fuel, spoilage and indicators. wx = today's weather at the village.
function M.daily(W, v, wx)
  local cfg = cfgmod.SOCIETY
  local st = v.stock
  local need = v.need_day
  if v.need_pop ~= v.pop or need <= 0 then need = M.need_day(W, v); v.need_pop = v.pop end
  -- rationing: make the food last to the next harvest (+ a margin)
  local food = st.fresh + st.grain
  local dth = farming.days_to_harvest(W, v)
  -- will the store last to the next harvest? What the boats, hunters and foragers usually bring in
  -- counts (discounted: the sea is fickle); a buffer of ration_margin_days of the store's share of
  -- the diet is kept back. Short of that, everyone eats less so the store lasts (rationing).
  local income = v.norm.fresh_in / 7 * cfg.income_trust
  if income > need then income = need end
  local from_store = need - income
  local buffer = need * cfg.ration_safety_days + from_store * min(dth, cfg.ration_margin_days) * 0.5
  local r = 1
  if need > 0 then
    local avail = food + income * dth - buffer
    local want = need * dth
    if avail < want then
      r = avail / want
      if r < cfg.ration_min then r = cfg.ration_min end
    end
  end
  local eat = need * r
  if eat > food and st.seed > 0 then
    -- food is gone: the seed grain is eaten (fewer fields next spring)
    local take = min(st.seed, eat - food + need * cfg.seed_eat_days)
    st.seed = st.seed - take
    st.grain = st.grain + take
    food = st.fresh + st.grain
    local lwk, lyear = farming.local_week(W, v)
    if v.seed_eaten_year ~= lyear then
      v.seed_eaten_year = lyear
      local id = emit(W, "seed_eaten", v.cell, v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause, floor(take), { village = v.id })
      if id ~= 0 then v.cause = id; v.foodtag[1] = id; v.foodidle[1] = 0 end
    end
  end
  if eat > food then eat = food end
  if need > 0 then r = eat / need else r = 1 end
  local from_fresh = min(st.fresh, eat)
  st.fresh = st.fresh - from_fresh
  st.grain = st.grain - (eat - from_fresh)
  v.ration = r
  v.ration_sum = v.ration_sum + r
  v.ration_n = v.ration_n + 1
  v.wk_eaten = (v.wk_eaten or 0) + eat
  acc(v, "eaten", eat)
  acc(v, "need", need)
  -- rationing and its event
  if r < 0.95 then v.low_days = v.low_days + 1 else v.low_days = 0 end
  if not v.rationing and v.low_days >= cfg.rationing_days then
    v.rationing = true
    local id = emit(W, "rationing", v.cell, v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause, floor((1 - r) * 100),
      { village = v.id, ration = r })
    if id ~= 0 then v.cause = id; v.foodtag[1] = id; v.foodidle[1] = 0 end
  elseif v.rationing and r >= 0.999 and v.low_days == 0 then
    v.rationing = false
  end
  -- fuel
  local T = wx.temp
  local fuel = labour.fuel_day(W, v, T)
  local burned = fuel
  if fuel > st.wood then
    burned = max(0, st.wood)
    if T < cfg.cold_temp then v.cold_days = (v.cold_days or 0) + 1 end
  end
  st.wood = st.wood - burned
  v.wk_wood_out = (v.wk_wood_out or 0) + burned
  acc(v, "burned", burned)
  if burned > 0 then
    local co2 = burned / cfg.forest_wu * cfgmod.VEG.k_co2
    W.G.co2_src = (W.G.co2_src or 0) + co2
    ledger.add(W, "co2", co2, 0, "villagers")
  end
  v.fuel_day = fuel
  -- spoilage and storage
  acc(v, "spoil_f", st.fresh * cfg.fresh_spoil)
  st.fresh = st.fresh * (1 - cfg.fresh_spoil)
  local gcap = v.grain_cap or 1e9
  local g = st.grain * (1 - cfg.grain_spoil)
  if g > gcap then g = g - (g - gcap) * cfg.overflow_spoil end
  acc(v, "spoil_g", st.grain - g)
  st.grain = g
  local wcap = v.wood_cap or 1e9
  if st.wood > wcap then st.wood = st.wood - (st.wood - wcap) * cfg.wood_rot end
  -- indicators
  local fd = need > 0 and (st.fresh + st.grain) / need or 999
  v.food_days = fd
  v.food_security = fd / 28
  v.wood_days = fuel > 0 and st.wood / fuel or 999
end

-- Weekly: stock tags, famine, wood shortage, crop failure, deaths spike, labour tag.
function M.weekly(W, v)
  local cfg = cfgmod.SOCIETY
  local st = v.stock
  local N = v.norm
  local k = 1 / 96   -- ~2-year normals of weekly flows
  local g_in = v.wk_grain or 0
  local f_in = v.wk_fresh or 0
  local out = v.wk_eaten or 0
  local imp = v.wk_imports or 0
  if (N.n or 0) < 10 then
    N.grain_in = N.grain_in + (g_in - N.grain_in) * 0.3
    N.fresh_in = N.fresh_in + (f_in - N.fresh_in) * 0.3
    N.food_out = N.food_out + (out - N.food_out) * 0.3
    N.imports = N.imports + (imp - N.imports) * 0.3
    N.n = (N.n or 0) + 1
  end
  local ag = g_in - N.grain_in
  local af = f_in - N.fresh_in
  local ac = -(out - N.food_out)
  local ai = imp - N.imports
  N.grain_in = N.grain_in + (g_in - N.grain_in) * k
  N.fresh_in = N.fresh_in + (f_in - N.fresh_in) * k
  N.food_out = N.food_out + (out - N.food_out) * k
  N.imports = N.imports + (imp - N.imports) * k
  N.catch_in = (N.catch_in or f_in) + ((v.wk_catch or 0) - (N.catch_in or f_in)) * k
  v.wk_grain, v.wk_fresh, v.wk_eaten, v.wk_imports, v.wk_catch = 0, 0, 0, 0, 0
  -- food tag: the dominant driver of the food stock's deviation (poor harvest, fish or boats
  -- lost, imports stopped, more mouths)
  local tg = v.harvest_bad and v.harvest_tag or 0
  local tf = v.fresh_tag or 0
  local ti = v.import_tag or 0
  local tc = v.arrivals_tag or 0
  local delta = ag + af + ac + ai
  if v.foodtag[1] ~= 0 or tg ~= 0 or tf ~= 0 or ti ~= 0 or tc ~= 0 then
    blame.step(v.foodtag, v.foodidle, 1, delta, v.need_day * 0.5 + 1, 26, ag, tg, af, tf, ai, ti, ac, tc)
  end
  -- wood tag: when wood runs short, why (logged-out or burned woods, or missing woodcutters)
  if v.wood_days < 14 then
    local t = 0
    local woods = W.vgeo[v.id] and W.vgeo[v.id].woods
    if woods then
      for i = 1, #woods do
        local c = woods[i]
        local ct = W.C.forest[c]
        if ct ~= 0 and ct ~= v.cause then t = ct; break end
      end
    end
    if t == 0 then t = v.labour_tag end
    v.foodtag[3] = t
  elseif v.wood_days > 45 then
    v.foodtag[3] = 0
  end
  -- the village's cause: its binding shortage
  if v.food_days < 60 and v.foodtag[1] ~= 0 then v.cause = v.foodtag[1]
  elseif v.wood_days < 7 and v.foodtag[3] ~= 0 then v.cause = v.foodtag[3]
  elseif v.food_days > 150 and not v.famine then v.cause = 0 end
  -- wood shortage event (cold season, once a year)
  local lwk, lyear = farming.local_week(W, v)
  local bites = (v.month_cold or 0) > 0 or (v.site_stalled and W.hour - v.site_stalled < 168)
  if v.wood_days < 3 and bites and v.wood_short_year ~= lyear then
    v.wood_short_year = lyear
    local id = emit(W, "wood_shortage", v.cell, v.foodtag[3], floor(v.wood_days * 10), { village = v.id })
    if id ~= 0 then v.foodtag[3] = id end
  end
  -- famine: deep rationing for weeks
  local rw = v.ration_n > 0 and v.ration_sum / v.ration_n or v.ration
  if rw < cfg.famine_ration then v.famine_weeks = v.famine_weeks + 1 else v.famine_weeks = 0 end
  if not v.famine and v.famine_weeks >= cfg.famine_weeks and W.hour - v.famine_last >= 24 * 168 then
    v.famine = true
    v.famine_last = W.hour
    local id = emit(W, "famine", v.cell, v.cause, v.pop, { village = v.id })
    if id ~= 0 then v.cause = id; v.foodtag[1] = id; v.foodidle[1] = 0 end
  elseif v.famine and rw >= 0.999 and v.food_days > 90 then
    v.famine = false
  end
  -- crop failure at the end of a harvest season
  if v.harvest_new then
    v.harvest_new = false
    local h = v.harvests
    v.harvest_bad = false
    if #h >= 2 then
      local s = 0
      for i = 1, #h do s = s + h[i] end
      local avg = s / #h
      if avg > 0 and v.last_harvest < 0.75 * avg then v.harvest_bad = true end
      if avg > 0 and v.last_harvest < cfgmod.DETECT.crop_failure * avg then
        local pct = floor(100 * (1 - v.last_harvest / avg) + 0.5)
        local id = emit(W, "crop_failure", v.cell, v.harvest_tag, pct, { village = v.id, harvest = v.last_harvest, avg = avg })
        if id ~= 0 then
          v.cause = id
          v.harvest_tag = id
          local C, I = W.C.crop, W.I.crop
          for i = 1, #v.farms do
            local c = v.farms[i]
            if C[c] == v.harvest_tag then C[c] = id; I[c] = 0 end
          end
        end
      end
    end
    h[#h + 1] = v.last_harvest
    if #h > 5 then table.remove(h, 1) end
    -- set aside seed for next spring's fields
    local fields_next = 0
    for i = 1, #v.farms do if W.S.farm[v.farms[i]].plan ~= "fallow" then fields_next = fields_next + 1 end end
    local want = fields_next * cfg.seed - st.seed
    if want > 0 then
      local take = min(want, max(0, st.grain - v.need_day * 30))
      st.grain = st.grain - take
      st.seed = st.seed + take
    end
  end
  -- labour tag: why the workforce is below its normal
  if v.workforce_norm and v.workforce_norm > 0 and (v.workforce or 0) < 0.85 * v.workforce_norm then
    if v.labour_candidate and v.labour_candidate ~= 0 then v.labour_tag = v.labour_candidate end
  elseif (v.workforce or 0) >= 0.95 * (v.workforce_norm or 0) then
    v.labour_tag = 0
  end
  cotag.note_tags(W, v)
  -- storage capacities (for spoilage)
  v.grain_cap, v.wood_cap = construction.capacities(W, v)
end

-- Monthly: deaths spike (>= 3 non-age deaths in a month).
function M.monthly(W, v)
  local n = v.nonage or 0
  if n >= cfgmod.DETECT.deaths_spike then
    local parent = v.death_tag ~= 0 and v.death_tag or v.cause
    local id = emit(W, "deaths_spike", v.cell, parent, n, { village = v.id })
    if id ~= 0 then v.labour_candidate = id end
  end
  v.nonage = 0
  v.deaths = 0
  v.death_tag = 0
end

return M
