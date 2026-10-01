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
local cfgmod = require("sim.config")

local M = {}
local max, min, sqrt, exp, floor = math.max, math.min, math.sqrt, math.exp, math.floor
local function C() return cfgmod.V3 end

-- base: worth in FU of grain a unit at full want; wt: share of a porter's load a unit; pool:
-- "village" (worth by the village's stock) or "house" (by the household's).
M.GOODS = {
  grain = { base = 1.0, wt = 1 / 10, pool = "village", food = true, name = "grain", unit = "FU" },
  smoked = { base = 1.2, wt = 1 / 8, pool = "village", food = true, name = "smoked fish", unit = "FU" },
  cheese = { base = 1.4, wt = 1 / 8, pool = "village", food = true, name = "cheese", unit = "FU" },
  logs = { base = 1.5, wt = 1 / 2, pool = "village", name = "logs", unit = "" },
  wool = { base = 3.0, wt = 1 / 3, pool = "house", name = "wool", unit = "GE" },
  cloth = { base = 6.0, wt = 1 / 3, pool = "house", name = "cloth", unit = "GE" },
}
M.ORDER = { "grain", "smoked", "cheese", "logs", "wool", "cloth" }

-- "8.0 FU of smoked fish", "2 logs": an amount of a good in words.
function M.amount(q, g)
  local G = M.GOODS[g]
  if G.unit == "" then return string.format("%.0f %s", q, G.name) end
  return string.format("%.1f %s of %s", q, G.unit, G.name)
end

-- Worth a unit at stock `have` against want `want`: dear when short, cheap when there is plenty.
local function worth(g, have, want)
  local G = M.GOODS[g]
  if want <= 0 then return G.base * 0.15 end
  local v = 1.6 - have / want
  if v < 0.15 then v = 0.15 elseif v > 1.6 then v = 1.6 end
  return G.base * v
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
    if b.kind == "house" then
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

-- A household's own stock and want of a house good (textiles: wool and cloth stand for the whole
-- pipeline from raw wool and flax to cloth).
function M.house_stock(W, hh, home, g)
  local c = C()
  local st = home.stock
  local n = max(1, #hh.members)
  if g == "wool" or g == "cloth" then
    local have = (st.wool or 0) + (st.flax or 0) + (st.fibre or 0) + (st.yarn or 0) + (st.cloth or 0)
    return have, n * c.textile_hh_want, (g == "wool" and (st.wool or 0) or (st.cloth or 0))
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
  return max(0, min(own, have - want * c.spare_k))
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
      buy[g], sell[g], spare[g] = v, v, max(0, have - want * C().spare_k)
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

-- Village s learns how things stand at o (someone of s is there, or someone of o came to s).
function M.learn(W, s, o, now)
  if s == o then return end
  s.know = s.know or {}
  local buy, sell, spare = M.market(W, o)
  s.know[o.id] = { t = now, buy = buy, sell = sell, spare = spare }
end

-- What s expects good g to fetch at o (buy) and to cost there (sell), and how much o has to spare:
-- what it last saw, fading toward not knowing (a neutral guess, nothing known to spare) as the news
-- grows old.
function M.expect(W, s, o, g, now)
  local G = M.GOODS[g]
  local k = s.know and s.know[o.id]
  local neutral = G.base * 0.6
  -- (not knowing: a guess that someone there would give a middling price, and has a little of
  -- anything to spare: half a load)
  local guess = 0.5 / G.wt
  if not k then return neutral, neutral * 1.5, guess end
  local w = exp(-(now - k.t) / (C().know_fade * 24))
  local b = k.buy[g] or neutral
  local sl = k.sell[g] or neutral
  return b * w + neutral * (1 - w), sl * w + neutral * 1.5 * (1 - w), (k.spare[g] or 0) * w + guess * (1 - w)
end

-- The relation between two villages (D-101): trust from deals kept, gifts and help.
function M.rel(s, o)
  s.rel = s.rel or {}
  local r = s.rel[o.id]
  if not r then r = { trust = 0.2, deals = 0, gifts = 0, lost = 0, t = 0 }; s.rel[o.id] = r end
  return r
end

-- The best exchange a trader of household hh (in village s) could set out to make, by what s
-- knows: returns { o, give, n, take, k, gain, cost, hours } or nil. `pool`: "village" plans the
-- village's goods (food, logs), "house" the household's own (wool, cloth).
-- `route(W, s, o)` gives the hours one way, the loads that can be carried and the people it takes
-- (by road: one porter, one load; by sea: a boat's hold and its crew of two, D-102).
function M.best_trade(W, s, hh, home, pool, partners, route, now)
  local c = C()
  local best = nil
  for gi = 1, #M.ORDER do
    local X = M.ORDER[gi]
    local GX = M.GOODS[X]
    if GX.pool == pool then
      local spareX = M.spare(W, s, hh, home, X)
      local aX = M.value(W, s, hh, home, X)
      for pi = 1, #partners do
        local o = partners[pi]
        local hrs, cap, crew = route(W, s, o)
        local nmax = min(spareX, cap / GX.wt)
        if nmax >= c.trade_min_load / GX.wt then
          local bX = M.expect(W, s, o, X, now)
          if bX > aX * c.trade_margin then
            -- the good to bring home: what this household (or village) wants and o can spare
            local bestH, br, sH, aH, spH = nil, 0, 0, 0, 0
            for hi = 1, #M.ORDER do
              local H = M.ORDER[hi]
              if H ~= X and (pool ~= "village" or M.GOODS[H].pool == "village") then
                local _, sl, sp = M.expect(W, s, o, H, now)
                local ah = M.value(W, s, hh, home, H)
                local ratio = bX * ah / (aX * sl)
                if sp > 0 and ratio > br then bestH, br, sH, aH, spH = H, ratio, sl, ah, sp end
              end
            end
            if bestH and br > c.trade_margin then
              local k = sqrt(aX * bX / (aH * sH))
              local GH = M.GOODS[bestH]
              local qH = min(nmax * k, cap / GH.wt, spH)
              local n = qH / k
              local cost = 2 * hrs * crew * c.trade_hour
              local gain = qH * aH - n * aX
              local r = M.rel(s, o)
              local net = gain * (1 + 0.5 * r.trust) - cost
              if net > 0 and (not best or net > best.net) then
                best = { o = o.id, give = X, n = n, take = bestH, k = k, gain = gain, cost = cost, net = net, hours = hrs,
                  aX = aX, aH = aH }
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
  -- the village's stores take pooled goods and pay in pooled goods
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
local SHARE = { "smoked", "cheese", "wool", "cloth" }
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
  s.tplan = #partners > 0 and M.best_trade(W, s, nil, nil, "village", partners, M.land_route, now) or nil
  local sea = trade.sea_partners(W, s)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and hh.home ~= 0 then
      local home = s.buildings[hh.home]
      hh.tplan = (home and #partners > 0) and M.best_trade(W, s, hh, home, "house", partners, M.land_route, now) or nil
      -- (a household with a sound boat may carry its own goods or the village's by sea, D-102)
      hh.splan = nil
      if home and #sea > 0 and M.own_boat(s, hh) then
        local a = M.best_trade(W, s, hh, home, "house", sea, M.sea_route, now)
        local b = M.best_trade(W, s, hh, home, "village", sea, M.sea_route, now)
        if b and (not a or b.net > a.net) then a = b; a.pool = "village" elseif a then a.pool = "house" end
        hh.splan = a
      end
    end
  end
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

-- A household's sound boat at its dock (not out, not away), or nil.
function M.own_boat(s, hh)
  local c = C()
  for _, boat in ipairs(s.boats or {}) do
    if boat.owner == hh.id and not boat.lost and (boat.cond or 1) > c.boat_unsafe + 0.15 and boat.crew == 0 and not boat.away then
      return boat
    end
  end
  return nil
end

return M
