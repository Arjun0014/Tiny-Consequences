# W10 — World system model: a clockwork tiny planet

This document supersedes `02_WORLD_MODEL.md` §1 (time), §5.10 (people & villages) and §5.11
(tankers & boats), and extends §5.9 (animals). The planet-layer sections of 02 (temperature,
moisture, ice, hydrology, vegetation, fire, ocean, biomes, global climate) stay valid. Decision
record: D-050. Research basis: `00_RESEARCH.md`. Audit: `01_AUDIT.md`.

## 0. Principles

1. **A network of flows, not a set of features.** Every system consumes outputs of other systems
   and produces inputs for others. Food, wood, labour, people, animals, fish, water, heat, oil and
   information *move*; consequences travel along those movements.
2. **Stocks and flows are conserved.** A harvest adds exactly what the field lost; a boat's catch
   removes exactly that much fish; a house consumes exactly its timber from the stockpile; a cargo
   exists in exactly one place (a store, a hold, or lost at the bottom of the sea).
3. **Agents act for reasons.** Every person, boat and herd is doing something because world state
   made it the best available thing to do (needs, stocks, season, daylight, weather, distance).
   No random wandering, no invented behaviour in the view.
4. **Constraints create consequences.** Labour time, daylight, travel time, storage, seed, timber,
   boats and housing are finite. Scarcity in one propagates to the others.
5. **No scripted consequences** (02 §0.2) and **everything blamed** (03). New stocks and entities
   carry cause tags like fields do.
6. **Determinism and locality** (05 §5). Entity randomness is keyed by where/when the entity was
   created; iteration is in id order; no `pairs()` in logic.
7. **Visible or it doesn't exist.** Every state that matters has a visual manifestation
   (`16_CAUSALITY_VISUALS.md`).

## 1. Two layers, one world

| Layer | Resolution | Clock | Contents |
|---|---|---|---|
| **Planet layer** | 1,442 geodesic cells (f12, D-047), fields in SoA | weekly (as today) | climate, weather totals, water, ice, vegetation, soil, fire, ocean life, oil, biomes, global carbon |
| **Settlement & agent layer** | sub-cell: lots, buildings, field cells, boats, people, herds | hourly clock; each village plans its *local* day at its local midnight | households, people, buildings, stocks, labour, farming, fishing, forestry, construction, transport, trade, migration; later herds and packs |

This is the "coarse global + fine local" hierarchy: the planet stays cheap and stable; detail
lives where life is. The layers couple only through fields (crop, forest, fish, grazers,
predators, fertility, fire, soil water, temperature, precip, ice, oil) and through events.

## 2. The clock (details: `15_CLIMATE_TIME.md`, scheduler §5)

- Master clock `W.hour` (hours elapsed). 1 day = 24 h, 1 week = 7 days = 168 h, 1 month = 4
  weeks, 1 year = 48 weeks = 336 days = 8,064 h. `W.day = floor(W.hour / 24)`; `W.tick` keeps its
  meaning: number of completed planet weeks.
- Local solar time: `local_hour = (W.hour + lon_deg / 15) mod 24`. The planet rotates under a sun
  whose subsolar point follows the day of year (declination ±23.44°) and the UTC hour.
- Each village runs its *day step* in the UTC hour in which its local midnight falls, so villages
  on the night side sleep while those on the day side work.

## 3. Systems

For each system: state · inputs · outputs · rate · dependencies · entities · spatial/temporal
scale · causal attribution · visual. "Planet" systems are unchanged unless noted.

### 3.1 Climate & daily weather (planet + downscaling)
- **State**: fields temp, heat_anom, sst, moisture, precip, ice (weekly); daily weather is *derived*
  (not stored): wet/dry, rain amount, wind/storm per (cell, day) from a WGEN-style downscaling of
  the week's precip keyed by rng(cell, day).
- **Inputs**: global climate, powers' forcings, season, latitude. **Outputs**: temp/precip/ice to
  everything; daily weather to labour, boats, construction, people's comfort; daylight hours.
- **Rate**: weekly fields; daily realisation computed on demand (cheap hash).
- **Causality**: unchanged; daily weather inherits the week's precip/temp tags.
- **Visual**: clouds and rain where and when it rains today, storms, snow, day/night, seasonal light.

### 3.2 Water, vegetation, soil, fire, biomes (planet)
Unchanged (02 §5.5–5.7, 5.15). New coupling: forestry removes `forest` biomass and field
clearing removes vegetation; farming depletes `fertility`; abandonment returns cells to
succession. Visual: tree density, stumps on logged cells, fields, fallow, succession stages.

### 3.3 Ocean life & oil (planet)
Unchanged dynamics (plankton → fish logistic with oil/heat/silt). Fishing is no longer a village
formula: **boats** remove catch from specific grounds. Oil comes from real cargo when a merchant
ship or boat sinks. Visual: fish schools/jumps where fish are dense in season, oil sheen, boats.

### 3.4 Farming (`11_ECONOMY.md` §3)
- **State**: per farm cell: owner village, plan (crop this season / fallow), sown fraction,
  harvested fraction, weeding debt, ripeness; the planet field `crop` = standing crop biomass.
- **Inputs**: labour hours (by task), seed grain from the store, temperature (growing degree
  days), soil water, fertility, frost, floods, fire, raids, blight, oil. **Outputs**: grain into
  the village store (harvest), fertility depletion, vegetation cleared, crop biomass to grazers
  (raids) and fire (fuel).
- **Rate**: labour and progress daily per village; growth weekly with the planet (existing crop
  growth moved into the farming module).
- **Entities**: fields (cells), workers. **Scale**: cells; seasonal.
- **Causality**: crop tag (existing) + labour-shortfall tag (the tag of whatever removed the
  workers: deaths, migration) + seed-shortfall tag (stock tag). Harvest tag → grain stock term.
- **Visual**: furrows being sown, green → gold, harvest sweep, stubble, fallow weeds, people
  working in the actual field.

### 3.5 Forestry & fuel (`11_ECONOMY.md` §4)
- **State**: village wood stock (+ tag); logging pressure per cell (recent cut).
- **Inputs**: woodcutter hours, forest density and distance, fuel demand (temperature, households,
  house condition), construction demand. **Outputs**: wood into the stock; `forest` reduced in the
  cut cells; CO₂ from burning (ledger "villagers"); cold stress when fuel runs out.
- **Rate**: daily. **Causality**: wood stock tag; logging writes forest blame with the village's
  cause. **Visual**: woodcutters in the woods, stumps and thinning, woodpiles at houses, chimney
  smoke proportional to burning.

### 3.6 Fishing (`13_TRANSPORT.md` §2)
- **State**: boats (entities) with crew, trip plan, catch aboard, condition.
- **Inputs**: fish density per ground, daylight, weather (storm = no trip), sea ice, crew
  availability. **Outputs**: fresh fish into the store; fish biomass removed; boats lost in
  storms. **Rate**: plan at the village day step; arrivals processed in the hour they happen.
- **Causality**: fish tag of the ground → catch term; lost boat events. **Visual**: boats leave
  the pier after dawn, sail to the ground, work, return at dusk; nets; catch unloaded.

### 3.7 Construction (`11_ECONOMY.md` §5)
- **State**: buildings (entities): kind, lot, progress, wood delivered, condition, occupants.
- **Inputs**: demand (households without a home, overcrowding, decayed buildings, storage
  overflow, boats needed), wood stock, builder hours, weather. **Outputs**: houses, stores, piers,
  boats; wood consumed; lots occupied (village grows physically, may claim a new cell).
- **Rate**: planning weekly, progress daily. **Causality**: a site stalled for lack of wood carries
  the wood stock's tag; `house_built` carries the demand's cause. **Visual**: staked lot → frame →
  walls → roof; scaffolds; builders at work; repairs.

### 3.8 People & households (`12_PEOPLE_VILLAGES.md`)
- **State**: persons (age, sex, household, occupation, today's task, nutrition, health,
  pregnancy/nursing, partner/betrothed, parents, traits, memories); households (home, members).
- **Inputs**: food ration, fuel/warmth, home, workload, family events, village state, news.
  **Outputs**: labour, consumption, births, deaths, marriages, household formation, migration.
- **Rate**: tasks at the village day step (assignments re-planned weekly and on urgency);
  nutrition/health/demography weekly per person; due births daily.
- **Causality**: deaths carry the dominant hazard's tag (hunger → food stock tag; cold → wood stock
  tag; exposure → event); migration carries the push factor's tag.
- **Visual**: each person's position is a pure function of their task, home, daylight, weather and
  state (`sim/society/schedule.lua`, shared by the view): asleep at home at night, at work by day,
  eating, walking, socialising at the well, children playing.

### 3.9 Village (`12_PEOPLE_VILLAGES.md` §4)
- **State**: stocks (grain, fresh, wood) with cause tags, fields, buildings, households, boats,
  lots, trade memory, targets, state label. **Inputs/outputs**: aggregates of its members' work
  and consumption; it *plans* (labour allocation, construction queue, field plan, trade) but has no
  production of its own. **Rate**: day step at local midnight; planning weekly (staggered).
- **Visual**: the settlement is drawn from its buildings, fields, piers, stocks and people.

### 3.10 Transport & trade (`13_TRANSPORT.md`)
- **Merchant ships** (replace tankers): real cargo on a circuit of ports, unloading where goods are
  short, loading surplus; carry passengers; spill their lamp-oil cargo if sunk.
- **Travel parties**: migrants and founders physically walk (or sail as passengers).
- **Causality**: `shipment_lost` events; villages that relied on a ship's calls get an import tag.
- **Visual**: ships sailing between ports, docking, cargo on deck; columns of walkers.

### 3.11 Ecology agents (W4, `14_ECOLOGY.md`)
Herds (grazers) and packs (predators) as agents that feed on and move over the fields they are
coupled to; the fields `grazers`/`predators` become the herds' densities. Breeding pulses, winter
starvation, predation encounters, hunting by villagers. Visual: the actual herds.

### 3.12 Causality & detectors (`16_CAUSALITY_VISUALS.md`)
Existing detectors plus stock- and entity-level detectors (rationing, seed eaten, wood shortage,
housing shortage, labour shortage, shipment lost, boat lost, house built…).

## 4. The flow network

```
 sun/season ─► temperature ─► moisture/precip ─► soil water ─► vegetation ─► forest ─┐
      │             │              │                 │            │ grass           │ wood (logging)
      │             │              ▼                 ▼            ▼                 ▼
      │             │           rivers/floods ─► fields ◄── fertility ◄── fallow   wood stock ─► fuel ─► warmth ─► health
      │             │                            │  ▲  seed                          │
      ▼             ▼                            │  └── grain store ◄── harvest ◄────┤ timber
  daylight ─► work hours ─► LABOUR ─────────────►┼──► fishing ─► fresh stock        ▼
      ▲                       ▲                  │      ▲  fish biomass ◄─ plankton  construction ─► houses ─► households ─► births
      │                       │                  │      │                            │                              │
   sleep ◄── schedules    workers ◄── people ◄───┼── food ration ◄── stocks ◄── merchant ships ◄── surplus elsewhere  │
                              ▲         │        │                                                                 │
                              └─ births ┘ deaths ◄── nutrition/cold/crowding        migration ◄── push/pull ◄───────┘
                                                                                     │
  herds ◄──► grass/forest (grazing, browsing) ; packs ◄──► herds (predation) ; hunters ◄──► herds (meat)
```

Every arrow is a real read/write in code. Chains the player sees are paths through this graph.

## 5. Scheduler

| Rate | When | Work | Cost notes |
|---|---|---|---|
| hour | every `world.step` | commands; village day steps whose local midnight is this UTC hour; boat/ship/party arrivals and departures due this hour | O(villages in bucket + due movers) |
| village day | local midnight | settle the day that ended (production, consumption, spoilage, progress), plan the new day (tasks from standing assignments, weather, daylight; boat trips) | O(tasks) per village |
| week (planet) | hour % 168 == 0 | all planet systems as today (+ farming growth, herds), detectors | ≈ 515k VM instr (f12) |
| village week | village's staggered weekday | re-plan labour assignments, construction queue, field plan, trade intent; per-person nutrition/health/fertility/mortality | O(people) |
| month | village's staggered month week | courtship, household formation, migration decisions, memories | O(people) |
| year | hour % 8064 == 0 | year-boundary snap + save capture, ageing housekeeping, ruins, pruning | — |

Budget targets (HTML5 release): planet week ≤ 4.5 ms; society ≤ 1.5 ms per simulated week
amortised; ⏩ ≥ 1.5 simulated years per real second. P07 counts instructions for both layers.

## 6. Speeds (supersedes 04 §3 rates)

| Level | Rate | Meaning |
|---|---|---|
| ⏸ | 0 | frozen |
| ▶ Watch | 1 hour / 5 s | a day in 2 minutes: people wake, walk to work, boats go out |
| ▶▶ | 1 hour / 0.8 s | a day in 20 s |
| ▶▶▶ | 1 day / 3 s | weeks pass; daily rhythm still visible |
| ⏩ | 1 week / s | seasons in ~12 s, a year in ~48 s |
| ⏩⏩ | 16 weeks / s | a year in 3 s |
| ⏩⏩⏩ | budget-bound | ≥ 1.5 years / s |

Determinism is independent of speed (the sim steps whole hours). When the day lasts less than
~3 real seconds the view blends day/night lighting toward a steady daylight so it doesn't strobe.

## 7. Entity catalogue (persistent, saved)

| Entity | Where | Key (rng) | Notes |
|---|---|---|---|
| village | `W.ent.villages[id]` | founding cell + hour | stocks, fields, buildings, households, plan |
| household | `W.ent.households[id]` | village key + formation hour + index | home building, members |
| person | `W.ent.people[id]` | village key + birth hour + index | as today + task, nutrition, pregnancy |
| building | `W.ent.buildings[id]` | village key + lot + start hour | kind, lot, progress, condition |
| boat | `W.ent.boats[id]` | village key + build hour | fishing boats (persistent, real) |
| merchant ship | `W.ent.ships[id]` | route + launch hour | cargo, passengers, route, position |
| travel party | `W.ent.parties[id]` | origin + departure hour | migrants/founders en route |
| field | `W.S.farm[cell]` | — | sparse per farm cell |
| herd / pack (W4) | `W.ent.herds[id]` | cell + birth hour | animals, condition |

## 8. What changes for the player

- At ▶ Watch the world is a diorama at work: dawn lights the villages, smoke rises, people walk to
  their fields, boats leave the pier, a merchant ship docks and sacks are carried off, a house frame
  goes up, the sun sets and windows glow.
- At ⏩ the world is a system: fields green and gold with the seasons, clearings spread around
  growing villages, ships run their circuits, stocks rise and fall, villages grow or empty.
- Interventions hit something people depend on — a ship, a woodlot, a boat, a field, a herd — and
  the consequence travels the network.
