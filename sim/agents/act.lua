-- Doing things (docs/v3/10_AGENTS.md §5): a person's plan is a list of steps; each step becomes
-- one action with a start t0 and an end t1 and, when they move, an analytic path
-- (pos(t) = path at (t - t0) * spd). Effects happen when the step completes (a sown row, a felled
-- tree, logs in the store); an interrupt cuts a step, keeping what is proportionally done.
--
-- Steps:
--   { op = "go", x, y, place, enter = building id | nil }
--   { op = "work", kind, dur, ref, row, x1, y1 (optional motion end), fx, fy (facing) }
--   { op = "pick", what, from, ref, n }       { op = "drop", what, to, ref }
-- Person action fields: act ("walk" | work kind | "pick" | "drop"), t0, t1, x, y (at t0), mv (path),
-- spd, fx, fy (facing), inside (building id while indoors), carry, camt.
local cfgmod = require("sim.config")
local nav = require("sim.agents.nav")
local mapm = require("sim.agents.map")
local people = require("sim.agents.people")
local ledger = require("sim.ledger")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local IDLE_KINDS = { sit = true, talk = true, rest = true, eat = true, lunch = true, play = true, wait = true,
  nurse = true, idle = true, shelter = true }
local TOOL_WORK = { till = true, harvest = true, chop = true, grub = true, build = true, repair = true, thresh = true, tend = true }
M.OUTDOOR_WORK = { grub = true, forage = true, till = true, thresh = true, sow = true, tend = true, harvest = true, chop = true, build = true, fill = true, play = true, repair = true }

-- Position of p at time t (analytic; exact at t0 and t1).
function M.pos(p, t)
  local mv = p.mv
  if not mv then return p.x, p.y, p.fx, p.fy end
  local d = (t - p.t0) * p.spd
  local x, y, dx, dy = nav.at(mv, d)
  return x, y, dx, dy
end

-- Body: hunger, energy and health over dt hours of an action of kind `kind`.
local function body(p, dt, kind)
  if dt <= 0 then return end
  local c = C()
  local h, e = p.hunger, p.energy
  if kind == "sleep" then
    e = e + c.energy_sleep * dt
    h = h + c.hunger_sleep * dt
  elseif kind == "walk" then
    e = e - c.energy_walk * dt * (p.carry ~= "" and 1.4 or 1)
    h = h + c.hunger_idle * dt
  elseif IDLE_KINDS[kind] then
    e = e - c.energy_idle * dt
    if kind == "rest" or kind == "sit" or kind == "shelter" then e = e + 0.05 * dt end
    h = h + c.hunger_idle * dt
  else
    e = e - c.energy_work * dt
    h = h + c.hunger_work * dt
  end
  if e > 1 then e = 1 elseif e < 0 then e = 0 end
  if h > 1 then h = 1 elseif h < 0 then h = 0 end
  p.hunger, p.energy = h, e
  if h > c.starving then
    p.health = p.health - c.health_starve * dt
  elseif h < 0.6 and p.health < 1 then
    p.health = min(1, p.health + 0.004 * dt)
  end
end
M.body = body

-- Starting a step ----------------------------------------------------------------------------

local function begin(p, now, act, dur)
  p.act, p.t0 = act, now
  if dur < 0.01 then dur = 0.01 end
  p.t1 = now + dur
end

local START = {}

START.go = function(W, s, p, st, now)
  if p.inside ~= 0 then p.inside = 0 end
  local path = p.path_buf
  if not path then path = { x = {}, y = {}, cum = {} }; p.path_buf = path end
  local si = mapm.tile_of(s, p.x, p.y)
  if si > 0 and s.cost[si] < 0 and not st.enter then
    -- standing on a footprint (a site rose around them): step to the nearest open ground
    local fx, fy = mapm.free_spot(s, p.x, p.y, 4)
    if fx then p.x, p.y = fx, fy end
  end
  local tx, ty = st.x, st.y
  local ti = mapm.tile_of(s, tx, ty)
  if ti > 0 and s.cost[ti] < 0 and not st.enter then
    local fx, fy = mapm.free_spot(s, tx, ty, 4)
    if fx then tx, ty = fx, fy end
  end
  local r = nav.path(s, p.x, p.y, tx, ty, p.temper, path)
  if not r then return false end
  p.mv = r
  p.spd = people.speed(W, p, now)
  if s.rain_now == 2 then p.spd = p.spd * C().speed_rain end   -- hurrying through a downpour
  p.dest = st.place or ""
  begin(p, now, "walk", r.len / p.spd)
  return true
end

START.work = function(W, s, p, st, now)
  local dur = st.dur
  if st.x1 then
    local ei = mapm.tile_of(s, st.x1, st.y1)
    if ei == 0 or s.cost[ei] < 0 then st.x1, st.y1 = nil, nil end
  end
  if st.x1 then
    -- working along a line (a row): a two-point path walked at the pace of the work
    local path = p.path_buf
    if not path then path = { x = {}, y = {}, cum = {} }; p.path_buf = path end
    local xs, ys, cum = path.x, path.y, path.cum
    xs[1], ys[1], xs[2], ys[2] = p.x, p.y, st.x1, st.y1
    for k = 3, #xs do xs[k], ys[k], cum[k] = nil, nil, nil end
    local L = nav.dist(p.x, p.y, st.x1, st.y1)
    cum[1], cum[2] = 0, L
    path.n, path.len = 2, L
    p.mv = path
    p.spd = L / max(dur, 0.01)
  else
    p.mv = nil
    if st.fx then
      local dx, dy = st.fx - p.x, st.fy - p.y
      local l = sqrt(dx * dx + dy * dy)
      if l > 1e-6 then p.fx, p.fy = dx / l, dy / l end
    end
  end
  if st.kind == "sleep" or st.kind == "rest" or st.kind == "shelter" or st.kind == "eat" or st.kind == "nurse"
    or st.kind == "visit" or st.kind == "cook" then
    if st.inside and st.inside ~= 0 then p.inside = st.inside end
  end
  begin(p, now, st.kind, dur)
  return true
end

START.pick = function(W, s, p, st, now)
  p.mv = nil
  begin(p, now, "pick", 0.04)
  return true
end

START.drop = function(W, s, p, st, now)
  p.mv = nil
  begin(p, now, "drop", 0.04)
  return true
end

-- Finishing a step ---------------------------------------------------------------------------

local WORK = {}

local function plot_row_line(s, pl, row)
  local T = s.T
  local y = pl.y0 + (row - 0.5) * T
  return pl.x0 + T * 0.5, y, pl.x1 - T * 0.5, y
end
M.plot_row_line = plot_row_line

-- Row work (D-080): the row's progress grows with the hours worked (st.rate: share per hour);
-- the row changes state when its progress is complete.
local function row_progress(pl, st, f, want)
  if pl.row_st[st.row] ~= want then return false end
  local rp = pl.row_p
  if not rp then rp = {}; pl.row_p = rp end
  local a = (rp[st.row] or 0) + st.dur * f * (st.rate or 1)
  if a >= 0.999 then rp[st.row] = 0; return true end
  rp[st.row] = a
  return false
end

WORK.till = function(W, s, p, st, f)
  local pl = s.plots[st.ref]
  if not pl then return end
  if pl.row_claim[st.row] == p.id then pl.row_claim[st.row] = 0 end
  local rs = pl.row_st[st.row]
  if (rs == 0 or rs == 3) and row_progress(pl, st, f, rs) then
    pl.row_st[st.row] = 1
    pl.tilled = (pl.tilled or 0) + 1
    if pl.stage == "rest" then
      -- ploughing the fallow: the weeds turned under; the whole field ploughed rests the soil better
      pl.weeds = max(0, pl.weeds - 1.5 / pl.rows)
      pl.ploughed = (pl.ploughed or 0) + 1
      if pl.ploughed >= pl.rows then pl.fert = min(C().fert_max, (pl.fert or 1) + C().fert_plough) end
    end
  end
  people.practice(p, "sk_farm", st.dur * f)
end

WORK.sow = function(W, s, p, st, f)
  local pl = s.plots[st.ref]
  if not pl then return end
  if pl.row_claim[st.row] == p.id then pl.row_claim[st.row] = 0 end
  local c = C()
  if pl.row_st[st.row] == 1 and p.carry == "seed" and p.camt >= c.seed_row - 1e-6 and row_progress(pl, st, f, 1) then
    pl.row_st[st.row] = 2
    pl.sown = pl.sown + 1
    p.camt = p.camt - c.seed_row
    s.acc.seed = (s.acc.seed or 0) + c.seed_row
    if p.camt <= 0.001 then p.carry, p.camt = "", 0 end
    if pl.stage == "fallow" then pl.stage = "growing"; pl.sown_day = floor(W.hour / 24); pl.growth = 0; pl.weeds = 0; pl.q_sum, pl.q_n = 0, 0 end
  end
  people.practice(p, "sk_farm", st.dur * f)
end

WORK.tend = function(W, s, p, st, f)
  local pl = s.plots[st.ref]
  if not pl then return end
  pl.tend_claim = max(0, (pl.tend_claim or 1) - 1)
  pl.weeds = max(0, pl.weeds - C().tend_clear * st.dur * f * (0.6 + 0.6 * p.sk_farm))
  people.practice(p, "sk_farm", st.dur * f)
end

WORK.harvest = function(W, s, p, st, f)
  local pl = s.plots[st.ref]
  if not pl then return end
  if pl.row_claim[st.row] == p.id then pl.row_claim[st.row] = 0 end
  if pl.stage == "ripe" and row_progress(pl, st, f, 2) then
    pl.row_st[st.row] = 3
    pl.harvested = pl.harvested + 1
    local y = C().row_yield * pl.quality * (pl.spoil or 1)
    pl.sheaves = pl.sheaves + y
    s.acc.harvest = (s.acc.harvest or 0) + y
  end
  people.practice(p, "sk_farm", st.dur * f)
end

WORK.chop = function(W, s, p, st, f)
  local tr = s.trees
  local k = st.ref
  if tr.claim[k] == p.id and f < 1 then
    -- partial: the cut so far stays in the tree
    tr.prog = tr.prog or {}
    tr.prog[k] = (tr.prog[k] or 0) + f
    if tr.prog[k] < 1 then tr.claim[k] = 0; people.practice(p, "sk_wood", st.dur * f); return end
  end
  if tr.st[k] == 1 then
    tr.st[k] = 2
    tr.logs[k] = mapm.tree_logs(tr.size[k])
    tr.t_cut[k] = W.hour
    if tr.prog then tr.prog[k] = nil end
    s.dirty_costs = true
    s.acc.felled = (s.acc.felled or 0) + 1
    -- the planet's forest loses this tree's share of its cell (and the carbon goes to the air)
    local cell = s.cell_of[tr.tile[k]]
    local per = s.tree_share and s.tree_share[cell]
    if per and per > 0 then
      local F = W.F
      local d = min(F.forest[cell], per * tr.size[k])
      F.forest[cell] = F.forest[cell] - d
      local co2 = d * cfgmod.VEG.k_co2
      W.G.co2_src = (W.G.co2_src or 0) + co2
      ledger.add(W, "co2", co2, 0, "villagers")
    end
  end
  tr.claim[k] = 0
  people.practice(p, "sk_wood", st.dur * f)
end

WORK.build = function(W, s, p, st, f)
  local b = s.buildings[st.ref]
  if not b then return end
  b.builders = max(0, (b.builders or 1) - 1)
  if b.state ~= "site" then return end
  local hh = W.S.v3.households[p.hh]
  b.work_done = b.work_done + st.dur * f * (0.6 + 0.6 * p.sk_build) * (0.55 + 0.45 * (hh and hh.tools or 1))
  people.practice(p, "sk_build", st.dur * f)
  if b.work_done >= b.work_need and b.logs_have >= b.logs_need then
    require("sim.agents.settle").complete_building(W, s, b)
  end
end

-- Mending a house (D-080): the repair's progress; done, the house is sound again and the log used.
WORK.repair = function(W, s, p, st, f)
  local b = s.buildings[st.ref]
  if not b then return end
  if b.repairer == p.id then b.repairer = 0 end
  local c = C()
  if (b.repair_logs or 0) < c.repair_logs - 1e-6 then return end
  b.rep_p = (b.rep_p or 0) + st.dur * f * (st.rate or 0.4)
  people.practice(p, "sk_build", st.dur * f)
  if b.rep_p >= 0.999 then
    b.rep_p = 0
    b.cond = 1
    b.repair_logs = b.repair_logs - c.repair_logs
    s.acc.repaired = (s.acc.repaired or 0) + 1
  end
end

-- Craft (D-080): tools mended and made; a little wood used.
WORK.craft = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  local b = s.buildings[st.ref or 0]
  if not hh or not b then return end
  local c = C()
  local h = st.dur * f
  local logs = min(b.stock.logs or 0, h * c.tool_logs)
  if logs <= 0 then return end
  b.stock.logs = b.stock.logs - logs
  hh.tools = min(1, (hh.tools or 1) + h * c.tool_mend * (logs / (h * c.tool_logs)))
end

WORK.thresh = function(W, s, p, st, f)
  local g = s.granary
  if not g then return end
  g.threshers = max(0, (g.threshers or 1) - 1)
  local c = C()
  local n = min(g.stock.sheaves or 0, c.thresh_fu * st.dur / c.thresh_h * f * (0.7 + 0.5 * p.sk_farm))
  if n > 0 then
    g.stock.sheaves = g.stock.sheaves - n
    g.stock.grain = (g.stock.grain or 0) + n
  end
end

WORK.grub = function(W, s, p, st, f)
  local tr = s.trees
  local k = st.ref
  if tr.claim[k] == p.id then tr.claim[k] = 0 end
  if f >= 0.6 and tr.st[k] == 3 then
    tr.st[k] = 0
    tr.size[k] = 0
    s.tree[tr.tile[k]] = 0
    s.dirty_costs = true
    require("sim.agents.settle").finish_clearing(W, s)
  end
end

-- Gathering at the woods' edge takes what is there: the edge's wild food (s.wild, regrown daily
-- on its standing trees, settle.lua) is picked out by everyone who forages (D-080).
WORK.forage = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.foraging == p.id then hh.foraging = 0 end
  local c = C()
  local wild = s.wild or 0
  local got = min(c.forage_basket * f * (0.75 + 0.5 * (st.luck or 0.5)), wild * 0.25)
  if got < 0.01 then return end
  s.wild = wild - got
  p.carry, p.camt = "food", got
  s.acc.foraged = (s.acc.foraged or 0) + got
end

WORK.fill = function(W, s, p, st, f)
  if f >= 0.6 then p.carry, p.camt = "water", 1 end
end

WORK.eat = function(W, s, p, st, f)
  if f < 0.4 then return end
  local c = C()
  local b = s.buildings[st.ref or 0]
  local meals = b and b.stock.meals or 0
  if meals >= c.meal_fu * 0.5 then
    local m = min(meals, c.meal_fu)
    b.stock.meals = meals - m
    p.hunger = max(0, p.hunger - c.meal * m / c.meal_fu)
    s.acc.eaten = (s.acc.eaten or 0) + m
  end
end

-- Cooking (D-080): at the hearth, grain from the pantry becomes the household's meals (bread,
-- porridge, pottage) — when the hearth has wood.
WORK.cook = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.cooking == p.id then hh.cooking = 0 end
  local b = s.buildings[st.ref or 0]
  if not b or f < 0.3 then return end
  if (b.stock.logs or 0) <= 0 and not b.fire then return end
  local c = C()
  local n = hh and #hh.members or 1
  local amt = min(b.stock.food or 0, n * c.cook_batch * f)
  if amt <= 0 then return end
  b.stock.food = b.stock.food - amt
  b.stock.meals = (b.stock.meals or 0) + amt
  b.cooked_t = W.S.v3.now or W.hour
end

WORK.lunch = function(W, s, p, st, f)
  if f < 0.4 then return end
  p.lunch = false
  s.acc.lunch = (s.acc.lunch or 0) + C().meal_fu
  p.hunger = max(0, p.hunger - C().meal)
end

WORK.visit = function(W, s, p, st, f)
  local q = W.S.v3.people[st.with or 0]
  if q and q.alive then
    people.bond(p, q, 0.04 * f)
    people.bond(q, p, 0.03 * f)
  end
end

WORK.talk = function(W, s, p, st, f)
  -- whoever else is gathered at the same spot now
  local V = W.S.v3
  local mem = s.members
  local now = W.S.v3.now or W.hour
  for i = 1, #mem do
    local q = V.people[mem[i]]
    if q and q ~= p and q.alive and q.act == p.act and q.inside == 0 then
      local dx, dy = q.x - p.x, q.y - p.y
      if dx * dx + dy * dy < 0.05 then people.bond(p, q, 0.02 * f) end
    end
  end
end

WORK.play = WORK.talk
WORK.sit = WORK.talk

local PICK, DROP = {}, {}

PICK.grain = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local c = C()
  local want = st.n or c.carry_grain
  local got = min(want, b.stock.grain or 0)
  if got > 0 then
    b.stock.grain = b.stock.grain - got
    p.carry, p.camt = st.as or "grain", got
  end
end

PICK.seed = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = min(st.n or 2, b.stock.seed or 0)
  if got > 0 then
    b.stock.seed = b.stock.seed - got
    p.carry, p.camt = "seed", got
  end
end

PICK.sheaves = function(W, s, p, st)
  local pl = s.plots[st.ref]
  if not pl then return end
  local c = C()
  local got = min(c.carry_grain * (p.strong and 1.3 or 1), pl.sheaves)
  pl.haul_claim = max(0, (pl.haul_claim or 1) - 1)
  if got > 0 then
    pl.sheaves = pl.sheaves - got
    p.carry, p.camt = "sheaves", got
  end
end

PICK.logs = function(W, s, p, st)
  local want = st.n or (p.strong and 2 or 1)
  if st.from == "tree" then
    local tr = s.trees
    local k = st.ref
    if tr.claim[k] == -p.id then tr.claim[k] = 0 end
    local got = min(want, tr.logs[k])
    if got > 0 then
      tr.logs[k] = tr.logs[k] - got
      if tr.logs[k] <= 0 then tr.st[k] = 3; tr.t_cut[k] = W.hour end
      p.carry, p.camt = "logs", got
    end
  else
    local b = s.buildings[st.ref]
    if not b then return end
    local got = min(want, b.stock.logs or 0)
    if got > 0 then
      b.stock.logs = b.stock.logs - got
      p.carry, p.camt = "logs", got
    end
  end
end

DROP.any = function(W, s, p, st)
  if p.carry == "" then return end
  local what, n = p.carry, p.camt
  local b = s.buildings[st.ref]
  if not b then return end
  if st.to == "repair" and what == "logs" then
    b.repair_logs = (b.repair_logs or 0) + n
  elseif st.to == "site" and what == "logs" then
    b.logs_have = b.logs_have + n
    b.deliver_claim = max(0, (b.deliver_claim or 1) - 1)
    if b.work_done >= b.work_need and b.logs_have >= b.logs_need then
      require("sim.agents.settle").complete_building(W, s, b)
    end
  elseif what == "sheaves" then
    b.stock.sheaves = (b.stock.sheaves or 0) + n
  elseif what == "seed" then
    if b.kind == "granary" then b.stock.grain = (b.stock.grain or 0) + n else b.stock.seed = (b.stock.seed or 0) + n end
  elseif what == "grain" then
    if b.kind == "granary" then
      b.stock.grain = (b.stock.grain or 0) + n
    else
      b.stock.food = (b.stock.food or 0) + n
    end
  elseif what == "food" then
    if b.kind == "granary" then b.stock.grain = (b.stock.grain or 0) + n else b.stock.food = (b.stock.food or 0) + n end
  elseif what == "logs" then
    b.stock.logs = (b.stock.logs or 0) + n
  elseif what == "water" then
    b.stock.water = (b.stock.water or 0) + n
  end
  p.carry, p.camt = "", 0
end

-- Apply the effects of p's current step, done to fraction f (1 = complete), at time now.
local function finish(W, s, p, now, f)
  local st = p.plan[p.pi]
  local dt = now - p.t0
  body(p, dt, p.act)
  if p.mv then
    local x, y, dx, dy = M.pos(p, now)
    -- wear along what was walked
    if p.act == "walk" then
      local mv = p.mv
      local d = (now - p.t0) * p.spd
      local px, py = mv.x[1], mv.y[1]
      local wv = C().wear_walk
      for k = 2, mv.n do
        if mv.cum[k] <= d then
          mapm.wear_segment(s, px, py, mv.x[k], mv.y[k], wv)
          px, py = mv.x[k], mv.y[k]
        else
          mapm.wear_segment(s, px, py, x, y, wv)
          break
        end
      end
    end
    p.x, p.y = x, y
    local l = sqrt(dx * dx + dy * dy)
    if l > 1e-6 then p.fx, p.fy = dx / l, dy / l end
    p.mv = nil
  end
  if not st then return end
  if st.op == "go" then
    if f >= 1 and st.enter then p.inside = st.enter end
  elseif st.op == "work" then
    local h = WORK[st.kind]
    if h then h(W, s, p, st, f) end
    if TOOL_WORK[st.kind] then
      -- the household's tools wear with the work
      local hh = W.S.v3.households[p.hh]
      if hh then hh.tools = max(0.15, (hh.tools or 1) - C().tool_wear * st.dur * f) end
    end
  elseif st.op == "pick" then
    local h = PICK[st.what]
    if h and f >= 1 then h(W, s, p, st) end
  elseif st.op == "drop" then
    if f >= 1 then DROP.any(W, s, p, st) end
    if st.release then
      local hh = W.S.v3.households[p.hh]
      if hh and hh[st.release] == p.id then hh[st.release] = 0 end
    end
  end
end
M.finish = finish

-- Start step i of p's plan at time now; returns false when it cannot start.
function M.start(W, s, p, now)
  local st = p.plan[p.pi]
  local h = START[st.op]
  return h(W, s, p, st, now)
end

return M
