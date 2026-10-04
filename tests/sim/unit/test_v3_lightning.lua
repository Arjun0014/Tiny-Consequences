-- R6.5b, lightning on a village (S20, D-154, docs/v3/65_R6_5_LIVING_PLANET.md §6): the physical event
-- only - a strike at a point, a roof set alight - with the weather and the hour as premises; the rest is
-- the village's own: who notices, who runs out (with the babe), who carries water, whether the house is
-- saved, who takes in a household with no roof, who tends the burnt. The same code, different outcomes.
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")

local M = {}

local C_fire_sight = require("sim.config").V3.fire_sight

local function shipped()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- Tekheim on day 3 at local hour `lh` with its ground at soil water `sw`.
local function at(W, lh, sw)
  local V = W.S.v3
  local s = V.settlements[1]
  local clock = require("sim.agents.clock")
  world.advance(W, W.hour + 72)
  local h = clock.local_hour(W.hour, W.grid.lon[s.cell])
  world.advance(W, W.hour + ((lh - h) % 24))
  W.F.soil_water[s.cell] = sw
  return s
end

local function a_house(s)
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.state == "standing" and b.hh ~= 0 then return b end
  end
end

local function watch(W, s, hours)
  local V = W.S.v3
  local t0 = W.hour
  local fought, fled = {}, {}
  for h = 0.1, hours, 0.1 do
    world.advance(W, t0 + h)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive then
        if p.objk == "fight_fire" then fought[id] = true end
        if p.objk == "flee_fire" then fled[id] = true end
      end
    end
  end
  local nf, nl = 0, 0
  for _ in pairs(fought) do nf = nf + 1 end
  for _ in pairs(fled) do nl = nl + 1 end
  return nf, nl
end

-- A roof alight on a moderately dry afternoon: the village carries water and saves the house.
function M.a_house_fire_by_day_is_put_out()
  local W = shipped()
  local s = at(W, 15, 0.33)
  local b = a_house(s)
  local blaze = require("sim.agents.blaze")
  local n0 = #s.members
  blaze.ignite(W, s, "bld", b.id, W.hour, nil)
  local nf, nl = watch(W, s, 6)
  T.ok(nf >= 3, nf .. " carried water to the fire")
  T.ok((s.acc.fires_out or 0) >= 1 and (s.acc.burnt or 0) == 0, "it was put out, nothing burnt down")
  T.ok(s.buildings[b.id] ~= nil and s.buildings[b.id].state == "standing", "the house stands")
  T.ok(#s.members >= n0 - 1, "nobody died of it")
  return string.format("%d fought it, %d kept away, %d buckets", nf, nl, s.acc.doused or 0)
end

-- The same roof on a night in a drought: it burns down; the household lodges with others, its babe
-- carried out, a house of its own planned.
function M.a_house_fire_in_a_drought_night_is_lost()
  local W = shipped()
  local V = W.S.v3
  local s = at(W, 2, 0.15)
  local b = a_house(s)
  local hh = V.households[b.hh]
  local blaze = require("sim.agents.blaze")
  blaze.ignite(W, s, "bld", b.id, W.hour, nil)
  local nf, nl = watch(W, s, 40)
  T.ok((s.acc.burnt or 0) >= 1, "the house burnt down")
  T.ok(hh.lodge ~= nil, "the household lodges with others (or camps)")
  local host = V.households[hh.lodge or 0]
  local planned = false
  for _, id in ipairs(s.sites) do
    local x = s.buildings[id]
    if x and x.for_hh == hh.id then planned = true end
  end
  T.ok(planned or (hh.house_site ~= nil), "a house of its own is planned")
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    T.ok(p.alive and (p.hurt or 0) < 0.5, p.name .. " got out unharmed")
  end
  local q = host and V.people[host.members[1] or 0]
  return string.format("burnt down; %d fought, %d fled; lodging with %s; house planned: %s", nf, nl, q and (q.surname .. "s") or "nobody (camp)", tostring(planned))
end

-- Lightning on someone at work in the open: burnt (or killed), and, alive, kept to bed and tended.
function M.lightning_hurts_who_it_strikes()
  local W = shipped()
  local V = W.S.v3
  local s = at(W, 11, 0.3)
  local act = require("sim.agents.act")
  local c = require("sim.config").V3
  -- (S22) nothing taller near them to take the bolt instead: no tree or roof within its pull out-tops a
  -- person at their place (the strike's own rule: height less 4 a unit of distance)
  local function tallest_near(x, y)
    local top = -1e9
    require("sim.agents.blaze").trees_near(s, x, y, c.strike_pull, function(k, d2) top = math.max(top, (s.trees.size[k] or 1) * c.strike_tree - math.sqrt(d2) * 4) end)
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      local d = math.sqrt(((b.cx or b.dx) - x) ^ 2 + ((b.cy or b.dy) - y) ^ 2)
      if b.state == "standing" and not b.natural and d < c.strike_pull then top = math.max(top, (c.strike_bld[b.kind] or c.strike_roof) - d * 4) end
    end
    return top
  end
  local who
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.alive and p.inside == 0 and (p.act == "till" or p.act == "harvest" or p.act == "sow" or p.act == "tend") then
      local x, y = act.pos(p, W.hour)
      if tallest_near(x, y) < c.strike_person - 0.05 then who = p; break end
    end
  end
  T.ok(who ~= nil, "someone at work in a field, in the open")
  if not who then return end
  local x, y = act.pos(who, W.hour)
  local blaze = require("sim.agents.blaze")
  local hit, ref = blaze.strike(W, s, x, y, W.hour, nil)
  T.ok(hit == "person" and ref == who.id, "the strike took " .. who.name .. " (" .. tostring(hit) .. ")")
  T.ok((who.hurt or 0) > 0 or not who.alive, "burnt")
  local down = (who.hurt or 0) >= require("sim.config").V3.hurt_down
  watch(W, s, 30)
  T.ok(not who.alive or (who.tended or -9) >= 0, "tended at home")
  -- (S20) too badly hurt to walk: helped home by someone who saw, or kin
  if down and who.alive then T.ok((s.acc.helped_home or 0) >= 1, "helped home") end
  return string.format("%s struck at work: %s%s", who.name, who.alive and string.format("alive, hurt %.2f", who.hurt or 0) or ("dead: " .. who.died_of),
    (s.acc.helped_home or 0) > 0 and "; helped home" or "")
end

-- R6.5c: the planet's fire on a wooded cell beside a village (premise "wildfire": a dry spell) comes
-- onto its map tree by tree; the people see it.
function M.a_wildfire_comes_onto_the_map()
  local W = shipped()
  local V = W.S.v3
  world.advance(W, W.hour + 48)
  T.ok(require("sim.agents.premise").named(W, "wildfire"), "the planet's fire was set beside the village")
  local s = V.settlements[4]
  local people = require("sim.agents.people")
  local orig = people.remember
  local saw = 0
  people.remember = function(p, t, text, ...)
    if type(text) == "string" and text:find("^Fire") then saw = saw + 1 end
    return orig(p, t, text, ...)
  end
  local t0 = W.hour
  -- (S23) whether it came within sight of anyone awake out of doors: then they saw it (where the fire goes is the
  -- wind's and the woods' - in one run it reached the fields, in another it stayed at the map's edge, 1.1 from
  -- anyone, unseen; the senses are what is tested, not where it went)
  local act = require("sim.agents.act")
  local near = C_fire_sight * 0.6
  local close = 0
  for h = 1, 48 do
    world.advance(W, t0 + h)
    for _, fr in ipairs(s.burn or {}) do
      for _, id in ipairs(s.members) do
        local q = V.people[id]
        if q and q.alive and not q.at and q.inside == 0 and q.act ~= "sleep" and q.act ~= "carried" then
          local x, y = act.pos(q, W.hour)
          if (x - fr.x) * (x - fr.x) + (y - fr.y) * (y - fr.y) < near * near then close = close + 1 end
        end
      end
    end
  end
  people.remember = orig
  T.ok((s.acc.trees_burnt or 0) >= 5, (s.acc.trees_burnt or 0) .. " trees of the map burnt")
  if close > 0 then T.ok(saw >= 1, string.format("%d saw it (%d times someone awake was near it)", saw, close)) end
  return string.format("%d trees burnt on %s's map in 2 days; %d saw it (%d times someone awake near it)", s.acc.trees_burnt or 0,
    s.name, saw, close)
end

-- (S23) A tree catches fire beside someone awake out of doors: they see it within the hour (and remember it). The
-- senses, tested where the fire is - a wildfire at the map's edge may burn unseen when nobody is near it.
function M.a_fire_beside_someone_is_seen()
  local W = shipped()
  local V = W.S.v3
  local s = at(W, 11, 0.15)
  local act = require("sim.agents.act")
  local blaze = require("sim.agents.blaze")
  local people = require("sim.agents.people")
  local now = V.now or W.hour
  local who, tree
  for _, id in ipairs(s.members) do
    local q = V.people[id]
    if not tree and q and q.alive and not q.at and q.inside == 0 and q.act ~= "sleep" and people.stage(q, now) == "adult" then
      local x, y = act.pos(q, now)
      local best, bd = nil, 0.3 * 0.3
      blaze.trees_near(s, x, y, 0.3, function(k, d2) if d2 < bd and d2 > 0.03 * 0.03 then best, bd = k, d2 end end)
      if best then who, tree = q, best end
    end
  end
  T.ok(tree ~= nil, "someone awake out of doors with a tree near")
  if not tree then return end
  local orig = people.remember
  local saw = {}
  people.remember = function(p, t, text, ...)
    if type(text) == "string" and text:find("^Fire") then saw[p.id] = true end
    return orig(p, t, text, ...)
  end
  blaze.ignite(W, s, "tree", tree, now, nil)
  world.advance(W, now + 1)
  people.remember = orig
  local n = 0
  for _ in pairs(saw) do n = n + 1 end
  T.ok(saw[who.id] == true, who.name .. " saw the tree burning beside them")
  return string.format("%s and %d in all saw it", who.name, n)
end

-- (S20) A fold set alight in a dry evening with the sheep shut in it: they cry out, someone near
-- wakes and runs to open it, the sheep run from the fire and keep away from it; a new fold is planned
-- for the herd once the old one is gone.
function M.beasts_shut_in_a_burning_fold_are_let_out()
  local W = shipped()
  local s = at(W, 21, 0.15)
  local V = W.S.v3
  local pen, H
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "fold" and b.state == "standing" then pen = b; break end
  end
  for _, k in ipairs({ "flock", "cattle", "swine" }) do if s.herds[k] and s.herds[k].pen == pen.id then H = s.herds[k] end end
  local n0 = 0
  for _, a in ipairs(H.animals) do if a.alive then n0 = n0 + 1 end end
  require("sim.agents.blaze").ignite(W, s, "bld", pen.id, V.now or W.hour, nil)
  local t0 = W.hour
  for h = 1, 96 do world.advance(W, t0 + h) end
  local n = 0
  for _, a in ipairs(H.animals) do if a.alive and a.born < t0 then n = n + 1 end end
  T.ok((s.acc.loosed or 0) >= 1, (s.acc.loosed or 0) .. " opened the fold")
  T.ok(n >= n0 * 0.8, string.format("%d of %d sheep alive", n, n0))
  local np = s.buildings[H.pen]
  T.ok(np and np.id ~= pen.id and np.kind == "fold", "a new fold for the flock")
  return string.format("%d of %d sheep alive; %d burnt; a new fold %s", n, n0, s.acc.stock_burnt or 0, np and np.state or "-")
end

-- (S20) A spark in a ripe field at midday: in a drought the field burns out (the standing corn and the
-- stooks lost, the fire on into the trees) though the village carries water to it; on moist ground
-- the same fire is put out and the reaping goes on.
local function field_fire(sw)
  local W = shipped()
  local V = W.S.v3
  local s, j
  for _ = 1, 60 do
    world.advance(W, W.hour + 24)
    for _, o in ipairs(V.settlements) do
      for k, pl in ipairs(o.plots) do
        if not s and pl.stage == "ripe" and pl.harvested < pl.sown and pl.sown >= 3 then s, j = o, k end
      end
    end
    if s then break end
  end
  local clock = require("sim.agents.clock")
  local lh = clock.local_hour(W.hour, W.grid.lon[s.cell])
  world.advance(W, W.hour + ((14 - lh) % 24))
  W.F.soil_water[s.cell] = sw
  require("sim.agents.blaze").ignite(W, s, "plot", j, V.now or W.hour, nil)
  local t0 = W.hour
  for h = 1, 12 do world.advance(W, t0 + h) end
  return s, s.plots[j]
end

function M.a_field_fire_in_a_drought_takes_the_harvest()
  local s, pl = field_fire(0.15)
  T.ok((s.acc.fields_burnt or 0) >= 1, "the field burnt out")
  T.ok(pl.harvested == pl.sown and (pl.sheaves or 0) == 0, string.format("nothing left to reap (%d of %d, %.1f sheaves)", pl.harvested, pl.sown, pl.sheaves or 0))
  T.ok((s.acc.doused or 0) >= 5, (s.acc.doused or 0) .. " buckets thrown")
  local s2, pl2 = field_fire(0.45)
  T.ok((s2.acc.fields_burnt or 0) == 0, "on moist ground it was put out")
  T.ok((pl2.sheaves or 0) > 0, string.format("the reaping went on: %.1f sheaves", pl2.sheaves or 0))
  return string.format("%s's %s: drought - burnt, %d buckets, %d trees caught; moist - put out, %d of %d rows reaped",
    s.name, pl.name, s.acc.doused or 0, s.acc.trees_burnt or 0, pl2.harvested, pl2.sown)
end

return M
