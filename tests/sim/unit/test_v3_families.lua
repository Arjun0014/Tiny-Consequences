-- Families across villages (S19, found by following people in a 192-day run): a widow who
-- remarries a man of another village takes her children with her - they walk the road beside her
-- and live in her new household - and a household with nobody left in it passes what it held (its
-- fields, boats and beasts) to its kin. Real premises: a husband lost at sea, a match made.
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

function M.a_widow_remarrying_away_brings_her_children()
  local W = shipped()
  local V = W.S.v3
  local people = require("sim.agents.people")
  local trade = require("sim.agents.trade")
  local life = require("sim.agents.life")
  local act = require("sim.agents.act")
  world.advance(W, W.hour + 1)
  local now = V.now or W.hour
  -- a mother of children still at home, with a husband, in a village with a road neighbour that has
  -- an unwed grown man
  local A, B, widow, husband, groom, kids
  for _, s in ipairs(V.settlements) do
    for _, o in ipairs(trade.partners(W, s)) do
      local g
      for _, id in ipairs(o.members) do
        local q = V.people[id]
        if q.alive and q.sex == "m" and q.partner == 0 and people.stage(q, now) == "adult" then g = q; break end
      end
      if g and not widow then
        for _, id in ipairs(s.members) do
          local q = V.people[id]
          local h = V.people[q.partner or 0]
          if q.alive and q.sex == "f" and h and h.alive and h.hh == q.hh and not widow then
            local ks = {}
            for _, kid in ipairs(V.households[q.hh].members) do
              local k = V.people[kid]
              local st = people.stage(k, now)
              if k.alive and k.mother == q.id and (st == "child" or st == "apprentice") then ks[#ks + 1] = k end
            end
            if #ks >= 1 then A, B, widow, husband, groom, kids = s, o, q, h, g, ks end
          end
        end
      end
    end
  end
  T.ok(widow ~= nil, "a mother with children at home and a road neighbour with an unwed man")
  if not widow then return end
  -- her husband is lost at sea; months later a man of B, come courting, marries her
  life.drown(W, husband, A.name, "the fishing grounds")
  T.ok(widow.partner == 0, "widowed")
  W.v3rt = nil
  now = V.now or W.hour
  groom.trip = { to = A.id, from = B.id, court = true, met = true, give = "grain", take = "grain", n = 0, t0 = now }
  act.match_away(W, A, B, groom, widow, now)
  for _, k in ipairs(kids) do T.ok(k.move and k.move.mother == widow.id, k.name .. " is to go with " .. widow.name) end
  local walked = {}
  local arrived = nil
  local t0 = W.hour
  for h = 0.5, 24 * 5, 0.5 do
    world.advance(W, t0 + h)
    for _, k in ipairs(kids) do
      if k.trip and k.trip.move and k.act == "travel" then walked[k.id] = true end
    end
    local done = widow.sett == B.id
    for _, k in ipairs(kids) do if k.sett ~= B.id then done = false end end
    if done and h > 24 then arrived = h; break end
  end
  T.ok(widow.alive and widow.sett == B.id, widow.name .. " lives at " .. B.name .. " now")
  for _, k in ipairs(kids) do
    T.ok(k.alive, k.name .. " is alive")
    T.ok(walked[k.id], k.name .. " walked the road")
    T.ok(k.sett == B.id, k.name .. " lives at " .. B.name .. " with her")
    T.ok(k.hh == widow.hh, k.name .. " is of her household (" .. tostring(k.hh) .. " / " .. tostring(widow.hh) .. ")")
  end
  local names = {}
  for _, k in ipairs(kids) do names[#names + 1] = k.name end
  local note = string.format("%s of %s, widowed, married %s of %s and went with %s to live there (%.0f h after the match)", widow.name, A.name, groom.name, B.name,
    table.concat(names, ", "), arrived or -1)
  -- nobody left in her old household: what it held went to its kin (or neighbours)
  local old = husband.hh ~= 0 and V.households[husband.hh]
  if old and #old.members == 0 then
    T.ok(old.estate ~= nil, "the empty household's estate was settled")
    for _, pl in ipairs(A.plots) do T.ok(pl.hh ~= old.id, "field " .. pl.name .. " is worked by someone") end
    for _, kind in ipairs({ "flock", "cattle", "swine" }) do
      local H = A.herds and A.herds[kind]
      for _, a in ipairs(H and H.animals or {}) do
        if a.alive then T.ok(a.owner ~= old.id, "a " .. a.sp .. " of the old household has an owner") end
      end
    end
    note = note .. "; the empty household's fields and beasts passed on"
  end
  return note
end

return M
