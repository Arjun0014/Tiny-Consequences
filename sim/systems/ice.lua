-- Ice & snow (02_WORLD_MODEL.md §5.4). Land: snowfall accumulates, melts with warmth (melt
-- water goes to hydrology via scratch.melt). Sea ice grows in freezing air, melts above.
-- ice is a 0–1 cover fraction; ≥ 0.8 for a year on land = ice sheet (biome classifier).
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "ice" }

function sys.init(W)
  local sc = W.scratch
  sc.melt = sc.melt or fields.filled(W.n, 0)
  sc.snow = sc.snow or fields.filled(W.n, 0)
end

-- Cells with no ice that are too warm to freeze (and carry no ice tag) can't change: they are
-- skipped, which is exactly equivalent to running the full update on them (D-019).
function sys.update(W)
  local cfg = cfgmod.ICE
  local F, C, I = W.F, W.C, W.I
  local ice, temp, precip, sst = F.ice, F.temp, F.precip, F.sst
  local cice, iice, cp, ct = C.ice, I.ice, C.precip, C.temp
  local melt_out = W.scratch.melt
  local k_snow, k_melt = cfg.k_snow, cfg.k_melt
  local sg, sm, sfreeze = cfg.sea_grow, cfg.sea_melt, cfg.sea_freeze_temp
  local sstf = cfg.sst_freeze + 0.05
  local mw = cfg.melt_water
  local eps, hold = 0.001, 96
  local geo = W.geo
  local oc, land = geo.ocean_cells, geo.land_cells
  for i = 1, #oc do
    local c = oc[i]
    local old = ice[c]
    local t = temp[c]
    if old > 0 or t < sfreeze or cice[c] ~= 0 then
      local acc, loss = 0, 0
      if t < sfreeze and sst[c] <= sstf then
        local f = (sfreeze - t) / 8
        if f > 1 then f = 1 end
        acc = sg * f * (1 - old)
      elseif t > sfreeze + 1 then
        local f = (t - sfreeze - 1) / 5
        if f > 1 then f = 1 end
        loss = sm * f
      end
      local v = old + acc
      if loss > v then loss = v end
      v = v - loss
      if v > 1 then v = 1 end
      ice[c] = v
      if cice[c] ~= 0 or cp[c] ~= 0 or ct[c] ~= 0 then
        blame.step(cice, iice, c, v - old, eps, hold, acc, cp[c], -loss, ct[c], 0, 0, 0, 0)
      end
    end
  end
  for i = 1, #land do
    local c = land[i]
    local old = ice[c]
    local t = temp[c]
    if old > 0 or t < 0 or cice[c] ~= 0 then
      local acc, loss = 0, 0
      if t < 0 then acc = precip[c] * k_snow * (1.05 - old) end
      if t > 0 and old > 0 then loss = k_melt * t end
      local v = old + acc
      if loss > v then loss = v end
      v = v - loss
      if v > 1 then v = 1 end
      ice[c] = v
      melt_out[c] = loss * mw
      if cice[c] ~= 0 or cp[c] ~= 0 or ct[c] ~= 0 then
        blame.step(cice, iice, c, v - old, eps, hold, acc, cp[c], -loss, ct[c], 0, 0, 0, 0)
      end
    elseif melt_out[c] ~= 0 then
      melt_out[c] = 0
    end
  end
end

return sys
