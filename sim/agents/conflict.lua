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
function M.take_weigh(W, s, p, hh, now)
  local c = C()
  local day = floor(now / 24)
  local e = CACHE[p]
  if e and e.day == day then return e.best end
  e = { day = day }
  CACHE[p] = e
  local push = migrate.push(W, s, hh, now)
  local V = W.S.v3
  local bold = people.bold(W, p)
  local mouths = max(1, #hh.members)
  local best, bn = nil, 0
  for _, o in ipairs(trade.partners(W, s)) do
    local k = s.know and s.know[o.id]
    if k and k.beasts and o.alive and o ~= s then
      local _, hrs = trade.road(W, s, o)
      -- (a wrong is answered by taking back only once redress was asked and not given - or where
      -- there is no trust left to ask it: claims first, R6b)
      local trust0 = econ.rel(s, o).trust
      local grudge, gd = deeds.grievance(W, s, o, hh.id, now, function(e)
        return e.answer == "denied" or e.answer == "refused" or trust0 < c.claim_trust_min end)
      if hrs <= c.take_road_h and (push >= c.take_push or grudge >= c.take_grudge) then
        -- restraint: who they are to us (kin there, trust, a friend there), less boldness and the
        -- wrongs they did us
        local kin = M.kin_at(W, hh, o)
        local friend = M.friend_at(W, p, o)
        local r = econ.rel(s, o)
        local restraint = c.take_restraint * (1 - c.take_bold * bold) + (kin and c.take_kin or 0)
          + r.trust * c.take_trust + (friend and c.take_friend or 0) - grudge * c.take_grudge_k
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
        for _, tk in ipairs(TAKE_KINDS) do
          local nb = k.beasts[tk.hi] or 0
          -- (only a kind the household's village keeps: a beast needs a pen to go into at home)
          if nb > 0 and (tk.kind ~= "cattle" or bold > 0.6) and s.herds and s.herds[tk.kind] then
            -- the gain: meat for a hungry household (no more than it can eat before it spoils), or
            -- a beast in place of what they took from us
            -- (as many as one person can lead off by night and the household needs: a cow, or up to
            -- take_flock sheep and goats)
            local meat = tk.kind == "cattle" and c.stock.cattle.meat or c.stock.sheep.meat
            local need = mouths * 0.7 * c.take_need_days
            local n = tk.kind == "cattle" and 1 or max(1, min(c.take_flock, nb, floor(need / meat + 0.5)))
            local gain = c.take_gain_k * min(n * meat, need) * min(1.5, push) + min(0.5, grudge * c.take_grudge_k)
            local slow = tk.kind == "cattle" and 0.15 or 0
            local net = gain - risk - restraint - hrs / 24 * 0.1 - slow
            if net > bn then
              local why
              if push >= c.take_push then
                why = sfmt("Nothing left to eat at home; %s's %s held %d beasts when we were there%s",
                  o.name, tk.kind == "flock" and "fold" or "byre", nb, kin and "" or ", and none of them is kin of ours")
              else
                why = sfmt("%s took from us and never made it good: taking one of theirs", gd and deeds.told(W, s, gd) or ("Someone of " .. o.name))
              end
              best, bn = { o = o.id, hi = tk.hi, kind = tk.kind, n = n, net = net, hrs = hrs, why = why, grudge = grudge, gdeed = gd and gd.id or nil }, net
            end
          end
        end
      end
    end
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
    answers = p.trip and p.trip.gdeed or nil })
  a.taken = d.id
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
    if d and d.harm and not d.settled and not e.own and d.to_s == s.id and d.to_hh == hh.id and e.by_s ~= 0
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
  local push = migrate.push(W, o, hh, now)
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
  for i, dg in ipairs(s.dogs or {}) do
    if dg.alive and dsq(dg.tx - x) + dsq(dg.ty - y) <= r2 and u(W, p, now, 40 + i) < c.take_dog_p * quiet then
      barked = true
      dogs.bark(W, s, dg, now, "thief", p.id)
    end
  end
  local L = danger.cue(W, s, x, y, now, { kind = "thief", src = p.id, skip = p.id, sight = c.sight_day, night = c.take_sight,
    sound = barked and c.bark_sound or c.take_bleat, loud = barked,
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
    people.remember(p, now, sfmt("Caught up with %s leading off one of our beasts: he let it go and ran", people.known_at(q, s.id) and q.name or "a stranger"))
    s.acc.thieves_caught = (s.acc.thieves_caught or 0) + 1
    p.alarm = nil
    danger.raise(W, q, { kind = "caught", src = p.id, why = "Caught" }, q.x, q.y, now, "saw")
    return "caught"
  end
  local qx, qy = danger.person_pos(q, now)
  if d <= sight then
    if dd then M.seen(W, s, dd, p, now) end
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
