-- Reporting helpers for headless tools: accumulators, map sets, event dumps, JSON.
local map = require("tools.map_dump")
local events = require("sim.causality.events")
local calendar = require("sim.calendar")

local R = {}

function R.mkdir(p)
  if package.config:sub(1, 1) == "\\" then
    os.execute('mkdir "' .. p:gsub("/", "\\") .. '" 2>nul')
  else
    os.execute('mkdir -p "' .. p .. '"')
  end
end

-- Yearly accumulators for maps (means over the year).
function R.new_accumulator(W)
  local a = { n = 0, precip = {}, temp = {}, soil = {}, ice_max = {}, ice_min = {} }
  for c = 1, W.n do a.precip[c], a.temp[c], a.soil[c], a.ice_max[c], a.ice_min[c] = 0, 0, 0, 0, 1 end
  return a
end
function R.accumulate(W, a)
  local F = W.F
  a.n = a.n + 1
  local p, t, s, imx, imn = a.precip, a.temp, a.soil, a.ice_max, a.ice_min
  local fp, ft, fs, fi = F.precip, F.temp, F.soil_water, F.ice
  for c = 1, W.n do
    p[c] = p[c] + fp[c]; t[c] = t[c] + ft[c]; s[c] = s[c] + fs[c]
    if fi[c] > imx[c] then imx[c] = fi[c] end
    if fi[c] < imn[c] then imn[c] = fi[c] end
  end
end
function R.reset_accumulator(a)
  a.n = 0
  for c = 1, #a.precip do a.precip[c], a.temp[c], a.soil[c], a.ice_max[c], a.ice_min[c] = 0, 0, 0, 0, 1 end
end

local TEMP_STOPS = { { 0, 60, 40, 160 }, { 0.3, 80, 150, 230 }, { 0.5, 230, 240, 230 }, { 0.7, 250, 200, 80 }, { 1, 200, 40, 40 } }
local WET_STOPS = { { 0, 150, 110, 60 }, { 0.2, 230, 200, 120 }, { 0.45, 140, 200, 110 }, { 0.7, 40, 150, 110 }, { 1, 20, 60, 160 } }

function R.write_maps(W, a, dir, year)
  local tag = string.format("_y%04d.png", year)
  local ocean = W.geo.ocean
  local nacc = math.max(1, a.n)
  map.write(W, dir .. "/elev" .. tag, map.elevation(W))
  map.write(W, dir .. "/temp" .. tag, function(c) return map.ramp(TEMP_STOPS, (a.temp[c] / nacc + 30) / 60) end)
  map.write(W, dir .. "/precip" .. tag, function(c)
    local v = a.precip[c] / nacc
    local r, g, b = map.ramp(WET_STOPS, math.min(1, v / 0.14))
    if ocean[c] == 1 then r, g, b = r * 0.55, g * 0.55, b * 0.7 end
    return r, g, b
  end)
  map.write(W, dir .. "/soil" .. tag, function(c)
    if ocean[c] == 1 then return 30, 50, 90 end
    return map.ramp(WET_STOPS, a.soil[c] / nacc)
  end)
  map.write(W, dir .. "/ice" .. tag, function(c)
    local mx, mn = a.ice_max[c], a.ice_min[c]
    local base = ocean[c] == 1 and { 40, 70, 130 } or { 120, 110, 90 }
    local r = base[1] + (230 - base[1]) * mn
    local g = base[2] + (240 - base[2]) * math.max(mn, mx * 0.5)
    local b = base[3] + (255 - base[3]) * mx
    return r, g, b
  end)
  local pal = require("content.palette")
  local biome = W.F.biome
  local elev = W.static.elev
  map.write(W, dir .. "/biome" .. tag, function(c)
    local b = biome[c]
    local key = pal.BIOME_KEY[b] or "barren"
    if b == 1 and elev[c] - W.G.sea_level > -0.6 then key = "shallow_ocean" end
    local r, g, bb = pal.rgb(pal[key])
    if W.F.fire[c] > 0 then r, g, bb = pal.rgb(pal.fire) end
    if W.F.oil[c] > 0.05 then r, g, bb = pal.rgb(pal.oil) end
    if W.geo.ocean[c] == 0 and W.F.flow[c] > 0.6 and b ~= 3 then r, g, bb = 70, 140, 220 end
    return r, g, bb
  end)
  for _, fn in ipairs(R.extra_maps) do fn(W, a, dir, tag) end
end
R.extra_maps = {}

function R.line(W, m)
  local parts = { string.format("Y%4d", m.year), string.format("T%.1f", m.temp_mean),
    string.format("P%.3f", m.land_precip), string.format("soil %.2f", m.soil_mean),
    string.format("ice %.3f", m.ice_fraction), string.format("gT %+.2f", m.g_temp),
    string.format("co2 %.0f", m.co2), string.format("forest %.2f grass %.2f desert %.2f tundra %.2f barren %.2f",
      m.forest_cover or 0, m.grassland or 0, m.desert or 0, m.tundra or 0, m.barren or 0) }
  for _, fn in ipairs(R.extra_line) do parts[#parts + 1] = fn(W, m) end
  return table.concat(parts, " | ")
end
R.extra_line = {}

function R.describe_event(W, r)
  local d = { string.format("#%d %s %s cell=%d mag=%.3g actor=%s parent=%d root=%d%s",
    r.id, calendar.label(r.tick), r.kind, r.cell, r.mag, r.actor, r.parent, r.root,
    r.headline and " HEADLINE" or "") }
  return d[1]
end

function R.write_events(W, path)
  local f = io.open(path, "w")
  local E = W.E
  for i = 1, #E.order do
    local r = E.rec[E.order[i]]
    if r then f:write(R.describe_event(W, r), "\n") end
  end
  f:close()
end

local function json(v, ind)
  local t = type(v)
  if t == "number" then
    if v ~= v or v == math.huge or v == -math.huge then return "null" end
    return string.format("%.6g", v)
  elseif t == "string" then return string.format("%q", v)
  elseif t == "boolean" then return tostring(v)
  elseif t == "table" then
    local keys = {}
    for k in pairs(v) do keys[#keys + 1] = tostring(k) end
    table.sort(keys)
    local parts = {}
    for _, k in ipairs(keys) do
      local x = v[k]; if x == nil then x = v[tonumber(k)] end
      parts[#parts + 1] = string.format("%q: %s", k, json(x))
    end
    return "{" .. table.concat(parts, ", ") .. "}"
  end
  return "null"
end
R.json = json

function R.write_json(path, t)
  local f = io.open(path, "w"); f:write(json(t), "\n"); f:close()
end

return R
