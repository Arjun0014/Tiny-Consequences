-- Global climate (02_WORLD_MODEL.md §5.13). Yearly integration of co2, g_temp, sea level,
-- ice fraction, era; aerosol decays every tick. Sources (fires, villagers, clearing) are
-- accumulated during the year into G.co2_src by the systems that emit, and credited to the
-- ledger as they occur. Sinks: silicate weathering (thermostat), ocean uptake, forest growth.
--
-- Self-calibration: during worldgen spin-up (G.calibrating) co2 and g_temp are pinned and the
-- world's own reference weathering index, ice fraction, ice volume and sst are measured, so
-- every seed's untouched no-human world balances at co2 ≈ 280.
local cfgmod = require("sim.config")
local ledger = require("sim.ledger")
local events = require("sim.causality.events")

local sys = { name = "globalclimate" }
local exp, log, max, min = math.exp, math.log, math.max, math.min
local LN2 = log(2)

function sys.init(W)
  local G = W.G
  G.co2_src = G.co2_src or 0
  G.co2_forest = G.co2_forest or 0
  G.acc_weather = G.acc_weather or 0
  G.acc_samples = G.acc_samples or 0
  G.sst_mean = G.sst_mean or 15
end

-- Area-weighted measures (also used by metrics).
function sys.measure(W)
  local F, n = W.F, W.n
  local area = W.grid.area
  local ocean = W.geo.ocean
  local ice, sst = F.ice, F.sst
  local tot, ia, vol, ss, so = 0, 0, 0, 0, 0
  for c = 1, n do
    local a = area[c]
    tot = tot + a
    ia = ia + a * ice[c]
    if ocean[c] == 1 then ss = ss + a * sst[c]; so = so + a
    else vol = vol + a * ice[c] end
  end
  return ia / tot, vol, so > 0 and ss / so or 0
end

function sys.update(W)
  local cfg = cfgmod.CLIMATE
  local G = W.G
  if G.aerosol > 0 then
    G.aerosol = G.aerosol * cfg.aerosol_decay
    if G.aerosol < 1e-5 then G.aerosol = 0 end
  end
  -- sample the weathering index monthly
  if W.tick % 4 == 0 then
    local F = W.F
    local precip, temp, ice = F.precip, F.temp, F.ice
    local land = W.geo.land_cells
    local area = W.grid.area
    local s = 0
    for i = 1, #land do
      local c = land[i]
      if temp[c] > 0 then s = s + area[c] * precip[c] * exp(0.07 * min(temp[c], 40)) * (1 - ice[c]) end
    end
    G.acc_weather = G.acc_weather + s
    G.acc_samples = G.acc_samples + 1
  end
end

local ERA_ORDER = { "garden", "warm", "hothouse", "iceage", "snowball", "dead" }
sys.ERA_ORDER = ERA_ORDER

local function classify_era(W, cur)
  local cfg = cfgmod.CLIMATE
  local G = W.G
  local gt, ifr = G.g_temp, G.ice_fraction
  local h = cfg.era_hysteresis
  -- dead world is flagged by metrics/fauna (Chapter 3); keep if set
  if G.dead_world then return "dead" end
  local function within(name)
    if name == "snowball" then return ifr >= cfg.era_snowball - (cur == name and h.ice or 0) end
    if name == "iceage" then return ifr >= cfg.era_iceage - (cur == name and h.ice or 0) end
    if name == "hothouse" then return gt >= cfg.era_hothouse - (cur == name and h.temp or 0) end
    if name == "warm" then return gt >= cfg.era_warm - (cur == name and h.temp or 0) end
    return true
  end
  if within("snowball") then return "snowball" end
  if within("iceage") then return "iceage" end
  if within("hothouse") then return "hothouse" end
  if within("warm") then return "warm" end
  return "garden"
end

function sys.year(W)
  local cfg = cfgmod.CLIMATE
  local G = W.G
  local ifr, vol, sstm = sys.measure(W)
  G.ice_fraction, G.ice_volume, G.sst_mean = ifr, vol, sstm
  local wx = G.acc_samples > 0 and G.acc_weather / G.acc_samples or 0
  G.weather_index = wx
  if G.calibrating then
    local R = G.calib or { n = 0, ice = 0, vol = 0, sst = 0, wx = 0 }
    G.calib = R
    if W.tick >= 48 * cfg.calib_skip_years then
      R.n = R.n + 1
      R.ice, R.vol, R.sst, R.wx = R.ice + ifr, R.vol + vol, R.sst + sstm, R.wx + wx
      G.ice_ref, G.ice_volume_ref = R.ice / R.n, R.vol / R.n
      G.sst_ref, G.weather_ref = R.sst / R.n, R.wx / R.n
    end
    G.co2, G.g_temp, G.co2_ref = cfg.co2_base, 0, cfg.co2_base
  elseif W.tick > 0 then
    local V = cfg.outgassing
    local wref = G.weather_ref or wx
    local sink_w = (wref > 0) and V * wx / wref or V
    local eq = cfg.co2_base + cfg.k_eq_sst * (sstm - (G.sst_ref or sstm))
    local sink_o = cfg.k_ocean * (G.co2 - eq)
    local sink_f = G.co2_forest
    local src = G.co2_src + V
    ledger.add(W, "co2", V, 0, "volcanic")
    ledger.add(W, "co2", -(sink_w + sink_o + sink_f), 0, "sinks")
    G.co2 = max(cfg.co2_min, min(cfg.co2_max, G.co2 + src - sink_w - sink_o - sink_f))
    G.last_flux = { src = src, weathering = sink_w, ocean = sink_o, forest = sink_f }
    local target = cfg.sensitivity * log(G.co2 / cfg.co2_base) / LN2
      + cfg.k_albedo * ((G.ice_ref or ifr) - ifr)
      + cfg.k_vapor * max(0, G.g_temp)
    G.g_temp = G.g_temp + (target - G.g_temp) / cfg.tau_years
    G.g_temp = max(cfg.g_temp_min, min(cfg.g_temp_max, G.g_temp))
    -- sea level: land ice melted (or grown) at the ice's own pace, and the ocean's thermal
    -- expansion, which follows the temperature over a century (the deep ocean warms slowly)
    local th = G.sea_thermal or 0
    th = th + (cfg.k_thermal * G.g_temp - th) / cfg.tau_sea_years
    G.sea_thermal = th
    G.sea_level = cfg.k_ice_sea * ((G.ice_volume_ref or vol) - vol) / W.n + th
    -- g_temp ledger mirrors whichever driver dominates this year
    local era = classify_era(W, G.era)
    if era ~= G.era then
      local old = G.era
      G.era = era
      if W.record_events then
        local tag = ledger.dominant_tag(W, "g_temp")
        events.emit(W, "era_change", 0, tag, 1, { from = old, to = era })
      end
    end
  end
  G.co2_src, G.co2_forest = 0, 0
  G.acc_weather, G.acc_samples = 0, 0
  ledger.decay(W)
end

return sys
