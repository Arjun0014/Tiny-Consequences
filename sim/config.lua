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
  k_ice_sea = 1.0, k_thermal = 0.004,     -- km sea level per ice-volume fraction / per °C (equilibrium)
  tau_sea_years = 100,                    -- thermal expansion follows the temperature this slowly
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
  grass_c = 0.5,                          -- carbon in a unit of grass cover, in forest units: every flux
                                          --   (growth, fire, grazing, gathering) counts grass at this weight
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
  overgraze = 3.0, overgraze_from = 0.7,  -- starving herds (hunger = H / K_eff above 0.7) graze up to 4x as hard (D-049)
  browse = 0.0013, h_browse = 0.08,       -- forest browsed (0.001 until D-049): browse × H × (browse_base + hunger) × f/(f + h):
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

-- Wild herds and packs as entities (docs/world/14_ECOLOGY.md §3, sim/systems/fauna.lua, D-070). The per-cell
-- ecology is FAUNA's (above); these are the groups': sizes, ranges, movement, migration, hunting.
C.WILD = {
  every = 4,                              -- herds and packs update every N ticks (dt = N: a month)
  look_every = 2,                         -- a settled herd reconsiders its range every this many updates
  gpc = 100, ppc = 25,                    -- animals per unit of the grazers / predators field (at the old equilibrium
                                          --   ~13 deer and ~1 wolf per cell; a wolf takes ~15-20 deer a year)
  herd_w0 = 0.4,                          -- a herd's time on its centre cell (the rest on the cells about it)
  pack_w = { 0.25, 0.4, 0.35 },           -- a pack's time at its den, on the first ring, on the second
  cond0 = 0.6,                            -- body condition (fat reserves 0..1) of a new group
  -- herds (D-071): forage per head = min(i_max, food / (phi H + psi)) - snow_cost x snow; 1 holds condition
  phi = 0.6, psi = 0.02, i_max = 1.5,     --   (1 where the herds reach what the grass carries, K_eff)
  snow_cost = 0.8,                        --   wading through deep snow costs this much forage
  h_gain = 0.2,                           -- condition per month per unit of forage above / below 1
  fawns = 1.0, fawn_cond = { 0.2, 0.7 },  -- fawns per head a year, by the herd's condition in the late spring (ramp)
                                          --   (was 1.4: lambda ~2.2 without predators; deer reach ~1.6-1.8, D-073)
  fawn_crowd = 1.0,                       -- ... that live: / (1 + (crowding / this)^2), crowding = animals / what the range's grass carries
  fawn_week = 16,                         -- local week of the fawning (one monthly update)
  h_starve = 0.25, h_starve_max = 0.25,   -- below this condition the weak starve (a month's share at condition 0)
  h_mort = 0.01,                          -- old age and accidents, a month
  -- predation: a' = a_pred x (1 + snow_kill x snow) x (0.7 + weak_kill x weakness of the prey), weakness = 1 - condition
  snow_kill = 1.5, weak_kill = 0.75,      --   (wolves kill 2-3x as often in deep snow and take the weak)
  -- packs: kills per wolf against need (FAUNA m_pred / e_pred); one litter a year
  p_gain = 0.15, p_food_max = 2,          -- condition per month per unit of kills above / below need (capped)
  pack_exp = 0.6, pack_ref = 5,           -- a pack's kills grow with (size / 5) ^ 0.6: bigger packs eat less a head (Schmidt & Mech 1997)
  small_game = 1.2,                       -- a territory's small game and carrion feed this many wolves (/ pack size)
  litter = 5, litter_cond = { 0.25, 0.75 },  -- pups a year from a pack with a breeding pair, by its condition (ramp)
  pup_week = 12,                          -- local week of the whelping
  p_starve = 0.3, p_starve_max = 0.3,     -- below this condition wolves starve (a month's share at condition 0)
  p_mort = 0.015,                         -- old age, strife, accidents, a month
  den_move_cond = 0.35,                   -- a pack this thin moves its den toward prey (monthly at most)
  disperse_week = 32,                     -- local week of the autumn dispersal
  keep = { 2, 6 },                        -- a pack keeps 2 + 6 x condition wolves; the rest leave
  disperse_survival = 0.6,                -- share of dispersers that live to settle
  disperse_reach = 7,                     -- steps a disperser searches
  rival_fear = 20,                        -- a site's appeal / (1 + this x (packs there: 2 P + 1 at a den))
  pack_min_prey = 0.3,                    -- prey about a site (2 x its density + its first ring) a disperser needs
  mate_pull = 3, adopt = 0.3,
  mate_reach = 8,                         -- steps a lone wolf roams for a mate
  kin_wait_years = 1,                     -- a lone wolf that found no unrelated mate in this long pairs with kin             -- a lone wolf (no kin) draws a disperser this x its prey; a pack with room this x
  grazers = {                             -- herd sizes: past max a herd splits; below min it joins its kind
    { name = "deer", max = 60, min = 4 },
    { name = "reindeer", max = 150, min = 8 },
    { name = "antelope", max = 100, min = 6 },
    { name = "boar", max = 20, min = 3 },
  },
  predators = { { name = "wolf", max = 12, min = 2 }, { name = "cat", max = 10, min = 2 } },
  migrates = { false, true, true, false },   -- reindeer and antelope walk between summer and winter ranges
  migrate_weeks = { 30, 6 },              -- local weeks: to the winter range from week 30, back from week 6
  migrate_reach = 4,                      -- a winter range lies at most this many steps from the summer one
  move_gain = 1.2,                        -- a herd moves when a neighbouring cell is this much better
  crowd = 3,                              -- other herds lower a cell's appeal: / (1 + crowd x their density)
  fields_fear = 0.5,                      -- fields are half as inviting (people about by day)
  split_share = 0.4,                      -- share of a herd that leaves when it splits
  disperse_sat = 1.1,                     -- a herd whose range holds this x what its grass carries sends a part away
  browse_crowd = 2.0,                     -- (grazing: herds crowded past their grass graze it up to this x as hard)
  browse_worth = 0.5,                     -- what herds lack of grass they take from the trees (twigs, bark), worth this much grass
  gone = 0.5,                             -- a group below this many animals is gone
  seed_herd = 36, seed_pack = 7,          -- group sizes gathered from the fields at worldgen
  hunt_rate = 0.04, hunt_max = 0.05,      -- hunters: animals per effective hour (x density/(density + 0.15)); at most
  meat = 40,                              --   this share of a herd a day; FU of meat per animal
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
  heat_anom = 4, heat_exit = 2, heat_cells = 6,   -- heatwave / cold snap: |heat anomaly| >= 4 °C over >= 6 level-4 cells
  sea_ice_on = 0.5, sea_ice_off = 0.2, sea_ice_cells = 6,   -- sea ice formed (caused freezes only)
  flood_cooldown = 24,                    -- ticks before the same cell floods again (as an event)
  crop_failure = 0.5,                     -- harvest below this x the village's 5-year average
  famine_cooldown = 24,
  deaths_spike = 3,                       -- non-age deaths in a village in one month
}

-- Time, sun and daily weather (docs/world/15_CLIMATE_TIME.md) -----------------------------------
C.TIME = {
  tilt = 23.44,                           -- degrees: axial tilt (declination amplitude)
  sunrise_angle = 0.8333,                 -- degrees below the horizon at sunrise/sunset (Forsythe CBM)
  wake_before_sunrise = 0.5, wake_min = 4, wake_max = 8,     -- hours (Yetish et al. 2015)
  sleep_after_sunset = 3.3, sleep_min = 19, sleep_max = 23.5,
  work_max = 10,                          -- working hours in a day (a meal hour on top)
  work_min = 3,                           -- even in polar night people work a few hours by lamplight
}

C.WEATHER_DAY = {
  wet_week = 0.08,                        -- weekly precip (moisture units) of a typically wet week
  p_dw = { 0.03, 0.6 },                   -- P(wet | dry yesterday) = a + b x wetness, clamped
  p_dw_max = 0.5, p_ww_add = 0.22, p_ww_max = 0.72,   -- even the wettest places see sun ~1 day in 3
  storm_day = 0.028,                      -- a wet day with at least this much rain is a storm
  storm_rel = 3.5,                        -- ... and at least this many times the week's mean daily rain
  storm_week_min = 0.04,                  -- ... in a week at least this wet
  gale = 0.04,                            -- daily chance of a gale (boats stay in) at |lat| > 35 deg in deep winter
  gale_tropics = 0.3,                     -- relative gale chance in the tropics
  temp_wobble = 1.5,                      -- °C day-to-day wobble around the weekly temperature
  diurnal = { 4, 12 },                    -- °C day-night range: wet/coastal -> dry/inland
}

-- Settlement and agent layer (docs/world/11_ECONOMY.md, 12_PEOPLE_VILLAGES.md, 13_TRANSPORT.md) --
-- Units: FU = one adult-day of food; WU = wood (one household-day of cooking fuel ~ 0.25 of a cold
-- day's fuel); h = person-hours at full fitness. Distances in cell spacings of the sim grid.
C.SOCIETY = {
  -- needs
  eat = { child = 0.6, adult = 1.0, elder = 0.8 },   -- FU per person-day
  adult_age = 14, elder_age = 60, light_work_age = 10, frail_age = 70,
  fuel_cook = 0.25,                       -- WU per household-day
  fuel_heat = 0.045,                      -- WU per household-day per °C below fuel_comfort
  fuel_comfort = 15,                      -- °C daily mean
  -- stocks and storage
  grain_spoil = 0.0005,                   -- per day (≈ 15% a year)
  fresh_spoil = 0.04,                     -- per day
  wood_rot = 0.001,                       -- per day, beyond sheltered storage
  overflow_spoil = 0.007,                 -- per day, grain beyond storage capacity
  store_house_grain = 400, store_house_wood = 150,
  store_granary = 6000, store_woodshed = 1500,
  -- rationing and coping (research: rationing -> seed eaten -> migration)
  ration_min = 0.55,
  ration_margin_days = 30,                -- ... plus half of this many days of the store's share of the
  ration_safety_days = 10,                --   diet (less as the harvest nears): the buffer kept back
                                          --   when deciding whether the store lasts to the harvest
  income_trust = 0.85,                    -- share of the usual catch/forage counted on until the harvest
  expect_trust = 0.8,                     -- share of grain bought and on its way that the village eats by (B15)
  rationing_days = 3, famine_ration = 0.7, famine_weeks = 4,
  seed_eat_days = 14,                     -- food below this many days: the seed grain is eaten
  -- body
  nutrition_rate = 0.35,                  -- per week toward the ration
  fed_n = 0.85,                           -- nutrition at which the body is fully fed
  hunger_health = 8,                      -- health lost per week toward what the diet sustains
                                          --   (100 when fed, 0 at starve_n - 0.05; the young and old
                                          --   15 lower and twice as fast)
  health_regen = 4,                       -- per week when fed and warm
  malnutrition_mult = 4,                  -- base mortality x (1 + this x shortfall share): disease
  cold_temp = 5, cold_health = 0.8,       -- per week per °C below cold_temp without fuel
  vulnerable_mult = 2,                    -- infants (< 5) and elders lose health this much faster
  crowd_hazard = 1.3,                     -- disease hazard multiplier when overcrowded or homeless
  -- mortality (annual base hazards; weekly = / 48). Gentler than Model West 3 (docs/world/12 §1.3)
  mort_infant = 0.12, mort_child = 0.025, mort_youth = 0.005, mort_adult = 0.006,
  mort_old = 0.0012, mort_old_age = 45, mort_old_scale = 8,
  frail_health = 40, frail_hazard = 0.0006,   -- per week per health point below frail_health
  starve_n = 0.4, starve_hazard = 0.5,    -- weekly death hazard per unit of nutrition below starve_n
  -- fertility
  fecund = { 16, 44 },
  conceive_day = 0.005,                   -- daily conception chance, fecund, fed, not nursing
  preg_days = 252, fetal_loss = 0.15, maternal_death = 0.008, nursing_days = 504,  -- ~18 months of nursing
  -- courtship and households (Hajnal: marriage waits for a home)
  court_age = { f = 19, m = 21 }, court_max_age = 45, court_prob = 0.15,   -- late marriage (Hajnal)
  house_capacity = 7, hut_capacity = 5,
  wait_migrate_months = 24,
  -- work
  work_days = 6,                          -- days a week people work (planning)
  walk_h = 0.33,                          -- hours to walk one cell spacing
  weather_rain = 0.4, weather_storm = 0.0, weather_snow_farm = 0.0,
  -- farming (per sim cell)
  -- (D-057: a field cell is four times the old one — a village's arable, not a plot: 4x the grain,
  -- seed and work per cell, the same work per person fed, a quarter of the land)
  yield = 16000,                          -- FU of grain from a fully sown, fully grown, harvested cell
                                          --   (crops rarely reach full biomass: ~8,000-11,000 in practice)
  seed = 1920,                            -- FU of seed grain to sow a full cell
  sow_h = 1800, tend_h = 1200, harvest_h = 2400,  -- ~15 person-days per person fed; peaks saturate labour
  sow_temp = 7, sow_weeks = 8,            -- the spring sowing season (weeks)
  tropics_lat = 0.41,                     -- radians (23.5 deg): no winter, sow once a year when warm
  gdd_base = 5, gdd_ripe = 1400,
  ripe_loss = 0.004, ripe_loss_wet = 0.012, -- per day, standing ripe crop (shattering, birds, sprouting
                                          --   in the wet): a two-week harvest loses ~5-10%
  weed_rate = 0.02,                       -- weeds per untended week (share of tending not done)
  field_margin = 1.3,                     -- fields for need x this
  dread_margin = 0.2, dread_store = 1.0,  -- a village remembering hunger (dread 0..1, D-064): margin x (1 + 0.2 dread), store years + dread
  fields_per_worker = 0.11,               -- labour bound on cropped fields
  farm_radius = 3,                        -- level-4 steps (scaled): fields within (walking cost in the plan)
  clear_h = 600,                          -- hours to clear a unit of forest for a field
  care_h = 2800, care_gain = 0.45,        -- intensification (Boserup): hours of extra care per full field
                                          --   for up to +45% grain (diminishing returns)
  fallow_fert = 0.22, poor_fert = 0.05, fallow_food_months = 6,
  -- crop growth (weekly; the old villages constants)
  crop_seed = 0.1, r_crop = 0.6, crop_t = { 8, 32 }, crop_soil_half = 0.2,
  drought_soil = 0.12, k_drought = 0.05, frost_temp = -2, k_frost = 0.08, k_oil_crop = 0.5,
  k_blight = 0.15, k_flood_crop = 0.6, deplete = 0.003,
  -- forestry
  wood_rate = 3.0,                        -- WU per productive hour in dense forest
  wood_half = 0.25,                       -- forest density at half the rate
  brush_wu = 4250,                        -- WU of brush, dung and straw a full cell of grass stands for
  forest_wu = 40000,                      -- WU in a fully forested sim cell (D-057: was 4,250 — a cell of
                                          --   woodland is ~170 ha, thousands of household-years of fuel)
  wood_radius = 6,                        -- cells to look for woods (walking eats into the day)
  brush_rate = 1.2,                       -- WU per hour of brush, dung and straw gathering (fuel only)
  coppice_floor = 0.3,                    -- forest cover left on a cut woodlot (stools regrow; D-061)
  coppice_desperate_days = 10,            -- ... unless the woodpile holds fewer days than this
  brush_half = 0.3,                       -- grass density at half the brush rate
  earth_wood = 18, earth_h = 360,         -- an earth (sod / mud-brick) house where timber is scarce
  forage_rate = 0.6,                      -- FU of wild food per hour in lush country (~1,500 kcal/h)
  forage_half = 0.3,                      -- vegetation (grass + 0.6 forest) at half the rate
  forage_fu = 6000,                       -- FU of wild food per unit of grass density in a cell
  forage_days = 45,                       -- food below this many days: people forage
  fuel_days = 60, fuel_days_winter = 90,
  -- fishing
  fish_q = 2.2,                           -- FU per crew-hour at fish density 1 (~8 FU a fisher-day on a
                                          --   healthy ground: 5-10 kg of fish)
  hold = 150, crew = 3,
  fish_fu = 18000,                        -- FU of fish in a sim cell at density 1
  sail_h = 0.3,                           -- hours to sail one cell spacing
  -- sea passage (sim/society/sea.lua, D-059): the villages' own boats carry people overseas
  sea_reach = 24,                         -- sea steps a village's boats sail (the BFS limit)
  sea_inland = 3,                         -- land steps from the landing to an overseas destination
  sea_bias = 2,                           -- extra walking steps a crossing weighs (risk, loading)
  boat_seats = 6,                         -- people (with their loads) a boat carries
  beach_wait_days = 30,                   -- a boat waits this long at a foreign shore for its people
  drown = 0.6,                            -- chance each person aboard drowns when a boat sinks
  voyage_squall = 0.0004,                 -- chance per sea step a crossing is caught by a squall (x squall_wet on a wet day)
  fish_radius = 5,
  squall = 0.00002, squall_wet = 6,       -- chance a trip on a dry / wet day is caught by a squall
                                          --   (storms keep boats in: ~1% of boats a year)
  boat_layup = 0.45, boat_relaunch = 0.9, -- worn boats are laid up and refitted before going out again
  boat_refit = 0.05, boat_mend_wood = 0.5,-- condition per crew-day of refitting, and its timber (WU)
  boat_decay = 0.08,                      -- condition lost per ~150 trips (a busy year)
  boat_mend = 0.004,                      -- condition regained per day in port (crews mend boats and nets)
  fish_share = 0.6, fish_share_short = 1.4,  -- daily catch goal (x need) when stores are fine / short
  fish_store_days = 200,                  -- stores below this many days of food: fish for the store
  fresh_keep_days = 4, dry_share = 0.5, dry_yield = 0.7,  -- fresh fish beyond this is dried (daily share,
                                          --   what survives salting and drying)
  -- construction (timber WU, labour h)
  build = {
    house = { wood = 60, h = 240 }, hut = { wood = 15, h = 60 }, granary = { wood = 50, h = 200 },
    woodshed = { wood = 25, h = 80 }, pier = { wood = 30, h = 150 }, boat = { wood = 25, h = 160 },
    repair = { wood = 12, h = 40 }, palisade = { wood = 220, h = 700 },
  },
  house_decay = 0.06, hut_decay = 0.25,   -- condition per year
  repair_below = 0.5, repair_gain = 0.4,
  max_sites_base = 1, sites_per_pop = 25, builders_per_site = 4,
  ruin_years = { 20, 40 },
  lot_r = 0.016,                          -- radians between lot rings (0.16 world units at R = 10)
  lot_rings = 3,
  -- migration and founding (monthly per household)
  hunger_full = 0.95,                     -- monthly mean ration below this counts as hunger
  hunger_memory = 0.8,                    -- a household's hunger: monthly shortfall summed with this decay
  push_hunger = 2.0, push_hunger_max = 3, -- push per unit of a household's hunger
  push_famine = 0.5,                      -- a famine now
  colony_years = 5,                       -- a village younger than this is a colony (small by design)
  push_wait = 0.4, push_homeless = 0.4, push_cold = 0.1, push_small = 1.0,
  push_disaster = 0.3, pull_margin = 0.3, migrate_prob = 0.15,
  migrate_prob_famine = 0.5,              -- monthly chance a pushed household acts, in a famine
  home_ties = 0.3, home_own = 0.2,        -- pull of home: kin, fields, graves; more with a house of one's own
  settle_months = 24,                     -- newly arrived households stay at least this long
  return_years = 5,                       -- ... and do not go back to the village they left for this long
  walk_hours_day = 10, party_food_days = 14,
  found_food_days = 60, found_seed_cells = 0.6,
  first_harvest_weeks = 20,               -- sowing to the first harvest of a new village (planning)
  bud_food_margin = 60,                   -- colonists carry food to their first harvest + this many days
  bud_min_provision = 0.4,                -- ... or at least this share of it (they forage and hunt the rest)
  bud_radius = 14,                        -- daughter villages within this many cell spacings (days' walk)
  refuge_radius = 20,                     -- refugees without a destination settle within this range
  found_max_households = 5,               -- households in a refugees' founding party (one a year per village)
  site_walk_penalty = 0.03,               -- site score lost per cell spacing of walking
  min_site_people = 20, min_spacing = 3,
  max_villages = 30, soft_cap = 2400,     -- performance guard: births taper toward soft_cap people (D-057: was 12 / 900)
  -- villages that know each other (W7, docs/world/18 §2.2-2.3)
  relief_margin_days = 60,                -- a giver keeps its need to its next harvest + this
  relief_reach = 16, trade_reach = 12, bride_reach = 12,   -- cell spacings (a few days' walk)
  bride_stranger = -0.25,                 -- a village no friend can match sends brides to strangers this well disposed
  -- skills (sim/society/skills.lua, D-063): an hour's work x (lo + (hi - lo) x skill)
  skill_lo = 0.7, skill_hi = 1.1,               -- (a practised hand, skill ~0.8, is ~1.0: the calibration)
  skill_learn = 0.006,                    -- weekly share of the gap to mastery a week's practice closes
  skill_keep = 0.9985,                    -- weekly share of an unpractised skill kept (half in ~9 years)
  -- friendship (sim/society/friends.lua, D-062)
  friend_prob = 0.08,                     -- monthly chance a person with room befriends a companion
  friend_max = 4,                         -- friends a person keeps
  friend_fade = 0.02,                     -- monthly chance a friend living elsewhere is lost
  friend_mood = 0.07, friend_lonely = 0.08,  -- mood per friend at hand (up to three); an adult with none
  friend_pull = 0.25,                     -- a household is drawn to a village where its friends live
  friend_faction = 2,                     -- friends among a leaving lineage that take a household along
  porters = 6, porter_fu = 30,            -- porters per errand, FU (or WU) a porter carries a trip
  bride_prob = 0.35,                      -- monthly chance a village with unmatched women looks abroad
  seed_kin = 2,                           -- kin ties between a landmass's first villages of one culture
  -- ties that fade (session 5): the common stock of first villages, past trades, the daughter tie
  anc_keep = 0.997,                       -- monthly share of a common-stock tie kept (half in ~20 years)
  trade_keep = 0.985,                     -- monthly share of past trades remembered (half in ~4 years)
  daughter_tie_years = 75,                -- a daughter village's tie to its mother fades out over this
  -- exchange (W7b, D-056): what a village keeps before it trades a good away, when it wants one
  wood_keep_days = 120,                   -- days of fuel kept back before wood is sold
  trade_wood_days = 25, trade_food_days = 75,   -- below this many days of a good a village buys it
  import_trust = 0.6, export_trust = 0.8, -- share of usual imports (exports) a field plan counts on
  trail_decay = 0.75,                     -- yearly share of a trail's traffic that is remembered
  trail_full = 150,                       -- walkers a year for a fully worn path
  trail_speed = 0.6,                      -- a worn path is walked this much faster (1 / (1 + x))
}

-- Status and leaders (docs/world/18 §2.1, sim/society/status.lua, D-057)
C.STATUS = {
  decay = 0.97,                           -- monthly share of prestige remembered
  elder_age = 50, elder_gain = 0.05,      -- a household with an elder head gains respect monthly
  founder_gain = 0.04,                    -- households of the village's founding lineage
  gift_prestige = 0.02,                   -- per need-day of relief the leader's household gives
  split_pop = 40,                         -- contested successions can split villages this big
  split_close = 0.75,                     -- the rival's prestige at least this share of the new leader's
  split_share = 0.2,                      -- the rival lineage is at least this share of the village
  split_prob = 0.5, split_gap_years = 15, split_min = 8,
  stress_pop = 160, stress_prob = 0.02,   -- scalar stress: monthly split chance x (pop - 160) / 160
  stress_share = 0.1,                     -- ... by the largest other lineage, if it is this share
  -- a split leaves bad blood (Chagnon; Walker & Hill 2014): grievance both ways when the leavers
  -- found their village — a contested succession, or the quarrels of a crowded village
  split_grievance = { contested = 1.6, stress = 0.9 },
}

-- Conflict (docs/world/18 §2.4, sim/society/conflict.lua, D-057)
C.CONFLICT = {
  fight_age = { 16, 45 },
  other_defence = 0.25,                   -- women, youths and elders count this much in defence
  palisade = 2.0,                         -- a standing palisade multiplies the defence
  home_advantage = 1.3, surprise = 1.25,  -- Lanchester weights: defenders at home, raiders at dawn
  min_pop = 25, min_target = 12, min_raiders = 5,
  raid_share = 0.6,                       -- share of the fighting men who go
  raid_reach = 12,                        -- cell spacings
  raid_gap_months = 6,
  friend_score = 0.7,                     -- villages this well disposed are not raided unless wronged
  loot_share = 0.45, carry = 30,          -- share of the store taken; FU a raider carries
  revenge_weight = 0.35, risk = 0.6, threshold = 0.25, raid_prob = 0.5,
  raid_grievance = 3,                     -- grievance a raid leaves (+ per defender killed)
  grievance_keep = 0.985,                 -- monthly (a raid is remembered for decades)
  burn_chance = 0.35, burn_damage = 0.8,
  win_prestige = 1.5, raider_prestige = 0.4,
  palisade_years = 12,                    -- villages raided within this many years build a palisade
  gift_forgives = 0.02,                   -- grievance washed away per need-day of relief received
  -- allies (a village's lord, and villages that hold it dear within a day's walk) come to its defence
  ally_score = 1.0, ally_reach = 6,       -- relation score and distance (cell spacings)
  ally_share = 0.3, ally_lord_share = 0.5,  -- share of their fighting men who come
  ally_grievance = 1.0,                   -- the allies' grudge against the raiders
  ally_days = 3,                          -- need-days of help each allied man counts for (reciprocity)
  -- fear: households of a village raided within fear_years look for safety (defensive aggregation)
  fear_years = 6, push_raided = 0.6, safety_pull = 0.5,
  -- ambition (D-065): a big village with a much respected leader raids weaker non-kin for tribute
  ambition = 0.45, amb_min_pop = 120, amb_prestige = 4, amb_edge = 1.3,
  -- cattle raids: a small party runs off a neighbour's herd from its pasture
  cattle_party = 10, cattle_share = 0.3,  -- raiders: at most, and the share of the fighting men
  cattle_per_raider = 12,                 -- sheep-equivalents a raider drives home
  cattle_alarm = 0.05,                    -- share of the village's men the alarm brings to the pasture
  cattle_surprise = 2.0,                  -- raiders strike the pasture by night
  cattle_reach = 9, cattle_min_se = 30,   -- cell spacings; herds worth raiding
  cattle_lack = 1.0, cattle_bachelors = 0.3,  -- motives: beasts lacking, young men without wives
  cattle_risk = 0.2,                      -- the cost of a failed raid (value units: they run when outmatched)
  cattle_grievance = 1.5,                 -- the grudge a cattle raid leaves (+ per herder killed)
  news_years = 10,                        -- raids between two villages within this of the last headline one are the feud's, not news
  cattle_threshold = 0.4,                 -- a cattle raid must promise this much (value units)
  -- feuds: revenge taken settles the raider's own grudge; after truce_after raids between two
  -- villages their leaders may make a truce (monthly chance x raids beyond it, less for warlike leaders)
  revenge_satisfies = 1.5,
  truce_after = 3, truce_prob = 0.03, truce_years = 20, truce_prestige = 0.8,
  want_breaks_ties = 0.9,                 -- hunger / land want lets a village raid this much better friends
  encroach_grievance = 0.6,               -- a land-short village's grievance when a neighbour takes a shared field
  -- rivals for shared pasture and fishing grounds (session 5): monthly grievance at full shortfall,
  -- x the share of the land (grounds) shared x the rival's share of the take
  pasture_grievance = 0.4,                -- shortfall: beasts beyond what the grass carries (crowd - 1, up to 1)
  fish_grievance = 0.4,                   -- shortfall: grounds below fish_rival x their recent state
  fish_rival = 0.7,
  -- tribute (W9, D-058)
  defeat_memory_years = 15, submit_defeats = 2,  -- beaten this often by one raider within the memory...
  submit_ratio = 0.7,                     -- ...and hemmed in, or with fewer than this share of its fighters: submits
  secede_ratio = 0.8,                     -- a tributary stops paying when its lord has fewer fighters than this share of its own
  tribute_share = 0.12,                   -- of each harvest
  submit_prestige = 3, protect_grievance = 2,
}

-- Livestock (docs/world/18 §2.6, sim/society/herds.lua, D-058): per head — milk (FU/day in its
-- season), meat (FU), yearly births and deaths, grazing weight (sheep-equivalents)
C.HERDS = {
  sheep = { milk = 0.10, meat = 20, birth = 0.9, death = 0.10, graze = 1 },
  goats = { milk = 0.18, meat = 18, birth = 1.0, death = 0.12, graze = 0.9 },
  cattle = { milk = 0.9, meat = 160, birth = 0.55, death = 0.07, graze = 5 },
  pasture = 500,                          -- sheep-equivalents one fully grassed cell carries (its regrowth
                                          --   feeds ~1,000: a cell is ~170 ha of grass)
  forest_browse = 0.25,                   -- woodland pasture counts this much of its forest as grass
  graze_week = 0.000012,                  -- grass units a sheep-equivalent eats a week (~550 kg a year)
  per_person = { 0.8, 2.5 },              -- herd kept per person: + this x the grassiness of the land
  herder_se = 80,                         -- sheep-equivalents one herder minds
  birth_weeks = { 4, 12 }, milk_weeks = { 6, 30 },   -- local weeks (spring lambing, summer milk)
  hunger_loss = 0.02,                     -- weekly share lost per unit of overstocking
  wolf_loss = 0.004,                      -- weekly share taken on an unguarded pasture among wolves
  hungry_days = 25, hungry_cull = 0.04,   -- a village with fewer food days eats this share a week
  breeding_share = 0.35,                  -- the herd kept whatever happens
  dry_meat = 0.6,                         -- dried meat into the store (half of a slaughter)
  manure = 0.0000015, manure_max = 0.0004,  -- weekly fertility per se per field (capped)
  sheep_per_person = 1.2,                 -- sheep for a village's wool to clothe everyone
  raid_take = 0.2,                        -- share of the herd raiders drive off
  -- household herds (D-061): bride-wealth from the groom's family to the bride's, the son's portion
  bride_price = 0.4,                      -- the going rate: this share of the village's mean family herd
  bride_share = 0.5,                      -- at most this share of the family's beasts
  bride_wait_years = 3,                   -- a couple waits this long for the price, then marries anyway
  heir_share = 0.2,                       -- a son marrying takes this share of his family's beasts
  wealth_prestige = 0.03,                 -- monthly standing per unit of beasts above the village mean share
}

-- Crafts (docs/world/18 §2.6, sim/society/crafts.lua, D-056): one unit = a person's (tools: a
-- worker's) use for a year; rate = units per effective hour at a source of quality 1.
C.CRAFT = {
  tools = { need = 1.0, rate = 0.012, value = 25, weight = 1.5, loss = 0.25 },   -- loss: work lost without tools
  pots = { need = 0.6, rate = 0.03, value = 10, weight = 2, fuel = 0.8, spoil = 1.2 },  -- spoil: extra grain spoilage
  cloth = { need = 0.5, rate = 0.008, value = 38, weight = 0.5, exposure = 0.18 },  -- health per week per °C of cold
  min_q = 0.12,                           -- the poorest source anyone can use (slow work)
  clay_flow = 60,                         -- river flow for the best clay banks
  cover_years = 0.5,                      -- a good is fully covered with this many years in hand
  target_years = 1.0,                     -- craft work aims at this many years of need in store
  keep_years = 0.6,                       -- a village sells only beyond this many years of need
  fu_hours = 3.3,                         -- hours of work a day's food is worth (make-or-buy)
  buy_margin = 1.4,                       -- buy when making costs this much more than buying
  buy_floor = 0.3,                        -- a buyer still keeps this many years' need made at home
}

-- Player powers (04_PLAYER.md §2, sim/powers.lua) ------------------------------------------------
C.POWERS = {
  radius = 2, introduce_radius = 1,       -- level-4 steps
  season_ticks = 12,                      -- Weather / Temperature last one season
  deluge_mult = 2.5, deluge_add = 0.04,   -- forced rain: precip x mult + add (moisture units per tick)
  drought_mult = 0.05, drought_heat = 3,  -- drought: rain blocked, +3 °C heat anomaly (drier soil)
  temp_anom = 6,                          -- Heat / Chill: ± °C heat anomaly held for the season
  sea_factor = 2,                         -- ... times this over open water (the sea holds it)
  cull_fraction = 0.9,
  introduce = { grazers = 0.35, predators = 0.12, fish = 0.35 },  -- density added per cell
  settlers = 5,
  lightning_heat = 4,                     -- °C of heat anomaly where lightning strikes land
  boat_oil = 3,                           -- oil from a sunk fishing boat (level-4-equivalent cells)
  oil = 36,                               -- Oil Spill pulse on open water, a tanker cargo (a tanker there is sunk instead)
  blight_dieback = 0.6,                   -- share of an infected farm's standing crop lost per month
  blight_spread = 0.3,                    -- monthly chance per neighbouring farm cell
  blight_trade = 0.08, blight_trade_steps = 12, -- monthly chance to reach another village within 12 level-4 steps (villages are sparse)
  blight_winters = 2,                     -- burns out after two winters
  meteor_radius = 2, meteor_heat = 12, meteor_kill = 0.8,
  meteor_deaths_centre = 0.6, meteor_deaths_ring = 0.15,
  meteor_aerosol = 1.2,                   -- impact winter (aerosol units; k_aer cools the whole planet)
}

return C
