-- S03 farm_fire_famine (07_TESTING.md §3): burn an inland farming village's fields shortly
-- before the harvest (tick-of-year ≈ 26 of the village's own farming calendar; harvest from 32).
-- Chapter 2 switches to Lightning.
return {
  name = "farm_fire_famine",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 4,
  chapter = 1,
  actions = {
    { year = 1.55, hook = "burn_farms", target = "village:inland", local_calendar = true },
  },
  expect = {
    -- crop_failure then famine within a year, in one chain of ≥ 3 links from the fire
    { chain_from_action = 1, min_links = 3, must_include = { "crop_failure", "famine" }, within_years = 1 },
    -- the village's community (with any daughter villages it founds, D-036) loses people
    { metric = "pop@community", compare = "ratio_to_baseline", op = "<=", value = 0.95, within_years = 3 },
  },
}
