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


local ICON = { lightning = "bolt", rain = "rain", dry = "drought", warm = "heat", cold = "chill", stir = "volcano",
  strain = "pickaxe", cull = "skull", bring = "paw", meteor = "meteor", oil = "oil", blight = "blight", sickness = "thermo", sea = "wave" }
M.ICON = ICON
local DANGER = { fire = true, flood = true, quake = true, wave = true, death = true, wound = true, strike = true, fell = true,
  lost = true, hunger = true, take = true, ["event:quake"] = true, ["event:eruption"] = true, ["event:wildfire"] = true,
  sickness = true, impact = true, blight = true, fouled = true, oiled = true }
local HELP = { help = true, lodging = true, ["return"] = true, pay = true, takenin = true, fed = true, palisade = true, rainout = true }
local STATE = { soil = true, river = true, air = true, crops = true, sick = true, fields = true, seed = true, sea = true, fish = true,
  oil = true, dust = true, packs = true, herds = true, volc = true, spread = true, melt = true }
local BLUE = vmath.vector4(0.24, 0.45, 0.70, 1)
-- (S25b, D-219) each kind of consequence its icon in the tree (the chain read at a glance: fire, death, flood, hunger, help)
local KIND_ICON = { fire = "fire", flood = "rain", quake = "pickaxe", wave = "wave", death = "skull", wound = "heart", strike = "bolt",
  fell = "house", lost = "skull", hunger = "bread", take = "paw", ["event:quake"] = "pickaxe", ["event:eruption"] = "volcano",
  ["event:wildfire"] = "fire", sickness = "thermo", impact = "meteor", blight = "blight", fouled = "oil", oiled = "oil",
  help = "heart", lodging = "house", ["return"] = "paw", pay = "star", takenin = "people", fed = "bread", palisade = "house",
  rainout = "rain", soil = "drop", river = "wave", air = "thermo", crops = "wheat", sick = "thermo", fields = "wheat", seed = "wheat",
  sea = "wave", fish = "fish", oil = "oil", dust = "cloud", packs = "wolf", herds = "deer", volc = "volcano", spread = "rain",
  melt = "snow", camp = "house", migrate = "settlers" }

local layout = require("game.ui.layout")

-- (S26) compact: the panel as tall as its content or the column's room (game/ui/layout.lua), its rows counted to fit
-- (S25's 15 tree rows overran its 470 px panel), "Go there" beside the count rather than over the title, indentation
-- capped so a deep chain's lines keep their room, a scrollbar when there is more; the News and Your actions pills side by
-- side in the bottom-left corner
M.PW = 300
M.HEAD = 48
M.LIST_N, M.LIST_H = 14, 42
M.TREE_N, M.TREE_H = 30, 20
M.BIG_H = 36
M.INDENT, M.MAX_DEPTH = 13, 9
M.PILL_H = 32

local function day_of(W, t)
  local d = math.floor(t / 24)
  local y = math.floor(d / 48)
  d = d % 48 + 1
  return y > 0 and string.format("Year %d, day %d", y, d) or string.format("Day %d", d)
end

function M.build(self)
  local A = { open = false, mode = "list", scroll = 0, rows = {}, trows = {}, t = 0 }
  self.actions = A
  -- the pills (bottom left): News, then Your actions
  local pills = ui.root(10, 10, gui.ANCHOR_LEFT, gui.ANCHOR_BOTTOM)
  A.pills = pills
  local np = ui.box(pills, 42, M.PILL_H / 2, 84, M.PILL_H, "pill", C.panel)
  A.npill = np
  ui.icon(np, -24, 1, "globe", 17, C.teal)
  ui.text(np, -10, 0, "News", "fredoka", 13.5, C.text)
  gui.set_enabled(np, false)
  local pill = ui.box(pills, 90 + 76, M.PILL_H / 2, 152, M.PILL_H, "pill", C.panel)
  A.pill = pill
  A.pill_icon = ui.icon(pill, -58, 1, "star", 18, C.gold)
  A.pill_t = ui.text(pill, -44, 0, "Your actions", "fredoka", 13.5, C.text)
  A.pill_n = ui.box(pill, 56, 0, 28, 20, "pill_s", C.gold)
  A.pill_nt = ui.text(A.pill_n, 0, 0, "0", "fredoka", 11, C.white, gui.PIVOT_CENTER)
  gui.set_enabled(pill, false)
  -- the panel (the left column's top-left corner)
  local PW = M.PW
  local root = ui.root(10, 720 - 62, gui.ANCHOR_LEFT, gui.ANCHOR_TOP)
  A.root = root
  A.panel, A.sh = ui.panel_nw(root, 0, 0, PW, 300, C.panel_solid)
  A.h = 300
  A.title = ui.text(A.panel, 14, -18, "YOUR ACTIONS", "fredoka", 14.5, C.text)
  A.close = ui.box(A.panel, PW - 18, -18, 24, 24, "circle", C.soft)
  ui.icon(A.close, 0, 0, "close", 11, C.text)
  A.sub = ui.text(A.panel, 14, -36, "", "nunito_semi", 10.5, C.muted)
  -- list rows
  for i = 1, M.LIST_N do
    local r = {}
    r.bg = ui.box(A.panel, PW / 2, 0, PW - 14, M.LIST_H - 4, "pill_s", C.clear)
    r.icon = ui.icon(r.bg, -PW / 2 + 26, 0, "star", 20, C.text)
    r.title = ui.text(r.bg, -PW / 2 + 44, 8, "", "fredoka", 13, C.text)
    r.line = ui.text(r.bg, -PW / 2 + 44, -9, "", "nunito_semi", 10.5, C.muted)
    r.dot = ui.box(r.bg, PW / 2 - 22, 8, 8, 8, "circle", C.gold)
    r.n = ui.text(r.bg, PW / 2 - 16, -9, "", "fredoka", 12, C.text, gui.PIVOT_E)
    r.i = i
    r.act_row = i
    A.rows[i] = r
  end
  -- the chain view: the cast, its size and "go there", then the tree
  A.back = ui.box(A.panel, 18, -18, 24, 24, "circle", C.soft)
  ui.icon(A.back, -1, 0, "chev_left", 12, C.text)
  -- (S25b) the chain's size, large: how many consequences, how many generations deep
  A.big = ui.box(A.panel, 14 + 31, -M.HEAD - M.BIG_H / 2 + 2, 62, 26, "pill", C.gold)
  A.big_t = ui.text(A.big, 0, 0, "", "fredoka", 16, C.white, gui.PIVOT_CENTER)
  A.big_l = ui.text(A.panel, 14 + 62 + 8, -M.HEAD - M.BIG_H / 2 + 2, "", "nunito_semi", 10.5, C.text)
  gui.set_enabled(A.big, false)
  A.go = ui.box(A.panel, PW - 12 - 36, -M.HEAD - M.BIG_H / 2 + 2, 72, 22, "pill_s", C.gold)
  A.go_t = ui.text(A.go, 0, 0, "Go there", "fredoka", 11.5, C.white, gui.PIVOT_CENTER)
  for i = 1, M.TREE_N do
    local r = {}
    r.bg = ui.box(A.panel, PW / 2, 0, PW - 14, M.TREE_H - 1, "pill_s", C.clear)
    r.vline = ui.box(r.bg, 0, 0, 1.5, M.TREE_H, "px", vmath.vector4(0.42, 0.38, 0.47, 0.35))
    r.tick = ui.box(r.bg, 0, 0, 7, 1.5, "px", vmath.vector4(0.42, 0.38, 0.47, 0.35))
    r.dot = ui.box(r.bg, 0, 0, 6, 6, "circle", C.gold)
    r.kicon = ui.icon(r.bg, 0, 0, "info", 12, C.text)
    r.title = ui.text(r.bg, 0, 0, "", "nunito_semi", 11.5, C.text)
    r.i = i
    r.act_node = i
    A.trows[i] = r
  end
  -- the scrollbar
  A.track = ui.box(A.panel, PW - 6, 0, 3.5, 100, "pill_s", vmath.vector4(0.42, 0.38, 0.47, 0.14), gui.PIVOT_N)
  A.thumb = ui.box(A.panel, PW - 6, 0, 3.5, 30, "pill_s", vmath.vector4(0.42, 0.38, 0.47, 0.55), gui.PIVOT_N)
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

--- (S26) Close the panel (Esc).
function M.close(self)
  if self.actions then set_open(self, false) end
end

-- Opens the chain of intervention ev (from a news card's "chain" pill).
function M.open_for(self, ev)
  local A = self.actions
  if not A then return end
  set_open(self, true)
  A.mode, A.ev, A.scroll, A.sig = "tree", ev, 0, nil
  A.t = 1
end

local function kind_color(nd)
  if DANGER[nd.kind] then return C.coral end
  if HELP[nd.kind] then return C.teal end
  if STATE[nd.kind] then return BLUE end
  return C.text
end

-- the panel's height for n rows of height rh under a header of top (the column's room at most); returns the height and
-- how many rows fit
local function size_for(A, top, n, rh)
  local maxh = layout.column_h(M.PW)
  local h = math.min(top + math.max(1, n) * rh + 10, maxh)
  if h ~= A.h then
    A.h = h
    ui.size_panel(A.panel, A.sh, M.PW, h)
  end
  return h, math.max(1, math.floor((h - top - 8) / rh))
end

local function scrollbar(A, top, h, first, shown, total)
  local on = total > shown
  gui.set_enabled(A.track, on)
  gui.set_enabled(A.thumb, on)
  if not on then return end
  local th = h - top - 10
  gui.set_position(A.track, V3(M.PW - 6, -top, 0))
  gui.set_size(A.track, V3(3.5, th, 0))
  local tsz = math.max(20, th * shown / total)
  gui.set_size(A.thumb, V3(3.5, tsz, 0))
  local span = math.max(1, total - shown)
  gui.set_position(A.thumb, V3(M.PW - 6, -top - (th - tsz) * math.min(1, first / span), 0))
end

-- Fill the panel and the pills from sim/chain.lua (five times a second).
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
  A.clock = (A.clock or 0) + dt
  if A.t < 0.2 then
    if A.open then
      for _, r in ipairs(A.mode == "tree" and A.trows or A.rows) do
        local glow = A.mode == "tree" and r.nd and A.seen and A.seen[r.nd] and A.primed and (A.clock or 0) - A.seen[r.nd] < 3
        if not glow then gui.set_color(r.bg, self.hover == r and C.hover or C.clear) end
      end
      gui.set_color(A.close, self.hover == "act_close" and C.hover or C.soft)
      gui.set_color(A.back, self.hover == "act_back" and C.hover or C.soft)
      gui.set_color(A.go, self.hover == "act_go" and vmath.vector4(0.98, 0.76, 0.30, 1) or C.gold)
    end
    return
  end
  A.t = 0
  chain.update(W, now)
  local list = chain.list(W)
  -- the pills: News once there is a story, Your actions once the god has acted (beside it)
  local NR = news.rt(W).list
  gui.set_enabled(A.npill, #NR > 0)
  gui.set_enabled(A.pill, #list > 0)
  gui.set_position(A.pill, V3((#NR > 0 and 90 or 0) + 76, M.PILL_H / 2, 0))
  gui.set_text(A.pill_nt, tostring(#list))
  gui.set_color(A.pill, (self.hover == "act_pill" or (A.open and A.mode ~= "news")) and C.hover or C.panel)
  gui.set_color(A.npill, (self.hover == "act_news" or (A.open and A.mode == "news")) and C.hover or C.panel)
  if not A.open then return end
  local PW = M.PW
  if A.mode == "news" or A.mode == "list" then
    -- the list: every story so far (newest first), or every cast and what it set off
    gui.set_enabled(A.big, false)
    gui.set_enabled(A.big_l, false)
    gui.set_enabled(A.back, false)
    gui.set_enabled(A.go, false)
    for _, r in ipairs(A.trows) do gui.set_enabled(r.bg, false) end
    gui.set_position(A.title, V3(14, -18, 0))
    local news_mode = A.mode == "news"
    local total = news_mode and #NR or #list
    gui.set_text(A.title, news_mode and "THE NEWS SO FAR" or "YOUR ACTIONS")
    gui.set_text(A.sub, ui.trunc("nunito_semi", news_mode and string.format("%d stories, newest first - click one to go there", #NR)
      or (#list == 0 and "Nothing yet: cast a power, then come back" or "What each of your casts set off - click one to follow it"), 10.5, PW - 28))
    local h, fit = size_for(A, M.HEAD, total, M.LIST_H)
    fit = math.min(fit, M.LIST_N)
    A.max_scroll = math.max(0, total - fit)
    A.scroll = math.min(A.scroll, A.max_scroll)
    for i, r in ipairs(A.rows) do
      local on = i <= fit
      local st = news_mode and on and NR[#NR - (i - 1) - A.scroll] or nil
      local I = (not news_mode) and on and list[i + A.scroll] or nil
      r.I, r.st = I, st
      local any = (st ~= nil and st.title ~= nil) or I ~= nil
      gui.set_enabled(r.bg, any)
      gui.set_position(r.bg, V3(PW / 2, -M.HEAD - M.LIST_H / 2 - (i - 1) * M.LIST_H, 0))
      if st and st.title then
        ui.set_icon(r.icon, st.icon or "info")
        gui.set_text(r.title, ui.trunc("fredoka", st.title, 13, PW - 120))
        local hh = now - (st.t1 or now)
        local ago = hh < 1 and "now" or (hh < 24 and string.format("%dh ago", math.floor(hh)) or string.format("%dd ago", math.floor(hh / 24)))
        gui.set_text(r.line, ui.trunc("nunito_semi", (st.line or "") .. " - " .. ago, 10.5, PW - 104))
        local root = st.root or news.chain(W, st).root
        gui.set_text(r.n, root == "you" and "YOU" or (root == "people" and "PEOPLE" or "NATURE"))
        gui.set_color(r.n, root == "you" and C.gold or (root == "people" and C.muted or C.teal))
        gui.set_enabled(r.dot, st.active and st.level == 1)
      elseif I then
        ui.set_icon(r.icon, ICON[I.power] or "star")
        gui.set_text(r.title, ui.trunc("fredoka", I.title, 13, PW - 120))
        local unf = I.active and " - still unfolding" or ""
        gui.set_text(r.line, ui.trunc("nunito_semi", string.format("%s - %d direct, %d in all%s", day_of(W, I.t), I.n_direct, I.n_total, unf), 10.5, PW - 104))
        gui.set_text(r.n, tostring(I.n_total))
        gui.set_color(r.n, C.text)
        gui.set_enabled(r.dot, I.active)
      end
    end
    scrollbar(A, M.HEAD, h, A.scroll, fit, total)
    return
  end
  -- the chain of one cast
  for _, r in ipairs(A.rows) do gui.set_enabled(r.bg, false) end
  local I = chain.get(W, A.ev)
  if not I then A.mode = "list"; return end
  gui.set_enabled(A.big, true)
  gui.set_enabled(A.big_l, true)
  gui.set_enabled(A.back, true)
  gui.set_enabled(A.go, true)
  gui.set_position(A.title, V3(38, -18, 0))
  gui.set_text(A.title, ui.trunc("fredoka", "YOUR " .. string.upper(I.title), 14.5, PW - 38 - 34))
  gui.set_text(A.sub, ui.trunc("nunito_semi", string.format("%s - one cast, and all it set off%s", day_of(W, I.t), I.active and " (still unfolding)" or ""), 10.5, PW - 28))
  local rows = chain.rows(W, I, 400)
  -- (S25b) the size of it, and the new links since the panel looked (they glow for a few seconds)
  local gen = 0
  for _, rw in ipairs(rows) do if rw.depth > gen then gen = rw.depth end end
  gui.set_text(A.big_t, tostring(I.n_total))
  gui.set_text(A.big_l, ui.trunc("nunito_semi", string.format("consequence%s, %d generation%s deep - %d direct", I.n_total == 1 and "" or "s", gen, gen == 1 and "" or "s", I.n_direct),
    10.5, PW - (14 + 62 + 8) - 72 - 18))
  A.seen = A.seen or {}
  A.seen_ev = A.seen_ev or A.ev
  if A.seen_ev ~= A.ev then A.seen, A.seen_ev, A.primed = {}, A.ev, false end
  local tnow = (A.clock or 0)
  -- (for the tree's lines: does a later sibling follow each row?)
  local last_at = {}
  for k = #rows, 1, -1 do
    local d = rows[k].depth
    rows[k].more = last_at[d] ~= nil and (last_at[d] > k)
    last_at[d] = last_at[d] or k
    for dd = d + 1, 40 do last_at[dd] = nil end
  end
  local top = M.HEAD + M.BIG_H + 4
  local h, fit = size_for(A, top, math.max(1, #rows), M.TREE_H)
  fit = math.min(fit, M.TREE_N)
  A.max_scroll = math.max(0, #rows - fit)
  A.scroll = math.min(A.scroll, A.max_scroll)
  if #rows == 0 then
    local r = A.trows[1]
    gui.set_enabled(r.bg, true)
    gui.set_position(r.bg, V3(PW / 2, -top - M.TREE_H / 2, 0))
    r.nd = nil
    gui.set_position(r.title, V3(-PW / 2 + 18, 0, 0))
    gui.set_text(r.title, I.active and "Nothing has come of it yet - give it time" or "Nothing came of it")
    gui.set_color(r.title, C.muted)
    gui.set_enabled(r.vline, false); gui.set_enabled(r.tick, false); gui.set_enabled(r.dot, false); gui.set_enabled(r.kicon, false)
    for k = 2, #A.trows do gui.set_enabled(A.trows[k].bg, false) end
    scrollbar(A, top, h, 0, 1, 1)
    return
  end
  for i, r in ipairs(A.trows) do
    local row = i <= fit and rows[i + A.scroll] or nil
    gui.set_enabled(r.bg, row ~= nil)
    r.nd = row and row.node
    if row then
      gui.set_position(r.bg, V3(PW / 2, -top - M.TREE_H / 2 - (i - 1) * M.TREE_H, 0))
      local nd = row.node
      local depth = math.min(row.depth, M.MAX_DEPTH)
      local x0 = -PW / 2 + 16 + (depth - 1) * M.INDENT
      gui.set_enabled(r.vline, row.depth > 1)
      gui.set_enabled(r.tick, row.depth > 1)
      gui.set_enabled(r.kicon, true)
      if row.depth > 1 then
        local lx = x0 - M.INDENT + 4
        gui.set_position(r.vline, V3(lx, row.more and 0 or M.TREE_H / 4, 0))
        gui.set_size(r.vline, V3(1.5, row.more and M.TREE_H or M.TREE_H / 2, 0))
        gui.set_position(r.tick, V3(lx + 4, 0, 0))
      end
      -- (S25b) its kind's icon on the branch, a gold dot while it still unfolds, and a glow when it is new
      ui.set_icon(r.kicon, KIND_ICON[nd.kind] or "star")
      gui.set_position(r.kicon, V3(x0 + 5, 0, 0))
      gui.set_color(r.kicon, kind_color(nd))
      gui.set_enabled(r.dot, nd.active)
      gui.set_position(r.dot, V3(x0 + 12, 5, 0))
      local tx = nd.title or nd.kind
      if nd.line then tx = tx .. " - " .. nd.line end
      if row.depth > M.MAX_DEPTH then tx = string.format("(%d) ", row.depth) .. tx end
      gui.set_position(r.title, V3(x0 + 15, 0, 0))
      gui.set_text(r.title, ui.trunc("nunito_semi", tx, 11.5, PW / 2 - 14 - (x0 + 15)))
      gui.set_color(r.title, kind_color(nd))
      local first = A.seen[nd]
      if not first then first = A.primed and tnow or -100; A.seen[nd] = first end
      if tnow - first < 3 and A.primed then gui.set_color(r.bg, vmath.vector4(1.0, 0.86, 0.45, 0.55)) end
    end
  end
  -- (the rows below the window are seen too: what grows later glows)
  for k = 1, #rows do if not A.seen[rows[k].node] then A.seen[rows[k].node] = A.primed and tnow or -100 end end
  A.primed = true
  scrollbar(A, top, h, A.scroll, fit, #rows)
end

--- (S26) The bottom-left pills' rectangle in window pixels (both as one), or nil.
function M.pills_rect(self)
  local A = self.actions
  if not A then return nil end
  local r = nil
  for _, n in ipairs({ A.npill, A.pill }) do
    if layout.shown(n) then
      local q = layout.rect(n)
      r = r and { math.min(r[1], q[1]), math.min(r[2], q[2]), math.max(r[3], q[3]), math.max(r[4], q[4]) } or q
    end
  end
  return r
end

-- What is under (x, y): the pill, a row, back, close, go there; "panel" over the panel; nil.
function M.hit(self, x, y)
  local A = self.actions
  if not A then return nil end
  if gui.is_enabled(A.pill) and gui.pick_node(A.pill, x, y) then return "act_pill" end
  if gui.is_enabled(A.npill) and gui.pick_node(A.npill, x, y) then return "act_news" end
  if not A.open or not gui.is_enabled(A.root) then return nil end
  if gui.pick_node(A.close, x, y) then return "act_close" end
  if A.mode == "tree" then
    if gui.is_enabled(A.back) and gui.pick_node(A.back, x, y) then return "act_back" end
    if gui.is_enabled(A.go) and gui.pick_node(A.go, x, y) then return "act_go" end
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
