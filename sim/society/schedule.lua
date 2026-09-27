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

local OUTDOOR = { sow = true, tend = true, harvest = true, clear = true, fuel = true, timber = true,
  brush = true, hunt = true, build = true }

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
  elseif k == "fuel" or k == "timber" or k == "brush" then
    return p.site ~= 0 and p.site or v.cell, -1, "wood", 0
  elseif k == "hunt" then
    return v.hunt_cell or v.cell, -1, "wood", 0
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
  local total = steps * cfg.walk_h * 1.5
  local s = walked / total * steps
  if s >= steps then return #party.path, 0, false end
  local i = floor(s)
  return i + 1, s - i, moving
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
  -- leisure and evenings: the well for some, home for the rest (a fixed habit per person)
  local u = p.u
  if lh >= we and lh < sleep - 1 and u < 0.35 then return set(out, "square", "square", v.cell, 0) end
  if lh >= ws and lh < we and (task == "" or storm) and not storm and u < 0.6 then
    return set(out, "square", "square", v.cell, 0)
  end
  return set(out, "home", "home", hc, hl)
end

return M
