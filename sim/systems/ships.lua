-- Tankers & boats (02_WORLD_MODEL.md §5.11, system slot 11).
--
-- Entity schema (stable; the renderer reads it):
--   W.static.lanes[i] = { cells = { closed loop of adjacent ocean cells }, village = id it passes,
--                         replace_at = tick a replacement tanker is due (-1 = none) }
--   W.ent.ships       array of tankers, ids = indices (sunk ones stay with alive = false):
--     { id, kind = "tanker", name, lane, pos (float index along the lane, 0-based), cell,
--       alive, sunk (tick), key }
--   W.ent.boats       fishing boats (visual; rebuilt monthly from each village's boat count):
--     { village, cell } — Lightning targets (Chapter 2) pick from here.
-- A sinking tanker releases CARGO oil at its position (tanker_sunk event -> oil tag); a
-- replacement arrives 3–5 years later if at least 2 villages are alive.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local events = require("sim.causality.events")

local sys = { name = "ships" }
local floor = math.floor

function sys.init(W)
  W.ent.ships = W.ent.ships or {}
  W.static.lanes = W.static.lanes or {}
end

function sys.new_tanker(W, li, pos)
  local ships = W.ent.ships
  local id = #ships + 1
  local lane = W.static.lanes[li]
  local key = rng.hash(W.seed, rng.S.SHIPS, li, W.tick, id)
  local ctr = 0
  local function u()
    ctr = ctr + 1
    return rng.float(W.seed, rng.S.NAMES, key, ctr, 0)
  end
  local used = {}
  for i = 1, #ships do used[ships[i].name] = true end
  local p = pos or 0
  local t = { id = id, kind = "tanker", name = names.unique(u, names.tanker, used), lane = li, pos = p,
    cell = lane.cells[floor(p) % #lane.cells + 1], alive = true, key = key }
  ships[id] = t
  return t
end

-- Sink tanker t (parent = the event that sank it). Returns the tanker_sunk event id.
function sys.sink(W, t, parent)
  if not t.alive then return 0 end
  t.alive = false
  t.sunk = W.tick
  local id = events.emit(W, "tanker_sunk", t.cell, parent or 0, 1, { ship = t.id, name = t.name })
  require("sim.systems.ocean").spill(W, t.cell, cfgmod.SHIPS.cargo, id ~= 0 and id or (parent or 0))
  local cfg = cfgmod.SHIPS
  local lane = W.static.lanes[t.lane]
  local yrs = cfg.replace_years[1] + rng.float(W.seed, rng.S.SHIPS, t.lane, W.tick, 1) * (cfg.replace_years[2] - cfg.replace_years[1])
  lane.replace_at = W.tick + floor(yrs * 48)
  return id
end

function sys.update(W)
  local ships = W.ent.ships
  local lanes = W.static.lanes
  local sp = cfgmod.SHIPS.tanker_speed / W.sscale
  for i = 1, #ships do
    local t = ships[i]
    if t.alive then
      local cells = lanes[t.lane].cells
      local n = #cells
      local p = t.pos + sp
      if p >= n then p = p - n end
      t.pos = p
      t.cell = cells[floor(p) + 1]
    end
  end
  local tick = W.tick
  -- replacements ("The Tanker Company regrets nothing")
  for li = 1, #lanes do
    local lane = lanes[li]
    if lane.replace_at >= 0 and tick >= lane.replace_at then
      local alive = 0
      local vs = W.ent.villages or {}
      for k = 1, #vs do if vs[k].alive then alive = alive + 1 end end
      if alive >= 2 then
        sys.new_tanker(W, li, 0)
        lane.replace_at = -1
      else
        lane.replace_at = tick + 48   -- try again next year
      end
    end
  end
end

function sys.shift_ticks(W, dt)
  local ships = W.ent.ships or {}
  for i = 1, #ships do if ships[i].sunk then ships[i].sunk = ships[i].sunk + dt end end
  local lanes = W.static.lanes or {}
  for i = 1, #lanes do if lanes[i].replace_at >= 0 then lanes[i].replace_at = lanes[i].replace_at + dt end end
end

return sys
