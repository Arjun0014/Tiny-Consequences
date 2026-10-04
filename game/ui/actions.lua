-- Your actions (S23, D-182; docs/v3/77_CHAIN_REACTION.md §5): each intervention of the god and what came
-- of it, read from sim/chain.lua (real causal links only) and laid out here. A pill at the bottom left
-- ("Your actions 3"); opened, a panel in the left column (the news steps aside): the casts, newest first -
-- the power and the place, the day, how many consequences (direct / all), still unfolding; a cast opens its
-- chain - the tree of incidents, each a line of what happened, clickable: the camera goes there and selects
-- whom it is about. News cards rooted at YOU open their cast's chain from their "chain" pill. A second pill,
-- "News", opens the same panel on every story so far (sim/news.lua's history, newest first; click to go there).
-- Call only from GUI script callbacks.
local ui = require("game.ui.uikit")
local chain = require("sim.chain")
local news = require("sim.news")

local M = {}
local C = ui.C
local V3 = vmath.vector3

M.PW, M.PH = 384, 470
M.X0 = 12
M.TOP = 720 - 84
M.LIST_N, M.LIST_H = 8, 52
M.TREE_N, M.TREE_H = 15, 25
M.INDENT = 15

local ICON = { lightning = "bolt", rain = "rain", dry = "drought", warm = "heat", cold = "chill", stir = "volcano",
  strain = "pickaxe", cull = "skull", bring = "paw", meteor = "meteor", oil = "oil", blight = "blight", sickness = "thermo", sea = "wave" }
M.ICON = ICON
local DANGER = { fire = true, flood = true, quake = true, wave = true, death = true, wound = true, strike = true, fell = true,
  lost = true, hunger = true, take = true, ["event:quake"] = true, ["event:eruption"] = true, ["event:wildfire"] = true,
  sickness = true, impact = true, blight = true, fouled = true, oiled = true }
local HELP = { help = true, lodging = true, ["return"] = true, pay = true, takenin = true, fed = true, palisade = true }
local STATE = { soil = true, river = true, air = true, crops = true, sick = true, fields = true, seed = true, sea = true, fish = true,
  oil = true, dust = true, packs = true, herds = true }
local BLUE = vmath.vector4(0.24, 0.45, 0.70, 1)

local function trunc(font, str, px, w)
  if ui.text_width(font, str, px) <= w then return str end
  local s = str
  while #s > 3 and ui.text_width(font, s .. "...", px) > w do s = s:sub(1, #s - 1) end
  return s .. "..."
end

local function day_of(W, t)
  local d = math.floor(t / 24)
  local y = math.floor(d / 48)
  d = d % 48 + 1
  return y > 0 and string.format("Year %d, day %d", y, d) or string.format("Day %d", d)
end

function M.build(self)
  local A = { open = false, mode = "list", scroll = 0, rows = {}, trows = {}, t = 0 }
  self.actions = A
  -- the pill (bottom left)
  local pill = ui.box(nil, M.X0 + 86, 14 + 20, 172, 40, "pill", C.panel)
  ui.anchor(pill, gui.ANCHOR_LEFT, gui.ANCHOR_BOTTOM)
  A.pill = pill
  A.pill_icon = ui.icon(pill, -64, 1, "star", 22, C.gold)
  A.pill_t = ui.text(pill, -46, 0, "Your actions", "fredoka", 15, C.text)
  A.pill_n = ui.box(pill, 66, 0, 30, 22, "pill_s", C.gold)
  A.pill_nt = ui.text(A.pill_n, 0, 0, "0", "fredoka", 12, C.white, gui.PIVOT_CENTER)
  gui.set_enabled(pill, false)
  -- the news so far (above it: the power bar takes the bottom edge beside it)
  local np = ui.box(nil, M.X0 + 50, 14 + 20, 100, 40, "pill", C.panel)
  ui.anchor(np, gui.ANCHOR_LEFT, gui.ANCHOR_BOTTOM)
  A.npill = np
  ui.icon(np, -30, 1, "globe", 20, C.teal)
  ui.text(np, -14, 0, "News", "fredoka", 15, C.text)
  gui.set_enabled(np, false)
  -- the panel
  local PW, PH = M.PW, M.PH
  local root = ui.box(nil, M.X0 + PW / 2, M.TOP - PH / 2, 1, 1, "px", C.clear)
  ui.anchor(root, gui.ANCHOR_LEFT, gui.ANCHOR_TOP)
  A.root = root
  A.sh = ui.box(root, 0, -3, PW + 22, PH + 22, "shadow", C.shadow)
  A.panel = ui.box(root, 0, 0, PW, PH, "panel", C.panel_solid)
  A.title = ui.text(A.panel, -PW / 2 + 16, PH / 2 - 22, "YOUR ACTIONS", "fredoka", 17, C.text)
  A.close = ui.box(A.panel, PW / 2 - 22, PH / 2 - 22, 28, 28, "circle", C.soft)
  ui.icon(A.close, 0, 0, "close", 16, C.text)
  A.sub = ui.text(A.panel, -PW / 2 + 16, PH / 2 - 44, "", "nunito_semi", 12, C.muted)
  -- list rows
  for i = 1, M.LIST_N do
    local y = PH / 2 - 62 - M.LIST_H / 2 - (i - 1) * M.LIST_H
    local r = {}
    r.bg = ui.box(A.panel, 0, y, PW - 16, M.LIST_H - 4, "pill_s", C.clear)
    r.icon = ui.icon(r.bg, -PW / 2 + 30, 0, "star", 24, C.text)
    r.title = ui.text(r.bg, -PW / 2 + 50, 9, "", "fredoka", 14.5, C.text)
    r.line = ui.text(r.bg, -PW / 2 + 50, -10, "", "nunito_semi", 12, C.muted)
    r.dot = ui.box(r.bg, PW / 2 - 24, 9, 9, 9, "circle", C.gold)
    r.n = ui.text(r.bg, PW / 2 - 18, -10, "", "fredoka", 13, C.text, gui.PIVOT_E)
    r.i = i
    r.act_row = i
    A.rows[i] = r
  end
  -- the chain view: the cast, "go there", then the tree
  A.back = ui.box(A.panel, -PW / 2 + 26, PH / 2 - 22, 28, 28, "circle", C.soft)
  ui.icon(A.back, -1, 0, "chev_left", 16, C.text)
  A.go = ui.box(A.panel, PW / 2 - 58, PH / 2 - 62, 84, 24, "pill_s", C.gold)
  A.go_t = ui.text(A.go, 0, 0, "Go there", "fredoka", 12.5, C.white, gui.PIVOT_CENTER)
  for i = 1, M.TREE_N do
    local y = PH / 2 - 92 - M.TREE_H / 2 - (i - 1) * M.TREE_H
    local r = {}
    r.bg = ui.box(A.panel, 0, y, PW - 16, M.TREE_H - 2, "pill_s", C.clear)
    r.vline = ui.box(r.bg, 0, 0, 2, M.TREE_H, "px", vmath.vector4(0.42, 0.38, 0.47, 0.35))
    r.tick = ui.box(r.bg, 0, 0, 8, 2, "px", vmath.vector4(0.42, 0.38, 0.47, 0.35))
    r.dot = ui.box(r.bg, 0, 0, 7, 7, "circle", C.gold)
    r.title = ui.text(r.bg, 0, 0, "", "nunito_semi", 12.5, C.text)
    r.i = i
    r.act_node = i
    A.trows[i] = r
  end
  gui.set_enabled(root, false)
  return A
end

local function set_open(self, on)
  local A = self.actions
  A.open = on
  gui.set_enabled(A.root, on)
  local ok, newsfeed = pcall(require, "game.ui.newsfeed")
  if ok then newsfeed.hidden = on end
end

-- Opens the chain of intervention ev (from a news card's "chain" pill).
function M.open_for(self, ev)
  local A = self.actions
  if not A then return end
  set_open(self, true)
  A.mode, A.ev, A.scroll, A.sig = "tree", ev, 0, nil
end

local function kind_color(nd)
  if DANGER[nd.kind] then return C.coral end
  if HELP[nd.kind] then return C.teal end
  if STATE[nd.kind] then return BLUE end
  return C.text
end

-- Fill the panel and the pill from sim/chain.lua (five times a second).
function M.update(self, W, now, dt)
  local A = self.actions
  if not A or not W or not W.v3 then return end
  -- (the bridge's request: open on an intervention, or close - shots and tests)
  local rq = M.request
  if rq then
    M.request = nil
    set_open(self, rq.open)
    if rq.open and rq.news then A.mode, A.scroll = "news", 0
    elseif rq.open and rq.ev then A.mode, A.ev, A.scroll = "tree", rq.ev, 0
    else A.mode, A.scroll = "list", 0 end
    A.t = 1
  end
  A.t = A.t + dt
  if A.t < 0.2 then
    if A.open then
      for _, r in ipairs(A.mode == "tree" and A.trows or A.rows) do
        gui.set_color(r.bg, self.hover == r and C.hover or C.clear)
      end
      gui.set_color(A.close, self.hover == "act_close" and C.hover or C.soft)
      gui.set_color(A.back, self.hover == "act_back" and C.hover or C.soft)
    end
    return
  end
  A.t = 0
  chain.update(W, now)
  local list = chain.list(W)
  -- the pill: shown once the god has acted
  gui.set_enabled(A.pill, #list > 0)
  gui.set_text(A.pill_nt, tostring(#list))
  gui.set_color(A.pill, (self.hover == "act_pill" or (A.open and A.mode ~= "news")) and C.hover or C.panel)
  local NR = news.rt(W).list
  gui.set_enabled(A.npill, #NR > 0)
  gui.set_position(A.npill, V3(M.X0 + 50, #list > 0 and 82 or 34, 0))
  gui.set_color(A.npill, (self.hover == "act_news" or (A.open and A.mode == "news")) and C.hover or C.panel)
  if not A.open then return end
  local PW = M.PW
  if A.mode == "news" then
    -- (S23) every story so far, newest first
    gui.set_enabled(A.back, false)
    gui.set_enabled(A.go, false)
    for _, r in ipairs(A.trows) do gui.set_enabled(r.bg, false) end
    gui.set_position(A.title, V3(-PW / 2 + 16, M.PH / 2 - 22, 0))
    gui.set_text(A.title, "THE NEWS SO FAR")
    gui.set_text(A.sub, string.format("%d stories, newest first - click one to go there", #NR))
    A.max_scroll = math.max(0, #NR - M.LIST_N)
    A.scroll = math.min(A.scroll, A.max_scroll)
    for i, r in ipairs(A.rows) do
      local st = NR[#NR - (i - 1) - A.scroll]
      r.I, r.st = nil, st
      gui.set_enabled(r.bg, st ~= nil and st.title ~= nil)
      if st and st.title then
        ui.set_icon(r.icon, st.icon or "info")
        gui.set_text(r.title, trunc("fredoka", st.title, 14.5, PW - 130))
        local h = now - (st.t1 or now)
        local ago = h < 1 and "now" or (h < 24 and string.format("%dh ago", math.floor(h)) or string.format("%dd ago", math.floor(h / 24)))
        gui.set_text(r.line, trunc("nunito_semi", (st.line or "") .. " - " .. ago, 12, PW - 110))
        local root = st.root or news.chain(W, st).root
        gui.set_text(r.n, root == "you" and "YOU" or (root == "people" and "PEOPLE" or "NATURE"))
        gui.set_color(r.n, root == "you" and C.gold or (root == "people" and C.muted or C.teal))
        gui.set_enabled(r.dot, st.active and st.level == 1)
      end
    end
    return
  end
  local list_mode = A.mode == "list"
  gui.set_enabled(A.back, not list_mode)
  gui.set_enabled(A.go, not list_mode)
  for _, r in ipairs(A.rows) do gui.set_enabled(r.bg, list_mode) end
  for _, r in ipairs(A.trows) do gui.set_enabled(r.bg, not list_mode) end
  if list_mode then
    gui.set_position(A.title, V3(-PW / 2 + 16, M.PH / 2 - 22, 0))
    gui.set_text(A.title, "YOUR ACTIONS")
    gui.set_text(A.sub, #list == 0 and "Nothing yet: cast a power, then come back"
      or "What each of your casts set off - click one to follow it")
    A.max_scroll = math.max(0, #list - M.LIST_N)
    A.scroll = math.min(A.scroll, A.max_scroll)
    for i, r in ipairs(A.rows) do
      local I = list[i + A.scroll]
      r.I, r.st = I, nil
      gui.set_enabled(r.bg, I ~= nil)
      if I then
        ui.set_icon(r.icon, ICON[I.power] or "star")
        gui.set_text(r.title, trunc("fredoka", I.title, 14.5, PW - 130))
        local unf = I.active and " - still unfolding" or ""
        gui.set_text(r.line, trunc("nunito_semi", string.format("%s - %d direct, %d in all%s", day_of(W, I.t), I.n_direct, I.n_total, unf), 12, PW - 110))
        gui.set_text(r.n, tostring(I.n_total))
        gui.set_color(r.n, C.text)
        gui.set_enabled(r.dot, I.active)
      end
    end
    return
  end
  -- the chain of one cast
  local I = chain.get(W, A.ev)
  if not I then A.mode = "list"; return end
  gui.set_position(A.title, V3(-PW / 2 + 48, M.PH / 2 - 22, 0))
  gui.set_text(A.title, trunc("fredoka", "YOUR " .. string.upper(I.title), 17, PW - 100))
  gui.set_text(A.sub, string.format("%s - %d consequences (%d direct)%s", day_of(W, I.t), I.n_total, I.n_direct,
    I.active and ", still unfolding" or ""))
  local rows = chain.rows(W, I, 400)
  -- (for the tree's lines: does a later sibling follow each row?)
  local last_at = {}
  for k = #rows, 1, -1 do
    local d = rows[k].depth
    rows[k].more = last_at[d] ~= nil and (last_at[d] > k)
    last_at[d] = last_at[d] or k
    for dd = d + 1, 12 do last_at[dd] = nil end
  end
  A.max_scroll = math.max(0, #rows - M.TREE_N)
  A.scroll = math.min(A.scroll, A.max_scroll)
  if #rows == 0 then
    local r = A.trows[1]
    gui.set_enabled(r.bg, true)
    r.nd = nil
    gui.set_position(r.title, V3(-PW / 2 + 22, 0, 0))
    gui.set_text(r.title, I.active and "Nothing has come of it yet - give it time" or "Nothing came of it")
    gui.set_color(r.title, C.muted)
    gui.set_enabled(r.vline, false); gui.set_enabled(r.tick, false); gui.set_enabled(r.dot, false)
    for k = 2, #A.trows do gui.set_enabled(A.trows[k].bg, false) end
    return
  end
  for i, r in ipairs(A.trows) do
    local row = rows[i + A.scroll]
    gui.set_enabled(r.bg, row ~= nil)
    r.nd = row and row.node
    if row then
      local nd = row.node
      local x0 = -PW / 2 + 18 + (row.depth - 1) * M.INDENT
      gui.set_enabled(r.vline, row.depth > 1)
      gui.set_enabled(r.tick, row.depth > 1)
      if row.depth > 1 then
        local lx = x0 - M.INDENT + 4
        gui.set_position(r.vline, V3(lx, row.more and 0 or M.TREE_H / 4, 0))
        gui.set_size(r.vline, V3(2, row.more and M.TREE_H or M.TREE_H / 2, 0))
        gui.set_position(r.tick, V3(lx + 4, 0, 0))
      end
      gui.set_enabled(r.dot, nd.active)
      gui.set_position(r.dot, V3(x0 + 3, 0, 0))
      local tx = nd.title or nd.kind
      if nd.line then tx = tx .. " - " .. nd.line end
      gui.set_position(r.title, V3(x0 + 12, 0, 0))
      gui.set_text(r.title, trunc("nunito_semi", tx, 12.5, PW / 2 - 14 - (x0 + 12)))
      gui.set_color(r.title, kind_color(nd))
    end
  end
end

-- What is under (x, y): the pill, a row, back, close, go there; "panel" over the panel; nil.
function M.hit(self, x, y)
  local A = self.actions
  if not A then return nil end
  if gui.is_enabled(A.pill) and gui.pick_node(A.pill, x, y) then return "act_pill" end
  if gui.is_enabled(A.npill) and gui.pick_node(A.npill, x, y) then return "act_news" end
  if not A.open then return nil end
  if gui.pick_node(A.close, x, y) then return "act_close" end
  if A.mode ~= "list" then
    if gui.pick_node(A.back, x, y) then return "act_back" end
    if gui.pick_node(A.go, x, y) then return "act_go" end
    for _, r in ipairs(A.trows) do
      if gui.is_enabled(r.bg) and r.nd and gui.pick_node(r.bg, x, y) then return r end
    end
  else
    for _, r in ipairs(A.rows) do
      if gui.is_enabled(r.bg) and (r.I or r.st) and gui.pick_node(r.bg, x, y) then return r end
    end
  end
  if gui.pick_node(A.panel, x, y) then return "panel" end
  return nil
end

-- The camera to a chain's node (or to the cast itself): flies close in, selects whom it is about.
function M.go(W, nd, I)
  local camera = require("game.view.camera")
  local selection = require("game.ui.selection")
  local uistate = require("game.ui.uistate")
  local fo = chain.focus(W, nd, I)
  if not fo then return nil end
  do local ok, audio = pcall(require, "game.audio.audio"); if ok and audio.ui then audio.ui("ui_click") end end
  local s = W.S.v3.settlements[fo.sid or 0]
  if s and s.frame then
    local ux, uy, uz = require("sim.agents.map").unit(s, fo.x or 0, fo.y or 0)
    camera.fly_to_dir(ux, uy, uz, 1.42)
    local p = fo.pid and W.S.v3.people[fo.pid]
    if p and p.alive then selection.set({ kind = "person", id = p.id, v3 = true })
    else selection.set({ kind = "settlement", sid = s.id }) end
    uistate.inspector_collapsed = false
  elseif fo.cell then
    local g = W.grid
    camera.fly_to_dir(g.X[fo.cell], g.Y[fo.cell], g.Z[fo.cell], 0.7)
  end
  return fo
end

-- A click on h (from M.hit). Returns true when handled.
function M.activate(self, W, h)
  local A = self.actions
  if not A then return false end
  if h == "act_pill" then
    set_open(self, not (A.open and A.mode ~= "news"))
    A.mode, A.scroll = "list", 0
    A.t = 1
    return true
  elseif h == "act_news" then
    set_open(self, not (A.open and A.mode == "news"))
    A.mode, A.scroll = "news", 0
    A.t = 1
    return true
  elseif h == "act_close" then set_open(self, false); return true
  elseif h == "act_back" then A.mode, A.scroll, A.t = "list", 0, 1; return true
  elseif h == "act_go" then M.go(W, nil, chain.get(W, A.ev)); return true
  elseif type(h) == "table" and h.act_row and h.st then
    -- (a story of the news so far: go there, as its card would)
    require("game.ui.newsfeed").go(W, h.st)
    return true
  elseif type(h) == "table" and h.act_row and h.I then
    A.mode, A.ev, A.scroll, A.t = "tree", h.I.ev, 0, 1
    return true
  elseif type(h) == "table" and h.act_node and h.nd then
    M.go(W, h.nd, chain.get(W, A.ev))
    return true
  end
  return false
end

-- The wheel over the open panel scrolls it.
function M.scroll(self, x, y, step)
  local A = self.actions
  if not A or not A.open or not gui.pick_node(A.panel, x, y) then return false end
  A.scroll = math.max(0, math.min((A.scroll or 0) + step, A.max_scroll or 0))
  A.t = 1
  return true
end

return M
