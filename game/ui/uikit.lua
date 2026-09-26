-- GUI construction helpers (Stage 1.9). Call only from GUI script callbacks.
-- Style (06_PRESENTATION.md §2): cream rounded panels, soft shadows, dark text, Fredoka for
-- titles/numbers, Nunito for text, big icons, hover states.
local M = {}

M.C = {
  panel = vmath.vector4(1.0, 0.973, 0.925, 0.96),      -- #FFF8EC
  panel_solid = vmath.vector4(1.0, 0.973, 0.925, 1),
  shadow = vmath.vector4(0.10, 0.12, 0.25, 0.22),
  text = vmath.vector4(0.165, 0.137, 0.208, 1),        -- #2A2335
  muted = vmath.vector4(0.42, 0.38, 0.47, 1),
  soft = vmath.vector4(0.94, 0.90, 0.82, 1),            -- button / bar background
  hover = vmath.vector4(0.99, 0.92, 0.78, 1),
  gold = vmath.vector4(0.949, 0.698, 0.2, 1),           -- #F2B233 (god / active)
  teal = vmath.vector4(0.227, 0.651, 0.627, 1),         -- #3AA6A0 (nature)
  coral = vmath.vector4(0.949, 0.42, 0.357, 1),         -- #F26B5B (danger)
  white = vmath.vector4(1, 1, 1, 1),
  clear = vmath.vector4(1, 1, 1, 0),
}

-- Font scale factors: fredoka.font is size 44, nunito*.font size 32 (distance field).
M.FONT_SIZE = { fredoka = 44, nunito = 32, nunito_semi = 32 }

local V3 = vmath.vector3

function M.box(parent, x, y, w, h, image, color, pivot)
  local n = gui.new_box_node(V3(x, y, 0), V3(w, h, 0))
  gui.set_size_mode(n, gui.SIZE_MODE_MANUAL)
  if image then
    gui.set_texture(n, "ui")
    gui.play_flipbook(n, image)
    gui.set_size(n, V3(w, h, 0))
  end
  if image == "panel" or image == "pill" or image == "shadow" or image == "pill_s" then
    local s = image == "shadow" and 24 or (image == "pill" and 23 or (image == "pill_s" and 7 or 15))
    gui.set_slice9(n, vmath.vector4(s, s, s, s))
  end
  gui.set_color(n, color or M.C.white)
  gui.set_pivot(n, pivot or gui.PIVOT_CENTER)
  gui.set_adjust_mode(n, gui.ADJUST_FIT)
  -- layers batch the GUI into a handful of draw calls: all boxes/icons (one atlas), then
  -- text per font (layer order beats tree order)
  gui.set_layer(n, M.top and "top_box" or "box")
  if parent then gui.set_parent(n, parent, false) end
  return n
end

M.top = false   -- set while building overlays that must draw above everything (tooltip)

--- Text node in pixel size `px` (font: "fredoka" | "nunito" | "nunito_semi").
function M.text(parent, x, y, str, font, px, color, pivot)
  local n = gui.new_text_node(V3(x, y, 0), str or "")
  gui.set_font(n, font)
  local s = px / M.FONT_SIZE[font]
  gui.set_scale(n, V3(s, s, 1))
  gui.set_color(n, color or M.C.text)
  gui.set_pivot(n, pivot or gui.PIVOT_W)
  gui.set_adjust_mode(n, gui.ADJUST_FIT)
  gui.set_outline(n, vmath.vector4(0, 0, 0, 0))
  gui.set_shadow(n, vmath.vector4(0, 0, 0, 0))
  gui.set_layer(n, M.top and "top_text" or ("t_" .. font))
  if parent then gui.set_parent(n, parent, false) end
  return n
end

function M.icon(parent, x, y, name, px, color)
  return M.box(parent, x, y, px, px, "i_" .. name, color or M.C.text)
end

function M.set_icon(n, name)
  gui.play_flipbook(n, "i_" .. name)
end

--- Unscaled text width in pixels at size px.
function M.text_width(font, str, px)
  local res = gui.get_font_resource(font)
  local m = resource.get_text_metrics(res, str or "")
  return m.width * px / M.FONT_SIZE[font]
end

--- Panel with a soft drop shadow. Returns panel, shadow.
function M.panel(parent, x, y, w, h, color)
  local sh = M.box(parent, x, y - 3, w + 22, h + 22, "shadow", M.C.shadow)
  local p = M.box(parent, x, y, w, h, "panel", color or M.C.panel)
  return p, sh
end

function M.anchor(n, xa, ya)
  gui.set_xanchor(n, xa or gui.ANCHOR_NONE)
  gui.set_yanchor(n, ya or gui.ANCHOR_NONE)
end

return M
