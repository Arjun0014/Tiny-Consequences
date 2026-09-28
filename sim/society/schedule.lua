-- Where everyone is, hour by hour (docs/world/12_PEOPLE_VILLAGES.md §1.1, 16 §4). A pure function
-- of sim state — the village's plan for its local day (wake, work window, sleep, weather), the
-- person's task and site, their home, their boat's trip or their party's route — shared by the
-- view and the behaviour tests, so what you see is what the sim is doing:
--   asleep at home at night; up at dawn; walking to the field / woodlot / building site / boat,
--   working, walking back by dusk; fishers aboard their boat from departure to return; the idle
--   (leisure, children, elders) at home or at the well; everyone indoors in a storm; travellers
--   on their party's path (walking by day, camped at night).
--
-- schedule.where(W, v, p, t, out) fills `out` (reused) and returns it:
--   out.act    "sleep" | "home" | "walk" | "work" | "fish" | "square" | "travel" | "camp"
--   out.place  "home" | "square" | "field" | "wood" | "site" | "boat" | "path"
--   out.cell, out.lot   where (lot: a building lot on the village's cells; lot 0 = the square)
--   out.from_cell, out.from_lot, out.f   while walking: from -> (cell, lot), progress f (0..1)
--   out.id     building id (site), boat id (fish) or party id (travel)
-- Times are absolute hours (fractional); local hours come from the village's longitude.
local cfgmod = require("sim.config")
local daylight = require("sim.society.daylight")
local sites = require("sim.society.sites")

local M = {}
local floor = math.floor
local LEISURE_H, leisure

local OUTDOOR = { sow = true, tend = true, harvest = true, clear = true, care = true, fuel = true, timber = true,
  brush = true, hunt = true, build = true, forage = true, craft_tools = true, craft_pots = true, herd = true }

-- The home (cell, lot) of person p (nil when homeless: they sleep by the square).
function M.home_of(W, p)
  local h = W.ent.households[p.household]
  if not h or h.home == 0 then return nil end
  local b = W.ent.buildings[h.home]
  if not b or b.state ~= "standing" then return nil end
  return b.cell, b.lot, b
end

-- The place a task is done: cell, lot, place kind, id.
local function task_place(W, v, p)
  local k = p.task
  if k == "build" then
    local b = W.ent.buildings[p.site]
    if b then return b.cell, b.lot, "site", b.id end
    return v.cell, 0, "square", 0
  elseif k == "fuel" or k == "timber" or k == "brush" or k == "forage" then
    return p.site ~= 0 and p.site or v.cell, -1, "wood", 0
  elseif k == "hunt" then
    return v.hunt_cell or v.cell, -1, "wood", 0
  elseif k == "herd" then
    return p.site ~= 0 and p.site or (v.herd_cell or v.cell), -1, "pasture", 0
  elseif k == "craft_cloth" then
    -- spinning and weaving at home
    local hc, hl = M.home_of(W, p)
    if hc then return hc, hl, "home", 0 end
    return v.cell, 0, "square", 0
  elseif k == "craft_tools" or k == "craft_pots" then
    -- at the quarry or the clay pit (the pots are fired by the village)
    return p.site ~= 0 and p.site or v.cell, -1, "craft", 0
  end
  return p.site ~= 0 and p.site or v.cell, -1, "field", 0
end

local function set(out, act, place, cell, lot, id)
  out.act, out.place, out.cell, out.lot, out.id = act, place, cell, lot, id or 0
  out.from_cell, out.from_lot, out.f = nil, nil, nil
  return out
end

local function walking(out, fc, fl, tc, tl, f, place, id)
  out.act, out.place, out.cell, out.lot, out.id = "walk", place, tc, tl, id or 0
  out.from_cell, out.from_lot, out.f = fc, fl, f
  return out
end

-- Party travel: members walk walk_hours_day hours a day from their departure (at dawn), camping
-- at night. Returns path position (index into party.path, fraction) and whether they are moving.
function M.party_pos(W, party, t)
  local cfg = cfgmod.SOCIETY
  local steps = #party.path - 1
  if steps < 1 or t >= party.arrive then return #party.path, 0, false end
  local e = t - party.depart
  if e <= 0 then return 1, 0, false end
  local days = floor(e / 24)
  local into = e - days * 24
  local moving = into < cfg.walk_hours_day
  local walked = days * cfg.walk_hours_day + (moving and into or cfg.walk_hours_day)
  -- the walking hours of the whole trip, from its own departure and arrival (worn trails are
  -- quicker: mobility.walk_hours)
  local et = party.arrive - party.depart
  local dt = floor(et / 24)
  local rt = et - dt * 24
  local total = dt * cfg.walk_hours_day + (rt < cfg.walk_hours_day and rt or cfg.walk_hours_day)
  if total <= 0 then total = steps * cfg.walk_h * 1.5 end
  local s
  local si = party.si
  if si then
    -- a crossing: land steps and sea steps take their own hours (mobility.walk_hours)
    local n1, n2 = si - 1, party.sj - si
    local n3 = steps - n1 - n2
    local hl, hs = party.hl, party.hs
    local x = walked / total * ((n1 + n3) * hl + n2 * hs)
    if x < n1 * hl then s = x / hl
    elseif x < n1 * hl + n2 * hs then s = n1 + (x - n1 * hl) / hs
    else s = n1 + n2 + (x - n1 * hl - n2 * hs) / hl end
  else
    s = walked / total * steps
  end
  if s >= steps then return #party.path, 0, false end
  local i = floor(s)
  return i + 1, s - i, moving
end

-- Free-time spot of person p in leisure slot `slot` of day `day`: "home" (their yard), "square"
-- (the well) or a neighbour's house ("home" place at another household's lot). Children play
-- about the square and the lanes, elders sit at home or by the well, adults mostly at home with
-- visits and gatherings — a fixed habit per person that varies with the hour.
LEISURE_H = 1.5
leisure = function(W, v, p, slot, day, evening, hc, hl, t)
  local u = p.u
  local age = (t - p.born) / 8064
  local r = (floor(u * 977) + slot * 7 + day * 3) % 10
  if age < 12 then
    if r < 5 then return "square", v.cell, 0 end
    return "home", hc, hl
  elseif age >= 60 then
    if r < 3 then return "square", v.cell, 0 end
    return "home", hc, hl
  end
  if r < (evening and 3 or 2) then return "square", v.cell, 0 end
  if r < 4 then
    -- visiting another household of the village
    local hs = v.households
    local n = #hs
    if n > 1 then
      local h = W.ent.households[hs[(floor(u * 131) + day * 5 + slot) % n + 1]]
      local b = h and h.home ~= 0 and W.ent.buildings[h.home]
      if b and b.state == "standing" then return "home", b.cell, b.lot end
    end
  end
  return "home", hc, hl
end

-- Where is person p (of village v) at absolute hour t?
function M.where(W, v, p, t, out)
  out = out or {}
  if p.away ~= 0 then
    local party = W.ent.parties[p.away]
    if party then
      local i, f, moving = M.party_pos(W, party, t)
      local c1 = party.path[i]
      local c2 = party.path[i + 1] or c1
      out.act, out.place, out.cell, out.lot, out.id = moving and "travel" or "camp", "path", c2, -1, party.id
      out.from_cell, out.from_lot, out.f = c1, -1, f
      return out
    end
  end
  local hc, hl = M.home_of(W, p)
  if not hc then hc, hl = v.cell, 0 end
  local plan = v.plan
  local lh = daylight.local_hour(t, W.grid.lon[v.cell])
  local wake, ws, we, sleep = plan.wake or 6, plan.ws or 7, plan.we or 18, plan.sleep or 21.5
  if lh < wake or lh >= sleep then return set(out, "sleep", "home", hc, hl) end
  local cfg = cfgmod.SOCIETY
  local task = p.task
  -- fishers follow their boat's trip
  if task == "fish" then
    local b = W.ent.boats[p.site]
    local tr = b and b.trip
    if tr and t >= tr.depart - 0.5 and t < tr.ret + 0.5 then
      if t < tr.depart then
        return walking(out, hc, hl, v.cell, v.pier_lot or 0, (t - (tr.depart - 0.5)) / 0.5, "boat", b.id)
      elseif t < tr.ret then
        return set(out, "fish", "boat", tr.ground, -1, b.id)
      end
      return walking(out, v.cell, v.pier_lot or 0, hc, hl, (t - tr.ret) / 0.5, "home", 0)
    end
    task = ""   -- no trip today (storm, ice, no catch to be had): an idle day
  end
  local storm = plan.storm
  if task ~= "" and task ~= "travel" and lh >= ws and lh < we and not (storm and OUTDOOR[task]) then
    if p.health >= 25 and not (p.preg_due > 0 and p.preg_due - t < 30 * 24) then
      local tc, tl, place, id = task_place(W, v, p)
      local walk = 0
      if place ~= "site" then walk = sites.dist(W, v.cell, tc) * cfg.walk_h end
      if walk > (we - ws) * 0.4 then walk = (we - ws) * 0.4 end
      local e = lh - ws
      if e < walk then return walking(out, hc, hl, tc, tl, walk > 0 and e / walk or 1, place, id) end
      if lh >= we - walk then return walking(out, tc, tl, hc, hl, walk > 0 and (lh - (we - walk)) / walk or 1, "home", 0) end
      return set(out, "work", place, tc, tl, id)
    end
  end
  -- free time (no work today, evenings): people move between home, the well and neighbours'
  -- houses in slots of LEISURE_H hours, each person on their own rhythm (their fixed fraction u
  -- and the day); a short walk between spots. Storms keep everyone in.
  if storm then return set(out, "home", "home", hc, hl) end
  local slot = floor(lh / LEISURE_H)
  local day = floor(t / 24)
  local pl, c1, l1 = leisure(W, v, p, slot, day, lh >= we, hc, hl, t)
  local e = (lh - slot * LEISURE_H) / LEISURE_H
  if e < 0.15 and lh - LEISURE_H >= wake then
    local _, c0, l0 = leisure(W, v, p, slot - 1, day, lh - LEISURE_H >= we, hc, hl, t)
    if c0 ~= c1 or l0 ~= l1 then return walking(out, c0, l0, c1, l1, e / 0.15, pl, 0) end
  end
  return set(out, pl, pl, c1, l1)
end

return M
