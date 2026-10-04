-- God powers on the v3 world (R7 v1, S21, D-174; docs/v3/70_R7_GOD_POWERS.md §3). Each writes physical state
-- through functions the world already has, with the power's root event (actor `player`) as its cause - and
-- nothing else. Who is struck, what burns, whose field drowns, who leaves, who helps: the world's own.
--   lightning  a bolt at a point of a village's map (p.sid, p.x, p.y) - the tallest thing near takes it - or
--              on the planet cell (a dry one may catch)
--   rain       a cloudburst over the cells about the point for `rain_days`
--   dry        no rain and a little heat over them for `dry_days`
--   warm, cold warm or cold air over them for `air_days` (heat_anom), fading after
--   stir       the nearest volcano's pressure raised by `stir_k` of its threshold (it erupts when it does)
--   strain     the nearest plate boundary's stress raised by `strain_k` of its threshold (it gives when it does)
--   cull       wolves or deer thinned on the cells (the planet's packs and herds; those on a village's map in
--              the area die where they stand)
--   bring      a pack of wolves, a herd of deer or a shoal of fish on the cell
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local blame = require("sim.causality.blame")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.POWERS end

M.KINDS = { "lightning", "rain", "dry", "warm", "cold", "stir", "strain", "cull", "bring", "sickness", "sea", "oil", "meteor", "blight" }
local ROOT = { lightning = "power_lightning", rain = "power_rain", dry = "power_drought", warm = "power_heat", cold = "power_chill",
  stir = "power_volcano", strain = "power_quake", cull = "power_cull", bring = "power_introduce", sickness = "power_sickness",
  sea = "power_sea", oil = "power_oil", meteor = "power_meteor", blight = "power_blight" }

local function root(W, kind, cell, mag, data)
  return events.emit(W, ROOT[kind], cell, 0, mag or 1, data)
end

local powers = require("sim.powers")
local function area(W, c, steps) return powers.area(W, c, steps) end

local function forcing(W, kind, cells, amount, tag, days)
  W.S.powers = W.S.powers or { forcings = {}, blight = {} }
  local fs = W.S.powers.forcings
  local f = { kind = kind, cells = cells, amount = amount, tag = tag, from = W.tick, to = W.tick + days }
  fs[#fs + 1] = f
  return f
end

-- (S22) The villages whose cells lie in `cells` (ids, ascending), and the moment of the cast: rain or a dry
-- spell begins over them when it is cast (sim/agents/weather.lua), not at the planet's next day.
local function at_villages(W, f, cells)
  local set = {}
  for _, c in ipairs(cells) do set[c] = true end
  local ids = {}
  for _, s in ipairs(W.S.v3.settlements) do if s.alive and set[s.cell] then ids[#ids + 1] = s.id end end
  f.at, f.sids = W.S.v3.now or W.hour, ids
  for _, s in ipairs(W.S.v3.settlements) do s.wx = nil end
end

local P = {}

function P.lightning(W, p)
  local V = W.S.v3
  local s = p.sid and V.settlements[p.sid]
  local ev = root(W, "lightning", s and s.cell or p.cell, 1, { settlement = s and s.id, x = p.x, y = p.y })
  if s and p.x then
    require("sim.agents.blaze").strike(W, s, p.x, p.y, V.now or W.hour, nil, ev)
    return ev
  end
  -- (on the planet: heat where it lands; a dry cell may catch)
  local c = p.cell
  local F = W.F
  if W.geo.ocean[c] == 0 and (F.soil_water[c] or 1) < 0.4 and (F.forest[c] + F.grass[c]) > 0.2 then
    require("sim.systems.fire").ignite(W, c, ev)
  end
  return ev
end

function P.rain(W, p)
  local c = C()
  local cells = area(W, p.cell, c.radius)
  local ev = root(W, "rain", p.cell, #cells)
  at_villages(W, forcing(W, "rain", cells, 1, ev, c.v3_rain_days), cells)
  return ev
end

function P.dry(W, p)
  local c = C()
  local cells = area(W, p.cell, c.radius)
  local ev = root(W, "dry", p.cell, #cells)
  at_villages(W, forcing(W, "drought", cells, 1, ev, c.v3_dry_days), cells)
  return ev
end

local function air(W, p, sign)
  local c = C()
  local cells = area(W, p.cell, c.radius)
  local ev = root(W, sign > 0 and "warm" or "cold", p.cell, c.v3_air_k)
  forcing(W, sign > 0 and "heat" or "chill", cells, sign * c.v3_air_k, ev, c.v3_air_days)
  -- (the air is changed now: the villages' weather today is redrawn from it)
  local F = W.F
  for _, cl in ipairs(cells) do
    local a = (W.geo.ocean[cl] == 1 and c.sea_factor or 1) * sign * c.v3_air_k
    if (a > 0 and F.heat_anom[cl] < a) or (a < 0 and F.heat_anom[cl] > a) then
      F.temp[cl] = F.temp[cl] + (a - F.heat_anom[cl])
      F.heat_anom[cl] = a
      blame.set(W.C.heat_anom, W.I.heat_anom, cl, ev)
    end
  end
  for _, q in ipairs(W.S.v3.settlements) do q.wx = nil end
  return ev
end
function P.warm(W, p) return air(W, p, 1) end
function P.cold(W, p) return air(W, p, -1) end

local function dot(W, a, b)
  local g = W.grid
  return g.X[a] * g.X[b] + g.Y[a] * g.Y[b] + g.Z[a] * g.Z[b]
end

function P.stir(W, p)
  local volcano = require("sim.agents.volcano")
  local best, bd = nil, -2
  for _, v in ipairs(volcano.list(W)) do
    local d = dot(W, v.cell, p.cell)
    if d > bd then best, bd = v, d end
  end
  if not best then return 0 end
  local ev = root(W, "stir", best.cell, 1, { name = best.name })
  best.pressure = best.pressure + C().v3_stir_k * best.thr
  best.cause = ev
  return ev
end

function P.strain(W, p)
  local shake = require("sim.agents.shake")
  local Q = shake.state(W)
  local best, bd = nil, -2
  for i = 1, #Q.cells do
    local d = dot(W, Q.cells[i], p.cell)
    if d > bd then best, bd = i, d end
  end
  if not best then return 0 end
  local cl = Q.cells[best]
  local ev = root(W, "strain", cl, 1)
  shake.init_cell(W, Q, best)
  Q.stress[best] = Q.stress[best] + C().v3_strain_k * Q.thr[best]
  Q.cause = Q.cause or {}
  Q.cause[best] = ev
  return ev
end

function P.cull(W, p)
  local c = C()
  local sp = p.species == "deer" and "grazers" or "predators"
  local cells = area(W, p.cell, c.radius)
  local data = { species = sp }
  local ev = root(W, "cull", p.cell, #cells, data)
  -- (S23) what the cull did, kept on its own event (the chain of what it caused starts there)
  data.killed = require("sim.systems.fauna").kill_in(W, cells, sp, c.cull_fraction, ev)
  -- (those on a village's map in the area die where they stand)
  local set = {}
  for _, cl in ipairs(cells) do set[cl] = true end
  local mapm = require("sim.agents.map")
  local rng = require("sim.rng")
  local deeds = require("sim.agents.deeds")
  local now = W.S.v3.now or W.hour
  for _, s in ipairs(W.S.v3.settlements) do
    local Wd = s.wildlife
    local n, what, sx, sy = 0, nil, 0, 0
    for _, w in ipairs(Wd and Wd.animals or {}) do
      if w.alive and not w.gone and (w.pred == (sp == "predators")) then
        local i = mapm.tile_of(s, w.tx or w.x, w.ty or w.y)
        if i > 0 and set[s.cell_of[i]] and rng.float(W.seed, rng.S.CULL, w.k, floor(now), 77) < c.cull_fraction then
          w.alive, w.died, w.cause, w.ver = false, now, "cull", (w.ver or 0) + 1
          Wd.carcasses = Wd.carcasses or {}
          Wd.carcasses[#Wd.carcasses + 1] = { x = w.tx or w.x, y = w.ty or w.y, sp = w.sp, t = now, meat = 0, by = "cull", ev = ev }
          s.acc.culled_wild = (s.acc.culled_wild or 0) + 1
          n, what, sx, sy = n + 1, w.sp, sx + (w.tx or w.x), sy + (w.ty or w.y)
        end
      end
    end
    -- (S22) the dead on this map: a deed of the cull (what the people there find, and the news)
    if n > 0 then
      deeds.add(W, { kind = "culled", t = now, at = s.id, x = sx / n, y = sy / n, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
        to_s = s.id, what = what, n = n, fu = 0, harm = false, event = ev })
    end
  end
  return ev
end

function P.bring(W, p)
  local sp = (p.species == "wolves" and "predators") or (p.species == "fish" and "fish") or "grazers"
  -- (the v2 power's own way: herds or packs of the landscape's kind, a shoal on the sea)
  return powers.apply(W, { power = "introduce", cell = p.cell, species = sp })
end

-- (S23, R7 v2) The village a cast is about: the one whose map holds the point, else the nearest within a
-- few cells of the aimed cell.
local function village_at(W, p)
  local V = W.S.v3
  local s = p.sid and V.settlements[p.sid]
  if s then return s end
  local best, bd = nil, -2
  for _, o in ipairs(V.settlements) do
    if o.alive then
      local d = dot(W, o.cell, p.cell)
      if d > bd then best, bd = o, d end
    end
  end
  return bd > 0.95 and best or nil
end

-- Sickness in a herd: a few beasts of the herd nearest the point (else the village's biggest herd) catch a
-- sickness the god sends. They carry it unseen for a day or two, then show it; the herd's own sickness does the
-- rest - spread in the pen by the share sick, crowding, wet and hunger; beasts sold or droven carry it to
-- other herds; the dead are their owners' loss. The cause travels with the sickness (stock.lua `sick_ev`).
function P.sickness(W, p)
  local s = village_at(W, p)
  if not s or not s.herds then return 0 end
  local stock = require("sim.agents.stock")
  local now = W.S.v3.now or W.hour
  local best, bd, bH
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and (a.sick or 0) == 0 and (a.sick_inc or 0) == 0 then
        local x, y = stock.pos(a, now)
        local d = p.x and ((x - p.x) ^ 2 + (y - p.y) ^ 2) or -#H.animals
        if not best or d < bd then best, bd, bH = a, d, H end
      end
    end
  end
  if not best then return 0 end
  local ev = root(W, "sickness", s.cell, C().v3_sick_n, { settlement = s.id, species = best.sp, x = p.x, y = p.y })
  -- (the one aimed at and the nearest of its herd to it)
  local bx, by = stock.pos(best, now)
  local cand = {}
  for _, a in ipairs(bH.animals) do
    if a.alive and a ~= best and (a.sick or 0) == 0 and (a.sick_inc or 0) == 0 then
      local x, y = stock.pos(a, now)
      cand[#cand + 1] = { a, (x - bx) ^ 2 + (y - by) ^ 2 }
    end
  end
  table.sort(cand, function(u1, u2) if u1[2] ~= u2[2] then return u1[2] < u2[2] end return u1[1].k < u2[1].k end)
  local list = { best }
  for k = 1, math.min(#cand, C().v3_sick_n - 1) do list[#list + 1] = cand[k][1] end
  for k, a in ipairs(list) do a.sick_inc, a.sick_ev = k == 1 and 1 or 2, ev end
  return ev
end

-- Warm / cool sea: the water about the point warmer or cooler for `v3_sea_days` - the sea cells only (the
-- surface follows the forced air at once, sim/powers.lua); the plankton and the fish answer by their own window
-- of warmth (ocean.lua), the fishers by what they find, the gulls by the fish.
function P.sea(W, p)
  local c = C()
  local sign = p.mode == "cool" and -1 or 1
  local cells = {}
  for _, cl in ipairs(area(W, p.cell, c.radius)) do if W.geo.ocean[cl] == 1 then cells[#cells + 1] = cl end end
  if #cells == 0 then return 0 end
  local ev = root(W, "sea", p.cell, sign * c.v3_sea_k, { mode = sign > 0 and "warm" or "cool" })
  forcing(W, sign > 0 and "heat" or "chill", cells, sign * c.v3_sea_k / c.sea_factor, ev, c.v3_sea_days)
  return ev
end

-- Oil on the sea: the planet's own slick (sim/powers.lua: spilled where aimed, or a tanker sunk; currents carry
-- it, it kills the fish, it comes ashore).
function P.oil(W, p)
  return powers.apply(W, { power = "oil", cell = p.cell, ship = p.ship })
end

-- Meteor: a compound physical shock and nothing more. On the planet (the v2 meteor's): heat and fire on the land
-- about the impact, the wild beasts in the blast killed, sulphur in the sky (the impact winter). On the ground: the
-- earth shaken (a quake of the impact, felt by distance like any quake), a wave if it falls in the sea. On a
-- village's map near it: the blast at the point - roofs and trees alight, those near it thrown down and burnt,
-- what stands nearest knocked down - all through the systems that answer lightning, fire and quakes.
function P.meteor(W, p)
  local c = C()
  local V = W.S.v3
  local now = V.now or W.hour
  -- the planet's part: the v2 meteor without its v2 villages (none in a v3 world)
  local ev = powers.apply(W, { power = "meteor", cell = p.cell })
  local shake = require("sim.agents.shake")
  shake.quake(W, p.cell, c.v3_meteor_mag, now, ev)
  -- (in the sea: the impact's own wave, stronger than the shaking's)
  if W.geo.ocean[p.cell] == 1 then require("sim.agents.tsunami").raise(W, p.cell, c.v3_meteor_wave, now, ev) end
  -- the blast on a map
  local s = village_at(W, p)
  if s and s.land then
    local x, y = p.x, p.y
    if not x then x, y = s.sqx or 0, s.sqy or 0 end
    local blaze = require("sim.agents.blaze")
    local deeds = require("sim.agents.deeds")
    local people = require("sim.agents.people")
    local d = deeds.add(W, { kind = "impact", t = now, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0,
      to_s = s.id, what = "meteor", n = 1, fu = 0, harm = true, event = ev })
    deeds.learn(W, s, d, "the fire from the sky", 0, 0, now)
    local R = c.v3_meteor_fire
    blaze.trees_near(s, x, y, R, function(k) blaze.ignite(W, s, "tree", k, now, d.id) end)
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      local bx, by = b.cx or b.dx or 0, b.cy or b.dy or 0
      local dd = math.sqrt((bx - x) ^ 2 + (by - y) ^ 2)
      if b.state == "standing" and not b.natural and dd < R then
        if dd < c.v3_meteor_blast * 0.6 then shake.fall_in(W, s, b, now, d, "quake") else blaze.ignite(W, s, "bld", id, now, d.id) end
      end
    end
    for _, id in ipairs(s.members) do
      local q = V.people[id]
      if q and q.alive and not q.at then
        local dd = math.sqrt((q.x - x) ^ 2 + (q.y - y) ^ 2)
        if dd < c.v3_meteor_blast then
          people.hurt(q, c.v3_meteor_hurt * (1 - dd / c.v3_meteor_blast), "burn", d.id, now)
          if (q.hurt or 0) >= 1 and blaze.kill then blaze.kill(W, s, q, "meteor", now) end
        end
      end
    end
  end
  return ev
end

-- Blight: the growing field nearest the point (else the village's largest) catches a plant sickness; the
-- weather, the wind, the harvest and the seed grain do the rest (sim/agents/blight.lua).
function P.blight(W, p)
  local s = village_at(W, p)
  if not s then return 0 end
  local ev = root(W, "blight", s.cell, 1, { settlement = s.id, x = p.x, y = p.y })
  if not require("sim.agents.blight").cast(W, s, p.x, p.y, ev) then return ev end
  return ev
end

function M.apply(W, p)
  local f = P[p.power]
  if not f then error("unknown v3 power " .. tostring(p.power)) end
  if not p.cell or p.cell < 1 or p.cell > W.n then error("power needs a cell") end
  return f(W, p)
end

function M.has(kind) return P[kind] ~= nil end

-- (the `power` command: the v3 powers in v3 worlds, the old ones otherwise - sim/powers.lua registers its
-- own first; this module wraps it, never the reverse: bob forbids circular requires)
do
  require("sim.powers")
  local world = require("sim.world")
  local base = world.COMMANDS.power
  world.COMMANDS.power = function(W, p)
    if W.v3 and P[p.power] then return M.apply(W, p) end
    return base(W, p)
  end
end

return M
