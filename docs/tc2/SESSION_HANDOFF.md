# TC2 Session handoff

## Session 2 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits since the last checkpoint (newest last): 4fb92e5 Web verified on both paths (D2-051) · 34d7699 ruins and land
  given back (D2-052) · 98ef641 the inspector's sentences and follow · c94a9c1 history compaction lets households' events go ·
  d436cef seals haul out together (D2-053, view) · e88ac31 fishing boats · 40a4ee9 fire steps without quadratic list work.
  Earlier this session: compaction hooks, wild food (D2-042), relief/ice/densities (D2-043/044), building and parties (D2-045),
  F1 closed, herds (D2-047), livestock (D2-048), farm animals in the view (D2-049), the village herd (D2-050), F2 closed (D2-046).
- Phases: F0 done, F1 closed, F2 closed, F3 in progress (its done-when looks met: a village lives untouched for centuries, the
  visible action is the simulation, the inspector tells a person's doing and why and follows them - close it after one more
  review of a followed life). 106 headless tests green. Web: WebGPU and WebGL2 6/6, bit-identical (D2-051).
- In flight: `tc2/out/long/run42_1000b.txt` (1,000 coarse years of seed 42 on the history-fixed binary, scratch copy
  `scratchpad/bin_long2`): compare its ledger size, hunger and cost with `run42_1000.txt` (the binary before the fix).
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
1. **Offline catch-up at scale** (the biggest open risk): a saturated world (~14,000 people) costs ~1 s a world year headless
   (~1.2 us per person-day in COARSE + fires) - "a day away" (a century) would take minutes in the browser. Options: cheaper
   person-days (profile `daily.coarse-day` 149 us, `daily.food` 110 us per village-day at ~350 people), a cohort-level
   resolution for deep time (03/12's Ancient/Deep modes), or the Living span adapting to the world's size. Measure the Web
   catch-up from a mature save first (`webbench.mjs`).
2. **Ecology calibration**: wide-ranging hunters (seals Range 12, tuna 3) must hunt over their range, not one cell (a seal may
   take a quarter of one cell's fish a step but needs ~66 kg: the miniature holds ~10-20 seals); the spin-up seeds the sea's
   predators ~10x below their level (they take a decade to grow into it). Validate with 10,000-year reruns (F2's evidence).
3. **Famine in the full world**: ~1 % of the people die of hunger each year once the land is full (1,791 famines in 1,000 years)
   - relief, exchange and storage between villages (F5) are the remedy; check that collapse stays possible.
4. WebGL2's first-entry stall at a new view (1-5 s): a traced shader-variant collection warmed progressively at start
   (`ShaderVariantCollection.WarmUpProgressively`), not `WarmupAllShaders` (36 s).
5. F3 remainder: persistent buildings and layouts (ruins as buildings to quarry or re-roof; houses that stay when a household
   moves), workshops and crafts, maintenance and decay; close F3. Look backlog: coast shapes off the cell grid (never sink a
   coastal village), gardens, orbit detail.
