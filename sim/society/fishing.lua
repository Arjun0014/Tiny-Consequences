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
local skills = require("sim.society.skills")

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
local AWAY = { voyage = true, beached = true, homeward = true }   -- boats off on the sea passage (sea.lua)

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
  -- how much fish the village wants landed today (Chayanov: needs, not maximums): its share of the
  -- day's food, more while the store is short (the surplus is dried for the store), less the fresh
  -- fish still in hand. Boats go out in order until their expected catch covers it.
  local cfg0 = cfg
  local share = v.food_days < cfg0.fish_store_days and cfg0.fish_share_short or cfg0.fish_share
  local goal = v.need_day * share - v.stock.fresh * 0.5
  local planned, wanted = 0, 0
  for i = 1, #v.boats do
    local b = B[v.boats[i]]
    b.trip = false
    -- (boats away on a voyage, beached at a foreign shore or sailing home don't fish: sea.lua)
    if b.state == "moored" or b.state == "out" then
      b.state = "moored"
      local crew = b.crew
      local fit = 0
      -- (boats beyond the day's need stay in: no need to look at their crews or the grounds)
      if planned < goal then
        for k = 1, #crew do
          local p = P[crew[k]]
          if p and not p.dead then
            local sk = p.sk
            fit = fit + (0.35 + 0.65 * p.nutrition) * min(1, p.health / 40)
              * (cfg.skill_lo + (cfg.skill_hi - cfg.skill_lo) * ((sk and sk.fish) or 0))
          end
        end
      end
      local iced = F.ice[b.home] >= 0.5
      -- a worn boat is laid up and mended (planks, caulking, nets) before it goes out again
      if b.condition < cfg.boat_layup then b.laid_up = true
      elseif b.laid_up and b.condition >= cfg.boat_relaunch then b.laid_up = nil end
      if b.laid_up then fit = 0 end
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
        if best and planned >= goal then best = nil end
        if best then
          local catch = cfg.fish_q * fish[best] * fit * bh
          if catch > cfg.hold then catch = cfg.hold end
          local stock = fish[best] * cfg.fish_fu
          if catch > stock * 0.5 then catch = stock * 0.5 end
          TAKEN[bj] = TAKEN[bj] + (stock > 0 and catch / stock or 1)
          planned = planned + catch
          local dep = h0 + ws
          b.trip = { depart = dep, ground = best, at = dep + bt, leave = dep + bt + bh, ret = dep + 2 * bt + bh,
            catch = catch, wet = weather.wet or false }
          b.state = "out"
          wanted = wanted + 1
        end
      end
    end
  end
  -- boats the village needed today (slow average): how many fishers it keeps (life.set_occupation)
  v.boats_used = (v.boats_used or #v.boats) * 0.98 + wanted * 0.02
end

-- Land yesterday's catches (at the village's next day step). Returns fresh food landed and the
-- dominant fish tag of the grounds fished.
function M.settle(W, v)
  local cfg = cfgmod.SOCIETY
  local F, C, I, Pf = W.F, W.C, W.I, W.P
  local B = W.ent.boats
  local landed = 0
  local tag, tag_n = 0, 0
  -- one hash per village-day; each boat's squall roll is a fixed offset from it (Weyl, D-021)
  -- (one full hash per day for the planet, a mixing round per village)
  local day = floor(W.hour / 24)
  local sc = W.scratch
  if sc.boat_day ~= day then sc.boat_day, sc.boat_base = day, rng.hash(W.seed, rng.S.BOAT, day, 0, 1) end
  local u0 = rng.fbase(sc.boat_base, v.key_cell)
  -- backwards: a lost boat leaves the list
  for i = #v.boats, 1, -1 do
    local b = B[v.boats[i]]
    local t = b.trip
    if t and b.state ~= "lost" then
      local g = t.ground
      -- a squall now and then catches a boat
      if (u0 + (b.key % 1000003) * 6.18034e-7) % 1 < cfg.squall * (t.wet and cfg.squall_wet or 1) then
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
        acc(v, "trips", 1)
        b.catch = catch
        local ft = C.fish[g]
        if ft ~= 0 and ft ~= vt then
          if ft == tag then tag_n = tag_n + 1 elseif tag_n == 0 then tag, tag_n = ft, 1 else tag_n = tag_n - 1 end
        end
      end
    end
    if b.state ~= "lost" and not AWAY[b.state] then
      if t then
        b.condition = b.condition - cfg.boat_decay / 150   -- worn by use (a trip a day)
      elseif b.laid_up and #b.crew > 0 and v.stock.wood >= cfg.boat_mend_wood then
        -- a day's work on the laid-up boat by its crew, with timber from the store
        v.stock.wood = v.stock.wood - cfg.boat_mend_wood
        acc(v, "boat_wood", cfg.boat_mend_wood)
        b.condition = math.min(1, b.condition + cfg.boat_refit)
      elseif #b.crew > 0 or v.pop > 0 then
        b.condition = math.min(1, b.condition + cfg.boat_mend)   -- nets and gear mended in port
      end
      b.trip = false
      b.state = "moored"
      if b.condition <= 0 then M.lose(W, v, b, 0, "wrecked") end
    end
  end
  v.stock.fresh = v.stock.fresh + landed
  acc(v, "catch", landed)
  -- fresh fish beyond a few days' eating is split, salted and dried for the store (with losses)
  local keep = v.need_day * cfg.fresh_keep_days
  if v.stock.fresh > keep then
    local dry = (v.stock.fresh - keep) * cfg.dry_share
    v.stock.fresh = v.stock.fresh - dry
    v.stock.grain = v.stock.grain + dry * cfg.dry_yield
    acc(v, "dried", dry * cfg.dry_yield)
  end
  return landed, tag
end

-- A boat is lost (sunk by a power, a squall, or wrecked). parent = the cause event.
function M.lose(W, v, b, parent, why)
  if b.state == "lost" then return 0 end
  local out = b.trip and b.trip or nil
  local voyage = b.state == "voyage" and b.party
  b.state = "lost"
  b.lost = W.hour
  local id = 0
  if W.record_events then
    local at = voyage and require("sim.society.sea").cell_of(W, b) or (out and out.ground) or b.home
    id = events.emit(W, "boat_lost", at, parent or 0, voyage and 0 or #b.crew,
      { village = v.id, boat = b.id, why = why })
  end
  if id ~= 0 then
    v.boat_tag, v.boat_tag_h = id, W.hour
    local alive = 0
    for i = 1, #v.boats do local o = W.ent.boats[v.boats[i]]; if o and o.state ~= "lost" then alive = alive + 1 end end
    v.boats_before = alive + 1
  end
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
  -- a boat carrying a party overseas: those aboard may drown, the cargo is lost (sea.lua)
  if voyage then require("sim.society.sea").sunk(W, b, id ~= 0 and id or parent) end
  b.trip = false
  b.crew = {}
  ents.remove_id(v.boats, b.id)
  W.ent.boats[b.id] = nil
  return id
end

return M
