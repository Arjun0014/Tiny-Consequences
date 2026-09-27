-- The settlement layer's structure (sim/society/): invariants on real worlds, keys, abandonment.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local check = require("tests.sim.support.society_check")
local world = require("sim.world")
local ents = require("sim.society.entities")
local M = {}

local function last_of(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind then return r end
  end
end

function M.invariants_hold_at_year0_and_after_years()
  local out = {}
  for _, seed in ipairs({ 1, 22 }) do
    local W = worlds.fresh(seed)
    local err = check.check(W)
    T.ok(err == nil, "seed " .. seed .. " Year 0: " .. tostring(err))
    local alive = 0
    for _, v in ipairs(W.ent.villages) do
      if v.alive then
        alive = alive + 1
        T.ok(#v.households > 0 and #v.buildings > 0, v.name .. " has households and buildings")
      end
    end
    T.ok(alive >= 2, "villages alive at Year 0")
    for y = 1, 12 do
      world.run_years(W, 1)
      err = check.check(W)
      T.ok(err == nil, "seed " .. seed .. " year " .. y .. ": " .. tostring(err))
    end
    out[#out + 1] = string.format("seed %d: %d people after 12 y", seed, W.S.society.pop)
  end
  return table.concat(out, "; ")
end

function M.person_keys_come_from_where_and_when()
  local W = worlds.fresh(1)
  local v = W.ent.villages[1]
  local k1 = ents.person_key(W, v, 100, 0)
  local k2 = ents.person_key(W, v, 100, 1)
  local k3 = ents.person_key(W, v, 101, 0)
  T.ok(k1 ~= k2 and k1 ~= k3 and k2 ~= k3, "distinct keys")
  W.ent.next_person = W.ent.next_person + 1000
  T.eq(ents.person_key(W, v, 100, 0), k1, "key independent of the id counter")
end

function M.abandoned_village_leaves_ruins_and_wild_farms()
  local W = worlds.fresh(2)
  local v
  for _, u in ipairs(W.ent.villages) do if u.alive then v = u; break end end
  local farms = {}
  for i, c in ipairs(v.farms) do farms[i] = c end
  local P = W.ent.people
  for i = #v.members, 1, -1 do ents.kill(W, P[v.members[i]], "test", 0) end
  ents.compact(W, v)
  world.run_ticks(W, 2)
  T.eq(v.alive, false, "abandoned")
  T.ok(v.ruin and W.S.ruins[v.cell], "ruins")
  T.eq(W.F.land_use[v.cell], 0, "centre released")
  for _, c in ipairs(farms) do
    T.ok(W.F.land_use[c] ~= v.id and (W.S.farm[c] == nil or W.S.farm[c].v ~= v.id), "farm released")
  end
  local ruins = 0
  for _, id in ipairs(v.buildings) do
    local b = W.ent.buildings[id]
    if b and b.state == "ruin" then ruins = ruins + 1 end
  end
  T.ok(ruins > 0, "houses are ruins")
  T.eq(#v.boats, 0, "boats gone")
  T.ok(last_of(W, "village_abandoned") ~= nil, "village_abandoned event")
  world.run_ticks(W, 16)
  -- the ruins stand until someone resettles the site (colonists may pick a cleared site at once)
  local resettled = W.F.land_use[v.cell] < 0 and W.ent.villages[-W.F.land_use[v.cell]].alive
  T.ok(W.F.biome[v.cell] == require("sim.systems.biome").B.ruins or resettled,
    "ruins biome (or resettled): biome " .. W.F.biome[v.cell] .. " land_use " .. W.F.land_use[v.cell]
    .. " ruins " .. tostring(W.S.ruins[v.cell]))
  T.ok(check.check(W) == nil, "invariants after abandonment")
end

function M.save_keeps_every_person_field()
  local W = worlds.fresh(1)
  local P = W.ent.people
  local v
  for _, u in ipairs(W.ent.villages) do if u.alive and u.members[1] then v = u; break end end
  local p = P[v.members[1]]
  p.wait_since = 1234
  p.some_new_field = { 1, 2 }
  local packed = ents.pack_people(P)
  local back = ents.unpack_people(packed)
  T.eq(back[p.id].wait_since, 1234, "extra field survives")
  T.eq(back[p.id].some_new_field[2], 2, "unknown fields survive")
end

return M
