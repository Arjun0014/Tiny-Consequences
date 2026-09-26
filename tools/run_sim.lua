-- Headless sim runner.
-- npm run sim -- --seed 42 --years 100 [--map] [--map-every 10] [--level 4] [--out DIR]
--   [--no-spinup] [--monthly]
-- Writes out/sim/seed<N>/: summary.json, series.csv (yearly; --monthly adds monthly rows),
-- events.txt, maps/<kind>_y<year>.png
package.path = "./?.lua;./?/init.lua;" .. package.path
local worldgen = require("sim.worldgen.init")
local world = require("sim.world")
local metrics = require("sim.metrics")
local map = require("tools.map_dump")
local report = require("tools.report")

local args = { seed = 42, years = 10, map_every = 10 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif a == "--level" then i = i + 1; args.level = tonumber(arg[i])
  elseif a == "--map" then args.map = true
  elseif a == "--out" then i = i + 1; args.out = arg[i]
  elseif a == "--map-every" then i = i + 1; args.map_every = tonumber(arg[i])
  elseif a == "--no-spinup" then args.no_spinup = true
  elseif a == "--monthly" then args.monthly = true
  elseif a == "--quiet" then args.quiet = true
  end
  i = i + 1
end
local out = args.out or ("out/sim/seed" .. args.seed)
report.mkdir(out .. "/maps")

local t0 = os.clock()
local W = worldgen.generate(args.seed, { level = args.level, no_spinup = args.no_spinup })
local r = W.worldgen_report
local tgen = os.clock() - t0
print(string.format("worldgen seed %d: attempt %d, land %.1f%%, continents %d, islands %d, %.2fs (incl. spin-up)",
  args.seed, r.attempt, r.land_frac * 100, r.continents, r.islands, tgen))

local acc = report.new_accumulator(W)
local series = { table.concat(metrics.SERIES_COLS, ",") }
local function row(m)
  local t = {}
  for _, k in ipairs(metrics.SERIES_COLS) do
    local v = m[k]
    t[#t + 1] = type(v) == "number" and string.format("%.5g", v) or tostring(v)
  end
  series[#series + 1] = table.concat(t, ",")
end

local function dump_maps(year)
  if not args.map then return end
  report.write_maps(W, acc, out .. "/maps", year)
end

row(metrics.compute(W))
local tsim = os.clock()
local tick_times = {}
for y = 1, args.years do
  for _ = 1, 48 do
    local a = os.clock()
    world.tick(W)
    tick_times[#tick_times + 1] = os.clock() - a
    report.accumulate(W, acc)
    if args.monthly and W.tick % 4 == 0 then row(metrics.compute(W)) end
  end
  if not args.monthly then row(metrics.compute(W)) end
  if y % args.map_every == 0 or y == args.years or y == 1 then dump_maps(y) end
  report.reset_accumulator(acc)
  if not args.quiet and y % 10 == 0 then
    local m = metrics.compute(W)
    print(report.line(W, m))
  end
end
local elapsed = os.clock() - tsim
table.sort(tick_times)
local med = tick_times[math.floor(#tick_times / 2) + 1] or 0
local p95 = tick_times[math.floor(#tick_times * 0.95) + 1] or 0

local f = io.open(out .. "/series.csv", "w"); f:write(table.concat(series, "\n"), "\n"); f:close()
report.write_events(W, out .. "/events.txt")
local m = metrics.compute(W)
m.tick_ms_median = med * 1000
m.tick_ms_p95 = p95 * 1000
m.sim_seconds = elapsed
m.seed = args.seed
report.write_json(out .. "/summary.json", m)
print(string.format("ran %d years in %.2fs: tick median %.2f ms, p95 %.2f ms", args.years, elapsed, med * 1000, p95 * 1000))
print(report.line(W, m))
print("out: " .. out)
