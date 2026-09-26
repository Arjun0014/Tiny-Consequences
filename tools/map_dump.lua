-- Equirectangular PNG maps of sim fields (tools only).
local png = require("tools.png")
local grid = require("sim.grid")

local M = {}

local function lerp(a, b, t) return a + (b - a) * t end
local function hex(h)
  return tonumber(h:sub(2, 3), 16), tonumber(h:sub(4, 5), 16), tonumber(h:sub(6, 7), 16)
end
M.hex = hex

local function ramp(stops, t)
  if t <= stops[1][1] then return stops[1][2], stops[1][3], stops[1][4] end
  for i = 2, #stops do
    local s0, s1 = stops[i - 1], stops[i]
    if t <= s1[1] then
      local f = (t - s0[1]) / (s1[1] - s0[1])
      return lerp(s0[2], s1[2], f), lerp(s0[3], s1[3], f), lerp(s0[4], s1[4], f)
    end
  end
  local s = stops[#stops]
  return s[2], s[3], s[4]
end
M.ramp = ramp

-- colour(c) -> r,g,b for cell c. Writes path. w x h (default 512x256).
function M.write(W, path, colour, w, h)
  w, h = w or 512, h or 256
  local g = W.grid
  local cellmap = {}
  local last = 1
  for y = 0, h - 1 do
    local lat = (0.5 - (y + 0.5) / h) * math.pi
    for x = 0, w - 1 do
      local lon = ((x + 0.5) / w) * 2 * math.pi - math.pi
      local cx = math.cos(lat) * math.cos(lon)
      local cz = math.cos(lat) * math.sin(lon)
      local cy = math.sin(lat)
      last = grid.nearest(g, cx, cy, cz, last)
      cellmap[y * w + x + 1] = last
    end
  end
  local cache = {}
  png.write(path, w, h, function(x, y)
    local c = cellmap[y * w + x + 1]
    local col = cache[c]
    if not col then
      local r, gg, b = colour(c)
      col = { math.max(0, math.min(255, math.floor(r + 0.5))), math.max(0, math.min(255, math.floor(gg + 0.5))),
        math.max(0, math.min(255, math.floor(b + 0.5))) }
      cache[c] = col
    end
    return col[1], col[2], col[3]
  end)
end

local ELEV_STOPS = {
  { -5, 20, 40, 110 }, { -1.5, 31, 95, 168 }, { -0.2, 63, 183, 201 }, { 0, 232, 212, 150 },
  { 0.3, 140, 203, 94 }, { 1.0, 90, 150, 70 }, { 2.0, 150, 130, 100 }, { 3.2, 245, 245, 250 },
}
function M.elevation(W)
  local elev = W.static.elev
  return function(c) return ramp(ELEV_STOPS, elev[c] - W.G.sea_level) end
end

function M.scalar(arr, lo, hi, stops)
  stops = stops or { { 0, 30, 30, 90 }, { 0.5, 60, 170, 160 }, { 1, 250, 240, 120 } }
  return function(c)
    local t = (arr[c] - lo) / (hi - lo)
    return ramp(stops, t)
  end
end

return M
