-- Fish in the sea, from the sea's own fish (S24, D-195). The planet keeps its fish as a density on each sea cell (`F.fish`:
-- the ocean system's stock, what the fishers' nets draw on - sim/agents/act.lua WORK.fish - and what warm water, oil and
-- overfishing thin). Here that density is shown honestly: small schools just under the water where a cell holds fish, as
-- many as it holds (none where it holds none, fewer as it thins, back as it recovers), each swimming its own slow round
-- about its patch of sea and turning as one; seen from the mid zoom in. Reads only.
local state = require("game.view.state")
local vmx = require("game.view.vmx")
local propgen = require("game.view.propgen")

local M = {}
M.POOL = 30            -- schools drawn at most
M.PER = 3.6            -- schools a cell shows at fish 1.0 (the cells near the view)
M.MIN_ZOOM = 0.8       -- seen from here in
M.NEAR = 0.992         -- cells whose middle is within this dot of the view focus
M.VARIANTS = 3         -- shapes of shoal (each school keeps its own)
M.stats = { schools = 0, cells = 0 }

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, floor, max, min, pi = math.sin, math.cos, math.sqrt, math.floor, math.max, math.min, math.pi

-- A school: n small fish (flat slender diamonds, nose along +x, on the water's plane y = 0) in a loose drawn-out shoal,
-- denser at its middle (variant v: its own shape). Dark backs with a few silver flanks, seen through the water - from the
-- mid zoom a dark shoal shifting on the blue, close in its fish. (S24: 9-14 fish a few pixels long, half transparent,
-- could not be seen at all: the shoal is drawn as many as make it read as one, each fish the size the game draws its
-- other beasts at.)
local function school_buffer(seed, v, n)
  local h = seed
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local dark = propgen.pack_rgb(0.08, 0.13, 0.20)
  local silver = propgen.pack_rgb(0.58, 0.66, 0.74)
  local buf = buffer.create(n * 6, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local k = 0
  for i = 1, n do
    -- (the shoal: longer than wide, denser at its middle)
    local a, r = u() * 2 * pi, sqrt(u())
    local x, z = cos(a) * r * 0.5, sin(a) * r * 0.22
    -- (each variant: this fish a little ahead or behind, to one side)
    local ph = u() * 2 * pi
    x = x + cos(ph + v * 2.1) * 0.07
    z = z + sin(ph * 1.3 + v * 1.7) * 0.04
    local L = 0.07 + 0.04 * u()
    local W2 = L * 0.24
    local yaw = (u() - 0.5) * 0.35 + sin(ph + v) * 0.15
    local cy, sy = cos(yaw), sin(yaw)
    local function p(ax, az) return x + ax * cy - az * sy, z + ax * sy + az * cy end
    local nx, nz = p(L * 0.5, 0)
    local tx, tz = p(-L * 0.5, 0)
    local lx, lz = p(L * 0.05, W2)
    local rx, rz = p(L * 0.05, -W2)
    local c = (u() < 0.2) and silver or dark
    local alpha = 3   -- (fx.vp: 0.25 + 0.75 x 3/7 - seen through the water)
    for _, q in ipairs({ { nx, nz }, { lx, lz }, { tx, tz }, { nx, nz }, { tx, tz }, { rx, rz } }) do
      local o = k * 3
      pos[o + 1], pos[o + 2], pos[o + 3] = q[1], 0, q[2]
      nrm[o + 1], nrm[o + 2], nrm[o + 3] = 0, 1, 0
      col[k * 2 + 1], col[k * 2 + 2] = c, 31 * 8 + alpha
      k = k + 1
    end
  end
  return buf
end

function M.init(W, fac, surface)
  M.W, M.surface = W, surface
  if M.pool then return end
  M.pool = {}
  M.res = {}
  for v = 1, M.VARIANTS do
    for sz = 1, 2 do
      local n = sz == 1 and 30 or 48
      M.res[(sz - 1) * M.VARIANTS + v] = resource.create_buffer("/tc/fish_school_" .. sz .. "_" .. v .. ".bufferc",
        { buffer = school_buffer(9973 * sz + 17, v, n) })
    end
  end
  for i = 1, M.POOL do
    local id = factory.create(fac, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    msg.post(url, MSG_DISABLE)
    M.pool[i] = { id = id, url = url, on = false, res = nil }
  end
  M.timer = 0
  M.list = {}
end

-- (a stable hash of a cell and a school's number)
local function hsh(c, k, m)
  local x = (c * 73856093 + k * 19349663 + m * 83492791) % 2147483647
  x = (x * 48271) % 2147483647
  return x / 2147483647
end

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

function M.update(W, dt, zoom, frozen)
  if not M.pool then return end
  M.timer = (M.timer or 0) - dt
  local g = W.grid
  if M.timer <= 0 then
    M.timer = 0.6
    -- the sea cells near the view and their schools (as many as the fish they hold)
    local L = M.list
    for i = #L, 1, -1 do L[i] = nil end
    if zoom >= M.MIN_ZOOM then
      local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
      local F = W.F
      local cells = 0
      for c = 1, W.n do
        if W.geo.ocean[c] == 1 and (F.ice[c] or 0) < 0.3 then
          local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
          if d > M.NEAR then
            local fish = F.fish[c] or 0
            local want = fish * M.PER
            local n = floor(want)
            if hsh(c, 0, 7) < want - n then n = n + 1 end
            if n > 0 then cells = cells + 1 end
            for k = 1, n do L[#L + 1] = { c = c, k = k, d = d, big = fish > 0.55 and hsh(c, k, 3) < 0.5 } end
          end
        end
      end
      table.sort(L, function(a, b) if a.d ~= b.d then return a.d > b.d end if a.c ~= b.c then return a.c < b.c end return a.k < b.k end)
      M.stats.cells = cells
    end
    M.stats.schools = min(#M.list, M.POOL)
  end
  local t = state.time
  -- (the cells' spacing in world units: the grid's is on the unit sphere)
  local sp = g.spacing * state.R
  local n = min(#M.list, M.POOL)
  -- (S25) the sea's own current this season (sim/systems/ocean.lua): the schools of a cell go with it together
  local CUR = W.currents and W.currents[floor(((W.tick or 0) % 48) / 12) + 1]
  for i, o in ipairs(M.pool) do
    local e = i <= n and M.list[i] or nil
    if e then
      local c = e.c
      -- its own slow round about its patch of the cell (a loop of its own size, way and pace)
      local cx, cy, cz = g.X[c], g.Y[c], g.Z[c]
      local ox = (hsh(c, e.k, 1) - 0.5) * sp * 0.7
      local oy = (hsh(c, e.k, 2) - 0.5) * sp * 0.7
      local rr = sp * (0.06 + 0.10 * hsh(c, e.k, 4))
      local w = (0.05 + 0.06 * hsh(c, e.k, 5)) * (hsh(c, e.k, 6) < 0.5 and 1 or -1)
      local a = t * w + hsh(c, e.k, 8) * 2 * pi
      local px, py = ox + cos(a) * rr, oy + sin(a * 1.3) * rr * 0.6
      -- (the heading: along its round)
      local vx, vy = -sin(a) * rr * w, cos(a * 1.3) * rr * 0.6 * 1.3 * w
      -- the cell's local frame: east and north on the sphere
      local nx, ny, nz = cx, cy, cz
      local ux, uy, uz = 0, 1, 0
      if math.abs(ny) > 0.95 then ux, uy, uz = 1, 0, 0 end
      -- east = up x n, north = n x east
      local Ex, Ey, Ez = uy * nz - uz * ny, uz * nx - ux * nz, ux * ny - uy * nx
      local el = sqrt(Ex * Ex + Ey * Ey + Ez * Ez)
      Ex, Ey, Ez = Ex / el, Ey / el, Ez / el
      local Nx, Ny, Nz = ny * Ez - nz * Ey, nz * Ex - nx * Ez, nx * Ey - ny * Ex
      -- (S25) a coherent run along the current: every school of the cell drifting down it and back over the same slow
      -- beat, heading the way they go - more so the stronger the current
      if CUR and CUR.vx and CUR.vx[c] then
        local ce = CUR.vx[c] * Ex + CUR.vy[c] * Ey + CUR.vz[c] * Ez
        local cn = CUR.vx[c] * Nx + CUR.vy[c] * Ny + CUR.vz[c] * Nz
        local cs = sqrt(ce * ce + cn * cn)
        if cs > 1e-6 then
          local beat = t * 0.045 + hsh(c, 0, 13) * 6.28
          local amp = sp * 0.22
          px, py = px + ce / cs * amp * sin(beat), py + cn / cs * amp * sin(beat)
          local dv = cos(beat) >= 0 and 1 or -1
          local k = min(0.85, 0.45 + cs * 3)
          local lv = sqrt(vx * vx + vy * vy) + 1e-9
          vx, vy = vx / lv * (1 - k) + ce / cs * dv * k, vy / lv * (1 - k) + cn / cs * dv * k
        end
      end
      local wx, wy, wz = nx + (Ex * px + Nx * py) / state.R, ny + (Ey * px + Ny * py) / state.R, nz + (Ez * px + Nz * py) / state.R
      local wl = sqrt(wx * wx + wy * wy + wz * wz)
      wx, wy, wz = wx / wl, wy / wl, wz / wl
      local r = M.surface(wx, wy, wz) + 0.0015
      pos.x, pos.y, pos.z = wx * r, wy * r, wz * r
      go.set_position(pos, o.id)
      -- (its heading in the world, turned into the movers' yaw: model +x forward - agents_view yaw_x)
      local Dx, Dy, Dz = Ex * vx + Nx * vy, Ey * vx + Ny * vy, Ez * vx + Nz * vy
      local fex, fey, fez, fnx, fny, fnz = propgen.frame(wx, wy, wz)
      local yaw = -math.atan2(Dx * fnx + Dy * fny + Dz * fnz, Dx * fex + Dy * fey + Dz * fez)
      go.set_rotation(vmx.set_up_yaw(rot, wx, wy, wz, yaw), o.id)
      local s = sp * (e.big and 0.26 or 0.18)
      scl.x, scl.y, scl.z = s, s, s
      go.set_scale(scl, o.id)
      -- (its own shape of shoal, kept: a school swims as one, its fish holding their places among the others)
      local v = floor(hsh(c, e.k, 9) * M.VARIANTS) % M.VARIANTS + 1
      local res = M.res[(e.big and 1 or 0) * M.VARIANTS + v]
      if o.res ~= res then o.res = res; go.set(o.url, "vertices", res) end
      if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
    elseif o.on then
      o.on = false
      msg.post(o.url, MSG_DISABLE)
    end
  end
end

return M
