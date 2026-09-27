-- S12 chill_sea_ice (07_TESTING.md §3): Chill the coldest ice-free coastal sea as its autumn
-- turns: the surface water cools past freezing and sea ice forms (fishing grounds under ice,
-- and in a cold enough era a bridge for animals). At 1,442 cells no fishing village sits on
-- waters that can freeze, so the catch part of 07 §3 is left to play (D-049).
return {
  name = "chill_sea_ice",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 2.2,
  chapter = 2,
  actions = {
    { year = 1.55, power = "temperature", mode = "chill", target = "ocean:cold_coast" },
  },
  expect = {
    { metric = "ice@target", compare = "absolute", op = ">=", value = 0.25, within_years = 0.5 },
  },
}
