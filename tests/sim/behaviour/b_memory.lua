-- B17 (D-064): a village that remembers a famine keeps more in store (and sows a wider margin) than
-- the same village that does not; its grown-ups' memories of the famine are what raise it (dread),
-- and the memory outlasts ordinary ones.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local economy = require("sim.society.economy")
local farming = require("sim.society.farming")
local U = require("tests.sim.behaviour.util")
local M = {}

local function plan_fields(W, v)
  -- the fields the village's plan wants cropped now (its adults as the workforce)
  local adults = 0
  for _, id in ipairs(v.members) do
    if ents.age(W, W.ent.people[id]) >= cfgmod.SOCIETY.adult_age then adults = adults + 1 end
  end
  return farming.field_want(W, v, adults)
end

function M.b17_remembered_famine_widens_the_margin()
  local Wa, Wb = worlds.fresh(22), worlds.fresh(22)
  local va = U.villages(Wa, function(u) return #u.farms > 0 end)[1]
  local vb = Wb.ent.villages[va.id]
  -- the famine happened (an event of this village), and everyone living there remembers it
  local rec_before = Wa.record_events
  Wa.record_events = true
  local id = events.emit(Wa, "famine", va.cell, 0, va.pop, { village = va.id })
  Wa.record_events = rec_before
  T.ok(id ~= 0, "the famine is recorded")
  for _, pid in ipairs(va.members) do ents.remember(Wa.ent.people[pid], id, 0.9) end
  local da = economy.dread(Wa, va)
  local db = economy.dread(Wb, vb)
  T.ok(da > 0.6, string.format("the grown-ups dread hunger: %.2f", da))
  T.near(db, 0, 1e-9, "the control village has no such memory")
  T.eq(va.dread_ev, id, "the famine is what they remember")
  -- with two years of grain in the granary the control village lets fields go fallow; the one
  -- that remembers the famine keeps sowing them (it is content only with a bigger store)
  local ny = va.need_day * 336
  va.stock.grain, vb.stock.grain = ny * 2.2, vb.need_day * 336 * 2.2
  local fa = plan_fields(Wa, va)
  local fb = plan_fields(Wb, vb)
  T.ok(fa > fb, string.format("more fields kept in crop: %d vs %d", fa, fb))
  -- the memory outlasts ordinary ones: twenty years of fading
  local q = { mem = { id, 0.9, 1, 0.3 } }
  for _ = 1, 20 do ents.fade_memories(q, cfgmod.PEOPLE.memory_fade) end
  T.ok(q.mem[1] == id and q.mem[2] > 0.2, string.format("the famine is still remembered after 20 years (%.2f)", q.mem[2] or 0))
  T.ok(#q.mem == 2, "the minor memory is forgotten")
  return string.format("%s: dread %.2f, fields %d vs %d", va.name, da, fa, fb)
end

-- B21 (D-074): social storage. What a village takes in its hunger binds it: it is better disposed
-- to the giver, gives back at least what it took when the giver is in need, and sends its surplus
-- to the giver first — a gift that carries the relief it repays as its cause. A village that
-- remembers a famine builds granaries beyond the store others are content with (each such granary
-- descends from the famine); grain the granaries cannot hold is feasted, the guests bound to the
-- host and the host's leader honoured.
local world = require("sim.world")
local relations = require("sim.society.relations")
local construction = require("sim.society.construction")
local sites = require("sim.society.sites")

-- Two living villages of one landmass within relief reach that have had no dealings yet (their
-- ledger and grudges are cleared: the test starts the story of their obligations) and are well
-- disposed both ways (the largest such pair first; deterministic).
local function pair(W)
  local vs = U.villages(W, function(u) return u.pop >= 20 end)
  local reach = cfgmod.SOCIETY.relief_reach
  for i = 1, #vs do
    for j = 1, #vs do
      local a, b = vs[i], vs[j]
      if a ~= b and a.continent == b.continent and sites.dist(W, a.cell, b.cell) <= reach then
        local r = relations.get(W, a.id, b.id, true)
        r.gab, r.gba, r.ga, r.gb = 0, 0, 0, 0
        if relations.score(W, a, b) >= 0 and relations.score(W, b, a) >= 0 then return a, b end
      end
    end
  end
end

-- Only village `giver` can send porters this month (the others are busy): the test isolates one
-- pair from the rest of the world.
local function only(W, giver)
  for _, u in ipairs(W.ent.villages) do
    if u == giver then u.porters_out = nil else u.porters_out = true end
  end
end

-- v in need: nothing in store, rationing, asking now.
local function in_need(v)
  v.stock.grain, v.stock.fresh = 0, 0
  v.rationing, v.relief_asked = true, nil
end

-- Step the world until the porters' goods reach `dest` (its relief_in grows) or max_h hours pass.
local function until_relief(W, dest, max_h)
  local before = dest.acc.relief_in or 0
  for _ = 1, max_h do
    world.step(W)
    if (dest.acc.relief_in or 0) > before then return (dest.acc.relief_in or 0) - before end
  end
  return 0
end

-- The latest event of `kind` from village `from` (data.village for gifts, data.from for relief).
local function latest(W, kind, test)
  for i = #W.E.order, 1, -1 do
    local id = W.E.order[i]
    local r = W.E.rec[id]
    if r and r.kind == kind and type(r.data) == "table" and test(r.data) then return r, id end
  end
end

-- The harvest is in, and it left v more than its granaries hold and than it needs to its next
-- harvest (a surplus is a harvest's, D-074): returns the level the surplus is above.
local function harvest_surplus(W, v, extra_days)
  v.grain_cap = construction.grain_capacity(W, v)
  v.harvest_end, v.last_harvest = W.hour, v.need_day * 336
  local keep = v.need_day * (farming.days_to_harvest(W, v) + cfgmod.SOCIETY.relief_margin_days) - v.stock.fresh
  local level = math.max(v.grain_cap, keep)
  v.stock.grain = level + v.need_day * extra_days
  return level
end

-- need-days u took from v (the ledger, u's side)
local function took(W, u, v)
  local r = relations.get(W, u.id, v.id)
  if not r then return 0 end
  if u.id < v.id then return r.gba end
  return r.gab
end

function M.b21a_a_gift_binds_the_taker()
  local W = worlds.fresh(22)
  W.record_events = true
  local a, b = pair(W)
  T.ok(a ~= nil, "a well-disposed pair of villages")
  local s0 = relations.score(W, b, a)
  -- 1. b is hungry, a has grain to spare: a sends relief
  in_need(b)
  a.stock.grain = a.need_day * 700
  only(W, a)
  relations.monthly(W, b)
  local got = until_relief(W, b, 24 * 20)
  T.ok(got > 0, string.format("the relief arrives: %.0f FU", got))
  local r = relations.get(W, a.id, b.id)
  local ev
  if a.id < b.id then ev = r.ev_ab else ev = r.ev_ba end
  T.ok(ev and ev ~= 0 and W.E.rec[ev] and W.E.rec[ev].kind == "relief", "the debt remembers the relief it came from")
  local s1 = relations.score(W, b, a)
  T.ok(s1 > s0 + 0.01, string.format("the taker is better disposed to the giver: %.3f -> %.3f", s0, s1))
  -- 2. now a is hungry and b has a little to spare: b gives back what it took, more than a
  -- friend's share; the control (the same world with no debt) gives a friend's share
  local Wc = worlds.fresh(22)
  Wc.record_events = true
  local ac, bc = Wc.ent.villages[a.id], Wc.ent.villages[b.id]
  local rc = relations.get(Wc, a.id, b.id, true)
  rc.gab, rc.gba, rc.ga, rc.gb = 0, 0, 0, 0
  local function set(Wx, ax, bx)
    in_need(ax)
    local dth = farming.days_to_harvest(Wx, bx)
    bx.stock.grain = bx.need_day * (dth + cfgmod.SOCIETY.relief_margin_days) + got * 1.3
    bx.stock.fresh, bx.rationing, bx.famine = 0, false, false
    only(Wx, bx)
  end
  set(W, a, b); set(Wc, ac, bc)
  local g0, gc0 = b.stock.grain, bc.stock.grain
  relations.monthly(W, a); relations.monthly(Wc, ac)
  local gave, gavec = g0 - b.stock.grain, gc0 - bc.stock.grain
  T.ok(gave > gavec * 1.2, string.format("the debtor gives back more: %.0f vs a friend's %.0f FU", gave, gavec))
  local rel = latest(W, "relief", function(d) return d.village == a.id and d.from == b.id end)
  T.ok(rel and (rel.data.repay or 0) > 0, "the relief is marked as repaying a debt")
  return string.format("%s -> %s: relief %.0f FU; score %.3f -> %.3f; repaid %.0f vs a friend's %.0f FU",
    a.name, b.name, got, s0, s1, gave, gavec)
end

function M.b21b_surplus_goes_to_the_creditor()
  local W = worlds.fresh(22)
  W.record_events = true
  local a, b = pair(W)
  in_need(b)
  a.stock.grain = a.need_day * 700
  only(W, a)
  relations.monthly(W, b)
  local got = until_relief(W, b, 24 * 20)
  T.ok(got > 0, "the relief arrives")
  local r = relations.get(W, a.id, b.id)
  local rid
  if a.id < b.id then rid = r.ev_ab else rid = r.ev_ba end
  -- b's next harvest overflows its granaries; a has room: the surplus goes to a first, as the
  -- repayment of the relief (its cause)
  b.rationing, b.famine, b.porters_out = false, false, nil
  harvest_surplus(W, b, 40)
  a.stock.grain = 0
  a.grain_cap = construction.grain_capacity(W, a)
  for _, u in ipairs(W.ent.villages) do if u ~= b then u.porters_out = nil end end
  relations.surplus(W, b)
  local g, gid = latest(W, "gift", function(d) return d.village == b.id end)
  T.ok(g ~= nil and g.data.to == a.id, "the surplus goes to the village that gave in the hunger")
  T.ok(g and (g.data.repay or 0) > 0, "it is a repayment")
  T.eq(g and W.E.parent[gid], rid, "the gift descends from the relief it repays")
  return string.format("%s gives %s %s FU (repaying %s need-days)", b.name, a.name,
    tostring(g and g.data.food), tostring(g and g.data.repay))
end

function M.b21c_dread_builds_granaries()
  local Wa, Wb = worlds.fresh(22), worlds.fresh(22)
  Wa.record_events, Wb.record_events = true, true
  local va = U.villages(Wa, function(u) return #u.farms > 0 end)[1]
  local vb = Wb.ent.villages[va.id]
  local id = events.emit(Wa, "famine", va.cell, 0, va.pop, { village = va.id })
  for _, pid in ipairs(va.members) do ents.remember(Wa.ent.people[pid], id, 0.9) end
  economy.dread(Wa, va); economy.dread(Wb, vb)
  -- both villages' granaries already hold the store others are content with, and are full
  local function prep(W, v)
    local ny = v.need_day * 336
    local guard = 0
    while construction.grain_capacity(W, v) < ny * cfgmod.SOCIETY.store_years and guard < 40 do
      local cell, lot = ents.free_lot(W, v, "granary")
      if not cell then break end
      ents.new_building(W, v, "granary", cell, lot, "standing")
      guard = guard + 1
    end
    v.stock.grain = construction.grain_capacity(W, v)
    for _, bid in ipairs(v.buildings) do
      local b = W.ent.buildings[bid]
      if b.state == "site" then b.state = "gone" end
    end
  end
  prep(Wa, va); prep(Wb, vb)
  local na, nb = #va.buildings, #vb.buildings
  construction.plan(Wa, va); construction.plan(Wb, vb)
  local site
  for i = na + 1, #va.buildings do
    local b = Wa.ent.buildings[va.buildings[i]]
    if b.kind == "granary" and b.state == "site" then site = b end
  end
  local other = false
  for i = nb + 1, #vb.buildings do
    local b = Wb.ent.buildings[vb.buildings[i]]
    if b.kind == "granary" then other = true end
  end
  T.ok(site ~= nil, string.format("the village that remembers the famine (dread %.2f) builds another granary", va.dread))
  T.ok(not other, "the village that does not remember builds none")
  T.eq(site and site.dread_ev, id, "the granary is built against the remembered famine")
  if site then
    site.progress = 1
    construction.complete(Wa, va, site)
  end
  local _, ev = latest(Wa, "granary_built", function(d) return d.village == va.id end)
  T.ok(ev and Wa.E.parent[ev] == id, "granary_built descends from the famine")
  return string.format("%s: dread %.2f, a granary against the famine", va.name, va.dread)
end

function M.b21d_a_feast_of_what_would_rot()
  local W = worlds.fresh(22)
  W.record_events = true
  local v = pair(W)   -- (a village with a friendly neighbour on its landmass to invite)
  local level = harvest_surplus(W, v, 30)
  v.rationing, v.famine = false, false
  v.porters_out = true   -- (no porters to carry a gift: all of it is feasted)
  local lh = v.leader_h and W.ent.households[v.leader_h]
  local p0 = lh and lh.prestige or 0
  local owed0 = {}
  for _, u in ipairs(W.ent.villages) do if u ~= v then owed0[u.id] = took(W, u, v) end end
  relations.surplus(W, v)
  T.near(v.stock.grain, level, 1e-6, "what the granaries hold and the year needs is kept, the rest feasted")
  local fe = latest(W, "feast", function(d) return d.village == v.id end)
  T.ok(fe ~= nil, "a feast is held")
  T.ok(fe and fe.data.guests >= 1, "with guests from friendly villages")
  local bound = 0
  for _, u in ipairs(W.ent.villages) do
    if u ~= v and took(W, u, v) > owed0[u.id] + 1e-9 then bound = bound + 1 end
  end
  T.ok(bound >= 1, string.format("the guests owe their host (%d)", bound))
  local p1 = lh and lh.prestige or 0
  T.ok(p1 > p0, string.format("the host's leader is honoured: %.3f -> %.3f", p0, p1))
  -- and only once a year
  v.stock.grain = level + v.need_day * 30
  relations.surplus(W, v)
  T.ok(v.stock.grain > level, "no second feast in the same year")
  return string.format("%s feasts %s FU with %d guests", v.name, tostring(fe and fe.data.food), fe and fe.data.guests or 0)
end

return M
