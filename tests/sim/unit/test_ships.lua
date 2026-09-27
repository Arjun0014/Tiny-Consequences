-- Tankers (sim/systems/ships.lua; merchant ships replace them in W3).
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local M = {}

local function last_of(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind then return r end
  end
end

function M.sunk_tanker_spills_oil_and_is_replaced()
  local W = worlds.fresh(1)
  local ships = W.ent.ships
  T.ok(#ships >= 1, "tankers exist")
  local t = ships[1]
  world.test_action(W, "sink_tanker", { tanker = 1 })
  world.tick(W)
  T.eq(t.alive, false, "sunk")
  local ev = last_of(W, "tanker_sunk")
  T.ok(ev and W.E.rec[ev.parent].kind == "test_sink_tanker", "tanker_sunk under the hook")
  local oil = 0
  for c = 1, W.n do if W.F.oil[c] > 0 then oil = oil + W.F.oil[c]; T.ok(W.C.oil[c] ~= 0, "oil tagged") end end
  T.ok(oil > 3, "cargo spilled")
  local lane = W.static.lanes[t.lane]
  T.ok(lane.replace_at > W.tick + 2 * 48, "replacement scheduled years later")
  local n0 = #ships
  world.run_ticks(W, lane.replace_at - W.tick + 1)
  local alive = 0
  for _, v in ipairs(W.ent.villages) do if v.alive then alive = alive + 1 end end
  if alive >= 2 then
    T.eq(#ships, n0 + 1, "a replacement tanker arrived")
    T.ok(ships[#ships].alive and ships[#ships].name ~= t.name, "new name")
  end
end

function M.tankers_move_along_their_lane()
  local W = worlds.fresh(3)
  local t = W.ent.ships[1]
  local lane = W.static.lanes[t.lane].cells
  local seen = {}
  for _ = 1, 200 do
    world.tick(W)
    T.eq(W.geo.ocean[t.cell], 1, "tankers stay at sea")
    seen[t.cell] = true
  end
  local n = 0
  for _ in pairs(seen) do n = n + 1 end
  T.ok(n >= math.min(6, #lane), "moved through several lane cells")
end

return M
