-- npm run bench [-- --level 4 --ticks 192 --seed 42]
package.path = "./?.lua;./?/init.lua;" .. package.path
local bench = require("sim.bench")
local args = { level = nil, ticks = 192, seed = 42 }
local i = 1
while arg[i] do
  if arg[i] == "--level" then i = i + 1; args.level = tonumber(arg[i])
  elseif arg[i] == "--ticks" then i = i + 1; args.ticks = tonumber(arg[i])
  elseif arg[i] == "--seed" then i = i + 1; args.seed = tonumber(arg[i]) end
  i = i + 1
end
-- os.clock has 1 ms resolution on Windows: measure per-tick by batching when needed
local clock = function() return os.clock() * 1000 end
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
