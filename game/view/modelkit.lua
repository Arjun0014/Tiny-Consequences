-- Procedural model kit (renderer v2, D-048). Pure Lua 5.1, no Defold APIs.
--
-- Templates are flat triangle lists in a local frame (y = up along the surface normal, x/z
-- tangent), stride 9: x y z nx ny nz slot shade sway. Slots 1..3 take per-instance colours
-- (e.g. foliage / bark / snow, roof / walls / trim); slots >= 4 are fixed colours (FIXED).
-- `shade` (0.5..1) is baked ambient occlusion: darker low down and inside crowns, under eaves.
-- Everything is built from a few primitives (ellipsoid lumps with deterministic jitter,
-- tapered cylinders, cones, boxes, roof slabs) placed with a scale/rotation/offset, so the
-- models stay soft and organic while vertex counts stay predictable (see M.stats()).
local M = {}

local sqrt, floor, min, max, abs, cos, sin, pi = math.sqrt, math.floor, math.min, math.max,
  math.abs, math.cos, math.sin, math.pi

M.STRIDE = 9

-- fixed colours for slots >= 4 (linear-ish sRGB 0..1)
M.FIXED = {
  [4] = { 0.20, 0.17, 0.16 },   -- window / door glass (dark)
  [5] = { 0.62, 0.60, 0.57 },   -- stone (chimney, well, foundations)
  [6] = { 0.45, 0.31, 0.20 },   -- timber
  [7] = { 0.93, 0.90, 0.84 },   -- plaster trim / white
  [8] = { 0.96, 0.97, 1.00 },   -- snow
  [9] = { 0.98, 0.84, 0.70 },   -- skin (default; movers override)
  [10] = { 0.30, 0.22, 0.16 },  -- hair / hooves / dark leather
  [11] = { 0.88, 0.55, 0.20 },  -- warm lamp window
  [12] = { 0.80, 0.70, 0.50 },  -- sacking, baskets (porters' loads)
  [13] = { 0.52, 0.37, 0.23 },  -- carried logs
  [14] = { 0.74, 0.46, 0.30 },  -- fired clay (pots, kilns)
}

local function norm3(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-12 then return 0, 1, 0 end
  return x / l, y / l, z / l
end
M.norm3 = norm3

function M.new() return { d = {}, nv = 0, tn = {} } end

-- Per-vertex tint (-1..1) of the lumps being built: sunlit lumps lean yellow-green (+), deep
-- ones blue-green (-) (props.vp). Set it around primitives with M.set_tint(x).
M.cur_tint = 0
function M.set_tint(x) M.cur_tint = x or 0 end

local function push(t, x, y, z, nx, ny, nz, slot, shade, sway)
  local d, o = t.d, t.nv * 9
  d[o + 1], d[o + 2], d[o + 3] = x, y, z
  d[o + 4], d[o + 5], d[o + 6] = nx, ny, nz
  d[o + 7], d[o + 8], d[o + 9] = slot, shade, sway
  t.nv = t.nv + 1
  local tn = t.tn
  if tn then tn[t.nv] = M.cur_tint end
end
M.push = push

-- deterministic jitter source (LCG on doubles, exact)
local seedv = 1
local function rnd()
  seedv = (seedv * 48271) % 2147483647
  return seedv / 2147483647
end
function M.seed(s) seedv = (s * 7919 + 13) % 2147483647; if seedv == 0 then seedv = 1 end end

-- Triangle with explicit per-vertex data; winding fixed so the face normal points along out.
local function tri(t, a, b, c, na, nb, nc, slot, sa, sb, sc, wa, wb, wc, ox, oy, oz)
  local ux, uy, uz = b[1] - a[1], b[2] - a[2], b[3] - a[3]
  local vx, vy, vz = c[1] - a[1], c[2] - a[2], c[3] - a[3]
  local fx, fy, fz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
  if fx * ox + fy * oy + fz * oz < 0 then
    b, c, nb, nc, sb, sc, wb, wc = c, b, nc, nb, sc, sb, wc, wb
  end
  if not na then
    local nx, ny, nz = norm3(fx, fy, fz)
    if fx * ox + fy * oy + fz * oz < 0 then nx, ny, nz = -nx, -ny, -nz end
    na = { nx, ny, nz }; nb = na; nc = na
  end
  push(t, a[1], a[2], a[3], na[1], na[2], na[3], slot, sa, wa)
  push(t, b[1], b[2], b[3], nb[1], nb[2], nb[3], slot, sb, wb)
  push(t, c[1], c[2], c[3], nc[1], nc[2], nc[3], slot, sc, wc)
end
M.tri = tri

local function lerp(a, b, t) return a + (b - a) * t end

---------------------------------------------------------------------------------------------
-- Placement: append src into dst, scaled, rotated (yaw about y, then tilt about x and z),
-- translated. Optional slot remap (table old -> new) and shade multiplier.

local function rot_matrix(yaw, tx, tz)
  local cy, sy = cos(yaw or 0), sin(yaw or 0)
  local cx, sx = cos(tx or 0), sin(tx or 0)
  local cz, sz = cos(tz or 0), sin(tz or 0)
  -- R = Ry * Rx * Rz
  local a11, a12, a13 = cz, -sz, 0
  local a21, a22, a23 = cx * sz, cx * cz, -sx
  local a31, a32, a33 = sx * sz, sx * cz, cx
  return cy * a11 + sy * a31, cy * a12 + sy * a32, cy * a13 + sy * a33,
    a21, a22, a23,
    -sy * a11 + cy * a31, -sy * a12 + cy * a32, -sy * a13 + cy * a33
end

function M.place(dst, src, ox, oy, oz, sx, sy, sz, yaw, tiltx, tiltz, remap, shade_mul, sway_mul, tint_add)
  local r11, r12, r13, r21, r22, r23, r31, r32, r33 = rot_matrix(yaw, tiltx, tiltz)
  local d = src.d
  local stn = src.tn
  local keep = M.cur_tint
  sy = sy or sx; sz = sz or sx
  for k = 0, src.nv - 1 do
    local o = k * 9
    local x, y, z = d[o + 1] * sx, d[o + 2] * sy, d[o + 3] * sz
    local nx, ny, nz = d[o + 4] / sx, d[o + 5] / sy, d[o + 6] / sz
    local px = r11 * x + r12 * y + r13 * z
    local py = r21 * x + r22 * y + r23 * z
    local pz = r31 * x + r32 * y + r33 * z
    local mx, my, mz = norm3(r11 * nx + r12 * ny + r13 * nz, r21 * nx + r22 * ny + r23 * nz,
      r31 * nx + r32 * ny + r33 * nz)
    local slot = d[o + 7]
    if remap and remap[slot] then slot = remap[slot] end
    local sh = d[o + 8] * (shade_mul or 1)
    M.cur_tint = (stn and stn[k + 1] or 0) + (tint_add or 0)
    push(dst, px + ox, py + oy, pz + oz, mx, my, mz, slot, sh, d[o + 9] * (sway_mul or 1))
  end
  M.cur_tint = keep
  return dst
end

---------------------------------------------------------------------------------------------
-- Primitives (all at the origin; use place() to position them)

-- Ellipsoid lump: `sides` around, `rings` latitude bands (>= 2), radial jitter `jit` (0..0.3),
-- shade lo..hi from bottom to top (AO), sway 0..1 grows with height. Smooth normals.
function M.lump(t, rx, ry, rz, sides, rings, slot, lo, hi, sway, jit)
  t = t or M.new()
  jit = jit or 0
  local P = {}
  for i = 0, rings do
    local lat = -pi / 2 + pi * i / rings
    local row = {}
    local n = (i == 0 or i == rings) and 1 or sides
    for k = 0, n - 1 do
      local lon = (k + (i % 2) * 0.5) / sides * 2 * pi
      local cl, sl = cos(lat), sin(lat)
      local ux, uy, uz = cl * cos(lon), sl, cl * sin(lon)
      local j = 1 + (rnd() - 0.5) * 2 * jit
      if i == 0 then j = 1 - jit * 0.5 end
      row[k + 1] = { ux * rx * j, uy * ry * j, uz * rz * j, ux, uy, uz }
    end
    P[i] = row
  end
  local function nrm(p) return { norm3(p[4] / rx, p[5] / ry, p[6] / rz) } end
  local function shd(p) return lo + (hi - lo) * ((p[2] / ry + 1) * 0.5) ^ 0.8 end
  local function swy(p) return sway * ((p[2] / ry + 1) * 0.5) end
  local function face(a, b, c)
    local ox, oy, oz = (a[1] + b[1] + c[1]) / 3, (a[2] + b[2] + c[2]) / 3, (a[3] + b[3] + c[3]) / 3
    tri(t, a, b, c, nrm(a), nrm(b), nrm(c), slot, shd(a), shd(b), shd(c), swy(a), swy(b), swy(c), ox, oy, oz)
  end
  for i = 0, rings - 1 do
    local A, B = P[i], P[i + 1]
    if #A == 1 then
      for k = 1, sides do face(A[1], B[k], B[k % sides + 1]) end
    elseif #B == 1 then
      for k = 1, sides do face(A[k], A[k % sides + 1], B[1]) end
    else
      for k = 1, sides do
        local k2 = k % sides + 1
        if i % 2 == 0 then
          face(A[k], A[k2], B[k]); face(A[k2], B[k2], B[k])
        else
          face(A[k], A[k2], B[k2]); face(A[k], B[k2], B[k])
        end
      end
    end
  end
  return t
end

-- Tapered cylinder (smooth sides) from y0 (radius r0) to y1 (radius r1); cap = top cap.
function M.cyl(t, r0, r1, y0, y1, sides, slot, lo, hi, sway, cap)
  t = t or M.new()
  sway = sway or 0
  for k = 0, sides - 1 do
    local a0, a1 = k / sides * 2 * pi, (k + 1) / sides * 2 * pi
    local c0, s0, c1, s1 = cos(a0), sin(a0), cos(a1), sin(a1)
    local slope = (r0 - r1) / (y1 - y0)
    local n0 = { norm3(c0, slope, s0) }
    local n1 = { norm3(c1, slope, s1) }
    local p00, p10 = { r0 * c0, y0, r0 * s0 }, { r0 * c1, y0, r0 * s1 }
    local p01, p11 = { r1 * c0, y1, r1 * s0 }, { r1 * c1, y1, r1 * s1 }
    local am = (a0 + a1) * 0.5
    tri(t, p00, p10, p11, n0, n1, n1, slot, lo, lo, hi, 0, 0, sway, cos(am), 0, sin(am))
    tri(t, p00, p11, p01, n0, n1, n0, slot, lo, hi, hi, 0, sway, sway, cos(am), 0, sin(am))
    if cap then
      tri(t, { 0, y1, 0 }, p01, p11, { 0, 1, 0 }, { 0, 1, 0 }, { 0, 1, 0 }, slot, hi, hi, hi, sway, sway, sway, 0, 1, 0)
    end
  end
  return t
end

-- Cone (no base): base ring at y0 radius r (drooped by `droop` at the rim), apex at y1.
function M.cone(t, y0, y1, r, sides, slot, lo, hi, sway, rot, droop)
  t = t or M.new()
  droop = droop or 0
  local slope = r / (y1 - y0)
  for k = 0, sides - 1 do
    local a0 = (k + (rot or 0)) / sides * 2 * pi
    local a1 = (k + 1 + (rot or 0)) / sides * 2 * pi
    local p0 = { r * cos(a0), y0 - droop, r * sin(a0) }
    local p1 = { r * cos(a1), y0 - droop, r * sin(a1) }
    local n0 = { norm3(cos(a0), slope, sin(a0)) }
    local n1 = { norm3(cos(a1), slope, sin(a1)) }
    local am = (a0 + a1) * 0.5
    local na = { norm3(cos(am), slope * 1.3, sin(am)) }
    tri(t, { 0, y1, 0 }, p0, p1, na, n0, n1, slot, hi, lo, lo, sway, sway * 0.5, sway * 0.5, cos(am), 0.3, sin(am))
  end
  return t
end

-- Axis-aligned box centred at the origin (flat normals); shade lo..hi by height. bottom=false
-- skips the underside (it sits on the ground).
function M.box(t, sx, sy, sz, slot, lo, hi, bottom)
  t = t or M.new()
  local x0, x1, y0, y1, z0, z1 = -sx / 2, sx / 2, -sy / 2, sy / 2, -sz / 2, sz / 2
  local function sh(y) return lo + (hi - lo) * (y - y0) / (y1 - y0) end
  local function quad(a, b, c, d, ox, oy, oz)
    tri(t, a, b, c, nil, nil, nil, slot, sh(a[2]), sh(b[2]), sh(c[2]), 0, 0, 0, ox, oy, oz)
    tri(t, a, c, d, nil, nil, nil, slot, sh(a[2]), sh(c[2]), sh(d[2]), 0, 0, 0, ox, oy, oz)
  end
  quad({ x0, y0, z0 }, { x1, y0, z0 }, { x1, y1, z0 }, { x0, y1, z0 }, 0, 0, -1)
  quad({ x1, y0, z1 }, { x0, y0, z1 }, { x0, y1, z1 }, { x1, y1, z1 }, 0, 0, 1)
  quad({ x0, y0, z1 }, { x0, y0, z0 }, { x0, y1, z0 }, { x0, y1, z1 }, -1, 0, 0)
  quad({ x1, y0, z0 }, { x1, y0, z1 }, { x1, y1, z1 }, { x1, y1, z0 }, 1, 0, 0)
  quad({ x0, y1, z0 }, { x1, y1, z0 }, { x1, y1, z1 }, { x0, y1, z1 }, 0, 1, 0)
  if bottom then quad({ x0, y0, z1 }, { x1, y0, z1 }, { x1, y0, z0 }, { x0, y0, z0 }, 0, -1, 0) end
  return t
end

-- Flat quad in the xy plane (facing +z), for windows and doors.
function M.quad(t, w, h, slot, shade)
  t = t or M.new()
  local a, b, c, d = { -w / 2, -h / 2, 0 }, { w / 2, -h / 2, 0 }, { w / 2, h / 2, 0 }, { -w / 2, h / 2, 0 }
  tri(t, a, b, c, nil, nil, nil, slot, shade, shade, shade, 0, 0, 0, 0, 0, 1)
  tri(t, a, c, d, nil, nil, nil, slot, shade, shade, shade, 0, 0, 0, 0, 0, 1)
  return t
end

-- Gable house body: walls sx (along x) × sz, height h, roof ridge along x at h + rh, with
-- overhang and slab thickness; roof slot 1, walls slot 2, gables slot 2; returns template.
function M.gable_house(t, sx, sz, h, rh, over, thick, wall_slot, roof_slot)
  t = t or M.new()
  wall_slot, roof_slot = wall_slot or 2, roof_slot or 1
  M.place(t, M.box(nil, sx, h, sz, wall_slot, 0.62, 0.95, false), 0, h / 2, 0, 1)
  -- gable triangles
  local zm = 0
  tri(t, { -sx / 2, h, -sz / 2 }, { -sx / 2, h, sz / 2 }, { -sx / 2, h + rh, zm }, nil, nil, nil, wall_slot,
    0.9, 0.9, 0.95, 0, 0, 0, -1, 0, 0)
  tri(t, { sx / 2, h, sz / 2 }, { sx / 2, h, -sz / 2 }, { sx / 2, h + rh, zm }, nil, nil, nil, wall_slot,
    0.9, 0.9, 0.95, 0, 0, 0, 1, 0, 0)
  -- roof slabs: rotated thin boxes
  local half = sz / 2 + over
  local ang = math.atan2(rh, sz / 2)
  local len = half / cos(ang)
  local slab = M.box(nil, sx + over * 2, thick, len, roof_slot, 0.72, 1.0, true)
  local cy = h + rh - (len / 2) * sin(ang) + thick * 0.35
  local cz = (len / 2) * cos(ang)
  M.place(t, slab, 0, cy, -cz + 0.0005, 1, 1, 1, 0, -ang, 0)
  M.place(t, slab, 0, cy, cz - 0.0005, 1, 1, 1, 0, ang, 0)
  return t
end

---------------------------------------------------------------------------------------------
-- Model library. M.build() fills M.T[lod][name] (lod 1 near, 2 far).

M.T = { {}, {} }

local function tree_crown(t, lumps, slot, sides, rings, jit)
  for _, L in ipairs(lumps) do
    local s = M.lump(nil, L[4], L[5], L[4], sides, rings, slot, L[6] or 0.58, 1.0, 1, jit or 0.12)
    M.place(t, s, L[1], L[2], L[3], 1)
  end
end

local function build_near()
  local N = M.T[1]
  local t
  -- broadleaf (oak-like): stout trunk, crown of 3 overlapping lumps
  M.seed(11)
  t = M.new()
  M.cyl(t, 0.013, 0.008, -0.02, 0.105, 5, 2, 0.5, 0.78, 0)
  M.place(t, M.cyl(nil, 0.005, 0.003, 0, 0.045, 4, 2, 0.6, 0.8, 0.3), 0.004, 0.075, 0, 1, 1, 1, 0.6, 0, -0.9)
  tree_crown(t, { { 0.000, 0.140, 0.000, 0.064, 0.054, 0.56 } }, 1, 7, 3, 0.14)
  tree_crown(t, {
    { 0.040, 0.118, 0.020, 0.044, 0.038, 0.52 },
    { -0.034, 0.122, -0.026, 0.046, 0.040, 0.52 },
  }, 1, 6, 2, 0.14)
  N.oak = t
  -- birch: slim pale trunk, tall narrow crown
  M.seed(12)
  t = M.new()
  M.cyl(t, 0.008, 0.005, -0.02, 0.15, 5, 2, 0.7, 0.95, 0)
  tree_crown(t, { { 0.000, 0.160, 0.000, 0.036, 0.062, 0.6 } }, 1, 6, 3, 0.16)
  tree_crown(t, {
    { 0.018, 0.115, 0.012, 0.028, 0.040, 0.55 },
    { -0.016, 0.130, -0.010, 0.028, 0.044, 0.55 },
  }, 1, 5, 2, 0.16)
  N.birch = t
  -- spruce: trunk + 4 drooping tiers
  M.seed(13)
  t = M.new()
  M.cyl(t, 0.010, 0.006, -0.02, 0.06, 5, 2, 0.5, 0.7, 0)
  local tiers = { { 0.035, 0.12, 0.060 }, { 0.075, 0.16, 0.050 }, { 0.112, 0.20, 0.038 }, { 0.150, 0.235, 0.026 } }
  for i, q in ipairs(tiers) do
    M.cone(t, q[1], q[2], q[3], 7, 1, 0.55 + i * 0.05, 1.0, 0.3 + i * 0.15, i * 0.5, q[3] * 0.25)
  end
  N.spruce = t
  -- snowy spruce: same with white upper tiers (slot 3 = snow colour)
  M.seed(13)
  t = M.new()
  M.cyl(t, 0.010, 0.006, -0.02, 0.06, 5, 2, 0.5, 0.7, 0)
  for i, q in ipairs(tiers) do
    M.cone(t, q[1], q[2], q[3], 7, 1, 0.55 + i * 0.05, 1.0, 0.3 + i * 0.15, i * 0.5, q[3] * 0.25)
    if i >= 2 then M.cone(t, q[1] + 0.012, q[2] + 0.003, q[3] * 0.72, 6, 3, 0.85, 1.0, 0.3 + i * 0.15, i * 0.5 + 0.25, q[3] * 0.15) end
  end
  N.snowpine = t
  -- scots pine: tall bare trunk, flat cloud-like top
  M.seed(14)
  t = M.new()
  M.cyl(t, 0.009, 0.006, -0.02, 0.19, 5, 2, 0.55, 0.85, 0.2)
  tree_crown(t, {
    { 0.000, 0.195, 0.000, 0.048, 0.024, 0.6 },
    { 0.030, 0.175, 0.012, 0.030, 0.018, 0.55 },
    { -0.026, 0.182, -0.014, 0.030, 0.018, 0.55 },
  }, 1, 6, 2, 0.18)
  N.pine = t
  -- palm: curved segmented trunk, 7 drooping fronds
  M.seed(15)
  t = M.new()
  local px, py = 0, -0.02
  for s = 0, 3 do
    local a0 = 0.12 + s * 0.08
    local seg = M.cyl(nil, 0.0085 - s * 0.001, 0.0075 - s * 0.001, 0, 0.05, 5, 2, 0.6, 0.85, 0.15 * s)
    M.place(t, seg, px, py, 0, 1, 1, 1, 0, 0, -a0)
    px = px + sin(a0) * 0.05
    py = py + cos(a0) * 0.05
  end
  for k = 0, 6 do
    local a = k / 7 * 2 * pi + 0.3
    local ca, sa = cos(a), sin(a)
    local len, w = 0.085, 0.018
    local b = { px, py, 0 }
    local m1 = { px + ca * len * 0.5, py + 0.018, sa * len * 0.5 }
    local tip = { px + ca * len, py - 0.03, sa * len }
    local l = { m1[1] - sa * w, m1[2], m1[3] + ca * w }
    local r = { m1[1] + sa * w, m1[2], m1[3] - ca * w }
    tri(t, b, l, m1, nil, nil, nil, 1, 0.75, 0.95, 1.0, 0.3, 0.8, 0.8, 0, 1, 0)
    tri(t, b, m1, r, nil, nil, nil, 1, 0.75, 1.0, 0.95, 0.3, 0.8, 0.8, 0, 1, 0)
    tri(t, l, tip, m1, nil, nil, nil, 1, 0.95, 0.85, 1.0, 0.8, 1, 0.8, 0, 1, 0)
    tri(t, m1, tip, r, nil, nil, nil, 1, 1.0, 0.85, 0.95, 0.8, 1, 0.8, 0, 1, 0)
  end
  M.place(t, M.lump(nil, 0.009, 0.009, 0.009, 5, 2, 6, 0.6, 0.9, 0, 0), px, py - 0.006, 0, 1)
  N.palm = t
  -- jungle giant: tall trunk with an umbrella crown and an understory lump
  M.seed(16)
  t = M.new()
  M.cyl(t, 0.014, 0.008, -0.02, 0.19, 5, 2, 0.5, 0.8, 0)
  tree_crown(t, { { 0.000, 0.205, 0.000, 0.078, 0.036, 0.55 } }, 1, 7, 3, 0.15)
  tree_crown(t, {
    { 0.050, 0.188, 0.030, 0.050, 0.028, 0.5 },
    { -0.045, 0.192, -0.030, 0.052, 0.028, 0.5 },
    { 0.020, 0.075, -0.020, 0.040, 0.036, 0.5 },
  }, 1, 5, 2, 0.15)
  N.jungle = t
  -- acacia (savanna): leaning trunk, wide flat crown
  M.seed(17)
  t = M.new()
  M.place(t, M.cyl(nil, 0.009, 0.006, 0, 0.13, 5, 2, 0.55, 0.85, 0), 0, -0.02, 0, 1, 1, 1, 0, 0, 0.15)
  M.place(t, M.cyl(nil, 0.005, 0.004, 0, 0.06, 4, 2, 0.6, 0.85, 0.2), 0.015, 0.085, 0, 1, 1, 1, 0, 0, -0.7)
  tree_crown(t, {
    { 0.020, 0.135, 0.000, 0.075, 0.020, 0.6 },
    { 0.060, 0.125, 0.020, 0.040, 0.015, 0.55 },
  }, 1, 8, 2, 0.1)
  N.acacia = t
  -- burnt snag: black trunk with a few bare branches
  M.seed(18)
  t = M.new()
  M.cyl(t, 0.010, 0.004, -0.02, 0.15, 5, 1, 0.6, 0.85, 0)
  M.place(t, M.cyl(nil, 0.004, 0.002, 0, 0.05, 4, 1, 0.6, 0.8, 0), 0, 0.08, 0, 1, 1, 1, 0.4, 0, -0.8)
  M.place(t, M.cyl(nil, 0.004, 0.002, 0, 0.045, 4, 1, 0.6, 0.8, 0), 0, 0.10, 0, 1, 1, 1, 2.6, 0, -0.9)
  N.snag = t
  -- stump
  t = M.new()
  M.cyl(t, 0.014, 0.012, -0.01, 0.022, 6, 1, 0.6, 0.9, 0, true)
  N.stump = t
  -- bush: 3 low lumps
  M.seed(19)
  t = M.new()
  tree_crown(t, {
    { 0.000, 0.022, 0.000, 0.040, 0.032, 0.5 },
    { 0.030, 0.016, 0.014, 0.028, 0.024, 0.5 },
  }, 1, 6, 2, 0.18)
  N.bush = t
  -- grass tuft: 5 blades
  t = M.new()
  for k = 0, 4 do
    local a = k / 5 * 2 * pi + 0.4
    local ca, sa = cos(a), sin(a)
    local lean = 0.35 + (k % 3) * 0.12
    tri(t, { -sa * 0.004, 0, ca * 0.004 }, { sa * 0.004, 0, -ca * 0.004 },
      { ca * 0.02 * lean, 0.035 + (k % 2) * 0.01, sa * 0.02 * lean }, nil, nil, nil, 1, 0.6, 0.6, 1.0, 0, 0, 1, ca, 0.4, sa)
  end
  N.tuft = t
  -- flowers: 4 stems with coloured heads (slot 1 = petals, slot 2 = stem green)
  M.seed(20)
  t = M.new()
  for k = 0, 2 do
    local a = k / 3 * 2 * pi + rnd()
    local d = 0.012 + rnd() * 0.01
    local x, z = cos(a) * d, sin(a) * d
    local hgt = 0.022 + rnd() * 0.012
    tri(t, { x - 0.0015, 0, z }, { x + 0.0015, 0, z }, { x, hgt, z }, nil, nil, nil, 2, 0.6, 0.6, 0.9, 0, 0, 0.8, 0, 0, 1)
    M.place(t, M.lump(nil, 0.007, 0.004, 0.007, 4, 2, 1, 0.85, 1.0, 1, 0), x, hgt, z, 1)
  end
  N.flower = t
  -- reeds (marsh and shores)
  t = M.new()
  for k = 0, 5 do
    local a = k / 6 * 2 * pi
    local x, z = cos(a) * 0.01, sin(a) * 0.01
    tri(t, { x - 0.002, 0, z }, { x + 0.002, 0, z }, { x + cos(a) * 0.006, 0.05 + (k % 2) * 0.015, z + sin(a) * 0.006 },
      nil, nil, nil, 1, 0.6, 0.6, 1.0, 0, 0, 1, 0, 0, 1)
  end
  N.reed = t
  -- boulder: jittered lump, mossy top (slot 2 on top via a second smaller lump)
  M.seed(21)
  t = M.new()
  M.place(t, M.lump(nil, 0.035, 0.024, 0.030, 6, 3, 1, 0.6, 1.0, 0, 0.25), 0, 0.006, 0, 1)
  N.rock = t
  -- cactus: column with two arms
  t = M.new()
  M.cyl(t, 0.011, 0.010, -0.01, 0.075, 5, 1, 0.6, 0.95, 0, false)
  M.place(t, M.lump(nil, 0.010, 0.008, 0.010, 5, 2, 1, 0.8, 1.0, 0, 0), 0, 0.075, 0, 1)
  M.place(t, M.cyl(nil, 0.0065, 0.006, 0, 0.022, 4, 1, 0.6, 0.9, 0), 0.008, 0.035, 0, 1, 1, 1, 0, 0, -1.5)
  M.place(t, M.cyl(nil, 0.006, 0.0055, 0, 0.025, 5, 1, 0.6, 0.95, 0, true), 0.028, 0.035, 0, 1)
  M.place(t, M.cyl(nil, 0.0065, 0.006, 0, 0.018, 5, 1, 0.6, 0.9, 0), -0.008, 0.048, 0, 1, 1, 1, 0, 0, 1.5)
  M.place(t, M.cyl(nil, 0.006, 0.0055, 0, 0.02, 5, 1, 0.6, 0.95, 0, true), -0.024, 0.048, 0, 1)
  N.cactus = t

  -- buildings (x = along the ridge; the door faces +z) ----------------------------------------
  -- cottage: walls slot 2, roof slot 1, chimney stone, door timber, windows dark
  t = M.new()
  M.gable_house(t, 0.07, 0.05, 0.042, 0.032, 0.008, 0.006, 2, 1)
  M.place(t, M.box(nil, 0.012, 0.03, 0.012, 5, 0.7, 0.95, false), 0.022, 0.075, -0.008, 1)
  M.place(t, M.quad(nil, 0.013, 0.024, 6, 0.8), -0.012, 0.012, 0.0252, 1)
  M.place(t, M.quad(nil, 0.011, 0.011, 4, 0.9), 0.017, 0.026, 0.0252, 1)
  M.place(t, M.quad(nil, 0.011, 0.011, 4, 0.9), -0.0352, 0.026, 0.0, 1, 1, 1, -pi / 2)
  M.place(t, M.box(nil, 0.074, 0.006, 0.054, 5, 0.6, 0.8, false), 0, 0.0, 0, 1)
  N.cottage = t
  -- longhouse / barn: long, low, big roof, timber walls
  t = M.new()
  M.gable_house(t, 0.11, 0.05, 0.032, 0.036, 0.008, 0.006, 2, 1)
  M.place(t, M.quad(nil, 0.018, 0.022, 6, 0.8), 0.0, 0.011, 0.0252, 1)
  M.place(t, M.quad(nil, 0.009, 0.009, 4, 0.9), 0.035, 0.022, 0.0252, 1)
  M.place(t, M.quad(nil, 0.009, 0.009, 4, 0.9), -0.035, 0.022, 0.0252, 1)
  N.longhouse = t
  -- two-storey house: tall walls, steep roof, two chimneys
  t = M.new()
  M.gable_house(t, 0.066, 0.052, 0.07, 0.04, 0.007, 0.006, 2, 1)
  M.place(t, M.box(nil, 0.011, 0.03, 0.011, 5, 0.7, 0.95, false), -0.02, 0.105, 0.006, 1)
  M.place(t, M.quad(nil, 0.013, 0.024, 6, 0.8), 0.0, 0.012, 0.0262, 1)
  for _, q in ipairs({ { -0.02, 0.05 }, { 0.02, 0.05 }, { -0.02, 0.026 }, { 0.02, 0.026 } }) do
    M.place(t, M.quad(nil, 0.01, 0.012, 4, 0.9), q[1], q[2], 0.0262, 1)
  end
  M.place(t, M.box(nil, 0.07, 0.006, 0.056, 5, 0.6, 0.8, false), 0, 0.0, 0, 1)
  N.townhouse = t
  -- round hut with thatched cone roof (slot 1 = thatch, slot 2 = walls)
  t = M.new()
  M.cyl(t, 0.03, 0.028, -0.01, 0.034, 8, 2, 0.62, 0.95, 0)
  M.cone(t, 0.028, 0.085, 0.042, 9, 1, 0.7, 1.0, 0, 0, 0.004)
  M.place(t, M.quad(nil, 0.012, 0.02, 6, 0.75), 0, 0.01, 0.0305, 1)
  N.hut = t
  -- granary: stone drum with a conical roof
  t = M.new()
  M.cyl(t, 0.022, 0.022, -0.01, 0.05, 8, 5, 0.65, 0.95, 0)
  M.cone(t, 0.048, 0.085, 0.03, 9, 1, 0.7, 1.0, 0, 0, 0.003)
  N.granary = t
  -- hall with a little bell tower (big villages)
  t = M.new()
  M.gable_house(t, 0.12, 0.07, 0.05, 0.045, 0.009, 0.007, 7, 1)
  M.place(t, M.box(nil, 0.026, 0.07, 0.026, 7, 0.75, 1.0, false), 0.07, 0.035, 0, 1)
  M.place(t, M.cone(nil, 0.0, 0.05, 0.022, 4, 1, 0.75, 1.0, 0, 0.5, 0), 0.07, 0.07, 0, 1)
  M.place(t, M.quad(nil, 0.02, 0.03, 6, 0.8), -0.02, 0.015, 0.0352, 1)
  for _, x in ipairs({ -0.045, 0.01, 0.035 }) do
    M.place(t, M.quad(nil, 0.012, 0.016, 4, 0.9), x, 0.032, 0.0352, 1)
  end
  N.hall = t
  -- well: stone ring, two posts, little roof
  t = M.new()
  M.cyl(t, 0.012, 0.012, -0.005, 0.012, 7, 5, 0.6, 0.9, 0, true)
  M.place(t, M.box(nil, 0.003, 0.03, 0.003, 6, 0.7, 0.9, false), -0.011, 0.02, 0, 1)
  M.place(t, M.box(nil, 0.003, 0.03, 0.003, 6, 0.7, 0.9, false), 0.011, 0.02, 0, 1)
  M.place(t, M.gable_house(nil, 0.03, 0.02, 0.0, 0.01, 0.003, 0.003, 6, 1), 0, 0.034, 0, 1)
  N.well = t
  -- fence segment (along x, 0.08 long)
  t = M.new()
  for _, x in ipairs({ -0.04, 0, 0.04 }) do M.place(t, M.box(nil, 0.003, 0.018, 0.003, 6, 0.7, 0.95, false), x, 0.009, 0, 1) end
  M.place(t, M.box(nil, 0.08, 0.003, 0.002, 6, 0.8, 0.95, true), 0, 0.012, 0, 1)
  M.place(t, M.box(nil, 0.08, 0.003, 0.002, 6, 0.8, 0.95, true), 0, 0.006, 0, 1)
  N.fence = t
  -- haystack (after harvest) and woodpile
  t = M.new()
  M.place(t, M.lump(nil, 0.016, 0.018, 0.016, 7, 3, 1, 0.7, 1.0, 0, 0.08), 0, 0.012, 0, 1)
  N.haystack = t
  t = M.new()
  for i = 0, 2 do M.place(t, M.cyl(nil, 0.004, 0.004, -0.015, 0.015, 5, 6, 0.6, 0.9, 0, true), 0, 0.005 + i * 0.007, -0.008 + i * 0.008, 1, 1, 1, 0, pi / 2, 0) end
  N.woodpile = t
  -- ruins: broken walls and a fallen roof beam
  t = M.new()
  M.place(t, M.box(nil, 0.07, 0.03, 0.006, 5, 0.6, 0.85, false), 0, 0.012, -0.022, 1)
  M.place(t, M.box(nil, 0.006, 0.02, 0.05, 5, 0.6, 0.85, false), -0.034, 0.008, 0, 1)
  M.place(t, M.box(nil, 0.006, 0.012, 0.03, 5, 0.6, 0.85, false), 0.034, 0.004, 0.008, 1)
  M.place(t, M.box(nil, 0.06, 0.005, 0.006, 6, 0.6, 0.8, false), 0.005, 0.004, 0.01, 1, 1, 1, 0.5, 0, 0.12)
  N.ruin = t
  -- pier: planks on posts (from the shore out to sea along +z)
  t = M.new()
  M.place(t, M.box(nil, 0.03, 0.004, 0.14, 6, 0.75, 0.95, true), 0, 0.006, 0.07, 1)
  for _, z in ipairs({ 0.03, 0.08, 0.13 }) do
    M.place(t, M.box(nil, 0.004, 0.03, 0.004, 6, 0.6, 0.8, false), 0.013, -0.01, z, 1)
    M.place(t, M.box(nil, 0.004, 0.03, 0.004, 6, 0.6, 0.8, false), -0.013, -0.01, z, 1)
  end
  N.pier = t
  -- construction stages of a house (W2c: buildings from the sim's sites) ---------------------
  -- staked lot: corner stakes and a string outline on the footprint of a cottage
  t = M.new()
  for _, q in ipairs({ { -0.035, -0.025 }, { 0.035, -0.025 }, { 0.035, 0.025 }, { -0.035, 0.025 } }) do
    M.place(t, M.box(nil, 0.003, 0.016, 0.003, 6, 0.7, 0.95, false), q[1], 0.008, q[2], 1)
  end
  M.place(t, M.box(nil, 0.07, 0.0012, 0.0012, 7, 0.9, 1.0, true), 0, 0.012, -0.025, 1)
  M.place(t, M.box(nil, 0.07, 0.0012, 0.0012, 7, 0.9, 1.0, true), 0, 0.012, 0.025, 1)
  M.place(t, M.box(nil, 0.0012, 0.0012, 0.05, 7, 0.9, 1.0, true), -0.035, 0.012, 0, 1)
  M.place(t, M.box(nil, 0.0012, 0.0012, 0.05, 7, 0.9, 1.0, true), 0.035, 0.012, 0, 1)
  M.place(t, M.box(nil, 0.074, 0.004, 0.054, 5, 0.6, 0.8, false), 0, 0.0, 0, 1)
  N.stakes = t
  -- timber frame: posts, wall plates, gable posts and rafters
  t = M.new()
  for _, q in ipairs({ { -0.035, -0.025 }, { 0.035, -0.025 }, { 0.035, 0.025 }, { -0.035, 0.025 }, { 0, -0.025 }, { 0, 0.025 } }) do
    M.place(t, M.box(nil, 0.004, 0.042, 0.004, 6, 0.65, 0.95, false), q[1], 0.021, q[2], 1)
  end
  M.place(t, M.box(nil, 0.074, 0.003, 0.003, 6, 0.8, 1.0, true), 0, 0.042, -0.025, 1)
  M.place(t, M.box(nil, 0.074, 0.003, 0.003, 6, 0.8, 1.0, true), 0, 0.042, 0.025, 1)
  M.place(t, M.box(nil, 0.074, 0.003, 0.003, 6, 0.8, 1.0, true), 0, 0.074, 0, 1)
  local ang = math.atan2(0.032, 0.025)
  local rl = math.sqrt(0.032 * 0.032 + 0.025 * 0.025)
  for _, x in ipairs({ -0.035, -0.012, 0.012, 0.035 }) do
    M.place(t, M.box(nil, 0.0025, 0.0025, rl, 6, 0.75, 1.0, true), x, 0.058, -0.0125, 1, 1, 1, 0, ang, 0)
    M.place(t, M.box(nil, 0.0025, 0.0025, rl, 6, 0.75, 1.0, true), x, 0.058, 0.0125, 1, 1, 1, 0, -ang, 0)
  end
  M.place(t, M.box(nil, 0.074, 0.006, 0.054, 5, 0.6, 0.8, false), 0, 0.0, 0, 1)
  N.frame = t
  -- walls up, roof still open (rafters only)
  t = M.new()
  M.place(t, M.box(nil, 0.07, 0.042, 0.05, 2, 0.62, 0.95, false), 0, 0.021, 0, 1)
  M.place(t, M.quad(nil, 0.013, 0.024, 6, 0.8), -0.012, 0.012, 0.0252, 1)
  for _, x in ipairs({ -0.035, -0.012, 0.012, 0.035 }) do
    M.place(t, M.box(nil, 0.0025, 0.0025, rl, 6, 0.75, 1.0, true), x, 0.058, -0.0125, 1, 1, 1, 0, ang, 0)
    M.place(t, M.box(nil, 0.0025, 0.0025, rl, 6, 0.75, 1.0, true), x, 0.058, 0.0125, 1, 1, 1, 0, -ang, 0)
  end
  M.place(t, M.box(nil, 0.074, 0.003, 0.003, 6, 0.8, 1.0, true), 0, 0.074, 0, 1)
  M.place(t, M.box(nil, 0.074, 0.006, 0.054, 5, 0.6, 0.8, false), 0, 0.0, 0, 1)
  N.walls = t
  -- sod / mud-brick house where timber is scarce: low thick walls, rounded turf roof
  t = M.new()
  M.place(t, M.box(nil, 0.068, 0.026, 0.05, 2, 0.6, 0.9, false), 0, 0.013, 0, 1)
  M.place(t, M.lump(nil, 0.04, 0.02, 0.03, 8, 3, 1, 0.7, 1.0, 0, 0.06), 0, 0.026, 0, 1)
  M.place(t, M.quad(nil, 0.012, 0.018, 6, 0.75), 0.0, 0.009, 0.0252, 1)
  N.sodhouse = t
  -- woodshed: a lean-to roof on posts over stacked logs
  t = M.new()
  for _, q in ipairs({ { -0.025, -0.014 }, { 0.025, -0.014 }, { -0.025, 0.014 }, { 0.025, 0.014 } }) do
    M.place(t, M.box(nil, 0.003, q[2] < 0 and 0.036 or 0.028, 0.003, 6, 0.65, 0.95, false), q[1], q[2] < 0 and 0.018 or 0.014, q[2], 1)
  end
  M.place(t, M.box(nil, 0.058, 0.003, 0.036, 1, 0.75, 1.0, true), 0, 0.033, 0, 1, 1, 1, 0, -0.22, 0)
  for i = 0, 2 do
    M.place(t, M.cyl(nil, 0.0045, 0.0045, -0.022, 0.022, 5, 6, 0.6, 0.9, 0, true), 0, 0.005 + i * 0.008, -0.006 + (i % 2) * 0.006, 1, 1, 1, 0, pi / 2, 0)
  end
  N.woodshed = t
  -- grain sacks (stock by the granary / houses)
  t = M.new()
  M.place(t, M.lump(nil, 0.008, 0.011, 0.007, 6, 3, 1, 0.65, 1.0, 0, 0.1), -0.008, 0.009, 0, 1)
  M.place(t, M.lump(nil, 0.008, 0.011, 0.007, 6, 3, 1, 0.65, 1.0, 0, 0.1), 0.008, 0.009, 0.002, 1)
  M.place(t, M.lump(nil, 0.008, 0.009, 0.007, 6, 3, 1, 0.7, 1.0, 0, 0.1), 0.0, 0.021, 0.001, 1)
  N.sacks = t
  -- fish drying rack: two posts, a pole, hanging fish (slot 1 = silver)
  t = M.new()
  M.place(t, M.box(nil, 0.0025, 0.03, 0.0025, 6, 0.65, 0.95, false), -0.022, 0.015, 0, 1)
  M.place(t, M.box(nil, 0.0025, 0.03, 0.0025, 6, 0.65, 0.95, false), 0.022, 0.015, 0, 1)
  M.place(t, M.box(nil, 0.048, 0.002, 0.002, 6, 0.8, 1.0, true), 0, 0.029, 0, 1)
  for k = 0, 4 do
    M.place(t, M.lump(nil, 0.0022, 0.007, 0.0016, 4, 2, 1, 0.7, 1.0, 0.4, 0), -0.016 + k * 0.008, 0.021, 0, 1)
  end
  N.fishrack = t
  -- a boat hull on trestles (a boat being built at the shore; slot 1 = planks)
  t = M.new()
  M.place(t, M.box(nil, 0.1, 0.018, 0.034, 1, 0.65, 0.95, false), 0, 0.02, 0, 1)
  tri(t, { 0.05, 0.011, -0.017 }, { 0.075, 0.029, 0.0 }, { 0.05, 0.029, -0.017 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, -1)
  tri(t, { 0.05, 0.011, 0.017 }, { 0.05, 0.029, 0.017 }, { 0.075, 0.029, 0.0 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, 1)
  for _, x in ipairs({ -0.03, 0.03 }) do
    M.place(t, M.box(nil, 0.004, 0.012, 0.04, 6, 0.6, 0.85, false), x, 0.006, 0, 1)
  end
  N.hull = t
  -- a stook of sheaves on a harvested field (slot 1 = straw)
  t = M.new()
  M.cone(t, 0.0, 0.026, 0.009, 6, 1, 0.65, 1.0, 0.2, 0, 0.002)
  M.place(t, M.lump(nil, 0.005, 0.004, 0.005, 5, 2, 1, 0.8, 1.0, 0, 0), 0, 0.026, 0, 1)
  N.stook = t
  -- a pen (W10): a ring of hurdles where the herd sleeps
  t = M.new()
  for k = 0, 9 do
    local a = k / 10 * 2 * math.pi
    local x, z = math.cos(a) * 0.055, math.sin(a) * 0.055
    M.place(t, M.box(nil, 0.003, 0.02, 0.003, 6, 0.7, 0.95, false), x, 0.01, z, 1)
    local a2 = (k + 0.5) / 10 * 2 * math.pi
    if k ~= 0 then
      M.place(t, M.box(nil, 0.036, 0.0025, 0.002, 6, 0.8, 0.95, true), math.cos(a2) * 0.054, 0.014, math.sin(a2) * 0.054, 1, 1, 1, -a2 + math.pi / 2)
      M.place(t, M.box(nil, 0.036, 0.0025, 0.002, 6, 0.8, 0.95, true), math.cos(a2) * 0.054, 0.007, math.sin(a2) * 0.054, 1, 1, 1, -a2 + math.pi / 2)
    end
  end
  N.pen = t
  -- a pottery kiln (crafts): a clay dome with a fire mouth and a short flue
  t = M.new()
  M.place(t, M.lump(nil, 0.03, 0.028, 0.03, 8, 3, 14, 0.6, 1.0, 0, 0.06), 0, 0.006, 0, 1)
  M.place(t, M.cyl(nil, 0.006, 0.005, 0, 0.022, 6, 14, 0.7, 0.95, 0, true), 0.006, 0.03, 0, 1)
  M.place(t, M.quad(nil, 0.014, 0.012, 4, 0.7), 0, 0.01, 0.0305, 1)
  N.kiln = t
  -- a stack of pots
  t = M.new()
  for k, q in ipairs({ { -0.008, 0, 0.004 }, { 0.008, 0, -0.003 }, { 0.001, 0, 0.011 }, { 0, 0.014, 0.003 } }) do
    M.place(t, M.lump(nil, 0.0075, 0.0085, 0.0075, 7, 3, 14, 0.65, 1.0, 0, 0.03), q[1], q[2] + 0.008, q[3], 1)
    M.place(t, M.cyl(nil, 0.004, 0.0048, 0, 0.004, 7, 14, 0.8, 1.0, 0), q[1], q[2] + 0.015, q[3], 1)
  end
  N.pots = t
  -- a run of palisade (W8): sharpened stakes along x, 0.1 long, with a rail behind
  t = M.new()
  for k = 0, 6 do
    local x = -0.045 + k * 0.015
    local hh = 0.042 + ((k * 7) % 3) * 0.004
    M.place(t, M.cyl(nil, 0.0042, 0.004, 0, hh, 5, 6, 0.6, 0.95, 0), x, -0.004, 0, 1)
    M.place(t, M.cone(nil, hh, hh + 0.01, 0.0042, 5, 6, 0.75, 1.0, 0), x, -0.004, 0, 1)
  end
  M.place(t, M.box(nil, 0.1, 0.004, 0.003, 6, 0.7, 0.9, true), 0, 0.03, -0.005, 1)
  N.palisade = t
  -- felled logs on the ground (a woodcutters' clearing)
  t = M.new()
  for i = 0, 1 do
    M.place(t, M.cyl(nil, 0.004, 0.0035, -0.02, 0.02, 5, 6, 0.6, 0.9, 0, true), 0.004 * i, 0.004 + i * 0.006, -0.004 + i * 0.006, 1, 1, 1, 0.2 * i, pi / 2, 0)
  end
  N.logs = t
end

local function build_far()
  local F = M.T[2]
  local t
  M.seed(31)
  t = M.new()
  tree_crown(t, { { 0, 0.125, 0, 0.07, 0.068, 0.5 } }, 1, 6, 2, 0.12)
  M.cyl(t, 0.012, 0.009, -0.02, 0.07, 3, 2, 0.5, 0.7, 0)
  F.oak = t
  t = M.new(); tree_crown(t, { { 0, 0.13, 0, 0.04, 0.075, 0.58 } }, 1, 6, 2, 0.1)
  F.birch = t
  t = M.new(); M.cone(t, 0.01, 0.235, 0.062, 6, 1, 0.55, 1.0, 0.8, 0, 0.012)
  F.spruce = t
  t = M.new(); M.cone(t, 0.01, 0.235, 0.062, 5, 1, 0.55, 1.0, 0.8, 0, 0.012); M.cone(t, 0.12, 0.24, 0.036, 4, 3, 0.75, 1.0, 1, 0.5, 0.006)
  F.snowpine = t
  t = M.new(); M.cyl(t, 0.009, 0.006, -0.02, 0.17, 3, 2, 0.55, 0.85, 0)
  tree_crown(t, { { 0, 0.19, 0, 0.055, 0.03, 0.6 } }, 1, 5, 2, 0.12)
  F.pine = t
  t = M.new(); M.place(t, M.cyl(nil, 0.009, 0.007, 0, 0.19, 3, 2, 0.6, 0.85, 0.3), 0, -0.02, 0, 1, 1, 1, 0, 0, -0.2)
  for k = 0, 3 do
    local a = k / 4 * 2 * pi + 0.3
    local ca, sa = cos(a), sin(a)
    local b = { 0.036, 0.16, 0 }
    tri(t, b, { 0.036 + ca * 0.09 - sa * 0.02, 0.15, sa * 0.09 + ca * 0.02 }, { 0.036 + ca * 0.09 + sa * 0.02, 0.13, sa * 0.09 - ca * 0.02 },
      nil, nil, nil, 1, 0.8, 1.0, 0.9, 0.3, 1, 1, 0, 1, 0)
  end
  F.palm = t
  t = M.new(); M.cyl(t, 0.014, 0.009, -0.02, 0.17, 3, 2, 0.5, 0.8, 0)
  tree_crown(t, { { 0, 0.2, 0, 0.085, 0.042, 0.55 } }, 1, 6, 2, 0.12)
  F.jungle = t
  t = M.new(); M.place(t, M.cyl(nil, 0.009, 0.006, 0, 0.13, 3, 2, 0.55, 0.85, 0), 0, -0.02, 0, 1, 1, 1, 0, 0, 0.15)
  tree_crown(t, { { 0.02, 0.135, 0, 0.078, 0.022, 0.6 } }, 1, 6, 2, 0.08)
  F.acacia = t
  t = M.new(); M.cyl(t, 0.010, 0.004, -0.02, 0.15, 3, 1, 0.6, 0.85, 0)
  F.snag = t
  F.stump = M.T[1].stump
  t = M.new(); tree_crown(t, { { 0, 0.02, 0, 0.044, 0.034, 0.5 } }, 1, 5, 2, 0.15)
  F.bush = t
  t = M.new(); M.place(t, M.lump(nil, 0.036, 0.024, 0.03, 5, 2, 1, 0.6, 1.0, 0, 0.2), 0, 0.006, 0, 1)
  F.rock = t
  t = M.new(); M.cyl(t, 0.011, 0.010, -0.01, 0.08, 4, 1, 0.6, 0.95, 0, true)
  F.cactus = t
  -- buildings: simple boxes + roofs (same footprint as near)
  t = M.new(); M.gable_house(t, 0.07, 0.05, 0.042, 0.032, 0.006, 0.004, 2, 1)
  F.cottage = t
  t = M.new(); M.gable_house(t, 0.11, 0.05, 0.032, 0.036, 0.006, 0.004, 2, 1)
  F.longhouse = t
  t = M.new(); M.gable_house(t, 0.066, 0.052, 0.07, 0.04, 0.006, 0.004, 2, 1)
  F.townhouse = t
  t = M.new(); M.cyl(t, 0.03, 0.028, -0.01, 0.034, 6, 2, 0.62, 0.95, 0); M.cone(t, 0.028, 0.085, 0.042, 6, 1, 0.7, 1.0, 0, 0, 0.004)
  F.hut = t
  t = M.new(); M.cyl(t, 0.022, 0.022, -0.01, 0.05, 6, 5, 0.65, 0.95, 0); M.cone(t, 0.048, 0.085, 0.03, 6, 1, 0.7, 1.0, 0, 0, 0.003)
  F.granary = t
  t = M.new(); M.gable_house(t, 0.12, 0.07, 0.05, 0.045, 0.006, 0.005, 7, 1)
  M.place(t, M.box(nil, 0.026, 0.07, 0.026, 7, 0.75, 1.0, false), 0.07, 0.035, 0, 1)
  F.hall = t
  F.ruin = M.T[1].ruin
  F.well = M.T[1].well
  F.pier = M.T[1].pier
  F.haystack = M.T[1].haystack
  F.woodpile = M.T[1].woodpile
  for _, n in ipairs({ "stakes", "frame", "walls", "sodhouse", "woodshed", "sacks", "fishrack", "hull", "stook", "logs", "palisade", "kiln", "pots", "pen" }) do
    F[n] = M.T[1][n]
  end
  -- ground cover is near-only (far LOD skips these kinds)
end

---------------------------------------------------------------------------------------------
-- Movers (single colour sets per pool; people/animals get more parts than before)

-- Villager, ~0.05 tall at scale 1: legs (slot 10 dark), tunic (slot 1 = job colour), arms
-- (slot 1), head (slot 2 = skin), hair or hat (slot 3). Limb vertices carry sway = limb code
-- for the walk cycle: 0.25/0.5 left/right leg, 0.75/1.0 left/right arm (props_ws.vp).
function M.person(hat, load)
  -- forward = +x (like the quadrupeds), legs and arms side by side along z
  local t = M.new()
  local function limb(src, code)
    for k = 0, src.nv - 1 do src.d[k * 9 + 9] = code end
    return src
  end
  M.place(t, limb(M.box(nil, 0.007, 0.02, 0.006, 10, 0.55, 0.8, false), 0.25), 0, 0.009, -0.0045, 1)
  M.place(t, limb(M.box(nil, 0.007, 0.02, 0.006, 10, 0.55, 0.8, false), 0.5), 0, 0.009, 0.0045, 1)
  -- tunic: slightly flared body
  M.place(t, M.cyl(nil, 0.0102, 0.0076, 0.0, 0.021, 6, 1, 0.65, 0.95, 0, false), 0, 0.016, 0, 1)
  M.place(t, limb(M.box(nil, 0.0045, 0.017, 0.0045, 1, 0.6, 0.9, false), 0.75), 0, 0.027, -0.0105, 1, 1, 1, 0, -0.08, 0)
  M.place(t, limb(M.box(nil, 0.0045, 0.017, 0.0045, 1, 0.6, 0.9, false), 1.0), 0, 0.027, 0.0105, 1, 1, 1, 0, 0.08, 0)
  M.place(t, M.lump(nil, 0.0082, 0.0088, 0.0082, 6, 3, 2, 0.8, 1.0, 0, 0), 0, 0.0455, 0, 1)
  if hat == "straw" then
    M.place(t, M.cone(nil, 0.0, 0.0015, 0.013, 7, 3, 0.8, 1.0, 0, 0, 0), 0, 0.051, 0, 1)
    M.place(t, M.cone(nil, 0.0, 0.007, 0.0075, 6, 3, 0.85, 1.0, 0, 0, 0), 0, 0.052, 0, 1)
  elseif hat == "cap" then
    M.place(t, M.lump(nil, 0.0088, 0.005, 0.0088, 7, 2, 3, 0.8, 1.0, 0, 0), 0, 0.051, 0, 1)
  else
    M.place(t, M.lump(nil, 0.0088, 0.006, 0.0086, 7, 2, 3, 0.7, 0.95, 0, 0), -0.0008, 0.0492, 0, 1)
  end
  if load == "spear" then
    -- a spear held upright and a round shield (raiders)
    M.place(t, M.cyl(nil, 0.0012, 0.001, 0, 0.075, 4, 13, 0.7, 0.95, 0), 0.008, 0.005, 0.013, 1)
    M.place(t, M.cone(nil, 0.075, 0.087, 0.0028, 4, 5, 0.8, 1.0, 0), 0.008, 0.005, 0.013, 1)
    M.place(t, M.cyl(nil, 0.009, 0.009, 0, 0.002, 7, 13, 0.7, 0.95, 0, true), 0.004, 0.028, -0.012, 1, 1, 1, 0, math.pi / 2, 0)
  elseif load == "sack" then
    -- a sack on the back (porters carrying grain, tools, pots or cloth)
    M.place(t, M.lump(nil, 0.0085, 0.0115, 0.0095, 6, 3, 12, 0.7, 1.0, 0, 0.12), -0.0105, 0.036, 0, 1)
  elseif load == "logs" then
    -- a bundle of logs across the shoulders
    for k = -1, 1 do
      M.place(t, M.cyl(nil, 0.0028, 0.0028, -0.014, 0.014, 5, 13, 0.6, 0.95, 0, true), -0.006, 0.044 + (k == 0 and 0.004 or 0), k * 0.004, 1, 1, 1, 0, math.pi / 2, 0)
    end
  end
  t.hip, t.shoulder = 0.019, 0.035
  return t
end

-- Quadruped along +x: body lump, neck, head, 4 legs (limb codes as for people), tail.
-- deer: slender legs, antlers (slot 3); wolf: shorter legs, pointy ears, bushy tail.
function M.quadruped(kind)
  if kind == "boar" or kind == "hare" or kind == "cat" or kind == "bear" then return M.beast(kind) end
  if kind == "sheep" or kind == "goat" or kind == "cow" then return M.stock(kind) end
  local t = M.new()
  local deer = kind == "deer"
  local function limb(src, code)
    for k = 0, src.nv - 1 do src.d[k * 9 + 9] = code end
    return src
  end
  local lh = deer and 0.034 or 0.026
  M.place(t, M.lump(nil, 0.03, 0.017, 0.016, 7, 3, 1, 0.6, 1.0, 0, 0.04), 0, lh + 0.012, 0, 1)
  local codes = { 0.25, 0.5, 0.5, 0.25 }
  for i, lp in ipairs({ { 0.02, 0.009 }, { 0.02, -0.009 }, { -0.02, 0.009 }, { -0.02, -0.009 } }) do
    M.place(t, limb(M.cyl(nil, 0.0038, 0.003, -lh, 0.0, 4, 2, 0.6, 0.85, 0), codes[i]), lp[1], lh + 0.004, lp[2], 1)
  end
  if deer then
    M.place(t, M.cyl(nil, 0.006, 0.005, 0, 0.026, 5, 1, 0.7, 0.95, 0), 0.026, lh + 0.018, 0, 1, 1, 1, 0, 0, -0.45)
    M.place(t, M.lump(nil, 0.012, 0.0085, 0.0075, 6, 3, 1, 0.75, 1.0, 0, 0), 0.042, lh + 0.042, 0, 1, 1, 1, 0, 0, -0.35)
    for _, s in ipairs({ -1, 1 }) do
      M.place(t, M.cyl(nil, 0.0015, 0.001, 0, 0.02, 3, 3, 0.8, 1.0, 0), 0.038, lh + 0.048, s * 0.004, 1, 1, 1, 0, s * 0.4, 0.3)
      M.place(t, M.cyl(nil, 0.0012, 0.0008, 0, 0.009, 3, 3, 0.8, 1.0, 0), 0.040, lh + 0.058, s * 0.008, 1, 1, 1, 0, s * 0.3, -0.5)
    end
    M.place(t, M.lump(nil, 0.005, 0.006, 0.004, 5, 2, 7, 0.8, 1.0, 0, 0), -0.03, lh + 0.02, 0, 1)
  else
    M.place(t, M.lump(nil, 0.013, 0.0095, 0.0085, 6, 3, 1, 0.75, 1.0, 0, 0), 0.034, lh + 0.022, 0, 1)
    M.place(t, M.cone(nil, 0.0, 0.012, 0.0045, 5, 1, 0.7, 1.0, 0, 0, 0), 0.047, lh + 0.02, 0, 1, 1, 1, 0, 0, -1.4)
    for _, s in ipairs({ -1, 1 }) do
      M.place(t, M.cone(nil, 0.0, 0.009, 0.0035, 4, 1, 0.75, 1.0, 0, 0, 0), 0.03, lh + 0.03, s * 0.005, 1)
    end
    M.place(t, M.lump(nil, 0.016, 0.006, 0.006, 6, 2, 1, 0.7, 1.0, 0, 0.05), -0.036, lh + 0.008, 0, 1, 1, 1, 0, 0, 0.5)
  end
  t.hip, t.shoulder = lh + 0.004, lh + 0.004
  return t
end

-- More wild animals (view species by biome, D-058): a boar (low, bristly, tusks), a hare, a big
-- cat (long body, long tail), a bear (heavy, short-legged). Same limb codes as the quadrupeds.
function M.beast(kind)
  local t = M.new()
  local function limb(src, code)
    for k = 0, src.nv - 1 do src.d[k * 9 + 9] = code end
    return src
  end
  local lh, bx, by, bz, legr
  if kind == "boar" then lh, bx, by, bz, legr = 0.016, 0.028, 0.016, 0.014, 0.0042
  elseif kind == "hare" then lh, bx, by, bz, legr = 0.008, 0.012, 0.008, 0.007, 0.002
  elseif kind == "cat" then lh, bx, by, bz, legr = 0.026, 0.032, 0.012, 0.011, 0.0034
  else lh, bx, by, bz, legr = 0.02, 0.036, 0.024, 0.021, 0.0065 end
  M.place(t, M.lump(nil, bx, by, bz, 7, 3, 1, 0.6, 1.0, 0, kind == "boar" and 0.12 or 0.05), 0, lh + by * 0.8, 0, 1)
  local codes = { 0.25, 0.5, 0.5, 0.25 }
  local lx, lz = bx * 0.62, bz * 0.55
  for i, lp in ipairs({ { lx, lz }, { lx, -lz }, { -lx, lz }, { -lx, -lz } }) do
    M.place(t, limb(M.cyl(nil, legr, legr * 0.8, -lh, 0.0, 4, 2, 0.6, 0.85, 0), codes[i]), lp[1], lh + 0.003, lp[2], 1)
  end
  if kind == "boar" then
    M.place(t, M.lump(nil, 0.013, 0.011, 0.01, 6, 3, 1, 0.65, 1.0, 0, 0.08), bx * 0.95, lh + by * 0.7, 0, 1, 1, 1, 0, 0, -0.35)
    M.place(t, M.cyl(nil, 0.0045, 0.004, 0, 0.008, 5, 10, 0.6, 0.9, 0, true), bx * 1.35, lh + by * 0.45, 0, 1, 1, 1, 0, 0, -1.5)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.cone(nil, 0, 0.006, 0.0012, 4, 7, 0.85, 1.0, 0), bx * 1.3, lh + by * 0.4, sgn * 0.004, 1, 1, 1, 0, 0, -0.9)
      M.place(t, M.cone(nil, 0, 0.007, 0.0025, 4, 1, 0.7, 1.0, 0), bx * 0.85, lh + by * 1.25, sgn * 0.005, 1)
    end
    M.place(t, M.box(nil, bx * 1.2, 0.004, 0.003, 10, 0.5, 0.8, false), 0, lh + by * 1.65, 0, 1)   -- bristly back
  elseif kind == "hare" then
    M.place(t, M.lump(nil, 0.006, 0.006, 0.005, 6, 3, 1, 0.7, 1.0, 0, 0), bx * 0.9, lh + by * 1.6, 0, 1)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.lump(nil, 0.0015, 0.008, 0.0028, 4, 2, 1, 0.75, 1.0, 0, 0), bx * 0.75, lh + by * 2.6, sgn * 0.0025, 1, 1, 1, 0, sgn * 0.15, 0.2)
    end
    M.place(t, M.lump(nil, 0.003, 0.003, 0.003, 4, 2, 7, 0.9, 1.0, 0, 0), -bx * 1.05, lh + by * 1.1, 0, 1)
  elseif kind == "cat" then
    M.place(t, M.lump(nil, 0.011, 0.0095, 0.0085, 6, 3, 1, 0.75, 1.0, 0, 0), bx * 1.1, lh + by * 1.6, 0, 1)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.cone(nil, 0, 0.005, 0.003, 4, 1, 0.7, 1.0, 0), bx * 1.05, lh + by * 2.3, sgn * 0.005, 1)
    end
    M.place(t, M.cyl(nil, 0.0022, 0.0016, 0, 0.034, 4, 1, 0.65, 0.95, 0.6), -bx * 0.95, lh + by * 0.9, 0, 1, 1, 1, 0, 0, 2.2)
    M.place(t, M.lump(nil, 0.0035, 0.0035, 0.0035, 4, 2, 3, 0.8, 1.0, 0, 0), -bx * 1.75, lh + by * 0.2, 0, 1)
  else
    M.place(t, M.lump(nil, 0.016, 0.014, 0.013, 6, 3, 1, 0.7, 1.0, 0, 0.05), bx * 1.05, lh + by * 1.15, 0, 1)
    M.place(t, M.lump(nil, 0.006, 0.005, 0.005, 5, 2, 10, 0.6, 0.9, 0, 0), bx * 1.45, lh + by * 1.0, 0, 1)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.lump(nil, 0.004, 0.004, 0.0025, 5, 2, 1, 0.7, 1.0, 0, 0), bx * 0.95, lh + by * 1.75, sgn * 0.009, 1)
    end
  end
  t.hip, t.shoulder = lh + 0.003, lh + 0.003
  return t
end

-- Livestock (W10): a woolly sheep (slot 1 fleece, slot 2 face and legs), a goat with horns, a cow
-- with patches (slot 3) and horns. Limb codes as the quadrupeds.
function M.stock(kind)
  local t = M.new()
  local function limb(src, code)
    for k = 0, src.nv - 1 do src.d[k * 9 + 9] = code end
    return src
  end
  local lh, bx, by, bz, legr
  if kind == "sheep" then lh, bx, by, bz, legr = 0.012, 0.02, 0.015, 0.014, 0.0025
  elseif kind == "goat" then lh, bx, by, bz, legr = 0.016, 0.018, 0.011, 0.009, 0.0022
  else lh, bx, by, bz, legr = 0.024, 0.034, 0.019, 0.016, 0.0045 end
  M.place(t, M.lump(nil, bx, by, bz, 7, 3, 1, 0.62, 1.0, 0, kind == "sheep" and 0.2 or 0.04), 0, lh + by * 0.75, 0, 1)
  local codes = { 0.25, 0.5, 0.5, 0.25 }
  local lx, lz = bx * 0.6, bz * 0.5
  for i, lp in ipairs({ { lx, lz }, { lx, -lz }, { -lx, lz }, { -lx, -lz } }) do
    M.place(t, limb(M.cyl(nil, legr, legr * 0.85, -lh, 0.0, 4, 2, 0.6, 0.85, 0), codes[i]), lp[1], lh + 0.003, lp[2], 1)
  end
  if kind == "sheep" then
    M.place(t, M.lump(nil, 0.0075, 0.0065, 0.0055, 6, 3, 2, 0.7, 1.0, 0, 0), bx * 1.05, lh + by * 1.1, 0, 1, 1, 1, 0, 0, -0.3)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.lump(nil, 0.0022, 0.0012, 0.0045, 4, 2, 2, 0.7, 1.0, 0, 0), bx * 0.95, lh + by * 1.35, sgn * 0.006, 1)
    end
  elseif kind == "goat" then
    M.place(t, M.lump(nil, 0.0065, 0.006, 0.005, 6, 3, 1, 0.75, 1.0, 0, 0), bx * 1.2, lh + by * 1.9, 0, 1, 1, 1, 0, 0, -0.5)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.cone(nil, 0, 0.011, 0.0016, 4, 3, 0.8, 1.0, 0), bx * 1.1, lh + by * 2.4, sgn * 0.0025, 1, 1, 1, 0, sgn * 0.2, 0.7)
    end
    M.place(t, M.lump(nil, 0.0018, 0.004, 0.0016, 4, 2, 2, 0.7, 1.0, 0, 0), bx * 1.55, lh + by * 1.4, 0, 1)
  else
    M.place(t, M.lump(nil, bx * 0.55, by * 0.75, bz * 0.9, 6, 3, 3, 0.65, 1.0, 0, 0.1), -bx * 0.25, lh + by * 1.05, 0.004, 1)
    M.place(t, M.lump(nil, 0.011, 0.01, 0.009, 6, 3, 1, 0.72, 1.0, 0, 0), bx * 1.1, lh + by * 1.0, 0, 1, 1, 1, 0, 0, -0.4)
    M.place(t, M.lump(nil, 0.006, 0.005, 0.0065, 5, 2, 7, 0.8, 1.0, 0, 0), bx * 1.45, lh + by * 0.75, 0, 1)
    for _, sgn in ipairs({ -1, 1 }) do
      M.place(t, M.cone(nil, 0, 0.008, 0.0016, 4, 7, 0.85, 1.0, 0), bx * 1.1, lh + by * 1.5, sgn * 0.007, 1, 1, 1, 0, sgn * 1.2, 0)
    end
    M.place(t, M.cyl(nil, 0.0012, 0.0008, 0, 0.018, 4, 1, 0.6, 0.9, 0.4), -bx * 0.95, lh + by * 1.2, 0, 1, 1, 1, 0, 0, 2.6)
  end
  t.hip, t.shoulder = lh + 0.003, lh + 0.003
  return t
end

-- A dolphin: a sleek grey body with a dorsal fin and a tail, along +x (it arcs out of the sea).
function M.dolphin()
  local t = M.new()
  M.place(t, M.lump(nil, 0.034, 0.009, 0.0085, 8, 3, 1, 0.65, 1.0, 0, 0), 0, 0, 0, 1)
  M.place(t, M.cone(nil, 0, 0.012, 0.004, 4, 1, 0.7, 1.0, 0), 0.028, 0, 0, 1, 1, 1, 0, 0, -1.57)
  M.place(t, M.cone(nil, 0.006, 0.019, 0.0055, 4, 1, 0.6, 0.95, 0), -0.002, 0, 0, 1, 1, 1, 0, 0, 0.35)
  M.tri(t, { -0.03, 0, 0 }, { -0.044, 0.002, 0.01 }, { -0.044, 0.002, -0.01 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 0, 1, 0)
  M.place(t, M.lump(nil, 0.024, 0.005, 0.006, 6, 2, 3, 0.85, 1.0, 0, 0), 0.004, -0.004, 0, 1)
  return t
end

function M.build()
  if M.built then return end
  build_near()
  build_far()
  M.built = true
end

--- Vertex counts per template (for budgets / the debug overlay).
function M.stats()
  local out = {}
  for lod = 1, 2 do
    for name, t in pairs(M.T[lod]) do out[#out + 1] = string.format("%s%d=%d", name, lod, t.nv) end
  end
  table.sort(out)
  return table.concat(out, " ")
end

return M
