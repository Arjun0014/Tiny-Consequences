-- Rivers of a v3 world (S14, D-112): the drainage basins (sim/geo.lua) and the rivers drawn on the
-- planet and laid on the settlements' maps from them (sim/surface.lua, sim/agents/map.lua) - on
-- the shipped world.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local surface = require("sim.surface")

local M = {}
local SHIP

local function shipped()
  if not SHIP then
    local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
    SHIP = assert(worlds.from_save_string(f:read("*a")))
    f:close()
  end
  return SHIP
end

-- The network: river cells, each one's river cells upstream, the sources and chains.
local function network(W)
  local geo = W.geo
  local wd = surface.river_widths(W)
  local down, ocean = geo.downhill, geo.ocean
  local ups = {}
  for c = 1, W.n do
    if ocean[c] == 0 and wd[c] > 0 then
      local d = down[c]
      if d ~= 0 and ocean[d] == 0 then ups[d] = (ups[d] or 0) + 1 end
    end
  end
  local sources = {}
  for c = 1, W.n do
    if ocean[c] == 0 and wd[c] > 0 and not ups[c] then sources[#sources + 1] = c end
  end
  -- each source's chain to its end
  local function chain(c)
    local out = { c }
    while true do
      local d = down[c]
      if d == 0 or ocean[d] == 1 or #out > 400 then return out, d end
      out[#out + 1] = d
      c = d
    end
  end
  return { wd = wd, ups = ups, sources = sources, chain = chain }
end

-- Every river runs downhill: its bed falls from each cell to the next, all the way to the sea.
function M.rivers_flow_downhill()
  local W = shipped()
  local N = network(W)
  local bed, down, ocean = W.geo.bed, W.geo.downhill, W.geo.ocean
  T.ok(bed ~= nil, "the world drains by basins (W.static.basins)")
  local links, bad = 0, 0
  for c = 1, W.n do
    if ocean[c] == 0 and N.wd[c] > 0 then
      local d = down[c]
      if d ~= 0 and ocean[d] == 0 then
        links = links + 1
        if bed[d] >= bed[c] then bad = bad + 1 end
      end
    end
  end
  T.ok(links >= 100, "river links " .. links)
  T.eq(bad, 0, "links whose bed does not fall")
end

-- A river's source is higher than its mouth, and the great rivers rise in the highlands.
function M.sources_are_above_mouths()
  local W = shipped()
  local N = network(W)
  local bed = W.geo.bed
  local fm = W.S.hydro.flow_mean
  for _, s in ipairs(N.sources) do
    local ch = N.chain(s)
    T.ok(bed[s] > bed[ch[#ch]] or #ch == 1, string.format("source %d (%.2f km) above its end (%.2f km)", s, bed[s], bed[ch[#ch]]))
  end
  -- the five largest rivers, each traced up its largest tributary to its farthest source
  local kids = {}
  local down, ocean = W.geo.downhill, W.geo.ocean
  for c = 1, W.n do
    local d = down[c]
    if ocean[c] == 0 and d ~= 0 then kids[d] = kids[d] or {}; table.insert(kids[d], c) end
  end
  local mouths = {}
  for c = 1, W.n do if ocean[c] == 0 and down[c] ~= 0 and ocean[down[c]] == 1 then mouths[#mouths + 1] = c end end
  table.sort(mouths, function(a, b) if fm[a] ~= fm[b] then return fm[a] > fm[b] end return a < b end)
  local high = 0
  for i = 1, math.min(5, #mouths) do
    local x = mouths[i]
    while kids[x] do
      local best, bq = nil, -1
      for _, k in ipairs(kids[x]) do if fm[k] > bq or (fm[k] == bq and k < best) then best, bq = k, fm[k] end end
      x = best
    end
    if W.static.elev[x] - W.G.sea_level > 1.0 then high = high + 1 end
  end
  T.ok(high >= 3, "of the five largest rivers, rising above 1 km: " .. high)
end

-- Tributaries join: rivers merge into trunks instead of running side by side to the sea.
function M.tributaries_merge()
  local W = shipped()
  local N = network(W)
  local junctions = 0
  for _, n in pairs(N.ups) do if n >= 2 then junctions = junctions + 1 end end
  T.ok(junctions >= 15, "junctions " .. junctions)
  local mouths = 0
  local down, ocean = W.geo.downhill, W.geo.ocean
  for c = 1, W.n do
    if ocean[c] == 0 and N.wd[c] > 0 and down[c] ~= 0 and ocean[down[c]] == 1 then mouths = mouths + 1 end
  end
  T.ok(#N.sources >= 1.5 * mouths, string.format("%d sources to %d mouths", #N.sources, mouths))
end

-- Downstream a river carries more water and is wider.
function M.discharge_grows_downstream()
  local W = shipped()
  local N = network(W)
  local fm = W.S.hydro.flow_mean
  local down, ocean = W.geo.downhill, W.geo.ocean
  local links, grow, wider = 0, 0, 0
  for c = 1, W.n do
    local d = down[c]
    if ocean[c] == 0 and N.wd[c] > 0 and d ~= 0 and ocean[d] == 0 then
      links = links + 1
      if fm[d] >= fm[c] * 0.98 then grow = grow + 1 end
      if N.wd[d] >= N.wd[c] * 0.98 then wider = wider + 1 end
    end
  end
  T.ok(grow >= 0.95 * links, string.format("discharge grows on %d of %d links", grow, links))
  T.ok(wider >= 0.9 * links, string.format("wider downstream on %d of %d links", wider, links))
  -- the trunks are substantial: wide enough to need a bridge or a boat
  local widest = 0
  for c = 1, W.n do if N.wd[c] > widest then widest = N.wd[c] end end
  T.ok(widest > 0.25, string.format("the widest river %.3f world units", widest))
end

-- Every river reaches the sea.
function M.rivers_reach_an_outlet()
  local W = shipped()
  local N = network(W)
  local ocean = W.geo.ocean
  for _, s in ipairs(N.sources) do
    local ch, last = N.chain(s)
    T.ok(last ~= 0 and ocean[last] == 1, string.format("river from %d ends in the sea (after %d cells)", s, #ch))
  end
end

-- A settlement's map lays the same river the planet draws: along every course across a map the
-- tiles are water (or bridged), and the map's water lies on those courses.
function M.local_map_matches_planet_river()
  local W = shipped()
  local N = network(W)
  local mapm = require("sim.agents.map")
  local G = surface.get(W)
  local g = W.grid
  local down, ocean = W.geo.downhill, W.geo.ocean
  local Q = {}
  local checked, wet = 0, 0
  for _, s in ipairs(W.S.v3.settlements) do
    local lim = s.N * s.T * 0.5 - 0.1
    for c = 1, W.n do
      local d = down[c]
      if ocean[c] == 0 and N.wd[c] > 0 and d ~= 0 then
        surface.river_path(G, c, g.X[c], g.Y[c], g.Z[c], g.X[d], g.Y[d], g.Z[d], Q)
        for k = 1, surface.RIVER_SUB - 1 do
          local q = Q[k]
          if not q.sea and not Q[k + 1].sea then
            local x, y = mapm.local_of(s, q.x, q.y, q.z)
            if x and math.abs(x) < lim and math.abs(y) < lim then
              local i = mapm.tile_of(s, x, y)
              if i > 0 and s.land[i] == 1 then
                checked = checked + 1
                if s.riv[i] > 0 or (s.bridge and s.bridge[i]) then wet = wet + 1 end
              end
            end
          end
        end
      end
    end
  end
  T.ok(checked >= 20, "course points on the settlements' maps " .. checked)
  T.ok(wet >= 0.95 * checked, string.format("water on the map where the planet's river runs: %d of %d", wet, checked))
end

return M
