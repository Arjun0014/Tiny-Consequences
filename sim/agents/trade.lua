-- Trade between settlements (roadmap v3 R4, D-097, D-101): the roads. What is worth carrying where
-- is sim/agents/econ.lua; the trip itself is think.lua's trip_legs (out with the goods, the
-- exchanges at the other village, home) and act.lua's deal steps.
--   p.trip = { to, from, give, take, n, aX, aH, pool, deals, back }
--   p.at   = the settlement whose map the person is on now (nil: their own)
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local cfgmod = require("sim.config")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min, sqrt, acos = math.floor, math.max, math.min, math.sqrt, dmath.acos
local function C() return cfgmod.V3 end

-- The road between two settlements: its length (local units) and the hours walking it - the land
-- route over the planet's cells (S17: round mountains, through forest, across rivers at fords), or
-- the great circle where there is none (other landmasses).
function M.road(W, a, b)
  local r = M.land_route(W, a.cell, b.cell)
  if r then return r.len, r.hrs end
  local g = W.grid
  local ca, cb = a.cell, b.cell
  local dot = g.X[ca] * g.X[cb] + g.Y[ca] * g.Y[cb] + g.Z[ca] * g.Z[cb]
  if dot > 1 then dot = 1 elseif dot < -1 then dot = -1 end
  local d = acos(dot) * require("sim.agents.map").R
  return d, d / C().road_speed
end

-- Settlements reachable on foot from s (the same landmass, within reach), nearest first.
local PARTNERS = setmetatable({}, { __mode = "k" })
function M.partners(W, s)
  local P = PARTNERS[s]
  if P then return P end
  P = {}
  local cont = W.geo.continent
  for _, o in ipairs(W.S.v3.settlements) do
    if o ~= s and o.alive and cont[o.cell] == cont[s.cell] and cont[s.cell] ~= 0 then
      local d = M.road(W, s, o)
      if d <= C().trade_reach then P[#P + 1] = o end
    end
  end
  table.sort(P, function(x, y)
    local dx, dy = M.road(W, s, x), M.road(W, s, y)
    if dx ~= dy then return dx < dy end
    return x.id < y.id
  end)
  PARTNERS[s] = P
  return P
end

-- The sea way between two settlements with docks (D-102): the shortest chain of ocean cells from
-- one's shore to the other's (a breadth-first search over the planet's ocean cells from every ocean
-- cell next to s's cell, to one next to o's). Returns { cells = {...}, len = local units } or false.
-- Runtime cache (the geography does not change while a world runs).
local ROUTES = setmetatable({}, { __mode = "k" })
function M.sea_route(W, s, o)
  local cache = ROUTES[W]
  if not cache then cache = {}; ROUTES[W] = cache end
  local key = s.id * 1000 + o.id
  local r = cache[key]
  if r ~= nil then return r end
  local g = W.grid
  local ocean = W.geo.ocean
  local nbr, cnt = g.nbr, g.nbr_count
  local prev, q, qi = {}, {}, 1
  -- (S14) the sea a village's map reaches: the ocean cells within two rings of its cell (a map
  -- spans about two cells either way; its water must still reach the map's border, sea_gate)
  local sites = require("sim.society.sites")
  local function near_sea(c)
    local out = {}
    for _, j in ipairs(sites.rings(W, c, 2)) do if ocean[j] == 1 then out[#out + 1] = j end end
    table.sort(out)
    return out
  end
  for _, j in ipairs(near_sea(s.cell)) do
    if not prev[j] then prev[j] = -1; q[#q + 1] = j end
  end
  local goal = {}
  for _, j in ipairs(near_sea(o.cell)) do goal[j] = true end
  local hit = nil
  while qi <= #q do
    local c = q[qi]; qi = qi + 1
    if goal[c] then hit = c; break end
    for k = 1, cnt[c] do
      local j = nbr[c * 6 - 6 + k]
      if j > 0 and ocean[j] == 1 and not prev[j] then prev[j] = c; q[#q + 1] = j end
    end
  end
  if not hit then cache[key] = false; return false end
  local cells = {}
  local c = hit
  while c and c > 0 do cells[#cells + 1] = c; c = prev[c] end
  -- (s's end first)
  for i = 1, floor(#cells / 2) do cells[i], cells[#cells + 1 - i] = cells[#cells + 1 - i], cells[i] end
  local R = require("sim.agents.map").R
  local function arc(a, b)
    local dot = g.X[a] * g.X[b] + g.Y[a] * g.Y[b] + g.Z[a] * g.Z[b]
    if dot > 1 then dot = 1 elseif dot < -1 then dot = -1 end
    return acos(dot) * R
  end
  local len = arc(s.cell, cells[1]) + arc(cells[#cells], o.cell)
  for i = 2, #cells do len = len + arc(cells[i - 1], cells[i]) end
  r = { cells = cells, len = len }
  cache[key] = r
  return r
end

-- Settlements reachable by sea from s (both with docks, within sea_reach), nearest first.
local SEA = setmetatable({}, { __mode = "k" })
function M.sea_partners(W, s)
  local P = SEA[s]
  if P then return P end
  P = {}
  if s.dock then
    for _, o in ipairs(W.S.v3.settlements) do
      if o ~= s and o.alive and o.dock then
        local r = M.sea_route(W, s, o)
        if r and r.len <= C().sea_reach and M.sea_gate(W, s, r.cells[1]) and M.sea_gate(W, o, r.cells[#r.cells]) then
          P[#P + 1] = o
        end
      end
    end
    table.sort(P, function(x, y)
      local a, b = M.sea_route(W, s, x).len, M.sea_route(W, s, y).len
      if a ~= b then return a < b end
      return x.id < y.id
    end)
  end
  SEA[s] = P
  return P
end

-- Where a boat leaves map s by sea toward planet cell c (D-102): the sailable water of the map's
-- border ring nearest that way (nil when the map's water does not reach its border).
local SEDGE = setmetatable({}, { __mode = "k" })
function M.sea_gate(W, s, c)
  local mapm = require("sim.agents.map")
  local ring = SEDGE[s]
  if not ring then
    local e = {}
    local N = s.N
    for ix = 3, N - 2 do e[#e + 1] = 2 * N + ix; e[#e + 1] = (N - 3) * N + ix end
    for iy = 4, N - 3 do e[#e + 1] = (iy - 1) * N + 3; e[#e + 1] = (iy - 1) * N + N - 2 end
    ring = {}
    for _, i in ipairs(e) do if s.wcost[i] > 0 and s.land[i] == 0 then ring[#ring + 1] = i end end
    SEDGE[s] = ring
  end
  local g = W.grid
  local x, y = mapm.local_of(s, g.X[c], g.Y[c], g.Z[c])
  x, y = x or 1, y or 0
  local best, bd = 0, 1e18
  for q = 1, #ring do
    local tx, ty = mapm.tile_xy(s, ring[q])
    local d = dsq(tx - x) + dsq(ty - y)
    if d < bd then best, bd = ring[q], d end
  end
  if best == 0 then return nil end
  return mapm.tile_xy(s, best)
end

-- (S17) A settlement appeared (or went): the partners by road and by sea are worked out again.
function M.forget(W)
  for k in pairs(PARTNERS) do PARTNERS[k] = nil end
  for k in pairs(SEA) do SEA[k] = nil end
end

-- Where a person leaves map s for settlement o (and arrives on o's map from s).
function M.gate(W, s, o)
  return require("sim.agents.wild").edge_toward(W, s, o.cell)
end

-- (S17, R5) The way overland between two planet cells as people would walk it: the cheapest chain of
-- land cells (A* over the cell graph), each step costing its walk (arc / road_speed) made dearer by
-- the ground - steep slopes, high ground and snow, thick forest - and by rivers crossed (wider ones
-- dearer: a ford found, a raft made). Passes and fords matter because the way goes round what
-- costs. Returns { cells = {c0 ... c1}, len = local units, hrs = hours walked } or false (no way by
-- land). Runtime cache (the geography does not change while a world runs).
local LROUTES = setmetatable({}, { __mode = "k" })
local LRIV = setmetatable({}, { __mode = "k" })
local function river_flow(W)
  local r = LRIV[W]
  if r then return r end
  local H = W.S.hydro
  local fm = H and H.flow_mean
  local thr = 1e18
  if fm then
    local _, t = require("sim.surface").river_widths(W)
    thr = t
  end
  r = { fm = fm, thr = thr }
  LRIV[W] = r
  return r
end
function M.land_route(W, c0, c1)
  local cache = LROUTES[W]
  if not cache then cache = {}; LROUTES[W] = cache end
  local key = c0 * 4096 + c1
  local r = cache[key]
  if r ~= nil then return r end
  if c0 > c1 then
    -- (one way for both directions: the way back is the way there, walked the other way)
    local f = M.land_route(W, c1, c0)
    if f then
      local cells = {}
      for i = #f.cells, 1, -1 do cells[#cells + 1] = f.cells[i] end
      r = { cells = cells, len = f.len, hrs = f.hrs }
    else
      r = false
    end
    cache[key] = r
    return r
  end
  local c = C()
  local g = W.grid
  local ocean = W.geo.ocean
  if ocean[c0] == 1 or ocean[c1] == 1 or W.geo.continent[c0] ~= W.geo.continent[c1] then
    cache[key] = false
    return false
  end
  local R = require("sim.agents.map").R
  local X, Y, Z = g.X, g.Y, g.Z
  local function arc(a, b)
    local dot = X[a] * X[b] + Y[a] * Y[b] + Z[a] * Z[b]
    if dot > 1 then dot = 1 elseif dot < -1 then dot = -1 end
    return acos(dot) * R
  end
  local F, elev, slope = W.F, W.static and W.static.elev, W.geo.slope
  local RF = river_flow(W)
  local fm, thr = RF.fm, RF.thr
  local spd = c.road_speed
  local function step(a, b)
    local f = 1 + c.route_slope_k * (slope and slope[b] or 0) + c.route_forest_k * (F.forest[b] or 0)
      + c.route_high_k * max(0, (elev and elev[b] or 0) - c.route_high) + c.route_ice_k * (F.ice[b] or 0)
    local h = arc(a, b) / spd * f
    if fm and fm[b] >= thr and not (fm[a] >= thr) then
      -- (into a river: a ford found or a raft made, the wider the longer)
      h = h + c.route_river_h * min(3, sqrt(fm[b] / thr))
    end
    return h
  end
  -- A* (binary heap of (f, cell); ties by cell)
  local gs, prev, closed = { [c0] = 0 }, {}, {}
  local hf, hc, n = {}, {}, 0
  local function push(f, cell)
    n = n + 1
    local i = n
    while i > 1 do
      local p = (i - i % 2) / 2
      if hf[p] < f or (hf[p] == f and hc[p] < cell) then break end
      hf[i], hc[i] = hf[p], hc[p]
      i = p
    end
    hf[i], hc[i] = f, cell
  end
  local function pop()
    local f, cell = hf[1], hc[1]
    local lf, lc = hf[n], hc[n]
    hf[n], hc[n] = nil, nil
    n = n - 1
    if n > 0 then
      local i = 1
      while true do
        local l = i * 2
        if l > n then break end
        local rr = l + 1
        if rr <= n and (hf[rr] < hf[l] or (hf[rr] == hf[l] and hc[rr] < hc[l])) then l = rr end
        if lf < hf[l] or (lf == hf[l] and lc < hc[l]) then break end
        hf[i], hc[i] = hf[l], hc[l]
        i = l
      end
      hf[i], hc[i] = lf, lc
    end
    return f, cell
  end
  push(arc(c0, c1) / spd, c0)
  local nbr, cnt = g.nbr, g.nbr_count
  local found = false
  while n > 0 do
    local _, a = pop()
    if a == c1 then found = true; break end
    if not closed[a] then
      closed[a] = true
      local ga = gs[a]
      for k = 1, cnt[a] do
        local b = nbr[a * 6 - 6 + k]
        if b > 0 and ocean[b] == 0 and not closed[b] then
          local gb = ga + step(a, b)
          if not gs[b] or gb < gs[b] then
            gs[b] = gb
            prev[b] = a
            push(gb + arc(b, c1) / spd, b)
          end
        end
      end
    end
  end
  if not found then cache[key] = false; return false end
  local cells = {}
  local x = c1
  while x do cells[#cells + 1] = x; x = prev[x] end
  for i = 1, floor(#cells / 2) do cells[i], cells[#cells + 1 - i] = cells[#cells + 1 - i], cells[i] end
  local len = 0
  for i = 2, #cells do len = len + arc(cells[i - 1], cells[i]) end
  r = { cells = cells, len = len, hrs = gs[c1] }
  cache[key] = r
  return r
end

return M
