-- Text the simulation keeps (memories, whys, objectives) formatted the same in every runtime (S14).
-- string.format("%.0f", 46.5) is "46" in the browser's Lua (musl: half to even) and "47" under
-- LuaJIT: a %.Nf argument is first rounded half up to N decimals here (floor(v * 10^N + 0.5) / 10^N),
-- so no value printed is ever a tie and both runtimes write the same text. Pure Lua 5.1.
local M = {}
local floor = math.floor
local sformat = string.format
local P10 = { [0] = 1, 10, 100, 1000, 10000 }

-- The %.Nf specifiers of a format by argument position (cached per format string).
local SPEC = {}
local function spec(fmt)
  local t = { any = false }
  local i, argn = 1, 0
  while true do
    local a, b = fmt:find("%%[^%%a-zA-Z]*[a-zA-Z%%]", i)
    if not a then break end
    local sp = fmt:sub(a, b)
    if sp ~= "%%" then
      argn = argn + 1
      local n = sp:match("^%%[-+ #0]*%d*%.(%d)f$")
      if n then t[argn] = tonumber(n); t.any = true end
    end
    i = b + 1
  end
  SPEC[fmt] = t
  return t
end

local function rd(v, n)
  if type(v) ~= "number" or v ~= v then return v end
  local p = P10[n] or 10 ^ n
  return floor(v * p + 0.5) / p
end
M.round = rd

function M.format(fmt, a1, a2, a3, a4, a5, a6, a7, a8)
  local t = SPEC[fmt] or spec(fmt)
  if t.any then
    if t[1] then a1 = rd(a1, t[1]) end
    if t[2] then a2 = rd(a2, t[2]) end
    if t[3] then a3 = rd(a3, t[3]) end
    if t[4] then a4 = rd(a4, t[4]) end
    if t[5] then a5 = rd(a5, t[5]) end
    if t[6] then a6 = rd(a6, t[6]) end
    if t[7] then a7 = rd(a7, t[7]) end
    if t[8] then a8 = rd(a8, t[8]) end
  end
  return sformat(fmt, a1, a2, a3, a4, a5, a6, a7, a8)
end

return M
