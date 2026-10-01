-- Founding a v3 settlement (docs/v3/10_AGENTS.md §2, §9): the site, the first layout (well,
-- granary, woodstore, houses round the square, fields beyond, the woods with individual trees,
-- the lanes years of walking have worn), and its people — households of real families with
-- parents, children and grandparents, each with their own body, mind and skills.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local mapm = require("sim.agents.map")
local nav = require("sim.agents.nav")
local settle = require("sim.agents.settle")
local people = require("sim.agents.people")

local M = {}
local floor, max, min, sqrt, abs = math.floor, math.max, math.min, math.sqrt, math.abs
local function C() return cfgmod.V3 end

-- River cells as the view draws them (game/view/rivers.lua: the top 22% of land cells by mean
-- flow, at least 0.004) — the slice's settlements avoid them until fords and bridges exist.
function M.river_cells(W)
  local H = W.S.hydro
  local fm = H and H.flow_mean
  local out = {}
  if not fm then return out end
  local land = W.geo.land_cells
  local vals = {}
  for i = 1, #land do vals[i] = fm[land[i]] end
  table.sort(vals)
  local thr = vals[floor(#vals * 0.78) + 1] or 1e9
  if thr < 0.004 then thr = 0.004 end
  for i = 1, #land do
    local c = land[i]
    if fm[c] >= thr then out[c] = true end
  end
  return out
end

-- Best site: temperate, watered, wooded around but not a dense forest at its centre, every cell
-- of the local map land, no river through it.
function M.pick_site(W, opts)
  opts = opts or {}
  local F, g = W.F, W.grid
  local nb = W.grid.neighbours or W.grid.nb
  local rivers = M.river_cells(W)
  local land = W.geo.land_cells
  local best, bs = nil, -1e9
  for i = 1, #land do
    local c = land[i]
    local ok = W.geo.coast_dist[c] >= 3 and not rivers[c]
    local T = F.temp[c]
    if ok and T > 6 and T < 19 and abs(g.lat[c]) < 1.0 then
      -- neighbours: land, no rivers, some forest
      local fsum, n = 0, 0
      local ring = require("sim.society.sites").rings(W, c, 1)
      for k = 1, #ring do
        local d = ring[k]
        if W.geo.ocean[d] == 1 or rivers[d] then ok = false end
        fsum = fsum + F.forest[d]
        n = n + 1
      end
      if ok and n > 0 then
        local fo = fsum / n
        local sc = -abs(T - 12) * 0.08 + min(fo, 0.6) * 2 - abs(F.forest[c] - 0.3) + (F.soil_water[c] or 0) * 0.8
          + (F.fertility and F.fertility[c] or 0.5) * 0.5
        -- the world's clock starts at UTC midnight: prefer a place where that is just before dawn,
        -- so the first thing seen is the village waking (opts.dawn = false to ignore)
        if opts.dawn ~= false then
          local lh = (math.deg(g.lon[c]) / 15) % 24
          local d = abs(lh - 4.6)
          if d > 12 then d = 24 - d end
          sc = sc - d * 0.25
        end
        if sc > bs then best, bs = c, sc end
      end
    end
  end
  return best
end

-- Households at founding: { {sex, age, role}, ... } per household; "p" marks a pregnancy,
-- "c" a couple still waiting for a home of their own (married, living with his father).
M.TEMPLATE = {
  { { "m", 61, "farmer" }, { "f", 58, "farmer" }, { "m", 24, "woodcutter" }, { "f", 19, "farmer" } },
  { { "m", 38, "farmer" }, { "f", 35, "farmer" }, { "m", 14 }, { "f", 11 }, { "m", 7 }, { "f", 3 } },
  { { "m", 30, "woodcutter" }, { "f", 28, "farmer" }, { "f", 6 }, { "m", 2.2 }, { "f", 0.5 } },
  { { "m", 45, "builder" }, { "f", 42, "farmer" }, { "m", 17, "farmer" }, { "f", 15 }, { "m", 12 }, { "f", 68, "farmer" } },
  { { "m", 23, "builder" }, { "f", 21, "farmer", "p" } },
  { { "m", 50, "woodcutter" }, { "m", 22, "farmer", "c" }, { "f", 20, "farmer", "c" }, { "f", 16, "farmer" } },
  { { "m", 34, "farmer" }, { "f", 33, "farmer" }, { "m", 9 }, { "f", 5 }, { "m", 1.2 } },
}

local function lane_walk(s, x0, y0, x1, y1, amount)
  local path = nav.path(s, x0, y0, x1, y1, 0)
  if not path then return end
  for k = 2, path.n do
    mapm.wear_segment(s, path.x[k - 1], path.y[k - 1], path.x[k], path.y[k], amount)
  end
end

function M.found(W, cell, opts)
  opts = opts or {}
  local c = C()
  local V = W.S.v3
  local u_n = 0
  local key = rng.hash(W.seed, rng.S.V3_FOUND, cell, 1, 0)
  local function u()
    u_n = u_n + 1
    return rng.float(W.seed, rng.S.V3_FOUND, key, u_n, 0)
  end
  local culture = opts.culture or (1 + floor(u() * 4))
  local s = settle.new(W, cell, { name = names.village(u, culture), culture = culture })
  local N = s.N
  local c0 = floor(N / 2) + 1
  W.F.land_use[cell] = -1
  W.lu_ver = (W.lu_ver or 0) + 1
  -- 1. the square, the well, the granary, the woodstore (toward the woods)
  settle.add_building(W, s, "well", c0, c0, "s", "standing")
  s.sqx, s.sqy = mapm.ixy_xy(s, c0, c0 - 2)
  -- forest direction: which quarter of the map is most wooded?
  local quarters = { 0, 0, 0, 0 }
  for i = 1, N * N, 7 do
    local ix, iy = mapm.tile_ixy(s, i)
    local q = (ix > c0 and 1 or 0) + (iy > c0 and 2 or 0) + 1
    quarters[q] = quarters[q] + W.F.forest[s.cell_of[i]]
  end
  local fq, fv = 1, -1
  for q = 1, 4 do if quarters[q] > fv then fq, fv = q, quarters[q] end end
  local sx = (fq == 2 or fq == 4) and 1 or -1
  local sy = (fq >= 3) and 1 or -1
  settle.add_building(W, s, "granary", c0 - sx * 7 - 2, c0 - sy * 4 - 2, sy > 0 and "n" or "s", "standing")
  settle.add_building(W, s, "woodstore", c0 + sx * 10 - 2, c0 + sy * 8 - 1, sy > 0 and "s" or "n", "standing")
  -- 2. houses round the square, one per household
  local homes = {}
  for h = 1, #M.TEMPLATE do
    local ix, iy, side = settle.find_lot(W, s)
    homes[h] = settle.add_building(W, s, "house", ix, iy, side, "standing")
  end
  -- 3. fields beyond the houses, on the side away from the woods first
  local plots_wanted = opts.plots or 9
  local cands = {}
  local P = c.plot_tiles
  for iy = 4, N - P - 3, P + 1 do
    for ix = 4, N - P - 3, P + 1 do
      local x, y = mapm.ixy_xy(s, ix + P / 2, iy + P / 2)
      local d = sqrt(x * x + y * y)
      if d > 0.42 and d < 1.1 then
        local i = (iy + P / 2 - 1) * N + ix + P / 2
        local fo = W.F.forest[s.cell_of[i]]
        -- toward the woods costs clearing: fields lie mostly on the open side
        local toward = (x * sx + y * sy) / max(d, 1e-6)
        cands[#cands + 1] = { ix = ix, iy = iy, sc = d + fo * 0.25 + toward * 0.18 }
      end
    end
  end
  table.sort(cands, function(a, b) return a.sc < b.sc or (a.sc == b.sc and (a.iy * N + a.ix) < (b.iy * N + b.ix)) end)
  local placed = 0
  for i = 1, #cands do
    if placed >= plots_wanted then break end
    local cd = cands[i]
    if mapm.rect_free(s, cd.ix, cd.iy, cd.ix + P - 1, cd.iy + P - 1, true) then
      placed = placed + 1
      settle.add_plot(W, s, cd.ix, cd.iy, 0, "")
    end
  end
  -- 4. the woods: individual trees from the planet's forest, off the village ground
  local cleared = 0.42
  for iy = 2, N - 1, 2 do
    for ix = 2, N - 1, 2 do
      local jx = (u() - 0.5) * 1.6
      local jy = (u() - 0.5) * 1.6
      local x, y = mapm.ixy_xy(s, ix + jx, iy + jy)
      local i = mapm.tile_of(s, x, y)
      if i > 0 and s.land[i] == 1 and s.occ[i] == 0 and s.plot[i] == 0 then
        local r = sqrt(x * x + y * y)
        local fo = W.F.forest[s.cell_of[i]]
        -- the forest edge near the village is thinner (wood gathered for generations)
        local edge = r < cleared and 0 or min(1, (r - cleared) / 0.35)
        local pt = fo * 0.95 * edge
        local ut = u()
        if ut < pt then
          mapm.add_tree(s, x, y, 0.35 + 0.65 * u())
        elseif ut < pt + 0.02 * edge then
          mapm.add_tree(s, x, y, 0.15 + 0.3 * u())   -- a young tree in the open
        end
      end
    end
  end
  -- the planet forest each tree stands for (felling writes the field back)
  local sum = {}
  local tr = s.trees
  for k = 1, tr.n do
    if tr.st[k] == 1 then
      local cl = s.cell_of[tr.tile[k]]
      sum[cl] = (sum[cl] or 0) + tr.size[k]
    end
  end
  s.tree_share = {}
  for k = 1, tr.n do
    local cl = s.cell_of[tr.tile[k]]
    if sum[cl] and sum[cl] > 0 and not s.tree_share[cl] then s.tree_share[cl] = W.F.forest[cl] / sum[cl] end
  end
  mapm.rebuild_costs(s)
  -- 5. the people
  local now = W.hour
  local hlist = {}
  for h = 1, #M.TEMPLATE do
    local hb = homes[h]
    local hh = people.new_household(W, s.id, hb.id)
    hb.hh = hh.id
    s.households[#s.households + 1] = hh.id
    hlist[h] = hh
    local fam = {}
    local surname = names.surname(u, culture)
    for k = 1, #M.TEMPLATE[h] do
      local t = M.TEMPLATE[h][k]
      local age = t[2] + u() * 0.4
      local p = people.new(W, { sex = t[1], born = now - age * 1152, hh = 0, sett = s.id, surname = surname,
        culture = culture, x = hb.dx, y = hb.dy, role = t[3] or "" })
      people.join(W, p, hh)
      s.members[#s.members + 1] = p.id
      p.inside = hb.id
      fam[k] = { p = p, t = t }
      -- skills from years of practice in a role
      local yrs = max(0, age - 14)
      local function sk(base) return min(0.95, 0.2 + yrs * 0.02 * base + u() * 0.1) end
      p.sk_farm = sk(p.role == "farmer" and 1.6 or 0.8)
      p.sk_wood = sk(p.role == "woodcutter" and 1.8 or 0.4)
      p.sk_build = sk(p.role == "builder" and 1.8 or 0.4)
      if age >= c.adult_age and p.sex == "m" and u() < 0.5 then p.strong = true end
      if t[4] == "p" then p.preg_due = now + (2.4 + u()) * 24 end
    end
    -- family ties: the first two of a household (when man and woman of an age) are a couple;
    -- the young are their children; a much older member is a grandparent
    local a, b = fam[1], fam[2]
    local couple = b and a.t[1] ~= b.t[1] and abs(a.t[2] - b.t[2]) < 8
    if couple then
      a.p.partner, b.p.partner = b.p.id, a.p.id
      a.p.married, b.p.married = now - 5 * 1152, now - 5 * 1152
    end
    for k = 2, #fam do
      local q = fam[k]
      if q.t[4] == "c" then
        -- (handled below: the waiting couple)
      elseif (couple and k >= 3 or (not couple and k >= 2)) and q.t[2] < a.t[2] - 14 and q.t[2] < 40 then
        local father = a.t[1] == "m" and a.p or (b and b.t[1] == "m" and b.p)
        local mother = a.t[1] == "f" and a.p or (b and b.t[1] == "f" and b.p)
        if not couple then mother = nil end
        if father then q.p.father = father.id; father.kids[#father.kids + 1] = q.p.id end
        if mother and mother ~= q.p then q.p.mother = mother.id; mother.kids[#mother.kids + 1] = q.p.id end
      end
    end
    -- the waiting couple (married, no home of their own yet)
    local wa, wb
    for k = 1, #fam do
      if fam[k].t[4] == "c" then if not wa then wa = fam[k].p else wb = fam[k].p end end
    end
    if wa and wb then
      wa.partner, wb.partner = wb.id, wa.id
      wa.married, wb.married = now - 30 * 24, now - 30 * 24
      wb.surname = wa.surname
      local wife = wa.sex == "f" and wa or wb
      s.couples[#s.couples + 1] = wife.id
      people.remember(wa, now, "Married; waiting for a house of our own")
      people.remember(wb, now, "Married; waiting for a house of our own")
    end
    -- infants are looked after by their mother
    for k = 1, #fam do
      local q = fam[k].p
      if people.stage(q, now) == "infant" then
        q.carer = q.mother ~= 0 and q.mother or (fam[2] and fam[2].p.id or 0)
        local m = V.people[q.carer]
        if m then m.nursing = now + 600 end
      end
      q.stage_was = people.stage(q, now)
    end
    -- bonds within the family
    for k = 1, #fam do
      for j = 1, #fam do
        if j ~= k then people.bond(fam[k].p, fam[j].p, 0.45 + 0.2 * u()) end
      end
    end
    -- stocks at home
    hb.stock.food = (#fam) * (1.5 + 1.5 * u())
    hb.stock.logs = 2
    hb.stock.water = u() < 0.6 and 1 or 0
  end
  -- neighbours and friends across households (a little affinity, children more)
  local mem = s.members
  for i = 1, #mem do
    local p = V.people[mem[i]]
    for k = 1, 3 do
      local q = V.people[mem[1 + floor(u() * #mem)]]
      if q ~= p and q.hh ~= p.hh then
        local st1, st2 = people.stage(p, now), people.stage(q, now)
        local amt = (st1 == "child" and st2 == "child") and 0.4 or 0.15 + 0.15 * u()
        people.bond(p, q, amt)
        people.bond(q, p, amt * 0.8)
      end
    end
  end
  -- fields belong to households (round robin, the larger families first)
  for k = 1, #s.plots do
    local hh = hlist[(k - 1) % #hlist + 1]
    local pl = s.plots[k]
    pl.hh = hh.id
    local m = V.people[hh.members[1]]
    pl.name = (m and m.surname or "Common") .. " field" .. (k > #hlist and " II" or "")
  end
  -- 6. stores: last year's harvest in the granary, cut wood in the woodstore
  local pop = #mem
  s.granary.stock.grain = pop * (opts.food_days or 30) + #s.plots * 8 * c.seed_row
  s.wood_store.stock.logs = 14
  -- 7. the lanes years of walking have worn: each house to the well, granary, woodstore and fields
  for pass = 1, 2 do
    for h = 1, #homes do
      local hb = homes[h]
      lane_walk(s, hb.dx, hb.dy, s.well.dx, s.well.dy, 30)
      lane_walk(s, hb.dx, hb.dy, s.granary.dx, s.granary.dy, 24)
    end
    lane_walk(s, s.granary.dx, s.granary.dy, s.wood_store.dx, s.wood_store.dy, 30)
    for k = 1, #s.plots do
      local pl = s.plots[k]
      lane_walk(s, s.granary.dx, s.granary.dy, pl.stx, pl.sty, 26)
    end
    mapm.daily_wear(s)
    mapm.rebuild_costs(s)
  end
  return s
end

return M
