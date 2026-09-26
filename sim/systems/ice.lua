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

function sys.update(W)
  local cfg = cfgmod.ICE
  local n = W.n
  local F, C, I = W.F, W.C, W.I
  local ice, temp, precip, sst = F.ice, F.temp, F.precip, F.sst
  local cice, iice, cp, ct = C.ice, I.ice, C.precip, C.temp
  local ocean = W.geo.ocean
  local melt_out, snow_out = W.scratch.melt, W.scratch.snow
  local k_snow, k_melt = cfg.k_snow, cfg.k_melt
  local sg, sm, sfreeze = cfg.sea_grow, cfg.sea_melt, cfg.sea_freeze_temp
  local eps, hold = 0.001, 96
  for c = 1, n do
    local old = ice[c]
    local t = temp[c]
    local acc, loss = 0, 0
    if ocean[c] == 1 then
      if t < sfreeze and sst[c] <= cfg.sst_freeze + 0.05 then
        local f = (sfreeze - t) / 8
        if f > 1 then f = 1 end
        acc = sg * f * (1 - old)
      elseif t > sfreeze + 1 then
        local f = (t - sfreeze - 1) / 5
        if f > 1 then f = 1 end
        loss = sm * f
      end
      melt_out[c], snow_out[c] = 0, 0
    else
      local snow = 0
      if t < 0 then snow = precip[c] end
      snow_out[c] = snow
      acc = snow * k_snow * (1.05 - old)
      if t > 0 and old > 0 then loss = k_melt * t end
    end
    local v = old + acc
    if loss > v then loss = v end
    v = v - loss
    if v > 1 then v = 1 end
    ice[c] = v
    if ocean[c] == 0 then melt_out[c] = loss * cfg.melt_water end
    if cice[c] ~= 0 or cp[c] ~= 0 or ct[c] ~= 0 then
      blame.step(cice, iice, c, v - old, eps, hold, acc, cp[c], -loss, ct[c], 0, 0, 0, 0)
    end
  end
end

return sys
