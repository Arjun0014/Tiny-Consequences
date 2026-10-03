-- R7 v1 god powers on the v3 world (S21, D-174; docs/v3/70_R7_GOD_POWERS.md): each writes physical state
-- through the world's own functions with the power's event as the cause; what follows is the world's,
-- and it traces back to YOU through real links.
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

local function root_of_event(W, id)
  local E = W.E
  for _ = 1, 20 do
    local e = E.rec[id]
    if not e then return nil end
    if (e.parent or 0) == 0 then return e end
    id = e.parent
  end
end

-- A bolt at a house: the tallest thing near takes it; its deed's cause is the power (YOU in the news).
function M.lightning_at_a_point()
  local W = shipped()
  local news = require("sim.news")
  local V = W.S.v3
  local s = V.settlements[1]
  local b
  for _, id in ipairs(s.blist) do local x = s.buildings[id]; if x.kind == "house" and x.state == "standing" then b = x; break end end
  world.command(W, "power", { power = "lightning", cell = s.cell, sid = s.id, x = b.cx, y = b.cy })
  world.advance(W, W.hour + 1)
  news.update(W, W.hour)
  local d
  for _, x in ipairs(V.deeds) do if x.kind == "strike" and x.at == s.id then d = x end end
  T.ok(d ~= nil, "a strike at " .. s.name)
  local r = d and d.event and root_of_event(W, d.event)
  T.ok(r and r.actor == "player" and r.kind == "power_lightning", "its cause: the power")
  local you = false
  for _, st in ipairs(news.rt(W).list) do if (st.kind == "fire" or st.kind == "strike") and st.sid == s.id and news.chain(W, st).root == "you" then you = true end end
  T.ok(you, "the news story of it is rooted at YOU")
  return string.format("struck %s (%s)", d and d.what or "?", d and (d.bkind or "") or "")
end

-- Cold air over a village: the air there colder at once, tagged with the power (what grows, falls or
-- freezes is the season's and the weather's).
function M.cold_air_is_the_power()
  local W = shipped()
  local s = W.S.v3.settlements[6]
  local t0 = W.F.temp[s.cell]
  world.command(W, "power", { power = "cold", cell = s.cell })
  world.advance(W, W.hour + 1)
  T.ok(W.F.temp[s.cell] < t0 - 5, string.format("%.1f C -> %.1f C", t0, W.F.temp[s.cell]))
  local tag = W.C.heat_anom[s.cell]
  local r = tag and tag ~= 0 and root_of_event(W, tag)
  T.ok(r and r.kind == "power_chill", "the cold air's cause on the cell: the power")
  return string.format("%.1f C -> %.1f C", t0, W.F.temp[s.cell])
end

-- A strained fault gives when it does (a cast or two), and the quake's cause is the power.
function M.a_strained_fault_gives()
  local W = shipped()
  local s = W.S.v3.settlements[1]
  local quake
  for k = 1, 3 do
    world.command(W, "power", { power = "strain", cell = s.cell })
    world.advance(W, W.hour + 30)
    for i = 1, W.E.next_id - 1 do
      local e = W.E.rec[i]
      if e and e.kind == "quake" and (e.parent or 0) ~= 0 then quake = e end
    end
    if quake then break end
  end
  T.ok(quake ~= nil, "a quake with a cause")
  local r = quake and root_of_event(W, quake.id)
  T.ok(r and r.kind == "power_quake", "its root: the power")
  return quake and string.format("magnitude %.1f", quake.mag) or "-"
end

-- Rain over wet hills: the river comes over its banks, and the flood traces to YOU.
function M.rain_floods_and_traces_to_you()
  local W = shipped()
  local news = require("sim.news")
  local s = W.S.v3.settlements[6]
  for _, c in ipairs(require("sim.powers").area(W, s.cell, 2)) do
    if W.geo.ocean[c] == 0 and W.F.soil_water[c] < 0.5 then W.F.soil_water[c] = 0.5 end
  end
  world.command(W, "power", { power = "rain", cell = s.cell })
  local st
  for h = 1, 72 do
    world.advance(W, W.hour + 1)
    news.update(W, W.hour)
    for _, x in ipairs(news.rt(W).list) do if x.kind == "flood" and x.sid == s.id then st = x end end
    if st then break end
  end
  T.ok(st ~= nil, "a flood at " .. s.name)
  T.eq(st and news.chain(W, st).root, "you", "rooted at YOU")
  return "flood rooted at you"
end

return M
