-- Allocation-free transform helpers for per-frame movers (clouds, puppets, fx).
local M = {}

local sqrt, sin, cos = math.sqrt, math.sin, math.cos

--- Writes into quat q the rotation taking +Y to the unit vector (ux,uy,uz), preceded by a
-- rotation of `yaw` radians around +Y (local heading).
function M.set_up_yaw(q, ux, uy, uz, yaw)
  local x1, z1, w1 = uz, -ux, 1 + uy
  if w1 < 1e-6 then x1, z1, w1 = 1, 0, 0 end   -- pointing straight down
  local l = sqrt(x1 * x1 + z1 * z1 + w1 * w1)
  x1, z1, w1 = x1 / l, z1 / l, w1 / l
  local s, c = sin(yaw * 0.5), cos(yaw * 0.5)
  q.x = x1 * c - z1 * s
  q.y = w1 * s
  q.z = x1 * s + z1 * c
  q.w = w1 * c
  return q
end

return M
