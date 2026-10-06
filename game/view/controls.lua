-- Player camera & selection controls (Stage 1.9, 04_PLAYER.md §4): drag = trackball rotate,
-- wheel / pinch / +- = zoom, click = pick (person > village > ship > animal > volcano > cell),
-- double-click = fly to, F = follow the selection, Esc = deselect, arrows = nudge.
-- Called from main.script's on_input after the GUI had its chance (GUI is above on the stack).
local camera = require("game.view.camera")
local picking = require("game.view.picking")
local selection = require("game.ui.selection")
local selring = require("game.view.selring")
local uistate = require("game.ui.uistate")
local powers = require("game.ui.powers")
local H_MOUSE_RIGHT = hash("mouse_right")

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
  -- (S26) a v3 person: the agents' view follows them (one owner of the camera's follow; game/view/agents_view.lua)
  if sel.kind == "person" and W and W.v3 and W.S and W.S.v3 then
    require("game.view.agents_view").start_follow(W, sel.id)
    return true
  end
  if sel.kind == "person" or sel.kind == "ship" or sel.kind == "animal" then
    camera.start_follow(function()
      if not selection.same(selection.sel, sel) then return nil end
      return selring.sel_dir(W, sel)
    end, sel)
    local want = sel.kind == "person" and 0.92 or 0.75
    if camera.tzoom < want then camera.tzoom = want end
  else
    local x, y, z = selring.sel_dir(W, sel)
    if x then camera.fly_to_dir(x, y, z, math.max(camera.tzoom, 0.55)) end
  end
  return true
end
M.follow_selection = follow_selection

--- (S26) Stop any follow (the camera stays where it is, free to turn).
function M.stop_follow()
  local av = require("game.view.agents_view")
  if av.follow then av.start_follow(nil, nil) end
  camera.stop_follow()
end

--- (S26) Follow the selection, or stop following it if the camera already does (F, the inspector's Follow).
function M.toggle_follow(W)
  if camera.following(selection.sel) then M.stop_follow(); return false end
  return follow_selection(W)
end

--- Fly to the current selection (GUI requests, e.g. a resident clicked in the inspector).
function M.fly_to_selection(W, zoom)
  local x, y, z = selring.sel_dir(W, selection.sel)
  if x then camera.fly_to_dir(x, y, z, zoom or math.max(camera.tzoom, 0.6)) end
end

-- (S22) Where the person or beast picked (sel) stands now on map s (only one of that map), or nil. Reads
-- the world; never writes it.
function M.entity_xy(W, s, sel)
  if not sel or not W.S.v3 then return nil end
  local now = W.S.v3.now or W.hour
  if sel.kind == "person" and sel.v3 then
    local p = W.S.v3.people[sel.id]
    if p and p.alive and (p.at or p.sett) == s.id and p.inside == 0 then return require("sim.agents.act").pos(p, now) end
  elseif sel.kind == "beast" and sel.sid == s.id then
    if sel.grp == "wild" then
      local wild = require("sim.agents.wild")
      local w = s.wildlife and wild.byk(s, sel.k)
      if w and w.alive then return wild.pos(w, now) end
    elseif sel.grp == "dog" then
      local dogs = require("sim.agents.dogs")
      local dg = dogs.byk(s, sel.k)
      if dg and dg.alive then return dogs.pos(dg, now) end
    else
      local H = s.herds and s.herds[sel.grp]
      local a = H and H.animals[sel.k]
      if a and a.alive then return require("sim.agents.stock").pos(a, now) end
    end
  end
  return nil
end

-- (S21, R7) The v3 cast's target under the pointer: its planet cell and, close to a village, the point on
-- that village's map (lightning lands there; the rest by their cells). (S22) The ground under the pointer by
-- the terrain's height; lightning or sickness aimed at a person or a beast under the pointer comes down where
-- that one stands. Returns cell, opts { sid, x, y } (opts empty away from the villages), or nil.
function M.v3_target(W, d, sx, sy)
  local c, ux, uy, uz = picking.cell_at(W, sx, sy)
  if not c then return nil end
  local opts = {}
  if not (ux and W.S.v3) then return c, opts end
  local gx, gy, gz = picking.ground_at(W, sx, sy)
  if gx then ux, uy, uz = gx, gy, gz end
  local mapm = require("sim.agents.map")
  local best, bd = nil, 0.985
  for _, s in ipairs(W.S.v3.settlements) do
    local f = s.frame
    if f and s.alive then
      local dd = f[1] * ux + f[2] * uy + f[3] * uz
      if dd > bd then best, bd = s, dd end
    end
  end
  if best then
    local x, y = mapm.local_of(best, ux, uy, uz)
    -- (S25, D-212) the point on the village's ground AS DRAWN under the pointer: the ray met with the map's own ground
    -- height (the planet's sphere put it behind what was aimed at)
    local av = require("game.view.agents_view")
    local VV = nil
    for _, v in ipairs(av.SETS or {}) do if v.s == best then VV = v; break end end
    if VV and VV.hg and x then
      local ox, oy, oz, rx, ry, rz = camera.ray(sx, sy)
      for _ = 1, 4 do
        local hx, hy, hz = camera.hit_sphere(ox, oy, oz, rx, ry, rz, av.height(VV, x, y))
        if not hx then break end
        local l = math.sqrt(hx * hx + hy * hy + hz * hz)
        local nx, ny = mapm.local_of(best, hx / l, hy / l, hz / l)
        if not nx then break end
        x, y = nx, ny
      end
    end
    if d.id == "lightning" or d.id == "sickness" then
      local ex, ey = M.entity_xy(W, best, picking.pick(W, sx, sy))
      if ex then x, y = ex, ey
      elseif d.id == "lightning" and VV and x then
        -- (S25, D-212) a tree aimed at by its trunk or its crown takes the bolt: the standing tree whose drawn trunk-to-
        -- crown line passes nearest the pointer (within a few pixels)
        local T = best.trees
        local bt, bdist = nil, 16
        require("sim.agents.blaze").trees_near(best, x, y, 0.14, function(k)
          local sc = (0.38 + 0.55 * (T.size[k] or 0.7)) * 1.25
          local x0, y0, z0 = av.world_pos(VV, T.x[k], T.y[k], 0)
          local x1, y1, z1 = av.world_pos(VV, T.x[k], T.y[k], 0.075 * sc)
          local ax, ay = camera.project(x0, y0, z0)
          local bx, by = camera.project(x1, y1, z1)
          if ax and bx then
            local vx, vy = bx - ax, by - ay
            local q = ((sx - ax) * vx + (sy - ay) * vy) / math.max(1e-6, vx * vx + vy * vy)
            q = math.max(0, math.min(1, q))
            local ddx, ddy = sx - (ax + vx * q), sy - (ay + vy * q)
            local dd = math.sqrt(ddx * ddx + ddy * ddy)
            if dd < bdist then bt, bdist = k, dd end
          end
        end)
        if bt then x, y = T.x[bt], T.y[bt] end
      end
    end
    local i = x and mapm.tile_of(best, x, y) or 0
    if i > 0 then opts.sid, opts.x, opts.y = best.id, x, y end
  end
  return c, opts
end

-- Cast the armed power at the pointer (Lightning prefers a ship or villager under it).
function M.cast(W, sx, sy)
  local d = powers.DEFS[powers.armed]
  if W.v3 and W.S.v3 then
    local c, opts = M.v3_target(W, d, sx, sy)
    if not c then return false end
    powers.cast(W, d.id, c, opts)
    return true
  end
  local c = picking.cell_at(W, sx, sy)
  if not c then return false end
  local opts = {}
  if d.id == "lightning" or d.id == "oil" then
    local sel = picking.pick(W, sx, sy)
    if sel and sel.kind == "ship" then
      local list = require("game.view.ents").ship_list(W, sel.list)
      local s = list and list[sel.index]
      if s then opts.ship, opts.list = s.id or sel.index, sel.list; c = s.cell or c end
    elseif sel and sel.kind == "person" and d.id == "lightning" then
      opts.person = sel.id
    end
  end
  powers.cast(W, d.id, c, opts)
  return true
end

--- Pointer position while aiming: the cell under it (for the aiming ring).
function M.hover(W, sx, sy)
  if not powers.armed or not W then return end
  local view = require("game.view.view")
  -- (S23) where the pointer is, for the aim preview (game/ui/hud: what the cast would act on there)
  view.aim_sx, view.aim_sy = sx, sy
  local c, ux, uy, uz = picking.cell_at(W, sx, sy)
  view.aim_cell = c
  -- (S22) the ring sits on the ground under the pointer (the terrain's height), where the bolt would land
  if W.v3 and c then
    local gx, gy, gz = picking.ground_at(W, sx, sy)
    if gx then ux, uy, uz = gx, gy, gz end
  end
  view.aim_point = view.aim_point or {}
  view.aim_point[1], view.aim_point[2], view.aim_point[3] = ux, uy, uz
end

function M.click(W, sx, sy)
  if powers.armed then return M.cast(W, sx, sy) end
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
  if action_id == nil then
    M.hover(W, action.screen_x, action.screen_y)
    return false
  end
  if action_id == H_MOUSE_RIGHT and powers.armed then
    if action.pressed then powers.disarm() end
    return true
  end
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
    if W then M.toggle_follow(W) end
    return true
  elseif action_id == H_ESC then
    if powers.armed then powers.disarm(); return true end
    selection.clear()
    M.stop_follow()
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
