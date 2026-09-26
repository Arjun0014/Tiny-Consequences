-- Field catalogue (02_WORLD_MODEL.md §3). Struct-of-arrays, one flat array per field.
-- `causal` fields get a cause-tag array (event id, 0 = baseline) and an idle counter array.
-- Ranges are the declared valid domain checked by property test P06.

local fields = {}

-- name, min, max, causal, blame eps (|delta| below eps doesn't touch the tag), hold (ticks a
-- tag survives without reinforcement), spatial (needs a prev-tick copy for neighbour reads)
fields.LIST = {
  { name = "temp",       min = -90, max = 90,  causal = true, eps = 0.05,  hold = 48 },
  { name = "heat_anom",  min = -60, max = 60,  causal = true, eps = 0.02,  hold = 48 },
  { name = "sst",        min = -3,  max = 45,  causal = true, eps = 0.01,  hold = 96 },
  { name = "moisture",   min = 0,   max = 20,  causal = true, eps = 0.002, hold = 12, spatial = true },
  { name = "precip",     min = 0,   max = 20,  causal = true, eps = 0.002, hold = 12 },
  { name = "soil_water", min = 0,   max = 1,   causal = true, eps = 0.001, hold = 48 },
  { name = "flow",       min = 0,   max = 1e6, causal = true, eps = 0.002, hold = 24 },
  { name = "ice",        min = 0,   max = 1,   causal = true, eps = 0.001, hold = 96 },
  { name = "grass",      min = 0,   max = 1,   causal = true, eps = 0.0005, hold = 96, spatial = true },
  { name = "forest",     min = 0,   max = 1,   causal = true, eps = 0.0002, hold = 480, spatial = true },
  { name = "crop",       min = 0,   max = 1,   causal = true, eps = 0.001, hold = 48 },
  { name = "fertility",  min = 0,   max = 1,   causal = true, eps = 0.0002, hold = 240 },
  { name = "fire",       min = 0,   max = 1,   causal = true, eps = 0.001, hold = 8 },
  { name = "plankton",   min = 0,   max = 1,   causal = true, eps = 0.0005, hold = 96 },
  { name = "fish",       min = 0,   max = 1,   causal = true, eps = 0.0002, hold = 144, spatial = true },
  { name = "oil",        min = 0,   max = 1,   causal = true, eps = 0.0005, hold = 96, spatial = true },
  { name = "grazers",    min = 0,   max = 1,   causal = true, eps = 0.0002, hold = 48 },
  { name = "predators",  min = 0,   max = 1,   causal = true, eps = 0.0001, hold = 48 },
  { name = "stress",     min = 0,   max = 1,   causal = true, eps = 0.001, hold = 48 },
  { name = "silt",       min = 0,   max = 1e6, causal = true, eps = 0.0005, hold = 48 },
  -- non-causal
  { name = "land_use",   int = true, min = -1e6, max = 1e6 },
  { name = "biome",      int = true, min = 1, max = 32 },
  { name = "biome_cand", int = true, min = 1, max = 32 },
  { name = "biome_dwell", int = true, min = 0, max = 1e9 },
  { name = "burn_scar",  int = true, min = 0, max = 1e9 },
}

fields.BY_NAME = {}
fields.CAUSAL = {}
for i, f in ipairs(fields.LIST) do
  f.index = i
  fields.BY_NAME[f.name] = f
  if f.causal then fields.CAUSAL[#fields.CAUSAL + 1] = f end
end

local function filled(n, v)
  local t = {}
  for i = 1, n do t[i] = v end
  return t
end
fields.filled = filled

-- Allocate all dynamic fields for n cells into W.F (values), W.C (cause tags), W.I (idle),
-- W.P (prev copies for spatial fields).
function fields.allocate(W, n)
  local F, C, I, P = {}, {}, {}, {}
  for _, f in ipairs(fields.LIST) do
    F[f.name] = filled(n, f.int and (f.name == "biome" or f.name == "biome_cand") and 1 or 0)
    if f.causal then
      C[f.name] = filled(n, 0)
      I[f.name] = filled(n, 0)
    end
    if f.spatial then P[f.name] = filled(n, 0) end
  end
  W.F, W.C, W.I, W.P = F, C, I, P
end

-- Copy current values into prev buffers (element-wise; called once per tick per spatial field).
function fields.snapshot(W, name)
  local src, dst = W.F[name], W.P[name]
  for i = 1, W.n do dst[i] = src[i] end
end

-- Year-boundary snap: quantise to 16-bit save precision within the field's range so that
-- a saved/reloaded world is bit-identical to the live one (05_ARCHITECTURE.md §8).
local floor = math.floor
function fields.quantize_value(v, lo, hi)
  if v <= lo then return lo end
  if v >= hi then return hi end
  local s = (hi - lo) / 65535
  return lo + floor((v - lo) / s + 0.5) * s
end

-- sqrt mapping for [0,hi] fields where tiny values matter (populations, oil): fine near 0.
function fields.quantize_sqrt(v, hi)
  if v <= 0 then return 0 end
  if v >= hi then return hi end
  local q = floor(math.sqrt(v / hi) * 65535 + 0.5) / 65535
  return q * q * hi
end

-- Save ranges: { lo, hi, sqrt_mode }
fields.QRANGE = {
  temp = { -90, 90 }, heat_anom = { -60, 60 }, sst = { -3, 45 }, moisture = { 0, 20 },
  precip = { 0, 20 }, soil_water = { 0, 1 }, flow = { 0, 1000, true }, ice = { 0, 1 },
  grass = { 0, 1, true }, forest = { 0, 1, true }, crop = { 0, 1 }, fertility = { 0, 1 },
  fire = { 0, 1 }, plankton = { 0, 1, true }, fish = { 0, 1, true }, oil = { 0, 1, true },
  grazers = { 0, 1, true }, predators = { 0, 1, true }, stress = { 0, 1 }, silt = { 0, 100, true },
}

local function snap_array(a, n, r)
  if r[3] then
    local qs, hi = fields.quantize_sqrt, r[2]
    for i = 1, n do a[i] = qs(a[i], hi) end
  else
    local qv, lo, hi = fields.quantize_value, r[1], r[2]
    for i = 1, n do a[i] = qv(a[i], lo, hi) end
  end
end
fields.snap_array = snap_array

-- Persistent per-cell system arrays (W.S.<system>.<name>) snapped with the fields at the year
-- boundary and saved in the same compact 16-bit form (D-026).
fields.SYSTEM_Q = {
  { "fauna", "hnorm", { 0, 1, true } }, { "fauna", "pnorm", { 0, 1, true } },
  { "hydro", "flow_mean", { 0, 1000, true } }, { "ocean", "fish_ref", { 0, 1, true } },
}

function fields.snap_all(W)
  local n = W.n
  for _, f in ipairs(fields.LIST) do
    local r = fields.QRANGE[f.name]
    if r then
      snap_array(W.F[f.name], n, r)
      local p = W.P[f.name]
      if p then snap_array(p, n, r) end
    end
  end
  local S = W.S
  for _, e in ipairs(fields.SYSTEM_Q) do
    local t = S and S[e[1]]
    local a = t and t[e[2]]
    if a then snap_array(a, #a, e[3]) end
  end
end

return fields
