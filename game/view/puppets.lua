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
local vmx = require("game.view.vmx")
local ents = require("game.view.ents")
local schedule = require("sim.society.schedule")
local layout = require("sim.society.layout")

local M = {}

M.DEER, M.WOLVES, M.PEOPLE, M.TANKERS, M.BOATS = 72, 28, 320, 8, 64
M.BIRDS = 96
M.BIRD_ZOOM = 0.5      -- birds show from this zoom in
M.MIN_ZOOM = 0.5
M.GRAZER_MIN, M.PRED_MIN = 0.12, 0.08
M.PER_CELL = 6
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
  end
  for _, list in ipairs({ M.pool, M.people, M.ships, M.birds }) do
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
-- Animals (fauna fields until W4's herds)

local function assign_animals(W, fx, fy, fz)
  local F, g = W.F, W.grid
  local gz, pr = F.grazers, F.predators
  local wants = { deer = {}, wolf = {} }
  for c = 1, W.n do
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > 0.93 and W.geo.ocean[c] == 0 then
      if gz[c] > M.GRAZER_MIN then wants.deer[#wants.deer + 1] = { c, gz[c] } end
      if pr[c] > M.PRED_MIN then wants.wolf[#wants.wolf + 1] = { c, pr[c] } end
    end
  end
  table.sort(wants.deer, function(a, b) return a[2] > b[2] end)
  table.sort(wants.wolf, function(a, b) return a[2] > b[2] end)
  local used = { deer = 0, wolf = 0 }
  for _, p in ipairs(M.pool) do
    local list = wants[p.kind]
    local slot = list[floor(used[p.kind] / M.PER_CELL) + 1]
    if slot then
      used[p.kind] = used[p.kind] + 1
      if p.cell ~= slot[1] then
        p.cell = slot[1]
        local sp = g.spacing * 0.45
        p.lat = g.lat[p.cell] + (rnd() - 0.5) * sp
        p.lon = g.lon[p.cell] + (rnd() - 0.5) * sp
        p.head = rnd() * 6.283
        p.home_lat, p.home_lon = g.lat[p.cell], g.lon[p.cell]
      end
      set_on(p, true)
    else
      p.cell = 0
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
local function assign_people(W, fx, fy, fz, t)
  local g = W.grid
  local P = W.ent.people or {}
  local want = {}
  local vs = {}
  for _, v in ents.each_village(W) do
    local c = v.cell
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > M.FOCUS_DOT and type(v.members) == "table" then vs[#vs + 1] = { v = v, d = d } end
  end
  table.sort(vs, function(a, b) return a.d > b.d end)
  for _, e in ipairs(vs) do
    local v = e.v
    local n = 0
    for k = 1, #v.members do
      local p = P[v.members[k]]
      if p and not p.dead then
        local x = locate(W, v, p, t)
        if x then
          want[#want + 1] = { pid = p.id, v = v, job = job_of(p) }
          n = n + 1
          if n >= M.PER_VILLAGE then break end
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
      if c and g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz > M.FOCUS_DOT - 0.02 then
        for k = 1, #party.members do
          local p = P[party.members[k]]
          if p and not p.dead then want[#want + 1] = { pid = p.id, party = party, k = k, job = job_of(p) } end
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

local function boat_pos(W, v, b, slot, t)
  local g = W.grid
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
  M.assign_timer = M.assign_timer - dt
  if M.assign_timer <= 0 then
    M.assign_timer = 0.6
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    assign_animals(W, fx, fy, fz)
    assign_people(W, fx, fy, fz, t)
    assign_birds(W, fx, fy, fz, t)
  end
  update_birds(W, dt, zoom, frozen)
  local sp = W.grid.spacing
  for _, p in ipairs(M.pool) do
    if p.on then
      active = active + 1
      walk(p, dt, frozen, 0.004 * (p.kind == "wolf" and 1.6 or 1.0), sp * 0.5)
      local x, y, z = unit(p.lat, p.lon)
      local r, v = M.surface(x, y, z, p.v)
      p.v = v
      place(p, x, y, z, r, -p.head, M.SCALE)
      ap[#ap + 1] = { kind = p.kind, cell = p.cell, x = x * r, y = y * r, z = z * r }
    end
  end
  local np = step_people(W, t, dt, frozen)
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
