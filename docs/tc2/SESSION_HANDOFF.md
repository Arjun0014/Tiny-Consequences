# TC2 Session handoff

## Session 2 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits (newest last): 9e18da6 compaction hooks · 321bcf9 wild food (D2-042) · 6522b94 relief, ice, densities
  (D2-043/044) · 5f6c9e0 building, knowing places, parties (D2-045) · 8405b5e housekeeping speed · fb48f58 F1 closed ·
  46631fa herds near people (D2-047) · 6b0903d livestock (D2-048) · 4cf5170 handoff checkpoint · e57666b farm animals, herds and
  camps in the view (D2-049) · 7bfab6b herd claims array · a830732 the village herd and its herders (D2-050) · fdfa486 seal
  shots · 65794ab one frost rule · 43e12a7 **F2 closed (D2-046)**.
- Phases: F0 done, F1 closed (D2-043), F2 closed (D2-046), F3 in progress. 104 headless tests green.
- In flight at this checkpoint: the Web build (`node tc2/scripts/unity.mjs build-web` -> `tc2/out/build_web_s2.txt`), to be
  followed by `node tc2/scripts/webtest.mjs --tag s2web` (do not rebuild the headless DLL in between: the Web test compares
  hashes with it), then the WebGL2 fallback (`build-web --webgl2`, `webtest --tag s2webgl2 --webgl2`). Record start time, fps per
  view, a day away's catch-up time in PROGRESS Measurements (the last were session 1's: start 6.1 s, 88-120 fps, 27 s).
- Uncommitted legacy TC1 work may exist in the tree and is NOT ours: `game/html/engine_template.html` and `docs/DECISIONS.md`
  (D-227). Never stage them in [tc2] commits.

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

### Next steps
1. Finish the Web checks above (WebGPU and WebGL2): bit-identical hashes, fps per view, start time, a day away; fix what breaks.
2. Long runs with people: `run --seed 42 --years 1000 --report-days 4800 --tier 3 --coarse --places --profile` (and seed 7) -
   saturation, collapse and abandonment possible? herds over centuries? the cost of a crowded world (village-days at 30-60
   villages); Souhope re-checked.
3. Ecology calibration with the look: the sea's predators start ~10x below their level (spin-up) and the miniature's seals
   level at ~11-20 in all the world (food-limited: `planet --eco` reports; D2-039's functional response) - the look wants
   colonies on some shores.
4. F3 next: workshops and crafts, maintenance and decay of buildings, ruins of abandoned villages (08 §4), the survey's cost
   (`survey.herds` 41 us per COARSE village-day), a "herd" shot view (the village herd with its herders on the day's ground).
5. Look backlog: coast shapes off the cell grid (never sink a coastal village), boats, gardens, orbit detail; check the field
   patchwork in a top-down shot (D2-049 consequence).
