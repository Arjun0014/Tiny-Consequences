-- Where people can live and work: grid neighbourhoods, site scoring and village geometry
-- (docs/world/12_PEOPLE_VILLAGES.md §3.4, §4). Pure helpers over the world state; moved here
-- from the old villages system (D-036 growing-season scoring, D-038 capacity-gated sites).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")

local M = {}
local floor, min, max, sqrt, acos = math.floor, math.min, math.max, math.sqrt, dmath.acos

-- level-4 steps -> steps on this grid (>= 1)
function M.radius(W, steps)
  local r = floor(steps / W.sscale + 0.5)
  if r < 1 then r = 1 end
  return r
end

-- Cells within r steps of c (BFS), ordered by distance then index. mask(j) optional: only
-- cells passing it are expanded (and listed).
function M.rings(W, c, r, mask)
  local g = W.grid
  local dist = { [c] = 0 }
  local list = { c }
  local head = 1
  while head <= #list do
    local x = list[head]; head = head + 1
    if dist[x] < r then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if dist[j] == nil and (not mask or mask(j)) then dist[j] = dist[x] + 1; list[#list + 1] = j end
      end
    end
  end
  table.sort(list, function(a, b2)
    if dist[a] ~= dist[b2] then return dist[a] < dist[b2] end
    return a < b2
  end)
  return list, dist
end

-- Distance from cell a to cell b through a per-origin cache table (cache[b] = spacings).
function M.dist_cached(W, cache, a, b)
  local d = cache[b]
  if not d then d = M.dist(W, a, b); cache[b] = d end
  return d
end

-- Great-circle distance between two cells in cell spacings (walking/sailing time scale).
function M.dist(W, a, b)
  local g = W.grid
  local d = g.X[a] * g.X[b] + g.Y[a] * g.Y[b] + g.Z[a] * g.Z[b]
  if d > 1 then d = 1 elseif d < -1 then d = -1 end
  return acos(d) / g.spacing
end

-- How warm a cell's growing season is for crops, 0..1: the crop temperature bell at summer warmth.
function M.growing_warmth(W, c)
  local cfg = cfgmod.SOCIETY
  local T = W.temp_static
  local lo, hi = cfg.crop_t[1], cfg.crop_t[2]
  local ts = T.base[c] + T.lapse[c] + W.G.g_temp + T.amp[c] * 0.7
  local x = (ts - (lo + hi) * 0.5) / ((hi - lo) * 0.5)
  local tf = 1 - x * x
  if tf < 0 then tf = 0 end
  return min(1, tf * 1.6)
end

-- How good a cell is to live on. -1 = unusable. Soil, water, open land, a coast or a river,
-- scaled by how well crops grow there in summer.
function M.site_score(W, c)
  local F = W.F
  local geo = W.geo
  if geo.ocean[c] == 1 or F.land_use[c] ~= 0 or F.ice[c] > 0.3 then return -1 end
  local T = W.temp_static
  local tm = T.base[c] + T.lapse[c] + W.G.g_temp
  if tm < 2 or tm > 27 then return -1 end
  local s = F.fertility[c] + 0.5 * min(F.soil_water[c], 0.8) + 0.4 * F.grass[c] - 0.1 * F.forest[c]
  if F.soil_water[c] < 0.12 then s = s - 0.5 end
  if geo.coast[c] == 1 then s = s + 0.25 end
  if (W.S.hydro and W.S.hydro.flow_mean[c] or 0) > cfgmod.HYDRO.river_threshold then s = s + 0.25 end
  if W.S.ruins and W.S.ruins[c] then s = s - 0.3 end
  -- timber within reach matters for houses and fuel
  local g = W.grid
  local b = c * 6 - 6
  local wood = F.forest[c]
  for k = 1, g.nbr_count[c] do wood = wood + F.forest[g.nbr[b + k]] end
  s = s + 0.15 * min(1, wood / 2)
  if s > 0 then s = s * (0.4 + 0.6 * M.growing_warmth(W, c)) end
  return s
end

-- Far enough from every living village (and reserved sites)?
function M.free_site(W, c)
  local g = W.grid
  local vs = W.ent.villages
  local lim = dmath.cos(cfgmod.SOCIETY.min_spacing * 0.07553)
  for i = 1, #vs do
    local v = vs[i]
    if v.alive then
      local e = v.cell
      if g.X[c] * g.X[e] + g.Y[c] * g.Y[e] + g.Z[c] * g.Z[e] > lim then return false end
    end
  end
  return true
end

-- Ocean cells a village at `cell` can fish: within fish_radius steps through the sea from the
-- ocean cells next to it (nearest first), and the mooring (the nearest ocean neighbour).
function M.fishing_cells(W, cell)
  local g = W.grid
  local ocean = W.geo.ocean
  local b = cell * 6 - 6
  local mooring = nil
  for k = 1, g.nbr_count[cell] do
    local j = g.nbr[b + k]
    if ocean[j] == 1 and (not mooring or j < mooring) then mooring = j end
  end
  if not mooring then return {}, nil end
  local r = cfgmod.SOCIETY.fish_radius
  local list = M.rings(W, mooring, r, function(j) return ocean[j] == 1 end)
  return list, mooring
end

-- Rough people-fed estimate for a would-be village at cell c (settle shares people by it; new
-- sites must reach min_site_people).
function M.site_capacity(W, c)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local food = 0
  local ring = M.rings(W, c, M.radius(W, cfg.farm_radius))
  for i = 1, #ring do
    local x = ring[i]
    if x ~= c and W.geo.ocean[x] == 0 and F.land_use[x] == 0 and F.ice[x] < 0.5 then
      local tf = M.growing_warmth(W, x)
      food = food + cfg.yield * 0.45 * tf * (0.3 + 0.7 * F.fertility[x]) * min(1, F.soil_water[x] / 0.3)
    end
  end
  local fish = M.fishing_cells(W, c)
  for i = 1, #fish do
    food = food + F.fish[fish[i]] * cfg.fish_fu * 0.06
  end
  return food / (336 * 0.85 * 1.3)
end

-- Best free site among land cells passing filter(c): the highest site score whose land and sea
-- could feed at least `min_people` (checked lazily, best first). Falls back to the best-scoring
-- site when none can (worldgen must place its villages).
function M.pick_site(W, filter, min_people, fallback, adjust)
  local land = W.geo.land_cells
  local cand, score = {}, {}
  for i = 1, #land do
    local c = land[i]
    if filter(c) then
      local s = M.site_score(W, c)
      if s > -1 and M.free_site(W, c) then
        cand[#cand + 1] = c
        score[c] = adjust and adjust(c, s) or s
      end
    end
  end
  table.sort(cand, function(a, b)
    if score[a] ~= score[b] then return score[a] > score[b] end
    return a < b
  end)
  for i = 1, #cand do
    if M.site_capacity(W, cand[i]) >= min_people then return cand[i] end
  end
  if fallback then return cand[1] end
  return nil
end

-- The best free site within `radius` cell spacings of `cell` on its landmass (daughter villages
-- and refugees settle within a few days' walk): site score minus a walking penalty, checked best
-- first against the land and sea it could feed. nil if none.
function M.near_site(W, cell, radius)
  local g = W.grid
  local cont = W.geo.continent[cell]
  local lim = dmath.cos(radius * g.spacing)
  local X, Y, Z = g.X, g.Y, g.Z
  local cx, cy, cz = X[cell], Y[cell], Z[cell]
  local cfg = cfgmod.SOCIETY
  return M.pick_site(W, function(c)
    return W.geo.continent[c] == cont and X[c] * cx + Y[c] * cy + Z[c] * cz >= lim
  end, cfg.min_site_people, false, function(c, s) return s - cfg.site_walk_penalty * M.dist(W, cell, c) end)
end

-- Best free site reachable from a village on landmass `continent` (by boat: any coastal site).
function M.best_site(W, continent, coastal)
  local geo = W.geo
  return M.pick_site(W, function(c)
    return geo.continent[c] == continent or (coastal and geo.coast[c] == 1)
  end, cfgmod.SOCIETY.min_site_people)
end

return M
