-- Trade between settlements (roadmap v3 R4, D-097, D-101): the roads. What is worth carrying where
-- is sim/agents/econ.lua; the trip itself is think.lua's trip_legs (out with the goods, the
-- exchanges at the other village, home) and act.lua's deal steps.
--   p.trip = { to, from, give, take, n, aX, aH, pool, deals, back }
--   p.at   = the settlement whose map the person is on now (nil: their own)
local cfgmod = require("sim.config")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min, sqrt, acos = math.floor, math.max, math.min, math.sqrt, math.acos
local function C() return cfgmod.V3 end

-- Great-circle distance between two settlements (local units) and the hours on the road.
function M.road(W, a, b)
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
  for k = 1, cnt[s.cell] do
    local j = nbr[s.cell * 6 - 6 + k]
    if j > 0 and ocean[j] == 1 and not prev[j] then prev[j] = -1; q[#q + 1] = j end
  end
  local goal = {}
  for k = 1, cnt[o.cell] do
    local j = nbr[o.cell * 6 - 6 + k]
    if j > 0 and ocean[j] == 1 then goal[j] = true end
  end
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
    local d = (tx - x) ^ 2 + (ty - y) ^ 2
    if d < bd then best, bd = ring[q], d end
  end
  if best == 0 then return nil end
  return mapm.tile_xy(s, best)
end

-- Where a person leaves map s for settlement o (and arrives on o's map from s).
function M.gate(W, s, o)
  return require("sim.agents.wild").edge_toward(W, s, o.cell)
end

return M
