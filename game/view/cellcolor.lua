-- Per-sim-cell display values for the terrain data texture (Stage 1.8; renderer v2, D-048).
-- Pure Lua 5.1. Each cell packs four RGBA8 texels (rows y .. y+3 of the texture):
--   land cell : A = ground colour rgb + snow cover
--               B = fire, ash (burn scar), dryness (drought), grass
--               C = farm, crop stage, village, forest
--               D = temperature ((t + 30) / 70), soil water, blight, 0   (props: seasons, snow)
--   ocean cell: A = 0,0,0 + sea ice
--               B = 0, oil, plankton, fish
--               C = 0, 0, 0, 0
--               D = temperature, 0, 0, 0
-- The terrain vertex shader blends these over the 3 sim cells of each render vertex, so a
-- colour update costs O(cells), never O(render vertices). Seasonal tints (spring flush, dry
-- summer, autumn straw) come from the cell's own temperature and soil water here.
local palette = require("content.palette")

local M = {}
M.TEXELS = 16        -- bytes per cell

local floor, min, max = math.floor, math.min, math.max

local function col(hex)
  local r, g, b = palette.rgb(hex)
  return { r / 255, g / 255, b / 255 }
end

-- Ground colours (the trees on top supply the dark forest greens).
local C = {
  meadow = col("#86C653"), meadow_lush = col("#6DBB4A"), meadow_warm = col("#AFCB58"),
  straw = col("#C9BE7A"), dry = col("#D8C58A"), bare = col("#C9B892"),
  forest_floor = col("#4F9A3E"), jungle_floor = col("#357F38"), tundra = col("#AEBB94"),
  desert = col("#E9C47E"), marsh = col("#5E8F5C"), farm = col("#B8A064"),
  village = col("#BFA27E"), ash = col("#4E4A48"), ruins = col("#8A7F74"), barren = col("#B9A787"),
}
M.COLORS = C
local STRAW_R, STRAW_G, STRAW_B = C.straw[1], C.straw[2], C.straw[3]
local MW_R, MW_G, MW_B = C.meadow_warm[1], C.meadow_warm[2], C.meadow_warm[3]
local MD_R, MD_G, MD_B = C.meadow[1], C.meadow[2], C.meadow[3]
local ML_R, ML_G, ML_B = C.meadow_lush[1], C.meadow_lush[2], C.meadow_lush[3]

local function clamp01(x) if x < 0 then return 0 elseif x > 1 then return 1 end return x end

local function byte(x)
  x = floor(x * 255 + 0.5)
  if x < 0 then return 0 elseif x > 255 then return 255 end
  return x
end

-- Biome ids (sim/systems/biome.lua)
local B_SEA_ICE, B_ICE_SHEET, B_VILLAGE, B_FARM, B_RUINS, B_ASH = 2, 3, 4, 5, 6, 7
local B_MARSH, B_DESERT, B_TUNDRA, B_JUNGLE = 8, 9, 10, 11

local function mixc(r, g, b, c, k)
  return r + (c[1] - r) * k, g + (c[2] - g) * k, b + (c[3] - b) * k
end

--- Computes the 16 bytes of cell c into out[base+1..base+16].
function M.cell(W, c, out, base)
  local F = W.F
  if W.geo.ocean[c] == 1 then
    out[base + 1], out[base + 2], out[base + 3] = 0, 0, 0
    out[base + 4] = byte(F.ice[c])
    out[base + 5] = 0
    out[base + 6] = byte(min(1, F.oil[c] * 3))
    out[base + 7] = byte(F.plankton[c])
    out[base + 8] = byte(F.fish[c])
    out[base + 9], out[base + 10], out[base + 11], out[base + 12] = 0, 0, 0, 0
    out[base + 13] = byte((F.temp[c] + 30) / 70)
    out[base + 14], out[base + 15], out[base + 16] = 0, 0, 0
    return
  end
  local biome = F.biome[c]
  local grass, forest = F.grass[c], F.forest[c]
  local soil = F.soil_water[c]
  local temp = F.temp[c]
  -- meadow by season: lush when warm and wet, straw when cold or dry
  local warmth = clamp01((temp - 2) / 14)
  local wet = clamp01(soil * 1.8)
  local r, gg, b = STRAW_R, STRAW_G, STRAW_B
  r, gg, b = r + (MW_R - r) * warmth, gg + (MW_G - gg) * warmth, b + (MW_B - b) * warmth
  local k1 = warmth * wet
  r, gg, b = r + (MD_R - r) * k1, gg + (MD_G - gg) * k1, b + (MD_B - b) * k1
  local k2 = k1 * clamp01(grass * 1.5 - 0.3)
  r, gg, b = r + (ML_R - r) * k2, gg + (ML_G - gg) * k2, b + (ML_B - b) * k2
  -- thin grass shows the soil
  local g = clamp01(grass * 1.9)
  r, gg, b = C.dry[1] + (r - C.dry[1]) * g, C.dry[2] + (gg - C.dry[2]) * g, C.dry[3] + (b - C.dry[3]) * g
  local ff = biome == B_JUNGLE and C.jungle_floor or C.forest_floor
  r, gg, b = mixc(r, gg, b, ff, clamp01(forest * 1.3))
  local over, k = nil, 0
  if biome == B_DESERT then over, k = C.desert, 0.9
  elseif biome == B_TUNDRA then over, k = C.tundra, 0.7
  elseif biome == B_MARSH then over, k = C.marsh, 0.6
  elseif biome == B_FARM then over, k = C.farm, 0.35
  elseif biome == B_VILLAGE then over, k = C.village, 0.35
  elseif biome == B_RUINS then over, k = C.ruins, 0.45
  elseif biome == 14 then over, k = C.barren, 0.6
  elseif biome == B_ICE_SHEET or biome == B_SEA_ICE then over, k = C.tundra, 0.3
  end
  if over then r, gg, b = mixc(r, gg, b, over, k) end
  -- burn scars fade back to green over scar_expire ticks
  local ash = 0
  local scar = F.burn_scar[c]
  if F.fire[c] > 0 then ash = 1
  elseif scar > 0 then ash = clamp01(1 - scar / 240) * 0.85 end
  if biome == B_ASH then ash = max(ash, 0.8) end
  if ash > 0 then r, gg, b = mixc(r, gg, b, C.ash, ash) end
  -- drought: the detector's dry-month counter browns the land
  local dry = 0
  local DS = W.D and W.D.soil
  if DS then
    if DS.on[c] == 1 then dry = 1 else dry = clamp01(DS.dry[c] / 3) * 0.6 end
  end
  if dry > 0 then r, gg, b = mixc(r, gg, b, C.dry, dry * 0.55) end
  out[base + 1], out[base + 2], out[base + 3] = byte(r), byte(gg), byte(b)
  out[base + 4] = byte(F.ice[c])
  out[base + 5] = byte(F.fire[c])
  out[base + 6] = byte(ash)
  out[base + 7] = byte(dry)
  out[base + 8] = byte(clamp01(grass))
  local lu = F.land_use[c]
  out[base + 9] = lu > 0 and 255 or 0
  out[base + 10] = lu > 0 and byte(F.crop[c]) or 0
  out[base + 11] = (lu < 0 or biome == B_VILLAGE) and 255 or (biome == B_RUINS and 110 or 0)
  out[base + 12] = byte(clamp01(forest))
  out[base + 13] = byte((temp + 30) / 70)
  out[base + 14] = byte(clamp01(soil))
  out[base + 15] = M.blighted and M.blighted[c] and 255 or 0
  out[base + 16] = 0
end

--- Marks infected farm cells (the Blight power's sparse set) for this recolour pass.
function M.prepare(W)
  local S = W.S and W.S.powers
  local b = M.blighted or {}
  for k in pairs(b) do b[k] = nil end
  if S and S.blight then
    for i = 1, #S.blight do b[S.blight[i].cell] = true end
  end
  M.blighted = b
end

return M
