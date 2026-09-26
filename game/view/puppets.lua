-- Puppets (Stage 1.8/1.9): pooled movers driven by Lua, world-space material (engine-batched).
--   * people: residents of villages near the view focus when zoomed in (W.ent.villages[*].members
--     -> W.ent.people[id]); body colour by job; they wander around their village centre.
--   * animals: deer/wolves near cells with high grazers/predators density when zoomed in.
--   * ships: tankers and boats from W.ent.ships at any zoom (if the sim provides positions).
-- Hard cap: pool sizes (05 §9: <= 300 Lua-driven movers per frame). Positions are exposed
-- for picking (M.people_pos / M.animals / M.ships_pos).
local propgen = require("game.view.propgen")
local state = require("game.view.state")
local vmx = require("game.view.vmx")
local ents = require("game.view.ents")

local M = {}

M.DEER, M.WOLVES, M.PEOPLE, M.TANKERS, M.BOATS = 60, 24, 120, 8, 24
M.MIN_ZOOM = 0.55
M.GRAZER_MIN, M.PRED_MIN = 0.12, 0.08
M.PER_CELL = 6
M.PER_VILLAGE = 16
M.SCALE = 1.3
M.PERSON_SCALE = 1.9   -- villagers are a touch oversized so they read at close zoom

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
local SKIN = { 0.98, 0.84, 0.70 }

M.pool = {}           -- animals
M.people = {}         -- person puppets
M.ships = {}          -- ship puppets
M.stats = { active = 0, people = 0, ships = 0 }
M.people_pos = {}     -- { id, x, y, z } for picking (visible puppets only)
M.animal_pos = {}     -- { kind, cell, x, y, z }
M.ship_pos = {}       -- { index, kind, x, y, z }

local function make_buffer(kind, c1, c2, scale)
  local t = propgen.mover_template(kind)
  local buf = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_COL, type = buffer.VALUE_TYPE_FLOAT32, count = 2 },
  })
  propgen.write_template(t, buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM),
    buffer.get_stream(buf, H_COL), c1, c2, scale or 1)
  return buf
end

local seed_h = 7
local function rnd()
  seed_h = (seed_h * 48271) % 2147483647
  return seed_h / 2147483647
end

local job_res = {}
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
    make_pool(M.pool, urls.deer_factory, M.DEER, "deer", make_buffer("deer", { 0.72, 0.50, 0.32 }, { 0.45, 0.32, 0.22 }))
    make_pool(M.pool, urls.wolf_factory, M.WOLVES, "wolf", make_buffer("wolf", { 0.55, 0.57, 0.62 }, { 0.35, 0.36, 0.40 }))
    make_pool(M.people, urls.person_factory, M.PEOPLE, "person", nil)
    make_pool(M.ships, urls.tanker_factory, M.TANKERS, "tanker",
      make_buffer("tanker", { 0.55, 0.18, 0.2 }, { 0.96, 0.94, 0.9 }))
    make_pool(M.ships, urls.boat_factory, M.BOATS, "boat", make_buffer("boat", { 0.62, 0.42, 0.26 }, { 0.98, 0.96, 0.9 }))
    -- one shared buffer resource per job colour for people
    res_gen = res_gen + 1
    for job, col in pairs(M.JOB_COLORS) do
      job_res[job] = resource.create_buffer("/tc/person_" .. job .. res_gen .. ".bufferc",
        { buffer = make_buffer("person", col, SKIN) })
    end
  end
  for _, list in ipairs({ M.pool, M.people, M.ships }) do
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
          want[#want + 1] = { pid = pid, cell = c }
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
      local sp = g.spacing * 0.22
      p.home_lat, p.home_lon = g.lat[w.cell], g.lon[w.cell]
      p.lat = p.home_lat + (rnd() - 0.5) * sp
      p.lon = p.home_lon + (rnd() - 0.5) * sp
      p.head = rnd() * 6.283
      local person = ents.person(W, w.pid)
      local job = person and person.job
      if type(job) ~= "string" or not job_res[job] then job = "default" end
      go.set(p.url, "vertices", job_res[job])
      p.job = job
      set_on(p, true)
    end
  end
end

local pos, rot = vmath.vector3(), vmath.quat()

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

function M.update(W, dt, zoom, frozen)
  update_ships(W, dt)
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
  for _, p in ipairs(M.people) do
    if p.on then
      np = np + 1
      walk(p, dt, frozen, 0.0025, sp * 0.18)
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
