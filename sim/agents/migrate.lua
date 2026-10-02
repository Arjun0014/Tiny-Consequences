-- Households moving away (roadmap v3 R5, S15 draft, S16). A household weighs leaving its village
-- from what pushes it - hunger (the village's stores and its own larder running out), no house of
-- its own, beasts lost to wolves - against what draws it to a village it knows: food there (what it
-- last heard of the grain they have to spare), its kin living there, the trust between the
-- villages; less the road and what holds it (its house, the years lived there). Hardship has to
-- last (`mig_days` mornings running) before a household thinks of leaving. When the balance tips
-- the whole household goes, on foot and together: each grown-up packs provisions and what they can
-- carry of the household's food, the babes are carried, a grown-up leads each kind of the
-- household's beasts the other village keeps (the rest go to kin), they gather at the gate and
-- take the road at the hour agreed (think.lua migrate_legs). At the other village's gate they
-- become its people; they lodge with kin there (or a household with room) until the house the
-- village plans for them is built (settle.lua plan_houses). Their old house stands empty for a
-- couple waiting for one. No village is made out of nothing: a household goes to where people live.
--   hh.migrate = { to, from, depart, why, decided, lead = { [kind] = person id }, arrived }
--   hh.pushd (mornings of hardship running), hh.lodge (host household id while lodging)
--   p.trip = { to, from, migrate = hh id, lead_kind, packed }
local cfgmod = require("sim.config")
local econ = require("sim.agents.econ")
local people = require("sim.agents.people")
local rng = require("sim.rng")
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min = math.floor, math.max, math.min
local function C() return cfgmod.V3 end

-- How hard household hh is pushed to leave village s now (0..), and why.
function M.push(W, s, hh, now)
  local c = C()
  local V = W.S.v3
  local push, why = 0, nil
  -- hunger: the village's grain and relish against its mouths
  local pop = max(1, #s.members)
  local g = econ.village_stock(W, s, "grain")
  local r = econ.village_stock(W, s, "smoked")
  local days = (g + r) / (pop * 0.7)
  if days < c.mig_hunger_days then
    push = push + (c.mig_hunger_days - days) / c.mig_hunger_days
    why = sfmt("the stores will feed us %.0f days more", days)
  end
  -- hungry people in the household
  local hungry, n = 0, 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      n = n + 1
      if p.hunger > c.starving * 0.8 then hungry = hungry + 1 end
    end
  end
  if n > 0 and hungry > 0 then
    push = push + 0.4 * hungry / n
    why = why or "we are going hungry"
  end
  -- no house of its own (a household that lost its home)
  if (hh.home or 0) == 0 then
    push = push + c.mig_homeless
    why = why or "we have no house of our own"
  end
  -- beasts lost to wolves lately
  if hh.dog_lost and floor(now / 24) - hh.dog_lost < 6 then
    push = push + 0.1
    why = why or "the wolves come to our pens"
  end
  return push, why
end

-- What draws household hh of village s to village o (by what s knows of o), and why.
function M.pull(W, s, hh, o, now)
  local c = C()
  local V = W.S.v3
  local pull, why = 0, nil
  -- food there: the grain they were heard to have to spare, against the household's mouths
  local _, _, sp = econ.expect(W, s, o, "grain", now)
  local mouths = max(1, #hh.members)
  local food = min(1, sp / (mouths * c.mig_food_days * 0.7))
  if food > 0.1 then
    pull = pull + 0.8 * food
    why = sfmt("%s had grain to spare when last heard of", o.name)
  end
  -- kin living there
  local kin = 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      for _, k in ipairs({ p.mother, p.father, p.partner }) do
        local q = V.people[k or 0]
        if q and q.alive and q.sett == o.id then kin = kin + 1 end
      end
    end
  end
  if kin > 0 then
    -- (S17: kin draw, but less to a place heard to be going hungry itself)
    pull = pull + min(0.6, 0.3 * kin) * (0.3 + 0.7 * min(1, food))
    why = why or ("our kin live at " .. o.name)
  end
  -- (S17) kin there offered the household a place under their roof
  local off = hh.kin_offer
  if off and off.sett == o.id and now - off.t < c.kin_offer_keep * 24 then
    local q = V.people[off.by or 0]
    pull = pull + c.kin_offer_pull
    why = (q and (q.name .. " offered us a place under their roof at " .. o.name)) or why
  end
  -- trust between the villages
  pull = pull + 0.3 * econ.rel(s, o).trust
  return pull, why
end

-- Each morning (settle.daily): households that would leave, and where to.
function M.daily(W, s, now)
  local c = C()
  local V = W.S.v3
  now = V.now or W.hour
  local trade = require("sim.agents.trade")
  local partners
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and not hh.migrate then
      local push, pwhy = M.push(W, s, hh, now)
      hh.pushd = push >= c.mig_min_push and (hh.pushd or 0) + 1 or 0
      -- (how long a household bears hardship before it thinks of leaving is its own)
      local patience = c.mig_days + floor(4 * rng.float(W.seed, rng.S.V3_THINK, hid, 0, 131))
      if hh.pushd >= patience then
        partners = partners or trade.partners(W, s)
        -- everyone at home (none away on a trip, none on a boat) and a grown-up among them
        local home_all, grown = true, false
        for _, id in ipairs(hh.members) do
          local p = V.people[id]
          if p and p.alive then
            if p.at or p.trip or (p.boat or 0) ~= 0 or (p.riding or 0) ~= 0 then home_all = false end
            local st = people.stage(p, now)
            if st == "adult" or st == "elder" then grown = true end
          end
        end
        -- what holds them: their house and the years they have lived here (S17: and, for those who
        -- came to make this place, what they set out to do: they give it a season first)
        local years = (now - (hh.formed or 0)) / 1152
        local hold = c.mig_hold + ((hh.home or 0) ~= 0 and 0.15 or 0) + 0.05 * min(4, max(0, years))
        if hh.found and hh.found.arrived and now - hh.found.arrived < c.found_stay_days * 24 then hold = hold + c.found_stay_hold end
        local best, bu, bwhy = nil, 0, nil
        for _, o in ipairs(partners) do
          if o.alive ~= false and o.id ~= s.id then
            local pull, why = M.pull(W, s, hh, o, now)
            local _, hrs = trade.road(W, s, o)
            local u = push + pull - hold - hrs * c.mig_road_hour
            if u > bu then best, bu, bwhy = o, u, why end
          end
        end
        -- (a little of each household's own mind in it)
        local noise = rng.float(W.seed, rng.S.V3_THINK, hid, floor(now / 24) % 4294967296, 130)
        local go = best and home_all and grown and bu > c.mig_threshold * (0.8 + 0.4 * noise)
        if not (best and bu > c.mig_threshold) then
          -- (S17) hardship that has lasted, and no village known draws them enough: founding.daily
          -- weighs making a new place
          hh.no_refuge, hh.mig_push, hh.mig_why = floor(now / 24), push, pwhy
        end
        if go then
          local depart = now + c.mig_pack_h
          hh.migrate = { to = best.id, from = s.id, depart = depart, decided = now,
            why = "Leaving " .. s.name .. " for " .. best.name .. ": " .. (pwhy or "life is hard here") .. "; " .. (bwhy or ("we will try " .. best.name)) }
          hh.migrate.lead = M.share_beasts(W, s, hh, best, now)
          for _, id in ipairs(hh.members) do
            local p = V.people[id]
            if p and p.alive then
              people.remember(p, now, sfmt("We decided to leave %s for %s: %s", s.name, best.name, pwhy or "life is hard here"))
            end
          end
          s.acc.leaving = (s.acc.leaving or 0) + 1
        end
      end
    end
  end
end

-- The beasts a migrating household takes (the kinds the other village keeps, each led by one of
-- its grown-ups) and gives to kin or neighbours (the rest): { [kind] = person id }.
function M.share_beasts(W, s, hh, o, now)
  local stock = require("sim.agents.stock")
  local V = W.S.v3
  local lead = {}
  local adults = {}
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    local st = p and p.alive and people.stage(p, now)
    if st == "adult" or st == "elder" or st == "apprentice" then adults[#adults + 1] = p end
  end
  local ai = 1
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    if H then
      local mine = {}
      for _, a in ipairs(H.animals) do
        if a.alive and a.owner == hh.id then mine[#mine + 1] = a end
      end
      if #mine > 0 then
        local keeps = o.herds and o.herds[kind]
        local p = adults[ai]
        if keeps and p then
          lead[kind] = p.id
          ai = ai + 1
        else
          -- (left with kin who stay, else the household staying with most beasts of the kind)
          local to, best = 0, -1
          for _, hid in ipairs(s.households) do
            local h2 = V.households[hid]
            if hid ~= hh.id and h2 and #h2.members > 0 and not h2.migrate then
              local n = 0
              for _, a in ipairs(H.animals) do if a.alive and a.owner == hid then n = n + 1 end end
              for _, id in ipairs(hh.members) do
                local q = V.people[id]
                if q then for _, qid in ipairs(h2.members) do if people.is_kin(W, q, qid) then n = n + 100 end end end
              end
              if n > best then to, best = hid, n end
            end
          end
          if to ~= 0 then
            for _, a in ipairs(mine) do a.owner = to end
            local q = V.people[V.households[to].members[1] or 0]
            for _, id in ipairs(hh.members) do
              local p = V.people[id]
              if p and p.alive and q then people.remember(p, now, sfmt("Left our %s with the %ss", kind == "cattle" and "cattle" or (kind == "swine" and "pigs" or "sheep and goats"), q.surname or "neighbour")) end
            end
          end
        end
      end
    end
  end
  return lead
end

-- The household that takes in a household come to village o: kin of theirs first, else the one
-- with the fewest mouths in a standing house.
local function host_of(W, o, hh)
  local V = W.S.v3
  -- (S17: the kin who offered them a place)
  local off = hh.kin_offer
  local kh = off and off.sett == o.id and V.households[off.host or 0]
  if kh and #kh.members > 0 and o.buildings[kh.home or 0] and not kh.lodge then return kh end
  local best, bs = nil, -1e9
  for _, hid in ipairs(o.households) do
    local h2 = V.households[hid]
    local b = h2 and h2 ~= hh and o.buildings[h2.home or 0]
    if b and b.state == "standing" and not h2.lodge and #h2.members > 0 then
      local kin = 0
      for _, id in ipairs(hh.members) do
        local p = V.people[id]
        if p then
          for _, qid in ipairs(h2.members) do if people.is_kin(W, p, qid) then kin = kin + 1 end end
        end
      end
      local sc = kin * 10 - #h2.members
      if sc > bs then best, bs = h2, sc end
    end
  end
  return best
end

-- Person p of a migrating household reaches village o's gate: they are its people now; the
-- household (moved with the first of it to arrive) lodges with a household of o until a house of
-- its own stands (o.waiting).
function M.arrive(W, o, p, gx, gy, now)
  local V = W.S.v3
  local hh = V.households[p.hh]
  local from = V.settlements[p.sett]
  if from and from ~= o then
    require("sim.agents.settle").carry_over(from, o, p)
    people.remove_id(from.members, p.id)
    if from.visitors then people.remove_id(from.visitors, p.id) end
  end
  if o.visitors then people.remove_id(o.visitors, p.id) end
  local found = false
  for _, id in ipairs(o.members) do if id == p.id then found = true end end
  if not found then o.members[#o.members + 1] = p.id end
  p.sett, p.at = o.id, nil
  -- (a babe in arms comes with its carer)
  local ib = V.people[p.babe or 0]
  if ib and ib.alive and ib.sett ~= o.id then
    if V.settlements[ib.sett] then people.remove_id(V.settlements[ib.sett].members, ib.id) end
    o.members[#o.members + 1] = ib.id
    ib.sett, ib.at, ib.x, ib.y = o.id, nil, gx, gy
  end
  if hh and hh.sett ~= o.id then
    local old = V.settlements[hh.sett]
    if old then
      people.remove_id(old.households, hh.id)
      local b = old.buildings[hh.home or 0]
      if b and b.hh == hh.id then b.hh = 0; b.for_name = "the " .. (p.surname or "people") .. "s, who left for " .. o.name end
      old.acc.left = (old.acc.left or 0) + 1
      require("sim.agents.settle").reassign_plots(W, old, hh.id)
    end
    hh.sett, hh.home = o.id, 0
    o.households[#o.households + 1] = hh.id
    -- (the babes of the household, in someone's arms, come too)
    for _, id in ipairs(hh.members) do
      local ib = V.people[id]
      if ib and ib.alive and ib.sett ~= o.id and people.stage(ib, now) == "infant" then
        if V.settlements[ib.sett] then people.remove_id(V.settlements[ib.sett].members, ib.id) end
        o.members[#o.members + 1] = ib.id
        ib.sett, ib.at, ib.x, ib.y = o.id, nil, gx, gy
      end
    end
    local host = host_of(W, o, hh)
    if host then
      hh.home, hh.lodge = host.home, host.id
      for _, id in ipairs(host.members) do
        local q = V.people[id]
        if q and q.alive then people.remember(q, now, sfmt("Took in the %s household, come from %s", p.surname or "newcomer", old and old.name or "away")) end
      end
    else
      local fx, fy = require("sim.agents.map").free_spot(o, gx * 0.6 + (o.sqx or 0) * 0.4, gy * 0.6 + (o.sqy or 0) * 0.4, 6)
      hh.camp = { x = fx or gx, y = fy or gy }
    end
    o.waiting = o.waiting or {}
    o.waiting[#o.waiting + 1] = hh.id
    hh.lodge = hh.lodge or 0
    -- (S17) their dog came with them
    require("sim.agents.dogs").come(W, o, hh, gx, gy, now)
    o.acc.arrived = (o.acc.arrived or 0) + 1
    hh.migrate.arrived = now
    hh.pushd = 0
  end
  people.remember(p, now, sfmt("Came to %s to live", o.name))
  require("sim.agents.founding").tell(W, p, o)
end

-- Setting off (p takes the road): every babe of the household is in the arms of one of those
-- going - its mother if she can, else p, else another grown-up of the household.
function M.take_babes(W, s, p, now)
  local V = W.S.v3
  local hh = V.households[p.hh]
  if not hh then return end
  local kin = require("sim.agents.kin")
  for _, id in ipairs(hh.members) do
    local ib = V.people[id]
    if ib and ib.alive and people.stage(ib, now) == "infant" then
      local q = V.people[ib.carer or 0]
      if not (q and q.alive and q.hh == hh.id and q.babe == ib.id and ib.act == "carried") then
        local m = V.people[ib.mother or 0]
        local to = (m and m.alive and m.hh == hh.id and (m.babe or 0) == 0) and m or ((p.babe or 0) == 0 and p or nil)
        if not to then
          for _, qid in ipairs(hh.members) do
            local o = V.people[qid]
            local st = o and o.alive and people.stage(o, now)
            if (st == "adult" or st == "elder" or st == "apprentice") and (o.babe or 0) == 0 then to = o; break end
          end
        end
        if to then
          kin.give(W, ib, to)
          ib.act = "carried"
          ib.why = "Carried on the road by " .. to.name
        end
      end
    end
  end
end

-- (S16, R4) A bride reaches her husband's village: she is its now, of his household; the couple
-- waits for a house of their own like any couple there.
function M.arrive_person(W, o, p, gx, gy, now)
  local V = W.S.v3
  local from = V.settlements[p.sett]
  if from and from ~= o then
    require("sim.agents.settle").carry_over(from, o, p)
    people.remove_id(from.members, p.id)
    if from.visitors then people.remove_id(from.visitors, p.id) end
  end
  if o.visitors then people.remove_id(o.visitors, p.id) end
  o.members[#o.members + 1] = p.id
  p.sett, p.at = o.id, nil
  local g = V.people[p.partner or 0]
  local hh = g and g.alive and V.households[g.hh]
  if hh then people.join(W, p, hh) end
  o.couples = o.couples or {}
  o.couples[#o.couples + 1] = p.id
  if p.move then p.move.arrived = now end
  o.acc.brides_in = (o.acc.brides_in or 0) + 1
  people.remember(p, now, sfmt("Came to %s to live with %s", o.name, g and g.name or "my husband"))
  -- (S17) what she knows of places comes with her: what she saw, and her old village's knowledge
  local founding = require("sim.agents.founding")
  founding.tell(W, p, o)
  if from then founding.share(W, o, from, now) end
end

-- Why p (of household hh) is doing what the move asks of them now.
function M.why(W, p, hh)
  local V = W.S.v3
  local tr = p.trip
  local mg = (tr and tr.move and p.move) or (hh and hh.migrate)
  local o = V.settlements[(mg and mg.to) or (tr and tr.to) or 0]
  local oname = o and o.name or "the new village"
  if mg and not mg.arrived and tr and tr.move then
    local g = V.people[mg.groom or 0]
    local gname = g and g.name or "my husband"
    local now = V.now or W.hour
    if now < mg.depart then return mg.why .. ": packing, then to the gate where " .. gname .. " waits" end
    return mg.why .. ": on the road with " .. gname
  end
  if mg and not mg.arrived then
    local now = V.now or W.hour
    if tr and tr.lead_kind and now < mg.depart then return mg.why .. ": bringing our beasts to the gate" end
    if now < mg.depart then return mg.why .. ": packing and meeting the others at the gate" end
    return mg.why .. ": on the road to " .. oname .. " with the household"
  end
  if p.move and tr and tr.move then
    local g = V.people[p.partner or 0]
    return "Come to " .. oname .. " to live with " .. (g and g.name or "my husband")
  end
  local host = hh and hh.lodge and V.households[hh.lodge]
  local hq = host and V.people[host.members[1] or 0]
  if hq then return "Come to live at " .. oname .. ": settling in with the " .. (hq.surname or "") .. "s until our own house stands" end
  return "Come to live at " .. oname .. ": settling in"
end

return M
