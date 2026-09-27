-- Yearly ledger of every village's real flows (v.acc_last, rolled over at each year boundary):
-- demography (births, deaths by cause, marriages, migrants in/out), food (harvest, catch, forage,
-- meat, eaten vs need, spoilage), land and sea (fields, sown, boats, trips, catch per trip), wood
-- (cut, brush, burned, used in building) and housing (homes, sites, couples waiting).
--   luajit tools/society_ledger.lua [--seed 22] [--years 30] [--village N] [--from Y] [--spinup]
--     --spinup: also print the spin-up years after the worldgen settle (negative years)
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only

local args = { seed = 22, years = 30, from = 0 }
local i = 1
while arg and arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif a == "--village" then i = i + 1; args.village = tonumber(arg[i])
  elseif a == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif a == "--spinup" then args.spinup = true
  elseif a == "--summary" then args.summary = true end
  i = i + 1
end

local worlds = require("sim.worlds")
local world = require("sim.world")
local construction = require("sim.society.construction")

local config = require("sim.config")
local W
local function r(x) return math.floor((x or 0) + 0.5) end
print(string.format("seed %d", args.seed))
local report
local last_s = {}
report = function(y)
  if args.summary then
    if y % 10 ~= 0 then return end
    local S = W.S.society
    local alive, pop = 0, 0
    for _, v in ipairs(W.ent.villages) do if v.alive then alive = alive + 1; pop = pop + v.pop end end
    local function d(k) local x = (S[k] or 0) - (last_s[k] or 0); last_s[k] = S[k]; return x end
    print(string.format("y%4d villages %2d people %4d | decade: births %4d deaths %4d built %4d migrated %4d founded %2d abandoned %2d",
      y, alive, pop, d("births"), d("deaths"), d("built"), d("migrated"), d("founded"), d("abandoned")))
    return
  end
    local alive, pop = 0, 0
  for _, v in ipairs(W.ent.villages) do if v.alive then alive = alive + 1; pop = pop + v.pop end end
  print(string.format("=== year %d: %d villages, %d people, CO2 %.0f", y, alive, pop, W.G.co2))
  for _, v in ipairs(W.ent.villages) do
    local a = v.acc_last
    if a and (v.alive or (v.abandoned and W.hour - v.abandoned < 8064)) and (not args.village or v.id == args.village) then
      local P = W.ent.people
      local jobs = {}
      for k = 1, #v.members do local j = P[v.members[k]].job; jobs[j] = (jobs[j] or 0) + 1 end
      local waiting = 0
      for k = 1, #v.members do local p = P[v.members[k]]; if p.betrothed ~= 0 and p.sex == "f" then waiting = waiting + 1 end end
      local homes = construction.count_kind(W, v, "house", "standing") + construction.count_kind(W, v, "hut", "standing")
      local deaths = {}
      for k, x in pairs(a) do if k:sub(1, 2) == "d_" then deaths[#deaths + 1] = k:sub(3) .. " " .. x end end
      table.sort(deaths)
      local trips = a.trips or 0
      print(string.format("  %2d %-13s %s pop %3d hh %2d homes %2d wait %2d | born %2d died %2d (%s) wed %2d in %2d out %2d | cap %4.0f",
        v.id, v.name:sub(1, 13), v.alive and " " or "X", v.pop, #v.households, homes, waiting, a.births or 0, a.deaths or 0,
        table.concat(deaths, ","), a.marriages or 0, a.mig_in or 0, a.mig_out or 0, v.capacity or 0))
      print(string.format("     food: need %6d eaten %6d (%.2f) | harvest %6d sown %4.1f/%2d fields | catch %6d %4d trips (%4.1f/trip) boats %2d | forage %5d meat %4d | spoil g %5d f %4d | grain %6d seed %5d",
        r(a.need), r(a.eaten), (a.need or 0) > 0 and (a.eaten or 0) / a.need or 1, r(a.harvest), a.sown or 0, #v.farms,
        r(a.catch), trips, trips > 0 and (a.catch or 0) / trips or 0, #v.boats, r(a.forage), r(a.meat), r(a.spoil_g), r(a.spoil_f),
        r(v.stock.grain), r(v.stock.seed)))
      print(string.format("     wood: cut %5d brush %5d burned %5d stock %5d | hours sow %5d tend %5d harv %5d clear %5d fuel %5d timber %5d build %5d fish %s forage %5d hunt %5d | F %d W %d B %d fishers %d",
        r(a.wood_cut), r(a.brush), r(a.burned), r(v.stock.wood), r(a.h_sow), r(a.h_tend), r(a.h_harvest), r(a.h_clear),
        r(a.h_fuel), r(a.h_timber), r(a.h_build), "-", r(a.h_forage), r(a.h_hunt), jobs.farmer or 0, jobs.woodcutter or 0,
        jobs.builder or 0, jobs.fisher or 0))
    end
  end
end
local first = 1
if args.spinup then
  -- the worldgen spin-up, step by step (sim/worldgen/init.lua), then Year 0 onward
  local worldgen = require("sim.worldgen.init")
  local climate_init = require("sim.worldgen.climate_init")
  local settle = require("sim.worldgen.settle")
  W = worldgen.generate(args.seed, { no_spinup = true })
  W.record_events = false
  W.G.calibrating = true
  local ticks = config.SPINUP_YEARS * 48
  for t = 1, ticks do
    world.tick(W)
    if t == config.SPINUP_VEG_YEAR * 48 then
      climate_init.vegetation(W)
      climate_init.fauna(W)
      require("sim.systems.biome").reset(W)
    end
    if t == config.SETTLE.year * 48 then settle.run(W) end
    if t > config.SETTLE.year * 48 and t % 48 == 0 then report(t / 48 - config.SPINUP_YEARS) end
  end
  W.G.calibrating = false
  W.record_events = true
  world.shift_ticks(W, -W.tick)
else
  W = worlds.generate(args.seed)
end
for y = first, args.years do
  world.run_ticks(W, 48)
  if y >= args.from then report(y) end
end


