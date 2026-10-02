-- Fairs (S19, D-144): after its harvest a village at the middle of its road neighbours calls a fair;
-- the news travels with people; households of several villages send people with goods, who walk
-- there, barter at the square, hear the news and walk home. Nothing forced: the shipped world, as it is.
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")

local M = {}

local function shipped()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

function M.a_fair_after_the_harvest()
  local W = shipped()
  local V = W.S.v3
  local t0 = W.hour
  local host, fair = nil, nil
  local came, walked, at_fair = {}, {}, {}
  local deaths0 = 0
  for _, p in pairs(V.people) do if not p.alive and p.died_of == "hunger" then deaths0 = deaths0 + 1 end end
  for h = 1, 24 * 20 do
    world.advance(W, t0 + h)
    for _, s in ipairs(V.settlements) do
      if s.fair and not host then host, fair = s, s.fair end
    end
    if host and fair then
      for _, id in ipairs(V.order) do
        local p = V.people[id]
        if p and p.alive and p.trip and p.trip.fair == host.id then
          if p.act == "travel" then walked[p.sett] = true end
          if p.act == "fair" and (p.at or p.sett) == host.id then at_fair[p.id] = true; came[p.sett] = true end
        end
      end
      if (t0 + h) > fair.t1 + 60 then break end
    end
  end
  T.ok(host ~= nil, "a fair was called")
  if not host then return end
  local villages, others = 0, 0
  for sid in pairs(came) do villages = villages + 1; if sid ~= host.id then others = others + 1 end end
  T.ok(others >= 1, "people of other villages came to the fair at " .. host.name .. ": " .. others)
  local walkers = 0
  for sid in pairs(walked) do if sid ~= host.id then walkers = walkers + 1 end end
  T.ok(walkers >= 1, "they walked the road to it")
  local n = 0
  for _ in pairs(at_fair) do n = n + 1 end
  T.ok(n >= 6, "a crowd at the fair: " .. n)
  T.ok((host.acc.fair_deals or 0) > 0, "they bartered at the fair: " .. tostring(host.acc.fair_deals))
  -- the news went round: the villages that came know each other afresh
  for sid in pairs(came) do
    local s = V.settlements[sid]
    if s ~= host then T.ok(s.know and s.know[host.id] and s.know[host.id].t >= fair.t0, s.name .. " heard how " .. host.name .. " stands") end
  end
  -- everyone home again after the fair
  for _, id in ipairs(V.order) do
    local p = V.people[id]
    if p and p.alive then
      T.ok(not (p.trip and p.trip.fair), p.name .. " is home from the fair")
    end
  end
  local deaths = 0
  for _, p in pairs(V.people) do if not p.alive and p.died_of == "hunger" then deaths = deaths + 1 end end
  T.eq(deaths, deaths0, "nobody starved going to the fair")
  return string.format("the fair at %s: %d people of %d villages, %d barters, %d matches", host.name, n, villages, host.acc.fair_deals or 0,
    host.acc.fair_matches or 0)
end

return M
