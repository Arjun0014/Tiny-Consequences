-- Tutorial world helpers (04_PLAYER.md §6, Stage 2.1). Pure Lua 5.1.
-- The tutorial strikes the tanker nearest a fishing village that has fishers.
local M = {}

M.SEED = 8   -- assets/worlds/tutorial.tcsave (tools/tutorial_scout.lua; S15)

--- The tanker closest to a fishing village with fishers: tanker, distance (level-4 steps),
-- village; or nil.
function M.pick(W)
  local g = W.grid
  local best, bd, bv = nil, 1e9, nil
  for _, t in ipairs(W.ent.ships or {}) do
    if t.alive and not t.sunk then
      for _, v in ipairs(W.ent.villages) do
        if v.alive and #v.fishing > 0 and (v.fish_jobs or 0) > 0 then
          local d = g.X[t.cell] * g.X[v.cell] + g.Y[t.cell] * g.Y[v.cell] + g.Z[t.cell] * g.Z[v.cell]
          local steps = math.acos(math.max(-1, math.min(1, d))) / 0.07553
          if steps < bd then best, bd, bv = t, steps, v end
        end
      end
    end
  end
  return best, bd, bv
end

return M
