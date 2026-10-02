-- Props: placement and baking into chunk meshes (Stage 1.8, D-009; renderer v2, D-048).
-- Pure Lua 5.1.
--
-- Models come from game/view/modelkit.lua (TPL[lod][kind]). Chunk placement turns sim values
-- into a flat instance list (reused arrays, no per-prop tables), and write() bakes every
-- instance into planet-space vertex streams: position(3) normal(3) color(4).
--   color.x = packed rgb (7 bits per channel), color.y = shade*8 + sway (unpack.glsl),
--   color.z = sim cell id (the shaders read the cell's snow / fire / season data from the
--   cell texture), color.w = material class (CLASS below: foliage, evergreen, roof ...).
-- Placement is stable: each land face owns a fixed set of slots with fixed random positions,
-- so density changes add/remove trees in place instead of reshuffling the forest. Villages
-- are laid out as settlements (houses in rings facing a well or hall, fences, granaries,
-- woodpiles, piers for fishing villages) from their size and culture.
local kit = require("game.view.modelkit")
local layout = require("sim.society.layout")

local M = {}

local sqrt, floor, min, max, abs, cos, sin, pi, atan2 = math.sqrt, math.floor, math.min, math.max,
  math.abs, math.cos, math.sin, math.pi, math.atan2

M.SCALE = 1.25         -- global prop scale, near LOD
M.SCALE_FAR = 1.7      -- far LOD props are bigger and fewer so forests still read from orbit
M.BUILDING = 1.15      -- buildings are drawn a little large (miniature-model proportions)
M.SLOTS = 24           -- prop slots per render face, near: 10 canopy, 3 understory, 4 shrubs,
                       -- 5 floor, 1 deadwood, 1 rock (renderer v4, D-056)
M.SLOTS_FAR = 7        -- 5 canopy, 1 shrub, 1 rock

-- Kinds (index into NAMES; templates by name)
local NAMES = { "oak", "birch", "spruce", "snowpine", "pine", "palm", "jungle", "acacia", "snag", "stump",
  "bush", "tuft", "flower", "reed", "rock", "cactus", "cottage", "longhouse", "townhouse", "hut",
  "granary", "hall", "well", "fence", "haystack", "woodpile", "ruin", "pier",
  "stakes", "frame", "walls", "sodhouse", "woodshed", "sacks", "fishrack", "hull", "stook", "logs", "palisade",
  "kiln", "pots", "pen", "banner" }
local flora = require("game.view.flora")
for _, n in ipairs(flora.NAMES) do NAMES[#NAMES + 1] = n end
local K = {}
for i, n in ipairs(NAMES) do K[n] = i end
M.K, M.NAMES = K, NAMES

-- material classes (color.w) per kind and slot: 1 deciduous foliage, 2 evergreen foliage,
-- 3 tropical foliage, 4 snow, 5 roof, 6 bark/wood, 7 ground plants (grass, flowers, reeds)
local CLASS = {}
local function cls(kind, s1, s2, s3) CLASS[K[kind]] = { s1 or 0, s2 or 0, s3 or 0 } end
cls("oak", 1, 6); cls("birch", 1, 6); cls("spruce", 2, 6, 4); cls("snowpine", 2, 6, 4); cls("pine", 2, 6)
cls("palm", 3, 6); cls("jungle", 3, 6); cls("acacia", 3, 6); cls("snag", 6); cls("stump", 6)
cls("bush", 1); cls("tuft", 7); cls("flower", 7, 7); cls("reed", 7); cls("rock", 0); cls("cactus", 3)
for _, n in ipairs({ "cottage", "longhouse", "townhouse", "hut", "granary", "hall", "well" }) do cls(n, 5, 0) end
cls("fence"); cls("haystack", 7); cls("woodpile"); cls("ruin"); cls("pier")
cls("stakes"); cls("frame"); cls("walls", 0, 0); cls("sodhouse", 7, 0); cls("woodshed", 5); cls("sacks")
cls("fishrack"); cls("hull"); cls("stook", 7); cls("logs"); cls("palisade"); cls("kiln"); cls("pots"); cls("pen")
cls("banner")
for _, n in ipairs({ "oak1", "oak2", "oak3", "beech1", "beech2", "birch1", "birch2", "poplar", "sapling_b" }) do cls(n, 1, 6) end
for _, n in ipairs({ "spruce1", "spruce2", "spruce3", "fir1", "pine1", "pine2", "sapling_c" }) do cls(n, 2, 6) end
cls("snowpine1", 2, 6, 4); cls("snowpine2", 2, 6, 4)
for _, n in ipairs({ "jungle1", "jungle2", "jungle3", "palm1", "palm2", "acacia1", "acacia2" }) do cls(n, 3, 6) end
cls("snag1", 0, 6); cls("burnt1"); cls("deadfall", 0, 6, 0)
cls("bush1", 1); cls("bush2", 1); cls("berry", 1, 6); cls("juniper", 2); cls("heather", 7); cls("dryshrub", 3)
cls("fern", 7); cls("fern2", 7); cls("grass1", 7); cls("grass2", 7); cls("meadow", 7, 7); cls("tallgrass", 7)
cls("shroom", 0, 0, 0); cls("boulder"); cls("mossrock", 0, 0, 0); cls("slab")

local TPL = { {}, {} }
M.TPL = TPL

local function pack_rgb(r, g, b)
  r = floor(min(1, max(0, r)) * 127 + 0.5)
  g = floor(min(1, max(0, g)) * 127 + 0.5)
  b = floor(min(1, max(0, b)) * 127 + 0.5)
  return (r * 128 + g) * 128 + b
end
M.pack_rgb = pack_rgb

local function prep(t)
  -- packed shade/sway per template vertex: shade 0.5..1 -> 0..31, sway 0..1 -> 0..7
  local ss = {}
  for k = 0, t.nv - 1 do
    local o = k * 9
    local sh = floor((min(1, max(0.5, t.d[o + 8])) - 0.5) * 62 + 0.5)
    local sw = floor(min(1, max(0, t.d[o + 9])) * 7 + 0.5)
    ss[k + 1] = sh * 8 + sw
  end
  t.ss = ss
  return t
end

local function build_templates()
  kit.build()
  if not kit.T[1].oak1 then flora.build() end
  for lod = 1, 2 do
    for i, n in ipairs(NAMES) do
      local t = kit.T[lod][n]
      if t then
        if not t.ss then prep(t) end
        TPL[lod][i] = t
      end
    end
  end
  -- fixed colours for slots >= 4, packed
  M.FIXED = {}
  for s, c in pairs(kit.FIXED) do M.FIXED[s] = pack_rgb(c[1], c[2], c[3]) end
end
build_templates()

---------------------------------------------------------------------------------------------
-- Colours

local function hex(h)
  return { tonumber(h:sub(2, 3), 16) / 255, tonumber(h:sub(4, 5), 16) / 255, tonumber(h:sub(6, 7), 16) / 255 }
end
local function hexes(list)
  local out = {}
  for i, h in ipairs(list) do out[i] = hex(h) end
  return out
end

local PAL = {
  [K.oak] = hexes({ "#5AAE47", "#65B84A", "#72BC4F", "#4FA548", "#7DBF52", "#5DA84E", "#8CC25A", "#4C9E46" }),
  [K.birch] = hexes({ "#8CC455", "#9ACB5B", "#7FBE52", "#A6CF62" }),
  [K.spruce] = hexes({ "#2F7048", "#2B6742", "#367B4E", "#3A7F50" }),
  [K.snowpine] = hexes({ "#336E4A", "#2E6645" }),
  [K.pine] = hexes({ "#3F8050", "#468757", "#3A784B" }),
  [K.palm] = hexes({ "#56B34A", "#62BE50", "#4DAA45" }),
  [K.jungle] = hexes({ "#2C8242", "#2A783E", "#349048", "#23733A" }),
  [K.acacia] = hexes({ "#86A94A", "#7CA044", "#93B052" }),
  [K.snag] = hexes({ "#2F2B29", "#3A3532" }),
  [K.stump] = hexes({ "#6B4B34", "#5E4230" }),
  [K.bush] = hexes({ "#5FB24A", "#6FBC50", "#539F45", "#7EC157" }),
  [K.tuft] = hexes({ "#8FCB57", "#9DD162", "#84BE52" }),
  [K.flower] = hexes({ "#F2F2F0", "#F4D03F", "#E8746B", "#B98BE0", "#F29BC4", "#F5A94A" }),
  [K.reed] = hexes({ "#7FA650", "#8DB05A" }),
  [K.rock] = hexes({ "#9A938A", "#A9A095", "#8B857D", "#B2A99C", "#958D82" }),
  [K.cactus] = hexes({ "#5E9E57", "#6BA85F" }),
  [K.haystack] = hexes({ "#E0C064", "#D6B458" }),
}
-- flora palettes (renderer v4): species hues, several per species; stands shift them further
local OAKS = hexes({ "#4E9A3E", "#5AA844", "#63B04A", "#4A9240", "#6EB24C", "#548F3C", "#7DB852", "#5E9F47", "#468A3A" })
local BEECH = hexes({ "#6DB348", "#78BC4E", "#62A843", "#86C258", "#5C9E40" })
local BIRCH = hexes({ "#93C653", "#A2CE5E", "#86BD4E", "#B0D468", "#9CC45A" })
local POPLAR = hexes({ "#5E9E43", "#6BAA4A", "#7AB350" })
local SPRUCE = hexes({ "#2C6A45", "#285F40", "#33744C", "#3B7A4F", "#2A5E48", "#335F3F" })
local FIR = hexes({ "#2A5A48", "#2F644D", "#264F42" })
local PINE = hexes({ "#3D7C4C", "#468454", "#3A7447", "#4C8B55" })
local JUNGLE = hexes({ "#2A8040", "#28753C", "#328C46", "#237038", "#3A9148", "#2E7A36" })
local PALM = hexes({ "#56B34A", "#62BE50", "#4DAA45", "#6CBF4E" })
local ACACIA = hexes({ "#86A94A", "#7CA044", "#93B052", "#8FA24A" })
local SHRUB = hexes({ "#5FA84A", "#6FB450", "#539A45", "#7EBB57", "#4E8F40" })
for _, n in ipairs({ "oak1", "oak2", "oak3", "jungle3" }) do PAL[K[n]] = OAKS end
PAL[K.beech1], PAL[K.beech2] = BEECH, BEECH
PAL[K.birch1], PAL[K.birch2], PAL[K.sapling_b] = BIRCH, BIRCH, BIRCH
PAL[K.poplar] = POPLAR
for _, n in ipairs({ "spruce1", "spruce2", "spruce3", "sapling_c", "snowpine1", "snowpine2" }) do PAL[K[n]] = SPRUCE end
PAL[K.fir1] = FIR
PAL[K.pine1], PAL[K.pine2] = PINE, PINE
PAL[K.jungle1], PAL[K.jungle2] = JUNGLE, JUNGLE
PAL[K.palm1], PAL[K.palm2] = PALM, PALM
PAL[K.acacia1], PAL[K.acacia2] = ACACIA, ACACIA
PAL[K.bush1], PAL[K.bush2], PAL[K.berry] = SHRUB, SHRUB, SHRUB
PAL[K.juniper] = hexes({ "#3F6E4E", "#46785A", "#3A6648" })
PAL[K.heather] = hexes({ "#9A6A9E", "#8C5F92", "#A77AA8", "#7F6A86" })
PAL[K.dryshrub] = hexes({ "#8E9A55", "#9AA05A", "#7F8C4E", "#A39A5C" })
PAL[K.fern], PAL[K.fern2] = hexes({ "#4F9E3E", "#5BAA46", "#45913A", "#68B04C" }), hexes({ "#5AA743", "#4C9A3B" })
PAL[K.grass1], PAL[K.grass2] = hexes({ "#86C150", "#93C95A", "#7AB84A", "#9CCB63" }), hexes({ "#7FBB4B", "#8DC456" })
PAL[K.meadow] = hexes({ "#8CC455", "#9ACB5B", "#82BC50" })
PAL[K.tallgrass] = hexes({ "#B7B25E", "#C2B866", "#A9A857", "#9FAE55" })
PAL[K.snag1] = hexes({ "#6E6258", "#7A6C60", "#5F564E" })
PAL[K.burnt1] = hexes({ "#2F2B29", "#3A3532" })
PAL[K.deadfall] = hexes({ "#6B5B4A", "#75624E" })
PAL[K.shroom] = hexes({ "#EDE3D0" })
PAL[K.boulder] = hexes({ "#9A938A", "#A9A095", "#8B857D", "#B2A99C", "#958D82" })
PAL[K.mossrock] = PAL[K.boulder]
PAL[K.slab] = hexes({ "#A59E92", "#968F85", "#B3AB9E" })
M.PAL = PAL
local BARK = {
  [K.oak] = hex("#7A5236"), [K.birch] = hex("#E9E4D8"), [K.spruce] = hex("#6E4A32"),
  [K.snowpine] = hex("#6E4A32"), [K.pine] = hex("#9A6244"), [K.palm] = hex("#A57C52"),
  [K.jungle] = hex("#6E4C34"), [K.acacia] = hex("#7C5A3E"), [K.flower] = hex("#5E9E4A"),
}
for _, n in ipairs({ "oak1", "oak2", "oak3", "jungle3", "beech1", "beech2" }) do BARK[K[n]] = n:sub(1, 5) == "beech" and hex("#8E8A80") or hex("#6E4C34") end
BARK[K.birch1], BARK[K.birch2], BARK[K.sapling_b] = hex("#ECE7DC"), hex("#E6E1D5"), hex("#9C8F7C")
BARK[K.poplar] = hex("#8A7A62")
for _, n in ipairs({ "spruce1", "spruce2", "spruce3", "fir1", "snowpine1", "snowpine2", "sapling_c" }) do BARK[K[n]] = hex("#5E4030") end
BARK[K.pine1], BARK[K.pine2] = hex("#A8603A"), hex("#9C5A38")
BARK[K.jungle1], BARK[K.jungle2] = hex("#7A6650"), hex("#6E5A44")
BARK[K.palm1], BARK[K.palm2] = hex("#A57C52"), hex("#9A7450")
BARK[K.acacia1], BARK[K.acacia2] = hex("#6E5040"), hex("#7C5A3E")
BARK[K.snag1], BARK[K.deadfall] = hex("#7C6E60"), hex("#6B5B4A")
BARK[K.berry], BARK[K.meadow], BARK[K.shroom] = hex("#5E4A3A"), hex("#5E9E4A"), hex("#EDE3D0")
local SNOW = hex("#F4F8FF")
-- building palettes by culture: roofs, walls
local ROOFS = {
  hexes({ "#C9553F", "#B84A3A", "#D4683F", "#A84638" }),   -- terracotta
  hexes({ "#5E6B7A", "#6B7888", "#566273", "#7A8494" }),   -- slate
  hexes({ "#D6B36A", "#C9A55E", "#E0BE73" }),              -- thatch
  hexes({ "#3F7A8C", "#4B8696", "#36707F", "#8A4E3A" }),   -- painted blue-green
}
local WALLS = {
  hexes({ "#F2E8D5", "#EFE2C8", "#F6EEDD", "#E8DCC4" }),   -- plaster
  hexes({ "#B08A62", "#A27E58", "#BC966C" }),              -- timber
  hexes({ "#DCCBA8", "#E3D3B2", "#D2BF9A" }),              -- clay
  hexes({ "#F4F1EA", "#E6EDF0", "#F0E4D0" }),              -- whitewash
}
-- building materials (houses are built of what the village's land gives, W10: its clay, stone
-- and woods, the same sources its crafts use; the most respected households build best)
local ROOF_MAT = {
  thatch = hexes({ "#C9A55E", "#D6B36A", "#BE9A55", "#B89250" }),
  shingle = hexes({ "#6E4A34", "#7A5238", "#5E3F2C", "#86603F" }),
  slate = hexes({ "#5E6B7A", "#566273", "#6B7888", "#4F5A66" }),
  tile = hexes({ "#C9553F", "#B84A3A", "#D4683F", "#A84638" }),
}
local WALL_MAT = {
  daub = hexes({ "#DCCBA8", "#D2BF9A", "#E3D3B2", "#CDB894" }),
  timber = hexes({ "#9A7450", "#A27E58", "#8C6A48", "#B08A62" }),
  stone = hexes({ "#B8B2A6", "#A9A398", "#C4BEB2", "#9F998C" }),
  plaster = hexes({ "#F2E8D5", "#F6EEDD", "#EFE2C8", "#F4F1EA" }),
}
M.ROOF_MAT, M.WALL_MAT = ROOF_MAT, WALL_MAT

-- What a village builds with: 0..1 supplies of clay, stone and timber from its land.
function M.village_supply(W, v)
  local cr = v.crafts or {}
  local clay = cr.pots and cr.pots.q or 0.2
  local stone = cr.tools and cr.tools.q or 0.2
  local g = W.grid
  local F = W.F
  local c = v.cell
  local wood, n = F.forest[c], 1
  local base = c * 6 - 6
  for k = 1, g.nbr_count[c] do
    local j = g.nbr[base + k]
    if W.geo.ocean[j] == 0 then wood = wood + F.forest[j]; n = n + 1 end
  end
  return clay, stone, wood / n
end

-- Roof and wall colours of a house: materials scored by the village's supplies and the
-- household's standing s (0..1), a fixed per-house choice among near ties (u, 0..1).
function M.house_colours(style, clay, stone, wood, s, u, key)
  local best, bs = "thatch", 0.5 + 0.2 * u
  local function try(m, sc) if sc > bs then best, bs = m, sc end end
  try("tile", clay * (0.45 + s) + 0.25 * ((u * 7.3) % 1))
  try("slate", stone * (0.3 + s) + 0.25 * ((u * 3.7) % 1))
  try("shingle", wood * 0.85 + 0.2 * ((u * 5.1) % 1))
  local roofs = ROOF_MAT[best]
  -- the most respected paint or tile in the culture's own colours
  if s >= 0.85 then roofs = ROOFS[style.roof] end
  local wm, ws = "daub", 0.45 + clay * 0.3 + 0.2 * ((u * 2.3) % 1)
  local function tw(m, sc) if sc > ws then wm, ws = m, sc end end
  tw("timber", wood * 0.9 + 0.2 * ((u * 4.9) % 1))
  tw("stone", stone * (0.2 + s * 0.9) + 0.15 * ((u * 6.1) % 1))
  tw("plaster", s * 0.95)
  local walls = WALL_MAT[wm]
  if s >= 0.85 then walls = WALLS[style.wall] end
  return roofs[key % #roofs + 1], walls[floor(key / 7) % #walls + 1]
end

-- culture -> building mix { kinds..., roof palette, wall palette }
local STYLES = {
  { houses = { "cottage", "cottage", "townhouse", "longhouse" }, roof = 1, wall = 1 },
  { houses = { "hut", "hut", "cottage", "hut" }, roof = 3, wall = 3 },
  { houses = { "longhouse", "cottage", "longhouse", "townhouse" }, roof = 2, wall = 2 },
  { houses = { "cottage", "townhouse", "cottage", "hut" }, roof = 4, wall = 4 },
}
M.STYLES = STYLES

---------------------------------------------------------------------------------------------
-- Instance list (flat, reused)

local L = {
  n = 0, verts = 0, lod = 2,
  kind = {}, x = {}, y = {}, z = {}, ux = {}, uy = {}, uz = {}, yaw = {}, sc = {}, sh = {},
  c1 = {}, c2 = {}, c3 = {}, cell = {}, rnd = {},
}
M.list = L

-- rnd: a per-instance random 0..1 (its own seasonal calendar, sway phase); default from yaw
local function add(kind, x, y, z, ux, uy, uz, yaw, sc, sh, c1, c2, c3, cell, rnd)
  local t = TPL[L.lod][kind]
  if not t then return end
  local ex = M.v3_exclude
  if ex and not L.v3 then
    for q = 1, #ex do
      local e = ex[q]
      local d = ux * e[1] + uy * e[2] + uz * e[3]
      if d > e[12] then
        local k = e[11] / d
        local lx = (ux * e[4] + uy * e[5] + uz * e[6]) * k
        local ly = (ux * e[7] + uy * e[8] + uz * e[9]) * k
        if lx > -e[10] and lx < e[10] and ly > -e[10] and ly < e[10] then return end
      end
    end
  end
  local i = L.n + 1
  L.n = i
  L.kind[i], L.x[i], L.y[i], L.z[i] = kind, x, y, z
  L.ux[i], L.uy[i], L.uz[i], L.yaw[i], L.sc[i], L.sh[i] = ux, uy, uz, yaw, sc, sh
  L.c1[i], L.c2[i], L.c3[i] = c1, c2, c3
  L.cell[i] = cell or 0
  L.rnd[i] = rnd or ((yaw * 0.6180339) % 1)
  L.verts = L.verts + t.nv
end
M.add = add

-- Park-Miller step (exact in doubles)
local function pm(h) return (h * 48271) % 2147483647 end

local P_SNOW = pack_rgb(SNOW[1], SNOW[2], SNOW[3])
local P_GREEN = pack_rgb(0.37, 0.62, 0.29)

local vnoise = require("game.view.vnoise")
local NOISE = {}
function M.noise_for(seed)
  local n = NOISE[seed]
  if not n then n = vnoise.new(seed * 131 + 71); NOISE[seed] = n end
  return n
end

-- Accent colours (slot 3): berries, meadow flowers, mushroom caps, moss.
local ACCENT = {
  [K.berry] = hexes({ "#C8323C", "#8E2F6E", "#3E4FA0", "#D8452E" }),
  [K.meadow] = hexes({ "#F2F2F0", "#F4D03F", "#E8746B", "#B98BE0", "#F29BC4", "#F5A94A", "#8FB8F0" }),
  [K.shroom] = hexes({ "#C8412F", "#B98A57", "#D9B98A", "#8E5A3A" }),
  [K.mossrock] = hexes({ "#6A9A45", "#5E8E3E", "#79A652" }),
  [K.deadfall] = hexes({ "#6A9A45", "#5E8E3E" }),
}
for k, list in pairs(ACCENT) do
  for i, c in ipairs(list) do list[i] = pack_rgb(c[1], c[2], c[3]) end
end

M.BARK = BARK
M.ACCENT = ACCENT

-- The tree species of a place: climate, soil and the coast choose the pool; `stand` (low-frequency
-- noise) makes patches of one species within a forest (birch groves in oak woods, pine stands).
local function pick_tree(tmean, ic, bi, soil, coast, u, stand)
  return M.pick_tree_impl(tmean, ic, bi, soil, coast, u, stand)
end
function M.pick_tree_impl(tmean, ic, bi, soil, coast, u, stand)
  if tmean < -3 or ic > 0.3 then return u < 0.6 and K.snowpine1 or K.snowpine2 end
  if tmean < 3 then
    if stand < 0.28 then return u < 0.6 and K.birch2 or K.birch1 end
    if stand > 0.72 then return u < 0.5 and K.pine1 or K.pine2 end
    return u < 0.4 and K.spruce1 or (u < 0.75 and K.spruce2 or K.fir1)
  end
  if tmean < 9 then
    if stand < 0.3 then return u < 0.5 and K.birch1 or K.birch2 end
    if stand > 0.7 then return u < 0.55 and K.spruce1 or K.spruce2 end
    if u < 0.35 then return K.beech1 elseif u < 0.55 then return K.beech2 elseif u < 0.8 then return K.oak1 end
    return K.oak3
  end
  if tmean < 19 then
    if stand > 0.78 then return u < 0.5 and K.pine1 or K.pine2 end
    if stand < 0.2 then return u < 0.55 and K.birch1 or K.poplar end
    if u < 0.3 then return K.oak1 elseif u < 0.5 then return K.oak2 elseif u < 0.65 then return K.oak3
    elseif u < 0.85 then return K.beech2 end
    return K.beech1
  end
  if bi == 11 then
    if coast and u < 0.3 then return u < 0.15 and K.palm1 or K.palm2 end
    return u < 0.35 and K.jungle1 or (u < 0.7 and K.jungle2 or K.jungle3)
  end
  if soil < 0.32 then
    if u < 0.7 then return u < 0.35 and K.acacia1 or K.acacia2 end
    return K.oak2
  end
  if coast and u < 0.4 then return u < 0.2 and K.palm1 or K.palm2 end
  return u < 0.3 and K.oak2 or (u < 0.55 and K.jungle3 or (u < 0.75 and K.jungle2 or K.oak1))
end
M.pick_tree = pick_tree

local function tree_kind(tmean, ic, bi, soil, coast, u)
  if tmean < -3 or ic > 0.3 then return K.snowpine end
  if tmean < 3 then return u < 0.75 and K.spruce or K.pine end
  if tmean < 9 then
    if u < 0.35 then return K.birch elseif u < 0.55 then return K.spruce end
    return K.oak
  end
  if tmean < 19 then
    if u < 0.12 then return K.birch elseif u < 0.24 then return K.pine end
    return K.oak
  end
  if bi == 11 then return (coast and u < 0.3) and K.palm or K.jungle end
  if soil < 0.32 then return u < 0.75 and K.acacia or K.oak end
  if coast and u < 0.4 then return K.palm end
  return u < 0.55 and K.oak or K.jungle
end

---------------------------------------------------------------------------------------------
-- Settlements

-- Tangent frame at unit direction (x, y, z): east (e) and north (n) unit vectors.
local function frame(x, y, z)
  local ex, ez = z, -x
  local el = sqrt(ex * ex + ez * ez)
  if el < 1e-5 then ex, ez, el = 1, 0, 1 end
  ex, ez = ex / el, ez / el
  local nx, ny, nz = -ez * y, ez * x - ex * z, ex * y
  return ex, 0, ez, nx, ny, nz
end

M.frame = frame

-- chimney tops in the house templates' local frame (modelkit)
local CHIMNEY = { cottage = { 0.022, 0.092, -0.008 }, townhouse = { -0.02, 0.122, 0.006 } }
M.chimneys = {}         -- village cell -> list of { x, y, z } chimney tops (world), for fx smoke
M.pens = {}             -- village id -> { x, y, z } unit direction of its pen (puppets: herds at night)

-- World position of local point (lx, ly, lz) of an instance (same frame as M.write).
local function to_world(x, y, z, ux, uy, uz, yaw, s, lx, ly, lz)
  return M.to_world(x, y, z, ux, uy, uz, yaw, s, lx, ly, lz)
end
M.CHIMNEY = CHIMNEY
function M.to_world(x, y, z, ux, uy, uz, yaw, s, lx, ly, lz)
  local tx, tz = uz, -ux
  local tl = sqrt(tx * tx + tz * tz)
  if tl < 1e-4 then tx, tz, tl = 1, 0, 1 end
  tx, tz = tx / tl, tz / tl
  local bx, by, bz = -tz * uy, tz * ux - tx * uz, tx * uy
  local cyw, syw = cos(yaw), sin(yaw)
  local ex, ey, ez = tx * cyw + bx * syw, by * syw, tz * cyw + bz * syw
  local fx, fy, fz = bx * cyw - tx * syw, by * cyw, bz * cyw - tz * syw
  lx, ly, lz = lx * s, ly * s, lz * s
  return x + ex * lx + ux * ly + fx * lz, y + ey * lx + uy * ly + fy * lz, z + ez * lx + uz * ly + fz * lz
end

-- The settlement is drawn from the sim's buildings (W2c, docs/world/16 §4): every house, hut,
-- granary, woodshed and pier stands on the lot the sim gave it (sim/society/layout.lua, the same
-- pure function the sim uses); sites show their construction stage (staked lot, timber frame,
-- walls under open rafters); earth houses where timber is scarce; ruins where buildings were
-- abandoned; weathered roofs on houses in poor repair. Stocks are visible: woodpiles by the
-- houses in proportion to the wood store, sacks by the granary for the grain, fish racks by
-- the pier for the fresh catch. Nothing here is invented: no building exists without its entity.
local SACK = { 0.86, 0.78, 0.6 }
local FISH = { 0.72, 0.78, 0.82 }
local PLANK = { 0.62, 0.44, 0.28 }
local EARTH_WALL = { 0.66, 0.52, 0.38 }
local TURF = { 0.42, 0.55, 0.28 }

--- Lays out village entry e ({ cell, id, ruin, culture }) into the instance list.
-- ctx.surface(x, y, z) -> radius, height (km; <= 0 = water).
function M.plan_village(ctx, e)
  local W = ctx.W
  local v = W.ent and W.ent.villages and W.ent.villages[e.id]
  if not v then return end
  local B = W.ent.buildings or {}
  local g = W.grid
  local c = v.cell
  local S = M.SCALE * M.BUILDING
  local style = STYLES[((v.culture or 1) - 1) % #STYLES + 1]
  local roofs, walls = ROOFS[style.roof], WALLS[style.wall]
  local clay_q, stone_q, wood_q = M.village_supply(W, v)
  local chim = {}
  M.chimneys[c] = chim
  -- a land point for a lot: lots whose ring crosses the shore are pulled in toward the cell centre
  local function land_point(bc, x, y, z)
    local cx, cy, cz = g.X[bc], g.Y[bc], g.Z[bc]
    for k = 0, 4 do
      local f = k * 0.22
      local px, py, pz = x + (cx - x) * f, y + (cy - y) * f, z + (cz - z) * f
      local l = sqrt(px * px + py * py + pz * pz)
      px, py, pz = px / l, py / l, pz / l
      local r, hk = ctx.surface(px, py, pz)
      if hk > 0.02 then return px, py, pz, r end
    end
    return nil
  end
  -- yaw so that the model's +z (door) faces unit vector (tx, ty, tz) from (x, y, z)
  local function yaw_to(x, y, z, tx, ty, tz)
    local ex, ey, ez, nx, ny, nz = frame(x, y, z)
    local dx, dy, dz = tx - x, ty - y, tz - z
    return atan2(dx * ex + dy * ey + dz * ez, dx * nx + dy * ny + dz * nz)
  end
  local function put(kind, x, y, z, r, yaw, sc, c1, c2)
    add(kind, x * r, y * r, z * r, x, y, z, yaw, sc * S, 1, c1, c2, 0, c)
    local ch = CHIMNEY[NAMES[kind]]
    if ch then
      local wx, wy, wz = to_world(x * r, y * r, z * r, x, y, z, yaw, sc * S, ch[1], ch[2], ch[3])
      chim[#chim + 1] = { wx, wy, wz }
    end
  end
  -- a point `d` world units from unit vector (x, y, z) at angle a in its tangent plane
  local function offset(x, y, z, a, d)
    local ex, ey, ez, nx, ny, nz = frame(x, y, z)
    local dd = d / 10
    local px, py, pz = x + (ex * cos(a) + nx * sin(a)) * dd, y + (ey * cos(a) + ny * sin(a)) * dd,
      z + (ez * cos(a) + nz * sin(a)) * dd
    local l = sqrt(px * px + py * py + pz * pz)
    return px / l, py / l, pz / l
  end
  local alive = v.alive ~= false
  -- the well on the square (a living village)
  local wx, wy, wz = layout.pos(W, v, c, 0)
  if alive then
    local r = ctx.surface(wx, wy, wz)
    put(K.well, wx, wy, wz, r, (v.key_cell or 0) % 7, 1.1, pack_rgb(unpack(roofs[1])), 0)
    -- the polity's banner beside the well (W9): the paramount's taller
    local top = M.polity_of(W, v)
    if top then
      local c1, c2 = M.polity_colours(top)
      local bx, by, bz = offset(wx, wy, wz, ((v.key_cell or 0) % 11) * 0.57 + 1.2, 0.05)
      local rb = ctx.surface(bx, by, bz)
      put(K.banner, bx, by, bz, rb, 0.4, top == v and 1.35 or 1.0, c1, c2)
    end
  end
  local houses, granary, pier = {}, nil, nil
  local list = v.buildings or {}
  -- standing buildings per cell (crowded cells build tighter)
  local crowd = {}
  for i = 1, #list do
    local b = B[list[i]]
    if b and (b.state == "standing" or b.state == "site") then crowd[b.cell] = (crowd[b.cell] or 0) + 1 end
  end
  -- the prestige of the top fifth of the village's households (two-storey houses)
  local H = W.ent.households or {}
  local prestige_hi = 0
  do
    local ps = {}
    for i = 1, #(v.households or {}) do
      local hh = H[v.households[i]]
      if hh then ps[#ps + 1] = hh.prestige or 0 end
    end
    table.sort(ps)
    if #ps >= 5 then prestige_hi = ps[floor(#ps * 0.8) + 1] end
  end
  for i = 1, #list do
    local b = B[list[i]]
    if b and b.state ~= "gone" then
      local lot = b.lot
      local bx, by, bz
      if b.kind == "boat" then
        -- boats are built on the shore by the pier
        local pl = v.pier_lot or 1
        bx, by, bz = layout.pos(W, v, v.cell, pl)
        bx, by, bz = offset(bx, by, bz, (b.id % 5) * 1.3, 0.05)
      else
        bx, by, bz = layout.pos(W, v, b.cell, lot)
      end
      local x, y, z, r = land_point(b.cell, bx, by, bz)
      if x then
        local key = b.key or b.id
        local sx, sy, sz = layout.pos(W, v, b.cell, 0)
        local yaw = yaw_to(x, y, z, sx, sy, sz) + ((key % 97) / 97 - 0.5) * 0.3
        local st = b.state
        -- the household's standing among the village's (0..1) picks the better materials
        local hh0 = b.household and b.household ~= 0 and H[b.household]
        local stand = 0
        if hh0 and prestige_hi > 0.3 then stand = min(1, (hh0.prestige or 0) / prestige_hi) end
        if hh0 and hh0.id == v.leader_h then stand = 1 end
        local roof, wall = M.house_colours(style, clay_q, stone_q, wood_q, stand, ((key * 0.6180339) % 1), key)
        if b.kind == "hut" then roof = ROOF_MAT.thatch[key % 4 + 1] end
        local cond = b.condition or 1
        local wear = cond < 0.5 and 0.72 or (cond < 0.8 and 0.88 or 1)
        local c1 = pack_rgb(roof[1] * wear, roof[2] * wear, roof[3] * wear)
        local ww = 0.9 + 0.1 * wear
        local c2 = pack_rgb(wall[1] * ww, wall[2] * ww, wall[3] * ww)
        local sc = 0.95 + (key % 13) / 13 * 0.12
        -- a crowded cell (a town) builds smaller, tighter houses
        if (crowd[b.cell] or 0) > 14 then sc = sc * 0.84 end
        if st == "ruin" then
          put(K.ruin, x, y, z, r, yaw, 0.95, pack_rgb(0.55, 0.52, 0.48), 0)
        elseif b.kind == "pier" then
          -- the pier reaches from the shore out toward the mooring
          local mc = v.mooring
          if mc and st == "standing" then
            local pyaw = yaw_to(x, y, z, g.X[mc], g.Y[mc], g.Z[mc])
            put(K.pier, x, y, z, r, pyaw, 1.0, 0, 0)
            pier = { x, y, z, r, pyaw }
          elseif st == "site" then
            put(K.stakes, x, y, z, r, yaw, 0.8, 0, 0)
          end
        elseif b.kind == "boat" then
          if st == "site" then put(K.hull, x, y, z, r, yaw, 1.0, pack_rgb(PLANK[1], PLANK[2], PLANK[3]), 0) end
        elseif st == "site" then
          local pg = b.progress or 0
          local kind = pg < 0.25 and K.stakes or (pg < 0.6 and K.frame or K.walls)
          local cw = c2
          if b.material == "earth" then
            cw = pack_rgb(EARTH_WALL[1], EARTH_WALL[2], EARTH_WALL[3])
            if pg >= 0.6 then kind = K.sodhouse; c1 = pack_rgb(TURF[1] * 0.8, TURF[2] * 0.7, TURF[3] * 0.6) end
          end
          put(kind, x, y, z, r, yaw, sc, c1, cw)
        elseif st == "standing" then
          local kind
          if b.kind == "house" then
            if b.material == "earth" then
              kind = K.sodhouse
              c1 = pack_rgb(TURF[1], TURF[2], TURF[3])
              c2 = pack_rgb(EARTH_WALL[1] * wear, EARTH_WALL[2] * wear, EARTH_WALL[3] * wear)
            else
              kind = K[style.houses[key % #style.houses + 1]]
              -- who lives there (W8): the leader's household in a hall, big households in
              -- longhouses, the most respected in two-storey houses
              local hh = b.household ~= 0 and H[b.household]
              if hh then
                if hh.id == v.leader_h then kind = K.hall
                elseif #hh.members >= 7 then kind = K.longhouse
                elseif (hh.prestige or 0) >= prestige_hi and prestige_hi > 0.3 then kind = K.townhouse end
              end
            end
          elseif b.kind == "hut" then kind = K.hut
          elseif b.kind == "granary" then kind = K.granary; granary = { x, y, z, r }
          elseif b.kind == "woodshed" then kind = K.woodshed
          end
          if kind then
            -- long buildings stand gable-on to the square where the ring is crowded
            if (kind == K.longhouse or kind == K.hall) and (crowd[b.cell] or 0) > 14 then yaw = yaw + pi * 0.5 end
            put(kind, x, y, z, r, yaw, sc, c1, c2)
            if b.kind == "house" or b.kind == "hut" then houses[#houses + 1] = { x, y, z, r, yaw, key } end
          end
        end
      end
    end
  end
  -- a palisade (W8) rings the village at the edge of its lots: stakes all round when standing,
  -- the part already raised while it is being built (a gap for the gate facing the sea or square)
  for i = 1, #list do
    local b = B[list[i]]
    if b and b.kind == "palisade" and (b.state == "standing" or b.state == "site" or b.state == "ruin") then
      local rad = (e.radius or 0.3) + 0.04
      local circ = 2 * pi * rad
      local nseg = max(8, floor(circ / (0.1 * S) + 0.5))
      local frac = b.state == "site" and (b.progress or 0) or (b.state == "ruin" and 0.4 or 1)
      local cond = b.condition or 1
      local ex, ey, ez, nx, ny, nz = frame(wx, wy, wz)
      local shown = floor(nseg * frac + 0.5)
      for k = 0, shown - 1 do
        if k ~= 0 then
          local a = (k + 0.5) / nseg * 2 * pi
          local d = rad / 10
          local px, py, pz = wx + (ex * cos(a) + nx * sin(a)) * d, wy + (ey * cos(a) + ny * sin(a)) * d,
            wz + (ez * cos(a) + nz * sin(a)) * d
          local l = sqrt(px * px + py * py + pz * pz)
          px, py, pz = px / l, py / l, pz / l
          local r, hk = ctx.surface(px, py, pz)
          if hk > 0.02 and (b.state ~= "ruin" or (k * 7) % 3 == 0) then
            local yaw = yaw_to(px, py, pz, wx, wy, wz) + pi * 0.5
            local w = cond < 0.5 and 0.75 or 1
            put(K.palisade, px, py, pz, r, yaw, circ / nseg / (0.1 * S) * 1.02, pack_rgb(0.55 * w, 0.4 * w, 0.26 * w), 0)
          end
        end
      end
    end
  end
  if not alive then return end
  local st = v.stock or {}
  -- woodpiles beside the houses: one per ~80 WU in store
  local nw = min(#houses, floor((st.wood or 0) / 80))
  for i = 1, nw do
    local h = houses[i]
    local px, py, pz = offset(h[1], h[2], h[3], h[5] + pi * 0.5 + ((h[6] % 2) * pi), 0.075)
    local r, hk = ctx.surface(px, py, pz)
    if hk > 0.02 then put(K.woodpile, px, py, pz, r, h[5], 1.0, 0, 0) end
  end
  -- grain sacks by the granary (or the square): one per ~2,500 FU
  local ns = min(8, floor(((st.grain or 0) + (st.seed or 0)) / 2500))
  local gx, gy, gz = wx, wy, wz
  if granary then gx, gy, gz = granary[1], granary[2], granary[3] end
  for i = 1, ns do
    local px, py, pz = offset(gx, gy, gz, i * 2.4, granary and 0.06 or 0.045)
    local r, hk = ctx.surface(px, py, pz)
    if hk > 0.02 then put(K.sacks, px, py, pz, r, i * 1.7, 1.0, pack_rgb(SACK[1], SACK[2], SACK[3]), 0) end
  end
  -- the pen where the herd sleeps (W10), at the edge of the village away from the kiln
  if v.herd and require("sim.society.herds").se(v.herd) > 5 then
    local pa = ((v.key_cell or 0) % 17) / 17 * 2 * pi + pi
    local px, py, pz = offset(wx, wy, wz, pa, (e.radius or 0.3) * 0.95)
    local r, hk = ctx.surface(px, py, pz)
    if hk > 0.02 then put(K.pen, px, py, pz, r, pa, 1.0, 0, 0) end
    M.pens[v.id] = { px, py, pz }
  end
  -- a pottery (crafts): the kiln at the edge of the village while pots are being made, pots stacked
  -- by it in proportion to the stock
  local acc = v.acc or {}
  local accl = v.acc_last or {}
  if (acc.made_pots or 0) + (accl.made_pots or 0) > 3 then
    local ka = ((v.key_cell or 0) % 17) / 17 * 2 * pi
    local px, py, pz = offset(wx, wy, wz, ka, (e.radius or 0.3) * 0.85)
    local r, hk = ctx.surface(px, py, pz)
    if hk > 0.02 then
      put(K.kiln, px, py, pz, r, ka, 1.0, 0, 0)
      local np = min(4, floor((st.pots or 0) / max(1, v.pop * 0.15)))
      for i = 1, np do
        local qx, qy, qz = offset(px, py, pz, ka + i * 1.3, 0.05)
        local r2, hk2 = ctx.surface(qx, qy, qz)
        if hk2 > 0.02 then put(K.pots, qx, qy, qz, r2, i * 2.1, 0.9, 0, 0) end
      end
    end
  end
  -- fish racks by the pier while the catch is in
  if pier then
    local nf = min(4, floor((st.fresh or 0) / 120))
    for i = 1, nf do
      local px, py, pz = offset(pier[1], pier[2], pier[3], pier[5] + pi + (i - 2.5) * 0.7, 0.07)
      local r, hk = ctx.surface(px, py, pz)
      if hk > 0.02 then put(K.fishrack, px, py, pz, r, pier[5] + pi * 0.5, 1.0, pack_rgb(FISH[1], FISH[2], FISH[3]), 0) end
    end
  end
end

--- Signature of what plan_village draws for village v (rebuild the chunk when it changes).
-- The paramount of the polity village v belongs to (nil when it stands alone).
function M.polity_of(W, v)
  local vs = W.ent and W.ent.villages
  if not vs then return nil end
  local top, guard = v, 0
  while top.lord and vs[top.lord] and vs[top.lord].alive and guard < 10 do top = vs[top.lord]; guard = guard + 1 end
  if top ~= v then return top end
  -- a paramount: does anyone pay it?
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u.lord == v.id then return v end
  end
  return nil
end

-- A polity's colours: from its paramount (a hue by its id, the stripe light).
function M.polity_colours(top)
  local h = ((top.id * 0.6180339) % 1) * 6
  local k = floor(h)
  local f = h - k
  local r, g, b
  if k == 0 then r, g, b = 1, f, 0 elseif k == 1 then r, g, b = 1 - f, 1, 0
  elseif k == 2 then r, g, b = 0, 1, f elseif k == 3 then r, g, b = 0, 1 - f, 1
  elseif k == 4 then r, g, b = f, 0, 1 else r, g, b = 1, 0, 1 - f end
  local d = 0.78
  return pack_rgb(0.12 + r * d * 0.8, 0.12 + g * d * 0.8, 0.12 + b * d * 0.8), pack_rgb(0.95, 0.93, 0.86)
end

function M.village_sig(W, v)
  local B = W.ent.buildings or {}
  local s = 0
  local list = v.buildings or {}
  for i = 1, #list do
    local b = B[list[i]]
    if b and b.state ~= "gone" then
      local stage = b.state == "site" and (1 + floor((b.progress or 0) * 3.3)) or (b.state == "ruin" and 7 or 9)
      local cond = b.condition or 1
      local wear = cond < 0.5 and 2 or (cond < 0.8 and 1 or 0)
      s = (s * 31 + b.id * 16 + stage * 3 + wear) % 1000000007
    end
  end
  local st = v.stock or {}
  s = (s + floor((st.wood or 0) / 80) * 7919 + floor(((st.grain or 0) + (st.seed or 0)) / 2500) * 104729
    + floor((st.fresh or 0) / 120) * 1299709) % 1000000007
  s = (s + (v.leader_h or 0) * 7727) % 1000000007
  -- the polity it belongs to (a banner on the square)
  local top = M.polity_of(W, v)
  if top then s = (s + top.id * 49979687) % 1000000007 end
  local pots = ((v.acc and v.acc.made_pots or 0) + (v.acc_last and v.acc_last.made_pots or 0) > 3) and 1 or 0
  s = (s + pots * 15485863 + min(4, floor((st.pots or 0) / math.max(1, v.pop * 0.15))) * 32452843) % 1000000007
  return s
end

---------------------------------------------------------------------------------------------
-- Chunks

--- Plans the props of one chunk into M.list. ctx = { W, PL, villages (cell -> {...}),
-- lod (1 near / 2 far), seed, surface, shore, owned (village cells owned by this chunk) }.
-- Returns instance count and vertex count.
function M.plan_chunk(ctx, ch, i0, i1, append)
  if not append then L.n, L.verts = 0, 0 end
  L.lod = ctx.lod or 2
  local near = ctx.lod ~= 2
  local W, PL = ctx.W, ctx.PL
  local F = W.F
  local forest, grass, biome, land_use = F.forest, F.grass, F.biome, F.land_use
  local fire, scar, ice, soil = F.fire, F.burn_scar, F.ice, F.soil_water
  local grazers = F.grazers
  local NZ = M.noise_for(ctx.seed or 1)
  local ocean, cdist = W.geo.ocean, W.geo.coast_dist
  local T = W.temp_static
  local gtemp = W.G.g_temp or 0
  local ruins = W.S and W.S.ruins
  local villages = ctx.villages
  local farm = W.S and W.S.farm
  local faces, px, py, pz, ph = PL.faces, PL.px, PL.py, PL.pz, PL.h
  local tland, tc1, tc2, tc3, tw1, tw2, tw3 = PL.tland, PL.tc1, PL.tc2, PL.tc3, PL.tw1, PL.tw2, PL.tw3
  local SX, SY, SZ = W.grid.X, W.grid.Y, W.grid.Z
  local spacing = W.grid.spacing * PL.R
  local slots = near and M.SLOTS or M.SLOTS_FAR
  local seed = ctx.seed or 1
  local S = near and M.SCALE or M.SCALE_FAR
  local list = ch.faces
  for li = i0 or 1, math.min(i1 or #list, #list) do
    local f = list[li]
    if tland[f] == 1 then
      local a1, a2, a3 = tc1[f], tc2[f], tc3[f]
      local w1, w2, w3 = tw1[f], tw2[f], tw3[f]
      local c = a1
      if ocean[c] == 0 then
        local fo = forest[a1] * w1 + forest[a2] * w2 + forest[a3] * w3
        local ic = ice[a1] * w1 + ice[a2] * w2 + ice[a3] * w3
        -- no trees standing on snowfields: fade out between 30% and 55% ice cover
        local snowfade = ic <= 0.3 and 1 or (ic >= 0.55 and 0 or (0.55 - ic) / 0.25)
        fo = fo * snowfade
        local gr = grass[a1] * w1 + grass[a2] * w2 + grass[a3] * w3
        local bi = biome[c]
        local lu = land_use[c]
        local pv = ctx.preview and ctx.preview[c]
        if pv then lu = pv end
        local tmean = T and T.base and (T.base[c] + T.lapse[c] + gtemp) or 10
        local burnt = fire[c] > 0 or (scar[c] > 0 and scar[c] < 240)
        local vil = villages and villages[c]
        local logged = ctx.logged and ctx.logged[c]
        local is_ruin = (ruins and ruins[c]) or (vil and vil.ruin) or bi == 6
        local i = f * 3 - 2
        local va, vb, vc = faces[i], faces[i + 1], faces[i + 2]
        local ax, ay, az = px[va], py[va], pz[va]
        local bx, by, bz = px[vb], py[vb], pz[vb]
        local cx, cy, cz = px[vc], py[vc], pz[vc]
        local hmax = max(ph[va], max(ph[vb], ph[vc]))
        local hmin = min(ph[va], min(ph[vb], ph[vc]))
        -- distance of the face centre from its main cell centre (keeps villages' squares clear)
        local mx, my, mz = ax + bx + cx, ay + by + cy, az + bz + cz
        local ml = sqrt(mx * mx + my * my + mz * mz)
        local dcell = 1 - (mx * SX[c] + my * SY[c] + mz * SZ[c]) / ml
        dcell = sqrt(max(0, 2 * dcell)) * PL.R / spacing   -- ~0 at the cell centre, ~0.5 at its edge
        -- settlements keep their ground clear: distance to any living village among the face's cells
        local in_village = false
        if villages then
          for q = 1, 3 do
            local vc = q == 1 and a1 or (q == 2 and a2 or a3)
            local vv = villages[vc]
            if vv and not vv.ruin then
              local dot = (mx * SX[vc] + my * SY[vc] + mz * SZ[vc]) / ml
              local dw = sqrt(max(0, 2 * (1 - dot))) * PL.R
              local rv = vv.radius or 0.3
              if dw < rv then in_village = true end
            end
          end
        end
        if lu < 0 and dcell < 0.3 then in_village = true end
        local tree_p = min(1, fo * 1.35) ^ 0.85
        local rocky = hmax > 2.2 or (hmax - hmin) > 0.5
        local coast = cdist[c] <= 1
        local sw = soil[c]
        local grz = grazers[c]
        -- forest structure from low-frequency noise over the face (stable per world): glades
        -- (windthrow, old clearings) open the canopy of dense forests; stands give patches of one
        -- species and hue instead of salt-and-pepper mixes
        local fx, fy, fz = mx / ml, my / ml, mz / ml
        local glade = 0
        if fo > 0.45 then
          local n = vnoise.value(NZ, fx * 26 + 3.1, fy * 26 - 1.7, fz * 26 + 0.4)
          glade = n <= 0.66 and 0 or (n >= 0.8 and 1 or (n - 0.66) / 0.14)
        end
        local stand = vnoise.value(NZ, fx * 14 - 5.3, fy * 14 + 2.2, fz * 14 + 7.9)
        local canopy = min(1, fo * (1 - 0.85 * glade) * 1.35) ^ 0.85
        local young = burnt and (fire[c] > 0 or scar[c] < 48)
        local quarry = ctx.quarries and ctx.quarries[c]
        local h = (f * 7919 + seed * 104729) % 2147483647
        if h == 0 then h = 1 end
        for j = 1, slots do
          h = pm(h + j * 131); local u1 = h / 2147483647
          h = pm(h); local u2 = h / 2147483647
          h = pm(h); local u3 = h / 2147483647
          h = pm(h); local u4 = h / 2147483647
          h = pm(h); local u5 = h / 2147483647
          local kind = nil
          local scale, hs = 1, 1
          -- slot roles: 1 canopy trees, 2 understory, 3 shrubs, 4 forest floor / ground cover,
          -- 5 deadwood, 6 rocks (the far set keeps trees, a shrub and a rock)
          local role
          if near then
            role = (j <= 10 and 1) or (j <= 13 and 2) or (j <= 17 and 3) or (j <= 22 and 4) or (j == 23 and 5) or 6
          else
            role = (j <= 5 and 1) or (j == 6 and 3) or 6
          end
          if in_village then
            -- the settlement layout owns the village square
          elseif is_ruin and dcell < 0.4 then
            -- nature takes the ruins back: bushes, grass, saplings
            if role == 3 and u3 < 0.5 then kind = K.bush1; scale = 0.7 + u5 * 0.4
            elseif role == 4 and u3 < 0.6 then kind = K.grass2
            elseif role == 2 and u3 < 0.35 then kind = K.sapling_b; scale = 0.8 + u5 * 0.6 end
          elseif lu > 0 then
            -- farm: the plots are drawn by the terrain shader; stooks where the harvest has been
            -- cut (the sim's field state); hedgerows along the field margins
            local fs = farm and farm[c]
            local cut = fs and fs.sown > 0 and fs.harvested > 0.03 and fs.harvested * fs.sown or 0
            if role == 1 and j <= 4 and u2 < cut * 0.85 then kind = K.stook; scale = 0.85 + u5 * 0.3
            elseif role == 1 and dcell > 0.34 and u3 < 0.06 then
              kind = pick_tree(tmean, ic, bi, sw, coast, u4, stand); scale = 0.75 + u5 * 0.4
            elseif role == 3 and dcell > 0.32 and u3 < 0.3 then kind = (u4 < 0.6) and K.bush1 or K.berry; scale = 0.7 + u5 * 0.4
            elseif role == 4 and dcell > 0.3 and u3 < 0.35 then kind = (u4 < 0.4 and tmean > 4) and K.meadow or K.grass2 end
          elseif burnt then
            -- a burnt stand: black snags; then fireweed, bushes and saplings come back
            if role == 1 then
              if u3 < tree_p * 0.6 + 0.15 then kind = (young or u4 < 0.6) and K.burnt1 or K.snag1; scale = 0.8 + u5 * 0.5
              elseif u3 < tree_p * 0.6 + 0.3 then kind = K.stump; scale = 0.8 + u5 * 0.4 end
            elseif role == 5 and u3 < 0.5 then kind = K.deadfall; scale = 0.8 + u5 * 0.4
            elseif not young then
              if role == 2 and u3 < 0.7 then kind = (tmean < 6) and K.sapling_c or K.sapling_b; scale = 0.6 + u5 * 0.8
              elseif role == 3 and u3 < 0.35 then kind = K.bush1; scale = 0.6 + u5 * 0.4
              elseif role == 4 and u3 < 0.55 then kind = (u4 < 0.5) and K.meadow or K.grass1 end
            end
          elseif bi == 3 then
            if role == 6 and u3 < 0.2 then kind = K.boulder; scale = 0.6 + u5 * 0.8 end
          elseif bi == 9 then
            -- desert: rock, scattered dry shrubs, cacti where it is hot
            if role == 6 and u3 < 0.07 + (rocky and 0.2 or 0) then kind = (u4 < 0.5) and K.slab or K.boulder; scale = 0.4 + u5 * u5 * 1.6
            elseif role == 3 and u3 < 0.1 + gr * 0.3 then kind = (tmean > 18 and u4 < 0.45) and K.cactus or K.dryshrub; scale = 0.7 + u5 * 0.6
            elseif role == 4 and u3 < gr * 0.4 then kind = K.tallgrass; scale = 0.6 + u5 * 0.4
            elseif role == 1 and u3 < tree_p then kind = pick_tree(tmean, ic, bi, sw, coast, u4, stand); scale = 0.6 + u5 * 0.4 end
          elseif bi == 8 then
            -- marsh: reeds and rushes, willow-like bushes, a few trees
            if (role == 4 or role == 3) and u3 < 0.55 then kind = (u4 < 0.7) and K.reed or K.tallgrass; scale = 0.8 + u5 * 0.5
            elseif role == 2 and u3 < 0.25 then kind = K.bush2; scale = 0.8 + u5 * 0.5
            elseif role == 1 and u3 < tree_p * 0.6 then kind = pick_tree(tmean, ic, bi, sw, coast, u4, stand); scale = 0.7 + u5 * 0.4 end
          elseif (bi == 10 or bi == 14) and fo < 0.3 then
            -- tundra and barren ground: rocks, heather, low junipers, thin grass
            if role == 6 and u3 < 0.3 then kind = (u4 < 0.5) and K.slab or K.boulder; scale = 0.5 + u5 * 0.8
            elseif role == 4 and u3 < 0.45 then kind = (tmean < 5 and u4 < 0.5) and K.heather or K.grass2; scale = 0.7 + u5 * 0.5
            elseif role == 3 and u3 < 0.12 then kind = K.juniper; scale = 0.6 + u5 * 0.5
            elseif role == 1 and u3 < tree_p then kind = pick_tree(tmean, ic, bi, sw, coast, u4, stand); scale = 0.55 + u5 * 0.4 end
          elseif role == 1 then
            if u3 < canopy then
              kind = pick_tree(tmean, ic, bi, sw, coast, u4, stand)
              -- denser forests grow taller; a few veterans stand over the canopy
              scale = (0.72 + u5 * 0.5) * (0.78 + 0.32 * fo)
              if u5 > 0.94 and fo > 0.6 then scale = scale * 1.25 end
              hs = 0.88 + u2 * 0.3
            elseif logged and u3 < tree_p + 0.22 then
              -- woodcutters were here lately: stumps and felled logs where trees stood
              kind = (u4 < 0.7) and K.stump or K.logs; scale = 0.8 + u5 * 0.4
            elseif not near and fo < 0.3 and u3 < canopy + gr * 0.08 then
              kind = (tmean > 18 and sw < 0.35) and K.acacia2 or K.bush2; scale = 0.8 + u5 * 0.5
            end
          elseif role == 2 then
            -- understory: young trees where light reaches the floor (edges, glades, thin canopy)
            local p = fo * 0.55 * (0.35 + 0.65 * (1 - canopy)) + glade * 0.35
            if u3 < p then
              if tmean < 6 then kind = (u4 < 0.6) and K.sapling_c or K.spruce3
              elseif bi == 11 or tmean > 20 then kind = (u4 < 0.5) and K.bush2 or K.jungle3
              else kind = (u4 < 0.45) and K.sapling_b or ((u4 < 0.75) and K.birch2 or K.bush2) end
              scale = 0.45 + u5 * 0.45
            end
          elseif role == 3 then
            -- shrubs: forest edges and open woods; browsing deer keep them down
            local p = (0.06 + 1.1 * fo * (1 - fo) + gr * 0.12) * (1 - min(0.7, grz * 1.5))
            if u3 < p then
              if tmean < 3 then kind = (u4 < 0.6) and K.juniper or K.berry
              elseif tmean > 18 and sw < 0.35 then kind = K.dryshrub
              elseif bi == 11 then kind = K.bush2
              else kind = (u4 < 0.4) and K.bush1 or ((u4 < 0.75) and K.bush2 or K.berry) end
              scale = 0.6 + u5 * 0.55
            elseif coast and hmin < 0.12 and u3 < p + 0.1 then kind = K.reed; scale = 0.8 + u5 * 0.4 end
          elseif role == 4 then
            if fo > 0.4 and sw > 0.3 and u3 < fo * 0.75 then
              -- the forest floor: ferns in moist woods, the odd ring of mushrooms
              kind = (u4 < 0.08) and K.shroom or ((u4 < 0.55) and K.fern or K.fern2); scale = 0.7 + u5 * 0.6
            elseif fo < 0.6 and u3 < gr * 0.95 + glade * 0.4 then
              -- open ground: grass, meadow flowers in the warm, tall dry grass in savannas, heather in the cold
              if tmean > 18 and sw < 0.4 then kind = K.tallgrass
              elseif tmean < 4 and u4 < 0.4 then kind = K.heather
              elseif tmean > 4 and u4 < 0.35 + glade * 0.3 then kind = K.meadow
              else kind = (u4 < 0.7) and K.grass1 or K.grass2 end
              scale = 0.75 + u5 * 0.5
            end
          elseif role == 5 then
            -- deadwood: standing snags and fallen trunks in old forests and glades
            if u3 < fo * 0.1 + glade * 0.3 then kind = (u4 < 0.6) and K.deadfall or K.snag1; scale = 0.7 + u5 * 0.5
            elseif coast and hmin < 0.05 and u3 < 0.12 then kind = K.logs; scale = 0.7 + u5 * 0.4   -- driftwood
            end
          else
            -- rocks: outcrops on steep or high ground, boulders on rocky shores, mossy stones in woods;
            -- a quarry where a village works its stone (crafts: tools)
            if quarry and u3 < 0.7 then kind = (u4 < 0.6) and K.slab or K.boulder; scale = 0.4 + u5 * 0.7
            elseif rocky and u3 < 0.35 then kind = (u4 < 0.4) and K.slab or K.boulder; scale = 0.5 + u5 * 1.0
            elseif coast and hmin < 0.1 and (hmax - hmin) > 0.22 and u3 < 0.4 then kind = K.boulder; scale = 0.5 + u5 * 0.9
            elseif fo > 0.5 and sw > 0.35 and u3 < 0.12 then kind = K.mossrock; scale = 0.5 + u5 * 0.7
            elseif u3 < 0.025 then kind = K.boulder; scale = 0.4 + u5 * 0.5 end
          end
          if kind and PL.rdist then
            -- (S14) the river's channel and floodplain stay open (grass, reeds); above the tree line
            -- (the point's own mean temperature: its cell's, 6.5 C cooler a km above the cell's
            -- ground) no trees: heather, grass and stones
            local r1 = sqrt(u1)
            local ka, kb, kc = 1 - r1, r1 * (1 - u2), r1 * u2
            local RD, RW, EC = PL.rdist, PL.rhw, PL.ecell
            local rd = min(RD[va], 2) * ka + min(RD[vb], 2) * kb + min(RD[vc], 2) * kc
            local rw = max(RW[va], max(RW[vb], RW[vc]))
            local wet = rd < rw * 2.0 + 0.07
            local hp = ph[va] * ka + ph[vb] * kb + ph[vc] * kc
            local ep = EC[va] * ka + EC[vb] * kb + EC[vc] * kc
            local tp = tmean - 6.5 * 0.35 * max(0, hp - ep)
            local woody = role == 1 or role == 2 or role == 5
            if wet then
              if role ~= 4 then kind = nil
              elseif u4 < 0.5 then kind = K.reed end
            elseif tp < 5 then
              local alp = tp <= 1.5 and 1 or (5 - tp) / 3.5
              if woody and u5 < alp then kind = nil
              elseif role == 3 and u5 < alp * 0.7 then kind = K.juniper; scale = 0.5 + u3 * 0.3
              elseif role == 4 and u5 < alp then kind = (u4 < 0.5) and K.heather or K.grass2 end
            end
            if not kind and tp < 1.5 and role == 6 and u3 < 0.3 then kind = (u4 < 0.5) and K.slab or K.boulder; scale = 0.5 + u5 * 0.9 end
          end
          if kind and TPL[L.lod][kind] then
            -- uniform point in the triangle
            local r1 = sqrt(u1)
            local ka, kb, kc = 1 - r1, r1 * (1 - u2), r1 * u2
            local yaw = u2 * 2 * pi + u5 * 7
            local x = ax * ka + bx * kb + cx * kc
            local y = ay * ka + by * kb + cy * kc
            local z = az * ka + bz * kb + cz * kc
            local l = sqrt(x * x + y * y + z * z)
            local pal = PAL[kind]
            -- stands bias the hue pick, so neighbouring trees of a stand look related
            local pick = u4 * 0.55 + stand * 0.45
            local col = pal and pal[floor(pick * 0.9999 * #pal) + 1] or { 0.6, 0.6, 0.6 }
            local jit = 0.88 + u5 * 0.24
            local c1 = pack_rgb(col[1] * jit, col[2] * jit, col[3] * jit)
            local bark = BARK[kind]
            local c2 = bark and pack_rgb(bark[1], bark[2], bark[3]) or c1
            local acc = ACCENT[kind]
            local c3 = acc and acc[floor(u3 * 9973) % #acc + 1] or P_SNOW
            add(kind, x, y, z, x / l, y / l, z / l, yaw, scale * S, hs, c1, c2, c3, c, (u1 * 7.31 + u5) % 1)
          end
        end
      end
    end
  end
  -- settlements whose centre this chunk owns (after the last face batch)
  if ctx.owned and (i1 == nil or i1 >= #list) then
    for _, vc in ipairs(ctx.owned) do
      local v = villages and villages[vc]
      if v then M.plan_village(ctx, v) end
    end
  end
  return L.n, L.verts
end

---------------------------------------------------------------------------------------------
-- Baking into streams

-- Template buffers (renderer v4 "instanced bake", D-056): every template's vertices once, as a
-- Defold buffer with the chunk's template streams (position, normal, tattr = slot, shade, sway,
-- fixed rgb or tint). Chunks copy them natively (buffer.copy_buffer); the instance data (origin,
-- yaw, scale, cell, classes, colours, random) is written once per instance and replicated over
-- its vertices by doubling copies, and props.vp does the transform.
local H_POS, H_NRM, H_TATTR = hash("position"), hash("normal"), hash("tattr")
local H_INST, H_INST2, H_INST3 = hash("inst"), hash("inst2"), hash("inst3")
M.H_INST, M.H_INST2, M.H_INST3 = H_INST, H_INST2, H_INST3
local TBUF = { {}, {} }

-- v3 (D-078): kinds added at runtime (crop rows, lane tiles), and a private instance list for
-- the v3 settlement renderer (game/view/agents_view.lua) — it builds synchronously, so it swaps
-- its own list in and the props job's list back before returning.
function M.register_kind(name, tnear, tfar, c1, c2, c3)
  local i = K[name]
  if not i then
    i = #NAMES + 1
    NAMES[i] = name
    K[name] = i
  end
  CLASS[i] = { c1 or 0, c2 or 0, c3 or 0 }
  local ts = { tnear, tfar or tnear }
  for lod = 1, 2 do
    local t = ts[lod]
    if not t.ss then prep(t) end
    TPL[lod][i] = t
    TBUF[lod][i] = nil
  end
  return i
end

function M.new_list()
  return { n = 0, verts = 0, lod = 1, kind = {}, x = {}, y = {}, z = {}, ux = {}, uy = {}, uz = {}, yaw = {},
    sc = {}, sh = {}, c1 = {}, c2 = {}, c3 = {}, cell = {}, rnd = {} }
end

function M.swap_list(nl)
  local old = L
  L = nl
  M.list = nl
  return old
end

-- placements inside these local squares are left to the v3 settlement renderer
M.v3_exclude = nil   -- list of { nx, ny, nz, ex, ey, ez, qx, qy, qz, half, R, cosr }

-- Is the unit point (ux, uy, uz) on a v3 settlement's square (drawn by agents_view)?
function M.in_v3_square(ux, uy, uz, margin)
  local ex = M.v3_exclude
  if not ex then return false end
  margin = margin or 0
  for q = 1, #ex do
    local e = ex[q]
    local d = ux * e[1] + uy * e[2] + uz * e[3]
    if d > e[12] then
      local k = e[11] / d
      local lx = (ux * e[4] + uy * e[5] + uz * e[6]) * k
      local ly = (ux * e[7] + uy * e[8] + uz * e[9]) * k
      local h = e[10] + margin
      if lx > -h and lx < h and ly > -h and ly < h then return true end
    end
  end
  return false
end

--- The stream declaration of prop chunk buffers.
function M.chunk_decl()
  return {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_TATTR, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_INST, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_INST2, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
    { name = H_INST3, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  }
end

function M.template_buffer(lod, kind)
  local tb = TBUF[lod][kind]
  if tb then return tb end
  local t = TPL[lod][kind]
  tb = buffer.create(t.nv, {
    { name = H_POS, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_NRM, type = buffer.VALUE_TYPE_FLOAT32, count = 3 },
    { name = H_TATTR, type = buffer.VALUE_TYPE_FLOAT32, count = 4 },
  })
  local pos, nrm, ta = buffer.get_stream(tb, H_POS), buffer.get_stream(tb, H_NRM), buffer.get_stream(tb, H_TATTR)
  local d, tn = t.d, t.tn
  local FIXED = M.FIXED
  for k = 0, t.nv - 1 do
    local o = k * 9
    local p, q = k * 3, k * 4
    pos[p + 1], pos[p + 2], pos[p + 3] = d[o + 1], d[o + 2], d[o + 3]
    nrm[p + 1], nrm[p + 2], nrm[p + 3] = d[o + 4], d[o + 5], d[o + 6]
    local slot = d[o + 7]
    ta[q + 1] = slot
    ta[q + 2] = min(1, max(0.5, d[o + 8]))
    ta[q + 3] = min(1, max(0, d[o + 9]))
    if slot >= 4 then ta[q + 4] = FIXED[slot] or 0
    else ta[q + 4] = tn and tn[k + 1] or 0 end
  end
  TBUF[lod][kind] = tb
  return tb
end

--- Writes instances [i0..i1] of M.list into chunk buffer `buf` (streams from M.chunk_decl;
-- S = { inst, inst2, inst3 } streams of buf) starting at vertex `vbase` (0-based). Returns the
-- next vertex index.
function M.write(buf, S, vbase, i0, i1)
  local copy = buffer.copy_buffer
  local kind, X, Y, Z = L.kind, L.x, L.y, L.z
  local YAW, SC, SH = L.yaw, L.sc, L.sh
  local C1, C2, C3, CELL, RND = L.c1, L.c2, L.c3, L.cell, L.rnd
  local lod = L.lod
  local tpls, tbufs = TPL[lod], TBUF[lod]
  local inst, inst2, inst3 = S.inst, S.inst2, S.inst3
  local v = vbase
  for i = i0, i1 do
    local kd = kind[i]
    local nv = tpls[kd].nv
    local tb = tbufs[kd] or M.template_buffer(lod, kd)
    local cl = CLASS[kd]
    local a = v * 4
    local s = SC[i]
    inst[a + 1], inst[a + 2], inst[a + 3], inst[a + 4] = X[i], Y[i], Z[i], YAW[i]
    inst2[a + 1], inst2[a + 2], inst2[a + 3] = s, s * SH[i], CELL[i]
    inst2[a + 4] = cl and (cl[1] + cl[2] * 8 + cl[3] * 64) or 0
    inst3[a + 1], inst3[a + 2], inst3[a + 3], inst3[a + 4] = C1[i], C2[i], C3[i], RND[i]
    -- replicate the instance over its vertices (doubling), then lay the template over them
    local k = 1
    while k < nv do
      local cnt = nv - k
      if cnt > k then cnt = k end
      copy(buf, v + k, buf, v, cnt)
      k = k + cnt
    end
    copy(buf, v, tb, 0, nv)
    v = v + nv
  end
  return v
end

--- Builds a standalone mover template. kind: deer | wolf | person[_straw|_cap] | cloud |
-- tanker | boat | ring | flame | smoke
function M.mover_template(kind)
  kit.build()
  if kind == "deer" or kind == "wolf" or kind == "boar" or kind == "hare" or kind == "cat" or kind == "bear" then
    return kit.quadruped(kind)
  end
  if kind == "dolphin" then return kit.dolphin() end
  if kind == "sheep" or kind == "goat" or kind == "cow" then return kit.quadruped(kind) end
  if kind == "plough" then
    -- (S14) the plough behind a yoked ox, in local units: the ploughman's hands at the origin, +x
    -- toward the ox (stock.YOKE_AHEAD ahead). Two handles slope down to the body and its iron share;
    -- the beam rises to the swingletree just behind the ox; traces run along its flanks to the yoke.
    local t = kit.new()
    -- (handles from the hands, ~0.038 up at the villagers' scale, down to the body at 0.034 ahead)
    for _, z in ipairs({ -0.0040, 0.0040 }) do
      kit.place(t, kit.box(nil, 0.0450, 0.0018, 0.0018, 1, 0.75, 1.0, true), 0.0215, 0.0200, z, 1, 1, 1, 0, 0, -0.97)
    end
    kit.place(t, kit.box(nil, 0.0016, 0.0016, 0.0100, 1, 0.75, 1.0, true), 0.0090, 0.0380, 0, 1)          -- the cross-grip
    kit.place(t, kit.box(nil, 0.0150, 0.0040, 0.0034, 2, 0.6, 0.9, true), 0.036, 0.0022, 0, 1)           -- body and share
    kit.place(t, kit.box(nil, 0.0256, 0.0022, 0.0022, 1, 0.75, 1.0, true), 0.044, 0.0095, 0, 1, 1, 1, 0, 0, 0.36)   -- beam
    kit.place(t, kit.box(nil, 0.0018, 0.0018, 0.0220, 1, 0.75, 1.0, true), 0.0565, 0.0140, 0, 1)         -- swingletree
    for _, z in ipairs({ -0.0105, 0.0105 }) do
      kit.place(t, kit.box(nil, 0.0450, 0.0009, 0.0009, 3, 0.7, 0.9, true), 0.0785, 0.0190, z, 1, 1, 1, 0, 0, 0.22)  -- traces
    end
    return t
  end
  if kind == "person" then return kit.person(nil) end
  if kind == "person_straw" then return kit.person("straw") end
  if kind == "person_cap" then return kit.person("cap") end
  if kind == "person_sack" then return kit.person(nil, "sack") end
  if kind == "person_logs" then return kit.person("cap", "logs") end
  if kind == "person_spear" then return kit.person(nil, "spear") end
  local t = kit.new()
  if kind == "cloud_cu1" or kind == "cloud_cu2" or kind == "cloud_deck" or kind == "cloud_tower" then
    return M.cloud_template(kind)
  end
  if kind == "cloud" then
    -- a cluster of soft puffs, ~1.0 across (scaled per cloud)
    kit.seed(3)
    kit.place(t, kit.lump(nil, 0.36, 0.26, 0.32, 10, 5, 1, 0.8, 1.0, 0, 0.06), 0, 0, 0, 1)
    for _, q in ipairs({ { 0.3, -0.04, 0.05, 0.25 }, { -0.3, -0.05, -0.04, 0.23 }, { 0.05, 0.13, -0.02, 0.23 },
        { -0.05, -0.06, 0.22, 0.2 }, { 0.12, 0.02, -0.22, 0.2 }, { -0.18, 0.08, 0.12, 0.18 } }) do
      kit.place(t, kit.lump(nil, q[4], q[4] * 0.82, q[4], 9, 4, 1, 0.8, 1.0, 0, 0.08), q[1], q[2], q[3], 1)
    end
    return t
  elseif kind == "bird" then
    -- a bird gliding: body along +x, wings a V (tips up). The view beats the wings by scaling
    -- local y (the V flattens and lifts), so they flap without a buffer swap.
    kit.place(t, kit.lump(nil, 0.012, 0.004, 0.0045, 5, 2, 1, 0.8, 1.0, 0, 0), 0, 0, 0, 1)
    kit.tri(t, { 0.004, 0, 0.002 }, { -0.004, 0, 0.002 }, { -0.002, 0.014, 0.03 }, nil, nil, nil, 2, 0.85, 0.85, 1.0, 0, 0, 0, 0, 1, 0)
    kit.tri(t, { -0.004, 0, 0.002 }, { 0.004, 0, 0.002 }, { -0.002, 0.014, 0.03 }, nil, nil, nil, 2, 0.8, 0.8, 0.95, 0, 0, 0, 0, -1, 0)
    kit.tri(t, { -0.004, 0, -0.002 }, { 0.004, 0, -0.002 }, { -0.002, 0.014, -0.03 }, nil, nil, nil, 2, 0.85, 0.85, 1.0, 0, 0, 0, 0, 1, 0)
    kit.tri(t, { 0.004, 0, -0.002 }, { -0.004, 0, -0.002 }, { -0.002, 0.014, -0.03 }, nil, nil, nil, 2, 0.8, 0.8, 0.95, 0, 0, 0, 0, -1, 0)
    kit.tri(t, { -0.01, 0, 0 }, { -0.018, 0.001, 0.005 }, { -0.018, 0.001, -0.005 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 0, 1, 0)
    return t
  elseif kind == "tanker" then
    -- hull (slot 1) along local x, deck house (slot 2) at the stern, funnel; ~0.36 long
    kit.place(t, kit.box(nil, 0.34, 0.05, 0.09, 1, 0.7, 0.95, false), 0, 0.005, 0, 1)
    kit.tri(t, { 0.17, -0.02, -0.045 }, { 0.24, 0.03, 0.0 }, { 0.17, 0.03, -0.045 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, -1)
    kit.tri(t, { 0.17, -0.02, 0.045 }, { 0.17, 0.03, 0.045 }, { 0.24, 0.03, 0.0 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, 1)
    kit.tri(t, { 0.17, 0.03, -0.045 }, { 0.24, 0.03, 0.0 }, { 0.17, 0.03, 0.045 }, nil, nil, nil, 1, 0.9, 0.9, 0.9, 0, 0, 0, 0, 1, 0)
    kit.place(t, kit.box(nil, 0.3, 0.004, 0.07, 6, 0.8, 0.9, false), 0.0, 0.032, 0, 1)
    for _, x in ipairs({ -0.06, 0.0, 0.06, 0.12 }) do
      kit.place(t, kit.cyl(nil, 0.012, 0.012, 0.03, 0.045, 6, 7, 0.75, 0.95, 0, true), x, 0, 0, 1)
    end
    kit.place(t, kit.box(nil, 0.06, 0.05, 0.07, 2, 0.8, 1.0, false), -0.13, 0.055, 0, 1)
    kit.place(t, kit.quad(nil, 0.05, 0.01, 4, 0.9), -0.13, 0.07, 0.0352, 1)
    kit.place(t, kit.cyl(nil, 0.009, 0.008, 0.08, 0.12, 6, 1, 0.8, 1.0, 0, true), -0.14, 0, 0, 1)
    return t
  elseif kind == "boat" then
    kit.place(t, kit.box(nil, 0.1, 0.02, 0.036, 1, 0.7, 0.95, false), 0, 0.0, 0, 1)
    kit.tri(t, { 0.05, -0.01, -0.018 }, { 0.075, 0.01, 0.0 }, { 0.05, 0.01, -0.018 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, -1)
    kit.tri(t, { 0.05, -0.01, 0.018 }, { 0.05, 0.01, 0.018 }, { 0.075, 0.01, 0.0 }, nil, nil, nil, 1, 0.8, 0.9, 0.9, 0, 0, 0, 1, 0, 1)
    kit.place(t, kit.cyl(nil, 0.0025, 0.002, 0.0, 0.1, 4, 6, 0.7, 0.9, 0), 0.0, 0.0, 0, 1)
    kit.tri(t, { 0.002, 0.02, 0.0 }, { 0.002, 0.095, 0.0 }, { 0.05, 0.025, 0.0 }, nil, nil, nil, 2, 0.85, 1.0, 0.9, 0, 0.3, 0.2, 0, 0, 1)
    kit.tri(t, { 0.002, 0.02, 0.0 }, { 0.05, 0.025, 0.0 }, { 0.002, 0.095, 0.0 }, nil, nil, nil, 2, 0.85, 0.9, 1.0, 0, 0.2, 0.3, 0, 0, -1)
    return t
  elseif kind == "ring" then
    -- flat annulus in the local xz plane, radius 1 (scaled per selection)
    local n = 40
    for k = 0, n - 1 do
      local a0, a1 = k / n * 2 * pi, (k + 1) / n * 2 * pi
      local p0o, p1o = { cos(a0), 0, sin(a0) }, { cos(a1), 0, sin(a1) }
      local p0i, p1i = { cos(a0) * 0.8, 0, sin(a0) * 0.8 }, { cos(a1) * 0.8, 0, sin(a1) * 0.8 }
      kit.tri(t, p0i, p0o, p1o, nil, nil, nil, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0)
      kit.tri(t, p0i, p1o, p1i, nil, nil, nil, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0)
    end
    return t
  elseif kind == "pulse" then
    -- area-of-effect pulse: a translucent disc (sway = opacity) with a bright rim, radius 1
    local n = 40
    for k = 0, n - 1 do
      local a0, a1 = k / n * 2 * pi, (k + 1) / n * 2 * pi
      local c0, s0, c1, s1 = cos(a0), sin(a0), cos(a1), sin(a1)
      kit.tri(t, { 0, 0, 0 }, { c0 * 0.86, 0, s0 * 0.86 }, { c1 * 0.86, 0, s1 * 0.86 }, nil, nil, nil, 1,
        1, 1, 1, 0.0, 0.14, 0.14, 0, 1, 0)
      local p0i, p1i = { c0 * 0.86, 0, s0 * 0.86 }, { c1 * 0.86, 0, s1 * 0.86 }
      local p0o, p1o = { c0, 0.002, s0 }, { c1, 0.002, s1 }
      kit.tri(t, p0i, p0o, p1o, nil, nil, nil, 1, 1, 1, 1, 0.6, 1, 1, 0, 1, 0)
      kit.tri(t, p0i, p1o, p1i, nil, nil, nil, 1, 1, 1, 1, 0.6, 1, 0.6, 0, 1, 0)
    end
    return t
  elseif kind == "flame" then
    kit.cone(t, -0.05, 1.0, 0.32, 5, 1, 0.75, 1.0, 1)
    kit.cone(t, -0.05, 0.62, 0.2, 5, 2, 0.9, 1.0, 1, 0.5)
    return t
  elseif kind == "smoke" then
    -- (round enough not to read as a hexagon when it swells near the camera)
    kit.lump(t, 0.5, 0.42, 0.5, 10, 5, 1, 0.8, 1.0, 0, 0.1)
    return t
  end
  return t
end

-- Cloud clusters (renderer v3): many soft lumps over a flat base, ~1.0 across at scale 1.
-- cu1/cu2: fair-weather cumulus (heaped tops); deck: a wide, low rain deck; tower: a storm
-- tower (stacked heaps under a flat anvil). Shade (baked AO) runs from grey bases to bright tops.
local function flatten(t, ybase)
  local d = t.d
  for k = 0, t.nv - 1 do
    local o = k * 9
    if d[o + 2] < ybase then
      d[o + 2] = ybase
      d[o + 5] = -0.6
      d[o + 8] = 0.55
    end
  end
  return t
end

function M.cloud_template(kind)
  local t = kit.new()
  local seed = kind == "cloud_cu1" and 3 or (kind == "cloud_cu2" and 7 or (kind == "cloud_deck" and 11 or 17))
  kit.seed(seed)
  local h = seed * 7919 + 1
  local function u() h = (h * 48271) % 2147483647; return h / 2147483647 end
  local function lump(x, y, z, r, ry)
    kit.place(t, kit.lump(nil, r, ry or r * 0.85, r, 7, 4, 1, 0.62, 1.0, 0, 0.1), x, y, z, 1)
  end
  if kind == "cloud_cu1" or kind == "cloud_cu2" then
    local long = kind == "cloud_cu2" and 1.5 or 1.0
    lump(0, 0.14, 0, 0.3)
    for k = 1, 9 do
      local a = k / 9 * 6.283 + u() * 0.5
      local d = 0.22 + u() * 0.18
      lump(cos(a) * d * long, 0.04 + u() * 0.08, sin(a) * d, 0.14 + u() * 0.1)
    end
    for k = 1, 4 do
      local a = u() * 6.283
      lump(cos(a) * 0.12 * long, 0.3 + u() * 0.1, sin(a) * 0.1, 0.13 + u() * 0.07)
    end
    flatten(t, -0.02)
  elseif kind == "cloud_deck" then
    for k = 1, 16 do
      local a, d = u() * 6.283, math.sqrt(u())
      lump(cos(a) * d * 0.55, 0.02 + u() * 0.06, sin(a) * d * 0.38, 0.14 + u() * 0.1, 0.09 + u() * 0.05)
    end
    for k = 1, 5 do
      local a, d = u() * 6.283, u() * 0.3
      lump(cos(a) * d, 0.1 + u() * 0.05, sin(a) * d * 0.7, 0.12 + u() * 0.06, 0.1)
    end
    flatten(t, -0.03)
  else
    -- storm tower: a broad dark base, heaped columns, an anvil spreading at the top
    for k = 1, 8 do
      local a, d = u() * 6.283, 0.15 + u() * 0.25
      lump(cos(a) * d, 0.04 + u() * 0.05, sin(a) * d, 0.16 + u() * 0.08, 0.12)
    end
    for lvl = 1, 5 do
      local y = 0.1 + lvl * 0.13
      for k = 1, 3 do
        local a = u() * 6.283
        lump(cos(a) * 0.08, y, sin(a) * 0.08, 0.17 - lvl * 0.012 + u() * 0.04)
      end
    end
    for k = 1, 7 do
      local a, d = u() * 6.283, 0.1 + u() * 0.35
      lump(cos(a) * d * 1.2, 0.86 + u() * 0.05, sin(a) * d, 0.16 + u() * 0.06, 0.06)
    end
    flatten(t, -0.03)
  end
  return t
end

--- Writes a template once in its local frame (pooled movers/clouds). Returns vertex count.
-- c1, c2 (and optional c3) are {r, g, b}; slots >= 4 use the kit's fixed colours.
-- limbs (optional): col has 4 components per vertex: packed rgb, shade*8 + limb code (1/2
-- legs, 3/4 arms; replaces sway), local x and local y above the limb's pivot, both times
-- lscale (the game object's scale); limb vertices get the forward axis (+x) as normal
-- (props_ws.vp). still = true writes code 0 (standing pose).
function M.write_template(t, pos, nrm, col, c1, c2, scale, c3, limbs, lscale, still)
  local d = t.d
  scale = scale or 1
  local p1 = pack_rgb(c1[1], c1[2], c1[3])
  local p2 = c2 and pack_rgb(c2[1], c2[2], c2[3]) or p1
  local p3 = c3 and pack_rgb(c3[1], c3[2], c3[3]) or p1
  local FIXED = M.FIXED
  local stride = limbs and 4 or 2
  for k = 0, t.nv - 1 do
    local o = k * 9
    local p, q = k * 3, k * stride
    pos[p + 1], pos[p + 2], pos[p + 3] = d[o + 1] * scale, d[o + 2] * scale, d[o + 3] * scale
    local sway = d[o + 9]
    local slot = d[o + 7]
    local code = 0
    if limbs then
      if sway > 0.2 then code = floor(sway * 4 + 0.5) end
      sway = 0
    end
    local nx, ny, nz = d[o + 4], d[o + 5], d[o + 6]
    if code > 0 then nx, ny, nz = 1, 0, 0 end
    nrm[p + 1], nrm[p + 2], nrm[p + 3] = nx, ny, nz
    local sh = floor((min(1, max(0.5, d[o + 8])) - 0.5) * 62 + 0.5)
    col[q + 1] = slot == 1 and p1 or (slot == 2 and p2 or (slot == 3 and p3 or (FIXED[slot] or p1)))
    if limbs then
      local pivot = 0
      if code == 1 or code == 2 then pivot = t.hip or 0 elseif code >= 3 then pivot = t.shoulder or 0 end
      local ls = (lscale or 1) * scale
      col[q + 2] = sh * 8 + (still and 0 or code)
      col[q + 3] = d[o + 1] * ls
      col[q + 4] = (d[o + 2] - pivot) * ls
    else
      col[q + 2] = sh * 8 + floor(min(1, max(0, sway)) * 7 + 0.5)
    end
  end
  return t.nv
end

return M
