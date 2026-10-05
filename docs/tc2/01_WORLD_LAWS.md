# TC2-01 — World laws: the physics, biology and time of a tiny restless planet

The world is fictional. Its laws are chosen so that (1) one moment can be watched at human scale,
(2) a player's evenings and absences span centuries of visible change, and (3) every law is internally
consistent and simulated, never narrated. All numbers live in `TC2.Core` configuration (`WorldLaws`)
and are tuning values unless marked **fixed** (changing a fixed law needs a DECISIONS entry).

## 1. Time (fixed)

| Unit | Value | Notes |
|---|---|---|
| Simulation tick | **1 second**, `long` | all sim time is integer seconds since world creation |
| Day | 24 h = 86,400 s | sleep, work and light follow the sun at each place |
| Season | 12 days | spring, summer, autumn, winter (opposite in the south) |
| Year | **48 days** = 4,147,200 s | TC1's calibrated rhythm: a crop in ~3 weeks of days, seasons watchable |
| Human generation | ~22 years | maturity 15, elder 55 |

A world date reads "Year 183 · Late Summer · Day 31 · 14:05". Year 0 is the world's creation (spin-up
history before people may span thousands of years; the calendar people use may start later).

## 2. Space

| Quantity | Value | Notes |
|---|---|---|
| Planet radius | **10 km** (sim metres) | surface 1,257 km²; land ~30-40% |
| Surface grid | cubed sphere N64 (24,576 cells, ~245 m) | ecology, hydrology, soils, land use |
| Climate grid | N32 (6,144 cells, ~490 m) | temperature, moisture, wind, cloud |
| Relief | ocean floor to -1,500 m, peaks to +2,500 m | vertically exaggerated rendering is presentation |
| Walking | 1.25 m/s adult on open ground, slower uphill, loaded, old, young, at night | ~4.5 km/h |
| Village footprint | 100-300 m houses; fields to ~600 m | 1-2 surface cells for the core, 4-20 for fields |
| Neighbouring villages | 1-4 km (20 min - 1 h walk) | the world is small, but people mostly stay home |
| Continent | 10-30 km | a few hours to a day's walk |
| Circumference | 63 km | sea travel (boats ~2 m/s) or very long journeys |

At saturation of pre-industrial farming (~40-60 people/km² of land) the planet supports **~20,000
people**; typical histories hold 500-5,000. Presentation exaggerates objects when zoomed out.

## 3. Sky and climate

- Axial tilt 23° (varies 18-28° with the obliquity cycle); the sun's position follows the day of year.
- **Orbital cycles** (fictional, fast): precession ~600 years, obliquity ~1,300 years, eccentricity
  ~3,100 years. Their combination paces glacial and warm periods within playable history.
- Energy balance per cell: insolation x (1 - albedo) against outgoing `A + B·T` reduced by a CO₂
  greenhouse term; ocean heat capacity ~5-year memory; ice and snow albedo feedback (runaway possible).
- CO₂ from volcanoes, fires, burning and clearing; removed by forests and by weathering (faster when warm
  and wet: the thermostat that ends hothouses and snowballs over millennia).
- Aerosols from eruptions and impacts cool within weeks and fade over ~2 years.
- Sea level follows land ice volume and warmth: about **±40 m** between deep glacial and hothouse states,
  enough to open land bridges and drown coastal plains.
- Weather: daily stochastic wet/dry spells and storms from the climate state; storms are entities that
  travel.

## 4. Ground

- **Plates** rotate about poles at **~0.5-2 m/year** (one surface cell per one to five centuries): coasts
  and ranges visibly move over millennia. Collisions uplift up to ~1 m/year; subduction makes trenches
  and volcanic arcs; rifts open new seas; hotspots build island chains.
- **Quakes** release boundary stress: damaging ones near active boundaries every few decades.
- **Volcanoes** gather pressure; eruptions every 50-300 years per active vent; planet-wide a VEI 5+
  every few centuries, VEI 6+ every one to three millennia (a volcanic winter).
- **Erosion**: rivers incise by the stream power law; hillslopes creep; sediment fills valleys and
  builds deltas; fresh rock weathers to soil over decades.

## 5. Life

| Law | Value |
|---|---|
| Grass recovery after fire | 1-2 years |
| Shrubland | 5-10 years |
| Forest canopy on abandoned land | 30-60 years; old growth ~150 years |
| Small grazers (hare-like) | generation 1 year, life 3-5 |
| Large grazers (deer/goat-like) | generation 3 years, life 10-15 |
| Predators (wolf-like) | generation 3 years, life 8-12 |
| Fish | generation 2 years |
| Heritability of traits | 0.3-0.5; mutation variance large (fast evolution is a world law) |
| Visible adaptation | trait means shift by ~1 SD in 50-150 years under steady selection |
| Speciation | populations isolated for ~500-1,500 years diverge past the species threshold |
| Extinction | permanent unless the same lineage survives elsewhere |

## 6. People

| Law | Value |
|---|---|
| Stages | infant < 2, child 2-11, youth 12-15, adult 16-54, elder 55+ |
| Fertility | women 16-44; pregnancy 36 days (3/4 year); nursing ~1.5 years suppresses conception |
| Mortality | high infant hazard; Gompertz old age; hunger, cold, injury, disease, violence |
| Food | ~1 food unit (FU) per adult-day (children 0.6, elders 0.8) |
| Work year | daylight-bound; seasonal peaks at sowing and harvest |
| Skill learning | months to years of practice beside a teacher; proficiency decays without use |
| Knowledge | carried by people (and later by records); lost when no one living holds it |
| Disease | incubation days, infectious days to weeks, immunity years; pathogens mutate |
| Buildings | timber 30-80 years without repair, stone 300+ years; ruins decay over centuries |

## 7. What these laws imply (sanity targets, measured not promised)

- 100 years: villages founded and abandoned, forests shifted, a famine or epidemic somewhere, a few
  generations, knowledge spread between neighbours, a polity possible.
- 1,000 years: a glacial swing, a coastline changed, a volcanic winter, species shifted or new, cities
  possible, ruins reused, collapse possible, writing or iron possible (never guaranteed).
- 10,000 years: continents visibly drifted, mountain ranges risen and worn, whole biotas replaced.
