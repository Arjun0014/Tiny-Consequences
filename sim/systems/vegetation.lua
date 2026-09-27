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

-- Multi-rate (D-019): each land cell updates every VEG.stride ticks with dt = stride (half the
-- cells per tick at stride 2). A cell reads its OWN values from the live field (fire, grazing
-- and oil may have changed them since its last update) and its neighbours' from the prev
-- buffer, which each cell publishes right after its update.
function sys.init(W)
  W.scratch.veg_light = W.scratch.veg_light or {}
end

function sys.rebuild(W)
  local g = W.grid
  local hs = {}
  for c = 1, W.n do
    local lat = g.lat[c]
    hs[c] = (lat >= 0 and 1 or -1) * abs(sin(lat))
  end
  W.veg_static = { hs = hs }
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
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I, P = W.F, W.C, W.I, W.P
  local grass, forest, pg, pf = F.grass, F.forest, P.grass, P.forest
  local soil, temp, fert, scar, lu = F.soil_water, F.temp, F.fertility, F.burn_scar, F.land_use
  local cg, ig, cf, iff = C.grass, I.grass, C.forest, I.forest
  local csoil, ctemp, cfert, ifert = C.soil_water, C.temp, C.fertility, I.fertility
  local land = W.geo.land_cells
  local hs = W.veg_static.hs
  local tick = W.tick
  local toy = tick % 48
  local season = sin(2 * math.pi * toy / 48 + cfgmod.TEMP.season_phase)
  local lamp = cfg.light_amp * season
  local stride = cfg.stride
  local dt = stride
  local r_g, r_f, K_g, K_f = cfg.r_grass * dt, cfg.r_forest * dt, cfg.K_grass, cfg.K_forest
  local die_g, die_f = cfg.die_grass * dt, cfg.die_forest * dt
  local h_g, h_f = cfg.soil_half_grass, cfg.soil_half_forest
  local seed_g, seed_f = cfg.seed_grass / W.sscale * dt, cfg.seed_forest / W.sscale * dt
  -- spin-up runs soil fertility's slow relaxation faster (same equilibrium, see config.SPINUP_FERT_BOOST)
  local k_regen, fire_block = cfg.k_regen * dt * (W.G.calibrating and cfgmod.SPINUP_FERT_BOOST or 1), cfg.fire_block_ticks
  local glo, ghi, flo, fhi = cfg.grass_t[1], cfg.grass_t[2], cfg.forest_t[1], cfg.forest_t[2]
  local gmid, ghalf = (glo + ghi) * 0.5, (ghi - glo) * 0.5
  local fmid, fhalf = (flo + fhi) * 0.5, (fhi - flo) * 0.5
  local fb0, fb1 = cfg.fert_base[1], cfg.fert_base[2]
  local die_frost, frost_t = cfg.die_frost * dt, cfg.frost_temp
  local scar_expire = cfg.scar_expire
  local geps, ghold, feps, fhold = 0.0005, 96, 0.0002, 480
  local netbio = 0
  local ph = tick % stride
  for i = 1 + ph, #land, stride do
    local c = land[i]
    local t = temp[c]
    local s = soil[c]
    local f = fert[c]
    local gr, fo = grass[c], forest[c]
    -- day length: brighter summers at high latitude
    local light = 1 + lamp * hs[c]
    if light < 0.2 then light = 0.2 end
    local wg = (s / (s + h_g)) * (1 + h_g)       -- water factors (1 at saturation)
    local wf = (s / (s + h_f)) * (1 + h_f)
    local x = (t - gmid) / ghalf
    local bg = 1 - x * x
    if bg < 0 then bg = 0 end
    x = (t - fmid) / fhalf
    local bf = 1 - x * x
    if bf < 0 then bf = 0 end
    local sg = bg * wg * (0.4 + 0.6 * f) * light
    local sf = bf * wf * (0.3 + 0.7 * f) * light
    local sc = scar[c]
    if sc > 0 then
      if sc < fire_block then sf = sf * 0.2 end
      sc = sc + stride
      if sc > scar_expire then sc = 0 end
      scar[c] = sc
    end
    local nursery = gr / 0.3
    if nursery > 1 then nursery = 1 end
    -- recolonisation from neighbours (published prev values)
    local b = c * 6 - 6
    local k_n = cnt[c]
    local ng, nf, tg, tf, bgv, bfv = 0, 0, 0, 0, 0, 0
    for k = 1, k_n do
      local j = nbr[b + k]
      local vg, vf = pg[j], pf[j]
      ng = ng + vg
      nf = nf + vf
      if vg > bgv then bgv = vg; tg = cg[j] end
      if vf > bfv then bfv = vf; tf = cf[j] end
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
    pg[c] = ng2
    -- forest (establishes on grassland; farms and villages keep it cleared)
    local grow_f = r_f * sf * fo * (1 - fo / K_f) * (0.5 + 0.5 * nursery)
    local dief = die_f * (1 - (wf < 1 and wf or 1)) * fo
    local seedf = seed_f * nf * (1 - fo) * sf * nursery
    if lu[c] ~= 0 then grow_f, seedf = 0, 0 end
    local nf2 = fo + grow_f - dief + seedf
    if nf2 < 0 then nf2 = 0 elseif nf2 > 1 then nf2 = 1 end
    forest[c] = nf2
    pf[c] = nf2
    netbio = netbio + (ng2 - gr) * 0.5 + (nf2 - fo)
    -- fertility regenerates toward a vegetation-dependent base
    local veg = ng2 + nf2
    if veg > 1 then veg = 1 end
    local df = k_regen * (fb0 + (fb1 - fb0) * veg - f)
    fert[c] = f + df
    -- blame
    local ts, tt = csoil[c], ctemp[c]
    local stress_tag = ts ~= 0 and ts or tt
    if cg[c] ~= 0 or stress_tag ~= 0 or tg ~= 0 then
      blame.step(cg, ig, c, ng2 - gr, geps, ghold, grow_g, 0, -die, stress_tag, seed, tg, 0, 0)
    end
    if cf[c] ~= 0 or stress_tag ~= 0 or tf ~= 0 then
      blame.step(cf, iff, c, nf2 - fo, feps, fhold, grow_f, 0, -dief, stress_tag, seedf, tf, 0, 0)
    end
    if cfert[c] ~= 0 then
      blame.step(cfert, ifert, c, df, 0.0002, 240, df, 0, 0, 0, 0, 0, 0, 0)
    end
  end
  -- net natural growth absorbs co2 (burning emits it back in the fire system)
  W.G.co2_forest = (W.G.co2_forest or 0) + netbio * cfg.k_co2
end

return sys
