local T = require("tests.sim.lib")
local events = require("sim.causality.events")
local M = {}

local function mkworld()
  return { tick = 0, E = events.new(), region = nil }
end

function M.chain_and_roots()
  local W = mkworld()
  local r = events.emit(W, "test_remove_predators", 5, 0, 1)
  local a = events.emit(W, "population_crash", 5, r, 0.5)
  local b = events.emit(W, "population_boom", 6, a, 2)
  local c = events.emit(W, "biome_shift", 6, b, 1)
  local ch = events.chain(W, c)
  T.eq(#ch, 4)
  T.eq(ch[1].id, r); T.eq(ch[4].id, c)
  T.eq(events.get(W, c).root, r)
  T.eq(events.get(W, r).actor, "player")
  T.eq(events.get(W, a).actor, "derived")
  T.eq(events.get(W, r).desc, 3)
  T.eq(events.get(W, a).desc, 2)
  T.ok(events.is_player_rooted(W, c))
  T.eq(events.chain_length(W, c), 4)
  local d = events.descendants(W, r)
  T.eq(#d, 3)
  -- derived event with no parent becomes a nature root
  local n = events.emit(W, "famine", 3, 0, 1)
  T.eq(events.get(W, n).actor, "nature")
  T.ok(not events.is_player_rooted(W, n))
end

function M.headlines()
  local W = mkworld()
  local w1 = events.emit(W, "wildfire", 1, 0, 5)
  T.ok(not events.get(W, w1).headline, "below hmin")
  local w2 = events.emit(W, "wildfire", 1, 0, 12)
  T.ok(events.get(W, w2).headline)
  T.eq(W.E.last_headline, w2)
end

function M.aggregation()
  local W = mkworld()
  local r = events.emit(W, "test_ignite", 1, 0, 1)
  local a = events.emit(W, "fire_started", 1, r, 1)
  W.tick = 2
  local b = events.emit(W, "fire_started", 2, r, 1)
  T.eq(a, b, "merged within window")
  T.eq(events.get(W, a).mag, 2)
  W.tick = 20
  local c = events.emit(W, "fire_started", 2, r, 1)
  T.ok(c ~= a, "new event after window")
end

function M.gc_drops_unpinned_keeps_chain()
  local W = mkworld()
  W.E.cap = 100
  local root = events.emit(W, "test_generic", 1, 0, 1)
  local prev = root
  for i = 1, 150 do
    W.tick = i
    prev = events.emit(W, "crop_failure", 1, prev, 1)
  end
  local pinned = { [root] = true }
  local dropped = events.gc(W, pinned)
  T.ok(dropped > 0)
  T.ok(#W.E.order <= 100)
  T.ok(events.get(W, root) ~= nil, "pinned root kept")
  -- the last event's chain still reaches the root through dropped links
  local ch = events.chain(W, prev)
  T.eq(ch[1].id, root)
  T.eq(W.E.root[prev], root)
end

return M
