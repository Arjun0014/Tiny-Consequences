-- Player powers (04_PLAYER.md §2, Stage 2.1). Pure Lua 5.1.
--
-- A power is the logged command `power` { power = name, cell = c, mode, species, ship, person }
-- (world.command(W, "power", {...})), applied at the start of the next tick. Each use creates
-- a root event power_<kind> (actor player) and writes its id into the cause tags of every field
-- it changes, so every consequence chains back to the player. Powers are nudges: none of them
-- emits a detector-level consequence (famine, abandonment, extinction) itself.
--
--   lightning  cell [ship | person]  sinks a tanker/boat on the cell (oil), kills a targeted
--                                    villager, else ignites the cell if it has fuel
--   weather    cell, mode rain|drought  one season: deluge (forced rain) or drought (no rain,
--                                    +heat); radius 2 level-4 steps
--   temperature cell, mode heat|chill   one season: heat anomaly ±6 °C, radius 2
--   cull       cell, species grazers|predators|fish   removes 90% in radius 2
--   introduce  cell, species grazers|predators|fish|settlers   breeding population in radius 1,
--                                    or 5 settlers found a village (always available)
--   blight     cell (a farm)          infected farms lose their crop; spreads to adjacent farms
--                                    and, by trade, to villages within 6 steps; burns out after
--                                    two winters (sparse set, W.S.powers.blight)
--   oil        cell (sea) [ship]      an oil pulse (a tanker's whole cargo if one is there)
--   meteor     cell                   impact: fire around, deaths in villages near, animals
--                                    killed, impact-winter aerosol (tsunami: Chapter 3)
--
-- Sustained effects (weather, temperature, blight) live in W.S.powers (saved) and are applied
-- by the `powers` system at the start of every tick (first in sim/systems/init.lua).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local blame = require("sim.causality.blame")
local rng = require("sim.rng")
local ledger = require("sim.ledger")

local powers = { name = "powers" }
local floor, min, max, sqrt = math.floor, math.min, math.max, math.sqrt

powers.KINDS = { "lightning", "weather", "temperature", "cull", "introduce", "blight", "oil", "meteor" }

local function cfg() return cfgmod.POWERS end

-- Cells within `steps` level-4 steps of c (BFS on the grid), c first.
local function area(W, c, steps)
  local r = floor(steps / W.sscale + 0.5)
  if r < 0 then r = 0 end
  local g = W.grid
  local list, dist = { c }, { [c] = 0 }
  local head = 1
  while head <= #list do
    local x = list[head]; head = head + 1
    if dist[x] < r then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if not dist[j] then dist[j] = dist[x] + 1; list[#list + 1] = j end
      end
    end
  end
  return list
end
powers.area = area

local function state(W)
  local S = W.S.powers
  if not S then
    S = { forcings = {}, blight = {} }
    W.S.powers = S
  end
  return S
end

function powers.init(W)
  state(W)
  W.scratch = W.scratch or {}
end

local function root(W, kind, c, mag, data)
  return events.emit(W, "power_" .. kind, c, 0, mag or 1, data)
end

---------------------------------------------------------------------------------------------
-- Powers

local P = {}

function P.lightning(W, p)
  local c = p.cell
  local id = root(W, "lightning", c, 1, { ship = p.ship, person = p.person })
  local ents = W.ent
  -- a ship on the cell (or the one aimed at)
  if p.ship then
    if p.list == "boats" then
      -- a village's fishing boat (sim/society/fishing.lua): lost with its catch, maybe its crew
      local b = ents.boats and ents.boats[p.ship]
      if b and b.state ~= "lost" then
        local v = ents.villages[b.village]
        local at = require("sim.society.sea").cell_of(W, b) or (b.trip and b.trip.ground) or b.home
        require("sim.society.fishing").lose(W, v, b, id, "lightning")
        require("sim.systems.ocean").spill(W, at, cfg().boat_oil, id)
        return id
      end
    else
      local t = ents.ships and ents.ships[p.ship]
      if t and t.alive ~= false and not t.sunk then
        require("sim.systems.ships").sink(W, t, id)
        return id
      end
    end
  end
  for _, t in ipairs(ents.ships or {}) do
    if t.alive and not t.sunk and t.cell == c then
      require("sim.systems.ships").sink(W, t, id)
      return id
    end
  end
  -- a villager
  if p.person then
    local person = ents.people[p.person]
    if person and not person.dead then
      local v = ents.villages[person.village]
      local sents = require("sim.society.entities")
      sents.kill(W, person, "lightning", id)
      if v then sents.compact(W, v); v.labour_candidate = id end
    end
  end
  -- the ground: fire if there is fuel, and a little heat
  if W.geo.ocean[c] == 0 then
    require("sim.systems.fire").ignite(W, c, id)
    W.F.heat_anom[c] = W.F.heat_anom[c] + cfg().lightning_heat
    blame.set(W.C.heat_anom, W.I.heat_anom, c, id)
  end
  return id
end

local function add_forcing(W, kind, c, amount, id)
  local S = state(W)
  local cells = area(W, c, cfg().radius)
  local f = { kind = kind, cells = cells, amount = amount, tag = id, from = W.tick,
    to = W.tick + cfg().season_ticks }
  S.forcings[#S.forcings + 1] = f
  return f
end

function P.weather(W, p)
  local mode = p.mode == "drought" and "drought" or "rain"
  local id = root(W, mode, p.cell, 1)
  add_forcing(W, mode, p.cell, 1, id)
  return id
end

function P.temperature(W, p)
  local mode = p.mode == "chill" and "chill" or "heat"
  local id = root(W, mode, p.cell, cfg().temp_anom)
  add_forcing(W, mode, p.cell, mode == "heat" and cfg().temp_anom or -cfg().temp_anom, id)
  return id
end

function P.cull(W, p)
  local sp = p.species or "predators"
  local cells = area(W, p.cell, cfg().radius)
  local id = root(W, "cull", p.cell, #cells, { species = sp })
  local frac = cfg().cull_fraction
  if sp == "grazers" or sp == "predators" then
    -- the herds or packs on these cells (W4)
    require("sim.systems.fauna").kill_in(W, cells, sp, frac, id)
    return id
  end
  local fieldname = (sp == "fish") and "fish" or sp
  local F, C, I = W.F, W.C, W.I
  local fld, cc, ic = F[fieldname], C[fieldname], I[fieldname]
  local prev = W.P[fieldname]
  local ocean = W.geo.ocean
  local want_ocean = (sp == "fish") and 1 or 0
  for i = 1, #cells do
    local c = cells[i]
    if ocean[c] == want_ocean and fld[c] > 0 then
      local v = fld[c] * (1 - frac)
      if v < 1e-5 then v = 0 end
      fld[c] = v
      if prev then prev[c] = v end
      blame.set(cc, ic, c, id)
    end
  end
  return id
end

function P.introduce(W, p)
  local sp = p.species or "grazers"
  if sp == "settlers" then
    local id = root(W, "introduce", p.cell, cfg().settlers, { species = "settlers" })
    require("sim.society.mobility").settlers(W, p.cell, cfg().settlers, id)
    return id
  end
  local cells = area(W, p.cell, p.radius or cfg().introduce_radius)
  local id = root(W, "introduce", p.cell, #cells, { species = sp })
  if sp == "grazers" or sp == "predators" then
    -- a breeding population: herds or packs of the landscape's kind (W4), as many animals as the
    -- density added over the land cells of the area
    local land = 0
    for i = 1, #cells do if W.geo.ocean[cells[i]] == 0 then land = land + 1 end end
    local wcfg = require("sim.config").WILD
    local per = sp == "grazers" and wcfg.gpc or wcfg.ppc
    if land > 0 then require("sim.systems.fauna").spawn(W, p.cell, sp, (cfg().introduce[sp] or 0.3) * land * per, id) end
    return id
  end
  local F, C, I = W.F, W.C, W.I
  local fld, cc, ic = F[sp], C[sp], I[sp]
  local prev = W.P[sp]
  local ocean = W.geo.ocean
  local want_ocean = (sp == "fish") and 1 or 0
  local amt = cfg().introduce[sp] or 0.3
  for i = 1, #cells do
    local c = cells[i]
    if ocean[c] == want_ocean then
      local v = fld[c] + amt
      if v > 1 then v = 1 end
      fld[c] = v
      if prev then prev[c] = v end
      blame.set(cc, ic, c, id)
    end
  end
  return id
end

-- infect a farm cell (sparse set; one record per cell)
local function infect(W, c, tag)
  local S = state(W)
  local B = S.blight
  for i = 1, #B do if B[i].cell == c then return false end end
  B[#B + 1] = { cell = c, tag = tag, since = W.tick, winters = 0 }
  blame.set(W.C.crop, W.I.crop, c, tag)
  return true
end

function P.blight(W, p)
  local c = p.cell
  local id = root(W, "blight", c, 1)
  if W.F.land_use[c] > 0 then infect(W, c, id)
  else
    -- aimed next to the fields: the nearest farm within the radius catches it
    local cells = area(W, c, cfg().radius)
    for i = 1, #cells do
      if W.F.land_use[cells[i]] > 0 then infect(W, cells[i], id); break end
    end
  end
  return id
end

function P.oil(W, p)
  local c = p.cell
  local id = root(W, "oil", c, cfg().oil)
  for _, t in ipairs(W.ent.ships or {}) do
    if t.alive and not t.sunk and (t.id == p.ship or t.cell == c) then
      require("sim.systems.ships").sink(W, t, id)
      return id
    end
  end
  if W.geo.ocean[c] == 1 then require("sim.systems.ocean").spill(W, c, cfg().oil, id) end
  return id
end

function P.meteor(W, p)
  local c = p.cell
  local C = cfg()
  local id = root(W, "meteor", c, C.meteor_aerosol)
  local F = W.F
  local ocean = W.geo.ocean
  local fire = require("sim.systems.fire")
  local cells = area(W, c, C.meteor_radius)
  for i = 1, #cells do
    local x = cells[i]
    if ocean[x] == 0 then
      -- the blast: flattened, burning, heat
      F.heat_anom[x] = F.heat_anom[x] + C.meteor_heat
      blame.set(W.C.heat_anom, W.I.heat_anom, x, id)
      fire.ignite(W, x, id)
    end
  end
  -- the animals in the blast (W4: herds and packs)
  local wildlife = require("sim.systems.fauna")
  wildlife.kill_in(W, cells, "grazers", C.meteor_kill, id)
  wildlife.kill_in(W, cells, "predators", C.meteor_kill, id)
  -- villages in the blast: houses and people
  local sents = require("sim.society.entities")
  local construction = require("sim.society.construction")
  local set = {}
  for i = 1, #cells do set[cells[i]] = true end
  for _, v in ipairs(W.ent.villages or {}) do
    if v.alive and set[v.cell] then
      local near = v.cell == c
      local share = near and C.meteor_deaths_centre or C.meteor_deaths_ring
      local m = v.members
      for k = 1, #m do
        local person = W.ent.people[m[k]]
        if person and not person.dead then
          local u = rng.float(W.seed, rng.S.DEATH, person.key, W.tick, 911)
          if u < share then sents.kill(W, person, "meteor", id) end
        end
      end
      sents.compact(W, v)
      construction.damage(W, v, near and 0.8 or 0.4)
      v.stock.grain = v.stock.grain * (near and 0.3 or 0.7)
      v.labour_candidate = id
      if v.cause == 0 then v.cause = id end
    end
  end
  -- impact winter
  W.G.aerosol = (W.G.aerosol or 0) + C.meteor_aerosol
  ledger.add(W, "aerosol", C.meteor_aerosol, id)
  return id
end

--- Applies a power (the `power` command handler). Returns the root event id.
function powers.apply(W, p)
  local f = P[p.power]
  if not f then error("unknown power " .. tostring(p.power)) end
  if not p.cell or p.cell < 1 or p.cell > W.n then error("power needs a cell") end
  return f(W, p)
end

---------------------------------------------------------------------------------------------
-- The powers system: sustained forcings and blight, every tick before the climate systems.

local function season_winter_end(W, cell)
  -- the end of this cell's winter (local calendar): southern cells are half a year off
  local toy = W.tick % 48
  if W.grid.Y[cell] < 0 then toy = (toy + 24) % 48 end
  return toy == 0
end

function powers.update(W)
  local S = state(W)
  local C = cfg()
  local F = W.F
  -- weather forcing is rebuilt every tick (scratch; moisture reads it)
  local sc = W.scratch
  local wf = sc.weather_force
  if not wf then wf = {}; sc.weather_force = wf end
  local wl = sc.weather_cells
  if not wl then wl = {}; sc.weather_cells = wl end
  for i = #wl, 1, -1 do wf[wl[i]] = nil; wl[i] = nil end
  local fs = S.forcings
  local keep = 0
  local any = false
  for i = 1, #fs do
    local f = fs[i]
    if W.tick < f.to then
      keep = keep + 1
      fs[keep] = f
      local cells = f.cells
      if f.kind == "rain" or f.kind == "drought" then
        any = true
        local rec = f.kind == "rain" and { mult = C.deluge_mult, add = C.deluge_add, tag = f.tag }
          or { mult = C.drought_mult, add = 0, tag = f.tag }
        for j = 1, #cells do
          local c = cells[j]
          if not wf[c] then wl[#wl + 1] = c end
          wf[c] = rec
        end
        if f.kind == "drought" then
          -- a hot dry season: the heat dries the soil faster
          local ha, ch, ih = F.heat_anom, W.C.heat_anom, W.I.heat_anom
          for j = 1, #cells do
            local c = cells[j]
            if ha[c] < C.drought_heat then ha[c] = C.drought_heat end
            if ch[c] ~= f.tag and not blame.descends(W.E.parent, ch[c], f.tag) then blame.set(ch, ih, c, f.tag) end
          end
        end
      else
        local ha, ch, ih = F.heat_anom, W.C.heat_anom, W.I.heat_anom
        local sst, csst, isst = F.sst, W.C.sst, W.I.sst
        local temp, ocean = F.temp, W.geo.ocean
        local a0 = f.amount
        -- (S24) a v3 warm or cold air (f.air) barely touches the sea under it in its few days (`v3_air_sea` of it, the
        -- water's own warmth untouched); the v2 powers and the v3 sea power heat or chill the water itself
        local airf = f.air
        for j = 1, #cells do
          local c = cells[j]
          -- the sea holds the heat (or cold) more than the land does
          local a = ocean[c] == 1 and (airf and a0 * C.v3_air_sea or a0 * C.sea_factor) or a0
          if (a > 0 and ha[c] < a) or (a < 0 and ha[c] > a) then ha[c] = a end
          if ch[c] ~= f.tag and not blame.descends(W.E.parent, ch[c], f.tag) then blame.set(ch, ih, c, f.tag) end
          -- over the sea the power heats (or chills) the water itself: the surface follows
          -- the forced air without the usual weeks of lag
          if not airf and ocean[c] == 1 and ((a > 0 and sst[c] < temp[c]) or (a < 0 and sst[c] > temp[c])) then
            sst[c] = temp[c]
            if csst[c] ~= f.tag and not blame.descends(W.E.parent, csst[c], f.tag) then blame.set(csst, isst, c, f.tag) end
          end
        end
      end
    end
  end
  for i = #fs, keep + 1, -1 do fs[i] = nil end
  sc.weather_on = any
  -- blight: monthly
  local B = S.blight
  if #B == 0 or W.tick % 4 ~= 0 then return end
  local g = W.grid
  local lu, crop = F.land_use, F.crop
  local cc, ic = W.C.crop, W.I.crop
  local n0 = #B
  for i = 1, n0 do
    local b = B[i]
    local c = b.cell
    if lu[c] > 0 then
      -- die-back of the standing crop
      if crop[c] > 0 then
        crop[c] = crop[c] * (1 - C.blight_dieback)
        blame.set(cc, ic, c, b.tag)
      end
      -- spreads to neighbouring farms
      local base = c * 6 - 6
      for k = 1, g.nbr_count[c] do
        local j = g.nbr[base + k]
        if lu[j] > 0 and rng.float(W.seed, rng.S.BLIGHT, j, W.tick, 1) < C.blight_spread then
          infect(W, j, b.tag)
        end
      end
      -- trade: to another village within reach
      if rng.float(W.seed, rng.S.BLIGHT, c, W.tick, 2) < C.blight_trade then
        local v = W.ent.villages[lu[c]]
        local vs = W.ent.villages
        local best, bd = nil, 1e9
        for vi = 1, #vs do
          local o = vs[vi]
          if o.alive and v and o.id ~= v.id and #o.farms > 0 then
            local d = g.X[o.cell] * g.X[c] + g.Y[o.cell] * g.Y[c] + g.Z[o.cell] * g.Z[c]
            local steps = dmath.acos(max(-1, min(1, d))) / (0.07553)
            if steps <= C.blight_trade_steps and steps < bd then
              -- only villages the blight hasn't reached yet
              local has = false
              for q = 1, #B do if lu[B[q].cell] == o.id then has = true; break end end
              if not has then best, bd = o, steps end
            end
          end
        end
        if best then
          local target = best.farms[1 + floor(rng.float(W.seed, rng.S.BLIGHT, best.cell, W.tick, 3) * #best.farms)]
          local ev = events.emit(W, "blight_spread", target, b.tag, 1, { village = best.id })
          infect(W, target, ev ~= 0 and ev or b.tag)
        end
      end
    end
  end
  -- burns out after two winters (per infected cell)
  local keep2 = 0
  for i = 1, #B do
    local b = B[i]
    if season_winter_end(W, b.cell) and W.tick > b.since then b.winters = b.winters + 1 end
    if b.winters < C.blight_winters and lu[b.cell] > 0 then
      keep2 = keep2 + 1
      B[keep2] = b
    end
  end
  for i = #B, keep2 + 1, -1 do B[i] = nil end
end

function powers.shift_ticks(W, dt)
  local S = W.S.powers
  if not S then return end
  for i = 1, #S.forcings do
    S.forcings[i].from = S.forcings[i].from + dt
    S.forcings[i].to = S.forcings[i].to + dt
  end
  for i = 1, #S.blight do S.blight[i].since = S.blight[i].since + dt end
end

-- the logged command (queued with world.command(W, "power", {...}))
require("sim.world").COMMANDS.power = function(W, p) powers.apply(W, p) end

return powers
