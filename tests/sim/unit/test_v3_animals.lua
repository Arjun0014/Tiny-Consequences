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
  local first = band[1]
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

return M
