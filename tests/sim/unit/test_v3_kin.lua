-- Kin across villages stand by each other (S21, R6): the premise is a daughter of Krikeivorn married
-- away to Zurug, her parents' house burnt down, and the news of it reaching her. Whether she (or
-- someone of her household) goes, what she brings and how long she stays are the people's own.
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

function M.kin_walk_over_to_help_raise_a_burnt_house()
  local W = shipped()
  local V = W.S.v3
  local premise = require("sim.agents.premise")
  local deeds = require("sim.agents.deeds")
  local A, B = V.settlements[6], V.settlements[2]   -- (Krikeivorn and Zurug: 21 hours apart by road, within courting reach)
  world.advance(W, W.hour + 2)
  local bride = premise.married_away(W, A, B)
  T.ok(bride ~= nil, "a daughter of " .. A.name .. " living at " .. B.name)
  if not bride then return end
  local mother = V.people[bride.mother]
  local hh = V.households[mother.hh]
  local house = A.buildings[hh.home]
  premise.house_burnt(W, A, house)
  -- (the news reaches her village: a traveller tells it)
  local d
  for _, x in ipairs(V.deeds) do if x.kind == "burnt" and x.to_hh == hh.id then d = x end end
  T.ok(d ~= nil, "the burnt deed")
  deeds.learn(W, B, d, "told by a traveller from " .. A.name, 0, 0, W.hour)
  local t0 = W.hour
  local went, helped_h, came_home, deed
  for h = 1, 24 * 8 do
    world.advance(W, t0 + h)
    for _, x in ipairs(V.deeds) do if x.kind == "help" and x.to_hh == hh.id then deed = x end end
    for _, id in ipairs(B.members) do
      local q = V.people[id]
      if q and q.trip and q.trip.help == hh.id then went = went or q end
    end
    if went and not went.trip and went.sett == B.id and not went.at and deed then came_home = true; break end
  end
  T.ok(went ~= nil, "someone of " .. B.name .. " set out to help")
  if not went then return end
  T.ok(deed ~= nil, "a deed of help at " .. A.name)
  helped_h = A.acc.kin_help_h or 0
  T.ok(helped_h > 0, string.format("%.1f hours of work at their house site", helped_h))
  local remembered = 0
  for _, id in ipairs(hh.members) do
    for _, m in ipairs(V.people[id].mem or {}) do
      local txt = type(m) == "table" and (m.text or m[2]) or m
      if type(txt) == "string" and txt:find("to help us raise our house") then remembered = remembered + 1 end
    end
  end
  T.ok(remembered >= 1, remembered .. " of the household remember who came")
  return string.format("%s of %s went to help (%s); %.1f hours at the site; %d remember it; home again: %s",
    went.name, B.name, went == bride and "the daughter" or "of her household", helped_h, remembered, tostring(came_home))
end

return M
