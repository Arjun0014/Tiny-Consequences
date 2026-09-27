-- Sun, day length, local time and daily weather (sim/society/daylight.lua, weather_day.lua).
local T = require("tests.sim.lib")
local daylight = require("sim.society.daylight")
local wday = require("sim.society.weather_day")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local M = {}
local DEG = math.pi / 180

local function near(a, b, tol, msg)
  T.ok(math.abs(a - b) <= tol, string.format("%s: %.3f vs %.3f", msg, a, b))
end

function M.day_length_matches_the_almanac()
  -- equinox at the equator: ~12.1 h (the sunrise angle adds a few minutes)
  near(daylight.day_length(0, daylight.declination(0)), 12.1, 0.1, "equator equinox")
  -- 60 N at the June solstice ~18.8 h, at the December solstice ~5.9 h (almanac with refraction)
  near(daylight.day_length(60 * DEG, daylight.declination(84)), 18.8, 0.4, "60N summer")
  near(daylight.day_length(60 * DEG, daylight.declination(252)), 5.9, 0.4, "60N winter")
  T.eq(daylight.day_length(80 * DEG, daylight.declination(84)), 24, "polar day")
  T.eq(daylight.day_length(80 * DEG, daylight.declination(252)), 0, "polar night")
  -- at the equator days are ~12 h all year
  for doy = 0, 335, 21 do near(daylight.day_length(0, daylight.declination(doy)), 12.1, 0.15, "equator " .. doy) end
end

function M.hemispheres_have_opposite_seasons()
  for _, lat in ipairs({ 20, 40, 55, 70 }) do
    for doy = 0, 335, 28 do
      local n = daylight.day_length(lat * DEG, daylight.declination(doy))
      local s = daylight.day_length(-lat * DEG, daylight.declination((doy + 168) % 336))
      near(n, s, 1e-9, "lat " .. lat .. " doy " .. doy)
    end
    -- northern summer = southern winter
    T.ok(daylight.day_length(lat * DEG, daylight.declination(84)) > daylight.day_length(-lat * DEG, daylight.declination(84)), "opposite at " .. lat)
  end
end

function M.local_time_follows_longitude()
  near(daylight.local_hour(12, 0), 12, 1e-9, "lon 0")
  near(daylight.local_hour(12, 90 * DEG), 18, 1e-9, "90E is 6 h ahead")
  near(daylight.local_hour(12, -90 * DEG), 6, 1e-9, "90W is 6 h behind")
  T.eq(daylight.midnight_bucket(90 * DEG), 18, "90E: local midnight at 18 UTC")
  T.eq(daylight.midnight_bucket(-90 * DEG), 6, "90W: local midnight at 06 UTC")
  -- at the subsolar longitude it is local noon
  for h = 0, 47, 5 do
    local _, lo = daylight.subsolar(h)
    near(daylight.local_hour(h, lo), 12, 1e-6, "subsolar is noon at " .. h)
  end
end

function M.human_rhythm()
  local wake, _, _, sleep, hours = daylight.rhythm(6, 18)
  near(wake, 5.5, 1e-9, "wake before sunrise")
  near(sleep, 21.3, 1e-9, "sleep 3.3 h after sunset")
  T.ok(hours <= 10 and hours >= 3, "work hours bounded")
  local _, _, _, _, polar = daylight.rhythm(12, 12)
  T.ok(polar >= 3, "polar night still has a few working hours")
end

function M.daily_weather_conserves_the_week()
  local W = worlds.fresh(1)
  world.run_ticks(W, 20)
  local out = {}
  local wet_lo, wet_hi, nlo, nhi = 0, 0, 0, 0
  local storms_dry = 0
  for c = 1, W.n, 7 do
    local wk = W.tick - 1
    local sum, wet = 0, 0
    for d = 0, 6 do
      wday.get(W, c, wk * 168 + d * 24, out)
      sum = sum + out.rain
      if out.wet then wet = wet + 1 end
      if out.storm and not out.wet and not out.gale then storms_dry = storms_dry + 1 end
    end
    local P = W.F.precip[c]
    if P > 0.002 then near(sum, P, 1e-9, "weekly total at cell " .. c) end
    if P < 0.01 then wet_lo = wet_lo + wet; nlo = nlo + 1
    elseif P > 0.06 then wet_hi = wet_hi + wet; nhi = nhi + 1 end
  end
  T.eq(storms_dry, 0, "rain storms only on wet days")
  T.ok(nlo > 0 and nhi > 0, "both dry and wet cells sampled")
  T.ok(wet_hi / nhi > wet_lo / nlo + 2, "wet weeks have more wet days")
  -- determinism: a pure function of state
  local h = (W.tick - 1) * 168 + 48
  local a = wday.get(W, 17, h, {}).rain
  wday.get(W, 18, h, out)
  local b = wday.get(W, 17, h, {}).rain
  T.eq(a, b, "pure function")
  -- the local day a place is living through starts at its local midnight
  local lon = W.grid.lon[17]
  local start = wday.local_day_start(h + 5.5, lon)
  near(daylight.local_hour(start, lon), 0, 0.51, "local day starts near local midnight")
  return string.format("wet days/week: dry cells %.1f, wet cells %.1f", wet_lo / nlo, wet_hi / nhi)
end

return M
