-- Scenario specs (07_TESTING.md §3). tests/sim/run.lua wraps each spec with
-- tests/sim/scenarios/lib.lua (as_module).
return {
  "tests.sim.scenarios.s01_baseline_stability",
  "tests.sim.scenarios.s02_wolves_removed",
  "tests.sim.scenarios.s03_farm_fire_famine",
  "tests.sim.scenarios.s04_tanker_oil",
  "tests.sim.scenarios.s05_ocean_heat_rain",
  "tests.sim.scenarios.s06_drought_wildfire",
  "tests.sim.scenarios.s07_invasive_island",
  "tests.sim.scenarios.s10_meteor",
  "tests.sim.scenarios.s11_blight_spread",
  "tests.sim.scenarios.s12_chill_sea_ice",
  "tests.sim.scenarios.s15_tutorial_chain",
  "tests.sim.scenarios.p08_attribution",
}
