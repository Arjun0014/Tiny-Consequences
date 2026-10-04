-- Sound settings (S23): a small pill at the bottom right ("Sound") opens four levels - everything, the world
-- (its beds: wind, rain, the village, the herds), effects (axes, thunder, shouts) and the interface (the news
-- chimes) - each with - and +, and the world's sound on or off (the same as M). Kept between visits (sys.save).
-- Call only from GUI script callbacks.
local ui = require("game.ui.uikit")

local M = {}
local C = ui.C
local V3 = vmath.vector3

M.ROWS = { { key = "master", name = "Everything" }, { key = "ambient", name = "The world" }, { key = "sfx", name = "Effects" },
  { key = "ui", name = "Interface" } }
M.FILE = "tc_sound"
M.DEFAULT = { master = 0.9, ambient = 0.7, sfx = 0.85, ui = 0.6 }

local function save_path() return sys.get_save_file("tiny_consequences", M.FILE) end

-- the saved levels (or the defaults), applied to the mixer's groups
function M.load()
  local ok, t = pcall(sys.load, save_path())
  local audio = require("game.audio.audio")
  if ok and type(t) == "table" then
    for k, v in pairs(M.DEFAULT) do
      if type(t[k]) == "number" then audio.GROUPS[k] = math.max(0, math.min(1, t[k])) end
    end
    if t.on ~= nil then audio.ON = t.on and true or false end
  end
  for name, g in pairs(audio.GROUPS) do pcall(sound.set_group_gain, hash(name), g) end
end

local function store()
  local audio = require("game.audio.audio")
  local t = { on = audio.ON }
  for k in pairs(M.DEFAULT) do t[k] = audio.GROUPS[k] end
  pcall(sys.save, save_path(), t)
end

function M.build(self)
  local S = { open = false, rows = {} }
  self.soundp = S
  local pill = ui.box(nil, 1280 - 120, 14 + 20, 96, 40, "pill", C.panel)
  ui.anchor(pill, gui.ANCHOR_RIGHT, gui.ANCHOR_BOTTOM)
  S.pill = pill
  S.pill_t = ui.text(pill, 0, 0, "Sound", "fredoka", 15, C.text, gui.PIVOT_CENTER)
  local PW, PH = 250, 196
  local panel, sh = ui.panel(nil, 1280 - 12 - PW / 2, 14 + 40 + 10 + PH / 2, PW, PH)
  ui.anchor(panel, gui.ANCHOR_RIGHT, gui.ANCHOR_BOTTOM)
  ui.anchor(sh, gui.ANCHOR_RIGHT, gui.ANCHOR_BOTTOM)
  S.panel, S.sh = panel, sh
  ui.text(panel, -PW / 2 + 14, PH / 2 - 18, "SOUND", "fredoka", 15, C.text)
  S.on = ui.box(panel, PW / 2 - 42, PH / 2 - 18, 64, 22, "pill_s", C.teal)
  S.on_t = ui.text(S.on, 0, 0, "On", "fredoka", 12, C.white, gui.PIVOT_CENTER)
  for i, r in ipairs(M.ROWS) do
    local y = PH / 2 - 52 - (i - 1) * 36
    local row = { key = r.key }
    row.label = ui.text(panel, -PW / 2 + 14, y, r.name, "nunito_semi", 13, C.text)
    row.minus = ui.box(panel, 10, y, 26, 26, "circle", C.soft)
    ui.text(row.minus, 0, 1, "-", "fredoka", 16, C.text, gui.PIVOT_CENTER)
    row.bar_bg = ui.box(panel, 58, y, 54, 8, "pill_s", C.soft, gui.PIVOT_W)
    gui.set_position(row.bar_bg, V3(33, y, 0))
    row.bar = ui.box(panel, 33, y, 54, 8, "pill_s", C.gold, gui.PIVOT_W)
    row.plus = ui.box(panel, PW / 2 - 22, y, 26, 26, "circle", C.soft)
    ui.text(row.plus, 0, 1, "+", "fredoka", 16, C.text, gui.PIVOT_CENTER)
    row.minus_hit = { sound_step = -1, row = row }
    row.plus_hit = { sound_step = 1, row = row }
    S.rows[i] = row
  end
  gui.set_enabled(panel, false)
  gui.set_enabled(sh, false)
  M.load()
  return S
end

function M.update(self)
  local S = self.soundp
  if not S then return end
  local audio = require("game.audio.audio")
  gui.set_color(S.pill, (self.hover == "sound_pill" or S.open) and C.hover or C.panel)
  if not S.open then return end
  gui.set_color(S.on, audio.ON and C.teal or C.coral)
  gui.set_text(S.on_t, audio.ON and "On" or "Off (M)")
  for _, row in ipairs(S.rows) do
    local v = audio.GROUPS[row.key] or 0
    gui.set_size(row.bar, V3(math.max(2, 54 * v), 8, 0))
    gui.set_color(row.minus, self.hover == row.minus_hit and C.hover or C.soft)
    gui.set_color(row.plus, self.hover == row.plus_hit and C.hover or C.soft)
  end
end

function M.hit(self, x, y)
  local S = self.soundp
  if not S then return nil end
  if gui.pick_node(S.pill, x, y) then return "sound_pill" end
  if not S.open then return nil end
  if gui.pick_node(S.on, x, y) then return "sound_on" end
  for _, row in ipairs(S.rows) do
    if gui.pick_node(row.minus, x, y) then return row.minus_hit end
    if gui.pick_node(row.plus, x, y) then return row.plus_hit end
  end
  if gui.pick_node(S.panel, x, y) then return "panel" end
  return nil
end

function M.activate(self, h)
  local S = self.soundp
  if not S then return false end
  local audio = require("game.audio.audio")
  if h == "sound_pill" then
    S.open = not S.open
    gui.set_enabled(S.panel, S.open)
    gui.set_enabled(S.sh, S.open)
    return true
  elseif h == "sound_on" then
    audio.ON = not audio.ON
    store()
    return true
  elseif type(h) == "table" and h.sound_step then
    local k = h.row.key
    local v = math.max(0, math.min(1, (audio.GROUPS[k] or 0) + 0.1 * h.sound_step))
    audio.GROUPS[k] = math.floor(v * 10 + 0.5) / 10
    pcall(sound.set_group_gain, hash(k), audio.GROUPS[k])
    store()
    return true
  end
  return false
end

return M
