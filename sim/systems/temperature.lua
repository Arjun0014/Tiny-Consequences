-- Temperature (02_WORLD_MODEL.md §5.1): temp, sst, heat_anom decay. Every tick.
--
-- temp = T_base(lat) + season*amp(lat) + lapse*max(elev-sea,0) + g_temp + heat_anom
--        - k_aer*aerosol + wobble
-- Blame: temp is a formula field, so its tag names the largest *anomaly* component
-- (heat_anom / g_temp / aerosol) when that component exceeds anom_min °C; else baseline.
-- sst lags the ocean air temperature; heat_anom's cause travels into sst.

local cfgmod = require("sim.config")
local noise = require("sim.worldgen.noise")
local rng = require("sim.rng")
local blame = require("sim.causality.blame")
local ledger = require("sim.ledger")

local sys = { name = "temperature" }
local sin, abs, pi, floor, exp = math.sin, math.abs, math.pi, math.floor, math.exp

-- Static per-cell tables (derived): base temperature, seasonal amplitude, hemisphere sign,
-- elevation lapse, wobble basis patterns.
function sys.rebuild(W)
  local cfg = cfgmod.TEMP
  local n, g = W.n, W.grid
  local geo = W.geo
  local elev, sea = W.static.elev, W.G.sea_level
  local T = W.temp_static or {}
  W.temp_static = T
  T.base, T.amp, T.hemi, T.lapse = T.base or {}, T.amp or {}, T.hemi or {}, T.lapse or {}
  local nb = cfg.wobble_patterns
  T.wob = T.wob or {}
  for k = 1, nb do T.wob[k] = T.wob[k] or {} end
  for c = 1, n do
    local lat = g.lat[c]
    local s = sin(lat)
    T.base[c] = cfg.t_equator - cfg.t_pole_drop * s * s
    local amp = cfg.season_amp * abs(s)
    if geo.ocean[c] == 1 then
      amp = amp * cfg.ocean_amp_factor
    elseif geo.coast[c] == 1 then
      amp = amp * (1 - cfg.coastal_damping)
    end
    T.amp[c] = amp
    T.hemi[c] = lat >= 0 and 1 or -1
    local h = elev[c] - sea
    T.lapse[c] = h > 0 and cfg.lapse * h or 0
    for k = 1, nb do
      T.wob[k][c] = noise.fbm(W.seed, 900 + k * 13, g.X[c], g.Y[c], g.Z[c], 1.6, 2, 0.5) * 2
    end
  end
end

function sys.init(W)
  W.scratch = W.scratch or {}
end

-- Wobble coefficient k for year y (deterministic, smooth across the year).
local function wob_coef(W, k, y)
  return rng.float(W.seed, rng.S.WEATHER_WOBBLE, y + 10, k, 0) * 2 - 1
end

function sys.update(W)
  local cfg = cfgmod.TEMP
  local n = W.n
  local F, C, I = W.F, W.C, W.I
  local temp, sst, ha = F.temp, F.sst, F.heat_anom
  local ctemp, itemp = C.temp, I.temp
  local csst, isst = C.sst, I.sst
  local cha = C.heat_anom
  local T = W.temp_static
  local base, amp, hemi, lapse = T.base, T.amp, T.hemi, T.lapse
  local ocean = W.geo.ocean
  local G = W.G
  local tick = W.tick
  local toy = tick % 48
  local season = sin(2 * pi * toy / 48 + cfg.season_phase)
  local gt = G.g_temp
  local aer = cfg.k_aer * G.aerosol
  local offset = gt - aer
  -- global tags (ledger top contributors), resolved once per tick
  local tag_g = ledger.dominant_tag(W, "g_temp")
  local tag_aer = ledger.dominant_tag(W, "aerosol")
  local anom_min = cfg.anom_min
  -- wobble: interpolate yearly coefficients
  local y = floor(tick / 48)
  local f = toy / 48
  local wob = T.wob
  local nb = cfg.wobble_patterns
  local wa = W.scratch.wob_a or {}
  W.scratch.wob_a = wa
  for k = 1, nb do
    local a0, a1 = wob_coef(W, k, y), wob_coef(W, k, y + 1)
    wa[k] = (a0 + (a1 - a0) * f) * cfg.wobble_amp
  end
  local w1, w2, w3 = wob[1], wob[2], wob[3]
  local a1, a2, a3 = wa[1], wa[2], wa[3]
  local ha_land, ha_ocean = cfg.heat_anom_decay_land, cfg.heat_anom_decay_ocean
  local sst_rate, sst_min = cfg.sst_rate, cfg.sst_min
  local tau_eps, hold = 1e-4, 48
  for c = 1, n do
    local isoc = ocean[c] == 1
    -- heat anomaly decay
    local h = ha[c]
    if h ~= 0 then
      h = h * (isoc and ha_ocean or ha_land)
      if h < 0.01 and h > -0.01 then h = 0 end
      ha[c] = h
    end
    local wobble = a1 * w1[c] + a2 * w2[c] + a3 * w3[c]
    local t = base[c] + season * hemi[c] * amp[c] + lapse[c] + offset + h + wobble
    temp[c] = t
    -- blame (anomaly attribution): cheap fast path when nothing is tagged
    local th = cha[c]
    if th ~= 0 or tag_g ~= 0 or tag_aer ~= 0 or ctemp[c] ~= 0 then
      local mh, mg, ma = abs(h), abs(gt), aer
      local best, bt = anom_min, 0
      if mh > best then best, bt = mh, th end
      if mg > best then best, bt = mg, tag_g end
      if ma > best then best, bt = ma, tag_aer end
      if bt ~= 0 then blame.set(ctemp, itemp, c, bt)
      elseif ctemp[c] ~= 0 then
        local i = itemp[c] + 1
        if i >= hold then ctemp[c] = 0; itemp[c] = 0 else itemp[c] = i end
      end
    end
    -- sea-surface temperature lags the air over the ocean
    if isoc then
      local old = sst[c]
      local target = t
      if target < sst_min then target = sst_min end
      local s = old + (target - old) * sst_rate
      if s < sst_min then s = sst_min end
      sst[c] = s
      if th ~= 0 or csst[c] ~= 0 or ctemp[c] ~= 0 then
        -- driver split: local heat anomaly (its own tag) vs global offset (temp's tag)
        local d = s - old
        local sh = abs(h) / (abs(h) + abs(offset) + 1e-9)
        blame.step(csst, isst, c, d, tau_eps, cfg.sst_hold, d * sh, th, d * (1 - sh), ctemp[c], 0, 0, 0, 0)
      end
    end
  end
end

return sys
