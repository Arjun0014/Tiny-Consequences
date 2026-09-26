-- Headless test runner: unit tests, property tests, scenario tests.
-- Usage (from repo root): luajit tests/sim/run.lua [--full] [--chapter N] [--only PATTERN]
--                         [--unit] [--props] [--scenarios] [--seeds N]
package.path = "./?.lua;./?/init.lua;" .. package.path
-- LuaJIT: the whole sim exceeds the default trace cache, which then flushes and re-records all
-- the time (3–4× slower). Plain Lua 5.1 ignores this.
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only

local T = require("tests.sim.lib")

local args = { chapter = 1 }
local i = 1
while arg and arg[i] do
  local a = arg[i]
  if a == "--full" then args.full = true
  elseif a == "--chapter" then i = i + 1; args.chapter = tonumber(arg[i])
  elseif a == "--only" then i = i + 1; args.only = arg[i]
  elseif a == "--unit" then args.unit = true
  elseif a == "--props" then args.props = true
  elseif a == "--scenarios" then args.scenarios = true
  elseif a == "--seeds" then i = i + 1; args.seeds = tonumber(arg[i])
  elseif a == "--verbose" or a == "-v" then args.verbose = true
  end
  i = i + 1
end
if not (args.unit or args.props or args.scenarios) then
  args.unit, args.props, args.scenarios = true, true, true
end
T.args = args

local groups = {}
if args.unit then groups[#groups + 1] = { "unit", require("tests.sim.unit.index") } end
if args.props then groups[#groups + 1] = { "props", require("tests.sim.props.index") } end
if args.scenarios then groups[#groups + 1] = { "scenarios", require("tests.sim.scenarios.index") } end

local t0 = os.clock()
for _, g in ipairs(groups) do
  for _, modname in ipairs(g[2]) do
    local ok, mod = pcall(require, modname)
    if not ok then
      T.record(modname, false, "load error: " .. tostring(mod))
    else
      if type(mod) == "table" and mod.expect then
        -- scenario spec (pure data) -> test module
        mod = require("tests.sim.scenarios.lib").as_module(mod, args)
      end
      T.run_module(modname, mod)
    end
  end
end
local ok = T.summary(os.clock() - t0)
os.exit(ok and 0 or 1)
