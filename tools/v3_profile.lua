-- Where a v3 world's time goes (catch-up speed, roadmap v3 R1/R2): Lua VM instructions sampled by
-- function and line with the JIT off (an instruction-count hook: load-independent, and the
-- interpreter is what the browser's Lua is like), plus the plain interpreter wall time.
--   luajit -joff tools/v3_profile.lua [--load assets/worlds/v3.tcsave] [--days 12] [--warm 1]
--                                     [--pattern sim/] [--top 50] [--nohook]
-- Prints ms of sim per game hour (no hook), instructions per game hour, and the hottest files,
-- functions and lines.
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", days = 12, warm = 1, pattern = "sim", top = 50 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--warm" then i = i + 1; args.warm = tonumber(arg[i])
  elseif a == "--pattern" then i = i + 1; args.pattern = arg[i]
  elseif a == "--top" then i = i + 1; args.top = tonumber(arg[i])
  elseif a == "--nohook" then args.nohook = true
  elseif a == "--time" then args.time = true
  elseif a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--settlements" then i = i + 1; args.settlements = tonumber(arg[i])
  end
  i = i + 1
end
local world = require("sim.world")
local W
if args.seed then
  W = require("sim.agents.worldgen").generate(args.seed, { settlements = args.settlements or 6 })
else
  local f = assert(io.open(args.load, "rb"))
  local data = f:read("*a")
  f:close()
  W = assert(require("sim.worlds").from_save_string(data))
end
local V = W.S.v3
local function alive()
  local n = 0
  for _, s in ipairs(V.settlements) do for _, id in ipairs(s.members) do if V.people[id].alive then n = n + 1 end end end
  return n
end
local t = W.hour
world.advance(W, t + args.warm * 24)
t = W.hour
local hours = args.days * 24
local c0 = os.clock()
if args.nohook then
  world.advance(W, t + hours)
  local dt = os.clock() - c0
  print(string.format("%d people, %d days: %.1f ms of sim per game hour (%.2f s a 12-day season); jit %s",
    alive(), args.days, dt * 1000 / hours, dt * 1000 / hours * 288 / 1000,
    tostring(rawget(_G, "jit") and jit.status()))) -- lint:allow luajit-only
  return
end
if args.time then
  -- time sampling (jit.profile, every 1 ms): C functions and allocation show up under their caller
  local prof = require("jit.profile") -- lint:allow luajit-only
  local fl, fn2 = {}, {}
  local n = 0
  prof.start("li1", function(th, samples, vmstate)
    n = n + samples
    local d = prof.dumpstack(th, "pl", 1)
    fl[d] = (fl[d] or 0) + samples
    local d2 = prof.dumpstack(th, "pF;", 3)
    fn2[d2] = (fn2[d2] or 0) + samples
  end)
  world.advance(W, t + hours)
  prof.stop()
  local dt = os.clock() - c0
  local function dumpt(tb, k, title)
    local ks = {}
    for x in pairs(tb) do ks[#ks + 1] = x end
    table.sort(ks, function(a, b) return tb[a] > tb[b] end)
    print(title)
    for j = 1, math.min(k, #ks) do print(string.format("  %5.1f%%  %s", 100 * tb[ks[j]] / n, ks[j])) end
  end
  print(string.format("%d people: %.1f ms per game hour (sampled)", alive(), dt * 1000 / hours))
  dumpt(fl, args.top, "lines:")
  dumpt(fn2, 30, "stacks (3 deep):")
  return
end
local STEP = 13
local lines, funcs, files = {}, {}, {}
local total = 0
local getinfo = debug.getinfo
local pat = args.pattern
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
world.advance(W, t + hours)
debug.sethook()
local function dump(tb, n, title)
  local ks = {}
  for k in pairs(tb) do ks[#ks + 1] = k end
  table.sort(ks, function(a, b) return tb[a] > tb[b] end)
  print(title)
  for j = 1, math.min(n, #ks) do
    print(string.format("  %9.0f /h %5.1f%%  %s", tb[ks[j]] / hours, 100 * tb[ks[j]] / total, ks[j]))
  end
end
print(string.format("%d people: %.0f instructions per game hour (%d days)", alive(), total / hours, args.days))
dump(files, 30, "files:")
dump(funcs, 45, "functions (file:first line):")
dump(lines, args.top, "lines:")
