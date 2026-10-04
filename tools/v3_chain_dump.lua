-- The causal graph a god power leaves behind (S23, forward causality): cast one power in a made state, live
-- on, then list every event and deed whose chain of real links reaches the cast - each with its parent - and
-- the planet's blame tags at the villages that carry it. A design and audit tool for sim/chain.lua.
--   luajit tools/v3_chain_dump.lua --case NAME [--hours H] [--load F]
-- cases: lightning_dry | rain_drought | rain_wet | cull_wolves | cull_deer | strain | stir | cold
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave", case = "lightning_dry" }
local i = 1
while arg[i] do
  local k = arg[i]:match("^%-%-(.+)$")
  if k and arg[i + 1] then i = i + 1; args[k] = tonumber(arg[i]) or arg[i] end
  i = i + 1
end
local world = require("sim.world")
local worlds = require("sim.worlds")
local clock = require("sim.agents.clock")
local f = assert(io.open(args.load, "rb"))
local W = assert(worlds.from_save_string(f:read("*a")))
f:close()
W.record_events = true
world.set_systems(require("sim.agents.worldgen").systems())
local V = W.S.v3
local function S(k) return V.settlements[k] end
local function goto_local(s, d, lh)
  local t = W.hour + d * 24
  local h = clock.local_hour(t, W.grid.lon[s.cell])
  t = t + ((lh - h) % 24)
  world.advance(W, t)
end
local function cast(p)
  world.command(W, "power", p)
  world.advance(W, (V.now or W.hour) + 0.01)
end
local powers = require("sim.powers")
local cfg = require("sim.config")
local max = math.max

local CASES = {
  lightning_dry = function()
    local s = S(1); goto_local(s, 2, 14)
    W.F.soil_water[s.cell] = 0.12
    local b
    for _, id in ipairs(s.blist) do local x = s.buildings[id]; if x.kind == "house" and x.state == "standing" then b = x; break end end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = b.cx, y = b.cy }) end, 48
  end,
  lightning_tree = function()
    local s = S(1); goto_local(s, 2, 14)
    W.F.soil_water[s.cell] = 0.12
    local T = s.trees
    local best
    for k = 1, T.n do if T.st[k] == 1 and (T.size[k] or 0) > 0.85 and math.abs(T.x[k]) + math.abs(T.y[k]) > 0.8 then best = k; break end end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = T.x[best], y = T.y[best] }) end, 72
  end,
  lightning_village = function()
    -- (a tree close to the houses on a dry, hot afternoon)
    local s = S(1); goto_local(s, 2, 15)
    W.F.soil_water[s.cell] = 0.1
    local T = s.trees
    local best, bd = nil, 1e9
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "house" and b.state == "standing" then
        for k = 1, T.n do
          if T.st[k] == 1 and (T.size[k] or 0) > 0.7 then
            local d = (T.x[k] - b.cx) ^ 2 + (T.y[k] - b.cy) ^ 2
            if d > 0.0016 and d < bd then best, bd = k, d end
          end
        end
      end
    end
    return s, function() cast({ power = "lightning", cell = s.cell, sid = s.id, x = T.x[best], y = T.y[best] }) end, 96
  end,
  rain_drought = function()
    local s = S(5); goto_local(s, 2, 10)
    for _, c in ipairs(powers.area(W, s.cell, cfg.POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = 0.1 end end
    return s, function() cast({ power = "rain", cell = s.cell }) end, 96
  end,
  rain_wet = function()
    local s = S(6); goto_local(s, 2, 10)
    for _, c in ipairs(powers.area(W, s.cell, cfg.POWERS.radius)) do if W.geo.ocean[c] == 0 then W.F.soil_water[c] = max(W.F.soil_water[c], 0.5) end end
    return s, function() cast({ power = "rain", cell = s.cell }) end, 120
  end,
  cull_wolves = function()
    local s = S(4); goto_local(s, 2, 10)
    return s, function() cast({ power = "cull", cell = s.cell, species = "wolves" }) end, 24 * 6
  end,
  cull_deer = function()
    local s = S(4); goto_local(s, 2, 10)
    return s, function() cast({ power = "cull", cell = s.cell, species = "deer" }) end, 24 * 6
  end,
  strain = function()
    local s = S(1); goto_local(s, 2, 2)
    return s, function() cast({ power = "strain", cell = s.cell }); cast({ power = "strain", cell = s.cell }) end, 48
  end,
  stir = function()
    local s = S(1); goto_local(s, 2, 10)
    return s, function() cast({ power = "stir", cell = s.cell }); cast({ power = "stir", cell = s.cell }) end, 96
  end,
  cold = function()
    local s = S(1); goto_local(s, 4, 10)
    return s, function() cast({ power = "cold", cell = s.cell }) end, 96
  end,
}

local s, go, hours = CASES[args.case]()
hours = args.hours or hours
local e0 = W.E.next_id
local d0 = #(V.deeds or {})
go()
local roots = {}
for id = e0, W.E.next_id - 1 do
  local r = W.E.rec[id]
  if r and r.actor == "player" and (r.parent or 0) == 0 then roots[id] = true end
end
world.advance(W, (V.now or W.hour) + hours)

-- the root of a deed: follow answers / cause, then the event, then events' roots
local function deed_root(d, guard)
  guard = guard or 0
  if guard > 20 then return 0 end
  local nd = (d.answers and V.deeds[d.answers]) or (d.cause and V.deeds[d.cause])
  if nd and nd ~= d then return deed_root(nd, guard + 1) end
  local ev = (d.event and d.event ~= 0) and d.event or d.ev
  if ev and ev ~= 0 then return W.E.root[ev] or 0 end
  return 0
end
print(string.format("== %s (+%d h): roots %s", args.case, hours, table.concat((function() local t = {} for k in pairs(roots) do t[#t + 1] = k end return t end)(), ",")))
print("-- events descending from the cast")
for id = e0, W.E.next_id - 1 do
  local root = W.E.root[id]
  if root and roots[root] then
    local r = W.E.rec[id]
    print(string.format("  e%-5d %-18s parent e%-5d cell %d mag %.2f", id, r and r.kind or "?", W.E.parent[id] or 0, r and r.cell or 0, r and r.mag or 0))
  end
end
print("-- deeds (new) and their links; * = reaches the cast")
local D = V.deeds or {}
local kinds = {}
for k = d0 + 1, #D do
  local d = D[k]
  local rt = deed_root(d)
  local mark = roots[rt] and "*" or " "
  kinds[d.kind .. mark] = (kinds[d.kind .. mark] or 0) + 1
  if roots[rt] or k - d0 <= 400 then
    if roots[rt] then
      print(string.format(" %s d%-5d t%.1f %-8s at %-2s what %-10s by %-4s to %-4s to_hh %-4s answers %-5s cause %-5s ev %-5s", mark, d.id, d.t, d.kind,
        tostring(d.at), tostring(d.what), tostring(d.by), tostring(d.to), tostring(d.to_hh), tostring(d.answers), tostring(d.cause), tostring(d.event or d.ev)))
    end
  end
end
local ks = {}
for k, n in pairs(kinds) do ks[#ks + 1] = k .. " " .. n end
table.sort(ks)
print("-- deed kinds (* reaching the cast): " .. table.concat(ks, ", "))
print("-- blame tags at the villages carrying the cast (or a descendant)")
for _, q in ipairs(V.settlements) do
  local c = q.cell
  local tags = {}
  for _, fld in ipairs({ "soil_water", "flow", "precip", "temp", "heat_anom", "fire", "grass", "forest" }) do
    local C = W.C[fld]
    local tg = C and C[c] or 0
    if tg ~= 0 and roots[W.E.root[tg] or 0] then tags[#tags + 1] = fld .. "=e" .. tg end
  end
  if #tags > 0 then print(string.format("  %-18s %s (soil %.2f)", q.name, table.concat(tags, " "), W.F.soil_water[c])) end
end
-- households deciding to leave or found with a cause in the tree
for _, hh in pairs(V.households) do
  for _, key in ipairs({ "migrate", "found" }) do
    local m = hh[key]
    if type(m) == "table" and m.cause then
      local d = D[m.cause]
      if d and roots[deed_root(d)] then print(string.format("  household %d %s cause d%d (%s)", hh.id, key, d.id, d.kind)) end
    end
  end
end
