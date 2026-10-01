-- The settlement's local map (docs/v3/10_AGENTS.md §2): a square of N x N tiles on the tangent
-- plane at the settlement's sim cell. Local coordinates (x, y) are world units from the cell
-- centre along the cell's east (e) and north (q) axes; the sphere point is the gnomonic
-- projection normalize(R n + x e + y q) (R = 10, the view's planet radius).
--
-- Per tile (flat arrays, index i = (iy - 1) N + ix):
--   land[i]  1 land, 0 water           cell[i]  the sim cell the tile lies in
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

-- Build the tile layer for settlement s at cell s.cell.
function M.build(W, s)
  local C = cfg()
  local N, T = C.map_n, C.tile
  s.N, s.T = N, T
  s.frame = M.frame(W, s.cell)
  local grid = require("sim.grid")
  local ocean = W.geo.ocean
  local NN = N * N
  local land, cell, occ, plot, tree, wear, lane, cost = {}, {}, {}, {}, {}, {}, {}, {}
  local prev = s.cell
  for i = 1, NN do
    local x, y = M.tile_xy(s, i)
    local ux, uy, uz = M.unit(s, x, y)
    local c = grid.nearest(W.grid, ux, uy, uz, prev)
    prev = c
    cell[i] = c
    land[i] = ocean[c] == 1 and 0 or 1
    occ[i], plot[i], tree[i], wear[i], lane[i], cost[i] = 0, 0, 0, 0, 0, C.cost_open
  end
  s.land, s.cell_of, s.occ, s.plot, s.tree, s.wear, s.lane, s.cost = land, cell, occ, plot, tree, wear, lane, cost
  s.trees = { x = {}, y = {}, size = {}, st = {}, logs = {}, claim = {}, t_cut = {}, tile = {}, n = 0 }
  s.ver = 1
end

-- Rebuild every tile's A* cost from its occupancy, trees, plots and lanes. Bumps the map version
-- (the path cache is only valid for one version).
function M.rebuild_costs(s)
  local C = cfg()
  local land, occ, plot, tree, lane, cost = s.land, s.occ, s.plot, s.tree, s.lane, s.cost
  local st = s.trees.st
  local co, ct, cp, ctr, cl = C.cost_open, C.cost_tree, C.cost_plot, C.cost_trail, C.cost_lane
  for i = 1, s.N * s.N do
    local c
    if land[i] == 0 or occ[i] > 0 then c = -1
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
      if s.land[i] == 0 or s.occ[i] ~= 0 or s.plot[i] ~= 0 then return false end
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
