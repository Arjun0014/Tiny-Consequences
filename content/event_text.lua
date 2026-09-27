-- Event descriptions for the chain popup, "Why?" and the event log (03_CAUSALITY.md §5,
-- Stage 2.3). Pure Lua data + one function: describe(W, record) -> icon, text.
-- Icons are UI atlas names (assets/ui/i_<name>.png). Chronicle headlines (the tabloid voice)
-- live in content/chronicle.lua; these are plain, short and exact.
local M = {}

local SPECIES = { grazers = "deer", predators = "wolves", fish = "fish", settlers = "settlers" }
local BIOMES = { "sea", "sea ice", "ice sheet", "village", "farmland", "ruins", "ash", "marsh", "desert",
  "tundra", "jungle", "forest", "grassland", "barren land" }

local function region(W, r)
  local reg = r.region and W.names and W.names.regions and W.names.regions[r.region]
  return reg and reg.name or "the wilds"
end

local function village(W, id)
  local v = id and W.ent and W.ent.villages and W.ent.villages[id]
  return v and v.name or "a village"
end

local function cap(s) return (s:gsub("^%l", string.upper)) end

M.ICON = {
  power_lightning = "bolt", power_rain = "rain", power_drought = "drought", power_heat = "heat",
  power_chill = "chill", power_cull = "skull", power_introduce = "paw", power_blight = "blight",
  power_oil = "oil", power_meteor = "meteor", lightning_strike = "bolt", village_accident = "fire",
  fire_started = "fire", wildfire = "fire", biome_shift = "tree", crop_failure = "wheat", famine = "bread",
  deaths_spike = "skull", person_died = "skull", migration = "people", village_founded = "house",
  village_abandoned = "house", fish_collapse = "fish", overfishing = "fish", tanker_sunk = "ship",
  oil_spreading = "oil", oil_beached = "oil", population_boom = "up", population_crash = "down",
  extinction_regional = "skull", extinction_global = "skull", flood = "wave", flooded_village = "wave",
  drought = "drought", heatwave = "heat", cold_snap = "chill", sea_ice_formed = "snow",
  sea_ice_melted = "snow", blight_spread = "blight", sea_level_change = "wave", era_change = "globe",
  tsunami = "wave", new_island = "globe", eruption = "volcano", quake = "volcano", meteor_natural = "meteor",
}

local T = {}
T.power_lightning = function(W, r)
  local d = r.data or {}
  if d.ship then
    local s = W.ent.ships and W.ent.ships[d.ship]
    if s then return 'Your lightning strikes the tanker "' .. s.name .. '"' end
  end
  if d.person then
    local p = W.ent.people and W.ent.people[d.person]
    if p then return "Your lightning strikes " .. p.name end
  end
  return "Your lightning strikes " .. region(W, r)
end
T.power_rain = function(W, r) return "You send a deluge over " .. region(W, r) end
T.power_drought = function(W, r) return "You call a drought over " .. region(W, r) end
T.power_heat = function(W, r) return "You heat " .. region(W, r) end
T.power_chill = function(W, r) return "You chill " .. region(W, r) end
T.power_cull = function(W, r)
  local sp = SPECIES[(r.data or {}).species] or "animals"
  return "You cull the " .. sp .. " of " .. region(W, r)
end
T.power_introduce = function(W, r)
  local sp = (r.data or {}).species
  if sp == "settlers" then return "You send settlers to " .. region(W, r) end
  return "You bring " .. (SPECIES[sp] or "animals") .. " to " .. region(W, r)
end
T.power_blight = function(W, r) return "You blight a farm in " .. region(W, r) end
T.power_oil = function(W, r) return "You spill oil into " .. region(W, r) end
T.power_meteor = function(W, r) return "Your meteor strikes " .. region(W, r) end
T.lightning_strike = function(W, r) return "Lightning strikes " .. region(W, r) end
T.village_accident = function(W, r) return "A hearth fire in " .. region(W, r) end
T.fire_started = function(W, r) return "Fire in " .. region(W, r) end
T.wildfire = function(W, r) return string.format("Wildfire burns %d fields of %s", math.floor(r.mag + 0.5), region(W, r)) end
T.biome_shift = function(W, r)
  local d = r.data or {}
  local to = BIOMES[d.to] or "something else"
  return cap(region(W, r)) .. " turns to " .. to
end
T.crop_failure = function(W, r)
  local d = r.data or {}
  return "The harvest fails in " .. village(W, d.village) .. (r.mag and r.mag > 0 and string.format(" (-%d%%)", math.floor(r.mag + 0.5)) or "")
end
T.famine = function(W, r) return "Famine in " .. village(W, (r.data or {}).village) end
T.deaths_spike = function(W, r) return string.format("%d die in %s", math.floor(r.mag + 0.5), village(W, (r.data or {}).village)) end
T.migration = function(W, r)
  local d = r.data or {}
  if d.to and d.to ~= 0 then return string.format("%d leave %s for %s", math.floor(r.mag + 0.5), village(W, d.from), village(W, d.to)) end
  if d.colonists then return string.format("%d colonists leave %s", math.floor(r.mag + 0.5), village(W, d.from)) end
  return string.format("%d leave %s", math.floor(r.mag + 0.5), village(W, d.from))
end
T.village_founded = function(W, r)
  local d = r.data or {}
  return village(W, d.village) .. " is founded" .. (d.settlers and " by settlers" or "")
end
T.village_abandoned = function(W, r) return village(W, (r.data or {}).village) .. " is abandoned" end
T.fish_collapse = function(W, r) return "Fish collapse in " .. region(W, r) end
T.overfishing = function(W, r) return "Overfishing in " .. region(W, r) end
T.tanker_sunk = function(W, r)
  local d = r.data or {}
  return 'The "' .. (d.name or "tanker") .. '" sinks'
end
T.oil_spreading = function(W, r)
  local d = r.data or {}
  return string.format("Oil spreads through %s (%d cells)", region(W, r), d.cells or math.floor(r.mag + 0.5))
end
T.oil_beached = function(W, r)
  local d = r.data or {}
  if d.village then return "Oil washes up at " .. village(W, d.village) end
  return "Oil washes up on " .. region(W, r)
end
T.population_boom = function(W, r)
  return cap(SPECIES[(r.data or {}).species] or "animals") .. " boom in " .. region(W, r)
end
T.population_crash = function(W, r)
  return cap(SPECIES[(r.data or {}).species] or "animals") .. " crash in " .. region(W, r)
end
T.extinction_regional = function(W, r)
  return "The last " .. (SPECIES[(r.data or {}).species] or "animals") .. " of " .. region(W, r) .. " are gone"
end
T.extinction_global = function(W, r)
  return "The " .. (SPECIES[(r.data or {}).species] or "animals") .. " are extinct"
end
T.flood = function(W, r) return "Floods in " .. region(W, r) end
T.flooded_village = function(W, r) return village(W, (r.data or {}).village) .. " is flooded" end
T.drought = function(W, r) return "Drought in " .. region(W, r) end
T.heatwave = function(W, r) return "Heatwave over " .. region(W, r) end
T.cold_snap = function(W, r) return "Cold snap over " .. region(W, r) end
T.sea_ice_formed = function(W, r) return "Sea ice forms over " .. region(W, r) end
T.sea_ice_melted = function(W, r) return "The sea ice melts in " .. region(W, r) end
T.blight_spread = function(W, r) return "Blight reaches " .. village(W, (r.data or {}).village) end
M.TEXT = T

--- Icon name and one-line text for event record r.
function M.describe(W, r)
  local f = T[r.kind]
  local text
  if f then text = f(W, r)
  elseif r.kind:sub(1, 5) == "test_" then text = "Test: " .. r.kind:sub(6):gsub("_", " ")
  else text = cap((r.kind:gsub("_", " "))) end
  return M.ICON[r.kind] or "info", text
end

--- "Year N" of an event (worldgen resets the clock to Year 0 after spin-up).
function M.year(W, r)
  return math.floor(r.tick / 48)
end

return M
