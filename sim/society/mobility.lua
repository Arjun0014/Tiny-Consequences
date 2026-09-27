-- People on the move (docs/world/12 §3.3–3.4, 13 §4): household migration by push and pull,
-- travelling parties that really walk their route (and can be caught on the way), founding new
-- villages (daughter villages of crowded places, and refugees with nowhere to go), abandonment.
--
-- Party: { id, key, members (person ids), from (village id), to (village id, 0 = found at site),
--          site (cell), culture, path (cells), depart, arrive (hours), food, seed, reason (event),
--          kind ("migrants" | "colonists" | "orphans") }
-- While travelling, members are out of their village (p.away = party id) and eat what they carry.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local sites = require("sim.society.sites")
local farming = require("sim.society.farming")

local M = {}
local floor, ceil, min, max = math.floor, math.ceil, math.min, math.max

local function emit(W, kind, cell, parent, mag, data)
  if not W.record_events then return 0 end
  return events.emit(W, kind, cell, parent, mag, data)
end

-- Land path from cell a to cell b (BFS over land; nil if unreachable within limit steps).
function M.land_path(W, a, b, limit)
  local g = W.grid
  local ocean = W.geo.ocean
  local prev = { [a] = 0 }
  local q = { a }
  local head = 1
  local steps = { [a] = 0 }
  while head <= #q do
    local x = q[head]; head = head + 1
    if x == b then break end
    if steps[x] < (limit or 60) then
      local base = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[base + k]
        if prev[j] == nil and (ocean[j] == 0 or j == b) then
          prev[j] = x
          steps[j] = steps[x] + 1
          q[#q + 1] = j
        end
      end
    end
  end
  if prev[b] == nil then return nil end
  local path = {}
  local x = b
  while x ~= 0 do table.insert(path, 1, x); x = prev[x] end
  return path
end

-- Travel time (hours) along a walking path: walk_h per step, walk_hours_day per day (camping at
-- night).
local function walk_hours(n_steps)
  local cfg = cfgmod.SOCIETY
  local h = n_steps * cfg.walk_h * 1.5   -- with loads, children, elders
  local days = floor(h / cfg.walk_hours_day)
  return days * 24 + (h - days * cfg.walk_hours_day)
end

-- Send a group of people from village v as a party toward village `to` (or a site cell).
function M.depart(W, v, members, to, site, kind, reason, food, seed)
  local E = W.ent
  local P = E.people
  local target_cell = to ~= 0 and E.villages[to].cell or site
  local path = M.land_path(W, v.cell, target_cell, 80) or { v.cell, target_cell }
  local id = E.next_party
  E.next_party = id + 1
  -- decisions are taken at the village's local midnight; they set off at first light
  local dep = W.hour + floor(v.plan.ws or 6)
  local party = { id = id, key = rng.hash(W.seed, rng.S.PARTY, v.key_cell, W.hour, #members), members = {},
    from = v.id, to = to, site = site or 0, culture = v.culture, path = path, depart = dep,
    arrive = dep + max(2, walk_hours(#path - 1)), food = food or 0, seed = seed or 0, reason = reason or 0,
    kind = kind }
  for i = 1, #members do
    local p = members[i]
    local h = E.households[p.household]
    if h then ents.remove_id(h.members, p.id) end
    ents.remove_id(v.members, p.id)
    p.household = 0
    p.away = id
    p.task, p.site = "travel", 0
    party.members[#party.members + 1] = p.id
  end
  v.pop = #v.members
  E.parties[id] = party
  E.party_list = E.party_list or {}
  E.party_list[#E.party_list + 1] = id
  W.S.society.migrated = W.S.society.migrated + #members
  return party
end

-- A party arrives (hour step): join the destination, or found a village at the site.
local function arrive(W, party)
  local E = W.ent
  local P = E.people
  local living = {}
  for i = 1, #party.members do
    local p = P[party.members[i]]
    if p and not p.dead then p.away = 0; living[#living + 1] = p end
  end
  if #living == 0 then return end
  local dest
  if party.to ~= 0 then
    dest = E.villages[party.to]
    if not dest.alive then dest = nil end
  end
  if not dest then
    -- found a village at the site (or where they stand if the destination is gone)
    local site = party.site ~= 0 and party.site or party.path[#party.path]
    if W.geo.ocean[site] == 1 or W.F.land_use[site] ~= 0 or not sites.free_site(W, site) then
      site = sites.best_site(W, W.geo.continent[party.path[#party.path]], false)
    end
    if not site then
      -- nowhere to live: they wander on and most die (the chronicle may mention it)
      for i = 1, #living do ents.kill(W, living[i], "wander", party.reason) end
      return
    end
    local from = E.villages[party.from]
    local used = {}
    for i = 1, #E.villages do used[E.villages[i].name] = true end
    local ctr = 0
    local key = rng.hash(W.seed, rng.S.NAMES, site, W.hour, 77)
    local function u()
      ctr = ctr + 1
      return rng.float(W.seed, rng.S.NAMES, key, ctr, 0)
    end
    local name = names.unique(u, function(uu) return names.new_village(uu, party.culture, from and from.name or "Old") end, used)
    dest = ents.found_village(W, site, { name = name, culture = party.culture, from = party.from })
    local id = emit(W, "village_founded", site, party.reason, #living, { village = dest.id, from = party.from })
    if id ~= 0 then dest.founded_event = id end
    dest.stock.seed = party.seed
    require("sim.society.init").register_village(W, dest)
  end
  -- households travel as units: re-form them at the destination (people keep partners)
  local by_old = {}
  local order = {}
  for i = 1, #living do
    local p = living[i]
    local key = p.partner ~= 0 and math.min(p.id, p.partner) or p.id
    -- children join a parent's group
    if p.partner == 0 and (p.mother ~= 0 or p.father ~= 0) then
      for _, rid in ipairs({ p.mother, p.father }) do
        local par = rid ~= 0 and P[rid]
        if par and par.away == 0 and not par.dead then
          for j = 1, #living do if living[j] == par then key = par.partner ~= 0 and math.min(par.id, par.partner) or par.id end end
        end
      end
    end
    if not by_old[key] then by_old[key] = {}; order[#order + 1] = key end
    local l = by_old[key]
    l[#l + 1] = p
  end
  for i = 1, #order do
    local group = by_old[order[i]]
    local h = ents.new_household(W, dest, 0)
    for j = 1, #group do ents.move_person(W, group[j], dest, h) end
  end
  dest.stock.grain = dest.stock.grain + party.food
  dest.arrivals_tag = party.reason
  dest.need_day = 0
  require("sim.society.life").households(W, dest)
end

-- Settlers (the Introduce power, 04 §2): `n` people arrive by boat and found a village at the
-- nearest free land to `cell`: two couples and children, with food, seed and a little timber for
-- huts. Returns the village.
function M.settlers(W, cell, n, parent)
  local cfg = cfgmod.SOCIETY
  local g = W.grid
  local site = cell
  if W.geo.ocean[site] == 1 or W.F.land_use[site] ~= 0 or not sites.free_site(W, site) then
    local best, bd = nil, 1e9
    for c = 1, W.n do
      if W.geo.ocean[c] == 0 and W.F.ice[c] < 0.5 and W.F.land_use[c] == 0 and sites.free_site(W, c) then
        local d = 1 - (g.X[c] * g.X[cell] + g.Y[c] * g.Y[cell] + g.Z[c] * g.Z[cell])
        if d < bd then best, bd = c, d end
      end
    end
    site = best
  end
  if not site then return nil end
  local culture = (W.names.cultures and W.names.cultures[W.geo.continent[site]]) or 1
  local ctr = 0
  local key = rng.hash(W.seed, rng.S.NAMES, site, W.hour, 91)
  local function u()
    ctr = ctr + 1
    return rng.float(W.seed, rng.S.NAMES, key, ctr, 0)
  end
  local E = W.ent
  local used = {}
  for i = 1, #E.villages do used[E.villages[i].name] = true end
  local name = names.unique(u, function(uu) return names.village(uu, culture) end, used)
  local v = ents.found_village(W, site, { name = name, culture = culture })
  require("sim.society.init").register_village(W, v)
  local ages = { 27, 25, 31, 29, 7, 4, 33, 30 }
  local sexes = { "m", "f", "m", "f", "f", "m", "m", "f" }
  local made = {}
  local h
  for i = 1, n do
    local k = (i - 1) % #ages + 1
    if i == 1 or i == 3 or i == 7 then h = ents.new_household(W, v, 0) end
    made[i] = ents.new_person(W, v, h, { born = W.hour - ages[k] * 8064 - i, idx = i, sex = sexes[k] })
  end
  for i = 1, n - 1, 2 do
    local a, b = made[i], made[i + 1]
    if a and b and ents.age(W, a) >= 18 and ents.age(W, b) >= 18 then a.partner, b.partner = b.id, a.id end
  end
  local life = require("sim.society.life")
  for i = 1, #made do
    local p = made[i]
    if ents.age(W, p) >= cfg.adult_age then life.set_occupation(W, v, p) end
  end
  local need = require("sim.society.economy").need_day(W, v)
  v.stock.grain = need * 120
  v.stock.seed = cfg.seed * cfg.found_seed_cells
  v.stock.wood = 40
  local id = emit(W, "village_founded", site, parent or 0, n, { village = v.id, settlers = true })
  if id ~= 0 then v.founded_event = id end
  return v
end

-- Hour step: parties due arrive.
function M.hour(W)
  local E = W.ent
  local list = E.party_list
  if not list or #list == 0 then return end
  local k = 0
  for i = 1, #list do
    local party = E.parties[list[i]]
    if party then
      if W.hour >= party.arrive then
        arrive(W, party)
        E.parties[list[i]] = nil
      else
        k = k + 1
        list[k] = list[i]
      end
    end
  end
  for i = #list, k + 1, -1 do list[i] = nil end
end

-- Abandonment: nobody is left.
function M.abandon(W, v)
  local F = W.F
  v.alive = false
  v.state = "abandoned"
  v.abandoned = W.hour
  v.ruin = true
  W.S.ruins[v.cell] = W.tick
  for i = 1, #v.land do
    local c = v.land[i]
    if F.land_use[c] == -v.id then F.land_use[c] = 0 end
  end
  farming.release_all(W, v)
  local B = W.ent.buildings
  for i = 1, #v.buildings do
    local b = B[v.buildings[i]]
    if b and b.state ~= "gone" and b.state ~= "ruin" then
      if b.state == "site" then b.state = "gone" else b.state = "ruin"; b.ruined = W.hour end
      b.household = 0
    end
  end
  for i = #v.boats, 1, -1 do
    local bo = W.ent.boats[v.boats[i]]
    if bo then W.ent.boats[bo.id] = nil end
    v.boats[i] = nil
  end
  local FS = W.S.fauna
  local G = W.vgeo[v.id]
  if FS and G then
    for i = 1, #G.ring do FS.hunt[G.ring[i]], FS.cull[G.ring[i]] = 0, 0 end
  end
  if W.record_events then W.S.society.abandoned = W.S.society.abandoned + 1 end
  emit(W, "village_abandoned", v.cell, v.cause, 1, { village = v.id })
  require("sim.society.init").unregister_village(W, v)
end

-- Where would a household from v go? Returns the best destination village and its pull.
local function best_destination(W, v, h)
  local E = W.ent
  local P = E.people
  local vs = E.villages
  local best, bp = nil, -1e9
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u ~= v and u.continent == v.continent then
      local d = sites.dist(W, v.cell, u.cell)
      if d < 30 then
        local pull = min(12, u.food_security) / 12
        if require("sim.society.life").vacant(W, u) or u.stock.wood > 80 then pull = pull + 0.5 end
        -- kin living there
        for j = 1, #h.members do
          local p = P[h.members[j]]
          for _, rid in ipairs({ p.mother, p.father }) do
            local q = rid ~= 0 and P[rid]
            if q and not q.dead and q.village == u.id then pull = pull + 0.3; break end
          end
        end
        pull = pull - d * cfgmod.SOCIETY.walk_h * 1.5 / 10 * 0.05
        if u.famine then pull = pull - 1 end
        if pull > bp then best, bp = u, pull end
      end
    end
  end
  return best, bp
end

-- Monthly migration decisions for village v (households, push vs pull).
function M.monthly(W, v)
  local cfg = cfgmod.SOCIETY
  local E = W.ent
  local P, H = E.people, E.households
  local hour = W.hour
  -- hunger months (rolling year): a month whose mean ration was < 0.8
  local mr = v.month_ration_n and v.month_ration_n > 0 and v.month_ration_sum / v.month_ration_n or 1
  v.month_ration_sum, v.month_ration_n = 0, 0
  local hm = v.hunger_months or {}
  v.hunger_months = hm
  hm[#hm + 1] = mr < 0.8 and 1 or 0
  if #hm > 12 then table.remove(hm, 1) end
  local hungry = 0
  for i = 1, #hm do hungry = hungry + hm[i] end
  local cm = v.cold_months or 0
  local month = floor(hour / 672)
  local u0 = rng.float(W.seed, rng.S.MIGRATION, v.key_cell, month, v.key_hour % 65536)
  local home_pull = min(12, v.food_security) / 12 + 0.3
  local leavers = {}
  local reason = v.cause
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h and #h.members > 0 then
      local push = min(6, hungry) * cfg.push_hunger + min(3, cm) * cfg.push_cold
      if h.home == 0 then
        h.homeless_since = h.homeless_since or hour
        if hour - h.homeless_since > 3 * 672 then push = push + cfg.push_homeless end
      else
        h.homeless_since = nil
      end
      for j = 1, #h.members do
        local p = P[h.members[j]]
        if p.betrothed ~= 0 and p.wait_since and hour - p.wait_since > cfg.wait_migrate_months * 672 then
          push = push + cfg.push_wait
          break
        end
      end
      if v.pop < 6 then push = push + cfg.push_small end
      if push >= 1 then
        local dest, pull = best_destination(W, v, h)
        local u = (u0 + (h.key % 1000) / 1000 * 1.618) % 1
        if dest and pull > home_pull - push + cfg.pull_margin and u < cfg.migrate_prob then
          leavers[#leavers + 1] = { h = h, dest = dest }
        elseif not dest and push >= 1.5 and u < cfg.migrate_prob then
          leavers[#leavers + 1] = { h = h, dest = nil }
        end
      end
    end
  end
  -- go: households with a destination walk there; those without gather to found a village
  local founders, found_food = {}, 0
  for i = 1, #leavers do
    local l = leavers[i]
    local members = {}
    for j = 1, #l.h.members do members[#members + 1] = P[l.h.members[j]] end
    local days = l.dest and cfg.party_food_days or cfg.found_food_days
    local share = #members * days * 0.9
    local food = min(share, v.stock.grain * #members / max(1, v.pop))
    if l.dest then
      v.stock.grain = v.stock.grain - food
      local mid = emit(W, "migration", v.cell, reason, #members, { from = v.id, to = l.dest.id })
      M.depart(W, v, members, l.dest.id, nil, "migrants", mid ~= 0 and mid or reason, food, 0)
      v.labour_candidate = mid ~= 0 and mid or v.labour_candidate
    else
      for j = 1, #members do founders[#founders + 1] = members[j] end
      found_food = found_food + food
    end
  end
  if #founders >= 3 then
    local site = sites.best_site(W, v.continent, false)
    if site then
      v.stock.grain = v.stock.grain - found_food
      local seed = min(v.stock.seed, cfg.seed * cfg.found_seed_cells)
      v.stock.seed = v.stock.seed - seed
      local mid = emit(W, "migration", v.cell, reason, #founders, { from = v.id, to = 0 })
      M.depart(W, v, founders, 0, site, "migrants", mid ~= 0 and mid or reason, found_food, seed)
      v.labour_candidate = mid ~= 0 and mid or v.labour_candidate
    end
  end
  -- daughter villages: a crowded, fed village whose young couples wait for homes sends them to
  -- found a village on free land, in the local early spring (so they can sow on arrival)
  local lwk = farming.local_week(W, v)
  local living = 0
  for i = 1, #E.villages do if E.villages[i].alive then living = living + 1 end end
  if lwk < 6 and v.pop >= 30 and v.food_days > 90 and not v.famine and living < cfg.max_villages
    and hour - (v.bud_last or v.founded) > 6 * 8064 then
    local waiting = {}
    for i = 1, #v.members do
      local p = P[v.members[i]]
      if p.sex == "f" and p.betrothed ~= 0 and p.wait_since and hour - p.wait_since > 12 * 672 then
        local q = P[p.betrothed]
        if q and not q.dead and q.village == v.id then waiting[#waiting + 1] = p; waiting[#waiting + 1] = q end
      end
    end
    if #waiting >= 4 and rng.float(W.seed, rng.S.MIGRATION, v.key_cell, month, 5) < 0.35 then
      local site = sites.best_site(W, v.continent, false)
      if site then
        v.bud_last = hour
        local group = {}
        for i = 1, min(#waiting, 8) do group[#group + 1] = waiting[i] end
        local food = min(v.stock.grain * 0.4, #group * cfg.found_food_days)
        local seed = min(v.stock.seed * 0.5, cfg.seed * cfg.found_seed_cells)
        v.stock.grain = v.stock.grain - food
        v.stock.seed = v.stock.seed - seed
        -- they marry as they leave: a new village starts with households
        for i = 1, #group, 2 do
          local a, b = group[i], group[i + 1]
          if a and b then a.partner, b.partner = b.id, a.id; a.betrothed, b.betrothed = 0, 0 end
        end
        local mid = emit(W, "migration", v.cell, 0, #group, { from = v.id, to = 0, colonists = true })
        M.depart(W, v, group, 0, site, "colonists", mid, food, seed)
      end
    end
  end
  -- only children left: the nearest village takes them in
  local adults = 0
  for i = 1, #v.members do if ents.age(W, P[v.members[i]]) >= cfg.adult_age then adults = adults + 1 end end
  if v.pop > 0 and adults == 0 then
    local dest, bd = nil, 1e9
    for i = 1, #E.villages do
      local u = E.villages[i]
      if u.alive and u ~= v then
        local d = sites.dist(W, v.cell, u.cell)
        if d < bd then dest, bd = u, d end
      end
    end
    if dest then
      local kids = {}
      for i = 1, #v.members do kids[#kids + 1] = P[v.members[i]] end
      local mid = emit(W, "migration", v.cell, v.cause, #kids, { from = v.id, to = dest.id, orphans = true })
      M.depart(W, v, kids, dest.id, nil, "orphans", mid ~= 0 and mid or v.cause, v.stock.grain * 0.5, 0)
    end
  end
end

return M
