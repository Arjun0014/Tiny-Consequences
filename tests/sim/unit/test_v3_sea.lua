-- Moving by sea (roadmap v3 R5, S18): a real premise is created - an island village whose fields
-- failed and whose stores are empty, and a village over the water heard to have grain to spare -
-- and what follows is the agents' own doing: a household with its own boat and someone to sail it
-- decides to go; they share out the larder for the passage, lead the few beasts the boat can take
-- to the dock, wait for everyone and for weather fit to sail, board, sail, land at the other
-- village's dock and become its people; their boat is moored there for good, theirs still.
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

function M.an_island_household_moves_by_sea()
  local W = shipped()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  local stock = require("sim.agents.stock")
  local c = require("sim.config").V3
  -- a village with a dock and no road to anywhere, and the nearest village over the sea
  local H, Z
  for _, s in ipairs(V.settlements) do
    if s.dock and #trade.partners(W, s) == 0 and #trade.sea_partners(W, s) > 0 then H = s end
  end
  T.ok(H ~= nil, "a village reached only by sea")
  if not H then return end
  Z = trade.sea_partners(W, H)[1]
  local function starve()
    H.granary.stock.grain, H.granary.stock.sheaves, H.granary.stock.smoked = 0, 0, 0
    for _, id in ipairs(H.blist) do
      local b = H.buildings[id]
      if b.kind == "house" and b.hh ~= 0 then
        b.stock.smoked, b.stock.cheese, b.stock.meat, b.stock.fish = 0, 0, 0, 0
        b.stock.food = math.min(b.stock.food or 0, 8)
      end
    end
  end
  require("sim.agents.premise").fail_harvest(W, H)
  -- (S19: a hungry household eats its own beasts before it thinks of leaving: a murrain took most)
  require("sim.agents.premise").murrain(W, H, 1)
  -- (S19: hardship is judged by each household - the fish failed in the island's waters too, or the
  -- fishers, their larders full of the catch, would stay and sail the others over: test_v3_ferry)
  for i = 1, #H.land do if H.land[i] == 0 then W.F.fish[H.cell_of[i]] = 0 end end
  starve()
  Z.granary.stock.grain = Z.granary.stock.grain + 3000
  econ.learn(W, H, Z, W.hour)
  local t0 = W.hour
  local decided, arrived_h, sailed, rode, walked_road = nil, nil, 0, 0, 0
  local ship0 = {}
  local hungriest = 0
  -- (S19: relief now comes by sea from the first days, so they live on it a week before they go)
  for h = 0.5, 24 * 16, 0.5 do
    world.advance(W, t0 + h)
    if h % 24 == 0 then starve() end
    if arrived_h and h > arrived_h + 2 then break end
    if not decided then
      for _, hid in ipairs(H.households) do
        local hh = V.households[hid]
        if hh and hh.migrate and hh.migrate.sea and not hh.migrate.sea.ferry then
          decided = hh
          for kind, list in pairs(hh.migrate.sea.ship or {}) do ship0[kind] = #list end
        end
      end
    end
    if decided then
      for _, id in ipairs(decided.members) do
        local p = V.people[id]
        if p.alive then
          if p.act == "voyage" then sailed = sailed + 1 end
          if p.act == "passage" then rode = rode + 1 end
          if p.act == "travel" then walked_road = walked_road + 1 end
          if not decided.migrate.arrived and p.hunger > hungriest then hungriest = p.hunger end
        end
      end
      if decided.migrate.arrived and not arrived_h then arrived_h = h end
    end
  end
  T.ok(decided ~= nil, "a household of " .. H.name .. " decided to move by sea")
  if not decided then return end
  local mg = decided.migrate
  T.ok(mg.why:find(Z.name) ~= nil and mg.why:find("by sea") ~= nil, "it knows why, where and how: " .. mg.why)
  T.ok(sailed > 0, "its skipper sailed the boat")
  T.ok(rode > 0, "the others crossed in the boat")
  T.eq(walked_road, 0, "nobody walked a road (there is none)")
  T.ok(arrived_h ~= nil, "they reached " .. Z.name)
  T.ok(hungriest < 0.9, "nobody starved on the way (hungriest " .. string.format("%.2f", hungriest) .. ")")
  T.eq(decided.sett, Z.id, "the household is " .. Z.name .. "'s now")
  for _, id in ipairs(decided.members) do
    local p = V.people[id]
    T.ok(not p.alive or (p.sett == Z.id and not p.at and (p.riding or 0) == 0), p.name .. " came too, and is ashore"
      .. string.format(" [sett %s at %s riding %s act %s age %.1f obj %s]", tostring(p.sett), tostring(p.at), tostring(p.riding), tostring(p.act), (W.hour - p.born) / 1152, tostring(p.obj)))
  end
  local listed = false
  for _, hid in ipairs(Z.households) do if hid == decided.id then listed = true end end
  T.ok(listed, Z.name .. " counts the household among its own")
  -- the boat: one boat, moored at its new home, theirs still; never two
  local own = 0
  for _, s in ipairs(V.settlements) do
    for _, b in ipairs(s.boats or {}) do
      if not b.lost and b.owner == decided.id then
        own = own + 1
        T.eq(s.id, Z.id, "their boat is at " .. Z.name)
        T.ok(b.came ~= nil and b.came.from == H.id, "the boat came from " .. H.name)
      end
    end
  end
  T.eq(own, 1, "the household has its one boat")
  local old = H.boats[mg.sea.boat]
  T.ok(old.lost and old.moved and old.moved.to == Z.id, "the boat's place at " .. H.name .. " is empty: it moved")
  -- the beasts: the few the boat could take came, the rest were left with people who stay
  local came = 0
  for _, kind in ipairs(stock.HERDS) do
    local HH = H.herds and H.herds[kind]
    for _, a in ipairs(HH and HH.animals or {}) do
      T.ok(not (a.alive and a.owner == decided.id), "no beast of the household left owned at " .. H.name)
    end
    local ZH = Z.herds and Z.herds[kind]
    for _, a in ipairs(ZH and ZH.animals or {}) do
      if a.alive and a.owner == decided.id then
        came = came + 1
        T.ok(kind ~= "cattle", "no cattle in a small boat")
        T.ok(a.why_owner and a.why_owner:find(H.name) ~= nil, "the beast knows how it came: " .. tostring(a.why_owner))
      end
    end
  end
  local shipped_n = 0
  for _, n in pairs(ship0) do shipped_n = shipped_n + n end
  T.ok(came <= c.mig_sea_beasts and came <= shipped_n, "a few small beasts came over the sea (" .. came .. " of " .. shipped_n .. " chosen)")
  -- the food on them left one village's books and entered the other's
  T.ok(math.abs((H.acc.moved_out or 0) - (Z.acc.moved_in or 0)) < 1e-6, "the food they carried moved between the books")
end

-- Founding by sea (R5, S18). The premise: a northern village's people have sailed to the villages
-- over the sea and seen the shores on the way (their knowledge of places, as a month of voyages
-- leaves it), and a household with its own boat, a babe in arms and young children has decided to
-- make a new home at the best shore it knows (best_sea_site: its own knowledge, never the planet).
-- What follows is theirs: packing, the beasts that fit, seed and goods in the hold, the crossing,
-- the place made as they come into its waters, a landing on its beach, a camp, then a hut.
function M.a_household_founds_a_place_over_the_sea()
  local W = shipped()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local founding = require("sim.agents.founding")
  local migrate = require("sim.agents.migrate")
  local people = require("sim.agents.people")
  local stock = require("sim.agents.stock")
  local now = W.hour
  local S
  for _, s in ipairs(V.settlements) do
    if s.dock and W.grid.lat[s.cell] > 0 then S = s end
  end
  T.ok(S ~= nil, "a northern village with a dock (spring now)")
  if not S then return end
  do
    local p = V.people[S.members[1]]
    for _, o in ipairs(trade.sea_partners(W, S)) do
      founding.note_sea(W, p, trade.sea_route(W, S, o), "seen from the sea on the way to " .. o.name, now)
    end
    founding.tell(W, p, S)
  end
  local hh, sea
  for _, hid in ipairs(S.households) do
    local h = V.households[hid]
    local babe = false
    for _, id in ipairs(h.members) do if people.stage(V.people[id], now) == "infant" then babe = true end end
    local sp = founding.best_sea_site(W, S, h, now)
    if sp and (babe or not hh) then hh, sea = h, sp end
  end
  T.ok(hh ~= nil, "a household with its own boat knows a shore it could sail to")
  if not hh then return end
  T.ok(founding.lore(W, S)[sea.cell] ~= nil, "the shore is one its village knows: " .. sea.why)
  T.ok(founding.place_at(W, sea.cell) == nil, "nothing is there yet")
  founding.decide(W, S, hh, sea.cell, sea.hrs, "there is no ground left for the fields we need", sea.why, now, false, sea)
  local fd = hh.found
  T.ok(fd.sea ~= nil and fd.sea.loads >= 1, "room kept in the boat for seed and goods (" .. tostring(fd.sea.loads) .. " loads)")
  local members = {}
  for _, id in ipairs(hh.members) do members[#members + 1] = V.people[id] end
  local boat0 = S.boats[fd.sea.boat]
  local place, first, sailed, rode, walked, camp_h = nil, nil, 0, 0, 0, nil
  for h = 0.5, 24 * 13, 0.5 do
    world.advance(W, now + h)
    for _, p in ipairs(members) do
      if p.alive then
        if p.act == "voyage" then sailed = sailed + 1 end
        if p.act == "passage" then rode = rode + 1 end
        if p.act == "travel" then walked = walked + 1 end
      end
    end
    local o = founding.place_at(W, sea.cell)
    if o and not place then
      place = o
      first = {}
      for _, id in ipairs(o.blist) do local b = o.buildings[id]; first[#first + 1] = b.kind .. ":" .. b.state end
    end
    if place and not camp_h then
      for _, id in ipairs(place.blist) do
        local b = place.buildings[id]
        if b.kind == "camp" and b.state == "standing" then camp_h = h end
      end
    end
  end
  T.ok(place ~= nil, "the place was made")
  if not place then return end
  T.ok(place.founding.by_sea and place.founded >= now, "made when they sailed in: " .. tostring(place.founding.name_why))
  local standing = 0
  for _, k in ipairs(first) do if k:find("standing") then standing = standing + 1 end end
  T.eq(standing, 0, "nothing stood there when they came (" .. table.concat(first, " ") .. ")")
  T.ok(sailed > 0 and rode > 0, "the skipper sailed and the others crossed in the boat")
  T.eq(walked, 0, "nobody walked a road over the sea")
  for _, p in ipairs(members) do
    T.ok(p.alive and p.sett == place.id and not p.at, p.name .. " (" .. people.stage(p, W.hour) .. ") lives at " .. place.name)
  end
  T.ok(camp_h ~= nil, "a camp stands, raised by them")
  local mine = 0
  for _, b in ipairs(place.boats or {}) do
    if not b.lost and b.owner == hh.id then mine = mine + 1; T.ok(b.came and b.came.from == S.id, "their boat came with them") end
  end
  T.eq(mine, 1, "their one boat is drawn up at the place")
  T.ok(boat0.lost and boat0.moved and boat0.moved.to == place.id, "the boat is gone from " .. S.name)
  for _, kind in ipairs(stock.HERDS) do
    local H = S.herds and S.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      T.ok(not (a.alive and a.owner == hh.id), "no beast of theirs left owned at " .. S.name)
    end
  end
  T.ok(math.abs((S.acc.moved_out or 0) - (place.acc.moved_in or 0)) < 1e-6, "the food they took moved between the books")
end

return M
