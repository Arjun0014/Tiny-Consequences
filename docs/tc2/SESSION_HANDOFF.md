# TC2 Session handoff

## Session 3 (6 Oct 2026, from 17:57 IST) — in progress

### Exact state
- Commits: 1156d3d D2-081 (the woods as masses, cover, villages' signs) - then D2-082 (Web: cover gathering cached, catch-up at orbit,
  capped builds; `s3webb` 6/6) and F8 slice 1 (DEEP) committed together after it (see git log).
- **F8 in progress - DEEP calibration (UNCOMMITTED fixes in the working tree, all one-day paths unchanged - 175 tests passed after
  the first four)**: the first 400-year comparisons (`tc2/out/deep/coarse{42,7}_400.txt` vs `deep*_400.txt`, `deep*_400b.txt`) showed
  DEEP biased: population -15..-37 % at year 200, cattle x3 and pigs x0.2, relief x0.5, wild hours +10 %, scouts x0.75, raids/claims up.
  Fixed (each a "step must keep its days" rule, now in 04 §4.3b): claims one a day (Conflict.Claims), relief window, scouts every six
  days (`Migration.ScoutsDue`), beasts' condition/hazard/milk/conception walked day by day (`Livestock.DeepBeast`), grazing not capped
  at one day's offer, mast per cell capped, catch caps 1-(1-s)^K (`WildFood.Most`), the least wild trip a quarter-hour a day, the
  store seen as days lived hold it (`Settlement.Ahead/AheadFrom/AheadTo`, `StoreSeen`; sim version 32; hashed only when set), a
  mother may conceive from the day her nursing ends / her 16th birthday, a girl marry from her 18th (`DaysUntil`), crops grow on each
  day's place on the season curve (`CropDay(.., k)`), hearths and pasture take the step's middle day. Headless `run --annals F.csv`
  writes the annals (yearly people, births, deaths by cause...). Scripts: `scratchpad/calib.sh` (env SEEDS, MODES, TAG, BIN),
  `calsum.mjs DIR c d seeds` (paired totals), `annals.mjs DIR coarse deep _400TAG seeds` (rates per 1000 person-years by century).
  Round 2 (`*_400d`, COARSE hashes of 42 and 7 identical to before: 55cfd96b471db300, 8c3e14c324165015) still showed DEEP biased
  (hunger/famines up, wild hours +33-50 %, conflict up on 42); round 3 fixes (all in the working tree): culling to the pasture as
  the step leaves it, death hazard day by day and conception on `Person.StepHealth`, the store-empty check and the wild urge on the
  store seen (a provisional `Ahead` right after Food), the step's mean ration (`Settlement.RationStep`), the smith's cap, a calm
  step never spans a plant pulse (`World.NextPlantTick`, derived), the woods at the step's middle (`Livestock.Rooting`,
  `WildFood.MidStep`), per-person derived constants (`Person.Derive`: build, copying scatter - exact). Seed 33 (60 y, small):
  DEEP wild work now matches COARSE (0.141 vs 0.147 h a working person-day; was 0.115). Draft decision: `scratchpad/d2083_draft.md`.
  Tools-only tallies: `SettlementSystem.Unrests` (why not calm), `AgentSystem.Hours` (COARSE hours by use, the wild choice by kind
  and by day of the plant cycle), printed by headless `run --places`. `scratchpad/calib2.sh` runs "seed:mode" items (BIN, TAG).
  Later in round 3: a step's later days grow the crop after its weeding (seed 1 crop 0.853 -> 0.871 against COARSE 0.884), harvest and
  leaving tallies (`SettlementSystem.Harvests`, `Migration.Weighs`). D2-083 written (in progress); 176 tests pass (DeepTime 7 after the
  last rule). Round 3a results (`*_400e`, tc2bin9): seeds 42/7/1 summarised in D2-083's evidence.
  **Blocked** (PROGRESS "Blockers"): the classifier refused to stop two leftover workers of this session and to start the final batch;
  they have since finished (round 3a on six seeds, D2-083's evidence: no early-growth bias, per-person rates at par, but -15 % people at
  year 400 on all six with less food: pigs x0.07, fish, herd meat, relief). After it: pigs root day by day (`Livestock.Day`,
  `MidStepMast`), a demography tally (`SettlementSystem.Demo`) and the wild decision's shortage terms - seed 1 DEEP's rations ran short
  16x as often from 9 % more people (an early lead crossing a threshold, not a rate). 176 tests pass. **Next**: with permission, DEEP 42/7/1-4 with the newest scratch build (TAG f), compare
  against COARSE (42/7 `*_400d`, 1/2/3/4 `*_400e`) with `annals.mjs`/`calsum.mjs`; open question: DEEP's early growth runs ahead on
  small worlds (seed 1 +9 % people at year 100, seed 33 +17 % at year 60) with three times the households leaving - look at
  marriages and births per person-year by age next; then speed on a quiet machine (never with parallel runs: everything ran 2x
  slower). Known COARSE bug left for its own slice: hunting skill and trips are never updated in `CoarseDay` (the line sits inside a
  comment since D2-063).
- Tools added: `tc2/scripts/webcatchup.mjs` (a day away's catch-up time by camera height and layers), the bridge's `catchupalt`,
  `covershadow`; `scratchpad/perfviews.sh` (webperf view by view), `scratchpad/calib.sh`.
- The slice (for reference) - **D2-081 (the woods as masses, ground cover by biome)**: `Canopy.cs` (plain-chance
  crown numbers - the tile repeats, so nothing in it may pattern -, radius 0.52-0.94 with few broad old crowns, height 0.64-1, K
  fitted on the layout (`Fit`), the cover texture `MakeCover` - whether a standing crown covers each texel at four thresholds, its
  mips the share covered -, `CoverMean`); `Terrain.shader` (`CoverAt`/`CoverOf`: the far woods are the area-average of the near canopy -
  clumps lit by a bump from their cover, the floor shaded by clumps sunward -, the crowns hand over at 1.3-3 m a pixel, the tile
  hands over to its mean at 3.5-7 m a pixel, leaves olive by the climate's dryness `i.land.x`); `TerrainLod.cs` (a fourth vertex
  stream `Land` = dryness, meadow, bare earth or rock, beach; clumps of ~20-45 m in the forest share where woods are open; tree
  colour groups with two dryness bits (ushort groups, 512 tints); ground cover by what the land is: meadow grass, flowers in their
  season, shrubs and ferns at wood edges and under the woods, heather on cool wet open land, cushions and stones on tundra, straw
  and stones where dry, dune grass on sand above the tide, reeds, boulders; cover re-placed with the seasons); `ProcMeshes.cs`
  (Shrub, Fern, Cushion, Stone; low icosahedron blobs); `WildlifeView.cs` (herds up to 3 km altitude, up to 4x at distance);
  `Sky.cs` (cover texture, K, mean as globals).
  Later in the slice: the far cover read a second time turned and scaled (`fb`) and stands (`Tri(.., 1/1700)`); tree kinds
  (`ProcMeshes.ForestBroadKind`: tall, spreading, birch) and young trees after fire; beaches and the sea floor per pixel with a metre
  of noise in the height read (the coast staircase); `WorldObjectsView` hut and camp smoke, woodpiles (`ProcMeshes.Woodpile`), the
  watch fire, night lights (`EmitLights`); `ShotRunner` `view+D@H`; `BuildScript` `-tc2Jobs`; the bridge's `off cover`.
- Shot rounds: `tc2/out/shots/woods1` (tile clumps read as a grid from orbit) -> `woods2` (grid gone, far woods too smooth; night
  lights) -> `woods3` (far texture and stands; seasons: a tropical forest stays green - no autumn above ~20 C mean, right) ->
  `woods4` (beach per pixel, edge shrubs to 480 m, flowers readable) -> `woods5` (sea floor per pixel) -> `woods6` (waterline noise).
- Look items seen and left for later: the surf's foam repeats as V shapes near the camera; the water sphere's own edge against
  coarse terrain can still step far off; shrubs at a wood's edge are one size; flowers only from ~100 m; animals at the village view
  are still specks (herds read as groups only when white); kitchen gardens (not simulated: not drawn).
- Machine: shared and loaded by others (see PROGRESS session 3); wait with `scratchpad/quiet.ps1`, run with `scratchpad/lowrun.cmd`
  (scratchpad = `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\1e4f3b75-2f3f-4d81-b3ff-5dffc75a3c54\scratchpad`).
- No TC2 process running between shot rounds.

## Session 2 (5 Oct 2026) — closed (see PROGRESS.md CURRENT STATE)

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
