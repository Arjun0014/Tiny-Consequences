-- Earthquakes (R6.5f, D-156, docs/v3/65_R6_5_LIVING_PLANET.md): the planet's plates never move
-- (02 §5.12), but their boundaries hold stress. Each day the cells on a convergent or transform
-- boundary gather stress by the boundary's strength; past a threshold of its own (noisy) a cell lets
-- it go: a quake there with a magnitude. Every village map within reach feels it by distance: the
-- ground shakes (everyone notices), buildings are damaged by how they are built and how sound they are
-- - cracked, or fallen in on whoever is inside (hurt, trapped until dug out). What follows - running
-- out, gathering the children, digging, tending, lodging the homeless with kin, building again - is
-- the people's own (think's urgent choice, people.hurt, migrate.lodge, settle's house planning).
--   W.S.v3.quake = { cells = { boundary cells }, stress = { }, thr = { }, last = { } }
--   p.trapped = { b (the fallen building), left (hours of digging still needed), t (since) }
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local function state(W)
  local V = W.S.v3
  local Q = V.quake
  if Q then return Q end
  Q = { cells = {}, stress = {}, thr = {}, last = {} }
  local st = W.static
  for c = 1, W.n do
    local b = st.boundary and st.boundary[c] or 0
    if b == 1 or b == 3 then Q.cells[#Q.cells + 1] = c end
  end
  V.quake = Q
  return Q
end

local function u(W, a, b, k)
  return rng.float(W.seed, rng.S.V3_CONFLICT, a % 4294967296, b % 4294967296, 7000 + (k or 0))
end

-- A cell's threshold, drawn anew after each quake (noisy: some let go sooner, some later).
local function threshold(W, Q, i, day)
  return C().quake_thr * (0.6 + 0.8 * u(W, Q.cells[i], day, 1))
end

-- Daily, at the planet's step: stress gathers on the boundaries; a cell past its threshold quakes.
function M.daily(W, day)
  local c = C()
  local Q = state(W)
  local st = W.static
  for i = 1, #Q.cells do
    local cl = Q.cells[i]
    local rate = c.quake_rate * (st.bstrength and st.bstrength[cl] or 0.5) * ((st.boundary[cl] == 3) and 0.7 or 1)
    -- (a cell's stress when first looked at: somewhere below its own threshold - never a burst of
    -- quakes on a world's first day)
    Q.thr[i] = Q.thr[i] or threshold(W, Q, i, 0)
    Q.stress[i] = (Q.stress[i] or (u(W, cl, 0, 2) * 0.95 * Q.thr[i])) + rate
    if Q.stress[i] >= Q.thr[i] then
      local mag = c.quake_mag[1] + (c.quake_mag[2] - c.quake_mag[1]) * u(W, cl, day, 3) * u(W, cl, day, 4)
      Q.stress[i], Q.thr[i], Q.last[i] = 0, threshold(W, Q, i, day), day
      M.quake(W, cl, mag, day * 24 + 24 * u(W, cl, day, 5), nil)
    end
  end
end

-- The intensity a quake of magnitude `mag` at cell `at` is felt with at village s (0: not felt).
function M.felt(W, s, at, mag)
  local g = W.grid
  local dot = g.X[at] * g.X[s.cell] + g.Y[at] * g.Y[s.cell] + g.Z[at] * g.Z[s.cell]
  if dot > 1 then dot = 1 end
  local d = sqrt(max(0, 2 - 2 * dot)) / g.spacing    -- (in cell steps)
  return max(0, mag - C().quake_fall * d - C().quake_floor)
end

-- A quake: an event, and every village that feels it shaken; a strong one is followed by aftershocks.
function M.quake(W, at, mag, t, cause, after)
  local V = W.S.v3
  local c = C()
  local ev = 0
  if W.record_events then ev = require("sim.causality.events").emit(W, "quake", at, cause or 0, mag, { mag = mag }) end
  -- (S20) aftershocks: smaller quakes at the same place over the next days, most of them soon after
  -- (Omori); the buildings the first one cracked fall the more easily
  if not after and mag >= c.after_mag then
    local Q = state(W)
    Q.after = Q.after or {}
    local n = floor((mag - c.after_mag) * c.after_n) + 1
    for k = 1, n do
      local u1, u2 = u(W, at, floor(t * 60) % 4294967296, 10 + k), u(W, at, floor(t * 60) % 4294967296, 40 + k)
      Q.after[#Q.after + 1] = { cell = at, mag = mag - c.after_drop - 1.5 * u1, t = t + c.after_days * 24 * u2 * u2, cause = ev }
    end
  end
  -- (S20) under the sea, a strong one raises a wave (tsunami.lua, injected)
  if M.wave then M.wave(W, at, mag, t, ev) end
  for _, s in ipairs(V.settlements) do
    if s.alive then
      local I = M.felt(W, s, at, mag)
      if I > 0 then M.shake(W, s, I, t, ev, mag) end
    end
  end
end

-- (S20) Hourly: the aftershocks whose time has come.
function M.aftershocks(W, now)
  local Q = W.S.v3.quake
  local A = Q and Q.after
  if not A or #A == 0 then return end
  local i = 1
  while i <= #A do
    local a = A[i]
    if a.t <= now then
      table.remove(A, i)
      M.quake(W, a.cell, a.mag, a.t, a.cause, true)
    else
      i = i + 1
    end
  end
end

-- How a building stands a shaking: by what it is and how it is built (log walls bend; stone and heavy
-- stores do not; a hut is light; footings of stone steady a house).
local VULN = { house = 1.0, hut = 0.6, granary = 1.2, woodstore = 0.8, forge = 1.4, well = 0.5, dock = 0.6,
  fold = 0.4, byre = 0.6, sty = 0.4, saltpan = 0.3, clamp = 0.3, bridge = 0.9, camp = 0 }

-- Map s shaken with intensity I at time t: everyone feels it; buildings crack or fall in.
function M.shake(W, s, I, t, ev, mag)
  local c = C()
  local V = W.S.v3
  local k = I / c.quake_unit
  s.acc.quakes = (s.acc.quakes or 0) + 1
  local d0 = deeds.add(W, { kind = "quake", t = t, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0, to_s = s.id,
    what = sfmt("%.1f", mag or 0), n = 1, fu = 0, harm = false, answers = nil, event = ev })
  deeds.learn(W, s, d0, "the ground shook", 0, 0, t)
  -- (everyone on the map feels it: those indoors run out, parents go to their children - think)
  danger.each_on_map(W, s, function(p)
    if p.act ~= "carried" then
      danger.raise(W, p, { kind = "quake", src = 0, why = "The ground is shaking" }, p.x, p.y, t, "felt")
      -- (thrown down out of doors: a bruise)
      if p.inside == 0 and rng.float(W.seed, rng.S.V3_CONFLICT, p.key, floor(t * 60) % 4294967296, 7100) < c.quake_throw * k then
        people.hurt(p, c.quake_bruise, "bruise", d0.id, t)
      end
    end
  end)
  -- the buildings
  local fell = {}
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    local vu = b and b.state == "standing" and not b.natural and VULN[b.kind]
    if vu and vu > 0 then
      if b.footing then vu = vu * c.quake_footing end
      local dmg = k * vu * (1.3 - (b.cond or 1)) * (0.5 + rng.float(W.seed, rng.S.V3_CONFLICT, b.id, floor(t * 60) % 4294967296, 7200))
      if dmg >= c.quake_fall_in then fell[#fell + 1] = b
      elseif dmg >= c.quake_crack then
        b.cond = max(0.05, (b.cond or 1) - dmg * 0.5)
        s.acc.cracked = (s.acc.cracked or 0) + 1
      end
    end
  end
  for _, b in ipairs(fell) do M.fall_in(W, s, b, t, d0) end
end

-- (S20, R6.5d) The weight on the roofs (ash, snow): each roof's strength by what it is, how sound it
-- is and how it happens to be built (fixed for the building); a load past it brings the roof in on
-- whoever is inside, a load near it cracks it (once for each heavier load).
local ROOF = { house = 1.0, hut = 1.25, granary = 0.9, woodstore = 1.1, forge = 1.2, byre = 1.0 }

function M.roof_load(W, s, load, t, d0, cause)
  local c = C()
  local fell = {}
  for j = 1, #s.blist do
    local b = s.buildings[s.blist[j]]
    local k = b and b.state == "standing" and not b.natural and ROOF[b.kind]
    if k then
      local strength = k * (0.5 + 0.5 * (b.cond or 1)) * (0.7 + 0.6 * rng.float(W.seed, rng.S.V3_CONFLICT, b.id, 0, 7400))
      if load >= strength then fell[#fell + 1] = b
      elseif load >= c.roof_crack * strength and load > (b.strain or 0) + 0.1 then
        b.strain = load
        b.cond = max(0.05, (b.cond or 1) - 0.3 * load / strength)
        s.acc.cracked = (s.acc.cracked or 0) + 1
      end
    end
  end
  for _, b in ipairs(fell) do M.fall_in(W, s, b, t, d0, cause) end
end

local FELL = {
  quake = { how = "the quake", ours = "Our house fell in the quake", lodge = "their house fell in the quake" },
  ash = { how = "the weight of the ash", ours = "Our house fell in under the ash", lodge = "their house fell in under the ash" },
  snow = { how = "the weight of the snow", ours = "Our house fell in under the snow", lodge = "their house fell in under the snow" },
  sea = { how = "the sea", ours = "The sea took our house", lodge = "the sea took their house" },
}

local STORES = { "food", "grain", "sheaves", "smoked", "cheese", "meat", "fish", "meals", "logs", "wool", "cloth", "hides",
  "leather", "flax", "hay", "salt", "water", "seed" }

-- Building b falls in: whoever is inside is hurt, the badly hurt trapped under it until dug out; a
-- ruin; its household lodges with others (as after a fire) and builds again.
function M.fall_in(W, s, b, t, d0, cause)
  local c = C()
  local V = W.S.v3
  local FT = FELL[cause or "quake"]
  local d = deeds.add(W, { kind = "fell", t = t, at = s.id, x = b.cx, y = b.cy, by = 0, by_hh = 0, by_s = 0, to = 0,
    to_hh = b.hh or 0, to_s = s.id, what = b.kind, n = 1, fu = b.kind == "house" and c.house_fu or 20, harm = true,
    answers = d0 and d0.id or nil })
  deeds.learn(W, s, d, FT.how, 0, 0, t)
  s.acc.fell = (s.acc.fell or 0) + 1
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive and p.inside == b.id and not p.at then
      local sev = c.quake_hurt[1] + (c.quake_hurt[2] - c.quake_hurt[1]) * rng.float(W.seed, rng.S.V3_CONFLICT, p.key, floor(t * 60) % 4294967296, 7300)
      people.hurt(p, sev, "crushed", d.id, t)
      p.inside = 0
      p.x, p.y = b.cx or b.dx, b.cy or b.dy
      if sev >= c.quake_trap and p.alive then
        p.trapped = { b = b.id, left = c.dig_h * sev, t = t, x = p.x, y = p.y }
        people.remember(p, t, "The house fell on us: trapped under it")
        s.trapped = s.trapped or {}
        s.trapped[#s.trapped + 1] = p.id
        if danger.interrupt then danger.interrupt(W, p, t + 0.01, "Trapped under the fallen house") end
      else
        people.remember(p, t, "The house fell in on us")
      end
      if (p.hurt or 0) >= 1 and M.kill then M.kill(W, s, p, "crushed", t) end
    end
  end
  -- (what was in it: half under the rubble)
  for _, key in ipairs(STORES) do
    local q = b.stock and b.stock[key]
    if q and q > 0 then b.stock[key] = q * 0.5 end
  end
  local hh = V.households[b.hh or 0]
  s.scars = s.scars or {}
  s.scars[#s.scars + 1] = { x = b.cx or b.dx, y = b.cy or b.dy, t = t, kind = b.kind, cause = cause or "quake" }
  require("sim.agents.settle").remove_building(W, s, b)
  if hh and #hh.members > 0 and b.kind == "house" then
    hh.home = 0
    for _, id in ipairs(hh.members) do
      local q = V.people[id]
      if q and q.alive and people.stage(q, t) ~= "infant" then people.remember(q, t, FT.ours) end
    end
    if M.lodge then M.lodge(W, s, hh, t, FT.lodge) end
  end
end

-- The trapped of village s still under the rubble (and those freed or dead taken off the list).
function M.trapped(W, s)
  local T = s.trapped
  if not T then return nil end
  local V = W.S.v3
  for i = #T, 1, -1 do
    local p = V.people[T[i]]
    if not (p and p.alive and p.trapped) then table.remove(T, i) end
  end
  return #T > 0 and T or nil
end

-- A stint of digging at p's side: hours off what is left; freed at nothing.
function M.dig(W, s, digger, p, hours, now)
  local tr = p.trapped
  if not tr then return end
  tr.left = tr.left - hours
  if tr.left <= 0 then
    p.trapped = nil
    people.remember(p, now, sfmt("Dug out of the rubble by %s", digger.name))
    people.remember(digger, now, sfmt("Dug %s out of the rubble", p.name))
    s.acc.dug_out = (s.acc.dug_out or 0) + 1
    if danger.interrupt then danger.interrupt(W, p, now + 0.01, "Dug out") end
  end
end

-- Daily for each village: the trapped weaken under the rubble (untended, the badly hurt may die).
function M.trapped_daily(W, s, now)
  local T = M.trapped(W, s)
  if not T then return end
  local c = C()
  local V = W.S.v3
  for _, id in ipairs(T) do
    local p = V.people[id]
    if p and p.alive and p.trapped and now - p.trapped.t > c.trapped_h then
      people.hurt(p, c.trapped_worse, "crushed", p.hurt_deed, now)
      if (p.hurt or 0) >= 1 and M.kill then M.kill(W, s, p, "crushed", now) end
    end
  end
end

return M
