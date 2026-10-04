-- What a god power caused (S23, D-182; docs/v3/77_CHAIN_REACTION.md): cast one power in a made state, live on,
-- then print the intervention's chain as the player sees it (sim/chain.lua: real causal links only) - its
-- consequences as a tree, with counts and what is still unfolding. Acceptance: the same power in different
-- states must give different chains.
--   luajit tools/v3_chain.lua --case NAME [--hours H] [--every H] [--load F]
-- cases: lightning_dry | rain_drought | rain_wet | cull_wolves | cull_deer | strain | stir | cold
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave", case = "lightning_dry" }
local i = 1
while arg[i] do
  local k = arg[i]:match("^%-%-(.+)$")
  if k and arg[i + 1] then i = i + 1; args[k] = tonumber(arg[i]) or arg[i] end
  i = i + 1
end
local world = require("sim.world")
local worlds = require("sim.worlds")
local clock = require("sim.agents.clock")
local chain = require("sim.chain")
local f = assert(io.open(args.load, "rb"))
local W = assert(worlds.from_save_string(f:read("*a")))
f:close()
W.record_events = true
world.set_systems(require("sim.agents.worldgen").systems())
local V = W.S.v3
local function S(k) return V.settlements[k] end
local function goto_local(s, d, lh)
  local t = W.hour + d * 24
  local h = clock.local_hour(t, W.grid.lon[s.cell])
  t = t + ((lh - h) % 24)
  world.advance(W, t)
end
local function cast(p)
  world.command(W, "power", p)
  world.advance(W, (V.now or W.hour) + 0.01)
end
local powers = require("sim.powers")
local cfg = require("sim.config")
local max = math.max

local CASES = {
  lightning_dry = function()
    local s = S(1); goto_local(s, 2, 14)
    W.F.soil_water[s.cell] = 0.12
    local b
    for _, id in ipairs(s.blist) do local x = s.buildings[id]; if x.kind == "house" and x.state == "standing" then b = x; break end end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = b.cx, y = b.cy }) end, 48
  end,
  lightning_tree = function()
    local s = S(1); goto_local(s, 2, 14)
    W.F.soil_water[s.cell] = 0.12
    local T = s.trees
    local best
    for k = 1, T.n do if T.st[k] == 1 and (T.size[k] or 0) > 0.85 and math.abs(T.x[k]) + math.abs(T.y[k]) > 0.8 then best = k; break end end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = T.x[best], y = T.y[best] }) end, 72
  end,
  lightning_village = function()
    -- (a tree close to the houses on a dry, hot afternoon; --day D picks the day, --sett K the village)
    local s = S(args.sett or 1); goto_local(s, args.day or 2, 15)
    W.F.soil_water[s.cell] = 0.05
    local T = s.trees
    local best, bd = nil, 1e9
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "house" and b.state == "standing" then
        for k = 1, T.n do
          if T.st[k] == 1 and (T.size[k] or 0) > 0.7 then
            local d = (T.x[k] - b.cx) ^ 2 + (T.y[k] - b.cy) ^ 2
            if d > 0.0016 and d < bd then best, bd = k, d end
          end
        end
      end
    end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = T.x[best], y = T.y[best] }) end, 96
  end,
  rain_drought = function()
    local s = S(args.sett or 5); goto_local(s, args.day or 2, 10)
    for _, c in ipairs(powers.area(W, s.cell, cfg.POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = 0.1 end end
    return s, function() cast({ power = "rain", cell = s.cell }) end, 96
  end,
  rain_wet = function()
    local s = S(args.sett or 6); goto_local(s, args.day or 2, 10)
    for _, c in ipairs(powers.area(W, s.cell, cfg.POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = max(W.F.soil_water[c], 0.5) end end
    return s, function() cast({ power = "rain", cell = s.cell }) end, 120
  end,
  cull_wolves = function()
    local s = S(4); goto_local(s, 2, 10)
    return s, function() cast({ power = "cull", cell = s.cell, species = "wolves" }) end, 24 * 6
  end,
  cull_deer = function()
    local s = S(4); goto_local(s, 2, 10)
    return s, function() cast({ power = "cull", cell = s.cell, species = "deer" }) end, 24 * 6
  end,
  strain = function()
    local s = S(1); goto_local(s, 2, 2)
    return s, function() cast({ power = "strain", cell = s.cell }); cast({ power = "strain", cell = s.cell }) end, 48
  end,
  stir = function()
    local s = S(1); goto_local(s, 2, 10)
    return s, function() cast({ power = "stir", cell = s.cell }); cast({ power = "stir", cell = s.cell }) end, 96
  end,
  cold = function()
    local s = S(1); goto_local(s, 4, 10)
    return s, function() cast({ power = "cold", cell = s.cell }) end, 96
  end,
}


local s, go, hours = CASES[args.case]()
hours = args.hours or hours
local every = args.every or 1
chain.update(W, V.now or W.hour)
go()
local t0 = V.now or W.hour
for h = every, hours, every do
  world.advance(W, t0 + h)
  chain.update(W, V.now or W.hour)
end
local function show(I)
  print(string.format("== %s  [%s] day %.1f: %d consequences (%d direct)%s", I.title, I.power, I.t / 24, I.n_total, I.n_direct, I.active and ", still unfolding" or ""))
  for _, row in ipairs(chain.rows(W, I, 120)) do
    local nd = row.node
    print(string.format("  %s%s %s%s%s", string.rep("  ", row.depth - 1), row.depth > 1 and "L" or "-", nd.title or nd.kind,
      nd.line and (" - " .. nd.line) or "", nd.active and "  *" or ""))
  end
end
print(string.format("-- %s, %d h after the cast", args.case, hours))
for _, I in ipairs(chain.list(W)) do show(I) end
