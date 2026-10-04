-- Clouds from the weather the world lives (renderer v5, S23, D-180).
--
-- One soft cloud layer around the planet, marched along each pixel's view ray on the GPU
-- (game/render/shaders/cloudvol.fp): banks with irregular, frayed outlines, thick and thin parts, flat
-- grey bases and sunlit tops - and a camera that flies into it sees mist close by, then the ground
-- through its thinning floor, then the deck's underside overhead. (v4 drew separate ray-traced puffs:
-- they read as cotton balls laid on the world, and close in a hole was cut through them.)
--   * Where and how much: a cover map (equirect, CW x CH) written hour by hour from each planet cell's
--     weather - the same rain spells the villages live under (sim/agents/weather.lua `cell_day`, one
--     draw for a village and for the cell it stands on), the god's rain and dry spells from the moment
--     they are cast, the humidity of the day between spells (fair-weather heaps where the air is damp,
--     clear sky where it is dry). Cover builds over the hours before a spell, is full while it rains and
--     thins after it; a downpour is darker and towers. A clear cell is never given dramatic cloud.
--   * Shape inside the cover: 3D noise, carried along by the zonal wind of each latitude (two phases,
--     each reset while its weight is nil, so the shear never builds up).
--   * Time: two maps, the one in force and the next; the shader blends from one to the other as the
--     hours pass, so cover never jumps. At the skip speeds (days flashing by) the day's rain as a whole.
--   * Altitude: the base floats above the highest ground about each place (geotex), decks are thicker,
--     storms tower.
--   * Shadows: a sphere at the layer's middle drawn from the sun writes the same cloud into the shadow
--     map's cover (game/render/shaders/cloudshell.fp) - soft cloud shadows on the ground.
--   * Rain and snow curtains hang under the places where it rains now, nearest the view.
local state = require("game.view.state")
local cfgmod = require("sim.config")
local weather = require("sim.agents.weather")
local vmx = require("game.view.vmx")
local clockm = require("game.platform.clock")

local M = {}

M.CW, M.CH = 128, 64      -- the cover map (equirect: u longitude, v latitude)
M.BMIN = 0.95             -- cloud base above sea level (world units) at the least
M.BMAX = 2.2              -- ... at the most (over the highest ground)
M.CLEAR = 0.72            -- clearance of the base above the highest ground about a place
M.EXAG = 0.13             -- planet.lua P.EXAG (world units per km)
M.TOP = 1.05              -- the tallest storm above its base (cloudvol.fp: 0.42 + 0.6, and some)
-- (S24) The layer lifts as the camera comes down to a place: far out it lies where the planet view wants it (a
-- shell hugging the planet); from LIFT_FAR down to LIFT_NEAR (the eye's height above the sea) it rises by LIFT, so
-- that the deck stands above the height from which a village is watched - the camera passes through it in a
-- wheel step or two and comes out under it with the village clear below and the cloud overhead (S23's layer sat
-- at the village view's own height: under the god's rain 90% of the frame was cloud from zoom 0.8 to 1.35).
M.LIFT = 1.45
M.LIFT_FAR = 12.0
M.LIFT_NEAR = 5.5
M.lift = 0
M.STEPS = 24              -- samples along a ray (at full quality)
M.STEPS_FIXED = nil       -- ?cloudq=N: the samples fixed (shots); else they follow the frame time
M.q_steps = 24            -- the samples now (adaptive: a slow GPU gets fewer)
M.q_dt = 1 / 60
M.DENS = 12               -- extinction of full cloud per world unit
M.FREQ = 1.3              -- noise frequency (per world unit)
M.DRIFT = 0.0025          -- radians per sim hour per unit zonal wind (~ half a cell a day)
M.DRIFT_MAX = 0.006
M.PERIOD = 36             -- hours a phase of the wind-carried noise lives
M.FAST_HPS = 6            -- hours per second above which clouds follow days, not spells
M.BUDGET_MS = 1.0         -- Lua time per frame for the next map
M.RAIN = 110              -- rain/snow curtain pool
M.hidden = false          -- gfx=low: nothing drawn, nothing computed
M.DENSITY = 1             -- (v4 compatibility: gfx=low)

M.rains = {}
M.stats = { active = 0, cover = 0, puffs = 0, maps = 0, rebuilt = 0, job_ms = 0, raining = 0 }

local H_RGBA = hash("rgba")
local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, abs, floor, pi, asin, atan2 = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.abs, math.floor, math.pi, math.asin, math.atan2

-- per cell: the cloud base (normalised), cover and darkness of the map being made
local BASEN, COVC, DRKC = {}, {}, {}
-- per texel: the cells it is made of (up to 7) and their weights
local TC, TW, TN = {}, {}, {}
-- per texel: the map in force (A), as bytes
local PREVC, PREVD = {}, {}
-- the god's sky at an hour: cell -> 2 downpour, 1 rain, 0 dry spell (one set for the map being made,
-- one for the curtains of now)
local JFORCE, JSTAMP, RFORCE, RSTAMP = {}, {}, {}, {}
local serial = 0
-- each cell's rain spells of its day (the villages' own draw), kept while the day and its rain hold
local wx = { a = {}, b = {}, heavy = {} }
local SD_DAY, SD_P, SD_WET, SD_N, SD_A1, SD_B1, SD_H1, SD_A2, SD_B2, SD_H2 = {}, {}, {}, {}, {}, {}, {}, {}, {}, {}
local function spells(W, c, day)
  local P = W.F.precip[c] or 0
  if SD_DAY[c] == day and SD_P[c] == P then return end
  weather.cell_day(W, c, day, wx)
  SD_DAY[c], SD_P[c], SD_WET[c], SD_N[c] = day, P, wx.wet and true or false, wx.n
  SD_A1[c], SD_B1[c], SD_H1[c] = wx.a[1] or 0, wx.b[1] or 0, wx.heavy[1] or false
  SD_A2[c], SD_B2[c], SD_H2[c] = wx.a[2] or 0, wx.b[2] or 0, wx.heavy[2] or false
end

local function prof(tbl, a)
  if a <= tbl[1][1] then return tbl[1][2] end
  for i = 2, #tbl do
    if a <= tbl[i][1] then
      local p, q = tbl[i - 1], tbl[i]
      return p[2] + (q[2] - p[2]) * (a - p[1]) / (q[1] - p[1])
    end
  end
  return tbl[#tbl][2]
end

---------------------------------------------------------------------------------------------
-- Textures and the shadow shell

local function rgba_texture(path, w, h, fill)
  local buf = buffer.create(w * h, { { name = H_RGBA, type = buffer.VALUE_TYPE_UINT8, count = 4 } })
  local st = buffer.get_stream(buf, H_RGBA)
  if fill then fill(st) end
  local params = { type = graphics.TEXTURE_TYPE_2D, width = w, height = h, format = graphics.TEXTURE_FORMAT_RGBA }
  local res = resource.create_texture(path, params, buf)
  return res, buf, st, params
end

-- value noise for the shader (iq's packing: g is r one slice up, offset by 37, 17)
local function noise_fill(st)
  local N = 256
  local h = 987654321
  local v = {}
  for i = 1, N * N do
    h = (h * 1103515245 + 12345) % 2147483648
    v[i] = floor(h / 2147483648 * 256)
  end
  for y = 0, N - 1 do
    for x = 0, N - 1 do
      local i = y * N + x
      local j = ((y + 17) % N) * N + (x + 37) % N
      local o = i * 4
      st[o + 1], st[o + 2], st[o + 3], st[o + 4] = v[i + 1], v[j + 1], 0, 255
    end
  end
end

local function shell_buffer(r)
  local g = require("sim.grid").build(3, { neighbours = false })
  local faces = g.faces
  local nf = #faces / 3
  local buf = buffer.create(nf * 3, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
  })
  local pos, nrm = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM)
  for i = 1, #faces do
    local v = faces[i]
    local p = (i - 1) * 3
    pos[p + 1], pos[p + 2], pos[p + 3] = g.X[v] * r, g.Y[v] * r, g.Z[v] * r
    nrm[p + 1], nrm[p + 2], nrm[p + 3] = g.X[v], g.Y[v], g.Z[v]
  end
  local R = r * 1.02
  buffer.set_metadata(buf, hash("AABB"), { -R, -R, -R, R, R, R }, buffer.VALUE_TYPE_FLOAT32)
  return buf
end

-- Rain/snow curtain: crossed quads (rain streaks) or small flakes in a disc under the cloud.
local function rain_buffer(snow)
  local n = 110
  local buf = buffer.create(n * 12, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm, col = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM), buffer.get_stream(buf, H_COL)
  local h = 17
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local len = snow and 0.02 or 0.09
  local wid = snow and 0.012 or 0.0018
  local v = 0
  for _ = 1, n do
    local a, r = u() * 6.283, sqrt(u()) * 0.45
    local x, z = cos(a) * r, sin(a) * r
    local ph = u()
    for q = 0, 1 do
      local dx, dz = (q == 0) and wid or 0, (q == 0) and 0 or wid
      local corners = { { x - dx, 0, z - dz }, { x + dx, 0, z + dz }, { x + dx, len, z + dz }, { x - dx, len, z - dz } }
      local order = { 1, 2, 3, 1, 3, 4 }
      for k = 1, 6 do
        local c = corners[order[k]]
        local p, cc = v * 3, v * 4
        pos[p + 1], pos[p + 2], pos[p + 3] = c[1], c[2] - len, c[3]
        nrm[p + 1], nrm[p + 2], nrm[p + 3] = 0, 1, 0
        col[cc + 1], col[cc + 2], col[cc + 3], col[cc + 4] = ph, snow and 1 or 0, order[k] - 1, q
        v = v + 1
      end
    end
  end
  return buf
end

---------------------------------------------------------------------------------------------
-- Init

-- urls: { vol = "/cloudvol#mesh", shell = "/cloudshell#mesh" }; rain_factory for the curtains.
function M.init(W, urls, rain_factory)
  M.W = W
  M.urls = urls
  local g = W.grid
  local CW, CH = M.CW, M.CH
  if #M.rains == 0 and rain_factory then
    M.rain_res = resource.create_buffer("/tc/rain.bufferc", { buffer = rain_buffer(false) })
    M.snow_res = resource.create_buffer("/tc/snow.bufferc", { buffer = rain_buffer(true) })
    for i = 1, M.RAIN do
      local id = factory.create(rain_factory, vmath.vector3(0, 0, 0), nil, nil, vmath.vector3(0.001))
      local url = msg.url(nil, id, "mesh")
      go.set(url, "vertices", M.rain_res)
      msg.post(url, MSG_DISABLE)
      M.rains[i] = { id = id, url = url, on = false, snow = false }
    end
  end
  -- the cloud base per cell: above the highest ground and trees around it
  local elev, sea = W.static.elev, W.G.sea_level or 0
  for c = 1, W.n do
    local m = elev[c] - sea
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local e = elev[g.nbr[b + k]] - sea
      if e > m then m = e end
    end
    local base = max(M.BMIN, (m > 0 and m or 0) * M.EXAG + M.CLEAR)
    BASEN[c] = min(1, (base - M.BMIN) / (M.BMAX - M.BMIN))
    COVC[c], DRKC[c] = 0, 0
  end
  -- texels: the nearest cell and its neighbours, weighted by distance (a smooth field over the grid)
  local sp2 = g.spacing * g.spacing
  local X, Y, Z = g.X, g.Y, g.Z
  local cell = 1
  for j = 0, CH - 1 do
    local lat = ((j + 0.5) / CH - 0.5) * pi
    local cl, sl = cos(lat), sin(lat)
    for i = 0, CW - 1 do
      local lon = ((i + 0.5) / CW - 0.5) * 2 * pi
      local px, py, pz = cl * cos(lon), sl, cl * sin(lon)
      -- (a greedy walk to the nearest cell from the last one)
      for _ = 1, 200 do
        local best, bd = cell, px * X[cell] + py * Y[cell] + pz * Z[cell]
        local b6 = cell * 6 - 6
        for k = 1, g.nbr_count[cell] do
          local q = g.nbr[b6 + k]
          local d = px * X[q] + py * Y[q] + pz * Z[q]
          if d > bd then best, bd = q, d end
        end
        if best == cell then break end
        cell = best
      end
      local t = j * CW + i + 1
      local o = (t - 1) * 7
      local n, ws = 0, 0
      local b6 = cell * 6 - 6
      for k = 0, g.nbr_count[cell] do
        local q = k == 0 and cell or g.nbr[b6 + k]
        local d = px * X[q] + py * Y[q] + pz * Z[q]
        local a = (1 - d) / sp2
        local w = 1 / (0.05 + a * a)
        n = n + 1
        TC[o + n], TW[o + n] = q, w
        ws = ws + w
      end
      for k = 1, n do TW[o + k] = TW[o + k] / ws end
      TN[t] = n
      PREVC[t], PREVD[t] = 0, 0
    end
  end
  -- textures: the cover maps (dynamic), the base and the wind (static), the noise
  if not M.cov then
    M.cov, M.covbuf, M.covst, M.covparams = rgba_texture("/tc/cloudcov.texturec", CW, CH)
    M.geo, M.geobuf, M.geost, M.geoparams = rgba_texture("/tc/cloudgeo.texturec", CW, CH)
    M.noise = rgba_texture("/tc/cloudnoise.texturec", 256, 256, noise_fill)
    for _, u in ipairs({ urls.vol, urls.shell }) do
      go.set(u, "texture0", M.cov)
      go.set(u, "texture1", M.geo)
      go.set(u, "texture2", M.noise)
    end
    local rs = state.R + (M.BMIN + M.BMAX) * 0.5 + 0.25
    resource.set_buffer(go.get(urls.shell, "vertices"), shell_buffer(rs))
  end
  local zonal = cfgmod.MOIST.zonal
  local gst = M.geost
  for j = 0, CH - 1 do
    local lat = ((j + 0.5) / CH - 0.5) * pi
    local w = prof(zonal, abs(lat) * 57.2958) * M.DRIFT / max(0.25, cos(lat))
    local wn = floor(max(0, min(255, (w / M.DRIFT_MAX * 0.5 + 0.5) * 255)) + 0.5)
    for i = 0, CW - 1 do
      local t = j * CW + i + 1
      local o = (t - 1) * 7
      local b = 0
      for k = 1, TN[t] do b = b + TW[o + k] * BASEN[TC[o + k]] end
      local s = (t - 1) * 4
      gst[s + 1], gst[s + 2], gst[s + 3], gst[s + 4] = floor(b * 255 + 0.5), wn, 0, 255
    end
  end
  resource.set_texture(M.geo, M.geoparams, M.geobuf)
  local cv0, cv1, cv2 = state.u_cv0, state.u_cv1, state.u_cv2
  cv1.x, cv1.y, cv1.z, cv1.w = 0, M.STEPS, M.DENS, M.FREQ
  cv2.z, cv2.w = M.DRIFT_MAX, M.PERIOD
  M.set_lift(0)
  M.job, M.ta, M.tb = nil, nil, nil
  M.rain_t = 0
  M.update(W, 0, true, true)
end

---------------------------------------------------------------------------------------------
-- Weather -> cover and darkness per cell at hour t

local function god_sky(W, t, FORCE, FSTAMP)
  serial = serial + 1
  local P = W.S and W.S.powers
  local fs = P and P.forcings
  if not fs then return serial end
  local burst = cfgmod.POWERS.v3_burst_h or 3
  for k = 1, #fs do
    local f = fs[k]
    if f.at and f.cells and t >= f.at and t < f.at + 24 and (f.kind == "rain" or f.kind == "drought") then
      local v = f.kind == "drought" and 0 or (t < f.at + burst and 2 or 1)
      for _, c in ipairs(f.cells) do FORCE[c], FSTAMP[c] = v, serial end
    end
  end
  return serial
end

-- 0 dry, 1 rain, 2 downpour at cell c at hour t (the god's sky first, then the day's spells)
local function rain_at(W, c, t, FORCE, FSTAMP, stamp)
  if FSTAMP[c] == stamp then return FORCE[c] end
  spells(W, c, floor(t / 24))
  if not SD_WET[c] then return 0 end
  if t >= SD_A1[c] and t < SD_B1[c] then return SD_H1[c] and 2 or 1 end
  if SD_N[c] >= 2 and t >= SD_A2[c] and t < SD_B2[c] then return SD_H2[c] and 2 or 1 end
  return 0
end

local function cell_cloud(W, c, t, fast, stamp)
  local F = W.F
  local P = F.precip[c] or 0
  local pw = P / cfgmod.WEATHER_DAY.wet_week
  if pw > 1.6 then pw = 1.6 elseif pw < 0 then pw = 0 end
  if fast then
    -- (days flash by: the day's rain as a whole)
    if pw > 1.05 then return min(0.92, 0.62 + (pw - 1.05) * 0.6), min(0.6, (pw - 1.05) * 0.9) end
    return max(0.0, min(0.55, (pw - 0.12) * 0.62)), 0
  end
  local f = JSTAMP[c] == stamp and JFORCE[c] or nil
  if f == 0 then return 0.0, 0 end
  -- between spells: fair-weather heaps where the air is damp, clear where it is dry
  local hum = max(0, min(0.5, (pw - 0.08) * 0.55))
  local k, heavy = 0, false
  if f then
    k, heavy = 1, f == 2
  else
    spells(W, c, floor(t / 24))
    if SD_WET[c] then
      -- a wet day is grey between its spells; cloud gathers over the three hours before a spell, is
      -- full while it rains, thins over three hours after it
      hum = max(hum, 0.32)
      for i = 1, SD_N[c] do
        local a, b, hv
        if i == 1 then a, b, hv = SD_A1[c], SD_B1[c], SD_H1[c] else a, b, hv = SD_A2[c], SD_B2[c], SD_H2[c] end
        local v
        if t >= a and t < b then v = 1
        elseif t < a then v = 1 - (a - t) / 3
        else v = 1 - (t - b) / 3 end
        if v > k then k, heavy = v, hv end
      end
    end
  end
  if k < 0 then k = 0 end
  return hum + (0.95 - hum) * k, k * (heavy and 0.8 or 0.36)
end

---------------------------------------------------------------------------------------------
-- The next map: cells, then texels, then the upload (budgeted)

local function start_job(W, t, fast)
  return { t = t, fast = fast, phase = 1, i = 1, stamp = god_sky(W, t, JFORCE, JSTAMP) }
end

local function run_job(W, J, deadline)
  local n = W.n
  if J.phase == 1 then
    while J.i <= n do
      COVC[J.i], DRKC[J.i] = cell_cloud(W, J.i, J.t, J.fast, J.stamp)
      J.i = J.i + 1
      if J.i % 32 == 0 and clockm.now_ms() > deadline then return false end
    end
    J.phase, J.i = 2, 1
    J.cover = 0
  end
  if J.phase == 2 then
    local st = M.covst
    local NT = M.CW * M.CH
    local cov_sum = J.cover
    while J.i <= NT do
      local t = J.i
      local o = (t - 1) * 7
      local cv, dk = 0, 0
      for k = 1, TN[t] do
        local q, w = TC[o + k], TW[o + k]
        cv, dk = cv + w * COVC[q], dk + w * DRKC[q]
      end
      local cb, db = floor(cv * 255 + 0.5), floor(dk * 255 + 0.5)
      local s = (t - 1) * 4
      st[s + 1], st[s + 2], st[s + 3], st[s + 4] = PREVC[t], cb, PREVD[t], db
      PREVC[t], PREVD[t] = cb, db
      cov_sum = cov_sum + cv
      J.i = t + 1
      if t % 256 == 0 and clockm.now_ms() > deadline then J.cover = cov_sum; return false end
    end
    J.cover = cov_sum
    resource.set_texture(M.cov, M.covparams, M.covbuf)
    M.stats.maps = M.stats.maps + 1
    M.stats.rebuilt = M.stats.rebuilt + 1
    M.stats.cover = cov_sum / NT
    return true
  end
  return false
end

---------------------------------------------------------------------------------------------
-- Per frame

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

-- (S24) The layer at a lift: the bounds the march uses, the base range, and the shadow shell at the layer's middle.
local shell_scl = vmath.vector3(1)
function M.set_lift(lift)
  M.lift = lift
  local cv0, cv2 = state.u_cv0, state.u_cv2
  cv0.y, cv0.z, cv0.w = state.R, state.R + M.BMIN + lift, state.R + M.BMAX + M.TOP + lift
  cv2.x, cv2.y = M.BMIN + lift, M.BMAX + lift
  if M.urls and M.urls.shell then
    local r0 = state.R + (M.BMIN + M.BMAX) * 0.5 + 0.25
    local k = (r0 + lift) / r0
    shell_scl.x, shell_scl.y, shell_scl.z = k, k, k
    go.set_scale(shell_scl, (M.urls.shell:match("^[^#]+")))
  end
end

local function smooth(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-9 then return 0, 0, 1 end
  return x / l, y / l, z / l
end

--- Per-frame: the next cover map (budgeted), the blend, the camera's basis, rain curtains.
-- instant: finish the map now (loading, M.flush before screenshots).
function M.update(W, dt, frozen, instant)
  if not M.urls or not M.cov then return end
  local hidden = M.hidden
  -- the full-screen pass's rays: the camera's basis
  local e, tg, u = state.eye, state.target, state.up
  local fx, fy, fz = norm(tg.x - e.x, tg.y - e.y, tg.z - e.z)
  local rx, ry, rz = norm(fy * u.z - fz * u.y, fz * u.x - fx * u.z, fx * u.y - fy * u.x)
  local ux, uy, uz = ry * fz - rz * fy, rz * fx - rx * fz, rx * fy - ry * fx
  local th = math.tan(state.fov * 0.5)
  local asp = (state.window_w or 1280) / max(1, state.window_h or 720)
  local cr, cu, cf = state.u_cr, state.u_cu, state.u_cf
  cr.x, cr.y, cr.z = rx * th * asp, ry * th * asp, rz * th * asp
  cu.x, cu.y, cu.z = ux * th, uy * th, uz * th
  cf.x, cf.y, cf.z = fx, fy, fz
  -- (S24) the lift by the eye's height above the sea
  local er = sqrt(e.x * e.x + e.y * e.y + e.z * e.z) - state.R
  local lift = M.LIFT * (1 - smooth(M.LIFT_NEAR, M.LIFT_FAR, er))
  if math.abs(lift - M.lift) > 1e-4 then M.set_lift(lift) end
  if hidden then
    state.u_cv1.y = 0
    M.stats.active = 0
    for _, rn in ipairs(M.rains) do if rn.on then rn.on = false; msg.post(rn.url, MSG_DISABLE) end end
    return
  end
  -- (S23) adaptive quality: when frames are slow (a weak GPU, a software rasteriser) the march takes fewer
  -- samples, with a margin so it does not flicker between levels
  if dt > 0 then M.q_dt = M.q_dt + (math.min(dt, 1) - M.q_dt) * (dt > M.q_dt * 2 and 0.3 or 0.05) end
  if M.STEPS_FIXED then
    M.q_steps = M.STEPS_FIXED
  elseif M.q_dt > 0.12 and M.q_steps > 8 then M.q_steps = 8
  elseif M.q_dt > 0.045 and M.q_steps > 14 then M.q_steps = 14
  elseif M.q_dt < 0.03 and M.q_steps < M.STEPS then M.q_steps = (M.q_dt < 0.022) and M.STEPS or 14
  end
  state.u_cv1.y = M.q_steps
  local speed = require("game.platform.speed")
  -- (the world's own clock, not the last frame's: a flush right after a jump in time makes the map for now)
  local hf = (W.v3 and speed.v3_t) or state.hour_f or W.hour
  local hps = speed.hours_per_second() or 0
  local fast = hps > M.FAST_HPS
  -- (the noise's clock: hours within a long cycle - a whole number of advection periods)
  local cyc = M.PERIOD * 1024
  state.u_cv0.x = hf % cyc
  -- the next map, for a moment a little ahead: made while the map in force holds, then blended in from
  -- the moment it is ready to its own hour (so cover never jumps)
  local t0 = clockm.now_ms()
  local t_end = t0 + (instant and 1e9 or M.BUDGET_MS)
  if instant then M.job = nil end
  if not M.job and (not M.tb or hf >= M.tb or hf < (M.ta or hf) - 1 or instant) then
    local ahead = fast and max(6, hps * 0.5) or max(0.25, hps * 0.6)
    M.job = start_job(W, instant and hf or (hf + ahead), fast)
  end
  if M.job and run_job(W, M.job, t_end) then
    M.stamp = M.job.stamp
    M.ta = hf
    M.tb = max(M.job.t, hf + 1e-3)
    M.job = nil
    if instant then
      -- (the map in force is the new one at once)
      local st = M.covst
      for t = 1, M.CW * M.CH do
        local s4 = (t - 1) * 4
        st[s4 + 1], st[s4 + 3] = st[s4 + 2], st[s4 + 4]
      end
      resource.set_texture(M.cov, M.covparams, M.covbuf)
      M.tb = hf
    end
  end
  local blend = 1
  if M.ta and M.tb and M.tb > M.ta then blend = max(0, min(1, (hf - M.ta) / (M.tb - M.ta))) end
  state.u_cv1.x = blend
  M.stats.job_ms = clockm.now_ms() - t0
  M.stats.active = M.stats.cover > 0.002 and 1 or 0
  M.stats.puffs = 0
  M.rain_t = (M.rain_t or 0) + dt
  if M.rain_t >= 0.25 or instant then
    M.rain_t = 0
    M.update_rain(W, hf, fast)
  end
end

-- Rain/snow curtains: the cells nearest the view focus where it rains now (the same weather).
local cand, cand_d, cand_v = {}, {}, {}
function M.update_rain(W, hf, fast)
  local g = W.grid
  local n = 0
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  if not fast then
    local stamp = god_sky(W, hf, RFORCE, RSTAMP)
    for c = 1, W.n do
      local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
      if d > 0.55 then
        local v = rain_at(W, c, hf, RFORCE, RSTAMP, stamp)
        if v > 0 then n = n + 1; cand[n], cand_d[n], cand_v[n] = c, d, v end
      end
    end
  end
  M.stats.raining = n
  local m = min(n, #M.rains)
  for i = 1, m do
    local bi = i
    for j = i + 1, n do if cand_d[j] > cand_d[bi] then bi = j end end
    cand[i], cand[bi] = cand[bi], cand[i]
    cand_d[i], cand_d[bi] = cand_d[bi], cand_d[i]
    cand_v[i], cand_v[bi] = cand_v[bi], cand_v[i]
  end
  local R = state.R
  for i, rn in ipairs(M.rains) do
    local on = i <= m
    if on then
      local c = cand[i]
      local x, y, z = g.X[c], g.Y[c], g.Z[c]
      local snow = (W.F.temp[c] or 10) < 0
      if snow ~= rn.snow then rn.snow = snow; go.set(rn.url, "vertices", snow and M.snow_res or M.rain_res) end
      local sz = g.spacing * R * (cand_v[i] == 2 and 1.25 or 0.95)
      local r2 = R + M.BMIN + BASEN[c] * (M.BMAX - M.BMIN) + 0.02 + M.lift
      pos.x, pos.y, pos.z = x * r2, y * r2, z * r2
      vmx.set_up_yaw(rot, x, y, z, (c % 7) * 0.9)
      scl.x, scl.y, scl.z = sz, 1, sz
      go.set_position(pos, rn.id)
      go.set_rotation(rot, rn.id)
      go.set_scale(scl, rn.id)
    end
    if on ~= rn.on then rn.on = on; msg.post(rn.url, on and MSG_ENABLE or MSG_DISABLE) end
  end
  for i = 1, n do cand[i] = nil end
end

--- (bridge `clouds`) What the cover map holds at each village's cell: cover, darkness, base, the god's sky.
function M.probe(W)
  local out = { ta = M.ta, tb = M.tb, blend = state.u_cv1.x, cover = M.stats.cover, maps = M.stats.maps, lift = M.lift, villages = {} }
  for _, s in ipairs(W.S.v3 and W.S.v3.settlements or {}) do
    local c = s.cell
    out.villages[#out.villages + 1] = { name = s.name, cell = c, cover = COVC[c], dark = DRKC[c], base = M.BMIN + (BASEN[c] or 0) * (M.BMAX - M.BMIN) + M.lift,
      god = JSTAMP[c] == M.stamp and JFORCE[c] or nil, rain_now = s.rain_now }
  end
  return out
end

--- The map for now, at once (screenshots, tests: view.flush).
function M.flush(W)
  M.job = nil
  M.update(W, 0, true, true)
end

return M
