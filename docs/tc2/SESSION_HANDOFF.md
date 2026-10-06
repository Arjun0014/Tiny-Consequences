# TC2 Session handoff

## Session 2 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits since the last checkpoint (newest last): 314fdd9 techniques (D2-062) · c8e9c23, 07b38be materials (D2-063) ·
  6bacfaa copper, F4 closed (D2-064/065) · 27d2a53 sickness (D2-066) · e01a933 trade and relief, F5 closed (D2-067/068) ·
  b78117d deeds and taking (D2-069, F6 slice 1) · 310ab38 sickness at scale (D2-070) · ceadd36 the watch, blows and injury
  (D2-071, F6 slice 2) · 0f2adde answers, news and taking back (D2-072, F6 slice 3) · 7121f7e raids (D2-073, F6 slice 4) · 41f5756 the palisade (D2-074, F6 slice 5) · ff6fab8 tribute and lordship (D2-075), F6 closed
  (D2-076) · 3d8f7b6 Web after F6 · the commit with the History Book (D2-077), F7 closed (D2-078) and the direction to make the
  living world visible (D2-079).
- Phases: F0 done; **F1 to F7 closed** (D2-043, D2-046, D2-060, D2-065, D2-068, D2-076, D2-078). 169
  headless tests green; `headless.mjs test` runs the float lint first. Sim version 30.
- Determinism: dotnet, the editor's Mono and the Web build (IL2CPP, WebGPU) are bit-identical at D2-077 (gen 9b6482ca9f9f606f,
  `tc2/out/webtest/s2webg`: 6/6).
- Sickness at scale is fixed (D2-070): seed 42 holds 12,000-16,000 people from year 270 to 400 and seed 7 4,600-5,400
  (`tc2/out/society/sick42_400.txt`, `sick7_400.txt`; the run report lists the deadliest sicknesses). Saturated worlds are
  famine-regulated (~100 hunger deaths a year) and take from each other's stores often (~30-75 loads a year): F6's pressure.
- Nothing running. Uncommitted legacy TC1 work may exist in the tree and is NOT ours: `game/html/engine_template.html` and
  `docs/DECISIONS.md` (D-227). Never stage them in [tc2] commits.

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` (~110 s for all 101) |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out DIR] [--why] [--profile] [--coarse]` |
| Villages over time | `run --seed 42 --years 200 --report-days 2400 --tier 9 --coarse --places --profile` (per village: people, fields, granary, ration, wild grounds, **stock and pasture** offered/wanted, herds; then the stock line, **deaths by kind and cause**, the profile with `daily.*` parts) |
| Planet over time | `planet --seed 42 --years 3000 --every 9600 --nopeople --coarse [--tect] [--volc] [--eco] [--csv F] [--ecocsv F] [--map DIR]` (each report line ends with elapsed seconds and the number of kinds) |
| 10,000-year runs | copy `tc2/headless/app/bin/Release/net8.0` to the scratchpad, then `dotnet <copy>/tc2.dll planet --seed 42 --years 10000 --every 4800 --coarse --nopeople --tect --volc --eco --csv tc2/out/f1/run_42X.csv --ecocsv tc2/out/f1/eco_42X.csv > tc2/out/f1/run_42X.txt` (~22-25 min each) |
| Climate alone (fast) | `node tc2/scripts/headless.mjs climate --seed 42 --years 4000 --every 200 [--co2 560]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save] [--coarse]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` / `node tc2/scripts/unity.mjs test` |
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low,volcano,...,coast,seals] [--hour 8] [--days D] [--seed N] [--erupt VEI] [--quake M] [--storm WIND]` -> `tc2/out/shots/T/*.png` |
| Asset kit | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--preview tc2/out/kit]` |
| Web build / serve / test | `node tc2/scripts/unity.mjs build-web [--webgl2]` (5-9 min) · `node tc2/scripts/serve.mjs` · `node tc2/scripts/webtest.mjs --tag T [--webgl2]` |
| URL params | `?fresh=1`, `?seed=N`, `?speed=I`, `?absence=24h`, `?view=close|village|region`, `?gfx=webgl2` |

### Important paths
- Unity editor 6000.6.4f1: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`; bundled dotnet in `.../Editor/Data/DotNetSdk/dotnet.exe`.
- Core: `unity/TinyConsequences2/Assets/TC2/Core/{Foundation,Scheduling,Simulation,Planet,Places,Agents,History,Persistence,Diagnostics,Worldgen}`;
  places: `Places/{Settlement,SettlementSystem,WildFood,Construction,Sites,Migration,Herds,Livestock}.cs`.
- Game view: `Game/View/{WorldObjectsView,WildlifeView,SeaLifeView,...}.cs`; kit models `Game/Resources/TC2Models/*.fbx` (from `tc2/art/kit.py`).
- Tests: `Tests/Core/*Tests.cs` (WildFood 11, Migration 5, Livestock 11, ...); `TestWorlds.Audit` checks people, queues and livestock lists.
- CC0 Quaternius farm animals (Blend/FBX) in `assets/3d-models/animals_quaternius/` (not used yet: the kit's own quadrupeds keep one style).

### Pitfalls found (session 2 additions; session 1's list still applies - see git history of this file)
- Grep build output for `error MSB` too: a locked DLL fails the copy with no `error CS`.
- A long headless run on the repo DLL locks it: run long jobs from a scratch copy.
- `planet --nopeople` used to found one village (`Math.Max(1, cfg.Settlements)`): runs before 6b0903d labelled planet-only may have people.
- Lookups that walk a whole store per agent-day (all herds, all settlements) are invisible at 6 villages and dominate at 30:
  index per village; profile a 200-300-year coarse run (`--profile`, `daily.*` parts) after adding any per-day lookup.
- `CellsWithin` keeps its answers per (cell, range) (the grid never changes); results are the same list in the same order.
- The village's day (`SettlementSystem.Daily`) runs at local midnight: `P.Temp` then is the night's (diurnal swing 5 C on land).
  Use `PlanetSystems.DayMean(c, PlanetSystems.Season(t))` for a day's mean.
- A test's premise can be undone by the world before the mechanism runs (hunters brought food home before the herd's day):
  call the system step directly when the test is about that step.
- A planet step (tectonic, evolution) can fall inside any test's hour: filter history checks by the kinds under test.
- Heredocs with apostrophes break: patch scripts go in `scratchpad/patch_*.py` written with the Write tool.
- `headless.mjs test --filter A|B` matches nothing (no alternation): run the filters one by one.
- Kit re-exports touch every FBX (new timestamps): restore the unchanged ones with
  `git diff --name-only -- .../TC2Models/ | xargs -r git checkout --` before committing; a glob also catches new untracked files.
- Any rule the simulation and the view must share (a herd's ground, the frost that pens the animals, which seals show) lives
  in Core or one static helper both call - never two copies.
- Test mechanisms, not demography: "mostly children herd" failed because only 1 of 19 households had a child of 8-15.
- **No float arithmetic in TC2.Core** (D2-057): `a + b` on two floats rounds differently under Mono; write `(double)a + b` and cast
  to float only when storing. `headless.mjs test` runs `tc2/tools/floatcheck` first and fails on any.
- A store whose ids come from a shared counter (process ids: herds, parties, fires, storms) is sparse: check `PagesHeld` and the
  cost of a pass over it at scale (D2-055).
- A new daily need (meals, water, fuel) changes what a "stocked" premise means in old tests: restate the premise (keep the store
  stocked), do not loosen the mechanism (D2-056).

- **Another Claude session may share this machine** (6 Oct: one on another project, with a gstack browse daemon whose headless
  Chromium renders WebGL with SwiftShader on ~7 cores). Its processes are not ours: never kill a process you did not start -
  check its parent first (`Get-CimInstance Win32_Process`) - and measure timings only when the machine is quiet.
- Claude Code stops background jobs when the machine runs critically low on memory (6 Oct: other sessions and their browsers
  shared it; the Web build and the local server were stopped). Check free memory before the Web build (it wants ~4-5 GB).
- The History Book in the game: B or the Book toggle; `?book=1` opens it; a fresh world has little history - reload a saved one
  with `?absence=24h` to live ~96 years first.

### Next steps
1. **The living world made visible (D2-079, Arjun's direction)** - slice 1 done (D2-080: the canopy and the near trees). Next:
   (a) the Web build and its frame rate with the woods (`webtest.mjs`, `webperf.mjs --view forest|forestlow|close`) - stopped once
   for lack of memory; (b) the far woods at the region view read as a faint speckle: keep the near crowns' shadows and the floor's
   occlusion out of the far blend (`farCol` from the litter without `shade`; `floorOcc` toward 1 - 0.25 x share with the far fade)
   and darken the mean crown (0.74 -> 0.62); (c) ground cover by biome (shrubs at wood edges, meadow flowers, heath), animals
   readable at the village view, the orbit's woods (`PlanetView`).
2. **F8 - Deep time** (17): Ancient and Deep time need ~10x: in COARSE the planet alone costs ~150 ms a world year (daily weather
   43, vegetation 54, housekeeping 43) and the village day ~620 us at ~500 people (coarse-day 252, learning 119 - each person's
   Gumbel scatter recomputed daily -, food 65, demography 65; `tc2/out/society/sick42_1000.txt` profile): a season step for
   households and coarser planet steps, fitted to COARSE by distribution tests (D2-061).
3. **Web performance**: the catch-up of a day away measured 48-54 s after F6 (s2webf, s2webf2 - both before anything else shared
   the machine, so the slowdown against 41-42 s is real): profile the browser's catch-up and the views layer by layer
   (`webperf.mjs`). Measure only on a quiet machine.
4. F7's carried-forward items (D2-078): Your Actions (with F9), timelines, news by incident, biography and polity pages, ages named
   apart. F6's (D2-076): lordships that hold, leaders, alliances, captives, gates and stone walls, bands of several villages,
   beasts as loot, the violence rate, the view of lordships and bands.
5. **The sea's calibration** (D2-053) and the look backlog (coast shapes, gardens, orbit detail, smoke from huts and camps).
6. F5's carried-forward items (D2-068): boats between coasts, roads worn by use, fairs, salt, credit, relief by kinship.
