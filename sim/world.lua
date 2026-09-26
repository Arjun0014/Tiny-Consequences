-- World: state container + tick orchestration + command queue/log (05_ARCHITECTURE.md §1, §4).
--
-- Persistent state lives under the keys in world.SAVE_KEYS (saved/restored/hashed generically).
-- Everything else on W is derived and rebuilt from that state by world.rebuild_derived().
--
--   W.seed, W.level, W.tick (ticks completed; the next tick to process is W.tick)
--   W.G       globals (co2, aerosol, g_temp, ice_fraction, ice_volume, sea_level, era, ...)
--   W.F/C/I/P field values / cause tags / idle counters / prev-tick copies (sim/fields.lua)
--   W.static  per-cell static(ish) data: elev, plate, boundary, upwelling, region, ...
--   W.ent     entities: villages, people, ships, volcanoes, ...
--   W.E       event log (sim/causality/events.lua)
--   W.D       detector memory (rolling averages, cooldowns)
--   W.S       system-private persistent state (fire list, blight set, ...)
--   W.names   generated names (regions, ...)
--   W.cmdlog  every command ever applied {tick, type, params}
--
-- Derived: W.grid (+ W.n), neighbour tables, wind/current weights, drainage order, masks...

local config = require("sim.config")
local grid_mod = require("sim.grid")
local fields = require("sim.fields")
local events = require("sim.causality.events")
local rng = require("sim.rng")
local calendar = require("sim.calendar")

local world = {}

world.SAVE_KEYS = { "seed", "level", "tick", "G", "F", "C", "I", "P", "static", "ent", "E",
  "D", "S", "names", "cmdlog", "pending" }

-- Systems in tick order (02_WORLD_MODEL.md §4). Each module may define:
--   init(W)            once after worldgen / restore (allocate scratch; no state changes)
--   rebuild(W)         rebuild derived tables (on load, coastline/elevation change)
--   update(W)          every tick
--   year(W)            at a year boundary, before update (after snap)
world.systems = {}

function world.set_systems(list)
  world.systems = list
end

local grid_cache = {}
function world.get_grid(level)
  local g = grid_cache[level]
  if not g then
    g = grid_mod.build(level)
    grid_cache[level] = g
  end
  return g
end

-- Create an empty world (worldgen fills it in).
function world.new(seed, opts)
  opts = opts or {}
  local level = opts.level or config.SIM_LEVEL
  local W = {
    seed = seed, level = level, tick = 0,
    G = { co2 = 280, aerosol = 0, g_temp = 0, ice_fraction = 0, ice_volume = 0,
          sea_level = 0, sea_built = 0, era = "garden" },
    static = {}, ent = {}, D = {}, S = {}, names = {}, cmdlog = {}, pending = {},
    E = events.new(),
    cfg = config,
    record_events = true,
  }
  W.E.cap = config.EVENT_CAP
  world.bind_grid(W)
  fields.allocate(W, W.n)
  local n = W.n
  local st = W.static
  st.elev = fields.filled(n, 0)
  st.region = fields.filled(n, 0)
  return W
end

-- Mean neighbour spacing at level 4 (radians). Spatial rates in config are tuned per level-4
-- cell; systems scale them by W.sscale (= spacing / level-4 spacing) so a SIM_LEVEL change
-- keeps physical rates (02_WORLD_MODEL.md §2).
world.SPACING_L4 = 0.07553

function world.bind_grid(W)
  local g = world.get_grid(W.level)
  W.grid = g
  W.n = g.n
  W.sscale = g.spacing / world.SPACING_L4
end

-- Sea level the derived tables (coastline, lapse, upwind elevation) were built for. G.sea_level
-- drifts every year; derived data follows it only at a coastline rebuild (set G.sea_built =
-- G.sea_level, then rebuild_derived), so a reloaded world rebuilds exactly what the live one had.
function world.sea_built(W)
  return W.G.sea_built or W.G.sea_level
end

-- Rebuild derived data for every system (after worldgen, load, or coastline change).
function world.rebuild_derived(W)
  require("sim.geo").rebuild(W)
  for i = 1, #world.systems do
    local s = world.systems[i]
    if s.rebuild then s.rebuild(W) end
  end
end

function world.init_systems(W)
  for i = 1, #world.systems do
    local s = world.systems[i]
    if s.init then s.init(W) end
  end
end

-- Commands --------------------------------------------------------------------------------
-- Handlers: world.COMMANDS[type] = function(W, params) ... end
world.COMMANDS = {}

-- Queue a command; it's applied at the start of the next tick processed and logged then.
function world.command(W, ctype, params)
  if not world.COMMANDS[ctype] then error("unknown command " .. tostring(ctype)) end
  local p = W.pending
  p[#p + 1] = { tick = W.tick, type = ctype, params = params or {} }
end

local function apply_commands(W)
  local p = W.pending
  if #p == 0 then return end
  local keep = {}
  for i = 1, #p do
    local cmd = p[i]
    if cmd.tick <= W.tick then
      cmd.tick = W.tick
      W.cmdlog[#W.cmdlog + 1] = cmd
      world.COMMANDS[cmd.type](W, cmd.params)
    else
      keep[#keep + 1] = cmd
    end
  end
  W.pending = keep
end

-- Tick ------------------------------------------------------------------------------------
function world.tick(W)
  local t = W.tick
  if t % 48 == 0 then
    fields.snap_all(W)
    if W.on_year_boundary then W.on_year_boundary(W) end   -- e.g. save.capture
    for i = 1, #world.systems do
      local s = world.systems[i]
      if s.year then s.year(W) end
    end
  end
  apply_commands(W)
  local systems = world.systems
  local prof = W.profile   -- optional: {clock = fn() -> ms (host-supplied), ms = {name -> total}}
  if prof then
    local clock, ms = prof.clock, prof.ms
    for i = 1, #systems do
      local s = systems[i]
      local a = clock()
      s.update(W)
      ms[s.name] = (ms[s.name] or 0) + clock() - a
    end
    prof.ticks = (prof.ticks or 0) + 1
  else
    for i = 1, #systems do
      systems[i].update(W)
    end
  end
  W.tick = t + 1
end

-- Shift every stored absolute tick by dt (used once after worldgen spin-up so the player's
-- clock starts at Year 0). Systems holding ticks implement shift_ticks(W, dt).
function world.shift_ticks(W, dt)
  W.tick = W.tick + dt
  for i = 1, #world.systems do
    local s = world.systems[i]
    if s.shift_ticks then s.shift_ticks(W, dt) end
  end
end

function world.run_ticks(W, n)
  for _ = 1, n do world.tick(W) end
end

function world.run_years(W, y)
  world.run_ticks(W, calendar.ticks(y))
end

function world.year(W) return math.floor(W.tick / 48) end

-- Hashing -----------------------------------------------------------------------------------
-- Deterministic hash of the persistent state: quantised fields + entities + clock.
local M32 = 4294967296
local mix, mul32 = rng.mix, rng.mul32
local bxor = (bit or require("bit")).bxor
local floor = math.floor

local function hnum(h, v)
  if v ~= v then v = -1234567 end -- NaN
  local q = floor(v * 4096 + 0.5) % M32
  return mix(bxor(h, mul32(q, 0x9E3779B1)) % M32)
end

local function hstr(h, s)
  for i = 1, #s do h = mix(bxor(h, s:byte(i) + i * 131) % M32) end
  return h
end

local function sorted_keys(t)
  local nk, sk = {}, {}
  for k in pairs(t) do
    if type(k) == "number" then nk[#nk + 1] = k else sk[#sk + 1] = tostring(k) end
  end
  table.sort(nk)
  table.sort(sk)
  return nk, sk
end
world.sorted_keys = sorted_keys

local function hval(h, v, depth)
  local tv = type(v)
  if tv == "number" then return hnum(h, v)
  elseif tv == "string" then return hstr(h, v)
  elseif tv == "boolean" then return mix(bxor(h, v and 7 or 3) % M32)
  elseif tv == "table" then
    if depth > 12 then return h end
    local nk, sk = sorted_keys(v)
    for i = 1, #nk do h = hval(hnum(h, nk[i]), v[nk[i]], depth + 1) end
    for i = 1, #sk do h = hval(hstr(h, sk[i]), v[sk[i]], depth + 1) end
    return h
  end
  return h
end

-- keys: optional subset of SAVE_KEYS. Excludes the event feed (view-only) and cmd log.
function world.state_hash(W, keys)
  keys = keys or world.SAVE_KEYS
  local h = 2166136261
  for i = 1, #keys do
    local k = keys[i]
    if k ~= "cmdlog" and k ~= "pending" then
      local v = W[k]
      if k == "E" then
        -- hash event records + counters, not the view feed
        h = hval(hstr(h, "E"), { rec = v.rec, next_id = v.next_id }, 0)
      elseif v ~= nil then
        h = hval(hstr(h, k), v, 0)
      end
    end
  end
  return h
end

-- Test hooks (07_TESTING.md §3) ----------------------------------------------------------------
-- world.test_action(W, name, params): queue a logged command; handlers in sim/testhooks.lua
function world.test_action(W, name, params)
  params = params or {}
  params.name = name
  world.command(W, "test_action", params)
end

return world
