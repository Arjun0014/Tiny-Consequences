# TC2 Session handoff

## Session 5 (7 Oct 2026, from 06:40 IST) — in progress

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-085 - the current priority)**: after D2-083 is verified and committed, STOP before the D2-084 polish
  and **reconsider the planet's physical size** (D2-007's R = 10 km is reopened): study TC1's actual scale, test smaller TC2 planets
  (N64 kept, R = 10 / 7 / 6 / 5 / 4 km) really simulated, measure population, interaction density, travel, performance, take comparable
  screenshots (orbit, region, village, close), decide the radius myself and change it now if the evidence supports it (old saves may be
  invalidated); then redo D2-084 on the chosen scale. Fewer people is fine (2,000-8,000 mature); no world where everyone reaches everyone.
  Study and decision: `docs/tc2/19_PLANET_SCALE.md`.
- **D2-083 closed and committed** (session 5): 177 tests (the small-world herd check now a factor-2.5 bound - DECISIONS D2-083
  "Closing checks"), COARSE 400-year hashes unchanged (42 55cfd96b471db300, 7 8c3e14c324165015).
- **Scale tools (working tree, D2-085 in progress)**: the planet's geophysics in geo-lengths (`PlanetState.GeoRadius` = 10 km,
  `GeoScale`, `GeoAreaHa`, `GeoCellSize`: plates, erosion, coasts, ice, drainage, quakes, storms, eruptions, tsunamis, copper's extent,
  slides, the ground's steepness for ecology and routes) - a seed makes the same ground at any size; living laws per hectare
  (`AreaScale`: predators' half-sated prey and territories, hunters' half-sated grounds, the occupancy of a cell for speciation and
  herds) and grounds as a patch of real size (`PlanetState.Patch(c, rings)`, `PatchRings`: herds, hunting grounds, rock for querns);
  initial villages 1.2-3.9 km apart in metres. **All bit-identical at R = 10 km** (12-year hash aeba16dc9bb1c04e against the D2-083
  build). Tools: headless `--radius M`, `run --scale` (`ScaleReport.cs`: spacing, villages within a day's walk, communities and hops,
  people reachable in a day, kin distance, polities, woods round villages, wild kinds, cost a person-day) with `Core/Diagnostics/
  ScaleTally.cs` (distances of marriages, trades, relief, moves, foundings, takings, raids, claims, visits, tribute; tools only). View:
  `?radius=`, shots `-tc2radius`, orbit/region framing, relief exaggeration (`Ground.ScaleTo`), terrain LOD depth and haze relative to
  R; multi-world shot folders named by save folder. Scratchpad (session 5):
  `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\a14b045d-19c2-46a3-b3ef-213aae01eceb\scratchpad`
  (`scale_runs.sh`, `queue1.sh`, `runs.sh`, `bin_b` = the study binaries).
- **Visual slice 1 of D2-084 is still uncompiled in Unity** (session 4's code: stands, palette, relief, rivers, tree kinds) - it will
  compile with the first scale shot round; judge it there.

## Session 4 (7 Oct 2026, from 00:16 IST) — closed into session 5

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-084)**: finish D2-083, then **the world made legible** is the priority (`18_WORLD_READABILITY.md`,
  roadmap phase W): audit -> composition -> forests -> rivers -> biomes -> terrain -> settlements -> life -> scars -> orbit ->
  multi-age acceptance -> browser performance. No simulation theatre. Ordinary low-priority runs need no permission (CPU/RAM ~50 %).
- **D2-083 (DEEP calibration) - working tree, uncommitted**: the step's mean ration (RationStep) removed (first day's ration);
  a pressing need's hours given each day of a step (`AgentSystem.CoarseDay` free); each worker of a step chooses on the ground as the
  step's earlier workers left it (`WildFood.Retake`, after each wild trip when days > 1); a worker's own take walked through the step
  (`WildFood.MidTake` in Hunt, Fish, Gather when days > 1). Rounds: h (no RationStep; seed 7 only), i (+ need-hours: wild hours
  2.17 vs 1.40 - overshoot, the woods overdrawn), j (+ Retake; six seeds, `scratchpad/verdict_j.txt`: per-person rates at par,
  people 0.85/0.87 at years 300/400 n.s. - seed 4 founded fewer villages, few epidemics -, hunting +34 % t 3.5, out for need +9 % t 7.1,
  fish -19 %), k (+ MidTake: seed 7 worse - more hunting, more out for need, famines 387 vs 312 - **reverted**; the working tree's
  Core is round j). D2-083 is written up as closed in DECISIONS/04/PROGRESS with round j's evidence and residual leans.
  **SESSION STOPPED 7 Oct ~01:04 at Arjun's request (usage limit). NOTHING COMMITTED since 6ad57fb.** Still to do, in order:
  (1) `node tc2/scripts/headless.mjs test` (all tests + the new `DeepTimeTests.EachWorkerOfAStepChoosesOnTheGroundTheStepLeft`; the
  repo's bin holds the reverted round-k build until it rebuilds); (2) COARSE hashes of seeds 42/7 must still be 55cfd96b471db300 /
  8c3e14c324165015 (one-day arithmetic untouched); (3) DEEP speed on a quiet machine (no --profile, nothing else running; round f:
  DEEP 42 3.17 y/s vs COARSE 1.80, DEEP 7 4.28 vs 2.97) - add to D2-083 and PROGRESS measurements; (4) commit D2-083 (stage Core
  AgentSystem/Settlement/SettlementSystem/WildFood, Tests DeepTimeTests, docs 04, 17, 18, DECISIONS, PROGRESS, SESSION_HANDOFF,
  NEXT_SESSION_PROMPT, headless Commands.cs `--snap`); (5) then the visual slice below (compile in Unity via shots, fix, judge, commit
  with its .meta files). A leftover round-k run of seed 3 (`deep3_400k`, tc2bin21) may have finished after the stop - ignore it.
  Scratch files: `scratchpad/verdict.mjs` (now with mechanism tallies), `pairsj.json`, `verdict_j.txt`, binaries tc2bin19-21.
- **Visual slice 1 (D2-084), code written, NOT yet compiled in Unity or shot**: woods as stands and glades (`Canopy.Stands`,
  StandNoise/StandRank/StandCdf, the shader's StandRank; trees and cover follow), open-land palette from climate and soil water
  (`TerrainLod.CellColour`), `Ground.Relief` (valley/ridge index: wet valley floors, conifers to ridges), distant relief lit steeper
  (Terrain.shader normal), haze ~40 % thinner, tilt-shift a hint (Sky, TiltShift.shader), **rivers** (`Rivers.cs` shared shape,
  `River.shader` ribbons widened to >= ~2.6 px far off for larger rivers, wet banks and riparian strip in Colourise, Land.y = river
  nearness, woods keep to banks, reeds at the edge only), **trees** (`ProcMeshes.ForestTree` 7 kinds x 3 variants: round, tall,
  wide, birch, willow by water, spruce, pine on dry/coastal land; crowns of shaded lumps), shot tools (`ShotRunner -tc2load F1;F2`,
  `SimHost.LoadWorld`, views `river`, `town`, `scar`; headless `run --snap Y1,Y2` -> `tc2/out/ages/sS/world_yY.tc2`), River.mat in
  ProjectSetup (run `unity.mjs configure` before a Web build). Next: `unity.mjs shots --tag leg1`, fix compile errors, judge.

## Session 3 (6 Oct 2026, from 17:57 IST) — closed into session 4

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
1. **F8 - DEEP calibration (D2-083, in progress)**: Arjun allowed the six-seed batch; round f (`*_400f`, tc2bin17 = commit 6ad57fb)
   done 23:51 - `scratchpad/verdict.mjs scratchpad/pairsf.json` (paired log-ratios, t over six seeds): population at every
   century, births and deaths per person-year, hunger and famines, wild food, herds, relief, households leaving and conflict at par;
   leans not significant (wild hours per person-year +17 %, t 2.0; smelting; infections -25 %); one metric flagged at 5 % (foundings
   per person-year, totals at par - ~2 false flags expected among ~50). COARSE 7 rerun with the tallies (`coarse7_400g`, hash
   unchanged) showed the one real remaining bias: the step's mean ration (`Settlement.RationStep`) walked the store down by the
   need alone - whole steps marked short (out for need on 43.5 % of person-days against 32.8 %; urge 0.072 against 0.036). Removed
   in the WORKING TREE (uncommitted; built as `scratchpad/tc2bin18`; not yet tested): the step's work reads its first day's ration.
   **Next**: retest DEEP 7 against `coarse7_400g` (`QUIET=1 BIN=tc2bin18 TAG=h calib2.sh 7:deep`), then the six seeds (TAG h),
   full tests, D2-083's rule 2 text (no mean ration), commit; then DEEP's speed on a quiet machine. The 23:56 memory-pressure stop
   (Claude Code) killed the local game server, the COARSE 7/42 tally chain (COARSE 42 kept running to finish `coarse42_400g`) and the
   DEEP 7 retest - restart only when Arjun asks.
2. **The COARSE hunting-skill bug - its own calibrated change after D2-083** (Arjun, 6 Oct: not part of D2-083 - changing COARSE
   moves DEEP's reference): in `AgentSystem.CoarseDay` the hunter's skill growth and `HuntTrips++` sit inside a comment since D2-063
   (`s.Hides += hides;   // (D2-063) p.SkHunt = ...; w.Metrics.HuntTrips++;`). Restore them, recalibrate COARSE against ACTIVITY
   (`CoarseAndActivityLiveOffTheWildAlike` and the wild-hours fits), record new COARSE baselines (hashes change), rerun DEEP vs COARSE.
3. **F8 - speed**: DEEP's per-step costs (coarse-day, learning, demography, conflict), the planet's share (weather, vegetation +
   ecology every 12 days are needed for seasons - look for exact speedups rather than coarser cadences), housekeeping; Ancient and
   Deep time measured in the browser (`webcatchup.mjs`); F8's done-when: 5,000 years in minutes.
4. **Web performance**: a day away catches up in 48-49 s (D2-082; 41-42 s before F5-F6) - profile the browser's catch-up.
5. **The look backlog (D2-081)**: the surf's foam repeating as V shapes near the camera, the water sphere's edge stepping far off,
   one size of shrub at wood edges, flowers only from ~100 m, animals specks at the village view, kitchen gardens (not simulated).
6. F7's carried-forward items (D2-078): Your Actions (with F9), timelines, news by incident, biography and polity pages, ages named
   apart. F6's (D2-076): lordships that hold, leaders, alliances, captives, gates and stone walls, bands of several villages,
   beasts as loot, the violence rate, the view of lordships and bands. F5's (D2-068): boats between coasts, roads worn by use,
   fairs, salt, credit, relief by kinship. The sea's calibration (D2-053).
