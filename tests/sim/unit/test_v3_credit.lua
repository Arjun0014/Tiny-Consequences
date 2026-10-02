-- Paying for a whole good (S19, D-143): a quern is worth more than any one good a household can spare,
-- so it is paid for with a basket of what the household can spare and, when that falls short and the
-- villages trust each other, the rest is owed - a debt between two households, asked for when
-- someone of the creditor's village next comes by. Real premise: a household with a quern to spare in
-- a village with hard rock, households in a village without rock with a little of several goods.
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

function M.a_quern_paid_in_kind_and_owed()
  local W = shipped()
  local V = W.S.v3
  local trade = require("sim.agents.trade")
  local econ = require("sim.agents.econ")
  world.advance(W, W.hour + 25)
  local A, B
  for _, s in ipairs(V.settlements) do
    if s.has_hard then
      for _, o in ipairs(trade.partners(W, s)) do
        if not o.has_rock and not A then A, B = s, o end
      end
    end
  end
  T.ok(A ~= nil, "a village with hard rock beside one with none")
  if not A then return end
  -- (premise: one household of A has a quern to spare; B's households each have a little of several
  -- goods beyond their want - not one of them enough to pay for a quern; the villages know each other)
  local seller
  for _, hid in ipairs(A.households) do
    local hh = V.households[hid]
    local home = hh and A.buildings[hh.home or 0]
    if home and not seller then home.stock.quern = (home.stock.quern or 0) + 1; hh.quern = 1; seller = hh end
  end
  for _, hid in ipairs(B.households) do
    local hh = V.households[hid]
    local home = hh and B.buildings[hh.home or 0]
    if home then
      -- (a little of each to spare beyond what they want for themselves)
      local have, want = econ.house_stock(W, hh, home, "cloth")
      home.stock.cloth = (home.stock.cloth or 0) + math.max(0, want - have) + 0.4
      local hs, ws = econ.house_stock(W, hh, home, "salt")
      home.stock.salt = (home.stock.salt or 0) + math.max(0, ws - hs) + 1.0
      hh.quern = nil
    end
  end
  local r1, r2 = econ.rel(A, B), econ.rel(B, A)
  r1.trust, r2.trust = math.max(r1.trust, 0.4), math.max(r2.trust, 0.4)
  econ.learn(W, A, B, W.hour)
  local t0 = W.hour
  local debt, bought = nil, nil
  local people = require("sim.agents.people")
  local orig = people.remember
  local sales = {}
  people.remember = function(p, t, text, ...)
    if type(text) == "string" and text:find("^Traded a quern") then sales[#sales + 1] = text end
    return orig(p, t, text, ...)
  end
  for h = 1, 24 * 20 do
    world.advance(W, t0 + h)
    if not debt then
      for _, d in ipairs(econ.debts(W)) do
        if d.crs == A.id and d.dbs == B.id then debt = { cr = d.cr, db = d.db, fu = d.fu, what = d.what, ref = d } end
      end
    end
    if debt and (debt.ref.fu < debt.fu - 0.05 or (B.acc.debts_paid or 0) > 0) then break end
  end
  people.remember = orig
  T.ok(debt ~= nil, "a sale of " .. B.name .. " was partly owed [" .. table.concat(sales, " | ") .. "]")
  if not debt then return end
  local dh = V.households[debt.db]
  T.ok(dh and ((dh.quern or 0) > 0 or (B.buildings[dh.home or 0] and (B.buildings[dh.home].stock.quern or 0) >= 1)), "the debtor household has the quern")
  T.ok(debt.fu > 0 and debt.fu <= require("sim.config").V3.credit_max, string.format("owed %.1f FU (at most credit_max)", debt.fu))
  local paid = debt.fu - debt.ref.fu
  return string.format("%s of %s owed %.0f FU for a %s; %.0f FU paid back within the window", (V.people[dh.members[1]] or {}).surname or "?", B.name,
    debt.fu, debt.what, paid)
end

return M
