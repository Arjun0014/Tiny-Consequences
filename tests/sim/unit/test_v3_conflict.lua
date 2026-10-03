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
  -- (who lived at A when the hardship began: a household of theirs may move away and take from there)
  local of_A = {}
  for _, id in ipairs(A.members) do of_A[id] = true end
  local t0 = W.hour
  local trips = {}
  for h = 1, 24 * 8 do
    world.advance(W, t0 + h)
    for _, id in ipairs(A.members) do
      local p = V.people[id]
      if p and p.trip and p.trip.steal then trips[p.id .. ":" .. p.trip.t0] = p.trip end
    end
    local nt, last = 0, nil
    for _, d in ipairs(V.deeds or {}) do if d.kind == "take" then nt, last = nt + 1, d end end
    if nt >= 2 and W.hour - last.t > 30 then break end
  end
  local n = 0
  for _ in pairs(trips) do n = n + 1 end
  T.ok(n >= 1, "someone of the hungry village set out to take beasts (" .. n .. " trips)")
  local D = V.deeds or {}
  T.ok(#D >= 1, "a taking (or an attempt) happened: " .. #D .. " deeds")
  local stock = require("sim.agents.stock")
  local away, back, known = 0, 0, 0
  local takes = {}
  for _, d in ipairs(D) do if d.kind == "take" then takes[#takes + 1] = d end end
  D = takes
  for _, d in ipairs(D) do
    T.ok(of_A[d.by], "deed " .. d.id .. " is a taking by someone who lived at " .. A.name)
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

-- Injury (D-148 §5): one badly hurt keeps to bed at home, is tended by the household and heals; a
-- lighter hurt heals while they go about lighter work. Hurt is given here as a premise (a fall).
function M.hurt_keeps_to_bed_and_heals()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local s = V.settlements[1]
  local people = require("sim.agents.people")
  local bad, light
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    local hh = p and V.households[p.hh]
    if p and p.alive and people.stage(p, W.hour) == "adult" and hh and #hh.members >= 3 and hh.home ~= 0 then
      if not bad then bad = p elseif not light and p.hh ~= bad.hh then light = p end
    end
  end
  T.ok(bad and light, "two grown-ups of households with others at home")
  if not (bad and light) then return end
  people.hurt(bad, 0.7, "broken", nil, W.hour)
  people.hurt(light, 0.25, "bruise", nil, W.hour)
  local t0 = W.hour
  local mend, tended, worked_bad = 0, false, 0
  for h = 1, 24 * 10 do
    world.advance(W, t0 + h)
    if bad.objk == "mend" then mend = mend + 1 end
    if (bad.hurt or 0) >= 0.6 and (bad.objk == "fell" or bad.objk == "till" or bad.objk == "harvest" or bad.objk == "build") then worked_bad = worked_bad + 1 end
    if (bad.tended or -9) >= 0 then tended = true end
  end
  T.ok(mend > 0, "the badly hurt one kept to bed at home (" .. mend .. " hours)")
  T.ok(worked_bad == 0, "and did no heavy work while badly hurt")
  T.ok(tended, "someone of the household tended them")
  T.ok((bad.hurt or 0) < 0.7 and bad.alive, "healing (hurt now " .. string.format("%.2f", bad.hurt or 0) .. ")")
  T.ok((light.hurt or 0) == 0, "the light hurt healed")
  return string.format("%s: 0.70 -> %.2f in 10 days, %d hours in bed; %s healed", bad.name, bad.hurt or 0, mend, light.name)
end

-- A blow (R6c): a deed of wounding, the hurt given, both villages knowing; a blow that answered the
-- struck man's own taking weighs little in his people's grievance.
function M.a_blow_is_a_deed()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local A, B = V.settlements[4], V.settlements[2]
  local people = require("sim.agents.people")
  local conflict = require("sim.agents.conflict")
  local deeds = require("sim.agents.deeds")
  local function man(s)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and p.sex == "m" and people.stage(p, W.hour) == "adult" then return p end
    end
  end
  local taker, owner = man(A), man(B)
  local d = deeds.add(W, { kind = "take", at = B.id, by = taker.id, by_hh = taker.hh, by_s = A.id, to = 0, to_hh = owner.hh, to_s = B.id,
    what = "sheep", n = 1, fu = 7, harm = true, back = true })
  local sev, w = conflict.strike(W, B, owner, taker, d.id, W.hour)
  T.ok(w.kind == "wound" or w.kind == "kill", "a deed of wounding")
  T.ok((taker.hurt or 0) > 0, string.format("the struck man is hurt (%.2f)", taker.hurt or 0))
  T.ok(deeds.known(B, w.id) ~= nil, "the striker's village knows it")
  T.ok(deeds.known(A, w.id) == nil and taker.tell and #taker.tell == 1, "the struck man carries it home")
  conflict.tell_home(W, A, taker, W.hour + 20)
  T.ok(deeds.known(A, w.id) ~= nil, "his village knows it once he has told it")
  local g1 = deeds.grievance(W, A, B, taker.hh, W.hour + 20)
  -- (the same blow, had it not answered his own taking)
  w.answers = nil
  local g2 = deeds.grievance(W, A, B, taker.hh, W.hour + 20)
  T.ok(g1 < g2 * 0.5, string.format("deserved: it weighs %.1f, %.1f unprovoked", g1, g2))
  return string.format("%s struck %s: hurt %.2f; grievance %.1f (unprovoked %.1f)", owner.name, taker.name, sev, g1, g2)
end

-- Raids (R6e): men given away alone at a village with dogs raise a party at home; others join for
-- their own reasons (nobody is assigned); those who go gather and walk the road together, and are at
-- the other village together.
function M.men_given_away_alone_go_together()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local A, B = V.settlements[4], V.settlements[2]
  hardship(W, A, B)
  -- (they know of no other fold than Zurug's: what their people saw elsewhere is forgotten)
  for _, o in ipairs(V.settlements) do
    if o ~= B and A.know and A.know[o.id] then A.know[o.id].beasts = nil end
  end
  local people = require("sim.agents.people")
  local orig = people.remember
  local stayed = {}
  people.remember = function(p, t, text, ...)
    if type(text) == "string" and text:find("^Did not go with the others") then stayed[p.id] = text end
    return orig(p, t, text, ...)
  end
  local t0 = W.hour
  local party, members, went, there = nil, {}, {}, {}
  for h = 1, 24 * 12 do
    world.advance(W, t0 + h)
    local R = A.raid
    if R and #R.party >= 2 and not party then party = R end
    if party then
      for _, id in ipairs(party.party) do members[id] = true end
      for id in pairs(members) do
        local p = V.people[id]
        if p and p.trip and p.trip.band == party.id then
          went[id] = true
          if p.at == party.to and not there[id] then there[id] = W.hour end
        end
      end
    end
  end
  people.remember = orig
  T.ok(party ~= nil, "a party of two or more formed at " .. A.name)
  if not party then return end
  local nm, ng, n, first, last = 0, 0, 0, 1e18, -1e18
  for id in pairs(members) do
    nm = nm + 1
    if went[id] then ng = ng + 1 else T.ok(stayed[id] ~= nil, V.people[id].name .. " agreed and did not go: with a reason") end
  end
  for _, t in pairs(there) do n = n + 1; first = math.min(first, t); last = math.max(last, t) end
  T.ok(ng >= 1, "someone of the party went")
  T.ok(n < 2 or last - first < 2, string.format("those who went came together (%.1f h apart)", n >= 2 and last - first or 0))
  local leader = V.people[party.leader]
  local why = {}
  for id, t in pairs(stayed) do why[#why + 1] = V.people[id].name .. ": " .. t end
  return string.format("%s raised %d men for %s; %d went, %d reached it; stayed: %s", leader.name, nm, V.settlements[party.to].name, ng, n,
    #why > 0 and table.concat(why, "; ") or "none")
end

-- Ending (R6g): a household that took from a village it trades with, and knows that village knows who
-- did it, makes it good unasked once it is not in want: goods carried over, the rest owed, the wrong
-- settled (history premise: premise.wronged; the villages trade).
function M.a_known_wrong_is_made_good_unasked()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local A, B = V.settlements[2], V.settlements[4]
  local premise = require("sim.agents.premise")
  local econ = require("sim.agents.econ")
  premise.knows(W, A, B)
  local d = premise.wronged(W, A, B, 3, 6, "denied")
  local r1, r2 = econ.rel(A, B), econ.rel(B, A)
  r1.trust, r2.trust, r1.deals, r2.deals = 0.6, 0.6, 10, 10
  local vh = V.households[d.to_hh]
  local hb = A.buildings[vh.home]
  local food0 = hb.stock.food or 0
  local t0 = W.hour
  for h = 1, 24 * 8 do
    world.advance(W, t0 + h)
    if d.settled then break end
  end
  T.ok(d.settled ~= nil, "the wrong was made good")
  if not d.settled then return end
  local g = require("sim.agents.deeds").get(W, d.settled)
  T.ok(g and g.kind == "pay" and g.by_s == B.id, "by someone of " .. B.name)
  local owed = 0
  for _, x in ipairs(econ.debts(W)) do if x.cr == vh.id and x.dbs == B.id then owed = owed + x.fu end end
  return string.format("%s of %s made it good on day %.1f: %.1f FU carried, %.1f FU owed", (V.people[g.by] or {}).name or "?", B.name, g.t / 24, g.n or 0, owed)
end

-- Defence that is built (R6f): a village that lost many beasts at its fold lately (history: wolves'
-- kills) decides to wall it and its people build the wall stint by stint with logs from the woodstore.
function M.a_fold_is_walled_after_losses()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 72)
  local s = V.settlements[1]
  require("sim.agents.premise").wolf_losses(W, s, 9, 20)
  local pen = s.buildings[s.herds.flock.pen]
  local logs0 = s.wood_store.stock.logs or 0
  local t0 = W.hour
  local decided
  for h = 1, 24 * 12 do
    world.advance(W, t0 + h)
    if pen.wall_want and not decided then decided = W.hour end
    if (pen.wall or 0) >= 1 then break end
  end
  T.ok(decided ~= nil or (pen.wall or 0) >= 1, "the village decided to wall its fold")
  T.ok((pen.wall or 0) >= 1, string.format("the wall stands (%.2f built, %d logs brought)", pen.wall or 0, pen.wall_logs or 0))
  T.ok((pen.wall_logs or 0) >= require("sim.config").V3.wall_logs, "its stakes were carried there")
  return string.format("%s walled its fold: decided day %.1f, standing day %.1f, %d logs", s.name, (decided or 0) / 24, W.hour / 24, pen.wall_logs or 0)
end

-- (S20) Fear of a place: a household whose man was struck down by a man of another village weighs it
-- as hardship (its neighbours a little), is drawn less toward the killers' village, and if it leaves
-- it does not go there.
function M.blood_makes_a_household_fear_its_place()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 24)
  local s, o = V.settlements[2], V.settlements[4]
  local migrate = require("sim.agents.migrate")
  local now = V.now or W.hour
  local hh, nb
  for _, hid in ipairs(s.households) do
    local h = V.households[hid]
    local man = false
    for _, id in ipairs(h.members) do
      local q = V.people[id]
      if q.alive and q.sex == "m" and require("sim.agents.people").stage(q, now) == "adult" then man = true end
    end
    if man and not hh then hh = h elseif not nb then nb = h end
  end
  local p0 = migrate.push(W, s, hh, now)
  local n0 = migrate.push(W, s, nb, now)
  local u0 = migrate.pull(W, s, hh, o, now)
  local victim, by = require("sim.agents.premise").blood(W, s, hh, o, 1)
  T.ok(victim ~= nil, "a man struck down")
  now = V.now or W.hour
  local p1, why = migrate.push(W, s, hh, now)
  local n1 = migrate.push(W, s, nb, now)
  local u1 = migrate.pull(W, s, hh, o, now)
  T.ok(p1 >= p0 + 0.3, string.format("the household's hardship %.2f -> %.2f (%s)", p0, p1, tostring(why)))
  T.ok(n1 > n0 and n1 - n0 < p1 - p0, string.format("a neighbour's %.2f -> %.2f", n0, n1))
  T.ok(u1 < u0, string.format("%s draws them %.2f -> %.2f", o.name, u0, u1))
  local t0 = W.hour
  for d = 1, 12 do world.advance(W, t0 + d * 24) end
  local mg = hh.migrate
  T.ok(not mg or mg.to ~= o.id, "not to the killers' village")
  return string.format("%s killed by %s of %s: push %.2f -> %.2f (%s); neighbour %.2f -> %.2f; %s draws %.2f -> %.2f; %s",
    victim.name, by.name, o.name, p0, p1, tostring(why), n0, n1, o.name, u0, u1,
    mg and ("left for " .. V.settlements[mg.to].name) or "stayed")
end

return M
