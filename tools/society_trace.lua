-- Watch the settlement layer headless: per-village state every `--every` months.
--   luajit tools/society_trace.lua [--seed 22] [--years 10] [--every 6] [--village N] [--events]
--                                  [--from-default]
-- Prints population, households, homes (standing / sites), boats, fields, stocks (days of food,
-- days of fuel), the season phase, births/deaths and the labour groups, then the society events.
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only

local args = { seed = 22, years = 10, every = 6 }
local i = 1
while arg and arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i])
  elseif a == "--village" then i = i + 1; args.village = tonumber(arg[i])
  elseif a == "--events" then args.events = true
  elseif a == "--from-default" then args.default = true end
  i = i + 1
end

local worlds = require("sim.worlds")
local world = require("sim.world")
local farming = require("sim.society.farming")
local construction = require("sim.society.construction")

local t0 = os.clock()
local W = worlds.generate(args.seed)
print(string.format("seed %d generated in %.1f s; Year 0: %d villages", args.seed, os.clock() - t0, #W.ent.villages))

local function summary(v)
  local E = W.ent
  local B = E.buildings
  local houses, sites, ruins = 0, 0, 0
  for k = 1, #v.buildings do
    local b = B[v.buildings[k]]
    if b then
      if b.state == "standing" and (b.kind == "house" or b.kind == "hut") then houses = houses + 1
      elseif b.state == "site" then sites = sites + 1
      elseif b.state == "ruin" then ruins = ruins + 1 end
    end
  end
  local couples = 0
  local P = E.people
  for k = 1, #v.members do
    local p = P[v.members[k]]
    if p.betrothed ~= 0 and p.sex == "f" then couples = couples + 1 end
  end
  local groups = {}
  for k = 1, #v.plan.groups do
    local g = v.plan.groups[k]
    groups[#groups + 1] = g.kind .. g.n
  end
  local sown = 0
  for k = 1, #v.farms do sown = sown + W.S.farm[v.farms[k]].sown end
  return string.format("%-14s pop %3d hh %2d homes %2d+%d site wait %d boats %d fields %d (sown %.1f) %-8s food %4.0fd (g %5.0f f %4.0f s %4.0f) wood %4.0fd r %.2f b/d %d/%d %s",
    v.name:sub(1, 14), v.pop, #v.households, houses, sites, couples, #v.boats, #v.farms, sown, v.season.phase,
    v.food_days, v.stock.grain, v.stock.fresh, v.stock.seed, math.min(999, v.wood_days), v.ration,
    v.births_y or 0, v.deaths_y or 0, table.concat(groups, " "))
end

local months = args.years * 12
local last_births, last_deaths = {}, {}
for m = 1, months do
  world.run_ticks(W, 4)
  if m % args.every == 0 then
    print(string.format("--- Year %.1f  (people %d, villages %d)", W.hour / 8064, W.S.society.pop or 0,
      (function() local n = 0 for _, v in ipairs(W.ent.villages) do if v.alive then n = n + 1 end end return n end)()))
    for _, v in ipairs(W.ent.villages) do
      if v.alive and (not args.village or v.id == args.village) then
        v.births_y = W.S.society.births - (last_births[v.id] or W.S.society.births)
        print("  " .. summary(v))
      end
    end
  end
end
print(string.format("done in %.1f s; births %d deaths %d built %d migrated %d founded %d abandoned %d",
  os.clock() - t0, W.S.society.births, W.S.society.deaths, W.S.society.built, W.S.society.migrated,
  W.S.society.founded, W.S.society.abandoned))
if args.events then
  local E = W.E
  local SOC = { rationing = 1, seed_eaten = 1, famine = 1, crop_failure = 1, deaths_spike = 1, migration = 1,
    village_founded = 1, village_abandoned = 1, wood_shortage = 1, boat_lost = 1, flooded_village = 1 }
  for _, id in ipairs(E.order) do
    local r = E.rec[id]
    if r and SOC[r.kind] then
      local chain = {}
      local x = id
      local guard = 0
      while x and x ~= 0 and guard < 10 do
        local rr = E.rec[x]
        chain[#chain + 1] = rr and rr.kind or "?"
        x = E.parent[x]
        guard = guard + 1
      end
      local vname = r.data and r.data.village and W.ent.villages[r.data.village] and W.ent.villages[r.data.village].name or ""
      print(string.format("  y%5.1f %-16s %-14s mag %s  chain %s", (r.hour or 0) / 8064, r.kind, vname:sub(1, 14),
        tostring(r.mag), table.concat(chain, "<")))
    end
  end
end
