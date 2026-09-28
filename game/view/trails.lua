-- Trails (W7, D-056): the paths people wear walking between villages — porters, brides, migrants
-- (the sim's W.S.trails: walkers a year per step between two cells, fading when nobody walks).
-- Each worn step is an earthen ribbon from cell centre to cell centre, meandering a little and
-- riding on the rendered terrain; width and wear follow the traffic, so a busy trade route
-- becomes a broad packed road and a forgotten one grasses over. Rebuilt when the quantised
-- traffic changes (at most every REBUILD_S seconds).
local planet = require("game.view.planet")
local cfgmod = require("sim.config")

local M = {}

M.REBUILD_S = 2.0
M.MIN = 3                -- walkers a year before a trail shows
M.SUB = 6                -- sub-segments per step
M.LIFT = 0.008           -- above the terrain
M.LANE_SUB = 2           -- sub-segments per village lane
M.LANE_W = 0.011         -- half-width of a village lane (world units)

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local sqrt, floor, max, min, sin, pi = math.sqrt, math.floor, math.max, math.min, math.sin, math.pi

M.stats = { links = 0, verts = 0, rebuilds = 0 }

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

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
  return PL.R + (hv * 0.6 + hm * 0.4) * PL.EXAG, false
end

function M.init(W, PL, url)
  M.W, M.PL, M.url = W, PL, url
  M.sig = nil
  M.since = 1e9
end

-- Village lanes: every standing building joined to the square by the shortest network over
-- the lots (Prim's tree from the well: lanes branch as the village grew), as packed earth.
-- Returns a flat list of segments { ax, ay, az, bx, by, bz, wear } and a signature.
local layout = require("sim.society.layout")
local function lanes(W)
  local out, sig = {}, 0
  local vs = W.ent and W.ent.villages
  local B = W.ent and W.ent.buildings
  if not vs or not B then return out, 0 end
  for i = 1, #vs do
    local v = vs[i]
    if v.alive ~= false and (v.pop or 0) >= 8 and v.buildings then
      local px, py, pz = {}, {}, {}
      local x, y, z = layout.pos(W, v, v.cell, 0)
      px[1], py[1], pz[1] = x, y, z
      for k = 1, #v.buildings do
        local b = B[v.buildings[k]]
        if b and b.state == "standing" and b.kind ~= "boat" and b.kind ~= "palisade" and b.kind ~= "pier" then
          x, y, z = layout.pos(W, v, b.cell, b.lot)
          px[#px + 1], py[#py + 1], pz[#pz + 1] = x, y, z
        end
      end
      local n = #px
      sig = (sig * 31 + v.id * 17 + n * 3 + floor((v.pop or 0) / 25)) % 2147483647
      if n >= 3 then
        -- Prim: best[j] = squared distance of j to the tree, from[j] its nearest tree node
        local intree, best, from = { true }, {}, {}
        for j = 2, n do
          local dx, dy, dz = px[j] - px[1], py[j] - py[1], pz[j] - pz[1]
          best[j], from[j] = dx * dx + dy * dy + dz * dz, 1
        end
        local wear = min(1, 0.35 + (v.pop or 0) / 300)
        for _ = 2, n do
          local bj, bd = nil, 1e9
          for j = 2, n do
            if not intree[j] and best[j] < bd then bj, bd = j, best[j] end
          end
          if not bj then break end
          intree[bj] = true
          local f = from[bj]
          out[#out + 1] = { px[f], py[f], pz[f], px[bj], py[bj], pz[bj], wear }
          for j = 2, n do
            if not intree[j] then
              local dx, dy, dz = px[j] - px[bj], py[j] - py[bj], pz[j] - pz[bj]
              local d = dx * dx + dy * dy + dz * dz
              if d < best[j] then best[j], from[j] = d, bj end
            end
          end
        end
      end
    end
  end
  return out, sig
end
M.lanes = lanes

-- worn steps (sorted keys) and a signature of their quantised traffic
local function worn(W)
  local tr = W.S.trails
  local keys = {}
  local s = 0
  if tr then
    for k, t in pairs(tr) do if t >= M.MIN then keys[#keys + 1] = k end end
    table.sort(keys)
    for i = 1, #keys do
      local k = keys[i]
      s = (s * 31 + k * 7 + floor(sqrt(tr[k]) * 2)) % 2147483647
    end
  end
  return keys, s + #keys
end

function M.rebuild(W)
  local PL = M.PL
  local g = W.grid
  local keys, sig = worn(W)
  local lane, lsig = lanes(W)
  sig = (sig * 7 + lsig) % 2147483647
  if sig == M.sig then return false end
  M.sig = sig
  local tr = W.S.trails
  local full = cfgmod.SOCIETY.trail_full
  local SUB = M.SUB
  local LSUB = M.LANE_SUB
  local nv = #keys * SUB * 6 + #lane * LSUB * 6
  M.stats.links, M.stats.lanes, M.stats.verts = #keys, #lane, nv
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
  for li = 1, #keys do
    local key = keys[li]
    local a, b = floor(key / 4096), key % 4096
    local t = tr[key]
    local wear = min(1, t / full)
    local w = 0.012 + 0.03 * sqrt(wear)
    local ax, ay, az = X[a], Y[a], Z[a]
    local tx, ty, tz = X[b] - ax, Y[b] - ay, Z[b] - az
    local hs = ((key * 7919) % 1000) / 1000
    local amp = 0.04 + 0.05 * hs
    local sgn = hs < 0.5 and -1 or 1
    local wet_any = false
    for k = 0, SUB do
      local f = k / SUB
      local x, y, z = norm(ax + tx * f, ay + ty * f, az + tz * f)
      local sx, sy, sz = y * tz - z * ty, z * tx - x * tz, x * ty - y * tx
      local sl = sqrt(sx * sx + sy * sy + sz * sz) + 1e-9
      sx, sy, sz = sx / sl, sy / sl, sz / sl
      local off = (sgn * sin(pi * f) + 0.4 * sin(2 * pi * f + hs * 6)) * amp / PL.R * sin(pi * f)
      x, y, z = norm(x + sx * off, y + sy * off, z + sz * off)
      local r, wet = surface(PL, x, y, z)
      if wet then wet_any = true end
      r = r + M.LIFT
      local e = P[k]
      if not e then e = {}; P[k] = e end
      e[1], e[2], e[3], e[4], e[5], e[6], e[7], e[8], e[9] = x * r, y * r, z * r, sx, sy, sz, x, y, z
    end
    local ulen = sqrt(tx * tx + ty * ty + tz * tz) * PL.R
    for k = 0, SUB - 1 do
      local A, B = P[k], P[k + 1]
      local u0, u1 = ulen * k / SUB + li * 3.1, ulen * (k + 1) / SUB + li * 3.1
      -- (a step that crosses water is not drawn there: a ford or a boat)
      local ww = wet_any and 0 or w
      local ax1, ay1, az1 = A[1] - A[4] * ww, A[2] - A[5] * ww, A[3] - A[6] * ww
      local ax2, ay2, az2 = A[1] + A[4] * ww, A[2] + A[5] * ww, A[3] + A[6] * ww
      local bx1, by1, bz1 = B[1] - B[4] * ww, B[2] - B[5] * ww, B[3] - B[6] * ww
      local bx2, by2, bz2 = B[1] + B[4] * ww, B[2] + B[5] * ww, B[3] + B[6] * ww
      local qx = { ax1, ax2, bx2, ax1, bx2, bx1 }
      local qy = { ay1, ay2, by2, ay1, by2, by1 }
      local qz = { az1, az2, bz2, az1, bz2, bz1 }
      local qs = { -1, 1, 1, -1, 1, -1 }
      local qu = { u0, u0, u1, u0, u1, u1 }
      local qn = { A, A, B, A, B, B }
      for q = 1, 6 do
        local o, oc = v * 3, v * 4
        local U = qn[q]
        pos[o + 1], pos[o + 2], pos[o + 3] = qx[q], qy[q], qz[q]
        nrm[o + 1], nrm[o + 2], nrm[o + 3] = U[7], U[8], U[9]
        col[oc + 1], col[oc + 2], col[oc + 3], col[oc + 4] = qu[q], qs[q], wear, 0
        v = v + 1
      end
    end
  end
  -- village lanes: straight, narrow, riding the terrain
  local lw = M.LANE_W
  for li = 1, #lane do
    local L = lane[li]
    local ax, ay, az = L[1], L[2], L[3]
    local tx, ty, tz = L[4] - ax, L[5] - ay, L[6] - az
    local wear = L[7]
    for k = 0, LSUB do
      local f = k / LSUB
      local x, y, z = norm(ax + tx * f, ay + ty * f, az + tz * f)
      local sx, sy, sz = y * tz - z * ty, z * tx - x * tz, x * ty - y * tx
      local sl = sqrt(sx * sx + sy * sy + sz * sz) + 1e-9
      sx, sy, sz = sx / sl, sy / sl, sz / sl
      local r = surface(PL, x, y, z) + M.LIFT
      local e = P[k]
      if not e then e = {}; P[k] = e end
      e[1], e[2], e[3], e[4], e[5], e[6], e[7], e[8], e[9] = x * r, y * r, z * r, sx, sy, sz, x, y, z
    end
    local ulen = sqrt(tx * tx + ty * ty + tz * tz) * PL.R
    for k = 0, LSUB - 1 do
      local A, B = P[k], P[k + 1]
      local u0, u1 = ulen * k / LSUB + li * 1.7, ulen * (k + 1) / LSUB + li * 1.7
      local ax1, ay1, az1 = A[1] - A[4] * lw, A[2] - A[5] * lw, A[3] - A[6] * lw
      local ax2, ay2, az2 = A[1] + A[4] * lw, A[2] + A[5] * lw, A[3] + A[6] * lw
      local bx1, by1, bz1 = B[1] - B[4] * lw, B[2] - B[5] * lw, B[3] - B[6] * lw
      local bx2, by2, bz2 = B[1] + B[4] * lw, B[2] + B[5] * lw, B[3] + B[6] * lw
      local qx = { ax1, ax2, bx2, ax1, bx2, bx1 }
      local qy = { ay1, ay2, by2, ay1, by2, by1 }
      local qz = { az1, az2, bz2, az1, bz2, bz1 }
      local qs = { -1, 1, 1, -1, 1, -1 }
      local qu = { u0, u0, u1, u0, u1, u1 }
      local qn = { A, A, B, A, B, B }
      for q = 1, 6 do
        local o, oc = v * 3, v * 4
        local U = qn[q]
        pos[o + 1], pos[o + 2], pos[o + 3] = qx[q], qy[q], qz[q]
        nrm[o + 1], nrm[o + 2], nrm[o + 3] = U[7], U[8], U[9]
        col[oc + 1], col[oc + 2], col[oc + 3], col[oc + 4] = qu[q], qs[q], wear, 0
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
    M.res = resource.create_buffer("/tc/trails" .. M.gen .. ".bufferc", { buffer = buf })
    go.set(M.url, "vertices", M.res)
  end
  M.stats.rebuilds = M.stats.rebuilds + 1
  return true
end

function M.update(W, dt)
  if not M.PL then return end
  M.since = M.since + dt
  if M.since < M.REBUILD_S then return end
  if W.tick == M.last_tick and M.sig then return end
  M.since = 0
  M.last_tick = W.tick
  M.rebuild(W)
end

return M
