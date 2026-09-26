-- Test hooks (sim/testhooks.lua): root events (actor player) + tags on the fields they change.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local M = {}

local function last_of(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind then return r end
  end
end

function M.each_hook_creates_a_player_root_and_tags()
  local W = scen.fresh_world(3)
  local land = W.geo.land_cells
  local oc = W.geo.ocean_cells
  local L1 = land[math.floor(#land / 2)]
  local L2 = land[math.floor(#land / 3)]   -- separate cell: removing predators releases grazers
  local O1 = oc[math.floor(#oc / 2)]
  local cases = {
    { "remove_predators", { cells = { L1 }, fraction = 1 }, "predators", L1 },
    { "add_grazers", { cells = { L2 }, amount = 0.3 }, "grazers", L2 },
    { "kill_fish", { cells = { O1 } }, "fish", O1 },
    { "heat", { cells = { O1 }, amount = 5 }, "heat_anom", O1 },
    { "oil", { cell = O1, amount = 1 }, "oil", O1 },
    { "burn_farms", {}, nil, nil },
  }
  for _, cs in ipairs(cases) do
    world.test_action(W, cs[1], cs[2])
    world.tick(W)
    local r = last_of(W, "test_" .. cs[1])
    T.ok(r ~= nil, "root for " .. cs[1])
    T.eq(r.actor, "player", "actor")
    T.eq(r.parent, 0, "root has no parent")
    if cs[3] then
      local tag = W.C[cs[3]][cs[4]]
      T.ok(tag ~= 0 and W.E.root[tag] == r.id, cs[1] .. " tags " .. cs[3])
    end
  end
  local ok = pcall(world.COMMANDS.test_action, W, { name = "nope" })
  T.ok(not ok, "unknown hook errors")
end

function M.ignite_blames_the_fire()
  local W = scen.fresh_world(1)
  local best, bf = nil, 0
  for _, c in ipairs(W.geo.land_cells) do
    local fuel = 0.5 * W.F.grass[c] + W.F.forest[c]
    if fuel > bf then best, bf = c, fuel end
  end
  world.test_action(W, "ignite", { cells = { best } })
  world.tick(W)
  local r = last_of(W, "test_ignite")
  local fs = last_of(W, "fire_started")
  T.ok(r and fs and fs.parent == r.id, "fire_started descends from the hook")
end

return M
