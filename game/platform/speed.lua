-- Time stepping & speed control (05_ARCHITECTURE.md §6; hour steps since D-050).
-- The caller passes the world's hour step; the level ladder is in hours per real second.

local clock = require("game.platform.clock")

local M = {}

-- Levels (docs/world/15_CLIMATE_TIME.md §6; the sim steps whole hours, D-050):
--   0 pause, 1 watch (1 h per 5 s), 2 (1 h per 0.8 s), 3 (1 day per 3 s), 4 (1 week per s),
--   5 (16 weeks per s), 6 max (as many hours as the frame budget allows).
M.PAUSE, M.NORMAL, M.FAST, M.FASTER, M.MAX = 0, 1, 2, 3, 6
M.MAX_LEVEL = 6

M.HOURS_PER_SECOND = { [0] = 0, 0.2, 1.25, 8, 168, 2688, -1 } -- -1 = as many as the budget allows
M.TICKS_PER_SECOND = M.HOURS_PER_SECOND   -- (old name, kept for the bridge)
M.SIM_BUDGET_MS = { [0] = 0, 4, 4, 5, 6, 8, 8 }
M.MAX_BACKLOG = { [0] = 0, 2, 4, 24, 168, 800, 2000 }  -- hours; never spiral
M.MAX_TICKS_PER_FRAME = 20000 -- hard safety cap on hour steps per frame
M.STATS_WINDOW = 60         -- frames per sim_ms_avg window

local level = M.NORMAL
local ticks_due = 0
local ticks_total = 0
local stat_frames, stat_sum, stat_max = 0, 0, 0
local last_avg, last_max = 0, 0
local last_frame_ms, last_frame_ticks = 0, 0
local tick_est = 0.02  -- ms, running estimate of a light hour step
local week_est = 4     -- ms, running estimate of an hour step that runs the planet week

function M.set(n)
	n = tonumber(n)
	if n == nil or n ~= math.floor(n) or n < 0 or n > M.MAX_LEVEL then
		error("speed.set: invalid speed level " .. tostring(n), 2)
	end
	if M.v3_t and n > M.V3_WATCH_MAX then
		-- v3 (D-079): no watch speed beyond ▶▶▶; faster is an explicit skip (M.v3_skip)
		error("speed.set: invalid speed level " .. tostring(n) .. " (v3 watches at 0-3; skip ahead instead)", 2)
	end
	if n ~= level then
		level = n
		ticks_due = 0
	end
	return level
end

function M.get()
	return level
end

function M.ticks()
	return ticks_total
end

--- Fraction of the next hour already accumulated (for view interpolation).
function M.alpha()
	if ticks_due < 0 then
		return 0
	end
	local a = ticks_due - math.floor(ticks_due)
	if ticks_due >= 1 then a = 0.999 end
	return a
end

--- Length of a real second in sim hours at the current level (0 when paused; max: last rate).
function M.hours_per_second()
	if M.skip then return M.skip.rate or 24 end
	local hps = (M.v3_t and M.V3_HPS or M.HOURS_PER_SECOND)[level]
	if hps < 0 then return M.max_rate or 3000 end
	return hps
end

--- Advances time by dt seconds, calling step_fn() once per due hour within budget.
-- heavy_fn() (optional) says whether the next step runs the planet week (a separate, larger cost
-- estimate, so a frame never starts a planet week it can't afford). Returns steps run.
function M.step(dt, step_fn, heavy_fn)
	local hps = M.HOURS_PER_SECOND[level]
	if hps == 0 then
		last_frame_ms, last_frame_ticks = 0, 0
		return 0
	end
	local backlog = M.MAX_BACKLOG[level]
	if hps < 0 then
		ticks_due = backlog
	else
		ticks_due = ticks_due + dt * hps
	end
	local budget = M.SIM_BUDGET_MS[level]
	local t0 = clock.now_ms()
	local ran = 0
	while ticks_due >= 1 and ran < M.MAX_TICKS_PER_FRAME do
		local heavy = heavy_fn and heavy_fn() or false
		local est = heavy and week_est or tick_est
		-- stop before a step that would overrun the frame budget (at least one step per frame);
		-- the estimate gets a 25% margin because single steps spike (GC, a busy machine)
		if ran > 0 and clock.now_ms() - t0 + est * 1.25 > budget then
			break
		end
		local a = clock.now_ms()
		step_fn()
		local b = clock.now_ms()
		local sample = b - a
		if heavy then
			-- robust running estimate: rare slow weeks (year boundaries, GC) don't inflate it
			if sample > week_est * 1.5 + 0.5 then sample = week_est * 1.5 + 0.5 end
			week_est = week_est + (sample - week_est) * 0.15
		else
			if sample > tick_est * 1.5 + 0.05 then sample = tick_est * 1.5 + 0.05 end
			tick_est = tick_est + (sample - tick_est) * 0.05
		end
		ticks_due = ticks_due - 1
		ran = ran + 1
	end
	if hps < 0 then
		ticks_due = 0
		if dt > 0 then M.max_rate = ran / math.max(dt, 1 / 120) end
	elseif ticks_due > backlog then
		ticks_due = backlog
	end
	ticks_total = ticks_total + ran

	local ms = clock.now_ms() - t0
	last_frame_ms, last_frame_ticks = ms, ran
	stat_frames = stat_frames + 1
	stat_sum = stat_sum + ms
	if ms > stat_max then
		stat_max = ms
	end
	if stat_frames >= M.STATS_WINDOW then
		last_avg = stat_sum / stat_frames
		last_max = stat_max
		stat_frames, stat_sum, stat_max = 0, 0, 0
	end
	return ran
end

-- v3 worlds (D-078, D-079): time runs continuously (world.advance to fractional hours). Watch speeds
-- are game minutes per real second: ▶ 1, ▶▶ 4 (the start), ▶▶▶ 12 (a day in 2 minutes). There is
-- no faster *watch* speed: catch-up is an explicit skip to a moment (dawn, evening, a day on, the
-- next season) during which the view hides the people and the sim runs unrendered as fast as the
-- frame budget allows; afterwards everyone is exactly where the sim put them.
M.V3_HPS = { [0] = 0, 1 / 60, 4 / 60, 12 / 60 }
M.V3_WATCH_MAX = 3
M.V3_START = 2
M.SKIP_BUDGET_MS = 30
M.v3_t = nil
M.skip = nil   -- { from, to, kind, label, rate, t_real0 }

function M.v3_init(W)
  M.v3_t = W.hour
  M.skip = nil
end

function M.v3_skipping()
  return M.skip ~= nil
end

M.SKIP_LABELS = { dawn = "Until dawn", evening = "Until evening", day = "One day", season = "Next season" }

--- The absolute hour a skip of `kind` ends at, at settlement s's local time (default: the first).
-- dawn: a little before sunrise (the early risers are up); evening: the walk home from the
-- fields; day: 24 hours on; season: the dawn of the first day of the next local season.
function M.v3_skip_target(W, kind, s)
  local clock = require("sim.agents.clock")
  s = s or W.S.v3.settlements[1]
  local now = M.v3_t or W.hour
  local lon, lat = W.grid.lon[s.cell], W.grid.lat[s.cell]
  local function next_lh(from, h)
    local d = (h - clock.local_hour(from, lon)) % 24
    if d < 0.1 then d = d + 24 end
    return from + d
  end
  if kind == "day" then return now + 24 end
  if kind == "evening" then
    local _, ss = clock.sun_times(lat, now + 6)
    return next_lh(now, ss - 1.5)
  end
  local from = now
  if kind == "season" then
    local ldoy = clock.local_doy(now, lat)
    from = (math.floor(now / 24) + (12 - ldoy % 12)) * 24 - 2
  elseif kind ~= "dawn" then
    error("speed.v3_skip_target: unknown skip " .. tostring(kind), 2)
  end
  local sr = clock.sun_times(lat, from + 12)
  return next_lh(from, sr - 0.8)
end

--- Start skipping to absolute hour `to` (kind: a label key). Returns the target.
function M.v3_skip(W, kind, to)
  to = to or M.v3_skip_target(W, kind)
  local now = M.v3_t or W.hour
  if to <= now then return now end
  M.skip = { from = now, to = to, kind = kind, label = M.SKIP_LABELS[kind] or "Skipping", rate = 24,
    t_real0 = clock.now_ms() }
  return to
end

--- Stop a skip where it is (the world simply continues from there).
function M.v3_skip_cancel()
  M.skip = nil
end

--- Progress of the running skip (0..1), or nil.
function M.v3_skip_progress()
  local k = M.skip
  if not k then return nil end
  return math.min(1, ((M.v3_t or k.from) - k.from) / math.max(1e-6, k.to - k.from))
end

--- v3: advance world W by dt real seconds at the current level (or the running skip), within the
-- frame budget.
function M.step_v3(dt, W, world)
	if not M.v3_t then M.v3_t = W.hour end
	-- real elapsed time, not the engine's frame dt (fixed per frame: a slow frame rate would slow
	-- the world down with it); at most a quarter second per frame
	local now_ms = clock.now_ms()
	if M.v3_last then dt = math.min(0.25, math.max(0, (now_ms - M.v3_last) / 1000)) end
	M.v3_last = now_ms
	local sk = M.skip
	local hps = M.V3_HPS[level] or 0
	if hps == 0 and not sk then
		last_frame_ms, last_frame_ticks = 0, 0
		return 0
	end
	local budget = sk and M.SKIP_BUDGET_MS or M.SIM_BUDGET_MS[level]
	local t0 = clock.now_ms()
	local target = sk and sk.to or (M.v3_t + dt * hps)
	local cur = M.v3_t
	local closed = 0
	while cur < target do
		local nxt = math.floor(cur) + 1
		if nxt > target then nxt = target end
		closed = closed + world.advance(W, nxt)
		cur = nxt
		if clock.now_ms() - t0 > budget then break end
	end
	-- a watch frame that could not keep up drops the rest (never spiral); a skip continues
	if sk then
		if dt > 0 then sk.rate = sk.rate + ((cur - M.v3_t) / math.max(dt, 1 / 120) - sk.rate) * 0.2 end
		if cur >= sk.to then
			M.skip = nil
			M.skip_done = { kind = sk.kind, ms = clock.now_ms() - sk.t_real0, hours = sk.to - sk.from }
		end
	end
	M.v3_t = cur
	local ms = clock.now_ms() - t0
	last_frame_ms, last_frame_ticks = ms, closed
	ticks_total = ticks_total + closed
	stat_frames = stat_frames + 1
	stat_sum = stat_sum + ms
	if ms > stat_max then stat_max = ms end
	if stat_frames >= M.STATS_WINDOW then
		last_avg = stat_sum / stat_frames
		last_max = stat_max
		stat_frames, stat_sum, stat_max = 0, 0, 0
	end
	return closed
end

--- Running estimate of a planet-week step in ms (budget prediction).
function M.week_estimate()
	return week_est
end

--- Running estimate of one tick in ms (budget prediction).
function M.tick_estimate()
	return tick_est
end

--- sim_ms_avg / sim_ms_max over the last full stats window (ms of sim work per frame).
function M.stats()
	return last_avg, last_max, last_frame_ms, last_frame_ticks
end

return M
