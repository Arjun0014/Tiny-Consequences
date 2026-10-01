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
local HN, HF = {}, {}
local stamp = 0
M.stats = { searches = 0, expanded = 0, cache_hits = 0, fallbacks = 0 }
M.MAX_EXPAND = 9000

local function heap_push(n, node, f)
  n = n + 1
  local i = n
  while i > 1 do
    local p = floor(i / 2)
    local pf = HF[p]
    if pf < f or (pf == f and HN[p] < node) then break end
    HN[i], HF[i] = HN[p], pf
    i = p
  end
  HN[i], HF[i] = node, f
  return n
end

local function heap_pop(n)
  local top = HN[1]
  local ln, lf = HN[n], HF[n]
  n = n - 1
  local i = 1
  while true do
    local l = i * 2
    if l > n then break end
    local r = l + 1
    local c = l
    if r <= n and (HF[r] < HF[l] or (HF[r] == HF[l] and HN[r] < HN[l])) then c = r end
    if HF[c] > lf or (HF[c] == lf and HN[c] > ln) then break end
    HN[i], HF[i] = HN[c], HF[c]
    i = c
  end
  if n >= 1 then HN[i], HF[i] = ln, lf end
  return top, n
end

-- Line of sight between tile centres a and b: every tile on the line walkable and no costlier
-- than cmax.
local function los(s, a, b, cmax)
  local ax, ay = mapm.tile_xy(s, a)
  local bx, by = mapm.tile_xy(s, b)
  local dx, dy = bx - ax, by - ay
  local len = sqrt(dx * dx + dy * dy)
  local T = s.T
  local steps = floor(len / (T * 0.4)) + 1
  local cost = s.cost
  for k = 1, steps - 1 do
    local f = k / steps
    local i = mapm.tile_of(s, ax + dx * f, ay + dy * f)
    if i == 0 then return false end
    local c = cost[i]
    if c < 0 or c > cmax then return false end
  end
  return true
end

-- A* from tile a to tile b; fills `tiles` with the tile path (a .. b); returns its length or 0.
local function astar(s, a, b, temper, tiles)
  local C = cfgmod.V3
  local N = s.N
  local cost, lane = s.cost, s.lane
  stamp = stamp + 2
  local so, sc = stamp, stamp + 1
  local tk = 1 + temper * C.temper_extra
  local bx, by = mapm.tile_ixy(s, b)
  local hw = 0.75
  local n = 0
  G[a] = 0
  PAR[a] = 0
  ST_OPEN[a] = so
  local ax, ay = mapm.tile_ixy(s, a)
  local ddx, ddy = abs(ax - bx), abs(ay - by)
  n = heap_push(n, a, hw * (ddx + ddy + (SQ2 - 2) * min(ddx, ddy)))
  local expanded = 0
  local found = false
  while n > 0 do
    local cur
    cur, n = heap_pop(n)
    if ST_CLOSED[cur] ~= sc then
      ST_CLOSED[cur] = sc
      if cur == b then found = true; break end
      expanded = expanded + 1
      if expanded > M.MAX_EXPAND then break end
      local gc = G[cur]
      local iy = floor((cur - 1) / N) + 1
      local ix = cur - (iy - 1) * N
      for dy = -1, 1 do
        local ny = iy + dy
        if ny >= 1 and ny <= N then
          for dx = -1, 1 do
            local nx = ix + dx
            if (dx ~= 0 or dy ~= 0) and nx >= 1 and nx <= N then
              local j = (ny - 1) * N + nx
              local c = cost[j]
              if j == b and c < 0 then c = 1 end
              if c > 0 and ST_CLOSED[j] ~= sc then
                local ok = true
                local step = 1
                if dx ~= 0 and dy ~= 0 then
                  -- no corner cutting past blocked tiles
                  if cost[(iy - 1) * N + nx] < 0 or cost[(ny - 1) * N + ix] < 0 then ok = false end
                  step = SQ2
                end
                if ok then
                  if lane[j] == 0 then c = c * tk end
                  local g2 = gc + c * step
                  if ST_OPEN[j] ~= so or g2 < G[j] then
                    G[j] = g2
                    PAR[j] = cur
                    ST_OPEN[j] = so
                    local hx, hy = abs(nx - bx), abs(ny - by)
                    n = heap_push(n, j, g2 + hw * (hx + hy + (SQ2 - 2) * min(hx, hy)))
                  end
                end
              end
            end
          end
        end
      end
    end
  end
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

-- Waypoint tiles for a tile path: string pulling with a cost ceiling (the costliest tile of the
-- pulled stretch), so a path that keeps to a lane does not cut across the field beside it.
local function pull(s, tiles, k, out)
  local cost = s.cost
  local m = 1
  out[1] = tiles[1]
  local i = 1
  while i < k do
    local j = i + 1
    local cmax = cost[tiles[j]]
    if cmax < 0 then cmax = 1 end
    local best = j
    while j < k do
      local cj = cost[tiles[j + 1]]
      if cj < 0 then cj = 1 end
      local cm = cmax > cj and cmax or cj
      if los(s, tiles[i], tiles[j + 1], cm + 1e-9) then
        j = j + 1
        cmax = cm
        best = j
      else
        break
      end
    end
    m = m + 1
    out[m] = tiles[best]
    i = best
  end
  return m
end

-- (runtime only, never saved: keyed by the settlement table)
local CACHES = setmetatable({}, { __mode = "k" })

local function cache_get(s, key)
  local pc = CACHES[s]
  if not pc or pc.ver ~= s.ver then
    pc = { ver = s.ver, map = {}, n = 0 }
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
function M.path(s, x0, y0, x1, y1, temper, out)
  out = out or { x = {}, y = {}, cum = {} }
  local a = mapm.tile_of(s, x0, y0)
  local b = mapm.tile_of(s, x1, y1)
  local xs, ys, cum = out.x, out.y, out.cum
  if a == 0 or b == 0 then return nil end
  local wps
  if a ~= b then
    local NN = s.N * s.N
    local key = (a - 1) * NN + (b - 1) + (temper or 0) * NN * NN
    wps = cache_get(s, key)
    if wps ~= nil then
      M.stats.cache_hits = M.stats.cache_hits + 1
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
