-- The HUD's layout (S26): the screen in GUI reference units, the UI's scale on small windows and the room each region of
-- the screen has. The GUI is laid out on a 1280 x 720 reference and drawn with ADJUST_FIT (one uniform scale, `fit`), each
-- panel anchored to its corner or edge: on any window its visible area is at least 1280 x 720 units, so a layout that fits
-- the reference fits every window. Small windows (a phone held sideways: 844 x 390 draws at 0.54) scale every panel up by
-- `k` (about its own corner) so text stays readable - as far as the bottom row (the pills, the power bar, the sound) still
-- fits across the screen - and the panels' heights come from the room left (column_h).
-- Call only from GUI script callbacks (it reads the window).
local M = {}

M.REF_W, M.REF_H = 1280, 720
M.MARGIN = 10          -- from the screen's edges (reference units)
M.GAP = 8              -- between stacked regions
M.TOP_H = 44           -- the top bar (clock, stats)
M.PILL_H = 32          -- the bottom corners' pills (News, Your actions, Sound)
M.BAR_H = 52           -- the power bar
M.BAR_ZONE = 52 + 74   -- the power bar and what shows above it while aiming (choices, the preview)
M.COL_W = 300          -- the left column (news, Your actions) and the inspector
M.RIGHT_W = 300 + 26   -- the inspector and its tab
M.bar_w = 630          -- the power bar's width (set by the HUD when it is built)
M.pills_w = 242        -- the bottom-left pills
M.sound_w = 80         -- the sound pill
M.FS_CSS = 46          -- the page's fullscreen button and its margin (CSS pixels)
M.CENTER_MIN = 320     -- the top centre's least width (the tutorial's card)
M.k, M.fit, M.vw, M.vh = 1, 1, 1280, 720
M.win_w, M.win_h = 0, 0
M.version = 0          -- bumped when the window (and so the layout) changed

local function compute()
  local fit = math.min(M.win_w / M.REF_W, M.win_h / M.REF_H)
  if fit <= 0 then fit = 1 end
  M.fit = fit
  M.vw, M.vh = M.win_w / fit, M.win_h / fit
  -- a reference unit should be at least ~0.8 screen pixels: below that (small windows) the panels grow (up to 1.45x) -
  -- as far as the bottom row still fits: the pills left of the centred power bar, the sound pill right of it
  local k = math.max(1, math.min(1.45, 0.8 / fit))
  local half = M.vw / 2 - M.MARGIN
  local fs = M.FS_CSS / fit
  k = math.min(k, half / (M.bar_w / 2 + M.pills_w + M.GAP), (half - fs) / (M.bar_w / 2 + M.sound_w + M.GAP))
  -- and the top centre keeps room for the tutorial's card (CENTER_MIN) between the two columns at their widest
  k = math.min(k, (M.vw - 2 * M.MARGIN) / (M.COL_W + 30 + M.RIGHT_W + 4 * M.GAP + M.CENTER_MIN))
  M.k = math.max(1, k)
  M.version = M.version + 1
end

--- Read the window; returns true when the layout changed.
function M.update(win_w, win_h)
  win_w, win_h = win_w or 1280, win_h or 720
  if win_w == M.win_w and win_h == M.win_h and not M.dirty then return false end
  M.win_w, M.win_h, M.dirty = win_w, win_h, false
  compute()
  return true
end

--- The widths the bottom row has to hold (the HUD's, once built).
function M.set_widths(bar_w, pills_w, sound_w)
  if bar_w ~= M.bar_w or (pills_w and pills_w ~= M.pills_w) or (sound_w and sound_w ~= M.sound_w) then
    M.bar_w, M.pills_w, M.sound_w = bar_w, pills_w or M.pills_w, sound_w or M.sound_w
    M.dirty = true
  end
end

--- The height (in a panel's own units, before k) of a side column `w` units wide: from under the top bar down to above
-- the bottom pills - or above the power bar and its aiming row when the bar reaches under the column.
function M.column_h(w)
  local k = M.k
  w = w or M.COL_W
  local top = M.vh - M.MARGIN - (M.TOP_H + M.GAP) * k
  local bar_left = M.vw / 2 - M.bar_w * k / 2
  local col_right = M.MARGIN + (w + M.GAP) * k
  local bottom = M.MARGIN + (M.PILL_H + M.GAP) * k
  if bar_left < col_right then bottom = M.MARGIN + (M.BAR_ZONE + M.GAP) * k end
  return math.max(120, (top - bottom) / k)
end

--- The width (before k) the top centre has between the two columns at their widest (the tutorial's card).
function M.center_w()
  local k = M.k
  return (M.vw - 2 * M.MARGIN - (M.COL_W + 30 + M.RIGHT_W + 4 * M.GAP) * k) / k
end

local function scale_of(node)
  local sx, sy = 1, 1
  local n = node
  while n do
    local sc = gui.get_scale(n)
    sx, sy = sx * sc.x, sy * sc.y
    n = gui.get_parent(n)
  end
  return sx, sy
end

local function box(node, w, h)
  local p = gui.get_screen_position(node)
  local pv = gui.get_pivot(node)
  local x0, y0 = p.x - w / 2, p.y - h / 2
  if pv == gui.PIVOT_W or pv == gui.PIVOT_NW or pv == gui.PIVOT_SW then x0 = p.x end
  if pv == gui.PIVOT_E or pv == gui.PIVOT_NE or pv == gui.PIVOT_SE then x0 = p.x - w end
  if pv == gui.PIVOT_S or pv == gui.PIVOT_SW or pv == gui.PIVOT_SE then y0 = p.y end
  if pv == gui.PIVOT_N or pv == gui.PIVOT_NW or pv == gui.PIVOT_NE then y0 = p.y - h end
  return { x0, y0, x0 + w, y0 + h }
end

--- A node's rectangle in window pixels { x0, y0, x1, y1 } (y up), whatever its parents' scale: its size times the
-- product of its and its parents' scales, about its pivot.
function M.rect(node)
  local s = gui.get_size(node)
  local sx, sy = scale_of(node)
  return box(node, s.x * sx * M.fit, s.y * sy * M.fit)
end

--- A text node's drawn rectangle in window pixels (its text measured in its font, its scale and its parents', its pivot).
function M.text_rect(node)
  local res = gui.get_font_resource(gui.get_font(node))
  local m = resource.get_text_metrics(res, gui.get_text(node) or "", { line_break = true, width = 100000 })
  local sx, sy = scale_of(node)
  return box(node, m.width * sx * M.fit, m.height * sy * M.fit)
end

--- Is node and every parent of it enabled (drawn)?
function M.shown(node)
  local n = node
  while n do
    if not gui.is_enabled(n) then return false end
    n = gui.get_parent(n)
  end
  return true
end

return M
