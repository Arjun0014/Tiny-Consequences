-- Conflict (R6, D-149, docs/v3/60_R6_CONFLICT.md): taking what is not one's own, weighed by a person
-- for their household, and what follows - noticed or not, found missing, tracked, remembered.
-- Pure weighing and bookkeeping; the walking and the work are think.lua's and act.lua's.
--   take_weigh(W, s, p, hh, now) -> the best village and herd to take a beast from, with its why,
--     or nil: need (migrate.push) or a grievance (deeds) against what is known of each village
--     within reach (the beasts and dogs seen there: econ.learn), the gain to the household, the
--     risk of being seen and what follows, restraint (kin there, trust, friends there, boldness).
--   took(W, s, p, H, a, now) -> the deed: beast a of village s taken by p (halter on).
--   missing(W, s, now)       -> the owners' morning count: beasts gone in the night are found
--                               missing; tracks on wet ground show the road taken.
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local people = require("sim.agents.people")
local deeds = require("sim.agents.deeds")
local econ = require("sim.agents.econ")
local trade = require("sim.agents.trade")
local stock = require("sim.agents.stock")
local migrate = require("sim.agents.migrate")
local clock = require("sim.agents.clock")
local danger = require("sim.agents.danger")
local dogs = require("sim.agents.dogs")
local dsq = require("sim.dmath").sq

local M = {}
local floor, max, min = math.floor, math.max, math.min
local function C() return cfgmod.V3 end

local function u(W, p, now, k)
  return rng.float(W.seed, rng.S.V3_CONFLICT, p.key, floor(now) % 4294967296, k)
end

-- Has household hh close kin (parents, children, a spouse, brothers and sisters) living at o?
function M.kin_at(W, hh, o)
  local V = W.S.v3
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      for _, q in ipairs({ V.people[p.mother], V.people[p.father], V.people[p.partner] }) do
        if q and q.alive and q.sett == o.id then return q end
      end
      for _, kid in ipairs(p.kids) do
        local q = V.people[kid]
        if q and q.alive and q.sett == o.id then return q end
      end
      for _, mid in ipairs(o.members) do
        local q = V.people[mid]
        if q and q.alive and ((p.mother ~= 0 and q.mother == p.mother) or (p.father ~= 0 and q.father == p.father)) then return q end
      end
    end
  end
  return nil
end

-- A friend of p's (affinity at least 0.3) living at o, or nil.
function M.friend_at(W, p, o)
  local V = W.S.v3
  for i = 1, #p.rel_id do
    if p.rel_v[i] >= 0.3 then
      local q = V.people[p.rel_id[i]]
      if q and q.alive and q.sett == o.id then return q end
    end
  end
  return nil
end

-- The herds a taker could lead off: sheep and goats by anyone, cattle by the bold, never pigs.
local TAKE_KINDS = { { hi = 1, kind = "flock" }, { hi = 2, kind = "cattle" } }

-- (one of our ...)
M.NOUN = { sheep = "sheep", goat = "goats", cattle = "cows", pig = "pigs" }
M.ONE = { sheep = "a sheep", goat = "a goat", cattle = "a cow", pig = "a pig" }

local CACHE = setmetatable({}, { __mode = "k" })
-- The best taking for p now, or nil (cached for the local day: the weighing is the morning's).
-- (S20, R6e) The weighing for one village o: the best kind to take there (or `only` that kind), its
-- net and why; `party` others going too share the risk. nil when there is no motive or nothing to take.
local function weigh_at(W, s, p, hh, o, now, push, bold, only, party)
  local c = C()
  local V = W.S.v3
  local k = s.know and s.know[o.id]
  if not (k and k.beasts and o.alive and o ~= s) then return nil end
  local _, hrs = trade.road(W, s, o)
  -- (a wrong is answered by taking back only once redress was asked and not given - or where
  -- there is no trust left to ask it: claims first, R6b)
  local trust0 = econ.rel(s, o).trust
  local grudge, gd = deeds.grievance(W, s, o, hh.id, now, function(e)
    return e.answer == "denied" or e.answer == "refused" or trust0 < c.claim_trust_min end)
  -- (S20) a household whose herd is far below what it had has lost its living: restocking
  local gap = 0
  local lost, had = hh.herd_lost or 0, (hh.herd_now or 0) + (hh.herd_lost or 0)
  if had >= c.restock_min_fu then gap = lost / had end
  if gap < c.take_restock then gap = 0 end
  if hrs > c.take_road_h or (push < c.take_push and grudge < c.take_grudge and gap == 0) then return nil end
  -- restraint: who they are to us (kin there, trust, a friend there), less boldness and the
  -- wrongs they did us
  local kin = M.kin_at(W, hh, o)
  local friend = M.friend_at(W, p, o)
  local restraint = c.take_restraint * (1 - c.take_bold * bold) + (kin and c.take_kin or 0)
    + trust0 * c.take_trust + (friend and c.take_friend or 0) - grudge * c.take_grudge_k
  -- the risk as known: their dogs, a watch kept, the grown men there; the bold care less
  local men = 0
  for _, mid in ipairs(o.members) do
    local q = V.people[mid]
    if q and q.alive and q.sex == "m" and people.stage(q, now) == "adult" then men = men + 1 end
  end
  -- (and what they themselves met there: given away before, they expect it again; it fades)
  local tried = p.tried and p.tried[o.id]
  local fails = tried and tried.n * deeds.fade({ kind = "take" }, now - tried.t) or 0
  local risk = (c.take_dog * min(3, k.dogs or 0) + (k.watch and c.take_watch or 0) + c.take_people * men / 10
    + c.take_failed * fails) * (1.2 - bold)
  local shared = risk / (1 + 0.5 * (party or 0))
  local mouths = max(1, #hh.members)
  local best, bn = nil, -1e9
  for _, tk in ipairs(TAKE_KINDS) do
    local nb = k.beasts[tk.hi] or 0
    -- (only a kind the household's village keeps: a beast needs a pen to go into at home)
    if nb > 0 and (not only or only == tk.kind) and (tk.kind ~= "cattle" or bold > 0.6) and s.herds and s.herds[tk.kind] then
      -- the gain: meat for a hungry household (as many as one person can lead off by night and the
      -- household needs: a cow, or up to take_flock sheep and goats, no more than it can eat before
      -- it spoils), or beasts in place of what they took from us
      local meat = tk.kind == "cattle" and c.stock.cattle.meat or c.stock.sheep.meat
      local need = mouths * 0.7 * c.take_need_days
      local n = tk.kind == "cattle" and 1 or max(1, min(c.take_flock, nb, floor(need / meat + 0.5)))
      local gain = c.take_gain_k * min(n * meat, need) * min(1.5, push) + min(0.5, grudge * c.take_grudge_k)
        + c.take_gain_k * n * meat * gap * c.restock_k
      local slow = tk.kind == "cattle" and 0.15 or 0
      -- (S20, R6f) a walled pen, as seen there
      local wall = (k.walled and k.walled[tk.kind]) and c.take_wall * (1.2 - bold) / (1 + 0.5 * (party or 0)) or 0
      local net = gain - shared - restraint - hrs / 24 * 0.1 - slow - wall
      if net > bn then
        local why
        if push >= c.take_push then
          why = sfmt("Nothing left to eat at home; %s's %s held %d beasts when we were there%s",
            o.name, tk.kind == "flock" and "fold" or "byre", nb, kin and "" or ", and none of them is kin of ours")
        elseif gap > 0 and gap * c.restock_k * n * meat >= min(0.5, grudge * c.take_grudge_k) / c.take_gain_k then
          why = sfmt("We have lost our beasts (%.0f FU of them dead or taken, %.0f left); %s's %s held %d beasts when we were there",
            hh.herd_lost or 0, hh.herd_now or 0, o.name, tk.kind == "flock" and "fold" or "byre", nb)
        elseif gd and gd.to_s ~= s.id then
          -- (S20, alliances) a wrong done to our kin in another village
          local ks = W.S.v3.settlements[gd.to_s]
          why = sfmt("The people of %s wronged our kin at %s and never made it good: taking one of theirs", o.name, ks and ks.name or "another village")
        else
          why = sfmt("%s took from us and never made it good: taking one of theirs", gd and deeds.told(W, s, gd) or ("Someone of " .. o.name))
        end
        best, bn = { o = o.id, hi = tk.hi, kind = tk.kind, n = n, net = net, hrs = hrs, why = why, grudge = grudge, gdeed = gd and gd.id or nil,
          risk = risk, push = push }, net
      end
    end
  end
  return best
end
M.weigh_at = weigh_at

function M.take_weigh(W, s, p, hh, now)
  local day = floor(now / 24)
  local e = CACHE[p]
  if e and e.day == day then return e.best end
  e = { day = day }
  CACHE[p] = e
  local push = migrate.push(W, s, hh, now, true)
  local bold = people.bold(W, p)
  local best = nil
  for _, o in ipairs(trade.partners(W, s)) do
    local b = weigh_at(W, s, p, hh, o, now, push, bold)
    if b and b.net > 0 and (not best or b.net > best.net) then best = b end
  end
  e.best = best
  return best
end

-- Beast a of herd H at village s taken by p now: the deed, the beast marked as taken.
function M.took(W, s, p, H, a, now, n)
  local V = W.S.v3
  local home_s = V.settlements[p.sett]
  local d = deeds.add(W, { kind = "take", t = now, at = s.id, x = a.tx, y = a.ty,
    by = p.id, by_hh = p.hh, by_s = p.sett, to = 0, to_hh = a.owner or 0, to_s = s.id,
    what = a.sp, n = n or 1, fu = stock.SP(a.sp).meat * 1.2 * (n or 1), harm = true,
    answers = p.trip and p.trip.gdeed or nil, raid = p.trip and p.trip.band or nil })
  a.taken = d.id
  stock.lose(W, a.owner, a)   -- (S20: a beast its owners did not mean to part with)
  -- (the taker's own household knows; it is not told about)
  if home_s then deeds.learn(W, home_s, d, "ours", p.id, p.sett, now, true) end
  return d
end

-- The owners' morning count (village s's daily round): a beast of theirs taken in the night (gone
-- from the herd with a taker, not dead, not sold) is found missing. The village learns the deed:
-- the taker by name if someone saw and knew him, by his village if tracks on wet ground showed the
-- road he took, else not at all. Trust in the taker's village falls when it is known.
function M.missing(W, s, now)
  local V = W.S.v3
  local c = C()
  -- (S20, R6e) a party long gone is done with
  if s.raid and now > s.raid.depart + 96 then s.raid = nil end
  -- (S20, R6d) burials past are done with
  local B = s.burials
  if B then
    for i = #B, 1, -1 do if B[i].day < floor(now / 24) - 1 then table.remove(B, i) end end
  end
  local L = deeds.list(W)
  for i = #L, 1, -1 do
    local d = L[i]
    if now - d.t > 48 then break end
    if d.kind == "take" and d.to_s == s.id and d.back and d.barked and not d.away and not deeds.known(s, d.id) and now - d.t >= 2 then
      -- (S20) the dogs barked in the night and the beasts were found loose about the fold: someone
      -- tried to take them
      deeds.learn(W, s, d, "the dogs barked in the night and the beasts were found loose", 0, 0, now)
      local hh = V.households[d.to_hh]
      for _, id in ipairs(hh and hh.members or {}) do
        local m = V.people[id]
        if m and m.alive and people.stage(m, now) ~= "infant" and m.sett == s.id and not m.at then
          people.remember(m, now, "Someone was at our beasts in the night: the dogs barked, and they were found loose in the morning")
        end
      end
      s.thief_alarm = now
    end
    if d.kind == "take" and d.to_s == s.id and d.away and not deeds.known(s, d.id) and now - d.t >= 2 then
      -- (it was got away with: it left the map with the taker)
      local q = V.people[d.by]
      local o = V.settlements[d.by_s]
      local wet = (W.F.soil_water[s.cell] or 0) >= c.tracks_wet or (W.F.precip[s.cell] or 0) >= c.tracks_rain
      local how, by, by_s
      if wet and o then
        how, by, by_s = sfmt("the tracks of a man leading %s went off by the road to %s", (d.n or 1) > 1 and "them" or "it", o.name), 0, o.id
      else
        how, by, by_s = "found missing in the morning", 0, 0
      end
      deeds.learn(W, s, d, how, by, by_s, now)
      local hh = V.households[d.to_hh]
      local what = (d.n or 1) > 1 and sfmt("%d of our %s were", d.n, M.NOUN[d.what] or d.what) or sfmt("One of our %s was", M.NOUN[d.what] or d.what)
      for _, id in ipairs(hh and hh.members or {}) do
        local m = V.people[id]
        if m and m.alive and people.stage(m, now) ~= "infant" and m.sett == s.id and not m.at then
          people.remember(m, now, by_s ~= 0 and sfmt("%s taken in the night: %s", what, how)
            or sfmt("%s gone in the morning: taken in the night", what))
        end
      end
      if by_s ~= 0 and o then
        local r = econ.rel(s, o)
        r.trust = max(0, r.trust - c.take_trust_loss)
      end
      s.thief_alarm = now
      s.acc.taken = (s.acc.taken or 0) + 1
    end
  end
end

-- A witness at village s saw p (a stranger, or someone they know) taking or leading a beast: the
-- village knows the deed and who, as far as a face is known there. Returns the entry.
function M.seen(W, s, d, witness, now)
  local V = W.S.v3
  local q = V.people[d.by]
  if not q then return nil end
  local face = people.known_at(q, s.id)
  local e, news = deeds.learn(W, s, d, sfmt("%s saw it", witness.name), face and q.id or 0, face and q.sett or 0, now)
  d.saw[#d.saw + 1] = witness.id
  if news then
    people.remember(witness, now, face and sfmt("Saw %s of %s leading one of our beasts away in the night", q.name, V.settlements[q.sett].name)
      or "Saw a stranger leading one of our beasts away in the night")
    if face then
      local o = V.settlements[q.sett]
      local r = o and econ.rel(s, o)
      if r then r.trust = max(0, r.trust - C().take_trust_loss) end
    end
    s.thief_alarm = now
  end
  return e
end

-- (S20, R6c) What happened to p away from home goes home with them (`p.tell`): their village learns it
-- when they come back (act.lua's arrival), as far as they knew who did it.
function M.carry_news(p, d)
  p.tell = p.tell or {}
  p.tell[#p.tell + 1] = d.id
end

function M.tell_home(W, s, p, now)
  local L = p.tell
  if not L then return end
  local V = W.S.v3
  for _, id in ipairs(L) do
    local d = deeds.get(W, id)
    if d then
      local q = V.people[d.by]
      local face = q and people.known_at(q, s.id)
      deeds.learn(W, s, d, sfmt("%s told it when %s came home", p.name, p.sex == "f" and "she" or "he"), face and q.id or 0, d.by_s, now)
    end
  end
  p.tell = nil
end

-- (S20, R6c) One person strikes another: hurt by the blow (an iron-edged tool in hand cuts deeper);
-- a deed of wounding (or killing) that answers `answers`; both remember. Returns the hurt.
function M.strike(W, s, by, to, answers, now, k)
  local c = C()
  local V = W.S.v3
  local hh = V.households[by.hh]
  local arm = 1 + ((hh and (hh.iron or 0) > c.iron_broken) and 0.5 or 0)
  local sev = c.blow * arm * (0.4 + 0.6 * u(W, by, now, k or 60))
  local kill = sev >= 1
  local d = deeds.add(W, { kind = kill and "kill" or "wound", t = now, at = s.id, x = to.x, y = to.y,
    by = by.id, by_hh = by.hh, by_s = by.sett, to = to.id, to_hh = to.hh, to_s = to.sett, what = "blow", n = 1,
    fu = kill and c.blood_fu or c.wound_fu * sev, harm = true, answers = answers })
  -- (S20) blood between villages: the struck man's people trust the striker's less (little, when the
  -- blow answered the struck man's own wrong); it shows in their dealings and their courting
  if to.sett ~= by.sett and V.settlements[to.sett] and V.settlements[by.sett] then
    local a = answers and deeds.get(W, answers)
    local deserved = a and a.by == to.id
    local r = econ.rel(V.settlements[to.sett], V.settlements[by.sett])
    r.trust = max(0, r.trust - (kill and c.blood_trust_loss or c.wound_trust_loss * sev) * (deserved and c.deserved_k or 1))
  end
  people.hurt(to, min(1, sev), "wound", d.id, now)
  -- (each knows it: their own villages at once - the struck man's when he comes home or is told)
  deeds.learn(W, V.settlements[by.sett], d, sfmt("%s did it", by.name), by.id, by.sett, now)
  if to.sett == s.id then deeds.learn(W, s, d, sfmt("%s saw it", to.name), by.id, by.sett, now)
  else M.carry_news(to, d) end
  to.struck_by = d.id
  return sev, d
end

-- (S20, R6c) Taker q caught by p at village s: p may strike him (his boldness, his beasts at stake);
-- q, bold enough and not badly hurt, may strike back. Returns what happened in words (for p's memory).
function M.blows(W, s, p, q, dd, now)
  local c = C()
  local said
  local bp = people.bold(W, p)
  if u(W, p, now, 61) < c.strike_k * (0.3 + bp) then
    local sev = M.strike(W, s, p, q, dd and dd.id or nil, now, 62)
    people.remember(q, now, sfmt("Caught at %s: %s struck me%s", s.name, people.known_at(p, q.sett) and p.name or "a man of theirs",
      sev >= 0.6 and ", badly" or ""))
    said = sev >= 0.6 and "I struck him hard and he got away" or "I struck him and he ran"
  end
  if q.alive and people.bold(W, q) >= c.take_fight and (q.hurt or 0) < 0.5 then
    local sev = M.strike(W, s, q, p, dd and dd.id or nil, now, 63)
    people.remember(p, now, sfmt("%s struck me when I caught him at the fold", people.known_at(q, s.id) and q.name or "The stranger"))
    said = (said and (said .. "; ") or "") .. (sev >= 0.6 and "he struck me badly" or "he struck back")
  end
  return said
end

-- (S20, R6b) The wrong household hh of village s would go and ask redress for: a known harm done
-- to it whose doer is known by name or village, not settled, not asked about lately and not
-- refused, within reach; the heaviest. Returns { d, e, o, hrs, w } or nil.
function M.claim_weigh(W, s, hh, now)
  local c = C()
  local dk = s.dk
  if not dk then return nil end
  local best, bw = nil, 0
  for i = 1, #dk do
    local e = dk[i]
    local d = deeds.get(W, e.id)
    if d and d.harm and not d.settled and not d.back and not e.own and d.to_s == s.id and d.to_hh == hh.id and e.by_s ~= 0
      and (not e.claim_t or now - e.claim_t > c.claim_again_days * 24) and e.answer ~= "refused" and e.answer ~= "denied" then
      local o = W.S.v3.settlements[e.by_s]
      if o and o.alive and o ~= s then
        local _, hrs = trade.road(W, s, o)
        local w = (d.fu or 0) * deeds.fade(d, now - d.t)
        if hrs <= c.claim_road_h and w >= c.claim_min and w > bw then best, bw = { d = d, e = e, o = o, hrs = hrs, w = w }, w end
      end
    end
  end
  return best
end

-- (S20, R6b) The beasts of deed d still alive in village o's pens (herders know their own beasts).
function M.beasts_of(o, d)
  local out = {}
  for _, kind in ipairs(stock.HERDS) do
    local H = o.herds and o.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and a.taken == d.id and (a.led or 0) == 0 then out[#out + 1] = { a = a, H = H } end
    end
  end
  return out
end

-- (S20, R6b) Claimant p of village s asks at village o for redress of deed d. Whose household answers:
-- the doer's if he is known by name, else the household whose fold holds the beasts (seen there).
-- It weighs giving way (the trust between the villages, kin there, the deals between them, the
-- claimant village's grown men, the evidence) against holding out (the doer's boldness, its own
-- need): returns { kind = "returned" | "paid" | "refused" | "denied", hh, beasts }.
function M.claim_answer(W, o, p, d, now)
  local c = C()
  local V = W.S.v3
  local s = V.settlements[p.sett]
  local e = deeds.known(s, d.id)
  local beasts = M.beasts_of(o, d)
  local named = e ~= nil and e.by ~= 0
  local hhid = (named and V.people[e.by] and V.people[e.by].hh) or (beasts[1] and beasts[1].a.owner) or nil
  if not hhid or not V.households[hhid] then return { kind = "denied" } end
  if not named and beasts[1] then
    deeds.learn(W, s, d, sfmt("%s found our beasts in their fold", p.name), d.by, d.by_s, now)
    named = true
  end
  local hh = V.households[hhid]
  local doer = V.people[d.by]
  local push = migrate.push(W, o, hh, now, true)
  local r = econ.rel(o, s)
  local men = 0
  for _, mid in ipairs(s.members) do
    local q = V.people[mid]
    if q and q.alive and q.sex == "m" and people.stage(q, now) == "adult" then men = men + 1 end
  end
  local press = c.claim_base + r.trust * c.claim_trust + (M.kin_at(W, hh, s) and c.claim_kin or 0)
    + min(1, (r.deals or 0) / 10) * c.claim_deals + c.claim_fear * men / 10 + (named and c.claim_named or 0)
  local hold = (doer and doer.alive and people.bold(W, doer) or 0.5) * c.claim_bold + min(1, push) * c.claim_need
  if press < hold then return { kind = "refused", hh = hh, press = press, hold = hold } end
  if #beasts > 0 then return { kind = "returned", hh = hh, beasts = beasts, press = press, hold = hold } end
  return { kind = "paid", hh = hh, press = press, hold = hold }
end

-- (S20, R6g) What household hh (home `home` at village o) can give to make good `w` FU, in one load:
-- its spare house goods and grain beyond `amends_keep_days` of its own bread. Returns the picks
-- { { good, q } } and their worth.
function M.amends_basket(W, o, hh, home, w)
  local c = C()
  local picks, got, load = {}, 0, 0
  if not home then return picks, 0 end
  for _, g in ipairs({ "cloth", "leather", "wool", "salt", "hides", "grain" }) do
    if got < w - 0.05 and load < 0.95 then
      local G = econ.GOODS[g]
      local sp
      if g == "grain" then
        sp = max(0, (home.stock.food or 0) - #hh.members * 0.7 * c.amends_keep_days)
      else
        sp = econ.spare(W, o, hh, home, g)
      end
      local q = min(sp, (w - got) / G.base, (1 - load) / G.wt)
      if q > 0.05 then
        picks[#picks + 1] = { g, q }
        got, load = got + q * G.base, load + q * G.wt
      end
    end
  end
  return picks, got
end

-- (S20, R6g) Amends: household hh of village o took from village s (its own deed, unsettled) and s
-- knows who did it (by name or by village). Not desperate now, it weighs making it good unasked:
-- the fear of an answer (s's grown men; whether s's people are known to answer wrongs) and what it
-- would lose (the trade between them, kin there) against the worth of what it would give. Returns
-- { d, s, w, why } or nil.
function M.amends_weigh(W, o, hh, now)
  local c = C()
  local dk = o.dk
  if not dk then return nil end
  local V = W.S.v3
  if migrate.push(W, o, hh, now, true) >= c.take_push then return nil end
  local best, bv = nil, 0
  for i = 1, #dk do
    local e = dk[i]
    local d = deeds.get(W, e.id)
    if e.own and d and d.harm and not d.settled and not d.back and d.by_hh == hh.id and now - d.t >= 48 then
      local s = V.settlements[d.to_s]
      local ks = s and deeds.known(s, d.id)
      if s and s.alive and ks and (ks.by ~= 0 or ks.by_s == o.id) then
        local men, answers = 0, 0
        for _, mid in ipairs(s.members) do
          local q = V.people[mid]
          if q and q.alive and q.sex == "m" and people.stage(q, now) == "adult" then men = men + 1 end
        end
        -- (are they known to answer wrongs? deeds by s's people that answered one, known here)
        for j = 1, #dk do
          local d2 = deeds.get(W, dk[j].id)
          if d2 and d2.by_s == s.id and d2.answers then answers = answers + 1 end
        end
        local r = econ.rel(o, s)
        local fear = c.amends_fear * (men / 10) * (1 + answers)
        local ties = r.trust * c.amends_trust + min(1, (r.deals or 0) / 10) * c.amends_deals + (M.kin_at(W, hh, s) and c.amends_kin or 0)
        local w = (d.fu or 0) * deeds.fade(d, now - d.t)
        local v = (fear + ties) - c.amends_cost * w / 20
        -- (only with something to give now: a quarter of it at least; the rest is owed)
        local _, can = M.amends_basket(W, o, hh, o.buildings[hh.home or 0], w)
        if can < w * c.amends_part then v = -1 end
        if v > bv then
          best, bv = { d = d, s = s, w = w, v = v, why = sfmt("We took %s from %s and they know it: making it good before they answer it",
            (d.n or 1) > 1 and sfmt("%d %s", d.n, M.NOUN[d.what] or d.what) or (M.ONE[d.what] or d.what), s.name) }, v
        end
      end
    end
  end
  return best
end

-- (S20, R6f) Defence that is built: a wall of stakes and hurdles round a pen (`b.wall` 0..1, logs
-- delivered `b.wall_logs`). Each day the village weighs it: the losses at that pen it remembers
-- (takings, attempts, wolves' kills: deeds within `wall_memory` days) as what a wall would save over
-- as long again (x `wall_save`) against its cost (`wall_logs` logs and `wall_h` hours of work).
local PEN_OF = { sheep = "flock", goat = "flock", cattle = "cattle", pig = "swine" }
function M.defence_daily(W, s, now)
  local c = C()
  if not s.herds or not s.dk then return end
  local L = deeds.list(W)
  local loss = {}
  for i = 1, #s.dk do
    local d = L[s.dk[i].id]
    if d and d.harm and d.to_s == s.id and (d.kind == "take" or d.kind == "lost") and now - d.t <= c.wall_memory * 24 then
      local k = PEN_OF[d.what]
      if k then loss[k] = (loss[k] or 0) + (d.fu or 0) end
    end
  end
  local cost = c.wall_logs * econ.GOODS.logs.base + c.wall_h * c.wall_hour_fu
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds[kind]
    local b = H and s.buildings[H.pen]
    if b then b.walling = 0 end
    if b and b.kind ~= "camp" and (b.wall or 0) < 1 then
      local want = (loss[kind] or 0) * c.wall_save >= cost
      if want and not b.wall_want then
        b.wall_want = now
        b.wall_why = sfmt("We lost %.0f FU of beasts at the %s in %d days: walling it", loss[kind], b.label or "pen", c.wall_memory)
        for _, hid in ipairs(s.households) do
          local hh = W.S.v3.households[hid]
          for _, id in ipairs(hh and hh.members or {}) do
            local q = W.S.v3.people[id]
            if q and q.alive and people.stage(q, now) == "adult" then people.remember(q, now, b.wall_why) end
          end
        end
      end
    end
  end
end

-- (S20, R6f) The pen of herd kind at village s is walled (built)?
function M.walled(s, kind)
  local H = s.herds and s.herds[kind]
  local b = H and s.buildings[H.pen]
  return b ~= nil and (b.wall or 0) >= 1
end

-- (S20, R6e) Raids -----------------------------------------------------------------------------
-- A man who weighs taking beasts where the risk alone is high (dogs, a watch, its men, his own
-- failures there) and who has kin or friends at home with reasons of their own may raise a party:
-- s.raid = { id, leader, to, kind, n, depart, dur, party = { pids }, why, hx, hy, t }. Each of the
-- others decides to join for their own reasons (`join_weigh`); those who go gather at the gate at the
-- hour (`p.raid`), walk the road together, lie up together and take beasts with numbers on their
-- side (no one lets go while his own outnumber those who come out).

-- Grown men of s besides p who could go: fit, no babe in arms, not on a journey or moving.
function M.companions(W, s, p, now)
  local V = W.S.v3
  local n = 0
  for _, id in ipairs(s.members) do
    local q = V.people[id]
    if q and q ~= p and q.alive and q.sex == "m" and not q.at and not q.trip and not q.raid and people.stage(q, now) == "adult"
      and q.health >= 0.6 and (q.hurt or 0) < 0.2 and (q.babe or 0) == 0 then
      local hh = V.households[q.hh]
      local mg = hh and (hh.migrate or hh.found)
      if not (mg and not mg.arrived and not mg.given_up) then n = n + 1 end
    end
  end
  return n
end

-- The party forming at village s (not yet set off), or nil.
function M.raid_open(W, s, now)
  local R = s.raid
  if R and now < R.depart and not R.gone then return R end
  return nil
end

-- Would p join the party R (net > 0: their own reasons)? The same weighing as taking alone - their
-- household's need or wrong, ties to the target, boldness - with the risk shared by the party, and
-- the bond to the one raising it (kin, a friend). Returns net and why.
function M.join_weigh(W, s, p, R, now)
  local c = C()
  local V = W.S.v3
  local o = V.settlements[R.to]
  local leader = V.people[R.leader]
  local hh = V.households[p.hh]
  if not o or not leader or not hh then return -1 end
  local push = migrate.push(W, s, hh, now, true)
  local bold = people.bold(W, p)
  local b = weigh_at(W, s, p, hh, o, now, push, bold, R.kind, #R.party)
  local kinl = people.is_kin(W, p, leader.id)
  local aff = people.affinity(p, leader.id)
  local bond = (kinl and c.join_kin or 0) + max(0, aff) * c.join_friend
  if not b then return -1 end
  -- (a wife with child or a babe at home holds a man back a little)
  local wife = V.people[p.partner]
  local family = (wife and wife.alive and ((wife.preg_due or 0) > 0 or (wife.nursing or 0) > now)) and c.join_family or 0
  local net = b.net + bond - family
  local why = sfmt("%s is raising men to take beasts from %s%s", leader.name, o.name,
    (kinl and ": he is my kin") or (aff >= 0.3 and ": he is my friend") or (b.push >= c.take_push and ": we have nothing to eat")
    or (b.grudge >= c.take_grudge and ": they wronged us") or "")
  return net, why
end

-- p of village s joins party R (p.raid; the trip is made when the hour comes).
function M.join(W, s, p, R, now)
  R.party[#R.party + 1] = p.id
  p.raid = R.id
  people.remember(p, now, sfmt("Agreed to go with %s to %s to take beasts", (W.S.v3.people[R.leader] or {}).name or "them",
    (W.S.v3.settlements[R.to] or {}).name or "another village"))
end

-- The raid's hour is near: p's own take trip, in the party (true when made).
function M.raid_ready(W, s, p, now)
  local R = s.raid
  if not R or R.id ~= p.raid or now > R.depart + 6 then p.raid = nil; return false end
  if now < R.depart - C().ready_h then return false end
  local o = W.S.v3.settlements[R.to]
  if not o then p.raid = nil; return false end
  -- (when the hour comes: one whose household is about to move or found a place, or who is too weak
  -- or hurt for the road now, stays - and says so)
  local hh = W.S.v3.households[p.hh]
  local mg = hh and (hh.migrate or hh.found)
  local moving = mg and not mg.arrived and not mg.given_up
  if moving or p.hunger > C().raid_hunger or p.health < 0.4 or (p.hurt or 0) > 0.2 then
    p.raid = nil
    people.remove_id(R.party, p.id)
    people.remember(p, now, moving and "Did not go with the others: the household is leaving" or "Did not go with the others: too weak for the road")
    return false
  end
  p.trip = { steal = true, to = R.to, from = s.id, kind = R.kind, lead_kind = R.kind, give = "grain", take = "grain", n = R.n,
    t0 = now, band = R.id, depart = R.depart, dur = R.dur, why = R.why }
  p.raid = nil
  local hh = W.S.v3.households[p.hh]
  if hh then hh.taking = p.id end
  return true
end

-- (S20) The party's hour is near: those who agreed and are busy at something are told it is time
-- (once; their own reaction - raid_ready - follows).
function M.raid_call(W, s, now)
  local R = s.raid
  if not R or R.called or now < R.depart - C().ready_h or now > R.depart then return end
  R.called = true
  local V = W.S.v3
  local leader = V.people[R.leader]
  for _, id in ipairs(R.party) do
    local q = V.people[id]
    if q and q.alive and q.raid == R.id and not q.at and danger.interrupt then
      danger.interrupt(W, q, now + 0.02, sfmt("Time to make ready: we go with %s at nightfall", leader and leader.name or "the others"))
    end
  end
end

-- (S20) How many of p's party are still to come to the gate (agreed, not yet on the road or at the
-- gate): the band waits for them.
function M.coming(W, s, p, now)
  local tr = p.trip
  local R = tr and tr.band and s.raid
  if not R or R.id ~= tr.band then return 0 end
  local V = W.S.v3
  local o = V.settlements[tr.to]
  if not o then return 0 end
  local gx, gy = trade.gate(W, s, o)
  local n = 0
  for _, id in ipairs(R.party) do
    local q = V.people[id]
    if q and q ~= p and q.alive and not q.at then
      if q.raid == R.id then n = n + 1
      elseif q.trip and q.trip.band == R.id and q.act ~= "travel" and not q.trip.back then
        local qx, qy = danger.person_pos(q, now)
        if (qx - gx) * (qx - gx) + (qy - gy) * (qy - gy) > 0.0025 then n = n + 1 end
      end
    end
  end
  return n
end

-- The others of p's party on map o now, not given away.
function M.party_here(W, o, p)
  local tr = p.trip
  local n = 0
  if not tr or not tr.band then return 0 end
  local V = W.S.v3
  local s = V.settlements[tr.from]
  local R = s and s.raid
  for _, id in ipairs(R and R.id == tr.band and R.party or {}) do
    local q = V.people[id]
    if q and q ~= p and q.alive and q.at == o.id and q.trip and q.trip.band == tr.band and not q.trip.fled then n = n + 1 end
  end
  return n
end

-- What noticing a taker at the pen looks like (danger cues, D-148): dogs at their posts near
-- notice a stranger at the beasts and bark (waking their household; the barking is heard and wakes
-- those close by); anyone awake out of doors within sight sees him; a beast handled cries out. The
-- taker hears the dogs.
function M.take_cues(W, s, p, a, d, now)
  local c = C()
  local x, y = a.tx, a.ty
  local barked = false
  local r2 = c.take_dog_r * c.take_dog_r
  -- (a dog notices a stranger at the beasts more often than not: one asleep or down the wind may
  -- not; a man who knows how to creep up on game, less often)
  local quiet = 1 - 0.5 * (p.sk_hunt or 0.2)
  -- (S20, R6f) over a wall with beasts takes longer and makes more noise
  local walled = M.walled(s, p.trip and p.trip.kind or "flock")
  if walled then quiet = quiet * c.wall_noise end
  for i, dg in ipairs(s.dogs or {}) do
    if dg.alive and dsq(dg.tx - x) + dsq(dg.ty - y) <= r2 and u(W, p, now, 40 + i) < c.take_dog_p * quiet then
      barked = true
      dogs.bark(W, s, dg, now, "thief", p.id)
    end
  end
  local L = danger.cue(W, s, x, y, now, { kind = "thief", src = p.id, skip = p.id, sight = c.sight_day, night = c.take_sight,
    sound = barked and c.bark_sound or (c.take_bleat * (walled and c.wall_noise or 1)), loud = barked,
    why = barked and "The dogs are barking at the beasts" or "A beast cried out at the pen" })
  for _, e in ipairs(L) do
    if e.how == "saw" and e.p.sett == s.id and not e.p.at then M.seen(W, s, d, e.p, now) end
  end
  if barked then
    d.barked = true
    danger.raise(W, p, { kind = "dogs", src = 0, why = "The dogs are barking" }, x, y, now, "heard")
  end
end

-- People of village o awake, out of doors and grown, within r of p now.
function M.watchers_near(W, o, p, now, r)
  local V = W.S.v3
  local px, py = danger.person_pos(p, now)
  local n = 0
  for _, id in ipairs(o.members) do
    local q = V.people[id]
    if q and q.alive and not q.at and q.inside == 0 and q.act ~= "sleep" and q.act ~= "carried" then
      local qx, qy = danger.person_pos(q, now)
      if dsq(qx - px) + dsq(qy - py) <= r * r then n = n + 1 end
    end
  end
  return n
end

-- p lets go of the beasts they lead on map o (they find their way back to the herd); the deed is
-- marked: the beast never left.
function M.let_go(W, o, p, now)
  local tr = p.trip
  for _, kind in ipairs(stock.HERDS) do
    local L, H = stock.led_by(o, p, kind)
    for _, a in ipairs(L) do stock.release(W, o, H, a, now, "Let go by the man who was leading it off") end
  end
  if tr then
    tr.led = {}
    local d = deeds.get(W, tr.deed)
    if d and not d.back then d.back = true; d.fu = d.fu * C().take_try_k end
  end
end

-- The taker p of village s is after: the person, if they are still on this map and the chase is
-- not too old; else the chase is over (nil).
function M.chase_target(W, s, p, now)
  local q = W.S.v3.people[p.alarm and p.alarm.src or 0]
  if q and q.alive and q.at == s.id and q.trip and q.trip.steal and (p.alarm.lost or 0) < C().chase_lost then return q end
  p.alarm = nil
  return nil
end

-- Does p's household keep beasts in village s's pens (its own at stake when a stranger is at them)?
function M.stake(W, s, p)
  for _, kind in ipairs(stock.HERDS) do
    local H = s.herds and s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and a.owner == p.hh then return true end
    end
  end
  return false
end

-- Who goes out after a stranger at the beasts by night: a grown man of a household whose beasts
-- are there (a woman where her household has no man), or anyone bold enough who saw him.
function M.goes_out(W, s, p, now)
  local c = C()
  local bold = people.bold(W, p)
  if p.alarm and p.alarm.how == "saw" and bold >= c.chase_bold then return true end
  if not M.stake(W, s, p) then return false end
  if p.sex == "m" then return true end
  local hh = W.S.v3.households[p.hh]
  return not require("sim.agents.kin").has_man(W, hh, now)
end

-- A chaser p of village s reached where taker q was (d: how far q is now): caught within reach -
-- he lets the beast go and is known; still in sight - seen (and he sees them coming); else on.
function M.chased(W, s, p, q, d, now)
  local c = C()
  local tr = q.trip
  local dd = tr and deeds.get(W, tr.deed)
  local night = danger.night_at(W, s, now)
  local sight = night and c.take_sight or c.sight_day
  if d <= c.chase_reach then
    if dd then M.seen(W, s, dd, p, now) end
    if tr and not tr.fled then
      M.let_go(W, s, q, now)
      tr.fled = true
      q.tried = q.tried or {}
      local tt = q.tried[s.id] or { n = 0, t = now }
      tt.n, tt.t = tt.n + 1, floor(now)
      q.tried[s.id] = tt
      people.remember(q, now, sfmt("Caught at %s by %s: I let the beast go and got away", s.name, p.name))
    end
    -- (S20, R6c) blows: the one who caught him may strike; a bold taker may strike back
    local struck = M.blows(W, s, p, q, dd, now)
    people.remember(p, now, sfmt("Caught up with %s leading off our beasts: %s", people.known_at(q, s.id) and q.name or "a stranger",
      struck or "he let them go and ran"))
    s.acc.thieves_caught = (s.acc.thieves_caught or 0) + 1
    p.alarm = nil
    danger.raise(W, q, { kind = "caught", src = p.id, why = "Caught" }, q.x, q.y, now, "saw")
    return "caught"
  end
  local qx, qy = danger.person_pos(q, now)
  if d <= sight then
    if dd then M.seen(W, s, dd, p, now) end
    -- (S20, R6e) too many of them: back to the houses
    local them = 1 + M.party_here(W, s, q)
    local us = M.watchers_near(W, s, p, now, 0.2)
    if them > us + 1 then
      people.remember(p, now, sfmt("%d men at the beasts in the night, too many for us", them))
      p.alarm = nil
      s.raiders_seen = now
      return "outnumbered"
    end
    p.alarm.x, p.alarm.y, p.alarm.t, p.alarm.how = qx, qy, now, "saw"
    -- (he sees them coming)
    if not (q.alarm and now - q.alarm.t < 0.05) then
      danger.raise(W, q, { kind = "chased", src = p.id, why = "Someone is coming" }, p.x, p.y, now, "saw")
    end
    return "seen"
  end
  p.alarm.lost = (p.alarm.lost or 0) + 1
  return "lost"
end

return M
