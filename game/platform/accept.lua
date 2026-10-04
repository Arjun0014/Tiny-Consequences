-- (S24) Readings of a village's state for the acceptance plays (scripts/play_accept.mjs, bridge `lua`): who lives
-- and who died, what stands, the beasts, the trees and fields, what burns - about a point or over the whole map.
-- Reads only; never writes the world.
local M = {}

local function d2(ax, ay, bx, by) return (ax - bx) * (ax - bx) + (ay - by) * (ay - by) end

-- s: settlement id; (x, y, r): a disc on its map (r nil: none); since: deaths counted from this hour.
function M.village(W, sid, x, y, r, now, since)
  local V = W.S.v3
  local s = V.settlements[sid]
  local act = require("sim.agents.act")
  local stock = require("sim.agents.stock")
  local r2 = r and r * r or -1
  x, y = x or s.sqx or 0, y or s.sqy or 0
  local o = { name = s.name, people = 0, inside = 0, hurt = 0, badly = 0, near = 0, near_hurt = 0, dead = 0, dead_near = 0,
    causes = {}, standing = 0, houses = 0, sites = 0, near_bld = 0, fallen = s.acc.fell or 0, burnt = s.acc.burnt or 0,
    beasts = 0, beasts_near = 0, beasts_dead = 0, trees = 0, trees_near = 0, trees_burnt_near = 0, stumps_near = 0,
    fields = 0, fields_near = 0, burning = #(s.burn or {}), fires = s.acc.fires or 0, lodged = 0, granary = 0, wild = 0, dogs = 0 }
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive and not p.at then
      o.people = o.people + 1
      if (p.inside or 0) ~= 0 then o.inside = o.inside + 1 end
      if (p.hurt or 0) > 0.05 then o.hurt = o.hurt + 1 end
      if (p.hurt or 0) > 0.5 then o.badly = o.badly + 1 end
      local px, py = act.pos(p, now)
      if d2(px, py, x, y) < r2 then
        o.near = o.near + 1
        if (p.hurt or 0) > 0.05 then o.near_hurt = o.near_hurt + 1 end
      end
    end
  end
  for _, p in pairs(V.people) do
    if not p.alive and p.sett == sid and (p.died or -1) >= (since or 1e18) - 1 then
      o.dead = o.dead + 1
      o.causes[p.died_of or "?"] = (o.causes[p.died_of or "?"] or 0) + 1
      if d2(p.x or 0, p.y or 0, x, y) < r2 then o.dead_near = o.dead_near + 1 end
    end
  end
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.state == "standing" and not b.natural then
      o.standing = o.standing + 1
      if b.kind == "house" then o.houses = o.houses + 1 end
      if d2(b.cx or b.dx, b.cy or b.dy, x, y) < r2 then o.near_bld = o.near_bld + 1 end
    elseif b.state == "site" then o.sites = o.sites + 1 end
  end
  if s.granary then o.granary = math.floor((s.granary.stock.grain or 0) + (s.granary.stock.sheaves or 0)) end
  for _, hh in pairs(V.households) do
    if hh.sett == sid and #hh.members > 0 and ((hh.lodge or 0) > 0 or (hh.home or 0) == 0) then o.lodged = o.lodged + 1 end
  end
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive then
        o.beasts = o.beasts + 1
        local ax, ay = stock.pos(a, now)
        if d2(ax, ay, x, y) < r2 then o.beasts_near = o.beasts_near + 1 end
      elseif (a.died or -1) >= (since or 1e18) - 1 then o.beasts_dead = o.beasts_dead + 1 end
    end
  end
  for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do if w.alive and not w.gone then o.wild = o.wild + 1 end end
  for _, dg in ipairs(s.dogs or {}) do if dg.alive then o.dogs = o.dogs + 1 end end
  local T = s.trees
  for k = 1, T.n do
    local st = T.st[k]
    if st == 1 then o.trees = o.trees + 1 end
    if d2(T.x[k], T.y[k], x, y) < r2 then
      if st == 1 then o.trees_near = o.trees_near + 1 elseif st == 3 then o.stumps_near = o.stumps_near + 1 end
      if (T.burnt and T.burnt[k]) then o.trees_burnt_near = o.trees_burnt_near + 1 end
    end
  end
  for _, pl in ipairs(s.plots) do
    if pl.alive then
      o.fields = o.fields + 1
      local ex = math.max(pl.x0 - x, 0, x - pl.x1)
      local ey = math.max(pl.y0 - y, 0, y - pl.y1)
      if ex * ex + ey * ey < r2 then o.fields_near = o.fields_near + 1 end
    end
  end
  return o
end

-- The herd `kind` of village sid now: each living beast's place and act, and the herd's spread and middle.
function M.herd(W, sid, kind, now)
  local s = W.S.v3.settlements[sid]
  local stock = require("sim.agents.stock")
  local H = s.herds and s.herds[kind or "flock"]
  local out = { n = 0, acts = {}, list = {} }
  if not H then return out end
  local sx, sy = 0, 0
  for _, a in ipairs(H.animals) do
    if a.alive then
      local ax, ay = stock.pos(a, now)
      out.n = out.n + 1
      sx, sy = sx + ax, sy + ay
      out.acts[a.act or "?"] = (out.acts[a.act or "?"] or 0) + 1
      out.list[#out.list + 1] = { k = a.k, x = ax, y = ay, act = a.act, sp = a.sp, health = a.health }
    end
  end
  if out.n > 0 then out.x, out.y = sx / out.n, sy / out.n end
  local m = 0
  for _, e in ipairs(out.list) do m = math.max(m, math.sqrt(d2(e.x, e.y, out.x, out.y))) end
  out.spread = m
  return out
end

-- The herd `kind` of village sid against what burns on its map now: how far each living beast is from the nearest
-- burning thing (by its edge), the mean and the nearest, how many within 0.2, and what burns (count, by kind).
function M.herd_fire(W, sid, kind, now)
  local s = W.S.v3.settlements[sid]
  local stock = require("sim.agents.stock")
  local danger = require("sim.agents.danger")
  local blaze = require("sim.agents.blaze")
  blaze.catch_up(W, s, now)
  local out = { n = 0, mean = 0, min = 99, near = 0, burning = 0, trees = 0, blds = 0, plots = 0, whys = {} }
  for _, f in ipairs(s.burn or {}) do
    if not f.out and f.heat > 0.02 then
      out.burning = out.burning + 1
      if f.kind == "tree" then out.trees = out.trees + 1 elseif f.kind == "bld" then out.blds = out.blds + 1 else out.plots = out.plots + 1 end
    end
  end
  local H = s.herds and s.herds[kind or "flock"]
  if not H then return out end
  for _, a in ipairs(H.animals) do
    if a.alive then
      local x, y = stock.pos(a, now)
      local _, d = danger.fire_near(s, x, y, 5)
      d = d or 5
      out.n = out.n + 1
      out.mean = out.mean + d
      if d < out.min then out.min = d end
      if d < 0.2 then out.near = out.near + 1 end
      out.whys[a.why or "?"] = (out.whys[a.why or "?"] or 0) + 1
    end
  end
  if out.n > 0 then out.mean = out.mean / out.n end
  return out
end

-- What burns on village sid's map, and the planet's fire on the cells its map covers (one fire at two scales).
function M.fire_scales(W, sid, now)
  local s = W.S.v3.settlements[sid]
  local blaze = require("sim.agents.blaze")
  blaze.catch_up(W, s, now)
  local mapm = require("sim.agents.map")
  local O = mapm.owners(W)
  local out = { burning = 0, cells = {} }
  for _, f in ipairs(s.burn or {}) do if not f.out and f.heat > 0.02 then out.burning = out.burning + 1 end end
  local seen = {}
  for i = 1, s.N * s.N do
    local cl = s.cell_of[i]
    if cl and not seen[cl] then
      seen[cl] = true
      local v = W.F.fire[cl] or 0
      if v > 0 then out.cells[#out.cells + 1] = string.format("%d%s:%.3f", cl, (O and O.by[cl] == sid) and "*" or "", v) end
    end
  end
  table.sort(out.cells)
  return out
end

-- (S24) The shape of herd `kind` of village sid now, as the eye sees it: its living beasts out of the pen - nearest
-- neighbours (median, 10th percentile), the spread about its middle (radius of gyration), how many clumps it falls into
-- (beasts within 2.5 spacings joined), how far its middle is from the herder, the share walking and how alike the
-- walkers' headings are (1: all one way).
function M.herd_shape(W, sid, kind, now)
  local s = W.S.v3.settlements[sid]
  local stock = require("sim.agents.stock")
  local H = s.herds and s.herds[kind or "flock"]
  local out = { n = 0 }
  if not H then return out end
  local P = {}
  local hx, hy, mv = 0, 0, 0
  for _, a in ipairs(H.animals) do
    if a.alive and a.act ~= "pen" then
      local x, y = stock.pos(a, now)
      P[#P + 1] = { x, y, a }
      if a.act == "walk" and a.t1 > a.t0 then
        local dx, dy = a.tx - a.x, a.ty - a.y
        local l = math.sqrt(dx * dx + dy * dy)
        if l > 1e-6 then hx, hy, mv = hx + dx / l, hy + dy / l, mv + 1 end
      end
    end
  end
  local n = #P
  out.n = n
  if n < 2 then return out end
  local cx, cy = 0, 0
  for _, p in ipairs(P) do cx, cy = cx + p[1], cy + p[2] end
  cx, cy = cx / n, cy / n
  local rg = 0
  local nn = {}
  local sxx, syy, sxy = 0, 0, 0
  for i = 1, n do
    local dx, dy = P[i][1] - cx, P[i][2] - cy
    sxx, syy, sxy = sxx + dx * dx, syy + dy * dy, sxy + dx * dy
    rg = rg + (P[i][1] - cx) ^ 2 + (P[i][2] - cy) ^ 2
    local b = 1e9
    for j = 1, n do
      if j ~= i then
        local d = (P[i][1] - P[j][1]) ^ 2 + (P[i][2] - P[j][2]) ^ 2
        if d < b then b = d end
      end
    end
    nn[i] = math.sqrt(b)
  end
  table.sort(nn)
  out.nn_med, out.nn_p10, out.rg = nn[math.floor(n / 2) + 1], nn[math.max(1, math.floor(n * 0.1))], math.sqrt(rg / n)
  -- (how drawn out it is: the long axis over the short, 1 a round clump)
  local tr, det = (sxx + syy) / n, (sxx * syy - sxy * sxy) / (n * n)
  local disc = math.sqrt(math.max(0, tr * tr / 4 - det))
  local l1, l2 = tr / 2 + disc, math.max(1e-12, tr / 2 - disc)
  out.elong = math.sqrt(l1 / l2)
  local sep = stock.SP(P[1][3].sp).sep
  out.sep = sep
  local lab = {}
  local clumps = 0
  for i = 1, n do
    if not lab[i] then
      clumps = clumps + 1
      local stack = { i }
      lab[i] = clumps
      while #stack > 0 do
        local k = table.remove(stack)
        for j = 1, n do
          if not lab[j] and (P[k][1] - P[j][1]) ^ 2 + (P[k][2] - P[j][2]) ^ 2 < (2.5 * sep) ^ 2 then lab[j] = clumps; stack[#stack + 1] = j end
        end
      end
    end
  end
  out.clumps = clumps
  out.walking = mv / n
  out.align = mv > 0 and math.sqrt(hx * hx + hy * hy) / mv or 0
  local p = stock.herder(W, s, H)
  if p then
    local x, y = stock.person_pos(p, now)
    out.herder_d = math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2)
    out.herder_walking = p.act == "walk"
  end
  out.cx, out.cy = cx, cy
  return out
end

-- (S24) The world about village sid now, for the powers' plays: its ground's water, air, rain, river and flood water,
-- snow, its fields (by stage, the worst blight), its herds' sickness, wild animals, and its sea (warmth, fish, oil),
-- shore food and gulls; a few of the planet's numbers on its cell.
function M.env(W, sid, now)
  local s = W.S.v3.settlements[sid]
  local F = W.F
  local c = s.cell
  local H = W.S.hydro
  local o = { soil = F.soil_water[c], temp = F.temp[c], anom = F.heat_anom[c], rain_now = s.rain_now or 0, snow = s.snow or 0,
    river = (F.flow[c] or 0) / math.max(1e-6, H and H.flow_mean and H.flow_mean[c] or 1), flood = 0, fields = {}, blight = 0,
    blighted = 0, sick = 0, sick_inc = 0, beasts = 0, wild = 0, wolves = 0, sea_t = nil, fish = nil, oil = 0, shore = s.shore_food,
    gulls = 0, gulls_oiled = s.acc.gulls_oiled or 0, forest = F.forest[c], grass = F.grass[c], aerosol = W.G.aerosol or 0 }
  local fd = require("sim.agents.map").FLOOD[s]
  if fd then for i = 1, s.N * s.N do if (fd[i] or 0) > 0 then o.flood = o.flood + 1 end end end
  for _, pl in ipairs(s.plots) do
    if pl.alive then
      o.fields[pl.stage] = (o.fields[pl.stage] or 0) + 1
      if (pl.blight or 0) > 0 then o.blighted = o.blighted + 1; o.blight = math.max(o.blight, pl.blight) end
    end
  end
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local Hd = s.herds and s.herds[kind]
    for _, a in ipairs(Hd and Hd.animals or {}) do
      if a.alive then
        o.beasts = o.beasts + 1
        if (a.sick or 0) > 0 then o.sick = o.sick + 1 elseif (a.sick_inc or 0) > 0 then o.sick_inc = o.sick_inc + 1 end
      end
    end
  end
  for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
    if w.alive and not w.gone then if w.pred then o.wolves = o.wolves + 1 else o.wild = o.wild + 1 end end
  end
  local sites = require("sim.society.sites")
  for _, cc in ipairs(sites.rings(W, c, 2)) do
    if W.geo.ocean[cc] == 1 then
      if not o.sea_t or F.temp[cc] > o.sea_t then o.sea_t = F.temp[cc] end
      o.fish = math.max(o.fish or 0, F.fish[cc] or 0)
      o.oil = math.max(o.oil, F.oil[cc] or 0)
    end
  end
  for _, f in ipairs(s.birds and s.birds.flocks or {}) do if f.alive ~= false then o.gulls = o.gulls + (f.n or 0) end end
  return o
end

return M
