-- Village diagnostics: month-by-month economy of villages, plus the chains of their events.
-- npm run sim:villages -- [--world default | --seed N] [--years Y] [--village NAME|ID|all]
--   [--from YEAR] [--summary]
-- Prints one row per village-month: population (adults/children), jobs, food flows (harvest,
-- catch, meat, need), store, food security (fs, fs_low), food ratio rho, farms (count, mean
-- fertility, soil water, crop), famine/low/chronic counters and the village cause tag. After the
-- run, every village event with its chain. --summary prints yearly founding/abandonment churn
-- and headline counts instead of monthly rows.
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local worlds = require("sim.worlds")
local events = require("sim.causality.events")
local calendar = require("sim.calendar")

local args = { years = 10, village = "all", from = 0 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--world" then i = i + 1; args.world = arg[i]
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif a == "--village" then i = i + 1; args.village = arg[i]
  elseif a == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif a == "--summary" then args.summary = true
  end
  i = i + 1
end

local W
if args.seed then
  W = worlds.generate(args.seed)
else
  local f = assert(io.open("assets/worlds/" .. (args.world or "default") .. ".tcsave", "rb"))
  local s = f:read("*a"); f:close()
  local err
  W, err = worlds.from_save_string(s)
  assert(W, err)
end
local t_start = W.tick

local function wanted(v)
  if args.village == "all" then return true end
  return v.name == args.village or tostring(v.id) == args.village
end

local function farm_stats(v)
  local F = W.F
  local n, fe, sw, cr = #v.farms, 0, 0, 0
  for k = 1, n do
    local c = v.farms[k]
    fe = fe + F.fertility[c]; sw = sw + F.soil_water[c]; cr = cr + F.crop[c]
  end
  if n > 0 then fe, sw, cr = fe / n, sw / n, cr / n end
  return n, fe, sw, cr
end

local function jobs(v)
  local P = W.ent.people
  local a, ch, fa, fi, hu, el = 0, 0, 0, 0, 0, 0
  for k = 1, #v.members do
    local j = P[v.members[k]].job
    if j == "child" then ch = ch + 1 else a = a + 1 end
    if j == "farmer" then fa = fa + 1 elseif j == "fisher" then fi = fi + 1
    elseif j == "woodcutter" then hu = hu + 1 elseif j == "elder" then el = el + 1 end
  end
  return a, ch, fa, fi, hu, el
end

if not args.summary then
  print("label              village        pop  ad ch  fa fi hu el   need  harv  catch  meat   store    fs  fslo   rho  farms fert  soil  crop  fam low chr cause")
end
local last_harvest = {}
local yearly = {}
for y = 1, args.years do
  local yr = { founded = 0, abandoned = 0, famine = 0, headlines = 0, crash = 0 }
  local e0 = W.E.next_id
  for _ = 1, 48 do
    world.tick(W)
    local vs = W.ent.villages
    for vi = 1, #vs do
      local v = vs[vi]
      -- the village updated on this tick (month() runs on (tick + id) % 4 == 0, before the increment)
      if wanted(v) and (v.alive or v.abandoned == W.tick - 1) and ((W.tick - 1) + vi) % 4 == 0
        and not args.summary and y > args.from then
        local a, ch, fa, fi, hu, el = jobs(v)
        local need = a * 1.0 + ch * 0.6
        local harv = 0
        if v.harvest_year ~= last_harvest[vi] and v.last_harvest then
          harv = v.last_harvest
          last_harvest[vi] = v.harvest_year
        end
        local n, fe, sw, cr = farm_stats(v)
        print(string.format("%-18s %-13s %4d %3d %2d %3d %2d %2d %2d %6.1f %5.0f %6.1f %5.1f %7.1f %5.2f %5.2f %5.2f %5d %5.2f %5.2f %5.2f  %3s %3d %3d %d",
          calendar.label(W.tick - 1), v.name:sub(1, 13), v.pop, a, ch, fa, fi, hu, el, need, harv,
          v.catch or 0, v.meat or 0, v.food, v.food_security, v.fs_low or 0, v.food_ratio or 0,
          n, fe, sw, cr, v.famine and "F" or "-", v.low_months, v.chronic or 0, v.cause))
      end
    end
  end
  for id = e0, W.E.next_id - 1 do
    local r = W.E.rec[id]
    if r then
      if r.kind == "village_founded" then yr.founded = yr.founded + 1 end
      if r.kind == "village_abandoned" then yr.abandoned = yr.abandoned + 1 end
      if r.kind == "famine" then yr.famine = yr.famine + 1 end
      if r.kind == "population_crash" then yr.crash = yr.crash + 1 end
      if r.headline then yr.headlines = yr.headlines + 1 end
    end
  end
  local pop, nv = 0, 0
  for vi = 1, #W.ent.villages do
    local v = W.ent.villages[vi]
    if v.alive then pop = pop + v.pop; nv = nv + 1 end
  end
  yr.pop, yr.nv = pop, nv
  yearly[y] = yr
end

if args.summary then
  local tf, ta, tfam, th = 0, 0, 0, 0
  for y = 1, args.years do
    local r = yearly[y]
    tf, ta, tfam, th = tf + r.founded, ta + r.abandoned, tfam + r.famine, th + r.headlines
    if r.founded + r.abandoned + r.famine > 0 or y % 10 == 0 then
      print(string.format("Y%-4d pop %4d villages %2d  +%d founded  -%d abandoned  %d famines  %d crashes  %d headlines",
        y, r.pop, r.nv, r.founded, r.abandoned, r.famine, r.crash, r.headlines))
    end
  end
  print(string.format("total: %d founded, %d abandoned, %d famines, %d headlines (%.2f/y)", tf, ta, tfam, th, th / args.years))
end

-- chains of village events
print("")
print("village events and chains:")
for k = 1, #W.E.order do
  local r = W.E.rec[W.E.order[k]]
  if r and r.tick >= t_start and (r.kind == "famine" or r.kind == "village_abandoned" or r.kind == "village_founded"
    or r.kind == "migration" or r.kind == "crop_failure" or r.kind == "deaths_spike") then
    local d = r.data
    local v = type(d) == "table" and W.ent.villages[d.village or d.from or 0]
    if (v and wanted(v)) or args.village == "all" then
      local ch = events.chain(W, r.id)
      local parts = {}
      for j = 1, #ch do
        local e = ch[j]
        local where = ""
        local dd = e.data
        if type(dd) == "table" and (dd.village or dd.from) and W.ent.villages[dd.village or dd.from] then
          where = " @" .. W.ent.villages[dd.village or dd.from].name
        elseif W.names.regions and W.names.regions[e.region] then where = " @" .. W.names.regions[e.region].name end
        parts[#parts + 1] = string.format("%s %s%s(%s)", calendar.label(e.tick), e.kind, where,
          e.magnitude and string.format("%.3g", e.magnitude) or "")
      end
      print(string.format("#%d %s", r.id, table.concat(parts, "  ->  ")))
    end
  end
end
