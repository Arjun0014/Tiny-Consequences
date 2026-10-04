-- World news (S21, R7, D-172; docs/v3/75_PLAYER_EXPERIENCE.md): stories read from the world as it is -
-- never invented. An observer only: it reads the deeds (`V.deeds`), the events (`W.E`) and live state
-- (things burning, flood water, the trapped, bands, households on the move) and never writes W.S; its own
-- runtime state is `W.newsrt` (not saved: a reloaded world's feed starts again from what is live and from
-- the deeds of the last days).
--   news.update(W, now)    incremental: new deeds and events by cursors, live state once a game hour
--   news.cards(W, n, now)  the n most important stories now (active BREAKING ones first)
--   news.chain(W, st)      the story's causes, newest first, to its root: { links, root = "you"|"nature"|"people" }
-- A story: { id, kind, level (1 BREAKING, 2 STORY, 3 SLOW), key, sid, x, y, pid, t0, t1, active, closed,
--   title, line, lines = {}, n = {}, deed (its first deed), ev (an event id), score }
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min = math.floor, math.max, math.min

M.LEVEL_W = { 100, 40, 20 }
M.QUIET_H = 12        -- a story with nothing new for this long is over
M.SHOW_H = 36         -- an ended story stays in the feed this long
M.HISTORY = 200       -- stories kept

-- kind -> level and icon (game/ui icons)
M.KINDS = {
  fire = { 1, "fire" }, flood = { 1, "wave" }, quake = { 1, "pickaxe" }, wave = { 1, "wave" }, eruption = { 1, "volcano" },
  raid = { 1, "skull" }, killing = { 1, "skull" }, strike = { 1, "bolt" },
  theft = { 2, "paw" }, leaving = { 2, "people" }, arriving = { 2, "people" }, founding = { 2, "house" },
  kinhelp = { 2, "heart" }, orphans = { 2, "heart" }, death = { 2, "skull" }, bride = { 2, "heart" },
  wolves = { 3, "wolf" }, hunger = { 3, "bread" }, winter = { 3, "snow" }, heat = { 3, "heat" }, ash = { 3, "volcano" },
  -- (everyday life: STORY level, weighed below danger and pressure - a quiet world still shows its life)
  birth = { 2, "heart", 14 }, passing = { 2, "person", 16 }, wedding = { 2, "heart", 12 }, house = { 2, "house", 10 },
  trade = { 2, "people", 16 }, fair = { 2, "star", 32 }, relief = { 2, "bread", 34 }, harvest = { 2, "wheat", 18 },
  -- (S22) a power cast: what it changed in the world, at once (its consequences are their own stories)
  power = { 2, "star", 30 }, culled = { 2, "paw", 26 },
  -- (S23, R7 v2)
  sickness = { 3, "thermo", 26 }, blight = { 3, "blight", 26 }, impact = { 1, "meteor" }, oil = { 3, "oil", 30 },
  palisade = { 2, "house", 40 },
}
-- (S22) everyday life: at most this many cards at once (four "a new house" cards hid the rest)
M.EVERYDAY = { birth = true, passing = true, wedding = true, house = true, trade = true, fair = true, harvest = true }
M.EVERYDAY_MAX = 2

local function rt(W)
  local R = W.newsrt
  if R then return R end
  local V = W.S.v3
  local now = (V and V.now) or W.hour
  R = { deed_cur = 0, ev_cur = (W.E and W.E.next_id or 1) - 1, list = {}, by_key = {}, next_id = 1, poll = {}, seen_mig = {} }
  -- (start from the deeds of the last days: a reloaded world's feed is not empty)
  local D = V and V.deeds or {}
  local i = #D
  while i > 0 and D[i].t and now - D[i].t < 48 do i = i - 1 end
  R.deed_cur = i
  W.newsrt = R
  return R
end
M.rt = rt

local function sett(W, id) return W.S.v3.settlements[id or 0] end
local function person(W, id) return W.S.v3.people[id or 0] end
local function household_name(W, hid)
  local hh = W.S.v3.households[hid or 0]
  local q = hh and person(W, hh.members[1])
  return q and ("the " .. q.surname .. "s") or "a household"
end

-- The story of `key`, made if new (or if the old one has closed).
local function story(W, R, key, kind, sid, now, fields)
  local st = R.by_key[key]
  if st and not st.closed then return st, false end
  local K = M.KINDS[kind]
  st = { id = R.next_id, kind = kind, level = K[1], icon = K[2], weight = K[3], key = key, sid = sid, t0 = now, t1 = now, active = true,
    closed = false, lines = {}, n = {} }
  R.next_id = R.next_id + 1
  if fields then for k, v in pairs(fields) do st[k] = v end end
  R.by_key[key] = st
  local L = R.list
  L[#L + 1] = st
  if #L > M.HISTORY then
    local old = table.remove(L, 1)
    if R.by_key[old.key] == old then R.by_key[old.key] = nil end
  end
  return st, true
end

local function cap(t) return (t:gsub("^%l", string.upper)) end

local function line(st, text, now)
  text = cap(text)
  local L = st.lines
  if L[#L] ~= text then L[#L + 1] = text end
  if #L > 6 then table.remove(L, 1) end
  st.line = text
  st.t1 = now
end

local function count(st, k, d) st.n[k] = (st.n[k] or 0) + (d or 1); return st.n[k] end

local function plural(n, one, many) return n == 1 and ("a " .. one) or (n .. " " .. (many or (one .. "s"))) end

-- The open story of `kind` at village sid (for deeds that belong to an incident there), or nil.
local function open_at(R, kind, sid)
  for i = #R.list, 1, -1 do
    local st = R.list[i]
    if st.kind == kind and st.sid == sid and not st.closed then return st end
  end
  return nil
end

-- Deeds ------------------------------------------------------------------------------------------
local HOUSE = { house = true, hut = true }
local BUILDING_WORD = { house = "a house", hut = "a hut", granary = "the granary", woodstore = "the woodstore", fold = "the fold",
  byre = "the byre", sty = "the sty", dock = "the dock", forge = "the forge", well = "the well", saltpan = "the salt pan" }

local ANIMAL = { sheep = "a sheep", goat = "a goat", pig = "a pig", cattle = "a cow", deer = "a deer", reindeer = "a reindeer",
  antelope = "an antelope", boar = "a boar", wolf = "a wolf", cat = "a wild cat", dog = "a dog" }

local function on_deed(W, R, d, now)
  local s = sett(W, d.at)
  if not s then return end
  local k = d.kind
  if k == "strike" and ANIMAL[d.what] then
    -- (S22) lightning on a beast: its own story - who lost it (a household's beast or dog), or a wild one
    local st = story(W, R, "strike:" .. d.id, "strike", s.id, d.t, { x = d.x, y = d.y, deed = d.id, ev = d.event })
    st.deed = st.deed or d.id
    st.title = sfmt("Lightning %s %s at %s", d.dead and "killed" or "struck", ANIMAL[d.what], s.name)
    -- (S24) the current through the ground killed others standing about it
    local more = d.killed or 0
    if more > 0 then
      st.title = sfmt("Lightning killed %d beasts at %s", more + (d.dead and 1 or 0), s.name)
    end
    if (d.to_hh or 0) ~= 0 then
      line(st, sfmt("%s %s%s", d.dead and "Lost by" or "Belongs to", household_name(W, d.to_hh),
        more > 0 and sfmt("; %d more dead of the current in the ground, the herd scattered", more) or ""), d.t)
    else
      line(st, d.dead and "A wild one, dead where it stood" or "A wild one: it bolted", d.t)
    end
    st.weak = not d.dead and (d.to_hh or 0) == 0 and more == 0
    st.active = false
    return
  end
  if k == "culled" then
    -- (S22) the wild animals of a map that died where they stood when the god culled them
    local st = story(W, R, "culled:" .. d.id, "culled", s.id, d.t, { x = d.x, y = d.y, deed = d.id, ev = d.event })
    local n = d.n or 1
    st.title = sfmt("%s %s dead about %s", n == 1 and "A" or tostring(n), n == 1 and (d.what or "beast") or (d.what == "deer" and "deer" or ((d.what or "beast") .. "s")), s.name)
    line(st, "Dead where they stood: carcasses in the woods and on the pasture", d.t)
    st.active = false
    return
  end
  if k == "strike" then
    -- lightning: news when it starts a fire or strikes someone (the fire's own story carries on)
    local st = story(W, R, "fire:" .. s.id, "strike", s.id, d.t, { x = d.x, y = d.y, deed = d.id })
    -- (S22: what it struck, in the title)
    st.title = st.title or sfmt("Lightning struck %s at %s", d.what == "tree" and "a tree" or (d.what == "ground" and "the ground"
      or (d.what == "bld" and (BUILDING_WORD[d.bkind or ""] or "a building")) or "someone"), s.name)
    line(st, d.what == "person" and "Lightning struck someone in the open" or (d.what == "ground" and "Lightning struck the ground")
      or (d.what == "tree" and "Lightning struck a tree") or sfmt("Lightning struck %s", BUILDING_WORD[d.bkind or ""] or "a building"), d.t)
    st.weak = d.what == "ground" or d.what == "tree"
    -- (S22) a bolt the player sent is news whatever it hit: they want to see where it went
    local pe = d.event and W.E and W.E.rec[d.event]
    if pe and pe.actor == "player" then st.weak = false; st.ev = st.ev or d.event end
    for _, f in ipairs(s.burn or {}) do
      if f.deed == d.id and not f.out then
        st.weak = false
        st.kind, st.icon = "fire", M.KINDS.fire[2]
        st.title = sfmt("Fire in %s", s.name)
        line(st, f.kind == "bld" and sfmt("%s caught fire", BUILDING_WORD[(s.buildings[f.ref] or {}).kind] or "a building") or "A tree caught fire", d.t)
        break
      end
    end
    if d.what == "person" and d.to ~= 0 then
      local q = person(W, d.to)
      st.weak = false
      st.pid = d.to
      st.title = sfmt("Lightning struck %s at %s", q and q.name or "someone", s.name)
    end
  elseif k == "doused" then
    local st = story(W, R, "fire:" .. s.id, "fire", s.id, d.t, { x = d.x, y = d.y, deed = d.id })
    st.deed = st.deed or d.id
    st.title = sfmt("Fire in %s", s.name)
    st.weak = false
    line(st, sfmt("%s caught fire: the neighbours put it out%s", BUILDING_WORD[d.what] or "A building",
      (d.hours or 0) >= 1 and sfmt(" after %d hours", floor(d.hours + 0.5)) or ""), d.t)
    st.active = false
  elseif k == "burnt" then
    local st = story(W, R, "fire:" .. s.id, "fire", s.id, d.t, { x = d.x, y = d.y, deed = d.id })
    st.deed = st.deed or d.id
    if HOUSE[d.what] then
      local nh = count(st, "houses")
      line(st, sfmt("%s burnt down (%s)", nh == 1 and "A house" or (nh .. " houses"), household_name(W, d.to_hh)), d.t)
      st.weak = false
    elseif d.what == "field" then
      count(st, "fields"); line(st, sfmt("%s burnt", plural(st.n.fields, "field")), d.t); st.weak = false
    else
      count(st, "other"); line(st, sfmt("%s burnt", BUILDING_WORD[d.what] or d.what), d.t); st.weak = false
    end
  elseif k == "quake" then
    local st = story(W, R, "quake:" .. d.id, "quake", s.id, d.t, { deed = d.id, ev = d.event, x = s.sqx, y = s.sqy })
    st.title = sfmt("Quake at %s", s.name)
    line(st, sfmt("A quake of magnitude %s", d.what), d.t)
    R.quake_at = R.quake_at or {}
    R.quake_at[s.id] = st
  elseif k == "fell" then
    local kind = (R.quake_at and R.quake_at[s.id] and not R.quake_at[s.id].closed) and "quake" or nil
    local a0 = d.answers and W.S.v3.deeds[d.answers]
    local st = (a0 and a0.kind == "impact" and open_at(R, "impact", s.id)) or kind and R.quake_at[s.id] or open_at(R, "wave", s.id) or open_at(R, "flood", s.id)
      or story(W, R, "fell:" .. s.id, "quake", s.id, d.t, { deed = d.id, x = d.x, y = d.y })
    st.title = st.title or sfmt("Houses fell at %s", s.name)
    if HOUSE[d.what] then
      local nh = count(st, "fell")
      line(st, sfmt("%s fell in (%s)", nh == 1 and "A house" or (nh .. " houses"), household_name(W, d.to_hh)), d.t)
    else
      line(st, sfmt("%s fell in", BUILDING_WORD[d.what] or d.what), d.t)
    end
    st.x, st.y = d.x or st.x, d.y or st.y
  elseif k == "wave" then
    local st = story(W, R, "wave:" .. d.id, "wave", s.id, d.t, { deed = d.id, ev = d.event, x = s.dock and s.dock.dx or s.sqx, y = s.dock and s.dock.dy or s.sqy })
    st.title = sfmt("The sea came in at %s", s.name)
    line(st, "A wave ran up over the shore", d.t)
  elseif k == "flood" then
    local st = story(W, R, "flood:" .. d.id, "flood", s.id, d.t, { deed = d.id, ev = d.ev or d.event })
    st.title = sfmt("Flood at %s", s.name)
    line(st, "The river is over its banks: water on the low ground", d.t)
    R.flood_at = R.flood_at or {}
    R.flood_at[s.id] = st
  elseif k == "drowned" then
    local st = open_at(R, "flood", s.id)
    if st then
      count(st, "fields")
      line(st, sfmt(d.what == "corn" and "%s of ripe corn rotted in the water (%s)" or "%s drowned (%s)", plural(st.n.fields, "field"),
        (d.to_hh or 0) ~= 0 and household_name(W, d.to_hh) or "the village's"), d.t)
    end
  elseif k == "ash" then
    local st = story(W, R, "ash:" .. s.id .. ":" .. floor(d.t / 72), "ash", s.id, d.t, { deed = d.id, ev = d.event, x = s.sqx, y = s.sqy })
    st.title = sfmt("Ash on %s", s.name)
    line(st, sfmt("Ash from %s lies on the roofs and the grass", d.what), d.t)
  elseif k == "sickness" then
    -- (S23, R7 v2) beasts dead of a sickness that bears a cause (a god's sickness): one story per herd
    local st = story(W, R, "sickness:" .. s.id .. ":" .. tostring(d.what), "sickness", s.id, d.t, { deed = d.id, ev = d.event, x = d.x, y = d.y })
    local nm = d.what == "cattle" and "cattle" or (d.what == "sheep" and "sheep" or ((d.what or "beast") .. "s"))
    st.title = sfmt("Sickness among the %s at %s", nm, s.name)
    local nd = count(st, "beasts", d.n or 1)
    line(st, sfmt("%s dead of it", plural(nd, d.what == "cattle" and "cow" or (d.what or "beast"), d.what == "sheep" and "sheep" or nil)), d.t)
  elseif k == "blight" then
    local st = story(W, R, "blight:" .. s.id .. ":" .. floor(d.t / (24 * 48)), "blight", s.id, d.t, { deed = d.id, ev = d.event, x = d.x, y = d.y })
    st.title = sfmt("Blight in the corn at %s", s.name)
    local nf = count(st, "fields")
    line(st, sfmt("%s spotted and withering (%s)", plural(nf, "field"), household_name(W, d.to_hh)), d.t)
  elseif k == "palisade" then
    -- (S23, R6f) a village raising a palisade round its houses after the raids it remembers
    local st = story(W, R, "palisade:" .. s.id, "palisade", s.id, d.t, { deed = d.id, ev = d.event, x = d.x, y = d.y })
    st.title = sfmt("A palisade at %s", s.name)
    line(st, sfmt("After the raids they remember, they are raising a palisade round the houses (%s)", plural(d.n or 1, "section")), d.t)
  elseif k == "fouled" or k == "oiled" then
    -- (S23, D-185) the oil on a village's coast: the shore fouled, the gulls oiled - one story per village and slick
    local st = story(W, R, "oil:" .. s.id, "oil", s.id, d.t, { deed = d.id, ev = d.event, x = d.x, y = d.y })
    st.title = sfmt("Oil on the coast at %s", s.name)
    if k == "fouled" then
      line(st, "The shellfish and the weed on the rocks are black and dying", d.t)
    else
      local n = count(st, "gulls", d.n or 1)
      line(st, sfmt("%s oiled, the colony dwindling", plural(n, "gull")), d.t)
    end
  elseif k == "impact" then
    -- (S24, D-188) what the rock did where it struck, as the sim counted it at that moment
    local st = story(W, R, "impact:" .. d.id, "impact", s.id, d.t, { deed = d.id, ev = d.event, x = d.x, y = d.y })
    st.title = sfmt("Fire from the sky at %s", s.name)
    local parts = {}
    if (d.dead or 0) > 0 then parts[#parts + 1] = plural(d.dead, "person", "people") .. " killed" end
    if (d.houses or 0) > 0 then parts[#parts + 1] = plural(d.houses, "house") .. " thrown down" end
    if (d.fell or 0) > (d.houses or 0) then parts[#parts + 1] = plural(d.fell - (d.houses or 0), "other building") .. " down" end
    if (d.beasts or 0) > 0 then parts[#parts + 1] = plural(d.beasts, "beast") .. " killed" end
    if (d.flat or 0) > 0 then parts[#parts + 1] = plural(d.flat, "tree") .. " thrown flat" end
    if (d.lit or 0) > 0 then parts[#parts + 1] = sfmt("%d alight", d.lit) end
    line(st, #parts > 0 and ("A rock fell from the sky: " .. table.concat(parts, ", ")) or "A rock fell from the sky beside it: the ground shook", d.t)
    st.weak = false
  elseif k == "slain" then
    -- (S24) beasts the blast and the flash killed
    local st = open_at(R, "impact", s.id)
    if st then
      local nm = d.what == "cattle" and "cattle" or (d.what == "sheep" and "sheep" or ((d.what or "beast") .. "s"))
      line(st, sfmt("%d %s of the village dead in the blast", d.n or 1, nm), d.t)
    end
  elseif k == "snow" then
    local st = story(W, R, "winter:" .. s.id, "winter", s.id, d.t, { deed = d.id, x = s.sqx, y = s.sqy })
    st.title = sfmt("Snow at %s", s.name)
    line(st, "Snow is lying on the fields and the pasture", d.t)
  elseif k == "take" then
    local st
    if d.raid then st = R.by_key["raid:" .. d.by_s .. ":" .. d.raid] end
    if not st then
      st = story(W, R, "theft:" .. s.id .. ":" .. floor(d.t / 24), "theft", s.id, d.t, { deed = d.id, x = d.x, y = d.y, pid = d.by })
      st.title = sfmt("Beasts taken at %s", s.name)
    end
    local nb = count(st, "beasts", d.n or 1)
    local q = person(W, d.by)
    line(st, sfmt("%s led off from %s' fold%s", plural(nb, d.what == "cattle" and "cow" or (d.what or "beast"), d.what == "sheep" and "sheep" or nil),
      household_name(W, d.to_hh), q and sfmt(" by %s of %s", q.name, (sett(W, d.by_s) or {}).name or "?") or ""), d.t)
    st.deed = st.deed or d.id
  elseif k == "return" or k == "pay" or k == "refuse" then
    local a = d.answers and W.S.v3.deeds[d.answers]
    if a and a.kind == "take" then
      local st = R.by_key["theft:" .. a.at .. ":" .. floor(a.t / 24)] or (a.raid and R.by_key["raid:" .. a.by_s .. ":" .. a.raid])
      if st then
        local o = sett(W, d.by_s)
        line(st, (k == "return" and sfmt("Given back by the people of %s", o and o.name or "?"))
          or (k == "pay" and sfmt("Made good by %s", o and o.name or "?")) or sfmt("%s refused to make it good", o and o.name or "?"), d.t)
        st.active = true
      end
    end
  elseif k == "wound" or k == "kill" then
    local by, to = person(W, d.by), person(W, d.to)
    if k == "kill" then
      local st = story(W, R, "killing:" .. d.id, "killing", s.id, d.t, { deed = d.id, x = d.x, y = d.y, pid = d.to })
      st.title = sfmt("A killing at %s", s.name)
      line(st, sfmt("%s of %s was killed by %s of %s", to and to.name or "?", (sett(W, d.to_s) or {}).name or "?", by and by.name or "?", (sett(W, d.by_s) or {}).name or "?"), d.t)
    else
      local st = open_at(R, "raid", d.by_s) or open_at(R, "theft", s.id) or open_at(R, "raid", d.to_s)
      if st then line(st, sfmt("%s was struck by %s", to and to.name or "?", by and by.name or "?"), d.t) end
    end
  elseif k == "death" then
    local to = person(W, d.to)
    local cause = d.what or "?"
    local inc = (cause == "meteor" and open_at(R, "impact", s.id)) or (cause == "burns" and open_at(R, "fire", s.id)) or (cause == "crushed" and (R.quake_at and R.quake_at[s.id]))
      or (cause == "drowned" and (open_at(R, "flood", s.id) or open_at(R, "wave", s.id)))
    if inc and not inc.closed then
      local nd = count(inc, "dead")
      line(inc, sfmt("%s died (%s)", to and to.name or "someone", cause), d.t)
      inc.deaths = nd
    else
      local age = to and (d.t - to.born) / 1152 or 30
      if cause == "old age" or (cause == "illness" and age >= 16) then
        local st = story(W, R, "passing:" .. d.id, "passing", s.id, d.t, { deed = d.id, pid = d.to, x = s.sqx, y = s.sqy })
        st.title = sfmt("A death at %s", s.name)
        line(st, sfmt("%s %s died of %s, aged %d", to and to.name or "?", to and to.surname or "", cause, floor(age)), d.t)
        st.active = false
      end
      if age < 16 or cause == "hunger" or cause == "wounds" or cause == "drowned" then
        local st = story(W, R, "death:" .. s.id .. ":" .. floor(d.t / 24), "death", s.id, d.t, { deed = d.id, pid = d.to, x = s.sqx, y = s.sqy })
        local nd = count(st, "dead")
        st.title = nd == 1 and sfmt("A death at %s", s.name) or sfmt("%d deaths at %s", nd, s.name)
        line(st, sfmt("%s, %s, died of %s", to and to.name or "?", age < 1 and "a baby" or (age < 13 and "a child" or sfmt("%d", floor(age))), cause), d.t)
        if cause == "hunger" then st.level = 1 end
      end
    end
  elseif k == "help" then
    local by = person(W, d.by)
    local st = story(W, R, "kinhelp:" .. d.id, "kinhelp", s.id, d.t, { deed = d.id, pid = d.by, x = d.x, y = d.y })
    st.title = sfmt("Kin help at %s", s.name)
    line(st, sfmt("%s came from %s to help %s raise their house", by and by.name or "?", (sett(W, d.by_s) or {}).name or "?", household_name(W, d.to_hh)), d.t)
    st.active = false
  elseif k == "lost" and d.wolves then
    local st = story(W, R, "wolves:" .. s.id, "wolves", s.id, d.t, { deed = d.id, x = d.x, y = d.y })
    local nb = count(st, "beasts")
    st.title = sfmt("Wolves at %s", s.name)
    line(st, sfmt("%s killed in the night", plural(nb, d.what == "sheep" and "sheep" or (d.what or "beast"), d.what == "sheep" and "sheep" or nil)), d.t)
    st.active = false
  end
end

-- Events -----------------------------------------------------------------------------------------
-- (S22) The village nearest planet cell c, and whether it is near (within a few cells).
local function nearest_village(W, c)
  local best, bd = nil, -2
  local g = W.grid
  for _, s in ipairs(W.S.v3.settlements) do
    local dd = g.X[s.cell] * g.X[c] + g.Y[s.cell] * g.Y[c] + g.Z[s.cell] * g.Z[c]
    if s.alive and dd > bd then best, bd = s, dd end
  end
  return best, bd > 0.93
end

-- (S22) What a cast changed, as a title and a line (the cast's root event; nil: another story tells it).
local POWER_STORY = {
  power_rain = function(W, e, s) return sfmt("Rain over the hills about %s", s.name), sfmt("A cloudburst for %d days", require("sim.config").POWERS.v3_rain_days) end,
  power_drought = function(W, e, s) return sfmt("A dry spell about %s", s.name), sfmt("No rain and a little heat for %d days", require("sim.config").POWERS.v3_dry_days) end,
  power_heat = function(W, e, s) return sfmt("Warm air over %s", s.name), sfmt("%d degrees warmer for %d days", floor(require("sim.config").POWERS.v3_air_k + 0.5), require("sim.config").POWERS.v3_air_days) end,
  power_chill = function(W, e, s) return sfmt("Cold air over %s", s.name), sfmt("%d degrees colder for %d days", floor(require("sim.config").POWERS.v3_air_k + 0.5), require("sim.config").POWERS.v3_air_days) end,
  power_cull = function(W, e, s)
    local wolves = e.data and e.data.species == "predators"
    return sfmt("%s culled about %s", wolves and "Wolves" or "Deer", s.name), wolves and "The packs of the hills about thinned" or "The herds of the hills about thinned"
  end,
  power_introduce = function(W, e, s)
    local sp = e.data and e.data.species
    if sp == "predators" then return sfmt("Wolves brought to the hills about %s", s.name), "A pack now roams there"
    elseif sp == "fish" then return sfmt("Fish brought to the sea off %s", s.name), "A shoal in the water"
    elseif sp == "settlers" then return nil end
    return sfmt("Deer brought to the hills about %s", s.name), "A herd now grazes there"
  end,
  power_volcano = function(W, e, s) return sfmt("%s stirred", e.data and e.data.name or "The mountain"), "Pressure is building under it" end,
  power_sickness = function(W, e, s) return sfmt("Sickness in a herd at %s", s.name), "A few beasts carry it - it will show in a day or two" end,
  power_blight = function(W, e, s) return sfmt("Blight in a field at %s", s.name), "A plant sickness in the growing corn" end,
  power_sea = function(W, e, s)
    local warm = not (e.data and e.data.mode == "cool")
    return sfmt("The sea off %s %s", s.name, warm and "warmed" or "cooled"), sfmt("%d degrees %s for %d days", require("sim.config").POWERS.v3_sea_k, warm and "warmer" or "cooler", require("sim.config").POWERS.v3_sea_days)
  end,
  power_oil = function(W, e, s) return sfmt("Oil on the sea near %s", s.name), "A slick the currents will carry" end,
  power_meteor = function(W, e, s, near) return near and sfmt("A meteor fell near %s", s.name) or sfmt("A meteor fell far from any village (nearest: %s)", s.name), "Heat, fire and shaking where it struck; dust in the sky" end,
  power_quake = function(W, e, s, near)
    return near and sfmt("The fault under %s strained", s.name) or sfmt("A fault strained far from any village (nearest: %s)", s.name), "It may give at any time"
  end,
}

local function on_event(W, R, e, now)
  local V = W.S.v3
  local ps = POWER_STORY[e.kind]
  if ps and e.actor == "player" then
    local s, near = sett(W, e.data and e.data.settlement), true
    if not s then s, near = nearest_village(W, e.cell) end
    local title, text
    if s then title, text = ps(W, e, s, near) end
    if title then
      -- (the same power cast again over the same village within a few hours: one story, counted)
      local st = story(W, R, "power:" .. e.kind .. ":" .. s.id .. ":" .. floor(now / 6), "power", s.id, now, { ev = e.id, cell = e.cell, x = s.sqx, y = s.sqy })
      local n = count(st, "casts")
      st.title = title
      line(st, n == 1 and text or sfmt("%s (cast %d times)", text, n), now)
      st.active = false
    end
    return
  end
  if e.kind == "eruption" then
    -- the nearest village to the mountain is where the camera goes
    local best, bd = nil, -2
    local g = W.grid
    for _, s in ipairs(V.settlements) do
      local dd = g.X[s.cell] * g.X[e.cell] + g.Y[s.cell] * g.Y[e.cell] + g.Z[s.cell] * g.Z[e.cell]
      if s.alive and dd > bd then best, bd = s, dd end
    end
    local st = story(W, R, "eruption:" .. e.id, "eruption", best and best.id or 0, now, { ev = e.id, cell = e.cell })
    st.title = sfmt("%s erupted", e.data and e.data.name or "A volcano")
    line(st, sfmt("An eruption of size %d", e.data and e.data.vei or e.mag or 1), now)
    st.active = true
  elseif e.kind == "settlement_founded" then
    local s = sett(W, e.data and e.data.settlement)
    if s then
      local st = story(W, R, "founding:" .. s.id, "founding", s.id, now, { ev = e.id, x = s.sqx, y = s.sqy })
      st.title = sfmt("A new place: %s", s.name)
      line(st, "Settlers have made camp there", now)
      st.active = false
    end
  elseif e.kind == "birth" then
    local s = sett(W, e.data and e.data.settlement)
    local baby = person(W, e.data and e.data.person)
    if s and baby then
      local st = story(W, R, "birth:" .. s.id .. ":" .. floor(now / 24), "birth", s.id, now, { ev = e.id, pid = baby.mother, x = s.sqx, y = s.sqy })
      local nb = count(st, "babies")
      st.title = nb == 1 and sfmt("A baby at %s", s.name) or sfmt("%d babies at %s", nb, s.name)
      local m = person(W, baby.mother)
      line(st, sfmt("%s was born to %s", baby.name, m and (m.name .. " " .. m.surname) or "?"), now)
      st.active = false
    end
  elseif e.kind == "house_built" then
    local s = sett(W, e.data and e.data.settlement)
    local b = s and s.buildings[e.data.building or 0]
    if s and b and (b.kind == "house" or b.kind == "hut") then
      local st = story(W, R, "house:" .. s.id .. ":" .. floor(now / 48), "house", s.id, now, { ev = e.id, x = b.cx, y = b.cy })
      local nh = count(st, "houses")
      st.title = nh == 1 and sfmt("A new house at %s", s.name) or sfmt("%d new houses at %s", nh, s.name)
      line(st, sfmt("Raised for %s", household_name(W, b.hh)), now)
      st.active = false
    end
  elseif e.kind == "marriage" then
    local a, b = person(W, e.data and e.data.a), person(W, e.data and e.data.b)
    local who = (a and a.move and a) or (b and b.move and b)
    if not who and a and b then
      local s = sett(W, e.data and e.data.settlement)
      if s then
        local st = story(W, R, "wedding:" .. e.id, "wedding", s.id, now, { ev = e.id, pid = a.id, x = s.sqx, y = s.sqy })
        st.title = sfmt("A wedding at %s", s.name)
        line(st, sfmt("%s and %s married", a.name, b.name), now)
        st.active = false
      end
    end
    if who and who.move and who.move.to ~= who.move.from then
      local to, from = sett(W, who.move.to), sett(W, who.move.from)
      local st = story(W, R, "bride:" .. who.id, "bride", from and from.id or 0, now, { pid = who.id, x = from and from.sqx, y = from and from.sqy })
      st.title = sfmt("A bride for %s", to and to.name or "?")
      line(st, sfmt("%s will walk to %s to live with %s", who.name, to and to.name or "?", (person(W, who.move.groom) or {}).name or "her husband"), now)
      st.active = false
    end
  end
end

-- Live state, once a game hour per village ---------------------------------------------------------
local function hottest(s)
  local best, bh = nil, 0
  for _, f in ipairs(s.burn or {}) do
    if not f.out and f.heat > bh then best, bh = f, f.heat end
  end
  return best
end

local function poll_fire(W, R, s, now)
  local V = W.S.v3
  local f = hottest(s)
  local fs = R.by_key["fire:" .. s.id]
  if f then
    if not fs or fs.closed then
      fs = story(W, R, "fire:" .. s.id, "fire", s.id, now, { x = f.x, y = f.y })
    end
    local n = 0
    for _, g in ipairs(s.burn) do if not g.out and g.heat > 0 then n = n + 1 end end
    local b = f.kind == "bld" and s.buildings[f.ref]
    fs.title = sfmt("Fire in %s", s.name)
    fs.kind, fs.icon, fs.level = "fire", M.KINDS.fire[2], 1   -- (S22: a strike's story becomes the fire's)
    -- (S23) the fire's cause: the deed that lit what burns (a bolt, the god's meteor); without it the story had no
    -- link and fell back to NATURE
    if not fs.deed and f.deed then fs.deed = f.deed end
    fs.burning = n
    fs.x, fs.y, fs.active, fs.t1 = f.x, f.y, true, now
    fs.weak = false
    if b and not fs.said_b then
      fs.said_b = true
      line(fs, sfmt("%s is burning", BUILDING_WORD[b.kind] or b.kind), now)
    end
    -- (S22) the woods and fields: a line as the fire grows (by how much burns, not every tree)
    local trees = 0
    for _, g in ipairs(s.burn) do if not g.out and g.heat > 0 and g.kind ~= "bld" then trees = trees + 1 end end
    local size = trees == 0 and 0 or (trees <= 1 and 1 or (trees <= 5 and 2 or (trees <= 15 and 3 or 4)))
    if size > (fs.size or 0) then
      fs.size = size
      line(fs, size == 1 and "A tree is burning" or (size == 2 and "A few trees are burning" or (size == 3 and "The woods are burning" or "A wide fire in the woods")), now)
    end
    -- (who is at it: someone carrying water, else someone near)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and not p.at and (p.objk == "fight_fire" or p.objk == "loose_beasts") then fs.pid = p.id; break end
    end
  elseif fs and not fs.closed and fs.active then
    fs.active = false
    -- (S22: only a story that saw something burn - a bolt on bare ground left no fire to go out)
    if not fs.weak and fs.burning then line(fs, sfmt("The fire is out after %d hours", max(1, floor(now - fs.t0 + 0.5))), now) end
  end
end

local function poll(W, R, s, now)
  local V = W.S.v3
  -- flood
  local fl = s.fl
  local ff = R.flood_at and R.flood_at[s.id]
  if fl and ff and not ff.closed then
    ff.active, ff.t1 = true, now
    local wet = 0
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b and b.wet and b.wet > 0.15 then
        wet = wet + 1
        if not ff.x or HOUSE[b.kind] then ff.x, ff.y = b.cx, b.cy end
      end
    end
    if wet > (ff.n.wet or 0) then ff.n.wet = wet; line(ff, sfmt("Water in %s", plural(wet, "building")), now) end
    if not ff.x then ff.x, ff.y = s.sqx, s.sqy end
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and not p.at and (p.objk == "flee_water" or p.objk == "loose_flood") then ff.pid = p.id; break end
    end
    if (s.acc.loosed or 0) > (ff.n.loosed0 or s.acc.loosed or 0) then line(ff, "Beasts let out of a flooded pen", now) end
    ff.n.loosed0 = s.acc.loosed or 0
  elseif ff and not ff.closed and ff.active and not fl then
    ff.active = false
    line(ff, "The water has gone down", now)
  end
  -- the trapped (a quake, a wave)
  local qs = R.quake_at and R.quake_at[s.id]
  if qs and not qs.closed then
    local T = s.trapped
    local n = 0
    for _, id in ipairs(T or {}) do local p = V.people[id]; if p and p.alive and p.trapped then n = n + 1; qs.pid = p.id; qs.x, qs.y = p.x, p.y end end
    if n > 0 then
      qs.active = true
      if n ~= qs.n.trapped then qs.n.trapped = n; line(qs, sfmt("%s trapped under the rubble", plural(n, "person", "people")), now) end
    elseif (qs.n.trapped or 0) > 0 then
      qs.n.trapped = 0
      qs.active = false
      line(qs, "Everyone trapped has been dug out", now)
    elseif now - qs.t0 > 3 then
      qs.active = false
    end
  end
  -- a band raised to raid, gathering or on its way
  local Rd = s.raid
  R.raid_done = R.raid_done or {}
  if Rd and not R.raid_done[s.id .. ":" .. Rd.id] then
    local o = sett(W, Rd.to)
    local key = "raid:" .. s.id .. ":" .. Rd.id
    local st = story(W, R, key, "raid", s.id, now, { pid = Rd.leader })
    local lead = person(W, Rd.leader)
    st.title = sfmt("Raid from %s", s.name)
    local gone, n = 0, 0
    for _, id in ipairs(Rd.party or {}) do
      local q = person(W, id)
      if q and q.alive then
        n = n + 1
        if q.trip and q.trip.band == Rd.id and q.at == Rd.to then gone = gone + 1; st.x, st.y, st.sid, st.pid = q.x, q.y, Rd.to, q.id end
      end
    end
    if gone > 0 then
      st.title = sfmt("Raid on %s", o and o.name or "?")
      if not st.said_at then st.said_at = true; line(st, sfmt("The band from %s has come to %s", s.name, o and o.name or "?"), now) end
    elseif now < Rd.depart then
      st.x, st.y = s.sqx, s.sqy
      if (st.n.party or 0) ~= n then
        st.n.party = n
        line(st, n <= 1 and sfmt("%s is going round the men: beasts to take at %s", lead and lead.name or "Someone", o and o.name or "?")
          or sfmt("%s and %s will go to take beasts at %s", lead and lead.name or "Someone", plural(n - 1, "other"), o and o.name or "?"), now)
      end
    end
    st.active, st.t1 = true, now
    if now > Rd.depart + 48 then R.raid_done[s.id .. ":" .. Rd.id] = true; st.active = false end
  end
  -- households leaving, founding
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local mg = hh and (hh.migrate or hh.found)
    if mg and mg.decided and not R.seen_mig[hid .. ":" .. floor(mg.decided)] then
      R.seen_mig[hid .. ":" .. floor(mg.decided)] = true
      local found = hh.found == mg
      local o = mg.to and sett(W, mg.to)
      local st = story(W, R, (found and "founding:" or "leaving:") .. hid .. ":" .. floor(mg.decided), found and "founding" or "leaving", s.id, now,
        { pid = hh.members[1], cause = mg.cause, x = s.sqx, y = s.sqy, hh = hid })
      local q1 = person(W, hh.members[1])
      local hn = household_name(W, hid)
      st.title = found and sfmt("%s leave to found", hn:gsub("^%l", string.upper)) or sfmt("%s leave %s", hn:gsub("^%l", string.upper), s.name)
      line(st, mg.why or "Life is hard here", now)
      st.deed = mg.cause
      st.active = true
      if q1 then st.pid = q1.id end
    end
  end
  -- (the leaving stories end when they arrive)
  for i = #R.list, math.max(1, #R.list - 40), -1 do
    local st = R.list[i]
    if (st.kind == "leaving" or st.kind == "founding") and st.hh and not st.closed and st.active then
      local hh = V.households[st.hh]
      local mg = hh and (hh.migrate or hh.found)
      if not mg or mg.arrived or mg.given_up then
        st.active = false
        local o = hh and sett(W, hh.sett)
        line(st, (mg and mg.given_up) and "They gave it up and stayed" or sfmt("They have arrived at %s", o and o.name or "their new home"), now)
      else
        for _, id in ipairs(hh.members) do
          local p = person(W, id)
          if p and p.alive and (p.act == "travel" or p.act == "voyage" or p.act == "passage") then
            if not st.said_road then st.said_road = true; line(st, "They are on their way", now) end
            break
          end
        end
      end
    end
  end
  -- traders at work here (people of another village with goods, their deals made) and relief carried
  for _, id in ipairs(s.visitors or {}) do
    local p = V.people[id]
    local tr = p and p.alive and p.at == s.id and p.trip
    if tr and (tr.sold or 0) > 0 and not tr.news_said then
      R.trade_said = R.trade_said or {}
      local key = p.id .. ":" .. floor(tr.t0 or 0)
      if not R.trade_said[key] then
        R.trade_said[key] = true
        local home = sett(W, p.sett)
        local rel = home and require("sim.agents.econ").rel(home, s)
        local first = rel and rel.deals <= 1
        local st = story(W, R, "trade:" .. key, "trade", s.id, now, { pid = p.id, x = s.sqx, y = s.sqy })
        st.title = first and sfmt("%s and %s trade", home and home.name or "?", s.name) or sfmt("A trader at %s", s.name)
        if first then st.weight = 30 end
        local GW = { smoked = "smoked fish", wool = "wool", cloth = "cloth", hides = "hides", leather = "leather", salt = "salt", grain = "grain",
          cheese = "cheese", quern = "a quern", iron = "iron", sheep = "sheep", goat = "goats", cattle = "cattle", pig = "pigs" }
        line(st, sfmt("%s of %s traded %s for %s%s", p.name, home and home.name or "?", GW[tr.give] or tr.give or "goods", GW[tr.take] or tr.take or "goods",
          first and " - the first trade between them" or ""), now)
        st.active = false
      end
    end
  end
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    local tr = p and p.alive and p.trip
    if tr and tr.ask and (tr.gift or 0) > 0.5 and p.at ~= s.id then
      R.relief_said = R.relief_said or {}
      local key = p.id .. ":" .. floor(tr.t0 or 0)
      if not R.relief_said[key] then
        R.relief_said[key] = true
        local o = sett(W, tr.to)
        local st = story(W, R, "relief:" .. key, "relief", s.id, now, { pid = p.id, x = s.sqx, y = s.sqy })
        st.title = sfmt("Food for %s", s.name)
        line(st, sfmt("%s is bringing %d measures from %s", p.name, floor(tr.gift + 0.5), o and o.name or "?"), now)
        st.active = false
      end
    end
  end
  local fair = s.fair
  if fair and fair.host == s.id and now >= fair.t0 - 6 and now <= fair.t1 then
    local st = story(W, R, "fair:" .. s.id .. ":" .. floor(fair.t0), "fair", s.id, now, { x = s.sqx, y = s.sqy })
    st.title = sfmt("A fair at %s", s.name)
    local n = #(fair.present or {})
    if n ~= st.n.present then st.n.present = n; line(st, n > 0 and sfmt("%s come to it", plural(n, "person", "people")) or "Called for after the harvest", now) end
    st.active, st.t1 = true, now
  end
  -- (S23) children left with no one to keep them, taken in (life.take_in: the village's own count)
  R.taken = R.taken or {}
  local tk = s.acc and s.acc.taken_in or 0
  if R.taken[s.id] == nil then R.taken[s.id] = tk end
  if tk > R.taken[s.id] then
    local n = tk - R.taken[s.id]
    R.taken[s.id] = tk
    local st = story(W, R, "orphans:" .. s.id .. ":" .. floor(now / 24), "orphans", s.id, now, { x = s.sqx, y = s.sqy })
    st.title = sfmt("Children taken in at %s", s.name)
    local nn = count(st, "children", n)
    line(st, sfmt("%s left with no one to keep them, taken in by kin or neighbours", plural(nn, "child", "children")), now)
    st.active = false
  end
  -- (S23) the harvest: from the first ripe field of the season to the last one cut, what came in
  local ripe = 0
  for _, pl in ipairs(s.plots or {}) do if pl.stage == "ripe" then ripe = ripe + 1 end end
  R.harv = R.harv or {}
  local hv = R.harv[s.id]
  local got = s.acc and s.acc.harvest or 0
  if ripe > 0 and not (hv and hv.on) then
    hv = { on = true, from = got, key = "harvest:" .. s.id .. ":" .. floor(now / 24) }
    R.harv[s.id] = hv
  end
  if hv and hv.on then
    local st = story(W, R, hv.key, "harvest", s.id, now, { x = s.sqx, y = s.sqy })
    st.title = sfmt("Harvest at %s", s.name)
    local inn = floor(got - hv.from + 0.5)
    if inn ~= st.n.inn then
      st.n.inn = inn
      line(st, ripe > 0 and sfmt("%d sheaves in so far; %s still standing ripe", inn, plural(ripe, "field")) or sfmt("All in: %d sheaves", inn), now)
    end
    st.active = ripe > 0
    if ripe == 0 then hv.on = false end
  end
  -- hunger at the edge
  local edge, starving = 0, nil
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive and not p.at and (p.edge or p.hunger >= 0.95) then edge = edge + 1; starving = p end
  end
  local hs = R.by_key["hunger:" .. s.id]
  if edge >= 2 then
    hs = story(W, R, "hunger:" .. s.id, "hunger", s.id, now, { x = s.sqx, y = s.sqy })
    hs.title = sfmt("Hunger at %s", s.name)
    if edge ~= hs.n.edge then hs.n.edge = edge; line(hs, sfmt("%s at the edge of starving", plural(edge, "person", "people")), now) end
    hs.pid = starving and starving.id
    hs.active, hs.t1 = true, now
  elseif hs and not hs.closed and hs.active then
    hs.active = false
  end
  -- snow lying (the winter story stays open while it lies)
  local ws = R.by_key["winter:" .. s.id]
  if ws and not ws.closed then
    if (s.snow or 0) > 0.02 then
      ws.active, ws.t1 = true, now
      local culled = s.acc.culled_snow or 0
      if culled > (ws.n.culled or 0) and culled - (ws.n.culled0 or culled) >= 1 then
        line(ws, sfmt("%s killed for the larder: no grass under the snow", plural(culled - (ws.n.culled0 or culled), "beast")), now)
      end
      ws.n.culled0 = ws.n.culled0 or culled
      ws.n.culled = culled
    elseif ws.active then
      ws.active = false
      line(ws, "The snow has melted", now)
    end
  end
end

-- Update ---------------------------------------------------------------------------------------
function M.update(W, now)
  local V = W.S.v3
  if not V then return end
  local R = rt(W)
  now = now or V.now or W.hour
  local D = V.deeds or {}
  for i = R.deed_cur + 1, #D do
    local d = D[i]
    if d then on_deed(W, R, d, now) end
  end
  R.deed_cur = #D
  local E = W.E
  if E then
    local last = E.next_id - 1
    if last < R.ev_cur then R.ev_cur = last end
    for id = R.ev_cur + 1, last do
      local e = E.rec[id]
      if e then on_event(W, R, e, now) end
    end
    R.ev_cur = last
  end
  for _, s in ipairs(V.settlements) do
    if s.alive and (s.burn and #s.burn > 0 or (R.by_key["fire:" .. s.id] and R.by_key["fire:" .. s.id].active)) then poll_fire(W, R, s, now) end
  end
  local h = floor(now)
  if R.poll_h ~= h then
    R.poll_h = h
    for _, s in ipairs(V.settlements) do if s.alive then poll(W, R, s, now) end end
    -- stories with nothing new for a while are over
    for _, st in ipairs(R.list) do
      if not st.closed and not st.active and now - st.t1 > M.QUIET_H then st.closed = true end
    end
  end
end

-- Importance -----------------------------------------------------------------------------------
local function root_of(W, st)
  if st.root_t == floor(st.t1) and st.root then return st.root end
  local ch = M.chain(W, st)
  st.root, st.root_t = ch.root, floor(st.t1)
  return st.root
end

function M.score(W, st, now)
  local n = st.n
  local mag = (n.houses or 0) * 6 + (n.fell or 0) * 6 + (n.dead or 0) * 12 + (st.deaths or 0) * 12 + (n.trapped or 0) * 8
    + (n.beasts or 0) * 2 + (n.fields or 0) * 3 + (n.wet or 0) * 3 + (n.edge or 0) * 4 + (n.party or 0) * 3
  local s = (st.weight or M.LEVEL_W[st.level]) + mag - (now - st.t1) * (st.active and 0.2 or 1.5)
  if root_of(W, st) == "you" then s = s + 25 end
  if st.active and st.level == 1 then s = s + 60 end
  if st.weak then s = s - 70 end
  return s
end

-- The n stories to show now: active and recent ones, by importance.
function M.cards(W, n, now)
  local R = rt(W)
  now = now or (W.S.v3 and W.S.v3.now) or W.hour
  local out = {}
  for i = #R.list, 1, -1 do
    local st = R.list[i]
    local recent = st.active or now - st.t1 < M.SHOW_H
    if recent and st.title and not (st.weak and not st.active and #st.lines <= 1) then
      st.score = M.score(W, st, now)
      out[#out + 1] = st
    end
  end
  table.sort(out, function(a, b) if a.score ~= b.score then return a.score > b.score end return a.id > b.id end)
  -- (S22) everyday life fills the cards only up to EVERYDAY_MAX of them
  local keep, ev = {}, 0
  for i = 1, #out do
    local st = out[i]
    if M.EVERYDAY[st.kind] then
      ev = ev + 1
      if ev <= M.EVERYDAY_MAX then keep[#keep + 1] = st end
    else
      keep[#keep + 1] = st
    end
    if #keep >= (n or 5) then break end
  end
  return keep
end

-- Why? -----------------------------------------------------------------------------------------
local DEED_TEXT = {
  burnt = function(W, d) return sfmt("%s burnt (%s)", BUILDING_WORD[d.what] or ("the " .. d.what), (sett(W, d.at) or {}).name or "?") end,
  strike = function(W, d)
    if ANIMAL[d.what] and (d.killed or 0) > 0 then return sfmt("Lightning killed %d beasts at %s", d.killed + (d.dead and 1 or 0), (sett(W, d.at) or {}).name or "?") end
    if ANIMAL[d.what] then return sfmt("Lightning %s %s at %s", d.dead and "killed" or "struck", ANIMAL[d.what], (sett(W, d.at) or {}).name or "?") end
    return sfmt("Lightning struck at %s", (sett(W, d.at) or {}).name or "?")
  end,
  flood = function(W, d) return sfmt("The river came over its banks at %s", (sett(W, d.at) or {}).name or "?") end,
  drowned = function(W, d)
    local what = d.what == "corn" and "The ripe corn of %s rotted in the flood" or "A field of %s drowned"
    return sfmt(what, (d.to_hh or 0) ~= 0 and household_name(W, d.to_hh) or "the village")
  end,
  quake = function(W, d) return sfmt("The ground shook at %s (magnitude %s)", (sett(W, d.at) or {}).name or "?", d.what) end,
  fell = function(W, d) return sfmt("%s fell in at %s", BUILDING_WORD[d.what] or d.what, (sett(W, d.at) or {}).name or "?") end,
  wave = function(W, d) return sfmt("The sea came over the land at %s", (sett(W, d.at) or {}).name or "?") end,
  ash = function(W, d) return sfmt("Ash from %s fell on %s", d.what, (sett(W, d.at) or {}).name or "?") end,
  snow = function(W, d) return sfmt("Snow fell at %s", (sett(W, d.at) or {}).name or "?") end,
  take = function(W, d)
    local q = person(W, d.by)
    return sfmt("%s of %s took %s from %s", q and q.name or "Someone", (sett(W, d.by_s) or {}).name or "?", plural(d.n or 1, d.what or "beast", d.what == "sheep" and "sheep" or nil), (sett(W, d.at) or {}).name or "?")
  end,
  wound = function(W, d) return sfmt("%s struck %s", (person(W, d.by) or {}).name or "Someone", (person(W, d.to) or {}).name or "someone") end,
  kill = function(W, d) return sfmt("%s killed %s", (person(W, d.by) or {}).name or "Someone", (person(W, d.to) or {}).name or "someone") end,
  death = function(W, d) return sfmt("%s died of %s", (person(W, d.to) or {}).name or "Someone", d.what or "?") end,
  help = function(W, d) return sfmt("%s came to help", (person(W, d.by) or {}).name or "Kin") end,
  lost = function(W, d) return sfmt("Wolves killed a %s at %s", d.what or "beast", (sett(W, d.at) or {}).name or "?") end,
  doused = function(W, d) return sfmt("%s caught fire at %s", BUILDING_WORD[d.what] or "A building", (sett(W, d.at) or {}).name or "?") end,
  culled = function(W, d) return sfmt("%d %s died about %s", d.n or 1, d.what or "beasts", (sett(W, d.at) or {}).name or "?") end,
  sickness = function(W, d) return sfmt("%d %s died of a sickness at %s", d.n or 1, d.what or "beasts", (sett(W, d.at) or {}).name or "?") end,
  blight = function(W, d) return sfmt("Blight in a field of %s", household_name(W, d.to_hh)) end,
  impact = function(W, d) return sfmt("A rock fell from the sky by %s", (sett(W, d.at) or {}).name or "?") end,
  fouled = function(W, d) return sfmt("Oil fouled the shore at %s", (sett(W, d.at) or {}).name or "?") end,
  palisade = function(W, d) return sfmt("%s began a palisade round its houses", (sett(W, d.at) or {}).name or "?") end,
  oiled = function(W, d) return sfmt("%d gulls oiled at %s", d.n or 1, (sett(W, d.at) or {}).name or "?") end,
  ["return"] = function(W, d) return "The beasts were given back" end,
  pay = function(W, d) return "It was made good" end,
  refuse = function(W, d) return "They refused to make it good" end,
}

local EV_TEXT = {
  power_lightning = "YOUR lightning", power_weather = "YOUR weather", power_rain = "YOUR rain", power_drought = "YOUR dry spell",
  power_temperature = "YOUR heat or cold", power_heat = "YOUR warm air", power_chill = "YOUR cold air", power_cull = "YOUR cull",
  power_introduce = "YOUR gift of beasts", power_volcano = "YOU stirred the mountain", power_quake = "YOU strained the fault",
  power_meteor = "YOUR meteor", power_oil = "YOUR oil", power_blight = "YOUR blight", power_sickness = "YOUR sickness",
  power_sea = "YOU warmed or cooled the sea",
}

-- (stories of people's own doing: with no deed or event behind them their root is PEOPLE, not nature)
local PEOPLE_KINDS = { trade = true, fair = true, relief = true, raid = true, leaving = true, founding = true, kinhelp = true,
  wedding = true, house = true, theft = true, bride = true, killing = true }

-- The causes of story st: deeds by their answers / cause / event, events by their parents, to the root.
function M.chain(W, st)
  local V = W.S.v3
  local links = {}
  local seen = {}
  local d = st.deed and V.deeds[st.deed]
  local ev = st.ev
  local root = "nature"
  local root_ev
  for _ = 1, 10 do
    if d then
      if seen["d" .. d.id] then break end
      seen["d" .. d.id] = true
      local f = DEED_TEXT[d.kind]
      links[#links + 1] = { t = d.t, text = f and f(W, d) or d.kind, who = (d.by or 0) ~= 0 and "people" or "nature" }
      if (d.by or 0) ~= 0 then root = "people" else root = "nature" end
      local nd = (d.answers and V.deeds[d.answers]) or (d.cause and V.deeds[d.cause])
      local e2 = (d.event and d.event ~= 0) and d.event or d.ev
      if nd and nd ~= d then d = nd
      elseif e2 and e2 ~= 0 then d = nil; ev = e2
      else break end
    elseif ev and ev ~= 0 and W.E then
      local e = W.E.rec[ev]
      if not e or seen["e" .. ev] then break end
      seen["e" .. ev] = true
      local K = W.E and require("sim.causality.events").KINDS[e.kind]
      local txt = EV_TEXT[e.kind] or (K and K.text) or e.kind
      links[#links + 1] = { t = e.hour or (e.tick and e.tick * (W.hpt or 24)) or 0, text = txt, who = e.actor == "player" and "you" or "nature" }
      root = e.actor == "player" and "you" or "nature"
      if e.actor == "player" then root_ev = ev end
      local par = e.parent or 0
      if par == 0 then break end
      ev = par
    else
      break
    end
  end
  if #links == 0 and PEOPLE_KINDS[st.kind] then root = "people" end
  -- (S23) root_ev: the god's cast at the root (sim/chain.lua's intervention), for the card's "chain" pill
  return { links = links, root = root, root_ev = root == "you" and root_ev or nil }
end

-- Where the camera goes for story st: the village, the point, someone to look at.
function M.focus(W, st)
  local s = sett(W, st.sid)
  return { sid = s and s.id or 0, x = st.x or (s and s.sqx) or 0, y = st.y or (s and s.sqy) or 0, pid = st.pid, cell = st.cell }
end

return M
