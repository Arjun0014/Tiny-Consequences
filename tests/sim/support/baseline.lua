-- Long untouched baseline runs shared by property tests (cached per seed, extended on demand).
-- Collects: yearly P06 range violations, yearly band metrics, monthly continent fauna means
-- and the global fish mean (first STATS_YEARS years).
local world = require("sim.world")
local fields = require("sim.fields")
local metrics = require("sim.metrics")
local scen = require("tests.sim.scenarios.lib")
local cfgmod = require("sim.config")

local B = {}
B.STATS_YEARS = 200

local cache = {}

local function check_ranges(W, out, max_keep)
  local F, n = W.F, W.n
  local inf = math.huge
  for _, f in ipairs(fields.LIST) do
    local a = F[f.name]
    local lo, hi = f.min, f.max
    for c = 1, n do
      local v = a[c]
      if v ~= v or v == inf or v == -inf or v < lo or v > hi then
        if #out < max_keep then
          out[#out + 1] = string.format("y%d %s[%d]=%s", math.floor(W.tick / 48), f.name, c, tostring(v))
        end
        out.count = (out.count or 0) + 1
      end
    end
  end
  for _, k in ipairs({ "co2", "g_temp", "aerosol", "sea_level", "ice_fraction" }) do
    local v = W.G[k]
    if v ~= v or v == inf or v == -inf then
      out[#out + 1] = "global " .. k .. "=" .. tostring(v)
      out.count = (out.count or 0) + 1
    end
  end
end

-- Continents = landmasses big enough to count (≥ 90% of the worldgen minimum share).
local function continents(W)
  local list = {}
  local comps = W.geo.components
  local min_frac = cfgmod.WORLDGEN.continent_min_frac * 0.9
  for i = 1, #comps do
    if comps[i].size / W.n >= min_frac then list[#list + 1] = i end
  end
  return list
end
B.continents = continents

function B.run(seed, years)
  local r = cache[seed]
  if not r then
    local W = scen.fresh_world(seed)
    local conts = continents(W)
    r = { W = W, seed = seed, years = 0, violations = {}, yearly = {}, conts = conts,
          g = {}, p = {}, fish = {} }
    for k = 1, #conts do r.g[k], r.p[k] = {}, {} end
    cache[seed] = r
  end
  local W = r.W
  local geo = W.geo
  while r.years < years do
    for t = 1, 48 do
      world.tick(W)
      if t % 4 == 0 and r.years < B.STATS_YEARS then
        local F = W.F
        local cont = geo.continent
        local sg, sp, sn = {}, {}, {}
        for k = 1, #r.conts do sg[k], sp[k], sn[k] = 0, 0, 0 end
        local idx = {}
        for k = 1, #r.conts do idx[r.conts[k]] = k end
        local land = geo.land_cells
        for i = 1, #land do
          local c = land[i]
          local k = idx[cont[c]]
          if k then sg[k] = sg[k] + F.grazers[c]; sp[k] = sp[k] + F.predators[c]; sn[k] = sn[k] + 1 end
        end
        for k = 1, #r.conts do
          local g, p = r.g[k], r.p[k]
          g[#g + 1] = sg[k] / sn[k]
          p[#p + 1] = sp[k] / sn[k]
        end
        local fs, no = 0, 0
        local oc = geo.ocean_cells
        for i = 1, #oc do fs = fs + F.fish[oc[i]]; no = no + 1 end
        r.fish[#r.fish + 1] = fs / no
      end
    end
    r.years = r.years + 1
    check_ranges(W, r.violations, 10)
    local m = metrics.compute(W)
    r.yearly[r.years] = { forest = m.forest_cover, grass = m.grassland, desert = m.desert,
      ice = m.ice_fraction, g_temp = m.g_temp, headlines = W.E.headline_count }
  end
  return r
end

-- Mean and coefficient of variation of t[from..to].
function B.cv(t, from, to)
  to = to or #t
  local s, s2, n = 0, 0, 0
  for i = from, to do s = s + t[i]; s2 = s2 + t[i] * t[i]; n = n + 1 end
  if n < 2 then return 0, 0 end
  local m = s / n
  local var = s2 / n - m * m
  if var < 0 then var = 0 end
  return m > 0 and math.sqrt(var) / m or 0, m
end

function B.minmax(t, from, to)
  local lo, hi = math.huge, -math.huge
  for i = from, to or #t do
    if t[i] < lo then lo = t[i] end
    if t[i] > hi then hi = t[i] end
  end
  return lo, hi
end

return B
