-- Helpers for the behaviour tests.
local world = require("sim.world")
local daylight = require("sim.society.daylight")
local U = {}

-- Living villages matching pred, largest first (stable by id).
function U.villages(W, pred)
  local out = {}
  for _, v in ipairs(W.ent.villages) do
    if v.alive and (not pred or pred(v)) then out[#out + 1] = v end
  end
  table.sort(out, function(a, b) if a.pop ~= b.pop then return a.pop > b.pop end return a.id < b.id end)
  return out
end

-- Step the world hour by hour until the village's local solar hour is in [h, h + 1).
function U.to_local(W, v, h)
  for _ = 1, 30 do
    local lh = daylight.local_hour(W.hour, W.grid.lon[v.cell])
    if lh >= h and lh < h + 1 then return end
    world.step(W)
  end
  error("to_local failed")
end

-- The hour index of the village's next day step (its local midnight bucket).
function U.steps_to_daystep(W, v)
  return (v.bucket - W.hour % 24) % 24
end

-- Root event kind of event id.
function U.root_kind(W, id)
  local r = W.E.root[id]
  local rec = r and W.E.rec[r]
  return rec and rec.kind
end

-- Events of `kind` whose data concerns village id (data.village or data.from).
function U.events_of(W, kind, vid, since_hour)
  local out = {}
  for _, id in ipairs(W.E.order) do
    local r = W.E.rec[id]
    if r and r.kind == kind and (r.hour or 0) >= (since_hour or 0) and type(r.data) == "table"
      and (r.data.village == vid or r.data.from == vid) then out[#out + 1] = r end
  end
  return out
end

return U
