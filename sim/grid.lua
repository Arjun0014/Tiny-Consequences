-- Icosphere graph (02_WORLD_MODEL.md §2). Pure Lua 5.1.
-- Cells are vertices of a subdivided icosahedron: level L has 10*4^L + 2 cells.
-- Planet axis is +Y (Defold is y-up): lat = asin(y), lon = atan2(z, x).
--
-- Returned grid (struct-of-arrays, 1-based):
--   n, level, X[], Y[], Z[], lat[], lon[], area[] (mean 1), nbr[] (flat, stride 6),
--   nbr_count[], faces[] (flat triangles, 3 per face), nfaces, spacing (mean angular
--   distance between neighbours, radians).

local grid = {}

local function normalize(x, y, z)
  local l = math.sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

local function build_icosphere(level)
  local phi = (1 + math.sqrt(5)) / 2
  local X, Y, Z = {}, {}, {}
  local base = {
    { -1, phi, 0 }, { 1, phi, 0 }, { -1, -phi, 0 }, { 1, -phi, 0 },
    { 0, -1, phi }, { 0, 1, phi }, { 0, -1, -phi }, { 0, 1, -phi },
    { phi, 0, -1 }, { phi, 0, 1 }, { -phi, 0, -1 }, { -phi, 0, 1 },
  }
  for i = 1, 12 do
    X[i], Y[i], Z[i] = normalize(base[i][1], base[i][2], base[i][3])
  end
  local F = {
    1, 12, 6, 1, 6, 2, 1, 2, 8, 1, 8, 11, 1, 11, 12,
    2, 6, 10, 6, 12, 5, 12, 11, 3, 11, 8, 7, 8, 2, 9,
    4, 10, 5, 4, 5, 3, 4, 3, 7, 4, 7, 9, 4, 9, 10,
    5, 10, 6, 3, 5, 12, 7, 3, 11, 9, 7, 8, 10, 9, 2,
  }
  local n = 12
  for _ = 1, level do
    local cache = {}
    local NF = {}
    local function mid(a, b)
      local lo, hi = a, b
      if lo > hi then lo, hi = hi, lo end
      local key = lo * 1048576 + hi
      local m = cache[key]
      if m then return m end
      n = n + 1
      X[n], Y[n], Z[n] = normalize((X[a] + X[b]) * 0.5, (Y[a] + Y[b]) * 0.5, (Z[a] + Z[b]) * 0.5)
      cache[key] = n
      return n
    end
    local k = 0
    for f = 1, #F, 3 do
      local a, b, c = F[f], F[f + 1], F[f + 2]
      local ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
      NF[k + 1], NF[k + 2], NF[k + 3] = a, ab, ca
      NF[k + 4], NF[k + 5], NF[k + 6] = b, bc, ab
      NF[k + 7], NF[k + 8], NF[k + 9] = c, ca, bc
      NF[k + 10], NF[k + 11], NF[k + 12] = ab, bc, ca
      k = k + 12
    end
    F = NF
  end
  return n, X, Y, Z, F
end

-- Build the grid. opts.neighbours (default true) sorts neighbours by angle (CCW from outside).
function grid.build(level, opts)
  opts = opts or {}
  local n, X, Y, Z, F = build_icosphere(level)
  local g = { level = level, n = n, X = X, Y = Y, Z = Z, faces = F, nfaces = #F / 3 }
  local lat, lon = {}, {}
  local asin, atan2 = math.asin, math.atan2
  for i = 1, n do
    local y = Y[i]
    if y > 1 then y = 1 elseif y < -1 then y = -1 end
    lat[i] = asin(y)
    lon[i] = atan2(Z[i], X[i])
  end
  g.lat, g.lon = lat, lon

  -- areas: 1/3 of each incident spherical-ish (flat) triangle area; normalised to mean 1
  local area = {}
  for i = 1, n do area[i] = 0 end
  for f = 1, #F, 3 do
    local a, b, c = F[f], F[f + 1], F[f + 2]
    local ux, uy, uz = X[b] - X[a], Y[b] - Y[a], Z[b] - Z[a]
    local vx, vy, vz = X[c] - X[a], Y[c] - Y[a], Z[c] - Z[a]
    local cx, cy, cz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    local t = math.sqrt(cx * cx + cy * cy + cz * cz) * 0.5 / 3
    area[a] = area[a] + t; area[b] = area[b] + t; area[c] = area[c] + t
  end
  local sum = 0
  for i = 1, n do sum = sum + area[i] end
  local mean = sum / n
  for i = 1, n do area[i] = area[i] / mean end
  g.area = area

  if opts.neighbours ~= false then
    -- adjacency from faces
    local adj = {}
    for i = 1, n do adj[i] = {} end
    local function add(a, b)
      local l = adj[a]
      for k = 1, #l do if l[k] == b then return end end
      l[#l + 1] = b
    end
    for f = 1, #F, 3 do
      local a, b, c = F[f], F[f + 1], F[f + 2]
      add(a, b); add(b, a); add(b, c); add(c, b); add(c, a); add(a, c)
    end
    local nbr, cnt = {}, {}
    local spacing_sum, spacing_n = 0, 0
    for i = 1, n do
      local l = adj[i]
      -- tangent basis at i
      local nx, ny, nz = X[i], Y[i], Z[i]
      local ax, ay, az = 0, 1, 0
      if math.abs(ny) > 0.9 then ax, ay, az = 1, 0, 0 end
      -- e1 = normalize(a - (a.n) n), e2 = n x e1
      local d = ax * nx + ay * ny + az * nz
      local e1x, e1y, e1z = normalize(ax - d * nx, ay - d * ny, az - d * nz)
      local e2x, e2y, e2z = ny * e1z - nz * e1y, nz * e1x - nx * e1z, nx * e1y - ny * e1x
      local ang = {}
      for k = 1, #l do
        local j = l[k]
        local dx, dy, dz = X[j] - nx, Y[j] - ny, Z[j] - nz
        ang[j] = atan2(dx * e2x + dy * e2y + dz * e2z, dx * e1x + dy * e1y + dz * e1z)
        local dot = X[j] * nx + Y[j] * ny + Z[j] * nz
        if dot > 1 then dot = 1 end
        spacing_sum = spacing_sum + math.acos(dot)
        spacing_n = spacing_n + 1
      end
      table.sort(l, function(p, q) return ang[p] < ang[q] end)
      cnt[i] = #l
      local base = i * 6 - 6
      for k = 1, 6 do nbr[base + k] = l[k] or 0 end
    end
    g.nbr, g.nbr_count = nbr, cnt
    g.spacing = spacing_sum / spacing_n
  end
  return g
end

-- Nearest cell to unit vector (x,y,z) by greedy walk over neighbours (needs neighbours).
function grid.nearest(g, x, y, z, start)
  local X, Y, Z, nbr, cnt = g.X, g.Y, g.Z, g.nbr, g.nbr_count
  local c = start or 1
  local best = X[c] * x + Y[c] * y + Z[c] * z
  while true do
    local moved = false
    local base = c * 6 - 6
    for k = 1, cnt[c] do
      local j = nbr[base + k]
      local d = X[j] * x + Y[j] * y + Z[j] * z
      if d > best then best, c, moved = d, j, true end
    end
    if not moved then return c end
  end
end

-- Great-circle angular distance between cells a and b (radians).
function grid.angle(g, a, b)
  local d = g.X[a] * g.X[b] + g.Y[a] * g.Y[b] + g.Z[a] * g.Z[b]
  if d > 1 then d = 1 elseif d < -1 then d = -1 end
  return math.acos(d)
end

-- Breadth-first ring distances from a source cell up to maxd steps (optionally filtered).
-- Returns list of cells and dist table (dist[c] = steps). Allocates: not for hot paths.
function grid.bfs(g, src, maxd, filter)
  local dist = { [src] = 0 }
  local list = { src }
  local head = 1
  local nbr, cnt = g.nbr, g.nbr_count
  while head <= #list do
    local c = list[head]; head = head + 1
    local d = dist[c]
    if d < maxd then
      local base = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[base + k]
        if dist[j] == nil and (filter == nil or filter(j)) then
          dist[j] = d + 1
          list[#list + 1] = j
        end
      end
    end
  end
  return list, dist
end

return grid
