-- S02 wolves_removed (07_TESTING.md §3): cull every predator cluster on the largest continent.
-- Stage 2.1: the Cull power (radius 2, 90%) on every cluster, like a determined player.
return {
  name = "wolves_removed",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 10,
  chapter = 1,
  actions = {
    { year = 1, power = "cull", helper = "cull_all", species = "predators", target = "continent:largest" },
  },
  expect = {
    { metric = "grazers@target", compare = "ratio_to_baseline", op = ">=", value = 2.0, within_years = 4 },
    { metric = "forest@target", compare = "ratio_to_baseline", op = "<=", value = 0.85, within_years = 8 },
    { chain_from_action = 1, min_links = 3, must_include = { "population_crash", "population_boom" } },
  },
}
