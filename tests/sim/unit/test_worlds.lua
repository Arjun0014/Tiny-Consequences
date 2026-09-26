-- Pre-spun worlds (sim/worlds.lua, tools/make_world.lua): a world restored from a Year-0 save
-- equals the freshly generated world after 5 years (state hash).
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")
local M = {}

local function five_years(W)
  world.run_years(W, 5)
  return world.state_hash(W)
end

function M.year0_save_restores_to_the_same_world()
  local str = worlds.year0_save_string(worlds.generate(2), { seed = 2 })
  local W1 = assert(worlds.from_save_string(str))
  T.eq(W1.tick, 0, "Year 0")
  local h1 = five_years(W1)
  local h2 = five_years(worlds.generate(2))
  T.eq(h1, h2, "restored == fresh after 5 years")
  return string.format("%d bytes", #str)
end

function M.default_world_matches_fresh_generation()
  local f = io.open("assets/worlds/default.tcsave", "rb")
  T.ok(f ~= nil, "assets/worlds/default.tcsave exists (luajit tools/make_world.lua)")
  local str = f:read("*a")
  f:close()
  local W1, err = worlds.from_save_string(str)
  T.ok(W1 ~= nil, "decodes: " .. tostring(err))
  local seed = W1.seed
  local h1 = five_years(W1)
  local h2 = five_years(worlds.generate(seed))
  T.eq(h1, h2, "default world file is current (regenerate with tools/make_world.lua if not)")
  return string.format("seed %d, %d bytes", seed, #str)
end

return M
