-- Line-level cost of the simulation: Lua VM instructions sampled by source line (the JIT off,
-- a count hook every STEP instructions: load-independent, like society_profile).
--   luajit tools/line_profile.lua <seed | world.tcsave> [years=0] [weeks=8] [pattern=society] [top=60]
-- Prints the hottest lines (instructions per planet week) and a per-function summary for the
-- source files matching `pattern`, plus each file's share of the whole.
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local W
if arg[1] and arg[1]:match("%.tcsave$") then
  local f = assert(io.open(arg[1], "rb")); local str = f:read("*a"); f:close()
  W = require("sim.worlds").from_save_string(str)
else
  W = require("sim.worldgen.init").generate(tonumber(arg[1]) or 22)
end
local years = tonumber(arg[2]) or 0
if years > 0 then world.run_years(W, years) end
local weeks = tonumber(arg[3]) or 8
local pat = arg[4] or "society"
local top = tonumber(arg[5]) or 60
local STEP = 7
local lines, funcs, files = {}, {}, {}
local total = 0
local getinfo = debug.getinfo
if rawget(_G, "jit") then jit.off(); jit.flush() end -- lint:allow luajit-only
debug.sethook(function()
  total = total + STEP
  local info = getinfo(2, "Sl")
  if not info then return end
  local src = info.short_src
  files[src] = (files[src] or 0) + STEP
  if src:find(pat, 1, true) then
    local k = src .. ":" .. (info.currentline or 0)
    lines[k] = (lines[k] or 0) + STEP
    local fk = src .. ":" .. (info.linedefined or 0)
    funcs[fk] = (funcs[fk] or 0) + STEP
  end
end, "", STEP)
for _ = 1, weeks do world.tick(W) end
debug.sethook()
local function dump(t, n, title)
  local ks = {}
  for k in pairs(t) do ks[#ks + 1] = k end
  table.sort(ks, function(a, b) return t[a] > t[b] end)
  print(title)
  for i = 1, math.min(n, #ks) do
    print(string.format("  %9.0f /wk %5.1f%%  %s", t[ks[i]] / weeks, 100 * t[ks[i]] / total, ks[i]))
  end
end
local pop = 0
for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop end end
print(string.format("total %.0f instr/week (%d people, %.0f per person-week)", total / weeks, pop, total / weeks / math.max(1, pop)))
dump(files, 25, "files:")
dump(funcs, 40, "functions (file:first line):")
dump(lines, top, "lines:")
