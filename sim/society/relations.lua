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
--   trade    a village short of one good and long in another trades with a friendly village that
--            has the first to spare (grassland villages buy timber from forest villages with grain;
--            forest villages short of food sell wood for grain): its porters carry its goods
--            there and the partner's goods back. Deliveries are counted as the villages' imports
--            and exports (weekly normals, economy.weekly): a village that can count on imports
--            plans fewer fields or cuts less wood, one that usually exports grows or cuts more
--            (specialisation, farming.plan and labour.assign); when a usual supplier cannot trade,
--            its trouble (its own cause) is blamed for the missing imports.
--   brides   a single woman with no match at home may be betrothed to a man of a friendly
--            village; she walks there (kin ties follow the marriages).
--
-- W.ent.rel["a_b"] (a < b village ids) = { kin, gab, gba (food a gave b, b gave a, FU: each side
-- reckons it in its own need-days, M.score), trades, last (hour of the last dealing), ev_ab, ev_ba
-- (the last relief each way: the cause of a gift that repays it) }. Lookups by key only (no pairs()
-- in logic).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local events = require("sim.causality.events")
local blame = require("sim.causality.blame")
local ents = require("sim.society.entities")
local sites = require("sim.society.sites")
local farming = require("sim.society.farming")
local sea = require("sim.society.sea")
local cotag = require("sim.society.cotag")
local crafts = require("sim.society.crafts")

local M = {}
local floor, min, max, ceil, log = math.floor, math.min, math.max, math.ceil, dmath.log

-- Set by sim/society/init.lua: mobility.depart (callbacks avoid require cycles).
M.depart = function() end

-- (the key strings are made once: lookups are the society's commonest operation)
local KEYS = {}
local function key(a, b)
  if a > b then a, b = b, a end
  local n = a * 65536 + b
  local k = KEYS[n]
  if not k then k = a .. "_" .. b; KEYS[n] = k end
  return k
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

-- Food a gave b (and b gave a) as seen from a (FU).
local function ledger(r, a, b)
  if a < b then return r.gab, r.gba end
  return r.gba, r.gab
end

local function give(W, a, b, fu)
  local r = M.get(W, a, b, true)
  if a < b then r.gab = r.gab + fu else r.gba = r.gba + fu end
  r.last = W.hour
end
M.give = give

-- a came to b's help (allies in a fight, conflict.battle): help worth `days` of b's need-days
function M.helped(W, a, b, days)
  local vb = W.ent.villages[b]
  return give(W, a, b, days * max(1, vb and vb.need_day or 1))
end

-- How well disposed village u is toward village v (>= 0 friendly): the parent/daughter tie, kin,
-- past trades, and the balance of gifts (a village that took much and gave little asks in vain; one
-- that took is bound to the giver), each reckoned in u's own need-days: what a gift cost a big
-- village is little, what it meant to a small one much.
function M.score(W, u, v)
  local s = 0
  if u.from == v.id or v.from == u.id then
    -- the parent/daughter tie fades as the daughter village grows up (living kin carry it on);
    -- a village that split away in a quarrel has none
    local young = u.from == v.id and u or v
    if not young.split then
      local age = (W.hour - (young.founded or W.hour)) / 8064
      s = s + max(0, 1 - age / cfgmod.SOCIETY.daughter_tie_years)
    end
  end
  local r = M.get(W, u.id, v.id)
  if r then
    -- kin by share: a handful of in-laws bind two hamlets, not two towns (per 30 people of the
    -- smaller village)
    local small = min(u.pop or 30, v.pop or 30)
    s = s + 0.25 * min(6, r.kin * 30 / max(30, small)) + 0.15 * min(5, r.trades)
    s = s + M.gift_term(W, u, v, r)
    -- grievance (W8): what u holds against v
    local gr = (u.id < v.id) and (r.ga or 0) or (r.gb or 0)
    s = s - 0.3 * min(6, gr)
  end
  -- lord and tributary (W9): bound to each other
  if u.lord == v.id or v.lord == u.id then s = s + 1 end
  return s
end

-- The gifts' part of u's disposition toward v: what u gave v and was not returned (resentment, ~250
-- need-days cancel a daughter tie) or what u took from v and has not returned (obligation: gifts
-- are remembered by those who took them, which is what makes giving a store — social storage,
-- D-074), in u's own need-days. It governs gifts, not trade: a village that took much and gave
-- little asks for help in vain, but may still buy (M.trade).
function M.gift_term(W, u, v, r)
  r = r or M.get(W, u.id, v.id)
  if not r then return 0 end
  local gave, got = ledger(r, u.id, v.id)
  local net = (gave - got) / max(1, u.need_day or 1)
  if net > 0 then return -0.004 * net end
  return cfgmod.SOCIETY.owe_w * min(cfgmod.SOCIETY.owe_cap, -net)
end

-- Set by init (callbacks: sim/society/status.lua, conflict.lua).
M.honour_leader = function() end
M.wrong = function() end
M.temper = function() return 1, 1 end

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
        local r = M.get(W, a.id, b.id, true)
        r.anc, r.ka, r.kb, r.kin = cfg.seed_kin, 0, 0, cfg.seed_kin
      end
    end
  end
end

-- Monthly: kin — people with a living parent in the other village, counted both ways (v's side
-- now; the other side in its own month: r.ka for the lower id's members, r.kb for the higher's).
-- A tie lives as long as the parents do: villages that stop marrying drift apart within a
-- generation, unless new marriages renew the tie. The common stock of a landmass's first villages
-- (r.anc), past dealings (r.trades) and old debts (gab, gba) fade too — each pair aged once a
-- month by its lower id.
local SEEN = {}
function M.kin_monthly(W, v)
  local P = W.ent.people
  local vs = W.ent.villages
  local cfg = cfgmod.SOCIETY
  local seen = SEEN
  local nv = #vs
  for i = 1, nv do seen[i] = 0 end
  local vid = v.id
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p and (p.mother ~= 0 or p.father ~= 0) then
      local u = 0
      local q = p.mother ~= 0 and P[p.mother]
      if q and not q.dead and q.village ~= vid then u = q.village end
      if u == 0 then
        q = p.father ~= 0 and P[p.father]
        if q and not q.dead and q.village ~= vid then u = q.village end
      end
      if u ~= 0 and vs[u] and vs[u].alive then seen[u] = seen[u] + 1 end
    end
  end
  for i = 1, nv do
    if i ~= vid then
      local n = seen[i]
      local r = M.get(W, vid, i, n > 0)
      if r then
        -- (a relation from before counts were kept both ways: its ratcheted kin becomes a
        -- fading memory of common stock)
        if r.ka == nil and r.kb == nil then r.anc = (r.anc or 0) + r.kin; r.ka, r.kb = 0, 0 end
        if vid < i then r.ka = n else r.kb = n end
        if vid < i then
          local a = r.anc
          if a then a = a * cfg.anc_keep; if a < 0.05 then a = nil end; r.anc = a end
          local t = r.trades
          if t > 0 then t = t * cfg.trade_keep; if t < 0.05 then t = 0 end; r.trades = t end
          -- old debts are forgiven as the generation that owed them passes
          if r.gab > 0 then r.gab = r.gab * cfg.anc_keep end
          if r.gba > 0 then r.gba = r.gba * cfg.anc_keep end
        end
        r.kin = r.ka + r.kb + (r.anc or 0)
      end
    end
  end
end

-- Food a village can spare beyond its own needs to its next harvest and a margin (FU).
local function spare_food(W, u)
  local wait = farming.days_to_harvest(W, u) + cfgmod.SOCIETY.relief_margin_days
  return u.stock.grain + u.stock.fresh - u.need_day * wait
end
M.spare_food = spare_food

-- The villages v could turn to: living, within reach, best disposed first. Overseas villages are
-- in reach by boat (sea.lua): the sender's boats (u's when u_sends, else v's) must seat n people;
-- d is then the crossing's cost in walking steps.
local function partners(W, v, reach, u_sends, n)
  -- (the same village's market reckoning and trade in the same step share one list)
  local sc = W.scratch
  if not sc then sc = {}; W.scratch = sc end
  local memo = sc.partners
  if not memo then memo = {}; sc.partners = memo end
  local mk = v.id * 8 + (u_sends and 4 or 0) + min(3, sea.boats_needed(n or 2))
  local m = memo[mk]
  if m and m.hour == W.hour and m.reach == reach then return m.list end
  local out = {}
  local vs = W.ent.villages
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u ~= v and u.pop > 0 then
      local d
      if u.continent == v.continent then d = sites.dist(W, v.cell, u.cell)
      elseif u_sends then d = sea.cost(W, u, v.cell, n or 2)
      else d = sea.cost(W, v, u.cell, n or 2) end
      if d and d <= reach then
        local s = M.score(W, u, v) - 0.02 * d
        out[#out + 1] = { u = u, s = s, d = d }
      end
    end
  end
  table.sort(out, function(a, b) if a.s ~= b.s then return a.s > b.s end return a.u.id < b.u.id end)
  memo[mk] = { hour = W.hour, reach = reach, list = out }
  return out
end

-- Trades made between two villages.
local function r_trades(W, a, b)
  local r = M.get(W, a.id, b.id)
  return r and r.trades or 0
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
      local list = partners(W, v, cfg.relief_reach, true, cfg.porters)
      local asked = 0
      for i = 1, #list do
        if short <= 0 or asked >= 3 then break end
        local e = list[i]
        local u = e.u
        if e.s < 0 and not u.rationing and not u.famine and spare_food(W, u) > short * 3 then
          -- a village with plenty that would not help: remembered
          M.wrong(W, v.id, u.id, 0.4, 0)
        end
        if e.s >= 0 and not u.rationing and not u.famine and not u.porters_out then
          asked = asked + 1
          local spare = spare_food(W, u)
          -- kin and daughters give up to half their spare, friends a quarter (as the giver's
          -- leader is inclined: a hoarder gives less, the devout more)
          local share = (M.get(W, u.id, v.id) and M.get(W, u.id, v.id).kin > 0 or u.from == v.id or v.from == u.id) and 0.5 or 0.25
          share = share * M.temper(W, u)
          local days = max(1, e.d * cfg.walk_h * 1.5 / cfg.walk_hours_day)
          -- what a few porters can carry in a month of back-and-forth trips
          local cap = cfg.porter_fu * cfg.porters * max(1, floor(28 / (2 * days + 1)))
          local gift = min(short, spare * share, cap)
          -- a village that owes v gives back at least what it took, as far as its spare allows
          local rr = M.get(W, u.id, v.id)
          local owed = 0
          if rr then
            local gave_u, got_u = ledger(rr, u.id, v.id)
            owed = got_u - gave_u
            if owed > 0 then gift = min(short, cap, max(gift, min(owed, spare * 0.75))) end
          end
          if gift >= v.need_day * 10 then
            local ps = porters(W, u, min(cfg.porters, max(2, ceil(gift / (cfg.porter_fu * 4)))))
            if #ps >= 2 then
              u.stock.grain = u.stock.grain - gift
              short = short - gift
              local parent = v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause
              local id = emit(W, "relief", v.cell, parent, floor(gift), { village = v.id, from = u.id, food = floor(gift),
                repay = owed > 0 and floor(owed + 0.5) or nil }, v)
              M.depart(W, u, ps, v.id, nil, "porters", id, 0, 0, { grain = gift, back_to = u.id, gift = true })
              u.porters_out = true
            end
          end
        end
      end
    end
  end
  -- 1b. what the granaries cannot hold is given away or feasted (D-074)
  M.surplus(W, v)
  -- 2. trade: what is short for what is long
  M.market(W, v)
  M.trade(W, v)
  -- 3. brides from friendly villages: an unmatched woman may be betrothed to a man there
  M.brides(W, v)
end

-- Monthly: grain beyond what the village's granaries hold rots (economy.daily). Rather than let it,
-- the village gives it to a friendly village within reach that has room to store it (the one
-- with fewest days of food), carried by its porters; the gift is remembered (the ledger: a village
-- that took is bound to help back, M.score, and gives back in need, the relief above). What is
-- left after that is eaten at a feast once a year, with guests from the friendly villages of its
-- landmass: the hospitality is remembered too, and the host (the leader's household) honoured.
-- Surplus as social storage and standing (Halstead and O'Shea 1989; Hayden 1996). D-074.
function M.surplus(W, v)
  local cfg = cfgmod.SOCIETY
  local cap = v.grain_cap
  if not cap or v.rationing or v.famine or v.pop <= 0 then return end
  -- a surplus is a harvest's: in the months after the village's own harvest, what that harvest
  -- left beyond the granaries (never the food a village just founded carried, its granary not yet
  -- built), and never what it needs itself to its next harvest and a margin
  if not v.harvest_end or W.hour - v.harvest_end > 3 * 672 then return end
  local keep = v.need_day * (farming.days_to_harvest(W, v) + cfg.relief_margin_days) - v.stock.fresh
  if keep > cap then cap = keep end
  local over = v.stock.grain - cap
  if over > (v.last_harvest or 0) then over = v.last_harvest or 0 end
  if over <= 0 then return end
  local give_f = M.temper(W, v)   -- (a hoarder gives half as much, the devout more)
  if not v.porters_out then
    local list = partners(W, v, cfg.relief_reach, false, cfg.porters)
    -- a village it owes first (what it took in its own hunger and has not returned, the most owed
    -- first: balanced reciprocity), else the partner with the fewest days of food
    local best, bneed, broom, bd, bowe = nil, 0, 0, 0, 0
    for i = 1, #list do
      local e = list[i]
      local u = e.u
      if e.s >= 0 and not u.porters_out and not u.famine then
        local room = (u.grain_cap or 0) - u.stock.grain - (u.expect and u.expect.food or 0)
        if room > u.need_day * cfg.gift_min_days then
          local r = M.get(W, v.id, u.id)
          local owe = 0
          if r then local gave, got = ledger(r, v.id, u.id); owe = (got - gave) / max(1, v.need_day) end
          if owe < 1 then owe = 0 end   -- (need-days)
          if not best or owe > bowe or (owe == bowe and u.food_days < bneed) then
            best, bneed, broom, bd, bowe = u, u.food_days, room, e.d, owe
          end
        end
      end
    end
    if best then
      local days = max(1, bd * cfg.walk_h * 1.5 / cfg.walk_hours_day)
      local pcap = cfg.porter_fu * cfg.porters * max(1, floor(28 / (2 * days + 1)))
      local gift = min(over * min(1, give_f), broom, pcap)
      if gift >= best.need_day * cfg.gift_min_days then
        local ps = porters(W, v, min(cfg.porters, max(2, ceil(gift / (cfg.porter_fu * 4)))))
        if #ps >= 2 then
          v.stock.grain = v.stock.grain - gift
          over = over - gift
          -- (a gift that repays carries the relief it answers as its cause)
          local parent = 0
          if bowe > 0 then
            local r = M.get(W, v.id, best.id)
            if best.id < v.id then parent = r.ev_ab or 0 else parent = r.ev_ba or 0 end
          end
          local id = emit(W, "gift", v.cell, parent, floor(gift), { village = v.id, to = best.id, food = floor(gift),
            repay = bowe > 0 and floor(bowe + 0.5) or nil }, v)
          M.depart(W, v, ps, best.id, nil, "porters", id, 0, 0, { grain = gift, back_to = v.id, gift = true, surplus = true })
          v.porters_out = true
        end
      end
    end
  end
  -- the harvest feast: once a year, what is still more than the granaries hold
  local _, lyear = farming.local_week(W, v)
  if over > v.need_day * cfg.feast_min_days and v.feast_year ~= lyear then
    v.feast_year = lyear
    v.stock.grain = v.stock.grain - over
    local guests, gn = {}, 0
    local list = partners(W, v, cfg.relief_reach)
    for i = 1, #list do
      local e = list[i]
      if gn < 3 and e.s >= 0 and e.u.continent == v.continent then gn = gn + 1; guests[gn] = e.u end
    end
    for i = 1, gn do
      local u = guests[i]
      give(W, v.id, u.id, over * cfg.guest_share / gn)
    end
    local nd = over / max(1, v.need_day)
    M.honour_leader(W, v, nd * cfgmod.STATUS.feast_prestige)
    v.acc.feast = (v.acc.feast or 0) + over
    emit(W, "feast", v.cell, 0, floor(nd + 0.5), { village = v.id, guests = gn, food = floor(over) }, v)
  end
end

-- Wood a village can spare beyond its reserve of fuel days (WU).
local function wood_spare(W, u)
  local fd = max(0.5, u.fuel_day or 1)
  return u.stock.wood - fd * cfgmod.SOCIETY.wood_keep_days
end
M.wood_spare = wood_spare

-- Goods that travel between villages: stored food (grain, dried fish), wood, and the crafts.
-- VALUE: what a unit is worth in days of food (roughly what its maker eats while making it);
-- WEIGHT: a porter's load of it relative to food.
local GOODS = { "food", "wood", "tools", "pots", "cloth" }
local STOCK = { food = "grain", wood = "wood", tools = "tools", pots = "pots", cloth = "cloth" }
local function value(g) return (g == "food" or g == "wood") and 1 or cfgmod.CRAFT[g].value end
local function weight(g) return (g == "food" or g == "wood") and 1 or cfgmod.CRAFT[g].weight end
M.GOODS, M.STOCK = GOODS, STOCK

-- Units of good g village u can let go (beyond its own reserve).
local function spare_of(W, u, g)
  if g == "food" then return (u.rationing or u.famine) and 0 or spare_food(W, u) end
  if g == "wood" then return wood_spare(W, u) end
  return (u.stock[g] or 0) - crafts.need_year(W, u, g) * cfgmod.CRAFT.keep_years
end
M.spare_of = spare_of

-- Units of good g village u wants to buy (0: none), and how urgent (lower = sooner).
local function want_of(W, u, g)
  local cfg = cfgmod.SOCIETY
  if g == "food" then
    if u.food_days < cfg.trade_food_days and not u.famine then return u.need_day * 45, u.food_days / cfg.trade_food_days end
    return 0, 9
  elseif g == "wood" then
    local fd = u.fuel_day or 0
    if fd > 0 and u.wood_days < cfg.trade_wood_days then return max(0.5, fd) * 60, u.wood_days / cfg.trade_wood_days end
    return 0, 9
  end
  local need = crafts.need_year(W, u, g)
  local have = u.stock[g] or 0
  -- a village that buys a good rather than make it keeps a year's supply coming
  local low = crafts.buys(u, g) and 0.7 or 0.35
  if need > 0 and have < need * low then return need * (low + 0.45) - have, have / (need * low) + 0.5 end
  return 0, 9
end
M.want_of = want_of

-- Monthly: the best craft source among the villages v trades with or could (its partners within
-- trading reach): what buying would cost (crafts.buys).
function M.market(W, v)
  local cfg = cfgmod.SOCIETY
  local m = v.market
  if not m then m = {}; v.market = m end
  local list = partners(W, v, cfg.trade_reach)
  for _, g in ipairs(crafts.GOODS) do
    local best, bq, bd = nil, 0, 0
    for i = 1, #list do
      local e = list[i]
      local s = e.u.crafts and e.u.crafts[g]
      if e.s >= -0.2 and s and s.q > bq then best, bq, bd = e.u.id, s.q, e.d end
    end
    local x = m[g]
    if not x then x = {}; m[g] = x end
    x.id, x.q, x.d = best or 0, bq, bd
  end
end

-- Monthly: a village short of a good trades with a friendly village that has it to spare, paying
-- with what it has most of. Its porters carry the payment there and the good back.
function M.trade(W, v)
  local cfg = cfgmod.SOCIETY
  if v.porters_out or v.famine then return end
  -- the most urgent want
  local gw, want, urg = nil, 0, 9
  for i = 1, #GOODS do
    local g = GOODS[i]
    local w, u = want_of(W, v, g)
    if w > 0 and u < urg then gw, want, urg = g, w, u end
  end
  if not gw then return end
  -- what v pays with: its largest spare by value (not the good it wants)
  local gp, pv = nil, 0
  for i = 1, #GOODS do
    local g = GOODS[i]
    if g ~= gw then
      local sp = spare_of(W, v, g)
      if sp > 0 and sp * value(g) > pv then gp, pv = g, sp * value(g) end
    end
  end
  if not gp or pv < 60 then return end
  local list = partners(W, v, cfg.trade_reach, false, cfg.porters)
  local refused = nil
  for i = 1, #list do
    local e = list[i]
    local u = e.u
    -- (a partner owed unreturned gifts still sells at a fair rate: the gifts' part of its
    -- disposition is left out; grudges are not)
    if e.s - M.gift_term(W, u, v) >= -0.2 and not u.porters_out then
      local theirs = spare_of(W, u, gw)
      local days = max(1, e.d * cfg.walk_h * 1.5 / cfg.walk_hours_day)
      local trips = max(1, floor(28 / (2 * days + 1)))
      local cap = cfg.porter_fu * cfg.porters * trips
      -- what can be got, carried there (payment) and back (goods)
      local q = min(want, theirs * 0.5, cap / weight(gw))
      local price = q * value(gw) * (1 + 0.03 * e.d)
      local pay = price / value(gp)
      local pmax = min(spare_of(W, v, gp) * 0.7, cap / weight(gp))
      if pay > pmax then q = q * pmax / pay; pay = pmax end
      if q * value(gw) >= 50 then
        local load = max(pay * weight(gp), q * weight(gw))
        local ps = porters(W, v, min(cfg.porters, max(2, ceil(load / (cfg.porter_fu * 4)))))
        if #ps >= 2 then
          local sk = STOCK[gp]
          v.stock[sk] = (v.stock[sk] or 0) - pay
          local id = emit(W, "trade", v.cell, 0, floor(q), { village = v.id, to = u.id, give = gp, get = gw,
            amount = floor(q) }, v)
          M.depart(W, v, ps, u.id, nil, "porters", id, 0, 0, { give = gp, amount = pay, get = gw, want = q,
            back_to = v.id })
          v.porters_out = true
          -- paid for and on its way: the village counts on it (economy.daily eats by it)
          local e = v.expect
          if not e then e = {}; v.expect = e end
          e[gw] = (e[gw] or 0) + q
        end
        return
      elseif r_trades(W, u, v) > 0 and not refused then
        -- a usual partner can't trade now: its own trouble explains the missing goods
        refused = u
      end
    end
  end
  if refused and gw == "food" then
    local t = refused.foodtag[1] ~= 0 and refused.foodtag[1] or refused.cause
    if t ~= 0 then v.import_tag = t end
  end
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
  local list = partners(W, v, cfg.bride_reach, false, 1)
  -- friends first; failing them, strangers who bear no grudge (how marriage networks spread to new
  -- villages and across the water: the first bride makes the kin tie)
  for pass = 1, 2 do
    for i = 1, #list do
      local e = list[i]
      if (pass == 1 and e.s >= 0) or (pass == 2 and e.s < 0 and e.s >= cfg.bride_stranger) then
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
end

-- A porter party arrives (mobility): deliver the cargo, trade, and set off home.
function M.porters_arrive(W, party, dest, living)
  local home = W.ent.villages[party.cargo.back_to]
  local c = party.cargo
  if c.buy_wood and not c.get then
    -- (a barter party from an older save)
    c.give, c.amount, c.get, c.want, c.grain = "food", c.grain, "wood", c.buy_wood, nil
  end
  local back = { back_to = c.back_to }
  if dest and dest.alive then
    if c.tribute then
      -- tribute delivered: the lord's store and standing grow
      dest.stock.grain = dest.stock.grain + c.grain
      dest.wk_food_imp = (dest.wk_food_imp or 0) + c.grain
      dest.acc.tribute_in = (dest.acc.tribute_in or 0) + c.grain
      M.honour_leader(W, dest, c.grain / max(1, dest.need_day) * cfgmod.STATUS.gift_prestige)
    elseif c.gift then
      dest.stock.grain = dest.stock.grain + c.grain
      local days = c.grain / max(1, dest.need_day)
      give(W, home.id, dest.id, c.grain)
      if not c.surplus then
        dest.relief_got = W.hour
        -- (the debt remembers the relief it came from: a gift that repays it descends from it)
        if (party.reason or 0) ~= 0 then
          local r = M.get(W, home.id, dest.id, true)
          if home.id < dest.id then r.ev_ab = party.reason else r.ev_ba = party.reason end
        end
      end
      -- generosity is honoured; a gift washes away some of an old grievance
      M.honour_leader(W, home, days * cfgmod.STATUS.gift_prestige)
      M.wrong(W, dest.id, home.id, -days * cfgmod.CONFLICT.gift_forgives, 0)
      local a = dest.acc
      local kind = c.surplus and "gift" or "relief"
      a[kind .. "_in"] = (a[kind .. "_in"] or 0) + c.grain
      home.acc[kind .. "_out"] = (home.acc[kind .. "_out"] or 0) + c.grain
    elseif c.get then
      -- the partner gives what it can still spare of the good wanted, for the goods brought
      local can = spare_of(W, dest, c.get)
      if c.get == "food" then can = can + dest.need_day * 30 end
      if c.get == "wood" then can = can + (dest.fuel_day or 1) * 30 end
      can = max(0, min(c.want, can))
      local paid = c.amount * can / c.want
      local gs, ps = STOCK[c.get], STOCK[c.give]
      dest.stock[gs] = (dest.stock[gs] or 0) - can
      dest.stock[ps] = (dest.stock[ps] or 0) + paid
      back.get, back.got, back.want = c.get, can, c.want
      if can < c.want * 0.9 and home and c.get == "food" then
        local t = dest.foodtag[1] ~= 0 and dest.foodtag[1] or dest.cause
        if t ~= 0 then home.import_tag = t end
      end
      -- unspent payment goes home again
      back.give, back.left = c.give, c.amount - paid
      local r = M.get(W, home.id, dest.id, true)
      r.trades = r.trades + 1
      r.last = W.hour
      -- exports and imports (weekly normals: specialisation)
      dest["wk_" .. c.give .. "_imp"] = (dest["wk_" .. c.give .. "_imp"] or 0) + paid
      dest["wk_" .. c.get .. "_exp"] = (dest["wk_" .. c.get .. "_exp"] or 0) + can
      home["wk_" .. c.give .. "_exp"] = (home["wk_" .. c.give .. "_exp"] or 0) + paid
      dest.acc[c.get .. "_sold"] = (dest.acc[c.get .. "_sold"] or 0) + can
      home.acc[c.get .. "_bought"] = (home.acc[c.get .. "_bought"] or 0) + can
      if c.give == "food" then dest.wk_imports = (dest.wk_imports or 0) + paid end
    end
  else
    back.give, back.left = c.give, c.amount
    back.grain = c.grain
  end
  if home and home.alive then
    M.depart(W, dest or home, living, home.id, nil, "returning", party.reason, 0, 0, back, party.path[#party.path])
  end
end

-- Goods counted on that will not come (B15): the expectation is settled and a food shortfall is
-- booked in the village's food account at once — the village knows the hour its porters are home
-- (or known dead), so rationing that begins within the week descends from the loss (its cause:
-- import_tag). Without a known cause the shortfall waits for the weekly account (economy.weekly).
function M.settle_expected(W, home, get, want, got)
  local e = home.expect
  if e and e[get] then
    local x = e[get] - want
    e[get] = x > 1e-6 and x or nil
  end
  local short = want - (got or 0)
  if short > 0 and get == "food" then
    local t = home.import_tag or 0
    if t ~= 0 then
      blame.step(home.foodtag, home.foodidle, 1, -short, home.need_day * 0.5 + 1, 26, -short, t, 0, 0, 0, 0, 0, 0)
    else
      home.wk_import_short = (home.wk_import_short or 0) + short
    end
  end
end

-- Porters are home: unload what they carry.
function M.returning_arrive(W, party, home)
  local c = party.cargo or {}
  if home and home.alive then
    home.stock.wood = home.stock.wood + (c.wood or 0)
    home.stock.grain = home.stock.grain + (c.grain or 0)
    if c.give and (c.left or 0) > 0 then
      local k = STOCK[c.give]
      home.stock[k] = (home.stock[k] or 0) + c.left
    end
    if c.get and (c.got or 0) > 0 then
      local k = STOCK[c.get]
      home.stock[k] = (home.stock[k] or 0) + c.got
      home["wk_" .. c.get .. "_imp"] = (home["wk_" .. c.get .. "_imp"] or 0) + c.got
      if c.get == "food" then home.wk_imports = (home.wk_imports or 0) + c.got end
    end
    -- what was counted on and did not come (lost at sea, a partner who could not give): an
    -- import shortfall, blamed on its cause (import_tag) in the food account (economy.weekly)
    if c.get and c.want then M.settle_expected(W, home, c.get, c.want, c.got) end
    home.porters_out = nil
  end
end

return M
