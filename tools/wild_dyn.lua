-- Predator-prey dynamics diagnostics (W4): why do (or don't) wolves and deer cycle?
--   luajit tools/wild_dyn.lua <seed> [years=120] [--no-society] [--series] [--csv out.csv]
-- Per continent: the predator CV as fauna_stability measures it (monthly field means, years 5+),
-- the CV of yearly means (the interannual part alone), the prey's; per pack (groups model): the
-- CV of its yearly mean size over its life and the mean correlation between packs' yearly sizes
-- (synchrony); winter severity (mean land snow over the local winter) and its correlation with the
-- predators' growth the next year. Works on the field model too (no groups: per-cell CVs instead).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local nosoc, series, csv, groups, monthly = false, false, nil, 0, -1
local pos = {}
local i = 1
while i <= #arg do
  local a = arg[i]
  if a == "--no-society" then nosoc = true
  elseif a == "--series" then series = true
  elseif a == "--csv" then csv = arg[i + 1]; i = i + 1
  elseif a == "--groups" then groups = tonumber(arg[i + 1]); i = i + 1
  elseif a == "--monthly" then monthly = tonumber(arg[i + 1]); i = i + 1
  else pos[#pos + 1] = a end
  i = i + 1
end
if nosoc then require("sim.worldgen.settle").run = function() end end
local world = require("sim.world")
local seed = tonumber(pos[1]) or 1
local years = tonumber(pos[2]) or 120
local W = require("sim.worldgen.init").generate(seed)
local geo = W.geo
local comps = geo.components
local cfgmod = require("sim.config")
local conts = {}
for k = 1, #comps do
  if comps[k].size / W.n >= cfgmod.WORLDGEN.continent_min_frac * 0.9 then conts[#conts + 1] = k end
end
local idx = {}
for k = 1, #conts do idx[conts[k]] = k end
local land = geo.land_cells

local function cv(t, from, to)
  local s, s2, n = 0, 0, 0
  for j = from or 1, to or #t do s = s + t[j]; s2 = s2 + t[j] * t[j]; n = n + 1 end
  if n < 2 then return 0, 0 end
  local m = s / n
  local v = s2 / n - m * m
  if v < 0 then v = 0 end
  return m > 0 and math.sqrt(v) / m or 0, m
end
local function corr(a, b)
  local n = math.min(#a, #b)
  if n < 3 then return nil end
  local ma, mb = 0, 0
  for j = 1, n do ma = ma + a[j]; mb = mb + b[j] end
  ma, mb = ma / n, mb / n
  local sab, saa, sbb = 0, 0, 0
  for j = 1, n do
    local x, y = a[j] - ma, b[j] - mb
    sab, saa, sbb = sab + x * y, saa + x * x, sbb + y * y
  end
  if saa <= 0 or sbb <= 0 then return nil end
  return sab / math.sqrt(saa * sbb)
end

local mp, mg = {}, {}           -- monthly continental means (predators, grazers)
local yp, yg, ysnow = {}, {}, {}  -- yearly
for k = 1, #conts do mp[k], mg[k], yp[k], yg[k], ysnow[k] = {}, {}, {}, {}, {} end
local packs = {}                -- id -> { first year, yearly sizes }
-- patches: ~20-cell Voronoi patches of land (synchrony against distance)
local reg = {}
local centres = {}
do
  local g = W.grid
  for j = 1, #land, 20 do centres[#centres + 1] = land[j] end
  for j = 1, #land do
    local c = land[j]
    local best, bd = 1, 1e9
    for k = 1, #centres do
      local x = centres[k]
      if geo.continent[x] == geo.continent[c] then
        local dx, dy, dz = g.X[c] - g.X[x], g.Y[c] - g.Y[x], g.Z[c] - g.Z[x]
        local d = dx * dx + dy * dy + dz * dz
        if d < bd then best, bd = k, d end
      end
    end
    reg[c] = best
  end
end
local regy = {}                 -- patch -> yearly mean predator density
local A0 = W.S.wild
local DBGK = { "lows", "lown", "conds", "lowbad", "pups", "pstarve", "pmort", "pother", "left", "live", "joined", "founded", "newpacks", "gonepacks", "born", "hstarve", "pred", "hunt" }
local function dbg_reset() local d = {}; for _, k in ipairs(DBGK) do d[k] = 0 end; W.scratch.wl_dbg = d end
if A0 and groups > 0 then dbg_reset() end
local cellts = {}               -- field model: yearly predator density of every land cell
local out = csv and assert(io.open(csv, "w"))
if out then out:write("month,cont,pred,prey,snow\n") end
local month = 0
for y = 1, years do
  local accp, accg, accs, nacc = {}, {}, {}, {}
  for k = 1, #conts do accp[k], accg[k], accs[k], nacc[k] = 0, 0, 0, 0 end
  local packacc = {}
  for t = 1, 48 do
    world.tick(W)
    if t % 4 == 0 then
      month = month + 1
      local F = W.F
      local sp, sg, sn, ss = {}, {}, {}, {}
      for k = 1, #conts do sp[k], sg[k], sn[k], ss[k] = 0, 0, 0, 0 end
      for j = 1, #land do
        local c = land[j]
        local k = idx[geo.continent[c]]
        if k then
          sp[k] = sp[k] + F.predators[c]; sg[k] = sg[k] + F.grazers[c]; sn[k] = sn[k] + 1
          ss[k] = ss[k] + F.ice[c]
        end
      end
      for k = 1, #conts do
        local p, g = sp[k] / sn[k], sg[k] / sn[k]
        mp[k][#mp[k] + 1] = p; mg[k][#mg[k] + 1] = g
        accp[k] = accp[k] + p; accg[k] = accg[k] + g; accs[k] = accs[k] + ss[k] / sn[k]; nacc[k] = nacc[k] + 1
        if out then out:write(string.format("%d,%d,%.5f,%.5f,%.4f\n", month, k, p, g, ss[k] / sn[k])) end
      end
      local A = W.S.wild
      if A and y == monthly then
        local nw, pc, na, hc, npk = 0, 0, 0, 0, 0
        local hs, hn2 = 0, 0
        for p = 1, A.np do nw = nw + A.p_n[p]; pc = pc + A.p_cond[p] * A.p_n[p]; npk = npk + 1 end
        for h = 1, A.nh do
          na = na + A.h_n[h]; hc = hc + A.h_cond[h] * A.h_n[h]
          if W.grid.lat[A.h_cell[h]] >= 0 then hs = hs + A.h_cond[h] * A.h_n[h]; hn2 = hn2 + A.h_n[h] end
        end
        local pn, pcn = 0, 0
        for p = 1, A.np do if W.grid.lat[A.p_cell[p]] >= 0 then pn = pn + A.p_n[p]; pcn = pcn + A.p_cond[p] * A.p_n[p] end end
        print(string.format("    week %2d: %3d packs %5.0f wolves cond %.2f (north %.2f) | %6.0f grazers cond %.2f (north %.2f)",
          t, npk, nw, pc / math.max(nw, 1e-9), pcn / math.max(pn, 1e-9), na, hc / math.max(na, 1e-9), hs / math.max(hn2, 1e-9)))
      end
      if A then
        for p = 1, A.np do
          local id = A.p_id[p]
          packacc[id] = (packacc[id] or 0) + A.p_n[p] / 12
        end
      else
        for j = 1, #land do
          local c = land[j]
          packacc[c] = (packacc[c] or 0) + F.predators[c] / 12
        end
      end
    end
  end
  for k = 1, #conts do
    yp[k][y] = accp[k] / nacc[k]; yg[k][y] = accg[k] / nacc[k]; ysnow[k][y] = accs[k] / nacc[k]
  end
  do
    local rs, rn = {}, {}
    for j = 1, #land do
      local c = land[j]
      local r = reg[c]
      rs[r] = (rs[r] or 0) + W.F.predators[c]; rn[r] = (rn[r] or 0) + 1
    end
    for r, v in pairs(rs) do
      if rn[r] >= 5 then regy[r] = regy[r] or {}; regy[r][y] = v / rn[r] end
    end
  end
  for id, v in pairs(packacc) do
    local r = packs[id]
    if not r then r = { first = y, s = {} }; packs[id] = r end
    r.s[y - r.first + 1] = v
  end
  if A0 and y <= groups then
    local A = W.S.wild
    local np, nw, pc, nh, na, hc = 0, 0, 0, 0, 0, 0
    for p = 1, A.np do np = np + 1; nw = nw + A.p_n[p]; pc = pc + A.p_cond[p] * A.p_n[p] end
    for h = 1, A.nh do nh = nh + 1; na = na + A.h_n[h]; hc = hc + A.h_cond[h] * A.h_n[h] end
    print(string.format("  y%-3d packs %3d wolves %5.0f (%.1f/pack) cond %.2f | herds %4d animals %6.0f (%.1f/herd) cond %.2f",
      y, np, nw, nw / math.max(np, 1), pc / math.max(nw, 1e-9), nh, na, na / math.max(nh, 1), hc / math.max(na, 1e-9)))
    local d = W.scratch.wl_dbg
    if d then
      print(string.format("       wolves: +%.0f pups, -%.0f starved, -%.0f old age, -%.0f fire/cull; %.0f left, %.0f lived, %.0f joined, %.0f founded %d packs; %d packs gone",
        d.pups, d.pstarve, d.pmort, d.pother, d.left, d.live, d.joined, d.founded, d.newpacks, d.gonepacks))
      print(string.format("       grazers: +%.0f born, -%.0f starved/old, -%.0f preyed, -%.0f hunted; at fawning: winter low %.2f, condition %.2f, low < 0.4 %.0f%%",
        d.born, d.hstarve, d.pred, d.hunt, (d.lows or 0) / math.max(d.lown or 1, 1), (d.conds or 0) / math.max(d.lown or 1, 1), 100 * (d.lowbad or 0) / math.max(d.lown or 1, 1)))
      dbg_reset()
    end
  end
  if series and y % 5 == 0 then
    local parts = {}
    for k = 1, #conts do parts[#parts + 1] = string.format("c%d P %.4f H %.3f snow %.2f", conts[k], yp[k][y], yg[k][y], ysnow[k][y]) end
    local extra = ""
    if A0 then extra = string.format(" | %d packs %d herds", W.S.wild.np, W.S.wild.nh) end
    print(string.format("y%-4d %s%s", y, table.concat(parts, " | "), extra))
  end
end
if out then out:close() end

print(string.format("seed %d, %d years, %s, %s", seed, years, A0 and "groups" or "fields", nosoc and "no society" or "society"))
local sum = 0
for k = 1, #conts do
  local cm = cv(mp[k], 61)
  local cy, mean = cv(yp[k], 5)
  local cg = cv(yg[k], 5)
  -- growth of predators year to year vs the previous winter's snow
  local gr, sn = {}, {}
  for y = 6, years do gr[#gr + 1] = math.log((yp[k][y] + 1e-9) / (yp[k][y - 1] + 1e-9)); sn[#sn + 1] = ysnow[k][y - 1] end
  local rs = corr(gr, sn)
  -- lag-1 autocorrelation of yearly predators (cycles show a structure)
  local a, b = {}, {}
  for y = 6, years do a[#a + 1] = yp[k][y]; b[#b + 1] = yp[k][y - 1] end
  local ac1 = corr(a, b)
  a, b = {}, {}
  for y = 9, years do a[#a + 1] = yp[k][y]; b[#b + 1] = yp[k][y - 4] end
  local ac4 = corr(a, b)
  sum = sum + cm
  print(string.format("  cont %d: pred CV monthly %.3f | yearly %.3f (mean %.4f) | prey yearly CV %.3f | r(growth, snow) %s | ac1 %s ac4 %s",
    conts[k], cm, cy, mean, cg, rs and string.format("%.2f", rs) or "-", ac1 and string.format("%.2f", ac1) or "-", ac4 and string.format("%.2f", ac4) or "-"))
end
print(string.format("  mean continental monthly CV %.3f (test needs >= 0.15)", sum / #conts))
-- per group (or cell) CVs and synchrony
local ids = {}
for id, r in pairs(packs) do if #r.s >= 15 then ids[#ids + 1] = id end end
table.sort(ids)
local cs, n = 0, 0
for _, id in ipairs(ids) do local c = cv(packs[id].s, 3); cs = cs + c; n = n + 1 end
local cc, m = 0, 0
for a = 1, #ids do
  for b = a + 1, math.min(#ids, a + 30) do
    local ra, rb = packs[ids[a]], packs[ids[b]]
    local f0 = math.max(ra.first, rb.first)
    local f1 = math.min(ra.first + #ra.s - 1, rb.first + #rb.s - 1)
    if f1 - f0 >= 12 then
      local xa, xb = {}, {}
      for y = f0, f1 do xa[#xa + 1] = ra.s[y - ra.first + 1]; xb[#xb + 1] = rb.s[y - rb.first + 1] end
      local r = corr(xa, xb)
      if r then cc = cc + r; m = m + 1 end
    end
  end
end
-- spatial synchrony: correlation of patches' yearly predator densities by distance (same continent)
do
  local g = W.grid
  local ids = {}
  for r in pairs(regy) do ids[#ids + 1] = r end
  table.sort(ids)
  local bins = { { 0.25, 0, 0 }, { 0.5, 0, 0 }, { 0.8, 0, 0 }, { 9, 0, 0 } }
  for a = 1, #ids do
    for b = a + 1, #ids do
      local ca, cb = centres[ids[a]], centres[ids[b]]
      if geo.continent[ca] == geo.continent[cb] then
        local xa, xb = {}, {}
        for y = 5, years do if regy[ids[a]][y] and regy[ids[b]][y] then xa[#xa + 1] = regy[ids[a]][y]; xb[#xb + 1] = regy[ids[b]][y] end end
        local r = corr(xa, xb)
        if r then
          local dx, dy, dz = g.X[ca] - g.X[cb], g.Y[ca] - g.Y[cb], g.Z[ca] - g.Z[cb]
          local d = math.sqrt(dx * dx + dy * dy + dz * dz)
          for k = 1, #bins do if d < bins[k][1] then bins[k][2] = bins[k][2] + r; bins[k][3] = bins[k][3] + 1; break end end
        end
      end
    end
  end
  local parts = {}
  for k = 1, #bins do parts[#parts + 1] = string.format("<%.2f: %s (%d)", bins[k][1], bins[k][3] > 0 and string.format("%.2f", bins[k][2] / bins[k][3]) or "-", bins[k][3]) end
  print("  synchrony of patch predators by distance: " .. table.concat(parts, "  "))
end
print(string.format("  %s living 15+ years: %d, mean CV of yearly size %.3f, mean pairwise correlation %.3f (%d pairs)",
  A0 and "packs" or "cells", n, n > 0 and cs / n or 0, m > 0 and cc / m or 0, m))
