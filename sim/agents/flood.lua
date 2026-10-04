-- Flood water on the maps (R6.5e, S21, D-169; docs/v3/65_R6_5_LIVING_PLANET.md §12). A river whose
-- flow passes the planet's own flood line (flood_mult x its bankfull flow, the mean annual flood:
-- sim/systems/hydrology.lua) rises over its banks. The water stands at a stage above the bank that
-- grows with the excess (Manning-like, ^0.6), rises over hours after the day's water reaches the
-- river and falls back over a day or two.
-- The map has no heights of its own: each tile's height above its nearest river (HAND - "height above
-- nearest drainage", as inundation maps are made) is its distance beyond the channel's usual bank
-- times the valley's cross gradient (the planet cell's slope: a flat plain floods wide, a steep
-- valley little) with a gentle unevenness (low hollows, rises). The water spreads from the channel
-- over the tiles lower than the stage, only through tiles under water themselves.
-- What lies in it is the world's own business: ways are waded or closed (map costs), fields drown
-- or their corn rots, houses take water (the stores on the floor spoil, the walls soften; a weak one
-- falls in), beasts shut in a flooded pen drown unless let out, someone caught in deep water is
-- swept off their feet. People see the river over its banks and answer it with the urgent choice
-- (think.flood_choice: out of the water and up, babes in arms; pens opened); a household whose door
-- is under water sleeps with kin or wherever is dry until it goes down (people.home reads b.wet).
--   s.fl = { st = { stage m, per course }, tg = { target m }, hw = { channel half-width },
--            g = { cross gradient m/tile }, riv = { 1 a river course }, t0, deed, peak, ver }
-- Runtime (never saved): HAND per map (from the saved hw/g/riv: the same after a reload), the water's
-- depth per tile, published as mapm.FLOOD[s] for the ways.
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local dmath = require("sim.dmath")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")
local mapm = require("sim.agents.map")
local surface = require("sim.surface")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

-- (set by sim/agents/init.lua: today's river widths from settle; a building falling in from shake;
-- bob forbids the circular requires)
M.widths = nil
M.fall_in = nil
M.kill = nil

-- The course list of map s (map.lua's, static) and, for a flood, which carry a river today.
local function courses(W, s) return mapm.courses(W, s) end
-- (S24) runtime: the tiles already in a flood's silt list (rebuilt from the saved list after a reload)
local SILT_SEEN = setmetatable({}, { __mode = "k" })

-- Height above nearest drainage per tile (runtime), from the flood's saved course widths and
-- gradients: { h = {}, ci = {} } (h 99 beyond reach or at sea).
local HAND = setmetatable({}, { __mode = "k" })
local function hand(W, s)
  local fl = s.fl
  local e = HAND[s]
  if e and e.fl == fl and e.ver == fl.ver then return e end
  local c = C()
  local cs = courses(W, s)
  local N, T = s.N, s.T
  local half = N / 2
  local NN = N * N
  local d2b, ci = {}, {}
  local BIG = 1e18
  for i = 1, NN do d2b[i], ci[i] = BIG, 0 end
  local reach = c.fl_reach * T
  for j = 1, #cs do
    if (fl.riv[j] or 0) == 1 then
      local co = cs[j]
      local hw = fl.hw[j]
      local xs, ys, sea = co.x, co.y, co.sea
      for k = 0, surface.RIVER_SUB - 1 do
        if not sea[k + 1] then
          local ax, ay, bx, by = xs[k], ys[k], xs[k + 1], ys[k + 1]
          local m = hw + reach
          local ix0 = floor((min(ax, bx) - m) / T + half) + 1
          local ix1 = floor((max(ax, bx) + m) / T + half) + 1
          local iy0 = floor((min(ay, by) - m) / T + half) + 1
          local iy1 = floor((max(ay, by) + m) / T + half) + 1
          if ix1 >= 1 and iy1 >= 1 and ix0 <= N and iy0 <= N then
            local dx, dy = bx - ax, by - ay
            local L2 = dx * dx + dy * dy
            for iy = max(1, iy0), min(N, iy1) do
              for ix = max(1, ix0), min(N, ix1) do
                local i = (iy - 1) * N + ix
                local px, py = (ix - 0.5 - half) * T, (iy - 0.5 - half) * T
                local u = L2 > 0 and ((px - ax) * dx + (py - ay) * dy) / L2 or 0
                if u < 0 then u = 0 elseif u > 1 then u = 1 end
                local ex, ey = px - ax - dx * u, py - ay - dy * u
                -- (distance beyond this course's bank, squared on the far side of it)
                local dd = sqrt(ex * ex + ey * ey) - hw
                if dd < 0 then dd = 0 end
                if dd < d2b[i] then d2b[i], ci[i] = dd, j end
              end
            end
          end
        end
      end
    end
  end
  local h = {}
  local rough, cell = c.fl_rough, c.fl_cell
  local land = s.land
  local key = s.key or s.id
  for i = 1, NN do
    local j = ci[i]
    if j == 0 or land[i] == 0 then
      h[i] = 99
    else
      local ix, iy = (i - 1) % N, floor((i - 1) / N)
      -- gentle unevenness: value noise on a lattice of fl_cell tiles (hollows and low rises)
      local gx, gy = floor(ix / cell), floor(iy / cell)
      local fx, fy = (ix - gx * cell) / cell, (iy - gy * cell) / cell
      local v00 = rng.float(W.seed, rng.S.V3_FLOOD, key, gx * 977 + gy, 1)
      local v10 = rng.float(W.seed, rng.S.V3_FLOOD, key, (gx + 1) * 977 + gy, 1)
      local v01 = rng.float(W.seed, rng.S.V3_FLOOD, key, gx * 977 + gy + 1, 1)
      local v11 = rng.float(W.seed, rng.S.V3_FLOOD, key, (gx + 1) * 977 + gy + 1, 1)
      local a = v00 + (v10 - v00) * fx
      local b = v01 + (v11 - v01) * fx
      local n = (a + (b - a) * fy) * 2 - 1
      local d = d2b[i] / T
      local hv = d * fl.g[j] + n * rough * min(1, d / 3)
      h[i] = hv > 0 and hv or 0
    end
  end
  e = { fl = fl, ver = fl.ver, h = h, ci = ci, d = d2b }
  HAND[s] = e
  return e
end

-- The water's depth per tile (runtime, from the stages), rebuilt when the stages moved.
local DEPTH = setmetatable({}, { __mode = "k" })
local function fill(W, s)
  local fl = s.fl
  local H = hand(W, s)
  local h, ci = H.h, H.ci
  local st = fl.st
  local N = s.N
  local NN = N * N
  local e = DEPTH[s]
  if not e then e = { d = {}, list = {}, ver = 0 }; DEPTH[s] = e end
  local d, list = e.d, e.list
  for k = 1, #list do d[list[k]] = 0 end
  for k = #list, 1, -1 do list[k] = nil end
  if #d < NN then for i = 1, NN do d[i] = d[i] or 0 end end
  local land, riv = s.land, s.riv
  -- from the channel (tiles at the bank's foot) outward through tiles below the stage
  local q, qh = {}, 1
  for i = 1, NN do
    local j = ci[i]
    if j > 0 and land[i] == 1 and h[i] <= 0 and st[j] > 0 then
      d[i] = st[j]
      q[#q + 1] = i
      list[#list + 1] = i
    end
  end
  while qh <= #q do
    local i = q[qh]
    qh = qh + 1
    local ix, iy = (i - 1) % N, floor((i - 1) / N)
    for nb = 1, 4 do
      local k
      if nb == 1 then k = ix > 0 and i - 1 or 0
      elseif nb == 2 then k = ix < N - 1 and i + 1 or 0
      elseif nb == 3 then k = iy > 0 and i - N or 0
      else k = iy < N - 1 and i + N or 0 end
      if k > 0 and d[k] == 0 and land[k] == 1 then
        local j = ci[k]
        if j > 0 then
          local dep = st[j] - h[k]
          if dep > 0 then
            d[k] = dep
            q[#q + 1] = k
            list[#list + 1] = k
          end
        end
      end
    end
  end
  -- (the channel itself is the river's: its tiles are not counted as flood over the land)
  local n = 0
  for k = 1, #list do
    local i = list[k]
    if riv[i] > 0 then d[i] = 0 else n = n + 1 end
  end
  e.n, e.ver = n, e.ver + 1
  e.st = {}
  for j = 1, #st do e.st[j] = st[j] end
  mapm.FLOOD[s] = n > 0 and d or nil
  -- (S24, D-203) the ground the water has lain on in this flood, kept in the order it first went under (saved: the same
  -- after a reload, the depths being the stages' own): its mud when the water goes down
  local fl = s.fl
  if fl and n > 0 then
    local sl = fl.silt
    if not sl then sl = {}; fl.silt = sl end
    local seen = SILT_SEEN[s]
    if not seen or seen.list ~= sl then
      seen = { list = sl, at = {} }
      for k = 1, #sl do seen.at[sl[k]] = true end
      SILT_SEEN[s] = seen
    end
    local at = seen.at
    for k = 1, #list do
      local i = list[k]
      if d[i] > 0 and not at[i] then at[i] = true; sl[#sl + 1] = i end
    end
  end
  return e
end

-- Depth of the flood water at tile i of map s (0 none).
function M.depth_i(s, i)
  local fd = mapm.FLOOD[s]
  return (fd and i > 0) and fd[i] or 0
end

-- Depth at point (x, y) and the margin (m) of that ground above the water standing at its river
-- (negative: under it; 99 far from any river in flood).
function M.at(W, s, x, y)
  if not s.fl then return 0, 99 end
  local i = mapm.tile_of(s, x, y)
  if i == 0 then return 0, 99 end
  local H = hand(W, s)
  local j = H.ci[i]
  local dep = M.depth_i(s, i)
  if j == 0 then return dep, 99 end
  return dep, H.h[i] - (s.fl.st[j] or 0)
end

-- The nearest walkable place from (x, y) whose ground stands at least `safe` m above the water
-- (within 40 tiles), or nil.
function M.high_ground(W, s, x, y, safe)
  if not s.fl then return nil end
  local H = hand(W, s)
  local h, ci, st = H.h, H.ci, s.fl.st
  local N = s.N
  local i0 = mapm.tile_of(s, x, y)
  if i0 == 0 then return nil end
  local ix0, iy0 = (i0 - 1) % N, floor((i0 - 1) / N)
  local cost = s.cost
  local best, bd = nil, 1e9
  for r = 1, 40 do
    for dy = -r, r do
      local edge = dy == -r or dy == r
      local step = edge and 1 or 2 * r
      for dx = -r, r, step do
        local ix, iy = ix0 + dx, iy0 + dy
        if ix >= 0 and iy >= 0 and ix < N and iy < N then
          local i = iy * N + ix + 1
          local j = ci[i]
          local m = j > 0 and h[i] - (st[j] or 0) or 99
          if m >= safe and cost[i] > 0 then
            local d = dx * dx + dy * dy
            if d < bd then best, bd = i, d end
          end
        end
      end
    end
    if best then break end
  end
  if not best then return nil end
  return mapm.tile_xy(s, best)
end

-- Today's stage targets (m above the bank) for each river course of map s, from the planet's flow:
-- fills tg (and returns whether any is above 0), with the courses' channels and gradients.
local function targets(W, s, out)
  local c = C()
  local H = W.S.hydro
  if not H then return false end
  local F, geo = W.F, W.geo
  local fm, bank = H.flow_mean, H.bankfull
  local wd = M.widths(W)
  local cs = courses(W, s)
  local hc = cfgmod.HYDRO
  local any = false
  for j = 1, #cs do
    local cc = cs[j].c
    local tg = 0
    if wd[cc] > 0 then
      local Qf = hc.flood_mult * max(bank[cc], fm[cc])
      local x = Qf > 0 and F.flow[cc] / Qf - 1 or 0
      if x > 0 then
        tg = c.fl_k * dmath.pow(min(x, c.fl_cap), 0.6)
        any = true
      end
    end
    out[j] = tg
  end
  return any
end

-- (S21) the river courses' usual channels and valleys at the start of a flood (kept with it: the
-- flood's ground the same after a reload)
local function begin(W, s, tg, now)
  local c = C()
  local cs = courses(W, s)
  local wd = M.widths(W)
  local fm = W.S.hydro.flow_mean
  local slope = W.geo.slope
  local fl = { st = {}, tg = {}, hw = {}, g = {}, riv = {}, t0 = now, peak = 0, ver = 1 }
  for j = 1, #cs do
    local cc = cs[j].c
    fl.st[j], fl.tg[j] = 0, tg[j]
    if wd[cc] > 0 then
      fl.riv[j] = 1
      fl.hw[j] = 0.5 * (surface.W_MIN + surface.W_K * dmath.pow(fm[cc] / surface.Q_REF, surface.W_B))
      fl.g[j] = c.fl_g * (0.25 + min(2, slope and slope[cc] or 0.3))
    else
      fl.riv[j], fl.hw[j], fl.g[j] = 0, 0, 1
    end
  end
  s.fl = fl
  return fl
end

-- The flood's cause: the event driving the river's flow on the planet (its blame tag: rain - and a
-- god's deluge - carried down the rivers), for the deed.
local function cause_of(W, s, tg)
  local cs = courses(W, s)
  local best, bj = 0, 0
  for j = 1, #cs do if tg[j] > best then best, bj = tg[j], j end end
  if bj == 0 then return 0, 0 end
  local cc = cs[bj].c
  return (W.C and W.C.flow and W.C.flow[cc]) or 0, cc
end

local TG = {}

-- Hourly for each village: the stages toward today's targets; the water spread; what lies in it.
function M.hourly(W, s, now)
  local c = C()
  if not s.land or not s.riv then return end
  -- (the targets move only with the planet's day; a cheap look while nothing floods)
  local fl = s.fl
  if not fl and (s.fl_tick or -1) == W.tick then return end
  s.fl_tick = W.tick
  local any = targets(W, s, TG)
  if not fl then
    if not any then return end
    fl = begin(W, s, TG, now)
  else
    for j = 1, #TG do fl.tg[j] = TG[j] end
  end
  -- the stages: up quickly as the water comes down from the hills, down slowly
  local moved, high, rising = false, 0, false
  for j = 1, #fl.st do
    local a, b = fl.st[j], fl.tg[j] or 0
    local v = a
    if b > a then v = min(b, a + c.fl_rise); rising = true
    elseif b < a then v = max(b, a - c.fl_fall) end
    if v < 0.005 then v = 0 end
    if v ~= a then fl.st[j] = v; moved = true end
    if v > high then high = v end
  end
  if high > fl.peak then fl.peak = high end
  local e = DEPTH[s]
  if high <= 0 then
    M.recede(W, s, now)
    return
  end
  local refill = not e or not e.st
  if not refill and moved then
    for j = 1, #fl.st do
      local dv = fl.st[j] - (e.st[j] or 0)
      if dv >= c.fl_step or dv <= -c.fl_step then refill = true; break end
    end
  end
  if refill then
    local n0 = e and e.n or 0
    e = fill(W, s)
    mapm.rebuild_costs(s)
    if n0 == 0 and e.n > 0 then M.start(W, s, now) end
  end
  if not e or e.n == 0 then return end
  M.lies(W, s, now, rising)
end

-- The water has come over the land: a deed (nature's, or a god's deluge through the rain's tag).
function M.start(W, s, now)
  local fl = s.fl
  local ev, cc = cause_of(W, s, fl.tg)
  local d = deeds.add(W, { kind = "flood", t = now, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0, to_s = s.id,
    what = "river", n = 1, fu = 0, harm = true, event = ev, cell = cc })
  deeds.learn(W, s, d, "the river came over its banks", 0, 0, now)
  fl.deed = d.id
  s.acc.floods = (s.acc.floods or 0) + 1
  if W.record_events then
    d.ev = require("sim.causality.events").emit(W, "flooded_village", s.cell, ev or 0, 1, { settlement = s.id, deed = d.id })
  end
end

local GROUND = { house = true, hut = true, granary = true, woodstore = true, forge = true, tanpit = true, clamp = true, camp = true,
  well = true, saltpan = true, rack = true, fold = true, byre = true, sty = true }
local PEN = { fold = true, byre = true, sty = true }
local FLOOR_STORES = { "food", "grain", "sheaves", "meals", "meat", "fish", "smoked", "cheese", "milk", "flour", "wool", "flax", "hay", "seed" }

-- An hour of standing water: buildings, fields, beasts, people; noticing it.
function M.lies(W, s, now, rising)
  local c = C()
  local V = W.S.v3
  local fl = s.fl
  local fd = mapm.FLOOD[s]
  local deed = fl.deed
  local HD = hand(W, s)
  local hh_, hci = HD.h, HD.ci
  local falls = nil
  -- buildings: the deepest water over their footprint (a footprint's tiles are closed to walking:
  -- the water stands there as on the ground round it)
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b and b.state == "standing" and GROUND[b.kind] then
      local dep = 0
      for iy = b.iy0, b.iy0 + b.h - 1 do
        for ix = b.ix0, b.ix0 + b.w - 1 do
          local i = (iy - 1) * s.N + ix
          local j = hci[i]
          if j > 0 then
            local v = (fl.st[j] or 0) - hh_[i]
            if v > dep then dep = v end
          end
        end
      end
      if dep > 0.02 then
        b.wet = dep
        if PEN[b.kind] then
          -- (a gate opened for the water stays open while the water is in the pen)
          for _, kind in ipairs({ "flock", "cattle", "swine" }) do
            local H = s.herds and s.herds[kind]
            if H and H.pen == b.id and H.loose and H.loose >= fl.t0 then H.loose = now end
          end
        end
        if dep > c.fl_floor and not PEN[b.kind] then
          if b.wet_flood ~= deed then
            b.wet_flood = deed
            -- what lay on the floor spoils (a granary stands on raised posts)
            if b.kind ~= "granary" or dep > c.fl_granary then
              local f = c.fl_spoil * min(1, dep / 0.8)
              local lost = 0
              for _, key in ipairs(FLOOR_STORES) do
                local q = b.stock and b.stock[key]
                if q and q > 0 then lost = lost + q * f; b.stock[key] = q * (1 - f) end
              end
              s.acc.flood_spoilt = (s.acc.flood_spoilt or 0) + lost
            end
            local hh = V.households[b.hh or 0]
            if b.kind == "house" or b.kind == "hut" then
              s.acc.flood_houses = (s.acc.flood_houses or 0) + 1
              for _, id in ipairs(hh and hh.members or {}) do
                local q = V.people[id]
                if q and q.alive and people.stage(q, now) ~= "infant" then
                  people.remember(q, now, "The river came into the house")
                end
              end
            end
          end
          b.cond = max(0, (b.cond or 1) - c.fl_wear * dep * (b.kind == "hut" and 2 or 1))
          if dep > c.fl_fall and (b.kind == "hut" or b.kind == "camp" or (b.cond or 1) < c.fl_weak) and M.fall_in then
            falls = falls or {}
            falls[#falls + 1] = b
          end
        end
      elseif b.wet then
        b.wet = nil
      end
    end
  end
  for k = 1, #(falls or {}) do M.fall_in(W, s, falls[k], now, deeds.get(W, deed), "flood") end
  -- fields: the share of a field under water; hours of it drown a growing crop, rot ripe corn and
  -- sheaves; the silt is left when it goes down
  local N = s.N
  for _, pl in ipairs(s.plots) do
    if pl.alive then
      local wet, deep = 0, 0
      for iy = pl.iy0, pl.iy0 + pl.h - 1, 2 do
        for ix = pl.ix0, pl.ix0 + pl.w - 1, 2 do
          local v = fd and fd[(iy - 1) * N + ix] or 0
          if v > 0 then wet = wet + 1; if v > deep then deep = v end end
        end
      end
      local share = wet / (floor((pl.h + 1) / 2) * floor((pl.w + 1) / 2))
      if share >= c.fl_field_share then
        pl.wet_h = (pl.wet_h or 0) + 1
        pl.wet_max = max(pl.wet_max or 0, deep)
        pl.wet_flood = deed
        local told = nil
        if pl.stage == "growing" and pl.wet_h >= c.fl_crop_h and pl.drowned ~= deed then
          pl.drowned = deed
          pl.frost = (pl.frost or 1) * (1 - c.fl_drown * share)
          do
            local dd = deeds.add(W, { kind = "drowned", t = now, at = s.id, x = (pl.x0 + pl.x1) * 0.5, y = (pl.y0 + pl.y1) * 0.5,
              by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = pl.hh or 0, to_s = s.id, what = "field", n = 1,
              fu = (pl.sown or 0) * c.fl_drown * share * 2, harm = true, answers = deed })
            deeds.learn(W, s, dd, "the flood", 0, 0, now)
          end
          told = sfmt("Our %s field drowned in the flood", pl.crop == "flax" and "flax" or "corn")
          s.acc.flood_fields = (s.acc.flood_fields or 0) + 1
        elseif pl.stage == "ripe" and pl.wet_h >= c.fl_ripe_h and pl.drowned ~= deed then
          pl.drowned = deed
          pl.spoil = (pl.spoil or 1) * (1 - c.fl_rot * share)
          told = "The ripe corn lay flat in the flood water and rotted"
          s.acc.flood_fields = (s.acc.flood_fields or 0) + 1
          local dd = deeds.add(W, { kind = "drowned", t = now, at = s.id, x = (pl.x0 + pl.x1) * 0.5, y = (pl.y0 + pl.y1) * 0.5,
            by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = pl.hh or 0, to_s = s.id, what = "corn", n = 1,
            fu = math.max(0, (pl.sown or 0) - (pl.harvested or 0)) * c.fl_rot * share * 2, harm = true, answers = deed })
          deeds.learn(W, s, dd, "the flood", 0, 0, now)
        end
        if (pl.sheaves or 0) > 0 and pl.wet_h >= c.fl_sheaf_h then
          pl.sheaves = pl.sheaves * (1 - c.fl_rot * share)
          if pl.sheaves < 0.5 then pl.sheaves = 0 end
        end
        if told and (pl.hh or 0) ~= 0 then
          for _, id in ipairs((V.households[pl.hh] or {}).members or {}) do
            local q = V.people[id]
            if q and q.alive and people.stage(q, now) ~= "infant" then people.remember(q, now, told) end
          end
        end
      end
    end
  end
  -- beasts: one standing in water above its own depth in a pen it cannot leave drowns by the hour
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive then
          local i = mapm.tile_of(s, a.tx, a.ty)
          local v = fd and i > 0 and fd[i] or 0
          local over = v - (c.fl_beast_d[a.sp] or 0.6)
          if over > 0 then
            a.health = (a.health or 1) - c.fl_beast_h * min(1, over / 0.5)
            a.wet = now
            if a.health <= 0 then
              a.health = 0
              a.alive, a.died, a.cause = false, now, "flood"
              s.acc.stock_died = (s.acc.stock_died or 0) + 1
              s.acc.stock_drowned = (s.acc.stock_drowned or 0) + 1
              local hh = V.households[a.owner or 0]
              for _, id in ipairs(hh and hh.members or {}) do
                local q = V.people[id]
                if q and q.alive and people.stage(q, now) ~= "infant" and (q.beast_drowned or -1) ~= floor(now) then
                  q.beast_drowned = floor(now)
                  people.remember(q, now, sfmt("One of our %s drowned in the flood", a.sp == "cattle" and "cattle" or (a.sp == "sheep" and "sheep" or a.sp .. "s")))
                end
              end
            end
          end
        end
      end
    end
  end
  -- people: anyone standing in water deeper than a person keeps their feet in is swept off them
  local seen = {}
  danger.each_on_map(W, s, function(p)
    if p.act == "carried" or (p.riding or 0) ~= 0 or p.trapped or p.act == "travel" or p.act == "voyage" then return end
    local px, py = danger.person_pos(p, now)
    local dep, margin = M.at(W, s, px, py)
    local inside = p.inside ~= 0 and s.buildings[p.inside]
    if inside then dep = inside.wet or 0; margin = -dep end
    if dep > c.fl_sweep then
      local sev = c.fl_hurt * min(1, (dep - c.fl_sweep) / 0.6 + 0.3)
        * (0.5 + rng.float(W.seed, rng.S.V3_FLOOD, p.key, floor(now * 60) % 4294967296, 2))
      if sev > 0.05 and (p.swept or -1) ~= floor(now) then
        p.swept = floor(now)
        people.hurt(p, min(1, sev), "drowned", deed, now)
        people.remember(p, now, "The flood water swept me off my feet")
        s.acc.flood_swept = (s.acc.flood_swept or 0) + 1
        if (p.hurt or 0) >= 1 and M.kill then M.kill(W, s, p, "drowned", now) end
      end
    end
    -- (noticing: the water in the house - felt even asleep; at their feet; the river over its banks
    -- near those awake out of doors. Raised again when what they face changes, else after a while)
    if p.alive and people.stage(p, now) ~= "infant" then
      local how, why = nil, nil
      if inside and dep > 0.02 then how, why = "felt", "Water is coming into the house"
      elseif dep > 0.02 then how, why = "saw", "The flood water is round my feet"
      elseif not inside and p.act ~= "sleep" and margin < c.fl_near then how, why = "saw", "The river is over its banks"
      end
      if how and (p.fl_why ~= why or now - (p.fl_alarm or -99) >= c.fl_alarm_h or (rising and dep > 0.02)) then
        p.fl_why, p.fl_alarm = why, now
        danger.raise(W, p, { kind = "flood", src = 0, why = why }, px, py, now, how)
        if (p.flood_seen or -1) ~= fl.deed then
          p.flood_seen = fl.deed
          people.remember(p, now, "Saw the river come over its banks")
        end
        seen[#seen + 1] = p
      end
    end
  end)
  -- (those who saw it shout it: a sound that wakes those near)
  for k = 1, min(4, #seen) do
    local p = seen[k]
    local px, py = danger.person_pos(p, now)
    if p.fl_shout ~= fl.deed then
      p.fl_shout = fl.deed
      danger.cue(W, s, px, py, now, { kind = "flood", src = 0, sight = 0, night = 0, sound = c.fl_shout, loud = true, skip = p.id,
        why = sfmt("%s is shouting: the river is in flood", p.name) })
    end
  end
end

-- The water has gone down: silt on the fields it lay on, the houses dry, the ways open again.
function M.recede(W, s, now)
  local c = C()
  local V = W.S.v3
  local fl = s.fl
  if not fl then return end
  for _, pl in ipairs(s.plots) do
    if pl.wet_flood == fl.deed and (pl.wet_max or 0) > 0 then
      pl.fert = min(c.fl_fert_max, (pl.fert or 1) + c.fl_silt * min(1, pl.wet_max))
    end
    pl.wet_h, pl.wet_max = nil, nil
  end
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b then b.wet = nil end
  end
  local d = deeds.get(W, fl.deed)
  if d then d.peak = sfmt("%.2f", fl.peak); d.hours = floor(now - fl.t0) end
  -- (S24, D-203) mud and silt over the ground it lay on, drying out over the weeks (drawn; the next flood's replaces it)
  if fl.silt and #fl.silt > 0 then s.silt = { t = now, tiles = fl.silt } end
  danger.each_on_map(W, s, function(p)
    if p.flood_seen == fl.deed and people.stage(p, now) ~= "infant" then people.remember(p, now, "The flood water has gone down") end
  end)
  s.fl = nil
  DEPTH[s] = nil
  HAND[s] = nil
  if mapm.FLOOD[s] then
    mapm.FLOOD[s] = nil
    mapm.rebuild_costs(s)
  end
  s.fl_tick = W.tick
end

-- (after a reload: the water's depth published again from the saved stages, before anyone walks)
function M.ensure(W, s)
  if s.fl and not DEPTH[s] then
    fill(W, s)
  end
end

-- The flood's runtime version (the view redraws the water when it changes) and the depth array.
function M.view(s)
  local e = DEPTH[s]
  return e and e.ver or 0, mapm.FLOOD[s]
end

return M
