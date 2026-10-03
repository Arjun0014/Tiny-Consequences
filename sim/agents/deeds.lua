-- Deeds (D-148, docs/v3/50_PRIMITIVES.md §2): what happened, by whom, to whom, and who knows it.
-- A deed is a small record of a significant act or event (a beast taken, a return, a payment, a
-- wound, a death, a rescue). Villages know deeds only as their people saw or found them, or as
-- people who knew them told them (the contact hook, econ.on_learn). Grievance, reputation and the
-- why of an answer are read from the deeds a village knows - never kept as a score.
--   W.S.v3.deeds = { d1, d2, ... } (id = index)
--   d = { id, kind, t, at (the map it happened on), x, y, by (person, 0 none/nature), by_hh, by_s,
--         to (person), to_hh, to_s, what, n, fu (its worth to the one who bore it), harm (bool),
--         answers (a deed id it answered), settled (the deed id that settled it), saw = { pids } }
--   s.dk = { { id, t, by, by_s, how, own }, ... } what village s knows and believes: the doer by
--          name (by), by village only (by_s) or not at all; how it came to know; own = the doer's
--          own household's knowledge (not told about).
local sfmt = require("sim.fmt").format   -- (the same text in every runtime, S14)
local cfgmod = require("sim.config")
local dmath = require("sim.dmath")

local M = {}
local exp, max, min, floor = dmath.exp, math.max, math.min, math.floor
local function C() return cfgmod.V3 end

local function list(W)
  local V = W.S.v3
  V.deeds = V.deeds or {}
  return V.deeds
end
M.list = list

-- A new deed (fields as above; t defaults to now). Returns it.
function M.add(W, rec)
  local L = list(W)
  rec.id = #L + 1
  rec.t = rec.t or W.S.v3.now or W.hour
  rec.saw = rec.saw or {}
  L[rec.id] = rec
  return rec
end

function M.get(W, id) return list(W)[id or 0] end

-- What village s knows of deed id (its entry), or nil.
function M.known(s, id)
  local dk = s.dk
  if not dk then return nil end
  for i = 1, #dk do if dk[i].id == id then return dk[i] end end
  return nil
end

-- Village s comes to know deed d: how (a short text: "saw it", "found it missing", "tracks on
-- the road to X", "told by the people of X"), believing the doer is person `by` (0: not known) of
-- village `by_s` (0: not known). A second learning keeps the most certain belief (a name over a
-- village over nothing). Returns the entry and whether it was news.
function M.learn(W, s, d, how, by, by_s, now, own)
  if not s or not d then return nil, false end
  s.dk = s.dk or {}
  local e = M.known(s, d.id)
  if e then
    local news = false
    if (by or 0) ~= 0 and e.by == 0 then e.by, e.by_s, e.how, news = by, by_s or e.by_s, how, true
    elseif (by_s or 0) ~= 0 and e.by_s == 0 then e.by_s, e.how, news = by_s, how, true end
    -- (a household's own secret stays its own: what others say of it does not make it the
    -- village's to tell)
    return e, news
  end
  e = { id = d.id, t = now or W.S.v3.now or W.hour, by = by or 0, by_s = by_s or 0, how = how, own = own or nil }
  s.dk[#s.dk + 1] = e
  if d.kind == "death" or d.kind == "kill" then M.mourn(W, s, d, how, now or W.hour) end
  return e, true
end

-- (S20, R6d) A death learned at village s: the dead one's close kin living there remember it as they
-- come to know it (a spouse is told at once by life.kill).
local KIN = { mother = "My mother", father = "My father", son = "My son", daughter = "My daughter", brother = "My brother", sister = "My sister" }
local function relation(q, p)
  if p.id == q.mother then return "mother" end
  if p.id == q.father then return "father" end
  if p.mother == q.id or p.father == q.id then return p.sex == "f" and "daughter" or "son" end
  if (q.mother ~= 0 and p.mother == q.mother) or (q.father ~= 0 and p.father == q.father) then return p.sex == "f" and "sister" or "brother" end
  return nil
end
M.relation = relation
function M.mourn(W, s, d, how, now)
  local V = W.S.v3
  local p = V.people[d.to]
  if not p then return end
  local people = require("sim.agents.people")
  local here = d.to_s == s.id
  for _, id in ipairs(s.members) do
    local q = V.people[id]
    if q and q.alive and q ~= p and people.stage(q, now) ~= "infant" then
      local r = relation(q, p)
      if r then
        local cause = d.kind == "kill" and "killed" or d.what
        people.remember(q, now, here and sfmt("%s %s died (%s)", KIN[r], p.name, cause)
          or sfmt("Heard that %s %s died at %s (%s)", KIN[r]:lower(), p.name, (V.settlements[d.to_s] or s).name, cause))
      end
    end
  end
end

-- (S21) The heaviest harm household hid suffered within `days` (faded by age), or nil: what a story
-- names as the cause of the household's decisions (to leave, to make a new place, to take beasts).
function M.top_harm(W, hid, now, days)
  if not hid or hid == 0 then return nil end
  local L = list(W)
  local lim = now - (days or 60) * 24
  local best, bw = nil, 0
  for i = #L, 1, -1 do
    local d = L[i]
    if d.t < lim then break end
    if d.harm and d.to_hh == hid and d.kind ~= "death" then
      local w = (d.fu or 0) * M.fade(d, now - d.t)
      if w > bw then best, bw = d, w end
    end
  end
  return best
end

-- How much a deed still weighs after `age` hours: harms to goods fade over deed_half_goods days,
-- blood (wounds and killings) over deed_half_blood.
local LN2 = 0.69314718055995
function M.fade(d, age)
  local c = C()
  local half = (d.kind == "kill" or d.kind == "wound") and c.deed_half_blood or c.deed_half_goods
  return exp(-LN2 * max(0, age) / (half * 24))
end

-- The grievance of village s (household hh if given: its own losses weigh fully, the village's
-- others' at deed_kin_k) against village o: the worth of the unsettled harms s knows o's people did
-- to s's people, faded. Returns the sum and the heaviest such deed (for the why).
-- (S20) Are households a and b close kin (a parent, a child, a brother or sister, a spouse of one in
-- the other)?
function M.kin_houses(W, a, b)
  local V = W.S.v3
  local A, B = V.households[a or 0], V.households[b or 0]
  if not A or not B or A == B then return false end
  for _, i in ipairs(A.members) do
    local p = V.people[i]
    if p then
      for _, j in ipairs(B.members) do
        local q = V.people[j]
        if q and (p.mother == q.id or p.father == q.id or q.mother == p.id or q.father == p.id or p.partner == q.id
          or (p.mother ~= 0 and p.mother == q.mother) or (p.father ~= 0 and p.father == q.father)) then return true end
      end
    end
  end
  return false
end

function M.grievance(W, s, o, hh, now, filter)
  local dk = s.dk
  if not dk or not o then return 0, nil end
  local L = list(W)
  local c = C()
  local sum, top, tw = 0, nil, 0
  for i = 1, #dk do
    local e = dk[i]
    if e.by_s == o.id and not e.own and (not filter or filter(e)) then
      local d = L[e.id]
      -- (S20, alliances) a wrong to close kin of household hh in another village weighs with it too
      local kin = hh and d and d.harm and not d.settled and d.to_s ~= s.id and M.kin_houses(W, hh, d.to_hh)
      if d and d.harm and not d.settled and (d.to_s == s.id or kin) then
        local w = (d.fu or 0) * M.fade(d, now - d.t)
        if kin then w = w * c.deed_ally_k
        elseif hh and d.to_hh ~= hh then w = w * c.deed_kin_k end
        -- (S20, R6c) a blow that answered the struck man's own wrong (a taker caught at it) weighs
        -- little with his people
        local a = d.answers and L[d.answers]
        if a and a.by == d.to and a.by ~= 0 then w = w * c.deserved_k end
        sum = sum + w
        if w > tw then top, tw = d, w end
      end
    end
  end
  return sum, top
end

-- (S20, R6) What makes a household afraid of where it lives: the unsettled harms people did to its
-- village's people that the village knows of (takings, wounds, killings - never nature's or the
-- wolves'), the household's own in full and its neighbours' at deed_kin_k, faded; a blow that answered
-- the struck man's own wrong is hardly feared. Returns the sum, the heaviest deed and what the village
-- believes of it (its entry: the doer's village by_s, or 0).
local FEAR = { take = true, wound = true, kill = true }
function M.fear(W, s, hh, now)
  local dk = s.dk
  if not dk then return 0, nil, nil end
  local L = list(W)
  local c = C()
  local sum, top, te, tw = 0, nil, nil, 0
  for i = 1, #dk do
    local e = dk[i]
    local d = L[e.id]
    if d and not e.own and FEAR[d.kind] and d.harm and not d.settled and d.to_s == s.id then
      local w = (d.fu or 0) * M.fade(d, now - d.t)
      if d.to_hh ~= hh then w = w * c.deed_kin_k end
      local a = d.answers and L[d.answers]
      if a and a.by == d.to and a.by ~= 0 then w = w * c.deserved_k end
      sum = sum + w
      if w > tw then top, te, tw = d, e, w end
    end
  end
  return sum, top, te
end

-- Harms s knows were done to its people whose doer it does not know (the losses it cannot answer),
-- faded: the night watch reads these as it reads wolves' kills.
function M.losses(W, s, now, days)
  local dk = s.dk
  if not dk then return 0, nil end
  local L = list(W)
  local n, last = 0, nil
  for i = #dk, 1, -1 do
    local e = dk[i]
    local d = L[e.id]
    if d and d.harm and d.to_s == s.id and not e.own and now - d.t <= days * 24 then
      n = n + 1
      if not last or d.t > last.t then last = d end
    end
  end
  return n, last
end

-- Settle deed d by deed s2 (a return, a payment).
function M.settle(W, d, by)
  if d and not d.settled then d.settled = by and by.id or -1 end
end

-- When people of s meet people of o (econ.on_learn), s hears the deeds o knows that touch either
-- village's people and are not too old to be told (deed_news_days), and what o believes of each.
-- A household's own secret (`own`) is not told.
function M.share(W, s, o, now)
  local dk = o.dk
  if not dk or s == o then return end
  local L = list(W)
  local c = C()
  for i = 1, #dk do
    local e = dk[i]
    local d = L[e.id]
    if d and not e.own and now - d.t <= c.deed_news_days * 24
      and (d.to_s == s.id or d.to_s == o.id or d.by_s == s.id or d.by_s == o.id) then
      local mine = M.known(s, d.id)
      if not mine or (mine.by == 0 and e.by ~= 0) or (mine.by_s == 0 and e.by_s ~= 0) then
        M.learn(W, s, d, sfmt("told by the people of %s", o.name), e.by, e.by_s, now)
      end
    end
  end
end

-- "Bralf of Zurug took one of our ewes" / "someone took a ewe of the Ulfs'" - a deed as village s
-- believes it, for whys and memories.
function M.told(W, s, d, e)
  local V = W.S.v3
  e = e or M.known(s, d.id)
  local who
  if e and e.by ~= 0 then
    local q = V.people[e.by]
    local o = V.settlements[e.by_s]
    who = q and (q.name .. (o and o ~= s and (" of " .. o.name) or "")) or "someone"
  elseif e and e.by_s ~= 0 then
    local o = V.settlements[e.by_s]
    who = o and ("someone of " .. o.name) or "someone"
  else
    who = "someone"
  end
  return who
end

return M
