-- Choosing what to do next (docs/v3/10_AGENTS.md §5). Called when a person's plan runs out or
-- is interrupted. Body first (sleep, shelter, eat), then everything they could do now — the
-- settlement's open work (sow, tend, harvest, carry sheaves, fell, carry logs, deliver, build),
-- their household's chores (food, firewood, water), care, leisure with a reason — scored by
-- urgency x fit x fatigue - travel. The chosen candidate becomes a plan of steps and a "why"
-- that names the state behind it.
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
local NOW = 0

local function clock_str(h)
  local hh = floor(h)
  local mm = floor((h - hh) * 60)
  return string.format("%02d:%02d", hh % 24, mm)
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
local CLAIM_OBJ = { cooking = "cook", fetch_food = "fetch_food", fetch_wood = "fetch_wood", fetch_water = "fetch_water", foraging = "forage",
  smoking = "smoke", dressing = "dress", weaving = "weave", sewing = "sew", mowing = "mow", feeding = "feed" }
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

local function go(pl, x, y, place, enter)
  pl[#pl + 1] = { op = "go", x = x, y = y, place = place, enter = enter }
end

local function work(pl, kind, dur, extra)
  local st = extra or {}
  st.op, st.kind, st.dur = "work", kind, dur
  pl[#pl + 1] = st
end

local function door(b) return b.dx, b.dy end
local function mapm_xy(s, ix, iy) return require("sim.agents.map").ixy_xy(s, ix, iy) end

-- A spot near (x, y) for person p (so people waiting or talking do not stand on each other).
local function near_spot(W, p, now, x, y, r, k)
  local a = u01(W, p, now, k or 7) * 6.283185307
  local rr = r * (0.4 + 0.6 * u01(W, p, now, (k or 7) + 1))
  -- (no trig in sim logic: a polygonal approximation of the circle)
  local ca, sa
  local t = a / 6.283185307 * 8
  local i = floor(t)
  local f = t - i
  local P = { { 1, 0 }, { 0.707, 0.707 }, { 0, 1 }, { -0.707, 0.707 }, { -1, 0 }, { -0.707, -0.707 }, { 0, -1 }, { 0.707, -0.707 }, { 1, 0 } }
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
  c.u, c.kind, c.a, c.b, c.why, c.lazy = u, kind, a, b, why, nil
end

-- A candidate whose target is costly to find (a scan of every tree) enters with an upper bound on
-- its utility and is worked out only if that bound would win (best_cand): exactly the choice the
-- full evaluation makes, at a fraction of the cost (D-086). fn(c, arg) sets c.u, c.a, c.b, c.why
-- (c.u = -1e9 when there is no target). The decision's context is in LZ.
local LZ = {}
local function add_lazy(bound, kind, fn, arg)
  ncand = ncand + 1
  local c = CAND[ncand]
  if not c then c = {}; CAND[ncand] = c end
  c.u, c.kind, c.a, c.b, c.why, c.lazy, c.larg = bound, kind, 0, 0, "", fn, arg
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
  woodcutter = { fell = true, haul_logs = true, grub = true },
  builder = { build = true, deliver = true, haul_logs = true, repair = true },
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
    -- (light chores from eight or nine; a child is never a field hand)
    local n = (p.chore_day == floor(NOW / 24)) and (p.chores or 0) or 0
    local tired = max(0.2, 1 - 0.3 * n)
    if kind == "fetch_water" and ctx.age >= C().help_age then return 1.2 * tired * bf end
    if kind == "fetch_wood" and ctx.age >= C().help_age + 1 then return 0.9 * tired * bf end
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
    if kind == "fell" or kind == "build" or kind == "haul_logs" or kind == "deliver" then f = f * 0.25
    elseif kind == "tend" or kind == "fetch_water" or kind == "fetch_food" then f = f * 1.1
    else f = f * 0.6 end
  end
  if p.preg_due > 0 and p.preg_due - NOW < 120 then
    if kind == "fell" or kind == "build" or kind == "haul_logs" then f = f * 0.1 end
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
  -- farming: a household works its own fields; another's only to help when they fall behind (late
  -- in the window) or at harvest (D-081)
  for k = 1, #plots do
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
            add(urg * f * fat - tc - extra, "sow", k, srow,
              string.format("It is sowing time: %d %s of %s still to sow; row %d is hoed", left, plural(left, "row", "rows"), pl.name, srow))
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
              add((0.42 + (wm and 0.15 or 0)) * (wm and 1 or (own and 1 or 0.25)) * f * fat - tc, "till", k, trow,
                string.format("Ploughing the fallow of %s against the weeds (%d %s to go)", pl.name, tl, plural(tl, "row", "rows")))
            else
              local urg = (0.62 + 0.25 * need.sow_late + (tp > 0 and 0.03 or 0) + (wm and 0.15 or 0)) * (wm and 1 or help)
              add(urg * f * fat - tc, "till", k, trow,
                tp > 0 and string.format("Hoeing %s for the %s crop: row %d is %d%% done (%d %s to go)", pl.name, pl.crop, trow, floor(tp * 100), tl, plural(tl, "row", "rows"))
                  or string.format("It is time to sow the %s crop and %s is not yet hoed (%d %s to go)", pl.crop, pl.name, tl, plural(tl, "row", "rows")))
            end
          end
        end
      end
      if pl.stage == "growing" and pl.weeds > c.weeds_tend and (pl.tend_claim or 0) < 3 then
        local f = fit(p, ctx, "tend", k)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if f > 0 then
          add((0.25 + 0.6 * pl.weeds + (wm and 0.15 or 0)) * (wm and 1 or (own and 1 or 0.45)) * f * fat - tc, "tend", k, 0,
            string.format("Weeds are choking %s (%d%%)", pl.name, floor(pl.weeds * 100)))
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
            add(urg * f * fat - tc, "harvest", k, row,
              string.format("%s is ripe%s; the granary holds %d days of food", pl.name,
                (pl.ripe_for or 0) >= 2 and string.format(" (%d days standing)", pl.ripe_for) or "", floor(need.food_days)))
          end
        end
      end
      if (pl.sheaves >= 2 or (pl.sheaves >= 0.05 and pl.stage == "stubble")) and (pl.haul_claim or 0) < pl.sheaves / c.carry_grain then
        local f = fit(p, ctx, "haul_grain", k)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if f > 0 then
          local urg = 0.7 + (s.rain_soon and 0.15 or 0) + (wm and 0.12 or 0)
          add(urg * f * fat - tc - travel(ctx, s.granary_x, s.granary_y, cx, cy) * 0.5, "haul_grain", k, 0,
            string.format("%d sheaves lie in %s; they belong in the granary", floor(pl.sheaves), pl.name))
        end
      end
    end
  end
  -- retted flax waiting on its field: carried to the granary (D-088)
  for k = 1, #plots do
    local pl = plots[k]
    if pl.stage == "stubble" and (pl.flax or 0) >= 0.05 and (pl.flax_claim or 0) < pl.flax / c.carry_flax then
      local f = fit(p, ctx, "haul_grain", k)
      ctx.with_mentor = false
      if f > 0 then
        local cx, cy = (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5
        add((0.55 + (s.rain_soon and 0.15 or 0)) * f * fat - travel(ctx, cx, cy, px, py) - travel(ctx, s.granary_x, s.granary_y, cx, cy) * 0.5,
          "haul_flax", k, 0, string.format("The flax on %s is retted (%.0f garments' worth): it belongs in the granary", pl.name, pl.flax))
      end
    end
  end
  -- wood: carry felled logs, fell trees
  local tr = s.trees
  if need.lying_trees > 0 then
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
      local dest = need.site_logs > 0 and "a building site" or "the woodstore"
      add((0.52 + 0.2 * need.wood_short + (bwm and 0.15 or 0)) * f * fat - bd / ctx.spd * 0.45, "haul_logs", best, 0,
        bwm and string.format("Carrying the logs %s cut to %s", ctx.mentor.name, dest)
          or string.format("%d %s lie where a tree was felled; %s needs them", tr.logs[best], plural(tr.logs[best], "log", "logs"), dest))
    end
  end
  if need.fell > 0 or need.clear_trees > 0 then
    local f = fit(p, ctx, "fell")
    if f > 0 and ctx.stage == "adult" then
      -- (the walk to the tree only lowers it)
      local k0 = (s.clearing and need.clear_trees > 0) and max(0.62, 0.35 + 0.5 * need.wood_short) or (0.35 + 0.5 * need.wood_short)
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
  if ctx.home and ctx.stage ~= "child" and ctx.hh and claim_free(W, ctx.hh, "foraging", p) then
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
  -- threshing the sheaves in the granary (under its roof: rain does not stop it): as the threshed
  -- grain runs down, and on wet and winter days when the fields have no work (D-080)
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
      add(urg * f * fatigue(p) - travel(ctx, s.granary_x, s.granary_y, px, py), "thresh", 0, 0,
        low > 0.3 and string.format("Only %d days of threshed grain are left; %d FU of sheaves wait at the granary", floor(gdays), floor(g.stock.sheaves))
          or (ctx.rain > 0 and string.format("A wet day: threshing under the granary roof (%d FU of sheaves)", floor(g.stock.sheaves))
          or string.format("%d FU of sheaves wait to be threshed at the granary", floor(g.stock.sheaves))))
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
          add((0.55 + 0.1 * b.prio) * f * fat - tc - travel(ctx, s.wood_x, s.wood_y, px, py) * 0.5, "deliver", b.id, 0,
            string.format("The %s for %s still needs %d logs", b.label, b.for_name or "the village", b.logs_need - b.logs_have))
        end
      end
      if b.logs_have >= min(b.logs_need, 4) and b.work_done < b.work_need and (b.builders or 0) < 3 then
        local f = fit(p, ctx, "build", b.id)
        local wm = ctx.with_mentor
        ctx.with_mentor = false
        if b.kind == "boat" and b.for_hh == p.hh and f > 0 then f = max(f, 1.3) end
        if f > 0 and (ctx.stage == "adult" or ctx.stage == "elder" or wm) then
          add((0.6 + 0.1 * b.prio + (wm and 0.15 or 0)) * f * fat - tc, "build", b.id, 0,
            wm and string.format("Helping %s raise the %s for %s", ctx.mentor.name, b.label, b.for_name or "the village")
              or string.format("Raising the %s for %s (%d%% done)", b.label, b.for_name or "the village",
              floor(100 * b.work_done / b.work_need)))
        end
      end
    end
  end
end

-- The tree to fell (settle.next_tree: a scan of the woods) and its utility (lazy, see add_lazy).
function M.lazy_fell(c, ff)
  local W, s, p, ctx = LZ.W, LZ.s, LZ.p, LZ.ctx
  local need, tr = s.need, s.trees
  local k, clearing = settle.next_tree(W, s, p)
  if k == 0 then c.u = -1e9; return end
  local d = dist(p.x, p.y, tr.x[k], tr.y[k])
  c.a, c.b = k, 0
  if clearing then
    c.u = 0.62 * ff - d / ctx.spd * 0.45
    c.why = string.format(s.expand_why == "spare" and "Clearing ground for a new field (%d %s still standing): hands to spare, and a bigger store against a bad year"
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
  for k = 1, tr.n do
    if tr.st[k] == 3 and tr.claim[k] == 0 and s.plot[tr.tile[k]] == -1 then
      local d = dist(px, py, tr.x[k], tr.y[k])
      if d < bd then best, bd = k, d end
    end
  end
  if best == 0 then c.u = -1e9; return end
  c.a, c.b = best, 0
  c.u = 0.55 * ff - bd / ctx.spd * 0.45
  c.why = string.format("Digging out stumps on the new field ground (%d left)", s.need.clear_stumps)
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
    local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }
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
    if b.crew == 0 and not b.lost and (b.owner == p.hh or b.owner == 0) and (b.cond or 1) > c.boat_unsafe then return b end
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
  local u = (0.22 + 0.18 * min(1, fd) + need + store + short) * f * fat - 0.3 * min(2, fresh)
  add(u - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "fish", boat.id, 0,
    store > 0.03 and string.format("Fishing for the winter: %.0f days of smoked fish in the house", smoked)
      or (need > 0.05 and string.format("Little food at home (%.1f days): out with the boat", pantry)
      or string.format("The fish are running offshore (%.0f%% of a full sea); the boat is ready", min(100, fd * 100))))
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
      add((0.3 + 0.9 * (c.boat_mend - b.cond)) * f * fat - travel(ctx, s.dock.dx, s.dock.dy, p.x, p.y), "mend_boat", b.id, 0,
        string.format("Our boat is leaking (%d%% sound): it wants new planks and caulking", floor((b.cond or 0) * 100)))
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
  add((0.36 + 0.8 * (c.repair_at - best.cond)) * f * fat - bd / ctx.spd * 0.45, "repair", best.id, 0,
    string.format("The roof of the %s house lets the weather in (%d%% sound)", q and q.surname or "empty", floor(best.cond * 100)))
end

-- Tools (D-080): the household's tools wear with work; worn tools slow it. Mending and making them
-- (handles, baskets, a new edge) is done at home, by the door when the weather is fair.
local function tool_factor(W, p)
  local hh = W.S.v3.households[p.hh]
  return 0.55 + 0.45 * (hh and hh.tools or 1)
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
  add(u - travel(ctx, ctx.hx, ctx.hy, p.x, p.y), "craft", home.id, 0,
    tools < 0.6 and string.format("The household's tools are worn (%d%%): a hoe handle is split, the sickle blunt", floor(tools * 100))
      or string.format("Mending tools and weaving baskets (the household's tools at %d%%)", floor(tools * 100)))
end

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
      add((0.3 + 0.45 * (1 - meals / want) + soon) * f * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, px, py), "cook", home.id, 0,
        meals < c.meal_fu and "Nothing cooked at home: baking bread and boiling porridge"
          or string.format("Cooking for the household (%.1f meals left for %d)", meals / c.meal_fu, n))
    end
  end
  local gran = s.granary
  if gran and (gran.stock.grain or 0) > 1 and food < need_day * c.pantry_low and claim_free(W, hh, "fetch_food", p) then
    local f = fit(p, ctx, "fetch_food")
    if f > 0 then
      local days = food / max(1, need_day)
      add((0.55 + 0.4 * (1 - min(1, days / c.pantry_low))) * f * fat - travel(ctx, s.granary_x, s.granary_y, px, py),
        "fetch_food", 0, 0, string.format("Only %.1f days of food left at home", days))
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
      add(urg * f * fat - travel(ctx, s.wood_x, s.wood_y, px, py), "fetch_wood", 0, 0,
        wdays < 1 and string.format("The woodpile at home is almost gone (%.1f logs; the hearth burns %.1f a day)", logs, burn)
          or string.format("Stacking firewood at home: %.1f days of burning left", wdays))
    end
  end
  -- fresh fish beyond what the pot will take today is smoked at the hearth before it spoils (D-087)
  local surplus = (home.stock.fish or 0) + (home.stock.meat or 0) - n * 0.8
  if surplus >= 1 and claim_free(W, hh, "smoking", p) and (home.stock.logs or 0) >= 0.3 then
    local f = fit(p, ctx, "smoke")
    if f > 0 then
      add((0.28 + 0.1 * min(4, surplus)) * f * fatigue(p) - travel(ctx, ctx.hx, ctx.hy, px, py), "smoke", home.id, 0,
        string.format("%.0f FU of fresh fish will not keep: smoking it over the hearth", surplus))
    end
  end
  if (home.stock.water or 0) < 1 and claim_free(W, hh, "fetch_water", p) and s.well then
    local f = fit(p, ctx, "fetch_water")
    if f > 0 then
      add(0.45 * f * fat - travel(ctx, s.well.dx, s.well.dy, px, py), "fetch_water", 0, 0,
        "The water jar at home is empty")
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
  local urg = min(1, def / max(1, n * 0.3))
  local slack = ((s.doy or 0) >= 36 or ctx.rain > 0 or not ctx.workday) and 0.07 or 0
  local fat = fatigue(p)
  -- sewing: a garment for whoever's clothes are worst, when the cloth is there
  if cloth >= 1 and worst < 0.5 and claim_free(W, hh, "sewing", p) then
    local f = fit(p, ctx, "sew")
    if f > 0 then
      add((0.32 + 0.5 * (0.5 - worst) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "sew", home.id, wq.id,
        wq == p and string.format("My clothes are worn through (%d%%): sewing new ones from our cloth", floor(worst * 100))
          or string.format("%s's clothes are worn through (%d%%): sewing new ones from our cloth", wq.name, floor(worst * 100)))
    end
  end
  -- weaving at the loom (one weaver a house)
  if yarn >= 0.3 and cloth < want and claim_free(W, hh, "weaving", p) then
    local f = fit(p, ctx, "weave")
    if f > 0 then
      add((0.2 + 0.18 * min(1, yarn) + 0.12 * urg + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "weave", home.id, 0,
        string.format("%.1f garments' worth of yarn waits at the loom", yarn))
    end
  end
  -- spinning (as many spindles as hands)
  if fib >= 0.02 and yarn + cloth < want * 1.3 then
    local f = fit(p, ctx, "spin")
    if f > 0 then
      local short = min(1, (want * 1.3 - yarn - cloth) / want)
      local eve = (ctx.lh > ctx.sunset - 2.5) and 0.05 or 0
      add((0.16 + 0.16 * short + 0.1 * urg + slack + eve) * f * fat, "spin", home.id, 0,
        urg > 0.2 and string.format("The household's clothes are wearing thin: spinning flax (%.1f garments' worth of fibre)", fib)
          or string.format("Spinning flax into yarn (%.1f garments' worth of fibre at home)", fib))
    end
  end
  -- dressing flax: the raw bundles from the granary broken, scutched and hackled into fibre
  local gflax = s.granary and (s.granary.stock.flax or 0) or 0
  if fib < want * 0.3 and fib + yarn + cloth < want * 1.5 and ((st.flax or 0) >= 0.3 or gflax >= 1) and claim_free(W, hh, "dressing", p) then
    local f = fit(p, ctx, "dress")
    if f > 0 then
      local walk = (st.flax or 0) >= 0.3 and 0 or travel(ctx, s.granary_x, s.granary_y, p.x, p.y) * 0.5
      add((0.2 + 0.12 * urg + slack) * f * fat * WEATHER_OK(ctx, p) - walk, "dress", home.id, 0,
        string.format("Breaking and hackling flax: the house has %.1f garments' worth of fibre, yarn and cloth", fib + yarn + cloth))
    end
  end
  -- mending one's own clothes (a little yarn)
  local mine = p.clothes or 1
  if mine < 0.55 and yarn >= c.mend_clothes_yarn then
    local f = fit(p, ctx, "mend_clothes")
    if f > 0 then
      add((0.16 + 0.5 * (0.55 - mine) + slack) * f * fat - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "mend_clothes", home.id, 0,
        string.format("Patching my clothes (%d%% whole)", floor(mine * 100)))
    end
  end
end

-- The flock (D-089). Herding is children's work as much as anyone's: a child of an owning
-- household (from nine) takes the sheep out of the fold to the day's pasture, watches them, and
-- brings them home before dusk. Shearing in late spring, hay mown in summer for the winter, hay
-- put in the fold's rack when the sheep are kept in.
local function owns_sheep(F, hh)
  for _, a in ipairs(F.sheep) do if a.alive and a.owner == hh then return true end end
  return false
end

local function herd_candidates(W, s, p, ctx, now)
  local F = s.flock
  if not F or ctx.night or not ctx.hh then return end
  local c = C()
  local flock = require("sim.agents.flock")
  local fold = s.buildings[F.fold]
  local st = ctx.stage
  local f = (st == "child" and ctx.age >= c.help_age + 1) and 1.3 or (st == "apprentice" and 1.1) or (st == "elder" and 0.9)
    or (st == "adult" and 0.6) or 0
  if p.role == "shepherd" then f = 1.45 end
  if ctx.babe then f = f * 0.6 end
  local h = flock.herder(W, s)
  local late = ctx.lh > ctx.sunset - 1.2
  if f > 0 and h == p then
    if late then
      add(1.0, "herd_home", 0, 0, "Dusk is coming: bringing the sheep home to the fold")
    else
      -- (once out, the flock is theirs to mind until it is home: not left for play)
      add((0.64 + 0.1 * f) * fatigue(p) * WEATHER_OK(ctx, p), "herd", 1, 0, "Keeping the sheep on fresh grass")
    end
    return
  end
  local mine = owns_sheep(F, p.hh)
  -- (the children of the household have the first chance in the morning: the grown-ups wait)
  local wait = st == "child" and 0.4 or 1.6
  if f > 0 and mine and not h and not F.out and not late and ctx.lh > ctx.wake + wait and ctx.rain < 2 then
    local hung, n = 0, 0
    for _, a in ipairs(F.sheep) do if a.alive then hung, n = hung + a.hunger, n + 1 end end
    if n > 0 then
      hung = hung / n
      -- (a duty the household gives its children more than a choice)
      add((0.45 + 0.45 * hung) * f * fatigue(p) * WEATHER_OK(ctx, p) - travel(ctx, fold.dx, fold.dy, p.x, p.y), "herd", 0, 0,
        string.format("The sheep are hungry in the fold (%d%%): taking them out to graze", floor(hung * 100)))
    end
  end
  if not mine or st == "child" and ctx.age < c.help_age + 1 then return end
  local doy = s.doy or 0
  -- shearing: a sheep of the household with its fleece grown, in the fold
  if doy >= c.shearing[1] and doy <= c.shearing[2] and not F.out and st ~= "child" then
    for k, a in ipairs(F.sheep) do
      if a.alive and a.owner == p.hh and (a.wool or 0) >= 1 and (a.shearer or 0) == 0 and (now - a.born) / 1152 > c.lamb_years then
        add(0.48 * fatigue(p) * WEATHER_OK(ctx, p) - travel(ctx, fold.dx, fold.dy, p.x, p.y), "shear", k, 0,
          string.format("Shearing time: a fleece of %.1f garments' wool on one of our sheep", a.wool))
        break
      end
    end
  end
  -- autumn: a beast of the household marked for killing (flock.plan_cull)
  if (st == "adult") and not F.out then
    for k, a in ipairs(F.sheep) do
      if a.alive and a.cull and a.owner == p.hh and (a.butcher or 0) == 0 then
        add(0.5 * fatigue(p) - travel(ctx, fold.dx, fold.dy, p.x, p.y), "slaughter", k, 0,
          "The hay will not winter the whole flock: killing one of our beasts for the larder")
        break
      end
    end
  end
  -- hay for the winter: mown in summer on the best meadow (not where the flock grazes today)
  local nsheep = 0
  for _, a in ipairs(F.sheep) do if a.alive then nsheep = nsheep + 1 end end
  local want = nsheep * c.hay_days * c.sheep_hunger * 24 * 0.6
  if doy >= 12 and doy < 26 and (F.hay or 0) < want and (st == "adult" or st == "apprentice") and claim_free(W, ctx.hh, "mowing", p) then
    local ff = fit(p, ctx, "harvest")
    if ff > 0 then
      add((0.3 + 0.35 * (1 - (F.hay or 0) / want)) * ff * fatigue(p) * WEATHER_OK(ctx, p), "mow", 0, 0,
        string.format("Making hay for the winter: the stack holds %d of the %d days the flock will want", floor((F.hay or 0) / max(1, nsheep * 0.5)), c.hay_days))
    end
  end
  -- hay into the rack when the flock is kept in and hungry
  if not F.out and (F.hay or 0) > 1 and (F.rack or 0) < nsheep * 0.2 and claim_free(W, ctx.hh, "feeding", p) then
    local hung, n = 0, 0
    for _, a in ipairs(F.sheep) do if a.alive then hung, n = hung + a.hunger, n + 1 end end
    hung = n > 0 and hung / n or 0
    local winter = doy >= 36 or (s.flock_grass_low or false)
    if hung > 0.45 and (winter or late or ctx.rain > 0) then
      add((0.35 + 0.5 * hung) * fatigue(p) - travel(ctx, fold.dx, fold.dy, p.x, p.y), "feed", 0, 0,
        string.format("The sheep are hungry in the fold (%d%%): hay into the rack", floor(hung * 100)))
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
      if q and q.alive and q.act == "play" and p.rel_v[i] + (q.hh == p.hh and 0.2 or 0) > ba then best, ba = q, p.rel_v[i] + (q.hh == p.hh and 0.2 or 0) end
    end
    -- (a whole day of play palls: chores and tagging along win now and then)
    local d = floor(now / 24)
    local played = p.play_day == d and (p.play_h or 0) or 0
    local u = (0.42 + 0.1 * p.soc) * wx * max(0.45, 1 - played * 0.06)
    if best then
      add(u + 0.1, "play", best.id, 0, string.format("Playing with %s", best.name))
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
    add(u + draw - travel(ctx, s.sqx, s.sqy, px, py) * 0.5, "talk", 0, 0,
      there > 0 and string.format("%d %s talking by the well", there, plural(there, "neighbour is", "neighbours are"))
        or "Going to the well to talk")
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
      if q and q.alive and q.hh ~= p.hh and q.inside ~= 0 then
        local a = p.rel_v[i] + (people.is_kin(W, p, qid) and 0.2 or 0)
        if a > ba then best, ba = qid, a end
      end
    end
    if best > 0 then
      local q = W.S.v3.people[best]
      local qb = s.buildings[q.inside]
      if qb and qb.kind == "house" then
        add((0.1 + 0.25 * ba) * (evening and 1.5 or 0.25) - travel(ctx, qb.dx, qb.dy, px, py) * 0.5, "visit", best, qb.id,
          string.format("Visiting %s%s", people.full_name(q), people.is_kin(W, p, best) and " (family)" or " (a friend)"))
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
        add(u, "nurse", ib.id, 0, q == p and string.format("%s is hungry: nursing", ib.name)
          or string.format("%s is hungry; %s has the baby", ib.name, q and q.name or "nobody"))
      end
    end
  end
  local ib = ctx.babe
  if ib and ib.hunger > c.infant_feed and not kin.nursing_mother(W, ib, now) and ctx.home then
    if (ctx.home.stock.meals or 0) >= c.porridge then
      add(0.9 + ib.hunger - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "feed_babe", ib.id, 0,
        string.format("%s is hungry: porridge at home", ib.name))
    elseif people.pantry(ctx.home) >= 0.3 and ctx.hh and claim_free(W, ctx.hh, "cooking", p) then
      add(0.85 + ib.hunger - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "cook", ctx.home.id, 0,
        string.format("%s is hungry and nothing is cooked: making porridge", ib.name))
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
    if q and q.alive and q.inside == 0 and CHILD_FOLLOW[q.objk or ""] and q.act ~= "sleep" then
      local qx, qy = act.pos(q, now)
      local d = dist(p.x, p.y, qx, qy)
      if d < 1.0 then
        local u = (0.36 + 0.22 * max(0, 1 - ctx.age / 9) + 0.15 * people.affinity(p, q.id)) - d * 0.25
        if u > bu then best, bu = q, u end
      end
    end
  end
  if best then
    add(bu * (ctx.rain > 0 and 0.3 or 1), "follow", best.id, 0,
      string.format("Tagging along with %s %s", best.sex == "f" and "mother," or "father,", best.name))
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
        add(0.24 + 0.1 * people.affinity(p, q.id) - travel(ctx, qx, qy, p.x, p.y) * 0.5, "watch", q.id, 0,
          string.format("Keeping an eye on %s at play", q.name))
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
  elseif home and people.pantry(home) >= 0.3 and ctx.stage ~= "child" and (ctx.hh and claim_free(W, ctx.hh, "cooking", p)) then
    -- nothing cooked: hungry, they cook (and then eat)
    add(0.45 + h - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.3, "cook", home.id, 1, "Hungry and nothing cooked at home")
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
  local home = s.buildings[cd.a]
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
  work(pl, "cook", c.cook_h * (0.75 + 0.5 * u01(W, p, now, 44)), { inside = home.id, ref = home.id })
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
    local P8 = { { 1, 0 }, { 0.7, 0.7 }, { 0, 1 }, { -0.7, 0.7 }, { -1, 0 }, { -0.7, -0.7 }, { 0, -1 }, { 0.7, -0.7 } }
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
  local x, y = near_spot(W, p, now, w.dx, w.dy, 0.03, 17)
  go(pl, x, y, "the well")
  work(pl, "fill", 0.12 + 0.08 * u01(W, p, now, 18), { fx = w.dx, fy = w.dy })
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id, release = "fetch_water" }
  return "Fetch water", "the well"
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
    -- seed from home when some is kept there, else from the granary
    local home = ctx.home
    if home and (home.stock.seed or 0) >= c.seed_row and dist(p.x, p.y, home.dx, home.dy) <= dist(p.x, p.y, s.granary_x, s.granary_y) + 0.1 then
      go(pl, home.dx, home.dy, "home", home.id)
      pl[#pl + 1] = { op = "pick", what = "seed", ref = home.id, n = c.seed_row * 4 }
    else
      local g = s.granary
      go(pl, g.dx, g.dy, "the granary")
      pl[#pl + 1] = { op = "pick", what = "grain", as = "seed", ref = g.id, n = c.seed_row * 4 }
    end
  end
  row_stint(W, s, p, now, plot, cd.b, "sow", c.sow_row_h * (1.25 - 0.45 * p.sk_farm) * (0.9 + 0.2 * u01(W, p, now, 19)), 36)
  return "Sow " .. plot.name, plot.name
end

PLAN.till = function(W, s, p, ctx, now, cd)
  local c = C()
  plan_reset(p)
  local plot = s.plots[cd.a]
  plot.row_claim[cd.b] = p.id
  local full = c.till_row_h * (1.25 - 0.45 * p.sk_farm) * (0.9 + 0.2 * u01(W, p, now, 26)) * (p.strong and 0.9 or 1) / tool_factor(W, p)
  local a0 = row_stint(W, s, p, now, plot, cd.b, "till", full, 37)
  local verb = plot.stage == "rest" and "Plough the fallow of" or "Hoe"
  return a0 > 0 and string.format("%s %s (row %d, %d%% done)", verb, plot.name, cd.b, floor(a0 * 100)) or (verb .. " " .. plot.name), plot.name
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
  ctx.hh.foraging = p.id
  local x, y = tr.x[best] + 0.025, tr.y[best] + 0.01
  go(pl, x, y, "the woods' edge")
  local x2, y2 = near_spot(W, p, now, x, y, 0.06, 33)
  work(pl, "forage", c.forage_h * (0.8 + 0.5 * u01(W, p, now, 34)), { x1 = x2, y1 = y2, luck = u01(W, p, now, 35) })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Gather food in the woods", "the woods' edge"
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
  return a0 > 0 and string.format("Reap %s (row %d, %d%% cut)", plot.name, cd.b, floor(a0 * 100)) or ("Reap " .. plot.name), plot.name
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
  if site and site.state == "site" then
    site.deliver_claim = (site.deliver_claim or 0) + 1
    go(pl, site.dx, site.dy, site.label)
    pl[#pl + 1] = { op = "drop", to = "site", ref = site.id }
    return "Carry logs to the " .. site.label, site.label
  end
  local w = s.wood_store
  go(pl, w.dx, w.dy, "the woodstore")
  pl[#pl + 1] = { op = "drop", to = "woodstore", ref = w.id }
  return "Carry logs to the woodstore", "the woodstore"
end

PLAN.deliver = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local b = s.buildings[cd.a]
  b.deliver_claim = (b.deliver_claim or 0) + 1
  local w = s.wood_store
  go(pl, w.dx, w.dy, "the woodstore")
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
  local amt = max(1, (home.stock.fish or 0) - #ctx.hh.members * 0.8)
  work(pl, "smoke", min(2.0, max(0.5, amt / c.smoke_fu_h)), { inside = home.id, ref = home.id })
  return "Smoke the fish", "home"
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
  if (home.stock.flax or 0) < 0.3 then
    local g = s.granary
    go(pl, g.dx, g.dy, "the granary")
    pl[#pl + 1] = { op = "pick", what = "flax", from = "store", ref = g.id, n = 2 }
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

-- The flock (D-089).
PLAN.herd = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local F = s.flock
  local fold = s.buildings[F.fold]
  local flock = require("sim.agents.flock")
  local x, y = flock.pick_spot(W, s, now)
  if not x then x, y = fold.dx, fold.dy end
  if cd.a == 0 then
    -- open the fold and drive them out
    go(pl, fold.dx, fold.dy, "the fold")
    work(pl, "open_fold", 0.06, { sx = x, sy = y, fx = fold.cx, fy = fold.cy })
  end
  local wx, wy = near_spot(W, p, now, x, y, 0.04, 61)
  go(pl, wx, wy, "the pasture")
  work(pl, "herd", (1.0 + 1.5 * u01(W, p, now, 62)) * p.dil, { fx = x, fy = y })
  return cd.a == 0 and "Take the sheep out to graze" or "Mind the sheep", "the pasture"
end

PLAN.herd_home = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local fold = s.buildings[s.flock.fold]
  go(pl, fold.dx, fold.dy, "the fold")
  -- (the flock follows them in; they wait a little at the gate, then close it)
  work(pl, "herd", 0.3, { fx = fold.cx, fy = fold.cy })
  work(pl, "close_fold", 0.06, { fx = fold.cx, fy = fold.cy })
  return "Bring the sheep home", "the fold"
end

PLAN.shear = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local F = s.flock
  local fold = s.buildings[F.fold]
  local a = F.sheep[cd.a]
  a.shearer = p.id
  local flock = require("sim.agents.flock")
  local ax, ay = flock.pos(a, now)
  go(pl, ax + 0.012, ay, "the fold")
  work(pl, "shear", 0.3 + 0.2 * u01(W, p, now, 63), { ref = cd.a, fx = ax, fy = ay })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home")
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Shear a sheep", "the fold"
end

PLAN.mow = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local F = s.flock
  local fold = s.buildings[F.fold]
  ctx.hh.mowing = p.id
  local flock = require("sim.agents.flock")
  -- the best meadow block that is not today's pasture
  local N, BS = s.N, 8
  local bn = N / BS
  local best, bs = 0, -1e9
  local sb = flock.block_of(s, F.spot_x or fold.cx, F.spot_y or fold.cy)
  for b = 1, bn * bn do
    local g = F.grass[b] or 0
    if b ~= sb and g > 0.4 then
      local by = floor((b - 1) / bn)
      local bx = (b - 1) - by * bn
      local x, y = mapm_xy(s, bx * BS + BS / 2, by * BS + BS / 2)
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
  local pl = plan_reset(p)
  local F = s.flock
  local a = F.sheep[cd.a]
  a.butcher = p.id
  local flock = require("sim.agents.flock")
  local ax, ay = flock.pos(a, now)
  go(pl, ax + 0.012, ay, "the fold")
  work(pl, "slaughter", 0.6 + 0.3 * u01(W, p, now, 67), { ref = cd.a, fx = ax, fy = ay })
  local home = ctx.home
  go(pl, home.dx, home.dy, "home", home.id)
  pl[#pl + 1] = { op = "drop", to = "home", ref = home.id }
  return "Kill a beast for the larder", "the fold"
end

PLAN.feed = function(W, s, p, ctx, now, cd)
  local pl = plan_reset(p)
  local F = s.flock
  local fold = s.buildings[F.fold]
  ctx.hh.feeding = p.id
  local n = 0
  for _, a in ipairs(F.sheep) do if a.alive then n = n + 1 end end
  go(pl, fold.dx, fold.dy, "the fold")
  work(pl, "feed", 0.2 + 0.1 * u01(W, p, now, 66), { amt = n * 0.5, fx = fold.cx, fy = fold.cy })
  pl[#pl + 1] = { op = "drop", to = "fold", ref = fold.id, release = "feeding" }
  return "Hay for the sheep", "the fold"
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
        elseif st.kind == "shear" then
          local a = s.flock and s.flock.sheep[st.ref or 0]
          if a and a.shearer == p.id then a.shearer = 0 end
        elseif st.kind == "slaughter" then
          local a = s.flock and s.flock.sheep[st.ref or 0]
          if a and a.butcher == p.id then a.butcher = 0 end
        elseif st.kind == "dress" or st.kind == "weave" or st.kind == "sew" then
          local hh = W.S.v3.households[p.hh]
          local field = st.kind == "dress" and "dressing" or (st.kind == "weave" and "weaving" or "sewing")
          if hh and hh[field] == p.id then hh[field] = 0 end
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
        end
      elseif st.op == "drop" then
        if st.to == "site" then
          local b = s.buildings[st.ref]
          if b then b.deliver_claim = max(0, (b.deliver_claim or 1) - 1) end
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
  local ctx = context(W, s, p, now)
  if ctx.stage == "infant" then return think_infant(W, s, p, now) end
  ncand = 0
  LZ.W, LZ.s, LZ.p, LZ.ctx = W, s, p, ctx
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
  -- night: home to sleep (the very tired go earlier)
  if night_feed then
    kind, why = "nurse", string.format("%s woke hungry in the night", night_feed.name)
    add(1, "nurse", night_feed.id, 0, why)
    M.cd = CAND[ncand]
  elseif ctx.night or p.energy < 0.12 then
    kind, why = "sleep", ctx.night and string.format("Night (bedtime %s)", clock_str(ctx.bed)) or "Exhausted"
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
            add(bu - 0.06 - travel(ctx, ctx.hx, ctx.hy, p.x, p.y) * 0.5, "leave_babe", m.id, ib.id,
              string.format("Leaving %s with %s to go and %s", ib.name, m.name, (bk.kind == "till" and "hoe") or (bk.kind == "fell" and "fell trees") or bk.kind))
          end
        end
      end
      work_candidates(W, s, p, ctx, now)
      repair_candidates(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      fish_candidate(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      mend_boat_candidate(W, s, p, ctx, now, fatigue(p) * WEATHER_OK(ctx, p))
      chores(W, s, p, ctx, now)
      craft_candidate(W, s, p, ctx, now)
      textile_candidates(W, s, p, ctx, now)
      herd_candidates(W, s, p, ctx, now)
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
      for i = 1, ncand do t[i] = string.format("%s %.2f%s", CAND[i].kind, CAND[i].u, CAND[i].lazy and "~" or "") end
      M.trace_out = M.trace_out or {}
      M.trace_out[#M.trace_out + 1] = string.format("#%d %s e%.2f h%.2f: %s", p.id, clock_str(ctx.lh), p.energy, p.hunger, table.concat(t, ", "))
    end
    if cd then
      kind, why = cd.kind, cd.why
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
    elseif cr == "hay" then to = s.flock and s.buildings[s.flock.fold]
    elseif cr == "wool" or cr == "meat" then to = home
    elseif cr == "water" then to = home
    else
      to = s.granary
      if home and (not to or dist(p.x, p.y, home.dx, home.dy) <= dist(p.x, p.y, to.dx, to.dy) + 0.1) then to = home end
    end
    if to then
      local pl = p.plan
      local ishome = to == home
      table.insert(pl, 1, { op = "drop", to = ishome and "home" or to.kind, ref = to.id })
      table.insert(pl, 1, { op = "go", x = to.dx, y = to.dy, place = ishome and "home" or ("the " .. to.label), enter = ishome and home.id or nil })
    end
  end
  p.obj, p.why, p.dest = obj, why, dest or ""
  p.objk = kind
  -- what they work on (an apprentice's mentor is found by it), and whether beside their mentor
  p.tgt_kind, p.tgt_ref = kind, cd and cd.a or 0
  p.mentored = ctx.stage == "apprentice" and ctx.mentor ~= nil and beside_mentor(W, ctx, kind, cd and cd.a or 0)
  if p.mentored and ctx.mentor then p.why = string.format("Working beside %s. %s", ctx.mentor.name, p.why) end
  if kind == "play" or kind == "follow" then
    local d = floor(now / 24)
    if p.play_day ~= d then p.play_day, p.play_h = d, 0 end
    p.play_h = p.play_h + 0.8
  end
  if ctx.stage == "child" and (kind == "fetch_water" or kind == "fetch_wood") then
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
