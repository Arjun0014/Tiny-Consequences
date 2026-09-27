-- Determinism self-test (07_TESTING.md §5 W06), shared by the game's bridge (the HTML5 Lua 5.1
-- runtime) and tools/selftest.lua (LuaJIT): build a world (a save string, or worldgen for a
-- seed with a short spin-up), ignite the first 3 forested land cells as a test action, run
-- `years`, and report the state hash. Same inputs give the same hash in both runtimes, so the
-- headless suite speaks for the browser build.
local world = require("sim.world")
local worlds = require("sim.worlds")
require("sim.testhooks")

local selftest = {}

-- opts: save (string) | seed (number), years (default 2)
function selftest.run(opts)
  local W
  if opts.save then
    local err
    W, err = worlds.from_save_string(opts.save)
    assert(W, "restore failed: " .. tostring(err))
  else
    W = worlds.generate(opts.seed or 5, { spinup_years = 2 })
  end
  local cells = {}
  for c = 1, W.n do
    if W.geo.ocean[c] == 0 and W.F.forest[c] > 0.3 then
      cells[#cells + 1] = c
      if #cells >= 3 then break end
    end
  end
  world.test_action(W, "ignite", { cells = cells })
  for _ = 1, (opts.years or 2) * 48 do world.tick(W) end
  return { hash = world.state_hash(W), tick = W.tick, events = W.E.total, ignited = #cells }
end

return selftest
