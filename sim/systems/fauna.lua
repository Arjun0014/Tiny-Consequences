-- Land animals: grazers (deer-like) and predators (wolf-like) (02_WORLD_MODEL.md §5.9, slot 9).
--
-- Local dynamics per land cell (multi-rate: every FAUNA.stride ticks with dt = stride,
-- staggered so each tick does 1/stride of the land cells, D-012):
--   food      = (grass + 0.5 crop) × (1 − snow_block × ice)          -- snow hides food in winter
--   K_eff     = K × food / (food + h_food)
--   grazers  += r H (1 − H/K_eff) [starvation capped] − predation − fire − hunting
--   predation = a P max(0, H − refuge) / (1 + a t_h max(0, H − refuge))   -- Holling II + refuge
--   predators += e predation − m P − fire − culling
-- Pressure on vegetation (written here, blamed with the grazers' tag): grazing on grass,
-- browsing on forest (b H (b0 + hunger) f/(f + h), hunger = H/K_eff: per unit of forest
-- strongest when forest is low, and hungry grazers strip saplings — this is what stops forest
-- recovery when predators are gone), crop raids.
-- Hooks for villages (Stage 1.7): W.S.fauna.cull[c] / hunt[c] are per-tick loss fractions for
-- predators / grazers, with cull_tag / hunt_tag as their cause tags.
--
-- Migration (every FAUNA.migrate_every ticks per species, species staggered, rate × that): a
-- conservative flux on every edge between walkable cells, biased toward the more attractive end
-- (grazers: food, few predators; predators: prey; burning cells repel). Land and sea ice ≥ 0.8
-- are walkable (ice bridges); open ocean never. Flows carry the source cell's tag. Nothing is
-- ever spawned: populations below min_pop vanish, recolonisation only by migration, so a global
-- extinction is permanent.
--
-- "Release" blame (D-013): when a pressure disappears (predators far below their local normal
-- while carrying a cause tag), the growth it no longer holds back carries that tag; likewise
-- predators starving because prey is far below normal carry the prey's tag.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "fauna" }

local FGZ, FPD = fields.BY_NAME.grazers, fields.BY_NAME.predators
local FGR, FFO, FCR = fields.BY_NAME.grass, fields.BY_NAME.forest, fields.BY_NAME.crop

function sys.init(W)
  local n = W.n
  local S = W.S.fauna
  if not S then
    local f = fields.filled
    S = { cull = f(n, 0), cull_tag = f(n, 0), hunt = f(n, 0), hunt_tag = f(n, 0),
          hnorm = f(n, 0), pnorm = f(n, 0) }
    W.S.fauna = S
  end
  local sc = W.scratch
  sc.fz_A = sc.fz_A or fields.filled(n, 0)
  sc.fz_acc = sc.fz_acc or fields.filled(n, 0)
  sc.fz_bin = sc.fz_bin or fields.filled(n, 0)
  sc.fz_tin = sc.fz_tin or fields.filled(n, 0)
end

-- Walkable domain and edge lists (derived; rebuilt when the coastline changes).
function sys.rebuild(W)
  local n, g = W.n, W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local geo = W.geo
  local ocean, coast = geo.ocean, geo.coast
  local FZ = {}
  W.fauna_static = FZ
  local walk, dom = {}, {}
  local cand = {}
  for c = 1, n do
    if ocean[c] == 0 then walk[c] = 1; dom[c] = 1
    elseif coast[c] == 1 then walk[c] = 0; dom[c] = 1; cand[#cand + 1] = c
    else walk[c] = 0; dom[c] = 0 end
  end
  local la, lb, ia, ib = {}, {}, {}, {}
  for c = 1, n do
    if dom[c] == 1 then
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if j > c and dom[j] == 1 then
          if ocean[c] == 0 and ocean[j] == 0 then la[#la + 1] = c; lb[#lb + 1] = j
          else ia[#ia + 1] = c; ib[#ib + 1] = j end
        end
      end
    end
  end
  FZ.walk, FZ.cand = walk, cand
  FZ.le_a, FZ.le_b, FZ.nle = la, lb, #la
  FZ.ie_a, FZ.ie_b, FZ.nie = ia, ib, #ia
  FZ.ice_list, FZ.nice = {}, 0
  sys.refresh_ice(W)
end

-- Sea ice bridges: coastal ocean cells with ice ≥ ice_walk are walkable this month. Animals
-- caught on ice that thins below it drown.
function sys.refresh_ice(W)
  local FZ = W.fauna_static
  local cand, walk, list = FZ.cand, FZ.walk, FZ.ice_list
  local ice, gz, pd = W.F.ice, W.F.grazers, W.F.predators
  local thr = cfgmod.FAUNA.ice_walk
  local k = 0
  for i = 1, #cand do
    local c = cand[i]
    if ice[c] >= thr then
      walk[c] = 1
      k = k + 1
      list[k] = c
    else
      walk[c] = 0
      if gz[c] > 0 or pd[c] > 0 then gz[c] = 0; pd[c] = 0 end
    end
  end
  for i = #list, k + 1, -1 do list[i] = nil end
  FZ.nice = k
end

local function local_step(W, S, cfg, ph, dt)
  local F, C, I = W.F, W.C, W.I
  local gz, pd, grass, forest, crop = F.grazers, F.predators, F.grass, F.forest, F.crop
  local ice, fire = F.ice, F.fire
  local cgz, igz, cpd, ipd = C.grazers, I.grazers, C.predators, I.predators
  local cgr, igr, cfo, ifo, ccr, icr = C.grass, I.grass, C.forest, I.forest, C.crop, I.crop
  local cfire = C.fire
  local cull, ctag, hunt, htag = S.cull, S.cull_tag, S.hunt, S.hunt_tag
  local hnorm, pnorm = S.hnorm, S.pnorm
  local land = W.geo.land_cells
  local stride = cfg.stride
  local rg, Kg, hfood, cfood, sblock, smax = cfg.r_grazer, cfg.K_grazer, cfg.h_food, cfg.crop_food, cfg.snow_block, cfg.starve_max
  local ap, th, refuge, ep, mp = cfg.a_pred, cfg.t_handle, cfg.refuge, cfg.e_pred, cfg.m_pred
  local kgr, hgr, kbr, hbr, kra, hra = cfg.graze, cfg.h_graze, cfg.browse, cfg.h_browse, cfg.raid, cfg.h_raid
  local kov, hstart = cfg.overgraze, cfg.overgraze_from
  local bbase = cfg.browse_base
  local fmort = cfg.fire_mort
  local relf, above = cfg.release_frac, cfg.pressure_above
  local descends, eparent = blame.descends, W.E.parent
  local rmin_h, rmin_p = cfg.release_min.grazers, cfg.release_min.predators
  local ema = dt / (cfg.ref_months * 4)
  local minp = cfg.min_pop
  local geps, ghold, peps, phold = FGZ.eps, FGZ.hold, FPD.eps, FPD.hold
  local eaten = 0   -- biomass eaten (grass counts half, like vegetation's net growth)
  local greps, grhold, foeps, fohold = FGR.eps, FGR.hold, FFO.eps, FFO.hold
  for i = 1 + ph, #land, stride do
    local c = land[i]
    local H, P = gz[c], pd[c]
    local hn, pn = hnorm[c], pnorm[c]
    if H > 0 or P > 0 then
      local gr, fo, cr = grass[c], forest[c], crop[c]
      local food = (gr + cfood * cr) * (1 - sblock * ice[c])
      local keff = Kg * food / (food + hfood)
      local lg
      if keff > 1e-6 then lg = rg * H * (1 - H / keff) else lg = -smax * H end
      if lg < -smax * H then lg = -smax * H end
      local hx = H - refuge
      local pred = 0
      if hx > 0 and P > 0 then pred = ap * P * hx / (1 + ap * th * hx) end
      local fi = fire[c]
      local hf, pf = 0, 0
      if fi > 0 then hf = fmort * fi * H; pf = fmort * fi * P end
      local hh, pc = hunt[c] * H, cull[c] * P
      local H1 = H + dt * (lg - pred - hf - hh)
      if H1 < minp then H1 = 0 elseif H1 > 1 then H1 = 1 end
      local P1 = P + dt * (ep * pred - mp * P - pf - pc)
      if P1 < minp then P1 = 0 elseif P1 > 1 then P1 = 1 end
      gz[c], pd[c] = H1, P1
      local tH, tP = cgz[c], cpd[c]
      -- A pressure carries its cause only while it is above normal (more grazers / predators
      -- than usual here); a missing pressure's cause travels into the growth it allows (release).
      local tHa, tPa = 0, 0
      if tH ~= 0 and H > above * hn then tHa = tH end
      if tP ~= 0 and P > above * pn then tPa = tP end
      -- vegetation pressure
      if H > 0 then
        -- hungry grazers (near their food limit) strip saplings and graze to the roots;
        -- well-fed ones barely browse (a herd without wolves overgrazes its range)
        local hunger = keff > 1e-6 and H / keff or 1
        if hunger > 1 then hunger = 1 end
        local starve = hunger > hstart and (hunger - hstart) / (1 - hstart) or 0
        local gg = dt * kgr * H * (1 + kov * starve) * gr / (gr + hgr)
        if gg > gr then gg = gr end
        grass[c] = gr - gg
        local bb = dt * kbr * H * (bbase + hunger) * fo / (fo + hbr)
        if bb > fo then bb = fo end
        forest[c] = fo - bb
        eaten = eaten + 0.5 * gg + bb
        if tHa ~= 0 or cgr[c] ~= 0 then blame.step(cgr, igr, c, -gg, greps, grhold, -gg, tHa, 0, 0, 0, 0, 0, 0) end
        if tHa ~= 0 or cfo[c] ~= 0 then blame.step(cfo, ifo, c, -bb, foeps, fohold, -bb, tHa, 0, 0, 0, 0, 0, 0) end
        if cr > 0 then
          local rr = dt * kra * H * cr / (cr + hra)
          if rr > cr then rr = cr end
          crop[c] = cr - rr
          eaten = eaten + 0.5 * rr
          if tHa ~= 0 or ccr[c] ~= 0 then blame.step(ccr, icr, c, -rr, FCR.eps, FCR.hold, -rr, tHa, 0, 0, 0, 0, 0, 0) end
        end
      end
      local relg = 0
      if tP ~= 0 and pn > rmin_p and P1 < relf * pn then
        relg = tP
        -- keep a more-derived tag (e.g. the boom this release produced)
        if tH ~= 0 and tH ~= tP and descends(eparent, tH, tP) then relg = tH end
      end
      local relp = 0
      if tH ~= 0 and hn > rmin_h and H1 < relf * hn then
        relp = tH
        if tP ~= 0 and tP ~= tH and descends(eparent, tP, tH) then relp = tP end
      end
      local tfire = 0
      if fi > 0 then tfire = cfire[c] end
      local tlg = relg
      if lg < 0 then tlg = cgr[c] end        -- starvation: the food's cause
      local th_ = htag[c]
      if tH ~= 0 or tlg ~= 0 or tPa ~= 0 or tfire ~= 0 or th_ ~= 0 then
        blame.step(cgz, igz, c, H1 - H, geps, ghold, dt * lg, tlg, -dt * pred, tPa, -dt * hf, tfire, -dt * hh, th_)
      end
      local tc_ = ctag[c]
      if tP ~= 0 or tHa ~= 0 or relp ~= 0 or tfire ~= 0 or tc_ ~= 0 then
        blame.step(cpd, ipd, c, P1 - P, peps, phold, dt * ep * pred, tHa, -dt * mp * P, relp, -dt * pf, tfire, -dt * pc, tc_)
      end
      hnorm[c] = hn + (H1 - hn) * ema
      pnorm[c] = pn + (P1 - pn) * ema
    else
      if hn ~= 0 then hn = hn - hn * ema; if hn < 1e-9 then hn = 0 end; hnorm[c] = hn end
      if pn ~= 0 then pn = pn - pn * ema; if pn < 1e-9 then pn = 0 end; pnorm[c] = pn end
      -- tags of an emptied cell idle out
      local tH, tP = cgz[c], cpd[c]
      if tH ~= 0 then blame.step(cgz, igz, c, 0, geps, ghold, 0, 0, 0, 0, 0, 0, 0, 0) end
      if tP ~= 0 then blame.step(cpd, ipd, c, 0, peps, phold, 0, 0, 0, 0, 0, 0, 0, 0) end
    end
  end
  -- animals breathe out what they eat: the biosphere stays carbon-neutral (D-008)
  if eaten > 0 then W.G.co2_forest = (W.G.co2_forest or 0) - eaten * cfgmod.VEG.k_co2 end
end

-- One migration step for a species (H values, CT/IT tags). mu = per-edge rate for this step.
local function migrate(W, cfg, grazer, mu)
  local F, C, I = W.F, W.C, W.I
  local H, CT, IT, eps, hold
  if grazer then H, CT, IT, eps, hold = F.grazers, C.grazers, I.grazers, FGZ.eps, FGZ.hold
  else H, CT, IT, eps, hold = F.predators, C.predators, I.predators, FPD.eps, FPD.hold end
  local gz, pd, grass, crop, ice, fire = F.grazers, F.predators, F.grass, F.crop, F.ice, F.fire
  local FZ = W.fauna_static
  local land = W.geo.land_cells
  local nland = #land
  local sc = W.scratch
  local A, acc, bin, tin = sc.fz_A, sc.fz_acc, sc.fz_bin, sc.fz_tin
  local a0, cfood, sblock, fear = cfg.attract0, cfg.crop_food, cfg.snow_block, cfg.fear
  local minp = cfg.min_pop
  local flow_frac = cfg.flow_tag_frac
  -- attractiveness
  if grazer then
    for i = 1, nland do
      local c = land[i]
      local a = (a0 + (grass[c] + cfood * crop[c]) * (1 - sblock * ice[c])) / (1 + fear * pd[c])
      if fire[c] > 0 then a = a * 0.05 end
      A[c] = a; acc[c] = 0; bin[c] = 0; tin[c] = 0
    end
  else
    for i = 1, nland do
      local c = land[i]
      local a = a0 + gz[c]
      if fire[c] > 0 then a = a * 0.05 end
      A[c] = a; acc[c] = 0; bin[c] = 0; tin[c] = 0
    end
  end
  local nice, ilist = FZ.nice, FZ.ice_list
  for i = 1, nice do
    local c = ilist[i]
    A[c] = a0; acc[c] = 0; bin[c] = 0; tin[c] = 0
  end
  -- conservative edge fluxes toward the more attractive end
  local ea, eb = FZ.le_a, FZ.le_b
  for e = 1, FZ.nle do
    local a, b = ea[e], eb[e]
    local ha, hb = H[a], H[b]
    if ha > 0 or hb > 0 then
      local Aa, Ab = A[a], A[b]
      local fl = mu * (ha * Ab - hb * Aa) / (Aa + Ab)
      acc[a] = acc[a] - fl
      acc[b] = acc[b] + fl
      if fl > 0 then
        local t = CT[a]
        if t ~= 0 and fl > bin[b] then bin[b] = fl; tin[b] = t end
      elseif fl < 0 then
        local t = CT[b]
        if t ~= 0 and -fl > bin[a] then bin[a] = -fl; tin[a] = t end
      end
    end
  end
  if nice > 0 then
    local walk = FZ.walk
    local ia, ib = FZ.ie_a, FZ.ie_b
    for e = 1, FZ.nie do
      local a, b = ia[e], ib[e]
      if walk[a] == 1 and walk[b] == 1 then
        local ha, hb = H[a], H[b]
        if ha > 0 or hb > 0 then
          local Aa, Ab = A[a], A[b]
          local fl = mu * (ha * Ab - hb * Aa) / (Aa + Ab)
          acc[a] = acc[a] - fl
          acc[b] = acc[b] + fl
          if fl > 0 then
            local t = CT[a]
            if t ~= 0 and fl > bin[b] then bin[b] = fl; tin[b] = t end
          elseif fl < 0 then
            local t = CT[b]
            if t ~= 0 and -fl > bin[a] then bin[a] = -fl; tin[a] = t end
          end
        end
      end
    end
  end
  -- apply
  for i = 1, nland + nice do
    local c
    if i <= nland then c = land[i] else c = ilist[i - nland] end
    local d = acc[c]
    if d ~= 0 then
      local h0 = H[c]
      local h1 = h0 + d
      if h1 < minp then h1 = 0 elseif h1 > 1 then h1 = 1 end
      H[c] = h1
      local tc, ti = CT[c], tin[c]
      -- a flow passes its tag on only when it really drives the change (not equilibrium
      -- exchange, which would keep old tags circulating forever)
      if ti ~= 0 and d < flow_frac * h0 then ti = 0 end
      if tc ~= 0 or ti ~= 0 then
        blame.step(CT, IT, c, h1 - h0, eps, hold, bin[c], ti, d - bin[c], 0, 0, 0, 0, 0)
      end
    end
  end
end

sys.migrate = migrate   -- exposed for tests

function sys.update(W)
  local cfg = cfgmod.FAUNA
  local S = W.S.fauna
  local tick = W.tick
  local stride = cfg.stride
  if tick % 4 == 0 then sys.refresh_ice(W) end
  local_step(W, S, cfg, tick % stride, stride)
  local me = cfg.migrate_every
  local mt = tick % me
  local ss2 = W.sscale * W.sscale
  if mt == 1 % me then
    migrate(W, cfg, true, cfg.mig_grazer / ss2 * me)
  elseif mt == (1 + math.floor(me / 2)) % me then
    migrate(W, cfg, false, cfg.mig_pred / ss2 * me)
  end
end

return sys
