-- Test bridge (05_ARCHITECTURE.md §10). Active only in HTML5 with ?test=1.
--
-- Lua -> JS: every PUBLISH_INTERVAL seconds (and after every command) sets
--   window.__TC = { ready, seed, tick, year, speed, fps, sim_ms_avg, sim_ms_max, view_ms,
--                   population, villages, events, last_headline, errors, state_hash,
--                   cmd_seq, last_cmd, clock, t_ms, ... }
-- JS -> Lua: each frame pops one command from window.__TC_CMDS (array of objects):
--   { cmd = "<name>", id = <optional correlation id>, ...args }
-- Result of each command is published as last_cmd = { id, cmd, ok, result, error, seq }.
-- Handlers: bridge.register(name, fn(args) -> result). Lua errors inside handlers,
-- unknown commands and any Lua runtime error (via sys.set_error_handler) go to `errors`.

local clock = require("game.platform.clock")
local query = require("game.platform.query")
local speed = require("game.platform.speed")

local M = {}

M.PUBLISH_INTERVAL = 0.5
M.MAX_ERRORS = 50
M.enabled = false
M.params = {}

local handlers = {}
local provider = nil
local errors = {}
local since_publish = 0
local cmd_seq = 0
local last_cmd = nil
local clock_info = nil
local fps_frames, fps_time, fps_value = 0, 0, 0

local JSON_OPTS = { encode_empty_table_as_object = false }

local POP_JS = "(function(){var q=window.__TC_CMDS;if(!q||!q.length)return '';"
	.. "var c=q.shift();try{return JSON.stringify(c);}catch(e){return '{\"cmd\":\"__bad_json\"}';}})()"

local DEFAULTS = {
	ready = false, seed = 0, tick = 0, year = 0, speed = 0,
	sim_ms_avg = 0, sim_ms_max = 0, view_ms = 0,
	population = 0, villages = 0, events = 0, last_headline = "", state_hash = "",
}

function M.error(message)
	message = tostring(message)
	if #errors < M.MAX_ERRORS then
		errors[#errors + 1] = message
	end
	print("[bridge] error: " .. message)
end

function M.register(name, fn)
	assert(type(name) == "string" and type(fn) == "function", "bridge.register(name, fn)")
	handlers[name] = fn
end

function M.set_provider(fn)
	provider = fn
end

function M.publish()
	if not M.enabled then
		return
	end
	since_publish = 0
	local state = {}
	if provider then
		local ok, res = pcall(provider)
		if ok and type(res) == "table" then
			state = res
		elseif not ok then
			M.error("provider: " .. tostring(res))
		end
	end
	for k, v in pairs(DEFAULTS) do
		if state[k] == nil then
			state[k] = v
		end
	end
	state.fps = fps_value
	state.errors = errors
	state.cmd_seq = cmd_seq
	state.last_cmd = last_cmd
	state.clock = clock_info
	state.t_ms = clock.now_ms()
	local ok, encoded = pcall(json.encode, state, JSON_OPTS)
	if not ok then
		M.error("publish encode: " .. tostring(encoded))
		encoded = json.encode({ ready = false, errors = errors }, JSON_OPTS)
	end
	html5.run("window.__TC = " .. encoded .. ";0")
end

local function dispatch(raw)
	local ok, cmd = pcall(json.decode, raw)
	if not ok or type(cmd) ~= "table" then
		M.error("bad command JSON: " .. tostring(raw))
		return
	end
	local name = cmd.cmd
	cmd_seq = cmd_seq + 1
	local fn = handlers[name]
	local result = { id = cmd.id, cmd = name, seq = cmd_seq, ok = false }
	if not fn then
		result.error = "unknown command: " .. tostring(name)
		M.error(result.error)
	else
		local ok_call, res = pcall(fn, cmd)
		result.ok = ok_call
		if ok_call then
			result.result = res
		else
			result.error = tostring(res)
			M.error("command " .. tostring(name) .. ": " .. result.error)
		end
	end
	last_cmd = result
end

-- Built-in handlers --------------------------------------------------------

handlers.ping = function(cmd)
	return { pong = true, t_ms = clock.now_ms(), echo = cmd.echo }
end

handlers.set_speed = function(cmd)
	return { speed = speed.set(cmd.n) }
end

handlers.publish = function()
	return true
end

-- Raises a Lua error outside the bridge's pcall (next frame, in a timer callback) to
-- prove that engine-level errors reach __TC.errors via sys.set_error_handler.
handlers.debug_error = function()
	timer.delay(0, false, function()
		error("debug_error requested")
	end)
	return true
end

handlers.clear_errors = function()
	for i = #errors, 1, -1 do
		errors[i] = nil
	end
	return true
end

-- Lifecycle ----------------------------------------------------------------

function M.init()
	M.params = query.get()
	M.enabled = (html5 ~= nil) and M.params.test == "1"
	if not M.enabled then
		return false
	end
	html5.run("window.__TC_CMDS = window.__TC_CMDS || []; window.__TC = window.__TC || {ready:false, errors:[]};0")
	sys.set_error_handler(function(source, message, traceback)
		M.error(tostring(source) .. ": " .. tostring(message))
	end)
	clock_info = { source = clock.source, res_ms = clock.probe(3), gettime_res_ms = clock.probe_gettime(3) }
	print("[bridge] test mode on; clock=" .. clock_info.source .. " res_ms=" .. tostring(clock_info.res_ms))
	return true
end

function M.update(dt)
	if not M.enabled then
		return
	end
	fps_frames = fps_frames + 1
	fps_time = fps_time + dt
	if fps_time >= 0.5 then
		fps_value = math.floor(fps_frames / fps_time * 10 + 0.5) / 10
		fps_frames, fps_time = 0, 0
	end

	local raw = html5.run(POP_JS)
	if raw and raw ~= "" then
		dispatch(raw)
		M.publish()
		return
	end

	since_publish = since_publish + dt
	if since_publish >= M.PUBLISH_INTERVAL then
		M.publish()
	end
end

return M
