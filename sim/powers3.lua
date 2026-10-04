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
-- (S23: the beast the sickness is sent into and its herd - reads only; the cast and the aim preview share it)
local function sick_target(W, s, p, now)
  local stock = require("sim.agents.stock")
  local best, bd, bH
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and (a.sick or 0) == 0 and (a.sick_inc or 0) == 0 then
        local x, y = stock.pos(a, now)
        local d = p.x and ((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y)) or -#H.animals
        if not best or d < bd then best, bd, bH = a, d, H end
      end
    end
  end
  return best, bH
end

function P.sickness(W, p)
  local s = village_at(W, p)
  if not s or not s.herds then return 0 end
  local stock = require("sim.agents.stock")
  local now = W.S.v3.now or W.hour
  local best, bH = sick_target(W, s, p, now)
  if not best then return 0 end
  local ev = root(W, "sickness", s.cell, C().v3_sick_n, { settlement = s.id, species = best.sp, x = p.x, y = p.y })
  -- (the one aimed at and the nearest of its herd to it)
  local bx, by = stock.pos(best, now)
  local cand = {}
  for _, a in ipairs(bH.animals) do
    if a.alive and a ~= best and (a.sick or 0) == 0 and (a.sick_inc or 0) == 0 then
      local x, y = stock.pos(a, now)
      cand[#cand + 1] = { a, (x - bx) * (x - bx) + (y - by) * (y - by) }
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

-- Meteor (S24, D-188): one rock with one energy - nothing but its physics. Its root event; dust in the sky (the impact
-- winter); the ground shaken (the quake of the impact, felt by distance like any quake); the wave if it falls in the
-- sea; on every village's map within reach of the point, the impact itself (sim/agents/impact.lua: the crater, the
-- blast, the flash, by distance and by what each thing is); on the land about it that no village's map owns, the
-- flash setting the dry cover alight and the blast killing the wild herds and packs by the share of their ground it
-- reaches. A cell a village's map owns burns by that map's own fire (D-190): the planet shows what burns there.
-- Where it fell is kept (W.S.powers.craters): the view draws the crater on the planet.
local function unit_of(W, p)
  local V = W.S.v3
  local s = p.sid and V.settlements[p.sid]
  if s and p.x then return require("sim.agents.map").unit(s, p.x, p.y) end
  local g = W.grid
  return g.X[p.cell], g.Y[p.cell], g.Z[p.cell]
end

function P.meteor(W, p)
  local c = C()
  local cv = cfgmod.V3
  local V = W.S.v3
  local now = V.now or W.hour
  local mapm = require("sim.agents.map")
  local ev = root(W, "meteor", p.cell, c.meteor_aerosol)
  W.G.aerosol = (W.G.aerosol or 0) + c.meteor_aerosol
  require("sim.ledger").add(W, "aerosol", c.meteor_aerosol, ev)
  local ux, uy, uz = unit_of(W, p)
  W.S.powers = W.S.powers or { forcings = {}, blight = {} }
  local PS = W.S.powers
  PS.craters = PS.craters or {}
  PS.craters[#PS.craters + 1] = { cell = p.cell, x = ux, y = uy, z = uz, t = now, ev = ev, sea = W.geo.ocean[p.cell] == 1 }
  -- the impact on the maps within reach of the point (each map's own coordinates of it)
  local reach = cv.imp_flash * 2.2
  if W.geo.ocean[p.cell] == 0 then
    local impact = require("sim.agents.impact")
    for _, s in ipairs(V.settlements) do
      if s.alive and s.land then
        local x, y = mapm.local_of(s, ux, uy, uz)
        local half = s.N * s.T * 0.5
        if x and math.abs(x) < half + reach and math.abs(y) < half + reach then impact.strike(W, s, x, y, now, ev) end
      end
    end
  end
  -- the land about it the maps do not own: the flash on its cover, the blast on its wild herds and packs
  local O = mapm.owners(W)
  local g = W.grid
  local fire = require("sim.systems.fire")
  local fauna = require("sim.systems.fauna")
  local R = mapm.R
  for _, cl in ipairs(area(W, p.cell, 1)) do
    if W.geo.ocean[cl] == 0 and not (O and O.by[cl]) then
      local dot = g.X[cl] * ux + g.Y[cl] * uy + g.Z[cl] * uz
      local d = math.sqrt(max(0, 2 - 2 * min(1, dot))) * R
      -- (the share of the cell within the flash's reach and within the blast's lethal reach, by the distance of its
      -- middle: a cell is ~0.93 across)
      local lit = max(0, min(1, (reach + 0.47 - d) / 0.94))
      local dry = max(0, 1 - (W.F.soil_water[cl] or 0.5) / cfgmod.HYDRO.field_capacity)
      if lit > 0 and require("sim.rng").float(W.seed, require("sim.rng").S.METEOR, cl, floor(now), 3) < lit * (0.3 + dry) then
        fire.ignite(W, cl, ev)
      end
      local slain = max(0, min(1, (cv.imp_blast * 1.4 + 0.47 - d) / 0.94)) * 0.9
      if slain > 0 then
        fauna.kill_in(W, { cl }, "grazers", slain, ev)
        fauna.kill_in(W, { cl }, "predators", slain, ev)
      end
    end
  end
  -- the ground shaken; in the sea, the wave
  local shake = require("sim.agents.shake")
  shake.quake(W, p.cell, c.v3_meteor_mag, now, ev)
  if W.geo.ocean[p.cell] == 1 then require("sim.agents.tsunami").raise(W, p.cell, c.v3_meteor_wave, now, ev) end
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

-- Previews (S23) ------------------------------------------------------------------------------------------
-- What a cast at (cell, o = { sid, x, y, mode, species }) would act on, read from the world by the same choices
-- the cast makes: the thing the bolt takes, the herd and the beast, the field, the cells and the villages under
-- them, the mountain and its pressure, the fault, the wild animals there, the buildings in the blast. For the HUD
-- while aiming (game/ui/hud.gui_script); reads only, never writes. One line, or nil.
local sfmt = string.format

local function under(W, cells)
  local set, out = {}, {}
  for _, cl in ipairs(cells) do set[cl] = true end
  for _, s in ipairs(W.S.v3.settlements) do if s.alive and set[s.cell] then out[#out + 1] = s.name end end
  if #out == 0 then return nil end
  if #out > 3 then return sfmt("%s, %s and %d more", out[1], out[2], #out - 2) end
  if #out == 1 then return out[1] end
  return table.concat(out, ", ", 1, #out - 1) .. " and " .. out[#out]
end

local function surname_of(W, hid)
  local hh = W.S.v3.households[hid or 0]
  local q = hh and W.S.v3.people[hh.members[1] or 0]
  return q and q.surname or nil
end

local SP_NAME = { sheep = "sheep", goat = "goats", cattle = "cattle", pig = "pigs" }

local function strike_words(W, s, best, bk)
  local V = W.S.v3
  if bk == "tree" then return "a tree" end
  if bk == "bld" then
    local b = s.buildings[best]
    if not b then return "a roof" end
    local sn = (b.kind == "house" or b.kind == "hut") and surname_of(W, b.hh)
    return sn and ("the " .. sn .. " house") or ("the " .. (b.label or b.kind))
  end
  if bk == "person" then
    local q = V.people[best]
    return q and sfmt("%s %s, out of doors", q.name, q.surname) or "someone out of doors"
  end
  if bk == "beast" then
    local sn = surname_of(W, best.owner)
    return sn and sfmt("one of the %ss' %s", sn, SP_NAME[best.sp] or best.sp) or ("a " .. tostring(best.sp))
  end
  if bk == "wild" then return "a wild " .. tostring(best.sp) end
  if bk == "dog" then return "a dog" end
  return "the bare ground"
end

local PREVIEW = {}

function PREVIEW.lightning(W, cell, o)
  local s = o.sid and W.S.v3.settlements[o.sid]
  if not (s and o.x) then return "A bolt on the land here - close in on a village to aim it at a point" end
  local best, bk = require("sim.agents.blaze").strike_target(W, s, o.x, o.y, W.S.v3.now or W.hour, true)
  return "The bolt would take " .. strike_words(W, s, best, bk)
end

local function weather(W, cell, what)
  local vs = under(W, area(W, cell, C().radius))
  return what .. (vs and (" over " .. vs) or " over open country - no village under it")
end
function PREVIEW.rain(W, cell) return weather(W, cell, sfmt("Rain for %d days", C().v3_rain_days)) end
function PREVIEW.dry(W, cell) return weather(W, cell, sfmt("No rain for %d days", C().v3_dry_days)) end
function PREVIEW.warm(W, cell) return weather(W, cell, sfmt("The air %d degrees warmer for %d days", C().v3_air_k, C().v3_air_days)) end
function PREVIEW.cold(W, cell) return weather(W, cell, sfmt("The air %d degrees colder for %d days", C().v3_air_k, C().v3_air_days)) end

function PREVIEW.stir(W, cell)
  local best, bd = nil, -2
  local VS = W.S.v3.volc   -- (never volcano.list: it makes the list when there is none - the view only reads)
  for _, v in ipairs(VS and VS.list or {}) do
    local d = dot(W, v.cell, cell)
    if d > bd then best, bd = v, d end
  end
  if not best then return "The nearest mountain's pressure would rise" end
  local after = (best.pressure + C().v3_stir_k * best.thr) / math.max(1e-6, best.thr)
  return sfmt("%s: its pressure to %d%% of what makes it erupt%s", best.name or "The mountain", floor(after * 100 + 0.5),
    after >= 1 and " - it would erupt" or "")
end

function PREVIEW.strain(W, cell)
  local Q = W.S.v3.quake   -- (never shake.state: it makes the state when there is none)
  if not (Q and Q.cells and #Q.cells > 0) then return "The nearest fault's stress would rise" end
  local best, bd = nil, -2
  for i = 1, #Q.cells do
    local d = dot(W, Q.cells[i], cell)
    if d > bd then best, bd = i, d end
  end
  local st, thr = Q.stress[best], Q.thr[best]
  if not (st and thr) then return "The fault here: its stress would rise" end
  local after = (st + C().v3_strain_k * thr) / math.max(1e-6, thr)
  return sfmt("The fault here: its stress to %d%% of what makes it give%s", floor(after * 100 + 0.5), after >= 1 and " - it would give" or "")
end

function PREVIEW.cull(W, cell, o)
  local pred = o.species ~= "deer"
  local cells = area(W, cell, C().radius)
  local set = {}
  for _, cl in ipairs(cells) do set[cl] = true end
  local n = 0
  local mapm = require("sim.agents.map")
  for _, s in ipairs(W.S.v3.settlements) do
    for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
      if w.alive and not w.gone and (w.pred == pred) then
        local i = mapm.tile_of(s, w.tx or w.x, w.ty or w.y)
        if i > 0 and set[s.cell_of[i]] then n = n + 1 end
      end
    end
  end
  local F = W.F[pred and "predators" or "grazers"]
  local fn = 0
  if F then for _, cl in ipairs(cells) do fn = fn + (F[cl] or 0) end end
  local what = pred and "wolves" or "deer"
  if n == 0 and fn <= 0 then return sfmt("No %s here to cull", what) end
  return sfmt("Nine in ten of the %s here%s", what, n > 0 and sfmt(" - %d of them by the villages", n) or "")
end

function PREVIEW.bring(W, cell, o)
  local sea = W.geo.ocean[cell] == 1
  if o.species == "fish" then return sea and "A shoal of fish here" or "Fish need the sea" end
  if sea then return "Wolves and deer need land" end
  return o.species == "wolves" and "A pack of wolves on this land" or "A herd of deer on this land"
end

function PREVIEW.sickness(W, cell, o)
  local s = village_at(W, { cell = cell, sid = o.sid })
  if not (s and s.herds) then return "No village's herds here" end
  local best, H = sick_target(W, s, o, W.S.v3.now or W.hour)
  if not best then return "No healthy beast here to fall sick" end
  local n = 0
  for _, a in ipairs(H.animals) do if a.alive then n = n + 1 end end
  local sn = surname_of(W, best.owner)
  return sfmt("The %s of %s (%d in the herd): %s and the %d nearest it", SP_NAME[best.sp] or best.sp, s.name, n,
    sn and ("one of the " .. sn .. "s'") or "one", C().v3_sick_n - 1)
end

function PREVIEW.blight(W, cell, o)
  local s = village_at(W, { cell = cell, sid = o.sid })
  if not s then return "No village's fields here" end
  local pl = require("sim.agents.blight").target(s, o.x, o.y)
  if not pl then return sfmt("No growing field at %s", s.name) end
  return sfmt("%s at %s (%s)", pl.name or "A field", s.name, pl.stage)
end

function PREVIEW.sea(W, cell, o)
  local n = 0
  for _, cl in ipairs(area(W, cell, C().radius)) do if W.geo.ocean[cl] == 1 then n = n + 1 end end
  if n == 0 then return "No sea here" end
  return sfmt("The sea here %d degrees %s for %d days", C().v3_sea_k, o.mode == "cool" and "cooler" or "warmer", C().v3_sea_days)
end

function PREVIEW.oil(W, cell)
  return W.geo.ocean[cell] == 1 and "A slick on the sea here - the currents will carry it" or "Oil needs the sea"
end

function PREVIEW.meteor(W, cell, o)
  local s = village_at(W, { cell = cell, sid = o.sid })
  local c = C()
  if s and s.land then
    local x, y = o.x or s.sqx or 0, o.y or s.sqy or 0
    local nb, np = 0, 0
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      local bx, by = b.cx or b.dx or 0, b.cy or b.dy or 0
      if b.state == "standing" and not b.natural and (bx - x) * (bx - x) + (by - y) * (by - y) < c.v3_meteor_fire * c.v3_meteor_fire then nb = nb + 1 end
    end
    for _, id in ipairs(s.members) do
      local q = W.S.v3.people[id]
      if q and q.alive and not q.at and (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y) < c.v3_meteor_blast * c.v3_meteor_blast then np = np + 1 end
    end
    return sfmt("At %s: %d buildings in the fire's reach, %d people in the blast; the ground shaken", s.name, nb, np)
  end
  if W.geo.ocean[cell] == 1 then return "Into the sea: the ground shaken and a wave" end
  return "On open land: fire, the ground shaken, dust in the air"
end

function M.preview(W, kind, cell, o)
  local f = PREVIEW[kind]
  if not (f and W.v3 and W.S.v3 and cell and cell >= 1 and cell <= W.n) then return nil end
  return f(W, cell, o or {})
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
