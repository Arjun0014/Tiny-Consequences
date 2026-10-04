-- Wild animals as herds and packs (docs/world/14_ECOLOGY.md §3, W4; D-070, D-071).
--
-- The animals live in groups that are real entities: a herd of deer, reindeer, antelope or boar
-- (W.S.wild h_*) and a pack of wolves or big cats (p_*), each with a centre cell, a head count, a
-- body condition (fat reserves, 0..1), a cause tag and its last move. A herd spends 40% of its
-- time on its centre cell and the rest on the cells about it (its range); a pack ranges two cells
-- from its den (its territory). The planet fields F.grazers and F.predators are derived views
-- (animals per cell / WILD.gpc, WILD.ppc), so vegetation pressure, the detectors, the release
-- blame, powers and hunting keep their meaning. H and P below are the cell densities all groups
-- make; a group feels the cells of its range, weighted by its time on each.
--   food       = (grass + 0.5 crop) × (1 − snow_block × snow)           -- snow hides food in winter
--   forage     = min(i_max, food / (φ H + ψ)) − snow_cost × snow        -- per head; 1 = enough
--                (deep snow costs energy to wade through as well as hiding the grass)
--   condition += h_gain × (forage − 1) a month: herds fatten in summer and live on their reserves
--                through a snowy winter or a dry season
--   fawns      once a year in the local late spring: n × fawns × f(condition) — a hard winter
--                is paid for in the next spring's fawns
--   starvation when the reserves are spent; a little old age
--   predation  = a' P max(0, H − refuge) / (1 + a' t_h max(0, H − refuge))   -- Holling II + refuge
--                a' = a × (1 + snow_kill × snow) × (1 + weak_kill × weakness of the prey there):
--                wolves kill two or three times as often in deep snow and take the weak (Nelson &
--                Mech 1986; Post et al. 1999)
--   a pack's condition += p_gain × (kills per wolf / need − 1) a month; one litter a year in the
--                spring (the breeding pair's), its size by the pack's condition; starvation when
--                the reserves are spent; in the autumn a pack keeps as many as it can feed and
--                the rest disperse — far, toward prey, to found a pack on empty ground, to pair
--                with a lone wolf, or to be taken into a pack that has room; many die on the way
-- So winters shared across a region (the climate's year-to-year wobble) weaken the herds and feed
-- the packs everywhere at once, the packs' numbers answer a spring later, and dispersers carry
-- wolves from hungry country to prey-rich country: the cycles are regional (the Moran effect and
-- dispersal) rather than a patchwork of unrelated local ones. What a group does is its own:
--   herds      move to a better neighbouring cell (food per head after the other herds, safety from
--              packs, suited to the species, away from villages, fields and fire); reindeer and
--              antelope walk between a summer and a winter (dry-season) range every year; herds
--              past their size split, dwindled ones join a herd of their kind or are gone.
--   packs      hunt the herds of their territory; a hungry pack moves its den toward prey (never
--              onto another pack's ground); dispersers leave in the autumn (above); a pack with
--              no wolves left is gone.
-- Nothing is spawned: a species gone from a landmass returns only by walking (sea ice ≥ 0.8) or by
-- the player's Introduce. Villagers hunt real herds (sys.hunt); powers cull and kill real groups
-- (sys.kill_in, sys.spawn).
--
-- Blame: a group carries its cause (h_tag / p_tag). Its growth carries the cause of a missing
-- pressure (predators far below their local normal while tagged: release, D-013) or, starving,
-- the food's cause; its losses the packs', the fire's, the hunters'. Each derived cell takes the
-- tag of the group that puts most animals on it; detectors re-tag cells with their events (a
-- boom), which the groups there adopt when the event descends from their cause.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")
local rng = require("sim.rng")

local sys = { name = "fauna" }
local floor, min, max = math.floor, math.min, math.max

local FGZ, FPD = fields.BY_NAME.grazers, fields.BY_NAME.predators
local FGR, FFO, FCR = fields.BY_NAME.grass, fields.BY_NAME.forest, fields.BY_NAME.crop

local HK = { "h_id", "h_sp", "h_cell", "h_home", "h_wint", "h_n", "h_tag", "h_idle", "h_prev", "h_moved", "h_cond", "h_low" }
local PK = { "p_id", "p_sp", "p_cell", "p_n", "p_tag", "p_idle", "p_prev", "p_moved", "p_disp", "p_cond", "p_kin" }
sys.HK, sys.PK = HK, PK

local function scratch(W)
  local sc = W.scratch
  if sc.wl_H then return sc end
  local n = W.n
  local f = fields.filled
  for _, k in ipairs({ "H", "P", "dd", "pp", "fm", "pr", "gt", "pt", "ft", "gn", "rp", "best", "bw",
    "seen", "den", "owner", "wk", "nt", "st", "claim", "pe", "ah", "ap", "brw" }) do sc["wl_" .. k] = f(n, 0) end
  sc.wl_stamp, sc.wl_dstamp = 0, 0
  sc.wl_rt, sc.wl_qt = {}, {}                       -- each group's range (cached tables, by slot)
  sc.wl_sh_n, sc.wl_sh_cell, sc.wl_sh_cond, sc.wl_sp_n, sc.wl_sp_cell = {}, {}, {}, {}, {}
  sc.wl_list = {}
  sc.wl_sst, sc.wl_ss, sc.wl_sstamp = {}, {}, 0     -- a herd's view of a cell (memo, one behave)
  sc.wl_near = f(n, 0)
  sc.wl_mstamp, sc.wl_midx = {}, {}
  return sc
end

function sys.init(W)
  local n = W.n
  local S = W.S.fauna
  if not S then
    local f = fields.filled
    S = { hnorm = f(n, 0), pnorm = f(n, 0) }
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
  -- (groups saved before they had a body condition start in fair shape)
  local A = W.S.wild
  if not A.h_cond then A.h_cond = {}; for i = 1, A.nh do A.h_cond[i] = cfgmod.WILD.cond0 end end
  if not A.h_low then A.h_low = {}; for i = 1, A.nh do A.h_low[i] = A.h_cond[i] end end
  if not A.p_cond then A.p_cond = {}; for i = 1, A.np do A.p_cond[i] = cfgmod.WILD.cond0 end end
  if not A.p_kin then A.p_kin = {}; for i = 1, A.np do A.p_kin[i] = 0; A.p_n[i] = floor(A.p_n[i] + 0.5) end end
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
  -- (where animals can be: land and the coastal sea; derive scans every cell once after this)
  local dom = {}
  for c = 1, n do if walk[c] == 1 or coast[c] == 1 then dom[#dom + 1] = c end end
  FZ.domain, FZ.full = dom, true
  FZ.ring2 = {}
  FZ.terr = {}   -- pack territories by den cell (walkable first and second ring; with the ice)
  -- a cell's own warmth (the latitude's base and the altitude's lapse; static): species' habitats
  local T = W.temp_static
  local warm = {}
  for c = 1, n do warm[c] = T and T.base and T.base[c] and (T.base[c] + T.lapse[c]) or 10 end
  FZ.warm = warm
  -- the mean distance between neighbouring cells (chord, unit sphere): lone wolves' reach
  local g = W.grid
  local sd, sn = 0, 0
  for c = 1, n, 7 do
    local j = g.nbr[c * 6 - 5]
    local dx, dy, dz = g.X[c] - g.X[j], g.Y[c] - g.Y[j], g.Z[c] - g.Z[j]
    sd, sn = sd + math.sqrt(dx * dx + dy * dy + dz * dz), sn + 1
  end
  FZ.step = sn > 0 and sd / sn or 0.1
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

local function add_herd(W, sp, cell, n, tag, cond)
  local A = W.S.wild
  local i = A.nh + 1
  A.nh = i
  A.h_id[i], A.h_sp[i], A.h_cell[i], A.h_home[i], A.h_wint[i] = A.next_id, sp, cell, cell, 0
  A.h_n[i], A.h_tag[i], A.h_idle[i], A.h_prev[i], A.h_moved[i] = n, tag or 0, 0, cell, W.hour or 0
  A.h_cond[i] = cond or cfgmod.WILD.cond0
  A.h_low[i] = A.h_cond[i]
  A.next_id = A.next_id + 1
  return i
end

local function add_pack(W, sp, cell, n, tag, cond)
  local A = W.S.wild
  local i = A.np + 1
  A.np = i
  A.p_id[i], A.p_sp[i], A.p_cell[i], A.p_n[i] = A.next_id, sp, cell, floor(n + 0.5)   -- (whole wolves)
  A.p_tag[i], A.p_idle[i], A.p_prev[i], A.p_moved[i] = tag or 0, 0, cell, W.hour or 0
  A.p_disp[i] = floor((W.hour or 0) / ((W.hpt or 168) * 48))
  A.p_cond[i] = cond or cfgmod.WILD.cond0
  A.p_kin[i] = 0
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

-- The key of what herds can use (villages, the sea ice): the cached ranges hold while it holds.
-- (`W.lu_ver` counts changes of village ground: entities.lua, mobility.lua)
local function range_key(W)
  local FZ = W.fauna_static
  local key = ((W.lu_ver or 0) * 31 + FZ.nice) % 2147483647
  for i = 1, FZ.nice do key = (key * 31 + FZ.ice_list[i]) % 2147483647 end
  if FZ.hkey ~= key then FZ.hkey, FZ.hrange = key, {} end
  return key
end


-- A herd's range by its centre cell c (cached until villages or the ice change what herds can
-- use): the centre (herd_w0 of its time) and the walkable neighbours not taken by a village
-- (the rest, shared). Cells t[1..L], weights t.w.
function sys.herd_range(W, c)
  range_key(W)
  local hrange = W.fauna_static.hrange
  local t = hrange[c]
  if t then return t end
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local walk, lu = W.fauna_static.walk, W.F.land_use
  t = {}
  local b = c * 6 - 6
  local L = 0
  for k = 1, cnt[c] do
    local j = nbr[b + k]
    if walk[j] == 1 and lu[j] >= 0 then L = L + 1; t[L] = j end
  end
  local w0 = L > 0 and cfgmod.WILD.herd_w0 or 1
  local wr = L > 0 and (1 - w0) / L or 0
  local tw = {}
  for k = 1, L do tw[k] = wr end
  t.L = L + 1
  t[L + 1], tw[L + 1] = c, w0
  t.w = tw
  hrange[c] = t
  return t
end

-- A pack's territory by its den c (static geography, cached): the den and two rings of walkable
-- cells (no territory across open water), weighted by pack_w.
function sys.territory(W, c)
  local terr = W.fauna_static.terr
  local t = terr[c]
  if t then return t end
  local pw = cfgmod.WILD.pack_w
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local walk = W.fauna_static.walk
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
  local w1 = n1 > 0 and pw[2] / n1 / tot or 0
  local w2 = n2 > 0 and pw[3] / n2 / tot or 0
  local tw = {}
  for k = 1, n1 do tw[k] = w1 end
  for k = n1 + 1, n1 + n2 do tw[k] = w2 end
  t[n1 + n2 + 1], tw[n1 + n2 + 1] = c, pw[1] / tot
  t.L, t.w = n1 + n2 + 1, tw
  terr[c] = t
  return t
end

-- Every group's range (the cached table of its cells and time shares: sc.wl_rt / wl_qt by slot)
-- and the densities they make; returns the touched cells. The groups' state it was made from is
-- kept (snapshot) so the next month can reuse it when nothing has changed since (the usual case).
local function densities(W, A, sc, cfg)
  local H, P, Wk, Pe = sc.wl_H, sc.wl_P, sc.wl_wk, sc.wl_pe
  local pref, pexp = cfg.pack_ref, cfg.pack_exp
  local seen = sc.wl_seen
  local stamp = sc.wl_stamp + 1
  sc.wl_stamp = stamp
  local list = sc.wl_list
  -- (the cells the last computation touched are emptied first: a cell no group's range reaches
  -- holds no animals. Without this a cell the herds had left kept its old density, and dispersing
  -- wolves settled on the ghost of prey long gone — and a reloaded world, whose densities start
  -- empty, diverged from the live one.)
  local H0, P0, W0, E0 = H, P, Wk, Pe
  for k = 1, sc.wl_nl or 0 do
    local x = list[k]
    H0[x], P0[x], W0[x], E0[x] = 0, 0, 0, 0
  end
  local nl = 0
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local walk, lu = W.fauna_static.walk, W.F.land_use
  local rt, qt = sc.wl_rt, sc.wl_qt
  local igpc, ippc = 1 / cfg.gpc, 1 / cfg.ppc
  local w0h = cfg.herd_w0
  -- (ranges by centre cell are cached; the cache is dropped when villages or the ice change what
  -- herds can use)
  local key = range_key(W)
  local hrange = W.fauna_static.hrange
  local sn, sce, sco = sc.wl_sh_n, sc.wl_sh_cell, sc.wl_sh_cond
  for i = 1, A.nh do
    local c = A.h_cell[i]
    local t = hrange[c] or sys.herd_range(W, c)
    rt[i] = t
    local tw = t.w
    local hn, hc = A.h_n[i], A.h_cond[i]
    sn[i], sce[i], sco[i] = hn, c, hc
    local n = hn * igpc
    local nw = n * (1 - hc)
    for k = 1, t.L do
      local x, w = t[k], tw[k]
      if seen[x] ~= stamp then seen[x] = stamp; H[x], P[x], Wk[x], Pe[x] = 0, 0, 0, 0; nl = nl + 1; list[nl] = x end
      H[x] = H[x] + n * w
      Wk[x] = Wk[x] + nw * w
    end
  end
  local pw = cfg.pack_w
  local terr = W.fauna_static.terr
  local pn_, pce = sc.wl_sp_n, sc.wl_sp_cell
  for i = 1, A.np do
    local c = A.p_cell[i]
    local t = terr[c] or sys.territory(W, c)
    qt[i] = t
    local tw = t.w
    local pn = A.p_n[i]
    pn_[i], pce[i] = pn, c
    local n = pn * ippc
    -- the pack hunts as one: its kills grow with its size ^ pack_exp (a pack of pack_ref kills as
    -- that many lone hunters would; a big pack shares each kill among more mouths)
    local e = pn > 0 and dmath.pow(pn / pref, pexp) * pref * ippc or 0
    for k = 1, t.L do
      local x, w = t[k], tw[k]
      if seen[x] ~= stamp then seen[x] = stamp; H[x], P[x], Wk[x], Pe[x] = 0, 0, 0, 0; nl = nl + 1; list[nl] = x end
      P[x] = P[x] + n * w
      Pe[x] = Pe[x] + e * w
    end
  end
  for i = #list, nl + 1, -1 do list[i] = nil end
  sc.wl_nl, sc.wl_snh, sc.wl_snp, sc.wl_skey = nl, A.nh, A.np, key
  return list, nl, stamp
end

-- The densities of the last computation if no group, range or village has changed since (a load,
-- a hunt, a power, a group drowned on thinning ice all show as a change); else computed anew.
local function densities_now(W, A, sc, cfg)
  local ok = sc.wl_snh == A.nh and sc.wl_snp == A.np and sc.wl_skey == range_key(W)
  if ok then
    local sn, sce, sco = sc.wl_sh_n, sc.wl_sh_cell, sc.wl_sh_cond
    local hn, hce, hco = A.h_n, A.h_cell, A.h_cond
    for i = 1, A.nh do
      if sn[i] ~= hn[i] or sce[i] ~= hce[i] or sco[i] ~= hco[i] then ok = false; break end
    end
    if ok then
      local pn_, pce = sc.wl_sp_n, sc.wl_sp_cell
      local pn, pc = A.p_n, A.p_cell
      for i = 1, A.np do
        if pn_[i] ~= pn[i] or pce[i] ~= pc[i] then ok = false; break end
      end
    end
  end
  if ok then return sc.wl_list, sc.wl_nl end
  return densities(W, A, sc, cfg)
end

-- The local week of year at cell c (the southern calendar runs half a year behind).
local function local_week(W, c)
  local wk = floor(W.hour / (W.hpt or 168))
  if W.grid.lat[c] < 0 then wk = wk + 24 end
  return wk % 48
end

---------------------------------------------------------------------------------------------
-- The step (every WILD.every ticks, dt = that)

-- Per cell: forage per head, the per-head rates (predation, fire; the packs' kills per head),
-- the causes of each, and the vegetation the grazers take.
local function ecology(W, sc, fcfg, dt, list, nl)
  local F, C, I = W.F, W.C, W.I
  local S = W.S.fauna
  local wcfg = cfgmod.WILD
  local grass, forest, crop, ice, fire = F.grass, F.forest, F.crop, F.ice, F.fire
  local H, P, Wk, Pe = sc.wl_H, sc.wl_P, sc.wl_wk, sc.wl_pe
  local dd_, pp_, fm_, pr_, nt_, st_ = sc.wl_dd, sc.wl_pp, sc.wl_fm, sc.wl_pr, sc.wl_nt, sc.wl_st
  local gt, pt, ft, gn, rp = sc.wl_gt, sc.wl_pt, sc.wl_ft, sc.wl_gn, sc.wl_rp
  local cah, cap = sc.wl_ah, sc.wl_ap
  local brw_ = sc.wl_brw
  local hnorm, pnorm = S.hnorm, S.pnorm
  local Kg, hfood, cfood, sblock = fcfg.K_grazer, fcfg.h_food, fcfg.crop_food, fcfg.snow_block
  local ap, th, refuge = fcfg.a_pred, fcfg.t_handle, fcfg.refuge
  local kgr, hgr, kbr, hbr, kra, hra = fcfg.graze, fcfg.h_graze, fcfg.browse, fcfg.h_browse, fcfg.raid, fcfg.h_raid
  local kov, hstart, bbase, fmort = fcfg.overgraze, fcfg.overgraze_from, fcfg.browse_base, fcfg.fire_mort
  local bmax, bworth, bcap = wcfg.browse_crowd, wcfg.browse_worth, wcfg.browse_cap
  local phi, psi, imax, scost = wcfg.phi, wcfg.psi, wcfg.i_max, wcfg.snow_cost
  local skill, wkill = wcfg.snow_kill, wcfg.weak_kill
  local relf, above = fcfg.release_frac, fcfg.pressure_above
  local rmin_h, rmin_p = fcfg.release_min.grazers, fcfg.release_min.predators
  local descends, eparent = blame.descends, W.E.parent
  local cgz, cpd = C.grazers, C.predators
  local cgr, igr, cfo, ifo, ccr, icr = C.grass, I.grass, C.forest, I.forest, C.crop, I.crop
  local cfire, cice = C.fire, C.ice
  local greps, grhold, foeps, fohold = FGR.eps, FGR.hold, FFO.eps, FFO.hold
  local grass_c = cfgmod.VEG.grass_c
  local eaten = 0
  for i = 1, nl do
    local c = list[i]
    local Hc, Pc = H[c], P[c]
    local hn, pn = hnorm[c], pnorm[c]
    local gr, fo, cr = grass[c], forest[c], crop[c]
    local sn = ice[c]
    local food = (gr + cfood * cr) * (1 - sblock * sn)
    local keff = Kg * food / (food + hfood)
    dd_[c] = keff > 1e-6 and Hc / keff or 1e9      -- crowding: animals against what the grass carries
    -- forage per head (1 = enough to hold condition), less the cost of wading through snow
    local it0 = food / (phi * Hc + psi)
    local it = it0
    if it > imax then it = imax end
    -- what the grass (under snow, eaten down, burnt by drought) lacks of their need the herds make
    -- up from the trees — twigs, saplings, bark — as far as the woods reach, and only in part:
    -- a ruminant cannot live on woody browse alone (browse_cap of its need). What they browse
    -- nourishes them (it is also what the forest loses, below): a herd in the woods holds on
    -- through an overgrazed year or a snowy winter on its trees, and the trees pay. (The cost of
    -- wading through the snow is theirs to pay: browse replaces grass, not strength.)
    local lack = 1 - it0
    if lack < 0 then lack = 0 elseif lack > bcap then lack = bcap end
    local bro = lack * fo / (fo + hbr)
    brw_[c] = bro
    nt_[c] = it - scost * sn + bro
    -- the cause of a lean month here: the snow's (a chill, a meteor winter) or the grass's
    local ts = 0
    if sn > 0.3 then ts = cice[c] end
    if ts == 0 then ts = cgr[c] end
    st_[c] = ts
    -- predation: easier in deep snow and on weak animals
    local wk = Hc > 0 and Wk[c] / Hc or 0
    local av = ap * (1 + skill * sn) * (0.7 + wkill * wk)
    local hx = Hc - refuge
    local pred = 0
    local Pec = Pe[c]
    if hx > 0 and Pec > 0 then pred = av * Pec * hx / (1 + av * th * hx) end
    pp_[c] = Hc > 0 and pred / Hc or 0
    local fi = fire[c]
    local fm = fi > 0 and fmort * fi or 0
    fm_[c] = fm
    pr_[c] = Pc > 0 and pred / Pc or 0   -- kills per head of the packs here (grazer units / week)
    -- causes: a pressure carries its cause only above normal (D-013); a missing pressure's cause
    -- travels into the growth it allows (release); starving grazers carry the food's cause
    local tH, tP = cgz[c], cpd[c]
    local tHa, tPa = 0, 0
    if tH ~= 0 and Hc > above * hn then tHa = tH end
    if tP ~= 0 and Pc > above * pn then tPa = tP end
    pt[c], gn[c] = tPa, tHa
    local tf = fi > 0 and cfire[c] or 0
    ft[c] = tf
    local relg = 0
    if tP ~= 0 and pn > rmin_p and Pc < relf * pn then
      relg = tP
      if tH ~= 0 and tH ~= tP and descends(eparent, tH, tP) then relg = tH end
    end
    gt[c] = relg
    local relp = 0
    if tH ~= 0 and hn > rmin_h and Hc < relf * hn then
      relp = tH
      if tP ~= 0 and tP ~= tH and descends(eparent, tP, tH) then relp = tP end
    end
    rp[c] = relp
    -- (does any cause for the herds / the packs lie here: demography looks only then)
    cah[c] = (relg ~= 0 or tPa ~= 0 or tf ~= 0 or ts ~= 0) and 1 or 0
    cap[c] = (tHa ~= 0 or relp ~= 0 or tf ~= 0) and 1 or 0
    -- vegetation pressure (blamed with the grazers' cause when they are abnormally many)
    if Hc > 0 then
      local over = keff > 1e-6 and Hc / keff or 2
      if over > bmax then over = bmax end
      local hunger = over > 1 and 1 or over
      local starve = hunger > hstart and (hunger - hstart) / (1 - hstart) or 0
      local gg = dt * kgr * Hc * (1 + kov * starve) * gr / (gr + hgr)
      if gg > gr then gg = gr end
      grass[c] = gr - gg
      -- (animals the grass cannot feed — crowded, or the grass under snow or burnt by drought —
      -- turn to the trees for what they lack: saplings, twigs, bark; worth browse_worth of grass,
      -- so it takes that much more of it. How too many deer, or a hard winter, stop a forest
      -- regrowing. Well-fed herds only nibble: browse_base.)
      local bb = dt * Hc * (kbr * bbase * fo / (fo + hbr) + kgr * brw_[c] / bworth)
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

-- 0..1 ramp
local function ramp(x, lo, hi)
  if x <= lo then return 0 elseif x >= hi then return 1 end
  return (x - lo) / (hi - lo)
end

-- Each group's condition and numbers from its range, blamed on the group.
local function demography(W, A, sc, cfg, fcfg, dt)
  local pp_, fm_, pr_, nt_, st_ = sc.wl_pp, sc.wl_fm, sc.wl_pr, sc.wl_nt, sc.wl_st
  local gt, pt, ft, gn, rp = sc.wl_gt, sc.wl_pt, sc.wl_ft, sc.wl_gn, sc.wl_rp
  local rt, qt = sc.wl_rt, sc.wl_qt
  local ah, ap = sc.wl_ah, sc.wl_ap
  local months = dt / 4
  local geps, ghold, peps, phold = FGZ.eps * cfg.gpc, FGZ.hold, FPD.eps * cfg.ppc, FPD.hold
  local hgain, fawns, fc0, fc1 = cfg.h_gain * months, cfg.fawns, cfg.fawn_cond[1], cfg.fawn_cond[2]
  local hst, hstm, hmort = cfg.h_starve, cfg.h_starve_max * months, cfg.h_mort * months
  local pgain, pneed, fmax = cfg.p_gain * months, fcfg.m_pred / fcfg.e_pred, cfg.p_food_max
  local litter, lc0, lc1 = cfg.litter, cfg.litter_cond[1], cfg.litter_cond[2]
  local pst, pstm, pmort = cfg.p_starve, cfg.p_starve_max * months, cfg.p_mort * months
  local alt = cfg.small_game
  local bw0, pw0 = cfg.fawn_week, cfg.pup_week
  local fsat = cfg.fawn_crowd
  local rb = rng.base(W.seed, rng.S.FAUNA, W.tick)
  for i = 1, A.nh do
    local t = rt[i]
    local tw = t.w
    local nt, pp, fm = 0, 0, 0
    local tg, tp, tf, ts = 0, 0, 0, 0
    local wg, wp, wf, ws, wrel = 0, 0, 0, 0, 0
    for k = 1, t.L do
      local c, w = t[k], tw[k]
      nt = nt + w * nt_[c]
      pp = pp + w * pp_[c]
      fm = fm + w * fm_[c]
      if ah[c] == 1 then   -- (a cell with a cause for the herds: most have none)
        if gt[c] ~= 0 and w > wg then tg, wg = gt[c], w end
        if pt[c] ~= 0 and w > wp then tp, wp = pt[c], w end
        if ft[c] ~= 0 and w > wf then tf, wf = ft[c], w end
        if st_[c] ~= 0 and w > ws then ts, ws = st_[c], w end
        if gt[c] ~= 0 then wrel = wrel + w end
      end
    end
    -- a herd living where the packs it knew are gone owes its numbers to their absence: it takes
    -- that cause at once (not only at the next fawning), unless it already carries what came of it
    if tg ~= 0 and wrel >= 0.5 then
      local cur = A.h_tag[i]
      if cur ~= tg and not (cur ~= 0 and blame.descends(W.E.parent, cur, tg)) then A.h_tag[i], A.h_idle[i] = tg, 0 end
    end
    -- the reserves: fattening on plenty, spent in a lean month
    local cond = A.h_cond[i] + hgain * (nt - 1)
    if cond < 0 then cond = 0 elseif cond > 1 then cond = 1 end
    A.h_cond[i] = cond
    -- the lowest the reserves fell since the last fawning: the does carried their fawns through it
    local low = A.h_low[i]
    if cond < low then low = cond end
    -- the year's fawns in the local late spring, as many as the does' condition through the
    -- winter allows (a hard winter is paid for in the next spring's fawns even after the grass
    -- is back: Mech et al. 1987)
    local births = 0
    local lw = local_week(W, A.h_cell[i])
    if lw >= bw0 and lw < bw0 + dt then
      -- (and the fawns that live through their first summer: fewer where the range is crowded —
      -- animals against what its grass carries — as the does compete for forage; density acts on
      -- ungulates mostly through juvenile survival near carrying capacity: Fowler 1987,
      -- Gaillard et al. 1998. Without it herds overshoot their grass and starve almost to the
      -- last, taking the packs with them.)
      local sat = 0
      local dd_ = sc.wl_dd
      for k = 1, t.L do sat = sat + tw[k] * dd_[t[k]] end
      local x = sat / fsat
      births = fawns * ramp(low, fc0, fc1) / (1 + x * x)
      local dbg = sc.wl_dbg
      if dbg then
        dbg.lows = (dbg.lows or 0) + low * A.h_n[i]; dbg.lown = (dbg.lown or 0) + A.h_n[i]
        dbg.conds = (dbg.conds or 0) + cond * A.h_n[i]
        if low < 0.4 then dbg.lowbad = (dbg.lowbad or 0) + A.h_n[i] end
      end
      low = cond
    end
    A.h_low[i] = low
    local starve = cond < hst and hstm * (hst - cond) / hst or 0
    local n = A.h_n[i]
    local born = n * births
    local lost_s = n * (starve + hmort)
    local lost_p, lost_f = n * dt * pp, n * dt * fm
    -- (the kills wolves made in sight of a settlement this month were of these: already taken,
    -- D-093)
    local bk = A.h_bank and A.h_bank[A.h_id[i]] or 0
    if bk > 0 then
      lost_p = lost_p - min(bk, lost_p)
      A.h_bank[A.h_id[i]] = nil
    end
    local d = born - lost_s - lost_p - lost_f
    local dbg = sc.wl_dbg
    if dbg and (not dbg.only or dbg.only[A.h_cell[i]]) then dbg.born = dbg.born + born; dbg.hstarve = dbg.hstarve + lost_s; dbg.pred = dbg.pred + lost_p end
    -- (followed herds, by id: the fawns of the herds that lived through a given winter, B18)
    if dbg and dbg.ids and dbg.ids[A.h_id[i]] and births > 0 then dbg.idborn = (dbg.idborn or 0) + born; dbg.idn = (dbg.idn or 0) + n end
    local n1 = n + d
    if n1 < 0 then n1 = 0; d = -n end
    A.h_n[i] = n1
    if A.h_tag[i] ~= 0 or tg ~= 0 or tp ~= 0 or tf ~= 0 or ts ~= 0 then
      -- fawns carry the cause of a missing pressure (release); starvation the lean month's
      -- (the snow's, the grass's); predation the packs'; fire the fire's (hunters: sys.hunt)
      blame.step(A.h_tag, A.h_idle, i, d, geps, ghold, born, tg, -lost_s, ts, -lost_p, tp, -lost_f, tf)
    end
  end
  for i = 1, A.np do
    local t = qt[i]
    local tw = t.w
    local kills, loss = 0, 0
    local tg, tr, tf = 0, 0, 0
    local wg, wr, wf = 0, 0, 0
    for k = 1, t.L do
      local c, w = t[k], tw[k]
      kills = kills + w * pr_[c]
      loss = loss + w * fm_[c]
      if ap[c] == 1 then   -- (a cell with a cause for the packs)
        if gn[c] ~= 0 and w > wg then tg, wg = gn[c], w end
        if rp[c] ~= 0 and w > wr then tr, wr = rp[c], w end
        if ft[c] ~= 0 and w > wf then tf, wf = ft[c], w end
      end
    end
    -- the pack's reserves by what it kills against what it needs, and the small game of its
    -- ground (hares, beavers, carrion: enough for a lone wolf or a pair, little for a big pack)
    local n = A.p_n[i]
    local f = kills / pneed + (n > 0 and alt / n or 0)
    if f > fmax then f = fmax end
    local cond = A.p_cond[i] + pgain * (f - 1)
    if cond < 0 then cond = 0 elseif cond > 1 then cond = 1 end
    A.p_cond[i] = cond
    -- (a pack is so many wolves: each birth and death is drawn, deterministically by the pack and
    -- the month, with the chances the pack's state gives)
    local key = A.p_id[i] * 4
    -- one litter a year (the breeding pair's), the pups that live by the pack's condition
    local pups = 0
    local lw = local_week(W, A.p_cell[i])
    if n >= 2 and lw >= pw0 and lw < pw0 + dt then pups = floor(litter * ramp(cond, lc0, lc1) + rng.fbase(rb, key)) end
    local starve = cond < pst and pstm * (pst - cond) / pst or 0
    local lost_s = floor(n * (starve + pmort) + rng.fbase(rb, key + 1))
    if lost_s > n then lost_s = n end
    local lost_o = floor((n - lost_s) * dt * loss + rng.fbase(rb, key + 2))
    if lost_o > n - lost_s then lost_o = n - lost_s end
    local d = pups - lost_s - lost_o
    local dbg = sc.wl_dbg
    if dbg and (not dbg.only or dbg.only[A.p_cell[i]]) then dbg.pups = dbg.pups + pups; dbg.pstarve = dbg.pstarve + lost_s; dbg.pmort = dbg.pmort + n * pmort; dbg.pother = dbg.pother + lost_o end
    local n1 = n + d
    if n1 < 0 then n1 = 0; d = -n end
    A.p_n[i] = n1
    if A.p_tag[i] ~= 0 or tg ~= 0 or tr ~= 0 or tf ~= 0 then
      -- (pups: the prey's cause when it is abnormally plentiful; hunger: the prey's missing;
      -- fire. Culls (powers) and hunters kill whole wolves: sys.kill_in)
      local v4 = 0
      if tf ~= 0 then v4 = -lost_o end
      blame.step(A.p_tag, A.p_idle, i, d, peps, phold, pups, tg, -lost_s, tr, 0, 0, v4, tf)
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

-- A pack's territory is its own: no new pack founds a den within two steps of another's.
function sys.claim(W, claim, c, ds)
  claim[c] = ds
  local g = W.grid
  local b = c * 6 - 6
  for k = 1, g.nbr_count[c] do claim[g.nbr[b + k]] = ds end
  local r2 = ring2(W, c)
  for k = 1, #r2 do claim[r2[k]] = ds end
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
  local rt = sc.wl_rt
  -- (a herd's view of a neighbouring cell is the same for every herd of its kind this month: kept)
  local sst, ssv = sc.wl_sst, sc.wl_ss
  local sstamp = sc.wl_sstamp + 1
  sc.wl_sstamp = sstamp
  local function view(sp, x)
    local key = x * 4 + sp
    if sst[key] == sstamp then return ssv[key] end
    local s = score(W, sc, cfg, fcfg, sp, x, 0)
    sst[key], ssv[key] = sstamp, s
    return s
  end
  local near = sc.wl_near
  local nh0 = A.nh
  local i = 1
  while i <= A.nh do
    local sp, c = A.h_sp[i], A.h_cell[i]
    local n = A.h_n[i]
    -- how crowded its range is: animals against what the grass carries (H / K_eff)
    local sat = 0
    local t = i <= nh0 and rt[i]
    if t then
      local tw = t.w
      for k = 1, t.L do sat = sat + tw[k] * dd_[t[k]] end
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
    local dbg = sc.wl_dbg
    if dbg then dbg.herds = (dbg.herds or 0) + 1; if sat > 1 then dbg.crowded = (dbg.crowded or 0) + 1 end; dbg.satsum = (dbg.satsum or 0) + sat end
    if not look then
      -- (settled this time: its range is still good enough)
    elseif target then
      -- on the way: the usable neighbour nearest the seasonal range
      local tx, ty, tz = g.X[target], g.Y[target], g.Z[target]
      local bd = 1e9
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if walk[j] == 1 and lu[j] >= 0 then
          local dx, dy, dz = g.X[j] - tx, g.Y[j] - ty, g.Z[j] - tz
          local d = dx * dx + dy * dy + dz * dz
          if d < bd then best, bd = j, d end
        end
      end
    else
      local bs = fire[c] > 0 and 0 or score(W, sc, cfg, fcfg, sp, c, own) * cfg.move_gain
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if walk[j] == 1 and lu[j] >= 0 then
          local s = view(sp, j)
          if s > bs then best, bs = j, s end
        end
      end
    end
    if best ~= c then
      if dbg then dbg.moves = (dbg.moves or 0) + 1 end
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
          if walk[j] == 1 and lu[j] >= 0 then
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
        if walk[j] == 1 and lu[j] >= 0 then
          local s = view(sp, j)
          if s > sb then cb, sb = j, s end
        end
      end
      if cb then
        local part = n * cfg.split_share
        if dbg then dbg.splits = (dbg.splits or 0) + 1 end
        A.h_n[i] = n - part
        local ni = add_herd(W, sp, cb, part, A.h_tag[i], A.h_cond[i])
        A.h_low[ni] = A.h_low[i]
        A.h_prev[ni] = c1
      end
    end
    -- a dwindled herd joins one of its kind on or next to its cell
    if A.h_n[i] < spc.min then
      local c1 = A.h_cell[i]
      local b1 = c1 * 6 - 6
      -- (its cell and the cells about it, stamped)
      local ns = (sc.wl_nstamp or 0) + 1
      sc.wl_nstamp = ns
      near[c1] = ns
      for k = 1, cnt[c1] do near[nbr[b1 + k]] = ns end
      local hsp, hn, hce = A.h_sp, A.h_n, A.h_cell
      local smin = spc.min
      for jx = 1, A.nh do
        if jx ~= i and hsp[jx] == sp and hn[jx] >= smin then
          if near[hce[jx]] == ns then
            local a, b = A.h_n[jx], A.h_n[i]
            A.h_cond[jx] = (A.h_cond[jx] * a + A.h_cond[i] * b) / (a + b)
            A.h_low[jx] = (A.h_low[jx] * a + A.h_low[i] * b) / (a + b)
            A.h_n[jx] = a + b; A.h_n[i] = 0
            break
          end
        end
      end
    end
    if A.h_n[i] < cfg.gone then
      if dbg then dbg.drops = (dbg.drops or 0) + 1 end
      -- (the last herd takes the slot, and its range with it; a herd split off this month has none)
      local last = A.nh
      rt[i] = last <= nh0 and rt[last] or false
      drop(A, HK, i, "nh")
    else i = i + 1 end
  end
  -- fission-fusion: herds of a kind that meet on the same ground become one (up to their size)
  local ms, mi = sc.wl_mstamp, sc.wl_midx
  local mst = (sc.wl_mst or 0) + 1
  sc.wl_mst = mst
  i = 1
  while i <= A.nh do
    local key = A.h_cell[i] * 4 + A.h_sp[i]
    local j = ms[key] == mst and mi[key] or nil
    local merged = false
    if j then
      local a, b = A.h_n[j], A.h_n[i]
      if a + b <= cfg.grazers[A.h_sp[i]].max then
        A.h_cond[j] = (A.h_cond[j] * a + A.h_cond[i] * b) / (a + b)
        A.h_low[j] = (A.h_low[j] * a + A.h_low[i] * b) / (a + b)
        if b > a then A.h_tag[j], A.h_idle[j] = A.h_tag[i], A.h_idle[i] end
        A.h_n[j] = a + b
        drop(A, HK, i, "nh")
        merged = true
      end
    end
    if not merged then
      if not j then ms[key], mi[key] = mst, i end
      i = i + 1
    end
  end
  -- packs: the dens and the ground about them (stamped: another pack's ground)
  local den, owner = sc.wl_den, sc.wl_owner
  local ds = sc.wl_dstamp + 1
  sc.wl_dstamp = ds
  local claim = sc.wl_claim
  for p = 1, A.np do
    local c = A.p_cell[p]
    den[c], owner[c] = ds, A.p_id[p]
    local b = c * 6 - 6
    for k = 1, cnt[c] do local j = nbr[b + k]; den[j], owner[j] = ds, A.p_id[p] end
    sys.claim(W, claim, c, ds)
  end
  local ocean = W.geo.ocean
  local np0 = A.np
  i = 1
  while i <= A.np do
    local sp, c = A.p_sp[i], A.p_cell[i]
    local n = A.p_n[i]
    local spc = cfg.predators[sp]
    -- prey about the den; a hungry pack (or one burned out) moves toward more, monthly at most
    if hour - A.p_moved[i] >= 4 * (W.hpt or 168) then
      local b = c * 6 - 6
      local here = 2 * H[c]
      for k = 1, cnt[c] do here = here + H[nbr[b + k]] end
      if A.p_cond[i] < cfg.den_move_cond or fire[c] > 0 then
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
    -- in the autumn a pack keeps as many as it can feed; the rest (and the pack's surplus young)
    -- leave to find a place of their own
    local year = floor(hour / ((W.hpt or 168) * 48))
    local lw = local_week(W, A.p_cell[i])
    local autumn = A.p_disp[i] ~= year and lw >= cfg.disperse_week and lw < cfg.disperse_week + cfg.every
    -- (a starving pack breaks up whatever the season: those it cannot feed go)
    if i <= np0 and (autumn or A.p_cond[i] < cfg.p_starve) then
      if autumn then A.p_disp[i] = year end
      local keep = cfg.keep[1] + cfg.keep[2] * A.p_cond[i]
      if keep > spc.max then keep = spc.max end
      local k = floor(n - keep)
      if k >= 1 then
        A.p_n[i] = n - k
        sys.disperse(W, A, i, k, ds, den, owner, claim)
      end
    end
    if A.p_n[i] < cfg.gone then
      if sc.wl_dbg then sc.wl_dbg.gonepacks = sc.wl_dbg.gonepacks + 1 end
      drop(A, PK, i, "np")
    else i = i + 1 end
  end
  sys.pair_lone(W, A, cfg)
end

-- Lone wolves roam far for a mate: two that are no kin of each other (or, after a year alone,
-- any), on the same land and within reach, pair up at the better den (the other gives its up).
-- Without this a crash that leaves only lone wolves ends the species there.
function sys.pair_lone(W, A, cfg)
  local g = W.grid
  local X, Y, Z = g.X, g.Y, g.Z
  local cont = W.geo.continent
  local H = scratch(W).wl_H
  local lone = {}
  for p = 1, A.np do if A.p_n[p] == 1 then lone[#lone + 1] = p end end
  if #lone < 2 then return end
  local r = cfg.mate_reach * W.fauna_static.step
  local r2 = r * r
  -- (kin avoid each other, but after a year alone with no other mate in reach they pair anyway —
  -- as on an island with one pack's descendants)
  local hour, wait = W.hour, cfg.kin_wait_years * (W.hpt or 168) * 48
  local gone = {}
  for a = 1, #lone do
    local pa = lone[a]
    if not gone[pa] then
      local ca = A.p_cell[pa]
      local best, bd = nil, r2
      for b = a + 1, #lone do
        local pb = lone[b]
        if not gone[pb] and A.p_sp[pb] == A.p_sp[pa] and cont[A.p_cell[pb]] == cont[ca]
          and (A.p_kin[pa] == 0 or A.p_kin[pa] ~= A.p_kin[pb]
            or (hour - A.p_moved[pa] >= wait and hour - A.p_moved[pb] >= wait)) then
          local cb = A.p_cell[pb]
          local dx, dy, dz = X[ca] - X[cb], Y[ca] - Y[cb], Z[ca] - Z[cb]
          local d = dx * dx + dy * dy + dz * dz
          if d <= bd then best, bd = pb, d end
        end
      end
      if best then
        -- the pair keeps the den with more prey about it
        local keep, go = pa, best
        if H[A.p_cell[best]] > H[ca] then keep, go = best, pa end
        A.p_n[keep], A.p_kin[keep] = 2, 0
        A.p_cond[keep] = (A.p_cond[keep] + A.p_cond[go]) / 2
        A.p_n[go] = 0
        gone[pa], gone[best] = true, true
        if scratch(W).wl_dbg then scratch(W).wl_dbg.paired = (scratch(W).wl_dbg.paired or 0) + 1 end
      end
    end
  end
  -- (the wolf that moved leaves an empty record)
  local i = 1
  while i <= A.np do
    if A.p_n[i] < cfg.gone then drop(A, PK, i, "np") else i = i + 1 end
  end
end

-- `k` wolves leave pack i, one by one (siblings rarely go together). Those that live through the
-- journey (disperse_survival) settle where life looks best within reach: a lone wolf that is no
-- kin of theirs (a mate: the pair founds a pack), free ground with prey (they hold it, alone, for
-- a mate), or a pack with room that takes them in; the pull of prey is weighed against the packs
-- already there.
function sys.disperse(W, A, i, k, ds, den, owner, claim)
  local cfg = cfgmod.WILD
  local g = W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local sc = scratch(W)
  local H, P = sc.wl_H, sc.wl_P
  local walk, ocean = W.fauna_static.walk, W.geo.ocean
  local from, sp, tag, kin = A.p_cell[i], A.p_sp[i], A.p_tag[i], A.p_id[i]
  local spc = cfg.predators[sp]
  local kin_wait = cfg.kin_wait_years * (W.hpt or 168) * 48
  local rb = rng.base(W.seed, rng.S.FAUNA, W.tick + 1)
  local live = floor(k * cfg.disperse_survival + rng.fbase(rb, kin * 4 + 3))
  local dbg = sc.wl_dbg
  if dbg then dbg.left = dbg.left + k; dbg.live = dbg.live + live end
  if live < 1 then return end
  -- the packs by id (slots), for the ground's owners
  local slot = {}
  for p = 1, A.np do slot[A.p_id[p]] = p end
  -- the ground within reach (breadth-first over walkable cells, not back onto the home range)
  local seen = { [from] = 0 }
  local q, head = { from }, 1
  while head <= #q do
    local x = q[head]; head = head + 1
    local d = seen[x]
    if d < cfg.disperse_reach then
      local b = x * 6 - 6
      for kk = 1, cnt[x] do
        local j = nbr[b + kk]
        if seen[j] == nil and walk[j] == 1 then seen[j] = d + 1; q[#q + 1] = j end
      end
    end
  end
  for w = 1, live do
    local best, bs, bkind, bp = nil, 0, 0, 0
    for qi = 1, #q do
      local x = q[qi]
      if seen[x] >= 2 and ocean[x] == 0 then
        local b = x * 6 - 6
        local prey = 2 * H[x]
        for kk = 1, cnt[x] do prey = prey + H[nbr[b + kk]] end
        local s, kind, p = 0, 0, 0
        if den[x] == ds then
          p = slot[owner[x]] or 0
          if p > 0 and A.p_sp[p] == sp then
            if A.p_n[p] < 2 and A.p_cell[p] == x and (A.p_kin[p] ~= kin or W.hour - A.p_moved[p] >= kin_wait) then
              s, kind = prey * cfg.mate_pull, 1                         -- a mate
            elseif A.p_n[p] >= 2 and A.p_n[p] < spc.max then
              s, kind = prey * cfg.adopt / (1 + cfg.rival_fear * 2 * P[x]), 2   -- taken in
            end
          end
        elseif prey >= cfg.pack_min_prey and claim[x] ~= ds then
          s, kind = prey / (1 + cfg.rival_fear * 2 * P[x]), 3              -- free ground
        end
        if s > bs then best, bs, bkind, bp = x, s, kind, p end
      end
    end
    if not best then
      -- (nowhere to go: the rest stay at home, for now)
      A.p_n[i] = A.p_n[i] + live - w + 1
      if dbg then dbg.stayed = (dbg.stayed or 0) + live - w + 1 end
      return
    end
    if bkind == 1 or bkind == 2 then
      A.p_n[bp] = A.p_n[bp] + 1
      if bkind == 1 then A.p_kin[bp] = 0 end   -- (a pair: its pups are the next generation's kin)
      if dbg then dbg.joined = dbg.joined + 1; if bkind == 1 then dbg.paired = (dbg.paired or 0) + 1 end end
    else
      if dbg then dbg.founded = dbg.founded + 1; dbg.newpacks = dbg.newpacks + 1 end
      local ni = add_pack(W, sp, best, 1, tag, cfg.cond0)
      A.p_prev[ni] = from
      A.p_disp[ni] = floor(W.hour / ((W.hpt or 168) * 48))
      A.p_kin[ni] = kin
      local me = A.p_id[ni]
      slot[me] = ni
      den[best], owner[best] = ds, me
      local b = best * 6 - 6
      for kk = 1, cnt[best] do local j = nbr[b + kk]; if den[j] ~= ds then den[j], owner[j] = ds, me end end
      sys.claim(W, claim, best, ds)
    end
  end
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
  -- cells that hold no animals now: animals and their causes are only ever on land or the coastal
  -- sea's ice (every cell once after a load or a changed coastline)
  local cg, cp = C.grazers, C.predators
  local ig, ip = I.grazers, I.predators
  local pg, pp = Pv.grazers, Pv.predators   -- (prev buffers, where the field keeps one)
  local ghold, phold = FGZ.hold, FPD.hold
  local step = dt > 0 and dt or 0
  local FZ = W.fauna_static
  local dom = FZ.domain
  local ndom = #dom
  if FZ.full then FZ.full = nil; dom, ndom = nil, W.n end
  for ci = 1, ndom do
    local c = dom and dom[ci] or ci
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
  local rt, qt = sc.wl_rt, sc.wl_qt
  for i = 1, A.nh do
    local n, t = A.h_n[i], A.h_tag[i]
    local r = rt[i]
    local tw = r.w
    for k = 1, r.L do
      local c = r[k]
      local x = n * tw[k]
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
  for i = 1, A.np do
    local n, t = A.p_n[i], A.p_tag[i]
    local r = qt[i]
    local tw = r.w
    for k = 1, r.L do
      local c = r[k]
      local x = n * tw[k]
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
  local list, nl = densities_now(W, A, sc, cfg)
  ecology(W, sc, fcfg, every, list, nl)
  demography(W, A, sc, cfg, fcfg, every)
  behave(W, A, sc, cfg, fcfg)
  derive(W, A, sc, cfg, fcfg, every)
end

-- Shift the groups' stored hours by dt weeks (after the worldgen spin-up, whose clock ends at
-- Year 0): the hour each group last moved (a pack moves its den at most monthly; lone wolves wait
-- a year before a kin mate; the view walks a group from its last cell) and the year each pack
-- last dispersed. Without it every group of the spin-up had moved "in the future" for 22 years:
-- packs never moved their dens and the view drew herds on the cell they had left.
function sys.shift_ticks(W, dt)
  local A = W.S.wild
  if not A then return end
  local dh = dt * (W.hpt or 168)
  for i = 1, A.nh do A.h_moved[i] = A.h_moved[i] + dh end
  for i = 1, A.np do
    A.p_moved[i] = A.p_moved[i] + dh
    A.p_disp[i] = A.p_disp[i] + floor(dh / ((W.hpt or 168) * 48))
  end
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
-- (S25c, D-220) Fire catching on cell c (or a god's bolt there): the herds whose range is on it bolt out of it at once -
-- to the neighbouring cell they can walk that burns least and grazes most - and a share of them (`kill`, by the fire's
-- intensity) is caught by the flames or the strike and dies there, carrying its cause. Returns how many died.
-- W.wild_fled (runtime, never saved: the view's alone) marks the move as a flight, so the view runs it.
function sys.flee_fire(W, c, tag, kill)
  local A = W.S.wild
  if not (A and A.nh and A.nh > 0) then return 0 end
  local F, g = W.F, W.grid
  local walk, lu = W.fauna_static and W.fauna_static.walk, F.land_use
  if not walk then return 0 end
  local dead, touched = 0, false
  for i = 1, A.nh do
    if A.h_cell[i] == c and A.h_n[i] > 0 then
      touched = true
      if kill and kill > 0 then
        local d = A.h_n[i] * kill
        A.h_n[i] = A.h_n[i] - d
        dead = dead + d
        if tag and tag ~= 0 then A.h_tag[i], A.h_idle[i] = tag, 0 end
      end
      local best, bs = nil, -1e9
      local b = c * 6 - 6
      for k = 1, g.nbr_count[c] do
        local j = g.nbr[b + k]
        if walk[j] == 1 and lu[j] >= 0 then
          local s = (F.grass[j] or 0) + 0.5 * (F.forest[j] or 0) - 4 * (F.fire[j] or 0)
          if s > bs then best, bs = j, s end
        end
      end
      if best then
        A.h_prev[i], A.h_cell[i], A.h_moved[i] = c, best, W.hour
        if not cfgmod.WILD.migrates[A.h_sp[i]] then A.h_home[i] = best end
        W.wild_fled = W.wild_fled or {}
        W.wild_fled[A.h_id[i]] = W.hour
      end
    end
  end
  if not touched then return 0 end
  if dead > 0 and tag and tag ~= 0 then
    local ct, it = W.C.grazers, W.I.grazers
    if ct then ct[c], it[c] = tag, 0 end
  end
  sys.refresh(W)
  return dead
end

function sys.kill_in(W, cells, species, frac, tag)
  local A = W.S.wild
  local sc = scratch(W)
  local cfg = cfgmod.WILD
  local set = {}
  for i = 1, #cells do set[cells[i]] = true end
  densities(W, A, sc, cfg)
  local herds = species == "grazers"
  local rt = herds and sc.wl_rt or sc.wl_qt
  local N = herds and A.h_n or A.p_n
  local T = herds and A.h_tag or A.p_tag
  local Id = herds and A.h_idle or A.p_idle
  local count = herds and A.nh or A.np
  local killed = 0
  local rb = rng.base(W.seed, rng.S.CULL, W.hour)
  for i = 1, count do
    local share = 0
    local r = rt[i]
    local tw = r.w
    for k = 1, r.L do if set[r[k]] then share = share + tw[k] end end
    if share > 0 then
      local d = N[i] * frac * share
      -- (wolves die whole)
      if not herds then d = floor(d + rng.fbase(rb, A.p_id[i])); if d > N[i] then d = N[i] end end
      N[i] = N[i] - d
      killed = killed + d
      if tag and tag ~= 0 then T[i], Id[i] = tag, 0 end
    end
  end
  sys.refresh(W)
  -- the cells the animals were killed on carry the cause (an emptied cell has no group left to
  -- lend it one: the packs a Cull wipes out leave their ground tagged, and what follows there —
  -- the herds' release, a regional extinction — chains to it)
  if tag and tag ~= 0 then
    local fld = W.F[species]
    local ct, it = W.C[species], W.I[species]
    for k = 1, #cells do
      local c = cells[k]
      if W.geo.ocean[c] == 0 and (fld[c] > 0 or killed > 0) then ct[c], it[c] = tag, 0 end
    end
  end
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
  local dbg = sc.wl_dbg
  if dbg and (not dbg.only or dbg.only[c]) then dbg.hunt = dbg.hunt + kills end
  if tag and tag ~= 0 then
    blame.step(A.h_tag, A.h_idle, best, -kills, FGZ.eps * cfg.gpc, FGZ.hold, -kills, tag, 0, 0, 0, 0, 0, 0)
  end
  return kills * cfg.meat, c
end

-- The local wild (sim/agents/wild.lua, D-093) --------------------------------------------------
-- (slots move when groups are dropped: always look a group up by its id)
function sys.find_herd(W, id)
  local A = W.S.wild
  for i = 1, A.nh do if A.h_id[i] == id then return i end end
  return nil
end
function sys.find_pack(W, id)
  local A = W.S.wild
  for i = 1, A.np do if A.p_id[i] == id then return i end end
  return nil
end
-- n animals of herd i killed in sight of a settlement: by wolves (banked: counted as part of the
-- herd's predation for the month), or by hunters.
function sys.take_from_herd(W, i, n, banked)
  local A = W.S.wild
  A.h_n[i] = math.max(0, A.h_n[i] - n)
  if banked then
    A.h_bank = A.h_bank or {}
    A.h_bank[A.h_id[i]] = (A.h_bank[A.h_id[i]] or 0) + n
  end
end
-- A pack fed on a village's beast (outside the herds' predation): its reserves rise.
function sys.feed_pack(W, i, meat)
  local A = W.S.wild
  local n = math.max(1, A.p_n[i])
  A.p_cond[i] = math.min(1, A.p_cond[i] + cfgmod.V3.wolf_feed * meat / n)
end

-- The group whose record has id `id` (view, inspector): "herd"/"pack", slot index.
function sys.find(W, id)
  local A = W.S.wild
  for i = 1, A.nh do if A.h_id[i] == id then return "herd", i end end
  for i = 1, A.np do if A.p_id[i] == id then return "pack", i end end
end

-- (S25c, D-220) the herds answer a fire the moment it catches on their cell
require("sim.systems.fire").on_burn = function(W, c, tag, inten)
  if not W.v3 then return end   -- (the legacy world's spin-up as it was: its saved world unchanged)
  sys.flee_fire(W, c, tag, cfgmod.WILD.herd_fire_kill * (inten or 0.6))
end

return sys
