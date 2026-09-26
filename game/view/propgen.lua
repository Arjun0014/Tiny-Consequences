-- Procedural low-poly props and chunk placement (Stage 1.8, DECISIONS D-009). Pure Lua 5.1.
--
-- Templates are built once in a local frame (y = up along the surface normal, x/z tangent),
-- as flat triangle lists (stride 9: x y z nx ny nz slot shade sway). Chunk placement turns
-- sim values into a flat instance list (reused arrays, no per-prop tables), and write()
-- bakes every instance into planet-space vertex streams: position(3) normal(3) color(4).
-- Placement is stable: each land face owns a fixed set of slots with fixed random positions,
-- so density changes add/remove trees in place instead of reshuffling the forest.
local M = {}

local sqrt, floor, min, max, abs, cos, sin, pi = math.sqrt, math.floor, math.min, math.max,
  math.abs, math.cos, math.sin, math.pi

M.SCALE = 1.3          -- global prop scale, near LOD (06: trees 0.12-0.2 at R = 10; "chunky")
M.SCALE_FAR = 1.85     -- far LOD props are bigger and fewer so forests still read from orbit
M.SLOTS = 5            -- prop slots per render face (level 5 face area ~0.061 units^2)
M.STRIDE = 9

-- Kinds
local K = { tree = 1, pine = 2, palm = 3, bush = 4, rock = 5, crop = 6, house = 7, hut = 8,
  ruin = 9, stump = 10, snowpine = 11, jungle = 12, tuft = 13 }
M.K = K

---------------------------------------------------------------------------------------------
-- Template builder

local function norm3(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-9 then return 0, 1, 0 end
  return x / l, y / l, z / l
end

local function new_tpl() return { d = {}, nv = 0 } end

local function push(t, x, y, z, nx, ny, nz, slot, shade, sway)
  local d, o = t.d, t.nv * 9
  d[o + 1], d[o + 2], d[o + 3] = x, y, z
  d[o + 4], d[o + 5], d[o + 6] = nx, ny, nz
  d[o + 7], d[o + 8], d[o + 9] = slot, shade, sway
  t.nv = t.nv + 1
end

-- Triangle with per-vertex normals; winding fixed so the face normal points along `out`.
local function tri(t, a, b, c, na, nb, nc, slot, shades, sways, ox, oy, oz)
  local ux, uy, uz = b[1] - a[1], b[2] - a[2], b[3] - a[3]
  local vx, vy, vz = c[1] - a[1], c[2] - a[2], c[3] - a[3]
  local fx, fy, fz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
  if fx * ox + fy * oy + fz * oz < 0 then
    b, c, nb, nc = c, b, nc, nb
    if shades then shades = { shades[1], shades[3], shades[2] } end
    if sways then sways = { sways[1], sways[3], sways[2] } end
  end
  if not na then
    local nx, ny, nz = norm3(fx, fy, fz)
    if fx * ox + fy * oy + fz * oz < 0 then nx, ny, nz = -nx, -ny, -nz end
    na = { nx, ny, nz }; nb = na; nc = na
  end
  local s1, s2, s3 = 1, 1, 1
  if shades then s1, s2, s3 = shades[1], shades[2], shades[3] end
  local w1, w2, w3 = 0, 0, 0
  if sways then w1, w2, w3 = sways[1], sways[2], sways[3] end
  push(t, a[1], a[2], a[3], na[1], na[2], na[3], slot, s1, w1)
  push(t, b[1], b[2], b[3], nb[1], nb[2], nb[3], slot, s2, w2)
  push(t, c[1], c[2], c[3], nc[1], nc[2], nc[3], slot, s3, w3)
end

local function shade_of(y, y0, y1, lo)
  local t = (y - y0) / (y1 - y0)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return lo + (1 - lo) * t
end

-- Soft blob (bipyramid with a mid ring pair): centre cy, radii rx/ry, `sides` around.
local function blob(t, cy, rx, ry, sides, slot, lo, sway, rings)
  local top = { 0, cy + ry, 0 }
  local bot = { 0, cy - ry * 0.85, 0 }
  local function sn(p)
    return { norm3(p[1] / (rx * rx), (p[2] - cy) / (ry * ry), p[3] / (rx * rx)) }
  end
  local ys = { cy - ry, cy + ry }
  local function sh(p) return shade_of(p[2], ys[1], ys[2], lo) end
  local function sw(p) return sway * shade_of(p[2], ys[1], ys[2], 0) end
  local ringsets = {}
  if rings == 2 then
    ringsets[1] = { y = cy + ry * 0.5, r = 0.87, off = 0.5 }
    ringsets[2] = { y = cy - ry * 0.1, r = 1.0, off = 0 }
  else
    ringsets[1] = { y = cy, r = 1.0, off = 0 }
  end
  local R = {}
  for ri, rs in ipairs(ringsets) do
    R[ri] = {}
    for k = 0, sides - 1 do
      local a = (k + rs.off) / sides * 2 * pi
      R[ri][k + 1] = { rx * rs.r * cos(a), rs.y, rx * rs.r * sin(a) }
    end
  end
  local function face(a, b, c)
    local ox, oy, oz = (a[1] + b[1] + c[1]) / 3, (a[2] + b[2] + c[2]) / 3 - cy, (a[3] + b[3] + c[3]) / 3
    tri(t, a, b, c, sn(a), sn(b), sn(c), slot, { sh(a), sh(b), sh(c) }, { sw(a), sw(b), sw(c) }, ox, oy, oz)
  end
  local first, last = R[1], R[#R]
  for k = 1, sides do
    local k2 = k % sides + 1
    face(top, first[k], first[k2])
    face(bot, last[k2], last[k])
  end
  if #R == 2 then
    local A, Bq = R[1], R[2]
    for k = 1, sides do
      local k2 = k % sides + 1
      face(A[k], Bq[k], A[k2])
      face(A[k2], Bq[k], Bq[k2])
    end
  end
end

-- Cone (no base): base ring at y0 radius r, apex at y1.
local function cone(t, y0, y1, r, sides, slot, lo, sway, rot)
  local apex = { 0, y1, 0 }
  local slope = r / (y1 - y0)
  for k = 0, sides - 1 do
    local a0 = (k + (rot or 0)) / sides * 2 * pi
    local a1 = (k + 1 + (rot or 0)) / sides * 2 * pi
    local p0 = { r * cos(a0), y0, r * sin(a0) }
    local p1 = { r * cos(a1), y0, r * sin(a1) }
    local n0 = { norm3(cos(a0), slope, sin(a0)) }
    local n1 = { norm3(cos(a1), slope, sin(a1)) }
    local am = (a0 + a1) * 0.5
    local na = { norm3(cos(am), slope, sin(am)) }
    tri(t, apex, p0, p1, na, n0, n1, slot, { 1, lo, lo }, { sway, sway * 0.4, sway * 0.4 },
      cos(am), 0.3, sin(am))
  end
end

-- Box walls (+ optional top), axis-aligned in the local frame; flat normals.
local function box(t, x0, x1, y0, y1, z0, z1, slot, with_top, lo)
  local cx, cz = (x0 + x1) * 0.5, (z0 + z1) * 0.5
  local cy = (y0 + y1) * 0.5
  local function quad(a, b, c, d, ox, oy, oz)
    tri(t, a, b, c, nil, nil, nil, slot, { shade_of(a[2], y0, y1, lo), shade_of(b[2], y0, y1, lo), shade_of(c[2], y0, y1, lo) }, nil, ox, oy, oz)
    tri(t, a, c, d, nil, nil, nil, slot, { shade_of(a[2], y0, y1, lo), shade_of(c[2], y0, y1, lo), shade_of(d[2], y0, y1, lo) }, nil, ox, oy, oz)
  end
  quad({ x0, y0, z0 }, { x1, y0, z0 }, { x1, y1, z0 }, { x0, y1, z0 }, 0, 0, -1)
  quad({ x1, y0, z1 }, { x0, y0, z1 }, { x0, y1, z1 }, { x1, y1, z1 }, 0, 0, 1)
  quad({ x0, y0, z1 }, { x0, y0, z0 }, { x0, y1, z0 }, { x0, y1, z1 }, -1, 0, 0)
  quad({ x1, y0, z0 }, { x1, y0, z1 }, { x1, y1, z1 }, { x1, y1, z0 }, 1, 0, 0)
  if with_top then
    quad({ x0, y1, z0 }, { x1, y1, z0 }, { x1, y1, z1 }, { x0, y1, z1 }, 0, 1, 0)
  end
  return cx, cy, cz
end

-- Gable roof over [x0,x1] x [z0,z1] from eave height y0 to ridge y1 (ridge along x).
local function gable(t, x0, x1, y0, y1, z0, z1, slot)
  local zm = (z0 + z1) * 0.5
  local over = (z1 - z0) * 0.12
  local a, b = { x0 - over * 0.5, y0 - over * 0.4, z0 - over }, { x1 + over * 0.5, y0 - over * 0.4, z0 - over }
  local c, d = { x1 + over * 0.5, y1, zm }, { x0 - over * 0.5, y1, zm }
  tri(t, a, b, c, nil, nil, nil, slot, { 0.85, 0.85, 1 }, nil, 0, 0.6, -1)
  tri(t, a, c, d, nil, nil, nil, slot, { 0.85, 1, 1 }, nil, 0, 0.6, -1)
  local e, f = { x1 + over * 0.5, y0 - over * 0.4, z1 + over }, { x0 - over * 0.5, y0 - over * 0.4, z1 + over }
  tri(t, e, f, d, nil, nil, nil, slot, { 0.85, 0.85, 1 }, nil, 0, 0.6, 1)
  tri(t, e, d, c, nil, nil, nil, slot, { 0.85, 1, 1 }, nil, 0, 0.6, 1)
  -- gables (wall colour slot 2)
  tri(t, { x0, y0, z0 }, { x0, y0, z1 }, { x0, y1 - over * 0.3, zm }, nil, nil, nil, 2, { 0.9, 0.9, 1 }, nil, -1, 0, 0)
  tri(t, { x1, y0, z1 }, { x1, y0, z0 }, { x1, y1 - over * 0.3, zm }, nil, nil, nil, 2, { 0.9, 0.9, 1 }, nil, 1, 0, 0)
end

-- Flat-ish leaf (double-sided triangle pair) from (0,y0,0) outward along angle a, drooping.
local function frond(t, y0, len, a, droop, width, slot)
  local ca, sa = cos(a), sin(a)
  local base = { 0, y0, 0 }
  local mid = { ca * len * 0.55, y0 + len * 0.12, sa * len * 0.55 }
  local tip = { ca * len, y0 - droop, sa * len }
  local px, pz = -sa * width, ca * width
  local l = { mid[1] + px, mid[2], mid[3] + pz }
  local r = { mid[1] - px, mid[2], mid[3] - pz }
  tri(t, base, l, tip, nil, nil, nil, slot, { 0.8, 1, 0.95 }, { 0.3, 0.8, 1 }, 0, 1, 0)
  tri(t, base, tip, r, nil, nil, nil, slot, { 0.8, 0.95, 1 }, { 0.3, 1, 0.8 }, 0, 1, 0)
  tri(t, base, tip, l, nil, nil, nil, slot, { 0.7, 0.85, 0.9 }, { 0.3, 1, 0.8 }, 0, -1, 0)
  tri(t, base, r, tip, nil, nil, nil, slot, { 0.7, 0.9, 0.85 }, { 0.3, 0.8, 1 }, 0, -1, 0)
end

---------------------------------------------------------------------------------------------
-- Templates (heights in world units before M.SCALE and per-instance scale).
-- TPL[lod][kind]: lod 1 = near (rounder, trunks), lod 2 = far (cheap; most of the planet).

local TPL = { {}, {} }
M.TPL = TPL

local function build_templates()
  local t
  local N, F = TPL[1], TPL[2]
  -- round deciduous tree
  t = new_tpl(); cone(t, -0.02, 0.11, 0.016, 3, 2, 0.6, 0); blob(t, 0.12, 0.062, 0.058, 7, 1, 0.62, 1, 1)
  N[K.tree] = t
  t = new_tpl(); blob(t, 0.11, 0.064, 0.066, 5, 1, 0.7, 1, 1)
  F[K.tree] = t
  -- jungle tree: taller, flatter, darker crown
  t = new_tpl(); cone(t, -0.02, 0.15, 0.018, 3, 2, 0.6, 0); blob(t, 0.155, 0.075, 0.05, 6, 1, 0.6, 1, 1)
  N[K.jungle] = t
  t = new_tpl(); blob(t, 0.14, 0.076, 0.056, 5, 1, 0.68, 1, 1)
  F[K.jungle] = t
  -- pine: trunk + two stacked cones
  t = new_tpl(); cone(t, -0.02, 0.06, 0.014, 3, 2, 0.6, 0)
  cone(t, 0.035, 0.15, 0.052, 5, 1, 0.62, 0.6); cone(t, 0.1, 0.215, 0.036, 5, 1, 0.75, 1, 0.5)
  N[K.pine] = t
  t = new_tpl(); cone(t, 0.0, 0.2, 0.05, 5, 1, 0.6, 1)
  F[K.pine] = t
  -- snowy pine: white upper cone (slot 3 = snow)
  t = new_tpl(); cone(t, -0.02, 0.06, 0.014, 3, 2, 0.6, 0)
  cone(t, 0.035, 0.15, 0.052, 5, 1, 0.62, 0.6); cone(t, 0.1, 0.215, 0.036, 5, 3, 0.8, 1, 0.5)
  N[K.snowpine] = t
  t = new_tpl(); cone(t, 0.0, 0.12, 0.05, 4, 1, 0.6, 0.6); cone(t, 0.08, 0.2, 0.034, 4, 3, 0.8, 1, 0.5)
  F[K.snowpine] = t
  -- palm: slender trunk + drooping fronds
  t = new_tpl(); cone(t, -0.02, 0.19, 0.014, 3, 2, 0.7, 0.3)
  for k = 0, 4 do frond(t, 0.18, 0.09, k / 5 * 2 * pi + 0.3, 0.045, 0.022, 1) end
  N[K.palm] = t
  t = new_tpl(); cone(t, -0.02, 0.19, 0.016, 3, 2, 0.7, 0.3)
  for k = 0, 2 do frond(t, 0.18, 0.1, k / 3 * 2 * pi + 0.3, 0.045, 0.03, 1) end
  F[K.palm] = t
  -- bush (top fan only; the ground hides the rest)
  t = new_tpl(); cone(t, -0.005, 0.05, 0.048, 5, 1, 0.65, 0.5)
  N[K.bush] = t
  t = new_tpl(); cone(t, -0.005, 0.05, 0.05, 4, 1, 0.65, 0.5)
  F[K.bush] = t
  -- grass tuft (three blades; near only)
  t = new_tpl()
  for k = 0, 2 do
    local a = k / 3 * 2 * pi
    local ca, sa = cos(a), sin(a)
    tri(t, { -sa * 0.01, 0, ca * 0.01 }, { sa * 0.01, 0, -ca * 0.01 }, { ca * 0.02, 0.05, sa * 0.02 },
      nil, nil, nil, 1, { 0.7, 0.7, 1 }, { 0, 0, 1 }, ca, 0.3, sa)
  end
  N[K.tuft] = t
  F[K.tuft] = t
  -- rock: low faceted mound
  t = new_tpl(); cone(t, -0.01, 0.028, 0.036, 5, 1, 0.7, 0)
  N[K.rock] = t
  t = new_tpl(); cone(t, -0.01, 0.028, 0.036, 4, 1, 0.7, 0)
  F[K.rock] = t
  -- burnt stump
  t = new_tpl(); cone(t, -0.02, 0.05, 0.014, 3, 1, 0.7, 0)
  N[K.stump] = t
  F[K.stump] = t
  -- crop field tile: soil slab (slot 2) + 3 ridged crop rows (slot 1); tiles form a patchwork
  t = new_tpl()
  box(t, -0.1, 0.1, -0.014, 0.004, -0.1, 0.1, 2, true, 0.9)
  for r = 0, 2 do
    local z0 = -0.088 + r * 0.062
    local zm = z0 + 0.022
    local a0, a1 = { -0.092, 0.002, z0 }, { 0.092, 0.002, z0 }
    local b0, b1 = { -0.092, 0.026, zm }, { 0.092, 0.026, zm }
    local c0, c1 = { -0.092, 0.002, z0 + 0.044 }, { 0.092, 0.002, z0 + 0.044 }
    tri(t, a0, a1, b1, nil, nil, nil, 1, { 0.78, 0.78, 1 }, nil, 0, 1, -1)
    tri(t, a0, b1, b0, nil, nil, nil, 1, { 0.78, 1, 1 }, nil, 0, 1, -1)
    tri(t, c1, c0, b0, nil, nil, nil, 1, { 0.85, 0.85, 1 }, nil, 0, 1, 1)
    tri(t, c1, b0, b1, nil, nil, nil, 1, { 0.85, 1, 1 }, nil, 0, 1, 1)
  end
  N[K.crop] = t
  F[K.crop] = t
  -- house: walls (slot 2) + gable roof (slot 1) + door (slot 3)
  t = new_tpl(); box(t, -0.045, 0.045, -0.02, 0.05, -0.035, 0.035, 2, false, 0.8)
  gable(t, -0.045, 0.045, 0.05, 0.095, -0.035, 0.035, 1)
  tri(t, { -0.008, 0.0, 0.0352 }, { 0.008, 0.0, 0.0352 }, { 0.008, 0.028, 0.0352 }, nil, nil, nil, 3, nil, nil, 0, 0, 1)
  tri(t, { -0.008, 0.0, 0.0352 }, { 0.008, 0.028, 0.0352 }, { -0.008, 0.028, 0.0352 }, nil, nil, nil, 3, nil, nil, 0, 0, 1)
  N[K.house] = t
  F[K.house] = t
  -- hut: round-ish walls + cone roof
  t = new_tpl(); cone(t, -0.02, 0.07, 0.04, 6, 2, 0.8, 0); cone(t, 0.04, 0.11, 0.055, 6, 1, 0.75, 0)
  N[K.hut] = t
  F[K.hut] = t
  -- ruin: broken wall stubs
  t = new_tpl(); box(t, -0.05, 0.04, -0.02, 0.045, -0.035, -0.02, 1, true, 0.75)
  box(t, -0.05, -0.035, -0.02, 0.028, -0.035, 0.035, 1, true, 0.75)
  box(t, 0.02, 0.035, -0.02, 0.018, 0.0, 0.03, 1, true, 0.75)
  N[K.ruin] = t
  F[K.ruin] = t
  -- packed shade/sway per template vertex: shade 0.5..1 -> 0..31, sway 0..1 -> 0..7
  for lod = 1, 2 do
    for _, tp in pairs(TPL[lod]) do
      if not tp.ss then
        local ss = {}
        for k = 0, tp.nv - 1 do
          local o = k * 9
          local sh = floor((min(1, max(0.5, tp.d[o + 8])) - 0.5) * 62 + 0.5)
          local sw = floor(min(1, max(0, tp.d[o + 9])) * 7 + 0.5)
          ss[k + 1] = sh * 8 + sw
        end
        tp.ss = ss
      end
    end
  end
end
build_templates()

--- Builds a standalone template (for pooled movers/clouds). kind: "deer" | "wolf" | "cloud"
function M.mover_template(kind)
  local t = new_tpl()
  if kind == "cloud" then
    -- a cluster of soft puffs, ~1.0 across (scaled per cloud)
    blob(t, 0.0, 0.34, 0.26, 9, 1, 0.78, 0, 2)
    local function puff(ox, oy, oz, r)
      local s = new_tpl(); blob(s, 0, r, r * 0.82, 9, 1, 0.78, 0, 2)
      for i = 0, s.nv - 1 do
        local o = i * 9
        push(t, s.d[o + 1] + ox, s.d[o + 2] + oy, s.d[o + 3] + oz, s.d[o + 4], s.d[o + 5], s.d[o + 6],
          1, s.d[o + 8], 0)
      end
    end
    puff(0.3, -0.04, 0.05, 0.24); puff(-0.28, -0.05, -0.04, 0.22); puff(0.05, 0.12, -0.02, 0.22)
    puff(-0.05, -0.06, 0.2, 0.2)
    return t
  end
  if kind == "person" then
    -- body (slot 1, job colour) + head (slot 2, skin); ~0.05 tall at scale 1
    cone(t, -0.004, 0.034, 0.013, 6, 1, 0.75, 0)
    local hd = new_tpl(); blob(hd, 0.0, 0.0095, 0.0095, 6, 2, 0.85, 0, 1)
    for i = 0, hd.nv - 1 do
      local o = i * 9
      push(t, hd.d[o + 1], hd.d[o + 2] + 0.04, hd.d[o + 3], hd.d[o + 4], hd.d[o + 5], hd.d[o + 6], 2, hd.d[o + 8], 0)
    end
    return t
  elseif kind == "tanker" then
    -- hull (slot 1) along local x, deck house (slot 2) at the stern; ~0.36 long
    box(t, -0.18, 0.18, -0.02, 0.03, -0.045, 0.045, 1, true, 0.75)
    tri(t, { 0.18, -0.02, -0.045 }, { 0.24, 0.03, 0.0 }, { 0.18, 0.03, -0.045 }, nil, nil, nil, 1, nil, nil, 1, 0, -1)
    tri(t, { 0.18, -0.02, 0.045 }, { 0.18, 0.03, 0.045 }, { 0.24, 0.03, 0.0 }, nil, nil, nil, 1, nil, nil, 1, 0, 1)
    tri(t, { 0.18, 0.03, -0.045 }, { 0.24, 0.03, 0.0 }, { 0.18, 0.03, 0.045 }, nil, nil, nil, 1, nil, nil, 0, 1, 0)
    box(t, -0.16, -0.09, 0.03, 0.09, -0.035, 0.035, 2, true, 0.85)
    box(t, -0.14, -0.12, 0.09, 0.13, -0.008, 0.008, 1, true, 0.85)
    return t
  elseif kind == "boat" then
    box(t, -0.05, 0.05, -0.01, 0.015, -0.02, 0.02, 1, true, 0.8)
    tri(t, { 0.0, 0.015, 0.0 }, { 0.0, 0.1, 0.0 }, { 0.045, 0.02, 0.0 }, nil, nil, nil, 2, nil, nil, 0, 0, 1)
    tri(t, { 0.0, 0.015, 0.0 }, { 0.045, 0.02, 0.0 }, { 0.0, 0.1, 0.0 }, nil, nil, nil, 2, nil, nil, 0, 0, -1)
    return t
  elseif kind == "ring" then
    -- flat annulus in the local xz plane, radius 1 (scaled per selection)
    local n = 40
    for k = 0, n - 1 do
      local a0, a1 = k / n * 2 * pi, (k + 1) / n * 2 * pi
      local p0o, p1o = { cos(a0), 0, sin(a0) }, { cos(a1), 0, sin(a1) }
      local p0i, p1i = { cos(a0) * 0.8, 0, sin(a0) * 0.8 }, { cos(a1) * 0.8, 0, sin(a1) * 0.8 }
      tri(t, p0i, p0o, p1o, nil, nil, nil, 1, nil, { 1, 1, 1 }, 0, 1, 0)
      tri(t, p0i, p1o, p1i, nil, nil, nil, 1, nil, { 1, 1, 1 }, 0, 1, 0)
    end
    return t
  elseif kind == "flame" then
    cone(t, -0.05, 1.0, 0.32, 5, 1, 0.75, 1)
    cone(t, -0.05, 0.62, 0.2, 5, 2, 0.9, 1, 0.5)
    return t
  elseif kind == "smoke" then
    blob(t, 0.0, 0.5, 0.42, 6, 1, 0.8, 0, 1)
    return t
  end
  local body = 1
  -- quadruped: body blob, head blob, 4 legs (cones), local x = forward
  local s = new_tpl(); blob(s, 0.035, 0.03, 0.018, 5, body, 0.7, 0, 1)
  for i = 0, s.nv - 1 do
    local o = i * 9
    -- stretch body along x
    push(t, s.d[o + 1] * 1.3, s.d[o + 2], s.d[o + 3] * 0.7, s.d[o + 4], s.d[o + 5], s.d[o + 6], 1, s.d[o + 8], 0)
  end
  local hd = new_tpl(); blob(hd, 0.0, 0.014, 0.013, 5, 1, 0.8, 0, 1)
  for i = 0, hd.nv - 1 do
    local o = i * 9
    push(t, hd.d[o + 1] + 0.042, hd.d[o + 2] + 0.058, hd.d[o + 3], hd.d[o + 4], hd.d[o + 5], hd.d[o + 6], 1, hd.d[o + 8], 0)
  end
  for _, lp in ipairs({ { 0.022, 0.012 }, { 0.022, -0.012 }, { -0.022, 0.012 }, { -0.022, -0.012 } }) do
    local lg = new_tpl(); cone(lg, 0.03, -0.004, 0.006, 3, 2, 0.8, 0)
    for i = 0, lg.nv - 1 do
      local o = i * 9
      push(t, lg.d[o + 1] + lp[1], lg.d[o + 2], lg.d[o + 3] + lp[2], lg.d[o + 4], lg.d[o + 5], lg.d[o + 6], 2, lg.d[o + 8], 0)
    end
  end
  return t
end

---------------------------------------------------------------------------------------------
-- Colours

local function hex(h)
  return { tonumber(h:sub(2, 3), 16) / 255, tonumber(h:sub(4, 5), 16) / 255, tonumber(h:sub(6, 7), 16) / 255 }
end
local function hexes(list)
  local out = {}
  for i, h in ipairs(list) do out[i] = hex(h) end
  return out
end

local PAL = {
  [K.tree] = hexes({ "#5DBB4C", "#6CC24A", "#7BC256", "#4CAF50", "#8BC34A", "#58B24F", "#9CCC65", "#48A84A" }),
  [K.jungle] = hexes({ "#2E8B4A", "#2A7F43", "#379548", "#1F7A3A" }),
  [K.pine] = hexes({ "#2F8050", "#2E7548", "#3E8F56", "#3A8A52" }),
  [K.snowpine] = hexes({ "#3A7A55", "#2F6E4A" }),
  [K.palm] = hexes({ "#4DB34A", "#5CC150", "#45A844" }),
  [K.bush] = hexes({ "#6DBE4E", "#7FC857", "#5AAE47", "#8DCB5C" }),
  [K.tuft] = hexes({ "#9BD35F", "#A6D96A" }),
  [K.rock] = hexes({ "#9C9489", "#B3AA9E", "#8E877D", "#A59C90" }),
  [K.stump] = hexes({ "#3E3A38", "#4A4441" }),
  [K.crop] = hexes({ "#E8C552", "#F0CF5A", "#D9B845" }),
  [K.house] = hexes({ "#D9534F", "#E07B39", "#C0504D", "#5B8DB8", "#B5651D" }),
  [K.hut] = hexes({ "#E3C35A", "#D6B24E" }),
  [K.ruin] = hexes({ "#8A7F74", "#9A8F83" }),
}
M.PAL = PAL
local SLOT2 = {
  [K.tree] = hex("#8B5A3C"), [K.jungle] = hex("#7A4E33"), [K.pine] = hex("#7A5236"),
  [K.snowpine] = hex("#7A5236"), [K.palm] = hex("#A57A4F"), [K.crop] = hex("#9C7A4E"),
  [K.house] = hex("#F5EBDC"), [K.hut] = hex("#C9A27A"),
}
local SLOT3 = { [K.snowpine] = hex("#F4FBFF"), [K.house] = hex("#6B4A36") }
M.CROP_YOUNG = hex("#9CCB4E")

---------------------------------------------------------------------------------------------
-- Instance list (flat, reused). Colours are packed per instance: 7 bits per channel.

local function pack_rgb(r, g, b)
  r = floor(min(1, max(0, r)) * 127 + 0.5)
  g = floor(min(1, max(0, g)) * 127 + 0.5)
  b = floor(min(1, max(0, b)) * 127 + 0.5)
  return (r * 128 + g) * 128 + b
end
M.pack_rgb = pack_rgb

local L = {
  n = 0, verts = 0, lod = 2,
  kind = {}, x = {}, y = {}, z = {}, ux = {}, uy = {}, uz = {}, yaw = {}, sc = {}, sh = {},
  c1 = {}, c2 = {}, c3 = {},
}
M.list = L
local WHITE = { 1, 1, 1 }

local function add(kind, x, y, z, ux, uy, uz, yaw, sc, sh, col, jit)
  local i = L.n + 1
  L.n = i
  L.kind[i], L.x[i], L.y[i], L.z[i] = kind, x, y, z
  L.ux[i], L.uy[i], L.uz[i], L.yaw[i], L.sc[i], L.sh[i] = ux, uy, uz, yaw, sc, sh
  L.c1[i] = pack_rgb(col[1] * jit, col[2] * jit, col[3] * jit)
  local s2 = SLOT2[kind] or col
  L.c2[i] = pack_rgb(s2[1], s2[2], s2[3])
  local s3 = SLOT3[kind] or WHITE
  L.c3[i] = pack_rgb(s3[1], s3[2], s3[3])
  L.verts = L.verts + TPL[L.lod][kind].nv
end
M.add = add

-- Park-Miller step (exact in doubles)
local function pm(h) return (h * 48271) % 2147483647 end

--- Plans the props of one chunk into M.list. ctx = { W, PL, villages (cell -> {houses, ruin}),
-- lod (1 near / 2 far), seed }. Returns instance count and vertex count.
function M.plan_chunk(ctx, ch, i0, i1, append)
  if not append then L.n, L.verts = 0, 0 end
  L.lod = ctx.lod or 2
  local W, PL = ctx.W, ctx.PL
  local F = W.F
  local forest, grass, biome, land_use = F.forest, F.grass, F.biome, F.land_use
  local fire, scar, crop, ice = F.fire, F.burn_scar, F.crop, F.ice
  local ocean, cdist = W.geo.ocean, W.geo.coast_dist
  local T = W.temp_static
  local gtemp = W.G.g_temp or 0
  local ruins = W.S and W.S.ruins
  local villages = ctx.villages
  local faces, px, py, pz = PL.faces, PL.px, PL.py, PL.pz
  local tland, tc1, tc2, tc3, tw1, tw2, tw3 = PL.tland, PL.tc1, PL.tc2, PL.tc3, PL.tw1, PL.tw2, PL.tw3
  local SX, SY, SZ = W.grid.X, W.grid.Y, W.grid.Z
  local spacing = W.grid.spacing
  local slots = ctx.lod == 2 and 3 or M.SLOTS
  local seed = ctx.seed or 1
  local S = (ctx.lod == 2) and M.SCALE_FAR or M.SCALE
  local list = ch.faces
  for li = i0 or 1, math.min(i1 or #list, #list) do
    local f = list[li]
    if tland[f] == 1 then
      local a1, a2, a3 = tc1[f], tc2[f], tc3[f]
      local w1, w2, w3 = tw1[f], tw2[f], tw3[f]
      local c = a1
      if ocean[c] == 0 then
        local fo = forest[a1] * w1 + forest[a2] * w2 + forest[a3] * w3
        local ic = ice[a1] * w1 + ice[a2] * w2 + ice[a3] * w3
        -- no trees standing on snowfields: fade out between 30% and 55% ice cover
        local snowfade = ic <= 0.3 and 1 or (ic >= 0.55 and 0 or (0.55 - ic) / 0.25)
        fo = fo * snowfade
        local gr = grass[a1] * w1 + grass[a2] * w2 + grass[a3] * w3
        local bi = biome[c]
        local lu = land_use[c]
        local pv = ctx.preview and ctx.preview[c]
        if pv then lu = pv end
        local tmean = T and T.base and (T.base[c] + T.lapse[c] + gtemp) or 10
        local burnt = fire[c] > 0 or (scar[c] > 0 and scar[c] < 240)
        local vil = villages and villages[c]
        if vil and not vil.ruin and lu >= 0 then lu = -1 end
        local is_ruin = (ruins and ruins[c]) or (vil and vil.ruin) or bi == 6
        local i = f * 3 - 2
        local va, vb, vc = faces[i], faces[i + 1], faces[i + 2]
        local ax, ay, az = px[va], py[va], pz[va]
        local bx, by, bz = px[vb], py[vb], pz[vb]
        local cx, cy, cz = px[vc], py[vc], pz[vc]
        -- distance (radians) of the face centre from its main cell centre (village clusters)
        local mx, my, mz = ax + bx + cx, ay + by + cy, az + bz + cz
        local ml = sqrt(mx * mx + my * my + mz * mz)
        local dcell = 1 - (mx * SX[c] + my * SY[c] + mz * SZ[c]) / ml
        dcell = sqrt(max(0, 2 * dcell)) / spacing   -- ~0 at the cell centre, ~0.5 at its edge
        local tree_p = min(1, fo * 1.35) ^ 0.85
        local h = (f * 7919 + seed * 104729) % 2147483647
        if h == 0 then h = 1 end
        for j = 1, slots do
          h = pm(h + j * 131); local u1 = h / 2147483647
          h = pm(h); local u2 = h / 2147483647
          h = pm(h); local u3 = h / 2147483647
          h = pm(h); local u4 = h / 2147483647
          h = pm(h); local u5 = h / 2147483647
          local kind = nil
          local scale, hs = 1, 1
          if lu < 0 and not is_ruin then
            -- village: houses clustered around the cell centre (hook: villages[c].houses)
            local want = vil and vil.houses or 8
            local p = max(0, 1 - dcell * 1.6) * min(1, want / 6)
            if u3 < p then kind = (u4 < 0.7) and K.house or K.hut; scale = 0.9 + u5 * 0.25
            elseif u3 < p + 0.15 then kind = K.bush end
          elseif is_ruin then
            local p = max(0, 1 - dcell * 1.8) * 0.6
            if u3 < p then kind = K.ruin; scale = 0.9 + u5 * 0.3
            elseif u3 < p + tree_p * 0.5 then kind = K.bush end
          elseif lu > 0 then
            -- farm: crop patches (one per face) + hedge trees
            if j == 1 and u3 < 0.92 then kind = K.crop
            elseif j > 2 and u3 < 0.12 then kind = K.tree; scale = 0.8 + u5 * 0.4 end
          elseif burnt then
            if u3 < tree_p * 0.7 + 0.25 then kind = K.stump; scale = 0.8 + u5 * 0.5 end
          elseif bi == 3 then
            if u3 < 0.03 then kind = K.rock end
          else
            if u3 < tree_p then
              if tmean < -3 or ic > 0.3 then kind = K.snowpine
              elseif tmean < 6 then kind = (u4 < 0.85) and K.pine or K.tree
              elseif tmean > 21 and bi == 11 then kind = (u4 < 0.3 and cdist[c] <= 1) and K.palm or K.jungle
              elseif tmean > 21 then kind = (u4 < 0.35 and cdist[c] <= 1) and K.palm or K.tree
              else kind = (u4 < 0.12) and K.pine or K.tree end
              scale = 0.75 + u5 * 0.5
              hs = 0.9 + u2 * 0.25
            elseif bi == 9 then
              if u3 < tree_p + 0.08 then kind = K.rock; scale = 0.6 + u5 * 0.8
              elseif u3 < tree_p + 0.12 and tmean > 20 then kind = K.palm; scale = 0.7 end
            elseif bi == 14 or bi == 10 then
              if u3 < tree_p + 0.1 then kind = K.rock; scale = 0.6 + u5 * 0.7
              elseif u3 < tree_p + 0.22 then kind = K.tuft end
            elseif u3 < tree_p + gr * 0.22 then
              kind = (u4 < 0.55) and K.bush or K.tuft
              scale = 0.7 + u5 * 0.6
            elseif u3 < tree_p + gr * 0.22 + 0.015 then
              kind = K.rock; scale = 0.5 + u5 * 0.5
            end
          end
          if kind and (ctx.lod ~= 2 or kind ~= K.tuft) then
            -- uniform point in the triangle (fields sit on the face centre)
            local r1 = sqrt(u1)
            local ka, kb, kc = 1 - r1, r1 * (1 - u2), r1 * u2
            local yaw = u2 * 2 * pi + u5 * 7
            if kind == K.crop then
              ka, kb, kc = 1 / 3, 1 / 3, 1 / 3
              yaw = (c * 0.61803) % 1 * pi + ((f % 2 == 0) and 0 or pi * 0.5)
              scale = 0.92 + u5 * 0.1
            end
            local x = ax * ka + bx * kb + cx * kc
            local y = ay * ka + by * kb + cy * kc
            local z = az * ka + bz * kb + cz * kc
            local l = sqrt(x * x + y * y + z * z)
            local pal = PAL[kind]
            local col = pal[floor(u4 * 9973) % #pal + 1]
            if kind == K.crop then
              local cr = pv and 0.8 or crop[c]
              local cy2 = M.CROP_YOUNG
              col = { cy2[1] + (col[1] - cy2[1]) * cr, cy2[2] + (col[2] - cy2[2]) * cr, cy2[3] + (col[3] - cy2[3]) * cr }
            end
            local jit = 0.9 + u5 * 0.2
            add(kind, x, y, z, x / l, y / l, z / l, yaw, scale * S, hs, col, jit)
          end
        end
      end
    end
  end
  return L.n, L.verts
end

---------------------------------------------------------------------------------------------
-- Baking into streams

--- Writes instances [i0..i1] of M.list into streams starting at vertex `vbase` (0-based).
-- pos(3) / nrm(3) / col(2: packed rgb, packed shade*8+sway) are buffer streams (or Lua arrays
-- in tests). Returns the next vertex index.
function M.write(pos, nrm, col, vbase, i0, i1)
  local kind, X, Y, Z = L.kind, L.x, L.y, L.z
  local UX, UY, UZ, YAW, SC, SH = L.ux, L.uy, L.uz, L.yaw, L.sc, L.sh
  local C1, C2, C3 = L.c1, L.c2, L.c3
  local tpls = TPL[L.lod]
  local v = vbase
  for i = i0, i1 do
    local t = tpls[kind[i]]
    local d, nv, ss = t.d, t.nv, t.ss
    local ux, uy, uz = UX[i], UY[i], UZ[i]
    -- tangent frame: T = normalize(Yaxis x U) (east), B = T x U
    local tx, tz = uz, -ux
    local tl = sqrt(tx * tx + tz * tz)
    if tl < 1e-4 then tx, tz, tl = 1, 0, 1 end
    tx, tz = tx / tl, tz / tl
    local bx, by, bz = -tz * uy, tz * ux - tx * uz, tx * uy
    local cyw, syw = cos(YAW[i]), sin(YAW[i])
    local ex, ey, ez = tx * cyw + bx * syw, by * syw, tz * cyw + bz * syw
    local fx, fy, fz = bx * cyw - tx * syw, by * cyw, bz * cyw - tz * syw
    local s, hs = SC[i], SC[i] * SH[i]
    local ox, oy, oz = X[i], Y[i], Z[i]
    local c1, c2, c3 = C1[i], C2[i], C3[i]
    local p, q = v * 3, v * 2
    for k = 0, nv - 1 do
      local o = k * 9
      local lx, ly, lz = d[o + 1] * s, d[o + 2] * hs, d[o + 3] * s
      pos[p + 1] = ox + ex * lx + ux * ly + fx * lz
      pos[p + 2] = oy + ey * lx + uy * ly + fy * lz
      pos[p + 3] = oz + ez * lx + uz * ly + fz * lz
      local nlx, nly, nlz = d[o + 4], d[o + 5], d[o + 6]
      nrm[p + 1] = ex * nlx + ux * nly + fx * nlz
      nrm[p + 2] = ey * nlx + uy * nly + fy * nlz
      nrm[p + 3] = ez * nlx + uz * nly + fz * nlz
      local slot = d[o + 7]
      col[q + 1] = slot == 1 and c1 or (slot == 2 and c2 or c3)
      col[q + 2] = ss[k + 1]
      p, q = p + 3, q + 2
    end
    v = v + nv
  end
  return v
end

--- Writes a template once in its local frame (pooled movers/clouds). Returns vertex count.
function M.write_template(t, pos, nrm, col, c1, c2, scale)
  local d = t.d
  scale = scale or 1
  local p1 = pack_rgb(c1[1], c1[2], c1[3])
  local p2 = c2 and pack_rgb(c2[1], c2[2], c2[3]) or p1
  for k = 0, t.nv - 1 do
    local o = k * 9
    local p, q = k * 3, k * 2
    pos[p + 1], pos[p + 2], pos[p + 3] = d[o + 1] * scale, d[o + 2] * scale, d[o + 3] * scale
    nrm[p + 1], nrm[p + 2], nrm[p + 3] = d[o + 4], d[o + 5], d[o + 6]
    local sh = floor((min(1, max(0.5, d[o + 8])) - 0.5) * 62 + 0.5)
    col[q + 1] = d[o + 7] == 1 and p1 or p2
    col[q + 2] = sh * 8 + floor(min(1, max(0, d[o + 9])) * 7 + 0.5)
  end
  return t.nv
end

return M
