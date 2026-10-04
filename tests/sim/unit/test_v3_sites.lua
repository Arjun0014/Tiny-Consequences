-- Sites never close a way (S23, D-184): a tanning pit laid snug in front of a neighbour's door sealed that
-- house off from its village in year 8 and the household starved inside it. Every planner keeps a yard round
-- what it lays; with a walkable ring round a footprint, no way that crossed it is lost.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")
local settle = require("sim.agents.settle")
local mapm = require("sim.agents.map")

local M = {}

local function load()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local W = assert(worlds.from_save_string(f:read("*a")))
  f:close()
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

-- Pits packed round the doors of every village, one after another, wherever the pit rule accepts one
-- within four tiles of a door (each closed in the walking field as a pit closes it): after every one,
-- every door of the village can still be walked to from its square. (In year 8 a house's door opened into
-- the two-tile lane the house yards leave between houses; one pit was laid in it beside the door, a later
-- one on the door's other side, and the door was sealed. Packing reproduces that with the old rule.)
function M.pits_packed_round_doors_never_seal()
  local W = load()
  local placed = 0
  for _, s in ipairs(W.S.v3.settlements) do
    T.eq(#mapm.cut_doors(s), 0, s.name .. ": every door reachable before")
    local N, cost, occ = s.N, s.cost, s.occ
    local doors = {}
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.dx and not b.natural then doors[#doors + 1] = b end
    end
    for _, b in ipairs(doors) do
      local dix, diy = mapm.tile_ixy(s, mapm.tile_of(s, b.dx, b.dy))
      for jy = diy - 4, diy + 3 do
        for jx = dix - 4, dix + 3 do
          if jx > 3 and jy > 3 and jx < N - 4 and jy < N - 4 and settle.pit_fits(s, jx, jy) then
            for yy = jy, jy + 1 do
              for xx = jx, jx + 1 do
                local i = (yy - 1) * N + xx
                occ[i], cost[i] = -1 - placed, -1   -- (a footprint: closed, and taken for the next pit's rule)
              end
            end
            placed = placed + 1
            local cut = mapm.cut_doors(s)
            if #cut > 0 then
              T.ok(false, string.format("%s: pit %d at %d,%d cut off the door of %s #%d", s.name, placed, jx, jy, cut[1].kind, cut[1].id))
              return
            end
          end
        end
      end
    end
  end
  T.ok(placed >= 100, "pits packed: " .. placed)
  return string.format("%d pits packed round the doors: none closed a way", placed)
end

-- The planner itself, every household of every village with hides to tan at once (each pit finished at
-- once so the next is laid beside the ones before): every door can still be walked to afterwards.
function M.pits_never_seal_a_door()
  local W = load()
  local V = W.S.v3
  local laid = 0
  for _, s in ipairs(V.settlements) do
    for _ = 1, 3 do
      for _, hid in ipairs(s.households) do
        local hh = V.households[hid]
        local home = hh and s.buildings[hh.home or 0]
        if home then home.stock.hides = 4 end
        local n0 = #s.blist
        settle.plan_tanpits(W, s, 0)
        if #s.blist > n0 then
          local b = s.buildings[s.blist[#s.blist]]
          if b.kind == "tanpit" then
            settle.complete_building(W, s, b)
            b.hh = 0   -- (another pit for the same household next round: a second one beside the first)
            laid = laid + 1
          end
        end
      end
    end
    local cut = mapm.cut_doors(s)
    local names = {}
    for _, b in ipairs(cut) do names[#names + 1] = b.kind .. "#" .. b.id end
    T.eq(#cut, 0, s.name .. ": doors cut off by the pits: " .. table.concat(names, ", "))
  end
  T.ok(laid >= 30, "pits laid: " .. laid)
  return string.format("%d pits laid, every door still reachable", laid)
end

return M
