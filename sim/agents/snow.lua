-- Snow and frost on the maps (R6.5d, D-158, docs/v3/65_R6_5_LIVING_PLANET.md §10). The planet's
-- precipitation and air temperature as the village lives them hour by hour (`weather.lua`): a spell
-- that falls while the air is below snow_t falls as snow and lies (`s.snow`, metres); above 0 C it
-- melts, faster in rain. Nothing else is stored. What the snow does is read where it matters:
-- walking and grazing (weather.walk_k / weather.reach), the herders keeping beasts in on the hay
-- (think), the hay reckoning while it lies (here: the beasts the stack cannot feed are killed for the
-- larder), the weight on the roofs (shake.roof_load, shared with volcanic ash), a night's frost on
-- bare fields (here), bare feet in it (life), nothing catching fire under it (blaze).
--   s.snow = depth on village s's map (metres); s.snowing while it falls
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")
local weather = require("sim.agents.weather")
local shake = require("sim.agents.shake")
local stock = require("sim.agents.stock")

local M = {}
local floor, min = math.floor, math.min
local function C() return cfgmod.V3 end

-- Snow begins to lie on village s's map: everyone out sees it (and remembers it, once in a while).
function M.lies(W, s, now)
  local d = deeds.add(W, { kind = "snow", t = now, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0, to_s = s.id,
    what = "snow", n = 1, fu = 0, harm = false })
  s.snow_deed = d
  deeds.learn(W, s, d, "snow fell", 0, 0, now)
  local day = floor(now / 24)
  danger.each_on_map(W, s, function(p)
    if p.act ~= "carried" and people.stage(p, now) ~= "infant" and (p.snow_seen or -9) < day - 2 then
      p.snow_seen = day
      people.remember(p, now, "Snow is falling and lying on the ground")
    end
  end)
  s.acc.snowfalls = (s.acc.snowfalls or 0) + 1
end

-- Hourly, after the village's weather of the hour (settle.hourly: s.rain_now, s.temp_now).
function M.hourly(W, s, now)
  local c = C()
  local r, T = s.rain_now or 0, s.temp_now or 10
  local d0 = s.snow or 0
  if r > 0 and T < c.snow_t then
    local d = d0 + c.snow_fall * (r == 2 and 2 or 1)
    s.snow, s.snowing = d, true
    if d0 < c.snow_cover and d >= c.snow_cover then M.lies(W, s, now) end
    -- (deep snow weighs on the roofs: the weakest crack, or fall in on whoever is inside)
    local load = d * c.snow_load_k
    if load >= c.roof_crack * 0.3 then shake.roof_load(W, s, load, now, s.snow_deed, "snow") end
  else
    s.snowing = nil
    if d0 > 0 and T > 0 then
      local d = d0 - c.snow_melt * T * (r > 0 and 2 or 1)
      if d > 0.001 then s.snow = d else s.snow, s.snow_deed = nil, nil end
    end
  end
end

local CROP = { winter = "winter grain", spring = "spring grain", flax = "flax" }

-- A night below frost_t on bare ground (snow keeps the frost off what is under it): part of each
-- growing crop is blackened - its yield falls (a winter crop is hardy to frost_t_winter).
function M.frost(W, s, tmin, now)
  local c = C()
  local V = W.S.v3
  local hit = {}
  for k = 1, #s.plots do
    local pl = s.plots[k]
    if pl.stage == "growing" then
      local ft = pl.crop == "winter" and c.frost_t_winter or c.frost_t
      if tmin < ft then
        local kill = min(1, (ft - tmin) / c.frost_span) * c.frost_k
        pl.frost = (pl.frost or 1) * (1 - kill)
        s.acc.frosted = (s.acc.frosted or 0) + 1
        if (pl.hh or 0) ~= 0 then hit[pl.hh] = pl.crop end
      end
    end
  end
  for _, hid in ipairs(s.households) do
    local crop = hit[hid]
    local hh = crop and V.households[hid]
    if hh then
      for _, id in ipairs(hh.members) do
        local p = V.people[id]
        if p and p.alive and people.stage(p, now) ~= "infant" and people.stage(p, now) ~= "child" then
          people.remember(p, now, sfmt("A frost in the night blackened our growing %s", CROP[crop] or "crop"))
        end
      end
    end
  end
end

-- Daily at the village's midnight: the coming night's frost; while snow lies on the grass, beasts
-- failing on no hay are killed for the larder before they die; once the grass has been buried
-- snow_wait days (nobody knows how long snow will lie), the hay reckoning - the stack must feed every
-- grazing beast snow_hay_days in full: those it cannot are killed while they still have flesh on them
-- (the autumn cull's own rule).
function M.daily(W, s, day, now)
  local c = C()
  local wx = weather.today(W, s, now)
  local tmin = wx.temp - 0.5 * wx.range
  if tmin < c.frost_t and (s.snow or 0) < c.snow_cover then M.frost(W, s, tmin, now) end
  if weather.reach(s, "sheep") < 0.5 then
    s.snow_days = (s.snow_days or 0) + 1
    local n = stock.plan_failing(W, s, "snow")
    -- (the reckoning only while the cold holds: snow that is going is waited out)
    local holding = (s.snow or 0) >= (s.snow_prev or 0) - 0.01
    if s.snow_days >= c.snow_wait and holding then n = n + stock.plan_cull(W, s, c.snow_hay_days, 1, "snow", true) end
    s.acc.culled_snow = (s.acc.culled_snow or 0) + n
  elseif s.snow_days then
    -- (the grass shows again: the beasts not yet killed for the snow are kept)
    s.snow_days = nil
    stock.unmark(s, "snow")
  end
  s.snow_prev = s.snow
end

return M
