-- Picking (Stage 1.9, 04_PLAYER.md §4): ray vs planet sphere -> nearest sim cell
-- (grid.nearest), then entities by screen distance with priority
-- person > village > tanker/boat > animal > volcano > cell.
local camera = require("game.view.camera")
local state = require("game.view.state")
local grid = require("sim.grid")
local ents = require("game.view.ents")
local puppets = require("game.view.puppets")

local M = {}

M.RADIUS_PX = 22      -- entity pick radius in window pixels (scaled by window height / 720)
local PRIORITY = { person = 1, village = 2, ship = 3, animal = 4, volcano = 5 }

local sqrt = math.sqrt

--- Cell under window pixel (sx, sy), or nil when the ray misses the planet.
function M.cell_at(W, sx, sy)
  local ox, oy, oz, dx, dy, dz = camera.ray(sx, sy)
  local hx, hy, hz = camera.hit_sphere(ox, oy, oz, dx, dy, dz, state.R + 0.05)
  if not hx then return nil end
  local l = sqrt(hx * hx + hy * hy + hz * hz)
  local c = grid.nearest(W.grid, hx / l, hy / l, hz / l, M.last_cell)
  M.last_cell = c
  return c, hx / l, hy / l, hz / l
end

local function consider(best, kind, sel, x, y, z, sx, sy, radius)
  local px, py, depth = camera.project(x, y, z)
  if not px then return best end
  local l = sqrt(x * x + y * y + z * z)
  if l > 0 and not camera.facing(x / l, y / l, z / l) then return best end
  local d = sqrt((px - sx) ^ 2 + (py - sy) ^ 2)
  if d > radius then return best end
  local pr = PRIORITY[kind]
  if not best or pr < best.pr or (pr == best.pr and d < best.d) then
    return { pr = pr, d = d, sel = sel }
  end
  return best
end

--- Selection at window pixel (sx, sy): the highest-priority entity within the pick radius,
-- else the cell under the cursor, else nil.
function M.pick(W, sx, sy)
  local radius = M.RADIUS_PX * (state.window_h or 720) / 720
  local best = nil
  for _, p in ipairs(puppets.people_pos) do
    best = consider(best, "person", { kind = "person", id = p.id }, p.x, p.y, p.z, sx, sy, radius)
  end
  local g = W.grid
  for _, v in ents.each_village(W) do
    local c = v.cell
    local r = camera.height_fn and camera.height_fn(g.X[c], g.Y[c], g.Z[c]) or state.R
    best = consider(best, "village", { kind = "village", id = v.id, cell = c },
      g.X[c] * r, g.Y[c] * r, g.Z[c] * r, sx, sy, radius * 1.6)
  end
  for _, s in ipairs(puppets.ship_pos) do
    best = consider(best, "ship", { kind = "ship", index = s.index, list = s.list, ship_kind = s.kind }, s.x, s.y, s.z, sx, sy, radius)
  end
  for _, a in ipairs(puppets.animal_pos) do
    best = consider(best, "animal", { kind = "animal", species = a.kind, cell = a.cell }, a.x, a.y, a.z, sx, sy, radius)
  end
  for i, v in ipairs(ents.volcanoes(W)) do
    if type(v) == "table" and type(v.cell) == "number" then
      local c = v.cell
      best = consider(best, "volcano", { kind = "volcano", index = i, cell = c },
        g.X[c] * state.R, g.Y[c] * state.R, g.Z[c] * state.R, sx, sy, radius)
    end
  end
  if best then return best.sel end
  local c = M.cell_at(W, sx, sy)
  if c then return { kind = "cell", cell = c } end
  return nil
end

return M
