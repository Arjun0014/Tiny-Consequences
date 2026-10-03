-- Fire effects (Stage 1.8): burning cells already glow through the terrain data texture; this
-- adds pooled flame spikes and rising smoke puffs (unlit fx material, engine-batched) over the
-- most intense visible fires. Later stages add lightning, meteors etc. from the event feed.
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")

local M = {}

M.FLAMES, M.SMOKE = 36, 36
M.CHIMNEY = 48          -- chimney smoke puffs (villages near the view, zoomed in)
M.BOLTS = 4             -- lightning bolts (real lightning strikes from the event feed; powers)
M.PULSES = 8            -- expanding rings where a power lands
M.PULSE_LIFE = 1.1
-- ring colours per power root event
M.PULSE_COLORS = {
  power_rain = { 0.45, 0.72, 1.0 }, power_drought = { 1.0, 0.72, 0.30 }, power_heat = { 1.0, 0.42, 0.25 },
  power_chill = { 0.62, 0.92, 1.0 }, power_cull = { 0.72, 0.62, 0.86 }, power_introduce = { 0.55, 0.92, 0.45 },
  power_blight = { 0.66, 0.40, 0.78 }, power_oil = { 0.25, 0.20, 0.32 }, power_meteor = { 1.0, 0.55, 0.20 },
  power_lightning = { 1.0, 0.95, 0.70 },
}
M.BOLT_LIFE = 0.4
M.MAX_FIRES = 12
M.FIRE_MIN = 0.05

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min = math.sin, math.cos, math.sqrt, math.max, math.min

M.flames, M.smoke, M.chim, M.bolts, M.pulses = {}, {}, {}, {}, {}
M.pulse_res = {}

-- A jagged bolt from the ground (y = 0) to the cloud base (y = 1), crossed quads per segment.
local function bolt_buffer(seed)
  local h = seed
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local pts = {}
  local n = 9
  for i = 0, n do
    local t = i / n
    local j = (i == 0) and 0 or 0.09
    pts[i] = { (u() - 0.5) * j, t, (u() - 0.5) * j }
  end
  local nv = n * 12
  local buf = buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local packed = require("game.view.propgen").pack_rgb(0.92, 0.96, 1.0)
  local v = 0
  local w = 0.008
  for i = 0, n - 1 do
    local a, b = pts[i], pts[i + 1]
    for q = 0, 1 do
      local dx, dz = (q == 0) and w or 0, (q == 0) and 0 or w
      local c = { { a[1] - dx, a[2], a[3] - dz }, { a[1] + dx, a[2], a[3] + dz }, { b[1] + dx, b[2], b[3] + dz }, { b[1] - dx, b[2], b[3] - dz } }
      for _, k in ipairs({ 1, 2, 3, 1, 3, 4 }) do
        local p = v * 3
        pos[p + 1], pos[p + 2], pos[p + 3] = c[k][1], c[k][2], c[k][3]
        nrm[p + 1], nrm[p + 2], nrm[p + 3] = 0, 1, 0
        col[v * 2 + 1], col[v * 2 + 2] = packed, 31 * 8 + 7
        v = v + 1
      end
    end
  end
  return buf
end

-- Ring buffer (the annulus template) in one colour, for pulses.
local function ring_buffer(col)
  local t = require("game.view.propgen").mover_template("pulse")
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  require("game.view.propgen").write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), col, nil, 1)
  return buf
end

--- An expanding ring where a power landed (kind = the power's root event kind).
function M.pulse(W, c, kind, radius)
  if #M.pulses == 0 then return end
  local col = M.PULSE_COLORS[kind] or { 1, 0.9, 0.6 }
  local res = M.pulse_res[kind]
  if not res then
    res = resource.create_buffer("/tc/pulse_" .. kind .. ".bufferc", { buffer = ring_buffer(col) })
    M.pulse_res[kind] = res
  end
  local p = M.pulses[M.pulse_next]
  M.pulse_next = M.pulse_next % #M.pulses + 1
  go.set(p.url, "vertices", res)
  p.t, p.cell, p.r = 0, c, radius or 0.8
end

--- A meteor falling on cell c (then the pulse; the sim's fires follow).
function M.meteor(W, c)
  M.meteor_fx = { t = 0, cell = c }
end

--- Lightning bolt at sim cell c (natural strikes, the Lightning power).
function M.bolt(W, c)
  if not M.W or #M.bolts == 0 then return end
  local g = W.grid
  local eye = state.eye
  local el = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  if (g.X[c] * eye.x + g.Y[c] * eye.y + g.Z[c] * eye.z) / el < 0.05 then return end
  local b = M.bolts[M.bolt_next]
  M.bolt_next = M.bolt_next % #M.bolts + 1
  b.t = 0
  b.cell = c
end
M.stats = { fires = 0 }

local function make(kind, c1, c2, factory_url, n, out, own_res)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, 1)
  for i = 1, n do
    local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    if own_res then
      -- a separate buffer resource (the factory's mesh resource is shared with other pools)
      if i == 1 then out.res = resource.create_buffer(own_res, { buffer = buf }) end
      go.set(url, "vertices", out.res)
    elseif i == 1 then resource.set_buffer(go.get(url, "vertices"), buf) end
    msg.post(url, MSG_DISABLE)
    out[i] = { id = id, url = url, on = false, phase = i * 1.37, life = (i * 0.61) % 1 }
  end
end

function M.init(W, flame_factory, smoke_factory, surface)
  M.W, M.surface = W, surface
  if #M.flames == 0 then
    make("flame", { 1.0, 0.50, 0.12 }, { 1.0, 0.86, 0.35 }, flame_factory, M.FLAMES, M.flames)
    make("smoke", { 0.45, 0.43, 0.42 }, nil, smoke_factory, M.SMOKE, M.smoke)
    make("smoke", { 0.86, 0.86, 0.88 }, nil, smoke_factory, M.CHIMNEY, M.chim, "/tc/chimney_smoke.bufferc")
    for i = 1, M.BOLTS do
      local id = factory.create(flame_factory, vmath.vector3(0, 0, 0))
      local url = msg.url(nil, id, "mesh")
      local res = resource.create_buffer("/tc/bolt" .. i .. ".bufferc", { buffer = bolt_buffer(i * 7717) })
      go.set(url, "vertices", res)
      msg.post(url, MSG_DISABLE)
      M.bolts[i] = { id = id, url = url, on = false, t = 99 }
    end
    M.bolt_next = 1
    for i = 1, M.PULSES do
      local id = factory.create(flame_factory, vmath.vector3(0, 0, 0))
      local url = msg.url(nil, id, "mesh")
      msg.post(url, MSG_DISABLE)
      M.pulses[i] = { id = id, url = url, on = false, t = 99 }
    end
    M.pulse_next = 1
    local fid = factory.create(flame_factory, vmath.vector3(0, 0, 0))
    M.fireball = { id = fid, url = msg.url(nil, fid, "mesh"), on = false }
    msg.post(M.fireball.url, MSG_DISABLE)
    local feed = require("game.view.feed")
    feed.on("lightning_strike", function(Wf, r) M.bolt(Wf, r.cell) end)
    -- powers: every cast shows where it lands at once (even while paused; the sim applies
    -- the command at the next tick)
    local aim = require("game.view.aim")
    local kinds = { weather = { rain = "power_rain", drought = "power_drought" },
      temperature = { heat = "power_heat", chill = "power_chill" } }
    local steps = { power_rain = 2, power_drought = 2, power_heat = 2, power_chill = 2, power_cull = 2,
      power_introduce = 1, power_blight = 1, power_oil = 1, power_meteor = 2, power_lightning = 0.3 }
    require("game.ui.powers").on_cast = function(Wc, id, cell, p)
      local k = kinds[id] and kinds[id][p.mode] or ("power_" .. id)
      if id == "lightning" then M.bolt(Wc, cell) end
      if id == "meteor" then M.meteor(Wc, cell); return end
      M.pulse(Wc, cell, k, aim.world_radius(Wc, steps[k] or 1))
    end
  end
  M.chim_list = {}
  M.timer = 0
  M.fires = {}
end

local function scan(W)
  local F, g = W.F, W.grid
  local eye = state.eye
  local el = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  local list = {}
  for c = 1, W.n do
    local f = F.fire[c]
    if f > M.FIRE_MIN then
      local d = (g.X[c] * eye.x + g.Y[c] * eye.y + g.Z[c] * eye.z) / el
      if d > 0.1 then list[#list + 1] = { c, f } end
    end
  end
  table.sort(list, function(a, b) return a[2] > b[2] end)
  local fires = {}
  for i = 1, min(#list, M.MAX_FIRES) do fires[i] = list[i] end
  M.fires = fires
end

local pos, scl, rot = vmath.vector3(), vmath.vector3(), vmath.quat()

local function place(obj, W, cell, off_a, off_b, lift, s, sh)
  local g = W.grid
  local sp = g.spacing * 0.3
  local lat = g.lat[cell] + off_a * sp
  local lon = g.lon[cell] + off_b * sp / max(0.2, cos(g.lat[cell]))
  local cl = cos(lat)
  local x, y, z = cl * cos(lon), sin(lat), cl * sin(lon)
  local r, v = M.surface(x, y, z, obj.v)
  obj.v = v
  r = r + lift
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, obj.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, 0), obj.id)
  scl.x, scl.y, scl.z = s, s * sh, s
  go.set_scale(scl, obj.id)
end

-- Chimneys of the villages nearest the view focus. Hearths burn wood all year for cooking and
-- far more in the cold (the sim's villagers' wood burning, sim/systems/villages.lua): a cold
-- village smokes from every chimney, a warm one from a few.
local function scan_chimneys(W)
  local list = {}
  local zoom = require("game.view.camera").zoom
  if zoom < 0.45 then M.chim_list = list; return end
  if W.v3 then
    -- v3 (D-082): the chimneys of the hearths in use now (game/view/agents_view.lua)
    M.chim_list = require("game.view.agents_view").chimneys
    return
  end
  local chims = require("game.view.propgen").chimneys
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local g = W.grid
  local vs = W.ent and W.ent.villages or {}
  for _, v in ipairs(vs) do
    if v.alive ~= false and chims[v.cell] then
      local d = g.X[v.cell] * fx + g.Y[v.cell] * fy + g.Z[v.cell] * fz
      if d > 0.93 then
        local temp = W.F.temp[v.cell]
        local share = temp < 0 and 1 or (temp < 10 and 0.7 or 0.3)
        local ch = chims[v.cell]
        for k = 1, #ch do
          if (k * 0.618) % 1 < share then list[#list + 1] = ch[k] end
        end
      end
    end
  end
  M.chim_list = list
end

function M.update(W, dt, frozen)
  M.timer = M.timer - dt
  if M.timer <= 0 then
    M.timer = 0.5
    scan(W)
    scan_chimneys(W)
  end
  local fires = M.fires
  local nf = #fires
  M.stats.fires = nf
  local t = state.time
  -- (S20, R6.5b) v3: what burns on the settlements' maps, close in, takes the flames first
  local LF = W.v3 and require("game.view.agents_view").local_fires or nil
  local nl = LF and #LF or 0
  if nl > 0 then
    for i, o in ipairs(M.flames) do
      local f = LF[(i - 1) % nl + 1]
      local on = i <= nl * 2
      if on then
        local fl = 0.8 + 0.25 * sin(t * 11 + o.phase * 5) + 0.1 * sin(t * 23 + o.phase)
        -- (a roof's flames rise above it as the heat grows; a tree's are smaller)
        local s = (f[8] and (0.045 + 0.075 * f[4]) or (0.03 + 0.05 * f[4])) * fl
        local a = (i % 2 == 0) and 0.003 or -0.003
        pos.x, pos.y, pos.z = f[1] + a * f[6], f[2] - a * f[5], f[3]
        go.set_position(pos, o.id)
        go.set_rotation(vmx.set_up_yaw(rot, f[5], f[6], f[7], o.phase), o.id)
        scl.x, scl.y, scl.z = s, s * 1.4, s
        go.set_scale(scl, o.id)
      end
      if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
    end
    nf = 0
  end
  for i, o in ipairs(nl > 0 and {} or M.flames) do
    local fire = nf > 0 and fires[(i - 1) % nf + 1] or nil
    local on = fire ~= nil and i <= nf * 3
    if on then
      local a, b = sin(o.phase * 3.1) * 0.8, cos(o.phase * 2.3) * 0.8
      local fl = 0.8 + 0.25 * sin(t * 11 + o.phase * 5) + 0.1 * sin(t * 23 + o.phase)
      local s = (0.09 + 0.12 * fire[2]) * fl
      place(o, W, fire[1], a, b, -0.02, s, 1.4)
    end
    if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  for i, o in ipairs(M.smoke) do
    local fire = nf > 0 and fires[(i - 1) % nf + 1] or nil
    local on = fire ~= nil and i <= nf * 3
    if on then
      if not frozen then o.life = o.life + dt * 0.35 end
      if o.life > 1 then o.life = o.life - 1; o.phase = o.phase + 1.7 end
      local l = o.life
      local a, b = sin(o.phase * 1.9) * 0.6, cos(o.phase * 2.7) * 0.6
      local s = (0.06 + 0.22 * l) * (1 - l * l) + 0.001
      place(o, W, fire[1], a + l * 0.3, b, 0.12 + l * 0.9, s, 0.8)
    end
    if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  -- lightning: a flickering bolt from the clouds to the strike, then gone
  for _, b in ipairs(M.bolts) do
    if not frozen then b.t = b.t + dt end
    local on = b.t < M.BOLT_LIFE and (math.floor(b.t * 28) % 3 ~= 1)
    if on and b.cell then
      local g = W.grid
      local x, y, z = g.X[b.cell], g.Y[b.cell], g.Z[b.cell]
      local r, v = M.surface(x, y, z, b.v)
      b.v = v
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      go.set_position(pos, b.id)
      go.set_rotation(vmx.set_up_yaw(rot, x, y, z, b.t * 40), b.id)
      local top = state.R + 0.55 - r
      -- thicker from far away so a strike still reads at planet zoom
      local dx, dy, dz = pos.x - state.eye.x, pos.y - state.eye.y, pos.z - state.eye.z
      local th = max(1, sqrt(dx * dx + dy * dy + dz * dz) / 6)
      scl.x, scl.y, scl.z = th, top, th
      go.set_scale(scl, b.id)
    end
    if on ~= b.on then b.on = on; msg.post(b.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  -- power pulses: a ring grows from the target and vanishes
  for _, p in ipairs(M.pulses) do
    if not frozen then p.t = p.t + dt end
    local on = p.t < M.PULSE_LIFE and p.cell ~= nil
    if on then
      local g = W.grid
      local x, y, z = g.X[p.cell], g.Y[p.cell], g.Z[p.cell]
      local r = M.surface(x, y, z) + 0.04
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      go.set_position(pos, p.id)
      go.set_rotation(vmx.set_up_yaw(rot, x, y, z, p.t), p.id)
      local f = p.t / M.PULSE_LIFE
      local s = p.r * (0.25 + 1.0 * math.sqrt(f)) * (f > 0.85 and (1 - f) / 0.15 or 1)
      scl.x, scl.y, scl.z = s, s, s
      go.set_scale(scl, p.id)
    end
    if on ~= p.on then p.on = on; msg.post(p.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  -- meteor: a fireball falls from the sky along a slant, then the blast ring
  local m = M.meteor_fx
  local fb = M.fireball
  if m and fb then
    if not frozen then m.t = m.t + dt end
    local fall = 0.8
    local on = m.t < fall
    if on then
      local g = W.grid
      local x, y, z = g.X[m.cell], g.Y[m.cell], g.Z[m.cell]
      local k = m.t / fall
      local r = state.R + 7 * (1 - k) * (1 - k)
      local ex, ez = z, -x
      local el = sqrt(ex * ex + ez * ez) + 1e-6
      local side = 2.5 * (1 - k)
      pos.x, pos.y, pos.z = x * r + ex / el * side, y * r, z * r + ez / el * side
      go.set_position(pos, fb.id)
      go.set_rotation(vmx.set_up_yaw(rot, x, y, z, m.t * 9), fb.id)
      local s = 0.35 + 0.25 * k
      scl.x, scl.y, scl.z = s, s * 1.6, s
      go.set_scale(scl, fb.id)
    elseif not m.boom then
      m.boom = true
      local aim = require("game.view.aim")
      M.pulse(W, m.cell, "power_meteor", aim.world_radius(W, 2.5))
      M.pulse(W, m.cell, "power_heat", aim.world_radius(W, 1.2))
      M.bolt(W, m.cell)
      M.shake = 0.6
    end
    if on ~= fb.on then fb.on = on; msg.post(fb.url, on and MSG_ENABLE or MSG_DISABLE) end
    if m.t > fall + 2 then M.meteor_fx = nil end
  end
  -- chimney smoke: each chimney gets a couple of puffs at different ages, rising and drifting
  local cl = M.chim_list or {}
  local nc = #cl
  for i, o in ipairs(M.chim) do
    local ch = nc > 0 and cl[(i - 1) % nc + 1] or nil
    local on = ch ~= nil and i <= nc * 2
    if on then
      if not frozen then o.life = o.life + dt * 0.28 end
      if o.life > 1 then o.life = o.life - 1; o.phase = o.phase + 1.3 end
      local l = o.life
      local x, y, z = ch[1], ch[2], ch[3]
      local r = math.sqrt(x * x + y * y + z * z)
      local ux, uy, uz = x / r, y / r, z / r
      local ex, ez = uz, -ux
      local el = math.sqrt(ex * ex + ez * ez) + 1e-6
      local drift = l * 0.1
      pos.x = x + ux * l * 0.3 + ex / el * drift
      pos.y = y + uy * l * 0.3
      pos.z = z + uz * l * 0.3 + ez / el * drift
      go.set_position(pos, o.id)
      go.set_rotation(vmx.set_up_yaw(rot, ux, uy, uz, o.phase), o.id)
      -- a small wisp at the chimney, swelling a little as it rises and thins away
      local s = (0.018 + 0.075 * l) * (1 - l * l) + 0.001
      scl.x, scl.y, scl.z = s, s * 0.85, s
      go.set_scale(scl, o.id)
    end
    if on ~= o.on then o.on = on; msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
end

return M
