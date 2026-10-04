-- Hourly weather at a settlement (docs/v3/10_AGENTS.md §6). In v3 a planet step is a day, so the
-- planet's `precip` of the settlement's cell is that day's rain. It is realised as 0-2 rain spells
-- (start, length, intensity) keyed by (seed, cell, day) — a pure function of state, cached per day.
--   weather.today(W, s) -> wx { day, wet, n, a = {}, b = {}, heavy = {}, temp, range }
--   weather.rain(W, s, t) -> 0 dry, 1 rain, 2 downpour (t absolute hours)
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local floor = math.floor

-- (S23) The day's rain spells of planet cell c - a pure function of the world (nothing is written but `wx`):
-- the draw a village on c lives by, and what the clouds over c show (game/view/clouds.lua).
--   wx.day, wx.P, wx.T, wx.wet, wx.n, wx.a[k], wx.b[k], wx.heavy[k] (k <= n)
function M.cell_day(W, c, day, wx)
  local P = W.F.precip[c] or 0
  local T = W.F.temp[c] or 10
  local wd = cfgmod.WEATHER_DAY
  wx.day, wx.P, wx.T = day, P, T
  local base = rng.hash(W.seed, rng.S.V3_WEATHER, c, day, 0)
  local pw = P / wd.wet_week
  if pw > 1.6 then pw = 1.6 elseif pw < 0 then pw = 0 end
  local pwet = 0.12 + 0.62 * pw
  if pwet > 0.92 then pwet = 0.92 end
  wx.wet = P > 0.002 and rng.fbase(base, 1) < pwet
  wx.n = 0
  if wx.wet then
    local n = 1
    if rng.fbase(base, 2) < 0.25 + 0.3 * pw then n = 2 end
    local heavy_day = P >= wd.storm_day * 1.2 and rng.fbase(base, 3) < 0.55
    for k = 1, n do
      local start = 24 * day + 24 * rng.fbase(base, 10 + k)
      local len = 0.7 + (1.5 + 4.5 * pw) * rng.fbase(base, 20 + k)
      wx.n = k
      wx.a[k], wx.b[k] = start, start + len
      wx.heavy[k] = heavy_day and (k == 1 or rng.fbase(base, 30 + k) < 0.5)
    end
  end
  return wx
end

local function compute(W, s, day)
  local c = s.cell
  local wd = cfgmod.WEATHER_DAY
  local wx = s.wx
  if not wx then wx = { a = {}, b = {}, heavy = {} }; s.wx = wx end
  M.cell_day(W, c, day, wx)
  wx.temp = wx.T
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

-- (S22) A god's rain over this village begins when it is cast - a downpour for its first `burst_h` hours,
-- rain for the rest of that day (the planet's wetter days follow by themselves); a dry spell stops the rain
-- at once. Nil when no power holds the sky here now.
local function forced(W, s, t)
  local P = W.S.powers
  local fs = P and P.forcings
  if not fs then return nil end
  for i = #fs, 1, -1 do
    local f = fs[i]
    if f.at and f.sids and t >= f.at and t < f.at + 24 then
      for k = 1, #f.sids do
        if f.sids[k] == s.id then
          if f.kind == "rain" then return t < f.at + cfgmod.POWERS.v3_burst_h and 2 or 1 end
          if f.kind == "drought" then return 0 end
        end
      end
    end
  end
  return nil
end

function M.rain(W, s, t)
  local fr = forced(W, s, t)
  if fr then return fr end
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

-- (S20, R6.5d) Snow lying on village s's map (`s.snow`, metres, sim/agents/snow.lua): walking slows
-- with its depth; the grass under it is out of a beast's reach (it paws so deep: goats deeper,
-- cattle less, pigs rooting least).
function M.walk_k(s)
  local d = s.snow
  if not d then return 1 end
  local c = cfgmod.V3
  local k = c.snow_walk * d
  if k > c.snow_walk_max then k = c.snow_walk_max end
  return 1 - k
end

function M.reach(s, sp)
  local d = s.snow
  if not d then return 1 end
  local c = cfgmod.V3
  local r = 1 - d / (c.snow_graze * (c.snow_paw[sp] or 1))
  if s.crust then r = r * c.crust_k end   -- (S20) an ice crust: the grass under it all but lost
  return r > 0 and r or 0
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
