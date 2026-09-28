-- Livestock (W10, docs/world/18 §2.6, D-058): the village herd.
--
-- Sheep, goats and cattle graze the grass within the village's reach by day, minded by herders,
-- and are penned at night. They breed in the local spring (when the pasture has room), die of
-- age, hunger (more beasts than the pasture carries) and wolves (where the pasture is unguarded
-- and predators are about). They give milk in their season (fresh food), meat (the surplus is
-- slaughtered, and a hungry village eats into its herd, keeping the breeding stock), wool (sheep:
-- a village with enough sheep spins wool — the best cloth source, crafts) and manure (the fields'
-- fertility). A herd is wealth: raiders drive beasts off (conflict).
--
-- Units: heads; "sheep-equivalents" (se) weigh cattle by their grazing (5). v.herd = { sheep,
-- goats, cattle } (heads, fractional in the books), v.herd_cell = today's pasture.
local cfgmod = require("sim.config")
local blame = require("sim.causality.blame")
local fields = require("sim.fields")

local M = {}
local floor, min, max = math.floor, math.min, math.max
local FGRASS = fields.BY_NAME.grass

M.KINDS = { "sheep", "goats", "cattle" }

function M.se(h)
  local cfg = cfgmod.HERDS
  return (h.sheep or 0) * cfg.sheep.graze + (h.goats or 0) * cfg.goats.graze + (h.cattle or 0) * cfg.cattle.graze
end

-- Pasture the village's reach can carry (sheep-equivalents): grass on land that is not field or
-- village, shared with the other villages that reach it.
function M.capacity(W, v)
  local cfg = cfgmod.HERDS
  local G = W.vgeo[v.id]
  local ring = G and G.ring or { v.cell }
  local F = W.F
  local rc = W.soc and W.soc.ring_count
  local cap, best, bg = 0, v.cell, -1e9
  local g = W.grid
  local vx, vy, vz = g.X[v.cell], g.Y[v.cell], g.Z[v.cell]
  local sp2 = g.spacing * g.spacing
  for i = 1, #ring do
    local c = ring[i]
    if W.geo.ocean[c] == 0 and F.land_use[c] == 0 and F.ice[c] < 0.5 then
      local gr = F.grass[c] + F.forest[c] * cfg.forest_browse
      cap = cap + gr * cfg.pasture / max(1, rc and rc[c] or 1)
      -- today's pasture: good grass not far from the pen (herders walk the flock out and back)
      local d2 = 2 * (1 - (g.X[c] * vx + g.Y[c] * vy + g.Z[c] * vz)) / sp2
      local sc = F.grass[c] - 0.08 * d2
      if sc > bg then best, bg = c, sc end
    end
  end
  v.herd_cap, v.herd_cell = cap, best
  return cap
end

-- Weekly for village v: pasture, grazing, births, deaths, milk, manure, slaughter.
function M.weekly(W, v)
  local h = v.herd
  if not h then return end
  local cfg = cfgmod.HERDS
  local se = M.se(h)
  if se <= 0.01 then return end
  local cap = M.capacity(W, v)
  -- the herd a village keeps: what its people need of milk, meat and wool (more where the land is
  -- grass), within what the pasture carries
  local target = min(cap, v.pop * (cfg.per_person[1] + cfg.per_person[2] * (v.grassy or 0.3)))
  v.herd_target = target
  local F, C, I = W.F, W.C, W.I
  local lwk = require("sim.society.farming").local_week(W, v)
  -- grazing: the week's grass taken from the pasture cells in proportion to their grass
  local G = W.vgeo[v.id]
  local ring = G and G.ring or { v.cell }
  local eat = se * cfg.graze_week
  local tot = 0
  for i = 1, #ring do
    local c = ring[i]
    if W.geo.ocean[c] == 0 and F.land_use[c] == 0 then tot = tot + F.grass[c] end
  end
  local grazed = 0
  if tot > 0 then
    local share = min(0.25, eat / tot)
    for i = 1, #ring do
      local c = ring[i]
      if W.geo.ocean[c] == 0 and F.land_use[c] == 0 and F.grass[c] > 0 then
        local d = F.grass[c] * share
        grazed = grazed + d
        F.grass[c] = F.grass[c] - d
        W.P.grass[c] = F.grass[c]
        local vt = v.cause
        if FGRASS and (vt ~= 0 or C.grass[c] ~= 0) then
          blame.step(C.grass, I.grass, c, -d, FGRASS.eps, FGRASS.hold, -d, vt, 0, 0, 0, 0, 0, 0)
        end
      end
    end
  end
  -- beasts breathe out what they eat (the biosphere stays carbon-neutral, as with wild grazers)
  require("sim.society.carbon").release(W, grazed, "grazing")
  -- guarded? the herders' hours this week against what the herd needs
  local need_h = se / cfg.herder_se * (v.plan and v.plan.hours or 9) * 6
  local guard = need_h > 0 and min(1, (v.herd_hours or 0) / need_h) or 1
  v.herd_hours = 0
  local wolves = F.predators[v.herd_cell or v.cell] or 0
  local room = target > 0 and se / target or 9      -- births stop at the herd the village wants
  local crowd = cap > 0 and se / cap or 9           -- hunger beyond what the pasture carries
  local acc = v.acc
  for _, k in ipairs(M.KINDS) do
    local n = h[k] or 0
    if n > 0 then
      local kc = cfg[k]
      -- births in the local spring when the pasture has room
      local born = 0
      if lwk >= cfg.birth_weeks[1] and lwk < cfg.birth_weeks[2] and room < 1 then
        born = n * kc.birth / (cfg.birth_weeks[2] - cfg.birth_weeks[1]) * (1 - room)
      end
      -- deaths: age, hunger beyond the pasture, wolves on an unguarded pasture
      local died = n * kc.death / 48
      if crowd > 1.1 then died = died + n * cfg.hunger_loss * (crowd - 1) end
      local taken = n * cfg.wolf_loss * min(1, wolves / 0.2) * (1 - guard)
      n = n + born - died - taken
      if n < 0 then n = 0 end
      -- milk in season
      if lwk >= cfg.milk_weeks[1] and lwk < cfg.milk_weeks[2] then
        local milk = n * kc.milk * 7 * (crowd > 1 and 1 / crowd or 1)
        v.stock.fresh = v.stock.fresh + milk
        v.wk_fresh = (v.wk_fresh or 0) + milk
        acc.milk = (acc.milk or 0) + milk
      end
      acc["lost_" .. k] = (acc["lost_" .. k] or 0) + taken
      acc["born_" .. k] = (acc["born_" .. k] or 0) + born
      h[k] = n
    end
  end
  -- slaughter: beasts the pasture can't carry, and the hungry eat into the herd (keeping the
  -- breeding stock); meat is eaten fresh or dried into the store
  se = M.se(h)
  local cull = 0
  if se > target * 1.1 then cull = (se - target) * 0.25 end
  if v.food_days < cfg.hungry_days then cull = max(cull, se * cfg.hungry_cull) end
  local keep = (h.base_se or se) * cfg.breeding_share
  if se - cull < keep then cull = max(0, se - keep) end
  if cull > 0 and se > 0 then
    local f = cull / se
    local meat = 0
    for _, k in ipairs(M.KINDS) do
      local n = h[k] or 0
      local x = n * f
      h[k] = n - x
      meat = meat + x * cfg[k].meat
    end
    v.stock.fresh = v.stock.fresh + meat * 0.5
    v.stock.grain = v.stock.grain + meat * 0.5 * cfg.dry_meat
    v.wk_fresh = (v.wk_fresh or 0) + meat * 0.5
    acc.meat_herd = (acc.meat_herd or 0) + meat
  end
  -- manure: the fields' fertility
  local nf = #v.farms
  if nf > 0 then
    local dm = min(cfg.manure_max, se * cfg.manure / nf)
    for i = 1, nf do
      local c = v.farms[i]
      F.fertility[c] = min(1, F.fertility[c] + dm)
    end
  end
  h.base_se = h.base_se and (h.base_se + (M.se(h) - h.base_se) / 48) or M.se(h)
  M.sync(W, v)
end

---------------------------------------------------------------------------------------------
-- Household herds (W8, D-061): each household owns h.beasts sheep-equivalents; the village herd
-- (v.herd, by species) is their sum. What befalls the whole herd (births, deaths, slaughter, a raid
-- suffered) is shared in proportion; transfers (bride-wealth, raiders' spoils, emigrants' beasts,
-- a son's portion) move beasts between particular households.

-- Rescale the households' beasts to the village herd (after whole-herd changes). Households with
-- none yet (old saves, a new village) are given shares by size.
function M.sync(W, v)
  local H = W.ent.households
  local se = v.herd and M.se(v.herd) or 0
  local tot, n = 0, 0
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then tot = tot + (h.beasts or 0); n = n + #h.members end
  end
  if tot <= 1e-9 then
    for i = 1, #v.households do
      local h = H[v.households[i]]
      if h then h.beasts = n > 0 and se * #h.members / n or 0 end
    end
    return
  end
  local f = se / tot
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then h.beasts = (h.beasts or 0) * f end
  end
end

-- Take `se` sheep-equivalents out of village v's herd (species in proportion); returns the beasts
-- by species (for a party to drive, or to add to another herd).
function M.take(v, se)
  local h = v.herd
  local tot = h and M.se(h) or 0
  if tot <= 0 or se <= 0 then return nil end
  local f = math.min(1, se / tot)
  local out = {}
  for _, k in ipairs(M.KINDS) do
    local x = (h[k] or 0) * f
    h[k] = (h[k] or 0) - x
    out[k] = x
  end
  return out
end

-- Add beasts (by species) to village v's herd.
function M.add(v, beasts)
  if not beasts then return end
  local h = v.herd
  if not h then h = { sheep = 0, goats = 0, cattle = 0 }; v.herd = h end
  for _, k in ipairs(M.KINDS) do h[k] = (h[k] or 0) + (beasts[k] or 0) end
end

-- Move `se` from household a (village va) to household b (village vb).
function M.give(W, va, a, vb, b, se)
  if se <= 0 then return end
  a.beasts = (a.beasts or 0) - se
  b.beasts = (b.beasts or 0) + se
  if va ~= vb then M.add(vb, M.take(va, se)) end
end

-- Herding hours worked today (labour.execute).
function M.work(W, v, eff)
  v.herd_hours = (v.herd_hours or 0) + eff
end

-- Labour demand (hours this week) for minding the herd.
function M.demand(W, v, hours_day)
  local h = v.herd
  if not h then return 0 end
  return M.se(h) / cfgmod.HERDS.herder_se * hours_day * cfgmod.SOCIETY.work_days
end

-- Wool: how well a village's sheep clothe it (0..1): a cloth source (crafts).
function M.wool_q(W, v)
  local h = v.herd
  if not h or v.pop <= 0 then return 0 end
  return min(1, (h.sheep or 0) / (v.pop * cfgmod.HERDS.sheep_per_person))
end

-- A starting herd for a village of pop people on its land (worldgen, founding): pastoral where
-- the land is grass, a few beasts where it is forest.
function M.seed(W, v, share)
  local cfg = cfgmod.HERDS
  local F = W.F
  local G = W.vgeo[v.id]
  local ring = G and G.ring or { v.cell }
  local g, n = 0, 0
  for i = 1, #ring do
    local c = ring[i]
    if W.geo.ocean[c] == 0 then g = g + F.grass[c]; n = n + 1 end
  end
  local grassy = n > 0 and g / n or 0.3
  v.grassy = grassy
  local k = (share or 1) * v.pop * (0.3 + grassy * 1.4)
  v.herd = { sheep = k * 0.6, goats = k * 0.25, cattle = k * 0.06 }
  v.herd.base_se = M.se(v.herd)
end

return M
