-- Animal puppets (Stage 1.8 stub; people/boats/tankers hook in with Stage 1.7 entities).
-- Pooled deer and wolf game objects (world-space material, engine-batched). Only when the
-- camera is zoomed in: cells near the view focus with high grazers/predators density get a
-- few wandering puppets. Hard cap: pool size (05 §9: <= 300 Lua-driven movers per frame).
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")

local M = {}

M.DEER, M.WOLVES = 90, 30
M.MIN_ZOOM = 0.55
M.GRAZER_MIN, M.PRED_MIN = 0.12, 0.08
M.PER_CELL = 6
M.SCALE = 1.3

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, floor, atan2 = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.floor, math.atan2

M.pool = {}
M.stats = { active = 0 }

--- Hooks for later stages (Stage 1.7 entities): people, boats and tankers arrive as
-- W.ent.people / W.ent.ships; until then these are no-ops.
function M.set_people(_list) end
function M.set_ships(_list) end

local function make_buffer(kind, c1, c2)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, 1)
  return buf
end

local seed_h = 7
local function rnd()
  seed_h = (seed_h * 48271) % 2147483647
  return seed_h / 2147483647
end

function M.init(W, deer_factory, wolf_factory, surface)
  M.W, M.surface = W, surface
  if #M.pool == 0 then
    local specs = {
      { "deer", deer_factory, M.DEER, { 0.72, 0.50, 0.32 }, { 0.45, 0.32, 0.22 } },
      { "wolf", wolf_factory, M.WOLVES, { 0.55, 0.57, 0.62 }, { 0.35, 0.36, 0.40 } },
    }
    for _, sp in ipairs(specs) do
      local buf = make_buffer(sp[1], sp[4], sp[5])
      for i = 1, sp[3] do
        local id = factory.create(sp[2], vmath.vector3(0, 0, 0))
        local url = msg.url(nil, id, "mesh")
        if i == 1 then resource.set_buffer(go.get(url, "vertices"), buf) end
        msg.post(url, MSG_DISABLE)
        M.pool[#M.pool + 1] = { id = id, url = url, kind = sp[1], on = false, cell = 0 }
      end
    end
  end
  for _, p in ipairs(M.pool) do
    if p.on then p.on = false; msg.post(p.url, MSG_DISABLE) end
    p.cell = 0
  end
  M.assign_timer = 0
end

local function unit(lat, lon)
  local cl = cos(lat)
  return cl * cos(lon), sin(lat), cl * sin(lon)
end

-- Assign puppets to the densest cells near the view focus.
local function assign(W, fx, fy, fz)
  local F, g = W.F, W.grid
  local gz, pr = F.grazers, F.predators
  local wants = { deer = {}, wolf = {} }
  for c = 1, W.n do
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > 0.93 and W.geo.ocean[c] == 0 then
      if gz[c] > M.GRAZER_MIN then wants.deer[#wants.deer + 1] = { c, gz[c] } end
      if pr[c] > M.PRED_MIN then wants.wolf[#wants.wolf + 1] = { c, pr[c] } end
    end
  end
  table.sort(wants.deer, function(a, b) return a[2] > b[2] end)
  table.sort(wants.wolf, function(a, b) return a[2] > b[2] end)
  local used = { deer = 0, wolf = 0 }
  for _, p in ipairs(M.pool) do
    local list = wants[p.kind]
    local idx = floor(used[p.kind] / M.PER_CELL) + 1
    local slot = list[idx]
    if slot then
      used[p.kind] = used[p.kind] + 1
      if p.cell ~= slot[1] then
        p.cell = slot[1]
        local sp = g.spacing * 0.45
        p.lat = g.lat[p.cell] + (rnd() - 0.5) * sp
        p.lon = g.lon[p.cell] + (rnd() - 0.5) * sp
        p.head = rnd() * 6.283
        p.home_lat, p.home_lon = g.lat[p.cell], g.lon[p.cell]
      end
      if not p.on then p.on = true; msg.post(p.url, MSG_ENABLE) end
    elseif p.on then
      p.on = false
      p.cell = 0
      msg.post(p.url, MSG_DISABLE)
    end
  end
end

local pos, rot = vmath.vector3(), vmath.quat()

function M.update(W, dt, zoom, frozen)
  local active = 0
  if zoom < M.MIN_ZOOM then
    for _, p in ipairs(M.pool) do
      if p.on then p.on = false; p.cell = 0; msg.post(p.url, MSG_DISABLE) end
    end
    M.stats.active = 0
    return
  end
  M.assign_timer = M.assign_timer - dt
  if M.assign_timer <= 0 then
    M.assign_timer = 1.0
    local t = state.target
    local l = sqrt(t.x * t.x + t.y * t.y + t.z * t.z)
    if l > 0 then assign(W, t.x / l, t.y / l, t.z / l) end
  end
  local sp = W.grid.spacing * 0.5
  for _, p in ipairs(M.pool) do
    if p.on then
      active = active + 1
      if not frozen then
        p.head = p.head + (rnd() - 0.5) * dt * 2.0
        local step = dt * 0.004 * (p.kind == "wolf" and 1.6 or 1.0)
        p.lat = p.lat + cos(p.head) * step
        p.lon = p.lon + sin(p.head) * step / max(0.2, cos(p.lat))
        local dl, dn = p.lat - p.home_lat, p.lon - p.home_lon
        if dl * dl + dn * dn > sp * sp then p.head = atan2(-dn, -dl) end
      end
      local x, y, z = unit(p.lat, p.lon)
      local r = M.surface(x, y, z)
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      go.set_position(pos, p.id)
      go.set_rotation(vmx.set_up_yaw(rot, x, y, z, -p.head), p.id)
      go.set_scale(M.SCALE, p.id)
    end
  end
  M.stats.active = active
end

return M
