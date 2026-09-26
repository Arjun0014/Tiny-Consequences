-- Detector helpers (sim/causality/detectors.lua).
local T = require("tests.sim.lib")
local det = require("sim.causality.detectors")
local M = {}

function M.dominant_tag_needs_a_share()
  local W = { scratch = { det_tk = {}, det_tc = {} } }
  local tags = { 5, 5, 0, 7, 5, 0, 0, 0, 0, 0 }
  local cells = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
  T.eq(det.dominant(W, cells, 10, tags, 0.2), 5, "most frequent non-zero tag")
  T.eq(det.dominant(W, cells, 10, tags, 0.5), 0, "below share -> 0")
  local tie = { 9, 3, 3, 9 }
  T.eq(det.dominant(W, { 1, 2, 3, 4 }, 4, tie, 0.1), 3, "ties -> lower id")
end

function M.flood_event_takes_the_flow_tag_and_retags()
  local scen = require("tests.sim.scenarios.lib")
  local events = require("sim.causality.events")
  local W = scen.fresh_world(1)
  local c = W.geo.land_cells[1]
  local root = events.emit(W, "test_generic", c, 0, 1)
  W.C.flow[c] = root
  W.scratch.flood = { c }
  W.tick = 5   -- not a monthly tick: only the per-tick flood check runs
  det.update(W)
  local r = W.E.rec[W.E.order[#W.E.order]]
  T.eq(r.kind, "flood", "flood emitted")
  T.eq(r.parent, root, "parent = flow's tag")
  T.eq(W.C.flow[c], r.id, "flow re-tagged with the flood")
  local n = W.E.next_id
  det.update(W)
  T.eq(W.E.next_id, n, "per-cell cooldown")
  W.scratch.flood = {}
end

return M
