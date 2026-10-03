-- Aiming ring for powers (Stage 2.1, 04_PLAYER.md §2): while a power is armed, a ring on the
-- surface under the pointer shows the area it will touch (radius in level-4 steps converted
-- to this grid's cells). Uses the overlay ring template (always visible, gently pulsing).
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")

local M = {}

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")

M.cell = nil          -- sim cell under the pointer while aiming
M.radius = 0          -- level-4 steps
M.on = false

function M.init(factory_url, surface)
  M.surface = surface
  if M.id then return end
  local t = propgen.mover_template("ring")
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), { 1.0, 0.86, 0.45 }, nil, 1)
  M.id = factory.create(factory_url, vmath.vector3(0, 0, 0))
  M.url = msg.url(nil, M.id, "mesh")
  M.res = resource.create_buffer("/tc/aim_ring.bufferc", { buffer = buf })
  go.set(M.url, "vertices", M.res)
  msg.post(M.url, MSG_DISABLE)
end

--- Ring radius in world units for a power radius in level-4 steps on world W.
function M.world_radius(W, steps)
  local cells = math.floor(steps / W.sscale + 0.5)
  return (cells + 0.45) * W.grid.spacing * state.R
end

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

function M.update(W, dt, cell, steps, point)
  if not M.id then return end
  local on = cell ~= nil and W ~= nil
  if on ~= M.on then M.on = on; msg.post(M.url, on and MSG_ENABLE or MSG_DISABLE) end
  M.cell = cell
  if not on then return end
  local g = W.grid
  local x, y, z = g.X[cell], g.Y[cell], g.Z[cell]
  if (steps or 0) == 0 and point and point[1] then
    -- (S21) close in, a point power (lightning) aims at the point itself, its ring as wide as a bolt reaches
    x, y, z = point[1], point[2], point[3]
    local r = M.surface(x, y, z) + 0.01
    pos.x, pos.y, pos.z = x * r, y * r, z * r
    go.set_position(pos, M.id)
    go.set_rotation(vmx.set_up_yaw(rot, x, y, z, state.time * 0.6), M.id)
    local s = (require("sim.config").V3.strike_pull or 0.08) * (1 + 0.08 * math.sin(state.time * 5))
    scl.x, scl.y, scl.z = s, s, s
    go.set_scale(scl, M.id)
    return
  end
  local r = M.surface(x, y, z) + 0.035
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, M.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, state.time * 0.6), M.id)
  local s = M.world_radius(W, steps or 0) * (1 + 0.05 * math.sin(state.time * 5))
  scl.x, scl.y, scl.z = s, s, s
  go.set_scale(scl, M.id)
end

return M
