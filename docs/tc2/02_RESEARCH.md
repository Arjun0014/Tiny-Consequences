# TC2-02 — Research

Research done 5 Oct 2026 for the TC2 foundation. Each topic separates **(I) inspiration** (what the
science or prior work says), **(G) game simplification** (what our fictional tiny world takes) and
**(D) implementation design** (what the code does). The legacy audit is `LEGACY_AUDIT.md`; TC1's own
research notes (`docs/world/00_RESEARCH.md`, `docs/v3/40_RESEARCH_CONFLICT_PLANET.md`, `docs/world/18_DEEP_WORLD.md`)
remain valid sources for subsistence economics, demography, conflict and disasters and are not repeated.

Primary sources for the Unity platform are the **manuals installed with this exact editor**
(`C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/Documentation/en/Manual/`, cited as `Manual/<page>`)
and the **built-in package docs** (`.../Resources/PackageManager/BuiltInPackages/<pkg>/Documentation~`).

---

## 1. The machine and the toolchain (measured 5 Oct 2026)

- AMD Ryzen 5 4600H, 6 cores / 12 threads; **11.4 GB RAM** (the binding constraint: one heavy Unity or
  browser process at a time); AMD Radeon RX 5600M 4 GB + integrated Radeon; 82 GB free disk; Windows 11.
- Unity Hub + **Unity 6.6 (6000.6.4f1)**, modules: Web Build Support, Windows (Mono) standalone,
  documentation. Licensed; **batchmode works** (`-batchmode -nographics -quit -createProject` in 52 s).
- The editor bundles **.NET SDK 8.0.318** (`Editor/Data/DotNetSdk/dotnet.exe`) with the netstandard2.1
  reference pack: plain C# can be compiled and run headless without Unity, offline.
- Node 22.14, Python 3.13, Java 26, LuaJIT 2.1, git 2.46; Playwright with Chromium, Firefox and WebKit.

## 2. Unity 6.6 on the Web: the facts that shape the architecture

| Fact | Source | Consequence |
|---|---|---|
| Managed C# threads are **not supported** on the Web: no multithreaded GC in wasm. `Task.Run`, `Parallel.*`, `ThreadPool`, `Task.Delay` cause **unrecoverable browser hangs**; `Monitor`/locks are no-ops | `Manual/web-multithreading-intro`, `web-dotnet-api-support` | the simulation runs on the main thread, time-sliced per frame; never use TPL in sim code |
| **Burst-compiled jobs do run in parallel** on the Web when "Enable Native C/C++ Multithreading" is on and the page is cross-origin isolated (COOP `same-origin`, COEP `require-corp`, CORP `cross-origin`); otherwise jobs run on the main thread | `Manual/web-multithreading-burst` | bulk numeric kernels (fields, instance buffers) as Burst jobs; always correct single-threaded |
| itch.io has a "SharedArrayBuffer support" checkbox that sets the isolation headers (since 2023) | itch.io community, bugnet.io notes | threaded builds are deployable on itch; Wavedash's headers unknown -> must work without threads |
| The GC on the Web runs **only at the end of a frame** (no stack scanning in wasm) | `Manual/webgl-memory` | long synchronous loops that allocate grow the heap until the frame ends: the sim must be allocation-free in hot paths and the catch-up loop must yield every frame |
| Heap up to 2 GB by default; **WebAssembly64** (new in 6.6) raises the limit to 16 GB, not in Safari, with known browser bugs > 2 GB | `Manual/class-PlayerSettingsWebGL`, `WhatsNewUnity66` | budget <= 1 GB heap; wasm32 |
| **WebGPU is no longer experimental** in 6.6: compute shaders, indirect rendering, GPU skinning, VFX Graph; no async compute, no sync readback (`AsyncGPUReadback` only), no RWBuffer (StructuredBuffer yes), strict uniformity; WebGL2 stays the default and can be a fallback in the API list | `Manual/WebGPU-features`, `WebGPU-limitations`, `WebGPU-enable`, `WhatsNewUnity66` | WebGPU first, WebGL2 fallback; nothing essential may require compute until WebGL2 is dropped |
| Browsers: WebGPU on by default in Chrome/Edge (113+), Firefox 141+ (Windows), Safari 26; Chrome Android 12+ on recent GPUs; not Firefox Android | web reports, 2026 | WebGPU reaches most desktop players |
| **Entities Graphics: Web platforms "Not supported"** (URP and HDRP) | `com.unity.entities.graphics/Documentation~/requirements-and-compatibility.md` | ECS rendering is not an option on our target |
| **BatchRendererGroup** platform list has no Web entry (DX11/12, Vulkan, Metal, GLES on Android, consoles) | `Manual/batch-renderer-group-how` | no BRG / GPU Resident Drawer on the Web; use `Graphics.RenderMeshInstanced` / `RenderMeshPrimitives` with instance data in textures or buffers |
| Entities 6.6 is a **core package** since 6.4 (versioned with the editor); Burst is a **built-in module** (`Unity.Burst`) in 6.6; Collections 6.6, Mathematics 1.4, URP 17.6, Test Framework 1.8, Performance Testing are core | `BuiltInPackages/*/package.json`, `WhatsNewUnity66` | no registry downloads needed for the core stack |
| Burst `FloatMode.Default` = Strict (no reordering); **`FloatMode.Deterministic`** gives cross-platform-identical results "on 64-bit architectures" (flushes denormals) | `Manual/burst/float-precision-determinism` | Burst kernels can match managed reference kernels if the operation order is identical; wasm32 support of Deterministic unverified |
| `Application.persistentDataPath` on the Web is `/idbfs/<MD5 of the page's directory URL>` | `ScriptReference/Application-persistentDataPath` | the path changes whenever the hosting URL changes (itch serves every upload from a new path) -> our own IndexedDB store with a fixed name |
| Safari does not support IndexedDB inside iframes | `Manual/webgl-browsercompatibility` | an in-memory fallback + export/import of the save as a file |
| C# 9.0 (Roslyn), Boehm GC (incremental by default; non-moving) | `Manual/csharp-compiler` | Core targets C# 9 / netstandard2.1; pinned arrays are stable |
| WebAssembly 2023 target (SIMD, bulk memory, native exceptions, non-trapping float->int) is the default | `Manual/class-PlayerSettingsWebGL` | keep it on; double->int casts saturate instead of trapping, but casts must still be range-checked (C# leaves them unspecified) |
| Domain reload on entering Play mode is **off by default** in 6.6 (preparing removal of Mono) | `WhatsNewUnity66` | static state must be reset explicitly; Core keeps no static mutable state |
| Test Framework CLI: `-runTests -testPlatform EditMode|PlayMode|<BuildTarget> -testResults <xml> -testFilter -testCategory -assemblyNames` (no `-quit` while tests run) | `Manual/test-framework/reference-command-line` | headless CI from scripts |

## 3. Simulation architecture

### 3.1 Discrete-event simulation (DES) and the future event list
- (I) Event lists: binary/d-ary heaps are O(log n) and robust; calendar queues (Brown 1988) and ladder
  queues are O(1) amortised but sensitive to the timestamp distribution and can be slower than heaps in
  practice (Furfaro et al., adaptive ladder queue). Lazy cancellation (mark stale, discard on pop) avoids
  heap deletions. Conservative synchronisation (Fujimoto) needs lookahead; Time Warp needs rollback.
- (G) One logical process (the world), single-threaded event loop; parallelism only inside data-parallel
  kernels whose results are applied at their own event time.
- (D) A 4-ary min-heap of plain structs keyed by `(time, phase, seq)`; cancellation by an entity version
  stamp; far-future events (> ~1 year) in a coarse bucket list to keep the heap small. Measured, not assumed.

### 3.2 Level of detail for agents ("simulation LOD")
- (I) Brockington 2002 (*Level-of-detail AI for a large role-playing game*, AI Game Programming Wisdom):
  NPCs far from the player run cheaper behaviour. Osborne & Dickinson 2010: grouped hierarchical LOD.
  **Sunshine-Hill & Badler 2010, "Perceptually Realistic Behavior through Alibi Generation"** (AIIDE 6):
  simulate cheaply when unobserved and, when scrutiny arrives, *retroactively elaborate* a history
  ("alibi") consistent with what could have been seen. DeAngelis, Scheffer et al. 1995, **super-individuals**:
  one model individual represents *n* real ones, so the same model zooms from individuals to cohorts.
- (G) Our variant keeps the **decision level authoritative everywhere** and makes only the *motor detail*
  observation-dependent: what someone does, where, with what, from when to when and with what effect is
  simulated for everyone always; *how their feet move along the street* is elaborated only when watched,
  constrained to the authoritative schedule. Rare physical processes (fire, flood, fights) are always
  simulated at fine resolution, observed or not. So watching never changes history (no observer effect),
  and determinism does not depend on the camera.
- (D) Three authoritative layers + one presentation layer (`04_TIME_AND_SCHEDULER.md`): PROCESS (fine,
  sparse), ACTIVITY (event-driven, everyone), COARSE (deep-time, households by season; later), and
  ELABORATION (presentation only). Super-individuals for wildlife herds (count + trait distribution).

### 3.3 Space-time intersection (interrupting a journey)
- (I) Kinetic data structures and swept-volume broad phases: index moving objects by the cells their
  trajectory crosses, with entry/exit times.
- (D) Every travel leg registers `(agent, activity version, cell, t_enter, t_exit)` in a planet-cell index
  (and a local index inside settlements). A hazard appearing at cell *c* queries the cells within its
  perception radius for legs with `t_exit > now`, and schedules for each an *interrupt* at the moment the
  traveller would first perceive it. Agents starting a journey only know hazards they perceived or heard
  of (knowledge, not omniscience).

### 3.4 Determinism in C# across runtimes
- (I) IEEE-754 guarantees correctly rounded `+ - * / sqrt`; transcendental functions are library-defined
  and differ between CRTs; FMA contraction and x87 extended precision break equality; .NET's
  `string.GetHashCode` is randomised per process; `Dictionary` enumeration order is implementation-defined.
  Older Mono could evaluate `float` arithmetic in double precision.
- (D) Sim arithmetic in `double`; storage may be `float` (conversion is deterministic); our own
  `DMath` (sin, cos, exp, log, pow, atan2, tanh) from `+ - * /`, `Math.Sqrt`, `Math.Floor`; integer time;
  counter-based hashing RNG (SplitMix64 finaliser keyed by seed, stream, entity, counter); total-order
  comparers for every sort; no iteration over hash containers in sim logic; a lint script forbids
  `Math.Sin/Cos/Tan/Exp/Log/Pow/Atan*`, `System.Random`, `DateTime`, `Stopwatch`, `GetHashCode()` in Core.
  Verified by state hashes compared across .NET 8 (RyuJIT), the Unity editor (Mono) and the Web build
  (IL2CPP -> wasm).

## 4. History, causality and narrative

- (I) **Dwarf Fortress** world generation simulates centuries of civilisations, historical figures and
  events before play, browsable in Legends mode (Adams; DF wiki "World gen"). **Story sifting**: James
  Ryan's *Curating Simulated Storyworlds* (2018) — "overgenerate and curate": interesting stories are found
  after the fact in the mass of simulated events; **Felt** (Kreminski et al. 2019) and **Winnow**
  (Kreminski, Dickinson, Mateas, AIIDE 2021) specify sifting patterns that run *incrementally*, detecting
  story-shaped sequences as they unfold (and partial matches). Caves of Qud's mythic histories (Grinblat)
  rationalise generated events into causal narrative. TC1: blame tags, detectors, deeds with `answers`.
- (G) History is *recorded truth plus after-the-fact interpretation*: the simulation writes events with
  causes; historians (observers) sift patterns into stories and eras; names come from what happened.
- (D) `11_HISTORY_AND_CAUSALITY.md`: an append-only ledger with tiers, causal links, importance that
  grows with descendants, compaction, and incremental sifting patterns for news and eras.

## 5. Planet

### 5.1 Grid
- (I) Climate models use cubed-sphere grids (e.g. GFDL FV3) for uniformity without pole singularities;
  equiangular gnomonic projection keeps cell areas within ~1.3x. Icosahedral grids are more uniform but
  awkward for textures and quadtrees.
- (D) Equiangular cubed sphere, 6 faces x N x N. Levels: N32 (6,144 cells) climate; N64 (24,576) surface
  systems; N256+ render heightmaps derived from sim elevation plus deterministic detail.

### 5.2 Plate tectonics
- (I) **Cortial, Peytavie, Galin, Guérin 2019, "Procedural Tectonic Planets"** (CGF 38(2), Eurographics):
  plates as rigid rotations of crust samples; subduction, continental collision and rifting deform the
  lithosphere into continents, ridges, ranges and island arcs, interactively. Viitanen 2012 (PlaTec),
  Tectonics.js (Davidson) as simpler simulations.
- (G) A young, restless tiny world: plates move ~1 surface cell per few centuries; collisions raise
  ranges over centuries; volcanic arcs and hotspots build islands; rifts open seas over millennia.
- (D) Per cell: plate id, crust kind and thickness, elevation, age. Plates rotate about Euler poles; a
  semi-Lagrangian crust advection every tectonic step; boundary classification by relative velocity;
  uplift, subduction, ridge creation; stress accumulation feeding quakes and volcano pressure.

### 5.3 Erosion and landscape evolution
- (I) Stream power law `dz/dt = U - K A^m S^n`; **Braun & Willett 2013** (*Geomorphology* 180-181):
  an O(n), implicit, parallel solution on a drainage tree, stable for any time step (FastScape, Landlab's
  `FastscapeEroder`). Cordonnier et al. 2016 (uplift + fluvial erosion for terrain generation).
  Priority-flood depression filling (Barnes et al. 2014) gives drainage on any terrain; lakes are
  depressions that fill.
- (D) Decadal erosion steps on N64 with the Braun-Willett scheme over the drainage DAG; hillslope
  diffusion; sediment deposition at basins and coasts; lakes from priority flood with spill.

### 5.4 Climate
- (I) **Budyko-Sellers energy balance models** (1969): latitude-dependent insolation balanced by OLR
  `A + B T`, meridional diffusive transport, ice-albedo feedback -> multiple equilibria, hysteresis and
  snowball states. Carbon-silicate weathering thermostat (Walker, Hays & Kasting 1981). Orbital forcing
  (Milankovitch) paces glacial cycles. Weather generators (Richardson 1981 WGEN): Markov wet/dry days with
  gamma amounts downscale coarse climate to daily weather (used in TC1).
- (G) A per-cell energy balance with seasonal insolation, ocean heat capacity, ice albedo, a
  greenhouse term from CO2, aerosols from eruptions/impacts; fictional orbital cycles of ~500, ~1,200 and
  ~3,000 years so ice ages come and go within playable history.
- (D) N32 climate step daily in watched time, coarser in catch-up/deep time; moisture advection by
  prevailing wind bands; precipitation by capacity; stochastic daily weather and storm entities.

## 6. Ecology and evolution

- (I) Lotka-Volterra with Holling type II predation and prey refuges; metapopulations (Levins) with
  dispersal between patches; succession from grass to shrub to forest over decades (TC1 notes: canopy in
  ~50 years); dynamic global vegetation models (LPJ) use plant functional types (PFTs) per cell.
  Quantitative genetics: the breeder's equation `R = h^2 S`, Lande's multivariate response; adaptive
  dynamics; allopatric speciation when gene flow stops and divergence accumulates.
- (G) Fast fictional evolution: generation times of 1-5 years and large heritable variance let trait
  means shift visibly in a century under selection (coat vs cold, size vs predation, diet breadth);
  isolated populations diverge and become new species after a divergence threshold.
- (D) Per cell and species: abundance by age class; per regional population: trait means and variances
  updated by selection differentials each breeding season; speciation and extinction as historical events.
  Wildlife herds and packs near people are group agents (super-individuals) drawn from and returned to
  the fields; livestock are persistent individuals.

## 7. People, societies, knowledge

- Households and subsistence (Artificial Anasazi; Chayanov's labour-consumer balance), demography
  (Coale-Demeny tables, natural fertility, famine coping sequence), Hajnal household formation, social
  storage (Halstead & O'Shea 1989), conflict (Keeley 1996), state formation by circumscription (Carneiro
  1970) and settlement tiers (Wright & Johnson 1975): all in TC1's research and still the basis.
- **Turchin, Currie, Turner & Gavrilets 2013** (PNAS): large-scale societies arose where warfare was
  intense (diffusing military technology, steppe frontiers, rugged land); a cultural-evolution model on a
  real map explained 65% of variance in the spread of large polities (16% without technology diffusion).
  -> polities in TC2 emerge from conflict pressure, the diffusion of real military knowledge and geography.
- **Henrich 2004** (*American Antiquity* 69(2)): a population that loses effective size (Tasmania cut off
  by sea level) keeps simple skills and loses complex ones, because learners copy imperfectly and complex
  skills need many skilled models. -> knowledge in TC2 lives in people; it decays when learners and
  teachers become scarce; complexity raises the threshold.
- **Arthur & Polak 2006** (*Complexity* 11(5)): technology evolves by *combining* existing technologies
  (NAND gates to 8-bit adders), not by mutation. -> discoveries are combinations of known techniques,
  materials and situations (the "adjacent possible").
- Disease: SEIR compartments per settlement with travel coupling (metapopulation SEIR; infection along
  routes and at gatherings); pathogens as lineages with transmissibility/virulence traits that mutate
  (virulence-transmission trade-off). Gatherings (fairs, festivals, armies) are where epidemics jump.

## 8. Settlements and construction

- (I) **Emilien, Bernhardt, Peytavie, Cani, Galin 2012, "Procedural generation of villages on arbitrary
  terrains"** (*The Visual Computer*): interest maps seed houses and roads (roads attract settlers, houses
  extend roads), anisotropic land conquest makes parcels, open shape grammars adapt buildings to slope.
  Defensive aggregation and fortification follow repeated attack (TC1 research A5).
- (G) Growth by individual decisions (a household needs a home; a lot near a road, water and fields),
  not templates; fortifications from remembered harm, labour, materials and knowledge; modular kits.
- (D) A settlement layout = parcels + buildings (state machines of construction) + roads graph; the
  renderer assembles kit pieces from building descriptors (`13_RENDERING_AND_ART.md`).

## 9. Persistence on the Web

- IndexedDB is per origin; itch serves all games from one origin (`html-classic.itch.zone`) with a new
  path per upload, so names must be namespaced and must not depend on the path. Quotas are generous;
  `navigator.storage.persist()` asks the browser not to evict. Saving on `visibilitychange` (hidden) is the
  only reliable "on quit" hook. Wavedash: Unity SDK `Wavedash.SDK.Init()` (git package
  `https://github.com/wvdsh/sdk-unity.git`), Remote Storage for per-player files (`docs.wavedash.com/sdk/remote-storage`).

## 10. Sources

- Sunshine-Hill & Badler (2010), Perceptually Realistic Behavior through Alibi Generation, AIIDE 6: https://ojs.aaai.org/index.php/AIIDE/article/view/12389
- DeAngelis, Scheffer, Rose, Baveco, van Nes (1995), Super-individuals, Ecological Modelling: https://catalog.comses.net/publications/50136
- Brockington (2002), Level-of-detail AI for a large role-playing game, AI Game Programming Wisdom 1:419-425
- Brown (1988), Calendar queues, CACM: https://www.maths.tcd.ie/~eoin/index/brown_calendar88.html ; Fujimoto, future event list notes: https://sigsim.acm.org/conf/mskr/Courseware/Fujimoto/Slides/FujimotoSlides-03-FutureEventList.pdf ; adaptive ladder queue: https://angelo.furfaro.dimes.unical.it/node/139
- Kreminski et al., Winnow (AIIDE 2021): https://ojs.aaai.org/index.php/AIIDE/article/view/18903 ; Felt: https://github.com/mkremins/felt
- Dwarf Fortress world generation: https://dwarffortresswiki.org/index.php/DF2014:World_gen
- Cortial et al. (2019), Procedural Tectonic Planets, CGF 38(2): https://diglib.eg.org/handle/10.1111/cgf13614
- Braun & Willett (2013), O(n) implicit stream power: https://esurf.copernicus.org/preprints/1/891/2013/esurfd-1-891-2013.pdf ; Landlab FastscapeEroder: https://landlab.readthedocs.io/en/latest/reference/components/stream_power.html
- Budyko-Sellers EBMs: https://arxiv.org/pdf/1406.6028 ; snowball tutorial: https://www.pik-potsdam.de/members/willeit/klimageschichte-ubungen/snowball/tutorial-snowball.pdf
- Henrich (2004), Demography and Cultural Evolution: https://www2.psych.ubc.ca/~henrich/Website/Papers/HenrichTasmania.pdf
- Turchin et al. (2013), War, space, and the evolution of Old World complex societies, PNAS: https://pmc.ncbi.nlm.nih.gov/articles/PMC3799307
- Arthur & Polak (2006), The evolution of technology within a simple computer model, Complexity 11(5):23-31
- Emilien et al. (2012), Procedural generation of villages on arbitrary terrains: https://perso.liris.cnrs.fr/egalin/Articles/2012-villages.pdf
- Metapopulation SEIR with travel: https://arxiv.org/pdf/2107.02430
- WebGPU support 2026: https://app.cinevva.com/guides/webgpu-vs-webgl-games
- itch.io SharedArrayBuffer option: https://itch.io/t/2805151/is-embed-game-with-sharedbufferarray-not-supported , https://bugnet.io/blog/how-to-fix-web-game-coop-coep-headers-sharedarraybuffer
- Wavedash Unity: https://docs.wavedash.com/engines/unity ; remote storage: https://docs.wavedash.com/sdk/remote-storage
- Unity 2018 WebAssembly benchmark (historic: Burst 35% faster than IL2CPP in one test): https://unity.com/blog/engine-platform/webassembly-load-times-and-performance
