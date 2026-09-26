-- Deterministic performance measure (P07, D-029): Lua VM instructions per tick, per system.
--
-- Wall-clock timings on the dev machine swing 3× with other load; instruction counts from a
-- debug count hook are exact and repeatable. The count hook only fires in the interpreter, so
-- the JIT is switched off (and its traces flushed) while measuring and restored afterwards.
-- Counts are LuaJIT interpreter bytecodes (not PUC Lua 5.1's), a close proxy for the HTML5
-- build's cost; the baseline file records the browser calibration.
--
--   perf.measure(cfg)            -> { total, systems = { name -> instr/tick }, other }
--   perf.load_baseline(path)     -> table | nil
--   perf.write_baseline(path, res, cfg, note)
--   perf.compare(res, base)      -> ok, lines (per-system > sys_limit and whole > total_limit)
local json = require("tools.json")

local perf = {}

perf.BASELINE = "tests/bench-baseline.json"
-- Fixed measurement world: seed 42, level 3, one year after spin-up, 192 ticks.
perf.CONFIG = { seed = 42, level = 3, warm_years = 1, ticks = 192, step = 100 }
perf.LIMITS = { system = 0.30, total = 0.15, min_abs = 2000 }

local function jit_off()
  local J = rawget(_G, "jit")
  if not J then return nil end
  local was = J.status()
  J.off() -- lint:allow luajit-only
  J.flush() -- lint:allow luajit-only
  return was
end

local function jit_restore(was)
  local J = rawget(_G, "jit")
  if J and was then J.on() end -- lint:allow luajit-only
end

function perf.measure(cfg)
  cfg = cfg or perf.CONFIG
  local worldgen = require("sim.worldgen.init")
  local world = require("sim.world")
  local W = worldgen.generate(cfg.seed, { level = cfg.level })
  world.run_years(W, cfg.warm_years)
  local was = jit_off()
  local STEP = cfg.step
  local cnt = 0
  W.profile = { clock = function() return cnt * STEP end, ms = {} }
  debug.sethook(function() cnt = cnt + 1 end, "", STEP)
  local a = cnt
  for _ = 1, cfg.ticks do world.tick(W) end
  local total = (cnt - a) * STEP / cfg.ticks
  debug.sethook()
  jit_restore(was)
  local res = { total = math.floor(total + 0.5), systems = {}, order = {} }
  local sum = 0
  for _, s in ipairs(world.systems) do
    local v = math.floor((W.profile.ms[s.name] or 0) / cfg.ticks + 0.5)
    res.systems[s.name] = v
    res.order[#res.order + 1] = s.name
    sum = sum + v
  end
  res.other = res.total - sum
  W.profile = nil
  return res
end

function perf.load_baseline(path)
  local f = io.open(path or perf.BASELINE, "rb")
  if not f then return nil end
  local s = f:read("*a")
  f:close()
  return json.decode(s)
end

function perf.write_baseline(path, res, cfg, note)
  local doc = {
    what = "P07 perf baseline: Lua VM instructions per tick (LuaJIT interpreter, JIT off) on a fixed world",
    config = cfg or perf.CONFIG,
    instructions_per_tick = res.total,
    systems = res.systems,
    other = res.other,
    limits = perf.LIMITS,
    date = note and note.date or "",
    calibration = note and note.calibration or "",
    update = "npm run bench -- --update-baseline  (intentional changes only; log them in DECISIONS.md)",
  }
  local f = assert(io.open(path or perf.BASELINE, "wb"))
  f:write(json.encode(doc))
  f:close()
  return doc
end

-- Regressions: a system > LIMITS.system above its baseline (and by > min_abs instructions, so
-- tiny systems don't trip on a few lines), or the whole tick > LIMITS.total.
function perf.compare(res, base)
  local lim = base.limits or perf.LIMITS
  local lines, ok = {}, true
  for name, b in pairs(base.systems) do
    local v = res.systems[name] or 0
    local d = v - b
    if b > 0 and d > lim.system * b and d > lim.min_abs then
      ok = false
      lines[#lines + 1] = string.format("%s %d -> %d (+%.0f%%)", name, b, v, 100 * d / b)
    end
  end
  for name, v in pairs(res.systems) do
    if base.systems[name] == nil and v > lim.min_abs then
      ok = false
      lines[#lines + 1] = string.format("%s new: %d (not in baseline)", name, v)
    end
  end
  local bt = base.instructions_per_tick
  if res.total > bt * (1 + lim.total) then
    ok = false
    lines[#lines + 1] = string.format("whole tick %d -> %d (+%.0f%%)", bt, res.total, 100 * (res.total - bt) / bt)
  end
  table.sort(lines)
  return ok, lines
end

return perf
