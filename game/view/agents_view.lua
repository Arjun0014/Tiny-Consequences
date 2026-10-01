-- The v3 settlement view (docs/v3/10_AGENTS.md, D-078): draws what the agent layer holds and
-- nothing else — each tree where the sim's tree stands (felled trunks, stumps, saplings), each
-- building at its footprint by its stage (stakes, frame, walls, standing), every crop row by its
-- state (bare, hoed, sown, growing to the sim's growth, ripe, stubble), the lanes worn by walking,
-- stores at the granary, woodstore and houses, and every person at their analytic position at
-- the view time (sim/agents/act.lua pos(t)): walking their path, working along a row, carrying
-- what they carry, indoors (hidden) when they are inside.
--
-- Static parts are baked with propgen's instanced chunk writer into chunk meshes (the props
-- material, so light, shadow and seasons match the planet); they are rebuilt when their part of
-- the sim changes (a signature per part). The flora renderer leaves the settlement's square to us
-- (propgen.v3_exclude).
local propgen = require("game.view.propgen")
local kit = require("game.view.modelkit")
local planet = require("game.view.planet")
local state = require("game.view.state")
local vmx = require("game.view.vmx")
local mapm = require("sim.agents.map")
local act = require("sim.agents.act")
local people = require("sim.agents.people")
local clock = require("game.platform.clock")

local M = {}
local floor, min, max, sqrt, atan2, abs, pi = math.floor, math.min, math.max, math.sqrt, math.atan2, math.abs, math.pi
local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local H_AABB = hash("AABB")

M.SETS = {}               -- per settlement view state
M.PEOPLE = 64             -- villager puppets (a settlement of up to 64 people outdoors at once)
M.PERSON_SCALE = 1.25
M.TREE_SPLIT = 4          -- trees in 4 x 4 sub-chunks
M.BUILD_SCALE = 1.5
M.people_pos = {}         -- picking: { id, x, y, z }
M.stats = { people = 0, rebuilt = 0, inside = 0 }
M.t = 0                   -- view time (absolute hours, fractional)

local pack = propgen.pack_rgb
local K = propgen.K

-- Templates ----------------------------------------------------------------------------------
local function build_templates()
  if K.v3row_furrow then return end
  local T = 0.025
  local len = 12 * T
  -- a bare strip of earth (fallow), a hoed row (two ridges), crop (tufts along the row), stubble
  local t = kit.new()
  kit.place(t, kit.box(nil, len, 0.002, T * 0.96, 1, 0.8, 1.0, false), 0, 0.001, 0, 1)
  propgen.register_kind("v3row_bare", t, t, 7)
  t = kit.new()
  kit.place(t, kit.box(nil, len, 0.003, T * 0.98, 2, 0.7, 0.85, false), 0, 0.0015, 0, 1)
  kit.place(t, kit.box(nil, len, 0.006, T * 0.32, 1, 0.75, 1.0, false), 0, 0.004, -T * 0.22, 1)
  kit.place(t, kit.box(nil, len, 0.006, T * 0.32, 1, 0.75, 1.0, false), 0, 0.004, T * 0.22, 1)
  propgen.register_kind("v3row_furrow", t, t, 0, 0)
  t = kit.new()
  kit.place(t, kit.box(nil, len, 0.003, T * 0.98, 2, 0.7, 0.85, false), 0, 0.0015, 0, 1)
  local n = 12
  for k = 0, n - 1 do
    local x = -len / 2 + (k + 0.5) * len / n
    kit.place(t, kit.cone(nil, 0.0, 0.03, 0.0075, 5, 1, 0.65, 1.0, 0.6), x, 0.002, -T * 0.18, 1, 1, 1, k * 1.3)
    kit.place(t, kit.cone(nil, 0.0, 0.026, 0.0068, 5, 1, 0.65, 1.0, 0.6), x + len / n * 0.5, 0.002, T * 0.2, 1, 1, 1, k * 0.7)
  end
  propgen.register_kind("v3row_crop", t, t, 7, 0)
  t = kit.new()
  kit.place(t, kit.box(nil, len, 0.003, T * 0.98, 2, 0.7, 0.85, false), 0, 0.0015, 0, 1)
  for k = 0, n - 1 do
    local x = -len / 2 + (k + 0.5) * len / n
    kit.place(t, kit.box(nil, 0.006, 0.006, T * 0.7, 1, 0.75, 1.0, false), x, 0.004, 0, 1)
  end
  propgen.register_kind("v3row_stubble", t, t, 7, 0)
  -- worn ground (trail / lane): strips joining each worn tile to its worn neighbours (straight
  -- and diagonal lengths), so the paths people wear read as paths, not a grid
  t = kit.new()
  kit.place(t, kit.box(nil, T * 0.62, 0.0014, T * 0.62, 1, 0.85, 1.0, false), 0, 0.0007, 0, 1)
  propgen.register_kind("v3tile", t, t, 0)
  t = kit.new()
  kit.place(t, kit.box(nil, T * 0.6, 0.0014, T * 1.08, 1, 0.85, 1.0, false), 0, 0.0007, T * 0.5, 1)
  propgen.register_kind("v3seg1", t, t, 0)
  t = kit.new()
  kit.place(t, kit.box(nil, T * 0.6, 0.0014, T * 1.5, 1, 0.85, 1.0, false), 0, 0.0007, T * 0.707, 1)
  propgen.register_kind("v3seg2", t, t, 0)
  -- the cottage's two windows, lit (D-082): drawn over its dark panes when the hearth inside burns
  -- and someone in the house is awake after dark
  t = kit.new()
  kit.place(t, kit.quad(nil, 0.0105, 0.0105, 1, 1.0), 0.017, 0.026, 0.0258, 1)
  kit.place(t, kit.quad(nil, 0.0105, 0.0105, 1, 1.0), -0.0358, 0.026, 0.0, 1, 1, 1, -pi / 2)
  propgen.register_kind("v3window", t, t, 0)
end

-- Height grid --------------------------------------------------------------------------------
local function build_heights(V, PL)
  local s = V.s
  local N = s.N
  local hg = {}
  local start = nil
  for i = 1, N * N do
    local x, y = mapm.tile_xy(s, i)
    local ux, uy, uz = mapm.unit(s, x, y)
    local r, v = planet.surface_exact(PL, ux, uy, uz, start)
    start = v
    hg[i] = r
  end
  V.hg = hg
end

-- Surface radius at local (x, y): bilinear over tile centres.
local function height(V, x, y)
  local s = V.s
  local N, T = s.N, s.T
  local fx = x / T + N / 2 - 0.5
  local fy = y / T + N / 2 - 0.5
  local ix, iy = floor(fx), floor(fy)
  local ax, ay = fx - ix, fy - iy
  if ix < 0 then ix, ax = 0, 0 elseif ix > N - 2 then ix, ax = N - 2, 1 end
  if iy < 0 then iy, ay = 0, 0 elseif iy > N - 2 then iy, ay = N - 2, 1 end
  local hg = V.hg
  local i = iy * N + ix + 1
  local h00, h10, h01, h11 = hg[i], hg[i + 1], hg[i + N], hg[i + N + 1]
  return (h00 * (1 - ax) + h10 * ax) * (1 - ay) + (h01 * (1 - ax) + h11 * ax) * ay
end
M.height = height

-- Planet point of local (x, y) at height offset dh.
local function world_pos(V, x, y, dh)
  local ux, uy, uz = mapm.unit(V.s, x, y)
  local r = height(V, x, y)
  if V.R and r < V.R then r = V.R end    -- on the water: the sea's surface (boats, D-084)
  r = r + (dh or 0)
  return ux * r, uy * r, uz * r, ux, uy, uz
end
M.world_pos = world_pos

-- Yaw (props.vp convention) so that the model's +z faces local direction (dx, dy) at (x, y).
local function yaw_z(V, x, y, dx, dy)
  local f = V.s.frame
  local ux, uy, uz = mapm.unit(V.s, x, y)
  local Dx = f[4] * dx + f[7] * dy
  local Dy = f[5] * dx + f[8] * dy
  local Dz = f[6] * dx + f[9] * dy
  local ex, ey, ez, nx, ny, nz = propgen.frame(ux, uy, uz)
  return atan2(Dx * ex + Dy * ey + Dz * ez, Dx * nx + Dy * ny + Dz * nz)
end

-- Yaw (movers: model +x forward, vmx.set_up_yaw) for local direction (dx, dy).
local function yaw_x(V, x, y, dx, dy)
  local f = V.s.frame
  local ux, uy, uz = mapm.unit(V.s, x, y)
  local Dx = f[4] * dx + f[7] * dy
  local Dy = f[5] * dx + f[8] * dy
  local Dz = f[6] * dx + f[9] * dy
  local ex, ey, ez, nx, ny, nz = propgen.frame(ux, uy, uz)
  return -atan2(Dx * nx + Dy * ny + Dz * nz, Dx * ex + Dy * ey + Dz * ez)
end

-- Chunks ------------------------------------------------------------------------------------
local LIST = propgen.new_list()
LIST.v3 = true

local function new_chunk(V, name)
  local id = factory.create(V.factory, vmath.vector3(0, 0, 0))
  local ch = { id = id, url = msg.url(nil, id, "mesh"), res = nil, name = name, sig = -1, empty = true }
  if V.celltex then go.set(ch.url, "texture0", V.celltex) end
  return ch
end

-- Bake the instances added by fill(V) into chunk ch.
local function bake(V, ch, fill)
  local old = propgen.swap_list(LIST)
  LIST.n, LIST.verts, LIST.lod = 0, 0, 1
  fill(V)
  local n, nv = LIST.n, LIST.verts
  if nv == 0 then
    propgen.swap_list(old)
    if not ch.empty then msg.post(ch.url, MSG_DISABLE); ch.empty = true end
    return
  end
  local buf = buffer.create(nv, propgen.chunk_decl())
  local S = { inst = buffer.get_stream(buf, propgen.H_INST), inst2 = buffer.get_stream(buf, propgen.H_INST2),
    inst3 = buffer.get_stream(buf, propgen.H_INST3) }
  propgen.write(buf, S, 0, 1, n)
  propgen.swap_list(old)
  buffer.set_metadata(buf, H_AABB, V.aabb, buffer.VALUE_TYPE_FLOAT32)
  if ch.res then
    resource.set_buffer(ch.res, buf, { transfer_ownership = true })
  else
    V.gen = (V.gen or 0) + 1
    ch.res = resource.create_buffer("/tc/v3_" .. V.s.id .. "_" .. ch.name .. ".bufferc", { buffer = buf })
    go.set(ch.url, "vertices", ch.res)
  end
  if ch.empty then msg.post(ch.url, MSG_ENABLE); ch.empty = false end
  M.stats.rebuilt = M.stats.rebuilt + 1
end

local function add(V, kind, x, y, dh, yaw, sc, shs, c1, c2, c3, rnd)
  local px, py, pz, ux, uy, uz = world_pos(V, x, y, dh)
  local cell = V.s.cell_of[mapm.tile_of(V.s, x, y)] or V.s.cell
  propgen.add(kind, px, py, pz, ux, uy, uz, yaw, sc, shs or 1, c1, c2, c3 or 0, cell, rnd)
end

-- Trees of sub-chunk (bx, by).
local function tree_kind(V, k)
  local s = V.s
  local tr = s.trees
  local W = V.W
  local cell = s.cell_of[tr.tile[k]]
  local T = W.temp_static
  local tmean = (T and T.base and (T.base[cell] + T.lapse[cell] + (W.G.g_temp or 0))) or (W.F.temp[cell] or 10)
  local u = ((k * 0.6180339887) % 1)
  local stand = ((floor(tr.x[k] * 6) * 0.37 + floor(tr.y[k] * 6) * 0.61) % 1)
  local kind = propgen.pick_tree_impl(tmean, W.F.ice[cell] or 0, W.F.biome[cell] or 0, W.F.soil_water[cell] or 0.5, false, u, stand)
  return kind, u, stand
end

local function fill_trees(bx, by)
  return function(V)
    local s = V.s
    local tr = s.trees
    local span = s.N / M.TREE_SPLIT
    local S = propgen.SCALE
    for k = 1, tr.n do
      local st = tr.st[k]
      if st ~= 0 then
        local ix, iy = mapm.tile_ixy(s, tr.tile[k])
        if floor((ix - 1) / span) == bx and floor((iy - 1) / span) == by then
          local x, y = tr.x[k], tr.y[k]
          local kind, u, stand = tree_kind(V, k)
          local yaw = u * 6.283 + k
          if st == 1 then
            local size = tr.size[k]
            local kd = kind
            if size < 0.3 then kd = (kind == K.spruce1 or kind == K.spruce2 or kind == K.fir1) and K.sapling_c or K.sapling_b end
            local pal = propgen.PAL[kd]
            local col = pal and pal[floor((u * 0.55 + stand * 0.45) * 0.9999 * #pal) + 1] or { 0.4, 0.6, 0.3 }
            local jit = 0.88 + ((k * 0.37) % 1) * 0.24
            local c1 = pack(col[1] * jit, col[2] * jit, col[3] * jit)
            local bark = propgen.BARK[kd]
            local c2 = bark and pack(bark[1], bark[2], bark[3]) or c1
            local sc = (0.38 + 0.55 * size) * S
            add(V, kd, x, y, 0, yaw, sc, 1, c1, c2, 0, u)
          elseif st == 2 then
            -- the felled trunk lies where it fell (until its logs are carried off)
            local bark = propgen.BARK[kind] or { 0.45, 0.31, 0.2 }
            add(V, K.deadfall, x, y, 0, yaw, (0.6 + 0.5 * tr.size[k]) * S, 1, pack(0.42, 0.36, 0.28),
              pack(bark[1], bark[2], bark[3]), pack(0.42, 0.6, 0.3), u)
            add(V, K.stump, x, y, 0, yaw, 0.9 * S, 1, pack(bark[1], bark[2], bark[3]), pack(bark[1], bark[2], bark[3]), 0, u)
          else
            local bark = propgen.BARK[kind] or { 0.45, 0.31, 0.2 }
            add(V, K.stump, x, y, 0, yaw, 0.9 * S, 1, pack(bark[1], bark[2], bark[3]), pack(bark[1], bark[2], bark[3]), 0, u)
          end
        end
      end
    end
  end
end

local function tree_sig(V, bx, by)
  local s = V.s
  local tr = s.trees
  local span = s.N / M.TREE_SPLIT
  local h = 7
  for k = 1, tr.n do
    local ix, iy = mapm.tile_ixy(s, tr.tile[k])
    if floor((ix - 1) / span) == bx and floor((iy - 1) / span) == by then
      h = (h * 31 + tr.st[k] * 5 + floor(tr.size[k] * 6) + (tr.st[k] == 2 and tr.logs[k] > 0 and 3 or 0)) % 1000000007
    end
  end
  return h
end

-- Buildings, stores and the square.
local STYLE_ROOF = { { 0.62, 0.36, 0.24 }, { 0.78, 0.66, 0.40 }, { 0.42, 0.40, 0.44 }, { 0.70, 0.30, 0.22 } }
local STYLE_WALL = { { 0.90, 0.85, 0.74 }, { 0.80, 0.70, 0.55 }, { 0.70, 0.66, 0.60 }, { 0.92, 0.88, 0.80 } }

local function fill_buildings(V)
  local s = V.s
  local S = M.BUILD_SCALE
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    local dx, dy = b.dx - b.cx, b.dy - b.cy
    local yaw = yaw_z(V, b.cx, b.cy, dx, dy)
    local key = b.id * 7919 + s.key
    local roof = STYLE_ROOF[key % #STYLE_ROOF + 1]
    local wall = STYLE_WALL[floor(key / 5) % #STYLE_WALL + 1]
    local c1, c2 = pack(roof[1], roof[2], roof[3]), pack(wall[1], wall[2], wall[3])
    if b.state == "site" then
      local pg = (b.work_done / b.work_need) * 0.75 + min(1, b.logs_have / max(1, b.logs_need)) * 0.25
      local kind = pg < 0.3 and K.stakes or (pg < 0.65 and K.frame or K.walls)
      add(V, kind, b.cx, b.cy, 0, yaw, S, 1, c1, c2, 0)
      if b.logs_have > 0 then
        add(V, K.logs, b.cx + dx * 1.6 + 0.02, b.cy + dy * 1.6, 0, yaw + 0.6, 0.8 * min(1.4, 0.5 + b.logs_have / 10), 1,
          pack(0.55, 0.40, 0.26), pack(0.45, 0.31, 0.2), 0)
      end
    elseif b.state == "standing" then
      if b.kind == "house" then
        add(V, K.cottage, b.cx, b.cy, 0, yaw, S, 1, c1, c2, 0)
        if V.lit and V.lit[b.id] then
          add(V, K.v3window, b.cx, b.cy, 0, yaw, S, 1, pack(1.0, 0.74, 0.38), 0, 0, 2.5)
        end
        local logs = b.stock.logs or 0
        if logs > 0.5 then
          -- the woodpile beside the house grows and shrinks with its logs
          local sx, sy = b.cx - dy * 1.2 + dx * 0.2, b.cy + dx * 1.2 + dy * 0.2
          add(V, K.woodpile, sx, sy, 0, yaw, 0.55 * min(1.3, 0.45 + logs / 8), 1, pack(0.55, 0.40, 0.26), pack(0.45, 0.31, 0.2), 0)
        end
      elseif b.kind == "granary" then
        add(V, K.granary, b.cx, b.cy, 0, yaw, S * 1.15, 1, pack(0.78, 0.66, 0.40), c2, 0)
        local sh = b.stock.sheaves or 0
        for q = 1, min(6, floor(sh / 20 + 0.999)) do
          add(V, K.stook, b.cx + dx * 1.4 + (q - 3.5) * 0.018, b.cy + dy * 1.4 - 0.03, 0, q, 0.7, 1, pack(0.86, 0.72, 0.38), pack(0.8, 0.66, 0.34), 0)
        end
      elseif b.kind == "woodstore" then
        add(V, K.woodshed, b.cx, b.cy, 0, yaw, S, 1, c1, c2, 0)
        local logs = b.stock.logs or 0
        local piles = min(6, floor(logs / 8 + 0.999))
        for q = 1, piles do
          add(V, K.logs, b.cx + dx * 1.5 + (q - 3.5) * 0.022, b.cy + dy * 1.5, 0, yaw, 0.75, 1, pack(0.55, 0.40, 0.26), pack(0.45, 0.31, 0.2), 0)
        end
      elseif b.kind == "well" then
        add(V, K.well, b.cx, b.cy, 0, yaw, 1.0, 1, pack(0.62, 0.36, 0.24), 0, 0)
      elseif b.kind == "fold" then
        -- a sheepfold (D-089): stakes round it, the haystack by its gate
        local T = s.T
        for q = 0, b.w * 2 - 1 do
          local f = q / (b.w * 2)
          local x0, y0 = b.cx - b.w * T * 0.5, b.cy - b.h * T * 0.5
          local L = b.w * T
          for side = 0, 3 do
            local x, y
            if side == 0 then x, y = x0 + f * L, y0
            elseif side == 1 then x, y = x0 + L, y0 + f * L
            elseif side == 2 then x, y = x0 + L - f * L, y0 + L
            else x, y = x0, y0 + L - f * L end
            if not (side == 0 and f > 0.4 and f < 0.6) then
              add(V, K.stakes, x, y, 0, q + side, 0.22, 1, 0, 0, 0)
            end
          end
        end
        local hay = s.flock and s.flock.hay or 0
        if hay > 5 then
          add(V, K.stook, b.dx + 0.05, b.dy - 0.01, 0, 0.7, 0.9 * min(2.2, 0.6 + hay / 120), 1,
            pack(0.80, 0.72, 0.42), pack(0.74, 0.66, 0.36), 0)
        end
      elseif b.kind == "dock" then
        -- a timber pier from the shore out over the water (D-084)
        add(V, K.pier, (b.cx + b.mx) * 0.5, (b.cy + b.my) * 0.5, 0, yaw_z(V, b.cx, b.cy, b.mx - b.cx, b.my - b.cy), 0.8, 1,
          pack(0.55, 0.40, 0.26), pack(0.45, 0.31, 0.2), 0)
      end
    end
  end
end

local function bld_sig(V)
  local s = V.s
  local h = 11 + (V.lit_sig or 0)
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    local pg = b.state == "site" and floor(((b.work_done / b.work_need) * 0.75 + min(1, b.logs_have / max(1, b.logs_need)) * 0.25) * 20) or 99
    h = (h * 31 + b.id * 3 + pg + floor((b.stock.logs or 0) / 2) * 7 + floor((b.stock.sheaves or 0) / 20) * 13
      + (b.state == "standing" and 1000 or 0)) % 1000000007
  end
  if s.flock then h = (h * 17 + floor((s.flock.hay or 0) / 30)) % 1000000007 end
  return h
end

-- Crop rows.
local CROP_GREEN = { 0.40, 0.66, 0.26 }
local CROP_GOLD = { 0.88, 0.74, 0.32 }
local EARTH = { 0.47, 0.34, 0.22 }
local EARTH_DARK = { 0.38, 0.26, 0.17 }
local FALLOW = { 0.52, 0.56, 0.30 }

local function fill_rows(V)
  local s = V.s
  local T = s.T
  for k = 1, #s.plots do
    local pl = s.plots[k]
    local yaw = yaw_z(V, (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5, 0, 1)
    for r = 1, pl.rows do
      local x0, y0, x1, y1 = act.plot_row_line(s, pl, r)
      local cx, cy = (x0 + x1) * 0.5, y0
      local rs = pl.row_st[r]
      local e1 = pack(EARTH[1], EARTH[2], EARTH[3])
      local e2 = pack(EARTH_DARK[1], EARTH_DARK[2], EARTH_DARK[3])
      if rs == 0 or (rs == 3 and pl.stage == "rest") then
        -- bare ground, or a resting field grassing over (its weeds) after the stubble
        local w = rs == 0 and 1 or min(1, 0.35 + (pl.weeds or 0) * 1.2)
        local f1 = { CROP_GOLD[1] + (FALLOW[1] - CROP_GOLD[1]) * w, CROP_GOLD[2] + (FALLOW[2] - CROP_GOLD[2]) * w,
          CROP_GOLD[3] + (FALLOW[3] - CROP_GOLD[3]) * w }
        add(V, K.v3row_bare, cx, cy, 0, yaw, 1, 1, pack(f1[1] * 0.8, f1[2] * 0.75, f1[3] * 0.7), 0, 0)
      elseif rs == 1 then
        add(V, K.v3row_furrow, cx, cy, 0, yaw, 1, 1, e1, e2, 0)
      elseif rs == 2 and pl.crop == "flax" then
        -- flax (D-088): green, a blue sea of flowers, then brown seed bolls
        local g = pl.stage == "ripe" and 1 or min(1, pl.growth or 0)
        local c
        if pl.stage == "ripe" then c = { 0.62, 0.52, 0.30 }
        elseif g > 0.45 and g < 0.8 then c = { 0.40, 0.52, 0.86 }
        else c = { 0.36, 0.60, 0.30 } end
        add(V, K.v3row_crop, cx, cy, 0, yaw, 1, 0.12 + 0.75 * g, pack(c[1], c[2], c[3]), e2, 0)
      elseif rs == 3 and pl.stage == "retting" then
        add(V, K.v3row_stubble, cx, cy, 0, yaw, 1, 1, pack(0.58, 0.56, 0.50), e2, 0)
      elseif rs == 2 then
        local g = pl.stage == "ripe" and 1 or min(1, pl.growth or 0)
        local ripe = pl.stage == "ripe"
        local c = ripe and CROP_GOLD or {
          CROP_GREEN[1] + (CROP_GOLD[1] - CROP_GREEN[1]) * max(0, g - 0.7) / 0.3,
          CROP_GREEN[2] + (CROP_GOLD[2] - CROP_GREEN[2]) * max(0, g - 0.7) / 0.3,
          CROP_GREEN[3] + (CROP_GOLD[3] - CROP_GREEN[3]) * max(0, g - 0.7) / 0.3 }
        local wd = pl.weeds or 0
        add(V, K.v3row_crop, cx, cy, 0, yaw, 1, 0.12 + 0.88 * g, pack(c[1] * (1 - wd * 0.2), c[2], c[3] * (1 - wd * 0.3)), e2, 0)
      else
        add(V, K.v3row_stubble, cx, cy, 0, yaw, 1, 1, pack(0.80, 0.70, 0.42), e2, 0)
      end
    end
    if (pl.flax or 0) > 0.3 then
      for q = 1, min(4, floor(pl.flax / 3 + 0.999)) do
        add(V, K.stook, pl.stx + (q - 2.5) * 0.02, pl.sty, 0, q * 1.3, 0.6, 1, pack(0.66, 0.62, 0.52), pack(0.6, 0.56, 0.46), 0)
      end
    end
    if pl.sheaves > 0.5 then
      for q = 1, min(5, floor(pl.sheaves / 12 + 0.999)) do
        add(V, K.stook, pl.stx + (q - 3) * 0.02, pl.sty, 0, q * 1.7, 0.75, 1, pack(0.86, 0.72, 0.38), pack(0.8, 0.66, 0.34), 0)
      end
    end
  end
  -- new field ground being cleared: staked corners
  local cl = s.clearing
  if cl then
    local P = 12
    for q = 0, 3 do
      local ix = cl.ix0 + (q % 2) * (P - 1)
      local iy = cl.iy0 + floor(q / 2) * (P - 1)
      local x, y = mapm.ixy_xy(s, ix, iy)
      add(V, K.stakes, x, y, 0, q, 0.35, 1, 0, 0, 0)
    end
  end
end

local function rows_sig(V)
  local s = V.s
  local h = 13
  for k = 1, #s.plots do
    local pl = s.plots[k]
    for r = 1, pl.rows do h = (h * 7 + pl.row_st[r] + 1) % 1000000007 end
    h = (h * 31 + floor((pl.growth or 0) * 24) + (pl.stage == "ripe" and 50 or 0) + (pl.stage == "rest" and 70 or 0)
      + floor(pl.sheaves / 12) * 3 + floor((pl.weeds or 0) * 4) + (pl.crop == "flax" and 90 or 0)
      + (pl.stage == "retting" and 110 or 0) + floor((pl.flax or 0) / 3) * 5) % 1000000007
  end
  if s.clearing then h = h + 777 end
  return h
end

-- Lanes and trails.
local function fill_ground(V)
  local s = V.s
  local N = s.N
  local lane = s.lane
  local c_lane = pack(0.58, 0.48, 0.34)
  local c_trail = pack(0.60, 0.58, 0.40)
  local DIRS = { { 1, 0, K.v3seg1 }, { 0, 1, K.v3seg1 }, { 1, 1, K.v3seg2 }, { -1, 1, K.v3seg2 } }
  for i = 1, N * N do
    local l = lane[i]
    if l > 0 and s.occ[i] == 0 then
      local x, y = mapm.tile_xy(s, i)
      local ix, iy = mapm.tile_ixy(s, i)
      local c = l == 2 and c_lane or c_trail
      local dh = 0.0002 * l
      add(V, K.v3tile, x, y, dh, 0, 1, 1, c, 0, 0)
      for d = 1, 4 do
        local D = DIRS[d]
        local jx, jy = ix + D[1], iy + D[2]
        if jx >= 1 and jy >= 1 and jx <= N and jy <= N then
          local j = (jy - 1) * N + jx
          local lj = lane[j]
          if lj > 0 and s.occ[j] == 0 then
            -- skip a diagonal where both orthogonal steps are worn (they already join)
            local skip = false
            if D[1] ~= 0 and D[2] ~= 0 then
              skip = lane[(iy - 1) * N + jx] > 0 and lane[(jy - 1) * N + ix] > 0
            end
            if not skip then
              local cc = (l == 2 and lj == 2) and c_lane or c_trail
              add(V, D[3], x, y, dh, yaw_z(V, x, y, D[1], D[2]), 1, 1, cc, 0, 0)
            end
          end
        end
      end
    end
  end
end

local function ground_sig(V)
  local s = V.s
  local h = 17
  local lane = s.lane
  for i = 1, s.N * s.N do
    local l = lane[i]
    if l > 0 then h = (h * 3 + i * l) % 1000000007 end
  end
  return h
end

-- People ------------------------------------------------------------------------------------
local ROLE_TUNIC = {
  farmer = { 0.80, 0.68, 0.36 }, woodcutter = { 0.30, 0.50, 0.30 }, builder = { 0.72, 0.40, 0.32 }, fisher = { 0.34, 0.44, 0.58 },
  [""] = { 0.62, 0.58, 0.70 },
}
local SKIN = { { 0.98, 0.84, 0.70 }, { 0.87, 0.67, 0.50 }, { 0.64, 0.46, 0.33 } }
local HAIRS = { { 0.33, 0.24, 0.17 }, { 0.20, 0.15, 0.12 }, { 0.62, 0.42, 0.22 }, { 0.85, 0.72, 0.42 } }

local function make_buffer(kind, c1, c2, c3, scale, still)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, scale or 1, c3, true, scale, still)
  return buf
end

-- One walking and one still resource per person look (made when first seen).
local RES = {}
local function person_res(p, carry, still)
  local key = p.id * 10 + (carry == "logs" and 1 or ((carry == "grain" or carry == "sheaves" or carry == "food" or carry == "seed") and 2 or 0)) + (still and 5 or 0)
  local r = RES[key]
  if r then return r end
  local st = p._stage or "adult"
  local tunic = ROLE_TUNIC[p.role] or ROLE_TUNIC[""]
  if st == "child" or st == "apprentice" then tunic = { 0.86, 0.56, 0.62 } end
  if p.sex == "f" and st ~= "child" then tunic = { tunic[1] * 0.85 + 0.12, tunic[2] * 0.8, tunic[3] * 0.9 + 0.08 } end
  local skin = SKIN[(p.key % 3) + 1]
  local hair = HAIRS[(floor(p.key / 3) % 4) + 1]
  if st == "elder" then hair = { 0.86, 0.86, 0.84 } end
  local kind = "person"
  if carry == "logs" then kind = "person_logs" elseif carry ~= "" and carry ~= "water" then kind = "person_sack"
  elseif p.role == "farmer" and st == "adult" then kind = "person_straw"
  elseif p.role == "woodcutter" or p.role == "builder" then kind = "person_cap" end
  r = resource.create_buffer("/tc/v3p_" .. key .. ".bufferc", { buffer = make_buffer(kind, tunic, skin, hair, M.PERSON_SCALE, still) })
  RES[key] = r
  return r
end

local function make_pool(V, n)
  V.pool = {}
  for i = 1, n do
    local id = factory.create(V.person_factory, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    msg.post(url, MSG_DISABLE)
    V.pool[i] = { id = id, url = url, on = false, res = nil, pid = 0 }
  end
end

local ROT = vmath.quat()
local POS = vmath.vector3()
local WORK_ANIM = { till = true, sow = true, harvest = true, chop = true, build = true, tend = true, grub = true,
  thresh = true, forage = true, fill = true }

local function update_people(V, t, zoom)
  local s = V.s
  local Vp = V.W.S.v3.people
  local pool = V.pool
  local n = 0
  local pos = M.people_pos
  local np = 0
  local inside = 0
  for i = 1, #s.members do
    local p = Vp[s.members[i]]
    if p and p.alive then
      p._stage = people.stage(p, t)
      local hidden = p.inside ~= 0 and p.act ~= "walk"
      if p.act == "carried" then
        local c = Vp[p.carrier or 0]
        hidden = true    -- (babes in arms are not drawn yet)
      end
      if hidden then
        inside = inside + 1
      elseif n < #pool then
        n = n + 1
        local pp = pool[n]
        -- the sim has been advanced to M.t; positions are exact for any t within the action
        local x, y, dx, dy = act.pos(p, t)
        local moving = p.mv ~= nil and p.spd > 0
        local still = not moving and not WORK_ANIM[p.act]
        local res = person_res(p, p.carry, still)
        if pp.res ~= res then go.set(pp.url, "vertices", res); pp.res = res end
        local lift = 0
        if WORK_ANIM[p.act] and not moving then
          -- a working bob (stooping to the hoe, swinging the axe): from the sim's action, not invented
          lift = -0.004 * abs(math.sin(state.time * (p.act == "chop" and 7 or 4) + p.id))
        end
        local px, py, pz, ux, uy, uz = world_pos(V, x, y, lift)
        POS.x, POS.y, POS.z = px, py, pz
        go.set_position(POS, pp.id)
        local fx, fy = dx, dy
        if not moving and p.act ~= "row" then fx, fy = p.fx, p.fy end
        local l = sqrt(fx * fx + fy * fy)
        if l > 1e-6 then pp.yaw = yaw_x(V, x, y, fx / l, fy / l) end
        go.set_rotation(vmx.set_up_yaw(ROT, ux, uy, uz, pp.yaw or 0), pp.id)
        local sc = (p._stage == "child") and 0.62 or ((p._stage == "apprentice") and 0.85 or 1)
        go.set_scale(sc, pp.id)
        if not pp.on then msg.post(pp.url, MSG_ENABLE); pp.on = true end
        pp.pid = p.id
        np = np + 1
        local e = pos[np]
        if not e then e = {}; pos[np] = e end
        e.id, e.x, e.y, e.z = p.id, px, py, pz
      end
    end
  end
  for i = n + 1, #pool do
    local pp = pool[i]
    if pp.on then msg.post(pp.url, MSG_DISABLE); pp.on = false; pp.pid = 0 end
  end
  for i = np + 1, #pos do pos[i] = nil end
  M.stats.people, M.stats.inside = n, inside
end

-- Villagers (skinned, instanced: assets/characters, docs/v3/20) ----------------------------------
-- Part ids as in tools/blender/build_villager.py (PARTS); the mask selects what a person wears and
-- holds; palette slots 0-4 are theirs.
local PART = {}
for i, n in ipairs({ "body", "head", "eyes", "legs_trousers", "legs_bare", "feet",
  "hair_short", "hair_long", "hair_bun", "beard",
  "hat_straw", "hat_hood", "hat_cap", "headscarf",
  "tunic", "skirt", "dress", "apron", "cloak", "belt",
  "axe", "hoe", "sickle", "hammer", "basket", "sack", "bucket", "log", "rod", "spear", "shield", "flail", "babe" }) do
  PART[n] = i - 1
end
M.PART = PART

-- what they hold for each action (the sim's action, not invented)
local TOOL = { till = "hoe", grub = "hoe", chop = "axe", harvest = "sickle", build = "hammer", thresh = "flail",
  forage = "basket", fill = "bucket", tend = nil, sow = "basket", repair = "hammer", fish = "rod", mend_boat = "hammer",
  dress = "flail" }
local CARRY_PART = { logs = "log", grain = "sack", sheaves = "sack", food = "sack", seed = "basket", water = "bucket", fish = "basket",
  flax = "sack" }
-- animation for each action: { clip, rate (at a game minute per second), moving clip }
local ANIM = {
  walk = { "walk" }, till = { "swing", 0.7 }, grub = { "kneel_work", 0.9 }, chop = { "swing", 0.9 },
  harvest = { "crouch_walk", 0.8 }, sow = { "walk_slow", 0.8 }, tend = { "crouch_walk", 0.6 },
  build = { "kneel_work", 1.0 }, thresh = { "swing", 0.8 }, forage = { "crouch_walk", 0.5 },
  fill = { "interact", 1 }, pick = { "pickup", 1.4 }, drop = { "pickup", 1.4 }, talk = { "talk", 1 },
  play = { "jump", 1 }, lunch = { "sit", 1 }, wait = { "idle", 1 }, idle = { "idle", 1 }, shelter = { "idle", 1 },
  sit = { "sit", 1 }, nurse = { "sit", 0.8 }, craft = { "kneel_work", 0.6 }, repair = { "kneel_work", 1.0 },
  hand = { "pickup", 1.2 },
  row = { "sit", 1 }, fish = { "sit", 0.7 }, board = { "pickup", 1.2 }, land = { "pickup", 1.2 },
  mend_boat = { "kneel_work", 1.0 }, smoke = { "kneel_work", 0.6 },
  spin = { "sit", 0.9 }, weave = { "sit", 0.8 }, sew = { "sit", 0.7 }, mend_clothes = { "sit", 0.7 },
  dress = { "swing", 0.7 }, haul_flax = { "walk" },
}

local function packc(c) return pack(c[1], c[2], c[3]) end
local SKINS = { { 0.98, 0.84, 0.70 }, { 0.92, 0.74, 0.58 }, { 0.80, 0.60, 0.44 }, { 0.62, 0.45, 0.32 } }
local HAIRC = { { 0.30, 0.20, 0.12 }, { 0.16, 0.12, 0.10 }, { 0.55, 0.36, 0.18 }, { 0.82, 0.68, 0.38 }, { 0.62, 0.30, 0.16 } }
local TUNIC = {
  farmer = { { 0.78, 0.66, 0.40 }, { 0.62, 0.55, 0.36 }, { 0.70, 0.50, 0.32 } },
  woodcutter = { { 0.30, 0.48, 0.30 }, { 0.36, 0.42, 0.28 }, { 0.26, 0.40, 0.36 } },
  builder = { { 0.70, 0.40, 0.30 }, { 0.62, 0.36, 0.28 }, { 0.55, 0.42, 0.36 } },
  fisher = { { 0.34, 0.44, 0.58 }, { 0.28, 0.38, 0.48 }, { 0.42, 0.50, 0.56 } },
  [""] = { { 0.60, 0.56, 0.70 }, { 0.52, 0.60, 0.66 }, { 0.70, 0.52, 0.56 } },
}
local LOWER = { { 0.40, 0.34, 0.26 }, { 0.32, 0.32, 0.38 }, { 0.46, 0.40, 0.30 } }
local DRESS = { { 0.62, 0.30, 0.32 }, { 0.40, 0.42, 0.60 }, { 0.50, 0.55, 0.36 }, { 0.66, 0.50, 0.30 }, { 0.46, 0.34, 0.50 } }
local ACCENT = { { 0.55, 0.28, 0.22 }, { 0.30, 0.36, 0.26 }, { 0.42, 0.32, 0.26 }, { 0.80, 0.74, 0.60 }, { 0.36, 0.40, 0.55 } }

local function frac(x) return x - floor(x) end

-- A person's look: what they always wear (from who they are), packed for the shader.
local function look(p, stage)
  local u = p.key / 4294967296
  local function r(k) return frac(u * (7.13 + k * 3.31) + k * 0.618) end
  local mask = { body = true, head = true, eyes = true, feet = true }
  local female = p.sex == "f"
  local adult = stage == "adult" or stage == "elder"
  if female then
    if stage == "child" then mask.skirt = true; mask.legs_bare = true; mask.tunic = true
    else mask.dress = true; mask.legs_bare = true end
    if r(1) < 0.5 then mask.hair_long = true else mask.hair_bun = true end
    if adult and r(2) < 0.45 then mask.headscarf = true end
    if adult and p.role == "farmer" and r(3) < 0.55 then mask.apron = true end
  else
    mask.legs_trousers = true
    mask.tunic = true
    mask.hair_short = true
    if adult then mask.belt = true end
    if adult and r(4) < 0.4 then mask.beard = true end
  end
  if adult and not mask.headscarf then
    if p.role == "farmer" and r(5) < 0.7 then mask.hat_straw = true
    elseif (p.role == "woodcutter" or p.role == "builder") and r(6) < 0.75 then mask.hat_cap = true end
  end
  if stage == "elder" then
    if r(7) < 0.6 then mask.cloak = true end
    if r(8) < 0.5 and not mask.headscarf then mask.hat_hood = true; mask.hat_straw = nil; mask.hat_cap = nil end
  end
  local skin = SKINS[p.key % #SKINS + 1]
  local hair = HAIRC[floor(p.key / 7) % #HAIRC + 1]
  if stage == "elder" then hair = { 0.82, 0.82, 0.80 } end
  local tl = TUNIC[p.role] or TUNIC[""]
  local upper = tl[floor(p.key / 11) % #tl + 1]
  if female and not (stage == "child") then upper = DRESS[floor(p.key / 13) % #DRESS + 1] end
  if stage == "child" or stage == "apprentice" then upper = DRESS[floor(p.key / 17) % #DRESS + 1] end
  local lower = female and DRESS[floor(p.key / 19) % #DRESS + 1] or LOWER[floor(p.key / 23) % #LOWER + 1]
  local accent = ACCENT[floor(p.key / 29) % #ACCENT + 1]
  return mask, vmath.vector4(packc(skin), packc(hair), packc(upper), packc(lower)), packc(accent)
end

local function mask_bits(mask, extra1, extra2)
  local lo, hi = 0, 0
  local function set(n)
    local id = PART[n]
    if not id then return end
    if id < 20 then lo = lo + 2 ^ id else hi = hi + 2 ^ (id - 20) end
  end
  for n, on in pairs(mask) do if on then set(n) end end
  if extra1 then set(extra1) end
  if extra2 and extra2 ~= extra1 then set(extra2) end
  return lo, hi
end

local H_PAL, H_PARTS = hash("u_pal"), hash("u_parts")
local LODS = { "#lod0", "#lod1", "#lod2" }
M.VILLAGER_SCALE = 0.04

local function make_villagers(V, n)
  V.vpool = {}
  for i = 1, n do
    local id = factory.create(V.villager_factory, vmath.vector3(0, 0, 0))
    local e = { id = id, on = false, lod = 0, urls = {}, pid = 0, anim = nil, rate = 0, key = -1 }
    for k = 1, 3 do
      e.urls[k] = msg.url(nil, id, LODS[k]:sub(2))
      msg.post(e.urls[k], MSG_DISABLE)
    end
    V.vpool[i] = e
  end
end

local VPOS = vmath.vector3()
local VROT = vmath.quat()
local P4 = vmath.vector4()

local function update_villagers(V, t, zoom)
  local s = V.s
  local Vp = V.W.S.v3.people
  local pool = V.vpool
  local n = 0
  local pos = M.people_pos
  local np = 0
  local inside = 0
  local lod = zoom >= 1.42 and 1 or (zoom >= 1.08 and 2 or 3)
  if M.LOD0 then lod = 1 end
  local show = zoom >= 0.75 and not M.skipping
  local kspeed = M.game_min_per_s or 1
  -- who has a babe in arms (an infant carried by them)
  local arms = V.arms or {}
  V.arms = arms
  for k in pairs(arms) do arms[k] = nil end
  for i = 1, #s.members do
    local q = Vp[s.members[i]]
    if q and q.alive and q.act == "carried" and (q.carrier or 0) ~= 0 then arms[q.carrier] = true end
  end
  for i = 1, #s.members do
    local p = Vp[s.members[i]]
    if p and p.alive then
      local stage = people.stage(p, t)
      local hidden = (p.inside ~= 0 and p.act ~= "walk") or p.act == "carried"
      if hidden then
        inside = inside + 1
      elseif show and n < #pool then
        n = n + 1
        local e = pool[n]
        local x, y, dx, dy = act.pos(p, t)
        local moving = p.mv ~= nil and p.spd > 0 and p.act == "walk"
        -- look + what they hold now
        local carry = p.carry ~= "" and CARRY_PART[p.carry] or nil
        local tool = TOOL[p.act]
        if not carry and arms[p.id] then carry = "babe"; tool = nil end
        local key = p.id * 1000 + (PART[carry or ""] or 99) * 31 + (PART[tool or ""] or 98) + (stage == "child" and 7 or 0)
          + (stage == "elder" and 13 or 0)
        if e.key ~= key or e.pid ~= p.id then
          local mask, pal, accent = look(p, stage)
          local lo, hi = mask_bits(mask, carry, tool)
          if not M.NOATTR then
            for k = 1, 3 do
              go.set(e.urls[k], H_PAL, pal)
              P4.x, P4.y, P4.z, P4.w = accent, lo, hi, 0
              go.set(e.urls[k], H_PARTS, P4)
            end
          end
          e.key = key
        end
        -- level of detail
        if (e.lod ~= lod or not e.on) and not M.NOENABLE then
          for k = 1, 3 do msg.post(e.urls[k], k == lod and MSG_ENABLE or MSG_DISABLE) end
          if e.lod ~= lod then e.anim = nil end
          e.lod = lod
          e.on = true
        end
        -- animation from the action
        local clip, rate
        -- (each person keeps their own tempo: nobody swings or steps in time with the rest)
        local tempo = 0.88 + 0.24 * frac(p.id * 0.6180339887)
        if moving then
          clip = carry and "carry_walk" or (stage == "elder" and "walk_slow" or "walk")
          if p.act == "walk" and stage == "child" and (p.objk == "play" or p.objk == "follow") then clip = "run" end
          rate = min(3, 1.07 * kspeed * (p.spd / 3.2))
        else
          local a = ANIM[p.act] or ANIM.idle
          clip = a[1]
          if carry and clip == "idle" then clip = "carry_idle" end
          if p.act == "play" then clip = (p.id % 3 == 0) and "dance" or "jump" end
          if p.act == "talk" then
            -- talkers listen as much as they talk: turns of a few game minutes
            local turn = floor((t * 60 + p.id * 7.3) / (4 + p.id % 5))
            if turn % 3 == 0 then clip = "idle" end
          end
          rate = min(2.5, (a[2] or 1) * (0.6 + 0.4 * kspeed) * tempo)
        end
        if M.NOANIM then
          e.anim = clip
        elseif e.anim ~= clip then
          model.play_anim(e.urls[lod], clip, go.PLAYBACK_LOOP_FORWARD, { blend_duration = 0.25, playback_rate = rate,
            offset = frac(p.id * 0.37) })
          e.anim, e.rate = clip, rate
        elseif abs(e.rate - rate) > 0.05 then
          go.set(e.urls[lod], "playback_rate", rate)
          e.rate = rate
        end
        -- place on the ground, facing their way
        local px, py, pz, ux, uy, uz = world_pos(V, x, y, 0)
        VPOS.x, VPOS.y, VPOS.z = px, py, pz
        go.set_position(VPOS, e.id)
        local fx, fy = dx, dy
        if not moving then fx, fy = p.fx, p.fy end
        local l = sqrt(fx * fx + fy * fy)
        if l > 1e-6 then e.yaw = yaw_x(V, x, y, fx / l, fy / l) + pi * 0.5 end
        go.set_rotation(vmx.set_up_yaw(VROT, ux, uy, uz, e.yaw or 0), e.id)
        local sc = M.VILLAGER_SCALE * ((stage == "child") and 0.62 or ((stage == "apprentice") and 0.86 or 1))
        if p.sex == "f" and stage ~= "child" then sc = sc * 0.95 end
        go.set_scale(sc, e.id)
        e.pid = p.id
        np = np + 1
        local q = pos[np]
        if not q then q = {}; pos[np] = q end
        q.id, q.x, q.y, q.z = p.id, px, py, pz
      end
    end
  end
  for i = n + 1, #pool do
    local e = pool[i]
    if e.on then
      for k = 1, 3 do msg.post(e.urls[k], MSG_DISABLE) end
      e.on = false
      e.pid = 0
      e.anim = nil
    end
  end
  for i = np + 1, #pos do pos[i] = nil end
  M.stats.people, M.stats.inside = n, inside
end

-- Public ------------------------------------------------------------------------------------

-- Before props are baked: the flora leaves the settlements' squares to this renderer.
function M.prepare(W)
  M.SETS = {}
  if not W.v3 or not W.S.v3 then propgen.v3_exclude = nil; return end
  local ex = {}
  for _, s in ipairs(W.S.v3.settlements) do
    local f = s.frame
    local half = s.N * s.T * 0.5
    ex[#ex + 1] = { f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], half, mapm.R, math.cos(half * 1.5 / mapm.R) }
  end
  propgen.v3_exclude = ex
end

function M.init(W, PL, urls, celltex)
  M.W = W
  if not W.v3 or not W.S.v3 then return end
  build_templates()
  for _, s in ipairs(W.S.v3.settlements) do
    local V = { W = W, s = s, factory = urls.chunk_factory, person_factory = urls.person_factory, celltex = celltex,
      boat_factory = urls.boat_factory, R = PL.R, deer_factory = urls.deer_factory,
      villager_factory = urls.villager_factory }
    build_heights(V, PL)
    -- an AABB around the whole square (planet space)
    local x0, y0, z0, x1, y1, z1 = 1e9, 1e9, 1e9, -1e9, -1e9, -1e9
    local half = s.N * s.T * 0.5
    for _, c in ipairs({ { -half, -half }, { half, -half }, { -half, half }, { half, half }, { 0, 0 } }) do
      local px, py, pz = world_pos(V, c[1], c[2], 0)
      x0, y0, z0 = min(x0, px), min(y0, py), min(z0, pz)
      x1, y1, z1 = max(x1, px), max(y1, py), max(z1, pz)
    end
    V.aabb = { x0 - 0.5, y0 - 0.5, z0 - 0.5, x1 + 0.5, y1 + 0.5, z1 + 0.5 }
    V.trees = {}
    for by = 0, M.TREE_SPLIT - 1 do
      for bx = 0, M.TREE_SPLIT - 1 do
        local ch = new_chunk(V, "trees" .. bx .. "_" .. by)
        ch.bx, ch.by = bx, by
        V.trees[#V.trees + 1] = ch
      end
    end
    V.bld = new_chunk(V, "bld")
    V.rows = new_chunk(V, "rows")
    V.ground = new_chunk(V, "ground")
    M.NOATTR = require("game.platform.query").flag("vnoattr")
    M.NOANIM = require("game.platform.query").flag("vnoanim")
    M.LOD0 = require("game.platform.query").flag("vlod0")
    M.NOENABLE = require("game.platform.query").flag("vnoenable")
    local qn = require("game.platform.query").number("villagers", nil)
    if qn then M.PEOPLE = qn end
    -- (several settlements: smaller pools; only the settlements in view are drawn)
    local per = #W.S.v3.settlements > 1 and math.min(M.PEOPLE, 40) or M.PEOPLE
    if V.villager_factory and not require("game.platform.query").flag("oldpeople") then make_villagers(V, per)
    else make_pool(V, M.PEOPLE) end
    M.SETS[#M.SETS + 1] = V
    -- bake everything once
    for _, ch in ipairs(V.trees) do ch.sig = tree_sig(V, ch.bx, ch.by); bake(V, ch, fill_trees(ch.bx, ch.by)) end
    V.bld.sig = bld_sig(V); bake(V, V.bld, fill_buildings)
    V.rows.sig = rows_sig(V); bake(V, V.rows, fill_rows)
    V.ground.sig = ground_sig(V); bake(V, V.ground, fill_ground)
  end
end

-- Hearths and windows (D-082), from the sim: a house's hearth is in use while someone in it is
-- awake (cooking makes the most smoke); it burns only with wood on it (logs at home, or the day's
-- fire). Its windows glow after dusk while the hearth burns and someone inside is awake.
-- Returns lit (house id -> true), a signature of it, and the chimney tops smoking now.
local CHIM = {}
local function hearths(V, t)
  local W, s = V.W, V.s
  local Vp = W.S.v3.people
  local lit = V.lit or {}
  for k in pairs(lit) do lit[k] = nil end
  local sig = 0
  local n = #CHIM
  local lon = W.grid.lon[s.cell]
  local lh = require("sim.agents.clock").local_hour(t, lon)
  local dark = lh < (s.sunrise or 6) + 0.35 or lh > (s.sunset or 18) - 0.35
  local awake, cooking = V.awake or {}, V.cooking or {}
  V.awake, V.cooking = awake, cooking
  for k in pairs(awake) do awake[k] = nil end
  for k in pairs(cooking) do cooking[k] = nil end
  for i = 1, #s.members do
    local p = Vp[s.members[i]]
    if p and p.alive and p.inside ~= 0 and p.act ~= "sleep" and p.act ~= "carried" then
      awake[p.inside] = true
      if p.act == "cook" then cooking[p.inside] = true end
    end
  end
  local S = M.BUILD_SCALE
  local ch = propgen.CHIMNEY.cottage
  for i = 1, #s.blist do
    local b = s.buildings[s.blist[i]]
    if b.kind == "house" and b.state == "standing" and b.hh ~= 0 and awake[b.id] then
      local fire = (b.stock.logs or 0) > 0 or b.fire
      if fire then
        if dark then lit[b.id] = true; sig = (sig * 31 + b.id) % 1000003 end
        -- smoke from the chimney (cooking: two puffs' worth)
        local px, py, pz, ux, uy, uz = world_pos(V, b.cx, b.cy, 0)
        local yaw = yaw_z(V, b.cx, b.cy, b.dx - b.cx, b.dy - b.cy)
        local x, y, z = propgen.to_world(px, py, pz, ux, uy, uz, yaw, S, ch[1], ch[2], ch[3])
        for rep = 1, cooking[b.id] and 2 or 1 do
          n = n + 1
          local c = CHIM[n]
          if not c then c = {}; CHIM[n] = c end
          c[1], c[2], c[3] = x, y, z
        end
      end
    end
  end
  V.lit, V.lit_sig = lit, sig
end
M.chimneys = CHIM

-- Per frame: t = view time (the sim has been advanced to it).
function M.update(W, dt, zoom, t)
  M.t = t
  M.light_timer = (M.light_timer or 0) - dt
  if M.light_timer <= 0 and not M.skipping then
    M.light_timer = 0.25
    for k = #CHIM, 1, -1 do CHIM[k] = nil end
    for _, V in ipairs(M.SETS) do hearths(V, t) end
  end
  if M.follow and M.SETS[1] then
    local p = W.S.v3.people[M.follow]
    if p and p.alive then
      local V = M.SETS[p.sett] or M.SETS[1]
      local x, y = act.pos(p, t)
      local carrier = p.act == "carried" and W.S.v3.people[p.carrier or 0]
      if carrier then x, y = act.pos(carrier, t) end
      local ux, uy, uz = mapm.unit(V.s, x, y)
      local fd = M.follow_dir or {}
      fd[1], fd[2], fd[3] = ux, uy, uz
      M.follow_dir = fd
      local cam = require("game.view.camera")
      if not cam.follow then cam.follow = function() local d = M.follow_dir; return d[1], d[2], d[3] end end
    else
      M.follow = nil
    end
  end
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  for _, V in ipairs(M.SETS) do
    -- (a settlement far from the view focus is neither rebuilt nor peopled until it comes into view)
    local f = V.s.frame
    V.in_view = f[1] * fx + f[2] * fy + f[3] * fz > 0.975
    if not V.in_view and V.vpool and not V.hidden_far then
      for _, e in ipairs(V.vpool) do
        if e.on then for k = 1, 3 do msg.post(e.urls[k], MSG_DISABLE) end; e.on, e.pid, e.anim = false, 0, nil end
      end
      V.hidden_far = true
    elseif V.in_view then
      V.hidden_far = nil
    end
    -- (while skipping, nothing is drawn of the settlement's changes: they are rebuilt, a part a
    -- frame, once the skip lands)
    if not M.skipping and V.in_view then
    -- rebuild at most one changed part per frame (signatures are cheap)
    V.ri = (V.ri or 0) % (#V.trees + 3) + 1
    local ri = V.ri
    if ri <= #V.trees then
      local ch = V.trees[ri]
      local sg = tree_sig(V, ch.bx, ch.by)
      if sg ~= ch.sig then ch.sig = sg; bake(V, ch, fill_trees(ch.bx, ch.by)) end
    elseif ri == #V.trees + 1 then
      local sg = bld_sig(V)
      if sg ~= V.bld.sig then V.bld.sig = sg; bake(V, V.bld, fill_buildings) end
    elseif ri == #V.trees + 2 then
      local sg = rows_sig(V)
      if sg ~= V.rows.sig then V.rows.sig = sg; bake(V, V.rows, fill_rows) end
    else
      local sg = ground_sig(V)
      if sg ~= V.ground.sig then V.ground.sig = sg; bake(V, V.ground, fill_ground) end
    end
    if V.vpool then update_villagers(V, t, zoom) else update_people(V, t, zoom) end
    end
    M.update_boats(V, t)
    M.update_sheep(V, t)
  end
  if M.skipping then
    -- hide everyone at once (the pool is not walked while skipping)
    for _, V in ipairs(M.SETS) do
      if V.vpool and not V.hidden_all then
        for _, e in ipairs(V.vpool) do
          if e.on then for k = 1, 3 do msg.post(e.urls[k], MSG_DISABLE) end; e.on, e.pid, e.anim = false, 0, nil end
        end
        V.hidden_all = true
      end
    end
  else
    for _, V in ipairs(M.SETS) do V.hidden_all = nil end
  end
end

-- Sheep (D-089): each animal of the flock where the sim has it (a pool of the procedural sheep
-- movers; lambs smaller), facing the way it walks.
M.SHEEP = 60
M.SHEEP_SCALE = 0.55
local SPOS, SROT = vmath.vector3(), vmath.quat()
function M.update_sheep(V, t)
  local s = V.s
  local F = s.flock
  if not F or not V.deer_factory then return end
  local res = M.sheep_res      -- (set by view.lua from the puppets' sheep: no require cycle)
  if not res then return end
  if not V.sheep_go then
    V.sheep_go, V.sheep_yaw = {}, {}
    for k = 1, M.SHEEP do
      local id = factory.create(V.deer_factory, vmath.vector3(0, 0, 0))
      local url = msg.url(nil, id, "mesh")
      go.set(url, "vertices", res)
      msg.post(url, MSG_DISABLE)
      V.sheep_go[k] = { id = id, url = url, on = false }
    end
  end
  local flock = require("sim.agents.flock")
  local show = not M.skipping and V.in_view
  local n = 0
  for k = 1, #F.sheep do
    local a = F.sheep[k]
    if a.alive and show and n < M.SHEEP then
      n = n + 1
      local e = V.sheep_go[n]
      if not e.on then msg.post(e.url, MSG_ENABLE); e.on = true end
      local x, y = flock.pos(a, t)
      local px, py, pz, ux, uy, uz = world_pos(V, x, y, 0)
      SPOS.x, SPOS.y, SPOS.z = px, py, pz
      go.set_position(SPOS, e.id)
      if a.act == "walk" then
        local dx, dy = a.tx - a.x, a.ty - a.y
        if dx * dx + dy * dy > 1e-10 then V.sheep_yaw[k] = yaw_x(V, x, y, dx, dy) end
      end
      local yaw = V.sheep_yaw[k] or (k * 1.7)
      go.set_rotation(vmx.set_up_yaw(SROT, ux, uy, uz, yaw), e.id)
      local lamb = (t - a.born) / 1152 < 0.35
      go.set_scale(M.SHEEP_SCALE * (lamb and 0.6 or 1) * (a.act == "rest" and 0.85 or 1), e.id)
    end
  end
  for k = n + 1, M.SHEEP do
    local e = V.sheep_go[k]
    if e.on then msg.post(e.url, MSG_DISABLE); e.on = false end
  end
end

-- Boats (D-084): moored at the dock, or wherever their rower is on the water.
local BROT, BPOS = vmath.quat(), vmath.vector3()
function M.update_boats(V, t)
  local s = V.s
  if not s.boats or not V.boat_factory then return end
  if not V.boat_go then
    V.boat_go = {}
    for k = 1, #s.boats do
      local id = factory.create(V.boat_factory, vmath.vector3(0, 0, 0))
      V.boat_go[k] = { id = id, url = msg.url(nil, id, "mesh"), yaw = 0 }
    end
  end
  -- (boats launched later get their game objects as they appear, D-087)
  for k = #V.boat_go + 1, #s.boats do
    local id = factory.create(V.boat_factory, vmath.vector3(0, 0, 0))
    V.boat_go[k] = { id = id, url = msg.url(nil, id, "mesh"), yaw = 0 }
  end
  local Vp = V.W.S.v3.people
  local show0 = not M.skipping and V.in_view
  for k, boat in ipairs(s.boats) do
    local e = V.boat_go[k]
    if e then
      local show = show0 and not boat.lost   -- (a boat rotted through is broken up)
      if show ~= e.on then msg.post(e.url, show and MSG_ENABLE or MSG_DISABLE); e.on = show end
      if show then
        local x, y, dx, dy = boat.x, boat.y, nil, nil
        local p = boat.crew ~= 0 and Vp[boat.crew]
        if p and p.alive and p.boat == boat.id then x, y, dx, dy = act.pos(p, t) end
        local px, py, pz, ux, uy, uz = world_pos(V, x, y, -0.003)
        BPOS.x, BPOS.y, BPOS.z = px, py, pz
        go.set_position(BPOS, e.id)
        if dx and (dx * dx + dy * dy) > 1e-8 then e.yaw = yaw_x(V, x, y, dx, dy) end
        go.set_rotation(vmx.set_up_yaw(BROT, ux, uy, uz, e.yaw or 0), e.id)
        go.set_scale(0.55, e.id)
      end
    end
  end
end

-- The settlement nearest the view focus (clock, skips), or the first.
function M.nearest(W)
  local V3 = W.S.v3
  if not V3 then return nil end
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local best, bd = V3.settlements[1], -2
  for _, s in ipairs(V3.settlements) do
    local f = s.frame
    local d = f[1] * fx + f[2] * fy + f[3] * fz
    if s.alive and d > bd then best, bd = s, d end
  end
  return best
end

-- The settlement (first) for camera presets: its centre as a unit vector.
function M.focus(W)
  local s = W.S.v3 and W.S.v3.settlements[1]
  if not s then return nil end
  local g = W.grid
  return g.lat[s.cell], g.lon[s.cell]
end

return M
