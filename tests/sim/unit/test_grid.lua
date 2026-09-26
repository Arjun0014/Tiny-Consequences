local T = require("tests.sim.lib")
local grid = require("sim.grid")
local M = {}

local function check(level)
  local g = grid.build(level)
  T.eq(g.n, 10 * 4 ^ level + 2, "cell count")
  local five = 0
  for c = 1, g.n do
    local k = g.nbr_count[c]
    T.ok(k == 5 or k == 6, "neighbour count")
    if k == 5 then five = five + 1 end
    for i = 1, k do
      local j = g.nbr[c * 6 - 6 + i]
      T.ok(j >= 1 and j <= g.n and j ~= c)
      -- symmetry
      local found = false
      for m = 1, g.nbr_count[j] do if g.nbr[j * 6 - 6 + m] == c then found = true end end
      T.ok(found, "adjacency symmetric")
    end
    -- lat/lon consistent with position
    T.near(math.sin(g.lat[c]), g.Y[c], 1e-9)
    T.near(math.cos(g.lat[c]) * math.cos(g.lon[c]), g.X[c], 1e-9)
    T.near(g.X[c] ^ 2 + g.Y[c] ^ 2 + g.Z[c] ^ 2, 1, 1e-9)
  end
  T.eq(five, 12, "12 pentagons")
  local amin, amax = math.huge, 0
  for c = 1, g.n do amin = math.min(amin, g.area[c]); amax = math.max(amax, g.area[c]) end
  T.ok(amax / amin < 1.6, "areas roughly equal: " .. amax / amin)
  T.eq(g.nfaces, 20 * 4 ^ level)
  return g
end

function M.level2() check(2) end
function M.level3() check(3) end
function M.level4()
  local g = check(4)
  return string.format("spacing=%.4f rad", g.spacing)
end

function M.neighbour_order_is_angular()
  local g = grid.build(3)
  -- consecutive neighbours in the sorted ring are themselves adjacent
  for c = 1, g.n do
    local k = g.nbr_count[c]
    for i = 1, k do
      local a = g.nbr[c * 6 - 6 + i]
      local b = g.nbr[c * 6 - 6 + (i % k) + 1]
      local adj = false
      for m = 1, g.nbr_count[a] do if g.nbr[a * 6 - 6 + m] == b then adj = true end end
      T.ok(adj, "ring order")
    end
  end
end

function M.nearest()
  local g = grid.build(3)
  for c = 1, g.n, 7 do
    T.eq(grid.nearest(g, g.X[c], g.Y[c], g.Z[c], 1), c)
  end
end

return M
