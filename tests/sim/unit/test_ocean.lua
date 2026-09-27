-- Ocean life, currents & oil (sim/systems/ocean.lua).
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local ocean_sys = require("sim.systems.ocean")
local cfgmod = require("sim.config")
local M = {}

local function total_oil(W)
  local s = 0
  for c = 1, W.n do s = s + W.F.oil[c] end
  return s
end

local function last_of(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind then return r end
  end
end

function M.currents_push_weights_conserve_mass()
  local W = scen.fresh_world(1)
  for s = 1, 4 do
    local t = W.currents[s]
    for _, c in ipairs(W.geo.ocean_cells) do
      local b = c * 6 - 6
      local sum = t.self[c]
      for k = 1, W.grid.nbr_count[c] do
        local j = W.grid.nbr[b + k]
        local w = t.w[b + k]
        T.ok(w >= 0, "weight >= 0")
        if W.geo.ocean[j] == 0 then T.eq(w, 0, "no current into land") end
        sum = sum + w
      end
      T.near(sum, 1, 1e-12, "weights sum to 1 at " .. c)
      T.ok(t.self[c] >= cfgmod.OCEAN.beach, "room for beaching")
    end
  end
end

function M.oil_advects_conservatively_and_carries_its_tag()
  local W = scen.fresh_world(2)
  local c, bd = nil, -1
  for _, x in ipairs(W.geo.ocean_cells) do
    if W.geo.coast_dist[x] > bd then c, bd = x, W.geo.coast_dist[x] end
  end
  T.ok(bd >= 3, "open water cell")
  local dec = 0.5 ^ (1 / cfgmod.OCEAN.oil_half_life)
  world.test_action(W, "oil", { cell = c, amount = 2 })
  world.tick(W)               -- applies the spill, then one transport step
  local root = last_of(W, "test_oil").id
  local before = total_oil(W)
  -- spill amounts are level-4-equivalent cells of cover (D-047): cell units = amount / sscale²
  T.near(before, 2 / (W.sscale * W.sscale) * dec, 1e-3, "first step: mass x decay")
  ocean_sys.update(W)
  local after = total_oil(W)
  -- offshore (no beaching): mass x decay, minus traces below oil_min at the fringe
  T.near(after, before * dec, 1e-3 * before, "offshore: mass x decay")
  local spread = 0
  for x = 1, W.n do
    if W.F.oil[x] > 0 then
      spread = spread + 1
      T.eq(W.C.oil[x], root, "oil carries its tag")
    end
  end
  T.ok(spread > 7, "slick spreads beyond the first ring")
  return string.format("%d oiled cells", spread)
end

function M.oil_beaches_and_damages_the_shore()
  local W = scen.fresh_world(3)
  local tg = scen.select(W, "ocean_near:coast:largest")
  world.test_action(W, "oil", { cell = tg.cell, amount = 3 })
  world.run_ticks(W, 24)
  local shore, damaged = 0, 0
  for c = 1, W.n do
    if W.geo.ocean[c] == 0 and W.F.oil[c] > 0 then
      shore = shore + 1
      T.ok(W.C.oil[c] ~= 0, "beached oil is tagged")
      if W.C.grass[c] ~= 0 then damaged = damaged + 1 end
    end
  end
  T.ok(shore > 0, "oil reached the shore")
  T.ok(last_of(W, "oil_beached") ~= nil, "oil_beached event")
  T.ok(damaged > 0, "shore grass damage is blamed")
  return string.format("%d shore cells oiled, %d with tagged grass", shore, damaged)
end

function M.marine_heatwave_collapses_plankton_with_blame()
  local W = scen.fresh_world(1)
  local best, bs = nil, -99
  for _, c in ipairs(W.geo.ocean_cells) do
    if W.F.sst[c] > bs and W.F.ice[c] == 0 then best, bs = c, W.F.sst[c] end
  end
  local p0 = W.F.plankton[best]
  world.test_action(W, "heat", { cells = { best }, amount = 12 })
  world.run_ticks(W, 48)
  local p1 = W.F.plankton[best]
  T.ok(p1 < 0.5 * p0, string.format("plankton collapses (%.3f -> %.3f)", p0, p1))
  local tag = W.C.plankton[best]
  local root = tag ~= 0 and W.E.root[tag]
  T.ok(root and W.E.rec[root] and W.E.rec[root].kind == "test_heat", "plankton blamed on the heat")
  return string.format("peak cell sst now %.1f, plankton %.2f -> %.3f", W.F.sst[best], p0, p1)
end

return M
