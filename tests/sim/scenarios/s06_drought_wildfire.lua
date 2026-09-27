-- S06 drought_wildfire (07_TESTING.md §3): a Drought over dry forest in spring, then a single
-- lightning bolt at midsummer: the parched forest burns as a wildfire (>= wildfire size)
-- within two seasons. (Natural ignitions alone are too rare to test in one season.)
return {
  name = "drought_wildfire",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 2.2,
  chapter = 2,
  actions = {
    { year = 1.1, power = "weather", mode = "drought", target = "forest:dry", local_calendar = true },
    { year = 1.4, power = "lightning", target = "same:1" },
  },
  expect = {
    { descendant_kind = "wildfire", from_action = 2 },
    { chain_from_action = 2, min_links = 3, must_include = { "fire_started", "wildfire" } },
  },
}
