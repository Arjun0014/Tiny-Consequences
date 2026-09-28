-- B16 (W8, D-060): a cattle raid moves beasts from the victim's herd to the raiders' home herd
-- (no more, no fewer), leaves the victim a grudge that names the raid, and the raiders' households
-- gain standing; a failed raid moves none.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local herds = require("sim.society.herds")
local conflict = require("sim.society.conflict")
local hooks = require("sim.testhooks")
local M = {}

local function run(surprise)
  local cfg = cfgmod.CONFLICT
  local s0 = cfg.cattle_surprise
  cfg.cattle_surprise = surprise
  local ok, res = pcall(function()
    local W = worlds.fresh(22)
    world.test_action(W, "cattle_raid", { n = 6 })
    world.step(W)
    local party = W.ent.parties[hooks.last_raid]
    T.ok(party and party.cargo.cattle, "a cattle raid set off")
    local home, u = W.ent.villages[party.cargo.home], W.ent.villages[party.cargo.target]
    -- herds change only by the raid over these days: stop the herds' own week (births, deaths)
    local weekly = herds.weekly
    herds.weekly = function() end
    local h0, u0 = herds.se(home.herd), herds.se(u.herd)
    local taken, got = nil, nil
    local orig = conflict.raiders_home
    for _ = 1, 24 * 10 do
      world.step(W)
      if not taken and not W.ent.parties[party.id] then taken = u0 - herds.se(u.herd) end
      if taken and not home.raid_out then got = herds.se(home.herd) - h0; break end
    end
    herds.weekly = weekly
    local ev
    for _, id in ipairs(W.E.order) do
      local r = W.E.rec[id]
      if r and (r.kind == "cattle_raid" or r.kind == "cattle_raid_repelled") then ev = r end
    end
    return { W = W, home = home, u = u, taken = taken or 0, got = got or 0, ev = ev }
  end)
  cfg.cattle_surprise = s0
  if not ok then error(res, 0) end
  return res
end

function M.b16_cattle_raid_moves_exactly_the_beasts_driven_off()
  local r = run(50)
  T.ok(r.ev and r.ev.kind == "cattle_raid", "the raid succeeds (overwhelming surprise)")
  T.ok(r.taken > 1, string.format("beasts driven off: %.1f se", r.taken))
  T.near(r.got, r.taken, 1e-6, "the raiders' herd grows by exactly what the victims lost")
  local g, ge = conflict.grievance(r.W, r.u.id, r.home.id)
  T.ok(g >= cfgmod.CONFLICT.cattle_grievance * 0.9, "the victims hold a grudge")
  T.eq(ge, r.ev.id, "the grudge names the raid")
  local f = run(0.001)
  T.ok(f.ev and f.ev.kind == "cattle_raid_repelled", "outmatched raiders are chased off")
  T.near(f.taken, 0, 1e-9, "no beasts taken")
  T.near(f.got, 0, 1e-9, "none brought home")
  return string.format("%s raided %s: %.1f se driven off and brought home", r.home.name, r.u.name, r.taken)
end

return M
