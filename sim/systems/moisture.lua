-- Wind tables + moisture/evaporation/advection/precipitation (02_WORLD_MODEL.md §5.2–5.3).
--
-- Wind: prevailing bands by latitude (trades / westerlies / polar easterlies) shifting ±5°
-- with the season, precomputed per season as semi-Lagrangian upwind weights on the graph
-- (self weight + up to 6 neighbour weights incl. a little diffusion), plus the weighted
-- upwind elevation (orographic lift) and a capacity multiplier (Hadley subsidence / ITCZ).
-- Moisture reads last tick's values (prev buffer). Flows carry the source cell's tag.

local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "moisture" }
local sin, cos, abs, sqrt, exp, max, min = math.sin, math.cos, math.abs, math.sqrt, math.exp, math.max, math.min
local rad = math.pi / 180

-- piecewise-linear profile over |lat| in degrees
local function prof(tbl, a)
  if a <= tbl[1][1] then return tbl[1][2] end
  for i = 2, #tbl do
    if a <= tbl[i][1] then
      local p, q = tbl[i - 1], tbl[i]
      return p[2] + (q[2] - p[2]) * (a - p[1]) / (q[1] - p[1])
    end
  end
  return tbl[#tbl][2]
end

-- Wind vector (3D tangent) at cell c for season s (0 spring .. 3 winter, northern labels).
function sys.wind_at(W, c, s)
  local cfg = cfgmod.MOIST
  local g = W.grid
  local lat, lon = g.lat[c], g.lon[c]
  local shift = cfg.band_shift[s + 1] * rad        -- bands follow the sun (north summer: +)
  local ld = (lat - shift) / rad
  local a = abs(ld)
  local sgn = ld >= 0 and 1 or -1
  local u = prof(cfg.zonal, a)                      -- eastward
  local vp = prof(cfg.merid, a)                     -- poleward
  local vn = vp * sgn                               -- northward
  local ex, ey, ez = -sin(lon), 0, cos(lon)
  local nx, ny, nz = -sin(lat) * cos(lon), cos(lat), -sin(lat) * sin(lon)
  return u * ex + vn * nx, u * ey + vn * ny, u * ez + vn * nz, u, vn
end

function sys.rebuild(W)
  local cfg = cfgmod.MOIST
  local n, g = W.n, W.grid
  local X, Y, Z = g.X, g.Y, g.Z
  local nbr, cnt = g.nbr, g.nbr_count
  local elev, sea = W.static.elev, W.G.sea_level
  local geo = W.geo
  local wt = W.wind or {}
  W.wind = wt
  for s = 0, 3 do
    local t = wt[s + 1] or {}
    wt[s + 1] = t
    t.w = t.w or {}          -- neighbour weights (stride 6)
    t.self = t.self or {}
    t.eup = t.eup or {}      -- upwind land elevation (orographic)
    t.cap = t.cap or {}      -- capacity multiplier
    t.speed = t.speed or {}
    for c = 1, n do
      local wx, wy, wz = sys.wind_at(W, c, s)
      local sp = sqrt(wx * wx + wy * wy + wz * wz)
      local b = c * 6 - 6
      local k_n = cnt[c]
      local tot = 0
      for k = 1, 6 do t.w[b + k] = 0 end
      if sp > 1e-6 then
        for k = 1, k_n do
          local j = nbr[b + k]
          local dx, dy, dz = X[j] - X[c], Y[j] - Y[c], Z[j] - Z[c]
          local dl = sqrt(dx * dx + dy * dy + dz * dz)
          local dot = -(dx * wx + dy * wy + dz * wz) / (dl * sp)   -- >0: neighbour is upwind
          if dot > 0.2 then
            local w = dot * dot
            t.w[b + k] = w
            tot = tot + w
          end
        end
      end
      local ss = W.sscale
      local alpha = min(cfg.courant_max, sp * cfg.wind_speed / ss)
      if tot <= 0 then alpha = 0 end
      local kd = cfg.diffusion / (ss * ss)
      local eup = 0
      for k = 1, k_n do
        local j = nbr[b + k]
        local up = tot > 0 and t.w[b + k] / tot or 0
        local ej = geo.ocean[j] == 1 and sea or elev[j]
        eup = eup + up * ej
        t.w[b + k] = alpha * up + kd / k_n
      end
      t.self[c] = 1 - alpha - kd
      if tot <= 0 then eup = geo.ocean[c] == 1 and sea or elev[c] end
      t.eup[c] = eup
      t.speed[c] = sp
      -- Hadley cell: subsidence dries the subtropics, convergence wets the ITCZ
      local shift = cfg.band_shift[s + 1]
      local a = abs(g.lat[c] / rad - shift)
      t.cap[c] = prof(cfg.cap_profile, a)
    end
  end
end

function sys.init(W)
  local sc = W.scratch or {}
  W.scratch = sc
  sc.evap_land = sc.evap_land or fields.filled(W.n, 0)
end

function sys.update(W)
  local cfg = cfgmod.MOIST
  local n, g = W.n, W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I, P = W.F, W.C, W.I, W.P
  fields.snapshot(W, "moisture")
  local m, pm = F.moisture, P.moisture
  local precip, temp, sst, ice, soil = F.precip, F.temp, F.sst, F.ice, F.soil_water
  local grass, forest = F.grass, F.forest
  local cm, im = C.moisture, I.moisture
  local cp, ip = C.precip, I.precip
  local csst, csoil, ctemp = C.sst, C.soil_water, C.temp
  local elev, sea = W.static.elev, W.G.sea_level
  local ocean = W.geo.ocean
  local season = math.floor((W.tick % 48) / 12)
  local t = W.wind[season + 1]
  local wv, wself, eup, capm = t.w, t.self, t.eup, t.cap
  local evl = W.scratch.evap_land
  local k_eo, k_el, k_cap, k_oro = cfg.k_eo, cfg.k_el, cfg.k_cap, cfg.k_oro
  local k_rain, k_drizzle = cfg.k_rain, cfg.k_drizzle
  local eps, hold = 0.002, 12
  local share = cfg.inflow_share
  local force = W.S.weather_force   -- sparse player/test forcing (Chapter 2), may be nil
  for c = 1, n do
    local b = c * 6 - 6
    local k_n = cnt[c]
    -- advection (+diffusion) from last tick; track the dominant tagged inflow
    local old = pm[c]
    local inflow, tot_in, best_in, tag_in = 0, 0, 0, 0
    for k = 1, k_n do
      local j = nbr[b + k]
      local w = wv[b + k]
      if w > 0 then
        local contrib = w * pm[j]
        inflow = inflow + contrib
        local tj = cm[j]
        if tj ~= 0 and contrib > best_in then best_in, tag_in = contrib, tj end
      end
    end
    if tag_in ~= 0 and best_in < share * inflow then tag_in = 0 end
    local mv = wself[c] * old + inflow
    -- evaporation
    local e, etag = 0, 0
    local tc = temp[c]
    if ocean[c] == 1 then
      local f = exp(0.07 * (sst[c] - 15))
      if f < 0.1 then f = 0.1 elseif f > 4 then f = 4 end
      e = k_eo * f * (1 - ice[c])
      etag = csst[c]
      evl[c] = 0
    else
      local veg = grass[c] + forest[c]
      if veg > 1 then veg = 1 end
      local tf = (tc + 5) / 30
      if tf < 0 then tf = 0 elseif tf > 1.5 then tf = 1.5 end
      e = k_el * soil[c] * (0.3 + 0.7 * veg) * tf
      evl[c] = e
      etag = csoil[c]
    end
    mv = mv + e
    -- precipitation: excess over capacity rains out; orographic lift lowers capacity
    local h = (ocean[c] == 1) and sea or elev[c]
    local lift = h - eup[c]
    if lift < 0 then lift = 0 end
    local lf = 1 - k_oro * lift
    if lf < 0.15 then lf = 0.15 end
    local cap = k_cap * exp(0.06 * tc) * capm[c] * lf
    local ex = mv - cap
    local p = mv * k_drizzle
    if ex > 0 then p = p + ex * k_rain end
    local ptag = 0
    if force then
      local fk = force[c]
      if fk then
        -- fk = {mult=, add=, tag=}; forcing is applied to rain-out and moisture
        p = p * fk.mult + fk.add
        ptag = fk.tag
      end
    end
    if p > mv then p = mv end
    if p < 0 then p = 0 end
    mv = mv - p
    m[c] = mv
    precip[c] = p
    -- blame
    local cur = cm[c]
    if cur ~= 0 or tag_in ~= 0 or etag ~= 0 then
      local adv = inflow - (1 - wself[c]) * old
      blame.step(cm, im, c, mv - old, eps, hold, adv, tag_in, e, etag, -p, ptag, 0, 0)
    end
    local pc = cp[c]
    if ptag ~= 0 then
      blame.set(cp, ip, c, ptag)
    elseif pc ~= 0 or cm[c] ~= 0 or ctemp[c] ~= 0 then
      local nt = cm[c]
      if nt == 0 then nt = ctemp[c] end
      if nt ~= 0 then blame.set(cp, ip, c, nt)
      else
        local i = ip[c] + 1
        if i >= hold then cp[c] = 0; ip[c] = 0 else ip[c] = i end
      end
    end
  end
end

return sys
