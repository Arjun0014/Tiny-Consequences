-- Hourly weather at a settlement (docs/v3/10_AGENTS.md §6). In v3 a planet step is a day, so the
-- planet's `precip` of the settlement's cell is that day's rain. It is realised as 0-2 rain spells
-- (start, length, intensity) keyed by (seed, cell, day) — a pure function of state, cached per day.
--   weather.today(W, s) -> wx { day, wet, n, a = {}, b = {}, heavy = {}, temp, range }
--   weather.rain(W, s, t) -> 0 dry, 1 rain, 2 downpour (t absolute hours)
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local floor = math.floor

local function compute(W, s, day)
  local c = s.cell
  local P = W.F.precip[c] or 0
  local T = W.F.temp[c] or 10
  local wd = cfgmod.WEATHER_DAY
  local wx = s.wx
  if not wx then wx = { a = {}, b = {}, heavy = {} }; s.wx = wx end
  wx.day, wx.P, wx.T = day, P, T
  local base = rng.hash(W.seed, rng.S.V3_WEATHER, c, day, 0)
  local function u(k) return rng.fbase(base, k) end
  local pw = P / wd.wet_week
  if pw > 1.6 then pw = 1.6 elseif pw < 0 then pw = 0 end
  local pwet = 0.12 + 0.62 * pw
  if pwet > 0.92 then pwet = 0.92 end
  wx.wet = P > 0.002 and u(1) < pwet
  wx.n = 0
  if wx.wet then
    local n = 1
    if u(2) < 0.25 + 0.3 * pw then n = 2 end
    local heavy_day = P >= wd.storm_day * 1.2 and u(3) < 0.55
    for k = 1, n do
      local start = 24 * day + 24 * u(10 + k)
      local len = 0.7 + (1.5 + 4.5 * pw) * u(20 + k)
      wx.n = k
      wx.a[k], wx.b[k] = start, start + len
      wx.heavy[k] = heavy_day and (k == 1 or u(30 + k) < 0.5)
    end
  end
  wx.temp = T
  local dry = 1 - (W.F.soil_water[c] or 0.5)
  wx.range = wd.diurnal[1] + (wd.diurnal[2] - wd.diurnal[1]) * (wx.wet and 0.3 or 1) * (0.4 + 0.6 * dry)
  return wx
end

function M.today(W, s, t)
  local day = floor((t or W.hour) / 24)
  local wx = s.wx
  if not wx or wx.day ~= day or wx.P ~= W.F.precip[s.cell] then wx = compute(W, s, day) end
  return wx
end

function M.rain(W, s, t)
  local wx = M.today(W, s, t)
  if not wx.wet then return 0 end
  for k = 1, wx.n do
    if t >= wx.a[k] and t < wx.b[k] then return wx.heavy[k] and 2 or 1 end
    -- a spell that began late yesterday is not carried over (each day is its own)
  end
  return 0
end

-- The next change of rain state after t today (hour), or nil.
function M.next_change(W, s, t)
  local wx = M.today(W, s, t)
  local best
  for k = 1, wx.n do
    local a, b = wx.a[k], wx.b[k]
    if a > t and (not best or a < best) then best = a end
    if b > t and (not best or b < best) then best = b end
  end
  return best
end

-- Air temperature at local solar hour lh (coldest at dawn, warmest mid-afternoon; piecewise).
function M.temp_at(W, s, t, lh)
  local wx = M.today(W, s, t)
  local r = wx.range
  local f
  if lh < 5 then f = -0.5 + (5 - lh) * 0.0
  elseif lh < 15 then f = -0.5 + (lh - 5) / 10
  else f = 0.5 - (lh - 15) / 14 end
  return wx.temp + r * f
end

return M
