-- Time stepping & speed control (05_ARCHITECTURE.md §6, 04_PLAYER.md §3).
-- Stage 1.1: the loop and speed levels; the world tick is passed in by the caller.
-- Levels: 0 pause, 1 normal (2 t/s), 2 fast (8), 3 faster (48), 4 max (budget-bound).
-- Level 5 (epoch) arrives in Chapter 3.

local clock = require("game.platform.clock")

local M = {}

M.PAUSE, M.NORMAL, M.FAST, M.FASTER, M.MAX = 0, 1, 2, 3, 4
M.MAX_LEVEL = 4

M.TICKS_PER_SECOND = { [0] = 0, 2, 8, 48, -1 } -- -1 = as many as the budget allows
M.SIM_BUDGET_MS = { [0] = 0, 4, 4, 6, 8 }
M.MAX_BACKLOG = 64          -- ticks; never spiral
M.MAX_TICKS_PER_FRAME = 4096 -- hard safety cap (matters only for trivial tick functions)
M.STATS_WINDOW = 60         -- frames per sim_ms_avg window

local level = M.NORMAL
local ticks_due = 0
local ticks_total = 0
local stat_frames, stat_sum, stat_max = 0, 0, 0
local last_avg, last_max = 0, 0
local last_frame_ms, last_frame_ticks = 0, 0

function M.set(n)
	n = tonumber(n)
	if n == nil or n ~= math.floor(n) or n < 0 or n > M.MAX_LEVEL then
		error("speed.set: invalid speed level " .. tostring(n), 2)
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

--- Fraction of the next tick already accumulated (for view interpolation).
function M.alpha()
	if ticks_due < 0 then
		return 0
	end
	return ticks_due - math.floor(ticks_due)
end

--- Advances time by dt seconds, calling tick_fn() once per due tick within budget.
-- Returns the number of ticks run this frame.
function M.step(dt, tick_fn)
	local tps = M.TICKS_PER_SECOND[level]
	if tps == 0 then
		last_frame_ms, last_frame_ticks = 0, 0
		return 0
	end
	if tps < 0 then
		ticks_due = M.MAX_BACKLOG
	else
		ticks_due = ticks_due + dt * tps
	end
	local budget = M.SIM_BUDGET_MS[level]
	local t0 = clock.now_ms()
	local ran = 0
	while ticks_due >= 1 and ran < M.MAX_TICKS_PER_FRAME do
		tick_fn()
		ticks_due = ticks_due - 1
		ran = ran + 1
		if clock.now_ms() - t0 >= budget then
			break
		end
	end
	if tps < 0 then
		ticks_due = 0
	elseif ticks_due > M.MAX_BACKLOG then
		ticks_due = M.MAX_BACKLOG
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

--- sim_ms_avg / sim_ms_max over the last full stats window (ms of sim work per frame).
function M.stats()
	return last_avg, last_max, last_frame_ms, last_frame_ticks
end

return M
