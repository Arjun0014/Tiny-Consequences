# TC2 Session handoff

## Session 2 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits (newest last): 9e18da6 compaction hooks · 321bcf9 F3 slice 1 wild food (D2-042) · 6522b94 relief, ice,
  densities (D2-043/044) · 5f6c9e0 F3 slice 2 building, knowing places, parties (D2-045) · 8405b5e housekeeping speed ·
  fb48f58 F1 closed · 46631fa herds near people (D2-047) · 6b0903d livestock (D2-048) + herd index + `--nopeople` fix.
- **Uncommitted, tested (101 tests green)**: the minimum-viable-population law (`Planet/Evolution.cs` `MinViable`: a kind under
  ~50 real animals may die out by chance each tectonic step) with `EvolutionTests.AKindTooFewDiesOutByChanceAThrivingKindNever`
  and two test premises adjusted (`EvolutionTests` thriving kinds, `VolcanismTests` skips planet-step events). Its 10,000-year
  validation runs in the background: `tc2/out/f1/run_42m.*` then `run_7m.*` (scratch binary `scratchpad/bin_f2m`; ~25 min each).
  When they finish: compare species over time with `run_42k`/`run_7k` (D2-046 without the law), write **D2-046** in DECISIONS
  (sister kinds share territory, drift 0.015, gentler split chance, minimum viable population; evidence from the k and m runs),
  then commit Evolution.cs + the two test files.
- F1 CLOSED (fb48f58). F2: done-when met (cycles, recovery, adaptation, speciation) - verdict in PROGRESS; turnover pending the m runs.
- F3: wild food, reserves, famine memory (D2-042); building, scouting, moving, founding (D2-045); herds near people (D2-047);
  livestock (D2-048). Next F3 items: a herding/tending activity (people's hours), pens and fences, workshops, maintenance, ruins.
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

### Next steps
1. When `run_42m`/`run_7m` finish: D2-046 entry, commit the minimum-viable-population law (see Exact state).
2. Visual slice for F3: cow/goat/pig with the kit's `quadruped()` (same style as the wild animals); draw each household's
   animals near its home and on the pasture (positions elaborated from the beast id; never written back), wild herds from the
   herd identities, a tent for camps; check `WorldObjectsView` compiles in Unity (`unity.mjs test`), then `unity.mjs shots`
   (village, close, low) and look at the PNGs.
3. Look track backlog: seals in a daylight shot, coast shapes off the cell grid (never sink a coastal village), boats, fences
   and gardens, orbit detail; Web build + WebGL2 fallback check (`build-web --webgl2`, `webtest --webgl2`) with fps per view.
4. F3 next slices: a tending/herding activity (people's hours; children as herders), pens and fences, the survey's cost
   (77 us per village-day: `HerdSystem.Update`'s new-herd scan), long runs with people (`run --coarse --places` 300-1,000
   years: saturation, collapse possible, abandonment), the Web catch-up benchmark with livestock.
