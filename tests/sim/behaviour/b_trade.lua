-- B15 (a trade disruption propagates, session 5): a village alone on its island, short of food
-- before the harvest, buys grain over the sea with what it can spare (its own trade decision,
-- relations.trade). The boat bringing the grain home is struck by Lightning at sea. Compared with
-- a control run in which the grain arrives (no famine logic anywhere; every step is the ordinary
-- economy):
--   B15a  a village with cloth left to sell: its store is short by about the cargo, it has a
--         boat fewer (or built one again), and it buys again at its next reckoning where the
--         control, its cargo home, does not — paying with more of its cloth; its partner sells more
--         of its grain (the loss passes along the route). The village's friends over the water hold
--         nothing against it (the premise; D-074: a village that took much and gave little is
--         helped by no one).
--   B15b  a village that counts on the grain: it can pay for one real cargo and then has nothing
--         more to sell (only what it makes later), and its store lasts to the harvest only with the grain on its way (as the
--         village itself reckons it: economy.daily). The premise is set up, not searched for — the
--         island villages of these worlds fish two-thirds of their food and can pay for only a few
--         days of grain, so no village here depends on bought grain by itself. The control does
--         not ration; with the loss it goes hungrier, its rationing and the relief its friends send
--         descend from the boat's loss (lightning > boat_lost > shipment_lost > rationing >
--         relief), and its people hunt and gather more wild food.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local relations = require("sim.society.relations")
local farming = require("sim.society.farming")
local schedule = require("sim.society.schedule")
local sea = require("sim.society.sea")
local M = {}

-- A village with boats that has no other village on its landmass and a village over the water
-- within trading reach.
local function lone_island(W)
  local reach = cfgmod.SOCIETY.trade_reach
  for _, v in ipairs(W.ent.villages) do
    if v.alive and v.mooring then
      local alone, over = true, false
      for _, u in ipairs(W.ent.villages) do
        if u ~= v and u.alive then
          if u.continent == v.continent then alone = false
          else
            local d = sea.cost(W, v, u.cell, cfgmod.SOCIETY.porters)
            if d and d <= reach then over = true end
          end
        end
      end
      if alone and over then return v end
    end
  end
end

local function chain_has(W, id, kind)
  if not id then return false end
  local ch = events.chain(W, id)
  for i = 1, #ch do if ch[i].kind == kind then return true end end
  return false
end

local function kinds_of(W, id)
  local k = {}
  if id then for _, e in ipairs(events.chain(W, id)) do k[#k + 1] = e.kind end end
  return table.concat(k, ">")
end

local WEEKS = 9

-- One run: the village has `days` of food in store and `cloth` more cloth to sell.
-- The first of these worlds that has such a village (the mechanism does not depend on which).
-- counted (B15b): after the purchase it has nothing more to sell, and its store is what lasts to
-- the harvest only with the cargo counted on.
local SEEDS = { 22, 9, 3, 1, 5, 7, 2, 4 }
local function island_world()
  for _, seed in ipairs(SEEDS) do
    local W = worlds.fresh(seed)
    local A = lone_island(W)
    if A then return W, A, seed end
  end
end

local function run(strike, days, cloth, net, counted)
  local W, A = island_world()
  T.ok(A, "a village alone on its island with partners over the water")
  -- (the premise: a village with friends over the water — what it took from them in the past and
  -- any grudge between them are forgotten, D-074: a village that took much and gave little is
  -- helped by no one, and B21 tests that)
  for _, u in ipairs(W.ent.villages) do
    local r = u ~= A and relations.get(W, A.id, u.id)
    if r then r.gab, r.gba, r.ga, r.gb = 0, 0, 0, 0 end
  end
  if counted then
    -- (three months or so before its harvest: a cargo the porters can carry is a real share of what
    -- the village eats until then)
    local guard = 0
    while farming.days_to_harvest(W, A) > 90 and guard < 60 do world.tick(W); guard = guard + 1 end
  end

  -- (net: `days` of what the village eats beyond its usual catch — a store that must run short
  -- without the cargo, however much the village fishes)
  local share = net and math.min(0.9, (A.norm and A.norm.catch_in or 0) / math.max(1, A.need_day * 7)) or 0
  A.stock.grain = A.need_day * days * (1 - share) - A.stock.fresh
  A.food_days = days   -- (as the day step reckons it)
  A.stock.cloth = (A.stock.cloth or 0) + cloth
  relations.trade(W, A)
  local party
  for _, id in ipairs(W.ent.party_list or {}) do
    local p = W.ent.parties[id]
    if p and p.from == A.id and p.kind == "porters" then party = p end
  end
  T.ok(party and party.cargo.get == "food" and party.si ~= nil, "it sends porters over the sea to buy grain")
  if counted then
    local cfg = cfgmod.SOCIETY
    -- nothing more to sell: what is left beyond its own reserve of every good
    for _, g in ipairs(relations.GOODS) do
      if g ~= "food" then
        local sp = relations.spare_of(W, A, g)
        local k = relations.STOCK[g]
        if sp > 0 then A.stock[k] = A.stock[k] - sp end
      end
    end
    -- the store that lasts to the harvest (4% to spare) only with the grain on its way counted
    local need, dth = A.need_day, farming.days_to_harvest(W, A)
    local income = math.min(need, A.norm.fresh_in / 7 * cfg.income_trust)
    local buffer = need * cfg.ration_safety_days + (need - income) * math.min(dth, cfg.ration_margin_days) * 0.5
    local q = party.cargo.want
    T.ok(q * cfg.expect_trust > 0.08 * need * dth, string.format("the cargo (%.0f) is a real share of what it eats to the harvest (%.0f)", q, need * dth))
    A.stock.grain = need * dth * 1.04 - income * dth + buffer - q * cfg.expect_trust - A.stock.fresh
    T.ok(A.stock.grain > 0, "a store to start with")
  end
  local ret, struck, got, food_home, lost_boat, next_boat
  local deficit = 0   -- sum over days of (1 - ration): how much less people ate
  local buys, seen = 0, {}   -- purchases of grain set off over the run (the village trades monthly:
                             -- a lost cargo is bought again at its next reckoning). Counted by the
                             -- porters' parties: trade events of a region aggregate
  for h = 1, 24 * 7 * WEEKS do
    world.step(W)
    for _, id in ipairs(W.ent.party_list or {}) do
      local p = W.ent.parties[id]
      if p and not seen[id] and p.from == A.id and p.kind == "porters" and p.cargo and p.cargo.get == "food" then
        seen[id] = true
        buys = buys + 1
      end
    end
    if not ret then
      for _, id in ipairs(W.ent.party_list or {}) do
        local p = W.ent.parties[id]
        if p and p.kind == "returning" and p.to == A.id then ret = p; got = p.cargo.got end
      end
    end
    if strike and ret and not struck and W.ent.parties[ret.id] then
      if sea.at_sea(ret, schedule.party_pos(W, ret, W.hour)) and ret.boats and #ret.boats > 0 then
        local b = W.ent.boats[ret.boats[1]]
        world.command(W, "power", { power = "lightning", cell = sea.cell_of(W, b), ship = b.id, list = "boats" })
        world.step(W)
        struck = W.hour
        lost_boat, next_boat = b.id, W.ent.next_boat
      end
    end
    deficit = deficit + (1 - A.ration) / 24
    -- the store the hour the porters are home (or would have been)
    if ret and not food_home and not W.ent.parties[ret.id] then food_home = A.stock.grain + A.stock.fresh end
  end
  local acc = A.acc or {}
  local out = { W = W, A = A, got = got or 0, struck = struck, food_home = food_home, deficit = deficit,
    boats = #A.boats, catch = acc.catch or 0, trips = acc.trips or 0, wild = (acc.meat or 0) + (acc.forage or 0),
    cloth = A.stock.cloth or 0, partner = party.to, grain = {}, buys = buys,
    sold = W.ent.villages[party.to].acc.food_sold or 0, lost_boat = lost_boat, rebuilt = 0 }
  for _, bid in ipairs(A.boats) do if next_boat and bid >= next_boat then out.rebuilt = out.rebuilt + 1 end end
  for _, id in ipairs(W.E.order) do
    local r = W.E.rec[id]
    if r and r.data and r.data.village == A.id then
      -- (the first rationing after the strike: a village already short may have rationed before)
      if r.kind == "rationing" and not out.rationing and (not struck or r.tick >= math.floor(struck / 168)) then out.rationing = id end
      if r.kind == "relief" and not out.relief then out.relief = id end
    end
    if r and r.kind == "shipment_lost" then out.lost = id end
  end
  for _, v in ipairs(W.ent.villages) do out.grain[v.id] = v.stock.grain end
  return out
end

function M.b15a_a_lost_cargo_is_bought_again_at_the_partners_cost()
  -- (70 days of food: short enough to buy, and enough with the cargo home that the control needs
  -- no more at its next reckoning — the premise: the cargo was what it lacked)
  local c = run(false, 70, 150)
  local s = run(true, 70, 150)
  T.ok(s.struck, "the boat bringing the grain was struck at sea")
  T.ok(c.got > 500, "control: the partner sold a real cargo")
  T.ok(chain_has(s.W, s.lost, "power_lightning") and chain_has(s.W, s.lost, "boat_lost"),
    "shipment_lost from the Lightning and the boat")
  T.ok(s.food_home < c.food_home - 0.8 * c.got, string.format("store when the porters are home %.0f vs control %.0f (cargo %.0f)",
    s.food_home, c.food_home, c.got))
  T.ok(s.lost_boat and not s.W.ent.boats[s.lost_boat], "the struck boat is gone")
  T.eq(s.boats + (s.rebuilt > 0 and 0 or 1), c.boats, string.format("a boat fewer, or one built since to replace it (%d built)", s.rebuilt))
  -- it buys again at its next reckoning (the control, its cargo home, needs no more), paying with
  -- more of its cloth; the partner sells more of its grain
  T.ok(s.buys > c.buys, string.format("purchases of grain %d vs control %d", s.buys, c.buys))
  T.ok(s.cloth < c.cloth, string.format("cloth left %.0f vs control %.0f", s.cloth, c.cloth))
  local p = s.partner
  T.ok(s.sold > c.sold + 0.1 * c.got, string.format("the partner sold %.0f grain vs control %.0f", s.sold, c.sold))
  return string.format("%s bought %.0f grain from %s, lost at sea: store %.0f vs %.0f when the porters were home; catch %.0f vs %.0f; purchases %d vs %d in 3 weeks; cloth %.0f vs %.0f; the partner sold %.0f vs %.0f",
    s.A.name, c.got, s.W.ent.villages[p].name, s.food_home, c.food_home, s.catch, c.catch, s.buys, c.buys, s.cloth, c.cloth, s.sold, c.sold)
end

function M.b15b_a_village_that_counts_on_the_grain_goes_hungry_and_is_helped()
  local c = run(false, 40, 150, false, true)
  local s = run(true, 40, 150, false, true)
  T.ok(s.struck, "the boat bringing the grain was struck at sea")
  T.ok(not c.rationing, "control: with the grain it does not ration")
  T.ok(s.deficit > c.deficit * 1.05 + 0.2, string.format("it goes hungrier: ration deficit %.2f vs control %.2f days",
    s.deficit, c.deficit))
  T.ok(chain_has(s.W, s.rationing, "boat_lost"), "its rationing descends from the boat's loss")
  T.ok(not chain_has(c.W, c.rationing, "boat_lost"), "(control: no such cause)")
  T.ok(chain_has(s.W, s.relief, "boat_lost"), "the relief its friends send descends from the loss")
  T.ok(s.wild > c.wild, string.format("hunting and gathering %.0f vs control %.0f", s.wild, c.wild))
  return string.format("%s: ration deficit %.2f vs %.2f days; wild food %.0f vs %.0f; purchases %d vs %d (with what it made since); chain %s",
    s.A.name, s.deficit, c.deficit, s.wild, c.wild, s.buys, c.buys, kinds_of(s.W, s.relief))
end

return M
