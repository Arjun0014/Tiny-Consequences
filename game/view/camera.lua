-- Planet camera (Stage 1.9): trackball orbit with no pole lock, zoom, fly-to tweens, follow,
-- idle auto-rotate, and screen <-> world helpers for picking.
--
-- The orbit frame is an orthonormal basis stored as plain numbers: S = focus direction (from
-- the planet centre toward the camera at zoom 0), U = screen up, R = U x S = screen right.
-- Dragging rotates this frame; nothing ever refers to the planet axis, so the poles are
-- ordinary places. zoom 0 = whole planet with a margin, ZOOM_DEFAULT = the reference framing
-- (planet large, sitting a little low), zoom 1 = low tilted view where villagers are readable.
local state = require("game.view.state")

local M = {}

local sin, cos, exp, log, sqrt, max, min, abs, pi, acos = math.sin, math.cos, math.exp, math.log,
  math.sqrt, math.max, math.min, math.abs, math.pi, math.acos

M.D_FAR = 58        -- eye distance from the look target at zoom 0 (planet ~65% of height)
M.D_NEAR = 4.6      -- eye distance from the surface target at zoom 1
M.ZOOM_EXP = 1.35   -- distance curve: log-lerp on zoom^ZOOM_EXP
M.ZOOM_DEFAULT = 0.16   -- title/default framing: planet ~85% of the frame height
M.TILT = math.rad(62)
M.LOW = 0.26        -- planet sits this many radii below the frame centre when zoomed out
M.SPEED = 3.0       -- tween rate (1/s)
M.WHEEL_STEP = 0.07
M.IDLE_ROTATE_AFTER = 8.0   -- s without input before the default view slowly turns
M.IDLE_ROTATE_SPEED = 0.035 -- rad/s around the planet axis

-- current frame + zoom
local Sx, Sy, Sz = 0, 0, 1
local Ux, Uy, Uz = 0, 1, 0
-- target frame + zoom (tweens)
local TSx, TSy, TSz = 0, 0, 1
local TUx, TUy, TUz = 0, 1, 0
M.zoom, M.tzoom = M.ZOOM_DEFAULT, M.ZOOM_DEFAULT
M.tweening = false
M.idle = 0
M.follow = nil        -- fn() -> x, y, z (unit direction) or nil to stop following
M.focus_cell = nil
M.height_fn = nil     -- fn(x, y, z) -> surface radius at unit direction

local function smoothstep(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-12 then return 0, 0, 1 end
  return x / l, y / l, z / l
end

-- Re-orthonormalise (S, U) -> returns S, U with U perpendicular to S.
local function ortho(sx, sy, sz, ux, uy, uz)
  sx, sy, sz = norm(sx, sy, sz)
  local d = ux * sx + uy * sy + uz * sz
  ux, uy, uz = ux - sx * d, uy - sy * d, uz - sz * d
  local l = sqrt(ux * ux + uy * uy + uz * uz)
  if l < 1e-6 then
    -- pick any perpendicular (prefer world +Y)
    ux, uy, uz = -sx * sy, 1 - sy * sy, -sz * sy
    l = sqrt(ux * ux + uy * uy + uz * uz)
    if l < 1e-6 then ux, uy, uz, l = 1, 0, 0, 1 end
  end
  return sx, sy, sz, ux / l, uy / l, uz / l
end

-- Rotate vector v around unit axis k by angle a (Rodrigues).
local function rot(vx, vy, vz, kx, ky, kz, a)
  local c, s = cos(a), sin(a)
  local d = (kx * vx + ky * vy + kz * vz) * (1 - c)
  local cx, cy, cz = ky * vz - kz * vy, kz * vx - kx * vz, kx * vy - ky * vx
  return vx * c + cx * s + kx * d, vy * c + cy * s + ky * d, vz * c + cz * s + kz * d
end

local function latlon_dir(lat, lon)
  local cl = cos(lat)
  return cl * cos(lon), sin(lat), cl * sin(lon)
end

local function north_of(sx, sy, sz)
  -- north tangent at S (projection of +Y); poles fall back inside ortho()
  return ortho(sx, sy, sz, 0, 1, 0)
end

--- Current focus as lat/lon (radians) — for the bridge and presets.
function M.latlon()
  return math.asin(max(-1, min(1, Sy))), math.atan2(Sz, Sx)
end

function M.target_latlon()
  return math.asin(max(-1, min(1, TSy))), math.atan2(TSz, TSx)
end

--- Fly to (lat, lon) with north up. instant = no tween.
function M.set(lat, lon, zoom, instant)
  local sx, sy, sz = latlon_dir(lat, lon)
  local ux, uy, uz
  sx, sy, sz, ux, uy, uz = north_of(sx, sy, sz)
  M.set_frame(sx, sy, sz, ux, uy, uz, zoom, instant)
end

--- Fly to a unit direction keeping the current screen orientation (double-click, follow).
function M.fly_to_dir(x, y, z, zoom, instant)
  local sx, sy, sz, ux, uy, uz = ortho(x, y, z, Ux, Uy, Uz)
  M.set_frame(sx, sy, sz, ux, uy, uz, zoom, instant)
end

function M.set_frame(sx, sy, sz, ux, uy, uz, zoom, instant)
  TSx, TSy, TSz, TUx, TUy, TUz = ortho(sx, sy, sz, ux, uy, uz)
  if zoom then M.tzoom = max(0, min(1, zoom)) end
  M.tweening = true
  M.idle = 0
  if instant then
    Sx, Sy, Sz, Ux, Uy, Uz = TSx, TSy, TSz, TUx, TUy, TUz
    M.zoom = M.tzoom
    M.tweening = false
    M.update(0)
  end
end

--- Surface angle (radians) swept by one screen pixel at the current zoom (drag speed).
function M.rad_per_pixel()
  local ex, ey, ez = state.eye.x, state.eye.y, state.eye.z
  local R = state.R
  local dx, dy, dz = ex - Sx * R, ey - Sy * R, ez - Sz * R
  local d = sqrt(dx * dx + dy * dy + dz * dz)
  local h = max(1, state.window_h or 720)
  return 2 * d * math.tan(state.fov * 0.5) / R / h
end

--- Trackball drag by (dx, dy) window pixels (dy up). Cancels tweens and follow.
function M.drag(dx, dy)
  local a = M.rad_per_pixel()
  local Rx, Ry, Rz = Uy * Sz - Uz * Sy, Uz * Sx - Ux * Sz, Ux * Sy - Uy * Sx
  -- horizontal: orbit around U
  local phi = dx * a
  local c, s = cos(phi), sin(phi)
  Sx, Sy, Sz = Sx * c - Rx * s, Sy * c - Ry * s, Sz * c - Rz * s
  -- vertical: orbit around R
  local th = dy * a
  c, s = cos(th), sin(th)
  local nsx, nsy, nsz = Sx * c - Ux * s, Sy * c - Uy * s, Sz * c - Uz * s
  local nux, nuy, nuz = Ux * c + Sx * s, Uy * c + Sy * s, Uz * c + Sz * s
  Sx, Sy, Sz, Ux, Uy, Uz = ortho(nsx, nsy, nsz, nux, nuy, nuz)
  TSx, TSy, TSz, TUx, TUy, TUz = Sx, Sy, Sz, Ux, Uy, Uz
  M.tweening = false
  M.follow = nil
  M.idle = 0
end

function M.zoom_by(step)
  M.tzoom = max(0, min(1, M.tzoom + step))
  M.idle = 0
end

function M.touch()
  M.idle = 0
end

local eye, target, upv = vmath.vector3(), vmath.vector3(), vmath.vector3()

function M.update(dt)
  M.idle = M.idle + dt
  if dt > 0 then
    local k = 1 - exp(-dt * M.SPEED)
    if M.follow then
      local fx, fy, fz = M.follow()
      if fx then
        TSx, TSy, TSz, TUx, TUy, TUz = ortho(fx, fy, fz, Ux, Uy, Uz)
        M.tweening = true
        M.idle = 0
      else
        M.follow = nil
      end
    end
    if M.tweening then
      -- move S toward TS along the great circle, then U toward TU
      local d = max(-1, min(1, Sx * TSx + Sy * TSy + Sz * TSz))
      local ang = acos(d)
      if ang > 1e-5 then
        local ax, ay, az = norm(Sy * TSz - Sz * TSy, Sz * TSx - Sx * TSz, Sx * TSy - Sy * TSx)
        if ang > pi - 1e-3 then ax, ay, az = Ux, Uy, Uz end
        local step = ang * k
        Sx, Sy, Sz = rot(Sx, Sy, Sz, ax, ay, az, step)
        Ux, Uy, Uz = rot(Ux, Uy, Uz, ax, ay, az, step)
      end
      Ux, Uy, Uz = Ux + (TUx - Ux) * k, Uy + (TUy - Uy) * k, Uz + (TUz - Uz) * k
      Sx, Sy, Sz, Ux, Uy, Uz = ortho(Sx, Sy, Sz, Ux, Uy, Uz)
      local du = Ux * TUx + Uy * TUy + Uz * TUz
      if ang < 1e-4 and du > 0.99999 and abs(M.zoom - M.tzoom) < 1e-4 then M.tweening = false end
    elseif not state.frozen and M.idle > M.IDLE_ROTATE_AFTER and M.zoom < 0.3 and not M.follow then
      -- idle: slow turn around the planet axis
      local a = M.IDLE_ROTATE_SPEED * dt
      Sx, Sy, Sz = rot(Sx, Sy, Sz, 0, 1, 0, a)
      Ux, Uy, Uz = rot(Ux, Uy, Uz, 0, 1, 0, a)
      TSx, TSy, TSz, TUx, TUy, TUz = Sx, Sy, Sz, Ux, Uy, Uz
    end
    M.zoom = M.zoom + (M.tzoom - M.zoom) * k
  end
  local R = state.R
  local z = M.zoom
  local s = smoothstep(0.08, 0.7, z)
  local th = M.TILT * smoothstep(0.3, 1.0, z)
  local ct, st = cos(th), sin(th)
  local dx, dy, dz = Sx * ct - Ux * st, Sy * ct - Uy * st, Sz * ct - Uz * st
  local ux, uy, uz = Ux * ct + Sx * st, Uy * ct + Sy * st, Uz * ct + Sz * st
  local rs = R
  if M.height_fn then rs = M.height_fn(Sx, Sy, Sz) end
  local low = M.LOW * R * (1 - s)
  local tx = Sx * rs * s + ux * low
  local ty = Sy * rs * s + uy * low
  local tz = Sz * rs * s + uz * low
  local dist = exp(log(M.D_FAR) + (log(M.D_NEAR) - log(M.D_FAR)) * (z ^ M.ZOOM_EXP))
  target.x, target.y, target.z = tx, ty, tz
  eye.x, eye.y, eye.z = tx + dx * dist, ty + dy * dist, tz + dz * dist
  upv.x, upv.y, upv.z = ux, uy, uz
  state.view = vmath.matrix4_look_at(eye, target, upv)
  state.eye.x, state.eye.y, state.eye.z = eye.x, eye.y, eye.z
  state.target.x, state.target.y, state.target.z = tx, ty, tz
  state.up.x, state.up.y, state.up.z = ux, uy, uz
  state.focus_x, state.focus_y, state.focus_z = Sx, Sy, Sz
  local er = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  local alt = max(0.05, er - R - 0.4)
  state.near = max(0.02, min(2, alt * 0.35))
  state.far = er + R * 1.6
  -- sun from the upper left of the view (follows the camera)
  local fx, fy, fz = -dx, -dy, -dz
  local rx, ry, rz = fy * uz - fz * uy, fz * ux - fx * uz, fx * uy - fy * ux
  local lx = -rx * 0.55 + ux * 0.62 - fx * 0.58
  local ly = -ry * 0.55 + uy * 0.62 - fy * 0.58
  local lz = -rz * 0.55 + uz * 0.62 - fz * 0.58
  lx, ly, lz = norm(lx, ly, lz)
  state.sun.x, state.sun.y, state.sun.z = lx, ly, lz
  state.u_cam.x, state.u_cam.y, state.u_cam.z = eye.x, eye.y, eye.z
  state.u_sun.x, state.u_sun.y, state.u_sun.z = lx, ly, lz
  M.fit_shadow(tx, ty, tz, dist, lx, ly, lz, ux, uy, uz, z)
  -- depth of field (tilt-shift): stronger the closer the camera gets to the surface
  local near_k = smoothstep(0.3, 0.95, z)
  state.u_focus.x = dist
  state.u_focus.y = 0.5 + 1.5 * near_k
  state.u_focus.z = 0.1 - 0.04 * near_k
  state.u_focus.w = 0.12 + 0.88 * smoothstep(0.35, 0.85, z)
end

-- Sun shadow map frame (renderer v2, D-048): an orthographic sun view covering the whole planet
-- when zoomed out, and the region around the look target when zoomed in (sharper shadows).
local lv_eye, lv_tgt, lv_up = vmath.vector3(), vmath.vector3(), vmath.vector3()
function M.fit_shadow(tx, ty, tz, dist, lx, ly, lz, ux, uy, uz, z)
  local R = state.R
  local t = smoothstep(0.28, 0.7, z)
  local r = R * 1.12 + (max(1.6, dist * 0.95) - R * 1.12) * t
  local cx, cy, cz = tx * t, ty * t, tz * t
  -- snap the centre to whole shadow texels in the sun's frame (steadier edges while zooming)
  local size = state.SHADOW_SIZE
  local texel = 2 * r / size
  local D = 40
  lv_eye.x, lv_eye.y, lv_eye.z = cx + lx * D, cy + ly * D, cz + lz * D
  lv_tgt.x, lv_tgt.y, lv_tgt.z = cx, cy, cz
  lv_up.x, lv_up.y, lv_up.z = ux, uy, uz
  local V = vmath.matrix4_look_at(lv_eye, lv_tgt, lv_up)
  -- the world origin's position in the sun frame, kept on the texel lattice
  local ox, oy = V.m03, V.m13
  local fx = ox - texel * math.floor(ox / texel)
  local fy = oy - texel * math.floor(oy / texel)
  local P = vmath.matrix4_orthographic(-r - fx, r - fx, -r - fy, r - fy, D - 23, D + 23)
  local PV = P * V
  state.light_view, state.light_proj = V, P
  local s0, s1, s2 = state.u_sh0, state.u_sh1, state.u_sh2
  s0.x, s0.y, s0.z, s0.w = 0.5 * PV.m00, 0.5 * PV.m01, 0.5 * PV.m02, 0.5 * PV.m03 + 0.5
  s1.x, s1.y, s1.z, s1.w = 0.5 * PV.m10, 0.5 * PV.m11, 0.5 * PV.m12, 0.5 * PV.m13 + 0.5
  s2.x, s2.y, s2.z, s2.w = 0.5 * PV.m20, 0.5 * PV.m21, 0.5 * PV.m22, 0.5 * PV.m23 + 0.5
  state.u_shn.x = texel * 1.5
  state.u_shadowp.x = 1 / size
  state.u_shadowp.y = texel * 1.2 / 46
end

-- Screen <-> world ---------------------------------------------------------------------------

local function viewproj()
  local w, h = state.window_w or 1280, state.window_h or 720
  local proj = vmath.matrix4_perspective(state.fov, w / h, state.near, state.far)
  return proj * state.view, w, h
end

--- World point -> window pixels (x right, y up) and depth flag (true if in front of camera).
function M.project(x, y, z)
  local vp, w, h = viewproj()
  local c = vp * vmath.vector4(x, y, z, 1)
  if c.w <= 1e-6 then return nil end
  return (c.x / c.w * 0.5 + 0.5) * w, (c.y / c.w * 0.5 + 0.5) * h, c.z / c.w
end

--- Window pixel -> ray (origin, unit direction) in world space.
function M.ray(sx, sy)
  local vp, w, h = viewproj()
  local inv = vmath.inv(vp)
  local nx, ny = sx / w * 2 - 1, sy / h * 2 - 1
  local a = inv * vmath.vector4(nx, ny, -1, 1)
  local b = inv * vmath.vector4(nx, ny, 1, 1)
  local ax, ay, az = a.x / a.w, a.y / a.w, a.z / a.w
  local bx, by, bz = b.x / b.w, b.y / b.w, b.z / b.w
  local dx, dy, dz = norm(bx - ax, by - ay, bz - az)
  return ax, ay, az, dx, dy, dz
end

--- Ray vs sphere of radius r: nearest hit point or nil.
function M.hit_sphere(ox, oy, oz, dx, dy, dz, r)
  local b = ox * dx + oy * dy + oz * dz
  local c = ox * ox + oy * oy + oz * oz - r * r
  local disc = b * b - c
  if disc < 0 then return nil end
  local t = -b - sqrt(disc)
  if t < 0 then t = -b + sqrt(disc) end
  if t < 0 then return nil end
  return ox + dx * t, oy + dy * t, oz + dz * t
end

--- Is the unit direction on the visible hemisphere (not behind the planet)?
function M.facing(x, y, z)
  local e = state.eye
  local el = sqrt(e.x * e.x + e.y * e.y + e.z * e.z)
  return (x * e.x + y * e.y + z * e.z) / el > state.R / el
end

-- Presets ------------------------------------------------------------------------------

local function centre_of(W, cells)
  local g = W.grid
  local x, y, z = 0, 0, 0
  for i = 1, #cells do
    local c = cells[i]
    x, y, z = x + g.X[c], y + g.Y[c], z + g.Z[c]
  end
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-6 then return 0, 0 end
  x, y, z = x / l, y / l, z / l
  return math.asin(max(-1, min(1, y))), math.atan2(z, x)
end

--- A cell worth zooming into: the biggest village if any, else a coastal lowland cell with
-- vegetation near the largest continent's centre.
function M.focus_candidate(W)
  local F, geo, g = W.F, W.geo, W.grid
  local best, bs = nil, -1e9
  local vils = W.ent and W.ent.villages
  if type(vils) == "table" then
    for _, v in pairs(vils) do
      if type(v) == "table" and v.cell and v.alive ~= false then
        local pop = type(v.members) == "table" and #v.members or 0
        if pop > bs then best, bs = v.cell, pop end
      end
    end
    if best then return best end
  end
  for c = 1, W.n do
    if F.land_use[c] < 0 then
      local sc = -F.land_use[c]
      if sc > bs then best, bs = c, sc end
    end
  end
  if best then return best end
  local comps = geo.components or {}
  local clat, clon = 0, 0
  if comps[1] then clat, clon = centre_of(W, comps[1].cells) end
  local cx, cy, cz = cos(clat) * cos(clon), sin(clat), cos(clat) * sin(clon)
  for c = 1, W.n do
    if geo.ocean[c] == 0 and geo.coast[c] == 1 then
      local e = W.static.elev[c] - W.G.sea_level
      local d = g.X[c] * cx + g.Y[c] * cy + g.Z[c] * cz
      local veg = F.forest[c] + F.grass[c]
      local sc = d * 2 + min(veg, 1) - max(0, e - 0.5) - F.ice[c] * 2 - abs(g.lat[c]) * 0.5
      if sc > bs then best, bs = c, sc end
    end
  end
  return best or 1
end

--- Resolves a preset name into lat, lon, zoom.
function M.preset(name, W)
  local comps = W.geo.components or {}
  local g = W.grid
  if name == "default" or name == "title" then
    -- open on life: the biggest village, pulled a little toward the equator so the globe
    -- (not a pole) fills the frame; landmass 1 when there are no villages
    local best, bp = nil, -1
    for _, v in ipairs(W.ent and W.ent.villages or {}) do
      if v.alive ~= false and (v.pop or 0) > bp then best, bp = v, v.pop or 0 end
    end
    if best then return g.lat[best.cell] * 0.75, g.lon[best.cell], M.ZOOM_DEFAULT end
  end
  if name == "continent_a" or name == "continent_b" or name == "default" or name == "title" then
    local comp = comps[name == "continent_b" and 2 or 1] or comps[1]
    local lat, lon = 0, 0
    if comp then lat, lon = centre_of(W, comp.cells) end
    local zoom = (name == "default" or name == "title") and M.ZOOM_DEFAULT or M.ZOOM_DEFAULT
    return max(-1.1, min(1.1, lat + 0.22)), lon, zoom
  elseif name == "whole" then
    local comp = comps[1]
    local lat, lon = 0, 0
    if comp then lat, lon = centre_of(W, comp.cells) end
    return max(-1.1, min(1.1, lat + 0.22)), lon, 0
  elseif name == "pole" then
    local n_ice, s_ice = 0, 0
    for c = 1, W.n do
      if W.F.ice[c] > 0.5 then
        if g.lat[c] > 0 then n_ice = n_ice + 1 else s_ice = s_ice + 1 end
      end
    end
    local lat0, lon0 = 0, 0
    if comps[1] then lat0, lon0 = centre_of(W, comps[1].cells) end
    return (n_ice >= s_ice) and 1.12 or -1.12, lon0, M.ZOOM_DEFAULT
  elseif name == "village" or name == "close" then
    local c = M.focus_cell or M.focus_candidate(W)
    M.focus_cell = c
    return g.lat[c], g.lon[c], name == "village" and 0.6 or 1.0
  end
  return nil
end

return M
