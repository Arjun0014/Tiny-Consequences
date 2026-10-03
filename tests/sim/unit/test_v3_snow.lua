-- R6.5d snow and frost on the maps (S20, D-158): the premise is only the cold air (a cold air mass
-- over Tekheim on a wet day); the snow falls from the day's own weather, and what follows - walking
-- slower, beasts kept in, the starving killed for the larder, frost on bare fields - is the village's.
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")

local M = {}

local function shipped()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- The cold air over Tekheim from the first day (after the first) whose weather is wet, at its start.
local function snap()
  local W = shipped()
  local s = W.S.v3.settlements[1]
  local weather = require("sim.agents.weather")
  world.advance(W, W.hour + 24)
  for _ = 1, 30 do
    local t = (math.floor(W.hour / 24) + 1) * 24 + 0.01
    world.advance(W, t)
    local wx = weather.today(W, s, t)
    if wx.wet and wx.a[1] > t + 2 then break end
  end
  local beasts = 0
  for _, k in ipairs({ "flock", "cattle" }) do
    for _, a in ipairs(s.herds[k] and s.herds[k].animals or {}) do if a.alive then beasts = beasts + 1 end end
  end
  T.ok(require("sim.agents.premise").named(W, "cold_snap"), "the cold air")
  return W, s, W.hour, beasts
end

local function said(W, s, pat)
  local n = 0
  for _, id in ipairs(s.members) do
    for _, m in ipairs(W.S.v3.people[id].mem or {}) do
      local txt = type(m) == "table" and (m.text or m[2]) or m
      if type(txt) == "string" and txt:find(pat) then n = n + 1 end
    end
  end
  return n
end

-- Snow falls from the day's spells in the cold air and lies; everyone sees it; people and beasts
-- wade through it; once the grass is buried nobody drives the beasts out to it.
function M.a_cold_snap_lays_snow_and_keeps_the_beasts_in()
  local W, s, t0 = snap()
  local weather = require("sim.agents.weather")
  local most, out_deep = 0, 0
  for h = 1, 36 do
    world.advance(W, t0 + h)
    most = math.max(most, s.snow or 0)
    if weather.reach(s, "sheep") < 0.25 then
      for _, k in ipairs({ "flock", "cattle" }) do if s.herds[k] and s.herds[k].out then out_deep = out_deep + 1 end end
    end
  end
  T.ok(most >= 0.1, string.format("snow lay %.2f m deep", most))
  T.ok(weather.walk_k(s) < 0.9, string.format("walking x%.2f", weather.walk_k(s)))
  T.ok(out_deep == 0, out_deep .. " herd-hours out on buried grass")
  local seen = said(W, s, "Snow is falling")
  T.ok(seen >= 5, seen .. " remember the snow")
  return string.format("%.2f m of snow; walking x%.2f; seen by %d", most, weather.walk_k(s), seen)
end

-- Days of buried grass with the stack empty (the end of winter): the beasts starve slowly; the
-- failing are killed for the larder before they die; the herd is not wiped out.
function M.beasts_starving_in_the_snow_are_killed_for_the_larder()
  local W, s, t0, beasts0 = snap()
  for h = 1, 120 do world.advance(W, t0 + h) end
  local beasts = 0
  for _, k in ipairs({ "flock", "cattle" }) do
    for _, a in ipairs(s.herds[k] and s.herds[k].animals or {}) do if a.alive then beasts = beasts + 1 end end
  end
  T.ok((s.acc.culled_snow or 0) >= 1, (s.acc.culled_snow or 0) .. " marked for killing in the snow")
  T.ok((s.acc.slaughtered or 0) >= 1, (s.acc.slaughtered or 0) .. " killed")
  T.ok(beasts >= beasts0 * 0.5, string.format("%d of %d grazing beasts still alive", beasts, beasts0))
  return string.format("%d marked, %d killed, %d died; %d of %d grazing beasts alive", s.acc.culled_snow or 0, s.acc.slaughtered or 0,
    s.acc.stock_died or 0, beasts, beasts0)
end

-- A night far below freezing on bare fields blackens part of the growing crops: their yield falls
-- and their households remember it.
function M.a_frost_on_bare_fields_costs_the_young_crop()
  local W, s, t0 = snap()
  for h = 1, 30 do world.advance(W, t0 + h) end
  local hit = 0
  for _, pl in ipairs(s.plots) do if (pl.frost or 1) < 1 then hit = hit + 1 end end
  T.ok((s.acc.frosted or 0) >= 1 and hit >= 1, hit .. " plots frosted")
  local n = said(W, s, "frost in the night")
  T.ok(n >= 1, n .. " remember the frost")
  return string.format("%d plots frosted; %d remember it", hit, n)
end

-- The weight on the roofs (shared by snow and ash): a light load cracks nothing, a heavy one cracks
-- the weakest and brings some in.
function M.a_heavy_load_cracks_and_brings_in_the_weakest_roofs()
  local W = shipped()
  local s = W.S.v3.settlements[1]
  local shake = require("sim.agents.shake")
  shake.roof_load(W, s, 0.3, W.hour, nil, "snow")
  T.ok((s.acc.cracked or 0) == 0 and (s.acc.fell or 0) == 0, "0.3: nothing")
  shake.roof_load(W, s, 0.9, W.hour, nil, "snow")
  local hurt = (s.acc.cracked or 0) + (s.acc.fell or 0)
  T.ok(hurt >= 1, hurt .. " roofs cracked or fell under 0.9")
  return string.format("load 0.9: %d cracked, %d fell", s.acc.cracked or 0, s.acc.fell or 0)
end

return M
