-- Iron (S19): ore only where the ground holds it (veins in hard rock, bog iron in wet lowland beside a
-- river); the chain from the ground to the tools accounted for (ore dug, carried, smelted with
-- charcoal burned in a clamp, forged into edges carried to a household); iron edges make the work go
-- faster and wear down with it.
local T = require("tests.sim.lib")
local world = require("sim.world")
local worlds = require("sim.worlds")

local M = {}

local function shipped()
  local f = assert(io.open("assets/worlds/v3.tcsave", "rb"))
  local data = f:read("*a")
  f:close()
  local W = assert(worlds.from_save_string(data))
  world.set_systems(require("sim.agents.worldgen").systems())
  return W
end

function M.ore_only_where_the_ground_holds_it()
  local W = shipped()
  local V = W.S.v3
  local c = require("sim.config").V3
  world.advance(W, W.hour + 25)
  local with, without = 0, 0
  for _, s in ipairs(V.settlements) do
    local n = 0
    for _, id in ipairs(s.blist) do
      local b = s.buildings[id]
      if b.kind == "outcrop" and b.ore then
        T.eq(b.rock, "hard", s.name .. ": a vein only in hard rock")
        n = n + 1
      elseif b.kind == "bog" then
        local cell = s.cell_of[(b.iy0 - 1) * s.N + b.ix0]
        T.ok((W.F.soil_water[cell] or 0) >= c.bog_wet, s.name .. ": bog iron on wet ground")
        T.ok(W.static.elev[cell] - (W.G.sea_level or 0) <= c.bog_elev, s.name .. ": bog iron on low ground")
        n = n + 1
      end
    end
    if n > 0 then with = with + 1 else without = without + 1 end
  end
  T.ok(with >= 1 and without >= 1, string.format("some villages have ore (%d), some none (%d)", with, without))
  return string.format("%d villages with ore, %d without", with, without)
end

function M.iron_is_made_from_the_ground_up()
  local W = shipped()
  local V = W.S.v3
  local c = require("sim.config").V3
  local t0 = W.hour
  local seen = { mine = false, fire = false, smelt = false, smith = false }
  for h = 1, 24 * 36 do
    world.advance(W, t0 + h)
    if h % 2 == 0 then
      for _, id in ipairs(V.order) do
        local p = V.people[id]
        if p and p.alive then
          if p.act == "mine" then seen.mine = true end
          if p.act == "fire_clamp" then seen.fire = true end
          if p.act == "smelt" then seen.smelt = true end
          if p.act == "smith" then seen.smith = true end
        end
      end
    end
  end
  for k, v in pairs(seen) do T.ok(v, "someone was seen at " .. k) end
  local sets, blooms, with = 0, 0, 0
  for _, s in ipairs(V.settlements) do
    local a = s.acc
    local dug, made, b = a.ore_dug or 0, a.iron_made or 0, a.blooms or 0
    sets, blooms = sets + made, blooms + b
    -- ore: dug = lying at the deposits + at the forge + carried + smelted
    local lying, carried, cchar = 0, 0, 0
    for _, id in ipairs(s.blist) do
      local bd = s.buildings[id]
      if bd.kind == "outcrop" or bd.kind == "bog" then lying = lying + (bd.stock.ore or 0) end
    end
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.carry == "ore" then carried = carried + p.camt end
      if p and p.carry == "charcoal" then cchar = cchar + p.camt end
    end
    local fs = s.forge and s.forge.stock or {}
    T.ok(math.abs(dug - (lying + (fs.ore or 0) + carried + b * c.smelt_ore)) < 1e-6,
      string.format("%s: every measure of ore dug is somewhere (dug %d)", s.name, dug))
    -- charcoal: burned out of the clamps = at the clamp + at the forge + carried + used
    local cl = s.clamp
    local burned = a.charcoal or 0
    T.ok(math.abs(burned - ((cl and cl.stock.charcoal or 0) + (fs.charcoal or 0) + cchar + b * c.smelt_charcoal + made * c.forge_charcoal)) < 1e-6,
      string.format("%s: every sack of charcoal is somewhere (burned %.0f)", s.name, burned))
    T.ok(made <= b, s.name .. ": no iron forged without a bloom")
    if not s.forge then T.eq(made, 0, s.name .. " forged nothing without a forge") end
    for _, hid in ipairs(s.households) do
      local hh = V.households[hid]
      if hh and (hh.iron or 0) > 0 then with = with + 1 end
    end
  end
  T.ok(sets > 0, "iron edges were forged: " .. sets)
  T.ok(with > 0, "households work with iron edges: " .. with)
  return string.format("%d blooms smelted, %d sets of edges forged, %d households with iron", blooms, sets, with)
end

function M.iron_edges_speed_the_work()
  local W = shipped()
  local V = W.S.v3
  local think = require("sim.agents.think")
  local c = require("sim.config").V3
  local p = V.people[V.order[1]]
  local hh = V.households[p.hh]
  hh.tools, hh.iron = 1, 0
  local a = think.tool_factor(W, p)
  hh.iron = 1
  local b = think.tool_factor(W, p)
  T.ok(math.abs(b / a - (1 + c.iron_speed)) < 1e-9, string.format("iron edges: tool work %.2fx", b / a))
  hh.iron = nil
end

return M
