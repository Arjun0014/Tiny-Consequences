-- Choosing what to do next (docs/v3/10_AGENTS.md §5). Called when a person's plan runs out or
-- is interrupted. Body first (sleep, shelter, eat), then everything they could do now — the
-- settlement's open work (sow, tend, harvest, carry sheaves, fell, carry logs, deliver, build),
-- their household's chores (food, firewood, water), care, leisure with a reason — scored by
-- urgency x fit x fatigue - travel. The chosen candidate becomes a plan of steps and a "why"
-- that names the state behind it.
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local clock = require("sim.agents.clock")
local nav = require("sim.agents.nav")
local act = require("sim.agents.act")
local settle = require("sim.agents.settle")
local kin = require("sim.agents.kin")

local M = {}
local floor, max, min, sqrt, abs = math.floor, math.max, math.min, math.sqrt, math.abs
local function C() return cfgmod.V3 end
local trade = require("sim.agents.trade")
local econ = require("sim.agents.econ")
local conflict = require("sim.agents.conflict")   -- (S20, R6)
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")
local dsq = require("sim.dmath").sq
local NOW = 0

local function clock_str(h)
  local hh = floor(h)
  local mm = floor((h - hh) * 60)
  return sfmt("%02d:%02d", hh % 24, mm)
end
M.clock_str = clock_str

-- A person's random draws at a minute: one hash per (person, minute), then a mixing round per
-- draw (a pure function of key and minute: the cache is never state)
local UB, UBM = {}, {}
local function u01(W, p, now, k)
  local m = floor(now * 60) % 4294967296
  local id = p.id
  if UBM[id] ~= m or UB[-id] ~= W.seed then
    UB[id] = rng.hash(W.seed, rng.S.V3_THINK, p.key, m, 0)
    UBM[id], UB[-id] = m, W.seed
  end
  return rng.fbase(UB[id], k)
end
M.u01 = u01

local function dist(x0, y0, x1, y1)
  local dx, dy = x1 - x0, y1 - y0
  return sqrt(dx * dx + dy * dy)
end

local function plural(n, one, many) return n == 1 and one or many end

-- A household's chore claim (who is cooking, fetching food/wood/water, foraging) holds only while
-- its holder is alive, still of the household and still at that objective (a claim must never
-- outlive a person who moved to a new house, died or changed their mind).
local CLAIM_OBJ = { cooking = "cook", fetch_food = "fetch_food", fetch_wood = "fetch_wood", fetch_water = "fetch_water", foraging = "forage", child_gathering = "forage",
  shore = "shore", smoking = "smoke", dressing = "dress", weaving = "weave", sewing = "sew", mowing = "mow", feeding = "feed", moving_in = "strike_camp",
  asking = "visit_kin",
  milking = "milk", slopping = "slop", cheesing = "cheese", hunting = "hunt", angling = "angle", trading = "trade", sharing = "share", tanning = "tan", lifting = "lift", cobbling = "cobble",
  salting_pans = "salt", querning = "quern", taking = "steal" }
local function claim_free(W, hh, field, p)
  local id = hh[field] or 0
  if id == 0 or (p and id == p.id) then return true end
  local q = W.S.v3.people[id]
  if q and q.alive and q.hh == hh.id and q.objk == CLAIM_OBJ[field] then return false end
  hh[field] = 0
  return true
end
M.claim_free = claim_free

-- Plans -------------------------------------------------------------------------------------

local function plan_reset(p)
  local pl = p.plan
  for i = #pl, 1, -1 do pl[i] = nil end
  p.pi = 1
  return pl
end

-- (constant directions, built once: a table literal in a hot function is garbage every call)
local CIRCLE9 = { { 1, 0 }, { 0.707, 0.707 }, { 0, 1 }, { -0.707, 0.707 }, { -1, 0 }, { -0.707, -0.707 }, { 0, -1 }, { 0.707, -0.707 }, { 1, 0 } }

-- (S17) At a camp each has their own place round the fire (a turn of the circle by who they are):
-- going "home" to a camp is going there. GO_S, GO_P: the settlement and person being planned for.
local GO_S, GO_P = nil, nil
local function camp_spot(s, b, p)
  local t = ((p.id * 0.6180339887) % 1) * 8
  local i = floor(t)
  local f = t - i
  local P = CIRCLE9
  local ca = P[i + 1][1] + (P[i + 2][1] - P[i + 1][1]) * f
  local sa = P[i + 1][2] + (P[i + 2][2] - P[i + 1][2]) * f
  local r = s.T * (1.1 + 0.7 * ((p.id * 0.37) % 1))
  return b.cx + ca * r, b.cy + sa * r
end
M.camp_spot = camp_spot

local function go(pl, x, y, place, enter)
  if enter and GO_S and GO_P then
    local b = GO_S.buildings[enter]
    if b and b.kind == "camp" then x, y = camp_spot(GO_S, b, GO_P) end
  end
  pl[#pl + 1] = { op = "go", x = x, y = y, place = place, enter = enter }
end

local function work(pl, kind, dur, extra)
  local st = extra or {}
  st.op, st.kind, st.dur = "work", kind, dur
  pl[#pl + 1] = st
end

local function door(b) return b.dx, b.dy end
local function mapm_xy(s, ix, iy) return require("sim.agents.map").ixy_xy(s, ix, iy) end

local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }

-- A spot near (x, y) for person p (so people waiting or talking do not stand on each other).
local function near_spot(W, p, now, x, y, r, k)
  local a = u01(W, p, now, k or 7) * 6.283185307
  local rr = r * (0.4 + 0.6 * u01(W, p, now, (k or 7) + 1))
  -- (no trig in sim logic: a polygonal approximation of the circle)
  local ca, sa
  local t = a / 6.283185307 * 8
  local i = floor(t)
  local f = t - i
  local P = CIRCLE9
  ca = P[i + 1][1] + (P[i + 2][1] - P[i + 1][1]) * f
  sa = P[i + 1][2] + (P[i + 2][2] - P[i + 1][2]) * f
  return x + ca * rr, y + sa * rr
end
M.near_spot = near_spot

-- Context ------------------------------------------------------------------------------------

local CTX = {}
local function context(W, s, p, now)
  local c = C()
  local ctx = CTX
  local lh = clock.local_hour(now, W.grid.lon[s.cell])
  ctx.lh = lh
  local sr, ss = s.sunrise or 6, s.sunset or 18
  local wake = sr + c.wake_after_sunrise + p.chrono
  if wake < c.wake_min then wake = c.wake_min elseif wake > c.wake_max then wake = c.wake_max end
  local bed = ss + c.bed_after_sunset + p.chrono * 0.5
  if bed < c.bed_min then bed = c.bed_min elseif bed > c.bed_max then bed = c.bed_max end
  local st, age = people.stage(p, now)
  if st == "child" then wake = wake + 0.6; bed = bed - 1.5 end
  if st == "elder" then wake = wake - 0.4; bed = bed - 0.7 end
  ctx.wake, ctx.bed, ctx.sunrise, ctx.sunset = wake, bed, sr, ss
  ctx.stage, ctx.age = st, age
  ctx.night = lh >= bed or lh < wake - 0.05
  -- the working day: from waking until dusk (the diligent stay a little later)
  local stop = ss + (p.dil - 1) * 0.8
  ctx.workday = (not ctx.night) and lh >= wake + 0.3 and lh < stop
  ctx.rain = s.rain_now or 0
  ctx.home = people.home(W, s, p)
  local hx, hy
  if ctx.home then hx, hy = door(ctx.home) else hx, hy = s.sqx, s.sqy end
  ctx.hx, ctx.hy = hx, hy
  ctx.dhome = dist(p.x, p.y, hx, hy)
  ctx.spd = people.speed(W, p, now)
  ctx.hh = W.S.v3.households[p.hh]
  -- (S19) the household is about to move or make a new place: nobody sets out on other journeys
  local hh0 = ctx.hh
  local mg0, fd0 = hh0 and hh0.migrate, hh0 and hh0.found
  ctx.moving = (mg0 and not mg0.arrived and not mg0.given_up) or (fd0 and not fd0.arrived and not fd0.given_up) or p.ferry ~= nil
  local V = W.S.v3
  local ib = p.babe ~= 0 and V.people[p.babe]
  if ib and not (ib.alive and ib.act == "carried" and ib.carer == p.id) then p.babe = 0; ib = nil end
  ctx.babe = ib or nil
  local m = st == "apprentice" and p.mentor ~= 0 and V.people[p.mentor]
  ctx.mentor = (m and m.alive) and m or nil
  ctx.with_mentor = false
  p.elder = st == "elder"
  p.adult = st == "adult"
  return ctx
end

-- Absolute time of the next local hour `h` after now.
local function next_local(now, lh, h)
  local d = (h - lh) % 24
  if d < 0.05 then d = d + 24 end
  return now + d
end

-- Candidates ---------------------------------------------------------------------------------
-- Each adds { u, kind, ... } to the list; best wins.

local CAND = {}
local ncand = 0
local function add(u, kind, a, b, why)
  ncand = ncand + 1
  local c = CAND[ncand]
  if not c then c = {}; CAND[ncand] = c end
  c.u, c.kind, c.a, c.b, c.why, c.lazy, c.fmt = u, kind, a, b, why, nil, nil
end

-- A candidate whose why is a format and its arguments: formatted only if it is chosen (the
-- browser's Lua pays dearly for every string.format, D-090).
local function addw(u, kind, a, b, fmt, w1, w2, w3, w4, w5, w6, w7)
  ncand = ncand + 1
  local c = CAND[ncand]
  if not c then c = {}; CAND[ncand] = c end
  c.u, c.kind, c.a, c.b, c.why, c.lazy, c.fmt = u, kind, a, b, nil, nil, fmt
  c.w1, c.w2, c.w3, c.w4, c.w5, c.w6, c.w7 = w1, w2, w3, w4, w5, w6, w7
end

-- The why of candidate c (its text, or its format worked out now).
local function why_of(c)
  if c.fmt then return sfmt(c.fmt, c.w1, c.w2, c.w3, c.w4, c.w5, c.w6, c.w7) end
  return c.why
end
M.why_of = why_of

-- A candidate whose target is costly to find (a scan of every tree) enters with an upper bound on
-- its utility and is worked out only if that bound would win (best_cand): exactly the choice the
-- full evaluation makes, at a fraction of the cost (D-086). fn(c, arg) sets c.u, c.a, c.b, c.why
-- (c.u = -1e9 when there is no target). The decision's context is in LZ.
local LZ = {}
local function add_lazy(bound, kind, fn, arg)
  ncand = ncand + 1
  local c = CAND[ncand]
  if not c then c = {}; CAND[ncand] = c end
  c.u, c.kind, c.a, c.b, c.why, c.lazy, c.larg, c.fmt = bound, kind, 0, 0, "", fn, arg, nil
  if M.EAGER then c.lazy = nil; fn(c, arg) end   -- (tests: the full evaluation, for comparison)
end

local WEATHER_OK = function(ctx, p)
  if ctx.rain == 2 then return 0.08 end
  if ctx.rain == 1 then return p.dil >= C().rain_hardy and 0.75 or 0.3 end
  return 1
end

local function travel(ctx, x, y, px, py)
  return dist(px, py, x, y) / ctx.spd * 0.45
end

local ROLE_JOBS = {
  farmer = { grub = true, till = true, sow = true, tend = true, harvest = true, haul_grain = true, thresh = true },
  woodcutter = { fell = true, haul_logs = true, grub = true, fire_clamp = true },
  builder = { build = true, deliver = true, haul_logs = true, repair = true, quarry = true, deliver_stone = true, quern = true, smith = true, mine = true },
  fisher = { fish = true, mend_boat = true, smoke = true },
  shepherd = { herd = true, shear = true, feed = true },
}

-- Does the apprentice's mentor work on this target now (D-081)? (ref: the plot, building or tree)
local function beside_mentor(W, ctx, kind, ref)
  local m = ctx.mentor
  if not m or not ref or ref == 0 then return false end
  local dm, dk = kin.DOMAIN[m.tgt_kind or ""], kin.DOMAIN[kind]
  if not dm or dm ~= dk then return false end
  if dm == "granary" then return true end
  if dm == "tree" then
    local tr = W.S.v3.settlements[m.sett].trees
    local a = m.tgt_ref or 0
    if a == 0 or a > tr.n then return false end
    return dist(tr.x[a], tr.y[a], tr.x[ref], tr.y[ref]) < 0.3
  end
  return m.tgt_ref == ref
end

local W_FIT
local function fit(p, ctx, kind, ref)
  local st = ctx.stage
  if st == "infant" then return 0 end
  local bf = ctx.babe and (kin.BABE_FIT[kind] or 1) or 1
  if st == "child" then
    -- (light chores: gathering at the woods' edge from six, water from seven, firewood from eight,
    -- spinning from nine; a child is never a field hand. D-109: children of seven to twelve had
    -- spent some two-thirds of their waking day at play)
    local n = (p.chore_day == floor(NOW / 24)) and (p.chores or 0) or 0
    -- (the day's first errand is one the parents send them on; after it, less eagerly)
    local tired = n == 0 and 1.8 or max(0.2, 1 - 0.3 * n)
    if kind == "forage" and ctx.age >= C().gather_age then return (ctx.age >= C().help_age and 0.9 or 0.7) * tired * bf end
    if kind == "fetch_water" and ctx.age >= C().help_age - 1 then return 1.2 * tired * bf end
    if kind == "fetch_wood" and ctx.age >= C().help_age then return 0.9 * tired * bf end
    if kind == "spin" and ctx.age >= C().help_age + 1 then return 0.85 * tired * bf end   -- (D-088)
    return 0
  end
  if st == "apprentice" then
    -- beside their mentor they take a hand in the mentor's own work (lighter parts, learning)
    if beside_mentor(W_FIT, ctx, kind, ref) then
      ctx.with_mentor = true
      return (kind == "fell" and 0 or 1.05) * bf
    end
  end
  if kind == "cook" then
    -- (no role bonus: whoever is at home and free; elders and nursing mothers at home most)
    if st == "apprentice" then return 0.85 * bf end
    if st == "elder" then return 1.15 * bf end
    return (p.nursing > 0 and 1.2 or 1) * bf
  end
  if kind == "spin" or kind == "sew" or kind == "weave" or kind == "dress" or kind == "mend_clothes" then
    -- the slack season's work: anyone, elders most at the spindle and needle (D-088)
    if st == "elder" then return (kind == "dress" and 0.7 or 1.25) * bf end
    if st == "apprentice" then return 0.9 * bf end
    return bf
  end
  if kind == "smoke" then
    if st == "apprentice" then return 0.8 * bf end
    if st == "elder" then return 1.2 * bf end
    return (p.role == "fisher" and 1.1 or 1) * bf
  end
  if kind == "cook" then
    -- (no role bonus: whoever is at home and free; elders and nursing mothers at home most)
    if st == "apprentice" then return 0.85 end
    if st == "elder" then return 1.15 end
    return p.nursing > 0 and 1.2 or 1
  end
  if st == "apprentice" then
    if kind == "haul_grain" or kind == "haul_logs" or kind == "fetch_water" or kind == "fetch_food"
      or kind == "fetch_wood" or kind == "harvest" or kind == "tend" or kind == "deliver" then return 0.75 * bf end
    if kind == "sow" then return 0.5 * bf end
    return 0
  end
  local f = 1
  local rj = ROLE_JOBS[p.role]
  if rj and rj[kind] then f = 1.45 end
  if st == "elder" then
    if kind == "fell" or kind == "build" or kind == "haul_logs" or kind == "deliver" or kind == "quarry" or kind == "haul_stone"
      or kind == "deliver_stone" then f = f * 0.25
    elseif kind == "tend" or kind == "fetch_water" or kind == "fetch_food" then f = f * 1.1
    else f = f * 0.6 end
  end
  if p.preg_due > 0 and p.preg_due - NOW < 120 then
    if kind == "fell" or kind == "build" or kind == "haul_logs" or kind == "quarry" or kind == "haul_stone" or kind == "deliver_stone" then f = f * 0.1 end
  end
  return f * bf
end

local function fatigue(p)
  local e = p.energy
  if e < 0.25 then return 0.35 end
  return 0.55 + 0.45 * e
end

-- Settlement work (the board) for person p.
local function work_candidates(W, s, p, ctx, now)
  local c = C()
  local wx = WEATHER_OK(ctx, p)
  local fat = fatigue(p) * wx
  local px, py = p.x, p.y
  local need = s.need
  local plots = s.plots
  -- (a child's fit is 0 for the fields, the wood and the building sites, D-109: only gathering is
  -- weighed for them here; S14)
  local grown = ctx.stage ~= "child"
  -- farming: a household works its own fields; another's only to help when they fall behind (late
  -- in the window) or at harvest (D-081)
  for k = 1, grown and #plots or 0 do
    local pl = plots[k]
    if pl.alive then
      local own = pl.hh == p.hh or pl.hh == 0
      local help = own and 1 or (0.25 + 0.75 * max(0, (need.sow_late or 0) - 0.4) / 0.6)
      local cx, cy = (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5
      local tc = travel(ctx, cx, cy, px, py)
      local doy = s.doy or 0
      local plough = settle.plough_open(pl, doy)
      if (need.sowing and settle.sow_open(pl, doy)) or plough then
        -- an unclaimed row to sow (hoed) or to hoe (bare)?
        -- (a row someone began is finished first: its progress is kept, D-080)
        local srow, trow, sp, tp = 0, 0, -1, -1
        local rp = pl.row_p
        for r = 1, pl.rows do
          if pl.row_claim[r] == 0 then
            local rs = pl.row_st[r]
            local pr = rp and rp[r] or 0
            if rs == 1 and pr > sp and not plough then srow, sp = r, pr elseif (rs == 0 or rs == 3) and pr > tp then trow, tp = r, pr end
          end
        end
        -- (sowing to need, D-100: a field the village does not want sown this time rests; one not
        -- yet decided waits for the decision)
        if not plough and pl.crop ~= "flax" and pl.sow_ok ~= true then trow, srow = 0, 0 end
        local left = pl.rows - pl.sown
        if srow > 0 then
          local f = fit(p, ctx, "sow", k)
          local wm = ctx.with_mentor
          ctx.with_mentor = false
          if f > 0 then
            -- seed in hand saves the walk to the granary
            local seeded = p.carry == "seed" and p.camt >= c.seed_row
            local urg = (0.66 + 0.25 * need.sow_late + (seeded and 0.06 or 0) + (wm and 0.15 or 0)) * (wm and 1 or help)
            local extra = seeded and 0 or travel(ctx, s.granary_x, s.granary_y, px, py) * 0.5
            addw(urg * f * fat - tc - extra, "sow", k, srow,
              "It is sowing time: %d %s of %s still to sow; row %d is hoed", left, plural(left, "row", "rows"), pl.name, srow)
          end
        end
        if trow > 0 then
          local f = fit(p, ctx, "till", k)
          local wm = ctx.with_mentor
          ctx.with_mentor = false
          if f > 0 and (ctx.stage ~= "apprentice" or wm) then
            local tl = 0
            for r = 1, pl.rows do if pl.row_st[r] == 0 or pl.row_st[r] == 3 then tl = tl + 1 end end
            if plough then
              -- the fallow is ploughed in summer: the weeds turned under, the soil readied for the winter crop
              addw((0.42 + (wm and 0.15 or 0)) * (wm and 1 or (own and 1 or 0.25)) * f * fat - tc, "till", k, trow,
                "Ploughing the fallow of %s against the weeds (%d %s to go)", pl.name, tl, plural(tl, "row", "rows"))
            else
              local urg = (0.62 + 0.25 * need.sow_late + (tp > 0 and 0.03 or 0) + (wm and 0.15 or 0)) * (wm and 1 or help)
              if tp > 0 then
                addw(urg * f * fat - tc, "till", k, trow, "Hoeing %s for the %s crop: row %d is %d%% done (%d %s to go)", pl.name, pl.crop, trow, floor(tp * 100), tl, plural(tl, "row", "rows"))
              else
                addw(urg * f * fat - tc, "till", k, trow, "It is time to sow the %s crop and %s is not yet hoed (%d %s to go)", pl.crop, pl.name, tl, plural(tl, "row", "rows"))
              end
            end
          end
        end
      end
      if pl.stage == "growing" and pl.weeds > c.weeds_tend and (pl.tend_claim or 0) < 3 then
        local f = fit(p, ctx, "tend", k)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if f > 0 then
          addw((0.25 + 0.6 * pl.weeds + (wm and 0.15 or 0)) * (wm and 1 or (own and 1 or 0.45)) * f * fat - tc, "tend", k, 0,
            "Weeds are choking %s (%d%%)", pl.name, floor(pl.weeds * 100))
        end
      end
      if pl.stage == "ripe" then
        local row, hp = 0, -1
        local rp = pl.row_p
        for r = 1, pl.rows do
          if pl.row_st[r] == 2 and pl.row_claim[r] == 0 then
            local pr = rp and rp[r] or 0
            if pr > hp then row, hp = r, pr end
          end
        end
        if row > 0 then
          local f = fit(p, ctx, "harvest", k)
          local wm = ctx.with_mentor
          ctx.with_mentor = false
          if f > 0 then
            -- (the harvest is everyone's: neighbours reap together)
            local urg = (0.85 + min(0.15, (pl.ripe_for or 0) * 0.03) + (wm and 0.15 or 0)) * (own and 1 or 0.85)
            addw(urg * f * fat - tc, "harvest", k, row,
              "%s is ripe%s; the granary holds %d days of food", pl.name, (pl.ripe_for or 0) >= 2 and sfmt(" (%d days standing)", pl.ripe_for) or "", floor(need.food_days))
          end
        end
      end
      if s.granary and (pl.sheaves >= 2 or (pl.sheaves >= 0.05 and pl.stage == "stubble")) and (pl.haul_claim or 0) < pl.sheaves / c.carry_grain then
        local f = fit(p, ctx, "haul_grain", k)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if f > 0 then
          local urg = 0.7 + (s.rain_soon and 0.15 or 0) + (wm and 0.12 or 0)
          addw(urg * f * fat - tc - travel(ctx, s.granary_x, s.granary_y, cx, cy) * 0.5, "haul_grain", k, 0,
            "%d sheaves lie in %s; they belong in the granary", floor(pl.sheaves), pl.name)
        end
      end
    end
  end
  -- retted flax waiting on its field: carried to the granary (D-088)
  for k = 1, grown and #plots or 0 do
    local pl = plots[k]
    if s.granary and pl.stage == "stubble" and (pl.flax or 0) >= 0.05 and (pl.flax_claim or 0) < pl.flax / c.carry_flax then
      local f = fit(p, ctx, "haul_grain", k)
      ctx.with_mentor = false
      if f > 0 then
        local cx, cy = (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5
        addw((0.55 + (s.rain_soon and 0.15 or 0)) * f * fat - travel(ctx, cx, cy, px, py) - travel(ctx, s.granary_x, s.granary_y, cx, cy) * 0.5,
          "haul_flax", k, 0, "The flax on %s is retted (%.0f garments' worth): it belongs in the granary", pl.name, pl.flax)
      end
    end
  end
  -- wood: carry felled logs, fell trees
  local tr = s.trees
  if grown and need.lying_trees > 0 then
    local f0 = fit(p, ctx, "haul_logs")
    local best, bd, bwm = 0, 1e9, false
    for i = 1, #s.felled do
      local k = s.felled[i]
      if tr.st[k] == 2 and tr.logs[k] > 0 and tr.claim[k] == 0 then
        local d = dist(px, py, tr.x[k], tr.y[k])
        local wm = ctx.stage == "apprentice" and beside_mentor(W, ctx, "haul_logs", k)
        if wm then d = d - 0.5 end
        if d < bd then best, bd, bwm = k, d, wm end
      end
    end
    local f = bwm and 1.05 * (ctx.babe and kin.BABE_FIT.haul_logs or 1) or f0
    if best > 0 and f > 0 then
      bd = dist(px, py, tr.x[best], tr.y[best])
      local dest = need.site_logs > 0 and "a building site" or (s.wood_store and "the woodstore" or "our woodpile")
      if bwm then
        addw((0.52 + 0.2 * need.wood_short + (bwm and 0.15 or 0)) * f * fat - bd / ctx.spd * 0.45, "haul_logs", best, 0, "Carrying the logs %s cut to %s", ctx.mentor.name, dest)
      else
        addw((0.52 + 0.2 * need.wood_short + (bwm and 0.15 or 0)) * f * fat - bd / ctx.spd * 0.45, "haul_logs", best, 0, "%d %s lie where a tree was felled; %s needs them", tr.logs[best], plural(tr.logs[best], "log", "logs"), dest)
      end
    end
  end
  if need.fell > 0 or need.clear_trees > 0 then
    local f = fit(p, ctx, "fell")
    if f > 0 and ctx.stage == "adult" then
      -- (the walk to the tree only lowers it)
      local k0 = ((s.clearing or s.lot) and need.clear_trees > 0) and max(0.62, 0.35 + 0.5 * need.wood_short) or (0.35 + 0.5 * need.wood_short)
      add_lazy(k0 * f * fat, "fell", M.lazy_fell, f * fat)
    end
  end
  if need.clear_stumps > 0 then
    local f = fit(p, ctx, "grub")
    if f > 0 and (ctx.stage == "adult" or ctx.stage == "apprentice") then
      add_lazy(0.55 * f * fat, "grub", M.lazy_grub, f * fat)
    end
  end
  -- gathering at the woods' edge (berries, nuts, greens): food for the household
  -- (children gather too, from six, on their own claim: a household's child and its grown forager
  -- may both be at the edge, D-109)
  local fclaim = ctx.stage == "child" and "child_gathering" or "foraging"
  if ctx.home and ctx.hh and claim_free(W, ctx.hh, fclaim, p) then
    local f = fit(p, ctx, "forage")
    if f > 0 then
      -- worth going when the edge has something to pick (everyone sees what foragers bring back)
      local a = min(1, (s.wild or 0) / (c.forage_basket * 4))
      local short = need.food_days < 20 and 0.15 or 0
      local doy = s.doy or clock.doy(now)
      local fruit = doy >= 12 and doy < 36
      if a > 0.05 then
        add((0.12 + 0.32 * a + short) * f * fat, "forage", 0, 0,
          fruit and (a > 0.6 and "Berries and nuts are ripe at the woods' edge" or "Some berries are left at the woods' edge")
            or "Gathering greens and roots at the woods' edge")
      end
    end
  end
  -- (S19) the shore at low water: shellfish off the rocks and seaweed - food for the hungry
  if (s.shore_food or 0) > c.shore_basket and ctx.home and ctx.hh and ctx.stage ~= "child" and claim_free(W, ctx.hh, "shore", p) then
    local n = max(1, #ctx.hh.members)
    local larder = people.pantry(ctx.home) + (ctx.home.stock.meals or 0)
    local short = need.food_days < c.shore_short and (1 - need.food_days / c.shore_short) or 0
    if larder < n * 0.75 * 2 then short = short + 0.3 end
    if short > 0 then
      local f = fit(p, ctx, "forage")
      if f > 0 then
        local a = min(1, s.shore_food / (c.shore_basket * 4))
        addw((0.15 + 0.5 * short) * a * f * fat, "shore", 0, 0,
          "Food is short (%.0f days in the village): gathering shellfish and seaweed on the shore", need.food_days)
      end
    end
  end
  -- threshing the sheaves in the granary (under its roof: rain does not stop it): as the threshed
  -- grain runs down, and on wet and winter days when the fields have no work (D-080)
  if not grown then return end
  local g = s.granary
  if g and (g.stock.sheaves or 0) >= 3 and (g.threshers or 0) < 4 then
    local f = fit(p, ctx, "thresh", 1)
    local wm = ctx.with_mentor
    ctx.with_mentor = false
    if f > 0 and (ctx.stage == "adult" or ctx.stage == "elder" or wm) then
      local gdays = (g.stock.grain or 0) / max(1, (s.pop or 1) * 0.75)
      local low = max(0, 1 - gdays / c.thresh_days)
      local winter = (s.doy or 0) >= 36
      local urg = 0.12 + 0.55 * low + (ctx.rain > 0 and 0.2 or 0) + (winter and 0.22 or 0)
      if low > 0.3 then
        addw(urg * f * fatigue(p) - travel(ctx, s.granary_x, s.granary_y, px, py), "thresh", 0, 0, "Only %d days of threshed grain are left; %d FU of sheaves wait at the granary", floor(gdays), floor(g.stock.sheaves))
      elseif ctx.rain > 0 then
        addw(urg * f * fatigue(p) - travel(ctx, s.granary_x, s.granary_y, px, py), "thresh", 0, 0, "A wet day: threshing under the granary roof (%d FU of sheaves)", floor(g.stock.sheaves))
      else
        addw(urg * f * fatigue(p) - travel(ctx, s.granary_x, s.granary_y, px, py), "thresh", 0, 0, "%d FU of sheaves wait to be threshed at the granary", floor(g.stock.sheaves))
      end
    end
  end
  -- building sites
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.state == "site" then
      local bx, by = b.dx, b.dy
      local tc = travel(ctx, bx, by, px, py)
      local missing = b.logs_need - b.logs_have - (b.deliver_claim or 0) * 1.5
      if missing > 0 and (s.wood_store and s.wood_store.stock.logs or 0) > 0 then
        local f = fit(p, ctx, "deliver", b.id)
        ctx.with_mentor = false
        if f > 0 then
          addw((0.55 + 0.1 * b.prio) * f * fat - tc - travel(ctx, s.wood_x, s.wood_y, px, py) * 0.5, "deliver", b.id, 0,
            "The %s for %s still needs %d logs", b.label, b.for_name or "the village", b.logs_need - b.logs_have)
        end
      elseif missing > 0 and not s.wood_store and ctx.home and (ctx.home.stock.logs or 0) >= 2 then
        -- (S18: a young place with no woodstore: the logs on the household's own woodpile)
        local f = fit(p, ctx, "deliver", b.id)
        ctx.with_mentor = false
        if f > 0 then
          addw((0.55 + 0.1 * b.prio) * f * fat - tc - travel(ctx, ctx.home.dx, ctx.home.dy, px, py) * 0.5, "deliver", b.id, ctx.home.id,
            "The %s for %s still needs %d logs: there are logs on our woodpile", b.label, b.for_name or "the village", b.logs_need - b.logs_have)
        end
      end
      if b.logs_have >= min(b.logs_need, 4) and b.work_done < b.work_need and (b.builders or 0) < 3 then
        local f = fit(p, ctx, "build", b.id)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if b.kind == "boat" and b.for_hh == p.hh and f > 0 then f = max(f, 1.3) end
        if f > 0 and (ctx.stage == "adult" or ctx.stage == "elder" or wm) then
          if wm then
            addw((0.6 + 0.1 * b.prio + (wm and 0.15 or 0)) * f * fat - tc, "build", b.id, 0, "Helping %s raise the %s for %s", ctx.mentor.name, b.label, b.for_name or "the village")
          else
            addw((0.6 + 0.1 * b.prio + (wm and 0.15 or 0)) * f * fat - tc, "build", b.id, 0, "Raising the %s for %s (%d%% done)", b.label, b.for_name or "the village", floor(100 * b.work_done / b.work_need))
          end
        end
      end
    end
  end
  -- (S19) stone and iron
  M.stone_candidates(W, s, p, ctx, now, fat)
  M.iron_candidates(W, s, p, ctx, now, fat)
end

-- (S19) Stone (sim/agents/stone.lua): quarrying at an outcrop's face when the village wants stone,
-- carrying the stones that lie there (one at a time: heavy) to a site that wants them or to the yard
-- by the woodstore, bringing yard stones to a site, and a household shaping two hard stones into a
-- quern (in stints, at the yard) when it has none.
local function stone_candidates(W, s, p, ctx, now, fat)
  local c = C()
  local need = s.need
  if not s.has_rock and not s.wood_store then return end
  local px, py = p.x, p.y
  local yard = s.wood_store and s.wood_store.state == "standing" and s.wood_store or nil
  -- sites wanting stone
  local site, sw = nil, 0
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.state == "site" then
      local m = (b.stone_need or 0) - (b.stone_have or 0) - (b.stone_claim or 0)
      if m > sw then site, sw = b, m end
    end
  end
  -- stones lying at the faces (not yet claimed by a carrier) and where they would go
  if s.has_rock then
    local face, fd = nil, 1e9
    for k = 1, #s.blist do
      local b = s.buildings[s.blist[k]]
      if b.kind == "outcrop" and (b.stock.stone or 0) - (b.haul_claim or 0) >= 1 then
        local d = dist(px, py, b.dx, b.dy)
        if d < fd then face, fd = b, d end
      end
    end
    if face and (site or yard) then
      local f = fit(p, ctx, "haul_stone")
      if f > 0 then
        local to = site or yard
        addw((0.5 + (site and 0.12 or 0)) * f * fat - travel(ctx, face.dx, face.dy, px, py) - travel(ctx, to.dx, to.dy, face.dx, face.dy) * 0.4,
          "haul_stone", face.id, to.id, "A quarried stone lies at the %s; the %s wants it", face.label, site and site.label or "yard by the woodstore")
      end
    end
    -- quarrying: the stone the village wants and nobody has split yet
    if (need.stone or 0) > 0 and ctx.stage == "adult" then
      local f = fit(p, ctx, "quarry")
      if f > 0 then
        local hard = (need.hardstone or 0) > 0
        local best, bd = nil, 1e9
        for k = 1, #s.blist do
          local b = s.buildings[s.blist[k]]
          if b.kind == "outcrop" and (b.left or 0) > 0 and (b.quarriers or 0) < 2 and (not hard or b.rock == "hard") then
            local d = dist(px, py, b.dx, b.dy)
            if d < bd then best, bd = b, d end
          end
        end
        if best then
          addw((0.42 + 0.06 * min(4, need.stone) + (site and 0.1 or 0)) * f * fat - travel(ctx, best.dx, best.dy, px, py), "quarry", best.id, 0,
            "%s: splitting stone at the %s", need.stone_why or "The village wants stone", best.label)
        end
      end
    end
  end
  -- yard stones to a site
  if site and yard then
    local ys, yh = (yard.stock.stone or 0), (yard.stock.hardstone or 0)
    if ys + yh - (yard.stone_claim or 0) >= 1 then
      local f = fit(p, ctx, "deliver_stone")
      if f > 0 then
        addw((0.52 + 0.1 * site.prio) * f * fat - travel(ctx, yard.dx, yard.dy, px, py) - travel(ctx, site.dx, site.dy, yard.dx, yard.dy) * 0.4,
          "deliver_stone", site.id, yard.id, "The %s for %s wants %d stones for its footing; there are stones in the yard", site.label,
          site.for_name or "the village", (site.stone_need or 0) - (site.stone_have or 0))
      end
    end
  end
  -- a quern for the household: two hard stones from the yard shaped into one (or the work on it
  -- carried on)
  local hh, home = ctx.hh, ctx.home
  if yard and hh and home and (ctx.stage == "adult" or ctx.stage == "elder") and claim_free(W, hh, "querning", p) then
    local mine = (hh.quern_work or 0) > 0
    local lack = (hh.quern or 0) < c.quern_broken and (home.stock.quern or 0) < 1
    local trade = s.export and s.export.quern and s.quern_export_ok and (hh.quern or 0) >= c.quern_broken and (home.stock.quern or 0) < 1
    if mine or ((lack or trade) and (yard.stock.hardstone or 0) >= 2) then
      local f = fit(p, ctx, "quern")
      if f > 0 then
        local u = (lack and 0.45 or 0.3) * f * fat * (ctx.rain == 2 and 0.5 or 1) - travel(ctx, yard.dx, yard.dy, px, py)
        if lack then
          addw(u, "quern", yard.id, 0, "We grind our grain on a saddle stone: shaping a quern from two hard stones (%.0f of %.0f hours done)",
            hh.quern_work or 0, c.quern_h)
        else
          addw(u, "quern", yard.id, 0, "%s wants querns: shaping one to trade (%.0f of %.0f hours done)", s.export.quern, hh.quern_work or 0, c.quern_h)
        end
      end
    end
  end
end
M.stone_candidates = stone_candidates

-- (S19) Iron (sim/agents/iron.lua): digging ore at a known deposit, carrying ore and charcoal to the
-- forge, charging, firing and opening the charcoal clamp, smelting a bloom, forging iron edges for a
-- household that lacks them.
local function iron_candidates(W, s, p, ctx, now, fat)
  local c = C()
  local need = s.need
  if not (s.ore_known or s.forge) then return end
  if ctx.stage ~= "adult" and ctx.stage ~= "apprentice" then return end
  local px, py = p.x, p.y
  local f = s.forge
  local fup = f and f.state == "standing"
  local cl = s.clamp
  local cup = cl and cl.state == "standing"
  local adult = ctx.stage == "adult"
  -- ore: dig it, carry it
  if s.ore_known then
    local dig, dd, lie, ld = nil, 1e9, nil, 1e9
    for k = 1, #s.blist do
      local b = s.buildings[s.blist[k]]
      if (b.kind == "outcrop" or b.kind == "bog") and b.known then
        local d = dist(px, py, b.dx, b.dy)
        if (b.ore or 0) > 0 and (b.miners or 0) < 2 and d < dd then dig, dd = b, d end
        if (b.stock.ore or 0) - (b.ore_claim or 0) * c.carry_ore >= 1 and d < ld then lie, ld = b, d end
      end
    end
    if adult and dig and (need.ore or 0) > 0 then
      local fi = fit(p, ctx, "mine")
      if fi > 0 then
        addw((0.42 + 0.05 * min(4, need.ore)) * fi * fat - travel(ctx, dig.dx, dig.dy, px, py), "mine", dig.id, 0,
          "%s: digging iron ore at the %s", s.iron_why or "Iron is wanted", dig.label)
      end
    end
    if lie and fup then
      local fi = fit(p, ctx, "haul_iron")
      if fi > 0 then
        addw(0.48 * fi * fat - travel(ctx, lie.dx, lie.dy, px, py) - travel(ctx, f.dx, f.dy, lie.dx, lie.dy) * 0.4, "haul_iron", lie.id, 1,
          "Ore lies dug at the %s: carrying it to the forge", lie.label)
      end
    end
  end
  -- the clamp: charge it with logs, fire it, open it; its charcoal to the forge
  if cup then
    local tgt = s.clamp_target or 0
    if not cl.lit_until and (cl.charge or 0) + (cl.charge_claim or 0) < tgt and s.wood_store and (s.wood_store.stock.logs or 0) >= 1 then
      local fi = fit(p, ctx, "deliver")
      if fi > 0 then
        addw(0.45 * fi * fat - travel(ctx, s.wood_x, s.wood_y, px, py) - travel(ctx, cl.dx, cl.dy, s.wood_x, s.wood_y) * 0.4, "charge_clamp", cl.id, 0,
          "Charcoal is wanted at the forge: stacking logs in the clamp (%d of %d)", cl.charge or 0, tgt)
      end
    end
    if adult and not cl.lit_until and (cl.charge or 0) > 0 and (cl.charge or 0) >= tgt and (cl.firer or 0) == 0 then
      local fi = fit(p, ctx, "fire_clamp")
      if fi > 0 then
        addw(0.6 * fi * fat - travel(ctx, cl.dx, cl.dy, px, py), "fire_clamp", cl.id, 0,
          "The clamp is stacked with %d logs: covering it with turf and firing it", cl.charge)
      end
    end
    if adult and cl.lit_until and now >= cl.lit_until and (cl.firer or 0) == 0 then
      local fi = fit(p, ctx, "fire_clamp")
      if fi > 0 then
        addw(0.62 * fi * fat - travel(ctx, cl.dx, cl.dy, px, py), "open_clamp", cl.id, 0,
          "The clamp has burned through: opening it and raking out the charcoal")
      end
    end
    if fup and (cl.stock.charcoal or 0) - (cl.char_claim or 0) * c.carry_charcoal >= 1 then
      local fi = fit(p, ctx, "haul_iron")
      if fi > 0 then
        addw(0.46 * fi * fat - travel(ctx, cl.dx, cl.dy, px, py) - travel(ctx, f.dx, f.dy, cl.dx, cl.dy) * 0.4, "haul_iron", cl.id, 2,
          "%.0f sacks of charcoal lie at the clamp: carrying them to the forge", cl.stock.charcoal)
      end
    end
  end
  -- the forge: smelting, smithing
  if fup and adult and (f.smith or 0) == 0 then
    local fs = f.stock
    if (need.bloom or 0) > 0 and (fs.ore or 0) >= c.smelt_ore and (fs.charcoal or 0) >= c.smelt_charcoal then
      local fi = fit(p, ctx, "smith")
      if fi > 0 then
        addw(0.55 * fi * fat - travel(ctx, f.dx, f.dy, px, py), "smelt", f.id, 0,
          "Ore and charcoal at the forge: smelting a bloom of iron in the furnace")
      end
    elseif (need.iron or 0) > 0 and (fs.bloom or 0) >= 1 and (fs.charcoal or 0) >= c.forge_charcoal then
      local fi = fit(p, ctx, "smith")
      if fi > 0 then
        addw(0.58 * fi * fat - travel(ctx, f.dx, f.dy, px, py), "smith", f.id, 0,
          "A bloom at the forge: forging iron edges for tools (%s)", s.iron_why or "they are wanted")
      end
    end
  end
end
M.iron_candidates = iron_candidates

-- (S19) Fairs (sim/agents/fair.lua): setting out for a fair the household decided to go to (in
-- time to be there when it opens), and the host village's criers telling the neighbours.
local function fair_candidate(W, s, p, ctx, now, fat)
  local c = C()
  local hh = ctx.hh
  local fd = hh and hh.fair
  if p.trip or ctx.moving or not ctx.home then return end
  if fd and now < fd.t1 then
    local mine = false
    for _, id in ipairs(fd.who) do if id == p.id then mine = true end end
    local o = W.S.v3.settlements[fd.host]
    if mine and o then
      local hrs = 0
      if o ~= s then local _; _, hrs = trade.road(W, s, o) end
      local depart = fd.t0 - (o == s and 0.5 or hrs + (hrs > 8 and 14 or 0) + 2)
      -- (not setting out so late that the fair would be over when they came: put off by hunger at
      -- home, they stay)
      local late = o ~= s and now + M.road_dur(ctx.lh + 0.4, hrs, ctx.wake, ctx.bed) > fd.t1 - 6
      if now >= depart and not late and (o == s or (not ctx.night and M.journey_ok(W, s, p, ctx, hrs))) then
        add(0.95, "fair", o.id, 0, fd.why or ("To the fair at " .. o.name))
      end
    end
  end
  -- the host's criers: to each road neighbour, a grown-up walks to tell of the fair
  local f = s.fair
  if f and s.fair_cry and #s.fair_cry > 0 and now < f.t0 - 30 and ctx.stage == "adult" and not ctx.night and ctx.rain < 1 then
    local o = W.S.v3.settlements[s.fair_cry[1]]
    if o then
      local _, hrs = trade.road(W, s, o)
      if M.journey_ok(W, s, p, ctx, hrs) and (hrs <= ctx.sunset - ctx.wake and ctx.lh + hrs <= ctx.sunset + 0.5 or ctx.lh <= ctx.wake + 2.5) then
        addw((0.35 + 0.2 * p.soc) * fat, "cry", o.id, 0, "Our fair is in %.0f days: walking to %s to tell them (%.0f hours)", (f.t0 - now) / 24, o.name, hrs)
      end
    end
  end
end
M.fair_candidate = fair_candidate

-- The tree to fell (settle.next_tree: a scan of the woods) and its utility (lazy, see add_lazy).
function M.lazy_fell(c, ff)
  local W, s, p, ctx = LZ.W, LZ.s, LZ.p, LZ.ctx
  local need, tr = s.need, s.trees
  local k, clearing = settle.next_tree(W, s, p)
  if k == 0 then c.u = -1e9; return end
  local d = dist(p.x, p.y, tr.x[k], tr.y[k])
  c.a, c.b = k, 0
  local lot = clearing and settle.on_lot(s, tr.tile[k])
  if lot then
    c.u = 0.66 * ff - d / ctx.spd * 0.45
    local left = 0
    for _, j in ipairs(settle.clearing_trees(s) or {}) do if tr.st[j] == 1 and settle.on_lot(s, tr.tile[j]) then left = left + 1 end end
    c.why = sfmt("Clearing the ground for the %s (%d %s still standing)", lot.opts.label or settle.KINDS[lot.kind].label, left, plural(left, "tree", "trees"))
  elseif clearing then
    c.u = 0.62 * ff - d / ctx.spd * 0.45
    c.why = sfmt(s.expand_why == "spare" and "Clearing ground for a new field (%d %s still standing): hands to spare, and a bigger store against a bad year"
      or "Clearing ground for a new field (%d %s still standing): the harvest will not feed us a year",
      need.clear_trees, plural(need.clear_trees, "tree", "trees"))
  else
    c.u = (0.35 + 0.5 * need.wood_short) * ff - d / ctx.spd * 0.45
    c.why = need.fell_why
  end
end

-- The nearest stump on the new field ground (lazy).
function M.lazy_grub(c, ff)
  local s, p, ctx = LZ.s, LZ.p, LZ.ctx
  local tr = s.trees
  local best, bd = 0, 1e9
  local px, py = p.x, p.y
  local L = settle.clearing_trees(s) or {}
  for j = 1, #L do
    local k = L[j]
    if tr.st[k] == 3 and tr.claim[k] == 0 and s.plot[tr.tile[k]] == -1 then
      local d = dist(px, py, tr.x[k], tr.y[k])
      if d < bd then best, bd = k, d end
    end
  end
  if best == 0 then c.u = -1e9; return end
  c.a, c.b = best, 0
  c.u = 0.55 * ff - bd / ctx.spd * 0.45
  local lot = settle.on_lot(s, tr.tile[best])
  if lot then
    c.u = 0.6 * ff - bd / ctx.spd * 0.45
    c.why = sfmt("Digging out stumps where the %s will stand (%d left)", settle.KINDS[lot.kind].label, s.need.clear_stumps)
  else
    c.why = sfmt("Digging out stumps on the new field ground (%d left)", s.need.clear_stumps)
  end
end

-- Fishing (D-084): a free boat at the dock and fish offshore. The ground is chosen on the water
-- within reach of the dock (a different spot on different days); its fish density is the planet's
-- fish in that sea cell.
local function fish_ground(W, s, p, now)
  local b = s.dock
  local day = floor(now / 24)
  for k = 1, 16 do
    local a = rng.float(W.seed, rng.S.V3_THINK, p.key, day, 200 + k) * 8
    local r = 0.22 + 0.5 * rng.float(W.seed, rng.S.V3_THINK, p.key, day, 300 + k)
    local o = P8[floor(a) + 1]
    local x, y = b.mx + o[1] * r, b.my + o[2] * r
    local i = require("sim.agents.map").tile_of(s, x, y)
    if i > 0 and s.wcost[i] > 0 then return x, y, s.cell_of[i] end
  end
  return nil
end

-- (D-087) A household fishes in its own boat, for what it needs: fresh fish for the pot (it keeps a
-- day or two), smoked fish laid in for the winter, the village's bread when stores run low. A
-- boat too worn is not taken out.
local function own_boat(s, p, c)
  if not s.boats then return nil end
  for i = 1, #s.boats do
    local b = s.boats[i]
    if b.crew == 0 and not b.lost and not b.trip and not b.away and not b.move and (b.owner == p.hh or b.owner == 0) and (b.cond or 1) > c.boat_unsafe then return b end
  end
  return nil
end
M.own_boat = own_boat

local function fish_candidate(W, s, p, ctx, now, fat)
  local c = C()
  if not s.dock or not s.boats or ctx.rain == 2 or ctx.stage ~= "adult" then return end
  local boat = own_boat(s, p, c)
  if not boat then return end
  local f = fit(p, ctx, "fish")
  if f <= 0 then return end
  local gx, gy, cell = fish_ground(W, s, p, now)
  if not gx then return end
  local fd = W.F.fish[cell] or 0
  if fd < 0.03 then return end
  local home = ctx.home
  local hn = ctx.hh and #ctx.hh.members or 1
  local eat = max(1, hn * 0.75)
  local fresh = home and (home.stock.fish or 0) / eat or 0
  local pantry = home and (people.pantry(home) + (home.stock.meals or 0)) / eat or 0
  local need = pantry < 3 and 0.15 * (1 - pantry / 3) or 0
  -- the winter's store (late summer and autumn): smoked fish to the household's want
  local doy = s.doy or 0
  local smoked = home and (home.stock.smoked or 0) / eat or 0
  local store = (doy >= 16 and doy < 40 and smoked < c.smoke_days) and 0.12 * (1 - smoked / c.smoke_days) or 0
  local short = s.need.food_days < 30 and 0.12 or 0
  -- (what more fish is worth: the village's store of it, or what a neighbour would give, as last
  -- heard: a village swimming in smoked fish rows out less, D-101)
  local worth = s.fish_worth or 1
  local u = ((0.22 + 0.18 * min(1, fd)) * worth + need + store + short) * f * fat - 0.3 * min(2, fresh)
  if store > 0.03 then
    addw(u - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "fish", boat.id, 0, "Fishing for the winter: %.0f days of smoked fish in the house", smoked)
  elseif need > 0.05 then
    addw(u - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "fish", boat.id, 0, "Little food at home (%.1f days): out with the boat", pantry)
  else
    addw(u - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "fish", boat.id, 0, "The fish are running offshore (%.0f%% of a full sea); the boat is ready", min(100, fd * 100))
  end
end

-- Trade (D-097, D-101): the exchange worked out each morning (econ.daily: the best one by what
-- the village knows of its neighbours): a household's own goods (wool, cloth), or the village's
-- (food, logs) carried by one of its people. Both sides must gain; the trip must be worth the days.
local sea_legs, sea_wet, sea_risk
-- What a trader's boat still holds (a voyage is over only when the hold is carried home).
local function boat_cargo(W, p)
  local tr = p.trip
  local boat = tr and tr.sea and W.S.v3.settlements[p.sett].boats[tr.boat]
  local n = 0
  if boat and boat.cargo then for _, g in ipairs(econ.ORDER) do n = n + (boat.cargo[g] or 0) end end
  return n
end
-- A storm or gale at sea today at planet cell c (the planet's daily weather, D-059): no sailing.
local sea_storm = trade.sea_storm
M.sea_storm = sea_storm

-- (S19) Whether p can go on a journey that is not about food (news, a visit, courting, a trade that
-- brings home no food) of `hrs` hours each way: not hungry, the household able to spare their bread
-- for the road and still eat, and no hunger in the village (found following a father who walked
-- 26 hours with an empty larder in a famine and died on the road).
local function journey_ok(W, s, p, ctx, hrs)
  if p.hunger > 0.55 then return false end
  -- (not with a babe in arms: a minder does not carry someone's baby off on a long road)
  if ctx.babe then return false end
  if s.need and (s.need.food_days or 99) < 3 then return false end
  local home, hh = ctx.home, ctx.hh
  if not home or not hh then return false end
  -- (their larder and their part of the granary, which they can fetch from)
  local gr = s.granary and s.granary.state ~= "site" and (s.granary.stock.grain or 0) or 0
  local food = people.pantry(home) + (home.stock.meals or 0) + gr * #hh.members / max(1, #s.members)
  local days = (2 * hrs / 24 + 1)
  return food >= max(1, #hh.members) * 0.75 * days
end
M.journey_ok = journey_ok

local function trade_candidate(W, s, p, ctx, now, fat)
  if ctx.moving then return end
  if ctx.stage ~= "adult" or not ctx.home or ctx.babe or ctx.rain >= 1 or ctx.lh > ctx.sunset - 4 then return end
  -- (a mother with a baby at the breast does not leave it for days on the road)
  if (p.nursing or 0) > now then return end
  local hh = ctx.hh
  if p.trip or not claim_free(W, hh, "trading", p) then return end
  local plan, which = hh.tplan, 1
  local q = W.S.v3.people[s.trading or 0]
  local vfree = not (q and q.alive and q.trip)    -- (one trader at a time for the village's goods)
  if s.tplan and vfree and (not plan or s.tplan.net > plan.net) then plan, which = s.tplan, 2 end
  -- (by sea: the household's fisher sails its own boat with a mate, D-102)
  local sp = hh.splan
  if sp and (p.role == "fisher" or (p.sk_fish or 0) > 0.4) and (sp.pool == "house" or vfree) and (not plan or sp.net > plan.net) then
    local o = W.S.v3.settlements[sp.o]
    local r = o and trade.sea_route(W, s, o)
    if r and not trade.sea_rough(W, r, now) and econ.own_boat(s, hh) then plan, which = sp, 3 end
  end
  if not plan then return end
  local o = W.S.v3.settlements[plan.o]
  if not o or not o.alive then return end
  local GX, GH = econ.GOODS[plan.give], econ.GOODS[plan.take]
  if not plan.ask and not GH.food and not journey_ok(W, s, p, ctx, plan.hours) then return end
  if which ~= 3 then
    -- (S14) a road is walked by day: set out when it can be walked before dusk, or early in the
    -- morning when it is more than a day's walk (a night camped on the road)
    if plan.hours <= ctx.sunset - ctx.wake then
      if ctx.lh + plan.hours > ctx.sunset + 0.5 then return end
    elseif ctx.lh > ctx.wake + 2.5 then return end
  end
  if plan.ask then
    if which == 3 then
      addw(0.55 * fat, "trade", plan.o, which, "Food for %.0f days left in the village and nothing to sell: sailing to ask %s for help (%.0f hours at sea)",
        plan.days, o.name, plan.hours)
    else
      addw(0.55 * fat, "trade", plan.o, which, "Food for %.0f days left in the village and nothing to sell: going to ask %s for help (%.0f hours' walk)",
        plan.days, o.name, plan.hours)
    end
    return
  end
  -- (S19: a big gain pulls harder: up to 0.15 more for a gain of ten FU and more)
  local u = (0.3 + 0.3 * min(1, plan.net / max(0.5, plan.cost)) + 0.15 * min(1, plan.net / 10)) * (p.sex == "m" and 1 or 0.8) * fat
  -- (fresh news, or a look on the chance: S14)
  local heard = (plan.w or 1) >= 0.5
  local what = heard and sfmt("%s wanted %s when last heard of", o.name, GX.plural and "them" or "it")
    or sfmt("nobody has been to %s lately: going to see whether anyone there wants %s", o.name, GX.plural and "them" or "it")
  if which == 3 then
    addw(u, "trade", plan.o, which,
      "%s %s worth little here (%s to spare) and %s; sailing over (%.0f hours at sea) for %s",
      (plan.pool == "house" and "Our " or "The village's ") .. GX.name, GX.plural and "are" or "is", econ.amount(plan.n, plan.give), what,
      plan.hours, GH.name)
  else
    addw(u, "trade", plan.o, which,
      "%s %s worth little here (%s to spare) and %s (%.0f hours' walk); bringing back %s",
      (which == 1 and "Our " or "The village's ") .. GX.name, GX.plural and "are" or "is", econ.amount(plan.n, plan.give), what,
      plan.hours, GH.name)
  end
end
M.trade_candidate = trade_candidate

-- (S20, R6a, D-149) Taking a beast from another village (conflict.take_weigh weighs it): a grown-up
-- fit for the road, of a household in hardship or holding a grievance, setting out in the morning.
local function steal_candidate(W, s, p, ctx, now, fat)
  if ctx.moving or p.trip or p.at or ctx.stage ~= "adult" or ctx.babe or not ctx.home or not ctx.hh then return end
  if (p.nursing or 0) > now or (p.preg_due or 0) > 0 or p.health < 0.6 or p.hunger > 0.8 or (p.hurt or 0) > 0.2 then return end
  if ctx.lh > ctx.wake + 3 or ctx.rain >= 2 then return end
  if not claim_free(W, ctx.hh, "taking", p) then return end
  local b = conflict.take_weigh(W, s, p, ctx.hh, now)
  if not b then return end
  add(min(0.95, 0.25 + 0.6 * b.net) * fat, "steal", b.o, b.hi, b.why)
end
M.steal_candidate = steal_candidate

-- (S20, R6b) Asking redress for a wrong (conflict.claim_weigh): a grown-up fit for the road of the
-- household that bore it, in the morning; the more it weighs and the more the villages trust each
-- other, the likelier they go and ask rather than wait.
function M.claim_candidate(W, s, p, ctx, now, fat)
  if ctx.moving or p.trip or p.at or ctx.stage ~= "adult" or ctx.babe or not ctx.home or not ctx.hh then return end
  if (p.nursing or 0) > now or (p.preg_due or 0) > 0 or p.health < 0.6 or (p.hurt or 0) > 0.2 then return end
  if ctx.lh > ctx.wake + 3 or ctx.rain >= 2 or not journey_ok(W, s, p, ctx, 12) then return end
  -- (the men first: a woman goes where her household has no man of age)
  if p.sex ~= "m" and kin.has_man(W, ctx.hh, now) then return end
  local b = conflict.claim_weigh(W, s, ctx.hh, now)
  if not b then return end
  local trust = econ.rel(s, b.o).trust
  local who = deeds.told(W, s, b.d, b.e)
  addw((0.3 + 0.35 * min(1, b.w / 20) + 0.25 * trust) * fat, "claim", b.d.id, b.o.id,
    "%s took %s of ours: going to %s to ask for %s back (%.0f hours' walk)", who, b.d.n > 1 and sfmt("%d %s", b.d.n, conflict.NOUN[b.d.what] or b.d.what) or (conflict.ONE[b.d.what] or b.d.what),
    b.o.name, b.d.n > 1 and "them" or "it", b.hrs)
end

-- (S16, R4) Courting (life.lua): a man with nobody to marry at home walks to the village he chose,
-- setting out early enough to walk it by day.
local function court_candidate(W, s, p, ctx, now, fat)
  if ctx.moving then return end
  local cr = p.court
  if not cr or p.trip or ctx.stage ~= "adult" or not ctx.home or ctx.rain >= 1 or ctx.night then return end
  local o = W.S.v3.settlements[cr.to]
  if not o or #o.members == 0 or now - cr.t > 24 * 12 then p.court = nil; return end
  local _, hrs = trade.road(W, s, o)
  if not journey_ok(W, s, p, ctx, hrs) then return end
  if hrs <= ctx.sunset - ctx.wake then
    if ctx.lh + hrs > ctx.sunset + 0.5 then return end
  elseif ctx.lh > ctx.wake + 2.5 then return end
  -- (put off while the work presses, more pressing every day he waits)
  addw((0.6 + 0.12 * min(6, (now - cr.t) / 24)) * fat, "court", o.id, 0, "Nobody to marry at home: going to %s to court (%.0f hours' walk)", o.name, hrs)
end
M.court_candidate = court_candidate

-- (S16, R4) Visiting kin who live at another village (a daughter married away, parents left
-- behind): after `visit_days` apart the wish to see them grows each day; set out when the road can
-- be walked by day. Not a mother with a baby at the breast.
local function visit_candidate(W, s, p, ctx, now, fat)
  if ctx.moving then return end
  local c = C()
  if p.trip or p.court or ctx.stage ~= "adult" and ctx.stage ~= "elder" or not ctx.home or ctx.rain >= 1 or ctx.night then return end
  if (p.nursing or 0) > now then return end
  local V = W.S.v3
  local best, bd, bo, bh
  local function try(qid)
    local q = V.people[qid or 0]
    local o = q and q.alive and q.sett ~= p.sett and not q.at and V.settlements[q.sett]
    if not o then return end
    local _, hrs = trade.road(W, s, o)
    if not hrs or hrs > c.court_road_h then return end
    local apart = (now - max(p.visit_t or 0, q.married or 0, p.married or 0)) / 24
    if apart >= c.visit_days and (not best or apart > bd) then best, bd, bo, bh = q, apart, o, hrs end
  end
  try(p.mother)
  try(p.father)
  for _, k in ipairs(p.kids or {}) do try(k) end
  if not best then return end
  if not journey_ok(W, s, p, ctx, bh) then return end
  if bh <= ctx.sunset - ctx.wake then
    if ctx.lh + bh > ctx.sunset + 0.5 then return end
  elseif ctx.lh > ctx.wake + 2.5 then return end
  local rel = best.id == p.mother and "my mother" or (best.id == p.father and "my father" or (best.sex == "f" and "my daughter" or "my son"))
  addw((0.25 + 0.04 * min(10, bd - c.visit_days)) * fat, "visit_kin", bo.id, best.id,
    "%.0f days since I saw %s: walking to %s to visit %s (%.0f hours)", bd, best.name, bo.name, rel, bh)
end
M.visit_candidate = visit_candidate
M.share_candidate = function(...) return share_candidate(...) end

-- (S17) Kin living at other villages: parents, children, brothers and sisters (each once).
local KIN_TMP = {}
local function kin_away(W, p)
  local V = W.S.v3
  local out = KIN_TMP
  for i = #out, 1, -1 do out[i] = nil end
  local function add_k(id, rel)
    local q = V.people[id or 0]
    if q and q.alive and q ~= p and q.sett ~= p.sett and not q.at then
      for i = 1, #out do if out[i].q == q then return end end
      out[#out + 1] = { q = q, rel = rel }
    end
  end
  add_k(p.mother, "mother")
  add_k(p.father, "father")
  for _, k in ipairs(p.kids or {}) do
    local q = V.people[k]
    add_k(k, q and (q.sex == "f" and "daughter" or "son") or "child")
  end
  for _, pid in ipairs({ p.mother, p.father }) do
    local par = V.people[pid or 0]
    for _, k in ipairs(par and par.kids or {}) do
      local q = V.people[k]
      add_k(k, q and (q.sex == "f" and "sister" or "brother") or "sibling")
    end
  end
  return out
end
M.kin_away = kin_away

-- (S17) Asking kin at another village for help: a grown-up of a household whose larder is nearly
-- empty while its own village has little to give either walks to a parent, child, brother or sister
-- living elsewhere (the nearest, the closest of them) to ask for food - and, when things are very bad
-- at home, they may offer the household a place under their roof. Not the same kin again for
-- kin_ask_again days.
local function ask_kin_candidate(W, s, p, ctx, now, fat)
  if ctx.moving or ctx.babe then return end
  local c = C()
  local hh = ctx.hh
  if p.trip or p.court or ctx.stage ~= "adult" or not ctx.home or not hh or ctx.rain >= 2 or ctx.night then return end
  if not claim_free(W, hh, "asking", p) or (hh.trading or 0) ~= 0 then return end
  local home = ctx.home
  local n = max(1, #hh.members)
  local days = (people.pantry(home) + (home.stock.meals or 0)) / (n * 0.75)
  local vdays = s.need and s.need.food_days or 99
  if days >= c.kin_ask_days or vdays >= c.kin_ask_village then return end
  local V = W.S.v3
  local day = floor(now / 24)
  local best, bs, bo, bh, brel
  for _, e in ipairs(kin_away(W, p)) do
    local q = e.q
    local o = V.settlements[q.sett]
    local kh = V.households[q.hh]
    local asked = hh.kin_asked and hh.kin_asked[q.hh]
    if o and kh and o.buildings[kh.home or 0] and not (asked and day - asked < c.kin_ask_again) then
      local _, hrs = trade.road(W, s, o)
      if hrs and hrs <= c.kin_reach_h then
        local close = (e.rel == "mother" or e.rel == "father" or e.rel == "son" or e.rel == "daughter") and 1 or 0.8
        local sc = close * (0.6 + people.affinity(p, q.id)) - hrs * 0.01
        if not best or sc > bs then best, bs, bo, bh, brel = q, sc, o, hrs, e.rel end
      end
    end
  end
  if not best then return end
  if bh <= ctx.sunset - ctx.wake then
    if ctx.lh + bh > ctx.sunset + 0.5 then return end
  elseif ctx.lh > ctx.wake + 2.5 then return end
  addw((0.6 + 0.5 * (1 - days / c.kin_ask_days)) * fat, "ask_kin", bo.id, best.id,
    "Food for %.1f days at home and the village has little: walking to %s to ask my %s %s for help (%.0f hours)",
    days, bo.name, brel, best.name, bh)
end
M.ask_kin_candidate = ask_kin_candidate

-- (S17) Going to see the neighbours: when the village has heard nothing of a neighbour for long
-- (its news faded under `news_stale`), nobody from here has gone lately (`news_again` days) and hands
-- are idle, a sociable grown-up may walk there to see how they fare and what they want, and tell how
-- things stand at home - the news both ways and what each knows of places. One at a time a
-- neighbour.
local function news_candidate(W, s, p, ctx, now, fat)
  if ctx.moving then return end
  local c = C()
  if p.trip or p.court or ctx.stage ~= "adult" or not ctx.home or ctx.rain >= 1 or ctx.night then return end
  if (p.nursing or 0) > now or (s.idle_avg or 0) < c.news_idle then return end
  local day = floor(now / 24)
  local best, bf, bh
  for _, o in ipairs(trade.partners(W, s)) do
    local fr = econ.fresh(W, s, o, now)
    local went = s.news_went and s.news_went[o.id]
    local k = s.know and s.know[o.id]
    local old = not k or (now - k.t) / 24 >= c.news_days
    if old and not (went and day - went < c.news_again) then
      local _, hrs = trade.road(W, s, o)
      if hrs <= c.court_road_h and (not best or fr < bf) then best, bf, bh = o, fr, hrs end
    end
  end
  if not best then return end
  if not journey_ok(W, s, p, ctx, bh) then return end
  if bh <= ctx.sunset - ctx.wake then
    if ctx.lh + bh > ctx.sunset + 0.5 then return end
  elseif ctx.lh > ctx.wake + 2.5 then return end
  local k = s.know and s.know[best.id]
  local days = k and (now - k.t) / 24 or 99
  addw((0.08 + 0.18 * p.soc + 0.12 * (1 - bf)) * fat, "news", best.id, 0,
    "Nothing heard from %s for %.0f days: walking there to see how they fare and what they want (%.0f hours)", best.name, days, bh)
end
M.news_candidate = news_candidate

-- Sharing (D-101): plenty taken to a household of the village that has little (econ.plan_gift).
local function share_candidate(W, s, p, ctx, now, fat)
  local hh = ctx.hh
  local g = hh and hh.gift
  if not g or not ctx.home or ctx.night or ctx.rain >= 2 then return end
  if ctx.stage ~= "adult" and ctx.stage ~= "apprentice" then return end
  if not claim_free(W, hh, "sharing", p) then return end
  local o = W.S.v3.households[g.to]
  local oh = o and o.home ~= 0 and s.buildings[o.home]
  if not oh then return end
  local q1 = W.S.v3.people[o.members[1] or 0]
  addw((0.22 + 0.25 * g.need) * fat - travel(ctx, oh.dx, oh.dy, p.x, p.y) - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5,
    "share", g.to, 0, "The %s household has little %s and we have plenty: taking them %s", q1 and q1.surname or "neighbour",
    econ.GOODS[g.g].name, econ.amount(g.q, g.g))
end

-- Crewing (D-096): a skipper waiting at the dock for a mate - one of the household first (a son,
-- a husband, a daughter), else a neighbour for a share of the catch.
local function crew_candidate(W, s, p, ctx, now, fat)
  local c = C()
  if not s.dock or not s.boats or ctx.rain == 2 then return end
  local st = ctx.stage
  if not (st == "adult" or st == "apprentice" or (st == "child" and ctx.age >= c.help_age + 4)) then return end
  -- (S18: not with a babe in arms - it would go hungry at sea)
  if ctx.babe then return end
  for _, boat in ipairs(s.boats) do
    local q = (boat.mate_want or 0) ~= 0 and (boat.mate or 0) == 0 and W.S.v3.people[boat.mate_want]
    if q and q.alive and q ~= p and q.act == "wait_mate" and q.boat == nil then
      local own = q.hh == p.hh
      local f = (own and 1.2 or 0.75) * (p.sex == "m" and 1 or 0.7) * (st == "apprentice" and 1.1 or 1)
      addw((0.5 + (own and 0.15 or 0)) * f * fat - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "crew", boat.id, q.id,
        own and "%s is waiting at the dock: out with the boat as the second hand" or "%s needs a second hand for the net (a share of the catch)", q.name)
      return
    end
  end
end

-- Drying racks (D-096): a fishing household with a rack by the shore hangs its surplus fish to dry
-- there in fair weather (no wood), and fetches it home when it is dry.
local function rack_of(s, hh)
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "rack" and b.state == "standing" and b.hh == hh.id then return b end
  end
  return nil
end
M.rack_of = rack_of

local function rack_candidates(W, s, p, ctx, now, fat)
  local c = C()
  local hh, home = ctx.hh, ctx.home
  if not hh or not home or ctx.stage == "infant" or (ctx.stage == "child" and ctx.age < c.help_age + 1) then return end
  local rk = rack_of(s, hh)
  if not rk then return end
  local n = #hh.members
  local surplus = (home.stock.fish or 0) - n * 0.8
  if surplus >= c.dry_min and ctx.rain == 0 and claim_free(W, hh, "smoking", p) then
    addw((0.32 + 0.1 * min(4, surplus)) * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) - travel(ctx, rk.dx, rk.dy, ctx.hx, ctx.hy) * 0.5,
      "dry", rk.id, 0, "%.0f FU of fresh fish will not keep: hanging it on our rack by the shore", surplus)
  end
  if (rk.stock.dried or 0) >= c.dry_fetch and claim_free(W, hh, "fetch_food", p) then
    addw((0.3 + 0.05 * min(6, rk.stock.dried)) * fat - travel(ctx, rk.dx, rk.dy, p.x, p.y), "fetch_dried", rk.id, 0,
      "%.0f FU of fish has dried on the rack: carrying it home", rk.stock.dried)
  end
end

-- Mending a household's boat at the dock (D-087): worn planks and caulking, a log and a few hours.
local function mend_boat_candidate(W, s, p, ctx, now, fat)
  local c = C()
  if not s.dock or not s.boats or ctx.stage ~= "adult" then return end
  local f = fit(p, ctx, "mend_boat")
  if f <= 0 then return end
  for i = 1, #s.boats do
    local b = s.boats[i]
    if b.crew == 0 and not b.lost and b.owner == p.hh and (b.cond or 1) < c.boat_mend and (b.mender or 0) == 0
      and ((b.repair_logs or 0) >= 1 or (s.wood_store and (s.wood_store.stock.logs or 0) >= 1)) then
      addw((0.3 + 0.9 * (c.boat_mend - b.cond)) * f * fat - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "mend_boat", b.id, 0,
        "Our boat is leaking (%d%% sound): it wants new planks and caulking", floor((b.cond or 0) * 100))
      return
    end
  end
end

-- Repairs (D-080): a weathered house (condition below repair_at) wants a log and some hours.
local function repair_candidates(W, s, p, ctx, now, fat)
  local c = C()
  if (s.need.repairs or 0) == 0 or ctx.stage ~= "adult" then return end
  local f = fit(p, ctx, "repair")
  if f <= 0 then return end
  local best, bd = nil, 1e9
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" and (b.cond or 1) < c.repair_at and (b.repairer or 0) == 0 then
      local d = dist(p.x, p.y, b.dx, b.dy)
      if d < bd then best, bd = b, d end
    end
  end
  if not best then return end
  if (best.repair_logs or 0) < c.repair_logs and not (s.wood_store and (s.wood_store.stock.logs or 0) >= c.repair_logs) then return end
  local hh = W.S.v3.households[best.hh]
  local q = hh and W.S.v3.people[hh.members[1] or 0]
  addw((0.36 + 0.8 * (c.repair_at - best.cond)) * f * fat - bd / ctx.spd * 0.45, "repair", best.id, 0,
    "The roof of the %s house lets the weather in (%d%% sound)", q and q.surname or "empty", floor(best.cond * 100))
end

-- Tools (D-080): the household's tools wear with work; worn tools slow it. Mending and making them
-- (handles, baskets, a new edge) is done at home, by the door when the weather is fair.
local function tool_factor(W, p)
  local hh = W.S.v3.households[p.hh]
  -- (S19) iron edges make the work go faster
  local ir = hh and (hh.iron or 0) >= C().iron_broken and hh.iron or 0
  return (0.55 + 0.45 * (hh and hh.tools or 1)) * (1 + C().iron_speed * ir)
end
M.tool_factor = tool_factor

local function craft_candidate(W, s, p, ctx, now)
  local c = C()
  local hh, home = ctx.hh, ctx.home
  if not hh or not home or ctx.stage == "child" then return end
  local tools = hh.tools or 1
  if tools > 0.92 or (home.stock.logs or 0) < 0.2 then return end
  local f = ctx.stage == "elder" and 1.25 or (ctx.stage == "apprentice" and 0.8 or 1)
  local u = (0.1 + 0.75 * (1 - tools)) * f * fatigue(p) * (ctx.rain == 2 and 0.6 or 1)
  if tools < 0.6 then
    addw(u - travel(ctx, ctx.hx, ctx.hy, p.x, p.y), "craft", home.id, 0, "The household's tools are worn (%d%%): a hoe handle is split, the sickle blunt", floor(tools * 100))
  else
    addw(u - travel(ctx, ctx.hx, ctx.hy, p.x, p.y), "craft", home.id, 0, "Mending tools and weaving baskets (the household's tools at %d%%)", floor(tools * 100))
  end
end

local CAMP_STOCK = { "food", "seed", "meals", "meat", "fish", "milk", "smoked", "cheese", "logs", "hides", "leather", "wool", "cloth" }

-- Household chores and care.
local function chores(W, s, p, ctx, now)
  local c = C()
  local hh, home = ctx.hh, ctx.home
  if not hh or not home then return end
  local wx = ctx.rain == 2 and 0.15 or (ctx.rain == 1 and 0.6 or 1)
  local fat = fatigue(p) * wx
  local px, py = p.x, p.y
  local n = #hh.members
  local need_day = n * 1.0
  local food = people.pantry(home)
  -- cooking: the household's meals at the hearth (one cook at a time; needs wood on the hearth)
  local meals = home.stock.meals or 0
  local want = n * c.meals_days * 0.75
  if food >= 0.3 and meals < want and claim_free(W, hh, "cooking", p) and ((home.stock.logs or 0) > 0 or home.fire) then
    local f = fit(p, ctx, "cook")
    if f > 0 then
      local soon = (ctx.lh > ctx.sunset - 2.5 or ctx.lh < ctx.wake + 1.2) and 0.15 or 0
      if meals < c.meal_fu then
        add((0.3 + 0.45 * (1 - meals / want) + soon) * f * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, px, py), "cook", home.id, 0, "Nothing cooked at home: baking bread and boiling porridge")
      else
        addw((0.3 + 0.45 * (1 - meals / want) + soon) * f * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, px, py), "cook", home.id, 0, "Cooking for the household (%.1f meals left for %d)", meals / c.meal_fu, n)
      end
    end
  end
  local gran = s.granary
  -- (bread too: a house full of fish and meat still wants bread for the pot, D-101)
  local bread = (home.stock.food or 0) / max(0.1, need_day * (1 - c.diet_p))
  if gran and (gran.stock.grain or 0) > 1 and (food < need_day * c.pantry_low or bread < c.pantry_low * 0.5) and claim_free(W, hh, "fetch_food", p) then
    local f = fit(p, ctx, "fetch_food")
    if f > 0 then
      local days = food / max(1, need_day)
      if days >= c.pantry_low then
        addw((0.5 + 0.3 * (1 - min(1, bread / c.pantry_low))) * f * fat - travel(ctx, s.granary_x, s.granary_y, px, py),
          "fetch_food", 0, 0, "Bread for %.1f days left at home (and fish and meat for longer)", bread)
      else
        addw((0.55 + 0.4 * (1 - min(1, days / c.pantry_low))) * f * fat - travel(ctx, s.granary_x, s.granary_y, px, py),
          "fetch_food", 0, 0, "Only %.1f days of food left at home", days)
      end
    end
  end
  -- firewood: the hearth burns every day (cooking; more when it is chilly)
  local logs = home.stock.logs or 0
  local burn = home.burn or c.fuel_cook
  local wdays = logs / max(0.1, burn)
  if wdays < c.woodpile_days and claim_free(W, hh, "fetch_wood", p) and s.wood_store and (s.wood_store.stock.logs or 0) >= 1 then
    local f = fit(p, ctx, "fetch_wood")
    if f > 0 then
      local urg = 0.32 + 0.4 * (1 - wdays / c.woodpile_days) + (s.cold and 0.15 * s.cold_k or 0)
      if wdays < 1 then
        addw(urg * f * fat - travel(ctx, s.wood_x, s.wood_y, px, py), "fetch_wood", 0, 0, "The woodpile at home is almost gone (%.1f logs; the hearth burns %.1f a day)", logs, burn)
      else
        addw(urg * f * fat - travel(ctx, s.wood_x, s.wood_y, px, py), "fetch_wood", 0, 0, "Stacking firewood at home: %.1f days of burning left", wdays)
      end
    end
  end
  -- (S17) the camp lived at until the house stood: its things carried in, a load at a time
  local oc = hh.old_camp and s.buildings[hh.old_camp]
  if oc and oc.state == "standing" and home.kind == "house" and claim_free(W, hh, "moving_in", p) and ctx.stage ~= "child" then
    local left = 0
    for _, k in ipairs(CAMP_STOCK) do left = left + (oc.stock[k] or 0) end
    if left > 0.05 then
      local f = fit(p, ctx, "fetch_food")
      if f > 0 then
        addw((0.5 + (ctx.rain > 0 and 0.15 or 0)) * f * fat - travel(ctx, oc.cx, oc.cy, px, py), "strike_camp", oc.id, 0,
          oc.kind == "camp" and "Our hut stands: carrying our things in from the camp (%.0f FU of food and stores left there)"
            or "Our new house stands: carrying our things over from the hut (%.0f FU of food and stores left there)", left)
      end
    end
  end
  -- fresh fish beyond what the pot will take today is smoked at the hearth before it spoils (D-087)
  local surplus = (home.stock.fish or 0) + (home.stock.meat or 0) - n * 0.8
  -- (with a rack by the shore and fair weather, fish is hung to dry instead: D-096)
  local racked = ctx.rain == 0 and (home.stock.fish or 0) > (home.stock.meat or 0) and M.rack_of(s, hh)
  local salt = home.stock.salt or 0
  if surplus >= 1 and not racked and claim_free(W, hh, "smoking", p) and ((home.stock.logs or 0) >= 0.3 or salt >= C().salt_cure) then
    local f = fit(p, ctx, "smoke")
    if f > 0 then
      -- (S18: salt cures it faster and without the fire)
      local what = (home.stock.meat or 0) > (home.stock.fish or 0) and "meat" or "fish"
      addw((0.28 + 0.1 * min(4, surplus)) * f * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, px, py), "smoke", home.id, 0,
        salt >= C().salt_cure and "%.0f FU of fresh %s will not keep: salting it" or "%.0f FU of fresh %s will not keep: smoking it over the hearth", surplus, what)
    end
  end
  if (home.stock.water or 0) < 1 and claim_free(W, hh, "fetch_water", p) and not s.well and s.has_river then
    -- (S17) no well (a young place): water from the river bank nearest home
    local f = fit(p, ctx, "fetch_water")
    if f > 0 then
      local bi, bd = require("sim.agents.map").house_bank(s, home)
      if bi > 0 then
        local bx, by = require("sim.agents.map").tile_xy(s, bi)
        addw(0.5 * f * fat - travel(ctx, bx, by, px, py), "fetch_water", bi, 1,
          "The water jar is empty: to the river (%.0f paces; no well dug yet)", bd * 400)
      end
    end
  end
  if (home.stock.water or 0) < 1 and claim_free(W, hh, "fetch_water", p) and s.well then
    local f = fit(p, ctx, "fetch_water")
    if f > 0 then
      -- (the well's clean water, unless the river is far nearer: D-095)
      local wd = dist(home.dx, home.dy, s.well.dx, s.well.dy)
      local bi, bd = 0, 1e9
      if s.has_river then bi, bd = require("sim.agents.map").house_bank(s, home) end
      -- (a household that fell sick from the river's water keeps to the well for a while: D-107)
      local shun = hh.river_bad and floor(now / 24) - hh.river_bad < C().river_shun_days
      if bi > 0 and bd * C().river_water_k < wd and not shun then
        local bx, by = require("sim.agents.map").tile_xy(s, bi)
        addw(0.45 * f * fat - travel(ctx, bx, by, px, py), "fetch_water", bi, 1,
          "The water jar at home is empty (the river is nearer than the well: %.0f against %.0f paces)", bd * 400, wd * 400)
      elseif shun and bi > 0 and bd * C().river_water_k < wd then
        add(0.45 * f * fat - travel(ctx, s.well.dx, s.well.dy, px, py), "fetch_water", 0, 0,
          "The water jar at home is empty: to the well, not the river, since the flux")
      else
        add(0.45 * f * fat - travel(ctx, s.well.dx, s.well.dy, px, py), "fetch_water", 0, 0,
          "The water jar at home is empty")
      end
    end
  end
  -- angling from the river bank (D-095): a fair day, little fresh fish or meat at home, the river's
  -- fish there to be caught (anyone from ten; children and elders most)
  local c = C()
  if s.has_river and (s.river_fish or 0) > c.angle_min and ctx.rain == 0 and not ctx.night and ctx.lh < ctx.sunset - 1.5
    and ctx.stage ~= "infant" and not (ctx.stage == "child" and ctx.age < c.help_age + 1) and claim_free(W, hh, "angling", p) then
    local fresh = (home.stock.fish or 0) + (home.stock.meat or 0)
    local nm = #hh.members
    if fresh < nm * 0.6 then
      local bi, bd = require("sim.agents.map").house_bank(s, home)
      if bi > 0 and bd < c.angle_reach then
        local share = min(1, (s.river_fish or 0) / max(1, s.river_fish_cap or 1))
        local fa = (ctx.stage == "child" or ctx.stage == "elder") and 1.0 or (ctx.stage == "apprentice" and 0.9 or 0.6)
        local bx, by = require("sim.agents.map").tile_xy(s, bi)
        addw((0.26 + 0.2 * share) * fa * fat - travel(ctx, bx, by, px, py), "angle", bi, 0,
          "Little fresh food at home: fishing in the river (it is %s today)", share > 0.6 and "full of fish" or "fished low")
      end
    end
  end
end

-- Textiles (D-088): the household's linen from flax to clothes. Clothes wear; the household wants
-- a pipeline of fibre, yarn and cloth (textile_want a person in garments' worth) and dresses,
-- spins, weaves and sews toward it — the slack season's work, and the evening's by the hearth or
-- among the neighbours at the well.
local function textile_candidates(W, s, p, ctx, now)
  local c = C()
  local hh, home = ctx.hh, ctx.home
  if not hh or not home or ctx.stage == "infant" then return end
  if ctx.stage == "child" and ctx.age < c.help_age then return end
  local V = W.S.v3
  local n = #hh.members
  -- clothes the household needs: the worn ones, in garments
  local def, worst, wq = 0, 1, nil
  for i = 1, n do
    local q = V.people[hh.members[i]]
    if q and q.alive and q.act ~= "carried" then
      local cl = q.clothes or 1
      if cl < 0.7 then def = def + (0.7 - cl) / 0.7 end
      if cl < worst then worst, wq = cl, q end
    end
  end
  local st = home.stock
  local fib, yarn, cloth = st.fibre or 0, st.yarn or 0, st.cloth or 0
  -- (cloth is a household's store of wealth: linen, the children's next clothes, a daughter's
  -- chest; a year's clothes a member in hand before the spindles rest)
  local want = max(1.5, n * c.cloth_keep)
  -- (S17) working for exchange: a neighbour heard to want cloth more than it is worth here
  local trade_for = s.export and s.export.cloth
  if trade_for then want = want + n * c.export_cloth end
  local urg = min(1, def / max(1, n * 0.3))
  local slack = ((s.doy or 0) >= 36 or ctx.rain > 0 or not ctx.workday) and 0.07 or 0
  local fat = fatigue(p)
  -- sewing: a garment for whoever's clothes are worst, when the cloth is there
  if cloth >= 1 and worst < 0.5 and claim_free(W, hh, "sewing", p) then
    local f = fit(p, ctx, "sew")
    if f > 0 then
      if wq == p then
        addw((0.32 + 0.5 * (0.5 - worst) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "sew", home.id, wq.id, "My clothes are worn through (%d%%): sewing new ones from our cloth", floor(worst * 100))
      else
        addw((0.32 + 0.5 * (0.5 - worst) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "sew", home.id, wq.id, "%s's clothes are worn through (%d%%): sewing new ones from our cloth", wq.name, floor(worst * 100))
      end
    end
  end
  -- weaving at the loom (one weaver a house)
  if yarn >= 0.3 and cloth < want and claim_free(W, hh, "weaving", p) then
    local f = fit(p, ctx, "weave")
    if f > 0 then
      if trade_for and cloth >= max(1.5, n * c.cloth_keep) then
        addw((0.2 + 0.18 * min(1, yarn) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "weave", home.id, 0,
          "Weaving cloth to trade: %s wants cloth (%.1f garments' worth of yarn at the loom)", trade_for, yarn)
      else
        addw((0.2 + 0.18 * min(1, yarn) + 0.12 * urg + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "weave", home.id, 0,
          "%.1f garments' worth of yarn waits at the loom", yarn)
      end
    end
  end
  -- spinning (as many spindles as hands)
  if fib >= 0.02 and yarn + cloth < want * 1.3 then
    local f = fit(p, ctx, "spin")
    if f > 0 then
      local short = min(1, (want * 1.3 - yarn - cloth) / want)
      local eve = (ctx.lh > ctx.sunset - 2.5) and 0.05 or 0
      if urg > 0.2 then
        addw((0.16 + 0.16 * short + 0.1 * urg + slack + eve) * f * fat, "spin", home.id, 0, "The household's clothes are wearing thin: spinning flax (%.1f garments' worth of fibre)", fib)
      else
        addw((0.16 + 0.16 * short + 0.1 * urg + slack + eve) * f * fat, "spin", home.id, 0, "Spinning flax into yarn (%.1f garments' worth of fibre at home)", fib)
      end
    end
  end
  -- dressing flax: the raw bundles from the granary broken, scutched and hackled into fibre
  local gflax = s.granary and ((s.granary.stock.flax or 0) + (s.granary.stock.wool or 0)) or 0
  if fib < want * 0.3 and fib + yarn + cloth < want * 1.5 and ((st.flax or 0) + (st.wool or 0) >= 0.3 or gflax >= 1) and claim_free(W, hh, "dressing", p) then
    local f = fit(p, ctx, "dress")
    if f > 0 then
      local walk = (st.flax or 0) >= 0.3 and 0 or travel(ctx, s.granary_x, s.granary_y, p.x, p.y) * 0.5
      addw((0.2 + 0.12 * urg + slack) * f * fat * WEATHER_OK(ctx, p) - walk, "dress", home.id, 0,
        "Breaking and hackling flax: the house has %.1f garments' worth of fibre, yarn and cloth", fib + yarn + cloth)
    end
  end
  -- mending one's own clothes (a little yarn)
  local mine = p.clothes or 1
  if mine < 0.55 and yarn >= c.mend_clothes_yarn then
    local f = fit(p, ctx, "mend_clothes")
    if f > 0 then
      addw((0.16 + 0.5 * (0.55 - mine) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "mend_clothes", home.id, 0,
        "Patching my clothes (%d%% whole)", floor(mine * 100))
    end
  end
end

-- Leather (D-104): hides to the household's tanning pit, the leather lifted out when it is done,
-- shoes made at home for whoever's are worn out.
local function tan_pit(s, hh)
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "tanpit" and b.hh == hh.id and b.state == "standing" then return b end
  end
  return nil
end
M.tan_pit = tan_pit

local function leather_candidates(W, s, p, ctx, now)
  local c = C()
  local hh, home = ctx.hh, ctx.home
  if not hh or not home or ctx.night then return end
  if ctx.stage ~= "adult" and ctx.stage ~= "apprentice" then return end
  local fat = fatigue(p) * WEATHER_OK(ctx, p)
  local day = floor(now / 24)
  local pit = tan_pit(s, hh)
  if pit then
    if (pit.stock.soaking or 0) > 0 and day >= (pit.ready or 0) and claim_free(W, hh, "lifting", p) then
      addw((0.3 + 0.1 * min(2, pit.stock.soaking)) * fat - travel(ctx, pit.dx, pit.dy, p.x, p.y), "lift", pit.id, 0,
        "%.0f hides have soaked long enough in the pit: lifting out the leather", pit.stock.soaking)
    elseif (pit.stock.soaking or 0) <= 0 and (home.stock.hides or 0) >= 1 and claim_free(W, hh, "tanning", p) then
      addw((0.22 + 0.05 * min(4, home.stock.hides)) * fat - travel(ctx, pit.dx, pit.dy, p.x, p.y) - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5,
        "tan", pit.id, 0, "%.0f hides at home: putting them to soak in the tanning pit", home.stock.hides)
    end
  end
  -- a harness for the household's ox (D-108)
  if not hh.harness and (home.stock.leather or 0) >= c.harness_leather and ctx.stage == "adult" and claim_free(W, hh, "cobbling", p) then
    local H = s.herds and s.herds.cattle
    local has_ox = false
    for _, a in ipairs(H and H.animals or {}) do if a.alive and a.ox and a.owner == hh.id then has_ox = true end end
    if has_ox then
      add(0.35 * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "harness", home.id, 0,
        "Our ox is ready for the plough: cutting and stitching a harness from our leather")
    end
  end
  -- shoes for whoever's are worst, when there is leather
  if (home.stock.leather or 0) >= c.shoe_leather and claim_free(W, hh, "cobbling", p) then
    local worst, wq = 0.35, nil
    for _, id in ipairs(hh.members) do
      local q = W.S.v3.people[id]
      if q and q.alive and q.act ~= "carried" and (q.shoes or 0.7) < worst then worst, wq = q.shoes or 0.7, q end
    end
    if wq then
      addw((0.25 + 0.6 * (0.35 - worst) + (s.cold and 0.1 or 0)) * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "cobble", home.id, wq.id,
        wq == p and "My shoes are worn through (%d%%): cutting new ones from our leather" or "%s's shoes are worn through: making new ones from our leather",
        wq == p and floor(worst * 100) or wq.name)
    end
  end
  -- (S18) salt boiled at the village's salt pans: for the household's curing and cheese, and more
  -- when a neighbour is heard to want it
  local sp = s.saltpan
  if sp and sp.state == "standing" and ctx.rain < 2 and claim_free(W, hh, "salting_pans", p) then
    local have, want = econ.house_stock(W, hh, home, "salt")
    local for_o = s.export and s.export.salt
    if for_o then want = want + #hh.members * c.export_salt end
    local logs = (sp.stock.logs or 0) + (s.wood_store and s.wood_store.stock.logs or 0) + (home.stock.logs or 0)
    if have < want and logs >= 1 then
      local short = 1 - have / want
      addw((0.18 + 0.4 * short) * fat - travel(ctx, sp.dx, sp.dy, p.x, p.y), "salt", sp.id, 0,
        for_o and "Boiling salt at the pans: %.1f measures at home, and %s wants salt" or "Boiling salt at the pans: %.1f measures at home of %.1f wanted",
        have, for_o or want)
    end
  end
end

-- Hunting (D-094): a household short of meat sends a grown man (or woman) after one animal of a
-- wild band on the map: tracked to stalking distance, crept up on, a spear thrown; the quarry may
-- see them first and bolt. Three misses or the day's end and they give up; a kill is carried home
-- whole (meat and hide). A hunt under way is pursued to its end.
local wild = require("sim.agents.wild")

local function hunt_candidates(W, s, p, ctx, now)
  local c = C()
  local H = p.hunting
  if H then
    -- (the hunt goes on: the quarry, or the kill to carry home)
    local w = wild.byk(s, H.k)
    if H.carcass then
      add(1.0, "fetch_game", 0, 0, sfmt("The %s is down: carrying it home", H.sp))
      return
    end
    if w and w.alive and H.tries < c.hunt_tries and now - H.t0 < c.hunt_hours and ctx.lh < ctx.sunset - 0.5 and ctx.rain < 2 then
      add(0.95 * fatigue(p), "hunt", w.k, 1, sfmt("After the %s (%s)", w.sp, H.tries > 0 and "it got away once" or "tracking it"))
      return
    end
    p.hunting = nil
    -- (S19) what they remember of their hunts: the recent ones count most
    p.hunts, p.kills = (p.hunts or 0) * 0.92 + 1, (p.kills or 0) * 0.92
    require("sim.agents.people").remember(p, now, H.tries > 0 and "A day's hunting: the quarry got away" or "Lost the quarry's track")
  end
  if ctx.stage ~= "adult" or not ctx.home or ctx.night or ctx.rain >= 1 or ctx.lh > ctx.sunset - 2.5 or ctx.babe then return end
  if not s.wildlife then return end
  local home = ctx.home
  local hh = ctx.hh
  local n = #hh.members
  -- (meat in the house against what the household would want laid by)
  local meat = (home.stock.meat or 0) + (home.stock.smoked or 0) + (home.stock.fish or 0)
  local need = max(0, 1 - meat / (n * c.meat_want))
  local short = s.need.food_days < 20 and 0.2 or 0
  if need + short < 0.5 or not claim_free(W, hh, "hunting", p) then return end
  -- the quarry: a grown animal of a band, the nearest
  local best, bs = nil, -1e9
  local V = W.S.v3
  for _, w in ipairs(s.wildlife.animals) do
    local hq = (w.hunter or 0) ~= 0 and V.people[w.hunter]
    local taken = hq and hq ~= p and hq.alive and hq.hunting and hq.hunting.k == w.k
    if w.alive and not w.pred and not taken and w.act ~= "flee" and w.act ~= "leave" and (now - w.born) / 1152 > 0.5 then
      local d = dist(p.x, p.y, w.tx, w.ty)
      if d < c.hunt_reach then
        local sc = wild.WP(w.sp).meat * 0.05 - d
        if sc > bs then best, bs = w, sc end
      end
    end
  end
  if not best then return end
  local f = (p.sex == "m" and 1.1 or 0.4) * (0.5 + 0.9 * (p.sk_hunt or 0.2))
  -- (S19) how their hunts have gone: one whose days at the woods' edge mostly come home empty goes
  -- less, a hunter who often brings meat home more (a guess of a quarter before they know)
  local luck = ((p.kills or 0) + 0.5) / ((p.hunts or 0) + 2)
  f = f * min(1.2, 0.3 + 2.5 * luck)
  if (p.hunts or 0) >= 4 and luck < 0.12 then
    addw((0.3 + 0.4 * min(1, need + short)) * f * fatigue(p) - travel(ctx, best.tx, best.ty, p.x, p.y) * 0.6, "hunt", best.k, 0,
      "Little meat at home (%.0f for %d): trying for a %s again, though my hunts mostly come home empty", meat, n, best.sp)
    return
  end
  addw((0.3 + 0.4 * min(1, need + short)) * f * fatigue(p) - travel(ctx, best.tx, best.ty, p.x, p.y) * 0.6, "hunt", best.k, 0,
    "Little meat at home (%.0f for %d): hunting a %s at the woods' edge", meat, n, best.sp)
end
M.hunt_candidates = hunt_candidates

-- Livestock (D-089, D-092). Herding is children's work as much as anyone's: a child of an owning
-- household takes the herd out of its pen to the day's ground, watches it, and brings it home
-- before dusk (the small stock from nine, the pigs from eight, the cattle from eleven). The dams in
-- milk are milked where they stand (mostly as the udder fills: at the pen in the morning and the
-- evening); the milk goes home, and what is not drunk becomes cheese. Shearing in late spring,
-- hay mown in summer and put in the racks when the beasts are kept in, the pens' dung carried to
-- the fields, the kitchen's scraps to the pigs, the beasts the hay cannot winter killed in autumn,
-- the pigs at the start of winter.
local stock = require("sim.agents.stock")

-- A herd at a glance (owners, head count, mean hunger), worked out once a game hour (D-090).
-- (and, per owning household, its dam with the fullest udder, a sheep ready to shear and a beast
-- marked for killing: the candidates read these instead of every animal at every decision)
local function herd_sum(H, now)
  local h = floor(now)
  local S = H.sum
  if S and S.h == h then return S end
  S = S or { owners = {}, milk = {}, milkv = {}, shear = {}, cull = {} }
  H.sum = S
  S.h = h
  for k in pairs(S.owners) do S.owners[k] = nil end
  local milk, milkv, shear, cull = S.milk, S.milkv, S.shear, S.cull
  for k in pairs(milk) do milk[k] = nil end
  for k in pairs(milkv) do milkv[k] = nil end
  for k in pairs(shear) do shear[k] = nil end
  for k in pairs(cull) do cull[k] = nil end
  local c = C()
  local n, hung, se = 0, 0, 0
  for k, a in ipairs(H.animals) do
    if a.alive then
      n, hung = n + 1, hung + a.hunger
      local SP = stock.SP(a.sp)
      se = se + SP.se
      local o = a.owner
      S.owners[o] = true
      local free = (a.led or 0) == 0     -- (a beast on a drove is not milked, shorn or killed: S15)
      if free and (a.milk or 0) > 0 and SP.milk then
        local v = a.milk / (SP.milk * 0.75)
        if a.milk >= SP.milk * c.milk_min * 0.8 and v > (milkv[o] or 0) then milk[o], milkv[o] = k, v end
      end
      if free and a.sp == "sheep" and (a.wool or 0) >= 1 and not shear[o] and (now - a.born) / 1152 > SP.young_years then shear[o] = k end
      if free and a.cull and not cull[o] then cull[o] = k end
    end
  end
  S.n, S.hunger, S.se = n, n > 0 and hung / n or 0, se
  return S
end
M.herd_sum = herd_sum

-- How fit p is to drive herd `kind` (children first).
local function herd_fit(p, ctx, kind)
  local c = C()
  local st, age = ctx.stage, ctx.age
  local f
  if kind == "cattle" then
    f = (st == "child" and age >= c.help_age + 3) and 1.2 or (st == "apprentice" and 1.2) or (st == "elder" and 0.8)
      or (st == "adult" and 0.7) or 0
  elseif kind == "swine" then
    f = (st == "child" and age >= c.help_age) and 1.3 or (st == "apprentice" and 1.0) or (st == "elder" and 0.9)
      or (st == "adult" and 0.5) or 0
  else
    f = (st == "child" and age >= c.help_age + 1) and 1.3 or (st == "apprentice" and 1.1) or (st == "elder" and 0.9)
      or (st == "adult" and 0.6) or 0
  end
  if p.role == "shepherd" and kind == "flock" then f = 1.45 end
  if ctx.babe then f = f * 0.6 end
  return f
end

-- How fit p is for the dairy (women and girls most, by the custom of the time).
local function dairy_fit(p, ctx)
  local c = C()
  local st = ctx.stage
  local fem = p.sex == "f"
  if st == "child" then return ctx.age >= c.help_age + 1 and (fem and 1.0 or 0.8) or 0 end
  if st == "apprentice" then return fem and 1.1 or 0.8 end
  if st == "elder" then return fem and 1.1 or 0.7 end
  if st == "adult" then return fem and 1.25 or 0.65 end
  return 0
end

local HERD_WHY = {
  flock = "The sheep and goats are hungry in the fold (%d%%): taking them out to graze",
  cattle = "The cattle are hungry in the byre yard (%d%%): driving them out to the pasture",
  swine = "The pigs are hungry in the sty (%d%%): taking them to root at the woods' edge",
}
local MIND_WHY = { flock = "Keeping the sheep and goats on fresh grass", cattle = "Minding the cattle on the pasture",
  swine = "Minding the pigs in the woods" }
local HOME_WHY = { flock = "Dusk is coming: bringing the sheep and goats home to the fold",
  cattle = "Dusk is coming: driving the cattle home", swine = "Dusk is coming: bringing the pigs home to the sty" }

local function herd_candidates(W, s, p, ctx, now)
  local herds = s.herds
  if not herds or ctx.night or not ctx.hh then return end
  local c = C()
  local st = ctx.stage
  local late = ctx.lh > ctx.sunset - 1.2
  local doy = s.doy or 0
  local hh = ctx.hh
  -- one herd at a time: whoever is out with a herd minds it until it is home
  for hi = 1, #stock.HERDS do
    local kind = stock.HERDS[hi]
    local H = herds[kind]
    if H and stock.herder(W, s, H) == p then
      if late then
        add(1.0, "herd_home", 0, hi, HOME_WHY[kind])
      else
        add((0.64 + 0.1 * herd_fit(p, ctx, kind)) * fatigue(p) * WEATHER_OK(ctx, p), "herd", 1, hi, MIND_WHY[kind])
      end
      return
    end
  end
  local df = dairy_fit(p, ctx)
  local adult = st == "adult"
  for hi = 1, #stock.HERDS do
    local kind = stock.HERDS[hi]
    local H = herds[kind]
    if H then
      local pen = s.buildings[H.pen]
      local HS = herd_sum(H, now)
      local mine = HS.owners[p.hh] == true
      local f = herd_fit(p, ctx, kind)
      -- (the children of the household have the first chance in the morning: the grown-ups wait)
      local wait = st == "child" and 0.4 or 1.6
      if f > 0 and mine and not stock.herder(W, s, H) and not H.out and not late and ctx.lh > ctx.wake + wait and ctx.rain < 2 and HS.n > 0 then
        -- (a duty the household gives its children more than a choice)
        addw((0.45 + 0.45 * HS.hunger) * f * fatigue(p) * WEATHER_OK(ctx, p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "herd", 0, hi,
          HERD_WHY[kind], floor(HS.hunger * 100))
      end
      if mine then
        -- milking: the household's dam with the fullest udder (wherever she is)
        local mk = HS.milk[p.hh]
        if df > 0 and kind ~= "swine" and mk and claim_free(W, hh, "milking", p) then
          local best = H.animals[mk]
          local S = stock.SP(best.sp)
          local bv = (best.milk or 0) / (S.milk * 0.75)
          if best.alive and (best.held or 0) == 0 and best.milk >= S.milk * c.milk_min then
            local ax, ay = best.tx, best.ty
            addw((0.42 + 0.45 * bv) * df * fatigue(p) * WEATHER_OK(ctx, p) - travel(ctx, ax, ay, p.x, p.y), "milk", best.k, hi,
              "The %s's udder is full (%.1f of milk): milking her", best.sp == "cattle" and "cow" or "goat", best.milk)
          end
        end
        if not (st == "child" and ctx.age < c.help_age + 1) then
          -- shearing: a sheep of the household with its fleece grown, kept in the fold
          local sk = HS.shear[p.hh]
          if kind == "flock" and sk and doy >= c.shearing[1] and doy <= c.shearing[2] and not H.out and st ~= "child" then
            local a = H.animals[sk]
            if a.alive and (a.wool or 0) >= 1 and (a.held or 0) == 0 then
              addw(0.48 * fatigue(p) * WEATHER_OK(ctx, p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "shear", sk, hi,
                "Shearing time: a fleece of %.1f garments' wool on one of our sheep", a.wool)
            end
          end
          -- autumn (pigs at the start of winter): a beast of the household marked for killing
          local ck = HS.cull[p.hh]
          if adult and ck and not H.out then
            local a = H.animals[ck]
            if a.alive and a.cull and (a.held or 0) == 0 then
              local fq = a.cull_for and W.S.v3.households[a.cull_for]
              fq = fq and W.S.v3.people[fq.members[1] or 0]
              if a.cull == "hunger" and fq then
                addw(0.85 * fatigue(p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "slaughter", ck, hi,
                  "The %s household has nothing left to eat and no beasts of its own: killing one of our %s for them",
                  fq.surname, a.sp == "cattle" and "cattle" or (a.sp == "sheep" and "sheep" or a.sp .. "s"))
              elseif a.cull == "hunger" then
                -- (S19) the larder is all but empty and the village has little to give
                addw(0.85 * fatigue(p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "slaughter", ck, hi,
                  "Almost nothing left to eat at home and little in the village: killing one of our %s for the pot",
                  a.sp == "cattle" and "cattle" or (a.sp == "sheep" and "sheep" or a.sp .. "s"))
              else
                addw(0.5 * fatigue(p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "slaughter", ck, hi,
                  kind == "swine" and "Winter is coming: killing a fattened %s for the larder" or "The hay will not winter all our beasts: killing a %s for the larder",
                  a.sp == "cattle" and "beast" or a.sp)
              end
            end
          end
          -- hay into the rack when the herd is kept in and hungry (not the pigs: scraps)
          if kind ~= "swine" and not H.out and (s.hay or 0) > 1 and (H.rack or 0) < HS.se * 0.2 and claim_free(W, hh, "feeding", p) then
            local winter = doy >= 36 or (s.grass_low or false)
            if HS.hunger > 0.45 and (winter or late or ctx.rain > 0) then
              addw((0.35 + 0.5 * HS.hunger) * fatigue(p) - travel(ctx, pen.dx, pen.dy, p.x, p.y), "feed", 0, hi,
                "%s are hungry in the pen (%d%%): hay into the rack", kind == "cattle" and "The cattle" or "The sheep and goats", floor(HS.hunger * 100))
            end
          end
        end
      end
      -- the pen's dung to a field waiting for the plough (or resting): anyone grown
      if (adult or st == "apprentice") and (H.manure or 0) >= c.manure_load and (H.muck_claim or 0) == 0 then
        local pl, bfert = nil, 9
        for _, q in ipairs(s.plots) do
          if q.alive and (q.stage == "fallow" or q.stage == "rest" or q.stage == "stubble") and (q.fert or 1) < c.fert_max - 0.05
            and (q.fert or 1) < bfert and (q.hh == 0 or q.hh == p.hh or (q.fert or 1) < 0.8) then
            pl, bfert = q, q.fert or 1
          end
        end
        if pl then
          local ff = fit(p, ctx, "haul_grain")
          if ff > 0 then
            addw((0.3 + 0.3 * (1 - bfert) + 0.05 * min(4, H.manure / c.manure_load)) * ff * fatigue(p) * WEATHER_OK(ctx, p)
              - travel(ctx, pen.dx, pen.dy, p.x, p.y), "muck", pl.id, hi,
              "%d loads of dung in the %s: carrying them to %s (its soil is tired)", floor(H.manure / c.manure_load), pen.label, pl.name)
          end
        end
      end
    end
  end
  if not (st == "child" and ctx.age < c.help_age) then
    -- the kitchen's scraps to the pigs
    local home = ctx.home
    local HSw = herds.swine
    if home and HSw and (home.stock.scraps or 0) >= c.slop_min and claim_free(W, hh, "slopping", p) then
      local sty = s.buildings[HSw.pen]
      addw(0.32 + 0.1 * min(3, home.stock.scraps / c.slop_min) - travel(ctx, sty.dx, sty.dy, p.x, p.y), "slop", home.id, 3,
        "Scraps from the kitchen (%.1f): to the pigs' trough", home.stock.scraps)
    end
    -- cheese: the milk at home that will not be drunk before it turns
    if home and (home.stock.milk or 0) >= c.cheese_min and (st ~= "child") and claim_free(W, hh, "cheesing", p) then
      addw((0.4 + 0.1 * min(3, home.stock.milk / c.cheese_min)) * max(0.6, df) * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, p.x, p.y), "cheese", home.id, 0,
        "%.1f of milk at home will turn by tomorrow: making cheese", home.stock.milk)
    end
  end
  -- hay for the winter: mown in summer on the best meadow (not where a herd grazes today)
  if (herds.flock or herds.cattle) and doy >= 12 and doy < 26 and (st == "adult" or st == "apprentice") then
    local want = stock.hay_want(s, now)
    if (s.hay or 0) < want and claim_free(W, hh, "mowing", p) then
      local ff = fit(p, ctx, "harvest")
      local owns = (herds.flock and herd_sum(herds.flock, now).owners[p.hh]) or (herds.cattle and herd_sum(herds.cattle, now).owners[p.hh])
      if ff > 0 and owns then
        addw((0.3 + 0.35 * (1 - (s.hay or 0) / want)) * ff * fatigue(p) * WEATHER_OK(ctx, p), "mow", 0, 0,
          "Making hay for the winter: the stack holds %d%% of what the beasts will want", floor(100 * (s.hay or 0) / max(1, want)))
      end
    end
  end
end

-- Leisure with a reason: the evening gathering at the well, visiting kin and friends, play, rest.
local function leisure(W, s, p, ctx, now)
  local px, py = p.x, p.y
  local st = ctx.stage
  local evening = ctx.lh >= ctx.sunset - 0.5
  local wx = ctx.rain > 0 and 0.2 or 1
  if st == "child" then
    -- play with friends by the well or near home
    local best, ba = nil, -1
    for i = 1, #p.rel_id do
      local q = W.S.v3.people[p.rel_id[i]]
      -- (S18: a friend on this map - not one left behind in another village)
      if q and q.alive and q.act == "play" and (q.at or q.sett) == s.id and p.rel_v[i] + (q.hh == p.hh and 0.2 or 0) > ba then best, ba = q, p.rel_v[i] + (q.hh == p.hh and 0.2 or 0) end
    end
    -- (a whole day of play palls: chores and tagging along win now and then)
    local d = floor(now / 24)
    local played = p.play_day == d and (p.play_h or 0) or 0
    -- (older children are expected to lend a hand: their play counts for less as they grow, D-109)
    local grown = ctx.age < C().gather_age and 1 or (ctx.age < C().help_age + 1 and 0.8 or 0.62)
    local u = (0.42 + 0.1 * p.soc) * wx * max(0.45, 1 - played * 0.06) * grown
    if best then
      addw(u + 0.1, "play", best.id, 0, "Playing with %s", best.name)
    else
      add(u, "play", 0, 0, "Playing near home")
    end
  else
    -- sit at the well where others are (sociable people more)
    local midday = ctx.lh >= 11 and ctx.lh < 14
    -- (the sociable go most evenings, the rest some evenings: crowds only when there is news)
    local eve = evening and (0.7 + 1.1 * p.soc) or (midday and 0.55 or 0.25)
    -- (cold evenings are spent by the hearth, visiting, not at the well)
    local chill = (s.temp_now or 15) < 12 and max(0.35, 1 - (12 - (s.temp_now or 15)) * 0.15) or 1
    local u = (0.1 + 0.2 * p.soc) * eve * wx * chill
    local there = s.at_well or 0
    -- a few talkers draw others; a crowd puts the quieter ones off (they sit by their own door)
    local draw = 0.03 * min(there, 4) - 0.025 * max(0, there - 6) * (1.2 - p.soc)
    local where = s.well and "the well" or "the fire"
    if there > 0 then
      addw(u + draw - travel(ctx, s.sqx, s.sqy, px, py) * 0.5, "talk", 0, 0, "%d %s talking by %s", there, plural(there, "neighbour is", "neighbours are"), where)
    else
      add(u + draw - travel(ctx, s.sqx, s.sqy, px, py) * 0.5, "talk", 0, 0, s.well and "Going to the well to talk" or "Going to sit by the fire and talk")
    end
    -- the evening on the bench by their own door
    if evening and ctx.home and ctx.rain == 0 then
      add((0.16 + 0.12 * (1 - p.soc)) * (0.8 + 0.4 * u01(W, p, now, 42)) - travel(ctx, ctx.hx, ctx.hy, px, py) * 0.5,
        "sit_out", 0, 0, "A quiet evening by the door")
    end
    -- visit the closest friend or kin elsewhere
    local best, ba = 0, 0.15
    for i = 1, #p.rel_id do
      local qid = p.rel_id[i]
      local q = W.S.v3.people[qid]
      if q and q.alive and q.hh ~= p.hh and q.inside ~= 0 and (q.at or q.sett) == s.id then
        local a = p.rel_v[i] + (people.is_kin(W, p, qid) and 0.2 or 0)
        if a > ba then best, ba = qid, a end
      end
    end
    if best > 0 then
      local q = W.S.v3.people[best]
      local qb = s.buildings[q.inside]
      if qb and qb.kind == "house" then
        addw((0.1 + 0.25 * ba) * (evening and 1.5 or 0.25) - travel(ctx, qb.dx, qb.dy, px, py) * 0.5, "visit", best, qb.id,
          "Visiting %s%s", people.full_name(q), people.is_kin(W, p, best) and " (family)" or " (a friend)")
      end
    end
  end
  -- rest at home
  add(0.05 + (1 - p.energy) * 0.55 + (evening and 0.08 or 0), "rest", 0, 0,
    p.energy < 0.4 and "Tired out: resting at home" or "Resting at home")
end

-- Care (D-081) -------------------------------------------------------------------------------
-- A hungry infant: its nursing mother feeds it (where she is when she holds it, else she goes to
-- whoever has it); a weaned one is fed porridge at home by whoever holds it.
local function care_candidates(W, s, p, ctx, now)
  local c = C()
  local V = W.S.v3
  if p.nursing > now then
    for i = 1, #p.kids do
      local ib = V.people[p.kids[i]]
      if ib and ib.alive and ib.act == "carried" and ib.hunger > c.infant_feed then
        local q = V.people[ib.carer or 0]
        local u = 0.95 + ib.hunger
        if q and q ~= p then
          local qx, qy = act.pos(q, now)
          u = u - travel(ctx, qx, qy, p.x, p.y) * 0.5
        end
        if q == p then
          addw(u, "nurse", ib.id, 0, "%s is hungry: nursing", ib.name)
        else
          addw(u, "nurse", ib.id, 0, "%s is hungry; %s has the baby", ib.name, q and q.name or "nobody")
        end
      end
    end
  end
  local ib = ctx.babe
  if ib and ib.hunger > c.infant_feed and not kin.nursing_mother(W, ib, now) and ctx.home then
    if (ctx.home.stock.meals or 0) >= c.porridge then
      addw(0.9 + ib.hunger - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "feed_babe", ib.id, 0,
        "%s is hungry: porridge at home", ib.name)
    elseif people.pantry(ctx.home) >= 0.3 and ctx.hh and claim_free(W, ctx.hh, "cooking", p) then
      addw(0.85 + ib.hunger - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "cook", ctx.home.id, 0,
        "%s is hungry and nothing is cooked: making porridge", ib.name)
    end
  end
end

-- Children (D-081): they tag along with a parent or grandparent at work nearby (the youngest most).
local CHILD_FOLLOW = { till = true, sow = true, harvest = true, tend = true, haul_grain = true, forage = true, fetch_water = true,
  build = true, repair = true, thresh = true, craft = true, fell = false }
local function follow_candidate(W, s, p, ctx, now)
  if ctx.stage ~= "child" or ctx.age < 2.5 then return end
  local V = W.S.v3
  local best, bu = nil, 0
  for _, rid in ipairs({ p.mother, p.father }) do
    local q = V.people[rid or 0]
    if q and q.alive and q.inside == 0 and CHILD_FOLLOW[q.objk or ""] and q.act ~= "sleep" and (q.at or q.sett) == (p.at or p.sett) then
      local qx, qy = act.pos(q, now)
      local d = dist(p.x, p.y, qx, qy)
      if d < 1.0 then
        local u = (0.36 + 0.22 * max(0, 1 - ctx.age / 9) + 0.15 * people.affinity(p, q.id)) - d * 0.25
        if u > bu then best, bu = q, u end
      end
    end
  end
  if best then
    addw(bu * (ctx.rain > 0 and 0.3 or 1), "follow", best.id, 0,
      "Tagging along with %s %s", best.sex == "f" and "mother," or "father,", best.name)
  end
end

-- Elders keep an eye on the grandchildren at play near home.
local function watch_candidate(W, s, p, ctx, now)
  if ctx.stage ~= "elder" or not ctx.home or ctx.rain > 0 then return end
  local V = W.S.v3
  local hh = ctx.hh
  if not hh then return end
  for i = 1, #hh.members do
    local q = V.people[hh.members[i]]
    if q and q.alive and q ~= p and q.objk == "play" and q.inside == 0 then
      local qx, qy = act.pos(q, now)
      if dist(qx, qy, ctx.hx, ctx.hy) < 0.4 then
        addw(0.24 + 0.1 * people.affinity(p, q.id) - travel(ctx, qx, qy, p.x, p.y) * 0.5, "watch", q.id, 0,
          "Keeping an eye on %s at play", q.name)
        return
      end
    end
  end
end

-- Body needs ---------------------------------------------------------------------------------

local function eat_candidate(W, s, p, ctx, now)
  local c = C()
  local h = p.hunger
  local home = ctx.home
  local mealtime = (ctx.lh < ctx.wake + 1.5) or (ctx.lh > ctx.sunset - 1.2)
  local thr = mealtime and 0.25 or c.hunger_eat
  if h < thr then return end
  if p.lunch and ctx.dhome > 0.35 then
    add(0.6 + h, "lunch", 0, 0, "Hungry far from home: eating the bread brought along")
    return
  end
  if home and (home.stock.meals or 0) >= c.meal_fu * 0.5 then
    add(0.5 + h - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "eat", home.id, 0,
      mealtime and (ctx.lh < 12 and "Breakfast at home" or "Supper at home") or "Hungry: going home to eat")
  elseif home and people.pantry(home) >= 0.3 and ctx.stage ~= "child" and ((home.stock.logs or 0) > 0 or home.fire)
    and (ctx.hh and claim_free(W, ctx.hh, "cooking", p)) then
    -- nothing cooked: hungry, they cook (and then eat)
    add(0.45 + h - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "cook", home.id, 1, "Hungry and nothing cooked at home")
  elseif home and people.pantry(home) >= 0.3 and (h > 0.6 or ctx.stage == "child") then
    -- (S17) no fire to cook on (a camp before any wood is cut): eaten cold
    add(0.4 + h - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "eat", home.id, 2, "Hungry, and no fire to cook on: eating cold food")
  elseif not home and p.pack and (p.pack.grain or 0) >= 0.3 then
    -- (S17) no home yet: bread from the pack they carried here
    add(0.5 + h, "eat", 0, 3, "Hungry, with no hearth yet: eating bread from my pack")
  end
end

-- Choose ------------------------------------------------------------------------------------

local function best_cand(p, now)
  local failed = p.failed and now - (p.failed_t or 0) < 1 and p.failed
  while true do
    local bi, bu = 0, -1e9
    for i = 1, ncand do
      local u = CAND[i].u
      if failed and CAND[i].kind == failed then u = u - 1 end
      if u > bu then bi, bu = i, u end
    end
    local c = CAND[bi]
    -- a lazy candidate on top is worked out, and the choice made again (add_lazy)
    if not c or not c.lazy then return c end
    local fn = c.lazy
    c.lazy = nil
    fn(c, c.larg)
  end
end

-- Fill p.plan for objective `kind`; returns objective label and destination.
local PLAN = {}

PLAN.sleep = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  local until_t = next_local(now, ctx.lh, ctx.wake)
  -- a little later on rest days or when exhausted
  if p.energy < 0.3 then until_t = until_t + 0.5 end
  -- (worn out in the day: a sleep of some hours, not until the next morning: D-102)
  if not ctx.night then until_t = min(until_t, now + C().nap_h) end
  if home then
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    work(pl, "sleep", until_t - now - (p.inside ~= home.id and 0.2 or 0), { inside = home.id })
  else
    local x, y = near_spot(W, p, now, s.sqx, s.sqy, 0.08, 3)
    go(pl, x, y, "the square")
    work(pl, "sleep", until_t - now - 0.2)
  end
  return "Sleep", home and "home" or "the square"
end

PLAN.shelter = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  -- home if near, else the nearest standing building with a door
  local b = ctx.home
  local bd = b and ctx.dhome or 1e9
  if bd > 0.4 then
    for i = 1, #s.blist do
      local o = s.buildings[s.blist[i]]
      if o and o.state == "standing" and o.kind ~= "well" and o.kind ~= "dock" then
        local d = dist(p.x, p.y, o.dx, o.dy)
        if d < bd - 0.15 then b, bd = o, d end
      end
    end
  end
  local stop = require("sim.agents.weather").next_change(W, s, now) or (now + 1)
  local wait = max(0.25, stop - now + 0.05 + 0.3 * u01(W, p, now, 5))
  if b then
    if p.inside ~= b.id then go(pl, b.dx, b.dy, b == ctx.home and "home" or b.label, b.id) end
    work(pl, "shelter", wait, { inside = b.id })
    return "Shelter from the rain", b == ctx.home and "home" or b.label
  end
  work(pl, "shelter", wait)
  return "Shelter from the rain", "where they stand"
end

PLAN.eat = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  if cd.b == 3 then
    work(pl, "eat", 0.3, { from = "pack" })
    return "Eat", "where they are"
  end
  local home = s.buildings[cd.a]
  if cd.b == 2 then
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    work(pl, "eat", 0.3, { inside = home.id, ref = home.id, from = "cold" })
    return "Eat", "home"
  end
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "eat", 0.3 + 0.2 * u01(W, p, now, 9), { inside = home.id, ref = home.id })
  -- take bread along when the day's work is still ahead
  return "Eat", "home"
end

PLAN.cook = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  ctx.hh.cooking = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  -- (S19) with a quern the grinding goes faster than on a saddle stone
  local qf = (ctx.hh.quern or 0) >= c.quern_broken and (1 - c.quern_save) or 1
  work(pl, "cook", c.cook_h * qf * (0.75 + 0.5 * u01(W, p, now, 44)), { inside = home.id, ref = home.id })
  if cd.b == 1 then work(pl, "eat", 0.3 + 0.2 * u01(W, p, now, 9), { inside = home.id, ref = home.id }) end
  return "Cook for the household", "home"
end

PLAN.craft = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  local dur = (1.0 + 1.0 * u01(W, p, now, 45)) * p.dil
  if ctx.rain > 0 then
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    work(pl, "craft", dur, { inside = home.id, ref = home.id })
    return "Mend tools at home", "home"
  end
  -- on the bench by the door, facing out
  local ox, oy = home.dx - home.cx, home.dy - home.cy
  local l = sqrt(ox * ox + oy * oy)
  if l < 1e-6 then ox, oy, l = 1, 0, 1 end
  local side = (p.id % 2 == 0) and 1 or -1
  local x = home.dx + (-oy / l) * 0.022 * side + ox / l * 0.01
  local y = home.dy + (ox / l) * 0.022 * side + oy / l * 0.01
  if dist(p.x, p.y, x, y) > 0.01 then go(pl, x, y, "home") end
  work(pl, "craft", dur, { ref = home.id, fx = x + ox / l, fy = y + oy / l })
  return "Mend tools by the door", "home"
end

PLAN.nurse = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local V = W.S.v3
  local ib = V.people[cd.a]
  local q = V.people[ib.carer or 0]
  local inside = p.inside ~= 0 and p.inside or nil
  if q and q ~= p then
    -- to whoever has the baby (a minder is at home, or by its door)
    if q.inside ~= 0 then
      local b = s.buildings[q.inside]
      if b and p.inside ~= b.id then go(pl, b.dx, b.dy, b == ctx.home and "home" or b.label, b.id) end
      inside = q.inside
    else
      local qx, qy = act.pos(q, now)
      go(pl, qx + 0.012, qy, "the baby")
      inside = nil
    end
  end
  work(pl, "nurse", c.nurse_h * (0.8 + 0.4 * u01(W, p, now, 46)), { ref = ib.id, inside = inside, fx = p.x + p.fx, fy = p.y + p.fy })
  return "Feed " .. ib.name, inside and "home" or "where they are"
end

PLAN.feed_babe = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local ib = W.S.v3.people[cd.a]
  local home = ctx.home
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "feed_babe", 0.2 + 0.15 * u01(W, p, now, 47), { ref = ib.id, inside = home.id })
  return "Feed " .. ib.name, "home"
end

PLAN.leave_babe = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  local m = W.S.v3.people[cd.a]
  if m.inside == home.id then
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    work(pl, "hand", 0.05, { ref = m.id, babe = cd.b, inside = home.id })
  else
    local mx, my = act.pos(m, now)
    go(pl, mx + 0.012, my, "home")
    work(pl, "hand", 0.05, { ref = m.id, babe = cd.b })
  end
  return "Leave " .. W.S.v3.people[cd.b].name .. " with " .. m.name, "home"
end

PLAN.follow = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local q = W.S.v3.people[cd.a]
  -- where the parent is going (or is): they play and watch nearby while the parent works
  local tx, ty = act.pos(q, now)
  if q.act == "walk" and q.mv then tx, ty = q.mv.x[q.mv.n], q.mv.y[q.mv.n] end
  local x, y = near_spot(W, p, now, tx, ty, 0.05, 48)
  go(pl, x, y, "with " .. q.name)
  local x2, y2 = near_spot(W, p, now, x, y, 0.03, 49)
  work(pl, "play", 0.5 + 0.8 * u01(W, p, now, 50), { x1 = x2, y1 = y2 })
  return "Tag along with " .. q.name, q.dest ~= "" and q.dest or "the fields"
end

PLAN.watch = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local q = W.S.v3.people[cd.a]
  local qx, qy = act.pos(q, now)
  local x, y = near_spot(W, p, now, qx, qy, 0.06, 51)
  go(pl, x, y, "near the children")
  work(pl, "sit", 0.6 + 0.8 * u01(W, p, now, 52), { fx = qx, fy = qy })
  return "Watch the children", "near home"
end

PLAN.lunch = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  work(pl, "lunch", 0.25 + 0.15 * u01(W, p, now, 9))
  return "Eat", "where they work"
end

PLAN.rest = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  local dur = 0.5 + 0.8 * u01(W, p, now, 9)
  if home then
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    work(pl, "rest", dur, { inside = home.id })
  else
    work(pl, "rest", dur)
  end
  return "Rest", "home"
end

PLAN.talk = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  -- already among the talkers: stay where they stand
  if not (p.objk == "talk" and p.inside == 0 and dist(p.x, p.y, s.sqx, s.sqy) < 0.14) then
    local x, y = near_spot(W, p, now, s.sqx, s.sqy, 0.09, 11)
    go(pl, x, y, "the well")
  end
  work(pl, "talk", 0.5 + 1.0 * u01(W, p, now, 12), { fx = s.sqx, fy = s.sqy })
  return "Talk with neighbours", "the well"
end

PLAN.visit = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.b]
  if p.inside ~= b.id then go(pl, b.dx, b.dy, "a neighbour's house", b.id) end
  work(pl, "visit", 0.6 + 0.9 * u01(W, p, now, 12), { inside = b.id, with = cd.a })
  local q = W.S.v3.people[cd.a]
  return "Visit " .. q.name, q.surname .. " house"
end

PLAN.sit_out = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  -- a bench beside the door, facing out
  local ox, oy = home.dx - home.cx, home.dy - home.cy
  local l = sqrt(ox * ox + oy * oy)
  if l < 1e-6 then ox, oy, l = 1, 0, 1 end
  local side = (p.id % 2 == 0) and 1 or -1
  local x = home.dx + (-oy / l) * 0.02 * side + ox / l * 0.008
  local y = home.dy + (ox / l) * 0.02 * side + oy / l * 0.008
  if dist(p.x, p.y, x, y) > 0.01 then go(pl, x, y, "home") end
  work(pl, "sit", 0.5 + 0.9 * u01(W, p, now, 43), { fx = x + ox / l, fy = y + oy / l })
  return "Sit by the door", "home"
end

PLAN.play = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local x, y
  local q = cd.a ~= 0 and W.S.v3.people[cd.a]
  if q then
    x, y = near_spot(W, p, now, q.x, q.y, 0.05, 13)
  else
    -- the household's play spot of the day (siblings who set out alone still meet there)
    local hk = ctx.hh and ctx.hh.id or p.id
    local day = floor(now / 24)
    local hu = rng.float(W.seed, rng.S.V3_THINK, hk, day, 77)
    local bx, by = ctx.hx, ctx.hy
    if hu < 0.4 then bx, by = s.sqx, s.sqy end
    local a = rng.float(W.seed, rng.S.V3_THINK, hk, day, 78) * 8
    local o = P8[floor(a) + 1]
    bx, by = bx + o[1] * 0.07, by + o[2] * 0.07
    x, y = near_spot(W, p, now, bx, by, 0.035, 13)
  end
  go(pl, x, y, q and ("with " .. q.name) or "near home")
  -- they run about while they play: a short loop to another spot and back
  local x2, y2 = near_spot(W, p, now, x, y, 0.06, 15)
  work(pl, "play", 0.35 + 0.5 * u01(W, p, now, 16), { x1 = x2, y1 = y2 })
  return "Play", q and ("with " .. q.name) or "near home"
end

PLAN.fetch_food = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local g, home = s.granary, ctx.home
  ctx.hh.fetch_food = p.id
  go(pl, g.dx, g.dy, "the granary")
  pl[#pl + 1] = { op = "pick", what = "grain", as = "food", ref = g.id, n = c.pantry_fetch }
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_food" }
  return "Fetch food from the granary", "the granary"
end

PLAN.strike_camp = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b, home = s.buildings[cd.a], ctx.home
  ctx.hh.moving_in = p.id
  go(pl, b.cx + 0.01, b.cy, "the old camp")
  work(pl, "strike", 0.15, { ref = b.id, fx = b.cx, fy = b.cy })
  pl[#pl + 1] = { op = "pick", what = "camp", ref = b.id }
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "moving_in" }
  return "Carry our things in from the camp", "the old camp"
end

PLAN.fetch_wood = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local w, home = s.wood_store, ctx.home
  ctx.hh.fetch_wood = p.id
  go(pl, w.dx, w.dy, "the woodstore")
  pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = w.id, n = ctx.stage == "child" and 1 or (p.strong and c.woodpile_fetch + 1 or c.woodpile_fetch) }
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_wood" }
  return "Fetch firewood", "the woodstore"
end

PLAN.fetch_water = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local w, home = s.well, ctx.home
  ctx.hh.fetch_water = p.id
  if cd and cd.b == 1 and cd.a > 0 then
    -- (from the river bank, D-095)
    local mapm = require("sim.agents.map")
    local bx, by = mapm.tile_xy(s, cd.a)
    go(pl, bx, by, "the river")
    work(pl, "fill", 0.1 + 0.06 * u01(W, p, now, 18), { fx = bx, fy = by, river = true })
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_water" }
    return "Fetch water from the river", "the river"
  end
  local x, y = near_spot(W, p, now, w.dx, w.dy, 0.03, 17)
  go(pl, x, y, "the well")
  work(pl, "fill", 0.12 + 0.08 * u01(W, p, now, 18), { fx = w.dx, fy = w.dy })
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_water" }
  return "Fetch water", "the well"
end

-- Angling from the bank (D-095).
PLAN.angle = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local mapm = require("sim.agents.map")
  ctx.hh.angling = p.id
  local bx, by = mapm.tile_xy(s, cd.a)
  -- (facing the water: the wet neighbour)
  local N = s.N
  local fx, fy = bx, by
  for _, o in ipairs({ 1, -1, N, -N }) do
    local j = cd.a + o
    if s.riv[j] and s.riv[j] > 0 then fx, fy = mapm.tile_xy(s, j); break end
  end
  go(pl, bx, by, "the river bank")
  work(pl, "angle", (1.2 + 1.2 * u01(W, p, now, 74)) * p.dil, { fx = fx, fy = fy, release = "angling" })
  local home = ctx.home
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "angling" }
  end
  return "Fish in the river", "the river bank"
end

-- Rows are worked in stints (D-080): from where the row's work stands (plot.row_p) for as long as
-- the stint lasts or the row is done; the row keeps its progress for whoever comes next.
local function row_stint(W, s, p, now, plot, row, kind, full_h, k)
  local c = C()
  local pl = p.plan
  local rp = plot.row_p
  if not rp then rp = {}; plot.row_p = rp end
  local a0 = rp[row] or 0
  local rate = 1 / full_h                         -- share of the row per hour
  local stint = (c.stint_h[1] + (c.stint_h[2] - c.stint_h[1]) * u01(W, p, now, k)) * p.dil
  local dur = min((1 - a0) / rate, max(0.3, stint))
  local a1 = min(1, a0 + dur * rate)
  local x0, y0, x1, y1 = act.plot_row_line(s, plot, row)
  if row % 2 == 0 then x0, x1 = x1, x0 end      -- back and forth across the plot
  local sx, ex = x0 + (x1 - x0) * a0, x0 + (x1 - x0) * a1
  go(pl, sx, y0, plot.name)
  work(pl, kind, dur, { ref = plot.id, row = row, x1 = ex, y1 = y1, rate = rate })
  return a0
end

PLAN.sow = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local plot = s.plots[cd.a]
  plot.row_claim[cd.b] = p.id
  if p.carry ~= "seed" or p.camt < c.seed_row then
    -- seed from home when some is kept there, else from the granary (S17: a young place with no
    -- granary yet sows from the grain its people carried there)
    local home = ctx.home
    if home and (home.stock.seed or 0) >= c.seed_row and (not s.granary or dist(p.x, p.y, home.dx, home.dy) <= dist(p.x, p.y, s.granary_x, s.granary_y) + 0.1) then
      go(pl, home.dx, home.dy, "home", home.id)
      pl[#pl + 1] = { op = "pick", what = "seed", ref = home.id, n = c.seed_row * 4 }
    elseif not s.granary then
      local src = home
      if not (src and (src.stock.food or 0) >= c.seed_row) then src = settle.seed_store(W, s) end
      if src then
        go(pl, src.dx, src.dy, src == home and "home" or src.label, src == home and home.id or nil)
        pl[#pl + 1] = { op = "pick", what = "seed", ref = src.id, n = c.seed_row * 4, food = true }
      end
    else
      local g = s.granary
      go(pl, g.dx, g.dy, "the granary")
      pl[#pl + 1] = { op = "pick", what = "grain", as = "seed", ref = g.id, n = c.seed_row * 4 }
    end
  end
  row_stint(W, s, p, now, plot, cd.b, "sow", c.sow_row_h * (1.25 - 0.45 * p.sk_farm) * (0.9 + 0.2 * u01(W, p, now, 19)), 36)
  return "Sow " .. plot.name, plot.name
end

-- The household's ox, free and fit for the plough, with its harness made (D-108), or nil.
local function free_ox(W, s, hh)
  local H = s.herds and s.herds.cattle
  if not H or not hh or not hh.harness then return nil end
  for _, a in ipairs(H.animals) do
    if a.alive and a.ox and a.owner == hh.id and (a.yoke or 0) == 0 and (a.held or 0) == 0 and a.health > 0.5 and a.hunger < 0.75
      and (a.sick or 0) <= 0 and (a.worked or 0) < C().ox_day_h then return a end
  end
  return nil
end
M.free_ox = free_ox

PLAN.till = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local plot = s.plots[cd.a]
  plot.row_claim[cd.b] = p.id
  local full = c.till_row_h * (1.25 - 0.45 * p.sk_farm) * (0.9 + 0.2 * u01(W, p, now, 26)) * (p.strong and 0.9 or 1) / tool_factor(W, p)
  -- (with the household's ox at the plough: fetched from the herd and yoked, the row turned in a
  -- fraction of the hoeing's hours; D-108)
  -- (the heavy plough behind an ox was men's work: a woman yokes it only in a household with no grown
  -- man; she hoes otherwise, S14)
  local ox = ctx.stage == "adult" and (p.sex == "m" or not kin.has_man(W, ctx.hh, now)) and free_ox(W, s, ctx.hh)
  local ox_h = 0
  if ox then
    -- (S14) the ox is fetched when the hoeing it saves in this stint is worth the walk to it and
    -- back; in the yoke it ploughs on, row after row of the field, for a longer stint
    local rp = plot.row_p or {}
    ox_h = min(c.ox_day_h - (ox.worked or 0), c.ox_stint_h * p.dil)
    local ax, ay = stock.pos(ox, now)
    local fetch = 2 * dist(p.x, p.y, ax, ay) / ctx.spd + 0.25
    local work_ox = min(ox_h, (1 - (rp[cd.b] or 0)) * full / c.ox_till)
    for r = 1, plot.rows do
      local rs = plot.row_st[r]
      if r ~= cd.b and plot.row_claim[r] == 0 and (rs == 0 or rs == 3) then work_ox = work_ox + (1 - (rp[r] or 0)) * full / c.ox_till end
      if work_ox >= ox_h then break end
    end
    work_ox = min(work_ox, ox_h)
    if work_ox * (c.ox_till - 1) < fetch * 1.5 then ox = nil end
  end
  if ox then
    local ax, ay = stock.pos(ox, now)
    go(pl, ax, ay, "the ox")
    work(pl, "yoke", 0.15, { ox = ox.k, fx = ax, fy = ay })
    full = full / c.ox_till
    local rp = plot.row_p or {}
    local a0 = rp[cd.b] or 0
    -- this row to its end, then the next waiting rows while the stint lasts
    local used, row = 0, cd.b
    while row and used < ox_h - 0.05 do
      local ar = rp[row] or 0
      local dur = min((1 - ar) * full, ox_h - used)
      local a1 = min(1, ar + dur / full)
      local rx0, ry0, rx1, ry1 = act.plot_row_line(s, plot, row)
      if row % 2 == 0 then rx0, rx1 = rx1, rx0 end
      go(pl, rx0 + (rx1 - rx0) * ar, ry0, plot.name)
      -- (the team turned at the headland to face along the row: the ox swings round, S14)
      work(pl, "turn", 0.12, { fx = rx1, fy = ry1 })
      work(pl, "till", dur, { ref = plot.id, row = row, x1 = rx0 + (rx1 - rx0) * a1, y1 = ry1, rate = 1 / full, ox = ox.k })
      plot.row_claim[row] = p.id
      used = used + dur
      local nxt = nil
      for r = 1, plot.rows do
        local rs = plot.row_st[r]
        if plot.row_claim[r] == 0 and (rs == 0 or rs == 3) then nxt = r; break end
      end
      row = nxt
    end
    work(pl, "unyoke", 0.1, { ox = ox.k })
    local verb = "Plough with the ox"
    return a0 > 0 and sfmt("%s: %s (row %d, %d%% done)", verb, plot.name, cd.b, floor(a0 * 100)) or (verb .. ": " .. plot.name), plot.name
  end
  local a0 = row_stint(W, s, p, now, plot, cd.b, "till", full, 37)
  local verb = plot.stage == "rest" and "Plough the fallow of" or "Hoe"
  return a0 > 0 and sfmt("%s %s (row %d, %d%% done)", verb, plot.name, cd.b, floor(a0 * 100)) or (verb .. " " .. plot.name), plot.name
end

PLAN.thresh = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local g = s.granary
  g.threshers = (g.threshers or 0) + 1
  local x, y = near_spot(W, p, now, g.dx, g.dy - 0.04, 0.04, 27)
  go(pl, x, y, "the threshing floor")
  work(pl, "thresh", c.thresh_h * (0.8 + 0.5 * u01(W, p, now, 28)), { fx = g.dx, fy = g.dy - 0.04 })
  return "Thresh the sheaves", "the threshing floor"
end

PLAN.grub = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local tr = s.trees
  local k = cd.a
  tr.claim[k] = p.id
  s.tv = (s.tv or 0) + 1
  go(pl, tr.x[k] + 0.02, tr.y[k], "the new field ground")
  work(pl, "grub", c.grub_h * (0.8 + 0.5 * u01(W, p, now, 30)) * (p.strong and 0.85 or 1), { ref = k, fx = tr.x[k], fy = tr.y[k] })
  return "Dig out a stump", "the new field ground"
end

PLAN.forage = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local tr = s.trees
  -- a stand of trees at the woods' edge (a different one each outing)
  local best, bs = 0, 1e9
  local start = 1 + floor(u01(W, p, now, 31) * tr.n)
  for j = 0, 199 do
    local k = (start + j * 7) % tr.n + 1
    if tr.st[k] == 1 then
      local x, y = tr.x[k], tr.y[k]
      local r = sqrt(x * x + y * y)
      if r > 0.5 and r < 1.3 then
        local d = dist(p.x, p.y, x, y) + u01(W, p, now, 32 + j) * 0.4
        if d < bs then best, bs = k, d end
      end
    end
  end
  if best == 0 then return PLAN.rest(W, s, p, ctx, now, cd) end
  if ctx.stage == "child" then ctx.hh.child_gathering = p.id else ctx.hh.foraging = p.id end
  local x, y = tr.x[best] + 0.025, tr.y[best] + 0.01
  go(pl, x, y, "the woods' edge")
  local x2, y2 = near_spot(W, p, now, x, y, 0.06, 33)
  work(pl, "forage", c.forage_h * (0.8 + 0.5 * u01(W, p, now, 34)), { x1 = x2, y1 = y2, luck = u01(W, p, now, 35) })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Gather food in the woods", "the woods' edge"
end

-- (S19) Stone: quarrying at an outcrop's face (the stone left lying there).
PLAN.quarry = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.quarriers = (b.quarriers or 0) + 1
  local x, y = near_spot(W, p, now, b.dx, b.dy, 0.03, 81)
  go(pl, x, y, "the " .. b.label)
  work(pl, "quarry", c.quarry_h * (0.8 + 0.4 * u01(W, p, now, 82)) / M.tool_factor(W, p), { ref = b.id, fx = b.cx, fy = b.cy })
  return "Quarry stone", "the " .. b.label
end

-- A quarried stone carried from the face to a site or the yard (one at a time: it is heavy).
PLAN.haul_stone = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  local to = s.buildings[cd.b]
  b.haul_claim = (b.haul_claim or 0) + 1
  if to.state == "site" then to.stone_claim = (to.stone_claim or 0) + 1 end
  go(pl, b.dx, b.dy, "the " .. b.label)
  pl[#pl + 1] = { op = "pick", what = "stone", ref = b.id }
  go(pl, to.dx, to.dy, to.state == "site" and to.label or "the yard")
  pl[#pl + 1] = { op = "drop", to = to.state == "site" and "stone_site" or "stone_yard", ref = to.id }
  return "Carry a stone to the " .. (to.state == "site" and to.label or "yard"), to.state == "site" and to.label or "the yard"
end

-- Yard stones to a site's footing.
PLAN.deliver_stone = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  local yard = s.buildings[cd.b]
  b.stone_claim = (b.stone_claim or 0) + 1
  yard.stone_claim = (yard.stone_claim or 0) + 1
  go(pl, yard.dx, yard.dy, "the yard")
  pl[#pl + 1] = { op = "pick", what = "yard_stone", ref = yard.id }
  go(pl, b.dx, b.dy, b.label)
  pl[#pl + 1] = { op = "drop", to = "stone_site", ref = b.id }
  return "Bring a stone to the " .. b.label, b.label
end

-- A stint of shaping a quern at the yard (two hard stones taken up when the work begins); finished,
-- it is carried home.
PLAN.quern = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local yard = s.buildings[cd.a]
  local hh = ctx.hh
  hh.querning = p.id
  local x, y = near_spot(W, p, now, yard.dx, yard.dy, 0.04, 83)
  go(pl, x, y, "the yard")
  -- (the hours of work left at this hand's pace - skill and tools, as WORK.quern counts them: S19,
  -- booking the bare hours left, each stint did only part of what remained and the quern was never
  -- finished)
  local rate = (0.6 + 0.6 * (p.sk_craft or 0.2)) * (0.55 + 0.45 * (hh.tools or 1))
  local left = (c.quern_h - (hh.quern_work or 0)) / max(0.05, rate) + 0.02
  work(pl, "quern", min(left, (1.8 + 1.0 * u01(W, p, now, 84)) * p.dil), { ref = yard.id, fx = yard.cx, fy = yard.cy })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "querning" }
  return "Shape a quern", "the yard"
end

-- (S19) Iron: digging ore at a deposit (left lying there for the carriers).
PLAN.mine = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.miners = (b.miners or 0) + 1
  local x, y = near_spot(W, p, now, b.dx, b.dy, 0.03, 85)
  go(pl, x, y, "the " .. b.label)
  work(pl, "mine", c.mine_h * (0.8 + 0.4 * u01(W, p, now, 86)) / M.tool_factor(W, p), { ref = b.id, fx = b.cx, fy = b.cy })
  return "Dig iron ore", "the " .. b.label
end

-- Ore (cd.b 1) or charcoal (cd.b 2) carried from where it lies to the forge.
PLAN.haul_iron = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  local f = s.forge
  local ore = cd.b == 1
  if ore then b.ore_claim = (b.ore_claim or 0) + 1 else b.char_claim = (b.char_claim or 0) + 1 end
  go(pl, b.dx, b.dy, "the " .. b.label)
  pl[#pl + 1] = { op = "pick", what = ore and "ore" or "charcoal", ref = b.id, n = ore and c.carry_ore or c.carry_charcoal }
  go(pl, f.dx, f.dy, "the forge")
  pl[#pl + 1] = { op = "drop", to = "forge", ref = f.id }
  return ore and "Carry ore to the forge" or "Carry charcoal to the forge", "the forge"
end

-- Logs from the woodstore into the clamp.
PLAN.charge_clamp = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local cl = s.buildings[cd.a]
  local n = p.strong and 2 or 1
  cl.charge_claim = (cl.charge_claim or 0) + n
  go(pl, s.wood_store.dx, s.wood_store.dy, "the woodstore")
  pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = s.wood_store.id, n = n }
  go(pl, cl.dx, cl.dy, "the charcoal clamp")
  pl[#pl + 1] = { op = "drop", to = "clamp", ref = cl.id, n = n }
  return "Stack logs in the charcoal clamp", "the charcoal clamp"
end

-- Covering the stacked clamp with turf and firing it; or, burned through, opening it.
PLAN.fire_clamp = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local cl = s.buildings[cd.a]
  cl.firer = p.id
  go(pl, cl.dx, cl.dy, "the charcoal clamp")
  work(pl, "fire_clamp", c.fire_clamp_h * (0.8 + 0.4 * u01(W, p, now, 87)), { ref = cl.id, fx = cl.cx, fy = cl.cy })
  return "Fire the charcoal clamp", "the charcoal clamp"
end
PLAN.open_clamp = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local cl = s.buildings[cd.a]
  cl.firer = p.id
  go(pl, cl.dx, cl.dy, "the charcoal clamp")
  work(pl, "open_clamp", c.open_clamp_h * (0.8 + 0.4 * u01(W, p, now, 88)), { ref = cl.id, fx = cl.cx, fy = cl.cy })
  return "Open the charcoal clamp", "the charcoal clamp"
end

-- Smelting a bloom at the forge's furnace.
PLAN.smelt = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local f = s.buildings[cd.a]
  f.smith = p.id
  go(pl, f.dx, f.dy, "the forge")
  work(pl, "smelt", c.smelt_h * (0.85 + 0.3 * u01(W, p, now, 89)), { ref = f.id, fx = f.cx, fy = f.cy })
  return "Smelt iron", "the forge"
end

-- Forging iron edges at the forge, then taking them to the household that lacks them most (the
-- smith's own first, then kin, then the others); one made for trade is kept at home.
PLAN.smith = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local f = s.buildings[cd.a]
  f.smith = p.id
  go(pl, f.dx, f.dy, "the forge")
  work(pl, "smith", c.forge_h * (0.85 + 0.3 * u01(W, p, now, 90)), { ref = f.id, fx = f.cx, fy = f.cy })
  -- whose: the household with the most worn iron (none = 0), own first on ties, then nearest
  local V = W.S.v3
  local iron = require("sim.agents.iron")
  local best, bs = nil, 1e9
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
    if home and iron.iron(hh) < c.iron_redo then
      local sc = iron.iron(hh) - (hid == p.hh and 0.05 or 0) + dist(f.dx, f.dy, home.dx, home.dy) * 0.01
      if sc < bs then best, bs = home, sc end
    end
  end
  local to = best or ctx.home
  if to then
    go(pl, to.dx, to.dy, to.label or "a house", to.id)
    pl[#pl + 1] = { op = "drop", to = "iron_to", ref = to.id }
  end
  return "Forge iron edges for tools", "the forge"
end

-- (S19) Gathering on the shore: a stretch of the waterline (a different one each outing, the nearest
-- of a few), a basket filled at low water, home.
PLAN.shore = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local mapm = require("sim.agents.map")
  local L = require("sim.agents.birds").shore(s)
  if #L == 0 or not ctx.home then return PLAN.rest(W, s, p, ctx, now, cd) end
  local best, bs = 0, 1e9
  local start = 1 + floor(u01(W, p, now, 36) * #L)
  for j = 0, 15 do
    local i = L[(start + j * 13) % #L + 1]
    local x, y = mapm.tile_xy(s, i)
    local d = dist(p.x, p.y, x, y) + u01(W, p, now, 37 + j) * 0.3
    if d < bs then best, bs = i, d end
  end
  ctx.hh.shore = p.id
  local x, y = mapm.tile_xy(s, best)
  go(pl, x, y, "the shore")
  work(pl, "shore", c.shore_h * (0.8 + 0.4 * u01(W, p, now, 38)), { x1 = x, y1 = y, luck = u01(W, p, now, 39) })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Gather on the shore", "the shore"
end

PLAN.tend = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local plot = s.plots[cd.a]
  plot.tend_claim = (plot.tend_claim or 0) + 1
  local row = 1 + floor(u01(W, p, now, 20) * plot.rows)
  local x0, y0, x1, y1 = act.plot_row_line(s, plot, row)
  if u01(W, p, now, 21) < 0.5 then x0, x1 = x1, x0 end
  go(pl, x0, y0, plot.name)
  work(pl, "tend", c.tend_h * (0.7 + 0.6 * u01(W, p, now, 22)) * p.dil, { ref = cd.a, x1 = x1, y1 = y1 })
  return "Weed " .. plot.name, plot.name
end

PLAN.harvest = function(W, s, p, ctx, now, cd)
  local c = C()
  plan_reset(p)
  local plot = s.plots[cd.a]
  plot.row_claim[cd.b] = p.id
  local full = c.harvest_row_h * (1.25 - 0.45 * p.sk_farm) * (0.9 + 0.2 * u01(W, p, now, 23)) / tool_factor(W, p)
  local a0 = row_stint(W, s, p, now, plot, cd.b, "harvest", full, 38)
  return a0 > 0 and sfmt("Reap %s (row %d, %d%% cut)", plot.name, cd.b, floor(a0 * 100)) or ("Reap " .. plot.name), plot.name
end

PLAN.haul_grain = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local plot = s.plots[cd.a]
  plot.haul_claim = (plot.haul_claim or 0) + 1
  local g = s.granary
  go(pl, plot.stx, plot.sty, plot.name)
  pl[#pl + 1] = { op = "pick", what = "sheaves", ref = cd.a }
  go(pl, g.dx, g.dy, "the granary")
  pl[#pl + 1] = { op = "drop", to = "granary", ref = g.id }
  return "Carry sheaves to the granary", plot.name
end

PLAN.fell = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local tr = s.trees
  local k = cd.a
  tr.claim[k] = p.id
  s.tv = (s.tv or 0) + 1
  -- stand beside the trunk on the side they come from
  local dx, dy = p.x - tr.x[k], p.y - tr.y[k]
  local l = sqrt(dx * dx + dy * dy)
  if l < 1e-6 then dx, dy, l = 1, 0, 1 end
  local sx, sy = tr.x[k] + dx / l * 0.022, tr.y[k] + dy / l * 0.022
  go(pl, sx, sy, "the woods")
  local done = tr.prog and tr.prog[k] or 0
  local dur = c.fell_h * (0.4 + 0.6 * tr.size[k]) * (1.3 - 0.6 * p.sk_wood) * (1 - done) / tool_factor(W, p)
  work(pl, "chop", max(0.15, dur), { ref = k, fx = tr.x[k], fy = tr.y[k] })
  return "Fell a tree", "the woods"
end

PLAN.haul_logs = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local tr = s.trees
  local k = cd.a
  tr.claim[k] = -p.id
  s.tv = (s.tv or 0) + 1
  go(pl, tr.x[k] + 0.015, tr.y[k], "the felled tree")
  pl[#pl + 1] = { op = "pick", what = "logs", from = "tree", ref = k }
  -- straight to a site that is short of logs, else the woodstore
  local site = s.site_short and s.buildings[s.site_short] or nil
  -- (S18: still short now - the morning's reckoning may be stale)
  if site and site.state == "site" and site.logs_need - site.logs_have - (site.deliver_claim or 0) * 1.5 > 0 then
    site.deliver_claim = (site.deliver_claim or 0) + 1
    go(pl, site.dx, site.dy, site.label)
    pl[#pl + 1] = { op = "drop", to = "site", ref = site.id }
    return "Carry logs to the " .. site.label, site.label
  end
  local w = s.wood_store
  if not w then
    -- (S17: no woodstore yet: to the household's own woodpile)
    local home = ctx.home
    if home then
      go(pl, home.dx, home.dy, "home", home.id)
      pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
      return "Carry logs to our woodpile", "home"
    end
    pl[#pl] = nil
    tr.claim[k] = 0
    return PLAN.rest(W, s, p, ctx, now, cd)
  end
  go(pl, w.dx, w.dy, "the woodstore")
  pl[#pl + 1] = { op = "drop", to = "woodstore", ref = w.id }
  return "Carry logs to the woodstore", "the woodstore"
end

PLAN.deliver = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.deliver_claim = (b.deliver_claim or 0) + 1
  -- (S18: from the household's woodpile at a young place with no woodstore)
  local w = (cd.b ~= 0 and s.buildings[cd.b]) or s.wood_store
  go(pl, w.dx, w.dy, w == s.wood_store and "the woodstore" or "our woodpile")
  pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = w.id, n = p.strong and 2 or 1 }
  go(pl, b.dx, b.dy, b.label)
  pl[#pl + 1] = { op = "drop", to = "site", ref = b.id }
  return "Bring logs to the " .. b.label, b.label
end

PLAN.build = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.builders = (b.builders or 0) + 1
  -- a spot just outside the footprint, on one of its sides
  local T = s.T
  local side = floor(u01(W, p, now, 24) * 4)
  local along = (u01(W, p, now, 29) - 0.5) * 0.8
  local hw, hh2 = b.w * T * 0.5 + T * 0.7, b.h * T * 0.5 + T * 0.7
  local x, y
  if b.kind == "bridge" then
    -- (a bridge is built out from its banks: the near one, or the far one when it is reachable)
    local far = side >= 2 and b.ex
    x, y = (far and b.ex or b.dx) + along * T, (far and b.ey or b.dy) + along * T * 0.5
    go(pl, x, y, b.label)
    work(pl, "build", (0.8 + 0.9 * u01(W, p, now, 25)) * p.dil, { ref = b.id, fx = b.cx, fy = b.cy })
    return "Build the bridge", "the river"
  end
  if side == 0 then x, y = b.cx + along * b.w * T, b.cy - hh2
  elseif side == 1 then x, y = b.cx + along * b.w * T, b.cy + hh2
  elseif side == 2 then x, y = b.cx - hw, b.cy + along * b.h * T
  else x, y = b.cx + hw, b.cy + along * b.h * T end
  go(pl, x, y, b.label)
  work(pl, "build", (0.8 + 0.9 * u01(W, p, now, 25)) * p.dil, { ref = b.id, fx = b.cx, fy = b.cy })
  return "Build the " .. b.label, b.label
end

PLAN.fish = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.dock
  local boat = s.boats[cd.a]
  boat.crew = p.id    -- (claimed now; released if the plan is cut before they board)
  local gx, gy = fish_ground(W, s, p, now)
  go(pl, b.dx, b.dy, "the dock")
  -- (a net wants two in the boat: the skipper waits a little at the dock for a mate, D-096)
  boat.mate_want, boat.mate = p.id, 0
  work(pl, "wait_mate", c.mate_wait, { boat = boat.id, fx = b.mx, fy = b.my })
  work(pl, "board", 0.08, { boat = boat.id, fx = b.mx, fy = b.my })
  pl[#pl + 1] = { op = "go", x = gx, y = gy, place = "the fishing ground", water = true }
  work(pl, "fish", (c.fish_h[1] + (c.fish_h[2] - c.fish_h[1]) * u01(W, p, now, 53)) * p.dil, { boat = boat.id })
  pl[#pl + 1] = { op = "go", x = boat.mx or b.mx, y = boat.my or b.my, place = "the dock", water = true }
  pl[#pl + 1] = { op = "go", x = b.mx, y = b.my, place = "the dock", water = true }
  work(pl, "land", 0.08, { boat = boat.id })
  local home = ctx.home
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  end
  return "Go fishing", "the fishing ground"
end

-- The trip (D-097, D-101), from wherever it stands: out with the goods (at home), the exchanges
-- (at the other village: what they find there decides with whom and how much), the way home. A
-- night away is slept at the other village's square.
-- (S14) A road leg as it is walked: by day only; where it runs past bedtime the night is camped on
-- the road and the walk goes on at first light. Returns the leg's whole duration from local hour lh.
local function road_dur(lh, hrs, wake, bed)
  local t, left = lh, hrs
  for _ = 1, 8 do
    local tod = t % 24
    if tod >= bed or tod < wake then
      t = t + (wake - tod) % 24
    else
      local can = bed - tod
      if can >= left then return t + left - lh end
      t, left = t + can, left - can
    end
  end
  return t + left - lh
end
M.road_dur = road_dur

local function trip_legs(W, s, p, ctx, now)
  local c = C()
  local pl = p.plan
  local V = W.S.v3
  local tr = p.trip
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  if here == tr.from and not tr.back and econ.GOODS[tr.give].beast then
    -- (S15, R4) a drove: provisions from home, then each beast to be sold taken by the halter where
    -- it is, and the way to the gate at the beasts' pace
    local GX = econ.GOODS[tr.give]
    if p.carry ~= "pack" then
      local home = ctx.home
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id }
    end
    local H = home_s.herds and home_s.herds[GX.beast]
    local want = floor(tr.n + 1e-6) - #(stock.led_by(home_s, p, GX.beast))
    if H and want > 0 then
      for _, a in ipairs(stock.for_sale(H, p.hh, tr.give, now, want)) do
        local ax, ay = stock.pos(a, now)
        go(pl, ax, ay, "a " .. GX.name1 .. " to sell")
        work(pl, "halter", 0.06, { herd = stock.HIDX[GX.beast] + 1, ref = a.k, fx = ax, fy = ay })
      end
    end
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    pl[#pl].lead = GX.beast
    local _, hrs = trade.road(W, home_s, o)
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs * c.drove_road, ctx.wake, ctx.bed), { to = o.id, from = home_s.id, lead = GX.beast })
    return
  end
  if here == tr.from and not tr.back then
    -- out: provisions and the goods from home (the village's from its stores too), to the gate
    if p.carry ~= "pack" then
      local home = ctx.home
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id }
      local inhouse = home.stock[tr.give == "grain" and "food" or tr.give] or 0
      if not tr.ask then pl[#pl + 1] = { op = "pick", what = "pack", good = tr.give, ref = home.id, n = tr.n } end
      if not tr.ask and econ.GOODS[tr.give].pool == "village" and inhouse < tr.n - 0.05 then
        local store = tr.give == "logs" and home_s.wood_store or home_s.granary
        if store then
          go(pl, store.dx, store.dy, store.label or "the stores")
          pl[#pl + 1] = { op = "pick", what = "pack", good = tr.give, ref = store.id, n = tr.n - inhouse }
        end
      end
    end
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    local _, hrs = trade.road(W, home_s, o)
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs, ctx.wake, ctx.bed), { to = o.id, from = home_s.id })
    return
  end
  if here == tr.to then
    if ctx.night and not tr.slept then
      -- (a night's lodging at the square)
      local until_t = next_local(now, ctx.lh, ctx.wake)
      go(pl, o.sqx or 0, o.sqy or 0, o.name .. "'s square")
      work(pl, "sleep", until_t - now, { trip_sleep = true })
    end
    if tr.ask and not tr.dealt and (p.at or p.sett) == tr.to then
      -- (asking for help: at the granary, what the village will give, D-103)
      econ.learn(W, home_s, o, now)
      econ.learn(W, o, home_s, now)
      tr.gift = econ.relief_gift(W, o, home_s, 1)
      tr.dealt, tr.deals = true, {}
      if o.granary then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "ask", c.deal_h, { ref = o.granary.id })
      end
    end
    if not tr.dealt and (p.at or p.sett) == tr.to then
      -- (on arriving they hear how things stand here, and tell how things stand at home)
      econ.learn(W, home_s, o, now)
      econ.learn(W, o, home_s, now)
      tr.have = p.pack and p.pack[tr.give] or 0
      tr.deals = econ.deals(W, o, tr)
      tr.dealt = true
      -- (S19) and what is owed to their village's households here is asked for
      for _, i in ipairs(econ.owed_at(W, home_s, o)) do
        local d = econ.debts(W)[i]
        tr.deals[#tr.deals + 1] = { collect = i, b = d.db, q = 0, qh = 0, k = 0 }
      end
      if #tr.deals == 0 then
        people.remember(p, now, sfmt("Nobody at %s wanted our %s at a price worth it now", o.name, econ.GOODS[tr.give].name))
      end
    end
    local pending = false
    for di, d in ipairs(tr.deals or {}) do
      if not d.done or d.owed then
        local hb
        if d.b == 0 then hb = (tr.give == "logs") and o.wood_store or o.granary
        else
          local hh = V.households[d.b]
          hb = hh and o.buildings[hh.home or 0]
        end
        if hb and not d.done and d.collect then
          -- (S19) a debt asked for at the debtor's house
          go(pl, hb.dx, hb.dy, hb.label or "a house")
          work(pl, "collect", c.deal_h, { di = di, ref = hb.id })
          pending = true
        elseif hb and not d.done then
          -- (beasts are bargained for at the pen they will go into, S15)
          local GX = econ.GOODS[tr.give]
          local pen = GX.beast and o.herds and o.herds[GX.beast] and o.buildings[o.herds[GX.beast].pen]
          local at = pen or hb
          go(pl, at.dx, at.dy, at.label or "a house")
          work(pl, "deal", c.deal_h, { di = di, ref = hb.id })
          pending = true
        end
        -- (what is paid from elsewhere is loaded there)
        local src = o.buildings[d.src or 0]
        if src and src ~= hb then
          go(pl, src.dx, src.dy, src.label or "the stores")
          pl[#pl + 1] = { op = "pick", what = "owed", ref = src.id, di = di }
        end
      end
    end
    if o.granary and p.hunger > 0.3 then
      -- (a meal for the traveller from the village's store: guests are fed)
      go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
      work(pl, "guest_meal", 0.4, { ref = o.granary.id })
    end
    -- (S14) the way home is planned when the deals are done, at the hour they end
    if pending then return end
    local _, hrs = trade.road(W, o, home_s)
    if not ctx.night and ctx.lh + hrs > ctx.sunset + 0.5 and hrs < ctx.sunset - ctx.wake then
      -- too late to be home by dusk: a night's lodging at the square, off at first light
      local until_t = next_local(now, ctx.lh, ctx.wake)
      go(pl, o.sqx or 0, o.sqy or 0, o.name .. "'s square")
      work(pl, "sleep", until_t - now, { trip_sleep = true })
    end
    local gx, gy = trade.gate(W, o, home_s)
    go(pl, gx, gy, "the road home")
    local lh0 = (ctx.night or ctx.lh + hrs > ctx.sunset + 0.5) and ctx.wake or ctx.lh
    -- (beasts nobody bought go home with the drover, S15)
    local GX = econ.GOODS[tr.give]
    local lead = GX.beast and #(stock.led_by(o, p, GX.beast)) > 0 and GX.beast or nil
    if lead then pl[#pl].lead = lead end
    work(pl, "travel", road_dur(lh0 + 0.4, hrs * (lead and c.drove_road or 1), ctx.wake, ctx.bed), { to = home_s.id, from = o.id, lead = lead })
  end
  -- home: the goods into the house (help asked for goes to the village's granary)
  local home = ctx.home or people.home(W, home_s, p)
  if tr.ask and home_s.granary then home = home_s.granary end
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
  elseif here == p.sett and #pl == 0 then
    -- (S19) no home here any more (the household went to live elsewhere meanwhile): the trip is
    -- over; they will go after it (think.decide)
    p.trip = nil
    work(pl, "wait", 0.1, {})
  end
end

-- (S18) The larder shared out among a moving household's walkers: each takes their part for the
-- way before anyone packs the rest (from a house they lodge in, only their part of it, by mouths).
-- Returns the provisions and the bread to pack for this walker.
local function move_share(W, hh, home, mg, now, prov_want, carry)
  local V = W.S.v3
  mg.packed = (mg.packed or 0) + 1
  local walkers = 0
  for _, id in ipairs(hh.members) do
    local q = V.people[id]
    if q and q.alive and people.stage(q, now) ~= "infant" then walkers = walkers + 1 end
  end
  local others = max(0, walkers - mg.packed)
  local part = 1
  local owner = V.households[home.hh or 0]
  if owner and owner ~= hh then part = #hh.members / max(1, #hh.members + #owner.members) end
  local food = ((home.stock.meals or 0) + (home.stock.food or 0)) * part
  local prov = min(prov_want, food / (others + 1))
  local pack = max(0, min(carry, (home.stock.food or 0) * part - prov - prov_want * others))
  return prov, pack
end

-- (S16, R5) A household moving to another village (sim/agents/migrate.lua). From home: each
-- grown-up packs what they can carry of the household's food and provisions for the road, the one
-- who leads a kind of the household's beasts takes each of them by the halter, and all gather at
-- the gate and take the road together at the hour they agreed. At the other village (they are its
-- people from the gate on): to the house they lodge in until their own stands, the food into its
-- larder, and the move is over.
local function migrate_legs(W, s, p, ctx, now)
  local c = C()
  local pl = plan_reset(p)
  local V = W.S.v3
  local tr = p.trip
  local hh = V.households[p.hh]
  -- (S19) a ferry's skipper: the move is the household he sails over
  if tr.ferry then hh = V.households[tr.migrate or 0] end
  local mg = tr.move and p.move or (hh and hh.migrate)
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  local grown = ctx.stage == "adult" or ctx.stage == "elder" or ctx.stage == "apprentice"
  if mg and mg.given_up and tr.migrate then
    -- (S18) the move was given up: home with what was packed
    local home = ctx.home
    if home and here == p.sett then
      go(pl, home.dx, home.dy, "home", home.id)
      pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
      return "Unpack: we are staying", "home"
    end
    p.trip = nil
    return "Stay at " .. home_s.name, home_s.name
  end
  if mg and mg.sea and not mg.arrived and not tr.move then
    -- (S18) by sea, in the household's own boat
    return M.sea_move_legs(W, s, p, ctx, now, pl, tr, hh, mg, home_s, o, grown)
  end
  if here == tr.from and not tr.back and mg and not mg.arrived then
    local home = ctx.home
    if home and p.carry ~= "pack" and not tr.packed then
      tr.packed = true
      local prov, pack = move_share(W, hh, home, mg, now, c.trade_prov * 2, grown and c.mig_carry or 0)
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = prov }
      if grown and pack > 0.05 then pl[#pl + 1] = { op = "pick", what = "pack", good = "grain", ref = home.id, n = pack } end
    end
    -- the household's beasts of the kind this one leads, each taken by the halter where it is
    local kind
    for _, k in ipairs(stock.HERDS) do if mg.lead and mg.lead[k] == p.id then kind = k end end
    if kind then
      tr.lead_kind = kind
      local H = home_s.herds and home_s.herds[kind]
      for _, a in ipairs(H and H.animals or {}) do
        if a.alive and a.owner == hh.id and (a.led or 0) == 0 and (a.yoke or 0) == 0 and (a.held or 0) == 0 then
          local ax, ay = stock.pos(a, now)
          go(pl, ax, ay, "a " .. (stock.SP(a.sp) and a.sp or "beast") .. " of ours")
          work(pl, "halter", 0.05, { herd = stock.HIDX[kind] + 1, ref = a.k, fx = ax, fy = ay, mig = true })
        end
      end
    end
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    if kind then pl[#pl].lead = kind end
    work(pl, "gather", 0, { until_t = mg.depart })
    local _, hrs = trade.road(W, home_s, o)
    local lh0 = (ctx.lh + max(0, mg.depart - now)) % 24
    work(pl, "travel", road_dur(lh0 + 0.4, hrs * (kind and c.drove_road or 1), ctx.wake, ctx.bed),
      { to = o.id, from = home_s.id, lead = kind, migrate = true,
        why = (mg.why or ("Moving to " .. o.name)) .. ": on the road to " .. o.name .. (kind and " with our beasts" or " with the household") })
    return "Move to " .. o.name, o.name
  end
  -- arrived: to the house they lodge in, the food into its larder
  local b = hh and o.buildings[hh.home or 0]
  if b then
    -- (S18) come by sea: what the boat brought carried up from the dock a load at a time (the
    -- household's grown-ups, each their share of the loads)
    local sea = mg and mg.sea
    local boat = sea and sea.boat2 and o.boats and o.boats[sea.boat2]
    local od = o.dock
    if boat and boat.cargo and od and grown and p.carry ~= "pack" then
      local loads, mine = 0, 0
      for _, g in ipairs(econ.ORDER) do
        local q = boat.cargo[g] or 0
        if q > 1e-3 then loads = loads + q * econ.GOODS[g].wt end
      end
      local hands = 0
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        local st = q and q.alive and people.stage(q, now)
        if st == "adult" or st == "elder" or st == "apprentice" then hands = hands + 1 end
      end
      local share = math.ceil(loads / max(1, hands) - 1e-6)
      for _, g in ipairs(econ.ORDER) do
        local q = boat.cargo[g] or 0
        while q > 1e-3 and mine < share do
          go(pl, od.dx, od.dy, "the dock")
          pl[#pl + 1] = { op = "pick", what = "cargo", good = g, boat = boat.id }
          go(pl, b.dx, b.dy, b.label or "the house we lodge in", b.id)
          pl[#pl + 1] = { op = "drop", to = "home", ref = b.id }
          q, mine = q - 1 / econ.GOODS[g].wt, mine + 1
        end
      end
    end
    go(pl, b.dx, b.dy, b.label or "the house we lodge in", b.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = b.id, trip_end = true }
    return "Settle in at " .. o.name, o.name
  end
  -- (nowhere to lodge: the camp at the edge of the village)
  local x, y = hh and hh.camp and hh.camp.x or o.sqx or 0, hh and hh.camp and hh.camp.y or o.sqy or 0
  go(pl, x, y, "our camp")
  work(pl, "wait", 0.2, {})
  p.trip = nil
  return "Camp at " .. o.name, o.name
end

-- (S18, R5) A household moving by sea in its own boat (migrate.sea_plan), from wherever it stands.
-- At home: each packs provisions for the passage and the grown-ups bread (the skipper carries the
-- household's goods to the boat a load at a time instead: sea.loads); whoever leads a kind of the
-- household's beasts takes the ones that will go in the boat (sea.ship) by the halter; all to the
-- dock, waiting there for the hour agreed. At the dock: the skipper waits for everyone (the babes in
-- their carers' arms) and for weather fit to sail; then boards and takes them aboard with the
-- beasts (WORK.embark), rows out to the open sea and sails (a voyage that moves everyone aboard).
-- At the other village's dock: landing (WORK.land: everyone ashore, the household and its boat that
-- village's now, the beasts let go to its herd); then migrate_legs' arrival (the goods carried up).
function M.sea_move_legs(W, s, p, ctx, now, pl, tr, hh, mg, home_s, o, grown)
  local c = C()
  local V = W.S.v3
  local sea = mg.sea
  local here = p.at or p.sett
  -- (S18) founding by sea: the place is made when they sail into its waters (WORK.voyage); until
  -- then it has no name but "the place we chose"
  local found = hh.found == mg
  local site = found and mg.site or nil
  if found then o = require("sim.agents.founding").place_at(W, site) end
  local oname = o and o.name or "the place we chose"
  local boat = home_s.boats and home_s.boats[sea.boat]
  local hd = home_s.dock
  local route = found and trade.sea_route_to(W, home_s, site) or (o and trade.sea_route(W, home_s, o))
  local skipper = sea.skipper == p.id
  local sk = V.people[sea.skipper or 0]
  if not boat or not hd or not route or (boat.lost and not tr.embarked)
    or (sea.ferry and not tr.embarked and not (sk and sk.alive and sk.ferry == hh.id)) then
    -- (the boat is gone before they could sail - rotted, wrecked: they stay, and may think again)
    mg.given_up = now
    if boat then boat.move = nil end
    people.remember(p, now, "We could not go: the boat was gone")
    p.trip = nil
    return "Stay: the boat is gone", home_s.name
  end
  if o and here == o.id and p.boat then
    -- (over the sea: rowing in to the dock - or the beach of the new place - and landing)
    local od = o.dock or o.beach
    local at = o.dock and (o.name .. "'s dock") or ("the shore of " .. oname)
    pl[#pl + 1] = { op = "go", x = od.mx, y = od.my, place = at, water = true, why = mg.why .. ": rowing in to " .. at }
    work(pl, "land", 0.08, { boat = boat.id, migrate = not found or nil, found = found or nil, why = mg.why .. ": landing at " .. at })
    return "Land at " .. oname, at
  end
  if here ~= home_s.id then
    -- (anyone else off the boat on the other map before the landing: wait by the water)
    local od = (o and (o.dock or o.beach)) or { dx = o and o.sqx or 0, dy = o and o.sqy or 0 }
    go(pl, od.dx, od.dy, oname)
    work(pl, "wait", 0.3, {})
    return "Wait for the others at " .. oname, oname
  end
  if p.boat and tr.embarked then
    -- (aboard and away: out to the open sea and over it)
    M.sea_out(W, pl, home_s, o, site, oname, route, boat, p, now, mg)
    return "Sail to " .. oname .. " with the household", "the sea"
  end
  local home = ctx.home or (found and hh.pack_from and home_s.buildings[hh.pack_from])
  if not tr.embarked and (mg.given_up or now < mg.depart - c.ready_h - 0.01) then
    -- (the sailing put off: home with what was packed, the beasts back to the herd; life goes on
    -- until the morning they try again)
    if home then
      go(pl, home.dx, home.dy, "home", home.id)
      pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true, why = mg.why .. ": home again until the morning we try to sail" }
      return "Home until we can sail", "home"
    end
    p.trip = nil
    return "Wait for the morning we sail", home_s.name
  end
  if tr.ferry and not tr.packed then
    -- (S19) a ferry's skipper: provisions from his own house for the passage there and back
    tr.packed = true
    if home then
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = c.trade_prov * 2 }
    end
  elseif home and p.carry ~= "pack" and not tr.packed then
    tr.packed = true
    local prov, pack = move_share(W, hh, home, mg, now, c.trade_prov * 2, (grown and not skipper) and (found and c.found_carry or c.mig_carry) or 0)
    if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
    pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = prov }
    if skipper then
      local left = sea.loads or 0
      -- (founders: their part of the granary first - seed and bread for the first months)
      local g = home_s.granary
      local share = found and (mg.share or 0) or 0
      while found and g and left > 0 and share > 1 do
        local q = min(share, 1 / econ.GOODS.grain.wt)
        go(pl, g.dx, g.dy, "the granary")
        pl[#pl + 1] = { op = "pick", what = "share", ref = g.id, n = q }
        go(pl, hd.dx, hd.dy, "the dock")
        pl[#pl + 1] = { op = "drop", to = "cargo", ref = hd.id, boat = boat.id }
        share, left = share - q, left - 1
      end
      -- (the household's goods to the boat, a load at a time: the dearest for their weight first)
      for _, gd in ipairs(M.SEA_GOODS) do
        local key = gd == "grain" and "food" or gd
        local have = home.stock[key] or 0
        local G = econ.GOODS[gd]
        while left > 0 and have * G.wt > 0.2 do
          local q = min(have, 1 / G.wt)
          if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
          pl[#pl + 1] = { op = "pick", what = "pack", good = gd, ref = home.id, n = q }
          go(pl, hd.dx, hd.dy, "the dock")
          pl[#pl + 1] = { op = "drop", to = "cargo", ref = hd.id, boat = boat.id }
          have, left = have - q, left - 1
        end
      end
    elseif grown and pack > 0.05 then
      pl[#pl + 1] = { op = "pick", what = "pack", good = "grain", ref = home.id, n = pack }
    end
  end
  -- the beasts going in the boat, each taken by the halter where it is
  local kind
  for _, k in ipairs(stock.HERDS) do if mg.lead and mg.lead[k] == p.id then kind = k end end
  if kind and not tr.haltered then
    tr.haltered = true
    tr.lead_kind = kind
    local H = home_s.herds and home_s.herds[kind]
    local want = {}
    for _, k in ipairs(sea.ship and sea.ship[kind] or {}) do want[k] = true end
    for _, a in ipairs(H and H.animals or {}) do
      if want[a.k] and a.alive and a.owner == hh.id and (a.led or 0) == 0 and (a.yoke or 0) == 0 and (a.held or 0) == 0 then
        local ax, ay = stock.pos(a, now)
        go(pl, ax, ay, "a " .. (stock.SP(a.sp) and a.sp or "beast") .. " of ours")
        work(pl, "halter", 0.05, { herd = stock.HIDX[kind] + 1, ref = a.k, fx = ax, fy = ay, mig = true })
      end
    end
  end
  local near = dsq(p.x - hd.dx) + dsq(p.y - hd.dy) < 0.0225
  if #pl > 0 or not near or now < mg.depart - 0.01 then
    go(pl, hd.dx, hd.dy, "the dock")
    if kind then pl[#pl].lead = kind end
    work(pl, "gather", 0, { until_t = mg.depart, fx = hd.mx, fy = hd.my })
    return "Go to " .. oname .. " by sea", "the dock"
  end
  if not skipper then
    -- (waiting on the dock to be taken aboard)
    work(pl, "wait_boat", 0.5, { fx = hd.mx, fy = hd.my })
    return "Wait to board", "the dock"
  end
  -- the skipper: the weather, and everyone here (a day too rough, or someone who does not come:
  -- the sailing is put off to the next morning; after mig_sea_tries mornings they give it up)
  local foul = trade.sea_rough(W, route, now)
  if foul or (tr.boardw or 0) >= c.sea_board_wait then
    mg.tries = (mg.tries or 0) + 1
    local why = foul and "a gale at sea" or "not everyone came down to the dock"
    if mg.tries >= c.mig_sea_tries then
      mg.given_up = now
      boat.move = nil
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive and people.stage(q, now) ~= "infant" then people.remember(q, now, sfmt("We gave up crossing to %s: %s, morning after morning", oname, why)) end
      end
      return M.sea_move_legs(W, s, p, ctx, now, pl, tr, hh, mg, home_s, o, grown)
    end
    local lh = clock.local_hour(now, W.grid.lon[home_s.cell])
    local wait = (c.mig_sea_dawn - lh) % 24
    if wait < 6 then wait = wait + 24 end
    mg.depart = now + wait
    tr.boardw = 0
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and people.stage(q, now) ~= "infant" then people.remember(q, now, sfmt("We could not sail to %s today (%s): we try again tomorrow", oname, why)) end
    end
    return M.sea_move_legs(W, s, p, ctx, now, pl, tr, hh, mg, home_s, o, grown)
  end
  local missing = nil
  for _, id in ipairs(hh.members) do
    local q = V.people[id]
    if q and q.alive and q ~= p and people.stage(q, now) ~= "infant" then
      local qx, qy = act.pos(q, now)
      local there = not q.at and q.sett == home_s.id and dsq(qx - hd.dx) + dsq(qy - hd.dy) < 0.0225
        and (q.act == "gather" or q.act == "wait_boat")
      if not there then missing = q; break end
    end
  end
  if missing then
    tr.boardw = (tr.boardw or 0) + 0.25
    work(pl, "wait_boat", 0.25, { fx = hd.mx, fy = hd.my, why = sfmt("Waiting at the dock for %s before we sail", missing.name) })
    return "Wait for the household", "the dock"
  end
  -- (the beasts brought down to the dock: a little while for any lagging behind)
  if (tr.beastw or 0) < 1 then
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      local k = q and q.alive and q.trip and q.trip.lead_kind
      if k and stock.drove_spread(home_s, q, k, now) > c.drove_gather then
        tr.beastw = (tr.beastw or 0) + 0.25
        work(pl, "wait_boat", 0.25, { fx = hd.mx, fy = hd.my, why = "Waiting for the beasts to come down to the boat" })
        return "Wait for the beasts", "the dock"
      end
    end
  end
  work(pl, "board", 0.08, { boat = boat.id, fx = hd.mx, fy = hd.my, why = mg.why .. ": everyone is here; into the boat" })
  work(pl, "embark", 0.15, { boat = boat.id, fx = hd.mx, fy = hd.my, why = mg.why .. ": taking the household aboard" })
  M.sea_out(W, pl, home_s, o, site, oname, route, boat, p, now, mg)
  return "Sail to " .. oname .. " with the household", "the sea"
end
-- (the skipper rows out to the open sea and sails the way over: to village o, or to the shore of
-- a place to be founded at planet cell `site`)
function M.sea_out(W, pl, home_s, o, site, oname, route, boat, p, now, mg)
  local c = C()
  local gx, gy = trade.sea_gate(W, home_s, route.cells[1])
  local wet = sea_wet(W, route, now)
  local hrs = route.len / c.sail_speed * (1 + 0.25 * (wet and 1 or 0))
  pl[#pl + 1] = { op = "go", x = gx, y = gy, place = "the open sea toward " .. oname, water = true, why = mg.why .. ": rowing the household out to the open sea" }
  work(pl, "voyage", hrs, { to = (not site) and o.id or nil, site = site, from = home_s.id, boat = boat.id, gate_c = route.cells[#route.cells],
    risk = sea_risk(W, route, boat, p, now), migrate = (not site) or nil, found = site and true or nil,
    why = sfmt("%s: sailing the household over to %s (%.0f hours%s)", mg.why, oname, hrs, wet and ", in wet weather" or "") })
end
-- (what a moving household loads into its boat first: the dearest for its weight)
M.SEA_GOODS = { "cloth", "leather", "cheese", "grain", "smoked", "wool", "hides" }

-- (S17, R5) A household going to make a new place (sim/agents/founding.lua). From home: provisions
-- for the longer road, each grown-up's pack from the larder, their part of the granary (seed and
-- bread for the first months), the household's beasts by the halter, all to the gate toward the
-- place and the road together at the hour agreed, at the party's pace. At the place (its people from
-- the moment they walk onto it): to the household's camp ground, the packs set down there.
local function found_legs(W, s, p, ctx, now)
  local c = C()
  local pl = plan_reset(p)
  local V = W.S.v3
  local tr = p.trip
  local hh = V.households[p.hh]
  local fd = hh and hh.found
  local home_s = V.settlements[tr.from]
  local here = p.at or p.sett
  local grown = ctx.stage == "adult" or ctx.stage == "elder" or ctx.stage == "apprentice"
  if fd and fd.sea and not fd.arrived and home_s then
    -- (S18) over the sea in the household's own boat
    return M.sea_move_legs(W, s, p, ctx, now, pl, tr, hh, fd, home_s, nil, grown)
  end
  if here == tr.from and fd and home_s then
    -- (a couple setting out from the house they lived in: their part of its larder)
    local home = ctx.home or (hh.pack_from and home_s.buildings[hh.pack_from])
    if home and home.state ~= "standing" then home = nil end
    if home and p.carry ~= "pack" and not tr.packed then
      tr.packed = true
      -- (the larder shared: each takes their part for the road before anyone packs the rest)
      fd.packed = (fd.packed or 0) + 1
      local walkers = 0
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive and people.stage(q, now) ~= "infant" then walkers = walkers + 1 end
      end
      local others = max(0, walkers - fd.packed)
      -- (from another's house: only their part of its larder, by mouths)
      local part = 1
      local owner = V.households[home.hh or 0]
      if owner and owner ~= hh then
        part = #hh.members / max(1, #hh.members + #owner.members)
      end
      local prov = min(c.trade_prov * 3, ((home.stock.meals or 0) + (home.stock.food or 0)) * part / (others + 1))
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = prov }
      if grown then
        -- (what is left once the others' provisions are set aside)
        pl[#pl + 1] = { op = "pick", what = "pack", good = "grain", ref = home.id,
          n = max(0, min(c.found_carry, (home.stock.food or 0) * part - prov - c.trade_prov * 3 * others)) }
        local g = home_s.granary
        if g and (fd.share or 0) > 0.5 then
          go(pl, g.dx, g.dy, "the granary")
          pl[#pl + 1] = { op = "pick", what = "share", ref = g.id, n = c.found_carry }
        end
      end
    end
    local kind
    for _, k in ipairs(stock.HERDS) do if fd.lead and fd.lead[k] == p.id then kind = k end end
    if kind then
      tr.lead_kind = kind
      local H = home_s.herds and home_s.herds[kind]
      for _, a in ipairs(H and H.animals or {}) do
        if a.alive and a.owner == hh.id and (a.led or 0) == 0 and (a.yoke or 0) == 0 and (a.held or 0) == 0 then
          local ax, ay = stock.pos(a, now)
          go(pl, ax, ay, "a " .. (stock.SP(a.sp) and a.sp or "beast") .. " of ours")
          work(pl, "halter", 0.05, { herd = stock.HIDX[kind] + 1, ref = a.k, fx = ax, fy = ay, mig = true })
        end
      end
    end
    local gx, gy = require("sim.agents.wild").edge_toward(W, home_s, tr.site)
    go(pl, gx, gy, "the way to the new place")
    if kind then pl[#pl].lead = kind end
    work(pl, "gather", 0, { until_t = fd.depart })
    local lh0 = (ctx.lh + max(0, fd.depart - now)) % 24
    local dur = road_dur(lh0 + 0.4, fd.hrs * (fd.slow and c.drove_road or 1), ctx.wake, ctx.bed)
    work(pl, "travel", dur, { site = tr.site, from = home_s.id, lead = kind, migrate = true, found = true,
      why = sfmt("%s: on the way to the place we chose, %.0f hours' walk with %s (%s)", fd.why, fd.hrs * (fd.slow and c.drove_road or 1),
        kind and "our beasts" or "the others", fd.swhy or "") })
    return "Go to make a new home", "the new place"
  end
  -- at the place: the packs set down at the household's camp ground
  local o = V.settlements[p.sett]
  local cb = o and hh and o.buildings[hh.camp or hh.camp_site or 0]
  if cb then
    local x, y = camp_spot(o, cb, p)
    -- (S18) come by sea: what the boat brought carried up from the beach a load at a time
    local sea = fd and fd.sea
    local boat = sea and sea.boat2 and o.boats and o.boats[sea.boat2]
    local ld = o.dock or o.beach
    if boat and boat.cargo and ld and grown and p.carry ~= "pack" then
      local loads, mine, hands = 0, 0, 0
      for _, g in ipairs(econ.ORDER) do loads = loads + (boat.cargo[g] or 0) * econ.GOODS[g].wt end
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        local st = q and q.alive and people.stage(q, now)
        if st == "adult" or st == "elder" or st == "apprentice" then hands = hands + 1 end
      end
      local share = math.ceil(loads / max(1, hands) - 1e-6)
      for _, g in ipairs(econ.ORDER) do
        local q = boat.cargo[g] or 0
        while q > 1e-3 and mine < share do
          go(pl, ld.dx, ld.dy, "the boat")
          pl[#pl + 1] = { op = "pick", what = "cargo", good = g, boat = boat.id }
          go(pl, x, y, "our camp")
          pl[#pl + 1] = { op = "drop", to = "home", ref = cb.id }
          q, mine = q - 1 / econ.GOODS[g].wt, mine + 1
        end
      end
    end
    go(pl, x, y, "our camp")
    pl[#pl + 1] = { op = "drop", to = "home", ref = cb.id, trip_end = true }
    return "Set down our packs at the camp", "our camp"
  end
  go(pl, o and o.sqx or 0, o and o.sqy or 0, "the place we chose")
  work(pl, "wait", 0.2, {})
  p.trip = nil
  return "Look about the new place", "the new place"
end
M.found_legs = found_legs

-- A trading voyage (D-102), from wherever it stands. Out: provisions, the cargo carried to the
-- boat a load at a time, the mate, the sea way (a voyage step). At the other village: to its dock,
-- the exchanges (the cargo carried from the boat to the buyers and what they pay back to the boat,
-- a load at a time; the mate minds the boat), the mate called aboard, the sea way home. Home: the
-- dock, the cargo carried home a load at a time.
function sea_legs(W, s, p, ctx, now)
  local c = C()
  local pl = p.plan
  local V = W.S.v3
  local tr = p.trip
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local boat = home_s.boats[tr.boat]
  local route = trade.sea_route(W, home_s, o)
  local here = p.at or p.sett
  local GX = econ.GOODS[tr.give]
  local hd = home_s.dock
  if here == tr.from and not tr.back and not tr.sailed then
    local home = ctx.home
    if p.carry == "pack" then
      go(pl, hd.dx, hd.dy, "the dock")
      pl[#pl + 1] = { op = "drop", to = "cargo", ref = hd.id, boat = boat.id }
    end
    if (p.prov or 0) < c.trade_prov then
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = c.trade_prov * 2 }
    end
    -- the cargo to the boat, a load at a time (the house first, then the village's stores)
    local left = tr.n - (boat.cargo and boat.cargo[tr.give] or 0) - (p.pack and p.pack[tr.give] or 0)
    local inhouse = home.stock[tr.give == "grain" and "food" or tr.give] or 0
    local store = tr.give == "logs" and home_s.wood_store or home_s.granary
    local loads = 0
    if GX.beast then
      -- (S18) a sea drove: each beast to be sold taken by the halter where it is and led down to the
      -- dock; they go in the boat when it is boarded
      left = 0
      tr.lead_kind = GX.beast
      local H = home_s.herds and home_s.herds[GX.beast]
      local want = floor(tr.n + 1e-6) - #(stock.led_by(home_s, p, GX.beast))
      if H and want > 0 then
        for _, a in ipairs(stock.for_sale(H, p.hh, tr.give, now, want)) do
          local ax, ay = stock.pos(a, now)
          go(pl, ax, ay, "a " .. GX.name1 .. " to sell")
          work(pl, "halter", 0.06, { herd = stock.HIDX[GX.beast] + 1, ref = a.k, fx = ax, fy = ay })
        end
      end
    end
    while left > 0.05 and loads < c.boat_hold + 1 do
      local q = min(left, 1 / GX.wt)
      local src = home
      if inhouse < q * 0.5 and GX.pool == "village" and store then src = store end
      go(pl, src.dx, src.dy, src.label or "home", src == home and home.id or nil)
      pl[#pl + 1] = { op = "pick", what = "pack", good = tr.give, ref = src.id, n = q }
      go(pl, hd.dx, hd.dy, "the dock")
      pl[#pl + 1] = { op = "drop", to = "cargo", ref = hd.id, boat = boat.id }
      left, inhouse, loads = left - q, inhouse - q, loads + 1
    end
    go(pl, hd.dx, hd.dy, "the dock")
    if GX.beast then pl[#pl].lead = GX.beast end
    boat.mate_want, boat.mate = p.id, 0
    work(pl, "wait_mate", c.mate_wait * 2, { boat = boat.id, fx = hd.mx, fy = hd.my })
    work(pl, "board", 0.08, { boat = boat.id, fx = hd.mx, fy = hd.my })
  end
  if here == tr.from and not tr.back then
    local gx, gy = trade.sea_gate(W, home_s, route.cells[1])
    pl[#pl + 1] = { op = "go", x = gx, y = gy, place = "the open sea toward " .. o.name, water = true }
    work(pl, "voyage", route.len / c.sail_speed * (1 + 0.25 * (sea_wet(W, route, now) and 1 or 0)), { to = o.id, from = home_s.id,
      boat = boat.id, gate_c = route.cells[#route.cells], risk = sea_risk(W, route, boat, p, now) })
    return
  end
  if here == tr.to then
    local od = o.dock
    if not tr.landed then
      pl[#pl + 1] = { op = "go", x = od.mx, y = od.my, place = o.name .. "'s dock", water = true }
      work(pl, "land", 0.08, { boat = boat.id })
      return
    end
    if tr.ask and not tr.dealt and tr.landed then
      -- (S16, R4) asking for help by sea: at the granary, what the village will give; then the
      -- grain carried to the boat a load at a time
      econ.learn(W, home_s, o, now)
      econ.learn(W, o, home_s, now)
      tr.gift = econ.relief_gift(W, o, home_s, c.boat_hold)
      tr.dealt, tr.deals = true, {}
      if o.granary then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "ask", c.deal_h, { ref = o.granary.id, sea = true })
        local loads = 0
        local left = tr.gift
        while left > 0.05 and loads < c.boat_hold do
          local q = min(left, 1 / econ.GOODS.grain.wt)
          go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
          pl[#pl + 1] = { op = "pick", what = "relief", ref = o.granary.id, n = q }
          go(pl, od.dx, od.dy, o.name .. "'s dock")
          pl[#pl + 1] = { op = "drop", to = "cargo", ref = od.id, boat = boat.id }
          left, loads = left - q, loads + 1
        end
      end
    end
    if not tr.dealt and tr.landed then
      econ.learn(W, home_s, o, now)
      econ.learn(W, o, home_s, now)
      tr.have = boat.cargo and boat.cargo[tr.give] or 0
      -- (S18: a sea drove's beasts, led off the boat)
      if GX.beast then tr.have = #(stock.led_by(o, p, GX.beast)) end
      tr.deals = econ.deals(W, o, tr, c.boat_hold)
      tr.dealt = true
      if #tr.deals == 0 then
        people.remember(p, now, sfmt("Nobody at %s wanted our %s at a price worth it now", o.name, GX.name))
      end
    end
    if not tr.homeward then
      for di, d in ipairs(tr.deals or {}) do
        local hb
        if d.b == 0 then hb = (tr.give == "logs") and o.wood_store or o.granary
        else
          local hh = V.households[d.b]
          hb = hh and o.buildings[hh.home or 0]
        end
        local src = o.buildings[d.src or 0]
        local paid_store = src ~= nil and src ~= hb
        local left = d.done and 0 or (d.left or d.q)
        local loads = 0
        if GX.beast and hb and not d.done then
          -- (S18) the beasts bargained for at the pen they go into; what is paid carried to the boat
          local pen = o.herds and o.herds[GX.beast] and o.buildings[o.herds[GX.beast].pen]
          local at = pen or hb
          go(pl, at.dx, at.dy, at.label or "the pen")
          pl[#pl].lead = GX.beast
          work(pl, "deal", c.deal_h, { di = di, ref = hb.id })
          if paid_store then
            go(pl, src.dx, src.dy, src.label or "the stores")
            pl[#pl + 1] = { op = "pick", what = "owed", ref = src.id, di = di }
          end
          go(pl, od.dx, od.dy, o.name .. "'s dock")
          pl[#pl].lead = GX.beast
          pl[#pl + 1] = { op = "drop", to = "cargo", ref = od.id, boat = boat.id }
          left = 0
        end
        while hb and left > 1e-3 and loads < c.boat_hold + 1 do
          local q = min(left, 1 / GX.wt)
          go(pl, od.dx, od.dy, o.name .. "'s dock")
          pl[#pl + 1] = { op = "pick", what = "cargo", good = tr.give, boat = boat.id, n = q }
          go(pl, hb.dx, hb.dy, hb.label or "a house")
          work(pl, "deal", c.deal_h, { di = di, ref = hb.id, part = true })
          if paid_store then
            go(pl, src.dx, src.dy, src.label or "the stores")
            pl[#pl + 1] = { op = "pick", what = "owed", ref = src.id, di = di }
          end
          go(pl, od.dx, od.dy, o.name .. "'s dock")
          pl[#pl + 1] = { op = "drop", to = "cargo", ref = od.id, boat = boat.id }
          left, loads = left - q, loads + 1
        end
        if d.owed and paid_store and left <= 1e-3 then
          go(pl, src.dx, src.dy, src.label or "the stores")
          pl[#pl + 1] = { op = "pick", what = "owed", ref = src.id, di = di }
          go(pl, od.dx, od.dy, o.name .. "'s dock")
          pl[#pl + 1] = { op = "drop", to = "cargo", ref = od.id, boat = boat.id }
        end
      end
      if o.granary and p.hunger > 0.3 then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "guest_meal", 0.4, { ref = o.granary.id })
      end
      -- (fair weather to sail home: a storm day is waited out at the dock)
      go(pl, od.dx, od.dy, o.name .. "'s dock")
      if trade.sea_rough(W, route, now) then
        work(pl, "wait", 6, {})
        return
      end
      work(pl, "call_mate", 0.05, { boat = boat.id })
      work(pl, "board", 0.08, { boat = boat.id, fx = od.mx, fy = od.my })
    end
    local gx, gy = trade.sea_gate(W, o, route.cells[#route.cells])
    pl[#pl + 1] = { op = "go", x = gx, y = gy, place = "the open sea toward home", water = true }
    work(pl, "voyage", route.len / c.sail_speed * (1 + 0.25 * (sea_wet(W, route, now) and 1 or 0)), { to = home_s.id, from = o.id,
      boat = boat.id, gate_c = route.cells[1], risk = sea_risk(W, route, boat, p, now) })
    return
  end
  if here == tr.from and tr.back and not tr.ashore then
    pl[#pl + 1] = { op = "go", x = hd.mx, y = hd.my, place = "the dock", water = true }
    work(pl, "land", 0.08, { boat = boat.id })
    return
  end
  -- home: what the boat carries, a load at a time, into the house (help asked for: into the
  -- village's granary, S16)
  local home = ctx.home or people.home(W, home_s, p)
  if tr.ask and home_s.granary then home = home_s.granary end
  if home then
    for _, g in ipairs(econ.ORDER) do
      local q = boat.cargo and boat.cargo[g] or 0
      local loads = 0
      while q > 1e-3 and loads < c.boat_hold * 2 do
        go(pl, hd.dx, hd.dy, "the dock")
        pl[#pl + 1] = { op = "pick", what = "cargo", good = g, boat = boat.id }
        go(pl, home.dx, home.dy, "home", home.id)
        pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
        q, loads = q - 1 / econ.GOODS[g].wt, loads + 1
      end
    end
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
  end
end

-- The weather on the sea way today and the risk of foundering (D-102): sim/agents/trade.lua (S18).
sea_wet = trade.sea_wet
sea_risk = trade.sea_risk

PLAN.trade = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local hh = ctx.hh
  local plan = (cd.b == 1 and hh.tplan) or (cd.b == 2 and s.tplan) or hh.splan
  local o = W.S.v3.settlements[cd.a]
  p.trip = { to = o.id, from = s.id, give = plan.give, take = plan.take, n = plan.n, aX = plan.aX, aH = plan.aH,
    pool = cd.b == 1 and "house" or (cd.b == 2 and "village" or plan.pool), t0 = now, ask = plan.ask, aw = plan.aw }
  hh.trading = p.id
  if cd.b == 3 then
    local boat = econ.own_boat(s, hh)
    p.trip.sea, p.trip.boat = true, boat.id
    boat.trip = p.id
    hh.splan = nil
    if plan.pool == "village" then s.trading = p.id end
    sea_legs(W, s, p, ctx, now)
    return "Sail " .. econ.GOODS[plan.give].name .. " to " .. o.name, o.name
  end
  if cd.b == 2 then s.trading = p.id; s.tplan = nil else hh.tplan = nil end
  trip_legs(W, s, p, ctx, now)
  if econ.GOODS[plan.give].beast then return "Drive " .. econ.amount(plan.n, plan.give) .. " to " .. o.name, o.name end
  return "Take " .. econ.GOODS[plan.give].name .. " to " .. o.name, o.name
end

-- (S16, R4) A courting trip: provisions, the road; at the other village to its square to meet its
-- people (WORK.court); home again by the gate - with a bride walking with him if a match was made.
local function court_legs(W, s, p, ctx, now)
  local c = C()
  local pl = plan_reset(p)
  local V = W.S.v3
  local tr = p.trip
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  if here == tr.from and not tr.back then
    local home = ctx.home
    if home and (p.prov or 0) < c.trade_prov then
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id }
    end
    if home and tr.visit and not tr.ask_kin and not tr.gift_packed and p.carry ~= "pack" and ctx.hh then
      -- (S17) a little of what the household has beyond its want, that the kin's village is heard to
      -- want: a gift for them
      tr.gift_packed = true
      local g, n = econ.visit_gift(W, home_s, ctx.hh, home, o, now)
      if g then
        tr.gift_good = g
        if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
        pl[#pl + 1] = { op = "pick", what = "pack", good = g, ref = home.id, n = n }
      end
    end
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    local _, hrs = trade.road(W, home_s, o)
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs, ctx.wake, ctx.bed), { to = o.id, from = home_s.id })
    return "Go courting at " .. o.name, o.name
  end
  if here == tr.to then
    local kin = tr.visit and V.people[tr.visit]
    local kh = kin and V.households[kin.hh]
    local kb = kh and o.buildings[kh.home or 0]
    if not tr.met and tr.claim then
      -- (S20, R6b) to the house of the man they know did it, else to the pen to look for their beasts
      local d = deeds.get(W, tr.claim)
      local e = d and deeds.known(home_s, d.id)
      local q = e and e.by ~= 0 and V.people[e.by]
      local qh = q and V.households[q.hh]
      local hb = qh and qh.sett == o.id and o.buildings[qh.home or 0]
      if not hb and d then
        local H = o.herds and o.herds[(d.what == "cattle" and "cattle") or (d.what == "pig" and "swine") or "flock"]
        hb = H and o.buildings[H.pen]
      end
      hb = hb or { dx = o.sqx or 0, dy = o.sqy or 0, id = 0, label = o.name .. "'s square" }
      go(pl, hb.dx, hb.dy, hb.label or "their house")
      work(pl, "claim", c.claim_h, { ref = hb.id })
      return "Ask at " .. o.name .. " for redress", o.name
    end
    if not tr.met then
      if tr.visit then
        -- (S16) to the kin's house: a meal and the news together (S17: or to ask for help)
        if kb then go(pl, kb.dx, kb.dy, kb.label or "their house", kb.id) end
        work(pl, tr.ask_kin and "ask_kin" or "visit_kin", tr.ask_kin and c.kin_ask_h or c.visit_h, { ref = kb and kb.id or 0, inside = kb and kb.id or nil })
        return (tr.ask_kin and "Ask " or "Visit ") .. (kin and kin.name or "kin") .. " at " .. o.name .. (tr.ask_kin and " for help" or ""), o.name
      end
      -- (S19) a hungry visitor is given a meal from the village's store first, as traders are
      if o.granary and o.granary.state == "standing" and p.hunger > 0.3 and not tr.fed then
        tr.fed = true
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "guest_meal", 0.4, { ref = o.granary.id })
      end
      local x, y = near_spot(W, p, now, o.sqx or 0, o.sqy or 0, 0.08, 61)
      go(pl, x, y, o.name .. "'s square")
      if tr.news then
        -- (S17) among its people at the square: how they fare, what they want, the news both ways
        work(pl, "news", c.news_h, { ref = o.id })
        return "Talk with the people of " .. o.name, o.name
      end
      work(pl, "court", c.court_h, { ref = o.id })
      return "Court at " .. o.name, o.name
    end
    if tr.ask_kin and not tr.packed and ((tr.gift_house or 0) > 0.05 or (tr.gift_left or 0) > 0.05) then
      -- (S17) what they give, into the pack: from their house, and from their village's granary
      tr.packed = true
      if kb and (tr.gift_house or 0) > 0.05 then
        if p.inside ~= kb.id then go(pl, kb.dx, kb.dy, kb.label or "their house", kb.id) end
        pl[#pl + 1] = { op = "pick", what = "kin_gift", ref = kb.id }
      end
      if o.granary and (tr.gift_left or 0) > 0.05 then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        pl[#pl + 1] = { op = "pick", what = "relief", ref = o.granary.id, n = tr.gift_left }
      end
      return "Take what " .. (kin and kin.name or "kin") .. " gives", o.name
    end
    local _, hrs0 = trade.road(W, o, home_s)
    -- (S19: a road longer than a day's walking is camped on anyway: they set off at once)
    local overday = hrs0 > ctx.sunset - ctx.wake
    if (tr.news or tr.claim) and not tr.slept and (ctx.night or (ctx.lh + hrs0 > ctx.sunset + 0.5 and not overday)) then
      -- (too late to walk home by day: the night at the square, off at first light; S19: a meal
      -- from the village's store before it)
      tr.slept = true
      local until_t = next_local(now, ctx.lh, ctx.wake)
      if o.granary and o.granary.state == "standing" and p.hunger > 0.2 then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "guest_meal", 0.4, { ref = o.granary.id })
      end
      go(pl, o.sqx or 0, o.sqy or 0, o.name .. "'s square")
      work(pl, "sleep", until_t - now, { trip_sleep = true })
      return "Sleep at " .. o.name, o.name
    end
    if tr.visit and kb and not tr.slept and (ctx.night or (ctx.lh + hrs0 > ctx.sunset + 0.5 and not overday)) then
      -- (too late to walk home by day: the night under their roof)
      tr.slept = true
      local until_t = next_local(now, ctx.lh, ctx.wake)
      if p.inside ~= kb.id then go(pl, kb.dx, kb.dy, kb.label or "their house", kb.id) end
      work(pl, "sleep", until_t - now, { inside = kb.id, trip_sleep = true })
      return "Stay the night at " .. (kin and kin.name or "kin") .. "'s", o.name
    end
    local gx, gy = trade.gate(W, o, home_s)
    go(pl, gx, gy, "the road home")
    -- (S20, R6b) beasts given back are led home
    local lead = tr.lead_kind and #(stock.led_by(o, p, tr.lead_kind)) > 0 and tr.lead_kind or nil
    if lead then pl[#pl].lead = lead end
    if tr.depart then work(pl, "gather", 0, { until_t = tr.depart }) end
    local _, hrs = trade.road(W, o, home_s)
    local lh0 = (ctx.lh + max(0, (tr.depart or now) - now)) % 24
    work(pl, "travel", road_dur(lh0 + 0.4, hrs * (lead and c.drove_road or 1), ctx.wake, ctx.bed), { to = home_s.id, from = o.id, lead = lead })
    return "Home from " .. o.name, home_s.name
  end
  local home = ctx.home or people.home(W, home_s, p)
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
  end
  return "Home", home_s.name
end
M.court_legs = court_legs

-- (S19) A fair (sim/agents/fair.lua): provisions and the household's goods packed, the road there,
-- the fair's hours at the host's square (a meal from its store when hungry, the night at the fair
-- ground), the road home, the pack into the house. Host households only walk to their square.
local function fair_legs(W, s, p, ctx, now)
  local c = C()
  local pl = p.plan
  local V = W.S.v3
  local tr = p.trip
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local f = o and o.fair
  local here = p.at or p.sett
  local hh = V.households[p.hh]
  local local_fair = o == home_s
  if here == tr.from and not tr.back and not tr.packed then
    tr.packed = true
    local home = ctx.home
    if home then
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      -- (bread for the road there and back and the days of the fair: S19, a woman walking home from
      -- a fair a day and a half away with nothing left in her bag starved on the road)
      local hrs = 0
      if not local_fair then local _; _, hrs = trade.road(W, home_s, o) end
      local need = (2 * hrs + (f and max(0, f.t1 - now) or c.fair_h) * 0.5) * c.hunger_idle / c.meal * c.meal_fu
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id, n = max(c.trade_prov, need) }
      local fd = hh and hh.fair
      if fd and fd.take and not fd.packed then
        -- (the household's goods for the fair: the first of it to go takes them)
        fd.packed = true
        for _, it in ipairs(fd.take) do pl[#pl + 1] = { op = "pick", what = "pack", good = it[1], ref = home.id, n = it[2] } end
      end
      pl[#pl + 1] = { op = "pick", what = "pack", good = "grain", ref = home.id, n = 0 }
    end
  end
  if here == tr.from and not tr.back and not local_fair then
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    local _, hrs = trade.road(W, home_s, o)
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs, ctx.wake, ctx.bed), { to = o.id, from = home_s.id })
    return "Go to the fair at " .. o.name, o.name
  end
  if here == tr.to and not tr.back and f and now < f.t1 then
    -- (hungry with nothing left in the bag: a meal at home, or from the host's store)
    if p.hunger > 0.5 and (p.prov or 0) < C().meal_fu then
      local home = ctx.home
      if local_fair and home then
        if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
        work(pl, "eat", 0.3, { inside = home.id, ref = home.id, from = (home.stock.meals or 0) > 0 and nil or "cold" })
      elseif o.granary and o.granary.state == "standing" then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "guest_meal", 0.4, { ref = o.granary.id })
      end
    end
    if ctx.night then
      local until_t = next_local(now, ctx.lh, ctx.wake)
      local home = ctx.home
      if local_fair and home then
        go(pl, home.dx, home.dy, "home", home.id)
        work(pl, "sleep", until_t - now, { inside = home.id, trip_sleep = true })
      else
        local x, y = near_spot(W, p, now, (o.sqx or 0) + 0.1, (o.sqy or 0) + 0.06, 0.1, 63)
        go(pl, x, y, "the fair ground at " .. o.name)
        work(pl, "sleep", until_t - now, { trip_sleep = true })
      end
      return "Sleep at the fair", o.name
    end
    local present = false
    for _, id in ipairs(f.present) do if id == p.id then present = true end end
    if not present then f.present[#f.present + 1] = p.id end
    local x, y = near_spot(W, p, now, o.sqx or 0, o.sqy or 0, 0.13, 62 + p.id % 7)
    go(pl, x, y, "the fair at " .. o.name)
    work(pl, "fair", max(0.25, min(1.0, f.t1 - now)), { ref = o.id })
    return "At the fair at " .. o.name, o.name
  end
  if here == tr.to and not local_fair and here ~= p.sett then
    local _, hrs0 = trade.road(W, o, home_s)
    -- (the host sends its guests home fed, with bread for the way when theirs ran short)
    local g = o.granary and o.granary.state == "standing" and o.granary
    local need = hrs0 * c.hunger_idle / c.meal * c.meal_fu - (p.prov or 0)
    if g and not tr.fed_home and (p.hunger > 0.3 or need > 0) then
      tr.fed_home = true
      go(pl, g.dx, g.dy, o.name .. "'s granary")
      if p.hunger > 0.3 then work(pl, "guest_meal", 0.4, { ref = g.id }) end
      if need > 0 then pl[#pl + 1] = { op = "pick", what = "guest_bread", ref = g.id, n = need } end
    end
    local gx, gy = trade.gate(W, o, home_s)
    go(pl, gx, gy, "the road home")
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs0, ctx.wake, ctx.bed), { to = home_s.id, from = o.id })
    return "Home from the fair at " .. o.name, home_s.name
  end
  local home = ctx.home or people.home(W, home_s, p)
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
  else
    p.trip = nil
    work(pl, "wait", 0.1, {})
  end
  return "Home from the fair", home_s.name
end
M.fair_legs = fair_legs

PLAN.fair = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local o = W.S.v3.settlements[cd.a]
  local fd = ctx.hh.fair
  p.trip = { to = o.id, from = s.id, fair = o.id, give = "grain", take = "grain", n = 0, t0 = now }
  local _ = pl
  return fair_legs(W, s, p, ctx, now)
end

PLAN.cry = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  -- (one crier a neighbour)
  for i = #(s.fair_cry or {}), 1, -1 do if s.fair_cry[i] == o.id then table.remove(s.fair_cry, i) end end
  p.trip = { to = o.id, from = s.id, court = true, news = true, cry = s.id, give = "grain", take = "grain", n = 0, t0 = now }
  return court_legs(W, s, p, ctx, now)
end

PLAN.news = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  s.news_went = s.news_went or {}
  s.news_went[o.id] = floor(now / 24)
  p.trip = { to = o.id, from = s.id, court = true, news = true, give = "grain", take = "grain", n = 0, t0 = now }
  return court_legs(W, s, p, ctx, now)
end

PLAN.ask_kin = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  local hh = ctx.hh
  local q = W.S.v3.people[cd.b]
  hh.asking = p.id
  hh.kin_asked = hh.kin_asked or {}
  if q then hh.kin_asked[q.hh] = floor(now / 24) end
  p.trip = { to = o.id, from = s.id, court = true, visit = cd.b, ask_kin = true, give = "grain", take = "grain", n = 0, t0 = now }
  p.visit_t = now
  return court_legs(W, s, p, ctx, now)
end

PLAN.visit_kin = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  p.trip = { to = o.id, from = s.id, court = true, visit = cd.b, give = "grain", take = "grain", n = 0, t0 = now }
  p.visit_t = now
  return court_legs(W, s, p, ctx, now)
end

PLAN.court = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  p.trip = { to = o.id, from = s.id, court = true, give = "grain", take = "grain", n = 0, t0 = now }
  p.court = nil
  return court_legs(W, s, p, ctx, now)
end

-- (S20, R6a, D-149) A take trip: provisions from home and the road (by day) to the village whose
-- beasts are known; there, off the road near the edge of its map to lie up until its people sleep;
-- by night to the pen, a beast taken by the halter (WORK.steal); to the gate leading it (or empty-
-- handed if the dogs gave them away) and the road home by night; the beast into the household's
-- herd at home (act.lua, WORK.travel).
local function steal_legs(W, s, p, ctx, now)
  local c = C()
  local pl = plan_reset(p)
  local V = W.S.v3
  local tr = p.trip
  local home_s = V.settlements[tr.from]
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  if not o or not home_s then p.trip = nil; work(pl, "wait", 0.1, {}); return "Home", "" end
  if here == tr.from and not tr.back then
    local home = ctx.home
    if home and (p.prov or 0) < c.trade_prov then
      if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
      pl[#pl + 1] = { op = "pick", what = "provisions", ref = home.id }
    end
    local gx, gy = trade.gate(W, home_s, o)
    go(pl, gx, gy, "the road to " .. o.name)
    local _, hrs = trade.road(W, home_s, o)
    work(pl, "travel", road_dur(ctx.lh + 0.4, hrs, ctx.wake, ctx.bed), { to = o.id, from = home_s.id })
    return "Go to " .. o.name .. " to take a beast", o.name
  end
  if here == tr.to then
    local H = o.herds and o.herds[tr.kind]
    local pen = H and o.buildings[H.pen]
    local led = H and #(stock.led_by(o, p, tr.kind)) > 0
    if not led and not tr.took and not tr.fled and pen then
      -- (the dark: past the latest bedtime of its people)
      local dark = (min(c.bed_max, (o.sunset or 18) + c.bed_after_sunset + 0.75) + c.take_dark) % 24
      local wait = (dark - ctx.lh) % 24
      local dawn = (ctx.wake - 1 - ctx.lh) % 24
      if wait > 0.05 and wait < dawn then
        if not tr.hx then
          -- off the road among the trees, a little in from the edge where the road comes in
          local gx, gy = trade.gate(W, o, home_s)
          local l = sqrt(gx * gx + gy * gy)
          local k = l > 1e-6 and (l - c.take_hide) / l or 1
          tr.hx, tr.hy = near_spot(W, p, now, gx * k, gy * k, 0.08, 83)
        end
        go(pl, tr.hx, tr.hy, "the woods above " .. o.name)
        work(pl, "lie_up", wait, {})
        return "Wait for dark near " .. o.name, o.name
      end
      if wait >= dawn then
        -- (past first light: they missed the night; another night, or home)
        tr.nights = (tr.nights or 0) + 1
        if tr.nights > 2 then tr.fled = true end
      end
      if not tr.fled then
        go(pl, pen.dx, pen.dy, "the " .. pen.label)
        work(pl, "steal", c.take_h, { herd = stock.HIDX[tr.kind] + 1, ref = pen.id })
        return "Take a beast from " .. o.name .. "'s " .. pen.label, o.name
      end
    end
    -- away: to the gate leading it (or empty-handed), and the road home through the night
    local gx, gy = trade.gate(W, o, home_s)
    go(pl, gx, gy, "the road home")
    if led then pl[#pl].lead = tr.kind end
    local _, hrs = trade.road(W, o, home_s)
    work(pl, "travel", hrs * (led and c.drove_road or 1), { to = home_s.id, from = o.id, lead = led and tr.kind or nil })
    return led and ("Lead the beast home from " .. o.name) or ("Home from " .. o.name), home_s.name
  end
  local home = ctx.home or people.home(W, home_s, p)
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, trip_end = true }
  else
    p.trip = nil
    work(pl, "wait", 0.1, {})
  end
  return "Home", home_s.name
end
M.steal_legs = steal_legs

local function steal_why(W, p)
  local tr = p.trip
  local V = W.S.v3
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  local on = o and o.name or "the other village"
  if here == tr.from then
    if tr.back then return tr.took and not tr.fled and "Home with the beast" or "Home again" end
    return tr.why or ("Going to take a beast from " .. on)
  end
  if here == tr.to then
    if tr.fled then return sfmt("Away from %s: the dogs gave me away, I let the beast go", on) end
    if tr.took then return sfmt("Leading home a beast taken from %s's %s in the night", on, tr.kind == "cattle" and "byre" or "fold") end
    return sfmt("Waiting near %s for its people to sleep, to take one of their beasts", on)
  end
  return tr.why or ""
end

PLAN.steal = function(W, s, p, ctx, now, cd)
  local o = W.S.v3.settlements[cd.a]
  local kind = stock.HERDS[cd.b]
  local b = conflict.take_weigh(W, s, p, ctx.hh, now)
  ctx.hh.taking = p.id
  p.trip = { steal = true, to = o.id, from = s.id, kind = kind, lead_kind = kind, give = "grain", take = "grain", n = b and b.n or 1, t0 = now,
    why = b and b.why or nil, gdeed = b and b.gdeed or nil }
  return steal_legs(W, s, p, ctx, now)
end

PLAN.claim = function(W, s, p, ctx, now, cd)
  local d = deeds.get(W, cd.a)
  local o = W.S.v3.settlements[cd.b]
  local e = d and deeds.known(s, d.id)
  if e then e.claim_t = now end
  local why
  if d and e then
    why = sfmt("%s took %s of ours: going to %s to ask for %s back", deeds.told(W, s, d, e),
      d.n > 1 and sfmt("%d %s", d.n, conflict.NOUN[d.what] or d.what) or (conflict.ONE[d.what] or d.what), o.name, d.n > 1 and "them" or "it")
  end
  p.trip = { court = true, claim = cd.a, to = o.id, from = s.id, give = "grain", take = "grain", n = 0, t0 = now, why = why }
  return court_legs(W, s, p, ctx, now)
end

-- (S20, R6b) Why a claimant is on the road or at the other village.
function M.claim_why(W, p)
  local tr = p.trip
  local V = W.S.v3
  local o = V.settlements[tr.to]
  local here = p.at or p.sett
  local on = o and o.name or "the other village"
  if here == tr.to then
    if not tr.met then return sfmt("At %s to ask for redress: %s", on, tr.why or "") end
    local a = tr.answer
    if a == "returned" then return sfmt("%s gave back our beasts: leading them home", on) end
    if a == "paid" then return sfmt("%s made it good: carrying home what they gave", on) end
    if a == "refused" then return sfmt("%s refused us: going home", on) end
    return sfmt("Nobody at %s would own to it: going home", on)
  end
  if here == tr.from and tr.back then return "Home from " .. on end
  return tr.why or ("On the road to " .. on .. " to ask for redress")
end

-- (S20, R6a) A take trip's decision (outside decide: Lua's limit on upvalues): the alarm at the
-- other village (let go and run, or hold on), then the legs. True when it planned the step.
function M.steal_decide(W, s, p, ctx, now, reason)
  local tr = p.trip
  local here = p.at or p.sett
  if here == tr.from and not tr.back then
    tr.replans = (tr.replans or 0) + 1
    if tr.replans > 30 then
      -- (it would not come right: given up)
      p.trip = nil
      if ctx.hh and ctx.hh.taking == p.id then ctx.hh.taking = 0 end
    end
  end
  if p.trip and here == tr.to and p.alarm and danger.fresh(p, now) and not tr.fled then
    -- the dogs bark, someone shouts or comes: let the beast go and run - unless bold enough and
    -- nobody is near yet
    local o = W.S.v3.settlements[tr.to]
    local near = conflict.watchers_near(W, o, p, now, C().take_sight * 1.5)
    if near > 0 or people.bold(W, p) < C().take_hold then
      conflict.let_go(W, o, p, now)
      tr.fled = true
      p.tried = p.tried or {}
      local tt = p.tried[o.id] or { n = 0, t = now }
      tt.n, tt.t = tt.n + 1, floor(now)
      p.tried[o.id] = tt
      -- (what he learned there goes home with him: their dogs, and that they are awake to it now)
      local nd = 0
      for _, dg in ipairs(o.dogs or {}) do if dg.alive then nd = nd + 1 end end
      tr.saw_dogs = nd
      people.remember(p, now, sfmt("Given away at %s (%s): I let the beast go and ran", o.name,
          (p.alarm.kind == "caught" and "they caught me") or (p.alarm.how == "saw" and "they saw me coming") or "the dogs barked"))
    end
    p.alarm = nil
  end
  if p.trip then
    local obj, dest = steal_legs(W, s, p, ctx, now)
    p.obj, p.dest, p.objk = obj, dest, "steal"
    if p.trip then p.why = (reason and (reason .. ". ") or "") .. steal_why(W, p) end
    p.tgt_kind, p.tgt_ref = "trade", p.trip and p.trip.to or 0
    return true
  end
  return false
end

PLAN.share = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local hh = ctx.hh
  local g = hh.gift
  local o = W.S.v3.households[cd.a]
  local oh = s.buildings[o.home]
  hh.sharing = p.id
  hh.gift = nil
  local home = ctx.home
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  pl[#pl + 1] = { op = "pick", what = "pack", good = g.g, ref = home.id, n = g.q }
  local q1 = W.S.v3.people[o.members[1] or 0]
  go(pl, oh.dx, oh.dy, "the " .. (q1 and q1.surname or "neighbour") .. " house")
  pl[#pl + 1] = { op = "drop", to = "home", ref = oh.id, gift = hh.id, release = "sharing" }
  return "Take " .. econ.GOODS[g.g].name .. " to a neighbour", oh.label or "a house"
end

PLAN.tan = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local pit = s.buildings[cd.a]
  local home = ctx.home
  ctx.hh.tanning = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  pl[#pl + 1] = { op = "pick", what = "hides", ref = home.id, n = 6 }
  go(pl, pit.dx, pit.dy, "the tanning pit")
  work(pl, "tan", c.tan_h, { ref = pit.id, fx = pit.cx, fy = pit.cy })
  return "Hides to the tanning pit", "the tanning pit"
end

-- (S18) Salt at the pans: logs for the fire (from the woodstore, else the household's woodpile) when
-- the pans' pile is low, hours of boiling and raking, the salt carried home.
PLAN.salt = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local sp = s.buildings[cd.a]
  local home = ctx.home
  ctx.hh.salting_pans = p.id
  if (sp.stock.logs or 0) < 1.5 then
    local w = (s.wood_store and (s.wood_store.stock.logs or 0) >= 2) and s.wood_store or home
    go(pl, w.dx, w.dy, w == home and "our woodpile" or "the woodstore")
    pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = w.id, n = 2 }
    go(pl, sp.dx, sp.dy, "the salt pans")
    pl[#pl + 1] = { op = "drop", to = "saltpan", ref = sp.id }
  end
  go(pl, sp.dx, sp.dy, "the salt pans")
  work(pl, "salt", (1.8 + 0.8 * u01(W, p, now, 61)) * p.dil, { ref = sp.id, fx = sp.cx, fy = sp.cy })
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "salting_pans" }
  return "Boil salt at the pans", "the salt pans"
end

PLAN.lift = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local pit = s.buildings[cd.a]
  local home = ctx.home
  ctx.hh.lifting = p.id
  go(pl, pit.dx, pit.dy, "the tanning pit")
  work(pl, "lift", c.tan_h, { ref = pit.id, fx = pit.cx, fy = pit.cy })
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "lifting" }
  return "Lift the leather out", "the tanning pit"
end

PLAN.harness = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local home = ctx.home
  ctx.hh.cobbling = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "harness", c.harness_h, { ref = home.id, inside = home.id })
  return "Make a harness for the ox", "home"
end

PLAN.cobble = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local home = ctx.home
  ctx.hh.cobbling = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "cobble", c.cobble_h, { ref = home.id, who = cd.b, inside = home.id })
  return "Make shoes", "home"
end

PLAN.drive_off = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local a = s.alarm
  p.drove_t = now
  go(pl, a.x, a.y, "the pen")
  work(pl, "drive_off", 0.4, { fx = a.x, fy = a.y })
  return "Drive the wolves off", "the pen"
end

-- (S20, R6a) A grown-up of the village, woken or seen to a stranger at the beasts, goes after him
-- while the alarm is fresh and he is still about.
function M.chase_ok(W, s, p, ctx, now)
  if p.at or ctx.stage ~= "adult" or ctx.babe or not danger.fresh(p, now, C().chase_h) then return false end
  if not conflict.goes_out(W, s, p, now) then p.alarm = nil; return false end
  return conflict.chase_target(W, s, p, now) ~= nil
end

-- (S20, R6a) After the stranger at the beasts: to where he is now; the step's end (WORK.chase) sees
-- whether he is caught, still in sight, or gone.
PLAN.chase = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local q = W.S.v3.people[cd.a]
  local x, y = p.alarm.x, p.alarm.y
  if q and q.alive and q.at == s.id then x, y = act.pos(q, now) end
  go(pl, x, y, "the stranger")
  work(pl, "chase", 0.01, { ref = cd.a, fx = x, fy = y })
  return "Go after the stranger at the beasts", "the stranger"
end

PLAN.trip = function(W, s, p, ctx, now, cd)
  plan_reset(p)
  if p.trip.fair then return M.fair_legs(W, s, p, ctx, now) end
  if p.trip.court then return court_legs(W, s, p, ctx, now) end
  if p.trip.sea then sea_legs(W, s, p, ctx, now) else trip_legs(W, s, p, ctx, now) end
  local o = W.S.v3.settlements[p.trip.to]
  return (p.at or p.sett) == p.trip.to and ("Trading at " .. o.name) or ("On the way " .. ((p.at or p.sett) == p.sett and "to " .. o.name or "home")), o.name
end

-- The mate's day (D-096): to the dock, into the skipper's boat, riding out and back with them
-- (the skipper's landing ends it).
PLAN.crew = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.dock
  local boat = s.boats[cd.a]
  boat.mate = p.id
  go(pl, b.dx, b.dy, "the dock")
  work(pl, "board_mate", 0.06, { boat = boat.id, skipper = cd.b, fx = b.mx, fy = b.my })
  work(pl, "ride", 8, { boat = boat.id })
  return "Crew the boat", "the fishing ground"
end

PLAN.ride = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  if p.trip and (p.trip.migrate or p.trip.found) then
    -- (S18) a passenger: the household moving over the sea, sitting it out until the landing
    work(pl, "passage", 8, { boat = p.ride_boat })
    return "Cross the sea with the household", "the sea"
  end
  work(pl, "ride", 8, { boat = p.ride_boat })
  return "Out in the boat", "the fishing ground"
end

-- Fish to the rack, and the dried fish home (D-096).
PLAN.dry = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local rk = s.buildings[cd.a]
  local home = ctx.home
  ctx.hh.smoking = p.id
  local amt = max(1, (home.stock.fish or 0) - #ctx.hh.members * 0.8)
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  pl[#pl + 1] = { op = "pick", what = "fish", ref = home.id, n = amt }
  go(pl, rk.dx, rk.dy, "the drying rack")
  work(pl, "hang", 0.25 + 0.05 * amt, { ref = rk.id, fx = rk.cx, fy = rk.cy, release = "smoking" })
  return "Hang the fish to dry", "the drying rack"
end

PLAN.fetch_dried = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local rk = s.buildings[cd.a]
  local home = ctx.home
  ctx.hh.fetch_food = p.id
  go(pl, rk.dx, rk.dy, "the drying rack")
  pl[#pl + 1] = { op = "pick", what = "dried", ref = rk.id }
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_food" }
  return "Fetch the dried fish", "the drying rack"
end

-- Mending the household's boat (D-087): a log from the woodstore unless one waits at the dock.
PLAN.mend_boat = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.dock
  local boat = s.boats[cd.a]
  boat.mender = p.id
  if (boat.repair_logs or 0) < 1 then
    local w = s.wood_store
    go(pl, w.dx, w.dy, "the woodstore")
    pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = w.id, n = 1 }
    go(pl, b.dx, b.dy, "the dock")
    pl[#pl + 1] = { op = "drop", to = "boat", boat = boat.id, ref = b.id }
  else
    go(pl, b.dx, b.dy, "the dock")
  end
  local left = (1 - (boat.mend_p or 0)) * c.mend_boat_h
  work(pl, "mend_boat", min(left, max(0.4, (0.8 + 1.0 * u01(W, p, now, 54)) * p.dil)), { ref = boat.id, fx = boat.x, fy = boat.y })
  return "Mend the boat", "the dock"
end

-- Smoking fish at the hearth (D-087).
PLAN.smoke = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  ctx.hh.smoking = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  local amt = max(1, (home.stock.fish or 0) + (home.stock.meat or 0) - #ctx.hh.members * 0.8)
  -- (S18: salted faster than smoked)
  local salted = min(amt, (home.stock.salt or 0) / c.salt_cure)
  local hrs = salted / c.salt_cure_h + (amt - salted) / c.smoke_fu_h
  work(pl, "smoke", min(2.0, max(0.5, hrs)), { inside = home.id, ref = home.id })
  return salted > 0.5 and "Salt the meat and fish" or "Smoke the fish", "home"
end

-- Textiles (D-088): where the work is done. Spinning goes where the people are (the well, among
-- the talkers, on a fair evening or when a few are there; a bench by the door in fair weather;
-- inside by the hearth); the loom and the needle are indoors; flax is broken by the door.
local function by_door(home, p, k)
  local ox, oy = home.dx - home.cx, home.dy - home.cy
  local l = sqrt(ox * ox + oy * oy)
  if l < 1e-6 then ox, oy, l = 1, 0, 1 end
  local side = (p.id % 2 == 0) and 1 or -1
  return home.dx + (-oy / l) * 0.022 * side + ox / l * k, home.dy + (ox / l) * 0.022 * side + oy / l * k, ox / l, oy / l
end

PLAN.spin = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  local dur = (0.8 + 1.0 * u01(W, p, now, 55)) * p.dil
  local fair = ctx.rain == 0 and (s.temp_now or 15) >= 9 and not ctx.night
  local company = fair and ((s.at_well or 0) >= 2 or (ctx.lh > ctx.sunset - 2 and p.soc > 0.45))
  if company then
    local x, y = near_spot(W, p, now, s.sqx, s.sqy, 0.09, 56)
    go(pl, x, y, "the well")
    work(pl, "spin", dur, { ref = home.id, fx = s.sqx, fy = s.sqy })
    return "Spin with the neighbours", "the well"
  end
  if fair then
    local x, y, ox, oy = by_door(home, p, 0.01)
    if dist(p.x, p.y, x, y) > 0.01 then go(pl, x, y, "home") end
    work(pl, "spin", dur, { ref = home.id, fx = x + ox, fy = y + oy })
    return "Spin by the door", "home"
  end
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "spin", dur, { ref = home.id, inside = home.id })
  return "Spin by the hearth", "home"
end

PLAN.weave = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  ctx.hh.weaving = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "weave", (1.0 + 1.2 * u01(W, p, now, 57)) * p.dil, { ref = home.id, inside = home.id })
  return "Weave at the loom", "home"
end

PLAN.sew = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  ctx.hh.sewing = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "sew", (0.9 + 1.0 * u01(W, p, now, 58)) * p.dil, { ref = home.id, inside = home.id, who = cd.b })
  local q = W.S.v3.people[cd.b]
  return q == p and "Sew new clothes" or ("Sew clothes for " .. (q and q.name or "the household")), "home"
end

PLAN.mend_clothes = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "mend_clothes", c.mend_clothes_h * (0.8 + 0.4 * u01(W, p, now, 59)), { ref = home.id, inside = home.id })
  return "Patch clothes", "home"
end

PLAN.dress = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = s.buildings[cd.a]
  ctx.hh.dressing = p.id
  if (home.stock.flax or 0) + (home.stock.wool or 0) < 0.3 then
    local g = s.granary
    go(pl, g.dx, g.dy, "the granary")
    pl[#pl + 1] = { op = "pick", what = "flax", from = "store", ref = g.id, n = 2, wool = true }
    go(pl, home.dx, home.dy, "home")
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  end
  local dur = (0.9 + 1.0 * u01(W, p, now, 60)) * p.dil
  if ctx.rain == 0 then
    local x, y, ox, oy = by_door(home, p, 0.02)
    go(pl, x, y, "home")
    work(pl, "dress", dur, { ref = home.id, fx = x + ox, fy = y + oy })
    return "Break and hackle flax by the door", "home"
  end
  go(pl, home.dx, home.dy, "home", home.id)
  work(pl, "dress", dur, { ref = home.id, inside = home.id })
  return "Hackle flax indoors", "home"
end

PLAN.haul_flax = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local plot = s.plots[cd.a]
  plot.flax_claim = (plot.flax_claim or 0) + 1
  local g = s.granary
  go(pl, plot.stx, plot.sty, plot.name)
  pl[#pl + 1] = { op = "pick", what = "flax", from = "plot", ref = cd.a }
  go(pl, g.dx, g.dy, "the granary")
  pl[#pl + 1] = { op = "drop", to = "granary", ref = g.id }
  return "Carry the flax to the granary", plot.name
end

-- The hunt (D-094): to stalking distance of the quarry, then creeping to a spear's throw.
PLAN.hunt = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local w = wild.byk(s, cd.a)
  if not w or not w.alive then return PLAN.rest(W, s, p, ctx, now, cd) end
  if not p.hunting then
    p.hunting = { k = w.k, sp = w.sp, t0 = now, tries = 0 }
    ctx.hh.hunting = p.id
  end
  p.hunting.k = w.k
  w.hunter = p.id
  local qx, qy = wild.pos(w, now)
  local d = dist(p.x, p.y, qx, qy)
  if d > c.hunt_stalk then
    -- (track it: to stalking distance, from the side away from the village)
    local ax, ay = qx - p.x, qy - p.y
    local l = max(1e-6, sqrt(ax * ax + ay * ay))
    local sx, sy = qx - ax / l * c.hunt_stalk * 0.8, qy - ay / l * c.hunt_stalk * 0.8
    go(pl, sx, sy, "the woods' edge")
  end
  -- creep to a throw
  local sx, sy = d > c.hunt_stalk and (qx - (qx - p.x) / max(d, 1e-6) * c.hunt_stalk * 0.8) or p.x,
    d > c.hunt_stalk and (qy - (qy - p.y) / max(d, 1e-6) * c.hunt_stalk * 0.8) or p.y
  local ex, ey = qx - sx, qy - sy
  local l = max(1e-6, sqrt(ex * ex + ey * ey))
  local tx, ty = qx - ex / l * c.hunt_throw * 0.8, qy - ey / l * c.hunt_throw * 0.8
  work(pl, "stalk", max(0.05, (l - c.hunt_throw * 0.8) / c.stalk_speed), { ref = w.k, x1 = tx, y1 = ty, fx = qx, fy = qy })
  return "Hunt a " .. w.sp, "the woods' edge"
end

-- The kill carried home whole (meat and hide).
PLAN.fetch_game = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H = p.hunting
  local home = ctx.home
  local cc = H and H.carcass
  if not cc or not home then p.hunting = nil; return PLAN.rest(W, s, p, ctx, now, cd) end
  go(pl, cc.x + 0.01, cc.y, "the kill")
  work(pl, "gut", 0.3 + 0.2 * u01(W, p, now, 73), { fx = cc.x, fy = cc.y })
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "hunting" }
  return "Carry the " .. H.sp .. " home", "home"
end

-- The night watch (D-093): the pen p's household keeps beasts in, if wolves took one lately and
-- nobody else watches it; nil otherwise.
function M.guard_herd(W, s, p, ctx, now)
  local c = C()
  -- (S20) wolves took a beast, or a beast was taken in the night (R6a)
  local last = max(s.wolf_alarm or -1e9, s.thief_alarm or -1e9)
  if not s.herds or now - last > c.guard_days * 24 then return nil end
  if ctx.stage ~= "adult" or not ctx.hh or p.energy < 0.2 or ctx.babe then return nil end
  s.guards = s.guards or {}
  for hi = 1, #stock.HERDS do
    local H = s.herds[stock.HERDS[hi]]
    if H and herd_sum(H, now).owners[p.hh] then
      local g = W.S.v3.people[s.guards[hi] or 0]
      local free = not (g and g.alive and g.objk == "guard" and g ~= p)
      -- (the men first: a woman watches where the household has no man of age)
      if free and (p.sex == "m" or not kin.has_man(W, ctx.hh, now)) then return hi end
    end
  end
  return nil
end

PLAN.guard = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H = s.herds[stock.HERDS[cd.b]]
  local pen = s.buildings[H.pen]
  s.guards = s.guards or {}
  s.guards[cd.b] = p.id
  local until_t = next_local(now, ctx.lh, ctx.wake)
  local x, y = near_spot(W, p, now, pen.dx, pen.dy, 0.02, 72)
  go(pl, x, y, "the " .. pen.label)
  work(pl, "sleep", until_t - now - 0.2, { guard = cd.b })
  return "Keep watch at the " .. pen.label, "the " .. pen.label
end

-- Livestock (D-089, D-092).
local function herd_of(s, cd)
  local kind = stock.HERDS[cd and cd.b or 1] or "flock"
  return s.herds and s.herds[kind], kind
end

PLAN.herd = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H, kind = herd_of(s, cd)
  p.herd_of = cd.b
  local pen = s.buildings[H.pen]
  local x, y = stock.pick_spot(W, s, H, now)
  if not x then x, y = pen.dx, pen.dy end
  if cd.a == 0 then
    -- open the pen and drive them out
    go(pl, pen.dx, pen.dy, "the " .. pen.label)
    work(pl, "open_pen", 0.06, { sx = x, sy = y, fx = pen.cx, fy = pen.cy, herd = cd.b })
  end
  local wx, wy = near_spot(W, p, now, x, y, 0.04, 61)
  go(pl, wx, wy, kind == "swine" and "the woods' edge" or "the pasture")
  pl[#pl].drove = cd.b
  work(pl, "herd", (1.0 + 1.5 * u01(W, p, now, 62)) * p.dil, { fx = x, fy = y, herd = cd.b })
  return cd.a == 0 and ("Take " .. stock.NAMES[kind] .. " out") or ("Mind " .. stock.NAMES[kind]), kind == "swine" and "the woods' edge" or "the pasture"
end

PLAN.herd_home = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H, kind = herd_of(s, cd)
  p.herd_of = cd.b
  local pen = s.buildings[H.pen]
  go(pl, pen.dx, pen.dy, "the " .. pen.label)
  pl[#pl].drove = cd.b
  -- (the herd follows them in; they wait a little at the gate, then close it)
  work(pl, "herd", 0.3, { fx = pen.cx, fy = pen.cy, herd = cd.b })
  work(pl, "close_pen", 0.06, { fx = pen.cx, fy = pen.cy, herd = cd.b })
  return "Bring " .. stock.NAMES[kind] .. " home", "the " .. pen.label
end

-- Working on one animal (milking, shearing, killing): it stands while they work (stock.step).
local function at_animal(W, s, p, now, cd, kind, dur, extra)
  local pl = plan_reset(p)
  local H = herd_of(s, cd)
  local a = H.animals[cd.a]
  a.held = p.id
  p.tgt_herd = stock.HIDX[H.kind]
  local ax, ay = a.tx, a.ty
  local e = extra or {}
  e.ref, e.herd, e.fx, e.fy = cd.a, cd.b, ax, ay
  go(pl, ax + 0.012, ay, "the " .. (H.out and "pasture" or s.buildings[H.pen].label))
  work(pl, kind, dur, e)
  return pl, a
end

PLAN.milk = function(W, s, p, ctx, now, cd)
  local S = stock.SP(herd_of(s, cd).animals[cd.a].sp)
  ctx.hh.milking = p.id
  local pl, a = at_animal(W, s, p, now, cd, "milk", S.milk_h * (0.8 + 0.4 * u01(W, p, now, 68)))
  local home = ctx.home
  if home then
    go(pl, home.dx, home.dy, "home", home.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "milking" }
  end
  return a.sp == "cattle" and "Milk the cow" or "Milk the goat", "the " .. (herd_of(s, cd).out and "pasture" or "pen")
end

PLAN.shear = function(W, s, p, ctx, now, cd)
  local pl = at_animal(W, s, p, now, cd, "shear", 0.3 + 0.2 * u01(W, p, now, 63))
  local home = ctx.home
  go(pl, home.dx, home.dy, "home")
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Shear a sheep", "the fold"
end

PLAN.mow = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local P = s.pasture
  local fold = s.buildings[(s.herds.flock or s.herds.cattle).pen]
  ctx.hh.mowing = p.id
  -- the best meadow block that is not where a herd grazes today
  local N, BS = s.N, stock.BS
  local bn = N / BS
  local best, bs = 0, -1e9
  local busy = {}
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds[kind]
    if H and H.out then busy[stock.block_of(s, H.spot_x or 0, H.spot_y or 0)] = true end
  end
  for b = 1, bn * bn do
    local g = P.grass[b] or 0
    if not busy[b] and g > 0.4 then
      local x, y = stock.block_xy(s, b)
      local sc = g - dist(x, y, fold.cx, fold.cy) * 0.5 - dist(x, y, p.x, p.y) * 0.3
      if sc > bs then best, bs = b, sc end
    end
  end
  if best == 0 then return PLAN.rest(W, s, p, ctx, now, cd) end
  local by = floor((best - 1) / bn)
  local bx = (best - 1) - by * bn
  local x0, y0 = mapm_xy(s, bx * BS + 1.5, by * BS + 1.5 + floor(u01(W, p, now, 64) * 5))
  local x1, y1 = mapm_xy(s, bx * BS + BS - 1.5, by * BS + 1.5 + floor(u01(W, p, now, 64) * 5))
  go(pl, x0, y0, "the meadow")
  work(pl, "mow", (1.2 + 1.0 * u01(W, p, now, 65)) * p.dil, { block = best, x1 = x1, y1 = y1 })
  go(pl, fold.dx, fold.dy, "the haystack")
  pl[#pl + 1] = { op = "drop", to = "fold", ref = fold.id, release = "mowing" }
  return "Mow hay for the winter", "the meadow"
end

PLAN.slaughter = function(W, s, p, ctx, now, cd)
  local a0 = herd_of(s, cd).animals[cd.a]
  local S = stock.SP(a0.sp)
  -- (S19) a beast killed for a starving neighbour: its meat carried to their door
  local fh = a0.cull_for and W.S.v3.households[a0.cull_for]
  local fb = fh and #fh.members > 0 and s.buildings[fh.home or 0]
  local pl = at_animal(W, s, p, now, cd, "slaughter", S.butcher_h * (0.8 + 0.4 * u01(W, p, now, 67)))
  if fb then
    local q = W.S.v3.people[fh.members[1]]
    go(pl, fb.dx, fb.dy, "the " .. (q and q.surname or "neighbours") .. "' house", fb.id)
    pl[#pl + 1] = { op = "drop", to = "home", ref = fb.id, gift_to = fh.id }
    return "Kill a beast for the " .. (q and q.surname or "neighbour") .. "s, who have nothing", "the pen"
  end
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Kill a beast for the larder", "the pen"
end

PLAN.feed = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H = herd_of(s, cd)
  local pen = s.buildings[H.pen]
  ctx.hh.feeding = p.id
  local HS = herd_sum(H, now)
  local hay = s.buildings[(s.herds.flock or s.herds.cattle).pen]
  -- (hay from the stack by the fold into this herd's rack)
  if hay ~= pen then go(pl, hay.dx, hay.dy, "the haystack") end
  go(pl, pen.dx, pen.dy, "the " .. pen.label)
  work(pl, "feed", 0.2 + 0.1 * u01(W, p, now, 66), { amt = HS.se * 0.5, fx = pen.cx, fy = pen.cy, herd = cd.b })
  pl[#pl + 1] = { op = "drop", to = "fold", ref = pen.id, release = "feeding" }
  return "Hay for " .. stock.NAMES[H.kind], "the " .. pen.label
end

-- Dung from a pen to a field: a load picked up, carried, spread along a row.
PLAN.muck = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local H = herd_of(s, cd)
  local pen = s.buildings[H.pen]
  local plot = s.plots[cd.a]
  local c = C()
  H.muck_claim = p.id
  go(pl, pen.dx, pen.dy, "the " .. pen.label)
  pl[#pl + 1] = { op = "pick", what = "manure", ref = pen.id, herd = cd.b, n = c.manure_load }
  local r = 1 + floor(u01(W, p, now, 69) * plot.rows)
  local T = s.T
  local x0, y = plot.x0 + T * 0.5, plot.y0 + (r - 0.5) * T
  go(pl, x0, y, plot.name)
  work(pl, "spread", 0.35 + 0.2 * u01(W, p, now, 70), { ref = plot.id, x1 = plot.x1 - T * 0.5, y1 = y, herd = cd.b })
  return "Carry dung to the field", plot.name
end

-- The kitchen's scraps to the pigs' trough.
PLAN.slop = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  local H = s.herds.swine
  local sty = s.buildings[H.pen]
  ctx.hh.slopping = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  pl[#pl + 1] = { op = "pick", what = "scraps", ref = home.id }
  go(pl, sty.dx, sty.dy, "the sty")
  work(pl, "slop", 0.08, { fx = sty.cx, fy = sty.cy, release = "slopping" })
  return "Feed the pigs", "the sty"
end

-- Cheese at home from the milk that will not be drunk in time.
PLAN.cheese = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local home = ctx.home
  ctx.hh.cheesing = p.id
  if p.inside ~= home.id then go(pl, home.dx, home.dy, "home", home.id) end
  work(pl, "cheese", C().cheese_h * (0.8 + 0.4 * u01(W, p, now, 71)), { ref = home.id, inside = home.id })
  return "Make cheese", "home"
end

-- Afloat: whatever stopped them, they row back to the dock first (D-084).
PLAN.return_boat = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.dock
  pl[#pl + 1] = { op = "go", x = b.mx, y = b.my, place = "the dock", water = true }
  work(pl, "land", 0.08, { boat = p.boat })
  return "Row back to the dock", "the dock"
end

PLAN.repair = function(W, s, p, ctx, now, cd)
  local c = C()
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.repairer = p.id
  if (b.repair_logs or 0) < c.repair_logs then
    local w = s.wood_store
    go(pl, w.dx, w.dy, "the woodstore")
    pl[#pl + 1] = { op = "pick", what = "logs", from = "store", ref = w.id, n = c.repair_logs }
    go(pl, b.dx, b.dy, "the house")
    pl[#pl + 1] = { op = "drop", to = "repair", ref = b.id }
  end
  -- beside a wall, working on the roof and walls
  local T = s.T
  local side = floor(u01(W, p, now, 39) * 4)
  local along = (u01(W, p, now, 40) - 0.5) * 0.8
  local hw, hh2 = b.w * T * 0.5 + T * 0.7, b.h * T * 0.5 + T * 0.7
  local x, y
  if b.kind == "bridge" then
    -- (a bridge is built out from its banks: the near one, or the far one when it is reachable)
    local far = side >= 2 and b.ex
    x, y = (far and b.ex or b.dx) + along * T, (far and b.ey or b.dy) + along * T * 0.5
    go(pl, x, y, b.label)
    work(pl, "build", (0.8 + 0.9 * u01(W, p, now, 25)) * p.dil, { ref = b.id, fx = b.cx, fy = b.cy })
    return "Build the bridge", "the river"
  end
  if side == 0 then x, y = b.cx + along * b.w * T, b.cy - hh2
  elseif side == 1 then x, y = b.cx + along * b.w * T, b.cy + hh2
  elseif side == 2 then x, y = b.cx - hw, b.cy + along * b.h * T
  else x, y = b.cx + hw, b.cy + along * b.h * T end
  go(pl, x, y, "the house")
  local full = c.repair_h * (1.25 - 0.45 * p.sk_build)
  local left = (1 - (b.rep_p or 0)) * full
  work(pl, "repair", min(left, max(0.4, (1.0 + 1.2 * u01(W, p, now, 41)) * p.dil)), { ref = b.id, fx = b.cx, fy = b.cy, rate = 1 / full })
  local hh = W.S.v3.households[b.hh]
  local q = hh and W.S.v3.people[hh.members[1] or 0]
  return "Mend the " .. (q and q.surname or "empty") .. " house", "the house"
end

-- Infants are carried by (or kept indoors with) their carer.
local function think_infant(W, s, p, now)
  local V = W.S.v3
  local carer = V.people[p.carer or 0]
  if not carer or not carer.alive then carer = nil end
  if carer and carer.babe ~= p.id then carer.babe = p.id end
  local pl = plan_reset(p)
  p.obj = "Be looked after"
  p.why = carer and ((carer.id == p.mother and "In mother's arms: " or ("Minded by " .. carer.name .. ": ")) .. carer.name
    .. (p.hunger > C().infant_feed and " (hungry)" or "")) or "Nobody holds them"
  p.carrier = carer and carer.id or 0
  work(pl, "carried", 0.5)
end

-- Release claims held by p's unfinished plan (aborted or interrupted).
function M.release(W, s, p)
  local pl = p.plan
  for i = p.pi, #pl do
    local st = pl[i]
    if st then
      if st.op == "work" then
        if st.kind == "sow" or st.kind == "harvest" or st.kind == "till" then
          local plot = s.plots[st.ref]
          if plot and plot.row_claim[st.row] == p.id then plot.row_claim[st.row] = 0 end
        elseif st.kind == "unyoke" then
          -- (a stint at the plough cut short: the ox is out of the yoke where it stands, S14)
          local H = s.herds and s.herds.cattle
          local a = H and H.animals[st.ox or 0]
          if a and a.yoke == p.id then
            local now = W.S.v3.now or W.hour
            if a.act == "yoked" then a.tx, a.ty = stock.pos(a, now); a.x, a.y = a.tx, a.ty; a.act = "rest" end
            a.yoke = 0
            a.ver = (a.ver or 0) + 1
            if act.stock_push then act.stock_push(W, s, a) end
          end
          p.ox = nil
        elseif st.kind == "tend" then
          local plot = s.plots[st.ref]
          if plot then plot.tend_claim = max(0, (plot.tend_claim or 1) - 1) end
        elseif st.kind == "chop" or st.kind == "grub" then
          if s.trees.claim[st.ref] == p.id then s.trees.claim[st.ref] = 0; s.tv = (s.tv or 0) + 1 end
        elseif st.kind == "build" then
          local b = s.buildings[st.ref]
          if b then b.builders = max(0, (b.builders or 1) - 1) end
        elseif st.kind == "forage" then
          local hh = W.S.v3.households[p.hh]
          if hh and hh.foraging == p.id then hh.foraging = 0 end
        elseif st.kind == "shore" then
          local hh = W.S.v3.households[p.hh]
          if hh and hh.shore == p.id then hh.shore = 0 end
        elseif st.kind == "thresh" then
          local g = s.granary
          if g then g.threshers = max(0, (g.threshers or 1) - 1) end
        elseif st.kind == "repair" then
          local b = s.buildings[st.ref]
          if b and b.repairer == p.id then b.repairer = 0 end
        elseif st.kind == "cook" then
          local hh = W.S.v3.households[p.hh]
          if hh and hh.cooking == p.id then hh.cooking = 0 end
        elseif st.kind == "board" then
          local boat = s.boats and s.boats[st.boat]
          if boat and boat.crew == p.id and not p.boat then boat.crew = 0 end
        elseif st.kind == "mend_boat" then
          local boat = s.boats and s.boats[st.ref]
          if boat and boat.mender == p.id then boat.mender = 0 end
        elseif st.kind == "smoke" then
          local hh = W.S.v3.households[p.hh]
          if hh and hh.smoking == p.id then hh.smoking = 0 end
        elseif st.kind == "shear" or st.kind == "slaughter" or st.kind == "milk" then
          local H = s.herds and s.herds[stock.HERDS[st.herd or 1]]
          local a = H and H.animals[st.ref or 0]
          if a and a.held == p.id then a.held = 0 end
        elseif st.kind == "spread" then
          local H = s.herds and s.herds[stock.HERDS[st.herd or 1]]
          if H and H.muck_claim == p.id then H.muck_claim = 0 end
        elseif st.kind == "dress" or st.kind == "weave" or st.kind == "sew" then
          local hh = W.S.v3.households[p.hh]
          local field = st.kind == "dress" and "dressing" or (st.kind == "weave" and "weaving" or "sewing")
          if hh and hh[field] == p.id then hh[field] = 0 end
        elseif st.kind == "quarry" then
          local b = s.buildings[st.ref]
          if b then b.quarriers = max(0, (b.quarriers or 1) - 1) end
        elseif st.kind == "mine" then
          local b = s.buildings[st.ref]
          if b then b.miners = max(0, (b.miners or 1) - 1) end
        elseif st.kind == "fire_clamp" or st.kind == "open_clamp" then
          local b = s.buildings[st.ref]
          if b and b.firer == p.id then b.firer = 0 end
        elseif st.kind == "smelt" or st.kind == "smith" then
          local b = s.buildings[st.ref]
          if b and b.smith == p.id then b.smith = 0 end
        end
      elseif st.op == "pick" then
        if st.what == "sheaves" then
          local plot = s.plots[st.ref]
          if plot then plot.haul_claim = max(0, (plot.haul_claim or 1) - 1) end
        elseif st.what == "flax" and st.from == "plot" then
          local plot = s.plots[st.ref]
          if plot then plot.flax_claim = max(0, (plot.flax_claim or 1) - 1) end
        elseif st.from == "tree" then
          if s.trees.claim[st.ref] == -p.id then s.trees.claim[st.ref] = 0; s.tv = (s.tv or 0) + 1 end
        elseif st.what == "stone" then
          local b = s.buildings[st.ref]
          if b then b.haul_claim = max(0, (b.haul_claim or 1) - 1) end
        elseif st.what == "yard_stone" then
          local b = s.buildings[st.ref]
          if b then b.stone_claim = max(0, (b.stone_claim or 1) - 1) end
        elseif st.what == "ore" then
          local b = s.buildings[st.ref]
          if b then b.ore_claim = max(0, (b.ore_claim or 1) - 1) end
        elseif st.what == "charcoal" then
          local b = s.buildings[st.ref]
          if b then b.char_claim = max(0, (b.char_claim or 1) - 1) end
        end
      elseif st.op == "drop" then
        if st.to == "site" then
          local b = s.buildings[st.ref]
          if b then b.deliver_claim = max(0, (b.deliver_claim or 1) - 1) end
        elseif st.to == "stone_site" then
          local b = s.buildings[st.ref]
          if b then b.stone_claim = max(0, (b.stone_claim or 1) - 1) end
        elseif st.to == "clamp" then
          local b = s.buildings[st.ref]
          if b then b.charge_claim = max(0, (b.charge_claim or st.n or 1) - (st.n or 1)) end
        end
        if st.release then
          local hh = W.S.v3.households[p.hh]
          if hh and hh[st.release] == p.id then hh[st.release] = 0 end
        end
      end
    end
  end
end

local FIELD = { sow = true, till = true, tend = true, harvest = true }

-- Decide and start p's next plan at time now. `reason` (optional): why they stopped.
function M.decide(W, s, p, now, reason)
  NOW = now
  W_FIT = W
  GO_S, GO_P = s, p
  local ctx = context(W, s, p, now)
  if ctx.stage == "infant" then return think_infant(W, s, p, now) end
  do
    -- (S19) their household lives at another village now (it moved while they were away): they go
    -- after it, as a bride goes to her husband's
    local hh = ctx.hh
    if hh and hh.sett and hh.sett ~= p.sett and not p.trip and not p.move and (p.riding or 0) == 0 and W.S.v3.settlements[hh.sett] then
      local o = W.S.v3.settlements[hh.sett]
      p.move = { to = o.id, from = p.sett, depart = now + 0.5, follow = hh.id,
        why = sfmt("My household has gone to live at %s: going after them", o.name) }
    end
  end
  ncand = 0
  LZ.W, LZ.s, LZ.p, LZ.ctx = W, s, p, ctx
  if p.riding and p.riding ~= 0 then
    -- (a mate in a boat: out until the skipper lands, D-096)
    local obj, dest = PLAN.ride(W, s, p, ctx, now, nil)
    p.obj, p.dest, p.objk = obj, dest, "ride"
    p.why = "In the boat with " .. ((W.S.v3.people[p.riding] or {}).name or "the skipper")
    if p.trip and p.trip.migrate then
      -- (S18) the household moving over the sea
      p.objk = "migrate"
      p.why = require("sim.agents.migrate").why(W, p, W.S.v3.households[p.hh])
    elseif p.trip and p.trip.found then
      p.objk = "found"
      p.why = require("sim.agents.founding").why(W, p, W.S.v3.households[p.hh])
    end
    p.tgt_kind, p.tgt_ref = "ride", 0
    return true
  end
  if p.trip and p.trip.mate then
    -- (a mate on a trading voyage, at the other village: minding the boat at the dock, D-102)
    local tr = p.trip
    if (p.at or p.sett) == tr.from then
      p.trip = nil
    else
      local o = W.S.v3.settlements[p.at]
      local pl = plan_reset(p)
      if o.granary and p.hunger > 0.5 and (p.prov or 0) <= 0 then
        go(pl, o.granary.dx, o.granary.dy, o.name .. "'s granary")
        work(pl, "guest_meal", 0.4, { ref = o.granary.id })
      end
      go(pl, o.dock.dx + 0.02, o.dock.dy, o.name .. "'s dock")
      work(pl, "mind_boat", 3, {})
      p.obj, p.dest, p.objk = "Mind the boat", o.name .. "'s dock", "mind_boat"
      p.why = (reason and (reason .. ". ") or "") .. "Minding our boat at " .. o.name .. " while " .. ((W.S.v3.people[tr.skipper] or {}).name or "the skipper") .. " trades"
      p.tgt_kind, p.tgt_ref = "trade", tr.to
      return true
    end
  end
  do
    -- (S17, R5) the household is going to make a new place: everyone but the babes in arms packs,
    -- gathers at the gate and takes the road with the party
    local hh = W.S.v3.households[p.hh]
    local fd = hh and hh.found
    -- (life goes on until the morning they set off: then they pack, gather and go)
    if (fd and not fd.given_up and not p.trip and (p.at or p.sett) == fd.from and p.sett == fd.from and now >= fd.depart - C().ready_h) or (p.trip and p.trip.found) then
      if not p.trip then
        p.trip = { site = fd.site, from = fd.from, found = hh.id, give = "grain", take = "grain", n = 0, t0 = now }
      end
      local obj, dest = M.found_legs(W, s, p, ctx, now)
      p.obj, p.dest, p.objk = obj, dest, "found"
      p.why = (reason and (reason .. ". ") or "") .. require("sim.agents.founding").why(W, p, hh)
      p.tgt_kind, p.tgt_ref = "found", fd and fd.site or 0
      return true
    end
  end
  do
    -- (S16, R5) the household is moving away: everyone but the babes in arms (who are carried)
    -- packs, gathers at the gate and takes the road with the others
    local hh = W.S.v3.households[p.hh]
    local mg = hh and hh.migrate
    -- (S19) a ferry's skipper sails another household's move: theirs is the move he follows
    if p.ferry then
      local hf = W.S.v3.households[p.ferry]
      local mf = hf and hf.migrate
      if not mf or mf.given_up or mf.arrived or not mf.sea or mf.sea.skipper ~= p.id then
        p.ferry = nil
      elseif (not p.trip and now >= mf.depart - C().ready_h) or (p.trip and p.trip.ferry) then
        if not p.trip then p.trip = { to = mf.to, from = mf.from, migrate = hf.id, ferry = true, give = "grain", take = "grain", n = 0, t0 = now } end
        local obj, dest = migrate_legs(W, s, p, ctx, now)
        p.obj, p.dest, p.objk = obj, dest, "ferry"
        local q1 = W.S.v3.people[hf.members[1] or 0]
        local o = W.S.v3.settlements[mf.to]
        p.why = (reason and (reason .. ". ") or "") .. sfmt("Sailing the %ss over to %s in our boat (they have none), then home",
          q1 and q1.surname or "neighbour", o and o.name or "the other village")
        p.tgt_kind, p.tgt_ref = "trade", mf.to
        return true
      end
    end
    local mv = p.move
    if mv and not mv.arrived and not p.trip and now >= (mv.depart or 0) - C().ready_h then
      -- (S16, R4) a bride going to live at her husband's village (S19: making ready only shortly
      -- before the hour agreed - a match at a fair sets it days ahead)
      p.trip = { to = mv.to, from = mv.from, move = true, give = "grain", take = "grain", n = 0, t0 = now }
    end
    if (mg and not mg.arrived and not mg.given_up and ((not p.trip and now >= mg.depart - C().ready_h) or (p.trip and p.trip.migrate))) or (p.trip and (p.trip.migrate or p.trip.move)) then
      if not p.trip then
        p.trip = { to = mg.to, from = mg.from, migrate = hh.id, give = "grain", take = "grain", n = 0, t0 = now }
      end
      local obj, dest = migrate_legs(W, s, p, ctx, now)
      p.obj, p.dest, p.objk = obj, dest, "migrate"
      p.why = (reason and (reason .. ". ") or "") .. require("sim.agents.migrate").why(W, p, hh)
      p.tgt_kind, p.tgt_ref = "trade", p.trip and p.trip.to or 0
      return true
    end
  end
  if p.trip and p.trip.steal and M.steal_decide(W, s, p, ctx, now, reason) then return true end
  if p.trip then
    local tr = p.trip
    local here = p.at or p.sett
    -- (a trip that keeps being planned anew at home without ending is given up: a safety net)
    if here == p.sett then tr.replans = (tr.replans or 0) + 1 else tr.replans = 0 end
    local stuck = here == p.sett and tr.replans > 40
    if stuck then people.remember(p, now, "Gave up the trading trip: it would not come right") end
    local hungry_home = here == tr.from and p.hunger > 0.65 and p.carry ~= "pack"
    if not stuck and not hungry_home and (here ~= tr.from or p.carry == "pack" or (tr.sea and (tr.sailed or tr.back)) or (tr.sea and boat_cargo(W, p) > 0)) then
      local obj, dest = PLAN.trip(W, s, p, ctx, now, nil)
      p.obj, p.dest, p.objk = obj, dest, "trade"
      local nodeal = tr.dealt and not tr.ask and #(tr.deals or {}) == 0
      local giver = W.S.v3.settlements[tr.to].name
      if tr.fair then
        -- (S19) a fair
        local hh = W.S.v3.households[p.hh]
        local f = W.S.v3.settlements[tr.to].fair
        p.why = (reason and (reason .. ". ") or "") .. ((hh and hh.fair and hh.fair.why) or ("The fair at " .. giver))
          .. ((f and now < f.t0 and ": waiting for it to open") or (f and now < f.t1 and (here == tr.to) and ": bartering, hearing the news") or "")
        p.objk = "fair"
        p.tgt_kind, p.tgt_ref = "trade", tr.to
        return true
      end
      if tr.cry and here == tr.to and not tr.met then
        p.why = (reason and (reason .. ". ") or "") .. "Telling the people of " .. giver .. " of the fair at home"
      end
      if tr.court and tr.claim then
        p.why = (reason and (reason .. ". ") or "") .. M.claim_why(W, p)
        p.objk = "claim"
        p.tgt_kind, p.tgt_ref = "trade", tr.to
        return true
      end
      if tr.court and tr.visit then
        local kin = W.S.v3.people[tr.visit]
        local kn = kin and kin.name or "kin"
        if tr.ask_kin then
          p.why = (reason and (reason .. ". ") or "") .. (here == tr.to and (tr.met and ((tr.packed and "Carrying home what " .. kn .. " gave") or ("Asked " .. kn .. " at " .. giver .. " for help: going home"))
            or ("Asking " .. kn .. " at " .. giver .. " for help: little food at home")) or (here == tr.from and tr.back and ("Home from " .. giver .. " with what " .. kn .. " gave")
            or ("On the road to " .. giver .. " to ask " .. kn .. " for help: little food at home")))
        else
          p.why = (reason and (reason .. ". ") or "") .. (here == tr.to and (tr.met and ("Visited " .. kn .. " at " .. giver .. ": going home")
            or ("Visiting " .. kn .. " at " .. giver)) or (here == tr.from and tr.back and ("Home from visiting " .. kn) or ("On the road to " .. giver .. " to visit " .. kn)))
        end
        p.objk = "visit_kin"
        p.tgt_kind, p.tgt_ref = "trade", tr.to
        return true
      end
      if tr.court and tr.news then
        p.why = (reason and (reason .. ". ") or "") .. (here == tr.to and (tr.met and ("Heard how they fare at " .. giver .. ": going home") or ("Talking with the people of " .. giver .. ": how they fare, what they want"))
          or (here == tr.from and tr.back and ("Home from " .. giver .. " with the news") or ("On the road to " .. giver .. ": nothing heard from them for long")))
        p.objk = "news"
        p.tgt_kind, p.tgt_ref = "trade", tr.to
        return true
      end
      if tr.court then
        local bride = W.S.v3.people[tr.bride or 0]
        p.why = (reason and (reason .. ". ") or "") .. (here == tr.to and (tr.met and (bride and ("Married " .. bride.name .. " of " .. giver .. ": taking her home") or ("Nobody at " .. giver .. " to marry: going home"))
          or ("Nobody to marry at home: meeting the people of " .. giver))
          or (here == tr.from and tr.back and (bride and ("Home from " .. giver .. " with " .. bride.name) or ("Home from " .. giver)) or ("On the road to " .. giver .. ", courting")))
        p.objk = "court"
        p.tgt_kind, p.tgt_ref = "trade", tr.to
        return true
      end
      p.why = (reason and (reason .. ". ") or "") .. (tr.ask and (here == tr.to and ("Asking " .. giver .. " for food")
        or ((here == tr.from and tr.back) and ("Home with what " .. giver .. " gave: carrying it to the granary")
        or ((tr.sea and "At sea" or "On the road") .. ", going to ask " .. giver .. " for food")))
        or (nodeal and ("Nobody at " .. W.S.v3.settlements[tr.to].name .. " wanted our " .. econ.GOODS[tr.give].name .. ": taking it home"))
        or (here == tr.to and ("Trading " .. econ.GOODS[tr.give].name .. " for " .. econ.GOODS[tr.take].name) or (tr.sea and "At sea" or "On the road")))
      p.tgt_kind, p.tgt_ref = "trade", tr.to
      return true
    end
    if not hungry_home then
      -- (at home with nothing packed: the trip is over, or never began)
      local hh = W.S.v3.households[p.hh]
      if hh and hh.trading == p.id then hh.trading = 0 end
      if s.trading == p.id then s.trading = 0 end
      if tr.sea then
        local boat = s.boats and s.boats[tr.boat]
        if boat and boat.trip == p.id then boat.trip = nil end
      end
      if stuck and p.pack then
        local home = people.home(W, s, p)
        if home then for _, g in ipairs(econ.ORDER) do
          local q = p.pack[g] or 0
          if q > 0 and not econ.GOODS[g].beast then local key = (g == "grain") and "food" or g; home.stock[key] = (home.stock[key] or 0) + q end
        end end
        p.pack, p.carry, p.camt = nil, "", 0
      end
      if tr.led and #tr.led > 0 then
        -- (a drove given up: the beasts are let go here)
        local G = econ.GOODS[tr.give]
        if G and G.beast then
          local L, H = stock.led_by(s, p, G.beast)
          for _, a in ipairs(L) do stock.release(W, s, H, a, now, "Let go: the drove was given up") end
        end
        tr.led = {}
      end
      p.trip = nil
    end
  end
  if p.boat and s.dock then
    -- afloat: back to the dock first, whatever else is wanted
    local obj, dest = PLAN.return_boat(W, s, p, ctx, now, nil)
    p.obj, p.dest, p.objk = obj, dest, "return_boat"
    p.why = (reason and (reason .. ". ") or "") .. "Rowing back to the dock"
    p.tgt_kind, p.tgt_ref = "return_boat", 0
    return true
  end
  local kind, why
  -- a hungry baby at night: its mother nurses it (in bed, or wherever it is)
  local night_feed = nil
  if (ctx.night or p.act == "sleep") and p.nursing > now then
    for i = 1, #p.kids do
      local ib = W.S.v3.people[p.kids[i]]
      if ib and ib.alive and ib.act == "carried" and ib.hunger > C().infant_feed then night_feed = ib; break end
    end
  end
  -- (S19) a weaned toddler in their arms who went to bed hungry wakes crying: porridge at home
  local night_babe = nil
  if not night_feed and (ctx.night or p.act == "sleep") then
    local ib = ctx.babe
    if ib and ib.hunger >= 0.9 and ctx.home and (ctx.home.stock.meals or 0) >= C().porridge then night_babe = ib end
  end
  -- night: home to sleep (the very tired go earlier)
  if night_feed then
    kind, why = "nurse", sfmt("%s woke hungry in the night", night_feed.name)
    add(1, "nurse", night_feed.id, 0, why)
    M.cd = CAND[ncand]
  elseif night_babe then
    kind, why = "feed_babe", sfmt("%s woke crying with hunger in the night", night_babe.name)
    add(1, "feed_babe", night_babe.id, 0, why)
    M.cd = CAND[ncand]
  elseif p.alarm and p.alarm.kind == "thief" and M.chase_ok(W, s, p, ctx, now) then
    -- (S20, R6a) a stranger at the beasts: out after him
    kind = "chase"
    add(1, "chase", p.alarm.src, 0, "")
    M.cd = CAND[ncand]
    why = p.alarm.how == "saw" and "A stranger is leading off one of the beasts: after him" or "The dogs are barking at the fold: someone is at the beasts"
  elseif ctx.night and M.guard_herd(W, s, p, ctx, now) then
    -- wolves took a beast lately: a grown man (else woman) of an owning household keeps the
    -- night's watch at the pen (one at each pen), sleeping by it
    local hi = M.guard_herd(W, s, p, ctx, now)
    kind = "guard"
    add(1, "guard", 0, hi, "")
    M.cd = CAND[ncand]
    local pen = s.buildings[s.herds[stock.HERDS[hi]].pen]
    local thief = (s.thief_alarm or -1e9) > (s.wolf_alarm or -1e9)
    local last = thief and s.thief_alarm or s.wolf_alarm
    why = sfmt("%s %s: keeping watch at the %s tonight", thief and "One of the beasts was taken" or "Wolves took one of the beasts",
      now - last < 24 and "last night" or "lately", pen.label)
  elseif s.alarm and (s.alarm.kind or "wolves") == "wolves" and now - s.alarm.t < 0.8 and ctx.stage == "adult" and not p.at and (p.hh == s.alarm.hh or p.objk == "guard")
    and (p.drove_t or -99) < now - 0.8 and not ctx.babe then
    -- (the dog is barking at the pen, or the watch woke to wolves: out to drive them off, D-105)
    kind, why = "drive_off", "The dog is barking at the pen: wolves"
    add(1, "drive_off", 0, 0, "")
    M.cd = CAND[ncand]
  elseif (ctx.night or p.energy < 0.12) and p.hunger > 0.5 and ctx.home and (ctx.home.stock.meals or 0) >= C().meal_fu * 0.5 then
    -- (nobody goes to bed starving with a meal at home: D-102, found after long voyages)
    kind, why = "eat", "Hungry before sleep"
    add(1, "eat", ctx.home.id, 0, "")
    M.cd = CAND[ncand]
  elseif ctx.night or p.energy < 0.12 then
    kind, why = "sleep", ctx.night and sfmt("Night (bedtime %s)", clock_str(ctx.bed)) or "Exhausted"
  elseif ctx.rain == 2 and p.inside == 0 and ctx.stage ~= "infant" then
    kind, why = "shelter", "A downpour"
  else
    eat_candidate(W, s, p, ctx, now)
    care_candidates(W, s, p, ctx, now)
    if ctx.workday then
      -- a mother holding her baby may leave it with a minder at home to go to work
      if ctx.babe and ctx.babe.mother == p.id and ctx.home then
        local m = kin.minder(W, s, p, ctx.home, now)
        if m then
          local n0 = ncand
          local ib = ctx.babe
          ctx.babe = nil
          work_candidates(W, s, p, ctx, now)
          local bu, bk = -1e9, nil
          while true do
            bu, bk = -1e9, nil
            for i = n0 + 1, ncand do if CAND[i].u > bu then bu, bk = CAND[i].u, CAND[i] end end
            if not bk or not bk.lazy then break end
            local fn = bk.lazy
            bk.lazy = nil
            fn(bk, bk.larg)
          end
          ncand = n0
          ctx.babe = ib
          if bk then
            addw(bu - 0.06 - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "leave_babe", m.id, ib.id,
              "Leaving %s with %s to go and %s", ib.name, m.name, (bk.kind == "till" and "hoe") or (bk.kind == "fell" and "fell trees") or bk.kind)
          end
        end
      end
      work_candidates(W, s, p, ctx, now)
      repair_candidates(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      fish_candidate(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      crew_candidate(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      trade_candidate(W, s, p, ctx, now, fatigue(p))
      M.steal_candidate(W, s, p, ctx, now, fatigue(p))
      M.claim_candidate(W, s, p, ctx, now, fatigue(p))
      court_candidate(W, s, p, ctx, now, fatigue(p))
      visit_candidate(W, s, p, ctx, now, fatigue(p))
      ask_kin_candidate(W, s, p, ctx, now, fatigue(p))
      news_candidate(W, s, p, ctx, now, fatigue(p))
      M.fair_candidate(W, s, p, ctx, now, fatigue(p))
      share_candidate(W, s, p, ctx, now, fatigue(p))
      leather_candidates(W, s, p, ctx, now)
      rack_candidates(W, s, p, ctx, now, fatigue(p))
      mend_boat_candidate(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      chores(W, s, p, ctx, now)
      craft_candidate(W, s, p, ctx, now)
      textile_candidates(W, s, p, ctx, now)
      herd_candidates(W, s, p, ctx, now)
      hunt_candidates(W, s, p, ctx, now)
    elseif not ctx.night then
      chores(W, s, p, ctx, now)
      textile_candidates(W, s, p, ctx, now)
    end
    leisure(W, s, p, ctx, now)
    follow_candidate(W, s, p, ctx, now)
    watch_candidate(W, s, p, ctx, now)
    if ctx.rain == 1 and p.inside == 0 then
      add(0.3 + (1 - p.dil) * 0.3, "shelter", 0, 0, "Rain: going indoors")
    end
    local cd = best_cand(p, now)
    if M.trace and M.trace[p.id] then
      -- (tools: every candidate this decision weighed)
      local t = {}
      for i = 1, ncand do t[i] = sfmt("%s %.2f%s", CAND[i].kind, CAND[i].u, CAND[i].lazy and "~" or "") end
      M.trace_out = M.trace_out or {}
      M.trace_out[#M.trace_out + 1] = sfmt("#%d %s e%.2f h%.2f: %s", p.id, clock_str(ctx.lh), p.energy, p.hunger, table.concat(t, ", "))
    end
    if cd then
      kind, why = cd.kind, why_of(cd)
      M.cd = cd
    else
      kind, why = "rest", "Nothing to do"
    end
  end
  local cd = M.cd
  if kind == "sleep" or kind == "shelter" then cd = nil; M.cd = nil end
  local obj, dest = PLAN[kind](W, s, p, ctx, now, cd)
  if p.carry ~= "" and not (p.carry == "seed" and FIELD[kind]) then
    -- whatever is in their hands is put away before anything else (a plan cut short by rain or
    -- night leaves a load in hand): seed and food at home (seed kept for the next sowing) or the
    -- granary, water home, sheaves to the granary, logs to the woodstore
    local home, to, toname = ctx.home, nil, nil
    local cr = p.carry
    if cr == "fish" then to = home or s.granary
    elseif cr == "sheaves" then to = s.granary
    elseif cr == "logs" then to = s.wood_store
    elseif cr == "hay" then to = s.herds and s.buildings[(s.herds.flock or s.herds.cattle or {}).pen or 0]
    elseif cr == "wool" or cr == "meat" or cr == "milk" or cr == "scraps" or cr == "carcass" or cr == "dried" or cr == "quern" then to = home
    elseif cr == "manure" then
      local H = s.herds and (s.herds.cattle or s.herds.flock or s.herds.swine)
      to = H and s.buildings[H.pen]
    elseif cr == "water" then to = home
    elseif (cr == "ore" or cr == "charcoal") and s.forge and s.forge.state == "standing" then to = s.forge
    elseif cr == "iron" then to = home
    elseif cr == "stone" or cr == "hardstone" then
      -- (S19) a stone in hand: to the yard, else back to a face of its own rock
      local y = s.wood_store
      if y and y.state == "standing" then to = y
      else
        local bd = 1e9
        for k = 1, #s.blist do
          local b = s.buildings[s.blist[k]]
          if b.kind == "outcrop" and (b.rock == "hard") == (cr == "hardstone") then
            local d = dist(p.x, p.y, b.dx, b.dy)
            if d < bd then to, bd = b, d end
          end
        end
      end
    else
      to = s.granary
      if home and (not to or dist(p.x, p.y, home.dx, home.dy) <= dist(p.x, p.y, to.dx, to.dy) + 0.1) then to = home end
    end
    if to then
      local pl = p.plan
      local ishome = to == home
      local dto = ishome and "home" or to.kind
      if cr == "stone" or cr == "hardstone" then dto = to.kind == "outcrop" and "stone_face" or "stone_yard" end
      if (cr == "ore" or cr == "charcoal") and to.kind == "forge" then dto = "forge" end
      if cr == "iron" then dto = "iron_to" end
      table.insert(pl, 1, { op = "drop", to = dto, ref = to.id })
      table.insert(pl, 1, { op = "go", x = to.dx, y = to.dy, place = ishome and "home" or ("the " .. to.label), enter = ishome and home.id or nil })
    end
  end
  p.obj, p.why, p.dest = obj, why, dest or ""
  p.objk = kind
  -- what they work on (an apprentice's mentor is found by it), and whether beside their mentor
  p.tgt_kind, p.tgt_ref = kind, cd and cd.a or 0
  p.mentored = ctx.stage == "apprentice" and ctx.mentor ~= nil and beside_mentor(W, ctx, kind, cd and cd.a or 0)
  if p.mentored and ctx.mentor then p.why = sfmt("Working beside %s. %s", ctx.mentor.name, p.why) end
  if kind == "play" or kind == "follow" then
    local d = floor(now / 24)
    if p.play_day ~= d then p.play_day, p.play_h = d, 0 end
    p.play_h = p.play_h + 0.8
  end
  if ctx.stage == "child" and (kind == "fetch_water" or kind == "fetch_wood" or kind == "forage") then
    local d = floor(now / 24)
    if p.chore_day ~= d then p.chore_day, p.chores = d, 0 end
    p.chores = p.chores + 1
  end
  if reason then p.why = reason .. ". " .. why end
  -- bread for the day, taken from home when setting out to work
  if (kind == "sow" or kind == "till" or kind == "harvest" or kind == "fell" or kind == "tend" or kind == "build")
    and not p.lunch and ctx.home and p.inside == ctx.home.id and (ctx.home.stock.meals or 0) > 1 then
    ctx.home.stock.meals = ctx.home.stock.meals - C().meal_fu
    p.lunch = true
  end
  M.cd = nil
  return true
end

return M
