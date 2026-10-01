-- Render planet geometry (Stage 1.8). Pure Lua 5.1, no Defold APIs, so it runs headless too.
--
-- The sim grid is coarse (SIM_LEVEL 3 = 642 cells). The view hides that with a finer render
-- icosphere (level 5 = 10,242 vertices): every render vertex maps to the sim triangle that
-- contains it (barycentric weights over 3 sim cells). Land/ocean is decided per render vertex
-- from an interpolated signed "coast field" (+ land, - ocean) plus noise, so coastlines are
-- organic but still follow the sim cells (a one-cell island keeps its sign at its centre).
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
local grid = require("sim.grid")
local vnoise = require("game.view.vnoise")

local P = {}

P.R = 10            -- planet radius in world units (06_PRESENTATION.md §1)
P.EXAG = 0.13       -- world units per km above sea level (exaggerated but soft relief)
P.LEVEL = 5         -- render icosphere level
P.CHUNK_LEVEL = 1   -- prop chunks = ancestor faces at this level (level 1 -> 80 chunks)
P.COAST_NOISE = 0.5 -- coast-field noise amplitude (coast field is +-0.6 at coastal cells)
P.COAST_FREQ = 7    -- noise lattice cells per unit length on the unit sphere
P.HILLS_KM = 0.45   -- rolling hills amplitude on land (km)
P.RIDGE_KM = 1.0    -- extra ridged relief on mountains (km, scaled by elevation)

local sqrt, floor, max, min, abs = math.sqrt, math.floor, math.max, math.min, math.abs

local function smoothstep(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end
P.smoothstep = smoothstep

-- Sim triangle containing unit vector (x,y,z) around nearest cell c0. Returns a, b, c, wa, wb, wc.
local function locate(sg, c0, x, y, z)
  local SX, SY, SZ = sg.X, sg.Y, sg.Z
  local nbr, cnt = sg.nbr, sg.nbr_count
  local base, kn = c0 * 6 - 6, cnt[c0]
  local px, py, pz = SX[c0], SY[c0], SZ[c0]
  local bestq, ba, bb, b0, b1, b2 = -1e9, c0, c0, 1, 0, 0
  for k = 1, kn do
    local a = nbr[base + k]
    local b = nbr[base + (k % kn) + 1]
    local ax, ay, az = SX[a], SY[a], SZ[a]
    local bx, by, bz = SX[b], SY[b], SZ[b]
    local w0 = x * (ay * bz - az * by) + y * (az * bx - ax * bz) + z * (ax * by - ay * bx)
    local w1 = x * (by * pz - bz * py) + y * (bz * px - bx * pz) + z * (bx * py - by * px)
    local w2 = x * (py * az - pz * ay) + y * (pz * ax - px * az) + z * (px * ay - py * ax)
    local s = w0 + w1 + w2
    if s < 0 then w0, w1, w2, s = -w0, -w1, -w2, -s end
    if s > 1e-12 then
      local q = min(w0, min(w1, w2)) / s
      if q > bestq then
        bestq, ba, bb, b0, b1, b2 = q, a, b, w0 / s, w1 / s, w2 / s
      end
    end
  end
  if b0 < 0 then b0 = 0 end
  if b1 < 0 then b1 = 0 end
  if b2 < 0 then b2 = 0 end
  local t = b0 + b1 + b2
  return c0, ba, bb, b0 / t, b1 / t, b2 / t
end

--- Builds the render planet for world W. opts: level, step(frac) progress callback (may yield).
function P.build(W, opts)
  opts = opts or {}
  local step = opts.step or function() end
  local level = opts.level or P.LEVEL
  local R, EXAG = P.R, P.EXAG
  local sg = W.grid
  local elev, sea = W.static.elev, W.G.sea_level
  local ocean, cdist = W.geo.ocean, W.geo.coast_dist
  local N = vnoise.new((W.seed or 1) * 7 + 3)

  local rg = grid.build(level, { neighbours = false })
  step(0.1)
  local n, faces = rg.n, rg.faces
  local nf = #faces / 3
  local X, Y, Z = rg.X, rg.Y, rg.Z

  -- A. sim triangle + barycentric weights per render vertex
  local ca, cb, cc, wa, wb, wc = {}, {}, {}, {}, {}, {}
  local prev = 1
  for v = 1, n do
    local x, y, z = X[v], Y[v], Z[v]
    local c0 = grid.nearest(sg, x, y, z, prev)
    prev = c0
    ca[v], cb[v], cc[v], wa[v], wb[v], wc[v] = locate(sg, c0, x, y, z)
    if v % 2048 == 0 then step(0.1 + 0.25 * v / n) end
  end

  -- B. render adjacency (for smoothing the coast field)
  local rn, rcnt = {}, {}
  for v = 1, n do rcnt[v] = 0 end
  local function add(a, b)
    local base, k = a * 6 - 6, rcnt[a]
    for i = 1, k do if rn[base + i] == b then return end end
    if k < 6 then rcnt[a] = k + 1; rn[base + k + 1] = b end
  end
  for f = 1, nf do
    local i = f * 3 - 2
    local a, b, c = faces[i], faces[i + 1], faces[i + 2]
    add(a, b); add(b, a); add(b, c); add(c, b); add(c, a); add(a, c)
  end
  step(0.4)

  -- C. coast field
  local scell = {}
  for c = 1, W.n do
    local mag = 0.6 + 0.4 * min(1, max(0, (cdist[c] - 1) / 2))
    scell[c] = ocean[c] == 1 and -mag or mag
  end
  local s = {}
  for v = 1, n do
    s[v] = wa[v] * scell[ca[v]] + wb[v] * scell[cb[v]] + wc[v] * scell[cc[v]]
  end
  local tmp = {}
  for _ = 1, 2 do
    for v = 1, n do
      local base, k, sum = v * 6 - 6, rcnt[v], 0
      for i = 1, k do sum = sum + s[rn[base + i]] end
      tmp[v] = 0.5 * s[v] + 0.5 * sum / k
    end
    s, tmp = tmp, s
  end
  local fbm, ridged = vnoise.fbm, vnoise.ridged
  local cn, cf = P.COAST_NOISE, P.COAST_FREQ
  local n1 = {}
  for v = 1, n do
    local x, y, z = X[v], Y[v], Z[v]
    local sv = s[v] + cn * fbm(N, x, y, z, cf, 3, 0.55)
    -- consistency: land needs a land sim cell in its triangle, water needs an ocean cell
    local has_land = (ocean[ca[v]] == 0 and wa[v] > 0.01) or (ocean[cb[v]] == 0 and wb[v] > 0.01)
      or (ocean[cc[v]] == 0 and wc[v] > 0.01)
    local has_ocean = (ocean[ca[v]] == 1 and wa[v] > 0.01) or (ocean[cb[v]] == 1 and wb[v] > 0.01)
      or (ocean[cc[v]] == 1 and wc[v] > 0.01)
    if sv > 0 and not has_land then sv = -0.02 end
    if sv <= 0 and not has_ocean then sv = 0.02 end
    if sv > -0.001 and sv <= 0 then sv = -0.001 end
    s[v] = sv
    n1[v] = fbm(N, x + 5.1, y, z, 22, 2, 0.5)
  end
  step(0.5)

  -- D. heights, restricted weights, positions
  local h, va, vb, vc, vwa, vwb, vwc = {}, {}, {}, {}, {}, {}, {}
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
    walk = 1,
  }
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
