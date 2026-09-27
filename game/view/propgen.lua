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

local M = {}

local sqrt, floor, min, max, abs, cos, sin, pi, atan2 = math.sqrt, math.floor, math.min, math.max,
  math.abs, math.cos, math.sin, math.pi, math.atan2

M.SCALE = 1.25         -- global prop scale, near LOD
M.SCALE_FAR = 1.7      -- far LOD props are bigger and fewer so forests still read from orbit
M.BUILDING = 1.3       -- buildings are drawn a little large (miniature-model proportions)
M.SLOTS = 7            -- prop slots per render face, near
M.SLOTS_FAR = 3

-- Kinds (index into NAMES; templates by name)
local NAMES = { "oak", "birch", "spruce", "snowpine", "pine", "palm", "jungle", "acacia", "snag", "stump",
  "bush", "tuft", "flower", "reed", "rock", "cactus", "cottage", "longhouse", "townhouse", "hut",
  "granary", "hall", "well", "fence", "haystack", "woodpile", "ruin", "pier" }
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
M.PAL = PAL
local BARK = {
  [K.oak] = hex("#7A5236"), [K.birch] = hex("#E9E4D8"), [K.spruce] = hex("#6E4A32"),
  [K.snowpine] = hex("#6E4A32"), [K.pine] = hex("#9A6244"), [K.palm] = hex("#A57C52"),
  [K.jungle] = hex("#6E4C34"), [K.acacia] = hex("#7C5A3E"), [K.flower] = hex("#5E9E4A"),
}
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
  c1 = {}, c2 = {}, c3 = {}, cell = {},
}
M.list = L

local function add(kind, x, y, z, ux, uy, uz, yaw, sc, sh, c1, c2, c3, cell)
  local t = TPL[L.lod][kind]
  if not t then return end
  local i = L.n + 1
  L.n = i
  L.kind[i], L.x[i], L.y[i], L.z[i] = kind, x, y, z
  L.ux[i], L.uy[i], L.uz[i], L.yaw[i], L.sc[i], L.sh[i] = ux, uy, uz, yaw, sc, sh
  L.c1[i], L.c2[i], L.c3[i] = c1, c2, c3
  L.cell[i] = cell or 0
  L.verts = L.verts + t.nv
end
M.add = add

-- Park-Miller step (exact in doubles)
local function pm(h) return (h * 48271) % 2147483647 end

local P_SNOW = pack_rgb(SNOW[1], SNOW[2], SNOW[3])
local P_GREEN = pack_rgb(0.37, 0.62, 0.29)

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

-- chimney tops in the house templates' local frame (modelkit)
local CHIMNEY = { cottage = { 0.022, 0.092, -0.008 }, townhouse = { -0.02, 0.122, 0.006 } }
M.chimneys = {}         -- village cell -> list of { x, y, z } chimney tops (world), for fx smoke

-- World position of local point (lx, ly, lz) of an instance (same frame as M.write).
local function to_world(x, y, z, ux, uy, uz, yaw, s, lx, ly, lz)
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

--- Lays out village v ({ cell, houses, ruin, culture, id, fishing, harvest }) into the
-- instance list. ctx.surface(x, y, z) -> radius, height (km; <= 0 = water).
function M.plan_village(ctx, v)
  local W = ctx.W
  local g = W.grid
  local c = v.cell
  local cx, cy, cz = g.X[c], g.Y[c], g.Z[c]
  local ex, ey, ez, nx, ny, nz = frame(cx, cy, cz)
  local S = ((ctx.lod == 2) and M.SCALE_FAR or M.SCALE) * M.BUILDING
  local style = STYLES[((v.culture or 1) - 1) % #STYLES + 1]
  local roofs, walls = ROOFS[style.roof], WALLS[style.wall]
  local h = (c * 7919 + (ctx.seed or 1) * 104729 + 17) % 2147483647
  if h == 0 then h = 1 end
  local function u() h = pm(h); return h / 2147483647 end
  local placed = {}
  local chim = {}
  M.chimneys[c] = chim
  local function put(kind, lx, lz, yaw, sc, cc1, cc2, clear)
    -- local (east, north) offset in world units -> surface point; skip water and crowding
    local px, py, pz = cx * 10 + ex * lx + nx * lz, cy * 10 + ey * lx + ny * lz, cz * 10 + ez * lx + nz * lz
    local l = sqrt(px * px + py * py + pz * pz)
    local ux, uy, uz = px / l, py / l, pz / l
    local r, hk = ctx.surface(ux, uy, uz)
    if hk <= 0.02 then return false end
    clear = clear or 0.05 * S
    for i = 1, #placed do
      local q = placed[i]
      local dx, dz = q[1] - lx, q[2] - lz
      if dx * dx + dz * dz < (clear + q[3]) * (clear + q[3]) then return false end
    end
    placed[#placed + 1] = { lx, lz, clear }
    add(kind, ux * r, uy * r, uz * r, ux, uy, uz, yaw, sc * S, 1, cc1, cc2, 0, c)
    local ch = CHIMNEY[NAMES[kind]]
    if ch then
      local wx, wy, wz = to_world(ux * r, uy * r, uz * r, ux, uy, uz, yaw, sc * S, ch[1], ch[2], ch[3])
      chim[#chim + 1] = { wx, wy, wz }
    end
    return true
  end
  local function face_centre(lx, lz) return atan2(-lx, -lz) end
  local houses = v.houses or 6
  if v.ruin then
    local n = max(3, min(10, houses))
    for i = 1, n do
      local a = u() * 2 * pi
      local rr = (0.04 + u() * 0.18) * S
      put(K.ruin, cos(a) * rr, sin(a) * rr, u() * 2 * pi, 0.9 + u() * 0.3, pack_rgb(0.55, 0.52, 0.48), 0)
    end
    return
  end
  -- centre: a hall for big villages, else a well in a little square
  if houses >= 12 then
    put(K.hall, 0, 0, u() * 2 * pi, 1.0, pack_rgb(unpack(roofs[1])), pack_rgb(unpack(walls[1])), 0.09 * S)
  else
    put(K.well, 0, 0, u() * 2 * pi, 1.1, pack_rgb(unpack(roofs[1])), 0, 0.05 * S)
  end
  -- houses on rings around the square, doors facing the centre
  local left = houses
  local ring = 0
  while left > 0 and ring < 6 do
    ring = ring + 1
    local rr = (0.075 + 0.068 * ring) * S
    local cap = floor(2 * pi * rr / (0.092 * S))
    local nk = min(left, cap)
    local a0 = u() * 2 * pi
    for k = 1, nk do
      local a = a0 + (k - 1 + (u() - 0.5) * 0.35) / cap * 2 * pi
      local r2 = rr + (u() - 0.5) * 0.03 * S
      local lx, lz = cos(a) * r2, sin(a) * r2
      local kind = K[style.houses[floor(u() * #style.houses) + 1]]
      if ring == 1 and u() < 0.25 then kind = K.townhouse end
      local roof = roofs[floor(u() * #roofs) + 1]
      local wall = walls[floor(u() * #walls) + 1]
      local j = 0.92 + u() * 0.14
      local yaw = face_centre(lx, lz) + (u() - 0.5) * 0.3
      if put(kind, lx, lz, yaw, 0.95 + u() * 0.15, pack_rgb(roof[1] * j, roof[2] * j, roof[3] * j),
          pack_rgb(wall[1], wall[2], wall[3]), 0.045 * S) then
        left = left - 1
        -- yard details: a fence, a woodpile or a haystack after the harvest
        local w = u()
        if w < 0.3 then
          local fa = a + 0.5 / cap * 2 * pi
          put(K.fence, cos(fa) * (r2 + 0.03 * S), sin(fa) * (r2 + 0.03 * S), fa + pi / 2, 0.9, 0, 0, 0.012 * S)
        elseif w < 0.45 then
          put(K.woodpile, cos(a) * (r2 + 0.045 * S), sin(a) * (r2 + 0.045 * S), a, 1.0, 0, 0, 0.015 * S)
        elseif w < 0.6 and v.harvest then
          local hs = PAL[K.haystack][1]
          put(K.haystack, cos(a) * (r2 + 0.06 * S), sin(a) * (r2 + 0.06 * S), u() * 6, 1.0,
            pack_rgb(hs[1], hs[2], hs[3]), 0, 0.02 * S)
        end
      else
        left = left - 1   -- no room here (water or crowding): the village is smaller on this side
      end
    end
  end
  if houses >= 7 then
    local a = u() * 2 * pi
    local rr = (0.085 + 0.075 * (ring + 1)) * S
    put(K.granary, cos(a) * rr, sin(a) * rr, 0, 1.0, pack_rgb(unpack(roofs[1])), 0, 0.04 * S)
  end
  -- fishing villages: a pier from the nearest shore
  if v.fishing and ctx.shore then
    local sx, sy, sz, dir_x, dir_y, dir_z = ctx.shore(cx, cy, cz)
    if sx then
      local r = ctx.surface(sx, sy, sz)
      -- yaw so that local +z points out to sea
      local sex, sey, sez, snx, sny, snz = frame(sx, sy, sz)
      local de = dir_x * sex + dir_y * sey + dir_z * sez
      local dn = dir_x * snx + dir_y * sny + dir_z * snz
      local yaw = atan2(de, dn)
      add(K.pier, sx * r, sy * r, sz * r, sx, sy, sz, yaw, S, 1, 0, 0, 0, c)
    end
  end
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
  local ocean, cdist = W.geo.ocean, W.geo.coast_dist
  local T = W.temp_static
  local gtemp = W.G.g_temp or 0
  local ruins = W.S and W.S.ruins
  local villages = ctx.villages
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
              local rv = M.SCALE * M.BUILDING * (0.16 + 0.07 * sqrt((vv.houses or 6) / 3))
              if dw < rv then in_village = true end
            end
          end
        end
        if lu < 0 and dcell < 0.3 then in_village = true end
        local tree_p = min(1, fo * 1.35) ^ 0.85
        local rocky = hmax > 2.2 or (hmax - hmin) > 0.5
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
          if in_village then
            -- the settlement layout owns the village square
          elseif is_ruin and dcell < 0.4 then
            if u3 < tree_p * 0.5 + 0.1 then kind = K.bush end
          elseif lu > 0 then
            -- farm: the plots are drawn by the terrain shader; a few hedge trees and bushes
            if j > 2 and u3 < 0.05 then kind = K.oak; scale = 0.8 + u5 * 0.4
            elseif j > 3 and u3 < 0.1 then kind = K.bush end
          elseif burnt then
            if u3 < tree_p * 0.6 + 0.15 then kind = (u4 < 0.55) and K.snag or K.stump; scale = 0.8 + u5 * 0.5 end
          elseif bi == 3 then
            if u3 < 0.03 then kind = K.rock end
          elseif u3 < tree_p then
            kind = tree_kind(tmean, ic, bi, soil[c], cdist[c] <= 1, u4)
            scale = 0.72 + u5 * 0.55
            hs = 0.88 + u2 * 0.3
          elseif rocky and u3 < tree_p + 0.14 then
            kind = K.rock; scale = 0.6 + u5 * 1.0
          elseif bi == 9 then
            if u3 < tree_p + 0.05 then kind = K.rock; scale = 0.6 + u5 * 0.8
            elseif u3 < tree_p + 0.09 and tmean > 18 then kind = K.cactus; scale = 0.8 + u5 * 0.5 end
          elseif bi == 8 then
            if u3 < tree_p + 0.35 then kind = K.reed; scale = 0.8 + u5 * 0.5 end
          elseif bi == 14 or bi == 10 then
            if u3 < tree_p + 0.08 then kind = K.rock; scale = 0.6 + u5 * 0.7
            elseif u3 < tree_p + 0.25 then kind = K.tuft end
          else
            local gp = gr * 0.35
            if u3 < tree_p + gp * 0.3 then kind = K.bush; scale = 0.7 + u5 * 0.6
            elseif u3 < tree_p + gp * 0.8 then kind = K.tuft; scale = 0.8 + u5 * 0.6
            elseif u3 < tree_p + gp * 1.05 and tmean > 4 then kind = K.flower; scale = 0.9 + u5 * 0.4
            elseif u3 < tree_p + gp * 1.05 + 0.012 then kind = K.rock; scale = 0.4 + u5 * 0.5
            elseif cdist[c] <= 1 and hmin < 0.12 and u3 < tree_p + gp * 1.05 + 0.1 then kind = K.reed end
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
            local col = pal and pal[floor(u4 * 9973) % #pal + 1] or { 0.6, 0.6, 0.6 }
            local jit = 0.88 + u5 * 0.24
            local c1 = pack_rgb(col[1] * jit, col[2] * jit, col[3] * jit)
            local bark = BARK[kind]
            local c2 = bark and pack_rgb(bark[1], bark[2], bark[3]) or c1
            add(kind, x, y, z, x / l, y / l, z / l, yaw, scale * S, hs, c1, c2, P_SNOW, c)
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

--- Writes instances [i0..i1] of M.list into streams starting at vertex `vbase` (0-based).
-- pos(3) / nrm(3) / col(4) are buffer streams (or Lua arrays in tests). Returns the next
-- vertex index.
function M.write(pos, nrm, col, vbase, i0, i1)
  local kind, X, Y, Z = L.kind, L.x, L.y, L.z
  local UX, UY, UZ, YAW, SC, SH = L.ux, L.uy, L.uz, L.yaw, L.sc, L.sh
  local C1, C2, C3, CELL = L.c1, L.c2, L.c3, L.cell
  local FIXED = M.FIXED
  local tpls = TPL[L.lod]
  local v = vbase
  for i = i0, i1 do
    local kd = kind[i]
    local t = tpls[kd]
    local d, nv, ss = t.d, t.nv, t.ss
    local cl = CLASS[kd] or { 0, 0, 0 }
    local cl1, cl2, cl3 = cl[1], cl[2], cl[3]
    local ux, uy, uz = UX[i], UY[i], UZ[i]
    -- tangent frame: T = normalize(Yaxis x U) (east), B = T x U
    local tx, tz = uz, -ux
    local tl = sqrt(tx * tx + tz * tz)
    if tl < 1e-4 then tx, tz, tl = 1, 0, 1 end
    tx, tz = tx / tl, tz / tl
    local bx, by, bz = -tz * uy, tz * ux - tx * uz, tx * uy
    local cyw, syw = cos(YAW[i]), sin(YAW[i])
    local ex, ey, ez = tx * cyw + bx * syw, by * syw, tz * cyw + bz * syw
    local fx, fy, fz = bx * cyw - tx * syw, by * cyw, bz * cyw - tz * syw
    local s, hs = SC[i], SC[i] * SH[i]
    local ox, oy, oz = X[i], Y[i], Z[i]
    local c1, c2, c3 = C1[i], C2[i], C3[i]
    local cell = CELL[i]
    local p, q = v * 3, v * 4
    for k = 0, nv - 1 do
      local o = k * 9
      local lx, ly, lz = d[o + 1] * s, d[o + 2] * hs, d[o + 3] * s
      pos[p + 1] = ox + ex * lx + ux * ly + fx * lz
      pos[p + 2] = oy + ey * lx + uy * ly + fy * lz
      pos[p + 3] = oz + ez * lx + uz * ly + fz * lz
      local nlx, nly, nlz = d[o + 4], d[o + 5], d[o + 6]
      nrm[p + 1] = ex * nlx + ux * nly + fx * nlz
      nrm[p + 2] = ey * nlx + uy * nly + fy * nlz
      nrm[p + 3] = ez * nlx + uz * nly + fz * nlz
      local slot = d[o + 7]
      local cc, mc
      if slot == 1 then cc, mc = c1, cl1
      elseif slot == 2 then cc, mc = c2, cl2
      elseif slot == 3 then cc, mc = c3, cl3
      else cc, mc = FIXED[slot] or c1, (slot == 6) and 6 or 0 end
      if cc == 0 then cc = c1 end
      col[q + 1] = cc
      col[q + 2] = ss[k + 1]
      col[q + 3] = cell
      col[q + 4] = mc
      p, q = p + 3, q + 4
    end
    v = v + nv
  end
  return v
end

--- Builds a standalone mover template. kind: deer | wolf | person[_straw|_cap] | cloud |
-- tanker | boat | ring | flame | smoke
function M.mover_template(kind)
  kit.build()
  if kind == "deer" or kind == "wolf" then return kit.quadruped(kind) end
  if kind == "person" then return kit.person(nil) end
  if kind == "person_straw" then return kit.person("straw") end
  if kind == "person_cap" then return kit.person("cap") end
  local t = kit.new()
  if kind == "cloud" then
    -- a cluster of soft puffs, ~1.0 across (scaled per cloud)
    kit.seed(3)
    kit.place(t, kit.lump(nil, 0.36, 0.26, 0.32, 10, 5, 1, 0.8, 1.0, 0, 0.06), 0, 0, 0, 1)
    for _, q in ipairs({ { 0.3, -0.04, 0.05, 0.25 }, { -0.3, -0.05, -0.04, 0.23 }, { 0.05, 0.13, -0.02, 0.23 },
        { -0.05, -0.06, 0.22, 0.2 }, { 0.12, 0.02, -0.22, 0.2 }, { -0.18, 0.08, 0.12, 0.18 } }) do
      kit.place(t, kit.lump(nil, q[4], q[4] * 0.82, q[4], 9, 4, 1, 0.8, 1.0, 0, 0.08), q[1], q[2], q[3], 1)
    end
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
  elseif kind == "flame" then
    kit.cone(t, -0.05, 1.0, 0.32, 5, 1, 0.75, 1.0, 1)
    kit.cone(t, -0.05, 0.62, 0.2, 5, 2, 0.9, 1.0, 1, 0.5)
    return t
  elseif kind == "smoke" then
    kit.lump(t, 0.5, 0.42, 0.5, 6, 3, 1, 0.8, 1.0, 0, 0.1)
    return t
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
