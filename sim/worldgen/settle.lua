-- Worldgen settle (02_WORLD_MODEL.md §2 "World generation requirements"). Runs inside the spin-up
-- once climate and vegetation have settled (SETTLE.year), so Year 0 starts lived-in:
--   * a culture (phoneme set) per landmass
--   * 3–5 villages at good sites, 150–250 people: ≥ 1 fishing (coastal) village, ≥ 1 inland
--     farming village, villages on ≥ 2 continents; families with ages, partners and children
--   * 1–2 tanker lanes: closed loops through ocean cells passing next to a coastal village,
--     1–2 tankers per lane
--   * volcano sites: 5–10 on convergent boundaries + 1–3 hotspots (≥ 1 in the ocean)
-- All choices are keyed by the seed (rng streams SETTLE / NAMES); the result is reported in
-- W.worldgen_report.settle and checked by P10.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local villages = require("sim.systems.villages")
local people = require("sim.systems.people")

local settle = {}
local floor = math.floor

local function ufactory(W, stream, key)
  local ctr = 0
  return function()
    ctr = ctr + 1
    return rng.float(W.seed, stream, key, ctr, 0)
  end
end

local function pick_range(u, r) return r[1] + floor(u() * (r[2] - r[1] + 1)) end

-- Continents = landmasses at least ~continent size.
local function is_continent(W, comp)
  return comp.size / W.n >= cfgmod.WORLDGEN.continent_min_frac * 0.9
end

local function cultures(W, u)
  local comps = W.geo.components
  local nc = #names.CULTURES
  local perm = {}
  for i = 1, nc do perm[i] = i end
  for i = nc, 2, -1 do
    local j = 1 + floor(u() * i)
    perm[i], perm[j] = perm[j], perm[i]
  end
  local out = {}
  local k = 0
  for i = 1, #comps do
    if is_continent(W, comps[i]) then k = k + 1; out[i] = perm[((k - 1) % nc) + 1] end
  end
  for i = 1, #comps do
    if not out[i] then out[i] = perm[1 + floor(u() * nc)] end
  end
  W.names.cultures = out
  return out
end

-- Best free site by score among land cells passing filter(c).
local function best_site(W, filter)
  local best, bs = nil, -math.huge
  local land = W.geo.land_cells
  for i = 1, #land do
    local c = land[i]
    if filter(c) then
      local s = villages.site_score(W, c)
      if s > -1 and s > bs and villages.free_site(W, c) then best, bs = c, s end
    end
  end
  return best
end

local function has_fish(W, c)
  local g = W.grid
  local b = c * 6 - 6
  for k = 1, g.nbr_count[c] do
    local j = g.nbr[b + k]
    if W.geo.ocean[j] == 1 and W.F.fish[j] > 0.2 and W.F.ice[j] < 0.3 then return true end
  end
  return false
end

-- Households until the village holds `target` people.
local function populate(W, v, target, u)
  local tick = W.tick
  local P = W.ent.people
  local idx = 0
  local function new(sex, age, mother, father)
    idx = idx + 1
    local born = tick - age * 48 - floor(u() * 48)
    return people.create(W, v, { sex = sex, born = born, mother = mother, father = father, idx = idx })
  end
  while v.pop < target do
    local r = u()
    if r < 0.62 then
      -- a couple with children
      local fa = 20 + floor(u() * 24)
      local ma = fa + floor(u() * 9) - 3
      local f = new("f", fa)
      local m = new("m", ma)
      f.partner, m.partner = m.id, f.id
      local kids = floor(u() * 4.5)
      for _ = 1, kids do
        local ka = floor(u() * math.min(14, fa - 17))
        if ka >= 0 then new(nil, ka, f.id, m.id) end
      end
    elseif r < 0.77 then
      local a = 55 + floor(u() * 18)
      local f = new("f", a)
      local m = new("m", a + floor(u() * 5) - 2)
      f.partner, m.partner = m.id, f.id
    elseif r < 0.9 then
      new(nil, 16 + floor(u() * 30))
    else
      local f = new("f", 18 + floor(u() * 6))
      local m = new("m", 19 + floor(u() * 6))
      f.partner, m.partner = m.id, f.id
    end
  end
  -- jobs with the village's needs known
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.job ~= "child" and p.job ~= "elder" then p.job = "child" end
  end
  v.jobs = {}
  for i = 1, #v.members do people.set_job(W, v, P[v.members[i]]) end
end

-- BFS through ocean cells from a; returns dist table and parent table.
local function ocean_bfs(W, a)
  local g = W.grid
  local ocean = W.geo.ocean
  local dist, par = { [a] = 0 }, {}
  local q = { a }
  local head = 1
  while head <= #q do
    local x = q[head]; head = head + 1
    local b = x * 6 - 6
    for k = 1, g.nbr_count[x] do
      local j = g.nbr[b + k]
      if ocean[j] == 1 and dist[j] == nil then dist[j] = dist[x] + 1; par[j] = x; q[#q + 1] = j end
    end
  end
  return dist, par, q
end

local function path(W, a, b)
  local _, par = ocean_bfs(W, a)
  local out = {}
  local x = b
  while x and x ~= a do out[#out + 1] = x; x = par[x] end
  if x ~= a then return nil end
  out[#out + 1] = a
  local r = {}
  for i = #out, 1, -1 do r[#r + 1] = out[i] end
  return r
end

-- A closed loop: A (next to the village) -> B (far) -> C (far from both) -> A.
local function make_lane(W, start, u)
  local dA, _, reach = ocean_bfs(W, start)
  local B, bd = nil, -1
  for i = 1, #reach do
    local c = reach[i]
    local d = dA[c] - (u() < 0.5 and 0 or 1) * 0.5
    if d > bd then B, bd = c, d end
  end
  if not B or dA[B] < 3 then return nil end
  local dB = ocean_bfs(W, B)
  local Cc, cd = nil, -1
  for i = 1, #reach do
    local c = reach[i]
    local d = math.min(dA[c], dB[c] or 0)
    if d > cd then Cc, cd = c, d end
  end
  local loop = {}
  local legs = { path(W, start, B), path(W, B, Cc), path(W, Cc, start) }
  for l = 1, 3 do
    local leg = legs[l]
    if not leg then return nil end
    for i = 1, #leg - 1 do loop[#loop + 1] = leg[i] end
  end
  return loop
end

local function volcanoes(W, u)
  local cfg = cfgmod.SETTLE
  local g = W.grid
  local st = W.static
  local n = W.n
  local sp = math.cos(cfg.volcano_spacing * 0.07553)
  local chosen = {}
  local function far(c)
    for i = 1, #chosen do
      local e = chosen[i].cell
      if g.X[c] * g.X[e] + g.Y[c] * g.Y[e] + g.Z[c] * g.Z[e] > sp then return false end
    end
    return true
  end
  local cand = {}
  for c = 1, n do if st.boundary[c] == 1 then cand[#cand + 1] = c end end
  table.sort(cand, function(a, b)
    if st.bstrength[a] ~= st.bstrength[b] then return st.bstrength[a] > st.bstrength[b] end
    return a < b
  end)
  local want = pick_range(u, cfg.arcs)
  for pass = 1, 2 do
    for i = 1, #cand do
      if #chosen >= want then break end
      local c = cand[i]
      if (pass == 2 or far(c)) then
        local dup = false
        for k = 1, #chosen do if chosen[k].cell == c then dup = true end end
        if not dup then chosen[#chosen + 1] = { cell = c, kind = "arc" } end
      end
    end
  end
  local nh = pick_range(u, cfg.hotspots)
  local ocells = W.geo.ocean_cells
  local tries = 0
  while nh > 0 and tries < 200 do
    tries = tries + 1
    local c
    local want_ocean = true
    for k = 1, #chosen do if chosen[k].kind == "hotspot" and W.geo.ocean[chosen[k].cell] == 1 then want_ocean = false end end
    if want_ocean then c = ocells[1 + floor(u() * #ocells)] else c = 1 + floor(u() * n) end
    if far(c) and st.boundary[c] == 0 then
      chosen[#chosen + 1] = { cell = c, kind = "hotspot" }
      nh = nh - 1
    end
  end
  local used = {}
  local list = {}
  for i = 1, #chosen do
    local v = chosen[i]
    local name = names.unique(u, names.volcano, used)
    list[i] = { id = i, cell = v.cell, kind = v.kind, name = name, ocean = W.geo.ocean[v.cell] == 1,
      pressure = 0 }
  end
  W.ent.volcanoes = list
  return list
end

function settle.run(W)
  local cfg = cfgmod.SETTLE
  local vcfg = cfgmod.VILLAGE
  local u = ufactory(W, rng.S.SETTLE, 1)
  local un = ufactory(W, rng.S.NAMES, 9001)
  local geo = W.geo
  local cult = cultures(W, u)
  local nv = pick_range(u, cfg.villages)
  local total = pick_range(u, cfg.people)
  local sites = {}
  local function add(c)
    if c then
      sites[#sites + 1] = c
      -- reserve the spot (free_site looks at living villages; use a placeholder)
      W.ent.villages[#W.ent.villages + 1] = { cell = c, alive = true, placeholder = true }
    end
    return c
  end
  local coast = geo.coast
  local cdist = geo.coast_dist
  local function cont_ok(c) return is_continent(W, geo.components[geo.continent[c]]) end
  -- 1. the fishing village, 2. the inland farming village, 3. one on another continent
  add(best_site(W, function(c) return coast[c] == 1 and cont_ok(c) and has_fish(W, c) end)
    or best_site(W, function(c) return coast[c] == 1 and has_fish(W, c) end))
  add(best_site(W, function(c) return cdist[c] >= 2 and cont_ok(c) end)
    or best_site(W, function(c) return cdist[c] >= 2 end))
  local conts = {}
  for i = 1, #sites do conts[geo.continent[sites[i]]] = true end
  local nconts = 0
  for _ in pairs(conts) do nconts = nconts + 1 end
  if nconts < 2 then
    add(best_site(W, function(c) return cont_ok(c) and not conts[geo.continent[c]] end))
  end
  while #sites < nv do
    local c = best_site(W, function(c) return cont_ok(c) end) or best_site(W, function() return true end)
    if not c then break end
    add(c)
  end
  -- drop placeholders, found for real
  for i = #W.ent.villages, 1, -1 do
    if W.ent.villages[i].placeholder then W.ent.villages[i] = nil end
  end
  local used = {}
  local weights, wsum = {}, 0
  for i = 1, #sites do weights[i] = 0.6 + u(); wsum = wsum + weights[i] end
  local placed = 0
  for i = 1, #sites do
    local c = sites[i]
    local culture = cult[geo.continent[c]] or 1
    local name = names.unique(un, function(uu) return names.village(uu, culture) end, used)
    local v = villages.found(W, c, { name = name, culture = culture })
    -- desired fishers from the fish on the doorstep
    local stock = 0
    for k = 1, #v.fishing do stock = stock + W.F.fish[v.fishing[k]] end
    stock = stock * vcfg.fish_q * W.sscale * W.sscale
    local target = (i == #sites) and (total - placed) or floor(total * weights[i] / wsum + 0.5)
    if target < 12 then target = 12 end
    v.fish_jobs = floor(stock * 0.25 / vcfg.fisher_max)
    local fmax = floor(target * 0.7 * vcfg.fish_share)
    if v.fish_jobs > fmax then v.fish_jobs = fmax end
    local up = ufactory(W, rng.S.SETTLE, 100 + i)
    populate(W, v, target, up)
    placed = placed + v.pop
    v.capacity = villages.capacity(W, v, W.vgeo[v.id])
    local need = 0
    for k = 1, #v.members do
      need = need + (W.ent.people[v.members[k]].job == "child" and vcfg.eat_child or vcfg.eat_adult)
    end
    v.food = vcfg.start_food * need
  end
  -- lanes (next to coastal villages) and tankers
  local nl = pick_range(u, cfg.lanes)
  local lanes = {}
  local coastal = {}
  for i = 1, #W.ent.villages do
    local v = W.ent.villages[i]
    if #v.fishing > 0 then coastal[#coastal + 1] = v end
  end
  for li = 1, nl do
    local v = coastal[((li - 1) % math.max(1, #coastal)) + 1]
    if v then
      local start = v.fishing[1 + floor(u() * #v.fishing)]
      local loop = make_lane(W, start, u)
      if loop and #loop >= 6 then lanes[#lanes + 1] = { cells = loop, village = v.id, replace_at = -1 } end
    end
  end
  W.static.lanes = lanes
  local ships = require("sim.systems.ships")
  for li = 1, #lanes do
    local n = #lanes[li].cells >= 30 and 2 or 1
    for k = 1, n do ships.new_tanker(W, li, (k - 1) * #lanes[li].cells / n) end
  end
  local vols = volcanoes(W, u)
  -- report (P10)
  local rep = { villages = #W.ent.villages, people = 0, fishing = false, inland = false,
    continents = 0, lanes = #lanes, arcs = 0, hotspots = 0, ocean_hotspot = false, lanes_ok = true }
  local vc = {}
  for i = 1, #W.ent.villages do
    local v = W.ent.villages[i]
    rep.people = rep.people + v.pop
    if #v.fishing > 0 then rep.fishing = true end
    if cdist[v.cell] >= 2 then rep.inland = true end
    if cont_ok(v.cell) then vc[geo.continent[v.cell]] = true end
  end
  for _ in pairs(vc) do rep.continents = rep.continents + 1 end
  for i = 1, #vols do
    if vols[i].kind == "arc" then rep.arcs = rep.arcs + 1
    else
      rep.hotspots = rep.hotspots + 1
      if vols[i].ocean then rep.ocean_hotspot = true end
    end
  end
  for li = 1, #lanes do
    local cells = lanes[li].cells
    local g = W.grid
    for i = 1, #cells do
      local a, b = cells[i], cells[i % #cells + 1]
      if geo.ocean[a] ~= 1 then rep.lanes_ok = false end
      local adj = false
      local base = a * 6 - 6
      for k = 1, g.nbr_count[a] do if g.nbr[base + k] == b then adj = true end end
      if not adj and a ~= b then rep.lanes_ok = false end
    end
  end
  W.worldgen_report = W.worldgen_report or {}
  W.worldgen_report.settle = rep
  return rep
end

return settle
