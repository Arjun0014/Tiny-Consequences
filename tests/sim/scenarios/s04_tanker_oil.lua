-- S04 tanker_oil (07_TESTING.md §3): oil released (test hook; the Oil power / a sunk tanker in
-- Chapter 2) on a fishing village's fishing grounds. amount 6 = a tanker's cargo
-- (SHIPS.cargo): ~6 level-3 cells (≈ 24 level-4 cells) of solid slick.
return {
  name = "tanker_oil",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 3,
  chapter = 1,
  actions = {
    { year = 1, hook = "oil", target = "ocean_near:village:fishing", amount = 6 },
  },
  expect = {
    { metric = "fish@target", compare = "ratio_to_baseline", op = "<=", value = 0.5, within_years = 1 },
    { descendant_kind = "oil_spreading", from_action = 1 },
    { descendant_kind = "oil_beached", from_action = 1 },
    -- oil → fish → the village's food
    { chain_from_action = 1, min_links = 2, must_include = { "test_oil", "fish_collapse" } },
    { metric = "food_security@village", compare = "ratio_to_baseline", op = "<=", value = 0.7, within_years = 2 },
  },
}
