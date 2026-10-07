# TC2-19 — The planet's size (D2-085): a scale study, and the decision

Arjun, 7 Oct 2026: "the world feels enormous and empty; settlements are visually insignificant; rivers barely read; people and
animals are microscopic; historical change occupies too little of the visible world." Before the world-readability pass (D2-084,
`18_WORLD_READABILITY.md`) is spent on a 10 km planet, D2-007's radius is reopened with evidence. This document is the study:
what TC1 actually was, what TC2 is, what smaller TC2 planets do when they are really simulated, and the decision.

## 1. TC1's actual scale (read from the code: `sim/config.lua`, `game/view/camera.lua`, `game/view/state.lua`, `docs/v3/`)

TC1 had no physical units. Its planet is a sphere of **radius 10 world units** carrying a geodesic grid of frequency 12 (**1,442
cells**, `C.SIM_LEVEL = 12`), a cell ~1.0 unit across. A village lives on a local map of 128 x 128 tiles of 0.025 units - **3.2 units
square, the village cell and its neighbours** (`C.V3.map_n`, `tile`). Read against its own objects the units mean different things:

| TC1 quantity | value (world units) | what 1 unit is "worth" if that object is real |
|---|---|---|
| a person (`scale 0.04`, 20_CHARACTER_PIPELINE) | 0.073 tall | 23 m (R = 230 m) |
| a house (4 x 4 tiles) | 0.1 across | 80-100 m |
| a field plot (12 x 12 tiles, ~162 FU, ~1.25 ha at TC2's yields) | 0.3 across | 370 m |
| walking (`walk = 3.2` units an hour) | a cell in ~19 min | 1.4 km (R = 14 km) |
| relief (0.13 units a km before D-112) | a 2.3 km range = 0.3 | 7.7 km vertical |

So TC1 was a **diorama, not a scaled world**: its people were drawn ~40x larger against the planet than a consistent scale allows,
houses ~10x, and a village's map - 3.2 units, **0.32 of the radius, 0.8 % of the whole surface** - was a large, plainly visible patch of
the globe. Walking round the world took ~20 hours of game time. Villages: worldgen 3-5 villages of 150-250 people in all
(`C.SETTLE`), at most 30 villages and a soft cap of 2,400 people (`C.DETECT.max_villages`, `soft_cap`), new villages at least 3 cell
spacings apart (0.28 R); contacts reached far: trade and brides 12 cell spacings (~1.1 R), relief 16 (~1.5 R). The camera (vertical
field of view 30 deg): the whole planet at 58 units (~65 % of the frame's height), villagers readable at 4.6 units (a person ~3 % of
the frame's height, ~32 px at 1080p; the village map about fills the frame), the closest view at 1.5 units (a person ~9 %, ~98 px).

**What made TC1 feel like a miniature** was therefore three things, only one of them a size: (1) a village with its fields was a large
share of the planet (a third of its radius) - few, big, legible units; (2) people and houses were drawn huge against the world; (3) the
near views came close (a person 32-98 px tall). TC1's limits (1,442 cells, no physical consistency, 30 villages at most) are not to be
ported; (1) is what a planet's size can buy, (2)-(3) are presentation and camera.

## 2. TC2 at R = 10 km (D2-007)

N64 cubed sphere (24,576 cells, mean spacing 228 m, 5.1 ha a cell), surface 1,257 km2, land ~36 % (451 km2 on seed 42), a person
1.7 m drawn at true size near the camera (exaggerated up to 2.5x for houses beyond 900 m). Villages keep 1.9 km apart, fields lie
within 950 m, a day's walk for visits, trade, marriage, relief, taking and raids is 6 km; walking round the world takes ~14 hours.
Mature worlds (400 years, D2-083 round j): 5,000-20,000 people in 18-57 villages of ~270-400 people. Against the planet: a person is
R/5,900 (TC1: R/137), a village's houses (100-300 m) 0.01-0.03 R, a village with its fields (~2 km) 0.2 R across (TC1's map: 0.32 R). The
camera (vertical field of view 45 deg): orbit at 22 km (2.2 R), region 5 km, village 900 m (a person ~2 px of 900), close 260 m (~7 px),
low 95 m (~20 px).

**The people-scale finding.** A person's size on screen depends only on how near the camera comes, not on the planet: TC2's near views
show a person at 2-20 px where TC1's showed 32-98 px. No radius fixes "people and animals are microscopic"; that is the camera and
presentation (D2-084's near views and readable exaggeration). What a radius changes is the share of the planet a village, a wood, a
river corridor, a burn or a kingdom covers - the granularity of the world and of its history.

## 3. What a smaller radius touches: the laws audited

Changing `WorldConfig.Radius` alone is not a smaller world: some laws were in metres and hectares and scale by themselves, some were
tuned per cell on the 10 km world and silently change, some count cells as if a cell were 228 m. Every scale-dependent law was read
(`grep` of `Radius`, `CellSize`, `AreaHa`, `ArcDistance`, `Nbr4/Nbr8` and absolute amounts in Core) and given one of three rules:

**Rule G - the ground is the same ground at any size.** The planet's geophysics was tuned in cells (01 §4: plates "one surface cell per
one to five centuries"; erosion balanced on 245 m cells, D2-043; ice "on so small a world", D2-043; storms of 1.8-3.4 km; "a small world
feels its quakes across much of itself"). Those laws keep their behaviour per cell: inside them lengths are **geo-metres**, a 10 km
sphere's (`PlanetState.GeoRadius`, `GeoAreaHa`, `GeoCellSize`); heights stay metres (the lapse rate, snowlines and sea level are unchanged).
A seed therefore makes the same continents, ranges, rivers, climate and geological history on any planet - a clean comparison, and
the 10,000-year F1/F2 tuning keeps holding.

| System | Was | Now |
|---|---|---|
| Plates (`Tectonics`) | angular speed = 0.5-1.5 m/yr / R (faster across a smaller world) | geo-m: the same cells a century |
| Uplift, spreading, isostasy | per m/yr of convergence | geo (follows the plates) |
| Erosion, rockfall, sediment (`Erosion`) | slopes over real metres, volumes over real m2 | geo: the same equilibrium relief |
| Coasts (`Coast`) | m a year over real cell areas | geo |
| Ice sheets (`Climate`) | shallow-ice diffusion over real cell spacing (substeps grew as 1/R2) | geo: the same sheets, same cost |
| Sea level from ice | ice volume / ocean area | geo (a ratio: unchanged) |
| Drainage, lakes, river flow (`Drainage`) | discharge = runoff x real area (rivers 1/4 at 5 km, streams vanish under the 1,000 m3/day line) | geo-m3: the same river network and classes |
| Quakes, slides, tsunamis (`Quakes`, `World`) | shaking and run-up reach in real metres | geo: the same share of the world |
| Storms (`Storms`, `World`) | radius 1.8-3.4 km, speed m/s | geo |
| Eruptions: ash, the deadly cloud, vents for the god power | real metres | geo |
| Copper's extent round a vent (`Learning.OreReach`) | 3 km real | geo (the arc's ground) |
| Steepness for animals (`Ecology.Steepness`) and for routes' climb (`Routes`) | over real metres (2x steeper at 5 km) | the ground's own steepness |
| Lightning, fire spread, weather spells | per cell | unchanged: the same weather on the same cells |
| Climate grid, 48 energy-balance bands, orbital cycles | latitude | unchanged |

**Rule L - what lives is in real units.** People, beasts and plants keep metres and hectares: walking 1.25 m/s, fields 0.5 ha a person
and their yields, pasture and browse per hectare, game 15-20 deer a km2, villages 1.9 km apart, fields within 950 m, herds within 900-1,500 m,
hunting 1,300 m, fishing 900 m, gathering 750 m, scouts 2-4.2 km, smoke seen at 700 m, clay within 1.2 km, a day's walk 6 km. Where such a
law was **written per cell** it now scales by the cell's real area (`PlanetState.AreaScale` = (R / 10 km)^2, exactly 1 at 10 km):
a predator's half-sated prey (`Ecology.HalfPrey`), a hunter's range (`Territory`), the hunters', fishers' and gatherers' half-sated grounds
(`WildFood.HuntHalf`, `FishHalf`, `GatherHalf`), how many animals make a cell lived in (dispersal, speciation's populations, a herd's
first cell). Without it, on a 5 km planet wolves, foxes, bears and seals starved out within twelve years (each cell held a quarter of the
prey a hunter needed to be half fed); with it they keep their densities (wolves 233 -> 58, a quarter, as the land).

**Rule P - a patch is a patch of real size.** "A cell and its eight neighbours" stood for a ~680 m patch: a hunting ground, a herd's
ground, two rings for stone. Now `PlanetState.Patch(c, rings)` with `PatchRings` = round(10 km / R) (1 at 10 km - the same cells in the
same order -, 2 at 5-7 km, 3 at 4 km) keeps the patch's real size; at 5 km a herd would otherwise need its 3 deer in a quarter the land
and no herds formed. The first villages are placed 1.2-3.9 km apart in metres (they were 5-16 cells).

**Left as they are, knowingly.** Fresh water "on the cell or next to it" and "a coast cell" are the ground's adjacency (rule G); routes'
search caps (20,000 cells) and the cells-within cache grow with finer cells (measured in cost). Animals wander and herds move a cell a
step (slower in metres on a smaller planet, the same across the ground). `MinViable` (5 animals of a kind in all the world) and a
speciation's 80 x Density animals stay absolute: viability is about numbers, so a smaller world holds fewer of the great beasts.

All of it is **bit-identical at R = 10 km** (a 12-year world hashes aeba16dc9bb1c04e before and after; the multiplications are by
exactly 1.0, the patches of one ring are the old Nbr8 order) - except one fix the shots found later: a new home takes the next lot of
its spiral on dry land (`Settlement.NextDryLot`), which also moves the few 10 km homes that stood in the water.

## 4. The candidates, really simulated

Tools: headless `--radius M` and `run --scale` (`tc2/headless/app/src/ScaleReport.cs`, `Core/Diagnostics/ScaleTally.cs`): at the end
of a run the villages' spacing, how many lie within a day's walk (6 km, the reach of visits, trade, marriage, relief, taking and raids),
the communities those walks join and the hops across them, how many people a person can reach in a day, kin (grown people and their
living mothers), polities, the woods round villages, the wild kinds, the distance of every marriage, trade, relief, move, founding,
taking, raid, claim and tribute, and the cost a world year and a person-day. Runs: four seeds (42, 7, 1, 2) at R = 10, 7, 6, 5 and 4 km,
400 years DEEP, the same ground on each (rule G; a planet-only check of seed 42 over 600 years at 10 and 5 km gives the same
collisions, subductions, ridges, peaks and eruptions, step for step; the small differences are the living layer's). Outputs:
`tc2/out/scale/r<R>_s<seed>.txt|.csv`, the table `tc2/out/scale/table.txt`, saves at years 100 and 300 in `tc2/out/scale/r<R>_s<seed>/`.

**People, villages, density** (year 400; for seed 42 in brackets years 100 / 200 / 300):

| R | seed 42 | seed 7 | seed 1 (cold) | seed 2 |
|---|---|---|---|---|
| 10 km (land 446-463 km2) | 13,546 in 36 (794 / 4,334 / 10,768) | 4,876 in 18 | 963 in 6 | 19,043 in 55 |
| 7 km (218-227) | 6,439 in 24 (705 / 3,663 / 7,287) | 2,471 in 11 | 816 in 6 | 8,197 in 32 |
| **6 km (160-167)** | **5,471 in 18** (765 / 3,962 / 4,539) | **1,995 in 10** | **1,116 in 11** | **7,110 in 30** |
| 5 km (111-116) | 2,564 in 11 (774 / 3,686 / 3,270) | 1,238 in 8 | 450 in 6 | 5,121 in 23 |
| 4 km (71-74) | 3,774 in 16 (816 / 2,860 / 3,332) | 1,010 in 7 | 2,164 in 21 | 3,062 in 18 |

The first century is the same at any size (the six first villages grow to ~600-900 people by year 100 whatever the land: they have
not met its limits); the curves part when the land fills - 4-5 km by year ~150-200, 6 km by ~200-250, 7 km by ~250-300, 10 km still
growing at 400 (D2-070's 1,000-year runs: 16,000-18,500). Small worlds swing harder once full (5 km seed 42 rose to 3,686 by year
200 and fell to 2,810 by 250). Density per km2 of land is set by the seed's climate, not the size (seed 7 11-14, seed 1 2-7, seeds 42
and 2 22-51). People a village: 300-380 at 10 km, 200-300 at 6 km on the good seeds.

**Interaction density** (year 400, the range over the four seeds):

| metric | 10 km | 7 km | 6 km | 5 km | 4 km |
|---|---|---|---|---|---|
| nearest village (km) | 1.4-2.1 | 1.3-2.1 | 1.8-2.1 | 1.5-2.3 | 1.6-2.1 |
| villages within a day's walk | 4.0-9.7 | 4.7-10.8 | 4.4-9.5 | 4.3-8.8 | 6.0-10.6 |
| hops across the settled world | 1.2-3.7 | 1.1-2.6 | 1.1-2.1 | 1.0-1.8 | 1.0-1.7 |
| share of all people reachable in a day (seed 42 / seed 2) | 36 % / 24 % | 54 % / 35 % | 61 % / 46 % | 76 % / 54 % | 55 % / 60 % |
| grown people living away from their mother | 35-45 %, 2.8-4.7 km | 24-56 %, 2.4-4.3 km | 16-47 %, 2.9-4.8 km | 18-55 %, 1.6-4.5 km | 37-51 %, 2.2-4.1 km |
| marriages between villages, mean distance (km) | 2.9-3.7 | 2.3-3.5 | 2.5-3.7 | 1.7-3.8 | 2.3-3.6 |
| trade journey (km) | 1.8-4.0 | 1.8-4.0 | 3.5-4.2 | 3.3-3.8 | 2.7-3.9 |
| households moving, distance (km) | 2.7-3.8 | 3.0-3.7 | 3.4-3.8 | 1.8-3.6 | 2.6-3.6 |
| polities (tributaries) | 1-4 (1-4) | 1-4 (1-5) | 1-4 (1-4) | 1-4 (1-4) | 0-6 (0-8) |
| raids in 400 years | 7-68 | 13-95 | 7-114 | 10-113 | 14-81 |
| trades a village-year | 0.06-0.46 | 0.01-0.44 | 0.13-0.33 | 0.10-0.28 | 0.02-0.27 |

A village's own social world does not depend on the planet: every reach is a physical law, so ~8-10 villages lie within a day's walk,
marriages, trade and moves run ~3-4 km, and two in five grown people live in another village than their mother at every size. What the
size sets is how many such neighbourhoods the world holds: at 10 km the settled land of the good seeds is 2.5-3.7 hops across and a
person can reach a quarter to a third of the world in a day (distant villages they never meet); at 6 km 1.6-2.1 hops and about half;
at 5 km and below the whole world is one neighbourhood (1.0-1.8 hops, three quarters of everyone a day away on seed 42). Conflict
and polities are chaotic per seed and show no trend with size (seed 7's small worlds raid more, seed 42's less); trades thin a little
on small worlds (fewer distinct villages with something to spare). Lived visits per grown person a day are the same at 10 and 6 km on
full worlds (0.028-0.036; COARSE uses 0.03).

**Wild kinds** (year 400): at 10 km seals 9-45, bears 9-21 (none on seed 2), aurochs 64-98, wolves 54-215; at 6 km bears die out on
three seeds of four and seals on two, aurochs 23-40, wolves 11-48; at 5 km no bears and no seals; at 4 km wolves too are lost on some.
The great beasts' viability is about numbers (`Evolution.MinViable` 5): a small world holds few, and on a planet-only run of 5 km seals
die at once and bears by year 180.

**Cost** (DEEP, ms a world year, the machine shared and loaded): 10 km 179-413, 7 km 173-335, 6 km 188-319, 5 km 165-297, 4 km 161-389;
per person-day 1.3-3.8 us at 10 km, 1.7-5.4 at 6 km. The planet's ~150-180 ms a world year (24,576 cells at any size) is now most of a
small world's cost - F8's speed work must target it; the people's share falls with their number, leaving room for richer persons later.

## 5. The pictures (same seed, same ground, same framing rules)

`tc2/out/shots/scale1/sheet_y300.png`, `sheet_y100.png` (10, 6, 5 km: orbit, region, village, close, low, river, town) and
`tc2/out/shots/scale2/sheet_s42_y300.png` (10, 7, 6, 5, 4 km: orbit, region, village, close, scar, scarfar), `sheet_s7_y300.png`
(seed 7 at 10 and 6 km). Framing: orbit at 2.2 R and region at 0.5 R (the planet and a part of it); village 900 m, close 260 m, low
95 m, scar 700 m, scarfar 2.5 km (metres). What they show:
- **Orbit**: the same continents; at year 100 each village's clearing is a brown disc in the woods - at 6 km 1.7x wider against the globe
  (a village with its fields ~0.17 of the diameter, TC1's village map 0.16; 0.10 at 10 km). By year 300 the good seeds' heartlands are
  one quilt of fields at any size - the readability of farmland is D2-084's (seasons, hedges, crops, pasture), not the radius's.
- **Region**: villages read as places from 6 km down (rings of houses round their greens, fields round them); at 10 km they are dots.
- **Village and nearer**: the same physical objects; the planet's curve shows from village height at 6 km and below (the horizon bends
  round the bay), which is what makes TC1-like dioramas of the 2-3 km views (`scarfar`: at 5-6 km a whole land with its villages and
  burns in one frame).
- **People and animals**: no larger on screen at any radius (a person ~7 px at the close view); their size is the camera's.
- **Rivers**: the same network (geo flows); hairlines at any size until D2-084 draws them.
- **Found in the shots, fixed**: clouds on a fixed 650 m shell stood out past the limb of small planets (the shell now goes with R); houses
  laid out on the spiral of lots stood in the sea where the coast lies nearer the anchor in metres (new homes take the next dry lot).

## 6. Recommendation and decision: **R = 6 km** (D2-085)

- It is the smallest planet on which every normal seed keeps ~2,000-7,000 people in 10-30 villages (the band Arjun named), so a
  civilisation of several villages, trade, raids and lordships still forms; at 5 km the weaker seeds fall to ~1,250 people in 8 villages,
  at 4 km to ~1,000 in 7, with boom-and-bust and lost wolves.
- It gives TC1's proportion: a village with its fields covers 0.17 of the globe's diameter (TC1's map 0.16; 10 km 0.10; 7 km 0.14), every
  human mark 1.67x wider and 2.8x the area at the orbit and region views; the curve of the world in every view from 900 m up.
- Its world is intimate but not one village: a village's neighbourhood is unchanged (~9 villages a day's walk, kin and brides at 3-4 km),
  the settled world is ~2 hops across and a person reaches about half of it in a day (5 km: three quarters, one neighbourhood).
- It is cheaper (-25..-36 % a world year) and shifts the cost to the planet, which a fixed grid makes predictable.
- Its costs are known and owned: the great beasts are few (a D2-084 wildlife decision: whether the miniature carries denser great beasts),
  scarcity comes after ~200 years instead of ~400 (more history sooner), and some 10 km test premises were restated.

## 7. What changed (D2-085)

`WorldConfig.Radius` 6,000; the geophysics in geo-lengths (`PlanetState.GeoRadius/GeoScale/GeoAreaHa/GeoCellSize` in Tectonics, Erosion,
Coast, Climate's ice, Drainage, Quakes, Storms, World's eruptions, slides, tsunamis and storms, copper's extent, the ground's steepness for
ecology and routes); living laws per hectare (`AreaScale` in Ecology, Evolution, WildFood, Herds); real-size patches (`Patch`,
`PatchRings` in WildFood, Herds, Learning); first villages in metres; dry lots (`Settlement.NextDryLot`); the view (`Ground.ScaleTo`,
`CameraRig` framing, `TerrainLod.MaxLevel`, `Sky` haze, `CloudView` shell); tools (`--radius`, `run --scale`, shots `--radius`, unique
shot folders). Old saves keep their radius (it is saved per world); 10 km worlds live bit-identically except where a new home used to
stand in the water. Tests at the new default: eight premises that assumed the 10 km world restated (drainage conserved in geo-areas;
grounds and herds as patches; a disease test waits for the stricken village's own midnight and lets visitors go again on later days; the
COARSE spread test with villages of sixty; children carrying water over a longer window) - no law loosened. Found and left for its own
calibrated slice (it changes COARSE): a COARSE disease catch that falls on a child is dropped, halving the sickness visits carry home.

## 8. What D2-084 does next, on 6 km

People and animals readable at the near views (camera and readable exaggeration, not the radius); farmland that reads by season, crop,
hedges, pasture and fallow (the year-300 quilt); rivers and corridors drawn from the (unchanged) network; settlements as places; scars;
the multi-age acceptance on the 6 km seeds (`tc2/out/scale/r6000_s*/world_y100|300.tc2` are ready to shoot); the great beasts' viability.

