-- Derived geography, rebuilt from elevation + sea level (on load, and whenever the coastline
-- changes): ocean mask, land components (continents/islands), coast distance, drainage.
-- Lives in W.geo (not saved; rebuilt by world.rebuild_derived).

local geo = {}

function geo.rebuild(W)
  local n, g = W.n, W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local elev = W.static.elev
  local sea = W.G.sea_built or W.G.sea_level   -- see world.sea_built (derived data is pinned)
  local G = W.geo or {}
  W.geo = G

  local ocean = G.ocean or {}
  local land_cells, ocean_cells = {}, {}
  for c = 1, n do
    if elev[c] < sea then
      ocean[c] = 1
      ocean_cells[#ocean_cells + 1] = c
    else
      ocean[c] = 0
      land_cells[#land_cells + 1] = c
    end
  end
  G.ocean, G.land_cells, G.ocean_cells = ocean, land_cells, ocean_cells
  G.n_land, G.n_ocean = #land_cells, #ocean_cells

  -- land components, ids ordered by size (1 = largest); stable tie-break by first cell
  local comp = {}
  for c = 1, n do comp[c] = 0 end
  local comps = {}
  for i = 1, #land_cells do
    local s = land_cells[i]
    if comp[s] == 0 then
      local id = #comps + 1
      local list = { s }
      comp[s] = id
      local head = 1
      while head <= #list do
        local c = list[head]; head = head + 1
        local b = c * 6 - 6
        for k = 1, cnt[c] do
          local j = nbr[b + k]
          if ocean[j] == 0 and comp[j] == 0 then comp[j] = id; list[#list + 1] = j end
        end
      end
      comps[id] = { id = id, cells = list, size = #list, first = s }
    end
  end
  table.sort(comps, function(a, b)
    if a.size ~= b.size then return a.size > b.size end
    return a.first < b.first
  end)
  local continent = G.continent or {}
  for c = 1, n do continent[c] = 0 end
  for i = 1, #comps do
    comps[i].id = i
    local cells = comps[i].cells
    for k = 1, #cells do continent[cells[k]] = i end
  end
  G.continent, G.components = continent, comps

  -- coast flags and coast distance (multi-source BFS on each side)
  local coast = G.coast or {}
  local cdist = G.coast_dist or {}
  local queue = {}
  for c = 1, n do
    coast[c] = 0
    cdist[c] = 1e9
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      if ocean[nbr[b + k]] ~= ocean[c] then coast[c] = 1; break end
    end
    if coast[c] == 1 then cdist[c] = 1; queue[#queue + 1] = c end
  end
  local head = 1
  while head <= #queue do
    local c = queue[head]; head = head + 1
    local b = c * 6 - 6
    local d = cdist[c] + 1
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      if ocean[j] == ocean[c] and cdist[j] > d then cdist[j] = d; queue[#queue + 1] = j end
    end
  end
  for c = 1, n do if cdist[c] == 1e9 then cdist[c] = 99 end end
  G.coast, G.coast_dist = coast, cdist

  if W.static.basins then
    geo.basins(W, G, elev, sea, ocean, nbr, cnt)
  else
  -- drainage: land cell drains to its lowest neighbour if lower (ocean neighbours count as
  -- sea level). 0 = basin (lake) or ocean.
  local downhill = G.downhill or {}
  for c = 1, n do
    downhill[c] = 0
    if ocean[c] == 0 then
      local best, be = 0, elev[c]
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        local e = ocean[j] == 1 and (sea - 1) or elev[j]
        if e < be or (e == be and best ~= 0 and j < best) then best, be = j, e end
      end
      downhill[c] = best
    end
  end
  G.downhill = downhill
  -- drainage order: land cells by elevation descending (ties by index) — upstream first
  local order = {}
  for i = 1, #land_cells do order[i] = land_cells[i] end
  table.sort(order, function(a, b)
    if elev[a] ~= elev[b] then return elev[a] > elev[b] end
    return a < b
  end)
  G.drain_order = order
  end

  -- slope (max elevation drop to a neighbour, km per step), used by erosion/fire/visuals
  local slope = G.slope or {}
  for c = 1, n do
    local s = 0
    if ocean[c] == 0 then
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        local e = ocean[j] == 1 and sea or elev[j]
        local d = elev[c] - e
        if d > s then s = d end
      end
    end
    slope[c] = s
  end
  G.slope = slope
  G.sea_level_built = sea
  return G
end

-- Drainage basins (S14; v3 worlds, W.static.basins). Steepest descent on the coarse grid sent
-- every coast cell's water straight to the sea: short parallel streams that never met. Here:
-- 1. outlets: coastal land cells lowest within OUTLET_D cells of land (the mouths of basins);
-- 2. a priority flood from the outlets inland, lowest ground first (the water's way up a basin
--    over its divides at their lowest passes): each land cell drains to the cell the flood reached
--    it from, so tributaries join and trunk rivers grow toward a few mouths;
-- 3. the river bed (G.bed, km): the ground, carved down so it falls all the way to the mouth
--    (a gorge where a river cuts through a ridge to the basin above it). Drain order is
--    topological (upstream first: the flood's order reversed).
geo.OUTLET_D = 3
geo.BED_FALL = 0.004          -- the least fall of a bed from one cell to the next (km)

local function hpush(H, pr, c)
  local n = H.n + 1
  H.n = n
  local P, C = H.p, H.c
  local i = n
  while i > 1 do
    local q = (i - i % 2) / 2
    if P[q] < pr or (P[q] == pr and C[q] < c) then break end
    P[i], C[i] = P[q], C[q]
    i = q
  end
  P[i], C[i] = pr, c
end
local function hpop(H)
  local P, C = H.p, H.c
  local pr, c = P[1], C[1]
  local n = H.n
  local lp, lc = P[n], C[n]
  P[n], C[n] = nil, nil
  n = n - 1
  H.n = n
  if n > 0 then
    local i = 1
    while true do
      local l = i + i
      if l > n then break end
      local r = l + 1
      if r <= n and (P[r] < P[l] or (P[r] == P[l] and C[r] < C[l])) then l = r end
      if P[l] > lp or (P[l] == lp and C[l] > lc) then break end
      P[i], C[i] = P[l], C[l]
      i = l
    end
    P[i], C[i] = lp, lc
  end
  return pr, c
end

function geo.basins(W, G, elev, sea, ocean, nbr, cnt)
  local n = W.n
  local coast, comp = G.coast, G.continent
  local downhill = G.downhill or {}
  for c = 1, n do downhill[c] = 0 end
  -- 1. outlets
  local outlet = {}
  local seen, ring, nxt = {}, {}, {}
  local stamp = 0
  for c = 1, n do seen[c] = 0 end
  for c = 1, n do
    if ocean[c] == 0 and coast[c] == 1 then
      local low = true
      stamp = stamp + 1
      seen[c] = stamp
      local nr = 1
      ring[1] = c
      for _ = 1, geo.OUTLET_D do
        local nn = 0
        for i = 1, nr do
          local a = ring[i]
          local b = a * 6 - 6
          for k = 1, cnt[a] do
            local j = nbr[b + k]
            if ocean[j] == 0 and seen[j] ~= stamp then
              seen[j] = stamp
              nn = nn + 1
              nxt[nn] = j
              if coast[j] == 1 and comp[j] == comp[c] and (elev[j] < elev[c] or (elev[j] == elev[c] and j < c)) then low = false end
            end
          end
        end
        for i = 1, nn do ring[i] = nxt[i] end
        nr = nn
        if not low then break end
      end
      if low then outlet[c] = true end
    end
  end
  -- 2. the flood
  local H = { n = 0, p = {}, c = {} }
  local level, done, order = {}, {}, {}
  for c = 1, n do
    if outlet[c] then
      -- (into its lowest sea neighbour)
      local best, be = 0, 1e9
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        if ocean[j] == 1 and (elev[j] < be or (elev[j] == be and j < best)) then best, be = j, elev[j] end
      end
      downhill[c] = best
      level[c] = elev[c]
      done[c] = true
      hpush(H, elev[c], c)
    end
  end
  while H.n > 0 do
    local lv, c = hpop(H)
    order[#order + 1] = c
    local b = c * 6 - 6
    for k = 1, cnt[c] do
      local j = nbr[b + k]
      if ocean[j] == 0 and not done[j] then
        done[j] = true
        downhill[j] = c
        local l = elev[j] > lv and elev[j] or lv
        level[j] = l
        hpush(H, l, j)
      end
    end
  end
  -- (a land cell no flood reached - impossible while every landmass has a lowest coast cell -
  -- falls back to its lowest neighbour)
  local land = G.land_cells
  for i = 1, #land do
    local c = land[i]
    if not done[c] then
      local best, be = 0, elev[c]
      local b = c * 6 - 6
      for k = 1, cnt[c] do
        local j = nbr[b + k]
        local e = ocean[j] == 1 and (sea - 1) or elev[j]
        if e < be or (e == be and best ~= 0 and j < best) then best, be = j, e end
      end
      downhill[c] = best
      order[#order + 1] = c
    end
  end
  G.downhill = downhill
  -- 3. upstream first; beds carved to fall all the way to the mouth
  local dorder = {}
  for i = #order, 1, -1 do dorder[#dorder + 1] = order[i] end
  G.drain_order = dorder
  local bed = G.bed or {}
  for c = 1, n do bed[c] = ocean[c] == 1 and elev[c] or elev[c] - sea end
  local fall = geo.BED_FALL
  for i = 1, #dorder do
    local c = dorder[i]
    local d = downhill[c]
    if d ~= 0 and ocean[d] == 0 and bed[d] > bed[c] - fall then bed[d] = bed[c] - fall end
  end
  G.bed = bed
  G.outlets = outlet
end

return geo
