-- HUD numbers + trend arrows (Stage 1.9). Pure Lua over read-only W.
-- Trends compare the current totals with a sample taken about a year earlier.
local inspect = require("game.ui.inspect")
local calendar = require("sim.calendar")
local daylight = require("sim.society.daylight")

local M = { cur = nil, hist = {} }

local function trend(now, before)
  if not before then return "flat" end
  local base = math.max(1, math.abs(before))
  local d = (now - before) / base
  if d > 0.03 then return "up" elseif d < -0.03 then return "down" end
  return "flat"
end

-- hour_f: the view's clock (sim hour + the fraction of the next hour); focus_lon: longitude
-- under the camera (the clock shows local solar time there).
function M.update(W, hour_f, focus_lon)
  local T = inspect.totals(W)
  local G = W.G
  local tick = W.tick
  hour_f = hour_f or W.hour
  -- keep one sample per quarter year, ~1.25 years deep
  local h = M.hist
  if #h == 0 or tick - h[#h].tick >= 12 or tick < h[#h].tick then
    if #h > 0 and tick < h[#h].tick then for i = #h, 1, -1 do h[i] = nil end end
    h[#h + 1] = { tick = tick, grazers = T.grazers, predators = T.predators, fish = T.fish, population = T.population }
    while #h > 6 do table.remove(h, 1) end
  end
  local old = nil
  for i = #h, 1, -1 do
    if tick - h[i].tick >= 40 then old = h[i]; break end
  end
  old = old or h[1]
  local toy = math.floor(hour_f / 168) % 48
  local lh = daylight.local_hour(hour_f, focus_lon or 0)
  local hh = math.floor(lh)
  local mm = math.floor((lh - hh) * 60 / 5) * 5
  M.cur = {
    year = math.floor(hour_f / 8064),
    clock = string.format("%02d:%02d", hh, mm), local_hour = lh,
    season = calendar.SEASONS[math.floor(toy / 12) + 1],
    part = ({ "Early", "Mid", "Late" })[math.floor((toy % 12) / 4) + 1],
    population = T.population, villages = T.villages,
    grazers = T.grazers, predators = T.predators, fish = T.fish,
    t_grazers = trend(T.grazers, old and old.grazers),
    t_predators = trend(T.predators, old and old.predators),
    t_fish = trend(T.fish, old and old.fish),
    t_population = trend(T.population, old and old.population),
    g_temp = G.g_temp or 0, co2 = G.co2 or 280, ice = G.ice_fraction or 0, sea = (G.sea_level or 0) * 1000,
    era = G.era,
  }
  return M.cur
end

function M.short(n)
  n = n or 0
  if n >= 1e6 then return string.format("%.1fM", n / 1e6) end
  if n >= 1e4 then return string.format("%dk", math.floor(n / 1000 + 0.5)) end
  if n >= 1e3 then return string.format("%.1fk", n / 1000) end
  return tostring(math.floor(n + 0.5))
end

return M
