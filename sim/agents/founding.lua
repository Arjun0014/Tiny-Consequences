-- Founding a new place (roadmap v3 R5, S17). No village is made out of nothing: a household (and
-- those who choose to go with it) leaves its village for a place its people know, walks there with
-- what it can carry and its beasts, and makes the place by work - a camp, water, a first shelter, a
-- field cleared and sown, then a granary, a woodstore and pens as there is something to keep in them.
--
-- Who and why. A household founds when staying is bad and nowhere it knows would take it in: its
--   hardship has lasted and no village it knows of pulls it enough (migrate.daily marks it), or a
--   couple has waited many days for a house with no lot left to build on, or the village has no room
--   left for the fields it needs. Kin and others in the same plight may go with them, each by its
--   own weighing.
-- Where. Only places people of the village have seen: the country about (years of hunting and
--   herding), the land either side of every road walked (trade, courting, kin visits, droves,
--   relief, moves), what visitors and hosts tell each other (s.lore, never the planet itself). A
--   place is judged by its water (a river, the shore, ground for a well), soil, wood, grass, climate,
--   roughness and wolves, as it was when seen; old sightings are trusted less; known villages keep
--   their country (found_min_steps).
-- The way. The party walks the land route (trade.land_route: over the planet's cells, round steep
--   and high ground and thick forest, rivers forded), camping at night, at the slowest one's pace
--   (the drove's when beasts are led).
-- The place. The settlement exists from the moment the first of them walks onto its map (settle.new:
--   the land, rivers, coast and trees only); its centre is chosen by them (open dry ground near
--   water); each household makes a camp (`camp`: the ground cleared, a fire ring, a lean-to - its
--   home and larder until a house stands; the beasts kept by it at night); water is fetched from the
--   river or a well dug; huts are built, a field cleared and sown, a granary raised before the
--   harvest, a woodstore and pens; trees on a lot are felled first (settle.lot).
-- (S18) By sea. A household with its own boat and someone to sail it may go to a place on a shore
--   its people saw from the sea (or heard of), over the water: the boat holds the household, a few
--   small beasts, its share of the granary and some goods; the place exists from the moment they
--   sail into its waters; they land on a beach reachable from the open sea (o.beach) and make the
--   place as by land; the boat is drawn up there, and a dock is built when the place has roofs.
--   s.lore[cell] = { t = hour seen, how = text }      p.seen = { { cell, t, how } }
--   hh.found = { site, from, depart, decided, why, swhy, hrs, slow, lead = { kind = person id },
--                party = { hh ids }, leader, share, arrived }
--   o.founding = { from, party = { hh ids }, started, leader, settled (camp stood), name_why }
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local sfmt = require("sim.fmt").format
local people = require("sim.agents.people")
local mapm = require("sim.agents.map")
local settle = require("sim.agents.settle")
local trade = require("sim.agents.trade")
local econ = require("sim.agents.econ")
local stock = require("sim.agents.stock")
local clock = require("sim.agents.clock")
local found = require("sim.agents.found")
local sites = require("sim.society.sites")
local names = require("content.names")
local dmath = require("sim.dmath")

local M = {}
local floor, max, min, sqrt, abs, exp = math.floor, math.max, math.min, math.sqrt, math.abs, dmath.exp
local function C() return cfgmod.V3 end

-- Knowledge of places ---------------------------------------------------------------------------

-- The village's knowledge of places (the country about it, known since it was founded: years of
-- hunting, herding and gathering; made the first time it is wanted).
function M.lore(W, s)
  local L = s.lore
  if L then return L end
  L = {}
  local ocean = W.geo.ocean
  for _, c in ipairs(sites.rings(W, s.cell, C().lore_home)) do
    if ocean[c] == 0 then L[c] = { t = s.founded or 0, how = "the country about " .. s.name } end
  end
  s.lore = L
  return L
end

local function see(L, cell, t, how)
  local e = L[cell]
  if not e then L[cell] = { t = t, how = how }
  elseif e.t < t then e.t, e.how = t, how end
end

-- p walked (or sailed past) the way from cell a to cell b: the cells along it and either side are
-- seen (p.seen, told at home or wherever they come to live).
local SEEN_MAX = 80
local function note(W, p, cell, t, how)
  local S = p.seen
  if not S then S = {}; p.seen = S end
  for i = 1, #S do
    local e = S[i]
    if e[1] == cell then e[2], e[3] = t, how; return end
  end
  if #S >= SEEN_MAX then table.remove(S, 1) end
  S[#S + 1] = { cell, t, how }
end
function M.note_leg(W, p, a, b, how, now)
  if a == b then return end
  local r = trade.land_route(W, a, b)
  local cells = r and r.cells
  if not cells then return end
  local ocean = W.geo.ocean
  local side = C().lore_side
  for i = 1, #cells do
    for _, c in ipairs(sites.rings(W, cells[i], side)) do
      if ocean[c] == 0 then note(W, p, c, now, how) end
    end
  end
end
-- (from the sea: the land along the shore of the way sailed)
function M.note_sea(W, p, route, how, now)
  local cells = route and route.cells
  if not cells then return end
  local ocean = W.geo.ocean
  local g = W.grid
  for i = 1, #cells do
    local c0 = cells[i]
    for k = 1, g.nbr_count[c0] do
      local j = g.nbr[c0 * 6 - 6 + k]
      if j > 0 and ocean[j] == 0 then note(W, p, j, now, how) end
    end
  end
end

-- (S18) A hunter or herder of village s has been out in the country about it: what is there is
-- known afresh (the home country's sightings renewed, never the planet beyond it).
function M.roamed(W, s, p, now)
  local L = M.lore(W, s)
  local how = "the country about " .. s.name .. " (" .. (p.name or "a hunter") .. " was out in it lately)"
  for _, c in ipairs(sites.rings(W, s.cell, C().lore_home)) do
    local e = L[c]
    if e and e.t < now - 24 then e.t, e.how = now, how end
  end
end

-- p tells village s what they saw (home again, or come to live there).
function M.tell(W, p, s)
  local S = p.seen
  if not S or #S == 0 then return end
  local L = M.lore(W, s)
  for i = 1, #S do
    local e = S[i]
    see(L, e[1], e[2], e[3])
  end
  p.seen = nil
end

-- At a meeting (a trader, a visitor, a bride's people): village s hears what the people of o know
-- of places (S17; econ.learn's hook).
function M.share(W, s, o, now)
  if s == o then return end
  local Lo = M.lore(W, o)
  local L = M.lore(W, s)
  local keys = {}
  for c in pairs(Lo) do keys[#keys + 1] = c end
  table.sort(keys)
  for i = 1, #keys do
    local e = Lo[keys[i]]
    see(L, keys[i], e.t, e.how)
  end
end

-- Sites ------------------------------------------------------------------------------------------

-- River cells (the planet's, as the view draws them), once a planet day (runtime).
local RIV = setmetatable({}, { __mode = "k" })
local function rivers(W)
  local e = RIV[W]
  if e and e.tick == W.tick then return e.r end
  local r = found.river_cells(W)
  RIV[W] = { tick = W.tick, r = r }
  return r
end
M.rivers = rivers

-- The cells too near a village s knows of (its own; those it has news of; those it has seen).
local function near_known(W, s)
  local V = W.S.v3
  local L = M.lore(W, s)
  local near = {}
  local r = C().found_min_steps - 1
  for _, o in ipairs(V.settlements) do
    if o.alive ~= false then
      local seen = L[o.cell] and L[o.cell].t >= (o.founded or 0)
      if o == s or (s.know and s.know[o.id]) or seen then
        for _, c in ipairs(sites.rings(W, o.cell, r)) do near[c] = true end
      end
    end
  end
  return near
end
M.near_known = near_known

-- What a place is worth to household hh of village s, as it was seen: water, soil, wood, grass,
-- climate, roughness and wolves; returns score (nil when it will not do), a line saying why.
function M.site_value(W, s, cell, hh, now, near)
  local c = C()
  local geo, F = W.geo, W.F
  local ocean = geo.ocean
  if ocean[cell] == 1 then return nil end
  local R = rivers(W)
  if R[cell] or (near and near[cell]) then return nil end
  local L = M.lore(W, s)
  local e = L[cell]
  if not e then return nil end
  local ring = sites.rings(W, cell, 1)
  local land, fo, fe, gr, sw, sea, riv = 0, 0, 0, 0, 0, 0, 0
  for _, d in ipairs(ring) do
    if ocean[d] == 1 then sea = sea + 1
    else
      land = land + 1
      fo = fo + (F.forest[d] or 0)
      fe = fe + (F.fertility and F.fertility[d] or 0.5)
      gr = gr + (F.grass[d] or 0)
      sw = sw + (F.soil_water[d] or 0)
      if R[d] then riv = riv + 1 end
    end
  end
  if land < 4 then return nil end
  fo, fe, gr, sw = fo / land, fe / land, gr / land, sw / land
  local TS = W.temp_static
  local T = (TS and TS.base and TS.base[cell]) and (TS.base[cell] + TS.lapse[cell] + (W.G.g_temp or 0)) or F.temp[cell]
  local fisher = false
  local V = W.S.v3
  for _, id in ipairs(hh and hh.members or {}) do
    local q = V.people[id]
    if q and q.alive and q.role == "fisher" then fisher = true end
  end
  local water = riv > 0 and 1 or (sea > 0 and (fisher and 0.9 or 0.45) or 0)
  water = max(water, min(1, sw * 1.6) * 0.6)
  local soil = min(1, fe)
  local wood = min(1, fo / 0.5)
  local climate = max(0, 1 - abs(T - 13) / 10)
  local rough = geo.slope and geo.slope[cell] or 0
  local wolves = F.predators and F.predators[cell] or 0
  local sc = 0.6 * water + 0.6 * soil + 0.35 * wood + 0.25 * min(1, gr * 1.5) + 0.5 * climate - 0.3 * rough - 0.6 * wolves
  -- (what was seen long ago is trusted less)
  local fresh = exp(-(now - e.t) / (c.lore_fade * 24))
  sc = sc * (0.6 + 0.4 * fresh)
  local good = {}
  if riv > 0 then good[#good + 1] = "a river" elseif sea > 0 then good[#good + 1] = "the shore" elseif water > 0.3 then good[#good + 1] = "wet ground for a well" end
  if soil > 0.55 then good[#good + 1] = "good soil" end
  if wood > 0.5 then good[#good + 1] = "woods" end
  if gr > 0.4 then good[#good + 1] = "grass" end
  local why = (#good > 0 and table.concat(good, ", ") or "open land") .. " (" .. e.how .. ")"
  return sc, why, { water = water, soil = soil, wood = wood, climate = climate, wolves = wolves, riv = riv, sea = sea }
end

-- The best place household hh of s knows of: cell, its worth less the walk, hours, why.
function M.best_site(W, s, hh, now)
  local c = C()
  local L = M.lore(W, s)
  local near = near_known(W, s)
  local keys = {}
  for cell in pairs(L) do keys[#keys + 1] = cell end
  table.sort(keys)
  local best, bu, bh, bwhy, bv = nil, -1e9, 0, nil, 0
  for i = 1, #keys do
    local cell = keys[i]
    local v, why = M.site_value(W, s, cell, hh, now, near)
    if v then
      local r = trade.land_route(W, s.cell, cell)
      if r and r.hrs <= c.found_reach_h then
        local u = v * c.found_site_k - r.hrs * c.found_road_hour
        if u > bu then best, bu, bh, bwhy, bv = cell, u, r.hrs, why, v end
      end
    end
  end
  return best, bu, bh, bwhy, bv
end

-- (S18) The best place on a shore household hh of s knows of that its own boat could take it to:
-- { cell, u (worth less the passage and the sea's dread), hrs, why, v, bp } or nil.
function M.best_sea_site(W, s, hh, now)
  local c = C()
  if not s.dock then return nil end
  local migrate = settle.migrate
  local bp = migrate and migrate.boat_party(W, s, hh, now)
  if not bp then return nil end
  local L = M.lore(W, s)
  local near = near_known(W, s)
  local keys = {}
  for cell in pairs(L) do keys[#keys + 1] = cell end
  table.sort(keys)
  local best = nil
  for i = 1, #keys do
    local cell = keys[i]
    local v, why, parts = M.site_value(W, s, cell, hh, now, near)
    if v and parts.sea > 0 then
      local r = trade.sea_route_to(W, s, cell)
      if r and r.len <= c.sea_reach then
        local hrs = r.len / c.sail_speed
        local u = v * c.found_site_k - hrs * c.found_road_hour - c.mig_sea_dread - migrate.sea_dread(W, bp, hrs) * c.mig_sea_risk_k
        if not best or u > best.u then best = { cell = cell, u = u, hrs = hrs, why = why, v = v, bp = bp } end
      end
    end
  end
  if best then
    -- (what the boat can take besides them: a few small beasts, the rest of the room for loads)
    local room = c.boat_places - bp.places
    local small, left = 0, 0
    for _, kind in ipairs(stock.HERDS) do
      local H = s.herds and s.herds[kind]
      for _, a in ipairs(H and H.animals or {}) do
        if a.alive and a.owner == hh.id then
          local G = econ.GOODS[a.sp]
          if kind ~= "cattle" and (kind ~= "swine" or stock.age(a, now) < stock.SP(a.sp).young_years * 2) and small < min(room, c.mig_sea_beasts) then
            small = small + 1
          else
            left = left + (G and G.base or 10)
          end
        end
      end
    end
    best.hold_fu = min(c.boat_hold, max(0, room - small) * 2) * 10
    best.left_k = c.found_beast_k * left / 100
  end
  return best
end

-- How daunting setting out is now: a place reached with no sowing before winter means living a
-- season on what was carried, gathered and hunted.
function M.season_risk(W, s, now)
  local c = C()
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  -- (time to clear a field before a sowing window closes: about ten days)
  if doy <= c.sow_season[2] - 8 or (doy >= c.sow_winter[1] - 10 and doy <= c.sow_winter[2] - 8) then return 0 end
  if doy >= 38 then return c.found_risk_winter end
  return c.found_risk_late
end

-- Who founds -------------------------------------------------------------------------------------

-- What pushes household hh of s to found a place, and why: hardship that has lasted with nowhere to
-- go (migrate.daily: hh.no_refuge), a couple of it waiting long with no lot to build on, no room
-- left for the fields the village needs.
local function waiting_couple(W, s, hh)
  local V = W.S.v3
  for _, wid in ipairs(s.couples or {}) do
    local a = V.people[wid]
    if a and a.alive and a.hh == hh.id and not a.house_site then
      local b = V.people[a.partner or 0]
      if b and b.alive and b.hh == hh.id then return a, b end
    end
  end
  return nil
end
function M.push(W, s, hh, now)
  local c = C()
  local push, why = 0, nil
  local day = floor(now / 24)
  if hh.no_refuge and day - hh.no_refuge <= 1 then
    push = push + (hh.mig_push or 0.4)
    why = hh.mig_why or "life is hard here and no village we know would take us in"
  end
  local a = waiting_couple(W, s, hh)
  if a and (s.no_lot or 0) >= c.found_lot_days then
    push = push + c.found_lot_push + c.found_lot_day * (s.no_lot - c.found_lot_days)
    why = why or sfmt("%s and %s have waited %d days for a house and there is no ground left to build on", a.name, W.S.v3.people[a.partner].name, s.no_lot)
  end
  if (s.no_field_room or 0) >= c.found_lot_days then
    push = push + c.found_land_push
    why = why or "the village wants more fields and there is no ground left to clear"
  end
  return push, why
end

-- The household's grown-ups and whether everyone is at home.
local function ready(W, hh, now)
  local V = W.S.v3
  local c = C()
  local home_all, grown = true, 0
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      if p.at or p.trip or (p.boat or 0) ~= 0 or (p.riding or 0) ~= 0 then home_all = false end
      -- (S19) not with a woman about to give birth, nor with a babe only days old
      if (p.preg_due or 0) > 0 and p.preg_due - now < c.found_due_days * 24 then home_all = false end
      if people.stage(p, now) == "infant" and now - p.born < c.found_due_days * 24 then home_all = false end
      local st = people.stage(p, now)
      if st == "adult" or st == "elder" then grown = grown + 1 end
    end
  end
  return home_all, grown
end

-- What holds household hh at s: its house, its years there.
local function hold(W, s, hh, now)
  local c = C()
  local years = (now - (hh.formed or 0)) / 1152
  local h = c.mig_hold + ((hh.home or 0) ~= 0 and 0.15 or 0) + 0.05 * min(4, max(0, years))
  if hh.found and hh.found.arrived and now - hh.found.arrived < c.found_stay_days * 24 then h = h + c.found_stay_hold end
  return h
end

-- What household hh could take with it (its larder and its part of the granary, as much as its
-- grown-ups can carry) against the bread its mouths want until a new place feeds them: the shortfall
-- weighs against going (0 none .. 1 nothing to take).
local function want_food(W, s, hh, now, grown, hold_fu)
  local c = C()
  local V = W.S.v3
  local home = s.buildings[hh.home or 0]
  local larder = home and ((home.stock.food or 0) + (home.stock.meals or 0) + (home.stock.smoked or 0)) or 0
  local gr = s.granary and (s.granary.stock.grain or 0) or 0
  local share = gr * c.found_share * #hh.members / max(1, s.pop or #s.members)
  -- (S18: by sea the boat's hold carries more than backs can)
  local carry = min(larder + share, max(1, grown) * c.found_carry + #hh.members * c.trade_prov * 3 + (hold_fu or 0))
  local need = #hh.members * c.found_food_days * 0.75
  return max(0, 1 - carry / max(1, need)), carry
end
M.want_food = want_food

-- Each morning (settle.daily, after migrate.daily): households that would found a place.
function M.daily(W, s, now)
  local c = C()
  local V = W.S.v3
  now = V.now or W.hour
  if (s.pop or #s.members) < 4 then return end
  local list = {}
  for _, hid in ipairs(s.households) do list[#list + 1] = hid end
  for _, hid in ipairs(list) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and not hh.migrate and (not hh.found or hh.found.given_up) and hh.sett == s.id then
      local push, pwhy = M.push(W, s, hh, now)
      if push > 0 then
        local cell, su, hrs, swhy = M.best_site(W, s, hh, now)
        local home_all, grown = ready(W, hh, now)
        -- (S18) over the sea in their own boat (not a couple setting up on their own: the boat is
        -- their parents')
        local sea = nil
        local couple0 = waiting_couple(W, s, hh)
        local short = nil
        if not (couple0 and (pwhy or ""):find("waited")) then
          sea = M.best_sea_site(W, s, hh, now)
          if sea then
            -- (what each way means: by sea the hold takes more bread and seed, but only a few small
            -- beasts can come - the rest are left behind)
            local short_l = want_food(W, s, hh, now, grown)
            local short_s = want_food(W, s, hh, now, grown, sea.hold_fu)
            local ul = cell and (su - c.found_hungry_k * short_l) or -1e9
            local us = sea.u - c.found_hungry_k * short_s - sea.left_k
            if us > ul then
              cell, su, hrs, swhy = sea.cell, sea.u - sea.left_k, sea.hrs, sea.why .. ", over the sea"
              short = short_s
            else
              sea = nil
            end
          end
        end
        if cell and home_all then
          local couple_a = waiting_couple(W, s, hh)
          -- (a couple with no lot founds as a household of its own: their grown-ups are theirs)
          if (pwhy or ""):find("waited") and couple_a then grown = 2 end
          short = short or want_food(W, s, hh, now, grown)
          local u = push + su - hold(W, s, hh, now) * (couple_a and pwhy and pwhy:find("waited") and 0.4 or 1) - M.season_risk(W, s, now)
            - c.found_hungry_k * short
          local noise = rng.float(W.seed, rng.S.V3_THINK, hid, floor(now / 24) % 4294967296, 140)
          hh.found_u = u
          if grown >= 1 and short <= c.found_short_max and u > c.found_threshold * (0.8 + 0.4 * noise) then
            M.decide(W, s, hh, cell, hrs, pwhy or "life is hard here", swhy, now, couple_a ~= nil and (pwhy or ""):find("waited") ~= nil, sea)
          end
        end
      end
    end
  end
end

-- The beasts a founding household takes (every kind it keeps, each led by one of its grown-ups or
-- apprentices; a kind nobody is left to lead goes to kin who stay): { [kind] = person id }.
local function lead_beasts(W, s, hh, now)
  local V = W.S.v3
  local lead = {}
  local hands = {}
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    local st = p and p.alive and people.stage(p, now)
    if st == "adult" or st == "elder" or st == "apprentice" then hands[#hands + 1] = p end
  end
  local ai = 1
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    if H then
      local mine = false
      for _, a in ipairs(H.animals) do
        if a.alive and a.owner == hh.id and (a.led or 0) == 0 then mine = true; break end
      end
      if mine then
        local p = hands[ai]
        if p then
          lead[kind] = p.id
          ai = ai + 1
        end
      end
    end
  end
  -- (kinds with nobody to lead them: left with kin or neighbours, as a moving household does)
  local migrate = settle.migrate
  if migrate then
    local fake = { herds = {} }
    for k in pairs(lead) do fake.herds[k] = true end
    migrate.share_beasts(W, s, hh, fake, now)
  end
  return lead
end

-- Household hh of s decides to found a place at `cell` (with `couple`: the waiting couple of it go
-- as a household of their own). Others in the same plight, kin first, may decide to go with them.
function M.decide(W, s, hh, cell, hrs, pwhy, swhy, now, couple, sea)
  local c = C()
  local V = W.S.v3
  if sea then return M.decide_sea(W, s, hh, cell, hrs, pwhy, swhy, now, sea) end
  if couple then
    -- (the couple leave the household they live in and go as their own)
    local a, b = waiting_couple(W, s, hh)
    -- (until they leave they still live, eat and sleep in the house they lived in: lodging there)
    local nh = people.new_household(W, s.id, hh.home or 0)
    nh.lodge = hh.id
    s.households[#s.households + 1] = nh.id
    nh.pack_from = hh.home    -- (they make ready in the house they lived in, with their part of its larder)
    for _, q in ipairs({ a, b }) do
      people.join(W, q, nh)
      people.remember(q, now, "Leaving my father's house: we will make a home of our own")
    end
    for i = #s.couples, 1, -1 do if s.couples[i] == a.id or s.couples[i] == b.id then table.remove(s.couples, i) end end
    hh = nh
  end
  local route = trade.land_route(W, s.cell, cell)
  local party = { hh.id }
  -- (others who would go too: households pushed as this one is, kin of it first, each weighing it)
  local cands = {}
  for _, hid in ipairs(s.households) do
    local h2 = V.households[hid]
    if h2 and h2 ~= hh and #h2.members > 0 and not h2.migrate and not h2.found then
      local kin = 0
      for _, id in ipairs(hh.members) do
        local p = V.people[id]
        if p then for _, qid in ipairs(h2.members) do if people.is_kin(W, p, qid) then kin = kin + 1 end end end
      end
      local push2 = M.push(W, s, h2, now)
      local home_all, grown = ready(W, h2, now)
      local u = push2 + min(0.3, 0.15 * kin) + (route and 0 or -1)
        + (M.site_value(W, s, cell, h2, now) or 0) * c.found_site_k - hrs * c.found_road_hour
        - hold(W, s, h2, now) - M.season_risk(W, s, now) - c.found_hungry_k * want_food(W, s, h2, now, grown)
      if home_all and grown >= 1 and u > c.found_threshold * 0.9 then cands[#cands + 1] = { h = h2, u = u, kin = kin } end
    end
  end
  table.sort(cands, function(x, y) if x.u ~= y.u then return x.u > y.u end return x.h.id < y.h.id end)
  for i = 1, min(#cands, c.found_party - 1) do party[#party + 1] = cands[i].h.id end
  -- the granary's share for those going: by their mouths
  local mouths = 0
  for _, hid in ipairs(party) do mouths = mouths + #V.households[hid].members end
  local pop = max(1, s.pop or #s.members)
  local gr = s.granary and (s.granary.stock.grain or 0) or 0
  -- (they set off in the morning after a day of making ready)
  local lh = clock.local_hour(now + c.found_pack_h, W.grid.lon[s.cell])
  local depart = now + c.found_pack_h + (c.found_dawn_h - lh) % 24
  local slow = false
  for k, hid in ipairs(party) do
    local h2 = V.households[hid]
    local lead = lead_beasts(W, s, h2, now)
    if next(lead) then slow = true end
    h2.found = { site = cell, from = s.id, depart = depart, decided = now, hrs = hrs, leader = hh.id,
      why = sfmt("Leaving %s to make a new home: %s", s.name, pwhy), swhy = swhy, lead = lead, party = {},
      share = gr * c.found_share * #h2.members / pop }
    for _, x in ipairs(party) do h2.found.party[#h2.found.party + 1] = x end
    local lq = V.people[hh.members[1] or 0]
    for _, id in ipairs(h2.members) do
      local p = V.people[id]
      if p and p.alive then
        if k == 1 then
          people.remember(p, now, sfmt("We decided to leave %s and make a new home at a place we know of: %s", s.name, swhy))
        else
          people.remember(p, now, sfmt("We will go with the %ss to make a new home: %s", lq and lq.surname or "others", swhy))
        end
      end
    end
  end
  for _, hid in ipairs(party) do V.households[hid].found.slow = slow end
  s.acc.founders = (s.acc.founders or 0) + #party
  if W.record_events then
    require("sim.causality.events").emit(W, "founding_planned", s.cell, 0, 0, { settlement = s.id, site = cell })
  end
end

-- (S18) Household hh of s decides to make a new home at the shore at `cell`, over the sea in its
-- own boat (sea: best_sea_site's). The boat holds this household only; it takes a few small beasts
-- (the rest left with kin), its part of the granary and some goods in the hold.
function M.decide_sea(W, s, hh, cell, hrs, pwhy, swhy, now, sea)
  local c = C()
  local V = W.S.v3
  local bp = sea.bp
  local mouths = #hh.members
  local pop = max(1, s.pop or #s.members)
  local gr = s.granary and (s.granary.stock.grain or 0) or 0
  local lh = clock.local_hour(now + c.found_pack_h, W.grid.lon[s.cell])
  local depart = now + c.found_pack_h + (c.mig_sea_dawn - lh) % 24
  local fd = { site = cell, from = s.id, depart = depart, decided = now, hrs = hrs, leader = hh.id, party = { hh.id },
    why = sfmt("Leaving %s to make a new home over the sea: %s", s.name, pwhy), swhy = swhy,
    share = gr * c.found_share * mouths / pop,
    sea = { boat = bp.boat.id, skipper = bp.skipper.id, hrs = hrs, room = c.boat_places - bp.places, loads = 0, found = true } }
  hh.found = fd
  bp.boat.move = hh.id
  -- (the beasts: those the boat can take; the new place keeps any kind they bring)
  fd.lead = settle.migrate.share_beasts(W, s, hh, { herds = { flock = true, cattle = true, swine = true } }, now, fd.sea)
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      people.remember(p, now, sfmt("We decided to leave %s and sail to a place we know of to make a new home: %s", s.name, swhy))
    end
  end
  s.acc.founders = (s.acc.founders or 0) + 1
  if W.record_events then
    require("sim.causality.events").emit(W, "founding_planned", s.cell, 0, 0, { settlement = s.id, site = cell, by_sea = true })
  end
end

-- (S18) Founders sailing in to the shore at `cell` from village `from`: the place is made when they
-- come into its waters (as by land when the first of them walks onto it), with a landing on its
-- shore that the boat can reach from the open sea (gate_c: the sea cell they came by). Returns it.
function M.sea_place(W, cell, p, from, gate_c, now)
  local V = W.S.v3
  local hh = V.households[p.hh]
  local fd = hh and hh.found
  local o = M.place_at(W, cell)
  if not o then
    o = M.new_place(W, cell, { culture = from and from.culture or 1, from = from and from.id or 0, leader = fd and fd.leader or 0 })
    o.founding.name_why = sfmt("Named by the %ss, come over the sea from %s", p.surname or "founder", from and from.name or "afar")
    o.founding.by_sea = true
  end
  if not o.beach then o.beach = M.beach(W, o, gate_c) end
  return o
end

-- (S18) Where a boat can be drawn up on place o's shore: a dry land tile with open water before it
-- that the open sea reaches (the water from the sea gate toward gate_c), land behind it; the one
-- nearest the map's middle. { dx, dy (the shore), mx, my (the water before it), ix, iy, side }.
function M.beach(W, o, gate_c)
  local N = o.N
  local gx, gy = trade.sea_gate(W, o, gate_c or o.cell)
  if not gx then return nil end
  local g0 = mapm.tile_of(o, gx, gy)
  local wet = { [g0] = true }
  local q, head = { g0 }, 1
  while head <= #q do
    local i = q[head]; head = head + 1
    local ix, iy = (i - 1) % N + 1, floor((i - 1) / N) + 1
    for d = 1, 4 do
      local jx, jy = ix + (d == 1 and 1 or (d == 2 and -1 or 0)), iy + (d == 3 and 1 or (d == 4 and -1 or 0))
      if jx >= 2 and jy >= 2 and jx <= N - 1 and jy <= N - 1 then
        local j = (jy - 1) * N + jx
        if not wet[j] and o.wcost[j] > 0 and o.land[j] == 0 then wet[j] = true; q[#q + 1] = j end
      end
    end
  end
  local D4 = { { 0, -1, "n" }, { 0, 1, "s" }, { -1, 0, "e" }, { 1, 0, "w" } }
  local best, bd, bw, bside = 0, 1e18, 0, nil
  for iy = 4, N - 3 do
    for ix = 4, N - 3 do
      local i = (iy - 1) * N + ix
      if o.land[i] == 1 and o.occ[i] == 0 and o.riv[i] == 0 and o.cost[i] > 0 then
        for _, d in ipairs(D4) do
          local wi = (iy + d[2] - 1) * N + ix + d[1]
          local w2 = (iy + 2 * d[2] - 1) * N + ix + 2 * d[1]
          local li = (iy - d[2] - 1) * N + ix - d[1]
          if wet[wi] and wet[w2] and o.land[li] == 1 and o.riv[li] == 0 then
            local x, y = mapm.ixy_xy(o, ix, iy)
            local r = x * x + y * y
            if r < bd then best, bd, bw, bside = i, r, wi, d[3] end
          end
        end
      end
    end
  end
  if best == 0 then return nil end
  local x, y = mapm.tile_xy(o, best)
  local mx, my = mapm.tile_xy(o, bw)
  local ix, iy = mapm.tile_ixy(o, best)
  return { dx = x, dy = y, mx = mx, my = my, ix = ix, iy = iy, side = bside }
end

-- (S18) The skipper of founders come by sea has landed at the shore of place o: their boat is the
-- place's now (drawn up at the landing), and everyone of the household ashore is its people (arrive);
-- the beasts they brought are lifted out and kept by their camp.
function M.land_sea(W, o, p, boat, now)
  local V = W.S.v3
  local hh = V.households[p.hh]
  local from = V.settlements[p.sett]
  local fd = hh and hh.found
  local ld = o.beach
  local gx, gy = ld and ld.dx or p.x, ld and ld.dy or p.y
  local nb = settle.migrate.move_boat(W, from, o, boat, hh, now)
  if fd and fd.sea then fd.sea.boat2 = nb.id end
  local list = { p }
  for _, id in ipairs(hh and hh.members or {}) do
    local q = V.people[id]
    if q and q.alive and q ~= p and q.at == o.id and people.stage(q, now) ~= "infant" then list[#list + 1] = q end
  end
  for _, q in ipairs(list) do
    if o.visitors then people.remove_id(o.visitors, q.id) end
    M.arrive(W, o.cell, q, now, gx, gy)
    local tr = q.trip
    local kind = tr and tr.lead_kind
    if kind and tr.party and #tr.party > 0 then
      stock.join_map(W, o, q, kind, gx, gy, now)
      local L, H = stock.led_by(o, q, kind)
      for _, a in ipairs(L) do
        stock.release(W, o, H, a, now, "Come over the sea with its people: kept by their camp")
        a.why_owner = sfmt("Brought over the sea from %s by its people", from and from.name or "afar")
      end
      tr.led = {}
    end
  end
  for _, q in ipairs(list) do people.remember(q, now, sfmt("Landed on the shore of %s after the crossing from %s", o.name, from and from.name or "home")) end
end

-- The party arrives ------------------------------------------------------------------------------

-- An empty map at `cell` (S17): the land, rivers, coast and the untouched woods, the grazing ground
-- and the wild food of the woods' edge; no building, no field, nobody. opts: name, culture, from.
function M.new_place(W, cell, opts)
  local V = W.S.v3
  local key = rng.hash(W.seed, rng.S.V3_FOUND, cell, 7, floor(W.hour))
  local n = 0
  local function u()
    n = n + 1
    return rng.float(W.seed, rng.S.V3_FOUND, key, n, 1)
  end
  local o = settle.new(W, cell, { name = opts.name or names.village(u, opts.culture or 1), culture = opts.culture or 1 })
  o.founded = W.hour
  o.empty = true
  found.woods(W, o, u, 0)
  mapm.rebuild_costs(o)
  -- (the ground about the map's middle, on its side of any river too deep to wade: where they will
  -- settle, and where they come onto the map)
  do
    local N = o.N
    local c0 = floor(N / 2) + 1
    local i0 = (c0 - 1) * N + c0
    for r = 0, 20 do
      local i = (c0 - 1) * N + c0 + r
      if o.land[i] == 1 and o.riv[i] == 0 then i0 = i; break end
    end
    mapm.home_bank(o, i0)
  end
  o.herds = {}
  stock.pasture_init(W, o, u)
  o.hay = 0
  settle.wild_food(W, o, floor(W.hour / 24), 0.9)
  o.founding = { from = opts.from or 0, started = W.hour, party = {}, leader = opts.leader or 0 }
  -- (the knowledge of places the founders bring)
  o.lore = {}
  local from = V.settlements[opts.from or 0]
  if from then
    for c0, e in pairs(M.lore(W, from)) do o.lore[c0] = { t = e.t, how = e.how } end
    -- (they came from there: they know it)
    o.know = {}
    econ.learn(W, o, from, W.hour)
    econ.rel(o, from).trust = 0.6
    econ.rel(from, o).trust = 0.6
  end
  -- (the roads and sea ways known so far are worked out again with the new place)
  trade.forget(W)
  return o
end

-- The settlement founded at `cell` by a party still on its way or arrived (or nil).
local function place_at(W, cell)
  for _, o in ipairs(W.S.v3.settlements) do
    if o.cell == cell and o.alive ~= false and o.founding then return o end
  end
  return nil
end
M.place_at = place_at

-- The centre: the spot within reach of the map's middle with water nearest (a wadeable river bank),
-- open ground about it, dry, on land; the square there; each household's camp round it, on ground
-- with room (trees may stand on a camp's ground: a camp needs no clearing).
function M.lay_out(W, o, now, gx, gy)
  local c = C()
  local N = o.N
  local c0 = floor(N / 2) + 1
  -- (only ground they can walk to from where they came onto the map)
  local reach = nil
  local g0 = gx and mapm.tile_of(o, gx, gy) or 0
  if g0 > 0 then
    reach = { [g0] = true }
    local q, head = { g0 }, 1
    local cost = o.cost
    while head <= #q do
      local i = q[head]; head = head + 1
      local ix, iy = (i - 1) % N + 1, floor((i - 1) / N) + 1
      for d = 1, 4 do
        local jx, jy = ix + (d == 1 and 1 or (d == 2 and -1 or 0)), iy + (d == 3 and 1 or (d == 4 and -1 or 0))
        if jx >= 1 and jy >= 1 and jx <= N and jy <= N then
          local j = (jy - 1) * N + jx
          if not reach[j] and cost[j] > 0 then reach[j] = true; q[#q + 1] = j end
        end
      end
    end
  end
  local best, bs = nil, -1e9
  for iy = c0 - 16, c0 + 16, 2 do
    for ix = c0 - 16, c0 + 16, 2 do
      local i = (iy - 1) * N + ix
      if o.land[i] == 1 and o.riv[i] == 0 and o.occ[i] == 0 and (not reach or reach[i]) then
        local x, y = mapm.ixy_xy(o, ix, iy)
        local bi, bd = mapm.nearest_bank(o, x, y)
        local wd = bi and bi > 0 and bd or 2
        local trees = 0
        for jy = iy - 3, iy + 3 do
          for jx = ix - 3, ix + 3 do
            local j = (jy - 1) * N + jx
            if o.tree[j] > 0 and o.trees.st[o.tree[j]] == 1 then trees = trees + 1 end
            if o.land[j] == 0 then trees = trees + 4 end
          end
        end
        local sc = -min(wd, 1.2) * 1.5 - trees * 0.05 - sqrt(x * x + y * y) * 0.6 + (wd < 0.08 and -1 or 0)
        if sc > bs then best, bs = i, sc end
      end
    end
  end
  if not best and reach then
    -- (no ground near the middle they can walk to: the reachable dry ground nearest it)
    local bd = 1e18
    for i in pairs(reach) do
      if o.land[i] == 1 and o.riv[i] == 0 and o.occ[i] == 0 then
        local x, y = mapm.tile_xy(o, i)
        local d = x * x + y * y
        if d < bd or (d == bd and i < best) then best, bd = i, d end
      end
    end
  end
  best = best or ((c0 - 1) * N + c0)
  local bx, by = mapm.tile_ixy(o, best)
  o.sqx, o.sqy = mapm.ixy_xy(o, bx, by)
  -- (until there are stores, wood and grain are reckoned from where they settle)
  o.wood_x, o.wood_y, o.granary_x, o.granary_y = o.sqx, o.sqy, o.sqx, o.sqy
  mapm.home_bank(o, best)
  o.empty = nil
  o.laid_out = now
end

-- A camp site for household hh at o, near the square (open ground first).
function M.plan_camp(W, o, hh, now)
  local N = o.N
  local cx, cy = mapm.tile_of(o, o.sqx, o.sqy), 0
  local ix0, iy0 = mapm.tile_ixy(o, cx)
  local best, bx, by, bs = nil, 0, 0, 1e9
  for r = 3, 22 do
    for iy = iy0 - r, iy0 + r do
      for ix = ix0 - r, ix0 + r do
        if (abs(ix - ix0) == r or abs(iy - iy0) == r) and mapm.rect_free(o, ix - 1, iy - 1, ix + 5, iy + 5, true) then
          local trees = 0
          for jy = iy, iy + 4 do
            for jx = ix, ix + 4 do
              local k = o.tree[(jy - 1) * N + jx]
              if k > 0 and o.trees.st[k] == 1 then trees = trees + 1 end
            end
          end
          local sc = r + trees * 2
          if sc < bs then best, bx, by, bs = true, ix, iy, sc end
        end
      end
    end
    if best and bs < r + 1 then break end
  end
  if not best then return nil end
  local q = W.S.v3.people[hh.members[1] or 0]
  local b = settle.add_building(W, o, "camp", bx, by, "s", "site",
    { for_hh = hh.id, for_name = "the " .. (q and q.surname or "newcomer") .. "s", prio = 3, keep_trees = true })
  hh.camp_site = b.id
  return b
end

-- Person p of a founding household walks onto the place's map at `cell`: the place is made when
-- the first of them comes (an empty map), laid out, and each household's camp planned; from here
-- on they are its people. Returns the place.
function M.arrive(W, cell, p, now, lx, ly)
  local V = W.S.v3
  local hh = V.households[p.hh]
  local fd = hh and hh.found
  local from = V.settlements[p.sett]
  local o = place_at(W, cell)
  if not o then
    -- (the name the founders give it, in their own tongue)
    o = M.new_place(W, cell, { culture = from and from.culture or 1, from = from and from.id or 0, leader = fd and fd.leader or 0 })
    o.founding.name_why = sfmt("Named by the %ss, come from %s", p.surname or "founder", from and from.name or "afar")
  end
  if not o.laid_out then
    local gx, gy = lx, ly
    if not gx then gx, gy = require("sim.agents.wild").edge_toward(W, o, from and from.cell or o.cell) end
    M.lay_out(W, o, now, gx, gy)
    -- (the place's board: what there is to do here, as its first people see it)
    settle.hourly(W, o, now)
  end
  -- p and the babes they carry are o's now (and the food on them)
  if from and from ~= o then
    settle.carry_over(from, o, p)
    people.remove_id(from.members, p.id)
    if from.visitors then people.remove_id(from.visitors, p.id) end
  end
  local here = false
  for _, id in ipairs(o.members) do if id == p.id then here = true end end
  if not here then o.members[#o.members + 1] = p.id end
  p.sett, p.at = o.id, nil
  local ib = V.people[p.babe or 0]
  if ib and ib.alive and ib.sett ~= o.id then
    if V.settlements[ib.sett] then people.remove_id(V.settlements[ib.sett].members, ib.id) end
    o.members[#o.members + 1] = ib.id
    ib.sett, ib.at = o.id, nil
  end
  if hh and hh.sett ~= o.id then
    local old = V.settlements[hh.sett]
    if old then
      people.remove_id(old.households, hh.id)
      local b = old.buildings[hh.home or 0]
      if b and b.hh == hh.id then b.hh = 0; b.for_name = "the " .. (p.surname or "people") .. "s, who went to make " .. o.name end
      old.acc.left = (old.acc.left or 0) + 1
      settle.reassign_plots(W, old, hh.id)
    end
    hh.sett, hh.home, hh.lodge = o.id, 0, nil
    o.households[#o.households + 1] = hh.id
    o.founding.party[#o.founding.party + 1] = hh.id
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and q.sett ~= o.id and people.stage(q, now) == "infant" then
        if V.settlements[q.sett] then people.remove_id(V.settlements[q.sett].members, q.id) end
        o.members[#o.members + 1] = q.id
        q.sett, q.at = o.id, nil
      end
    end
    if fd then fd.arrived = now end
    M.plan_camp(W, o, hh, now)
    -- (their dog came with them)
    local gx, gy = lx, ly
    if not gx then gx, gy = require("sim.agents.wild").edge_toward(W, o, from and from.cell or o.cell) end
    require("sim.agents.dogs").come(W, o, hh, gx, gy, now)
    o.waiting = o.waiting or {}
    o.waiting[#o.waiting + 1] = hh.id
    o.acc.arrived = (o.acc.arrived or 0) + 1
  end
  -- (the beasts this one leads: the herd of their kind is kept by the household's camp until a pen
  -- is built)
  local tr = p.trip
  local kind = tr and tr.lead_kind
  if kind then
    local H = o.herds[kind]
    local camp = o.buildings[hh and hh.camp_site or 0]
    if not H and camp then
      o.herds[kind] = { kind = kind, animals = {}, pen = camp.id, herder = 0, out = false, rack = 0, manure = 0, sick = 0 }
    end
  end
  people.remember(p, now, sfmt("Came to the place we chose and called it %s", o.name))
  M.tell(W, p, o)
  return o
end

-- The young place ---------------------------------------------------------------------------------

-- Water within reach of the camps: a wadeable river bank near the square.
function M.water_near(W, o)
  local bi, bd = mapm.nearest_bank(o, o.sqx or 0, o.sqy or 0)
  return bi and bi > 0 and bd <= C().found_water_reach
end

-- Each morning at a young place: what its people see wanting, in the order needs come: water (a
-- well when no river is near), the stores (a granary before the first harvest, a woodstore once
-- the houses stand or winter nears), pens for the beasts kept at the camps.
function M.place_daily(W, o, now)
  if not o.founding or not o.laid_out then return end
  local c = C()
  local V = W.S.v3
  -- camps struck: nobody lives at them, their things carried away, no herd kept by them
  local struck = {}
  for _, id in ipairs(o.blist) do
    local b = o.buildings[id]
    if b.kind == "camp" and b.state == "standing" then
      local lived = false
      for _, hid in ipairs(o.households) do
        local hh = V.households[hid]
        if hh and #hh.members > 0 and (hh.home == b.id or hh.camp == b.id) then lived = true end
      end
      local left = 0
      for _, k in ipairs({ "food", "seed", "meals", "meat", "fish", "milk", "smoked", "cheese", "logs", "hides", "leather", "wool", "cloth" }) do
        left = left + (b.stock[k] or 0)
      end
      local pen = false
      for _, kind in ipairs(stock.HERDS) do
        local H = o.herds and o.herds[kind]
        if H and H.pen == b.id then pen = true end
      end
      if not lived and left < 0.05 and not pen then struck[#struck + 1] = b end
    end
  end
  for _, b in ipairs(struck) do
    settle.remove_building(W, o, b)
    for _, hid in ipairs(o.households) do
      local hh = V.households[hid]
      if hh and hh.old_camp == b.id then hh.old_camp = nil end
    end
  end
  local open = 0
  for _, id in ipairs(o.sites) do
    local b = o.buildings[id]
    if b and b.kind ~= "camp" then open = open + 1 end
  end
  if o.lot then open = open + 1 end
  local homeless = 0
  for _, hid in ipairs(o.households) do
    local hh = V.households[hid]
    local b = hh and o.buildings[hh.home or 0]
    if hh and #hh.members > 0 and not (b and b.kind == "house") then homeless = homeless + 1 end
  end
  -- water
  if not o.well and not o.well_planned and not M.water_near(W, o) then
    if settle.plan_lot(W, o, "well", { prio = 2, for_name = "the village" }, o.sqx, o.sqy) then
      o.well_planned = now
    end
  end
  if open >= 2 then return end
  -- the granary: before the first harvest (a field sown), or when food is carried in to keep
  if not o.granary and not o.granary_planned then
    local sown = false
    for _, pl in ipairs(o.plots) do if pl.stage == "growing" or pl.stage == "ripe" or pl.sown > 0 then sown = true end end
    if sown then
      if settle.plan_lot(W, o, "granary", { prio = 2, for_name = "the village", logs_need = 14, work_need = 80 }, o.sqx, o.sqy) then
        o.granary_planned = now
        open = open + 1
      end
    end
  end
  if open >= 2 then return end
  -- the woodstore: once every household has a roof, or winter comes near
  local doy = clock.local_doy(now, W.grid.lat[o.cell])
  if not o.wood_store and not o.wood_planned and (homeless == 0 or doy >= 30) then
    if settle.plan_lot(W, o, "woodstore", { prio = 1, for_name = "the village" }, o.sqx, o.sqy) then
      o.wood_planned = now
      open = open + 1
    end
  end
  if open >= 2 then return end
  -- (S18) a hut lived in long enough, the place's stores standing: a proper house in its place's
  -- village, built by the household; the hut is left for whoever needs a roof next
  if o.granary and o.wood_store and not o.lot then
    for _, hid in ipairs(o.households) do
      local hh = V.households[hid]
      local hut = hh and o.buildings[hh.home or 0]
      if hut and hut.hut and hut.state == "standing" and not hh.house_site and not hh.lodge and now - (hut.built or now) >= c.found_hut_days * 24 then
        local q = V.people[hh.members[1] or 0]
        local r = settle.plan_lot(W, o, "house", { for_hh = hh.id, for_name = "the " .. (q and q.surname or "settler") .. "s", prio = 1, replaces = hut.id },
          hut.cx, hut.cy, 4)
        if r then
          for _, id in ipairs(hh.members) do
            local p = V.people[id]
            if p and p.alive and people.stage(p, now) ~= "infant" then people.remember(p, now, "We will build a proper house in place of our hut") end
          end
          return
        end
      end
    end
  end
  -- (S18) a dock for the boat they came in, once the households have roofs: on the landing
  if not o.dock and not o.dock_planned and o.beach and homeless == 0 then
    local boat = false
    for _, b in ipairs(o.boats or {}) do if not b.lost then boat = true end end
    local ld = o.beach
    if boat and mapm.rect_free(o, ld.ix, ld.iy, ld.ix, ld.iy, true) then
      settle.add_building(W, o, "dock", ld.ix, ld.iy, ld.side, "site", { prio = 1, for_name = "the boats" })
      o.dock_planned = now
      open = open + 1
    end
  end
  if open >= 2 then return end
  -- pens: for each herd still kept at a camp, once the households have roofs (or wolves came)
  for _, kind in ipairs(stock.HERDS) do
    local H = o.herds and o.herds[kind]
    local pen = H and o.buildings[H.pen or 0]
    if H and (not pen or pen.kind == "camp") and not H.pen_planned and (homeless == 0 or (o.wolf_t and now - o.wolf_t < 72)) then
      if settle.plan_lot(W, o, stock.PEN[kind], { prio = 1, for_name = "the " .. kind, herd = kind }, o.sqx, o.sqy, 10) then
        H.pen_planned = now
        return
      end
    end
  end
end

-- (S20) A village whose pen is gone (burnt, fallen in): a new one planned for the herd, as at a
-- young place; the beasts are kept where it stood meanwhile.
function M.replan_pens(W, o, now)
  for _, kind in ipairs(stock.HERDS) do
    local H = o.herds and o.herds[kind]
    local pen = H and o.buildings[H.pen or 0]
    if H and pen and pen.state == "gone" and not H.pen_planned and (H.n_alive or 1) > 0 then
      if settle.plan_lot(W, o, stock.PEN[kind], { prio = 1, for_name = "the " .. kind, herd = kind }, o.sqx, o.sqy, 10) then
        H.pen_planned = now
        return
      end
    end
  end
end

-- Why p (of household hh) is doing what the founding asks of them now.
function M.why(W, p, hh)
  local V = W.S.v3
  local fd = hh and hh.found
  if not fd then return "Making a new home" end
  local now = V.now or W.hour
  local from = V.settlements[fd.from]
  if not fd.arrived and fd.sea then
    -- (S18) by sea
    local sk = V.people[fd.sea.skipper or 0]
    local skn = sk and sk.name or "our skipper"
    if p.act == "passage" or (p.riding or 0) ~= 0 then return fd.why .. ": at sea in " .. skn .. "'s boat" end
    if p.act == "voyage" then return fd.why .. ": sailing the household over to the place we chose" end
    if p.boat then return fd.why .. ": rowing the household out" end
    if (p.at or p.sett) == fd.from then
      local tr = p.trip
      if tr and tr.lead_kind and now < fd.depart then return fd.why .. ": bringing our beasts down to the boat" end
      if now < fd.depart then
        if sk == p then return fd.why .. ": loading seed and goods into the boat" end
        return fd.why .. ": packing and going down to the dock"
      end
      return fd.why .. ": waiting at the dock to sail with " .. skn
    end
  end
  if not fd.arrived and (p.at or p.sett) == fd.from then
    local tr = p.trip
    if now < fd.depart then
      if tr and tr.lead_kind then return fd.why .. ": bringing our beasts to the gate" end
      return fd.why .. ": packing what we can carry and meeting the others at the gate"
    end
    return fd.why .. ": on the way to the place we chose (" .. (fd.swhy or "") .. ")"
  end
  local o = V.settlements[p.sett]
  local camp = o and o.buildings[hh.home or 0]
  if camp and camp.kind == "camp" then return "Making a home at " .. (o and o.name or "the new place") .. ": living at our camp until a house stands" end
  return "Making a home at " .. (o and o.name or "the new place")
end

return M
