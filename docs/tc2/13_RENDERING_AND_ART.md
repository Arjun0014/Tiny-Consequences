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

- Instancing: `Graphics.RenderMeshInstanced` with instanced properties (both APIs); WebGPU may later use
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
