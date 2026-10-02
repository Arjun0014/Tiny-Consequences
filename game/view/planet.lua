-- Render planet geometry (Stage 1.8). Pure Lua 5.1, no Defold APIs, so it runs headless too.
--
-- The sim grid is coarse (SIM_LEVEL 3 = 642 cells). The view hides that with a finer render
-- icosphere (level 5 = 10,242 vertices): every render vertex maps to the sim triangle that
-- contains it (barycentric weights over 3 sim cells). Land/ocean is decided per render vertex
-- from an interpolated signed "coast field" (+ land, - ocean) plus noise, so coastlines are
-- organic but still follow the sim cells (a one-cell island keeps its sign at its centre). That
-- field is computed by sim/surface.lua (D-085), which the settlements' local maps sample too.
-- Heights: land cells' elevation interpolated over land cells only, ramped to 0 at the
-- coastline, plus hills/ridge noise; water depth likewise over ocean cells.
--
-- Output (flat arrays, 1-based):
--   n, nf, faces[3*nf], X/Y/Z (unit), px/py/pz (displaced position), nx/ny/nz (smooth normal),
--   h (km above sea, negative = depth), s (coast field; >0 land), n1 (colour noise -0.5..0.5),
--   va/vb/vc + vwa/vwb/vwc (sim cells + weights restricted to land cells for land vertices,
--   ocean cells for water vertices), tland[f] (1 if all three corners are land),
--   tc1/tc2/tc3 + tw1/tw2/tw3 (per-face blended sim cells for props), tchunk[f],
--   chunks[k] = { id, faces = {...}, cells = {...}, cx, cy, cz (unit centre), cos_radius,
--                 land_faces, min/max bounds }
local surface = require("sim.surface")
local vnoise = require("sim.noise")

local P = {}

P.R = 10            -- planet radius in world units (06_PRESENTATION.md §1)
P.EXAG = 0.13       -- world units per km above sea level (exaggerated but soft relief)
P.LEVEL = 5         -- render icosphere level
P.CHUNK_LEVEL = 1   -- prop chunks = ancestor faces at this level (level 1 -> 80 chunks)
P.HILLS_KM = 0.45   -- rolling hills amplitude on land (km)
P.RIDGE_KM = 1.6    -- extra ridged relief on mountains (km, scaled by elevation; S14 was 1.0)
-- (S14) mountains with a silhouette: high ground is lifted (a miniature planet's exaggeration of
-- its ranges) - nothing below MTN_E0 km, MTN_LIFT km more at MTN_E1 and above
P.MTN_E0, P.MTN_E1, P.MTN_LIFT, P.MTN_PEAK = 0.8, 3.0, 1.2, 3.2
-- (the drawn relief above a cell's mean ground is about 1 / REAL_K times the real one: snow and the
-- tree line go by the real height, game/render/shaders/planet.vp and propgen)
P.REAL_K = 0.35
-- (S14) valleys: the land along every river course is carved down toward its bed (geo.bed: the
-- drainage basins' beds fall all the way to the sea) - a flat floodplain FP_K x the river's half-
-- width (+ FP_0) either side, then walls rising over VALLEY_W (+ VALLEY_DK per km of depth)
P.FP_K, P.FP_0, P.VALLEY_W, P.VALLEY_DK = 2.0, 0.07, 0.30, 0.12

local sqrt, floor, max, min, abs = math.sqrt, math.floor, math.max, math.min, math.abs

local function smoothstep(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end
P.smoothstep = smoothstep

--- Builds the render planet for world W. opts: level, step(frac) progress callback (may yield).
function P.build(W, opts)
  opts = opts or {}
  local step = opts.step or function() end
  local level = opts.level or P.LEVEL
  local R, EXAG = P.R, P.EXAG
  local elev, sea = W.static.elev, W.G.sea_level
  local ocean = W.geo.ocean

  -- A-C. the render icosphere and its coast field: the shared surface (sim/surface.lua, D-085),
  -- the same field the settlements' local maps take their land and sea from
  local G = surface.get(W, { level = level, step = step })
  local n, faces, nf = G.n, G.faces, G.nf
  local X, Y, Z = G.X, G.Y, G.Z
  local ca, cb, cc, wa, wb, wc = G.ca, G.cb, G.cc, G.wa, G.wb, G.wc
  local rn, rcnt, s, n1 = G.rn, G.rcnt, G.s, G.n1
  local N = G.noise
  local fbm, ridged = vnoise.fbm, vnoise.ridged
  step(0.5)

  -- D. heights, restricted weights, positions
  local h, va, vb, vc, vwa, vwb, vwc = {}, {}, {}, {}, {}, {}, {}
  local ecell = {}
  local px, py, pz = {}, {}, {}
  local hills_km, ridge_km = P.HILLS_KM, P.RIDGE_KM
  for v = 1, n do
    local a, b, c = ca[v], cb[v], cc[v]
    local x, y, z = X[v], Y[v], Z[v]
    local land = s[v] > 0
    local want = land and 0 or 1
    local ua = ocean[a] == want and wa[v] or 0
    local ub = ocean[b] == want and wb[v] or 0
    local uc = ocean[c] == want and wc[v] or 0
    local t = ua + ub + uc
    if t <= 0 then ua, ub, uc, t = wa[v], wb[v], wc[v], 1 end
    ua, ub, uc = ua / t, ub / t, uc / t
    va[v], vb[v], vc[v], vwa[v], vwb[v], vwc[v] = a, b, c, ua, ub, uc
    local e = ua * (elev[a] - sea) + ub * (elev[b] - sea) + uc * (elev[c] - sea)
    local hv
    if land then
      if e < 0.02 then e = 0.02 end
      local ramp = smoothstep(0, 0.35, s[v])
      local hills = fbm(N, x, y, z, 16, 3, 0.5) * hills_km
      local mount = 0
      if e > 0.6 then mount = ridged(N, x, y, z, 9, 3) * ridge_km * min(1, (e - 0.6) / 1.5) end
      -- (S14) the range lifted, its ridges sharper toward the crest
      local up = smoothstep(P.MTN_E0, P.MTN_E1, e)
      if up > 0 then
        -- a lifted range with sharp crests: ridged noise raised to a power peaks narrowly
        local r9 = ridged(N, x, y, z, 9, 3)
        mount = mount + P.MTN_LIFT * up * (0.6 + 0.4 * ridged(N, x, y, z, 4, 2)) + P.MTN_PEAK * up * r9 * r9 * r9
      end
      ecell[v] = e
      hv = (e + hills * (0.4 + 0.6 * ramp) + mount) * ramp
      if hv < 0.004 then hv = 0.004 end
    else
      local d = -e
      if d < 0.05 then d = 0.05 end
      local ramp = smoothstep(0, 0.5, -s[v])
      hv = -(d * ramp)
      if hv > -0.004 then hv = -0.004 end
    end
    h[v] = hv
    local r = R + (hv > 0 and hv * EXAG or 0)
    px[v], py[v], pz[v] = x * r, y * r, z * r
    if v % 2048 == 0 then step(0.5 + 0.2 * v / n) end
  end

  -- D2. (S14) river valleys and floodplains, and each vertex's nearest river for the props
  local rdist, rhw = P.carve_valleys(W, G, h, ecell)
  for v = 1, n do
    local r = R + (h[v] > 0 and h[v] * EXAG or 0)
    px[v], py[v], pz[v] = X[v] * r, Y[v] * r, Z[v] * r
  end
  step(0.75)

  -- E. smooth normals (area-weighted face normals; water vertices radial)
  local nx, ny, nz = {}, {}, {}
  for v = 1, n do nx[v], ny[v], nz[v] = 0, 0, 0 end
  for f = 1, nf do
    local i = f * 3 - 2
    local a, b, c = faces[i], faces[i + 1], faces[i + 2]
    local ux, uy, uz = px[b] - px[a], py[b] - py[a], pz[b] - pz[a]
    local wx, wy, wz = px[c] - px[a], py[c] - py[a], pz[c] - pz[a]
    local fx, fy, fz = uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx
    -- faces are CCW from outside; guard against inverted orientation
    if fx * px[a] + fy * py[a] + fz * pz[a] < 0 then fx, fy, fz = -fx, -fy, -fz end
    nx[a], ny[a], nz[a] = nx[a] + fx, ny[a] + fy, nz[a] + fz
    nx[b], ny[b], nz[b] = nx[b] + fx, ny[b] + fy, nz[b] + fz
    nx[c], ny[c], nz[c] = nx[c] + fx, ny[c] + fy, nz[c] + fz
  end
  for v = 1, n do
    if h[v] <= 0 then
      nx[v], ny[v], nz[v] = X[v], Y[v], Z[v]
    else
      local l = sqrt(nx[v] * nx[v] + ny[v] * ny[v] + nz[v] * nz[v])
      if l > 0 then nx[v], ny[v], nz[v] = nx[v] / l, ny[v] / l, nz[v] / l
      else nx[v], ny[v], nz[v] = X[v], Y[v], Z[v] end
    end
  end
  step(0.8)

  -- F. faces: land flag, blended prop cells, chunks
  local tland, tchunk = {}, {}
  local tc1, tc2, tc3, tw1, tw2, tw3 = {}, {}, {}, {}, {}, {}
  local div = 4 ^ (level - P.CHUNK_LEVEL)
  local chunks = {}
  local mc, mw = { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0, 0, 0, 0 }
  local corner, top = { 0, 0, 0 }, { 0, 0, 0 }
  for f = 1, nf do
    local i = f * 3 - 2
    local a, b, c = faces[i], faces[i + 1], faces[i + 2]
    local k = floor((f - 1) / div) + 1
    tchunk[f] = k
    local ch = chunks[k]
    if not ch then
      ch = { id = k, faces = {}, cells = {}, cellset = {}, cx = 0, cy = 0, cz = 0, land_faces = 0 }
      chunks[k] = ch
    end
    ch.faces[#ch.faces + 1] = f
    ch.cx = ch.cx + X[a] + X[b] + X[c]
    ch.cy = ch.cy + Y[a] + Y[b] + Y[c]
    ch.cz = ch.cz + Z[a] + Z[b] + Z[c]
    local island = s[a] > 0 and s[b] > 0 and s[c] > 0
    tland[f] = island and 1 or 0
    -- merge the corners' restricted weights into <= 3 cells
    local m = 0
    corner[1], corner[2], corner[3] = a, b, c
    for qq = 1, 3 do
      local v = corner[qq]
      for q = 1, 3 do
        local cell = q == 1 and va[v] or (q == 2 and vb[v] or vc[v])
        local w = (q == 1 and vwa[v] or (q == 2 and vwb[v] or vwc[v])) / 3
        if w > 0 and (not island or ocean[cell] == 0) then
          local found = false
          for j = 1, m do
            if mc[j] == cell then mw[j] = mw[j] + w; found = true; break end
          end
          if not found then m = m + 1; mc[m] = cell; mw[m] = w end
        end
      end
    end
    -- top 3 by weight (selection)
    local r1, r2, r3, s1, s2, s3 = 0, 0, 0, 0, 0, 0
    for j = 1, m do
      local w = mw[j]
      if w > s1 then r3, s3, r2, s2, r1, s1 = r2, s2, r1, s1, mc[j], w
      elseif w > s2 then r3, s3, r2, s2 = r2, s2, mc[j], w
      elseif w > s3 then r3, s3 = mc[j], w end
    end
    if r1 == 0 then r1, s1 = va[a], 1 end
    if r2 == 0 then r2 = r1 end
    if r3 == 0 then r3 = r1 end
    local t = s1 + s2 + s3
    tc1[f], tc2[f], tc3[f] = r1, r2, r3
    tw1[f], tw2[f], tw3[f] = s1 / t, s2 / t, s3 / t
    if island then
      ch.land_faces = ch.land_faces + 1
      top[1], top[2], top[3] = r1, r2, r3
      for q = 1, 3 do
        local cell = top[q]
        if not ch.cellset[cell] then ch.cellset[cell] = true; ch.cells[#ch.cells + 1] = cell end
      end
    end
  end
  for k = 1, #chunks do
    local ch = chunks[k]
    local l = sqrt(ch.cx * ch.cx + ch.cy * ch.cy + ch.cz * ch.cz)
    ch.cx, ch.cy, ch.cz = ch.cx / l, ch.cy / l, ch.cz / l
    local cosr = 1
    for _, f in ipairs(ch.faces) do
      local i = f * 3 - 2
      for q = 0, 2 do
        local v = faces[i + q]
        local d = X[v] * ch.cx + Y[v] * ch.cy + Z[v] * ch.cz
        if d < cosr then cosr = d end
      end
    end
    ch.cos_radius = cosr
    table.sort(ch.cells)
    ch.cellset = nil
  end
  step(1)

  return {
    level = level, R = R, EXAG = EXAG, n = n, nf = nf, faces = faces,
    X = X, Y = Y, Z = Z, px = px, py = py, pz = pz, nx = nx, ny = ny, nz = nz,
    h = h, s = s, n1 = n1, va = va, vb = vb, vc = vc, vwa = vwa, vwb = vwb, vwc = vwc,
    tland = tland, tchunk = tchunk, tc1 = tc1, tc2 = tc2, tc3 = tc3, tw1 = tw1, tw2 = tw2, tw3 = tw3,
    chunks = chunks, sea_level = sea, noise = N, seed = W.seed, rn = rn, rcnt = rcnt,
    walk = 1, ecell = ecell, rdist = rdist, rhw = rhw,
  }
end

-- (S14) Carve the land along every river course toward the river's bed (W.geo.bed, falling all
-- the way to the sea in v3 worlds; elsewhere the cells' own elevations), flattening a floodplain
-- either side and raising walls beyond: rivers run in the valleys they made. Returns, per render
-- vertex, the distance to the nearest course (world units, 1e9 when none near) and that river's
-- half-width (for the props: no trees in the channel and on the floodplain).
function P.carve_valleys(W, G, h, ecell)
  local n, R = G.n, P.R
  local X, Y, Z, rn, rcnt = G.X, G.Y, G.Z, G.rn, G.rcnt
  local rdist, rhw, rbed = {}, {}, {}
  for v = 1, n do rdist[v] = 1e9; rhw[v] = 0; rbed[v] = 0 end
  local geo = W.geo
  local down, ocean = geo.downhill, geo.ocean
  local elev, sea = W.static.elev, W.G.sea_level
  local bed = geo.bed
  local wd = surface.river_widths(W)
  local g = W.grid
  local SUB = surface.RIVER_SUB
  local Q = {}
  local seen, stamp = {}, 0
  for v = 1, n do seen[v] = 0 end
  local queue = {}
  local function bed_of(c)
    if ocean[c] == 1 then return 0 end
    if bed then return bed[c] end
    return elev[c] - sea
  end
  for c = 1, W.n do
    local d = down[c]
    if ocean[c] == 0 and wd[c] > 0 and d and d ~= 0 then
      local w0, w1 = surface.link_widths(wd, c, d)
      surface.river_path(G, c, g.X[c], g.Y[c], g.Z[c], g.X[d], g.Y[d], g.Z[d], Q)
      local b0, b1 = bed_of(c), bed_of(d)
      for k = 0, SUB - 1 do
        local A, B = Q[k], Q[k + 1]
        if not (A.sea and B.sea) then
          local hw = surface.river_at(c, k, w0, w1)
          local reach = (hw * P.FP_K + P.FP_0 + P.VALLEY_W + P.VALLEY_DK * 2) / R
          local ax, ay, az = A.x, A.y, A.z
          local ex, ey, ez = B.x - ax, B.y - ay, B.z - az
          local L2 = ex * ex + ey * ey + ez * ez
          -- the vertices near this stretch: a walk out from the one nearest its middle
          stamp = stamp + 1
          local v0 = surface.nearest(G, ax + ex * 0.5, ay + ey * 0.5, az + ez * 0.5)
          local qn, head = 1, 1
          queue[1] = v0
          seen[v0] = stamp
          while head <= qn do
            local v = queue[head]; head = head + 1
            local vx, vy, vz = X[v], Y[v], Z[v]
            local t = L2 > 0 and ((vx - ax) * ex + (vy - ay) * ey + (vz - az) * ez) / L2 or 0
            if t < 0 then t = 0 elseif t > 1 then t = 1 end
            local dx, dy, dz = vx - ax - ex * t, vy - ay - ey * t, vz - az - ez * t
            local dd = sqrt(dx * dx + dy * dy + dz * dz)
            if dd <= reach or v == v0 then
              local dw = dd * R
              if dw < rdist[v] then
                rdist[v], rhw[v] = dw, hw
                rbed[v] = b0 + (b1 - b0) * (k + t) / SUB
              end
              local base = v * 6 - 6
              for q = 1, rcnt[v] do
                local j = rn[base + q]
                if seen[j] ~= stamp then seen[j] = stamp; qn = qn + 1; queue[qn] = j end
              end
            end
          end
        end
      end
    end
  end
  -- the carving: only ever lower the land, never below the shore
  for v = 1, n do
    local d = rdist[v]
    local hv = h[v]
    if d < 1e8 and hv > 0 then
      local fp = rhw[v] * P.FP_K + P.FP_0
      local floor_h = rbed[v] + 0.02
      if floor_h < 0.006 then floor_h = 0.006 end
      if floor_h < hv then
        local wall = P.VALLEY_W + P.VALLEY_DK * min(4, hv - floor_h)
        local target = floor_h + (hv - floor_h) * smoothstep(fp, fp + wall, d)
        if target < hv then h[v] = target end
      end
    end
  end
  return rdist, rhw
end

--- Nearest render vertex to unit direction (x,y,z) by greedy walk (from `start`, else the
-- last hit; movers pass their own previous vertex so walks stay a step or two long).
function P.nearest(PL, x, y, z, start)
  local X, Y, Z, rn, rcnt = PL.X, PL.Y, PL.Z, PL.rn, PL.rcnt
  local v = start or PL.walk
  local best = X[v] * x + Y[v] * y + Z[v] * z
  while true do
    local moved = false
    local base = v * 6 - 6
    for k = 1, rcnt[v] do
      local j = rn[base + k]
      local d = X[j] * x + Y[j] * y + Z[j] * z
      if d > best then best, v, moved = d, j, true end
    end
    if not moved then break end
  end
  PL.walk = v
  return v
end

--- Approximate surface radius (land height or sea level) at unit direction (x,y,z).
-- Returns radius, vertex (pass the vertex back as `start` next time).
function P.surface_radius(PL, x, y, z, start)
  local v = P.nearest(PL, x, y, z, start)
  local h = PL.h[v]
  return PL.R + (h > 0 and h * PL.EXAG or 0), v
end

--- Exact surface radius along unit direction (x, y, z): the ray from the planet centre hits one of
-- the render faces around the nearest vertex (v3 settlement placement; smooth for walkers).
function P.surface_exact(PL, x, y, z, start)
  local v = P.nearest(PL, x, y, z, start)
  local vf = PL.vfaces
  if not vf then
    vf = {}
    local faces = PL.faces
    for f = 1, PL.nf do
      local i = f * 3 - 2
      for q = 0, 2 do
        local a = faces[i + q]
        local l = vf[a]
        if not l then l = {}; vf[a] = l end
        l[#l + 1] = f
      end
    end
    PL.vfaces = vf
  end
  local faces, px, py, pz = PL.faces, PL.px, PL.py, PL.pz
  local list = vf[v]
  for k = 1, #list do
    local i = list[k] * 3 - 2
    local a, b, c = faces[i], faces[i + 1], faces[i + 2]
    local ax, ay, az = px[a], py[a], pz[a]
    local e1x, e1y, e1z = px[b] - ax, py[b] - ay, pz[b] - az
    local e2x, e2y, e2z = px[c] - ax, py[c] - ay, pz[c] - az
    -- Moller-Trumbore with the ray origin at the centre, direction (x, y, z)
    local hx, hy, hz = y * e2z - z * e2y, z * e2x - x * e2z, x * e2y - y * e2x
    local det = e1x * hx + e1y * hy + e1z * hz
    if det > 1e-12 or det < -1e-12 then
      local inv = 1 / det
      local sx, sy, sz = -ax, -ay, -az
      local u = (sx * hx + sy * hy + sz * hz) * inv
      if u >= -1e-6 and u <= 1 + 1e-6 then
        local qx, qy, qz = sy * e1z - sz * e1y, sz * e1x - sx * e1z, sx * e1y - sy * e1x
        local w = (x * qx + y * qy + z * qz) * inv
        if w >= -1e-6 and u + w <= 1 + 1e-6 then
          local t = (e2x * qx + e2y * qy + e2z * qz) * inv
          if t > 0 then return t, v end
        end
      end
    end
  end
  return P.surface_radius(PL, x, y, z, v)
end

--- Surface radius at a unit direction's nearest render vertex (approximate; for placement).
function P.radius_at(PL, v)
  local h = PL.h[v]
  return PL.R + (h > 0 and h * PL.EXAG or 0)
end

return P
