-- Millisecond wall clock for the Defold side (budgets, profiling). Never used by sim/.
-- Measured in the HTML5 build (Stage 1.1, headless Chromium):
--   socket.gettime(): works, Date.now()-backed, 1 ms resolution.
--   os.clock():       emscripten clock() -> performance.now(), 0.1 ms resolution, monotonic.
-- So HTML5 uses os.clock(); native builds use socket.gettime() (os.clock is CPU time there).

local M = {}

local gettime = socket and socket.gettime
local os_clock = os.clock

if html5 then
	M.source = "os.clock"
	local t_origin = os_clock()
	function M.now_ms()
		return (os_clock() - t_origin) * 1000
	end
elseif gettime then
	M.source = "socket.gettime"
	local t_origin = gettime()
	function M.now_ms()
		return (gettime() - t_origin) * 1000
	end
else
	M.source = "os.clock"
	local t_origin = os_clock()
	function M.now_ms()
		return (os_clock() - t_origin) * 1000
	end
end

--- Measures the smallest observable non-zero step of `fn` (default now_ms) by spinning
-- for up to `max_ms` (default 5). Returns resolution in ms (or -1 if it never moved).
function M.probe(max_ms, fn)
	max_ms = max_ms or 5
	fn = fn or M.now_ms
	local start = fn()
	local last = start
	local best = -1
	local spins = 0
	while spins < 2000000 do
		spins = spins + 1
		local t = fn()
		if t ~= last then
			local d = t - last
			if best < 0 or d < best then
				best = d
			end
			last = t
		end
		if t - start >= max_ms then
			break
		end
	end
	return best
end

--- Resolution of socket.gettime() in ms (diagnostic; -1 if LuaSocket is missing).
function M.probe_gettime(max_ms)
	if not gettime then
		return -1
	end
	return M.probe(max_ms, function() return gettime() * 1000 end)
end

return M
