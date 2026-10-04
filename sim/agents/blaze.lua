-- Fire on a settlement's map and the lightning that starts it (R6.5b, D-154, docs/v3/65_R6_5_LIVING_PLANET.md
-- §6). A fire is burning things - a tree, a building - each where it is, with its heat (0..1) and the
-- fuel it has left; it spreads to things near it by dryness, heat and the wind, burns what is in a
-- building, burns out, and is put out by rain or by people throwing water on it. Lightning strikes a
-- point and hits the tallest thing near it: a tree, a roof, someone standing in the open. Who notices,
-- who flees, who carries water, who is hurt and what is lost is everyone's own (danger cues, think's
-- urgent choice, people.hurt, life, settle) - nothing here decides an outcome.
--   s.burn = { { kind = "tree" | "bld", ref, x, y, heat, fuel, t0, deed } }
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")
local dsq = require("sim.dmath").sq
local stock = require("sim.agents.stock")   -- (S22: beasts as lightning's targets; none of these requires this)
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local function u(W, s, now, a, b)
  return rng.float(W.seed, rng.S.V3_CONFLICT, s.key or s.id, floor(now * 360) % 4294967296, a * 1000 + (b or 0))
end

-- How dry things are on map s now: the ground's water (the planet's soil water), less while it rains.
function M.dryness(W, s)
  local c = C()
  local sw = W.F.soil_water[s.cell] or 0.5
  local d = max(0, min(1, (c.fire_wet - sw) / (c.fire_wet - c.fire_dry)))
  local r = s.rain_now or 0
  if r == 2 then d = d * 0.15 elseif r == 1 then d = d * 0.45 end
  if s.snow then d = d * max(0, 1 - s.snow / 0.05) end   -- (S20, R6.5d) under snow
  return d
end

-- Each standing tree within r of (x, y): fn(k, d2), through the tiles about the point (a map holds
-- thousands of trees: never scanned whole).
local mapm = require("sim.agents.map")
local function trees_near(s, x, y, r, fn)
  local N, Tl = s.N, s.T
  local i0 = mapm.tile_of(s, x, y)
  if i0 == 0 then return end
  local ix0, iy0 = (i0 - 1) % N + 1, floor((i0 - 1) / N) + 1
  local R = floor(r / Tl) + 1
  local T = s.trees
  local r2 = r * r
  for iy = max(1, iy0 - R), min(N, iy0 + R) do
    for ix = max(1, ix0 - R), min(N, ix0 + R) do
      local k = s.tree[(iy - 1) * N + ix]
      if k and k > 0 and T.st[k] == 1 then
        local d2 = dsq(T.x[k] - x) + dsq(T.y[k] - y)
        if d2 < r2 then fn(k, d2) end
      end
    end
  end
end
M.trees_near = trees_near

local function find(s, kind, ref)
  local B = s.burn
  for i = 1, #(B or {}) do if B[i].kind == kind and B[i].ref == ref then return B[i], i end end
  return nil
end
M.find = find

-- The burning thing nearest (x, y) within r, or nil.
function M.near(s, x, y, r, W, now)
  local B = s.burn
  if not B then return nil end
  if W and now then M.catch_up(W, s, now) end
  local best, bd = nil, r * r
  for i = 1, #B do
    local d = dsq(B[i].x - x) + dsq(B[i].y - y)
    if d <= bd then best, bd = B[i], d end
  end
  return best, sqrt(bd)
end

-- Set something alight: the fire is seen (and smelt) from afar; those inside a burning house know it at
-- once. `cause`: the deed that started it.
local PENS = { fold = true, byre = true, sty = true }   -- (S20: the beasts' pens)

-- (S20) What a field has to burn (0 none): ripe corn standing, sheaves in stooks, corn drying off
-- toward the harvest, stubble (a little).
function M.plot_fuel(pl)
  if not pl.alive then return 0 end
  local fu = 0
  if pl.stage == "ripe" and pl.harvested < pl.sown then fu = fu + (pl.sown - pl.harvested) / max(1, pl.rows) end
  if pl.stage == "growing" and (pl.growth or 0) >= 0.7 then fu = fu + 0.5 * (pl.growth - 0.5) end
  if (pl.sheaves or 0) > 0 then fu = fu + 0.5 end
  if pl.stage == "stubble" or pl.stage == "retting" then fu = fu + 0.25 end
  return fu
end

function M.ignite(W, s, kind, ref, now, cause)
  local c = C()
  if find(s, kind, ref) then return nil end
  local x, y, fuel
  local r = 0
  if kind == "tree" then
    local T = s.trees
    if (T.st[ref] or 0) ~= 1 then return nil end
    x, y, fuel = T.x[ref], T.y[ref], c.fire_tree_h * (0.5 + 0.5 * (T.size[ref] or 1))
  elseif kind == "plot" then
    -- (S20) a field: what stands on it or lies in stooks burns (green corn hardly)
    local pl = s.plots[ref]
    local fu = pl and M.plot_fuel(pl) or 0
    if fu <= 0 then return nil end
    x, y, fuel = (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5, c.fire_plot_h * fu
    r = (pl.x1 - pl.x0) * 0.5
  else
    local b = s.buildings[ref]
    if not b or b.state ~= "standing" or b.natural then return nil end
    x, y, fuel = b.cx or b.dx, b.cy or b.dy, c.fire_bld_h * (b.kind == "house" and 1 or 0.7)
  end
  s.burn = s.burn or {}
  local f = { kind = kind, ref = ref, x = x, y = y, heat = 0.25, fuel = fuel, t0 = now, t = now, deed = cause, r = r > 0 and r or nil }
  s.burn[#s.burn + 1] = f
  s.acc.fires = (s.acc.fires or 0) + 1
  M.cue(W, s, f, now, kind == "bld" and "Fire! The roof is burning" or (kind == "plot" and "Fire in the fields" or "Fire in the trees"))
  -- (S20) a pen alight: the beasts in it are up at once
  if kind == "bld" and PENS[s.buildings[ref].kind] and M.rouse_pen then M.rouse_pen(W, s, s.buildings[ref], now) end
  -- (S22) the beasts, the wild animals and the dogs that see or smell it look up, each in its own time
  if M.alarm_beasts then M.alarm_beasts(W, s, f, now) end
  return f
end

-- The fire seen: by day from far, by night farther still (its light); its smoke smelt about it; those
-- inside a burning building are alerted at once.
function M.cue(W, s, f, now, why)
  local c = C()
  danger.cue(W, s, f.x, f.y, now, { kind = "fire", src = 0, sight = c.fire_sight, night = c.fire_sight * 1.3,
    sound = c.fire_shout, loud = true, why = why })
  if f.kind == "bld" then
    for _, id in ipairs(s.members) do
      local q = W.S.v3.people[id]
      if q and q.alive and q.inside == f.ref and not q.at then
        danger.raise(W, q, { kind = "fire", src = 0, why = "The roof over us is burning" }, f.x, f.y, now, "saw")
      end
    end
  end
end

-- Water thrown on the fire nearest (x, y): less heat.
function M.douse(W, s, x, y, now)
  local f = M.near(s, x, y, C().douse_r, W, now)
  if not f then return false end
  -- (water tells early; a roof well alight hardly notices a bucket)
  f.heat = max(0, f.heat - C().douse_k * max(0.05, 1.1 - f.heat))
  return true
end

-- (S20) A field burnt out: the corn standing on it and the sheaves in its stooks are lost (nothing
-- left to cut), the stubble burnt off (its ash a little richness for the soil); its owners remember.
function M.field_burnt(W, s, f, now)
  local c = C()
  local V = W.S.v3
  local pl = s.plots[f.ref]
  if not pl then return end
  local lost = 0
  if pl.stage == "ripe" and pl.harvested < pl.sown then
    lost = lost + (pl.sown - pl.harvested)
    pl.harvested = pl.sown
    -- (the rows still standing are ash: nothing left for the reapers to finish)
    for r = 1, pl.rows do if pl.row_st[r] == 2 then pl.row_st[r] = 3 end end
    pl.row_p = nil
  end
  if pl.stage == "retting" then pl.flax_ret = 0 end
  if pl.stage == "growing" and (pl.growth or 0) >= 0.7 then
    lost = lost + pl.sown
    pl.frost = (pl.frost or 1) * 0.2   -- (what survives the fire green is little)
  end
  local sheaves = pl.sheaves or 0
  pl.sheaves = 0
  pl.fert = min(c.fert_max, (pl.fert or 1) + c.fire_plot_ash)
  pl.burnt_t = now
  s.acc.fields_burnt = (s.acc.fields_burnt or 0) + 1
  local hh = V.households[pl.hh or 0]
  local d = deeds.add(W, { kind = "burnt", t = now, at = s.id, x = f.x, y = f.y, by = 0, by_hh = 0, by_s = 0, to = 0,
    to_hh = hh and hh.id or 0, to_s = s.id, what = "field", n = 1, fu = (lost + sheaves) * 2, harm = lost + sheaves > 0, answers = f.deed })
  deeds.learn(W, s, d, "the fire", 0, 0, now)
  if hh and lost + sheaves > 0 then
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and people.stage(q, now) ~= "infant" then
        people.remember(q, now, sheaves > 0 and lost == 0 and "The fire took the sheaves on our field" or "Our field burnt: the corn on it is lost")
      end
    end
  end
end

-- What a burning building holds burns with it.
local BURNS = { "food", "grain", "sheaves", "smoked", "cheese", "meat", "fish", "meals", "logs", "wool", "cloth", "hides",
  "leather", "flax", "hay" }

local function burn_out(W, s, f, now)
  local c = C()
  local V = W.S.v3
  if f.kind == "tree" then
    local T = s.trees
    T.st[f.ref] = 3
    T.logs[f.ref] = 0
    T.t_cut[f.ref] = now
    -- (S24) a charred trunk stands where it burnt (the view draws it black) until the ground greens again
    T.burnt = T.burnt or {}
    T.burnt[f.ref] = now
    s.tv = (s.tv or 0) + 1
    -- (the planet's woods are thinner by it: as a felled tree, D-078)
    local cell = s.cell
    if W.F.forest[cell] then W.F.forest[cell] = max(0, W.F.forest[cell] - c.fire_tree_forest) end
    s.acc.trees_burnt = (s.acc.trees_burnt or 0) + 1
    return
  end
  if f.kind == "plot" then return M.field_burnt(W, s, f, now) end
  local b = s.buildings[f.ref]
  if not b then return end
  local hh = V.households[b.hh or 0]
  local d = deeds.add(W, { kind = "burnt", t = now, at = s.id, x = f.x, y = f.y, by = 0, by_hh = 0, by_s = 0, to = 0,
    to_hh = hh and hh.id or 0, to_s = s.id, what = b.kind, n = 1, fu = c.house_fu, harm = true, answers = f.deed })
  deeds.learn(W, s, d, "the fire", 0, 0, now)
  -- (a ruin: its ground cleared later; the household without a roof lodges with others until a house
  -- of its own stands - as households come from elsewhere do, D-119)
  s.scars = s.scars or {}
  s.scars[#s.scars + 1] = { x = f.x, y = f.y, t = now, kind = b.kind }
  local settle = require("sim.agents.settle")
  settle.remove_building(W, s, b)
  s.acc.burnt = (s.acc.burnt or 0) + 1
  if hh and #hh.members > 0 and b.kind == "house" then
    hh.home = 0
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and people.stage(q, now) ~= "infant" then people.remember(q, now, "Our house burnt down") end
    end
    if M.lodge then M.lodge(W, s, hh, now, "their house burnt") end
  end
  -- (S24) those lodging under its roof have none now either (they lodge anew, or camp)
  if M.unroof then M.unroof(W, s, b, now, "their house burnt", hh) end
end

M.burn_out = burn_out   -- (S21: premise.house_burnt)

-- The wind of map s: the prevailing wind of its latitude band (easterly trades toward the equator,
-- westerlies, polar easterlies). Map x is east, y north.
local function wind(W, s)
  local lat = W.grid.lat[s.cell] * 57.29578
  local alat = lat < 0 and -lat or lat
  if alat >= 30 and alat < 60 then return 1, 0 elseif alat >= 60 then return -1, 0 end
  return -1, (lat > 0 and -0.4 or 0.4)
end

-- One fire lived from f.t to `to` in steps of at most a quarter hour: heat grows on its fuel (faster
-- in the dry), rain damps it, it reaches the trees and buildings near it (downwind more), it burns a
-- building's contents and anyone still inside. Never ahead of the time asked: the people acting on it
-- (water thrown, a look) bring it to their own moment first.
local function live(W, s, f, to)
  local c = C()
  local V = W.S.v3
  if f.out or (f.t or 0) >= to then return end
  local dry = M.dryness(W, s)
  local rain = s.rain_now or 0
  local wx, wy = wind(W, s)
  while not f.out and f.t < to - 1e-9 do
    local dt = min(0.25, to - f.t)
    local t = f.t
    if f.heat > 0 and f.fuel > 0 then
      f.heat = min(1, f.heat + c.fire_grow * dt * (0.4 + dry)) - (rain == 2 and c.fire_rain2 or (rain == 1 and c.fire_rain1 or 0)) * dt
      f.fuel = f.fuel - dt * max(0.2, f.heat)
      -- (S22) roaring up: seen and heard farther - the animals about look up again
      if f.heat >= 0.6 and not f.roar then
        f.roar = true
        if M.alarm_beasts then M.alarm_beasts(W, s, f, t + dt) end
      end
      -- (S22) any beast, wild animal or dog within its flames is burnt
      if f.heat > 0 and M.scorch then M.scorch(W, s, f, t, dt) end
      if f.heat > 0 then
        local T = s.trees
        trees_near(s, f.x, f.y, c.fire_reach + (f.r or 0), function(k)
          if not find(s, "tree", k) then
            local dx, dy = T.x[k] - f.x, T.y[k] - f.y
            local down = 1 + c.fire_wind * max(0, dx * wx + dy * wy) / max(1e-6, sqrt(dx * dx + dy * dy))
            if u(W, s, t, 11, k) < c.fire_spread * f.heat * dry * down * dt then M.ignite(W, s, "tree", k, t + dt, f.deed) end
          end
        end)
        -- (S20) the fields: a fire within reach of a field's edge, a field burning to its neighbours
        local rp = c.fire_reach + (f.r or 0)
        for j = 1, #s.plots do
          local pl = s.plots[j]
          if pl.alive and not (f.kind == "plot" and f.ref == j) then
            local ex = max(pl.x0 - f.x, 0, f.x - pl.x1)
            local ey = max(pl.y0 - f.y, 0, f.y - pl.y1)
            if ex * ex + ey * ey < rp * rp and not find(s, "plot", j) and M.plot_fuel(pl) > 0
              and u(W, s, t, 13, j) < c.fire_spread * c.fire_plot_catch * f.heat * dry * dt then
              M.ignite(W, s, "plot", j, t + dt, f.deed)
            end
          end
        end
        local rb = c.fire_reach * 1.3 + (f.r or 0)
        for j = 1, #s.blist do
          local b = s.buildings[s.blist[j]]
          if b and b.state == "standing" and not b.natural and b.kind ~= "outcrop" and b.kind ~= "bog"
            and dsq((b.cx or b.dx) - f.x) + dsq((b.cy or b.dy) - f.y) < rb * rb
            and not find(s, "bld", b.id) and u(W, s, t, 12, b.id) < c.fire_spread * 0.7 * f.heat * dry * dt then
            M.ignite(W, s, "bld", b.id, t + dt, f.deed)
          end
        end
        if f.kind == "bld" then
          local b = s.buildings[f.ref]
          if b then
            for _, key in ipairs(BURNS) do
              local q = b.stock[key]
              if q and q > 0 then b.stock[key] = q * (1 - min(1, c.fire_contents * f.heat * dt)) end
            end
            -- (anyone still inside is burnt)
            for _, id in ipairs(s.members) do
              local q = V.people[id]
              if q and q.alive and q.inside == b.id and not q.at and q.act ~= "carried" then
                people.hurt(q, c.fire_burn * f.heat * dt, "burn", f.deed, t)
              end
            end
            -- (S20) the hay stacked by the fold goes up with it
            if b.kind == "fold" and (s.hay or 0) > 0 then s.hay = s.hay * (1 - min(1, c.fire_contents * f.heat * dt)) end
          end
        end
      end
    end
    f.t = t + dt
    if f.fuel <= 0 then f.out = "burnt" elseif f.heat <= 0 then f.out = "out" end
  end
end
M.live = live

-- Fires done with (burnt out: the tree a snag, the building a ruin; put out: what burnt of a building
-- to be mended) leave the list.
local function sweep(W, s)
  local c = C()
  local B = s.burn
  for i = #B, 1, -1 do
    local f = B[i]
    if f.out then
      table.remove(B, i)
      if f.out == "burnt" then
        burn_out(W, s, f, f.t)
      elseif f.kind == "bld" then
        local b = s.buildings[f.ref]
        if b then b.cond = max(0.05, (b.cond or 1) - (1 - max(0, f.fuel) / c.fire_bld_h)) end
        s.acc.fires_out = (s.acc.fires_out or 0) + 1
        -- (S21) put out: a deed of the village's (what burnt is mended; what lit it is its cause)
        if b then
          local d = deeds.add(W, { kind = "doused", t = f.t, at = s.id, x = f.x, y = f.y, by = 0, by_hh = 0, by_s = 0, to = 0,
            to_hh = b.hh or 0, to_s = s.id, what = b.kind, n = 1, fu = 0, harm = false, answers = f.deed, hours = f.t - (f.t0 or f.t) })
          deeds.learn(W, s, d, "we put the fire out", 0, 0, f.t)
        end
      elseif f.kind == "plot" then
        s.acc.fires_out = (s.acc.fires_out or 0) + 1
      end
    end
  end
end

-- Every fire on map s brought to `now` (and the finished ones swept away).
function M.catch_up(W, s, now)
  local B = s.burn
  if not B or #B == 0 then return end
  local n = #B
  for i = 1, n do if B[i] then live(W, s, B[i], now) end end
  sweep(W, s)
end

-- At every hour while anything burns (and whenever someone acts on a fire: M.near, M.douse).
function M.hourly(W, s, now)
  M.catch_up(W, s, now)
end

-- (S20, R6.5c) The planet's cells that map s covers (runtime).
local CELLS = setmetatable({}, { __mode = "k" })
local function cells_of(s)
  local L = CELLS[s]
  if L then return L end
  L = {}
  local seen = {}
  for i = 1, s.N * s.N do
    local cl = s.cell_of[i]
    if cl and cl > 0 and not seen[cl] then seen[cl] = true; L[#L + 1] = cl end
  end
  table.sort(L)
  CELLS[s] = L
  return L
end

-- (S20, R6.5c; S24, D-190) The planet's fire and the map's are one fire at two scales.
-- On a planet cell the map owns (mapm.owner: most of the cell is on the map) the planet's fire IS the map's: each hour
-- it is written from what burns on the map in that cell - its intensity the heat of the trees, roofs and fields alight
-- there against the trees the cell holds, so three burning trees are a glow and a burning wood is a fire - and a fire
-- the planet carries in from a cell about it comes onto the map as a front: the trees of that cell nearest the side it
-- comes from catch (fire_front_n of them), and the map's own fire does the rest (a wet map puts it out, and the planet's
-- with it). On a cell the map covers only in part the planet's fire is its own: where it burns, the map's tree of that
-- cell nearest the map's edge catches, once a day; and a fire on the map that has taken hold there (fire_planet_n
-- burning) sets the planet's fire going on the cell.
local TREES_IN = setmetatable({}, { __mode = "k" })
local function trees_in(s)
  local e = TREES_IN[s]
  if e and e.tv == s.tv then return e.n end
  local n = {}
  local T = s.trees
  for k = 1, T.n do
    if T.st[k] == 1 then
      local cl = s.cell_of[T.tile[k]]
      if cl then n[cl] = (n[cl] or 0) + 1 end
    end
  end
  TREES_IN[s] = { tv = s.tv, n = n }
  return n
end

-- the heat of what burns on map s, by planet cell (a roof counts as three trees, a field as four), and the event
-- behind the first thing burning in each (the planet's blame for it)
local function map_heat(W, s)
  local out, tag = {}, {}
  local B = s.burn
  for i = 1, #(B or {}) do
    local f = B[i]
    if not f.out and f.heat > 0.02 then
      local t = mapm.tile_of(s, f.x, f.y)
      local cl = t > 0 and s.cell_of[t]
      if cl then
        out[cl] = (out[cl] or 0) + f.heat * (f.kind == "bld" and 3 or (f.kind == "plot" and 4 or 1))
        if not tag[cl] then
          local d = f.deed and deeds.get(W, f.deed)
          tag[cl] = d and d.event or 0
        end
      end
    end
  end
  return out, tag
end

-- the planet's fire coming into owned cell cl of map s: the trees of cl nearest the burning cells about it catch
local function enter(W, s, cl, now, O)
  local c = C()
  local g = W.grid
  local sx, sy, n = 0, 0, 0
  local b6 = cl * 6 - 6
  for k = 1, g.nbr_count[cl] do
    local j = g.nbr[b6 + k]
    if (W.F.fire[j] or 0) > 0 and O.by[j] ~= s.id then
      local x, y = mapm.local_of(s, g.X[j], g.Y[j], g.Z[j])
      if x then sx, sy, n = sx + x, sy + y, n + 1 end
    end
  end
  local T = s.trees
  local cand = {}
  if n > 0 then
    sx, sy = sx / n, sy / n
    for k = 1, T.n do
      if T.st[k] == 1 and s.cell_of[T.tile[k]] == cl then cand[#cand + 1] = { k, dsq(T.x[k] - sx) + dsq(T.y[k] - sy) } end
    end
  else
    -- (no burning cell about it - the planet's own fire began here: the trees nearest the map's edge, as before)
    for k = 1, T.n do
      if T.st[k] == 1 and s.cell_of[T.tile[k]] == cl then
        cand[#cand + 1] = { k, -max(math.abs(T.x[k]), math.abs(T.y[k])) }
      end
    end
  end
  table.sort(cand, function(a, b) if a[2] ~= b[2] then return a[2] < b[2] end return a[1] < b[1] end)
  local lit = 0
  for q = 1, min(#cand, c.fire_front_n) do if M.ignite(W, s, "tree", cand[q][1], now, nil) then lit = lit + 1 end end
  return lit
end

function M.planet(W, s, now)
  local c = C()
  local F = W.F
  local day = floor(now / 24)
  local L = cells_of(s)
  local O = mapm.owners(W)
  local fsys = require("sim.systems.fire")
  local heat, tag
  if s.burn and #s.burn > 0 then heat, tag = map_heat(W, s) end
  s.fire_own = s.fire_own or {}
  local own = s.fire_own
  for _, cl in ipairs(L) do
    local pf = F.fire[cl] or 0
    if O and O.by[cl] == s.id then
      local h = heat and heat[cl] or 0
      local wrote = own[cl] or 0
      if pf > 0 and h <= 0 and math.abs(pf - wrote) > 1e-9 and (not s.fire_in or s.fire_in[cl] ~= day) then
        -- (carried in by the planet from a cell about it: a front at its side)
        s.fire_in = s.fire_in or {}
        s.fire_in[cl] = day
        if enter(W, s, cl, now, O) > 0 then heat, tag = map_heat(W, s); h = heat[cl] or 0 end
      end
      local v = 0
      if h > 0 then
        v = min(1, h / max(10, (trees_in(s)[cl] or 0) * 0.25))
        if v < 0.004 then v = 0.004 end
      end
      if v ~= pf then fsys.from_map(W, cl, v, tag and tag[cl] or 0) end
      own[cl] = v > 0 and v or nil
    else
      if pf > c.fire_planet_in and (not s.fire_in or s.fire_in[cl] ~= day) then
        s.fire_in = s.fire_in or {}
        s.fire_in[cl] = day
        local N, T = s.N, s.trees
        local best, bd = nil, -1
        for i = 1, N * N do
          local k = s.tree[i]
          if k and k > 0 and T.st[k] == 1 and s.cell_of[i] == cl then
            local ix, iy = (i - 1) % N + 1, floor((i - 1) / N) + 1
            local edge = max(math.abs(ix - N / 2), math.abs(iy - N / 2))
            if edge > bd then best, bd = k, edge end
          end
        end
        if best then M.ignite(W, s, "tree", best, now, nil) end
      end
    end
  end
  local B = s.burn
  if B and #B >= c.fire_planet_n then
    -- (the cell under most of what burns, when the map does not own it)
    local count, top, tn = {}, nil, 0
    for _, f in ipairs(B) do
      local i = mapm.tile_of(s, f.x, f.y)
      local cl = i > 0 and s.cell_of[i] or nil
      if cl and not (O and O.by[cl] == s.id) then
        count[cl] = (count[cl] or 0) + 1
        if count[cl] > tn or (count[cl] == tn and cl < top) then top, tn = cl, count[cl] end
      end
    end
    if top and tn >= c.fire_planet_n and (F.fire[top] or 0) <= 0 then
      fsys.ignite(W, top, 0)
    end
  end
end

-- (S24, D-189) A sudden blow at (x, y) - a bolt, a blast: every beast, wild animal and dog within r sees and hears it
-- at once and bolts (each its own way and time: stock.jolt, wild.jolt, dogs.jolt).
function M.jolt(W, s, x, y, now, r)
  stock.jolt(W, s, x, y, now, r)
  wild.jolt(W, s, x, y, now, r)
  dogs.jolt(W, s, x, y, now, r)
end

-- (S22) Each beast of map s that stands out of doors - a fold is open to the sky, a byre or a sty has
-- a roof (the roof is the target) - each wild animal and each dog: fn(kind, o, H, x, y), kind "beast"
-- (o of herd H), "wild" or "dog".
local function beasts_out(W, s, now, fn)
  if s.herds then
    for _, kind in ipairs(stock.HERDS) do
      local H = s.herds[kind]
      if H then
        local pen = s.buildings[H.pen or 0]
        local roofed = pen and pen.state == "standing" and (pen.kind == "byre" or pen.kind == "sty")
        local L = stock.live_list(H)
        for i = 1, #L do
          local a = H.animals[L[i]]
          if a.alive then
            local ax, ay = stock.pos(a, now)
            if not (roofed and stock.inside_pen(s, pen, ax, ay)) then fn("beast", a, H, ax, ay) end
          end
        end
      end
    end
  end
  for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
    if w.alive and w.act ~= "leave" then
      local wx, wy = wild.pos(w, now)
      fn("wild", w, nil, wx, wy)
    end
  end
  for _, dg in ipairs(s.dogs or {}) do
    if dg.alive then
      local gx, gy = dogs.pos(dg, now)
      fn("dog", dg, nil, gx, gy)
    end
  end
end
M.beasts_out = beasts_out

-- (S22) A beast struck (by the bolt itself, or by the current through the ground beside it): `sev` of
-- its strength - a beast of a herd dead at nothing (else stunned: it looks up at once), a wild animal
-- or a dog dead past its strength.
local function beast_struck(W, s, kind, o, H, sev, now, side)
  if kind == "beast" then
    o.health = (o.health or 1) - sev
    if o.health <= 0 then
      stock.dies(W, s, H, o, now, "lightning", side and "Lightning struck beside one of our %s and killed it" or "Lightning killed one of our %s")
      return true
    end
    stock.decide_at(W, s, H, o, now, now + 0.01)
  elseif kind == "wild" then
    o.hp = (o.hp or 1) - sev
    if o.hp <= 0 then wild.dies(W, s, o, now, "lightning"); return true end
    wild.interrupt(W, s, o, now)
    o.flee_x, o.flee_y = o.tx, o.ty
  else
    o.hp = (o.hp or 1) - sev
    if o.hp <= 0 then dogs.dies(W, s, o, now, "lightning", "Lightning killed our dog"); return true end
    dogs.interrupt(W, s, o, now)
  end
  return false
end

-- Lightning at (x, y): the tallest thing within its pull takes it - a standing tree (its height), a
-- roof (by the building: a fold of hurdles is low), someone standing out in the open, a beast out of
-- doors (S22: by its height - a cow before a sheep, a man before either); thunder heard over the whole
-- map, the flash seen. (S23) What a bolt at (x, y) takes: the tallest thing within strike_pull (reads only;
-- the strike and the aim preview share it). Returns best (a tree index, building id, person id or beast), its
-- kind ("tree", "bld", "person", "beast", "wild", "dog" or nil for the bare ground) and a beast's herd.
-- (S24, D-189) `god`: a bolt the god aims comes down where it is aimed - the thing at the point takes it (its pull
-- is a hand's breadth, strike_pull_god, the nearest by far the likeliest); a storm's bolt finds the tallest.
function M.strike_target(W, s, x, y, now, god)
  local c = C()
  local best, bs, bk, bh = nil, -1e9, nil, nil
  local pull = god and c.strike_pull_god or c.strike_pull
  local dk = god and 40 or 4
  local r2 = pull * pull
  local T = s.trees
  trees_near(s, x, y, pull, function(k, d2)
    local sc = (T.size[k] or 1) * c.strike_tree - sqrt(d2) * dk
    if sc > bs then best, bs, bk = k, sc, "tree" end
  end)
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    if b and b.state == "standing" and not b.natural and b.kind ~= "outcrop" and b.kind ~= "bog" then
      local d2 = dsq((b.cx or b.dx) - x) + dsq((b.cy or b.dy) - y)
      -- (S24: a god's bolt on a building: the point within its walls is on its roof)
      if god then d2 = dsq(max(0, math.abs((b.cx or b.dx) - x) - (b.w or 1) * s.T * 0.5)) + dsq(max(0, math.abs((b.cy or b.dy) - y) - (b.h or 1) * s.T * 0.5)) end
      if d2 < r2 then
        local sc = (c.strike_bld[b.kind] or c.strike_roof) - sqrt(d2) * dk
        if sc > bs then best, bs, bk = b.id, sc, "bld" end
      end
    end
  end
  danger.each_on_map(W, s, function(q)
    if q.inside == 0 and q.act ~= "carried" then
      local qx, qy = danger.person_pos(q, now)
      local d2 = dsq(qx - x) + dsq(qy - y)
      if d2 < r2 then
        local sc = c.strike_person - sqrt(d2) * dk
        if sc > bs then best, bs, bk = q.id, sc, "person" end
      end
    end
  end)
  beasts_out(W, s, now, function(kind, o, H, ox, oy)
    local d2 = dsq(ox - x) + dsq(oy - y)
    if d2 < r2 then
      local sc = (c.strike_beast[o.sp or "dog"] or 0.25) - sqrt(d2) * dk
      if sc > bs then best, bs, bk, bh = o, sc, kind, H end
    end
  end)
  return best, bk, bh
end

-- (S24, D-189) The current a bolt drives into the ground spreads from where it went in: a body standing on that
-- ground with its feet apart takes the step between them - the farther apart its feet (a cow's) the more - falling
-- off with the square of the distance. The lethal reach of a bolt is strike_gc x its current (1 a storm's,
-- strike_god_i the god's) x the body's stride (strike_stride). Returns the harm at distance r (a share of a body's
-- strength: at 1 a beast is dead), with the body's own luck.
local function ground_harm(W, s, now, r, stride, cur, key)
  local c = C()
  local R = c.strike_gc * cur * stride
  local q = R / max(r, 0.005)
  return min(3, q * q) * (0.6 + 0.8 * u(W, s, now, 26, key))
end

-- (S24) A scorch where a bolt went into the ground (the grass about it burnt off): kept a while, drawn black
local function scorch_mark(s, x, y, r, now)
  s.scorch = s.scorch or {}
  local L = s.scorch
  -- (only the marks still showing are kept)
  for i = #L, 1, -1 do if now - L[i].t > 24 * 40 then table.remove(L, i) end end
  L[#L + 1] = { x = x, y = y, r = r, t = now }
end
M.scorch_mark = scorch_mark

function M.strike(W, s, x, y, now, cause, ev)
  local c = C()
  local V = W.S.v3
  -- (S24) the god's bolt: aimed, and a superbolt's current
  local god = ev ~= nil and ev ~= 0
  local cur = god and c.strike_god_i or 1
  local best, bk, bh = M.strike_target(W, s, x, y, now, god)
  local T = s.trees
  local what = bk or "ground"
  if bk == "beast" or bk == "wild" then what = best.sp end
  local d = deeds.add(W, { kind = "strike", t = now, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
    to_s = s.id, what = what, n = 1, fu = 0, harm = false, answers = cause, event = ev,
    bkind = bk == "bld" and (s.buildings[best] or {}).kind or nil })
  -- (the bolt seen on the planet: the view's lightning at the village's cell; S21: a god's bolt is the
  -- parent of the event)
  if W.record_events then
    local e = require("sim.causality.events").emit(W, "lightning_strike", s.cell, ev or 0, 1, { settlement = s.id, x = x, y = y })
    if not ev then d.event = e end
  end
  s.acc.strikes = (s.acc.strikes or 0) + 1
  -- (thunder everyone hears; the flash those out of doors see)
  danger.cue(W, s, x, y, now, { kind = "thunder", src = 0, sight = 3, night = 3, sound = c.thunder_sound, loud = true,
    why = "Lightning struck close by" })
  local dry = M.dryness(W, s)
  -- where the bolt went to ground
  local hx, hy = x, y
  if bk == "tree" then hx, hy = T.x[best], T.y[best]
  elseif bk == "person" then hx, hy = danger.person_pos(V.people[best], now)
  elseif bk == "beast" then hx, hy = stock.pos(best, now)
  elseif bk == "wild" then hx, hy = wild.pos(best, now)
  elseif bk == "dog" then hx, hy = dogs.pos(best, now)
  elseif bk == "bld" then local b = s.buildings[best]; hx, hy = b.cx or b.dx, b.cy or b.dy end
  d.hx, d.hy = hx, hy
  -- (S24) the current through the ground: beasts about the point thrown down or killed by their stride and distance
  -- (those sheltering under a struck tree too); a roof that took it carries it down its walls (nothing beside)
  local killed, thrown = 0, 0
  if bk ~= "bld" then
    local reach = c.strike_gc * cur * 1.6 * 2.5
    local side = {}
    beasts_out(W, s, now, function(kind, o, H, ox, oy)
      if o ~= best then
        local r = sqrt(dsq(ox - hx) + dsq(oy - hy))
        if r < reach then side[#side + 1] = { kind, o, H, r } end
      end
    end)
    for i = 1, #side do
      local e = side[i]
      local stride = c.strike_stride[e[2].sp or "dog"] or 0.8
      local h = ground_harm(W, s, now, e[4], stride, cur, 100 + i)
      if h > 0.03 then
        if beast_struck(W, s, e[1], e[2], e[3], h, now, true) then killed = killed + 1 else thrown = thrown + 1 end
      end
    end
  end
  -- (S24) the scorch where it went in; every beast near looks up at once and bolts
  scorch_mark(s, hx, hy, god and c.strike_scorch_god or c.strike_scorch, now)
  M.jolt(W, s, hx, hy, now, god and c.strike_jolt_god or c.strike_jolt)
  d.killed, d.thrown = killed > 0 and killed or nil, thrown > 0 and thrown or nil
  local ign = god and c.strike_god_ign or 1
  if bk == "beast" or bk == "wild" or bk == "dog" then
    -- (S22) a beast struck: most die where they stand; its owners lose it (a deed of the strike)
    local sev = (c.strike_hurt[1] + (c.strike_hurt[2] - c.strike_hurt[1]) * u(W, s, now, 23)) * c.strike_beast_k * (god and 2 or 1)
    local hh = (bk == "beast" and best.owner) or (bk == "dog" and best.hh) or 0
    local dead = beast_struck(W, s, bk, best, bh, sev, now, false)
    d.dead = dead or nil
    if hh ~= 0 then
      d.to_hh, d.harm = hh, true
      d.fu = bk == "beast" and (c.stock[best.sp] and c.stock[best.sp].meat or 5) or 4
      deeds.learn(W, s, d, "the lightning struck it", 0, 0, now)
    end
  elseif bk == "tree" then
    if u(W, s, now, 21) < min(0.95, c.strike_ignite * (0.25 + 0.75 * dry) * ign) then M.ignite(W, s, "tree", best, now, d.id) end
  elseif bk == "bld" then
    if u(W, s, now, 22) < min(0.95, c.strike_ignite_roof * (0.3 + 0.7 * dry) * ign) then M.ignite(W, s, "bld", best, now, d.id) end
  elseif bk == "person" then
    local q = V.people[best]
    local sev = (c.strike_hurt[1] + (c.strike_hurt[2] - c.strike_hurt[1]) * u(W, s, now, 23)) * (god and c.strike_god_hurt or 1)
    people.hurt(q, min(1, sev), "burn", d.id, now)
    d.to, d.to_hh = q.id, q.hh
    people.remember(q, now, "Struck by lightning")
  end
  -- the ground's current on people about the point (not the one struck: above; under a struck roof, none)
  local hurt_list = {}
  if bk ~= "bld" then
    danger.each_on_map(W, s, function(o)
      if o.inside == 0 and o.act ~= "carried" and not (bk == "person" and o.id == best) then
        local ox, oy = danger.person_pos(o, now)
        local r = sqrt(dsq(ox - hx) + dsq(oy - hy))
        local h = ground_harm(W, s, now, r, c.strike_stride.person, cur, 500 + o.id % 997)
        if h > 0.04 then hurt_list[#hurt_list + 1] = { o, min(1, h) } end
      end
    end)
  end
  for _, e in ipairs(hurt_list) do
    local o = e[1]
    people.hurt(o, e[2], "burn", d.id, now)
    people.remember(o, now, bk == "person" and sfmt("Lightning struck %s beside me", V.people[best].name) or "Lightning struck the ground beside me")
  end
  -- (a field it went into that has something dry on it catches)
  if bk == nil or bk == "beast" or bk == "person" then
    local i = mapm.tile_of(s, hx, hy)
    local pk = i > 0 and s.plot[i] or 0
    local pl = pk > 0 and s.plots[pk]
    if pl and M.plot_fuel(pl) > 0 and u(W, s, now, 27) < min(0.95, c.strike_ignite * c.fire_plot_catch * (0.25 + 0.75 * dry) * ign) then
      M.ignite(W, s, "plot", pk, now, d.id)
    end
  end
  for _, e in ipairs(hurt_list) do
    if (e[1].hurt or 0) >= 1 and M.kill then M.kill(W, s, e[1], "lightning", now) end
  end
  if bk == "person" then
    local q = V.people[best]
    if (q.hurt or 0) >= 1 and M.kill then M.kill(W, s, q, "lightning", now) end
  end
  return bk, best, d
end

-- A storm's lightning over map s this hour (a downpour: the planet's thunderstorm), at a point on its
-- land; most strike the woods and fields.
function M.natural(W, s, now)
  local c = C()
  if (s.rain_now or 0) < 2 then return end
  if u(W, s, now, 31) >= c.strike_rate then return end
  local N, T = s.N, s.T
  for k = 1, 6 do
    local ix = 2 + floor(u(W, s, now, 32, k) * (N - 2))
    local iy = 2 + floor(u(W, s, now, 33, k) * (N - 2))
    local i = (iy - 1) * N + ix
    if s.land[i] == 1 then
      local x = (ix - 0.5 - N * 0.5) * T
      local y = (iy - 0.5 - N * 0.5) * T
      local at = now + u(W, s, now, 34, k)
      M.strike(W, s, x, y, at, nil)
      return
    end
  end
end

return M
