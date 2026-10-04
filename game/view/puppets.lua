-- Puppets: pooled movers driven by Lua, world-space material (engine-batched).
--   * people (W2c, docs/world/16 §4): residents of the villages near the view focus, placed
--     exactly where the sim's schedule puts them (sim/society/schedule.lua — the same pure function
--     the behaviour tests use): walking from their home to their field, woodlot, building site or
--     boat at the hours the village's day allows, working there, walking back, gathering at the
--     well in the evening; asleep indoors at night (not drawn), indoors in a storm. Tunic and hat
--     by occupation; walking and working poses.
--   * travelling parties: migrants and colonists walking their route (schedule.party_pos), strung
--     out along the path by day, camped at night.
--   * boats: each village's fishing boats, moored by the pier unless the sim planned a trip for
--     today (b.trip: depart -> at the ground -> leave -> return), when they sail out, fish and come
--     home at the hours the trip says.
--   * tankers: the merchant tankers on their lanes (W.ent.ships).
--   * animals: deer/wolves near cells with high grazers/predators density when zoomed in (the
--     fauna fields; herds become entities in W4).
-- Positions are exposed for picking (M.people_pos / M.animal_pos / M.ship_pos).
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local speed = require("game.platform.speed")
local vmx = require("game.view.vmx")
local ents = require("game.view.ents")
local schedule = require("sim.society.schedule")
local layout = require("sim.society.layout")

local M = {}

M.DEER, M.WOLVES, M.PEOPLE, M.TANKERS, M.BOATS = 150, 44, 360, 8, 64
M.DOLPHINS = 14
M.STOCK = 120          -- livestock puppets (the herds of the villages near the view)
M.BIRDS = 96
M.BIRD_ZOOM = 0.5      -- birds show from this zoom in
M.MIN_ZOOM = 0.5
M.PER_VILLAGE = 110
M.SCALE = 1.3
M.PERSON_SCALE = 1.7   -- villagers a little oversized (toy-like) so they read among the houses
M.FOCUS_DOT = 0.95     -- villages within ~18 degrees of the view focus get people

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, floor, atan2, acos = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.floor, math.atan2, math.acos

-- Job tints (06 §1: people = body + head; job tints). Never colour alone: the inspector
-- always shows the job name and icon too.
M.JOB_COLORS = {
  farmer = { 0.89, 0.76, 0.35 }, fisher = { 0.25, 0.72, 0.79 }, hunter = { 0.55, 0.35, 0.24 },
  woodcutter = { 0.18, 0.55, 0.29 }, builder = { 0.95, 0.42, 0.36 }, trader = { 0.36, 0.55, 0.72 },
  elder = { 0.70, 0.62, 0.86 }, child = { 0.96, 0.56, 0.69 }, healer = { 0.98, 0.98, 0.95 },
  default = { 0.96, 0.92, 0.86 },
}
local SKINS = { { 0.98, 0.84, 0.70 }, { 0.87, 0.67, 0.50 }, { 0.62, 0.44, 0.31 } }
local HAIR = { farmer = { 0.85, 0.72, 0.38 }, fisher = { 0.22, 0.30, 0.42 }, default = { 0.33, 0.24, 0.17 },
  elder = { 0.86, 0.86, 0.84 }, child = { 0.55, 0.36, 0.20 } }
local HAT = { farmer = "straw", fisher = "cap", hunter = "cap" }

M.pool = {}           -- animals
M.dolphins = {}       -- dolphins over rich fishing waters
M.stock = {}          -- livestock
-- Wild species by landscape (D-058): model, colours (body, dark parts, accents), scale, speed.
M.SPECIES = {
  deer = { model = "deer", c1 = { 0.70, 0.48, 0.30 }, c2 = { 0.40, 0.29, 0.21 }, c3 = { 0.86, 0.80, 0.70 }, scale = 1, speed = 1 },
  reindeer = { model = "deer", c1 = { 0.55, 0.50, 0.44 }, c2 = { 0.30, 0.27, 0.24 }, c3 = { 0.92, 0.90, 0.86 }, scale = 1.12, speed = 0.9 },
  antelope = { model = "deer", c1 = { 0.80, 0.60, 0.36 }, c2 = { 0.28, 0.22, 0.18 }, c3 = { 0.25, 0.2, 0.17 }, scale = 0.92, speed = 1.3 },
  boar = { model = "boar", c1 = { 0.36, 0.28, 0.23 }, c2 = { 0.24, 0.19, 0.16 }, c3 = { 0.9, 0.86, 0.78 }, scale = 1, speed = 0.8 },
  hare = { model = "hare", c1 = { 0.66, 0.52, 0.38 }, c2 = { 0.5, 0.4, 0.3 }, c3 = { 0.95, 0.93, 0.9 }, scale = 1, speed = 1.5 },
  wolf = { model = "wolf", c1 = { 0.52, 0.54, 0.58 }, c2 = { 0.33, 0.34, 0.37 }, c3 = { 0.3, 0.3, 0.32 }, scale = 1, speed = 1.6 },
  fox = { model = "wolf", c1 = { 0.85, 0.45, 0.2 }, c2 = { 0.25, 0.18, 0.14 }, c3 = { 0.95, 0.92, 0.88 }, scale = 0.62, speed = 1.8 },
  cat = { model = "cat", c1 = { 0.80, 0.62, 0.36 }, c2 = { 0.5, 0.36, 0.2 }, c3 = { 0.3, 0.22, 0.16 }, scale = 1, speed = 1.3 },
  bear = { model = "bear", c1 = { 0.40, 0.29, 0.20 }, c2 = { 0.22, 0.16, 0.12 }, c3 = { 0.3, 0.22, 0.16 }, scale = 1.1, speed = 0.7 },
}
M.people = {}         -- person puppets (residents and travellers)
M.ships = {}          -- ship puppets
M.stats = { active = 0, people = 0, ships = 0, travellers = 0 }
M.people_pos = {}     -- { id, x, y, z } for picking (visible puppets only)
M.animal_pos = {}     -- { kind, cell, x, y, z }
M.ship_pos = {}       -- { index, list, kind, x, y, z }
M.birds = {}          -- bird puppets
M.flocks = {}         -- { x, y, z, n, r, alt, kind } flock centres (from the sim, see assign_birds)

-- still = true: no limb codes (standing / working). lscale: limb data for the walk cycle.
local function make_buffer(kind, c1, c2, scale, c3, lscale, still)
  local t = propgen.mover_template(kind)
  local limbs = lscale ~= nil and not M.NO_LIMBS
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = limbs and 4 or 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, scale or 1, c3, limbs, lscale, still)
  return buf
end

local seed_h = 7
local function rnd()
  seed_h = (seed_h * 48271) % 2147483647
  return seed_h / 2147483647
end

local job_res = {}
local job_res_still = {}
local res_gen = 0

local function make_pool(out, factory_url, n, kind, buf)
  for i = 1, n do
    local id = factory.create(factory_url, vmath.vector3(0, 0, 0))
    local url = msg.url(nil, id, "mesh")
    if i == 1 and buf then resource.set_buffer(go.get(url, "vertices"), buf) end
    msg.post(url, MSG_DISABLE)
    out[#out + 1] = { id = id, url = url, kind = kind, on = false, cell = 0 }
  end
end

function M.init(W, urls, surface)
  M.W, M.surface = W, surface
  if #M.pool == 0 then
    make_pool(M.pool, urls.deer_factory, M.DEER, "deer", make_buffer("deer", { 0.70, 0.48, 0.30 }, { 0.40, 0.29, 0.21 }, 1,
      { 0.86, 0.80, 0.70 }, M.SCALE))
    make_pool(M.pool, urls.wolf_factory, M.WOLVES, "wolf", make_buffer("wolf", { 0.52, 0.54, 0.58 }, { 0.33, 0.34, 0.37 }, 1,
      { 0.3, 0.3, 0.32 }, M.SCALE))
    -- wild species by landscape (the sim's grazers and predators, drawn as the animals of their
    -- biome): one buffer resource each, swapped onto the pooled animals
    local SP = M.SPECIES
    for name, sp in pairs(SP) do
      sp.res = resource.create_buffer("/tc/animal_" .. name .. ".bufferc",
        { buffer = make_buffer(sp.model, sp.c1, sp.c2, 1, sp.c3, M.SCALE) })
      -- (standing, grazing or lying: no walk cycle; v3 wild animals, D-093)
      sp.res_still = resource.create_buffer("/tc/animal_" .. name .. "_still.bufferc",
        { buffer = make_buffer(sp.model, sp.c1, sp.c2, 1, sp.c3, M.SCALE, true) })
    end
    make_pool(M.dolphins, urls.deer_factory, M.DOLPHINS, "dolphin", nil)
    -- livestock (W10)
    M.stock_res = {
      sheep = resource.create_buffer("/tc/stock_sheep.bufferc", { buffer = make_buffer("sheep", { 0.93, 0.91, 0.86 }, { 0.25, 0.22, 0.2 }, 1, { 0.9, 0.9, 0.9 }, M.SCALE) }),
      goats = resource.create_buffer("/tc/stock_goat.bufferc", { buffer = make_buffer("goat", { 0.45, 0.33, 0.24 }, { 0.2, 0.17, 0.15 }, 1, { 0.8, 0.78, 0.7 }, M.SCALE) }),
      cattle = resource.create_buffer("/tc/stock_cow.bufferc", { buffer = make_buffer("cow", { 0.55, 0.36, 0.24 }, { 0.3, 0.22, 0.16 }, 1, { 0.94, 0.92, 0.88 }, M.SCALE) }),
    }
    make_pool(M.stock, urls.deer_factory, M.STOCK, "stock", nil)
    M.dolphin_res = resource.create_buffer("/tc/animal_dolphin.bufferc",
      { buffer = make_buffer("dolphin", { 0.50, 0.56, 0.64 }, { 0.5, 0.56, 0.64 }, 1, { 0.86, 0.88, 0.9 }, M.SCALE, true) })
    for _, d in ipairs(M.dolphins) do go.set(d.url, "vertices", M.dolphin_res) end
    make_pool(M.people, urls.person_factory, M.PEOPLE, "person", nil)
    if urls.bird_factory then
      M.gull_res = resource.create_buffer("/tc/bird_gull.bufferc",
        { buffer = make_buffer("bird", { 0.95, 0.95, 0.93 }, { 0.98, 0.98, 1.0 }, 1, nil, 1, true) })
      M.song_res = resource.create_buffer("/tc/bird_song.bufferc",
        { buffer = make_buffer("bird", { 0.32, 0.26, 0.22 }, { 0.42, 0.34, 0.28 }, 1, nil, 1, true) })
      make_pool(M.birds, urls.bird_factory, M.BIRDS, "bird", nil)
      for _, b in ipairs(M.birds) do go.set(b.url, "vertices", M.gull_res); b.res = "gull" end
    end
    make_pool(M.ships, urls.tanker_factory, M.TANKERS, "tanker",
      make_buffer("tanker", { 0.55, 0.18, 0.2 }, { 0.96, 0.94, 0.9 }, 1, nil, 1, true))
    make_pool(M.ships, urls.boat_factory, M.BOATS, "boat", make_buffer("boat", { 0.62, 0.42, 0.26 }, { 0.98, 0.96, 0.9 }, 1, nil, 1, true))
    -- one shared buffer resource per job colour for people
    res_gen = res_gen + 1
    for job, col in pairs(M.JOB_COLORS) do
      local hat = HAT[job]
      local kind = hat and ("person_" .. hat) or "person"
      local hair = HAIR[job] or HAIR.default
      if hat == "straw" then hair = { 0.93, 0.80, 0.45 } elseif hat == "cap" then hair = { 0.25, 0.35, 0.55 } end
      local skin = SKINS[(#job % #SKINS) + 1]
      job_res[job] = resource.create_buffer("/tc/person_" .. job .. res_gen .. ".bufferc",
        { buffer = make_buffer(kind, col, skin, 1, hair, M.PERSON_SCALE) })
      job_res_still[job] = resource.create_buffer("/tc/person_still_" .. job .. res_gen .. ".bufferc",
        { buffer = make_buffer(kind, col, skin, 1, hair, M.PERSON_SCALE, true) })
    end
    -- porters with their loads (W7): a sack of grain or goods, or a bundle of logs
    local tcol, skin = M.JOB_COLORS.trader, SKINS[2]
    local rcol = { 0.62, 0.20, 0.18 }
    job_res.raider = resource.create_buffer("/tc/person_raider" .. res_gen .. ".bufferc",
      { buffer = make_buffer("person_spear", rcol, skin, 1, HAIR.default, M.PERSON_SCALE) })
    job_res_still.raider = resource.create_buffer("/tc/person_still_raider" .. res_gen .. ".bufferc",
      { buffer = make_buffer("person_spear", rcol, skin, 1, HAIR.default, M.PERSON_SCALE, true) })
    for _, load in ipairs({ "sack", "logs" }) do
      local key = "porter_" .. load
      job_res[key] = resource.create_buffer("/tc/person_" .. key .. res_gen .. ".bufferc",
        { buffer = make_buffer("person_" .. load, tcol, skin, 1, HAIR.default, M.PERSON_SCALE) })
      job_res_still[key] = resource.create_buffer("/tc/person_still_" .. key .. res_gen .. ".bufferc",
        { buffer = make_buffer("person_" .. load, tcol, skin, 1, HAIR.default, M.PERSON_SCALE, true) })
    end
  end
  for _, list in ipairs({ M.pool, M.people, M.ships, M.birds, M.dolphins, M.stock }) do
    for _, p in ipairs(list) do
      if p.on then p.on = false; msg.post(p.url, MSG_DISABLE) end
      p.cell = 0
      p.pid = nil
    end
  end
  M.assign_timer = 0
end

local function unit(lat, lon)
  local cl = cos(lat)
  return cl * cos(lon), sin(lat), cl * sin(lon)
end

local function set_on(p, on)
  if p.on ~= on then p.on = on; msg.post(p.url, on and MSG_ENABLE or MSG_DISABLE) end
end

local function norm(x, y, z)
  local l = sqrt(x * x + y * y + z * z)
  return x / l, y / l, z / l
end

-- Tangent frame at unit vector (x, y, z): east (ex, 0, ez) and north (nx, ny, nz).
local function tframe(x, y, z)
  local ex, ez = z, -x
  local el = sqrt(ex * ex + ez * ez)
  if el < 1e-6 then ex, ez, el = 1, 0, 1 end
  ex, ez = ex / el, ez / el
  return ex, ez, -y * ez, ez * x - ex * z, y * ex
end

-- A point d world units from unit vector (x, y, z) at angle a in its tangent plane.
local function offset(x, y, z, a, d)
  local ex, ez, nx, ny, nz = tframe(x, y, z)
  local k = d / state.R
  local ca, sa = cos(a) * k, sin(a) * k
  return norm(x + ex * ca + nx * sa, y + ny * sa, z + ez * ca + nz * sa)
end

-- Heading (yaw) of a movement from (ax, ay, az) to (bx, by, bz) at (ax, ay, az).
local function heading(ax, ay, az, bx, by, bz)
  local ex, ez, nx, ny, nz = tframe(ax, ay, az)
  local dx, dy, dz = bx - ax, by - ay, bz - az
  return atan2(dx * nx + dy * ny + dz * nz, dx * ex + dz * ez)
end

-- Great-circle interpolation (short arcs: a normalised lerp is enough).
local function slerp(ax, ay, az, bx, by, bz, f)
  return norm(ax + (bx - ax) * f, ay + (by - ay) * f, az + (bz - az) * f)
end

---------------------------------------------------------------------------------------------
-- Animals: the sim's herds and packs near the view (W4, D-070). A group is drawn as a few of its
-- animals (more for a bigger herd) gathered about where it is: its centre cell, or — for a few
-- days after it moved — on the way from its last cell; they graze and wander about that spot and
-- follow it when the group moves on. Nothing is drawn where the sim has no group.

M.PER_HERD, M.PER_PACK = 8, 5
M.MOVE_H = 72          -- hours a group takes to shift to its new cell (drawn as a walk)
local GRAZER_NAMES = { "deer", "reindeer", "antelope", "boar" }
local PRED_NAMES = { "wolf", "cat" }
local GROUPS = { deer = {}, wolf = {} }
local SLOTS = { deer = {}, wolf = {} }

-- Where group (cell, prev, moved) is at hour t: lat, lon.
local function group_pos(g, cell, prev, moved, t)
  local lat, lon = g.lat[cell], g.lon[cell]
  if prev and prev ~= cell and t - moved < M.MOVE_H then
    local f = (t - moved) / M.MOVE_H
    if f < 0 then f = 0 end
    local plat, plon = g.lat[prev], g.lon[prev]
    local dl = lon - plon
    if dl > math.pi then dl = dl - 2 * math.pi elseif dl < -math.pi then dl = dl + 2 * math.pi end
    lat, lon = plat + (lat - plat) * f, plon + dl * f
  end
  return lat, lon
end

local function assign_animals(W, fx, fy, fz)
  local A = W.S and W.S.wild
  local g = W.grid
  local t = state.hour_f or W.hour
  for _, k in ipairs({ "deer", "wolf" }) do
    local l = GROUPS[k]
    for i = #l, 1, -1 do l[i] = nil end
  end
  if A then
    -- groups near the focus, nearest first; wild beasts keep off village cells (the sim's herds
    -- never centre there)
    for i = 1, A.nh do
      local c = A.h_cell[i]
      local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
      if d > 0.93 and A.h_n[i] >= 1 then
        local l = GROUPS.deer
        l[#l + 1] = { id = A.h_id[i], d = d, cell = c, prev = A.h_prev[i], moved = A.h_moved[i],
          n = min(M.PER_HERD, max(1, floor(sqrt(A.h_n[i]) * 1.2 + 0.5))), sp = GRAZER_NAMES[A.h_sp[i]] or "deer",
          heads = A.h_n[i] }
      end
    end
    for i = 1, A.np do
      local c = A.p_cell[i]
      local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
      if d > 0.93 and A.p_n[i] >= 1 then
        local l = GROUPS.wolf
        l[#l + 1] = { id = A.p_id[i], d = d, cell = c, prev = A.p_prev[i], moved = A.p_moved[i],
          n = min(M.PER_PACK, max(1, floor(A.p_n[i] + 0.5))), sp = PRED_NAMES[A.p_sp[i]] or "wolf",
          heads = A.p_n[i] }
      end
    end
  end
  for _, k in ipairs({ "deer", "wolf" }) do
    local l = GROUPS[k]
    table.sort(l, function(a, b) if a.d ~= b.d then return a.d > b.d end return a.id < b.id end)
    -- slots: (group, animal index) in order, as many as the pool holds
    local sl = SLOTS[k]
    local ns = 0
    for gi = 1, #l do
      for a = 1, l[gi].n do
        ns = ns + 1
        local e = sl[ns]
        if not e then e = {}; sl[ns] = e end
        e.grp, e.a = l[gi], a
      end
    end
    for i = #sl, ns + 1, -1 do sl[i] = nil end
  end
  local used = { deer = 0, wolf = 0 }
  local sp = g.spacing
  for i, p in ipairs(M.pool) do
    local k = p.kind
    local sl = SLOTS[k]
    local e = sl and sl[used[k] + 1]
    if e then
      used[k] = used[k] + 1
      local grp = e.grp
      if p.group ~= grp.id or p.ga ~= e.a then
        p.group, p.ga = grp.id, e.a
        p.cell = grp.cell
        if p.species ~= grp.sp then
          p.species = grp.sp
          go.set(p.url, "vertices", M.SPECIES[grp.sp].res)
        end
        p.lat, p.lon = nil, nil
        p.head = rnd() * 6.283
      end
      -- the animal's spot in its group (fixed per group and animal), about the group's position
      local lat, lon = group_pos(g, grp.cell, grp.prev, grp.moved, t)
      local h1 = ((grp.id * 7919 + e.a * 104729) % 1000) / 1000
      local h2 = ((grp.id * 3571 + e.a * 7907) % 1000) / 1000
      local spread = sp * (k == "wolf" and 0.28 or 0.22)
      p.home_lat = lat + (h1 - 0.5) * spread
      p.home_lon = lon + (h2 - 0.5) * spread / max(0.2, cos(lat))
      if not p.lat then p.lat, p.lon = p.home_lat, p.home_lon end
      p.cell = grp.cell
      p.heads = grp.heads
      -- (on a v3 settlement's map the settlement's own wild animals are drawn: D-093)
      local cl = cos(p.home_lat)
      set_on(p, not propgen.in_v3_square(cl * cos(p.home_lon), math.sin(p.home_lat), cl * sin(p.home_lon), 0.1))
    else
      p.cell, p.group = 0, nil
      set_on(p, false)
    end
  end
end

local function walk(p, dt, frozen, speed, radius)
  if not frozen then
    p.head = p.head + (rnd() - 0.5) * dt * 2.0
    local step = dt * speed
    p.lat = p.lat + cos(p.head) * step
    p.lon = p.lon + sin(p.head) * step / max(0.2, cos(p.lat))
    local dl, dn = p.lat - p.home_lat, p.lon - p.home_lon
    if dl * dl + dn * dn > radius * radius then p.head = atan2(-dn, -dl) end
  end
end

---------------------------------------------------------------------------------------------
-- People from the sim's schedule

local pos, rot = vmath.vector3(), vmath.quat()

local function place(p, x, y, z, r, yaw, scale)
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, p.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, yaw), p.id)
  go.set_scale(scale, p.id)
end

-- A person's own fixed fraction (sim p.u) spun into independent-looking numbers.
local function frac(x) return x - floor(x) end

-- Where on the ground a (cell, lot) place is for person p: lots come from the sim's layout; a
-- whole-cell place (field, woods, path: lot -1) is a spot in the cell fixed per person, kept on
-- land. kind shapes the spread: people at home stand in the yard, at the square around the well.
local KCODE = { home = 1, square = 2, site = 3, field = 0, wood = 0, boat = 0, path = 0 }
local function spot_raw(W, v, p, cell, lot, kind)
  local g = W.grid
  local u = p.u or 0.5
  if lot and lot >= 0 then
    local x, y, z = layout.pos(W, v, cell, lot)
    if kind == "home" then
      return offset(x, y, z, frac(u * 5.3) * 6.283, 0.05 + 0.02 * frac(u * 9.1))
    elseif kind == "square" or lot == 0 then
      return offset(x, y, z, frac(u * 7.7) * 6.283, 0.035 + 0.045 * frac(u * 3.3))
    end
    return offset(x, y, z, frac(u * 4.1) * 6.283, 0.03 + 0.03 * frac(u * 6.7))
  end
  local cx, cy, cz = g.X[cell], g.Y[cell], g.Z[cell]
  local a = frac(u * 7.13 + cell * 0.618) * 6.283
  local d = g.spacing * state.R * (0.08 + 0.3 * frac(u * 3.71 + cell * 0.31))
  for k = 0, 3 do
    local x, y, z = offset(cx, cy, cz, a, d * (1 - k * 0.3))
    local _, vtx = M.surface(x, y, z)
    if (M.height and M.height(vtx) or 1) > 0.01 then return x, y, z end
  end
  return cx, cy, cz
end

-- Memoised per puppet (a person's spots never move; the cache is dropped on reassignment).
local function spot(W, v, p, cell, lot, kind)
  local cache = M.spots
  local key = p.id * 1e7 + cell * 400 + ((lot or -1) + 1) * 4 + (KCODE[kind] or 0)
  local e = cache[key]
  if not e then
    local x, y, z = spot_raw(W, v, p, cell, lot, kind)
    e = { x, y, z }
    cache[key] = e
    M.spots_n = M.spots_n + 1
  end
  return e[1], e[2], e[3]
end
M.spots, M.spots_n = {}, 0

local WALKING = { walk = true, travel = true }

-- What a travelling party's people carry (porters out with payment or a gift, home with goods).
local function load_of(party)
  local c = party.cargo
  if not c then return nil end
  if party.kind == "raid" then return "raider" end
  if party.kind == "raid_home" then return (c.grain or 0) > 0 and "porter_sack" or "raider" end
  if party.kind == "porters" then return c.give == "wood" and "porter_logs" or "porter_sack" end
  if party.kind == "returning" then
    if c.get == "wood" or (c.wood or 0) > 0 then return "porter_logs" end
    if (c.got or 0) > 0 or (c.grain or 0) > 0 or (c.left or 0) > 0 then return "porter_sack" end
  end
  return nil
end
local HIDDEN = { sleep = true, fish = true }
local out_where = {}

-- Resolve person p's position now (t = fractional sim hour): unit vector, act, heading target.
local function locate(W, v, p, t)
  if p.away ~= 0 then return nil, "travel" end   -- on the road: drawn with their party
  local w = schedule.where(W, v, p, t, out_where)
  local act = w.act
  if HIDDEN[act] then return nil, act end
  if act == "home" and (p.u or 0) >= 0.7 then return nil, act end   -- indoors
  local x, y, z
  if act == "walk" then
    local fx, fy, fz = spot(W, v, p, w.from_cell, w.from_lot, w.from_lot == 0 and "square" or "home")
    local tx, ty, tz = spot(W, v, p, w.cell, w.lot, w.place)
    local f = w.f or 0
    if f < 0 then f = 0 elseif f > 1 then f = 1 end
    x, y, z = slerp(fx, fy, fz, tx, ty, tz, f)
    return x, y, z, act, tx, ty, tz
  end
  x, y, z = spot(W, v, p, w.cell, w.lot, w.place)
  return x, y, z, act
end

-- Party members strung out along the route (k-th member a little behind), camped at night.
local function locate_traveller(W, party, k, t)
  local g = W.grid
  local i, f, moving = schedule.party_pos(W, party, t)
  local path = party.path
  local c1 = path[i]
  local c2 = path[i + 1] or c1
  local back = (k - 1) * (moving and 0.18 or 0.0)
  local ff = f - back
  while ff < 0 and i > 1 do i = i - 1; ff = ff + 1; c2 = c1; c1 = path[i] end
  if ff < 0 then ff = 0 end
  local x, y, z = slerp(g.X[c1], g.Y[c1], g.Z[c1], g.X[c2], g.Y[c2], g.Z[c2], ff)
  -- side by side at camp, single file on the road
  local a = (k * 2.39996) % 6.283
  x, y, z = offset(x, y, z, a, moving and 0.012 or (0.02 + 0.01 * (k % 3)))
  return x, y, z, moving and "travel" or "camp", g.X[c2], g.Y[c2], g.Z[c2]
end

local function job_of(p)
  local job = p and p.job
  if type(job) ~= "string" or not job_res[job] then job = "default" end
  return job
end

-- Choose who to draw: residents of the villages nearest the focus who are outdoors now, and the
-- members of parties on the road nearby. Pool entries keep their person when still wanted.
M.LOCATE_BUDGET = 700   -- schedule lookups per reassignment (big towns: the nearest first)
local function assign_people(W, fx, fy, fz, t, zoom)
  local g = W.grid
  local P = W.ent.people or {}
  local want = {}
  local vs = {}
  -- the closer the camera, the smaller the circle of villages worth peopling
  local dot = M.FOCUS_DOT + (0.992 - M.FOCUS_DOT) * min(1, max(0, ((zoom or 0.5) - 0.5) / 0.5))
  for _, v in ents.each_village(W) do
    local c = v.cell
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > dot and type(v.members) == "table" then vs[#vs + 1] = { v = v, d = d } end
  end
  table.sort(vs, function(a, b) return a.d > b.d end)
  local budget = M.LOCATE_BUDGET
  local daylight = require("sim.society.daylight")
  for _, e in ipairs(vs) do
    local v = e.v
    -- asleep: nobody out (travellers are found through their parties)
    local lh = daylight.local_hour(t, g.lon[v.cell])
    local plan = v.plan or {}
    local awake = lh >= (plan.wake or 6) and lh < (plan.sleep or 21.5)
    local n = 0
    if awake then
      for k = 1, #v.members do
        if budget <= 0 or #want >= #M.people then break end
        local p = P[v.members[k]]
        if p and not p.dead then
          budget = budget - 1
          local x = locate(W, v, p, t)
          if x then
            want[#want + 1] = { pid = p.id, v = v, job = job_of(p) }
            n = n + 1
            if n >= M.PER_VILLAGE then break end
          end
        end
      end
    end
  end
  local E = W.ent
  local list = E.party_list or {}
  for i = 1, #list do
    local party = E.parties and E.parties[list[i]]
    if party and party.path and #party.path > 0 then
      local pi, _ = schedule.party_pos(W, party, t)
      local c = party.path[pi]
      -- (in their boats from boarding to the landing: the boats are drawn)
      local afloat = party.si and pi >= party.si - 1 and pi <= party.sj and party.boats and #party.boats > 0
      if c and not afloat and g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz > M.FOCUS_DOT - 0.02 then
        for k = 1, #party.members do
          local p = P[party.members[k]]
          if p and not p.dead then want[#want + 1] = { pid = p.id, party = party, k = k, job = load_of(party) or job_of(p) } end
        end
      end
    end
  end
  -- group by job so pool entries (drawn in creation order) share buffers in runs
  table.sort(want, function(a, b) if a.job ~= b.job then return a.job < b.job end return a.pid < b.pid end)
  local keep = {}
  for i = 1, min(#want, #M.people) do keep[want[i].pid] = want[i] end
  local placed = {}
  for _, p in ipairs(M.people) do
    local w = p.pid and keep[p.pid]
    if w then placed[p.pid] = true; p.v, p.party, p.k = w.v, w.party, w.k
    else p.pid = nil; p.v, p.party = nil, nil; set_on(p, false) end
  end
  local free = 1
  for i = 1, min(#want, #M.people) do
    local w = want[i]
    if not placed[w.pid] then
      while M.people[free] and M.people[free].pid do free = free + 1 end
      local p = M.people[free]
      if not p then break end
      p.pid, p.v, p.party, p.k = w.pid, w.v, w.party, w.k
      p.job = w.job
      p.head = rnd() * 6.283
      p.still = nil
      p.px = nil
      p.next_eval, p.walking = nil, nil
    end
  end
end

-- Stationary people are re-resolved a few times a second (staggered); walkers every frame.
M.EVAL_S = 0.25
local function step_people(W, t, dt, frozen)
  local P = W.ent.people or {}
  local pp = M.people_pos
  local np, nt = 0, 0
  local now = state.time
  if M.spots_n > 20000 then M.spots, M.spots_n = {}, 0 end
  for i, p in ipairs(M.people) do
    if p.pid and p.on and not p.walking and p.px and p.next_eval and now < p.next_eval then
      -- unchanged since the last look: already placed
      np = np + 1
      if p.party then nt = nt + 1 end
      local r = p.r or state.R
      pp[#pp + 1] = { id = p.pid, x = p.px * r, y = p.py * r, z = p.pz * r }
    elseif p.pid then
      p.next_eval = now + M.EVAL_S * (0.75 + 0.5 * ((i * 0.618) % 1))
      local person = P[p.pid]
      local x, y, z, act, tx, ty, tz
      if person and not person.dead then
        if p.party then
          if person.away == p.party.id then x, y, z, act, tx, ty, tz = locate_traveller(W, p.party, p.k or 1, t) end
        elseif p.v and person.village == p.v.id and person.away == 0 then
          x, y, z, act, tx, ty, tz = locate(W, p.v, person, t)
        end
      end
      if x then
        local walking = WALKING[act] and not frozen
        p.walking = walking
        if p.still ~= not walking then
          p.still = not walking
          go.set(p.url, "vertices", walking and job_res[p.job] or job_res_still[p.job])
        end
        -- heading: along the way while walking (toward the destination), else keep
        if tx then p.head = heading(x, y, z, tx, ty, tz)
        elseif p.px then
          local dx, dy, dz = x - p.px, y - p.py, z - p.pz
          if dx * dx + dy * dy + dz * dz > 1e-12 then p.head = heading(p.px, p.py, p.pz, x, y, z) end
        end
        p.px, p.py, p.pz = x, y, z
        local r, vtx = M.surface(x, y, z, p.vtx)
        p.vtx, p.r = vtx, r
        set_on(p, true)
        place(p, x, y, z, r, -(p.head or 0), M.PERSON_SCALE)
        np = np + 1
        if p.party then nt = nt + 1 end
        pp[#pp + 1] = { id = p.pid, x = x * r, y = y * r, z = z * r }
      else
        set_on(p, false)
      end
    end
  end
  M.stats.people, M.stats.travellers = np, nt
  return np
end

---------------------------------------------------------------------------------------------
-- Boats (from the sim's trips) and tankers (lanes)

-- The mooring spot of boat b of village v: off the end of the pier, boats side by side.
local function mooring_pos(W, v, b, slot)
  local g = W.grid
  local mc = v.mooring or b.home
  local x, y, z
  if v.pier_lot then
    x, y, z = layout.pos(W, v, v.cell, v.pier_lot)
    local tx, ty, tz = g.X[mc], g.Y[mc], g.Z[mc]
    x, y, z = slerp(x, y, z, tx, ty, tz, 0.28)
  else
    x, y, z = g.X[mc], g.Y[mc], g.Z[mc]
  end
  return offset(x, y, z, 1.2 + slot * 1.9, 0.02 + 0.015 * (slot % 3))
end

-- A boat away from its mooring (sim/society/sea.lua): carrying a party overseas (the party's boats
-- side by side), anchored off a foreign shore while its people are ashore, or sailing home empty.
-- nil when it is at its mooring (a party still walking down to the boat).
local function voyage_pos(W, b, t)
  local g = W.grid
  local st = b.state
  if st == "voyage" then
    local party = W.ent.parties and W.ent.parties[b.party or 0]
    if not party or not party.si then return nil end
    local i, f = schedule.party_pos(W, party, t)
    local si, sj = party.si, party.sj
    local path = party.path
    local k = 1
    local bl = party.boats or {}
    for q = 1, #bl do if bl[q] == b.id then k = q end end
    local c1, c2, ff
    if i < si then
      -- boarding: an outbound boat waits at its mooring (nil); on the way home it waits by the shore
      if i < si - 1 or path[si] == W.ent.villages[b.village].mooring then return nil end
      c1, c2, ff = path[si], path[si - 1], 0.42
    elseif i >= sj then
      -- the landing step: run up to the shore, where they step off
      if i > sj then c1, c2, ff = path[sj], path[sj + 1] or path[sj], 0.42
      else c1, c2, ff = path[sj], path[sj + 1] or path[sj], 0.42 * math.min(1, f * 2) end
    else c1, c2, ff = path[i], path[i + 1], f end
    local x, y, z = slerp(g.X[c1], g.Y[c1], g.Z[c1], g.X[c2], g.Y[c2], g.Z[c2], ff)
    if k > 1 then x, y, z = offset(x, y, z, k * 2.4, 0.025 * (k - 1)) end
    return x, y, z, g.X[c2], g.Y[c2], g.Z[c2]
  elseif st == "beached" and b.at then
    -- drawn up by the shore where its people landed
    local c, s2 = b.at, b.coast or b.at
    local x, y, z = slerp(g.X[c], g.Y[c], g.Z[c], g.X[s2], g.Y[s2], g.Z[s2], 0.42)
    x, y, z = offset(x, y, z, (b.id * 1.7) % 6.283, 0.012)
    return x, y, z, g.X[s2], g.Y[s2], g.Z[s2]
  elseif st == "homeward" and b.back and #b.back > 0 then
    local back = b.back
    local n = #back
    local u = (t - (b.t0 or t)) / max(0.01, (b.t1 or t) - (b.t0 or t))
    if u < 0 then u = 0 elseif u > 1 then u = 1 end
    local sidx = u * (n - 1)
    local i = floor(sidx) + 1
    local c1, c2 = back[i], back[math.min(n, i + 1)]
    local x, y, z = slerp(g.X[c1], g.Y[c1], g.Z[c1], g.X[c2], g.Y[c2], g.Z[c2], sidx - (i - 1))
    return x, y, z, g.X[c2], g.Y[c2], g.Z[c2]
  end
  return nil
end

local function boat_pos(W, v, b, slot, t)
  local g = W.grid
  if b.state == "voyage" or b.state == "beached" or b.state == "homeward" then
    local x, y, z, hx, hy, hz = voyage_pos(W, b, t)
    if x then return x, y, z, hx, hy, hz end
  end
  local mx, my, mz = mooring_pos(W, v, b, slot)
  local tr = b.trip
  if tr and tr.ground and t >= tr.depart and t < tr.ret then
    local gc = tr.ground
    -- each boat works its own part of the ground
    local gx, gy, gz = offset(g.X[gc], g.Y[gc], g.Z[gc], (b.id * 2.4) % 6.283, 0.08 + 0.06 * frac(b.id * 0.37))
    if t < tr.at then
      local f = (t - tr.depart) / max(0.01, tr.at - tr.depart)
      local x, y, z = slerp(mx, my, mz, gx, gy, gz, f)
      return x, y, z, gx, gy, gz
    elseif t < tr.leave then
      -- hauling nets: a slow drift around the spot
      local a = t * 0.8 + b.id
      local x, y, z = offset(gx, gy, gz, a, 0.02)
      local x2, y2, z2 = offset(gx, gy, gz, a + 0.3, 0.02)
      return x, y, z, x2, y2, z2
    end
    local f = (t - tr.leave) / max(0.01, tr.ret - tr.leave)
    local x, y, z = slerp(gx, gy, gz, mx, my, mz, f)
    return x, y, z, mx, my, mz
  end
  return mx, my, mz
end

local function update_ships(W, t)
  local R = state.R
  local sp = M.ship_pos
  for i = #sp, 1, -1 do sp[i] = nil end
  local used = {}
  local nt, nb = 0, 0
  local function take(kind)
    for _, q in ipairs(M.ships) do
      if q.kind == kind and not used[q] then used[q] = true; return q end
    end
    return nil
  end
  local function show(p, x, y, z, hx, hy, hz, list, index, kind)
    if hx then p.head = heading(x, y, z, hx, hy, hz)
    elseif p.px then
      local dx, dy, dz = x - p.px, y - p.py, z - p.pz
      if dx * dx + dy * dy + dz * dz > 1e-10 then p.head = heading(p.px, p.py, p.pz, x, y, z) end
    end
    p.px, p.py, p.pz = x, y, z
    set_on(p, true)
    place(p, x, y, z, R + 0.004, -(p.head or 0), M.SCALE)
    sp[#sp + 1] = { index = index, list = list, kind = kind, x = x * R, y = y * R, z = z * R }
    p.list, p.index = list, index
  end
  -- fishing boats, village by village (moored unless today's trip is under way)
  local Bo = W.ent.boats or {}
  for _, v in ents.each_village(W) do
    local list = v.boats
    if type(list) == "table" then
      for s = 1, #list do
        local b = Bo[list[s]]
        if b and b.state ~= "lost" then
          local p = take("boat")
          if not p then break end
          local x, y, z, hx, hy, hz = boat_pos(W, v, b, s, t)
          if p.boat ~= b.id then p.boat = b.id; p.px = nil; p.head = nil end
          show(p, x, y, z, hx, hy, hz, "boats", b.id, "boat")
          nb = nb + 1
        end
      end
    end
  end
  -- tankers on their lanes
  local ships = ents.ships(W)
  for i = 1, #ships do
    local s = ships[i]
    if type(s) == "table" and s.alive ~= false and not s.sunk then
      local x, y, z = ents.ship_dir(W, s)
      if x then
        local p = take("tanker")
        if p then show(p, x, y, z, nil, nil, nil, "ships", i, "tanker"); nt = nt + 1 end
      end
    end
  end
  for _, q in ipairs(M.ships) do if not used[q] then set_on(q, false) end end
  M.stats.ships = nt + nb
  M.stats.ship_draws = (nt > 0 and 1 or 0) + (nb > 0 and 1 or 0)
end

---------------------------------------------------------------------------------------------
-- Birds, where the sim gives them a reason: gulls over boats hauling their nets and over rich
-- fishing water near the coast; small flocks of songbirds over the woods in daylight.

local function sunlit(W, c, t)
  local daylight = require("sim.society.daylight")
  local sx, sy, sz = daylight.sun_dir(t)
  local g = W.grid
  return g.X[c] * sx + g.Y[c] * sy + g.Z[c] * sz > 0.05
end

local function assign_birds(W, fx, fy, fz, t)
  local g, F = W.grid, W.F
  local fl = {}
  -- boats on their grounds (from the boat puppets already placed from the trips)
  for _, q in ipairs(M.ships) do
    if q.on and q.kind == "boat" and q.px then
      local b = W.ent.boats and W.ent.boats[q.index]
      local tr = b and b.trip
      if tr and t >= tr.at and t < tr.leave and #fl < 10 then
        fl[#fl + 1] = { x = q.px, y = q.py, z = q.pz, n = 3, r = 0.05, alt = 0.12, kind = "gull" }
      end
    end
  end
  -- rich coastal fishing water and woods near the focus
  local cand = {}
  for c = 1, W.n do
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > 0.965 then
      if W.geo.ocean[c] == 1 then
        if W.geo.coast_dist[c] <= 2 and F.fish[c] > 0.55 and F.ice[c] < 0.3 then
          cand[#cand + 1] = { c = c, w = F.fish[c] * d, kind = "gull" }
        end
      elseif F.forest[c] > 0.35 and F.fire[c] == 0 and sunlit(W, c, t) then
        cand[#cand + 1] = { c = c, w = F.forest[c] * d * 0.9, kind = "song" }
      end
    end
  end
  table.sort(cand, function(a, b) if a.w ~= b.w then return a.w > b.w end return a.c < b.c end)
  for i = 1, min(#cand, 18) do
    local e = cand[i]
    local gull = e.kind == "gull"
    fl[#fl + 1] = { x = g.X[e.c], y = g.Y[e.c], z = g.Z[e.c], n = gull and 5 or 6, r = gull and 0.18 or 0.12,
      alt = gull and 0.22 or 0.45, kind = e.kind, c = e.c }
  end
  M.flocks = fl
end

local function update_birds(W, dt, zoom, frozen)
  if #M.birds == 0 then return end
  local show = zoom >= M.BIRD_ZOOM
  local slot = 1
  local R = state.R
  local tv = state.time
  if show then
    for fi, f in ipairs(M.flocks) do
      for k = 1, f.n do
        local b = M.birds[slot]
        if not b then break end
        slot = slot + 1
        if b.res ~= f.kind then
          b.res = f.kind
          go.set(b.url, "vertices", f.kind == "gull" and M.gull_res or M.song_res)
        end
        -- circling (gulls wide and slow, songbirds quick loops), each bird its own phase
        local ph = fi * 1.7 + k * 2.1
        local w = (f.kind == "gull" and 0.5 or 1.1) * (0.8 + 0.1 * k)
        local a = tv * w + ph
        local rr = f.r * (0.6 + 0.12 * k)
        local x, y, z = offset(f.x, f.y, f.z, a, rr)
        local x2, y2, z2 = offset(f.x, f.y, f.z, a + 0.05, rr)
        local r = M.surface(x, y, z) + f.alt + 0.03 * sin(tv * 0.7 + ph)
        pos.x, pos.y, pos.z = x * r, y * r, z * r
        go.set_position(pos, b.id)
        go.set_rotation(vmx.set_up_yaw(rot, x, y, z, -heading(x, y, z, x2, y2, z2)), b.id)
        -- wing beat: the V of the wings flattens and lifts (scale must stay positive)
        local flap = 0.12 + 0.88 * (0.5 + 0.5 * cos(tv * (f.kind == "gull" and 5 or 11) + ph * 3))
        if f.kind == "gull" and (math.floor(tv * 0.4 + ph) % 3) ~= 0 then flap = 0.8 end   -- gulls glide
        local s = M.SCALE * (f.kind == "gull" and 1.6 or 1.1)
        b.scl = b.scl or vmath.vector3()
        b.scl.x, b.scl.y, b.scl.z = s, s * flap, s
        go.set_scale(b.scl, b.id)
        set_on(b, true)
      end
    end
  end
  for i = slot, #M.birds do set_on(M.birds[i], false) end
  M.stats.birds = slot - 1
end

M.PEOPLE_MAX_HPS = 60   -- hours a second above which villagers and flocks are not drawn
local AP_POOL = {}
function M.update(W, dt, zoom, frozen)
  local t = state.hour_f or W.hour
  update_ships(W, t)
  local active = 0
  local ap, pp = M.animal_pos, M.people_pos
  for i = #ap, 1, -1 do ap[i] = nil end
  for i = #pp, 1, -1 do pp[i] = nil end
  if zoom < M.MIN_ZOOM then
    for _, p in ipairs(M.pool) do if p.on then p.cell = 0; set_on(p, false) end end
    for _, p in ipairs(M.people) do if p.on or p.pid then p.pid = nil; set_on(p, false) end end
    for _, b in ipairs(M.birds) do if b.on then set_on(b, false) end end
    M.stats.active, M.stats.people, M.stats.draws = 0, 0, 0
    return
  end
  -- at weeks a second and faster a person's day is a blur: villagers, travellers and flocks are
  -- not drawn (their fields, buildings, trails and boats show what they do), and their per-frame
  -- schedule lookups leave the frame to the sim
  local fast = speed.hours_per_second() > M.PEOPLE_MAX_HPS
  if fast and not M.was_fast then
    for _, p in ipairs(M.people) do if p.on or p.pid then p.pid = nil; set_on(p, false) end end
    for _, p in ipairs(M.stock) do if p.on then p.v, p.party = nil, nil; set_on(p, false) end end
  end
  M.was_fast = fast
  M.assign_timer = M.assign_timer - dt
  if M.assign_timer <= 0 then
    M.assign_timer = 0.6
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    assign_animals(W, fx, fy, fz)
    if not fast then assign_people(W, fx, fy, fz, t, zoom) end
    -- (v3 worlds: birds are the settlements' flocks, agents with reasons - S14; no circling here)
    if W.v3 then M.flocks = {} else assign_birds(W, fx, fy, fz, t) end
  end
  update_birds(W, dt, zoom, frozen)
  M.update_dolphins(W, dt, frozen)
  if not fast then M.update_stock(W, dt, frozen, t) end
  local sp = W.grid.spacing
  for _, p in ipairs(M.pool) do
    if p.on then
      active = active + 1
      local spc = M.SPECIES[p.species or p.kind] or M.SPECIES.deer
      walk(p, dt, frozen, 0.004 * spc.speed, sp * (p.kind == "wolf" and 0.18 or 0.12))
      local x, y, z = unit(p.lat, p.lon)
      local r, v = M.surface(x, y, z, p.v)
      p.v = v
      place(p, x, y, z, r, -p.head, M.SCALE * spc.scale)
      -- (reused entries: no garbage per animal per frame)
      local k = #ap + 1
      local e = AP_POOL[k]
      if not e then e = {}; AP_POOL[k] = e end
      e.kind, e.cell, e.x, e.y, e.z = p.species or p.kind, p.cell, x * r, y * r, z * r
      e.group = p.group
      ap[k] = e
    end
  end
  local np = fast and 0 or step_people(W, t, dt, frozen)
  M.stats.active = active + np
  -- draw-call estimate: one batch per distinct mover buffer in use
  local kinds, n = {}, 0
  for _, p in ipairs(M.pool) do if p.on and not kinds[p.kind] then kinds[p.kind] = true; n = n + 1 end end
  for _, p in ipairs(M.people) do
    local k = "job_" .. tostring(p.job) .. (p.still and "s" or "w")
    if p.on and not kinds[k] then kinds[k] = true; n = n + 1 end
  end
  M.stats.draws = n
end

-- Livestock (W10): each nearby village's herd (sim v.herd, one puppet per ~6 sheep-equivalents,
-- the species in proportion) grazes its pasture (v.herd_cell) in the working day and stands in
-- its pen by the village at night and in storms.
local stock_timer = 0
function M.update_stock(W, dt, frozen, t)
  local herds = require("sim.society.herds")
  local daylight = require("sim.society.daylight")
  local g = W.grid
  stock_timer = stock_timer - dt
  if stock_timer <= 0 then
    stock_timer = 1.0
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    local want = {}
    local zoom = require("game.view.camera").zoom or 0.5
    local dot = M.FOCUS_DOT + (0.992 - M.FOCUS_DOT) * min(1, max(0, (zoom - 0.5) / 0.5))
    for _, v in ents.each_village(W) do
      local d = g.X[v.cell] * fx + g.Y[v.cell] * fy + g.Z[v.cell] * fz
      if d > dot and v.herd then
        local h = v.herd
        local se = herds.se(h)
        local n = math.min(40, floor(se / 6 + 0.5))
        local tot = (h.sheep or 0) + (h.goats or 0) + (h.cattle or 0)
        for k = 1, n do
          local f = (k - 0.5) / n * tot
          local kind = f < (h.sheep or 0) and "sheep" or (f < (h.sheep or 0) + (h.goats or 0) and "goats" or "cattle")
          want[#want + 1] = { v = v, k = k, kind = kind }
        end
      end
    end
    -- beasts driven home by cattle raiders (W8): they trail the raiding party
    local E = W.ent
    for _, id in ipairs(E.party_list or {}) do
      local party = E.parties and E.parties[id]
      local b = party and party.kind == "raid_home" and party.cargo and party.cargo.beasts
      if b then
        local pi = schedule.party_pos(W, party, t)
        local c = party.path[pi]
        if c and g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz > dot then
          local tot = (b.sheep or 0) + (b.goats or 0) + (b.cattle or 0)
          local n = math.min(10, floor(herds.se(b) / 6 + 0.5))
          for k = 1, n do
            local f = (k - 0.5) / n * tot
            local kind = f < (b.sheep or 0) and "sheep" or (f < (b.sheep or 0) + (b.goats or 0) and "goats" or "cattle")
            want[#want + 1] = { party = party, k = k, kind = kind }
          end
        end
      end
    end
    for i, p in ipairs(M.stock) do
      local w = want[i]
      if w then
        if p.kind2 ~= w.kind then p.kind2 = w.kind; go.set(p.url, "vertices", M.stock_res[w.kind]) end
        p.v, p.k, p.party = w.v, w.k, w.party
        set_on(p, true)
      else
        p.v, p.party = nil, nil
        set_on(p, false)
      end
    end
  end
  local ns = 0
  for _, p in ipairs(M.stock) do
    local v = p.v
    if p.on and p.party then
      -- driven along the raiders' road, a little behind them
      local party = p.party
      if W.ent.parties and W.ent.parties[party.id] then
        ns = ns + 1
        local k = #party.members + p.k
        local x, y, z, _, hx, hy, hz = locate_traveller(W, party, k, t)
        local a = (p.k * 2.39996) % 6.283
        x, y, z = offset(x, y, z, a, 0.006)
        local r, vv = M.surface(x, y, z, p.vtx)
        p.vtx = vv
        place(p, x, y, z, r, -heading(x, y, z, hx, hy, hz), M.SCALE)
      else
        set_on(p, false)
      end
    elseif p.on and v then
      ns = ns + 1
      local lh = daylight.local_hour(t, g.lon[v.cell])
      local plan = v.plan
      local out = lh >= (plan.ws or 7) and lh < (plan.we or 18) and not plan.storm
      local cx, cy, cz
      if out then
        local c = v.herd_cell or v.cell
        cx, cy, cz = g.X[c], g.Y[c], g.Z[c]
      else
        local pen = propgen.pens[v.id]
        if pen then cx, cy, cz = pen[1], pen[2], pen[3] else cx, cy, cz = g.X[v.cell], g.Y[v.cell], g.Z[v.cell] end
      end
      -- a loose flock: each beast its own spot around the centre, drifting as it grazes
      local u = (p.k * 0.618034) % 1
      local a = u * 6.283 + (out and t * 0.02 or 0)
      local rad = (out and 0.012 or 0.0035) * (0.3 + 0.7 * ((p.k * 0.4142) % 1)) * (out and 1 or 1)
      local lat0 = math.asin(cy)
      local lon0 = math.atan2(cz, cx)
      local lat = lat0 + math.cos(a) * rad
      local lon = lon0 + math.sin(a) * rad / max(0.2, math.cos(lat0))
      local x, y, z = unit(lat, lon)
      local r, vv = M.surface(x, y, z, p.vtx)
      p.vtx = vv
      place(p, x, y, z, r, -(a + (out and 1.2 or 0)), M.SCALE)
    end
  end
  M.stats.stock = ns
end

-- Dolphins: pods over the richest fishing waters near the view (the sim's fish), arcing out of
-- the sea and back.
local dolphin_timer = 0
function M.update_dolphins(W, dt, frozen)
  dolphin_timer = dolphin_timer - dt
  local g = W.grid
  if dolphin_timer <= 0 then
    dolphin_timer = 1.5
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    local best = {}
    for c = 1, W.n do
      if W.geo.ocean[c] == 1 and W.F.ice[c] < 0.2 and W.F.fish[c] > 0.55 then
        local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
        if d > 0.94 then best[#best + 1] = { c, W.F.fish[c] + d } end
      end
    end
    table.sort(best, function(a, b) return a[2] > b[2] end)
    -- (S24: a pod of two over the richest water near the view - a rare sight, not four over every rich cell; the
    -- sea's fish are the schools, game/view/fish.lua)
    for i, dph in ipairs(M.dolphins) do
      local e = i <= 2 and best[1] or nil
      if e then
        if dph.cell ~= e[1] then
          dph.cell = e[1]
          local sp = g.spacing * 0.3
          dph.lat = g.lat[e[1]] + (rnd() - 0.5) * sp
          dph.lon = g.lon[e[1]] + (rnd() - 0.5) * sp
          dph.home_lat, dph.home_lon = dph.lat, dph.lon
          dph.head = rnd() * 6.283
          dph.phase = rnd() * 6.283
        end
        set_on(dph, true)
      else
        dph.cell = 0
        set_on(dph, false)
      end
    end
  end
  local R = state.R
  local t = state.time
  for _, dph in ipairs(M.dolphins) do
    if dph.on then
      if not frozen then
        dph.head = dph.head + dt * 0.25
        local step = dt * 0.006
        dph.lat = dph.lat + cos(dph.head) * step
        dph.lon = dph.lon + sin(dph.head) * step / max(0.2, cos(dph.lat))
      end
      -- a leap: out of the water for part of each cycle
      local ph = (t * 0.9 + dph.phase) % 6.283
      local up = sin(ph)
      local alt = up > 0 and up * 0.05 or up * 0.03
      local x, y, z = unit(dph.lat, dph.lon)
      local r = R + alt
      pos.x, pos.y, pos.z = x * r, y * r, z * r
      go.set_position(pos, dph.id)
      local pitch = cos(ph) * 0.9
      local q = vmx.set_up_yaw(rot, x, y, z, -dph.head)
      go.set_rotation(q * vmath.quat_rotation_z(-pitch), dph.id)
      go.set_scale(M.SCALE, dph.id)
    end
  end
end

--- Unit direction of a ship puppet, or nil.
function M.ship_dir(list, index)
  for _, q in ipairs(M.ships) do
    if q.on and q.list == list and q.index == index and q.px then return q.px, q.py, q.pz end
  end
  return nil
end

--- Current world direction of a person puppet (for follow), or nil. A fisher out at sea is
-- where their boat is.
function M.person_dir(pid)
  for _, p in ipairs(M.people) do
    if p.on and p.pid == pid and p.px then return p.px, p.py, p.pz end
  end
  local W = M.W
  local person = W and W.ent.people and W.ent.people[pid]
  if person and person.task == "fish" and person.site ~= 0 then
    local x, y, z = M.ship_dir("boats", person.site)
    if x then return x, y, z end
  end
  return nil
end

return M
