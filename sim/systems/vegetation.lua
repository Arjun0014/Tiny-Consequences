-- Vegetation & soil (02_WORLD_MODEL.md §5.6): grass and forest growth, dieback, succession
-- (forests establish on grassland), recolonisation from neighbours (never from nothing),
-- fertility regeneration. Grazing, browsing, burning, clearing and oil damage are applied by
-- the systems that cause them (fauna, fire, villages, ocean), each with its own blame step.
--
-- Blame: growth/dieback is baseline unless a stress is tagged — dieback is attributed to the
-- soil-water tag (drought), else temperature's tag (heat/cold); recolonisation carries the
-- dominant tagged neighbour's tag.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "vegetation" }
local sin, abs = math.sin, math.abs

function sys.init(W)
  W.scratch.veg_light = W.scratch.veg_light or {}
end

local function bell(t, lo, hi)
  local mid, half = (lo + hi) * 0.5, (hi - lo) * 0.5
  local x = (t - mid) / half
  local v = 1 - x * x
  if v < 0 then return 0 end
  return v
end
sys.bell = bell

function sys.update(W)
  local cfg = cfgmod.VEG
  local n, g = W.n, W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I, P = W.F, W.C, W.I, W.P
  fields.snapshot(W, "grass")
  fields.snapshot(W, "forest")
  local grass, forest, pg, pf = F.grass, F.forest, P.grass, P.forest
  local soil, temp, fert, scar, lu = F.soil_water, F.temp, F.fertility, F.burn_scar, F.land_use
  local cg, ig, cf, iff = C.grass, I.grass, C.forest, I.forest
  local csoil, ctemp, cfert, ifert = C.soil_water, C.temp, C.fertility, I.fertility
  local ocean = W.geo.ocean
  local land = W.geo.land_cells
  local lat = g.lat
  local toy = W.tick % 48
  local season = sin(2 * math.pi * toy / 48 + cfgmod.TEMP.season_phase)
  local r_g, r_f, K_g, K_f = cfg.r_grass, cfg.r_forest, cfg.K_grass, cfg.K_forest
  local die_g, die_f = cfg.die_grass, cfg.die_forest
  local h_g, h_f = cfg.soil_half_grass, cfg.soil_half_forest
  local seed_g, seed_f = cfg.seed_grass / W.sscale, cfg.seed_forest / W.sscale
  local k_regen, fire_block = cfg.k_regen, cfg.fire_block_ticks
  local glo, ghi, flo, fhi = cfg.grass_t[1], cfg.grass_t[2], cfg.forest_t[1], cfg.forest_t[2]
  local fb0, fb1 = cfg.fert_base[1], cfg.fert_base[2]
  local die_frost, frost_t = cfg.die_frost, cfg.frost_temp
  local netbio = 0
  for i = 1, #land do
    local c = land[i]
    local t = temp[c]
    local s = soil[c]
    local f = fert[c]
    local gr, fo = pg[c], pf[c]
    -- day length: brighter summers at high latitude
    local light = 1 + cfg.light_amp * season * (lat[c] >= 0 and 1 or -1) * abs(sin(lat[c]))
    if light < 0.2 then light = 0.2 end
    local wg = (s / (s + h_g)) * (1 + h_g)       -- water factors (1 at saturation)
    local wf = (s / (s + h_f)) * (1 + h_f)
    local sg = bell(t, glo, ghi) * wg * (0.4 + 0.6 * f) * light
    local sf = bell(t, flo, fhi) * wf * (0.3 + 0.7 * f) * light
    local recent = scar[c] > 0 and scar[c] < fire_block
    if recent then sf = sf * 0.2 end
    local nursery = gr / 0.3
    if nursery > 1 then nursery = 1 end
    -- recolonisation from neighbours (last tick's values)
    local b = c * 6 - 6
    local k_n = cnt[c]
    local ng, nf, tg, tf, bg, bf = 0, 0, 0, 0, 0, 0
    for k = 1, k_n do
      local j = nbr[b + k]
      local vg, vf = pg[j], pf[j]
      ng = ng + vg
      nf = nf + vf
      if vg > bg then bg = vg; tg = cg[j] end
      if vf > bf then bf = vf; tf = cf[j] end
    end
    ng, nf = ng / k_n, nf / k_n
    -- grass
    local grow_g = r_g * sg * gr * (1 - (gr + 0.5 * fo) / K_g)
    local die = die_g * (1 - (wg < 1 and wg or 1)) * gr
    if t < frost_t then die = die + die_frost * gr end
    local seed = seed_g * ng * (1 - gr) * sg
    local ng2 = gr + grow_g - die + seed
    if ng2 < 0 then ng2 = 0 elseif ng2 > 1 then ng2 = 1 end
    grass[c] = ng2
    -- forest (establishes on grassland; farms and villages keep it cleared)
    local grow_f = r_f * sf * fo * (1 - fo / K_f) * (0.5 + 0.5 * nursery)
    local dief = die_f * (1 - (wf < 1 and wf or 1)) * fo
    local seedf = seed_f * nf * (1 - fo) * sf * nursery
    if lu[c] ~= 0 then grow_f, seedf = 0, 0 end
    local nf2 = fo + grow_f - dief + seedf
    if nf2 < 0 then nf2 = 0 elseif nf2 > 1 then nf2 = 1 end
    forest[c] = nf2
    netbio = netbio + (ng2 - gr) * 0.5 + (nf2 - fo)
    -- fertility regenerates toward a vegetation-dependent base
    local veg = ng2 + nf2
    if veg > 1 then veg = 1 end
    local fbase = fb0 + (fb1 - fb0) * veg
    local df = k_regen * (fbase - f)
    fert[c] = f + df
    -- blame
    local ts, tt = csoil[c], ctemp[c]
    local stress_tag = ts ~= 0 and ts or tt
    if cg[c] ~= 0 or stress_tag ~= 0 or tg ~= 0 then
      blame.step(cg, ig, c, ng2 - gr, 0.0005, 96, grow_g, 0, -die, stress_tag, seed, tg, 0, 0)
    end
    if cf[c] ~= 0 or stress_tag ~= 0 or tf ~= 0 then
      blame.step(cf, iff, c, nf2 - fo, 0.0002, 480, grow_f, 0, -dief, stress_tag, seedf, tf, 0, 0)
    end
    if cfert[c] ~= 0 then
      blame.step(cfert, ifert, c, df, 0.0002, 240, df, 0, 0, 0, 0, 0, 0, 0)
    end
    if scar[c] > 0 then scar[c] = scar[c] + 1 end
    if scar[c] > cfg.scar_expire then scar[c] = 0 end
  end
  -- net natural growth absorbs co2 (burning emits it back in the fire system)
  W.G.co2_forest = (W.G.co2_forest or 0) + netbio * cfg.k_co2
  -- ocean cells hold no vegetation
  for c = 1, n do
    if ocean[c] == 1 and (grass[c] ~= 0 or forest[c] ~= 0) then grass[c], forest[c] = 0, 0 end
  end
end

return sys
