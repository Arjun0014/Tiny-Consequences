-- Beasts, wild animals and dogs facing fire and lightning (S22, D-176; D-159; docs/v3/65_R6_5_LIVING_PLANET.md
-- §6): a fire is seen and smelt by its own extent - each animal notices it in its own time, runs its own way
-- out, keeps away while it burns and goes back to the herd when the place it is kept is clear; lightning
-- takes an exposed beast as it would a person (the tallest thing near the point), and its loss is the
-- owners' like any other.
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

-- A stand of trees alight beside the flock at pasture on a dry day: every beast that the fire comes near
-- reacts within minutes, in its own time; they run different ways; none goes back into it while it burns.
function M.a_flock_runs_from_a_fire_each_its_own_way()
  local W = shipped()
  local audit = require("tests.sim.support.fire_audit")
  local R = assert(audit.run(W, { sett = 1, herd = "flock", fire = 6, off = 0.15, hours = 4 }))
  T.ok(R.exposed >= 4, string.format("%d of %d came near the fire", R.exposed, R.n))
  T.ok(R.never == 0, string.format("every one near it reacted (%d never)", R.never))
  T.ok(R.react_p90 <= 20, string.format("reaction p90 %.1f min", R.react_p90))
  T.ok(R.startspread >= 1, string.format("they set off over %.1f min, not as one", R.startspread))
  T.ok(R.spread < 0.92, string.format("headings' mean resultant %.2f", R.spread))
  T.ok(R.reentry == 0, string.format("%d came back near it while it burned", R.reentry))
  T.ok(R.inside_aware < 0.05, string.format("%.2f beast-hours in the flames' reach after noticing them (%.2f in all)", R.inside_aware, R.inside))
  T.ok(R.dead == 0, string.format("%d dead", R.dead))
  return string.format("%d near, react med %.1f p90 %.1f min, started over %.1f min, R %.2f, vcorr %.2f, back in 0, in flames %.2f h, displaced %.2f/%.2f",
    R.exposed, R.react_med, R.react_p90, R.startspread, R.spread, R.vcorr, R.inside, R.disp1 or 0, R.disp3 or 0)
end

-- The same for a wild band.
function M.a_wild_band_runs_from_a_fire()
  local W = shipped()
  local audit = require("tests.sim.support.fire_audit")
  local R = assert(audit.run(W, { sett = 1, herd = "wild", fire = 6, off = 0.15, hours = 3 }))
  T.ok(R.exposed >= 3, string.format("%d of %d came near the fire", R.exposed, R.n))
  T.ok(R.never == 0, string.format("every one near it reacted (%d exposed, %d never)", R.exposed, R.never))
  T.ok(R.reentry == 0, string.format("%d came back near it while it burned", R.reentry))
  T.ok(R.dead == 0, string.format("%d dead", R.dead))
  return string.format("%d of %d near, react med %.1f min, R %.2f, back in %d", R.exposed, R.n, R.react_med, R.spread, R.reentry)
end

-- Tekheim's beasts out at pasture: the one whose height most outdoes, at its own place, every tree,
-- roof and person near it by the strike's own rule (height less 4 a unit of distance within its pull).
local function exposed_beast(W, s)
  local stock = require("sim.agents.stock")
  local blaze = require("sim.agents.blaze")
  local danger = require("sim.agents.danger")
  local c = require("sim.config").V3
  local now = W.S.v3.now or W.hour
  local best, bx, by, bm
  for _, kind in ipairs({ "flock", "cattle" }) do
    local H = s.herds[kind]
    for _, a in ipairs(H and H.animals or {}) do
      if a.alive and a.act ~= "pen" and (a.led or 0) == 0 then
        local x, y = stock.pos(a, now)
        local top = -1e9
        blaze.trees_near(s, x, y, c.strike_pull, function(k, d2) top = math.max(top, (s.trees.size[k] or 1) * c.strike_tree - math.sqrt(d2) * 4) end)
        for _, id in ipairs(s.blist) do
          local b = s.buildings[id]
          local d = math.sqrt(((b.cx or b.dx) - x) ^ 2 + ((b.cy or b.dy) - y) ^ 2)
          if b.state == "standing" and not b.natural and d < c.strike_pull then top = math.max(top, (c.strike_bld[b.kind] or c.strike_roof) - d * 4) end
        end
        danger.each_on_map(W, s, function(q)
          if q.inside == 0 then
            local qx, qy = danger.person_pos(q, now)
            local d = math.sqrt((qx - x) ^ 2 + (qy - y) ^ 2)
            if d < c.strike_pull then top = math.max(top, c.strike_person - d * 4) end
          end
        end)
        local m = c.strike_beast[a.sp] - top
        if m > 0.05 and (not bm or m > bm) then best, bx, by, bm = a, x, y, m end
      end
    end
  end
  return best, bx, by, bm
end

-- Lightning aimed at a beast standing alone in the open: it takes the bolt (most die where they stand);
-- the strike's deed names it and its owners; the loss is theirs (an unwilling loss - restocking reads it);
-- the news story of it ends at YOU.
function M.lightning_on_an_exposed_beast()
  local W = shipped()
  local V = W.S.v3
  local s = V.settlements[1]
  local news = require("sim.news")
  local stock = require("sim.agents.stock")
  local a, x, y, near
  local t = W.hour
  for _ = 1, 40 do
    t = t + 0.5
    world.advance(W, t)
    local H = s.herds.flock
    if H and H.out then
      a, x, y, near = exposed_beast(W, s)
      if a then break end
    end
  end
  T.ok(a ~= nil, "a beast alone in the open at Tekheim")
  if not a then return "no beast in the open" end
  local h0 = a.health or 1
  world.command(W, "power", { power = "lightning", cell = s.cell, sid = s.id, x = x, y = y })
  world.advance(W, math.floor(t) + 1.2)
  local d
  for _, q in ipairs(V.deeds) do if q.kind == "strike" and q.at == s.id then d = q end end
  T.ok(d and d.what == a.sp, string.format("the bolt took the %s (%s)", a.sp, d and d.what or "nothing"))
  T.ok(not a.alive or (a.health or 1) < h0 - 0.5, string.format("struck: %s, health %.2f", a.alive and "alive" or "dead", a.health or 0))
  T.ok(d and d.to_hh == a.owner and d.harm, "the strike's deed is its owners' loss")
  local r = d and d.event and root_of_event(W, d.event)
  T.ok(r and r.actor == "player", "its cause: the power")
  news.update(W, W.hour)
  local story
  for _, st in ipairs(news.rt(W).list) do if st.kind == "strike" and st.sid == s.id then story = st end end
  T.ok(story ~= nil and news.chain(W, story).root == "you", "a story of it, rooted at YOU: " .. (story and story.title or "none"))
  if not a.alive then
    local cc
    for _, q in ipairs(s.wildlife.carcasses) do if q.by == "lightning" then cc = q end end
    T.ok(cc ~= nil, "its carcass lies where it fell")
    -- (the household's herd counts it lost against its will at the next daily reckoning)
    world.advance(W, W.hour + 25)
    local hh = V.households[a.owner]
    T.ok(hh and (hh.herd_lost or 0) > 0, string.format("the household's unwilling losses %.1f FU", hh and hh.herd_lost or 0))
  end
  return string.format("%s (%s): %s; story '%s'", a.sp, near > 1e8 and "nothing else within the bolt's reach"
    or string.format("out-topping all near by %.2f", near), a.alive and "hurt" or "killed",
    story and story.title or "-")
end

-- Lightning at a beast grazing under a big tree: the tree takes the bolt (it is taller); the beast beside
-- it is thrown down by the ground's current.
function M.a_tree_takes_the_bolt_before_a_beast_under_it()
  local W = shipped()
  local V = W.S.v3
  local s = V.settlements[1]
  local stock = require("sim.agents.stock")
  local blaze = require("sim.agents.blaze")
  local now
  local a, x, y, tk
  local t = W.hour
  for _ = 1, 60 do
    t = t + 0.5
    world.advance(W, t)
    now = V.now or W.hour
    for _, kind in ipairs({ "flock", "cattle" }) do
      local H = s.herds[kind]
      for _, b in ipairs(H and H.animals or {}) do
        if not a and b.alive and b.act ~= "pen" and (b.led or 0) == 0 then
          local bx, by = stock.pos(b, now)
          blaze.trees_near(s, bx, by, 0.035, function(k, d2)
            if not tk and (s.trees.size[k] or 1) >= 0.6 then a, x, y, tk = b, bx, by, k end
          end)
        end
      end
    end
    if a then break end
  end
  T.ok(a ~= nil, "a beast grazing under a tree")
  if not a then return "none under a tree" end
  local h0 = a.health or 1
  local kind, ref = blaze.strike(W, s, x, y, now, nil, nil)
  T.ok(kind == "tree", "the bolt took " .. tostring(kind))
  T.ok(not a.alive or (a.health or 1) < h0, string.format("the %s beside it thrown down (health %.2f -> %.2f)", a.sp, h0, a.health or 0))
  return string.format("the tree took it; the %s under it %s", a.sp, a.alive and string.format("hurt (%.2f)", a.health) or "killed")
end

return M
