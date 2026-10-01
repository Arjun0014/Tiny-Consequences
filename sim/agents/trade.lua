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

-- A buyer for a load of smoked fish: the nearest partner with grain to spare and little fish.
-- Returns the partner and the grain a FU of fish fetches there (dearer where fish is scarcer).
function M.buyer(W, s)
  local c = C()
  for _, o in ipairs(M.partners(W, s)) do
    if o.granary then
      local food, fish = M.stores(W, o)
      local spare = (o.granary.stock.grain or 0)
      if food > c.trade_food_spare and fish < c.trade_fish_short and spare > c.trade_load * 2 then
        local rate = c.trade_rate * (1 + 0.5 * (1 - fish / c.trade_fish_short))
        return o, rate
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
