-- R6.5b, lightning on a village (S20, D-154, docs/v3/65_R6_5_LIVING_PLANET.md §6): the physical event
-- only - a strike at a point, a roof set alight - with the weather and the hour as premises; the rest is
-- the village's own: who notices, who runs out (with the babe), who carries water, whether the house is
-- saved, who takes in a household with no roof, who tends the burnt. The same code, different outcomes.
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

-- Tekheim on day 3 at local hour `lh` with its ground at soil water `sw`.
local function at(W, lh, sw)
  local V = W.S.v3
  local s = V.settlements[1]
  local clock = require("sim.agents.clock")
  world.advance(W, W.hour + 72)
  local h = clock.local_hour(W.hour, W.grid.lon[s.cell])
  world.advance(W, W.hour + ((lh - h) % 24))
  W.F.soil_water[s.cell] = sw
  return s
end

local function a_house(s)
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.state == "standing" and b.hh ~= 0 then return b end
  end
end

local function watch(W, s, hours)
  local V = W.S.v3
  local t0 = W.hour
  local fought, fled = {}, {}
  for h = 0.1, hours, 0.1 do
    world.advance(W, t0 + h)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive then
        if p.objk == "fight_fire" then fought[id] = true end
        if p.objk == "flee_fire" then fled[id] = true end
      end
    end
  end
  local nf, nl = 0, 0
  for _ in pairs(fought) do nf = nf + 1 end
  for _ in pairs(fled) do nl = nl + 1 end
  return nf, nl
end

-- A roof alight on a moderately dry afternoon: the village carries water and saves the house.
function M.a_house_fire_by_day_is_put_out()
  local W = shipped()
  local s = at(W, 15, 0.33)
  local b = a_house(s)
  local blaze = require("sim.agents.blaze")
  local n0 = #s.members
  blaze.ignite(W, s, "bld", b.id, W.hour, nil)
  local nf, nl = watch(W, s, 6)
  T.ok(nf >= 3, nf .. " carried water to the fire")
  T.ok((s.acc.fires_out or 0) >= 1 and (s.acc.burnt or 0) == 0, "it was put out, nothing burnt down")
  T.ok(s.buildings[b.id] ~= nil and s.buildings[b.id].state == "standing", "the house stands")
  T.ok(#s.members >= n0 - 1, "nobody died of it")
  return string.format("%d fought it, %d kept away, %d buckets", nf, nl, s.acc.doused or 0)
end

-- The same roof on a night in a drought: it burns down; the household lodges with others, its babe
-- carried out, a house of its own planned.
function M.a_house_fire_in_a_drought_night_is_lost()
  local W = shipped()
  local V = W.S.v3
  local s = at(W, 2, 0.15)
  local b = a_house(s)
  local hh = V.households[b.hh]
  local blaze = require("sim.agents.blaze")
  blaze.ignite(W, s, "bld", b.id, W.hour, nil)
  local nf, nl = watch(W, s, 40)
  T.ok((s.acc.burnt or 0) >= 1, "the house burnt down")
  T.ok(hh.lodge ~= nil, "the household lodges with others (or camps)")
  local host = V.households[hh.lodge or 0]
  local planned = false
  for _, id in ipairs(s.sites) do
    local x = s.buildings[id]
    if x and x.for_hh == hh.id then planned = true end
  end
  T.ok(planned or (hh.house_site ~= nil), "a house of its own is planned")
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    T.ok(p.alive and (p.hurt or 0) < 0.5, p.name .. " got out unharmed")
  end
  local q = host and V.people[host.members[1] or 0]
  return string.format("burnt down; %d fought, %d fled; lodging with %s; house planned: %s", nf, nl, q and (q.surname .. "s") or "nobody (camp)", tostring(planned))
end

-- Lightning on someone at work in the open: burnt (or killed), and, alive, kept to bed and tended.
function M.lightning_hurts_who_it_strikes()
  local W = shipped()
  local V = W.S.v3
  local s = at(W, 11, 0.3)
  local act = require("sim.agents.act")
  local who
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.alive and p.inside == 0 and (p.act == "till" or p.act == "harvest" or p.act == "sow" or p.act == "tend") then who = p; break end
  end
  T.ok(who ~= nil, "someone at work in a field")
  if not who then return end
  local x, y = act.pos(who, W.hour)
  local blaze = require("sim.agents.blaze")
  local hit, ref = blaze.strike(W, s, x, y, W.hour, nil)
  T.ok(hit == "person" and ref == who.id, "the strike took " .. who.name .. " (" .. tostring(hit) .. ")")
  T.ok((who.hurt or 0) > 0 or not who.alive, "burnt")
  watch(W, s, 30)
  T.ok(not who.alive or (who.tended or -9) >= 0, "tended at home")
  return string.format("%s struck at work: %s", who.name, who.alive and string.format("alive, hurt %.2f", who.hurt or 0) or ("dead: " .. who.died_of))
end

-- R6.5c: the planet's fire on a wooded cell beside a village (premise "wildfire": a dry spell) comes
-- onto its map tree by tree; the people see it.
function M.a_wildfire_comes_onto_the_map()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 48)
  T.ok(require("sim.agents.premise").named(W, "wildfire"), "the planet's fire was set beside the village")
  local s = V.settlements[4]
  local people = require("sim.agents.people")
  local orig = people.remember
  local saw = 0
  people.remember = function(p, t, text, ...)
    if type(text) == "string" and text:find("^Fire") then saw = saw + 1 end
    return orig(p, t, text, ...)
  end
  local t0 = W.hour
  for h = 1, 48 do world.advance(W, t0 + h) end
  people.remember = orig
  T.ok((s.acc.trees_burnt or 0) >= 5, (s.acc.trees_burnt or 0) .. " trees of the map burnt")
  T.ok(saw >= 1, saw .. " people saw it")
  return string.format("%d trees burnt on %s's map in 2 days; %d saw it", s.acc.trees_burnt or 0, s.name, saw)
end

return M
