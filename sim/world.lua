-- World: state container + tick orchestration + command queue/log (05_ARCHITECTURE.md §1, §4).
--
-- Persistent state lives under the keys in world.SAVE_KEYS (saved/restored/hashed generically).
-- Everything else on W is derived and rebuilt from that state by world.rebuild_derived().
--
--   W.seed, W.level
--   W.hour    master clock: hours elapsed (the next hour to process is W.hour; D-050)
--   W.tick    planet weeks completed (= ceil(W.hour / 168)); planet systems run at week starts
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

world.SAVE_KEYS = { "seed", "level", "hour", "tick", "hpt", "v3", "G", "F", "C", "I", "P", "static", "ent", "E",
  "D", "S", "names", "cmdlog", "pending" }

-- Systems in tick order (02_WORLD_MODEL.md §4). Each module may define:
--   init(W)            once after worldgen / restore (allocate scratch; no state changes)
--   snap(W)            at a year boundary, right after the field snap: quantise saved state
--   rebuild(W)         rebuild derived tables (on load, coastline/elevation change)
--   update(W)          every planet week (the hour a UTC week starts)
--   hour(W)            every hour (the settlement and agent layer, docs/world/10 §5)
--   year(W)            at a year boundary, before update (after snap)
world.systems = {}
world.weekly = {}
world.hourly = {}
world.continuous = {}   -- systems with advance(W, t): continuous-time agents (v3, D-078)

world.HOURS_PER_DAY = 24
world.HOURS_PER_WEEK = 168
world.HOURS_PER_YEAR = 8064

function world.set_systems(list)
  world.systems = list
  local wk, hr, ct = {}, {}, {}
  for i = 1, #list do
    if list[i].update then wk[#wk + 1] = list[i] end
    if list[i].hour then hr[#hr + 1] = list[i] end
    if list[i].advance then ct[#ct + 1] = list[i] end
  end
  world.weekly, world.hourly, world.continuous = wk, hr, ct
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
    seed = seed, level = level, hour = 0, tick = 0,
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

-- Queue a command; it's applied at the start of the next hour processed and logged then. (S22) In a v3
-- world a command given while an hour is open (the game between frames, the agents part-way through the
-- hour) is applied at its own moment - the agents' clock when it was given - at the next advance, before
-- any more time passes, and logged with that moment (`at`): a bolt aimed at a sheep falls where the
-- sheep is, not an hour later where it has wandered from.
function world.command(W, ctype, params)
  if not world.COMMANDS[ctype] then error("unknown command " .. tostring(ctype)) end
  local p = W.pending
  local at = (W.v3 and W.hour_open and W.S.v3) and W.S.v3.now or nil
  p[#p + 1] = { hour = W.hour, at = at, type = ctype, params = params or {} }
end

-- (S22) v3: the commands given part-way through the open hour, applied now (at their own moment).
local function apply_now(W)
  local p = W.pending
  if #p == 0 then return end
  local keep = {}
  for i = 1, #p do
    local cmd = p[i]
    if cmd.at then
      cmd.hour = W.hour
      W.cmdlog[#W.cmdlog + 1] = cmd
      world.COMMANDS[cmd.type](W, cmd.params)
    else
      keep[#keep + 1] = cmd
    end
  end
  W.pending = keep
end

local function apply_commands(W)
  local p = W.pending
  if #p == 0 then return end
  local keep = {}
  local h = W.hour
  for i = 1, #p do
    local cmd = p[i]
    if cmd.hour <= h then
      cmd.hour = h
      W.cmdlog[#W.cmdlog + 1] = cmd
      world.COMMANDS[cmd.type](W, cmd.params)
    else
      keep[#keep + 1] = cmd
    end
  end
  W.pending = keep
end

-- Clock -----------------------------------------------------------------------------------
-- world.step advances one hour. At the start of a UTC week the planet systems run (the old weekly
-- tick); at a year boundary the fields are snapped and the save snapshot captured first, before
-- that hour's commands (so the snapshot + the commands since reproduce the live world). Hourly
-- systems (the settlement and agent layer) run every hour after the planet update.
local function year_boundary(W)
  fields.snap_all(W)
  local systems = world.systems
  for i = 1, #systems do
    local s = systems[i]
    if s.snap then s.snap(W) end                         -- quantise system state for saving
  end
  if W.on_year_boundary then W.on_year_boundary(W) end   -- e.g. save.capture
  for i = 1, #systems do
    local s = systems[i]
    if s.year then s.year(W) end
  end
end

local function planet_week(W)
  local systems = world.weekly
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
  W.tick = W.tick + 1
end

-- Hours per planet step: 168 (a week) in v2 worlds; 24 (a day) in v3 worlds (W.hpt, D-078: a
-- 48-day year, so seasons pass at watchable speeds while every planet step is still 1/48 year).
local function hpt_of(W) return W.hpt or 168 end
world.hpt_of = hpt_of

-- v3 worlds split an hour in two so the host can advance to a fractional time (D-078):
-- begin_hour runs everything that happens *at* hour h (year boundary, commands, the planet step,
-- the hourly systems); continuous systems (the agents, `advance(W, t)`) then live through
-- [h, h + 1) in event order, however the host slices it; end_hour closes the hour.
function world.begin_hour(W)
  local h = W.hour
  local week_start = h % hpt_of(W) == 0
  if week_start and W.tick % 48 == 0 then year_boundary(W) end
  apply_commands(W)
  if week_start then planet_week(W) end
  local hs = world.hourly
  for i = 1, #hs do hs[i].hour(W) end
  W.hour_open = true
end

function world.end_hour(W)
  local h = W.hour
  local cs = world.continuous
  for i = 1, #cs do cs[i].advance(W, h + 1) end
  W.hour = h + 1
  W.hour_open = false
end

-- Advance a v3 world to absolute (fractional) hour t, at most `max_hours` whole hours (nil: no
-- limit). Returns the number of hours closed.
function world.advance(W, t, max_hours)
  local n = 0
  if W.hour_open and #W.pending > 0 then apply_now(W) end
  while W.hour + 1 <= t do
    if max_hours and n >= max_hours then return n end
    if not W.hour_open then world.begin_hour(W) end
    world.end_hour(W)
    n = n + 1
  end
  if t > W.hour then
    if not W.hour_open then world.begin_hour(W) end
    local cs = world.continuous
    for i = 1, #cs do cs[i].advance(W, t) end
  end
  return n
end

function world.step(W)
  if W.v3 then
    if not W.hour_open then world.begin_hour(W) end
    world.end_hour(W)
    return
  end
  local h = W.hour
  local week_start = h % 168 == 0
  if week_start and W.tick % 48 == 0 then year_boundary(W) end
  apply_commands(W)
  if week_start then planet_week(W) end
  local hs = world.hourly
  local prof = W.profile
  if prof then
    local clock, ms = prof.clock, prof.ms
    for i = 1, #hs do
      local s = hs[i]
      local a = clock()
      s.hour(W)
      local key = s.name .. ":hour"
      ms[key] = (ms[key] or 0) + clock() - a
    end
  else
    for i = 1, #hs do hs[i].hour(W) end
  end
  W.hour = h + 1
end

-- One planet week: step hours until the next UTC week starts (168 steps from a week start).
function world.tick(W)
  local hpt = hpt_of(W)
  repeat world.step(W) until W.hour % hpt == 0
end

-- Shift every stored absolute tick by dt weeks (used once after worldgen spin-up so the player's
-- clock starts at Year 0). Systems holding ticks or hours implement shift_ticks(W, dt) (dt in
-- weeks; hours shift by dt × 168).
function world.shift_ticks(W, dt)
  W.tick = W.tick + dt
  W.hour = W.hour + dt * hpt_of(W)
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

function world.year(W) return math.floor(W.hour / (hpt_of(W) * 48)) end
function world.day(W) return math.floor(W.hour / 24) end

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

-- (tools, S14) The hash of each child of the table at `path` (a list of keys from W): where two
-- runtimes' states first differ (scripts/v3_hash_web.mjs --drill).
function world.hash_parts(W, path)
  local t = W
  for i = 1, #(path or {}) do
    local k = path[i]
    t = t[k] ~= nil and t[k] or t[tonumber(k) or k]
    if type(t) ~= "table" then return {} end
  end
  local out = {}
  local nk, sk = sorted_keys(t)
  for i = 1, #nk do out[#out + 1] = { k = tostring(nk[i]), h = tostring(hval(2166136261, t[nk[i]], #(path or {}))), t = type(t[nk[i]]) } end
  for i = 1, #sk do
    local v = t[sk[i]]
    if v == nil then v = t[sk[i] == "true" or (sk[i] ~= "false" and nil)] end
    out[#out + 1] = { k = sk[i], h = tostring(hval(2166136261, v, #(path or {}))), t = type(v) }
  end
  return out
end

-- Test hooks (07_TESTING.md §3) ----------------------------------------------------------------
-- world.test_action(W, name, params): queue a logged command; handlers in sim/testhooks.lua
function world.test_action(W, name, params)
  params = params or {}
  params.name = name
  world.command(W, "test_action", params)
end

return world
