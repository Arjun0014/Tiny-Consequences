-- View event feed (renderer v2): the view reads the sim's new events each frame, read-only, by
-- keeping its own cursor into the event ids (W.E.next_id). Effects subscribe by kind. Events
-- that happen while the view is loading or far in the past are skipped (reset()).
local M = {}

M.last = 0
M.listeners = {}      -- kind -> { fn, ... }
M.any = {}            -- listeners for every event

function M.reset(W)
  M.last = (W and W.E and W.E.next_id or 1) - 1
end

--- fn(W, record) for events of `kind` (nil = every kind).
function M.on(kind, fn)
  if kind then
    local l = M.listeners[kind]
    if not l then l = {}; M.listeners[kind] = l end
    l[#l + 1] = fn
  else
    M.any[#M.any + 1] = fn
  end
end

function M.poll(W)
  local E = W and W.E
  if not E then return end
  local last = E.next_id - 1
  if last < M.last then M.last = last end          -- a new world
  -- at very high speeds only the newest few hundred events matter for effects
  if last - M.last > 400 then M.last = last - 400 end
  for id = M.last + 1, last do
    local r = E.rec[id]
    if r then
      local l = M.listeners[r.kind]
      if l then for i = 1, #l do l[i](W, r) end end
      for i = 1, #M.any do M.any[i](W, r) end
    end
  end
  M.last = last
end

return M
