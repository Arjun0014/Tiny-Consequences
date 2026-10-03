-- Navigation on the settlement's tile map (docs/v3/10_AGENTS.md §3): A* (8-connected, no corner
-- cutting, octile heuristic), string-pulled into waypoints, cached per map version.
--
-- nav.path(s, x0, y0, x1, y1, temper, out) fills `out` = { n, x = {}, y = {}, cum = {}, len }
-- (waypoints in local units, cumulative lengths) and returns it, or nil when unreachable.
-- The cost field changes only at deterministic moments (the daily lane update, a new building:
-- map.rebuild_costs bumps s.ver), so a cached path is exactly the path A* would find again —
-- live and reloaded worlds walk the same lines.
local cfgmod = require("sim.config")
local mapm = require("sim.agents.map")

local M = {}
local sqrt, floor, abs, min, max = math.sqrt, math.floor, math.abs, math.min, math.max
local SQ2 = sqrt(2)

-- Scratch (shared by all settlements; reset by stamps, never cleared).
local G, PAR, ST_OPEN, ST_CLOSED = {}, {}, {}, {}
-- (filled densely once so that every runtime keeps them as arrays, not hash tables: tile indices
-- arrive in any order, D-086)
for i = 1, 128 * 128 do G[i], PAR[i], ST_OPEN[i], ST_CLOSED[i] = 0, 0, 0, 0 end
local stamp = 0
M.stats = { searches = 0, expanded = 0, cache_hits = 0, fallbacks = 0 }
M.MAX_EXPAND = 9000
M.HW = 0.95               -- the heuristic per tile step (D-086)

-- Tile coordinates by index (one table per map size; the interpreter pays for every floor and
-- division, D-086).
local IXY = {}
local function ixy(N)
  local t = IXY[N]
  if t then return t[1], t[2] end
  local IX, IY = {}, {}
  for iy = 1, N do
    for ix = 1, N do
      local i = (iy - 1) * N + ix
      IX[i], IY[i] = ix, iy
    end
  end
  IXY[N] = { IX, IY }
  return IX, IY
end

-- Line of sight between tile centres a and b: every tile the straight line passes through is
-- walkable and no costlier than cmax (the end tiles excepted); where the line passes exactly
-- through a tile corner, neither tile beside it may be closed. An exact grid traversal in integer
-- arithmetic (D-086).
local function los(s, a, b, cmax)
  local N = s.N
  local IX, IY = ixy(N)
  local cost = s.cost
  local dx, dy = IX[b] - IX[a], IY[b] - IY[a]
  local sx, sy = 1, N
  if dx < 0 then dx, sx = -dx, -1 elseif dx == 0 then sx = 0 end
  if dy < 0 then dy, sy = -dy, -N elseif dy == 0 then sy = 0 end
  if dx == 0 and dy == 0 then return true end
  -- the next x and y crossings in units of 1 / (2 dx dy): (2k + 1) dy and (2k + 1) dx
  local nx = dx > 0 and dy or 1e30
  local ny = dy > 0 and dx or 1e30
  if dy == 0 then nx = 1 end
  if dx == 0 then ny = 1 end
  local ddx, ddy = 2 * dy, 2 * dx
  if dy == 0 then ddx = 2 end
  if dx == 0 then ddy = 2 end
  local i = a
  for _ = 1, dx + dy do
    if nx < ny then
      i = i + sx
      nx = nx + ddx
    elseif ny < nx then
      i = i + sy
      ny = ny + ddy
    else
      -- through a corner: the two tiles beside it must be open
      if cost[i + sx] < 0 or cost[i + sy] < 0 then return false end
      i = i + sx + sy
      nx = nx + ddx
      ny = ny + ddy
    end
    if i == b then return true end
    local c = cost[i]
    if c < 0 or c > cmax then return false end
  end
  return true
end
M.los = los

-- A* from tile a to tile b; fills `tiles` with the tile path (a .. b); returns its length or 0.
-- The open list is a bucket queue (D-086): f quantised to 1/BQ of an open-ground step, last in
-- first out within a bucket — O(1) pushes and pops (a binary heap's sifting was half the cost in
-- the interpreter). Deterministic: the same map and query always expand the same tiles.
local BQ = 8
local BH, BS = {}, {}             -- bucket head entry, bucket stamp
local EN, EX = {}, {}             -- entry: node, next entry
for i = 0, 16383 do BH[i], BS[i] = 0, 0 end
for i = 1, 4096 do EN[i], EX[i] = 0, 0 end
local function astar(s, a, b, temper, tiles)
  local C = cfgmod.V3
  local N = s.N
  local IX, IY = ixy(N)
  local cost, lane = s.cost, s.lane
  stamp = stamp + 2
  local so, sc = stamp, stamp + 1
  local tk = 1 + temper * C.temper_extra
  local bx, by = IX[b], IY[b]
  local hw = M.HW * BQ
  local hd = hw * (SQ2 - 2)
  local oNW, oN, oNE, oW, oE, oSW, oS, oSE = -N - 1, -N, -N + 1, -1, 1, N - 1, N, N + 1
  local Gs, PARs, SO, SC = G, PAR, ST_OPEN, ST_CLOSED
  local bh, bs, en, ex = BH, BS, EN, EX
  -- the goal may be a closed tile (a door, a footprint): open it for this search
  local cb = cost[b]
  if cb < 0 then cost[b] = 1 end
  Gs[a] = 0
  PARs[a] = 0
  SO[a] = so
  local ddx, ddy = abs(IX[a] - bx), abs(IY[a] - by)
  local q0 = floor(hw * (ddx + ddy) + hd * (ddx < ddy and ddx or ddy))
  local ne = 1
  en[1], ex[1] = a, 0
  bh[q0], bs[q0] = 1, sc
  local qmin, qmax = q0, q0
  local expanded = 0
  local found = false
  local maxe = M.MAX_EXPAND
  while true do
    -- pop: the lowest non-empty bucket
    while qmin <= qmax and (bs[qmin] ~= sc or bh[qmin] == 0) do qmin = qmin + 1 end
    if qmin > qmax then break end
    local e = bh[qmin]
    bh[qmin] = ex[e]
    local cur = en[e]
    if SC[cur] ~= sc then
      SC[cur] = sc
      if cur == b then found = true; break end
      expanded = expanded + 1
      if expanded > maxe then break end
      local gc = Gs[cur]
      -- (the map's border tiles are closed, so every neighbour of an expanded tile is on the map)
      -- The eight neighbours in a fixed order (NW N NE W E SW S SE), written out: the same pushes
      -- in the same order as a loop over direction tables, without the table reads (D-091).
      local cN, cW, cE, cS = cost[cur - N], cost[cur - 1], cost[cur + 1], cost[cur + N]
      local cx, cy = IX[cur] - bx, IY[cur] - by
      do
        local j = cur + oNW
        local c = cost[j]
        if c > 0 and SC[j] ~= sc and cW >= 0 and cN >= 0 then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c * SQ2
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx - 1, cy - 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oN
        local c = cN
        if c > 0 and SC[j] ~= sc then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx, cy - 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oNE
        local c = cost[j]
        if c > 0 and SC[j] ~= sc and cE >= 0 and cN >= 0 then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c * SQ2
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx + 1, cy - 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oW
        local c = cW
        if c > 0 and SC[j] ~= sc then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx - 1, cy
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oE
        local c = cE
        if c > 0 and SC[j] ~= sc then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx + 1, cy
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oSW
        local c = cost[j]
        if c > 0 and SC[j] ~= sc and cW >= 0 and cS >= 0 then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c * SQ2
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx - 1, cy + 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oS
        local c = cS
        if c > 0 and SC[j] ~= sc then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx, cy + 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
      do
        local j = cur + oSE
        local c = cost[j]
        if c > 0 and SC[j] ~= sc and cE >= 0 and cS >= 0 then
          if lane[j] == 0 then c = c * tk end
          local g2 = gc + c * SQ2
          if SO[j] ~= so or g2 < Gs[j] then
            Gs[j] = g2
            PARs[j] = cur
            SO[j] = so
            local hx, hy = cx + 1, cy + 1
            if hx < 0 then hx = -hx end
            if hy < 0 then hy = -hy end
            local q = g2 * BQ + hw * (hx + hy) + hd * (hx < hy and hx or hy)
            q = q - q % 1
            ne = ne + 1
            en[ne] = j
            if bs[q] ~= sc then bs[q] = sc; ex[ne] = 0 else ex[ne] = bh[q] end
            bh[q] = ne
            if q < qmin then qmin = q end
            if q > qmax then qmax = q end
          end
        end
      end
    end
  end
  cost[b] = cb
  M.stats.searches = M.stats.searches + 1
  M.stats.expanded = M.stats.expanded + expanded
  if not found then return 0 end
  -- walk back
  local k = 0
  local cur = b
  while cur ~= 0 do
    k = k + 1
    tiles[k] = cur
    cur = PAR[cur]
  end
  -- reverse
  for i = 1, floor(k / 2) do
    tiles[i], tiles[k - i + 1] = tiles[k - i + 1], tiles[i]
  end
  return k
end

local TILES = {}
local nav_short

-- Short trips with a clear straight line over ground no costlier than open land.
nav_short = function(s, a, b)
  local IX, IY = ixy(s.N)
  local dx, dy = IX[b] - IX[a], IY[b] - IY[a]
  if dx * dx + dy * dy > 24 * 24 then return false end
  local cmax = cfgmod.V3.cost_open
  local ca = s.cost[a]
  if ca > cmax then cmax = ca end
  return los(s, a, b, cmax + 1e-9)
end

-- Waypoint tiles for a tile path: string pulling with a cost ceiling (the costliest tile of the
-- pulled stretch), so a path that keeps to a lane does not cut across the field beside it. From
-- each waypoint the farthest visible tile is found by galloping (2, 4, 8 ... tiles ahead) and
-- then halving (D-086: the old tile-by-tile scan was quadratic in the path's length).
local function pull(s, tiles, k, out)
  local cost = s.cost
  local m = 1
  out[1] = tiles[1]
  local i = 1
  while i < k do
    -- cm(j): the costliest tile of tiles[i + 1 .. j] (closed tiles count as 1)
    local ti = tiles[i]
    local good = i + 1
    local cmax = cost[tiles[good]]
    if cmax < 0 then cmax = 1 end
    local step = 1
    local bad = 0
    local cm = cmax
    local scanned = good
    while true do
      local j = good + step
      if j > k then j = k end
      if j <= good then break end
      -- the ceiling up to j
      local cj = cm
      for q = scanned + 1, j do
        local c = cost[tiles[q]]
        if c < 0 then c = 1 end
        if c > cj then cj = c end
      end
      if los(s, ti, tiles[j], cj + 1e-9) then
        good, cm, scanned = j, cj, j
        if j == k then break end
        step = step * 2
      else
        bad = j
        break
      end
    end
    -- halve between the last visible and the first blocked
    while bad > 0 and bad - good > 1 do
      local mid = good + floor((bad - good) / 2)
      local cj = cm
      for q = good + 1, mid do
        local c = cost[tiles[q]]
        if c < 0 then c = 1 end
        if c > cj then cj = c end
      end
      if los(s, ti, tiles[mid], cj + 1e-9) then good, cm = mid, cj else bad = mid end
    end
    m = m + 1
    out[m] = tiles[good]
    i = good
  end
  return m
end

-- (runtime only, never saved: keyed by the settlement table)
local CACHES = setmetatable({}, { __mode = "k" })

-- (S17: valid while the cost field's content is the same - map.cost_ver - not merely its version:
-- the daily rebuild that changes nothing keeps the paths)
local function cache_get(s, key)
  local pc = CACHES[s]
  local v = mapm.cost_ver[s] or -1
  if not pc or pc.ver ~= v then
    pc = { ver = v, map = {}, n = 0 }
    CACHES[s] = pc
    return nil
  end
  return pc.map[key]
end

local function cache_put(s, key, v)
  local pc = CACHES[s]
  if pc.n >= 1500 then
    pc.map, pc.n = {}, 0
  end
  pc.map[key] = v
  pc.n = pc.n + 1
end

-- Fill `out` with a path from (x0, y0) to (x1, y1).
-- (S20) The land's cost field with the deep river passable at cost_swim (swimming, wading where
-- it is too deep to ford): for someone cut off by risen water (runtime, rebuilt with the field).
local SWIM = setmetatable({}, { __mode = "k" })
local function swim_cost(s)
  local ver = mapm.cost_ver[s] or -1
  local e = SWIM[s]
  if e and e.ver == ver then return e.cost end
  e = e or { cost = {} }
  local sc, cost, riv, land, occ = e.cost, s.cost, s.riv, s.land, s.occ
  local k = cfgmod.V3.cost_swim
  local fd = mapm.FLOOD[s]   -- (S21: flood water over the land swum as the river is)
  for i = 1, s.N * s.N do
    local c = cost[i]
    if c < 0 and (riv[i] > 0 or (fd and fd[i] > 0)) and land[i] == 1 and occ[i] == 0 then c = k end
    sc[i] = c
  end
  -- (the border stays closed: A* has no bounds checks, D-086 - water crossing the map's edge is not
  -- a way off it)
  local N = s.N
  for q = 1, N do
    sc[q], sc[(N - 1) * N + q], sc[(q - 1) * N + 1], sc[q * N] = -1, -1, -1, -1
  end
  e.ver = ver
  SWIM[s] = e
  return sc
end

function M.path(s, x0, y0, x1, y1, temper, out, water, swim)
  if water then
    -- a boat's way over the water: the same search on the water's cost field
    local land = s.cost
    s.cost = s.wcost
    local r = M.path(s, x0, y0, x1, y1, 3, out, false)
    s.cost = land
    return r
  end
  if swim then
    local land = s.cost
    s.cost = swim_cost(s)
    local r = M.path(s, x0, y0, x1, y1, 8, out, false)
    s.cost = land
    return r
  end
  out = out or { x = {}, y = {}, cum = {} }
  local a = mapm.tile_of(s, x0, y0)
  local b = mapm.tile_of(s, x1, y1)
  local xs, ys, cum = out.x, out.y, out.cum
  if a == 0 or b == 0 then return nil end
  do
    -- (a start on the map's closed border searches from the tile inside it)
    local N = s.N
    local IX, IY = ixy(N)
    local ix, iy = IX[a], IY[a]
    if ix == 1 or iy == 1 or ix == N or iy == N then
      ix = ix < 2 and 2 or (ix > N - 1 and N - 1 or ix)
      iy = iy < 2 and 2 or (iy > N - 1 and N - 1 or iy)
      a = (iy - 1) * N + ix
    end
  end
  local wps
  if a ~= b then
    local NN = s.N * s.N
    local key = (a - 1) * NN + (b - 1) + (temper or 0) * NN * NN
    wps = cache_get(s, key)
    if wps ~= nil then
      M.stats.cache_hits = M.stats.cache_hits + 1
    elseif nav_short(s, a, b) then
      -- a short walk across open ground (or along one lane) needs no search: straight there
      wps = { a, b, n = 2 }
      M.stats.direct = (M.stats.direct or 0) + 1
      cache_put(s, key, wps)
    else
      local k = astar(s, a, b, temper or 0, TILES)
      if k == 0 then
        M.stats.fallbacks = M.stats.fallbacks + 1
        wps = false
      else
        wps = {}
        local m = pull(s, TILES, k, wps)
        wps.n = m
      end
      cache_put(s, key, wps)
    end
    if wps == false then return nil end
  end
  -- waypoints: exact start, the pulled tile centres between, exact end
  local n = 1
  xs[1], ys[1] = x0, y0
  if wps then
    for k = 2, wps.n - 1 do
      n = n + 1
      xs[n], ys[n] = mapm.tile_xy(s, wps[k])
    end
  end
  n = n + 1
  xs[n], ys[n] = x1, y1
  cum[1] = 0
  local L = 0
  for k = 2, n do
    local dx, dy = xs[k] - xs[k - 1], ys[k] - ys[k - 1]
    L = L + sqrt(dx * dx + dy * dy)
    cum[k] = L
  end
  out.n, out.len = n, L
  for k = n + 1, #xs do xs[k], ys[k], cum[k] = nil, nil, nil end
  return out
end

-- Position along a path at distance d (clamped); returns x, y, and the segment's direction.
function M.at(path, d)
  local n = path.n
  local xs, ys, cum = path.x, path.y, path.cum
  if d <= 0 or n < 2 then
    local dx, dy = 0, 0
    if n >= 2 then dx, dy = xs[2] - xs[1], ys[2] - ys[1] end
    return xs[1], ys[1], dx, dy
  end
  if d >= path.len then
    return xs[n], ys[n], xs[n] - xs[n - 1], ys[n] - ys[n - 1]
  end
  -- binary search for the segment
  local lo, hi = 1, n
  while hi - lo > 1 do
    local mid = floor((lo + hi) / 2)
    if cum[mid] <= d then lo = mid else hi = mid end
  end
  local seg = cum[hi] - cum[lo]
  local f = seg > 0 and (d - cum[lo]) / seg or 0
  local dx, dy = xs[hi] - xs[lo], ys[hi] - ys[lo]
  return xs[lo] + dx * f, ys[lo] + dy * f, dx, dy
end

-- Straight-line distance.
function M.dist(x0, y0, x1, y1)
  local dx, dy = x1 - x0, y1 - y0
  return sqrt(dx * dx + dy * dy)
end

return M
