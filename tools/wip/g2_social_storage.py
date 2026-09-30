# NOT APPLIED YET. Session 7 draft of D-074 (memory of hunger with real levers): obligation from gifts
# received (relations.score), repayment in relief, surplus gifts to partners with room, harvest feasts,
# granaries built to (store_years + dread) years of need. Asserts its anchors; run from anywhere:
#   python tools/wip/g2_social_storage.py <repo root>
import os, sys
os.chdir(sys.argv[1])
def load(p): return open(p, encoding='utf-8').read()
def save(p, s): open(p, 'w', encoding='utf-8', newline='\n').write(s)
def rep(s, old, new):
    assert s.count(old) == 1, (old[:90], s.count(old))
    return s.replace(old, new)

p = 'sim/society/relations.lua'
s = load(p)
s = rep(s, """    local gave, got = ledger(r, u.id, v.id)
    s = s - 0.004 * max(0, gave - got)   -- ~250 need-days unreturned cancel a daughter tie
""", """    local gave, got = ledger(r, u.id, v.id)
    s = s - 0.004 * max(0, gave - got)   -- ~250 need-days unreturned cancel a daughter tie
    -- and what u took from v and has not returned binds u to v: gifts are remembered by those
    -- who took them, which is what makes giving a store (social storage, D-074)
    local owe = got - gave
    if owe > 0 then s = s + cfgmod.SOCIETY.owe_w * min(cfgmod.SOCIETY.owe_cap, owe) end
""")
s = rep(s, """          local gift = min(short, spare * share, cap)
""", """          local gift = min(short, spare * share, cap)
          -- a village that owes v gives back at least what it took, as far as its spare allows
          local rr = M.get(W, u.id, v.id)
          if rr then
            local gave_u, got_u = ledger(rr, u.id, v.id)
            local owed = (got_u - gave_u) * u.need_day
            if owed > 0 then gift = min(short, cap, max(gift, min(owed, spare * 0.75))) end
          end
""")
s = rep(s, """  -- 2. trade: what is short for what is long
""", """  -- 1b. what the granaries cannot hold is given away or feasted (D-074)
  M.surplus(W, v)
  -- 2. trade: what is short for what is long
""")
s = rep(s, """-- Wood a village can spare beyond its reserve of fuel days (WU).
""", """-- Monthly: grain beyond what the village's granaries hold rots (economy.daily). Rather than let it,
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
  local over = v.stock.grain - cap
  if over <= 0 then return end
  local give_f = M.temper(W, v)   -- (a hoarder gives half as much, the devout more)
  if not v.porters_out then
    local list = partners(W, v, cfg.relief_reach, false, cfg.porters)
    local best, bneed, broom, bd = nil, 0, 0, 0
    for i = 1, #list do
      local e = list[i]
      local u = e.u
      if e.s >= 0 and not u.porters_out and not u.famine then
        local room = (u.grain_cap or 0) - u.stock.grain - (u.expect and u.expect.food or 0)
        if room > u.need_day * cfg.gift_min_days and (not best or u.food_days < bneed) then
          best, bneed, broom, bd = u, u.food_days, room, e.d
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
          local id = emit(W, "gift", v.cell, 0, floor(gift), { village = v.id, to = best.id, food = floor(gift) }, v)
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
      give(W, v.id, u.id, over * cfg.guest_share / gn / max(1, u.need_day))
    end
    local nd = over / max(1, v.need_day)
    M.honour_leader(W, v, nd * cfgmod.STATUS.feast_prestige)
    v.acc.feast = (v.acc.feast or 0) + over
    emit(W, "feast", v.cell, 0, floor(nd + 0.5), { village = v.id, guests = gn, food = floor(over) }, v)
  end
end

-- Wood a village can spare beyond its reserve of fuel days (WU).
""")
s = rep(s, """    elseif c.gift then
      dest.stock.grain = dest.stock.grain + c.grain
      local days = c.grain / max(1, dest.need_day)
      give(W, home.id, dest.id, days)
      dest.relief_got = W.hour
""", """    elseif c.gift then
      dest.stock.grain = dest.stock.grain + c.grain
      local days = c.grain / max(1, dest.need_day)
      give(W, home.id, dest.id, days)
      if not c.surplus then dest.relief_got = W.hour end
""")
s = rep(s, """      local a = dest.acc
      a.relief_in = (a.relief_in or 0) + c.grain
      home.acc.relief_out = (home.acc.relief_out or 0) + c.grain""", """      local a = dest.acc
      local kind = c.surplus and "gift" or "relief"
      a[kind .. "_in"] = (a[kind .. "_in"] or 0) + c.grain
      home.acc[kind .. "_out"] = (home.acc[kind .. "_out"] or 0) + c.grain""")
save(p, s)

p = 'sim/config.lua'
s = load(p)
s = rep(s, """  dread_margin = 0.2, dread_store = 1.0,  -- a village remembering hunger (dread 0..1, D-064): margin x (1 + 0.2 dread), store years + dread
""", """  dread_margin = 0.2, dread_store = 1.0,  -- a village remembering hunger (dread 0..1, D-064): margin x (1 + 0.2 dread), store years + dread
  store_years = 1.0,                      -- granaries are built while the store overflows, up to this many years of need
                                          --   (+ dread_store x dread: a village that remembers hunger builds to keep more, D-074)
  gift_min_days = 10,                     -- a surplus gift is worth carrying at this many of the receiver's need-days (D-074)
  feast_min_days = 4,                     -- grain beyond the granaries worth a harvest feast: this many of the village's need-days
  guest_share = 0.3,                      -- share of a feast eaten by guests from friendly villages of the landmass
  owe_w = 0.004, owe_cap = 250,           -- a village is disposed to help one it owes: per need-day taken and not returned
""")
s = rep(s, """  gift_prestige = 0.02,                   -- per need-day of relief the leader's household gives
""", """  gift_prestige = 0.02,                   -- per need-day of relief the leader's household gives
  feast_prestige = 0.01,                  -- per need-day of the village's food eaten at a feast it hosts (D-074)
""")
save(p, s)

p = 'sim/causality/event_kinds.lua'
s = load(p)
s = rep(s, """def("trade",            { actor = "derived", icon = "boat", agg = 48, text = "Trade" })
""", """def("trade",            { actor = "derived", icon = "boat", agg = 48, text = "Trade" })
def("gift",             { actor = "derived", icon = "wheat", agg = 48, text = "Surplus grain given to neighbours" })
def("feast",            { actor = "derived", icon = "people", text = "A harvest feast" })
def("granary_built",    { actor = "derived", icon = "house", text = "A granary built against hunger" })
""")
save(p, s)

p = 'sim/society/construction.lua'
s = load(p)
s = rep(s, """  -- 5. storage when stocks overflow (at most a granary per 40 people: surplus beyond that rots)
  local gcap, wcap = M.capacities(W, v)
  if v.stock.grain > gcap * 0.9 and cnt("granary:site") == 0
    and cnt("granary") < 1 + floor(v.pop / 40) then open("granary") end
""", """  -- 5. storage when stocks overflow, up to the store the village wants to keep: a year of need
  -- (store_years), more when its grown-ups remember hunger (dread, D-074: they build against it;
  -- such a granary carries the remembered famine as its cause). Beyond it, grain would rot: it is
  -- given away or feasted (relations.surplus).
  local gcap, wcap = M.capacities(W, v)
  local need_year = (v.need_day or 0) * 336
  local dread = v.dread or 0
  if v.stock.grain > gcap * 0.9 and cnt("granary:site") == 0
    and gcap < need_year * (cfg.store_years + cfg.dread_store * dread) then
    local b = open("granary")
    if b and dread > 0 and gcap >= need_year * cfg.store_years then b.dread_ev = v.dread_ev end
  end
""")
s = rep(s, """  if b.kind == "house" or b.kind == "hut" then
    M.on_house_ready(W, v, b)""", """  if b.kind == "granary" and b.dread_ev and W.record_events then
    events.emit(W, "granary_built", b.cell, b.dread_ev, 1, { village = v.id, building = b.id })
  end
  if b.kind == "house" or b.kind == "hut" then
    M.on_house_ready(W, v, b)""")
save(p, s)
print("ok")
