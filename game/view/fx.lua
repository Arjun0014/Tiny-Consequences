-- Fire effects (Stage 1.8): burning cells already glow through the terrain data texture; this
-- adds pooled flame spikes and rising smoke puffs (unlit fx material, engine-batched) over the
-- most intense visible fires. Later stages add lightning, meteors etc. from the event feed.
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")

local M = {}

M.FLAMES, M.SMOKE = 36, 36
M.MAX_FIRES = 12
M.FIRE_MIN = 0.05

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min = math.sin, math.cos, math.sqrt, math.max, math.min

M.flames, M.smoke = {}, {}
M.stats = { fires = 0 }

local function make(kind, c1, c2, factory_url, n, out)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, 1)
  for i = 1, n do
    local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    if i == 1 then resource.set_buffer(go.get(url, "vertices"), buf) end
    msg.post(url, MSG_DISABLE)
    out[i] = { id = id, url = url, on = false, phase = i * 1.37, life = (i * 0.61) % 1 }
  end
end

function M.init(W, flame_factory, smoke_factory, surface)
  M.W, M.surface = W, surface
  if #M.flames == 0 then
    make("flame", { 1.0, 0.50, 0.12 }, { 1.0, 0.86, 0.35 }, flame_factory, M.FLAMES, M.flames)
    make("smoke", { 0.45, 0.43, 0.42 }, nil, smoke_factory, M.SMOKE, M.smoke)
  end
  M.timer = 0
  M.fires = {}
end

local function scan(W)
  local F, g = W.F, W.grid
  local eye = state.eye
  local el = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  local list = {}
  for c = 1, W.n do
    local f = F.fire[c]
    if f > M.FIRE_MIN then
      local d = (g.X[c] * eye.x + g.Y[c] * eye.y + g.Z[c] * eye.z) / el
      if d > 0.1 then list[#list + 1] = { c, f } end
    end
  end
  table.sort(list, function(a, b) return a[2] > b[2] end)
  local fires = {}
  for i = 1, min(#list, M.MAX_FIRES) do fires[i] = list[i] end
  M.fires = fires
end

local pos, scl, rot = vmath.vector3(), vmath.vector3(), vmath.quat()

local function place(obj, W, cell, off_a, off_b, lift, s, sh)
  local g = W.grid
  local sp = g.spacing * 0.3
  local lat = g.lat[cell] + off_a * sp
  local lon = g.lon[cell] + off_b * sp / max(0.2, cos(g.lat[cell]))
  local cl = cos(lat)
  local x, y, z = cl * cos(lon), sin(lat), cl * sin(lon)
  local r = M.surface(x, y, z) + lift
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, obj.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, 0), obj.id)
  scl.x, scl.y, scl.z = s, s * sh, s
  go.set_scale(scl, obj.id)
end

function M.update(W, dt, frozen)
  M.timer = M.timer - dt
  if M.timer <= 0 then
    M.timer = 0.5
    scan(W)
  end
  local fires = M.fires
  local nf = #fires
  M.stats.fires = nf
  local t = state.time
  for i, o in ipairs(M.flames) do
    local fire = nf > 0 and fires[(i - 1) % nf + 1] or nil
    local on = fire ~= nil and i <= nf * 3
    if on then
      local a, b = sin(o.phase * 3.1) * 0.8, cos(o.phase * 2.3) * 0.8
      local fl = 0.8 + 0.25 * sin(t * 11 + o.phase * 5) + 0.1 * sin(t * 23 + o.phase)
      local s = (0.09 + 0.12 * fire[2]) * fl
      place(o, W, fire[1], a, b, -0.02, s, 1.4)
    end
    if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  for i, o in ipairs(M.smoke) do
    local fire = nf > 0 and fires[(i - 1) % nf + 1] or nil
    local on = fire ~= nil and i <= nf * 3
    if on then
      if not frozen then o.life = o.life + dt * 0.35 end
      if o.life > 1 then o.life = o.life - 1; o.phase = o.phase + 1.7 end
      local l = o.life
      local a, b = sin(o.phase * 1.9) * 0.6, cos(o.phase * 2.7) * 0.6
      local s = (0.06 + 0.22 * l) * (1 - l * l) + 0.001
      place(o, W, fire[1], a + l * 0.3, b, 0.12 + l * 0.9, s, 0.8)
    end
    if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
end

return M
