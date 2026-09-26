-- Regions & names (sim/worldgen/regions.lua).
local T = require("tests.sim.lib")
local scen = require("tests.sim.scenarios.lib")
local names = require("content.names")
local events = require("sim.causality.events")
local M = {}

function M.every_cell_named_seas_6_to_10()
  local out = {}
  for seed = 1, 3 do
    local W = scen.fresh_world(seed)
    local reg, recs = W.static.region, W.names.regions
    local ocean = W.geo.ocean
    local seas, used = 0, {}
    for r = 1, #recs do
      local rec = recs[r]
      T.ok(type(rec.name) == "string" and #rec.name > 0, "region name")
      T.ok(not names.is_blocked(rec.name), "blocked name " .. rec.name)
      T.ok(not used[rec.name], "duplicate region name " .. rec.name)
      used[rec.name] = true
      if rec.kind == "sea" or rec.kind == "ocean" then seas = seas + 1 end
    end
    T.between(seas, 6, 10, "seas")
    for c = 1, W.n do
      local r = reg[c]
      T.ok(r >= 1 and r <= #recs, "cell " .. c .. " has a region")
      local k = recs[r].kind
      local is_sea = (k == "sea" or k == "ocean")
      T.eq(is_sea, ocean[c] == 1, "cell " .. c .. " region kind matches land/ocean")
      if not is_sea then T.eq(recs[r].continent, W.geo.continent[c], "land region follows its landmass") end
    end
    -- landmasses holding > 40% of land are split in two
    for i, comp in ipairs(W.geo.components) do
      local parts, np = {}, 0
      for c = 1, W.n do
        if W.geo.continent[c] == i and not parts[reg[c]] then parts[reg[c]] = true; np = np + 1 end
      end
      local want = (comp.size / W.geo.n_land > 0.4 and comp.size >= 8) and 2 or 1
      T.eq(np, want, "split rule for landmass " .. i)
    end
    out[#out + 1] = string.format("s%d: %d regions, %d seas", seed, #recs, seas)
  end
  return table.concat(out, ", ")
end

function M.events_carry_region()
  local W = scen.fresh_world(1)
  local c = W.geo.ocean_cells[1]
  local id = events.emit(W, "flood", c, 0, 1)
  T.eq(W.E.rec[id].region, W.static.region[c], "event region from W.static.region")
end

return M
