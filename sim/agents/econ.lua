-- The village economy (roadmap v3 R4, D-101). What a good is worth to a village or a household
-- (its stock against what it wants), what one village knows of another's wants (only what people
-- who went there or came from there saw, fading with time), and the best exchange a trader could
-- make with what they know. Trade happens because both sides gain: a village or household with
-- more of a good than it wants (worth little at home) carries it to one that wants it (worth much
-- there), and takes home what it wants itself and the other has to spare.
--
-- Goods are kept in two ways: food and logs are the village's (the granary, the woodstore and the
-- houses' larders together: a village of kin shares its stores, D-101); wool and cloth are a
-- household's own.
--   s.know[o.id] = { t = hour seen, buy = { good -> worth there }, sell = { good -> worth there },
--                    spare = { good -> amount to spare there } }
--   s.rel[o.id]  = { trust = 0..1, deals, gifts, lost, t }  (D-101 relations)
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local clock = require("sim.agents.clock")

local M = {}
local max, min, sqrt, exp, floor = math.max, math.min, math.sqrt, dmath.exp, math.floor
local function C() return cfgmod.V3 end

-- base: worth in FU of grain a unit at full want; wt: share of a porter's load a unit; pool:
-- "village" (worth by the village's stock) or "house" (by the household's).
M.GOODS = {
  grain = { base = 1.0, wt = 1 / 10, pool = "village", food = true, name = "grain", unit = "FU" },
  smoked = { base = 1.2, wt = 1 / 8, pool = "village", food = true, name = "smoked fish", unit = "FU" },
  cheese = { base = 1.4, wt = 1 / 8, pool = "village", food = true, name = "cheese", unit = "FU" },
  logs = { base = 1.5, wt = 1 / 2, pool = "village", name = "logs", unit = "", plural = true },
  wool = { base = 3.0, wt = 1 / 3, pool = "house", name = "wool", unit = "GE" },
  cloth = { base = 6.0, wt = 1 / 3, pool = "house", name = "cloth", unit = "GE" },
  hides = { base = 2.0, wt = 1 / 3, pool = "house", name = "hides", unit = "", plural = true },
  leather = { base = 4.0, wt = 1 / 4, pool = "house", name = "leather", unit = "pieces" },
  -- (S18) made only on the coast (salt pans); cures meat and fish, keeps cheese
  salt = { base = 2.5, wt = 1 / 10, pool = "house", name = "salt", unit = "measures" },
  -- (S19) a rotary quern of hard stone: made only where there is hard rock; a household's own,
  -- whole (one to a load: a man carries one pair of stones)
  quern = { base = 30, wt = 0.8, pool = "house", name = "querns", name1 = "quern", unit = "", whole = true },
  -- (S19) iron edges for a household's tools (axe bit, sickle, hoe and spade edges, knife): made only
  -- at a forge, from ore that only some places have
  iron = { base = 40, wt = 0.25, pool = "house", name = "sets of iron edges", name1 = "set of iron edges for tools", unit = "", whole = true },
  -- (S15, R4) beasts sold alive: a household's own, led by a drover (wt: the share of the beasts
  -- one drover can lead), never carried, never taken in payment
  sheep = { base = 12, wt = 1 / 6, pool = "house", name = "sheep", name1 = "sheep", unit = "", beast = "flock" },
  goat = { base = 10, wt = 1 / 6, pool = "house", name = "goats", name1 = "goat", unit = "", beast = "flock" },
  cattle = { base = 70, wt = 1 / 2, pool = "house", name = "cattle", name1 = "cow", unit = "", beast = "cattle" },
  pig = { base = 28, wt = 1 / 4, pool = "house", name = "pigs", name1 = "pig", unit = "", beast = "swine" },
}
M.ORDER = { "grain", "smoked", "cheese", "logs", "wool", "cloth", "hides", "leather", "salt", "quern", "iron", "sheep", "goat", "cattle", "pig" }

-- "8.0 FU of smoked fish", "2 logs": an amount of a good in words.
function M.amount(q, g)
  local G = M.GOODS[g]
  if (G.beast or G.whole) and q < 1.5 then return "a " .. G.name1 end
  if G.unit == "" then
    -- (S19: "0.4 hides", not "0 hides")
    if q < 9.5 and math.abs(q - floor(q + 0.5)) >= 0.05 then return sfmt("%.1f %s", q, G.name) end
    return sfmt("%.0f %s", q, G.name)
  end
  return sfmt("%.1f %s of %s", q, G.unit, G.name)
end

-- Worth a unit at stock `have` against want `want`: dear when short, cheap when there is plenty.
local function worth(g, have, want)
  local G = M.GOODS[g]
  local floor_v = G.base * 0.15
  if G.beast then
    -- (S15) a beast is never worth less than what killing it gives: its meat and its hide
    local S = cfgmod.V3.stock[g]
    floor_v = S.meat + (S.hide or 0) * M.GOODS.hides.base * 0.6
  end
  if want <= 0 then return floor_v end
  -- (S19) a durable whole good one has enough of - a second quern, a second set of iron edges - is
  -- worth little to a household
  if G.whole and have >= want then return floor_v end
  local v = 1.6 - have / want
  if v > 1.6 then v = 1.6 end
  return max(floor_v, G.base * v)
end
M.worth = worth

-- A village's own stock and want of a pooled good.
function M.village_stock(W, s, g)
  local c = C()
  local pop = max(1, s.pop or #s.members)
  local gr = s.granary and s.granary.stock or nil
  if g == "logs" then
    return s.wood_have or 0, s.wood_want or c.store_logs
  end
  local have = 0
  if g == "grain" then
    have = gr and ((gr.grain or 0) + (gr.sheaves or 0)) or 0
  else
    -- (smoked fish and cheese are the relish that keeps: worth by the village's store of both)
    have = gr and ((gr.smoked or 0) + (gr.cheese or 0)) or 0
  end
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b.kind == "house" or b.kind == "camp" then
      local st = b.stock
      if g == "grain" then have = have + (st.food or 0)
      else have = have + (st.smoked or 0) + (st.fish or 0) + (st.cheese or 0) end
    elseif b.kind == "rack" and g ~= "grain" then
      have = have + (b.stock.dried or 0) + (b.stock.drying or 0)
    end
  end
  local want
  if g == "grain" then want = s.grain_target or pop * 30
  else want = pop * c.fish_want end
  return have, want
end

-- (S15, R4) Beasts: a household's head of a kind, what it wants to keep and how many it would sell.
-- Want comes from fodder: with the hay in (autumn and winter), a village whose stack would winter
-- more beasts than it keeps has room - shared among its households by the beasts of the kind each
-- keeps, one more each so that a household with none may want some - and a village whose stack
-- falls short has beasts marked for killing (stock.plan_cull), which a household would sooner sell
-- alive; a herd with no grown sire wants one. Counted once a game hour (runtime).
local CENSUS = setmetatable({}, { __mode = "k" })
local function census(W, s, now)
  local h = floor(now)
  local cen = CENSUS[s]
  if cen and cen.h == h then return cen end
  cen = { h = h, hh = {}, n = {}, owners = {}, sire = {}, room = 0 }
  CENSUS[s] = cen
  local stock = require("sim.agents.stock")
  local se = 0
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    if H then
      for _, a in ipairs(H.animals) do
        if a.alive and (a.led or 0) == 0 then
          local S = stock.SP(a.sp)
          se = se + S.se
          local e = cen.hh[a.owner]
          if not e then e = {}; cen.hh[a.owner] = e end
          local f = e[a.sp]
          if not f then
            f = { n = 0, spare = 0 }
            e[a.sp] = f
            cen.owners[a.sp] = (cen.owners[a.sp] or 0) + 1
          end
          f.n = f.n + 1
          cen.n[a.sp] = (cen.n[a.sp] or 0) + 1
          if stock.sellable(a, a.owner, now) then f.spare = f.spare + 1 end
          if a.sex == "m" and not a.ox and (now - a.born) / 1152 >= S.adult_years then cen.sire[a.sp] = true end
        end
      end
    end
  end
  local c = C()
  if clock.local_doy(now, W.grid.lat[s.cell]) >= c.beast_room_doy then
    cen.room = (s.hay or 0) / (c.hay_days * c.sheep_hunger * 24 * 0.6) - se
  end
  return cen
end
function M.beast_stock(W, s, hh, g, now)
  local G = M.GOODS[g]
  local H = s and s.herds and s.herds[G.beast]
  if not H then return 0, 0, 0 end      -- (no pen for them: neither kept nor wanted)
  local cen = census(W, s, now)
  local e = cen.hh[hh.id] and cen.hh[hh.id][g]
  local have, spare = e and e.n or 0, e and e.spare or 0
  local want = have - spare
  local se = require("sim.agents.stock").SP(g).se
  if cen.room > 0 and se > 0 then
    want = want + cen.room / se * (have + 1) / ((cen.n[g] or 0) + max(1, cen.owners[g] or 0))
  end
  if not cen.sire[g] and have > 0 then want = want + 1 end
  -- (no room and none of its own: not wanted at all)
  return have, max(0, want), spare
end

-- A household's own stock and want of a house good (textiles: wool and cloth stand for the whole
-- pipeline from raw wool and flax to cloth).
function M.house_stock(W, hh, home, g)
  local c = C()
  if M.GOODS[g].beast then return M.beast_stock(W, W.S.v3.settlements[hh.sett], hh, g, W.S.v3.now or W.hour) end
  local st = home.stock
  local n = max(1, #hh.members)
  if g == "wool" or g == "cloth" then
    local have = (st.wool or 0) + (st.flax or 0) + (st.fibre or 0) + (st.yarn or 0) + (st.cloth or 0)
    return have, n * c.textile_hh_want, (g == "wool" and (st.wool or 0) or (st.cloth or 0))
  end
  if g == "hides" or g == "leather" then
    -- (hides and leather stand for the household's shoes to come: D-104)
    local have = (st.hides or 0) + (st.leather or 0) * 1.2
    return have, n * c.leather_want, st[g] or 0
  end
  if g == "salt" then
    -- (S18) for the curing and the cheese of a year
    return st.salt or 0, c.salt_hh_want + n * c.salt_member, st.salt or 0
  end
  if g == "quern" then
    -- (S19) one at work in the house; any more are to spare. A household whose village has hard rock
    -- makes its own (or is making it): it does not buy one
    local own = (hh.quern or 0) >= c.quern_broken or (hh.quern_work or 0) > 0
    local vs = W.S.v3.settlements[hh.sett]
    if not own and vs and vs.has_hard then own = true end
    return (own and 1 or 0) + (st.quern or 0), 1, st.quern or 0
  end
  if g == "iron" then
    -- (S19) the iron edges on the household's tools (worn ones count for little); any more to spare.
    -- A household whose village works iron (a forge, ore known) gets its edges from its own smith
    local own = (hh.iron or 0) >= c.iron_redo
    local vs = W.S.v3.settlements[hh.sett]
    if not own and vs and vs.ore_known and vs.forge then own = true end
    return (own and 1 or 0) + (st.iron or 0), 1, st.iron or 0
  end
  return 0, 0, 0
end

-- What more fish is worth to a fishing household (a factor about 1 at the village's want): the
-- village's own want of it, or the best a known neighbour would give, less the walk. Worked out in
-- the settlement's hour (settle.hourly) and kept in s.fish_worth.
function M.fish_worth(W, s, now)
  local c = C()
  local have, want = M.village_stock(W, s, "smoked")
  local v = worth("smoked", have, want)
  if s.know then
    local trade = require("sim.agents.trade")
    for _, o in ipairs(trade.partners(W, s)) do
      local b = M.expect(W, s, o, "smoked", now)
      local _, hrs = trade.road(W, s, o)
      -- (a load's worth there, less the days it takes to carry it)
      local net = b - 2 * hrs * c.trade_hour * M.GOODS.smoked.wt
      if net > v then v = net end
    end
  end
  return min(1.2, max(0.25, v / M.GOODS.smoked.base))
end

-- What good g is worth to a household (pooled goods: to its village).
function M.value(W, s, hh, home, g)
  local G = M.GOODS[g]
  if G.pool == "village" then
    local have, want = M.village_stock(W, s, g)
    return worth(g, have, want)
  end
  if not home then return G.base * 0.6 end
  local have, want = M.house_stock(W, hh, home, g)
  return worth(g, have, want)
end

-- How much of good g a household (or its village) can spare: beyond `spare_k` x what it wants,
-- and never more than the household holds of the good itself.
function M.spare(W, s, hh, home, g)
  local c = C()
  if M.GOODS[g].pool == "village" then
    local have, want = M.village_stock(W, s, g)
    return max(0, have - want * c.spare_k)
  end
  if not home then return 0 end
  local have, want, own = M.house_stock(W, hh, home, g)
  -- (beasts: those the hay will not winter, whole)
  if M.GOODS[g].beast then return own end
  return max(0, min(own, have - want * c.house_spare_k))
end

-- What a visitor sees of village o: for each good what the neediest there would give for it
-- (buy), what those with plenty would let it go for (sell) and how much is to spare.
function M.market(W, o)
  local V = W.S.v3
  local buy, sell, spare = {}, {}, {}
  for _, g in ipairs(M.ORDER) do
    local G = M.GOODS[g]
    if G.pool == "village" then
      local have, want = M.village_stock(W, o, g)
      local v = worth(g, have, want)
      buy[g], sell[g], spare[g] = v, v, max(0, min(have - want * C().spare_k, M.packable(W, o, nil, g)))
    else
      local hi, lo, sp = 0, 1e9, 0
      for _, hid in ipairs(o.households) do
        local hh = V.households[hid]
        if hh and #hh.members > 0 and hh.home ~= 0 then
          local home = o.buildings[hh.home]
          if home then
            local v = M.value(W, o, hh, home, g)
            if v > hi then hi = v end
            local q = M.spare(W, o, hh, home, g)
            if q > 0 and v < lo then lo = v end
            if q > sp then sp = q end
          end
        end
      end
      buy[g], sell[g], spare[g] = hi, lo < 1e9 and lo or G.base * 1.6, sp
    end
  end
  return buy, sell, spare
end

-- (S19) What a visitor sees of how village o's people and stores stand, good by good: each
-- household's worth of the household goods, what it could spare, has and wants; the stores' (the
-- village's goods) the same. Deals are worked out from it (M.deal_plan) - on arrival from what is
-- there now, and when planning a trip from what was seen last time.
local HOUSE_G = { "wool", "cloth", "hides", "leather", "salt", "quern", "iron" }
local VILLAGE_G = { "grain", "smoked", "cheese", "logs" }
function M.view(W, o)
  local c = C()
  local V = W.S.v3
  local st = { v = {}, sp = {}, have = {}, want = {} }
  for _, g in ipairs(VILLAGE_G) do
    local have, want = M.village_stock(W, o, g)
    -- (S19) what can be paid in it: the store that would pay holds that much of this very good
    -- (grain from the granary, logs from the woodstore, the relish from the granary or the larder
    -- holding most of it: smoked fish and cheese are counted together as relish, paid apart)
    local pay
    if g == "grain" then pay = o.granary and (o.granary.stock.grain or 0) or 0
    elseif g == "logs" then pay = o.wood_store and (o.wood_store.stock.logs or 0) or 0
    else
      pay = o.granary and (o.granary.stock[g] or 0) or 0
      for k = 1, #o.blist do
        local hb = o.buildings[o.blist[k]]
        if hb.kind == "house" and (hb.stock[g] or 0) > pay then pay = hb.stock[g] end
      end
    end
    st.v[g], st.sp[g], st.have[g], st.want[g] = worth(g, have, want), max(0, min(have - want * c.spare_k, pay)), have, want
  end
  local hhs = {}
  for _, hid in ipairs(o.households) do
    local hh = V.households[hid]
    local home = hh and #hh.members > 0 and hh.home ~= 0 and o.buildings[hh.home]
    if home then
      local e = { id = hid, v = {}, sp = {}, have = {}, want = {} }
      for _, g in ipairs(HOUSE_G) do
        local have, want = M.house_stock(W, hh, home, g)
        e.v[g], e.sp[g], e.have[g], e.want[g] = worth(g, have, want), M.spare(W, o, hh, home, g), have, want
      end
      hhs[#hhs + 1] = e
    end
  end
  return { store = st, hh = hhs }
end

-- (S19) The deals a trader with `left` of good X (worth aX at home), wanting H (worth aH), would make
-- with the people of view: the village's stores take pooled goods and pay in pooled goods; otherwise
-- the three neediest households of X, each while it is still worth the price to them, paying in what
-- they can spare (their own goods, or their share of the stores). Each { b = household id (0: the
-- stores), q, qh, k } into `out`. Not for beasts (M.deals).
local ORDER_C = setmetatable({}, { __mode = "k" })
local function neediest(view, X, vill)
  local oc = ORDER_C[view]
  if not oc then oc = {}; ORDER_C[view] = oc end
  local L = oc[X]
  if L then return L end
  L = {}
  for i = 1, #view.hh do L[i] = view.hh[i] end
  if vill then
    table.sort(L, function(a, b) return a.id < b.id end)
  else
    table.sort(L, function(a, b) if a.v[X] ~= b.v[X] then return a.v[X] > b.v[X] end return a.id < b.id end)
  end
  for i = #L, 4, -1 do L[i] = nil end
  oc[X] = L
  return L
end
-- (S19) A whole good paid for with a basket of what household entry e of the view can spare (its
-- household goods, the trader's favourites first - the trader's worth of each over the buyer's), at
-- the middle of the two worths of the good; what it still falls short may be owed (credit) when the
-- villages trust each other enough. Returns the basket { {g, q} }, the debt (FU), the basket's
-- weight and its worth to the trader (the debt counted at credit_k), or nil.
local BASKET = {}
local function basket(c, e, X, aX, vX, aw, wcap, opt)
  if not aw then return nil end
  local n = 0
  for gi = 1, #HOUSE_G do
    local g = HOUSE_G[gi]
    local sp = e.sp[g] or 0
    if g ~= X and sp > 0.05 and (aw[g] or 0) > 0 and (e.v[g] or 0) > 0 then
      n = n + 1
      local it = BASKET[n]
      if not it then it = {}; BASKET[n] = it end
      it.g, it.sp, it.vs, it.vb, it.i = g, sp, aw[g], e.v[g], gi
    end
  end
  for i = n + 1, #BASKET do BASKET[i] = nil end
  table.sort(BASKET, function(a, b) local ra, rb = a.vs / a.vb, b.vs / b.vb; if ra ~= rb then return ra > rb end return a.i < b.i end)
  local need_s = max(aX * c.trade_margin, sqrt(aX * vX))
  local lim_b = vX / c.trade_margin
  local got, cost, w = 0, 0, 0
  local out = {}
  for i = 1, n do
    local it = BASKET[i]
    if got < need_s - 1e-6 then
      local q = min(it.sp, (need_s - got) / it.vs, (lim_b - cost) / it.vb, (wcap - w) / M.GOODS[it.g].wt)
      if q > 0.05 then
        out[#out + 1] = { it.g, q }
        got, cost, w = got + q * it.vs, cost + q * it.vb, w + q * M.GOODS[it.g].wt
      end
    end
  end
  local owe = 0
  if got < need_s - 1e-6 then
    -- (short: the rest owed, if trusted, not already in debt, and both still gain by it)
    if not opt or (opt.trust or 0) < c.credit_trust or (opt.indebted and opt.indebted[e.id]) then return nil end
    -- (something paid down in kind: nothing changes hands on credit alone)
    if got < need_s * c.credit_down then return nil end
    owe = min(c.credit_max, need_s - got, lim_b - cost)
    if owe <= 0 or got + owe * c.credit_k < aX * c.trade_margin then return nil end
  end
  if #out == 0 and owe <= 0 then return nil end
  return out, owe, w, got + owe * c.credit_k
end

function M.deal_plan(view, X, H, aX, aH, left, cap, out, opt)
  local c = C()
  local GX, GH = M.GOODS[X], M.GOODS[H]
  local room = (cap or 1) / GH.wt
  for i = #out, 1, -1 do out[i] = nil end
  local function try(b, vX, vH, spH, haveX, wantX, e)
    if GX.whole and e and left >= 1 and opt and opt.aw then
      -- (S19) a whole good: a basket of what they can spare, and the rest owed, when no one good pays
      local qk = (spH > 1e-6 and vX / vH > aX / aH * c.trade_margin) and min(spH, room) or 0
      if not (qk * aH >= aX * c.trade_margin and vX >= qk * vH * c.trade_margin) and vX > aX * c.trade_margin then
        local bk, owe, w, val = basket(c, e, X, aX, vX, opt.aw, cap or 1, opt)
        if bk then
          out[#out + 1] = { b = b, q = 1, qh = 0, k = 0, basket = bk, owe = owe, val = val }
          left = left - 1
          return
        end
      end
    end
    if left <= 1e-6 or room <= 1e-6 or spH <= 1e-6 then return end
    if left <= 1e-6 or room <= 1e-6 or spH <= 1e-6 then return end
    if vX / vH <= aX / aH * c.trade_margin then return end
    local k = sqrt(aX * vX / (aH * vH))
    -- (they take it while it is still worth the price to them)
    local q = left
    if wantX > 0 then q = min(q, max(0, (1.6 - k * vH / GX.base) * wantX - haveX)) end
    q = min(q, spH / k, room / k)
    if GX.whole then
      -- (S19) a whole one, or none - or a whole one for all that can be paid and carried, when both
      -- still gain by it
      local qf = floor(q + 1e-6)
      if qf < 1 and left >= 1 then
        local keff = min(room, spH)
        if keff * aH >= aX * c.trade_margin and vX >= keff * vH * c.trade_margin then qf, k = 1, keff end
      end
      q = qf
      if q < 1 then return end
    elseif GH.whole then
      local qh = floor(q * k + 1e-6)
      if qh < 1 then return end
      q = qh / k
    end
    if q * GX.wt < c.trade_min_load * 0.5 and not GX.whole then return end
    out[#out + 1] = { b = b, q = q, qh = q * k, k = k, val = q * k * aH }
    left, room = left - q, room - q * k
  end
  local st = view.store
  local vX_, vH_ = GX.pool == "village", GH.pool == "village"
  if vX_ and vH_ then
    try(0, st.v[X], st.v[H], st.sp[H], st.have[X], st.want[X])
  else
    local L = neediest(view, X, vX_)
    for i = 1, #L do
      local e = L[i]
      local vX = vX_ and st.v[X] or e.v[X]
      local vH = vH_ and st.v[H] or e.v[H]
      local spH = vH_ and st.sp[H] or e.sp[H]
      if vX_ then try(e.id, vX, vH, spH, st.have[X], st.want[X], e) else try(e.id, vX, vH, spH, e.have[X], e.want[X], e) end
    end
  end
  return out
end

-- (S19) Debts between households (credit for what a basket could not pay): W.S.v3.debts, a list of
-- { cr = creditor household, db = debtor household, crs, dbs = their villages, fu, t, what }.
function M.debts(W)
  local D = W.S.v3.debts
  if not D then D = {}; W.S.v3.debts = D end
  return D
end
-- The households of village o in debt (a set), counted once a game hour (runtime).
local INDEBT = setmetatable({}, { __mode = "k" })
function M.indebted(W, o)
  local h = floor(W.S.v3.now or W.hour)
  local e = INDEBT[o]
  if e and e.h == h then return e.set end
  local set = {}
  for _, d in ipairs(M.debts(W)) do if d.dbs == o.id and d.fu > 0.05 then set[d.db] = true end end
  INDEBT[o] = { h = h, set = set }
  return set
end
-- What village s's people are owed at village o: the debts, oldest first.
function M.owed_at(W, s, o)
  local L = {}
  for i, d in ipairs(M.debts(W)) do if d.crs == s.id and d.dbs == o.id and d.fu > 0.05 then L[#L + 1] = i end end
  return L
end
-- Each morning at the debtor's village: old debts written off; trust falls both ways and the creditors
-- remember it.
function M.debts_daily(W, s, now)
  local c = C()
  local V = W.S.v3
  local D = M.debts(W)
  for i = #D, 1, -1 do
    local d = D[i]
    if d.fu <= 0.05 then table.remove(D, i)
    elseif d.dbs == s.id and now - d.t > c.credit_days * 24 then
      local cs = V.settlements[d.crs]
      if cs then
        local r1, r2 = M.rel(cs, s), M.rel(s, cs)
        r1.trust, r2.trust = max(0, r1.trust - c.credit_loss), max(0, r2.trust - c.credit_loss * 0.5)
        local ch = V.households[d.cr]
        local dh = V.households[d.db]
        local dq = dh and V.people[dh.members[1] or 0]
        for _, id in ipairs(ch and ch.members or {}) do
          local q = V.people[id]
          if q and q.alive then
            require("sim.agents.people").remember(q, now, sfmt("The %s household of %s never paid the %.0f FU they owed us for the %s", dq and dq.surname or "", s.name, d.fu, d.what))
          end
        end
      end
      s.acc.debts_lost = (s.acc.debts_lost or 0) + 1
      table.remove(D, i)
    end
  end
end

-- Village s learns how things stand at o (someone of s is there, or someone of o came to s).
function M.learn(W, s, o, now)
  if s == o then return end
  s.know = s.know or {}
  local buy, sell, spare = M.market(W, o)
  s.know[o.id] = { t = now, buy = buy, sell = sell, spare = spare, view = M.view(W, o) }
  -- (S20, R6a) the beasts in its pens and the dogs that keep them, as a visitor sees them; whether
  -- its people were keeping watch at night
  local k = s.know[o.id]
  local stock = require("sim.agents.stock")
  k.beasts = {}
  for hi, kind in ipairs(stock.HERDS) do
    local H = o.herds and o.herds[kind]
    local n = 0
    if H then for _, a in ipairs(H.animals) do if a.alive and (a.led or 0) == 0 then n = n + 1 end end end
    k.beasts[hi] = n
  end
  local nd = 0
  for _, d in ipairs(o.dogs or {}) do if d.alive then nd = nd + 1 end end
  k.dogs = nd
  -- (S20, R6f) the pens walled
  k.walled = {}
  for _, kind in ipairs(stock.HERDS) do
    local H = o.herds and o.herds[kind]
    local b = H and o.buildings[H.pen]
    if b and (b.wall or 0) >= 1 then k.walled[kind] = true end
  end
  k.watch = (o.wolf_alarm and now - o.wolf_alarm < C().guard_days * 24) or (o.thief_alarm and now - o.thief_alarm < C().guard_days * 24) or nil
  -- (S17) and what o's people know of places (founding.share; set by sim/agents/init.lua)
  if M.on_learn then M.on_learn(W, s, o, now) end
end

-- What s expects good g to fetch at o (buy) and to cost there (sell), and how much o has to spare:
-- what it last saw there (S19: kept as seen - a glut seen a week ago is not guessed away; how far a
-- trader trusts old news is the trip's own weighing, best_trade). A village never heard of: a guess
-- that someone there would give a middling price and has a little of anything to spare (half a load).
function M.expect(W, s, o, g, now)
  local G = M.GOODS[g]
  local k = s.know and s.know[o.id]
  local neutral = G.base * 0.6
  if not k then return neutral, neutral * 1.5, 0.5 / G.wt end
  return k.buy[g] or neutral, k.sell[g] or neutral * 1.5, k.spare[g] or 0
end

-- (S19) How much of village good g a trader of the village could carry off: from the household's
-- own house (when known) and the village's store of it (grain and the relish from the granary, logs
-- from the woodstore) - not the other houses' larders.
function M.packable(W, s, home, g)
  local key = g == "grain" and "food" or g
  local store = g == "logs" and s.wood_store or s.granary
  local q = store and store.state ~= "site" and (store.stock[g] or 0) or 0
  if home then q = q + (home.stock[key] or 0) end
  return q
end

-- How fresh s's news of o is: 1 just heard, fading to 0 (never heard) over know_fade days (S14).
function M.fresh(W, s, o, now)
  local k = s.know and s.know[o.id]
  if not k then return 0 end
  return exp(-(now - k.t) / (C().know_fade * 24))
end

-- The relation between two villages (D-101): trust from deals kept, gifts and help.
function M.rel(s, o)
  s.rel = s.rel or {}
  local r = s.rel[o.id]
  if not r then r = { trust = 0.2, deals = 0, gifts = 0, lost = 0, t = 0 }; s.rel[o.id] = r end
  return r
end

-- (S19) One exchange as a trader of worths aX (the good given) and aH (the good brought home) would
-- expect it at o: o's people give bX for X, let H go at sH and have spH
-- to spare; nmax of X can be carried. Returns n of X, qH of H, the rate k and the gain at home -
-- or nil when it would not be a deal there (too little for anyone to bother: trade_min_load).
local DEALS_TMP = {}
local AW_C = setmetatable({}, { __mode = "k" })
local function exchange(c, aX, bX, nmax, aH, sH, spH, cap, GX, GH)
  if spH <= 0 or bX <= aX * c.trade_margin or bX * aH / (aX * sH) <= c.trade_margin then return nil end
  if not GX.beast and nmax * GX.wt < c.trade_min_load * 0.5 then return nil end
  local k = sqrt(aX * bX / (aH * sH))
  local qH = min(nmax * k, cap / GH.wt, spH)
  if GH.whole then qH = floor(qH + 1e-6); if qH < 1 then return nil end end
  local n = qH / k
  if GX.beast or GX.whole then
    n = floor(n + 1e-6)
    if n < 1 and nmax >= 1 and qH * aH >= aX * c.trade_margin and bX >= qH * sH * c.trade_margin then
      -- (a whole beast for all that can be carried back, when both still gain by it)
      n, k = 1, qH
    end
    if n < 1 then return nil end
    qH = n * k
  elseif n * GX.wt < c.trade_min_load * 0.5 then
    return nil
  end
  return n, qH, k, qH * aH - n * aX
end

-- The best exchange a trader of household hh (in village s) could set out to make, by what s
-- knows: returns { o, give, n, take, k, gain, cost, hours } or nil. `pool`: "village" plans the
-- village's goods (food, logs), "house" the household's own (wool, cloth).
-- `route(W, s, o)` gives the hours one way, the loads that can be carried and the people it takes
-- (by road: one porter, one load; by sea: a boat's hold and its crew of two, D-102).
function M.best_trade(W, s, hh, home, pool, partners, route, now)
  local c = C()
  local best = nil
  local land = route == M.land_route
  for gi = 1, #M.ORDER do
    local X = M.ORDER[gi]
    local GX = M.GOODS[X]
    -- (S18: by sea only small beasts, a few in a boat: sheep and goats - never cattle, and the pigs
    -- sold are grown)
    if GX.pool == pool and (land or not GX.beast or GX.beast == "flock") then
      local spareX = M.spare(W, s, hh, home, X)
      -- (S19) the village's goods a trader can really carry off: their own house's and the store's
      if GX.pool == "village" then spareX = min(spareX, M.packable(W, s, home, X)) end
      local aX = M.value(W, s, hh, home, X)
      -- (S19) the trader's own worth of every good: what a basket paid in is worth to them, and what
      -- else they would take if what they meant to bring home is not to be had
      local aw = AW_C[s]
      if not aw or aw.hh ~= (hh and hh.id or 0) or aw.t ~= now then
        aw = { hh = hh and hh.id or 0, t = now }
        for gi = 1, #M.ORDER do
          local g = M.ORDER[gi]
          if not M.GOODS[g].beast then aw[g] = M.value(W, s, hh, home, g) end
        end
        AW_C[s] = aw
      end
      for pi = 1, #partners do
        local o = partners[pi]
        local hrs, cap, crew = route(W, s, o)
        -- (beasts are driven slower than a walker, D-115; S18: a boat holds only a few)
        if GX.beast and land then hrs = hrs * c.drove_road end
        local nmax = min(spareX, cap / GX.wt)
        if GX.beast and not land then nmax = min(nmax, c.mig_sea_beasts) end
        if GX.beast or GX.whole then nmax = floor(nmax + 1e-6) end
        if nmax >= (GX.whole and 1 or c.trade_min_load / GX.wt) then
          local r = M.rel(s, o)
          -- (S19) what they expect is two things: what was last seen there (counted by how fresh the
          -- news is) and, for what may have changed since, a guess (a middling price, a little to
          -- spare and to take: S14's trade_guess) - the gains of both, not a blend of the prices
          -- (a glut seen a week ago does not turn into a market as the news ages)
          local kn = s.know and s.know[o.id]
          -- (what others have and want changes with their seasons: a trader trusts what was seen there
          -- for about trade_fade days - longer than that, it may well have changed)
          local w = kn and exp(-(now - kn.t) / (c.trade_fade * 24)) or 0
          -- (S17: the road's cost is the work given up for it: less where hands are idle - the
          -- bread is eaten at home or on the road alike)
          local cost = 2 * hrs * crew * c.trade_hour * max(c.trade_idle_min, 1 - c.trade_idle_k * (s.idle_avg or 0))
          -- (S19) by sea the danger is weighed too: the chance of foundering on the way there and back
          -- in the wet weather the sea mostly has, for each life aboard
          if not land then cost = cost + 2 * hrs * c.sea_squall * c.sea_wet_k * 1.2 * crew * c.sea_life_fu end
          local neutralX, guessX = GX.base * 0.6, 0.5 / GX.wt
          local bXo = kn and (kn.buy[X] or neutralX) or 0
          -- the good to bring home: what this household (or village) wants and o can spare - S19:
          -- each weighed by the whole exchange it would make (what o can pay with decides how
          -- much can be sold), not by its price alone
          for hi = 1, #M.ORDER do
            local H = M.ORDER[hi]
            local GH = M.GOODS[H]
            if H ~= X and not GH.beast and (pool ~= "village" or GH.pool == "village") then
              local aH = M.value(W, s, hh, home, H)
              local go, n, qH, k = 0, 0, 0, 0
              if kn and w > 0 and kn.view and not GX.beast then
                -- (the deals they would have made with what was seen there, household by household)
                local out = M.deal_plan(kn.view, X, H, aX, aH, nmax, cap, DEALS_TMP,
                  GX.whole and { aw = aw, trust = r.trust, indebted = M.indebted(W, o) } or nil)
                local q1, h1, v1 = 0, 0, 0
                for di = 1, #out do q1, h1, v1 = q1 + out[di].q, h1 + out[di].qh, v1 + out[di].val end
                if q1 > 0 then go, n, qH, k = (v1 - q1 * aX) * w, q1, h1, q1 > 0 and h1 / q1 or 0 end
              elseif kn and w > 0 then
                -- (beasts: by the prices seen)
                local n1, q1, k1, g1 = exchange(c, aX, bXo, nmax, aH, kn.sell[H] or GH.base * 0.9, kn.spare[H] or 0, cap, GX, GH)
                if n1 then go, n, qH, k = g1 * w, n1, q1, k1 end
              end
              if w < 1 then
                local n2, q2, k2, g2 = exchange(c, aX, neutralX, min(nmax, GX.beast and nmax or guessX), aH, GH.base * 0.9,
                  0.5 / GH.wt, cap, GX, GH)
                if n2 then
                  go = go + g2 * (1 - w) * c.trade_guess
                  -- (nothing to be had by what was seen: they go on the chance, as much as a guess)
                  if n == 0 then n, qH, k = n2, q2, k2 end
                end
              end
              if M.dbg then M.dbg(s, hh, X, o, H, w, go, cost, n, aX, aH, nmax, bXo, 0) end
              if n > 0 then
                local net = go * (1 + 0.5 * r.trust) - cost
                if net > 0 and (not best or net > best.net) then
                  -- (the worths travel with the trader: a copy, the cache is reused)
                  local awc = {}
                  for g2, v2 in pairs(aw) do if type(v2) == "number" and g2 ~= "t" and g2 ~= "hh" then awc[g2] = v2 end end
                  best = { o = o.id, give = X, n = n, take = H, k = k, gain = qH * aH - n * aX, cost = cost, net = net, hours = hrs,
                    aX = aX, aH = aH, w = w, aw = awc, trust = r.trust }
                end
              end
            end
          end
        end
      end
    end
  end
  return best
end

-- The deals a trader makes on arrival at o, from what they find there (o's households and stores
-- now): a list of { b = household id (0: the village's stores), q = units of `give`, qh = units of
-- `take` }. The trader's own worths (aX, aH) are those they set out with.
function M.deals(W, o, tr, cap)
  local c = C()
  local V = W.S.v3
  local X, H = tr.give, tr.take
  local GX, GH = M.GOODS[X], M.GOODS[H]
  local left, room = tr.have or tr.n, (cap or 1) / GH.wt
  local out = {}
  local function try(b, vX, vH, spH, haveX, wantX, vbase)
    if left <= 1e-6 or room <= 1e-6 or spH <= 1e-6 then return end
    if vX / vH <= tr.aX / tr.aH * c.trade_margin then return end
    local k = sqrt(tr.aX * vX / (tr.aH * vH))
    -- (they take it while it is still worth the price to them)
    local q = left
    if wantX > 0 then q = min(q, max(0, (1.6 - k * vH / vbase) * wantX - haveX)) end
    q = min(q, spH / k, room / k)
    if GX.beast then
      local qf = floor(q + 1e-6)
      if qf < 1 and left >= 1 then
        -- (a whole beast for what can be paid and carried, when both still gain by it)
        local keff = min(room, spH)
        if keff * tr.aH >= tr.aX * c.trade_margin and vX >= keff * vH * c.trade_margin then qf, k = 1, keff end
      end
      q = qf
      if q < 1 then return end
    end
    if q * GX.wt < c.trade_min_load * 0.5 then return end
    out[#out + 1] = { b = b, q = q, qh = q * k, k = k }
    left, room = left - q, room - q * k
  end
  -- (where what is paid is taken from: grain from the granary, logs from the woodstore, the
  -- relish from the larder that holds the most of it; a household's own goods from its house)
  local function source(b)
    if GH.pool == "village" then
      if H == "grain" then return o.granary and o.granary.id end
      if H == "logs" then return o.wood_store and o.wood_store.id end
      local best, bv = o.granary, o.granary and (o.granary.stock[H] or 0) or 0
      for k = 1, #o.blist do
        local hb = o.buildings[o.blist[k]]
        if hb.kind == "house" and (hb.stock[H] or 0) > bv then best, bv = hb, hb.stock[H] end
      end
      return best and best.id
    end
    local hh = V.households[b]
    return hh and hh.home
  end
  if not GX.beast then
    -- (S19) the same deals a planner works out from what it saw, on what is here now
    local view = M.view(W, o)
    local opt = tr.aw and { aw = tr.aw, trust = M.rel(V.settlements[tr.from], o).trust, indebted = M.indebted(W, o) } or nil
    M.deal_plan(view, X, H, tr.aX, tr.aH, left, cap, out, GX.whole and opt or nil)
    if #out == 0 and tr.aw and not GX.whole then
      -- (what they meant to bring home is not to be had: anything else worth their while that the
      -- people here can pay with)
      local bestH, bg = nil, 0
      local tmp = {}
      for hi = 1, #M.ORDER do
        local H2 = M.ORDER[hi]
        local aH2 = tr.aw[H2]
        if H2 ~= X and H2 ~= H and aH2 and aH2 > 0 and not M.GOODS[H2].beast
          and (GX.pool ~= "village" or M.GOODS[H2].pool == "village" or tr.pool ~= "village") then
          M.deal_plan(view, X, H2, tr.aX, aH2, left, cap, tmp)
          local g = 0
          for i = 1, #tmp do g = g + tmp[i].val - tmp[i].q * tr.aX end
          if g > bg then bestH, bg = H2, g end
        end
      end
      if bestH then
        tr.take, tr.aH = bestH, tr.aw[bestH]
        H, GH = bestH, M.GOODS[bestH]
        M.deal_plan(view, X, H, tr.aX, tr.aH, left, cap, out)
      end
    end
    for i = 1, #out do out[i].src = source(out[i].b) end
    return out
  end
  -- (beasts) the village's stores take pooled goods and pay in pooled goods
  if GX.pool == "village" and GH.pool == "village" then
    local hX, wX = M.village_stock(W, o, X)
    local hH, wH = M.village_stock(W, o, H)
    try(0, worth(X, hX, wX), worth(H, hH, wH), max(0, hH - wH * c.spare_k), hX, wX, GX.base)
  else
    -- households, the neediest first (a household pays in its own goods or its share of the stores)
    local L = {}
    for _, hid in ipairs(o.households) do
      local hh = V.households[hid]
      local home = hh and #hh.members > 0 and hh.home ~= 0 and o.buildings[hh.home]
      if home then L[#L + 1] = { hh = hh, home = home, v = M.value(W, o, hh, home, X) } end
    end
    table.sort(L, function(a, b) if a.v ~= b.v then return a.v > b.v end return a.hh.id < b.hh.id end)
    for i = 1, min(3, #L) do
      local e = L[i]
      local vH = M.value(W, o, e.hh, e.home, H)
      local spH = M.spare(W, o, e.hh, e.home, H)
      local hX, wX = 0, 0
      if GX.pool == "house" then hX, wX = M.house_stock(W, e.hh, e.home, X) else hX, wX = M.village_stock(W, o, X) end
      try(e.hh.id, e.v, vH, spH, hX, wX, GX.base)
    end
  end
  for i = 1, #out do out[i].src = source(out[i].b) end
  return out
end

-- Sharing within the village (D-101): a household with far more relish (smoked fish, cheese) or
-- wool and cloth than it wants takes some to a household that has little, kin first and those
-- who gave to it before. A gift, remembered by both (hh.got / hh.gave), not a sale.
local SHARE = { "smoked", "cheese", "wool", "cloth", "leather" }
local function hh_have(W, hh, home, g)
  local c = C()
  local n = max(1, #hh.members)
  local st = home.stock
  if g == "smoked" or g == "cheese" then
    return (st.smoked or 0) + (st.fish or 0) + (st.cheese or 0), n * c.fish_want, st[g] or 0
  end
  return M.house_stock(W, hh, home, g)
end

local function kin_of(W, a, b)
  local V = W.S.v3
  local pa, pb = V.people[a.members[1] or 0], V.people[b.members[1] or 0]
  return pa and pb and pa.surname == pb.surname
end

function M.plan_gift(W, s, hh, home)
  local c = C()
  local V = W.S.v3
  local best, bs = nil, 0
  for gi = 1, #SHARE do
    local g = SHARE[gi]
    local have, want, own = hh_have(W, hh, home, g)
    local spare = min(own, have - want * c.share_keep)
    if spare >= 0.5 then
      for _, hid in ipairs(s.households) do
        local o = V.households[hid]
        if o and o ~= hh and #o.members > 0 and o.home ~= 0 then
          local oh = s.buildings[o.home]
          if oh and oh.state == "standing" then
            local h2, w2 = hh_have(W, o, oh, g)
            if w2 > 0 and h2 < w2 * c.share_short then
              local need = 1 - h2 / w2
              local sc = need * (kin_of(W, hh, o) and 1.5 or 1) * (1 + 0.5 * min(2, (hh.got and hh.got[o.id] or 0) / 4))
              if sc > bs then
                local q = min(spare, w2 - h2, 1 / M.GOODS[g].wt)
                best, bs = { to = o.id, g = g, q = q, need = need }, sc
              end
            end
          end
        end
      end
    end
  end
  return best
end

-- Each morning (settle.daily): the best exchange the village could make with its goods, and each
-- household with its own, by what the village knows (D-101); and what each household might share.
function M.daily(W, s, now)
  local trade = require("sim.agents.trade")
  local V = W.S.v3
  M.debts_daily(W, s, now)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.home ~= 0 and s.buildings[hh.home] then
      hh.gift = M.plan_gift(W, s, hh, s.buildings[hh.home])
    elseif hh then
      hh.gift = nil
    end
  end
  local partners = trade.partners(W, s)
  if not s.know then
    -- (a village knows how its neighbours stood when it was founded or last heard: D-101)
    s.know = {}
    for _, o in ipairs(partners) do M.learn(W, s, o, now) end
    for _, o in ipairs(trade.sea_partners(W, s)) do if not s.know[o.id] then M.learn(W, s, o, now) end end
  end
  -- (S17) work for exchange: goods its news says a neighbour wants more than they are worth here
  M.exports(W, s, partners, now)
  s.tplan = #partners > 0 and M.best_trade(W, s, nil, nil, "village", partners, M.land_route, now) or nil
  -- (hungry and nothing to sell: ask the trusted neighbour for help, D-103)
  if not s.tplan and #partners > 0 then s.tplan = M.plan_ask(W, s, partners, now) end
  local sea = trade.sea_partners(W, s)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.home ~= 0 then
      local home = s.buildings[hh.home]
      hh.tplan = (home and #partners > 0) and M.best_trade(W, s, hh, home, "house", partners, M.land_route, now) or nil
      -- (a household with a sound boat may carry its own goods or the village's by sea, D-102)
      hh.splan = nil
      if home and #sea > 0 and M.own_boat(s, hh, true) then
        local a = M.best_trade(W, s, hh, home, "house", sea, M.sea_route, now)
        local b = M.best_trade(W, s, hh, home, "village", sea, M.sea_route, now)
        if b and (not a or b.net > a.net) then a = b; a.pool = "village" elseif a then a.pool = "house" end
        -- (S16, R4) hungry and nothing to sell: a boat may sail to ask a trusted village over the
        -- sea for help (not the one already being asked by road)
        if not a then
          a = M.plan_ask(W, s, sea, now, M.sea_route)
          if a and s.tplan and s.tplan.ask and s.tplan.o == a.o then a = nil end
        end
        hh.splan = a
      end
    end
  end
end

-- (S17) A small gift for kin at village o (a visit): of what household hh has beyond its own want, the
-- good o is heard to want most over what it is worth at home (at visit_gift_k x at least), as much
-- as is spare up to a little (VISIT_CAP). Returns good, amount (or nil).
local VISIT_GOODS = { "cloth", "wool", "leather", "hides", "smoked", "cheese" }
local VISIT_CAP = { cloth = 0.5, wool = 0.6, leather = 0.5, hides = 1, smoked = 4, cheese = 3 }
function M.visit_gift(W, s, hh, home, o, now)
  local c = C()
  if not home or not o then return nil end
  local best, bs, bn = nil, 0, 0
  for _, g in ipairs(VISIT_GOODS) do
    local sp
    if g == "smoked" or g == "cheese" then
      local have, want, own = hh_have(W, hh, home, g)
      sp = max(0, min(own, have - want * c.house_spare_k))
    else
      sp = M.spare(W, s, hh, home, g)
    end
    local n = min(sp, VISIT_CAP[g])
    if n >= VISIT_CAP[g] * 0.25 then
      local here = M.value(W, s, hh, home, g)
      local there = M.expect(W, s, o, g, now)
      if there > here * c.visit_gift_k then
        local sc = (there - here) * n
        if sc > bs then best, bs, bn = g, sc, n end
      end
    end
  end
  return best, bn
end

-- (S17) What village s would work to make more of for exchange, by its news of its neighbours (road
-- and sea): cloth (its households' looms and spindles), when a neighbour heard of lately would give
-- export_margin x trade_margin what a household here would. s.export = { cloth = name of the
-- neighbour } or nil.
local EXPORTS = { "cloth", "salt", "quern", "iron" }
function M.exports(W, s, partners, now)
  local c = C()
  local V = W.S.v3
  local ex = nil
  for _, g in ipairs(EXPORTS) do
    -- (what a household of s gives it, the lowest: those who would make more of it; S19: querns
    -- only where there is hard rock to make them from)
    local lo = nil
    if g == "quern" and not s.has_hard then lo = false end
    if g == "iron" and not (s.forge and s.forge.state == "standing" and s.ore_known) then lo = false end
    for _, hid in ipairs(lo ~= false and s.households or {}) do
      local hh = V.households[hid]
      local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
      if home and home.state == "standing" then
        local v = M.value(W, s, hh, home, g)
        if not lo or v < lo then lo = v end
      end
    end
    if lo then
      local best, bv = nil, lo * c.trade_margin * c.export_margin
      local function look(list)
        for _, o in ipairs(list) do
          if M.fresh(W, s, o, now) >= c.export_fresh then
            local b = M.expect(W, s, o, g, now)
            if b > bv then best, bv = o, b end
          end
        end
      end
      look(partners)
      look(require("sim.agents.trade").sea_partners(W, s))
      if best then ex = ex or {}; ex[g] = best.name end
    end
  end
  s.export = ex
end

-- Asking for help (D-103): a village whose food (grain and relish) would last under `relief_days`
-- days and which has nothing a neighbour wants sends someone to the partner it trusts most among
-- those it knows to have grain to spare: { o, take = "grain", n, ask = true } or nil.
function M.plan_ask(W, s, partners, now, route)
  local c = C()
  local pop = max(1, s.pop or #s.members)
  local g = M.village_stock(W, s, "grain")
  local r = M.village_stock(W, s, "smoked")
  local days = (g + r) / (pop * 0.7)
  if days >= c.relief_days then return nil end
  local best, bs = nil, 0
  for _, o in ipairs(partners) do
    if not route or (o.dock and o.granary) then
      local _, _, sp = M.expect(W, s, o, "grain", now)
      -- (S19) what was seen there may have changed: as the news ages, a village is hoped to have a
      -- little grain to spare (two loads) - hungry people ask on old news
      local kn = s.know and s.know[o.id]
      local w = kn and exp(-(now - kn.t) / (C().trade_fade * 24)) or 0
      sp = sp * w + 20 * (1 - w)
      local rel = M.rel(s, o)
      local sc = min(sp, route and 10 * c.boat_hold or 10) * (0.3 + rel.trust) * (1 + 0.2 * min(5, rel.gifts))
      if sp > 2 and sc > bs then best, bs = o, sc end
    end
  end
  if not best then return nil end
  local hrs
  if route then hrs = route(W, s, best) else local _; _, hrs = require("sim.agents.trade").road(W, s, best) end
  return { o = best.id, give = "grain", take = "grain", n = 0, ask = true, net = 1, cost = 0, hours = hrs, aX = 1, aH = 1, days = days,
    pool = route and "village" or nil }
end

-- What village o gives a neighbour who came asking for food: a share of its grain to spare by how
-- much it trusts them and what it owes them (help given before), at most what can be carried.
function M.relief_gift(W, o, s, cap)
  local c = C()
  local have, want = M.village_stock(W, o, "grain")
  local spare = max(0, have - want * c.spare_k)
  local r = M.rel(o, s)
  -- (what o owes s: help s gave o before)
  local owe = min(1, (r.owe_them or 0) / 20)
  local will = min(1, 0.2 + r.trust + owe)
  local gift = spare * will * 0.25
  -- (S19) people with nothing left to eat are given something even from what o keeps for itself:
  -- a share of what lies beyond relief_keep_days of its own bread (a village with weeks of bread
  -- had told a starving neighbour it had none to give)
  local sg = M.village_stock(W, s, "grain")
  if sg / max(1, (s.pop or #s.members) * 0.7) < c.relief_starving_days then
    local beyond = max(0, have - max(1, o.pop or #o.members) * 0.7 * c.relief_keep_days)
    gift = max(gift, beyond * will * c.relief_share)
  end
  return min(gift, (cap or 1) * 10)
end

-- Routes for best_trade: hours one way, loads, people.
function M.land_route(W, s, o)
  local _, hrs = require("sim.agents.trade").road(W, s, o)
  return hrs, 1, 1
end
function M.sea_route(W, s, o)
  local c = C()
  local r = require("sim.agents.trade").sea_route(W, s, o)
  return r.len / c.sail_speed, c.boat_hold, 2
end

-- A household's sound boat at its dock (not out, not away), or nil. (S19) `plan`: for the morning's
-- planning, a boat out fishing counts too (it will be back).
function M.own_boat(s, hh, plan)
  local c = C()
  for _, boat in ipairs(s.boats or {}) do
    if boat.owner == hh.id and not boat.lost and not boat.moved and not boat.move and (boat.cond or 1) > c.boat_unsafe + 0.15
      and (plan or boat.crew == 0) and not boat.away then
      return boat
    end
  end
  return nil
end

return M
