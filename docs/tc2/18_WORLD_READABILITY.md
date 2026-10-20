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

**W-FOREST - forests and biomes still read as the same trees (OPEN; Arjun, 8 Oct 2026 - NOT solved, never mark it done without his
judgement in real shots).** Slices 3 and 5 above were begun (D2-080 to D2-082, D2-084 slice 1) but the result is not accepted: many forests
read as the same handful of trees recoloured, and a biome's identity is weak. A dedicated ecology/art pass (after the current civilisation
slices, unless a C-slice touches it directly) must deliver:
- strongly biome-specific tree composition and silhouettes (boreal spires, temperate broadleaf domes, dry open pine/oak savanna, wet
  alder/willow carr, riparian galleries, montane krummholz, warm evergreen) - not one kit recoloured;
- far more species, form, height and age variation inside a stand (saplings, poles, mature, veterans, snags);
- forest interiors vs edges vs young regrowth vs old growth readable as different structure (density, height, gaps, understorey);
- distinct wet / dry / cold / warm / riparian vegetation, read from the simulation's climate, moisture, streams and disturbance;
- much stronger ground-cover and biome palette differences (heath, bracken, sedge, steppe grass, tundra cushions, leaf litter);
- forests that look genuinely different by climate and ecology - the test: shots of four climates side by side must not be confusable.
Real state where a meaningful difference cannot be drawn (`18` §5; e.g. stand age / disturbance kind per cell, forest composition as state).

**W-FOREST pass 1 (session 16, D2-130) - still OPEN for Arjun's judgement.** Measured first (`place --woods`, five worlds at year 300): the
wooded land is mostly hot (22-30 degC mean: 40-75 % of wooded cells) or cool (2-10 degC: 25-55 %), little between, moisture 0.3-0.9, true
drylands rare, wet ground common - and seed 42's tropical forest was drawn as temperate lumps. Now (`Game/View/ForestTypes.cs`, `TerrainLod.
PlaceForest`, `ProcMeshes.ForestTree`): each crown's **kind of wood** is read from the simulation's climate and water at its point - the cell's mean
temperature at the point's height (lapse rate), moisture, soil and standing water, a stream's corridor: rain forest, dry tropical woodland, warm
temperate, temperate broadleaf, cool mixed, boreal, treeline, wet carr or swamp, river gallery. **Ten new forms** (17 kinds x 3 variants): firs
(narrow drooping spires), krummholz, rain forest emergents (a tall buttressed bole under a broad flat crown) and canopy trees (flat-topped
cauliflowers), palms, umbrella trees of the dry tropics (a forked trunk under a flat crown), alders of the carr (several stems), poplars,
mangroves (stilt roots), snags. **Stands have ages**: saplings, poles, mature trees and veterans by the crown's number; snags standing longer in the
boreal woods and many where a fire passed in the last 30 years; edges and glades lower. **Openness by kind** (dry woodland and the treeline
scattered, a carr's pools) and **stature** (the rain forest 1.45x, emergents above it; the treeline knee- to man-high). **Palettes by kind**: the rain
forest's deep glossy greens, the dry tropics' olive, the carr's grey-greens, warm evergreens' dark olive, palms, birches' light leaves golden in
autumn (4,096 colour groups). Camera views `forest:KIND[:low|:far]` frame the most wooded ground of a kind in the first village's half of the day.
Shots `tc2/out/shots/wf2/sheet4.png` (rain, dry, mixed, boreal side by side), `wf2`, `wf3`. The far canopy (the terrain shader) takes the kind
too (UV4: how tropical, how seasonally dry; `wf6/before_after_far.png`). Not yet: ground cover by kind, stand age as state (only fire's is real; felling and windthrow leave none), a straight snow/no-snow seam seen on one boreal patch.

**W-FOREST pass 2 (session 17) - still OPEN for Arjun's judgement.** Measured first (`place --woods`, by kind and by the summer's warmth): the
simulation itself held little cool forest - the median woods share of wooded boreal cells 0.08-0.16, broadleaf 0.09-0.24, none at the treeline -
because tree suitability ramped on the annual mean (`PlanetSystems.GrowVegetation`: SmoothStep(-1, 8) degC), so a boreal cell at an annual 2 degC
held a quarter of the cover its moisture allowed. Real treelines follow the growing season's warmth (Koerner & Paulsen 2004: a growing-season
mean of ~6.4 degC everywhere). **The simulation (D2-142, ten seeds `_w1` against `_v4`)**: trees by the growing season - SmoothStep(5, 10) of the
annual mean plus 0.6 of the seasonal swing (`--oldwoods` the control, byte-identical to `_v4`). **The view**: the woods' floor by kind of wood
(moss, lichen and bilberry under boreal spruce, big dark fronds under the rain forest, golden grass and thorn scrub in the dry tropics, maquis,
bracken, sedge in a carr), the rain forest's canopy closed with emergents above it, boreal spires narrower and taller, the dry tropics'
woodland bare through the dry half of the year, the treeline by the growing season's warmth (`ForestTypes.Classify`). Baseline shots
(`tc2/out/shots/s17a`, seed 4 year 300) found no woods at all in the boreal and the mixed views - cleared or thin.
**Found in passing - the dry season is not in the state.** The weather's rain chance swings with the seasons by only +-0.08 x moisture
(`PlanetSystems.Weather`) at every latitude: the outer tropics have no dry season, though the view draws the dry tropics' leaves falling in one
(an elaboration of the climate's class, not of the state). **Candidate W-FOREST pass 2b**: the rain belt follows the sun - in the outer tropics
(|latitude| ~8-25 degrees) the seasonal swing of the rain chance grows to most of its mean (wet summers, dry winters: the savanna's), none on the
equator and in the subtropical highs; the annual moisture index unchanged. What it would change, from the state: the soil dries in the dry
season - the grass browns (`CellColour` already browns it by soil water), fires find dry fuel (the savanna's fire season), herds and wild grazers
thin out on the dry pasture, the deciduous woodland's leaf fall becomes the soil's own; the crops grow in the wet summer (their growth reads soil
water only in the growing stage, local summer), so harvests should hold. Ten seeds against pass 2 (hunger, herds, fires, people), then shots of
the four climates side by side in the dry and the wet season.

**W-FOREST pass 2b (session 19, D2-148) - still OPEN for Arjun's judgement.** Built as above: the rain belt follows the sun (`--oldrain` the
control, byte-identical); the dry tropics' soil now dries to ~0.10 in the dry season and wets to ~0.43 in the rains (`place --seasons`, seed 42);
ten seeds at par in per-person rates (people x1.04-1.08, the wild's food x0.90). The view reads the soil's own water: the seasonal tropics' grass
greens with the rains and goes to straw, their woodland withers khaki and drops its leaves tree by tree (a mosaic, near and in the far canopy), the
grass tall; the cool lands' open ground a duller turf. **Found in the shots**: every spruce, fir and pine had been drawn pale grey since the forms
were made - the mesh builder's face shading scaled the vertex alpha that marks what the draw tints (`ProcMeshes.Builder.Dim` now keeps it); the
boreal and mixed woods are now dark green spires among birches. Shot views `forest:KIND` pick a representative inland stand off the fields
and away from cones and steep ground, and log its latitude and soil water. Shots `tc2/out/shots/s19b*`, `s19c*`, `s19d*`;
`s19b_dry428.png` (one stand at 20 S in the rains and the dry season).

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

**Polities on the map (D2-167, the realms map layer).** The bonds between villages (C10.4-C10.7) are real and consequential but read only up close:
a tributary flies its lord's standard by its first household's door (D2-152) - a speck from the village view, even enlarged. The HUD's **Realms**
(key R; shots `unity.mjs shots --realms`; the Web bridge's `on realms`) washes the land each village of a lordship works - its fields and built
ground - in its realm's colour (the seat 0.72, a tributary 0.43, the ground's light and dark kept), with a band of that colour about three pixels wide
just inside the land's edge at every view (measured by the weight's own derivative), fading out within ~450-1,300 m of the camera, where the
standards and the carriers tell the bond; chevrons in the realm's colour run from each tributary toward its own lord along the way its tenth is
carried (straight over water); a legend lists each head of a lordship with the villages that pay it and their people. The map's dyes are the
standards', drawn brighter (`WorldObjectsView.HeraldryMap`: over fields the standards' gold read as ripe grain and the dark dyes as bare earth), and
a realm takes only dyes that cannot pass for the land's own colours (`MapSafe`): never white or black (snow or salt, burnt ground), no sky blue
(magenta in its place), azure drawn blue-violet (a blue wash reads as a lake) - eight dyes, and no two realms fly one colour while there are dyes for
all. Every mark is real state - the land worked, who pays whom, the way the tenth goes; no border is drawn where no bond is. Judged in real shots
(`s24r3`/`s24r8` found the first look's faults; `s24s3`/`s24s8` read at the region and orbit views; `s24t`, a year-900 overkingship, found a sky-blue
realm reading as lakes and a white one as a salt flat).
