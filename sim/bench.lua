-- Representative-tick benchmark shared by tools/bench.lua (LuaJIT) and the browser bridge
-- (HTML5 Lua 5.1). The host passes a millisecond clock: sim/ never reads real time itself.
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local climate_init = require("sim.worldgen.climate_init")

local bench = {}

-- opts: level, seed, warm (ticks before measuring), ticks, clock (fn -> ms), spinup_years
function bench.run(opts)
  local clock = assert(opts.clock, "clock required")
  local t0 = clock()
  local W = worldgen.generate(opts.seed or 42, { level = opts.level, spinup_years = opts.spinup_years or 0,
    no_spinup = (opts.spinup_years or 0) == 0 })
  if (opts.spinup_years or 0) == 0 then
    -- no spin-up: settle climate briefly, seed vegetation so every system has work to do
    for _ = 1, 48 do world.tick(W) end
    climate_init.vegetation(W)
    climate_init.fauna(W)
    require("sim.worldgen.settle").run(W)
    require("sim.systems.biome").reset(W)
  end
  local tgen = clock() - t0
  for _ = 1, opts.warm or 48 do world.tick(W) end
  local times = {}
  W.profile = { clock = clock, ms = {} }
  local n = opts.ticks or 96
  for i = 1, n do
    local a = clock()
    world.tick(W)
    times[i] = clock() - a
  end
  table.sort(times)
  local res = {
    level = W.level, cells = W.n, ticks = n, gen_ms = tgen,
    median_ms = times[math.floor(n / 2) + 1], p95_ms = times[math.floor(n * 0.95) + 1],
    max_ms = times[n], systems = {},
  }
  for _, s in ipairs(world.systems) do
    res.systems[s.name] = (W.profile.ms[s.name] or 0) / n
  end
  W.profile = nil
  return res, W
end

return bench
