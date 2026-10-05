# TC2 Session handoff

## Session 1 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Commits (newest last): a2283fa docs · 18cd0cc Core + POC · 50e6baa activity layer · c478f96 Unity host ·
  4d7705a land and people · 8a8ea3b Web verified + COARSE · a5c5c50 hydrology · 30464c4 climate · 19d7079 tectonics ·
  2fbf6d4 look-dev v1 · b8f1593 v2 · c870730 v3 · 6848601 v4 · 80b99ce volcanoes + carbon cycle · 39fdcd1 quakes +
  look-dev v5 · a9149c0 erosion + storms (then look-dev v6: smoke, rain, glint).
- Headless: 62 tests green (`node tc2/scripts/headless.mjs test`).
- Web: 6/6 each build (start, browser == headless bit for bit, time, reload, a day away, no console errors); 89-124
  fps by view (Chrome/WebGPU, RX 5600M, 1280x720).
- F1 status: hydrology, climate, tectonics, volcanoes + carbon, quakes, erosion, storms done; the 10,000-year
  untouched runs (seeds 42 with maps, 7, 1234) write to `tc2/out/f1/run_<seed>.txt` and `tc2/out/f1/maps42/`.
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
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low,volcano,volcanoclose,volcanofar] [--hour 8] [--seed N] [--erupt VEI] [--quake M] [--storm WIND]` -> `tc2/out/shots/T/*.png` (~40 s; the god commands are logged commands) |
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
  planet: `Planet/{PlanetState,PlanetSystems,Climate,Tectonics,Drainage,FireSystem,CubeSphere,Volcanism,Quakes,Erosion,Storms}.cs`;
  `Simulation/Fx.cs` (happenings for the view, never read by the sim).
- Game: `.../Game/{Host,View,UI,Platform}`; view: `View/{Ground,TerrainLod,PlanetView,WaterView,CloudView,Sky,TiltShiftFeature,WorldObjectsView,Models,Instancer,VolcanoShape,VolcanoView,Puffs,RainView}.cs`;
  shaders `Game/Resources/TC2Shaders/{Terrain,Water,Instanced,Atmosphere,Clouds,TiltShift,Puff,Rain}.shader`; materials
  `Game/Resources/TC2Materials` (created by ProjectSetup); models `Game/Resources/TC2Models/*.fbx` (from kit.py).
- Editor: `.../Editor/{ProjectSetup,BuildScript,BatchTools}.cs` (BatchTools.PlayShots = the screenshot tool).
- Tests: `.../Tests/Core/{FoundationTests,SimulationTests,PlanetTests,OfflineTests,ClimateTests,TectonicsTests,VolcanismTests,QuakeTests,ErosionTests,StormTests}.cs`.

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

### Next steps
1. Read the 10,000-year runs (`tc2/out/f1/`): drift, ranges rising and wearing, a glacial cycle, coastline change,
   forests moving, stability over seeds; judge the maps; record the F1 verdict in PROGRESS and close F1.
2. F2 ecology per the roadmap (species as data, abundance fields, herds and packs near people, visible animals) -
   also the next big step for the living look (animals in the miniature).
3. Look: boats and fences/gardens with F3; orbit detail; WebGL2 path check.
4. F3: households that move between villages and found new ones (villages empty within centuries today).
