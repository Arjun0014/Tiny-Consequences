local T = require("tests.sim.lib")
local blame = require("sim.causality.blame")
local M = {}

function M.dominant_term_takes_tag()
  local tags, idle = { 0 }, { 0 }
  -- decrease dominated by term tagged 7
  blame.step(tags, idle, 1, -0.5, 0.01, 48, 0.1, 0, -0.6, 7, 0, 0, 0, 0)
  T.eq(tags[1], 7)
end

function M.margin_prevents_flicker()
  local tags, idle = { 3 }, { 0 }
  -- tag 9 is dominant but only 1.2x the tag-3 term: keep 3
  blame.step(tags, idle, 1, -1.1, 0.01, 48, -0.5, 3, -0.6, 9, 0, 0, 0, 0)
  T.eq(tags[1], 3)
  -- now 2x: switch
  blame.step(tags, idle, 1, -1.5, 0.01, 48, -0.5, 3, -1.0, 9, 0, 0, 0, 0)
  T.eq(tags[1], 9)
end

function M.same_tag_terms_dont_block()
  local tags, idle = { 0 }, { 0 }
  -- two terms with the same tag: second (different-tag) is 0, so switch
  blame.step(tags, idle, 1, 1.0, 0.01, 48, 0.5, 4, 0.5, 4, 0, 0, 0, 0)
  T.eq(tags[1], 4)
end

function M.opposite_direction_terms_ignored()
  local tags, idle = { 0 }, { 0 }
  -- big negative term tagged 5, but the net change is positive and driven by baseline
  blame.step(tags, idle, 1, 0.2, 0.01, 48, 1.0, 0, -0.8, 5, 0, 0, 0, 0)
  T.eq(tags[1], 0)
end

function M.idle_reset()
  local tags, idle = { 8 }, { 0 }
  for _ = 1, 47 do blame.step(tags, idle, 1, 0.0, 0.01, 48, 0, 0, 0, 0, 0, 0, 0, 0) end
  T.eq(tags[1], 8)
  blame.step(tags, idle, 1, 0.0, 0.01, 48, 0, 0, 0, 0, 0, 0, 0, 0)
  T.eq(tags[1], 0, "reset after 48 idle ticks")
end

function M.baseline_dominance_resets_after_hold()
  local tags, idle = { 8 }, { 0 }
  for _ = 1, 47 do blame.step(tags, idle, 1, 0.5, 0.01, 48, 0.5, 0, 0, 0, 0, 0, 0, 0) end
  T.eq(tags[1], 8)
  blame.step(tags, idle, 1, 0.5, 0.01, 48, 0.5, 0, 0, 0, 0, 0, 0, 0)
  T.eq(tags[1], 0)
end

function M.reinforcement_resets_idle()
  local tags, idle = { 8 }, { 40 }
  blame.step(tags, idle, 1, 0.5, 0.01, 48, 0.5, 8, 0, 0, 0, 0, 0, 0)
  T.eq(idle[1], 0)
end

function M.inflow_tag()
  -- cell 1 with neighbours 2,3,4; weights 0.2,0.5,0.3; vals 1; tags 0, 6, 0
  local nbr = { 2, 3, 4, 0, 0, 0 }
  local w = { 0.2, 0.5, 0.3, 0, 0, 0 }
  local vals = { 0, 1, 1, 1 }
  local tags = { 0, 0, 6, 0 }
  T.eq(blame.inflow_tag(nbr, 3, 0, w, vals, tags, 0.3), 6)
  T.eq(blame.inflow_tag(nbr, 3, 0, w, vals, tags, 0.6), 0)
end

return M
