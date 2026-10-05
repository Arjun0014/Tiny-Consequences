# TC2 Session handoff

## Session 1 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Commits (newest last): a2283fa docs · 18cd0cc Core + POC · 50e6baa activity layer · c478f96 Unity host ·
  4d7705a land and people · 8a8ea3b Web verified + COARSE · a5c5c50 hydrology · 30464c4 climate · 19d7079 tectonics ·
  2fbf6d4 look-dev v1 · b8f1593 look-dev v2 (clouds, smooth weather, tilt-shift, window light).
- Headless: 48 tests green (`node tc2/scripts/headless.mjs test`).
- Web: 6/6 (start, browser == headless bit for bit, time, reload, a day away, no console errors); look-dev v1 at
  118-125 fps at every view (Chrome/WebGPU, RX 5600M).
- Uncommitted legacy TC1 work exists in the tree and is NOT ours: `game/html/engine_template.html` (S25g mouse wheel)
  and `docs/DECISIONS.md` (D-227). Leave both untouched; never stage them in [tc2] commits.

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out DIR] [--why] [--profile] [--coarse]` |
| Planet over time (+maps) | `node tc2/scripts/headless.mjs planet --seed 42 --years 3000 --every 9600 --nopeople --coarse --tect [--map DIR --zoom first:55]` |
| Climate alone (fast) | `node tc2/scripts/headless.mjs climate --seed 42 --years 4000 --every 200 [--co2 560]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save] [--coarse]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` (after new materials/features) / `node tc2/scripts/unity.mjs test` |
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low] [--hour 8] [--seed N]` -> `tc2/out/shots/T/*.png` (~40 s) |
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
  planet: `Planet/{PlanetState,PlanetSystems,Climate,Tectonics,Drainage,FireSystem,CubeSphere}.cs`.
- Game: `.../Game/{Host,View,UI,Platform}`; view: `View/{Ground,TerrainLod,PlanetView,WaterView,CloudView,Sky,TiltShiftFeature,WorldObjectsView,Models,Instancer}.cs`;
  shaders `Game/Resources/TC2Shaders/{Terrain,Water,Instanced,Atmosphere,Clouds,TiltShift}.shader`; materials
  `Game/Resources/TC2Materials` (created by ProjectSetup); models `Game/Resources/TC2Models/*.fbx` (from kit.py).
- Editor: `.../Editor/{ProjectSetup,BuildScript,BatchTools}.cs` (BatchTools.PlayShots = the screenshot tool).
- Tests: `.../Tests/Core/{FoundationTests,SimulationTests,PlanetTests,OfflineTests,ClimateTests,TectonicsTests}.cs`.

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

### Next steps
1. Verify look-dev v2 in the browser (fps with tilt-shift/clouds/post; screenshots), WebGL2 path.
2. Look: ice-sheet edges (sawtooth at coasts), beach width variety, a bluer deep sea, grass and flowers close in,
   people walking animation, boats, roads between villages, fences/gardens, night ground light.
3. F1 remaining: river erosion (stream power) and sediment, volcanoes and quakes as events with consequences,
   carbon cycle (outgassing, weathering, forests), storms; a 10,000-year untouched run (F1 done-when).
4. Then F2 ecology (species as data, herds) per the roadmap.
