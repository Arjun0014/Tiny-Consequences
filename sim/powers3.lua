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

M.KINDS = { "lightning", "rain", "dry", "warm", "cold", "stir", "strain", "cull", "bring" }
local ROOT = { lightning = "power_lightning", rain = "power_rain", dry = "power_drought", warm = "power_heat", cold = "power_chill",
  stir = "power_volcano", strain = "power_quake", cull = "power_cull", bring = "power_introduce" }

local function root(W, kind, cell, mag, data)
  return events.emit(W, ROOT[kind], cell, 0, mag or 1, data)
end

local powers = require("sim.powers")
local function area(W, c, steps) return powers.area(W, c, steps) end

local function forcing(W, kind, cells, amount, tag, days)
  W.S.powers = W.S.powers or { forcings = {}, blight = {} }
  local fs = W.S.powers.forcings
  fs[#fs + 1] = { kind = kind, cells = cells, amount = amount, tag = tag, from = W.tick, to = W.tick + days }
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
  forcing(W, "rain", cells, 1, ev, c.v3_rain_days)
  return ev
end

function P.dry(W, p)
  local c = C()
  local cells = area(W, p.cell, c.radius)
  local ev = root(W, "dry", p.cell, #cells)
  forcing(W, "drought", cells, 1, ev, c.v3_dry_days)
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
  local ev = root(W, "cull", p.cell, #cells, { species = sp })
  require("sim.systems.fauna").kill_in(W, cells, sp, c.cull_fraction, ev)
  -- (those on a village's map in the area die where they stand)
  local set = {}
  for _, cl in ipairs(cells) do set[cl] = true end
  local mapm = require("sim.agents.map")
  local rng = require("sim.rng")
  local now = W.S.v3.now or W.hour
  for _, s in ipairs(W.S.v3.settlements) do
    local Wd = s.wildlife
    for _, w in ipairs(Wd and Wd.animals or {}) do
      if w.alive and not w.gone and (w.pred == (sp == "predators")) then
        local i = mapm.tile_of(s, w.tx or w.x, w.ty or w.y)
        if i > 0 and set[s.cell_of[i]] and rng.float(W.seed, rng.S.CULL, w.k, floor(now), 77) < c.cull_fraction then
          w.alive, w.died, w.cause, w.ver = false, now, "cull", (w.ver or 0) + 1
          Wd.carcasses = Wd.carcasses or {}
          Wd.carcasses[#Wd.carcasses + 1] = { x = w.tx or w.x, y = w.ty or w.y, sp = w.sp, t = now, meat = 0, by = "cull", ev = ev }
          s.acc.culled_wild = (s.acc.culled_wild or 0) + 1
        end
      end
    end
  end
  return ev
end

function P.bring(W, p)
  local sp = (p.species == "wolves" and "predators") or (p.species == "fish" and "fish") or "grazers"
  -- (the v2 power's own way: herds or packs of the landscape's kind, a shoal on the sea)
  return powers.apply(W, { power = "introduce", cell = p.cell, species = sp })
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
