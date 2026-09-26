-- S01 baseline_stability (07_TESTING.md §3): the untouched world, 200 years, every tuning band in
-- 02_WORLD_MODEL.md §10. A seed passes when all its bands hold; cross-seed criteria (village
-- turnover) are checked over the seed set. Runs share the cached baseline runs with the
-- property tests (tests/sim/support/baseline.lua).
return {
  name = "baseline_stability",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 200,
  chapter = 1,
  baseline_only = true,
  expect = {
    -- vegetation bands on natural land (land minus villages, farms, ruins; D-025)
    { band = "forest_nat", min = 0.20, max = 0.55, stat = "mean" },
    { vary = "forest_nat", min = 0.05 },
    { band = "grass_nat", min = 0.20, max = 0.50, stat = "mean" },
    { band = "desert_nat", min = 0.05, max = 0.25, stat = "mean" },
    { band = "ice", min = 0.05, max = 0.20, stat = "mean" },
    { band = "g_temp", min = -1.5, max = 3.5, stat = "all" },
    { band = "g_temp", min = 0.0, max = 2.0, stat = "final" },
    { band = "population", min = 120, max = 400, stat = "all", from_year = 5 },
    { fauna = true },                                    -- no continental extinction, predator CV >= 0.15, fish
    { events_per_decade = "fire_started", min = 3 },
    { wildfire_every_years = 50 },
    { headline_rate = { 0.2, 3.0 } },
    { cross_seed = "turnover", min_share = 0.5 },        -- >= 1 village founded or abandoned by year 200
  },
}
