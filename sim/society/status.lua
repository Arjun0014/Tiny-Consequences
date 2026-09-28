-- Status: lineages, household prestige, village leaders and succession (docs/world/18 §2.1, W8,
-- D-057).
--
-- Lineage: every person belongs to the lineage of a founding ancestor, reckoned through fathers
-- or mothers by culture (entities.new_person: odd cultures patrilineal, even matrilineal); people
-- with no known parents found a lineage (W.ent.lineages[id] = { name = the founder's surname,
-- founded, village, founder }).
-- Prestige (per household, h.prestige): respect earned and slowly forgotten (monthly decay):
-- an elder head, a household of the village's founding lineage, the leader's generosity when the
-- village sends relief (relations), success in raids and defence (conflict), and a big catch
-- (fishing). The leader of a village is the head of its most prestigious household; the leader's
-- traits colour the village's choices (a hoarder gives little, a grump raids, the devout give
-- and keep the peace). When a leader dies and two lineages of similar standing both claim the
-- leadership, the village may split along lineage lines: the lesser lineage's households walk
-- away to found their own village (kin-biased fission, Walker & Hill 2014).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local cotag = require("sim.society.cotag")

local M = {}
local floor, min, max = math.floor, math.min, math.max

-- Set by sim/society/init.lua (mobility.depart and the site picker; avoids require cycles).
M.depart = function() end
M.best_site = function() return nil end

local function emit(W, kind, cell, parent, mag, data, v)
  if not W.record_events then return 0 end
  local co = v and cotag.co_of(W, v)
  return events.emit(W, kind, cell, parent, mag, data, co and { co = co } or nil)
end

-- The household's head: its oldest adult (nil if none).
function M.head(W, h)
  local P = W.ent.people
  local best, bb = nil, math.huge
  local ad = cfgmod.SOCIETY.adult_age * 8064
  for i = 1, #h.members do
    local p = P[h.members[i]]
    if p and not p.dead and W.hour - p.born >= ad and p.born < bb then best, bb = p, p.born end
  end
  return best
end

-- Add prestige to household id (0 = none).
function M.honour(W, hid, x)
  local h = hid ~= 0 and W.ent.households[hid]
  if h then h.prestige = (h.prestige or 0) + x end
end

-- The leader's household of village v honoured (generosity, victories) or shamed.
function M.honour_leader(W, v, x)
  if v.leader_h then M.honour(W, v.leader_h, x) end
end

-- Temper of village v from its leader's traits: multipliers for giving and raiding.
function M.temper(W, v)
  local give, war = 1, 1
  local p = v.leader and W.ent.people[v.leader]
  if p and not p.dead then
    for i = 1, #p.traits do
      local t = p.traits[i]
      if t == "hoarder" then give = give * 0.5
      elseif t == "grump" then war = war * 1.6
      elseif t == "devout" then give = give * 1.3; war = war * 0.6
      elseif t == "optimist" then war = war * 0.8
      elseif t == "doomsayer" then give = give * 0.8 end
    end
  end
  return give, war
end

-- Monthly for village v: prestige earned and forgotten, the leader, succession and splits.
function M.monthly(W, v)
  local cfg = cfgmod.STATUS
  local E = W.ent
  local H, P = E.households, E.people
  local hour = W.hour
  local best, bp, second, sp = nil, -1, nil, -1
  local lin_n = {}
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p and p.lin then lin_n[p.lin] = (lin_n[p.lin] or 0) + 1 end
  end
  -- the village's mean herd per household (wealth brings standing, D-061)
  local mean = 0
  for i = 1, #v.households do local h = H[v.households[i]]; if h then mean = mean + (h.beasts or 0) end end
  mean = #v.households > 0 and mean / #v.households or 0
  local wp = cfgmod.HERDS.wealth_prestige
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then
      local pr = (h.prestige or 0) * cfg.decay
      if mean > 0.5 then pr = pr + wp * max(-1, min(3, (h.beasts or 0) / mean - 1)) end
      local hd = M.head(W, h)
      if hd then
        local age = (hour - hd.born) / 8064
        if age >= cfg.elder_age then pr = pr + cfg.elder_gain end
        if v.founder_lin and hd.lin == v.founder_lin then pr = pr + cfg.founder_gain end
      end
      h.prestige = floor(pr * 1000 + 0.5) / 1000
      if hd then
        local s = h.prestige
        if s > bp or (s == bp and best and h.id < best.id) then
          second, sp = best, bp
          best, bp = h, s
        elseif s > sp or (s == sp and second and h.id < second.id) then
          second, sp = h, s
        end
      end
    end
  end
  if not best then v.leader, v.leader_h, v.leader_lin = nil, nil, nil; return end
  local head = M.head(W, best)
  -- scalar stress (Johnson 1982): past a few hundred people, disputes outgrow what a leader and
  -- kin can settle; the largest lineage that is not the leader's may walk away
  if v.pop > cfg.stress_pop and hour - (v.split_last or -1e9) > cfg.split_gap_years * 8064 then
    local p = cfg.stress_prob * (v.pop - cfg.stress_pop) / cfg.stress_pop
    if rng.float(W.seed, rng.S.STATUS, v.key_cell, floor(hour / 672), 2) < p then
      -- the rival: the largest other lineage, led by its most respected household
      local rl, rn = nil, 0
      for i = 1, #v.members do
        local q = P[v.members[i]]
        local l = q and q.lin
        if l and l ~= head.lin and (lin_n[l] > rn or (lin_n[l] == rn and l < rl)) then rl, rn = l, lin_n[l] end
      end
      if rl and rn >= v.pop * cfg.stress_share then
        local lead, lp = nil, -1
        for i = 1, #v.households do
          local h = H[v.households[i]]
          local hd = h and M.head(W, h)
          if hd and hd.lin == rl and (h.prestige or 0) > lp then lead, lp = h, h.prestige or 0 end
        end
        if lead then
          M.split(W, v, rl, lead, 0)
          return
        end
      end
    end
  end
  local old = v.leader and P[v.leader]
  local died = old and old.dead
  if v.leader ~= head.id then
    local prev_lin = v.leader_lin
    v.leader, v.leader_h, v.leader_lin = head.id, best.id, head.lin
    v.leader_since = hour
    local id = emit(W, "new_leader", v.cell, 0, 1, { village = v.id, person = head.id, lineage = head.lin }, nil)
    -- a contested succession: the old leader died, and a second lineage of like standing and size
    -- will not follow the new leader
    if died and second and v.pop >= cfg.split_pop and hour - (v.split_last or -1e9) > cfg.split_gap_years * 8064 then
      local h2 = M.head(W, second)
      if h2 and h2.lin ~= head.lin and sp >= bp * cfg.split_close and (lin_n[h2.lin] or 0) >= v.pop * cfg.split_share then
        local u = rng.float(W.seed, rng.S.STATUS, v.key_cell, hour, 1)
        if u < cfg.split_prob then M.split(W, v, h2.lin, second, id) end
      end
    end
  end
end

-- The households of lineage `lin` leave village v to found their own (led by household `lead`).
function M.split(W, v, lin, lead, parent)
  local cfg = cfgmod.SOCIETY
  local E = W.ent
  local H, P = E.households, E.people
  local site = M.best_site(W, v)
  if not site then return end
  local members = {}
  local nh = 0
  local going = {}
  for i = 1, #v.households do
    local h = H[v.households[i]]
    local hd = h and M.head(W, h)
    if hd and hd.lin == lin then
      nh = nh + 1
      for k = 1, #h.members do
        local p = P[h.members[k]]
        if p and not p.dead and p.away == 0 then members[#members + 1] = p; going[p.id] = true end
      end
    end
  end
  -- the faction: other households whose head counts enough friends among those leaving (D-062)
  local need = cfgmod.SOCIETY.friend_faction
  local extra = 0
  for i = 1, #v.households do
    local h = H[v.households[i]]
    local hd = h and M.head(W, h)
    if hd and hd.lin ~= lin and hd.friends and extra < nh then
      local n = 0
      for k = 1, #hd.friends do if going[hd.friends[k]] then n = n + 1 end end
      if n >= need then
        extra = extra + 1
        for k = 1, #h.members do
          local p = P[h.members[k]]
          if p and not p.dead and p.away == 0 then members[#members + 1] = p end
        end
      end
    end
  end
  nh = nh + extra
  if #members < cfgmod.STATUS.split_min then return end
  local name = W.ent.lineages and W.ent.lineages[lin] and W.ent.lineages[lin].name or "?"
  local id = emit(W, "village_split", v.cell, parent, #members, { village = v.id, lineage = lin, name = name,
    households = nh }, v)
  v.split_last = W.hour
  -- they take their share of the store for the road and the first months
  local share = #members / max(1, v.pop)
  local food = v.stock.grain * share
  local seed = v.stock.seed * share
  v.stock.grain = v.stock.grain - food
  v.stock.seed = v.stock.seed - seed
  M.depart(W, v, members, 0, site, "colonists", id, food, seed)
end

-- Lineages for people who have none (older saves): through a parent if they have one, else
-- one lineage per surname and village (sorted ids: deterministic).
function M.ensure(W)
  local E = W.ent
  local P = E.people
  if not P then return end
  local ids = {}
  for id, p in pairs(P) do if not p.lin then ids[#ids + 1] = id end end
  if #ids == 0 then return end
  table.sort(ids)
  local by = {}
  for pass = 1, 3 do
    for i = 1, #ids do
      local p = P[ids[i]]
      if not p.lin then
        local v = E.villages[p.village]
        local pat = v and (v.culture or 1) % 2 == 1
        local f, m = P[p.father], P[p.mother]
        local l = pat and (f and f.lin or (m and m.lin)) or (m and m.lin or (f and f.lin))
        if l then
          p.lin = l
        elseif pass == 3 then
          local key = (p.village or 0) .. ":" .. tostring(p.surname)
          if not by[key] then by[key] = ents.new_lineage(W, p, v) end
          p.lin = by[key]
        end
      end
    end
  end
  -- founding lineages of the villages: their oldest living lineage
  for _, v in ipairs(E.villages) do
    if not v.founder_lin and v.alive then
      local best, bb = nil, math.huge
      for i = 1, #v.members do
        local p = P[v.members[i]]
        if p and p.lin and p.born < bb then best, bb = p.lin, p.born end
      end
      v.founder_lin = best
    end
  end
end

return M
