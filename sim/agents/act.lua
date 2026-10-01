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
local kin = require("sim.agents.kin")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local IDLE_KINDS = { sit = true, talk = true, rest = true, eat = true, lunch = true, play = true, wait = true,
  nurse = true, idle = true, shelter = true }
local SKILL_OF = { till = "sk_farm", sow = "sk_farm", harvest = "sk_farm", tend = "sk_farm", thresh = "sk_farm",
  chop = "sk_wood", grub = "sk_wood", build = "sk_build", repair = "sk_build", fish = "sk_fish", mend_boat = "sk_fish" }
local TOOL_WORK = { till = true, harvest = true, chop = true, grub = true, build = true, repair = true, thresh = true, tend = true,
  mend_boat = true }
M.OUTDOOR_WORK = { grub = true, forage = true, till = true, thresh = true, sow = true, tend = true, harvest = true, chop = true, build = true, fill = true, play = true, repair = true,
  mend_boat = true }

-- Position of p at time t (analytic; exact at t0 and t1).
local function rng_float(W, p, k)
  local rng = require("sim.rng")
  local now = W.S.v3.now or W.hour
  return rng.float(W.seed, rng.S.V3_THINK, p.key, floor(now * 60) % 4294967296, k)
end

-- (people by id, for a mate riding in a skipper's boat; ending a step now: set by
-- sim/agents/init.lua)
M.people = nil
M.skip_step = function() end

function M.pos(p, t)
  local r = p.riding
  if r and r ~= 0 and M.people then
    local q = M.people[r]
    if q and q.alive and q ~= p and not q.riding then
      local x, y, dx, dy = M.pos(q, t)
      return x - (dx or 0) * 0.0, y + 0.006, dx, dy
    end
  end
  local mv = p.mv
  if not mv then return p.x, p.y, p.fx, p.fy end
  local d = (t - p.t0) * p.spd
  local x, y, dx, dy = nav.at(mv, d)
  return x, y, dx, dy
end

-- Body: hunger, energy and health over dt hours of an action of kind `kind`.
local function body(p, dt, kind)
  if dt <= 0 or kind == "carried" then return end   -- (an infant's hunger: sim/agents/kin.lua)
  local c = C()
  local h, e = p.hunger, p.energy
  if kind == "sleep" then
    e = e + c.energy_sleep * dt
    h = h + c.hunger_sleep * dt
  elseif kind == "travel" then
    -- (on the road: walking with rests, sleeping at the wayside; D-097)
    h = h + c.hunger_idle * dt
  elseif kind == "walk" then
    e = e - c.energy_walk * dt * (p.carry ~= "" and 1.4 or 1)
    h = h + c.hunger_idle * dt
  elseif IDLE_KINDS[kind] then
    e = e - c.energy_idle * dt
    if kind == "rest" or kind == "sit" or kind == "shelter" then e = e + 0.05 * dt end
    h = h + c.hunger_idle * dt
  else
    e = e - c.energy_work * dt * (p.elder and c.elder_tire or 1)
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
  if st.water then
    -- rowing over the water (D-084)
    local r = nav.path(s, p.x, p.y, st.x, st.y, 0, path, true)
    if not r then return false end
    p.mv = r
    p.spd = C().row_speed
    p.dest = st.place or ""
    begin(p, now, "row", r.len / p.spd)
    return true
  end
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
  if s.has_river then
    -- (D-095) a trip whose straight way crosses the river: it wades a ford or goes round to one;
    -- the village remembers where the crossings are wanted and what they cost
    local a, b = mapm.tile_of(s, p.x, p.y), mapm.tile_of(s, tx, ty)
    local w = (a > 0 and b > 0) and mapm.first_wet(s, a, b) or 0
    if w > 0 then
      -- (what the crossing costs this trip: the share of the way it goes round, and wading if
      -- the way walked crosses unbridged water; a trip over a bridge costs nothing)
      local straight = nav.dist(p.x, p.y, tx, ty)
      local extra = straight > 1e-6 and max(0, r.len / straight - 1) or 0
      local wades = false
      for k = 2, r.n do
        local ta, tb = mapm.tile_of(s, r.x[k - 1], r.y[k - 1]), mapm.tile_of(s, r.x[k], r.y[k])
        if ta > 0 and tb > 0 and mapm.first_wet(s, ta, tb) > 0 then wades = true; break end
      end
      local cost = 3 * extra + (wades and 1 or 0)
      if cost > 0.05 then
        local cr = s.cross
        if not cr then cr = {}; s.cross = cr end
        cr[w] = (cr[w] or 0) + cost
      end
      s.acc.crossings = (s.acc.crossings or 0) + 1
      if wades then s.acc.wades = (s.acc.wades or 0) + 1 end
    end
  end
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
    or st.kind == "visit" or st.kind == "cook" or st.kind == "feed_babe" or st.kind == "craft" or st.kind == "hand"
    or st.kind == "smoke" or st.kind == "weave" or st.kind == "sew" or st.kind == "spin" or st.kind == "dress"
    or st.kind == "mend_clothes" then
    if st.inside and st.inside ~= 0 then p.inside = st.inside end
  end
  -- a mother home again takes her baby back from its minder (D-081)
  if p.inside ~= 0 and p.babe == 0 and kin.HOME_KINDS[st.kind] then kin.reclaim(W, s, p) end
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
    if pl.crop == "flax" then
      -- flax is pulled by the roots and laid out on the field to ret (D-088)
      local y = C().flax_row * pl.quality * (pl.spoil or 1)
      pl.flax_ret = (pl.flax_ret or 0) + y
      s.acc.flax = (s.acc.flax or 0) + y
    else
      local y = C().row_yield * pl.quality * (pl.spoil or 1)
      pl.sheaves = pl.sheaves + y
      s.acc.harvest = (s.acc.harvest or 0) + y
    end
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
    if tr.prog[k] < 1 then tr.claim[k] = 0; s.tv = (s.tv or 0) + 1; people.practice(p, "sk_wood", st.dur * f); return end
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
  s.tv = (s.tv or 0) + 1
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

-- Feeding an infant (D-081): at the breast, or a bowl of porridge from the home's meals.
WORK.nurse = function(W, s, p, st, f)
  local ib = W.S.v3.people[st.ref or 0]
  if not ib or not ib.alive or f < 0.3 then return end
  ib.hunger = max(0.03, ib.hunger - 0.95 * f)
  ib.called = nil
  p.hunger = min(1, p.hunger + 0.04 * f)   -- nursing makes the mother hungry
end

WORK.feed_babe = function(W, s, p, st, f)
  local ib = W.S.v3.people[st.ref or 0]
  local b = s.buildings[p.inside or 0]
  if not ib or not ib.alive or not b or f < 0.3 then return end
  local c = C()
  local m = min(b.stock.meals or 0, c.porridge)
  if m <= 0 then return end
  b.stock.meals = b.stock.meals - m
  s.acc.eaten = (s.acc.eaten or 0) + m
  ib.hunger = max(0.03, ib.hunger - 0.85 * m / c.porridge)
  ib.called = nil
end

-- Handing a baby to its minder (D-081).
WORK.hand = function(W, s, p, st, f)
  local V = W.S.v3
  local ib, q = V.people[st.babe or 0], V.people[st.ref or 0]
  if not ib or not ib.alive or not q or not q.alive or p.babe ~= ib.id then return end
  if q.babe ~= 0 or q.act == "sleep" then return end
  if not (q.inside == p.inside or (q.inside == 0 and (q.x - p.x) ^ 2 + (q.y - p.y) ^ 2 < 0.004)) then return end
  if kin.give(W, ib, q) then
    people.remember(q, W.S.v3.now or W.hour, "Minding " .. ib.name .. " for " .. p.name)
    if M.interrupt_hook then M.interrupt_hook(W, q, (W.S.v3.now or W.hour) + 0.02, "Minding " .. ib.name) end
  end
end

-- Boats (D-084): boarding at the dock, the catch from the planet's fish, landing.
WORK.board = function(W, s, p, st, f)
  local boat = s.boats and s.boats[st.boat]
  local b = s.dock
  if not boat or not b then return end
  boat.crew = p.id
  boat.t_out = W.S.v3.now or W.hour
  p.boat = boat.id
  p.x, p.y = b.mx, b.my
end

WORK.wait_mate = function(W, s, p, st, f)
  -- (nobody came: out alone with the lines; a mate who comes later finds the boat gone)
  local boat = s.boats and s.boats[st.boat or 0]
  if boat and boat.mate == 0 then boat.mate_want = 0 end
end

WORK.board_mate = function(W, s, p, st, f)
  local boat = s.boats and s.boats[st.boat or 0]
  local q = W.S.v3.people[st.skipper or 0]
  if not boat or not q or not q.alive or boat.mate ~= p.id or q.objk ~= "fish" then
    if boat and boat.mate == p.id then boat.mate = 0 end
    return
  end
  p.riding, p.ride_boat = q.id, boat.id
  boat.mate_want = 0
  -- the skipper stops waiting and rows out
  if q.act == "wait_mate" then M.skip_step(W, q) end
end

WORK.ride = function(W, s, p, st, f) end

WORK.fish = function(W, s, p, st, f)
  local c = C()
  local i = mapm.tile_of(s, p.x, p.y)
  if i == 0 or s.land[i] ~= 0 then return end
  local cell = s.cell_of[i]
  local F = W.F
  local fd = F.fish[cell] or 0
  -- (a skilled fisher knows the grounds and the lines: D-087; two in the boat work the net, one
  -- alone only the lines: D-096)
  local boat = s.boats and s.boats[st.boat or 0]
  local mate = boat and boat.mate ~= 0 and W.S.v3.people[boat.mate]
  local two = mate and mate.alive and mate.riding == p.id
  local gear = two and c.net_k or c.line_k
  local catch = c.fish_q * fd * st.dur * f * gear * (0.55 + 0.6 * (p.sk_fish or 0.2)) * (0.85 + 0.3 * (p.dil - 0.6) / 0.8)
  -- weather at sea: a squall while they were out tears the gear and the planks; now and then it
  -- swamps the boat and the catch goes over the side
  if (s.rain_now or 0) >= 2 and boat then
    boat.cond = max(0, (boat.cond or 1) - c.squall_wear)
    local v = rng_float(W, p, 82)
    if v < c.squall_swamp then
      catch = 0
      s.acc.swamped = (s.acc.swamped or 0) + 1
      people.remember(p, W.S.v3.now or W.hour, "A squall swamped the boat: the catch went over the side")
    end
  end
  if two then people.practice(mate, "sk_fish", st.dur * f * 0.5) end
  local stock = fd * c.fish_fu
  if catch > stock * 0.05 then catch = stock * 0.05 end
  if catch <= 0 then return end
  F.fish[cell] = fd - catch / c.fish_fu
  p.carry, p.camt = "fish", (p.carry == "fish" and p.camt or 0) + catch
  s.acc.fished = (s.acc.fished or 0) + catch
end

WORK.land = function(W, s, p, st, f)
  local boat = s.boats and s.boats[p.boat or st.boat or 0]
  local b = s.dock
  if boat and (boat.mate or 0) ~= 0 then
    -- the mate steps ashore with their share (half to another household's hand, D-096)
    local q = W.S.v3.people[boat.mate]
    if q and q.alive and q.riding == p.id then
      q.riding, q.ride_boat = nil, nil
      q.x, q.y = b.dx + 0.01, b.dy
      if q.hh ~= p.hh and p.carry == "fish" and p.camt > 0 then
        local share = p.camt * 0.5
        p.camt = p.camt - share
        q.carry, q.camt = "fish", share
      end
      M.skip_step(W, q)
    end
    boat.mate = 0
  end
  if boat then
    boat.crew = 0
    boat.x, boat.y = b.mx + (boat.id - 1) * 0.012, b.my
    -- hours afloat wear the hull and the oars
    local out = (W.S.v3.now or W.hour) - (boat.t_out or (W.S.v3.now or W.hour))
    boat.cond = max(0, (boat.cond or 1) - C().boat_wear * out)
    boat.trips = (boat.trips or 0) + 1
  end
  p.boat = nil
  if b then p.x, p.y = b.dx, b.dy end
end

-- Mending a boat at the dock (D-087): with the log brought, hours of caulking and new boards.
WORK.mend_boat = function(W, s, p, st, f)
  local boat = s.boats and s.boats[st.ref or 0]
  if not boat then return end
  if boat.mender == p.id then boat.mender = 0 end
  if (boat.repair_logs or 0) < 1 - 1e-6 then return end
  local c = C()
  boat.mend_p = (boat.mend_p or 0) + st.dur * f / c.mend_boat_h
  if boat.mend_p >= 0.999 then
    boat.mend_p = 0
    boat.cond = 1
    boat.repair_logs = boat.repair_logs - 1
    s.acc.boats_mended = (s.acc.boats_mended or 0) + 1
  end
end

-- Smoking fish over the hearth (D-087): fresh fish that would spoil becomes smoked fish that
-- keeps through the winter; the fire takes wood.
WORK.smoke = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.smoking == p.id then hh.smoking = 0 end
  local b = s.buildings[st.ref or 0]
  if not b then return end
  local c = C()
  local fresh = (b.stock.fish or 0) + (b.stock.meat or 0)
  local amt = min(fresh, c.smoke_fu_h * st.dur * f)
  local logs = b.stock.logs or 0
  if logs < amt * c.smoke_logs then amt = logs / c.smoke_logs end
  if amt <= 0 then return end
  local mt = min(b.stock.meat or 0, amt)
  b.stock.meat = (b.stock.meat or 0) - mt
  b.stock.fish = (b.stock.fish or 0) - (amt - mt)
  b.stock.smoked = (b.stock.smoked or 0) + amt
  b.stock.logs = logs - amt * c.smoke_logs
  s.acc.smoked = (s.acc.smoked or 0) + amt
  if hh and amt > mt then hh.fish_spare = (hh.fish_spare or 0) + amt - mt end
end

-- Textiles (D-088): flax dressed into fibre, spun into yarn, woven into cloth, sewn into clothes.
-- Rates in garments' worth (GE) an hour, with skill.
local function craft_rate(p, h) return (0.6 + 0.6 * (p.sk_craft or 0.2)) / h end

WORK.dress = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.dressing == p.id then hh.dressing = 0 end
  local b = s.buildings[st.ref or 0]
  if not b then return end
  -- (raw wool is washed and carded first, else flax broken and hackled, D-089)
  local rate = st.dur * f * craft_rate(p, C().dress_h)
  local wl = min(b.stock.wool or 0, rate)
  local amt = min(b.stock.flax or 0, rate - wl)
  if amt + wl <= 0 then return end
  b.stock.wool = (b.stock.wool or 0) - wl
  b.stock.flax = (b.stock.flax or 0) - amt
  amt = amt + wl
  b.stock.fibre = (b.stock.fibre or 0) + amt
  people.practice(p, "sk_craft", st.dur * f)
end

WORK.spin = function(W, s, p, st, f)
  local b = s.buildings[st.ref or 0]
  if not b then return end
  local amt = min(b.stock.fibre or 0, st.dur * f * craft_rate(p, C().spin_h))
  if amt > 0 then
    b.stock.fibre = b.stock.fibre - amt
    b.stock.yarn = (b.stock.yarn or 0) + amt
    s.acc.spun = (s.acc.spun or 0) + amt
  end
  people.practice(p, "sk_craft", st.dur * f)
  if p.inside == 0 then
    -- spinning in company: the neighbours spinning and talking beside them
    local V = W.S.v3
    for i = 1, #s.members do
      local q = V.people[s.members[i]]
      if q and q ~= p and q.alive and q.inside == 0 and (q.act == "spin" or q.act == "talk") then
        local dx, dy = q.x - p.x, q.y - p.y
        if dx * dx + dy * dy < 0.05 then people.bond(p, q, 0.015 * f) end
      end
    end
  end
end

WORK.weave = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.weaving == p.id then hh.weaving = 0 end
  local b = s.buildings[st.ref or 0]
  if not b then return end
  local amt = min(b.stock.yarn or 0, st.dur * f * craft_rate(p, C().weave_h))
  if amt <= 0 then return end
  b.stock.yarn = b.stock.yarn - amt
  b.stock.cloth = (b.stock.cloth or 0) + amt
  s.acc.woven = (s.acc.woven or 0) + amt
  people.practice(p, "sk_craft", st.dur * f)
end

-- A garment sewn for one of the household (st.who): done when the work adds up to sew_h.
WORK.sew = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.sewing == p.id then hh.sewing = 0 end
  local b = s.buildings[st.ref or 0]
  if not b or not hh then return end
  hh.sew_p = (hh.sew_p or 0) + st.dur * f * craft_rate(p, C().sew_h)
  people.practice(p, "sk_craft", st.dur * f)
  if hh.sew_p >= 0.999 and (b.stock.cloth or 0) >= 1 - 1e-6 then
    hh.sew_p = 0
    b.stock.cloth = b.stock.cloth - 1
    local V = W.S.v3
    local q = V.people[st.who or 0]
    if not (q and q.alive and q.hh == hh.id) then q = p end
    q.clothes = 1
    s.acc.garments = (s.acc.garments or 0) + 1
    people.remember(q, W.S.v3.now or W.hour, q == p and "Sewed new clothes" or ("New clothes, sewn by " .. p.name))
  end
end

WORK.mend_clothes = function(W, s, p, st, f)
  local b = s.buildings[st.ref or 0]
  if not b then return end
  local c = C()
  local y = min(b.stock.yarn or 0, c.mend_clothes_yarn * f)
  if y <= 0 then return end
  b.stock.yarn = b.stock.yarn - y
  p.clothes = min(1, (p.clothes or 1) + 0.3 * f * y / c.mend_clothes_yarn)
end

-- Livestock (D-089, D-092): a pen opened and its herd driven out to the day's ground, watched,
-- brought home; dams milked, sheep shorn, beasts killed; hay mown and racked; dung spread on a
-- field; slops in the pigs' trough; cheese made at home.
local stock = require("sim.agents.stock")
local function herd_of(s, st) return s.herds and s.herds[stock.HERDS[st.herd or 1]] end

WORK.open_pen = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if not H then return end
  H.out, H.herder = true, p.id
  H.spot_x, H.spot_y = st.sx, st.sy
end

WORK.herd = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if H and H.herder == p.id then H.spot_x, H.spot_y = p.x, p.y end
end

WORK.close_pen = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if not H then return end
  H.out, H.herder = false, 0
end

-- (the animal worked on is let go)
local function release_animal(s, p, st)
  local H = herd_of(s, st)
  local a = H and H.animals[st.ref or 0]
  if a and a.held == p.id then a.held = 0 end
  return H, a
end

WORK.milk = function(W, s, p, st, f)
  local H, a = release_animal(s, p, st)
  if not a or not a.alive or f < 0.5 then return end
  local m = (a.milk or 0) * min(1, f)
  if m <= 0 then return end
  a.milk = a.milk - m
  p.carry, p.camt = "milk", (p.carry == "milk" and p.camt or 0) + m
  s.acc.milk = (s.acc.milk or 0) + m
end

WORK.shear = function(W, s, p, st, f)
  local H, a = release_animal(s, p, st)
  if not a or f < 0.8 or not a.alive then return end
  local w = a.wool or 0
  if w <= 0 then return end
  a.wool = 0
  p.carry, p.camt = "wool", (p.carry == "wool" and p.camt or 0) + w
  s.acc.wool = (s.acc.wool or 0) + w
  people.practice(p, "sk_craft", st.dur * f)
end

WORK.mow = function(W, s, p, st, f)
  local P = s.pasture
  if not P then return end
  local c = C()
  local b = st.block or 0
  local g = P.grass[b] or 0
  local cut = min(g / c.grass_per_feed * 0.5, c.mow_rate * st.dur * f * g)
  if cut <= 0 then return end
  P.grass[b] = max(0, g - cut * c.grass_per_feed * 0.5)
  p.carry, p.camt = "hay", (p.carry == "hay" and p.camt or 0) + cut
  s.acc.hay = (s.acc.hay or 0) + cut
end

-- Killing a beast (D-089, D-092): the meat goes home fresh (cooked first, smoked if it will not
-- be eaten in time), a sheep's fleece to the household's wool.
WORK.slaughter = function(W, s, p, st, f)
  local H, a = release_animal(s, p, st)
  if not a or f < 0.8 or not a.alive then return end
  local now = W.S.v3.now or W.hour
  a.alive, a.died, a.cull, a.cause = false, now, nil, "killed"
  local S = stock.SP(a.sp)
  local meat = S.meat * min(1, 0.4 + (now - a.born) / 1152 / S.adult_years) * (0.5 + 0.5 * a.health)
  p.carry, p.camt = "meat", meat
  s.acc.meat = (s.acc.meat or 0) + meat
  s.acc.slaughtered = (s.acc.slaughtered or 0) + 1
  s.acc["killed_" .. a.sp] = (s.acc["killed_" .. a.sp] or 0) + 1
  local home = W.S.v3.households[a.owner]
  if home and (a.wool or 0) > 0.2 then
    local hb = s.buildings[home.home]
    if hb then hb.stock.wool = (hb.stock.wool or 0) + a.wool * 0.8 end
  end
end

WORK.feed = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if not H or f < 0.5 then return end
  local amt = min(s.hay or 0, st.amt or 0)
  s.hay = (s.hay or 0) - amt
  H.rack = (H.rack or 0) + amt
end

-- Dung spread along a row: the field's soil the richer for it.
WORK.spread = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if H and H.muck_claim == p.id then H.muck_claim = 0 end
  if p.carry ~= "manure" then return end
  local pl = s.plots[st.ref or 0]
  local c = C()
  local n = p.camt * min(1, f)
  if pl and n > 0 then
    pl.fert = min(c.fert_max, (pl.fert or 1) + n * c.manure_fert)
    pl.mucked = (pl.mucked or 0) + n
    s.acc.mucked = (s.acc.mucked or 0) + n
  end
  p.camt = p.camt - n
  if p.camt <= 1e-6 then p.carry, p.camt = "", 0 end
end

-- Slops in the pigs' trough.
WORK.slop = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.slopping == p.id then hh.slopping = 0 end
  local H = s.herds and s.herds.swine
  if not H or p.carry ~= "scraps" then return end
  H.rack = (H.rack or 0) + p.camt * C().slop_feed
  s.acc.slops = (s.acc.slops or 0) + p.camt
  p.carry, p.camt = "", 0
end

-- Cheese: the milk curdled, the curd pressed; the whey goes to the pigs with the scraps.
WORK.cheese = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.cheesing == p.id then hh.cheesing = 0 end
  local b = s.buildings[st.ref or 0]
  if not b or f < 0.5 then return end
  local c = C()
  local m = (b.stock.milk or 0) * min(1, f)
  if m <= 0 then return end
  b.stock.milk = b.stock.milk - m
  b.stock.cheese = (b.stock.cheese or 0) + m * c.cheese_keep
  b.stock.scraps = (b.stock.scraps or 0) + m * (1 - c.cheese_keep)
  s.acc.whey = (s.acc.whey or 0) + m * (1 - c.cheese_keep)
  s.acc.cheese = (s.acc.cheese or 0) + m * c.cheese_keep
end

-- The hunt (D-094): at the end of the creep, a spear thrown if the quarry is still in range (the
-- odds by skill and by whether it is running); a hit kills it where it stands (its herd loses
-- it); a miss, or a quarry already off, sets it running.
WORK.stalk = function(W, s, p, st, f)
  local H = p.hunting
  if not H or f < 0.9 then return end
  local wild = require("sim.agents.wild")
  local w = wild.byk(s, st.ref or 0)
  if not w or not w.alive then return end
  local c = C()
  local now = W.S.v3.now or W.hour
  local qx, qy = wild.pos(w, now)
  local d = sqrt((qx - p.x) ^ 2 + (qy - p.y) ^ 2)
  p.sk_hunt = p.sk_hunt or 0.2
  people.practice(p, "sk_hunt", st.dur * 0.5)
  if d <= c.hunt_throw * 1.4 then
    local hit = c.hunt_hit + c.hunt_skill * (p.sk_hunt or 0.2) - (w.act == "flee" and 0.25 or 0)
    local rng = require("sim.rng")
    local v = rng.float(W.seed, rng.S.V3_THINK, p.key, floor(now * 60) % 4294967296, 81)
    s.acc.spears = (s.acc.spears or 0) + 1
    if v < hit then
      local cc = wild.shot(W, s, w, now, p.id)
      H.carcass = { x = cc.x, y = cc.y, meat = cc.meat, hide = cc.hide }
      people.practice(p, "sk_hunt", 1)
      people.remember(p, now, "Speared a " .. w.sp .. " at the woods' edge")
      return
    end
  end
  H.tries = H.tries + 1
  w.flee_x, w.flee_y = p.x, p.y
  wild.interrupt(W, s, w, now)
end

-- On the road between settlements (D-097): at the leg's end the person is on the other map, at
-- its gate toward where they came from.
WORK.travel = function(W, s, p, st, f)
  if f < 0.99 then return end
  local V = W.S.v3
  local o = V.settlements[st.to or 0]
  local from = V.settlements[st.from or 0]
  if not o or not from then return end
  local gx, gy = require("sim.agents.trade").gate(W, o, from)
  local old = V.settlements[p.at or p.sett]
  if old and old.visitors then people.remove_id(old.visitors, p.id) end
  if o.id == p.sett then
    p.at = nil
  else
    p.at = o.id
    o.visitors = o.visitors or {}
    o.visitors[#o.visitors + 1] = p.id
  end
  p.x, p.y = gx, gy
  p.inside = 0
  s.acc.road_h = (s.acc.road_h or 0) + st.dur
end

-- A guest's meal (D-097): the host village feeds a traveller from its granary (its own ledger).
WORK.guest_meal = function(W, s, p, st, f)
  local g = s.buildings[st.ref or 0]
  if not g or f < 0.5 then return end
  local c = C()
  local m = min(g.stock.grain or 0, c.meal_fu * 2)
  if m <= 0 then return end
  g.stock.grain = g.stock.grain - m
  s.acc.eaten = (s.acc.eaten or 0) + m
  s.acc.guests = (s.acc.guests or 0) + m
  p.hunger = max(0, p.hunger - c.meal * 2)
end

-- Fish hung on the rack (D-096).
WORK.hang = function(W, s, p, st, f)
  local hh = W.S.v3.households[p.hh]
  if hh and hh.smoking == p.id then hh.smoking = 0 end
  local rk = s.buildings[st.ref or 0]
  if not rk or p.carry ~= "fish" then return end
  rk.stock.drying = (rk.stock.drying or 0) + p.camt
  s.acc.hung = (s.acc.hung or 0) + p.camt
  if hh then hh.fish_spare = (hh.fish_spare or 0) + p.camt end
  p.carry, p.camt = "", 0
end

-- At the kill: gutted, slung over the shoulders (the meat counts from here).
WORK.gut = function(W, s, p, st, f)
  local H = p.hunting
  if not H or not H.carcass or f < 0.5 then return end
  local cc = H.carcass
  p.carry, p.camt = "carcass", cc.meat
  p.hide = cc.hide
  s.acc.meat = (s.acc.meat or 0) + cc.meat
  s.acc.hunt_meat = (s.acc.hunt_meat or 0) + cc.meat
  -- (the carcass on the ground is gone with them)
  local Wd = s.wildlife
  for i = #Wd.carcasses, 1, -1 do
    local q = Wd.carcasses[i]
    if q.by == p.id and math.abs(q.x - cc.x) < 1e-6 and math.abs(q.y - cc.y) < 1e-6 then table.remove(Wd.carcasses, i) end
  end
  p.hunting = nil
end

-- Angling from the bank (D-095): fish from the river's own stock, by skill and the hours.
WORK.angle = function(W, s, p, st, f)
  local c = C()
  local stock_ = s.river_fish or 0
  local cap = max(1, s.river_fish_cap or 1)
  local got = min(stock_, c.angle_rate * st.dur * f * (0.4 + 0.8 * (p.sk_fish or 0.2)) * (stock_ / cap))
  p.sk_fish = p.sk_fish or 0.2
  people.practice(p, "sk_fish", st.dur * f * 0.5)
  if got <= 0 then return end
  s.river_fish = stock_ - got
  p.carry, p.camt = "fish", (p.carry == "fish" and p.camt or 0) + got
  s.acc.fished = (s.acc.fished or 0) + got
  s.acc.angled = (s.acc.angled or 0) + got
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
  s.tv = (s.tv or 0) + 1
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
  -- (what will not keep goes into the pot first: meat, milk, fresh fish (D-087, D-092); cheese
  -- and smoked fish last)
  local want = n * c.cook_batch * f
  local meat = min(b.stock.meat or 0, want)
  b.stock.meat = (b.stock.meat or 0) - meat
  want = want - meat
  local milk = min(b.stock.milk or 0, want)
  b.stock.milk = (b.stock.milk or 0) - milk
  want = want - milk
  local fish = min(b.stock.fish or 0, want)
  local amt = min(b.stock.food or 0, want - fish)
  local sm = min(b.stock.smoked or 0, want - fish - amt)
  local ch = min(b.stock.cheese or 0, want - fish - amt - sm)
  if amt + fish + sm + ch + meat + milk <= 0 then return end
  b.stock.fish = (b.stock.fish or 0) - fish
  b.stock.food = (b.stock.food or 0) - amt
  b.stock.smoked = (b.stock.smoked or 0) - sm
  b.stock.cheese = (b.stock.cheese or 0) - ch
  amt = amt + fish + sm + meat + milk + ch
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
  if st.bought then
    -- (D-097) the price of the fish just sold: grain from the buyer's granary
    local tr = p.trip
    local want = tr and (tr.sold or 0) * (tr.rate or c.trade_rate) or 0
    local got = min(want, b.stock.grain or 0)
    if got > 0 then
      b.stock.grain = b.stock.grain - got
      p.carry, p.camt = "grain", got
      s.acc.export = (s.acc.export or 0) + got
      local home = W.S.v3.settlements[p.sett]
      home.acc.import = (home.acc.import or 0) + got
      s.acc.traded_grain = (s.acc.traded_grain or 0) + got
    end
    return
  end
  local want = st.n or c.carry_grain
  -- (smoked fish bought into the granary goes out with the grain, in proportion: D-097)
  local gr, sm = b.stock.grain or 0, b.stock.smoked or 0
  local fish = (st.as == "food" and sm > 0) and min(sm, want * sm / (gr + sm)) or 0
  local got = min(want - fish, gr)
  if got + fish > 0 then
    b.stock.grain = gr - got
    b.stock.smoked = sm - fish
    p.carry, p.camt = st.as or "grain", got + fish
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

PICK.flax = function(W, s, p, st)
  local c = C()
  if st.from == "plot" then
    local pl = s.plots[st.ref]
    if not pl then return end
    pl.flax_claim = max(0, (pl.flax_claim or 1) - 1)
    local got = min(c.carry_flax, pl.flax or 0)
    if got > 0 then pl.flax = pl.flax - got; p.carry, p.camt = "flax", got end
  else
    local b = s.buildings[st.ref]
    if not b then return end
    local got = min(st.n or 2, b.stock.flax or 0)
    if got > 0 then b.stock.flax = b.stock.flax - got; p.carry, p.camt = "flax", got end
  end
end

PICK.logs = function(W, s, p, st)
  local want = st.n or (p.strong and 2 or 1)
  if st.from == "tree" then
    local tr = s.trees
    local k = st.ref
    if tr.claim[k] == -p.id then tr.claim[k] = 0 end
    s.tv = (s.tv or 0) + 1
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

-- Food for the road (D-097): meals and grain from home.
PICK.provisions = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local want = st.n or C().trade_prov
  local ml = min(want, b.stock.meals or 0)
  b.stock.meals = (b.stock.meals or 0) - ml
  local fd = min(want - ml, b.stock.food or 0)
  b.stock.food = (b.stock.food or 0) - fd
  p.prov = (p.prov or 0) + ml + fd
end

PICK.smoked = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = min(st.n or 2, b.stock.smoked or 0)
  if got > 0 then
    b.stock.smoked = b.stock.smoked - got
    p.carry, p.camt = "smoked", got
  end
end

PICK.fish = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = min(st.n or 2, b.stock.fish or 0)
  if got > 0 then
    b.stock.fish = b.stock.fish - got
    p.carry, p.camt = "fish", got
  end
end

PICK.dried = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = b.stock.dried or 0
  if got > 0 then
    b.stock.dried = 0
    p.carry, p.camt = "dried", got
  end
end

PICK.manure = function(W, s, p, st)
  local H = s.herds and s.herds[stock.HERDS[st.herd or 1]]
  if not H then return end
  local got = min(st.n or 1, H.manure or 0)
  if got > 0 then
    H.manure = H.manure - got
    p.carry, p.camt = "manure", got
  end
end

PICK.scraps = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = b.stock.scraps or 0
  if got > 0 then
    b.stock.scraps = 0
    p.carry, p.camt = "scraps", got
  end
end

DROP.any = function(W, s, p, st)
  if p.carry == "" then return end
  local what, n = p.carry, p.camt
  if st.to == "boat" and what == "logs" then
    local boat = s.boats and s.boats[st.boat or 0]
    if boat then boat.repair_logs = (boat.repair_logs or 0) + n; p.carry, p.camt = "", 0 end
    return
  end
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
  elseif what == "flax" then
    b.stock.flax = (b.stock.flax or 0) + n
  elseif what == "wool" then
    b.stock.wool = (b.stock.wool or 0) + n
  elseif what == "meat" then
    b.stock.meat = (b.stock.meat or 0) + n
  elseif what == "hay" then
    s.hay = (s.hay or 0) + n
  elseif what == "milk" then
    b.stock.milk = (b.stock.milk or 0) + n
  elseif what == "dried" then
    b.stock.smoked = (b.stock.smoked or 0) + n
  elseif what == "smoked" then
    b.stock.smoked = (b.stock.smoked or 0) + n
    if st.sold and p.trip then
      -- (D-097) sold to the buyer's village: out of the seller's ledger, into the buyer's
      local home = W.S.v3.settlements[p.sett]
      home.acc.export = (home.acc.export or 0) + n
      s.acc.import = (s.acc.import or 0) + n
      s.acc.traded_fish = (s.acc.traded_fish or 0) + n
      local _, rate = require("sim.agents.trade").buyer(W, home)
      p.trip.sold, p.trip.rate = n, rate or C().trade_rate
    end
  elseif what == "carcass" then
    -- (butchered at home: the meat to the larder, the hide kept)
    b.stock.meat = (b.stock.meat or 0) + n
    b.stock.hides = (b.stock.hides or 0) + (p.hide or 1)
    p.hide = nil
  elseif what == "scraps" then
    b.stock.scraps = (b.stock.scraps or 0) + n
  elseif what == "manure" then
    for _, kind in ipairs(stock.HERDS) do
      local H = s.herds and s.herds[kind]
      if H and H.pen == b.id then H.manure = (H.manure or 0) + n end
    end
  elseif what == "seed" then
    if b.kind == "granary" then b.stock.grain = (b.stock.grain or 0) + n else b.stock.seed = (b.stock.seed or 0) + n end
  elseif what == "grain" then
    if b.kind == "granary" then
      b.stock.grain = (b.stock.grain or 0) + n
    else
      b.stock.food = (b.stock.food or 0) + n
    end
  elseif what == "fish" then
    -- fresh fish: a house's own stock, cooked first, spoiling within days (D-087)
    b.stock.fish = (b.stock.fish or 0) + n
  elseif what == "food" then
    if b.kind == "granary" then b.stock.grain = (b.stock.grain or 0) + n else b.stock.food = (b.stock.food or 0) + n end
  elseif what == "logs" then
    b.stock.logs = (b.stock.logs or 0) + n
  elseif what == "water" then
    b.stock.water = (b.stock.water or 0) + n
  end
  p.carry, p.camt = "", 0
  if st.trip_end and (p.prov or 0) > 0 then
    -- (what is left of the provisions goes back in the larder)
    b.stock.food = (b.stock.food or 0) + p.prov
    p.prov = 0
  end
  if st.trip_end and p.trip then
    local hh = W.S.v3.households[p.hh]
    if hh and hh.trading == p.id then hh.trading = 0 end
    local o = W.S.v3.settlements[p.trip.to]
    people.remember(p, W.S.v3.now or W.hour, string.format("Traded %.0f FU of smoked fish for %.0f of grain at %s",
      p.trip.sold or 0, (p.trip.sold or 0) * (p.trip.rate or 0), o and o.name or "?"))
    p.trip = nil
    s.acc.trips = (s.acc.trips or 0) + 1
  end
end

-- Apply the effects of p's current step, done to fraction f (1 = complete), at time now.
local LEISURE = { rest = true, talk = true, visit = true, sit_out = true }
local function finish(W, s, p, now, f)
  local st = p.plan[p.pi]
  local dt = now - p.t0
  if p.act == "travel" and dt > 0 and (p.prov or 0) > 0 then
    -- the provisions from home, eaten along the road (D-097)
    local c = C()
    local want = max(0, p.hunger + c.hunger_idle * dt - 0.35) / c.meal * c.meal_fu
    local m = min(p.prov, want)
    if m > 0 then
      p.prov = p.prov - m
      p.hunger = max(0, p.hunger - m / c.meal_fu * c.meal)
      local hs = W.S.v3.settlements[p.sett]
      hs.acc.eaten = (hs.acc.eaten or 0) + m
    end
  end
  body(p, dt, p.act)
  if dt > 0 and p.act ~= "sleep" and p.act ~= "carried" and p.adult then
    -- (the village's measure of idle hands: adults' leisure hours of their waking hours)
    s.acc.aw_h = (s.acc.aw_h or 0) + dt
    if LEISURE[p.objk or ""] then s.acc.lz_h = (s.acc.lz_h or 0) + dt end
  end
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
    if p.mentored and SKILL_OF[st.kind] and f > 0 then people.practice(p, SKILL_OF[st.kind], st.dur * f * 0.6) end
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
