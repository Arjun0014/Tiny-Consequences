-- Page URL query parameters (HTML5 only; empty table elsewhere).
-- Recognised params (05_ARCHITECTURE.md §10): test, seed, skip_intro, cam, speed, freeze, debug.

local M = {}

local cached = nil

local function url_decode(s)
	s = s:gsub("+", " ")
	s = s:gsub("%%(%x%x)", function(h) return string.char(tonumber(h, 16)) end)
	return s
end

--- Parses a query string like "?a=1&b=two" into { a = "1", b = "two" }.
-- A key without "=" maps to "1".
function M.parse(search)
	local params = {}
	if type(search) ~= "string" then
		return params
	end
	search = search:gsub("^%?", "")
	for pair in search:gmatch("[^&]+") do
		local k, v = pair:match("^([^=]*)=(.*)$")
		if not k then
			k, v = pair, "1"
		end
		if k ~= "" then
			params[url_decode(k)] = url_decode(v)
		end
	end
	return params
end

function M.get()
	if cached then
		return cached
	end
	local search = ""
	if html5 then
		local ok, res = pcall(html5.run, "window.location.search || ''")
		if ok and type(res) == "string" then
			search = res
		end
	end
	cached = M.parse(search)
	return cached
end

function M.flag(name)
	local v = M.get()[name]
	return v ~= nil and v ~= "0" and v ~= "false" and v ~= ""
end

function M.number(name, default)
	local v = tonumber(M.get()[name])
	if v == nil then
		return default
	end
	return v
end

return M
