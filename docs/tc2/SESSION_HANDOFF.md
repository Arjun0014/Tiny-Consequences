# TC2 Session handoff

## Session 1 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Commits (newest last): a2283fa docs · 18cd0cc Core + POC · 50e6baa activity layer · c478f96 Unity host ·
  4d7705a land and people · 8a8ea3b Web verified + COARSE · a5c5c50 hydrology · 30464c4 climate · 19d7079 tectonics ·
  2fbf6d4 look-dev v1 · b8f1593 v2 · c870730 v3 · 6848601 v4 · 80b99ce volcanoes + carbon cycle · 39fdcd1 quakes +
  look-dev v5 · a9149c0 erosion + storms · 4ea987a look-dev v6 · 8eab3ff coasts, thermostat, wildlife ·
  386e76d ice-aware weathering + evolution · 9e8122f crag fare, refuges, lineage drift · 3aa1321 the sea's life ·
  e3b5f33 lineages in sizeable populations · 63da6b4 water by depth + sea life view · 9d925b6 kinds one at a time.
- Headless: 73 tests green (`node tc2/scripts/headless.mjs test`).
- Web: 6/6 each build (start, browser == headless bit for bit, time, reload, a day away, no console errors); last
  `tc2/out/webtest/sea2`: start 6.1 s, orbit/region 120, village 88, close 98, low 103 fps; a day away 27 s.
- F1 status: all systems done; 10,000-year evidence in PROGRESS (seed 7: a full glacial cycle; seed 42: a long
  glaciation, stable). Final reruns on the current build: `tc2/out/f1/run_42h.txt`, `run_7h.txt` (maps `maps42h/`,
  `maps7h/`), run from a scratch copy of the headless binaries (so the repo's DLL stays free for builds and Web tests).
- F2 status: ecology (13 kinds, land and sea), evolution (coats, lineages, speciation by chance, extinction) done;
  herds as agents near people and hunting/fishing wait for F3.
  Known weakness: villages empty within centuries in coarse runs (no migration or new villages before F3/F5).
- Uncommitted legacy TC1 work may exist in the tree and is NOT ours: `game/html/engine_template.html` (S25g mouse
  wheel) and `docs/DECISIONS.md` (D-227). Leave both untouched; never stage them in [tc2] commits.

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out DIR] [--why] [--profile] [--coarse]` |
| Planet over time (+maps) | `node tc2/scripts/headless.mjs planet --seed 42 --years 3000 --every 9600 --nopeople --coarse [--tect] [--volc] [--quake] [--storms] [--erosion] [--map DIR --zoom first:55]` |
| Climate alone (fast) | `node tc2/scripts/headless.mjs climate --seed 42 --years 4000 --every 200 [--co2 560]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save] [--coarse]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` (after new materials/features) / `node tc2/scripts/unity.mjs test` |
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low,volcano,volcanoclose,volcanofar,wild,wildfar,coast,coastfar,seals,sealsfar] [--hour 8] [--days D] [--seed N] [--erupt VEI] [--quake M] [--storm WIND]` -> `tc2/out/shots/T/*.png` (~40 s; the log prints sea-life and wildlife counts per shot) |
| Ecology diagnostics | `planet --seed 42 --years 200 --every 960 --coarse --eco [--ecodiag] [--ecocsv FILE] [--steep]` (deaths by cause and births, yearly series, steepness) |
| Asset kit | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--preview tc2/out/kit]` |
| Web build | `node tc2/scripts/unity.mjs build-web [--dev] [--threads] [--webgl2]` -> `tc2/web-build/web` (5-9 min) |
| Serve | `node tc2/scripts/serve.mjs` (http://127.0.0.1:8642, COOP/COEP on) |
| Web test | `node tc2/scripts/webtest.mjs --tag T [--webgl2]` (system Chrome; fps per view in report.json `perf`) |
| In-browser sim benchmark | `node tc2/scripts/webbench.mjs [--sets "6x24x20:activity,..."]` |
| URL params | `?fresh=1`, `?seed=N`, `?speed=I`, `?absence=24h`, `?view=close|village|region`, `?gfx=webgl2` |

### Important paths
- Unity editor 6000.6.4f1: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`; bundled dotnet in
  `.../Editor/Data/DotNetSdk/dotnet.exe`; local manuals `.../Editor/Data/Documentation/en/`.
- Core: `unity/TinyConsequences2/Assets/TC2/Core/{Foundation,Scheduling,Simulation,Planet,Places,Agents,History,Persistence,Diagnostics,Worldgen}`;
  planet: `Planet/{PlanetState,PlanetSystems,Climate,Tectonics,Drainage,FireSystem,CubeSphere,Volcanism,Quakes,Erosion,Storms,Coast,Ecology,Evolution}.cs`;
  `Simulation/Fx.cs` (happenings for the view, never read by the sim).
- Game: `.../Game/{Host,View,UI,Platform}`; view: `View/{Ground,TerrainLod,PlanetView,WaterView,CloudView,Sky,TiltShiftFeature,WorldObjectsView,Models,Instancer,VolcanoShape,VolcanoView,Puffs,RainView,WildlifeView,SeaLifeView}.cs`;
  shaders `Game/Resources/TC2Shaders/{Terrain,Water,Instanced,Atmosphere,Clouds,TiltShift,Puff,Rain,Underwater}.shader`; materials
  `Game/Resources/TC2Materials` (created by ProjectSetup); models `Game/Resources/TC2Models/*.fbx` (from kit.py).
- Editor: `.../Editor/{ProjectSetup,BuildScript,BatchTools}.cs` (BatchTools.PlayShots = the screenshot tool).
- Tests: `.../Tests/Core/{FoundationTests,SimulationTests,PlanetTests,OfflineTests,ClimateTests,TectonicsTests,VolcanismTests,QuakeTests,ErosionTests,StormTests,CoastTests,EcologyTests,EvolutionTests}.cs`.

### Pitfalls found
- Bash heredocs containing apostrophes break: write patch scripts with the Write tool (`scratchpad/patch_*.py`, idempotent `rep`).
- C# 9: no struct field initializers; the headless netstandard2.1 build catches what Unity would reject.
- Integer seconds for wake/bed times; precompute per-cell transcendentals; a waiting traveller needs `Until`.
- Unity can exit 0 with compile errors: unity.mjs fails on `error CS`. `Resolution` is ambiguous with UnityEngine.Resolution in Game code.
- Derived state must be derived from exactly what a load sees (float-stored values, the coastline level, the drainage's ice snapshot).
- Unity's `Mathf.SmoothStep(from, to, t)` is NOT GLSL smoothstep - use `TerrainLod.Edge`.
- `MaterialPropertyBlock.SetColor` converts sRGB->linear itself; mesh vertex colours are raw (linear).
- A material asset keeps its first-saved property values: set the look in code (PlanetView sets the water's).
- In batch mode `WaitForEndOfFrame` never fires; instanced draws are queued in LateUpdate (capture after them).
- A new world must cancel a running catch-up (fixed in SimHost.NewWorld).
- Relief may change only where the drainage is rebuilt (inside the tectonic step, or with LandChanged at once): a load
  rebuilds the tree from the relief it sees. Cones wait for the tectonic step; landslides rebuild at once.
- The headless DLL is the Web test's reference: never rebuild it between a Web build and its test; a running
  10,000-year headless run locks it (no rebuilds until it ends).
- Weather is the coarse year's cost (~0.8 ms a day); profile with `run --coarse --profile` before guessing.
- Tectonic steps come every 10 years (`Tectonics.StepYears`), not 50: anything run per tectonic step (speciation,
  lineage drift) must be tuned to that.
- Species are found by kind or id, never by a remembered index: extinct kinds are compacted out of the fields.
- Caches derived from the planet must be keyed by everything they read (the sea geometry reads the sea level, not only
  the coastline version), or a loaded world differs from the live one.
- The scene depth texture is off (D2-026): water colour by depth lives in the terrain shader (`below` from
  `_TC2SeaRadius`), and things under the water veil themselves by their depth (`Underwater.shader`).
- `Instancer` takes runs of one group cheaply; interleave groups as little as possible when adding thousands.

### Next steps
1. Read the final 10,000-year reruns (`tc2/out/f1/run_42h.txt`, `run_7h.txt`, maps): record the F1 and F2 verdicts in
   PROGRESS (speciation one at a time? kinds kept? climate stable?) and close both phases.
2. F3: households that move between villages and found new ones (villages empty within centuries today); hunting and
   fishing from the fields (herds as agents near people); livestock.
3. Look: seals in a daylight shot (the planet clock is global: pick the hour for the seal shore's longitude), boats and
   fences/gardens with F3, orbit detail, WebGL2 path check.
