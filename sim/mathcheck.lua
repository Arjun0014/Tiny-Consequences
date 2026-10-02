-- (tools, S14) Which C math functions give different bits in two runtimes: each function over
-- n deterministic inputs; returns, per function, a hash of the results and the first inputs' exact
-- values (%.17g), so the browser's Lua and LuaJIT can be compared (scripts/mathcheck.mjs).
local M = {}
local function inputs(n, lo, hi)
  local t, x = {}, 12345
  for i = 1, n do
    x = (x * 1103515245 + 12345) % 2147483648
    t[i] = lo + (hi - lo) * (x / 2147483648)
  end
  return t
end
function M.run(n)
  n = n or 20000
  local fmt = string.format
  local F = {
    { "sin", function(x) return math.sin(x) end, -50, 50 },
    { "cos", function(x) return math.cos(x) end, -50, 50 },
    { "exp", function(x) return math.exp(x) end, -20, 5 },
    { "pow2", function(x) return x ^ 2 end, -3, 3 },
    { "mul2", function(x) return x * x end, -3, 3 },
    { "pow08", function(x) return x ^ 0.8 end, 0, 50 },
    { "pow05", function(x) return x ^ 0.5 end, 0, 50 },
    { "sqrt", function(x) return math.sqrt(x) end, 0, 50 },
    { "acos", function(x) return math.acos(x) end, -1, 1 },
    { "log", function(x) return math.log(x) end, 0.001, 100 },
    { "floor", function(x) return math.floor(x) end, -100, 100 },
    { "mod1", function(x) return x % 1 end, -100, 100 },
    { "fmtf", function(x) return tonumber(fmt("%.14g", x)) end, -100, 100 },
  }
  local out = {}
  for _, f in ipairs(F) do
    local xs = inputs(n, f[3], f[4])
    local vals = {}
    for i = 1, n do vals[i] = fmt("%.17g", f[2](xs[i])) end
    out[#out + 1] = { name = f[1], vals = vals }
  end
  return out
end
return M
