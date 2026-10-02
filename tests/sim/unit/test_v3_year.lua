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

local function food_total(W, s) return require("sim.agents.settle").food_total(W, s) end

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
  -- (the crop standing at the founding, at its expected yield: what the village should bring in)
  local cfg = cfgmod.V3
  R.standing = 0
  for _, pl in ipairs(s.plots) do
    if pl.crop ~= "flax" and (pl.stage == "growing" or pl.stage == "ripe") then
      local q = pl.q_n > 0.5 and pl.q_sum / pl.q_n or 0.85
      for r = 1, pl.rows do if pl.row_st[r] == 2 then R.standing = R.standing + cfg.row_yield * (pl.fert or 1) * q end end
    end
  end
  local prev = food_total(W, s)
  local acc0 = {}
  local function accs() return require("sim.agents.settle").food_flows(s) end
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
    local f, a = food_total(W, s), accs()
    local resid = f - (prev + (a - a0))
    if math.abs(resid) > math.abs(R.worst_resid) then R.worst_resid = resid end
    prev, a0 = f, a
    -- (D-089) every sheep on dry, open ground (never in water, a river or a building)
    do
      local mapm = require("sim.agents.map")
      require("sim.agents.stock").each(s, function(H, a)
        local i = mapm.tile_of(s, a.tx, a.ty)
        if i == 0 or s.land[i] ~= 1 or s.occ[i] ~= 0 or s.riv[i] > 0 then R.sheep_bad = R.sheep_bad + 1 end
        -- (D-092) spacing: out of the pen, nobody stands on another of its herd
        if a.act ~= "pen" and a.mother == 0 then
          R.spaced_n = (R.spaced_n or 0) + 1
          local near = 1e9
          for _, b in ipairs(H.animals) do
            if b ~= a and b.alive and b.act ~= "pen" and b.mother ~= a.k then
              local d = (b.tx - a.tx) ^ 2 + (b.ty - a.ty) ^ 2
              if d < near then near = d end
            end
          end
          if near < (require("sim.config").V3.stock[a.sp].sep * 0.3) ^ 2 then R.crowded = (R.crowded or 0) + 1 end
        end
      end)
    end
    for _, pl in ipairs(s.plots) do
      if pl.stage == "rest" and (pl.ploughed or 0) > 0 then R.plough = math.max(R.plough, pl.ploughed) end
      if pl.crop == "winter" and pl.stage == "growing" and pl.sown_day and pl.sown_day >= 20 then R.winter_sown = R.winter_sown + 1 end
      R.seen = R.seen or {}
      R.seen[pl.stage] = true
      -- (D-100) a field the village chose to rest is not sown
      if pl.crop ~= "flax" and pl.sow_ok == false and pl.stage == "fallow" and pl.sown > 0 then R.rest_sown = (R.rest_sown or 0) + 1 end
    end
    -- (D-100) the autumn's decision: did the village want winter grain?
    if s.need.sowing and (s.doy or 0) >= cfgmod.V3.sow_winter[1] then R.autumn_want = math.max(R.autumn_want or -1e9, s.sow_want or 0) end
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
  -- (D-108: was "> 600"; the founding's draws changed with the oxen and the harvest depends on the
  -- standing crop: the village must bring in what stood in its fields, at least 70% of it)
  T.ok(R.standing > 300, "crop standing at the founding " .. R.standing)
  T.ok((s.acc.harvest or 0) >= 0.7 * R.standing, string.format("harvest %.0f of a standing crop of %.0f", s.acc.harvest or 0, R.standing))
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
  -- (S19: since D-133 salt cures fish too: the surplus is smoked or salted)
  T.ok((a.smoked or 0) + (a.salted or 0) > 5, string.format("fish cured: smoked %.1f, salted %.1f", a.smoked or 0, a.salted or 0))
end

-- The herds (D-089, D-092): taken out and brought home, fed, born, shorn, milked, the dung on the
-- fields; animals stay on dry land and keep their distance from each other.
function M.the_flock_is_tended()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  local F = s.herds and s.herds.flock
  T.ok(F ~= nil, "no flock")
  T.ok(R.herd > 10, "herding stints " .. R.herd)
  T.ok((s.acc.born_sheep or 0) > 3, "lambs " .. tostring(s.acc.born_sheep))
  T.ok((s.acc.wool or 0) > 5, "wool shorn " .. tostring(s.acc.wool))
  T.ok((s.acc.hay or 0) > 20, "hay mown " .. tostring(s.acc.hay))
  local n, h = 0, 0
  for _, a in ipairs(F.animals) do if a.alive then n, h = n + 1, h + a.hunger end end
  T.ok(n > 10 and h / n < 0.5, string.format("%d in the flock, mean hunger %.2f", n, n > 0 and h / n or 0))
  T.eq(R.sheep_bad, 0, "animal-days on water or in a building")
  T.ok((R.crowded or 0) <= (R.spaced_n or 1) * 0.03, string.format("animals on top of each other: %d of %d", R.crowded or 0, R.spaced_n or 0))
end

-- Milk and dung (D-092): dams milked by their households, cheese made, the pens' dung on the fields.
function M.milk_cheese_and_dung()
  local R = run()
  local s = R.W.S.v3.settlements[1]
  T.ok(s.herds.cattle ~= nil or s.herds.flock ~= nil, "no herds")
  T.ok((s.acc.milk or 0) > 5, "milk " .. tostring(s.acc.milk))
  T.ok((s.acc.cheese or 0) > 1, "cheese " .. tostring(s.acc.cheese))
  T.ok((s.acc.mucked or 0) > 3, "dung carried to the fields " .. tostring(s.acc.mucked))
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
  -- (D-100) the winter crop is sown when the village wants more grain than its store and crop
  -- will give, and the fields rest when it does not
  if (R.autumn_want or 0) > 0 then
    T.ok(R.winter_sown > 0, "no winter crop sown in autumn though the village wanted " .. R.autumn_want .. " rows")
  else
    T.ok(R.winter_sown == 0, "winter crop sown though the store sufficed")
  end
  T.ok((R.rest_sown or 0) == 0, "fields chosen to rest were sown")
  -- (the fields went through the year's stages: D-100 lets a field rest when the store suffices, so
  -- the day-50 snapshot no longer needs a growing field)
  for _, st in ipairs({ "growing", "ripe", "stubble", "rest", "fallow" }) do
    T.ok(R.seen[st], "no field was " .. st .. " in 50 days")
  end
end

return M
