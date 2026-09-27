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
  -- natural land = land minus villages, farms and ruins (the §10 vegetation bands, D-025)
  local nat = L - counts[4] - counts[5] - counts[6]
  if nat <= 0 then nat = 1 end
  m.human_land = (counts[4] + counts[5] + counts[6]) / L
  m.forest_nat = (counts[12] + counts[11]) / nat
  m.grass_nat = counts[13] / nat
  m.desert_nat = counts[9] / nat
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
  -- animals (mean density over land; per continent c1..c3) and ocean life (mean over ocean)
  local cont = geo.continent
  local gz, pd = F.grazers, F.predators
  local cg, cp, ca = { 0, 0, 0 }, { 0, 0, 0 }, { 0, 0, 0 }
  local gs, ps = 0, 0
  local land_cells = geo.land_cells
  for i = 1, #land_cells do
    local c = land_cells[i]
    local a = area[c]
    gs = gs + gz[c] * a
    ps = ps + pd[c] * a
    local k = cont[c]
    if k >= 1 and k <= 3 then cg[k] = cg[k] + gz[c] * a; cp[k] = cp[k] + pd[c] * a; ca[k] = ca[k] + a end
  end
  m.grazers, m.predators = gs / L, ps / L
  for k = 1, 3 do
    m["grazers_c" .. k] = ca[k] > 0 and cg[k] / ca[k] or 0
    m["predators_c" .. k] = ca[k] > 0 and cp[k] / ca[k] or 0
  end
  local fs, pls, oa, ot = 0, 0, 0, 0
  local oil = F.oil
  for c = 1, n do
    if ocean[c] == 1 then
      local a = area[c]
      fs = fs + F.fish[c] * a
      pls = pls + F.plankton[c] * a
      oa = oa + a
    end
    ot = ot + oil[c] * area[c]
  end
  m.fish = oa > 0 and fs / oa or 0
  m.plankton = oa > 0 and pls / oa or 0
  m.oil = ot
  m.oil_cells = W.S.ocean and W.S.ocean.oil_n or 0
  -- people and villages
  local vs = W.ent.villages or {}
  local pop, alive, fsum = 0, 0, 0
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      alive = alive + 1
      pop = pop + v.pop
      fsum = fsum + v.food_security
    end
  end
  m.population, m.villages = pop, alive
  m.food_security = alive > 0 and fsum / alive or 0
  local SV = W.S.society
  m.villages_founded = SV and SV.founded or 0
  m.villages_abandoned = SV and SV.abandoned or 0
  m.headlines = W.E.headline_count or 0
  m.headline_rate = m.headlines / math.max(1, m.year)
  local ships = W.ent.ships or {}
  local t = 0
  for i = 1, #ships do if ships[i].alive then t = t + 1 end end
  m.tankers = t
  -- extension point: systems may add metrics
  for _, fn in ipairs(metrics.extra) do fn(W, m, area, tot, land) end
  return m
end

metrics.extra = {}

metrics.SERIES_COLS = { "year", "tick", "temp_mean", "precip_mean", "land_precip", "soil_mean",
  "ice_fraction", "g_temp", "co2", "aerosol", "sea_level", "forest_cover", "grassland", "desert",
  "tundra", "barren", "ice_sheet", "marsh", "grass_mean", "forest_mean", "fires_burning",
  "grazers", "predators", "grazers_c1", "predators_c1", "grazers_c2", "predators_c2",
  "grazers_c3", "predators_c3", "fish", "plankton", "oil", "oil_cells",
  "population", "villages", "villages_founded", "villages_abandoned", "food_security", "headlines",
  "headline_rate", "tankers", "human_land", "forest_nat", "grass_nat", "desert_nat" }

return metrics
