-- The settlement's local map (docs/v3/10_AGENTS.md §2): a square of N x N tiles on the tangent
-- plane at the settlement's sim cell. Local coordinates (x, y) are world units from the cell
-- centre along the cell's east (e) and north (q) axes; the sphere point is the gnomonic
-- projection normalize(R n + x e + y q) (R = 10, the view's planet radius).
--
-- Per tile (flat arrays, index i = (iy - 1) N + ix):
--   land[i]  1 land, 0 sea (the drawn coast, sim/surface.lua)   cell[i]  the sim cell it stands for
--   riv[i]   0 dry, -1 floodplain, > 0 river now (1 + wading depth x 100; <= 101 wadeable)
--   wcost[i] the boats' cost field (open water 1, else -1)
--   occ[i]   building id (footprint) or 0      plot[i]  plot index or 0
--   tree[i]  tree index standing on the tile or 0
--   wear[i]  traffic (decays daily)     lane[i]  0 open, 1 trail, 2 lane (from wear, updated daily)
--   cost[i]  A* cost of entering the tile (rebuilt when the map changes: daily and on building)
-- Trees: s.trees = { x, y, size, st (1 standing, 2 felled: logs lie there, 3 stump), logs, claim,
-- t_cut (hour), tile, n }.
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local sqrt, floor, max, min, abs = math.sqrt, math.floor, math.max, math.min, math.abs
M.R = 10

local function cfg() return cfgmod.V3 end

-- Tangent frame at a cell (as sim/society/layout.lua): normal n, east e, north q.
function M.frame(W, cell)
  local g = W.grid
  local nx, ny, nz = g.X[cell], g.Y[cell], g.Z[cell]
  local ex, ey, ez = nz, 0, -nx
  local l = sqrt(ex * ex + ez * ez)
  if l < 1e-6 then ex, ey, ez, l = 0, -nz, ny, sqrt(nz * nz + ny * ny) end
  ex, ey, ez = ex / l, ey / l, ez / l
  local qx, qy, qz = ny * ez - nz * ey, nz * ex - nx * ez, nx * ey - ny * ex
  return { nx, ny, nz, ex, ey, ez, qx, qy, qz }
end

-- Unit sphere point of local (x, y) on settlement s.
function M.unit(s, x, y)
  local f = s.frame
  local R = M.R
  local px = f[1] * R + f[4] * x + f[7] * y
  local py = f[2] * R + f[5] * x + f[8] * y
  local pz = f[3] * R + f[6] * x + f[9] * y
  local l = sqrt(px * px + py * py + pz * pz)
  return px / l, py / l, pz / l
end

-- Local (x, y) of a unit sphere point (inverse gnomonic; nil behind the plane).
function M.local_of(s, ux, uy, uz)
  local f = s.frame
  local d = ux * f[1] + uy * f[2] + uz * f[3]
  if d <= 0.05 then return nil end
  local k = M.R / d
  return (ux * f[4] + uy * f[5] + uz * f[6]) * k, (ux * f[7] + uy * f[8] + uz * f[9]) * k
end

function M.tile_of(s, x, y)
  local N, T = s.N, s.T
  local ix = floor(x / T + N / 2) + 1
  local iy = floor(y / T + N / 2) + 1
  if ix < 1 or iy < 1 or ix > N or iy > N then return 0 end
  return (iy - 1) * N + ix
end

function M.tile_ixy(s, i)
  local N = s.N
  local iy = floor((i - 1) / N) + 1
  return i - (iy - 1) * N, iy
end

-- Centre of tile i in local coordinates.
function M.tile_xy(s, i)
  local N, T = s.N, s.T
  local iy = floor((i - 1) / N) + 1
  local ix = i - (iy - 1) * N
  return (ix - 0.5 - N / 2) * T, (iy - 0.5 - N / 2) * T
end

function M.ixy_xy(s, ix, iy)
  local N, T = s.N, s.T
  return (ix - 0.5 - N / 2) * T, (iy - 0.5 - N / 2) * T
end

-- Build the tile layer for settlement s at cell s.cell. Land and sea are the drawn surface's
-- (sim/surface.lua, D-085): a tile is land when the coast field at its centre is, exactly as the
-- view draws it; the sim cell a tile stands for is the nearest one of its own kind (forest, fish
-- and soil come from it). Boats keep to tiles clear of the shore (boat_margin).
function M.build(W, s)
  local C = cfg()
  local N, T = C.map_n, C.tile
  s.N, s.T = N, T
  s.frame = M.frame(W, s.cell)
  local grid = require("sim.grid")
  local surface = require("sim.surface")
  local G = surface.get(W)
  local sg = W.grid
  local ocean = W.geo.ocean
  local NN = N * N
  local land, cell, occ, plot, tree, wear, lane, cost = {}, {}, {}, {}, {}, {}, {}, {}
  local wcost, riv = {}, {}
  local prev, hint = s.cell, nil
  local margin = C.boat_margin
  for i = 1, NN do
    local x, y = M.tile_xy(s, i)
    local ux, uy, uz = M.unit(s, x, y)
    local c = grid.nearest(sg, ux, uy, uz, prev)
    prev = c
    local sv, v = surface.sample(G, ux, uy, uz, hint)
    hint = v
    local l = sv > 0 and 1 or 0
    if (ocean[c] == 0) ~= (l == 1) then
      -- drawn land whose nearest cell is sea (or the reverse): the sim triangle's heaviest cell of
      -- the tile's own kind
      local c0, a, b, w0, w1, w2 = surface.locate(sg, c, ux, uy, uz)
      local want = l == 1 and 0 or 1
      local bc, bw = 0, -1
      if ocean[c0] == want and w0 > bw then bc, bw = c0, w0 end
      if ocean[a] == want and w1 > bw then bc, bw = a, w1 end
      if ocean[b] == want and w2 > bw then bc, bw = b, w2 end
      if bc > 0 then c = bc end
    end
    cell[i] = c
    land[i] = l
    -- the water's own cost field (boats, D-084): open water 1, the shore and land closed
    wcost[i] = sv < -margin and 1 or -1
    occ[i], plot[i], tree[i], wear[i], lane[i], cost[i], riv[i] = 0, 0, 0, 0, 0, C.cost_open, 0
  end
  s.land, s.cell_of, s.occ, s.plot, s.tree, s.wear, s.lane, s.cost = land, cell, occ, plot, tree, wear, lane, cost
  s.wcost, s.riv = wcost, riv
  s.trees = { x = {}, y = {}, size = {}, st = {}, logs = {}, claim = {}, t_cut = {}, tile = {}, n = 0 }
  s.ver = 1
  M.rivers(W, s, true)
end

-- Rivers on the local map (D-085) ------------------------------------------------------------
-- The planet's rivers cross the map as the view draws them (sim/surface.lua: the same courses,
-- widths and fords). s.riv per tile: 0 dry land, -1 floodplain (dry now: nothing is built or
-- sown there), else 1 + wading depth x 100 (wet now; 101 or less is wadeable: a ford, a stream).
-- Re-laid every day from the day's flow: a flood widens and deepens the river and closes its
-- fords; a drought opens more of them.

-- (runtime only, never saved: each settlement's river courses in local coordinates, derived from
-- the grid, the drainage and the surface)
local COURSES = setmetatable({}, { __mode = "k" })

local function courses(W, s)
  local cs = COURSES[s]
  if cs then return cs end
  cs = {}
  local surface = require("sim.surface")
  local G = surface.get(W)
  local g, geo = W.grid, W.geo
  local down = geo.downhill
  local f = s.frame
  local cr = 0.936             -- (cos 0.36 rad: the map's half-diagonal and a link's length)
  local Q = {}
  local lim = s.N * s.T * 0.5 + 0.2
  for c = 1, W.n do
    local d = down[c]
    if geo.ocean[c] == 0 and d and d ~= 0 and g.X[c] * f[1] + g.Y[c] * f[2] + g.Z[c] * f[3] > cr then
      surface.river_path(G, c, g.X[c], g.Y[c], g.Z[c], g.X[d], g.Y[d], g.Z[d], Q)
      local co = { c = c, d = d, x = {}, y = {}, sea = {} }
      local inside = false
      for k = 0, surface.RIVER_SUB do
        local q = Q[k]
        local x, y = M.local_of(s, q.x, q.y, q.z)
        co.x[k], co.y[k], co.sea[k] = x or 1e9, y or 1e9, q.sea
        if x and abs(x) < lim and abs(y) < lim then inside = true end
      end
      if inside then cs[#cs + 1] = co end
    end
  end
  COURSES[s] = cs
  return cs
end

-- Lay the rivers for today's flow (wd: surface.river_widths, shared by all settlements on a day).
-- found: the first laying also marks the floodplain (the widest a flood makes the river).
function M.rivers(W, s, found, wd)
  local surface = require("sim.surface")
  wd = wd or surface.river_widths(W)
  local cs = courses(W, s)
  local riv, land = s.riv, s.land
  local N, T = s.N, s.T
  for i = 1, N * N do
    if riv[i] > 0 then riv[i] = -1 end      -- (wet yesterday: floodplain unless wet again)
  end
  local SUB = surface.RIVER_SUB
  local half = N / 2
  for pass = found and 1 or 2, 2 do
    local flood = pass == 1
    for ci = 1, #cs do
      local co = cs[ci]
      local c, d = co.c, co.d
      if wd[c] > 0 then
        local w0, w1 = surface.link_widths(wd, c, d)
        if flood then w0, w1 = w0 * 2.2, w1 * 2.2 end
        local xs, ys, sea = co.x, co.y, co.sea
        for k = 0, SUB - 1 do
          if not sea[k + 1] then
            local ax, ay, bx, by = xs[k], ys[k], xs[k + 1], ys[k + 1]
            local hw0, d0 = surface.river_at(c, k, w0, w1)
            local hw1, d1 = surface.river_at(c, k + 1, w0, w1)
            local hm = hw0 > hw1 and hw0 or hw1
            local ix0 = floor((min(ax, bx) - hm) / T + half) + 1
            local ix1 = floor((max(ax, bx) + hm) / T + half) + 1
            local iy0 = floor((min(ay, by) - hm) / T + half) + 1
            local iy1 = floor((max(ay, by) + hm) / T + half) + 1
            if ix1 >= 1 and iy1 >= 1 and ix0 <= N and iy0 <= N then
              local dx, dy = bx - ax, by - ay
              local L2 = dx * dx + dy * dy
              for iy = max(1, iy0), min(N, iy1) do
                for ix = max(1, ix0), min(N, ix1) do
                  local i = (iy - 1) * N + ix
                  if land[i] == 1 then
                    local px, py = (ix - 0.5 - half) * T, (iy - 0.5 - half) * T
                    local u = L2 > 0 and ((px - ax) * dx + (py - ay) * dy) / L2 or 0
                    if u < 0 then u = 0 elseif u > 1 then u = 1 end
                    local ex, ey = px - ax - dx * u, py - ay - dy * u
                    local hw = hw0 + (hw1 - hw0) * u
                    if ex * ex + ey * ey <= hw * hw then
                      if flood then
                        if riv[i] == 0 then riv[i] = -1 end
                      else
                        local depth = d0 + (d1 - d0) * u
                        local code = 1 + floor(depth * 100 + 0.5)
                        if code > 999 then code = 999 end
                        if riv[i] <= 0 or code < riv[i] then riv[i] = code end
                      end
                    end
                  end
                end
              end
            end
          end
        end
      end
    end
  end
end

-- Is tile i wet river now, and can it be waded?
function M.wet(s, i) return s.riv[i] > 0 end
function M.wadeable(s, i) local r = s.riv[i]; return r > 0 and r <= 101 end

-- Rebuild every tile's A* cost from its occupancy, trees, plots and lanes. Bumps the map version
-- (the path cache is only valid for one version).
function M.rebuild_costs(s)
  local C = cfg()
  local land, occ, plot, tree, lane, cost, riv = s.land, s.occ, s.plot, s.tree, s.lane, s.cost, s.riv
  local st = s.trees.st
  local co, ct, cp, ctr, cl, cf = C.cost_open, C.cost_tree, C.cost_plot, C.cost_trail, C.cost_lane, C.cost_ford
  for i = 1, s.N * s.N do
    local c
    if land[i] == 0 or occ[i] > 0 then c = -1
    elseif riv[i] > 0 then
      -- a river: waded at a ford or across a stream (slowly), else no way across (D-085)
      c = riv[i] <= 101 and cf or -1
    else
      local l = lane[i]
      if l == 2 then c = cl
      elseif l == 1 then c = ctr
      elseif plot[i] > 0 then c = cp
      elseif tree[i] > 0 and st[tree[i]] == 1 then c = ct
      else c = co end
    end
    cost[i] = c
  end
  s.ver = s.ver + 1
end

-- Daily: wear decays and becomes trails/lanes.
function M.daily_wear(s)
  local C = cfg()
  local wear, lane = s.wear, s.lane
  local k = 1 - C.wear_decay
  local wt, wl = C.wear_trail, C.wear_lane
  for i = 1, s.N * s.N do
    local w = wear[i]
    if w > 0 then
      w = w * k
      if w < 0.05 then w = 0 end
      wear[i] = w
      -- hysteresis: a lane stays a lane until its wear falls well below the threshold
      local l = lane[i]
      if w >= wl or (l == 2 and w >= wl * 0.6) then lane[i] = 2
      elseif w >= wt or (l >= 1 and w >= wt * 0.6) then lane[i] = 1
      else lane[i] = 0 end
    elseif lane[i] ~= 0 then lane[i] = 0 end
  end
end

-- Add wear along the segment (x0,y0)-(x1,y1): one unit per tile entered (DDA over tiles).
function M.wear_segment(s, x0, y0, x1, y1, amount)
  local T = s.T
  local dx, dy = x1 - x0, y1 - y0
  local len = sqrt(dx * dx + dy * dy)
  local steps = floor(len / (T * 0.7)) + 1
  local wear = s.wear
  local last = 0
  for k = 0, steps do
    local f = k / steps
    local i = M.tile_of(s, x0 + dx * f, y0 + dy * f)
    if i > 0 and i ~= last then
      wear[i] = wear[i] + amount
      last = i
    end
  end
end

-- Trees ------------------------------------------------------------------------------------

function M.add_tree(s, x, y, size)
  local t = s.trees
  local i = M.tile_of(s, x, y)
  if i == 0 or s.tree[i] ~= 0 then return 0 end
  local k = t.n + 1
  t.n = k
  t.x[k], t.y[k], t.size[k], t.st[k], t.logs[k], t.claim[k], t.t_cut[k], t.tile[k] = x, y, size, 1, 0, 0, -1, i
  s.tree[i] = k
  return k
end

-- Logs a tree of size `size` yields.
function M.tree_logs(size)
  local C = cfg()
  local lo, hi = C.logs_per_tree[1], C.logs_per_tree[2]
  return lo + floor((hi - lo) * size + 0.5)
end

-- Clear trees (and their tile marks) in a tile rectangle (founding: the village ground).
function M.clear_rect(s, ix0, iy0, ix1, iy1)
  local N = s.N
  local t = s.trees
  for iy = max(1, iy0), min(N, iy1) do
    for ix = max(1, ix0), min(N, ix1) do
      local i = (iy - 1) * N + ix
      local k = s.tree[i]
      if k > 0 then
        t.st[k] = 0          -- gone (not a stump: cleared ground)
        t.size[k] = 0
        s.tree[i] = 0
      end
    end
  end
end

-- Is the tile rectangle free land (no water, building, plot)?
function M.rect_free(s, ix0, iy0, ix1, iy1, allow_trees)
  local N = s.N
  if ix0 < 2 or iy0 < 2 or ix1 > N - 1 or iy1 > N - 1 then return false end
  for iy = iy0, iy1 do
    for ix = ix0, ix1 do
      local i = (iy - 1) * N + ix
      if s.land[i] == 0 or s.occ[i] ~= 0 or s.plot[i] ~= 0 or s.riv[i] ~= 0 then return false end
      if not allow_trees and s.tree[i] ~= 0 then return false end
    end
  end
  return true
end

-- The nearest walkable point to (x, y): itself when its tile is walkable, else the centre of
-- the nearest walkable tile within r tiles (spiral), else nil.
function M.free_spot(s, x, y, r)
  local i = M.tile_of(s, x, y)
  if i > 0 and s.cost[i] > 0 then return x, y end
  local N = s.N
  local ix = floor(x / s.T + N / 2) + 1
  local iy = floor(y / s.T + N / 2) + 1
  for d = 1, r or 6 do
    local best, bd = 0, 1e9
    for jy = iy - d, iy + d do
      for jx = ix - d, ix + d do
        if (abs(jx - ix) == d or abs(jy - iy) == d) and jx >= 1 and jy >= 1 and jx <= N and jy <= N then
          local j = (jy - 1) * N + jx
          if s.cost[j] > 0 then
            local dd = (jx - ix) * (jx - ix) + (jy - iy) * (jy - iy)
            if dd < bd then best, bd = j, dd end
          end
        end
      end
    end
    if best > 0 then return M.tile_xy(s, best) end
  end
  return nil
end

-- Deterministic float for map placement (stream V3_MAP).
function M.u(W, s, a, b)
  return rng.float(W.seed, rng.S.V3_MAP, s.key, a, b)
end

return M
