-- Founding a new place (roadmap v3 R5, S17): no village is made out of nothing. A real premise is
-- created - a village grown until there is no ground left near it to build on - and what follows is
-- the agents' own doing: a couple that waited long decides to make a home at a place their village
-- knows, others in the same plight may join them, they pack and lead their beasts, walk the land
-- route, and the place exists from the moment they walk onto it with nothing built; what stands
-- there later is what their work raised (a camp first, then huts, the stores as needed).
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

function M.a_crowded_village_founds_a_new_place()
  local W = shipped()
  local V = W.S.v3
  local Z
  for _, s in ipairs(V.settlements) do if s.name == "Zurug" then Z = s end end
  T.ok(Z ~= nil, "Zurug")
  local added = require("sim.agents.premise").crowd(W, Z, require("sim.config").V3.found_lot_days)
  local settle = require("sim.agents.settle")
  local founding = require("sim.agents.founding")
  T.ok(added >= 5 and settle.find_lot(W, Z) == nil, "no ground left to build on at Zurug (" .. added .. " families more)")
  T.ok(#Z.couples >= 1, "a couple waits for a house")
  local n_sets = #V.settlements
  local t0 = W.hour
  local decided, place, first_seen, walked, on_site = nil, nil, nil, 0, 0
  local order = {}      -- buildings of the place in the order they came to stand
  local seen_standing = {}
  for h = 1, 24 * 22 do
    world.advance(W, t0 + h)
    if not decided then
      for _, hid in ipairs(Z.households) do
        local hh = V.households[hid]
        if hh and hh.found then decided = hh; break end
      end
    end
    if decided then
      for _, id in ipairs(decided.members) do
        local p = V.people[id]
        if p and p.alive and p.act == "travel" then
          walked = walked + 1
          local st = p.plan[p.pi]
          if st and st.site == decided.found.site then on_site = on_site + 1 end
        end
      end
      place = place or founding.place_at(W, decided.found.site)
      if place then
        if not first_seen then
          local standing = 0
          for _, id in ipairs(place.blist) do if place.buildings[id].state == "standing" then standing = standing + 1 end end
          first_seen = { standing = standing, plots = #place.plots, trees = place.trees.n }
        end
        for _, id in ipairs(place.blist) do
          local b = place.buildings[id]
          if b.state == "standing" and not seen_standing[id] then seen_standing[id] = true; order[#order + 1] = b end
        end
      end
    end
  end
  T.ok(decided ~= nil, "a household of Zurug decided to make a new home")
  if not decided then return end
  local fd = decided.found
  T.ok(fd.why:find("no ground left to build on") ~= nil, "they know why: " .. fd.why)
  T.ok(fd.swhy ~= nil and fd.swhy ~= "", "and why there: " .. tostring(fd.swhy))
  -- the place was known to them, and is nobody's country
  T.ok(Z.lore and Z.lore[fd.site] ~= nil, "the place was known at Zurug (" .. tostring(Z.lore and Z.lore[fd.site] and Z.lore[fd.site].how) .. ")")
  local sites = require("sim.society.sites")
  local near = {}
  for _, c in ipairs(sites.rings(W, fd.site, require("sim.config").V3.found_min_steps - 1)) do near[c] = true end
  for i = 1, n_sets do T.ok(not near[V.settlements[i].cell], "no village near the place: " .. V.settlements[i].name) end
  T.ok(walked > 0 and on_site > 0, "they walked the way to it (" .. walked .. " person-hours on the road)")
  T.ok(place ~= nil and #V.settlements == n_sets + 1, "the place exists, made when they came")
  if not place then return end
  T.eq(first_seen.standing, 0, "nothing stood there when it was made")
  T.eq(first_seen.plots, 0, "no field there when it was made")
  T.ok(first_seen.trees > 50, "its woods were there (" .. first_seen.trees .. " trees)")
  T.ok(#order >= 2, "things were built there: " .. #order)
  T.ok(order[1] and order[1].kind == "camp", "the first thing to stand was a camp (" .. tostring(order[1] and order[1].kind) .. ")")
  local hut = false
  for _, b in ipairs(order) do
    T.ok(b.work_done >= b.work_need - 1e-6, b.label .. " stands by work done (" .. string.format("%.1f/%.1f", b.work_done, b.work_need) .. ")")
    T.ok(b.logs_have >= b.logs_need, b.label .. " stands with its logs (" .. b.logs_have .. "/" .. b.logs_need .. ")")
    if b.kind == "house" then hut = true end
  end
  T.ok(hut, "a first house (a hut) stands")
  for _, pl in ipairs(place.plots) do T.ok(pl.cleared ~= nil, pl.name .. " is ground they cleared") end
  -- they are its people
  local members = 0
  for _, id in ipairs(decided.members) do
    local p = V.people[id]
    if p.alive then
      members = members + 1
      T.eq(p.sett, place.id, p.name .. " lives at " .. place.name)
    end
  end
  T.ok(members >= 2, "the founders live there: " .. members)
  T.ok((place.acc.moved_in or 0) > 0, "they brought food: " .. string.format("%.1f FU", place.acc.moved_in or 0))
  T.ok(place.founding and place.founding.settled ~= nil, "its first fire was lit")
  for _, hid in ipairs(Z.households) do T.ok(hid ~= decided.id, "the household is no longer Zurug's") end
  -- whys and memories
  local remembered = false
  for _, id in ipairs(decided.members) do
    local p = V.people[id]
    for i = 1, 8 do
      local m = p.mem[i]
      if m and (m[2]:find("make a new home") or m[2]:find("Came to the place we chose")) then remembered = true end
    end
  end
  T.ok(remembered, "they remember deciding and coming")
  local names = {}
  for _, b in ipairs(order) do names[#names + 1] = b.label end
  return string.format("%s founded by %d of Zurug (%.0f h away): %s", place.name, members, fd.hrs, table.concat(names, ", "))

end

return M
