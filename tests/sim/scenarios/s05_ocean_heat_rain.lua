-- S05 ocean_heat_rain_shift (07_TESTING.md §3): Heat a coastal sea upwind of land for a season;
-- the warmer sea evaporates more and the land downwind gets measurably more rain.
return {
  name = "ocean_heat_rain_shift",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 1.6,
  chapter = 2,
  actions = {
    { year = 1.05, power = "temperature", mode = "heat", target = "ocean_upwind:land" },
  },
  expect = {
    { metric = "precip@target", compare = "ratio_to_baseline", op = ">=", value = 1.15, within_years = 0.3, mean = true },
  },
}
