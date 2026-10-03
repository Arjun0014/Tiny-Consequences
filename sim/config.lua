-- Every tunable constant (02_WORLD_MODEL.md: "never hard-code a tunable inside a system").
-- Grouped by system; units and intended effect in comments. Tuning is expected. Changing a
-- tuning *band* or test *threshold* needs a DECISIONS.md entry; changing these doesn't.

local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
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
  -- (S21, R6.5e) infiltration excess where a step is a day (v3): the ground takes in at most
  -- inf_cap x (1 - soil water) x (inf_bare + (1 - inf_bare) x cover) of a day's rain (cover: woods,
  -- grass at 0.6); on frozen ground x inf_frozen; the rest runs off that day
  inf_cap = 0.07, inf_bare = 0.35, inf_frozen = 0.2,
}

-- Global climate (§5.13) -----------------------------------------------------------------------------------
C.CLIMATE = {
  aerosol_decay = dmath.exp(-1 / 72),      -- per tick (e-folding 1.5 years)
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
  fawns = 1.0, fawn_cond = { 0.2, 1.0 },  -- fawns per head a year, by the lowest condition of the winter (ramp: Verme 1977)
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
  browse_cap = 0.5,                       -- ... up to this share of their need (ruminants starve on woody browse alone); it nourishes them
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
  store_years = 1.5,                      -- the store a village is content with, in years of need (+ dread_store x dread: a
                                          --   village that remembers hunger wants more, D-074): granaries are built while the
                                          --   store overflows until they hold it, and beyond it nobody sows more (Chayanov)
  gift_min_days = 10,                     -- a surplus gift is worth carrying at this many of the receiver's need-days (D-074)
  feast_min_days = 4,                     -- grain beyond the granaries worth a harvest feast: this many of the village's need-days
  guest_share = 0.3,                      -- share of a feast eaten by guests from friendly villages of the landmass
  owe_w = 0.004, owe_cap = 250,           -- a village is disposed to help one it owes: per need-day (its own) taken and not returned
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
  feast_prestige = 0.01,                  -- per need-day of the village's food eaten at a feast it hosts (D-074)
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
  -- (S21, R7 v1 on the v3 world, sim/powers3.lua) rain and the dry spell last v3_rain_days / v3_dry_days
  -- planet days; warm or cold air +-v3_air_k C for v3_air_days; stirring a mountain adds v3_stir_k of its
  -- threshold to its pressure, straining a fault v3_strain_k of its threshold to its stress
  v3_rain_days = 3, v3_dry_days = 6, v3_air_k = 8, v3_air_days = 4, v3_stir_k = 0.5, v3_strain_k = 0.9,
}

-- v3: the agentic world (docs/v3/, D-078). One planet step = one game day (W.hpt = 24); a year is
-- 48 days. Distances in world units on the local map (planet radius 10, a sim cell ~1.0 across);
-- times in hours.
C.V3 = {
  hpt = 24, days_per_year = 48,
  map_n = 128, tile = 0.025,              -- local map: 128 x 128 tiles of 0.025 units (3.2 units square)
  -- movement (world units per hour; a cell spacing in ~19 min)
  walk = 3.2, pace = { 0.85, 1.15 },
  speed_child = 0.8, speed_elder = 0.72, speed_load = 0.85, speed_rain = 1.12, speed_dark = 0.85,
  -- tile costs (A*): open land, under trees, across a plot, a trail, a lane
  cost_open = 1.0, cost_tree = 1.5, cost_plot = 1.6, cost_trail = 0.75, cost_lane = 0.55,
  cost_ford = 3.0, cost_swim = 12.0,   -- (S20: swimming where it is too deep to ford - only when cut off)                        -- wading a ford or a stream (D-085): slow, wet and cold
  boat_margin = 0.015,                    -- boats keep to water this far into the coast field (D-085)
  temper_extra = 0.12,                    -- extra cost per temperament step off lanes (route variety)
  wear_walk = 1.0,                        -- wear added per tile crossed
  wear_decay = 0.04,                      -- share of wear lost per day
  wear_trail = 6, wear_lane = 30,         -- wear thresholds of a trail and a lane
  -- body (per hour)
  hunger_work = 0.065, hunger_idle = 0.045, hunger_sleep = 0.018, meal = 0.55, meal_fu = 0.34,
  energy_work = 0.05, energy_walk = 0.04, energy_idle = 0.02, energy_sleep = 0.12,
  hunger_eat = 0.5,                       -- eat when hungrier than this (sooner at home)
  starving = 0.92, health_starve = 0.015, -- health lost per hour above `starving` (S19, D-147: was 0.04 - dead two days after the last meal)
  -- day rhythm (local solar hours)
  wake_after_sunrise = -0.4, chrono = { -1.3, 1.3 }, bed_after_sunset = 3.0, bed_min = 19.5, bed_max = 23.6,
  wake_min = 4.3, wake_max = 8.6,
  -- farming: plots of 8 x 8 tiles worked row by row
  -- (D-080) rows are worked in stints with their progress kept: a row of hoeing is hours of work
  plot_tiles = 12, till_row_h = 7.0, sow_row_h = 1.2, tend_h = 1.0, harvest_row_h = 5.0,
  stint_h = { 1.0, 2.2 },                 -- a stint of field work before they look up (x diligence)
  thresh_h = 1.0, thresh_fu = 6,          -- an hour of threshing at the granary turns 6 FU of sheaves to grain
  seed_row = 0.5,                         -- FU of seed grain per row
  grow_days = 20,                         -- days from sowing to ripe at good temperatures
  grow_tmin = 3, grow_topt = 13,          -- degC: crops do not grow below tmin, grow fully from topt (D-087)
  heat_t = 24, heat_span = 10,            -- (S21, R6.5g) a day's mean above heat_t spoils grain filling (none at +span) and browns grass
  row_yield = 13.5,                       -- FU per row at full quality
  weeds_day = 0.15, tend_clear = 0.09, weeds_tend = 0.15,   -- weeds a day; cleared per hour; weeding starts above
  ripe_spoil_days = 8,                    -- ripe grain left standing is lost after this
  -- the three-field rotation (D-080): a winter crop (sown in autumn, it overwinters slowly), a
  -- spring crop, a fallow year (ploughed in summer against the weeds; the soil recovers)
  sow_season = { 0, 13 },                 -- local day-of-year window for sowing the spring crop
  sow_winter = { 26, 35 },                -- ... and the winter crop
  plough_fallow = { 12, 25 },             -- the fallow is ploughed in summer
  grow_days_winter = 28, winter_dormant = 0.35,   -- growth days of the winter crop; its rate in winter
  fert_crop = 0.12, fert_rest = 0.006, fert_plough = 0.05, fert_max = 1.2,   -- soil: a crop takes, rest and ploughing give
  -- wood: a log is the unit
  fell_h = 1.3,                           -- hours to fell a full-grown tree (/ skill)
  logs_per_tree = { 2, 5 },
  tree_grow = 0.012,                      -- size gained per day (0..1)
  sapling_days = 6,                       -- a stump sprouts after this many days (sometimes)
  house_logs = 18, house_work = 110,      -- logs and work-hours for a house (D-080)
  carry_grain = 10, carry_logs = 1,       -- load per trip (FU, logs; strong adults carry 2 logs)
  pantry_low = 2.0,                       -- days of food at home before someone fetches
  pantry_fetch = 8,                       -- FU fetched from the granary
  woodpile_days = 3,                      -- days of the hearth's burning a household keeps stacked at home
  woodpile_fetch = 2,                     -- logs fetched from the woodstore a trip (a child 1)
  store_logs = 20,                        -- logs the woodstore keeps in reserve
  store_years = 0.5,                      -- food beyond the next harvest the village wants (years)
  grain_use_k = 0.04,                     -- (D-100) daily weight of the measured grain use (the households' fetching)
  max_plots = 20,
  idle_expand = 0.4,                      -- adults idle more than this share of the day: more fields are cleared
  grub_h = 0.45,                          -- hours to dig out a stump on new field ground
  -- wild food at the woods' edge (D-080): berries, nuts, greens and roots regrow on the standing
  -- trees of the edge band (FU per tree per day by local season) and are picked out by foragers
  wild_regrow = { 0.0045, 0.010, 0.014, 0.0012 },
  wild_cap_days = 8,                      -- the edge holds at most this many days of regrowth
  forage_h = 2.4, forage_basket = 3.0,    -- an outing: a basket of up to 3 FU when the edge is full
  -- (S19) the shore: shellfish and seaweed regrow shore_regrow FU a shore tile a day (at most
  -- shore_cap_days of it); an outing at low water fills a basket of up to shore_basket FU; worth it
  -- only when food is short (the village under shore_short days, or the household's larder low)
  shore_regrow = 0.02, shore_cap_days = 10, shore_basket = 2.5, shore_h = 2.0, shore_short = 12,
  -- hearths (D-080): cooking burns wood every day; a chilly day burns more
  fuel_cook = 0.55, fuel_warm = 16, fuel_heat = 1.2,  -- logs a day; + fuel_heat per 10 degC below fuel_warm
  -- cooking (D-080): grain from the pantry becomes the household's meals at the hearth
  cook_h = 2.4, cook_batch = 1.0, meals_days = 1.0,   -- hours a batch (grinding, baking); FU per member; days kept
  -- (D-101) the pot: the relish share (fish, meat, milk, cheese) the cook aims for; the household's
  -- diet as a running share (weight a full batch); a diet under lo or over hi weakens (health held
  -- to diet_weak) and adds diet_hz a day to the risk of death
  diet_p = 0.35, diet_k = 0.08, diet_lo = 0.12, diet_hi = 0.8, diet_weak = 0.55, diet_hz = 0.0004,
  spoil_meals = 0.05,                     -- share of cooked food gone stale a day
  -- stores (D-080): grain keeps, not forever; threshing as the threshed grain runs down
  spoil_grain = 0.0012, spoil_sheaves = 0.0005, spoil_food = 0.004,
  spoil_fish = 0.3,                       -- share of fresh fish at home spoiled a day (D-087)
  thresh_days = 30,                       -- days of threshed grain the village likes to have ready
  -- upkeep (D-080): roofs and walls weather; a worn house is repaired with a log and some hours
  wear_house = 0.005, repair_at = 0.75, repair_h = 2.5, repair_logs = 1,
  -- tools (D-080): hoes, sickles, axes and baskets wear with the work and slow it when worn; a
  -- household mends and makes them (by the door in fair weather), using a little wood
  tool_wear = 0.0035, tool_mend = 0.045, tool_logs = 0.05,   -- per work hour; per craft hour; logs a craft hour
  winter_wood_days = 40,                  -- in autumn and winter the woodstore lays in this many days of burning
  -- people
  adult_age = 16, elder_age = 55, child_age = 2, apprentice_age = 12, help_age = 8,
  gather_age = 6, child_gather = 0.6,     -- (D-109) children gather at the woods' edge from six, filling a basket more slowly
  marry_f = 17, marry_m = 19, fecund = { 16, 44 },
  conceive_year = 0.42,                   -- annual chance for a fed married woman in her own home
  pregnancy_days = 36,
  mortality = { infant = 0.08, child = 0.01, adult = 0.006, old_a = 0.006, old_b = 0.0012, old_k = 8 },
  -- infants and carers (D-081): an infant's hunger an hour awake / asleep; it calls for food above
  -- infant_feed; a nursing lasts nurse_h; weaned, a bowl of porridge from the home's meals
  infant_hunger = 0.17, infant_hunger_night = 0.05, infant_feed = 0.5, infant_starve = 0.03,
  nurse_h = 0.35, porridge = 0.15,
  child_chores = 3,                       -- a child's chores a day before play wins every time
  elder_tire = 1.4,                       -- elders tire this much faster at work
  -- fishing (D-084): boats rowed from the dock to grounds offshore; the catch comes out of the
  -- planet's fish (the v2 scale: FU per crew-hour at density 1; FU of fish in a cell at density 1)
  fish_q = 2.2, fish_fu = 18000, fish_h = { 1.6, 3.2 }, row_speed = 1.6, boats = 2,
  -- fishing as an occupation (D-087): boats belong to fisher households, wear afloat (and rot a
  -- little ashore), are mended with a log at the dock and built new at the shore; fresh fish
  -- spoils in days, so the surplus is smoked at the hearth for the winter
  boat_wear = 0.012, boat_rot = 0.002,    -- condition lost an hour afloat; a day ashore
  boat_mend = 0.55, boat_unsafe = 0.2,    -- owners mend a boat below this; nobody rows out below this
  mend_boat_h = 2.0,                      -- hours of mending a boat (and a log)
  boat_logs = 5, boat_work = 30,          -- logs and work-hours for a new boat
  smoke_fu_h = 3.0, smoke_logs = 0.08,    -- FU of fresh fish smoked an hour at the hearth; logs a FU
  spoil_smoked = 0.004,                   -- share of smoked fish spoiled a day
  smoke_days = 8,                         -- days of smoked fish a fishing household lays in for the winter
  -- (D-096) the net with two in the boat, the lines alone (catch factors); how long a skipper waits
  -- at the dock for a mate; a squall at sea: wear, the chance it swamps the boat; drying racks:
  -- fish to spare (FU, a household's running sum) before one is built, fish worth a trip to the
  -- rack, dried fish worth fetching, the share drying a fair day, spoiled a wet day
  net_k = 1.5, line_k = 0.7, mate_wait = 0.5, squall_wear = 0.15, squall_swamp = 0.08,
  rack_after = 6, dry_min = 1.5, dry_fetch = 2, dry_rate = 0.35, dry_rain_spoil = 0.15,
  -- textiles (D-088): flax sown in spring, pulled, retted on the field, dressed, spun, woven and
  -- sewn into clothes that wear out. Units: garments' worth (GE). Hours a GE at full skill.
  grow_days_flax = 14,                    -- flax grows fast
  flax_row = 1.1,                         -- GE of flax a row pulls
  ret_days = 8,                           -- days the pulled flax lies retting on the field
  dress_h = 3.0, spin_h = 60.0, weave_h = 14.0, sew_h = 3.0,  -- break/scutch/hackle, spin (a drop spindle), weave, sew
  mend_clothes_h = 1.0, mend_clothes_yarn = 0.05,
  textile_want = 1.5,                     -- GE a person the village wants in the pipeline (flax to cloth)
  cloth_keep = 1.2,                       -- GE of fibre, yarn and cloth a member a household keeps in hand
  clothes_wear = { child = 0.012, apprentice = 0.008, adult = 0.007, elder = 0.004 },   -- a day
  carry_flax = 3,                         -- GE of flax bundles a load
  -- livestock (D-089 sheep, D-092 herds): individual animals of households in the village's herds.
  -- Per species: walking speed (units an hour), a hop toward its place, spacing in the herd's spread;
  -- hunger an hour, eaten an hour on full grass (rooting for pigs, browse for goats where there are
  -- trees, mast in autumn), sheep-equivalents of grass and hay; FU of meat; milk (FU a day in
  -- milk, days in milk after a birth, hours to milk); wool (GE a day); ages (young, grown, old,
  -- years); births (local days, a second season, daily chance in season, twins, litter);
  -- behaviour (share of decisions a few steps on, lying down; hours a bout of grazing, of rest);
  -- hours to butcher.
  stock = {
    sheep = { speed = 1.6, hop = 0.08, sep = 0.034, hunger = 0.035, graze = 0.25, se = 1, meat = 6, hide = 1, wool = 0.03,
      young_years = 0.35, adult_years = 1, old_years = 7, birth = { 1, 10 }, birth_chance = 0.12, twins = 0.25, litter = 1,
      stepping = 0.22, resting = 0.1, graze_h = 0.9, rest_h = 1.1, butcher_h = 0.75 },
    goat = { speed = 1.8, hop = 0.09, sep = 0.034, hunger = 0.035, graze = 0.25, browse = 0.45, se = 0.8, meat = 5, hide = 1,
      milk = 0.22, lact_days = 22, milk_h = 0.15,
      young_years = 0.3, adult_years = 1, old_years = 8, birth = { 0, 10 }, birth_chance = 0.12, twins = 0.4, litter = 1,
      stepping = 0.3, resting = 0.08, graze_h = 0.75, rest_h = 1.0, butcher_h = 0.7 },
    cattle = { speed = 1.3, hop = 0.1, sep = 0.062, hunger = 0.03, graze = 0.22, se = 4.5, meat = 45, hide = 2,
      milk = 0.75, lact_days = 26, milk_h = 0.3,
      young_years = 0.5, adult_years = 2, old_years = 12, birth = { 2, 14 }, birth_chance = 0.08, twins = 0.02, litter = 1,
      stepping = 0.18, resting = 0.2, graze_h = 1.0, rest_h = 1.5, butcher_h = 1.6 },
    pig = { speed = 1.4, hop = 0.08, sep = 0.032, hunger = 0.04, root = 0.12, mast = 0.28, se = 0, meat = 20, hide = 0.5,
      young_years = 0.25, adult_years = 1, old_years = 6, birth = { 4, 14 }, birth2 = { 22, 30 }, birth_chance = 0.15,
      twins = 0, litter = 6, stepping = 0.25, resting = 0.15, graze_h = 0.95, rest_h = 1.4, butcher_h = 1.0 },
  },
  -- (S16) livestock spacing: a herd of n spreads stock_range[1] x sqrt(n) + stock_range[2] spacings
  -- about where it is kept; a beast closer than stock_room spacings to another makes room, within
  -- stock_bend its grazing way bends off (sheep grazing calmly keep 2-4 body lengths apart)
  stock_range = { 1.3, 1.5 }, stock_room = 0.7, stock_bend = 1.6,
  drove_pace = 1.25,                      -- (S15) a herder driving a herd walks at its slowest beast's speed x this
  drove_road = 1.6,                       -- (S15, R4) a road leg with beasts led takes this x a walker's hours
  drove_gather = 0.15,                    -- (S16) a drover waits at the gate until the drove is within this of them
  beast_room_doy = 24,                    -- (S15, R4) from this local day (the hay in) a village knows how many beasts its stack will winter
  pen_size = { flock = { 6, 6 }, cattle = { 7, 6 }, swine = { 5, 4 } },   -- tiles of the fold, byre yard, sty
  rack_found = { flock = 4, cattle = 10, swine = 3 },                      -- feed in the racks at founding
  cattle_share = 0.45, pig_share = 0.7,   -- founding: share of households with a cow, with a sow (by grass, woods)
  pig_reach = 1.0, pig_roam = 0.4, mast_days = { 24, 36 },   -- (pig_roam: how far from the sty pigs root unherded)   -- the pigs' woods: within this of the sty; acorns and beech mast (local days)
  pig_slaughter = { 36, 40 }, pig_keep = 3,   -- (S20: growers a household fattens at a time)             -- local days the fattened pigs are killed (the start of winter)
  -- (S19) hunger: a household with food for under hunger_cull_days while its village's stores hold
  -- under hunger_cull_village days kills one of its beasts for the pot
  hunger_cull_days = 1.5, hunger_cull_village = 4,
  manure_hour = 0.012,                    -- loads of dung a sheep-equivalent leaves in its pen an hour
  manure_load = 1.0, manure_fert = 0.03,  -- a load carried; the fertility a load gives a field
  milk_min = 0.35,                        -- milking when the udder holds this share of a day's milk
  cheese_min = 0.6, cheese_h = 1.5, cheese_keep = 0.85,   -- milk at home worth a cheese; hours; share kept (the whey to the pigs)
  spoil_milk = 0.5, spoil_cheese = 0.003, -- share spoiled a day
  scrap_share = 0.7,                      -- of what spoils at home, the share that is pig food
  slop_min = 0.4, slop_feed = 3.0,        -- scraps worth a trip to the sty; a pig's hunger units an FU of scraps feeds
  sick_base = 0.004, sick_spread = 0.25, sick_recover = 0.18, sick_days = 6, sick_harm = 0.004,   -- livestock sickness (a day; health an hour)
  -- wild animals on the map (D-093): per species walking and running speed (units an hour),
  -- spacing in a band, FU of meat, hides
  wild = {
    deer = { speed = 1.8, run = 5.0, sep = 0.05, meat = 14, hide = 1 },
    reindeer = { speed = 1.8, run = 4.6, sep = 0.05, meat = 18, hide = 1 },
    antelope = { speed = 2.0, run = 5.6, sep = 0.05, meat = 12, hide = 1 },
    boar = { speed = 1.5, run = 4.2, sep = 0.04, meat = 16, hide = 1 },
    wolf = { speed = 2.4, run = 6.0, sep = 0.035, meat = 0, hide = 1 },
    cat = { speed = 2.2, run = 6.5, sep = 0.04, meat = 0, hide = 1 },
  },
  wild_seen = 1.0,                        -- share of a herd's animals on the map's cells drawn here
  wild_max = 24,                          -- at most this many wild grazers on one map
  wild_keep = 0.9,                        -- bands keep this far from the village square
  wild_alert = 0.2, wild_alert_wolf = 0.3,   -- grazers take flight from people / hunting wolves this close
  wild_flee = 0.35,                       -- how far a frightened grazer runs
  wild_alarm_seps = 6,                    -- (S15) a grazer taking flight frightens its band within this many spacings
  wild_shy = 0.3,                         -- (S15) a frightened band's grazing ground moves this far from the danger for the day
  wolf_hungry = 0.55,                     -- a pack below this condition comes for a village's beasts when
  wolf_wild_scarce = 8,                   --   fewer wild grazers than this are on the map; at once below
  wolf_starving = 0.25,                   --   this condition
  guard_days = 6,
  -- hunting (D-094): meat a household member wants laid by (FU); how far a hunter goes; stalking
  -- distance, a spear's throw; creeping pace (units an hour); hit odds (+ skill); tries; hours
  -- rivers in use (D-095): the river's water when the bank is this much nearer than the well
  -- (the well's water is cleaner); river fish a wet tile holds (FU), their regrowth a day; angling
  -- (FU an hour on a full river, reach, the stock below which nobody bothers); crossings wanted (a
  -- trip: 1 + 3 x its detour share; kept x cross_keep a day) past which a bridge is laid out; its
  -- logs and work-hours a tile; the longest; how many a village builds
  river_water_k = 1.8, rfish_tile = 0.6, rfish_regrow = 0.12, angle_rate = 0.5, angle_reach = 1.0, angle_min = 3,
  cross_keep = 0.85, bridge_need = 40, bridge_logs = 2, bridge_work = 10, bridge_max = 9, bridges_max = 2,
  -- trade (D-097): the road's pace (local units of the planet's surface an hour), how far a porter
  -- walks to trade; a load (FU); smoked fish a household keeps a member before it sells; grain a FU
  -- of fish fetches (more where fish is scarce); a buyer: days of grain a person above which it
  -- spares grain, smoked fish a person below which it wants fish; days of food at home below which
  -- the household wants grain back
  road_speed = 0.45, trade_reach = 11, trade_load = 8, trade_keep = 3, trade_rate = 1.4,
  trade_food_spare = 25, trade_fish_short = 2, trade_home_food = 4, trade_prov = 2.0,
  -- (D-098) wool: GE a household keeps a member, a load, grain a GE fetches at full want; a buyer
  -- wants wool while its cloth pipeline is under this share of textile_want a person
  trade_wool_keep = 1.0, trade_wool_load = 3, trade_wool_rate = 3.0, trade_wool_short = 0.8,
  -- (D-101) the economy: a village wants fish_want FU of smoked fish and cheese_want of cheese a
  -- person in store, a household textile_hh_want GE a member (wool to cloth); what is beyond spare_k x
  -- the want can go; a trip wants at least trade_min_load of a load and the good worth trade_margin x
  -- more there than here; an hour on the road costs trade_hour FU; news fades over know_fade days;
  -- a deal kept adds trust_deal of the way to full trust; an exchange takes deal_h hours
  fish_want = 3.0, cheese_want = 0.8, textile_hh_want = 1.0, spare_k = 1.2, trade_min_load = 0.25,
  -- (S17) a household's goods (textiles, leather) are a stock it keeps to a target, and what is beyond
  -- house_spare_k x the target can go; a village working for exchange (its news says a neighbour
  -- wants cloth at trade_margin x export_margin what it is worth at home, news fresher than
  -- export_fresh) keeps export_cloth GE a member more in its pipeline
  house_spare_k = 1.0, export_margin = 1.1, export_fresh = 0.25, export_cloth = 0.6,
  -- (S17) a kin visitor brings a little of what the household has beyond its want that the kin's
  -- village is heard to want visit_gift_k x more than it is worth at home; the kin give back the same way
  visit_gift_k = 1.3,
  -- (S17) an hour on the road costs trade_hour x (1 - trade_idle_k x the village's idle share of the
  -- day), at least trade_idle_min of it
  trade_idle_k = 1.0, trade_idle_min = 0.4,
  -- (S17) going to see a neighbour when nothing has been heard from it for news_days and nobody went
  -- for news_again days, the village's hands idle at least news_idle of the day; news_h at its square
  news_days = 8, news_again = 10, news_idle = 0.25, news_h = 2.0,
  trade_margin = 1.25, trade_hour = 0.06, know_fade = 48, trust_deal = 0.06, deal_h = 0.3,
  trade_guess = 0.4,                      -- (S14) a gain known only by guesswork (faded news) counts this much
  trade_fade = 16,                        -- (S19) days a trader trusts what was seen at a village of its goods and wants
  share_keep = 2.0, share_short = 0.5,
  -- (D-102) sea trade: reach (local units of sea way), a boat under sail and oars, its hold in loads
  sea_reach = 40, sail_speed = 1.1, boat_hold = 3,
  sea_squall = 0.00008, sea_wet_k = 3,    -- chance a voyage hour of foundering (fair day; x on a wet day; worn boats and unskilled skippers more; S19: was 0.00025 - ~1% a wet 14-hour crossing)
  sea_life_fu = 150,                      -- (S19) what a trader weighs a life lost at sea at, in FU, against a voyage's gain
  nap_h = 4,
  relief_days = 8,
  -- (S19) asked by people with under relief_starving_days of bread, a village gives relief_share of
  -- what it has beyond relief_keep_days of its own bread (times its will), even with no surplus
  relief_starving_days = 2, relief_keep_days = 20, relief_share = 0.2,
  -- (S16, R5) households moving away: hardship (village food under mig_hunger_days days, hungry
  -- members, no house of its own: mig_homeless) of at least mig_min_push for mig_days mornings running;
  -- they go where the pull (grain heard spare for mig_food_days, kin, trust) beats what holds them
  -- (mig_hold, their house, their years) and the road (mig_road_hour an hour) by mig_threshold;
  -- they pack for mig_pack_h hours; each grown-up carries up to mig_carry FU of the household's food
  mig_hunger_days = 6, mig_homeless = 0.5, mig_food_days = 30, mig_min_push = 0.35, mig_days = 3,
  mig_desperate = 0.95,  -- (S19) push at which a household (its stores feed it no day more) leaves after a day
  mig_hold = 0.45, mig_road_hour = 0.012, mig_threshold = 0.35, mig_pack_h = 10, mig_carry = 10,
  mig_hoof_k = 0.5,                       -- (S19) share of the herds' meat counted as food before leaving
  -- (S18, R5) moving by sea in the household's own boat (migrate.sea_plan): boat_places places in a
  -- boat (a grown-up 1, a child 0.5, a babe in arms 0, a small beast 1, a load of goods 0.5), at most
  -- mig_sea_beasts beasts; a skipper is a fisher or a grown-up with sk_fish >= skipper_skill; an
  -- hour at sea weighs mig_sea_hour; the sea is dreaded (mig_sea_dread), more in winter
  -- (mig_sea_winter) and by the chance of foundering x mig_sea_risk_k; they sail from mig_sea_dawn
  -- local; the skipper waits at most sea_board_wait hours at the dock for everyone and puts the
  -- sailing off to the next morning (a storm or gale along the way, someone missing)
  -- at most mig_sea_tries times
  road_forage = 0.025,                    -- (S18) FU an hour a hungry traveller with nothing left picks along the road (summer)
  boat_places = 7, mig_sea_beasts = 4, skipper_skill = 0.4, mig_sea_hour = 0.012, mig_sea_dread = 0.05,
  -- (S19) a ferry: a household with no boat asks one of its village with a boat - kin, a friend (a
  -- grown-up of each fond of the other: ferry_fond), or else for a fare (ferry_fare FU of bread,
  -- weighed at ferry_fare_k a FU); a passage given is remembered as a gift of ferry_worth FU
  ferry_fond = 0.3, ferry_fare = 6, ferry_fare_k = 0.02, ferry_worth = 8, ferry_any = 0.2,
  mig_sea_winter = 0.25, mig_sea_risk_k = 20, mig_sea_dawn = 7, sea_board_wait = 3, mig_sea_tries = 4,
  -- (S18) salt (econ.GOODS.salt, a measure about a kilogram): made only at salt pans on a village's
  -- shore (saltpan, built by work) by boiling seawater over a fire, salt_logs logs a measure - in warm
  -- dry weather the sun does salt_sun of the fire's part (at salt_sun_temp °C and more); salt_h
  -- measures an hour of work. A household wants salt_hh_want measures and salt_member more a member
  -- (export_salt more a member when a neighbour is heard to want it). Salt cures meat and fish without
  -- smoke (salt_cure measures a FU, salt_cure_h FU an hour) and is worked into cheese (cheese_salt a
  -- FU of cheese); unsalted, only cheese_keep_nosalt of the curd keeps
  salt_h = 0.8, salt_logs = 0.4, salt_sun = 0.75, salt_sun_temp = 15, salt_hh_want = 0.6, salt_member = 0.3,
  export_salt = 0.6, salt_cure = 0.06, salt_cure_h = 6.0, cheese_salt = 0.025, cheese_keep_nosalt = 0.6,
  -- (S19) stone (sim/agents/stone.lua): an outcrop may stand on a map tile whose planet cell is rocky -
  -- (slope - rock_slope) / rock_slope_span x 0.7 + (height - rock_elev) / rock_elev_span x 0.5, each
  -- 0..1 - tried on a lattice every rock_step tiles with that x rock_density, not within rock_keep of
  -- the map's middle; hard rock at rock_hard_elev km or rock_hard_slope and more; an outcrop holds
  -- rock_stones stones. A stone is quarried in quarry_h hours; houses and granaries raised where stone
  -- is at hand stand on footing_stones stones and weather footing_wear as fast. A quern: two hard
  -- stones shaped in quern_h hours (in stints at the yard); it saves quern_save of a cooking batch's
  -- time (the grinding), wears quern_wear a batch, broken below quern_broken. The village quarries
  -- for quern_at_once querns at a time and keeps stone_reserve stones in the yard.
  rock_slope = 0.6, rock_slope_span = 1.4, rock_elev = 1.6, rock_elev_span = 1.4, rock_hard_elev = 2.0, rock_hard_slope = 1.5,
  rock_step = 7, rock_density = 0.04, rock_keep = 0.45, rock_stones = { 30, 70 },
  quarry_h = 1.5, footing_stones = 4, footing_wear = 0.45, quern_h = 10, quern_save = 0.4, quern_wear = 0.0015,
  quern_broken = 0.15, quern_at_once = 1, stone_reserve = 2, footing_wait = 3,
  -- (S19) iron (sim/agents/iron.lua): a hard outcrop carries a vein with chance ore_vein_p (ore_vein
  -- measures); bog iron on maps of wet (soil water bog_wet) low (bog_elev km) ground beside the
  -- floodplain: such a map has one with chance bog_map_p (ore_bog measures). Ore dug in
  -- mine_h hours (carried carry_ore a load); a clamp holds clamp_logs logs, fired in fire_clamp_h and
  -- left clamp_h hours, opened in open_clamp_h: charcoal_log sacks a log (carried carry_charcoal a
  -- load); a bloom smelted from smelt_ore ore and smelt_charcoal charcoal in smelt_h; a household's
  -- iron edges forged from a bloom and forge_charcoal in forge_h. Iron edges (hh.iron) make tool work
  -- iron_speed faster and the other tools wear iron_save less; they wear iron_wear a work hour, worn
  -- out below iron_broken, new ones wanted below iron_redo; iron_at_once set made at a time.
  ore_vein_p = 0.3, ore_vein = { 40, 110 }, bog_map_p = 0.85, ore_near = 0.9, bog_wet = 0.38, bog_elev = 1.2,
  ore_bog = { 30, 80 }, mine_h = 2.0, carry_ore = 2, clamp_logs = 8, fire_clamp_h = 2.0, clamp_h = 30, open_clamp_h = 1.5,
  charcoal_log = 1.0, carry_charcoal = 3, smelt_ore = 3, smelt_charcoal = 4, smelt_h = 5, forge_charcoal = 1, forge_h = 4,
  iron_speed = 0.25, iron_save = 0.5, iron_wear = 0.0004, iron_broken = 0.1, iron_redo = 0.3, iron_at_once = 1,
  export_stock = 2,                       -- (S19) querns or iron sets made to trade while fewer than this lie to spare in the village
  -- (S19) a whole good (a quern, a set of iron edges) may be paid for with a basket of what the buyer's
  -- household can spare; what it still falls short is owed (credit) when the villages trust each other
  -- credit_trust and more: at most credit_max FU, counted by the seller at credit_k (the chance of
  -- being paid), with at least credit_down of the price paid down in kind; unpaid after credit_days it
  -- is written off and trust falls by credit_loss
  credit_trust = 0.25, credit_max = 25, credit_k = 0.7, credit_days = 48, credit_loss = 0.15, credit_down = 0.25,
  -- (S19) fairs (sim/agents/fair.lua): called after the harvest (local days fair_doy) by a village of
  -- 8 and more with fair_food_days of food, at the middle of the villages it knows within fair_reach
  -- hours' road, fair_notice days ahead, opening at fair_open local, lasting fair_h hours; a household
  -- goes when what it could sell (at fair_price_k of base) and want, kin there (fair_kin FU each), a
  -- match to be made (fair_court) and the company (fair_company) outweigh the road; fair_party at most
  -- go from a household; a match is made when two take to each other more than fair_match
  fair_doy = { 30, 37 }, fair_food_days = 30, fair_reach = 34, fair_notice = 7, fair_open = 8, fair_h = 34,
  fair_price_k = 0.7, fair_kin = 2, fair_court = 5, fair_company = 1, fair_party = 3, fair_match = 0.2,
  -- (S16, R4) courting: a man with nobody to marry at home goes to a neighbour within court_road_h
  -- hours' walk with this chance a courtship round; he spends court_h meeting its people; a bride
  -- sets off with him court_wait_h after the match
  court_far = 0.25, court_road_h = 30, court_h = 1.5, court_wait_h = 3, court_retry_days = 60,
  -- (S16, R4) visiting kin at another village after visit_days apart; visit_h hours with them
  visit_days = 10, visit_h = 3,
  -- (S17) asking kin elsewhere for help: a household with food for under kin_ask_days at home while
  -- its village has under kin_ask_village days asks kin within kin_reach_h hours (not the same kin
  -- again for kin_ask_again days; kin_ask_h with them); kin keep kin_keep_days for themselves and
  -- give up to kin_gift_days of the asker's mouths; when the asker's village has under
  -- kin_offer_days and theirs over twice that, with room for both households (kin_room people), they
  -- offer a place: kin_offer_pull to moving there for kin_offer_keep days
  kin_ask_days = 2, kin_ask_village = 5, kin_reach_h = 40, kin_ask_again = 8, kin_ask_h = 1.5,
  kin_keep_days = 4, kin_gift_days = 6, kin_offer_days = 3, kin_room = 11, kin_offer_pull = 0.5, kin_offer_keep = 20,
  -- (S17, R5) the way overland (trade.land_route, every road walked): an hour's walk made dearer by
  -- rough ground (geo.slope), forest, mountains above route_high (km) and ice; entering a river
  -- costs route_river_h x its width (a ford found, a raft made)
  route_slope_k = 0.3, route_forest_k = 0.25, route_high_k = 0.8, route_high = 2.8, route_ice_k = 1.5,
  route_river_h = 1.0,
  -- (S17, R5) knowledge of places (founding.lua): a village knows the country within lore_home
  -- rings (a day or two out: years of hunting, herding and gathering); a road walked shows lore_side
  -- rings either side of it;
  -- what was seen is trusted less as it grows old (lore_fade days)
  lore_home = 4, lore_side = 1, lore_fade = 240,
  -- (S17, R5) founding a new place: no closer than found_min_steps cells to a village known; within
  -- found_reach_h hours' walk; push (hardship, no lot, no land) + the site's worth x found_site_k
  -- against what holds them, the road (found_road_hour an hour) and the season's risk, by
  -- found_threshold; a couple waiting found_lot_days with no lot to build on is pushed found_lot_push
  -- (+ found_lot_day a day more); they pack for found_pack_h; each grown-up carries up to found_carry
  -- FU, their share of the granary (found_share of their mouths' part) among it
  found_min_steps = 4, found_reach_h = 70, found_site_k = 0.6, found_road_hour = 0.008, found_threshold = 0.55,
  found_lot_days = 12, found_lot_push = 0.25, found_lot_day = 0.02, found_land_push = 0.2,
  found_pack_h = 20, found_carry = 12, found_share = 0.8, found_party = 3,
  found_dawn_h = 8, ready_h = 3,          -- they set off at this local hour, making ready ready_h before
  -- they want found_food_days of bread for their mouths to take (less weighs against going, x
  -- found_hungry_k); those who came to make a place hold to it found_stay_hold more for found_stay_days
  found_food_days = 10, found_hungry_k = 0.9, found_stay_days = 24, found_stay_hold = 0.6,
  found_risk_late = 0.35, found_risk_winter = 0.6,
  -- (S18) founding by sea: beasts that cannot come in the boat weigh against it, found_beast_k a
  -- hundred FU of their worth (a cow 0.1, a sheep 0.02)
  found_beast_k = 0.15, found_sea_loads = 3,
  -- (S18) a household founds only if it can take found_short_max of the bread it wants at least
  -- (starving people do not found); a hut lived in found_hut_days is replaced by a proper house once
  -- the place has its granary and woodstore
  found_short_max = 0.4, found_hut_days = 24,   -- (S19, D-147: was 0.6 - a starving household founded with 4 days' bread and died there)
  found_due_days = 8,                     -- (S19) nobody sets off to found within this many days of a birth (before or after)   -- (founders by sea keep room in the boat for this many loads of seed and bread)
  -- the young place: a camp (camp_work person-hours: the ground cleared, a fire ring, a brush lean-to),
  -- a first house a hut (hut_logs, hut_work), water from a river bank within found_water_reach of the
  -- camp or a well dug
  camp_work = 2.0, hut_logs = 10, hut_work = 55, found_water_reach = 0.7,
  -- (D-105) dogs: hearing wolves within dog_alert; trotting and running speeds; a dog alone at a
  -- wolf is killed with wolf_kill_dog a meeting; a pup comes dog_pup_days after a dog is lost;
  -- people awake within wolf_fear of a wolf at a pen drive the pack off; a barking dog turns it with
  -- dog_deter a step (not a starving pack); a watch asleep within guard_alert wakes
  -- (D-106) floods: the river's usual width is a running mean (flood_mean_k a day); water over
  -- flood_bridge x usual wears each bridge flood_wear x the excess a day; worn through, swept away
  flood_mean_k = 0.03, flood_bridge = 1.35, flood_wear = 0.9,
  -- (D-107) river water: dirt from houses and pens within foul_reach of where it is drawn; a day's
  -- chance of the flux flux_k x foulness; flux_days of sickness losing flux_health a day; the very
  -- young and old risk flux_hz a day; a household that fell sick keeps to the well river_shun_days
  -- (D-108) oxen: ploughing with one is ox_till x faster; ox_day_h hours of it a day; hungrier at
  -- the work; a harness takes harness_leather and harness_h
  ox_till = 2.5, ox_day_h = 6, ox_work_hunger = 0.03, harness_leather = 1.0, harness_h = 3.0,
  ox_stint_h = 2.5,                       -- (S14) hours in the yoke at a stretch (x diligence), row after row
  -- (S14) gulls (sim/agents/birds.lua): flight speed (local units an hour), hunger an hour, what an
  -- hour of each food takes off it, how long a landed catch draws them, flock size by the sea's fish
  gull_speed = 14, gull_hunger = 0.12, gull_eat_boat = 0.6, gull_eat_scraps = 0.7, gull_eat_plough = 0.5,
  gull_eat_shore = 0.3, gull_landing_h = 0.6, gull_max = 13, gull_per_fish = 7,
  foul_reach = 0.8, flux_k = 0.004, flux_days = 4, flux_health = 0.05, flux_hz = 0.01, river_shun_days = 40,
  dog_alert = 0.45, dog_trot = 1.9, dog_run = 4.0, wolf_kill_dog = 0.15, dog_pup_days = 12,
  wolf_fear = 0.16, dog_deter = 0.3, guard_alert = 0.4, dog_charge = 0.18,
  dog_see_off = 0.25,                     -- (S16) a dog out with a herd goes to see off a wild animal within this of the herd's edge
  -- (D-104) leather: hides soak in a tanning pit tan_days (put in and lifted out in tan_h); a pair of
  -- shoes takes shoe_leather and cobble_h; shoes wear a day by life stage; barefoot (under 0.1) one
  -- walks at barefoot_speed and the cold costs health; a household wants leather_want a member
  tan_days = 10, tan_h = 1.0, shoe_leather = 0.5, cobble_h = 2.0, barefoot_speed = 0.92, leather_want = 0.4,
  shoes_wear = { child = 0.016, apprentice = 0.012, adult = 0.009, elder = 0.006 },                        -- (D-103) food for fewer days than this and nothing to sell: ask a neighbour                              -- (D-102) hours an exhausted person sleeps in the day                          -- a skipper does not set out on a crossing longer than this on a wet day    -- a household shares beyond 2x its want with one under half of its own
  meat_want = 1.2, hunt_reach = 1.6, hunt_stalk = 0.3, hunt_throw = 0.07, stalk_speed = 0.4,
  hunt_hit = 0.25, hunt_skill = 0.35, hunt_tries = 3, hunt_hours = 5,                         -- after wolves took a beast, the village keeps a night watch this long
  wolf_visit = 0.5,                       -- a pack's chance to hunt on the map tonight: its time here x this
  pack_max = 7,                           -- wolves of a pack that come (the rest stay at the den)
  wolf_reach = 0.03, wolf_chase = 0.22,   -- caught up within; the chase begins within
  wolf_kill_wild = 0.3, wolf_kill_pen = 0.35,   -- a kill when caught up (the young +0.25, the sick +0.2; a pen's fence)
  wolf_eat_h = 1.0,                       -- hours the pack eats at a kill
  wolf_feed = 0.012,                      -- a pack's condition gained per FU of a village's beast per wolf
  carcass_hours = 30, carcass_left = 0.4, -- a carcass lies this long; share of its meat left by wolves
  sheep_speed = 1.6, sheep_hop = 0.08,    -- (D-089, kept for old saves)
  flock_radius = 0.07,
  sheep_hunger = 0.035, sheep_graze = 0.25,   -- hunger an hour; eaten an hour on full grass
  grass_per_feed = 0.01, grass_regrow = 0.08, -- a block's grass a unit of hunger takes; regrowth a day
  pasture_reach = 1.0,                    -- the herder's pasture: within this of the fold
  lamb_years = 0.35, lambing = { 1, 10 }, lamb_chance = 0.12,   -- a lamb keeps to its mother; lambing days, daily chance
  wool_day = 0.03, wool_max = 2.0,        -- GE of wool a sheep grows a day; at most
  shearing = { 4, 16 },                   -- local days of shearing (late spring)
  hay_found = { 10, 40, 140, 70 },        -- hay at founding by local season (hunger units)
  hay_days = 10,                          -- days of winter feed the village wants stacked a sheep
  mow_rate = 6,                           -- hay cut an hour on full grass (hunger units)
  slaughter = { 28, 40 },                 -- local days the surplus beasts are killed (autumn)
  sheep_meat = 6, spoil_meat = 0.2,       -- FU of meat a sheep; share of fresh meat spoiled a day
  -- reactions (hours)
  react_near = { 0.03, 0.12 }, react_far = { 0.1, 0.45 },
  rain_hardy = 0.7,                       -- diligence above which light rain does not stop work
  -- (S20, D-148) deeds: harms to goods fade over deed_half_goods days, blood over deed_half_blood; a
  -- harm to another household of the village weighs deed_kin_k of one's own; deeds younger than
  -- deed_news_days are told when people of two villages meet
  deed_half_goods = 96, deed_half_blood = 480, deed_kin_k = 0.35, deed_news_days = 96,
  -- (S20, D-149, R6a) taking a beast: weighed by a grown-up of a household whose hardship
  -- (migrate.push) is at least take_push, or that holds a grievance of take_grudge FU against the
  -- village; within take_road_h of road; the gain as days of food the beast is for the household
  -- (take_gain_k a FU), against the risk (take_dog for each dog seen there, take_watch for a watch,
  -- take_people for each grown man there / 10) and restraint (take_restraint, less boldness x
  -- take_bold; kin there take_kin; trust x take_trust; a friend there take_friend); grievance FU
  -- lower restraint by take_grudge_k each. A taker lies up until take_dark hours after the
  -- village's latest bedtime; a stranger by the pen at night is noticed by a dog within
  -- take_dog_r, by people awake within take_sight (by day sight_day), a beast handled is heard
  -- within take_bleat. Tracks are found when it rained in the last day or the soil is wetter than
  -- tracks_wet.
  take_push = 0.55, take_grudge = 6, take_road_h = 30, take_gain_k = 0.12,
  take_dog = 0.18, take_watch = 0.35, take_people = 0.06,
  take_restraint = 0.75, take_bold = 0.6, take_kin = 1.5, take_trust = 0.8, take_friend = 0.5, take_grudge_k = 0.04,
  take_dark = 1.5, take_dog_r = 0.18, take_sight = 0.12, sight_day = 0.6, take_bleat = 0.1,
  tracks_wet = 0.42, tracks_rain = 0.03, take_days = 4,
  take_hide = 0.35, take_h = 0.08, take_hold = 0.8, take_trust_loss = 0.15,
  take_flock = 3, take_need_days = 6,     -- sheep and goats one man leads off by night; days of food a taking is weighed against
  -- (S20, D-148) noticing: night sight a share of day sight; a sound wakes a sleeper within wake_k of
  -- its reach; reaction delays (hours) awake / asleep; an alarm is fresh this long; barking is
  -- heard this far; a chaser gives up after chase_h; caught within chase_reach
  night_sight_k = 0.2, wake_k = 0.6, notice_delay = 0.015, wake_delay = 0.04, alarm_h = 0.5,
  bark_sound = 0.35, chase_h = 0.6, chase_reach = 0.04,
  chase_lost = 3, chase_bold = 0.5,       -- a chase given up after this many looks without sight; the bold go out after what they saw
  -- (S20, R6b) claims: a wrong worth claim_min FU and more, within claim_road_h, asked about again
  -- after claim_again_days; taking back is weighed only for wrongs claimed and not made good, or
  -- where trust is below claim_trust_min. The accused household gives way when claim_base + trust x
  -- claim_trust + kin there claim_kin + deals/10 x claim_deals + the claimant village's grown men/10 x
  -- claim_fear + evidence claim_named outweighs the doer's boldness x claim_bold + its need x
  -- claim_need; settling raises trust by settle_trust; a refusal is a harm of refuse_fu
  claim_min = 3, claim_road_h = 36, claim_again_days = 12, claim_trust_min = 0.12, claim_h = 1.0,
  claim_base = 0.3, claim_trust = 0.8, claim_kin = 0.6, claim_deals = 0.3, claim_fear = 0.25, claim_named = 0.3,
  claim_bold = 0.9, claim_need = 0.6, settle_trust = 0.1, refuse_fu = 4,
  take_try_k = 0.25, take_dog_p = 0.6, take_failed = 0.5,   -- (each time given away there before, fading)
  -- (S20, R6c) injury: kept to bed at hurt_bed and above; heals hurt_heal a day (x0.4 hungry, x1.6
  -- tended); an untended wound festers with chance hurt_fester x hurt a day (+0.1); above 0.5 a daily
  -- death chance hurt_death x hurt^2. Blows: a man who catches a taker strikes with chance
  -- strike_k x (0.3 + boldness), a bold taker (take_fight) strikes back; a blow's hurt is blow x
  -- (1 + iron edge 0.5) x (0.4..1); at 1 it kills. A wound that answered the wounded man's own
  -- wrong weighs deserved_k in his people's grievance.
  hurt_bed = 0.6, hurt_heal = 0.035, hurt_fester = 0.15, hurt_death = 0.05,
  strike_k = 0.5, take_fight = 0.85, blow = 0.45, deserved_k = 0.2,
  wound_fu = 30, blood_fu = 200,
  run_k = 1.7,                            -- running pace (chasing, fleeing) over walking
  -- (S20, R6e) raids: a taking whose risk alone is raid_risk or more is raised as a party when there
  -- are men at home; joining weighs need (join_need), the wrong (as taking), kin of the leader
  -- (join_kin), his friendship (x join_friend), boldness (x join_bold) against join_base, ties to the
  -- target (as taking), a wife with child or a babe (join_family) and the risk shared by the party;
  -- the party sets off raid_lead hours after the next dawn
  raid_risk = 0.3, join_need = 0.5, join_kin = 0.45, join_friend = 0.5, join_bold = 0.4, join_base = 0.55,
  join_family = 0.25, raid_lead = 0.8, raid_hunger = 0.8,   -- (a man hungrier than this at the hour stays at home)
  -- (S20, R6g) amends: a household not in hardship weighs making good what it took when fear of an
  -- answer (amends_fear x their men/10 x (1 + wrongs they are known to have answered)) and ties
  -- (trust x amends_trust, deals/10 x amends_deals, kin there amends_kin) outweigh the worth given
  -- (amends_cost x FU/20)
  amends_fear = 0.35, amends_trust = 0.6, amends_deals = 0.4, amends_kin = 0.6, amends_cost = 0.5, amends_keep_days = 6, amends_part = 0.25,
  -- (S20, R6f) a fold wall: wanted when the losses at the pen in wall_memory days x wall_save outweigh
  -- wall_logs logs and wall_h hours (at wall_hour_fu a work hour); a taker at a walled pen is noticed
  -- x wall_noise more easily (dogs, cries) and weighs take_wall more risk; wolves are wary (wall_wolves)
  wall_memory = 48, wall_save = 0.7, wall_logs = 10, wall_h = 24, wall_hour_fu = 0.6,
  wall_noise = 1.35, take_wall = 0.3, wall_wolves = 1.5,
  -- (S20, R6.5b, D-154) fire on the maps: dryness from the soil's water (fire_wet .. fire_dry), less
  -- in rain; a tree burns fire_tree_h x (0.5 + 0.5 size) hours, a building fire_bld_h; heat grows
  -- fire_grow an hour (x (0.4 + dryness)), rain takes fire_rain1/fire_rain2 an hour; it reaches
  -- things within fire_reach (buildings x1.3) with chance fire_spread x heat x dryness x (1 +
  -- fire_wind downwind) an hour; a building's contents burn fire_contents x heat an hour, anyone
  -- inside fire_burn x heat; seen within fire_sight (more by night), shouted within fire_shout; water
  -- thrown takes douse_k of heat within douse_r; a tree burnt thins the planet's woods by
  -- fire_tree_forest; a house lost weighs house_fu
  fire_wet = 0.5, fire_dry = 0.18, fire_tree_h = 1.5, fire_bld_h = 1.4, fire_grow = 1.2, fire_rain1 = 0.3, fire_rain2 = 1.4,
  fire_reach = 0.06, fire_spread = 0.9, fire_wind = 1.0, fire_contents = 0.6, fire_burn = 0.6, fire_sight = 0.9,
  fire_shout = 0.45, douse_r = 0.1, douse_k = 0.06, fire_tree_forest = 0.0004, house_fu = 60, fire_help_r = 0.9,
  -- lightning: a strike takes the tallest thing within strike_pull (trees by size x strike_tree, a
  -- roof strike_roof, a person out of doors strike_person, less 4 a unit of distance); a tree catches
  -- with strike_ignite x (0.25 + 0.75 dryness), a roof strike_ignite_roof x (0.3 + 0.7 dryness); a
  -- person is hurt strike_hurt (at 1 they die), those within strike_side strike_side_hurt; thunder heard
  -- within thunder_sound; a downpour hour sends a strike to the map with chance strike_rate
  strike_pull = 0.08, strike_tree = 1.0, strike_roof = 0.9, strike_person = 0.5, strike_ignite = 0.5, strike_ignite_roof = 0.45,
  strike_hurt = { 0.5, 1.15 }, strike_side = 0.025, strike_side_hurt = 0.15, thunder_sound = 4, strike_rate = 0.12,
  -- (S20, R6.5c) the planet's fire on a cell above fire_planet_in comes onto the map; fire_planet_n
  -- things burning on one cell of the map set the planet's fire going there
  fire_planet_in = 0.05, fire_planet_n = 6,
  -- (S20, R6.5f, D-156) quakes: a convergent or transform boundary cell gathers quake_rate x its
  -- boundary strength a day (transform x0.7) and lets go past quake_thr x (0.6..1.4): magnitude in
  -- quake_mag (u x u: mostly small); felt at a village as magnitude - quake_fall a cell step -
  -- quake_floor; a building's damage = intensity/quake_unit x its vulnerability (x quake_footing on
  -- stone footings) x (1.3 - condition) x (0.5..1.5): cracked from quake_crack, fallen in from
  -- quake_fall_in; those inside hurt quake_hurt, trapped from quake_trap (dig_h x hurt hours of digging);
  -- trapped longer than trapped_h they weaken by trapped_worse a day; out of doors thrown down with
  -- chance quake_throw x intensity (quake_bruise)
  quake_thr = 1, quake_rate = 0.0007, quake_mag = { 4.5, 7.6 }, quake_fall = 1.0, quake_floor = 3.6, quake_unit = 2.0,
  quake_footing = 0.7, quake_crack = 0.35, quake_fall_in = 0.9, quake_hurt = { 0.2, 1.1 }, quake_trap = 0.5, dig_h = 2.0,
  trapped_h = 24, trapped_worse = 0.15, quake_throw = 0.2, quake_bruise = 0.08,
  -- (S20, R6.5f, D-157) volcanoes: volc_arcs sites on the strongest convergent boundaries (volc_spacing
  -- level-4 steps apart) and a hotspot; pressure volc_rate x (0.5..1.5) a day to volc_thr x (0.6..1.4);
  -- VEI 1 + 5u^3; aerosol by VEI (volc_aerosol: k_aer cools every cell), carbon volc_co2 x VEI; the
  -- ground shaken volc_quake + 0.6 VEI; ash on a village volc_ash x VEI^2 / (1 + steps^2) (x0.35 upwind);
  -- ash on the roofs weighs ash_load_k a unit of depth (shake.roof_load); washed ash_wash a day +
  -- ash_rain x precipitation; while it lies grass regrows x (1 - ash_grass x ash), crops lose ash_crop x
  -- ash of a day's growth
  volc_spacing = 3, volc_arcs = 6, volc_thr = 1, volc_rate = 0.0004, volc_aerosol = { 0, 0, 0.05, 0.15, 0.6, 1.5 },
  volc_co2 = 0.5, volc_quake = 3.0, volc_ash = 0.05, ash_wash = 0.03, ash_rain = 1.0,
  ash_grass = 0.5, ash_crop = 0.5,
  -- (S20, R6.5d, D-158) snow on the maps: a spell falling below snow_t C lies, snow_fall m a
  -- spell-hour (x2 in a downpour), melting snow_melt m per degree-hour above 0 (x2 in rain); lying
  -- from snow_cover; walking x (1 - min(snow_walk_max, snow_walk x depth)); grass in reach x (1 -
  -- depth / (snow_graze x snow_paw[sp])); while the grass is half buried the hay must feed every
  -- grazing beast snow_hay_days once it has been buried snow_wait days (starving beasts, health under
  -- snow_fail_health with no hay left, are killed at once); roofs weighed snow_load_k a metre (ash ash_load_k a unit) against
  -- their strength, cracked from roof_crack of it; a night below frost_t (frost_t_winter for a winter
  -- crop) on bare ground blackens min(1, degrees under / frost_span) x frost_k of a growing crop;
  -- bare feet in snow cost x (1 + snow_barefoot x depth / 0.2)
  snow_t = 0.5, snow_fall = 0.02, snow_melt = 0.0015, snow_cover = 0.02, snow_walk = 1.0, snow_walk_max = 0.55,
  snow_graze = 0.3, snow_paw = { sheep = 1.0, goat = 1.4, cattle = 0.8, pig = 0.6 }, snow_hay_days = 8, snow_wait = 3,
  snow_fail_health = 0.5, stock_starve = 0.012,   -- (a starving beast's health lost an hour: 0.02 before S20)
  snow_load_k = 0.6, ash_load_k = 1.0, roof_crack = 0.75, frost_t = -2, frost_t_winter = -12, frost_span = 6,
  frost_k = 0.5, snow_barefoot = 2,
  premise_cold = 22, premise_aerosol = 12,
  -- (S20, R6.5b) beasts and fire: out of its pen only when the gate is open - H.loose for loose_h
  -- hours - or a fold is well alight; its cry carries beast_cry; in the flames (fire_scorch +
  -- fire_scorch_heat x heat of a burning thing's edge: shut in a burning pen, trapped) it loses
  -- fire_beast x heat health an hour (wild animals and dogs alike).
  -- (S22) The fire by its own extent (a tree's crown fire_crown, a building's walls, a field's edges:
  -- danger.fire_near). A beast notices something catching or roaring up within beast_notice of it (x1.3
  -- by night: the glow) after its own delay - beast_notice_h x (0.3 + 0.9 x how slow it is to notice),
  -- x1.6 lying down, nearer sooner; it sees fire within beast_fire_see whenever it looks; it runs at
  -- beast_run x its speed its own way out (the way away from all that burns about it, bent to its own
  -- side by up to beast_fan, a stretch of at most beast_flee_hop at a time, never toward another fire)
  -- until the fire is its own safe distance off (beast_safe x (0.75 + 0.6 x timid)); while anything burns
  -- within beast_keep of where it is kept (the herder, the ground, the pen - or of the way there) it keeps
  -- about where it got to; it cries out within beast_cry_r of the fire. Wild animals run to wild_fire_safe
  -- and dogs to dog_fire_safe.
  beast_cry = 0.6, fire_beast = 1.5, loose_h = 3,
  fire_crown = 0.012, fire_scorch = 0.015, fire_scorch_heat = 0.025,
  beast_notice = 0.6, beast_notice_h = 0.06, beast_fire_see = 0.6, beast_run = 2.4, beast_fan = 1.1, beast_flee_hop = 0.25,
  beast_safe = 0.45, beast_keep = 0.4, beast_cry_r = 0.25, wild_fire_safe = 0.6, dog_fire_safe = 0.35,
  -- (S22) lightning on beasts: a beast out of doors (a fold is open to the sky; a byre or a sty has a roof)
  -- is a target by its height (a person is strike_person); struck it loses strike_hurt x strike_beast_k
  -- health (a wild animal or a dog dies past strike_beast_die); those within strike_side x
  -- strike_side_beast of it (four feet apart: the ground's current) lose strike_side_hurt x 2
  strike_beast = { sheep = 0.27, goat = 0.27, pig = 0.24, cattle = 0.42, deer = 0.36, reindeer = 0.38, antelope = 0.34,
    boar = 0.27, wolf = 0.26, cat = 0.24, dog = 0.24 },
  strike_beast_k = 1.4, strike_beast_die = 1.0, strike_side_beast = 1.6,
  -- (S22) a building's height to the bolt (strike_roof for any other): a fold of hurdles or a hay rack is
  -- low, a house's roof high
  strike_bld = { house = 0.9, granary = 0.9, byre = 0.8, forge = 0.7, woodstore = 0.6, sty = 0.5, camp = 0.4, clamp = 0.4,
    well = 0.35, fold = 0.3, rack = 0.3, boat = 0.3, dock = 0.2, bridge = 0.2, saltpan = 0.15, tanpit = 0.1 },
  -- (S20, R6) fear of a place: the unanswered harms people did to a household (its neighbours' at
  -- deed_kin_k), faded, push it to leave mig_fear_k x min(1, worth / fear_fu); a village whose people
  -- did them draws it 2 x as much less
  mig_fear_k = 0.5, fear_fu = 200,
  raid_wait_h = 1.5,
  deed_ally_k = 0.5,   -- (S20) a wrong to close kin in another village weighs this much of one's own
  wound_trust_loss = 0.2, blood_trust_loss = 0.5,
  -- (S20) fields burning: a field burns fire_plot_h x its fuel (ripe corn standing, stooks, corn
  -- drying off, stubble) hours, catches at fire_plot_catch of a tree's chance; its ash fire_plot_ash
  fire_plot_h = 1.0, fire_plot_catch = 0.8, fire_plot_ash = 0.03,
  -- (S20, R6.5f, D-162) tsunami: a quake under the sea of tsu_mag or more (an ocean volcano's VEI
  -- tsu_vei or more) raises a wave of strength (mag - tsu_mag + 1); at a coastal village it runs up
  -- tsu_k x strength / (1 + steps^2) world units from the sea (below tsu_min: nothing), arriving
  -- tsu_delay + steps / tsu_speed hours later; the sea draws back in the hour before (seen within
  -- tsu_see of the water, by night 0.3 of it; shouted within tsu_shout); those nearer the sea than
  -- tsu_safe run inland; within the run-up buildings fall, people are swept (tsu_hurt x depth), moored
  -- boats and beasts lost, fields salted (tsu_salt)
  tsu_mag = 7.0, tsu_vei = 5, tsu_k = 0.5, tsu_min = 0.05, tsu_delay = 0.3, tsu_speed = 4, tsu_see = 0.4,
  tsu_shout = 0.5, tsu_safe = 0.6, tsu_hurt = 1.3, tsu_salt = 0.25,
  -- (S20) aftershocks: a quake of after_mag or more is followed by 1 + (mag - after_mag) x after_n
  -- smaller ones (mag - after_drop - 0..1.5) within after_days, most of them soon after
  after_mag = 5.8, after_n = 2, after_drop = 1.0, after_days = 3,
  -- (S20) the badly hurt: from hurt_down they cannot walk away from home; the able who see them
  -- within help_sight (a quarter by night), or their kin, help them home at support_pace of a walk
  hurt_down = 0.75, help_sight = 0.6, support_pace = 0.35,
  -- (S20) leaf fodder: with the hay gone and the beasts hungry in the pen, boughs lopped from a tree
  -- within lop_r of the pen (lop_fodder sheep-days x (0.6 + 0.4 size)); a tree is lopped again after
  -- lop_days
  lop_r = 0.5, lop_fodder = 3.0, lop_days = 30,
  -- (S20) an ice crust: snow that thawed within the day and freezes below crust_t; grass in reach
  -- x crust_k until the snow is gone
  crust_t = -1.0, crust_k = 0.25,   -- (S20) trust lost by a village whose man another's struck (x the blow)   -- (S20) a band at the gate waits up to this long for those of the party still coming
  -- (S20, R6) restocking: the beasts a household lost against its will (sickness, hunger, wolves,
  -- fire, thieves; meat FU, fading herd_lost_fade a day) against what it had: from restock_min_fu, a
  -- loss of take_restock of it is a motive to take beasts even when fed: gain + take_gain_k x the
  -- beasts' meat x the share lost x restock_k
  herd_lost_fade = 0.99, restock_min_fu = 30, take_restock = 0.5, restock_k = 1.0,
  -- (S21, R6.5e, D-169) flood water on the maps (sim/agents/flood.lua). A river whose flow passes the
  -- planet's flood line (HYDRO.flood_mult x bankfull) stands fl_k x min(excess, fl_cap)^0.6 m above its
  -- bank; the stage rises fl_rise m an hour and falls fl_fall; each tile's height above its river is
  -- its distance beyond the bank (tiles, within fl_reach) x fl_g x (0.25 + the cell's slope) plus an
  -- unevenness of fl_rough m on a lattice of fl_cell tiles; the water is spread again when a stage
  -- moved fl_step m. Shallow water (fl_wade m) is waded, deeper closes the way.
  fl_k = 1.0, fl_cap = 2.5, fl_rise = 0.2, fl_fall = 0.05, fl_reach = 40, fl_g = 0.16, fl_rough = 0.25,
  fl_cell = 7, fl_step = 0.03, fl_wade = 0.45,
  -- what lies in it: houses take water above fl_floor (the stores on the floor spoil fl_spoil x
  -- depth/0.8; a granary on its posts above fl_granary), walls soften fl_wear x depth an hour (huts
  -- x2), a hut or a building below fl_weak condition falls in under fl_fall m; a field fl_field_share
  -- under water drowns its growing crop after fl_crop_h hours (yield x 1 - fl_drown), its ripe corn
  -- and sheaves rot after fl_ripe_h / fl_sheaf_h (x 1 - fl_rot); silt fl_silt x depth (fertility at
  -- most fl_fert_max) when it goes down; a beast in water fl_beast_d m above its own depth loses
  -- fl_beast_h health an hour; a person in water above fl_sweep m is swept (fl_hurt x ...)
  fl_floor = 0.15, fl_spoil = 0.6, fl_granary = 0.5, fl_wear = 0.02, fl_weak = 0.45, fl_fall = 1.2,
  fl_field_share = 0.35, fl_crop_h = 30, fl_drown = 0.85, fl_ripe_h = 10, fl_sheaf_h = 6, fl_rot = 0.7,
  fl_silt = 0.08, fl_fert_max = 1.3, fl_beast_h = 0.35,
  fl_beast_d = { sheep = 0.35, goat = 0.4, cattle = 0.7, pig = 0.3 },
  fl_sweep = 0.75, fl_hurt = 0.9,
  -- noticing it: those awake out of doors where the ground stands less than fl_near m above the water
  -- see the river over its banks; raised again after fl_alarm_h hours; shouted (fl_shout units);
  -- they go where the ground stands fl_safe m above the water (think.flood_choice); a household
  -- whose house has fl_home m of water in it sleeps elsewhere (people.home)
  fl_near = 0.5, fl_alarm_h = 3, fl_shout = 0.45, fl_safe = 0.6, fl_home = 0.15, fl_see = 0.4,
  -- (S21, R6) arms: a hunter (hunting skill from arm_hunt) takes his spear, anyone else the household's
  -- axe; a blow x arm_blow_spear / _axe / _none (bare hands), x arm_blow_iron with an iron edge; in a
  -- fight an armed man counts 1 + arm_might (+ arm_iron_might with iron)
  arm_hunt = 0.35, arm_blow_spear = 1.15, arm_blow_axe = 1.0, arm_blow_none = 0.6, arm_blow_iron = 1.35,
  arm_might = 0.4, arm_iron_might = 0.15,
  -- (S21, R6) kin who heard their kin lost a house within help_days (and have no roof of their own
  -- yet) go to help: not a household whose push is over help_push; a gift of up to help_gift FU from
  -- what its larder holds beyond help_keep_days a member; they stay help_stay_h (and the road) at most;
  -- the help weighs help_fu in the deed, trust rises help_trust; again for the same kin after
  -- help_again_days
  help_days = 16, help_push = 0.35, help_gift = 12, help_keep_days = 4, help_stay_h = 52, help_fu = 15,
  help_trust = 0.08, help_again_days = 24,   -- (premises: a cold air mass, degC; sulphur after a great eruption)          -- what a wound (x its hurt) and a life weigh in grievance (FU)    -- (a dog near the pen notices a stranger at the beasts with this chance, less for a stalker)                      -- an attempt that came to nothing weighs this share of a taking
}

return C
