-- Sun, day length and local solar time (docs/world/15_CLIMATE_TIME.md §2).
-- Pure functions; nothing here is state. Day of year `doy` runs 0..335 (336-day year, the
-- northern spring equinox at doy 0, summer solstice at doy 84). Day length follows the Forsythe
-- et al. (1995) CBM form: cos w0 = -(sin p + sin(lat) sin(decl)) / (cos(lat) cos(decl)).
-- Longitudes increase eastward (the planet turns toward +lon), so local solar time is
-- UTC + lon/15 degrees and the subsolar point moves toward -lon as the UTC hour advances.
local cfgmod = require("sim.config")

local M = {}
local sin, cos, acos, asin, pi, floor = math.sin, math.cos, math.acos, math.asin, math.pi, math.floor
local DEG = pi / 180

M.DAYS_PER_YEAR = 336
M.HOURS_PER_YEAR = 8064

function M.doy(hour) return floor(hour / 24) % 336 end

-- Solar declination (radians) for a (fractional) day of year.
function M.declination(doy)
  return cfgmod.TIME.tilt * DEG * sin(2 * pi * doy / 336)
end

-- Day length in hours at latitude `lat` (radians) for declination `decl` (radians).
function M.day_length(lat, decl)
  local p = cfgmod.TIME.sunrise_angle * DEG
  local cl, cd = cos(lat), cos(decl)
  local den = cl * cd
  if den < 1e-9 then den = 1e-9 end
  local c = -(sin(p) + sin(lat) * sin(decl)) / den
  if c <= -1 then return 24 end
  if c >= 1 then return 0 end
  return 24 / pi * acos(c)
end

-- Local sunrise and sunset (local solar hours) at latitude lat on day of year doy.
function M.sun_times(lat, doy)
  local d = M.day_length(lat, M.declination(doy))
  return 12 - d * 0.5, 12 + d * 0.5, d
end

-- Local solar hour (0..24) at longitude `lon` (radians) for the absolute hour `hour` (may be
-- fractional).
function M.local_hour(hour, lon)
  local h = (hour + lon / DEG / 15) % 24
  if h < 0 then h = h + 24 end
  return h
end

-- UTC hour at which local midnight falls for longitude lon: an integer bucket 0..23 (the hour
-- step in which a village at this longitude runs its day step).
function M.midnight_bucket(lon)
  local b = floor((-lon / DEG / 15) % 24 + 0.5) % 24
  return b
end

-- The local day number for a place at longitude lon at absolute hour `hour` (days since the clock
-- started, in that place's local solar calendar).
function M.local_day(hour, lon)
  return floor((hour + lon / DEG / 15) / 24)
end

-- Subsolar point (lat, lon in radians) at absolute hour `hour` (fractional allowed).
function M.subsolar(hour)
  local doy = (hour / 24) % 336
  local utc = hour % 24
  local lon = -15 * (utc - 12) * DEG
  return M.declination(doy), lon
end

-- Unit vector toward the sun in grid coordinates (x = cos lat cos lon, y = sin lat,
-- z = cos lat sin lon).
function M.sun_dir(hour)
  local la, lo = M.subsolar(hour)
  local cl = cos(la)
  return cl * cos(lo), sin(la), cl * sin(lo)
end

-- The human day at a place (Yetish et al. 2015): wake just before sunrise, sleep ~3.3 h after
-- sunset (bounded), work from an hour after waking to sunset (at most work_max hours plus a meal
-- hour). Returns wake, work_start, work_end, sleep, work_hours (all local hours).
function M.rhythm(sunrise, sunset)
  local T = cfgmod.TIME
  local wake = sunrise - T.wake_before_sunrise
  if wake < T.wake_min then wake = T.wake_min elseif wake > T.wake_max then wake = T.wake_max end
  local sleep = sunset + T.sleep_after_sunset
  if sleep < T.sleep_min then sleep = T.sleep_min elseif sleep > T.sleep_max then sleep = T.sleep_max end
  local ws = wake + 1
  local we = sunset
  if we > ws + T.work_max + 1 then we = ws + T.work_max + 1 end
  if we < ws + T.work_min + 1 then we = ws + T.work_min + 1 end
  return wake, ws, we, sleep, we - ws - 1
end

return M
