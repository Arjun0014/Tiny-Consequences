-- The meteor, seen (S26, rebuilt from scratch). A rock that a god throws: it appears high in the sky beyond the place
-- aimed at, a white-hot point that swells as it falls, the air in front of it burning - a corona, a long plasma streak and
-- a dark smoke trail left hanging in the sky - the ground below lit by it in the last second; then the strike: a white
-- flash, a fireball that swells and darkens into a rising cloud of smoke and dust, shock rings running out over the land
-- (the blast's reach), rock and embers thrown out and falling back, a skirt of dust rolling out low - and the camera shaken.
-- What it does to the world is the sim's alone (sim/agents/impact.lua): THE COMMAND IS GIVEN AT THE STRIKE. The cast only
-- starts the fall (game/ui/powers.lua); when the rock reaches the ground this module queues the logged `power` command,
-- which the world applies at that moment, so not a house falls nor a fire starts before the rock is down. The fall runs in
-- real time while the world runs (paused, the rock hangs in the sky; it goes on when time does). Sizes are the impact's own
-- physics (sim/config.lua imp_*), kept readable from orbit by the eye's distance. Reads the world only; writes none of it.
local state = require("game.view.state")
local vmx = require("game.view.vmx")
local propgen = require("game.view.propgen")

local M = {}

M.FALL = 4.2            -- seconds from the cast to the strike (real time, while the world runs)
M.AFTER = 18            -- seconds the strike's own effects last (the sim's fires, crater and dust go on)
M.PUFFS, M.CLOUD, M.FIRES, M.SPARKS, M.DEBRIS, M.EMBERS = 84, 30, 12, 20, 22, 16
M.seq = nil             -- the sequence in flight: { t, dir, T (the point), path, hit, ... }
M.hit_ms = nil          -- (probes) the clock when the last rock struck

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local H_TINT = hash("u_tint")
local sin, cos, sqrt, max, min, floor, abs, pi = math.sin, math.cos, math.sqrt, math.max, math.min, math.floor, math.abs, math.pi
local pos, scl, rot = vmath.vector3(), vmath.vector3(), vmath.quat()
local tint = vmath.vector4(1, 1, 1, 1)

-- Buffers ------------------------------------------------------------------------------------

local function new_buf(nv)
  return buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
end

-- a writer over a buffer: vtx(x, y, z, r, g, b, opacity 0..7)
local function writer(buf)
  local P, N, C = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local k = 0
  local pack = propgen.pack_rgb
  return function(x, y, z, r, g, b, op)
    local o = k * 3
    P[o + 1], P[o + 2], P[o + 3] = x, y, z
    local l = sqrt(x * x + y * y + z * z)
    if l < 1e-6 then l = 1 end
    N[o + 1], N[o + 2], N[o + 3] = x / l, y / l, z / l
    C[k * 2 + 1], C[k * 2 + 2] = pack(max(0, min(1, r)), max(0, min(1, g)), max(0, min(1, b))), 31 * 8 + (op or 7)
    k = k + 1
  end
end

-- colour along stops { { t, r, g, b, op } } at t (0..1)
local function grad(stops, t)
  if t <= stops[1][1] then local s = stops[1]; return s[2], s[3], s[4], s[5] end
  for i = 2, #stops do
    local a, b = stops[i - 1], stops[i]
    if t <= b[1] then
      local f = (t - a[1]) / max(1e-6, b[1] - a[1])
      return a[2] + (b[2] - a[2]) * f, a[3] + (b[3] - a[3]) * f, a[4] + (b[4] - a[4]) * f, floor(a[5] + (b[5] - a[5]) * f + 0.5)
    end
  end
  local s = stops[#stops]
  return s[2], s[3], s[4], s[5]
end

-- a disc in the plane y = 0 (normal +y), radius 1, coloured by the radius (stops from the middle out)
local function disc(stops, rings, seg)
  local buf = new_buf(seg * 3 + (rings - 1) * seg * 6)
  local v = writer(buf)
  local function at(ri, i)
    local a = i / seg * 2 * pi
    local r = ri / rings
    return cos(a) * r, sin(a) * r
  end
  for ri = 1, rings do
    local r0, r1 = (ri - 1) / rings, ri / rings
    local ar, ag, ab, ao = grad(stops, r0)
    local br, bg, bb, bo = grad(stops, r1)
    for i = 0, seg - 1 do
      local x1, z1 = at(ri, i)
      local x2, z2 = at(ri, i + 1)
      if ri == 1 then
        v(0, 0, 0, ar, ag, ab, ao); v(x1, 0, z1, br, bg, bb, bo); v(x2, 0, z2, br, bg, bb, bo)
      else
        local x3, z3 = at(ri - 1, i)
        local x4, z4 = at(ri - 1, i + 1)
        v(x3, 0, z3, ar, ag, ab, ao); v(x1, 0, z1, br, bg, bb, bo); v(x2, 0, z2, br, bg, bb, bo)
        v(x3, 0, z3, ar, ag, ab, ao); v(x2, 0, z2, br, bg, bb, bo); v(x4, 0, z4, ar, ag, ab, ao)
      end
    end
  end
  return buf
end

-- a cone along +y from its base (radius 1 at y = 0) to its tip (y = 1), coloured along its length
local function cone(stops, rings, seg)
  local buf = new_buf(rings * seg * 6)
  local v = writer(buf)
  for ri = 1, rings do
    local t0, t1 = (ri - 1) / rings, ri / rings
    -- (the radius narrows fast toward the tip: a comet's taper)
    local r0, r1 = (1 - t0) ^ 1.5, (1 - t1) ^ 1.5
    local ar, ag, ab, ao = grad(stops, t0)
    local br, bg, bb, bo = grad(stops, t1)
    for i = 0, seg - 1 do
      local a1, a2 = i / seg * 2 * pi, (i + 1) / seg * 2 * pi
      local x1, z1, x2, z2 = cos(a1), sin(a1), cos(a2), sin(a2)
      v(x1 * r0, t0, z1 * r0, ar, ag, ab, ao); v(x2 * r0, t0, z2 * r0, ar, ag, ab, ao); v(x2 * r1, t1, z2 * r1, br, bg, bb, bo)
      v(x1 * r0, t0, z1 * r0, ar, ag, ab, ao); v(x2 * r1, t1, z2 * r1, br, bg, bb, bo); v(x1 * r1, t1, z1 * r1, br, bg, bb, bo)
    end
  end
  return buf
end

-- a sphere of radius 1 (top = +y), coloured by height (stops from the bottom, 0, to the top, 1); hemi: the upper half,
-- rough: each vertex pushed out by a seeded amount (a rock)
local function sphere(stops, rings, seg, hemi, rough, seed)
  local buf = new_buf(rings * seg * 6)
  local v = writer(buf)
  local h = seed or 7
  local bump = {}
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local function rad(ri, i)
    if not rough then return 1 end
    local key = ri * 1000 + (i % seg)
    if ri == 0 or ri == rings then key = ri * 1000 end
    if not bump[key] then bump[key] = 1 + (u() - 0.5) * rough end
    return bump[key]
  end
  local lat0 = hemi and 0 or -pi / 2
  local function p(ri, i)
    local lat = lat0 + (pi / 2 - lat0) * ri / rings
    local a = i / seg * 2 * pi
    local r = rad(ri, i)
    return cos(lat) * cos(a) * r, sin(lat) * r, cos(lat) * sin(a) * r
  end
  for ri = 0, rings - 1 do
    local cr0, cg0, cb0, co0 = grad(stops, ri / rings)
    local cr1, cg1, cb1, co1 = grad(stops, (ri + 1) / rings)
    for i = 0, seg - 1 do
      local ax, ay, az = p(ri, i)
      local bx, by, bz = p(ri, i + 1)
      local cx, cy, cz = p(ri + 1, i)
      local dx, dy, dz = p(ri + 1, i + 1)
      v(ax, ay, az, cr0, cg0, cb0, co0); v(bx, by, bz, cr0, cg0, cb0, co0); v(dx, dy, dz, cr1, cg1, cb1, co1)
      v(ax, ay, az, cr0, cg0, cb0, co0); v(dx, dy, dz, cr1, cg1, cb1, co1); v(cx, cy, cz, cr1, cg1, cb1, co1)
    end
  end
  return buf
end

-- a flat ring (an annulus) in y = 0 between radii r0 and 1, coloured across its width (stops from inside to outside)
local function ring(stops, r0, bands, seg)
  local buf = new_buf(bands * seg * 6)
  local v = writer(buf)
  for bi = 1, bands do
    local t0, t1 = (bi - 1) / bands, bi / bands
    local q0, q1 = r0 + (1 - r0) * t0, r0 + (1 - r0) * t1
    local ar, ag, ab, ao = grad(stops, t0)
    local br, bg, bb, bo = grad(stops, t1)
    for i = 0, seg - 1 do
      local a1, a2 = i / seg * 2 * pi, (i + 1) / seg * 2 * pi
      local x1, z1, x2, z2 = cos(a1), sin(a1), cos(a2), sin(a2)
      v(x1 * q0, 0, z1 * q0, ar, ag, ab, ao); v(x2 * q0, 0, z2 * q0, ar, ag, ab, ao); v(x2 * q1, 0, z2 * q1, br, bg, bb, bo)
      v(x1 * q0, 0, z1 * q0, ar, ag, ab, ao); v(x2 * q1, 0, z2 * q1, br, bg, bb, bo); v(x1 * q1, 0, z1 * q1, br, bg, bb, bo)
    end
  end
  return buf
end

-- Pools ----------------------------------------------------------------------------------------

local RES = {}
local function res(name, buf)
  RES[name] = RES[name] or resource.create_buffer("/tc/meteor_" .. name .. ".bufferc", { buffer = buf })
  return RES[name]
end

local function obj(factory_url, r)
  local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
  local url = msg.url(nil, id, "mesh")
  go.set(url, "vertices", r)
  msg.post(url, MSG_DISABLE)
  return { id = id, url = url, on = false }
end

local function show(o, on)
  if o.on ~= on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
end

local function set_tint(o, r, g, b, a)
  tint.x, tint.y, tint.z, tint.w = r, g, b, a
  go.set(o.url, H_TINT, tint)
end

-- o at world point (x, y, z), its +y along (ux, uy, uz), scaled (sx across, sy along)
local function put(o, x, y, z, ux, uy, uz, sx, sy, yaw)
  pos.x, pos.y, pos.z = x, y, z
  go.set_position(pos, o.id)
  local l = sqrt(ux * ux + uy * uy + uz * uz)
  if l < 1e-9 then ux, uy, uz, l = 0, 1, 0, 1 end
  go.set_rotation(vmx.set_up_yaw(rot, ux / l, uy / l, uz / l, yaw or 0), o.id)
  scl.x, scl.y, scl.z = sx, sy or sx, sx
  go.set_scale(scl, o.id)
end

-- a flat thing facing the eye
local function face(o, x, y, z, s, yaw)
  local e = state.eye
  put(o, x, y, z, e.x - x, e.y - y, e.z - z, s, s, yaw)
end

function M.init(W, urls, surface, fx)
  M.surface, M.fx = surface, fx
  if M.core then return end
  local glow, soft = urls.glow_factory, urls.soft_factory
  if not (glow and soft) then return end
  -- the head: a white-hot core, its corona and a wide halo of burning air
  M.core = obj(glow, res("core", sphere({ { 0, 1, 1, 0.92, 7 }, { 0.45, 1, 0.93, 0.62, 7 }, { 1, 1, 0.80, 0.42, 7 } }, 6, 12)))
  local corona = res("corona", disc({ { 0, 1, 0.97, 0.86, 7 }, { 0.14, 1, 0.86, 0.52, 7 }, { 0.38, 0.95, 0.52, 0.18, 7 },
    { 0.7, 0.42, 0.14, 0.03, 7 }, { 1, 0, 0, 0, 7 } }, 12, 32))
  M.corona = obj(glow, corona)
  M.halo = obj(glow, res("halo", disc({ { 0, 0.70, 0.44, 0.20, 7 }, { 0.3, 0.44, 0.21, 0.08, 7 }, { 0.65, 0.16, 0.06, 0.02, 7 },
    { 1, 0, 0, 0, 7 } }, 10, 32)))
  -- the plasma: a white-hot streak inside a long orange-red one, both tapering to nothing
  M.streak_in = obj(glow, res("streak_in", cone({ { 0, 1, 0.96, 0.82, 7 }, { 0.2, 0.98, 0.80, 0.40, 7 }, { 0.55, 0.70, 0.30, 0.07, 7 },
    { 1, 0, 0, 0, 7 } }, 12, 16)))
  M.streak_out = obj(glow, res("streak_out", cone({ { 0, 0.95, 0.55, 0.18, 7 }, { 0.25, 0.75, 0.28, 0.06, 7 }, { 0.6, 0.32, 0.08, 0.02, 7 },
    { 1, 0, 0, 0, 7 } }, 14, 16)))
  -- the ground lit by it, then glowing in the crater
  M.ground = obj(glow, res("ground", disc({ { 0, 1, 0.72, 0.36, 7 }, { 0.35, 0.80, 0.40, 0.12, 7 }, { 0.7, 0.30, 0.10, 0.02, 7 },
    { 1, 0, 0, 0, 7 } }, 8, 32)))
  -- the strike: a blinding flash, the fireball's tongues of flame, the shock rings, the curtain of thrown earth
  M.flash = obj(glow, res("flash", disc({ { 0, 1, 1, 1, 7 }, { 0.25, 1, 0.98, 0.9, 7 }, { 0.6, 0.8, 0.62, 0.4, 7 }, { 1, 0, 0, 0, 7 } }, 10, 32)))
  local fire = res("fire", disc({ { 0, 1, 0.96, 0.80, 7 }, { 0.3, 1, 0.72, 0.30, 7 }, { 0.65, 0.62, 0.22, 0.05, 7 }, { 1, 0, 0, 0, 7 } }, 8, 20))
  M.shock = obj(glow, res("shock", ring({ { 0, 0, 0, 0, 7 }, { 0.6, 0.9, 0.80, 0.60, 7 }, { 0.85, 1, 0.92, 0.75, 7 }, { 1, 0, 0, 0, 7 } }, 0.86, 4, 64)))
  M.shock2 = obj(glow, RES.shock)
  M.curtain = obj(soft, res("curtain", cone({ { 0, 0.40, 0.33, 0.27, 6 }, { 0.5, 0.48, 0.41, 0.34, 4 }, { 1, 0.55, 0.49, 0.42, 0 } }, 6, 28)))
  -- soft smoke and dust (billboards whose edges fade to nothing); sparks and embers; rock thrown out
  local smoke = res("smoke", disc({ { 0, 0.62, 0.60, 0.58, 6 }, { 0.45, 0.64, 0.62, 0.60, 5 }, { 0.8, 0.68, 0.66, 0.64, 2 },
    { 1, 0.70, 0.68, 0.66, 0 } }, 6, 20))
  local spark = res("spark", disc({ { 0, 1, 0.95, 0.75, 7 }, { 0.4, 1, 0.66, 0.25, 7 }, { 1, 0, 0, 0, 7 } }, 4, 12))
  local chunk = res("chunk", sphere({ { 0, 0.14, 0.10, 0.08, 7 }, { 1, 0.32, 0.25, 0.19, 7 } }, 3, 5, false, 0.6, 17))
  M.puffs, M.cloud, M.fires, M.sparks, M.debris, M.embers = {}, {}, {}, {}, {}, {}
  for i = 1, M.PUFFS do M.puffs[i] = obj(soft, smoke) end
  for i = 1, M.CLOUD do M.cloud[i] = obj(soft, smoke) end
  for i = 1, M.FIRES do M.fires[i] = obj(glow, fire) end
  for i = 1, M.SPARKS do M.sparks[i] = obj(glow, spark) end
  for i = 1, M.DEBRIS do M.debris[i] = obj(soft, chunk) end
  for i = 1, M.EMBERS do M.embers[i] = obj(glow, spark) end
  M.singles = { M.core, M.corona, M.halo, M.streak_in, M.streak_out, M.ground, M.flash, M.shock, M.shock2, M.curtain }
  M.pools = { M.puffs, M.cloud, M.fires, M.sparks, M.debris, M.embers }
end

-- The sequence -----------------------------------------------------------------------------------

local function unit(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-9 then return 0, 1, 0 end
  return x / l, y / l, z / l
end

local function eye_dist(x, y, z)
  local e = state.eye
  local dx, dy, dz = x - e.x, y - e.y, z - e.z
  return sqrt(dx * dx + dy * dy + dz * dz)
end

-- a seeded number 0..1
local function hash01(i, k)
  local h = (i * 73856093 + k * 19349663) % 2147483647
  h = (h * 48271) % 2147483647
  h = (h * 48271) % 2147483647
  return h / 2147483647
end

local function hide_all()
  for _, o in ipairs(M.singles or {}) do show(o, false) end
  for _, L in ipairs(M.pools or {}) do
    for _, o in ipairs(L) do show(o, false); o.live = false end
  end
end

--- Start the fall onto unit direction (ux, uy, uz) (the point aimed at); commit() gives the sim its command at the strike.
-- instant: the strike now (the bridge's harnesses), commit already done.
function M.cast(W, ux, uy, uz, commit, instant)
  if not M.core then if commit then commit() end; return end
  -- (a rock still in the air strikes now: its command is never lost)
  if M.seq and not M.seq.hit and M.seq.commit then M.seq.commit() end
  hide_all()
  local R = M.surface and M.surface(ux, uy, uz) or state.R
  local Tx, Ty, Tz = ux * R, uy * R, uz * R
  -- the way it comes: from high above and beyond the point as the eye looks (the camera's up laid on the ground), a little
  -- to the right - so in every view it crosses the frame from its top toward the point; from afar it comes in low and
  -- long (it crosses the planet's face), close in steeply (out of the sky)
  local cam = require("game.view.camera")
  local f = cam.get_frame()
  local Ux, Uy, Uz = f[4], f[5], f[6]
  local d = Ux * ux + Uy * uy + Uz * uz
  local Fx, Fy, Fz = unit(Ux - d * ux, Uy - d * uy, Uz - d * uz)
  local Rx, Ry, Rz = unit(uy * Fz - uz * Fy, uz * Fx - ux * Fz, ux * Fy - uy * Fx)
  local dist = eye_dist(Tx, Ty, Tz)
  local a = math.rad(38 + 30 * max(0, min(1, (dist - 4) / 30)))
  local ca, sa = cos(a), sin(a)
  local Dx, Dy, Dz = unit(ux * ca + (Fx * 0.97 + Rx * 0.2) * sa, uy * ca + (Fy * 0.97 + Ry * 0.2) * sa, uz * ca + (Fz * 0.97 + Rz * 0.2) * sa)
  -- where it appears: just inside the top of the frame (sampled along its way)
  local w, h = state.window_w or 1280, state.window_h or 720
  local s_top = 60
  local s = 0.2
  while s < 60 do
    local sx, sy = cam.project(Tx + Dx * s, Ty + Dy * s, Tz + Dz * s)
    if not sx or sy > h * 0.8 or sx < w * 0.2 or sx > w * 0.8 then s_top = s; break end
    s = s * 1.08
  end
  local s0 = max(1.0, min(60, s_top * 0.95))
  M.seq = { t = 0, ux = ux, uy = uy, uz = uz, T = { Tx, Ty, Tz }, D = { Dx, Dy, Dz }, s0 = s0, R = R,
    F = { Fx, Fy, Fz }, Rt = { Rx, Ry, Rz }, commit = commit, hit = false, puff_t = 0, puff_i = 0, spark_i = 0,
    vis = max(0.35, min(4, dist * 0.09)) }
  M.seq.t = instant and M.FALL or 0
  M.hit_ms = nil
end

-- where the rock is at t (0..FALL): falling faster and faster, a slight curve as the air takes it
local function rock_at(S, t)
  local k = min(1, t / M.FALL)
  local q = 1 - k ^ 1.65                     -- the distance left (1 at the start, 0 at the strike)
  local s = S.s0 * q
  local bend = 0.05 * S.s0 * q * (1 - q)     -- (a gentle arc)
  local T, D, F = S.T, S.D, S.F
  return T[1] + D[1] * s + F[1] * bend, T[2] + D[2] * s + F[2] * bend, T[3] + D[3] * s + F[3] * bend, k
end

-- a point at (along F, along Rt, up) from the strike, in world units
local function off(S, f, r, u)
  local T, F, Rt = S.T, S.F, S.Rt
  return T[1] + F[1] * f + Rt[1] * r + S.ux * u, T[2] + F[2] * f + Rt[2] * r + S.uy * u, T[3] + F[3] * f + Rt[3] * r + S.uz * u
end

local function strike(W, S)
  S.hit = true
  S.ht = 0
  M.hit_ms = require("game.platform.clock").now_ms()
  -- the world's own strike: the command, at this moment
  if S.commit then S.commit(); S.commit = nil end
  local fx = M.fx   -- (game/view/fx.lua, handed over at init: no require back into it)
  if fx then fx.flash = 2.6 end
  local dist = eye_dist(S.T[1], S.T[2], S.T[3])
  require("game.view.camera").shake(min(1.5, 2.8 / (1 + dist * 0.22)), 2.4)
  local cv = require("sim.config").V3
  local sc, B = S.vis, cv.imp_blast * S.vis
  -- the fireball's tongues of flame
  for i, o in ipairs(M.fires) do
    local a = hash01(i, 41) * 2 * pi
    local rr = 0.15 + 0.5 * hash01(i, 42)
    o.f, o.r, o.u = cos(a) * rr * B * 0.5, sin(a) * rr * B * 0.5, (0.05 + 0.35 * hash01(i, 43)) * B
    o.vf, o.vr, o.vu = cos(a) * rr * B * 0.6, sin(a) * rr * B * 0.6, (0.3 + 0.7 * hash01(i, 44)) * B
    o.s = (0.35 + 0.35 * hash01(i, 45)) * B
    o.life = 1.3 + 1.2 * hash01(i, 46)
    o.age, o.live = 0, true
  end
  -- the cloud: a column rising, a cap spreading at its top, a surge of dust rolling out low over the ground
  for i, o in ipairs(M.cloud) do
    local a = hash01(i, 51) * 2 * pi
    local kind = i <= 11 and "column" or (i <= 18 and "cap" or "surge")
    o.kind = kind
    o.age, o.live = 0, true
    o.a = a
    if kind == "column" then
      o.f, o.r, o.u = cos(a) * 0.1 * B, sin(a) * 0.1 * B, 0.05 * B
      o.vf, o.vr, o.vu = cos(a) * 0.06 * B, sin(a) * 0.06 * B, (0.5 + 0.9 * hash01(i, 52)) * B
      o.s0, o.s1 = 0.25 * B, (0.55 + 0.45 * hash01(i, 53)) * B
      o.life = 10 + 6 * hash01(i, 54)
      o.c = 0.52 + 0.1 * hash01(i, 55)
    elseif kind == "cap" then
      o.f, o.r, o.u = 0, 0, 0.1 * B
      o.vf, o.vr, o.vu = cos(a) * (0.25 + 0.3 * hash01(i, 56)) * B, sin(a) * (0.25 + 0.3 * hash01(i, 56)) * B, (1.3 + 0.5 * hash01(i, 57)) * B
      o.s0, o.s1 = 0.3 * B, (0.75 + 0.4 * hash01(i, 58)) * B
      o.life = 12 + 6 * hash01(i, 59)
      o.c = 0.6 + 0.08 * hash01(i, 60)
    else
      o.f, o.r, o.u = cos(a) * 0.2 * B, sin(a) * 0.2 * B, 0.03 * B
      local v = (1.8 + 1.4 * hash01(i, 61)) * B
      o.vf, o.vr, o.vu = cos(a) * v, sin(a) * v, 0.05 * B
      o.s0, o.s1 = 0.2 * B, (0.5 + 0.35 * hash01(i, 62)) * B
      o.life = 6 + 4 * hash01(i, 63)
      o.c = 0.64 + 0.08 * hash01(i, 64)
    end
  end
  -- rock and embers thrown out (each its own way, seeded by its number)
  local ux, uy, uz = S.ux, S.uy, S.uz
  local F, Rt = S.F, S.Rt
  for i, o in ipairs(M.debris) do
    local a = hash01(i, 1) * 2 * pi
    local out = (0.35 + 0.9 * hash01(i, 2)) * sc
    local up = (0.9 + 1.6 * hash01(i, 3)) * sc
    o.vx = (F[1] * cos(a) + Rt[1] * sin(a)) * out + ux * up
    o.vy = (F[2] * cos(a) + Rt[2] * sin(a)) * out + uy * up
    o.vz = (F[3] * cos(a) + Rt[3] * sin(a)) * out + uz * up
    o.x, o.y, o.z = S.T[1] + ux * 0.02, S.T[2] + uy * 0.02, S.T[3] + uz * 0.02
    o.s = (0.010 + 0.022 * hash01(i, 4)) * sc
    o.spin = hash01(i, 5) * 9
    o.landed, o.live = false, true
  end
  for i, o in ipairs(M.embers) do
    local a = hash01(i, 11) * 2 * pi
    local out = (0.5 + 1.4 * hash01(i, 12)) * sc
    local up = (0.6 + 2.0 * hash01(i, 13)) * sc
    o.vx = (F[1] * cos(a) + Rt[1] * sin(a)) * out + ux * up
    o.vy = (F[2] * cos(a) + Rt[2] * sin(a)) * out + uy * up
    o.vz = (F[3] * cos(a) + Rt[3] * sin(a)) * out + uz * up
    o.x, o.y, o.z = S.T[1], S.T[2], S.T[3]
    o.s = (0.02 + 0.03 * hash01(i, 14)) * sc
    o.life = 0.9 + 1.4 * hash01(i, 15)
    o.age, o.live = 0, true
  end
end

--- Each frame. running: the world's time runs (the fall waits while it is paused); frozen: the view's effects stand still.
function M.update(W, dt, frozen, running)
  local S = M.seq
  if not S then return end
  local step = (frozen or not running) and 0 or min(dt, 0.2)
  S.t = S.t + step
  local G = 2.4    -- the debris' gravity, in the view's own scale (units / s^2 per unit of S.vis)
  local any = false
  -- the fall ---------------------------------------------------------------------------------------------
  if not S.hit then
    any = true
    local x, y, z, k = rock_at(S, S.t)
    local px, py, pz = rock_at(S, max(0, S.t - 0.05))
    local vx, vy, vz = unit(x - px, y - py, z - pz)
    if S.t <= 0.05 then vx, vy, vz = -S.D[1], -S.D[2], -S.D[3] end
    local dist = eye_dist(x, y, z)
    -- seen size: an angle of the eye's view that grows as it comes (from a far bright point to a fireball), the heat of the
    -- air in front of it rising as it falls into thicker air (it appears as a faint point; the first quarter it kindles)
    local heat = min(1, 0.12 + 0.88 * k ^ 1.2)
    local appear = min(1, 0.35 + S.t / 0.6)
    local ang = 0.0045 + 0.011 * k ^ 1.5
    local r = dist * ang
    local flick = 1 + 0.10 * sin(S.t * 47) + 0.06 * sin(S.t * 83)
    show(M.core, true); face(M.core, x, y, z, r * 0.5)
    set_tint(M.core, 1, 1, 1, (1.1 + 0.9 * heat) * appear)
    show(M.corona, true); face(M.corona, x, y, z, r * (1.8 + 0.8 * heat) * flick, S.t * 2)
    set_tint(M.corona, 1, 1, 1, (0.55 + 0.85 * heat) * flick * appear)
    show(M.halo, true); face(M.halo, x, y, z, r * (3.5 + 5 * heat), -S.t)
    set_tint(M.halo, 1, 1, 1, (0.18 + 0.42 * heat) * appear)
    -- the plasma behind it, along its way, longer and brighter as it heats
    local len_in = r * (3 + 9 * heat)
    local len_out = r * (6 + 26 * heat) * flick
    show(M.streak_in, heat > 0.15); put(M.streak_in, x, y, z, -vx, -vy, -vz, r * 0.5, len_in, S.t * 5)
    set_tint(M.streak_in, 1, 1, 1, (0.5 + 0.8 * heat) * appear)
    show(M.streak_out, heat > 0.15); put(M.streak_out, x, y, z, -vx, -vy, -vz, r * 1.05, len_out, -S.t * 3)
    set_tint(M.streak_out, 1, 1, 1, (0.3 + 0.6 * heat) * appear)
    -- the ground below it, lit in the last second and a half
    local gl = max(0, (S.t - (M.FALL - 1.6)) / 1.6)
    show(M.ground, gl > 0)
    if gl > 0 then
      local gs = (0.25 + 0.9 * gl) * S.vis
      put(M.ground, S.T[1] + S.ux * 0.01, S.T[2] + S.uy * 0.01, S.T[3] + S.uz * 0.01, S.ux, S.uy, S.uz, gs, gs)
      set_tint(M.ground, 1, 1, 1, 0.3 + 1.2 * gl * gl)
    end
    -- the frame brightens as it comes; the ground trembles in the last moment
    local fx = M.fx
    if fx then fx.flash = max(fx.flash or 0, 0.1 + 0.8 * k ^ 3) end
    if S.t > M.FALL - 0.6 and not S.rumble then S.rumble = true; require("game.view.camera").shake(0.22, 0.8) end
    -- smoke left hanging where it passed (thin by the head, widening as it drifts), sparks shed
    S.puff_t = S.puff_t + step
    while S.puff_t > 0.055 and step > 0 do
      S.puff_t = S.puff_t - 0.055
      S.puff_i = S.puff_i % #M.puffs + 1
      local o = M.puffs[S.puff_i]
      o.x, o.y, o.z = x - vx * r * 2.5, y - vy * r * 2.5, z - vz * r * 2.5
      o.age, o.s0 = 0, r * (0.7 + 0.4 * hash01(S.puff_i, 21)) * (0.5 + 0.7 * heat)
      o.life = 4.5 + 3 * hash01(S.puff_i, 22)
      o.dx, o.dy, o.dz = S.F[1] * 0.015 * S.vis, S.F[2] * 0.015 * S.vis, S.F[3] * 0.015 * S.vis
      o.a = heat * 0.55
      o.live = true
    end
    if step > 0 and heat > 0.3 then
      for _ = 1, 1 do
        S.spark_i = S.spark_i % #M.sparks + 1
        local o = M.sparks[S.spark_i]
        local aa = hash01(S.spark_i, floor(S.t * 30)) * 2 * pi
        o.x, o.y, o.z = x, y, z
        o.vx = -vx * r * 5 + (S.F[1] * cos(aa) + S.Rt[1] * sin(aa)) * r * 2.5
        o.vy = -vy * r * 5 + (S.F[2] * cos(aa) + S.Rt[2] * sin(aa)) * r * 2.5
        o.vz = -vz * r * 5 + (S.F[3] * cos(aa) + S.Rt[3] * sin(aa)) * r * 2.5
        o.age, o.life, o.s, o.live = 0, 0.4 + 0.4 * hash01(S.spark_i, 3), r * 0.6, true
      end
    end
    if S.t >= M.FALL then
      for _, o in ipairs({ M.core, M.corona, M.halo, M.streak_in, M.streak_out }) do show(o, false) end
      strike(W, S)
    end
  else
    -- the strike and what it throws up -------------------------------------------------------------------
    S.ht = S.ht + step
    local t = S.ht
    local T, ux, uy, uz = S.T, S.ux, S.uy, S.uz
    local cv = require("sim.config").V3
    local sc = S.vis
    if t < M.AFTER then any = true end
    -- the flash: a blinding white disc swelling in a blink
    local fl = t / 0.5
    show(M.flash, fl < 1)
    if fl < 1 then
      local fs = (0.2 + cv.imp_flash * cv.imp_reach_k * 1.1 * min(1, t / 0.12)) * sc
      local fx2, fy2, fz2 = off(S, 0, 0, 0.08 * sc)
      face(M.flash, fx2, fy2, fz2, fs)
      set_tint(M.flash, 1, 1, 1, 3.5 * (1 - fl) ^ 2)
    end
    -- the crater glowing, cooling over seconds
    local cg = t / 7
    show(M.ground, cg < 1)
    if cg < 1 then
      local gs = (cv.imp_crater * 2.4) * sc * (1 + 0.3 * min(1, t))
      put(M.ground, T[1] + ux * 0.01, T[2] + uy * 0.01, T[3] + uz * 0.01, ux, uy, uz, gs, gs)
      set_tint(M.ground, 1, 0.75 + 0.25 * (1 - cg), 0.6 * (1 - cg), 1.8 * (1 - cg) ^ 1.5)
    end
    -- the fireball: tongues of flame swelling and rising, cooling from white through orange to nothing
    for _, o in ipairs(M.fires) do
      if o.live then
        if step > 0 then
          o.age = o.age + step
          o.f, o.r, o.u = o.f + o.vf * step, o.r + o.vr * step, o.u + o.vu * step
          o.vf, o.vr, o.vu = o.vf * (1 - 1.6 * step), o.vr * (1 - 1.6 * step), o.vu * (1 - 0.8 * step)
        end
        local f = o.age / o.life
        if f >= 1 then o.live = false; show(o, false)
        else
          local x, y, z = off(S, o.f, o.r, o.u)
          face(o, x, y, z, o.s * (0.35 + 0.9 * min(1, f * 3)) * (1 + 0.4 * f), o.age * 0.7)
          local hot = 1 - f
          set_tint(o, 1, 0.45 + 0.55 * hot, 0.15 + 0.85 * hot * hot, 2.4 * hot ^ 1.4)
          show(o, true)
        end
      end
    end
    -- the shock: two bright rings running out over the ground to past the blast's reach
    for si, o in ipairs({ M.shock, M.shock2 }) do
      local st = (t - (si - 1) * 0.2) / (0.9 + 0.6 * si)
      show(o, st > 0 and st < 1)
      if st > 0 and st < 1 then
        local sr = (0.1 + cv.imp_flash * cv.imp_reach_k * (1.25 + 0.45 * si) * (st ^ 0.55)) * sc
        put(o, T[1] + ux * 0.012, T[2] + uy * 0.012, T[3] + uz * 0.012, ux, uy, uz, sr, sr)
        set_tint(o, 1, 1, 1, (si == 1 and 2.0 or 1.0) * (1 - st) ^ 1.3)
      end
    end
    -- the curtain of thrown earth: a cone opening up from the crater, falling away
    local cu = t / 2.2
    show(M.curtain, cu < 1)
    if cu < 1 then
      local cr = (0.06 + cv.imp_crater * 3.0 * cu ^ 0.5) * sc
      local ch = (0.12 + cv.imp_blast * 1.1 * cu ^ 0.6) * sc
      put(M.curtain, T[1] + ux * ch, T[2] + uy * ch, T[3] + uz * ch, -ux, -uy, -uz, cr, ch, t)
      set_tint(M.curtain, 1, 1, 1, min(1, cu * 6) * (1 - cu) ^ 1.3 * 0.8)
    end
    -- the cloud: smoke and dust billowing up and rolling out
    for i, o in ipairs(M.cloud) do
      if o.live then
        if step > 0 then
          o.age = o.age + step
          o.f, o.r, o.u = o.f + o.vf * step, o.r + o.vr * step, o.u + o.vu * step
          local drag = o.kind == "surge" and 1.1 or 0.45
          o.vf, o.vr = o.vf * (1 - drag * step), o.vr * (1 - drag * step)
          o.vu = o.vu * (1 - (o.kind == "cap" and 0.55 or 0.35) * step)
          -- (the cap spreads out once it has risen)
          if o.kind == "cap" and o.age > 1.2 and not o.spread then
            o.spread = true
            local v = (0.5 + 0.4 * hash01(i, 70)) * cv.imp_blast * sc
            o.vf, o.vr = o.vf + cos(o.a) * v, o.vr + sin(o.a) * v
          end
        end
        local f = o.age / o.life
        if f >= 1 then o.live = false; show(o, false)
        else
          local x, y, z = off(S, o.f, o.r, o.u)
          local sz = o.s0 + (o.s1 - o.s0) * min(1, (o.age / 3) ^ 0.6) * (1 + 0.5 * f)
          face(o, x, y, z, sz, i * 1.3 + o.age * 0.1)
          -- (dark and hot-tinged at first, paling to dust)
          local c = o.c + 0.15 * f
          local warm = max(0, 1 - o.age / 1.5)
          set_tint(o, c + 0.25 * warm, c * (1 - 0.05 * warm), c * (1 - 0.25 * warm), min(1, o.age * 4) * (1 - f) ^ 1.2 * 0.75)
          show(o, true)
        end
      end
    end
    -- rock thrown out and falling back (landing where the ground is), embers burning out on the way
    local surf = M.surface
    for _, o in ipairs(M.debris) do
      if o.live then
        if not o.landed and step > 0 then
          o.x, o.y, o.z = o.x + o.vx * step, o.y + o.vy * step, o.z + o.vz * step
          local nx, ny, nz = unit(o.x, o.y, o.z)
          o.vx, o.vy, o.vz = o.vx - nx * G * sc * step, o.vy - ny * G * sc * step, o.vz - nz * G * sc * step
          local gr = surf and surf(nx, ny, nz) or S.R
          local r = sqrt(o.x * o.x + o.y * o.y + o.z * o.z)
          if r < gr and t > 0.2 then o.landed = true; o.x, o.y, o.z = nx * gr, ny * gr, nz * gr end
        end
        local fade = t > 8 and max(0, 1 - (t - 8) / 4) or 1
        if fade <= 0 then o.live = false; show(o, false)
        else
          local nx, ny, nz = unit(o.x, o.y, o.z)
          put(o, o.x, o.y, o.z, nx, ny, nz, o.s, o.s * 0.8, o.landed and o.spin or (o.spin + t * 6))
          set_tint(o, 1, 1, 1, fade)
          show(o, true)
        end
      end
    end
    for _, o in ipairs(M.embers) do
      if o.live then
        if step > 0 then
          o.age = o.age + step
          o.x, o.y, o.z = o.x + o.vx * step, o.y + o.vy * step, o.z + o.vz * step
          local nx, ny, nz = unit(o.x, o.y, o.z)
          o.vx, o.vy, o.vz = o.vx - nx * G * sc * step * 0.6, o.vy - ny * G * sc * step * 0.6, o.vz - nz * G * sc * step * 0.6
        end
        local f = o.age / o.life
        if f >= 1 then o.live = false; show(o, false)
        else
          face(o, o.x, o.y, o.z, o.s * (1 - f * 0.5))
          set_tint(o, 1, 0.75 + 0.25 * (1 - f), 0.5 * (1 - f), 2.0 * (1 - f))
          show(o, true)
        end
      end
    end
  end
  -- the smoke left in the sky and the sparks, in either phase
  for i, o in ipairs(M.puffs) do
    if o.live then
      if step > 0 then o.age = o.age + step end
      local f = o.age / o.life
      if f >= 1 then
        o.live = false; show(o, false)
      else
        any = true
        o.x, o.y, o.z = o.x + o.dx * step, o.y + o.dy * step, o.z + o.dz * step
        local sz = o.s0 * (1 + 2.6 * f ^ 0.6)
        face(o, o.x, o.y, o.z, sz, i * 0.9)
        show(o, true)
        set_tint(o, 0.95, 0.93, 0.92, min(1, o.age * 5) * (1 - f) ^ 1.5 * (o.a or 0.5))
      end
    end
  end
  for _, o in ipairs(M.sparks) do
    if o.live then
      if step > 0 then o.age = o.age + step; o.x, o.y, o.z = o.x + o.vx * step, o.y + o.vy * step, o.z + o.vz * step end
      local f = o.age / o.life
      if f >= 1 then o.live = false; show(o, false)
      else
        any = true
        show(o, true); face(o, o.x, o.y, o.z, o.s * (1 - f))
        set_tint(o, 1, 0.85, 0.6, 1.8 * (1 - f))
      end
    end
  end
  if S.hit and not any then
    hide_all()
    M.seq = nil
  end
end

--- Is a rock in the air now (cast, not yet struck)?
function M.falling()
  return M.seq ~= nil and not M.seq.hit
end

return M
