-- Planet camera (Stage 1.8: bridge-driven presets; interactive controls arrive in 1.9).
-- State is (lat, lon, zoom in 0..1). zoom 0 = whole planet filling ~70% of the frame height,
-- viewed from slightly above the focus latitude with the planet sitting a little low in the
-- frame (docs/ART_DIRECTION.md §1); zoom 1 = low, tilted view over the surface.
local state = require("game.view.state")

local M = {}

local sin, cos, exp, log, sqrt, max, min, abs, pi = math.sin, math.cos, math.exp, math.log,
  math.sqrt, math.max, math.min, math.abs, math.pi

M.D_FAR = 54        -- eye distance from the look target at zoom 0 (planet ~70% of height)
M.D_NEAR = 4.6      -- eye distance from the surface target at zoom 1
M.ZOOM_EXP = 1.35   -- distance curve: log-lerp on zoom^ZOOM_EXP
M.TILT = math.rad(62)
M.LOW = 0.26        -- planet sits this many radii below the frame centre at zoom 0
M.SPEED = 3.0       -- tween rate (1/s)

M.lat, M.lon, M.zoom = 0.35, 0, 0
M.tlat, M.tlon, M.tzoom = 0.35, 0, 0
M.focus_cell = nil
M.height_fn = nil   -- optional fn(x, y, z) -> surface radius at unit direction

local function smoothstep(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end

local function wrap_pi(a)
  while a > pi do a = a - 2 * pi end
  while a < -pi do a = a + 2 * pi end
  return a
end

function M.set(lat, lon, zoom, instant)
  M.tlat = max(-1.45, min(1.45, lat or M.tlat))
  M.tlon = wrap_pi(lon or M.tlon)
  M.tzoom = max(0, min(1, zoom or M.tzoom))
  if instant then
    M.lat, M.lon, M.zoom = M.tlat, M.tlon, M.tzoom
    M.update(0)
  end
end

local eye, target, upv, sun = vmath.vector3(), vmath.vector3(), vmath.vector3(), vmath.vector3()

function M.update(dt)
  if dt > 0 then
    local k = 1 - exp(-dt * M.SPEED)
    M.lat = M.lat + (M.tlat - M.lat) * k
    M.lon = wrap_pi(M.lon + wrap_pi(M.tlon - M.lon) * k)
    M.zoom = M.zoom + (M.tzoom - M.zoom) * k
  end
  local R = state.R
  local lat, lon, z = M.lat, M.lon, M.zoom
  local cl, sl, co, so = cos(lat), sin(lat), cos(lon), sin(lon)
  -- surface direction S (grid convention: lon = atan2(z, x), +Y axis) and north tangent Nt
  local sx, sy, sz = cl * co, sl, cl * so
  local nx, ny, nz = -sl * co, cl, -sl * so
  local s = smoothstep(0.08, 0.7, z)
  local th = M.TILT * smoothstep(0.3, 1.0, z)
  local ct, st = cos(th), sin(th)
  local dx, dy, dz = sx * ct - nx * st, sy * ct - ny * st, sz * ct - nz * st
  local ux, uy, uz = nx * ct + sx * st, ny * ct + sy * st, nz * ct + sz * st
  local rs = R
  if M.height_fn then rs = M.height_fn(sx, sy, sz) end
  -- look target: from (centre shifted up in screen) to the surface point
  local low = M.LOW * R * (1 - s)
  local tx = sx * rs * s + ux * low
  local ty = sy * rs * s + uy * low
  local tz = sz * rs * s + uz * low
  local dist = exp(log(M.D_FAR) + (log(M.D_NEAR) - log(M.D_FAR)) * (z ^ M.ZOOM_EXP))
  target.x, target.y, target.z = tx, ty, tz
  eye.x, eye.y, eye.z = tx + dx * dist, ty + dy * dist, tz + dz * dist
  upv.x, upv.y, upv.z = ux, uy, uz
  state.view = vmath.matrix4_look_at(eye, target, upv)
  state.eye.x, state.eye.y, state.eye.z = eye.x, eye.y, eye.z
  state.target.x, state.target.y, state.target.z = tx, ty, tz
  state.up.x, state.up.y, state.up.z = ux, uy, uz
  -- clip planes: near scales with the distance to the surface
  local er = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  local alt = max(0.05, er - R - 0.4)
  state.near = max(0.02, min(2, alt * 0.35))
  state.far = er + R * 1.6
  -- sun from the upper left of the view (follows the camera)
  local fx, fy, fz = -dx, -dy, -dz                      -- forward
  local rx, ry, rz = fy * uz - fz * uy, fz * ux - fx * uz, fx * uy - fy * ux  -- right = f x up
  local lx = -rx * 0.55 + ux * 0.62 - fx * 0.58
  local ly = -ry * 0.55 + uy * 0.62 - fy * 0.58
  local lz = -rz * 0.55 + uz * 0.62 - fz * 0.58
  local ll = sqrt(lx * lx + ly * ly + lz * lz)
  sun.x, sun.y, sun.z = lx / ll, ly / ll, lz / ll
  state.sun.x, state.sun.y, state.sun.z = sun.x, sun.y, sun.z
  state.u_cam.x, state.u_cam.y, state.u_cam.z = eye.x, eye.y, eye.z
  state.u_sun.x, state.u_sun.y, state.u_sun.z = sun.x, sun.y, sun.z
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
  if name == "continent_a" or name == "continent_b" or name == "default" then
    local comp = comps[name == "continent_b" and 2 or 1] or comps[1]
    local lat, lon = 0, 0
    if comp then lat, lon = centre_of(W, comp.cells) end
    return max(-1.1, min(1.1, lat + 0.22)), lon, 0
  elseif name == "pole" then
    -- the pole with more land ice in view
    local n_ice, s_ice = 0, 0
    for c = 1, W.n do
      if W.F.ice[c] > 0.5 then
        if g.lat[c] > 0 then n_ice = n_ice + 1 else s_ice = s_ice + 1 end
      end
    end
    local lat0, lon0 = 0, 0
    if comps[1] then lat0, lon0 = centre_of(W, comps[1].cells) end
    return (n_ice >= s_ice) and 1.12 or -1.12, lon0, 0
  elseif name == "village" or name == "close" then
    local c = M.focus_cell or M.focus_candidate(W)
    M.focus_cell = c
    return g.lat[c], g.lon[c], name == "village" and 0.6 or 1.0
  end
  return nil
end

return M
