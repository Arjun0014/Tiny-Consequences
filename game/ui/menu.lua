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

-- The guide: each page a title, a diagram (rows of { icon, label }, joined by arrows when it is a chain - `set` pages are a
-- set of things without arrows; "|" starts a second row, at most six to a row) and a few lines.
M.PAGES = {
  { title = "The living planet", sub = "Sun, air, water and ground - day by day, on every cell of the planet",
    diagram = { { "sun", "Sun & season" }, { "thermo", "Temperature" }, { "cloud", "Wind & rain" }, { "drop", "Soil water" },
      { "wave", "Rivers" }, "|", { "rain", "Floods" }, { "drought", "Drought" }, { "fire", "Wildfire" }, { "snow", "Snow & ice" } },
    lines = { "The sun's height makes the seasons; winds carry the rain, and the rain soaks the soil or runs off into the rivers.",
      "Too much rain floods the valleys; too little dries the ground until lightning can set it alight. Cold brings snow and frost.",
      "The sea warms and cools slower than the land, and its currents carry whatever floats on it.",
      "Every village's weather is its own cell's weather - drawn as the clouds over it." } },
  { title = "Ecology", sub = "Plants, grazers, hunters and the sea - each living off the next",
    diagram = { { "leaf", "Grass & forest" }, { "deer", "Grazers" }, { "wolf", "Wolves" }, { "tree", "Regrowth" }, "|",
      { "drop", "Plankton" }, { "fish", "Fish" }, { "boat", "Fishers" } },
    lines = { "Forests and grass grow by sun, warmth and water; grazers eat them; wolves hunt the grazers. Each answers the others.",
      "Near the villages every deer, wolf, sheep, cow, pig and dog is one animal with its own place, pace and fear.",
      "Fish live off plankton; warm water, oil and nets thin them. Burnt or felled land grows back over the seasons." } },
  { title = "People and families", sub = "Every villager is a person who lives a whole life", set = true,
    diagram = { { "person", "A person" }, { "heart", "Family" }, { "people", "Children" }, { "hourglass", "Ageing" }, "|",
      { "bread", "Hunger" }, { "thermo", "Health" }, { "bubble", "Memory" }, { "house", "Home" } },
    lines = { "Each person is born, grows up, marries, raises children, grows old and dies - nobody is a statistic.",
      "They are fed or hungry, well, hurt or sick, and they remember what happened to them and who did it.",
      "Each chooses what to do next for reasons of their own: click anyone to read their reasons, family and memories." } },
  { title = "Settlements and work", sub = "Work makes food, goods and buildings - and trade moves them",
    diagram = { { "wheat", "Fields" }, { "tree", "Wood" }, { "paw", "Herds" }, { "fish", "Fishing" }, "|",
      { "pickaxe", "Stone, iron, salt" }, { "house", "Building" }, { "ship", "Boats & trade" }, { "star", "Fairs & credit" } },
    set = true,
    lines = { "Households sow and reap, cut wood, keep sheep, goats, cattle and pigs, fish, weave, quarry, mine and smelt.",
      "The granary and each larder hold what they have; what they lack they trade for, on foot or by boat.",
      "Households give, lend, owe and repay. Houses, pens, wells and docks are built log by log." } },
  { title = "Society", sub = "Kin, help, quarrels and leaving", set = true,
    diagram = { { "heart", "Kin & marriage" }, { "people", "Help" }, { "settlers", "Migration" }, { "pin", "New places" }, "|",
      { "skull", "Theft & grudges" }, { "wolf", "Raids" }, { "house", "Defence" } },
    lines = { "Kin help kin; neighbours lodge those who lost their home; food is carried to the hungry.",
      "Households that cannot live where they are leave - and some found new places, by land or by sea.",
      "Harm is remembered and answered - sometimes with a raid; a village that is harmed may build a palisade." } },
  { title = "Disasters", sub = "The planet moves on its own - people live through it", set = true,
    diagram = { { "fire", "Fire" }, { "rain", "Flood" }, { "snow", "Snow" }, { "pickaxe", "Quake" }, "|",
      { "volcano", "Volcano" }, { "wave", "Tsunami" }, { "meteor", "Meteor" } },
    lines = { "Fire spreads by dryness and wind, burns its fuel and goes out: trees become snags, houses ruins.",
      "Rivers rise over their banks; quakes bring roofs down on whoever is inside; the sea draws back before a wave.",
      "People notice danger by sight and sound, run, carry their babies, fight fires, dig out the trapped, tend the hurt." } },
  { title = "God powers", sub = "Powers do not command outcomes. They change the physical world.", set = true,
    diagram = { { "bolt", "Lightning" }, { "rain", "Rain" }, { "drought", "Dry spell" }, { "heat", "Warm / cold" }, { "volcano", "Stir" }, "|",
      { "skull", "Cull" }, { "paw", "Bring" }, { "blight", "Blight" }, { "oil", "Oil" }, { "meteor", "Meteor" } },
    lines = { "Rain does not 'add food': it adds water. What the water does next - crops, floods, fires put out - is the world's.",
      "Lightning is a bolt at a point; a meteor a crater, a blast and a flash; oil a slick the currents carry.",
      "Each power recharges in game time (the meteor takes seven days): watch what one did before the next." } },
  { title = "Chain reactions", sub = "One touch - and everything after it belongs to the simulation",
    diagram = { { "star", "Your action" }, { "bolt", "Physical change" }, { "people", "People & beasts react" }, "|",
      { "wheat", "Food, trade, ecology" }, { "hourglass", "Later consequences" } },
    lines = { "NEWS: what is happening now. Click a story to fly to the very place.",
      "WHY? (the badge on a story): its causes, back to NATURE, PEOPLE or YOU.",
      "YOUR ACTIONS: everything each of your casts has caused, as a growing tree of real links. Skip time to watch it grow." } },
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
    local root, p = panel(R, 600, 470)
    R.screens.credits = root
    ui.text(p, 0, 200, "CREDITS", "fredoka", 30, C.text, gui.PIVOT_CENTER)
    local L = {
      { "Tiny Consequences", "fredoka", 24, C.text },
      { "a tiny planet that lives without you", "nunito_semi", 15, C.muted },
      { "", "nunito_semi", 8, C.text },
      { "Direction and playtesting  -  Arjun", "nunito_semi", 16, C.text },
      { "Made with the Defold engine", "nunito_semi", 16, C.text },
      { "Fredoka and Nunito typefaces (SIL Open Font License)", "nunito_semi", 16, C.text },
      { "Villager and farm-animal rigs by Quaternius (CC0)", "nunito_semi", 16, C.text },
      { "", "nunito_semi", 8, C.text },
      { "Made for the Defold Game Jam 2026 - theme: Chain Reaction", "nunito_semi", 14, C.muted },
    }
    local y = 150
    for _, l in ipairs(L) do ui.text(p, 0, y, l[1], l[2], l[3], l[4], gui.PIVOT_CENTER); y = y - (l[3] < 10 and 12 or 32) end
    button(R, p, 0, -190, 200, 44, "Back", "back", true, 20)
  end
  -- the guide: a header, the diagram, the lines, and a footer (Back, the pages, Next)
  do
    local GW, GH = 1000, 620
    M.GW, M.GH = GW, GH
    local root, p = panel(R, GW, GH)
    R.screens.guide = root
    R.g = { p = p }
    ui.text(p, -GW / 2 + 32, GH / 2 - 32, "HOW THE WORLD WORKS", "fredoka", 15, C.teal)
    R.g.title = ui.text(p, -GW / 2 + 32, GH / 2 - 66, "", "fredoka", 32, C.text)
    R.g.sub = ui.text(p, -GW / 2 + 33, GH / 2 - 100, "", "nunito_semi", 17, C.muted)
    ui.box(p, 0, GH / 2 - 122, GW - 56, 2, "px", C.soft)
    R.g.boxes, R.g.arrows = {}, {}
    for row = 1, 2 do
      for k = 1, 6 do
        local b = ui.box(p, 0, 0, 136, 86, "panel", C.soft)
        local ic = ui.icon(b, 0, 13, "info", 34, C.text)
        local t = ui.text(b, 0, -25, "", "nunito_semi", 13, C.text, gui.PIVOT_CENTER)
        R.g.boxes[#R.g.boxes + 1] = { node = b, icon = ic, text = t }
      end
      for k = 1, 6 do
        R.g.arrows[#R.g.arrows + 1] = ui.icon(p, 0, 0, "chev_right", 22, ARROW)
      end
    end
    R.g.lines = {}
    for i = 1, 5 do
      local dot = ui.box(p, 0, 0, 9, 9, "circle", C.gold)
      local t = ui.text(p, 0, 0, "", "nunito_semi", 16, C.text)
      gui.set_pivot(t, gui.PIVOT_NW)
      R.g.lines[i] = { dot = dot, text = t }
    end
    -- the footer: a rule, Back on the left, the page count and the dots (clickable) in the middle, Next on the right
    ui.box(p, 0, -GH / 2 + 66, GW - 56, 2, "px", C.soft)
    R.g.count = ui.text(p, 0, -GH / 2 + 46, "", "nunito_semi", 13, C.muted, gui.PIVOT_CENTER)
    R.g.dots = {}
    for i = 1, #M.PAGES do
      local d = ui.box(p, (i - (#M.PAGES + 1) / 2) * 22, -GH / 2 + 24, 12, 12, "circle", C.soft)
      R.g.dots[i] = d
      R.btns[#R.btns + 1] = { node = d, text = nil, menu_btn = "g_dot", page = i, dot = true }
    end
    R.g.prev = button(R, p, -GW / 2 + 100, -GH / 2 + 34, 150, 42, "Back", "g_prev", false, 17)
    R.g.next = button(R, p, GW / 2 - 100, -GH / 2 + 34, 150, 42, "Next", "g_next", true, 17)
    R.g.prev_ic = ui.icon(R.g.prev.node, -50, 0, "chev_left", 16, C.text)
    R.g.next_ic = ui.icon(R.g.next.node, 50, 0, "chev_right", 16, C.white)
    ui.text(p, GW / 2 - 200, -GH / 2 + 34, "or the arrow keys", "nunito_semi", 12, C.muted, gui.PIVOT_E)
    button(R, p, GW / 2 - 40, GH / 2 - 36, 42, 42, "x", "back", false, 18)
    R.page = 1
  end
  for _, n in pairs(R.screens) do gui.set_enabled(n, false) end
  gui.set_enabled(R.dim, false)
  ui.top = false
  return R
end

-- Words wrapped to `w` pixels at `px` (measured with the font itself): the text with its line breaks, and the line count.
local function wrap(font, txt, px, w)
  local out, line, n = {}, "", 0
  for word in txt:gmatch("%S+") do
    local try = line == "" and word or (line .. " " .. word)
    if line ~= "" and ui.text_width(font, try, px) > w then
      out[#out + 1] = line
      line = word
    else
      line = try
    end
  end
  if line ~= "" then out[#out + 1] = line end
  return table.concat(out, "\n"), #out
end

-- Lay the guide's page out: the diagram's rows centred, each box joined to the next by an arrow on a chain page; the
-- lines wrapped by measure below it, smaller when a page has more to say than the room there is (never under the footer).
local function show_page(R, i)
  local P = M.PAGES[i]
  R.page = i
  local g = R.g
  local GW, GH = M.GW, M.GH
  gui.set_text(g.title, string.format("%d  %s", i, P.title))
  gui.set_text(g.sub, P.sub)
  local rows, cur = {}, {}
  for _, it in ipairs(P.diagram) do
    if it == "|" then rows[#rows + 1] = cur; cur = {} else cur[#cur + 1] = it end
  end
  rows[#rows + 1] = cur
  for _, b in ipairs(g.boxes) do gui.set_enabled(b.node, false) end
  for _, a in ipairs(g.arrows) do gui.set_enabled(a, false) end
  local step, y0, bi, ai = 162, GH / 2 - 180, 0, 0
  for r = 1, math.min(2, #rows) do
    local row = rows[r]
    local n = math.min(6, #row)
    local y = y0 - (r - 1) * 104
    for k = 1, n do
      local it = row[k]
      bi = bi + 1
      local b = g.boxes[bi]
      local x = (k - (n + 1) / 2) * step
      gui.set_enabled(b.node, true)
      gui.set_position(b.node, V3(x, y, 0))
      ui.set_icon(b.icon, it[1])
      gui.set_text(b.text, it[2])
      local s = math.min(13, 13 * 124 / math.max(1, ui.text_width("nunito_semi", it[2], 13)))
      gui.set_scale(b.text, V3(s / ui.FONT_SIZE.nunito_semi, s / ui.FONT_SIZE.nunito_semi, 1))
      gui.set_color(b.node, (r == 1 and k == 1 and not P.set) and vmath.vector4(1.0, 0.93, 0.76, 1) or C.soft)
      gui.set_color(b.icon, (it[1] == "bolt" or it[1] == "star" or it[1] == "meteor") and C.gold or
        ((it[1] == "fire" or it[1] == "skull") and C.coral or C.text))
      if k < n and not P.set then
        ai = ai + 1
        local a = g.arrows[ai]
        gui.set_enabled(a, true)
        gui.set_position(a, V3(x + step / 2, y, 0))
        gui.set_rotation(a, vmath.quat())
      end
    end
    -- (a chain that runs on into its second row: an arrow down beside the row's first box)
    if r > 1 and not P.set then
      ai = ai + 1
      local a = g.arrows[ai]
      gui.set_enabled(a, true)
      gui.set_position(a, V3((1 - (n + 1) / 2) * step - 94, y, 0))
    end
  end
  -- the lines: from under the diagram to above the footer
  local top = y0 - (math.min(2, #rows) - 1) * 104 - 70
  local bottom = -GH / 2 + 80
  local tw = GW - 120
  local px, wrapped, total
  for _, size in ipairs({ 16, 15, 14, 13 }) do
    px, wrapped, total = size, {}, 0
    for k, txt in ipairs(P.lines) do
      local t, nl = wrap("nunito_semi", txt, size, tw)
      wrapped[k] = { t, nl }
      total = total + nl * size * 1.42 + 12
    end
    if top - total >= bottom then break end
  end
  local y = top
  local sc = px / ui.FONT_SIZE.nunito_semi
  for k, l in ipairs(g.lines) do
    local w = wrapped[k]
    gui.set_enabled(l.dot, w ~= nil)
    gui.set_enabled(l.text, w ~= nil)
    if w then
      gui.set_text(l.text, w[1])
      gui.set_scale(l.text, V3(sc, sc, 1))
      gui.set_position(l.text, V3(-GW / 2 + 60, y + px * 0.62, 0))
      gui.set_position(l.dot, V3(-GW / 2 + 44, y, 0))
      y = y - w[2] * px * 1.42 - 12
    end
  end
  for k, d in ipairs(g.dots) do
    gui.set_color(d, k == i and C.gold or C.soft)
    gui.set_size(d, k == i and V3(16, 16, 0) or V3(11, 11, 0))
  end
  gui.set_text(g.count, string.format("%d of %d", i, #M.PAGES))
  gui.set_text(g.next.text, i < #M.PAGES and "Next" or "Done")
  gui.set_enabled(g.next_ic, i < #M.PAGES)
  gui.set_enabled(g.prev.node, i > 1)
end

-- (S25) the guide's page by the arrow keys
function M.page_step(self, d)
  local R = self.menu
  if not R or M.screen ~= "guide" then return end
  local i = (R.page or 1) + d
  if i >= 1 and i <= #M.PAGES then show_page(R, i) end
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
    if b ~= R.play and not b.dot then
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
  elseif id == "g_dot" then show_page(R, h.page)
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
