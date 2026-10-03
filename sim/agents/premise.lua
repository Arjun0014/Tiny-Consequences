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

-- (S20, R6b) A wrong in village s's past: `days` ago a man of village o took n of a household's
-- sheep from s's fold, s knows it was someone of o (tracks), asked for redress and was told nobody
-- there would own to it. History written as a deed and what s knows of it; what s's people do
-- about it now is their own. Returns the deed.
function M.wronged(W, s, o, n, days, answer)
  local deeds = require("sim.agents.deeds")
  local people = require("sim.agents.people")
  local V = W.S.v3
  local now = W.S.v3.now or W.hour
  local by
  for _, id in ipairs(o.members) do
    local q = V.people[id]
    if q and q.alive and q.sex == "m" and people.stage(q, now) == "adult" then by = q; break end
  end
  local hh = V.households[s.households[1]]
  local d = deeds.add(W, { kind = "take", t = now - days * 24, at = s.id, by = by and by.id or 0, by_hh = by and by.hh or 0,
    by_s = o.id, to = 0, to_hh = hh.id, to_s = s.id, what = "sheep", n = n, fu = (n or 1) * 6 * 1.2, harm = true, away = true })
  local e = deeds.learn(W, s, d, "the tracks of a man leading them went off by the road to " .. o.name, 0, o.id, now - days * 24 + 30)
  e.answer, e.claim_t = answer or "denied", now - (days - 2) * 24
  -- (the man's own household knows what he did)
  if by then deeds.learn(W, o, d, "ours", by.id, o.id, now - days * 24, true) end
  return d, by
end

-- (S20, R6f) Village s lost n sheep to wolves at its fold over the last `days` days (history: deeds of
-- loss, as wolves' kills write them).
function M.wolf_losses(W, s, n, days)
  local deeds = require("sim.agents.deeds")
  local V = W.S.v3
  local now = V.now or W.hour
  local H = s.herds and s.herds.flock
  for i = 1, n do
    local a = H and H.animals[i]
    local t = now - days * 24 * (n - i + 1) / (n + 1)
    local d = deeds.add(W, { kind = "lost", t = t, at = s.id, by = 0, by_hh = 0, by_s = 0, to = 0, to_hh = a and a.owner or 0,
      to_s = s.id, what = "sheep", n = 1, fu = 6, harm = true, wolves = true })
    deeds.learn(W, s, d, "wolves in the night", 0, 0, t)
  end
end

-- (S20) Named premises for tools that must apply the same world in two runtimes (the browser's hash
-- check): "conflict" - Hottoodingle Hop's harvest failed, its beasts died, its stores are empty, and
-- its people know Zurug and Krikeivorn.
function M.named(W, name)
  local V = W.S.v3
  if name == "conflict" then
    local A = V.settlements[4]
    M.fail_harvest(W, A); M.murrain(W, A, 0); M.empty_stores(W, A)
    M.knows(W, A, V.settlements[2]); M.knows(W, A, V.settlements[6])
    return true
  elseif name == "fire" then
    -- (S20, R6.5b) a dry spell at Tekheim and lightning sets a house alight
    local s = V.settlements[1]
    W.F.soil_water[s.cell] = 0.2
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "house" and b.state == "standing" and b.hh ~= 0 then
        local blaze = require("sim.agents.blaze")
        blaze.strike(W, s, b.cx, b.cy, V.now or W.hour, nil)
        blaze.ignite(W, s, "bld", b.id, V.now or W.hour, nil)
        return true
      end
    end
  end
  return false
end

-- (S20) A sickness took village s's dogs.
function M.no_dogs(W, s)
  for _, d in ipairs(s.dogs or {}) do d.alive = false end
end

-- (S20, R6a) Village s's people have been to village o lately (a trader, a fair): they know how it
-- stands - its stores, its people's wants, the beasts in its pens and its dogs (econ.learn), and o's
-- people have seen their faces.
function M.knows(W, s, o)
  local econ = require("sim.agents.econ")
  local people = require("sim.agents.people")
  econ.learn(W, s, o, W.S.v3.now or W.hour)
  for _, id in ipairs(s.members) do
    local p = W.S.v3.people[id]
    if p and p.alive and people.stage(p, W.hour) == "adult" and p.sex == "m" then people.came(p, o.id, W.hour) end
  end
end

return M
