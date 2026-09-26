-- Shared UI state (Stage 1.9): written by the HUD GUI script, read by main.script and the
-- bridge (shared_state = 1). Screen rects are window pixels {x0, y0, x1, y1} (y up).
local M = {
  bounds = {},            -- name -> rect of every visible HUD panel (W09 overlap check)
  inspector_collapsed = false,
  debug = false,          -- debug overlay (F3; always on with ?test=1)
  pointer_over_ui = false,
  requests = {},          -- GUI -> main: { cmd = "fly_to", cell = c } etc.
  edge = {},              -- edge indicators: id -> { x, y, z (world), icon, label }
  debug_lines = nil,      -- filled by main.script for the overlay
}

function M.request(r)
  M.requests[#M.requests + 1] = r
end

function M.pop_request()
  local r = M.requests[1]
  if r then table.remove(M.requests, 1) end
  return r
end

--- Edge-of-screen indicator API (stub for Chapter 2 headlines): show one for a world point.
function M.edge_show(id, x, y, z, icon, label)
  M.edge[id] = { x = x, y = y, z = z, icon = icon or "info", label = label }
end

function M.edge_hide(id)
  M.edge[id] = nil
end

return M
