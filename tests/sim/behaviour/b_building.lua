-- B04 construction consumes exactly its timber and stalls without it; B05 growth creates housing
-- pressure (waiting couples, queued house sites) that construction resolves; B06 woodcutting
-- removes exactly the wood gathered from the forest of the cell cut.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local ents = require("sim.society.entities")
local construction = require("sim.society.construction")
local labour = require("sim.society.labour")
local U = require("tests.sim.behaviour.util")
local M = {}

function M.b04_site_needs_timber_and_uses_exactly_it()
  local W = worlds.fresh(1)
  local v = U.villages(W)[1]
  local cell, lot = ents.free_lot(W, v, "house")
  local b = ents.new_building(W, v, "house", cell, lot, "site")
  local spec = construction.spec(b)
  v.stock.wood = 0
  T.eq(construction.work(W, v, b, 100), 0, "no hours can be worked without wood")
  T.eq(b.progress, 0, "no progress without wood")
  T.eq(v.site_stalled, W.hour, "the stall is recorded")
  v.stock.wood = 500
  local guard = 0
  while b.state == "site" and guard < 1000 do
    construction.work(W, v, b, 7)
    guard = guard + 1
  end
  T.eq(b.state, "standing", "finished")
  T.near(500 - v.stock.wood, spec.wood, 1e-6, "the store paid exactly the timber")
  T.near(b.wood_used, spec.wood, 1e-6, "the house holds exactly its timber")
  -- in the running world every building finished is made of its full timber
  local t0 = W.hour
  world.run_years(W, 4)
  local n = 0
  for _, u in ipairs(W.ent.villages) do
    for _, id in ipairs(u.buildings) do
      local x = W.ent.buildings[id]
      if x and x.built >= t0 and x.state == "standing" and x.id ~= b.id then
        T.near(x.wood_used, construction.spec(x).wood, 1e-6, "built house timber")
        n = n + 1
      end
    end
  end
  T.ok(n > 0, "buildings were finished")
  return string.format("%d buildings finished in 4 years", n)
end

function M.b05_waiting_couples_queue_houses_and_move_in()
  local W = worlds.fresh(22)
  local P = W.ent.people
  local farming = require("sim.society.farming")
  -- a village with land to spare and singles of marrying age
  local v, sf, sm
  for _, u in ipairs(U.villages(W)) do
    farming.capacity(W, u)
    local f, m = {}, {}
    for _, id in ipairs(u.members) do
      local p = P[id]
      local a = ents.age(W, p)
      if p.partner == 0 and p.betrothed == 0 and a >= 17 and a <= 45 then
        if p.sex == "f" then f[#f + 1] = p else m[#m + 1] = p end
      end
    end
    if u.capacity > u.pop * 1.1 and #f > 0 and #m > 0 then v, sf, sm = u, f, m; break end
  end
  T.ok(v, "a village with land to spare and singles")
  local vac = construction.vacant_houses(W, v)
  local couples = {}
  for i = 1, math.min(#sf, #sm, 3) do
    sf[i].betrothed, sm[i].betrothed = sm[i].id, sf[i].id
    sf[i].wait_since, sm[i].wait_since = W.hour, W.hour
    couples[#couples + 1] = { sf[i], sm[i] }
  end
  T.ok(#couples >= 1, "singles to betroth")
  local _, waiting = construction.housing_need(W, v)
  T.ok(waiting >= #couples, "the couples count as waiting")
  local sites0 = construction.count_kind(W, v, "house", "site")
  construction.plan(W, v)
  local sites1 = construction.count_kind(W, v, "house", "site")
  T.ok(sites1 > sites0 or vac > 0, "house sites queued for the waiting couples")
  world.run_years(W, 3)
  local married = 0
  for _, c in ipairs(couples) do
    local f, m = c[1], c[2]
    if not f.dead and not m.dead and f.partner == m.id then
      married = married + 1
      local h = W.ent.households[f.household]
      T.ok(h and h.home ~= 0 and m.household == f.household, "married couples share a household with a home")
    end
  end
  T.ok(married >= 1, "a waiting couple married once a home was built")
  return string.format("%s: %d/%d couples married, %d house sites queued", v.name, married, #couples, sites1 - sites0)
end

function M.b06_woodcutting_takes_exactly_the_wood_from_the_forest()
  local W = worlds.fresh(22)
  local cfg = cfgmod.SOCIETY
  local v, lot
  for _, u in ipairs(U.villages(W)) do
    lot = labour.woodlot(W, u, 9)
    if lot then v = u; break end
  end
  T.ok(v, "a village with woods in reach")
  v.plan.groups = { { kind = "fuel", target = lot, cap = 3, n = 3 } }
  local f0, w0 = W.F.forest[lot], v.stock.wood
  labour.execute(W, v, 9, { storm = false, wet = false, snow = false })
  local got = v.stock.wood - w0
  T.ok(got > 0, "wood gathered")
  T.near((f0 - W.F.forest[lot]) * cfg.forest_wu, got, 1e-6, "forest removed = wood gathered")
  T.ok(v.logged[lot] == W.hour, "the cut is remembered (stumps)")
  return string.format("%s cut %.1f WU in cell %d", v.name, got, lot)
end

return M
