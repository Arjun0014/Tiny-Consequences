-- Iron (S19, roadmap v3 R4: geography -> resources -> livelihoods). Iron is had only where its ore
-- lies: a vein in some hard-rock outcrops of the mountains, or bog iron - the rusty nodules that
-- gather in wet low ground beside a river - on some lowland maps; most villages have neither. A
-- deposit is known once people have walked by it (red earth, rusty water: the wear of feet near it).
--
-- The chain, every step a person's work: ore dug at the deposit (it lies there) and carried to the
-- forge; logs stacked in a charcoal clamp, covered with turf and fired, left to char for a day and
-- more, the clamp opened and the charcoal raked out and carried to the forge; ore smelted with
-- charcoal in the forge's bloomery into a bloom of iron; the bloom forged with more charcoal into
-- iron edges for a household's tools (axe bit, sickle, hoe and spade edges, knife), carried to the
-- household that lacks them. A household's iron edges (hh.iron 0..1) make the work of its tools
-- faster and wear its other tools slower; they wear down with the work. A village with a forge may
-- make iron to trade when a neighbour is heard to want it.
--   deposits: outcrop b.ore (vein, measures) or kind `bog` (natural) b.ore; b.stock.ore lying dug;
--             b.known (hour) once people have walked by it
--   forge b: stock.ore, stock.charcoal, stock.bloom; clamp b: charge (logs in), lit_until, stock.charcoal
--   s.need.ore / charcoal / bloom / iron and s.iron_why
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min = math.floor, math.max, math.min
local function C() return cfgmod.V3 end

-- Lay out the ore of settlement s (once: s.ores): veins in hard outcrops, bog iron in wet low ground
-- near the river.
function M.deposits(W, s)
  if s.ores then return end
  if not s.rocks then return end      -- (after the outcrops: stone.lua)
  s.ores = true
  local c = C()
  local N = s.N
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b.kind == "outcrop" and b.rock == "hard" then
      if rng.float(W.seed, rng.S.V3_MAP, s.key, b.id, 95) < c.ore_vein_p then
        b.ore = floor(c.ore_vein[1] + (c.ore_vein[2] - c.ore_vein[1]) * rng.float(W.seed, rng.S.V3_MAP, s.key, b.id, 96))
        b.label = "outcrop of hard rock, with a vein of iron ore"
      end
    end
  end
  -- bog iron: wet low ground beside the floodplain on maps whose ground is wet and low - some such
  -- maps have it (bog_map_p), at the candidate the draw favours
  local settle = M.settle
  local land, riv, occ, plot = s.land, s.riv, s.occ, s.plot
  if rng.float(W.seed, rng.S.V3_MAP, s.key, 0, 94) >= c.bog_map_p then return end
  local best, bu = nil, 2
  for iy0 = 6, N - 8, 2 do
    for ix0 = 6, N - 8, 2 do
      local i = (iy0 - 1) * N + ix0
      local cell = s.cell_of[i]
      local wet = W.F.soil_water[cell] or 0
      local elev = W.static.elev[cell] - (W.G.sea_level or 0)
      if wet >= c.bog_wet and elev <= c.bog_elev then
        -- (beside the floodplain: a floodplain tile within three of the footprint, none under it)
        local near, ok = false, true
        for yy = iy0 - 3, iy0 + 4 do
          for xx = ix0 - 3, ix0 + 4 do
            if riv[(yy - 1) * N + xx] == -1 then near = true end
          end
        end
        if near then
          for yy = iy0 - 1, iy0 + 2 do
            for xx = ix0 - 1, ix0 + 2 do
              local j = (yy - 1) * N + xx
              if land[j] ~= 1 or riv[j] ~= 0 or occ[j] ~= 0 or plot[j] ~= 0 then ok = false end
            end
          end
          if ok then
            local u = rng.float(W.seed, rng.S.V3_MAP, s.key, i, 97)
            if u < bu then best, bu = i, u end
          end
        end
      end
    end
  end
  if best then
    local ix0, iy0 = (best - 1) % N + 1, floor((best - 1) / N) + 1
    local b = settle.add_building(W, s, "bog", ix0, iy0, "s", "standing", { natural = true })
    b.ore = floor(c.ore_bog[1] + (c.ore_bog[2] - c.ore_bog[1]) * rng.float(W.seed, rng.S.V3_MAP, s.key, best, 98))
    b.label = "bog with iron in it"
  end
end

-- The village's deposits (with ore left or dug ore lying), known or not.
function M.ore_sites(s)
  local L = {}
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if (b.kind == "outcrop" or b.kind == "bog") and ((b.ore or 0) > 0 or (b.stock.ore or 0) > 0) then L[#L + 1] = b end
  end
  return L
end

-- Does the village know deposit b? The ground near its houses it knows (rusty water in the meadow by
-- the river, red earth in the rock); farther out, once someone has walked by (worn ground within a
-- few tiles).
local function seen(s, b)
  local N = s.N
  local dx, dy = b.cx - (s.sqx or 0), b.cy - (s.sqy or 0)
  if dx * dx + dy * dy < C().ore_near * C().ore_near then return true end
  for iy = b.iy0 - 5, b.iy0 + b.h + 4 do
    for ix = b.ix0 - 5, b.ix0 + b.w + 4 do
      if ix >= 1 and iy >= 1 and ix <= N and iy <= N then
        if s.wear[(iy - 1) * N + ix] > 0.3 then return true end
      end
    end
  end
  return false
end

-- A household's iron edges in use (condition), or 0.
function M.iron(hh) return hh and (hh.iron or 0) >= C().iron_broken and hh.iron or 0 end

-- The village's forge and clamp (standing or being built), or nil.
function M.forge(s) return s.forge end
function M.clamp(s) return s.clamp end

-- Each morning (settle.daily, after stone.daily): deposits (once), which are known, what the
-- village wants of ore, charcoal, blooms and iron, and the forge and clamp planned when wanted.
function M.daily(W, s, now)
  local c = C()
  local V = W.S.v3
  M.deposits(W, s)
  local need = s.need
  need.ore, need.charcoal, need.bloom, need.iron, s.iron_why = 0, 0, 0, 0, nil
  s.clamp_target = 0
  local known = false
  for _, b in ipairs(M.ore_sites(s)) do
    if not b.known and seen(s, b) then
      b.known = now
      s.ore_found = s.ore_found or now
    end
    if b.known then known = true end
  end
  s.ore_known = known
  -- a forge's iron set up by a household that has none (one made to spare, or brought home)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
    if home and M.iron(hh) == 0 and (home.stock.iron or 0) >= 1 then
      home.stock.iron = home.stock.iron - 1
      hh.iron = 1
    end
  end
  if not known and not s.forge then return end
  -- households with iron edges worn out or none (one set at a time), and one to trade when a
  -- neighbour wants it
  local lack = 0
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 and M.iron(hh) < c.iron_redo then lack = lack + 1 end
  end
  -- (to trade: while fewer than export_stock sets lie to spare in the village)
  if s.export and s.export.iron then
    local spare = 0
    for _, hid in ipairs(s.households) do
      local hh = V.households[hid]
      local home = hh and s.buildings[hh.home or 0]
      if home then spare = spare + (home.stock.iron or 0) end
    end
    if spare < c.export_stock then lack = lack + 1 end
  end
  if lack == 0 then return end
  local f = s.forge
  local fs = f and f.stock or {}
  local want_iron = min(lack, c.iron_at_once)
  local blooms = max(0, want_iron - (fs.bloom or 0))
  need.iron = want_iron
  need.bloom = blooms
  local lying = 0
  for _, b in ipairs(M.ore_sites(s)) do lying = lying + (b.stock.ore or 0) end
  need.ore = known and max(0, blooms * c.smelt_ore - (fs.ore or 0) - lying) or 0
  local cl = s.clamp
  -- (a clamp burning counts: its charge is charcoal to come)
  local char_have = (fs.charcoal or 0) + (cl and (cl.stock.charcoal or 0) + (cl.lit_until and (cl.charge or 0) * c.charcoal_log or 0) or 0)
  need.charcoal = max(0, blooms * c.smelt_charcoal + want_iron * c.forge_charcoal - char_have)
  -- (the clamp is charged with the logs that charcoal asks for, a full clamp at most, and fired
  -- when it holds them)
  if cl then s.clamp_target = min(c.clamp_logs, math.ceil(need.charcoal / c.charcoal_log - 1e-6)) end
  s.iron_why = sfmt("%d households of %s work with no iron to their tools", lack, s.name)
  -- the forge and the clamp, made when there is ore known and iron wanted (one at a time)
  if known and not s.forge and not s.lot and M.settle then
    M.settle.plan_lot(W, s, "forge", { prio = 1 }, s.wood_x, s.wood_y)
  end
  if s.forge and not s.clamp and not s.lot and M.settle and need.charcoal > 0 then
    M.settle.plan_lot(W, s, "clamp", { prio = 1 }, s.forge.cx, s.forge.cy)
  end
end

return M
