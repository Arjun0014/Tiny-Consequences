-- S15 tutorial_chain (07_TESTING.md §3, 04_PLAYER.md §6): on the tutorial world (seed 8,
-- assets/worlds/tutorial.tcsave), Lightning on the tanker next to the fishing village must
-- give a chain of headlines within 3 years: the tanker sinks, the fish collapse, the village
-- starves. One fixed world, so it must pass every time.
return {
  name = "tutorial_chain",
  seeds = { 8 },
  pass_ratio = { quick = 1, full = 1 },
  run_years = 3.1,
  chapter = 2,
  actions = {
    { year = 0, power = "lightning", target = "tanker:tutorial" },
  },
  expect = {
    { chain_from_action = 1, min_links = 3, must_include = { "tanker_sunk", "fish_collapse", "famine" }, within_years = 3 },
  },
}
