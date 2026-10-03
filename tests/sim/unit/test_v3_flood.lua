-- R6.5e flood water on the maps (S21, D-169): the premise is only rain (wet ground, then five days of
-- downpours over Little Leelee's hills); the river rising over its banks comes from the planet's
-- hydrology (infiltration excess), and what lies in the water - closed ways, a drowned field, a pen
-- opened, people up out of it - is the world's own.
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

-- The rain makes the river rise over its banks within hours of reaching it; the water spreads over
-- low ground only (the houses the founders put above the floodplain stay dry); ways through deep
-- water close; a field on the bottomland drowns; the people see it and get up out of it; the byre in
-- the water is opened; and when the rain is over the water goes down and leaves its silt.
function M.a_wet_spell_floods_the_bottomland()
  local W = shipped()
  local mapm = require("sim.agents.map")
  T.ok(require("sim.agents.premise").named(W, "flood5"), "the wet spell")
  local s = W.S.v3.settlements[5]
  local t0 = W.hour
  local peak, closed, peak_h = 0, 0, 0
  local houses_wet = 0
  for h = 1, 168 do
    world.advance(W, t0 + h)
    local fd = mapm.FLOOD[s]
    if fd then
      local n, c = 0, 0
      for i = 1, s.N * s.N do
        if fd[i] > 0 then
          n = n + 1
          if s.cost[i] < 0 and s.occ[i] == 0 and s.riv[i] <= 0 then c = c + 1 end
        end
      end
      if n > peak then peak, closed, peak_h = n, c, h end
    end
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b and b.kind == "house" and b.wet and b.wet > 0.15 then houses_wet = houses_wet + 1 end
    end
  end
  T.ok(peak >= 500, peak .. " tiles under water at the peak")
  T.ok(peak_h <= 24, "the water over the banks within a day (hour " .. peak_h .. ")")
  T.ok(closed >= 100, closed .. " tiles of deep water closed to walking")
  T.ok(houses_wet == 0, houses_wet .. " house-hours with water in them (the founders built above the floodplain)")
  T.ok((s.acc.floods or 0) >= 1, "a flood deed")
  local seen = said(W, s, "Saw the river come over its banks")
  T.ok(seen >= 5, seen .. " saw the river over its banks")
  T.ok((s.acc.flood_fields or 0) >= 1, (s.acc.flood_fields or 0) .. " fields drowned")
  T.ok((s.acc.loosed or 0) >= 1, (s.acc.loosed or 0) .. " pens opened to the water")
  T.ok(s.fl == nil and mapm.FLOOD[s] == nil, "the water has gone down within a week")
  local silt = 0
  for _, pl in ipairs(s.plots) do if (pl.fert or 1) > 1.001 then silt = silt + 1 end end
  T.ok(silt >= 1, silt .. " fields with fresh silt")
  local deed
  for _, d in ipairs(W.S.v3.deeds or {}) do if d.kind == "flood" and d.at == s.id then deed = d end end
  T.ok(deed ~= nil and deed.peak ~= nil, "the flood remembered as a deed with its peak")
  return string.format("peak %d tiles (%d closed) at hour %d; %d saw it; %d fields drowned, %d pens opened, %d silted",
    peak, closed, peak_h, seen, s.acc.flood_fields or 0, s.acc.loosed or 0, silt)
end

-- The same village without the wet spell: at most a little water on the bottomland after its own
-- rains (a wet season there), never in the fields or the pens.
function M.without_the_rain_only_a_little_water()
  local W = shipped()
  local mapm = require("sim.agents.map")
  local s = W.S.v3.settlements[5]
  local t0 = W.hour
  local peak = 0
  for h = 1, 96 do
    world.advance(W, t0 + h)
    local fd = mapm.FLOOD[s]
    if fd then
      local n = 0
      for i = 1, s.N * s.N do if fd[i] > 0 then n = n + 1 end end
      if n > peak then peak = n end
    end
  end
  T.ok(peak < 1200, peak .. " tiles under water at most without the wet spell")
  T.ok((s.acc.flood_fields or 0) == 0 and (s.acc.loosed or 0) == 0, "no field drowned, no pen flooded")
  return string.format("at most %d tiles of water on the bottomland", peak)
end

return M
