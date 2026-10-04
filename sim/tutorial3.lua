-- The v3 tutorial's starting conditions (S25, D-211; the legacy v2 tutorial is sim/tutorial.lua): a command, logged and applied at the next tick like every
-- player action, that sets PHYSICAL state only - the ground about one village dry, as after a rainless spell -
-- so that a bolt into a tree there can catch. Nothing here lights a fire, harms anyone or decides an outcome:
-- whether the tree catches, how far the fire goes, who runs, who fights it and what is lost are the world's own
-- rules. It carries no cause (the dry ground is nature's; the bolt the player throws is the root of the chain).
--   tutorial_dry { sid, sw }: the soil water of the village's cell and the cells about it at most sw
local world = require("sim.world")

local M = {}

world.COMMANDS.tutorial_dry = function(W, p)
  local V = W.S.v3
  local s = V and V.settlements[p.sid or 0]
  if not s then return end
  local sw = p.sw or 0.2
  local F, g = W.F, W.grid
  local cells = { s.cell }
  for k = 1, g.nbr_count[s.cell] do cells[#cells + 1] = g.nbr[s.cell * 6 - 6 + k] end
  for _, c in ipairs(cells) do
    if W.geo.ocean[c] == 0 and F.soil_water[c] > sw then F.soil_water[c] = sw end
  end
end

return M
