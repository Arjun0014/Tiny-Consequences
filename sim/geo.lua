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

return geo
