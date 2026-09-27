-- Puffy clouds (Stage 1.8): a pool of cloud game objects sharing one world-space mesh buffer
-- (the engine batches them). Clouds live over moist cells, drift with the zonal winds of
-- sim/config.lua MOIST.zonal, and shrink away when the air under them dries out; new ones
-- grow over the wettest cells. Total cloud area stays below MAX_COVER of the planet surface.
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local grid = require("sim.grid")
local cfgmod = require("sim.config")
local vmx = require("game.view.vmx")

local M = {}

M.COUNT = 56
M.MAX_COVER = 0.12       -- of total surface area (half of it is visible: never > ~30% in view)
M.ALT = 0.5              -- height above the surface (world units)
M.NEAR_HIDE = { 3.0, 7.0 } -- clouds closer than this to the eye shrink away (zoomed-in views)
M.DRIFT = 0.018          -- radians per second per unit zonal wind at 1x visual speed
M.RESCAN = 2.0           -- seconds between moisture rescans

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, abs, floor, exp = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.abs, math.floor, math.exp

M.clouds = {}
M.stats = { active = 0, cover = 0 }

local function prof(tbl, a)
  if a <= tbl[1][1] then return tbl[1][2] end
  for i = 2, #tbl do
    if a <= tbl[i][1] then
      local p, q = tbl[i - 1], tbl[i]
      return p[2] + (q[2] - p[2]) * (a - p[1]) / (q[1] - p[1])
    end
  end
  return tbl[#tbl][2]
end

local seed_h = 1
local function rnd()
  seed_h = (seed_h * 48271) % 2147483647
  return seed_h / 2147483647
end

--- Creates the pool and the shared cloud geometry.
function M.init(W, factory_url)
  M.W = W
  seed_h = ((W.seed or 1) * 7919) % 2147483646 + 1
  if #M.clouds == 0 then
    local t = propgen.mover_template("cloud")
    local buf = buffer.create(t.nv, {
      { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
      { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
      { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
    })
    propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
      buffer.get_stream(buf, H_COL), { 1, 1, 1 }, nil, 1)
    for i = 1, M.COUNT do
      local id = factory.create(factory_url, vmath.vector3(0, 0, 0), nil, nil, vmath.vector3(0.001))
      M.clouds[i] = { id = id, url = msg.url(nil, id, "mesh"), lat = 0, lon = 0, s = 0, target = 0,
        size = 1, yaw = 0, alive = false, shown = true }
      if i == 1 then
        local res = go.get(msg.url(nil, id, "mesh"), "vertices")
        resource.set_buffer(res, buf)
      end
    end
  end
  M.scan(W)
  for _, c in ipairs(M.clouds) do
    c.alive = false
    c.s = 0
  end
  M.respawn_all(W, true)
  M.since = 0
end

-- Candidate cells: moist cells, weighted by how much wetter than the median they are.
function M.scan(W)
  local F = W.F
  local m = F.moisture
  local vals = {}
  for c = 1, W.n do vals[c] = m[c] end
  table.sort(vals)
  local thr = vals[floor(W.n * 0.55)] or 0
  M.thr = thr
  local cand, wsum = {}, 0
  for c = 1, W.n do
    local w = m[c] - thr
    if w > 0 then
      w = w + F.precip[c] * 2
      cand[#cand + 1] = c
      wsum = wsum + w
      cand[#cand + 1] = wsum
    end
  end
  M.cand, M.wsum = cand, wsum
end

local function pick_cell()
  local cand = M.cand
  if not cand or #cand == 0 then return nil end
  local r = rnd() * M.wsum
  for i = 2, #cand, 2 do
    if cand[i] >= r then return cand[i - 1] end
  end
  return cand[#cand - 1]
end

local function spawn(c, W, grown)
  local cell = pick_cell()
  if not cell then c.alive = false; c.target = 0; return end
  local g = W.grid
  c.lat = g.lat[cell] + (rnd() - 0.5) * g.spacing
  c.lon = g.lon[cell] + (rnd() - 0.5) * g.spacing
  c.size = 1.1 + rnd() * 1.0
  c.yaw = rnd() * 6.283
  c.alive = true
  c.target = 1
  c.s = grown and 1 or 0
  c.cell = cell
  c.flat = 0.5 + rnd() * 0.2
end

function M.respawn_all(W, grown)
  local area = 0
  local cap = M.MAX_COVER * 4 * math.pi * state.R * state.R
  for _, c in ipairs(M.clouds) do
    spawn(c, W, grown)
    local a = math.pi * (c.size * 0.55) ^ 2
    if area + a > cap then c.alive = false; c.target = 0; c.s = 0 else area = area + a end
  end
end

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

function M.update(W, dt, frozen)
  local zonal = cfgmod.MOIST.zonal
  local R = state.R
  M.since = (M.since or 0) + dt
  if M.since > M.RESCAN and not frozen then
    M.since = 0
    M.scan(W)
  end
  local g = W.grid
  local m = W.F.moisture
  local active, area = 0, 0
  local eye = state.eye
  local ex, ey, ez = eye.x, eye.y, eye.z
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local zoom = require("game.view.camera").zoom
  local cap = M.MAX_COVER * 4 * math.pi * R * R
  -- clouds drift slowly: each one moves (and has its transform written) every other frame, with
  -- twice the step, which halves the per-frame cost (05 §9 view budget)
  M.frame = (M.frame or 0) + 1
  local par = M.frame % 2
  local dt2 = dt * 2
  for i, c in ipairs(M.clouds) do
    local mine = i % 2 == par
    if not frozen and mine then
      local dt = dt2
      if c.alive then
        local u = prof(zonal, abs(c.lat) * 57.2958)
        c.lon = c.lon + u * M.DRIFT * dt / max(0.2, cos(c.lat))
        -- dry air under the cloud: fade out
        local x, y, z = cos(c.lat) * cos(c.lon), sin(c.lat), cos(c.lat) * sin(c.lon)
        c.cell = grid.nearest(g, x, y, z, c.cell)
        if m[c.cell] < (M.thr or 0) * 0.9 then c.target = 0 end
      end
      local k = 1 - exp(-dt * 0.8)
      c.s = c.s + (c.target - c.s) * k
      if c.target == 0 and c.s < 0.02 then
        spawn(c, W, false)
      end
    end
    local sz = c.size * c.s
    if M.hidden then sz = 0 end
    if sz > 0.01 then
      -- shrink clouds that would sit in front of a zoomed-in camera
      local cl = cos(c.lat)
      local r = R + M.ALT
      local dx, dy, dz = cl * cos(c.lon) * r - ex, sin(c.lat) * r - ey, cl * sin(c.lon) * r - ez
      local de = sqrt(dx * dx + dy * dy + dz * dz)
      local t = (de - M.NEAR_HIDE[1]) / (M.NEAR_HIDE[2] - M.NEAR_HIDE[1])
      if t < 1 then sz = sz * max(0, t) end
      -- zoomed in: keep the view focus clear of clouds
      if zoom > 0.35 then
        local fd = (cl * cos(c.lon) * fx + sin(c.lat) * fy + cl * sin(c.lon) * fz)
        local k = (fd - 0.955) / (0.985 - 0.955)
        if k > 0 then sz = sz * max(0, 1 - k * min(1, (zoom - 0.35) * 4)) end
      end
    end
    if sz > 0.01 then
      local a = math.pi * (sz * 0.55) ^ 2
      if area + a <= cap then
        area = area + a
        active = active + 1
      else
        sz = 0
      end
    end
    local show = sz > 0.01
    if show and (mine or not c.shown) then
      do
        local cl = cos(c.lat)
        local x, y, z = cl * cos(c.lon), sin(c.lat), cl * sin(c.lon)
        local r = R + M.ALT
        pos.x, pos.y, pos.z = x * r, y * r, z * r
        vmx.set_up_yaw(rot, x, y, z, c.yaw)
        scl.x, scl.y, scl.z = sz, sz * c.flat, sz
        go.set_position(pos, c.id)
        go.set_rotation(rot, c.id)
        go.set_scale(scl, c.id)
      end
    end
    if show ~= c.shown then
      c.shown = show
      msg.post(c.url, show and MSG_ENABLE or MSG_DISABLE)
    end
  end
  M.stats.active = active
  M.stats.cover = area / (4 * math.pi * R * R)
end

return M
