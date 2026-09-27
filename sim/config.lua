-- Every tunable constant (02_WORLD_MODEL.md: "never hard-code a tunable inside a system").
-- Grouped by system; units and intended effect in comments. Tuning is expected. Changing a
-- tuning *band* or test *threshold* needs a DECISIONS.md entry; changing these doesn't.

local C = {}

-- Grid --------------------------------------------------------------------------------------
C.SIM_LEVEL = 12           -- grid: icosphere level (3 = 642, 4 = 2,562 cells) or geodesic frequency >= 10 (12 = 1,442 cells); D-004, D-030, D-047

-- Time --------------------------------------------------------------------------------------
C.TICKS_PER_YEAR = 48
C.SPINUP_YEARS = 30        -- worldgen spin-up (no events recorded)
C.SPINUP_VEG_YEAR = 4      -- spin-up year at which vegetation is seeded from the settled climate
C.SPINUP_FERT_BOOST = 8    -- during spin-up, fertility regeneration and erosion run this much faster: soils
                           --   reach their equilibrium (a ~40-year relaxation) before Year 0 instead of drifting

-- Events --------------------------------------------------------------------------------------
C.EVENT_CAP = 8192

-- Worldgen (02_WORLD_MODEL.md §2) -----------------------------------------------------------------
C.WORLDGEN = {
  max_attempts = 40,
  plates_min = 6, plates_max = 10,
  p_three_continents = 0.5,
  continent_max_lat = 0.95,                        -- rad: continent centres stay off the poles
  continent_min_sep = { [2] = 1.9, [3] = 1.45 },   -- rad between continent centres
  continent_frac = { [2] = { 0.155, 0.19 }, [3] = { 0.121, 0.127 } }, -- of surface, each
  islands_min = 4, islands_max = 8,
  island_size = { 1, 5 },                          -- cells at level 4 (scaled with level)
  coast_roughness = 0.95,                          -- noise weight in continent growth cost
  land_base = 0.12,                                -- km, lowland elevation
  mountain_height = 2.3,                           -- km added at convergent land boundaries
  ridge_height = 1.3,                              -- km, interior ridged-noise ranges
  ocean_depth = 3.6,                               -- km below the shelf in open ocean
  land_frac = { 0.30, 0.40 },                      -- constraint on total land
  continent_min_frac = 0.12,                       -- constraint: each continent >= this
}

-- Temperature (§5.1) ------------------------------------------------------------------------------
C.TEMP = {
  t_equator = 28, t_pole_drop = 42,       -- T_base(lat) = 28 - 42 sin^2(lat)  (°C)
  season_amp = 12, ocean_amp_factor = 0.4, coastal_damping = 0.3,
  season_phase = -math.pi / 4,            -- northern mid-summer peaks at tick-of-year 18
  lapse = -6.5,                           -- °C per km above sea level
  k_aer = 1.0,                            -- °C of cooling per unit aerosol (fast, no lag)
  heat_anom_decay_land = 0.8, heat_anom_decay_ocean = 0.98,   -- per tick
  sst_rate = 0.1, sst_min = -1.8, sst_hold = 96,
  anom_min = 1.0,                         -- °C: smaller anomalies don't claim temp's tag
  wobble_patterns = 3, wobble_amp = 1.2,  -- slow interannual temperature noise (°C)
}

-- Moisture & wind (§5.2–5.3) -------------------------------------------------------------------------
C.MOIST = {
  band_shift = { 0, 5, 0, -5 },           -- degrees the wind bands move north per season
  zonal = { { 0, -1 }, { 15, -0.9 }, { 30, 0 }, { 45, 1 }, { 60, 0 }, { 75, -0.6 }, { 90, 0 } },
  merid = { { 0, 0 }, { 10, -0.35 }, { 25, -0.2 }, { 30, 0 }, { 45, 0.25 }, { 60, 0 }, { 75, -0.25 }, { 90, 0 } },
  wind_speed = 0.8, courant_max = 0.85,   -- advection fraction per tick per unit wind
  diffusion = 0.06,                       -- at level-4 spacing (/sscale²); 0.04 at level 3 (finer grids have less numerical diffusion, D-047)
  k_eo = 0.08,                            -- ocean evaporation per tick at sst 15 °C
  k_el = 0.06,                            -- land evapotranspiration scale
  k_cap = 1.0,                            -- capacity at 0 °C (x exp(0.06 T))
  k_oro = 0.5,                            -- capacity loss per km of upslope lift
  k_rain = 0.5, k_drizzle = 0.06,
  -- capacity multiplier by |lat| (degrees, band-shifted): ITCZ wet, subtropics dry
  cap_profile = { { 0, 0.7 }, { 8, 0.75 }, { 15, 1.0 }, { 22, 1.5 }, { 30, 1.7 }, { 38, 1.2 }, { 50, 0.95 }, { 65, 1.0 }, { 90, 1.2 } },
  inflow_share = 0.3,                     -- a tagged inflow must carry ≥ 30% to pass its tag on
  -- evaporation wobble (interannual variability): sea evaporation x (1 + amp x sum of pattern x coefficient)
  rain_wobble_patterns = 2, rain_wobble_freq = 1.3, rain_wobble_amp = 0.6,
  rain_wobble_years = 2,                  -- coefficient knots every 2 years (multi-year dry/wet spells)
  rain_wobble_floor = 0.3,                -- evaporation factor never below this
}

-- Ice (§5.4) -------------------------------------------------------------------------------------------
C.ICE = {
  k_snow = 1.2, k_melt = 0.05,            -- cover per unit snowfall / per °C per tick
  sea_grow = 0.08, sea_melt = 0.1, sea_freeze_temp = -2, sst_freeze = -1.8,
  melt_water = 0.5,                       -- melt (cover units) -> water into the soil
}

-- Hydrology (§5.5) -------------------------------------------------------------------------------------
C.HYDRO = {
  k_ps = 0.5, k_es = 1.0,                 -- moisture units -> soil water; evaporation draw
  field_capacity = 0.7, k_run = 0.5, k_base = 0.01,
  k_ero = 0.02, erosion_max = 0.002, silt_carry = 0.9,
  lake_evap = 0.05, river_threshold = 0.15, tag_share = 0.3,
  flow_ema = 0.01,
  bankfull_ema = 0.15,                    -- per year: bankfull = EMA of the yearly peak flow (mean annual flood)
  flood_mult = 2.0, flood_min = 0.05,     -- flood when flow > flood_mult x bankfull on a river of at least flood_min
  floodplain = 0.05,                      -- km: land neighbours up to this much higher are flooded too
  flood_silt = 0.01,                      -- fertility left per tick of overbank flow (x overflow share, <= 1)
}

-- Global climate (§5.13) -----------------------------------------------------------------------------------
C.CLIMATE = {
  aerosol_decay = math.exp(-1 / 72),      -- per tick (e-folding 1.5 years)
  calib_skip_years = 10,                  -- spin-up years ignored before measuring references
  co2_base = 280, outgassing = 0.5,       -- ppm/yr volcanic outgassing (balanced by weathering)
  k_eq_sst = 8, k_ocean = 0.02,           -- ocean equilibrium shift per °C; uptake per yr
  co2_min = 50, co2_max = 5000,
  sensitivity = 3.0,                      -- °C per CO2 doubling
  k_albedo = 8.0,                         -- °C per unit ice-fraction below the reference
  k_vapor = 0.1, tau_years = 10,
  g_temp_min = -30, g_temp_max = 20,
  k_ice_sea = 1.0, k_thermal = 0.01,      -- km sea level per ice-volume fraction / per °C
  era_warm = 2, era_hothouse = 6, era_iceage = 0.35, era_snowball = 0.85,
  era_hysteresis = { ice = 0.03, temp = 0.3 },
}

-- Vegetation & soil (§5.6) -----------------------------------------------------------------------------------
C.VEG = {
  stride = 2,                             -- each land cell updates every N ticks (dt = N), staggered (D-019)
  r_grass = 0.06, r_forest = 0.006,       -- per tick at ideal conditions (grass 1–3 y, forest 30–60 y)
  K_grass = 1.0, K_forest = 0.95,
  die_grass = 0.03, die_forest = 0.0025,  -- per tick under full water stress
  die_frost = 0.004, frost_temp = -8,     -- slow frost dieback of grass below this temp
  soil_half_grass = 0.2, soil_half_forest = 0.45,
  seed_grass = 0.01, seed_forest = 0.002, -- recolonisation from neighbours
  grass_t = { 0, 36 }, forest_t = { 2, 36 },   -- temperature bells (°C)
  light_amp = 0.4,
  k_regen = 0.0005, fert_base = { 0.3, 0.8 },  -- fertility target from bare to fully vegetated
  fire_block_ticks = 96, scar_expire = 480,      -- recent burns slow forest establishment
  k_co2 = 0.4,                            -- ppm per unit biomass (growth absorbs, burning emits)
}

-- Fire (§5.7) --------------------------------------------------------------------------------------------------
C.FIRE = {
  k_lightning = 1.9e-3,                   -- ignition probability scale per land cell per tick
  storm_precip = 5,                       -- summer storms: more lightning where it rains a little
  min_fuel = 0.08, start_intensity = 0.6,
  k_spread = 0.46, wind_boost = 1.5,
  burn_grass = 0.6, burn_forest = 0.25,   -- fraction consumed per tick at intensity 1
  intensity_per_fuel = 1.5, min_intensity = 0.12,
  rain_extinguish = 4,                    -- intensity loss per unit precip
  k_co2 = 0.4,                            -- ppm per unit biomass burned
  k_heat = 3,                             -- heat_anom °C per unit intensity per tick
  wildfire_cells = 8, ash_fertility = 0.15,          -- wildfire: level-4-equivalent cells burning (8 since D-047)
}

-- Biomes (§5.15): entry / exit thresholds (hysteresis) and dwell ------------------------------------------
C.BIOME = {
  sea_ice_enter = 0.5, sea_ice_exit = 0.35, ice_sheet_enter = 0.8, ice_sheet_exit = 0.6,
  ash_ticks = 24,
  marsh_enter = 0.85, marsh_exit = 0.75, marsh_max_elev = 0.4,
  desert_soil = 0.17, desert_veg = 0.17, desert_min_temp = 5, desert_exit_veg = 0.3, desert_exit_soil = 0.25,
  tundra_temp = 0, tundra_veg = 0.4, tundra_exit_veg = 0.5,
  jungle_enter = 0.6, jungle_exit = 0.5, jungle_temp = 22, jungle_soil = 0.5, jungle_soil_exit = 0.35,
  forest_enter = 0.42, forest_exit = 0.34, grass_enter = 0.25, grass_exit = 0.18,
  dwell = 12, dwell_long = 48,           -- ticks
  stride = 4,                             -- a rotating 1/stride of the cells is classified per tick (D-019)
}

C.LEDGER = { decay_per_year = 0.03, dominant_share = 0.4 }

-- Regions (named places for events; 02_WORLD_MODEL.md §2, D-011) ---------------------------------------------
C.REGIONS = {
  split_frac = 0.4,                       -- continents holding more than this share of land split in two
  seas_per_ocean = 12,                    -- seas ≈ ocean fraction × this, clamped to [seas_min, seas_max]
  seas_min = 6, seas_max = 10,
  sea_jitter = 0.8,                       -- next sea centre drawn among cells ≥ this × max distance
}

-- Ocean life, currents & oil (§5.8) ----------------------------------------------------------------------------
C.OCEAN = {
  stride = 8,                             -- each ocean cell's life update runs every N ticks (dt = N), staggered
  r_plankton = 0.08,                      -- plankton growth per tick at ideal light/nutrients/temperature
  nut_base = 0.35, nut_up = 0.65,         -- nutrients = base + up × upwelling + river pulse
  nut_river = 0.6, river_ref = 0.5,       -- river-mouth nutrient pulse: nut_river × min(1, flow / river_ref)
  sst_lo = 10, sst_hi = 26,               -- plankton temperature optimum (°C)
  sst_cold_min = -2, cold_floor = 0.35,   -- cold seas still grow at this fraction
  sst_hot = 30, heat_die = 0.06,          -- marine heatwave: no growth above sst_hot, die-off per tick per °C above
  light_amp = 0.4,
  graze = 0.03,                           -- plankton eaten per tick per unit fish
  k_oil_p = 2.0, oil_kill_p = 0.1,        -- plankton growth loss per unit oil; direct kill per tick per unit oil
  r_fish = 0.03, h_fish = 0.15,           -- fish growth per tick; plankton half-saturation
  m_fish = 0.006,                         -- natural fish mortality per tick
  k_oil_f = 0.45,                         -- fish killed per tick per unit oil
  heat_fish_t = 31, heat_fish = 0.02,     -- direct heat stress per tick per °C above
  silt_pen = 0.01, silt_ref = 0.01,       -- fish spawning loss per tick at silt ≥ silt_ref
  fish_mig = 0.03,                        -- fish diffusion per tick at level-4 spacing (/sscale²)
  flow_tag_frac = 0.05,                   -- fish inflow passes its tag only if ≥ this × fish per update
  ref_months = 60,                        -- per-cell "normal" fish (5-year EMA) for collapse detection
  min_pop = 1e-5,                         -- below this a population is gone (no spawning from nothing)
  init_plankton = 0.4, init_fish = 0.4,
  -- currents (push weights, D-015) and oil
  current_speed = 0.5, courant_max = 0.5,
  oil_diffusion = 0.03,                   -- per tick at level-4 spacing (/sscale²)
  oil_half_life = 96,                     -- ticks (≈ 2 years)
  oil_min = 1e-4,                         -- oil below this is gone (leaves the active set)
  beach = 0.06,                           -- fraction of a coastal ocean cell's oil washed ashore per tick
  shore_half_life = 24, leach = 0.02,     -- beached oil: half-life (ticks); fraction leaching back to sea per tick
  shore_grass = 0.08, shore_fert = 0.003, -- beached oil damage per tick per unit oil (grass fraction; fertility)
}

-- Land animals (§5.9) --------------------------------------------------------------------------------------------
C.FAUNA = {
  stride = 4,                             -- local dynamics per land cell every N ticks (dt = N), staggered
  migrate_every = 8,                      -- each species migrates every N ticks (species staggered, rate × N)
  r_grazer = 0.03, K_grazer = 1.0,        -- grazer births per tick; capacity at abundant food
  h_food = 0.15, crop_food = 0.5,         -- food half-saturation; crop counts half (raids)
  snow_block = 0.8,                       -- snow/ice cover hides this fraction of food (winter starvation)
  starve_max = 0.03,                      -- max starvation loss per tick
  a_pred = 1.5, t_handle = 4,             -- Holling II attack rate, handling time (ticks)
  refuge = 0.018,                         -- prey refuge density (never eaten); larger damps the cycles
  e_pred = 0.1, m_pred = 0.010,           -- predator conversion efficiency; mortality per tick
                                          -- (predator units are 1/3 of grazer units: a×3, t_h/3, e/3)
  graze = 0.004, h_graze = 0.1,           -- grass eaten per tick per unit grazer (saturating in grass)
  browse = 0.001, h_browse = 0.08,        -- forest browsed: browse × H × (browse_base + hunger) × f/(f + h):
  browse_base = 0.05,                    --   per unit strongest when forest is low; hunger = H / K_eff (≤ 1)
  raid = 0.01, h_raid = 0.1,              -- crop raided per tick per unit grazer
  fire_mort = 0.3,                        -- mortality per tick per unit fire intensity
  mig_grazer = 0.04, mig_pred = 0.04,     -- migration per edge per tick at level-4 spacing (/sscale²); × migrate_every ≤ ~0.1
  fear = 30,                              -- grazer attractiveness ÷ (1 + fear × predators)
  attract0 = 0.05,                        -- base attractiveness (random wandering)
  ice_walk = 0.8,                         -- sea ice this thick is walkable
  pressure_above = 1.25,                  -- grazing/predation carry the animals' tag only above this × normal
  release_frac = 0.5,                     -- "release" blame: local density below this × its normal ...
  release_min = { grazers = 0.02, predators = 0.002 },  -- ... and the normal above this
  ref_months = 60,                        -- per-cell "normal" density (5-year EMA)
  flow_tag_frac = 0.05,                   -- migration passes its tag only if net inflow ≥ this × population
  min_pop = 1e-5,
  init_grazers = 0.3, init_predators = 0.013,
}

-- Villages (§5.10) ------------------------------------------------------------------------------------------------
-- Food is counted in person-months (an adult eats 1 per month, a child 0.6).
C.VILLAGE = {
  ring = 2, near = 3,                     -- farm/fishing radius and "affects the village" radius, level-4 steps
  k_farm = 0.45,                          -- level-4 farm cells per farmer
  granary_burn = 0.8, granary_burn_max = 0.6, -- share of the store lost per month of fire in the village (x intensity, capped)
  max_farm_l4 = 99,                       -- a village farms at most this many level-4 cells (99: no limit)
  yield = 66,                             -- person-months per unit crop per level-4 cell per harvest
  farmer_max = 30,                        -- person-months a farmer can bring in per year
  crop_seed = 0.1, r_crop = 0.6,          -- planted in spring; logistic growth per tick at ideal conditions
  crop_t = { 8, 32 }, crop_soil_half = 0.2,
  plant_toy = 12, harvest_toy = 32,       -- planting before tick-of-year 12 (spring); harvest from 32
  drought_soil = 0.12, k_drought = 0.05,  -- crop loss per tick when the soil is this dry
  frost_temp = -2, k_frost = 0.08,        -- crop loss per tick in frost during the season
  k_oil_crop = 0.5,                       -- crop loss per tick per unit of beached oil
  k_blight = 0.15,                        -- crop loss per tick on blighted farms (hook: W.S.blight[c] = tag)
  k_flood_crop = 0.6,                     -- share of the standing crop a river flood drowns
  flood_news_years = 5,                   -- a village's floods make the news at most this often
  deplete = 0.003,                        -- fertility lost per unit crop harvested
  fallow_fert = 0.22,                     -- below this a farm lies fallow for a year (if the village can afford it)
  fallow_fs = 6,                          -- months of food security below which exhausted fields are planted anyway
  poor_fert = 0.05,                       -- a hungry village clears fields down to this fertility
  spoil = 0.08,                           -- per month, of the store beyond `granary` months of needs
  granary = 9,                            -- months of needs a granary holds; the rest spoils
  fish_q = 20,                            -- catchable person-months per unit fish per level-4 cell per month
                                          --   (= 5% of the stock a month: near the fish's best sustainable yield)
  fisher_max = 2.5,                       -- person-months a fisher can land per month
  fish_units = 340,                       -- person-months of fish per unit density per level-4 cell
  fish_share = 0.45,                      -- at most this share of working adults fish
  hunt = 0.004,                           -- grazer fraction taken per tick per hunter per level-4 cell
  meat_units = 300,                       -- person-months per unit grazer density per level-4 cell
  cull_threshold = 0.004, cull_rate = 0.02, cull_prob = 0.12,   -- predators near farms: monthly chance to cull
  k_burn = 0.00042,                       -- ppm co2 per person-month of wood burning (x (1 + winter cold))
  wood = 0.00001,                         -- forest removed per person-month from the village cells
  k_clear_co2 = 0.4,                      -- ppm per unit forest cleared for fields
  eat_adult = 1.0, eat_child = 0.6,
  fs_smooth = 0.5,                        -- food security EMA per month
  famine_fs = 1, famine_exit = 2,         -- months of food
  migrate_months = 6, migrate_to_fs = 3, min_founders = 4, min_spacing = 3,
  min_site_people = 20,                   -- a new village's site must be able to feed this many (capacity)
  -- daughter villages: a village of >= bud_pop with >= bud_fs months of food whose food margin fell
  -- under bud_rho (land filling up) sends bud_share of its people (young families, adults <= bud_age)
  -- with start_food months of provisions to found a village; a monthly chance bud_prob in the
  -- spring months before planting, at most once per bud_years, while the planet has fewer than
  -- bud_total people and fewer than max_villages villages
  bud_pop = 60, bud_fs = 5, bud_rho = 1.25, bud_share = 0.2, bud_age = 35, bud_prob = 0.1,
  bud_years = 15, bud_total = 330, max_villages = 8,
  bud_big = 100,                          -- ... or a village this big splits whatever its food margin
  houses_per = 4,
  accident = 1.5e-5,                      -- village fire: ignition chance per person-month x dryness
  start_food = 8,                         -- months of food when founded
  ruins_years = 20,
  cause_hold = 12,                        -- months with food to spare (a full harvest cycle) before the cause clears
  hist_months = 36,                       -- food history (production, consumption) EMA length, months
  birth_rho = { 1.05, 1.35 },             -- births floor -> full as production / consumption goes 1.05 -> 1.35
  birth_floor = 0.1,
  migrate_rho = 0.95, chronic_months = 3, -- a chronic deficit sends a family away every few months
  comfort = 1.4,                          -- capacity = people fed with this margin (a store to last the year)
  fishing_cap_share = 0.5,                -- sustainable share of fish regrowth counted in capacity
}

C.PEOPLE = {
  adult_age = 14, elder_age = 60, woodcutter_share = 0.08,
  memories = 6, grief_salience = 1.0, memory_fade = 0.8,
  hunger_rate = 0.5, hunger_hurts = 30, health_loss = 0.4, health_regen = 5,
  vulnerable_young = 5, vulnerable_mult = 2,      -- children under 5 and elders lose health this much faster
  child_hazard = 0.0015, adult_hazard = 0.0004,   -- per month
  old_age = 50, old_hazard = 0.0012, old_scale = 8,  -- + old_hazard x exp((age - old_age) / old_scale)
  frail = 40, frail_hazard = 0.006,       -- health below frail: + (frail - health) x this per month
  pair_min = 18, pair_max = 50, pair_rate = 0.1,
  birth_min = 18, birth_max = 40, birth_rate = 0.02,    -- per month for partnered women
  birth_fs = { 0.5, 2.5 },                -- births scale 0 -> full as the hungry gap (fs_low) goes 0.5 -> 2.5 months
  global_cap = 500, global_soft = 450, cap_soft = 0.55,   -- hard cap; births taper to 0 over the last
                                          --   55% below the soft ceiling (keeps the planet under ~400)
  keep_dead_years = 10,
}

-- Ships (§5.11) -------------------------------------------------------------------------------------------------
C.SHIPS = {
  tanker_speed = 0.12,                    -- level-4 cells per tick (scaled by sscale)
  cargo = 36,                             -- oil released when a tanker sinks (level-4-equivalent cells of full cover; D-047)
  replace_years = { 3, 5 },               -- a replacement tanker arrives after this long (≥ 2 villages)
  fishers_per_boat = 3,
}

-- Worldgen settle (§2) --------------------------------------------------------------------------------------------
C.SETTLE = {
  year = 8,                               -- spin-up year at which the first villages are founded
  villages = { 3, 5 }, people = { 150, 250 },
  lanes = { 1, 2 },
  arcs = { 5, 10 }, hotspots = { 1, 3 }, volcano_spacing = 3,
}

-- Detectors (03_CAUSALITY.md §4) -----------------------------------------------------------------------------------
C.DETECT = {
  avg_months = 60,                        -- regional 5-year average (EMA of monthly samples)
  boom = 2.0, boom_exit = 1.4,            -- × 5-year average (entry / exit)
  boom_local = 2.2, boom_cells = 10,      -- local boom: connected cells ≥ 2.2 × their own normal, ≥ 10 level-4 cells
  crash = 0.5, crash_exit = 0.75,
  news_years = 10,                        -- a region's natural boom/crash headlines at most this often (D-047)
  cooldown = 96,                          -- ticks between boom/crash events per region + species
  min_density = { grazers = 0.01, predators = 0.001 },   -- regional mean below this: too few to judge
  extinct_density = 1e-4, present_density = 1e-3,
  dominant_share = 0.2,                   -- a tag must cover this share of the region's cells to be the parent
  fish_low = 0.5, fish_exit = 0.75, fish_min_ref = 0.05,
  fish_cells = 6,                         -- level-4-equivalent cells in a collapsed fishing ground (min 2)
  oil_spread_cells = 10,                  -- level-4-equivalent cells in a connected slick (min 3)
  oil_spread_min = 0.01, oil_beach_min = 0.005,
  -- drought: connected land (>= drought_cells level-4 cells) whose soil water stayed below drought_anom x
  -- its own seasonal normal (>= drought_norm_min) for drought_months; a cell recovers at drought_exit_anom
  drought_anom = 0.7, drought_months = 3, drought_cells = 12, drought_norm_min = 0.12, drought_exit_anom = 0.9,
  flood_cooldown = 24,                    -- ticks before the same cell floods again (as an event)
  crop_failure = 0.5,                     -- harvest below this x the village's 5-year average
  famine_cooldown = 24,
  deaths_spike = 3,                       -- non-age deaths in a village in one month
}

return C
