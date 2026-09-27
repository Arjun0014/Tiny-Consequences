-- View orchestrator (Stage 1.8, 05_ARCHITECTURE.md §7): builds and updates every view manager
-- from a read-only world W. The view never writes sim state.
local state = require("game.view.state")
local camera = require("game.view.camera")
local planet = require("game.view.planet")
local terrain = require("game.view.terrain")
local props = require("game.view.props")
local clouds = require("game.view.clouds")
local puppets = require("game.view.puppets")
local fx = require("game.view.fx")
local selring = require("game.view.selring")
local rivers = require("game.view.rivers")
local feed = require("game.view.feed")
local aim = require("game.view.aim")
local powers_ui = require("game.ui.powers")
local grid = require("sim.grid")
local clock = require("game.platform.clock")
local speed = require("game.platform.speed")

local M = {}

M.ATMO_RADIUS = 1.3      -- shell radius in planet radii (glow fades well inside it)

local H_POS, H_NRM = hash("position"), hash("normal")

M.W, M.PL = nil, nil
M.ready = false
M.view_ms = 0
M.view_ms_avg = 0
M.view_ms_max, M.win_max, M.win_n = 0, 0, 0
M.view_ms_med, M.view_ms_p90 = 0, 0
M.samples = {}
M.parts = { terrain = 0, props = 0, clouds = 0, movers = 0 }
M.urls = {}

local function build_atmo()
  local g = grid.build(3, { neighbours = false })
  local faces = g.faces
  local nf = #faces / 3
  local buf = buffer.create(nf * 3, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
  })
  local pos, nrm = buffer.get_stream(buf, H_POS), buffer.get_stream(buf, H_NRM)
  local r = state.R * M.ATMO_RADIUS
  for i = 1, #faces do
    local v = faces[i]
    local p = (i - 1) * 3
    pos[p + 1], pos[p + 2], pos[p + 3] = g.X[v] * r, g.Y[v] * r, g.Z[v] * r
    nrm[p + 1], nrm[p + 2], nrm[p + 3] = g.X[v], g.Y[v], g.Z[v]
  end
  resource.set_buffer(go.get(M.urls.atmo, "vertices"), buf)
end

function M.surface(x, y, z, start)
  if not M.PL then return state.R end
  return planet.surface_radius(M.PL, x, y, z, start)
end

--- Builds all view data for world W. step(text, frac) is called often (it may yield).
function M.build(W, urls, step)
  step = step or function() end
  M.urls = urls
  M.ready = false
  state.planet_on = false
  M.W = W
  step("Sculpting coastlines", 0)
  local PL = planet.build(W, { step = function(f) step("Sculpting coastlines", f) end })
  M.PL = PL
  step("Painting the land", 0)
  terrain.build(PL, W, function(f) step("Painting the land", f) end)
  rivers.init(W, PL, "/rivers#mesh")
  rivers.rebuild(W)
  if not M.atmo_built then build_atmo(); M.atmo_built = true end
  step("Planting forests", 0)
  props.init(PL, W, urls.chunk_factory, terrain.tex)
  M.village_sig = nil
  M.sync_villages(W)
  props.build_all(function(f) step("Planting forests", f) end)
  step("Gathering clouds", 0)
  clouds.init(W, urls.cloud_factory, urls.rain_factory)
  puppets.init(W, urls, M.surface)
  puppets.height = function(v) return M.PL.h[v] end
  selring.init(urls.ring_factory, M.surface)
  aim.init(urls.ring_factory, M.surface)
  fx.init(W, urls.flame_factory, urls.smoke_factory, M.surface)
  camera.height_fn = function(x, y, z) return M.surface(x, y, z) end
  camera.focus_cell = nil
  feed.reset(W)
  M.ready = true
  state.planet_on = true
  step("Ready", 1)
end

--- Rebuild terrain + props after a coastline change (sea level / elevation). Rare; blocking.
function M.rebuild_geometry()
  local W = M.W
  M.PL = planet.build(W, {})
  terrain.build(M.PL, W)
  rivers.init(W, M.PL, "/rivers#mesh")
  rivers.rebuild(W)
  props.init(M.PL, W, M.urls.chunk_factory, terrain.tex)
  M.village_sig = nil
  M.sync_villages(W)
  props.build_all()
end

--- Finish all pending view work now (bridge flush_view: stable screenshots after run_ticks).
function M.flush()
  if not M.ready then return end
  camera.update(0)
  terrain.recolour_all(M.W)
  props.flush(M.W, camera.zoom)
end

function M.set_frozen(on)
  state.frozen = on and true or false
end

-- Village entities (sim/systems/villages.lua) -> props.set_villages: living villages
-- (alive ~= false) get a settlement sized by v.houses (else pop / 4), in their culture's
-- style, with a pier if they fish and haystacks after their own harvest; dead ones show ruins
-- only while v.ruin is set.
local function local_toy(W, v)
  local geo = W.vgeo and W.vgeo[v.id]
  local shift = geo and geo.shift or 0
  return (W.tick + shift) % 48
end

function M.sync_villages(W)
  local list = W.ent and W.ent.villages
  if type(list) ~= "table" then return end
  local out, sig = {}, 0
  for i = 1, #list do
    local v = list[i]
    if type(v) == "table" and type(v.cell) == "number" then
      local entry = nil
      if v.alive ~= false then
        local pop = tonumber(v.pop) or (type(v.members) == "table" and #v.members) or 0
        local houses = tonumber(v.houses) or math.max(3, math.ceil(pop / 4))
        local toy = local_toy(W, v)
        entry = { cell = v.cell, houses = houses, ruin = false, culture = v.culture or 1,
          fishing = type(v.fishing) == "table" and #v.fishing > 0, harvest = toy >= 32 }
      elseif v.ruin then
        entry = { cell = v.cell, houses = 0, ruin = true, culture = v.culture or 1 }
      end
      if entry then
        out[#out + 1] = entry
        sig = sig + v.cell * 7 + entry.houses * 131 + (entry.ruin and 997 or 0) + (entry.harvest and 5 or 0)
      end
    end
  end
  sig = sig + #out * 100003
  if sig ~= M.village_sig then
    M.village_sig = sig
    props.set_villages(out)
  end
end
local sync_villages = M.sync_villages

--- Per-frame update. dt = real seconds.
function M.update(dt)
  local t0 = clock.now_ms()
  if not state.frozen then state.time = state.time + dt end
  camera.update(dt)
  state.u_cam.w = state.time
  state.u_anim.x = state.time
  if M.W then
    state.u_anim.z = M.W.tick % 48
    state.u_anim.w = 1
  end
  state.u_sky.x = state.time
  local P = M.parts
  if M.ready then
    local W = M.W
    if math.abs((W.G.sea_level or 0) - M.PL.sea_level) > 0.05 then M.rebuild_geometry() end
    M.village_timer = (M.village_timer or 0) - dt
    if M.village_timer <= 0 then
      M.village_timer = 1.0
      sync_villages(W)
    end
    -- at high sim speeds the world changes every frame: spend less per frame on refreshes
    -- (05 §9: view updates <= 4 ms per frame amortised)
    local sp_level = speed.get()
    local fast = sp_level >= 3
    puppets.speed_k = (sp_level <= 1 and 1) or (sp_level == 2 and 1.5) or (sp_level == 3 and 2.5) or 3
    props.BUDGET_MS = fast and 1.4 or 2.5
    props.JOB_GAP_MS = fast and 250 or 0      -- at most ~4 chunk rebuilds a second at high speed
    terrain.CELLS_PER_FRAME = fast and 160 or 240
    terrain.BUDGET_MS = fast and 0.9 or 1.4
    rivers.REBUILD_S = fast and 4 or 1.5
    terrain.MAX_RATE = fast and 3 or 4
    local a = clock.now_ms()
    terrain.update(W, dt)
    local b = clock.now_ms()
    rivers.update(W, dt)
    feed.poll(W)
    local b2 = clock.now_ms()
    local b3 = clock.now_ms()
    props.update(W, dt, camera.zoom)
    local c = clock.now_ms()
    clouds.update(W, dt, state.frozen)
    local d = clock.now_ms()
    puppets.update(W, dt, camera.zoom, state.frozen)
    fx.update(W, dt, state.frozen)
    selring.update(W, dt)
    local armed = powers_ui.armed and powers_ui.DEFS[powers_ui.armed]
    aim.update(W, dt, armed and M.aim_cell or nil, armed and armed.radius or 0)
    local e = clock.now_ms()
    P.terrain = P.terrain + (b - a - P.terrain) * 0.05
    P.rivers = (P.rivers or 0) + (b2 - b - (P.rivers or 0)) * 0.05
    P.props = P.props + (c - b3 - P.props) * 0.05
    P.clouds = P.clouds + (d - c - P.clouds) * 0.05
    P.movers = P.movers + (e - d - P.movers) * 0.05
  end
  local ms = clock.now_ms() - t0
  M.view_ms = ms
  M.view_ms_avg = M.view_ms_avg + (ms - M.view_ms_avg) * 0.05
  M.win_max = math.max(M.win_max, ms)
  M.win_n = M.win_n + 1
  M.samples[M.win_n] = ms
  if M.win_n >= 60 then
    table.sort(M.samples)
    M.view_ms_med, M.view_ms_p90 = M.samples[30], M.samples[54]
    M.view_ms_max, M.win_max, M.win_n = M.win_max, 0, 0
  end
end

function M.cam_info()
  local lat, lon = camera.latlon()
  return { lat = math.deg(lat), lon = math.deg(lon), zoom = camera.zoom }
end

--- Numbers for the bridge / debug overlay.
function M.stats()
  local ps = props.stats
  local draw = 1                                  -- sky
  if M.ready then
    draw = draw + 1 + 1                           -- terrain, atmosphere
      + ps.visible                                -- prop chunks in view
      + ((clouds.stats.active > 0) and 1 or 0)
      + (puppets.stats.draws or 0) + (puppets.stats.ship_draws or 0)
      + ((fx.stats.fires > 0) and 2 or 0)
      + (selring.on and 1 or 0)
  end
  return {
    view_ms = M.view_ms_med,
    view_ms_avg = M.view_ms_avg,
    view_ms_p90 = M.view_ms_p90,
    view_ms_last = M.view_ms,
    view_ms_max = M.view_ms_max,
    view_parts = M.parts,
    rebuild_ms_max = ps.rebuild_ms_max,
    props_top_ms = ps.top_ms,
    props_phase_max = { plan = ps.plan_max, create = ps.create_max, write = ps.write_max, finish = ps.finish_max },
    prop_count = ps.visible_instances,
    prop_total = ps.instances,
    prop_verts = ps.verts,
    chunks_visible = ps.visible,
    chunks_rebuilt = ps.rebuilt,
    chunks_dirty = ps.dirty or 0,
    draw_calls = draw + 5,                        -- + GUI (layer-batched: boxes + 3 fonts + top)
    clouds = clouds.stats.active,
    cloud_cover = clouds.stats.cover,
    puppets = puppets.stats.active,
    fires = fx.stats.fires,
    recolours = terrain.stats.recolours,
    cam = M.cam_info(),
    people_puppets = puppets.stats.people,
    ships = puppets.stats.ships,
  }
end

return M
