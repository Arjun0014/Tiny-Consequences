-- The planet's surface as it is drawn, as pure functions shared by the view and the sim (D-085).
-- Pure Lua 5.1, deterministic, no Defold APIs.
--
-- The sim's grid is coarse (f12: 1,442 cells); the view draws a finer render icosphere (level 5:
-- 10,242 vertices) whose land and water come from a coast field: per render vertex, the sim
-- cells' signed field (+ land, - ocean) interpolated over the sim triangle that contains it,
-- smoothed twice over the render mesh, plus noise, made consistent with the cells (land needs a
-- land cell in its triangle, water an ocean cell). The drawn coastline is where that field,
-- interpolated over a render triangle, crosses zero (planet.fp: s > 0 is land).
--
-- The settlements' local maps (sim/agents/map.lua) take their land and sea from the same field
-- at the same points (M.sample), so a boat rows only on drawn water and a field lies only on
-- drawn land. Rivers likewise: the view's ribbons and the local maps' channels and fords come
-- from M.river_widths and M.river_path.
--
-- M.get(W) builds once per world (a runtime cache, never saved: it is derived from the grid, the
-- geography's ocean flags and the seed).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local grid = require("sim.grid")
local noise = require("sim.noise")

local M = {}

M.LEVEL = 5          -- render icosphere level
M.COAST_NOISE = 0.5  -- coast-field noise amplitude (coast field is +-0.6 at coastal cells)
M.COAST_FREQ = 7     -- noise lattice cells per unit length on the unit sphere
M.R = 10             -- planet radius in world units (the view's and the local maps')

local sqrt, floor, max, min, abs, sin, pi = math.sqrt, math.floor, math.max, math.min, math.abs, dmath.sin, math.pi

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
M.locate = locate

--- Builds the render icosphere and its coast field for world W. opts: level, step(frac) progress
-- callback (may yield; called with 0.1 .. 0.5). Returns
--   { level, n, nf, faces, X, Y, Z, ca, cb, cc, wa, wb, wc (sim triangle + barycentric weights per
--     vertex), rn, rcnt (render adjacency, stride 6), s (coast field, > 0 land), n1 (colour noise),
--     noise (the value-noise instance the view's relief uses), walk }
function M.build(W, opts)
  opts = opts or {}
  local step = opts.step or function() end
  local level = opts.level or M.LEVEL
  local sg = W.grid
  local ocean, cdist = W.geo.ocean, W.geo.coast_dist
  local N = noise.new((W.seed or 1) * 7 + 3)

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
  local fbm = noise.fbm
  local cn, cf = M.COAST_NOISE, M.COAST_FREQ
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
  return { level = level, n = n, nf = nf, faces = faces, X = X, Y = Y, Z = Z,
    ca = ca, cb = cb, cc = cc, wa = wa, wb = wb, wc = wc, rn = rn, rcnt = rcnt, s = s, n1 = n1,
    noise = N, walk = 1, ocean = ocean }
end

-- (runtime only: one surface per world, keyed by the world table; rebuilt if its geography is)
local CACHE = setmetatable({}, { __mode = "k" })

--- The world's surface (built on first use; opts as M.build, used only when it is built).
function M.get(W, opts)
  local G = CACHE[W]
  if G and G.ocean == W.geo.ocean and G.level == ((opts and opts.level) or M.LEVEL) then return G end
  G = M.build(W, opts)
  CACHE[W] = G
  return G
end

--- Nearest render vertex to unit direction (x,y,z) by greedy walk (from `start`, else the last).
function M.nearest(G, x, y, z, start)
  local X, Y, Z, rn, rcnt = G.X, G.Y, G.Z, G.rn, G.rcnt
  local v = start or G.walk
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
  G.walk = v
  return v
end

local function vfaces(G)
  local vf = G.vf
  if vf then return vf end
  vf = {}
  local faces = G.faces
  for f = 1, G.nf do
    local i = f * 3 - 2
    for q = 0, 2 do
      local a = faces[i + q]
      local l = vf[a]
      if not l then l = {}; vf[a] = l end
      l[#l + 1] = f
    end
  end
  G.vf = vf
  return vf
end

--- The coast field at unit direction (x,y,z) as drawn: interpolated over the render triangle the
-- ray from the centre passes through (> 0 land). Returns s, the nearest vertex (pass it back as
-- `start`), and the triangle's corners and weights (a, b, c, ua, ub, uc).
function M.sample(G, x, y, z, start)
  local v = M.nearest(G, x, y, z, start)
  local list = vfaces(G)[v]
  local faces, X, Y, Z, S = G.faces, G.X, G.Y, G.Z, G.s
  for k = 1, #list do
    local i = list[k] * 3 - 2
    local a, b, c = faces[i], faces[i + 1], faces[i + 2]
    local ax, ay, az = X[a], Y[a], Z[a]
    local e1x, e1y, e1z = X[b] - ax, Y[b] - ay, Z[b] - az
    local e2x, e2y, e2z = X[c] - ax, Y[c] - ay, Z[c] - az
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
          if t > 0 then
            local ua = 1 - u - w
            return ua * S[a] + u * S[b] + w * S[c], v, a, b, c, ua, u, w
          end
        end
      end
    end
  end
  return S[v], v, v, v, v, 1, 0, 0
end

-- Rivers ---------------------------------------------------------------------------------------
-- Every land cell whose mean flow is in the upper part of the planet's range carries a river from
-- its centre to its downhill neighbour (W.geo.downhill), its width from sqrt(flow): tributaries
-- join into rivers that widen toward the sea; a flood widens it, a drought thins it. One sub-point
-- of every link is a ford: the bed spreads out and shallows there.

M.RIVER_SHARE = 0.22     -- share of land cells (by mean flow) that carry a visible river
M.RIVER_MIN_FLOW = 0.004 -- ... but never below this mean flow
M.RIVER_SUB = 7          -- sub-segments per cell-to-cell link
M.FORD_SPREAD = 0.5      -- a ford is this much wider ...
M.FORD_SHALLOW = 0.6     -- ... and this much shallower
M.WADE_W = 0.03          -- a bed this wide (world units) is waist-deep at the river's mean flow

--- Visible width per cell (0 = no river) from the current flow; fills and returns `out`.
-- (thr: the mean flow above which a land cell carries a river, returned second)
function M.river_widths(W, out)
  local geo, F = W.geo, W.F
  local fm = W.S.hydro and W.S.hydro.flow_mean
  local bank = W.S.hydro and W.S.hydro.bankfull
  out = out or {}
  local vals = {}
  for c = 1, W.n do
    out[c] = 0
    if geo.ocean[c] == 0 and fm then vals[#vals + 1] = fm[c] end
  end
  if #vals == 0 then return out, 1e9 end
  table.sort(vals)
  local thr = max(M.RIVER_MIN_FLOW, vals[max(1, floor(#vals * (1 - M.RIVER_SHARE)))])
  local top = vals[#vals]
  for c = 1, W.n do
    if geo.ocean[c] == 0 and fm[c] >= thr then
      local f = fm[c]
      -- floods swell the river, droughts thin it
      local now = F.flow[c]
      local k = 1
      if bank and bank[c] > 0 then k = min(2.2, max(0.45, (0.5 * now + 0.5 * f) / max(f, 1e-6))) end
      local rel = sqrt(f / top)
      out[c] = (0.018 + 0.07 * rel) * k
    end
  end
  return out, thr
end

--- Widths at both ends of link c -> d (a river running into a cell with none, or the sea, keeps
-- widening a little).
function M.link_widths(wd, c, d)
  local w0 = wd[c]
  local w1 = wd[d] > 0 and wd[d] or w0 * 1.1
  return w0, w1
end

--- The sub-point of link c's ford (1 .. SUB - 1).
function M.ford_k(c)
  return 1 + (c * 2654435761) % (M.RIVER_SUB - 1)
end

--- Half-width and wading depth (<= 1 wadeable) of link c at sub-point k, between widths w0 (at
-- c) and w1 (at its downhill end).
function M.river_at(c, k, w0, w1)
  local t = k / M.RIVER_SUB
  local wb = w0 + (w1 - w0) * t
  local ford = (k == M.ford_k(c)) and 1 or 0
  local hw = wb * 0.5 * (1 + M.FORD_SPREAD * ford)
  local depth = dmath.pow(wb / M.WADE_W, 0.8) * (1 - M.FORD_SHALLOW * ford)
  return hw, depth
end

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

--- The meandering course of link c from unit point a to unit point b (normally the centres of c
-- and its downhill cell): fills out[k] (k = 0 .. SUB) = { x, y, z (unit), sx, sy, sz (unit side
-- direction), sea } — where the course reaches drawn sea, the rest of the link is the shore point
-- (sea = true). G: the surface (M.get).
function M.river_path(G, c, ax, ay, az, bx, by, bz, out)
  local SUB = M.RIVER_SUB
  local hs = ((c * 7919) % 1000) / 1000
  local amp = 0.06 + 0.06 * hs
  local sgn = hs < 0.5 and -1 or 1
  local tx, ty, tz = bx - ax, by - ay, bz - az
  local hint = nil
  for k = 0, SUB do
    local t = k / SUB
    local x, y, z = norm(ax + tx * t, ay + ty * t, az + tz * t)
    -- side direction: up x along
    local sx, sy, sz = y * tz - z * ty, z * tx - x * tz, x * ty - y * tx
    local sl = sqrt(sx * sx + sy * sy + sz * sz) + 1e-9
    sx, sy, sz = sx / sl, sy / sl, sz / sl
    local off = (sgn * sin(pi * t) + 0.45 * sin(2 * pi * t + hs * 6)) * amp / M.R * sin(pi * t)
    x, y, z = norm(x + sx * off, y + sy * off, z + sz * off)
    local P = out[k]
    if not P then P = {}; out[k] = P end
    local prev = out[k - 1]
    local sv, v = M.sample(G, x, y, z, hint)
    hint = v
    if k > 0 and (sv <= 0 or prev.sea) then
      -- the river has reached the sea: the rest of the link collapses onto the shore point
      P.x, P.y, P.z, P.sx, P.sy, P.sz, P.sea = prev.x, prev.y, prev.z, prev.sx, prev.sy, prev.sz, true
    else
      P.x, P.y, P.z, P.sx, P.sy, P.sz, P.sea = x, y, z, sx, sy, sz, false
    end
  end
  return out
end

return M
