-- Player camera & selection controls (Stage 1.9, 04_PLAYER.md §4): drag = trackball rotate,
-- wheel / pinch / +- = zoom, click = pick (person > village > ship > animal > volcano > cell),
-- double-click = fly to, F = follow the selection, Esc = deselect, arrows = nudge.
-- Called from main.script's on_input after the GUI had its chance (GUI is above on the stack).
local camera = require("game.view.camera")
local picking = require("game.view.picking")
local selection = require("game.ui.selection")
local selring = require("game.view.selring")
local uistate = require("game.ui.uistate")

local M = {}

local H_TOUCH, H_MULTI = hash("touch"), hash("touch_multi")
local H_WHEEL_UP, H_WHEEL_DOWN = hash("wheel_up"), hash("wheel_down")
local H_ZOOM_IN, H_ZOOM_OUT = hash("zoom_in"), hash("zoom_out")
local H_F, H_ESC, H_TAB = hash("key_f"), hash("key_esc"), hash("key_tab")
local H_LEFT, H_RIGHT, H_UP, H_DOWN = hash("key_left"), hash("key_right"), hash("key_up"), hash("key_down")

M.DRAG_START_PX = 5
M.DOUBLE_CLICK_S = 0.35
M.DOUBLE_CLICK_PX = 14

local press = nil        -- { x, y, t, dragging }
local last_click = nil   -- { x, y, t }
local pinch = nil
local clock_s = 0

function M.tick(dt)
  clock_s = clock_s + dt
end

local function follow_selection(W)
  local sel = selection.sel
  if not sel or sel.kind == "planet" then return false end
  if sel.kind == "person" or sel.kind == "ship" or sel.kind == "animal" then
    camera.follow = function()
      if not selection.same(selection.sel, sel) then return nil end
      return selring.sel_dir(W, sel)
    end
    local want = sel.kind == "person" and 0.92 or 0.75
    if camera.tzoom < want then camera.tzoom = want end
  else
    local x, y, z = selring.sel_dir(W, sel)
    if x then camera.fly_to_dir(x, y, z, math.max(camera.tzoom, 0.55)) end
  end
  return true
end
M.follow_selection = follow_selection

--- Fly to the current selection (GUI requests, e.g. a resident clicked in the inspector).
function M.fly_to_selection(W, zoom)
  local x, y, z = selring.sel_dir(W, selection.sel)
  if x then camera.fly_to_dir(x, y, z, zoom or math.max(camera.tzoom, 0.6)) end
end

function M.click(W, sx, sy)
  local sel = picking.pick(W, sx, sy)
  local now = clock_s
  local dbl = last_click and now - last_click.t < M.DOUBLE_CLICK_S
    and math.abs(sx - last_click.x) + math.abs(sy - last_click.y) < M.DOUBLE_CLICK_PX
  last_click = { x = sx, y = sy, t = now }
  if dbl then
    last_click = nil
    local c, hx, hy, hz = picking.cell_at(W, sx, sy)
    if c then camera.fly_to_dir(hx, hy, hz, math.max(camera.tzoom + 0.2, 0.55)) end
    return sel
  end
  if sel then selection.set(sel) else selection.clear() end
  return sel
end

--- Returns true when the action was consumed.
function M.on_input(W, action_id, action)
  if action_id == H_TOUCH then
    if action.pressed then
      press = { x = action.screen_x, y = action.screen_y, dragging = false }
      camera.touch()
      if not action.released then return true end
    end
    if action.released then
      local p = press
      press = nil
      if p and not p.dragging and W then M.click(W, action.screen_x, action.screen_y) end
      return true
    elseif press then
      if not press.dragging then
        local d = math.abs(action.screen_x - press.x) + math.abs(action.screen_y - press.y)
        if d > M.DRAG_START_PX then press.dragging = true end
      end
      if press.dragging and not pinch then camera.drag(action.screen_dx or 0, action.screen_dy or 0) end
      return true
    end
  elseif action_id == H_MULTI then
    local t = action.touch
    if t and #t >= 2 then
      local dx, dy = t[1].screen_x - t[2].screen_x, t[1].screen_y - t[2].screen_y
      local d = math.sqrt(dx * dx + dy * dy)
      if pinch then camera.zoom_by((d - pinch) / 500) end
      pinch = d
      if press then press.dragging = true end
    else
      pinch = nil
    end
    return true
  elseif action_id == H_WHEEL_UP or action_id == H_ZOOM_IN then
    if action.pressed or action_id == H_WHEEL_UP then camera.zoom_by(camera.WHEEL_STEP) end
    return true
  elseif action_id == H_WHEEL_DOWN or action_id == H_ZOOM_OUT then
    if action.pressed or action_id == H_WHEEL_DOWN then camera.zoom_by(-camera.WHEEL_STEP) end
    return true
  end
  if not action.pressed then return false end
  if action_id == H_F then
    if W then follow_selection(W) end
    return true
  elseif action_id == H_ESC then
    selection.clear()
    camera.follow = nil
    return true
  elseif action_id == H_TAB then
    uistate.inspector_collapsed = not uistate.inspector_collapsed
    return true
  elseif action_id == H_LEFT then camera.drag(40, 0); return true    -- look left
  elseif action_id == H_RIGHT then camera.drag(-40, 0); return true
  elseif action_id == H_UP then camera.drag(0, -40); return true     -- look north-ish (screen up)
  elseif action_id == H_DOWN then camera.drag(0, 40); return true
  end
  return false
end

return M
