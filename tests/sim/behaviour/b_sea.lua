-- B02/B03 (sea passage, D-059): the villages' own boats carry goods overseas.
-- B02: a relief shipment sails to another island in one of the giver's boats; the giver's stock
--      falls and the receiver's rises by exactly the cargo; the boat doesn't fish while away and
--      comes home with the porters.
-- B03: the loaded boat is struck by Lightning at sea: the receiver gets nothing (less than in the
--      control run), and the chain lightning > boat_lost > shipment_lost is recorded.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local relations = require("sim.society.relations")
local schedule = require("sim.society.schedule")
local sea = require("sim.society.sea")
local U = require("tests.sim.behaviour.util")
local M = {}

-- A giver with boats and a receiver on another landmass within its boats' reach.
local function pair(W)
  for _, a in ipairs(U.villages(W, function(u) return u.mooring ~= nil and #sea.free_boats(W, u) > 0 end)) do
    for _, b in ipairs(U.villages(W, function(u) return u.continent ~= a.continent end)) do
      if sea.cost(W, a, b.cell, cfgmod.SOCIETY.porters) then return a, b end
    end
  end
  return nil
end

-- Send `gift` grain from a to b with a's porters. Returns the party.
local function ship(W, a, b, gift)
  local P = W.ent.people
  local ps = {}
  for i = 1, #a.members do
    local p = P[a.members[i]]
    local age = (W.hour - p.born) / 8064
    if p.away == 0 and age >= 18 and age < 45 and #ps < 4 then ps[#ps + 1] = p end
  end
  T.ok(#ps >= 2, "porters")
  a.stock.grain = a.stock.grain - gift
  return relations.depart(W, a, ps, b.id, nil, "porters", 0, 0, 0, { gift = true, grain = gift, back_to = a.id })
end

-- Wrap relations.porters_arrive to measure each delivery (receiver stock before/after).
local function watch_deliveries()
  local log = {}
  local orig = relations.porters_arrive
  relations.porters_arrive = function(W, party, dest, living)
    local g0 = dest and dest.stock.grain
    orig(W, party, dest, living)
    if dest then log[#log + 1] = { party = party.id, delta = dest.stock.grain - g0, cargo = party.cargo.grain } end
  end
  return log, function() relations.porters_arrive = orig end
end

function M.b02_shipment_overseas_moves_exactly_its_cargo()
  local W = worlds.fresh(22)
  local a, b = pair(W)
  T.ok(a, "an island village with boats and a village over the water")
  local gift = 180
  local a0 = a.stock.grain
  local log, restore = watch_deliveries()
  local party = ship(W, a, b, gift)
  T.near(a0 - a.stock.grain, gift, 1e-9, "the giver's store falls by the cargo")
  T.ok(party.si and party.sj > party.si, "the route crosses the sea")
  for i = party.si, party.sj do T.eq(W.geo.ocean[party.path[i]], 1, "sea leg over the sea") end
  T.eq(W.geo.continent[party.path[#party.path]], b.continent, "ends on the receiver's land")
  T.ok(party.boats and #party.boats >= 1, "a boat carries them")
  local boat = W.ent.boats[party.boats[1]]
  T.eq(boat.state, "voyage", "the boat is away")
  local fished = false
  local home_at = nil
  for _ = 1, 24 * 8 do
    world.step(W)
    if boat.trip then fished = true end
    if not home_at and boat.state == "moored" then home_at = W.hour end
    if home_at then break end
  end
  restore()
  T.ok(not fished, "no fishing trip while on the voyage")
  T.eq(#log, 1, "one delivery")
  T.near(log[1].delta, gift, 1e-6, "the receiver's store rises by exactly the cargo")
  T.ok(home_at ~= nil, "the boat is home again")
  local away = 0
  for _, id in ipairs(party.members) do if W.ent.people[id].away ~= 0 then away = away + 1 end end
  T.eq(away, 0, "the porters are home")
  return string.format("%s -> %s over %d sea steps: %d grain, boat home after %d h", a.name, b.name,
    party.sj - party.si, gift, home_at - party.depart)
end

function M.b03_boat_sunk_at_sea_loses_the_shipment()
  local function run(strike)
    local W = worlds.fresh(22)
    local a, b = pair(W)
    local log, restore = watch_deliveries()
    local party = ship(W, a, b, 180)
    local boat = W.ent.boats[party.boats[1]]
    local struck = nil
    for _ = 1, 24 * 4 do
      world.step(W)
      local i = schedule.party_pos(W, party, W.hour)
      if strike and not struck and W.ent.parties[party.id] and sea.at_sea(party, i) then
        world.command(W, "power", { power = "lightning", cell = sea.cell_of(W, boat), ship = boat.id, list = "boats" })
        world.step(W)
        struck = W.hour
      end
      if not W.ent.parties[party.id] then break end
    end
    restore()
    local got = 0
    for _, e in ipairs(log) do if e.party == party.id then got = got + e.delta end end
    return W, got, struck, party, b
  end
  local _, got_control = run(false)
  local W, got, struck, party, b = run(true)
  T.ok(struck, "the boat was struck at sea")
  T.near(got_control, 180, 1e-6, "control: the shipment arrives")
  T.ok(got < got_control, "struck: the receiver gets less than in the control run")
  -- the chain
  local lost = nil
  for _, id in ipairs(W.E.order) do
    local r = W.E.rec[id]
    if r and r.kind == "shipment_lost" then lost = r end
  end
  T.ok(lost, "shipment_lost recorded")
  local ch = events.chain(W, lost.id)
  local kinds = {}
  for i = 1, #ch do kinds[i] = ch[i].kind end
  local s = table.concat(kinds, ">")
  T.ok(s:find("lightning", 1, true) and s:find("boat_lost", 1, true), "chain lightning > boat_lost > shipment_lost: " .. s)
  T.eq(b.import_tag, lost.id, "the receiver blames its missing imports on the loss")
  return string.format("received %.0f (control %.0f); chain %s", got, got_control, s)
end

return M
