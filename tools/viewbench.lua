-- Headless profile of view-side Lua work (clouds, prop planning) on the default world, with the
-- Defold APIs stubbed (tools/viewstub.lua). Usage (repo root):
--   luajit tools/viewbench.lua [--interp] [--what clouds|props|all] [--hour H]
--     --interp   switch the JIT off (closer to the HTML5 runtime, an interpreter)
-- Reports ms per unit of work and counts (puffs per sector, props per chunk).
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { what = "all" }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--interp" then args.interp = true
  elseif a == "--what" then i = i + 1; args.what = arg[i]
  elseif a == "--hour" then i = i + 1; args.hour = tonumber(arg[i]) end
  i = i + 1
end
if rawget(_G, "jit") then
  if args.interp then jit.off() else jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
end
local stub = require("tools.viewstub")
local worlds = require("sim.worlds")
local f = assert(io.open("assets/worlds/default.tcsave", "rb"))
local str = f:read("*a")
f:close()
local W = assert(worlds.from_save_string(str))
local world = require("sim.world")
if args.hour then for _ = 1, args.hour do world.step(W) end end
local state = require("game.view.state")
state.hour_f = W.hour
package.loaded["game.platform.speed"] = { hours_per_second = function() return 1 end }
package.loaded["game.view.camera"] = { zoom = 0 }

local function now() return os.clock() * 1000 end

if args.what == "clouds" or args.what == "all" then
  local clouds = require("game.view.clouds")
  local t0 = now()
  clouds.init(W, "f", "r")
  local t1 = now()
  local tot = 0
  for _, sec in ipairs(clouds.sectors) do tot = tot + sec.objs[sec.cur].n end
  print(string.format("clouds: init (all %d sectors) %.1f ms, %d puffs (%.0f per sector), cover %.3f",
    #clouds.sectors, t1 - t0, tot, tot / #clouds.sectors, clouds.stats.cover))
  -- one sector rebuild, timed by phase
  local r0 = clouds.stats.rebuilt
  local t2 = now()
  for _ = 1, 3 do
    for _, sec in ipairs(clouds.sectors) do sec.key = -99 end
    clouds.update(W, 0, true, true)
  end
  local n = clouds.stats.rebuilt - r0
  local ph = clouds.stats.ph
  print(string.format("clouds: %.2f ms per sector rebuild (%d rebuilds); phases total weather %.0f, plan %.0f, write %.0f ms",
    (now() - t2) / math.max(1, n), n, ph[1], ph[2], ph[3]))
end

if args.what == "props" or args.what == "all" then
  local planet = require("game.view.planet")
  local propgen = require("game.view.propgen")
  local t0 = now()
  local PL = planet.build(W, {})
  print(string.format("props: planet.build %.0f ms (%d faces, %d chunks)", now() - t0, PL.nf, #PL.chunks))
  local ctx = { W = W, PL = PL, seed = W.seed, lod = 2, surface = function(x, y, z) return planet.surface_radius(PL, x, y, z) end }
  for lod = 2, 1, -1 do
    ctx.lod = lod
    local inst, verts, tp, tw = 0, 0, 0, 0
    for _, ch in ipairs(PL.chunks) do
      local a = now()
      local ni, nv = propgen.plan_chunk(ctx, ch)
      local b = now()
      if ni > 0 then
        local buf = buffer.create(nv, propgen.chunk_decl())
        propgen.write(buf, { inst = buf.streams.inst, inst2 = buf.streams.inst2, inst3 = buf.streams.inst3 }, 0, 1, ni)
      end
      tw = tw + now() - b
      tp = tp + b - a
      inst, verts = inst + ni, verts + nv
    end
    print(string.format("props lod %d: %d instances, %d verts (%.0f per chunk); plan %.0f ms, write %.0f ms (%.2f us per vertex)",
      lod, inst, verts, verts / #PL.chunks, tp, tw, tw * 1000 / math.max(1, verts)))
  end
end
