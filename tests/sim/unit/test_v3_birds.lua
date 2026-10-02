-- Birds of a v3 world (S14): gull flocks are agents with reasons (sim/agents/birds.lua), on the
-- shipped world over three days.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")

local M = {}

-- Every coast has its gulls; they sleep on their roost, go out at dawn, and every flight and every
-- wheel over the water has a reason in the world: a boat hauling its net when they follow one, a
-- plough at work when they follow it, the dusk or a downpour when they go home.
function M.gulls_have_reasons()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local W = assert(worlds.from_save_string(f:read("*a")))
  f:close()
  world.set_systems(require("sim.agents.worldgen").systems())
  local birds = require("sim.agents.birds")
  local clock = require("sim.agents.clock")
  local V = W.S.v3
  local t0 = W.hour
  local flocks, follow, follow_ok, plough, plough_ok, night, night_home, acts = 0, 0, 0, 0, 0, 0, 0, {}
  for t = t0 + 24, t0 + 72, 0.1 do
    world.advance(W, t)
    for _, s in ipairs(V.settlements) do
      birds.each(s, function(fl)
        acts[fl.act] = (acts[fl.act] or 0) + 1
        if fl.act == "follow" then
          follow = follow + 1
          local q = V.people[fl.tgt or 0]
          if q and q.alive and q.boat and (q.act == "fish" or q.act == "row") then follow_ok = follow_ok + 1 end
        elseif fl.act == "plough" then
          plough = plough + 1
          local q = V.people[fl.tgt or 0]
          if q and q.alive and (q.act == "till" or q.act == "turn" or q.objk == "till") then plough_ok = plough_ok + 1 end
        end
        local lh = clock.local_hour(t, W.grid.lon[s.cell])
        if lh > (s.sunset or 18) + 1.5 or lh < (s.sunrise or 6) - 1.5 then
          night = night + 1
          local x, y = birds.pos(fl, t)
          if (x - fl.rx) ^ 2 + (y - fl.ry) ^ 2 < 0.05 ^ 2 then night_home = night_home + 1 end
        end
      end)
    end
  end
  for _, s in ipairs(V.settlements) do if s.birds and #s.birds.flocks > 0 then flocks = flocks + 1 end end
  T.ok(flocks >= 3, "settlements with gulls " .. flocks)
  T.ok(follow > 0, "time following boats " .. follow)
  T.ok(follow_ok >= 0.9 * follow, string.format("following a boat that is out: %d of %d", follow_ok, follow))
  T.ok(plough == 0 or plough_ok >= 0.9 * plough, string.format("following a plough at work: %d of %d", plough_ok, plough))
  T.ok(night > 0 and night_home >= 0.95 * night, string.format("nights at the roost: %d of %d", night_home, night))
  T.ok((acts.forage or 0) > 0 and (acts.raft or 0) > 0 and (acts.fly or 0) > 0, "they forage, rest and fly")
end

return M
