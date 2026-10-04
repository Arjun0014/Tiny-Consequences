-- Chain reaction (S23, D-182; docs/v3/77_CHAIN_REACTION.md): what a god's cast caused, read from real causal
-- links by sim/chain.lua. The same power in different states must give different chains; every node rests
-- on a record or on live state carrying the cast's mark; the observer never changes the world.
local T = require("tests.sim.lib")
local worlds = require("sim.worlds")
local world = require("sim.world")
local clock = require("sim.agents.clock")
local chain = require("sim.chain")

local M = {}

local function load()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local W = assert(worlds.from_save_string(f:read("*a")))
  f:close()
  W.record_events = true
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

local function goto_local(W, s, d, lh)
  local t = W.hour + d * 24
  local h = clock.local_hour(t, W.grid.lon[s.cell])
  t = t + ((lh - h) % 24)
  world.advance(W, t)
end

local function cast(W, p)
  world.command(W, "power", p)
  world.advance(W, (W.S.v3.now or W.hour) + 0.01)
end

-- live `hours`, reading the chain every `every` hours (as the HUD does many times an hour)
local function live(W, hours, every)
  local V = W.S.v3
  local t0 = V.now or W.hour
  for h = every, hours, every do
    world.advance(W, t0 + h)
    chain.update(W, V.now or W.hour)
  end
end

-- every node rests on something real: a deed or event that exists, or a reading of live state
local STATE = { soil = true, river = true, air = true, crops = true, packs = true, herds = true, lodging = true, hunger = true,
  leaving = true, founding = true, fire = true }
local function rests_on_the_world(W, I)
  local bad = {}
  for _, row in ipairs(chain.rows(W, I, 500)) do
    local nd = row.node
    local ok = (nd.deed and W.S.v3.deeds[nd.deed] ~= nil) or (nd.ev and W.E.parent[nd.ev] ~= nil) or STATE[nd.kind]
    if not ok or not nd.title or nd.title == "" then bad[#bad + 1] = tostring(nd.kind) end
  end
  return bad
end

local function chain_kinds(W, I)
  local k = {}
  for _, row in ipairs(chain.rows(W, I, 500)) do k[row.node.kind] = (k[row.node.kind] or 0) + 1 end
  return k
end

local function rain_on(W, sid, soil, day)
  local cfg = require("sim.config")
  local powers = require("sim.powers")
  local s = W.S.v3.settlements[sid]
  goto_local(W, s, day, 10)
  for _, c in ipairs(powers.area(W, s.cell, cfg.POWERS.radius)) do
    if W.geo.ocean[c] == 0 then W.F.soil_water[c] = soil == "dry" and 0.1 or math.max(W.F.soil_water[c], 0.5) end
  end
  chain.update(W, W.S.v3.now)
  cast(W, { power = "rain", cell = s.cell })
  live(W, 96, 1)
  local list = chain.list(W)
  return list[1]
end

-- Rain on a drought in the growing season waters the soil the fields drink; rain on a wet floodplain runs
-- off into the river and over its banks. Different chains from the same power, each from real links.
function M.the_same_rain_two_chains()
  local Wd = load()
  local Id = rain_on(Wd, 1, "dry", 4)      -- (Tekheim: its corn grows from the first days of the year)
  local Ww = load()
  local Iw = rain_on(Ww, 6, "wet", 2)
  T.ok(Id ~= nil and Iw ~= nil, "both casts are interventions")
  if not Id or not Iw then return end
  local kd, kw = chain_kinds(Wd, Id), chain_kinds(Ww, Iw)
  local function list(k) local t = {} for x, n in pairs(k) do t[#t + 1] = x .. " " .. n end table.sort(t) return table.concat(t, ", ") end
  print("      on a drought: " .. list(kd))
  print("      on wet ground: " .. list(kw))
  T.ok((kd.soil or 0) >= 1, "on a drought the soil is watered")
  T.ok((kd.crops or 0) >= 1, "on a drought the growing fields drink it")
  T.ok((kw.flood or 0) >= 1, "on wet ground the river comes over its banks")
  T.ok((kw.soil or 0) == 0, "on soaked ground the soil is not 'watered' (the rain ran off)")
  local bd, bw = rests_on_the_world(Wd, Id), rests_on_the_world(Ww, Iw)
  T.ok(#bd == 0 and #bw == 0, "every node rests on a record or on live state: " .. table.concat(bd, ",") .. table.concat(bw, ","))
  -- the chain hangs together: the flood under the river that rose, the fields under the soil
  for _, row in ipairs(chain.rows(Ww, Iw, 500)) do
    if row.node.kind == "flood" then T.ok(row.node.parent and row.node.parent.kind == "river", "a flood hangs under its river") end
  end
end

-- A bolt in a dry village: the strike, and what caught fire hangs under it; the observer writes nothing.
function M.lightning_and_what_it_lit()
  local W = load()
  local V = W.S.v3
  local s = V.settlements[1]
  goto_local(W, s, 4, 15)
  W.F.soil_water[s.cell] = 0.05
  local Tr = s.trees
  local best, bd = nil, 1e9
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.state == "standing" then
      for k = 1, Tr.n do
        if Tr.st[k] == 1 and (Tr.size[k] or 0) > 0.7 then
          local d = (Tr.x[k] - b.cx) ^ 2 + (Tr.y[k] - b.cy) ^ 2
          if d > 0.0016 and d < bd then best, bd = k, d end
        end
      end
    end
  end
  chain.update(W, V.now)
  cast(W, { power = "lightning", cell = s.cell, sid = s.id, x = Tr.x[best], y = Tr.y[best] })
  local h0 = world.state_hash(W)
  chain.update(W, V.now)
  T.eq(world.state_hash(W), h0, "reading the chain changes nothing in the world")
  live(W, 48, 0.25)
  local I = chain.list(W)[1]
  T.ok(I and I.power == "lightning", "the bolt is an intervention")
  if not I then return end
  local rows = chain.rows(W, I, 100)
  T.ok(#rows >= 1 and rows[1].node.kind == "strike", "the strike first")
  local fire
  for _, row in ipairs(rows) do if row.node.kind == "fire" then fire = row.node end end
  print("      " .. (rows[1] and rows[1].node.title or "?") .. (fire and (" / " .. fire.title .. " - " .. (fire.line or "")) or ""))
  T.ok(fire ~= nil and fire.parent and fire.parent.kind == "strike", "the fire it lit hangs under the strike")
  local bad = rests_on_the_world(W, I)
  T.ok(#bad == 0, "every node rests on the world: " .. table.concat(bad, ","))
end

-- Culling the wolves: how many died is the cull's own record; the packs left are read from the planet.
function M.a_cull_and_the_packs_left()
  local W = load()
  local V = W.S.v3
  local s = V.settlements[4]
  goto_local(W, s, 2, 10)
  chain.update(W, V.now)
  cast(W, { power = "cull", cell = s.cell, species = "wolves" })
  live(W, 24, 2)
  local I = chain.list(W)[1]
  T.ok(I and I.power == "cull" and (I.killed or 0) > 0, "the cull's own event says how many it killed")
  local packs
  for _, row in ipairs(chain.rows(W, I, 50)) do if row.node.kind == "packs" then packs = row.node end end
  T.ok(packs ~= nil, "the packs it thinned are a consequence")
  print("      " .. (packs and packs.title or "?"))
end

return M
