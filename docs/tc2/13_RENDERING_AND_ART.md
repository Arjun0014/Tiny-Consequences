# TC2-13 — Rendering and art direction

Decision: D2-011 (URP; WebGPU first, WebGL2 fallback; instancing without BRG). Quality bar: a readable,
charming diorama from orbit; believable, dense life up close; strong light, lovely terrain, convincing
water, clouds and weather; the passage of ages visible (scars, ruins, coasts, forests).

## 1. Art direction (initial)
- **Stylised natural**: low-to-mid poly forms with soft gradients, not flat-shaded toy primitives
  (Arjun's TC1 feedback: "too comical/simple"). Saturated but natural palette; biome colour harmony
  (grass greens shifting with season and moisture, ochre drylands, blue-white ice).
- **Light**: one sun with soft shadows, sky and ground ambient, warm sunsets; rim atmosphere from orbit;
  night side with settlement lights (lit windows from people awake inside).
- **Scale**: real proportions close in; objects (trees, buildings, people) exaggerated progressively as
  the camera rises so towns and forests read from orbit; mountains vertically exaggerated (~1.5-2x).
- **Ages visible**: scars persist (burnt ground greening over years, ruins weathering, abandoned fields
  returning to forest, new coasts), settlement architecture changes with knowledge and materials.

## 2. Architecture
| Layer | Technique | Data source |
|---|---|---|
| Camera | orbit -> region -> close continuous zoom; camera-relative (floating origin) rendering | Game |
| Terrain | cube-sphere faces as quadtree patches; vertex displacement from per-face height textures (sim elevation upsampled + deterministic detail); normals from height; material weights from fields | N64 fields -> textures, updated on change |
| Ocean | sea-level sphere: depth tint, Fresnel, sun glint, procedural waves, coast foam from a coast distance texture | elevation + sea level |
| Atmosphere | outer shell, rim scattering | sun direction |
| Clouds | shell textured from the climate cloud field + procedural detail; shadows from the same texture | N32 cloud |
| Vegetation | instanced trees and shrubs (several species-shaped variants) placed by keyed hashing per cell x density; LOD and distance culling; far: terrain colour | PFT biomass |
| Settlements | buildings assembled from a modular kit (footings, walls, frames, roofs, towers, gates) per building descriptor and construction state; roads and fields as meshes/decals | layout + building states |
| Agents | low-poly people and animals, GPU-instanced, vertex animation textures (no skinned renderers at scale) | positions from activities + elaboration |
| Effects | fire and smoke, rain and snow, lightning, dust, ash | processes and weather |
| UI | UI Toolkit (runtime): HUD, inspector, news, History Book, catch-up screen | Core views |

- Instancing: `Graphics.RenderMeshInstanced` with instanced properties (both APIs; per-instance colour, emission and walk, batches of
  1,023 with their own bounds: D2-109); WebGPU may later use
  `RenderMeshIndirect` with structured buffers and compute culling. No BRG, no Entities Graphics (Web).
- Shaders hand-written for URP (HLSL including URP's core and lighting libraries), compiled for GLSL ES 3.0
  (WebGL2) and WGSL (WebGPU); no compute dependency in the WebGL2 path.

## 3. Budgets (initial; to be measured)
60 fps at 1080p on a mid-range GPU: terrain <= 400K triangles visible, <= 20K tree instances close/mid,
<= 2,000 people and animals drawn, <= 150 draw calls; texture memory <= 512 MB.

## 4. Foundation (F0) proof
The planet as six displaced face meshes coloured by biome, a sea sphere, a rim atmosphere, instanced
trees from vegetation, settlements as clusters of simple buildings, agents as instanced figures moving on
their analytic paths, fire cells glowing - built for the Web (WebGPU and WebGL2), screenshot-checked.

## 5. Look-dev v1 (session 1, D2-026)

Implemented: ground model (`Game/View/Ground.cs`), chunked LOD terrain (`TerrainLod`), farmland/village ground,
translucent sea over the coloured sea floor, surf, meandering rivers, haze/sky/grade, Blender asset kit
(`tc2/art/kit.py` -> `Resources/TC2Models`), batch-editor screenshots (`unity.mjs shots`). Art notes kept for the
next passes: the miniature reads from soft morning light with long shadows, packed-earth paths and yards, patchwork
fields with hedges, clustered groves, clear shallows and a broken surf line; flat, uniform greens and evenly
scattered trees read as a toy. Open: clouds, tilt-shift, close-in grass, night lights, walking animation, boats.

## 6. Look-dev v2-v5 (session 1, D2-027, D2-029)

- v2-v4 (D2-027): clouds from the weather (cubemap of rain and moisture, cloud shadows), tilt-shift by camera height,
  window lamplight, grass/flowers/reeds/boulders on the finest patches, roads along the villages' routes, seasonal
  canopies, walking people, a slim HUD.
- v5 (D2-029): **volcanoes** - a cone per vent in the ground model (`VolcanoShape`: concave flanks for an arc volcano,
  a broad shield over a plume, a crater bowl, radial gullies; size from its age and eruptions; bare lava rock and
  cinders at the summit, green lower flanks), **puffs** (`Puff.shader`: soft camera-facing sprites whose size,
  opacity, ash, glow and seed ride in the instance matrix) for steam from fed vents, a plume volcano's lava-lake
  fire, eruption columns (height by VEI, umbrella spreading downwind, fire at the base) and the lava's glow down the
  steepest way, each show lasting its world time or a few real seconds; **gentle shores** (a drawn elevation per cell
  that ramps land and sea toward the coast by distance in cells - coarse cells against a deep trench drew as
  cliffs); **moonlight** (a moon opposite the sun in every lit shader, bluer nights) and a sun that the planet hides
  below the horizon (round canopies caught it from beneath at night); the HUD date on one line.
- Art notes: volcanoes read best as dark summits on green mountains with a thin white plume; an eruption reads from
  orbit as a grey column with an orange root; the glow belongs to the night.

## 7. Farm animals, herds and camps (session 2, D2-049)

- `LivestockView`: every live animal of a village near the camera, from the simulation's `Beast` store - by day each
  household's cattle and goats on a grazing ground of their own (one of the ten nearest pasture cells: open land, no fields,
  no water, beyond the houses; a stable key per household), the pigs rooting round the yard; at night penned behind the home
  in a round fenced pen sized to the herd (the gate a different way at each home); walking out from 6.6 h and home from
  17.9 h local, one after another; all day in when the day's mean is below -4 C. The young are smaller (cube root of their
  grown share). Coats: cattle dun, brown, black or pale; goats white, brown or black; pigs pink or grey (tints over the kit's
  colours). One presentation scale per village so pens, fences and animals keep their proportions. Headings follow the
  motion (`LookRotation` on the walk), not a yaw convention.
- Kit (`tc2/art/kit.py`, the same `quadruped()` as the wild animals): `animal_cow` (dun, short horns - the red-brown of the
  first try read as deer), `animal_goat`, `animal_pig`, and a hide `tent` for camps (camps were a shrunken round hut).
- `WildlifeView` draws the simulation's herds and packs near villages (D2-047) where they are and as many as they number, and
  leaves out the field's animals they gather (their cell and its eight neighbours); a herd whose ground is a village's keeps
  to its edge (350 m from the square) - the first shots had a deer herd walking among the houses at dusk.
- Art notes: herds read as dots of white (goats) on the outfield beyond the fields from the village view; pens read as small
  rings of fence behind the houses from the close view and are full and lit by the windows at night.

## 8. Ruins, seal haul-outs, boats and the WebGL2 path (session 2, D2-051 to D2-053)

- **Ruins** (`WorldObjectsView.Ruins`, D2-052): an abandoned village's last households' houses stand roofless (kit `ruin`),
  lowering and greening with the years since abandonment (a house ~300 years, a hut ~80, camps nothing) and its well's stone
  ring (~600); its fields go back to the vegetation. Premise `unity.mjs shots --abandon YEARS`.
- **Seal haul-outs** (`SeaLifeView.HaulOut`, D2-053): a stretch of coast's seals drawn together at its best shore (the field's
  count kept on average; haul-outs three cells apart; at most 18), side by side at the water's edge looking out to sea.
- **Boats** (`WorldObjectsView.Beached`, the people pass): a fisher at the water's edge is drawn rowed out 35-65 % of the way
  toward the fishing ground (where the water is deeper than 0.3 m), sitting in a boat that swings slowly; a coastal village's
  boats lie drawn up on the shore of its best fishing ground, bows to the land, one for two skilled fishers less those out.
  Kit `boat` (an open hull: the kit's `bowl()` primitive).
- **Inspector** (`Hud`): the person's doing and why in a sentence (`HistoryText.Doing`, read from state), the household
  (people, shelter, cattle, goats, pigs, larder), the body's reserve, all four skills; Follow keeps the camera on the person.
- **WebGL2** (D2-051): one shadow cascade over 1.6 x the camera's height on OpenGLES3 (`Sky.GlCascades`, `GlShadowReach`):
  the village view 37 -> 60 fps. Open: the first entry to a new view stalls 1-5 s while the driver builds shaders and
  buffers (a traced shader-variant collection warmed progressively at start is the planned fix; `WarmupAllShaders` took 36 s).
- **Chimney smoke** reads the simulation (D2-056): a house smokes while one of its people cooks and all day while its hearth
  burns against the cold (what today's fire adds to its warmth), no longer by the clock and a temperature threshold.
- **Homes** (D2-059): the view draws a village's buildings, not its households - lived in (windows lit while their people are
  awake, smoke while one cooks or the hearth burns), standing empty (weathering toward the colours of the land), roofless
  past mending, fallen and sinking (the ruin model), gone; camps only for households without a home that are not on the road.

## 9. The living world seen: woods, cover, villages' signs (session 2-3, D2-079 to D2-081)

- **One layout of crowns** (`Game/View/Canopy.cs`): 24 x 24 jittered crowns on a 156 m tile, each with a radius (0.52-0.94 of the
  spacing; a few old broad ones) and a number of plain chance. A crown stands where its number is below -ln(1 - share) x K (K fitted
  on the layout so the standing crowns cover the share); it is a conifer where a hash of its number is below the conifers' share.
  The terrain shader and the trees near the camera read the same layout, so a tree stands where the canopy shows its crown.
- **Three distances of woods** in `Terrain.shader`: near (the crowns: lit domes and pointed conifers with shadows sunward, mixed
  greens, autumn's gold and rust where there is an autumn, bare boughs, snow on conifers, leaf litter between); within 440 m of the
  camera every standing crown is a tree mesh (`TerrainLod.PlaceForest`: round, tall, spreading and birch broadleaves, conifers in tiers
  or one cone; bare in winter; young and short where a fire burnt in the last ~40 years), the canopy fading to them from 400 to 300 m;
  far (crowns under a few pixels) the **cover texture** (`Canopy.MakeCover`: whether a standing crown covers each texel at four
  thresholds; its mips the share covered) - the near canopy averaged, with clumps lit by a bump and floors shaded sunward; past
  3.5-7 m a pixel the cover read turned and scaled up (clumps of ~20 m that never line up with the tile), past 10-20 m a pixel its mean;
  **stands** of a few hundred metres shade the far woods (their texture from orbit).
- **Rule: nothing in a repeating tile may make a pattern.** The woods' clumps (20-45 m, open woods only), glades (~170 m) and stands
  live in the forest share and the stand noise, which never repeat; the tile holds only chance (`woods1`: clumped numbers in the tile
  read as a grid from orbit).
- **The land's own vertex stream** (`TerrainLod` uv3): the climate's dryness (leaves olive, grass straw), the meadow, bare earth or
  rock, the beach's width. **Beaches are drawn per pixel** from the ground's height over the sea (vertex colours made a staircase along
  coasts on coarse patches).
- **Ground cover by what the land is** (`TerrainLod.PlaceCover`, finest patches, to 260 m; shrubs and boulders to 480 m): meadow grass
  patchy in density and tone, wildflowers in their season, shrubs here and there; ferns and undergrowth under the woods; shrubs at
  their edges; heather on cool wet open land (purple in late summer); cushions, lichen and stones on tundra; straw and stones where dry;
  dune grass above the tide; reeds by fresh water; boulders on slopes. Kit-free meshes `ProcMeshes.Shrub/Fern/Cushion/Stone`.
- **The villages' signs** (`WorldObjectsView`): smoke from chimneys, huts and camps while someone cooks or the hearth burns against the
  cold; woodpiles by lived-in homes sized by the days of wood the village holds; a watch fire by the store at night while the watch is
  kept; from high above at night a warm glow over each village by its lit homes (puffs; drawn above 1.5 km).
- **Animals**: wild herds drawn to 3 km of altitude, up to 4x far off; livestock in pens and on the pasture (§7).
- Shots: `unity.mjs shots --tag T --views orbit,region,village,close,low,forest,forestlow,forestfar,coast,low+14,orbit@21` (`+D` days
  ahead, `@H` local hour: seasons and nights in one round). Art notes: forests read from afar by value (darker than meadow) and by grain
  (clumps and gaps); a meadow reads by patchiness in density and tone, not by more of the same tuft; shrubs read best darker than the
  grass; lights at night tell "inhabited" from orbit more than anything by day.

## 10. The world made legible (D2-084; slices in `18_WORLD_READABILITY.md`)

Slice 1 (composition, forests, rivers - in progress, session 4):
- **Stands and glades** (`Canopy.Stands`, the terrain shader's `StandRank`): where the land is wooded its forest share is drawn as stands
  of closed canopy with edges and glades and meadows between - a stand where the share is above the point's rank in a never-repeating
  value noise (~150-300 m), the ranks spread evenly through the noise's own distribution (`Canopy.StandCdf`, a 256-texel texture), so
  the stands cover the share; thin woods (share < ~0.12) stay scattered trees; a pixel wider than ~45-110 m draws the share itself.
  The trees near the camera and the ground cover follow the same rule (the CPU's copy of the shader's arithmetic). Near a river the
  rank is lowered: the woods keep to the banks.
- **The open land's palette** (`TerrainLod.CellColour`) from the climate and the ground's water, not the biome's class: lush meadow,
  gold-olive steppe, sand, grey-green tundra, grass browning in a dry spell (soil water), thin alpine turf, dark wet meadow on valley
  floors (`Ground.Relief`: a cell's height against its two rings of neighbours, -1 valley .. +1 ridge). Conifers keep to ridges and
  broadleaves to valleys where the climate holds both.
- **Relief from afar**: past ~0.5-5 km the ground's slopes are lit as if steeper (a cartographer's hillshade in the terrain shader);
  haze ~40 % thinner; the tilt-shift a hint at the frame's edges below ~1 km (it softened whole views).
- **Rivers** (`Rivers.cs`, `River.shader`, `WaterView`): one shape for every reach (the drainage's path, meandering), shared by the
  water and the ground; the water is a ribbon on the centre line, as wide as the flow says near the camera (~2.5x a real river) and,
  for the planet's larger rivers (by their rank in flow), never thinner than ~2.6 pixels far off; ripples run down its course, the
  sky in its fresnel, the sun's glint; brooks fade below a pixel. The ground draws the corridor: wet earth at the edge, a lusher
  riparian strip as wide as the river is large, reeds at the edge only (no cover in the channel), no trees in the water.
- **Trees** (`ProcMeshes.ForestTree`): seven kinds in three variants each - round, tall, wide and forked (oak), birch, willow and alder by
  the water, spruce in tiers, pine (a tall bare bole under a flat broken crown) on dry or coastal land - every crown lumps of different
  sizes and shades (never one smooth ball); the far forms as before.
- Tools: `unity.mjs shots --load "F1;F2"` (saved worlds, each into its own folder), views `river`, `riverfar`, `town`, `townclose`,
  `scar`, `scarfar`; headless `run --snap Y1,Y2` saves the world at those years (`tc2/out/ages/sS/world_yY.tc2`).

## 11. The view on a planet of any size (D2-085)

The planet is 6 km now (D2-085; `19_PLANET_SCALE.md`) and the view follows `PlanetState.GeoScale` (R / 10 km): the named views frame
the planet (orbit 2.2 R, the catch-up camera 2.2 R) and a part of it (region 0.5 R) and keep metres from the village view down (900,
260, 95 m); the drawn relief is 0.4 x GeoScale of the simulated heights about the sea (`Ground.ScaleTo`, set with each world - the ranges
keep their proportion to the globe; hills, gullies and beaches are drawn at their own size) and the mountains' crags with it; the cloud
shell sits 650 x GeoScale m up (on a fixed 650 m it stood out past a small planet's limb); the terrain quadtree stops at the same finest
quads (`TerrainLod.MaxLevel` = 7 + round(log2 GeoScale)); the haze's top and the sky's change to space go with R; the tilt toward the
horizon reaches 0 at 0.9 R. `?radius=` (and shots `--radius`) make worlds of other sizes; a saved world keeps its own.

## 12. The civilisation kit (D2-089)

Every structure kind of `21` gets a distinct, readable model in one miniature art direction (low/moderate polygons, strong silhouette,
good materials), made by headless Blender (`tc2/art/kit.py`, Claude has permission to create them): homes in material and wealth tiers
and building traditions; workshops, granaries, barns, smithies, mills, markets, halls, training grounds, barracks; fortification pieces
(ditch, bank, palisade stretches and gates, wooden towers, stone wall pieces, towers, gatehouses, keep parts); economy props (carts,
barrels, crates, woodpiles, ore and stone piles, furnaces, mine entrances, quarry faces); maritime (dugout, fishing boat, cargo boat,
sailing ship, dock modules); and more nature (tree silhouettes, shrubs, rocks, reeds, crops). Visible labour (chopping, carrying,
quarrying, hammering, training, marching, sailing) is drawn only while the simulation says it happens.

## 13. Building variety (Arjun, 8 Oct 2026; D2-114) - a standing requirement

A settlement must never read as repeated same-colour, same-shape models. **Homes** vary by real circumstances - household size, means,
material at hand, age, the settlement's history, its building tradition (later regional cultures) - as families of plans with deterministic
variants: footprints, one or two storeys, roof forms, roof and wall materials, timber/stone/plaster, extensions, chimneys, yards and fences,
wear; never random colours. **Functional buildings** are recognisable at a glance by footprint, height, silhouette, roof, materials, props and the
activity round them (smithy, training ground, granary, market, boathouse, mine and quarry, hall; later towers, gates and keeps dominating the
silhouette). **Places grow in substance**: richer, larger, older settlements become denser and more substantial - a hamlet, a village, a walled
town and a political centre never read as the same houses at different counts. Modular families plus variants (not hundreds of unique meshes);
judged in real shots at close, village and region views at several ages, never from code.
First implementation (D2-114): a building's form is real state recorded when it is raised (`Building.Form`, `Core/Places/Architecture.cs`), a
village's building tradition is carried by its founders with drift (`Settlement.Tradition`), households of means rebuild old or poor houses
(`Homes.WeighRebuilding`); the kit's five plans x three roof forms with framing, chimney, plinth and lean-to overlays, walls and roofs as two
tint channels (`_WallColor`); barns, granaries, smithies and the market with silhouettes of their own.
What the real shots taught (session 10, `tc2/out/shots/arch1`-`arch3works`): (1) every old thatch had weathered to one dull brown and every tile was
one saturated orange - now a roof's colour is its material weathered by the years since it was last laid (`WorldObjectsView.RoofColour`): a
lived-in house or a work in use is kept, its straw re-laid every ~28 years, reed ~45, shingles ~40, each on its own turn, so a village shows fresh
golden thatch beside greyed and mossy; tiles and slates come in kiln and quarry batches and darken over a century; a building left empty weathers
from the day it was left. (2) Daub walls read as dark as old thatch - lightened and warmed. (3) Each house's 8.5 m earth yard ran together into one
bare brown disc in a town - the yard is now a doorstep (3.5-6.5 m), paths and the square stay. (4) A kitchen garden (`garden`: raised beds of greens,
wattle hurdles) behind each house whose household has working hands, as wide as its hands, beside the house where the beasts' pen is; none in a
town's built-up core. (5) Works: barns, granaries on staddles, the market's cross and stalls, the boat shed and the quarry read at a glance; the
training ground was a faint ring of posts in ploughland - its ground is now beaten pale by the hours of practice put in (`b.Use`); the smithy is
drawn 1.2 times life and its walls blacken with its forge's hours. (6) The plans themselves were 81 % one cottage because the household's "means"
did not separate households (`21` §6): with a measure that does, a town shows one-room cottages, cottages, longhouses, two-storey houses in its
core and its hall - and an old prosperous village of longhouses reads unlike a crowded late town.
**Streets (D2-116)**: villages grow along their roads - a house that fronts a street faces it, square to it, its garden behind, its door path to the
street, the street beaten earth out past its last house; a hamlet with no worn way keeps its houses round its green (shots `street1`,
`street1y100`). Back lanes (D2-117) parallel to each street fill behind its frontage: a town of streets and lanes (shot `lanes1`).
