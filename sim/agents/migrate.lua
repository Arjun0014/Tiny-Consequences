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
-- (S18) By sea: a household with its own sound boat and someone to sail it may go over the water
-- to a village it knows of (news heard, kin there): the boat has room for so many (a grown-up a
-- place, a child half, a babe in arms none), a few small beasts (sheep, goats, a young pig: a place
-- each; never cattle or grown pigs) and a load or two of the household's goods; they provision the
-- passage from the larder; carry the goods and lead the beasts to the dock, wait for everyone and
-- for weather fit to sail, board, sail, land at the other village's dock and carry the goods up;
-- the boat is the new village's from then on (theirs still). If it founders, all aboard are lost.
--   hh.migrate = { to, from, depart, why, decided, lead = { [kind] = person id }, arrived,
--                  sea = { boat, skipper, hrs, ship = { [kind] = { animal keys } }, loads, boat2 } }
--   hh.pushd (mornings of hardship running), hh.lodge (host household id while lodging)
--   p.trip = { to, from, migrate = hh id, lead_kind, packed }
local cfgmod = require("sim.config")
local econ = require("sim.agents.econ")
local people = require("sim.agents.people")
local rng = require("sim.rng")
local clock = require("sim.agents.clock")
local sfmt = require("sim.fmt").format
local deeds = require("sim.agents.deeds")   -- (S20, R6: fear of a place, read from deeds)

local M = {}
local floor, max, min = math.floor, math.max, math.min
local function C() return cfgmod.V3 end

-- How hard household hh is pushed to leave village s now (0..), and why.
function M.push(W, s, hh, now, need_only)
  local c = C()
  local V = W.S.v3
  local push, why = 0, nil
  -- hunger: what the household has to eat against its mouths - its part of the village's granary
  -- and (S19) of the beasts they could kill before giving up the place (a share of the meat on the
  -- hoof: breeding stock is the last to go), and its own larder (S19: judged by each household, not
  -- by the village - a fisher's household with its larder full of the catch does not leave because
  -- its neighbours' larders are empty; they do)
  local pop = max(1, #s.members)
  local n = max(1, #hh.members)
  local gr = s.granary and s.granary.stock
  local shared = (gr and ((gr.grain or 0) + (gr.sheaves or 0) + (gr.smoked or 0) + (gr.cheese or 0)) or 0)
    + M.meat_on_hoof(W, s) * c.mig_hoof_k
  local own = 0
  local home = s.buildings[hh.home or 0]
  if home then
    local st = home.stock
    own = (st.food or 0) + (st.meals or 0) + (st.smoked or 0) + (st.fish or 0) + (st.cheese or 0) + (st.meat or 0)
    local owner = V.households[home.hh or 0]
    if owner and owner ~= hh then own = own * n / (n + #owner.members) end
  end
  local days = (shared * n / pop + own) / (n * 0.7)
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
  -- (S20, R6) fear of the place: what people did to us and our neighbours here, unanswered
  local fs, fd, fe = 0, nil, nil
  if not need_only then fs, fd, fe = deeds.fear(W, s, hh.id, now) end
  if fs > 0 then
    local f = c.mig_fear_k * min(1, fs / c.fear_fu)
    if f >= 0.02 then
      push = push + f
      local o = fe and V.settlements[fe.by_s or 0]
      if fd.kind == "take" then
        why = why or (o and sfmt("the people of %s take our beasts", o.name) or "thieves come in the night")
      else
        why = why or (o and sfmt("the people of %s have shed our blood", o.name) or "our people have been struck down")
      end
    end
  end
  -- beasts lost to wolves lately
  if hh.dog_lost and floor(now / 24) - hh.dog_lost < 6 then
    push = push + 0.1
    why = why or "the wolves come to our pens"
  end
  return push, why
end

-- (S19) The meat on the hoof of village s's herds (FU), counted once a game hour (runtime).
local HOOF = setmetatable({}, { __mode = "k" })
function M.meat_on_hoof(W, s)
  local h = floor(W.hour)
  local e = HOOF[s]
  if e and e.h == h then return e.m end
  local stock = require("sim.agents.stock")
  local m = 0
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and not a.ox and (a.led or 0) == 0 then m = m + stock.SP(a.sp).meat end
    end
  end
  HOOF[s] = { h = h, m = m }
  return m
end

-- What draws household hh of village s to village o (by what s knows of o), and why.
function M.pull(W, s, hh, o, now)
  local c = C()
  local V = W.S.v3
  local pull, why = 0, nil
  -- (S19) a young place with no granary standing cannot feed newcomers: it draws only by a kin's
  -- offer of a place (a starving household moved to a camp two days old and died there)
  local off0 = hh.kin_offer
  if o.founding and not (o.granary and o.granary.state == "standing") and not (off0 and off0.sett == o.id) then
    return -1, nil
  end
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
  -- (S20, R6) never toward the people who harmed us (what we know of them, unanswered)
  local gv = deeds.grievance(W, s, o, hh.id, now)
  if gv > 0 then pull = pull - 2 * c.mig_fear_k * min(1, gv / c.fear_fu) end
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
    -- (S19: not a household already set on making a new place)
    if hh and #hh.members > 0 and (not hh.migrate or hh.migrate.given_up) and not (hh.found and not hh.found.given_up) then
      local push, pwhy = M.push(W, s, hh, now)
      -- (S19: hardship judged over the days, not by the morning: living hand to mouth on a sack of
      -- grain from a neighbour now and then is still hardship; a better day eases the count by one)
      hh.push_avg = hh.push_avg and (hh.push_avg * 0.6 + push * 0.4) or push
      hh.pushd = hh.push_avg >= c.mig_min_push and (hh.pushd or 0) + 1 or max(0, (hh.pushd or 0) - 1)
      -- (how long a household bears hardship before it thinks of leaving is its own)
      local patience = c.mig_days + floor(4 * rng.float(W.seed, rng.S.V3_THINK, hid, 0, 131))
      -- (S19) with nothing left to eat they do not wait out their patience: a day of it is enough
      -- (a starving household had waited three to seven days and died before it set out)
      if push >= c.mig_desperate then patience = 1 end
      if hh.pushd >= patience then
        partners = partners or trade.partners(W, s)
        -- everyone at home (none away on a trip, none on a boat) and a grown-up among them
        local home_all, grown = true, false
        for _, id in ipairs(hh.members) do
          local p = V.people[id]
          if p and p.alive then
            if p.at or p.trip or p.ferry or (p.boat or 0) ~= 0 or (p.riding or 0) ~= 0 then home_all = false end
            local st = people.stage(p, now)
            if st == "adult" or st == "elder" then grown = true end
          end
        end
        -- what holds them: their house and the years they have lived here (S17: and, for those who
        -- came to make this place, what they set out to do: they give it a season first)
        local years = (now - (hh.formed or 0)) / 1152
        local hold = c.mig_hold + ((hh.home or 0) ~= 0 and 0.15 or 0) + 0.05 * min(4, max(0, years))
        if hh.found and hh.found.arrived and now - hh.found.arrived < c.found_stay_days * 24 then hold = hold + c.found_stay_hold end
        local best, bu, bwhy, bsea = nil, 0, nil, nil
        for _, o in ipairs(partners) do
          if o.alive ~= false and o.id ~= s.id then
            local pull, why = M.pull(W, s, hh, o, now)
            local _, hrs = trade.road(W, s, o)
            local u = push + pull - hold - hrs * c.mig_road_hour
            if u > bu then best, bu, bwhy, bsea = o, u, why, nil end
          end
        end
        -- (S18) over the sea in their own boat, to villages they know of (S19: the boat may be out
        -- fishing this morning; and S19: a household with no boat asks one that has - a ferry)
        local own = econ.own_boat(s, hh, true)
        local fb = (not own) and s.dock and M.ferry_for(W, s, hh, now) or nil
        if own or fb then
          for _, o in ipairs(trade.sea_partners(W, s)) do
            if o.alive ~= false and o.id ~= s.id then
              local sp
              if own then sp = M.sea_plan(W, s, hh, o, now) else sp = M.ferry_plan(W, s, hh, o, now, fb) end
              if sp then
                local pull, why = M.pull(W, s, hh, o, now)
                local u = push + pull - hold - sp.hrs * c.mig_sea_hour - c.mig_sea_dread - sp.winter - sp.risk * c.mig_sea_risk_k
                  - (sp.fare or 0) * c.ferry_fare_k
                if u > bu then best, bu, bwhy, bsea = o, u, why, sp end
              end
            end
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
          if bsea then
            -- (by sea they set off in the morning, to make the most of the daylight on the water)
            local lh = clock.local_hour(depart, W.grid.lon[s.cell])
            depart = depart + (c.mig_sea_dawn - lh) % 24
          end
          local cause = require("sim.agents.deeds").top_harm(W, hh.id, now, 60)   -- (S21: the story's cause)
          hh.migrate = { to = best.id, from = s.id, depart = depart, decided = now, cause = cause and cause.id or nil,
            why = "Leaving " .. s.name .. " for " .. best.name .. (bsea and " by sea" or "") .. ": " .. (pwhy or "life is hard here") .. "; " .. (bwhy or ("we will try " .. best.name)) }
          if bsea then
            local boat = s.boats[bsea.boat]
            boat.move = hh.id
            hh.migrate.sea = { boat = bsea.boat, skipper = bsea.skipper, hrs = bsea.hrs, loads = bsea.loads, room = bsea.room,
              ferry = bsea.ferry, fare = bsea.fare }
            if bsea.ferry then
              -- (S19) a skipper of another household agrees to sail them over, and to come back
              local sk = V.people[bsea.skipper]
              sk.ferry = hh.id
              local q1 = V.people[hh.members[1] or 0]
              people.remember(sk, now, sfmt("I will sail the %ss over to %s in our boat%s", q1 and q1.surname or "neighbour", best.name,
                bsea.fare > 0 and " for some bread" or ""))
              hh.migrate.why = hh.migrate.why .. sfmt("; %s (%s) will sail us over", sk.name, bsea.ferry_why)
            end
          end
          hh.migrate.lead = M.share_beasts(W, s, hh, best, now, hh.migrate.sea)
          for _, id in ipairs(hh.members) do
            local p = V.people[id]
            if p and p.alive then
              people.remember(p, now, sfmt("We decided to leave %s for %s%s: %s", s.name, best.name, bsea and " by sea" or "", pwhy or "life is hard here"))
            end
          end
          s.acc.leaving = (s.acc.leaving or 0) + 1
        end
      end
    end
  end
end

-- (S18) How many of household hh's people live at village o (parents, children, partners).
local function kin_at(W, hh, o)
  local V = W.S.v3
  local kin = 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      for _, k in ipairs({ p.mother, p.father, p.partner }) do
        local q = V.people[k or 0]
        if q and q.alive and q.sett == o.id then kin = kin + 1 end
      end
      for _, k in ipairs(p.kids or {}) do
        local q = V.people[k]
        if q and q.alive and q.sett == o.id then kin = kin + 1 end
      end
    end
  end
  return kin
end
M.kin_at = kin_at

-- (S18) What household hh of village s would need to move to o over the sea, or nil: its own sound
-- boat at the dock, someone of it to sail it (a fisher, or a grown-up handy with boats), room in
-- the boat for all of it (boat_places: a grown-up a place, a child half, a babe in arms none), food
-- in its larder for the passage, and o a village it knows of (news heard, kin there, kin's offer).
-- Returns { boat, skipper, hrs, risk, winter, room (places left for beasts and goods), loads }.
-- (S18) Household hh of s and its own boat: the boat, who would sail it (a fisher, or a grown-up
-- handy with boats), the places its people take (a grown-up 1, a child half, a babe none) and its
-- mouths; nil when it has no sound boat, nobody to sail it or more people than the boat holds.
function M.boat_party(W, s, hh, now)
  local c = C()
  local V = W.S.v3
  local boat = econ.own_boat(s, hh, true)
  if not boat then return nil end
  local skipper, places, mouths = nil, 0, 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      local st = people.stage(p, now)
      if st == "adult" or st == "elder" or st == "apprentice" then places = places + 1
      elseif st == "child" then places = places + 0.5 end
      if st ~= "infant" then mouths = mouths + 1 end
      if (st == "adult" or st == "elder") and (p.role == "fisher" or (p.sk_fish or 0) >= c.skipper_skill)
        and (not skipper or (p.sk_fish or 0) > (skipper.sk_fish or 0)) then skipper = p end
    end
  end
  if not skipper or places > c.boat_places then return nil end
  return { boat = boat, skipper = skipper, places = places, mouths = mouths }
end

-- (S18) The chance household hh's boat founders on a passage of `hrs` hours, as they weigh it (in
-- the wet weather the sea mostly has).
function M.sea_dread(W, bp, hrs)
  local c = C()
  return hrs * c.sea_squall * c.sea_wet_k * (2 - (bp.boat.cond or 1)) * (1.3 - 0.6 * (bp.skipper.sk_fish or 0.3))
end

function M.sea_plan(W, s, hh, o, now)
  local c = C()
  local trade = require("sim.agents.trade")
  local bp = M.boat_party(W, s, hh, now)
  if not bp then return nil end
  local off = hh.kin_offer
  if not (s.know and s.know[o.id]) and kin_at(W, hh, o) == 0 and not (off and off.sett == o.id) then return nil end
  local route = trade.sea_route(W, s, o)
  if not route then return nil end
  local boat, skipper, places, mouths = bp.boat, bp.skipper, bp.places, bp.mouths
  local hrs = route.len / c.sail_speed
  -- (food for the passage: each walker's bread for its hours and a day more, from the larder)
  local home = s.buildings[hh.home or 0]
  local larder = home and ((home.stock.food or 0) + (home.stock.meals or 0) + (home.stock.smoked or 0) + (home.stock.cheese or 0)) or 0
  if larder < mouths * (hrs / 24 + 1) * 0.7 then return nil end
  local doy = require("sim.agents.clock").local_doy(now, W.grid.lat[s.cell])
  local room = c.boat_places - places
  -- (the risk they weigh: the passage in the wet weather the sea mostly has)
  return { boat = boat.id, skipper = skipper.id, hrs = hrs, risk = M.sea_dread(W, bp, hrs),
    winter = doy >= 36 and c.mig_sea_winter or 0, room = room, loads = 0 }
end

-- (S19) A ferry for household hh of village s, which has no boat of its own: a household of the
-- village with a sound boat and someone to sail it who would take them - kin first (an obligation
-- among kin), then friends (a grown-up of each fond of the other), then a household they have
-- traded with, then any neighbour (those for a fare) - not one moving or making a new place
-- itself; the skipper at home, free
-- (no babe in arms, none at the breast, no other passage promised); room in the boat for them and
-- the skipper. Returns { boat, skipper, ferry (household), places, mouths, why, fare } or nil.
function M.ferry_for(W, s, hh, now)
  local c = C()
  local V = W.S.v3
  local places, mouths = 0, 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      local st = people.stage(p, now)
      if st == "adult" or st == "elder" or st == "apprentice" then places = places + 1 elseif st == "child" then places = places + 0.5 end
      if st ~= "infant" then mouths = mouths + 1 end
    end
  end
  if places + 1 > c.boat_places then return nil end
  local best, bs = nil, 0
  for _, h2id in ipairs(s.households) do
    local h2 = V.households[h2id]
    if h2 and h2 ~= hh and #h2.members > 0 and not (h2.migrate and not h2.migrate.arrived and not h2.migrate.given_up)
      and not (h2.found and not h2.found.arrived and not h2.found.given_up) then
      local boat = econ.own_boat(s, h2, true)
      if boat then
        local sk = nil
        for _, id in ipairs(h2.members) do
          local p = V.people[id]
          local st = p and p.alive and people.stage(p, now)
          if (st == "adult" or st == "elder") and not p.trip and not p.at and not p.ferry and (p.babe or 0) == 0 and (p.nursing or 0) <= now
            and (p.role == "fisher" or (p.sk_fish or 0) >= c.skipper_skill)
            and (not sk or (p.sk_fish or 0) > (sk.sk_fish or 0)) then sk = p end
        end
        if sk then
          local kin, fond = false, 0
          for _, id in ipairs(hh.members) do
            local q = V.people[id]
            if q and q.alive then
              for _, id2 in ipairs(h2.members) do
                if people.is_kin(W, q, id2) then kin = true end
                local a = people.affinity(q, id2)
                if a > fond then fond = a end
              end
            end
          end
          local traded = (hh.partners and hh.partners[h2id] or 0) + (h2.partners and h2.partners[hh.id] or 0)
          local friends = fond >= c.ferry_fond
          -- (any neighbour would, for a fare; kin and friends first)
          local sc = c.ferry_any + (kin and 3 or 0) + (friends and 1 + fond or 0) + (traded > 0 and 0.5 or 0)
          if sc > bs then
            best, bs = { boat = boat, skipper = sk, ferry = h2, places = places + 1, mouths = mouths,
              why = kin and "our kin" or (friends and "our friend" or "for a fare"),
              fare = (not kin and not friends) and c.ferry_fare or 0 }, sc
          end
        end
      end
    end
  end
  return best
end

-- (S19) A move over the sea to village o in ferry fb (M.ferry_for): as sea_plan, the boat and the
-- skipper another household's.
function M.ferry_plan(W, s, hh, o, now, fb)
  local c = C()
  local trade = require("sim.agents.trade")
  if not fb then return nil end
  local off = hh.kin_offer
  if not (s.know and s.know[o.id]) and kin_at(W, hh, o) == 0 and not (off and off.sett == o.id) then return nil end
  local route = trade.sea_route(W, s, o)
  if not route then return nil end
  local hrs = route.len / c.sail_speed
  local home = s.buildings[hh.home or 0]
  local larder = home and ((home.stock.food or 0) + (home.stock.meals or 0) + (home.stock.smoked or 0) + (home.stock.cheese or 0)) or 0
  if larder < fb.mouths * (hrs / 24 + 1) * 0.7 then return nil end
  local doy = require("sim.agents.clock").local_doy(now, W.grid.lat[s.cell])
  return { boat = fb.boat.id, skipper = fb.skipper.id, hrs = hrs, risk = M.sea_dread(W, fb, hrs),
    winter = doy >= 36 and c.mig_sea_winter or 0, room = c.boat_places - fb.places, loads = 0,
    ferry = fb.ferry.id, ferry_why = fb.why, fare = fb.fare }
end

-- The beasts a migrating household takes (the kinds the other village keeps, each led by one of
-- its grown-ups) and gives to kin or neighbours (the rest): { [kind] = person id }. (S18) By sea
-- (`sea`): only a few small beasts, as many as the boat has places left (the breeding ewes and does
-- first, then the young; a young pig; never cattle or grown pigs): sea.ship[kind] = their keys;
-- the places they leave are for the household's goods (sea.loads, two loads a place).
function M.share_beasts(W, s, hh, o, now, sea)
  local stock = require("sim.agents.stock")
  local c = C()
  local V = W.S.v3
  local lead = {}
  local adults = {}
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    local st = p and p.alive and people.stage(p, now)
    if st == "adult" or st == "elder" or st == "apprentice" then adults[#adults + 1] = p end
  end
  local ai = 1
  -- (founders keep room first for the seed and bread of the first months: found_sea_loads loads)
  local room = sea and min(c.mig_sea_beasts, floor(sea.room - (sea.found and c.found_sea_loads * 0.5 or 0) + 1e-6)) or 0
  if sea then sea.ship = {} end
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    if H then
      local mine = {}
      for _, a in ipairs(H.animals) do
        if a.alive and a.owner == hh.id then mine[#mine + 1] = a end
      end
      local keeps = o.herds and o.herds[kind]
      if sea and #mine > 0 and keeps and kind ~= "cattle" and room > 0 then
        -- (what will go in the boat: small beasts, sound ones; the breeding females in their prime
        -- first, then the young; pigs only young)
        local cand = {}
        for _, a in ipairs(mine) do
          local S = stock.SP(a.sp)
          local age = stock.age(a, now)
          local ok = (a.yoke or 0) == 0 and (a.held or 0) == 0 and (a.led or 0) == 0 and a.health > 0.5 and not a.ox
          if kind == "swine" then ok = ok and age < S.young_years * 2 end
          if ok then
            local pri = (a.sex == "f" and age >= S.adult_years and age < S.old_years - 1) and 0 or (age < S.adult_years and 1 or 2)
            cand[#cand + 1] = { a = a, pri = pri, age = age }
          end
        end
        table.sort(cand, function(x, y)
          if x.pri ~= y.pri then return x.pri < y.pri end
          if x.age ~= y.age then return x.age < y.age end
          return x.a.k < y.a.k
        end)
        local ship = {}
        for i = 1, min(room, #cand) do ship[#ship + 1] = cand[i].a.k end
        if #ship > 0 and adults[ai] then
          sea.ship[kind] = ship
          room = room - #ship
          lead[kind] = adults[ai].id
          ai = ai + 1
          local set = {}
          for _, k in ipairs(ship) do set[k] = true end
          local rest = {}
          for _, a in ipairs(mine) do if not set[a.k] then rest[#rest + 1] = a end end
          mine = rest
        end
        keeps = false
      elseif sea then
        keeps = false
      end
      if #mine > 0 then
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
            for _, a in ipairs(mine) do
              a.owner = to
              a.why_owner = sfmt("Left with the %ss when its people went away", (V.people[V.households[to].members[1] or 0] or {}).surname or "neighbour")
            end
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
  if sea then
    -- (the places the beasts leave: the household's goods, two loads a place)
    sea.loads = min(c.boat_hold, floor(max(0, room + (sea.found and c.found_sea_loads * 0.5 or 0)) * 2 + 1e-6))
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

-- (S20, R6.5b) Household hh of village s lost its house (fire): it lodges with a household of s - kin
-- first - until a house of its own stands (s.waiting: settle plans it), else camps by the square.
function M.lodge(W, s, hh, now, why)
  local V = W.S.v3
  local host = host_of(W, s, hh)
  local q1 = V.people[hh.members[1] or 0]
  if host then
    hh.home, hh.lodge = host.home, host.id
    for _, id in ipairs(host.members) do
      local q = V.people[id]
      if q and q.alive then people.remember(q, now, sfmt("Took in the %s household: %s", q1 and q1.surname or "", why or "they have no roof")) end
    end
  else
    -- (S24) nobody left with a roof to take them in (a meteor, a great fire): a camp by the square made by their own
    -- hands in an hour or two - a fire, a lean-to, their larder (founding.plan_camp, as founders make theirs) - until a
    -- house of their own stands. With no camp a household had no hearth and no larder: it starved beside the granary.
    hh.lodge = 0
    local cb = M.plan_camp and not hh.camp_site and M.plan_camp(W, s, hh, now)
    if not cb then
      local fx, fy = require("sim.agents.map").free_spot(s, s.sqx or 0, s.sqy or 0, 6)
      hh.camp = { x = fx or (s.sqx or 0), y = fy or (s.sqy or 0) }
    end
  end
  s.waiting = s.waiting or {}
  local listed = false
  for _, id in ipairs(s.waiting) do if id == hh.id then listed = true end end
  if not listed then s.waiting[#s.waiting + 1] = hh.id end
end

-- (S24) Building b of village s is gone (burnt, fallen in): every household that lived in it - its own and those
-- lodging there - is without a roof and lodges anew (its own household first; `except` handled by the caller).
function M.unroof(W, s, b, now, why, except)
  local V = W.S.v3
  local list = {}
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and hh ~= except and #hh.members > 0 and hh.home == b.id then list[#list + 1] = hh end
  end
  for _, hh in ipairs(list) do
    hh.home = 0
    hh.lodge = nil
    M.lodge(W, s, hh, now, why)
  end
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

-- (S18) A beast of household hh that will not come with it (strayed from the drove, too far from
-- the boat): left with kin who stay, else the household staying with most beasts of its kind.
function M.leave_beast(W, s, hh, a, kind, now)
  local V = W.S.v3
  local H = s.herds and s.herds[kind]
  local to, best = 0, -1
  for _, hid in ipairs(s.households) do
    local h2 = V.households[hid]
    if hid ~= hh.id and h2 and #h2.members > 0 and not (h2.migrate and not h2.migrate.arrived and not h2.migrate.given_up) and not h2.found then
      local n = 0
      for _, b in ipairs(H and H.animals or {}) do if b.alive and b.owner == hid then n = n + 1 end end
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q then for _, qid in ipairs(h2.members) do if people.is_kin(W, q, qid) then n = n + 100 end end end
      end
      if n > best then to, best = hid, n end
    end
  end
  if to == 0 then return end
  local q = V.people[V.households[to].members[1] or 0]
  a.owner = to
  a.why_owner = sfmt("Left with the %ss when its people went away without it", q and q.surname or "neighbour")
end

-- (S18) A boat changes its home: the same boat (its wear, its trips, who owns it, what is in its
-- hold) is moored at o's dock from now and counted among o's boats; its entry at the old village
-- is marked moved (never used again, so nothing that pointed at it finds another boat). The food in
-- its hold leaves the old village's books and enters o's.
function M.move_boat(W, from, o, boat, hh, now)
  o.boats = o.boats or {}
  local k = #o.boats + 1
  local d = o.dock or o.beach
  local nb = { id = k, x = d and d.mx + (k - 1) * 0.012 or boat.x, y = d and d.my or boat.y, crew = 0, owner = hh and hh.id or 0,
    cond = boat.cond, trips = boat.trips, built = boat.built, cargo = boat.cargo,
    came = { from = from.id, t = now, old = boat.id,
      why = (o.founding and o.founding.by_sea and "Sailed here by its people, come to make " or "Sailed here by its people, come to live at ") .. o.name } }
  o.boats[k] = nb
  local food = 0
  for _, g in ipairs(econ.ORDER) do
    if econ.GOODS[g].food then food = food + (boat.cargo and boat.cargo[g] or 0) end
  end
  if food > 0 then
    from.acc.moved_out = (from.acc.moved_out or 0) + food
    o.acc.moved_in = (o.acc.moved_in or 0) + food
  end
  boat.lost, boat.owner, boat.crew, boat.mate, boat.away, boat.cargo, boat.riders, boat.move = true, -1, 0, 0, nil, nil, nil, nil
  boat.moved = { to = o.id, k = k, t = now }
  return nb
end

-- (S18) The skipper of household come by sea lands at village o's dock: the household's boat is
-- o's from now (move_boat); the skipper and everyone of the household ashore become o's people
-- (arrive); the beasts they brought are lifted out and go to o's herd.
function M.land(W, o, p, boat, now)
  local V = W.S.v3
  local stock = require("sim.agents.stock")
  local hh = V.households[p.hh]
  local from = V.settlements[p.sett]
  local mg = hh and hh.migrate
  local d = o.dock
  local gx, gy = d and d.dx or p.x, d and d.dy or p.y
  local nb = M.move_boat(W, from, o, boat, hh, now)
  if mg and mg.sea then mg.sea.boat2 = nb.id end
  local list = { p }
  for _, id in ipairs(hh and hh.members or {}) do
    local q = V.people[id]
    if q and q.alive and q ~= p and q.at == o.id and people.stage(q, now) ~= "infant" then list[#list + 1] = q end
  end
  for _, q in ipairs(list) do
    M.arrive(W, o, q, gx, gy, now)
    local tr = q.trip
    local kind = tr and tr.lead_kind
    if kind and tr.party and #tr.party > 0 then
      stock.join_map(W, o, q, kind, gx, gy, now)
      local L, H = stock.led_by(o, q, kind)
      for _, a in ipairs(L) do
        stock.release(W, o, H, a, now, "Come over the sea to " .. o.name .. " with its people: to the herd")
        a.why_owner = sfmt("Brought over the sea from %s by its people", from and from.name or "afar")
      end
      tr.led = {}
    end
  end
  for _, q in ipairs(list) do people.remember(q, now, sfmt("Landed at %s after %s", o.name, from and ("the crossing from " .. from.name) or "the crossing")) end
end

-- (S19) Skipper p (of another household) lands the household it ferried at village o's dock: they
-- step ashore and are o's people (arrive); the boat stays the skipper's household's (no change of
-- home); the fare, when one was agreed, is paid from their packs; then the skipper sails home - the
-- trip becomes a voyage done with nothing to deal (sea_legs takes it home).
function M.land_ferry(W, o, p, boat, now)
  local c = C()
  local V = W.S.v3
  local stock = require("sim.agents.stock")
  local tr = p.trip
  local hh = V.households[tr.migrate or 0]
  local from = V.settlements[p.sett]
  local mg = hh and hh.migrate
  local d = o.dock or o.beach
  local gx, gy = d and d.dx or p.x, d and d.dy or p.y
  if boat then boat.move = nil end
  local list = {}
  for _, id in ipairs(hh and hh.members or {}) do
    local q = V.people[id]
    if q and q.alive and q.at == o.id and people.stage(q, now) ~= "infant" then list[#list + 1] = q end
  end
  local fare = mg and mg.sea and mg.sea.fare or 0
  for _, q in ipairs(list) do
    M.arrive(W, o, q, gx, gy, now)
    local qt = q.trip
    local kind = qt and qt.lead_kind
    if kind and qt.party and #qt.party > 0 then
      stock.join_map(W, o, q, kind, gx, gy, now)
      local L, H = stock.led_by(o, q, kind)
      for _, a in ipairs(L) do
        stock.release(W, o, H, a, now, "Come over the sea to " .. o.name .. " with its people: to the herd")
        a.why_owner = sfmt("Brought over the sea from %s by its people", from and from.name or "afar")
      end
      qt.led = {}
    end
    -- (the fare: bread from their packs into the skipper's)
    if fare > 1e-3 and q.pack and (q.pack.grain or 0) > 0 then
      local f = min(fare, q.pack.grain)
      q.pack.grain = q.pack.grain - f
      p.pack = p.pack or {}
      p.pack.grain = (p.pack.grain or 0) + f
      fare = fare - f
      o.acc.export = (o.acc.export or 0) + f
      if from then from.acc.import = (from.acc.import or 0) + f end
    end
    people.remember(q, now, sfmt("Landed at %s: %s sailed us over and goes home", o.name, p.name))
  end
  -- (a passage given: remembered between the households as a gift is)
  local ferry = V.households[p.hh]
  if hh and ferry and (mg and mg.sea and mg.sea.fare or 0) <= 0 then
    hh.got = hh.got or {}
    hh.got[ferry.id] = (hh.got[ferry.id] or 0) + c.ferry_worth
    ferry.gave = ferry.gave or {}
    ferry.gave[hh.id] = (ferry.gave[hh.id] or 0) + c.ferry_worth
  end
  local q1 = hh and V.people[hh.members[1] or 0]
  people.remember(p, now, sfmt("Sailed the %ss over to %s: now home", q1 and q1.surname or "neighbour", o.name))
  if from then from.acc.ferries = (from.acc.ferries or 0) + 1 end
  p.ferry = nil
  p.trip = { to = o.id, from = p.sett, sea = true, boat = boat and boat.id or 0, landed = true, dealt = true, deals = {},
    give = "grain", take = "grain", n = 0, t0 = now, ferried = true }
  if p.pack and (p.pack.grain or 0) > 0 then p.carry = "pack" end
end

-- Setting off (p takes the road): every babe of the household is in the arms of one of those
-- going - its mother if she can, else p, else another grown-up of the household.
function M.take_babes(W, s, p, now)
  local V = W.S.v3
  local hh = V.households[p.hh]
  if not hh then return end
  local kin = require("sim.agents.kin")
  -- (S19) a bride (or a child going with its mother) leaving alone takes only her own babes, not the
  -- household's
  local alone = p.trip and p.trip.move
  for _, id in ipairs(hh.members) do
    local ib = V.people[id]
    if ib and ib.alive and people.stage(ib, now) == "infant" and (not alone or ib.mother == p.id) then
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
  -- (S19) one come after their household: of it still, at its new village
  if p.move and p.move.follow then
    p.move.arrived = now
    local hh0 = V.households[p.hh]
    if hh0 and hh0.sett == o.id then
      people.remember(p, now, sfmt("Came to %s, where my household had gone to live", o.name))
      require("sim.agents.founding").tell(W, p, o)
      return
    end
  end
  -- (S19) a child come with its mother joins her new household (her husband's)
  local child = p.move and p.move.mother and p.partner == 0
  local g = V.people[(child and p.move.groom) or p.partner or 0]
  local hh = g and g.alive and V.households[g.hh]
  if hh then people.join(W, p, hh) end
  -- (S19) a babe in her arms comes with her, into the same household
  local ib = V.people[p.babe or 0]
  if ib and ib.alive and ib.sett ~= o.id then
    if from then people.remove_id(from.members, ib.id) end
    o.members[#o.members + 1] = ib.id
    ib.sett, ib.at, ib.x, ib.y = o.id, nil, gx, gy
    if hh then people.join(W, ib, hh) end
  end
  if not child then
    o.couples = o.couples or {}
    o.couples[#o.couples + 1] = p.id
    o.acc.brides_in = (o.acc.brides_in or 0) + 1
  end
  if p.move then p.move.arrived = now end
  if child then
    local m = V.people[p.move.mother or 0]
    people.remember(p, now, sfmt("Came to %s with %s, to live with %s", o.name, m and m.name or "my mother", g and g.name or "her husband"))
  else
    people.remember(p, now, sfmt("Came to %s to live with %s", o.name, g and g.name or "my husband"))
  end
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
  if mg and not mg.arrived and mg.sea then
    -- (S18) by sea
    local now = V.now or W.hour
    local sk = V.people[mg.sea.skipper or 0]
    local skn = sk and sk.name or "our skipper"
    if p.act == "passage" or (p.riding or 0) ~= 0 then return mg.why .. ": at sea in " .. skn .. "'s boat" end
    if p.act == "voyage" then return mg.why .. ": sailing the household over to " .. oname end
    if p.boat then return mg.why .. ": rowing the household out" end
    if tr and tr.lead_kind and now < mg.depart then return mg.why .. ": bringing our beasts to the boat" end
    if now < mg.depart then
      if sk == p then return mg.why .. ": loading our goods into the boat" end
      return mg.why .. ": packing and going down to the dock"
    end
    return mg.why .. ": waiting at the dock to sail with " .. skn
  end
  if mg and not mg.arrived then
    local now = V.now or W.hour
    if tr and tr.lead_kind and now < mg.depart then return mg.why .. ": bringing our beasts to the gate" end
    if now < mg.depart then return mg.why .. ": packing and meeting the others at the gate" end
    return mg.why .. ": on the road to " .. oname .. " with the household"
  end
  if p.move and tr and tr.move then
    if p.move.mother then
      local m = V.people[p.move.mother]
      return "Come to " .. oname .. " with " .. (m and m.name or "my mother") .. " to live"
    end
    local g = V.people[p.partner or 0]
    return "Come to " .. oname .. " to live with " .. (g and g.name or "my husband")
  end
  local host = hh and hh.lodge and V.households[hh.lodge]
  local hq = host and V.people[host.members[1] or 0]
  if mg and mg.sea and p.carry == "pack" then return "Come to live at " .. oname .. " by sea: carrying our things up from the boat" end
  if hq then return "Come to live at " .. oname .. ": settling in with the " .. (hq.surname or "") .. "s until our own house stands" end
  return "Come to live at " .. oname .. ": settling in"
end

return M
