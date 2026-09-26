-- Current selection (Stage 1.9), shared by the picking code, the inspector GUI and the bridge.
-- sel = { kind = "cell"|"village"|"person"|"ship"|"animal"|"volcano"|"planet", cell = c,
--         id = entity id, index = list index, species = "deer"|"wolf" }
local M = { sel = nil, version = 0, hover = nil }

function M.set(sel)
  M.sel = sel
  M.version = M.version + 1
end

function M.clear()
  if M.sel ~= nil then M.set(nil) end
end

function M.same(a, b)
  if a == nil or b == nil then return a == b end
  return a.kind == b.kind and a.cell == b.cell and a.id == b.id and a.index == b.index
end

return M
