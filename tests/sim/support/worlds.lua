-- Post-spin-up worlds cached per seed (deep copies of the persistent state), shared by scenario
-- runs, baseline runs and property tests. fresh(seed) returns an independent live world.
local world = require("sim.world")
local worldgen = require("sim.worldgen.init")
local save = require("sim.save")

local M = {}
local cache = {}

function M.snapshot(W)
  local snap = {}
  for _, k in ipairs(world.SAVE_KEYS) do
    if k ~= "cmdlog" and k ~= "pending" then snap[k] = save.deepcopy(W[k]) end
  end
  return snap
end

function M.restore(snap)
  -- other tests (the toy world) swap the global system list; always run the real one here
  world.set_systems(require("sim.systems.init"))
  local W = world.new(snap.seed, { level = snap.level })
  for _, k in ipairs(world.SAVE_KEYS) do
    if k == "E" then
      local src = save.deepcopy(snap.E)
      for kk, vv in pairs(src) do W.E[kk] = vv end
      W.E.feed = {}
    elseif k ~= "cmdlog" and k ~= "pending" and snap[k] ~= nil then
      W[k] = save.deepcopy(snap[k])
    end
  end
  W.cmdlog, W.pending = {}, {}
  world.rebuild_derived(W)
  world.init_systems(W)
  return W
end

-- worldgen report of the cached generation (settle / terrain constraints), by seed
M.reports = {}

function M.fresh(seed)
  local snap = cache[seed]
  if not snap then
    local W = worldgen.generate(seed)
    M.reports[seed] = W.worldgen_report
    snap = M.snapshot(W)
    cache[seed] = snap
  end
  return M.restore(snap)
end

return M
