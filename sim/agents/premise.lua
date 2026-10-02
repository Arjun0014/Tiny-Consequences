-- Premises for tests, tools and screenshots (S17): states of the world created on purpose so that
-- what follows from them can be watched - never called by the running simulation itself. Each is a
-- plain change of the world as it is (more households, an emptied store, a failed harvest); what the
-- people then do is their own.
local M = {}

-- Village s a generation on: families (the founding templates, in turn) in standing houses on every
-- lot near it (at most `max` more), its granary that of a village its size (40 days a new mouth);
-- `waited`: its waiting couples have waited that many days already. Returns the families added.
function M.crowd(W, s, waited, max)
  local settle = require("sim.agents.settle")
  local found = require("sim.agents.found")
  local rng = require("sim.rng")
  local clock = require("sim.agents.clock")
  local n0 = #s.members
  local k = 0
  local function u() k = k + 1; return rng.float(W.seed, rng.S.V3_FOUND, 777, k, 0) end
  local doy0 = clock.local_doy(W.hour, W.grid.lat[s.cell])
  local pool = {}
  for _, t in ipairs(found.TEMPLATE) do pool[#pool + 1] = t end
  for _, t in ipairs(found.MORE) do pool[#pool + 1] = t end
  local added = 0
  while added < (max or 40) do
    local ix, iy, side = settle.find_lot(W, s)
    if not ix then break end
    local hb = settle.add_building(W, s, "house", ix, iy, side, "standing")
    found.add_family(W, s, hb, pool[added % #pool + 1], u, s.culture, doy0)
    added = added + 1
  end
  if s.granary then s.granary.stock.grain = (s.granary.stock.grain or 0) + (#s.members - n0) * 40 end
  if waited then s.no_lot = waited end
  -- (the agents' event queue is derived: rebuilt with the new people in it, as after a load)
  W.v3rt = nil
  return added
end

-- Village s's harvest failed: its standing crops lost, its sheaves gone.
function M.fail_harvest(W, s)
  for _, pl in ipairs(s.plots) do
    pl.sheaves = 0
    if pl.stage == "growing" or pl.stage == "ripe" then
      for r = 1, pl.rows do pl.row_st[r] = 0 end
      pl.sown, pl.stage = 0, "fallow"
    end
  end
end

-- (S19) A murrain at village s: its beasts died of a sickness, all but `keep` a household (the
-- youngest; the plough oxen spared). Since S19 a hungry household kills its own beasts before it
-- thinks of leaving, so a premise for hunger that should move people takes the herds too.
function M.murrain(W, s, keep)
  keep = keep or 0
  local now = W.S.v3.now or W.hour
  local kept = {}
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[kind]
    local list = {}
    for _, a in ipairs(H and H.animals or {}) do if a.alive and not a.ox then list[#list + 1] = a end end
    table.sort(list, function(x, y) if x.born ~= y.born then return x.born > y.born end return x.k < y.k end)
    for _, a in ipairs(list) do
      if (kept[a.owner] or 0) < keep then
        kept[a.owner] = (kept[a.owner] or 0) + 1
      else
        a.alive, a.died, a.cause = false, now, "sickness"
        s.acc.stock_died = (s.acc.stock_died or 0) + 1
      end
    end
  end
end

-- Village s's stores emptied (granary and larders), as after a store fire or a long hunger.
function M.empty_stores(W, s)
  local settle = require("sim.agents.settle")
  if s.granary then s.granary.stock.grain, s.granary.stock.sheaves, s.granary.stock.smoked = 0, 0, 0 end
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if settle.HOME[b.kind] and b.hh ~= 0 then
      b.stock.smoked, b.stock.food, b.stock.cheese, b.stock.meat, b.stock.fish, b.stock.meals = 0, 0, 0, 0, 0, 0
    end
  end
end

return M
