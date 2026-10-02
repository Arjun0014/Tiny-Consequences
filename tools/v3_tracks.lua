-- The tracks of a herd, band or pack seen from above (S15 animal audit): every animal's path over
-- a span of game hours drawn in its own colour (darker early, brighter late), its position at the
-- end as a dot, the herder's track in black, trees as faint green, water blue, buildings grey.
-- What the sim did, not what the view draws: a blob moving as one shows as parallel tracks.
--   luajit tools/v3_tracks.lua --sett K --from H --hours 2 [--what flock|cattle|swine|band|pack|dogs]
--          [--gid G] [--size 600] [--every 0.01] [--out out/tracks.png]
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave", sett = 4, from = 40, hours = 2, what = "flock", size = 600, every = 0.01, out = "out/tracks.png" }
local i = 1
while arg[i] do
  local a = arg[i]
  local v = arg[i + 1]
  if a == "--load" then args.load = v
  elseif a == "--sett" then args.sett = tonumber(v)
  elseif a == "--from" then args.from = tonumber(v)
  elseif a == "--hours" then args.hours = tonumber(v)
  elseif a == "--what" then args.what = v
  elseif a == "--gid" then args.gid = tonumber(v)
  elseif a == "--size" then args.size = tonumber(v)
  elseif a == "--every" then args.every = tonumber(v)
  elseif a == "--out" then args.out = v
  else i = i - 1 end
  i = i + 2
end
local world = require("sim.world")
local stock = require("sim.agents.stock")
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")
local act = require("sim.agents.act")
local mapm = require("sim.agents.map")
local png = require("tools.png")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local V = W.S.v3
world.advance(W, args.from)
local s = V.settlements[args.sett]
local tracks, herder = {}, {}
local function members()
  local out = {}
  if args.what == "flock" or args.what == "cattle" or args.what == "swine" then
    local H = s.herds and s.herds[args.what]
    for _, a in ipairs(H and H.animals or {}) do if a.alive then out[#out + 1] = { "a" .. a.k, a, stock.pos } end end
    local p = H and stock.herder(W, s, H)
    if p then out.herder = p end
  elseif args.what == "dogs" then
    for _, d in ipairs(s.dogs or {}) do if d.alive then out[#out + 1] = { "d" .. d.k, d, dogs.pos } end end
  else
    for _, w in ipairs(s.wildlife and s.wildlife.animals or {}) do
      if w.alive and ((args.what == "pack") == w.pred) and (not args.gid or w.gid == args.gid) then out[#out + 1] = { "w" .. w.k, w, wild.pos } end
    end
  end
  return out
end
local t1 = args.from + args.hours
local n = 0
for t = args.from, t1, args.every do
  world.advance(W, t)
  n = n + 1
  local ms = members()
  for _, m in ipairs(ms) do
    local x, y = m[3](m[2], t)
    local tr = tracks[m[1]]
    if not tr then tr = { k = #tracks + 1 }; tracks[m[1]] = tr; tracks[#tracks + 1] = m[1] end
    tr[#tr + 1] = { x, y, t }
  end
  if ms.herder then
    local x, y = act.pos(ms.herder, t)
    herder[#herder + 1] = { x, y, t }
  end
end
-- the frame: the tracks' extent plus a margin
local x0, y0, x1, y1 = 1e9, 1e9, -1e9, -1e9
local function grow(p) x0, y0, x1, y1 = math.min(x0, p[1]), math.min(y0, p[2]), math.max(x1, p[1]), math.max(y1, p[2]) end
for _, k in ipairs(tracks) do for _, p in ipairs(tracks[k]) do grow(p) end end
for _, p in ipairs(herder) do grow(p) end
local span = math.max(x1 - x0, y1 - y0, 0.2) * 1.15
local cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
local S = args.size
local img = {}
local function idx(px, py) return py * S + px end
local function world_of(px, py) return cx + (px / S - 0.5) * span, cy + (0.5 - py / S) * span end
for py = 0, S - 1 do
  for px = 0, S - 1 do
    local x, y = world_of(px, py)
    local ti = mapm.tile_of(s, x, y)
    local r, g, b = 236, 232, 220
    if ti > 0 then
      if s.land[ti] ~= 1 or (s.riv[ti] or 0) > 0 then r, g, b = 150, 185, 220
      elseif s.occ[ti] > 0 then r, g, b = 170, 165, 160
      elseif s.plot[ti] > 0 then r, g, b = 222, 210, 170
      elseif s.tree[ti] > 0 and s.trees.st[s.tree[ti]] == 1 then r, g, b = 190, 215, 180 end
    end
    img[idx(px, py)] = { r, g, b }
  end
end
local function hsv(h, v)
  local i6 = math.floor(h * 6) % 6
  local f6 = h * 6 - math.floor(h * 6)
  local p, q, tt = 0, v * (1 - f6), v * f6
  local r, g, b
  if i6 == 0 then r, g, b = v, tt, p elseif i6 == 1 then r, g, b = q, v, p elseif i6 == 2 then r, g, b = p, v, tt
  elseif i6 == 3 then r, g, b = p, q, v elseif i6 == 4 then r, g, b = tt, p, v else r, g, b = v, p, q end
  return math.floor(r * 255), math.floor(g * 255), math.floor(b * 255)
end
local function plot(x, y, c, rad)
  local px = math.floor((x - cx) / span * S + S / 2)
  local py = math.floor((cy - y) / span * S + S / 2)
  rad = rad or 0
  for dy = -rad, rad do
    for dx = -rad, rad do
      local qx, qy = px + dx, py + dy
      if qx >= 0 and qy >= 0 and qx < S and qy < S then img[idx(qx, qy)] = c end
    end
  end
end
local function line(a, b, c)
  local steps = math.max(1, math.floor(math.max(math.abs(a[1] - b[1]), math.abs(a[2] - b[2])) / span * S * 1.5))
  for q = 0, steps do
    local f2 = q / steps
    plot(a[1] + (b[1] - a[1]) * f2, a[2] + (b[2] - a[2]) * f2, c, 0)
  end
end
for j, k in ipairs(tracks) do
  local tr = tracks[k]
  local h = (j * 0.618034) % 1
  for q = 2, #tr do
    local v = 0.45 + 0.5 * (tr[q][3] - args.from) / args.hours
    local r, g, b = hsv(h, v)
    line(tr[q - 1], tr[q], { r, g, b })
  end
  local r, g, b = hsv(h, 0.95)
  plot(tr[#tr][1], tr[#tr][2], { r, g, b }, 2)
end
for q = 2, #herder do line(herder[q - 1], herder[q], { 20, 20, 20 }) end
if #herder > 0 then plot(herder[#herder][1], herder[#herder][2], { 0, 0, 0 }, 3) end
png.write(args.out, S, S, function(x, y) local c = img[idx(x, y)]; return c[1], c[2], c[3] end)
print(string.format("%s: %d animals over %.1f h (frame %.2f units) -> %s", args.what, #tracks, args.hours, span, args.out))
