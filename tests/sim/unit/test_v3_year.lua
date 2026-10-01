-- v3: a whole year of village life (D-080, D-081), one 50-day run of seed 22 shared by the tests.
-- The economy closes (every FU accounted for), nobody starves, infants are always held and fed,
-- children are never field hands, apprentices learn beside a mentor, the three-field rotation turns.
local T = require("tests.sim.lib")
local world = require("sim.world")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local cfgmod = require("sim.config")

local M = {}
local RUN

local function food_total(V, s)
  local c = cfgmod.V3
  local tot = (s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)
  for _, bid in ipairs(s.blist) do
    local b = s.buildings[bid]
    -- (raw food at home: grain and gathered food, fresh and smoked fish, D-087)
    if b.kind == "house" then tot = tot + (b.stock.food or 0) + (b.stock.fish or 0) + (b.stock.smoked or 0) + (b.stock.meat or 0) + (b.stock.seed or 0) + (b.stock.meals or 0) end
  end
  for _, pl in ipairs(s.plots) do tot = tot + pl.sheaves end
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.alive then
      if p.carry == "seed" or p.carry == "grain" or p.carry == "food" or p.carry == "sheaves" or p.carry == "fish" or p.carry == "meat" then tot = tot + p.camt end
      if p.lunch then tot = tot + c.meal_fu end
    end
  end
  return tot
end

local function run()
  if RUN then return RUN end
  local W = require("sim.agents.worldgen").generate(22, {})
  world.set_systems(require("sim.agents.worldgen").systems())
  local V = W.S.v3
  local s = V.settlements[1]
  local R = { W = W, worst_resid = 0, child_field = {}, infant_free = 0, infant_hungry_h = {}, beside = 0,
    follow = 0, chores = 0, plough = 0, winter_sown = 0, nursed = 0, boat_wrong = {}, boarded = 0,
    last = {}, winter = { all = 0, leisure = 0 }, herd = 0, sheep_bad = 0 }
  local clock = require("sim.agents.clock")
  local LEISURE = { rest = true, visit = true, talk = true, sit_out = true }
  local orig = act.start
  act.start = function(W_, s_, p, now)
    local ok = orig(W_, s_, p, now)
    local st = people.stage(p, now)
    if st == "child" and (p.act == "till" or p.act == "harvest" or p.act == "chop" or p.act == "build" or p.act == "sow") then
      R.child_field[#R.child_field + 1] = p.name .. " " .. p.act
    end
    if st == "child" and p.objk == "follow" then R.follow = R.follow + 1 end
    if st == "child" and (p.objk == "fetch_water" or p.objk == "fetch_wood") then R.chores = R.chores + 1 end
    if p.mentored then R.beside = R.beside + 1 end
    if p.act == "nurse" or p.act == "feed_babe" then R.nursed = R.nursed + 1 end
    if p.objk == "herd" and p.act == "herd" then R.herd = R.herd + 1 end
    -- (D-087) a household rows out only in its own boat
    if p.act == "board" then
      R.boarded = R.boarded + 1
      local st = p.plan[p.pi]
      local boat = s_.boats and st and s_.boats[st.boat or 0]
      if boat and boat.owner ~= 0 and boat.owner ~= p.hh then R.boat_wrong[#R.boat_wrong + 1] = p.name end
    end
    -- (D-088) adults' waking winter hours: how much is leisure
    if st == "adult" then
      local L = R.last[p.id]
      if L and L.k ~= "sleep" and clock.local_doy(L.t, W_.grid.lat[s_.cell]) >= 36 then
        local d = now - L.t
        R.winter.all = R.winter.all + d
        if LEISURE[L.k] then R.winter.leisure = R.winter.leisure + d end
      end
      R.last[p.id] = { t = now, k = p.objk or "" }
    end
    return ok
  end
  local prev = food_total(V, s)
  local acc0 = {}
  local function accs() return (s.acc.harvest or 0) + (s.acc.foraged or 0) + (s.acc.fished or 0) + (s.acc.meat or 0) - (s.acc.eaten or 0) - (s.acc.seed or 0)
    - (s.acc.lunch or 0) - (s.acc.spoiled or 0) end
  local a0 = accs()
  for d = 1, 50 do
    for h = 1, 24 do
      world.advance(W, (d - 1) * 24 + h)
      for _, id in ipairs(s.members) do
        local p = V.people[id]
        if p.alive and p.act == "carried" then
          local c = V.people[p.carer or 0]
          if not (c and c.alive) then R.infant_free = R.infant_free + 1 end
          if p.hunger >= 1 then R.infant_hungry_h[p.id] = (R.infant_hungry_h[p.id] or 0) + 1 end
        end
      end
    end
    local f, a = food_total(V, s), accs()
    local resid = f - (prev + (a - a0))
    if math.abs(resid) > math.abs(R.worst_resid) then R.worst_resid = resid end
    prev, a0 = f, a
    -- (D-089) every sheep on dry, open ground (never in water, a river or a building)
    if s.flock then
      local mapm = require("sim.agents.map")
      for _, a in ipairs(s.flock.sheep) do
        if a.alive then
          local i = mapm.tile_of(s, a.tx, a.ty)
          if i == 0 or s.land[i] ~= 1 or s.occ[i] ~= 0 or s.riv[i] > 0 then R.sheep_bad = R.sheep_bad + 1 end
        end
      end
    end
    for _, pl in ipairs(s.plots) do
      if pl.stage == "rest" and (pl.ploughed or 0) > 0 then R.plough = math.max(R.plough, pl.ploughed) end
      if pl.crop == "winter" and pl.stage == "growing" and pl.sown_day and pl.sown_day >= 20 then R.winter_sown = R.winter_sown + 1 end
    end
  end
  act.start = orig
  RUN = R
  return R
end

function M.economy_closes_and_nobody_starves()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  T.ok(math.abs(R.worst_resid) < 0.5, string.format("food appeared or vanished: %.2f FU in a day", R.worst_resid))
  for _, id in ipairs(R.W.S.v3.order) do
    local p = R.W.S.v3.people[id]
    T.ok(p.alive or p.died_of ~= "hunger", p.name .. " starved")
  end
  T.ok((s.acc.harvest or 0) > 600, "harvest " .. tostring(s.acc.harvest))
  T.ok((s.acc.felled or 0) > 30, "trees felled " .. tostring(s.acc.felled))
end

function M.infants_held_and_fed()
  local R = run()
  T.eq(R.infant_free, 0, "an infant with nobody holding them (hours)")
  for id, h in pairs(R.infant_hungry_h) do T.ok(h <= 6, string.format("infant #%d starving %d hours", id, h)) end
  T.ok(R.nursed > 20, "feeds " .. R.nursed)
end

function M.children_are_children()
  local R = run()
  T.eq(#R.child_field, 0, "children at field work: " .. table.concat(R.child_field, ", "))
  T.ok(R.follow > 10, "children tagging along " .. R.follow)
  T.ok(R.chores > 5, "children's chores " .. R.chores)
end

function M.apprentices_learn_beside_a_mentor()
  local R = run()
  local V = R.W.S.v3
  local n = 0
  for _, id in ipairs(V.settlements[1].members) do
    local p = V.people[id]
    if p.alive and people.stage(p, R.W.hour) == "apprentice" then
      n = n + 1
      T.ok(p.mentor ~= 0, p.name .. " has no mentor")
    end
  end
  T.ok(n >= 1, "no apprentices")
  T.ok(R.beside > 10, "steps beside a mentor " .. R.beside)
end

-- Textiles (D-088): flax grown, pulled, spun and woven; clothes sewn; nobody left in rags.
function M.linen_is_made()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  local a = s.acc
  T.ok((a.flax or 0) > 5, "flax pulled " .. tostring(a.flax))
  T.ok((a.spun or 0) > 5, "yarn spun " .. tostring(a.spun))
  T.ok((a.woven or 0) > 3, "cloth woven " .. tostring(a.woven))
  T.ok((a.garments or 0) >= 3, "garments sewn " .. tostring(a.garments))
  local rags = 0
  for _, id in ipairs(s.members) do
    local p = R.W.S.v3.people[id]
    if p.alive and p.act ~= "carried" and (p.clothes or 1) < 0.15 then rags = rags + 1 end
  end
  T.ok(rags <= 2, "people in rags " .. rags)
end

-- Fishing (D-087): own boats only; the catch real but not more than the fields'; surplus smoked.
function M.fishers_fish_from_their_own_boats()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  if not s.dock then return end
  T.eq(#R.boat_wrong, 0, "rowed out in another household's boat: " .. table.concat(R.boat_wrong, ", "))
  T.ok(R.boarded > 10, "boardings " .. R.boarded)
  local a = s.acc
  T.ok((a.fished or 0) > 50 and (a.fished or 0) < (a.harvest or 0), string.format("fish %.0f against harvest %.0f", a.fished or 0, a.harvest or 0))
  T.ok((a.smoked or 0) > 5, "fish smoked " .. tostring(a.smoked))
end

-- The flock (D-089): taken out and brought home, fed, lambing, shorn; sheep stay on dry land.
function M.the_flock_is_tended()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  local F = s.flock
  T.ok(F ~= nil, "no flock")
  T.ok(R.herd > 10, "herding stints " .. R.herd)
  T.ok((s.acc.lambs or 0) > 3, "lambs " .. tostring(s.acc.lambs))
  T.ok((s.acc.wool or 0) > 5, "wool shorn " .. tostring(s.acc.wool))
  T.ok((s.acc.hay or 0) > 20, "hay mown " .. tostring(s.acc.hay))
  local n, h = 0, 0
  for _, a in ipairs(F.sheep) do if a.alive then n, h = n + 1, h + a.hunger end end
  T.ok(n > 10 and h / n < 0.5, string.format("%d sheep, mean hunger %.2f", n, n > 0 and h / n or 0))
  T.eq(R.sheep_bad, 0, "sheep-days on water or in a building")
end

-- Winter has work (D-088): adults' waking winter hours are not mostly rest and visits.
function M.winter_is_not_idle()
  local R = run()
  T.ok(R.winter.all > 100, "winter hours measured " .. R.winter.all)
  local share = R.winter.leisure / R.winter.all
  T.ok(share < 0.55, string.format("adults' winter leisure %.0f%%", share * 100))
end

function M.the_rotation_turns()
  local R = run()
  T.ok(R.plough >= 6, "the fallow was not ploughed (" .. R.plough .. " rows)")
  T.ok(R.winter_sown > 0, "no winter crop sown in autumn")
  local s = R.W.S.v3.settlements[1]
  local st = {}
  for _, pl in ipairs(s.plots) do st[pl.stage] = (st[pl.stage] or 0) + 1 end
  T.ok((st.rest or 0) >= 1 and (st.growing or 0) >= 1, "plots not in rotation at day 50")
end

return M
