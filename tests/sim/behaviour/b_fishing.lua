-- B01: a fishing boat leaves, reaches its ground, fishes and returns; the village's fresh stock
-- rises by exactly the catch and the ground's fish falls by exactly the same amount.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local fishing = require("sim.society.fishing")
local schedule = require("sim.society.schedule")
local U = require("tests.sim.behaviour.util")
local M = {}

function M.b01_boat_trip_moves_fish_from_ground_to_store()
  local W = worlds.fresh(22)
  local v = U.villages(W, function(u) return #u.boats > 0 end)[1]
  T.ok(v, "a fishing village")
  local cfg = cfgmod.SOCIETY
  local B = W.ent.boats
  for _ = 1, 40 do
    -- the day's plan is made at local midnight; look at it just before the next day step
    for _ = 1, U.steps_to_daystep(W, v) + 1 do world.step(W) end
    local out = {}
    local trips = {}
    for _, id in ipairs(v.boats) do
      local b = B[id]
      if b.trip then trips[#trips + 1] = b end
    end
    -- the next settle must not straddle a planet week (the ocean changes fish weekly)
    local next_ds = W.hour + 23
    if #trips > 0 and math.floor(next_ds / 168) == math.floor(W.hour / 168) then
      local h0 = v.plan.h0
      for _, b in ipairs(trips) do
        local tr = b.trip
        T.ok(tr.depart >= h0 + v.plan.ws - 1e-9 and tr.at > tr.depart and tr.leave > tr.at and tr.ret > tr.leave,
          "trip times ordered, departing after the work day starts")
        T.ok(tr.ret <= h0 + v.plan.we + 1e-6, "home by the end of the work window")
        local listed = false
        for _, g in ipairs(v.fishing) do if g == tr.ground then listed = true end end
        T.ok(listed and W.geo.ocean[tr.ground] == 1, "the ground is one of the village's waters")
        -- a crew member is aboard mid-trip
        local p = W.ent.people[b.crew[1]]
        schedule.where(W, v, p, (tr.at + tr.leave) / 2, out)
        T.eq(out.act, "fish", "crew aboard while the boat is on the ground")
        T.eq(out.id, b.id, "aboard their own boat")
      end
      -- land the catch now (what the next day step does first) and account for every unit
      local grounds, before = {}, {}
      for _, b in ipairs(trips) do
        local g = b.trip.ground
        if not before[g] then grounds[#grounds + 1] = g; before[g] = W.F.fish[g] end
      end
      local fresh0 = v.stock.fresh
      local landed = fishing.settle(W, v)
      local removed = 0
      for _, g in ipairs(grounds) do removed = removed + (before[g] - W.F.fish[g]) * cfg.fish_fu end
      T.ok(landed > 0, "fish landed")
      T.near(v.stock.fresh - fresh0, landed, 1e-6, "fresh stock rises by the catch")
      T.near(removed, landed, 1e-6, "the grounds lose exactly the catch")
      for _, b in ipairs(trips) do
        if b.state ~= "lost" then T.ok(b.trip == false and b.state == "moored", "boat back at its mooring") end
      end
      return string.format("%s: %d boats landed %.1f FU", v.name, #trips, landed)
    end
    world.step(W)
  end
  error("no fishing day found")
end

return M
