-- Puppets (Stage 1.8/1.9): pooled movers driven by Lua, world-space material (engine-batched).
--   * people: residents of villages near the view focus when zoomed in (W.ent.villages[*].members
--     -> W.ent.people[id]); tunic and hat by job. They go about their jobs (renderer v2): farmers
--     to the village's fields while the local season has crops, fishers to the shore, woodcutters
--     to the nearest woods, children play around the square, the rest visit houses; they walk
--     (limbs swing, props_ws.vp) and stop to work (a still buffer).
--   * animals: deer/wolves near cells with high grazers/predators density when zoomed in.
--   * ships: tankers and boats from W.ent.ships at any zoom (if the sim provides positions).
-- Hard cap: pool sizes (05 §9: <= 300 Lua-driven movers per frame). Positions are exposed
-- for picking (M.people_pos / M.animals / M.ships_pos).
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")
local ents = require("game.view.ents")

local M = {}

M.DEER, M.WOLVES, M.PEOPLE, M.TANKERS, M.BOATS = 60, 24, 140, 8, 24
M.MIN_ZOOM = 0.55
M.GRAZER_MIN, M.PRED_MIN = 0.12, 0.08
M.PER_CELL = 6
M.PER_VILLAGE = 22
M.SCALE = 1.3
M.PERSON_SCALE = 1.7   -- villagers a little oversized (toy-like) so they read among the houses
M.WALK = 0.0032        -- people's walking speed (rad/s on the unit sphere, ~0.03 world units/s)
M.MIGRANTS = 18        -- walkers for migrant columns (sim migration / founding events)
M.MIGRANT_ZOOM = 0.3   -- columns show from this zoom in
M.migrants = {}        -- pooled walkers
M.columns = {}         -- active columns { from = {x,y,z}, to = {x,y,z}, n, t, dur }

local H_POS, H_NRM, H_COL = hash("position"), hash("normal"), hash("color")
local MSG_ENABLE, MSG_DISABLE = hash("enable"), hash("disable")
local sin, cos, sqrt, max, min, floor, atan2 = math.sin, math.cos, math.sqrt, math.max, math.min,
  math.floor, math.atan2

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
M.people = {}         -- person puppets
M.ships = {}          -- ship puppets
M.stats = { active = 0, people = 0, ships = 0 }
M.people_pos = {}     -- { id, x, y, z } for picking (visible puppets only)
M.animal_pos = {}     -- { kind, cell, x, y, z }
M.ship_pos = {}       -- { index, kind, x, y, z }

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

-- A column of n walkers from village cell a to cell b (a family leaving, colonists).
function M.column(W, a, b, n)
  local g = W.grid
  if not a or not b or a == b then return end
  local ax, ay, az = g.X[a], g.Y[a], g.Z[a]
  local bx, by, bz = g.X[b], g.Y[b], g.Z[b]
  local d = math.acos(math.max(-1, math.min(1, ax * bx + ay * by + az * bz)))
  local col = { a = { ax, ay, az }, b = { bx, by, bz }, n = math.max(2, math.min(6, n or 4)), t = 0,
    dur = math.max(8, d / (M.WALK * 1.4)) }
  M.columns[#M.columns + 1] = col
  if #M.columns > 3 then table.remove(M.columns, 1) end
end

function M.init(W, urls, surface)
  M.W, M.surface = W, surface
  M.columns = {}
  if #M.pool == 0 then
    make_pool(M.pool, urls.deer_factory, M.DEER, "deer", make_buffer("deer", { 0.70, 0.48, 0.30 }, { 0.40, 0.29, 0.21 }, 1,
      { 0.86, 0.80, 0.70 }, M.SCALE))
    make_pool(M.pool, urls.wolf_factory, M.WOLVES, "wolf", make_buffer("wolf", { 0.52, 0.54, 0.58 }, { 0.33, 0.34, 0.37 }, 1,
      { 0.3, 0.3, 0.32 }, M.SCALE))
    make_pool(M.people, urls.person_factory, M.PEOPLE, "person", nil)
    make_pool(M.migrants, urls.person_factory, M.MIGRANTS, "person", nil)
    local feed = require("game.view.feed")
    feed.on("migration", function(Wf, r)
      local d = r.data
      if not d or not d.to or d.to == 0 then return end
      local from, to = ents.village(Wf, d.from), ents.village(Wf, d.to)
      if from and to then M.column(Wf, from.cell, to.cell, r.mag) end
    end)
    feed.on("village_founded", function(Wf, r)
      local d = r.data
      local v = d and ents.village(Wf, d.village)
      local from = v and v.from and ents.village(Wf, v.from)
      if v and from then M.column(Wf, from.cell, v.cell, 5) end
    end)
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
  for _, list in ipairs({ M.pool, M.people, M.ships, M.migrants }) do
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

-- Assign animal puppets to the densest cells near the view focus.
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

-- Assign person puppets to residents of villages near the focus.
local function assign_people(W, fx, fy, fz)
  local g = W.grid
  local want = {}
  for _, v in ents.each_village(W) do
    local c = v.cell
    local d = g.X[c] * fx + g.Y[c] * fy + g.Z[c] * fz
    if d > 0.95 and type(v.members) == "table" then
      local n = 0
      for k = 1, #v.members do
        local pid = v.members[k]
        if ents.is_alive(W, pid) then
          want[#want + 1] = { pid = pid, cell = c, vid = v.id }
          n = n + 1
          if n >= M.PER_VILLAGE then break end
        end
      end
    end
  end
  -- group residents by job so pool entries (drawn in creation order) share buffers in runs
  for i = 1, #want do
    local person = ents.person(W, want[i].pid)
    want[i].job = (person and type(person.job) == "string" and job_res[person.job]) and person.job or "default"
  end
  table.sort(want, function(a, b) if a.job ~= b.job then return a.job < b.job end return a.pid < b.pid end)
  local keep = {}
  for i = 1, #want do keep[want[i].pid] = want[i] end
  -- keep existing assignments, free the rest
  local placed = {}
  local same = true
  for i = 1, #want do
    local p = M.people[i]
    if not p or p.pid ~= want[i].pid then same = false; break end
  end
  for _, p in ipairs(M.people) do
    if same and p.pid and keep[p.pid] then placed[p.pid] = true
    else p.pid = nil; set_on(p, false) end
  end
  local free = 1
  for i = 1, #want do
    local w = want[i]
    if not placed[w.pid] then
      while M.people[free] and M.people[free].pid do free = free + 1 end
      local p = M.people[free]
      if not p then break end
      p.pid = w.pid
      p.cell = w.cell
      p.vid = w.vid
      p.home_lat, p.home_lon = g.lat[w.cell], g.lon[w.cell]
      -- start somewhere in the settlement (a ring of houses ~0.1-0.35 world units out)
      local a, rr = rnd() * 6.283, (0.012 + rnd() * 0.022)
      p.lat = p.home_lat + math.cos(a) * rr
      p.lon = p.home_lon + math.sin(a) * rr / max(0.2, cos(p.home_lat))
      p.head = rnd() * 6.283
      local person = ents.person(W, w.pid)
      local job = person and person.job
      if type(job) ~= "string" or not job_res[job] then job = "default" end
      p.job = job
      p.tlat, p.tlon, p.wait = nil, nil, rnd() * 3
      p.still = true
      go.set(p.url, "vertices", job_res_still[job])
      set_on(p, true)
    end
  end
end

local pos, rot = vmath.vector3(), vmath.quat()

-- Where a villager goes next (lat, lon, seconds of work there), by job and the local season.
local function next_target(W, p)
  local v = ents.village(W, p.vid)
  local g = W.grid
  local hl, ho = p.home_lat, p.home_lon
  local cl = max(0.2, cos(hl))
  local function around(lat, lon, r)
    local a, d = rnd() * 6.283, r * (0.3 + 0.7 * rnd())
    return lat + math.cos(a) * d, lon + math.sin(a) * d / cl
  end
  local shift = W.vgeo and v and W.vgeo[v.id] and W.vgeo[v.id].shift or 0
  local toy = (W.tick + shift) % 48
  local job = p.job
  local u = rnd()
  if v and job == "farmer" and toy < 36 and type(v.farms) == "table" and #v.farms > 0 and u < 0.75 then
    local c = v.farms[1 + floor(rnd() * #v.farms)]
    local la, lo = around(g.lat[c], g.lon[c], g.spacing * 0.35)
    return la, lo, 5 + rnd() * 9
  elseif v and job == "fisher" and type(v.fishing) == "table" and #v.fishing > 0 and u < 0.7 then
    local c = v.fishing[1 + floor(rnd() * #v.fishing)]
    return g.lat[c], g.lon[c], 4 + rnd() * 6     -- walks until the shore stops them
  elseif v and job == "woodcutter" and u < 0.7 then
    local b, best, bc = v.cell * 6 - 6, 0.25, nil
    for k = 1, g.nbr_count[v.cell] do
      local j = g.nbr[b + k]
      if W.geo.ocean[j] == 0 and W.F.forest[j] > best then best, bc = W.F.forest[j], j end
    end
    if bc then
      local la, lo = around(g.lat[bc], g.lon[bc], g.spacing * 0.25)
      return la, lo, 6 + rnd() * 8
    end
  elseif job == "child" then
    local la, lo = around(hl, ho, 0.022)
    return la, lo, rnd() * 1.5
  elseif job == "elder" then
    local la, lo = around(hl, ho, 0.012)
    return la, lo, 4 + rnd() * 10
  end
  local la, lo = around(hl, ho, 0.03)
  return la, lo, 2 + rnd() * 6
end

-- One villager's frame: walk toward the target, stop at water, then work/wait.
local function step_person(W, p, dt, frozen, speedk)
  if frozen then return end
  if not p.tlat then
    p.wait = (p.wait or 0) - dt
    if p.wait > 0 then return end
    local la, lo, w = next_target(W, p)
    p.tlat, p.tlon, p.work = la, lo, w
  end
  local cl = max(0.2, cos(p.lat))
  local dl, dn = p.tlat - p.lat, (p.tlon - p.lon) * cl
  local d = sqrt(dl * dl + dn * dn)
  local spd = M.WALK * speedk * (p.job == "child" and 1.6 or (p.job == "elder" and 0.6 or 1.0))
  local step = spd * dt
  local arrive = d <= step
  if not arrive then
    p.head = atan2(dn, dl)
    local nl, no = p.lat + dl / d * step, p.lon + dn / d * step / cl
    -- water stops walkers (fishers wait at the shore)
    local x, y, z = unit(nl, no)
    local _, vtx = M.surface(x, y, z, p.v)
    local h = M.height and M.height(vtx) or 1
    if h <= 0.01 then arrive = true
    else p.lat, p.lon = nl, no end
  end
  if arrive then
    p.tlat, p.tlon = nil, nil
    p.wait = p.work or 2
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

local function place(p, x, y, z, r, yaw, scale)
  pos.x, pos.y, pos.z = x * r, y * r, z * r
  go.set_position(pos, p.id)
  go.set_rotation(vmx.set_up_yaw(rot, x, y, z, yaw), p.id)
  go.set_scale(scale, p.id)
end

local function update_ships(W, dt)
  local R = state.R
  local nt, nb = 0, 0
  local sp = M.ship_pos
  for i = #sp, 1, -1 do sp[i] = nil end
  local used = {}
  for _, lname in ipairs({ "ships", "boats" }) do
  local list = ents.ship_list(W, lname)
  for i = 1, #list do
    local s = list[i]
    if type(s) == "table" and s.alive ~= false and not s.sunk then
      local x, y, z = ents.ship_dir(W, s)
      if x and lname == "boats" then
        -- several boats share a fishing cell: spread them around it
        local a = i * 2.39996
        local off = 0.018 + (i % 3) * 0.008
        local ex, ez = z, -x
        local el = math.sqrt(ex * ex + ez * ez)
        if el > 1e-6 then
          ex, ez = ex / el, ez / el
          local nx, ny, nz = -y * ez, ez * x - ex * z, y * ex
          x, y, z = x + (ex * cos(a) + nx * sin(a)) * off, y + ny * sin(a) * off, z + (ez * cos(a) + nz * sin(a)) * off
          local l = math.sqrt(x * x + y * y + z * z)
          x, y, z = x / l, y / l, z / l
        end
      end
      if x then
        local kind = lname == "boats" and "boat" or ents.ship_kind(s)
        local p
        for _, q in ipairs(M.ships) do
          if q.kind == kind and not used[q] then p = q; break end
        end
        if p then
          used[p] = true
          -- heading from movement (keep the last heading when still)
          if p.px then
            local dx, dy, dz = x - p.px, y - p.py, z - p.pz
            if dx * dx + dy * dy + dz * dz > 1e-10 then
              -- project movement onto local east/north
              local ex, ez = z, -x
              local el = sqrt(ex * ex + ez * ez)
              if el > 1e-6 then
                ex, ez = ex / el, ez / el
                local nx, ny, nz = -y * ez, ez * x - ex * z, y * ex
                p.head = atan2(dx * nx + dy * ny + dz * nz, dx * ex + dz * ez)
              end
            end
          end
          p.px, p.py, p.pz = x, y, z
          set_on(p, true)
          place(p, x, y, z, R + 0.004, -(p.head or 0), M.SCALE)
          sp[#sp + 1] = { index = i, list = lname, kind = kind, x = x * R, y = y * R, z = z * R }
          p.list, p.index = lname, i
          if kind == "tanker" then nt = nt + 1 else nb = nb + 1 end
        end
      end
    end
  end
  end
  for _, q in ipairs(M.ships) do if not used[q] then set_on(q, false) end end
  M.stats.ships = nt + nb
  M.stats.ship_draws = (nt > 0 and 1 or 0) + (nb > 0 and 1 or 0)
end

-- migrant columns: walkers strung out along the way, arriving and vanishing
local function update_migrants(W, dt, zoom, frozen)
  local speedk = M.speed_k or 1
  local nm = 0
  local show = zoom >= M.MIGRANT_ZOOM
  local slot = 1
  for ci = #M.columns, 1, -1 do
    local col = M.columns[ci]
    if not frozen then col.t = col.t + dt * speedk end
    if col.t > col.dur + 2 then table.remove(M.columns, ci) end
  end
  for _, col in ipairs(M.columns) do
    for k = 1, col.n do
      local p = M.migrants[slot]
      if not p then break end
      slot = slot + 1
      local f = (col.t - (k - 1) * 0.9) / col.dur
      local on = show and f > 0 and f < 1
      if on then
        if not p.res_set then go.set(p.url, "vertices", job_res.default); p.res_set = true end
        local a, b = col.a, col.b
        local wobble = 0.004 * math.sin(k * 2.1)
        local x, y, z = a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f, a[3] + (b[3] - a[3]) * f
        local l = sqrt(x * x + y * y + z * z)
        x, y, z = x / l + wobble * 0.3, y / l + wobble, z / l - wobble * 0.3
        l = sqrt(x * x + y * y + z * z)
        x, y, z = x / l, y / l, z / l
        local r, v = M.surface(x, y, z, p.v)
        p.v = v
        -- heading along the path (east/north components of b - a)
        local ex, ez = z, -x
        local el = sqrt(ex * ex + ez * ez) + 1e-9
        ex, ez = ex / el, ez / el
        local nx, ny, nz = -y * ez, ez * x - ex * z, y * ex
        local dx, dy, dz = b[1] - a[1], b[2] - a[2], b[3] - a[3]
        local head = atan2(dx * nx + dy * ny + dz * nz, dx * ex + dz * ez)
        place(p, x, y, z, r, -head, M.PERSON_SCALE)
        nm = nm + 1
      end
      if on ~= p.on then set_on(p, on) end
    end
  end
  for i = slot, #M.migrants do
    local p = M.migrants[i]
    if p.on then set_on(p, false) end
  end
  M.stats.migrants = nm
end

function M.update(W, dt, zoom, frozen)
  update_ships(W, dt)
  update_migrants(W, dt, zoom, frozen)
  local active = 0
  local ap, pp = M.animal_pos, M.people_pos
  for i = #ap, 1, -1 do ap[i] = nil end
  for i = #pp, 1, -1 do pp[i] = nil end
  if zoom < M.MIN_ZOOM then
    for _, p in ipairs(M.pool) do if p.on then p.cell = 0; set_on(p, false) end end
    for _, p in ipairs(M.people) do if p.on then p.pid = nil; set_on(p, false) end end
    M.stats.active, M.stats.people, M.stats.draws = 0, 0, 0
    return
  end
  M.assign_timer = M.assign_timer - dt
  if M.assign_timer <= 0 then
    M.assign_timer = 1.0
    local fx, fy, fz = state.focus_x or 0, state.focus_y or 0, state.focus_z or 1
    assign_animals(W, fx, fy, fz)
    assign_people(W, fx, fy, fz)
  end
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
  local np = 0
  local speedk = M.speed_k or 1
  for _, p in ipairs(M.people) do
    if p.on then
      np = np + 1
      step_person(W, p, dt, frozen, speedk)
      local walking = p.tlat ~= nil and not frozen
      if walking == p.still then
        p.still = not walking
        go.set(p.url, "vertices", walking and job_res[p.job] or job_res_still[p.job])
      end
      local x, y, z = unit(p.lat, p.lon)
      local r, v = M.surface(x, y, z, p.v)
      p.v = v
      place(p, x, y, z, r, -p.head, M.PERSON_SCALE)
      pp[#pp + 1] = { id = p.pid, x = x * r, y = y * r, z = z * r }
    end
  end
  M.stats.active = active + np
  M.stats.people = np
  -- draw-call estimate: one batch per distinct mover buffer in use
  local kinds, n = {}, 0
  for _, p in ipairs(M.pool) do if p.on and not kinds[p.kind] then kinds[p.kind] = true; n = n + 1 end end
  for _, p in ipairs(M.people) do
    local k = "job_" .. tostring(p.job)
    if p.on and not kinds[k] then kinds[k] = true; n = n + 1 end
  end
  M.stats.draws = n
end

--- Unit direction of a ship puppet (smooth, including boat spread), or nil.
function M.ship_dir(list, index)
  for _, q in ipairs(M.ships) do
    if q.on and q.list == list and q.index == index and q.px then return q.px, q.py, q.pz end
  end
  return nil
end

--- Current world position of a person puppet (for follow), or nil.
function M.person_dir(pid)
  for _, p in ipairs(M.people) do
    if p.on and p.pid == pid then return unit(p.lat, p.lon) end
  end
  return nil
end

return M
