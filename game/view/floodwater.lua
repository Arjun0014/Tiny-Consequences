-- Flood water over the land (S21, R6.5e): the tiles the sim's flood module has under water on the
-- villages' maps (sim/agents/flood.lua -> mapm.FLOOD) drawn with the river's water shader (rippling,
-- muddy toward its edges), rebuilt when the water moves. Read-only: the sim's runtime depth array.
-- (S23) The water's outline by marching squares over the tile centres: a flooded tile's centre is in the
-- water, a dry one's is not, and the edge runs midway between them - straight along a row, cut across a
-- corner where it turns (a lone flooded tile is a diamond, not a square); every vertex at the terrain's own
-- height. The edge's vertices are the shader's muddy bank, the centres the water's colour by its depth.
local planet = require("game.view.planet")
local mapm = require("sim.agents.map")
local flood = require("sim.agents.flood")

local M = {}
M.LIFT = 0.007
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

local function across_of(deep)
  return deep > 0.6 and 0.55 or (deep > 0.25 and 0.7 or 0.86)
end

-- the corners of a dual cell (between four tile centres) counter-clockwise, and the edges between them
local CX, CY = { 0, 1, 1, 0 }, { 0, 0, 1, 1 }

-- (S25d) A vertex for the ground's water and paths only where the ground is: anything else (not a number, or far off
-- the planet - Arjun saw two dark planes reaching out from the world) is put at the centre, where the planet hides it.
local function onground(R, x, y, z)
  local l = x * x + y * y + z * z
  if l == l and l < R * R * 1.44 and l > R * R * 0.49 then return x, y, z end
  return 0, 0, 0
end

function M.rebuild(W)
  local PL = M.PL
  local V = W.S.v3
  if not PL or not V then return false end
  -- the water's pieces: per dual cell with water at a corner, its polygon (corners in the water and the
  -- midpoints of the edges the shore crosses), as x, y, across, depth per vertex
  local verts, nt = {}, 0
  local poly = {}
  for _, s in ipairs(V.settlements) do
    local _, fd = flood.view(s)
    if fd and s.alive then
      local N, T = s.N, s.T
      local sid = s.id
      for iy = 1, N - 1 do
        for ix = 1, N - 1 do
          local i1 = (iy - 1) * N + ix
          local d1, d2, d3, d4 = fd[i1], fd[i1 + 1], fd[i1 + N + 1], fd[i1 + N]
          if d1 > 0.02 or d2 > 0.02 or d3 > 0.02 or d4 > 0.02 then
            local D = { d1, d2, d3, d4 }
            local x0, y0 = mapm.ixy_xy(s, ix, iy)
            local np = 0
            for q = 1, 4 do
              local r = q % 4 + 1
              local wq, wr = D[q] > 0.02, D[r] > 0.02
              if wq then
                np = np + 1
                poly[np] = { x0 + CX[q] * T, y0 + CY[q] * T, across_of(D[q]), D[q] }
              end
              if wq ~= wr then
                np = np + 1
                poly[np] = { x0 + (CX[q] + CX[r]) * 0.5 * T, y0 + (CY[q] + CY[r]) * 0.5 * T, 1.0, 0 }
              end
            end
            -- a fan of triangles (the pieces are convex; two corners across a diagonal join as one)
            for k = 2, np - 1 do
              local a, b, c = poly[1], poly[k], poly[k + 1]
              nt = nt + 1
              verts[nt] = { sid, a, b, c }
            end
          end
        end
      end
    end
  end
  local nv = nt * 3
  M.stats.spans, M.stats.verts = nt, nv
  if nv == 0 then nv = 3 end
  -- (S25d, D-222) the mesh never shrinks: it keeps the most vertices it has had, the ones not used this time empty
  -- triangles at the centre (a smaller buffer under a mesh drawn with its old count is one way to the planes Arjun saw)
  local used = M.stats.verts or 0
  nv = math.max(nv, M.cap or 0)
  M.cap = nv
  local buf = buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local v = 0
  for k = 1, nt do
    local t = verts[k]
    local s = V.settlements[t[1]]
    local T = s.T
    for o = 2, 4 do
      local p = t[o]
      local x, y = p[1], p[2]
      local ux, uy, uz = mapm.unit(s, x, y)
      local r = radius(PL, ux, uy, uz)
      local i3, i4 = v * 3, v * 4
      pos[i3 + 1], pos[i3 + 2], pos[i3 + 3] = onground(PL.R, ux * r, uy * r, uz * r)
      nrm[i3 + 1], nrm[i3 + 2], nrm[i3 + 3] = ux, uy, uz
      col[i4 + 1], col[i4 + 2], col[i4 + 3], col[i4 + 4] = x * 9 + s.id * 3.7 + y * 9 * 0.37, p[3], T, p[4]
      v = v + 1
    end
  end
  for k = used, nv - 1 do
    local i3, i4 = k * 3, k * 4
    pos[i3 + 1], pos[i3 + 2], pos[i3 + 3] = 0, 0, 0
    nrm[i3 + 1], nrm[i3 + 2], nrm[i3 + 3] = 0, 1, 0
    col[i4 + 1], col[i4 + 2], col[i4 + 3], col[i4 + 4] = 0, 0, 0, 0
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
