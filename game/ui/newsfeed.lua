-- World news cards (S21, R7, D-172; docs/v3/75_PLAYER_EXPERIENCE.md §3-6), built in code like the
-- inspector. The stories are sim/news.lua's (read from the world); this module only lays them out:
-- the left column under the clock, up to N cards, newest and most important first. A card: a stripe by
-- level (coral BREAKING, teal STORY, plum SLOW), the story's icon, its title and newest line, how long
-- ago, the root of its causes (YOU in gold, NATURE in teal, PEOPLE in grey: click it for "Why?"), and a
-- pulsing dot while a BREAKING story is still happening. Clicking a card goes there (hud.gui_script).
-- Call only from GUI script callbacks.
local ui = require("game.ui.uikit")
local news = require("sim.news")

local M = {}
local C = ui.C
local V3 = vmath.vector3
local layout = require("game.ui.layout")

-- (S26) compact cards in the left column under a "World news" header that folds the column away; as many cards as the
-- column has room for (game/ui/layout.lua), every line cut to its card's width
M.N = 5
M.CW, M.CH, M.GAP = 300, 46, 5
M.HEAD = 24
M.collapsed = false

local LEVEL_C = { vmath.vector4(0.949, 0.42, 0.357, 1), vmath.vector4(0.227, 0.651, 0.627, 1), vmath.vector4(0.55, 0.45, 0.62, 1) }
local ROOT_C = { you = C.gold, nature = C.teal, people = vmath.vector4(0.46, 0.43, 0.50, 1) }
local ROOT_T = { you = "YOU", nature = "NATURE", people = "PEOPLE" }

local trunc = ui.trunc

local function ago(h)
  if h < 1 then return "now" end
  if h < 24 then return string.format("%dh", math.floor(h)) end
  return string.format("%dd", math.floor(h / 24))
end

function M.build(self)
  local F = { cards = {} }
  self.news = F
  local CW, CH = M.CW, M.CH
  -- the root at the column's top-left corner (placed and scaled by the HUD's layout)
  local root = ui.root(10, 720 - 62, gui.ANCHOR_LEFT, gui.ANCHOR_TOP)
  F.root = root
  -- the header: "WORLD NEWS", folds the column
  F.head = ui.box(root, 62, -M.HEAD / 2, 124, M.HEAD, "pill", C.panel)
  F.head_icon = ui.icon(F.head, -48, 0, "globe", 14, C.teal)
  F.head_t = ui.text(F.head, -36, 0, "WORLD NEWS", "fredoka", 11.5, C.text)
  F.head_chev = ui.icon(F.head, 48, 0, "chev_up", 10, C.muted)
  F.head_hit = { news_toggle = true, tip = "Fold the news away (or open it)" }
  for i = 1, M.N do
    local y = -M.HEAD - 6 - CH / 2 - (i - 1) * (CH + M.GAP)
    local c = {}
    c.sh = ui.box(root, CW / 2, y - 2, CW + 14, CH + 14, "shadow", C.shadow)
    c.node = ui.box(root, CW / 2, y, CW, CH, "panel", C.panel)
    c.stripe = ui.box(c.node, -CW / 2 + 3, 0, 4, CH - 12, "pill_s", LEVEL_C[2])
    c.icon = ui.icon(c.node, -CW / 2 + 21, 1, "info", 20, C.text)
    c.dot = ui.box(c.node, -CW / 2 + 10, CH / 2 - 8, 8, 8, "circle", LEVEL_C[1])
    c.title = ui.text(c.node, -CW / 2 + 37, 9, "", "fredoka", 13.5, C.text)
    c.line = ui.text(c.node, -CW / 2 + 37, -10, "", "nunito_semi", 11.5, C.muted)
    c.ago = ui.text(c.node, CW / 2 - 9, 11, "", "nunito_semi", 10.5, C.muted, gui.PIVOT_E)
    c.badge = ui.box(c.node, CW / 2 - 29, -10, 46, 16, "pill_s", C.teal)
    c.badge_t = ui.text(c.badge, 0, 0, "", "fredoka", 9.5, C.white, gui.PIVOT_CENTER)
    -- (S23) for a story rooted at YOU: its cast's chain - all that cast has caused so far (game/ui/actions.lua)
    c.chain = ui.box(c.node, CW / 2 - 85, -10, 58, 16, "pill_s", vmath.vector4(0.30, 0.25, 0.38, 1))
    c.chain_t = ui.text(c.chain, 0, 0, "", "fredoka", 9.5, C.gold, gui.PIVOT_CENTER)
    gui.set_enabled(c.chain, false)
    c.y = y
    F.cards[i] = c
    gui.set_enabled(c.node, false)
    gui.set_enabled(c.sh, false)
  end
  -- the "Why?" panel, under the card it explains
  F.why = ui.box(root, CW / 2, 0, CW, 40, "panel", C.panel_solid)
  F.why_lines = {}
  for k = 1, 7 do
    F.why_lines[k] = ui.text(F.why, -CW / 2 + 12, 0, "", "nunito_semi", 11.5, C.text)
  end
  gui.set_enabled(F.why, false)
  gui.set_enabled(F.head, false)
  F.sig = nil
  F.t = 0
  return F
end

local function hide_all(F)
  for _, c in ipairs(F.cards) do gui.set_enabled(c.node, false); gui.set_enabled(c.sh, false); c.st = nil end
  gui.set_enabled(F.why, false)
end

-- The cards for now (sim/news.lua), refreshed when the stories change or every few seconds.
function M.update(self, W, now, dt)
  local F = self.news
  if not F or not W or not W.v3 then return end
  -- (S23) "Your actions" open in the same column: the cards step aside; (S25) so do they while a dropdown or a menu
  -- is open over them
  if M.hidden or M.suppress or M.menu_open then
    hide_all(F)
    gui.set_enabled(F.head, false)
    F.sig, F.t = nil, 1
    return
  end
  F.t = (F.t or 0) + dt
  F.pulse = (F.pulse or 0) + dt
  gui.set_color(F.head, self.hover == F.head_hit and C.hover or C.panel)
  -- (the stories are read five times a second; the pulse and hover every frame)
  if F.t < 0.2 and F.list then
    for _, c in ipairs(F.cards) do
      local st = c.st
      if st and gui.is_enabled(c.node) then
        if st.active and st.level == 1 then
          gui.set_color(c.dot, vmath.vector4(0.949, 0.42, 0.357, 0.55 + 0.45 * math.sin(F.pulse * 5)))
        end
        local hov = self.hover == c or self.hover == c.badge_hit
        gui.set_color(c.node, (hov and C.hover) or (F.why_for == st.id and C.panel_solid) or C.panel)
      end
    end
    return
  end
  F.t = 0
  news.update(W, now)
  local cards = news.cards(W, M.N, now)
  -- (S21) a story new among the cards is heard (game/audio): once, restrained; not on the first look
  F.heard = F.heard or {}
  for _, st in ipairs(cards) do
    if not F.heard[st.id] then
      F.heard[st.id] = true
      if F.primed then
        local ok, audio = pcall(require, "game.audio.audio")
        if ok and audio.news_cue then audio.news_cue(st, st.root or news.chain(W, st).root) end
      end
    end
  end
  F.primed = true
  gui.set_enabled(F.head, #cards > 0)
  gui.set_text(F.head_t, M.collapsed and string.format("WORLD NEWS (%d)", #cards) or "WORLD NEWS")
  ui.set_icon(F.head_chev, M.collapsed and "chev_down" or "chev_up")
  local hw = ui.text_width("fredoka", gui.get_text(F.head_t), 11.5) + 62
  gui.set_size(F.head, V3(hw, M.HEAD, 0))
  gui.set_position(F.head, V3(hw / 2, -M.HEAD / 2, 0))
  gui.set_position(F.head_icon, V3(-hw / 2 + 14, 0, 0))
  gui.set_position(F.head_t, V3(-hw / 2 + 26, 0, 0))
  gui.set_position(F.head_chev, V3(hw / 2 - 14, 0, 0))
  if M.collapsed then
    hide_all(F)
    F.list, F.sig = cards, nil
    return
  end
  -- as many cards as the column has room for (the Why? panel below its card counts too)
  local room = layout.column_h() - M.HEAD - 6
  local fit_n = math.max(1, math.floor((room + M.GAP) / (M.CH + M.GAP)))
  local sig = #cards * 7 + fit_n
  local chain = require("sim.chain")
  for _, st in ipairs(cards) do
    sig = sig * 31 + st.id * 7 + #st.lines * 3 + (st.active and 1 or 0) + math.floor((now - st.t1) / 6)
    -- (S23) a chain that grew
    if st.root == "you" or not st.root then
      local rev = news.chain(W, st).root_ev
      local I = rev and chain.get(W, rev)
      if I then sig = sig + I.n_total * 131 end
    end
  end
  F.list = cards
  for i, c in ipairs(F.cards) do
    local st = cards[i]
    local on = st ~= nil and i <= fit_n
    gui.set_enabled(c.node, on)
    gui.set_enabled(c.sh, on)
    c.st = on and st or nil
    if on then
      if sig ~= F.sig then
        ui.set_icon(c.icon, st.icon or "info")
        gui.set_text(c.title, trunc("fredoka", st.title or "", 13.5, M.CW - 37 - 34))
        local ch = news.chain(W, st)
        local I = ch.root_ev and require("sim.chain").get(W, ch.root_ev)
        local has_chain = I ~= nil and I.n_total > 0
        gui.set_text(c.line, trunc("nunito_semi", st.line or "", 11.5, M.CW - 37 - (has_chain and 126 or 64)))
        gui.set_text(c.ago, ago(now - st.t1))
        gui.set_color(c.stripe, LEVEL_C[st.level] or LEVEL_C[2])
        local root = st.root or ch.root
        gui.set_color(c.badge, ROOT_C[root] or C.teal)
        gui.set_text(c.badge_t, ROOT_T[root] or "NATURE")
        -- (S23) the chain of the cast at its root: how many consequences so far
        c.chain_ev = has_chain and I.ev or nil
        gui.set_enabled(c.chain, c.chain_ev ~= nil)
        if I then gui.set_text(c.chain_t, string.format("chain %d", I.n_total)) end
      end
      gui.set_enabled(c.dot, st.active and st.level == 1)
      if st.active and st.level == 1 then
        local a = 0.55 + 0.45 * math.sin(F.pulse * 5)
        gui.set_color(c.dot, vmath.vector4(0.949, 0.42, 0.357, a))
      end
      local hov = self.hover == c or self.hover == c.badge_hit
      gui.set_color(c.node, (hov and C.hover) or (F.why_for == st.id and C.panel_solid) or C.panel)
    end
  end
  F.sig = sig
  -- the "Why?" panel
  local wst = nil
  if F.why_for then
    for i, st in ipairs(cards) do if st.id == F.why_for and i <= fit_n then wst = st; F.why_i = i end end
    if not wst then F.why_for = nil end
  end
  gui.set_enabled(F.why, wst ~= nil)
  if wst then
    local ch = news.chain(W, wst)
    local n = math.min(#ch.links, #F.why_lines - 1)
    local h = 22 + n * 16
    local yb = F.cards[F.why_i].y - M.CH / 2 - 4 - h / 2
    gui.set_size(F.why, V3(M.CW, h, 0))
    gui.set_position(F.why, V3(M.CW / 2, yb, 0))
    for k, t in ipairs(F.why_lines) do
      local ln = k == 1 and { text = "Why?", t = nil } or ch.links[k - 1]
      gui.set_enabled(t, k <= n + 1)
      if k <= n + 1 then
        gui.set_position(t, V3(-M.CW / 2 + 12, h / 2 - 11 - (k - 1) * 16, 0))
        local txt = k == 1 and "Why?" or ((k == 2 and "" or "<- ") .. ln.text .. string.format("  (%s)", ago(now - (ln.t or now))))
        gui.set_text(t, trunc("nunito_semi", txt, 11.5, M.CW - 24))
        gui.set_color(t, k == 1 and C.muted or (ln.who == "you" and C.gold or (ln.who == "people" and C.text or C.teal)))
      end
    end
    -- (the cards below the open card move down out of the panel's way; those that would leave the column hide)
    local bottom = -M.HEAD - 6 - room
    for i, c in ipairs(F.cards) do
      local dy = (i > F.why_i) and (h + 8) or 0
      gui.set_position(c.node, V3(M.CW / 2, c.y - dy, 0))
      gui.set_position(c.sh, V3(M.CW / 2, c.y - 2 - dy, 0))
      if c.st and c.y - dy - M.CH / 2 < bottom then gui.set_enabled(c.node, false); gui.set_enabled(c.sh, false); c.st = nil end
    end
  else
    for _, c in ipairs(F.cards) do
      gui.set_position(c.node, V3(M.CW / 2, c.y, 0))
      gui.set_position(c.sh, V3(M.CW / 2, c.y - 2, 0))
    end
  end
end

--- (S26) Fold the news column away, or open it.
function M.toggle(self)
  M.collapsed = not M.collapsed
  if self.news then self.news.sig, self.news.t = nil, 1 end
end

--- (S26) The column's rectangle in window pixels (its header, cards and Why? as one), or nil when nothing shows.
function M.rect(self)
  local F = self.news
  if not F or not layout.shown(F.head) then return nil end
  local r = layout.rect(F.head)
  local function grow(n)
    if layout.shown(n) then
      local q = layout.rect(n)
      r = { math.min(r[1], q[1]), math.min(r[2], q[2]), math.max(r[3], q[3]), math.max(r[4], q[4]) }
    end
  end
  for _, c in ipairs(F.cards) do grow(c.node) end
  grow(F.why)
  return r
end

-- What is under (x, y): a card's badge ({ badge = card }), a card, or nil.
function M.hit(self, x, y)
  local F = self.news
  if not F then return nil end
  if M.hidden or M.suppress or M.menu_open then return nil end
  if gui.is_enabled(F.head) and gui.pick_node(F.head, x, y) then return F.head_hit end
  for _, c in ipairs(F.cards) do
    if c.st and gui.is_enabled(c.node) then
      if c.chain_ev and gui.is_enabled(c.chain) and gui.pick_node(c.chain, x, y) then
        c.chain_hit = c.chain_hit or { news_chain = c }
        return c.chain_hit
      end
      if gui.pick_node(c.badge, x, y) then
        c.badge_hit = c.badge_hit or { news_why = c }
        return c.badge_hit
      end
      if gui.pick_node(c.node, x, y) then
        c.news_card = true
        return c
      end
    end
  end
  if gui.is_enabled(F.why) and gui.pick_node(F.why, x, y) then return "panel" end
  return nil
end

-- The story card c was clicked: its focus (the hud flies the camera and selects).
function M.focus_of(W, c)
  local st = c and c.st
  if not st then return nil end
  return news.focus(W, st), st
end

-- (S21, R7) Go to story st: the camera flies to where it is happening, close in; the person at the
-- heart of it (else the village) selected; an active BREAKING story slows time to Watch, so one arrives
-- while it happens. Returns what was done (for the bridge).
function M.go(W, st)
  local camera = require("game.view.camera")
  local selection = require("game.ui.selection")
  local uistate = require("game.ui.uistate")
  local speed = require("game.platform.speed")
  if not W or not W.v3 or not st then return nil end
  do local ok, audio = pcall(require, "game.audio.audio"); if ok and audio.ui then audio.ui("ui_click") end end
  M.last_go = { st = st, t = speed.v3_t or W.hour }   -- (S25: the tutorial watches for it)
  local fo = news.focus(W, st)
  local s = W.S.v3.settlements[fo.sid or 0]
  local res = { sid = fo.sid, pid = nil }
  if s and s.frame then
    -- (S25, D-208) the story's own place (the fire's hottest point, the flood's water, the crater, the bolt's mark, the
    -- herd, the field); a story about someone on the move follows them
    local p = fo.pid and W.S.v3.people[fo.pid]
    local x, y = fo.x or 0, fo.y or 0
    if fo.follow and p and p.alive and p.at == nil and p.x then x, y = p.x, p.y end
    local ux, uy, uz = require("sim.agents.map").unit(s, x, y)
    camera.fly_to_dir(ux, uy, uz, 1.42)
    res.x, res.y = x, y
    if p and p.alive then
      selection.set({ kind = "person", id = p.id, v3 = true }); res.pid = p.id
      if fo.follow then uistate.request({ cmd = "follow", id = p.id }); res.follow = true end
    else selection.set({ kind = "settlement", sid = s.id }) end
    uistate.inspector_collapsed = false
  elseif fo.cell then
    local g = W.grid
    camera.fly_to_dir(g.X[fo.cell], g.Y[fo.cell], g.Z[fo.cell], 0.9)
    res.cell = fo.cell
  end
  if st.active and st.level == 1 then
    if speed.v3_skipping() then speed.v3_skip_cancel() end
    if speed.get() > 1 then speed.set(1) end
    res.slowed = true
  end
  return res
end

-- Toggle "Why?" for the card's story.
function M.toggle_why(self, c)
  local F = self.news
  local st = c and c.st
  if not F or not st then return end
  F.why_for = (F.why_for ~= st.id) and st.id or nil
  F.sig = nil
end

function M.bounds(self)
  local F = self.news
  if not F then return nil end
  local n = 0
  for _, c in ipairs(F.cards) do if gui.is_enabled(c.node) then n = n + 1 end end
  return n
end

return M
