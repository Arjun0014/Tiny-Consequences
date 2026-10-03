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
function M.ignite(W, s, kind, ref, now, cause)
  local c = C()
  if find(s, kind, ref) then return nil end
  local x, y, fuel
  if kind == "tree" then
    local T = s.trees
    if (T.st[ref] or 0) ~= 1 then return nil end
    x, y, fuel = T.x[ref], T.y[ref], c.fire_tree_h * (0.5 + 0.5 * (T.size[ref] or 1))
  else
    local b = s.buildings[ref]
    if not b or b.state ~= "standing" or b.natural then return nil end
    x, y, fuel = b.cx or b.dx, b.cy or b.dy, c.fire_bld_h * (b.kind == "house" and 1 or 0.7)
  end
  s.burn = s.burn or {}
  local f = { kind = kind, ref = ref, x = x, y = y, heat = 0.25, fuel = fuel, t0 = now, t = now, deed = cause }
  s.burn[#s.burn + 1] = f
  s.acc.fires = (s.acc.fires or 0) + 1
  M.cue(W, s, f, now, kind == "bld" and "Fire! The roof is burning" or "Fire in the trees")
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
    s.tv = (s.tv or 0) + 1
    -- (the planet's woods are thinner by it: as a felled tree, D-078)
    local cell = s.cell
    if W.F.forest[cell] then W.F.forest[cell] = max(0, W.F.forest[cell] - c.fire_tree_forest) end
    s.acc.trees_burnt = (s.acc.trees_burnt or 0) + 1
    return
  end
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
    if M.lodge then M.lodge(W, s, hh, now) end
  end
end

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
      if f.heat > 0 then
        local T = s.trees
        trees_near(s, f.x, f.y, c.fire_reach, function(k)
          if not find(s, "tree", k) then
            local dx, dy = T.x[k] - f.x, T.y[k] - f.y
            local down = 1 + c.fire_wind * max(0, dx * wx + dy * wy) / max(1e-6, sqrt(dx * dx + dy * dy))
            if u(W, s, t, 11, k) < c.fire_spread * f.heat * dry * down * dt then M.ignite(W, s, "tree", k, t + dt, f.deed) end
          end
        end)
        local rb = c.fire_reach * 1.3
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

-- (S20, R6.5c) The planet's fire on a cell the map covers comes onto the map: the standing tree of
-- that cell nearest the map's edge (where it comes from) catches, once a day a cell; the local fire
-- does the rest. And a fire on the map that has taken hold (fire_planet_n burning) sets the planet's
-- fire going on its cell, whose own spreading carries it on beyond the map.
function M.planet(W, s, now)
  local c = C()
  local F = W.F
  local day = floor(now / 24)
  local L = cells_of(s)
  for _, cl in ipairs(L) do
    if (F.fire[cl] or 0) > c.fire_planet_in and (not s.fire_in or s.fire_in[cl] ~= day) then
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
  local B = s.burn
  if B and #B >= c.fire_planet_n then
    -- (the cell under most of what burns)
    local count, top, tn = {}, nil, 0
    for _, f in ipairs(B) do
      local i = mapm.tile_of(s, f.x, f.y)
      local cl = i > 0 and s.cell_of[i] or nil
      if cl then
        count[cl] = (count[cl] or 0) + 1
        if count[cl] > tn or (count[cl] == tn and cl < top) then top, tn = cl, count[cl] end
      end
    end
    if top and tn >= c.fire_planet_n and (F.fire[top] or 0) <= 0 then
      require("sim.systems.fire").ignite(W, top, 0)
    end
  end
end

-- Lightning at (x, y): the tallest thing within its pull takes it - a standing tree (its height), a
-- roof, someone standing out in the open; thunder heard over the whole map, the flash seen. Returns
-- what was hit ("tree", "bld", "person" or nil) and its ref. `cause`: the deed or event that sent it.
function M.strike(W, s, x, y, now, cause)
  local c = C()
  local V = W.S.v3
  local best, bs, bk = nil, -1e9, nil
  local r2 = c.strike_pull * c.strike_pull
  local T = s.trees
  trees_near(s, x, y, c.strike_pull, function(k, d2)
    local sc = (T.size[k] or 1) * c.strike_tree - sqrt(d2) * 4
    if sc > bs then best, bs, bk = k, sc, "tree" end
  end)
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    if b and b.state == "standing" and not b.natural and b.kind ~= "outcrop" and b.kind ~= "bog" then
      local d2 = dsq((b.cx or b.dx) - x) + dsq((b.cy or b.dy) - y)
      if d2 < r2 then
        local sc = c.strike_roof - sqrt(d2) * 4
        if sc > bs then best, bs, bk = b.id, sc, "bld" end
      end
    end
  end
  danger.each_on_map(W, s, function(q)
    if q.inside == 0 and q.act ~= "carried" then
      local qx, qy = danger.person_pos(q, now)
      local d2 = dsq(qx - x) + dsq(qy - y)
      if d2 < r2 then
        local sc = c.strike_person - sqrt(d2) * 4
        if sc > bs then best, bs, bk = q.id, sc, "person" end
      end
    end
  end)
  local d = deeds.add(W, { kind = "strike", t = now, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
    to_s = s.id, what = bk or "ground", n = 1, fu = 0, harm = false, answers = cause })
  -- (the bolt seen on the planet: the view's lightning at the village's cell)
  if W.record_events then require("sim.causality.events").emit(W, "lightning_strike", s.cell, 0, 1, { settlement = s.id, x = x, y = y }) end
  s.acc.strikes = (s.acc.strikes or 0) + 1
  -- (thunder everyone hears; the flash those out of doors see)
  danger.cue(W, s, x, y, now, { kind = "thunder", src = 0, sight = 3, night = 3, sound = c.thunder_sound, loud = true,
    why = "Lightning struck close by" })
  local dry = M.dryness(W, s)
  if bk == "tree" then
    if u(W, s, now, 21) < c.strike_ignite * (0.25 + 0.75 * dry) then M.ignite(W, s, "tree", best, now, d.id) end
    return "tree", best, d
  elseif bk == "bld" then
    if u(W, s, now, 22) < c.strike_ignite_roof * (0.3 + 0.7 * dry) then M.ignite(W, s, "bld", best, now, d.id) end
    return "bld", best, d
  elseif bk == "person" then
    local q = V.people[best]
    local sev = c.strike_hurt[1] + (c.strike_hurt[2] - c.strike_hurt[1]) * u(W, s, now, 23)
    people.hurt(q, min(1, sev), "burn", d.id, now)
    d.to, d.to_hh = q.id, q.hh
    people.remember(q, now, "Struck by lightning")
    -- (those standing close by are thrown down and burnt a little)
    danger.each_on_map(W, s, function(o)
      if o ~= q and o.inside == 0 and o.act ~= "carried" then
        local ox, oy = danger.person_pos(o, now)
        local qx, qy = danger.person_pos(q, now)
        if dsq(ox - qx) + dsq(oy - qy) < c.strike_side * c.strike_side then
          people.hurt(o, c.strike_side_hurt * (0.5 + u(W, s, now, 24, o.id)), "burn", d.id, now)
          people.remember(o, now, sfmt("Lightning struck %s beside me", q.name))
        end
      end
    end)
    if (q.hurt or 0) >= 1 and M.kill then M.kill(W, s, q, "lightning", now) end
    return "person", best, d
  end
  return nil, nil, d
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
