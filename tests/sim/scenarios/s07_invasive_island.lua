-- S07 invasive_island (07_TESTING.md §3): Introduce grazers to a predator-free island. With no
-- wolves they boom, strip the grass and saplings, and the island's vegetation collapses.
return {
  name = "invasive_island",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 12,
  chapter = 2,
  actions = {
    { year = 1, power = "introduce", species = "grazers", target = "island:no_predators", radius = 2 },
  },
  expect = {
    { metric = "veg@target", compare = "ratio_to_baseline", op = "<=", value = 0.6, within_years = 10 },   -- 0.5 in 07 §3; D-049
    { chain_from_action = 1, min_links = 2, must_include = { "population_boom" } },
  },
}
