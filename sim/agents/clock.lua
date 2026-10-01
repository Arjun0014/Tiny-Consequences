-- The v3 calendar (D-078): one planet step = one game day; a year is 48 days in 4 seasons of 12.
-- Sun position uses the v2 daylight model with the day of year mapped onto its 336-day cycle, so
-- day length and seasons follow latitude exactly as before.
local daylight = require("sim.society.daylight")

local M = {}
local floor = math.floor

M.HOURS_PER_DAY = 24
M.DAYS_PER_YEAR = 48
M.HOURS_PER_YEAR = 24 * 48
M.SEASONS = { "spring", "summer", "autumn", "winter" }

function M.day(t) return floor(t / 24) end
function M.doy(t) return floor(t / 24) % 48 end
function M.year(t) return floor(t / 1152) end
-- Fractional day of year on the 336-day daylight cycle.
function M.doy336(t) return ((t / 24) % 48) * 7 end
function M.season(t) return M.SEASONS[floor(M.doy(t) / 12) + 1] end
function M.age_years(t, born) return (t - born) / 1152 end

-- Local solar hour at longitude lon (radians).
M.local_hour = daylight.local_hour

-- Sunrise and sunset (local solar hours) at latitude lat on the day containing t, quantised to
-- whole minutes (agents compare against them).
function M.sun_times(lat, t)
  local doy = floor(M.doy(t)) * 7 + 3
  local r, s = daylight.sun_times(lat, doy)
  return floor(r * 60 + 0.5) / 60, floor(s * 60 + 0.5) / 60
end

-- Subsolar point at absolute hour t (view): declination from the v3 day of year.
function M.subsolar(t)
  local doy = M.doy336(t)
  local utc = t % 24
  local lon = -15 * (utc - 12) * math.pi / 180
  return daylight.declination(doy), lon
end

-- "Day 7 of spring, year 2" style label.
function M.label(t)
  local d = M.doy(t)
  return string.format("Day %d of %s, year %d", d % 12 + 1, M.season(t), M.year(t))
end

return M
