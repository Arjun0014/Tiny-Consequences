-- Systems in tick order (02_WORLD_MODEL.md §4). Static requires so bob bundles them.
return {
  require("sim.powers"),                -- sustained power effects (weather, temperature, blight)
  require("sim.systems.globalclimate"),
  require("sim.systems.temperature"),
  require("sim.systems.moisture"),
  require("sim.systems.ice"),
  require("sim.systems.hydrology"),
  require("sim.systems.vegetation"),
  require("sim.systems.fire"),
  require("sim.systems.ocean"),
  require("sim.systems.fauna"),
  require("sim.systems.villages"),
  require("sim.systems.ships"),
  require("sim.systems.biome"),
  require("sim.causality.detectors"),
}
