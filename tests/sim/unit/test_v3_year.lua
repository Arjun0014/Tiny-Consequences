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
    if b.kind == "house" then tot = tot + (b.stock.food or 0) + (b.stock.seed or 0) + (b.stock.meals or 0) end
  end
  for _, pl in ipairs(s.plots) do tot = tot + pl.sheaves end
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.alive then
      if p.carry == "seed" or p.carry == "grain" or p.carry == "food" or p.carry == "sheaves" or p.carry == "fish" then tot = tot + p.camt end
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
    follow = 0, chores = 0, plough = 0, winter_sown = 0, nursed = 0 }
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
    return ok
  end
  local prev = food_total(V, s)
  local acc0 = {}
  local function accs() return (s.acc.harvest or 0) + (s.acc.foraged or 0) + (s.acc.fished or 0) - (s.acc.eaten or 0) - (s.acc.seed or 0)
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
