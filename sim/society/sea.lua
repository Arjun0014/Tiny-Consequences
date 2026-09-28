-- Sea passage (W3, docs/world/13 §4, D-059): the villages' own boats carry their people overseas.
--
-- A village with a mooring and boats can reach other shores: its porters, brides, migrants,
-- colonists and raiders walk to the mooring, sail the sea cells to a landing by the destination and
-- walk the last steps inland. Each boat seats `boat_seats` people; a party takes as many of its
-- village's free boats as it needs (M.boats_for). Boats on a voyage don't fish. An errand's boats
-- wait beached at the landing and carry the party home again (the return trip inherits them); a
-- one-way party (migrants, colonists, a bride) hands its boats to the village it joins if that
-- village has a mooring, else they sail home on their own. A boat lost at sea (Lightning) drowns
-- those aboard and loses the cargo (shipment_lost).
--
-- Derived (not saved): W.vsea[v.id] = { dist = { [ocean cell] = steps from v's mooring },
-- prev = { [cell] = previous cell } } — a BFS over the sea from the mooring (sea ice is ignored for
-- routing; boats don't leave an iced mooring).
-- Boat fields (saved): state "voyage" (b.party = party id) | "beached" (b.at = landing sea cell,
-- b.since) | "homeward" (b.back = sea cells to sail, b.t0, b.t1); party fields: path, si/sj (first
-- and last sea cell of the path), hl/hs (hours per land/sea step), boats = { ids }.
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local rng = require("sim.rng")

local M = {}
local floor, min, max, ceil = math.floor, math.min, math.max, math.ceil

M.arriving = nil       -- the party whose arrival is being handled (its boats pass to the next leg)
M.lose = function() end   -- fishing.lose (set by sim/society/init.lua: the build rejects require cycles)

-- Sea distances from village v's mooring (derived, cached).
function M.field(W, v)
  local cache = W.vsea
  if not cache then cache = {}; W.vsea = cache end
  local f = cache[v.id]
  if f then return f end
  local dist, prev = {}, {}
  f = { dist = dist, prev = prev }
  cache[v.id] = f
  local m = v.mooring
  if not m then return f end
  local g = W.grid
  local ocean = W.geo.ocean
  local limit = cfgmod.SOCIETY.sea_reach
  dist[m], prev[m] = 0, 0
  local q, head = { m }, 1
  while head <= #q do
    local x = q[head]; head = head + 1
    local d = dist[x]
    if d < limit then
      local base = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[base + k]
        if ocean[j] == 1 and dist[j] == nil then
          dist[j], prev[j] = d + 1, x
          q[#q + 1] = j
        end
      end
    end
  end
  return f
end

-- Seaworthy boats of v free to sail (moored, not laid up), in list order.
function M.free_boats(W, v)
  local out = {}
  if not v.mooring or W.F.ice[v.mooring] >= 0.5 then return out end
  local B = W.ent.boats
  for i = 1, #v.boats do
    local b = B[v.boats[i]]
    -- ("out" and b.trip are yesterday's fishing trip until today's plan, made after the village's
    -- decisions: the boat is home at night, and a voyage takes it instead)
    if b and (b.state == "moored" or b.state == "out") and not b.laid_up then out[#out + 1] = b end
  end
  return out
end

-- How many boats of v are free (no list).
function M.n_free(W, v)
  if not v.mooring or W.F.ice[v.mooring] >= 0.5 then return 0 end
  local B = W.ent.boats
  local n = 0
  for i = 1, #v.boats do
    local b = B[v.boats[i]]
    if b and (b.state == "moored" or b.state == "out") and not b.laid_up then n = n + 1 end
  end
  return n
end

-- Boats needed for n people.
function M.boats_needed(n)
  return max(1, ceil(n / cfgmod.SOCIETY.boat_seats))
end

-- The best landing for reaching `cell` from v's mooring: the sea cell (next to a coastal land cell
-- within sea_inland steps of `cell`) that minimises sailing + walking hours. Returns sea cell, its
-- coastal land cell, sea steps, land steps (coast -> cell), or nil.
function M.landing(W, v, cell)
  local f = M.field(W, v)
  -- (static geography: cached per destination)
  local lc = f.land
  if not lc then lc = {}; f.land = lc end
  local e = lc[cell]
  if e ~= nil then
    if e == false then return nil end
    return e[1], e[2], e[3], e[4]
  end
  local dist = f.dist
  local g = W.grid
  local ocean = W.geo.ocean
  local cfg = cfgmod.SOCIETY
  local inland = cfg.sea_inland
  local walk = cfg.walk_h * 1.5
  -- land BFS from the destination (a few steps)
  local seen = { [cell] = 0 }
  local q, head = { cell }, 1
  local bs, bc, bn, bl, bh = nil, nil, 0, 0, 1e9
  while head <= #q do
    local x = q[head]; head = head + 1
    local lx = seen[x]
    local base = x * 6 - 6
    for k = 1, g.nbr_count[x] do
      local j = g.nbr[base + k]
      if ocean[j] == 1 then
        local d = dist[j]
        if d then
          local h = d * cfg.sail_h + lx * walk
          if h < bh or (h == bh and j < bs) then bs, bc, bn, bl, bh = j, x, d, lx, h end
        end
      elseif seen[j] == nil and lx < inland then
        seen[j] = lx + 1
        q[#q + 1] = j
      end
    end
  end
  if not bs then lc[cell] = false; return nil end
  lc[cell] = { bs, bc, bn, bl }
  return bs, bc, bn, bl
end

-- Travel cost (in laden walking steps, the unit partners and migrants weigh distance in) for v to
-- reach `cell` by sea with n people, or nil (no mooring, no free boats enough, out of reach).
function M.cost(W, v, cell, n)
  if not v.mooring then return nil end
  if M.n_free(W, v) < M.boats_needed(n or 1) then return nil end
  local s, _, sn, ln = M.landing(W, v, cell)
  if not s then return nil end
  local cfg = cfgmod.SOCIETY
  return sn * cfg.sail_h / (cfg.walk_h * 1.5) + ln + 1 + cfg.sea_bias
end

-- The sea leg from v's mooring to sea cell s (mooring first).
local function sea_cells(W, v, s)
  local prev = M.field(W, v).prev
  local out = {}
  local x = s
  local guard = 0
  while x and x ~= 0 and guard < 400 do
    out[#out + 1] = x
    x = prev[x]
    guard = guard + 1
  end
  -- reverse: mooring .. s
  local n = #out
  for i = 1, floor(n / 2) do out[i], out[n + 1 - i] = out[n + 1 - i], out[i] end
  return out
end

-- Route between owner's village and a place overseas. homeward: from `far` back to the owner's
-- village; else from the owner's village to `far`. Returns path, si, sj (indices of the first and
-- last sea cells) or nil.
function M.route(W, owner, far, homeward, land_path)
  local s, coast = M.landing(W, owner, far)
  if not s then return nil end
  local sea = sea_cells(W, owner, s)
  if #sea == 0 or sea[1] ~= owner.mooring then return nil end
  local inland = land_path(W, coast, far, cfgmod.SOCIETY.sea_inland + 1) or { coast }
  if inland[#inland] ~= far then inland[#inland + 1] = far end
  local path = { owner.cell }
  for i = 1, #sea do path[#path + 1] = sea[i] end
  for i = 1, #inland do path[#path + 1] = inland[i] end
  local si, sj = 2, 1 + #sea
  if homeward then
    local n = #path
    for i = 1, floor(n / 2) do path[i], path[n + 1 - i] = path[n + 1 - i], path[i] end
    si, sj = n + 1 - sj, n + 1 - si
  end
  return path, si, sj
end

-- Boats for a party of n from owner (the arriving party's own boats first: the trip home).
function M.take_boats(W, owner, n)
  local need = M.boats_needed(n)
  local out = {}
  local B = W.ent.boats
  local a = M.arriving
  if a and a.boats then
    for i = 1, #a.boats do
      local b = B[a.boats[i]]
      if b and b.state == "beached" and b.village == owner.id and #out < need then out[#out + 1] = b end
    end
  end
  if #out < need then
    local fb = M.free_boats(W, owner)
    for i = 1, #fb do
      if #out >= need then break end
      out[#out + 1] = fb[i]
    end
  end
  return out
end

local WX = {}   -- scratch: the day's weather at a mooring

-- The hour a crossing from sea cell c can set off at, from `dep`: storm days are waited out
-- (three at most; then they go anyway).
function M.weather_wait(W, c, dep)
  local wday = require("sim.society.weather_day")
  for _ = 1, 3 do
    if not wday.get(W, c, dep, WX).storm then return dep end
    dep = dep + 24
  end
  return dep
end

-- A crossing may be caught by a squall (more likely on a wet day): decided when it sets off,
-- deterministically; party.wreck = the hour, somewhere on the sea leg.
function M.squall_roll(W, party)
  local cfg = cfgmod.SOCIETY
  local n1, n2 = party.si - 1, party.sj - party.si
  if n2 <= 0 then return end
  local wx = require("sim.society.weather_day").get(W, party.path[party.si], party.depart, WX)
  local p = cfg.voyage_squall * n2 * (wx.wet and cfg.squall_wet or 1)
  local u = rng.float(W.seed, rng.S.BOAT, party.key, party.depart, 11)
  if u >= p then return end
  -- the moving hour of the wreck, inside the sea leg, then the wall hour (days of walk_hours_day)
  local x = n1 * party.hl + (0.15 + 0.7 * (u / p)) * n2 * party.hs
  local whd = cfg.walk_hours_day
  local days = floor(x / whd)
  party.wreck = party.depart + days * 24 + (x - days * whd)
end

-- The squall strikes: a storm at sea (nature) sinks the party's boats.
function M.wreck(W, party)
  local B = W.ent.boats
  local vs = W.ent.villages
  local i = require("sim.society.schedule").party_pos(W, party, W.hour)
  local c = party.path[min(max(i, party.si), party.sj)]
  local id = events.emit(W, "storm", c, 0, 1, { at_sea = true, party = party.id })
  local ids = {}
  for k = 1, #(party.boats or {}) do ids[k] = party.boats[k] end
  for k = 1, #ids do
    local b = B[ids[k]]
    local owner = b and vs[b.village]
    if b and owner and b.state == "voyage" then M.lose(W, owner, b, id, "storm") end
  end
end

-- The party sets sail (mobility.depart): its boats go with it.
function M.embark(W, party, boats)
  local ids = {}
  for i = 1, #boats do
    local b = boats[i]
    b.state, b.party, b.at, b.since, b.back, b.coast = "voyage", party.id, nil, nil, nil, nil
    ids[#ids + 1] = b.id
  end
  party.boats = ids
end

-- A boat sails home on its own along the sea cells `cells` (landing first).
local function sail_home(W, b, cells)
  local v = W.ent.villages[b.village]
  if not v or not v.alive or not v.mooring then return end
  local back = {}
  for i = 1, #cells do back[i] = cells[i] end
  if back[#back] ~= v.mooring then back[#back + 1] = v.mooring end
  b.state, b.party, b.back = "homeward", nil, back
  b.t0 = W.hour
  b.t1 = W.hour + max(1, floor(#back * cfgmod.SOCIETY.sail_h * 2 + 0.5))
end

-- The party has arrived (mobility.arrive, before and after the arrival is handled).
function M.landed(W, party)
  local B = W.ent.boats
  local vs = W.ent.villages
  for i = 1, #party.boats do
    local b = B[party.boats[i]]
    if b and b.state == "voyage" and b.party == party.id then
      if party.to == b.village then
        b.state, b.party = "moored", nil
      else
        b.state, b.party = "beached", nil
        b.at, b.since = party.path[party.sj], W.hour
        b.coast = party.path[party.sj + 1]
      end
    end
  end
end

-- After the arrival: boats nobody took on stay with the village a one-way party joined (if it has
-- a mooring) or sail home; an errand's boats wait for their people (M.daily sends them home if
-- nobody comes).
function M.settle_boats(W, party, joined, errand)
  local B = W.ent.boats
  local vs = W.ent.villages
  local cells = {}
  for i = party.sj, party.si, -1 do cells[#cells + 1] = party.path[i] end
  for i = 1, #party.boats do
    local b = B[party.boats[i]]
    if b and b.state == "beached" and b.since == W.hour then
      local owner = vs[b.village]
      if joined and joined.alive and joined.mooring and joined ~= owner then
        -- the boat stays with its people
        if owner then
          for k = #owner.boats, 1, -1 do if owner.boats[k] == b.id then table.remove(owner.boats, k) end end
        end
        joined.boats[#joined.boats + 1] = b.id
        b.village, b.home, b.state, b.crew, b.at, b.since = joined.id, joined.mooring, "moored", {}, nil, nil
      elseif not errand then
        sail_home(W, b, cells)
      end
    end
  end
end

-- Daily for village v (fishing.plan): boats coming home arrive; boats left waiting too long at a
-- foreign shore (their party never came back) sail home.
function M.daily(W, v)
  local B = W.ent.boats
  local P = W.ent.parties or {}
  for i = 1, #v.boats do
    local b = B[v.boats[i]]
    if b then
      if b.state == "homeward" and W.hour >= (b.t1 or 0) then
        b.state, b.back, b.t0, b.t1 = "moored", nil, nil, nil
      elseif b.state == "beached" and W.hour - (b.since or 0) > cfgmod.SOCIETY.beach_wait_days * 24 then
        b.state = "homeward"
        local f = M.field(W, v)
        local cells = {}
        local x = b.at
        local guard = 0
        while x and x ~= 0 and guard < 400 do cells[#cells + 1] = x; x = f.prev[x]; guard = guard + 1 end
        sail_home(W, b, cells)
      elseif b.state == "voyage" and not P[b.party or 0] then
        -- (its party is gone: the boat drifts home)
        b.state, b.party = "moored", nil
      end
    end
  end
end

-- The best free coastal site on another landmass v's boats can reach with n people (daughter
-- villages, splits and refugees when their own land has no room): site score less the crossing.
function M.site(W, v, n)
  if not v.mooring or #M.free_boats(W, v) < M.boats_needed(n) then return nil end
  local geo = W.geo
  local sites = require("sim.society.sites")
  local cfg = cfgmod.SOCIETY
  local cost = {}
  return sites.pick_site(W, function(c)
    if geo.coast[c] ~= 1 or geo.continent[c] == v.continent then return false end
    local s, _, sn = M.landing(W, v, c)
    if not s then return false end
    cost[c] = sn * cfg.sail_h / (cfg.walk_h * 1.5) + 1 + cfg.sea_bias
    return true
  end, cfg.min_site_people, false, function(c, sc) return sc - cfg.site_walk_penalty * cost[c] end)
end

-- The sea cell a boat away from its mooring is on now (nil at its mooring or fishing).
function M.cell_of(W, b)
  if b.state == "voyage" then
    local party = W.ent.parties and W.ent.parties[b.party or 0]
    if not party or not party.si then return nil end
    local i = require("sim.society.schedule").party_pos(W, party, W.hour)
    if i < party.si then return nil end
    return party.path[min(i, party.sj)]
  elseif b.state == "beached" then
    return b.at
  elseif b.state == "homeward" and b.back and #b.back > 0 then
    local n = #b.back
    local u = (W.hour - (b.t0 or W.hour)) / max(1, (b.t1 or W.hour) - (b.t0 or W.hour))
    return b.back[min(n, max(1, floor(u * (n - 1) + 0.5) + 1))]
  end
  return nil
end

-- Is party's step index i (the step from path[i] to path[i+1]) at sea?
function M.at_sea(party, i)
  return party.si ~= nil and i >= party.si and i < party.sj
end

-- A boat carrying a party sinks (fishing.lose): those aboard may drown and the cargo is lost.
function M.sunk(W, b, parent)
  local party = b.party and W.ent.parties and W.ent.parties[b.party]
  if not party then return end
  local schedule = require("sim.society.schedule")
  local i = schedule.party_pos(W, party, W.hour)
  if not M.at_sea(party, i) then return end
  local P = W.ent.people
  local ents = require("sim.society.entities")
  local share = 1 / max(1, #party.boats)
  local drowned = 0
  for k = 1, #party.members do
    local p = P[party.members[k]]
    -- the people in this boat (members are shared out over the boats in order)
    local slot = floor((k - 1) / cfgmod.SOCIETY.boat_seats) + 1
    local mine = party.boats[slot] == b.id or (#party.boats == 1)
    if p and not p.dead and mine and rng.float(W.seed, rng.S.BOAT, b.key, p.id, 9) < cfgmod.SOCIETY.drown then
      ents.kill(W, p, "drowned", parent)
      drowned = drowned + 1
    end
  end
  -- (people on an errand are still members of their village: the drowned leave its lists)
  if drowned > 0 then
    local home = W.ent.villages[(party.kind == "returning" or party.kind == "raid_home") and party.to or party.from]
    if home then ents.compact(W, home) end
  end
  -- the cargo in this boat goes down with it
  local c = party.cargo
  local lost = 0
  if c then
    for _, k in ipairs({ "grain", "amount", "got", "left", "wood" }) do
      if type(c[k]) == "number" and c[k] > 0 then
        local x = c[k] * share
        c[k] = c[k] - x
        lost = lost + x
      end
    end
  end
  lost = lost + (party.food or 0) * share
  party.food = (party.food or 0) * (1 - share)
  if lost > 0 and W.record_events then
    local dest = W.ent.villages[party.to]
    local id = events.emit(W, "shipment_lost", b.at or party.path[party.si] or 0, parent or 0, floor(lost + 0.5),
      { from = party.from, to = party.to, kind = party.kind, drowned = drowned })
    if id ~= 0 then
      c = c or {}
      c.lost_ev = id
      party.cargo = c
      -- the village waiting for these goods will blame its shortfall on the loss
      if dest then dest.import_tag, dest.import_tag_h = id, W.hour end
    end
  end
  for k = #party.boats, 1, -1 do if party.boats[k] == b.id then table.remove(party.boats, k) end end
end

return M
