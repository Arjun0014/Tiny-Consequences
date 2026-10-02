-- Stone (S19): rock only where the land breaks; every stone quarried at a face is accounted for -
-- lying there, in the yard, laid in a footing or shaped into a quern; querns cut the grinding; a
-- village with no rock gets querns only by trade with one that has hard rock.
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")

local M = {}

local function shipped()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

local function outcrops(s)
  local n, hard = 0, 0
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "outcrop" then n = n + 1; if b.rock == "hard" then hard = hard + 1 end end
  end
  return n, hard
end

function M.rock_where_the_land_breaks()
  local W = shipped()
  local V = W.S.v3
  local stone = require("sim.agents.stone")
  world.advance(W, W.hour + 25)
  local rocky, flat = 0, 0
  for _, s in ipairs(V.settlements) do
    local n = outcrops(s)
    -- (how rocky the map's own ground is, from the planet's cells under it)
    local best = 0
    for i = 1, s.N * s.N, 97 do
      local r = stone.rock_of(W, s.cell_of[i])
      if r > best then best = r end
    end
    if best == 0 then T.eq(n, 0, s.name .. ": no rock where the land does not break") ; flat = flat + 1 end
    if n > 0 then rocky = rocky + 1 end
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "outcrop" then
        for iy = b.iy0, b.iy0 + b.h - 1 do
          for ix = b.ix0, b.ix0 + b.w - 1 do
            local i = (iy - 1) * s.N + ix
            T.ok(s.land[i] == 1 and s.riv[i] == 0, s.name .. ": an outcrop on dry land")
            T.ok(s.tree[i] == 0, s.name .. ": no tree on bare rock")
          end
        end
      end
    end
  end
  T.ok(rocky >= 2, "some villages have rock (" .. rocky .. ")")
  T.ok(flat >= 1, "some have none (" .. flat .. ")")
  return string.format("%d villages with outcrops, %d on flat ground with none", rocky, flat)
end

function M.stone_is_quarried_carried_and_used()
  local W = shipped()
  local V = W.S.v3
  local c = require("sim.config").V3
  local t0 = W.hour
  local seen = { quarry = false, haul = false, quern = false }
  for h = 1, 24 * 20 do
    world.advance(W, t0 + h)
    if h % 2 == 0 then
      for _, id in ipairs(V.order) do
        local p = V.people[id]
        if p and p.alive then
          if p.act == "quarry" then seen.quarry = true end
          if p.carry == "stone" or p.carry == "hardstone" then seen.haul = true end
          if p.act == "quern" then seen.quern = true end
        end
      end
    end
  end
  T.ok(seen.quarry, "someone split stone at a face")
  T.ok(seen.haul, "someone carried a stone")
  T.ok(seen.quern, "someone shaped a quern")
  local querns_total, quarried_total = 0, 0
  for _, s in ipairs(V.settlements) do
    local q = s.acc.quarried or 0
    quarried_total = quarried_total + q
    -- every stone: lying at a face, in the yard, carried right now, laid in a footing or in a quern
    local lying, yard, carried = 0, 0, 0
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "outcrop" then lying = lying + (b.stock.stone or 0) end
    end
    if s.wood_store then yard = (s.wood_store.stock.stone or 0) + (s.wood_store.stock.hardstone or 0) end
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and (p.carry == "stone" or p.carry == "hardstone") then carried = carried + 1 end
    end
    -- (a quern under way has taken up its two stones)
    local under = 0
    for _, hid in ipairs(s.households) do
      local hh = V.households[hid]
      if hh and (hh.quern_work or 0) > 0 then under = under + 1 end
    end
    local made = s.acc.querns or 0
    querns_total = querns_total + made
    T.eq(q, lying + yard + carried + (s.acc.stone_laid or 0) + 2 * (made + under),
      s.name .. ": every stone quarried is somewhere (lying, yard, carried, footings, querns)")
    if q == 0 then T.eq(made, 0, s.name .. " made no quern without stone") end
  end
  T.ok(quarried_total > 10, "stone was quarried: " .. quarried_total)
  T.ok(querns_total > 0, "querns were made: " .. querns_total)
  -- (with a quern the cooking takes less time: the plan's batch is shorter by quern_save)
  local with = 0
  for _, s in ipairs(V.settlements) do
    for _, hid in ipairs(s.households) do
      local hh = V.households[hid]
      if hh and (hh.quern or 0) >= c.quern_broken then with = with + 1 end
    end
  end
  T.ok(with > 0, "households grind with querns: " .. with)
  return string.format("%d stones quarried, %d querns made, %d households grinding with one", quarried_total, querns_total, with)
end

function M.a_quern_travels_to_a_village_without_rock()
  local W = shipped()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  world.advance(W, W.hour + 25)
  -- a village with hard rock next to one with none, joined by a road
  local A, B
  for _, s in ipairs(V.settlements) do
    local _, hard = outcrops(s)
    if hard > 0 then
      for _, o in ipairs(trade.partners(W, s)) do
        if outcrops(o) == 0 and not A then A, B = s, o end
      end
    end
  end
  T.ok(A ~= nil, "a village with hard rock and a neighbour with none")
  if not A then return end
  -- (premise: two households of A have a quern to spare; B's households had a good season at the
  -- looms (cloth to spare: something to pay with); A has just heard how B stands)
  for _, hid in ipairs(B.households) do
    local hh = V.households[hid]
    local home = hh and B.buildings[hh.home or 0]
    if home then home.stock.cloth = (home.stock.cloth or 0) + 2.5 * #hh.members end
  end
  local n = 0
  for _, hid in ipairs(A.households) do
    local hh = V.households[hid]
    local home = hh and A.buildings[hh.home or 0]
    if home and n < 2 then home.stock.quern = (home.stock.quern or 0) + 1; hh.quern = 1; n = n + 1 end
  end
  econ.learn(W, A, B, W.hour)
  local q0 = 0
  local t0 = W.hour
  local got = nil
  for h = 1, 24 * 16 do
    world.advance(W, t0 + h)
    for _, hid in ipairs(B.households) do
      local hh = V.households[hid]
      if hh and (hh.quern or 0) > 0 and not got then got = hh end
    end
    if got then break end
  end
  T.ok(got ~= nil, B.name .. " (no rock) got a quern from " .. A.name)
  T.ok((B.acc.quarried or 0) == 0, B.name .. " quarried nothing")
  return string.format("%s (no rock) got a quern from %s", B.name, A.name)
end

return M
