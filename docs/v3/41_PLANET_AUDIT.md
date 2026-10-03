# V3-41 — Audit: what of the planet is live in v3, and what reaches the agents

Session 20 (3 Oct 2026), main at `463b6ca`. Proved from the code and from a run of the shipped
world (`assets/worlds/v3.tcsave`, 96 days = 2 planet years, scratch probe `planet_audit.lua`:
events by kind, events within 3 cells of a village, each village's field extremes). "Live" means
the system runs in a v3 world (`sim/agents/worldgen.lua` `M.systems()`: powers, globalclimate,
temperature, moisture, ice, hydrology, vegetation, fire, ocean, fauna, agents, biome, detectors -
one planet step per game day, each step 1/48 of a planet year; no v2 society, no tanker ships).

Legend - CODE: where it lives. LIVE: runs in v3. AGENTS: what v3 people/animals/economy read
from it. TAG: causal blame on its field. VIS: drawn. GAP: the missing link.

## 1. Planet systems

| System | CODE | LIVE | AGENTS read it? | TAG | VIS | GAP |
|---|---|---|---|---|---|---|
| Temperature (lat, season, lapse, g_temp, heat_anom, aerosol, yearly wobble) | `systems/temperature.lua` | yes | **yes**: crop growth (`settle.grow_plots`), pasture regrowth (`stock.daily_pasture`), daily temp (`agents/weather.lua`) -> `s.cold` (hearth wood, cold meals, rags/barefoot health, infant/elder hazard) | yes | seasons, snow tint | no frost kill of crops; no heat stress; no shelter from cold beyond the hearth |
| SST (lagged) | `temperature.lua` | yes | indirectly (evaporation, plankton -> fish) | yes | ocean colour | none needed for R6.5 start |
| Winds (prevailing bands, season shift) | `systems/moisture.lua` | yes (advection tables) | no (fire spread on maps, smoke, boats do not read wind) | - | clouds drift | wind for local fire and sailing |
| Moisture / precipitation | `moisture.lua` | yes | **yes**: rain spells per village per day (`agents/weather.lua`: 0-2 spells, downpours) -> shelter, work, birds | yes | rain, clouds | snowfall not distinguished on maps |
| Multi-year wet/dry (evaporation wobble, 2-year knots) | `moisture.lua` `rain_wobble` | yes | indirectly (soil water -> crops, pasture) | baseline | - | - |
| Daily weather (storms, gales) | `society/weather_day.lua` | yes (read on demand) | **yes**: boats stay in for storms (`trade.sea_rough`), rain | - | - | storms do not damage roofs, trees, crops |
| Snow / sea ice / land ice | `systems/ice.lua` | yes | only road cost (`trade.lua` `route_ice_k`) | yes | snow on planet | **no snow on maps**: walking, forage, hay, cold not affected (no village had any snow in 96 days: all at lat -37..+14) |
| Hydrology: soil water, runoff, rivers, floods, silt | `systems/hydrology.lua` | yes | **yes**: soil water -> crop yield and pasture; flow -> map rivers re-laid daily (`map.rivers`: floods widen, close fords; drought opens fords), bridges worn and swept (`settle floods`, D-106) | yes | rivers swell | flood water never leaves the river's floodplain on a map (fields and houses are kept off it by founding); everyone "remembers" a bridge lost whether they saw it or not |
| Drought | (soil water, detectors) | yes as a state | indirectly: thin harvests, pasture | yes | browning | wells/water sources never fail; no drought event for v3 villages |
| Fire (natural lightning ignition, spread, burn, scar, ash fertility) | `systems/fire.lua` | yes (2 strikes, 2 fires planet-wide in 96 days; none within 3 cells of a village) | **no** (map trees, plots, buildings, people and beasts ignore `F.fire`) | yes | fire on planet | **no fire on maps**: no burning trees/buildings/fields, no flight |
| Vegetation succession, biome shifts | `vegetation.lua`, `biome.lua` | yes (7 shifts in 96 days) | map trees seeded once from `F.forest`; felling writes back (biomass, CO2) | yes | biome colours | map trees do not regrow from the planet; planet regrowth does not add trees on a map |
| Fish / plankton / currents | `systems/ocean.lua` | yes | **yes**: catches from `F.fish` (act.lua), gulls | yes | - | - |
| Oil | `ocean.lua` | yes (no source in v3: no tankers) | no | yes | sheen | - |
| Carbon / CO2 | `globalclimate.lua` (+ felling in `act.lua`) | yes (280.0 -> 280.2 in 2 years) | felling adds CO2 | ledger | - | hearth burning not counted (small) |
| Aerosol (decay; meteor power adds) | `globalclimate.lua`, `temperature.lua` | yes | **indirectly yes**: cools every cell's temperature -> v3 crops and pasture grow slower | ledger | - | no natural source (no eruptions) |
| Global temperature, ice-albedo, vapour feedback, eras (garden/warm/hothouse/iceage/snowball/dead) | `globalclimate.lua` | yes (flat over 2 years) | indirectly through temperature | ledger | - | never exercised in v3; v3 calendar makes a planet year 48 days |
| Sea level | `globalclimate.lua` | computed | no | - | - | **coastline never rebuilt** (`G.sea_built` stays 0): spec-only |
| Tectonic plates (static, boundary type and strength) | `worldgen/terrain.lua` | static data (153 convergent, 141 divergent, 34 transform cells) | no | - | mountains | **no stress, no earthquakes**: spec-only. Tekheim and Zurug stand on convergent boundary cells |
| Volcano sites | `worldgen/settle.lua` `volcanoes()` | **not generated in v3** (`no_settle` skips it: `W.ent.volcanoes` nil) | no | - | - | spec-only: no pressure, eruption, lava, ash |
| Earthquake, tsunami, landslide | event kinds only (`event_kinds.lua`) | no | no | - | - | spec-only |
| Meteor | `sim/powers.lua` (v2 power) | the command exists | planet fields (heat, fire, aerosol, wild herds) yes; **v3 people no** (it kills v2 `W.ent.people`) | yes | - | power needs a v3 exposure path |
| Lightning power | `sim/powers.lua` | the command exists | **no** (targets v2 people/boats/ships; on v3 it can only ignite the planet cell) | yes | bolt | R6.5 lightning vertical |
| Weather / temperature / cull / blight powers | `sim/powers.lua` | yes on fields | fields yes (rain, heat reach crops); cull of wild herds reaches maps via fauna; blight targets v2 farm cells (no v3 plots) | yes | - | blight on v3 plots |
| Wild herds and packs (W4) | `systems/fauna.lua` | yes | **yes**: their members walk on maps (`agents/wild.lua`); fauna avoids fire cells and dies in fire, snow hides forage and helps wolves | yes | animals | map animals do not see a fire or flood on the map |

## 2. Do the agents respond? (code-proven)

| Hazard / change | People | Livestock | Wild animals | Economy | Memory |
|---|---|---|---|---|---|
| Rain | yes: shelter with per-person delay (`agents/init.lua rain_change`), work resumes | downpour keeps beasts penned (`stock.lua` 536) | - | - | no |
| Downpour / storm | shelter; boats stay in (`trade.sea_rough`) | penned | gulls roost | voyages wait | no |
| Cold | hearth wood, cold meals, rags/barefoot lose health, infants/elders hazard | hay in winter (doy >= 36) | (planet: snow) | wood demand | "Shivering..." |
| Heat | no | no | no | no | no |
| Snow | **no** (none at villages) | no | no | no | no |
| Flood | fords close, ways change, bridges swept | no | no | crossings want a new bridge | "The flood swept the bridge away" (all adults, unseen) |
| Wildfire | **no** | **no** | **no** (on maps) | **no** | no |
| Drought | indirect: thin harvest -> hunger -> relief, culls, migration (D-140, D-147) | thin pasture -> culls | (planet) | worths rise | hunger memories |
| Destroyed crops | only by spoilage/neglect | - | - | yes | - |
| Destroyed homes | no hazard destroys a house (houses weather and are repaired) | - | - | - | - |
| Sudden death | widow(er) remembers; household and village lists updated; orphans taken in (`life.take_in`); estates pass to kin (`settle.estates`); apprentices find a new mentor | beasts to kin | - | labour simply missing | **children, parents, siblings do not remember the death** |
| Route loss | bridges swept, fords closed: A* re-routes; long roads use planet cells (ice raises cost) | - | - | trade sees the road's cost | - |
| Wolves | night watch after a loss (`think.guard_herd`), dogs bark, people drive packs off (`s.alarm`, `drive_off`) | penned beasts taken | flight with alarm spreading through the band (`wild.alarm`, `wild.threat`: wolves, running dogs, people) | beasts lost | "Drove the wolves away..." |

## 3. What this means for R6.5
- **Already a chain through fields, with no new code**: aerosol or a cold anomaly -> `F.temp` ->
  slower crop growth and pasture -> thinner harvest and hay -> culls, relief, trade by worth,
  migration. Rain anomalies -> soil water -> yields. SST -> fish -> catches. These are R6.5's
  climate verticals waiting only for a *source* (an eruption, an SST shift, a god power).
- **Missing entirely on the maps**: fire, snow, ash, shaking, flood water beyond the river,
  lightning at a place, and any agent response to a physical danger other than wolves and rain.
- **Missing in the agents**: a general notion of danger, injury, damage to buildings by hazards,
  structured memory of what happened (memories are 8 lines of text), bereavement for anyone but a
  spouse, witnesses (memories are given to people who did not see).
- **Spec-only on the planet**: geology (stress, quakes, eruptions, tsunami, landslides), coastline
  rebuild from sea level, volcano sites in v3 worlds.
- **Nothing in v3 has been exercised at the climate extremes** (ice age, hothouse, snowball): the
  untouched world stays a garden; v3 villages sit between 37° S and 14° N.
