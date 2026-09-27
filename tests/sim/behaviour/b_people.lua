-- B07: births create child entities with parents and a household; children come of age at 14 and
-- take an occupation.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local ents = require("sim.society.entities")
local life = require("sim.society.life")
local check = require("tests.sim.support.society_check")
local U = require("tests.sim.behaviour.util")
local M = {}

function M.b07_births_make_children_who_grow_up_into_workers()
  local W = worlds.fresh(22)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local v = U.villages(W)[1]
  local mum
  for _, id in ipairs(v.members) do
    local p = P[id]
    local a = ents.age(W, p)
    if p.sex == "f" and p.partner ~= 0 and p.preg_due == 0 and a >= 20 and a <= 38 then mum = p; break end
  end
  T.ok(mum, "a married woman")
  mum.preg_due = W.hour + 5
  v.preg = v.preg or {}
  v.preg[#v.preg + 1] = mum.id
  local pop0 = v.pop
  local next0 = W.ent.next_person
  life.births(W, v)
  local child
  for id = next0, W.ent.next_person - 1 do
    if P[id] and P[id].mother == mum.id then child = P[id] end
  end
  T.ok(child ~= nil, "a child entity exists")
  T.eq(child.mother, mum.id, "mother")
  T.eq(child.father, mum.partner, "father")
  T.eq(child.household, mum.household, "born into the mother's household")
  T.eq(child.village, v.id, "in the village")
  T.eq(child.job, "child", "a child")
  T.eq(child.born, W.hour, "born now")
  local listed = false
  for _, id in ipairs(v.members) do if id == child.id then listed = true end end
  T.ok(listed and v.pop == #v.members and v.pop >= pop0, "a member of the village")
  T.eq(mum.preg_due, 0, "no longer pregnant")
  T.ok(mum.dead or mum.nursing_until > W.hour, "nursing")
  -- coming of age: a child who turns 14 this week takes the occupation the village is short of
  local kid
  for _, id in ipairs(v.members) do
    local p = P[id]
    if p.job == "child" and ents.age(W, p) >= 10 then kid = p; break end
  end
  T.ok(kid, "an older child")
  kid.born = W.hour - cfg.adult_age * 8064 - 1
  life.weekly(W, v)
  if not kid.dead then
    T.ok(kid.job == "farmer" or kid.job == "fisher" or kid.job == "woodcutter" or kid.job == "builder",
      "an adult occupation: " .. kid.job)
  end
  -- in the running world every child born has living-world parents and a household
  local t0 = W.hour
  world.run_years(W, 3)
  local born = 0
  for id, p in pairs(P) do
    if p.born > t0 then
      born = born + 1
      T.ok(p.mother ~= 0 and p.father ~= 0, "both parents recorded")
      if not p.dead and p.away == 0 then T.ok(W.ent.households[p.household] ~= nil, "in a household") end
    end
  end
  T.ok(born > 0, "children were born")
  T.ok(check.check(W) == nil, "invariants")
  return string.format("%d births in 3 years", born)
end

return M
