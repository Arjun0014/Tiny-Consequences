-- S15 tutorial_chain (07_TESTING.md §3, 04_PLAYER.md §6): on the tutorial world (seed 8,
-- assets/worlds/tutorial.tcsave), Lightning on the tanker next to the fishing village must
-- give a chain of headlines within 3 years. One fixed world, so it must pass every time.
-- W2 (D-054): the settlement layer's villages farm, forage, dry fish and fish other grounds, so a
-- single spill no longer starves them (a 40-seed scout found no fish_collapse > famine chain);
-- until W3's merchant ships carry the tutorial's cargo (tanker_sunk > shipment_lost > rationing),
-- the chain it must show is the spill reaching the shore: tanker_sunk > ... > oil_beached.
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
    { chain_from_action = 1, min_links = 4, must_include = { "tanker_sunk", "oil_beached" }, within_years = 3 },
  },
}
