-- Villages, people and ships (sim/systems/villages.lua, people.lua, ships.lua).
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local people = require("sim.systems.people")
local M = {}

local function last_of(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind then return r end
  end
end

function M.entity_schema_and_invariants()
  local W = worlds.fresh(1)
  local vs = W.ent.villages
  T.ok(#vs >= 3, "villages")
  local P = W.ent.people
  local alive, pop = 0, 0
  for i, v in ipairs(vs) do
    T.eq(v.id, i, "village id = index")
    for _, k in ipairs({ "name", "cell", "culture", "founded", "members", "pop", "food", "food_security",
      "farms", "fishing", "boats", "houses", "state", "cause" }) do
      T.ok(v[k] ~= nil, "village field " .. k)
    end
    if v.alive then
      alive = alive + 1
      pop = pop + v.pop
      T.eq(#v.members, v.pop, "pop = #members")
      T.eq(W.F.land_use[v.cell], -v.id, "centre land use")
      for _, c in ipairs(v.farms) do T.eq(W.F.land_use[c], v.id, "farm land use") end
      for _, id in ipairs(v.members) do
        local p = P[id]
        T.ok(p and not p.dead and p.village == v.id, "member lives here")
        T.ok(p.partner == 0 or (P[p.partner] and P[p.partner].partner == p.id), "partners are mutual")
      end
    end
  end
  T.ok(alive >= 2, "villages alive at Year 0")
  return string.format("%d villages alive, %d people", alive, pop)
end

function M.person_keys_come_from_where_and_when()
  local W = worlds.fresh(1)
  local v = W.ent.villages[1]
  local k1 = people.key(W, v, 100, 0)
  local k2 = people.key(W, v, 100, 1)
  local k3 = people.key(W, v, 101, 0)
  T.ok(k1 ~= k2 and k1 ~= k3 and k2 ~= k3, "distinct keys")
  -- the key does not depend on how many people exist (no global counter)
  W.ent.next_person = W.ent.next_person + 1000
  T.eq(people.key(W, v, 100, 0), k1, "key independent of the id counter")
end

function M.abandoned_village_leaves_ruins_and_wild_farms()
  local W = worlds.fresh(2)
  local v
  for _, u in ipairs(W.ent.villages) do if u.alive then v = u; break end end
  local farms = {}
  for i, c in ipairs(v.farms) do farms[i] = c end
  for _, id in ipairs(v.members) do people.kill(W, v, W.ent.people[id], "test", 0) end
  people.compact(W, v)
  world.run_ticks(W, 8)
  T.eq(v.alive, false, "abandoned")
  T.ok(v.ruin and W.S.ruins[v.cell], "ruins")
  T.eq(W.F.land_use[v.cell], 0, "centre released")
  for _, c in ipairs(farms) do T.eq(W.F.land_use[c], 0, "farm back to the wild") end
  T.ok(last_of(W, "village_abandoned") ~= nil, "village_abandoned event")
  world.run_ticks(W, 8)
  T.eq(W.F.biome[v.cell], require("sim.systems.biome").B.ruins, "ruins biome")
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
