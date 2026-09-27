-- Villages that know each other (docs/world/18_DEEP_WORLD.md §2.2-2.3, phase W7).
--
-- A sparse relation per pair of living villages that have met: kin (people with a parent in the
-- other village), the reciprocity ledger (need-days of food and goods given each way), trades
-- made, and the parent/daughter link. Everything that moves between villages is carried by real
-- people: porters walk the path with the cargo (mobility party kind "porters"), deliver it, and
-- walk home ("returning"); brides walk to their husband's village ("bride").
--
--   relief   a village that is rationing asks the villages that are well disposed to it; one that
--            has grain to spare beyond its own needs to its next harvest sends porters with a gift
--            (social storage: Halstead & O'Shea 1989). The famine's chain gains a relief link.
--   barter   a village short of wood with grain to spare trades with a friendly village that has
--            wood to spare (grassland villages burning brush and dung buy timber from forest
--            villages): porters carry grain there and wood back.
--   brides   a single woman with no match at home may be betrothed to a man of a friendly
--            village; she walks there (kin ties follow the marriages).
--
-- W.ent.rel["a_b"] (a < b village ids) = { kin, gab, gba (need-days a gave b, b gave a), trades,
-- last (hour of the last dealing) }. Lookups by key only (no pairs() in logic).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local sites = require("sim.society.sites")
local farming = require("sim.society.farming")
local cotag = require("sim.society.cotag")

local M = {}
local floor, min, max, ceil, log = math.floor, math.min, math.max, math.ceil, math.log

-- Set by sim/society/init.lua: mobility.depart (callbacks avoid require cycles).
M.depart = function() end

local function key(a, b)
  if a > b then a, b = b, a end
  return a .. "_" .. b
end

function M.get(W, a, b, create)
  local R = W.ent.rel
  if not R then R = {}; W.ent.rel = R end
  local k = key(a, b)
  local r = R[k]
  if not r and create then
    r = { kin = 0, gab = 0, gba = 0, trades = 0, last = W.hour }
    R[k] = r
  end
  return r
end

-- Need-days a gave b (and b gave a) as seen from a.
local function ledger(r, a, b)
  if a < b then return r.gab, r.gba end
  return r.gba, r.gab
end

local function give(W, a, b, days)
  local r = M.get(W, a, b, true)
  if a < b then r.gab = r.gab + days else r.gba = r.gba + days end
  r.last = W.hour
end

-- How well disposed village u is toward village v (>= 0 friendly): the parent/daughter tie, kin,
-- past trades, and the balance of gifts (a village that took much and gave little asks in vain).
function M.score(W, u, v)
  local s = 0
  if u.from == v.id or v.from == u.id then s = s + 1 end
  local r = M.get(W, u.id, v.id)
  if r then
    s = s + 0.25 * min(6, r.kin) + 0.15 * min(5, r.trades)
    local gave, got = ledger(r, u.id, v.id)
    s = s - 0.004 * max(0, gave - got)   -- ~250 need-days unreturned cancel a daughter tie
  end
  return s
end

-- Worldgen: villages of one culture on one landmass within marrying reach start with kin ties
-- (their settlers came of common stock).
function M.seed(W)
  local cfg = cfgmod.SOCIETY
  local vs = W.ent.villages
  for i = 1, #vs do
    for j = i + 1, #vs do
      local a, b = vs[i], vs[j]
      if a.alive and b.alive and a.continent == b.continent and a.culture == b.culture
        and sites.dist(W, a.cell, b.cell) <= cfg.bride_reach then
        M.get(W, a.id, b.id, true).kin = cfg.seed_kin
      end
    end
  end
end

-- Monthly: kin counts (people with a parent living in the other village).
function M.kin_monthly(W, v)
  local P = W.ent.people
  local vs = W.ent.villages
  local seen = {}
  local order = {}
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p and (p.mother ~= 0 or p.father ~= 0) then
      for _, rid in ipairs({ p.mother, p.father }) do
        local q = rid ~= 0 and P[rid]
        if q and not q.dead and q.village ~= v.id and vs[q.village] and vs[q.village].alive then
          local u = q.village
          if not seen[u] then seen[u] = 0; order[#order + 1] = u end
          seen[u] = seen[u] + 1
          break
        end
      end
    end
  end
  table.sort(order)
  for i = 1, #order do
    local r = M.get(W, v.id, order[i], true)
    -- kin both ways: keep the larger of the two villages' counts this month
    if seen[order[i]] > r.kin then r.kin = seen[order[i]] end
  end
end

-- Food a village can spare beyond its own needs to its next harvest and a margin (FU).
local function spare_food(W, u)
  local wait = farming.days_to_harvest(W, u) + cfgmod.SOCIETY.relief_margin_days
  return u.stock.grain + u.stock.fresh - u.need_day * wait
end
M.spare_food = spare_food

-- The villages v could turn to: living, same landmass, within reach, best disposed first.
local function partners(W, v, reach)
  local out = {}
  local vs = W.ent.villages
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u ~= v and u.continent == v.continent and u.pop > 0 then
      local d = sites.dist(W, v.cell, u.cell)
      if d <= reach then
        local s = M.score(W, u, v) - 0.02 * d
        out[#out + 1] = { u = u, s = s, d = d }
      end
    end
  end
  table.sort(out, function(a, b) if a.s ~= b.s then return a.s > b.s end return a.u.id < b.u.id end)
  return out
end

-- Porters of village u: able adults not away, not the only hands of their household (members order).
local function porters(W, u, n)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local out = {}
  for i = 1, #u.members do
    if #out >= n then break end
    local p = P[u.members[i]]
    local a = ents.age(W, p)
    if p.away == 0 and a >= 16 and a < 50 and p.health >= 60 and p.preg_due == 0 and p.task ~= "fish" then
      out[#out + 1] = p
    end
  end
  return out
end

local function emit(W, kind, cell, parent, mag, data, v)
  if not W.record_events then return 0 end
  local co = v and cotag.co_of(W, v)
  return events.emit(W, kind, cell, parent, mag, data, co and { co = co } or nil)
end

-- Monthly for village v (after its migration decisions).
function M.monthly(W, v)
  local cfg = cfgmod.SOCIETY
  M.kin_monthly(W, v)
  if v.pop == 0 then return end
  local hour = W.hour
  -- 1. relief: rationing villages ask their friends
  if (v.rationing or v.famine) and hour - (v.relief_asked or -1e9) >= 672 then
    v.relief_asked = hour
    local wait = farming.days_to_harvest(W, v) + 30
    local short = v.need_day * wait - (v.stock.grain + v.stock.fresh)
    if short > 0 then
      local list = partners(W, v, cfg.relief_reach)
      local asked = 0
      for i = 1, #list do
        if short <= 0 or asked >= 3 then break end
        local e = list[i]
        local u = e.u
        if e.s >= 0 and not u.rationing and not u.famine and not u.porters_out then
          asked = asked + 1
          local spare = spare_food(W, u)
          -- kin and daughters give up to half their spare, friends a quarter
          local share = (M.get(W, u.id, v.id) and M.get(W, u.id, v.id).kin > 0 or u.from == v.id or v.from == u.id) and 0.5 or 0.25
          local days = max(1, e.d * cfg.walk_h * 1.5 / cfg.walk_hours_day)
          -- what a few porters can carry in a month of back-and-forth trips
          local cap = cfg.porter_fu * cfg.porters * max(1, floor(28 / (2 * days + 1)))
          local gift = min(short, spare * share, cap)
          if gift >= v.need_day * 10 then
            local ps = porters(W, u, min(cfg.porters, max(2, ceil(gift / (cfg.porter_fu * 4)))))
            if #ps >= 2 then
              u.stock.grain = u.stock.grain - gift
              short = short - gift
              local parent = v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause
              local id = emit(W, "relief", v.cell, parent, floor(gift), { village = v.id, from = u.id, food = floor(gift) }, v)
              M.depart(W, u, ps, v.id, nil, "porters", id, 0, 0, { grain = gift, back_to = u.id, gift = true })
              u.porters_out = true
            end
          end
        end
      end
    end
  end
  -- 2. barter: wood for grain
  local fday = v.fuel_day or 0
  local wood_short = fday > 0 and v.wood_days < 25
  if wood_short and not v.porters_out and not v.rationing then
    local spare = spare_food(W, v)
    if spare > 200 then
      local list = partners(W, v, cfg.trade_reach)
      for i = 1, #list do
        local e = list[i]
        local u = e.u
        local ufd = u.fuel_day or 1
        local uspare = u.stock.wood - ufd * 120
        if e.s >= -0.2 and uspare > 100 and not u.porters_out then
          local days = max(1, e.d * cfg.walk_h * 1.5 / cfg.walk_hours_day)
          local cap = cfg.porter_fu * cfg.porters * max(1, floor(28 / (2 * days + 1)))
          local want = min(cap, fday * 60, uspare * 0.5, spare * 0.5)
          if want >= 60 then
            local ps = porters(W, v, min(cfg.porters, max(2, ceil(want / (cfg.porter_fu * 4)))))
            if #ps >= 2 then
              -- one WU of wood for one FU of grain, made a little dearer the farther it comes
              local price = want * (1 + 0.03 * e.d)
              v.stock.grain = v.stock.grain - price
              local id = emit(W, "trade", v.cell, 0, floor(want), { village = v.id, to = u.id, wood = floor(want) }, v)
              M.depart(W, v, ps, u.id, nil, "porters", id, 0, 0, { grain = price, back_to = v.id, buy_wood = want })
              v.porters_out = true
              break
            end
          end
        end
      end
    end
  end
  -- 3. brides from friendly villages: an unmatched woman may be betrothed to a man there
  M.brides(W, v)
end

-- Unmatched singles look to the best-disposed village within reach: a woman of v is betrothed to
-- an unmatched man there and walks to his village (at most one a month).
function M.brides(W, v)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local hour = W.hour
  local month = floor(hour / 672)
  local function singles(u, sex)
    local out = {}
    for i = 1, #u.members do
      local p = P[u.members[i]]
      if p.sex == sex and p.partner == 0 and p.betrothed == 0 and p.away == 0 then
        local a = (hour - p.born) / 8064
        if a >= cfg.court_age[sex] and a <= 35 then out[#out + 1] = p end
      end
    end
    return out
  end
  local women = singles(v, "f")
  if #women == 0 then return end
  local men_here = singles(v, "m")
  if #men_here >= #women then return end
  local u0 = rng.float(W.seed, rng.S.PAIRING, v.key_cell, month, 17)
  if u0 > cfg.bride_prob then return end
  local list = partners(W, v, cfg.bride_reach)
  for i = 1, #list do
    local e = list[i]
    if e.s >= 0 then
      local u = e.u
      local men = singles(u, "m")
      local women_there = singles(u, "f")
      if #men > #women_there then
        local w, m = women[#women], men[#men]
        if not (w.mother ~= 0 and (w.mother == m.mother or w.father == m.father)) then
          w.betrothed, m.betrothed = m.id, w.id
          w.wait_since, m.wait_since = hour, hour
          local id = emit(W, "bride", v.cell, 0, 1, { village = v.id, to = u.id }, nil)
          M.depart(W, v, { w }, u.id, nil, "bride", id, v.need_day > 0 and 14 or 0, 0)
          return
        end
      end
    end
  end
end

-- A porter party arrives (mobility): deliver the cargo, trade, and set off home.
function M.porters_arrive(W, party, dest, living)
  local cfg = cfgmod.SOCIETY
  local home = W.ent.villages[party.cargo.back_to]
  local c = party.cargo
  local back = { back_to = c.back_to }
  if dest and dest.alive then
    if c.gift then
      dest.stock.grain = dest.stock.grain + c.grain
      give(W, home.id, dest.id, c.grain / max(1, dest.need_day))
      local a = dest.acc
      a.relief_in = (a.relief_in or 0) + c.grain
      home.acc.relief_out = (home.acc.relief_out or 0) + c.grain
    elseif c.buy_wood then
      -- the seller gives what it can still spare for the grain brought
      local ufd = dest.fuel_day or 1
      local can = max(0, min(c.buy_wood, dest.stock.wood - ufd * 90))
      local paid = c.grain * can / c.buy_wood
      dest.stock.wood = dest.stock.wood - can
      dest.stock.grain = dest.stock.grain + paid
      back.wood = can
      back.grain = c.grain - paid     -- unspent grain goes home again
      local r = M.get(W, home.id, dest.id, true)
      r.trades = r.trades + 1
      r.last = W.hour
      dest.acc.wood_sold = (dest.acc.wood_sold or 0) + can
      home.acc.wood_bought = (home.acc.wood_bought or 0) + can
    end
  else
    back.grain = c.grain
  end
  if home and home.alive then
    M.depart(W, dest or home, living, home.id, nil, "returning", party.reason, 0, 0, back, party.path[#party.path])
  end
end

-- Porters are home: unload what they carry.
function M.returning_arrive(W, party, home)
  local c = party.cargo or {}
  if home and home.alive then
    home.stock.wood = home.stock.wood + (c.wood or 0)
    home.stock.grain = home.stock.grain + (c.grain or 0)
    home.porters_out = nil
  end
end

return M
