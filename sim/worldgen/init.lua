-- World generation orchestration (Stage 1.3). Deterministic per seed: attempts are keyed by
-- (seed, attempt) and the first attempt satisfying every constraint wins.
local world = require("sim.world")
local config = require("sim.config")
local terrain = require("sim.worldgen.terrain")
local geo = require("sim.geo")
local climate_init = require("sim.worldgen.climate_init")
local regions = require("sim.worldgen.regions")
require("sim.testhooks")   -- registers the test_action command (07_TESTING.md §3)

local worldgen = {}

-- Validate terrain constraints; returns ok, report table.
function worldgen.check_terrain(W)
  local G = W.geo
  local cfg = config.WORLDGEN
  local area = W.grid.area
  local total = 0
  for c = 1, W.n do total = total + area[c] end
  local land = 0
  for _, c in ipairs(G.land_cells) do land = land + area[c] end
  local rep = { land_frac = land / total, continents = 0, islands = 0, sizes = {} }
  for _, comp in ipairs(G.components) do
    local a = 0
    for _, c in ipairs(comp.cells) do a = a + area[c] end
    local frac = a / total
    comp.frac = frac
    if frac >= cfg.continent_min_frac then rep.continents = rep.continents + 1
    else rep.islands = rep.islands + 1 end
    rep.sizes[#rep.sizes + 1] = frac
  end
  local ok = rep.land_frac >= cfg.land_frac[1] and rep.land_frac <= cfg.land_frac[2]
    and rep.continents >= 2 and rep.continents <= 3
    and rep.islands >= cfg.islands_min and rep.islands <= cfg.islands_max
  return ok, rep
end

-- Generate a world. opts.level, opts.spinup_years (default config), opts.no_spinup.
function worldgen.generate(seed, opts)
  opts = opts or {}
  local cfg = config.WORLDGEN
  local W, rep
  for attempt = 1, cfg.max_attempts do
    W = world.new(seed, { level = opts.level })
    local info = terrain.generate(W, attempt)
    if info then
      geo.rebuild(W)
      local ok
      ok, rep = worldgen.check_terrain(W)
      rep.attempt = attempt
      if ok then break end
    end
    W = nil
  end
  if not W then error("worldgen failed for seed " .. tostring(seed)) end
  W.worldgen_report = rep
  regions.generate(W)
  world.set_systems(opts.systems or require("sim.systems.init"))
  world.rebuild_derived(W)
  world.init_systems(W)
  climate_init.apply(W)
  if worldgen.post_climate then worldgen.post_climate(W, opts) end
  if not opts.no_spinup then worldgen.spinup(W, opts.spinup_years or config.SPINUP_YEARS, opts.progress) end
  return W
end

-- Spin-up: simulate pre-history without recording events while the global climate measures
-- its references, then reset the clock to Year 0.
function worldgen.spinup(W, years, progress)
  W.record_events = false
  W.G.calibrating = true
  local ticks = years * 48
  local veg_at = math.min(ticks, config.SPINUP_VEG_YEAR * 48)
  for t = 1, ticks do
    world.tick(W)
    if t == veg_at then
      climate_init.vegetation(W)
      climate_init.fauna(W)
      require("sim.systems.biome").reset(W)
    end
    if progress and t % 48 == 0 then progress(t / ticks) end
  end
  W.G.calibrating = false
  W.record_events = true
  world.shift_ticks(W, -W.tick)
end

return worldgen
