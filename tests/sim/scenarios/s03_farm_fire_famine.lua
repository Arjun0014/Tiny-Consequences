-- S03 farm_fire_famine (07_TESTING.md §3): burn an inland farming village's fields shortly
-- before the harvest. Stage 2.1: Lightning on every farm cell (the fire spreads through the
-- ripening crops). W2: the strike waits for the village's own crops to be past half-grown
-- (`when = "crop_half"`: villages sow by their own climate, tropical ones whenever warm).
return {
  name = "farm_fire_famine",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 4,
  chapter = 1,
  actions = {
    { year = 1.0, when = "crop_half", power = "lightning", helper = "lightning_farms", target = "village:inland" },
    -- a determined player burns the next harvest too: at 1,442 cells the inland village can
    -- store more than a year of food, so one lost harvest only empties the granary (D-047)
    { year = 2.0, when = "crop_half", power = "lightning", helper = "lightning_farms", target = "same:1" },
  },
  expect = {
    -- crop_failure then famine within a year, in one chain of ≥ 3 links from the fire
    { chain_from_action = 1, min_links = 3, must_include = { "crop_failure", "famine" }, within_years = 1 },
    -- the village's community (with any daughter villages it founds, D-036) loses people
    { metric = "pop@community", compare = "ratio_to_baseline", op = "<=", value = 0.95, within_years = 3 },
  },
}
