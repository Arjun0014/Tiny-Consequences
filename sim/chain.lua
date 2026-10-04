-- Chain reaction (S23, D-182; docs/v3/77_CHAIN_REACTION.md): what each of the god's interventions caused.
-- An observer like the news: it reads the world's causal records and never writes W.S (its runtime is
-- `W.chainrt`, not saved: after a load it is rebuilt from the deeds and the events the save keeps).
--   chain.update(W, now)       new events and deeds by cursors; live state joined by links once a game hour
--   chain.list(W)              the interventions, newest first
--   chain.get(W, ev)           one intervention (by its root event id)
--   chain.rows(W, I, max)      its consequences as a tree, depth-first: { { node, depth }, ... }
--   chain.of_ev(W, ev)         the intervention event ev descends from, or nil
--   chain.focus(W, node)       where the camera goes for a node (or for the intervention itself)
-- Every node is a reading of a real record (an event whose root is the cast; a deed whose answers / cause /
-- event lead to it) or of live state joined to one by a real link (the planet's blame tag on a village's
-- soil, river or air; a household's decision whose cause is a node; people at the edge of hunger in a
-- household whose heaviest harm is a node). Records of one incident are one node (a fire's houses, a
-- flood's fields), so nothing is counted twice.
-- An intervention: { ev, kind, power, cell, sid, x, y, t, title, kids, by_key, n_direct, n_total, last_t,
--   active }; a node: { id, key, kind, sid, x, y, pid, cell, t0, t1, title, line, n, kids, parent, active,
--   deed, ev }.
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min = math.floor, math.max, math.min

M.POLL_DAYS = 60      -- an intervention's live state is read this long after the cast
M.RECENT_H = 6        -- "still unfolding": a node changed this recently

local POWER_OF = { power_lightning = "lightning", power_rain = "rain", power_drought = "dry", power_heat = "warm",
  power_chill = "cold", power_volcano = "stir", power_quake = "strain", power_cull = "cull", power_introduce = "bring",
  power_meteor = "meteor", power_oil = "oil", power_blight = "blight", power_sickness = "sickness", power_sea = "sea" }
M.POWER_OF = POWER_OF
local POWER_NAME = { lightning = "Lightning", rain = "Rain", dry = "Dry spell", warm = "Warm air", cold = "Cold air",
  stir = "Stir the mountain", strain = "Strain the fault", cull = "Cull", bring = "Bring", meteor = "Meteor", oil = "Oil",
  blight = "Blight", sickness = "Sickness in a herd", sea = "Sea warmth" }
M.POWER_NAME = POWER_NAME

local function rt(W)
  local R = W.chainrt
  if R then return R end
  R = { ev_cur = 0, deed_cur = 0, list = {}, by_ev = {}, node_of_deed = {}, node_of_ev = {}, next_id = 1, poll_h = -1 }
  W.chainrt = R
  return R
end
M.rt = rt

local function sett(W, id) return W.S.v3.settlements[id or 0] end
local function sname(W, id) local s = sett(W, id); return s and s.name or "?" end
local function person(W, id) return W.S.v3.people[id or 0] end
local function pname(W, id) local p = person(W, id); return p and p.name or "someone" end
local function hhname(W, hid)
  local hh = W.S.v3.households[hid or 0]
  local q = hh and person(W, hh.members[1])
  return q and ("the " .. q.surname .. "s") or "a household"
end
local function plural(n, one, many) return n == 1 and ("a " .. one) or (n .. " " .. (many or (one .. "s"))) end
local function cap(t) return (t:gsub("^%l", string.upper)) end
local ANIMAL = { sheep = "a sheep", goat = "a goat", pig = "a pig", cattle = "a cow", deer = "a deer", reindeer = "a reindeer",
  antelope = "an antelope", boar = "a boar", wolf = "a wolf", cat = "a wild cat", dog = "a dog" }
local BUILDING = { house = "a house", hut = "a hut", granary = "the granary", woodstore = "the woodstore", fold = "the fold",
  byre = "the byre", sty = "the sty", dock = "the dock", forge = "the forge", well = "the well", saltpan = "the salt pan" }

-- The village nearest planet cell c (and whether it is near).
local function nearest(W, c)
  local best, bd = nil, -2
  local g = W.grid
  for _, s in ipairs(W.S.v3.settlements) do
    local d = g.X[s.cell] * g.X[c] + g.Y[s.cell] * g.Y[c] + g.Z[s.cell] * g.Z[c]
    if s.alive and d > bd then best, bd = s, d end
  end
  return best, bd > 0.93
end

-- Interventions ------------------------------------------------------------------------------------
local function intervention(W, R, e)
  local power = POWER_OF[e.kind] or (e.kind:gsub("^power_", ""))
  local d = e.data or {}
  local s = d.settlement and sett(W, d.settlement)
  local near = s ~= nil
  if not s then s, near = nearest(W, e.cell) end
  local I = { ev = e.id, kind = e.kind, power = power, cell = e.cell, sid = s and s.id or 0, near = near,
    x = d.x, y = d.y, t = e.hour or 0, kids = {}, by_key = {}, nodes = {}, n_direct = 0, n_total = 0, last_t = e.hour or 0, active = true,
    species = d.species, name = d.name, killed = d.killed, mode = d.mode }
  local where = s and (near and s.name or ("far from " .. s.name)) or "the planet"
  local what = POWER_NAME[power] or power
  if power == "cull" or power == "bring" then
    local sp = d.species == "predators" and "wolves" or (d.species == "fish" and "fish" or "deer")
    what = sfmt("%s %s", what, sp)
  elseif power == "stir" and d.name then
    what = sfmt("Stir %s", d.name)
  end
  I.title = sfmt("%s - %s", what, where)
  R.by_ev[e.id] = I
  R.list[#R.list + 1] = I
  return I
end

local function of_ev(W, R, ev)
  if not ev or ev == 0 then return nil end
  local E = W.E
  local root = E and E.root and E.root[ev]
  return root and R.by_ev[root] or nil
end

-- Nodes -------------------------------------------------------------------------------------------
-- The node of `key` under parent (a node, or nil: the intervention itself), made if new.
local function node(R, I, parent, key, kind, sid, t, fields)
  local nd = I.by_key[key]
  if nd then return nd, false end
  nd = { id = R.next_id, key = key, kind = kind, sid = sid, t0 = t, t1 = t, n = {}, kids = {}, parent = parent, active = false, I = I }
  R.next_id = R.next_id + 1
  if fields then
    nd.x, nd.y, nd.pid, nd.deed, nd.ev, nd.cell = fields.x, fields.y, fields.pid, fields.deed, fields.ev, fields.cell
  end
  I.by_key[key] = nd
  I.nodes[#I.nodes + 1] = nd
  local list = parent and parent.kids or I.kids
  list[#list + 1] = nd
  I.n_total = I.n_total + 1
  if not parent then I.n_direct = I.n_direct + 1 end
  if t > I.last_t then I.last_t = t end
  return nd, true
end

local function touch(I, nd, t)
  if t > nd.t1 then nd.t1 = t end
  if t > I.last_t then I.last_t = t end
end

local function count(nd, k, d) nd.n[k] = (nd.n[k] or 0) + (d or 1); return nd.n[k] end

-- Deeds -------------------------------------------------------------------------------------------
-- The node a deed's own links lead to (its parent in a chain), or nil: (node or false for the intervention
-- itself, the intervention).
local function deed_parent(W, R, d)
  local V = W.S.v3
  local pd = (d.answers and V.deeds[d.answers]) or (d.cause and V.deeds[d.cause])
  if pd and pd ~= d then
    local nd = R.node_of_deed[pd.id]
    if nd then return nd, nd.I end
  end
  local ev = (d.event and d.event ~= 0) and d.event or d.ev
  if ev and ev ~= 0 then
    local I = of_ev(W, R, ev)
    if I then
      if ev == I.ev then return false, I end
      local nd = R.node_of_ev[ev]
      return nd or false, I
    end
  end
  return nil, nil
end

-- (snow on a village whose air the god cooled: the deed carries no event; its village's cell does)
local function state_parent(W, R, d)
  if d.kind ~= "snow" then return nil, nil end
  local s = sett(W, d.at)
  local C = W.C
  if not s or not C then return nil, nil end
  for _, f in ipairs({ "heat_anom", "temp" }) do
    local tg = C[f] and C[f][s.cell]
    local I = tg and tg ~= 0 and of_ev(W, R, tg) or nil
    if I then return (R.node_of_ev[tg] or I.by_key["air:" .. s.id] or false), I end
  end
  return nil, nil
end

local MERGE = { burnt = "fire", doused = "fire", drowned = "flood", fell = "quake" }

local function on_deed(W, R, d)
  local parent, I = deed_parent(W, R, d)
  if not I then parent, I = state_parent(W, R, d) end
  if not I then return end
  local pnode = parent or nil
  local k = d.kind
  local sid = d.at
  local nd
  -- deeds of one incident join its node: a fire's buildings and fields, a flood's fields, a quake's falls
  local mk = MERGE[k]
  if mk and pnode and (pnode.kind == mk or (mk == "quake" and (pnode.kind == "wave" or pnode.kind == "flood" or pnode.kind == "impact"))) then
    nd = pnode
  elseif mk == "fire" then
    nd = node(R, I, pnode, sfmt("fire:%d:%s", sid, pnode and pnode.key or "root"), "fire", sid, d.t, { x = d.x, y = d.y, deed = d.id })
    nd.title = sfmt("Fire at %s", sname(W, sid))
  elseif k == "rained_out" then
    -- (S24) the god's rain put a fire out (the fire's own cause is its own chain)
    nd = node(R, I, I.by_key["soil:" .. sid] or pnode, "rainout:" .. d.id, "rainout", sid, d.t, { x = d.x, y = d.y, deed = d.id })
    nd.title = sfmt("The rain put out the fire at %s (%s burning)", sname(W, sid), plural(d.n or 1, "thing", "things"))
  elseif k == "strike" then
    nd = node(R, I, pnode, "strike:" .. d.id, "strike", sid, d.t, { x = d.x, y = d.y, deed = d.id })
    local what = d.what
    local tx
    if ANIMAL[what] and (d.killed or 0) > 0 then tx = sfmt("The bolt killed %d beasts at %s (%d by the current in the ground)", d.killed + (d.dead and 1 or 0), sname(W, sid), d.killed)
    elseif ANIMAL[what] then tx = sfmt("%s %s %s at %s", "The bolt", d.dead and "killed" or "struck", ANIMAL[what], sname(W, sid))
    elseif what == "bld" or d.bkind then tx = sfmt("The bolt struck %s at %s", BUILDING[d.bkind or "house"] or "a building", sname(W, sid))
    elseif what == "person" then tx = sfmt("The bolt struck someone at %s", sname(W, sid))
    elseif what == "tree" then tx = sfmt("The bolt struck a tree at %s", sname(W, sid))
    else tx = sfmt("The bolt came down at %s", sname(W, sid)) end
    if not ANIMAL[what] and (d.killed or 0) > 0 then tx = tx .. sfmt("; %d beasts by it killed by the current in the ground", d.killed) end
    nd.title = tx
  elseif k == "flood" then
    -- (under the river that rose: the flood is the river over its banks)
    local rv = I.by_key["river:" .. sid]
    if not rv and not pnode then
      local s = sett(W, sid)
      local H = W.S.hydro
      local c = s and s.cell
      local ratio = c and (W.F.flow[c] or 0) / max(1e-6, H and H.flow_mean and H.flow_mean[c] or 1) or 0
      rv = node(R, I, nil, "river:" .. sid, "river", sid, d.t)
      rv.peak = ratio
      rv.title = sfmt("The river at %s ran high (%.1fx its usual)", sname(W, sid), ratio)
    end
    if not pnode then pnode = rv end
    nd = node(R, I, pnode, "flood:" .. d.id, "flood", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("Flood at %s", sname(W, sid))
    nd.line = "The river came over its banks"
  elseif k == "quake" then
    nd = node(R, I, pnode, "quake:" .. d.id, "quake", sid, d.t, { deed = d.id })
    nd.title = sfmt("The ground shook at %s (magnitude %s)", sname(W, sid), tostring(d.what))
  elseif k == "wave" then
    nd = node(R, I, pnode, "wave:" .. d.id, "wave", sid, d.t, { deed = d.id })
    nd.title = sfmt("The sea came over the land at %s", sname(W, sid))
  elseif k == "ash" then
    nd = node(R, I, pnode, "ash:" .. sid, "ash", sid, d.t, { deed = d.id })
    nd.title = sfmt("Ash fell on %s", sname(W, sid))
  elseif k == "snow" then
    nd = node(R, I, pnode, "snow:" .. sid, "snow", sid, d.t, { deed = d.id })
    nd.title = sfmt("Snow fell at %s", sname(W, sid))
  elseif k == "death" or k == "kill" then
    nd = node(R, I, pnode, "death:" .. (d.to or 0), "death", sid, d.t, { deed = d.id, pid = d.to, x = d.x, y = d.y })
    if k == "kill" then nd.title = sfmt("%s killed %s", pname(W, d.by), pname(W, d.to))
    else nd.title = sfmt("%s died (%s)", pname(W, d.to), tostring(d.what or "?")) end
  elseif k == "wound" then
    nd = node(R, I, pnode, "wound:" .. d.id, "wound", sid, d.t, { deed = d.id, pid = d.to })
    nd.title = sfmt("%s struck %s", pname(W, d.by), pname(W, d.to))
  elseif k == "help" then
    nd = node(R, I, pnode, sfmt("help:%d:%d", d.to_hh or 0, d.by_s or 0), "help", sid, d.t, { deed = d.id, pid = d.by, x = d.x, y = d.y })
    nd.title = sfmt("Kin from %s came to help %s", sname(W, d.by_s), hhname(W, d.to_hh))
  elseif k == "culled" then
    nd = node(R, I, pnode, "culled:" .. d.id, "culled", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("%d %s died about %s", d.n or 1, tostring(d.what or "beasts"), sname(W, sid))
  elseif k == "lost" then
    nd = node(R, I, pnode, sfmt("lost:%d:%d", sid, floor(d.t / 24)), "lost", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    local n = count(nd, "beasts")
    nd.title = sfmt("Wolves killed %s at %s", plural(n, d.what or "beast", d.what == "sheep" and "sheep" or nil), sname(W, sid))
  elseif k == "fell" then
    nd = node(R, I, pnode, "fell:" .. d.id, "fell", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("%s fell in at %s", cap(BUILDING[d.what] or ("the " .. tostring(d.what))), sname(W, sid))
  elseif k == "sickness" then
    -- (S23, R7 v2) beasts dead of the god's sickness: one node per village and kind of beast
    nd = node(R, I, pnode, sfmt("sickdead:%d:%s", sid, tostring(d.what)), "sickness", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    local n = count(nd, "dead", d.n or 1)
    nd.title = sfmt("The sickness killed %s at %s", plural(n, d.what == "cattle" and "cow" or (d.what or "beast"), d.what == "sheep" and "sheep" or nil), sname(W, sid))
  elseif k == "blight" then
    nd = node(R, I, pnode, "blight:" .. sid, "blight", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    local n = count(nd, "seen")
    nd.title = sfmt("Blight seen in %s at %s", plural(n, "field"), sname(W, sid))
  elseif k == "impact" then
    nd = node(R, I, pnode, "impact:" .. d.id, "impact", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    -- (S24, D-188) what the rock did there, as the sim counted it at the moment it struck
    local parts = {}
    if (d.dead or 0) > 0 then parts[#parts + 1] = plural(d.dead, "person", "people") .. " killed" end
    if (d.houses or 0) > 0 then parts[#parts + 1] = plural(d.houses, "house") .. " thrown down" end
    if (d.fell or 0) > (d.houses or 0) then parts[#parts + 1] = plural(d.fell - (d.houses or 0), "other building") .. " down" end
    if (d.beasts or 0) > 0 then parts[#parts + 1] = plural(d.beasts, "beast") .. " killed" end
    if (d.flat or 0) > 0 then parts[#parts + 1] = plural(d.flat, "tree") .. " thrown flat" end
    nd.title = sfmt(#parts > 0 and "The rock struck %s" or "The rock struck by %s", sname(W, sid))
    nd.line = #parts > 0 and table.concat(parts, ", ") or nil
  elseif k == "slain" then
    nd = node(R, I, pnode, "slain:" .. d.id, "slain", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("The blast killed %s at %s", plural(d.n or 1, d.what == "cattle" and "cow" or (d.what or "beast"), d.what == "sheep" and "sheep" or nil), sname(W, sid))
  elseif k == "palisade" then
    -- (S23, R6f) a village walling its houses after the harm it remembers (the deed answers the heaviest of it)
    nd = node(R, I, pnode, "palisade:" .. sid, "palisade", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("%s began a palisade round its houses", sname(W, sid))
  elseif k == "fouled" then
    -- (S23, D-185) the oil on a village's shore
    nd = node(R, I, pnode, "fouled:" .. sid, "fouled", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    nd.title = sfmt("Oil on the shore at %s: the shellfish and the weed fouled", sname(W, sid))
  elseif k == "oiled" then
    nd = node(R, I, pnode, "oiled:" .. sid, "oiled", sid, d.t, { deed = d.id, x = d.x, y = d.y })
    local n = count(nd, "gulls", d.n or 1)
    nd.title = sfmt("%s oiled at %s", plural(n, "gull"), sname(W, sid))
  elseif k == "take" then
    nd = node(R, I, pnode, "take:" .. d.id, "take", sid, d.t, { deed = d.id, pid = d.by, x = d.x, y = d.y })
    nd.title = sfmt("%s of %s took %s from %s", pname(W, d.by), sname(W, d.by_s), plural(d.n or 1, d.what or "beast", d.what == "sheep" and "sheep" or nil), sname(W, sid))
  elseif k == "return" or k == "pay" or k == "refuse" then
    nd = node(R, I, pnode, k .. ":" .. d.id, k, sid, d.t, { deed = d.id })
    nd.title = (k == "return" and "The beasts were given back") or (k == "pay" and "It was made good") or "They refused to make it good"
  else
    return
  end
  R.node_of_deed[d.id] = nd
  -- what an incident gathered
  if k == "burnt" then
    -- (one field may burn, be lit again and burn again: each place counts once)
    nd.burnt_at = nd.burnt_at or {}
    local at = sfmt("%s:%.3f:%.3f", tostring(d.what), d.x or 0, d.y or 0)
    if not nd.burnt_at[at] then
      nd.burnt_at[at] = true
      if d.what == "field" then count(nd, "fields") elseif BUILDING[d.what] then count(nd, d.what == "house" and "houses" or "buildings") else count(nd, "things") end
    end
  elseif k == "doused" then count(nd, "doused")
  elseif k == "drowned" then count(nd, "fields")
  elseif k == "fell" then count(nd, d.what == "house" and "houses" or "buildings") end
  if nd.kind == "fire" then
    local n, parts = nd.n, {}
    if (n.houses or 0) > 0 then parts[#parts + 1] = plural(n.houses, "house") end
    if (n.buildings or 0) > 0 then parts[#parts + 1] = plural(n.buildings, "building") end
    if (n.fields or 0) > 0 then parts[#parts + 1] = plural(n.fields, "field") end
    nd.burnt = #parts > 0 and (table.concat(parts, ", ") .. " burnt") or nil
    if (n.doused or 0) > 0 then nd.burnt = (nd.burnt and (nd.burnt .. "; ") or "") .. sfmt("%d put out", n.doused) end
    nd.line = (nd.lit and (nd.lit .. (nd.burnt and "; " or "")) or "") .. (nd.burnt or "")
  elseif nd.kind == "flood" and (nd.n.fields or 0) > 0 then
    nd.line = sfmt("%s drowned", plural(nd.n.fields, "field"))
  elseif nd.kind == "quake" and ((nd.n.houses or 0) + (nd.n.buildings or 0)) > 0 then
    nd.line = sfmt("%s fell in", plural((nd.n.houses or 0) + (nd.n.buildings or 0), "building"))
  end
  touch(I, nd, d.t)
end

-- Events ------------------------------------------------------------------------------------------
local EV_TITLE = {
  quake = function(W, e) return sfmt("The fault gave way (magnitude %.1f)", e.mag or 0) end,
  eruption = function(W, e) return sfmt("%s erupted", (e.data and e.data.name) or "The mountain") end,
  fire_started = function(W, e) return "Fire on the land" end,
  wildfire = function(W, e) return "A wildfire" end,
  population_boom = function(W, e) return "The wild herds grew" end,
  population_crash = function(W, e) return "The wild herds dwindled" end,
}

local function on_event(W, R, e)
  if e.actor == "player" and (e.parent or 0) == 0 then
    intervention(W, R, e)
    return
  end
  local I = of_ev(W, R, e.id)
  if not I then return end
  local f = EV_TITLE[e.kind]
  if not f then return end
  local parent = R.node_of_ev[e.parent or 0] or nil
  local s, near = nearest(W, e.cell)
  local nd = node(R, I, parent, "ev:" .. e.id, "event:" .. e.kind, near and s and s.id or 0, e.hour or 0, { ev = e.id, cell = e.cell })
  nd.title = f(W, e) .. ((s and near) and (" near " .. s.name) or "")
  R.node_of_ev[e.id] = nd
  touch(I, nd, e.hour or 0)
end

-- Live state joined by links (once a game hour) --------------------------------------------------
-- (S24) the tiles of map s under flood water now (the flood's own depths, published for the ways)
local function flood_tiles(s)
  local fd = s.fl and require("sim.agents.map").FLOOD[s]
  if not fd then return 0 end
  local n = 0
  for i = 1, s.N * s.N do if fd[i] > 0 then n = n + 1 end end
  return n
end

local function poll_village(W, R, I, s, now)
  local C, F = W.C, W.F
  local c = s.cell
  local E = W.E
  local function mine(fld)
    local tg = C[fld] and C[fld][c]
    return tg and tg ~= 0 and E.root[tg] == I.ev
  end
  local young = now - I.t < M.POLL_DAYS * 24
  if not young then return end
  -- the soil (rain or a dry spell)
  local sw = I.sw and I.sw[s.id]
  if mine("soil_water") then
    -- (the soil carries the cast's mark; shown once it moved for real - a soaked soil that stays soaked is
    -- not news, its rain ran off: the river tells that)
    if not sw then
      sw = { v0 = I.soil0 and I.soil0[s.id] or F.soil_water[c] }
      sw.lo, sw.hi = sw.v0, sw.v0
      I.sw = I.sw or {}
      I.sw[s.id] = sw
    end
    local v = F.soil_water[c]
    sw.lo, sw.hi = min(sw.lo, v), max(sw.hi, v)
  end
  local nd0 = I.by_key["soil:" .. s.id]
  if sw and mine("soil_water") and (nd0 or sw.hi - sw.v0 >= 0.04 or sw.v0 - sw.lo >= 0.04) then
    local key = "soil:" .. s.id
    local nd = node(R, I, nil, key, "soil", s.id, now)
    nd.v0, nd.lo, nd.hi = sw.v0, sw.lo, sw.hi
    local wetter = nd.hi - nd.v0 >= nd.v0 - nd.lo
    nd.title = sfmt(wetter and "The soil at %s watered (%.2f -> %.2f)" or "The soil at %s dried (%.2f -> %.2f)", s.name, nd.v0, wetter and nd.hi or nd.lo)
    nd.active = true
    touch(I, nd, now)
    -- the growing fields on it: their yield is made of this water (settle.grow_plots)
    local growing = 0
    for k = 1, #(s.plots or {}) do if s.plots[k].stage == "growing" then growing = growing + 1 end end
    if growing > 0 then
      local cn = node(R, I, nd, "crops:" .. s.id, "crops", s.id, now)
      local day = floor(now / 24)
      if cn.day ~= day then cn.day = day; count(cn, "days") end
      cn.title = sfmt(wetter and "The growing fields of %s drank it" or "The growing fields of %s went short of water", s.name)
      cn.line = sfmt("%s, %d growing", plural(cn.n.days, "day"), growing)
      cn.active = true
      touch(I, cn, now)
    end
  elseif nd0 then
    nd0.active = false
  end
  -- the river
  if mine("flow") then
    local H = W.S.hydro
    local ratio = (F.flow[c] or 0) / max(1e-6, H and H.flow_mean and H.flow_mean[c] or 1)
    local nd = I.by_key["river:" .. s.id]
    if ratio >= 1.5 or ratio <= 0.6 or nd then
      local new
      nd, new = node(R, I, nil, "river:" .. s.id, "river", s.id, now)
      nd.peak = max(nd.peak or 0, ratio)
      nd.low = min(nd.low or 99, ratio)
      nd.title = nd.peak >= 1.5 and sfmt("The river at %s ran high (%.1fx its usual)", s.name, nd.peak)
        or sfmt("The river at %s ran low (%.1fx its usual)", s.name, nd.low)
      nd.active = true
      touch(I, nd, now)
      -- (S24) a flood already on the land that the high river spread (its own deed is the river's, older than the cast)
      if nd.peak >= 1.5 and s.fl then
        local n, n0 = flood_tiles(s), I.flood0 and I.flood0[s.id] or 0
        if n0 > 0 and n > n0 * 1.3 + 40 then
          local fn = node(R, I, nd, "spread:" .. s.id, "spread", s.id, now)
          fn.peak = max(fn.peak or 0, n)
          fn.title = sfmt("The flood at %s spread: %d tiles under water (%d before)", s.name, fn.peak, n0)
          fn.active = true
          touch(I, fn, now)
        end
      end
    end
  end
  -- the air
  if mine("heat_anom") then
    local a = F.heat_anom[c] or 0
    if math.abs(a) >= 1 then
      local nd = node(R, I, nil, "air:" .. s.id, "air", s.id, now)
      nd.peak = (a > 0) and max(nd.peak or 0, a) or min(nd.peak or 0, a)
      nd.title = sfmt("The air at %s %d degrees %s", s.name, floor(math.abs(nd.peak) + 0.5), nd.peak > 0 and "warmer" or "colder")
      nd.active = true
      touch(I, nd, now)
    end
  end
end

local function poll_households(W, R, now)
  local V = W.S.v3
  local deeds = require("sim.agents.deeds")
  for hid = 1, (V.next_household or 1) - 1 do
    local hh = V.households[hid]
    if hh and hh.members and #hh.members > 0 then
      -- a household leaving or making a new place, for a cause in a chain
      for _, key in ipairs({ "migrate", "found" }) do
        local m = hh[key]
        local nd0 = type(m) == "table" and m.cause and R.node_of_deed[m.cause]
        if nd0 then
          local I = nd0.I
          local s = sett(W, m.from or hh.sett)
          local nd, new = node(R, I, nd0, key .. ":" .. hid, key == "migrate" and "leaving" or "founding", s and s.id or 0, now,
            { pid = hh.members[1] })
          if new then
            nd.title = key == "migrate" and sfmt("%s left %s for %s", cap(hhname(W, hid)), sname(W, m.from), sname(W, m.to))
              or sfmt("%s set out from %s to make a new place", cap(hhname(W, hid)), sname(W, m.from))
            touch(I, nd, now)
          end
          nd.active = not m.arrived
        end
      end
      -- a household under another's roof, or at the edge of hunger, after a harm in a chain (the harm the
      -- sim itself reads as the household's heaviest, deeds.top_harm)
      local lodged = hh.lodge and hh.lodge ~= 0
      -- (S24) no roof and nobody to take them in: a camp by the square (migrate.lodge)
      local camped = hh.lodge == 0
      local edge = 0
      for _, id in ipairs(hh.members) do local p = V.people[id]; if p and p.alive and p.edge then edge = edge + 1 end end
      local harm = (lodged or camped or edge > 0) and deeds.top_harm(W, hid, now, 30) or nil
      local nd0 = harm and R.node_of_deed[harm.id]
      if nd0 then
        local I = nd0.I
        if lodged then
          local nd, new = node(R, I, nd0, "lodge:" .. hid, "lodging", hh.sett, now, { pid = hh.members[1] })
          if new then
            nd.title = sfmt("%s lodged with %s", cap(hhname(W, hid)), hhname(W, hh.lodge))
            touch(I, nd, now)
          end
          nd.active = true
        elseif camped then
          local nd, new = node(R, I, nd0, "camp:" .. hid, "lodging", hh.sett, now, { pid = hh.members[1] })
          if new then
            nd.title = sfmt("%s, without a roof, camp by the square at %s", cap(hhname(W, hid)), sname(W, hh.sett))
            touch(I, nd, now)
          end
          nd.active = true
        end
        if edge > 0 then
          local nd = node(R, I, nd0, "hunger:" .. hid, "hunger", hh.sett, now, { pid = hh.members[1] })
          nd.peak = max(nd.peak or 0, edge)
          nd.title = sfmt("Hunger: %s of %s at the edge", plural(nd.peak, "person", "people"), hhname(W, hid))
          nd.active = true
          touch(I, nd, now)
        end
      end
      if not lodged or not camped or edge == 0 then
        for _, I in ipairs(R.list) do
          local nd = not lodged and I.by_key["lodge:" .. hid]
          if nd then nd.active = false end
          nd = not camped and I.by_key["camp:" .. hid]
          if nd then nd.active = false end
          nd = edge == 0 and I.by_key["hunger:" .. hid]
          if nd then nd.active = false end
        end
      end
      -- (S23) food neighbours brought (the households' own gift accounts, hh.got) to a household whose heaviest
      -- harm is in a chain: the help that came of it
      local got, G = 0, hh.got
      if G then for k = 1, (V.next_household or 1) - 1 do got = got + (G[k] or 0) end end
      R.got = R.got or {}
      local g0 = R.got[hid]
      R.got[hid] = got
      if g0 and got > g0 + 0.05 then
        local h2 = deeds.top_harm(W, hid, now, 30)
        local nf = h2 and R.node_of_deed[h2.id]
        if nf then
          local nd = node(R, nf.I, nf, "fed:" .. hid, "fed", hh.sett, now, { pid = hh.members[1] })
          nd.n.fu = (nd.n.fu or 0) + (got - g0)
          nd.title = sfmt("Neighbours brought %s food (worth %d FU)", hhname(W, hid), floor(nd.n.fu + 0.5))
          touch(nf.I, nd, now)
        end
      end
    end
  end
end

-- Fires on the maps (every update: a burning tree leaves no deed; each burning thing carries the deed that
-- lit it, `f.deed` - a fire's node is the same whether its burnt houses or its burning trees come first)
local FIRE_WHAT = { tree = "trees", bld = "buildings", plot = "fields" }
local function poll_fires(W, R, now)
  for _, s in ipairs(W.S.v3.settlements) do
    local B = s.burn
    if B and #B > 0 then
      for i = 1, #B do
        local f = B[i]
        local nd0 = f.deed and R.node_of_deed[f.deed]
        if nd0 then
          local I = nd0.I
          local pnode = nd0.kind == "fire" and nd0.parent or nd0
          local key = sfmt("fire:%d:%s", s.id, pnode and pnode.key or "root")
          local nd, new = node(R, I, pnode, key, "fire", s.id, f.t0 or now, { x = f.x, y = f.y })
          if new then nd.title = sfmt("Fire at %s", s.name) end
          nd.seen = nd.seen or {}
          local k = f.kind .. ":" .. tostring(f.ref)
          if not nd.seen[k] then
            nd.seen[k] = true
            count(nd, "lit_" .. (FIRE_WHAT[f.kind] or "things"))
            local n = nd.n
            local parts = {}
            if (n.lit_trees or 0) > 0 then parts[#parts + 1] = plural(n.lit_trees, "tree") end
            if (n.lit_buildings or 0) > 0 then parts[#parts + 1] = plural(n.lit_buildings, "building") end
            if (n.lit_fields or 0) > 0 then parts[#parts + 1] = plural(n.lit_fields, "field") end
            nd.lit = table.concat(parts, ", ") .. " caught fire"
            nd.line = nd.lit .. (nd.burnt and ("; " .. nd.burnt) or "")
            touch(I, nd, now)
          end
          if not f.out then nd.active = true; nd.burning_t = now end
        end
      end
    end
  end
end

-- The wild herds and packs that carry a cull's or a bring's mark (fauna's own blame: a group keeps the cause
-- of its fortunes - culled, brought, or released when the wolves that hunted it were culled)
local function poll_wild(W, R, I, now)
  local A = W.S.wild
  if not A or not A.h_tag then return end
  local E = W.E
  local cw = require("sim.config").WILD
  local hn, pn, hsp, psp = 0, 0, nil, nil
  for i = 1, A.nh or 0 do
    local tg = A.h_tag[i]
    if tg ~= 0 and E.root[tg] == I.ev then hn = hn + A.h_n[i]; hsp = hsp or A.h_sp[i] end
  end
  for i = 1, A.np or 0 do
    local tg = A.p_tag[i]
    if tg ~= 0 and E.root[tg] == I.ev then pn = pn + A.p_n[i]; psp = psp or A.p_sp[i] end
  end
  local s = sett(W, I.sid)
  local where = s and s.name or "the hills"
  local wolves_cast = I.species == "predators"
  for _, side in ipairs({ { n = pn, sp = psp, pred = true }, { n = hn, sp = hsp, pred = false } }) do
    if side.sp then
      local nm = side.pred and (cw.predators[side.sp] or {}).name or (cw.grazers[side.sp] or {}).name or "wild beasts"
      local plural_nm = nm == "deer" and "deer" or (nm == "wolf" and "wolves" or (nm == "reindeer" and "reindeer" or nm .. "s"))
      local key = (side.pred and "packs:" or "herds:") .. I.ev
      local own = side.pred == wolves_cast
      -- (the other side carries the mark for one way only: deer released by a wolf cull grow, wolves whose
      -- deer were culled go hungry - a change the other way is not the cast's to claim)
      local nd = I.by_key[key]
      local base = nd and nd.v0 or side.n
      local grew_ok = (I.power == "cull") == wolves_cast
      local shows = own or (grew_ok and side.n > base + 0.5) or (not grew_ok and side.n < base - 0.5)
      if not nd then
        I.wild0 = I.wild0 or {}
        I.wild0[key] = I.wild0[key] or side.n
        base = I.wild0[key]
        shows = own or (grew_ok and side.n > base + 0.5) or (not grew_ok and side.n < base - 0.5)
      end
      local new = false
      if shows then nd, new = node(R, I, nil, key, side.pred and "packs" or "herds", I.sid, now) end
      if new then nd.v0 = base end
      if nd then
        nd.peak = max(nd.peak or 0, side.n)
        local n = floor(side.n + 0.5)
        if I.power == "bring" and own then
          nd.title = sfmt("%d %s roam about %s, brought by you", n, plural_nm, where)
        elseif I.power == "cull" and own then
          nd.title = sfmt("The %s about %s thinned: %d left%s", plural_nm, where, n, I.killed and sfmt(", %d killed", floor(I.killed + 0.5)) or "")
        else
          -- (the others the cast touched: the deer let grow when their wolves were culled, the prey of a pack
          -- brought)
          local v0 = floor(nd.v0 + 0.5)
          nd.title = n >= v0 and sfmt("The %s about %s grew (%d -> %d)", plural_nm, where, v0, n)
            or sfmt("The %s about %s fell (%d -> %d)", plural_nm, where, v0, n)
        end
        nd.active = true
        if new or n ~= nd.last_n then touch(I, nd, now) end
        nd.last_n = n
      end
    end
  end
end

-- (S23, R7 v2) The live marks of the new powers: beasts carrying the god's sickness (unseen or showing), fields
-- holding the god's blight (by the cause each carries), the sea's warmth and its fish off a village (the planet's
-- blame on the sea cells beside it), oil off a village, the dust in the sky.
-- (S24) a village's sea: the sea cells within two rings of it (its map reaches that far - Tekheim's shore is two cells
-- off: with only the cells next to it its fishers' sea was not its own)
local function sea_cells(W, s)
  local out = {}
  for _, c in ipairs(require("sim.society.sites").rings(W, s.cell, 2)) do
    if W.geo.ocean[c] == 1 then out[#out + 1] = c end
  end
  return out
end

local function poll_v2(W, R, I, now)
  local V = W.S.v3
  local E = W.E
  local F, Cb = W.F, W.C
  for _, s in ipairs(V.settlements) do
    if s.alive then
      if I.power == "sickness" and s.herds then
        local n, sp = 0, nil
        for _, kind in ipairs({ "flock", "cattle", "swine" }) do
          local H = s.herds[kind]
          for _, a in ipairs(H and H.animals or {}) do
            if a.alive and a.sick_ev and E.root[a.sick_ev] == I.ev and ((a.sick or 0) > 0 or (a.sick_inc or 0) > 0) then n = n + 1; sp = sp or a.sp end
          end
        end
        local nd = I.by_key["sick:" .. s.id]
        if n > 0 or nd then
          local new
          nd, new = node(R, I, nil, "sick:" .. s.id, "sick", s.id, now)
          nd.peak = max(nd.peak or 0, n)
          nd.sp = nd.sp or sp
          local nm = nd.sp == "cattle" and "cattle" or (nd.sp == "sheep" and "sheep" or ((nd.sp or "beast") .. "s"))
          nd.title = n > 0 and sfmt("%d %s carry the sickness at %s", n, nm, s.name) or sfmt("The sickness has passed from the %s at %s (%d had it at once)", nm, s.name, nd.peak)
          nd.active = n > 0
          if new or n ~= nd.last_n then touch(I, nd, now) end
          nd.last_n = n
        end
      elseif I.power == "blight" and s.plots then
        local n, sev = 0, 0
        for _, pl in ipairs(s.plots) do
          if pl.blight and pl.blight_ev and E.root[pl.blight_ev] == I.ev then n = n + 1; sev = max(sev, pl.blight) end
        end
        local sb = s.seed_blight
        local seed = sb and sb.ev and E.root[sb.ev] == I.ev and sb.share > 0.01
        local nd = I.by_key["fields:" .. s.id]
        if n > 0 or nd then
          local new
          nd, new = node(R, I, nil, "fields:" .. s.id, "fields", s.id, now)
          nd.peak = max(nd.peak or 0, n)
          nd.title = n > 0 and sfmt("Blight in %s at %s (the worst %d%% taken)", plural(n, "field"), s.name, floor(sev * 100 + 0.5))
            or sfmt("The blight is gone from the fields of %s (%s had it)", s.name, plural(nd.peak, "field"))
          nd.active = n > 0
          if new or n ~= nd.last_n then touch(I, nd, now) end
          nd.last_n = n
        end
        if seed then
          local sn, new = node(R, I, nd, "seed:" .. s.id, "seed", s.id, now)
          sn.title = sfmt("The grain of %s carries it: %d%% of the store from blighted fields", s.name, floor(sb.share * 100 + 0.5))
          sn.active = true
          if new then touch(I, sn, now) end
        end
      elseif I.power == "sea" or I.power == "oil" or ((I.power == "bring" or I.power == "cull") and I.species == "fish") then
        local cells = sea_cells(W, s)
        local t_on, f_on, o_on, dt, fish, oil, fsum, fn = false, false, false, 0, 0, 0, 0, 0
        for _, c in ipairs(cells) do
          local tg = Cb.sst and Cb.sst[c]
          if tg and tg ~= 0 and E.root[tg] == I.ev then t_on = true; dt = max(dt, math.abs(F.heat_anom[c] or 0)) end
          local fg = Cb.fish and Cb.fish[c]
          if fg and fg ~= 0 and E.root[fg] == I.ev then f_on = true; fsum, fn = fsum + (F.fish and F.fish[c] or 0), fn + 1 end
          fish = fish + (F.fish and F.fish[c] or 0)
          local og = Cb.oil and Cb.oil[c]
          if og and og ~= 0 and E.root[og] == I.ev and (F.oil and F.oil[c] or 0) > 0.01 then o_on = true; oil = max(oil, F.oil[c]) end
        end
        if #cells > 0 then
          if t_on then
            local nd, new = node(R, I, nil, "sst:" .. s.id, "sea", s.id, now)
            nd.title = sfmt("The sea off %s %s", s.name, I.mode ~= "cool" and "warmer" or "cooler")
            nd.active = true
            if new then touch(I, nd, now) end
          end
          if o_on then
            local nd, new = node(R, I, nil, "oil:" .. s.id, "oil", s.id, now)
            nd.title = sfmt("Oil on the sea off %s", s.name)
            nd.active = true
            if new then touch(I, nd, now) end
          end
          local fk = "fish:" .. s.id
          local nd = I.by_key[fk]
          I.fish0 = I.fish0 or {}
          I.fish0[s.id] = I.fish0[s.id] or fish
          if f_on or nd then
            local new
            nd, new = node(R, I, I.by_key["sst:" .. s.id] or I.by_key["oil:" .. s.id], fk, "fish", s.id, now)
            local f0 = I.fish0[s.id]
            if I.power == "bring" or I.power == "cull" then
              -- (S24: a shoal brought or fish taken - the sea there now, against what it can hold)
              nd.title = sfmt("%s off %s: the fish at %d%% of what the sea there holds", I.power == "bring" and "A shoal brought" or "Fish taken",
                s.name, floor((fn > 0 and fsum / fn or 0) * 100 + 0.5))
            else
              nd.title = sfmt("The fish off %s %s (%d%%)", s.name, fish >= f0 and "more" or "fewer", floor((fish / math.max(1e-6, f0)) * 100 + 0.5))
            end
            nd.active = f_on
            if new then touch(I, nd, now) end
          end
        end
      end
    end
  end
  -- (S24) a stirred mountain: the pressure the god added under it, building day by day until it erupts (the eruption
  -- itself comes in as its event, caused by the stirring) - the first thing it changed, shown at once
  if I.power == "stir" then
    local VS = V.volc
    for _, v in ipairs(VS and VS.list or {}) do
      local key = "volc:" .. v.cell
      if v.cause == I.ev or I.by_key[key] then
        local nd, new = node(R, I, nil, key, "volc", I.sid, now)
        local erupted = v.cause ~= I.ev and v.last and v.last * 24 + 24 >= I.t
        local r = (v.thr or 0) > 0 and v.pressure / v.thr or 0
        nd.title = erupted and sfmt("The pressure under %s built past what makes it erupt", v.name or "the mountain")
          or sfmt("Pressure under %s: %d%% of what makes it erupt", v.name or "the mountain", floor(r * 100 + 0.5))
        nd.active = not erupted
        local q = erupted and -1 or floor(r * 10)
        if new or nd.last_q ~= q then touch(I, nd, now) end
        nd.last_q = q
      end
    end
  end
  if I.power == "meteor" then
    local G = W.G
    local nd, new = node(R, I, nil, "dust", "dust", I.sid, now)
    I.dust0 = I.dust0 or (G.aerosol or 0)
    nd.title = sfmt("Dust in the sky over the whole world (%.3f)", G.aerosol or 0)
    nd.active = (G.aerosol or 0) > 0.01
    if new then touch(I, nd, now) end
  end
end

-- (S23) the children of the dead in a chain taken in by another household after the death, left with no
-- grown-up at home (life.take_in records who took each child in and when: the link)
local function poll_orphans(W, R, I, now)
  local V = W.S.v3
  for _, nd in ipairs(I.nodes) do
    if nd.kind == "death" and nd.pid and not nd.orphans_done then
      local p = V.people[nd.pid]
      if p then
        local n, by = 0, nil
        for _, kid in ipairs(p.kids or {}) do
          local k = V.people[kid]
          if k and k.alive and k.taken_by and (k.taken_t or 0) >= nd.t0 then
            n, by = n + 1, by or V.people[k.taken_by]
          end
        end
        if n > 0 then
          local cn = node(R, I, nd, "takenin:" .. nd.pid, "takenin", nd.sid, now, { pid = by and by.id })
          cn.title = sfmt("%s %s taken in by %s", n == 1 and "A child of" or (n .. " children of"), p.name, by and (by.name .. " " .. by.surname) or "others")
          touch(I, cn, now)
          nd.orphans_done = true
        end
      end
    end
  end
end

-- Update ------------------------------------------------------------------------------------------
function M.update(W, now)
  local V = W.S.v3
  if not V or not W.E then return end
  local R = rt(W)
  now = now or V.now or W.hour
  -- events, then deeds (a deed may name an event made the same hour)
  local E = W.E
  for id = R.ev_cur + 1, E.next_id - 1 do
    local e = E.rec[id]
    if e then on_event(W, R, e) end
  end
  R.ev_cur = E.next_id - 1
  -- (the soil each young intervention found at its villages, for "0.10 -> 0.22")
  for _, I in ipairs(R.list) do
    if not I.soil0 then
      I.soil0 = {}
      I.flood0 = {}
      for _, s in ipairs(V.settlements) do
        I.soil0[s.id] = W.F.soil_water[s.cell]
        if s.fl and (I.power == "rain" or I.kind == "power_weather") then I.flood0[s.id] = flood_tiles(s) end
      end
    end
  end
  local D = V.deeds or {}
  for k = R.deed_cur + 1, #D do on_deed(W, R, D[k]) end
  R.deed_cur = #D
  if #R.list > 0 then poll_fires(W, R, now) end
  local h = floor(now)
  if R.poll_h ~= h and #R.list > 0 then
    R.poll_h = h
    for _, I in ipairs(R.list) do
      if now - I.t < M.POLL_DAYS * 24 then
        for _, s in ipairs(V.settlements) do if s.alive then poll_village(W, R, I, s, now) end end
        if I.power == "cull" or I.power == "bring" then poll_wild(W, R, I, now) end
        poll_orphans(W, R, I, now)
        if I.power == "sickness" or I.power == "blight" or I.power == "sea" or I.power == "oil" or I.power == "meteor"
          or I.power == "stir" or ((I.power == "bring" or I.power == "cull") and I.species == "fish") then poll_v2(W, R, I, now) end
      end
    end
    poll_households(W, R, now)
    -- incidents of records that are still going on: a fire burning, flood water standing
    for _, I in ipairs(R.list) do
      local act = false
      for _, nd in ipairs(I.nodes) do
        if nd.kind == "fire" then
          nd.active = nd.burning_t ~= nil and now - nd.burning_t < 0.5
        elseif nd.kind == "flood" then
          local s = sett(W, nd.sid)
          nd.active = s and s.fl ~= nil or false
        end
        if nd.active then act = true end
      end
      I.active = act or (now - I.last_t) < M.RECENT_H
    end
  end
end

-- Reading -----------------------------------------------------------------------------------------
function M.list(W)
  local R = rt(W)
  local out = {}
  for i = #R.list, 1, -1 do out[#out + 1] = R.list[i] end
  return out
end

function M.get(W, ev)
  return rt(W).by_ev[ev or 0]
end

function M.of_ev(W, ev)
  return of_ev(W, rt(W), ev)
end

-- The intervention's nodes depth-first (oldest first at each level), at most `cap` rows.
function M.rows(W, I, cap_n)
  local out = {}
  cap_n = cap_n or 200
  local function walk(list, depth)
    for _, nd in ipairs(list) do
      if #out >= cap_n then return end
      out[#out + 1] = { node = nd, depth = depth }
      walk(nd.kids, depth + 1)
    end
  end
  if I then walk(I.kids, 1) end
  return out
end

-- Where the camera goes: a village and a point on its map, someone to select; or a planet cell.
function M.focus(W, nd, I)
  if nd then
    local s = sett(W, nd.sid)
    if s then return { sid = s.id, x = nd.x or s.sqx or 0, y = nd.y or s.sqy or 0, pid = nd.pid } end
    if nd.cell then return { cell = nd.cell } end
  end
  if I then
    local s = sett(W, I.sid)
    if s and I.near then return { sid = s.id, x = I.x or s.sqx or 0, y = I.y or s.sqy or 0 } end
    return { cell = I.cell }
  end
  return nil
end

return M
