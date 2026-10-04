-- v3 agent world (docs/v3/10_AGENTS.md, D-078): the slice's guarantees, on one generated world
-- (seed 22, generated once per run and shared by the tests).
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")
local people = require("sim.agents.people")
local clock = require("sim.agents.clock")
local nav = require("sim.agents.nav")

local M = {}
local BASE

local function fresh()
  if not BASE then
    local W = require("sim.agents.worldgen").generate(22, {})
    BASE = worlds.year0_save_string(W, { seed = 22 })
  end
  local W = assert(worlds.from_save_string(BASE))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- Time is continuous: advancing in odd slices gives exactly the state of one big advance.
function M.slicing_is_deterministic()
  local A, B = fresh(), fresh()
  world.advance(A, 30)
  local t = 0
  while t < 30 do
    t = math.min(30, t + 0.137)
    world.advance(B, t)
  end
  T.eq(world.state_hash(A, { "S" }), world.state_hash(B, { "S" }), "sliced and unsliced worlds differ")
end

-- A restored save continues exactly as the live world (aliases re-linked, paths re-cached).
function M.save_round_trip()
  local A = fresh()
  local str = worlds.year0_save_string(A, { seed = 22 })
  local B = assert(worlds.from_save_string(str))
  world.set_systems(require("sim.agents.worldgen").systems())
  world.advance(A, 40)
  world.advance(B, 40)
  T.eq(world.state_hash(A), world.state_hash(B), "restored world diverged")
end

-- Four days of village life: nobody starves, people wake at different times, work is done in
-- the work itself (rows hoed and sown, trees felled, logs carried), the waiting couple's house
-- rises, and every path was found.
function M.four_days()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local n0 = #s.members
  nav.stats.fallbacks = 0
  -- wake times: the first step that is not sleep after each night, per person
  local act = require("sim.agents.act")
  local orig = act.start
  local woke = {}
  local was = {}
  act.start = function(W_, s_, p, now)
    local ok = orig(W_, s_, p, now)
    if was[p.id] == "sleep" and p.act ~= "sleep" then
      woke[#woke + 1] = clock.local_hour(now, W.grid.lon[s.cell])
    end
    was[p.id] = p.act
    return ok
  end
  world.advance(W, 4 * 24)
  act.start = orig
  local alive = 0
  for _, id in ipairs(s.members) do if V.people[id].alive then alive = alive + 1 end end
  T.ok(alive >= n0 - 1, "people died: " .. (n0 - alive))
  T.ok((s.acc.deaths or 0) <= 1, "deaths " .. tostring(s.acc.deaths))
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    T.ok(p.health > 0.5, p.name .. " health " .. p.health)
  end
  table.sort(woke)
  T.ok(#woke >= 40, "wakings " .. #woke)
  T.ok(woke[#woke] - woke[1] >= 1.5, string.format("wake times spread only %.2f h", woke[#woke] - woke[1]))
  -- (D-100) rows sown as the village decided: its flax and the grain it wants (fields chosen to rest
  -- stay bare)
  local sown = 0
  for _, pl in ipairs(s.plots) do
    sown = sown + pl.sown
    T.ok(pl.crop == "flax" or pl.sow_ok ~= false or pl.stage ~= "fallow" or pl.sown == 0, pl.name .. " sown though chosen to rest")
  end
  T.ok(sown >= 70, "rows sown " .. sown)
  T.ok((s.acc.felled or 0) >= 4, "trees felled " .. tostring(s.acc.felled))
  -- the waiting couple's house rises (logs carried, hours worked) and stands within 12 days
  -- (a house is ~110 work-hours, D-080)
  local site
  for _, bid in ipairs(s.blist) do
    local b = s.buildings[bid]
    if b.kind == "house" and b.for_a then site = b end
  end
  T.ok(site ~= nil, "no house site for the waiting couple")
  T.ok(site.state == "standing" or (site.logs_have >= 4 and site.work_done > 10),
    string.format("the couple's house barely begun: logs %d, work %.0f h", site.logs_have, site.work_done))
  world.advance(W, 12 * 24)
  T.ok((s.acc.built or 0) >= 1, "no house built for the waiting couple in 12 days")
  T.eq(nav.stats.fallbacks, 0, "unreachable paths")
end

-- Families: children have parents, couples are partners both ways, infants have a carer and are
-- never drawn walking on their own.
function M.families_and_stages()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local kids, infants = 0, 0
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.partner ~= 0 then T.eq(V.people[p.partner].partner, p.id, "partner not mutual") end
    local st = people.stage(p, W.hour)
    if st == "child" or st == "infant" then
      kids = kids + 1
      T.ok(p.mother ~= 0 or p.father ~= 0, p.name .. " has no parent")
    end
    if st == "infant" then infants = infants + 1; T.ok((p.carer or 0) ~= 0, "infant without carer") end
  end
  T.ok(kids >= 8, "children " .. kids)
  T.ok(infants >= 1, "infants " .. infants)
  world.advance(W, 12)
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    local st = people.stage(p, W.hour)
    if st == "infant" then T.eq(p.act, "carried", "an infant acting alone") end
    if st == "child" then
      T.ok(p.act ~= "till" and p.act ~= "chop" and p.act ~= "build" and p.act ~= "harvest", "a child at " .. p.act)
    end
  end
end

-- Positions are continuous: no step moves a person further than their speed allows.
-- (S23) A toddler weakened by hungry nights gets its strength back once fed, as a grown-up does: it never did - every
-- hungry night left its mark for good, and a toddler fed that morning died "at the edge" in year 7.
function M.a_fed_toddler_recovers()
  local W = fresh()
  local V = W.S.v3
  local now = W.hour
  local ib
  for id = 1, #V.people do
    local p = V.people[id]
    if not ib and p and p.alive and people.stage(p, now) == "infant" and (p.carer or 0) ~= 0 then ib = p end
  end
  T.ok(ib ~= nil, "an infant")
  if not ib then return end
  -- weaned (its mother no longer nursing it), weakened, and fed just now
  local m = V.people[ib.mother or 0]
  if m then m.nursing = 0 end
  ib.health, ib.hunger, ib.diet_cap = 0.3, 0.1, nil
  world.advance(W, now + 24)
  T.ok(ib.alive, ib.name .. " alive")
  T.ok(ib.health > 0.33, string.format("%s's health %.2f a day later (0.30 when fed)", ib.name, ib.health))
  return string.format("%s: health 0.30 -> %.2f in a day", ib.name, ib.health)
end

-- (S23) A woman whose time is near does not set out on a journey (a crier about to give birth walked 20 hours to
-- a neighbour and her newborn starved there), nor one with a baby at the breast; and a baby in its nursing mother's
-- arms on a trip is fed where she is, as on the road (a trip's plan has no hour for nursing).
function M.near_term_and_nursing_mothers_on_journeys()
  local W = fresh()
  local V = W.S.v3
  world.advance(W, W.hour + 30)   -- (the villages' food reckoned)
  local now = V.now or W.hour
  local think = require("sim.agents.think")
  local kin = require("sim.agents.kin")
  local act = require("sim.agents.act")
  local m, s, ctx
  for id = 1, #V.people do
    local p = V.people[id]
    if not m and p and p.alive and p.sex == "f" and people.stage(p, now) == "adult" and (p.nursing or 0) <= now and (p.preg_due or 0) == 0 then
      local hh = V.households[p.hh]
      local o = V.settlements[p.sett]
      local c = hh and hh.home ~= 0 and { home = o.buildings[hh.home], hh = hh }
      p.hunger = 0.2
      if c and think.journey_ok(W, o, p, c, 20) then m, s, ctx = p, o, c end
    end
  end
  T.ok(m ~= nil, "a woman free to make a journey")
  if not m then return end
  local hh = ctx.hh
  local free = true
  m.preg_due = now + 30
  T.ok(not think.journey_ok(W, s, m, ctx, 20), "due in 30 hours: no 20-hour journey")
  m.preg_due = now + 2000
  T.eq(think.journey_ok(W, s, m, ctx, 20), free, "due in 80 days: as anyone else")
  m.preg_due = 0
  m.nursing = now + 500
  T.ok(not think.journey_ok(W, s, m, ctx, 20), "a baby at the breast: no journey")
  -- a hungry baby in her arms while she is away on a trip, doing something other than walking
  local ib = people.new(W, { born = now - 24, mother = m.id, father = 0, hh = 0, sett = s.id, surname = m.surname,
    culture = s.culture, x = m.x, y = m.y })
  people.join(W, ib, hh)
  kin.give(W, ib, m)
  m.trip, m.act = { to = s.id }, "news"
  ib.hunger, ib.called = 0.7, nil
  kin.infant_tick(W, s, ib, now, 0.1, function() end, act.pos)
  T.ok(ib.hunger < 0.5, string.format("fed at the breast on the trip (hunger %.2f)", ib.hunger))
  m.trip = nil
  return string.format("free to go when not due: %s; fed on the trip: %.2f", tostring(free), ib.hunger)
end

function M.no_teleports()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local act = require("sim.agents.act")
  local last = {}
  local worst = 0
  for k = 1, 600 do
    local t = k * 0.05
    world.advance(W, t)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p.alive and p.act ~= "carried" then
        local x, y = act.pos(p, t)
        local l = last[id]
        if l then
          local dx, dy = x - l[1], y - l[2]
          local d = math.sqrt(dx * dx + dy * dy)
          -- 0.05 h at up to ~5 units/h, plus the one-tile un-stick step
          if d > worst then worst = d end
        end
        last[id] = { x, y }
      end
    end
  end
  T.ok(worst < 0.3, string.format("a person jumped %.3f units in 3 game minutes", worst))
end


-- Wild animals on the maps (D-093): the shipped world's six settlements over six days. They are
-- the members of real herds and packs (never more on a map than their herd holds), stand on dry
-- land, come in from the edges; wolves come at night and what they kill is a real animal (a wild
-- one taken off its herd's count); hunters kill real animals and carry them home (D-094).
function M.wild_animals_belong_to_their_herds()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local wild = require("sim.agents.wild")
  local fauna = require("sim.systems.fauna")
  local mapm = require("sim.agents.map")
  local A = W.S.wild
  local t0 = W.hour
  local seen, bad, over, visits, kills = 0, 0, 0, 0, 0
  local n0 = {}
  for i = 1, A.nh do n0[A.h_id[i]] = A.h_n[i] end
  for h = 6, 6 * 24, 6 do
    world.advance(W, t0 + h)
    for _, s in ipairs(W.S.v3.settlements) do
      local per = {}
      wild.each(s, function(w)
        seen = seen + 1
        local i = mapm.tile_of(s, w.tx, w.ty)
        if i == 0 or s.land[i] ~= 1 or s.occ[i] ~= 0 then
          bad = bad + 1
          if bad <= 5 then print(string.format("    bad: %s %s act %s at %.3f,%.3f tile %d land %s occ %s", s.name, w.sp, w.act, w.tx, w.ty, i, tostring(s.land[i]), tostring(s.occ[i]))) end
        end
        if not w.pred then per[w.gid] = (per[w.gid] or 0) + 1 end
      end)
      for gid, n in pairs(per) do
        local i = fauna.find_herd(W, gid)
        if i and n > A.h_n[i] + 1 then over = over + 1 end
      end
    end
  end
  local hunted, meat, spears = 0, 0, 0
  for _, s in ipairs(W.S.v3.settlements) do
    visits = visits + (s.acc.wolf_nights or 0)
    kills = kills + (s.acc.wild_killed_wolves or 0) + (s.acc.stock_wolves or 0)
    hunted = hunted + (s.acc.wild_hunted or 0)
    meat = meat + (s.acc.hunt_meat or 0)
    spears = spears + (s.acc.spears or 0)
  end
  print(string.format("      hunted %d of %d spears (meat %.0f), samples %d, wolf nights %d, kills by wolves %d", hunted, spears, meat, seen, visits, kills))
  -- (D-094) hunters kill real animals and carry them home. (S23, D-179: kills in six days are a
  -- small number - about 0.4 a day over the six villages, so "two or more" failed a third of the
  -- time under any change of the world's course; the hunters' stalking to a throw is the count)
  T.ok(spears >= 6, "spears thrown at wild animals " .. spears)
  T.ok(hunted >= 1, "animals hunted " .. hunted)
  T.ok(meat > 10, "hunted meat brought home " .. meat)
  T.ok(seen > 500, "wild animal-samples on the maps " .. seen)
  T.eq(bad, 0, "wild animals on water or in a building")
  T.eq(over, 0, "more animals of a herd on a map than the herd holds")
  T.ok(visits >= 3, "wolf nights " .. visits)
  T.ok(kills >= 1, "kills by wolves " .. kills)
  -- (D-105) every household with beasts keeps a dog, on dry open ground; dogs hear the wolves
  local dogs, wet, barks = 0, 0, 0
  for _, s in ipairs(W.S.v3.settlements) do
    barks = barks + (s.acc.barks or 0)
    for _, d in ipairs(s.dogs or {}) do
      if d.alive then
        dogs = dogs + 1
        local i = require("sim.agents.map").tile_of(s, d.tx, d.ty)
        if i == 0 or s.land[i] ~= 1 or s.riv[i] > 0 then wet = wet + 1 end
      end
    end
  end
  T.ok(dogs >= 12, "dogs " .. dogs)
  T.eq(wet, 0, "dogs on water")
  -- (S15: dogs barking at wolves at the pens is tested where it is caused, test_v3_animals
  -- dogs_bark_at_hungry_wolves: with wild prey plentiful, wolves need not come near a pen in six days)
end


-- Trade answers real want (D-097, D-101, D-102): a failed harvest and a bumper catch at Upper
-- Linamist (grain short, smoked fish to spare) bring its neighbours' grain by sea for its fish -
-- carried to the boats a load at a time, sailed, carried to the buyers - with both villages' books
-- equal, news of each other and trust grown; nobody starves on the way.
function M.trade_answers_real_want()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  -- (the village with the nearest sea partner: geography, not a settlement number - S14 regenerated
  -- the shipped world with drainage basins)
  local trade = require("sim.agents.trade")
  local UL, bl = nil, 1e9
  for _, s in ipairs(V.settlements) do
    local o = trade.sea_partners(W, s)[1]
    if o and trade.sea_route(W, s, o).len < bl then UL, bl = s, trade.sea_route(W, s, o).len end
  end
  T.ok(UL ~= nil, "a village with a sea partner")
  UL.granary.stock.grain = 120
  for _, id in ipairs(UL.blist) do
    local b = UL.buildings[id]
    if b.kind == "house" and b.hh ~= 0 then b.stock.smoked = (b.stock.smoked or 0) + 60; b.stock.food = 2 end
  end
  local t0 = W.hour
  local sea, carried = 0, 0
  for h = 1, 24 * 8 do
    world.advance(W, t0 + h)
    for _, s in ipairs(V.settlements) do
      for _, id in ipairs(s.members) do
        local p = V.people[id]
        if p.alive and p.act == "voyage" then sea = sea + 1 end
        if p.alive and p.carry == "pack" and p.trip and p.trip.sea then carried = carried + 1 end
      end
    end
  end
  local deals, exp, imp = 0, 0, 0
  for _, s in ipairs(V.settlements) do
    deals = deals + (s.acc.deals or 0)
    exp = exp + (s.acc.export or 0)
    imp = imp + (s.acc.import or 0)
  end
  T.ok(sea >= 4, "hours of boats on a voyage " .. sea)
  T.ok(carried >= 4, "hours of cargo carried to and from boats " .. carried)
  T.ok((UL.acc.got_grain or 0) + (UL.acc.out_grain or 0) + (UL.acc.in_grain or 0) > 10,
    string.format("grain reached %s: bought %.1f, sold there %.1f", UL.name, UL.acc.got_grain or 0, UL.acc.in_grain or 0))
  T.ok(deals >= 2, "deals " .. deals)
  T.ok(math.abs(exp - imp) < 1e-6, string.format("exports %.3f against imports %.3f", exp, imp))
  -- news and trust: Upper Linamist heard how its partners stand, and trusts one it dealt with more
  local best = 0
  for id, r in pairs(UL.rel or {}) do if r.deals > 0 and r.trust > best then best = r.trust end end
  T.ok(best > 0.2, "trust after deals " .. best)
  for _, id in ipairs(V.order) do
    local p = V.people[id]
    T.ok(p.alive or p.died_of ~= "hunger", p.name .. " starved")
  end
end

-- Relief (D-103): a village with no food and nothing to sell sends someone to ask a neighbour with
-- grain to spare; the neighbour gives, the grain is carried home to the granary, both remember it.
function M.hungry_village_asks_for_help()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  -- (a village without a dock and its nearest neighbour by road: S14)
  local trade = require("sim.agents.trade")
  local H, Z, bh = nil, nil, 1e9
  for _, s in ipairs(V.settlements) do
    if not s.dock then
      for _, o in ipairs(trade.partners(W, s)) do
        local _, hrs = trade.road(W, s, o)
        if hrs < bh then H, Z, bh = s, o, hrs end
      end
    end
  end
  T.ok(H ~= nil, "a village with a road neighbour")
  H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 10, 0, 0
  for _, pl in ipairs(H.plots) do
    pl.sheaves = 0
    if pl.stage == "growing" or pl.stage == "ripe" then
      for r = 1, pl.rows do pl.row_st[r] = 0 end
      pl.sown, pl.stage = 0, "fallow"
    end
  end
  for _, id in ipairs(H.blist) do
    local b = H.buildings[id]
    if b.kind == "house" and b.hh ~= 0 then
      b.stock.smoked, b.stock.food, b.stock.cheese, b.stock.meat, b.stock.fish, b.stock.meals = 0, 1, 0, 0, 0, 1
    end
  end
  Z.granary.stock.grain = Z.granary.stock.grain + 1500
  local t0 = W.hour
  for h = 1, 24 * 4 do world.advance(W, t0 + h) end
  T.ok((H.acc.relief_in or 0) >= 10, "relief carried to " .. H.name .. " " .. tostring(H.acc.relief_in))
  T.ok((Z.acc.relief_out or 0) >= 10, "relief given by " .. Z.name .. " " .. tostring(Z.acc.relief_out))
  local r = require("sim.agents.econ").rel(H, Z)
  T.ok(r.gifts >= 1 and (r.owe_them or 0) > 0, "help remembered: gifts " .. r.gifts .. ", owed " .. tostring(r.owe_them))
end

-- Relief by sea (S16, R4): a coastal village with no road to anyone runs out of food; one of its
-- boats sails to the trusted village over the water that has grain to spare, the grain is carried
-- from that granary to the boat a load at a time, sailed home and carried into the village's own
-- granary - the same grain, every FU accounted for.
function M.relief_comes_by_sea()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  -- (a village with boats and no road neighbour, and its nearest neighbour by sea)
  local H, Z, bh = nil, nil, 1e9
  for _, s in ipairs(V.settlements) do
    if s.dock and #trade.partners(W, s) == 0 then
      for _, o in ipairs(trade.sea_partners(W, s)) do
        local hrs = econ.sea_route(W, s, o)
        if o.granary and o.dock and hrs < bh then H, Z, bh = s, o, hrs end
      end
    end
  end
  T.ok(H ~= nil, "a village reached only by sea")
  if not H then return end
  H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 10, 0, 0
  for _, pl in ipairs(H.plots) do
    pl.sheaves = 0
    if pl.stage == "growing" or pl.stage == "ripe" then
      for r = 1, pl.rows do pl.row_st[r] = 0 end
      pl.sown, pl.stage = 0, "fallow"
    end
  end
  for _, id in ipairs(H.blist) do
    local b = H.buildings[id]
    if b.kind == "house" and b.hh ~= 0 then
      b.stock.smoked, b.stock.food, b.stock.cheese, b.stock.meat, b.stock.fish, b.stock.meals = 0, 1, 0, 0, 0, 1
    end
  end
  Z.granary.stock.grain = Z.granary.stock.grain + 1500
  econ.learn(W, H, Z, W.hour)
  local t0 = W.hour
  local sailed, loaded = false, 0
  for h = 0.25, 24 * 5, 0.25 do
    world.advance(W, t0 + h)
    for _, id in ipairs(H.members) do
      local p = V.people[id]
      if p.alive and p.trip and p.trip.ask and p.trip.sea then
        sailed = true
        local boat = H.boats[p.trip.boat]
        if boat and boat.cargo then loaded = math.max(loaded, boat.cargo.grain or 0) end
      end
    end
  end
  T.ok(sailed, "a boat of " .. H.name .. " sailed to ask " .. Z.name .. " for help")
  T.ok(loaded >= 10, string.format("the gift carried aboard: %.1f FU of grain in the hold", loaded))
  T.ok((H.acc.relief_in or 0) >= 10, "relief carried to " .. H.name .. " " .. tostring(H.acc.relief_in))
  T.ok((Z.acc.relief_out or 0) >= 10, "relief given by " .. Z.name .. " " .. tostring(Z.acc.relief_out))
  T.ok((H.granary.stock.grain or 0) > 10 or (H.acc.relief_in or 0) >= 10, "the granary got the grain")
  local r = econ.rel(H, Z)
  T.ok(r.gifts >= 1, "help remembered: gifts " .. r.gifts)
end

-- Migration (S16, R5): a village whose food has run out for days (its fields failed, nothing in
-- the stores) and a road neighbour heard to have grain to spare. Households weigh leaving, and
-- some go: the whole household packs, gathers at the gate, walks the road together with its beasts
-- led, and becomes the other village's - lodging with a household there, a house planned for it.
function M.a_hungry_household_moves_away()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  local H, Z, bh = nil, nil, 1e9
  for _, s in ipairs(V.settlements) do
    for _, o in ipairs(trade.partners(W, s)) do
      local _, hrs = trade.road(W, s, o)
      if hrs < bh then H, Z, bh = s, o, hrs end
    end
  end
  T.ok(H ~= nil, "two villages joined by a road")
  if not H then return end
  local function starve()
    H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 0, 0, 0
    for _, id in ipairs(H.blist) do
      local b = H.buildings[id]
      if b.kind == "house" and b.hh ~= 0 then
        b.stock.smoked, b.stock.food, b.stock.cheese, b.stock.meat, b.stock.fish = 0, math.min(b.stock.food or 0, 1), 0, 0, 0
      end
    end
  end
  for _, pl in ipairs(H.plots) do
    pl.sheaves = 0
    if pl.stage == "growing" or pl.stage == "ripe" then
      for r = 1, pl.rows do pl.row_st[r] = 0 end
      pl.sown, pl.stage = 0, "fallow"
    end
  end
  starve()
  -- (S19: a hungry household eats its own beasts before it thinks of leaving: a murrain took them)
  require("sim.agents.premise").murrain(W, H, 0)
  Z.granary.stock.grain = Z.granary.stock.grain + 3000
  econ.learn(W, H, Z, W.hour)
  local t0 = W.hour
  local decided, moved, walked = nil, nil, 0
  for h = 0.5, 24 * 9, 0.5 do
    world.advance(W, t0 + h)
    -- (the fields failed and the stores are empty: what little comes in is eaten at once)
    if h % 24 == 0 then starve() end
    for _, hid in ipairs(H.households) do
      local hh = V.households[hid]
      if hh and hh.migrate and not decided then decided = hh end
    end
    if decided then
      for _, id in ipairs(decided.members) do
        local p = V.people[id]
        if p.alive and p.act == "travel" then walked = walked + 1 end
      end
      if decided.migrate.arrived and not moved then moved = h end
    end
  end
  T.ok(decided ~= nil, "a household of " .. H.name .. " decided to leave")
  if not decided then return end
  T.ok(decided.migrate.why and decided.migrate.why:find(Z.name) ~= nil, "it knows why and where: " .. tostring(decided.migrate.why))
  T.ok(walked > 0, "its people walked the road")
  T.ok(moved ~= nil, "it reached " .. Z.name)
  T.eq(decided.sett, Z.id, "the household is " .. Z.name .. "'s now")
  local here = 0
  for _, id in ipairs(decided.members) do
    local p = V.people[id]
    if p.alive and p.sett == Z.id then here = here + 1 end
    T.ok(not p.alive or p.sett == Z.id, p.name .. " came too")
  end
  T.ok(here >= 1, "its people live at " .. Z.name .. ": " .. here)
  T.ok(decided.lodge ~= nil or decided.home ~= 0 or decided.house_site ~= nil, "it lodges with a household there or has a house")
  local listed = false
  for _, hid in ipairs(Z.households) do if hid == decided.id then listed = true end end
  T.ok(listed, Z.name .. " counts the household among its own")
  -- (households that left: none stay counted at the old village)
  for _, hid in ipairs(H.households) do T.ok(hid ~= decided.id, "the household is no longer " .. H.name .. "'s") end
  -- its beasts went with it (those of kinds the other village keeps)
  for _, kind in ipairs(require("sim.agents.stock").HERDS) do
    local HH = H.herds and H.herds[kind]
    for _, a in ipairs(HH and HH.animals or {}) do
      T.ok(not (a.alive and a.owner == decided.id), "no beast of the household left behind at " .. H.name .. " (" .. kind .. ")")
    end
  end
end

-- Courting (S16, R4): a man with nobody to marry at home (in the shipped world, the only unwed
-- grown man of his village with no unwed woman there) walks to a neighbouring village he chose,
-- meets its people; a match is made with one of its unwed women, who packs and walks back with
-- him and becomes his village's, in his household; the couple waits for a house; the villages
-- trust each other more and each hears how the other stands.
function M.a_man_goes_courting()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  local people = require("sim.agents.people")
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  local now = W.hour
  -- a single grown man whose village has no unwed woman, and a road neighbour that has one
  local man, H, O
  for _, s in ipairs(V.settlements) do
    local wives, men = 0, {}
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      local st, a = people.stage(p, now)
      if p.alive and p.partner == 0 and st == "adult" then
        if p.sex == "f" and a >= 17 and a < 40 then wives = wives + 1 end
        if p.sex == "m" and a >= 19 and a < 40 then men[#men + 1] = p end
      end
    end
    if wives == 0 and #men > 0 then
      for _, o in ipairs(trade.partners(W, s)) do
        for _, id in ipairs(o.members) do
          local q = V.people[id]
          local st, a = people.stage(q, now)
          if q.alive and q.sex == "f" and q.partner == 0 and st == "adult" and a >= 17 and a < 40 and not man then man, H, O = men[1], s, o end
        end
      end
    end
  end
  T.ok(man ~= nil, "a man with nobody to marry at home, a neighbour with an unwed woman")
  if not man then return end
  -- (he has made up his mind where to go: life.court_village's choice, here the one with a woman)
  man.court = { to = O.id, t = now }
  local tr0 = econ.rel(H, O).trust
  local bride, home_again, walked = nil, false, false
  for h = 0.5, 24 * 10, 0.5 do
    world.advance(W, now + h)
    if man.trip and man.trip.court and man.act == "travel" then walked = true end
    if man.partner ~= 0 then bride = V.people[man.partner] end
    if bride and bride.sett == H.id and (man.at or man.sett) == H.id and not man.trip then home_again = true; break end
  end
  T.ok(walked, man.name .. " walked the road to " .. O.name)
  T.ok(bride ~= nil, man.name .. " married at " .. O.name)
  if not bride then return end
  T.ok(home_again, bride.name .. " came to live at " .. H.name .. " with " .. man.name)
  T.eq(bride.hh, man.hh, "she is of his household")
  local listed = false
  for _, id in ipairs(H.members) do if id == bride.id then listed = true end end
  T.ok(listed, H.name .. " counts her among its people")
  for _, id in ipairs(O.members) do T.ok(id ~= bride.id, O.name .. " no longer counts her") end
  local waiting = false
  for _, id in ipairs(H.couples) do if id == bride.id then waiting = true end end
  T.ok(waiting or man.house_site or bride.house_site or V.households[man.hh].home ~= 0, "the couple waits for a house of their own")
  T.ok(econ.rel(H, O).trust > tr0, string.format("the villages trust each other more: %.2f -> %.2f", tr0, econ.rel(H, O).trust))
  T.ok(people.is_kin(W, bride, bride.mother) or bride.mother == 0, "her kin are at " .. O.name)
  -- (S16) kin across villages visit each other after days apart: a walk, a meal, the news carried
  local kin_at_O = 0
  for _, id in ipairs({ bride.mother, bride.father }) do
    local q = V.people[id or 0]
    if q and q.alive and q.sett == O.id then kin_at_O = kin_at_O + 1 end
  end
  if kin_at_O == 0 then return end
  local k0 = (H.acc.kin_visits or 0) + (O.acc.kin_visits or 0)
  local seen, walker = false, nil
  local t1 = W.hour
  for h = 1, 24 * 16 do
    world.advance(W, t1 + h)
    for _, s2 in ipairs({ H, O }) do
      for _, id in ipairs(s2.members) do
        local q = V.people[id]
        if q.alive and q.trip and q.trip.visit and q.act == "travel" then seen, walker = true, q end
      end
    end
    if (H.acc.kin_visits or 0) + (O.acc.kin_visits or 0) > k0 then break end
  end
  T.ok(seen, "kin walked the road to visit")
  T.ok((H.acc.kin_visits or 0) + (O.acc.kin_visits or 0) > k0, "a visit was paid: " .. tostring(walker and walker.name))
  T.ok((O.know and O.know[H.id] and O.know[H.id].t or 0) > t1 or (H.know and H.know[O.id] and H.know[O.id].t or 0) > t1,
    "the news went with them")
  -- (S17) kin are asked for help: a premise at H - its fields failed and its stores ran out - and
  -- the bride (or anyone of H with kin at O) walks to them for food; they give what they can spare
  -- from their larder (and their village's granary when it has grain to spare), carried home
  for _, pl in ipairs(H.plots) do
    pl.sheaves = 0
    if pl.stage == "growing" or pl.stage == "ripe" then
      for r = 1, pl.rows do pl.row_st[r] = 0 end
      pl.sown, pl.stage = 0, "fallow"
    end
  end
  local function starve()
    H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 0, 0, 0
    for _, id in ipairs(H.blist) do
      local b = H.buildings[id]
      if b.kind == "house" and b.hh ~= 0 then b.stock.smoked, b.stock.food, b.stock.cheese, b.stock.meat, b.stock.fish, b.stock.meals = 0, 0, 0, 0, 0, 0 end
    end
  end
  starve()
  -- (S19: with beasts of their own to kill they would eat those first: a murrain took them)
  require("sim.agents.premise").murrain(W, H, 0)
  local imp0 = H.acc.import or 0
  local asked, asker = false, nil
  local t2 = W.hour
  -- (S22: 9 days - with the babies born meanwhile living their own lives the village's course differs;
  -- relief from a neighbouring village may come first and the household ask its kin later)
  for h = 1, 24 * 9 do
    world.advance(W, t2 + h)
    for _, id in ipairs(H.members) do
      local q = V.people[id]
      if q.alive and q.trip and q.trip.ask_kin then asked, asker = true, q end
    end
    if (O.acc.kin_help or 0) > 0 and asker and not asker.trip then break end
  end
  -- (asked for food and given it - or, things being bad enough, gone to live with them)
  -- (S19: with nothing left a household no longer waits out its patience - deciding to go and live
  -- with them, on the road there, counts)
  local hb = V.households[bride.hh]
  local moved = hb and hb.migrate and hb.migrate.to == O.id and not hb.migrate.given_up
  T.ok(asked or moved, "someone of " .. H.name .. " went to kin at " .. O.name .. " for help: " .. tostring(asker and asker.name)
    .. string.format(" [village food %.1f days]", H.need.food_days or -1))
  if moved and not asked then
    local mg = V.households[bride.hh].migrate
    T.ok(mg.why:find(O.name) ~= nil, "they went to live with kin at " .. O.name .. ": " .. mg.why)
    return string.format("%s's household went to live at %s: %s", bride.name, O.name, mg.why)
  end
  T.ok((O.acc.kin_help or 0) >= 1, "kin at " .. O.name .. " gave: " .. string.format("%.1f FU", O.acc.kin_help_fu or 0))
  T.ok((H.acc.import or 0) > imp0, "the food came to " .. H.name .. "'s books: " .. string.format("%.1f FU", (H.acc.import or 0) - imp0))
  local told = false
  for _, id in ipairs(O.members) do
    local q = V.people[id]
    for i = 1, 8 do
      local m = q.mem[i]
      if m and m[2]:find("came from " .. H.name .. " hungry") then told = true end
    end
  end
  T.ok(told, "the kin remember being asked")
  return string.format("%s asked kin at %s: %.1f FU given", asker and asker.name or "?", O.name, O.acc.kin_help_fu or 0)
end

return M
