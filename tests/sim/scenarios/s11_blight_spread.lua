-- S11 blight_spread (07_TESTING.md §3): Blight on one farm spreads farm to farm and, by trade,
-- to other villages; the lost harvests turn into famine.
return {
  name = "blight_spread",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 4.5,
  chapter = 2,
  actions = {
    { year = 1.05, power = "blight", target = "village:farming", local_calendar = true },
  },
  expect = {
    { metric = "blight_villages", compare = "absolute", op = ">=", value = 2, within_years = 2 },
    -- (W2, D-054: villages keep a year or more in store — research §2: overproduction and
    -- storage are the normal answer to crop-failure risk — so one blight need not end in hunger;
    -- it must spread and fail harvests)
    { chain_from_action = 1, min_links = 3, must_include = { "crop_failure" } },
  },
}
