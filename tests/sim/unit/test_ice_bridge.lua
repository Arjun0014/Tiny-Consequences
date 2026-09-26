-- Ice bridges (02_WORLD_MODEL.md §5.9): animals cross sea ice ≥ 0.8, never open water.
-- Seed 1 has single-cell straits between continents 1 and 2; predators are wiped from
-- continent 2, then the straits are either left open or frozen for two years.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local M = {}

local function straits(W, a, b)
  local g, geo = W.grid, W.geo
  local out = {}
  for _, c in ipairs(geo.ocean_cells) do
    local ta, tb = false, false
    local base = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local j = g.nbr[base + k]
      if geo.ocean[j] == 0 and geo.continent[j] == a then ta = true end
      if geo.ocean[j] == 0 and geo.continent[j] == b then tb = true end
    end
    if ta and tb then out[#out + 1] = c end
  end
  return out
end

local function run(freeze)
  local W = scen.fresh_world(1)
  local st = straits(W, 1, 2)
  T.ok(#st > 0, "seed 1 has a strait between continents 1 and 2")
  world.test_action(W, "remove_predators", { continent = 2, fraction = 1 })
  local oc = W.geo.ocean_cells
  for _ = 1, 48 * 2 do
    -- melt all sea ice (polar coasts can bridge continents naturally), then freeze the strait
    for i = 1, #oc do W.F.ice[oc[i]] = 0 end
    if freeze then for i = 1, #st do W.F.ice[st[i]] = 1 end end
    world.tick(W)
  end
  local p = 0
  for c = 1, W.n do if W.geo.continent[c] == 2 then p = p + W.F.predators[c] end end
  return p, W
end

function M.animals_cross_sea_ice_only()
  local open = run(false)
  T.eq(open, 0, "no predators cross open water")
  local frozen, W = run(true)
  T.ok(frozen > 0, "predators walk over the frozen strait")
  -- they carry blame along: the arrivals' tags come from the migrating population
  return string.format("predators on continent 2 after 2 frozen years: %.4f (mean density %.5f)", frozen,
    frozen / W.geo.components[2].size)
end

return M
