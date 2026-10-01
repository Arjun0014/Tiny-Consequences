-- Children's days by age band (D-109): luajit tools/v3_kids.lua  (30 days of the shipped world, all villages)
package.path = "./?.lua;./?/init.lua;" .. package.path
local world = require("sim.world")
local people = require("sim.agents.people")
local f = assert(io.open("assets/worlds/v3.tcsave", "rb")); local data = f:read("*a"); f:close()
local W = assert(require("sim.worlds").from_save_string(data))
local V = W.S.v3
local acc = {}  -- by age band: { total, play, work }
local WORKK = { forage = 1, fetch_water = 1, fetch_wood = 1, spin = 1, herd = 1, herd_home = 1, slop = 1, follow = 0 }
for m = 1, 24 * 6 * 30 do
  local t = m / 6
  world.advance(W, t)
  for _, s in ipairs(V.settlements) do
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p.alive and people.stage(p, t) == "child" and p.act ~= "sleep" then
        local a = people.age(p, t)
        local band = a < 6 and "2-5" or (a < 9 and "6-8" or "9-12")
        local e = acc[band] or { 0, 0, 0, 0 }
        acc[band] = e
        e[1] = e[1] + 1
        if p.objk == "play" then e[2] = e[2] + 1 end
        if WORKK[p.objk or ""] == 1 then e[3] = e[3] + 1 end
        if p.objk == "follow" then e[4] = e[4] + 1 end
      end
    end
  end
end
for _, b in ipairs({ "2-5", "6-8", "9-12" }) do
  local e = acc[b]
  if e then print(b, string.format("play %.0f%% work %.0f%% follow %.0f%% (n %d)", 100 * e[2] / e[1], 100 * e[3] / e[1], 100 * e[4] / e[1], e[1])) end
end
