-- Tsunami (R6.5f, D-162, docs/v3/65_R6_5_LIVING_PLANET.md §11): a strong quake under the sea (or a
-- great eruption of an ocean volcano) raises a wave; each coastal village within reach gets a run-up by
-- distance, after the time the wave takes to cross. The map has no heights: the low ground is the
-- ground near the sea (each land tile's distance to the sea, as the floodplain is the ground near the
-- river). Before it comes the sea draws back from the shore - seen by those who can see the water;
-- they run inland (think). What lies within the run-up is struck: buildings fall in (those inside hurt
-- or trapped), people still there are swept (hurt; the worst drowned), moored boats and beasts lost,
-- fields salted. The rest is the people's own (lodging, rebuilding, boats laid down anew).
--   s.wave = { t (arrival), runup (world units), ev, warned }
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

-- Each tile's distance to the sea in tiles (0 sea, -1 no sea on the map), worked out once (runtime).
local DIST = setmetatable({}, { __mode = "k" })
function M.sea_dist(s)
  local D = DIST[s]
  if D then return D end
  D = {}
  local N = s.N
  local q, qh = {}, 1
  for i = 1, N * N do
    if s.land[i] == 0 then D[i] = 0; q[#q + 1] = i else D[i] = -1 end
  end
  while qh <= #q do
    local i = q[qh]
    qh = qh + 1
    local ix, iy = (i - 1) % N, floor((i - 1) / N)
    local d1 = D[i] + 1
    if ix > 0 and D[i - 1] == -1 then D[i - 1] = d1; q[#q + 1] = i - 1 end
    if ix < N - 1 and D[i + 1] == -1 then D[i + 1] = d1; q[#q + 1] = i + 1 end
    if iy > 0 and D[i - N] == -1 then D[i - N] = d1; q[#q + 1] = i - N end
    if iy < N - 1 and D[i + N] == -1 then D[i + N] = d1; q[#q + 1] = i + N end
  end
  DIST[s] = D
  return D
end

-- The sea distance (tiles) at point (x, y) of map s (-1: no sea, or off the map).
function M.dist_at(s, x, y)
  local mapm = require("sim.agents.map")
  local i = mapm.tile_of(s, x, y)
  if i == 0 then return -1 end
  return M.sea_dist(s)[i]
end

-- The nearest place on map s from (x, y) at least `safe` tiles from the sea that can be walked to
-- (within 40 tiles), or nil.
function M.inland(s, x, y, safe)
  local mapm = require("sim.agents.map")
  local D = M.sea_dist(s)
  local N = s.N
  local i0 = mapm.tile_of(s, x, y)
  if i0 == 0 then return nil end
  local ix0, iy0 = (i0 - 1) % N, floor((i0 - 1) / N)
  local best, bd = nil, 1e9
  for r = 1, 40 do
    for dy = -r, r do
      for dx = -r, r do
        if (dx == -r or dx == r or dy == -r or dy == r) then
          local ix, iy = ix0 + dx, iy0 + dy
          if ix >= 0 and iy >= 0 and ix < N and iy < N then
            local i = iy * N + ix + 1
            if D[i] >= safe and s.cost[i] > 0 then
              local d = dx * dx + dy * dy
              if d < bd then best, bd = i, d end
            end
          end
        end
      end
    end
    if best then break end
  end
  if not best then return nil end
  return mapm.tile_xy(s, best)
end

-- A quake of magnitude mag at cell `at` (under the sea) at time t: the wave set going.
function M.from_quake(W, at, mag, t, ev)
  local c = C()
  if W.geo.ocean[at] ~= 1 or mag < c.tsu_mag then return end
  M.raise(W, at, mag - c.tsu_mag + 1, t, ev)
end

-- A wave of strength k from cell `at` at time t: each coastal village within reach gets its run-up
-- and the hour it arrives.
function M.raise(W, at, k, t, ev)
  local c = C()
  local g = W.grid
  for _, s in ipairs(W.S.v3.settlements) do
    if s.alive and s.land then
      local dot = g.X[at] * g.X[s.cell] + g.Y[at] * g.Y[s.cell] + g.Z[at] * g.Z[s.cell]
      local d = sqrt(max(0, 2 - 2 * min(1, dot))) / g.spacing
      local runup = c.tsu_k * k / (1 + d * d)
      if runup >= c.tsu_min then
        local D = M.sea_dist(s)
        local coastal = false
        for i = 1, s.N * s.N do if D[i] == 0 then coastal = true; break end end
        if coastal then
          s.wave = { t = t + c.tsu_delay + d / c.tsu_speed, runup = runup, ev = ev, warned = false }
        end
      end
    end
  end
end

-- Hourly for each village: the sea drawing back (seen by those who can see the water) in the hour
-- before the wave; the wave.
function M.hourly(W, s, now)
  local wv = s.wave
  if not wv then return end
  local c = C()
  if not wv.warned and now >= wv.t - 1 then
    wv.warned = true
    local D = M.sea_dist(s)
    local mapm = require("sim.agents.map")
    local tiles = c.tsu_see / s.T
    local night = danger.night_at(W, s, now)
    danger.each_on_map(W, s, function(p)
      if p.act ~= "carried" and p.act ~= "sleep" and p.inside == 0 then
        local px, py = danger.person_pos(p, now)
        local i = mapm.tile_of(s, px, py)
        local dd = i > 0 and D[i] or -1
        if dd >= 0 and dd <= tiles * (night and 0.3 or 1) then
          danger.raise(W, p, { kind = "wave", src = 0, why = "The sea is drawing back from the shore" }, px, py, now, "saw")
          if (p.wave_seen or -9) < floor(now / 24) then
            p.wave_seen = floor(now / 24)
            people.remember(p, now, "Saw the sea draw back from the shore")
          end
        end
      end
    end)
    -- (those who saw it shout it: a sound that carries, waking those near)
    local n = 0
    danger.each_on_map(W, s, function(p)
      if n < 6 and p.alarm and p.alarm.kind == "wave" and p.alarm.t == now and p.alarm.how == "saw" then
        n = n + 1
        local px, py = danger.person_pos(p, now)
        danger.cue(W, s, px, py, now, { kind = "wave", src = 0, sight = 0, night = 0, sound = c.tsu_shout, loud = true, skip = p.id,
          why = sfmt("%s is shouting: the sea is going out", p.name) })
      end
    end)
    s.acc.waves_seen = (s.acc.waves_seen or 0) + 1
  end
  if now >= wv.t then
    s.wave = nil
    M.strike(W, s, wv.runup, now, wv.ev)
  end
end

local GONE = { dock = true, saltpan = true, rack = true, house = true, hut = true, granary = true, woodstore = true, fold = true,
  byre = true, sty = true, forge = true, well = true, tanpit = true, clamp = true, camp = true }

-- The wave over village s's map: everything within `runup` of the sea struck.
function M.strike(W, s, runup, now, ev)
  local c = C()
  local V = W.S.v3
  local D = M.sea_dist(s)
  local mapm = require("sim.agents.map")
  local shake = require("sim.agents.shake")
  local R = runup / s.T
  local d0 = deeds.add(W, { kind = "wave", t = now, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0, to_s = s.id,
    what = sfmt("%.2f", runup), n = 1, fu = 0, harm = false, event = ev })
  deeds.learn(W, s, d0, "the sea came over the land", 0, 0, now)
  s.acc.waves = (s.acc.waves or 0) + 1
  local function within(x, y)
    local i = mapm.tile_of(s, x, y)
    local dd = i > 0 and D[i] or -1
    return dd >= 0 and dd <= R, dd
  end
  -- the buildings
  local fell = {}
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    if b and b.state == "standing" and not b.natural and GONE[b.kind] and within(b.cx or b.dx, b.cy or b.dy) then fell[#fell + 1] = b end
  end
  for _, b in ipairs(fell) do shake.fall_in(W, s, b, now, d0, "sea") end
  if #fell > 0 then s.acc.fell_sea = (s.acc.fell_sea or 0) + #fell end
  -- the boats moored
  for _, boat in ipairs(s.boats or {}) do
    if not boat.lost and (boat.crew or 0) == 0 and not boat.away and within(boat.x, boat.y) then
      boat.lost, boat.owner, boat.cargo = true, -1, nil
      s.acc.boats_sea = (s.acc.boats_sea or 0) + 1
    end
  end
  -- the people out in it (those in a building that fell are hurt by the fall)
  danger.each_on_map(W, s, function(p)
    if p.act == "carried" or (p.riding or 0) ~= 0 or p.trapped then return end
    local px, py = danger.person_pos(p, now)
    local inz, dd = within(px, py)
    if inz and p.inside == 0 then
      local deep = 1 - dd / max(1, R)
      local sev = c.tsu_hurt * deep * (0.5 + rng.float(W.seed, rng.S.V3_CONFLICT, p.key, floor(now * 60) % 4294967296, 7500))
      if sev > 0.05 then
        people.hurt(p, min(1, sev), "drowned", d0.id, now)
        people.remember(p, now, "The sea came over the land and swept me off my feet")
        s.acc.swept = (s.acc.swept or 0) + 1
        if (p.hurt or 0) >= 1 and shake.kill then shake.kill(W, s, p, "drowned", now) end
      end
    end
  end)
  -- the beasts in it
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and within(a.tx, a.ty) then
        a.alive, a.died, a.cause = false, now, "sea"
        s.acc.stock_died = (s.acc.stock_died or 0) + 1
        s.acc.stock_sea = (s.acc.stock_sea or 0) + 1
      end
    end
  end
  -- the fields: salted, what grows on them spoilt
  for _, pl in ipairs(s.plots) do
    if pl.alive and within((pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5) then
      pl.fert = max(0.3, (pl.fert or 1) - c.tsu_salt)
      if pl.stage == "growing" or pl.stage == "ripe" then pl.frost = (pl.frost or 1) * 0.3 end
      pl.sheaves = 0
      s.acc.fields_salted = (s.acc.fields_salted or 0) + 1
    end
  end
  -- everyone on the map knows the sea came
  danger.each_on_map(W, s, function(p)
    if p.act ~= "carried" and people.stage(p, now) ~= "infant" and (p.wave_day or -9) ~= floor(now / 24) then
      p.wave_day = floor(now / 24)
      people.remember(p, now, "The sea came over the land")
    end
  end)
end

return M
