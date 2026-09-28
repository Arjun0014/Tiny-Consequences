-- The society's carbon (D-058): biomass people and their beasts take from the land — wood cut,
-- brush and wild food gathered, grass grazed, forest cleared — goes back to the air when it is
-- taken (burned, eaten and breathed out soon after), at the vegetation's own rate (VEG.k_co2, the
-- same the growth that absorbed it used). Burning fuel from the store emits nothing more.
local cfgmod = require("sim.config")
local ledger = require("sim.ledger")

local M = {}

-- `biomass` units (forest or grass cover of a cell) returned to the air by villagers.
M.by = {}   -- (diagnostics for tools: biomass released by kind; not state)
function M.release(W, biomass, kind)
  if biomass <= 0 then return end
  if kind then M.by[kind] = (M.by[kind] or 0) + biomass end
  local co2 = biomass * cfgmod.VEG.k_co2
  W.G.co2_src = (W.G.co2_src or 0) + co2
  ledger.add(W, "co2", co2, 0, "villagers")
end

-- Grass taken (grazed, gathered as brush or wild food): its carbon at the weight the vegetation's
-- growth absorbed it (VEG.grass_c; session 5 — it was released at the forest's weight, twice
-- what the grass had absorbed: a standing source of CO2 wherever beasts grazed).
function M.release_grass(W, grass, kind)
  M.release(W, grass * cfgmod.VEG.grass_c, kind)
end

return M
