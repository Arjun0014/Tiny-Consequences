# TC2 Session handoff

## Session 2 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits since the last checkpoint (newest last): 314fdd9 techniques (D2-062) · c8e9c23, 07b38be materials (D2-063) ·
  6bacfaa copper, F4 closed (D2-064/065) · 27d2a53 sickness (D2-066) · e01a933 trade and relief, F5 closed (D2-067/068) ·
  b78117d deeds and taking (D2-069, F6 slice 1) · the commit with sickness at scale (D2-070).
- Phases: F0 done; **F1 to F5 closed** (D2-043, D2-046, D2-060, D2-065, D2-068); **F6 in progress** (plan 10 §6). 148 headless
  tests green; `headless.mjs test` runs the float lint first. Sim version 24.
- Determinism: dotnet equals the editor's Mono (gen cb0333f12013579a, D2-070); the Web build equals both at D2-068
  (`tc2/out/webtest/s2webe`, `s2webe2`: 6/6).
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

### Next steps
1. **A 1,000-year run of seed 42 on D2-070** (`tc2/out/society/sick42_1000.txt`): populations hold, plagues come and go, the
   ledger and the cost at scale; compare with the pre-sickness run (`tc2/out/long/run42_1000*.txt`: 12,000-14,800 people).
2. **F6 slice 2 - the watch, blows and injury** (10 §6): a village that remembers losses keeps a night watch (labour, sleep); a
   taker met is struck or chased; injury that heals with care or kills; a death by violence is a deed with mourners.
3. F6 slices 3-7 (10 §6): answers and news, raids, defence built, prestige and leaders, tribute and polities.
4. **The sea's calibration**: fish at ~1 % of real biomass, so the miniature holds ~10-20 seals (D2-053).
5. Look and Web: the after-a-century view at 48-68 fps (s2webe: measure layer by layer with `webperf.mjs`); WebGL2's first-entry
   stall; coast shapes off the cell grid, gardens, orbit detail, smoke from huts and camps; traders and takers on the roads.
6. **An aggregate layer of people for Ancient and Deep time** (D2-061), once F6's first slices settle the people model.
7. F5's carried-forward items (D2-068): boats between coasts, roads worn by use, fairs, salt, credit, relief by kinship.
