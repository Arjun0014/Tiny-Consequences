-- Every tunable constant (02_WORLD_MODEL.md: "never hard-code a tunable inside a system").
-- Grouped by system; units and intended effect in comments. Tuning is expected. Changing a
-- tuning *band* or test *threshold* needs a DECISIONS.md entry; changing these doesn't.

local C = {}

-- Grid --------------------------------------------------------------------------------------
C.SIM_LEVEL = 3            -- icosphere level: 4 = 2,562 cells (target), 3 = 642 (fallback); D-004

-- Time --------------------------------------------------------------------------------------
C.TICKS_PER_YEAR = 48
C.SPINUP_YEARS = 30        -- worldgen spin-up (no events recorded)
C.SPINUP_VEG_YEAR = 4      -- spin-up year at which vegetation is seeded from the settled climate

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
  diffusion = 0.04,
  k_eo = 0.08,                            -- ocean evaporation per tick at sst 15 °C
  k_el = 0.06,                            -- land evapotranspiration scale
  k_cap = 1.0,                            -- capacity at 0 °C (x exp(0.06 T))
  k_oro = 0.5,                            -- capacity loss per km of upslope lift
  k_rain = 0.5, k_drizzle = 0.06,
  -- capacity multiplier by |lat| (degrees, band-shifted): ITCZ wet, subtropics dry
  cap_profile = { { 0, 0.7 }, { 8, 0.75 }, { 15, 1.0 }, { 22, 1.5 }, { 30, 1.7 }, { 38, 1.2 }, { 50, 0.95 }, { 65, 1.0 }, { 90, 1.2 } },
  inflow_share = 0.3,                     -- a tagged inflow must carry ≥ 30% to pass its tag on
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
  flow_ema = 0.01, bankfull_min = 0.2, flood_mult = 1.5,
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
  r_grass = 0.06, r_forest = 0.006,       -- per tick at ideal conditions (grass 1–3 y, forest 30–60 y)
  K_grass = 1.0, K_forest = 0.95,
  die_grass = 0.03, die_forest = 0.0025,  -- per tick under full water stress
  die_frost = 0.004, frost_temp = -8,     -- slow frost dieback of grass below this temp
  soil_half_grass = 0.2, soil_half_forest = 0.45,
  seed_grass = 0.01, seed_forest = 0.002, -- recolonisation from neighbours
  grass_t = { 0, 36 }, forest_t = { 2, 36 },   -- temperature bells (°C)
  light_amp = 0.4,
  k_regen = 0.0005, fert_base = { 0.25, 0.75 },  -- fertility target from bare to fully vegetated
  fire_block_ticks = 96, scar_expire = 480,      -- recent burns slow forest establishment
  k_co2 = 0.4,                            -- ppm per unit biomass (growth absorbs, burning emits)
}

-- Fire (§5.7) --------------------------------------------------------------------------------------------------
C.FIRE = {
  k_lightning = 1.5e-3,                   -- ignition probability scale per land cell per tick
  storm_precip = 5,                       -- summer storms: more lightning where it rains a little
  min_fuel = 0.08, start_intensity = 0.6,
  k_spread = 0.42, wind_boost = 1.5,
  burn_grass = 0.6, burn_forest = 0.25,   -- fraction consumed per tick at intensity 1
  intensity_per_fuel = 1.5, min_intensity = 0.12,
  rain_extinguish = 4,                    -- intensity loss per unit precip
  k_co2 = 0.4,                            -- ppm per unit biomass burned
  k_heat = 3,                             -- heat_anom °C per unit intensity per tick
  wildfire_cells = 10, ash_fertility = 0.15,
}

-- Biomes (§5.15): entry / exit thresholds (hysteresis) and dwell ------------------------------------------
C.BIOME = {
  sea_ice_enter = 0.5, sea_ice_exit = 0.35, ice_sheet_enter = 0.8, ice_sheet_exit = 0.6,
  ash_ticks = 24,
  marsh_enter = 0.85, marsh_exit = 0.75, marsh_max_elev = 0.4,
  desert_soil = 0.15, desert_veg = 0.15, desert_min_temp = 5, desert_exit_veg = 0.3, desert_exit_soil = 0.25,
  tundra_temp = 0, tundra_veg = 0.4, tundra_exit_veg = 0.5,
  jungle_enter = 0.6, jungle_exit = 0.5, jungle_temp = 22, jungle_soil = 0.5,
  forest_enter = 0.5, forest_exit = 0.4, grass_enter = 0.25, grass_exit = 0.18,
  dwell = 12, dwell_long = 48,
}

C.LEDGER = { decay_per_year = 0.03, dominant_share = 0.4 }

return C
