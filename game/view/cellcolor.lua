-- Per-sim-cell display values for the terrain data texture (Stage 1.8). Pure Lua 5.1.
-- Each cell packs two RGBA8 texels:
--   land cell : A = ground colour rgb + snow cover,   B = fire, ash, crop, village
--   ocean cell: A = 0,0,0 + sea ice,                  B = 0, oil, plankton, 0
-- The terrain vertex shader blends these over the 3 sim cells of each render vertex, so a
-- colour update costs O(cells), never O(render vertices).
local palette = require("content.palette")

local M = {}

local floor, min, max = math.floor, math.min, math.max

local function col(hex)
  local r, g, b = palette.rgb(hex)
  return { r / 255, g / 255, b / 255 }
end

-- Ground colours (the trees on top supply the dark forest greens).
local C = {
  meadow = col("#8CCB5E"), meadow_warm = col("#AED15A"), dry = col("#DDCA8C"), bare = col("#C9B892"),
  forest_floor = col("#5FAE4A"), jungle_floor = col("#3E9A45"), tundra = col("#B9C7A3"),
  desert = col("#EDCB84"), marsh = col("#6E9C6A"), farm = col("#E3C35A"), farm_young = col("#A7CF55"),
  village = col("#C9A27A"), ash = col("#5A5552"), ruins = col("#8A7F74"), barren = col("#C2B08C"),
}
M.COLORS = C

local function clamp01(x) if x < 0 then return 0 elseif x > 1 then return 1 end return x end

local function byte(x)
  x = floor(x * 255 + 0.5)
  if x < 0 then return 0 elseif x > 255 then return 255 end
  return x
end

-- Biome ids (sim/systems/biome.lua)
local B_SEA_ICE, B_ICE_SHEET, B_VILLAGE, B_FARM, B_RUINS, B_ASH = 2, 3, 4, 5, 6, 7
local B_MARSH, B_DESERT, B_TUNDRA, B_JUNGLE = 8, 9, 10, 11

--- Computes the 8 bytes of cell c into out[base+1..base+8].
function M.cell(W, c, out, base)
  local F = W.F
  if W.geo.ocean[c] == 1 then
    out[base + 1], out[base + 2], out[base + 3] = 0, 0, 0
    out[base + 4] = byte(F.ice[c])
    out[base + 5] = 0
    out[base + 6] = byte(min(1, F.oil[c] * 3))
    out[base + 7] = byte(F.plankton[c])
    out[base + 8] = 0
    return
  end
  local biome = F.biome[c]
  local grass, forest = F.grass[c], F.forest[c]
  local soil = F.soil_water[c]
  -- natural ground: dry soil -> meadow by grass, then forest floor by forest
  local g = clamp01(grass * 1.7)
  local warm = clamp01(1 - soil * 1.6) * 0.6
  local r = C.meadow[1] + (C.meadow_warm[1] - C.meadow[1]) * warm
  local gg = C.meadow[2] + (C.meadow_warm[2] - C.meadow[2]) * warm
  local b = C.meadow[3] + (C.meadow_warm[3] - C.meadow[3]) * warm
  r = C.dry[1] + (r - C.dry[1]) * g
  gg = C.dry[2] + (gg - C.dry[2]) * g
  b = C.dry[3] + (b - C.dry[3]) * g
  local ff = biome == B_JUNGLE and C.jungle_floor or C.forest_floor
  local f = clamp01(forest * 1.3)
  r, gg, b = r + (ff[1] - r) * f, gg + (ff[2] - gg) * f, b + (ff[3] - b) * f
  local over, k = nil, 0
  if biome == B_DESERT then over, k = C.desert, 0.9
  elseif biome == B_TUNDRA then over, k = C.tundra, 0.7
  elseif biome == B_MARSH then over, k = C.marsh, 0.6
  elseif biome == B_FARM then
    local crop = F.crop[c]
    over, k = { C.farm_young[1] + (C.farm[1] - C.farm_young[1]) * crop,
      C.farm_young[2] + (C.farm[2] - C.farm_young[2]) * crop,
      C.farm_young[3] + (C.farm[3] - C.farm_young[3]) * crop }, 0.75
  elseif biome == B_VILLAGE then over, k = C.village, 0.55
  elseif biome == B_RUINS then over, k = C.ruins, 0.5
  elseif biome == 14 then over, k = C.barren, 0.6
  elseif biome == B_ICE_SHEET or biome == B_SEA_ICE then over, k = C.tundra, 0.3
  end
  if over then r, gg, b = r + (over[1] - r) * k, gg + (over[2] - gg) * k, b + (over[3] - b) * k end
  -- burn scars fade back to green over scar_expire ticks
  local ash = 0
  local scar = F.burn_scar[c]
  if F.fire[c] > 0 then ash = 1
  elseif scar > 0 then ash = clamp01(1 - scar / 240) * 0.85 end
  if biome == B_ASH then ash = max(ash, 0.8) end
  if ash > 0 then r, gg, b = r + (C.ash[1] - r) * ash, gg + (C.ash[2] - gg) * ash, b + (C.ash[3] - b) * ash end
  out[base + 1], out[base + 2], out[base + 3] = byte(r), byte(gg), byte(b)
  out[base + 4] = byte(F.ice[c])
  out[base + 5] = byte(F.fire[c])
  out[base + 6] = byte(ash)
  local lu = F.land_use[c]
  out[base + 7] = lu > 0 and byte(F.crop[c]) or 0
  out[base + 8] = lu < 0 and 255 or 0
end

return M
