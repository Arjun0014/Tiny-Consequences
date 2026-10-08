# TC2-18 — The world made legible

Decision: D2-084 (Arjun, 7 Oct 2026). A standing project requirement, not a look-dev pass: it holds for every later phase.

> "I can understand that this world is alive before I open a single statistics panel."

## 1. The requirement

The simulation already knows forests, grass, climate, rivers, lakes, coasts, fire, quakes and landslides, wild animals, herds,
fields, homes, ruins, roads, people at work, famine, sickness, raids, walls and lordships - but on 7 Oct 2026 almost none of it
could be read by looking at the planet. The world must read as alive, and as historical, from the picture itself:

- at the **region** view (half the radius up: 3 km on the 6 km world of D2-085) a player can point and say "a forest", "a valley", "pasture", "a river", "a village";
- at the **orbit** view (2.2 radii: 13 km) the planet reads as land with structure (mountain ranges, river basins, woodland masses, dry
  lands, ice, inhabited places), not "a dark coloured sphere";
- at the **village** and **close** views a hamlet of 20-50 people reads as a place (clustered homes, paths, smoke, gardens, pens,
  animals, fields, stores, wells, fences) and the land round it as a working landscape;
- **over centuries the world visibly changes**: year 0 and year 300 must not look like the same green ball with more houses.

**No simulation theatre** (absolute). Everything drawn corresponds to state: presentation may *elaborate* (one forest share ->
thousands of deterministic trees; 70 L/s -> a river widened to read; a herd -> a readable group) and *exaggerate for legibility*,
never *invent* (no city where the simulation has five houses). Where a meaningful difference cannot be drawn because the
simulation keeps no state for it, add the **smallest real state** that makes it true (and test it) - never decorative fake state.

## 2. Baseline (7 Oct 2026, D2-081/D2-082 builds; editor shots `tc2/out/shots/woods5`, `woods6`; Arjun's play of the Web build)

- **Orbit**: a cartoon globe - land one smeared yellow-green with a beige band of desert and an ice cap; no mountain ranges, no
  rivers, no basins, no visible places by day (night lights only).
- **Region**: a uniform speckled green (the woods' cover texture) over flat-looking land; desert as a beige smear; beaches clear; the
  villages small brown dots; rivers invisible; no relief, valleys, ridges or river corridors.
- **Village**: soft (tilt-shift blur) uniform canopy; a field patchwork; a thin blue line of river; a tiny village whose homes read only
  close in; herds as specks.
- **Close**: near trees all the same rounded blob ("green lollipops"), ground one yellow-green, cover sparse.
- UI: the top buttons, the History Book and the inspector still look like development panels (later pass: §8).

## 3. Audit: what the simulation knows -> what is drawn -> what should read

| Category | Simulation state (Core) | Drawn now | Should read |
|---|---|---|---|
| Woods | `Forest` 0-1 per cell (137 m on the 6 km world) (logistic to a climate capacity); `BurnedAt`; conifer share from `TMean` (view) | canopy shader (crowns in a 156 m tile, cover texture far off), trees within 440 m: five meshes of one style | interiors vs edges vs glades; broadleaf / conifer / mixed by climate; young / mature / old stands; dead and burnt; seasons; masses from orbit |
| Woods' age / structure | **none** (only `Forest` and fire's `BurnedAt`) | young trees for ~40 y after fire | regrowth after clearing, abandonment, slides and fire; old-growth vs young -> **needs real state** (disturbance kind + time per cell) |
| Grass, open land | `Grass` 0-1, `SoilWater`, `Moist`, `RainToday` | ground colour (biome colour lerps), tufts near camera | meadow vs grazed pasture vs dry steppe vs heath vs bare; drought browning that follows the weather |
| Climate | `TMean`, `TAmp`, `Temp`, `Moist`, `SoilWater`, `Ice` | snow from temperature by height; olive leaves by dryness | snowline and its season, frost, drought, wet lowlands |
| Biomes | `Biome` (classification of the above) | `BiomeColor` palette lerped per cell | distinct palettes and textures per land kind (§5) |
| Relief, geology | `Elev`, `Crust`, `Plate`, slope (derived), volcano vents, `Magma`, quake `Stress` | smooth drawn relief at 0.4x plus noise; rock tint on slopes > 0.1; volcano cones | ridges, valleys, cliffs, scree, exposed rock by slope and height, erosion and landslide scars |
| Rivers | drainage `Receiver`, `Discharge`, `RiverFlow` (m3/day), `IsStream` | 2-26 m flat ribbons in the terrain material, 1 m above ground | river corridors: channel widened for legibility, wet banks, reeds, riparian trees, floodplain, meanders, reflections |
| Lakes | `LakeWater`, `LakeDepth` | one square quad per lake cell | shores following the water, depth colour, reed margins |
| Coast | coast mask, sea level, depth | per-pixel beaches, shelf colours, surf | cliffs where steep, dunes, estuaries, river mouths |
| Fire | `Intensity`, `BurnedAt` | burning cells glow; dark ground for 2 y; young trees 40 y | burnt forest: black ground, dead trunks, then grass, shrubs, young trees over decades |
| Landslides | relief change + vegetation cut (no lasting record but the vegetation) | nothing lasting | a scar of bare rock and soil greening over years -> **needs real state** (disturbance record) |
| Floods | sea floods on fields (history only); no river flood state | nothing | later (F9 powers, river floods): wet floodplain, damaged fields |
| Storms | storm tracks, landfalls (`Storms`) | clouds, rain | later: windthrow if the simulation records it |
| Wild animals | `Abund` per species per cell; herds and packs near villages (`Herds`) | animals near the camera from the fields; herds near villages to 3 km | readable groups at region (herds as moving clusters), trails? (only if real) |
| Livestock | `Beast` store per village | pens, grazing animals near the camera | herds on the pasture visible from the village and region views, grazed ground |
| Fields | `FieldOwner` per cell, crop stage, growth, burnt | farmland shader (strips, furrows, hedges), crop colour | the agricultural calendar visible; fallow; **abandoned fields** -> weeds -> shrubs -> woods (**needs real state**: when a cell was last farmed) |
| Homes | `Buildings` (kind, style, condition, household), camps | kit houses, huts, tents; empty homes weather; ruins | clustered hamlets that read from the region view; growth into denser villages; material change with knowledge |
| Village works | granary, well, palisade (`Works`, `Wall*`), woodstore | granary, well, palisade, woodpiles, haystacks | yards, gardens (not simulated: do not draw), paths, fences, storage |
| Roads | `Routes` between villages (the walkers' paths) | every pair of villages within 4.5 km gets a road | roads **by use** (traffic worn into the ground) -> **needs real state** (use per route, decaying) |
| People | positions, activities, trips, parties, bands | figures near the camera | activity readable at village view: work in fields, smoke, travellers on roads, parties moving |
| Famine, sickness | ration, hunger events, epidemics, the sick abed | nothing visible | no skulls: fewer people out, cold hearths, empty yards, empty homes - occupancy and activity from state |
| Conflict, polities | raids (`Bands`), walls, tribute, lordships | palisades; bands as walking people | walls clearly visible; damage only where state records it; lordship centres inspectable |
| Knowledge, materials | techniques known (pottery, ard, nets, loom, quern, metal) | nothing | architecture and tools that change with knowledge (later) |
| Abandonment | `Abandoned`, ruins by `Homes.RuinLeft`, fields released | roofless ruins sinking; wells | ruins in regrowth, old field patterns, roads fading |

## 4. Principles for the presentation

1. **Composition first**: the land must break into readable masses (dense forest, woodland edge, clearing, meadow, pasture,
   heath, tundra, wetland, floodplain, river corridor, rocky mountain, ridge, beach, dry land, snow and ice, burnt land,
   regrowth, farmland, abandoned farmland, settlement) whose borders follow state: forest share, grass, moisture, temperature,
   height, slope, drainage, fields, fire and disturbance age.
2. **Scale-aware representations**: near - real forms; mid - grouped forms; far - masses with value, hue and grain that stay
   legible; orbit - large regions and structure (relief shading, basins, rivers as lines, settlements as lit/cleared patches).
3. **Value and hue hierarchy**: forests darker than meadows, wet darker than dry, rock lighter and greyer, water clearly water;
   biomes differ in hue family, not only in brightness.
4. **Clarity over softness**: tilt-shift and haze must never smear the subject (the current blur softens whole views: to be
   reduced to a hint at the frame's edges near the ground).
5. **History leaves marks**: disturbance (fire, clearing, slides, lava, floods, abandonment) is drawn from its kind and age; the
   marks fade at the rate the simulation regrows them.
6. **Exaggerate for legibility, never invent**: river width, object scale with distance, animal groups - all from real state.

## 5. Real state added for legibility (D2-084 rule: the smallest that makes a real difference drawable)

To be added slice by slice, each with tests and documented here:
- **Disturbance memory per cell** (`Planet.DisturbKind`, `Planet.DisturbedAt`): fire, clearing for fields, abandonment of fields,
  landslide, lava - set where the simulation already makes the change; read by the view to draw scars and regrowth stages, and by
  the woods' stand age (years since the last stand-replacing disturbance).
- **Roads by use**: traffic counted per route (walkers, traders, parties), decaying - roads appear, widen and fade with use.
- Candidates, only if the drawn difference matters: forest composition (broadleaf/conifer/mixed as state rather than a temperature
  rule), river floods.

## 6. Slices (Arjun's order, 7 Oct 2026)

1. Audit (this document) and the **multi-age baseline** (§7).
2. Landscape composition: relief legible (hillshade from the simulation's relief, valleys and ridges), height and slope driving
   cover, biome palettes.
3. Forests beyond D2-080: kinds by climate, size and age variation, understorey, edges and clearings, trunks near, groups mid,
   masses far.
4. Rivers and river corridors.
5. Biomes and ground cover.
6. Terrain and geology (rock, cliffs, scree, gullies, snowline).
7. Settlements that read as places (and an analysis of founding: is the simulation fragmenting people too much?).
8. Wildlife, livestock and world activity.
9. Persistent scars and history.
10. Orbit-scale legibility.
11. Multi-age acceptance (§7) passed.
12. Browser performance (60 fps in normal views, WebGPU and WebGL2).

## 6b. On the 6 km planet (D2-085)

The planet's size was reopened before these slices were spent on 10 km (`19_PLANET_SCALE.md`): the work from here is judged on the
6 km world. What the radius gives and what it does not: every human and historical mark is 1.67x wider against the globe (a village
with its fields 0.17 of its diameter), villages read as places from the region view, the curve of the world shows from 900 m; people
and animals are no larger on screen (that is the camera and readable exaggeration - slice 8), and the good seeds' year-300 heartland is
one quilt of fields at any size (farmland legibility - seasons, crops, fallow, hedges, pasture - is slices 2 and 5).

## 7. Acceptance: can I see history without reading the date?

`node tc2/scripts/headless.mjs run --seed S --years 1000 --deep 12 --snap 0,25,100,300,1000 --snapdir DIR` saves the same world at five
ages (6 km: `tc2/out/scale/r6000_sS_ages/world_yY.tc2`); `node tc2/scripts/unity.mjs shots --load F --tag ages_S_yY --views ...` shoots each in the editor
(orbit, region, river, forest, village, close, and a disturbance scar where one exists). The rounds are compared side by side: over
centuries the land must visibly transform (clearings, fields and their abandonment, regrowth, scars, roads, villages growing and
falling) and each view must read without the HUD.

## 8. Later (recorded, not first)

The UI (top buttons, History Book, inspector) still resembles development panels: once the world's legibility stabilises it gets a
full art pass - it must not ship as debug UI.

## 9. The civilisation made visible (D2-089)

Done so far (D2-090-D2-097): works drawn from the structure table (granaries on staddles, barns, the smithy with smoke while a smith works,
the quarry face on cut rock, the training ground, market stalls, stone piles, houses on posts or footings); woods thinned where felled and
cleared under fields; roads drawn by use (no road nobody walks; beaten roads visible from the region view). Open: works hard to tell from homes
at the village view; rings that do not grow with the town; the training ground's posts too thin.


The C-phases (`25`) make this doc's requirement larger, not smaller: lumber cut and woods thinned, quarry faces and stone piles, smithy
smoke and the smith at work, training grounds in use, roads worn by use (replacing roads drawn by distance), markets, walls and gates,
docks and boats, forces on the move, ruins, battle damage, recovering woods - each drawn from state when its slice lands, judged at the
orbit, region, village and street views across ages.

**Building variety (D2-114, standing).** Settlements read by their buildings: homes differ by household, means, material, age, history and
tradition; works are recognisable by silhouette; places get denser and more substantial as they grow richer and older (`13` §13). Judged at the
close, village and region views and across ages - the core of an old town and its newer edges should read differently.
