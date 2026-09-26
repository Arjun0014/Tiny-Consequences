-- Biome classification with hysteresis and dwell (02_WORLD_MODEL.md §5.15).
-- Candidate by priority; commit after the candidate persists for its dwell time; exit
-- thresholds are looser than entry thresholds so states stick. Committed changes emit
-- biome_shift with the tag of the field that drove the candidate.
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local blame = require("sim.causality.blame")

local sys = { name = "biome" }

local B = {
  ocean = 1, sea_ice = 2, ice_sheet = 3, village = 4, farmland = 5, ruins = 6, ash = 7,
  marsh = 8, desert = 9, tundra = 10, jungle = 11, forest = 12, grassland = 13, barren = 14,
}
sys.B = B
sys.NAMES = {}
for k, v in pairs(B) do sys.NAMES[v] = k end

-- Classify cell c given its current committed biome `cur` (for hysteresis). Returns biome,
-- driver field name (for blame).
function sys.classify(W, c, cur)
  local cfg = cfgmod.BIOME
  local F = W.F
  local ice = F.ice[c]
  if W.geo.ocean[c] == 1 then
    if ice >= (cur == B.sea_ice and cfg.sea_ice_exit or cfg.sea_ice_enter) then return B.sea_ice, "ice" end
    return B.ocean, "ice"
  end
  if ice >= (cur == B.ice_sheet and cfg.ice_sheet_exit or cfg.ice_sheet_enter) then return B.ice_sheet, "ice" end
  local lu = F.land_use[c]
  if lu < 0 then return B.village, nil end
  if lu > 0 then return B.farmland, nil end
  if W.S.ruins and W.S.ruins[c] then return B.ruins, nil end
  local scar = F.burn_scar[c]
  if F.fire[c] > 0 or (scar > 0 and scar < cfg.ash_ticks) then return B.ash, "fire" end
  local grass, forest, soil = F.grass[c], F.forest[c], F.soil_water[c]
  local veg = grass + forest
  if veg > 1 then veg = 1 end
  local elev = W.static.elev[c] - W.G.sea_level
  if soil >= (cur == B.marsh and cfg.marsh_exit or cfg.marsh_enter) and elev < cfg.marsh_max_elev then
    return B.marsh, "soil_water"
  end
  local T = W.temp_static
  local tmean = T.base[c] + T.lapse[c] + W.G.g_temp
  if cur == B.desert then
    if veg < cfg.desert_exit_veg and soil < cfg.desert_exit_soil then return B.desert, "grass" end
  elseif soil < cfg.desert_soil and veg < cfg.desert_veg and tmean > cfg.desert_min_temp then
    return B.desert, "grass"
  end
  if tmean < cfg.tundra_temp and veg < (cur == B.tundra and cfg.tundra_exit_veg or cfg.tundra_veg) then
    return B.tundra, "temp"
  end
  local fthr = cur == B.forest and cfg.forest_exit or cfg.forest_enter
  local jthr = cur == B.jungle and cfg.jungle_exit or cfg.jungle_enter
  if forest >= jthr and tmean >= cfg.jungle_temp and soil >= cfg.jungle_soil then return B.jungle, "forest" end
  if forest >= fthr then return B.forest, "forest" end
  if grass >= (cur == B.grassland and cfg.grass_exit or cfg.grass_enter) then return B.grassland, "grass" end
  return B.barren, "grass"
end

function sys.update(W)
  local cfg = cfgmod.BIOME
  local n = W.n
  local F, C = W.F, W.C
  local biome, cand, dwell = F.biome, F.biome_cand, F.biome_dwell
  local classify = sys.classify
  local dw_default, dw_long = cfg.dwell, cfg.dwell_long
  local rec = W.record_events
  for c = 1, n do
    local cur = biome[c]
    local b, driver = classify(W, c, cur)
    if b == cur then
      dwell[c] = 0
      cand[c] = cur
    else
      if b == cand[c] then dwell[c] = dwell[c] + 1 else cand[c] = b; dwell[c] = 1 end
      local need = (b == B.desert or b == B.ice_sheet) and dw_long or dw_default
      if b == B.village or b == B.farmland or b == B.ash or b == B.ocean or cur == B.ocean then need = 1 end
      if dwell[c] >= need then
        biome[c] = b
        dwell[c] = 0
        if rec and cur ~= 0 and sys.notable(cur, b) then
          local tag = 0
          if driver then
            local arr = C[driver]
            tag = arr and arr[c] or 0
            -- desert/barren/grassland shifts driven by soil when vegetation carries no tag
            if tag == 0 and (b == B.desert or b == B.barren) then tag = C.soil_water[c] end
            if tag == 0 and driver == "forest" then tag = C.grass[c] end
            if tag == 0 and (cur == B.forest or cur == B.jungle) then tag = C.forest[c] end
          end
          local id = events.emit(W, "biome_shift", c, tag, 1, { from = cur, to = b })
          -- re-tag: downstream effects of the new landscape carry the shift
          if id ~= 0 and tag ~= 0 and driver and C[driver] then blame.set(C[driver], W.I[driver], c, id) end
        end
      end
    end
  end
end

-- Only natural landscape transitions make stories (farms/villages/ash come with their own).
function sys.notable(from, to)
  if to == B.farmland or to == B.village or to == B.ash or from == B.ash then return false end
  if from == B.farmland or from == B.village then return false end
  if to == B.ocean or from == B.ocean then return false end
  return true
end

-- Initialise committed biomes from the current state (after spin-up start).
function sys.reset(W)
  for c = 1, W.n do
    local b = sys.classify(W, c, 0)
    W.F.biome[c], W.F.biome_cand[c], W.F.biome_dwell[c] = b, b, 0
  end
end

return sys
