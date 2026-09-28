-- Wild animals as herds and packs (docs/world/14_ECOLOGY.md §3, W4; session 5, D-070).
--
-- The animals live in groups that are real entities: a herd of deer, reindeer, antelope or boar
-- (W.S.wild h_*) and a pack of wolves or big cats (p_*), each with a centre cell, a head count, a
-- cause tag and its last move. A herd spends 40% of its time on its centre cell and the rest on
-- the cells about it (its range); a pack ranges two cells from its den (its territory). The
-- ecology is the calibrated density model of the planet layer (02 §5.9, D-012), evaluated on the
-- densities the groups make — so the planet fields F.grazers and F.predators are derived views
-- (animals per cell / WILD.gpc, WILD.ppc) and vegetation pressure, the detectors, the release
-- blame, powers and hunting keep their meaning:
--   food       = (grass + 0.5 crop) × (1 − snow_block × ice)          -- snow hides food in winter
--   K_eff      = K × food / (food + h_food)
--   a herd    += n (r × season − r H / K_eff  [≥ −starve_max]  − predation/H − fire − hunting)
--              (births come in the local spring and summer; the year's mean is r)
--   predation  = a P max(0, H − refuge) / (1 + a t_h max(0, H − refuge))   -- Holling II + refuge
--   a pack    += n (e predation / P − m − fire − culling)
-- where H and P are the cell densities all groups make and a group's rate is the mean over its
-- range, weighted by the time it spends on each cell. What a group does is its own:
--   herds      move to a better neighbouring cell (food per head after the other herds, safety from
--              packs, suited to the species, away from villages, fields and fire); reindeer and
--              antelope walk between a summer and a winter (dry-season) range every year; herds
--              past their size split, dwindled ones join a herd of their kind or are gone.
--   packs      hunt the herds of their territory; a hungry pack moves its den toward prey (never
--              onto another pack's ground); a big pack sends a pair away to found a pack where prey
--              is and no pack is; a dwindled pack is gone.
-- Nothing is spawned: a species gone from a landmass returns only by walking (sea ice ≥ 0.8) or by
-- the player's Introduce. Villagers hunt real herds (sys.hunt); powers cull and kill real groups
-- (sys.kill_in, sys.spawn).
--
-- Blame: a group carries its cause (h_tag / p_tag). Its growth carries the cause of a missing
-- pressure (predators far below their local normal while tagged: release, D-013) or, starving,
-- the food's cause; its losses the packs', the fire's, the hunters'. Each derived cell takes the
-- tag of the group that puts most animals on it; detectors re-tag cells with their events (a
-- boom), which the groups there adopt when the event descends from their cause.
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")

local sys = { name = "fauna" }
local floor, min, max = math.floor, math.min, math.max

local FGZ, FPD = fields.BY_NAME.grazers, fields.BY_NAME.predators
local FGR, FFO, FCR = fields.BY_NAME.grass, fields.BY_NAME.forest, fields.BY_NAME.crop

local HK = { "h_id", "h_sp", "h_cell", "h_home", "h_wint", "h_n", "h_tag", "h_idle", "h_prev", "h_moved" }
local PK = { "p_id", "p_sp", "p_cell", "p_n", "p_tag", "p_idle", "p_prev", "p_moved", "p_disp" }
sys.HK, sys.PK = HK, PK

local function scratch(W)
  local sc = W.scratch
  if sc.wl_H then return sc end
  local n = W.n
  local f = fields.filled
  for _, k in ipairs({ "H", "P", "dd", "pp", "fm", "pr", "gt", "pt", "ft", "gn", "rp", "best", "bw",
    "seen", "den", "owner" }) do sc["wl_" .. k] = f(n, 0) end
  sc.wl_stamp, sc.wl_dstamp = 0, 0
  sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl = {}, {}, {}, {}
  sc.wl_qc, sc.wl_qw, sc.wl_qs, sc.wl_ql = {}, {}, {}, {}
  sc.wl_list = {}
  return sc
end

function sys.init(W)
  local n = W.n
  local S = W.S.fauna
  if not S then
    local f = fields.filled
    S = { cull = f(n, 0), cull_tag = f(n, 0), hunt = f(n, 0), hunt_tag = f(n, 0),
          hnorm = f(n, 0), pnorm = f(n, 0) }
    W.S.fauna = S
  end
  scratch(W)
  if not W.S.wild then
    local A = { nh = 0, np = 0, next_id = 1 }
    for _, k in ipairs(HK) do A[k] = {} end
    for _, k in ipairs(PK) do A[k] = {} end
    W.S.wild = A
    -- a world from before the herds (an older save): its animals gather into groups
    local land = W.geo and W.geo.land_cells
    if land then
      local any = false
      for i = 1, #land do if W.F.grazers[land[i]] > 0 or W.F.predators[land[i]] > 0 then any = true; break end end
      if any then sys.seed(W) end
    end
  end
end

-- Walkable domain (derived; rebuilt when the coastline changes).
function sys.rebuild(W)
  local n = W.n
  local ocean, coast = W.geo.ocean, W.geo.coast
  local FZ = {}
  W.fauna_static = FZ
  local walk, cand = {}, {}
  for c = 1, n do
    if ocean[c] == 0 then walk[c] = 1
    elseif coast[c] == 1 then walk[c] = 0; cand[#cand + 1] = c
    else walk[c] = 0 end
  end
  FZ.walk, FZ.cand = walk, cand
  FZ.ring2 = {}
  FZ.terr = {}   -- pack territories by den cell (walkable first and second ring; with the ice)
  -- a cell's own warmth (the latitude's base and the altitude's lapse; static): species' habitats
  local T = W.temp_static
  local warm = {}
  for c = 1, n do warm[c] = T and T.base and T.base[c] and (T.base[c] + T.lapse[c]) or 10 end
  FZ.warm = warm
  FZ.ice_list, FZ.nice = {}, 0
  sys.refresh_ice(W)
end

-- Sea ice bridges: coastal ocean cells with ice ≥ ice_walk are walkable this month. Groups caught
-- on ice that thins below it drown.
function sys.refresh_ice(W)
  local FZ = W.fauna_static
  local cand, walk, list = FZ.cand, FZ.walk, FZ.ice_list
  local ice = W.F.ice
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
    end
  end
  local changed = k ~= FZ.nice
  if not changed then for i = 1, k do if FZ.ice_prev and FZ.ice_prev[i] ~= list[i] then changed = true; break end end end
  for i = #list, k + 1, -1 do list[i] = nil end
  FZ.nice = k
  if changed or not FZ.ice_prev then
    FZ.terr = {}
    local pv = {}
    for i = 1, k do pv[i] = list[i] end
    FZ.ice_prev = pv
  end
  local A = W.S.wild
  if A then
    local ocean = W.geo.ocean
    for i = 1, A.nh do if ocean[A.h_cell[i]] == 1 and walk[A.h_cell[i]] == 0 then A.h_n[i] = 0 end end
    for i = 1, A.np do if ocean[A.p_cell[i]] == 1 and walk[A.p_cell[i]] == 0 then A.p_n[i] = 0 end end
  end
end

-- The cells two steps from c, cached (static geography).
local function ring2(W, c)
  local FZ = W.fauna_static
  local r = FZ.ring2[c]
  if r then return r end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local seen = { [c] = true }
  local r1 = {}
  local b = c * 6 - 6
  for k = 1, cnt[c] do local j = nbr[b + k]; seen[j] = true; r1[#r1 + 1] = j end
  r = {}
  for i = 1, #r1 do
    local x = r1[i]
    local bx = x * 6 - 6
    for k = 1, cnt[x] do
      local j = nbr[bx + k]
      if not seen[j] then seen[j] = true; r[#r + 1] = j end
    end
  end
  FZ.ring2[c] = r
  return r
end
sys.ring2 = ring2

-- Cells a herd can use: land (or frozen sea), not a village (people are dangerous).
local function herd_ok(walk, land_use, c)
  return walk[c] == 1 and land_use[c] >= 0
end

---------------------------------------------------------------------------------------------
-- Groups

local function add_herd(W, sp, cell, n, tag)
  local A = W.S.wild
  local i = A.nh + 1
  A.nh = i
  A.h_id[i], A.h_sp[i], A.h_cell[i], A.h_home[i], A.h_wint[i] = A.next_id, sp, cell, cell, 0
  A.h_n[i], A.h_tag[i], A.h_idle[i], A.h_prev[i], A.h_moved[i] = n, tag or 0, 0, cell, W.hour or 0
  A.next_id = A.next_id + 1
  return i
end

local function add_pack(W, sp, cell, n, tag)
  local A = W.S.wild
  local i = A.np + 1
  A.np = i
  A.p_id[i], A.p_sp[i], A.p_cell[i], A.p_n[i] = A.next_id, sp, cell, n
  A.p_tag[i], A.p_idle[i], A.p_prev[i], A.p_moved[i] = tag or 0, 0, cell, W.hour or 0
  A.p_disp[i] = floor((W.hour or 0) / 8064)
  A.next_id = A.next_id + 1
  return i
end

-- Remove group i: the last takes its slot (deterministic, the arrays stay dense).
local function drop(A, keys, i, count)
  local last = A[count]
  for k = 1, #keys do
    local t = A[keys[k]]
    t[i] = t[last]
    t[last] = nil
  end
  A[count] = last - 1
end

-- Species of a new herd from the landscape at c: 1 deer, 2 reindeer, 3 antelope, 4 boar; of a new
-- pack: 1 wolves, 2 big cats.
local function warmth(W, c)
  return W.fauna_static.warm[c]
end
local function grazer_species(W, c)
  local tm = warmth(W, c)
  local fo = W.F.forest[c]
  if tm < 1 or W.F.biome[c] == 10 then return 2 end
  if tm > 18 and fo < 0.5 then return 3 end
  if fo > 0.45 and c % 3 == 0 then return 4 end
  return 1
end
local function predator_species(W, c)
  if warmth(W, c) > 18 and W.F.forest[c] < 0.5 then return 2 end
  return 1
end
sys.grazer_species, sys.predator_species = grazer_species, predator_species

-- How well a cell suits a grazer species (where herds choose to be; the rates are shared).
local function habitat(W, sp, c)
  local F = W.F
  local fo, gr = F.forest[c], F.grass[c]
  if sp == 1 then return 0.6 + 0.8 * min(fo, 1 - fo) + 0.2 * gr             -- deer: woodland edges
  elseif sp == 2 then                                                     -- reindeer: the cold
    local tm = warmth(W, c)
    return tm < 4 and 1.2 or max(0.3, 1.2 - (tm - 4) * 0.08)
  elseif sp == 3 then return (warmth(W, c) > 14 and 1.1 or 0.6) * (1.2 - 0.8 * fo)   -- antelope
  end
  return 0.5 + fo                                                          -- boar: forest
end

-- The winter (dry-season) range of a migrating herd: the warmest, grassiest walkable cell a few
-- steps from its summer range (static geography; chosen once).
local function winter_range(W, c)
  local cfg = cfgmod.WILD
  local g = W.grid
  local walk, lu = W.fauna_static.walk, W.F.land_use
  local best, bs = c, -1e9
  local seen = { [c] = 0 }
  local q, head = { c }, 1
  while head <= #q do
    local x = q[head]; head = head + 1
    local d = seen[x]
    local s = warmth(W, x) + 8 * W.F.grass[x] - 0.5 * d
    if d >= 2 and s > bs then best, bs = x, s end
    if d < cfg.migrate_reach then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if seen[j] == nil and herd_ok(walk, lu, j) then seen[j] = d + 1; q[#q + 1] = j end
      end
    end
  end
  return best
end

---------------------------------------------------------------------------------------------
-- Ranges and densities

-- Every group's range (cells and time shares, in the scratch lists) and the densities they make;
-- returns the touched cells.
local function densities(W, A, sc, cfg)
  local H, P = sc.wl_H, sc.wl_P
  local seen = sc.wl_seen
  local stamp = sc.wl_stamp + 1
  sc.wl_stamp = stamp
  local list = sc.wl_list
  local nl = 0
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local walk, lu = W.fauna_static.walk, W.F.land_use
  local rc, rw, rs, rl = sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl
  local qc, qw, qs, ql = sc.wl_qc, sc.wl_qw, sc.wl_qs, sc.wl_ql
  local igpc, ippc = 1 / cfg.gpc, 1 / cfg.ppc
  local w0h = cfg.herd_w0
  -- (ranges by centre cell are cached; the cache is dropped when villages or the ice change what
  -- herds can use: the key below)
  local FZ = W.fauna_static
  local key = 0
  local land = W.geo.land_cells
  for i = 1, #land do local c = land[i]; if lu[c] < 0 then key = (key * 31 + c) % 2147483647 end end
  key = (key * 31 + FZ.nice) % 2147483647
  for i = 1, FZ.nice do key = (key * 31 + FZ.ice_list[i]) % 2147483647 end
  if FZ.hkey ~= key then FZ.hkey, FZ.hrange = key, {} end
  local hrange = FZ.hrange
  local off = 0
  for i = 1, A.nh do
    local c = A.h_cell[i]
    local t = hrange[c]
    if not t then
      t = {}
      local b = c * 6 - 6
      local L = 0
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if walk[j] == 1 and lu[j] >= 0 then L = L + 1; t[L] = j end
      end
      t.w0 = L > 0 and w0h or 1
      t.wr = L > 0 and (1 - t.w0) / L or 0
      t.L = L + 1
      t[L + 1] = c
      hrange[c] = t
    end
    local L = t.L
    local wr = t.wr
    for k = 1, L - 1 do rc[off + k], rw[off + k] = t[k], wr end
    rc[off + L], rw[off + L] = c, t.w0
    rs[i], rl[i] = off, L
    local n = A.h_n[i] * igpc
    for k = off + 1, off + L do
      local x = rc[k]
      if seen[x] ~= stamp then seen[x] = stamp; H[x], P[x] = 0, 0; nl = nl + 1; list[nl] = x end
      H[x] = H[x] + n * rw[k]
    end
    off = off + L
  end
  off = 0
  local pw = cfg.pack_w
  local terr = W.fauna_static.terr
  for i = 1, A.np do
    local c = A.p_cell[i]
    local t = terr[c]
    if not t then
      t = {}
      local n1 = 0
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if walk[j] == 1 then n1 = n1 + 1; t[n1] = j end
      end
      -- (the second ring through walkable cells only: no territory across open water)
      local n2 = 0
      local mark = { [c] = true }
      for k = 1, n1 do mark[t[k]] = true end
      for k = 1, n1 do
        local x = t[k]
        local bx = x * 6 - 6
        for q = 1, cnt[x] do
          local j = nbr[bx + q]
          if not mark[j] and walk[j] == 1 then mark[j] = true; n2 = n2 + 1; t[n1 + n2] = j end
        end
      end
      local tot = pw[1] + (n1 > 0 and pw[2] or 0) + (n2 > 0 and pw[3] or 0)
      t.w1 = n1 > 0 and pw[2] / n1 / tot or 0
      t.w2 = n2 > 0 and pw[3] / n2 / tot or 0
      t.w0, t.n1, t.n2 = pw[1] / tot, n1, n2
      terr[c] = t
    end
    local n1, n2 = t.n1, t.n2
    local L = n1 + n2 + 1
    local w1, w2 = t.w1, t.w2
    for k = 1, n1 do qc[off + k], qw[off + k] = t[k], w1 end
    for k = n1 + 1, n1 + n2 do qc[off + k], qw[off + k] = t[k], w2 end
    qc[off + L], qw[off + L] = c, t.w0
    qs[i], ql[i] = off, L
    local n = A.p_n[i] * ippc
    for k = off + 1, off + L do
      local x = qc[k]
      if seen[x] ~= stamp then seen[x] = stamp; H[x], P[x] = 0, 0; nl = nl + 1; list[nl] = x end
      P[x] = P[x] + n * qw[k]
    end
    off = off + L
  end
  for i = #list, nl + 1, -1 do list[i] = nil end
  return list, nl, stamp
end

-- The local week of year at cell c (the southern calendar runs half a year behind).
local function local_week(W, c)
  local wk = floor(W.hour / 168)
  if W.grid.lat[c] < 0 then wk = wk + 24 end
  return wk % 48
end

---------------------------------------------------------------------------------------------
-- The step (every WILD.every ticks, dt = that)

-- Per cell: the per-head rates (density-dependent deaths, predation, fire; the packs' net), the
-- causes of each, and the vegetation the grazers take.
local function ecology(W, sc, fcfg, dt, list, nl)
  local F, C, I = W.F, W.C, W.I
  local S = W.S.fauna
  local grass, forest, crop, ice, fire = F.grass, F.forest, F.crop, F.ice, F.fire
  local H, P = sc.wl_H, sc.wl_P
  local dd_, pp_, fm_, pr_ = sc.wl_dd, sc.wl_pp, sc.wl_fm, sc.wl_pr
  local gt, pt, ft, gn, rp = sc.wl_gt, sc.wl_pt, sc.wl_ft, sc.wl_gn, sc.wl_rp
  local cull = S.cull
  local hnorm, pnorm = S.hnorm, S.pnorm
  local Kg, hfood, cfood, sblock = fcfg.K_grazer, fcfg.h_food, fcfg.crop_food, fcfg.snow_block
  local ap, th, refuge, ep, mp = fcfg.a_pred, fcfg.t_handle, fcfg.refuge, fcfg.e_pred, fcfg.m_pred
  local kgr, hgr, kbr, hbr, kra, hra = fcfg.graze, fcfg.h_graze, fcfg.browse, fcfg.h_browse, fcfg.raid, fcfg.h_raid
  local kov, hstart, bbase, fmort = fcfg.overgraze, fcfg.overgraze_from, fcfg.browse_base, fcfg.fire_mort
  local bmax = cfgmod.WILD.browse_crowd
  local relf, above = fcfg.release_frac, fcfg.pressure_above
  local rmin_h, rmin_p = fcfg.release_min.grazers, fcfg.release_min.predators
  local descends, eparent = blame.descends, W.E.parent
  local cgz, cpd = C.grazers, C.predators
  local cgr, igr, cfo, ifo, ccr, icr = C.grass, I.grass, C.forest, I.forest, C.crop, I.crop
  local cfire = C.fire
  local greps, grhold, foeps, fohold = FGR.eps, FGR.hold, FFO.eps, FFO.hold
  local grass_c = cfgmod.VEG.grass_c
  local rg = fcfg.r_grazer
  local eaten = 0
  for i = 1, nl do
    local c = list[i]
    local Hc, Pc = H[c], P[c]
    local hn, pn = hnorm[c], pnorm[c]
    local gr, fo, cr = grass[c], forest[c], crop[c]
    local food = (gr + cfood * cr) * (1 - sblock * ice[c])
    local keff = Kg * food / (food + hfood)
    dd_[c] = keff > 1e-6 and rg * Hc / keff or 1e9
    local hx = Hc - refuge
    local pred = 0
    if hx > 0 and Pc > 0 then pred = ap * Pc * hx / (1 + ap * th * hx) end
    pp_[c] = Hc > 0 and pred / Hc or 0
    local fi = fire[c]
    local fm = fi > 0 and fmort * fi or 0
    fm_[c] = fm
    pr_[c] = Pc > 0 and (ep * pred / Pc) or 0   -- the packs' gain per head (their losses below)
    -- causes: a pressure carries its cause only above normal (D-013); a missing pressure's cause
    -- travels into the growth it allows (release); starving grazers carry the food's cause
    local tH, tP = cgz[c], cpd[c]
    local tHa, tPa = 0, 0
    if tH ~= 0 and Hc > above * hn then tHa = tH end
    if tP ~= 0 and Pc > above * pn then tPa = tP end
    pt[c], gn[c] = tPa, tHa
    ft[c] = fi > 0 and cfire[c] or 0
    local relg = 0
    if tP ~= 0 and pn > rmin_p and Pc < relf * pn then
      relg = tP
      if tH ~= 0 and tH ~= tP and descends(eparent, tH, tP) then relg = tH end
    end
    if Hc > keff then relg = cgr[c] end
    gt[c] = relg
    local relp = 0
    if tH ~= 0 and hn > rmin_h and Hc < relf * hn then
      relp = tH
      if tP ~= 0 and tP ~= tH and descends(eparent, tP, tH) then relp = tP end
    end
    rp[c] = relp
    -- vegetation pressure (blamed with the grazers' cause when they are abnormally many)
    if Hc > 0 then
      local over = keff > 1e-6 and Hc / keff or 2
      if over > bmax then over = bmax end
      local hunger = over > 1 and 1 or over
      local starve = hunger > hstart and (hunger - hstart) / (1 - hstart) or 0
      local gg = dt * kgr * Hc * (1 + kov * starve) * gr / (gr + hgr)
      if gg > gr then gg = gr end
      grass[c] = gr - gg
      -- (a herd crowded past what the grass feeds turns to the trees: saplings, bark, leaves —
      -- how too many deer stop a forest regrowing; up to bmax x the browsing of a full range)
      local bb = dt * kbr * Hc * (bbase + over) * fo / (fo + hbr)
      if bb > fo then bb = fo end
      forest[c] = fo - bb
      eaten = eaten + grass_c * gg + bb
      if tHa ~= 0 or cgr[c] ~= 0 then blame.step(cgr, igr, c, -gg, greps, grhold, -gg, tHa, 0, 0, 0, 0, 0, 0) end
      if tHa ~= 0 or cfo[c] ~= 0 then blame.step(cfo, ifo, c, -bb, foeps, fohold, -bb, tHa, 0, 0, 0, 0, 0, 0) end
      if cr > 0 then
        local rr = dt * kra * Hc * cr / (cr + hra)
        if rr > cr then rr = cr end
        crop[c] = cr - rr
        eaten = eaten + grass_c * rr
        if tHa ~= 0 or ccr[c] ~= 0 then blame.step(ccr, icr, c, -rr, FCR.eps, FCR.hold, -rr, tHa, 0, 0, 0, 0, 0, 0) end
      end
    end
  end
  -- animals breathe out what they eat: the biosphere stays carbon-neutral (D-008, D-067)
  if eaten > 0 then W.G.co2_forest = (W.G.co2_forest or 0) - eaten * cfgmod.VEG.k_co2 end
end

-- Each group's numbers by its range's per-head rates, blamed on the group.
local function demography(W, A, sc, cfg, fcfg, dt)
  local dd_, pp_, fm_, pr_ = sc.wl_dd, sc.wl_pp, sc.wl_fm, sc.wl_pr
  local gt, pt, ft, gn, rp = sc.wl_gt, sc.wl_pt, sc.wl_ft, sc.wl_gn, sc.wl_rp
  local rc, rw, rs, rl = sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl
  local qc, qw, qs, ql = sc.wl_qc, sc.wl_qw, sc.wl_qs, sc.wl_ql
  local S = W.S.fauna
  local hunt, htag, ctag, cull = S.hunt, S.hunt_tag, S.cull_tag, S.cull
  local mp = fcfg.m_pred
  local rg, smax = fcfg.r_grazer, fcfg.starve_max
  local season = cfg.birth_season
  local geps, ghold, peps, phold = FGZ.eps * cfg.gpc, FGZ.hold, FPD.eps * cfg.ppc, FPD.hold
  for i = 1, A.nh do
    local o, L = rs[i], rl[i]
    local births = rg * season[floor(local_week(W, A.h_cell[i]) / 12) + 1]
    local dd, pp, fm, hu = 0, 0, 0, 0
    local tg, tp, tf, th = 0, 0, 0, 0
    local wg, wp, wf, wh = 0, 0, 0, 0
    for k = o + 1, o + L do
      local c, w = rc[k], rw[k]
      dd = dd + w * dd_[c]
      pp = pp + w * pp_[c]
      fm = fm + w * fm_[c]
      hu = hu + w * hunt[c]
      if gt[c] ~= 0 and w > wg then tg, wg = gt[c], w end
      if pt[c] ~= 0 and w > wp then tp, wp = pt[c], w end
      if ft[c] ~= 0 and w > wf then tf, wf = ft[c], w end
      if htag[c] ~= 0 and w > wh then th, wh = htag[c], w end
    end
    local growth = births - dd
    if growth < -smax then growth = -smax end
    local n = A.h_n[i]
    local d = n * dt * (growth - pp - fm - hu)
    local n1 = n + d
    if n1 < 0 then n1 = 0; d = -n end
    A.h_n[i] = n1
    if A.h_tag[i] ~= 0 or tg ~= 0 or tp ~= 0 or tf ~= 0 or th ~= 0 then
      blame.step(A.h_tag, A.h_idle, i, d, geps, ghold, n * dt * growth, tg, -n * dt * pp, tp,
        -n * dt * fm, tf, -n * dt * hu, th)
    end
  end
  for i = 1, A.np do
    local o, L = qs[i], ql[i]
    local gain, loss = 0, 0
    local tg, tr, tf, tc = 0, 0, 0, 0
    local wg, wr, wf, wc = 0, 0, 0, 0
    for k = o + 1, o + L do
      local c, w = qc[k], qw[k]
      gain = gain + w * pr_[c]
      loss = loss + w * (fm_[c] + cull[c])
      if gn[c] ~= 0 and w > wg then tg, wg = gn[c], w end
      if rp[c] ~= 0 and w > wr then tr, wr = rp[c], w end
      if ft[c] ~= 0 and w > wf then tf, wf = ft[c], w end
      if ctag[c] ~= 0 and w > wc then tc, wc = ctag[c], w end
    end
    -- wolves breed once a year: what the pack eats becomes pups in the spring (the year's gain,
    -- by season as the herds' births), so the packs answer the herds a season late
    local rate = gain * season[floor(local_week(W, A.p_cell[i]) / 12) + 1] - mp - loss
    local n = A.p_n[i]
    local d = n * dt * rate
    local n1 = n + d
    if n1 < 0 then n1 = 0; d = -n end
    A.p_n[i] = n1
    if A.p_tag[i] ~= 0 or tg ~= 0 or tr ~= 0 or tf ~= 0 or tc ~= 0 then
      -- (the prey's cause when it grows, the release when prey is missing; fire, culling)
      blame.step(A.p_tag, A.p_idle, i, d, peps, phold, d > 0 and d or 0, tg, d < 0 and d or 0, tr, 0, tf, 0, tc)
    end
  end
end

-- A herd's view of cell x: food per head after the other herds, safety, its kind of land.
local function score(W, sc, cfg, fcfg, sp, x, own)
  local F = W.F
  local food = (F.grass[x] + fcfg.crop_food * F.crop[x]) * (1 - fcfg.snow_block * F.ice[x])
  local crowd = sc.wl_H[x] - own
  if crowd < 0 then crowd = 0 end
  local s = (0.05 + food) / (1 + fcfg.fear * sc.wl_P[x]) / (1 + crowd * cfg.crowd) * habitat(W, sp, x)
  if F.land_use[x] > 0 then s = s * cfg.fields_fear end
  if F.fire[x] > 0 then s = s * 0.05 end
  return s
end

-- Herds move, migrate, split and join; packs follow their prey and disperse; the dwindled go.
local function behave(W, A, sc, cfg, fcfg)
  local g = W.grid
  local F = W.F
  local H = sc.wl_H
  local fire = F.fire
  local hour = W.hour
  local nbr, cnt = g.nbr, g.nbr_count
  local walk, lu = W.fauna_static.walk, F.land_use
  local igpc = 1 / cfg.gpc
  local upd = floor(W.tick / cfg.every)
  local dd_ = sc.wl_dd
  local rgi = 1 / fcfg.r_grazer
  local rc, rw, rs, rl = sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl
  local nh0 = A.nh
  local i = 1
  while i <= A.nh do
    local sp, c = A.h_sp[i], A.h_cell[i]
    local n = A.h_n[i]
    -- how crowded its range is: animals against what the grass carries (H / K_eff)
    local sat = 0
    if i <= nh0 and rs[i] then
      for k = rs[i] + 1, rs[i] + rl[i] do sat = sat + rw[k] * dd_[rc[k]] * rgi end
    end
    local own = n * igpc * cfg.herd_w0
    local migrates = cfg.migrates[sp]
    local target = nil
    if migrates then
      local wk = local_week(W, c)
      local winter = wk >= cfg.migrate_weeks[1] or wk < cfg.migrate_weeks[2]
      if A.h_wint[i] == 0 then A.h_wint[i] = winter_range(W, A.h_home[i]) end
      target = winter and A.h_wint[i] or A.h_home[i]
      if target == c then target = nil end
    end
    local best = c
    local b = c * 6 - 6
    local look = target or fire[c] > 0 or sat > 1 or (A.h_id[i] + upd) % cfg.look_every == 0
    if not look then
      -- (settled this time: its range is still good enough)
    elseif target then
      -- on the way: the usable neighbour nearest the seasonal range
      local tx, ty, tz = g.X[target], g.Y[target], g.Z[target]
      local bd = 1e9
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if herd_ok(walk, lu, j) then
          local dx, dy, dz = g.X[j] - tx, g.Y[j] - ty, g.Z[j] - tz
          local d = dx * dx + dy * dy + dz * dz
          if d < bd then best, bd = j, d end
        end
      end
    else
      local bs = fire[c] > 0 and 0 or score(W, sc, cfg, fcfg, sp, c, own) * cfg.move_gain
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if herd_ok(walk, lu, j) then
          local s = score(W, sc, cfg, fcfg, sp, j, 0)
          if s > bs then best, bs = j, s end
        end
      end
    end
    if best ~= c then
      A.h_prev[i], A.h_cell[i], A.h_moved[i] = c, best, hour
      if not migrates then A.h_home[i] = best end
      -- (a migrating herd covers a second step when the way is long)
      if target and best ~= target then
        local c2 = best
        local b2 = c2 * 6 - 6
        local tx, ty, tz = g.X[target], g.Y[target], g.Z[target]
        local dx, dy, dz = g.X[c2] - tx, g.Y[c2] - ty, g.Z[c2] - tz
        local bd = dx * dx + dy * dy + dz * dz
        for k = 1, cnt[c2] do
          local j = nbr[b2 + k]
          if herd_ok(walk, lu, j) then
            dx, dy, dz = g.X[j] - tx, g.Y[j] - ty, g.Z[j] - tz
            local d = dx * dx + dy * dy + dz * dz
            if d < bd then best, bd = j, d end
          end
        end
        A.h_cell[i] = best
      end
    end
    -- past its size a herd splits, and a crowded one (more animals than its range's grass carries)
    -- sends a part to the least crowded, best-fed cell about: how numbers spread into free range
    local spc = cfg.grazers[sp]
    if n > spc.max or (sat > cfg.disperse_sat and n >= 2 * spc.min) then
      local c1 = A.h_cell[i]
      local b1 = c1 * 6 - 6
      local cb, sb = nil, -1
      for k = 1, cnt[c1] do
        local j = nbr[b1 + k]
        if herd_ok(walk, lu, j) then
          local s = score(W, sc, cfg, fcfg, sp, j, 0)
          if s > sb then cb, sb = j, s end
        end
      end
      if cb then
        local part = n * cfg.split_share
        A.h_n[i] = n - part
        local ni = add_herd(W, sp, cb, part, A.h_tag[i])
        A.h_prev[ni] = c1
      end
    end
    -- a dwindled herd joins one of its kind on or next to its cell
    if A.h_n[i] < spc.min then
      local c1 = A.h_cell[i]
      local b1 = c1 * 6 - 6
      for jx = 1, A.nh do
        if jx ~= i and A.h_sp[jx] == sp and A.h_n[jx] >= spc.min then
          local cj = A.h_cell[jx]
          local near = cj == c1
          if not near then for k = 1, cnt[c1] do if nbr[b1 + k] == cj then near = true; break end end end
          if near then A.h_n[jx] = A.h_n[jx] + A.h_n[i]; A.h_n[i] = 0; break end
        end
      end
    end
    if A.h_n[i] < cfg.gone then drop(A, HK, i, "nh") else i = i + 1 end
  end
  -- packs: the dens and the ground about them (stamped: another pack's ground)
  local den, owner = sc.wl_den, sc.wl_owner
  local ds = sc.wl_dstamp + 1
  sc.wl_dstamp = ds
  for p = 1, A.np do
    local c = A.p_cell[p]
    den[c], owner[c] = ds, A.p_id[p]
    local b = c * 6 - 6
    for k = 1, cnt[c] do local j = nbr[b + k]; den[j], owner[j] = ds, A.p_id[p] end
  end
  local ocean = W.geo.ocean
  i = 1
  while i <= A.np do
    local sp, c = A.p_sp[i], A.p_cell[i]
    local n = A.p_n[i]
    local spc = cfg.predators[sp]
    -- prey about the den; a hungry pack (or one burned out) moves toward more, monthly at most
    if hour - A.p_moved[i] >= 672 then
      local b = c * 6 - 6
      local here = 2 * H[c]
      for k = 1, cnt[c] do here = here + H[nbr[b + k]] end
      if here < cfg.pack_hungry or fire[c] > 0 then
        local best, bs = nil, here
        local me = A.p_id[i]
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          -- (not onto another pack's ground: its den and the cells about it)
          if walk[j] == 1 and ocean[j] == 0 and not (den[j] == ds and owner[j] ~= me) then
            local s = 2 * H[j]
            local bj = j * 6 - 6
            for q = 1, cnt[j] do s = s + H[nbr[bj + q]] end
            if s > bs then best, bs = j, s end
          end
        end
        if best then
          A.p_prev[i], A.p_cell[i], A.p_moved[i] = c, best, hour
          den[best], owner[best] = ds, me
        end
      end
    end
    -- young wolves leave in the spring (every year from a pack big enough, or when it is too big):
    -- a pair founds a pack where prey is and no pack is — how wolves colonise new ground
    local year = floor(hour / 8064)
    local lw = local_week(W, A.p_cell[i])
    if n > spc.max or (n >= cfg.disperse_min and A.p_disp[i] ~= year and lw >= 8 and lw < 12) then
      A.p_disp[i] = year
      local site = sys.pack_site(W, A, A.p_cell[i])
      if site then
        A.p_n[i] = n - 2
        local ni = add_pack(W, sp, site, 2, A.p_tag[i])
        A.p_prev[ni] = A.p_cell[i]
        den[site], owner[site] = ds, A.p_id[ni]
      end
    end
    if A.p_n[i] < cfg.gone then drop(A, PK, i, "np") else i = i + 1 end
  end
end

-- A den for a dispersing pair: the land cell 3-6 steps from `from` with the most prey about it
-- and no other pack's den within two steps.
function sys.pack_site(W, A, from)
  local cfg = cfgmod.WILD
  local g = W.grid
  local sc = scratch(W)
  local H = sc.wl_H
  local walk = W.fauna_static.walk
  local ocean = W.geo.ocean
  -- ground claimed: two steps about every den
  local claimed = {}
  for p = 1, A.np do
    local d = A.p_cell[p]
    claimed[d] = true
    local b = d * 6 - 6
    for k = 1, g.nbr_count[d] do claimed[g.nbr[b + k]] = true end
    for _, j in ipairs(ring2(W, d)) do claimed[j] = true end
  end
  local seen = { [from] = 0 }
  local q, head = { from }, 1
  local best, bs = nil, cfg.pack_min_prey
  while head <= #q do
    local x = q[head]; head = head + 1
    local d = seen[x]
    if d >= 3 and ocean[x] == 0 and not claimed[x] and H[x] > bs then best, bs = x, H[x] end
    if d < 6 then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if seen[j] == nil and walk[j] == 1 then seen[j] = d + 1; q[#q + 1] = j end
      end
    end
  end
  return best
end

-- The derived views: F.grazers / F.predators (density, capped at 1), their prev buffers, the
-- cells' tags (the group that puts most animals there), the per-cell normals for the detectors.
local function derive(W, A, sc, cfg, fcfg, dt)
  local F, Pv, C, I = W.F, W.P, W.C, W.I
  local S = W.S.fauna
  local gz, pd = F.grazers, F.predators
  local list, nl, stamp = densities(W, A, sc, cfg)
  local H, P = sc.wl_H, sc.wl_P
  local seen = sc.wl_seen
  local best, bw = sc.wl_best, sc.wl_bw
  -- cells that hold no animals now (every cell: a reloaded world has no list of the last ones)
  local cg, cp = C.grazers, C.predators
  local ig, ip = I.grazers, I.predators
  local pg, pp = Pv.grazers, Pv.predators   -- (prev buffers, where the field keeps one)
  local ghold, phold = FGZ.hold, FPD.hold
  local step = dt > 0 and dt or 0
  for c = 1, W.n do
    if seen[c] ~= stamp and (gz[c] ~= 0 or pd[c] ~= 0 or cg[c] ~= 0 or cp[c] ~= 0) then
      gz[c], pd[c] = 0, 0
      if pg then pg[c] = 0 end
      if pp then pp[c] = 0 end
      -- (an emptied cell's cause idles out over the field's hold, as it always did)
      if cg[c] ~= 0 then local x = ig[c] + step; if x >= ghold then cg[c], ig[c] = 0, 0 else ig[c] = x end end
      if cp[c] ~= 0 then local x = ip[c] + step; if x >= phold then cp[c], ip[c] = 0, 0 else ip[c] = x end end
    end
  end
  for i = 1, nl do
    local c = list[i]
    local h, p = H[c], P[c]
    if h > 1 then h = 1 end
    if p > 1 then p = 1 end
    gz[c], pd[c] = h, p
    if pg then pg[c] = h end
    if pp then pp[c] = p end
    best[c], bw[c] = 0, 0
  end
  local rc, rw, rs, rl = sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl
  for i = 1, A.nh do
    local n, t = A.h_n[i], A.h_tag[i]
    for k = rs[i] + 1, rs[i] + rl[i] do
      local c = rc[k]
      local x = n * rw[k]
      if x > bw[c] then bw[c], best[c] = x, t end
    end
  end
  for i = 1, nl do
    local c = list[i]
    if H[c] > 0 and C.grazers[c] ~= best[c] then C.grazers[c], I.grazers[c] = best[c], 0 end
    if H[c] <= 0 and C.grazers[c] ~= 0 then
      local x = I.grazers[c] + (dt > 0 and dt or 0)
      if x >= FGZ.hold then C.grazers[c], I.grazers[c] = 0, 0 else I.grazers[c] = x end
    end
    best[c], bw[c] = 0, 0
  end
  local qc, qw, qs, ql = sc.wl_qc, sc.wl_qw, sc.wl_qs, sc.wl_ql
  for i = 1, A.np do
    local n, t = A.p_n[i], A.p_tag[i]
    for k = qs[i] + 1, qs[i] + ql[i] do
      local c = qc[k]
      local x = n * qw[k]
      if x > bw[c] then bw[c], best[c] = x, t end
    end
  end
  for i = 1, nl do
    local c = list[i]
    if P[c] > 0 and C.predators[c] ~= best[c] then C.predators[c], I.predators[c] = best[c], 0 end
    if P[c] <= 0 and C.predators[c] ~= 0 then
      local x = I.predators[c] + (dt > 0 and dt or 0)
      if x >= FPD.hold then C.predators[c], I.predators[c] = 0, 0 else I.predators[c] = x end
    end
    best[c], bw[c] = 0, 0
  end
  if dt > 0 then
    -- normals (every land cell: an emptied cell's normal decays)
    local ema = dt / (fcfg.ref_months * 4)
    local hnorm, pnorm = S.hnorm, S.pnorm
    local land = W.geo.land_cells
    for i = 1, #land do
      local c = land[i]
      local hn, pn = hnorm[c], pnorm[c]
      local h, p = gz[c], pd[c]
      if h ~= 0 or hn ~= 0 then
        hn = hn + (h - hn) * ema
        if hn < 1e-9 then hn = 0 end
        hnorm[c] = hn
      end
      if p ~= 0 or pn ~= 0 then
        pn = pn + (p - pn) * ema
        if pn < 1e-9 then pn = 0 end
        pnorm[c] = pn
      end
    end
  end
end

-- Detectors re-tag cells with the events they record (a boom, a crash): the groups there take
-- that event as their cause when it descends from theirs (the chain goes on through them).
local function adopt_tags(W, A)
  local C = W.C
  local par = W.E.parent
  local descends = blame.descends
  for i = 1, A.nh do
    local t = C.grazers[A.h_cell[i]]
    local h = A.h_tag[i]
    if t ~= 0 and t ~= h and (h == 0 or descends(par, t, h)) then A.h_tag[i], A.h_idle[i] = t, 0 end
  end
  for i = 1, A.np do
    local t = C.predators[A.p_cell[i]]
    local h = A.p_tag[i]
    if t ~= 0 and t ~= h and (h == 0 or descends(par, t, h)) then A.p_tag[i], A.p_idle[i] = t, 0 end
  end
end

function sys.update(W)
  local cfg = cfgmod.WILD
  local fcfg = cfgmod.FAUNA
  local A = W.S.wild
  local sc = scratch(W)
  local tick = W.tick
  if tick % 4 == 0 then sys.refresh_ice(W) end
  local every = cfg.every
  if tick % every ~= 0 then return end
  adopt_tags(W, A)
  local list, nl = densities(W, A, sc, cfg)
  ecology(W, sc, fcfg, every, list, nl)
  demography(W, A, sc, cfg, fcfg, every)
  behave(W, A, sc, cfg, fcfg)
  derive(W, A, sc, cfg, fcfg, every)
end

-- Rebuild the derived fields now (a load, a power acting on the groups).
function sys.refresh(W)
  derive(W, W.S.wild, scratch(W), cfgmod.WILD, cfgmod.FAUNA, 0)
end

---------------------------------------------------------------------------------------------
-- Seeding, powers, hunting

-- The densities the planet fields hold become herds and packs (spin-up, or an older save): cells
-- taken in order of density, each group gathering the animals of its range.
function sys.seed(W)
  local cfg = cfgmod.WILD
  local A = W.S.wild
  local F = W.F
  local g = W.grid
  local land = W.geo.land_cells
  local function gather(field, per, maxn, add, spf, wide)
    local rest = {}
    local order = {}
    for i = 1, #land do
      local c = land[i]
      rest[c] = field[c] * per
      if rest[c] > 0 then order[#order + 1] = c end
    end
    table.sort(order, function(a, b) if rest[a] ~= rest[b] then return rest[a] > rest[b] end return a < b end)
    for _, c in ipairs(order) do
      if rest[c] > 0.3 then
        local cells = { c }
        local b = c * 6 - 6
        for k = 1, g.nbr_count[c] do cells[#cells + 1] = g.nbr[b + k] end
        if wide then for _, j in ipairs(ring2(W, c)) do cells[#cells + 1] = j end end
        local n = 0
        for _, x in ipairs(cells) do
          local r = rest[x] or 0
          if r > 0 and n < maxn then
            local take = min(r, maxn - n)
            n = n + take
            rest[x] = r - take
          end
        end
        if n >= 2 then add(W, spf(W, c), c, n, 0) end
      end
    end
  end
  gather(F.grazers, cfg.gpc, cfg.seed_herd, add_herd, grazer_species, false)
  gather(F.predators, cfg.ppc, cfg.seed_pack, add_pack, predator_species, true)
  sys.refresh(W)
end

-- `frac` of the animals of `species` ("grazers" / "predators") on `cells` die: each group loses
-- that share of the time it spends there; their cause becomes `tag`.
function sys.kill_in(W, cells, species, frac, tag)
  local A = W.S.wild
  local sc = scratch(W)
  local cfg = cfgmod.WILD
  local set = {}
  for i = 1, #cells do set[cells[i]] = true end
  densities(W, A, sc, cfg)
  local herds = species == "grazers"
  local cs, ws, ss, ls
  if herds then cs, ws, ss, ls = sc.wl_rc, sc.wl_rw, sc.wl_rs, sc.wl_rl
  else cs, ws, ss, ls = sc.wl_qc, sc.wl_qw, sc.wl_qs, sc.wl_ql end
  local N = herds and A.h_n or A.p_n
  local T = herds and A.h_tag or A.p_tag
  local Id = herds and A.h_idle or A.p_idle
  local count = herds and A.nh or A.np
  local killed = 0
  for i = 1, count do
    local share = 0
    for k = ss[i] + 1, ss[i] + ls[i] do if set[cs[k]] then share = share + ws[k] end end
    if share > 0 then
      local d = N[i] * frac * share
      N[i] = N[i] - d
      killed = killed + d
      if tag and tag ~= 0 then T[i], Id[i] = tag, 0 end
    end
  end
  sys.refresh(W)
  return killed
end

-- A new group of `species` at cell c with n animals (the player's Introduce): herds or packs of
-- the landscape's kind, no bigger than their kind grows.
function sys.spawn(W, c, species, n, tag)
  local cfg = cfgmod.WILD
  local herds = species == "grazers"
  local sp = herds and grazer_species(W, c) or predator_species(W, c)
  local mx = herds and cfg.grazers[sp].max or cfg.predators[sp].max
  local left = n
  while left > 0.5 do
    local x = min(left, mx)
    if herds then add_herd(W, sp, c, x, tag) else add_pack(W, sp, c, x, tag) end
    left = left - x
  end
  sys.refresh(W)
end

-- Village hunters (labour.lua): `eff` hours after the biggest herd whose centre is within the
-- village's reach (`ring`). Returns the meat (FU) and the herd's cell; the animals are its own.
function sys.hunt(W, ring, eff, tag)
  local A = W.S.wild
  local cfg = cfgmod.WILD
  local sc = scratch(W)
  local set = sc.wl_huntset or {}
  sc.wl_huntset = set
  local stamp = (sc.wl_huntstamp or 0) + 1
  sc.wl_huntstamp = stamp
  for i = 1, #ring do set[ring[i]] = stamp end
  local best, bn = nil, 0
  for i = 1, A.nh do
    if set[A.h_cell[i]] == stamp and A.h_n[i] > bn then best, bn = i, A.h_n[i] end
  end
  if not best then return 0, nil end
  local c = A.h_cell[best]
  local dens = W.F.grazers[c]
  local kills = eff * cfg.hunt_rate * dens / (dens + 0.15)
  if kills > bn * cfg.hunt_max then kills = bn * cfg.hunt_max end
  A.h_n[best] = bn - kills
  if tag and tag ~= 0 then
    blame.step(A.h_tag, A.h_idle, best, -kills, FGZ.eps * cfg.gpc, FGZ.hold, -kills, tag, 0, 0, 0, 0, 0, 0)
  end
  return kills * cfg.meat, c
end

-- The group whose record has id `id` (view, inspector): "herd"/"pack", slot index.
function sys.find(W, id)
  local A = W.S.wild
  for i = 1, A.nh do if A.h_id[i] == id then return "herd", i end end
  for i = 1, A.np do if A.p_id[i] == id then return "pack", i end end
end

return sys
