-- Contributing causes for society events (D-054, 03 extended). No requires: shared by
-- economy, mobility and farming without require cycles (the Defold build rejects them).
local M = {}

-- The player's recent hand in a village's troubles (D-054): the latest player-rooted cause tag
-- that touched its stocks, harvest, workforce or arrivals, for 5 years. Society events name it
-- as a contributing cause when their own parent is something else.
function M.co_of(W, v)
  local t = v and v.player_tag
  if t and t ~= 0 and W.hour - (v.player_tag_h or -1e9) < 5 * 8064 then return t end
  return nil
end

function M.note_tags(W, v)
  local E = W.E
  local function check(t)
    if t and t ~= 0 then
      local root = E.root[t]
      local r = root and E.rec[root]
      if r and r.actor == "player" then v.player_tag, v.player_tag_h = t, W.hour end
    end
  end
  check(v.foodtag[1]); check(v.foodtag[3]); check(v.cause); check(v.labour_tag); check(v.harvest_tag)
  check(v.arrivals_tag); check(v.fresh_tag); check(v.import_tag)
end

return M
