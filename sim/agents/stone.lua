-- Stone (S19, roadmap v3 R4: geography -> resources -> livelihoods). Usable stone lies only where
-- the land breaks into rock: on a settlement's map, outcrops stand where the planet's cell under the
-- ground is steep or high (geo.slope, static.elev) - hard rock (granite, basalt: good for querns) on
-- the high and steep ground of the mountains, softer rock (sandstone, limestone) on lower broken
-- ground; a lowland village has none. An outcrop is a natural feature on the map (kind `outcrop`,
-- built by nobody), holding so many stones (b.left) that the village's people can quarry.
--
-- The loop: the village wants stone (footings for the houses and granaries it raises where stone is
-- at hand, querns for its households, querns to trade when a neighbour is heard to want them) ->
-- a grown-up walks to an outcrop and splits a stone from it (WORK.quarry: it lies at the face) ->
-- someone carries it, one at a time (heavy), to a building site that needs it or to the yard by the
-- woodstore -> it is used: laid as a footing (a house on stone footings weathers slower), or two hard
-- stones shaped by a household into a rotary quern (grinding for the pot goes faster: cooking takes
-- less time), which is carried home and set up; a household with a quern to spare may trade it.
--   s.rocks = true once the map's outcrops are laid out (saved)
--   outcrop b: { rock = "hard"|"soft", left = stones, stock.stone = stones lying at the face }
--   yard: s.wood_store.stock.stone / .hardstone
--   site b: stone_need, stone_have (footings); house b.footing = true
--   hh.quern = condition 0..1 (a quern in use); home.stock.quern = querns to spare
--   hh.quern_work = hours of shaping done on the next quern (at the yard)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min = math.floor, math.max, math.min
local function C() return cfgmod.V3 end

-- How rocky planet cell c is (0 none .. 1) and what rock: "hard" on high, steep ground, "soft" on
-- lower broken ground, nil where there is none to be had.
function M.rock_of(W, c)
  local cc = C()
  local slope = W.geo.slope and W.geo.slope[c] or 0
  local elev = W.static.elev[c] - (W.G.sea_level or 0)
  local r = max(0, min(1, (slope - cc.rock_slope) / cc.rock_slope_span)) * 0.7 + max(0, min(1, (elev - cc.rock_elev) / cc.rock_elev_span)) * 0.5
  if r <= 0 then return 0, nil end
  local hard = elev >= cc.rock_hard_elev or slope >= cc.rock_hard_slope
  return min(1, r), hard and "hard" or "soft"
end

-- Lay out the outcrops of settlement s (once: the rock was always there; it is placed the first
-- day the village looks at its land with this in mind). Candidates on a lattice of the map, each
-- placed by its cell's rockiness; the footprint must be dry, open land (no river or floodplain, no
-- building, field or pen); trees on bare rock never stood (cleared with the footprint).
function M.outcrops(W, s)
  if s.rocks then return end
  s.rocks = true
  local c = C()
  local settle = M.settle
  local N, step = s.N, c.rock_step
  local land, riv, occ, plot = s.land, s.riv, s.occ, s.plot
  local half = N / 2
  local n = 0
  for iy0 = 4, N - 6, step do
    for ix0 = 4, N - 6, step do
      local i = (iy0 - 1) * N + ix0
      local r, kind = M.rock_of(W, s.cell_of[i])
      if kind then
        local u = rng.float(W.seed, rng.S.V3_MAP, s.key, i, 91)
        if u < r * c.rock_density then
          -- (a size by the draw: 2x2 or 3x2, not within the village's heart)
          local w = rng.float(W.seed, rng.S.V3_MAP, s.key, i, 92) < 0.5 and 2 or 3
          local h = 2
          local cx, cy = (ix0 - half) * s.T, (iy0 - half) * s.T
          local ok = cx * cx + cy * cy > c.rock_keep * c.rock_keep
          for yy = iy0 - 1, iy0 + h do
            for xx = ix0 - 1, ix0 + w do
              local j = (yy - 1) * N + xx
              if land[j] ~= 1 or riv[j] ~= 0 or occ[j] ~= 0 or plot[j] ~= 0 then ok = false end
            end
          end
          if ok then
            -- (the face people work: the side toward the village's square)
            local b = settle.add_building(W, s, "outcrop", ix0, iy0, cy > 0 and "s" or "n", "standing", { rock = kind, natural = true, w = w, h = h })
            b.left = floor(c.rock_stones[1] + (c.rock_stones[2] - c.rock_stones[1]) * rng.float(W.seed, rng.S.V3_MAP, s.key, i, 93))
            b.label = kind == "hard" and "outcrop of hard rock" or "outcrop of soft rock"
            n = n + 1
          end
        end
      end
    end
  end
  s.n_rocks = n
end

-- The village's outcrops (standing, with stone left), and whether it has hard rock.
function M.faces(s)
  local L, hard = {}, false
  for k = 1, #s.blist do
    local b = s.buildings[s.blist[k]]
    if b.kind == "outcrop" and ((b.left or 0) > 0 or (b.stock.stone or 0) > 0) then
      L[#L + 1] = b
      if b.rock == "hard" and (b.left or 0) > 0 then hard = true end
    end
  end
  return L, hard
end

-- Stones in the yard (by the woodstore): soft, hard.
function M.yard(s)
  local y = s.wood_store
  if not y or y.state ~= "standing" then return 0, 0 end
  return y.stock.stone or 0, y.stock.hardstone or 0
end

-- A household's quern at work (condition), or 0.
function M.quern(hh) return hh and hh.quern or 0 end

-- Each morning (settle.daily): the outcrops laid out (once); a quern to spare set up by a household
-- that has none; the stone the village wants and how much of it must be hard (s.need.stone,
-- s.need.hardstone), and why.
function M.daily(W, s, now)
  local c = C()
  local V = W.S.v3
  M.outcrops(W, s)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
    if home and (hh.quern or 0) < c.quern_broken and (home.stock.quern or 0) >= 1 then
      home.stock.quern = home.stock.quern - 1
      hh.quern = 1
    end
  end
  local faces, hard = M.faces(s)
  s.has_rock, s.has_hard = #faces > 0, hard
  local need = s.need
  need.stone, need.hardstone, need.stone_why = 0, 0, nil
  if #faces == 0 then return end
  local ys, yh = M.yard(s)
  local lying, lying_h = 0, 0
  for _, b in ipairs(faces) do
    local q = b.stock.stone or 0
    if b.rock == "hard" then lying_h = lying_h + q else lying = lying + q end
  end
  -- footings: the sites waiting for stone (a site whose walls and roof are done and whose stones
  -- have not come for footing_wait days is finished on what was laid: timber sills for the rest)
  local sites = 0
  local done = nil
  for i = 1, #s.sites do
    local b = s.buildings[s.sites[i]]
    if b and b.state == "site" and (b.stone_need or 0) > (b.stone_have or 0) then
      if b.work_done >= b.work_need and b.logs_have >= b.logs_need then
        b.stone_wait = (b.stone_wait or 0) + 1
        if b.stone_wait >= c.footing_wait then done = done or {}; done[#done + 1] = b end
      end
      sites = sites + b.stone_need - b.stone_have
    end
  end
  for _, b in ipairs(done or {}) do
    b.stone_need = b.stone_have or 0
    if M.settle then M.settle.complete_building(W, s, b) end
  end
  -- querns: households without one (one at a time), and one to trade when a neighbour wants it
  local querns = 0
  if hard then
    for _, hid in ipairs(s.households) do
      local hh = V.households[hid]
      if hh and #hh.members > 0 and (hh.quern or 0) < c.quern_broken then querns = querns + 1 end
    end
    if s.export and s.export.quern then querns = querns + 1 end
  end
  local want_h = min(querns, c.quern_at_once) * 2
  local want = sites + want_h + c.stone_reserve
  local have = ys + yh + lying + lying_h
  need.stone = max(0, want - have)
  need.hardstone = max(0, want_h - yh - lying_h)
  if need.stone > 0 then
    if sites > 0 then
      need.stone_why = sfmt("The village wants %d stones for footings", sites)
    elseif want_h > 0 then
      need.stone_why = "Hard stone is wanted for querns"
    else
      need.stone_why = "A few stones laid by in the yard"
    end
  end
  s.stone_want, s.stone_have = want, have
end

-- (S19) Whether a site planned now at village s gets stone footings: where stone is at hand (an
-- outcrop on its map) houses and granaries are raised on a footing of stones.
function M.footing(W, s, kind)
  if kind ~= "house" and kind ~= "granary" then return 0 end
  local faces = M.faces(s)
  if #faces == 0 then return 0 end
  return C().footing_stones
end

return M
