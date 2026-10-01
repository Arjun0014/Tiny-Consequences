-- v3 agent world (docs/v3/10_AGENTS.md, D-078): the slice's guarantees, on one generated world
-- (seed 22, generated once per run and shared by the tests).
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")
local people = require("sim.agents.people")
local clock = require("sim.agents.clock")
local nav = require("sim.agents.nav")

local M = {}
local BASE

local function fresh()
  if not BASE then
    local W = require("sim.agents.worldgen").generate(22, {})
    BASE = worlds.year0_save_string(W, { seed = 22 })
  end
  local W = assert(worlds.from_save_string(BASE))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- Time is continuous: advancing in odd slices gives exactly the state of one big advance.
function M.slicing_is_deterministic()
  local A, B = fresh(), fresh()
  world.advance(A, 30)
  local t = 0
  while t < 30 do
    t = math.min(30, t + 0.137)
    world.advance(B, t)
  end
  T.eq(world.state_hash(A, { "S" }), world.state_hash(B, { "S" }), "sliced and unsliced worlds differ")
end

-- A restored save continues exactly as the live world (aliases re-linked, paths re-cached).
function M.save_round_trip()
  local A = fresh()
  local str = worlds.year0_save_string(A, { seed = 22 })
  local B = assert(worlds.from_save_string(str))
  world.set_systems(require("sim.agents.worldgen").systems())
  world.advance(A, 40)
  world.advance(B, 40)
  T.eq(world.state_hash(A), world.state_hash(B), "restored world diverged")
end

-- Four days of village life: nobody starves, people wake at different times, work is done in
-- the work itself (rows hoed and sown, trees felled, logs carried), the waiting couple's house
-- rises, and every path was found.
function M.four_days()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local n0 = #s.members
  nav.stats.fallbacks = 0
  -- wake times: the first step that is not sleep after each night, per person
  local act = require("sim.agents.act")
  local orig = act.start
  local woke = {}
  local was = {}
  act.start = function(W_, s_, p, now)
    local ok = orig(W_, s_, p, now)
    if was[p.id] == "sleep" and p.act ~= "sleep" then
      woke[#woke + 1] = clock.local_hour(now, W.grid.lon[s.cell])
    end
    was[p.id] = p.act
    return ok
  end
  world.advance(W, 4 * 24)
  act.start = orig
  local alive = 0
  for _, id in ipairs(s.members) do if V.people[id].alive then alive = alive + 1 end end
  T.ok(alive >= n0 - 1, "people died: " .. (n0 - alive))
  T.ok((s.acc.deaths or 0) <= 1, "deaths " .. tostring(s.acc.deaths))
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    T.ok(p.health > 0.5, p.name .. " health " .. p.health)
  end
  table.sort(woke)
  T.ok(#woke >= 40, "wakings " .. #woke)
  T.ok(woke[#woke] - woke[1] >= 1.5, string.format("wake times spread only %.2f h", woke[#woke] - woke[1]))
  local sown = 0
  for _, pl in ipairs(s.plots) do sown = sown + pl.sown end
  T.ok(sown >= 80, "rows sown " .. sown)
  T.ok((s.acc.felled or 0) >= 4, "trees felled " .. tostring(s.acc.felled))
  T.ok((s.acc.built or 0) >= 1, "no house built for the waiting couple")
  T.eq(nav.stats.fallbacks, 0, "unreachable paths")
end

-- Families: children have parents, couples are partners both ways, infants have a carer and are
-- never drawn walking on their own.
function M.families_and_stages()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local kids, infants = 0, 0
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p.partner ~= 0 then T.eq(V.people[p.partner].partner, p.id, "partner not mutual") end
    local st = people.stage(p, W.hour)
    if st == "child" or st == "infant" then
      kids = kids + 1
      T.ok(p.mother ~= 0 or p.father ~= 0, p.name .. " has no parent")
    end
    if st == "infant" then infants = infants + 1; T.ok((p.carer or 0) ~= 0, "infant without carer") end
  end
  T.ok(kids >= 8, "children " .. kids)
  T.ok(infants >= 1, "infants " .. infants)
  world.advance(W, 12)
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    local st = people.stage(p, W.hour)
    if st == "infant" then T.eq(p.act, "carried", "an infant acting alone") end
    if st == "child" then
      T.ok(p.act ~= "till" and p.act ~= "chop" and p.act ~= "build" and p.act ~= "harvest", "a child at " .. p.act)
    end
  end
end

-- Positions are continuous: no step moves a person further than their speed allows.
function M.no_teleports()
  local W = fresh()
  local V = W.S.v3
  local s = V.settlements[1]
  local act = require("sim.agents.act")
  local last = {}
  local worst = 0
  for k = 1, 600 do
    local t = k * 0.05
    world.advance(W, t)
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p.alive and p.act ~= "carried" then
        local x, y = act.pos(p, t)
        local l = last[id]
        if l then
          local dx, dy = x - l[1], y - l[2]
          local d = math.sqrt(dx * dx + dy * dy)
          -- 0.05 h at up to ~5 units/h, plus the one-tile un-stick step
          if d > worst then worst = d end
        end
        last[id] = { x, y }
      end
    end
  end
  T.ok(worst < 0.3, string.format("a person jumped %.3f units in 3 game minutes", worst))
end

return M
