-- The settlement (docs/v3/10_AGENTS.md §2, §9): buildings, plots, stores, the hourly planner
-- (weather, the open work people can see: the board's summary), the daily round (crops, trees,
-- lanes, fuel, water) and growth (a couple without a home makes a house site).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local mapm = require("sim.agents.map")
local clock = require("sim.agents.clock")
local weather = require("sim.agents.weather")
local people = require("sim.agents.people")
local events = require("sim.causality.events")

local M = {}
local floor, max, min, sqrt, abs, ceil = math.floor, math.max, math.min, math.sqrt, math.abs, math.ceil
local function C() return cfgmod.V3 end

M.KINDS = {
  house = { w = 4, h = 4, label = "house", logs = 18, work = 110 },
  granary = { w = 4, h = 4, label = "granary", logs = 22, work = 140 },
  woodstore = { w = 4, h = 3, label = "woodstore", logs = 8, work = 40 },
  well = { w = 1, h = 1, label = "well", logs = 0, work = 30 },
  dock = { w = 1, h = 1, label = "dock", logs = 6, work = 30 },
  boat = { w = 2, h = 1, label = "boat", logs = 5, work = 30 },   -- a boat on the stocks at the shore (D-087)
}

function M.new(W, cell, opts)
  local V = W.S.v3
  local id = #V.settlements + 1
  local s = {
    id = id, cell = cell, name = opts.name or "?", culture = opts.culture or 1, founded = W.hour,
    key = rng.hash(W.seed, rng.S.V3_FOUND, cell, 0, 0) % 1000003,
    buildings = {}, blist = {}, plots = {}, members = {}, households = {}, sites = {}, felled = {},
    need = { food_days = 0, fell = 0, wood_short = 0, lying_trees = 0, site_logs = 0, sowing = false, sow_late = 0 },
    acc = {}, alive = true, sqx = 0, sqy = 0, rain_now = 0, couples = {},
  }
  V.settlements[id] = s
  mapm.build(W, s)
  return s
end

-- Buildings -----------------------------------------------------------------------------------
-- door: the tile in front of the middle of the footprint's side `side` ("s", "n", "e", "w").
function M.add_building(W, s, kind, ix0, iy0, side, state, opts)
  local V = W.S.v3
  local K = M.KINDS[kind]
  local w, h = K.w, K.h
  if side == "e" or side == "w" then w, h = h, w end
  local id = V.next_building
  V.next_building = id + 1
  local N = s.N
  local b = { id = id, kind = kind, label = K.label, ix0 = ix0, iy0 = iy0, w = w, h = h, side = side,
    state = state or "standing", logs_need = K.logs, logs_have = state == "site" and 0 or K.logs,
    work_need = K.work, work_done = state == "site" and 0 or K.work, hh = 0, stock = {}, prio = 0,
    started = W.hour, built = state == "site" and -1 or W.hour }
  if opts then for k, v in pairs(opts) do b[k] = v end end
  for iy = iy0, iy0 + h - 1 do
    for ix = ix0, ix0 + w - 1 do
      s.occ[(iy - 1) * N + ix] = id
    end
  end
  mapm.clear_rect(s, ix0 - 1, iy0 - 1, ix0 + w, iy0 + h)
  -- centre and door point
  local x0, y0 = mapm.ixy_xy(s, ix0, iy0)
  local x1, y1 = mapm.ixy_xy(s, ix0 + w - 1, iy0 + h - 1)
  b.cx, b.cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
  local T = s.T
  if side == "s" then b.dx, b.dy = b.cx, y0 - T
  elseif side == "n" then b.dx, b.dy = b.cx, y1 + T
  elseif side == "e" then b.dx, b.dy = x1 + T, b.cy
  else b.dx, b.dy = x0 - T, b.cy end
  if kind == "well" then b.dx, b.dy = b.cx, y0 - T end
  s.buildings[id] = b
  s.blist[#s.blist + 1] = id
  if state == "site" then s.sites[#s.sites + 1] = id end
  if kind == "granary" then s.granary = b; s.granary_x, s.granary_y = b.dx, b.dy end
  if kind == "dock" then
    -- boats moor on the water beside the dock, opposite its door (on the land side)
    s.dock = b
    b.mx, b.my = 2 * b.cx - b.dx, 2 * b.cy - b.dy
  end
  if kind == "woodstore" then s.wood_store = b; s.wood_x, s.wood_y = b.dx, b.dy end
  if kind == "well" then s.well = b end
  mapm.rebuild_costs(s)
  return b
end

-- A site's work and logs are complete: it stands. A house for a waiting couple becomes their
-- home and their household.
function M.complete_building(W, s, b)
  if b.state ~= "site" then return end
  b.state = "standing"
  b.built = W.hour
  people.remove_id(s.sites, b.id)
  local V = W.S.v3
  if b.kind == "house" and b.for_a then
    local a, c = V.people[b.for_a], V.people[b.for_b or 0]
    local hh = people.new_household(W, s.id, b.id)
    s.households[#s.households + 1] = hh.id
    b.hh = hh.id
    if a and a.alive then people.join(W, a, hh); people.remember(a, W.hour, "Moved into our new house") end
    if c and c.alive then people.join(W, c, hh); people.remember(c, W.hour, "Moved into our new house") end
    b.stock.food = 0
    b.stock.meals = 0
    b.stock.water = 0
    b.stock.logs = 0
    for i = #s.couples, 1, -1 do
      if s.couples[i] == b.for_a then table.remove(s.couples, i) end
    end
  end
  if b.kind == "boat" then
    -- launched: a new boat at the dock for the household that built it; the stocks are cleared
    s.boats = s.boats or {}
    local k = #s.boats + 1
    local d = s.dock
    s.boats[k] = { id = k, x = d and d.mx + (k - 1) * 0.012 or b.cx, y = d and d.my or b.cy, crew = 0, owner = b.for_hh or 0,
      cond = 1, trips = 0, built = W.hour }
    local hh = V.households[b.for_hh or 0]
    if hh then
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive then people.remember(q, W.hour, "Launched our new boat") end
      end
    end
    M.remove_building(W, s, b)
    s.acc.boats_built = (s.acc.boats_built or 0) + 1
    return
  end
  if W.record_events then
    events.emit(W, "house_built", s.cell, b.cause or 0, 1, { settlement = s.id, building = b.id, kind = b.kind })
  end
  s.acc.built = (s.acc.built or 0) + 1
end

-- A building taken down (a boat launched from its stocks): its ground is free again; the record
-- stays (plans in progress may still name it) but nothing lists it.
function M.remove_building(W, s, b)
  local N = s.N
  for iy = b.iy0, b.iy0 + b.h - 1 do
    for ix = b.ix0, b.ix0 + b.w - 1 do
      local i = (iy - 1) * N + ix
      if s.occ[i] == b.id then s.occ[i] = 0 end
    end
  end
  people.remove_id(s.blist, b.id)
  people.remove_id(s.sites, b.id)
  b.state = "gone"
  mapm.rebuild_costs(s)
end

-- Plots ------------------------------------------------------------------------------------

function M.add_plot(W, s, ix0, iy0, hh, name)
  local c = C()
  local P = c.plot_tiles
  local N = s.N
  local k = #s.plots + 1
  local x0, y0 = mapm.ixy_xy(s, ix0, iy0)
  local T = s.T
  local pl = { id = k, ix0 = ix0, iy0 = iy0, w = P, h = P, rows = P, hh = hh or 0, name = name or ("plot " .. k),
    x0 = x0 - T * 0.5, y0 = y0 - T * 0.5, x1 = x0 - T * 0.5 + P * T, y1 = y0 - T * 0.5 + P * T,
    stage = "fallow", crop = "spring", fert = 1, row_st = {}, row_claim = {}, sown = 0, harvested = 0, growth = 0, weeds = 0, quality = 1,
    sheaves = 0, alive = true, sown_day = -1, ripe_day = -1, q_sum = 0, q_n = 0 }
  for r = 1, P do pl.row_st[r], pl.row_claim[r] = 0, 0 end
  -- the stook: where sheaves are stacked, at the corner nearest the village centre
  local cxs = (pl.x0 + pl.x1) * 0.5
  local cys = (pl.y0 + pl.y1) * 0.5
  pl.stx = cxs + (cxs > 0 and -1 or 1) * (P * T * 0.5 - T * 0.8)
  pl.sty = cys + (cys > 0 and -1 or 1) * (P * T * 0.5 - T * 0.8)
  mapm.clear_rect(s, ix0, iy0, ix0 + P - 1, iy0 + P - 1)
  for iy = iy0, iy0 + P - 1 do
    for ix = ix0, ix0 + P - 1 do s.plot[(iy - 1) * N + ix] = k end
  end
  s.plots[k] = pl
  return pl
end

-- Which tree should a woodcutter fell next? A tree on new field ground first (any size), else
-- the nearest grown tree to the woodstore (and to them), off the village ground.
function M.next_tree(W, s, p)
  local tr = s.trees
  local wx, wy = s.wood_x or 0, s.wood_y or 0
  local best, bs = 0, 1e9
  if s.clearing then
    for k = 1, tr.n do
      if tr.st[k] == 1 and tr.claim[k] == 0 and s.plot[tr.tile[k]] == -1 then
        local dx, dy = tr.x[k] - p.x, tr.y[k] - p.y
        local d = sqrt(dx * dx + dy * dy)
        if d < bs then best, bs = k, d end
      end
    end
    if best > 0 then return best, true end
  end
  for k = 1, tr.n do
    if tr.st[k] == 1 and tr.claim[k] == 0 and tr.size[k] >= 0.45 and s.plot[tr.tile[k]] == 0 then
      local x, y = tr.x[k], tr.y[k]
      local r2 = x * x + y * y
      if r2 > 0.09 then
        local dx, dy = x - wx, y - wy
        local d1 = sqrt(dx * dx + dy * dy)
        local ex, ey = x - p.x, y - p.y
        local d2 = sqrt(ex * ex + ey * ey)
        local sc = d1 + 0.5 * d2 - 0.15 * tr.size[k]
        if sc < bs then best, bs = k, sc end
      end
    end
  end
  return best
end

-- House sites: the free 4x4 lot (with a two-tile yard all round) nearest the square, not on a
-- plot, preferring lots beside a lane.
function M.find_lot(W, s)
  local N = s.N
  local c0 = floor(N / 2)
  local best, bx, by, bside = 1e9, 0, 0, "s"
  for r = 3, 26 do
    for iy = c0 - r, c0 + r do
      for ix = c0 - r, c0 + r do
        if (abs(ix - c0) == r or abs(iy - c0) == r) and (ix + iy) % 2 == 0 then
          if mapm.rect_free(s, ix - 2, iy - 2, ix + 5, iy + 5, true) then
            local x, y = mapm.ixy_xy(s, ix + 1.5, iy + 1.5)
            local d = sqrt(x * x + y * y)
            -- beside a lane?
            local lane = 0
            for dy = -3, 6 do
              for dx = -3, 6 do
                local i = (iy + dy - 1) * N + ix + dx
                if i >= 1 and i <= N * N and s.lane[i] > 0 then lane = 1 end
              end
            end
            local sc = d - 0.08 * lane
            if sc < best then
              best, bx, by = sc, ix, iy
              -- the door faces the square
              if abs(x) > abs(y) then bside = x > 0 and "w" or "e" else bside = y > 0 and "s" or "n" end
            end
          end
        end
      end
    end
    if best < 1e8 and r > 6 and best < (r - 4) * s.T then break end
  end
  if best >= 1e8 then return nil end
  return bx, by, bside
end

-- Hourly ------------------------------------------------------------------------------------

-- Summary of the open work and the conditions everyone reads (the board). Returns true when the
-- rain state changed this hour (people outdoors react).
function M.hourly(W, s, now)
  local c = C()
  local V = W.S.v3
  local need = s.need
  -- weather
  local r0 = s.rain_now or 0
  local r1 = weather.rain(W, s, now)
  s.rain_now = r1
  local nc = weather.next_change(W, s, now)
  s.rain_soon = r1 == 0 and nc ~= nil and nc - now < 3
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  s.temp_now = weather.temp_at(W, s, now, lh)
  local wx = weather.today(W, s, now)
  s.cold = wx.temp < c.fuel_warm
  s.cold_k = s.cold and min(1, (c.fuel_warm - wx.temp) / 12) or 0
  -- food
  local pop, food = 0, 0
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive then pop = pop + 1 end
  end
  s.pop = pop
  food = s.granary and ((s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)) or 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" then food = food + people.pantry(b) + (b.stock.meals or 0) end
  end
  need.food = food
  need.food_days = pop > 0 and food / pop or 99
  -- sowing window
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  s.doy = doy
  -- sowing: the spring window, or the winter crop's in autumn (per plot: M.sow_open)
  local seed = s.granary and (s.granary.stock.grain or 0) > c.seed_row
  local w0, w1 = c.sow_season[1], c.sow_season[2]
  if doy >= c.sow_winter[1] and doy <= c.sow_winter[2] then w0, w1 = c.sow_winter[1], c.sow_winter[2] end
  need.sowing = seed and doy >= w0 and doy <= w1
  need.sow_late = need.sowing and (doy - w0) / max(1, w1 - w0) or 0
  -- wood: what is wanted (sites + fuel reserve) against what is stored or lying cut
  local tr = s.trees
  local felled = s.felled
  -- (the woods are scanned again only when a tree, its logs or a claim on it changed: s.tv, D-086)
  local lying, chopping = s.lying or 0, s.chopping or 0
  if s.tv_seen ~= s.tv or not s.lying then
    s.tv_seen = s.tv
    for i = #felled, 1, -1 do felled[i] = nil end
    lying, chopping = 0, 0
    local ct, cs = 0, 0
    local plot = s.plot
    for k = 1, tr.n do
      local st = tr.st[k]
      if st == 2 and tr.logs[k] > 0 then
        felled[#felled + 1] = k
        lying = lying + tr.logs[k]
      elseif st == 1 and tr.claim[k] > 0 then
        chopping = chopping + 1
      end
      if st ~= 0 and plot[tr.tile[k]] == -1 then
        if st == 1 then ct = ct + 1 elseif st == 3 then cs = cs + 1 end
      end
    end
    s.lying, s.chopping, s.clear_t, s.clear_s = lying, chopping, ct, cs
  end
  need.lying_trees = #felled
  local site_logs, short_id, short_n = 0, nil, 0
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b then
      local m = b.logs_need - b.logs_have
      if m > 0 then
        site_logs = site_logs + m
        if m > short_n then short_id, short_n = b.id, m end
      end
    end
  end
  need.site_logs = site_logs
  s.site_short = short_id
  local nh = 0
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then nh = nh + 1 end
  end
  -- the reserve: a season of the hearths' burning (at least store_logs), and logs for repairs
  local burn = 0
  local repairs = 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" then
      if b.hh ~= 0 then burn = burn + (b.burn or c.fuel_cook) end
      if (b.cond or 1) < c.repair_at then repairs = repairs + 1 end
    end
  end
  s.burn_day = burn
  need.repairs = repairs
  -- (autumn and winter: the wood for the cold months is laid in)
  local wdays = (s.doy or 0) >= 24 and c.winter_wood_days or 12
  local reserve = max(c.store_logs, floor(burn * wdays + 0.5)) + repairs * c.repair_logs
  local have = (s.wood_store and s.wood_store.stock.logs or 0) + lying
  local want = site_logs + reserve
  need.wood_short = want > 0 and max(0, min(1, (want - have) / want)) or 0
  need.fell = max(0, ceil((want - have) / 3.5) - chopping)
  -- new field ground: trees to fell and stumps to dig out
  local cl = s.clearing
  need.clear_trees, need.clear_stumps = 0, 0
  if cl then need.clear_trees, need.clear_stumps = s.clear_t, s.clear_s end
  if site_logs > 0 then
    need.fell_why = string.format("Logs are wanted for the %s (%d more) and the woodstore holds %d",
      s.buildings[short_id].label, site_logs, s.wood_store and s.wood_store.stock.logs or 0)
  else
    need.fell_why = string.format("The woodstore holds %d logs; the hearths burn %.0f a day and the village wants %d in store",
      s.wood_store and s.wood_store.stock.logs or 0, burn, reserve)
  end
  -- who is gathered at the well
  local n = 0
  for i = 1, #s.members do
    local p = V.people[s.members[i]]
    if p and p.alive and p.act == "talk" then n = n + 1 end
  end
  s.at_well = n
  -- sun
  local lat = W.grid.lat[s.cell]
  s.sunrise, s.sunset = clock.sun_times(lat, now)
  return r1 ~= r0, r1
end

-- Daily (at the planet step) ----------------------------------------------------------------

-- The sowing window of a plot's crop (local days of year).
local function window(c, crop)
  if crop == "winter" then return c.sow_winter[1], c.sow_winter[2] end
  return c.sow_season[1], c.sow_season[2]
end
M.window = window

-- May plot pl be sown (or hoed for sowing) on local day doy?
function M.sow_open(pl, doy)
  if pl.stage ~= "fallow" and pl.stage ~= "growing" then return false end
  if pl.sown >= pl.rows then return false end
  local a, b = window(C(), pl.crop)
  return doy >= a and doy <= b
end

-- Is it time to plough plot pl's fallow?
function M.plough_open(pl, doy)
  local c = C()
  return pl.stage == "rest" and doy >= c.plough_fallow[1] and doy <= c.plough_fallow[2]
end

-- Plots through the year (D-080): growing (weeds, the crop's growth from the cell's temperature
-- and soil water; a winter crop all but rests in winter), ripe (left standing it spoils), stubble
-- (once the sheaves are carried off: after a winter crop the plot waits for the spring sowing;
-- after a spring crop it rests a year), rest (the soil recovers; ploughed in summer; sown with the
-- winter crop in the autumn window).
local function grow_plots(W, s, day)
  local c = C()
  local F = W.F
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  local winter = doy >= 36
  for k = 1, #s.plots do
    local pl = s.plots[k]
    local cell = s.cell_of[(pl.iy0 + 3 - 1) * s.N + pl.ix0 + 3]
    local T = F.temp[cell] or 10
    local sw = F.soil_water[cell] or 0.5
    if pl.stage == "growing" then
      -- the crop develops with warmth (landraces suited to the climate: full rate from grow_topt;
      -- weeds hold it back a little); its yield is made by water and clean rows over the days it
      -- grows (a dry year ripens on time with a thin harvest, D-087)
      local gt = T > c.grow_tmin and min(1, (T - c.grow_tmin) / (c.grow_topt - c.grow_tmin)) or 0
      local wet = min(1, 0.35 + sw * 1.3)
      local d = gt * (1 - 0.25 * pl.weeds)
      local days = pl.crop == "flax" and c.grow_days_flax or c.grow_days
      if pl.crop == "winter" then
        days = c.grow_days_winter
        if winter then d = d * c.winter_dormant end
      end
      pl.growth = pl.growth + d / days
      pl.q_sum, pl.q_n = pl.q_sum + d * wet * (1 - 0.5 * pl.weeds), pl.q_n + d
      pl.weeds = min(1, pl.weeds + c.weeds_day * (0.5 + wet) * (winter and 0.3 or 1))
      if pl.growth >= 1 then
        pl.stage = "ripe"
        pl.ripe_day = day
        pl.ripe_for = 0
        pl.quality = max(0.25, pl.q_n > 0 and pl.q_sum / pl.q_n or 0.75) * (pl.fert or 1)
        s.q_avg = s.q_avg and (s.q_avg * 0.7 + pl.quality * 0.3) or pl.quality
        pl.spoil = 1
        if W.record_events then events.emit(W, "crop_ripe", s.cell, 0, 0, { settlement = s.id, plot = k }) end
      end
    elseif pl.stage == "ripe" then
      pl.ripe_for = (pl.ripe_for or 0) + 1
      if pl.ripe_for > c.ripe_spoil_days then pl.spoil = max(0, (pl.spoil or 1) - 0.2) end
      if pl.harvested >= pl.sown then
        pl.fert = max(0.3, (pl.fert or 1) - c.fert_crop)
        if pl.crop == "flax" then
          -- the pulled flax lies retting on the field before it is carried in (D-088)
          pl.stage, pl.ret_until = "retting", day + c.ret_days
        else
          pl.stage = "stubble"
        end
      end
    elseif pl.stage == "retting" then
      if day >= (pl.ret_until or day) then
        pl.stage = "stubble"
        pl.flax = (pl.flax or 0) + (pl.flax_ret or 0)
        pl.flax_ret = 0
      end
    elseif pl.stage == "stubble" then
      if pl.sheaves < 0.5 and (pl.flax or 0) < 0.05 then
        pl.sown, pl.harvested, pl.growth = 0, 0, 0
        pl.weeds = min(1, pl.weeds * 0.5 + 0.1)
        for r = 1, pl.rows do pl.row_claim[r] = 0 end
        pl.row_p, pl.win = nil, nil
        if pl.crop == "winter" then
          pl.stage, pl.crop = "fallow", "spring"    -- the spring crop follows next spring
        else
          pl.stage, pl.crop, pl.ploughed, pl.rest_from = "rest", "winter", 0, day   -- a year's rest
        end
      end
    elseif pl.stage == "rest" then
      pl.fert = min(c.fert_max, (pl.fert or 1) + c.fert_rest)
      pl.weeds = min(1, pl.weeds + c.weeds_day * 0.3)
      if doy >= c.sow_winter[1] and doy <= c.sow_winter[2] and day - (pl.rest_from or -99) >= 30 then pl.stage = "fallow" end
    end
    -- the end of the sowing window: what was sown grows; a plot nobody sowed waits for the next
    -- sowing (an unsown winter field is sown in spring, an unsown spring field rests)
    if pl.stage == "fallow" then
      local a, b = window(c, pl.crop)
      if doy >= a and doy <= b then pl.win = true
      elseif pl.win then
        pl.win = nil
        if pl.sown > 0 then pl.stage = "growing"
        elseif pl.crop == "winter" then pl.crop = "spring"
        else pl.stage, pl.crop, pl.ploughed, pl.rest_from = "rest", "winter", 0, day end
      end
    end
  end
end

local function grow_trees(W, s, day)
  local c = C()
  local tr = s.trees
  local hour = W.hour
  for k = 1, tr.n do
    local st = tr.st[k]
    if st == 1 then
      if tr.size[k] < 1 then tr.size[k] = min(1, tr.size[k] + c.tree_grow) end
    elseif st == 3 then
      -- a stump may sprout again after some days (on land not taken by the village)
      if (hour - tr.t_cut[k]) / 24 >= c.sapling_days and s.plot[tr.tile[k]] == 0 and s.occ[tr.tile[k]] == 0 then
        local u = rng.float(W.seed, rng.S.V3_MAP, s.key, k, day)
        if u < 0.06 then
          tr.st[k] = 1
          tr.size[k] = 0.05
          s.tv = (s.tv or 0) + 1
          s.dirty_costs = true
        end
      end
    end
  end
end

-- Hearths (D-080): every lived-in house cooks on its hearth each day (logs from its woodpile) and
-- burns more on a chilly day; a house with no wood is cold (and eats its meals cold). Water: a jar
-- a day. Houses weather and are repaired when worn. Food at home spoils slowly.
local function hearths(W, s, day)
  local c = C()
  local wx = weather.today(W, s, day * 24)
  local V = W.S.v3
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" then
      b.cond = max(0, (b.cond or 1) - c.wear_house)
    end
    if b.kind == "house" and b.state == "standing" and b.hh ~= 0 then
      local hh = V.households[b.hh]
      local n = hh and #hh.members or 0
      if n > 0 then
        b.stock.water = max(0, (b.stock.water or 0) - 1)
        local burn = c.fuel_cook * (0.7 + 0.06 * n)
        if wx.temp < c.fuel_warm then burn = burn + c.fuel_heat * min(1.6, (c.fuel_warm - wx.temp) / 10) end
        b.burn = burn
        local logs = b.stock.logs or 0
        if logs >= burn then
          b.stock.logs = logs - burn
          b.cold = false
          b.fire = true
        else
          b.stock.logs = 0
          b.cold = wx.temp < c.fuel_warm
          b.fire = logs > 0
        end
        -- seed kept at home after the sowing is grain for the pot
        if (b.stock.seed or 0) > 0 and not s.need.sowing then
          b.stock.food = (b.stock.food or 0) + b.stock.seed
          b.stock.seed = 0
        end
        local fd, ml, fi, sm = b.stock.food or 0, b.stock.meals or 0, b.stock.fish or 0, b.stock.smoked or 0
        if fd > 0 or ml > 0 or fi > 0 or sm > 0 then
          local lf, lm, li, ls = fd * c.spoil_food, ml * c.spoil_meals, fi * c.spoil_fish, sm * c.spoil_smoked
          b.stock.food, b.stock.meals, b.stock.fish, b.stock.smoked = fd - lf, ml - lm, fi - li, sm - ls
          s.acc.spoiled = (s.acc.spoiled or 0) + lf + lm + li + ls
          s.acc.fish_spoiled = (s.acc.fish_spoiled or 0) + li
        end
      else
        b.fire = false
      end
    end
  end
  -- the granary: grain and sheaves keep, but mice and damp take a little every day
  local g = s.granary
  if g then
    local gr, sh = g.stock.grain or 0, g.stock.sheaves or 0
    local lost = gr * c.spoil_grain + sh * c.spoil_sheaves
    g.stock.grain = gr - gr * c.spoil_grain
    g.stock.sheaves = sh - sh * c.spoil_sheaves
    s.acc.spoiled = (s.acc.spoiled or 0) + lost
  end
end

-- Wild food at the woods' edge (D-080): it regrows on the standing trees of the edge band (more in
-- summer and autumn, little in winter) up to what the edge can hold; fruit not picked rots. Clearing
-- the woods shrinks it.
local function wild_food(W, s, day, fill)
  local c = C()
  local tr = s.trees
  local n = 0
  for k = 1, tr.n do
    if tr.st[k] == 1 then
      local x, y = tr.x[k], tr.y[k]
      local r2 = x * x + y * y
      if r2 > 0.25 and r2 < 1.69 then n = n + tr.size[k] end
    end
  end
  s.edge_trees = n
  local se = floor(clock.local_doy(day * 24, W.grid.lat[s.cell]) / 12) + 1
  local g = n * c.wild_regrow[se]
  local cap = n * c.wild_regrow[se] * c.wild_cap_days
  local w = fill and cap * fill or (s.wild or 0) + g
  if w > cap then w = cap + (w - cap) * 0.6 end
  s.wild = w
end
M.wild_food = wild_food

-- Food: will the fields feed everyone to the next harvest and leave a store? If not, new field
-- ground is staked out next to the fields (trees felled, stumps dug out, then it is a plot).
local function plan_fields(W, s, day)
  local c = C()
  if s.clearing or #s.plots >= c.max_plots then return end
  local pop = s.pop or 0
  if pop == 0 then return end
  local expect = (s.granary and ((s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)) or 0)
  -- (the yield to expect: the standing crop's own so far, else the village's lately: a dry site
  -- clears more land for the same bread, D-087)
  local qv = s.q_avg or 0.85
  for k = 1, #s.plots do
    local pl = s.plots[k]
    local standing = 0
    for r = 1, pl.rows do if pl.row_st[r] == 2 then standing = standing + 1 end end
    local q = (pl.stage == "growing" and pl.q_n > 0.5) and pl.q_sum / pl.q_n or qv
    local fy = pl.crop == "flax" and 0 or c.row_yield * (pl.fert or 1) * q
    expect = expect + standing * fy * 0.85 + pl.sheaves
    if pl.stage == "fallow" then expect = expect + (pl.rows - pl.sown) * fy * 0.6 end
    -- a resting field will bear the winter crop: part of the year ahead
    if pl.stage == "rest" then expect = expect + pl.rows * fy * 0.3 end
  end
  -- what the village eats (as they have eaten lately: meals and bread in the fields) for a year
  -- and a half, and the seed for every field (D-080)
  local rows = 0
  for k = 1, #s.plots do rows = rows + s.plots[k].rows end
  local want = (s.eat_avg or pop * 0.75) * c.days_per_year * (1 + c.store_years) + rows * c.seed_row
  s.food_expect, s.food_want = expect, want
  -- hands to spare (adults idle much of the day, measured: s.idle_avg) and open land: they clear
  -- more than they need (a bigger store against a bad year), up to half again (D-081)
  local spare = (s.idle_avg or 0) > c.idle_expand and expect < want * 1.6
  s.expand_why = (expect < want) and "short" or (spare and "spare" or nil)
  if expect >= want and not spare then return end
  -- the free lattice spot nearest the fields and the village
  local N, P = s.N, c.plot_tiles
  local best, bix, biy = 1e9, 0, 0
  for iy = 4, N - P - 3, P + 1 do
    for ix = 4, N - P - 3, P + 1 do
      if mapm.rect_free(s, ix, iy, ix + P - 1, iy + P - 1, true) then
        local x, y = mapm.ixy_xy(s, ix + P / 2, iy + P / 2)
        local d = sqrt(x * x + y * y)
        if d > 0.42 and d < 1.4 then
          local dp = 9
          for k = 1, #s.plots do
            local pl = s.plots[k]
            local ex, ey = (pl.x0 + pl.x1) * 0.5 - x, (pl.y0 + pl.y1) * 0.5 - y
            local e = sqrt(ex * ex + ey * ey)
            if e < dp then dp = e end
          end
          local trees = 0
          for jy = iy, iy + P - 1 do
            for jx = ix, ix + P - 1 do
              if s.tree[(jy - 1) * N + jx] > 0 then trees = trees + 1 end
            end
          end
          local sc = d * 0.6 + dp + trees * 0.012
          if sc < best then best, bix, biy = sc, ix, iy end
        end
      end
    end
  end
  if best >= 1e9 then return end
  s.clearing = { ix0 = bix, iy0 = biy, started = W.hour }
  for jy = biy, biy + P - 1 do
    for jx = bix, bix + P - 1 do s.plot[(jy - 1) * N + jx] = -1 end
  end
  s.tv = (s.tv or 0) + 1
  local x, y = mapm.ixy_xy(s, bix + P / 2, biy + P / 2)
  s.clearing.x, s.clearing.y = x, y
  if W.record_events then events.emit(W, "village_expanded", s.cell, 0, 0, { settlement = s.id, kind = "field" }) end
end

-- New field ground with no tree, stump or log left on it becomes a plot.
local function finish_clearing(W, s)
  local cl = s.clearing
  if not cl then return end
  local c = C()
  local N, P = s.N, c.plot_tiles
  local tr = s.trees
  for jy = cl.iy0, cl.iy0 + P - 1 do
    for jx = cl.ix0, cl.ix0 + P - 1 do
      local k = s.tree[(jy - 1) * N + jx]
      if k > 0 and tr.st[k] ~= 0 then return end
    end
  end
  for jy = cl.iy0, cl.iy0 + P - 1 do
    for jx = cl.ix0, cl.ix0 + P - 1 do s.plot[(jy - 1) * N + jx] = 0 end
  end
  s.tv = (s.tv or 0) + 1
  local V = W.S.v3
  -- the household with the fewest fields per mouth takes it
  local bh, br = nil, 1e9
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then
      local n = 0
      for k = 1, #s.plots do if s.plots[k].hh == hh.id then n = n + 1 end end
      local r = n / #hh.members
      if r < br then bh, br = hh, r end
    end
  end
  local pl = M.add_plot(W, s, cl.ix0, cl.iy0, bh and bh.id or 0, "")
  local m = bh and V.people[bh.members[1]]
  pl.name = (m and m.surname or "New") .. " new field"
  pl.cleared = W.hour
  s.clearing = nil
  mapm.rebuild_costs(s)
end
M.finish_clearing = finish_clearing

-- Boats (D-087): they rot a little ashore; a household with a fisher and no sound boat of its own
-- lays down a new one on the stocks by the dock (logs and work, like a house).
local function plan_boats(W, s)
  if not s.dock then return end
  local c = C()
  local V = W.S.v3
  s.boats = s.boats or {}
  for _, boat in ipairs(s.boats) do
    if boat.crew == 0 and not boat.lost then
      boat.cond = max(0, (boat.cond or 1) - c.boat_rot)
      if boat.cond <= 0 then
        boat.lost = true    -- rotten through: broken up for firewood
        boat.owner = -1
      end
    end
  end
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "boat" then return end
  end
  for i = 1, #s.households do
    local hh = V.households[s.households[i]]
    if hh and #hh.members > 0 then
      local fisher = nil
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive and q.role == "fisher" then fisher = q end
      end
      if fisher then
        local has = false
        for _, boat in ipairs(s.boats) do
          if boat.owner == hh.id and not boat.lost and (boat.cond or 1) > c.boat_unsafe then has = true end
        end
        if not has then
          -- the stocks: free shore land near the dock
          local d = s.dock
          local N = s.N
          local dix, diy = mapm.tile_ixy(s, mapm.tile_of(s, d.dx, d.dy))
          for r = 1, 10 do
            local done = false
            for jy = diy - r, diy + r do
              for jx = dix - r, dix + r do
                if not done and (abs(jx - dix) == r or abs(jy - diy) == r) and mapm.rect_free(s, jx - 1, jy - 1, jx + 2, jy + 1, true) then
                  local b = M.add_building(W, s, "boat", jx, jy, "s", "site",
                    { for_hh = hh.id, for_name = "the " .. fisher.surname .. "s' boat", prio = 0.4 })
                  people.remember(fisher, W.hour, "Laid down a new boat by the dock")
                  done = b ~= nil
                end
              end
            end
            if done then return end
          end
          return
        end
      end
    end
  end
end

-- Textiles (D-088): the village's linen, from flax field to cloth, in garments' worth.
function M.textile_pipeline(W, s)
  local c = C()
  local t = s.granary and (s.granary.stock.flax or 0) or 0
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" then t = t + (b.stock.flax or 0) + (b.stock.fibre or 0) + (b.stock.yarn or 0) + (b.stock.cloth or 0) end
  end
  for k = 1, #s.plots do
    local pl = s.plots[k]
    if pl.crop == "flax" then
      t = t + (pl.flax or 0) + (pl.flax_ret or 0)
      if pl.stage == "growing" or pl.stage == "ripe" then t = t + (pl.sown - pl.harvested) * c.flax_row * 0.8 end
    end
  end
  return t
end

-- In the spring sowing window, a village short of linen sows one field with flax: one of the
-- fields waiting for the spring crop, its household's (the village's want: textile_want a person).
local function plan_flax(W, s, day)
  local c = C()
  local doy = clock.local_doy(day * 24, W.grid.lat[s.cell])
  if doy < c.sow_season[1] or doy > c.sow_season[2] - 4 then return end
  local year = floor((day + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  if s.flax_year == year then return end
  local want = (s.pop or #s.members) * c.textile_want
  local have = M.textile_pipeline(W, s)
  s.flax_year = year
  -- (as many fields as the want takes, the most fertile waiting for the spring crop first)
  while have < want do
    local best = nil
    for k = 1, #s.plots do
      local pl = s.plots[k]
      if pl.stage == "fallow" and pl.crop == "spring" and pl.sown == 0 then
        if not best or (pl.fert or 1) > (best.fert or 1) then best = pl end
      end
    end
    if not best then return end
    best.crop = "flax"
    best.name = best.name:gsub(" field", " flax field", 1)
    local hh = W.S.v3.households[best.hh]
    if hh then
      for _, id in ipairs(hh.members) do
        local q = W.S.v3.people[id]
        if q and q.alive and people.stage(q, W.hour) == "adult" then
          people.remember(q, W.hour, string.format("Our field goes to flax this year (the village has %.0f of the %.0f garments' linen it wants)", have, want))
        end
      end
    end
    have = have + best.rows * c.flax_row * 0.8
  end
end

-- Growth: couples without a home get a house site.
local function plan_houses(W, s)
  local V = W.S.v3
  local open = 0
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.kind == "house" then open = open + 1 end
  end
  for i = 1, #s.couples do
    if open >= 2 then break end
    local a = V.people[s.couples[i]]
    if a and a.alive and not a.house_site then
      local ix, iy, side = M.find_lot(W, s)
      if ix then
        local b = M.add_building(W, s, "house", ix, iy, side, "site",
          { for_a = a.id, for_b = a.partner, for_name = "the " .. a.surname .. "s", prio = 1 })
        a.house_site = b.id
        open = open + 1
        if W.record_events then
          events.emit(W, "house_planned", s.cell, 0, 0, { settlement = s.id, building = b.id })
        end
      end
    end
  end
end

function M.daily(W, s, day)
  -- adults' idle share of the waking day, as measured (act.lua counts leisure hours)
  local lz, aw = s.acc.lz_h or 0, s.acc.aw_h or 0
  if s.aw_prev and aw - s.aw_prev > 1 then
    local share = (lz - s.lz_prev) / (aw - s.aw_prev)
    s.idle_avg = s.idle_avg and (s.idle_avg + (share - s.idle_avg) * 0.15) or share
  end
  s.lz_prev, s.aw_prev = lz, aw
  -- the village's daily eating, as measured (meals at home and bread in the fields)
  local ate = (s.acc.eaten or 0) + (s.acc.lunch or 0)
  if s.ate_prev and day > 0 then
    local d = ate - s.ate_prev
    s.eat_avg = s.eat_avg and (s.eat_avg + (d - s.eat_avg) * 0.1) or d
  end
  s.ate_prev = ate
  grow_plots(W, s, day)
  grow_trees(W, s, day)
  hearths(W, s, day)
  wild_food(W, s, day)
  plan_houses(W, s)
  plan_boats(W, s)
  plan_flax(W, s, day)
  plan_fields(W, s, day)
  finish_clearing(W, s)
  mapm.daily_wear(s)
  -- the rivers as today's flow has them (a flood closes the fords, a drought opens more)
  mapm.rivers(W, s, false, M.today_widths(W))
  mapm.rebuild_costs(s)
  s.dirty_costs = false
end

-- The planet's river widths today (one computation a planet day for every settlement; runtime).
local WD = setmetatable({}, { __mode = "k" })
function M.today_widths(W)
  local e = WD[W]
  if e and e.tick == W.tick then return e.wd end
  local wd = require("sim.surface").river_widths(W, e and e.wd)
  WD[W] = { tick = W.tick, wd = wd }
  return wd
end

return M
