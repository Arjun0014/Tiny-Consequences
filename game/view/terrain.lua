-- Terrain + sea surface mesh and the sim-cell data texture (Stage 1.8; renderer v2 D-048).
--
-- The mesh (render icosphere, non-indexed) is static: rebuilt only when the coastline or
-- sea level changes. Colours live in a small RGBA texture with 4 texels per sim cell
-- (game/view/cellcolor.lua) that the vertex shader blends; recolouring is O(sim cells),
-- amortised over a few frames and at most MAX_RATE times per second, and only texels whose
-- bytes changed are rewritten before one texture upload.
local cellcolor = require("game.view.cellcolor")
local state = require("game.view.state")

local M = {}

M.TW = 64            -- texture width in cells
M.MAX_RATE = 4       -- colour refreshes per second (max)
M.CELLS_PER_FRAME = 200
M.BUDGET_MS = 1.2    -- per-frame time budget for a recolour pass (checked every 16 cells)
local clock = require("game.platform.clock")

local H_POS, H_NRM, H_CA, H_CC, H_VI = hash("position"), hash("normal"), hash("cella"), hash("cellc"), hash("vinfo")
local H_RGBA = hash("rgba")
local MESH_URL = "/terrain#mesh"

local gen = 0
M.stats = { recolours = 0, texels_changed = 0, last_ms = 0 }

local ROWS = 4        -- texels per cell (stacked vertically)
M.ROWS = ROWS

local function cell_uv(c, th)
  local i = c - 1
  local x = i % M.TW
  local y = math.floor(i / M.TW) * ROWS
  return (x + 0.5) / M.TW, (y + 0.5) / th
end

--- Builds the mesh buffer for planet PL. step(frac) may yield.
function M.build(PL, W, step)
  step = step or function() end
  gen = gen + 1
  local nf = PL.nf
  local nverts = nf * 3
  local buf = buffer.create(nverts, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_CA, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_CC, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_VI, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM)
  local sca, scc, svi = buffer.get_stream(buf, H_CA), buffer.get_stream(buf, H_CC), buffer.get_stream(buf, H_VI)
  local th = ROWS * math.ceil(W.n / M.TW)
  M.TH = th
  -- per sim cell uv
  local cu, cv = {}, {}
  for c = 1, W.n do cu[c], cv[c] = cell_uv(c, th) end
  local faces = PL.faces
  local px, py, pz, nx, ny, nz = PL.px, PL.py, PL.pz, PL.nx, PL.ny, PL.nz
  local va, vb, vc, vwa, vwb = PL.va, PL.vb, PL.vc, PL.vwa, PL.vwb
  local h, s, n1 = PL.h, PL.s, PL.n1
  local p, q = 0, 0
  for f = 1, nf do
    local i = f * 3 - 2
    for k = 0, 2 do
      local v = faces[i + k]
      pos[p + 1], pos[p + 2], pos[p + 3] = px[v], py[v], pz[v]
      nrm[p + 1], nrm[p + 2], nrm[p + 3] = nx[v], ny[v], nz[v]
      local a, b, c = va[v], vb[v], vc[v]
      sca[q + 1], sca[q + 2], sca[q + 3], sca[q + 4] = cu[a], cv[a], cu[b], cv[b]
      scc[q + 1], scc[q + 2], scc[q + 3], scc[q + 4] = cu[c], cv[c], vwa[v], vwb[v]
      local sv = s[v]
      if sv > 1 then sv = 1 elseif sv < -1 then sv = -1 end
      -- (z: the smooth cell elevation under the vertex, km: its height above that is cooler, S14)
      svi[q + 1], svi[q + 2], svi[q + 3], svi[q + 4] = h[v], n1[v], PL.ecell and PL.ecell[v] or 0, sv
      p, q = p + 3, q + 4
    end
    if f % 2048 == 0 then step(f / nf) end
  end
  local R = PL.R * 1.08
  buffer.set_metadata(buf, hash("AABB"), { -R, -R, -R, R, R, R }, buffer.VALUE_TYPE_FLOAT32)
  if M.res then
    resource.set_buffer(M.res, buf, { transfer_ownership = true })
  else
    M.res = resource.create_buffer("/tc/terrain" .. gen .. ".bufferc", { buffer = buf })
    go.set(MESH_URL, "vertices", M.res)
  end

  -- data texture (created once per sim size)
  if not M.tex or M.tex_th ~= th then
    M.texbuf = buffer.create(M.TW * th, { { name = H_RGBA, type = buffer.VALUE_TYPE_UINT8, count = 4 } })
    M.texstream = buffer.get_stream(M.texbuf, H_RGBA)
    M.tex_params = { type = graphics.TEXTURE_TYPE_2D, width = M.TW, height = th, format = graphics.TEXTURE_FORMAT_RGBA }
    M.tex = resource.create_texture("/tc/celltex" .. gen .. ".texturec", M.tex_params, M.texbuf)
    M.tex_th = th
    go.set(MESH_URL, "texture0", M.tex)
  end
  state.u_tex.x = 1 / th
  state.u_tex.y = M.TW
  state.u_tex.z = th
  M.bytes = {}
  M.W = W
  M.pass_cell = 0
  M.pass_dirty = false
  M.since = 0
  M.last_tick = -1
  M.recolour_all(W)
  return nverts
end

-- Writes cell c's 16 bytes into the texture stream if they changed. Returns true if changed.
local tmp = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
local function update_cell(W, c)
  cellcolor.cell(W, c, tmp, 0)
  local bytes = M.bytes
  local base = c * 16 - 16
  local changed = false
  for k = 1, 16 do
    if bytes[base + k] ~= tmp[k] then changed = true; bytes[base + k] = tmp[k] end
  end
  if changed then
    local i = c - 1
    local x = i % M.TW
    local y = math.floor(i / M.TW) * ROWS
    local st = M.texstream
    for r = 0, ROWS - 1 do
      local s0 = ((y + r) * M.TW + x) * 4
      local t0 = r * 4
      st[s0 + 1], st[s0 + 2], st[s0 + 3], st[s0 + 4] = tmp[t0 + 1], tmp[t0 + 2], tmp[t0 + 3], tmp[t0 + 4]
    end
  end
  return changed
end

function M.recolour_all(W)
  cellcolor.prepare(W)
  local n = 0
  for c = 1, W.n do
    if update_cell(W, c) then n = n + 1 end
  end
  resource.set_texture(M.tex, M.tex_params, M.texbuf)
  M.stats.recolours = M.stats.recolours + 1
  M.stats.texels_changed = n
end

--- Per-frame: amortised recolour when the sim advanced (<= MAX_RATE full passes per second).
function M.update(W, dt)
  if not M.tex then return end
  M.since = M.since + dt
  if M.pass_cell == 0 then
    if W.tick == M.last_tick or M.since < 1 / M.MAX_RATE then
      -- (S20, R6.5d) snow on the village maps changes by the hour: those cells alone between passes
      M.snow_since = (M.snow_since or 0) + dt
      if M.snow_since > 0.5 then
        M.snow_since = 0
        local ch = cellcolor.map_snow(W)
        local any = false
        for i = 1, #ch do if update_cell(W, ch[i]) then any = true end end
        if any then resource.set_texture(M.tex, M.tex_params, M.texbuf) end
      end
      return
    end
    M.since = 0
    M.last_tick = W.tick
    M.pass_cell = 1
    cellcolor.prepare(W)
    M.pass_dirty = false
    M.pass_changed = 0
  end
  local last = math.min(W.n, M.pass_cell + M.CELLS_PER_FRAME - 1)
  local t0 = clock.now_ms()
  for c = M.pass_cell, last do
    if update_cell(W, c) then M.pass_dirty = true; M.pass_changed = M.pass_changed + 1 end
    if c % 16 == 0 and c < last and clock.now_ms() - t0 > M.BUDGET_MS then last = c; break end
  end
  if last >= W.n then
    M.pass_cell = 0
    if M.pass_dirty then
      resource.set_texture(M.tex, M.tex_params, M.texbuf)
      M.stats.recolours = M.stats.recolours + 1
      M.stats.texels_changed = M.pass_changed
    end
  else
    M.pass_cell = last + 1
  end
end

return M
