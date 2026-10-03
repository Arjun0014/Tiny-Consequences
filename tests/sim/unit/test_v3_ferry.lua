-- A ferry (S19, D-145): a household with no boat of its own that has to leave a village reached
-- only by sea asks a household of its village with a boat - kin, friends, or for a fare - and its
-- skipper sails them over, lands them at the other village's dock and sails home in the same boat.
-- The premise is real, made once: on an island the fields failed, a murrain took the beasts, the
-- granary and the larders of the households without a boat are empty (the fishers' larders hold
-- their catch), and a village over the sea is heard to have grain to spare. Nothing forced: who
-- goes, in whose boat, who sails them and when is theirs; the fishers share what they have.
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

function M.a_boatless_household_is_ferried_over()
  local W = shipped()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  local H, Z
  for _, s in ipairs(V.settlements) do
    if s.dock and #trade.partners(W, s) == 0 and #trade.sea_partners(W, s) > 0 then H = s end
  end
  T.ok(H ~= nil, "a village reached only by sea")
  if not H then return end
  Z = trade.sea_partners(W, H)[1]
  local boated = {}
  for _, b in ipairs(H.boats or {}) do if not b.lost and b.owner ~= 0 then boated[b.owner] = true end end
  local function starve()
    H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 0, 0, 0
    for _, id in ipairs(H.blist) do
      local b = H.buildings[id]
      if b.kind == "house" and b.hh ~= 0 and not boated[b.hh] then
        b.stock.smoked, b.stock.cheese, b.stock.meat, b.stock.fish, b.stock.food, b.stock.meals = 0, 0, 0, 0, 0, 0
      end
    end
  end
  require("sim.agents.premise").fail_harvest(W, H)
  require("sim.agents.premise").murrain(W, H, 1)
  starve()
  Z.granary.stock.grain = Z.granary.stock.grain + 3000
  econ.learn(W, H, Z, W.hour)
  local t0 = W.hour
  local hh, sk, boat, arrived_h, home_h = nil, nil, nil, nil, nil
  local sailed, rode, alive0, lost = 0, 0, nil, 0
  for h = 0.5, 24 * 24, 0.5 do
    world.advance(W, t0 + h)
    if not hh then
      for _, hid in ipairs(H.households) do
        local x = V.households[hid]
        if x and x.migrate and x.migrate.sea and x.migrate.sea.ferry then
          hh, sk, boat = x, V.people[x.migrate.sea.skipper], H.boats[x.migrate.sea.boat]
        end
      end
    end
    if hh then
      if sk.alive and sk.act == "voyage" then sailed = sailed + 1 end
      local alive = 0
      for _, id in ipairs(hh.members) do
        local p = V.people[id]
        if p.alive then
          alive = alive + 1
          if p.act == "passage" then rode = rode + 1 end
        end
      end
      alive0 = alive0 or alive
      if not hh.migrate.arrived then lost = alive0 - alive end
      if hh.migrate.arrived and not arrived_h then arrived_h = h end
      if arrived_h and not home_h and sk.alive and not sk.trip and not sk.at and (sk.boat or 0) == 0 then home_h = h end
      if home_h and h > home_h + 2 then break end
    end
  end
  T.ok(hh ~= nil, "a household of " .. H.name .. " with no boat chose to cross in another's")
  if not hh then return end
  local mg = hh.migrate
  T.ok(not boated[hh.id], "it has no boat of its own")
  T.ok(sk ~= nil and sk.hh ~= hh.id, "its skipper is of another household: " .. (sk and sk.name or "?"))
  T.ok(mg.why:find(Z.name) ~= nil and mg.why:find("sail us over") ~= nil, "it knows why, where and who sails them: " .. mg.why)
  T.ok(sailed > 0, "the skipper sailed")
  T.ok(rode > 0, "the household crossed in the boat")
  T.ok(arrived_h ~= nil, "they reached " .. Z.name)
  T.eq(lost, 0, "nobody of the household died before they landed")
  T.eq(hh.sett, Z.id, "the household is " .. Z.name .. "'s now")
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    T.ok(not p.alive or (p.sett == Z.id and not p.at and (p.riding or 0) == 0), p.name .. " came too, and is ashore")
  end
  -- the skipper and the boat came home: the same boat, still his household's, at its own dock
  T.ok(home_h ~= nil, sk.name .. " sailed home again")
  T.eq(sk.sett, H.id, sk.name .. " is still of " .. H.name)
  T.ok(sk.ferry == nil, "the passage is done")
  T.ok(boat and not boat.lost and not boat.moved and boat.owner == sk.hh and not boat.away and not boat.move,
    "the boat is home, still the " .. sk.surname .. "s'")
  local n = 0
  for _, s in ipairs(V.settlements) do
    for _, b in ipairs(s.boats or {}) do if not b.lost and b.owner == sk.hh then n = n + 1 end end
  end
  local n0 = 0
  for _, b in ipairs(H.boats) do if not b.lost and b.owner == sk.hh then n0 = n0 + 1 end end
  T.eq(n, n0, "no boat appeared anywhere else")
  T.ok((H.acc.ferries or 0) >= 1, "the passage is on " .. H.name .. "'s books")
  return string.format("%s sailed the %ss (%s) to %s, landed on day %.1f, home on day %.1f", sk.name, V.people[hh.members[1]].surname,
    mg.sea.fare > 0 and "for a fare" or "for nothing", Z.name, arrived_h / 24, home_h / 24)
end

return M
