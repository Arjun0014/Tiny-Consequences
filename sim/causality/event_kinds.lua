-- Event kinds (03_CAUSALITY.md §6). One data table; no kind-specific logic scattered in code.
-- Fields: actor ("player" | "nature" | "derived"), icon (short text id the view maps to a glyph),
-- headline (in the headline set), hmin / hmax (headline minimum / maximum magnitude: a crash's
-- magnitude is a ratio, so smaller is bigger news; hmax_nature applies to nature-rooted events),
-- cooldown (ticks, per
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
  "heat", "drought", "kill_fish", "sink_tanker", "generic", "empty_stores", "emigrate", "voyage", "cattle_raid", "submit" }) do
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
def("blight_spread",    { actor = "derived", icon = "blight", headline = true, text = "Blight spreads" })
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
-- a crash with a cause is news at ÷2; a natural one only when it is deeper than the ordinary
-- predator–prey trough (deer troughs sit at 0.25–0.45 of their 5-year normal)
def("population_crash", { actor = "derived", icon = "crash", headline = true, hmax = 0.5, hmax_nature = 0.33,
  text = "Population crash" })
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
def("flooded_village",  { actor = "derived", icon = "flood", headline = true, text = "Village flooded" })

-- settlement layer (docs/world/16_CAUSALITY_VISUALS.md §3, D-050)
def("rationing",        { actor = "derived", icon = "famine", text = "Rationing" })
def("seed_eaten",       { actor = "derived", icon = "crop", headline = true, text = "Seed grain eaten" })
def("wood_shortage",    { actor = "derived", icon = "wood", text = "No firewood" })
def("housing_shortage", { actor = "derived", icon = "house", text = "No homes" })
def("labour_shortage",  { actor = "derived", icon = "people", text = "Too few hands" })
def("boat_lost",        { actor = "derived", icon = "boat", text = "Boat lost" })
def("shipment_lost",    { actor = "derived", icon = "tanker", headline = true, text = "Shipment lost" })
def("house_built",      { actor = "derived", icon = "house", text = "House built" })
def("village_expanded", { actor = "derived", icon = "house", text = "Village grows" })
def("fields_abandoned", { actor = "derived", icon = "crop", text = "Fields abandoned" })
-- villages that know each other (W7, docs/world/18 §2.3)
def("relief",           { actor = "derived", icon = "wheat", text = "Grain sent by neighbours" })
def("trade",            { actor = "derived", icon = "boat", agg = 48, text = "Trade" })
def("gift",             { actor = "derived", icon = "wheat", agg = 48, text = "Surplus grain given to neighbours" })
def("feast",            { actor = "derived", icon = "people", text = "A harvest feast" })
def("granary_built",    { actor = "derived", icon = "house", text = "A granary built against hunger" })
def("bride",            { actor = "derived", icon = "heart", text = "A bride leaves for another village" })
-- status and conflict (W8, docs/world/18 §2.1, §2.4)
def("new_leader",       { actor = "derived", icon = "people", text = "A new leader" })
def("village_split",    { actor = "derived", icon = "people", headline = true, text = "A lineage leaves to found its own village" })
def("raid",             { actor = "derived", icon = "fire", headline = true, text = "Raided" })
def("raid_repelled",    { actor = "derived", icon = "people", headline = true, text = "A raid beaten off" })
def("cattle_raid",      { actor = "derived", icon = "deer", headline = true, text = "Herds driven off by raiders" })
def("cattle_raid_repelled", { actor = "derived", icon = "people", text = "Cattle raiders chased off" })
def("truce",            { actor = "derived", icon = "people", headline = true, text = "A feud ends in a truce" })
def("grazing_dispute",  { actor = "derived", icon = "deer", text = "Neighbours quarrel over overgrazed pasture" })
def("fishing_dispute",  { actor = "derived", icon = "fish", text = "Neighbours quarrel over fished-out grounds" })
-- tribute and polities (W9, docs/world/18 §2.5)
def("submission",       { actor = "derived", icon = "people", headline = true, text = "Submits and pays tribute" })
def("secession",        { actor = "derived", icon = "people", headline = true, text = "Stops paying tribute" })
def("tribute",          { actor = "derived", icon = "wheat", text = "Tribute carried to the lord" })

return K
