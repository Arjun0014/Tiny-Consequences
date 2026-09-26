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
local grid = require("sim.grid")
local clock = require("game.platform.clock")

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

function M.surface(x, y, z)
  if not M.PL then return state.R end
  return planet.surface_radius(M.PL, x, y, z)
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
  if not M.atmo_built then build_atmo(); M.atmo_built = true end
  step("Planting forests", 0)
  props.init(PL, W, urls.chunk_factory)
  props.build_all(function(f) step("Planting forests", f) end)
  step("Gathering clouds", 0)
  clouds.init(W, urls.cloud_factory)
  puppets.init(W, urls.deer_factory, urls.wolf_factory, M.surface)
  fx.init(W, urls.flame_factory, urls.smoke_factory, M.surface)
  camera.height_fn = function(x, y, z) return M.surface(x, y, z) end
  camera.focus_cell = nil
  M.ready = true
  state.planet_on = true
  step("Ready", 1)
end

--- Rebuild terrain + props after a coastline change (sea level / elevation). Rare; blocking.
function M.rebuild_geometry()
  local W = M.W
  M.PL = planet.build(W, {})
  terrain.build(M.PL, W)
  props.init(M.PL, W, M.urls.chunk_factory)
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

-- Village entities (Stage 1.7) -> props.set_villages hook. Defensive about the entity shape:
-- { cell | centre, houses | population | pop, ruin | abandoned }. Until entities exist the props
-- fall back to the land_use field (villages on land_use < 0 cells, crops on land_use > 0).
local village_sig = nil
local function sync_villages(W)
  local list = W.ent and W.ent.villages
  if type(list) ~= "table" then return end
  local out, sig = {}, 0
  local keys = {}
  for k in pairs(list) do keys[#keys + 1] = k end
  table.sort(keys, function(a, b) return tostring(a) < tostring(b) end)
  for _, k in ipairs(keys) do
    local v = list[k]
    if type(v) == "table" then
      local cell = v.cell or v.centre or v.center
      if type(cell) == "number" then
        local pop = v.population or v.pop or (type(v.people) == "table" and #v.people) or 0
        local houses = v.houses or math.max(3, math.min(14, math.ceil(pop / 6)))
        local ruin = (v.ruin or v.abandoned or v.dead) and true or false
        out[#out + 1] = { cell = cell, houses = houses, ruin = ruin }
        sig = sig + cell * 7 + houses * 131 + (ruin and 997 or 0)
      end
    end
  end
  sig = sig + #out * 100003
  if sig ~= village_sig then
    village_sig = sig
    props.set_villages(out)
  end
end

--- Per-frame update. dt = real seconds.
function M.update(dt)
  local t0 = clock.now_ms()
  if not state.frozen then state.time = state.time + dt end
  camera.update(dt)
  state.u_cam.w = state.time
  state.u_anim.x = state.time
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
    local a = clock.now_ms()
    terrain.update(W, dt)
    local b = clock.now_ms()
    props.update(W, dt, camera.zoom)
    local c = clock.now_ms()
    clouds.update(W, dt, state.frozen)
    local d = clock.now_ms()
    puppets.update(W, dt, camera.zoom, state.frozen)
    fx.update(W, dt, state.frozen)
    local e = clock.now_ms()
    P.terrain = P.terrain + (b - a - P.terrain) * 0.05
    P.props = P.props + (c - b - P.props) * 0.05
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

--- Numbers for the bridge / debug overlay.
function M.stats()
  local ps = props.stats
  local draw = 1                                  -- sky
  if M.ready then
    draw = draw + 1 + 1                           -- terrain, atmosphere
      + ps.visible                                -- prop chunks in view
      + ((clouds.stats.active > 0) and 1 or 0)
      + ((puppets.stats.active > 0) and 1 or 0)
      + ((fx.stats.fires > 0) and 1 or 0)
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
    draw_calls = draw + 2,                        -- + GUI
    clouds = clouds.stats.active,
    cloud_cover = clouds.stats.cover,
    puppets = puppets.stats.active,
    fires = fx.stats.fires,
    recolours = terrain.stats.recolours,
    cam = { lat = camera.lat, lon = camera.lon, zoom = camera.zoom },
  }
end

return M
