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
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local dsq = dmath.sq
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local nav = require("sim.agents.nav")
local mapm = require("sim.agents.map")
local people = require("sim.agents.people")
local econ = require("sim.agents.econ")
local ledger = require("sim.ledger")
local kin = require("sim.agents.kin")
local stock = require("sim.agents.stock")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local IDLE_KINDS = { sit = true, talk = true, rest = true, eat = true, lunch = true, play = true, wait = true,
  nurse = true, idle = true, shelter = true }
local SKILL_OF = { till = "sk_farm", sow = "sk_farm", harvest = "sk_farm", tend = "sk_farm", thresh = "sk_farm",
  chop = "sk_wood", grub = "sk_wood", build = "sk_build", repair = "sk_build", fish = "sk_fish", mend_boat = "sk_fish" }
local TOOL_WORK = { till = true, harvest = true, chop = true, grub = true, build = true, repair = true, thresh = true, tend = true,
  mend_boat = true }
M.OUTDOOR_WORK = { grub = true, forage = true, till = true, turn = true, thresh = true, sow = true, tend = true, harvest = true, chop = true, build = true, fill = true, play = true, repair = true,
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
  elseif kind == "travel" or kind == "voyage" or kind == "mind_boat" then
    -- (on the road: walking with rests, sleeping at the wayside; D-097; at sea the crew rows and
    -- rests by turns; minding a boat is waiting: D-102)
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
  elseif h < 0.6 and p.health < (p.diet_cap or 1) then
    p.health = min(p.diet_cap or 1, p.health + 0.004 * dt)
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
  if st.lead then
    -- (S15, R4) leading a drove: at the slowest beast's pace
    local L = stock.led_by(s, p, st.lead)
    local sp = 9
    for _, a in ipairs(L) do sp = min(sp, stock.SP(a.sp).speed) end
    if sp < 9 then p.spd = min(p.spd, sp * C().drove_pace) end
  end
  if st.drove then
    -- (S15) driving a herd out or home: at the beasts' pace, and they notice them setting off
    local H = s.herds and s.herds[stock.HERDS[st.drove]]
    if H then
      local sp = 9
      for _, a in ipairs(H.animals) do
        if a.alive then sp = min(sp, stock.SP(a.sp).speed) end
      end
      if sp < 9 then p.spd = min(p.spd, sp * C().drove_pace) end
      stock.rouse(W, s, H, now)
    end
  end
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
  if st.kind == "travel" and st.lead and p.trip then
    -- (S15, R4) taking the road with a drove: the beasts led leave this map with the drover
    stock.leave_map(W, s, p, st.lead)
    if econ.GOODS[p.trip.give] and econ.GOODS[p.trip.give].beast then
      p.pack = p.pack or {}
      p.pack[p.trip.give] = #(p.trip.party or {})
    end
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
  if st.ox then
    -- (the ox's hours at the plough today: D-108)
    local H = s.herds and s.herds.cattle
    local a = H and H.animals[st.ox]
    if a then a.worked = (a.worked or 0) + st.dur * f; s.acc.ox_h = (s.acc.ox_h or 0) + st.dur * f end
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
  if not (q.inside == p.inside or (q.inside == 0 and dsq(q.x - p.x) + dsq(q.y - p.y) < 0.004)) then return end
  if kin.give(W, ib, q) then
    people.remember(q, W.S.v3.now or W.hour, "Minding " .. ib.name .. " for " .. p.name)
    if M.interrupt_hook then M.interrupt_hook(W, q, (W.S.v3.now or W.hour) + 0.02, "Minding " .. ib.name) end
  end
end

-- Boats (D-084): boarding at the dock, the catch from the planet's fish, landing. A boat belongs to
-- its village (p.sett's boats) wherever it is moored (D-102: at another village's dock on a trade).
local function boats_of(W, p) return W.S.v3.settlements[p.sett].boats end
M.boats_of = boats_of
WORK.board = function(W, s, p, st, f)
  local B = boats_of(W, p)
  local boat = B and B[st.boat]
  local b = s.dock
  if not boat or not b then return end
  boat.crew = p.id
  boat.t_out = W.S.v3.now or W.hour
  p.boat = boat.id
  p.x, p.y = b.mx, b.my
  local tr = p.trip
  if tr and tr.sea then
    if (p.at or p.sett) == tr.from then tr.sailed = true else tr.homeward = true end
  end
end

WORK.wait_mate = function(W, s, p, st, f)
  -- (nobody came: out alone with the lines; a mate who comes later finds the boat gone)
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
  if boat and boat.mate == 0 then boat.mate_want = 0 end
end

WORK.board_mate = function(W, s, p, st, f)
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
  local q = W.S.v3.people[st.skipper or 0]
  if not boat or not q or not q.alive or boat.mate ~= p.id or (q.objk ~= "fish" and q.objk ~= "trade") then
    if boat and boat.mate == p.id then boat.mate = 0 end
    return
  end
  p.riding, p.ride_boat = q.id, boat.id
  boat.mate_want = 0
  local tr = q.trip
  if tr and tr.sea then
    -- (a trading voyage: the mate goes the whole way, fed from the skipper's provisions, D-102)
    p.trip = { mate = true, skipper = q.id, to = tr.to, from = tr.from, boat = boat.id }
    local half = (q.prov or 0) * 0.5
    q.prov, p.prov = (q.prov or 0) - half, (p.prov or 0) + half
  end
  -- the skipper stops waiting and rows out
  if q.act == "wait_mate" then M.skip_step(W, q) end
end

-- The skipper calls the mate minding the boat at the other village's dock aboard (D-102).
WORK.call_mate = function(W, s, p, st, f)
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
  local q = boat and W.S.v3.people[boat.mate or 0]
  if not q or not q.alive or not q.trip or q.trip.skipper ~= p.id then
    if boat then boat.mate = 0 end
    return
  end
  q.riding, q.ride_boat = p.id, boat.id
  q.x, q.y = p.x, p.y
  M.skip_step(W, q)
end

WORK.mind_boat = function(W, s, p, st, f) end

-- A voyage (D-102): the boat under sail and oars along the sea way between two villages; at its
-- end skipper and mate are on the other map, at the water's edge toward where they came from. A
-- squall may founder the boat on the way (the risk set when it sailed): the boat, its cargo and
-- both aboard are lost.
WORK.voyage = function(W, s, p, st, f)
  if f < 0.99 then return end
  local V = W.S.v3
  local o = V.settlements[st.to or 0]
  local from = V.settlements[st.from or 0]
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
  if not o or not from or not boat then return end
  local home = V.settlements[p.sett]
  local q = boat.mate ~= 0 and V.people[boat.mate] or nil
  if q and not (q.alive and q.riding == p.id) then q = nil end
  local now = V.now or W.hour
  home.acc.sea_h = (home.acc.sea_h or 0) + st.dur
  if rng_float(W, p, 91) < (st.risk or 0) then
    -- foundered: the boat, the cargo and the crew
    boat.lost, boat.owner, boat.crew, boat.mate, boat.away = true, -1, 0, 0, nil
    local food = 0
    for _, g in ipairs(econ.ORDER) do if econ.GOODS[g].food then food = food + (boat.cargo and boat.cargo[g] or 0) end end
    boat.cargo = nil
    home.acc.lost_sea = (home.acc.lost_sea or 0) + food + (p.prov or 0) + (q and q.prov or 0)
    home.acc.wrecks = (home.acc.wrecks or 0) + 1
    p.prov = 0
    if q then q.prov = 0 end
    local r = econ.rel(home, o)
    r.lost = r.lost + 1
    if M.drown_hook then
      if q then M.drown_hook(W, q, from.name, o.name) end
      M.drown_hook(W, p, from.name, o.name)
    end
    return
  end
  local gx, gy = require("sim.agents.trade").sea_gate(W, o, st.gate_c or o.cell)
  for _, who in ipairs({ p, q }) do
    if who then
      local old = V.settlements[who.at or who.sett]
      if old and old.visitors then people.remove_id(old.visitors, who.id) end
      if o.id == who.sett then
        who.at = nil
      else
        who.at = o.id
        o.visitors = o.visitors or {}
        o.visitors[#o.visitors + 1] = who.id
      end
      who.x, who.y = gx or 0, gy or 0
      who.inside = 0
    end
  end
  boat.away = (o.id ~= p.sett) and o.id or nil
  boat.x, boat.y = gx or 0, gy or 0
  if o.id == p.sett and p.trip then p.trip.back = true end
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
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
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
  local B = boats_of(W, p)
  local boat = B and B[p.boat or st.boat or 0]
  local b = s.dock
  if boat and (boat.mate or 0) ~= 0 then
    -- the mate steps ashore with their share (half to another household's hand, D-096)
    local q = W.S.v3.people[boat.mate]
    if q and q.alive and q.riding == p.id then
      q.riding, q.ride_boat = nil, nil
      q.x, q.y = b.dx + 0.01, b.dy
      -- (home from a trading voyage: the mate's part is done; at the other village they mind the boat)
      if q.trip and q.trip.mate and s.id == q.sett then q.trip = nil end
      if q.hh ~= p.hh and p.carry == "fish" and p.camt > 0 then
        local share = p.camt * 0.5
        p.camt = p.camt - share
        q.carry, q.camt = "fish", share
      end
      M.skip_step(W, q)
    end
    if not (p.trip and p.trip.sea and s.id ~= p.sett) then boat.mate = 0 end
  end
  if boat then
    boat.crew = 0
    boat.x, boat.y = b.mx + (boat.id - 1) * 0.012, b.my
    boat.away = (s.id ~= p.sett) and s.id or nil
    if p.trip and p.trip.sea then
      if s.id ~= p.sett then p.trip.landed = true elseif p.trip.back then p.trip.ashore = true end
    end
    -- hours afloat wear the hull and the oars
    local out = (W.S.v3.now or W.hour) - (boat.t_out or (W.S.v3.now or W.hour))
    boat.cond = max(0, (boat.cond or 1) - C().boat_wear * out)
    boat.trips = (boat.trips or 0) + 1
  end
  -- (a catch landed: the gulls know it, sim/agents/birds.lua)
  if p.carry == "fish" and (p.camt or 0) > 0.5 and s.id == p.sett then
    s.landing = { t = W.S.v3.now or W.hour, amt = p.camt, who = p.name }
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
local function herd_of(s, st) return s.herds and s.herds[stock.HERDS[st.herd or 1]] end

WORK.open_pen = function(W, s, p, st, f)
  local H = herd_of(s, st)
  if not H then return end
  H.out, H.herder = true, p.id
  H.spot_x, H.spot_y = st.sx, st.sy
  -- (the gate opens: the beasts come out, each when it notices, S15)
  stock.rouse(W, s, H, W.S.v3.now or W.hour)
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
  -- (the hide, for the tanning pit: D-104)
  if home and (S.hide or 0) > 0 then
    local hb = s.buildings[home.home]
    if hb then hb.stock.hides = (hb.stock.hides or 0) + S.hide end
  end
end

-- Tanning (D-104): hides put to soak in the household's pit; after tan_days they are lifted out as
-- leather and carried home.
WORK.tan = function(W, s, p, st, f)
  local pit = s.buildings[st.ref or 0]
  if not pit or f < 0.99 or p.carry ~= "hides" then return end
  local day = math.floor((W.S.v3.now or W.hour) / 24)
  pit.stock.soaking = (pit.stock.soaking or 0) + p.camt
  pit.ready = day + C().tan_days
  s.acc.tanned_in = (s.acc.tanned_in or 0) + p.camt
  p.carry, p.camt = "", 0
end

WORK.lift = function(W, s, p, st, f)
  local pit = s.buildings[st.ref or 0]
  if not pit or f < 0.99 or (pit.stock.soaking or 0) <= 0 then return end
  p.carry, p.camt = "leather", pit.stock.soaking
  s.acc.leather = (s.acc.leather or 0) + pit.stock.soaking
  pit.stock.soaking, pit.ready = 0, nil
end

-- The ox (D-108): yoked where it stands, led to the field, unyoked after the stint (it goes back to
-- the herd by itself); a day's ploughing hours counted (it rests when it has done its day).
local function ox_of(s, k)
  local H = s.herds and s.herds.cattle
  return H and H.animals[k or 0]
end
WORK.yoke = function(W, s, p, st, f)
  local a = ox_of(s, st.ox)
  if not a or not a.alive or f < 0.9 then return end
  local now = W.S.v3.now or W.hour
  a.yoke = p.id
  p.ox = a.k
  -- (from where it stands beside the ploughman it steps round in front of him, S14)
  stock.yoke_turn(a, p, now, true)
  a.ver = (a.ver or 0) + 1
  if M.stock_push then M.stock_push(W, s, a) end
end
WORK.unyoke = function(W, s, p, st, f)
  local a = ox_of(s, st.ox)
  if a and a.yoke == p.id then
    -- (out of the yoke where it stands now: it goes back to the herd at its next step, S14)
    if a.act == "yoked" then
      a.tx, a.ty = stock.pos(a, W.S.v3.now or W.hour)
      a.x, a.y = a.tx, a.ty
    end
    a.yoke = 0
    if a.act == "yoked" then a.act = "rest" end
    a.ver = (a.ver or 0) + 1
    if M.stock_push then M.stock_push(W, s, a) end
  end
  p.ox = nil
end
WORK.harness = function(W, s, p, st, f)
  local home = s.buildings[st.ref or 0]
  local hh = W.S.v3.households[p.hh]
  if not home or not hh or f < 0.99 then return end
  local c = C()
  if (home.stock.leather or 0) < c.harness_leather - 1e-6 then return end
  home.stock.leather = home.stock.leather - c.harness_leather
  hh.harness = true
  people.remember(p, W.S.v3.now or W.hour, "Made the harness for our ox")
end

-- Shoes (D-104): a pair cut and stitched at home from the household's leather.
WORK.cobble = function(W, s, p, st, f)
  local home = s.buildings[st.ref or 0]
  local q = W.S.v3.people[st.who or 0]
  if not home or not q or f < 0.99 then return end
  local c = C()
  if (home.stock.leather or 0) < c.shoe_leather - 1e-6 then return end
  home.stock.leather = home.stock.leather - c.shoe_leather
  q.shoes = 1
  s.acc.shoes = (s.acc.shoes or 0) + 1
  people.remember(q, W.S.v3.now or W.hour, q == p and "Made myself new shoes" or (p.name .. " made me new shoes"))
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
  local d = sqrt(dsq(qx - p.x) + dsq(qy - p.y))
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
  local tr = p.trip
  local now = V.now or W.hour
  if tr and tr.migrate and o.id ~= p.sett then
    -- (S16, R5) a household moving here: from the gate on they are this village's people
    require("sim.agents.migrate").arrive(W, o, p, gx, gy, now)
  elseif tr and tr.move and o.id ~= p.sett then
    -- (S16, R4) a bride come to live at her husband's village
    require("sim.agents.migrate").arrive_person(W, o, p, gx, gy, now)
  end
  if tr and tr.party and #tr.party > 0 then
    -- (S15, R4) the beasts of the drove come onto this map with the drover, still led; home again
    -- (or come to live here) they are let go to find the herd
    local kind = tr.lead_kind or (econ.GOODS[tr.give] and econ.GOODS[tr.give].beast)
    if kind then
      stock.join_map(W, o, p, kind, gx, gy, now)
      if o.id == p.sett then
        local L, H = stock.led_by(o, p, kind)
        for _, a in ipairs(L) do
          stock.release(W, o, H, a, now, tr.migrate and ("Come to " .. o.name .. " with its people: to the herd") or "Home from the drove: back to the herd")
        end
        tr.led = {}
        if p.pack and econ.GOODS[tr.give].beast then p.pack[tr.give] = 0 end
      end
    end
  end
  local old = V.settlements[p.at or p.sett]
  if old and old.visitors then people.remove_id(old.visitors, p.id) end
  if tr and (tr.migrate or tr.move) and o.id == p.sett then
    p.at = nil
  elseif o.id == p.sett then
    p.at = nil
    if p.trip then p.trip.back = true end
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
  -- (a child's basket fills slower: D-109)
  if p.stage_was == "child" then got = got * C().child_gather end
  if got < 0.01 then return end
  s.wild = wild - got
  p.carry, p.camt = "food", got
  s.acc.foraged = (s.acc.foraged or 0) + got
end

WORK.fill = function(W, s, p, st, f)
  if f >= 0.6 then
    p.carry, p.camt = "water", 1
    p.cfoul = st.river and M.river_foul(W, s, p.x, p.y) or 0
  end
end

-- How foul the river is where water is drawn (D-107): the dirt that drains into it from the houses
-- and pens along it (settle.daily: `s.river_dirt`, nearer the bank more), less in a high river (more
-- water to carry it), more in a low one, and more in warm weather (summer fluxes).
function M.river_foul(W, s, x, y)
  local flow = max(0.5, min(2, s.flood or 1))
  local warm = max(0.2, min(2, ((s.temp_now or 12) - 4) / 14))
  return (s.river_dirt or 0) * warm / flow
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
  want = want - fish
  -- (a balanced pot, D-101: bread and porridge with a relish of fish, meat, milk or cheese. The
  -- perishables went in first; the keeping relish tops it up to the cook's share, bread fills it)
  local relish = meat + milk + fish
  local batch = relish + want
  local top = max(0, min(want, batch * c.diet_p - relish))
  local sm = min(b.stock.smoked or 0, top)
  local ch = min(b.stock.cheese or 0, top - sm)
  want = want - sm - ch
  local amt = min(b.stock.food or 0, want)
  want = want - amt
  -- (no bread left: more relish instead)
  local sm2 = min((b.stock.smoked or 0) - sm, want)
  local ch2 = min((b.stock.cheese or 0) - ch, want - sm2)
  sm, ch = sm + sm2, ch + ch2
  if amt + fish + sm + ch + meat + milk <= 0 then return end
  b.stock.fish = (b.stock.fish or 0) - fish
  b.stock.food = (b.stock.food or 0) - amt
  b.stock.smoked = (b.stock.smoked or 0) - sm
  b.stock.cheese = (b.stock.cheese or 0) - ch
  local tot = amt + fish + sm + meat + milk + ch
  b.stock.meals = (b.stock.meals or 0) + tot
  -- (the household's diet: the relish share of what it cooks, as it goes)
  local share = (tot - amt) / tot
  b.diet = b.diet and (b.diet + (share - b.diet) * c.diet_k * min(1, tot / max(0.1, n * c.cook_batch))) or share
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
  -- (smoked fish bought into the granary goes out with the grain, in proportion: D-097)
  local gr, sm = b.stock.grain or 0, b.stock.smoked or 0
  local fish = (st.as == "food" and sm > 0) and min(sm, want * sm / (gr + sm)) or 0
  local got = min(want - fish, gr)
  if got + fish > 0 then
    b.stock.grain = gr - got
    b.stock.smoked = sm - fish
    p.carry, p.camt = st.as or "grain", got + fish
    p.cfish = fish    -- (the smoked fish in the basket stays fish at home: D-101)
    -- (the grain the households take home to eat: the village's measure of its bread, D-100)
    if st.as == "food" then s.acc.g_food = (s.acc.g_food or 0) + got end
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
    if got > 0 then b.stock.flax = b.stock.flax - got; p.carry, p.camt = "flax", got
    elseif st.wool and (b.stock.wool or 0) > 0 then
      -- (wool bought into the granary: the households fetch it to dress as they fetch flax, D-098)
      got = min(st.n or 2, b.stock.wool)
      b.stock.wool = b.stock.wool - got
      p.carry, p.camt = "wool", got
    end
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

-- Trade goods into a trader's pack (D-101): from the house, the granary or the woodstore.
local function stock_key(b, g)
  if g == "grain" then return b.kind == "granary" and "grain" or "food" end
  return g
end
M.stock_key = stock_key

local function pack_load(p)
  local w = 0
  if p.pack then
    for _, g in ipairs(econ.ORDER) do
      local G = econ.GOODS[g]
      if not G.beast then w = w + (p.pack[g] or 0) * G.wt end   -- (beasts are led, not carried: S15)
    end
  end
  return w
end
M.pack_load = pack_load

PICK.pack = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local g = st.good
  local key = stock_key(b, g)
  local room = ((p.strong and 1.3 or 1) - pack_load(p)) / econ.GOODS[g].wt
  local got = min(st.n or 0, b.stock[key] or 0, room)
  if p.carry ~= "" and p.carry ~= "pack" then return end
  p.pack = p.pack or {}
  if got > 0 then
    b.stock[key] = b.stock[key] - got
    p.pack[g] = (p.pack[g] or 0) + got
  end
  p.carry, p.camt = "pack", pack_load(p)
end

-- (S16, R4) Help given by another village, from its granary into the pack (what is left of what
-- was agreed at the granary), to be carried to the boat.
PICK.relief = function(W, s, p, st)
  local b = s.buildings[st.ref]
  local tr = p.trip
  if not b or not tr or p.carry ~= "" and p.carry ~= "pack" then return end
  local room = ((p.strong and 1.3 or 1) - pack_load(p)) / econ.GOODS.grain.wt
  local got = min(st.n or 0, tr.gift_left or 0, b.stock.grain or 0, room)
  if got <= 0 then return end
  b.stock.grain = b.stock.grain - got
  tr.gift_left = tr.gift_left - got
  p.pack = p.pack or {}
  p.pack.grain = (p.pack.grain or 0) + got
  p.carry, p.camt = "pack", pack_load(p)
end

-- Cargo out of a boat into the pack (D-102): a load at a time.
PICK.cargo = function(W, s, p, st)
  local B = boats_of(W, p)
  local boat = B and B[st.boat or 0]
  if not boat or not boat.cargo then return end
  if p.carry ~= "" and p.carry ~= "pack" then return end
  local g = st.good
  local room = ((p.strong and 1.3 or 1) - pack_load(p)) / econ.GOODS[g].wt
  local got = min(st.n or room, boat.cargo[g] or 0, room)
  if got <= 0 then return end
  boat.cargo[g] = boat.cargo[g] - got
  p.pack = p.pack or {}
  p.pack[g] = (p.pack[g] or 0) + got
  p.carry, p.camt = "pack", pack_load(p)
end

-- (S15, R4) A beast taken by the halter for the drove: it joins the line behind the drover.
WORK.halter = function(W, s, p, st, f)
  if f < 0.9 or not p.trip then return end
  local H = s.herds and s.herds[stock.HERDS[st.herd or 1]]
  local a = H and H.animals[st.ref or 0]
  local now = W.S.v3.now or W.hour
  if st.mig then
    -- (S16, R5) the household's own beast, taken along to the new village
    if a and a.alive and a.owner == p.hh and (a.led or 0) == 0 and (a.yoke or 0) == 0 and (a.held or 0) == 0 then
      stock.halter(W, s, H, a, p, now)
      a.why = "Taken along by " .. (p.name or "its people") .. ": the household is leaving"
    end
    return
  end
  if not a or a.sp ~= p.trip.give or not stock.sellable(a, p.hh, now) then return end
  stock.halter(W, s, H, a, p, now)
  p.pack = p.pack or {}
  p.pack[a.sp] = (p.pack[a.sp] or 0) + 1
  p.carry, p.camt = "pack", pack_load(p)
end

-- What the other village's stores pay for a deal struck at a house (D-101), loaded at the stores.
PICK.owed = function(W, s, p, st)
  local tr = p.trip
  local d = tr and tr.deals and tr.deals[st.di or 0]
  local b = s.buildings[st.ref]
  if not d or not d.owed or not b then return end
  local H = tr.take
  local key = stock_key(b, H)
  local q = min(d.owed, b.stock[key] or 0)
  d.owed = nil
  if q <= 0 then return end
  b.stock[key] = b.stock[key] - q
  p.pack = p.pack or {}
  p.pack[H] = (p.pack[H] or 0) + q
  p.carry, p.camt = "pack", pack_load(p)
  if econ.GOODS[H].food then
    local home = W.S.v3.settlements[p.sett]
    s.acc.export = (s.acc.export or 0) + q
    home.acc.import = (home.acc.import or 0) + q
  end
end

-- An exchange at the other village (D-101): the trader's goods for what the household (or the
-- village's stores) there pays, at the price both found worth it; both villages' books, the
-- relation between them and the people's memories.
WORK.deal = function(W, s, p, st, f)
  if f < 0.99 then return end
  local V = W.S.v3
  local tr = p.trip
  if not tr or not p.pack then return end
  local d = tr.deals and tr.deals[st.di or 0]
  if not d or d.done then return end
  if not st.part then d.done = true end
  local home = V.settlements[p.sett]
  local X, H = tr.give, tr.take
  local GX, GH = econ.GOODS[X], econ.GOODS[H]
  -- (where the goods go: the buyer's house, or the village's stores; what pays: the buyer's own
  -- goods, or the village's stores, loaded there afterwards if they are elsewhere)
  local hb = s.buildings[st.ref or 0]
  if not hb then return end
  local src = s.buildings[d.src or 0]
  if not src then return end
  local hkey = stock_key(src, H)
  local qx = min(d.left or d.q, p.pack[X] or 0)
  if qx <= 1e-6 then return end
  local qh = min(qx * d.k, src.stock[hkey] or 0)
  if qh <= 1e-6 then return end
  qx = qh / d.k
  local led, Hb
  if GX.beast then
    -- (S15, R4) beasts change hands whole: those led here, as many as are paid for
    led, Hb = stock.led_by(s, p, GX.beast)
    qx = min(floor(qx + 1e-6), #led)
    if qx < 1 then return end
    qh = qx * d.k
  end
  -- goods change hands
  d.left = (d.left or d.q) - qx
  if d.left <= 1e-3 then d.done = true end
  p.pack[X] = p.pack[X] - qx
  if led then
    -- the beasts themselves: now the buyer's, let go into the herd here (the same beasts that left
    -- home, each with its age, coat, fleece and health)
    local now = V.now or W.hour
    local why = "New in " .. s.name .. "'s herd: bought from " .. home.name
    for i = 1, qx do
      local a = led[i]
      a.owner, a.cull = d.b, nil
      a.from, a.bought = home.id, now
      people.remove_id(tr.led, a.k)
      stock.release(W, s, Hb, a, now, why)
    end
  else
    local xkey = stock_key(hb, X)
    hb.stock[xkey] = (hb.stock[xkey] or 0) + qx
  end
  if src == hb then
    src.stock[hkey] = src.stock[hkey] - qh
    p.pack[H] = (p.pack[H] or 0) + qh
    if GH.food then
      s.acc.export = (s.acc.export or 0) + qh
      home.acc.import = (home.acc.import or 0) + qh
    end
  else
    d.owed = (d.owed or 0) + qh
  end
  p.carry, p.camt = "pack", pack_load(p)
  -- the books
  if GX.food then
    home.acc.export = (home.acc.export or 0) + qx
    s.acc.import = (s.acc.import or 0) + qx
  end
  home.acc["sold_" .. X] = (home.acc["sold_" .. X] or 0) + qx
  home.acc["got_" .. H] = (home.acc["got_" .. H] or 0) + qh
  s.acc["in_" .. X] = (s.acc["in_" .. X] or 0) + qx
  s.acc["out_" .. H] = (s.acc["out_" .. H] or 0) + qh
  s.acc.deals = (s.acc.deals or 0) + 1
  home.acc.deals = (home.acc.deals or 0) + 1
  tr.sold = (tr.sold or 0) + qx
  tr.got = (tr.got or 0) + qh
  -- trust grows with deals kept, both ways
  local c = C()
  local now = V.now or W.hour
  for _, pr in ipairs({ { home, s }, { s, home } }) do
    local r = econ.rel(pr[1], pr[2])
    r.deals = r.deals + 1
    r.trust = r.trust + (1 - r.trust) * c.trust_deal
    r.t = now
  end
  local hh_b = d.b ~= 0 and V.households[d.b] or nil
  local hh_a = V.households[p.hh]
  if hh_b and hh_a then
    hh_a.partners = hh_a.partners or {}
    hh_a.partners[hh_b.id] = (hh_a.partners[hh_b.id] or 0) + 1
    hh_b.partners = hh_b.partners or {}
    hh_b.partners[hh_a.id] = (hh_b.partners[hh_a.id] or 0) + 1
  end
  -- memories
  local q1 = V.people[hh_b and hh_b.members[1] or 0]
  local who = hh_b and q1 and ("the " .. q1.surname .. " household") or (s.name .. "'s stores")
  people.remember(p, now, sfmt("Traded %s for %s with %s at %s", econ.amount(qx, X), econ.amount(qh, H), who, s.name))
  if hh_b then
    for _, id in ipairs(hh_b.members) do
      local q = V.people[id]
      if q and q.alive and people.stage(q, now) == "adult" then
        people.remember(q, now, sfmt("Got %s from %s of %s for %s", econ.amount(qx, X), p.name, home.name, econ.amount(qh, H)))
      end
    end
  end
end

-- Asking for help (D-103): the other village gives grain from its granary; both remember it.
WORK.ask = function(W, s, p, st, f)
  if f < 0.99 then return end
  local V = W.S.v3
  local tr = p.trip
  local g = s.buildings[st.ref or 0]
  if not tr or not g then return end
  local home = V.settlements[p.sett]
  local now = V.now or W.hour
  local q = min(tr.gift or 0, g.stock.grain or 0)
  if q < 0.5 then
    tr.gift_left = 0
    people.remember(p, now, sfmt("Asked %s for food: they had none to give us", s.name))
    return
  end
  if st.sea then
    -- (S16, by sea: what they give is carried from the granary to the boat a load at a time)
    tr.gift_left = q
  else
    g.stock.grain = g.stock.grain - q
    p.pack = p.pack or {}
    p.pack.grain = (p.pack.grain or 0) + q
    p.carry, p.camt = "pack", pack_load(p)
  end
  s.acc.export = (s.acc.export or 0) + q
  home.acc.import = (home.acc.import or 0) + q
  s.acc.relief_out = (s.acc.relief_out or 0) + q
  home.acc.relief_in = (home.acc.relief_in or 0) + q
  -- (help is remembered: the asker trusts the giver more and owes it; a debt the giver had to the
  -- asker is paid down by the help)
  local r = econ.rel(home, s)
  local r2 = econ.rel(s, home)
  r.gifts = r.gifts + 1
  r.trust = r.trust + (1 - r.trust) * 0.2
  r2.trust = r2.trust + (1 - r2.trust) * 0.05
  r2.given = (r2.given or 0) + 1
  local repay = min(q, r2.owe_them or 0)
  r2.owe_them = (r2.owe_them or 0) - repay
  r.owed_us = (r.owed_us or 0) - repay
  r.owe_them = (r.owe_them or 0) + q - repay
  r2.owed_us = (r2.owed_us or 0) + q - repay
  people.remember(p, now, sfmt("%s gave us %s in our need", s.name, econ.amount(q, "grain")))
end

-- (S16, R4) Courting at another village (life.lua court_village): he meets its people; among its
-- unwed women of an age to marry (not his kin) the one he and she take to - by age and their own
-- draws - marries him and will walk home with him; the villages trust each other more for it, and
-- each hears how the other stands. If there is nobody, he goes home alone.
WORK.court = function(W, s, p, st, f)
  if f < 0.99 then return end
  local V = W.S.v3
  local tr = p.trip
  if not tr or tr.met then return end
  tr.met = true
  local now = V.now or W.hour
  local home = V.settlements[p.sett]
  econ.learn(W, home, s, now)
  econ.learn(W, s, home, now)
  local c = C()
  local best, bs = nil, -1e9
  for _, id in ipairs(s.members) do
    local q = V.people[id]
    if q and q.alive and q.sex == "f" and q.partner == 0 and not q.move and not q.at and not people.is_kin(W, p, q.id) then
      local st2, a = people.stage(q, now)
      if st2 == "adult" and a >= c.marry_f and a < 40 then
        local sc = -math.abs(a + 2.5 - people.age(p, now)) * 0.08
          + require("sim.rng").float(W.seed, require("sim.rng").S.V3_THINK, p.id * 131 + q.id, floor(now / 24) % 4294967296, 140) * 0.6
        if sc > bs then best, bs = q, sc end
      end
    end
  end
  if not best then
    people.remember(p, now, sfmt("Went courting at %s: nobody there to marry", s.name))
    return
  end
  best.partner, p.partner = p.id, best.id
  best.married, p.married = now, now
  people.remember(best, now, sfmt("Married %s of %s, who came courting", p.name, home.name))
  people.remember(p, now, sfmt("Married %s of %s", best.name, s.name))
  local r1, r2 = econ.rel(home, s), econ.rel(s, home)
  r1.trust = r1.trust + (1 - r1.trust) * 0.15
  r2.trust = r2.trust + (1 - r2.trust) * 0.15
  tr.bride, tr.depart = best.id, now + c.court_wait_h
  best.move = { to = home.id, from = s.id, depart = tr.depart, groom = p.id,
    why = sfmt("Married %s: going to live at %s", p.name, home.name) }
  s.acc.brides_out = (s.acc.brides_out or 0) + 1
  -- (she hears at once, wherever she is in the village)
  if M.interrupt_hook then M.interrupt_hook(W, best, now + 0.05, "Married " .. p.name) end
  if W.record_events then require("sim.causality.events").emit(W, "marriage", s.cell, 0, 1, { settlement = s.id, a = best.id, b = p.id }) end
end

-- Driving wolves off a pen (D-105): shouting, a torch, a stick; any wolf of a pack still near the
-- pen turns away.
WORK.drive_off = function(W, s, p, st, f)
  if not s.wildlife then return end
  local wild = require("sim.agents.wild")
  for _, w in ipairs(s.wildlife.animals) do
    if w.alive and w.pred and not w.done then
      local wx, wy = wild.pos(w, W.S.v3.now or W.hour)
      if dsq(wx - p.x) + dsq(wy - p.y) < 0.12 then wild.pack_flee(W, s, w, W.S.v3.now or W.hour, p) end
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

PICK.hides = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = min(st.n or 4, b.stock.hides or 0)
  if got > 0 then
    b.stock.hides = b.stock.hides - got
    p.carry, p.camt = "hides", got
  end
end

PICK.wool = function(W, s, p, st)
  local b = s.buildings[st.ref]
  if not b then return end
  local got = min(st.n or 2, b.stock.wool or 0)
  if got > 0 then
    b.stock.wool = b.stock.wool - got
    p.carry, p.camt = "wool", got
  end
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
  if what == "pack" and st.to == "cargo" then
    -- (a load into the boat's hold, D-102)
    local B = boats_of(W, p)
    local boat = B and B[st.boat or 0]
    if boat then
      boat.cargo = boat.cargo or {}
      for _, g in ipairs(econ.ORDER) do
        local q = p.pack and p.pack[g] or 0
        if q > 0 then boat.cargo[g] = (boat.cargo[g] or 0) + q end
      end
      p.pack = nil
      p.carry, p.camt = "", 0
    end
    return
  end
  if what == "pack" then
    -- (a trader home, D-101: what is left of the goods and what they brought back, each where the
    -- household keeps it: food in the larder, logs on the woodpile, wool and cloth at home)
    local b = s.buildings[st.ref]
    local V = W.S.v3
    local gave, gv = nil, 0
    if b then
      for _, g in ipairs(econ.ORDER) do
        local q = p.pack and p.pack[g] or 0
        if q > 0 and not econ.GOODS[g].beast then
          local key = stock_key(b, g)
          b.stock[key] = (b.stock[key] or 0) + q
          gave, gv = g, q
        end
      end
    end
    p.pack = nil
    p.carry, p.camt = "", 0
    local hh_a = st.gift and V.households[st.gift]
    local hh_b = hh_a and b and V.households[b.hh]
    if hh_a and hh_b and gave then
      -- (a gift: both households remember it; the people who met grow closer)
      local now = V.now or W.hour
      local G = econ.GOODS[gave]
      local val = gv * G.base
      hh_b.got = hh_b.got or {}
      hh_b.got[hh_a.id] = (hh_b.got[hh_a.id] or 0) + val
      hh_a.gave = hh_a.gave or {}
      hh_a.gave[hh_b.id] = (hh_a.gave[hh_b.id] or 0) + val
      s.acc.gifts = (s.acc.gifts or 0) + 1
      local q1 = V.people[hh_b.members[1] or 0]
      people.remember(p, now, sfmt("Took %s to the %s household", econ.amount(gv, gave), q1 and q1.surname or "neighbour"))
      for _, id in ipairs(hh_b.members) do
        local q = V.people[id]
        if q and q.alive and people.stage(q, now) ~= "infant" then
          people.bond(q, p, 0.05)
          people.bond(p, q, 0.03)
          if people.stage(q, now) == "adult" then
            people.remember(q, now, sfmt("%s brought us %s", p.name, econ.amount(gv, gave)))
          end
        end
      end
    end
    return
  end
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
  elseif what == "leather" or what == "hides" then
    b.stock[what] = (b.stock[what] or 0) + n
  elseif what == "hay" then
    s.hay = (s.hay or 0) + n
  elseif what == "milk" then
    b.stock.milk = (b.stock.milk or 0) + n
  elseif what == "dried" then
    b.stock.smoked = (b.stock.smoked or 0) + n
  elseif what == "smoked" then
    b.stock.smoked = (b.stock.smoked or 0) + n
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
    local fish = min(n, p.cfish or 0)
    p.cfish = nil
    if b.kind == "granary" then
      b.stock.grain = (b.stock.grain or 0) + n - fish
      b.stock.smoked = (b.stock.smoked or 0) + fish
    else
      b.stock.food = (b.stock.food or 0) + n - fish
      b.stock.smoked = (b.stock.smoked or 0) + fish
    end
  elseif what == "logs" then
    b.stock.logs = (b.stock.logs or 0) + n
  elseif what == "water" then
    b.stock.water = (b.stock.water or 0) + n
    b.foul = p.cfoul or 0     -- (the jar holds what was drawn last: D-107)
    p.cfoul = nil
  end
  p.carry, p.camt = "", 0
end

-- The end of a trip (D-097, D-101, D-102): what is left of the provisions back in the larder, the
-- claims and the boat freed. Called after the trip's last drop, whatever the hands hold.
local function end_trip(W, s, p, st)
  local b = s.buildings[st.ref or 0]
  if not b then return end
  if st.trip_end and (p.prov or 0) > 0 then
    -- (what is left of the provisions goes back in the larder)
    b.stock.food = (b.stock.food or 0) + p.prov
    p.prov = 0
  end
  if st.trip_end and p.trip then
    local hh = W.S.v3.households[p.hh]
    if hh and hh.trading == p.id then hh.trading = 0 end
    if s.trading == p.id then s.trading = 0 end
    if p.trip.sea then
      local boat = s.boats and s.boats[p.trip.boat]
      if boat and boat.trip == p.id then boat.trip = nil end
      if boat and boat.cargo then
        -- (anything left in the hold goes home with the last load)
        for _, g in ipairs(econ.ORDER) do
          local v = boat.cargo[g] or 0
          if v > 1e-6 then local key = (g == "grain") and "food" or g; b.stock[key] = (b.stock[key] or 0) + v end
        end
        boat.cargo = nil
      end
    end
    p.trip = nil
    s.acc.trips = (s.acc.trips or 0) + 1
  end
end

-- Apply the effects of p's current step, done to fraction f (1 = complete), at time now.
local LEISURE = { rest = true, talk = true, visit = true, sit_out = true }
local function finish(W, s, p, now, f)
  local st = p.plan[p.pi]
  local dt = now - p.t0
  if (p.act == "travel" or ((p.act == "voyage" or p.act == "ride" or p.act == "mind_boat") and (p.prov or 0) > 0)) and dt > 0 then
    -- the provisions from home, eaten along the road hour by hour when hungry (D-097; eaten
    -- before the leg's hunger was charged, a long leg starved its walker: D-100)
    local c = C()
    local hs = W.S.v3.settlements[p.sett]
    local left = dt
    while left > 0 do
      local h = min(1, left)
      body(p, h, p.act == "travel" and "travel" or p.act)
      left = left - h
      if p.hunger > 0.45 and (p.prov or 0) > 0 then
        local m = min(p.prov, c.meal_fu)
        p.prov = p.prov - m
        p.hunger = max(0, p.hunger - m / c.meal_fu * c.meal)
        hs.acc.eaten = (hs.acc.eaten or 0) + m
      end
    end
  else
    body(p, dt, p.act)
  end
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
    if f >= 1 and st.trip_end then end_trip(W, s, p, st) end
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
  if p.ox then
    -- (S14) a ploughman's new step turns the team: the ox swings round from where it is now
    local H = s.herds and s.herds.cattle
    local a = H and H.animals[p.ox]
    if a and a.act == "yoked" and a.yoke == p.id then stock.yoke_turn(a, p, now) end
  end
  if st.op == "work" and st.kind == "gather" then
    -- (S16, R5) waiting at the gate for the hour the household agreed to set off
    st.dur = max(0.02, (st.until_t or now) - now)
  end
  if st.op == "work" and st.kind == "travel" and st.migrate then
    -- (S16, R5) the household's babes are in the arms of those setting off
    require("sim.agents.migrate").take_babes(W, s, p, now)
  end
  local tr = p.trip
  if tr and tr.led and #tr.led > 0 then
    local G = econ.GOODS[tr.give]
    local bk = tr.lead_kind or (G and G.beast)
    if bk then
      G = { beast = bk }
      if st.op == "work" and st.kind == "travel" and st.lead then
        -- (S16) at the gate the drover waits for the drove to come up before taking the road (an
        -- hour at most: a beast that will not come is left to find its way back to the herd)
        local far = stock.drove_spread(s, p, G.beast, now)
        if far > C().drove_gather and (st.waited or 0) < 1 then
          st.waited = (st.waited or 0) + 0.06
          table.insert(p.plan, p.pi, { op = "work", kind = "wait", dur = 0.06, why = "Waiting for the drove to come up" })
          st = p.plan[p.pi]
          h = START.work
          p.why = st.why
        elseif far > C().drove_gather then
          for _, a in ipairs((stock.led_by(s, p, G.beast))) do
            local ax, ay = stock.pos(a, now)
            if nav.dist(ax, ay, p.x, p.y) > C().drove_gather then
              people.remove_id(tr.led, a.k)
              stock.release(W, s, s.herds[G.beast], a, now, "Strayed from the drove: back to the herd")
            end
          end
        end
      end
      -- (S16) a drover's new step: the beasts of the drove notice them move on, each in its time
      stock.rouse_drove(W, s, p, G.beast, now)
    end
  end
  return h(W, s, p, st, now)
end

return M
