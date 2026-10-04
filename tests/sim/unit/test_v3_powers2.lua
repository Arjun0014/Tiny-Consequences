-- R7 v2 god powers on the v3 world (S23, D-183; docs/v3/70_R7_GOD_POWERS.md §9): each writes physical state with
-- the cast as its cause, and the world's own systems answer - the herd's sickness, the crops and the wind, the
-- sea's warmth and its fish, the slick on the currents, a rock from the sky. Tests by premises, never by outcome.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")
local clock = require("sim.agents.clock")
local chain = require("sim.chain")

local M = {}

local function load()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local W = assert(worlds.from_save_string(f:read("*a")))
  f:close()
  W.record_events = true
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

local function goto_local(W, s, d, lh)
  local t = W.hour + d * 24
  local h = clock.local_hour(t, W.grid.lon[s.cell])
  t = t + ((lh - h) % 24)
  world.advance(W, t)
end

local function cast(W, p)
  local E = W.E
  local n0 = E.next_id
  world.command(W, "power", p)
  world.advance(W, (W.S.v3.now or W.hour) + 0.01)
  for id = n0, E.next_id - 1 do
    local r = E.rec[id]
    if r and r.actor == "player" and (r.parent or 0) == 0 then return id end
  end
  return 0
end

local function live(W, hours, every)
  local V = W.S.v3
  local t0 = V.now or W.hour
  for h = every, hours, every do
    world.advance(W, t0 + h)
    chain.update(W, V.now or W.hour)
  end
end

local function kinds(W, I)
  local k = {}
  for _, row in ipairs(chain.rows(W, I, 500)) do k[row.node.kind] = (k[row.node.kind] or 0) + 1 end
  return k
end

-- A sickness sent into Tekheim's flock: the beasts aimed at carry it unseen, then show it; it spreads in the pen
-- by the herd's own sickness, carrying the cast as its cause; the chain reads the beasts that carry it.
function M.sickness_in_a_herd()
  local W = load()
  local V = W.S.v3
  local s = V.settlements[1]
  goto_local(W, s, 2, 9)
  local stock = require("sim.agents.stock")
  local H = s.herds.flock
  local a0
  for _, a in ipairs(H.animals) do if a.alive then a0 = a; break end end
  local x, y = stock.pos(a0, V.now)
  chain.update(W, V.now)
  local ev = cast(W, { power = "sickness", cell = s.cell, sid = s.id, x = x, y = y })
  T.ok(ev ~= 0, "the cast is a root event")
  local seeded = 0
  for _, a in ipairs(H.animals) do if a.alive and a.sick_ev == ev and (a.sick_inc or 0) > 0 then seeded = seeded + 1 end end
  T.ok(seeded >= 2, "a few beasts carry it unseen: " .. seeded)
  live(W, 24 * 8, 2)
  local showed, carry = 0, 0
  for _, a in ipairs(H.animals) do
    if a.sick_ev == ev then carry = carry + 1 end
    if (a.sick or 0) > 0 and a.sick_ev == ev then showed = showed + 1 end
  end
  local I = chain.get(W, ev)
  local k = kinds(W, I)
  print(string.format("      seeded %d; after 8 days carrying %d (showing %d); chain: sick %d, dead %d", seeded, carry, showed, k.sick or 0, k.sickness or 0))
  T.ok((s.acc.stock_sick or 0) > 0, "the seeded beasts came to show it")
  T.ok((k.sick or 0) >= 1, "the chain reads the beasts that carried it")
end

-- Blight in a growing field: in mild wet weather it worsens and the wind takes it to the fields near; on parched
-- ground it is held back. The same cast, two courses.
function M.blight_in_wet_and_dry()
  local function run(dry)
    local W = load()
    local V = W.S.v3
    local s = V.settlements[1]
    goto_local(W, s, 3, 9)
    if dry then require("sim.agents.premise").named(W, "dry_day") end
    local best
    for _, pl in ipairs(s.plots) do if pl.stage == "growing" and (not best or pl.sown > best.sown) then best = pl end end
    local px, py = (best.x0 + best.x1) * 0.5, (best.y0 + best.y1) * 0.5
    chain.update(W, V.now)
    local ev = cast(W, { power = "blight", cell = s.cell, sid = s.id, x = px, y = py })
    local sev0 = best.blight or 0
    live(W, 24 * 6, 4)
    local n, worst = 0, 0
    for _, pl in ipairs(s.plots) do if pl.blight and pl.blight_ev == ev then n = n + 1; worst = math.max(worst, pl.blight) end end
    return sev0, n, worst, W, ev
  end
  local s0w, nw, ww, Ww, evw = run(false)
  local s0d, nd, wd = run(true)
  print(string.format("      wet: %.2f -> worst %.2f in %d fields; dry: %.2f -> worst %.2f in %d fields", s0w, ww, nw, s0d, wd, nd))
  T.ok(s0w > 0 and s0d > 0, "the field aimed at caught it")
  T.ok(ww > wd, "it worsens more in the wet than on parched ground")
  local I = chain.get(Ww, evw)
  T.ok(I and (kinds(Ww, I).fields or 0) >= 1, "the chain reads the blighted fields")
end

-- Warm sea: the water off a coastal village warmer, carrying the cast; the chain says so.
function M.a_warmer_sea()
  local W = load()
  local V = W.S.v3
  local g = W.grid
  local s, sea
  for _, o in ipairs(V.settlements) do
    for k = 1, g.nbr_count[o.cell] do
      local c = g.nbr[o.cell * 6 - 6 + k]
      if W.geo.ocean[c] == 1 and not sea then s, sea = o, c end
    end
  end
  T.ok(sea ~= nil, "a village with the sea beside its cell")
  if not sea then return end
  goto_local(W, s, 2, 10)
  local t0 = W.F.sst[sea]
  chain.update(W, V.now)
  local ev = cast(W, { power = "sea", cell = sea, mode = "warm" })
  live(W, 48, 4)
  print(string.format("      the sea off %s: %.1f -> %.1f C", s.name, t0, W.F.sst[sea]))
  T.ok(W.F.sst[sea] > t0 + 1, "the water is warmer")
  T.ok(W.E.root[W.C.sst[sea]] == ev, "its warmth carries the cast")
  local I = chain.get(W, ev)
  T.ok(I and (kinds(W, I).sea or 0) >= 1, "the chain reads the warmer sea")
end

-- A rock from the sky by Tekheim: the ground shaken (a quake with the cast as its cause), the trees and roofs by
-- the point alight, the blast as a deed - each a consequence in the chain.
function M.a_meteor_by_a_village()
  local W = load()
  local V = W.S.v3
  local s = V.settlements[1]
  goto_local(W, s, 2, 11)
  chain.update(W, V.now)
  local ev = cast(W, { power = "meteor", cell = s.cell, sid = s.id, x = (s.sqx or 0) + 0.35, y = (s.sqy or 0) + 0.2 })
  T.ok(ev ~= 0, "the cast is a root event")
  live(W, 12, 0.5)
  local I = chain.get(W, ev)
  local k = kinds(W, I)
  local list = {}
  for x, n in pairs(k) do list[#list + 1] = x .. " " .. n end
  table.sort(list)
  print("      chain: " .. table.concat(list, ", "))
  T.ok((k.impact or 0) >= 1, "the blast at the point")
  T.ok((k.quake or 0) >= 1 or (k["event:quake"] or 0) >= 1, "the ground shaken")
  T.ok((k.dust or 0) >= 1, "dust in the sky")
  local burning = (k.fire or 0) >= 1
  T.ok(burning, "something by the point caught fire")
end

return M
