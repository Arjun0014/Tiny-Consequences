-- Scenario specs (07_TESTING.md §3). tests/sim/run.lua wraps each spec with
-- tests/sim/scenarios/lib.lua (as_module).
return {
  "tests.sim.scenarios.s01_baseline_stability",
  "tests.sim.scenarios.s02_wolves_removed",
  "tests.sim.scenarios.s03_farm_fire_famine",
  "tests.sim.scenarios.s04_tanker_oil",
}
