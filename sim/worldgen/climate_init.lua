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

return ci
