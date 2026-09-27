-- S10 meteor (meteor part; the tsunami comes with Chapter 3's geology): a Meteor next to the
-- biggest village sets the land burning, kills villagers and animals and darkens the sky.
return {
  name = "meteor",
  seeds = "standard",
  pass_ratio = { quick = 2 / 3, full = 7 / 10 },
  run_years = 2,
  chapter = 2,
  actions = {
    { year = 1, power = "meteor", target = "near:village" },
  },
  expect = {
    { metric = "aerosol@global", compare = "absolute", op = ">=", value = 0.5, within_years = 0.2 },
    { metric = "pop@village", compare = "ratio_to_baseline", op = "<=", value = 0.9, within_years = 0.5 },
    { descendant_kind = "fire_started", from_action = 1 },
  },
}
