-- Menus (S25, D-210): the title screen (Play, How the World Works, Settings, Credits), the pause menu (Esc or the
-- menu button), the settings (the sound's levels, clouds, shadows, fullscreen, the tutorial again), the credits, and
-- "How the World Works" - an illustrated guide to what the world simulates, in pages of small diagrams (icons and
-- arrows) and a few lines each. Every page describes the game as it is built (docs/v3). Drawn on the top layers above
-- the HUD and the news; while one is open the world's clock stands still (the speed is restored when it closes).
-- Call only from GUI script callbacks.
local ui = require("game.ui.uikit")

local M = {}
local C = ui.C
local V3 = vmath.vector3
local DIM = vmath.vector4(0.09, 0.08, 0.15, 0.62)
local PLUM = vmath.vector4(0.30, 0.25, 0.38, 1)
local ARROW = vmath.vector4(0.42, 0.38, 0.47, 1)

M.screen = "title"      -- nil while playing; "title" | "pause" | "settings" | "credits" | "guide"
M.prev = nil            -- the screen to go back to from settings / credits / the guide
M.PREFS_FILE = "tc_prefs"
M.prefs = { tutorial_done = false, clouds = true, shadows = true }
M.want = nil            -- requests for the HUD script: "play", "tutorial", "resume"

local function prefs_path() return sys.get_save_file("tiny_consequences", M.PREFS_FILE) end
function M.load_prefs()
  local ok, t = pcall(sys.load, prefs_path())
  if ok and type(t) == "table" then
    for k, v in pairs(t) do if M.prefs[k] ~= nil and type(v) == type(M.prefs[k]) then M.prefs[k] = v end end
  end
end
function M.save_prefs() pcall(sys.save, prefs_path(), M.prefs) end

-- The guide: each page a title, a diagram (a row of { icon, label } joined by arrows; "|" starts a second row) and lines.
M.PAGES = {
  { title = "1  The living planet", sub = "Sun, air, water and ground, day by day, on every cell of the planet",
    diagram = { { "sun", "Sun & season" }, { "thermo", "Temperature" }, { "cloud", "Wind & rain" }, { "drop", "Soil water" },
      { "wave", "Rivers" }, "|", { "rain", "Floods" }, { "drought", "Drought" }, { "fire", "Wildfire" }, { "snow", "Snow & ice" } },
    lines = { "The sun's height makes the seasons; warm air rises, winds carry the rain, and the rain soaks the soil or runs off into rivers.",
      "The sea warms and cools slower than the land, and its currents, driven by the winds, carry whatever floats on it.",
      "Too much rain floods the valleys; too little dries the ground until lightning can set it alight. Cold brings snow and frost.",
      "Every village's weather is its own planet cell's weather, drawn as clouds over it." } },
  { title = "2  Ecology", sub = "Plants, grazers, hunters and the sea - each living off the next",
    diagram = { { "leaf", "Grass & forest" }, { "deer", "Grazers" }, { "wolf", "Wolves" }, "|", { "drop", "Plankton" }, { "fish", "Fish" },
      { "boat", "Fishers" }, "|", { "tree", "Regrowth" } },
    lines = { "Forests and grass grow by sun, warmth and water; grazers eat them; wolves hunt the grazers. Each is a population that answers the others.",
      "Near the villages every deer, wolf, sheep, cow, pig and dog is an individual animal with its own place, pace and fear.",
      "Fish live off plankton in the sea; warm water, oil and nets thin them. Birds fly as flocks of individuals.",
      "Burnt or felled land grows back: fireweed, then saplings, then forest." } },
  { title = "3  People and families", sub = "Every villager is a person who lives a whole life",
    diagram = { { "person", "A person" }, { "heart", "Family" }, { "people", "Children" }, { "hourglass", "Ageing" }, "|",
      { "bread", "Hunger" }, { "thermo", "Health" }, { "bubble", "Memory" }, { "house", "Home" } },
    lines = { "Each person is born, grows up, marries, raises children, grows old and dies - nobody is a statistic.",
      "They are hungry or fed, well or hurt or sick, and they remember what happened to them and who did it.",
      "Every hour each one chooses what to do next for their own reasons: click anyone to read their reasons, family and memories.",
      "Knowledge travels by contact only: a village knows of a place only if someone has been there or heard of it." } },
  { title = "4  Settlements and economy", sub = "Work makes food, goods and buildings - and trade moves them",
    diagram = { { "wheat", "Fields" }, { "tree", "Wood" }, { "paw", "Herds" }, { "fish", "Fishing" }, "|", { "pickaxe", "Stone, iron, salt" },
      { "house", "Building" }, { "ship", "Boats & trade" }, { "star", "Fairs & credit" } },
    lines = { "Households sow and harvest fields, cut wood, keep sheep, goats, cattle and pigs, fish, weave, quarry, mine and smelt.",
      "The granary and each household's larder hold what they have; what they lack they trade for, on foot or by boat, with what they can spare.",
      "Goods have worth by need; households give, lend, owe and repay. Fairs gather traders. Houses, pens, wells and docks are built log by log." } },
  { title = "5  Society", sub = "Kin, help, quarrels and leaving", set = true,
    diagram = { { "heart", "Kin & marriage" }, { "people", "Help" }, { "settlers", "Migration" }, { "pin", "Founding" }, "|",
      { "skull", "Theft & grievance" }, { "wolf", "Raids" }, { "house", "Defence" } },
    lines = { "Kin help kin; neighbours lodge those who lost their home; relief is carried to the hungry.",
      "Brides walk to their husband's village. Households that cannot live where they are leave - and some found new places by land or sea.",
      "Harm is remembered: a theft or a killing is known and answered, sometimes with a raid; villages that are harmed may build a palisade." } },
  { title = "6  Disasters and the planet", sub = "The planet moves on its own - people live through it", set = true,
    diagram = { { "fire", "Fire" }, { "rain", "Flood" }, { "snow", "Snow" }, { "pickaxe", "Quake" }, { "volcano", "Volcano" },
      { "wave", "Tsunami" }, { "meteor", "Meteor" } },
    lines = { "Fire spreads by dryness and wind, burns its fuel and goes out: trees become charred snags, houses ruins, fields black stubble.",
      "Rivers rise over their banks; quakes bring roofs down on whoever is inside; the sea draws back before a wave.",
      "People notice danger by sight and sound, run, carry their babies, fight fires, dig out the trapped and tend the hurt - each by their own choice." } },
  { title = "7  God powers", sub = "Powers do not command outcomes. They change the physical world.", set = true,
    diagram = { { "bolt", "Lightning" }, { "rain", "Rain" }, { "drought", "Dry spell" }, { "heat", "Warm / cold air" }, { "volcano", "Stir" }, "|",
      { "skull", "Cull" }, { "paw", "Bring" }, { "blight", "Blight" }, { "oil", "Oil" }, { "meteor", "Meteor" } },
    lines = { "Rain does not 'increase food': it adds water to the ground. What the water does next - crops, floods, fires put out - is the world's.",
      "Lightning is a bolt at a point; a meteor is a crater, a blast and a flash by distance; oil is a slick the currents carry.",
      "Each power recharges in game time (the meteor takes seven days): watch what one did before the next." } },
  { title = "8  Chain reactions", sub = "One touch, and everything after it belongs to the simulation",
    diagram = { { "star", "Your action" }, { "bolt", "Physical change" }, { "people", "People & beasts react" }, "|",
      { "wheat", "Food, trade, ecology" }, { "hourglass", "Later consequences" } },
    lines = { "NEWS tells you what is happening now. Click a story to go to the very place.",
      "WHY? (the coloured badge on a story) shows its causes, back to NATURE, PEOPLE or YOU.",
      "YOUR ACTIONS shows everything each of your casts has caused so far, as a tree of real links - it grows as you skip time." } },
}

-- Buttons ---------------------------------------------------------------------------------------------------------
local function button(R, parent, x, y, w, h, label, id, primary, px)
  local b = ui.box(parent, x, y, w, h, "pill", primary and C.gold or C.soft)
  local t = ui.text(b, 0, 0, label, "fredoka", px or 19, primary and C.white or C.text, gui.PIVOT_CENTER)
  local o = { node = b, text = t, menu_btn = id, primary = primary }
  R.btns[#R.btns + 1] = o
  return o
end

local function panel(R, w, h)
  local root = ui.box(nil, 640, 360, 1, 1, "px", C.clear)
  ui.box(root, 0, -4, w + 26, h + 26, "shadow", C.shadow)
  local p = ui.box(root, 0, 0, w, h, "panel", C.panel_solid)
  return root, p
end

function M.build(self)
  M.load_prefs()
  ui.top = true
  local R = { btns = {}, screens = {} }
  self.menu = R
  R.dim = ui.box(nil, 640, 360, 3000, 2000, "px", DIM)
  -- the title screen
  do
    local root, p = panel(R, 560, 470)
    R.screens.title = root
    ui.icon(p, -232, 196, "globe", 34, C.teal)
    ui.text(p, 0, 168, "TINY CONSEQUENCES", "fredoka", 44, C.text, gui.PIVOT_CENTER)
    ui.text(p, 0, 124, "A tiny planet that lives without you.", "nunito_semi", 17, C.muted, gui.PIVOT_CENTER)
    ui.text(p, 0, 100, "Touch it once - then follow everything it causes.", "nunito_semi", 17, C.muted, gui.PIVOT_CENTER)
    R.play = button(R, p, 0, 40, 300, 58, "Play", "play", true, 26)
    R.load_t = ui.text(p, 0, 2, "", "nunito_semi", 14, C.muted, gui.PIVOT_CENTER)
    button(R, p, 0, -46, 300, 46, "How the World Works", "guide")
    button(R, p, 0, -102, 300, 46, "Settings", "settings")
    button(R, p, 0, -158, 300, 46, "Credits", "credits")
    ui.text(p, 0, -210, "Defold Game Jam 2026  -  theme: Chain Reaction", "nunito_semi", 13, C.muted, gui.PIVOT_CENTER)
  end
  -- the pause menu
  do
    local root, p = panel(R, 380, 470)
    R.screens.pause = root
    ui.text(p, 0, 196, "PAUSED", "fredoka", 32, C.text, gui.PIVOT_CENTER)
    button(R, p, 0, 128, 290, 50, "Resume", "resume", true, 22)
    button(R, p, 0, 66, 290, 46, "How the World Works", "guide")
    button(R, p, 0, 12, 290, 46, "Settings", "settings")
    button(R, p, 0, -42, 290, 46, "Play the tutorial", "tutorial")
    button(R, p, 0, -96, 290, 46, "Credits", "credits")
    button(R, p, 0, -150, 290, 46, "Title screen", "title")
    ui.text(p, 0, -204, "Esc opens and closes this menu", "nunito_semi", 13, C.muted, gui.PIVOT_CENTER)
  end
  -- settings
  do
    local root, p = panel(R, 540, 520)
    R.screens.settings = root
    ui.text(p, 0, 226, "SETTINGS", "fredoka", 30, C.text, gui.PIVOT_CENTER)
    R.vol = {}
    local rows = { { "master", "Master volume" }, { "ambient", "Ambient (the world)" }, { "sfx", "Effects" }, { "ui", "Interface" } }
    for i, r in ipairs(rows) do
      local y = 170 - (i - 1) * 44
      ui.text(p, -240, y, r[2], "nunito_semi", 16, C.text)
      local m = button(R, p, 20, y, 36, 34, "-", "vol-" .. r[1])
      ui.box(p, 50, y, 150, 10, "pill_s", C.soft, gui.PIVOT_W)
      local bar = ui.box(p, 50, y, 150, 10, "pill_s", C.gold, gui.PIVOT_W)
      local pl = button(R, p, 226, y, 36, 34, "+", "vol+" .. r[1])
      R.vol[i] = { key = r[1], bar = bar, minus = m, plus = pl }
    end
    local function toggle_row(y, label, id)
      ui.text(p, -240, y, label, "nunito_semi", 16, C.text)
      return button(R, p, 160, y, 150, 36, "On", id, false, 16)
    end
    R.t_sound = toggle_row(-12, "World sound (M)", "t_sound")
    R.t_clouds = toggle_row(-56, "Clouds", "t_clouds")
    R.t_shadows = toggle_row(-100, "Shadows", "t_shadows")
    R.t_fs = toggle_row(-144, "Fullscreen", "t_fs")
    ui.text(p, -240, -188, "Tutorial", "nunito_semi", 16, C.text)
    R.t_tut = button(R, p, 160, -188, 150, 36, "Show again", "t_tut", false, 16)
    button(R, p, 0, -232, 200, 44, "Back", "back", true, 20)
  end
  -- credits
  do
    local root, p = panel(R, 600, 460)
    R.screens.credits = root
    ui.text(p, 0, 196, "CREDITS", "fredoka", 30, C.text, gui.PIVOT_CENTER)
    local L = {
      { "Tiny Consequences", "fredoka", 22, C.text },
      { "Made for the Defold Game Jam 2026 - theme: Chain Reaction", "nunito_semi", 15, C.muted },
      { "Direction and playtesting: Arjun", "nunito_semi", 16, C.text },
      { "Code, simulation and art: generated for this game with Claude (Anthropic)", "nunito_semi", 16, C.text },
      { "Engine: Defold", "nunito_semi", 16, C.text },
      { "Fonts: Fredoka and Nunito (SIL Open Font License 1.1)", "nunito_semi", 16, C.text },
      { "Character and farm-animal rigs: Quaternius (CC0)", "nunito_semi", 16, C.text },
      { "World sounds: generated for this game (ElevenLabs sound effects)", "nunito_semi", 16, C.text },
      { "Full list: assets/CREDITS.md", "nunito_semi", 13, C.muted },
    }
    for i, l in ipairs(L) do ui.text(p, 0, 150 - (i - 1) * 34, l[1], l[2], l[3], l[4], gui.PIVOT_CENTER) end
    button(R, p, 0, -186, 200, 44, "Back", "back", true, 20)
  end
  -- the guide
  do
    local GW, GH = 980, 600
    local root, p = panel(R, GW, GH)
    R.screens.guide = root
    R.g = { p = p }
    ui.text(p, -GW / 2 + 28, GH / 2 - 30, "HOW THE WORLD WORKS", "fredoka", 16, C.teal)
    R.g.title = ui.text(p, -GW / 2 + 28, GH / 2 - 64, "", "fredoka", 32, C.text)
    R.g.sub = ui.text(p, -GW / 2 + 30, GH / 2 - 98, "", "nunito_semi", 17, C.muted)
    -- the diagram: up to two rows of six boxes
    R.g.boxes, R.g.arrows = {}, {}
    for row = 1, 2 do
      for k = 1, 6 do
        local b = ui.box(p, 0, 0, 132, 92, "panel", C.soft)
        local ic = ui.icon(b, 0, 14, "info", 36, C.text)
        local t = ui.text(b, 0, -26, "", "nunito_semi", 13.5, C.text, gui.PIVOT_CENTER)
        R.g.boxes[#R.g.boxes + 1] = { node = b, icon = ic, text = t }
        if k < 6 then
          local a = ui.text(p, 0, 0, ">", "fredoka", 30, ARROW, gui.PIVOT_CENTER)
          R.g.arrows[#R.g.arrows + 1] = a
        end
      end
    end
    R.g.lines = {}
    for i = 1, 4 do
      local dot = ui.box(p, -GW / 2 + 40, 0, 9, 9, "circle", C.gold)
      local t = ui.text(p, -GW / 2 + 56, 0, "", "nunito_semi", 16, C.text)
      gui.set_line_break(t, true)
      gui.set_size(t, V3((GW - 100) / (16 / ui.FONT_SIZE.nunito_semi), 200, 0))
      gui.set_pivot(t, gui.PIVOT_NW)
      R.g.lines[i] = { dot = dot, text = t }
    end
    R.g.dots = {}
    for i = 1, #M.PAGES do
      R.g.dots[i] = ui.box(p, (i - (#M.PAGES + 1) / 2) * 20, -GH / 2 + 34, 11, 11, "circle", C.soft)
    end
    R.g.prev = button(R, p, -GW / 2 + 90, -GH / 2 + 34, 130, 40, "< Back", "g_prev", false, 17)
    R.g.next = button(R, p, GW / 2 - 90, -GH / 2 + 34, 130, 40, "Next >", "g_next", true, 17)
    button(R, p, GW / 2 - 40, GH / 2 - 34, 40, 40, "x", "back", false, 18)
    R.page = 1
  end
  for _, n in pairs(R.screens) do gui.set_enabled(n, false) end
  gui.set_enabled(R.dim, false)
  ui.top = false
  return R
end

-- Lay the guide's page out.
local function show_page(R, i)
  local P = M.PAGES[i]
  R.page = i
  local g = R.g
  gui.set_text(g.title, P.title)
  gui.set_text(g.sub, P.sub)
  -- split the diagram into rows at "|"
  local rows, cur = {}, {}
  for _, it in ipairs(P.diagram) do
    if it == "|" then rows[#rows + 1] = cur; cur = {} else cur[#cur + 1] = it end
  end
  rows[#rows + 1] = cur
  local bi, ai = 0, 0
  for _, b in ipairs(g.boxes) do gui.set_enabled(b.node, false) end
  for _, a in ipairs(g.arrows) do gui.set_enabled(a, false) end
  local y0 = 120
  for r, row in ipairs(rows) do
    local n = #row
    local step = 160
    local y = y0 - (r - 1) * 112
    for k, it in ipairs(row) do
      bi = bi + 1
      local b = g.boxes[bi]
      if b then
        local x = (k - (n + 1) / 2) * step
        gui.set_enabled(b.node, true)
        gui.set_position(b.node, V3(x, y, 0))
        ui.set_icon(b.icon, it[1])
        gui.set_text(b.text, it[2])
        local first = r == 1 and k == 1
        gui.set_color(b.node, first and vmath.vector4(1.0, 0.93, 0.76, 1) or C.soft)
        gui.set_color(b.icon, (it[1] == "bolt" or it[1] == "star" or it[1] == "meteor") and C.gold or
          ((it[1] == "fire" or it[1] == "skull") and C.coral or C.text))
        if k < n and not P.set then
          ai = ai + 1
          local a = g.arrows[ai]
          if a then gui.set_enabled(a, true); gui.set_position(a, V3(x + step / 2, y + 4, 0)) end
        end
      end
    end
    -- (a second row continues the first: an arrow down into it at its left)
    if r > 1 and not P.set then
      ai = ai + 1
      local a = g.arrows[ai]
      if a then
        gui.set_enabled(a, true)
        gui.set_position(a, V3((1 - (n + 1) / 2) * step - 92, y + 4, 0))
      end
    end
  end
  local ly = y0 - #rows * 112 + 30
  for k, l in ipairs(g.lines) do
    local txt = P.lines[k]
    gui.set_enabled(l.dot, txt ~= nil)
    gui.set_enabled(l.text, txt ~= nil)
    if txt then
      gui.set_text(l.text, txt)
      gui.set_position(l.text, V3(-980 / 2 + 56, ly + 8, 0))
      gui.set_position(l.dot, V3(-980 / 2 + 40, ly, 0))
      local nl = math.max(1, math.ceil(ui.text_width("nunito_semi", txt, 16) / 905))
      ly = ly - nl * 23 - 14
    end
  end
  for k, d in ipairs(g.dots) do gui.set_color(d, k == i and C.gold or C.soft) end
  gui.set_text(g.next.text, i < #M.PAGES and "Next >" or "Done")
  gui.set_enabled(g.prev.node, i > 1)
end

-- Open screen `name` (nil: close everything).
function M.open(self, name)
  local R = self.menu
  if not R then return end
  if name and (name == "settings" or name == "credits" or name == "guide") and M.screen ~= name then
    M.prev = (M.screen == "title" or M.screen == "pause") and M.screen or M.prev
  end
  M.screen = name
  for k, n in pairs(R.screens) do gui.set_enabled(n, k == name) end
  gui.set_enabled(R.dim, name ~= nil)
  if name == "guide" then show_page(R, R.page or 1) end
  local ok, newsfeed = pcall(require, "game.ui.newsfeed")
  if ok then newsfeed.menu_open = name ~= nil end
end

function M.is_open() return M.screen ~= nil end

local function fs_on()
  if not html5 then return false end
  local ok, r = pcall(html5.run, "(window.tcFS && window.tcFS.isOn()) ? '1' : '0'")
  return ok and r == "1"
end

function M.update(self, loaded, frac)
  local R = self.menu
  if not R or not M.screen then return end
  -- the title's Play waits for the world
  if M.screen == "title" then
    gui.set_color(R.play.node, loaded and (self.hover == R.play and vmath.vector4(0.98, 0.76, 0.30, 1) or C.gold) or C.soft)
    gui.set_color(R.play.text, loaded and C.white or C.muted)
    gui.set_text(R.load_t, loaded and "" or string.format("Growing a tiny world... %d%%", math.floor((frac or 0) * 100)))
  end
  for _, b in ipairs(R.btns) do
    if b ~= R.play then
      local hov = self.hover == b
      gui.set_color(b.node, b.primary and (hov and vmath.vector4(0.98, 0.76, 0.30, 1) or C.gold) or (hov and C.hover or C.soft))
    end
  end
  if M.screen == "settings" then
    local audio = require("game.audio.audio")
    for _, v in ipairs(R.vol) do gui.set_size(v.bar, V3(math.max(4, 150 * (audio.GROUPS[v.key] or 0)), 10, 0)) end
    gui.set_text(R.t_sound.text, audio.ON and "On" or "Off")
    gui.set_text(R.t_clouds.text, M.prefs.clouds and "On" or "Off")
    gui.set_text(R.t_shadows.text, M.prefs.shadows and "On" or "Off")
    R.fs_t = (R.fs_t or 0) + 1
    if R.fs_t % 15 == 1 then R.fs = fs_on() end
    gui.set_text(R.t_fs.text, R.fs and "On" or "Off")
    gui.set_text(R.t_tut.text, "Play it now")
  end
end

function M.hit(self, x, y)
  local R = self.menu
  if not R or not M.screen then return nil end
  local scr = R.screens[M.screen]
  for _, b in ipairs(R.btns) do
    if gui.is_enabled(b.node) and gui.pick_node(b.node, x, y) then
      -- (only the buttons of the screen that is showing)
      local n = b.node
      local top = n
      while gui.get_parent(top) do top = gui.get_parent(top) end
      if top == scr then return b end
    end
  end
  return "panel"   -- (the backdrop swallows every click while a menu is open)
end

-- Apply the graphics preferences (clouds, shadows) to the view.
function M.apply_gfx()
  local state = require("game.view.state")
  local clouds = require("game.view.clouds")
  clouds.hidden = not M.prefs.clouds
  state.shadow_on = M.prefs.shadows and true or false
end

function M.activate(self, h)
  if type(h) ~= "table" or not h.menu_btn then return false end
  local id = h.menu_btn
  local R = self.menu
  do local ok, audio = pcall(require, "game.audio.audio"); if ok and audio.ui then audio.ui("ui_click") end end
  if id == "play" then
    if not R.loaded then return true end
    M.want = "play"
  elseif id == "resume" then M.want = "resume"
  elseif id == "tutorial" then M.want = "tutorial"
  elseif id == "title" then M.open(self, "title")
  elseif id == "guide" then R.page = 1; M.open(self, "guide")
  elseif id == "settings" then M.open(self, "settings")
  elseif id == "credits" then M.open(self, "credits")
  elseif id == "back" then M.open(self, M.prev or "title")
  elseif id == "g_prev" then if R.page > 1 then show_page(R, R.page - 1) end
  elseif id == "g_next" then
    if R.page < #M.PAGES then show_page(R, R.page + 1) else M.open(self, M.prev or "title") end
  elseif id:sub(1, 4) == "vol-" or id:sub(1, 4) == "vol+" then
    local audio = require("game.audio.audio")
    local k = id:sub(5)
    local d = id:sub(4, 4) == "+" and 0.1 or -0.1
    local v = math.max(0, math.min(1, (audio.GROUPS[k] or 0) + d))
    audio.GROUPS[k] = math.floor(v * 10 + 0.5) / 10
    pcall(sound.set_group_gain, hash(k), audio.GROUPS[k])
    local sp = require("game.ui.sound_panel")
    if sp.store then sp.store() end
  elseif id == "t_sound" then
    local audio = require("game.audio.audio")
    audio.ON = not audio.ON
    local sp = require("game.ui.sound_panel")
    if sp.store then sp.store() end
  elseif id == "t_clouds" then M.prefs.clouds = not M.prefs.clouds; M.apply_gfx(); M.save_prefs()
  elseif id == "t_shadows" then M.prefs.shadows = not M.prefs.shadows; M.apply_gfx(); M.save_prefs()
  elseif id == "t_fs" then
    if html5 then pcall(html5.run, "window.tcFS && window.tcFS.toggle()") end
    R.fs_t = 0
  elseif id == "t_tut" then M.want = "tutorial"
  end
  return true
end

return M
