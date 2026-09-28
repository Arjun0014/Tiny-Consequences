-- Initial field values before spin-up: a rough latitude/elevation climatology so the 30-year
-- spin-up converges quickly. Spin-up (not this) produces the lived-in equilibrium.
local cfgmod = require("sim.config")

local ci = {}

function ci.apply(W)
  local cfg = cfgmod.TEMP
  local F, n = W.F, W.n
  local g, geo = W.grid, W.geo
  local elev, sea = W.static.elev, W.G.sea_level
  for c = 1, n do
    local s = math.sin(g.lat[c])
    local t = cfg.t_equator - cfg.t_pole_drop * s * s
    local h = elev[c] - sea
    if h > 0 then t = t + cfg.lapse * h end
    F.temp[c] = t
    F.moisture[c] = 1
    W.P.moisture[c] = 1
    if geo.ocean[c] == 1 then
      F.sst[c] = math.max(cfg.sst_min, t)
      F.ice[c] = t < -6 and 1 or 0
      F.plankton[c] = cfgmod.OCEAN.init_plankton
      F.fish[c] = cfgmod.OCEAN.init_fish
      W.P.fish[c] = F.fish[c]
      if W.S.ocean then W.S.ocean.fish_ref[c] = F.fish[c] end
    else
      F.soil_water[c] = 0.4
      F.ice[c] = t < -4 and 1 or 0
      F.fertility[c] = 0.5
    end
  end
end

-- After the climate has settled (a few spin-up years), seed vegetation near its equilibrium so
-- forests don't need a century to appear: forest where warm and wet, grass elsewhere habitable.
function ci.vegetation(W)
  local F, n = W.F, W.n
  local ocean = W.geo.ocean
  local T = W.temp_static
  for c = 1, n do
    if ocean[c] == 0 then
      local tm = T.base[c] + T.lapse[c]
      local s = F.soil_water[c]
      local warm = (tm > -2) and math.min(1, (tm + 2) / 10) or 0
      local wet = math.max(0, math.min(1, (s - 0.2) / 0.4))
      F.forest[c] = 0.75 * warm * wet
      F.grass[c] = math.min(1, 0.6 * warm * math.max(0.15, math.min(1, s / 0.3)))
      F.fertility[c] = 0.3 + 0.4 * wet
      W.P.grass[c], W.P.forest[c] = F.grass[c], F.forest[c]
    end
  end
end

-- Seed land animals once vegetation exists (spin-up; never later): grazers and predators on the
-- continents only — animals can't swim, so small islands start untouched (no grazers, no
-- predators) until something walks over sea ice or the player introduces them.
function ci.fauna(W)
  local cfg = cfgmod.FAUNA
  local F, n = W.F, W.n
  local geo = W.geo
  local ocean, cont = geo.ocean, geo.continent
  local min_frac = cfgmod.WORLDGEN.continent_min_frac * 0.9
  for c = 1, n do
    if ocean[c] == 0 then
      local gr = F.grass[c]
      local comp = geo.components[cont[c]]
      if comp and comp.size / n >= min_frac then
        F.grazers[c] = cfg.init_grazers * math.min(1, gr / 0.4)
        F.predators[c] = gr > 0.05 and cfg.init_predators or 0
      else
        F.grazers[c], F.predators[c] = 0, 0
      end
      local S = W.S.fauna
      if S then S.hnorm[c], S.pnorm[c] = F.grazers[c], F.predators[c] end
    end
  end
  -- the animals live in herds and packs (W4, D-070): the densities gather into groups
  require("sim.systems.fauna").seed(W)
end

return ci
