-- B15 (a trade disruption propagates, session 5): a village alone on its island, short of food
-- before the harvest, buys grain over the sea with what it can spare (its own trade decision,
-- relations.trade). The boat bringing the grain home is struck by Lightning at sea. Compared with
-- a control run in which the grain arrives (no famine logic anywhere; every step is the ordinary
-- economy):
--   B15a  a village with cloth left to sell: its store is short by about the cargo, it has a
--         boat fewer (a smaller catch), and it buys again at once (not a month later) — paying with
--         more of its cloth; its partner sells more of its grain (the loss passes along the route).
--   B15b  a village with nothing more to sell: it had counted on the grain, so it goes hungrier
--         than in the control run, its rationing and the relief its friends send descend from the
--         boat's loss (lightning > boat_lost > rationing > relief), and its people hunt and gather
--         more wild food.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local relations = require("sim.society.relations")
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
local function run(strike, days, cloth)
  local W = worlds.fresh(22)
  local A = lone_island(W)
  T.ok(A, "a village alone on its island with partners over the water")
  A.stock.grain = A.need_day * days - A.stock.fresh
  A.food_days = days   -- (as the day step reckons it)
  A.stock.cloth = (A.stock.cloth or 0) + cloth
  relations.trade(W, A)
  local party
  for _, id in ipairs(W.ent.party_list or {}) do
    local p = W.ent.parties[id]
    if p and p.from == A.id and p.kind == "porters" then party = p end
  end
  T.ok(party and party.cargo.get == "food" and party.si ~= nil, "it sends porters over the sea to buy grain")
  local ret, struck, got, food_home
  local deficit = 0   -- sum over days of (1 - ration): how much less people ate
  local buys, seen = 0, {}   -- purchases of grain set off in the first three weeks
  for h = 1, 24 * 7 * WEEKS do
    world.step(W)
    if h <= 24 * 21 then
      for _, id in ipairs(W.ent.party_list or {}) do
        local p = W.ent.parties[id]
        if p and not seen[id] and p.from == A.id and p.kind == "porters" and p.cargo and p.cargo.get == "food" then
          seen[id] = true
          buys = buys + 1
        end
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
      end
    end
    deficit = deficit + (1 - A.ration) / 24
    -- the store the hour the porters are home (or would have been)
    if ret and not food_home and not W.ent.parties[ret.id] then food_home = A.stock.grain + A.stock.fresh end
  end
  local acc = A.acc or {}
  local out = { W = W, A = A, got = got or 0, struck = struck, food_home = food_home, deficit = deficit,
    boats = #A.boats, catch = acc.catch or 0, wild = (acc.meat or 0) + (acc.forage or 0),
    cloth = A.stock.cloth or 0, partner = party.to, grain = {}, buys = buys,
    sold = W.ent.villages[party.to].acc.food_sold or 0 }
  for _, id in ipairs(W.E.order) do
    local r = W.E.rec[id]
    if r and r.data and r.data.village == A.id then
      if r.kind == "rationing" and not out.rationing then out.rationing = id end
      if r.kind == "relief" and not out.relief then out.relief = id end
    end
    if r and r.kind == "shipment_lost" then out.lost = id end
  end
  for _, v in ipairs(W.ent.villages) do out.grain[v.id] = v.stock.grain end
  return out
end

function M.b15a_a_lost_cargo_is_bought_again_at_the_partners_cost()
  local c = run(false, 60, 150)
  local s = run(true, 60, 150)
  T.ok(s.struck, "the boat bringing the grain was struck at sea")
  T.ok(c.got > 500, "control: the partner sold a real cargo")
  T.ok(chain_has(s.W, s.lost, "power_lightning") and chain_has(s.W, s.lost, "boat_lost"),
    "shipment_lost from the Lightning and the boat")
  T.ok(s.food_home < c.food_home - 0.8 * c.got, string.format("store when the porters are home %.0f vs control %.0f (cargo %.0f)",
    s.food_home, c.food_home, c.got))
  T.eq(s.boats, c.boats - 1, "a boat fewer")
  T.ok(s.catch < c.catch, string.format("catch %.0f vs control %.0f", s.catch, c.catch))
  -- it buys again at once (it would otherwise have waited a month), paying with more of its
  -- cloth; the partner sells more of its grain
  T.ok(s.buys > c.buys, string.format("purchases in the first three weeks %d vs control %d", s.buys, c.buys))
  T.ok(s.cloth < c.cloth, string.format("cloth left %.0f vs control %.0f", s.cloth, c.cloth))
  local p = s.partner
  T.ok(s.sold > c.sold + 0.1 * c.got, string.format("the partner sold %.0f grain vs control %.0f", s.sold, c.sold))
  return string.format("%s bought %.0f grain from %s, lost at sea: store %.0f vs %.0f when the porters were home; catch %.0f vs %.0f; purchases %d vs %d in 3 weeks; cloth %.0f vs %.0f; the partner sold %.0f vs %.0f",
    s.A.name, c.got, s.W.ent.villages[p].name, s.food_home, c.food_home, s.catch, c.catch, s.buys, c.buys, s.cloth, c.cloth, s.sold, c.sold)
end

function M.b15b_a_village_that_cannot_buy_again_goes_hungry_and_is_helped()
  local c = run(false, 55, 0)
  local s = run(true, 55, 0)
  T.ok(s.struck, "the boat bringing the grain was struck at sea")
  T.ok(s.deficit > c.deficit * 1.05 + 0.2, string.format("it goes hungrier: ration deficit %.2f vs control %.2f days",
    s.deficit, c.deficit))
  T.ok(chain_has(s.W, s.rationing, "boat_lost"), "its rationing descends from the boat's loss")
  T.ok(not chain_has(c.W, c.rationing, "boat_lost"), "(control: no such cause)")
  T.ok(chain_has(s.W, s.relief, "boat_lost"), "the relief its friends send descends from the loss")
  T.ok(s.wild > c.wild, string.format("hunting and gathering %.0f vs control %.0f", s.wild, c.wild))
  return string.format("%s: ration deficit %.2f vs %.2f days; wild food %.0f vs %.0f; chain %s",
    s.A.name, s.deficit, c.deficit, s.wild, c.wild, kinds_of(s.W, s.relief))
end

return M
