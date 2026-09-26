-- Hydrology (02_WORLD_MODEL.md §5.5): soil water, runoff, river flow (one pass in drainage
-- order), floods, erosion & silt. Rain = precip when temp ≥ 0 (snow goes to ice).
-- Flow and silt carry tags downstream (the dominant upstream contributor's tag).
--
-- Per-cell state beyond the field budget: hydro.flow_mean (EMA of flow, "bankfull" reference
-- for floods) — see DECISIONS.md D-006.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "hydrology" }

function sys.init(W)
  local sc = W.scratch
  sc.runoff = sc.runoff or fields.filled(W.n, 0)
  sc.flow_best = sc.flow_best or fields.filled(W.n, 0)
  sc.flow_btag = sc.flow_btag or fields.filled(W.n, 0)
  sc.silt_best = sc.silt_best or fields.filled(W.n, 0)
  sc.silt_btag = sc.silt_btag or fields.filled(W.n, 0)
  sc.flood = sc.flood or {}
  local H = W.S.hydro
  if not H then
    H = { flow_mean = fields.filled(W.n, 0) }
    W.S.hydro = H
  end
end

function sys.update(W)
  local cfg = cfgmod.HYDRO
  local n = W.n
  local F, C, I = W.F, W.C, W.I
  local soil, precip, temp, flow, silt = F.soil_water, F.precip, F.temp, F.flow, F.silt
  local grass, forest, fert = F.grass, F.forest, F.fertility
  local csoil, isoil, cp, ct = C.soil_water, I.soil_water, C.precip, C.temp
  local cflow, iflow, csilt, isilt = C.flow, I.flow, C.silt, I.silt
  local cgrass, cforest, cfert, ifert = C.grass, C.forest, C.fertility, I.fertility
  local cice = C.ice
  local geo = W.geo
  local ocean, downhill, order, slope = geo.ocean, geo.downhill, geo.drain_order, geo.slope
  local sc = W.scratch
  local evl, melt, runoff = sc.evap_land, sc.melt, sc.runoff
  local fbest, fbtag, sbest, sbtag = sc.flow_best, sc.flow_btag, sc.silt_best, sc.silt_btag
  local fmean = W.S.hydro.flow_mean
  local k_ps, k_es, fc, k_run, k_base = cfg.k_ps, cfg.k_es, cfg.field_capacity, cfg.k_run, cfg.k_base
  local k_ero, ero_max = cfg.k_ero, cfg.erosion_max
  local lake_evap = cfg.lake_evap

  -- 1. soil water balance, local runoff, erosion (land); reset accumulators
  for c = 1, n do
    flow[c] = 0
    silt[c] = 0
    fbest[c], fbtag[c], sbest[c], sbtag[c] = 0, 0, 0, 0
    if ocean[c] == 0 then
      local old = soil[c]
      local rain = temp[c] >= 0 and precip[c] or 0
      local inp = (rain + melt[c]) * k_ps
      local out = evl[c] * k_es
      local s = old + inp - out
      if s < 0 then out = out + s; s = 0 end
      local ro = 0
      if s > fc then ro = (s - fc) * k_run end
      local base = s * k_base
      ro = ro + base
      s = s - ro
      if s > 1 then ro = ro + s - 1; s = 1 end
      soil[c] = s
      runoff[c] = ro
      flow[c] = ro
      -- erosion: rain on bare slopes strips fertility into silt
      local veg = grass[c] + forest[c]
      if veg > 1 then veg = 1 end
      local ero = precip[c] * slope[c] * (1 - veg) * k_ero
      if ero > ero_max then ero = ero_max end
      local f0 = fert[c]
      if ero > f0 then ero = f0 end
      fert[c] = f0 - ero
      silt[c] = ero
      -- blame: soil water (rain vs melt vs evaporation)
      local ts, tpc, ttc = csoil[c], cp[c], ct[c]
      if ts ~= 0 or tpc ~= 0 or ttc ~= 0 or cice[c] ~= 0 then
        blame.step(csoil, isoil, c, s - old, 0.001, 48, rain * k_ps, tpc, melt[c] * k_ps, cice[c], -out, ttc, 0, 0)
      end
      -- erosion tag: bare ground (vegetation's tag) or the rain that fell on it
      if ero > 0 then
        local vt = cgrass[c]
        if vt == 0 then vt = cforest[c] end
        if vt == 0 then vt = tpc end
        sbtag[c] = vt
        sbest[c] = ero
        if vt ~= 0 then blame.step(cfert, ifert, c, -ero, 0.00005, 240, -ero, vt, 0, 0, 0, 0, 0, 0) end
      end
      fbtag[c] = ts ~= 0 and ts or tpc
      fbest[c] = ro
    else
      runoff[c] = 0
    end
  end

  -- 2. accumulate flow + silt downstream (upstream first); carry the dominant tag
  for i = 1, #order do
    local c = order[i]
    local d = downhill[c]
    local fcv = flow[c]
    -- resolve this cell's tags now that all upstream inflow has arrived
    local ft = fbtag[c]
    if fbest[c] < cfg.tag_share * fcv then ft = 0 end
    local st = sbtag[c]
    local sv = silt[c]
    if sbest[c] < cfg.tag_share * sv then st = 0 end
    if ft ~= 0 or cflow[c] ~= 0 then
      if ft ~= 0 then blame.set(cflow, iflow, c, ft)
      else
        local k = iflow[c] + 1
        if k >= 24 then cflow[c] = 0; iflow[c] = 0 else iflow[c] = k end
      end
    end
    if st ~= 0 or csilt[c] ~= 0 then
      if st ~= 0 then blame.set(csilt, isilt, c, st)
      else
        local k = isilt[c] + 1
        if k >= 48 then csilt[c] = 0; isilt[c] = 0 else isilt[c] = k end
      end
    end
    if d ~= 0 then
      flow[d] = flow[d] + fcv
      silt[d] = silt[d] + sv * cfg.silt_carry
      if fcv > fbest[d] then fbest[d] = fcv; fbtag[d] = cflow[c] end
      if sv > sbest[d] then sbest[d] = sv; sbtag[d] = csilt[c] end
    else
      -- basin: standing water keeps the soil saturated (lake), evaporates slowly
      if fcv > cfg.river_threshold then
        local s = soil[c] + fcv * lake_evap
        if s > 1 then s = 1 end
        soil[c] = s
      end
    end
  end

  -- 3. ocean mouth cells: tag from the arriving river; flow_mean EMA; flood candidates
  local floods = sc.flood
  for k = #floods, 1, -1 do floods[k] = nil end
  local ema = cfg.flow_ema
  local bank_min, fmult = cfg.bankfull_min, cfg.flood_mult
  for c = 1, n do
    local fv = flow[c]
    if ocean[c] == 1 then
      if fv > 0 then
        local ft = fbtag[c]
        if ft ~= 0 then blame.set(cflow, iflow, c, ft) end
        local st = sbtag[c]
        if st ~= 0 then blame.set(csilt, isilt, c, st) end
      end
    else
      local mean = fmean[c]
      local bank = mean > bank_min and mean or bank_min
      if fv > fmult * bank and fv > cfg.river_threshold then floods[#floods + 1] = c end
    end
    fmean[c] = fmean[c] + (fv - fmean[c]) * ema
  end
end

return sys
