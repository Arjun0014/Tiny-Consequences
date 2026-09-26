-- Tuning-band metrics (02_WORLD_MODEL.md §10), computed on demand and yearly into a series.
local metrics = {}

-- Returns a flat table of named numbers for the current state.
function metrics.compute(W)
  local F, n = W.F, W.n
  local area = W.grid.area
  local geo = W.geo
  local ocean = geo.ocean
  local m = {}
  local tot, land = 0, 0
  local tsum, psum, lsoil, lp = 0, 0, 0, 0
  local ice_a = 0
  for c = 1, n do
    local a = area[c]
    tot = tot + a
    tsum = tsum + a * F.temp[c]
    psum = psum + a * F.precip[c]
    ice_a = ice_a + a * F.ice[c]
    if ocean[c] == 0 then
      land = land + a
      lsoil = lsoil + a * F.soil_water[c]
      lp = lp + a * F.precip[c]
    end
  end
  m.land_frac = land / tot
  m.temp_mean = tsum / tot
  m.precip_mean = psum / tot
  m.land_precip = land > 0 and lp / land or 0
  m.soil_mean = land > 0 and lsoil / land or 0
  m.ice_fraction = ice_a / tot
  local G = W.G
  m.g_temp, m.co2, m.aerosol, m.sea_level = G.g_temp, G.co2, G.aerosol, G.sea_level
  m.year = math.floor(W.tick / 48)
  m.tick = W.tick
  -- biome shares of land
  local biome = F.biome
  local counts = {}
  for b = 1, 14 do counts[b] = 0 end
  for c = 1, n do
    if ocean[c] == 0 then counts[biome[c]] = counts[biome[c]] + area[c] end
  end
  local L = land > 0 and land or 1
  m.forest_cover = (counts[12] + counts[11]) / L
  m.grassland = counts[13] / L
  m.desert = counts[9] / L
  m.tundra = counts[10] / L
  m.barren = counts[14] / L
  m.ice_sheet = counts[3] / L
  m.marsh = counts[8] / L
  m.farmland = counts[5] / L
  m.ash = counts[7] / L
  local gsum, fsum = 0, 0
  for _, c in ipairs(geo.land_cells) do gsum = gsum + F.grass[c] * area[c]; fsum = fsum + F.forest[c] * area[c] end
  m.grass_mean, m.forest_mean = gsum / L, fsum / L
  m.fires_burning = W.S.fire and #W.S.fire.list or 0
  -- extension point: systems may add metrics
  for _, fn in ipairs(metrics.extra) do fn(W, m, area, tot, land) end
  return m
end

metrics.extra = {}

metrics.SERIES_COLS = { "year", "tick", "temp_mean", "precip_mean", "land_precip", "soil_mean",
  "ice_fraction", "g_temp", "co2", "aerosol", "sea_level", "forest_cover", "grassland", "desert",
  "tundra", "barren", "ice_sheet", "marsh", "grass_mean", "forest_mean", "fires_burning" }

return metrics
