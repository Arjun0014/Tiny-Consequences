-- Wild herds and packs (D-070, D-071): the mechanisms behind the predator-prey cycles, checked by
-- intervention against an untouched twin.
--   B18  a hard winter (deep snow held on one land for a winter) weakens its herds, feeds its
--        packs, and is paid for in the next spring: fewer fawns a head, more pups a pack.
--   B19  a starving pack breaks up: those it cannot feed leave, and settle only where prey is,
--        off other packs' ground.
--   B20  lone wolves roam for a mate: two that are no kin pair up (and breed); siblings do not.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local fauna = require("sim.systems.fauna")
local M = {}

local DBGK = { "pups", "pstarve", "pmort", "pother", "left", "live", "joined", "founded", "newpacks",
  "gonepacks", "born", "hstarve", "pred", "hunt", "paired", "stayed" }
local function dbg_on(W, only)
  local d = { only = only }
  for _, k in ipairs(DBGK) do d[k] = 0 end
  W.scratch.wl_dbg = d
  return d
end

-- The northern cells of the landmass with the most northern packs (one calendar for all of them).
local function northern_land(W)
  local A = W.S.wild
  local cont, lat = W.geo.continent, W.grid.lat
  local count = {}
  for p = 1, A.np do
    local c = A.p_cell[p]
    if lat[c] > 0.2 then count[cont[c]] = (count[cont[c]] or 0) + 1 end
  end
  local best, bn = nil, 0
  for k, n in pairs(count) do if n > bn or (n == bn and k < best) then best, bn = k, n end end
  local set = {}
  for _, c in ipairs(W.geo.land_cells) do if cont[c] == best and lat[c] > 0.2 then set[c] = true end end
  return set, bn
end

-- Animal-weighted herd condition, pack condition, grazers and wolves whose centre is in `set`.
local function state(W, set)
  local A = W.S.wild
  local hn, hc, pn, pc, np = 0, 0, 0, 0, 0
  for i = 1, A.nh do if set[A.h_cell[i]] then hn = hn + A.h_n[i]; hc = hc + A.h_n[i] * A.h_cond[i] end end
  for i = 1, A.np do if set[A.p_cell[i]] then pn = pn + A.p_n[i]; pc = pc + A.p_n[i] * A.p_cond[i]; np = np + 1 end end
  return { grazers = hn, hcond = hn > 0 and hc / hn or 0, wolves = pn, pcond = pn > 0 and pc / pn or 0, packs = np }
end

-- Run to the given week of the year (the northern calendar), then one more tick.
local function run_to_week(W, wk)
  local n = (wk - W.tick % 48 + 48) % 48
  if n == 0 then n = 48 end
  world.run_ticks(W, n)
end

function M.b18_a_hard_winter_weakens_herds_feeds_packs_and_shapes_the_spring()
  local Wa, Wb = worlds.fresh(1), worlds.fresh(1)
  local set, npk = northern_land(Wa)
  T.ok(npk >= 3, "packs in the north: " .. npk)
  -- to the start of a northern winter the packs meet fit (the experiment needs packs that can
  -- breed in the spring: not the trough of a cycle)
  for _ = 1, 8 do
    run_to_week(Wa, 36); run_to_week(Wb, 36)
    if state(Wb, set).pcond >= 0.5 then break end
  end
  -- twelve weeks of deep snow on that land in Wa (the snow the herds wade through and dig for)
  local cells = {}
  for c in pairs(set) do cells[#cells + 1] = c end
  table.sort(cells)
  local da, db = dbg_on(Wa, set), dbg_on(Wb, set)
  for _ = 1, 12 do
    for _, c in ipairs(cells) do if Wa.F.ice[c] < 0.9 then Wa.F.ice[c] = 0.9 end end
    world.run_ticks(Wa, 1); world.run_ticks(Wb, 1)
  end
  local a, b = state(Wa, set), state(Wb, set)
  T.ok(a.hcond < b.hcond - 0.05, string.format("the herds are thinner after the hard winter: %.2f vs %.2f", a.hcond, b.hcond))
  T.ok(da.pred > db.pred * 1.2, string.format("the packs killed more (deep snow, weak prey): %.0f vs %.0f", da.pred, db.pred))
  T.ok(a.pcond > b.pcond + 0.03, string.format("the packs are better fed: %.2f vs %.2f", a.pcond, b.pcond))
  -- the spring: the whelping (week 12) and the fawning (week 16)
  local ga, gb = a.grazers, b.grazers
  dbg_on(Wa, set); dbg_on(Wb, set)
  run_to_week(Wa, 20); run_to_week(Wb, 20)
  local fa = Wa.scratch.wl_dbg.born / math.max(ga, 1)
  local fb = Wb.scratch.wl_dbg.born / math.max(gb, 1)
  T.ok(fa < fb, string.format("fewer fawns a head in the spring after: %.2f vs %.2f", fa, fb))
  local pa = Wa.scratch.wl_dbg.pups / math.max(a.packs, 1)
  local pb = Wb.scratch.wl_dbg.pups / math.max(b.packs, 1)
  T.ok(pa >= pb, string.format("as many or more pups a pack: %.2f vs %.2f", pa, pb))
  Wa.scratch.wl_dbg, Wb.scratch.wl_dbg = nil, nil
  return string.format("herd condition %.2f vs %.2f, grazers killed %.0f vs %.0f, pack condition %.2f vs %.2f, fawns/head %.2f vs %.2f, pups/pack %.2f vs %.2f",
    a.hcond, b.hcond, da.pred, db.pred, a.pcond, b.pcond, fa, fb, pa, pb)
end

function M.b19_a_starving_pack_breaks_up_and_its_wolves_go_where_prey_is()
  local W = worlds.fresh(1)
  local A = W.S.wild
  local cfg = cfgmod.WILD
  -- the biggest pack goes hungry
  local p, bn = nil, 0
  for i = 1, A.np do if A.p_n[i] > bn then p, bn = i, A.p_n[i] end end
  T.ok(bn >= 5, "a pack of " .. bn)
  local id = A.p_id[p]
  A.p_cond[p] = 0.1
  local next0 = A.next_id
  local d = dbg_on(W)
  -- to the pack's next monthly update
  world.run_ticks(W, 4)
  local _, q = fauna.find(W, id)
  local n1 = q and A.p_n[q] or 0
  T.ok(n1 <= math.floor(cfg.keep[1] + cfg.keep[2] * 0.3) + 1 + d.stayed, string.format("the pack keeps what it can feed: %d of %d", n1, bn))
  T.ok(d.left >= bn - n1 - 1, string.format("%d wolves left", d.left))
  -- the new packs are off other packs' ground and have prey about them
  local H = W.scratch.wl_H
  local g = W.grid
  local newp = 0
  for i = 1, A.np do
    if A.p_id[i] >= next0 then
      newp = newp + 1
      local c = A.p_cell[i]
      local prey = 2 * H[c]
      local b = c * 6 - 6
      for k = 1, g.nbr_count[c] do prey = prey + H[g.nbr[b + k]] end
      T.ok(prey >= cfg.pack_min_prey * 0.5, string.format("a new den with prey about it (%.2f)", prey))
    end
  end
  W.scratch.wl_dbg = nil
  return string.format("pack of %d kept %d; %d left, %d lived, %d joined, %d founded (%d new packs)", bn, n1, d.left, d.live, d.joined, d.founded, newp)
end

function M.b20_lone_wolves_roam_for_a_mate_but_not_a_sibling()
  local W = worlds.fresh(1)
  local A = W.S.wild
  -- two cells two steps apart on land, far from other packs is not needed: lone wolves pair anywhere
  local g = W.grid
  local land = W.geo.land_cells
  local c1, c2
  for _, c in ipairs(land) do
    local r2 = fauna.ring2(W, c)
    for _, j in ipairs(r2) do
      if W.geo.ocean[j] == 0 and W.geo.continent[j] == W.geo.continent[c]
        and fauna.predator_species(W, j) == fauna.predator_species(W, c) then c1, c2 = c, j; break end
    end
    if c1 then break end
  end
  T.ok(c1 and c2, "two land cells")
  local function lone_pair(kin1, kin2)
    local W2 = worlds.fresh(1)
    local A2 = W2.S.wild
    -- (no other lone wolf anywhere: every lone wolf already found has a mate)
    for i = 1, A2.np do if A2.p_n[i] == 1 then A2.p_n[i] = 2 end end
    fauna.spawn(W2, c1, "predators", 1, 0)
    local i1 = A2.np
    fauna.spawn(W2, c2, "predators", 1, 0)
    local i2 = A2.np
    A2.p_kin[i1], A2.p_kin[i2] = kin1, kin2
    local id1, id2 = A2.p_id[i1], A2.p_id[i2]
    world.run_ticks(W2, 4)
    local _, q1 = fauna.find(W2, id1)
    local _, q2 = fauna.find(W2, id2)
    return (q1 and A2.p_n[q1] or 0), (q2 and A2.p_n[q2] or 0)
  end
  local a1, a2 = lone_pair(0, 0)
  T.ok((a1 >= 2 and a2 == 0) or (a2 >= 2 and a1 == 0), string.format("strangers pair up: %d + %d", a1, a2))
  local s1, s2 = lone_pair(777, 777)
  T.ok(s1 == 1 and s2 == 1, string.format("siblings do not: %d + %d", s1, s2))
  return string.format("strangers %d+%d, siblings %d+%d", a1, a2, s1, s2)
end

return M
