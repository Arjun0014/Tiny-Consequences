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
  local v3 = W.v3
  local toy = v3 and (math.floor(hour_f / 24) % 48) or (math.floor(hour_f / 168) % 48)
  if v3 and W.S.v3 and W.S.v3.settlements[1] then
    -- the season of the settlement nearest the camera (the south is half a year on, D-087)
    local st = require("game.view.state")
    local fx, fy, fz = st.focus_x or 0, st.focus_y or 0, st.focus_z or 1
    local best, bd = W.S.v3.settlements[1], -2
    for _, s in ipairs(W.S.v3.settlements) do
      local f = s.frame
      local d = f[1] * fx + f[2] * fy + f[3] * fz
      if s.alive and d > bd then best, bd = s, d end
    end
    if W.grid.lat[best.cell] < 0 then toy = (toy + 24) % 48 end
  end
  local lh = daylight.local_hour(hour_f, focus_lon or 0)
  local hh = math.floor(lh)
  local mm = math.floor((lh - hh) * 60 / (v3 and 1 or 5)) * (v3 and 1 or 5)
  if v3 and W.S.v3 then
    -- v3 (D-078): people are agents; a year is 48 days, a season 12
    local n = 0
    for _, s in ipairs(W.S.v3.settlements) do
      for _, id in ipairs(s.members) do if W.S.v3.people[id].alive then n = n + 1 end end
    end
    T.population, T.villages = n, #W.S.v3.settlements
  end
  M.cur = {
    year = math.floor(hour_f / (v3 and 1152 or 8064)),
    clock = string.format("%02d:%02d", hh, mm), local_hour = lh,
    season = calendar.SEASONS[math.floor(toy / 12) + 1],
    part = v3 and ("Day " .. (toy % 12 + 1)) or ({ "Early", "Mid", "Late" })[math.floor((toy % 12) / 4) + 1],
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
