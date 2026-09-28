-- Rivers (renderer v2, D-048): the sim's drainage network drawn as meandering water ribbons.
-- Every land cell whose mean flow is in the upper part of the planet's range gets a ribbon from
-- its centre to its downhill neighbour (W.geo.downhill), width from sqrt(flow): tributaries
-- join into rivers that widen toward the sea. The ribbons follow the rendered terrain height,
-- widen while a river floods (flow above its bankfull) and dry to thin streams in droughts.
-- Rebuilt at most every REBUILD_S seconds when the quantised widths change.
local planet = require("game.view.planet")
local cfgmod = require("sim.config")
local speed = require("game.platform.speed")

local M = {}

M.REBUILD_S = 1.5
M.SHARE = 0.22           -- share of land cells (by mean flow) that carry a visible river
M.MIN_FLOW = 0.004       -- ... but never below this mean flow
M.SUB = 7                -- sub-segments per cell-to-cell link
M.LIFT = 0.012           -- height above the terrain surface (world units)
M.BYPASS = 3.6           -- lot rings out a river passes a village (outside its houses)

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local sqrt, floor, max, min, sin, cos, abs, pi = math.sqrt, math.floor, math.max, math.min, math.sin,
  math.cos, math.abs, math.pi

M.stats = { links = 0, verts = 0, rebuilds = 0 }

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

-- Surface radius at unit direction: the highest of the nearest render vertex and its
-- neighbours (the ribbon rides just above the local terrain; cheap: one short greedy walk).
local walk = 1
local function surface(PL, x, y, z)
  local v = planet.nearest(PL, x, y, z, walk)
  walk = v
  local h, rn, rcnt = PL.h, PL.rn, PL.rcnt
  if h[v] <= 0 then return PL.R, true end
  local hv = h[v]
  local base = v * 6 - 6
  local hm = hv
  for k = 1, rcnt[v] do
    local hj = h[rn[base + k]]
    if hj > hm then hm = hj end
  end
  -- between the vertex and its highest neighbour: a little above the flat terrain
  return PL.R + (hv * 0.6 + hm * 0.4) * PL.EXAG, false
end

function M.init(W, PL, url)
  M.W, M.PL, M.url = W, PL, url
  M.sig = nil
  M.since = 1e9
end

local function signature(W, widths)
  local s = 0
  for c = 1, W.n do
    local w = widths[c]
    -- (quantised: a river is redrawn when it visibly swells or shrinks, not every week)
    if w > 0 then s = (s * 31 + c * 7 + floor(w * 50)) % 2147483647 end
  end
  return s
end

-- Visible width per cell (0 = no river).
local function widths(W)
  local geo, F = W.geo, W.F
  local fm = W.S.hydro and W.S.hydro.flow_mean
  local bank = W.S.hydro and W.S.hydro.bankfull
  local out = M.wbuf or {}
  M.wbuf = out
  local vals = {}
  for c = 1, W.n do
    out[c] = 0
    if geo.ocean[c] == 0 and fm then vals[#vals + 1] = fm[c] end
  end
  if #vals == 0 then return out end
  table.sort(vals)
  local thr = max(M.MIN_FLOW, vals[max(1, floor(#vals * (1 - M.SHARE)))])
  local top = vals[#vals]
  for c = 1, W.n do
    if geo.ocean[c] == 0 and fm[c] >= thr then
      local f = fm[c]
      -- floods swell the river, droughts thin it
      local now = F.flow[c]
      local k = 1
      if bank and bank[c] > 0 then k = min(2.2, max(0.45, (0.5 * now + 0.5 * f) / max(f, 1e-6))) end
      local rel = sqrt(f / top)
      out[c] = (0.018 + 0.07 * rel) * k
    end
  end
  return out
end

-- Cells where a living village has buildings (their rivers skirt the houses).
local function built_cells(W)
  local out, n = {}, 0
  local vs = W.ent and W.ent.villages
  local B = W.ent and W.ent.buildings
  if not vs or not B then return out, 0 end
  for i = 1, #vs do
    local v = vs[i]
    if v.alive ~= false and v.buildings then
      if not out[v.cell] then out[v.cell] = true; n = n + v.cell end
      for k = 1, #v.buildings do
        local b = B[v.buildings[k]]
        if b and b.cell and not out[b.cell] and b.kind ~= "pier" and b.kind ~= "boat" then out[b.cell] = true; n = n + b.cell end
      end
    end
  end
  return out, n
end

-- The point a river passes through cell c: its centre, or beside the houses where a village
-- stands (M.BYPASS lot rings out, across the flow), so the water runs past the village.
local function junction(W, c, built)
  local g = W.grid
  local X, Y, Z = g.X, g.Y, g.Z
  if not built[c] then return X[c], Y[c], Z[c] end
  local d = W.geo.downhill[c]
  if not d or d == 0 then return X[c], Y[c], Z[c] end
  local x, y, z = X[c], Y[c], Z[c]
  local tx, ty, tz = X[d] - x, Y[d] - y, Z[d] - z
  local sx, sy, sz = y * tz - z * ty, z * tx - x * tz, x * ty - y * tx
  local sl = sqrt(sx * sx + sy * sy + sz * sz) + 1e-9
  local sgn = (c * 7919) % 2 == 0 and 1 or -1
  local k = sgn * M.BYPASS * cfgmod.SOCIETY.lot_r / sl
  return norm(x + sx * k, y + sy * k, z + sz * k)
end

function M.rebuild(W)
  local PL = M.PL
  local g = W.grid
  local geo = W.geo
  local down = geo.downhill
  local wd = widths(W)
  local built, bsig = built_cells(W)
  local sig = (signature(W, wd) * 13 + bsig) % 2147483647
  if sig == M.sig then return false end
  M.sig = sig
  -- count links
  local links = {}
  for c = 1, W.n do
    if wd[c] > 0 then
      local d = down[c]
      if d and d ~= 0 then links[#links + 1] = c end
    end
  end
  local SUB = M.SUB
  local nv = #links * SUB * 6
  M.stats.links, M.stats.verts = #links, nv
  if nv == 0 then nv = 3 end
  local buf = buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local v = 0
  local X, Y, Z = g.X, g.Y, g.Z
  local P = {}
  for li = 1, #links do
    local c = links[li]
    local d = down[c]
    local w0 = wd[c]
    local w1 = wd[d] > 0 and wd[d] or w0 * 1.1
    local ax, ay, az = junction(W, c, built)
    local bx, by, bz = junction(W, d, built)
    -- meander: a smooth sideways offset, zero at both ends so links join up
    local hs = ((c * 7919) % 1000) / 1000
    local amp = 0.06 + 0.06 * hs
    local sgn = hs < 0.5 and -1 or 1
    local tx, ty, tz = bx - ax, by - ay, bz - az
    for k = 0, SUB do
      local t = k / SUB
      local x, y, z = norm(ax + tx * t, ay + ty * t, az + tz * t)
      -- side direction: up x along
      local sx, sy, sz = y * tz - z * ty, z * tx - x * tz, x * ty - y * tx
      local sl = sqrt(sx * sx + sy * sy + sz * sz) + 1e-9
      sx, sy, sz = sx / sl, sy / sl, sz / sl
      local off = (sgn * sin(pi * t) + 0.45 * sin(2 * pi * t + hs * 6)) * amp / PL.R * sin(pi * t)
      x, y, z = norm(x + sx * off, y + sy * off, z + sz * off)
      local r, wet = surface(PL, x, y, z)
      local w = (w0 + (w1 - w0) * t) * 0.5
      if k > 0 and (wet or P[k - 1].sea) then
        -- the river has reached the sea: the rest of the link collapses onto the shore point
        P[k] = P[k - 1]
        P[k].sea = true
      else
        r = r + M.LIFT
        P[k] = { x * r, y * r, z * r, sx, sy, sz, w, x, y, z }
      end
    end
    local ulen = sqrt(tx * tx + ty * ty + tz * tz) * PL.R
    for k = 0, SUB - 1 do
      local A, B = P[k], P[k + 1]
      local u0, u1 = ulen * k / SUB + li * 3.1, ulen * (k + 1) / SUB + li * 3.1
      local a1 = { A[1] - A[4] * A[7], A[2] - A[5] * A[7], A[3] - A[6] * A[7], -1, u0 }
      local a2 = { A[1] + A[4] * A[7], A[2] + A[5] * A[7], A[3] + A[6] * A[7], 1, u0 }
      local b1 = { B[1] - B[4] * B[7], B[2] - B[5] * B[7], B[3] - B[6] * B[7], -1, u1 }
      local b2 = { B[1] + B[4] * B[7], B[2] + B[5] * B[7], B[3] + B[6] * B[7], 1, u1 }
      local quad = { a1, a2, b2, a1, b2, b1 }
      local ups = { A, A, B, A, B, B }
      for q = 1, 6 do
        local p, U = quad[q], ups[q]
        local o, oc = v * 3, v * 4
        pos[o + 1], pos[o + 2], pos[o + 3] = p[1], p[2], p[3]
        nrm[o + 1], nrm[o + 2], nrm[o + 3] = U[8], U[9], U[10]
        col[oc + 1], col[oc + 2], col[oc + 3], col[oc + 4] = p[5], p[4], U[7], wd[c]
        v = v + 1
      end
    end
  end
  local R = PL.R * 1.08
  buffer.set_metadata(buf, hash("AABB"), { -R, -R, -R, R, R, R }, buffer.VALUE_TYPE_FLOAT32)
  if M.res then
    resource.set_buffer(M.res, buf, { transfer_ownership = true })
  else
    M.gen = (M.gen or 0) + 1
    M.res = resource.create_buffer("/tc/rivers" .. M.gen .. ".bufferc", { buffer = buf })
    go.set(M.url, "vertices", M.res)
  end
  M.stats.rebuilds = M.stats.rebuilds + 1
  return true
end

function M.update(W, dt)
  if not M.PL then return end
  M.since = M.since + dt
  -- (at weeks a second and faster the rivers are checked less often: the frame is the sim's)
  local every = M.REBUILD_S
  if speed.hours_per_second() > 60 then every = every * 4 end
  if M.since < every then return end
  if W.tick == M.last_tick and M.sig then return end
  M.since = 0
  M.last_tick = W.tick
  M.rebuild(W)
end

return M
