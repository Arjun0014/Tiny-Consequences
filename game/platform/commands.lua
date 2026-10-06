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

local function biggest_village(W)
  local best, bp = nil, -1
  for _, v in ents.each_village(W) do
    if (v.pop or 0) > bp then best, bp = v, v.pop or 0 end
  end
  return best
end

-- someone of the biggest village with work this week (inspector shots)
local function a_worker(W)
  local v = biggest_village(W)
  if not v then return nil end
  for k = 1, #v.members do
    local p = ents.person(W, v.members[k])
    if p and not p.dead and p.task and p.task ~= "" then return v.members[k] end
  end
  return v.members[1]
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

-- v3 (D-078): advance an agent world to an absolute hour, or to the next local hour `lh` at the
-- settlement; look at (and optionally follow) a person; a person's state for tests and shots.
local function v3_person(W, id)
  local V = W.S.v3
  if id == "fisher" then
    -- (shots) someone afloat: rowing or fishing
    for _, pid in ipairs(V.order) do
      local p = V.people[pid]
      if p.alive and (p.act == "fish" or p.act == "row") then return p end
    end
    return nil
  end
  if id == nil or id == "worker" then
    -- the first adult outdoors at work
    local s = V.settlements[1]
    for _, pid in ipairs(s.members) do
      local p = V.people[pid]
      if p.alive and p.inside == 0 and p.act ~= "carried" and p.act ~= "sleep" then return p end
    end
    return V.people[s.members[1]]
  end
  return V.people[tonumber(id)]
end

function M.v3_state(W, p, t)
  local act = require("sim.agents.act")
  local people = require("sim.agents.people")
  local hh = W.S.v3.households[p.hh]
  local x, y = act.pos(p, t)
  local st, age = people.stage(p, t)
  return { id = p.id, name = p.name .. " " .. p.surname, sex = p.sex, age = math.floor(age), stage = st, role = p.role,
    household = p.hh, home = hh and hh.home or 0, partner = p.partner, mother = p.mother, father = p.father,
    kids = #p.kids, objective = p.obj, action = p.act, dest = p.dest, why = p.why, x = x, y = y, inside = p.inside,
    carry = p.carry, hunger = p.hunger, energy = p.energy, health = p.health, t0 = p.t0, t1 = p.t1 }
end

function M.register(G)
  bridge.register("v3_goto", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local world = require("sim.world")
    local speed = require("game.platform.speed")
    local t = args.hour
    if args.lh then
      local clock = require("sim.agents.clock")
      local s = W.S.v3.settlements[tonumber(args.sett) or 1]
      local now = speed.v3_t or W.hour
      local lh = clock.local_hour(now, W.grid.lon[s.cell])
      local d = (args.lh - lh) % 24
      t = now + d + 24 * (args.days or 0)
    end
    world.advance(W, t)
    speed.v3_t = t
    return { hour = t }
  end)
  -- v3_skip{kind = "dawn" | "evening" | "day" | "season"} starts a skip (D-079); {cancel = true}
  -- stops one. Returns the target hour.
  bridge.register("v3_skip", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local speed = require("game.platform.speed")
    if args.cancel then speed.v3_skip_cancel(); return { hour = speed.v3_t } end
    local to = speed.v3_skip(W, args.kind or "dawn", args.to)
    return { from = speed.v3_t, to = to }
  end)
  -- v3_hash{hours, every} (probes, S14): advance the world from where it stands and return the full
  -- state hash every `every` hours (world.state_hash) - the same lines tools/v3_hash.lua prints
  -- under LuaJIT: the browser's Lua 5.1 must live the same world.
  bridge.register("v3_hash", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local speed = require("game.platform.speed")
    local world = require("sim.world")
    local hours, every = args.hours or 48, args.every or 24
    local t0 = speed.v3_t or W.hour
    -- (S20) a named premise applied first, as tools/v3_hash.lua --premise does
    if args.premise then
      -- (the premise at a start hour both runtimes write and read exactly: the start crosses to
      -- LuaJIT as text, and a strike a few ulps apart is another world)
      local t1 = math.ceil(t0 * 1000) / 1000
      world.advance(W, t1)
      t0 = t1
      assert(require("sim.agents.premise").named(W, args.premise), "no premise " .. args.premise)
    end
    local out = { { hour = 0, hash = tostring(world.state_hash(W)) } }
    for h = every, hours, every do
      world.advance(W, t0 + h)
      out[#out + 1] = { hour = h, hash = tostring(world.state_hash(W)) }
    end
    speed.v3_t = t0 + hours
    return { start = t0, hashes = out }
  end)
  -- mathcheck{n}: sim/mathcheck.lua (which C math functions differ from LuaJIT's)
  bridge.register("mathcheck", function(args)
    return require("sim.mathcheck").run(args.n or 20000)
  end)
  -- get_path{path}: a plain value of the world's state (probes: what differs)
  bridge.register("get_path", function(args)
    local t = assert(G.W, "world not ready")
    for _, k in ipairs(args.path or {}) do
      t = t[k] ~= nil and t[k] or t[tonumber(k) or k]
      if type(t) ~= "table" then break end
    end
    if type(t) == "table" then return { table = true } end
    return { v = t, s = string.format("%.17g", tonumber(t) or 0), str = tostring(t) }
  end)
  -- hash_parts{path}: world.hash_parts (where two runtimes' states differ)
  bridge.register("hash_parts", function(args)
    local W = assert(G.W, "world not ready")
    return require("sim.world").hash_parts(W, args.path or {})
  end)
  -- v3_bench{hours} (probes): advance the world `hours` synchronously; the sim's own cost in this
  -- runtime (ms per game hour), without the view. Moves the world on (a probe, not a game action).
  -- {fresh = true}: a second copy of the shipped world, unpacked and brought to its first dawn in
  -- this page, is benched instead (the game's world is untouched): the same game hours long after
  -- page load (does a cost belong to the sim's first hours or to the browser's first seconds?)
  bridge.register("v3_bench", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local speed = require("game.platform.speed")
    local clock = require("game.platform.clock")
    local hours = args.hours or 24
    local t0 = speed.v3_t or W.hour
    if args.fresh then
      W = assert(require("sim.worlds").from_save_string(sys.load_resource("/assets/worlds/v3.tcsave")))
      t0 = args.from or speed.v3_skip_target(W, "dawn")
      require("sim.world").advance(W, t0)
    end
    local a = clock.now_ms()
    local step = args.step or 1
    local t = t0
    local per, mem = {}, {}
    if args.collect then collectgarbage("collect") end
    if args.nogc then collectgarbage("stop") end
    while t < t0 + hours do
      t = math.min(t0 + hours, t + step)
      local b0 = clock.now_ms()
      require("sim.world").advance(W, t)
      per[#per + 1] = clock.now_ms() - b0
      mem[#mem + 1] = math.floor(collectgarbage("count"))
    end
    if args.nogc then collectgarbage("restart") end
    local ms = clock.now_ms() - a
    if not args.fresh then speed.v3_t = t end
    return { hours = hours, ms = ms, ms_per_hour = ms / hours, people = W.S.v3.next_person - 1, per = per, mem = mem, t0 = t0,
      hash = args.hash and require("sim.world").state_hash(W) or nil }
  end)
  -- v3_prof{hours, every, depth, top} (probes): v3_bench under the wall-time sampler
  -- (game/platform/luaprof.lua): where the sim's time goes in this runtime, by function and line.
  bridge.register("v3_prof", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local speed = require("game.platform.speed")
    local hours = args.hours or 24
    local t0 = speed.v3_t or W.hour
    if args.fresh then
      -- (as v3_bench{fresh}: a copy of the shipped world, the game's world untouched)
      W = assert(require("sim.worlds").from_save_string(sys.load_resource("/assets/worlds/v3.tcsave")))
      t0 = args.from or speed.v3_skip_target(W, "dawn")
      require("sim.world").advance(W, t0)
    end
    local r = require("game.platform.luaprof").run(function()
      local t = t0
      while t < t0 + hours do
        t = math.min(t0 + hours, t + 1)
        require("sim.world").advance(W, t)
      end
    end, args)
    if not args.fresh then speed.v3_t = t0 + hours end
    r.hours = hours
    r.ms_per_hour = r.sampled_ms / hours
    return r
  end)
  -- v3_time{hours, fns = {"sim.agents.settle:next_tree", ...}} (probes): v3_bench with the named
  -- module functions wrapped in wall timers (calls and inclusive ms each); exact where the sampler
  -- smears. Only calls made through the module table are seen.
  bridge.register("v3_time", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local speed = require("game.platform.speed")
    local hours = args.hours or 24
    local now = clock.now_ms
    local stats, undo = {}, {}
    for _, spec in ipairs(args.fns or {}) do
      local mod, fname = spec:match("^([^:]+):(.+)$")
      local m = require(mod)
      local orig = assert(m[fname], "no function " .. spec)
      local st = { fn = spec, calls = 0, ms = 0 }
      stats[#stats + 1] = st
      undo[#undo + 1] = { m, fname, orig }
      m[fname] = function(...)
        local a = now()
        local r1, r2, r3, r4 = orig(...)
        st.ms = st.ms + (now() - a)
        st.calls = st.calls + 1
        return r1, r2, r3, r4
      end
    end
    local t0 = speed.v3_t or W.hour
    local a = now()
    local t = t0
    local ok, err = pcall(function()
      while t < t0 + hours do
        t = math.min(t0 + hours, t + 1)
        require("sim.world").advance(W, t)
      end
    end)
    local ms = now() - a
    for _, u in ipairs(undo) do u[1][u[2]] = u[3] end
    assert(ok, err)
    speed.v3_t = t
    for _, st in ipairs(stats) do st.ms_per_hour = st.ms / hours; st.us_per_call = 1000 * st.ms / math.max(1, st.calls) end
    return { hours = hours, ms = ms, ms_per_hour = ms / hours, fns = stats }
  end)
  -- microbench{n}: what common operations cost in this runtime (ns each): a VM loop, table reads,
  -- C calls (bit, math.floor, math.sqrt), string.format, a full rng.hash and rng.fbase.
  bridge.register("microbench", function(args)
    local n = args.n or 200000
    local bitm = bit or require("bit")
    local bxor, rshift, floor, sqrt = bitm.bxor, bitm.rshift, math.floor, math.sqrt
    local rng = require("sim.rng")
    local now = clock.now_ms
    local t = { 1, 2, 3, 4 }
    local out = {}
    local function run(name, fn)
      local a = now()
      local r = fn()
      out[name] = (now() - a) * 1e6 / n
      return r
    end
    local base = run("loop", function() local x = 0; for i = 1, n do x = x + i end; return x end)
    run("table_read", function() local x = 0; for i = 1, n do x = x + t[(i % 4) + 1] end; return x end)
    run("bxor", function() local x = 0; for i = 1, n do x = bxor(x, i) end; return x end)
    run("rshift", function() local x = 0; for i = 1, n do x = x + rshift(i, 3) end; return x end)
    run("floor", function() local x = 0; for i = 1, n do x = x + floor(i * 0.37) end; return x end)
    run("mod_floor", function() local x = 0; for i = 1, n do local v = i * 0.37; x = x + (v - v % 1) end; return x end)
    run("sqrt", function() local x = 0; for i = 1, n do x = x + sqrt(i) end; return x end)
    run("lua_call", function() local f = function(a) return a + 1 end; local x = 0; for i = 1, n do x = f(x) end; return x end)
    run("format", function() local x; for i = 1, n / 10 do x = string.format("%d of %s", i, "x") end; return x end)
    out.format = out.format * 10
    run("hash", function() local x = 0; for i = 1, n / 10 do x = x + rng.hash(1, 2, i, 3, 4) end; return x end)
    out.hash = out.hash * 10
    run("fbase", function() local x = 0; for i = 1, n / 10 do x = x + rng.fbase(12345, i) end; return x end)
    out.fbase = out.fbase * 10
    out._ = base
    return out
  end)
  bridge.register("v3_person", function(args)
    local W = assert(G.W, "world not ready")
    local speed = require("game.platform.speed")
    local p = assert(v3_person(W, args.pid), "no such person")
    local t = speed.v3_t or W.hour
    if args.look then
      local av = require("game.view.agents_view")
      local V = av.SETS[p.at or p.sett] or av.SETS[1]
      local x, y = require("sim.agents.act").pos(p, t)
      local ux, uy, uz = require("sim.agents.map").unit(V.s, x, y)
      -- (S17: on the road, where the road has them on the planet)
      local rx, ry, rz = av.traveller_unit(W, p, t)
      if rx then ux, uy, uz = rx, ry, rz end
      local lat = math.asin(uy)
      local lon = math.atan2(uz, ux)
      camera.set(math.deg(lat) * math.pi / 180, lon, args.zoom or 1.6, true)
    end
    if args.follow ~= nil then
      require("game.view.agents_view").follow = args.follow and p.id or nil
    end
    return M.v3_state(W, p, t)
  end)
  -- v3_look{sett, what = "centre" | "dock" | "ford" | "river", x, y, zoom}: the camera on a place of
  -- settlement `sett` (shots of the geography, D-085). Returns the local point looked at.
  bridge.register("v3_look", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local mapm = require("sim.agents.map")
    local s = assert(W.S.v3.settlements[args.sett or 1], "no such settlement")
    local x, y = args.x or 0, args.y or 0
    local info
    if args.what == "square" then
      -- (S17) the village's square (a new place's: where its founders chose to settle)
      x, y = s.sqx or 0, s.sqy or 0
    elseif args.what == "dock" then
      assert(s.dock, "no dock")
      x, y = s.dock.mx, s.dock.my
    elseif args.what == "house" then
      -- (S22) the first house standing with a household in it
      local found
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if not found and b.kind == "house" and b.state == "standing" and b.hh ~= 0 then found = b end
      end
      assert(found, "no house")
      x, y = found.cx, found.cy
    elseif args.what == "tree_near_house" then
      -- (S23) the standing tree nearest a standing house (not touching it): where a bolt may set a village alight
      local T = s.trees
      local bd = 1e9
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "house" and b.state == "standing" then
          for k = 1, T.n do
            if T.st[k] == 1 and (T.size[k] or 0) > 0.7 then
              local d = (T.x[k] - b.cx) ^ 2 + (T.y[k] - b.cy) ^ 2
              if d > 0.0016 and d < bd then bd, x, y = d, T.x[k], T.y[k] end
            end
          end
        end
      end
    elseif args.what == "bridge" then
      local found
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "bridge" then found = b end
      end
      assert(found, "no bridge")
      x, y = found.cx, found.cy
      info = { state = found.state, tiles = #found.tiles }
    elseif args.what == "wild" or args.what == "wolves" then
      -- the middle of the biggest wild band on the map now (or of the wolves hunting it)
      local wild = require("sim.agents.wild")
      local t = require("game.platform.speed").v3_t or W.hour
      local by, cnt = {}, {}
      wild.each(s, function(w)
        if (args.what == "wolves") == w.pred then
          local wx, wy = wild.pos(w, t)
          local b = by[w.gid] or { 0, 0 }
          by[w.gid] = b
          b[1], b[2] = b[1] + wx, b[2] + wy
          cnt[w.gid] = (cnt[w.gid] or 0) + 1
        end
      end)
      local best, bn = nil, 0
      for g, n2 in pairs(cnt) do if n2 > bn or (n2 == bn and g < best) then best, bn = g, n2 end end
      assert(best, "no wild animals of that kind on the map")
      x, y = by[best][1] / bn, by[best][2] / bn
      -- (a grazing band: its day's ground, where most of it is)
      local gr = s.wildlife.ground and s.wildlife.ground[best]
      if gr and args.what == "wild" then x, y = gr[1], gr[2] end
      info = { n = bn, gid = best }
    elseif args.what == "flock" or args.what == "herd" then
      -- the middle of a herd's living animals now (sheep, or `species` of s.herds)
      local H = s.herds and s.herds[args.what == "flock" and "flock" or (args.herd or "cattle")]
      assert(H, "no such herd")
      local flock = require("sim.agents.stock")
      local t = require("game.platform.speed").v3_t or W.hour
      local n = 0
      x, y = 0, 0
      for _, a in ipairs(H.animals) do
        if a.alive then
          local ax, ay = flock.pos(a, t)
          x, y, n = x + ax, y + ay, n + 1
        end
      end
      assert(n > 0, "no animals")
      x, y = x / n, y / n
      local acts, spread = {}, 0
      for _, a in ipairs(H.animals) do
        if a.alive then
          acts[a.act] = (acts[a.act] or 0) + 1
          local ax, ay = flock.pos(a, t)
          spread = math.max(spread, math.sqrt((ax - x) ^ 2 + (ay - y) ^ 2))
        end
      end
      info = { acts = acts, spread = spread, n = n }
    elseif args.what == "birds" then
      -- (S14) the settlement's first bird flock where it is now
      local birds = require("sim.agents.birds")
      local t = require("game.platform.speed").v3_t or W.hour
      local f = s.birds and s.birds.flocks and s.birds.flocks[1]
      assert(f, "no flock")
      x, y = birds.pos(f, t)
      info = { act = f.act, obj = f.obj, why = f.why, n = f.n, hunger = f.hunger }
    elseif args.what == "ford" or args.what == "river" then
      -- the wet tile nearest the square (a wadeable one for "ford")
      local best, bd = 0, 1e9
      for i = 1, s.N * s.N do
        local r = s.riv[i]
        if r > 0 and (args.what == "river" or r <= 101) then
          local tx, ty = mapm.tile_xy(s, i)
          local d = tx * tx + ty * ty
          if d < bd then best, bd = i, d end
        end
      end
      assert(best > 0, "no " .. args.what)
      x, y = mapm.tile_xy(s, best)
    elseif args.what == "volcano" then
      -- (S24) the vent of the volcano nearest the village (where the world keeps them; none made by looking)
      local g = W.grid
      local VS = W.S.v3.volc
      local best, bd = nil, -2
      for _, v in ipairs(VS and VS.list or {}) do
        local d = g.X[v.cell] * g.X[s.cell] + g.Y[v.cell] * g.Y[s.cell] + g.Z[v.cell] * g.Z[s.cell]
        if d > bd then best, bd = v, d end
      end
      assert(best, "no volcano")
      x, y = mapm.local_of(s, g.X[best.cell], g.Y[best.cell], g.Z[best.cell])
      assert(x, "the volcano is behind the map")
      info = { cell = best.cell, name = best.name, pressure = best.pressure, thr = best.thr }
    elseif args.what == "sea" then
      -- (S24) the middle of the sea cell by the village holding the most fish (where its fish are drawn)
      local g = W.grid
      local best, bf = nil, -1
      for _, c in ipairs(require("sim.society.sites").rings(W, s.cell, 2)) do
        if W.geo.ocean[c] == 1 and (W.F.fish[c] or 0) > bf then best, bf = c, W.F.fish[c] or 0 end
      end
      assert(best, "no sea near")
      x, y = mapm.local_of(s, g.X[best], g.Y[best], g.Z[best])
      assert(x, "the sea is behind the map")
      info = { cell = best, fish = bf }
    elseif args.what == "flood" then
      -- (S21) the flood water nearest the village's square
      local fd = assert(mapm.FLOOD[s], "no flood water")
      local best, bd = 0, 1e18
      for i = 1, s.N * s.N do
        if fd[i] > 0 then
          local tx, ty = mapm.tile_xy(s, i)
          local d = (tx - (s.sqx or 0)) ^ 2 + (ty - (s.sqy or 0)) ^ 2
          if d < bd then best, bd = i, d end
        end
      end
      x, y = mapm.tile_xy(s, best)
    elseif args.what and args.what ~= "centre" then
      -- (S21) the first standing building of that kind ("byre", "granary", ...)
      local found
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == args.what and b.state == "standing" then found = b; break end
      end
      assert(found, "no " .. args.what)
      x, y = found.cx, found.cy
      info = { wet = found.wet }
    end
    local ux, uy, uz = mapm.unit(s, x, y)
    camera.set(math.asin(uy), math.atan2(uz, ux), args.zoom or 1.5, true)
    return { x = x, y = y, name = s.name, info = info, cell = s.cell }
  end)
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

  -- run_hours{n | to_local = h}: step the world synchronously by n hours, or until the local
  -- solar hour at the view focus reaches `to_local` (0..24). The hour clock (D-050).
  bridge.register("run_hours", function(args)
    assert(G.W, "world not ready")
    local world = require("sim.world")
    local daylight = require("sim.society.daylight")
    local W = G.W
    local n = math.floor(tonumber(args.n) or 0)
    if args.to_local then
      local _, lon = camera.target_latlon()
      local want = tonumber(args.to_local) % 24
      local here = daylight.local_hour(W.hour, lon)
      n = math.floor((want - here) % 24 + 0.5)
    end
    local t0 = clock.now_ms()
    for _ = 1, n do world.step(W) end
    return { hour = W.hour, ran = n, ms = clock.now_ms() - t0 }
  end)

  -- powers (Stage 2.1): cast through the same path as the power bar. args: power, cell (or
  -- target = "tanker" | "village" | "focus"), mode, species, ship, force (ignore cooldown)
  -- resolve a power target: args.cell, or target = "tanker" | "village" | "farm" | "sea" |
  -- "land" (near the first village) | nil (the view focus). Returns cell, ship id.
  local function power_target(W, args)
    local cell, ship = args.cell, args.ship
    if not cell and args.target == "tanker" then
      local t = require("sim.tutorial").pick(W)
      if t then cell, ship = t.cell, t.id end
    elseif not cell and args.target == "village" then
      local v = first_village(W)
      cell = v and v.cell
    elseif not cell and args.target == "farm" then
      local v = first_village(W)
      cell = v and v.farms and v.farms[1]
    elseif not cell and (args.target == "sea" or args.target == "land") then
      local v = first_village(W)
      local g = W.grid
      local want = args.target == "sea" and 1 or 0
      local best, bd = nil, -2
      for c = 1, W.n do
        if W.geo.ocean[c] == want and (want == 1 or c ~= v.cell) and W.F.land_use[c] == 0 then
          local d = g.X[c] * g.X[v.cell] + g.Y[c] * g.Y[v.cell] + g.Z[c] * g.Z[v.cell]
          if d > bd then best, bd = c, d end
        end
      end
      cell = best
    elseif not cell then
      cell = camera.focus_cell or require("game.view.picking").cell_at(W, (state.window_w or 1280) / 2, (state.window_h or 720) / 2)
    end
    return cell, ship
  end

  local function screen_of(W, cell)
    local g = W.grid
    local r = view.surface(g.X[cell], g.Y[cell], g.Z[cell])
    return camera.project(g.X[cell] * r, g.Y[cell] * r, g.Z[cell] * r)
  end

  -- point the camera at a power target (tests, screenshots)
  bridge.register("look", function(args)
    local W = G.W
    local cell = power_target(W, args)
    if not cell then error("no target", 0) end
    local g = W.grid
    camera.fly_to_dir(g.X[cell], g.Y[cell], g.Z[cell], args.zoom or 0.62, true)
    camera.update(0)
    view.flush()
    local sx, sy = screen_of(W, cell)
    return { cell = cell, sx = sx, sy = sy }
  end)

  -- powers (Stage 2.1): cast through the same path as the power bar. args: power, cell or
  -- target (see power_target), mode, species, ship, force (ignore cooldown; default on)
  bridge.register("power", function(args)
    local powers = require("game.ui.powers")
    local W = G.W
    local cell, ship = power_target(W, args)
    local opts = { mode = args.mode, species = args.species, ship = ship, force = args.force ~= false, sid = args.sid, x = args.x, y = args.y }
    local p, why = powers.cast(W, args.power, cell, opts)
    if not p then error(why or "power refused", 0) end
    local sx, sy = screen_of(W, cell)
    return { cell = cell, power = args.power, tick = W.tick, sx = sx, sy = sy }
  end)
  -- a shipment overseas (sim/testhooks.lua voyage), for screenshots and tests: returns the party
  -- (S17) v3_found_info{}: the places founded while the world ran and the parties setting out or
  -- on their way (shots, probes)
  bridge.register("v3_found_info", function(args)
    local W = assert(G.W, "world not ready")
    local V = assert(W.S.v3, "not a v3 world")
    local out = { places = {}, parties = {}, hour = require("game.platform.speed").v3_t or W.hour }
    for _, o in ipairs(V.settlements) do
      if o.founding then
        local bl = {}
        local first
        for _, id in ipairs(o.blist) do
          local b = o.buildings[id]
          bl[#bl + 1] = b.label .. ":" .. b.state
          if not first and b.kind ~= "outcrop" and b.kind ~= "bog" then first = { x = b.cx, y = b.cy, label = b.label } end
        end
        local av = package.loaded["game.view.agents_view"]
        local VS = av and av.SETS and av.SETS[o.id]
        out.places[#out.places + 1] = { id = o.id, name = o.name, members = #o.members, buildings = bl, settled = o.founding.settled,
          first = first, sq = { o.sqx, o.sqy }, view = VS and { in_view = VS.in_view, trees = o.trees.n } or false, n_view = av and #av.SETS or 0 }
      end
    end
    for hid, hh in pairs(V.households) do
      if hh.found and #hh.members > 0 then
        local m = {}
        for _, id in ipairs(hh.members) do m[#m + 1] = id end
        out.parties[#out.parties + 1] = { hh = hid, members = m, site = hh.found.site, depart = hh.found.depart, arrived = hh.found.arrived, why = hh.found.why }
      end
    end
    table.sort(out.parties, function(a, b) return a.hh < b.hh end)
    return out
  end)
  -- (S17) v3_premise{kind = "crowd" | "fail_harvest" | "empty_stores", sett = id | name, waited = days}:
  -- a premise created on purpose (sim/agents/premise.lua) so what follows can be watched
  bridge.register("v3_premise", function(args)
    local W = assert(G.W, "world not ready")
    local V = assert(W.S.v3, "not a v3 world")
    local s
    for _, o in ipairs(V.settlements) do if o.id == tonumber(args.sett) or o.name == args.sett then s = o end end
    assert(s, "no such settlement")
    local pr = require("sim.agents.premise")
    local n = 0
    if args.kind == "crowd" then n = pr.crowd(W, s, tonumber(args.waited) or require("sim.config").V3.found_lot_days)
    elseif args.kind == "fail_harvest" then pr.fail_harvest(W, s)
    elseif args.kind == "empty_stores" then pr.empty_stores(W, s)
    elseif args.kind == "named" then
      -- (S20) a named premise (premise.named: cold_snap, hard_winter, quake, eruption, wildfire, ...)
      n = pr.named(W, args.name) and 1 or 0
    elseif args.kind == "snow" then
      -- (S20, R6.5d) the snow on the map now (shots_snow.mjs)
      local herds_out = 0
      for _, k in ipairs({ "flock", "cattle", "swine" }) do if s.herds and s.herds[k] and s.herds[k].out then herds_out = herds_out + 1 end end
      return { sett = s.id, name = s.name, snow = s.snow or 0, snowing = s.snowing and true or false, temp = s.temp_now or 0,
        herds_out = herds_out, hay = s.hay or 0, cover = require("game.view.cellcolor").snow_over[s.cell] or 0 }
    elseif args.kind == "burning" then
      -- (S20) what burns there now (shots_fire.mjs)
      local out = {}
      for _, f in ipairs(s.burn or {}) do out[#out + 1] = string.format("%s%d heat %.2f fuel %.2f", f.kind, f.ref, f.heat, f.fuel) end
      local nf = #(require("game.view.agents_view").local_fires or {})
      return { sett = s.id, name = s.name, burning = table.concat(out, "; "), drawn = nf, acc = string.format("fires %d out %d burnt %d doused %d", s.acc.fires or 0, s.acc.fires_out or 0, s.acc.burnt or 0, s.acc.doused or 0) }
    elseif args.kind == "pasture_fire" then
      -- (S22) a dry morning and a stand of trees alight beside a herd at pasture (args.herd: flock | cattle |
      -- swine; args.off: from its middle; args.n: trees) - where it is, to look at (shots_beasts_fire.mjs)
      if args.dry then W.F.soil_water[s.cell] = tonumber(args.dry) end
      local blaze = require("sim.agents.blaze")
      local stock = require("sim.agents.stock")
      local now = require("game.platform.speed").v3_t or W.hour
      local H = assert(s.herds and s.herds[args.herd or "flock"], "no such herd")
      local cx, cy, k = 0, 0, 0
      for _, a in ipairs(H.animals) do
        if a.alive and a.act ~= "pen" then local x, y = stock.pos(a, now); cx, cy, k = cx + x, cy + y, k + 1 end
      end
      assert(k > 0, "the herd is in its pen")
      cx, cy = cx / k, cy / k
      local ang = (s.key % 97) / 97 * 6.2832
      local off = tonumber(args.off) or 0.15
      local px, py = cx + math.cos(ang) * off, cy + math.sin(ang) * off
      local cand = {}
      blaze.trees_near(s, px, py, 0.6, function(t, d2) cand[#cand + 1] = { t, d2 } end)
      table.sort(cand, function(a, b) if a[2] ~= b[2] then return a[2] < b[2] end return a[1] < b[1] end)
      for q = 1, math.min(tonumber(args.n) or 6, #cand) do if blaze.ignite(W, s, "tree", cand[q][1], now, nil) then n = n + 1 end end
      return { sett = s.id, name = s.name, added = n, x = cx, y = cy, fx = px, fy = py, beasts = k }
    elseif args.kind == "fire" or args.kind == "strike" then
      -- (S20, R6.5b) a roof set alight / lightning at a house (args.dry: the ground's water)
      if args.dry then W.F.soil_water[s.cell] = tonumber(args.dry) end
      local blaze = require("sim.agents.blaze")
      local speed = require("game.platform.speed")
      local now = speed.v3_t or W.hour
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        if b.kind == "house" and b.state == "standing" and b.hh ~= 0 then
          if args.kind == "fire" then blaze.ignite(W, s, "bld", b.id, now, nil) else blaze.strike(W, s, b.cx, b.cy, now, nil) end
          n = b.id
          break
        end
      end
    else error("v3_premise: kind crowd | fail_harvest | empty_stores | fire | strike | named | snow | burning") end
    return { sett = s.id, name = s.name, added = n, people = #s.members }
  end)
  bridge.register("voyage", function(args)
    local W = G.W
    local world = require("sim.world")
    local hooks = require("sim.testhooks")
    hooks.last_voyage = nil
    if args.probe then
      -- who would sail where (no command: nothing changes)
      hooks.voyage(W, { from = args.from, to = args.to, probe = true })
      local pr = hooks.last_probe
      if not pr then error("no voyage possible", 0) end
      local c = W.ent.villages[pr.from].cell
      return { from = pr.from, to = pr.to, lat = math.deg(W.grid.lat[c]), lon = math.deg(W.grid.lon[c]) }
    end
    world.test_action(W, "voyage", { from = args.from, to = args.to, grain = args.grain })
    world.step(W)
    local id = hooks.last_voyage
    local party = id and W.ent.parties and W.ent.parties[id]
    if not party then error("no voyage possible", 0) end
    local g = W.grid
    local mid = party.path[math.floor((party.si + party.sj) / 2)]
    return { party = id, from = party.from, to = party.to, depart = party.depart, arrive = party.arrive, hour = W.hour,
      si = party.si, sj = party.sj, cell = mid, lat = math.deg(g.lat[mid]), lon = math.deg(g.lon[mid]),
      boat = party.boats and party.boats[1] }
  end)
  -- a cattle raid (sim/testhooks.lua cattle_raid), for screenshots: returns the party and the
  -- raiders' village
  bridge.register("cattle_raid", function(args)
    local W = G.W
    local world = require("sim.world")
    local hooks = require("sim.testhooks")
    hooks.last_raid = nil
    if args.probe then
      hooks.cattle_raid(W, { from = args.from, to = args.to, probe = true })
      local pr = hooks.last_probe
      if not pr then error("no raid possible", 0) end
      local c = W.ent.villages[pr.from].cell
      return { from = pr.from, to = pr.to, lat = math.deg(W.grid.lat[c]), lon = math.deg(W.grid.lon[c]) }
    end
    world.test_action(W, "cattle_raid", { from = args.from, to = args.to, n = args.n })
    world.step(W)
    local party = hooks.last_raid and W.ent.parties[hooks.last_raid]
    if not party then error("no raid possible", 0) end
    local g = W.grid
    local c = W.ent.villages[party.cargo.target].cell
    return { party = party.id, from = party.from, to = party.to, depart = party.depart, arrive = party.arrive,
      hour = W.hour, lat = math.deg(g.lat[c]), lon = math.deg(g.lon[c]) }
  end)
  -- a submission (sim/testhooks.lua submit): a polity for pictures; returns both villages
  bridge.register("submit", function(args)
    local W = G.W
    local world = require("sim.world")
    world.test_action(W, "submit", { village = args.village, lord = args.lord })
    world.step(W)
    local out = {}
    for _, v in ipairs(W.ent.villages) do
      if v.alive and v.lord then
        local c = v.cell
        out[#out + 1] = { village = v.id, lord = v.lord, lat = math.deg(W.grid.lat[c]), lon = math.deg(W.grid.lon[c]) }
      end
    end
    return { tributaries = out }
  end)
  -- where a party is now (any kind; follows the raiders home: the newest party from their target)
  bridge.register("party_pos", function(args)
    local W = G.W
    local g = W.grid
    local party = W.ent.parties and W.ent.parties[args.party or 0]
    if not party and args.home then
      for id, q in pairs(W.ent.parties or {}) do
        if q.to == args.home and (q.kind == "raid_home" or q.kind == "returning") and (not party or id > party.id) then party = q end
      end
    end
    if not party then error("no such party", 0) end
    local i = require("sim.society.schedule").party_pos(W, party, W.hour)
    local c = party.path[i]
    return { party = party.id, kind = party.kind, hour = W.hour, cell = c, lat = math.deg(g.lat[c]), lon = math.deg(g.lon[c]),
      arrive = party.arrive }
  end)
  -- where a voyage is now: its boat (at sea, beached, sailing home), else the party on land
  bridge.register("voyage_pos", function(args)
    local W = G.W
    local g = W.grid
    local sea = require("sim.society.sea")
    local b = args.boat and W.ent.boats[args.boat]
    local cell = b and sea.cell_of(W, b)
    local party = W.ent.parties and W.ent.parties[args.party or 0]
    if not cell and party then
      local i = require("sim.society.schedule").party_pos(W, party, W.hour)
      cell = party.path[i]
    end
    if not cell and b then cell = b.home end
    return { hour = W.hour, cell = cell, lat = math.deg(g.lat[cell]), lon = math.deg(g.lon[cell]),
      boat_state = b and b.state, party_alive = party ~= nil }
  end)
  bridge.register("arm", function(args)
    local powers = require("game.ui.powers")
    powers.armed = args.index
    if args.option then powers.mode[powers.DEFS[args.index].id] = args.option end
    return { armed = powers.armed }
  end)
  -- renderer toggles (debugging / quality): shadow, post, near (near-LOD props), movers
  bridge.register("gfx", function(args)
    if args.shadow ~= nil then state.shadow_on = args.shadow and true or false end
    if args.post ~= nil then state.post_on = args.post and true or false end
    if args.near ~= nil then require("game.view.props").NEAR_ZOOM = args.near and 0.45 or 99 end
    if args.shadow_size then state.SHADOW_SIZE = args.shadow_size end
    if args.movers ~= nil then require("game.view.puppets").MIN_ZOOM = args.movers and 0.55 or 99 end
    -- prop density (profiling the CPU rasteriser): every chunk rebuilds at the new slot counts
    if args.slots or args.slots_far then
      local propgen = require("game.view.propgen")
      propgen.SLOTS = args.slots or propgen.SLOTS
      propgen.SLOTS_FAR = args.slots_far or propgen.SLOTS_FAR
      local props = require("game.view.props")
      for _, c in ipairs(props.chunks) do c.dirty = true end
      props.flush(G.W, camera.zoom)
    end
    if args.props ~= nil then
      for _, c in ipairs(require("game.view.props").chunks) do
        if c.url and not c.empty then msg.post(c.url, args.props and "enable" or "disable") end
      end
    end
    return { shadow = state.shadow_on, post = state.post_on }
  end)
  -- render_off{on} (probes): draw nothing (a cleared frame), so headless Chromium's CPU rasteriser
  -- (SwiftShader) stops competing with the sim for the processor while it is measured
  bridge.register("render_off", function(args)
    require("game.view.state").render_off = args.on ~= false
    return { render_off = require("game.view.state").render_off }
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

  -- select{cell=c | village=id|"first" | person=id|"first" | flock={sett, k} | planet=true | ship=i | none=true}
  bridge.register("select", function(args)
    local W = assert(G.W, "world not ready")
    local sel
    if args.none then selection.clear(); return { selected = false } end
    if args.cell then
      local c = tonumber(args.cell) or camera.focus_candidate(W)
      assert(c >= 1 and c <= W.n, "cell out of range")
      sel = { kind = "cell", cell = c }
    elseif args.village then
      local v = (args.village == "first" and first_village(W)) or (args.village == "biggest" and biggest_village(W))
        or ents.village(W, args.village)
      assert(v, "no such village (villages: " .. #ents.villages(W) .. ")")
      sel = { kind = "village", id = v.id, cell = v.cell }
    elseif args.person and W.v3 then
      local pid = tonumber(args.person)
      assert(pid and W.S.v3.people[pid], "no such person")
      sel = { kind = "person", id = pid, v3 = true }
    elseif args.person then
      local pid = (args.person == "first" and first_person(W)) or (args.person == "worker" and a_worker(W)) or args.person
      assert(pid and ents.person(W, pid), "no such person")
      sel = { kind = "person", id = pid }
    elseif args.flock and W.v3 then
      -- (S14) flock = { sett, k }
      sel = { kind = "flock", sid = tonumber(args.flock[1]) or 1, k = tonumber(args.flock[2]) or 1 }
    elseif args.beast and W.v3 then
      -- (S16) beast = { sett, "flock" | "cattle" | "swine" | "wild" | "dog", k }
      sel = { kind = "beast", sid = tonumber(args.beast[1]) or 1, grp = args.beast[2] or "flock", k = tonumber(args.beast[3]) or 1 }
    elseif args.sett and W.v3 then
      -- (S17) sett = settlement id (or "last": the newest)
      local id = args.sett == "last" and #W.S.v3.settlements or tonumber(args.sett)
      assert(id and W.S.v3.settlements[id], "no such settlement")
      sel = { kind = "settlement", sid = id }
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
  -- (S21, R7) news{n, go = k}: the world news now (sim/news.lua: titles, lines, levels, roots and their
  -- causes), and with go = k the k-th card visited as a click on it would (game/ui/newsfeed.lua M.go).
  bridge.register("news", function(args)
    local W = assert(G.W, "world not ready")
    assert(W.v3, "not a v3 world")
    local news = require("sim.news")
    local t = require("game.platform.speed").v3_t or W.hour
    news.update(W, t)
    local cards = news.cards(W, tonumber(args.n) or 5, t)
    local out = {}
    for i, st in ipairs(cards) do
      local ch = news.chain(W, st)
      local links = {}
      for k, l in ipairs(ch.links) do links[k] = l.text .. " (" .. l.who .. ")" end
      out[i] = { title = st.title, line = st.line, lines = st.lines, level = st.level, kind = st.kind, active = st.active,
        root = ch.root, why = links, sid = st.sid }
    end
    local went
    if args.go then
      local st = cards[tonumber(args.go) or 1]
      went = st and require("game.ui.newsfeed").go(W, st) or false
    end
    return { cards = out, went = went }
  end)

  -- (S21) audio{on = bool}: the world audio's state (beds playing and their gains, one-shots and cues so far).
  bridge.register("audio", function(args)
    local audio = require("game.audio.audio")
    if args.on ~= nil then audio.ON = args.on and true or false end
    local beds = {}
    for k, v in pairs(audio.stats.beds) do beds[k] = v end
    -- (S23) one-shot energy by category (sum of gain^2 since the last reset: the mix's balance)
    local energy = {}
    for k, v in pairs(audio.stats.energy or {}) do energy[k] = math.floor(v * 1000 + 0.5) / 1000 end
    if args.reset then audio.stats.energy = {}; audio.stats.shots = 0 end
    return { ready = audio.ready == true, on = audio.ON, beds = beds, shots = audio.stats.shots, cues = audio.stats.cues, last = audio.stats.last,
      energy = energy, duck = audio.duck }
  end)

  -- (S23) actions{open, ev}: "Your actions" (game/ui/actions.lua) opened (on intervention ev, or the newest);
  -- returns the interventions and the open one's chain as the panel shows them (sim/chain.lua)
  bridge.register("actions", function(args)
    local W = assert(G.W, "world not ready")
    local chain = require("sim.chain")
    chain.update(W, require("game.platform.speed").v3_t or W.hour)
    local list = chain.list(W)
    local ev = args.ev == "latest" and list[1] and list[1].ev or tonumber(args.ev)
    if args.open ~= nil then require("game.ui.actions").request = { open = args.open and true or false, ev = ev, news = args.news } end
    local out = { list = {} }
    for _, I in ipairs(list) do
      out.list[#out.list + 1] = { ev = I.ev, title = I.title, power = I.power, n_total = I.n_total, n_direct = I.n_direct, active = I.active }
    end
    local I = ev and chain.get(W, ev)
    if I then
      out.rows = {}
      for _, row in ipairs(chain.rows(W, I, 60)) do
        out.rows[#out.rows + 1] = { depth = row.depth, kind = row.node.kind, title = row.node.title, line = row.node.line, active = row.node.active }
      end
    end
    return out
  end)

  -- (S23) clouds{}: the cloud cover map at each village (clouds v5)
  bridge.register("clouds", function(args)
    local W = assert(G.W, "world not ready")
    return require("game.view.clouds").probe(W)
  end)

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
      res.buttons = uistate.buttons
      res.window = { w = state.window_w, h = state.window_h }
      res.k = require("game.ui.layout").k
    end
    -- (S26) the layout audit: ask (audit_request), then read a frame later (audit) - texts leaving their panel or the window
    if args.audit_request then uistate.audit_req = true; uistate.audit = nil end
    if args.audit then res.audit = uistate.audit or false end
    if args.idle ~= nil then res.idle = uistate.idle or false end
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

  -- bench_view{chunks, lod}: the Lua cost of baking prop chunks (plan + write into a real buffer)
  -- and of rebuilding cloud sectors, in this runtime.
  bridge.register("bench_view", function(args)
    local W = assert(G.W, "world not ready")
    local props = require("game.view.props")
    local propgen = require("game.view.propgen")
    local clouds = require("game.view.clouds")
    local lod = args.lod or 1
    local n = args.chunks or 4
    local out = { lod = lod }
    props.flush(W, camera.zoom)      -- no rebuild job may be holding propgen's instance list
    local tp, tw, verts, inst = 0, 0, 0, 0
    local ctx = props.ctx
    ctx.lod = lod
    for k = 1, math.min(n, #props.chunks) do
      local ch = props.chunks[k * 7 % #props.chunks + 1].ch
      local a = clock.now_ms()
      local ni, nv = propgen.plan_chunk(ctx, ch)
      local b = clock.now_ms()
      if nv > 0 then
        local buf = buffer.create(nv, propgen.chunk_decl())
        local S = { inst = buffer.get_stream(buf, propgen.H_INST), inst2 = buffer.get_stream(buf, propgen.H_INST2),
          inst3 = buffer.get_stream(buf, propgen.H_INST3) }
        propgen.write(buf, S, 0, 1, ni)
      end
      tw = tw + clock.now_ms() - b
      tp = tp + b - a
      verts, inst = verts + nv, inst + ni
    end
    out.plan_ms, out.write_ms, out.verts, out.instances = tp, tw, verts, inst
    out.us_per_vertex = tw * 1000 / math.max(1, verts)
    -- (S23, clouds v5) a whole cover map made at once from the weather of now
    local r0 = clouds.stats.rebuilt
    local t0 = clock.now_ms()
    clouds.flush(W)
    out.cloud_maps = clouds.stats.rebuilt - r0
    out.cloud_ms_per_map = clock.now_ms() - t0
    out.cloud_cover = clouds.stats.cover
    props.flush(W, camera.zoom)
    return out
  end)

  -- biome_spots: a representative land cell per landscape (the densest temperate forest, boreal,
  -- jungle, savanna, desert, tundra, marsh, mountains, coast) for review shots.
  bridge.register("biome_spots", function()
    local W = assert(G.W, "world not ready")
    local F, g = W.F, W.grid
    local T = W.temp_static
    local best = {}
    local function consider(name, c, score)
      local b = best[name]
      if not b or score > b.score then best[name] = { cell = c, score = score, lat = math.deg(g.lat[c]), lon = math.deg(g.lon[c]) } end
    end
    for c = 1, W.n do
      if W.geo.ocean[c] == 0 and F.land_use[c] == 0 then
        local tm = T.base[c] + T.lapse[c] + (W.G.g_temp or 0)
        local fo, gr, bi, sw = F.forest[c], F.grass[c], F.biome[c], F.soil_water[c]
        if bi == 11 then consider("jungle", c, fo) end
        if fo > 0.5 and tm >= 9 and tm < 19 then consider("temperate", c, fo - math.abs(tm - 13) * 0.02) end
        if fo > 0.4 and tm >= 3 and tm < 9 then consider("mixed", c, fo) end
        if fo > 0.3 and tm < 3 and F.ice[c] < 0.3 then consider("boreal", c, fo) end
        if tm > 18 and sw < 0.4 and gr > 0.2 then consider("savanna", c, gr + fo * 0.5) end
        if bi == 9 then consider("desert", c, -sw) end
        if bi == 10 then consider("tundra", c, gr) end
        if bi == 8 then consider("marsh", c, sw) end
        if bi == 13 then consider("grassland", c, gr) end
        consider("mountain", c, W.static.elev[c] + fo * 0.5)
      end
    end
    return best
  end)

  -- wild_spots: the biggest herd of each kind and the biggest pack of each kind (W4, D-070), for
  -- review shots: { name = { cell, lat, lon, n, id } }.
  bridge.register("wild_spots", function()
    local W = assert(G.W, "world not ready")
    local A = W.S and W.S.wild
    local wcfg = require("sim.config").WILD
    local g = W.grid
    local best = {}
    if A then
      for i = 1, A.nh do
        local nm = wcfg.grazers[A.h_sp[i]].name
        local b = best[nm]
        if not b or A.h_n[i] > b.n then
          local c = A.h_cell[i]
          best[nm] = { cell = c, lat = math.deg(g.lat[c]), lon = math.deg(g.lon[c]), n = A.h_n[i], id = A.h_id[i] }
        end
      end
      for i = 1, A.np do
        local nm = wcfg.predators[A.p_sp[i]].name
        local b = best[nm]
        if not b or A.p_n[i] > b.n then
          local c = A.p_cell[i]
          best[nm] = { cell = c, lat = math.deg(g.lat[c]), lon = math.deg(g.lon[c]), n = A.p_n[i], id = A.p_id[i] }
        end
      end
    end
    return best
  end)

  -- bench{level, seed, ticks}: sim tick benchmark in the real HTML5 runtime (SIM_LEVEL, W08).
  bridge.register("bench", function(args)
    local bench = require("sim.bench")
    local save = args.default and sys.load_resource("/assets/worlds/default.tcsave") or nil
    if args.world and tostring(args.world):match("^[%w_]+$") then
      local ok, data = pcall(sys.load_resource, "/assets/worlds/" .. args.world .. ".tcsave")
      if ok and data then save = data end
    end
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

  -- wild_debug: the animal puppets drawn (species, group, where, height over the ground) and the
  -- camera focus — shots:wild checks the herds it frames are drawn where it looks (W4)
  bridge.register("wild_debug", function()
    local puppets = require("game.view.puppets")
    local out = { focus = { math.deg(math.asin(state.focus_y or 0)), math.deg(math.atan2(state.focus_z or 0, state.focus_x or 1)) }, animals = {} }
    for _, p in ipairs(puppets.pool) do
      if p.on and p.lat then
        local pos = go.get_position(p.id)
        local r = math.sqrt(pos.x * pos.x + pos.y * pos.y + pos.z * pos.z)
        local x, y, z = pos.x / r, pos.y / r, pos.z / r
        local rs = puppets.surface and select(1, puppets.surface(x, y, z)) or 0
        out.animals[#out.animals + 1] = { sp = p.species or p.kind, group = p.group, lat = math.deg(p.lat), lon = math.deg(p.lon),
          r = r, ground = rs, scale = go.get_scale_uniform and go.get_scale_uniform(p.id) or 0 }
      end
    end
    return out
  end)

  -- (S22) v3_beasts{sett}: every beast, wild animal and dog out of doors on the map now, with what out-tops it
  -- for a bolt there (the strike's own rule: height less 4 a unit of distance within its pull) - reads only
  bridge.register("v3_beasts", function(args)
    local W = assert(G.W, "world not ready")
    local s = assert(W.S.v3.settlements[tonumber(args.sett) or 1], "no such settlement")
    local blaze = require("sim.agents.blaze")
    local danger = require("sim.agents.danger")
    local c = require("sim.config").V3
    local now = require("game.platform.speed").v3_t or W.S.v3.now or W.hour
    local out = {}
    blaze.beasts_out(W, s, now, function(kind, o, H, x, y)
      local top, what = -1e9, "nothing"
      blaze.trees_near(s, x, y, c.strike_pull, function(k, d2)
        local sc = (s.trees.size[k] or 1) * c.strike_tree - math.sqrt(d2) * 4
        if sc > top then top, what = sc, "a tree" end
      end)
      for _, id in ipairs(s.blist) do
        local b = s.buildings[id]
        local d = math.sqrt(((b.cx or b.dx) - x) ^ 2 + ((b.cy or b.dy) - y) ^ 2)
        if b.state == "standing" and not b.natural and d < c.strike_pull then
          local sc = (c.strike_bld[b.kind] or c.strike_roof) - d * 4
          if sc > top then top, what = sc, "the " .. b.kind end
        end
      end
      danger.each_on_map(W, s, function(q)
        if q.inside == 0 then
          local qx, qy = danger.person_pos(q, now)
          local d = math.sqrt((qx - x) ^ 2 + (qy - y) ^ 2)
          if d < c.strike_pull and c.strike_person - d * 4 > top then top, what = c.strike_person - d * 4, q.name end
        end
      end)
      local mine = c.strike_beast[o.sp or "dog"] or 0.25
      out[#out + 1] = { grp = kind == "beast" and H.kind or kind, k = o.k, sp = o.sp or "dog", x = x, y = y, exposed = mine > top,
        margin = mine - math.max(top, -9), taller = what, owner = o.owner or o.hh or 0 }
    end)
    return { sett = s.id, name = s.name, beasts = out, now = now }
  end)
  -- (S22) v3_screen{sett, grp = flock | cattle | swine | wild | dog, k} or {pid}: where that beast or person is
  -- drawn now in window pixels (origin bottom left, as camera.project) and the window's size - for clicking on
  -- it as a player would (scripts/play_powers.mjs). Reads the view only.
  bridge.register("v3_screen", function(args)
    local av = require("game.view.agents_view")
    local e
    if args.pid then
      for _, p in ipairs(av.people_pos) do if p.id == tonumber(args.pid) then e = p end end
    else
      for i = 1, av.beast_n or 0 do
        local b = av.beast_pos[i]
        if b.sid == tonumber(args.sett) and b.grp == args.grp and b.k == tonumber(args.k) then e = b end
      end
    end
    if not e then error("not drawn now", 0) end
    local px, py = camera.project(e.x, e.y, e.z)
    local w, h = window.get_size()
    return { sx = px, sy = py, w = w, h = h }
  end)
  bridge.register("picking_info", function()
    return { last_cell = picking.last_cell }
  end)
  -- (S24) lua{code}: a chunk run with W (the world), now (the world's clock) and require - the acceptance plays
  -- (scripts/play_accept.mjs) read the sim's state before and after a cast with it. Test mode only (the bridge).
  bridge.register("lua", function(args)
    local W = assert(G.W, "world not ready")
    local f, err = loadstring(args.code or "return nil", "=lua")
    if not f then error(err, 0) end
    local env = setmetatable({ W = W, now = require("game.platform.speed").v3_t or W.hour, G = G,
      accept = require("game.platform.accept") }, { __index = _G })
    setfenv(f, env)
    return { v = f() }
  end)
end

return M
