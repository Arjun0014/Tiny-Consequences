-- Named regions (02_WORLD_MODEL.md §2 "Region names", D-011). Worldgen only (allocates freely).
--
-- Land: one region per landmass (continent or island); a continent holding more than
-- REGIONS.split_frac of all land is split in two (two far-apart seeds, graph-distance Voronoi
-- inside the landmass) and the halves are named "North X" / "South X" (or East/West).
-- Ocean: 6–10 seas from seeded centres (farthest-point sampling with jitter over ocean graph
-- distance), cells assigned by multi-source BFS through the ocean so every sea is connected.
--
-- Output (persistent): W.static.region[c] = region id (land ids first, then seas);
-- W.names.regions[id] = { name, kind, continent, cells, centre, part };
-- W.names.continents[comp] = landmass name.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local rng = require("sim.rng")
local cfgmod = require("sim.config")
local names = require("content.names")

local regions = {}
local floor = math.floor

-- Multi-source BFS inside `allow` (allow[c] == true). seeds: list of cells. Returns owner[c]
-- (index into seeds, 0 = unreached) and dist[c].
local function voronoi(g, n, seeds, allow)
  local nbr, cnt = g.nbr, g.nbr_count
  local owner, dist = {}, {}
  for c = 1, n do owner[c] = 0; dist[c] = -1 end
  local q = {}
  for i = 1, #seeds do
    local s = seeds[i]
    if owner[s] == 0 then owner[s] = i; dist[s] = 0; q[#q + 1] = s end
  end
  local head = 1
  while head <= #q do
    local c = q[head]; head = head + 1
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      if allow[j] and owner[j] == 0 then
        owner[j] = owner[c]
        dist[j] = dist[c] + 1
        q[#q + 1] = j
      end
    end
  end
  return owner, dist
end

-- Farthest cell (graph distance inside allow) from src; ties: lowest index.
local function farthest(g, n, src, allow)
  local _, dist = voronoi(g, n, { src }, allow)
  local best, bd = src, 0
  for c = 1, n do
    if dist[c] > bd then best, bd = c, dist[c] end
  end
  return best
end

local function strip_the(s)
  if s:sub(1, 4) == "The " then return s:sub(5), true end
  return s, false
end

-- "North X" / "Northern Y" for "The Y"
local function part_name(dir, base)
  local core, had_the = strip_the(base)
  if had_the then
    local adj = { North = "Northern", South = "Southern", East = "Eastern", West = "Western" }
    return adj[dir] .. " " .. core
  end
  return dir .. " " .. base
end

function regions.generate(W)
  local cfg = cfgmod.REGIONS
  local n, g = W.n, W.grid
  local geo = W.geo
  local ocean = geo.ocean
  local seed = W.seed
  local reg = W.static.region
  for c = 1, n do reg[c] = 0 end
  local recs = {}
  local used = {}
  local ctr = 0
  local function u()
    ctr = ctr + 1
    return rng.float(seed, rng.S.NAMES, 7001, ctr, 0)
  end
  local function new_region(kind, name, continent, cells, centre, part)
    local id = #recs + 1
    recs[id] = { name = name, kind = kind, continent = continent, cells = #cells, centre = centre, part = part }
    for i = 1, #cells do reg[cells[i]] = id end
    return id
  end

  -- land -----------------------------------------------------------------------------------
  local n_land = geo.n_land
  local continents = {}
  local wcfg = cfgmod.WORLDGEN
  for ci = 1, #geo.components do
    local comp = geo.components[ci]
    local frac = comp.size / n
    local kind = frac >= wcfg.continent_min_frac * 0.9 and "continent" or "island"
    local cname = names.unique(u, function(uu) return names.region(uu, kind) end, used)
    continents[ci] = cname
    local cells = {}
    for i = 1, #comp.cells do cells[i] = comp.cells[i] end
    table.sort(cells)
    if kind == "continent" and n_land > 0 and comp.size / n_land > cfg.split_frac and comp.size >= 8 then
      local allow = {}
      for i = 1, #cells do allow[cells[i]] = true end
      local a = farthest(g, n, cells[1], allow)
      local b = farthest(g, n, a, allow)
      local owner = voronoi(g, n, { a, b }, allow)
      local ca, cb = {}, {}
      for i = 1, #cells do
        local c = cells[i]
        if owner[c] == 2 then cb[#cb + 1] = c else ca[#ca + 1] = c end
      end
      -- direction words from the seeds' relative position
      local dlat = g.lat[a] - g.lat[b]
      local dlon = g.lon[a] - g.lon[b]
      if dlon > math.pi then dlon = dlon - 2 * math.pi elseif dlon < -math.pi then dlon = dlon + 2 * math.pi end
      local coslat = dmath.cos((g.lat[a] + g.lat[b]) * 0.5)
      local da, db
      if math.abs(dlat) >= math.abs(dlon * coslat) then
        if dlat > 0 then da, db = "North", "South" else da, db = "South", "North" end
      else
        if dlon > 0 then da, db = "East", "West" else da, db = "West", "East" end
      end
      new_region("continent", part_name(da, cname), ci, ca, a, da)
      new_region("continent", part_name(db, cname), ci, cb, b, db)
    else
      new_region(kind, cname, ci, cells, cells[1], nil)
    end
  end

  -- seas -----------------------------------------------------------------------------------
  local ocells = geo.ocean_cells
  if #ocells > 0 then
    local allow = {}
    for i = 1, #ocells do allow[ocells[i]] = true end
    local k = floor(#ocells / n * cfg.seas_per_ocean + 0.5)
    if k < cfg.seas_min then k = cfg.seas_min elseif k > cfg.seas_max then k = cfg.seas_max end
    if k > #ocells then k = #ocells end
    local centres = {}
    local first = ocells[1 + floor(rng.float(seed, rng.S.WORLDGEN, 7100, 0, 0) * #ocells)]
    centres[1] = first
    local mind = {}
    for c = 1, n do mind[c] = 1e9 end
    local _, d0 = voronoi(g, n, { first }, allow)
    for c = 1, n do if d0[c] >= 0 then mind[c] = d0[c] end end
    for ci = 2, k do
      local maxd = 0
      for i = 1, #ocells do
        local c = ocells[i]
        if mind[c] < 1e9 and mind[c] > maxd then maxd = mind[c] end
      end
      if maxd <= 0 then break end
      -- candidates: ocean cells at ≥ jitter × max distance; pick one by rng
      local cand = {}
      for i = 1, #ocells do
        local c = ocells[i]
        if mind[c] < 1e9 and mind[c] >= cfg.sea_jitter * maxd then cand[#cand + 1] = c end
      end
      local pick = cand[1 + floor(rng.float(seed, rng.S.WORLDGEN, 7100, ci, 0) * #cand)]
      centres[ci] = pick
      local _, dd = voronoi(g, n, { pick }, allow)
      for c = 1, n do if dd[c] >= 0 and dd[c] < mind[c] then mind[c] = dd[c] end end
    end
    local owner = voronoi(g, n, centres, allow)
    -- unreached ocean (enclosed seas): nearest centre by angle
    for i = 1, #ocells do
      local c = ocells[i]
      if owner[c] == 0 then
        local best, bd = 1, -2
        for j = 1, #centres do
          local e = centres[j]
          local d = g.X[c] * g.X[e] + g.Y[c] * g.Y[e] + g.Z[c] * g.Z[e]
          if d > bd then best, bd = j, d end
        end
        owner[c] = best
      end
    end
    local lists = {}
    for j = 1, #centres do lists[j] = {} end
    for i = 1, #ocells do
      local c = ocells[i]
      local l = lists[owner[c]]
      l[#l + 1] = c
    end
    local largest, ls = 1, -1
    for j = 1, #centres do if #lists[j] > ls then largest, ls = j, #lists[j] end end
    for j = 1, #centres do
      if #lists[j] > 0 then
        local kind = (j == largest) and "ocean" or "sea"
        local nm = names.unique(u, function(uu) return names.region(uu, kind) end, used)
        new_region(kind, nm, 0, lists[j], centres[j], nil)
      end
    end
  end

  W.names.regions = recs
  W.names.continents = continents
  return recs
end

return regions
