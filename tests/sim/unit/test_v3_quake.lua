-- R6.5f earthquakes (S20, D-156): the physical event only - a strong quake under Tekheim (it stands on
-- a convergent boundary) at an hour of the night or of the day; the rest is the village's: who is under
-- a roof when it falls, who is trapped, who digs whom out, who is taken in.
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

local function quake_at(lh, hours)
  local W = shipped()
  local V = W.S.v3
  local s = V.settlements[1]
  local clock = require("sim.agents.clock")
  world.advance(W, W.hour + 72)
  local h = clock.local_hour(W.hour, W.grid.lon[s.cell])
  world.advance(W, W.hour + ((lh - h) % 24))
  local fell0, dug0 = s.acc.fell or 0, s.acc.dug_out or 0
  require("sim.agents.premise").named(W, "quake")
  local t0 = W.hour
  local trapped = 0
  for k = 1, hours * 4 do
    world.advance(W, t0 + k * 0.25)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.trapped and not p.counted then p.counted = true; trapped = trapped + 1 end
    end
  end
  return W, s, (s.acc.fell or 0) - fell0, (s.acc.dug_out or 0) - dug0, trapped
end

-- At night, everyone asleep under a roof: houses fall on them, the trapped are dug out by neighbours,
-- the homeless are taken in.
function M.a_night_quake_traps_and_neighbours_dig()
  local W, s, fell, dug, trapped = quake_at(2, 30)
  local V = W.S.v3
  T.ok(fell >= 1, fell .. " buildings fell in")
  T.ok(trapped >= 1, trapped .. " people trapped")
  T.ok(dug >= 1, dug .. " dug out by others")
  local still = 0
  for _, id in ipairs(s.members) do if V.people[id].trapped then still = still + 1 end end
  T.ok(still == 0, "nobody left under the rubble after a day")
  local lodged = 0
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and hh.lodge then lodged = lodged + 1 end
  end
  T.ok(lodged >= 1, lodged .. " households taken in by others")
  return string.format("%d fell, %d trapped, %d dug out, %d households lodging", fell, trapped, dug, lodged)
end

-- At midday most are out of doors: what falls traps few or none.
function M.a_midday_quake_finds_people_out_of_doors()
  local W, s, fell, dug, trapped = quake_at(11, 12)
  local W2, s2, fell2, dug2, trapped2 = quake_at(2, 12)
  T.ok(trapped < trapped2, string.format("fewer trapped by day (%d) than by night (%d)", trapped, trapped2))
  return string.format("by day %d fell, %d trapped; by night %d fell, %d trapped", fell, trapped, fell2, trapped2)
end

-- Volcanoes (D-157): an eruption puts sulphur in the sky (the planet cooler for a while); heavy ash on a
-- village (premise: the ash of an eruption upwind) loads its roofs and buries its grass until washed off.
function M.an_eruption_cools_and_ash_buries_the_grass()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 48)
  local a0 = W.G.aerosol or 0
  T.ok(require("sim.agents.premise").named(W, "eruption"), "an eruption")
  T.ok((W.G.aerosol or 0) > a0 + 0.3, string.format("aerosol %.2f -> %.2f", a0, W.G.aerosol or 0))
  local s = V.settlements[1]
  local P = s.pasture
  local g0 = 0
  for b = 1, #P.grass do g0 = g0 + (P.grass[b] or 0) end
  require("sim.agents.volcano").ashfall(W, s, 0.8, V.now or W.hour, 0, nil, 5)
  local t0 = W.hour
  for h = 1, 48 do world.advance(W, t0 + h) end
  local g1 = 0
  for b = 1, #P.grass do g1 = g1 + (P.grass[b] or 0) end
  T.ok((s.acc.cracked or 0) + (s.acc.fell or 0) >= 1, "roofs cracked or fell under the ash")
  T.ok(g1 < g0, string.format("grass under the ash: %.0f -> %.0f", g0, g1))
  T.ok((s.ash or 0) < 0.8, "the ash is washing off")
  return string.format("aerosol +%.2f; ash 0.8 on %s: %d cracked, %d fell; grass %.0f -> %.0f; ash now %.2f", (W.G.aerosol or 0) - a0, s.name,
    s.acc.cracked or 0, s.acc.fell or 0, g0, g1, s.ash or 0)
end

-- Tsunami (D-162): a great quake under the sea off Tekheim (premise); the wave's run-up from the sea
-- takes what stands near the shore (the dock, the salt pan, the boats moored), whoever is still there
-- is swept, the village above it is spared; everyone knows the sea came.
function M.a_wave_takes_the_shore()
  local W = shipped()
  local V = W.S.v3
  local s = V.settlements[1]
  local clock = require("sim.agents.clock")
  world.advance(W, W.hour + 48)
  local h = clock.local_hour(W.hour, W.grid.lon[s.cell])
  world.advance(W, W.hour + ((11 - h) % 24))
  local houses0 = 0
  for _, id in ipairs(s.blist) do if s.buildings[id].kind == "house" then houses0 = houses0 + 1 end end
  T.ok(require("sim.agents.premise").named(W, "tsunami"), "a quake under the sea")
  T.ok(s.wave ~= nil, "a wave is coming to " .. s.name)
  local runup = s.wave.runup
  local t0 = W.hour
  for k = 1, 16 do world.advance(W, t0 + k * 0.5) end
  T.ok((s.acc.waves or 0) == 1, "the wave came")
  T.ok((s.acc.fell_sea or 0) >= 1, (s.acc.fell_sea or 0) .. " buildings by the shore taken")
  T.ok(s.dock == nil or s.dock.state ~= "standing", "the dock is gone")
  local houses = 0
  for _, id in ipairs(s.blist) do if s.buildings[id].kind == "house" then houses = houses + 1 end end
  T.eq(houses, houses0, "the houses above the shore stand")
  local knew = 0
  for _, id in ipairs(s.members) do
    for _, m in ipairs(V.people[id].mem or {}) do
      local txt = type(m) == "table" and (m.text or m[2]) or m
      if type(txt) == "string" and txt:find("The sea came over the land") then knew = knew + 1; break end
    end
  end
  T.ok(knew >= 10, knew .. " remember the sea coming")
  return string.format("run-up %.2f: %d taken by the shore, %d boats, %d swept, %d saw it go out; %d remember", runup, s.acc.fell_sea or 0,
    s.acc.boats_sea or 0, s.acc.swept or 0, s.acc.waves_seen or 0, knew)
end

return M
