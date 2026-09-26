# 02 — World Model (the game's DNA)

This is the most important document. It defines what the planet *is*: the grid, the state each cell holds, the systems that change that state, and how they couple. Code in `sim/` implements this document. When the code and this document disagree, fix one of them and log the decision in DECISIONS.md.

Every constant mentioned here is a **starting value**. All of them live in `sim/config.lua` and are expected to be tuned. Never hard-code a tunable inside a system.

---

## 0. Principles (non-negotiable)

1. **Shared state, local rules.** Systems never reference each other directly. They read and write fields. Fire doesn't know about trees; it burns `fuel`, which vegetation produces. Villagers don't know about oil; they catch fish, and oil kills fish.
2. **No scripted consequences.** There is no code of the form "if tanker sinks then village dies". If a chain is desired and doesn't happen, fix couplings or constants, not by adding a special case.
3. **Tipping points come from thresholds with hysteresis.** Continuous equations give drift; thresholds (biome shifts, abandonment, era changes) give cascades. Every threshold has a dwell time and a different exit threshold, so states stick and don't flicker.
4. **Neighbour reads use last tick's values.** Any term that reads a neighbour (diffusion, advection, migration, fire spread) reads the previous-tick snapshot. Per-cell local terms may update in place in a fixed system order. This removes update-order artifacts.
5. **Determinism.** Same seed + same player command log = same world, bit for bit, on the same runtime. See 05_ARCHITECTURE.md (RNG).
6. **Every change can be blamed.** Every system reports the dominant driver of each significant change. See 03_CAUSALITY.md. Build the blame hooks at the same time as the system, not afterwards.
7. **Resilient baseline.** The untouched world must survive 200 years without collapse across 10 seeds (see §10 tuning bands). Collapse on a human timescale requires the player.

---

## 1. Time

- **1 tick = 1 week.** 1 month = 4 ticks. 1 season = 12 ticks. **1 year = 48 ticks** (12 months of 4 weeks). Displayed as "Year 12 · Late Spring".
- Seasons: tick-of-year 0–11 spring, 12–23 summer, 24–35 autumn, 36–47 winter (northern hemisphere; southern hemisphere is offset by half a year in the temperature formula; the label shows the northern season).

### Update rates (detailed mode)

| Every... | Systems |
|---|---|
| tick (week) | temperature, moisture/precipitation, soil water & rivers, ice, vegetation, fire, ocean life & oil, land animals |
| month (4 ticks, staggered by village) | people & villages |
| year (48 ticks) | global climate integration (CO₂, temperature offset, sea level), geology rolls, fertility slow terms, **year-boundary snap** (fields quantised to save precision, see 05_ARCHITECTURE.md §8) |
| on demand | precomputed tables: wind/current weights (per season), drainage order, coastline, neighbour tables — rebuilt when elevation or sea level changes |

### Time regimes

- **Detailed mode** (all normal speeds): the table above.
- **Epoch mode** (Chapter 3, deep time): 1 tick = 1 year. Weather is replaced by seasonal climatology (see §5.14). Target ≥ 50 years per real second (floor 40). Must agree with detailed mode on long-run aggregates (see 07_TESTING.md, epoch consistency).
- **Offline catch-up**: on load, simulate elapsed real time at **1 real hour = 1 world year**, capped at 1,000 years. Uses detailed mode for ≤ 20 years, epoch mode beyond (once Chapter 3 lands; before that, cap at 20 years).

---

## 2. Planet structure

### Grid

- The planet is an **icosphere graph**. Each vertex of a subdivided icosahedron is a sim cell with 5 or 6 neighbours (12 cells have 5). No poles, no seams.
- Level 4 = **2,562 cells** (target). Level 3 = 642 cells (fallback). `config.SIM_LEVEL` decides. Choose the largest level where one detailed tick costs **≤ 4 ms median in the HTML5 release build** on the dev machine. Decide provisionally in Stage 1.2, and finally after Stage 1.5 when the tick is realistic; log both in DECISIONS.md.
- Express spatial rates (diffusion, advection, migration, fire spread) per unit distance and scale them by cell spacing, so changing `SIM_LEVEL` doesn't throw away tuning.
- Render meshes are finer (level 5–6) and sample the sim grid; the sim resolution is invisible to the player except through coarse coastlines.

### Static(ish) per-cell data

| Field | Meaning |
|---|---|
| `pos` (x,y,z unit vector), `lat`, `lon` | location |
| `nbr[1..6]`, `nbr_count` | neighbour indices (fixed order) |
| `area` | relative cell area (≈ equal; use for area-weighted sums) |
| `elev` | elevation in "km" (−5 … +4). Changes only via geology and meteor impacts |
| `plate`, `boundary` | plate id; boundary type: none / convergent / divergent / transform |
| `continent` | connected-landmass id (recomputed when coastline changes) |
| `coast_dist` | graph distance to nearest coast (land and ocean sides) |
| `downhill` | drainage target (neighbour with lowest elev, or 0 for basin/ocean) |
| `wind_w[season]` | upwind neighbour weights for advection (precomputed per season) |
| `current_w` | ocean current weights (ocean cells; mass-conserving push weights, D-015) |
| `upwelling` | static nutrient bonus for some ocean cells (worldgen) |

A cell is **ocean** if `elev < sea_level`. That can change (sea level rise/fall, new islands).

### World generation requirements (Stage 1.3)

- 2–3 continents (each ≥ 12% of the surface) plus 4–8 islands. Land ≈ 30–40% of the surface. Reject and reseed until constraints hold; worldgen must be deterministic per seed.
- 6–10 tectonic plates (Voronoi on the sphere) with random motion vectors → boundary types. Mountains along convergent land boundaries; trenches at convergent ocean boundaries; ridges at divergent ocean boundaries.
- Volcano sites: 5–10 on convergent boundaries + 1–3 hotspots (at least one ocean hotspot able to build islands).
- Polar ice caps and at least one hot desert, one rainforest-like region, one temperate farming belt.
- 3–5 villages at good sites (fertile, water, temperate), 150–250 people total. At least one fishing village, one inland farming village, one on a different continent from the others.
- 1–2 tanker shipping lanes: closed loops through ocean cells passing near villages.
- Region names: every continent, sea, mountain range, big forest and village gets a generated name (see 06_PRESENTATION.md, naming).
- **Spin-up**: after generation, run 30 simulated years without recording events, so the world starts in a lived-in equilibrium rather than a painted one. That costs seconds, so: the **default and tutorial worlds ship pre-spun** as save files in `assets/worlds/` (instant start); new worlds spin up in chunks behind a "Forming continents…" progress screen.

---

## 3. Field catalogue (per cell, dynamic)

All fields are arrays indexed by cell (struct-of-arrays). Values are floats unless noted. "Causal" fields carry a blame tag array (`cause_<field>`), see 03_CAUSALITY.md.

| # | Field | Domain | Range / unit | Meaning | Causal |
|---|---|---|---|---|---|
| 1 | `temp` | all | °C | air temperature this tick | yes |
| 2 | `heat_anom` | all | °C | local heat anomaly (fire, player heat/chill, volcano), decays | yes |
| 3 | `sst` | ocean | °C | sea-surface temperature, lagged | yes |
| 4 | `moisture` | all | 0–~3 | atmospheric water | yes |
| 5 | `precip` | all | 0–~2 | rain/snow this tick (derived, stored for visuals and blame) | yes |
| 6 | `soil_water` | land | 0–1 | soil moisture | yes |
| 7 | `flow` | land | ≥ 0 | river discharge this tick (derived) | yes |
| 8 | `ice` | all | 0–1 | snow/ice cover (land ice sheet or sea ice) | yes |
| 9 | `grass` | land | 0–1 | grass/shrub biomass | yes |
| 10 | `forest` | land | 0–1 | tree biomass | yes |
| 11 | `crop` | farm cells | 0–1 | standing crop | yes |
| 12 | `fertility` | land | 0–1 | soil fertility | yes |
| 13 | `fire` | land | 0–1 | fire intensity (sparse: keep an active-fire list) | yes |
| 14 | `plankton` | ocean | 0–1 | base of the ocean food web | yes |
| 15 | `fish` | ocean | 0–1 | fish density | yes |
| 16 | `oil` | ocean (+shore) | 0–1 | oil contamination | yes |
| 17 | `grazers` | land | 0–1 | herbivore density (deer-like) | yes |
| 18 | `predators` | land | 0–1 | predator density (wolf-like) | yes |
| 19 | `land_use` | land | int | 0 wild, village id for farm cells, −id for village centre | no |
| 20 | `biome` | all | enum | classified biome (committed, with hysteresis) | via events |
| 21 | `biome_cand`, `biome_dwell` | all | enum, int | candidate biome and how long it has persisted | no |
| 22 | `burn_scar` | land | ticks | ticks since last fire (0 = never / expired) | no |
| 23 | `stress` | boundary cells | 0–1 | tectonic stress (Chapter 3) | yes |
| 24 | `silt` | all | ≥ 0 | sediment carried this tick (derived; river mouths) | yes |

That's the full budget. **Adding a field requires a DECISIONS.md entry** explaining why an existing field can't carry it.

### Global state

| Field | Meaning |
|---|---|
| `tick`, `year`, `tick_of_year` | clock |
| `co2` | atmospheric CO₂ (baseline 280, flavour units "ppm") |
| `aerosol` | volcanic/impact dust (0 baseline) |
| `g_temp` | global temperature offset °C (lagged) |
| `ice_fraction`, `ice_volume` | area-weighted ice cover / volume proxy |
| `sea_level` | km, baseline 0 |
| `era` | Garden / Warm Age / Hothouse / Ice Age / Snowball / Dead World |
| global cause ledgers | top contributors to `co2`, `aerosol`, `g_temp` (see 03_CAUSALITY.md) |

### Entities (not per-cell)

- **Villages** and **people** (§5.10), **tankers** and **boats** (§5.11), **volcanoes** (§5.12), active **storms** (optional, §5.3), **events** (03_CAUSALITY.md).

---

## 4. System order within a tick

1. Global climate (only on year boundary; otherwise use current globals)
2. Temperature (+ sst, heat_anom decay)
3. Moisture: evaporation → advection → precipitation
4. Ice & snow
5. Hydrology: soil water, runoff, rivers, floods, silt
6. Vegetation & soil
7. Fire (ignite → spread → burn)
8. Ocean life & oil
9. Land animals
10. People & villages (the ¼ of villages whose turn it is this tick)
11. Tankers & boats
12. Geology (Chapter 3; rolls on year boundary, resolves events immediately)
13. Biome candidate update; commit on dwell
14. Event detection & blame commit (03_CAUSALITY.md)

---

## 5. Systems

Equations are *shapes*, not final numbers. `k_*` are config constants. `clamp01` clamps to [0,1].

### 5.1 Temperature

```
temp = T_base(lat) + season(lat, tick_of_year) * amp(lat, is_ocean)
     + lapse * max(elev, 0) + g_temp + heat_anom + ocean_moderation
     − k_aer * aerosol                          -- fast volcanic/impact cooling, no lag
T_base(lat)  ≈ 28 − 42 * sin²(lat)          -- equator ~28, poles ~−14
amp(lat)     ≈ 12 * |sin(lat)| (land), × 0.4 over ocean
season(...)  = sin(2π·tick_of_year/48 + phase), phase flipped in the south
lapse        ≈ −6.5 per km
heat_anom   *= 0.8 per tick on land, 0.98 over ocean
sst         += (temp_target − sst) * 0.1       -- ocean lags the air
```

Coastal land cells get `ocean_moderation` that damps seasonal swing by 30%.

### 5.2 Wind & currents (precomputed)

- Prevailing wind by latitude band: |lat| < 30° easterly trades blowing toward the equator; 30–60° westerlies; > 60° polar easterlies. Bands shift ±5° with season (follow the sun).
- Precompute, per season, each cell's upwind neighbours and weights (semi-Lagrangian advection on the graph). 4 tables. Add a small slow wobble (a low-frequency noise over years) so weather isn't identical every year.
- Ocean currents: follow the wind over ocean, deflected along coastlines (remove the component pointing into land, renormalise). Produces gyres. Recompute when the coastline changes.

### 5.3 Moisture & precipitation

```
evap_ocean = k_eo * clamp(exp(0.07 * (sst − 15)), 0.1, 4) * (1 − ice)
evap_land  = k_el * soil_water * (0.3 + 0.7 * veg_cover)     -- transpiration: forests recycle rain
moisture   = Σ upwind_w * moisture_prev[upwind] + evap − precip   (+ tiny diffusion)
capacity   = k_cap * exp(0.06 * temp) * (1 − k_oro * max(0, elev − elev_upwind))
precip     = max(0, moisture − capacity) * k_rain + moisture * k_drizzle
```

- Precipitation falls as **snow** when `temp < 0`.
- Result we need to see: wet windward coasts, rain shadows behind mountains, dry continental interiors and subtropical deserts, wet equator. Heating an ocean patch must measurably change rain downwind (scenario test).
- **Storms (optional stretch — build only if Chapter 3 finishes early; nothing depends on them)**: when a warm ocean cell (sst > 27) has moisture > threshold in summer, spawn a storm entity that drifts with the wind for a few weeks, dumping heavy rain (floods) and damaging coasts. Storms are events (blameable: "warm sea" → storm).

### 5.4 Ice & snow

- Land: `ice += snowfall − melt`, melt ∝ max(0, temp) · k_melt. Ice ≥ 0.8 persisting over a year = ice sheet.
- Sea ice: forms when sst < −1.8 (grows), melts above. Sea ice with `ice ≥ 0.8` is walkable by land animals (ice bridges in ice ages).
- `ice_fraction` = area-weighted mean of `ice`; `ice_volume` = Σ land ice (sea level proxy).

### 5.5 Hydrology

```
soil_water += rain + melt − evap_land − percolation − runoff
runoff      = max(0, soil_water − field_capacity) * k_run + rain_excess
flow[c]     = runoff[c] + Σ flow[uphill neighbours]    -- one pass in drainage order
```

- Cells with `flow` above `river_threshold` are rivers (visual + fishing access + village siting).
- Closed basins accumulate a lake (visual; simple: lake cell = land with standing water; treat like ocean for fishing, not for currents).
- **Flood**: `flow > bankfull * 1.5` → flood event on that cell and low neighbours: crops destroyed, houses damaged, fertility +0.05 (silt), people may die (low probability).
- **Erosion & silt**: `erosion = precip * slope * (1 − veg_cover) * k_ero`; fertility −= erosion; silt travels with flow to the river mouth. Ocean cells at river mouths get nutrients (+plankton) but high silt hurts fish spawning.

### 5.6 Vegetation & soil

```
S_grass  = bell(temp, 5..30) * sat(soil_water) * (0.4 + 0.6 fertility) * light(season, lat)
S_forest = bell(temp, 5..28) * sat(soil_water, needs more) * (0.3 + 0.7 fertility)
           * (1 − recent_fire) * min(1, grass / 0.3)      -- forests establish on grassland

grass  += r_g * S_grass * grass * (1 − (grass + 0.5 forest)/K) − grazing − burn − oil_damage + seed_in
forest += r_f * S_forest * forest * (1 − forest/K_f) − burn − clearing − browsing + seed_in
fertility += k_regen * (base_fert(biome) − fertility) − crop_depletion − erosion + ash_bonus
```

- `seed_in`: tiny recolonisation from neighbours that have that vegetation (no spontaneous generation).
- `browsing`: grazers eat saplings — strongest when forest is low, and hungry grazers (near their food limit, e.g. with no predators) strip far more (this is what makes predator removal stop forest recovery; D-017).
- Forest growth is slow: from bare land to mature forest ≈ 30–60 years. Grass recovers in 1–3 years.
- `veg_cover = clamp01(grass + forest)`; `fuel = (0.5 grass + forest) * dryness`, `dryness = 1 − soil_water / field_capacity`.

### 5.7 Fire

- Keep a sparse **active-fire list**; never loop over all cells for spread.
- Ignition sources: natural lightning (∝ summer storminess × dryness × fuel, rare), village accidents (∝ population × dryness, very rare), player Lightning, lava, meteors.
- Spread per burning cell to each neighbour: `p = k_spread * fuel[n] * dryness[n] * intensity * (1 + wind_alignment)`. Heavy rain extinguishes.
- Burning consumes grass fast, forest slower; intensity ∝ fuel; burns out below minimum fuel.
- Effects: grazers/predators flee (migration pulse) with some mortality; crops destroyed; village houses damaged, people may die (they mostly flee); `co2 += burned_biomass * k_co2`; `heat_anom += k`; `burn_scar` set; ash fertility bonus after the fire.
- Target behaviour: small natural fires several times per decade; big fires in dry years; forests recover over decades.

### 5.8 Ocean life & oil

```
plankton += r_p * light * nutrients * bell(sst, 10..26, collapses > 30) * (1 − k_oil_p * oil) * (1 − ice)
            * plankton * (1 − plankton) − fish_grazing
fish     += r_fish * fish * (plankton/(plankton + h)) * (1 − fish) − catch − k_oil_f * oil * fish
            − heat_stress − silt_penalty + migration
oil       = advect(currents) + diffuse − decay            -- half-life ~ 2 years
```

- `nutrients` = static coastal/upwelling factor + river-mouth pulses.
- Oil reaching a coastal ocean cell "beaches": damages the adjacent land cell's grass and fertility, and poisons shore fishing for months.
- Marine heatwaves (sst > 30) collapse plankton → fish crash (climate chain).

### 5.9 Land animals (grazers, predators)

```
food_g    = grass + 0.5 * crop (crop raids anger farmers, see villages)
grazers  += r_h * grazers * food_g/(food_g + h_g) * (1 − grazers/K_g) − predation − hunt − disaster − winter_starve
predation = a * P * max(0, H − H_refuge) / (1 + a * t_h * max(0, H − H_refuge))   -- Holling II with prey refuge
predators += e * predation − m * predators − culled_by_villagers
```

- **Migration** each tick (implemented multi-rate, D-012): a fraction moves to neighbours weighted by attractiveness (grazers: food, few predators; predators: prey). Flows carry blame tags.
- Animals return the carbon they eat (CO₂), keeping the untouched biosphere carbon-neutral (D-008, D-017).
- Animals cannot cross ocean. They **can** cross sea ice with `ice ≥ 0.8` (ice-age land bridges) and newly exposed land (sea level drops).
- **Villagers cull predators** near farms with some probability (emergent loop: villages expand → wolves culled → deer boom → crops raided).
- Species presence is per continent through connectivity; **global extinction is permanent** unless the player reintroduces the species.

### 5.10 People & villages (monthly, staggered)

**Village**: id, name, centre cell, member ids, `food` store (person-months), farm cells, fishing access (ocean/lake cells within 2 steps, or river with flow ≥ threshold), boats, houses (visual, ≈ population/4), founded tick, recent-event memory (event ids), `food_security` (smoothed months of food).

**Person**: id, name, sex, birth tick, village id, job (child / farmer / fisher / woodcutter / elder), health 0–100, hunger 0–100, partner id, parent ids, children count, 1–2 traits (e.g. optimist, doomsayer, hoarder, devout, skeptic, romantic, grump), memories (≤ 6 event ids with salience), mood (derived).

Monthly per village:

1. **Land use**: keep `farm_cells ≈ ceil(farmers * k_farm)` best-suited cells within 2 rings of the centre. Claiming a forest cell clears it (wood → `co2`).
2. **Crops**: planted in spring on farm cells, grow with temperature 10–30 °C, soil water and fertility; harvested at tick-of-year ≈ 32 → `food += crop * yield`. Damaged by fire, flood, drought, frost, blight, grazer raids, oil (coastal). Harvest depletes fertility; low fertility → fallow.
   - **Blight** (player power): a sparse set of infected farm cells (like the active-fire list). Infected crops die back; infection spreads to adjacent farm cells and, via trade, to villages within 6 cells with a monthly probability; it burns out after two winters.
3. **Fishing**: `catch = min(Σ fish in access cells * q, fishers * max_catch)`; fish reduced by the catch (overfishing is possible and emergent).
4. **Wood burning**: `co2 += population * k_burn * (1 + winter_cold)`; forest near the village slowly depleted. This is the slow, built-in human climate driver.
5. **Consumption**: adults eat 1 food/month, children 0.6. If the store runs out, hunger rises for everyone; health falls with hunger; mortality rises.
6. **Life events**: aging (on each birthday), pairing (unpartnered adults 18–45, probability per month), births (partnered women 18–40, probability × f(food_security) × f(crowding)), deaths (age hazard after ~50, hunger, disasters, fire/flood/quake/lava/tsunami exposure), job assignment at 14.
7. **Migration**: if `food_security < 1` month for 6 consecutive months, or the village is destroyed, families leave for the nearest village with food security > 3 months (same continent, or coastal-to-coastal by boat); if none, a group of ≥ 4 founds a new village at the best free site; smaller groups become wanderers (and usually die, which the chronicle will mention).
8. **Abandonment**: population 0 → "village abandoned" event; houses become ruins (visual, decay over ~20 years); farm cells revert to wild and succession begins.

Population: soft carrying capacity per village from farms + fishing; global hard cap 500 (births blocked at cap; log when it happens). Target range 120–400.

Staggering: village `i` updates on ticks where `(tick + i) % 4 == 0`, so each village updates once per month and the cost is spread.

### 5.11 Tankers & boats

- Boats: visual fishing boats near fishing villages (count ∝ fishers). They are also targets (Lightning kills the fisher, small oil).
- Tankers: 1–2 per lane, moving along a precomputed loop of ocean cells. Cargo: lamp oil. Sinking one (Lightning, Meteor, or the Oil Spill power) releases a large oil pulse at its position. A replacement tanker appears a few years later if ≥ 2 villages exist ("The Tanker Company regrets nothing").

### 5.12 Geology (Chapter 3)

- **Plates never move.** Continental drift would require remeshing; out of scope. Landscape changes come from volcanic islands, sea-level change, meteor craters and very slow uplift.
- **Earthquakes**: boundary cells accumulate `stress` (rate by boundary type). Release when stress exceeds a noisy threshold → magnitude ∝ stress. Effects in a radius: houses destroyed, people killed, landslides on steep cells (vegetation loss, silt).
- **Volcanoes**: each has `pressure` and a dormancy period. Eruption probability per year = sigmoid((pressure − p0)/w). Magnitude VEI 1–6 drawn from a steep distribution. VEI≥5 ≈ once per 300–800 years planet-wide, VEI 6 ≈ once per 3,000–10,000 years.
  - Effects: lava/fire in radius (scales with VEI), deaths, destroyed houses; ash → fertility bonus after ~5 years; `aerosol += f(VEI)` (VEI 5: ~1–2 °C cooling for 2–3 years; VEI 6: 3–6 °C for 3–8 years); small `co2`.
  - Ocean volcanoes raise `elev` of their cell (and sometimes neighbours). Crossing sea level creates a new island (headline event) and triggers coastline/current/drainage rebuild.
- **Tsunami**: a quake of magnitude ≥ threshold in or next to the ocean, or a meteor ocean impact. The sim resolves it **instantly within the tick**: breadth-first distance over ocean cells from the epicentre; coastal land cells within range take damage ∝ magnitude/(1 + distance): crops and houses destroyed, people killed (with a warning chance), boats lost, fertility down (salt). The view plays the wave animation over 3–5 real seconds using the event's epicentre and per-cell arrival order.
- **Uplift** (cut-able): convergent boundary cells gain ~0.001 km/year; erosion removes. Only visible in epoch mode.
- **Meteor** (player power; natural ones extremely rare): crater (elevation dip, may become a lake), fire radius, deaths, `aerosol` impact winter scaled by size, tsunami if ocean.

### 5.13 Global climate (yearly)

```
d(co2)/yr = volcanic_outgassing + fires + village_burning + eruptions
            − forest_uptake(net forest growth) − ocean_uptake(k_o * (co2 − eq(sst_mean)))
            − weathering(k_w * Σ_land rain * exp(0.07 * temp) * (1 − ice))
aerosol   decays every tick with an e-folding time of ~1.5 years (+ eruptions, impacts)
g_target  = S * log2(co2/280) + k_alb * (ice_ref − ice_fraction) + vapor_feedback
g_temp   += (g_target − g_temp) / τ,  τ ≈ 10 years     -- slow terms only
-- aerosol cools directly through the temperature formula (§5.1), so a VEI 6 bites within a year;
-- the extra ice it creates then feeds the slow albedo term.
-- In Lua 5.1, log2(x) = math.log(x) / math.log(2)  (math.log takes one argument)
sea_level = k_ice * (ice_volume_ref − ice_volume) + k_th * g_temp
```

- Tune so the **no-human** world sits at co2 ≈ 280 in equilibrium, and a normal human world drifts up by roughly +1.5 to +3 °C over 150–300 years if left alone (the slow built-in climate story).
- **Ice–albedo feedback** is real and can run away: past a threshold ice fraction the planet slides into an ice age; further, into a **Snowball**. Weathering stops under ice, so volcanic CO₂ accumulates and eventually ends the snowball (target 2,000–20,000 years).
- **Hothouse**: high CO₂ + vapor feedback; ice gone, seas rise, deserts spread, fires rampant. Weathering (hot + wet) slowly pulls CO₂ down (target 1,000–10,000 years).
- Guardrails: clamp `g_temp` to [−30, +20], `co2` to [50, 5000].
- **Sea level changes**: when `sea_level` moves by ≥ 0.05 km since the last rebuild, reclassify ocean/land, rebuild coastline tables, and emit flooding/exposure events (villages on newly flooded cells relocate or drown; land bridges appear when the sea drops).
- **Eras** (headline events on change): Garden (normal), Warm Age (g_temp ≥ +2), Hothouse (≥ +6), Ice Age (ice_fraction ≥ 0.35), Snowball (≥ 0.85), Dead World (no land vegetation and no animals).

### 5.14 Epoch mode (Chapter 3)

- 1 tick = 1 year. Temperature/precipitation come from a **climatology**: per-season averages computed by running the moisture model in steady state (a few relaxation passes per season) rather than week by week.
- Vegetation, animals, fish integrate yearly with 4 seasonal substeps (large timesteps destabilise predator–prey).
- People stay individuals; life events use yearly probabilities.
- Fire, storms, quakes, eruptions: rolled as yearly probabilities and resolved as events.
- Must agree with detailed mode (07_TESTING.md P05): over 200 years from the same seed, forest cover and desert within ±5 percentage points, ice fraction within ±3 points, g_temp within ±0.5 °C, population within ±25%.

### 5.15 Biomes

Candidate biome each tick by priority:

1. `ocean` (or `sea_ice` if ice ≥ 0.5)
2. `ice_sheet` (land ice ≥ 0.8)
3. `village` / `farmland` / `ruins` (land use)
4. `ash` (burn_scar recent or lava)
5. `marsh` (soil_water ≥ 0.85 and elev low)
6. `desert` (soil_water < 0.15 and veg_cover < 0.15, temp > 5)
7. `tundra` (temp mean < 0 and veg_cover < 0.4)
8. `jungle` (forest ≥ 0.6 and temp ≥ 22 and wet)
9. `forest` (forest ≥ 0.5)
10. `grassland` (grass ≥ 0.25)
11. `barren` (otherwise)

Commit a change only when the candidate persists for its dwell time (default 12 ticks; desert and ice sheet 48). Exit thresholds differ from entry thresholds (hysteresis, e.g. desert entry at veg < 0.15, exit only at veg > 0.3). Committed biome changes emit events with blame.

---

## 6. Coupling matrix

Reads (→) and writes (←). This is how chains travel.

| System | Reads | Writes |
|---|---|---|
| Temperature | lat, elev, season, g_temp, heat_anom | temp, sst |
| Moisture | sst, temp, soil_water, veg_cover, elev, ice, wind | moisture, precip |
| Ice | temp, precip, sst | ice, ice_fraction, ice_volume |
| Hydrology | precip, temp, veg_cover, elev | soil_water, flow, silt, fertility (erosion), floods |
| Vegetation | temp, soil_water, fertility, season, grazers, fire, oil, land_use | grass, forest, fertility |
| Fire | fuel (grass, forest, soil_water), wind, lightning, villages | fire, grass, forest, crop, grazers, predators, co2, heat_anom, houses, people |
| Ocean life | sst, nutrients, silt, oil, ice, catch | plankton, fish |
| Oil | currents, tanker events | oil, grass/fertility at shore |
| Animals | grass, crop, forest, ice bridges, fire, villages | grazers, predators, grass, forest (browsing), crop (raids) |
| Villages | crop, fish, grazers, predators, soil, temp, floods, fires, quakes, sea level | crop, fish, forest (clearing), land_use, co2, predators (culls), people |
| Geology | stress, pressure, sea level | elev, fire, aerosol, co2, fertility, houses, people, tsunami |
| Global climate | co2 sources/sinks, aerosol, ice | g_temp, sea_level, era |

---

## 7. Chains we expect (tuning targets, not promises)

These are the stories the couplings are designed to allow. Each one is also a scenario test in 07_TESTING.md. If the sim can't produce them, tune couplings; never script them.

1. **Wolves culled** → grazers boom → grass and saplings eaten → forest can't regrow, erosion → silt in rivers → fish decline at river mouths → fishing village food falls.
2. **Farm fire at harvest** → crop loss → famine → deaths/migration → village abandoned → farmland reverts → forest returns over decades.
3. **Tanker sunk** → oil drifts with the current → plankton and fish die → fishing catch collapses → coastal famine; beached oil kills coastal grass.
4. **Ocean heated upwind** → more evaporation → more rain downwind (floods) → a rain-shadow region elsewhere gets drier → crops fail there.
5. **Drought season** → dry fuel → wildfire → CO₂ and ash → later fertility boost → grass boom → grazer boom.
6. **Grazers introduced to a predator-free island** → boom → vegetation collapse → desert that stays desert (hysteresis).
7. **Supervolcano** → aerosols → cold years → crop failures on every continent → famines; if the planet is near the threshold: ice age → sea level drops → land bridge → predators reach the island.
8. **Human growth (baseline, no player)** → forest clearing and wood burning → CO₂ up → warming → ice melts → sea level rises → a coastal village floods.
9. **Meteor into the ocean** → tsunami → coastal villages destroyed + impact winter.
10. **Blight on one farm** → spreads farm to farm and village to village → continental famine.
11. **Chill a sea** → sea ice → fishing access lost → food drops; animals walk across the ice to new land.
12. **Burn the forest upwind of mountains** → less transpiration → less rain downwind → slow drying of the next region.

---

## 8. Stability toolkit

Use these, in this order, when the world is too volatile or too static:

- **Prey refuge** in predation (§5.9) — stops predators eating prey to zero.
- **Recolonisation from neighbours** (`seed_in`, migration) — lets life return; never spawn life from nothing.
- **Carrying capacities and caps** — logistic terms everywhere biomass grows.
- **Damping/lag** — sst, g_temp, food_security are smoothed.
- **Hysteresis + dwell** — on every threshold.
- **Staggering** — avoids all villages reacting on the same tick.
- **Spin-up** — start from equilibrium, not from painted initial values.
- **Noise with memory** — weather wobble over years so the baseline isn't a perfect loop.

If a fix needs a special case ("if predators < x then add predators"), stop and rethink the coupling instead. Log it in DECISIONS.md if you must.

---

## 9. Long-horizon behaviour

- The baseline slowly drifts (human CO₂). Over centuries, without the player, the planet may warm or (via volcanism and ice feedback) cool into an ice age. This is intended: the "come back later and see a changed world" fantasy.
- Humans can go extinct. Then the chronicle notes it, the world keeps running, and the player can use Introduce to seed new settlers (a small group arriving by boat, see 04_PLAYER.md).
- Life never fully dies unless the player really tries; plankton survive in refuges (warm/cold ocean pockets) and recolonise. A Dead World era is possible but should require extreme action (e.g. repeated meteors, runaway hothouse).

---

## 10. Tuning bands (baseline, untouched, 10 seeds × 200 years)

The headless suite asserts these (07_TESTING.md). Adjust bands only with a DECISIONS.md entry.

| Metric | Band |
|---|---|
| Forest cover (fraction of land) | 20–55%, and must vary by ≥ 5 percentage points over the run ("alive") |
| Grassland | 20–50% of land |
| Desert | 5–25% of land |
| Ice fraction (global) | 5–20% |
| g_temp | within ±1.5 °C of start, plus human drift of 0 to +2 °C by year 200 |
| Population | 120–400 at all times after year 5 |
| Village turnover | ≥ 1 village founded or abandoned by year 200 in ≥ 5 of 10 seeds |
| Grazers & predators | no continental extinction in ≥ 9 of 10 seeds; predator coefficient of variation ≥ 0.15 (they oscillate) |
| Fish | no ocean-wide collapse; local overfishing events allowed |
| Fires | ≥ 3 per decade planet-wide; a large one (≥ 10 cells) at least once per 50 years |
| Headline natural events | ≥ 1 per 5 years on average |
| Eruptions (Chapter 3) | VEI ≥ 3 roughly once per 50–150 years |

---

## 11. Recipe: adding a new element

1. Write down which **existing fields** it reads and writes. If it needs a new field, justify it in DECISIONS.md.
2. Add its constants to `sim/config.lua`.
3. Implement its update inside the correct system slot (§4), respecting neighbour-reads-previous-tick.
4. Add blame hooks for every field it writes (03_CAUSALITY.md).
5. Add or extend a scenario test proving at least one chain passes through it.
6. Run the baseline stability suite; it must stay green.
