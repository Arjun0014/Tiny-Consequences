-- npm run bench [-- --level 4 --ticks 192 --seed 42] [--iso]
--   --update-baseline: re-measure the P07 baseline (VM instructions per system on the fixed
--                      world, tools/perf.lua) and write tests/bench-baseline.json (intentional
--                      changes only; log them in DECISIONS.md)
--   --check:           compare against tests/bench-baseline.json (what P07 does)
--   default: representative-tick benchmark (sim/bench.lua, shared with the browser bridge)
--   --iso:   whole-tick mean + per-system means, each the best of --reps runs (default 3) of
--            --ticks ticks (os.clock has ~1 ms resolution on Windows, and other processes add
--            noise, so we time many ticks and keep the fastest run). Use `luajit -joff` for the
--            interpreter (browser proxy: HTML5 ≈ 3.5 × LuaJIT -joff at level 3).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local bench = require("sim.bench")
local args = { level = nil, ticks = 192, seed = 42, reps = 3 }
local i = 1
while arg[i] do
  if arg[i] == "--level" then i = i + 1; args.level = tonumber(arg[i])
  elseif arg[i] == "--ticks" then i = i + 1; args.ticks = tonumber(arg[i])
  elseif arg[i] == "--reps" then i = i + 1; args.reps = tonumber(arg[i])
  elseif arg[i] == "--iso" then args.iso = true
  elseif arg[i] == "--update-baseline" then args.update = true
  elseif arg[i] == "--check" then args.check = true
  elseif arg[i] == "--seed" then i = i + 1; args.seed = tonumber(arg[i]) end
  i = i + 1
end
local clock = function() return os.clock() * 1000 end

if args.update or args.check then
  local perf = require("tools.perf")
  local res = perf.measure()
  local rows = {}
  for _, name in ipairs(res.order) do rows[#rows + 1] = string.format("%s %d", name, res.systems[name]) end
  print(string.format("instructions/tick %d (other %d): %s", res.total, res.other, table.concat(rows, ", ")))
  if args.update then
    perf.write_baseline(nil, res, nil, {
      date = os.date("%Y-%m-%d"),
      calibration = "HTML5 release build, sim/bench.lua config (seed 42, level 3, 192 ticks): 1.70 ms median "
        .. "for 230,910 instructions/tick, i.e. ~7.4 ns per instruction (6-10 ns per system), 2026-09-27",
    })
    print("wrote " .. perf.BASELINE)
  else
    local base = assert(perf.load_baseline(), "no baseline: npm run bench -- --update-baseline")
    local ok, lines = perf.compare(res, base)
    print(ok and "P07 ok" or ("P07 REGRESSION: " .. table.concat(lines, "; ")))
    os.exit(ok and 0 or 1)
  end
  return
end

if args.iso then
  local worldgen = require("sim.worldgen.init")
  local world = require("sim.world")
  local save = require("sim.save")
  local W0 = worldgen.generate(args.seed, { level = args.level })
  world.run_years(W0, 1)
  local snapW = {}
  for _, k in ipairs(world.SAVE_KEYS) do snapW[k] = save.deepcopy(W0[k]) end
  local function fresh()
    local W = world.new(snapW.seed, { level = snapW.level })
    for _, k in ipairs(world.SAVE_KEYS) do
      if k == "E" then
        for kk, vv in pairs(save.deepcopy(snapW.E)) do W.E[kk] = vv end
      elseif k ~= "cmdlog" and k ~= "pending" then W[k] = save.deepcopy(snapW[k]) end
    end
    W.cmdlog, W.pending = {}, {}
    world.rebuild_derived(W)
    world.init_systems(W)
    return W
  end
  local n = args.ticks
  -- whole tick
  local best_tick = math.huge
  for _ = 1, args.reps do
    local W = fresh()
    local a = clock()
    for _ = 1, n do world.tick(W) end
    local ms = (clock() - a) / n
    if ms < best_tick then best_tick = ms end
  end
  -- per system: run whole ticks, time each system (sum over n ticks), keep the best run
  local best = {}
  for _ = 1, args.reps do
    local W = fresh()
    W.profile = { clock = clock, ms = {} }
    for _ = 1, n do world.tick(W) end
    for k, v in pairs(W.profile.ms) do
      local ms = v / n
      if not best[k] or ms < best[k] then best[k] = ms end
    end
  end
  print(string.format("level %d (%d cells), %d ticks x %d reps: whole tick mean %.3f ms",
    W0.level, W0.n, n, args.reps, best_tick))
  local total = 0
  for _, s in ipairs(world.systems) do
    local v = best[s.name] or 0
    total = total + v
    print(string.format("  %-14s %6.3f ms", s.name, v))
  end
  print(string.format("  %-14s %6.3f ms (sum of per-system bests)", "sum", total))
  return
end

local res = bench.run({ level = args.level, seed = args.seed, ticks = args.ticks, clock = clock })
print(string.format("level %d (%d cells): tick median %.2f ms, p95 %.2f ms, max %.2f ms (gen %.0f ms)",
  res.level, res.cells, res.median_ms, res.p95_ms, res.max_ms, res.gen_ms))
local names = {}
for k in pairs(res.systems) do names[#names + 1] = k end
table.sort(names, function(a, b) return res.systems[a] > res.systems[b] end)
local total = 0
for _, k in ipairs(names) do total = total + res.systems[k] end
for _, k in ipairs(names) do print(string.format("  %-14s %6.3f ms", k, res.systems[k])) end
print(string.format("  %-14s %6.3f ms (mean)", "total", total))
