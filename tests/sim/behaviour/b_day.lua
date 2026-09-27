-- B12: day and night — at local noon workers are at their sites (or on the way), at local 02:00
-- everyone is at home; local time follows longitude. B13: people sleep in their own household's
-- home. Positions come from sim/society/schedule.lua (the same function the view draws with).
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local daylight = require("sim.society.daylight")
local schedule = require("sim.society.schedule")
local U = require("tests.sim.behaviour.util")
local M = {}

function M.b12_b13_days_and_nights_follow_the_local_sun()
  local W = worlds.fresh(22)
  world.run_ticks(W, 20)   -- the local summer of the north: long days, crops in the fields
  local P = W.ent.people
  local out = {}
  local vs = U.villages(W)
  T.ok(#vs >= 2, "villages")
  local report = {}
  local noon_utc = {}
  for _, v in ipairs(vs) do
    -- local 02:00: everyone asleep in their own home (B13)
    U.to_local(W, v, 2)
    local t = W.hour + 0.5
    local asleep, n = 0, 0
    for _, id in ipairs(v.members) do
      local p = P[id]
      schedule.where(W, v, p, t, out)
      n = n + 1
      if out.act == "sleep" then asleep = asleep + 1 end
      local hc, hl = schedule.home_of(W, p)
      if hc then
        T.eq(out.place, "home", "asleep at home")
        T.ok(out.cell == hc and out.lot == hl, "in their own household's home")
      end
    end
    T.eq(asleep, n, v.name .. ": everyone asleep at 02:00")
    -- local noon: workers at their sites (B12)
    U.to_local(W, v, 12)
    noon_utc[#noon_utc + 1] = { v = v, utc = W.hour % 24 }
    t = W.hour + 0.5
    local tasked, away_from_home, at_work = 0, 0, 0
    for _, id in ipairs(v.members) do
      local p = P[id]
      schedule.where(W, v, p, t, out)
      if out.act == "sleep" then error(v.name .. ": asleep at noon") end
      if p.task ~= "" and p.task ~= "fish" and not v.plan.storm and p.health >= 25 then
        tasked = tasked + 1
        if out.act == "work" or out.act == "walk" then away_from_home = away_from_home + 1 end
        if out.act == "work" then
          at_work = at_work + 1
          if out.place == "site" then T.eq(out.id, p.site, "at their own building site")
          elseif out.place == "field" or out.place == "wood" then
            T.eq(out.cell, p.task == "hunt" and (v.hunt_cell or v.cell) or p.site, "at their own field or woodlot")
          end
        end
      end
    end
    if tasked > 0 then
      T.ok(away_from_home >= tasked * 0.9, v.name .. ": workers out at noon (" .. away_from_home .. "/" .. tasked .. ")")
    end
    report[#report + 1] = string.format("%s %d/%d at work", v.name, at_work, tasked)
  end
  -- local time follows longitude: noon comes at different UTC hours, 1 h per 15 degrees
  for i = 2, #noon_utc do
    local a, b = noon_utc[1], noon_utc[i]
    local dl = (W.grid.lon[a.v.cell] - W.grid.lon[b.v.cell]) * 180 / math.pi / 15
    local du = (b.utc - a.utc) % 24
    local expect = dl % 24
    local err = math.abs(du - expect)
    if err > 12 then err = 24 - err end
    T.ok(err <= 1.01, "noon moves with longitude")
  end
  return table.concat(report, "; ")
end

return M
