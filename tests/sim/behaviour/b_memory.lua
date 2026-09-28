-- B17 (D-064): a village that remembers a famine keeps more in store (and sows a wider margin) than
-- the same village that does not; its grown-ups' memories of the famine are what raise it (dread),
-- and the memory outlasts ordinary ones.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local cfgmod = require("sim.config")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local economy = require("sim.society.economy")
local farming = require("sim.society.farming")
local U = require("tests.sim.behaviour.util")
local M = {}

local function plan_fields(W, v)
  -- the fields the village's plan wants cropped now (its adults as the workforce)
  local adults = 0
  for _, id in ipairs(v.members) do
    if ents.age(W, W.ent.people[id]) >= cfgmod.SOCIETY.adult_age then adults = adults + 1 end
  end
  return farming.field_want(W, v, adults)
end

function M.b17_remembered_famine_widens_the_margin()
  local Wa, Wb = worlds.fresh(22), worlds.fresh(22)
  local va = U.villages(Wa, function(u) return #u.farms > 0 end)[1]
  local vb = Wb.ent.villages[va.id]
  -- the famine happened (an event of this village), and everyone living there remembers it
  local rec_before = Wa.record_events
  Wa.record_events = true
  local id = events.emit(Wa, "famine", va.cell, 0, va.pop, { village = va.id })
  Wa.record_events = rec_before
  T.ok(id ~= 0, "the famine is recorded")
  for _, pid in ipairs(va.members) do ents.remember(Wa.ent.people[pid], id, 0.9) end
  local da = economy.dread(Wa, va)
  local db = economy.dread(Wb, vb)
  T.ok(da > 0.6, string.format("the grown-ups dread hunger: %.2f", da))
  T.near(db, 0, 1e-9, "the control village has no such memory")
  T.eq(va.dread_ev, id, "the famine is what they remember")
  -- with two years of grain in the granary the control village lets fields go fallow; the one
  -- that remembers the famine keeps sowing them (it is content only with a bigger store)
  local ny = va.need_day * 336
  va.stock.grain, vb.stock.grain = ny * 2.2, vb.need_day * 336 * 2.2
  local fa = plan_fields(Wa, va)
  local fb = plan_fields(Wb, vb)
  T.ok(fa > fb, string.format("more fields kept in crop: %d vs %d", fa, fb))
  -- the memory outlasts ordinary ones: twenty years of fading
  local q = { mem = { id, 0.9, 1, 0.3 } }
  for _ = 1, 20 do ents.fade_memories(q, cfgmod.PEOPLE.memory_fade) end
  T.ok(q.mem[1] == id and q.mem[2] > 0.2, string.format("the famine is still remembered after 20 years (%.2f)", q.mem[2] or 0))
  T.ok(#q.mem == 2, "the minor memory is forgotten")
  return string.format("%s: dread %.2f, fields %d vs %d", va.name, da, fa, fb)
end

return M
