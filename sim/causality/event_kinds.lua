-- Event kinds (03_CAUSALITY.md §6). One data table; no kind-specific logic scattered in code.
-- Fields: actor ("player" | "nature" | "derived"), icon (short text id the view maps to a glyph),
-- headline (in the headline set), hmin (headline minimum magnitude), cooldown (ticks, per
-- kind+key, enforced by events.emit when a key is given), agg (aggregation window in ticks:
-- same kind + same parent + same region within the window merges into one event), text (short
-- fallback label; chronicle templates live in content/).

local K = {}

local function def(name, t) t.name = name; K[name] = t end

-- player roots
for _, p in ipairs({ "lightning", "rain", "drought", "heat", "chill", "cull", "introduce",
  "blight", "oil", "meteor", "volcano", "quake" }) do
  def("power_" .. p, { actor = "player", icon = "power_" .. p, headline = false, text = "Power: " .. p })
end
-- test roots (actor player, used by scenario hooks before powers exist)
for _, t in ipairs({ "remove_predators", "ignite", "oil", "burn_farms", "add_grazers",
  "heat", "drought", "kill_fish", "generic" }) do
  def("test_" .. t, { actor = "player", icon = "test", headline = false, text = "Test: " .. t })
end

-- nature roots
def("lightning_strike", { actor = "nature", icon = "lightning", text = "Lightning strike" })
def("drought_onset",    { actor = "nature", icon = "drought", text = "Drought begins" })
def("storm",            { actor = "nature", icon = "storm", text = "Storm" })
def("heatwave_onset",   { actor = "nature", icon = "heat", text = "Heatwave begins" })
def("eruption",         { actor = "nature", icon = "volcano", headline = true, hmin = 3, text = "Eruption" })
def("quake",            { actor = "nature", icon = "quake", headline = true, hmin = 6, text = "Earthquake" })
def("meteor_natural",   { actor = "nature", icon = "meteor", headline = true, text = "Meteor" })
def("village_accident", { actor = "nature", icon = "fire", text = "Hearth fire" })

-- derived (detectors)
def("biome_shift",      { actor = "derived", icon = "biome", agg = 48, text = "Land changes" })
def("fire_started",     { actor = "derived", icon = "fire", agg = 4, text = "Fire" })
def("wildfire",         { actor = "derived", icon = "wildfire", headline = true, hmin = 10, text = "Wildfire" })
def("crop_failure",     { actor = "derived", icon = "crop", text = "Crop failure" })
def("famine",           { actor = "derived", icon = "famine", headline = true, text = "Famine" })
def("deaths_spike",     { actor = "derived", icon = "death", text = "Deaths" })
def("person_died",      { actor = "derived", icon = "ghost", text = "Death" })
def("migration",        { actor = "derived", icon = "migration", text = "Migration" })
def("village_founded",  { actor = "derived", icon = "house", headline = true, text = "Village founded" })
def("village_abandoned",{ actor = "derived", icon = "ruins", headline = true, text = "Village abandoned" })
def("overfishing",      { actor = "derived", icon = "fish", agg = 48, text = "Overfishing" })
def("fish_collapse",    { actor = "derived", icon = "fish", headline = true, agg = 48, text = "Fish collapse" })
def("tanker_sunk",      { actor = "derived", icon = "tanker", headline = true, text = "Tanker sinks" })
def("oil_spreading",    { actor = "derived", icon = "oil", text = "Oil spreads" })
def("oil_beached",      { actor = "derived", icon = "oil", headline = true, agg = 48, text = "Oil reaches the coast" })
def("population_boom",  { actor = "derived", icon = "boom", headline = true, text = "Population boom" })
def("population_crash", { actor = "derived", icon = "crash", headline = true, text = "Population crash" })
def("extinction_regional", { actor = "derived", icon = "extinct", headline = true, text = "Regional extinction" })
def("extinction_global",   { actor = "derived", icon = "extinct", headline = true, text = "Extinction" })
def("flood",            { actor = "derived", icon = "flood", agg = 12, text = "Flood" })
def("drought",          { actor = "derived", icon = "drought", agg = 48, text = "Drought" })
def("heatwave",         { actor = "derived", icon = "heat", agg = 12, text = "Heatwave" })
def("cold_snap",        { actor = "derived", icon = "cold", agg = 12, text = "Cold snap" })
def("sea_ice_formed",   { actor = "derived", icon = "ice", text = "Sea ice forms" })
def("sea_ice_melted",   { actor = "derived", icon = "ice", text = "Sea ice melts" })
def("sea_level_change", { actor = "derived", icon = "sea", headline = true, text = "Sea level changes" })
def("era_change",       { actor = "derived", icon = "era", headline = true, text = "New era" })
def("tsunami",          { actor = "derived", icon = "wave", headline = true, text = "Tsunami" })
def("new_island",       { actor = "derived", icon = "island", headline = true, text = "New island" })
def("blight_spread",    { actor = "derived", icon = "blight", text = "Blight spreads" })
def("flooded_village",  { actor = "derived", icon = "flood", headline = true, text = "Village flooded" })

return K
