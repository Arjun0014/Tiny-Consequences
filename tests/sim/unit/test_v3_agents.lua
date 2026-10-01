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
  local hunted, meat = 0, 0
  for _, s in ipairs(W.S.v3.settlements) do
    visits = visits + (s.acc.wolf_nights or 0)
    kills = kills + (s.acc.wild_killed_wolves or 0) + (s.acc.stock_wolves or 0)
    hunted = hunted + (s.acc.wild_hunted or 0)
    meat = meat + (s.acc.hunt_meat or 0)
  end
  -- (D-094) hunters kill real animals and carry them home
  T.ok(hunted >= 2, "animals hunted " .. hunted)
  T.ok(meat > 10, "hunted meat brought home " .. meat)
  T.ok(seen > 500, "wild animal-samples on the maps " .. seen)
  T.eq(bad, 0, "wild animals on water or in a building")
  T.eq(over, 0, "more animals of a herd on a map than the herd holds")
  T.ok(visits >= 3, "wolf nights " .. visits)
  T.ok(kills >= 1, "kills by wolves " .. kills)
end


-- Trade by land (D-097): the shipped world over 24 days. A fishing household's porter walks a
-- load of smoked fish to a village with grain to spare, sells it into the granary, walks home
-- with grain: a round trip completes; what one village exports another imports, FU for FU.
function M.trade_walks_its_goods()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  local t0 = W.hour
  local road = 0
  for h = 12, 24 * 24, 12 do
    world.advance(W, t0 + h)
    for _, s in ipairs(W.S.v3.settlements) do
      for _, id in ipairs(s.members) do
        local p = W.S.v3.people[id]
        if p.alive and p.act == "travel" then road = road + 1 end
      end
    end
  end
  local trips, exp, imp, fish = 0, 0, 0, 0
  for _, s in ipairs(W.S.v3.settlements) do
    trips = trips + (s.acc.trips or 0)
    exp = exp + (s.acc.export or 0)
    imp = imp + (s.acc.import or 0)
    fish = fish + (s.acc.traded_fish or 0)
  end
  T.ok(trips >= 1, "trade round trips completed " .. trips)
  T.ok(road >= 2, "samples of porters on the road " .. road)
  T.ok(fish > 5, "smoked fish sold " .. fish)
  T.ok(math.abs(exp - imp) < 1e-6, string.format("exports %.3f against imports %.3f", exp, imp))
end

return M
