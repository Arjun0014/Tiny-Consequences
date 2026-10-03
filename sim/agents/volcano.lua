-- Volcanoes (R6.5f, D-157, docs/v3/65_R6_5_LIVING_PLANET.md): sites on the strongest convergent
-- boundaries and a hotspot (as worldgen places them for v2 worlds; a v3 world gets them the first day
-- they are asked for), each gathering pressure day by day; past a threshold of its own it erupts with
-- a size (VEI). An eruption is physical: heat and fire at the vent and about it (the planet's fire),
-- the ground shaking (shake.quake), sulphur in the sky (`G.aerosol`, which cools every cell's air - and
-- so the growing of every village's crops and grass), and ash falling downwind: on a village's map the
-- ash lies on the grass (beasts go hungry), on the fields (crops grow slower) and on the roofs (heavy:
-- they crack and fall as in a quake), washed off by rain over the days. The rest is the people's.
--   W.S.v3.volc = { list = { { cell, kind, name, pressure, thr, last } } }
--   s.ash = depth (0..1) on village s's map
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local dmath = require("sim.dmath")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local danger = require("sim.agents.danger")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

local function u(W, a, b, k)
  return rng.float(W.seed, rng.S.V3_CONFLICT, a % 4294967296, b % 4294967296, 8000 + (k or 0))
end

-- The volcanoes of the world (made the first time they are asked for in a v3 world).
function M.list(W)
  local V = W.S.v3
  if V.volc then return V.volc.list end
  local c = C()
  local st, g = W.static, W.grid
  local cand = {}
  for cl = 1, W.n do if st.boundary and st.boundary[cl] == 1 then cand[#cand + 1] = cl end end
  table.sort(cand, function(a, b)
    local sa, sb = st.bstrength[a] or 0, st.bstrength[b] or 0
    if sa ~= sb then return sa > sb end
    return a < b
  end)
  local sp = dmath.cos(c.volc_spacing * 0.07553)
  local list = {}
  local function far(cl)
    for _, v in ipairs(list) do
      if g.X[cl] * g.X[v.cell] + g.Y[cl] * g.Y[v.cell] + g.Z[cl] * g.Z[v.cell] > sp then return false end
    end
    return true
  end
  for _, cl in ipairs(cand) do
    if #list >= c.volc_arcs then break end
    if far(cl) then list[#list + 1] = { cell = cl, kind = "arc" } end
  end
  -- (a hotspot away from any boundary)
  for k = 1, 50 do
    local cl = 1 + floor(u(W, 77, k, 1) * W.n)
    if (st.boundary[cl] or 0) == 0 and far(cl) then list[#list + 1] = { cell = cl, kind = "hotspot" }; break end
  end
  local names = require("content.names")
  local used = {}
  local n = 0
  local function un() n = n + 1; return u(W, 91, n, 2) end
  for i, v in ipairs(list) do
    v.id = i
    v.name = names.unique(un, names.volcano, used)
    v.thr = c.volc_thr * (0.6 + 0.8 * u(W, v.cell, 0, 4))
    v.pressure = u(W, v.cell, 0, 3) * 0.95 * v.thr    -- (below its own threshold when first made)
    v.ocean = W.geo.ocean[v.cell] == 1
  end
  V.volc = { list = list }
  return list
end

-- Daily, at the planet's step: pressure gathers; a volcano past its threshold erupts.
function M.daily(W, day)
  local c = C()
  for _, v in ipairs(M.list(W)) do
    v.pressure = v.pressure + c.volc_rate * (0.5 + u(W, v.cell, day, 5))
    if v.pressure >= v.thr then
      local x = u(W, v.cell, day, 6)
      local vei = 1 + floor(5 * x * x * x + 0.0001)
      v.pressure, v.thr, v.last = 0, c.volc_thr * (0.6 + 0.8 * u(W, v.cell, day, 7)), day
      -- (S21) a mountain a god stirred erupts with the god's event as its cause
      local cause = v.cause
      v.cause = nil
      M.erupt(W, v, vei, day * 24 + 24 * u(W, v.cell, day, 8), cause)
    end
  end
end

-- The way the ash goes from cell `from`: the prevailing wind of its latitude band (map x east, y
-- north; easterly trades, westerlies, polar easterlies) as a direction on the sphere.
local function downwind(W, from, to)
  local g = W.grid
  local lat = g.lat[from] * 57.29578
  local alat = lat < 0 and -lat or lat
  local we = (alat >= 30 and alat < 60) and 1 or -1   -- (+1: blowing east)
  -- (the east component of the step from `from` to `to`: lon difference)
  local dl = g.lon[to] - g.lon[from]
  if dl > 3.14159265 then dl = dl - 6.2831853 elseif dl < -3.14159265 then dl = dl + 6.2831853 end
  return (dl * we > 0) and 1 or 0.35
end

-- An eruption of volcano v with size vei at time t.
function M.erupt(W, v, vei, t, cause)
  local c = C()
  local V = W.S.v3
  local g = W.grid
  local ev = 0
  if W.record_events then ev = require("sim.causality.events").emit(W, "eruption", v.cell, cause or 0, vei, { vei = vei, name = v.name }) end
  v.erupted = (v.erupted or 0) + 1
  -- (sulphur in the sky: the whole planet cooler for a while; a little carbon)
  local aer = c.volc_aerosol[vei] or 0
  if aer > 0 then
    W.G.aerosol = (W.G.aerosol or 0) + aer
    require("sim.ledger").add(W, "aerosol", aer, ev)
  end
  W.G.co2_src = (W.G.co2_src or 0) + c.volc_co2 * vei
  -- (heat and fire at the vent and about it)
  if vei >= 2 then
    local fire = require("sim.systems.fire")
    fire.ignite(W, v.cell, ev)
    if vei >= 4 then
      for k = 1, g.nbr_count[v.cell] do fire.ignite(W, g.nbr[(v.cell - 1) * 6 + k], ev) end
    end
  end
  -- (the ground shakes about it)
  require("sim.agents.shake").quake(W, v.cell, c.volc_quake + 0.6 * vei, t, ev)
  -- (S20) an ocean volcano's great eruption raises a wave (tsunami.lua, injected)
  if v.ocean and vei >= c.tsu_vei and M.wave then M.wave(W, v.cell, vei - c.tsu_vei + 1, t, ev) end
  -- (ash on the villages downwind, by distance)
  for _, s in ipairs(V.settlements) do
    if s.alive then
      local dot = g.X[v.cell] * g.X[s.cell] + g.Y[v.cell] * g.Y[s.cell] + g.Z[v.cell] * g.Z[s.cell]
      local d = sqrt(max(0, 2 - 2 * min(1, dot))) / g.spacing
      local ash = c.volc_ash * vei * vei / (1 + d * d) * downwind(W, v.cell, s.cell)
      if ash >= 0.02 then M.ashfall(W, s, min(1, ash), t, ev, v, vei) end
    end
  end
end

-- Ash falls on village s's map: on the grass, the fields and the roofs; everyone sees it fall.
function M.ashfall(W, s, ash, t, ev, v, vei)
  local c = C()
  s.ash = min(1, (s.ash or 0) + ash)
  s.ash_t = t
  local d = deeds.add(W, { kind = "ash", t = t, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = 0, to_s = s.id,
    what = v and v.name or "the mountain", n = 1, fu = 0, harm = false, event = ev })
  deeds.learn(W, s, d, "ash fell from the sky", 0, 0, t)
  danger.each_on_map(W, s, function(p)
    if p.act ~= "carried" and people.stage(p, t) ~= "infant" and (p.ash_seen or -1) ~= floor(t / 24) then
      p.ash_seen = floor(t / 24)
      people.remember(p, t, sfmt("Ash fell from the sky%s", v and (": " .. v.name .. " is burning") or ""))
    end
  end)
  -- (heavy ash weighs on the roofs: the weakest crack, or fall in on whoever is inside)
  require("sim.agents.shake").roof_load(W, s, s.ash * c.ash_load_k, t, d, "ash")
  s.acc.ashfalls = (s.acc.ashfalls or 0) + 1
end

-- Daily for each village: rain and wind wash the ash off; while it lies the grass is under it (the
-- herds' pasture regrows less) and the crops grow slower.
function M.ash_daily(W, s, day)
  local a = s.ash
  if not a or a <= 0 then return end
  local c = C()
  local wet = W.F.precip[s.cell] or 0
  s.ash = max(0, a - c.ash_wash - c.ash_rain * wet)
  local P = s.pasture
  if P and P.grass then
    for b = 1, #P.grass do P.grass[b] = (P.grass[b] or 0) * (1 - c.ash_grass * a) end
  end
  for _, pl in ipairs(s.plots or {}) do
    if pl.stage == "growing" then pl.growth = max(0, (pl.growth or 0) - c.ash_crop * a / max(1, c.grow_days)) end
  end
  if s.ash <= 0 then s.ash = nil end
end

return M
