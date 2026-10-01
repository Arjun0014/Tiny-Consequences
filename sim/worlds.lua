-- World entry points for the game (pure sim, deterministic):
--   worlds.generate(seed)               worldgen + spin-up (seconds at level 3)
--   worlds.from_save_string(str)        decode + restore a save string (e.g. a pre-spun world
--                                       shipped in assets/worlds/, 02 §2) -> W, or nil, err
--   worlds.year0_save_string(W, extras) the Year-0 save of a freshly generated world (used by
--                                       tools/make_world.lua to make assets/worlds/default.tcsave)
-- A world restored from a Year-0 save equals the freshly generated world tick for tick (the
-- save carries the same year-boundary snap the first tick performs; unit-tested).
local world = require("sim.world")
local fields = require("sim.fields")
local save = require("sim.save")
local worldgen = require("sim.worldgen.init")

local worlds = {}

function worlds.generate(seed, opts)
  return worldgen.generate(seed, opts)
end

function worlds.from_save_string(str)
  local doc, err = save.decode(str)
  if not doc then return nil, err end
  -- v3 worlds (D-078) run the agent layer instead of the v2 society and tankers
  world.set_systems(doc.v3 and require("sim.agents.worldgen").systems() or require("sim.systems.init"))
  return save.restore(doc)
end

function worlds.year0_save_string(W, extras)
  -- the year-boundary snap tick W.tick would perform, then the capture (idempotent: the
  -- restored world's first tick snaps again with no effect)
  if W.tick % 48 ~= 0 then error("year0_save_string: world is not at a year boundary") end
  fields.snap_all(W)
  for i = 1, #world.systems do
    local s = world.systems[i]
    if s.snap then s.snap(W) end
  end
  local snap = save.capture(W)
  return save.encode(snap, {}, W.tick, extras or {})
end

return worlds
