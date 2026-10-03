-- Selection ring (Stage 1.9): one pooled gold ring (unlit fx material) that sits on the
-- surface under the selected thing, follows moving selections and gently pulses.
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local selection = require("game.ui.selection")
local ents = require("game.view.ents")
local puppets = require("game.view.puppets")
local vmx = require("game.view.vmx")

local M = {}

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sqrt, sin = math.sqrt, math.sin

local SIZE = { cell = 0.62, village = 0.42, person = 0.085, ship = 0.26, animal = 0.1, volcano = 0.4, settlement = 0.22 }

function M.init(factory_url, surface)
  M.surface = surface
  if not M.id then
    local t = propgen.mover_template("ring")
    local buf = buffer.create(t.nv, {
      { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
      { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
      { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
    })
    propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
      buffer.get_stream(buf, H_COL), { 0.95, 0.70, 0.20 }, nil, 1)
    M.id = factory.create(factory_url, vmath.vector3(0, 0, 0))
    M.url = msg.url(nil, M.id, "mesh")
    resource.set_buffer(go.get(M.url, "vertices"), buf)
    msg.post(M.url, MSG_DISABLE)
    M.on = false
  end
end

--- Unit direction of the current selection (or nil).
function M.sel_dir(W, sel)
  if not sel then return nil end
  local g = W.grid
  if sel.kind == "person" then
    local x, y, z = puppets.person_dir(sel.id)
    if x then return x, y, z end
    local v = ents.village_of_person(W, sel.id)
    if v then return g.X[v.cell], g.Y[v.cell], g.Z[v.cell] end
    return nil
  elseif sel.kind == "ship" then
    local x, y, z = puppets.ship_dir(sel.list or "ships", sel.index)
    if x then return x, y, z end
    return ents.ship_dir(W, ents.ship_list(W, sel.list)[sel.index])
  elseif sel.kind == "settlement" and W.v3 and W.S.v3 then
    -- (S19) a v3 place, small or new: the ring round its square marks it on the planet
    local s = W.S.v3.settlements[sel.sid or 0]
    if s then return require("sim.agents.map").unit(s, s.sqx or 0, s.sqy or 0) end
  elseif sel.kind == "village" then
    local v = ents.village(W, sel.id)
    local c = v and v.cell or sel.cell
    if c then return g.X[c], g.Y[c], g.Z[c] end
  elseif sel.cell and g.X[sel.cell] then
    return g.X[sel.cell], g.Y[sel.cell], g.Z[sel.cell]
  end
  return nil
end

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

function M.update(W, dt)
  if not M.id then return end
  local sel = selection.sel
  local x, y, z
  if sel and sel.kind ~= "planet" then x, y, z = M.sel_dir(W, sel) end
  local camera = require("game.view.camera")
  local on = x ~= nil and camera.facing(x, y, z)
  if on ~= M.on then M.on = on; msg.post(M.url, on and MSG_ENABLE or MSG_DISABLE) end
  if not on then return end
  local r = (sel.kind == "ship") and (state.R + 0.01) or (M.surface(x, y, z) + (sel.kind == "settlement" and 0.06 or 0.03))
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, M.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, state.time * 0.4), M.id)
  local s = (SIZE[sel.kind] or 0.3) * (1 + 0.08 * sin(state.time * 4))
  if sel.kind == "settlement" then
    -- (S19) a mark for finding the place from afar: it fades out close in, where the place shows
    local zf = camera.zoom or 1
    s = s * (1 - 0.75 * math.min(1, math.max(0, (zf - 1.05) / 0.35)))
  end
  scl.x, scl.y, scl.z = s, s, s
  go.set_scale(scl, M.id)
end

return M
