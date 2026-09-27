-- Clouds from the sim's weather (renderer v4, docs/world/16 §5, D-056).
--
-- Clouds are banks of many overlapping puffs, not objects: every puff is a flat-based spheroid
-- ray-traced on the GPU (game/render/shaders/cloud.vp/.fp), so neighbouring puffs merge into one
-- solid mass with a shared flat base, soft frayed rims and true depth.
--   * What: the day each place is living through (sim/society/weather_day.lua — the same days
--     the villagers work or shelter in): grey rain decks over wet days, heavy dark storm masses
--     (a broad heap over the deck, wider than tall) over storm days, white fair-weather banks
--     and lone heaps over dry days (more where the air is humid and where tomorrow's rain is
--     coming), a few high wisps over clear skies. The weekly precipitation drives them when the
--     sim runs so fast that days flash by. A continuous cover field (the cells' covers
--     interpolated, plus drifting low-frequency noise) decides where puffs stand, so banks span
--     several cells with organic outlines, holes and frayed edges; each sample of the field
--     grows a broad flat "pancake" with cauliflower lobes on top where the cover is thick.
--   * When: the planet is cut into longitude sectors. At normal speeds a sector is rebuilt once
--     per local day, at its local midnight (the swap happens in the dark), into one of its two
--     buffers: yesterday's puffs shrink away while today's grow in. At high speeds the sectors
--     are refreshed round-robin a few seconds apart from the week's precipitation. The GPU grows,
--     fades and drifts every puff with the zonal wind (a rotation about the polar axis at the
--     puff's own latitude rate), so the clouds cost no per-frame Lua work of their own.
--   * Altitude: the base floats above the highest ground and tallest trees around its cell (no
--     clipping); rain decks sit lowest, fair-weather banks higher, wisps high above.
--   * Rain and snow curtains hang under the decks and storms nearest the view.
-- Zoomed in, the shader opens a frayed hole in the deck between the camera and the view focus
-- (state.u_cut), so the sky around stays full.
local state = require("game.view.state")
local cfgmod = require("sim.config")
local wday = require("sim.society.weather_day")
local vnoise = require("game.view.vnoise")
local vmx = require("game.view.vmx")
local clockm = require("game.platform.clock")
local daylight = require("sim.society.daylight")

local M = {}

M.SECTORS = 16
M.ALT_MIN = 0.95          -- cloud base above sea level (world units) at the least
M.CLEAR = 0.72            -- clearance above the local ground (the tallest far-LOD trees are ~0.6)
M.DRIFT = 0.0025          -- radians per sim hour per unit zonal wind (~ half a cell a day)
M.FAST_HPS = 6            -- hours per second above which clouds follow weeks, not days
M.EXAG = 0.13             -- planet.lua P.EXAG (world units per km)
M.DENSITY = 1             -- sample density multiplier (gfx=low: fewer)
M.BUDGET_MS = 1.2         -- Lua time per frame for rebuilding sectors
M.RAIN = 110              -- rain/snow curtain pool
M.FADE_SLOW = 3           -- sim hours over which puffs grow in / shrink out at normal speeds
M.REFRESH_S = 8           -- real seconds for a full round of sector refreshes at high speeds
M.T = 0.61                -- cover threshold of the field

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local H_P1, H_P2, H_P3 = hash("puff"), hash("puff2"), hash("puff3")
local H_AABB = hash("AABB")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, abs, floor, pi = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.abs, math.floor, math.pi

M.sectors = {}
M.rains = {}
M.stats = { active = 0, cover = 0, puffs = 0, rebuilt = 0, job_ms = 0, ph = { 0, 0, 0 } }

-- triangle circumscribing the unit circle (the impostor's corners)
local CX = { 0, -1.7320508, 1.7320508 }
local CY = { 2, -1, -1 }

---------------------------------------------------------------------------------------------
-- Module-level scratch (no allocation while planning)

local EX, EY, EZ, NX, NY, NZ = {}, {}, {}, {}, {}, {}   -- tangent frames per cell
local DRIFTC, BASE = {}, {}                             -- drift rate (rad/h), cloud base (world, above R)
local COV, KIND, DARK, SNOW, SM, STAMP, SMSTAMP = {}, {}, {}, {}, {}, {}, {}
local P = {}          -- planned puffs, 13 numbers each
local NPUFF = 0
local serial = 0      -- job serial (stamps)
local rs = 1          -- LCG state for the planning randomness
local JF = { fast = false, ds = 0, born = 0, gone = 0, fade = 3, key = 0, nk = 0 }
local wx, wx2 = {}, {}

local function rnd()
  rs = (rs * 48271) % 2147483647
  return rs / 2147483647
end

local function seed_cell(c, key)
  local x = (c * 73856093 + key * 19349663 + 83492791) % 2147483647
  if x == 0 then x = 1 end
  rs = x
  rnd(); rnd()
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

-- Rain/snow curtain: crossed quads (rain streaks) or small flakes in a disc under the cloud.
local function rain_buffer(snow)
  local n = 110
  local nv = n * 12
  local buf = buffer.create(nv, {
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

local function new_buffer(n)
  return buffer.create(max(3, n * 3), {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_P1, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_P2, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_P3, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
end

--- Creates the sector buffers and the rain pool; plans every sector for the current hour.
function M.init(W, factory_url, rain_factory)
  M.W = W
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
  local g = W.grid
  local S = M.SECTORS
  if #M.sectors == 0 then
    for s = 1, S do
      local sec = { objs = {} }
      for k = 1, 2 do
        local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
        local url = msg.url(nil, id, "mesh")
        local res = resource.create_buffer("/tc/clouds_" .. s .. "_" .. k .. ".bufferc", { buffer = new_buffer(1) })
        go.set(url, "vertices", res)
        msg.post(url, MSG_DISABLE)
        sec.objs[k] = { id = id, url = url, res = res, on = false, n = 0, gone = 0, area = 0 }
      end
      M.sectors[s] = sec
    end
  end
  -- per-cell frames, drift and cloud base (above the highest ground and trees around the cell)
  local elev, sea = W.static.elev, W.G.sea_level or 0
  local zonal = cfgmod.MOIST.zonal
  for c = 1, W.n do
    local x, y, z = g.X[c], g.Y[c], g.Z[c]
    local ex, ez = z, -x
    local el = sqrt(ex * ex + ez * ez)
    if el < 1e-5 then ex, ez, el = 1, 0, 1 end
    ex, ez = ex / el, ez / el
    EX[c], EY[c], EZ[c] = ex, 0, ez
    NX[c], NY[c], NZ[c] = -ez * y, ez * x - ex * z, ex * y
    local lat = g.lat[c]
    DRIFTC[c] = prof(zonal, abs(lat) * 57.2958) * M.DRIFT / max(0.25, cos(lat))
    local m = elev[c] - sea
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local e = elev[g.nbr[b + k]] - sea
      if e > m then m = e end
    end
    BASE[c] = max(M.ALT_MIN, (m > 0 and m or 0) * M.EXAG + M.CLEAR)
    STAMP[c], SMSTAMP[c] = -1, -1
  end
  -- cells by sector (longitude); sample directions for the visibility test
  for s = 1, S do
    local sec = M.sectors[s]
    sec.cells = {}
    sec.lon0 = -pi + (s - 1) * 2 * pi / S
    sec.lon1 = sec.lon0 + 2 * pi / S
    sec.lonc = (sec.lon0 + sec.lon1) * 0.5
    sec.key, sec.mode, sec.cur = nil, nil, 1
    sec.wet_list = nil
    sec.pts = {}
    for li = -2, 2 do
      local lat = li * 0.6
      local cl = cos(lat)
      for q = 0, 2 do
        local lon = sec.lon0 + (sec.lon1 - sec.lon0) * q * 0.5
        local pt = sec.pts
        pt[#pt + 1] = cl * cos(lon); pt[#pt + 1] = sin(lat); pt[#pt + 1] = cl * sin(lon)
      end
    end
    for k = 1, 2 do sec.objs[k].n = 0 end
  end
  for c = 1, W.n do
    local s = floor((g.lon[c] + pi) / (2 * pi) * S) % S + 1
    local l = M.sectors[s].cells
    l[#l + 1] = c
  end
  M.noise = vnoise.new((W.seed or 1) * 31 + 7)
  M.epoch = floor((W.hour or 0) / 8064) * 8064
  M.job = nil
  M.rr = 0
  M.rain_t = 0
  M.update(W, 0, true, true)
end

---------------------------------------------------------------------------------------------
-- Weather -> cover per cell (stamped per job)

local function cell_weather(W, c)
  local cfgw = cfgmod.WEATHER_DAY
  local pw = W.F.precip[c] / cfgw.wet_week
  if pw > 1.5 then pw = 1.5 end
  local cov, kind, dark = 0, 1, 0
  local snow = W.F.temp[c] < 0
  if JF.fast then
    if pw > 1.05 then cov, kind, dark = min(0.9, 0.55 + (pw - 1.05) * 0.6), 2, min(0.45, (pw - 1.05) * 0.8)
    else cov = max(0, min(0.5, (pw - 0.2) * 0.6)) end
  else
    local d0 = wday.local_day_start(JF.ds + 12, W.grid.lon[c])
    local day = wday.get(W, c, d0, wx)
    snow = day.snow
    if day.wet then
      local rain = day.rain or 0
      if day.storm and rain >= cfgw.storm_day * 0.8 then
        cov, kind, dark = 0.95, 3, 0.72
      else
        cov, kind = min(0.9, 0.52 + rain * 10), 2
        dark = min(0.55, 0.15 + rain * 12)
      end
    else
      local nxt = wday.get(W, c, d0 + 24, wx2)
      cov = max(0, min(0.5, 0.02 + 0.32 * pw)) + (nxt.wet and 0.14 or 0)
      if day.gale then cov, dark = max(cov, 0.4), 0.12 end
    end
    if snow then dark = dark * 0.6 end
  end
  COV[c], KIND[c], DARK[c], SNOW[c] = cov, kind, dark, snow
  STAMP[c] = serial
end

local function weather_around(W, c)
  if STAMP[c] ~= serial then cell_weather(W, c) end
  local g = W.grid
  local b = c * 6 - 6
  local n = g.nbr_count[c]
  local s = 0
  for k = 1, n do
    local q = g.nbr[b + k]
    if STAMP[q] ~= serial then cell_weather(W, q) end
    s = s + COV[q]
  end
  -- smoothed cover: banks span neighbouring cells, edges fade out over a cell
  SM[c] = COV[c] * 0.55 + (s / n) * 0.45
  SMSTAMP[c] = serial
end

---------------------------------------------------------------------------------------------
-- Puff planning

local function add_puff(x, y, z, r, sq, h, shade, drift, op, dark, base_r)
  local n = NPUFF + 1
  NPUFF = n
  local o = n * 13 - 13
  -- x, y, z: unit direction of the base point; h: centre height above the base (world units)
  local F = JF.fade
  P[o + 1], P[o + 2], P[o + 3] = x, y, z
  P[o + 4], P[o + 5], P[o + 6] = r, sq, h
  P[o + 7], P[o + 8], P[o + 9] = shade, drift, op
  P[o + 10], P[o + 11], P[o + 12] = JF.born + rnd() * F * 0.8, JF.gone + rnd() * F * 0.8, dark
  P[o + 13] = base_r
end

-- point at (de, dn) radians east/north of unit dir (x, y, z), in the frame of cell c
local ox_, oy_, oz_ = 0, 0, 0
local function offset(c, x, y, z, de, dn)
  local px, py, pz = x + EX[c] * de + NX[c] * dn, y + EY[c] * de + NY[c] * dn, z + EZ[c] * de + NZ[c] * dn
  local l = sqrt(px * px + py * py + pz * pz)
  ox_, oy_, oz_ = px / l, py / l, pz / l
end

-- the continuous cover field at unit point (px, py, pz) near cell c; returns value, nearest cell
local function field(W, c, px, py, pz)
  local g = W.grid
  local sp2 = g.spacing * g.spacing
  local X, Y, Z = g.X, g.Y, g.Z
  local wsum, csum = 0, 0
  local best, bd = c, -2
  local b6 = c * 6 - 6
  for k = 0, g.nbr_count[c] do
    local q = k == 0 and c or g.nbr[b6 + k]
    local d = px * X[q] + py * Y[q] + pz * Z[q]
    if d > bd then best, bd = q, d end
    local a = (1 - d) / sp2              -- ~ half the squared distance in spacings
    local w = 1 / (0.05 + a * a)
    wsum = wsum + w
    csum = csum + w * (SMSTAMP[q] == serial and SM[q] or COV[q])
  end
  local N, nk = M.noise, JF.nk
  local n1 = vnoise.value(N, px * 3.4 + nk, py * 3.4 + nk * 0.7, pz * 3.4 - nk * 0.3)
  local n2 = vnoise.value(N, px * 9 - nk, py * 9 + nk * 0.3, pz * 9 + nk * 0.9)
  return csum / wsum + 0.72 * (n1 * 0.65 + n2 * 0.35 - 0.5), best
end

local function plan_cell(W, c, wet)
  local g = W.grid
  local dens = M.DENSITY
  local x, y, z = g.X[c], g.Y[c], g.Z[c]
  local sp = g.spacing                 -- radians
  local R = state.R
  local spw = sp * R                   -- world units across a cell
  local drift = DRIFTC[c]
  local T = M.T
  seed_cell(c, JF.key)
  local count0 = NPUFF
  local kc = KIND[c]
  -- stratified samples: the centre and two rings around it
  local npts = floor(7 * (0.5 + 0.5 * dens) + 0.5)
  for s = 1, npts do
    local rr, aa
    if s == 1 then rr, aa = 0.12 * rnd(), rnd() * 6.283
    elseif s <= 6 then rr, aa = 0.28 + 0.12 * rnd(), (s - 2 + rnd() * 0.6) / 5 * 6.283
    else rr, aa = 0.5 + 0.1 * rnd(), (s - 7 + rnd() * 0.7) / max(1, npts - 6) * 6.283 + 0.5 end
    offset(c, x, y, z, cos(aa) * rr * sp, sin(aa) * rr * sp)
    local px, py, pz = ox_, oy_, oz_
    local d, q = field(W, c, px, py, pz)
    if d > T then
      local kind = KIND[q] or 1
      local mg = min(1, (d - T) / 0.4)
      local dark = (DARK[q] or 0) * (0.85 + 0.3 * rnd())
      local base_r = R + BASE[q] + (kind == 1 and 0.1 or 0)
      if kind >= 2 or rnd() > 0.2 then
        -- a bank: a broad flat pancake per sample (neighbouring pancakes merge into one deck),
        -- cauliflower lobes rising from its top where the cover is thick, small lobes fraying
        -- its rim (rain decks and storms: grey, low, thick; fair-weather banks: white, higher)
        local deck = kind >= 2
        if not deck then dark = dark * 0.5 end
        local r = spw * (deck and (0.26 + 0.26 * mg) or (0.18 + 0.22 * mg)) * (0.8 + 0.4 * rnd())
        local sq = deck and (0.3 + 0.08 * rnd()) or (0.36 + 0.1 * rnd())
        local hh = r * sq * (-0.35 + 0.3 * mg)
        local top = hh + r * sq                            -- the pancake's top above the base
        add_puff(px, py, pz, r, sq, hh, 0.75 + 0.2 * mg, drift, 1, dark, base_r)
        local lobes = floor((deck and 0.5 or 1.3) + mg * 2.6 * (0.6 + 0.4 * rnd()))
        for _ = 1, lobes do
          local a2, d2 = rnd() * 6.283, sqrt(rnd()) * r * 0.62 / R
          offset(c, px, py, pz, cos(a2) * d2, sin(a2) * d2)
          local r2 = r * (0.28 + 0.26 * rnd()) * (0.7 + 0.5 * mg)
          local s2 = deck and 0.62 or 0.78
          add_puff(ox_, oy_, oz_, r2, s2, top - r2 * s2 * (0.55 - 0.35 * mg), 0.88 + 0.12 * rnd(), drift, 1, dark * 0.85, base_r)
        end
        if mg < 0.4 then
          -- fraying at thin edges: a small lobe at the rim
          for _ = 1, 1 do
            local a2 = rnd() * 6.283
            offset(c, px, py, pz, cos(a2) * r * 0.95 / R, sin(a2) * r * 0.95 / R)
            local r3 = r * (0.22 + 0.14 * rnd())
            add_puff(ox_, oy_, oz_, r3, 0.6, r3 * 0.1, 0.85, drift, 1, dark, base_r)
          end
        end
        if kind == 3 and mg > 0.4 and rnd() < 0.75 then
          -- storms heap up where they are thickest: a broad dark mass, wider than tall
          for k = 1, 3 do
            local a2, d2 = rnd() * 6.283, rnd() * r * 0.6 / R
            offset(c, px, py, pz, cos(a2) * d2, sin(a2) * d2)
            local r3 = r * (0.5 + 0.2 * rnd())
            add_puff(ox_, oy_, oz_, r3, 0.7, top + r3 * (0.1 + 0.25 * k), 0.85 + 0.05 * k, drift, 1, 0.5 + 0.2 * rnd(), base_r)
          end
        end
      else
        -- a lone fair-weather heap strung out along the wind, its big puff in the middle
        local S = spw * (0.06 + 0.09 * mg) * (0.85 + 0.3 * rnd())
        local elong = 1.2 + 0.8 * rnd()
        local ang = (rnd() - 0.5) * 0.8
        local ax, an = cos(ang), sin(ang)
        dark = dark * 0.6
        add_puff(px, py, pz, S, 0.8, S * (0.16 + 0.14 * mg), 1.0, drift, 1, dark, base_r)
        local n2 = 1 + floor(rnd() * 2 + mg * 2)
        for k = 1, n2 do
          local side = (k % 2 == 0) and 1 or -1
          local along = side * S * elong * (0.55 + 0.35 * rnd())
          local across = (rnd() - 0.5) * S * 0.7
          offset(c, px, py, pz, (ax * along - an * across) / R, (an * along + ax * across) / R)
          local r2 = S * (0.6 + 0.2 * rnd())
          add_puff(ox_, oy_, oz_, r2, 0.78, r2 * (0.04 + 0.14 * rnd()), 0.9 + 0.08 * rnd(), drift, 1, dark, base_r)
        end
        local n3 = 1 + floor(rnd() * 3)
        for _ = 1, n3 do
          local a2 = rnd() * 6.283
          local dd = S * (0.8 + 0.5 * rnd())
          offset(c, px, py, pz, cos(a2) * dd * elong / R, sin(a2) * dd / R)
          local r3 = S * (0.36 + 0.16 * rnd())
          add_puff(ox_, oy_, oz_, r3, 0.74, r3 * 0.05, 0.82 + 0.1 * rnd(), drift, 1, dark, base_r)
        end
      end
    end
  end
  if NPUFF == count0 and SM[c] < 0.12 and rnd() < 0.3 * dens then
    -- a high wisp over clear sky: a thin, see-through streak along the wind
    local base_r = R + BASE[c] + 0.6
    offset(c, x, y, z, (rnd() - 0.5) * sp, (rnd() - 0.5) * sp)
    local px, py, pz = ox_, oy_, oz_
    local ang = (rnd() - 0.5) * 0.6
    local ax, an = cos(ang), sin(ang)
    local nw = 4 + floor(rnd() * 4)
    local len = spw * (0.35 + 0.3 * rnd())
    for k = 1, nw do
      local f = (k - 0.5) / nw - 0.5
      local along = f * len
      local bend = (f * f - 0.08) * len * 0.5
      offset(c, px, py, pz, (ax * along - an * bend) / R, (an * along + ax * bend) / R)
      local r = spw * (0.05 + 0.035 * (1 - abs(f) * 1.6)) * (0.8 + 0.4 * rnd())
      add_puff(ox_, oy_, oz_, r, 0.3, 0, 1.0, drift, 0.4 + 0.2 * rnd(), 0, base_r)
    end
  end
  -- rain curtains under decks and storms
  if kc >= 2 and SM[c] > 0.5 and NPUFF > count0 and not JF.fast then
    wet[#wet + 1] = { c = c, alt = BASE[c], snow = SNOW[c], drift = drift, born = JF.born, gone = JF.gone + JF.fade, heavy = kc == 3 }
  end
end

---------------------------------------------------------------------------------------------
-- Sector jobs: weather, puffs, then the buffer written in batches and swapped in

local function start_job(s, key, fast, ds, born, gone, fade)
  local sec = M.sectors[s]
  serial = serial + 1
  NPUFF = 0
  JF.fast, JF.ds, JF.key = fast, ds, key
  JF.born, JF.gone, JF.fade = born, gone, fade
  JF.nk = (key % 997) * 3.1
  return { s = s, phase = 1, i = 1, target = sec.cur == 1 and 2 or 1, wet = {}, serial = serial }
end

local function run_job(W, J, deadline)
  local sec = M.sectors[J.s]
  local cells = sec.cells
  if J.serial ~= serial then return true end    -- superseded
  local ph = M.stats.ph
  local ta = clockm.now_ms()
  if J.phase == 1 then
    while J.i <= #cells do
      weather_around(W, cells[J.i])
      J.i = J.i + 1
      if J.i % 8 == 0 and clockm.now_ms() > deadline then ph[1] = ph[1] + clockm.now_ms() - ta; return false end
    end
    J.phase, J.i = 2, 1
    local tb = clockm.now_ms()
    ph[1], ta = ph[1] + tb - ta, tb
  end
  if J.phase == 2 then
    while J.i <= #cells do
      plan_cell(W, cells[J.i], J.wet)
      J.i = J.i + 1
      if J.i % 4 == 0 and clockm.now_ms() > deadline then ph[2] = ph[2] + clockm.now_ms() - ta; return false end
    end
    J.n = NPUFF
    J.buf = new_buffer(J.n)
    J.pos = buffer.get_stream(J.buf, H_POS)
    J.nrm = buffer.get_stream(J.buf, H_NRM)
    J.p1 = buffer.get_stream(J.buf, H_P1)
    J.p2 = buffer.get_stream(J.buf, H_P2)
    J.p3 = buffer.get_stream(J.buf, H_P3)
    J.phase, J.i, J.area = 3, 1, 0
    local tb = clockm.now_ms()
    ph[2], ta = ph[2] + tb - ta, tb
    if tb > deadline then return false end
  end
  if J.phase == 3 then
    local pos, nrm, p1, p2, p3 = J.pos, J.nrm, J.p1, J.p2, J.p3
    local n = J.n
    local cx1, cx2, cx3, cy1, cy2, cy3 = CX[1], CX[2], CX[3], CY[1], CY[2], CY[3]
    local area = J.area
    while J.i <= n do
      local i = J.i
      local o = i * 13 - 13
      local x, y, z = P[o + 1], P[o + 2], P[o + 3]
      local r, sq, hh = P[o + 4], P[o + 5], P[o + 6]
      local shade, drift, op = P[o + 7], P[o + 8], P[o + 9]
      local born, gone, dark = P[o + 10], P[o + 11], P[o + 12]
      local cr = P[o + 13] + hh
      local cx, cy, cz = x * cr, y * cr, z * cr
      local seed = (i * 0.61803) % 1
      local v3, v4 = (i - 1) * 9, (i - 1) * 12
      pos[v3 + 1], pos[v3 + 2], pos[v3 + 3] = cx, cy, cz
      pos[v3 + 4], pos[v3 + 5], pos[v3 + 6] = cx, cy, cz
      pos[v3 + 7], pos[v3 + 8], pos[v3 + 9] = cx, cy, cz
      nrm[v3 + 1], nrm[v3 + 2], nrm[v3 + 3] = x, y, z
      nrm[v3 + 4], nrm[v3 + 5], nrm[v3 + 6] = x, y, z
      nrm[v3 + 7], nrm[v3 + 8], nrm[v3 + 9] = x, y, z
      p1[v4 + 1], p1[v4 + 2], p1[v4 + 3], p1[v4 + 4] = cx1, cy1, r, sq
      p1[v4 + 5], p1[v4 + 6], p1[v4 + 7], p1[v4 + 8] = cx2, cy2, r, sq
      p1[v4 + 9], p1[v4 + 10], p1[v4 + 11], p1[v4 + 12] = cx3, cy3, r, sq
      p2[v4 + 1], p2[v4 + 2], p2[v4 + 3], p2[v4 + 4] = -hh, shade, drift, op
      p2[v4 + 5], p2[v4 + 6], p2[v4 + 7], p2[v4 + 8] = -hh, shade, drift, op
      p2[v4 + 9], p2[v4 + 10], p2[v4 + 11], p2[v4 + 12] = -hh, shade, drift, op
      p3[v4 + 1], p3[v4 + 2], p3[v4 + 3], p3[v4 + 4] = born, gone, dark, seed
      p3[v4 + 5], p3[v4 + 6], p3[v4 + 7], p3[v4 + 8] = born, gone, dark, seed
      p3[v4 + 9], p3[v4 + 10], p3[v4 + 11], p3[v4 + 12] = born, gone, dark, seed
      area = area + r * r
      J.i = i + 1
      if i % 48 == 0 and clockm.now_ms() > deadline then J.area = area; ph[3] = ph[3] + clockm.now_ms() - ta; return false end
    end
    ph[3] = ph[3] + clockm.now_ms() - ta
    -- swap into the older buffer of the sector
    local o = sec.objs[J.target]
    local R = state.R + 3
    buffer.set_metadata(J.buf, H_AABB, { -R, -R, -R, R, R, R }, buffer.VALUE_TYPE_FLOAT32)
    resource.set_buffer(o.res, J.buf, { transfer_ownership = true })
    o.n = n
    o.gone = JF.gone + JF.fade
    o.area = area
    sec.cur = J.target
    sec.wet_list = J.wet
    M.stats.rebuilt = M.stats.rebuilt + 1
    return true
  end
  return false
end

---------------------------------------------------------------------------------------------
-- Per frame

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

local function sector_visible(sec, ex, ey, ez)
  local pt = sec.pts
  for i = 1, #pt, 3 do
    if pt[i] * ex + pt[i + 1] * ey + pt[i + 2] * ez > -0.25 then return true end
  end
  return false
end

--- Per-frame: sector rebuilds, visibility, rain curtains, shader inputs.
-- frozen: screenshots and tests (stale sectors are rebuilt at once). instant: finish all now.
function M.update(W, dt, frozen, instant)
  if not M.sectors[1] or not M.sectors[1].cells then return end
  local speed = require("game.platform.speed")
  local hf = state.hour_f or W.hour
  if hf < M.epoch or hf - M.epoch > 60000 then
    M.epoch = floor(hf / 8064) * 8064
    for _, sec in ipairs(M.sectors) do sec.key = nil; for k = 1, 2 do sec.objs[k].n = 0 end end
    M.job = nil
  end
  local hps = speed.hours_per_second() or 0
  local fast = hps > M.FAST_HPS
  local fade = fast and max(M.FADE_SLOW, hps * 0.6) or M.FADE_SLOW
  local hrel = hf - M.epoch
  local uc = state.u_cloud
  if uc then uc.x, uc.y = hrel, fade end
  local ex, ey, ez = state.eye.x, state.eye.y, state.eye.z
  local el = sqrt(ex * ex + ey * ey + ez * ez)
  ex, ey, ez = ex / el, ey / el, ez / el
  instant = instant or frozen
  local t0 = clockm.now_ms()
  local t_end = t0 + (instant and 1e9 or M.BUDGET_MS)
  M.rr = (M.rr or 0) + dt
  local mode = fast and 1 or 0
  for _ = 1, instant and 64 or 3 do
    if not M.job then
      local pick, best = nil, -1e9
      for s, sec in ipairs(M.sectors) do
        local key, ds
        if fast then
          -- round-robin refreshes from the week's weather, one sector per slot
          key = sec.key or 0
          if sec.mode ~= 1 or (M.rr >= M.REFRESH_S / M.SECTORS and s == (M.rr_next or 1)) then
            key = floor(hf) + s
          end
          ds = floor(hf)
        else
          local b = daylight.midnight_bucket(sec.lonc)
          key = floor((hf - b) / 24)
          ds = key * 24 + b
        end
        sec.want_key, sec.want_ds = key, ds
        if sec.key ~= key or sec.mode ~= mode then
          local pr = (sector_visible(sec, ex, ey, ez) and 10 or 0) - s * 0.01
          if pr > best then pick, best = s, pr end
        end
      end
      if not pick then break end
      local sec = M.sectors[pick]
      sec.key, sec.mode = sec.want_key, mode
      local born, gone
      if fast then
        M.rr = 0
        M.rr_next = pick % M.SECTORS + 1
        born = hrel
        gone = hrel + max(168, hps * M.REFRESH_S * 1.25)
      else
        born = sec.want_ds - M.epoch
        gone = born + 24
      end
      M.job = start_job(pick, sec.want_key, fast, sec.want_ds, born, gone, fade)
    end
    if run_job(W, M.job, t_end) then
      M.job = nil
    elseif not instant then
      break
    end
    if clockm.now_ms() > t_end then break end
  end
  M.stats.job_ms = clockm.now_ms() - t0
  -- visibility of the sector buffers
  local active, area = 0, 0
  local hidden = M.hidden
  for _, sec in ipairs(M.sectors) do
    local vis = not hidden and sector_visible(sec, ex, ey, ez)
    for k = 1, 2 do
      local o = sec.objs[k]
      local on = vis and o.n > 0 and o.gone > hrel
      if on ~= o.on then
        o.on = on
        msg.post(o.url, on and MSG_ENABLE or MSG_DISABLE)
      end
      if on and k == sec.cur then active, area = active + o.n, area + o.area end
    end
  end
  M.stats.active = active
  M.stats.puffs = active
  M.stats.cover = area * pi * 0.7 / (4 * pi * state.R * state.R)
  M.rain_t = (M.rain_t or 0) + dt
  if M.rain_t >= 0.2 or instant then
    M.rain_t = 0
    M.update_rain(W, hrel)
  end
  -- the cut-away: clouds between the camera and the view focus open up (cloud.fp)
  local zoom = require("game.view.camera").zoom
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local cut = state.u_cut
  if cut then
    local rs2 = state.R + 0.2
    cut.x, cut.y, cut.z = fx * rs2, fy * rs2, fz * rs2
    cut.w = min(1, max(0, (zoom - 0.25) * 2.5))
  end
end

-- Rain/snow curtains: the wet cells of the current day nearest the view focus.
local cand, cand_d = {}, {}
function M.update_rain(W, hrel)
  local n = 0
  local g = W.grid
  if not M.hidden then
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    for _, sec in ipairs(M.sectors) do
      local wl = sec.wet_list
      if wl then
        for i = 1, #wl do
          local w = wl[i]
          if hrel >= w.born and hrel < w.gone - 1 then
            local c = w.c
            local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
            if d > 0.55 then
              n = n + 1
              cand[n], cand_d[n] = w, d
            end
          end
        end
      end
    end
  end
  -- the nearest first (partial selection: the pool is small)
  local m = min(n, #M.rains)
  for i = 1, m do
    local bi = i
    for j = i + 1, n do if cand_d[j] > cand_d[bi] then bi = j end end
    cand[i], cand[bi] = cand[bi], cand[i]
    cand_d[i], cand_d[bi] = cand_d[bi], cand_d[i]
  end
  local R = state.R
  for i, rn in ipairs(M.rains) do
    local w = i <= m and cand[i] or nil
    local on = w ~= nil
    if on then
      local c = w.c
      local a = w.drift * (hrel - w.born)
      local x0, y0, z0 = g.X[c], g.Y[c], g.Z[c]
      local ca, sa = cos(a), sin(a)
      local x, y, z = x0 * ca - z0 * sa, y0, x0 * sa + z0 * ca
      if w.snow ~= rn.snow then rn.snow = w.snow; go.set(rn.url, "vertices", w.snow and M.snow_res or M.rain_res) end
      local sz = g.spacing * R * (w.heavy and 1.25 or 0.95)
      local r2 = R + w.alt + 0.02
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

return M
