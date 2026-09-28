-- Vegetation models (renderer v4, D-056): species with several variants each, built from the
-- model kit's primitives (game/view/modelkit.lua). Pure Lua 5.1, no Defold APIs.
--
-- Trees are made the way the eye reads them in a miniature: a trunk that forks into visible
-- branches, and a crown of many overlapping lumps of different sizes whose tints differ (sunlit
-- lumps lean yellow-green, deep ones blue-green: props.vp), so every crown has light, shade and
-- an irregular silhouette. Conifers are stacks of drooping, slightly ragged tiers. Deadwood
-- (snags, fallen logs), saplings, shrubs, ferns and ground-cover patches make the forest floor.
-- Near templates (lod 1) carry the detail; far templates (lod 2) keep each species' silhouette
-- with a fraction of the vertices, so forests read from orbit as varied masses.
--
-- Slots: 1 foliage (per-instance colour), 2 bark/stems, 3 accent (berries, blossom, snow, caps).
local kit = require("game.view.modelkit")

local M = {}

local sqrt, floor, min, max, abs, cos, sin, pi, atan2, acos = math.sqrt, math.floor, math.min,
  math.max, math.abs, math.cos, math.sin, math.pi, math.atan2, math.acos

local h = 1
local function seed(s) h = (s * 7919 + 17) % 2147483647; if h == 0 then h = 1 end end
local function u()
  h = (h * 48271) % 2147483647
  return h / 2147483647
end
local function uu(a, b) return a + (b - a) * u() end

-- a tapered cylinder from p0 to p1 (slot 2 bark unless given)
local function branch(t, x0, y0, z0, x1, y1, z1, r0, r1, sides, slot, sway)
  local dx, dy, dz = x1 - x0, y1 - y0, z1 - z0
  local len = sqrt(dx * dx + dy * dy + dz * dz)
  if len < 1e-6 then return end
  dx, dy, dz = dx / len, dy / len, dz / len
  local tz = acos(max(-1, min(1, dy)))
  local yaw = atan2(dz, -dx)
  kit.place(t, kit.cyl(nil, r0, r1, 0, len, sides, slot or 2, 0.5, 0.82, sway or 0), x0, y0, z0, 1, 1, 1, yaw, 0, tz)
end

-- a foliage lump with a tint
local function lump(t, x, y, z, rx, ry, rz, sides, rings, tint, lo, jit, slot, sway)
  kit.set_tint(tint)
  kit.place(t, kit.lump(nil, rx, ry, rz, sides, rings, slot or 1, lo or 0.55, 1.0, sway or 1, jit or 0.14), x, y, z, 1)
  kit.set_tint(0)
end

---------------------------------------------------------------------------------------------
-- Broadleaf trees

-- o: seed, h (trunk height to the crown base), cw (crown half-width), ch (crown half-height),
-- n (lumps), lean, br (branches), sides, rings, flat (0..1: flatter crowns), r0 (trunk radius)
local function broadleaf(o, far)
  seed(o.seed)
  local t = kit.new()
  local lx, lz = (u() - 0.5) * (o.lean or 0.02), (u() - 0.5) * (o.lean or 0.02)
  local th = o.h
  local r0 = o.r0 or 0.012
  branch(t, 0, -0.02, 0, lx, th, lz, r0, r0 * 0.62, far and 4 or 6)
  -- crown centre
  local cx, cy, cz = lx, th + o.ch * 0.7, lz
  -- branches forking up and out into the crown
  if not far then
    for b = 1, o.br or 3 do
      local a = (b / (o.br or 3) + u() * 0.3) * 2 * pi
      local y0 = th * uu(0.62, 0.95)
      local ex, ez = cos(a) * o.cw * uu(0.45, 0.75), sin(a) * o.cw * uu(0.45, 0.75)
      branch(t, lx * y0 / th, y0, lz * y0 / th, cx + ex, cy + o.ch * uu(-0.35, 0.1), cz + ez, r0 * 0.42, r0 * 0.2, 4)
    end
  end
  -- the crown: a core and lumps on an ellipsoid shell, biggest at the top
  local sides, rings = o.sides or (far and 5 or 6), o.rings or (far and 2 or 3)
  local n = far and max(3, floor(o.n * 0.45 + 0.5)) or o.n
  local flat = o.flat or 0
  lump(t, cx, cy, cz, o.cw * 0.82, o.ch * 0.8 * (1 - flat * 0.3), o.cw * 0.8, sides, rings, -0.3, 0.5, 0.12)
  for k = 1, n do
    local a = (k / n) * 2 * pi + u() * 0.9
    local el = uu(-0.35, 0.95)                   -- elevation on the shell (more at the top)
    local ce, se = cos(el * 1.2), sin(el * 1.2)
    local rr = uu(0.55, 0.9)
    local px = cx + cos(a) * ce * o.cw * rr
    local pz = cz + sin(a) * ce * o.cw * rr
    local py = cy + se * o.ch * (0.65 - flat * 0.3)
    local s = uu(0.28, 0.62) * (1.05 - 0.25 * (el < 0 and 1 or 0))
    local rx = o.cw * s
    local tint = max(-1, min(1, el * 0.75 - 0.1 + (u() - 0.5) * 0.35))
    lump(t, px, py, pz, rx, rx * uu(0.62, 0.85) * (1 - flat * 0.35), rx * uu(0.85, 1.1), sides, rings, tint, 0.55, 0.18)
  end
  return t
end

-- Birch: slim pale trunk with dark marks, a narrow airy crown of small clumps up the stem.
local function birch(o, far)
  seed(o.seed)
  local t = kit.new()
  local th = o.h
  branch(t, 0, -0.02, 0, (u() - 0.5) * 0.015, th, (u() - 0.5) * 0.015, 0.0075, 0.004, far and 4 or 6)
  if not far then
    for k = 1, 4 do
      local y = th * uu(0.25, 0.8)
      kit.place(t, kit.cyl(nil, 0.0079, 0.0079, 0, 0.004, 6, 4, 0.7, 0.8, 0), 0, y, 0, 1)   -- dark bands
    end
  end
  local n = far and 3 or o.n
  for k = 1, n do
    local f = (k - 0.5) / n
    local y = th * (0.45 + 0.6 * f)
    local a = k * 2.4 + u()
    local d = o.cw * (0.55 - 0.3 * f) * uu(0.6, 1.0)
    local r = o.cw * uu(0.36, 0.5) * (1.1 - 0.4 * f)
    lump(t, cos(a) * d, y, sin(a) * d, r, r * uu(0.9, 1.3), r, far and 5 or 5, far and 2 or 3, (f - 0.5) * 1.2 + (u() - 0.5) * 0.4, 0.58, 0.18)
  end
  lump(t, 0, th * 1.08, 0, o.cw * 0.42, o.cw * 0.62, o.cw * 0.42, 5, far and 2 or 3, 0.6, 0.6, 0.14)
  return t
end

-- Columnar poplar: a tall narrow crown (vertical accents in a forest edge or along water).
local function poplar(o, far)
  seed(o.seed)
  local t = kit.new()
  branch(t, 0, -0.02, 0, 0, o.h * 0.35, 0, 0.008, 0.006, far and 4 or 5)
  local n = far and 3 or 6
  for k = 1, n do
    local f = (k - 0.5) / n
    local y = o.h * (0.3 + 0.75 * f)
    local r = o.cw * (0.9 - 0.55 * abs(f - 0.35)) * uu(0.85, 1.05)
    lump(t, (u() - 0.5) * 0.01, y, (u() - 0.5) * 0.01, r, o.h * 0.16, r, far and 5 or 6, far and 2 or 3, (f - 0.4) * 1.1, 0.55, 0.12)
  end
  return t
end

---------------------------------------------------------------------------------------------
-- Conifers

-- o: seed, h (total height), w (base half-width), tiers, droop, snow (slot 3 caps)
local function conifer(o, far)
  seed(o.seed)
  local t = kit.new()
  local H, w = o.h, o.w
  branch(t, 0, -0.02, 0, 0, H * 0.35, 0, w * 0.17, w * 0.1, far and 3 or 5)
  local tiers = far and max(2, floor(o.tiers * 0.5 + 0.5)) or o.tiers
  local base = H * (o.bare or 0.14)
  for i = 1, tiers do
    local f = (i - 1) / tiers
    local y0 = base + (H - base) * f * 0.92
    local y1 = y0 + (H - base) * (far and 0.55 or 0.34) * (1 - f * 0.35)
    local r = w * (1 - f * 0.86) * uu(0.9, 1.08)
    local sides = far and 6 or 8
    kit.set_tint(max(-1, min(1, f * 1.4 - 0.7 + (u() - 0.5) * 0.3)))
    kit.cone(t, y0, min(H, y1), r, sides, 1, 0.5 + f * 0.2, 1.0, 0.25 + f * 0.6, u(), r * (o.droop or 0.3))
    if not far then
      -- ragged tier: a second, rotated, slightly smaller skirt
      kit.cone(t, y0 + (y1 - y0) * 0.1, min(H, y1) - (y1 - y0) * 0.05, r * 0.86, 7, 1, 0.55 + f * 0.2, 1.0, 0.3 + f * 0.6, u() + 0.5, r * 0.22)
    end
    if o.snow and i >= 2 then
      kit.set_tint(0)
      kit.cone(t, y0 + (y1 - y0) * 0.3, min(H, y1) + 0.002, r * 0.7, far and 5 or 6, 3, 0.85, 1.0, 0.3 + f * 0.6, u(), r * 0.14)
    end
  end
  kit.set_tint(0)
  return t
end

-- Scots pine: a tall bare orange trunk, a few branches, a flat irregular umbrella of lumps.
local function pine(o, far)
  seed(o.seed)
  local t = kit.new()
  local th = o.h
  local lx, lz = (u() - 0.5) * 0.03, (u() - 0.5) * 0.03
  branch(t, 0, -0.02, 0, lx, th, lz, 0.009, 0.005, far and 3 or 5)
  local n = far and 2 or o.n
  for k = 1, n do
    local a = k / n * 2 * pi + u() * 0.8
    local d = o.cw * uu(0.2, 0.7)
    local y = th + uu(-0.02, 0.025)
    local px, pz = lx + cos(a) * d, lz + sin(a) * d
    if not far then branch(t, lx, th - 0.02, lz, px, y - 0.004, pz, 0.004, 0.0025, 4) end
    local r = o.cw * uu(0.35, 0.55)
    lump(t, px, y, pz, r, r * 0.42, r * 0.9, far and 5 or 6, far and 2 or 3, (u() - 0.3) * 0.9, 0.55, 0.2)
  end
  lump(t, lx, th + 0.015, lz, o.cw * 0.5, o.cw * 0.26, o.cw * 0.5, 6, 2, 0.4, 0.6, 0.18)
  return t
end

---------------------------------------------------------------------------------------------
-- Tropical and dry-land trees

local function jungle(o, far)
  seed(o.seed)
  local t = kit.new()
  local th = o.h
  branch(t, 0, -0.02, 0, 0, th, 0, 0.014, 0.008, far and 4 or 6)
  if not far then
    -- buttress roots
    for k = 1, 4 do
      local a = k / 4 * 2 * pi + u() * 0.5
      branch(t, cos(a) * 0.028, -0.01, sin(a) * 0.028, cos(a) * 0.004, 0.05, sin(a) * 0.004, 0.006, 0.003, 4)
    end
    for k = 1, 3 do
      local a = k / 3 * 2 * pi + u()
      branch(t, 0, th * 0.85, 0, cos(a) * o.cw * 0.6, th + 0.02, sin(a) * o.cw * 0.6, 0.006, 0.003, 4)
    end
  end
  local n = far and 3 or o.n
  for k = 1, n do
    local a = k / n * 2 * pi + u() * 0.7
    local d = o.cw * uu(0.3, 0.72)
    local r = o.cw * uu(0.38, 0.55)
    lump(t, cos(a) * d, th + uu(0.0, 0.03), sin(a) * d, r, r * uu(0.45, 0.6), r, far and 5 or 6, far and 2 or 3,
      (u() - 0.4) * 1.0, 0.52, 0.16)
  end
  lump(t, 0, th + 0.03, 0, o.cw * 0.6, o.cw * 0.3, o.cw * 0.6, 6, 2, 0.5, 0.55, 0.14)
  if o.under and not far then
    -- a lower storey hugging the trunk
    lump(t, 0.02, th * 0.45, -0.015, o.cw * 0.45, o.cw * 0.35, o.cw * 0.45, 6, 3, -0.6, 0.5, 0.16)
  end
  return t
end

local function palm(o, far)
  seed(o.seed)
  local t = kit.new()
  local px, py = 0, -0.02
  local segs = far and 3 or 5
  local bend = o.bend or 0.1
  for s = 0, segs - 1 do
    local a0 = bend + s * bend * 0.8
    local len = o.h / segs
    local seg = kit.cyl(nil, 0.0085 - s * 0.0008, 0.0078 - s * 0.0008, 0, len, far and 4 or 5, 2, 0.6, 0.85, 0.15 * s)
    kit.place(t, seg, px, py, 0, 1, 1, 1, 0, 0, -a0)
    px = px + sin(a0) * len
    py = py + cos(a0) * len
  end
  local nf = far and 5 or 9
  for k = 0, nf - 1 do
    local a = k / nf * 2 * pi + u() * 0.3
    local ca, sa = cos(a), sin(a)
    local len, w = o.frond * uu(0.85, 1.1), 0.018
    local b = { px, py, 0 }
    local m1 = { px + ca * len * 0.5, py + 0.016, sa * len * 0.5 }
    local tip = { px + ca * len, py - 0.03 * uu(0.8, 1.3), sa * len }
    local l = { m1[1] - sa * w, m1[2] - 0.004, m1[3] + ca * w }
    local r = { m1[1] + sa * w, m1[2] - 0.004, m1[3] - ca * w }
    kit.set_tint((u() - 0.5) * 0.8)
    kit.tri(t, b, l, m1, nil, nil, nil, 1, 0.75, 0.95, 1.0, 0.3, 0.8, 0.8, 0, 1, 0)
    kit.tri(t, b, m1, r, nil, nil, nil, 1, 0.75, 1.0, 0.95, 0.3, 0.8, 0.8, 0, 1, 0)
    kit.tri(t, l, tip, m1, nil, nil, nil, 1, 0.95, 0.85, 1.0, 0.8, 1, 0.8, 0, 1, 0)
    kit.tri(t, m1, tip, r, nil, nil, nil, 1, 1.0, 0.85, 0.95, 0.8, 1, 0.8, 0, 1, 0)
  end
  kit.set_tint(0)
  kit.place(t, kit.lump(nil, 0.009, 0.009, 0.009, 5, 2, 3, 0.6, 0.9, 0, 0), px, py - 0.006, 0, 1)
  return t
end

local function acacia(o, far)
  seed(o.seed)
  local t = kit.new()
  local lean = uu(0.08, 0.22)
  kit.place(t, kit.cyl(nil, 0.009, 0.006, 0, o.h * 0.75, far and 3 or 5, 2, 0.55, 0.85, 0), 0, -0.02, 0, 1, 1, 1, 0, 0, lean)
  local topx = sin(lean) * o.h * 0.75
  local topy = -0.02 + cos(lean) * o.h * 0.75
  if not far then
    for k = 1, 3 do
      local a = k / 3 * 2 * pi + u()
      branch(t, topx * 0.8, topy * 0.8, 0, topx + cos(a) * o.cw * 0.6, o.h, sin(a) * o.cw * 0.6, 0.004, 0.0025, 4)
    end
  end
  local n = far and 2 or o.n
  for k = 1, n do
    local a = k / n * 2 * pi + u()
    local d = o.cw * uu(0.25, 0.65)
    local r = o.cw * uu(0.4, 0.62)
    lump(t, topx + cos(a) * d, o.h + uu(-0.005, 0.01), sin(a) * d, r, r * 0.26, r, far and 6 or 7, 2, (u() - 0.3) * 0.8, 0.6, 0.1)
  end
  return t
end

---------------------------------------------------------------------------------------------
-- Deadwood, saplings, shrubs, ground cover

local function snag(o, far)
  seed(o.seed)
  local t = kit.new()
  local H = o.h
  local slot = o.burnt and 1 or 2
  branch(t, 0, -0.02, 0, (u() - 0.5) * 0.02, H, (u() - 0.5) * 0.02, 0.01, 0.0035, far and 3 or 5, slot)
  if not far then
    for k = 1, 4 do
      local a = u() * 2 * pi
      local y = H * uu(0.4, 0.85)
      local len = uu(0.03, 0.055)
      branch(t, 0, y, 0, cos(a) * len, y + len * uu(0.2, 0.7), sin(a) * len, 0.003, 0.0012, 3, slot)
    end
  end
  return t
end

-- a fallen trunk with its root plate and moss (slot 3 = moss)
local function deadfall(o, far)
  seed(o.seed)
  local t = kit.new()
  local L = o.len
  kit.place(t, kit.cyl(nil, 0.009, 0.006, -L * 0.5, L * 0.5, far and 4 or 6, 2, 0.55, 0.85, 0, true), 0, 0.008, 0, 1, 1, 1, 0, pi / 2, 0)
  if not far then
    kit.place(t, kit.lump(nil, 0.004, 0.018, 0.016, 6, 2, 2, 0.5, 0.8, 0, 0.3), -L * 0.5, 0.012, 0, 1)
    for k = 1, 3 do
      kit.place(t, kit.lump(nil, L * 0.12, 0.005, 0.009, 5, 2, 3, 0.7, 1.0, 0, 0.2), (k - 2) * L * 0.25, 0.016, 0, 1)
    end
    branch(t, L * 0.15, 0.012, 0, L * 0.2, 0.03, 0.02, 0.0025, 0.001, 3)
  end
  return t
end

local function sapling(o)
  seed(o.seed)
  local t = kit.new()
  if o.conifer then
    branch(t, 0, -0.01, 0, 0, 0.02, 0, 0.003, 0.002, 4)
    kit.set_tint(-0.1)
    kit.cone(t, 0.008, 0.06, 0.016, 6, 1, 0.55, 1.0, 0.6, 0, 0.004)
    kit.set_tint(0.3)
    kit.cone(t, 0.03, 0.075, 0.011, 6, 1, 0.6, 1.0, 0.8, 0.5, 0.003)
    kit.set_tint(0)
  else
    branch(t, 0, -0.01, 0, 0.004, 0.045, 0, 0.0028, 0.0018, 4)
    lump(t, 0.004, 0.05, 0, 0.016, 0.014, 0.015, 5, 3, 0.3, 0.6, 0.2)
    lump(t, -0.006, 0.038, 0.004, 0.011, 0.01, 0.011, 5, 2, -0.2, 0.6, 0.2)
  end
  return t
end

local function shrub(o, far)
  seed(o.seed)
  local t = kit.new()
  local n = far and 2 or o.n
  for k = 1, n do
    local a = k / n * 2 * pi + u()
    local d = o.w * uu(0.1, 0.55)
    local r = o.w * uu(0.4, 0.62)
    lump(t, cos(a) * d, r * 0.55 + (o.lift or 0), sin(a) * d, r, r * (o.tall or 0.8), r, far and 5 or 6, far and 2 or 3,
      (u() - 0.4) * 1.0, 0.5, 0.2)
  end
  if o.berries and not far then
    for k = 1, 9 do
      local a = u() * 2 * pi
      local r = o.w * uu(0.35, 0.6)
      kit.place(t, kit.lump(nil, 0.0032, 0.0032, 0.0032, 4, 2, 3, 0.9, 1.0, 0.5, 0), cos(a) * r, o.w * uu(0.3, 0.75), sin(a) * r, 1)
    end
  end
  return t
end

-- a fern: fronds arching out of a crown (slot 1)
local function fern(o)
  seed(o.seed)
  local t = kit.new()
  local nf = o.n or 7
  for k = 0, nf - 1 do
    local a = k / nf * 2 * pi + u() * 0.4
    local ca, sa = cos(a), sin(a)
    local len = o.len * uu(0.8, 1.15)
    local w = len * 0.2
    local b = { 0, 0.002, 0 }
    local m1 = { ca * len * 0.5, len * 0.55, sa * len * 0.5 }
    local tip = { ca * len, len * 0.25, sa * len }
    local l = { m1[1] - sa * w, m1[2] - 0.003, m1[3] + ca * w }
    local r = { m1[1] + sa * w, m1[2] - 0.003, m1[3] - ca * w }
    kit.set_tint((u() - 0.5) * 0.9)
    kit.tri(t, b, l, m1, nil, nil, nil, 1, 0.6, 0.85, 1.0, 0.2, 0.7, 0.7, 0, 1, 0)
    kit.tri(t, b, m1, r, nil, nil, nil, 1, 0.6, 1.0, 0.85, 0.2, 0.7, 0.7, 0, 1, 0)
    kit.tri(t, l, tip, m1, nil, nil, nil, 1, 0.85, 0.8, 1.0, 0.7, 1, 0.7, 0, 1, 0)
    kit.tri(t, m1, tip, r, nil, nil, nil, 1, 1.0, 0.8, 0.85, 0.7, 1, 0.7, 0, 1, 0)
  end
  kit.set_tint(0)
  return t
end

-- a patch of grass blades (slot 1), with flowers (slot 3) if asked
local function grass_patch(o)
  seed(o.seed)
  local t = kit.new()
  for c = 1, o.clumps do
    local cx, cz = (u() - 0.5) * o.w, (u() - 0.5) * o.w
    local nb = floor(uu(4, 7))
    kit.set_tint((u() - 0.5) * 0.9)
    for k = 0, nb - 1 do
      local a = k / nb * 2 * pi + u()
      local ca, sa = cos(a), sin(a)
      local lean = uu(0.3, 0.7)
      local hgt = o.h * uu(0.6, 1.2)
      kit.tri(t, { cx - sa * 0.0035, 0, cz + ca * 0.0035 }, { cx + sa * 0.0035, 0, cz - ca * 0.0035 },
        { cx + ca * hgt * 0.5 * lean, hgt, cz + sa * hgt * 0.5 * lean }, nil, nil, nil, 1, 0.55, 0.55, 1.0, 0, 0, 1, ca, 0.4, sa)
    end
  end
  kit.set_tint(0)
  for f = 1, o.flowers or 0 do
    local x, z = (u() - 0.5) * o.w, (u() - 0.5) * o.w
    local hgt = o.h * uu(0.8, 1.3)
    kit.tri(t, { x - 0.0012, 0, z }, { x + 0.0012, 0, z }, { x, hgt, z }, nil, nil, nil, 2, 0.6, 0.6, 0.9, 0, 0, 0.8, 0, 0, 1)
    kit.place(t, kit.lump(nil, 0.0055, 0.0035, 0.0055, 5, 2, 3, 0.85, 1.0, 1, 0), x, hgt, z, 1)
  end
  return t
end

-- mushrooms under the trees (caps slot 3, stems slot 2)
local function mushrooms(o)
  seed(o.seed)
  local t = kit.new()
  for k = 1, o.n do
    local x, z = (u() - 0.5) * 0.03, (u() - 0.5) * 0.03
    local hgt = uu(0.006, 0.012)
    kit.place(t, kit.cyl(nil, 0.0018, 0.0015, 0, hgt, 4, 2, 0.8, 1.0, 0), x, 0, z, 1)
    kit.place(t, kit.lump(nil, 0.0055, 0.0035, 0.0055, 6, 2, 3, 0.7, 1.0, 0, 0.1), x, hgt, z, 1)
  end
  return t
end

local function rock(o, far)
  seed(o.seed)
  local t = kit.new()
  kit.place(t, kit.lump(nil, o.w, o.h, o.w * 0.85, far and 5 or 7, 3, 1, 0.55, 1.0, 0, 0.26), 0, o.h * 0.25, 0, 1)
  if o.moss and not far then
    kit.set_tint(0.2)
    kit.place(t, kit.lump(nil, o.w * 0.75, o.h * 0.35, o.w * 0.65, 6, 2, 3, 0.7, 1.0, 0, 0.2), 0.002, o.h * 0.95, 0, 1)
    kit.set_tint(0)
  end
  if o.second and not far then
    kit.place(t, kit.lump(nil, o.w * 0.55, o.h * 0.6, o.w * 0.5, 6, 2, 1, 0.55, 1.0, 0, 0.3), o.w * 1.1, o.h * 0.1, o.w * 0.3, 1)
  end
  return t
end

---------------------------------------------------------------------------------------------
-- Catalogue: name -> builder(lod) ; M.NAMES lists the new kinds (propgen appends them)

local B = {}
B.oak1 = function(far) return broadleaf({ seed = 101, h = 0.085, cw = 0.07, ch = 0.055, n = 8, br = 3, lean = 0.02 }, far) end
B.oak2 = function(far) return broadleaf({ seed = 102, h = 0.07, cw = 0.085, ch = 0.05, n = 9, br = 4, lean = 0.03, flat = 0.4, r0 = 0.014 }, far) end
B.oak3 = function(far) return broadleaf({ seed = 103, h = 0.095, cw = 0.062, ch = 0.06, n = 7, br = 3, lean = 0.05 }, far) end
B.beech1 = function(far) return broadleaf({ seed = 111, h = 0.12, cw = 0.058, ch = 0.075, n = 8, br = 2, lean = 0.01, r0 = 0.011 }, far) end
B.beech2 = function(far) return broadleaf({ seed = 112, h = 0.105, cw = 0.066, ch = 0.068, n = 9, br = 3, lean = 0.015, r0 = 0.012 }, far) end
B.birch1 = function(far) return birch({ seed = 121, h = 0.13, cw = 0.042, n = 6 }, far) end
B.birch2 = function(far) return birch({ seed = 122, h = 0.105, cw = 0.038, n = 5 }, far) end
B.poplar = function(far) return poplar({ seed = 131, h = 0.2, cw = 0.03 }, far) end
B.spruce1 = function(far) return conifer({ seed = 141, h = 0.27, w = 0.058, tiers = 7, droop = 0.3 }, far) end
B.spruce2 = function(far) return conifer({ seed = 142, h = 0.2, w = 0.052, tiers = 6, droop = 0.35 }, far) end
B.spruce3 = function(far) return conifer({ seed = 143, h = 0.14, w = 0.045, tiers = 5, droop = 0.25, bare = 0.08 }, far) end
B.fir1 = function(far) return conifer({ seed = 144, h = 0.24, w = 0.064, tiers = 8, droop = 0.4, bare = 0.1 }, far) end
B.snowpine1 = function(far) return conifer({ seed = 151, h = 0.23, w = 0.058, tiers = 6, droop = 0.35, snow = true }, far) end
B.snowpine2 = function(far) return conifer({ seed = 152, h = 0.17, w = 0.05, tiers = 5, droop = 0.3, snow = true }, far) end
B.pine1 = function(far) return pine({ seed = 161, h = 0.19, cw = 0.06, n = 5 }, far) end
B.pine2 = function(far) return pine({ seed = 162, h = 0.15, cw = 0.07, n = 4 }, far) end
B.jungle1 = function(far) return jungle({ seed = 171, h = 0.2, cw = 0.09, n = 7, under = true }, far) end
B.jungle2 = function(far) return jungle({ seed = 172, h = 0.14, cw = 0.075, n = 6 }, far) end
B.jungle3 = function(far) return broadleaf({ seed = 173, h = 0.09, cw = 0.07, ch = 0.06, n = 8, br = 3, lean = 0.02 }, far) end
B.palm1 = function(far) return palm({ seed = 181, h = 0.2, frond = 0.085, bend = 0.1 }, far) end
B.palm2 = function(far) return palm({ seed = 182, h = 0.15, frond = 0.075, bend = 0.16 }, far) end
B.acacia1 = function(far) return acacia({ seed = 191, h = 0.13, cw = 0.085, n = 5 }, far) end
B.acacia2 = function(far) return acacia({ seed = 192, h = 0.1, cw = 0.07, n = 4 }, far) end
B.snag1 = function(far) return snag({ seed = 201, h = 0.15 }, far) end
B.burnt1 = function(far) return snag({ seed = 202, h = 0.14, burnt = true }, far) end
B.deadfall = function(far) return deadfall({ seed = 211, len = 0.13 }, far) end
B.sapling_b = function() return sapling({ seed = 221 }) end
B.sapling_c = function() return sapling({ seed = 222, conifer = true }) end
B.bush1 = function(far) return shrub({ seed = 231, w = 0.042, n = 4 }, far) end
B.bush2 = function(far) return shrub({ seed = 232, w = 0.055, n = 5, tall = 0.6 }, far) end
B.berry = function(far) return shrub({ seed = 233, w = 0.036, n = 4, berries = true }, far) end
B.juniper = function(far) return shrub({ seed = 234, w = 0.03, n = 3, tall = 1.5 }, far) end
B.heather = function(far) return shrub({ seed = 235, w = 0.03, n = 5, tall = 0.45 }, far) end
B.dryshrub = function(far) return shrub({ seed = 236, w = 0.03, n = 3, tall = 0.7, lift = 0.004 }, far) end
B.fern = function() return fern({ seed = 241, len = 0.058, n = 8 }) end
B.fern2 = function() return fern({ seed = 242, len = 0.045, n = 7 }) end
B.grass1 = function() return grass_patch({ seed = 251, clumps = 5, w = 0.09, h = 0.04 }) end
B.grass2 = function() return grass_patch({ seed = 252, clumps = 4, w = 0.07, h = 0.055 }) end
B.meadow = function() return grass_patch({ seed = 253, clumps = 4, w = 0.09, h = 0.04, flowers = 7 }) end
B.tallgrass = function() return grass_patch({ seed = 254, clumps = 5, w = 0.08, h = 0.085 }) end
B.shroom = function() return mushrooms({ seed = 261, n = 5 }) end
B.boulder = function(far) return rock({ seed = 271, w = 0.035, h = 0.026, second = true }, far) end
B.mossrock = function(far) return rock({ seed = 272, w = 0.03, h = 0.02, moss = true }, far) end
B.slab = function(far) return rock({ seed = 273, w = 0.045, h = 0.014 }, far) end

M.NAMES = { "oak1", "oak2", "oak3", "beech1", "beech2", "birch1", "birch2", "poplar", "spruce1", "spruce2",
  "spruce3", "fir1", "snowpine1", "snowpine2", "pine1", "pine2", "jungle1", "jungle2", "jungle3", "palm1", "palm2",
  "acacia1", "acacia2", "snag1", "burnt1", "deadfall", "sapling_b", "sapling_c", "bush1", "bush2", "berry",
  "juniper", "heather", "dryshrub", "fern", "fern2", "grass1", "grass2", "meadow", "tallgrass", "shroom",
  "boulder", "mossrock", "slab" }

-- ground cover and small things exist only near the camera
M.NEAR_ONLY = { sapling_b = true, sapling_c = true, fern = true, fern2 = true, grass1 = true, grass2 = true,
  meadow = true, tallgrass = true, shroom = true, heather = true, berry = true }

--- Builds every flora template into kit.T[1] (near) and kit.T[2] (far).
function M.build()
  for _, name in ipairs(M.NAMES) do
    kit.T[1][name] = B[name](false)
    if not M.NEAR_ONLY[name] then kit.T[2][name] = B[name](true) end
  end
end

return M
