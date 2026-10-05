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
