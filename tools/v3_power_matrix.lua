-- The god powers played in different states (S22): each case loads the world, makes a state (a drought, a
-- wet floodplain, a fire burning, a cold snap, the harvest ...), casts a power through the same `power`
-- command the game sends, lives on, and reports what the world did (deeds, deaths, beasts lost, the
-- ground's water, the air, snow, the river) and what the news shows (the top cards and their roots).
-- The same power in different states must give different stories; a power whose consequences never reach
-- the news, or reach it without YOU at the root, is a finding.
--   luajit tools/v3_power_matrix.lua [--only NAME] [--load F]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave" }
local i = 1
while arg[i] do
  local k = arg[i]:match("^%-%-(.+)$")
  if k and arg[i + 1] then i = i + 1; args[k] = tonumber(arg[i]) or arg[i] end
  i = i + 1
end
local world = require("sim.world")
local worlds = require("sim.worlds")
local clock = require("sim.agents.clock")
local news = require("sim.news")
local f = assert(io.open(args.load, "rb"))
local DATA = f:read("*a")
f:close()
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt

local function fresh()
  local W = assert(worlds.from_save_string(DATA))
  W.record_events = true
  return W
end
-- to local hour lh of day d at settlement s
local function goto_local(W, s, d, lh)
  local t = W.hour + d * 24
  local h = clock.local_hour(t, W.grid.lon[s.cell])
  t = t + ((lh - h) % 24)
  world.advance(W, t)
  return t
end
local function cast(W, p)
  world.command(W, "power", p)
  world.advance(W, (W.S.v3.now or W.hour) + 0.01)
end
local function state(W, s)
  local V = W.S.v3
  local F = W.F
  local hydro = W.S.hydro
  local flow = hydro and hydro.flow and hydro.flow[s.cell] or 0
  local mean = hydro and hydro.flow_mean and hydro.flow_mean[s.cell] or 1
  local burning = 0
  for _, b in ipairs(s.burn or {}) do if not b.out then burning = burning + 1 end end
  local alive, beasts = 0, 0
  for _, id in ipairs(s.members) do if V.people[id] and V.people[id].alive then alive = alive + 1 end end
  for _, kind in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do if a.alive then beasts = beasts + 1 end end
  end
  local wild, wolves = 0, 0
  for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do if w.alive then if w.pred then wolves = wolves + 1 else wild = wild + 1 end end end
  return string.format("soil %.2f temp %.1f snow %.2f river x%.1f flood %s burning %d people %d beasts %d wild %d wolves %d",
    F.soil_water[s.cell], F.temp[s.cell], s.snow or 0, flow / max(1e-6, mean), s.fl and "yes" or "no", burning, alive, beasts, wild, wolves)
end
local function deeds_since(W, d0)
  local D = W.S.v3.deeds
  local c = {}
  for k = d0 + 1, #D do
    local d = D[k]
    local key = d.kind .. (d.what and (":" .. tostring(d.what)) or "")
    c[key] = (c[key] or 0) + 1
  end
  local ks = {}
  for k, n in pairs(c) do ks[#ks + 1] = k .. " " .. n end
  table.sort(ks)
  return table.concat(ks, ", ")
end
local function report(W, name, before, s, d0, hours)
  news.update(W, W.S.v3.now or W.hour)
  print(string.format("== %s (+%d h)", name, hours))
  print("   before: " .. before)
  print("   after:  " .. state(W, s))
  print("   deeds:  " .. deeds_since(W, d0))
  local cards = news.cards(W, 5)
  for _, st in ipairs(cards) do
    local ch = news.chain(W, st)
    local why = {}
    for _, l in ipairs(ch.links) do why[#why + 1] = l.text end
    print(string.format("   card [%d] %-44s | %s | root %s | %s", st.level, st.title or "?", st.line or "", ch.root, table.concat(why, " <- ")))
  end
end

local CASES = {}
local function case(name, fn) CASES[#CASES + 1] = { name = name, fn = fn } end
local function S(W, k) return W.S.v3.settlements[k] end
local function run_for(W, h) world.advance(W, (W.S.v3.now or W.hour) + h) end

-- LIGHTNING ------------------------------------------------------------------------------------
local function lightning_at(W, s, x, y) cast(W, { power = "lightning", cell = s.cell, sid = s.id, x = x, y = y }) end
case("lightning: a dry tree in a drought", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 14)
  W.F.soil_water[s.cell] = 0.12
  local T = s.trees
  local best
  for k = 1, T.n do if T.st[k] == 1 and (T.size[k] or 0) > 0.85 and math.abs(T.x[k]) + math.abs(T.y[k]) > 0.8 then best = k; break end end
  return s, function() lightning_at(W, s, T.x[best], T.y[best]) end, 6
end)
case("lightning: the same tree after rain (wet)", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 14)
  W.F.soil_water[s.cell] = 0.62
  local T = s.trees
  local best
  for k = 1, T.n do if T.st[k] == 1 and (T.size[k] or 0) > 0.85 and math.abs(T.x[k]) + math.abs(T.y[k]) > 0.8 then best = k; break end end
  return s, function() lightning_at(W, s, T.x[best], T.y[best]) end, 6
end)
case("lightning: a roof on a dry afternoon", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 15)
  W.F.soil_water[s.cell] = 0.2
  local b
  for _, id in ipairs(s.blist) do local x = s.buildings[id]; if x.kind == "house" and x.state == "standing" then b = x; break end end
  return s, function() lightning_at(W, s, b.cx, b.cy) end, 8
end)
case("lightning: empty open ground", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 13)
  -- (the open ground farthest from anything tall within the map's middle)
  local blaze = require("sim.agents.blaze")
  local bx, by, bd = 0, 0, -1
  for gx = -1.2, 1.2, 0.1 do
    for gy = -1.2, 1.2, 0.1 do
      local near = 1
      blaze.trees_near(s, gx, gy, 0.2, function(_, d2) near = min(near, sqrt(d2)) end)
      for _, id in ipairs(s.blist) do local b = s.buildings[id]; near = min(near, sqrt((b.cx - gx) ^ 2 + (b.cy - gy) ^ 2)) end
      local ti = require("sim.agents.map").tile_of(s, gx, gy)
      if ti > 0 and s.land[ti] == 1 and near > bd then bx, by, bd = gx, gy, near end
    end
  end
  return s, function() lightning_at(W, s, bx, by) end, 3
end)

-- RAIN -----------------------------------------------------------------------------------------
case("rain: on a drought", function(W)
  local s = S(W, 5); goto_local(W, s, 2, 10)
  local powers = require("sim.powers")
  for _, c in ipairs(powers.area(W, s.cell, require("sim.config").POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = 0.1 end end
  return s, function() cast(W, { power = "rain", cell = s.cell }) end, 72
end)
case("rain: on wet hills over a floodplain", function(W)
  local s = S(W, 6); goto_local(W, s, 2, 10)
  local powers = require("sim.powers")
  for _, c in ipairs(powers.area(W, s.cell, require("sim.config").POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = max(W.F.soil_water[c], 0.5) end end
  return s, function() cast(W, { power = "rain", cell = s.cell }) end, 96
end)
case("rain: on a fire burning in a drought", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 11)
  require("sim.agents.premise").named(W, "pasture_fire")
  return s, function() cast(W, { power = "rain", cell = s.cell }) end, 8
end)
case("rain: the same fire, no rain (control)", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 11)
  require("sim.agents.premise").named(W, "pasture_fire")
  return s, function() end, 8
end)
case("rain: in a cold snap", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 10)
  require("sim.agents.premise").named(W, "cold_snap")
  return s, function() cast(W, { power = "rain", cell = s.cell }) end, 48
end)

-- WARM / COLD AIR ------------------------------------------------------------------------------
case("cold air: growing season", function(W)
  local s = S(W, 1); goto_local(W, s, 4, 10)
  return s, function() cast(W, { power = "cold", cell = s.cell }) end, 96
end)
case("warm air: into a cold snap with snow lying", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 10)
  require("sim.agents.premise").named(W, "cold_snap")
  run_for(W, 48)
  return s, function() cast(W, { power = "warm", cell = s.cell }) end, 72
end)
case("warm air: a hot village in summer", function(W)
  local s = S(W, 5); goto_local(W, s, 18, 10)
  return s, function() cast(W, { power = "warm", cell = s.cell }) end, 96
end)

-- STIR / STRAIN --------------------------------------------------------------------------------
case("strain the fault: under Tekheim (near people)", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 2)
  return s, function() cast(W, { power = "strain", cell = s.cell }); cast(W, { power = "strain", cell = s.cell }) end, 36
end)
case("strain the fault: far from anyone", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 2)
  -- (the boundary cell farthest from every village)
  local shake = require("sim.agents.shake")
  local Q = shake.state(W)
  local g = W.grid
  local best, bd = nil, -1
  for q = 1, #Q.cells do
    local c = Q.cells[q]
    local near = -2
    for _, o in ipairs(W.S.v3.settlements) do near = max(near, g.X[c] * g.X[o.cell] + g.Y[c] * g.Y[o.cell] + g.Z[c] * g.Z[o.cell]) end
    if -near > bd then best, bd = c, -near end
  end
  return s, function() cast(W, { power = "strain", cell = best }); cast(W, { power = "strain", cell = best }) end, 36
end)
case("stir the mountain nearest Tekheim", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 10)
  return s, function() cast(W, { power = "stir", cell = s.cell }); cast(W, { power = "stir", cell = s.cell }) end, 72
end)

-- CULL / BRING ---------------------------------------------------------------------------------
case("cull wolves about Hottoodingle Hop", function(W)
  local s = S(W, 4); goto_local(W, s, 2, 10)
  return s, function() cast(W, { power = "cull", cell = s.cell, species = "wolves" }) end, 24 * 6
end)
case("cull deer about Hottoodingle Hop", function(W)
  local s = S(W, 4); goto_local(W, s, 2, 10)
  return s, function() cast(W, { power = "cull", cell = s.cell, species = "deer" }) end, 24 * 6
end)
case("bring wolves to Tekheim", function(W)
  local s = S(W, 1); goto_local(W, s, 2, 10)
  return s, function() cast(W, { power = "bring", cell = s.cell, species = "wolves" }) end, 24 * 6
end)

for _, cs in ipairs(CASES) do
  if not args.only or cs.name:find(args.only, 1, true) then
    local W = fresh()
    local ok, err = pcall(function()
      local s, go, hours = cs.fn(W)
      local before = state(W, s)
      news.update(W, W.S.v3.now or W.hour)
      local d0 = #W.S.v3.deeds
      go()
      run_for(W, hours)
      report(W, cs.name, before, s, d0, hours)
    end)
    if not ok then print("== " .. cs.name .. ": ERROR " .. tostring(err)) end
  end
end
