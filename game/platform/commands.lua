-- Bridge commands for the game (05_ARCHITECTURE.md §10). Registered by main.script; `G` is
-- the main script's self (world W, seed, fast-forward state, start_loading).
local bridge = require("game.platform.bridge")
local clock = require("game.platform.clock")
local state = require("game.view.state")
local camera = require("game.view.camera")
local view = require("game.view.view")
local selection = require("game.ui.selection")
local uistate = require("game.ui.uistate")
local inspect = require("game.ui.inspect")
local ents = require("game.view.ents")
local picking = require("game.view.picking")
local controls = require("game.view.controls")

local M = {}

local function first_village(W)
  for _, v in ents.each_village(W) do return v end
  return nil
end

local function first_person(W)
  for _, v in ents.each_village(W) do
    if type(v.members) == "table" then
      for k = 1, #v.members do
        if ents.person(W, v.members[k]) and ents.is_alive(W, v.members[k]) then return v.members[k] end
      end
    end
  end
  return nil
end

local function summary(W, sel)
  local data = inspect.build(W, sel)
  if not data then return nil end
  local rows = {}
  for _, it in ipairs(data.items) do
    rows[#rows + 1] = (it.label or it.text or "") .. ((it.value and (": " .. it.value)) or "") .. (it.hl and " [!]" or "")
  end
  return { kind = data.kind, title = data.title, subtitle = data.subtitle, rows = rows }
end

function M.register(G)
  -- camera{preset | lat, lon, zoom | drag={dx,dy} | follow=true}; lat/lon in degrees.
  bridge.register("camera", function(args)
    local W = assert(G.W, "world not ready")
    if args.drag then
      camera.drag(args.drag.dx or args.drag[1] or 0, args.drag.dy or args.drag[2] or 0)
    elseif args.follow then
      assert(controls.follow_selection(W), "nothing selected to follow")
    elseif args.preset then
      local lat, lon, zoom = camera.preset(args.preset, W)
      assert(lat, "unknown camera preset " .. tostring(args.preset))
      camera.set(lat, lon, zoom, not args.tween)
    else
      local tl, tn = camera.target_latlon()
      local lat = args.lat and math.rad(args.lat) or tl
      local lon = args.lon and math.rad(args.lon) or tn
      camera.set(lat, lon, args.zoom or camera.tzoom, not args.tween)
    end
    local lat, lon = camera.target_latlon()
    return { lat = math.deg(lat), lon = math.deg(lon), zoom = camera.tzoom, focus_cell = camera.focus_cell }
  end)

  -- run_ticks{n}: chunked fast-forward; __TC.ff_remaining reaches 0 when done.
  bridge.register("run_ticks", function(args)
    assert(G.W, "world not ready")
    local n = math.floor(tonumber(args.n) or 0)
    assert(n >= 0, "run_ticks: n must be >= 0")
    G.ff = { target = G.W.tick + n, t0 = clock.now_ms() }
    return { from = G.W.tick, to = G.ff.target }
  end)

  bridge.register("freeze_visuals", function(args)
    view.set_frozen(args.on ~= false)
    return { frozen = state.frozen }
  end)

  bridge.register("new_world", function(args)
    selection.clear()
    G.start_loading(G, math.floor(tonumber(args.seed) or G.seed))
    return { seed = G.seed }
  end)

  -- select{cell=c | village=id|"first" | person=id|"first" | planet=true | ship=i | none=true}
  bridge.register("select", function(args)
    local W = assert(G.W, "world not ready")
    local sel
    if args.none then selection.clear(); return { selected = false } end
    if args.cell then
      local c = tonumber(args.cell) or camera.focus_candidate(W)
      assert(c >= 1 and c <= W.n, "cell out of range")
      sel = { kind = "cell", cell = c }
    elseif args.village then
      local v = args.village == "first" and first_village(W) or ents.village(W, args.village)
      assert(v, "no such village (villages: " .. #ents.villages(W) .. ")")
      sel = { kind = "village", id = v.id, cell = v.cell }
    elseif args.person then
      local pid = args.person == "first" and first_person(W) or args.person
      assert(pid and ents.person(W, pid), "no such person")
      sel = { kind = "person", id = pid }
    elseif args.planet then
      sel = { kind = "planet" }
    elseif args.ship then
      sel = { kind = "ship", index = tonumber(args.ship) or 1, list = args.list or "ships" }
    else
      error("select: give cell, village, person, planet, ship or none")
    end
    selection.set(sel)
    if args.fly then controls.fly_to_selection(W, args.zoom) end
    local s = summary(W, sel)
    assert(s, "nothing to inspect for this selection")
    return s
  end)

  -- pick{x, y}: click at window pixels (y up) through the real picking path.
  bridge.register("pick", function(args)
    local W = assert(G.W, "world not ready")
    local sel = controls.click(W, tonumber(args.x) or 0, tonumber(args.y) or 0)
    return sel or { kind = "none" }
  end)

  -- ui{inspector | bounds | collapse=bool | debug=bool | edge={lat,lon} / edge=false}
  bridge.register("ui", function(args)
    local W = G.W
    local res = {}
    if args.collapse ~= nil then uistate.inspector_collapsed = args.collapse and true or false end
    if args.debug ~= nil then uistate.debug = args.debug and true or false end
    if args.clouds ~= nil then require("game.view.clouds").hidden = not args.clouds end
    if args.edge ~= nil then
      if args.edge == false then uistate.edge_hide("bridge")
      else
        local lat, lon = math.rad(args.edge.lat or 0), math.rad(args.edge.lon or 0)
        local r = state.R
        uistate.edge_show("bridge", math.cos(lat) * math.cos(lon) * r, math.sin(lat) * r, math.cos(lat) * math.sin(lon) * r, "fire")
      end
    end
    if args.inspector then res.inspector = W and selection.sel and summary(W, selection.sel) or false end
    if args.bounds then
      res.bounds = uistate.bounds
      res.window = { w = state.window_w, h = state.window_h }
    end
    res.collapsed = uistate.inspector_collapsed
    res.debug = uistate.debug
    return res
  end)

  bridge.register("view_stats", function() return view.stats() end)

  bridge.register("flush_view", function()
    local t0 = clock.now_ms()
    view.flush()
    return { ms = clock.now_ms() - t0 }
  end)

  -- preview_village{cell?}: view-only art preview of houses + farms (never touches the sim).
  bridge.register("preview_village", function(args)
    local W = assert(G.W, "world not ready")
    local props = require("game.view.props")
    if args.off then props.set_preview(nil); return { off = true } end
    local c = args.cell or camera.focus_cell or camera.focus_candidate(W)
    local map = { [c] = -1 }
    local g = W.grid
    local base = c * 6 - 6
    for k = 1, g.nbr_count[c] do
      local j = g.nbr[base + k]
      if W.geo.ocean[j] == 0 and k % 2 == 1 then map[j] = 1 end
    end
    props.set_preview(map)
    return { cell = c }
  end)

  -- bench_update{n, nogc}: view.update n times back-to-back (pure Lua cost).
  bridge.register("bench_update", function(args)
    local n = args.n or 30
    local t = {}
    if args.nogc then collectgarbage("stop") end
    for i = 1, n do
      local t0 = clock.now_ms()
      view.update(1 / 60)
      t[i] = clock.now_ms() - t0
    end
    if args.nogc then collectgarbage("restart") end
    table.sort(t)
    return { min = t[1], median = t[math.floor(n / 2) + 1], max = t[n], kb = collectgarbage("count") }
  end)

  -- bench{level, seed, ticks}: sim tick benchmark in the real HTML5 runtime (SIM_LEVEL, W08).
  bridge.register("bench", function(args)
    local bench = require("sim.bench")
    local save = args.default and sys.load_resource("/assets/worlds/default.tcsave") or nil
    return bench.run({ level = args.level, seed = args.seed or 42, ticks = args.ticks or 96, spinup_years = args.spinup_years,
      warm = args.warm or 24, clock = clock.now_ms, save = save })
  end)

  -- debug_cell{x, y}: sim cell under a window pixel with its display bytes (texture) and fields.
  bridge.register("debug_cell", function(args)
    local W = assert(G.W, "world not ready")
    local c, hx, hy, hz = picking.cell_at(W, args.x, args.y)
    if not c then return { hit = false } end
    local terrain = require("game.view.terrain")
    local b = {}
    for k = 1, 8 do b[k] = terrain.bytes[c * 8 - 8 + k] end
    local planet = require("game.view.planet")
    local PL = view.PL
    local v = planet.nearest(PL, hx, hy, hz)
    return { cell = c, bytes = b, ice = W.F.ice[c], biome = W.F.biome[c], land_use = W.F.land_use[c],
      crop = W.F.crop[c], vertex = v, h = PL.h[v], s = PL.s[v], va = PL.va[v], vb = PL.vb[v], vc = PL.vc[v],
      wa = PL.vwa[v], wb = PL.vwb[v], wc = PL.vwc[v],
      bytes_a = { terrain.bytes[PL.va[v] * 8 - 4], terrain.bytes[PL.vb[v] * 8 - 4], terrain.bytes[PL.vc[v] * 8 - 4] } }
  end)
  -- selftest{years=2, seed?}: W06 determinism in the real HTML5 runtime. Builds the same
  -- world twice (the pre-spun default world, or worldgen for `seed`), applies the same test
  -- action (ignite 3 forested cells), runs `years`, and returns both state hashes. Runs
  -- synchronously on private worlds; the live world's hash is reported before and after.
  bridge.register("selftest", function(args)
    local world = require("sim.world")
    local selftest = require("sim.selftest")
    local years = tonumber(args.years) or 2
    local live_before = G.W and world.state_hash(G.W) or 0
    local live_tick = G.W and G.W.tick or 0
    local data = (not args.seed) and sys.load_resource("/assets/worlds/default.tcsave") or nil
    local t0 = clock.now_ms()
    local opts = { save = data, seed = tonumber(args.seed) or 5, years = years }
    local a = selftest.run(opts)
    local b = selftest.run(opts)
    local live_after = G.W and world.state_hash(G.W) or 0
    return {
      hash1 = a.hash, hash2 = b.hash, same = a.hash == b.hash, tick1 = a.tick, tick2 = b.tick,
      events1 = a.events, events2 = b.events, ignited = a.ignited,
      source = data and "default.tcsave" or ("seed " .. tostring(args.seed or 5)),
      live_unchanged = live_before == live_after and (G.W == nil or G.W.tick == live_tick),
      live_hash = live_after, ms = clock.now_ms() - t0,
    }
  end)

  -- perf{reset=true} starts a measuring window; perf{} reports it (W08): per-tick ms
  -- (median / p95 / max), per-frame sim ms (avg / max), ticks/s, view ms (avg / p90).
  bridge.register("perf", function(args)
    if args.reset then G.perf_reset(G); return { reset = true } end
    local p = G.perf
    local n = math.min(p.n, 2048)
    local t = {}
    local tsum = 0
    for i = 1, n do t[i] = p.ticks[i]; tsum = tsum + t[i] end
    table.sort(t)
    local vf = math.min(p.frames, 2048)
    local v = {}
    for i = 1, vf do v[i] = p.view[i] or 0 end
    table.sort(v)
    local secs = (clock.now_ms() - p.t0) / 1000
    local speed = require("game.platform.speed")
    return {
      seconds = secs, ticks = p.n, frames = p.frames,
      ticks_per_s = secs > 0 and p.n / secs or 0,
      tick_median = n > 0 and t[math.floor(n * 0.5) + 1] or 0,
      tick_mean = n > 0 and tsum / n or 0,
      tick_p95 = n > 0 and t[math.min(n, math.floor(n * 0.95) + 1)] or 0,
      tick_max = n > 0 and t[n] or 0,
      sim_ms_avg = p.frames > 0 and p.sim_sum / p.frames or 0,
      sim_ms_max = p.sim_max,
      view_ms_avg = p.frames > 0 and p.view_sum / p.frames or 0,
      view_ms_p90 = vf > 0 and v[math.min(vf, math.floor(vf * 0.9) + 1)] or 0,
      view_ms_max = vf > 0 and v[vf] or 0,
      tick_estimate = speed.tick_estimate(),
      multi_frames = p.multi, multi_over = p.multi_over,
      heap_kb = collectgarbage("count"),
      chunks_rebuilt = view.stats().chunks_rebuilt,
    }
  end)

  -- gc{op="count"|"stop"|"restart"|"collect", pause=, stepmul=}: Lua heap diagnostics (W08)
  bridge.register("gc", function(args)
    local op = args.op or "count"
    if op == "stop" or op == "restart" or op == "collect" then collectgarbage(op) end
    if args.pause then collectgarbage("setpause", args.pause) end
    if args.stepmul then collectgarbage("setstepmul", args.stepmul) end
    return { kb = collectgarbage("count") }
  end)

  bridge.register("picking_info", function()
    return { last_cell = picking.last_cell }
  end)
end

return M
