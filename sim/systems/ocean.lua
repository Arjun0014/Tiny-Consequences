-- Ocean life, currents & oil (02_WORLD_MODEL.md §5.8, system slot 8).
--
-- Plankton: logistic growth × light × nutrients (static upwelling + river-mouth pulses from
-- hydrology's flow on ocean cells) × temperature (optimum 10–26 °C; marine heatwaves above
-- 30 °C stop growth and kill) × (1 − ice) × oil damage − fish grazing.
-- Fish: growth on plankton (Holling II) − mortality − oil kill − heat − silt (river mouths)
-- + diffusion between ocean neighbours (last tick's values, P.fish). Fishing is applied by
-- villages (Stage 1.7) in their own system.
-- Multi-rate: each ocean cell's life update runs every OCEAN.stride ticks with dt = stride,
-- staggered so each tick does 1/stride of the cells (D-012).
--
-- Currents (rebuild, per season): the wind over ocean, deflected along coasts (the component
-- pointing into land removed, renormalised) → mass-conserving push weights (D-015).
-- Oil: sparse active set (ocean + beached shore cells), advected by currents + diffusion, decays
-- (half-life ~2 years). Coastal ocean cells wash oil ashore; beached oil damages the shore
-- cell's grass and fertility, decays in months and leaches back into the sea (so shore fishing
-- stays poisoned for months). The oil's tag travels with the oil (largest contributor).
--
-- Blame: fish killed by oil carry the oil's tag; plankton/fish die-off from heat carries sst's
-- tag; fish decline from missing food carries plankton's tag; fish inflow carries the source tag.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "ocean" }
local sin, abs, sqrt, min = math.sin, math.abs, math.sqrt, math.min

local FP, FF, FO, FG = fields.BY_NAME.plankton, fields.BY_NAME.fish, fields.BY_NAME.oil, fields.BY_NAME.grass
local FFE = fields.BY_NAME.fertility

function sys.init(W)
  local n = W.n
  local S = W.S.ocean
  if not S then
    S = { oil = {}, oil_n = 0, fish_ref = fields.filled(n, 0) }
    W.S.ocean = S
  end
  local sc = W.scratch
  sc.oil_acc = sc.oil_acc or fields.filled(n, 0)
  sc.oil_bamt = sc.oil_bamt or fields.filled(n, 0)
  sc.oil_btag = sc.oil_btag or fields.filled(n, 0)
  sc.oil_mark = sc.oil_mark or fields.filled(n, -1)
  sc.oil_touched = sc.oil_touched or {}
end

-- tangent unit vector at c toward j
local function tangent(X, Y, Z, c, j)
  local cx, cy, cz = X[c], Y[c], Z[c]
  local dx, dy, dz = X[j] - cx, Y[j] - cy, Z[j] - cz
  local dn = dx * cx + dy * cy + dz * cz
  dx, dy, dz = dx - dn * cx, dy - dn * cy, dz - dn * cz
  local l = sqrt(dx * dx + dy * dy + dz * dz)
  if l < 1e-12 then return 0, 0, 0 end
  return dx / l, dy / l, dz / l
end

function sys.rebuild(W)
  local cfg = cfgmod.OCEAN
  local n, g = W.n, W.grid
  local X, Y, Z = g.X, g.Y, g.Z
  local nbr, cnt = g.nbr, g.nbr_count
  local ocean = W.geo.ocean
  local wind_at = require("sim.systems.moisture").wind_at
  local ss = W.sscale
  local kd = cfg.oil_diffusion / (ss * ss)
  local cur = W.currents or {}
  W.currents = cur
  for s = 0, 3 do
    local t = cur[s + 1] or {}
    cur[s + 1] = t
    t.w = t.w or {}
    t.self = t.self or {}
    t.vx, t.vy, t.vz = t.vx or {}, t.vy or {}, t.vz or {}
    local w, self = t.w, t.self
    for c = 1, n do
      local b = c * 6 - 6
      for k = 1, 6 do w[b + k] = 0 end
      self[c] = 1
      t.vx[c], t.vy[c], t.vz[c] = 0, 0, 0
      if ocean[c] == 1 then
        local vx, vy, vz = wind_at(W, c, s)
        local sp = sqrt(vx * vx + vy * vy + vz * vz)
        -- deflect along coasts: remove components pointing into land (two passes)
        for _ = 1, 2 do
          for k = 1, cnt[c] do
            local j = nbr[b + k]
            if ocean[j] == 0 then
              local tx, ty, tz = tangent(X, Y, Z, c, j)
              local d = vx * tx + vy * ty + vz * tz
              if d > 0 then vx, vy, vz = vx - d * tx, vy - d * ty, vz - d * tz end
            end
          end
        end
        local sp2 = sqrt(vx * vx + vy * vy + vz * vz)
        if sp2 > 1e-9 then
          local f = sp / sp2
          vx, vy, vz = vx * f, vy * f, vz * f
        end
        t.vx[c], t.vy[c], t.vz[c] = vx, vy, vz
        -- downstream ocean neighbours
        local tot = 0
        local no = 0
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if ocean[j] == 1 then
            no = no + 1
            if sp > 1e-9 then
              local tx, ty, tz = tangent(X, Y, Z, c, j)
              local d = (vx * tx + vy * ty + vz * tz) / sp
              if d > 0.2 then w[b + k] = d * d; tot = tot + d * d end
            end
          end
        end
        local alpha = sp * cfg.current_speed / ss
        if alpha > cfg.courant_max then alpha = cfg.courant_max end
        if tot <= 0 then alpha = 0 end
        local out = 0
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if ocean[j] == 1 then
            local v = (tot > 0 and alpha * w[b + k] / tot or 0) + kd / 6
            w[b + k] = v
            out = out + v
          end
        end
        self[c] = 1 - out
      end
    end
  end
  -- per-cell statics: light seasonality sign, land/ocean neighbour counts
  local O = W.ocean_static or {}
  W.ocean_static = O
  O.hs, O.nland, O.nocean = O.hs or {}, O.nland or {}, O.nocean or {}
  for c = 1, n do
    local lat = g.lat[c]
    O.hs[c] = (lat >= 0 and 1 or -1) * abs(sin(lat))
    local nl, no = 0, 0
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      if ocean[nbr[b + k]] == 1 then no = no + 1 else nl = nl + 1 end
    end
    O.nland[c], O.nocean[c] = nl, no
  end
end

-- Add `amount` of oil (cell-units) around ocean cell c, filling rings outward (≤ 1 per cell),
-- tagged `tag`. Used by test hooks, tankers and the Oil power. Returns cells touched.
-- amount: oil in level-4-equivalent cells of full cover (area-based, so a spill covers the
-- same part of the sea at any grid resolution, D-047).
function sys.spill(W, c, amount, tag)
  amount = amount / (W.sscale * W.sscale)
  local S = W.S.ocean
  local oil, coil, ioil = W.F.oil, W.C.oil, W.I.oil
  local ocean = W.geo.ocean
  local g = W.grid
  local ring = { c }
  local seen = { [c] = true }
  local touched = 0
  local list = S.oil
  local function add_active(x)
    for i = 1, S.oil_n do if list[i] == x then return end end
    S.oil_n = S.oil_n + 1
    list[S.oil_n] = x
  end
  while amount > 1e-9 and #ring > 0 do
    local room = 0
    for i = 1, #ring do room = room + (1 - oil[ring[i]]) end
    if room > 0 then
      local f = amount / room
      if f > 1 then f = 1 end
      for i = 1, #ring do
        local x = ring[i]
        local add = (1 - oil[x]) * f
        if add > 0 then
          oil[x] = oil[x] + add
          amount = amount - add
          blame.set(coil, ioil, x, tag or 0)
          add_active(x)
          touched = touched + 1
        end
      end
    end
    local nxt = {}
    for i = 1, #ring do
      local x = ring[i]
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if not seen[j] and ocean[j] == 1 then seen[j] = true; nxt[#nxt + 1] = j end
      end
    end
    table.sort(nxt)
    ring = nxt
  end
  return touched
end

-- Oil transport step over the sparse active set (every tick).
local function oil_step(W, S, cfg)
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I = W.F, W.C, W.I
  local oil, coil, ioil = F.oil, C.oil, I.oil
  local ocean = W.geo.ocean
  local O = W.ocean_static
  local nland, nocean = O.nland, O.nocean
  local season = math.floor((W.tick % 48) / 12)
  local cur = W.currents[season + 1]
  local cw, cself = cur.w, cur.self
  local sc = W.scratch
  local acc, bamt, btag, mark, touched = sc.oil_acc, sc.oil_bamt, sc.oil_btag, sc.oil_mark, sc.oil_touched
  local list, nlist = S.oil, S.oil_n
  local stamp = (sc.oil_stamp or 0) + 1   -- private counter (ticks can shift after spin-up)
  sc.oil_stamp = stamp
  local nt = 0
  -- 1. touched set: active cells and their neighbours
  for i = 1, nlist do
    local a = list[i]
    if mark[a] ~= stamp then
      mark[a] = stamp; nt = nt + 1; touched[nt] = a; acc[a] = 0; bamt[a] = 0; btag[a] = 0
    end
    local b = a * 6 - 6
    for k = 1, cnt[a] do
      local j = nbr[b + k]
      if mark[j] ~= stamp then
        mark[j] = stamp; nt = nt + 1; touched[nt] = j; acc[j] = 0; bamt[j] = 0; btag[j] = 0
      end
    end
  end
  -- 2. push: currents + diffusion (ocean), beaching (coastal ocean), leaching (shore)
  local beach, leach = cfg.beach, cfg.leach
  for i = 1, nlist do
    local a = list[i]
    local o = oil[a]
    if o > 0 then
      local ta = coil[a]
      local b = a * 6 - 6
      if ocean[a] == 1 then
        local nl = nland[a]
        local dep = 0
        if nl > 0 then dep = o * beach end
        local keep = o * cself[a] - dep
        if keep < 0 then dep = dep + keep; keep = 0 end
        acc[a] = acc[a] + keep
        if keep > bamt[a] then bamt[a] = keep; btag[a] = ta end
        local share = nl > 0 and dep / nl or 0
        for k = 1, cnt[a] do
          local j = nbr[b + k]
          local amt
          if ocean[j] == 1 then amt = o * cw[b + k] else amt = share end
          if amt > 0 then
            acc[j] = acc[j] + amt
            if amt > bamt[j] then bamt[j] = amt; btag[j] = ta end
          end
        end
      else
        local no = nocean[a]
        local lv = no > 0 and o * leach or 0
        local keep = o - lv
        acc[a] = acc[a] + keep
        if keep > bamt[a] then bamt[a] = keep; btag[a] = ta end
        if no > 0 then
          local share = lv / no
          for k = 1, cnt[a] do
            local j = nbr[b + k]
            if ocean[j] == 1 then
              acc[j] = acc[j] + share
              if share > bamt[j] then bamt[j] = share; btag[j] = ta end
            end
          end
        end
      end
    end
  end
  -- 3. decay, write back, shore damage, rebuild the active list (touched order)
  local dec_o = 0.5 ^ (1 / cfg.oil_half_life)
  local dec_s = 0.5 ^ (1 / cfg.shore_half_life)
  local omin = cfg.oil_min
  local grass, fert = F.grass, F.fertility
  local cg, ig, cfe, ife = C.grass, I.grass, C.fertility, I.fertility
  local sg, sfe = cfg.shore_grass, cfg.shore_fert
  local nn = 0
  local killed = 0
  for i = 1, nt do
    local c = touched[i]
    local isoc = ocean[c] == 1
    local v = acc[c] * (isoc and dec_o or dec_s)
    if v < omin then v = 0 elseif v > 1 then v = 1 end
    oil[c] = v
    if v > 0 then
      nn = nn + 1
      list[nn] = c
      local bt = btag[c]
      if bt ~= 0 and bt ~= coil[c] then coil[c] = bt; ioil[c] = 0 end
      if not isoc then
        -- beached oil smothers shore grass and poisons the soil
        local t = coil[c]
        local g0 = grass[c]
        local dg = sg * v * g0
        grass[c] = g0 - dg
        killed = killed + 0.5 * dg
        local f0 = fert[c]
        local df = sfe * v
        if df > f0 then df = f0 end
        fert[c] = f0 - df
        -- a single damage term: any real damage claims the tag (eps / 20)
        if t ~= 0 or cg[c] ~= 0 then blame.step(cg, ig, c, -dg, FG.eps * 0.05, FG.hold, -dg, t, 0, 0, 0, 0, 0, 0) end
        if t ~= 0 or cfe[c] ~= 0 then blame.step(cfe, ife, c, -df, FFE.eps * 0.05, FFE.hold, -df, t, 0, 0, 0, 0, 0, 0) end
      end
    elseif coil[c] ~= 0 then
      coil[c] = 0; ioil[c] = 0
    end
  end
  for i = nlist, nn + 1, -1 do list[i] = nil end
  S.oil_n = nn
  -- smothered shore grass rots back into co2 (carbon-neutral biosphere, D-008)
  if killed > 0 then W.G.co2_forest = (W.G.co2_forest or 0) - killed * cfgmod.VEG.k_co2 end
end

function sys.update(W)
  local cfg = cfgmod.OCEAN
  local S = W.S.ocean
  if S.oil_n > 0 then oil_step(W, S, cfg) end

  local stride = cfg.stride
  local oc = W.geo.ocean_cells
  local noc = #oc
  if noc == 0 then return end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I, P = W.F, W.C, W.I, W.P
  local plank, fish, pf, oil = F.plankton, F.fish, P.fish, F.oil
  local sst, ice, flow, silt = F.sst, F.ice, F.flow, F.silt
  local cpl, ipl, cfi, ifi = C.plankton, I.plankton, C.fish, I.fish
  local csst, coil, cflow, csilt = C.sst, C.oil, C.flow, C.silt
  local ocean = W.geo.ocean
  local up = W.static.upwelling
  local hs = W.ocean_static.hs
  local fref = S.fish_ref
  local toy = W.tick % 48
  local season = sin(2 * math.pi * toy / 48 + cfgmod.TEMP.season_phase)
  local dt = stride
  local r_p, nb, nu, nr, rref = cfg.r_plankton, cfg.nut_base, cfg.nut_up, cfg.nut_river, cfg.river_ref
  local lo, hi, cmin, cfloor, hot, hdie = cfg.sst_lo, cfg.sst_hi, cfg.sst_cold_min, cfg.cold_floor, cfg.sst_hot, cfg.heat_die
  local lamp, grz, koilp, okp = cfg.light_amp * season, cfg.graze, cfg.k_oil_p, cfg.oil_kill_p
  local r_f, h_f, m_f, koilf = cfg.r_fish, cfg.h_fish, cfg.m_fish, cfg.k_oil_f
  local hft, hf, spen, sref = cfg.heat_fish_t, cfg.heat_fish, cfg.silt_pen, cfg.silt_ref
  local kmig = cfg.fish_mig / (W.sscale * W.sscale) / 6
  local ema = dt / (cfg.ref_months * 4)
  local minp = cfg.min_pop
  local ftf = cfg.flow_tag_frac
  local peps, phold, feps, fhold = FP.eps, FP.hold, FF.eps, FF.hold
  local ph = W.tick % stride
  for i = 1 + ph, noc, stride do
    local c = oc[i]
    local p0, f0 = plank[c], fish[c]
    local s = sst[c]
    local o = oil[c]
    -- plankton
    local tf, heat = 1, 0
    if s < lo then
      local x = (s - cmin) / (lo - cmin)
      if x < 0 then x = 0 end
      tf = cfloor + (1 - cfloor) * x
    elseif s > hi then
      if s >= hot then tf = 0; heat = hdie * (s - hot)
      else tf = (hot - s) / (hot - hi) end
    end
    local fl = flow[c]
    local rv = 0
    if fl > 0 then rv = fl / rref; if rv > 1 then rv = 1 end; rv = nr * rv end
    local nut = nb + nu * up[c] + rv
    local oilf = 1 - koilp * o
    if oilf < 0 then oilf = 0 end
    local light = 1 + lamp * hs[c]
    local grow = r_p * nut * tf * light * (1 - ice[c]) * oilf * p0 * (1 - p0)
    local graze = grz * f0 * p0
    local pheat = heat * p0
    local poil = okp * o * p0
    local p1 = p0 + dt * (grow - graze - pheat - poil)
    if p1 < minp then p1 = 0 elseif p1 > 1 then p1 = 1 end
    plank[c] = p1
    -- fish
    local fnet = r_f * f0 * (p0 / (p0 + h_f)) * (1 - f0) - m_f * f0
    local foil = koilf * o * f0
    local fheat = 0
    if s > hft then fheat = hf * (s - hft) * f0 end
    local fsilt = 0
    local sv = silt[c]
    if sv > 0 then
      local x = sv / sref
      if x > 1 then x = 1 end
      fsilt = spen * x * f0
    end
    -- diffusion from ocean neighbours (last update's values); dominant tagged inflow
    local b = c * 6 - 6
    local pfc = pf[c]
    local mig, bin, tin = 0, 0, 0
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      if ocean[j] == 1 then
        local d = pf[j] - pfc
        mig = mig + d
        if d > bin then
          local tj = cfi[j]
          if tj ~= 0 then bin = d; tin = tj end
        end
      end
    end
    mig = mig * kmig
    if tin ~= 0 and dt * mig < ftf * f0 then tin = 0 end   -- equilibrium exchange carries no cause
    local f1 = f0 + dt * (fnet - foil - fheat - fsilt + mig)
    if f1 < minp then f1 = 0 elseif f1 > 1 then f1 = 1 end
    fish[c] = f1
    fref[c] = fref[c] + (f1 - fref[c]) * ema
    -- blame (fast path: nothing tagged → nothing to do)
    local ts, to, tp = csst[c], coil[c], cpl[c]
    local tfl = 0
    if rv > 0 then tfl = cflow[c]; if tfl == 0 then tfl = csilt[c] end end
    if tp ~= 0 or ts ~= 0 or to ~= 0 or tfl ~= 0 or cfi[c] ~= 0 then
      blame.step(cpl, ipl, c, p1 - p0, peps, phold, dt * grow, tfl, -dt * pheat, ts, -dt * poil, to, -dt * graze, cfi[c])
    end
    if tp ~= 0 or ts ~= 0 or to ~= 0 or tin ~= 0 or cfi[c] ~= 0 or (fsilt > 0 and csilt[c] ~= 0) then
      local hs_t = ts
      if fsilt > fheat then hs_t = csilt[c] end
      blame.step(cfi, ifi, c, f1 - f0, feps, fhold, dt * fnet, tp, -dt * foil, to,
        -dt * (fheat + fsilt), hs_t, dt * bin * kmig, tin)
    end
  end
  -- publish this phase's fish for neighbour reads next time
  for i = 1 + ph, noc, stride do
    local c = oc[i]
    pf[c] = fish[c]
  end
end

return sys
