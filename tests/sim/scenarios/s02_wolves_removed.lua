-- S02 wolves_removed (07_TESTING.md §3): remove every predator on the largest continent.
-- Chapter 1 uses the test hook (via the cull_all helper); Stage 2.1 switches to the Cull power.
return {
  name = "wolves_removed",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 10,
  chapter = 1,
  actions = {
    { year = 1, helper = "cull_all", species = "predators", target = "continent:largest" },
  },
  expect = {
    { metric = "grazers@target", compare = "ratio_to_baseline", op = ">=", value = 2.0, within_years = 4 },
    { metric = "forest@target", compare = "ratio_to_baseline", op = "<=", value = 0.85, within_years = 8 },
    { chain_from_action = 1, min_links = 3, must_include = { "population_crash", "population_boom" } },
  },
}
