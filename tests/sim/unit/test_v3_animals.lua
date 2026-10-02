-- Animals as individuals (S15): a herd, band or pack shares an objective, but every animal keeps
-- its own place, pace, timing and reasons - no rigid formation, no synchronised blob. Measured on
-- the shipped world with tests/sim/support/animals_audit.lua.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")

local M = {}

local function load()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local W = assert(worlds.from_save_string(f:read("*a")))
  f:close()
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- Over a day and a night: herds out at pasture and wild bands reshuffle (each animal's place in
-- the group changes over two hours; nearest neighbours change), few moves begin all at once, herded
-- beasts walk at their own speeds, dogs work rather than run circles, and the animals of every
-- group have a reason for what they are doing.
function M.animals_are_individuals()
  local W = load()
  local audit = require("tests.sim.support.animals_audit")
  local rows = audit.run(W, { from = 20, hours = 30, dt = 0.05, lag = 2 })
  local so, sn, nk, nkn, starts, mass, cv, cvn, bo, bn = 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
  local frozen, dogs_mv, dogs_n, nowhy = {}, 0, 0, {}
  for _, r in ipairs(rows) do
    local stock_out = r.kind == "stock_herded" or r.kind == "stock_left"
    if stock_out and r.maxn >= 5 and r.offn >= 50 then
      so, sn = so + r.offset, sn + 1
      if r.offset < 0.12 then frozen[#frozen + 1] = string.format("%s %.2f", r.label, r.offset) end
      nk, nkn = nk + r.nnkeep, nkn + 1
    end
    if r.kind:sub(1, 5) == "stock" then starts, mass = starts + r.starts, mass + r.mass * r.starts end
    if r.kind == "stock_herded" and r.cvn >= 10 then cv, cvn = cv + r.speedcv, cvn + 1 end
    if r.kind == "band" and r.maxn >= 5 and r.offn >= 50 then bo, bn = bo + r.offset, bn + 1 end
    if r.kind == "dogs" then dogs_mv, dogs_n = dogs_mv + r.moving, dogs_n + 1 end
    if r.nowhy > 0.1 then nowhy[#nowhy + 1] = string.format("%s %.0f%%", r.label, 100 * r.nowhy) end
  end
  T.ok(sn >= 6, "herds measured out at pasture " .. sn)
  T.ok(so / sn >= 0.3, string.format("herds out reshuffle: mean offset change %.2f of their radius in 2 h", so / sn))
  T.ok(#frozen == 0, "no herd frozen in formation: " .. table.concat(frozen, ", "))
  T.ok(nk / nkn <= 0.6, string.format("nearest neighbours change: %.2f kept after 2 h", nk / nkn))
  T.ok(starts > 100 and mass / starts <= 0.12, string.format("moves begun all at once: %.1f%% of %d", 100 * mass / math.max(1, starts), starts))
  T.ok(cvn >= 3 and cv / cvn >= 0.25, string.format("herded beasts walk at their own speeds: speed cv %.2f", cv / math.max(1, cvn)))
  T.ok(bn >= 2 and bo / bn >= 0.25, string.format("wild bands reshuffle: mean offset change %.2f", bo / math.max(1, bn)))
  T.ok(dogs_n >= 3 and dogs_mv / dogs_n <= 0.45, string.format("dogs with the herds on the move %.0f%% of the time", 100 * dogs_mv / math.max(1, dogs_n)))
  T.ok(#nowhy == 0, "every group's animals have reasons: " .. table.concat(nowhy, ", "))
  -- (S16) suspicious synchronisation, by class (sums weighted by the frames each group was seen):
  -- livestock out moving as one body (velocity correlation), stopping or lying down all at once;
  -- packs travelling as a rigid formation (all one heading and one velocity)
  local sum = {}
  for _, o in ipairs(audit.summary(rows)) do sum[o.cls] = o end
  local function cls(prefix, f)
    local v, n = 0, 0
    for k, o in pairs(sum) do
      if k:sub(1, #prefix) == prefix and o[f] >= 0 then v, n = v + o[f], n + 1 end
    end
    return n > 0 and v / n or -1, n
  end
  for _, pre in ipairs({ "herded", "left" }) do
    local vc = cls(pre, "vcorr")
    local ms = cls(pre, "mstop")
    local mr = cls(pre, "mrest")
    print(string.format("      %s: vcorr %.2f, mass stops %.0f%%, mass rests %.0f%%", pre, vc, 100 * ms, 100 * mr))
    T.ok(vc <= 0.5, string.format("%s livestock do not move as one body: velocity correlation %.2f", pre, vc))
    T.ok(ms <= 0.12, string.format("%s livestock do not stop all at once: %.0f%% of stops", pre, 100 * ms))
    T.ok(mr <= 0.15, string.format("%s livestock do not lie down all at once: %.0f%% of lyings-down", pre, 100 * mr))
  end
  local pk = sum["pack wolf"]
  if pk then
    print(string.format("      wolves: blob %.0f%%, vcorr %.2f, mass stops %.0f%%, mass starts %.0f%%", 100 * pk.blob, pk.vcorr, 100 * pk.mstop, 100 * pk.mass))
    T.ok(pk.blob <= 0.6, string.format("wolves are not a moving formation: %.0f%% of moments all one heading", 100 * pk.blob))
    -- (S18, D-136: 0.75 -> 0.8: the window now holds a hunt - a pack closing on its prey single file
    -- along the leader's trail moves with correlated velocity; blob and mass stops still bound it)
    T.ok(pk.vcorr <= 0.8, string.format("wolves move each its own way: velocity correlation %.2f", pk.vcorr))
    T.ok(pk.mstop <= 0.2, string.format("wolves do not stop all at once: %.0f%%", 100 * pk.mstop))
  end
  local bd = cls("band", "vcorr")
  print(string.format("      wild bands: vcorr %.2f", bd))
  T.ok(bd <= 0.5, string.format("wild bands do not move as one body: velocity correlation %.2f", bd))
end

-- One deer of a band is frightened: those near it take flight after it, each when it notices -
-- not all at the same moment - and run from the same danger.
function M.a_band_takes_fright_one_by_one()
  local W = load()
  local wild = require("sim.agents.wild")
  local V = W.S.v3
  -- a band of five or more grazing by day
  local s, band
  for t = 30, 60, 0.5 do
    world.advance(W, t)
    for _, s2 in ipairs(V.settlements) do
      local by = {}
      for _, w in ipairs(s2.wildlife and s2.wildlife.animals or {}) do
        if w.alive and not w.pred and w.act ~= "flee" and not w.leaving then
          by[w.gid] = by[w.gid] or {}
          by[w.gid][#by[w.gid] + 1] = w
        end
      end
      for _, L in pairs(by) do
        if #L >= 5 and (not band or #L > #band) then s, band = s2, L end
      end
    end
    if band then break end
  end
  T.ok(band ~= nil, "a band of five or more found")
  if not band then return end
  local now = V.now
  -- (S19: a deer with others of its band about it - not a straggler off on its own)
  local c3 = require("sim.config").V3
  local first, fn = band[1], -1
  for _, w in ipairs(band) do
    local wx, wy = wild.pos(w, now)
    local n = 0
    for _, o in ipairs(band) do
      local ox, oy = wild.pos(o, now)
      local reach = c3.wild_alarm_seps * wild.WP(w.sp).sep
      if o ~= w and (ox - wx) ^ 2 + (oy - wy) ^ 2 < reach * reach then n = n + 1 end
    end
    if n > fn then first, fn = w, n end
  end
  local x, y = wild.pos(first, now)
  first.flee_x, first.flee_y = x + 0.05, y
  wild.interrupt(W, s, first, now)
  local fled_at = {}
  for t = now + 0.005, now + 0.3, 0.005 do
    world.advance(W, t)
    for _, w in ipairs(band) do
      if w.act == "flee" and not fled_at[w.k] then fled_at[w.k] = t end
    end
  end
  local n, tmin, tmax = 0, 1e9, -1e9
  for k, t in pairs(fled_at) do
    if k ~= first.k then
      n = n + 1
      tmin, tmax = math.min(tmin, t), math.max(tmax, t)
    end
  end
  T.ok(fled_at[first.k] ~= nil, "the frightened one ran")
  T.ok(n >= 2, string.format("others took flight after it: %d of %d", n, #band - 1))
  T.ok(n < 2 or tmax - tmin >= 0.01, string.format("each in its own time: first %.3f, last %.3f h after", tmin - now, tmax - now))
end

-- A herder drives the flock out: the beasts come out of the pen and after them each when it
-- notices, at its own speed, and none is left standing in the pen.
function M.the_flock_follows_its_herder()
  local W = load()
  local stock = require("sim.agents.stock")
  local V = W.S.v3
  local s, H, p
  for t = 20, 80, 0.05 do
    world.advance(W, t)
    for _, s2 in ipairs(V.settlements) do
      local H2 = s2.herds and s2.herds.flock
      local q = H2 and stock.herder(W, s2, H2)
      if q and q.act == "walk" and H2.out then s, H, p = s2, H2, q; break end
    end
    if H then break end
  end
  T.ok(H ~= nil, "a flock being driven out found")
  if not H then return end
  local now = V.now
  local started, n = {}, 0
  for _, a in ipairs(H.animals) do if a.alive then n = n + 1 end end
  local why = {}
  for t = now + 0.01, now + 0.6, 0.01 do
    world.advance(W, t)
    for _, a in ipairs(H.animals) do
      if a.alive and a.act == "walk" and not started[a.k] then started[a.k] = t end
      if a.alive and a.why then why[a.why] = true end
    end
  end
  local m, tmin, tmax = 0, 1e9, -1e9
  for _, t in pairs(started) do m, tmin, tmax = m + 1, math.min(tmin, t), math.max(tmax, t) end
  T.ok(m >= 0.6 * n, string.format("the flock set off after its herder: %d of %d", m, n))
  T.ok(tmax - tmin >= 0.04, string.format("each when it noticed: over %.2f h", tmax - tmin))
  T.ok(why["Following the herder"] or why["Catching up with the herder"], "following the herder is a reason they give")
end

-- Live beasts sold to a neighbour (S15, R4): a village whose hay will not winter all its sheep has
-- some marked for killing; its road neighbour has hay to spare and leather the first wants. A
-- household drives the very sheep it would have killed there: each is taken by the halter where it
-- grazes and walks behind the drover (never jumping), leaves the map with them, is the same beast on
-- the other map, and becomes a household's of the buyer's village at the deal; leather comes home.
function M.beasts_are_driven_to_a_buyer()
  local W = load()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local stock = require("sim.agents.stock")
  local econ = require("sim.agents.econ")
  world.advance(W, 30)
  -- the nearest pair of villages by road that both keep sheep
  local S, B, bh = nil, nil, 1e9
  for _, s in ipairs(V.settlements) do
    if s.herds and s.herds.flock then
      for _, o in ipairs(trade.partners(W, s)) do
        local _, hrs = trade.road(W, s, o)
        if o.herds and o.herds.flock and hrs < bh then S, B, bh = s, o, hrs end
      end
    end
  end
  T.ok(S ~= nil, "two neighbours by road keeping sheep")
  if not S then return end
  -- real want: little hay at S (beasts marked for killing), much at B, and leather at B that S lacks
  S.hay = 20
  B.hay = 400
  local culled = stock.plan_cull(W, S)
  for _, id in ipairs(S.blist) do
    local b = S.buildings[id]
    if b.kind == "house" then b.stock.leather, b.stock.hides = 0, 0 end
  end
  for _, id in ipairs(B.blist) do
    local b = B.buildings[id]
    if b.kind == "house" and b.hh ~= 0 then b.stock.leather = (b.stock.leather or 0) + 6 end
  end
  -- (someone from B came by: S knows how things stand there)
  econ.learn(W, S, B, V.now)
  local who = {}
  for _, a in ipairs(S.herds.flock.animals) do
    if a.alive and a.cull then who[#who + 1] = { a = a, born = a.born, coat = a.coat, sex = a.sex } end
  end
  T.ok(culled > 0 and #who > 0, "sheep marked for killing at " .. S.name .. ": " .. culled)
  local drover, haltered, jumps, on_road, arrived = nil, 0, 0, 0, false
  local last = {}
  local t0 = V.now
  for t = t0 + 0.02, t0 + 24 * 5, 0.02 do
    world.advance(W, t)
    if not drover then
      for _, id in ipairs(S.members) do
        local p = V.people[id]
        if p.alive and p.trip and p.trip.give == "sheep" then drover = p end
      end
    end
    if drover then
      local tr = drover.trip
      if tr and tr.party and #tr.party > 0 then on_road = on_road + 1 end
      if (drover.at or drover.sett) == B.id then arrived = true end
      for _, s2 in ipairs({ S, B }) do
        for _, a in ipairs(s2.herds.flock.animals) do
          if a.alive and (a.led or 0) == drover.id then
            local x, y = stock.pos(a, t)
            local key = s2.id .. ":" .. a.k
            local l = last[key]
            if l and math.sqrt((x - l[1]) ^ 2 + (y - l[2]) ^ 2) > 0.02 * 6 then jumps = jumps + 1 end
            last[key] = { x, y }
            if not l and s2 == S then haltered = haltered + 1 end
          end
        end
      end
    end
  end
  T.ok(drover ~= nil, "a drove set out from " .. S.name)
  if not drover then return end
  T.ok(haltered >= 1, "sheep taken by the halter " .. haltered)
  T.ok(jumps == 0, "led sheep never jumped: " .. jumps)
  T.ok(on_road > 0, "the sheep went with the drover on the road (samples " .. on_road .. ")")
  T.ok(arrived, "the drover reached " .. B.name)
  -- the same beasts, now kept by households of B
  local sold = 0
  for _, a in ipairs(B.herds.flock.animals) do
    if a.alive and a.from == S.id then
      local same = false
      for _, w in ipairs(who) do if w.born == a.born and w.coat == a.coat and w.sex == a.sex then same = true end end
      local hh = V.households[a.owner]
      if same and hh and hh.sett == B.id then sold = sold + 1 end
    end
  end
  T.ok(sold >= 1, "sheep of " .. S.name .. " now kept by households of " .. B.name .. ": " .. sold)
  T.ok((S.acc.got_leather or 0) > 0 or (S.acc.got_cloth or 0) > 0 or (S.acc.got_wool or 0) > 0 or (S.acc.got_cheese or 0) > 0,
    "paid for them in goods carried home")
end

-- Dogs and hungry wolves (D-105, S15): packs gone hungry with the wild prey about the villages
-- hunted out come for the beasts in the pens by night; the dogs at the pens hear them and bark,
-- and the wolves are met there (barking dogs, people woken) or take a beast.
function M.dogs_bark_at_hungry_wolves()
  local W = load()
  local V = W.S.v3
  local A = W.S.wild
  for i = 1, A.np do A.p_cond[i] = 0.3 end
  local t0 = W.hour
  local near_pen = 0
  for h = 1, 24 * 4 do
    world.advance(W, t0 + h)
    for _, s in ipairs(V.settlements) do
      for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
        -- (the grazers about the villages hunted out)
        if w.alive and not w.pred then w.alive, w.gone, w.died = false, true, t0 + h end
        if w.alive and w.pred and w.prey and w.prey.kind ~= "wild" then near_pen = near_pen + 1 end
      end
    end
  end
  local barks, visits, pen, driven = 0, 0, 0, 0
  for _, s in ipairs(V.settlements) do
    barks = barks + (s.acc.barks or 0)
    visits = visits + (s.acc.wolf_nights or 0)
    pen = pen + (s.acc.stock_wolves or 0)
    driven = driven + (s.acc.packs_driven or 0)
  end
  T.ok(visits >= 2, "hungry wolf nights " .. visits)
  T.ok(near_pen > 0, "hours wolves were after beasts in a pen " .. near_pen)
  T.ok(barks >= 1, "dogs barking at wolves " .. barks)
end

return M
