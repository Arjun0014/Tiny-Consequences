-- A meteor's impact on a village's map (S24, D-188). One shock from one energy, felt by everything by its
-- distance from the point where the rock struck and by what it is:
--   the crater   within imp_crater all is gone - dug out and thrown up: whoever and whatever stood there,
--                the trees, the buildings and what they held; the ground broken (s.craters, kept)
--   the blast    the air's overpressure P(r): buildings fall by how they are built and how sound they are (on
--                whoever is inside), trees snap and lie flat pointing away from it, people and beasts out of
--                doors are thrown down and hurt (indoors under a roof that stands, a quarter of it)
--   the flash    the fireball's heat Q(r): what is dry and in the open catches (trees, thatch, ripe corn,
--                stooks), whoever is in the open is burnt, the grass burnt off and the ground scorched
--   the shaking  the quake of the impact (shake.quake, by the caller): felt by every village by distance
-- Laws of distance and each thing's own strength - never a list of what must die. What follows is the world's
-- own: fires spread by dryness and wind, the hurt are tended or die of their wounds, the trapped dug out, the
-- homeless lodged, the dead mourned and buried, the thrown timber carried off for building, the crater stays.
--   s.craters = { { x, y, r, t, deed } }   s.scorch = { { x, y, r, t } }   (both kept; the view draws them)
local sfmt = require("sim.fmt").format
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local dmath = require("sim.dmath")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")
local mapm = require("sim.agents.map")
local blaze = require("sim.agents.blaze")
local shake = require("sim.agents.shake")
local stock = require("sim.agents.stock")
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local dsq = dmath.sq
local function C() return cfgmod.V3 end

local function u(W, s, now, a, b)
  return rng.float(W.seed, rng.S.V3_IMPACT, (s.key or s.id) % 4294967296, floor(now * 3600) % 4294967296, a * 7919 + (b or 0))
end

-- The overpressure and the flash's heat at distance r (the crater's edge is the nearest any of it is felt).
function M.over(r)
  local c = C()
  local q = c.imp_blast / max(r, c.imp_crater * 0.6)
  return q * q
end
function M.flash(r)
  local c = C()
  local q = c.imp_flash / max(r, c.imp_crater * 0.6)
  return q * q
end

-- The harm the blast and the flash do to a body out of doors at r (people, beasts, wild animals, dogs alike;
-- `cover` 1 in the open, less behind walls), with its own share of luck.
local function harm(W, s, now, r, cover, key)
  local c = C()
  local luck = 0.7 + 0.6 * u(W, s, now, 11, key)
  return (c.imp_hurt_blast * M.over(r) * (cover < 1 and 0.25 or 1) + c.imp_burn * M.flash(r) * cover) * luck
end

local function dist(ax, ay, bx, by) return sqrt(dsq(ax - bx) + dsq(ay - by)) end

-- The rock strikes map s at (x, y) at `now`; `ev` the cast's event. Returns the impact's deed (with what it did).
function M.strike(W, s, x, y, now, ev)
  local c = C()
  local V = W.S.v3
  local d = deeds.add(W, { kind = "impact", t = now, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
    to_s = s.id, what = "meteor", n = 1, fu = 0, harm = true, event = ev })
  deeds.learn(W, s, d, "the fire from the sky", 0, 0, now)
  local got = { dead = 0, hurt = 0, fell = 0, houses = 0, beasts = 0, wild = 0, dogs = 0, flat = 0, lit = 0, fields = 0 }
  local dry = blaze.dryness(W, s)
  local N, T = s.N, s.T
  local R0 = c.imp_crater
  -- 1. the crater: the trees in it gone, its buildings gone with all they held and everyone in them
  s.craters = s.craters or {}
  s.craters[#s.craters + 1] = { x = x, y = y, r = R0, t = now, deed = d.id }
  local Tr = s.trees
  blaze.trees_near(s, x, y, R0, function(k)
    Tr.st[k], Tr.logs[k], Tr.t_cut[k] = 0, 0, now
    s.tree[Tr.tile[k]] = 0
    if Tr.burnt then Tr.burnt[k] = nil end
    if Tr.fall then Tr.fall[k] = nil end
  end)
  local in_crater = {}
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    if b and b.state ~= "gone" and not b.natural and dist(b.cx or b.dx, b.cy or b.dy, x, y) < R0 + T * 0.5 * max(b.w or 1, b.h or 1) * 0.7 then
      in_crater[#in_crater + 1] = b
    end
  end
  for _, b in ipairs(in_crater) do
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and p.inside == b.id and not p.at then people.hurt(p, 1, "crushed", d.id, now) end
    end
    if b.stock then for key in pairs(b.stock) do if type(b.stock[key]) == "number" then b.stock[key] = 0 end end end
    if b.state == "standing" then
      got.fell = got.fell + 1
      if b.kind == "house" then got.houses = got.houses + 1 end
      shake.fall_in(W, s, b, now, d, "impact")
    else
      require("sim.agents.settle").remove_building(W, s, b)
    end
  end
  -- (the broken ground: nothing is built or sown on it; it is rough to cross)
  local i0 = mapm.tile_of(s, x, y)
  if i0 > 0 then
    local ix0, iy0 = (i0 - 1) % N + 1, floor((i0 - 1) / N) + 1
    local Rt = floor(R0 / T) + 1
    for iy = max(2, iy0 - Rt), min(N - 1, iy0 + Rt) do
      for ix = max(2, ix0 - Rt), min(N - 1, ix0 + Rt) do
        local i = (iy - 1) * N + ix
        local tx, ty = mapm.ixy_xy(s, ix, iy)
        if dist(tx, ty, x, y) < R0 and s.land[i] == 1 and s.occ[i] == 0 and s.riv[i] == 0 then
          if s.plot[i] == 0 then s.plot[i] = -3 end
        end
      end
    end
    s.dirty_costs = true
  end
  -- 2. the blast on the buildings (before the people: those inside a house that falls are under it)
  local fell = {}
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    local vu = b and b.state == "standing" and not b.natural and c.imp_bld[b.kind]
    if vu and vu > 0 then
      local r = dist(b.cx or b.dx, b.cy or b.dy, x, y)
      local P = M.over(r) * vu
      local strength = (0.7 + 0.6 * u(W, s, now, 21, b.id)) * (0.6 + 0.4 * (b.cond or 1))
      if P >= strength then fell[#fell + 1] = b
      elseif P >= c.imp_crack * strength then
        b.cond = max(0.05, (b.cond or 1) - 0.6 * P / strength)
        s.acc.cracked = (s.acc.cracked or 0) + 1
      end
    end
  end
  for _, b in ipairs(fell) do
    got.fell = got.fell + 1
    if b.kind == "house" then got.houses = got.houses + 1 end
    shake.fall_in(W, s, b, now, d, "blast")
  end
  -- 3. the people: the blast and the flash on whoever is out of doors (or under a roof still standing)
  danger.each_on_map(W, s, function(p)
    local px, py = danger.person_pos(p, now)
    local r = dist(px, py, x, y)
    if r < R0 then
      people.hurt(p, 1, "burn", d.id, now)
    else
      local inside = (p.inside or 0) ~= 0 and s.buildings[p.inside] and s.buildings[p.inside].state == "standing"
      local h = harm(W, s, now, r, inside and 0 or 1, p.id)
      if h > 0.02 then
        people.hurt(p, h, inside and "crushed" or "burn", d.id, now)
        got.hurt = got.hurt + 1
        if h > 0.3 then people.remember(p, now, inside and "The house shook about us: the fire from the sky" or "The fire from the sky threw me down and burnt me") end
      end
    end
    -- (a babe in someone's arms takes what they take)
    if (p.hurt or 0) >= 1 and blaze.kill then
      got.dead = got.dead + 1
      blaze.kill(W, s, p, "meteor", now)
    end
  end)
  -- 4. the beasts of the herds (a byre's or a sty's roof keeps the flash off those inside it while it stands)
  local slain = {}
  if s.herds then
    for _, kind in ipairs(stock.HERDS) do
      local H = s.herds[kind]
      if H then
        local pen = s.buildings[H.pen or 0]
        local roofed = pen and pen.state == "standing" and (pen.kind == "byre" or pen.kind == "sty")
        local L = stock.live_list(H)
        local list = {}
        for i = 1, #L do list[i] = H.animals[L[i]] end
        for _, a in ipairs(list) do
          if a.alive then
            local ax, ay = stock.pos(a, now)
            local r = dist(ax, ay, x, y)
            local cover = (roofed and stock.inside_pen(s, pen, ax, ay)) and 0 or 1
            local h = r < R0 and 2 or harm(W, s, now, r, cover, 100000 + a.k * 7 + #kind)
            if h > 0.02 then
              a.health = (a.health or 1) - h
              if a.health <= 0 then
                stock.dies(W, s, H, a, now, "meteor", "One of our %s was killed by the fire from the sky")
                slain[a.sp] = (slain[a.sp] or 0) + 1
                got.beasts = got.beasts + 1
              end
            end
          end
        end
      end
    end
  end
  for _, sp in ipairs({ "sheep", "goat", "cattle", "pig" }) do
    if (slain[sp] or 0) > 0 then
      local dd = deeds.add(W, { kind = "slain", t = now, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
        to_s = s.id, what = sp, n = slain[sp], fu = 0, harm = true, answers = d.id })
      deeds.learn(W, s, dd, "the fire from the sky", 0, 0, now)
    end
  end
  -- 5. wild animals and dogs on the map
  for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
    if w.alive and w.act ~= "leave" and not w.gone then
      local wx, wy = wild.pos(w, now)
      local r = dist(wx, wy, x, y)
      local h = r < R0 and 2 or harm(W, s, now, r, 1, 200000 + w.k)
      if h > 0.02 then
        w.hp = (w.hp or 1) - h
        if w.hp <= 0 then wild.dies(W, s, w, now, "meteor"); got.wild = got.wild + 1 end
      end
    end
  end
  for _, dg in ipairs(s.dogs or {}) do
    if dg.alive then
      local gx, gy = dogs.pos(dg, now)
      local r = dist(gx, gy, x, y)
      local h = r < R0 and 2 or harm(W, s, now, r, 1, 300000 + (dg.k or 0))
      if h > 0.02 then
        dg.hp = (dg.hp or 1) - h
        if dg.hp <= 0 then dogs.dies(W, s, dg, now, "meteor", "Our dog was killed by the fire from the sky"); got.dogs = got.dogs + 1 end
      end
    end
  end
  -- 6. the trees: thrown flat by the blast (the trunk lies pointing away from the rock, its logs there to
  -- be had), the rest by the flash set alight where they are dry
  Tr.fall = Tr.fall or {}
  local reach = c.imp_flash * c.imp_reach_k
  blaze.trees_near(s, x, y, reach, function(k, d2)
    if Tr.st[k] ~= 1 then return end
    local r = sqrt(d2)
    local P = M.over(r)
    if P >= 0.8 + 0.6 * u(W, s, now, 31, k) then
      Tr.st[k] = 2
      Tr.logs[k] = mapm.tree_logs(Tr.size[k])
      Tr.t_cut[k] = now
      Tr.fall[k] = dmath.atan2(Tr.y[k] - y, Tr.x[k] - x)
      got.flat = got.flat + 1
      local cell = s.cell_of[Tr.tile[k]]
      if W.F.forest[cell] then W.F.forest[cell] = max(0, W.F.forest[cell] - c.fire_tree_forest) end
    else
      local pign = 1 - dmath.exp(-M.flash(r) * (0.3 + dry) * c.imp_ign)
      if u(W, s, now, 32, k) < pign and blaze.ignite(W, s, "tree", k, now, d.id) then got.lit = got.lit + 1 end
    end
  end)
  s.tv = (s.tv or 0) + 1
  -- 7. the fields: the corn in the crater gone, flattened by the blast, the dry or ripe set alight by the flash
  for j = 1, #s.plots do
    local pl = s.plots[j]
    if pl.alive then
      local ex = max(pl.x0 - x, 0, x - pl.x1)
      local ey = max(pl.y0 - y, 0, y - pl.y1)
      local r = sqrt(ex * ex + ey * ey)
      if r < reach then
        local P = M.over(r)
        local hit = false
        if r < R0 then
          -- (thrown up with the crater: what stood on it is gone, the earth from below lies over it)
          if pl.stage == "growing" or pl.stage == "ripe" then pl.sown, pl.harvested, pl.growth = 0, 0, 0; pl.stage = "fallow"; hit = true end
          pl.sheaves = 0
          pl.fert = max(0.3, (pl.fert or 1) - 0.4)
        elseif pl.stage == "growing" and P > 0.15 then
          -- (laid flat: the stalks broken, part of the yield lost)
          pl.frost = (pl.frost or 1) * max(0.1, 1 - c.imp_plot_lodge * min(1, P))
          hit = true
        end
        local pign = 1 - dmath.exp(-M.flash(r) * (0.3 + dry) * c.imp_ign * c.fire_plot_catch)
        if blaze.plot_fuel(pl) > 0 and u(W, s, now, 41, j) < pign and blaze.ignite(W, s, "plot", j, now, d.id) then hit = true end
        if hit then got.fields = got.fields + 1 end
      end
    end
  end
  -- 8. the ground: the grass burnt off under the fireball and the earth scorched (the pasture there is bare)
  s.scorch = s.scorch or {}
  s.scorch[#s.scorch + 1] = { x = x, y = y, r = c.imp_scorch, t = now }
  local P = s.pasture
  if P and P.grass then
    local BS = stock.BS
    local bn = N / BS
    for b = 1, bn * bn do
      local bx, by = (b - 1) % bn, floor((b - 1) / bn)
      local cx, cy = ((bx + 0.5) * BS - N / 2) * T, ((by + 0.5) * BS - N / 2) * T
      if P.grass[b] and dist(cx, cy, x, y) < c.imp_scorch then P.grass[b] = 0 end
    end
  end
  -- 9. every living beast within reach looks up at once; the people already know (the quake, the fires)
  blaze.jolt(W, s, x, y, now, c.imp_jolt, c.imp_jolt_k)
  -- (S25, D-206) the strike itself is the danger: seen, heard and felt over the whole map, sleepers woken - whoever is near
  -- the point runs from it (think.impact_choice), whatever else they were doing
  danger.cue(W, s, x, y, now, { kind = "impact", src = 0, sight = 8, night = 8, sound = 8, loud = true, why = "Fire fell from the sky" })
  d.dead, d.hurt, d.fell, d.houses, d.beasts, d.flat, d.lit, d.fields = got.dead, got.hurt, got.fell, got.houses,
    got.beasts + got.wild + got.dogs, got.flat, got.lit, got.fields
  s.acc.impacts = (s.acc.impacts or 0) + 1
  return d, got
end

return M
