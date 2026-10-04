-- The opening tutorial (S25, D-211): teaching by doing. A coach card at the top of the screen walks the player
-- through looking at the world, inspecting someone, ONE lightning bolt into a dry tree by a village, the news that
-- noticed it, going there, watching, "Your actions", skipping a day and then a season, and coming back to the same
-- bolt's chain. Every step waits for the player to do the thing (read from the game's own state: the camera, the
-- selection, the armed power, the casts, the news, the open panels, the skips) and everything it shows - whether the
-- tree caught, how many consequences, which branches - is read from the world (sim/chain.lua, sim/news.lua): nothing is
-- scripted. Its only starting condition is physical: the ground about the village dry (sim/tutorial3.lua, a logged
-- command). Skippable at every step; replayable from the menu. Call only from GUI script callbacks.
local ui = require("game.ui.uikit")

local M = {}
local C = ui.C
local V3 = vmath.vector3

M.on = false
M.step = 0
M.CW, M.CH = 520, 150
M.CX = 668   -- (clear of the news column and of "Your actions" open beside it)

local function fmt_n(n, one, many) return n == 1 and ("1 " .. one) or (n .. " " .. (many or (one .. "s"))) end

-- The steps: id, title, text (a string or fn(T, W) -> string), and done(T, W, self) -> true when complete.
-- `next`: a Next button instead of waiting; `hl`: what to ring ("lightning", "news", "actions", "skip", "tree", ...).
local STEPS

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
  ui.box(root, 0, -4, M.CW + 24, M.CH + 24, "shadow", C.shadow)
  T.card = ui.box(root, 0, 0, M.CW, M.CH, "panel", C.panel_solid)
  T.stripe = ui.box(T.card, -M.CW / 2 + 4, 0, 6, M.CH - 18, "pill_s", C.gold)
  T.count = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 18, "", "nunito_semi", 12, C.muted)
  T.title = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 42, "", "fredoka", 21, C.text)
  T.body = ui.text(T.card, -M.CW / 2 + 20, M.CH / 2 - 60, "", "nunito_semi", 14.5, C.text)
  gui.set_pivot(T.body, gui.PIVOT_NW)
  gui.set_line_break(T.body, true)
  gui.set_size(T.body, V3((M.CW - 40) / (14.5 / ui.FONT_SIZE.nunito_semi), 300, 0))
  T.next = ui.box(T.card, M.CW / 2 - 66, -M.CH / 2 + 22, 110, 30, "pill", C.gold)
  T.next_t = ui.text(T.next, 0, 0, "Next", "fredoka", 15, C.white, gui.PIVOT_CENTER)
  T.next_hit = { tut = "next" }
  T.skip = ui.box(T.card, M.CW / 2 - 186, -M.CH / 2 + 22, 116, 30, "pill", C.soft)
  ui.text(T.skip, 0, 0, "Skip tutorial", "fredoka", 13, C.muted, gui.PIVOT_CENTER)
  T.skip_hit = { tut = "skip" }
  T.go = ui.box(T.card, M.CW / 2 - 316, -M.CH / 2 + 22, 130, 30, "pill", C.teal)
  T.go_t = ui.text(T.go, 0, 0, "Take me there", "fredoka", 13, C.white, gui.PIVOT_CENTER)
  T.go_hit = { tut = "go" }
  for _, n in ipairs({ T.root, T.ring, T.ring2, T.tag, T.rect }) do gui.set_enabled(n, false) end
  ui.top = false
  return T
end

-- The village of the lesson and a dry tree at the edge of its woods (reads the world only).
local function pick_village(W)
  local V = W.S.v3
  local best = nil
  for _, s in ipairs(V.settlements) do
    if s.alive and s.land and s.trees and not s.founding then
      if not best or (s.id == 1) then best = s end
      if s.id == 1 then break end
    end
  end
  return best
end

local function pick_tree(W, s, avoid)
  local blaze = require("sim.agents.blaze")
  local T = s.trees
  local cand = {}
  blaze.trees_near(s, s.sqx or 0, s.sqy or 0, 0.9, function(k, d2)
    if d2 > 0.25 * 0.25 and not (avoid and avoid[k]) then cand[#cand + 1] = { k, d2 } end
  end)
  table.sort(cand, function(a, b) if a[2] ~= b[2] then return a[2] < b[2] end return a[1] < b[1] end)
  -- (the nearest tree with a stand of others about it: the edge of a wood, where a fire can go somewhere)
  for _, c in ipairs(cand) do
    local n = 0
    blaze.trees_near(s, T.x[c[1]], T.y[c[1]], 0.08, function() n = n + 1 end)
    if n >= 6 then return c[1] end
  end
  return cand[1] and cand[1][1] or nil
end

local function now_h(W) return require("game.platform.speed").v3_t or W.hour end

-- Start (or restart) the tutorial.
function M.start(self, W)
  local T = self.tut
  if not T or not W or not W.v3 then return end
  M.on, M.step = true, 1
  T.s = pick_village(W)
  T.tree, T.ev, T.cast_n, T.skips, T.n_first = nil, nil, nil, {}, nil
  T.zoom0 = require("game.view.camera").zoom
  T.t = 0
  if T.s then require("sim.world").command(W, "tutorial_dry", { sid = T.s.id, sw = 0.2 }) end
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
  local powers = require("game.ui.powers")
  powers.tutorial = nil
end

local function chain_of(W, T)
  if not T.ev then return nil end
  local chain = require("sim.chain")
  chain.update(W, now_h(W))
  return chain.get(W, T.ev)
end

-- The real branches of the bolt's chain (titles of its nodes, the first few levels), for the coach's text.
local function branches(W, T, n)
  local I = chain_of(W, T)
  if not I then return {} end
  local rows = require("sim.chain").rows(W, I, 40)
  local out = {}
  for _, r in ipairs(rows) do
    if #out >= n then break end
    out[#out + 1] = string.rep("   ", r.depth - 1) .. "- " .. (r.node.title or "?")
  end
  return out
end

local function fire_now(T)
  local s = T.s
  local n = 0
  for _, f in ipairs(s and s.burn or {}) do if not f.out and f.heat > 0.02 then n = n + 1 end end
  return n
end

STEPS = {
  { title = "This tiny world lives without you",
    text = "Everyone you see is a person with a family, a home, work and reasons of their own. The animals, the weather, the rivers and the sea all run by themselves.\nYou are a god who can touch it - once - and then follow everything that touch causes.",
    next = true },
  { title = "1  Look at the world",
    text = "Drag to turn the planet, and scroll (or pinch) to zoom in on a village until you can see its people. Or press 'Take me there'.",
    go = true,
    done = function(T, W)
      -- (the camera moved or zoomed by the player, or 'Take me there' - and close enough to see the people)
      local cam = require("game.view.camera")
      local st = require("game.view.state")
      local c0 = T.cam0
      if not c0 then return false end
      local moved = math.abs(cam.zoom - c0.z) > 0.12 or ((st.focus_x or 0) * c0.x + (st.focus_y or 0) * c0.y + (st.focus_z or 0) * c0.zz) < 0.9997
      return (moved or T.went) and cam.zoom >= 1.05
    end },
  { title = "2  Inspect someone",
    text = "Click a person (or a house, a sheep, a field). The panel on the right shows who they are, their family and what they are doing - and why.",
    done = function(T, W)
      local sel = require("game.ui.selection").sel
      return sel ~= nil and sel.kind ~= "planet"
    end },
  { title = "3  Choose Lightning",
    text = "Now make ONE intervention. Pick Lightning on the power bar at the bottom (the bolt, or Shift+1).",
    hl = "lightning",
    done = function(T, W)
      local powers = require("game.ui.powers")
      local d = powers.armed and powers.DEFS[powers.armed]
      return d and d.id == "lightning"
    end },
  { title = "4  Strike the tree",
    text = function(T, W)
      return "The ground here is dry after a rainless spell. Click the tree marked with the ring - at the edge of the woods by the village. The bolt hits the tallest thing where you click."
        .. (T.miss and "\n(" .. T.miss .. ")" or "")
    end,
    hl = "tree", go = true,
    done = function(T, W)
      local powers = require("game.ui.powers")
      if powers.casts ~= T.cast_n and powers.last and powers.last.id == "lightning" then
        T.cast_n = powers.casts
        T.cast_t = now_h(W)
        return true
      end
      return false
    end },
  { title = "5  The world noticed",
    text = function(T, W)
      if not T.ev then return "The bolt is coming down..." end
      local f = fire_now(T)
      local I = chain_of(W, T)
      local caught = I and I.n_total > 0
      return (f > 0 and "The tree caught fire - nobody scripted that: it was dry, and fire spreads by dryness and wind. "
        or (caught and "The bolt struck. " or "The bolt struck - but nothing caught: the world decides that, not you. "))
        .. "A story appeared in the WORLD NEWS on the left (marked with the glow). Click it."
        .. (T.wrong and " (That was another story - the world has its own. Click the one marked YOU.)" or "")
    end,
    hl = "news", retry = true,
    done = function(T, W)
      local nf = require("game.ui.newsfeed")
      local lg = nf.last_go
      if not (lg and T.ev) then return false end
      local ch = require("sim.news").chain(W, lg.st)
      if ch.root_ev == T.ev then return true end
      -- (another story: it went there, but the lesson is the bolt's)
      if not T.wrong_go or T.wrong_go ~= lg then T.wrong_go = lg; T.wrong = true end
      return false
    end },
  { title = "6  Go there - and watch",
    text = function(T, W)
      local f = fire_now(T)
      return "The camera went to the very place it happened. Time runs at Watch speed: see who runs, who carries water, how the animals bolt. "
        .. (f > 0 and ("Burning now: " .. fmt_n(f, "thing") .. ".") or "Nothing is burning now.")
    end,
    next = true, wait = 12 },
  { title = "7  Your actions",
    text = "You caused one thing. Everything after it belongs to the simulation. Open YOUR ACTIONS (bottom left) to see every real consequence of your bolt, as a tree.",
    hl = "actions",
    done = function(T, W, self) local A = self.actions; return A and A.open and A.mode == "tree" end },
  { title = "8  Skip forward",
    text = "Time can jump. Open the SKIP menu (the last button by the clock) and choose 'Until dawn' or 'One day'. People are hidden while time passes.",
    hl = "skip",
    done = function(T, W) return T.skips.short or T.skips.season end },
  { title = "9  Now a whole season",
    text = "Open the skip menu again and choose 'Next season' - twelve days. Watch the clock run.",
    hl = "skip",
    done = function(T, W) return T.skips.season end },
  { title = "10  Back to your one bolt",
    text = function(T, W)
      local I = chain_of(W, T)
      local n = I and I.n_total or 0
      local b = branches(W, T, 4)
      local head = n == 0 and "Your one lightning bolt caused nothing more - the world shrugged it off. The same bolt in another place, another day, makes a different chain."
        or ("Your one lightning bolt now has " .. fmt_n(n, "consequence") .. ":")
      return head .. (#b > 0 and ("\n" .. table.concat(b, "\n")) or "") .. "\nOpen Your actions to see them all - each line takes you to where it happened."
    end,
    next = true, final = true },
}
M.STEPS = STEPS

function M.enter(self, W)
  local T = self.tut
  local S = STEPS[M.step]
  if not S then M.stop(self, true); return end
  T.t = 0
  T.miss = nil
  local powers = require("game.ui.powers")
  -- (in the lesson the bolt is ready whenever it is asked for: the recharge would only make one wait)
  if S.hl == "tree" or S.hl == "lightning" then powers.ready.lightning = nil end
  if S.hl == "tree" and T.s and not T.tree then T.tree = pick_tree(W, T.s, T.struck) end
  if S.hl == "news" then require("game.ui.newsfeed").last_go = nil; powers.disarm() end
  if M.step == 2 then
    local cam = require("game.view.camera")
    local st = require("game.view.state")
    T.cam0 = { z = cam.zoom, x = st.focus_x or 0, y = st.focus_y or 0, zz = st.focus_z or 0 }
    T.went = false
  end
  -- (the tree to strike framed below the card, close in)
  if S.hl == "tree" and T.s and T.tree then
    local s = T.s
    local ux, uy, uz = require("sim.agents.map").unit(s, s.trees.x[T.tree], s.trees.y[T.tree])
    require("game.view.camera").fly_to_dir(ux, uy, uz, 1.42)
  end
  if S.hl == "skip" then T.skip_seen = false end
  gui.set_text(T.next_t, S.final and "Finish" or "Next")
end

local function layout(self, W, S)
  local T = self.tut
  local txt = type(S.text) == "function" and S.text(T, W) or S.text
  gui.set_text(T.count, M.step == 1 and "TUTORIAL" or string.format("TUTORIAL  -  step %d of %d", M.step - 1, #STEPS - 1))
  gui.set_text(T.title, S.title)
  gui.set_text(T.body, txt)
  local lines = 0
  for seg in (txt .. "\n"):gmatch("(.-)\n") do
    lines = lines + math.max(1, math.ceil(ui.text_width("nunito_semi", seg, 14.5) / (M.CW - 44)))
  end
  local h = math.max(150, 96 + lines * 20)
  if h ~= T.h then
    T.h = h
    gui.set_size(T.card, V3(M.CW, h, 0))
    gui.set_position(T.root, V3(M.CX, 720 - 92 - h / 2, 0))
    gui.set_position(T.count, V3(-M.CW / 2 + 20, h / 2 - 18, 0))
    gui.set_position(T.title, V3(-M.CW / 2 + 20, h / 2 - 42, 0))
    gui.set_position(T.body, V3(-M.CW / 2 + 20, h / 2 - 60, 0))
    gui.set_size(T.stripe, V3(6, h - 18, 0))
    gui.set_position(T.stripe, V3(-M.CW / 2 + 4, 0, 0))
    for _, n in ipairs({ T.next, T.skip, T.go }) do
      local p = gui.get_position(n)
      gui.set_position(n, V3(p.x, -h / 2 + 22, 0))
    end
  end
  local wait_left = S.wait and math.max(0, S.wait - T.t) or 0
  local show_next = S.next and wait_left <= 0
  gui.set_enabled(T.next, S.next and true or false)
  gui.set_color(T.next, show_next and (self.hover == T.next_hit and vmath.vector4(0.98, 0.76, 0.30, 1) or C.gold) or C.soft)
  gui.set_text(T.next_t, S.final and "Finish" or (wait_left > 0 and string.format("Next (%d)", math.ceil(wait_left)) or "Next"))
  -- (S25) the bolt struck but nothing caught (wet, or the luck of it): another tree may be tried - a second real cast
  local retry = S.retry and T.ev and T.cast_t and now_h(W) - T.cast_t > 0.15 and fire_now(T) == 0 and (T.retries or 0) < 2
  T.can_retry = retry
  gui.set_enabled(T.go, (S.go and T.s ~= nil) or retry)
  gui.set_text(T.go_t, retry and "Strike another tree" or "Take me there")
  gui.set_size(T.go, V3(retry and 160 or 130, 30, 0))
  gui.set_color(T.skip, self.hover == T.skip_hit and C.hover or C.soft)
end

-- A ring round a GUI node (its screen rectangle) or at a screen point.
local function ring_at(T, sx, sy, size, label, t)
  local pulse = 1 + 0.12 * math.sin(t * 6)
  for _, n in ipairs({ T.ring, T.ring2 }) do gui.set_enabled(n, true) end
  gui.set_screen_position(T.ring, V3(sx, sy, 0))
  gui.set_screen_position(T.ring2, V3(sx, sy, 0))
  gui.set_size(T.ring, V3(size * pulse, size * pulse, 0))
  gui.set_size(T.ring2, V3(size * 0.78, size * 0.78, 0))
  gui.set_color(T.ring, vmath.vector4(0.949, 0.698, 0.2, 0.75 + 0.25 * math.sin(t * 6)))
  if label then
    gui.set_enabled(T.tag, true)
    gui.set_text(T.tag_t, label)
    gui.set_size(T.tag, V3(ui.text_width("fredoka", label, 14) + 26, 26, 0))
    gui.set_screen_position(T.tag, V3(sx, sy, 0))
    local p = gui.get_position(T.tag)
    local up = p.y + size * 0.5 + 22
    gui.set_position(T.tag, V3(p.x, (up > 700) and (p.y - size * 0.5 - 22) or up, 0))
  else
    gui.set_enabled(T.tag, false)
  end
end

local function ring_node(T, node, label, t, grow)
  if not node or not gui.is_enabled(node) then return false end
  local p = gui.get_screen_position(node)
  local s = gui.get_size(node)
  if s.x > s.y * 1.8 then
    -- (a wide thing - a story card, a pill: a glowing frame over it, the label above)
    ring_at(T, p.x, p.y, s.y, label, t)
    gui.set_enabled(T.ring, false); gui.set_enabled(T.ring2, false)
    gui.set_enabled(T.rect, true)
    gui.set_screen_position(T.rect, V3(p.x, p.y, 0))
    gui.set_size(T.rect, V3(s.x + 14, s.y + 14, 0))
    gui.set_color(T.rect, vmath.vector4(0.949, 0.698, 0.2, 0.28 + 0.18 * math.sin(t * 6)))
    T.rect_on = true
    return true
  end
  ring_at(T, p.x, p.y, math.max(s.x, s.y) * (grow or 1.35), label, t)
  return true
end

function M.update(self, W, dt)
  local T = self.tut
  if not M.on or not T or not W or not W.v3 then return end
  local menu = require("game.ui.menu")
  local S = STEPS[M.step]
  if not S then M.stop(self, true); return end
  -- (hidden under a menu)
  local hide = menu.is_open()
  gui.set_enabled(T.root, not hide)
  if hide then
    for _, n in ipairs({ T.ring, T.ring2, T.tag, T.rect }) do gui.set_enabled(n, false) end
    return
  end
  T.t = T.t + dt
  T.clock = (T.clock or 0) + dt
  -- the bolt's root event: the newest lightning intervention after the cast
  if T.cast_t and not T.ev then
    local chain = require("sim.chain")
    chain.update(W, now_h(W))
    for _, I in ipairs(chain.list(W)) do
      if I.power == "lightning" and (I.t or I.t0 or now_h(W)) >= T.cast_t - 0.05 then T.ev = I.ev; break end
    end
    if not T.ev then
      local L = chain.list(W)
      if L[1] and L[1].power == "lightning" then T.ev = L[1].ev end
    end
  end
  -- skips seen through
  local speed = require("game.platform.speed")
  if speed.v3_skipping() and speed.skip then T.skipping = speed.skip.kind
  elseif T.skipping then
    if T.skipping == "season" then T.skips.season = true else T.skips.short = true end
    T.skipping = nil
  end
  layout(self, W, S)
  -- the ring
  local ringed = false
  if S.hl == "lightning" and self.pbtns then
    for i, b in ipairs(self.pbtns) do
      local d = require("game.ui.powers").DEFS[i]
      if d and d.id == "lightning" then ringed = ring_node(T, b.node, "Lightning", T.clock) end
    end
  elseif S.hl == "tree" and T.s and T.tree then
    local s = T.s
    local Tr = s.trees
    if Tr.st[T.tree] == 1 then
      local av = require("game.view.agents_view")
      local VV = nil
      for _, v in ipairs(av.SETS or {}) do if v.s == s then VV = v; break end end
      local px, py, pz, ux, uy, uz
      if VV and VV.s then px, py, pz, ux, uy, uz = av.world_pos(VV, Tr.x[T.tree], Tr.y[T.tree], 0.004)
      else
        ux, uy, uz = require("sim.agents.map").unit(s, Tr.x[T.tree], Tr.y[T.tree])
        local r = require("game.view.view").surface(ux, uy, uz) + 0.01
        px, py, pz = ux * r, uy * r, uz * r
      end
      local cam = require("game.view.camera")
      local sx, sy = cam.project(px, py, pz)
      if sx and cam.facing(ux, uy, uz) then ring_at(T, sx, sy, 46, "Strike here", T.clock); ringed = true end
    else
      T.tree = pick_tree(W, s)
    end
  elseif S.hl == "news" and self.news and T.ev then
    local nf = require("game.ui.newsfeed")
    local news = require("sim.news")
    for _, c in ipairs(self.news.cards) do
      if c.st and gui.is_enabled(c.node) then
        local ch = news.chain(W, c.st)
        if ch.root_ev == T.ev or c.chain_ev == T.ev then ringed = ring_node(T, c.node, "Click this story", T.clock, 1.05); break end
      end
    end
    if not ringed then
      for _, c in ipairs(self.news.cards) do
        if c.st and gui.is_enabled(c.node) then ringed = ring_node(T, c.node, "Click a story", T.clock, 1.05); break end
      end
    end
    local _ = nf
  elseif S.hl == "actions" and self.actions then
    ringed = ring_node(T, self.actions.pill, "Your actions", T.clock)
  elseif S.hl == "skip" and self.speed_btns then
    if self.skip_menu_open then
      local want = M.step == 9 and 4 or 1
      local o = self.skip_opts[want]
      if o then ringed = ring_node(T, o.node, M.step == 9 and "Next season" or "Until dawn", T.clock, 1.0) end
    else
      for _, b in ipairs(self.speed_btns) do
        if b.skipmenu then ringed = ring_node(T, b.node, "Skip ahead", T.clock) end
      end
    end
  end
  if not ringed then for _, n in ipairs({ T.ring, T.ring2, T.tag }) do gui.set_enabled(n, false) end end
  if not T.rect_on then gui.set_enabled(T.rect, false) end
  T.rect_on = false
  -- a strike that missed the tree (the bolt took something else, or nothing caught): say so, the bolt ready again
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
  if h.tut == "go" and T.can_retry and S and S.retry then
    -- (back to the strike with another tree; the bolt ready again and in hand)
    T.retries = (T.retries or 0) + 1
    T.struck = T.struck or {}
    T.struck[T.tree or 0] = true
    T.tree, T.ev, T.cast_t, T.wrong = nil, nil, nil, nil
    local powers = require("game.ui.powers")
    for i, d in ipairs(powers.DEFS) do if d.id == "lightning" then powers.armed = i end end
    M.step = 5
    M.enter(self, W)
    return true
  end
  if h.tut == "go" and T.s then
    local s = T.s
    local x, y = s.sqx or 0, s.sqy or 0
    if T.tree and s.trees.st[T.tree] == 1 then x, y = s.trees.x[T.tree], s.trees.y[T.tree] end
    local ux, uy, uz = require("sim.agents.map").unit(s, x, y)
    require("game.view.camera").fly_to_dir(ux, uy, uz, 1.42)
    T.went = true
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
