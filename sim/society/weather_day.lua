-- Daily weather, downscaled from the planet's weekly fields (docs/world/15_CLIMATE_TIME.md §4).
--
-- A stochastic weather generator in the style of Richardson's WGEN: wet/dry days form a
-- two-state Markov chain whose probabilities follow the week's precipitation; rain amounts on the
-- wet days are exponential weights normalised so the week's total is conserved exactly (hydrology
-- keeps using the weekly field). Everything is a pure function of (seed, cell, UTC week) and the
-- live fields — no stored state — so villages, boats and the view all see the same day.
--
-- Days: the planet week t covers UTC hours 168t .. 168t+167 and its 7 days are indexed by the UTC
-- day of the week. A village runs its day step once per UTC day (in the hour its local midnight
-- falls), so it draws each of the week's 7 days exactly once; `for_local_day` finds the day a
-- place is currently living through (for the view).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local daylight = require("sim.society.daylight")

local M = {}
local floor, log, sin, abs, pi = math.floor, math.log, math.sin, math.abs, math.pi
local S_WD = rng.S.WEATHER_DAY
local DEG = pi / 180

-- one-week cache (pure: recomputed identically on a miss)
local cache_c, cache_wk, cache_seed, cache_P, cache_T = -1, -1, -1, -1, -1
local WET, AMT, STORM, GALE, TW = {}, {}, {}, {}, {}

local function compute_week(W, c, wk)
  local cfg = cfgmod.WEATHER_DAY
  local P = W.F.precip[c]
  local T = W.F.temp[c]
  if cache_c == c and cache_wk == wk and cache_seed == W.seed and cache_P == P and cache_T == T then return end
  cache_c, cache_wk, cache_seed, cache_P, cache_T = c, wk, W.seed, P, T
  local seed = W.seed
  local pw = P / cfg.wet_week
  if pw > 1.5 then pw = 1.5 elseif pw < 0 then pw = 0 end
  local pdw = cfg.p_dw[1] + cfg.p_dw[2] * pw
  if pdw < 0.02 then pdw = 0.02 elseif pdw > cfg.p_dw_max then pdw = cfg.p_dw_max end
  local pww = pdw + cfg.p_ww_add
  if pww > cfg.p_ww_max then pww = cfg.p_ww_max end
  local stationary = pdw / (1 - pww + pdw)
  local prev = rng.float(seed, S_WD, c, wk, 0) < stationary
  local n, wsum = 0, 0
  for d = 1, 7 do
    local wet = rng.float(seed, S_WD, c, wk, d) < (prev and pww or pdw)
    local w = 0
    if wet then
      local u = rng.float(seed, S_WD, c, wk, 10 + d)
      if u < 1e-6 then u = 1e-6 end
      w = -log(u) + 0.15
      n = n + 1
      wsum = wsum + w
    end
    WET[d], AMT[d] = wet, w
    prev = wet
  end
  if n == 0 and P > 0.002 then
    -- the week rained: at least one wet day carries it
    local d = 1 + floor(rng.float(seed, S_WD, c, wk, 9) * 7)
    WET[d], AMT[d] = true, 1
    wsum = 1
  end
  -- gales: winter storms at mid/high latitudes (boats stay in even on dry days)
  local lat = W.grid.lat[c]
  local doy = (wk * 7) % 336
  local sgn = lat >= 0 and 1 or -1
  local winter = 0.5 * (1 - sgn * sin(2 * pi * doy / 336))
  local band = abs(lat) > 35 * DEG and 1 or cfg.gale_tropics
  local pg = cfg.gale * (0.25 + 0.75 * winter) * band
  for d = 1, 7 do
    local a = 0
    if WET[d] and wsum > 0 then a = P * AMT[d] / wsum end
    AMT[d] = a
    STORM[d] = WET[d] and a >= cfg.storm_day and P >= cfg.storm_week_min
    GALE[d] = rng.float(seed, S_WD, c, wk, 20 + d) < pg
    TW[d] = T + (rng.float(seed, S_WD, c, wk, 30 + d) - 0.5) * 2 * cfg.temp_wobble - (WET[d] and 1 or 0)
  end
end

-- Weather of the day that starts at absolute hour `hour` (a village's day-step hour) at cell c.
-- Fills `out` (a reused table): wet, rain (moisture units that day), storm (heavy rain or gale:
-- no boats, no outdoor work), gale, snow, temp (daily mean °C), range (day-night °C).
function M.get(W, c, hour, out)
  local wk = floor(hour / 168)
  local d = floor(hour / 24) % 7 + 1
  compute_week(W, c, wk)
  local cfg = cfgmod.WEATHER_DAY
  out.wet = WET[d]
  out.rain = AMT[d]
  out.storm = STORM[d] or GALE[d]
  out.gale = GALE[d]
  out.temp = TW[d]
  out.snow = WET[d] and TW[d] < 0
  local dry = 1 - (W.F.soil_water[c] or 0.5)
  local wetf = WET[d] and 0.3 or 1
  out.range = cfg.diurnal[1] + (cfg.diurnal[2] - cfg.diurnal[1]) * wetf * (0.4 + 0.6 * dry)
  return out
end

-- The start hour of the local day a place at longitude lon is living through at absolute hour
-- `now` (the most recent step hour of its local-midnight bucket).
function M.local_day_start(now, lon)
  local b = daylight.midnight_bucket(lon)
  local h = floor(now)
  return h - ((h - b) % 24)
end

-- The weather cell c is living through at absolute hour `now` (fractional ok).
function M.for_local_day(W, c, now, out)
  return M.get(W, c, M.local_day_start(now, W.grid.lon[c]), out)
end

return M
