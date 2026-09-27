-- Static props as batched chunk meshes (Stage 1.8, DECISIONS D-009).
--
-- The planet is split into 80 surface chunks (ancestor faces of the render icosphere). Each
-- chunk is one mesh component whose runtime buffer holds all of its trees, bushes, rocks,
-- crops, houses and ruins, baked in planet space (game/view/propgen.lua).
--   * Dirty detection: every DIRTY_INTERVAL s (if the sim advanced) each land sim cell gets a
--     quantised prop signature (forest, grass, biome, land use, burn state, ruins, village
--     hook); a changed cell marks the chunks that use it.
--   * Rebuilds are amortised: a job bakes a chunk's instances into a fresh buffer across
--     frames within BUDGET_MS, then swaps it in with resource.set_buffer.
--   * Culling: chunks behind the planet horizon are disabled; the engine frustum-culls the
--     rest through the buffer AABB. LOD: chunks near a zoomed-in camera are rebuilt with the
--     near templates and more slots; the rest use the cheap far set.
local propgen = require("game.view.propgen")
local planet = require("game.view.planet")
local state = require("game.view.state")
local clock = require("game.platform.clock")

local M = {}

M.BUDGET_MS = 2.5
M.JOB_GAP_MS = 0            -- min real ms between starting chunk rebuilds (raised at high sim speeds)
M.DIRTY_INTERVAL = 0.5
M.NEAR_DIST = 7.5           -- eye-to-chunk distance (world units) for near LOD
M.NEAR_ZOOM = 0.45          -- camera zoom needed before any chunk goes near

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local H_AABB = hash("AABB")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")

local sqrt, floor, max, min, abs, acos = math.sqrt, math.floor, math.max, math.min, math.abs, math.acos

M.chunks = {}
M.villages = nil          -- hook: cell -> { houses = n, ruin = bool }
M.stats = { rebuilt = 0, instances = 0, verts = 0, visible = 0, visible_instances = 0, jobs = 0 }

local job = nil
local gen = 0

--- Villages: list of { cell, id, ruin, culture, sig, radius } (view.lua sync_villages).
-- Each settlement is laid out from its sim buildings by the chunk that owns its centre
-- (propgen.plan_village); sig changes when a building or a visible stock changes.
function M.set_villages(list)
  local map = {}
  local owned = {}
  for i = 1, #(list or {}) do
    local v = list[i]
    local e = { cell = v.cell, id = v.id, ruin = v.ruin and true or false, culture = v.culture or 1,
      sig = v.sig or 0, radius = v.radius or 0.3 }
    map[v.cell] = e
    local k = M.owner_chunk(v.cell)
    if k then
      owned[k] = owned[k] or {}
      local l = owned[k]
      l[#l + 1] = v.cell
      if M.chunks[k] then M.chunks[k].dirty = true end
    end
  end
  for _, l in pairs(owned) do table.sort(l) end
  M.villages = map
  M.owned = owned
  M.force_signatures = true
end

--- Chunk whose faces contain the centre of sim cell c.
function M.owner_chunk(c)
  local PL, W = M.PL, M.W
  if not PL or not W or not M.vface then return nil end
  local g = W.grid
  local v = planet.nearest(PL, g.X[c], g.Y[c], g.Z[c])
  local f = M.vface[v]
  return f and PL.tchunk[f]
end

-- Surface radius and height (km, <= 0 water) at a unit direction.
local function surface(x, y, z)
  local PL = M.PL
  local r, v = planet.surface_radius(PL, x, y, z)
  return r, PL.h[v]
end

-- Nearest shore to a village centre (unit dir): returns the last land vertex before the
-- water (unit vector) and the unit direction out to sea, or nil if the sea is far.
local function shore(x, y, z)
  local PL = M.PL
  local rn, rcnt, h = PL.rn, PL.rcnt, PL.h
  local X, Y, Z = PL.X, PL.Y, PL.Z
  local start = planet.nearest(PL, x, y, z)
  local seen = { [start] = 0 }
  local q = { start }
  local head = 1
  while head <= #q do
    local v = q[head]; head = head + 1
    local d = seen[v]
    if d < 4 then
      local base = v * 6 - 6
      for k = 1, rcnt[v] do
        local j = rn[base + k]
        if seen[j] == nil then
          seen[j] = d + 1
          if h[j] <= 0 and h[v] > 0.02 then
            local dx, dy, dz = X[j] - X[v], Y[j] - Y[v], Z[j] - Z[v]
            local l = math.sqrt(dx * dx + dy * dy + dz * dz)
            return X[v], Y[v], Z[v], dx / l, dy / l, dz / l
          end
          q[#q + 1] = j
        end
      end
    end
  end
  return nil
end

--- Debug preview (view-only; never touches the sim): cell -> -1 (village) / 1 (farm), so the
-- house and crop art can be reviewed before Stage 1.7 villages exist. nil clears it.
function M.set_preview(map)
  M.preview = map
  M.force_signatures = true
end

-- Cells woodcutters worked in the last half year (the villages' logging records): stumps and logs.
function M.scan_logged(W)
  local set = {}
  local vs = W.ent and W.ent.villages or {}
  for i = 1, #vs do
    local lg = vs[i].alive ~= false and vs[i].logged
    if lg then
      for c, h in pairs(lg) do
        if W.hour - h < 4032 then set[c] = true end
      end
    end
  end
  M.logged = set
end

local function signature(W, c)
  local F = W.F
  -- forest in eighths; grass only matters for bushes/tufts: two levels (cuts seasonal churn)
  local q = floor(F.forest[c] * 8 + 0.5) + 11 * (F.grass[c] > 0.35 and 1 or 0)
    + 61 * F.biome[c]
  local lu = F.land_use[c]
  q = q + 1000 * (lu < 0 and 1 or (lu > 0 and 2 or 0))
  local scar = F.burn_scar[c]
  if F.fire[c] > 0 or (scar > 0 and scar < 240) then q = q + 4000 end
  if W.S and W.S.ruins and W.S.ruins[c] then q = q + 8000 end
  local v = M.villages and M.villages[c]
  if v then q = q + 16000 + (v.sig % 999983) * 32000 + (v.ruin and 7 or 0) end
  if lu > 0 then
    q = q + floor(F.crop[c] * 4 + 0.5) * 100000
    local fs = W.S and W.S.farm and W.S.farm[c]
    if fs and fs.sown > 0 then q = q + floor(fs.harvested * fs.sown * 5) * 3e6 end
  end
  if M.logged and M.logged[c] then q = q + 7e7 end
  local ic = F.ice[c]
  q = q + (ic > 0.55 and 2 or (ic > 0.3 and 1 or 0)) * 500000
  if M.preview and M.preview[c] then q = q + 1e7 * (M.preview[c] + 2) end
  return q
end

--- Creates the chunk game objects and plans the initial far-LOD build (baked by build_all).
function M.init(PL, W, factory_url, celltex)
  gen = gen + 1
  M.PL, M.W = PL, W
  M.ctx = { W = W, PL = PL, seed = W.seed, villages = M.villages, lod = 2, surface = surface, shore = shore }
  -- a face for every render vertex (settlement ownership)
  M.vface = {}
  local faces = PL.faces
  for f = 1, PL.nf do
    local i = f * 3 - 2
    for q = 0, 2 do
      local v = faces[i + q]
      if not M.vface[v] then M.vface[v] = f end
    end
  end
  M.owned = {}
  if #M.chunks == 0 then
    for k = 1, #PL.chunks do
      local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
      M.chunks[k] = { id = id, url = msg.url(nil, id, "mesh"), res = nil, enabled = true }
    end
  end
  if celltex then
    for k = 1, #M.chunks do go.set(M.chunks[k].url, "texture0", celltex) end
  end
  M.cell_chunks = {}
  for k, ch in ipairs(PL.chunks) do
    local c = M.chunks[k]
    c.ch = ch
    c.lod, c.want_lod = 0, 2
    c.dirty = true
    c.nverts, c.ninst = 0, 0
    -- AABB from the chunk's surface vertices (+ prop height margin)
    local x0, y0, z0, x1, y1, z1 = 1e9, 1e9, 1e9, -1e9, -1e9, -1e9
    for _, f in ipairs(ch.faces) do
      local i = f * 3 - 2
      for q = 0, 2 do
        local v = PL.faces[i + q]
        local x, y, z = PL.px[v], PL.py[v], PL.pz[v]
        if x < x0 then x0 = x end
        if y < y0 then y0 = y end
        if z < z0 then z0 = z end
        if x > x1 then x1 = x end
        if y > y1 then y1 = y end
        if z > z1 then z1 = z end
      end
    end
    local m = 0.9     -- props stand up to ~0.4 high; settlements reach past their chunk's faces
    c.aabb = { x0 - m, y0 - m, z0 - m, x1 + m, y1 + m, z1 + m }
    for _, cell in ipairs(ch.cells) do
      local l = M.cell_chunks[cell]
      if not l then l = {}; M.cell_chunks[cell] = l end
      l[#l + 1] = k
    end
  end
  M.sig = {}
  M.scan_logged(W)
  for c = 1, W.n do M.sig[c] = signature(W, c) end
  M.since = 0
  M.last_tick = W.tick
  job = nil
end

-- A rebuild job owns propgen.list for its lifetime (only one job runs at a time).
-- Phases: plan faces in batches -> create the buffer -> write instances in batches -> swap.
local function start_job(k)
  local c = M.chunks[k]
  c.dirty = false
  return { k = k, lod = c.want_lod, phase = 1, face = 1, next = 1, v = 0 }
end

-- small batches: the frame budget is checked between batches, so they bound the overshoot
local FACE_BATCH, INST_BATCH = 8, 24
local INST_BATCH_NEAR = 12

-- Advances job j until done or the deadline passes. Returns true when finished.
local function run_job(j, deadline)
  local c = M.chunks[j.k]
  local ch = c.ch
  if j.phase == 1 then
    M.ctx.lod = j.lod
    M.ctx.villages = M.villages
    M.ctx.logged = M.logged
    M.ctx.preview = M.preview
    M.ctx.owned = M.owned and M.owned[j.k]
    local nfaces = #ch.faces
    while j.face <= nfaces do
      local t0 = clock.now_ms()
      propgen.plan_chunk(M.ctx, ch, j.face, j.face + FACE_BATCH - 1, j.face > 1)
      M.stats.plan_max = max(M.stats.plan_max or 0, clock.now_ms() - t0)
      j.face = j.face + FACE_BATCH
      if clock.now_ms() > deadline then return false end
    end
    if nfaces == 0 then propgen.plan_chunk(M.ctx, ch, 1, 0, false) end
    local L = propgen.list
    j.n, j.nv = L.n, L.verts
    if j.nv == 0 then
      c.lod = j.lod
      c.nverts, c.ninst, c.empty = 0, 0, true
      return true
    end
    local tb = clock.now_ms()
    j.buf = buffer.create(j.nv, propgen.chunk_decl())
    j.S = { inst = buffer.get_stream(j.buf, propgen.H_INST), inst2 = buffer.get_stream(j.buf, propgen.H_INST2),
      inst3 = buffer.get_stream(j.buf, propgen.H_INST3) }
    M.stats.create_max = max(M.stats.create_max or 0, clock.now_ms() - tb)
    j.phase = 2
  end
  if j.phase == 2 then
    while j.next <= j.n do
      local last = min(j.n, j.next + (j.lod == 1 and INST_BATCH_NEAR or INST_BATCH) - 1)
      local tw = clock.now_ms()
      j.v = propgen.write(j.buf, j.S, j.v, j.next, last)
      M.stats.write_max = max(M.stats.write_max or 0, clock.now_ms() - tw)
      j.next = last + 1
      if clock.now_ms() > deadline then return j.next > j.n and M.finish(j) end
    end
    return M.finish(j)
  end
  return false
end

function M.finish(j)
  local tf = clock.now_ms()
  local c = M.chunks[j.k]
  buffer.set_metadata(j.buf, H_AABB, c.aabb, buffer.VALUE_TYPE_FLOAT32)
  if c.res then
    resource.set_buffer(c.res, j.buf, { transfer_ownership = true })
  else
    c.res = resource.create_buffer("/tc/chunk" .. gen .. "_" .. j.k .. ".bufferc", { buffer = j.buf })
    go.set(c.url, "vertices", c.res)
  end
  c.lod = j.lod
  c.nverts, c.ninst = j.nv, j.n
  c.empty = false
  M.stats.rebuilt = M.stats.rebuilt + 1
  M.stats.finish_max = max(M.stats.finish_max or 0, clock.now_ms() - tf)
  return true
end

local function set_enabled(c, on)
  if c.enabled ~= on then
    c.enabled = on
    msg.post(c.url, on and MSG_ENABLE or MSG_DISABLE)
  end
end

--- Bakes every chunk (loading screen). step(frac) may yield.
function M.build_all(step)
  step = step or function() end
  local n = #M.chunks
  for k = 1, n do
    local c = M.chunks[k]
    c.want_lod = 2
    local j = start_job(k)
    run_job(j, math.huge)
    set_enabled(M.chunks[k], not c.empty)
    step(k / n)
  end
  M.recount()
end

function M.recount()
  local ti, tv = 0, 0
  for _, c in ipairs(M.chunks) do ti, tv = ti + c.ninst, tv + c.nverts end
  M.stats.instances, M.stats.verts = ti, tv
end

local function check_dirty(W)
  local sig, cc = M.sig, M.cell_chunks
  local ocean = W.geo.ocean
  local force = M.force_signatures
  M.force_signatures = false
  for c = 1, W.n do
    if ocean[c] == 0 or force then
      local q = signature(W, c)
      if q ~= sig[c] then
        sig[c] = q
        local l = cc[c]
        if l then
          for i = 1, #l do M.chunks[l[i]].dirty = true end
        end
      end
    end
  end
end

--- Test/screenshot helper: detect changes and finish every pending rebuild synchronously.
function M.flush(W, zoom)
  local budget = M.BUDGET_MS
  M.BUDGET_MS = 1e9
  M.force_signatures = true
  M.since = 1e9
  for _ = 1, 400 do
    M.update(W, 0, zoom)
    local pending = job ~= nil
    for _, c in ipairs(M.chunks) do if c.dirty then pending = true; break end end
    if not pending then break end
  end
  M.BUDGET_MS = budget
  M.stats.dirty = 0
end

--- Per-frame: visibility, LOD wishes, dirty detection and amortised rebuilds.
function M.update(W, dt, zoom)
  if not M.PL then return end
  local t_top = clock.now_ms()
  local eye = state.eye
  local ex, ey, ez = eye.x, eye.y, eye.z
  local er = sqrt(ex * ex + ey * ey + ez * ez)
  local R = state.R
  local horizon = acos(min(1, (R - 0.3) / er))
  local vis, visinst = 0, 0
  for k, c in ipairs(M.chunks) do
    local ch = c.ch
    local d = (ch.cx * ex + ch.cy * ey + ch.cz * ez) / er
    local ang = acos(max(-1, min(1, d)))
    local visible = ang < horizon + acos(ch.cos_radius) + 0.05
    if visible and not c.empty then
      vis = vis + 1
      visinst = visinst + c.ninst
    end
    set_enabled(c, visible and not c.empty)
    -- LOD wish
    local want = 2
    if zoom >= M.NEAR_ZOOM and visible then
      local dx, dy, dz = ch.cx * R - ex, ch.cy * R - ey, ch.cz * R - ez
      if sqrt(dx * dx + dy * dy + dz * dz) < M.NEAR_DIST then want = 1 end
    end
    if want ~= c.want_lod then
      c.want_lod = want
      if want ~= c.lod then c.dirty = true end
    end
  end
  M.stats.visible, M.stats.visible_instances = vis, visinst
  local nd = job and 1 or 0
  for _, c in ipairs(M.chunks) do if c.dirty then nd = nd + 1 end end
  M.stats.dirty = nd

  M.since = M.since + dt
  if M.since >= M.DIRTY_INTERVAL and (W.tick ~= M.last_tick or M.force_signatures) then
    M.since = 0
    M.last_tick = W.tick
    M.scan_logged(W)
    check_dirty(W)
  end

  -- amortised rebuilds: visible dirty chunks first
  local t_start = clock.now_ms()
  local deadline = t_start + M.BUDGET_MS
  local guard = 0
  while clock.now_ms() < deadline and guard < 8 do
    guard = guard + 1
    if not job then
      if M.JOB_GAP_MS > 0 and t_start - (M.last_job_ms or -1e9) < M.JOB_GAP_MS then break end
      local pick, best = nil, -1
      for k, c in ipairs(M.chunks) do
        if c.dirty then
          local pr = (c.want_lod == 1 and 4 or 0) + ((c.enabled or c.empty) and 2 or 0) + 1
          if pr > best then pick, best = k, pr end
        end
      end
      if not pick then break end
      job = start_job(pick)
      M.last_job_ms = t_start
      M.stats.jobs = M.stats.jobs + 1
    end
    if run_job(job, deadline) then
      local c = M.chunks[job.k]
      job = nil
      M.recount()
      c.enabled = nil
      if c.empty then set_enabled(c, false) end
    end
  end
  local spent = clock.now_ms() - t_start
  M.stats.rebuild_ms_max = max(M.stats.rebuild_ms_max or 0, spent)
  M.stats.top_ms = clock.now_ms() - t_top
end

return M
