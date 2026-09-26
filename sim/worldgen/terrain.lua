-- Worldgen terrain: plates, boundaries, continent/island masks, elevation, upwelling.
-- (02_WORLD_MODEL.md §2 "World generation requirements"). Deterministic per (seed, attempt).
local rng = require("sim.rng")
local noise = require("sim.worldgen.noise")
local cfgmod = require("sim.config")

local terrain = {}
local S = rng.S
local sqrt, abs, floor, min, max = math.sqrt, math.abs, math.floor, math.min, math.max

local BOUNDARY = { none = 0, convergent = 1, divergent = 2, transform = 3 }
terrain.BOUNDARY = BOUNDARY

-- Binary min-heap keyed by (cost, cell) for deterministic growth.
local function heap_push(h, cost, cell)
  local n = #h + 1
  h[n] = { cost, cell }
  while n > 1 do
    local p = floor(n / 2)
    local a, b = h[p], h[n]
    if a[1] < b[1] or (a[1] == b[1] and a[2] <= b[2]) then break end
    h[p], h[n] = b, a
    n = p
  end
end
local function heap_pop(h)
  local top = h[1]
  local last = h[#h]
  h[#h] = nil
  if #h > 0 then
    h[1] = last
    local i, n = 1, #h
    while true do
      local l, r = 2 * i, 2 * i + 1
      local s = i
      local function lt(x, y) return h[x][1] < h[y][1] or (h[x][1] == h[y][1] and h[x][2] < h[y][2]) end
      if l <= n and lt(l, s) then s = l end
      if r <= n and lt(r, s) then s = r end
      if s == i then break end
      h[i], h[s] = h[s], h[i]
      i = s
    end
  end
  return top
end

local function rand_unit(seed, stream, a, b)
  local z = rng.float(seed, stream, a, b, 1) * 2 - 1
  local t = rng.float(seed, stream, a, b, 2) * 2 * math.pi
  local r = sqrt(1 - z * z)
  return r * math.cos(t), z, r * math.sin(t)   -- y is the pole axis
end

-- Grow a landmass from `center` to `target` cells into free cells (mask[c]==0), never touching
-- cells of other masses (keeps ≥1 cell of ocean between masses). Returns list of cells.
local function grow(g, mask, id, center, target, seed, attempt, rough, forbid_polar)
  local X, Y, Z = g.X, g.Y, g.Z
  local nbr, cnt = g.nbr, g.nbr_count
  local cx, cy, cz = X[center], Y[center], Z[center]
  local cells = {}
  local seen = {}
  local h = {}
  local function cost(c)
    local d = X[c] * cx + Y[c] * cy + Z[c] * cz
    if d > 1 then d = 1 end
    local ang = math.acos(d)
    local nz = noise.fbm(seed, 300 + id * 7 + attempt * 31, X[c], Y[c], Z[c], 2.6, 4, 0.6)
    local pol = 0
    if forbid_polar and abs(Y[c]) > 0.9 then pol = 1 end
    return ang + rough * nz + pol
  end
  heap_push(h, 0, center)
  seen[center] = true
  while #cells < target and #h > 0 do
    local top = heap_pop(h)
    local c = top[2]
    -- reject if adjacent to another mass
    local ok = mask[c] == 0
    if ok then
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local m = mask[nbr[b + k]]
        if m ~= 0 and m ~= id then ok = false; break end
      end
    end
    if ok then
      mask[c] = id
      cells[#cells + 1] = c
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if not seen[j] then seen[j] = true; heap_push(h, cost(j), j) end
      end
    end
  end
  return cells
end

-- Generate terrain into W.static. Returns info table (continents, islands, plates, land_frac).
function terrain.generate(W, attempt)
  local g, n, seed = W.grid, W.n, W.seed
  local X, Y, Z = g.X, g.Y, g.Z
  local nbr, cnt = g.nbr, g.nbr_count
  local st = W.static
  local cfg = cfgmod.WORLDGEN
  local function u(a, b) return rng.float(seed, S.TERRAIN, attempt, a, b or 0) end

  -- 1. plates: warped Voronoi on the sphere, Euler-pole rotation velocities
  local np = cfg.plates_min + floor(u(1) * (cfg.plates_max - cfg.plates_min + 1))
  local px, py, pz, wx, wy, wz = {}, {}, {}, {}, {}, {}
  for p = 1, np do
    px[p], py[p], pz[p] = rand_unit(seed, S.PLATES, attempt, p)
    local ax, ay, az = rand_unit(seed, S.PLATES, attempt, p + 100)
    local speed = 0.5 + u(10 + p)
    wx[p], wy[p], wz[p] = ax * speed, ay * speed, az * speed
  end
  local plate = {}
  for c = 1, n do
    local x, y, z = X[c], Y[c], Z[c]
    local w1 = noise.fbm(seed, 200 + attempt, x, y, z, 1.5, 3)
    local w2 = noise.fbm(seed, 210 + attempt, x, y, z, 1.5, 3)
    local w3 = noise.fbm(seed, 220 + attempt, x, y, z, 1.5, 3)
    x, y, z = x + 0.6 * w1, y + 0.6 * w2, z + 0.6 * w3
    local best, bp = -1e9, 1
    for p = 1, np do
      local d = x * px[p] + y * py[p] + z * pz[p]
      if d > best then best, bp = d, p end
    end
    plate[c] = bp
  end
  -- boundary types from relative motion
  local boundary, bstrength = {}, {}
  for c = 1, n do
    boundary[c], bstrength[c] = 0, 0
    local p = plate[c]
    local b = c * 6 - 6
    local best = 0
    local btype = 0
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      local q = plate[j]
      if q ~= p then
        -- velocities v = w x pos
        local vx1 = wy[p] * Z[c] - wz[p] * Y[c]
        local vy1 = wz[p] * X[c] - wx[p] * Z[c]
        local vz1 = wx[p] * Y[c] - wy[p] * X[c]
        local vx2 = wy[q] * Z[j] - wz[q] * Y[j]
        local vy2 = wz[q] * X[j] - wx[q] * Z[j]
        local vz2 = wx[q] * Y[j] - wy[q] * X[j]
        local dx, dy, dz = X[j] - X[c], Y[j] - Y[c], Z[j] - Z[c]
        local dl = sqrt(dx * dx + dy * dy + dz * dz)
        local rv = ((vx1 - vx2) * dx + (vy1 - vy2) * dy + (vz1 - vz2) * dz) / dl
        local mag = abs(rv)
        if mag > best then
          best = mag
          if rv > 0.25 then btype = BOUNDARY.convergent
          elseif rv < -0.25 then btype = BOUNDARY.divergent
          else btype = BOUNDARY.transform end
        end
        if btype == 0 then btype = BOUNDARY.transform end
      end
    end
    boundary[c], bstrength[c] = btype, min(1, best / 1.5)
  end

  -- 2. continents and islands (growth from separated centres)
  local mask = {}
  for c = 1, n do mask[c] = 0 end
  local K = (u(20) < cfg.p_three_continents) and 3 or 2
  local centres = {}
  local tries = 0
  while #centres < K and tries < 400 do
    tries = tries + 1
    local c = 1 + floor(u(30, tries) * n)
    if abs(g.lat[c]) < cfg.continent_max_lat then
      local ok = true
      for i = 1, #centres do
        local d = X[c] * X[centres[i]] + Y[c] * Y[centres[i]] + Z[c] * Z[centres[i]]
        if math.acos(max(-1, min(1, d))) < cfg.continent_min_sep[K] then ok = false end
      end
      if ok then centres[#centres + 1] = c end
    end
  end
  if #centres < K then return nil, "centres" end
  local lo, hi = cfg.continent_frac[K][1], cfg.continent_frac[K][2]
  local continents = {}
  for i = 1, K do
    local frac = lo + (hi - lo) * u(40 + i)
    local target = floor(frac * n + 0.5)
    continents[i] = grow(g, mask, i, centres[i], target, seed, attempt, cfg.coast_roughness, true)
  end
  local ni = cfg.islands_min + floor(u(50) * (cfg.islands_max - cfg.islands_min + 1))
  local islands = {}
  tries = 0
  while #islands < ni and tries < 2000 do
    tries = tries + 1
    local c = 1 + floor(u(60, tries) * n)
    if mask[c] == 0 and abs(g.lat[c]) < 1.2 then
      -- must be ≥ 3 steps from any land
      local far = true
      local ring = { c }
      local seen = { [c] = true }
      for _ = 1, 3 do
        local nxt = {}
        for _, a in ipairs(ring) do
          local b = a * 6 - 6
          for k = 1, cnt[a] do
            local j = nbr[b + k]
            if not seen[j] then
              seen[j] = true
              if mask[j] ~= 0 then far = false end
              nxt[#nxt + 1] = j
            end
          end
        end
        ring = nxt
      end
      if far then
        local id = 100 + #islands + 1
        local size = cfg.island_size[1] + floor(u(70, tries) * (cfg.island_size[2] - cfg.island_size[1] + 1))
        size = max(1, floor(size * n / 2562 + 0.5))
        islands[#islands + 1] = grow(g, mask, id, c, size, seed, attempt, 0.3, false)
      end
    end
  end

  -- 3. elevation
  -- distance to the mask edge (land side and ocean side) by BFS
  local dist = {}
  local queue = {}
  for c = 1, n do
    dist[c] = 1e9
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      if (mask[nbr[b + k]] ~= 0) ~= (mask[c] ~= 0) then dist[c] = 1; queue[#queue + 1] = c; break end
    end
  end
  local head = 1
  while head <= #queue do
    local c = queue[head]; head = head + 1
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      if (mask[j] ~= 0) == (mask[c] ~= 0) and dist[j] > dist[c] + 1 then
        dist[j] = dist[c] + 1
        queue[#queue + 1] = j
      end
    end
  end
  -- scale distances to cell spacing so shapes hold across SIM_LEVEL
  local sc = g.spacing / 0.0755
  -- mountain influence from convergent land boundaries (spread 2 rings)
  local mtn = {}
  for c = 1, n do mtn[c] = 0 end
  for c = 1, n do
    if mask[c] ~= 0 and boundary[c] == BOUNDARY.convergent then
      local s = 0.6 + 0.4 * bstrength[c]
      if s > mtn[c] then mtn[c] = s end
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if 0.55 * s > mtn[j] then mtn[j] = 0.55 * s end
        local b2 = j * 6 - 6
        for k2 = 1, cnt[j] do
          local j2 = nbr[b2 + k2]
          if 0.25 * s > mtn[j2] then mtn[j2] = 0.25 * s end
        end
      end
    end
  end
  local elev = st.elev
  for c = 1, n do
    local x, y, z = X[c], Y[c], Z[c]
    local nz = noise.fbm(seed, 400 + attempt, x, y, z, 3.0, 4, 0.5)
    local d = dist[c] * sc
    if mask[c] ~= 0 then
      local interior = min(1, (d - 1) / 5)
      local ridged = 1 - abs(noise.fbm(seed, 450 + attempt, x, y, z, 2.5, 3, 0.5) * 2)
      local e = cfg.land_base + 0.55 * interior * (0.7 + nz) + 0.5 * nz
        + cfg.mountain_height * mtn[c] + cfg.ridge_height * max(0, ridged - 0.55) * interior
      if mask[c] > 100 then e = min(e, 0.35 + 0.2 * nz) + 0.05 end -- islands stay low
      elev[c] = max(0.02, min(3.9, e))
    else
      local deep = min(1, (d - 1) / 4)
      deep = deep * deep * (3 - 2 * deep)
      local e = -(0.18 + cfg.ocean_depth * deep) + 0.35 * nz * deep
      if boundary[c] == BOUNDARY.divergent then e = e + 1.1 * bstrength[c] * deep end
      if boundary[c] == BOUNDARY.convergent then e = e - 0.9 * bstrength[c] * deep end
      elev[c] = max(-4.9, min(-0.05, e))
    end
  end

  -- 4. upwelling / nutrient patchiness for ocean cells (static)
  local up = {}
  for c = 1, n do
    up[c] = 0
    if mask[c] == 0 then
      local p = noise.fbm(seed, 500 + attempt, X[c], Y[c], Z[c], 2.0, 3, 0.5) + 0.5
      local lat = abs(g.lat[c])
      local eq = (lat < 0.15) and 0.3 or 0
      local coastal = dist[c] <= 2 and 0.35 or 0
      up[c] = min(1, max(0, (p - 0.45) * 1.6) + eq + coastal)
    end
  end

  st.plate, st.boundary, st.bstrength, st.upwelling = plate, boundary, bstrength, up
  W.S.worldgen = { plates = np, continents_target = K, islands_target = ni, attempt = attempt }
  return { plates = np, K = K, islands = ni }
end

return terrain
