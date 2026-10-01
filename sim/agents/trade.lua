-- Trade between settlements (roadmap v3 R4, D-097): the first loop, by land. A fishing household
-- with smoked fish to spare sends one of its people with a load of it to a village it can walk to
-- (the same landmass, within `trade_reach`) whose granary has grain to spare and whose people have
-- little fish. The porter takes the fish from their own larder, walks off their map toward the
-- other village, travels the road (a timed leg, drawn on the planet), walks into the other
-- village, unloads the fish into its granary, loads grain at the going rate, and walks home with
-- it. Nothing moves without someone carrying it; both villages' food ledgers count the goods as
-- they change hands (acc.export / acc.import).
--   p.trip = { to = settlement id, from = settlement id, give = "smoked", take = "grain", n }
--   p.at   = the settlement whose map the person is on now (nil: their own)
local cfgmod = require("sim.config")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min, sqrt, acos = math.floor, math.max, math.min, math.sqrt, math.acos
local function C() return cfgmod.V3 end

-- Great-circle distance between two settlements (local units) and the hours on the road.
function M.road(W, a, b)
  local g = W.grid
  local ca, cb = a.cell, b.cell
  local dot = g.X[ca] * g.X[cb] + g.Y[ca] * g.Y[cb] + g.Z[ca] * g.Z[cb]
  if dot > 1 then dot = 1 elseif dot < -1 then dot = -1 end
  local d = acos(dot) * require("sim.agents.map").R
  return d, d / C().road_speed
end

-- Settlements reachable on foot from s (the same landmass, within reach), nearest first.
local PARTNERS = setmetatable({}, { __mode = "k" })
function M.partners(W, s)
  local P = PARTNERS[s]
  if P then return P end
  P = {}
  local cont = W.geo.continent
  for _, o in ipairs(W.S.v3.settlements) do
    if o ~= s and o.alive and cont[o.cell] == cont[s.cell] and cont[s.cell] ~= 0 then
      local d = M.road(W, s, o)
      if d <= C().trade_reach then P[#P + 1] = o end
    end
  end
  table.sort(P, function(x, y)
    local dx, dy = M.road(W, s, x), M.road(W, s, y)
    if dx ~= dy then return dx < dy end
    return x.id < y.id
  end)
  PARTNERS[s] = P
  return P
end

-- What a village has: days of grain and gathered food for its people, smoked fish a person.
function M.stores(W, s)
  local V = W.S.v3
  local n = 0
  for _, id in ipairs(s.members) do if V.people[id].alive then n = n + 1 end end
  n = max(1, n)
  local grain = s.granary and ((s.granary.stock.grain or 0) + (s.granary.stock.smoked or 0)) or 0
  local fish = s.granary and (s.granary.stock.smoked or 0) or 0
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" then
      grain = grain + (b.stock.food or 0)
      fish = fish + (b.stock.smoked or 0) + (b.stock.fish or 0)
    end
  end
  return grain / n, fish / n, n
end

-- The goods a household carries to sell (D-097; wool D-098): what it is (the house's stock and the
-- buyer granary's), what the household keeps a member, a load, and the buyer's want: a share 0..1
-- of how short it is (nil: not a buyer), its price in grain a unit at full want.
M.GOODS = {
  smoked = { food = true, keep = "trade_keep", load = "trade_load", rate = "trade_rate",
    want = function(W, o)
      local _, fish = M.stores(W, o)
      local c = C()
      if fish >= c.trade_fish_short then return nil end
      return 1 - fish / c.trade_fish_short
    end },
  wool = { food = false, keep = "trade_wool_keep", load = "trade_wool_load", rate = "trade_wool_rate",
    want = function(W, o)
      local c = C()
      local _, _, n = M.stores(W, o)
      local pipe = require("sim.agents.settle").textile_pipeline(W, o) / n
      if pipe >= c.textile_want * c.trade_wool_short then return nil end
      return 1 - pipe / (c.textile_want * c.trade_wool_short)
    end },
}
M.ORDER = { "smoked", "wool" }

-- A buyer for a load of `good`: the nearest partner with grain to spare that wants it. Returns the
-- partner and the grain a unit fetches there (dearer the shorter it is).
function M.buyer(W, s, good)
  local c = C()
  local G = M.GOODS[good or "smoked"]
  for _, o in ipairs(M.partners(W, s)) do
    if o.granary then
      local food = M.stores(W, o)
      local spare = (o.granary.stock.grain or 0)
      if food > c.trade_food_spare and spare > c.trade_load * 2 then
        local w = G.want(W, o)
        if w then return o, c[G.rate] * (1 + 0.5 * w) end
      end
    end
  end
  return nil
end

-- Where a person leaves map s for settlement o (and arrives on o's map from s).
function M.gate(W, s, o)
  return require("sim.agents.wild").edge_toward(W, s, o.cell)
end

return M
