-- Player powers (sim/powers.lua, 04_PLAYER.md §2): each power is a logged command that creates
-- a root event (actor player) and tags the fields it changes; sustained effects expire; the
-- command log replays them bit for bit.
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local world = require("sim.world")
local events = require("sim.causality.events")
local M = {}

local function last_root(W, kind)
  for i = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[i]]
    if r.kind == kind and r.parent == 0 then return r end
  end
end

local function cast(W, p)
  world.command(W, "power", p)
  world.tick(W)
end

function M.every_power_makes_a_player_root_and_tags_its_field()
  local W = scen.fresh_world(3)
  local F, C = W.F, W.C
  local land, oc = W.geo.land_cells, W.geo.ocean_cells
  -- a forested cell for lightning, a grazer cell for cull, open sea for oil/heat
  local forest, grz = nil, nil
  for _, c in ipairs(land) do
    if not forest and F.forest[c] > 0.4 and F.land_use[c] == 0 then forest = c end
    if not grz and F.grazers[c] > 0.05 then grz = c end
  end
  local sea = oc[math.floor(#oc / 2)]
  local farm = nil
  for _, v in ipairs(W.ent.villages) do if v.alive and #v.farms > 0 then farm = v.farms[1]; break end end
  local cases = {
    { { power = "lightning", cell = forest }, "power_lightning", "fire", forest },
    { { power = "weather", cell = forest, mode = "rain" }, "power_rain", nil, nil },
    { { power = "weather", cell = forest, mode = "drought" }, "power_drought", "heat_anom", forest },
    { { power = "temperature", cell = sea, mode = "heat" }, "power_heat", "heat_anom", sea },
    { { power = "temperature", cell = grz, mode = "chill" }, "power_chill", "heat_anom", grz },
    { { power = "cull", cell = grz, species = "grazers" }, "power_cull", "grazers", grz },
    { { power = "introduce", cell = forest, species = "predators" }, "power_introduce", "predators", forest },
    { { power = "blight", cell = farm }, "power_blight", "crop", farm },
    { { power = "oil", cell = sea }, "power_oil", "oil", sea },
    { { power = "meteor", cell = forest }, "power_meteor", "heat_anom", forest },
  }
  for _, cs in ipairs(cases) do
    local before = W.E.next_id
    cast(W, cs[1])
    local r = last_root(W, cs[2])
    T.ok(r and r.id >= before, cs[2] .. " root event")
    T.ok(r.actor == "player", cs[2] .. " is the player's")
    if cs[3] then
      local tag = C[cs[3]][cs[4]]
      T.ok(tag ~= 0 and events.is_player_rooted(W, tag), cs[2] .. " tags " .. cs[3] .. " (" .. tostring(tag) .. ")")
    end
  end
  T.ok((W.G.aerosol or 0) > 0.5, "meteor: impact-winter aerosol")
end

function M.weather_and_temperature_last_one_season()
  local W = scen.fresh_world(3)
  local c = W.geo.land_cells[10]
  cast(W, { power = "weather", cell = c, mode = "drought" })
  T.ok(W.scratch.weather_on and W.scratch.weather_force[c] ~= nil, "drought forcing active")
  for _ = 1, 12 do world.tick(W) end
  T.ok(not W.scratch.weather_on, "forcing expired after a season")
  T.eq(#W.S.powers.forcings, 0, "no forcings left")
end

function M.settlers_found_a_village()
  local W = scen.fresh_world(3)
  local n0 = #W.ent.villages
  local site = nil
  for _, c in ipairs(W.geo.land_cells) do
    if require("sim.society.sites").free_site(W, c) and W.F.ice[c] < 0.3 then site = c; break end
  end
  cast(W, { power = "introduce", cell = site, species = "settlers" })
  T.eq(#W.ent.villages, n0 + 1, "a new village")
  local v = W.ent.villages[n0 + 1]
  T.eq(v.pop, 5, "five settlers")
  local r = last_root(W, "power_introduce")
  local found = false
  for _, d in ipairs(events.descendants(W, r.id)) do if d.kind == "village_founded" then found = true end end
  T.ok(found, "village_founded descends from the power")
end

function M.powers_replay_bit_identically()
  local A = scen.fresh_world(2)
  local B = scen.fresh_world(2)
  local c = A.geo.land_cells[20]
  for _, W in ipairs({ A, B }) do
    world.command(W, "power", { power = "weather", cell = c, mode = "rain" })
    world.command(W, "power", { power = "lightning", cell = c })
    for _ = 1, 30 do world.tick(W) end
  end
  T.eq(world.state_hash(A), world.state_hash(B), "same commands, same world")
end

return M
