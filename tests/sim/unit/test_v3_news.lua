-- The world news (S21, R7, D-172): stories read from the world, aggregated by incident, with causes that
-- are real links (deeds, events, the planet's blame tags) ending at NATURE, PEOPLE or YOU.
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

-- One flood is one story: it opens when the water comes over the banks, gathers its lines (water in
-- buildings, beasts let out), stays active while the water lies and closes after it has gone down.
function M.a_flood_is_one_evolving_story()
  local W = shipped()
  local news = require("sim.news")
  T.ok(require("sim.agents.premise").named(W, "flood5"), "the wet spell")
  local t0 = W.hour
  local was_active = false
  for h = 1, 24 * 7 do
    world.advance(W, t0 + h)
    news.update(W, W.hour)
    local R = news.rt(W)
    for _, st in ipairs(R.list) do if st.kind == "flood" and st.sid == 5 and st.active then was_active = true end end
  end
  local R = news.rt(W)
  local floods = {}
  for _, st in ipairs(R.list) do if st.kind == "flood" and st.sid == 5 then floods[#floods + 1] = st end end
  T.ok(#floods >= 1, #floods .. " flood stories at Little Leelee")
  local st = floods[#floods]
  T.ok(was_active, "the story was active while the water lay")
  T.ok(#st.lines >= 3, #st.lines .. " lines: " .. table.concat(st.lines, " / "))
  T.ok(not st.active, "it ended when the water went down")
  local ch = news.chain(W, st)
  T.ok(#ch.links >= 1 and ch.root == "nature", "its cause: " .. (ch.links[1] and ch.links[1].text or "?") .. " (root " .. ch.root .. ")")
  return string.format("%d lines; root %s", #st.lines, ch.root)
end

-- The god's rain over the hills: the flood it brings traces to YOU through the planet's own tags (rain ->
-- soil -> river flow -> the flood deed).
function M.a_flood_from_your_rain_traces_to_you()
  local W = shipped()
  local news = require("sim.news")
  require("sim.powers")
  local s = W.S.v3.settlements[6]
  for _, c in ipairs(require("sim.powers").area(W, s.cell, 2)) do
    if W.geo.ocean[c] == 0 and W.F.soil_water[c] < 0.5 then W.F.soil_water[c] = 0.5 end
  end
  world.command(W, "power", { power = "weather", cell = s.cell, mode = "rain" })
  local t0 = W.hour
  local st
  for h = 1, 72 do
    world.advance(W, t0 + h)
    news.update(W, W.hour)
    for _, x in ipairs(news.rt(W).list) do if x.kind == "flood" and x.sid == s.id then st = x end end
    if st then break end
  end
  T.ok(st ~= nil, "a flood at " .. s.name)
  if not st then return end
  local ch = news.chain(W, st)
  local texts = {}
  for _, l in ipairs(ch.links) do texts[#texts + 1] = l.text end
  T.eq(ch.root, "you", "the root of its causes: " .. table.concat(texts, " <- "))
  return table.concat(texts, " <- ")
end

-- Nothing invented: every story with a title rests on a deed or an event that exists, or on live state
-- of its village (things burning, water, a band, a household on the move, hunger).
function M.every_story_rests_on_the_world()
  local W = shipped()
  local news = require("sim.news")
  T.ok(require("sim.agents.premise").named(W, "conflict"), "the hungry village")
  local t0 = W.hour
  for h = 1, 24 * 6 do
    world.advance(W, t0 + h)
    news.update(W, W.hour)
  end
  local V = W.S.v3
  local n, bad = 0, {}
  for _, st in ipairs(news.rt(W).list) do
    if st.title then
      n = n + 1
      local ok = (st.deed and V.deeds[st.deed]) or (st.ev and W.E.rec[st.ev]) or st.kind == "hunger" or st.kind == "raid"
        or st.kind == "leaving" or st.kind == "founding" or st.kind == "fire"
        or st.kind == "harvest" or st.kind == "orphans"   -- (S23: ripe fields being cut; the village's children taken in)
      if not ok then bad[#bad + 1] = st.kind .. ":" .. st.title end
    end
  end
  T.ok(n >= 2, n .. " stories")
  T.eq(#bad, 0, "stories without a deed, an event or live state: " .. table.concat(bad, "; "))
  return n .. " stories, each resting on the world"
end

return M
