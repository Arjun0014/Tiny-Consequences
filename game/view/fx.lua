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
local sin, cos, sqrt, max, min, floor = math.sin, math.cos, math.sqrt, math.max, math.min, math.floor
local disc_buffer, pool_of, place_flat   -- (S25b: defined below, used by init)

M.flames, M.smoke, M.chim, M.bolts, M.pulses = {}, {}, {}, {}, {}
-- (S24) the v3 volcanoes' plumes: steam rising from a vent as the pressure under it builds, dark ash after it erupts
M.PLUMES = 18
M.plume_steam, M.plume_ash = {}, {}
-- (S25, D-206) a meteor's dust: a heavy column of earth and smoke over the crater at once, spreading and settling over
-- DUST_H game hours (the planet's craters, W.S.powers.craters - on a map or off it)
M.DUST, M.DUST_H = 28, 36
M.plume_dust = {}
-- (S25b, D-216) the meteor's fall: a long slanted streak with a trail of fire behind the rock; the crater and the scorched
-- ground it leaves as marks on the planet's surface, seen from every height (the village's map draws its own bowl close in)
M.DECALS = 8
M.decals = {}
-- (S25b, D-217) oil you can see on the water: dark glossy blobs where the sea holds oil (F.oil), as many as it holds,
-- drifting down the sea's own current and thinning as it disperses; a spill bursting out where the power is cast
M.OILB = 56
M.oilb = {}
M.SPILLB = 14
M.spillb = {}
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

--- An expanding ring where a power landed (kind = the power's root event kind). (S24) dir: the unit direction of
-- the point it landed on, else the cell's middle.
function M.pulse(W, c, kind, radius, dir)
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
  p.t, p.cell, p.r, p.dir = 0, c, radius or 0.8, dir
end

-- (S24) The unit direction of a cast's point: the point on a village's map it was aimed at, else its cell.
local function point_dir(W, c, p)
  local V = W.S and W.S.v3
  local s = p and p.sid and V and V.settlements[p.sid]
  if s and p.x then return require("sim.agents.map").unit(s, p.x, p.y) end
  local g = W.grid
  return g.X[c], g.Y[c], g.Z[c]
end
M.point_dir = point_dir

--- A meteor falling on cell c, on the point aimed at (p: sid, x, y). (S26) The fall, the strike and what it throws up are
-- game/view/meteor.lua's; commit() gives the sim its command when the rock reaches the ground (nil and instant: the
-- command was given already - the strike is drawn at once).
function M.meteor(W, c, p, commit, instant)
  local x, y, z = point_dir(W, c, p)
  require("game.view.meteor").cast(W, x, y, z, commit, instant)
end

--- Lightning bolt at sim cell c (natural strikes, the Lightning power). (S24) dir: the unit direction of the point it
-- struck (a village map's point), else the cell's middle.
function M.bolt(W, c, dir)
  if not M.W or #M.bolts == 0 then return end
  local g = W.grid
  local eye = state.eye
  local el = sqrt(eye.x * eye.x + eye.y * eye.y + eye.z * eye.z)
  local dx, dy, dz = g.X[c], g.Y[c], g.Z[c]
  if dir then dx, dy, dz = dir[1], dir[2], dir[3] end
  if (dx * eye.x + dy * eye.y + dz * eye.z) / el < 0.05 then return end
  local b = M.bolts[M.bolt_next]
  M.bolt_next = M.bolt_next % #M.bolts + 1
  b.t = 0
  b.cell = c
  b.dir = { dx, dy, dz }
  b.v = nil
  M.flash = 1
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

function M.init(W, flame_factory, smoke_factory, surface, surface_exact)
  M.W, M.surface, M.surface_exact = W, surface, surface_exact or surface
  if #M.flames == 0 then
    make("flame", { 1.0, 0.50, 0.12 }, { 1.0, 0.86, 0.35 }, flame_factory, M.FLAMES, M.flames)
    make("smoke", { 0.45, 0.43, 0.42 }, nil, smoke_factory, M.SMOKE, M.smoke)
    make("smoke", { 0.86, 0.86, 0.88 }, nil, smoke_factory, M.CHIMNEY, M.chim, "/tc/chimney_smoke.bufferc")
    make("smoke", { 0.58, 0.58, 0.60 }, nil, smoke_factory, M.PLUMES, M.plume_steam, "/tc/plume_steam.bufferc")
    make("smoke", { 0.30, 0.28, 0.27 }, nil, smoke_factory, M.PLUMES, M.plume_ash, "/tc/plume_ash.bufferc")
    make("smoke", { 0.46, 0.39, 0.32 }, nil, smoke_factory, M.DUST, M.plume_dust, "/tc/plume_dust.bufferc")
    -- (S25b) the meteor's trail, the crater marks, the oil
    M.decal_res = resource.create_buffer("/tc/crater_mark.bufferc", { buffer = disc_buffer({
      { 0.26, 0.05, 0.04, 0.04, 7 }, { 0.34, 0.16, 0.12, 0.10, 7 }, { 0.42, 0.46, 0.36, 0.26, 7 }, { 0.50, 0.36, 0.28, 0.20, 6 },
      { 0.72, 0.13, 0.11, 0.10, 6 }, { 1.0, 0.16, 0.14, 0.12, 2 } }, 40, 7) })
    M.decal_core_res = resource.create_buffer("/tc/crater_core.bufferc", { buffer = disc_buffer({
      { 0.26, 0.07, 0.06, 0.05, 7 }, { 0.34, 0.18, 0.15, 0.12, 7 }, { 0.42, 0.44, 0.35, 0.26, 7 }, { 0.56, 0.34, 0.28, 0.21, 3 } }, 40, 11) })
    pool_of(flame_factory, M.DECALS, M.decals, M.decal_res)
    M.oil_res = resource.create_buffer("/tc/oil_blob.bufferc", { buffer = disc_buffer({
      { 0.55, 0.02, 0.02, 0.025, 7 }, { 0.80, 0.05, 0.045, 0.06, 6 }, { 0.92, 0.22, 0.16, 0.30, 4 }, { 1.0, 0.10, 0.18, 0.22, 1 } }, 18, 23) })
    pool_of(flame_factory, M.OILB, M.oilb, M.oil_res)
    pool_of(flame_factory, M.SPILLB, M.spillb, M.oil_res)
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
    local feed = require("game.view.feed")
    feed.on("lightning_strike", function(Wf, r)
      -- (S24) the bolt comes down where it struck on the village's map, not at the middle of the planet's cell
      local d = r.data
      local V = Wf.S and Wf.S.v3
      local s = d and d.settlement and V and V.settlements[d.settlement]
      if s and d.x then
        local x, y, z = require("sim.agents.map").unit(s, d.x, d.y)
        M.bolt(Wf, r.cell, { x, y, z })
      else
        M.bolt(Wf, r.cell)
      end
    end)
    -- powers: every cast shows where it lands at once (even while paused; the sim applies
    -- the command at the next tick)
    local aim = require("game.view.aim")
    local kinds = { weather = { rain = "power_rain", drought = "power_drought" },
      temperature = { heat = "power_heat", chill = "power_chill" } }
    local steps = { power_rain = 2, power_drought = 2, power_heat = 2, power_chill = 2, power_cull = 2,
      power_introduce = 1, power_blight = 1, power_oil = 1, power_meteor = 2, power_lightning = 0.3 }
    -- (S26) the meteor's command waits for its strike: the rock falls first (game/view/meteor.lua), then the world is hit.
    -- Cast while time stands still, time runs at Watch (a god's strike is watched); a skip under way ends.
    require("game.ui.powers").defer_meteor = function(Wc, cell, p, commit)
      local speed = require("game.platform.speed")
      if speed.v3_skipping and speed.v3_skipping() then speed.v3_skip_cancel() end
      if speed.get() == 0 then speed.set(1) end
      M.meteor(Wc, cell, p, commit, false)
    end
    require("game.ui.powers").on_cast = function(Wc, id, cell, p, deferred)
      local k = kinds[id] and kinds[id][p.mode] or ("power_" .. id)
      -- (S24: a bolt aimed at a village's map is drawn by its strike, where it comes down - the event feed)
      if id == "lightning" and not (Wc.v3 and p.sid and p.x) then M.bolt(Wc, cell) end
      -- (S26) a meteor cast for a later strike was started by defer_meteor; one given at once (the harnesses) strikes now
      if id == "meteor" then if not deferred then M.meteor(Wc, cell, p, nil, true) end; return end
      if id == "oil" then local x, y, z = point_dir(Wc, cell, p); M.spill = { t = 0, dir = { x, y, z } } end
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
  -- (S24, D-190) a cell a village's map owns burns by the map's own fire: its flames are drawn where each thing burns
  local O = W.v3 and W.S.v3 and require("sim.agents.map").owners(W) or nil
  for c = 1, W.n do
    local f = F.fire[c]
    -- (S25, D-207) nor one a map covers in good part: its flames would stand over the map's own trees, which that fire
    -- lights where it comes onto the map (blaze.planet) - the map's own flames show it there
    if f > M.FIRE_MIN and not (O and (O.by[c] or (O.share[c] or 0) >= 0.25)) then
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

-- (S25b) A flat disc in the plane y = 0 (the object's up is the ground's): rings of colour { radius, r, g, b, opacity 0..7 },
-- each ring's edge a little ragged (seeded) so a mark on the ground does not read as a drawn circle.
disc_buffer = function(rings, seg, seed)
  local h = seed or 1
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local rag = {}
  for ri = 1, #rings do
    rag[ri] = {}
    for i = 0, seg - 1 do rag[ri][i] = 1 + (ri > 1 and (u() - 0.5) * 0.22 or 0) end
    rag[ri][seg] = rag[ri][0]
  end
  local nv = seg * 3 + (#rings - 1) * seg * 6
  local buf = buffer.create(nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  local P, N, Cc = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local pack = propgen.pack_rgb
  local k = 0
  local function vtx(x, z, ring)
    local o = k * 3
    P[o + 1], P[o + 2], P[o + 3] = x, 0, z
    N[o + 1], N[o + 2], N[o + 3] = 0, 1, 0
    Cc[k * 2 + 1], Cc[k * 2 + 2] = pack(ring[2], ring[3], ring[4]), 31 * 8 + ring[5]
    k = k + 1
  end
  local function at(ri, i)
    local a = i / seg * 2 * math.pi
    local r = rings[ri][1] * rag[ri][i]
    return cos(a) * r, sin(a) * r
  end
  for i = 0, seg - 1 do
    local x1, z1 = at(1, i)
    local x2, z2 = at(1, i + 1)
    vtx(0, 0, rings[1]); vtx(x1, z1, rings[1]); vtx(x2, z2, rings[1])
  end
  for ri = 2, #rings do
    for i = 0, seg - 1 do
      local ax, az = at(ri - 1, i)
      local bx, bz = at(ri - 1, i + 1)
      local cx, cz = at(ri, i)
      local dx, dz = at(ri, i + 1)
      vtx(ax, az, rings[ri - 1]); vtx(cx, cz, rings[ri]); vtx(dx, dz, rings[ri])
      vtx(ax, az, rings[ri - 1]); vtx(dx, dz, rings[ri]); vtx(bx, bz, rings[ri - 1])
    end
  end
  return buf
end

pool_of = function(factory_url, n, out, res)
  for i = 1, n do
    local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    if res then go.set(url, "vertices", res) end
    msg.post(url, MSG_DISABLE)
    out[i] = { id = id, url = url, on = false, phase = i * 1.37, life = (i * 0.61) % 1 }
  end
end

-- obj on the ground at unit direction (ux, uy, uz), lifted, turned, scaled (sx across, sy up)
place_flat = function(obj, ux, uy, uz, lift, yaw, sx, sy)
  local r, v = M.surface(ux, uy, uz, obj.v)
  obj.v = v
  r = r + lift
  pos.x, pos.y, pos.z = ux * r, uy * r, uz * r
  go.set_position(pos, obj.id)
  go.set_rotation(vmx.set_up_yaw(rot, ux, uy, uz, yaw or 0), obj.id)
  scl.x, scl.y, scl.z = sx, sy or sx, sx
  go.set_scale(scl, obj.id)
end

-- (S25) obj at the point of unit direction (ux, uy, uz), off_e east and off_n north of it along the ground, lifted
local function place_dir(obj, ux, uy, uz, off_e, off_n, lift, s, sh)
  local ex, ez = uz, -ux
  local el = sqrt(ex * ex + ez * ez)
  if el < 1e-6 then ex, ez, el = 1, 0, 1 end
  ex, ez = ex / el, ez / el
  local nx, ny, nz = uy * ez, uz * ex - ux * ez, -uy * ex
  local x, y, z = ux + (ex * off_e + nx * off_n) / M.R0, uy + (ny * off_n) / M.R0, uz + (ez * off_e + nz * off_n) / M.R0
  local l = sqrt(x * x + y * y + z * z)
  x, y, z = x / l, y / l, z / l
  local r, v = M.surface(x, y, z, obj.v)
  obj.v = v
  r = r + lift
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, obj.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, obj.phase), obj.id)
  scl.x, scl.y, scl.z = s, s * sh, s
  go.set_scale(scl, obj.id)
end

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

-- (S24) a bolt's or a meteor's flash: the frame's bloom burst for a moment (game/render post_comp.fp u_post.x)
M.flash = 0
M.BLOOM = 0.35
function M.update(W, dt, frozen)
  if M.flash > 0 then
    state.u_post.x = M.BLOOM + 2.2 * M.flash * M.flash
    M.flash = max(0, M.flash - dt * 4)
    if M.flash == 0 then state.u_post.x = M.BLOOM end
  end
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
  -- (S24) the maps' own fires first - each burning tree, roof and field where it burns, larger seen from afar so a fire
  -- on a village reads at every zoom - then the planet's fires on the land no map owns, with what the pool has left
  local used = 0
  if nl > 0 then
    local ex, ey, ez = state.eye.x, state.eye.y, state.eye.z
    local nlf = min(nl, #M.flames)
    for i = 1, nlf do
      local o = M.flames[i]
      local f = LF[i]
      local fl = 0.8 + 0.25 * sin(t * 11 + o.phase * 5) + 0.1 * sin(t * 23 + o.phase)
      local ddx, ddy, ddz = f[1] - ex, f[2] - ey, f[3] - ez
      local far = max(1, sqrt(ddx * ddx + ddy * ddy + ddz * ddz) / 2.2)
      -- (a roof's flames rise above it as the heat grows; a tree's are smaller)
      local s = (f[8] and (0.045 + 0.075 * f[4]) or (0.03 + 0.05 * f[4])) * fl * min(far, 4)
      local a = (i % 2 == 0) and 0.003 or -0.003
      pos.x, pos.y, pos.z = f[1] + a * f[6], f[2] - a * f[5], f[3]
      go.set_position(pos, o.id)
      go.set_rotation(vmx.set_up_yaw(rot, f[5], f[6], f[7], o.phase), o.id)
      scl.x, scl.y, scl.z = s, s * 1.4, s
      go.set_scale(scl, o.id)
      if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
    end
    used = nlf
  end
  for i = used + 1, #M.flames do
    local o = M.flames[i]
    local j = i - used
    local fire = nf > 0 and fires[(j - 1) % nf + 1] or nil
    local on = fire ~= nil and j <= nf * 3
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
      if b.dir then x, y, z = b.dir[1], b.dir[2], b.dir[3] end
      local r, v = M.surface(x, y, z, b.v)
      b.v = v
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      go.set_position(pos, b.id)
      go.set_rotation(vmx.set_up_yaw(rot, x, y, z, b.t * 40), b.id)
      -- (S24: from the cloud base, which lifts as the camera comes down - game/view/clouds.lua)
      local top = state.R + max(0.55, 0.95 + (require("game.view.clouds").lift or 0)) - r
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
      if p.dir then x, y, z = p.dir[1], p.dir[2], p.dir[3] end
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
  -- (S26) the meteor: its fall and strike in real time while the world runs (game/view/meteor.lua)
  do
    local sp = require("game.platform.speed")
    require("game.view.meteor").update(W, dt, frozen, sp.get() > 0 or (sp.v3_skipping and sp.v3_skipping()) or false)
  end
  -- (S24) the volcanoes near the view (read where the world keeps them - never volcano.list, which makes them): steam
  -- from the vent of one whose pressure is building (from 35% of what makes it erupt; more and taller the nearer), dark
  -- ash rising from one that erupted in the last days
  local VS = W.v3 and W.S.v3 and W.S.v3.volc
  local fxv, fyv, fzv = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local day = W.v3 and floor((require("game.platform.speed").v3_t or W.hour) / 24) or 0
  local ns, na = 0, 0
  local g = W.grid
  for _, v in ipairs(VS and VS.list or {}) do
    local c = v.cell
    local d = g.X[c] * fxv + g.Y[c] * fyv + g.Z[c] * fzv
    if d > 0.6 then
      local ratio = (v.thr or 1) > 0 and v.pressure / v.thr or 0
      local erupting = v.last and day - v.last < 3 and (v.erupted or 0) > 0
      local pool = erupting and M.plume_ash or M.plume_steam
      local k = erupting and 12 or (ratio > 0.35 and min(M.PLUMES, floor(4 + ratio * 10)) or 0)
      for q = 1, k do
        local o
        if erupting then na = na + 1; o = M.plume_ash[na] else ns = ns + 1; o = M.plume_steam[ns] end
        if o then
          if not frozen then o.life = o.life + dt * (erupting and 0.22 or 0.3) end
          if o.life > 1 then o.life = o.life - 1; o.phase = o.phase + 1.7 end
          local l = o.life
          local a, b = sin(o.phase * 1.9) * 0.4, cos(o.phase * 2.7) * 0.4
          -- (S24: grey-white steam, tall and broad enough to be seen over a snowy summit from the mid zoom)
          local big = erupting and 2.6 or (0.8 + 1.2 * ratio)
          local sz = (0.08 + 0.3 * l) * big * (1 - l * l) + 0.001
          place(o, W, c, a * 0.3 + l * 0.2, b * 0.3, 0.1 + l * (erupting and 2.0 or 1.4) * big * 0.5, sz, 0.9)
          if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
        end
      end
    end
  end
  -- (S25, D-206) the meteors' dust over their craters: thick and tall at once, thinning and settling over M.DUST_H
  local CRS = W.v3 and W.S.powers and W.S.powers.craters
  local nd = 0
  if CRS and #CRS > 0 then
    M.R0 = M.R0 or state.R or 10
    local now = require("game.platform.speed").v3_t or W.hour
    for ci = #CRS, 1, -1 do
      local cr = CRS[ci]
      local age = now - (cr.t or now)
      if age >= 0 and age < M.DUST_H and cr.x then
        local ul = sqrt(cr.x * cr.x + cr.y * cr.y + cr.z * cr.z)
        local ux, uy, uz = cr.x / ul, cr.y / ul, cr.z / ul
        if ux * fxv + uy * fyv + uz * fzv > 0.5 then
          local k = 1 - age / M.DUST_H
          local n = min(M.DUST - nd, floor(8 + 20 * k))
          for q = 1, n do
            nd = nd + 1
            local o = M.plume_dust[nd]
            if not frozen then o.life = o.life + dt * (0.10 + 0.08 * (q % 3)) end
            if o.life > 1 then o.life = o.life - 1; o.phase = o.phase + 1.7 end
            local l = o.life
            local a, b = sin(o.phase * 1.9 + q * 0.9), cos(o.phase * 2.7 + q * 0.7)
            -- (a column that billows out at its top; low rolling clouds of dust spreading over the ground about it)
            local low = q % 3 == 0
            local big = 0.5 + 1.8 * k
            local sz = (low and (0.10 + 0.25 * l) or (0.10 + 0.40 * l)) * big * (1 - 0.5 * l * l) + 0.002
            local spread = low and (0.25 + 0.9 * l) * big or (0.08 + 0.35 * l * l) * big
            local lift = low and 0.03 + 0.08 * l or (0.05 + l * 1.5 * big)
            place_dir(o, ux, uy, uz, a * spread, b * spread, lift, sz, low and 0.55 or 0.9)
            if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
          end
        end
      end
      if nd >= M.DUST then break end
    end
  end
  for i = nd + 1, #M.plume_dust do local o = M.plume_dust[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
  -- (S25b, D-216) the craters' marks on the planet: a dark bowl, its pale rim of thrown earth and the scorched ground about
  -- it out to the fireball's reach (fading into the core alone after M.SCORCH_DAYS); a crater on a village's map gives way
  -- to the map's own bowl when the eye comes down to it
  do
    local nk = 0
    local zoom = require("game.view.camera").zoom
    local now = W.v3 and (require("game.platform.speed").v3_t or W.hour) or 0
    local cv = require("sim.config").V3
    local mapm = W.v3 and require("sim.agents.map")
    M.cr_map = M.cr_map or {}
    for ci = #(CRS or {}), 1, -1 do
      local cr = CRS[ci]
      if nk < M.DECALS and cr.x and not cr.sea then
        local ul = sqrt(cr.x * cr.x + cr.y * cr.y + cr.z * cr.z)
        local ux, uy, uz = cr.x / ul, cr.y / ul, cr.z / ul
        local on_map = M.cr_map[cr]
        if on_map == nil then
          on_map = false
          for _, s in ipairs(W.S.v3 and W.S.v3.settlements or {}) do
            if s.alive and s.frame then
              local lx, ly = mapm.local_of(s, ux, uy, uz)
              local half = s.N * s.T * 0.5
              if lx and math.abs(lx) < half and math.abs(ly) < half then on_map = true end
            end
          end
          M.cr_map[cr] = on_map
        end
        local ex, ey, ez = state.eye.x, state.eye.y, state.eye.z
        local el = sqrt(ex * ex + ey * ey + ez * ez)
        if (ux * ex + uy * ey + uz * ez) / el > 0.05 and not (on_map and zoom > 0.78) then
          nk = nk + 1
          local o = M.decals[nk]
          local young = (now - (cr.t or now)) / 24 < 36
          local res = young and M.decal_res or M.decal_core_res
          if o.res ~= res then o.res = res; go.set(o.url, "vertices", res) end
          local S = young and cv.imp_scorch * 1.05 or cv.imp_crater * 1.8
          place_flat(o, ux, uy, uz, 0.006, (ci * 2.3) % 6.28, S, 1)
          if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
        end
      end
    end
    for i = nk + 1, #M.decals do local o = M.decals[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
  end
  -- (S25b, D-217) the oil on the sea near the view: blobs as many as each cell's oil (F.oil), each drifting down the cell's
  -- own current (W.currents) over its beat, swelling up and thinning away so that none jumps, the whole of them a slick
  -- that moves where the sea moves it
  do
    local no = 0
    local zoom = require("game.view.camera").zoom
    if zoom >= 0.25 and W.F.oil then
      M.oil_timer = (M.oil_timer or 0) - dt
      if M.oil_timer <= 0 then
        M.oil_timer = 0.5
        local L = {}
        local F, g2 = W.F, W.grid
        for c = 1, W.n do
          local o = F.oil[c] or 0
          if o > 0.004 and W.geo.ocean[c] == 1 then
            local d = g2.X[c] * fxv + g2.Y[c] * fyv + g2.Z[c] * fzv
            if d > 0.95 then L[#L + 1] = { c, o, d } end
          end
        end
        table.sort(L, function(a, b) if a[3] ~= b[3] then return a[3] > b[3] end return a[1] < b[1] end)
        M.oil_cells = L
      end
      local L = M.oil_cells or {}
      local CUR = W.currents and W.currents[floor(((W.tick or 0) % 48) / 12) + 1]
      local sp = W.grid.spacing * state.R
      local t = state.time
      for _, e in ipairs(L) do
        local c, oil = e[1], e[2]
        local n = math.min(6, math.max(1, math.floor(math.sqrt(oil) * 9 + 0.5)))
        local cx, cy, cz = W.grid.X[c], W.grid.Y[c], W.grid.Z[c]
        local Ex, Ez = cz, -cx
        local El = sqrt(Ex * Ex + Ez * Ez) + 1e-9
        Ex, Ez = Ex / El, Ez / El
        local Nx, Ny, Nz = cy * Ez, cz * Ex - cx * Ez, -cy * Ex
        local ce, cn = 1, 0
        if CUR and CUR.vx and CUR.vx[c] then
          local vx, vy, vz = CUR.vx[c], CUR.vy[c], CUR.vz[c]
          ce, cn = vx * Ex + vz * Ez, vx * Nx + vy * Ny + vz * Nz
          local cl = sqrt(ce * ce + cn * cn)
          if cl > 1e-9 then ce, cn = ce / cl, cn / cl else ce, cn = 1, 0 end
        end
        for q = 1, n do
          if no >= M.OILB then break end
          no = no + 1
          local o = M.oilb[no]
          local h1 = ((c * 73856093 + q * 19349663) % 1000) / 1000
          local h2 = ((c * 83492791 + q * 2654435761) % 1000) / 1000
          local beat = (t * 0.035 + h1) % 1
          local along = (beat - 0.5) * sp * 0.9
          local ox = (h1 - 0.5) * sp * 0.8 + ce * along - cn * (h2 - 0.5) * sp * 0.5
          local oy = (h2 - 0.5) * sp * 0.8 + cn * along + ce * (h1 - 0.5) * sp * 0.3
          local wx, wy, wz = cx + (Ex * ox + Nx * oy) / state.R, cy + (Ny * oy) / state.R, cz + (Ez * ox + Nz * oy) / state.R
          local wl = sqrt(wx * wx + wy * wy + wz * wz)
          local size = sp * (0.16 + 0.22 * math.min(1, sqrt(oil))) * (0.7 + 0.6 * h2) * sin(beat * math.pi)
          place_flat(o, wx / wl, wy / wl, wz / wl, 0.012, h1 * 6.28 + t * 0.05, size + 0.001, 1)
          if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
        end
        if no >= M.OILB then break end
      end
    end
    for i = no + 1, #M.oilb do local o = M.oilb[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
    -- the spill itself: black oil bursting out over the water from where it was cast, for its first seconds
    local spl = M.spill
    local ns = 0
    if spl then
      if not frozen then spl.t = spl.t + dt end
      local k = spl.t / 2.6
      if k < 1 then
        local x, y, z = spl.dir[1], spl.dir[2], spl.dir[3]
        local Ex, Ez = z, -x
        local El = sqrt(Ex * Ex + Ez * Ez) + 1e-9
        Ex, Ez = Ex / El, Ez / El
        local Nx, Ny, Nz = y * Ez, z * Ex - x * Ez, -y * Ex
        for q = 1, M.SPILLB do
          ns = ns + 1
          local o = M.spillb[q]
          local a = q / M.SPILLB * 6.283 + sin(q * 3.1) * 0.3
          local rr = (0.15 + 1.6 * math.sqrt(k)) * (0.6 + 0.5 * ((q * 0.618) % 1))
          local ox, oy = cos(a) * rr, sin(a) * rr
          local wx, wy, wz = x + (Ex * ox + Nx * oy) / state.R, y + (Ny * oy) / state.R, z + (Ez * ox + Nz * oy) / state.R
          local wl = sqrt(wx * wx + wy * wy + wz * wz)
          place_flat(o, wx / wl, wy / wl, wz / wl, 0.014, q, (0.25 + 0.55 * math.sqrt(k)) * (1 - 0.3 * k), 1)
          if not o.on then o.on = true; msg.post(o.url, MSG_ENABLE) end
        end
      else
        M.spill = nil
      end
    end
    for i = ns + 1, #M.spillb do local o = M.spillb[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
  end
  for i = ns + 1, #M.plume_steam do local o = M.plume_steam[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
  for i = na + 1, #M.plume_ash do local o = M.plume_ash[i]; if o.on then o.on = false; msg.post(o.url, MSG_DISABLE) end end
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
