-- Clouds from the sim's weather (renderer v3, docs/world/16 §5): a pool of cloud clusters placed
-- where the weather is — today's wet/dry day and rain amount per cell from the sim's daily
-- weather (sim/society/weather_day.lua, the same days the villagers work or shelter in), the
-- week's precipitation and moisture when the sim runs so fast that days flash by. Each cloud is
-- keyed to a (cell, day) and drifts with the zonal wind through that day, so what you see is
-- where it is raining and what the people under it are living through:
--   fair-weather cumulus over humid dry days, grey rain decks over wet days (rain curtains, snow
--   when freezing), dark storm towers over storm days.
-- Altitude: a cloud floats above the highest ground and the tallest trees around its cell (never
-- clipping terrain, forests or houses). Zoomed in, clouds are not shrunk: the cloud shader cuts
-- away only what sits between the camera and the view focus (state.u_cut), so the sky around
-- stays full. The pool is spent on the visible hemisphere.
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local cfgmod = require("sim.config")
local vmx = require("game.view.vmx")
local wday = require("sim.society.weather_day")

local M = {}

M.COUNT = 120
M.ALT_MIN = 1.05          -- cloud base above sea level (world units) at the least
M.CLEAR = 0.85            -- clearance above the local ground (the tallest far-LOD trees are ~0.6)
M.RESCAN = 1.0            -- seconds between placement rescans
M.DRIFT = 0.012           -- radians per sim hour per unit zonal wind (a third of a cell a day)
M.FAST_HPS = 12           -- hours per second above which clouds follow weeks, not days
M.EXAG = 0.13             -- planet.lua P.EXAG (world units per km)

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, abs, floor, exp = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.abs, math.floor, math.exp

M.clouds = {}
M.rains = {}
M.stats = { active = 0, cover = 0, kinds = {} }

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

-- deterministic fractions per (cell, key, salt)
local function h01(c, key, salt)
  local x = (c * 73856093 + key * 19349663 + salt * 83492791) % 2147483647
  x = (x * 48271) % 2147483647
  x = (x * 48271) % 2147483647
  return x / 2147483647
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
  for i = 1, n do
    local a, r = u() * 6.283, math.sqrt(u()) * 0.45
    local x, z = math.cos(a) * r, math.sin(a) * r
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

local function cloud_res(kind, rgb, name)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), rgb, nil, 1)
  return resource.create_buffer("/tc/" .. name .. ".bufferc", { buffer = buf })
end

-- Highest ground around each cell (km above sea level: the cell and its neighbours).
local function ceilings(W)
  local g = W.grid
  local elev, sea = W.static.elev, W.G.sea_level or 0
  local out = {}
  for c = 1, W.n do
    local m = elev[c] - sea
    local b = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local e = elev[g.nbr[b + k]] - sea
      if e > m then m = e end
    end
    out[c] = m > 0 and m or 0
  end
  return out
end

--- Creates the pool and the shared cloud geometry.
function M.init(W, factory_url, rain_factory)
  M.W = W
  if #M.rains == 0 and rain_factory then
    M.rain_res = resource.create_buffer("/tc/rain.bufferc", { buffer = rain_buffer(false) })
    M.snow_res = resource.create_buffer("/tc/snow.bufferc", { buffer = rain_buffer(true) })
    for i = 1, M.COUNT do
      local id = factory.create(rain_factory, vmath.vector3(0, 0, 0), nil, nil, vmath.vector3(0.001))
      local url = msg.url(nil, id, "mesh")
      go.set(url, "vertices", M.rain_res)
      msg.post(url, MSG_DISABLE)
      M.rains[i] = { id = id, url = url, on = false, snow = false }
    end
  end
  if #M.clouds == 0 then
    M.res = {
      cu1 = cloud_res("cloud_cu1", { 1, 1, 1 }, "cloud_cu1"),
      cu2 = cloud_res("cloud_cu2", { 0.98, 0.98, 1 }, "cloud_cu2"),
      deck = cloud_res("cloud_deck", { 0.80, 0.82, 0.86 }, "cloud_deck"),
      tower = cloud_res("cloud_tower", { 0.60, 0.62, 0.68 }, "cloud_tower"),
    }
    for i = 1, M.COUNT do
      local id = factory.create(factory_url, vmath.vector3(0, 0, 0), nil, nil, vmath.vector3(0.001))
      local url = msg.url(nil, id, "mesh")
      go.set(url, "vertices", M.res.cu1)
      msg.post(url, MSG_DISABLE)
      M.clouds[i] = { id = id, url = url, s = 0, target = 0, kind = "cu1", shown = false, key = nil }
    end
  end
  M.ceil = ceilings(W)
  for _, c in ipairs(M.clouds) do c.s, c.target, c.key, c.cell = 0, 0, nil, nil end
  M.since = 1e9
  M.scan(W, true)
end

local wx = {}

-- Choose the clouds for the visible hemisphere: every cell under weather gets a weight (today's
-- rain, storms, humid dry days); the heaviest cells (with a per-day jitter so decks are ragged)
-- get a cloud. Slots keep their (cell, key) while it stays chosen; others fade out and are reused.
function M.scan(W, instant)
  local F, g = W.F, W.grid
  local hf = state.hour_f or W.hour
  local fast = (require("game.platform.speed").hours_per_second() or 0) > M.FAST_HPS
  local ex, ey, ez = state.eye.x, state.eye.y, state.eye.z
  local el = sqrt(ex * ex + ey * ey + ez * ez)
  ex, ey, ez = ex / el, ey / el, ez / el
  local cfgw = cfgmod.WEATHER_DAY
  local cand = {}
  for c = 1, W.n do
    local d = g.X[c] * ex + g.Y[c] * ey + g.Z[c] * ez
    if d > -0.05 then
      local P = F.precip[c]
      local pw = P / cfgw.wet_week
      local w, kind = 0, nil
      local key
      if fast then
        key = floor(hf / 168)
        if pw > 1.2 then w, kind = 1.2 + pw * 0.3, "deck"
        elseif pw > 0.35 then w, kind = pw, (h01(c, key, 3) < 0.5 and "cu1" or "cu2") end
      else
        key = floor(hf / 24)
        local day = wday.for_local_day(W, c, hf, wx)
        if day.storm and day.wet then w, kind = 3 + day.rain * 20, "tower"
        elseif day.wet then w, kind = 1.4 + day.rain * 25, "deck"
        elseif pw > 0.2 then w, kind = 0.3 + pw * 0.8, (h01(c, key, 3) < 0.5 and "cu1" or "cu2") end
      end
      if kind then
        -- the pool goes to the face of the planet you look at (the limb gets what is left)
        w = w * (0.6 + 0.8 * h01(c, key, 1)) * (0.35 + 0.65 * (d > 0 and d or 0))
        if w > 0.25 then cand[#cand + 1] = { c = c, w = w, kind = kind, key = key, rain = not fast and wx.wet, snow = F.temp[c] < 0 } end
      end
    end
  end
  table.sort(cand, function(a, b) if a.w ~= b.w then return a.w > b.w end return a.c < b.c end)
  local want = {}
  local nmax = min(#cand, M.MAX_ACTIVE or M.COUNT)
  for i = 1, nmax do
    local e = cand[i]
    want[e.c * 1e6 + e.key] = e
  end
  -- keep slots that are still wanted, release the rest
  local placed = {}
  for _, cl in ipairs(M.clouds) do
    local e = cl.cell and want[cl.cell * 1e6 + (cl.key or -1)]
    if e then
      placed[cl.cell * 1e6 + cl.key] = true
      cl.target, cl.kind_want, cl.rain, cl.snow = 1, e.kind, e.rain, e.snow
    else
      cl.target = 0
    end
  end
  local free = 1
  for i = 1, nmax do
    local e = cand[i]
    local k = e.c * 1e6 + e.key
    if not placed[k] then
      while M.clouds[free] and (M.clouds[free].target > 0 or M.clouds[free].s > 0.02) do free = free + 1 end
      local cl = M.clouds[free]
      if not cl then break end
      cl.cell, cl.key, cl.target, cl.kind_want = e.c, e.key, 1, e.kind
      cl.rain, cl.snow = e.rain, e.snow
      cl.s = instant and 1 or 0
      cl.t0 = fast and e.key * 168 or e.key * 24
      -- where in its cell, how big, which way
      local sp = g.spacing
      cl.dlat = (h01(e.c, e.key, 5) - 0.5) * sp * 0.9
      cl.dlon = (h01(e.c, e.key, 6) - 0.5) * sp * 0.9
      cl.size = (e.kind == "tower" and 1.25 or (e.kind == "deck" and 1.35 or 0.8)) * (0.8 + 0.5 * h01(e.c, e.key, 7))
      cl.yaw = h01(e.c, e.key, 8) * 6.283
      local ce = M.ceil[e.c] or 0
      cl.alt = max(M.ALT_MIN, ce * M.EXAG + M.CLEAR) + 0.08 * h01(e.c, e.key, 9)
      free = free + 1
    end
  end
end

local pos, rot, scl = vmath.vector3(), vmath.quat(), vmath.vector3()

function M.update(W, dt, frozen)
  local zonal = cfgmod.MOIST.zonal
  local R = state.R
  local g = W.grid
  M.since = (M.since or 0) + dt
  if M.since > M.RESCAN then
    M.since = 0
    M.scan(W, frozen)   -- frozen visuals (screenshots): new clouds appear at once
  end
  local hf = state.hour_f or W.hour
  local active, area = 0, 0
  local kinds = M.stats.kinds
  kinds.cu1, kinds.cu2, kinds.deck, kinds.tower = 0, 0, 0, 0
  M.frame = (M.frame or 0) + 1
  local par = M.frame % 2
  local k = 1 - exp(-dt * 2 * 1.2)
  for i, c in ipairs(M.clouds) do
    local mine = i % 2 == par
    if mine and not frozen then
      c.s = c.s + (c.target - c.s) * k
      if c.target == 0 and c.s < 0.02 then c.s = 0 end
    end
    local show = c.cell ~= nil and c.s > 0.02 and not M.hidden
    if show and (mine or not c.shown) then
      if c.kind_want and c.kind_want ~= c.kind and (c.s < 0.3 or not c.shown) then
        c.kind = c.kind_want
        go.set(c.url, "vertices", M.res[c.kind])
      end
      local lat0 = g.lat[c.cell] + c.dlat
      local u = prof(zonal, abs(lat0) * 57.2958)
      local lon = g.lon[c.cell] + c.dlon + u * M.DRIFT * (hf - (c.t0 or hf)) / max(0.2, cos(lat0))
      local cl = cos(lat0)
      local x, y, z = cl * cos(lon), sin(lat0), cl * sin(lon)
      local r = R + c.alt
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      vmx.set_up_yaw(rot, x, y, z, c.yaw)
      local sz = c.size * (0.35 + 0.65 * c.s)
      scl.x, scl.y, scl.z = sz, sz * (0.6 + 0.4 * c.s), sz
      go.set_position(pos, c.id)
      go.set_rotation(rot, c.id)
      go.set_scale(scl, c.id)
      local rn = M.rains[i]
      if rn then
        local raining = c.rain and c.s > 0.5 and (c.kind == "deck" or c.kind == "tower")
        if raining then
          if c.snow ~= rn.snow then rn.snow = c.snow; go.set(rn.url, "vertices", c.snow and M.snow_res or M.rain_res) end
          -- the curtain reaches from the cloud base down to the ground
          scl.x, scl.y, scl.z = sz, 1, sz
          local r2 = R + c.alt - 0.02
          pos.x, pos.y, pos.z = x * r2, y * r2, z * r2
          go.set_position(pos, rn.id)
          go.set_rotation(rot, rn.id)
          go.set_scale(scl, rn.id)
        end
        if raining ~= rn.on then rn.on = raining; msg.post(rn.url, raining and MSG_ENABLE or MSG_DISABLE) end
      end
    end
    if show ~= c.shown then
      c.shown = show
      msg.post(c.url, show and MSG_ENABLE or MSG_DISABLE)
      local rn = M.rains[i]
      if not show and rn and rn.on then rn.on = false; msg.post(rn.url, MSG_DISABLE) end
    end
    if show then
      active = active + 1
      area = area + math.pi * (c.size * 0.55) ^ 2
      kinds[c.kind] = (kinds[c.kind] or 0) + 1
    end
  end
  M.stats.active = active
  M.stats.cover = area / (4 * math.pi * R * R)
  -- the cut-away: clouds between the camera and the view focus fade (cloud.fp), more when close
  local zoom = require("game.view.camera").zoom
  local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
  local cut = state.u_cut
  if cut then
    local rs = R + 0.2
    cut.x, cut.y, cut.z = fx * rs, fy * rs, fz * rs
    cut.w = min(1, max(0, (zoom - 0.25) * 2.5))
  end
end

return M
