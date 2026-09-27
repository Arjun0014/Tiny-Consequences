-- Farming (docs/world/11_ECONOMY.md §3). Fields are sim cells a village owns (land_use = id),
-- with sparse state in W.S.farm[cell]:
--   { v = village id, plan = "crop" | "fallow" | "clear", sown (0-1 of the cell), bio (crop
--     biomass per sown area, 0-1), harvested (0-1 of the sown area), weeds (0-1), gdd, ripe,
--     tended (hours of weeding done this week), clear (hours of clearing still needed) }
-- The planet field crop[cell] = sown x bio x (1 - harvested), so fire, raids, floods, blight and
-- oil act on it; farming syncs bio back from it before it acts.
--
-- The calendar comes from climate, per village (its centre cell's weekly temperature): sowing
-- opens in the local spring once it is warm enough, lasts sow_weeks, crops grow by degree days,
-- ripen, and must then be harvested before they rot. Every step costs labour hours; seed comes
-- from the store. Nothing here produces food without people working.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")
local sites = require("sim.society.sites")
local ledger = require("sim.ledger")

local M = {}

-- add x to the village's yearly flow account under key k
local function acc(v, k, x)
  local a = v.acc
  if not a then a = {}; v.acc = a end
  a[k] = (a[k] or 0) + x
end
local floor, ceil, min, max = math.floor, math.ceil, math.min, math.max
local FCROP = fields.BY_NAME.crop

-- local week of year: the southern hemisphere's calendar runs half a year behind the labels
function M.local_week(W, v)
  local wk = floor(W.hour / 168)
  if W.grid.lat[v.cell] < 0 then wk = wk + 24 end
  return wk % 48, floor(wk / 48)
end

-- local week of year at a cell (for a village not founded yet)
function M.local_week_at(W, cell)
  local wk = floor(W.hour / 168)
  if W.grid.lat[cell] < 0 then wk = wk + 24 end
  return wk % 48
end

local function field(W, c) return W.S.farm[c] end

local function sync_bio(F, f, c)
  local standing = f.sown * (1 - f.harvested)
  if standing > 1e-6 then
    local b = F.crop[c] / standing
    if b < f.bio then f.bio = b end
  end
end

local function write_crop(F, f, c)
  F.crop[c] = f.sown * f.bio * (1 - f.harvested)
end

-- Claim / release ------------------------------------------------------------------------------

function M.claim(W, v, c, plan)
  local F = W.F
  F.land_use[c] = v.id
  W.S.farm[c] = { v = v.id, plan = plan or "crop", sown = 0, bio = 0, harvested = 0, weeds = 0,
    gdd = 0, ripe = false, tended = 0, clear = 0 }
  local farms = v.farms
  farms[#farms + 1] = c
  return W.S.farm[c]
end

function M.release(W, v, c)
  local F = W.F
  if F.land_use[c] == v.id then F.land_use[c] = 0 end
  F.crop[c] = 0
  W.S.farm[c] = nil
  local farms = v.farms
  for i = #farms, 1, -1 do if farms[i] == c then table.remove(farms, i) end end
end

function M.release_all(W, v)
  for i = #v.farms, 1, -1 do M.release(W, v, v.farms[i]) end
end

local function farm_score(W, v, c)
  local F = W.F
  local d = sites.dist(W, v.cell, c)
  local fl = W.D.flood_last
  local flooded = (fl and W.tick - fl[c] < 96) and 0.6 or 0
  -- open grassland first; nearer is better (walking time is lost working time)
  return F.fertility[c] + 0.5 * min(F.soil_water[c], 0.6) + 0.2 * F.grass[c] - 0.8 * F.forest[c]
    - 0.12 * d + 0.3 * sites.growing_warmth(W, c) - flooded
end

-- Field plan (weekly, outside the growing season): as many cropped fields as the village needs
-- with a margin, bounded by the labour it can put into sowing and harvest.
function M.plan(W, v, workers)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local lu, fert, soil, ice = F.land_use, F.fertility, F.soil_water, F.ice
  local need_year = v.need_day * 336
  local fresh_part = v.norm.fresh_in * 48        -- what the boats and hunters bring in a year (weekly normal)
  -- fish and meat count for half (the sea is less reliable than stored grain); fields are planned
  -- with what this village's fields really gave (learned from its harvests)
  local per_field = v.yield_field or cfg.yield * 0.5
  if per_field < cfg.yield * 0.2 then per_field = cfg.yield * 0.2 end
  -- the grain the village must grow: its need less the fish and meat it usually brings in (counted
  -- at 80%: the sea is less reliable than a granary), with a margin for bad years
  local grain_need = need_year - fresh_part * 0.8
  if grain_need < need_year * 0.25 then grain_need = need_year * 0.25 end
  local want = ceil(grain_need * cfg.field_margin / per_field - 0.05)
  -- Chayanov: with more than a year and a half of grain in store, nobody breaks their back
  -- for more; fields go fallow until the stores run down
  local grain_years = v.stock.grain / max(1, need_year)
  if grain_years > 1.5 then want = floor(want * math.max(0.4, 1 - (grain_years - 1.5) * 0.4)) end
  local by_labour = floor(workers * cfg.fields_per_worker + 0.5)
  if want > by_labour then want = by_labour end
  if workers > 0 and want < 1 then want = 1 end
  if want < 0 then want = 0 end
  -- drop fields that are no longer usable
  for i = #v.farms, 1, -1 do
    local c = v.farms[i]
    if lu[c] ~= v.id or ice[c] >= 0.5 then M.release(W, v, c) end
  end
  -- fallow: an exhausted field rests (if the village can afford it)
  local rich = v.food_days >= cfg.fallow_food_months * 28
  local cropped = 0
  for i = 1, #v.farms do
    local f = field(W, v.farms[i])
    if f.plan ~= "clear" then
      if fert[v.farms[i]] < cfg.fallow_fert and rich then f.plan = "fallow" else f.plan = "crop" end
      if f.plan == "crop" then cropped = cropped + 1 end
    else
      cropped = cropped + 1
    end
  end
  -- too many: release the worst (fallow first), one field a week and only when clearly too many
  -- (fields are an investment: a bad year doesn't make a village give up its land)
  if cropped > want * 1.5 + 1 and #v.farms > 0 then
    local wi, ws = 0, math.huge
    for i = 1, #v.farms do
      local c = v.farms[i]
      local s = farm_score(W, v, c) + (field(W, c).plan == "fallow" and -5 or 0)
      if s < ws then wi, ws = i, s end
    end
    local c = v.farms[wi]
    if field(W, c).plan ~= "fallow" then cropped = cropped - 1 end
    M.release(W, v, c)
  end
  -- too few: claim the best free cells within reach
  local G = W.vgeo[v.id]
  local ring = G and G.ring or sites.rings(W, v.cell, sites.radius(W, cfg.farm_radius))
  local min_fert = rich and cfg.fallow_fert or cfg.poor_fert
  local guard = 0
  while cropped < want and guard < 20 do
    guard = guard + 1
    local best, bs = nil, -math.huge
    for i = 1, #ring do
      local c = ring[i]
      if lu[c] == 0 and W.geo.ocean[c] == 0 and ice[c] < 0.5 and soil[c] > 0.08 and fert[c] >= min_fert then
        local s = farm_score(W, v, c)
        if s > bs then best, bs = c, s end
      end
    end
    if not best then break end
    local f = M.claim(W, v, best, "crop")
    if F.forest[best] > 0.2 then
      f.plan = "clear"
      f.clear = cfg.clear_h * F.forest[best]
    end
    cropped = cropped + 1
  end
end

-- Season machine and weekly crop growth (runs with the planet week, after temperatures).
function M.weekly(W, v)
  local cfg = cfgmod.SOCIETY
  local F, C, I = W.F, W.C, W.I
  local S = v.season
  local lwk, lyear = M.local_week(W, v)
  local T = F.temp[v.cell]
  -- phase changes
  if S.phase == "winter" then
    -- the local spring (or, in the tropics, whenever it is warm: no winter to wait out)
    local tropics = math.abs(W.grid.lat[v.cell]) < cfg.tropics_lat
    if S.year ~= lyear and (lwk < 22 or tropics) and T >= cfg.sow_temp and F.ice[v.cell] < 0.3 then
      S.phase, S.year, S.week = "sowing", lyear, lwk
      for i = 1, #v.farms do
        local f = field(W, v.farms[i])
        f.sown, f.bio, f.harvested, f.gdd, f.ripe, f.weeds, f.weeks = 0, 0, 0, 0, false, 0, 0
      end
      v.replan = true
    end
  elseif S.phase == "sowing" then
    local all = true
    for i = 1, #v.farms do
      local f = field(W, v.farms[i])
      if f.plan == "crop" and f.sown < 0.999 then all = false end
    end
    if all or ((lwk - S.week) % 48) >= cfg.sow_weeks then
      S.phase = "growing"
      v.replan = true
      M.sowing_done(W, v)
    end
  end
  -- growth (sown fields, until harvested)
  local ccrop, icrop = C.crop, I.crop
  local lo, hi = cfg.crop_t[1], cfg.crop_t[2]
  local mid, half = (lo + hi) * 0.5, (hi - lo) * 0.5
  local sh = cfg.crop_soil_half
  local blight = W.S.blight
  local flood_last = W.D.flood_last
  local any_ripe, any_standing = false, false
  for i = 1, #v.farms do
    local c = v.farms[i]
    local f = field(W, c)
    if f.sown > 0 and f.harvested < 0.999 then
      sync_bio(F, f, c)
      local c0 = f.bio
      local t = F.temp[c]
      local x = (t - mid) / half
      local tf = 1 - x * x
      if tf < 0 then tf = 0 end
      local s = F.soil_water[c]
      local wf = (s / (s + sh)) * (1 + sh)
      if wf > 1 then wf = 1 end
      local grow = 0
      if not f.ripe then grow = cfg.r_crop * tf * wf * (0.3 + 0.7 * F.fertility[c]) * c0 * (1 - c0) end
      local dry = s < cfg.drought_soil and cfg.k_drought * c0 or 0
      local frost = t < cfg.frost_temp and cfg.k_frost * c0 or 0
      local oilx = 0
      local o = F.oil[c]
      if o > 0 then oilx = cfg.k_oil_crop * o * c0 end
      local to = C.oil[c]
      local bt = blight and blight[c]
      if bt then oilx = oilx + cfg.k_blight * c0; to = bt end
      if flood_last and W.tick - flood_last[c] <= 1 then oilx = oilx + cfg.k_flood_crop * c0; to = C.flow[c] end
      local c1 = c0 + grow - dry - frost - oilx
      if c1 < 0 then c1 = 0 elseif c1 > 1 then c1 = 1 end
      f.bio = c1
      local before = F.crop[c]
      write_crop(F, f, c)
      local ts, tt = C.soil_water[c], C.temp[c]
      if ccrop[c] ~= 0 or ts ~= 0 or tt ~= 0 or to ~= 0 then
        local k = f.sown * (1 - f.harvested)
        blame.step(ccrop, icrop, c, F.crop[c] - before, FCROP.eps, FCROP.hold, grow * k, 0, -dry * k, ts,
          -frost * k, tt, -oilx * k, to)
      end
      -- degree days and ripeness
      local gd = (t - cfg.gdd_base) * 7
      if gd > 0 then f.gdd = f.gdd + gd end
      f.weeks = (f.weeks or 0) + 1
      if not f.ripe and ((f.gdd >= cfg.gdd_ripe and (c1 >= 0.85 or f.weeks >= 16)) or (lwk >= 34 and c1 > 0.3)
        or lwk >= 40 or f.weeks >= 30) then f.ripe = true end
      if f.ripe then any_ripe = true end
      any_standing = true
      -- weeds grow where tending fell short this week (growing season)
      if S.phase == "growing" and not f.ripe then
        local need = cfg.tend_h * f.sown / 14
        local share = need > 0 and min(1, f.tended / need) or 1
        f.weeds = min(1, f.weeds + cfg.weed_rate * (1 - share))
      end
      f.tended = 0
    end
  end
  if (S.phase == "growing" or S.phase == "sowing") and any_ripe then S.phase = "harvest"; v.replan = true end
  if S.phase == "growing" and not any_standing then
    -- nothing was sown (or all of it died): the season is over
    M.end_season(W, v)
  end
  if S.phase == "harvest" and not any_standing then M.end_season(W, v) end
  S.t_prev = T
end

-- The sowing window closed: how much of the planned cropland stayed unsown, and why — missing
-- seed (the grain stock's cause) or missing hands (the labour tag). A window closing with >= 30%
-- undone for lack of hands is a labour shortage (16 §3).
function M.sowing_done(W, v)
  local planned, sown = 0, 0
  for i = 1, #v.farms do
    local f = field(W, v.farms[i])
    if f.plan == "crop" then planned = planned + 1; sown = sown + f.sown end
  end
  local short = planned > 0 and (planned - sown) / planned or 0
  local seed = planned > 0 and min(short, (v.seed_short_fields or 0) / planned) or 0
  v.seed_short_fields = 0
  v.seed_short = seed
  v.area_short = short - seed
  if v.area_short >= 0.3 and planned >= 2 and W.record_events then
    local parent = v.labour_tag ~= 0 and v.labour_tag or v.cause
    local id = require("sim.causality.events").emit(W, "labour_shortage", v.cell, parent,
      floor(v.area_short * 100 + 0.5), { village = v.id, unsown = planned - sown })
    if id ~= 0 and v.labour_tag ~= 0 then v.labour_tag = id end
  end
end

-- The harvest season is over: record it (crop_failure detection) and reset the fields.
function M.end_season(W, v)
  local S = v.season
  S.phase = "winter"
  S.harvest_week = M.local_week(W, v)
  v.replan = true
  v.last_harvest = v.harvest_acc
  -- learn what a sown field gives here (a fire- or flood-ruined year teaches less)
  local sown_total = 0
  for i = 1, #v.farms do sown_total = sown_total + field(W, v.farms[i]).sown end
  if sown_total > 0.5 then
    local y = v.harvest_acc / sown_total
    v.yield_field = v.yield_field and (0.6 * v.yield_field + 0.4 * y) or y
  end
  v.harvest_acc = 0
  v.harvest_new = true
  -- the harvest's cause: the most frequent crop tag among the fields, or the labour tag when
  -- sown/harvested area fell short
  local C = W.C
  local best, bc = 0, 0
  local farms = v.farms
  for i = 1, #farms do
    local t = C.crop[farms[i]]
    if t ~= 0 then
      local n = 0
      for j = 1, #farms do if C.crop[farms[j]] == t then n = n + 1 end end
      if n > bc or (n == bc and t < best) then best, bc = t, n end
    end
  end
  -- ripe grain lost in the fields while waiting for hands
  local lost = v.harvest_lost or 0
  local lost_share = lost / max(1, lost + v.harvest_acc)
  v.harvest_lost = 0
  if ((v.area_short or 0) > 0.3 or lost_share > 0.3) and v.labour_tag ~= 0 then best = v.labour_tag end
  if (v.seed_short or 0) > 0.3 and v.foodtag[1] ~= 0 then best = v.foodtag[1] end
  v.harvest_tag = best
  v.area_short, v.seed_short = 0, 0
  local F = W.F
  for i = 1, #farms do
    local c = farms[i]
    local f = field(W, c)
    if f.plan ~= "clear" then
      f.sown, f.bio, f.harvested, f.ripe, f.gdd = 0, 0, 0, false, 0
      F.crop[c] = 0
    end
  end
end

-- Labour demand this week (person-hours), by kind. out = { sow, tend, harvest, clear }.
function M.demands(W, v, out)
  local cfg = cfgmod.SOCIETY
  local S = v.season
  out.sow, out.tend, out.harvest, out.clear = 0, 0, 0, 0
  local seed_fields = (v.stock.seed + max(0, v.stock.grain - v.need_day * 14)) / cfg.seed
  for i = 1, #v.farms do
    local f = field(W, v.farms[i])
    if f.plan == "clear" then
      out.clear = out.clear + f.clear
    elseif S.phase == "sowing" and f.plan == "crop" and f.sown < 1 then
      local s = min(1 - f.sown, max(0, seed_fields))
      seed_fields = seed_fields - s
      out.sow = out.sow + s * cfg.sow_h
    end
    if f.sown > 0 and f.harvested < 1 then
      if f.ripe then
        out.harvest = out.harvest + (1 - f.harvested) * f.sown * cfg.harvest_h
      elseif S.phase == "growing" or S.phase == "sowing" then
        out.tend = out.tend + cfg.tend_h * f.sown / 14
      end
    end
  end
  return out
end

-- Hours of `kind` work field f still needs this week (0 if none).
local function field_need(cfg, S, f, kind)
  if kind == "sow" then
    if S.phase == "sowing" and f.plan == "crop" and f.sown < 1 then return (1 - f.sown) * cfg.sow_h end
  elseif kind == "tend" then
    if f.sown > 0 and not f.ripe and f.harvested < 1 then return max(0, cfg.tend_h * f.sown / 14 - f.tended) end
  elseif kind == "harvest" then
    if f.ripe and f.sown > 0 and f.harvested < 1 then return (1 - f.harvested) * f.sown * cfg.harvest_h end
  elseif kind == "clear" then
    if f.plan == "clear" then return f.clear end
  end
  return 0
end
M.field_need = field_need

-- Which field each worker of a `kind` group goes to: the fields in the order work() spends hours
-- on them (v.farms order), each filled with workers until their week's capacity covers its need.
-- people[1..n] = persons, caps[1..n] = their capacity (0-1), week_h = working hours this week.
function M.field_sites(W, v, kind, people, caps, n, week_h)
  local cfg = cfgmod.SOCIETY
  local S = v.season
  local farms = v.farms
  local fi, left = 0, 0
  local last = farms[1] or v.cell
  for i = 1, n do
    while left <= 0 and fi < #farms do
      fi = fi + 1
      left = field_need(cfg, S, field(W, farms[fi]), kind)
      if left > 0 then last = farms[fi] end
    end
    people[i].site = last
    left = left - caps[i] * week_h
  end
end

-- Apply `hours` of work of `kind` on the village's fields (spread over the fields that need it,
-- nearest first). Returns hours used. Harvests add grain to the store.
function M.work(W, v, kind, hours)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local left = hours
  for i = 1, #v.farms do
    if left <= 1e-6 then break end
    local c = v.farms[i]
    local f = field(W, c)
    if kind == "sow" and f.plan == "crop" and v.season.phase == "sowing" and f.sown < 1 then
      local want = (1 - f.sown) * cfg.sow_h
      local h = min(left, want)
      local ds = h / cfg.sow_h
      -- seed: the reserve first, then the store
      local seed_need = ds * cfg.seed
      local from_seed = min(v.stock.seed, seed_need)
      v.stock.seed = v.stock.seed - from_seed
      local rest = seed_need - from_seed
      local avail = max(0, v.stock.grain)
      if rest > avail then
        -- not enough seed: sow what the grain allows
        local ok = (from_seed + avail) / cfg.seed
        if ok < ds then
          v.seed_short_fields = (v.seed_short_fields or 0) + (ds - ok)
          ds = ok
          h = ds * cfg.sow_h
        end
        rest = avail
      end
      v.stock.grain = v.stock.grain - rest
      if ds > 0 then
        sync_bio(F, f, c)
        local s0 = f.sown
        f.bio = (f.bio * s0 + cfg.crop_seed * ds) / (s0 + ds)
        f.sown = s0 + ds
        write_crop(F, f, c)
        acc(v, "sown", ds)
      end
      left = left - h
    elseif kind == "tend" and f.sown > 0 and not f.ripe and f.harvested < 1 then
      local want = cfg.tend_h * f.sown / 14 - f.tended
      if want > 0 then
        local h = min(left, want)
        f.tended = f.tended + h
        left = left - h
      end
    elseif kind == "harvest" and f.ripe and f.sown > 0 and f.harvested < 1 then
      sync_bio(F, f, c)
      local want = (1 - f.harvested) * f.sown * cfg.harvest_h
      local h = min(left, want)
      local dh = h / (f.sown * cfg.harvest_h)
      local grain = dh * f.sown * f.bio * cfg.yield * (1 - 0.5 * f.weeds)
      f.harvested = min(1, f.harvested + dh)
      -- fertility: the crop takes nutrients with it
      local fe = F.fertility[c] - cfg.deplete * dh * f.bio * 4
      if fe < 0 then fe = 0 end
      F.fertility[c] = fe
      write_crop(F, f, c)
      v.stock.grain = v.stock.grain + grain
      v.harvest_acc = v.harvest_acc + grain
      acc(v, "harvest", grain)
      v.day_grain = (v.day_grain or 0) + grain
      left = left - h
    elseif kind == "clear" and f.plan == "clear" then
      local h = min(left, f.clear)
      f.clear = f.clear - h
      left = left - h
      if f.clear <= 1e-6 then
        -- cleared: the timber goes to the store, the rest is burned (CO2)
        local fo = F.forest[c]
        local wood = fo * cfg.forest_wu * 0.5
        v.stock.wood = v.stock.wood + wood
        local co2 = fo * 0.5 * cfgmod.VEG.k_co2
        W.G.co2_src = (W.G.co2_src or 0) + co2
        ledger.add(W, "co2", co2, 0, "villagers")
        F.forest[c] = 0
        W.P.forest[c] = 0
        blame.set(W.C.forest, W.I.forest, c, v.cause)
        f.plan = "crop"
        f.clear = 0
      end
    end
  end
  return hours - left
end

-- Daily: ripe crops left standing are lost (more on wet days).
function M.daily(W, v, wet)
  local cfg = cfgmod.SOCIETY
  if v.season.phase ~= "harvest" then return end
  local F = W.F
  local loss = wet and cfg.ripe_loss_wet or cfg.ripe_loss
  for i = 1, #v.farms do
    local c = v.farms[i]
    local f = field(W, c)
    if f.ripe and f.sown > 0 and f.harvested < 1 then
      sync_bio(F, f, c)
      v.harvest_lost = (v.harvest_lost or 0) + f.bio * loss * f.sown * (1 - f.harvested) * cfg.yield
      f.bio = f.bio * (1 - loss)
      write_crop(F, f, c)
      if f.bio < 0.02 then f.harvested = 1; write_crop(F, f, c) end
    end
  end
end

-- The people the village's reachable land (fields within farm_radius, cropped with fallow) and
-- waters (a sustainable share of the fish) could feed: the "livelihood" couples need to marry.
function M.capacity(W, v)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local G = W.vgeo[v.id]
  local ring = G and G.ring or sites.rings(W, v.cell, sites.radius(W, cfg.farm_radius))
  local per_field = v.yield_field or cfg.yield * 0.5
  local fields = 0
  for i = 1, #ring do
    local c = ring[i]
    local lu = F.land_use[c]
    if W.geo.ocean[c] == 0 and (lu == 0 or lu == v.id) and F.ice[c] < 0.5 and F.soil_water[c] > 0.08
      and F.fertility[c] >= cfg.poor_fert then
      fields = fields + 1
    end
  end
  local food = fields * 0.8 * per_field
  for i = 1, #v.fishing do food = food + F.fish[v.fishing[i]] * cfg.fish_fu * 0.08 end
  local cap = food / (336 * 0.85)
  v.capacity = cap
  v.land_fields = fields
  return cap
end

-- Days until the next harvest is expected (for rationing).
function M.days_to_harvest(W, v)
  local cfg = cfgmod.SOCIETY
  local S = v.season
  local lwk = M.local_week(W, v)
  if S.phase == "harvest" then return 7 end
  if not v.farms[1] and v.season.harvest_week then return 336 end
  if S.phase == "growing" or S.phase == "sowing" then
    -- the crop's progress toward ripeness at this summer's pace
    local g, n = 0, 0
    for i = 1, #v.farms do
      local f = field(W, v.farms[i])
      if f.sown > 0 then g = g + f.gdd; n = n + 1 end
    end
    local rate = max(20, (W.F.temp[v.cell] - cfg.gdd_base) * 7)
    local left = n > 0 and max(0, cfg.gdd_ripe - g / n) or cfg.gdd_ripe
    return min(200, left / rate * 7 + 14)
  end
  if not S.harvest_week then
    -- no harvest here yet (a new village): the local spring's sowing plus a growing season
    local T = W.F.temp[v.cell]
    local wait = T >= cfg.sow_temp and 0 or ((6 - lwk) % 48)
    return min(336, (wait + cfg.first_harvest_weeks) * 7)
  end
  local d = ((S.harvest_week - lwk) % 48) * 7
  if d < 14 then d = d + 336 end
  return d
end

return M
