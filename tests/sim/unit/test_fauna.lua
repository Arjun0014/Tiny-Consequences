-- Land animals (sim/systems/fauna.lua): conservation, no spawning, release blame.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local fauna = require("sim.systems.fauna")
local cfgmod = require("sim.config")
local M = {}

local function total(W, arr)
  local s = 0
  for c = 1, W.n do s = s + arr[c] end
  return s
end

function M.migration_conserves_animals()
  local W = scen.fresh_world(1)
  for _, grazer in ipairs({ true, false }) do
    local arr = grazer and W.F.grazers or W.F.predators
    local before = total(W, arr)
    fauna.migrate(W, cfgmod.FAUNA, grazer, 0.08)
    local after = total(W, arr)
    T.near(after, before, 1e-6 * before + 1e-9, grazer and "grazers conserved" or "predators conserved")
  end
end

function M.nothing_spawns_on_empty_islands()
  local W = scen.fresh_world(1)
  local islands = {}
  for _, comp in ipairs(W.geo.components) do
    if comp.size / W.n < 0.05 then
      local temperate = true
      for _, c in ipairs(comp.cells) do if math.abs(W.grid.lat[c]) > 0.9 then temperate = false end end
      if temperate then islands[#islands + 1] = comp end
    end
  end
  T.ok(#islands > 0, "some temperate islands")
  world.run_years(W, 3)
  for _, comp in ipairs(islands) do
    for _, c in ipairs(comp.cells) do
      T.eq(W.F.grazers[c], 0, "no grazers from nothing")
      T.eq(W.F.predators[c], 0, "no predators from nothing")
    end
  end
  return #islands .. " islands stayed empty"
end

function M.release_blame_tags_grazers_after_predators_vanish()
  local W = scen.fresh_world(2)
  local tg = scen.select(W, "continent:largest")
  world.test_action(W, "remove_predators", { cells = tg.cells, fraction = 1 })
  world.run_ticks(W, 48)
  local root
  for i = 1, #W.E.order do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == "test_remove_predators" then root = r.id end
  end
  T.ok(root, "root event")
  local tagged = 0
  for _, c in ipairs(tg.cells) do
    local t = W.C.grazers[c]
    if t ~= 0 and W.E.root[t] == root then tagged = tagged + 1 end
  end
  T.ok(tagged >= #tg.cells / 2, string.format("grazers carry the removal's chain (%d/%d cells)", tagged, #tg.cells))
  return string.format("%d/%d cells", tagged, #tg.cells)
end

return M
