-- Settlement layout: where each building lot is (docs/world/12_PEOPLE_VILLAGES.md §4.3).
-- A pure function shared by the sim and the view, so the houses you see stand exactly where the
-- sim put them. Lot 0 of a village cell is the square (well); lots 1..36 sit on three rings
-- around the cell centre in its tangent plane, rotated and jittered per village.
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local sin, cos, sqrt, pi, floor, acos = math.sin, math.cos, math.sqrt, math.pi, math.floor, math.acos

M.RING_LOTS = { 6, 12, 18 }
M.LOTS = 36                       -- buildable lots per village cell (lot 0 is the square)

-- ring (1..3), index in ring (0-based), lots in ring for lot 1..36
function M.ring_of(lot)
  local k, first = 1, 1
  while k <= #M.RING_LOTS do
    local n = M.RING_LOTS[k]
    if lot < first + n then return k, lot - first, n end
    first = first + n
    k = k + 1
  end
  return #M.RING_LOTS, 0, M.RING_LOTS[#M.RING_LOTS]
end

-- Tangent frame at a cell: normal n, east e, north q (unit vectors).
function M.frame(W, cell)
  local g = W.grid
  local nx, ny, nz = g.X[cell], g.Y[cell], g.Z[cell]
  -- east = axis (0,1,0) x n, normalised (near the poles use x as the reference)
  local ex, ey, ez = nz, 0, -nx
  local l = sqrt(ex * ex + ez * ez)
  if l < 1e-6 then ex, ey, ez, l = 0, -nz, ny, sqrt(nz * nz + ny * ny) end
  ex, ey, ez = ex / l, ey / l, ez / l
  local qx, qy, qz = ny * ez - nz * ey, nz * ex - nx * ez, nx * ey - ny * ex
  return nx, ny, nz, ex, ey, ez, qx, qy, qz
end

local function vkey(v) return (v.key_cell or v.cell or 0) * 7919 + ((v.key_hour or 0) % 100003) end

-- Unit vector of lot `lot` (0..36) on village cell `cell` of village v.
function M.pos(W, v, cell, lot)
  local nx, ny, nz, ex, ey, ez, qx, qy, qz = M.frame(W, cell)
  if lot <= 0 then return nx, ny, nz end
  local k, i, n = M.ring_of(lot)
  local key = vkey(v)
  local rot = (rng.hash(W.seed, rng.S.BUILD, key, cell, 0) % 65536) / 65536 * 2 * pi
  local h = rng.hash(W.seed, rng.S.BUILD, key, cell, lot)
  -- (jitter kept below half a lot's spacing: neighbouring houses never stand in each other)
  local jr = ((h % 1024) / 1024 - 0.5) * 0.16
  local ja = ((floor(h / 1024) % 1024) / 1024 - 0.5) * 0.3 / k
  local a = 2 * pi * (i + 0.5 * (k % 2)) / n + rot + ja
  local r = k * cfgmod.SOCIETY.lot_r * (1 + jr)
  local ca, sa = cos(a) * r, sin(a) * r
  local x, y, z = nx + ex * ca + qx * sa, ny + ey * ca + qy * sa, nz + ez * ca + qz * sa
  local l = sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

-- The outer-ring lot of `cell` facing `toward` (a neighbouring cell, e.g. the mooring): the pier.
function M.facing_lot(W, v, cell, toward)
  local g = W.grid
  local best, bd = 1, -2
  local first = 1 + M.RING_LOTS[1] + M.RING_LOTS[2]
  for lot = first, M.LOTS do
    local x, y, z = M.pos(W, v, cell, lot)
    local d = x * g.X[toward] + y * g.Y[toward] + z * g.Z[toward]
    if d > bd then best, bd = lot, d end
  end
  return best
end

return M
