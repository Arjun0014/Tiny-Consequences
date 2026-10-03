-- R6 conflict (S20, D-149, docs/v3/60_R6_CONFLICT.md): taking what is not one's own is a person's
-- choice for their household, made from real hardship and what their people saw of another village,
-- walked, done by night, noticed or not, remembered as a deed known to those who saw or found it.
-- Premises only (sim/agents/premise.lua): a village whose harvest failed, whose beasts died and whose
-- stores are empty, whose people have been to a neighbour. Nothing is told to steal.
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

local function hardship(W, A, B)
  local premise = require("sim.agents.premise")
  premise.fail_harvest(W, A)
  premise.murrain(W, A, 0)
  premise.empty_stores(W, A)
  premise.knows(W, A, B)
end

-- A starving village's people weigh taking beasts from a village they know; whoever goes walks
-- there, waits for the dark, takes beasts from the pen, and either leads them home or is given away
-- and lets them go. Every beast taken is where its deed says.
function M.a_hungry_household_takes_a_beast()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 25)
  local A = V.settlements[4]   -- Hottoodingle Hop
  local B = V.settlements[2]   -- Zurug, 18 h by road
  hardship(W, A, B)
  local t0 = W.hour
  local trips = {}
  for h = 1, 24 * 8 do
    world.advance(W, t0 + h)
    for _, id in ipairs(A.members) do
      local p = V.people[id]
      if p and p.trip and p.trip.steal then trips[p.id .. ":" .. p.trip.t0] = p.trip end
    end
    if #(V.deeds or {}) >= 2 and W.hour - V.deeds[#V.deeds].t > 30 then break end
  end
  local n = 0
  for _ in pairs(trips) do n = n + 1 end
  T.ok(n >= 1, "someone of the hungry village set out to take beasts (" .. n .. " trips)")
  local D = V.deeds or {}
  T.ok(#D >= 1, "a taking (or an attempt) happened: " .. #D .. " deeds")
  local stock = require("sim.agents.stock")
  local away, back, known = 0, 0, 0
  for _, d in ipairs(D) do
    T.ok(d.kind == "take" and d.by_s == A.id, "deed " .. d.id .. " is a taking by someone of " .. A.name)
    local victim = V.settlements[d.to_s]
    -- every beast of the deed is where the deed says
    for _, s in ipairs(V.settlements) do
      for _, kind in ipairs(stock.HERDS) do
        local H = s.herds and s.herds[kind]
        for _, a in ipairs(H and H.animals or {}) do
          if a.taken == d.id and a.alive and (a.led or 0) == 0 then
            if d.away then T.ok(s.id == d.by_s and a.owner == d.by_hh, "a beast taken away is in the taker's herd")
            else T.ok(s.id == d.to_s and a.owner ~= d.by_hh, "a beast let go is back with its owners") end
          end
        end
      end
    end
    if d.away then away = away + 1 elseif d.back then back = back + 1 end
    if W.hour - d.t > 30 and (d.away or d.barked) then
      T.ok(require("sim.agents.deeds").known(victim, d.id) ~= nil, "the robbed village knows of deed " .. d.id)
      known = known + 1
    end
  end
  return string.format("%d trips; %d deeds: %d got away with, %d given away; %d found out by the owners", n, #D, away, back, known)
end

-- Restraint: a village is not robbed by a household with close kin living there (the same hardship,
-- the same knowledge; only the kin differ).
function M.kin_are_not_robbed()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 25)
  local A, B = V.settlements[4], V.settlements[2]
  hardship(W, A, B)
  local conflict = require("sim.agents.conflict")
  local people = require("sim.agents.people")
  local best, who
  for _, id in ipairs(A.members) do
    local p = V.people[id]
    if p and p.alive and people.stage(p, W.hour) == "adult" then
      local b = conflict.take_weigh(W, A, p, V.households[p.hh], W.hour)
      if b and (not best or b.net > best.net) then best, who = b, p end
    end
  end
  T.ok(best ~= nil, "without kin there, someone weighs taking beasts")
  if not best then return end
  local O = V.settlements[best.o]
  -- (his mother lives at O: married there - the premise makes her so)
  local hh = V.households[who.hh]
  local mum = V.people[O.members[1]]
  who.mother = mum.id
  local b2 = conflict.take_weigh(W, A, who, hh, W.hour + 24)
  T.ok(not b2 or b2.o ~= O.id or b2.net < best.net - 1, "with his mother living there he does not weigh it the same")
  return string.format("%s weighed %s at %.2f; with his mother there: %s", who.name, O.name, best.net,
    b2 and (b2.o == O.id and string.format("%.2f", b2.net) or ("another village, " .. V.settlements[b2.o].name)) or "nothing")
end

-- Taking back: a household wronged by a village that would not make it good (history: premise.wronged)
-- weighs taking beasts from that village although it is not hungry; one with the same history whose
-- wrong was made good does not.
function M.a_wrong_not_made_good_is_answered()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local A, B = V.settlements[2], V.settlements[4]   -- Zurug wronged by Hottoodingle Hop
  local premise = require("sim.agents.premise")
  premise.knows(W, A, B)
  local d = premise.wronged(W, A, B, 3, 6, "denied")
  local conflict = require("sim.agents.conflict")
  local deeds = require("sim.agents.deeds")
  local people = require("sim.agents.people")
  local hh = V.households[d.to_hh]
  local best, who
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive and people.stage(p, W.hour) == "adult" then
      local b = conflict.take_weigh(W, A, p, hh, W.hour)
      if b and b.o == B.id and (not best or b.net > best.net) then best, who = b, p end
    end
  end
  T.ok(best ~= nil, "the wronged household weighs taking back from " .. B.name)
  if not best then return end
  -- (the same, but the wrong was made good)
  local e = deeds.known(A, d.id)
  deeds.settle(W, d, { id = -1 })
  local b2 = conflict.take_weigh(W, A, who, hh, W.hour + 24)
  T.ok(not b2 or b2.o ~= B.id, "once made good it is not answered")
  return string.format("%s weighed taking back from %s at %.2f: %s", who.name, B.name, best.net, best.why)
end

return M
