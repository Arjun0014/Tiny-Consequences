-- S02 wolves_removed (07_TESTING.md §3): cull every predator cluster on the largest continent.
-- Stage 2.1: the Cull power (radius 2, 90%) on every cluster, like a determined player — and,
-- since the wolves are packs that breed (D-071, D-073), again every six months for two years: a
-- single cull only dents them (seed 3: 254 -> 91 wolves, 144 again a year later, fed by the
-- prey it spared), as real extirpations took decades of bounties. The thresholds are unchanged.
return {
  name = "wolves_removed",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 10,
  chapter = 1,
  actions = {
    { year = 1, power = "cull", helper = "cull_all", species = "predators", target = "continent:largest" },
    { year = 1.5, power = "cull", helper = "cull_all", species = "predators", target = "same:1" },
    { year = 2, power = "cull", helper = "cull_all", species = "predators", target = "same:1" },
    { year = 2.5, power = "cull", helper = "cull_all", species = "predators", target = "same:1" },
    { year = 3, power = "cull", helper = "cull_all", species = "predators", target = "same:1" },
  },
  expect = {
    { metric = "grazers@target", compare = "ratio_to_baseline", op = ">=", value = 2.0, within_years = 4 },
    { metric = "forest@target", compare = "ratio_to_baseline", op = "<=", value = 0.85, within_years = 8 },
    -- the chain from the campaign (each cast is its own root; the herds' boom follows the cull
    -- that emptied their ground last): the wolves crash or die out, and the herds boom — through
    -- the crash where the crash detector re-tagged the ground, straight from the cull elsewhere
    -- (D-073: of 10 seeds, 5 chain cull > crash > boom, 2 cull > extinction > boom, 3 cull > boom)
    { descendant_kind = { "population_crash", "extinction_regional" }, from_action = { 1, 2, 3, 4, 5 } },
    { chain_from_action = { 1, 2, 3, 4, 5 }, min_links = 2, must_include = { "power_cull", "population_boom" } },
  },
}
