-- Land animals (sim/systems/fauna.lua, herds and packs since D-070): conservation, no spawning,
-- release blame.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local fauna = require("sim.systems.fauna")
local cfgmod = require("sim.config")
local M = {}

-- Herds and packs (W4, D-070, D-071): with births, deaths and predation switched off, moving,
-- migrating, splitting, joining and the wolves' dispersal neither make nor lose animals, and the
-- herds do move.
function M.groups_move_and_conserve_animals()
  local W = scen.fresh_world(1)
  local F = cfgmod.FAUNA
  local Wc = cfgmod.WILD
  -- (every birth and death off: predation, fire, hunting, fawns and litters, starvation, old age,
  -- deaths on the way; dispersal, moving, splitting and joining stay on)
  local KF = { "a_pred", "fire_mort" }
  local KW = { "hunt_rate", "fawns", "litter", "h_starve_max", "p_starve_max", "h_mort", "p_mort" }
  local keep = {}
  for _, k in ipairs(KF) do keep["F" .. k] = F[k]; F[k] = 0 end
  for _, k in ipairs(KW) do keep["W" .. k] = Wc[k]; Wc[k] = 0 end
  keep.surv = Wc.disperse_survival
  Wc.disperse_survival = 1
  local ok, err = pcall(function()
    local A = W.S.wild
    local function sums()
      local h, p = 0, 0
      for i = 1, A.nh do h = h + A.h_n[i] end
      for i = 1, A.np do p = p + A.p_n[i] end
      return h, p
    end
    local h0, p0 = sums()
    local cells0 = {}
    for i = 1, A.nh do cells0[A.h_id[i]] = A.h_cell[i] end
    T.ok(A.nh > 20 and A.np > 3, "herds and packs from worldgen")
    world.run_ticks(W, 48 * 2)
    local h1, p1 = sums()
    T.near(h1, h0, 1e-6 * h0 + 0.5 * 3, "grazers conserved")
    T.near(p1, p0, 1e-6 * p0 + 0.5 * 3, "predators conserved")
    local moved = 0
    for i = 1, A.nh do
      local c0 = cells0[A.h_id[i]]
      if c0 and c0 ~= A.h_cell[i] then moved = moved + 1 end
    end
    T.ok(moved > 0, "herds moved")
    return string.format("%d herds (%.0f animals), %d packs (%.0f): %d herds moved in two years", A.nh, h1, A.np, p1, moved)
  end)
  for _, k in ipairs(KF) do F[k] = keep["F" .. k] end
  for _, k in ipairs(KW) do Wc[k] = keep["W" .. k] end
  Wc.disperse_survival = keep.surv
  if not ok then error(err, 0) end
  return err
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
