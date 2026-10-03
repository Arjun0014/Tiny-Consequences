-- Flood water over the land (S21, R6.5e): the tiles the sim's flood module has under water on the
-- villages' maps (sim/agents/flood.lua -> mapm.FLOOD) drawn with the river's water shader (rippling,
-- muddy toward its edges), rebuilt when the water moves. Read-only: the sim's runtime depth array.
-- Rows of flooded tiles are drawn as short spans (at most SPAN tiles: the planet curves under a long
-- one), each end at the terrain's own height.
local planet = require("game.view.planet")
local mapm = require("sim.agents.map")
local flood = require("sim.agents.flood")

local M = {}
M.LIFT = 0.007
M.SPAN = 6
M.CHECK_S = 0.4

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local walk = 1

function M.init(W, PL, url)
  M.W, M.PL, M.url = W, PL, url
  M.sig = nil
  M.since = 1e9
  M.stats = { spans = 0, verts = 0, rebuilds = 0 }
end

local function signature(W)
  local h = 0
  local V = W.S.v3
  for _, s in ipairs(V and V.settlements or {}) do
    local v = flood.view(s)
    h = (h * 31 + s.id * 7 + v) % 2147483647
  end
  return h
end

local function radius(PL, x, y, z)
  local r
  if PL.rdist then r, walk = planet.surface_exact(PL, x, y, z, walk) else r = PL.R end
  return r + M.LIFT
end

function M.rebuild(W)
  local PL = M.PL
  local V = W.S.v3
  if not PL or not V then return false end
  -- the spans: per village, per row, runs of flooded tiles cut at SPAN
  local spans = {}
  for _, s in ipairs(V.settlements) do
    local _, fd = flood.view(s)
    if fd and s.alive then
      local N = s.N
      for iy = 1, N do
        local ix = 1
        local row = (iy - 1) * N
        while ix <= N do
          if fd[row + ix] > 0.02 then
            local a = ix
            local deep = fd[row + ix]
            while ix + 1 <= N and ix + 1 - a < M.SPAN and fd[row + ix + 1] > 0.02 do
              ix = ix + 1
              if fd[row + ix] > deep then deep = fd[row + ix] end
            end
            spans[#spans + 1] = { s, a, ix, iy, deep }
          end
          ix = ix + 1
        end
      end
    end
  end
  local n = #spans
  local nv = n * 6
  M.stats.spans, M.stats.verts = n, nv
  if nv == 0 then nv = 3 end
  local buf = buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local v = 0
  for k = 1, n do
    local sp = spans[k]
    local s, a, b, iy, deep = sp[1], sp[2], sp[3], sp[4], sp[5]
    local T = s.T
    local x0, y0 = mapm.ixy_xy(s, a, iy)
    local x1 = mapm.ixy_xy(s, b, iy)
    x0, x1 = x0 - T * 0.5, x1 + T * 0.5
    local ya, yb = y0 - T * 0.5, y0 + T * 0.5
    -- (shallow water reads muddy, deeper water as the river's own)
    local across = deep > 0.6 and 0.55 or (deep > 0.25 and 0.7 or 0.86)
    local corners = { { x0, ya }, { x1, ya }, { x1, yb }, { x0, yb } }
    local P = {}
    for q = 1, 4 do
      local ux, uy, uz = mapm.unit(s, corners[q][1], corners[q][2])
      local r = radius(PL, ux, uy, uz)
      P[q] = { ux * r, uy * r, uz * r, ux, uy, uz, corners[q][1] * 9 + s.id * 3.7, corners[q][2] * 9 }
    end
    local order = { 1, 2, 3, 1, 3, 4 }
    for o = 1, 6 do
      local p = P[order[o]]
      local i3, i4 = v * 3, v * 4
      pos[i3 + 1], pos[i3 + 2], pos[i3 + 3] = p[1], p[2], p[3]
      nrm[i3 + 1], nrm[i3 + 2], nrm[i3 + 3] = p[4], p[5], p[6]
      col[i4 + 1], col[i4 + 2], col[i4 + 3], col[i4 + 4] = p[7] + p[8] * 0.37, across, T, deep
      v = v + 1
    end
  end
  local R = PL.R * 1.08
  buffer.set_metadata(buf, hash("AABB"), { -R, -R, -R, R, R, R }, buffer.VALUE_TYPE_FLOAT32)
  if M.res then
    resource.set_buffer(M.res, buf, { transfer_ownership = true })
  else
    M.gen = (M.gen or 0) + 1
    M.res = resource.create_buffer("/tc/floodwater" .. M.gen .. ".bufferc", { buffer = buf })
    go.set(M.url, "vertices", M.res)
  end
  M.stats.rebuilds = M.stats.rebuilds + 1
  return true
end

function M.update(W, dt)
  if not M.PL or not W.v3 then return end
  M.since = M.since + dt
  if M.since < M.CHECK_S then return end
  M.since = 0
  local sg = signature(W)
  if sg == M.sig then return end
  M.sig = sg
  M.rebuild(W)
end

return M
