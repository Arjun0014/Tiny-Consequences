-- The opening tutorial (S25, D-211, rebuilt as D-215): teaching by doing, and showing what a chain reaction is. A coach
-- card at the top walks the player from the whole planet down to a village (look, inspect someone, the speeds of time),
-- then to ONE intervention that matters - a meteor at the edge of the woods beside the village - and stays with what
-- follows: the strike read from the world (the dead, the fallen houses, the fires, the people running), the news that
-- noticed it, "Your actions" (the cast's real chain), a day skipped (the planet turning under the camera), the chain
-- grown, a season skipped, and the chain after it. Every number and every branch it shows is read from the world
-- (sim/chain.lua, the impact's deed, the village) - nothing is scripted; the same strike elsewhere makes another chain.
-- Its only starting condition is physical and logged: the ground about the village dry (sim/tutorial3.lua). Each step
-- waits for the player's own action, read from the game's state. Skippable at every step; replayable from the menus.
-- Call only from GUI script callbacks.
local ui = require("game.ui.uikit")

local M = {}
local C = ui.C
local V3 = vmath.vector3

M.on = false
M.step = 0
M.CW, M.CH = 540, 150
M.CX = 668   -- (clear of the news column and of "Your actions" beside it)

local function now_h(W) return require("game.platform.speed").v3_t or W.hour end
local function fmt_n(n, one, many) return n == 1 and ("1 " .. one) or (n .. " " .. (many or (one .. "s"))) end

-- Words wrapped to `w` pixels at `px` (measured with the font): the text with its breaks, and the line count.
local function wrap(font, txt, px, w)
  local out = {}
  for para in (txt .. "\n"):gmatch("(.-)\n") do
    local line = ""
    for word in para:gmatch("%S+") do
      local try = line == "" and word or (line .. " " .. word)
      if line ~= "" and ui.text_width(font, try, px) > w then
        out[#out + 1] = line
        line = word
      else
        line = try
      end
    end
    out[#out + 1] = line
  end
  return table.concat(out, "\n"), #out
end

function M.build(self)
  ui.top = true
  local T = {}
  self.tut = T
  M.T = T   -- (probes read it)
  T.rect = ui.box(nil, 0, 0, 80, 80, "pill", vmath.vector4(0.949, 0.698, 0.2, 0.35))
  T.ring = ui.box(nil, 0, 0, 80, 80, "ring", C.gold)
  T.ring2 = ui.box(nil, 0, 0, 60, 60, "ring", vmath.vector4(1, 1, 1, 0.9))
  T.tag = ui.box(nil, 0, 0, 120, 26, "pill", C.gold)
  T.tag_t = ui.text(T.tag, 0, 0, "", "fredoka", 14, C.white, gui.PIVOT_CENTER)
  local root = ui.box(nil, M.CX, 720 - 92 - M.CH / 2, 1, 1, "px", C.clear)
  ui.anchor(root, gui.ANCHOR_NONE, gui.ANCHOR_TOP)
  T.root = root
  T.shadow = ui.box(root, 0, -4, M.CW + 24, M.CH + 24, "shadow", C.shadow)
  T.card = ui.box(root, 0, 0, M.CW, M.CH, "panel", C.panel_solid)
  T.stripe = ui.box(T.card, -M.CW / 2 + 4, 0, 6, M.CH - 18, "pill_s", C.gold)
  T.count = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 18, "", "nunito_semi", 12, C.muted)
  T.title = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 42, "", "fredoka", 21, C.text)
  T.body = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 60, "", "nunito_semi", 14.5, C.text)
  gui.set_pivot(T.body, gui.PIVOT_NW)
  T.next = ui.box(T.card, M.CW / 2 - 66, -M.CH / 2 + 22, 110, 30, "pill", C.gold)
  T.next_t = ui.text(T.next, 0, 0, "Next", "fredoka", 15, C.white, gui.PIVOT_CENTER)
  T.next_hit = { tut = "next" }
  T.skip = ui.box(T.card, M.CW / 2 - 186, -M.CH / 2 + 22, 116, 30, "pill", C.soft)
  ui.text(T.skip, 0, 0, "Skip tutorial", "fredoka", 13, C.muted, gui.PIVOT_CENTER)
  T.skip_hit = { tut = "skip" }
  T.go = ui.box(T.card, M.CW / 2 - 326, -M.CH / 2 + 22, 140, 30, "pill", C.teal)
  T.go_t = ui.text(T.go, 0, 0, "Take me there", "fredoka", 13, C.white, gui.PIVOT_CENTER)
  T.go_hit = { tut = "go" }
  for _, n in ipairs({ T.root, T.ring, T.ring2, T.tag, T.rect }) do gui.set_enabled(n, false) end
  ui.top = false
  return T
end

-- The village of the lesson (the first that stands) and the place to strike: the edge of the woods beside it - the
-- point among six about the square with the most trees near it, on land and on the map (reads the world only).
local function pick_village(W)
  for _, s in ipairs(W.S.v3.settlements) do
    if s.alive and s.land and s.trees and not s.founding then return s end
  end
  return nil
end

local function pick_target(W, s)
  local mapm = require("sim.agents.map")
  local blaze = require("sim.agents.blaze")
  local best, bn = nil, -1
  for _, o in ipairs({ { 0.6, 0.4 }, { -0.6, 0.4 }, { 0.6, -0.4 }, { -0.6, -0.4 }, { 0.7, 0 }, { -0.7, 0 } }) do
    local x, y = (s.sqx or 0) + o[1], (s.sqy or 0) + o[2]
    local i = mapm.tile_of(s, x, y)
    if i > 0 and s.land[i] == 1 then
      local n = 0
      blaze.trees_near(s, x, y, 0.3, function() n = n + 1 end)
      if n > bn then best, bn = { x = x, y = y }, n end
    end
  end
  return best
end

-- The chain of the tutorial's cast, brought up to now.
local function chain_of(W, T)
  if not T.ev then return nil end
  local chain = require("sim.chain")
  chain.update(W, now_h(W))
  return chain.get(W, T.ev)
end

-- Its real branches, the human cost first: the chain's nodes counted by kind (people dead, buildings fallen, households
-- without a roof, hunger, children taken in, help...), then what it changed in the land and the sky - at most n lines.
local SUMMARY = {
  { "death", "a person died", "%d people died" }, { "wound", "a person hurt", "%d people hurt" },
  { "fell", "a building fell in", "%d buildings fell in" }, { "lodging", "a household lost its roof", "%d households lost their roofs" },
  { "hunger", "hunger in a household", "hunger in %d households" }, { "takenin", "a child taken in by others", "%d children taken in by others" },
  { "help", "kin or neighbours came to help", "kin or neighbours came to help %d times" }, { "slain", "beasts killed", "beasts killed in %d herds" },
  { "lost", "beasts lost", "beasts lost %d times" }, { "fire", "fire in the woods", "%d fires" }, { "quake", "the ground shook", "the ground shook %d times" },
  { "dust", "dust in the sky over the whole world", "dust in the sky over the whole world" }, { "river", "the river changed", "the rivers changed" },
  { "flood", "a flood", "%d floods" }, { "soil", "the soil changed", "the soil changed" },
}
local function branches(W, T, n)
  local I = chain_of(W, T)
  if not I then return {}, 0 end
  local rows = require("sim.chain").rows(W, I, 400)
  local by = {}
  for _, r in ipairs(rows) do local k = r.node.kind; by[k] = (by[k] or 0) + 1 end
  local out = {}
  for _, e in ipairs(SUMMARY) do
    local c = by[e[1]]
    if c and #out < n then out[#out + 1] = c == 1 and e[2] or string.format(e[3], c) end
  end
  return out, math.max(0, I.n_total - #rows)
end

-- What the strike did, from its own deed (sim/agents/impact.lua): the dead, the hurt, what fell, what burns.
local function impact_of(W, T)
  local D = W.S.v3.deeds
  for k = #D, math.max(1, #D - 400), -1 do
    local d = D[k]
    if d.kind == "impact" and d.t >= (T.cast_t or 0) - 0.02 then return d end
  end
  return nil
end

local function fleeing(W, T)
  local n = 0
  for _, id in ipairs(T.s and T.s.members or {}) do
    local p = W.S.v3.people[id]
    if p and p.alive and (p.objk == "flee_impact" or p.objk == "flee_fire" or p.objk == "fight_fire") then n = n + 1 end
  end
  return n
end

local STEPS = {
  { id = "intro", title = "This tiny world lives without you",
    text = "Everyone on this planet is a person with a family, a home, work and reasons of their own. The animals, the weather, the rivers and the sea all run by themselves - whether you watch or not.\nYou are a god who can touch it. Then you watch what that touch sets off.",
    next = true },
  { id = "look", title = "Fly down to a village",
    text = "Drag to turn the planet; scroll (or pinch) to zoom. Fly down until you can see the people of the village - or press 'Take me there'.",
    go = true,
    done = function(T, W)
      local cam = require("game.view.camera")
      return cam.zoom >= 1.05 and T.s ~= nil and require("game.view.agents_view").nearest(W) == T.s
    end },
  { id = "inspect", title = "Meet someone",
    text = "Click a person. The panel on the right shows who they are - their family, what they are doing and why.",
    done = function(T, W)
      local sel = require("game.ui.selection").sel
      return sel ~= nil and sel.kind ~= "planet"
    end },
  { id = "speed", title = "Time is yours",
    text = "The buttons by the clock set how fast the world runs: Pause (Space), Watch, Brisk and Fast. Press Fast now and watch the village go about its day.",
    hl = "speed",
    done = function(T, W) return require("game.platform.speed").get() >= 3 end },
  { id = "meteor", title = "Now touch the world",
    text = "Choose the Meteor - the last power on the bar. A god's power changes the physical world; what happens next is the world's.",
    hl = "meteor",
    done = function(T, W)
      local powers = require("game.ui.powers")
      local d = powers.armed and powers.DEFS[powers.armed]
      return d and d.id == "meteor"
    end },
  { id = "strike", title = "Strike the woods beside the village",
    text = "Click inside the ring - the edge of the woods beside the village. The ground here is dry after a rainless spell.",
    hl = "target", go = true,
    done = function(T, W)
      local powers = require("game.ui.powers")
      if powers.casts ~= T.cast_n and powers.last and powers.last.id == "meteor" then
        T.cast_n = powers.casts
        T.cast_t = now_h(W)
        return true
      end
      return false
    end },
  { id = "impact", title = "Watch",
    text = function(T, W)
      local d = impact_of(W, T)
      if not d then return "The rock is coming down..." end
      local fl = fleeing(W, T)
      return string.format("The rock killed %s, threw down %s and %s, set %s alight and laid %s flat. %s running from the blast or fighting the fires - each by their own choice.",
        fmt_n(d.dead or 0, "person", "people"), fmt_n(d.houses or 0, "house"), fmt_n(math.max(0, (d.fell or 0) - (d.houses or 0)), "other building"),
        fmt_n(d.lit or 0, "thing", "things"), fmt_n(d.flat or 0, "tree"), fl == 1 and "1 person is" or (fl .. " people are"))
    end,
    next = true, wait = 8 },
  { id = "news", title = "The world noticed",
    text = function(T, W)
      return "Every story in the WORLD NEWS (left) rests on what really happened. Click the one marked YOU - the camera flies to the very place."
        .. (T.wrong and " (That one was another story - the world has its own. Click the one marked YOU.)" or "")
    end,
    hl = "news",
    done = function(T, W)
      local lg = require("game.ui.newsfeed").last_go
      if not (lg and T.ev) then return false end
      if require("sim.news").chain(W, lg.st).root_ev == T.ev then return true end
      if T.wrong_go ~= lg then T.wrong_go = lg; T.wrong = true end
      return false
    end },
  { id = "actions", title = "Your actions",
    text = function(T, W)
      local I = chain_of(W, T)
      return string.format("You caused ONE thing. Everything after it belongs to the simulation. Open YOUR ACTIONS (bottom left): your meteor has already set off %s - each one a real link you can click.",
        fmt_n(I and I.n_total or 0, "consequence"))
    end,
    hl = "actions",
    done = function(T, W, self) local A = self.actions; return A and A.open and A.mode == "tree" end },
  { id = "skip_day", title = "Let a day pass",
    text = "Chains take time. Open the SKIP menu (the last button by the clock) and choose 'One day'. The camera rises and the planet turns through the day while the world lives it.",
    hl = "skip", want = 3,
    done = function(T, W) return T.skips.short or T.skips.season end },
  { id = "grown", title = "The chain grew",
    text = function(T, W)
      local I = chain_of(W, T)
      local b = branches(W, T, 6)
      return string.format("A day on, your meteor has %s:\n- %s\nEvery one is in Your actions - click a line to fly there.",
        fmt_n(I and I.n_total or 0, "consequence"), table.concat(b, "\n- "))
    end,
    next = true },
  { id = "skip_season", title = "Now a whole season",
    text = "Skip again: 'Next season' - twelve days. Survivors bury their dead, lodge the homeless, rebuild, run short of food, ask their kin for help... or not: the world decides.",
    hl = "skip", want = 4,
    done = function(T, W) return T.skips.season end },
  { id = "final", title = "One touch, a world of consequences",
    text = function(T, W)
      local I = chain_of(W, T)
      local b = branches(W, T, 7)
      return string.format("A season after your meteor: %s.\n- %s\nThe same strike in another place, at another time, makes another chain. The world is yours - watch it, touch it, follow what it does.",
        fmt_n(I and I.n_total or 0, "consequence"), table.concat(b, "\n- "))
    end,
    next = true, final = true },
}
M.STEPS = STEPS

-- Start (or restart) the tutorial.
function M.start(self, W)
  local T = self.tut
  if not T or not W or not W.v3 then return end
  M.on, M.step = true, 1
  T.s = pick_village(W)
  T.target = T.s and pick_target(W, T.s)
  T.ev, T.cast_n, T.cast_t, T.skips, T.wrong, T.wrong_go = nil, require("game.ui.powers").casts, nil, {}, nil, nil
  T.t, T.clock, T.h = 0, 0, nil
  -- (the interventions there before: the lesson's own cast is the first meteor not among them)
  T.known = {}
  local chain = require("sim.chain")
  chain.update(W, now_h(W))
  for _, I in ipairs(chain.list(W)) do T.known[I.ev] = true end
  if T.s then
    require("sim.world").command(W, "tutorial_dry", { sid = T.s.id, sw = 0.2 })
    -- (a lesson in the dark shows nothing: if it is late in the village's day, the night passes while the intro is read -
    -- the planet turning under the camera)
    local lh = require("sim.agents.clock").local_hour(now_h(W), W.grid.lon[T.s.cell])
    if lh >= 13 or lh < 5 then require("game.platform.speed").v3_skip(W, "dawn") end
  end
  gui.set_enabled(T.root, true)
  M.enter(self, W)
end

function M.stop(self, done)
  local T = self.tut
  M.on = false
  if T then for _, n in ipairs({ T.root, T.ring, T.ring2, T.tag, T.rect }) do gui.set_enabled(n, false) end end
  local menu = require("game.ui.menu")
  menu.prefs.tutorial_done = true
  menu.save_prefs()
end

local function fly_to_target(T)
  if not (T.s and T.target) then return end
  local ux, uy, uz = require("sim.agents.map").unit(T.s, T.target.x, T.target.y)
  require("game.view.camera").fly_to_dir(ux, uy, uz, 0.95)
end

function M.enter(self, W)
  local T = self.tut
  local S = STEPS[M.step]
  if not S then M.stop(self, true); return end
  T.t = 0
  local powers = require("game.ui.powers")
  -- (in the lesson the meteor is ready whenever it is asked for)
  if S.id == "meteor" or S.id == "strike" then powers.ready.meteor = nil end
  if S.id == "strike" then fly_to_target(T) end
  if S.id == "impact" then
    powers.disarm()
    local speed = require("game.platform.speed")
    if speed.get() > 1 then speed.set(1) end
  end
  if S.id == "news" then require("game.ui.newsfeed").last_go = nil end
end

local function layout(self, W, S)
  local T = self.tut
  local txt = type(S.text) == "function" and S.text(T, W) or S.text
  gui.set_text(T.count, M.step == 1 and "TUTORIAL" or string.format("TUTORIAL  -  step %d of %d", M.step - 1, #STEPS - 1))
  gui.set_text(T.title, S.title)
  local body, lines = wrap("nunito_semi", txt, 14.5, M.CW - 44)
  gui.set_text(T.body, body)
  local h = math.max(150, 100 + lines * 20)
  if h ~= T.h then
    T.h = h
    gui.set_size(T.card, V3(M.CW, h, 0))
    gui.set_size(T.shadow, V3(M.CW + 24, h + 24, 0))
    gui.set_position(T.root, V3(M.CX, 720 - 92 - h / 2, 0))
    gui.set_position(T.count, V3(-M.CW / 2 + 20, h / 2 - 18, 0))
    gui.set_position(T.title, V3(-M.CW / 2 + 20, h / 2 - 42, 0))
    gui.set_position(T.body, V3(-M.CW / 2 + 20, h / 2 - 60, 0))
    gui.set_size(T.stripe, V3(6, h - 18, 0))
    for _, nd in ipairs({ T.next, T.skip, T.go }) do
      local p = gui.get_position(nd)
      gui.set_position(nd, V3(p.x, -h / 2 + 22, 0))
    end
  end
  local wait_left = S.wait and math.max(0, S.wait - T.t) or 0
  gui.set_enabled(T.next, S.next and true or false)
  gui.set_color(T.next, (S.next and wait_left <= 0) and (self.hover == T.next_hit and vmath.vector4(0.98, 0.76, 0.30, 1) or C.gold) or C.soft)
  gui.set_text(T.next_t, S.final and "Finish" or (wait_left > 0 and string.format("Next (%d)", math.ceil(wait_left)) or "Next"))
  gui.set_enabled(T.go, S.go and T.s ~= nil)
  gui.set_text(T.go_t, S.id == "strike" and "Show me the place" or "Take me there")
  gui.set_color(T.skip, self.hover == T.skip_hit and C.hover or C.soft)
end

-- A ring at a screen point (its label above it, or to the right of it: `side`).
local function ring_at(T, sx, sy, size, label, t, side)
  local pulse = 1 + 0.12 * math.sin(t * 6)
  gui.set_enabled(T.ring, true)
  gui.set_enabled(T.ring2, true)
  gui.set_screen_position(T.ring, V3(sx, sy, 0))
  gui.set_screen_position(T.ring2, V3(sx, sy, 0))
  gui.set_size(T.ring, V3(size * pulse, size * pulse, 0))
  gui.set_size(T.ring2, V3(size * 0.78, size * 0.78, 0))
  gui.set_color(T.ring, vmath.vector4(0.949, 0.698, 0.2, 0.75 + 0.25 * math.sin(t * 6)))
  if label then
    gui.set_enabled(T.tag, true)
    gui.set_text(T.tag_t, label)
    local tw = ui.text_width("fredoka", label, 14) + 26
    gui.set_size(T.tag, V3(tw, 26, 0))
    gui.set_screen_position(T.tag, V3(sx, sy, 0))
    local p = gui.get_position(T.tag)
    if side == "right" then
      gui.set_position(T.tag, V3(p.x + size * 0.5 + tw / 2 + 10, p.y, 0))
    else
      local up = p.y + size * 0.5 + 22
      gui.set_position(T.tag, V3(p.x, (up > 700) and (p.y - size * 0.5 - 22) or up, 0))
    end
  else
    gui.set_enabled(T.tag, false)
  end
end

local function ring_node(T, node, label, t, grow, side)
  if not node or not gui.is_enabled(node) then return false end
  local p = gui.get_screen_position(node)
  local s = gui.get_size(node)
  if s.x > s.y * 1.8 then
    -- (a wide thing - a story card, a pill, a menu row: a glowing frame over it, its label beside it, clear of the rest)
    gui.set_enabled(T.rect, true)
    gui.set_screen_position(T.rect, V3(p.x, p.y, 0))
    gui.set_size(T.rect, V3(s.x + 14, s.y + 12, 0))
    gui.set_color(T.rect, vmath.vector4(0.949, 0.698, 0.2, 0.28 + 0.18 * math.sin(t * 6)))
    T.rect_on = true
    ring_at(T, p.x, p.y, s.y, label, t, side)
    if side == "right" and label then
      local rp, tp = gui.get_position(T.rect), gui.get_position(T.tag)
      gui.set_position(T.tag, V3(rp.x + (s.x + 14) / 2 + ui.text_width("fredoka", label, 14) / 2 + 26, tp.y, 0))
    end
    gui.set_enabled(T.ring, false); gui.set_enabled(T.ring2, false)
    return true
  end
  ring_at(T, p.x, p.y, math.max(s.x, s.y) * (grow or 1.35), label, t, side)
  return true
end

local function hide_marks(T)
  for _, n in ipairs({ T.ring, T.ring2, T.tag, T.rect }) do gui.set_enabled(n, false) end
end

function M.update(self, W, dt)
  local T = self.tut
  if not M.on or not T or not W or not W.v3 then return end
  local menu = require("game.ui.menu")
  local S = STEPS[M.step]
  if not S then M.stop(self, true); return end
  local speed = require("game.platform.speed")
  local skipping = speed.v3_skipping()
  -- (hidden under a menu, and while a skip runs: the planet turning is the lesson then)
  local hide = menu.is_open() or skipping
  gui.set_enabled(T.root, not hide)
  T.t = T.t + dt
  T.clock = (T.clock or 0) + dt
  -- the meteor's root event: the newest meteor intervention since the cast
  if T.cast_t and not T.ev then
    local chain = require("sim.chain")
    chain.update(W, now_h(W))
    for _, I in ipairs(chain.list(W)) do
      if I.power == "meteor" and not (T.known and T.known[I.ev]) then T.ev = I.ev; break end
    end
  end
  -- skips seen through
  if skipping and speed.skip then T.skipping = speed.skip.kind
  elseif T.skipping then
    -- (only the skips the lesson asked for count: the night passing at the start does not)
    if S.id == "skip_day" or S.id == "skip_season" then
      if T.skipping == "season" then T.skips.season = true else T.skips.short = true end
    end
    T.skipping = nil
  end
  if hide then
    hide_marks(T)
    if S.done and S.done(T, W, self) then M.step = M.step + 1; M.enter(self, W) end
    return
  end
  layout(self, W, S)
  T.rect_on = false
  local ringed = false
  if S.hl == "speed" and self.speed_btns then
    for _, b in ipairs(self.speed_btns) do
      if b.level == 3 and not b.hidden then ringed = ring_node(T, b.node, "Fast", T.clock) end
    end
  elseif S.hl == "meteor" and self.pbtns then
    for i, b in ipairs(self.pbtns) do
      local d = require("game.ui.powers").DEFS[i]
      if d and d.id == "meteor" then ringed = ring_node(T, b.node, "Meteor", T.clock) end
    end
  elseif S.hl == "target" and T.s and T.target then
    local av = require("game.view.agents_view")
    local VV = nil
    for _, v in ipairs(av.SETS or {}) do if v.s == T.s then VV = v; break end end
    local px, py, pz, ux, uy, uz
    if VV and VV.s then px, py, pz, ux, uy, uz = av.world_pos(VV, T.target.x, T.target.y, 0.004)
    else
      ux, uy, uz = require("sim.agents.map").unit(T.s, T.target.x, T.target.y)
      local r = require("game.view.view").surface(ux, uy, uz) + 0.01
      px, py, pz = ux * r, uy * r, uz * r
    end
    local cam = require("game.view.camera")
    local sx, sy = cam.project(px, py, pz)
    if sx and cam.facing(ux, uy, uz) then ring_at(T, sx, sy, 70, "Strike here", T.clock); ringed = true end
  elseif S.hl == "news" and self.news and T.ev then
    local news = require("sim.news")
    for _, c in ipairs(self.news.cards) do
      if c.st and gui.is_enabled(c.node) and news.chain(W, c.st).root_ev == T.ev then
        ringed = ring_node(T, c.node, "Click", T.clock, 1.05, "right")
        break
      end
    end
  elseif S.hl == "actions" and self.actions then
    ringed = ring_node(T, self.actions.pill, "Your actions", T.clock, 1.0)
  elseif S.hl == "skip" and self.speed_btns then
    if self.skip_menu_open then
      local o = self.skip_opts[S.want or 3]
      if o then ringed = ring_node(T, o.node, S.want == 4 and "Next season" or "One day", T.clock, 1.0, "right") end
    else
      for _, b in ipairs(self.speed_btns) do
        if b.skipmenu then ringed = ring_node(T, b.node, "Skip ahead", T.clock) end
      end
    end
  end
  if not ringed then hide_marks(T) end
  if not T.rect_on then gui.set_enabled(T.rect, false) end
  if S.done and S.done(T, W, self) then
    M.step = M.step + 1
    M.enter(self, W)
  end
end

function M.hit(self, x, y)
  local T = self.tut
  if not M.on or not T or not gui.is_enabled(T.root) then return nil end
  if gui.is_enabled(T.next) and gui.pick_node(T.next, x, y) then return T.next_hit end
  if gui.pick_node(T.skip, x, y) then return T.skip_hit end
  if gui.is_enabled(T.go) and gui.pick_node(T.go, x, y) then return T.go_hit end
  if gui.pick_node(T.card, x, y) then return "panel" end
  return nil
end

function M.activate(self, W, h)
  local T = self.tut
  if type(h) ~= "table" or not h.tut then return false end
  local S = STEPS[M.step]
  if h.tut == "skip" then M.stop(self, false); return true end
  if h.tut == "go" and T.s then
    if S and S.id == "strike" then fly_to_target(T); return true end
    local ux, uy, uz = require("sim.agents.map").unit(T.s, T.s.sqx or 0, T.s.sqy or 0)
    require("game.view.camera").fly_to_dir(ux, uy, uz, 1.3)
    return true
  end
  if h.tut == "next" and S and S.next then
    if S.wait and T.t < S.wait then return true end
    if S.final then M.stop(self, true); return true end
    M.step = M.step + 1
    M.enter(self, W)
    return true
  end
  return true
end

return M
