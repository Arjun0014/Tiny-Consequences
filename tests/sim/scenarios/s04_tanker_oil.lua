-- S04 tanker_oil — oil portion (07_TESTING.md §3). Oil released by the test hook next to the
-- largest continent (villages and tankers arrive in Stage 1.7; then target the fishing village's
-- sea and add the village food / famine expectations). amount 6 ≈ a tanker's cargo: ~6 level-3
-- cells (≈ 24 level-4 cells) of solid slick.
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
    -- oil → fish: test_oil > [oil_spreading >] fish_collapse
    { chain_from_action = 1, min_links = 2, must_include = { "test_oil", "fish_collapse" } },
  },
}
