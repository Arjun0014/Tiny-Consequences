-- Fishing boats and their trips (docs/world/13_TRANSPORT.md §2).
-- At a village's day step each crewed boat plans a day trip: leave the mooring after dawn, sail to
-- the ground that gives the best expected catch for the hours left after sailing there and back,
-- fish, and be home by dusk. Catch = q x fish density x crew fitness x hours on the ground, capped
-- by the hold (Schaefer: catch per unit effort follows biomass). The catch is landed at the next
-- day step: removed from that ground's fish and added to the village's fresh stock. Storms, gales
-- and sea ice keep boats in. A boat destroyed while out loses its catch (and maybe its crew).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local fields = require("sim.fields")
local blame = require("sim.causality.blame")
local events = require("sim.causality.events")
local sites = require("sim.society.sites")
local ents = require("sim.society.entities")

local M = {}

-- add x to the village's yearly flow account under key k
local function acc(v, k, x)
  local a = v.acc
  if not a then a = {}; v.acc = a end
  a[k] = (a[k] or 0) + x
end
local floor, min, max = math.floor, math.min, math.max
local FFISH = fields.BY_NAME.fish
local TAKEN = {}   -- scratch: share of a ground's fish already targeted today (cleared per village)

-- Weekly: rank the village's grounds by fish x fishing hours (a typical day's window); the daily
-- plan considers the best RANK of them. (Fish change weekly with the planet.) The ranking steers
-- behaviour, so it is saved state (v.grounds): a reloaded world must fish the same grounds.
M.RANK = 8
function M.rank(W, v, hours)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local G = W.vgeo[v.id]
  local dcache = G.fishd
  if not dcache then dcache = {}; G.fishd = dcache end
  local home = v.mooring or v.cell
  local list, score = {}, {}
  for j = 1, #v.fishing do
    local g = v.fishing[j]
    if F.ice[g] < 0.5 and F.fish[g] > 0.01 then
      local travel = sites.dist_cached(W, dcache, home, g) * cfg.sail_h
      local h = hours - 2 * travel - 0.5
      if h >= 1 then list[#list + 1] = g; score[g] = F.fish[g] * h end
    end
  end
  table.sort(list, function(a, b)
    if score[a] ~= score[b] then return score[a] > score[b] end
    return a < b
  end)
  for k = #list, M.RANK + 1, -1 do list[k] = nil end
  v.grounds = list
end

-- Plan today's trips. h0 = absolute hour of the village's local midnight, ws/we = its work
-- window (local hours), weather = today's weather at the village.
function M.plan(W, v, h0, ws, we, weather)
  local cfg = cfgmod.SOCIETY
  local F = W.F
  local B = W.ent.boats
  local P = W.ent.people
  local fish = F.fish
  local G = W.vgeo[v.id]
  local dcache = G.fishd
  if not dcache then dcache = {}; G.fishd = dcache end
  local home = v.mooring or v.cell
  for k = 1, M.RANK do TAKEN[k] = 0 end
  for i = 1, #v.boats do
    local b = B[v.boats[i]]
    b.trip = false
    if b.state ~= "lost" then
      b.state = "moored"
      local crew = b.crew
      local fit = 0
      for k = 1, #crew do
        local p = P[crew[k]]
        if p and not p.dead then fit = fit + (0.35 + 0.65 * p.nutrition) * min(1, p.health / 40) end
      end
      local iced = F.ice[b.home] >= 0.5
      local grounds = v.grounds
      if not grounds then M.rank(W, v, we - ws); grounds = v.grounds end
      if fit > 0 and not weather.storm and not iced and #grounds > 0 then
        local best, bs, bh, bt, bj = nil, 0, 0, 0, 0
        for j = 1, #grounds do
          local g = grounds[j]
          if F.ice[g] < 0.5 and fish[g] > 0.01 then
            local travel = (b.home == home and sites.dist_cached(W, dcache, home, g) or sites.dist(W, b.home, g)) * cfg.sail_h
            local hours = (we - ws) - 2 * travel - 0.5
            if hours >= 1 then
              local s = fish[g] * hours * (1 - TAKEN[j])
              if s > bs then best, bs, bh, bt, bj = g, s, hours, travel, j end
            end
          end
        end
        if best then
          local catch = cfg.fish_q * fish[best] * fit * bh
          if catch > cfg.hold then catch = cfg.hold end
          local stock = fish[best] * cfg.fish_fu
          if catch > stock * 0.5 then catch = stock * 0.5 end
          TAKEN[bj] = TAKEN[bj] + (stock > 0 and catch / stock or 1)
          local dep = h0 + ws
          b.trip = { depart = dep, ground = best, at = dep + bt, leave = dep + bt + bh, ret = dep + 2 * bt + bh,
            catch = catch }
          b.state = "out"
        end
      end
    end
  end
end

-- Land yesterday's catches (at the village's next day step). Returns fresh food landed and the
-- dominant fish tag of the grounds fished.
function M.settle(W, v)
  local cfg = cfgmod.SOCIETY
  local F, C, I, Pf = W.F, W.C, W.I, W.P
  local B = W.ent.boats
  local landed = 0
  local tag, tag_n = 0, 0
  -- backwards: a lost boat leaves the list
  for i = #v.boats, 1, -1 do
    local b = B[v.boats[i]]
    local t = b.trip
    if t and b.state ~= "lost" then
      local g = t.ground
      -- a squall now and then catches a boat
      if rng.float(W.seed, rng.S.BOAT, b.key, floor(t.depart / 24), 1) < cfg.squall then
        M.lose(W, v, b, 0, "squall")
      else
        local d = t.catch / cfg.fish_fu
        local f0 = F.fish[g]
        if d > f0 then d = f0 end
        local f1 = f0 - d
        F.fish[g] = f1
        Pf.fish[g] = f1
        local vt = v.cause
        if vt ~= 0 or C.fish[g] ~= 0 then
          blame.step(C.fish, I.fish, g, -d, FFISH.eps, FFISH.hold, -d, vt, 0, 0, 0, 0, 0, 0)
        end
        local catch = d * cfg.fish_fu
        landed = landed + catch
        b.catch = catch
        local ft = C.fish[g]
        if ft ~= 0 and ft ~= vt then
          if ft == tag then tag_n = tag_n + 1 elseif tag_n == 0 then tag, tag_n = ft, 1 else tag_n = tag_n - 1 end
        end
      end
    end
    if b.state ~= "lost" then
      if t then
        b.condition = b.condition - cfg.boat_decay / 150   -- worn by use (a trip a day)
      elseif #b.crew > 0 or v.pop > 0 then
        b.condition = math.min(1, b.condition + cfg.boat_mend)   -- mended in port
      end
      b.trip = false
      b.state = "moored"
      if b.condition <= 0 then M.lose(W, v, b, 0, "wrecked") end
    end
  end
  v.stock.fresh = v.stock.fresh + landed
  acc(v, "catch", landed)
  return landed, tag
end

-- A boat is lost (sunk by a power, a squall, or wrecked). parent = the cause event.
function M.lose(W, v, b, parent, why)
  if b.state == "lost" then return 0 end
  local out = b.trip and b.trip or nil
  b.state = "lost"
  b.lost = W.hour
  local id = 0
  if W.record_events then
    id = events.emit(W, "boat_lost", out and out.ground or b.home, parent or 0, #b.crew,
      { village = v.id, boat = b.id, why = why })
  end
  if id ~= 0 then v.boat_tag = id end
  -- crew out at sea may drown
  if out then
    local P = W.ent.people
    for k = 1, #b.crew do
      local p = P[b.crew[k]]
      if p and not p.dead and rng.float(W.seed, rng.S.BOAT, b.key, p.id, 7) < 0.6 then
        ents.kill(W, p, "drowned", id ~= 0 and id or parent)
      end
    end
    ents.compact(W, v)
  end
  b.trip = false
  b.crew = {}
  ents.remove_id(v.boats, b.id)
  W.ent.boats[b.id] = nil
  return id
end

return M
