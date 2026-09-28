-- Crafts: tools, pots and cloth from the land around a village (docs/world/18 §2.6, D-056).
--
-- Every household wears out tools, breaks pots and wears thin its clothes; what a village can
-- make depends on where it stands:
--   tools   worked from stone where the ground is broken and high (quarry quality from the
--           terrain's slope and height); a worker without good tools gets less done
--   pots    shaped from clay in wet lowlands and river banks, fired with wood; without enough
--           pots the stored grain spoils faster (mice, damp)
--   cloth   spun and woven from bast fibres of the grassland and marsh (wool when W10 brings
--           herds); in the cold, people without enough cloth lose health outdoors even by a fire
-- Craft work fills the slack season (labour demand, low priority unless the good runs short).
-- A village that stands on good stone or clay makes more cheaply than one that does not; the
-- rest buy (relations.trade): regional specialisation grows out of geography and exchange.
--
-- Units: one unit of a good is what one person (tools: one worker) uses in a year.
-- v.stock.tools / pots / cloth; v.have (0..1 cover of each good, weekly); v.crafts = the village's
-- sources { tools = {cell, q}, pots = {...}, cloth = {...} } (derived weekly, not saved state that
-- matters: recomputed from the land).
local cfgmod = require("sim.config")

local M = {}
local floor, min, max = math.floor, math.min, math.max

M.GOODS = { "tools", "pots", "cloth" }

-- Quality (0..1) of each craft source at cell c.
local function q_stone(W, c)
  local slope = W.geo.slope and W.geo.slope[c] or 0
  local elev = W.static.elev[c] - (W.G.sea_level or 0)
  local q = (slope - 0.08) / 0.5 + (elev - 1.0) * 0.25
  return q < 0 and 0 or (q > 1 and 1 or q)
end

local function q_clay(W, c)
  local F = W.F
  local elev = W.static.elev[c] - (W.G.sea_level or 0)
  local wet = F.soil_water[c] / cfgmod.HYDRO.field_capacity
  local river = F.flow[c] > 0 and min(1, F.flow[c] / (cfgmod.CRAFT.clay_flow or 50)) or 0
  local q = max(wet - 0.35, 0) * 1.4 + river * 0.6
  if elev > 0.6 then q = q * max(0, 1 - (elev - 0.6) * 1.5) end
  return q < 0 and 0 or (q > 1 and 1 or q)
end

local function q_fibre(W, c)
  local F = W.F
  local q = F.grass[c] * 0.8 + (F.biome[c] == 8 and 0.4 or 0)
  return q > 1 and 1 or q
end

-- Yearly need of good g in village v (units).
function M.need_year(W, v, g)
  local cfg = cfgmod.CRAFT
  local P = W.ent.people
  local hour = W.hour
  if g == "tools" then
    -- working-age people (counted once a week by crafts.weekly)
    local n = v.n_workers
    if not n or v.n_workers_h ~= floor(hour / 168) then
      n = 0
      local ad, el = cfgmod.SOCIETY.adult_age, cfgmod.SOCIETY.elder_age
      for i = 1, #v.members do
        local a = (hour - P[v.members[i]].born) / 8064
        if a >= ad and a < el then n = n + 1 end
      end
      v.n_workers, v.n_workers_h = n, floor(hour / 168)
    end
    return n * cfg.tools.need
  elseif g == "pots" then
    return v.pop * cfg.pots.need
  else
    local cold = (v.cold_months or 0) > 0 and 1.5 or (W.F.temp[v.cell] < 8 and 1.25 or 1)
    return v.pop * cfg.cloth.need * cold
  end
end

-- The best source of each good within the village's reach (its farm ring), weekly.
function M.sources(W, v)
  local G = W.vgeo[v.id]
  local ring = G and G.ring or { v.cell }
  local s = v.crafts
  if not s then s = {}; v.crafts = s end
  local best = { tools = { v.cell, 0 }, pots = { v.cell, 0 }, cloth = { v.cell, 0 } }
  for i = 1, #ring do
    local c = ring[i]
    if W.geo.ocean[c] == 0 then
      local a, b, f = q_stone(W, c), q_clay(W, c), q_fibre(W, c)
      if a > best.tools[2] then best.tools = { c, a } end
      if b > best.pots[2] then best.pots = { c, b } end
      if f > best.cloth[2] then best.cloth = { c, f } end
    end
  end
  -- wool: a village with sheep enough spins and weaves wool at home
  local wool = require("sim.society.herds").wool_q(W, v)
  if wool > best.cloth[2] then best.cloth = { v.cell, wool } end
  for _, g in ipairs(M.GOODS) do
    local e = s[g]
    if not e then e = {}; s[g] = e end
    -- a village can always make a little of anything (poor stone, pit clay, nettle), slowly
    e.cell, e.q = best[g][1], max(cfgmod.CRAFT.min_q, best[g][2])
  end
  return s
end

-- Weekly: goods wear out, the cover of each good (v.have) for its effects.
function M.weekly(W, v)
  local cfg = cfgmod.CRAFT
  local st = v.stock
  local have = v.have
  if not have then have = {}; v.have = have end
  -- the sources change with the land and the seasons: looked at monthly
  local month = floor(W.hour / 672)
  if not v.crafts or v.crafts_month ~= month then
    M.sources(W, v)
    v.crafts_month = month
  end
  for _, g in ipairs(M.GOODS) do
    local need = M.need_year(W, v, g)
    -- villages from before crafts (older saves) start with most of a year's goods
    if st[g] == nil then st[g] = need * 0.8 end
    local x = st[g]
    local use = need / 48
    if use > x then use = x end
    x = x - use
    st[g] = x
    local a = v.acc
    a["used_" .. g] = (a["used_" .. g] or 0) + use
    -- fully covered with half a year in hand
    local h = need > 0 and x / (need * cfg.cover_years) or 1
    have[g] = h > 1 and 1 or h
  end
end

-- Make or buy: hours a village spends on one unit of good g at its own source, against the price
-- (in hours of work) of buying from the best source among its trading partners (v.market, set
-- monthly by relations.market). True when buying is clearly cheaper.
function M.buys(v, g)
  local cfg = cfgmod.CRAFT
  local m = v.market and v.market[g]
  local e = v.crafts and v.crafts[g]
  if not m or not e then return false end
  local own = 1 / (cfg[g].rate * e.q)
  local buy = cfg[g].value * cfg.fu_hours * (1 + 0.03 * m.d)
  return own > buy * cfg.buy_margin and m.q > e.q * 1.5
end

-- Labour demands for crafts (hours this week): up to the target stock (a year's need plus what
-- partners usually buy, less what the village can count on buying). A village that does better
-- buying a good keeps only a small stock made at home.
function M.demands(W, v, add, week_h)
  local cfg = cfgmod.CRAFT
  local scfg = cfgmod.SOCIETY
  local s = v.crafts
  if not s then return end
  local N = v.norm
  for _, g in ipairs(M.GOODS) do
    local c = cfg[g]
    local need = M.need_year(W, v, g)
    local target = need * cfg.target_years + ((N[g .. "_exp"] or 0) * scfg.export_trust
      - (N[g .. "_imp"] or 0) * scfg.import_trust) * 48 * cfg.target_years
    if M.buys(v, g) then target = min(target, need * cfg.buy_floor) end
    if target < need * cfg.buy_floor then target = need * cfg.buy_floor end
    local short = target - (v.stock[g] or 0)
    if short > 0 then
      local e = s[g]
      local rate = c.rate * e.q
      local hours = short / rate
      -- craft work fills the slack; it becomes urgent when the good runs out
      local prio = (v.have and (v.have[g] or 1) < 0.35) and 44 or 30
      -- a week's work at most (the rest next week)
      local cap = min(hours, week_h * max(1, #v.members * 0.15))
      add("craft_" .. g, e.cell, prio, cap)
    end
  end
end

-- Work done (labour.execute): `eff` effective hours of craft `g`. Returns units made.
function M.work(W, v, g, eff)
  local cfg = cfgmod.CRAFT
  local e = v.crafts and v.crafts[g]
  if not e then return 0 end
  local made = eff * cfg[g].rate * e.q
  if g == "pots" then
    -- firing takes wood
    local fuel = made * cfg.pots.fuel
    if fuel > v.stock.wood then made = v.stock.wood / cfg.pots.fuel; fuel = v.stock.wood end
    v.stock.wood = v.stock.wood - fuel
    v.acc.kiln_wood = (v.acc.kiln_wood or 0) + fuel
  end
  v.stock[g] = (v.stock[g] or 0) + made
  v.acc["made_" .. g] = (v.acc["made_" .. g] or 0) + made
  return made
end

-- Effects of the cover: work done with poor tools, grain spoiling without pots, cold without cloth.
function M.tool_factor(v)
  local h = v.have and v.have.tools or 1
  return 1 - cfgmod.CRAFT.tools.loss * (1 - h)
end

function M.spoil_factor(v)
  local h = v.have and v.have.pots or 1
  return 1 + cfgmod.CRAFT.pots.spoil * (1 - h)
end

function M.cold_exposure(v)
  local h = v.have and v.have.cloth or 1
  return cfgmod.CRAFT.cloth.exposure * (1 - h)
end

-- Worldgen / founding: a village starts with some of each good.
function M.seed_stocks(W, v, years)
  for _, g in ipairs(M.GOODS) do
    v.stock[g] = M.need_year(W, v, g) * (years or 0.8)
  end
end

return M
