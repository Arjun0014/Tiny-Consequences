# Progress log

Claude Code: read this first in every session. Keep "Current state" accurate; append stage entries below it (newest first). Keep entries short: what changed, test status, key numbers, open issues.

## Current state

- **Direction reset (D-050, 27 Sep 22:30 IST).** Arjun: the simulation is theatre; build the genuinely living tiny world first, jams secondary. Roadmap v2: `docs/world/17_ROADMAP.md`. Spec for the new world: `docs/world/00_RESEARCH.md` (sources), `01_AUDIT.md`, `10_WORLD_SYSTEMS.md` … `16_CAUSALITY_VISUALS.md`. Stage 2.3 of the old roadmap is **not** being continued.
- Phase: W1 (time) **done** (D-051). **W2 settlements in progress** (D-052): the settlement layer (`sim/society/`) runs headless and replaces villages/people in the systems list; **not yet ported**: tests (unit 58/63; scenarios/props not run), save bit-identity, pre-spun worlds, the view/inspector, tools. The game build likely breaks on the old schema until W2c. **Read `docs/SESSION_HANDOFF.md` first** — it lists the exact next steps.
- Then: W3 merchant ships/trade, W4 herds, W5 visuals (incl. clouds, village lights), W6 profiling.
- Baseline at 4e108b7 (verified 27 Sep): `npm run test:sim` 79/80; the failure is P07 (detectors +55% since the heat/sea-ice detectors; not re-baselined). W03 flaky pulse checks remain from the previous session.
- No subagents in this work (Arjun's instruction for this session).
- Blockers needing the human: none.
- Machine note: this laptop (12 threads, 11.6 GB RAM) also runs Arjun's other workloads; run seed sweeps sequentially when memory is low (`cmd //c systeminfo | grep -i memory`).

### Playtest-1 feedback tasks (F-tasks)

**F1 Visual quality: a major upgrade, not polish.** Reassess the rendering/art approach. Measure GPU/CPU cost as detail rises instead of keeping the Chapter-1 budget (05 §9 prop budget is to be revised with evidence).
- [x] F1.1 Before-set: capture the Chapter-1 shot set (planet, mid zoom, close zoom, villagers, pole) to `out/shots/before_ch1/` for comparison.
- [x] F1.2 Terrain (v1, D-048; level-5 mesh kept): finer render mesh, per-pixel material treatment (grass/forest floor/rock/sand/snow/farm textures from procedural noise, slope and altitude rock, beaches, snow caps), softer biome blending, better relief shading.
- [x] F1.3 Water (caustic shallows, rolling foam, glitter; rivers still to do): ocean shader with animated waves/ripples, depth colour, shore foam, specular glints, shelf halo; rivers/lakes readable.
- [x] F1.4 Atmosphere & composition (sun shadows incl. clouds, tilt-shift DoF, bloom, grade, FXAA): rim/scattering, sky, soft shadows/AO where affordable, colour grading, the miniature (tilt-shift) feel.
- [x] F1.5 Trees and vegetation (modelkit, 8 species + ground cover; seasonal shader): new tree models (layered crowns, trunks, colour variation per tree, several species per biome), bushes, grass tufts, flowers, reeds, cacti, rocks; wind sway; denser near-camera detail (LOD tiers).
- [x] F1.6 Settlements (laid-out villages, 4 culture styles, piers, granary, chimney smoke): better houses (walls, roofs, chimneys, doors), size variety with population, fences, paths, wells/granaries, docks; farm fields as real crop rows with growth stages; chimney smoke.
- [x] F1.7 People (new model, walk cycle, job activities): new villager model (proportions, head/hair, clothing by job, arms/legs), walking animation, visible activities (farming in fields, fishing boats going out, woodcutting, carrying).
- [x] F1.8 Animals (deer with antlers, wolves; walk cycle; no birds yet): better deer/wolves, herds, animation; birds/fish jumps if cheap.
- [~] F1.9 Performance measured (CPU side only: view ≈ 7 ms/frame avg in headless release; real-GPU fps unknown — ask Arjun) at every step (release build, bridge numbers, draw calls, vertex counts); record new budgets in DECISIONS.
- [x] F1.10 After-set (out/shots/latest vs out/shots/before_ch1): same shots after the upgrade; compare side by side and iterate until the difference is substantial.

**F2 A world that visibly lives.** No scripted events: improve the real simulation and how it is shown.
- [x] F2.1 Resolution study (D-047: 642 / 1,442 / 2,562 cells measured) (profiling, not the old benchmark): measure the full tick at 642 / 1,442 (f=12) / 2,562 cells in LuaJIT (P07 instruction counts) and in the HTML5 release build; per-system cost; which systems could run multi-rate; whether a hierarchical scheme (coarse climate, finer land/ecology) buys richer local behaviour; also check whether the visual problem is resolution or update rules/visualisation. Decide with evidence → DECISIONS.
- [x] F2.2 Apply the decision (f12, re-tuned, default world seed 22) (grid level and/or multi-rate/hierarchy), re-tune bands, keep S01–S04, P01–P10 green, regenerate default.tcsave.
- [x] F2.3 Visible seasons (terrain + props from the cell texture): spring bloom, summer green, autumn colour, winter snow cover on land (from sim temp/ice), crops that visibly grow and get harvested, grass that browns in drought.
- [x] F2.4 Visible weather from the sim (rain/snow curtains, storm clouds, bolts for natural lightning and the Lightning power): clouds that form/move with moisture and wind, rain/snow falling where `precip` is, storm darkening, lightning flashes for real lightning strikes, fog.
- [~] F2.5 Visible life (done: job activities, walk cycles, migrant columns, houses follow population, fields follow land use; not done: birth/death pops): villagers with daily activity (to fields, boats, woodcutting, home), herds that move with the sim's animal densities and migrations, visible births/deaths/migrations (migrant columns walking to a new village), houses that appear/disappear with population, fields that expand/contract with land use, ruins decaying, fire fronts and regrowth.
- [x] F2.6 More local dynamics (via f12, D-047) in the sim where profiling allows (finer cells, per-cell variety), checked against the tuning bands and headline rates.
- [~] F2.7 Visible consequences (done: fire, drought browning, oil, blight tint, burnt snags, rivers swell in floods; not done: flood water sheet, famine icons): event FX on the planet for real events (fire, flood water, drought cracks, famine icons, oil sheen, crop failure).

**Then Chapter 2** from Stage 2.1 (powers, real S02–S04, S05–S07/S10–S12, tutorial world + S15), 2.2, 2.3 (chain popup and "Why?"), and onward.

## Environment (verified 27 Sep)

- Windows 11, Git Bash. Node 22.14, npm 10.9, Java 26 (Oracle), Git 2.46.
- LuaJIT 2.1 installed with `winget install DEVCOM.LuaJIT` to `C:\Users\aswin\AppData\Local\Programs\LuaJIT\bin\luajit.exe` (on the user PATH for new shells; `scripts/lua.mjs` resolves it anyway). No lua5.1 (optional; W06 now proves LuaJIT and the HTML5 Lua 5.1 runtime produce identical state hashes).
- Defold 1.13.1 bob in `tools/bob/bob.jar` (`npm run setup` re-downloads). Bob platform flag is `wasm-web` (not `js-web`). The bundle lands in `dist/web/app/`.
- Python is `python` (3.13), not `python3`; set `PYTHONUTF8=1` when writing files with it. Git Bash heredocs containing apostrophes break in this harness: write patch scripts with a file.

## Key numbers (update as they change)

- SIM_LEVEL: 12 (geodesic f12, D-047) — 1,442 cells (was 3 = 642, D-030)
- Sim tick at f12, HTML5 release, synchronous: 4.2 ms median (bench:web, D-047); W08 3.8 ms median. P07 baseline 513,763 instr/tick (level 12).
- Default world: seed 22 (D-047). Tutorial world: seed 8 (D-049).
- Sim tick, real default world, HTML5 release, synchronous (W08, 27 Sep): **1.9 ms median, p95 3.3–4.0 ms** (budget 4 / 8). Wall-clock numbers on this machine swing up to 2× with other load; use P07 instruction counts to compare.
- P07 baseline (`tests/bench-baseline.json`, D-046): 241,170 VM instructions/tick (moisture 78.6k, hydrology 33.7k, temperature 27.0k, vegetation 26.6k, fauna 19.9k, villages 11.6k, ice 9.4k, ocean 9.1k, biome 8.7k, fire 4.6k, detectors 4.4k); ≈ 7.9 ns/instruction in the browser. Year-boundary tick ≈ 540k.
- In-game at ⏩ under headless SwiftShader (not representative of a GPU): tick 5–13 ms median (CPU contention with the rasteriser: 13.8 ms at 1280×720 vs 6.8 at 320×180), view 4.5–8 ms/frame at 640×360. Real-GPU numbers come from the playtest.
- Epoch tick (HTML5 release): —
- S01 baseline bands (level 3, 10 seeds × 200 y, full profile 7/10, turnover 10/10; vegetation shares of natural land, D-028):

  | seed | forest (varies) | grass | desert | ice | g_temp y200 | population y5+ | headlines/y | villages +/− | S01 |
  |---|---|---|---|---|---|---|---|---|---|
  | 1 | .37 (.13) | .32 | .08 | .14 | +1.65 | 262–355 | .45 | +2 −0 | ok |
  | 2 | .19 (.11) | .38 | .19 | .13 | +1.61 | 257–352 | .34 | +1 −1 | forest (dry world) |
  | 3 | .35 (.08) | .24 | .14 | .11 | +1.59 | 278–343 | .22 | +1 −0 | ok |
  | 4 | .23 (.06) | .22 | .22 | .15 | +1.57 | 281–343 | .165 | +1 −1 | headlines |
  | 5 | .25 (.09) | .24 | .22 | .13 | +1.50 | 261–354 | .27 | +3 −0 | ok |
  | 6 | .30 (.09) | .22 | .20 | .15 | +1.38 | 193–339 | .25 | +3 −1 | ok |
  | 7 | .27 (.08) | .41 | .20 | .11 | +1.52 | 254–348 | .34 | +4 −1 | ok |
  | 8 | .25 (.12) | .29 | .23 | .12 | +1.54 | 244–338 | .29 | +2 −0 | ok |
  | 9 | .19 (.10) | .27 | .21 | .13 | +1.51 | 240–342 | .28 | +3 −2 | forest (dry world) |
  | 10 | .26 (.09) | .22 | .25 | .13 | +1.62 | 226–343 | .28 | +4 −0 | ok |

  All seeds: predator CV ≥ 0.19, no extinctions, fish stable, wildfire in every 50-year window. Floods, droughts (re-tagging soil), village floods, famines, colonies and abandonments all occur naturally (D-039, D-044).
- Default world (`assets/worlds/default.tcsave`, seed 27, 186 KB, D-042): Year 0 has 5 villages / 299 people; first 100 years: 24 headlines (flood at Whewhist Drift Y3, lightning wildfire + deer boom Y14–17, colonists found New Robecombe Y39 and Plan B Y94), population 276–335, g_temp +1.0 °C by Y60. Loads in ≈ 2–3.5 s (pre-spun).
- Save size: ≈ 232 KB at year 40 (P02)
- Bundle size: 3.12 MB release

## Stage entries

### [w2 WIP] Settlement layer, first calibration — 2026-09-28 ~01:45 IST (handoff at ~700K context)
- Sim: `sim/society/` — entities (villages, households, people, buildings, boats, parties), sites, layout (lots), farming (fields, climate calendar, sow/tend/harvest labour, seed, weekly crop growth with blame, learned yields, capacity), fishing (daily trips, Schaefer catch, squalls, mending), labour (weekly assignment by need and occupation, daily execution with daylight/weather/travel/fitness; woodcutting, brush, hunting), construction (sites, timber + labour, earth houses, repairs, decay, storage), economy (consumption, rationing, seed coping, fuel and cold, spoilage, stock tags, society events), life (bodies, mortality, conception/pregnancy/birth, courtship, Hajnal marriage gated by home + capacity, households, occupations, memories), mobility (push/pull migration, walking parties, founding, daughter villages, settlers, abandonment), init (hourly dispatch by local midnight, weekly/monthly rhythms, hazards, snap, shift). Worldgen settle builds households, houses, piers, boats, fields and stocks. Powers (lightning on boats/people, settlers, meteor) and save packing use the new entities; village detectors moved into the society layer; ships no longer fake boats. New event kinds (rationing, seed_eaten, wood_shortage, boat_lost, shipment_lost, house_built…). `tools/society_trace.lua`.
- Runs (80 y): seeds 22 / 3 / 7 / 1 / 5 / 9 → 629 / 580 / 237 / 426 / 501 / 183 people; founded 18 / 17 / 14 / 12 / 14 / 5, abandoned 12 / 11 / 10 / 7 / 10 / 4. Chains seen: lightning > wildfire > crop_failure > famine > deaths > seed_eaten > migration > abandoned; boat_lost > rationing > famine > migration; flood > crop_failure.
- Tests: lint clean; unit 58/63 (old village schema test; save/reload divergence from lazily rebuilt fishing-ground ranks; stale pre-spun worlds). Scenario/property suites not run since the society switch.
- Perf: society ≈ 390k VM instr/week at ~375 people (planet ≈ 500k).

### [w1] Time: hourly clock, sun, daily weather — 2026-09-27 ~23:40 IST
- Sim: `W.hour` master clock, `world.step` (hour) / `world.tick` (week), hourly systems hook, commands/saves/events on hours (SAVE_VERSION 3); `sim/society/daylight.lua`, `sim/society/weather_day.lua`; calendar hour helpers; rng streams 30–38 reserved for the society layer.
- Game: speed ladder in hours/s (7 HUD buttons, keys 1–6), HUD local time at the focus, real sun with slerp to the camera sun at fast speeds, night ambient/sky/stars/clouds/atmosphere; bridge `run_hours {n | to_local}`; `__TC.hour`, `hour_f`.
- Tests: quick 85/85 (new `unit.test_daylight`: almanac day lengths, opposite hemispheres, local time, rhythm, weekly rain conserved); W01, W02, W02-lite pass (rates 0.2 / 1.3 / 8.1 / 174 / 1,124 / 1,230 h/s in the headless debug build). P07 re-baselined 524,752 (D-051).
- Shots: out/peek/t13.png (noon), t18.png (dusk terminator), t1.png (night).

### [ch2.s1 + s2 partial] Powers, causality completeness — 2026-09-27 21:10 IST (uncommitted work committed at handoff)
- Sim: `sim/powers.lua` (8 powers, logged command `power`, root events power_*, tags on every field touched; weather/temperature forcings and blight in a new first system `powers`), `villages.settlers`, `sim/tutorial.lua` (tutorial world = seed 8, `assets/worlds/tutorial.tcsave`), detectors credit a player "volley" (same power, same tick) and re-tag across it, starving herds overgraze (`overgraze`), heatwave / cold_snap / sea_ice_formed detectors, blight_spread headline kind. D-049.
- Tests: scenarios now use powers (S02 Cull on every pack, S03 Lightning on every farm ×2 harvests, S04 Oil power) + new S05, S06, S07 (threshold 0.6, D-049), S10 (meteor part), S11, S12 (sea ice only), S15 (tutorial chain: lightning > tanker_sunk > fish_collapse > famine > deaths_spike); P08 attribution (96% of added headlines chain to the player), P09 headline rate (0.29/y); unit `test_powers`. Last full profile 78/78 (S01 9/10, S02 8/10, S03 9/10, S04 8/10, S05 7/10, S06 8/10, S07 10/10, S10 10/10, S11 9/10, S12 10/10, S15 1/1) — before the heat/sea-ice detectors; afterwards quick scenarios + P08 + P09 green, full not re-run.
- Game: `game/ui/powers.lua` (defs, modes/species, cooldowns, target checks, cast via the command queue), power bar in the HUD (Shift+1..8, choice row), `game/view/aim.lua` aiming ring, cast effects at once (bolt, pulse disc, falling meteor), blight tint on fields, bridge commands `power`, `arm`, `look`, `gfx`, URL param `gfx=low` for functional web tests. New icons in `scripts/gen_ui.py`.
- Web: W03 (new) passes lightning, rain, drought, heat, cull, introduce, blight, oil, meteor in one run or another, but the pulse-only checks are borderline (chill 4.1 vs 5 in the last run) — make the test wait for the sim's effect or boost pulse contrast. W06 green after rebuild. W01, W02, W07, W08, W09 green at the last full run.


### [ch1b] Playtest-1 feedback I: f12 sim + renderer v2 — 2026-09-27 ~20:00 IST
- Resolution study (D-047): browser tick 2.0 ms (642 cells) / 4.2 ms (1,442, geodesic f12) / 7.9 ms (2,562). Switched to f12: `grid.build` takes geodesic frequencies ≥ 10. Re-tuned (moisture diffusion, area-based oil spills, wildfire headline size, news fatigue, villager CO₂), village fires burn granaries, S01 desert band 5–30%, S03 burns two harvests, S04 spills a tanker cargo. Default world seed 22. P07 re-baselined 513,763 instr/tick.
- Renderer v2 (D-048): shadow map + post pipeline (DoF/bloom/grade/FXAA), terrain materials with real farm plots, model kit (trees, plants, buildings, villagers, animals), settlement layouts, seasonal/snow/fire props from the cell texture, walk cycles, job activities, rain/snow/storm clouds, chimney smoke.
- Pitfalls found: `render.render_target` in 1.13.1 has no `sample_count`; a props material sampler order bug bound the shadow map as the cell texture (everything "on fire"); an extra vertex stream on world-space movers crashed batching; mover buffers must declare the same colour width as their .buffer files; mesh.max_count raised to 1024.
- Tests: `npm run test:sim` 67/67, `--full` 67/67 (S01 8/10, S02 10/10, S03 9/10, S04 8/10). Shots: out/shots/before_ch1 vs out/shots/latest.

### [ch1.s10] Chapter 1 hardening → PLAYTEST 1 — DONE 2026-09-27 14:45 IST
- Browser: pre-spun default world verified in the build (`prespun` flag, load ≈ 2–3.5 s). W06 now runs the self-test through `sim/selftest.lua` and compares with LuaJIT (`tools/selftest.lua`): identical hashes, so the headless suite speaks for HTML5. W08 measures the real default world's tick synchronously (1.9 ms median) and the speed controller's frame contract at ⏩; W02/W08 run at 640×360 (CPU rasteriser).
- Perf diagnosis (`npm run perf:probe`, `bench:web --default`): in-game timings are inflated 2–4× by SwiftShader; props chunk rebuilds overshot their budget → finer batches and ≤ 4 rebuild starts/s at high speed; clouds update on alternate frames; the controller predicts ticks with a 25% margin.
- Colonies (D-044): leave in early spring by the destination's calendar with provisions; fishing jobs set before settlers get jobs; landless farmers fish; orphans are taken in; villages ≥ 100 split. S03 threshold 0.95, news fatigue 15 y (D-045). P07 re-baselined 241,170 (D-046).
- Visual pass (06 §6 checklist on 17 shots): fixed an orange band along ice coasts (sea ice leaking into the land fire channel across coast triangles), the opening camera (now the biggest village, was an ice cap), bunched/tiny villagers, the village inspector's misleading "+4 more", the F3 panel's clipped last line. Year 0 → 50 shows a wildfire scar, forest spread, CO₂ 280 → 341, +0.8 °C, +11 m sea level.
- Tests: full 67/67, quick 67/67, web 8/8 (debug + release). Commits 6741b5b, 1dc240e, 6534fa2, 0e5ef19, c487556 (playtest build) and the report commit.

### [ch1.s10] Sim hardening II: living-world causes — 2026-09-27 13:10 IST
- Diagnosed with new tools `npm run sim:villages` (month-by-month village economy + chains) and `npm run sim:scout` (seed liveliness):
  - Rialora starving (Years 3–4, default world): southern villages farmed on the northern calendar → frost every season. Fixed with per-village farming calendars + growing-season site scoring + capacity-gated site picking (D-036).
  - Churn (seed 7 +24/−26): same bug + villages founded on unfarmable sites. Now 1–8 founded / 0–5 abandoned per 200 y.
  - Slow starvation decades in (seed 10 Venmere): soils were not at equilibrium after spin-up → accelerated fertility spin-up (D-037); hungry villages plant exhausted fields.
  - No new villages ever → daughter villages (D-038). Population now 213–352 on the full profile (soft ceiling 450).
  - Floods and droughts had never fired on any seed (near-perfect climate loop) → ocean-evaporation wobble, bankfull = mean annual flood, flood crop damage + `flooded_village`, per-cell seasonal drought detector (D-039).
  - Crash headline pile-up (~60% of headlines were ordinary deer-cycle troughs) → trough tracking, nature/caused headline depth, 25-year regional news fatigue (D-040).
  - A lost harvest killed nobody → children/elders weaken 2× faster (D-041).
- Default world is now seed 27 (D-042), regenerated. S03 uses the village's own calendar and `pop@community` (D-043).
- Tests: `npm run test:sim -- --full` 67/67 (S01 7/10, S02 9/10, S03 7/10, S04 9/10, P02/P03, P06 500 y, P07 241k instr/tick (+3.3%), P10 20/20).
- Headline mix per seed (200 y): 0.17–0.42/y; wildfires, village floods, booms, deep crashes, famines, village founding/abandonment.

### [ch1.s10] Sim hardening I + P07 — 2026-09-27 (commits 64a294f, c9f1a58, c9cb374; previous session)
- Food-history population regulation (D-025), compact saves (D-026), pre-spun default world (D-027), natural-land vegetation bands (D-028), P07 on VM instruction counts (D-029), SIM_LEVEL 3 final (D-030), rendering/UI decisions (D-031..D-035). Full profile 66/66 at the time.

### [ch1.s9] Camera, time, HUD, inspector — DONE 2026-09-27 (commit b341c2c; previous session)
- Trackball camera, fly-to/follow, picking (cell/village/person/animal/ship), speed controller + buttons, HUD, inspector for cell/village/person/planet, F3 debug overlay, edge-indicator stub. W02 and W09 pass.

### [ch1.s7] Villages & people — DONE 2026-09-27 (commit afde000)
- What: villages.lua (farms, crops, harvest, granary, fishing, hunting/culls, wood CO₂, migration, founding, abandonment, ruins, memories), people.lua (individual lives), ships.lua (tankers on lanes, boats, sinking → oil), worldgen settle at spin-up y15 (villages, lanes, volcano sites), village detectors; perf pass (tick 343k → 225k Lua VM instructions, ≈3.4 ms est. HTML5).
- Tests: quick 64/64 in 29 s (S01 3/3, S02 2/3, S03 3/3, S04 3/3, P02, P03, P10 20/20). Full: S01 5/10 (population regulation too crude on poor worlds), S02 9/10, S03 8/10, S04 10/10 → hardening in progress.
- Numbers (quick seeds, 200 y): pop 159–392, g_temp at y200 +1.24…+1.48 (human drift), fires 5–8/decade, headlines 0.33–0.43/yr, forest 0.21–0.37, desert 0.06–0.16. Save 301 KB at y40.
- Chains seen: test_burn_farms → fire_started → crop_failure → famine → migration → village_founded; test_oil → fish_collapse → famine → migration.

### [ch1.s8] Planet rendering — DONE 2026-09-27 (commit fe7baaf)
- What: terrain + sea in one level-5 mesh (61k verts; colours from a per-cell texture sampled in the vertex shader; coasts from interpolated elevation + noise), 80 batched prop chunk meshes (~12k procedural trees/pines/palms/bushes/fields/houses/ruins; rebuilt per chunk on value change, 2.5 ms budget), sky gradient + streaks, fresnel rim, pooled clouds/deer/wolves/fire meshes, bridge camera presets/run_ticks/freeze/new_world.
- Tests: test:web 4/4 (W01, W02-lite ×2, W07 view); lint clean. Shots: 13 PNGs in out/shots/latest (reviewed: matches ART_DIRECTION on sky, rim, ocean halos, dense trees + limb silhouettes; issues: white HTML footer strip, mottled snow over forest, default planet a bit small; being fixed in 1.9).
- Numbers: 37–45 real draw calls at planet view, 5.7k–9.5k props drawn, view 0.7 ms/frame (release, Lua). Load ≈ 11 s (spin-up dominates → pre-spun default world later).

### [ch1.s6] Oceans & animals — DONE 2026-09-27 (commit d97afff)
- What: ocean.lua (plankton/fish/oil with seasonal currents, sparse oil set, beaching), fauna.lua (Holling II + refuge, migration flows with tags, sea-ice bridges, browsing/grazing/crop raids, cull hooks), detectors.lua (boom/crash/extinction/fish collapse/oil spreading & beached/drought/flood with re-tagging), named regions, test hooks, scenario framework; "release" blame (D-013); save bug fix (D-016).
- Tests: test:sim 54/54 quick (171 s; P06 500 y dominates → trimmed in 1.7); --full passes; S02 and S04-oil 10/10 seeds.
- Numbers (3 seeds × 200 y): forest 0.24–0.42, grass 0.22–0.33, desert 0.05–0.15, ice 0.13–0.15, predator CV 0.22–0.28, no extinctions. ocean+fauna+detectors ≈ +9% tick.

### [ch1.s2] Sim foundation — DONE 2026-09-27 02:20 IST
- What: `sim/` core in pure Lua 5.1: rng (counter-based, 16-bit-split mul32), grid (icosphere + neighbours sorted CCW, areas, nearest, BFS), calendar, fields (catalogue, SoA, prev buffers, year-boundary 16-bit snap with a sqrt mode for small values), events (records + flat parent/root arrays so chains survive GC, aggregation, cooldowns, descendants), blame (fixed-arity step with fast path, inflow tags), ledger (global co2/aerosol/g_temp ledgers), world (tick orchestration, commands queue/log, state_hash, profiling hook), save (text-safe format, capture at year boundary, restore + replay). Tools: run_sim (summary/series/events/maps), map_dump + pure-Lua PNG writer, zonal diagnostics, bench (LuaJIT) + bench:web (real HTML5 runtime via bridge).
- Tests: `npm run test:sim` 33/33 (rng, grid L2–L4, calendar, events, blame, save round trip bit-identical mid-year with a command, names, P01 determinism). lint clean.
- Numbers: see Key numbers; SIM_LEVEL decided provisionally from the browser benchmark (D-004).
- Also done early: 1.3 terrain, 1.4 climate & water, 1.5 vegetation/fire/biome (see Current state).

### [ch1.s1] Toolchain & skeleton — DONE 2026-09-27 02:05 IST
- What: Defold project, custom render script, input bindings, test bridge (`?test=1`, `window.__TC`, `__TC_CMDS`), speed controller skeleton, npm scripts (setup/build/serve/test:web/shots/play + sim wrappers), lint:lua (Lua 5.1 + sim purity rules, selftest), Playwright config with SwiftShader flags.
- Tests: test:web 3/3 (W01 smoke, W02-lite speed round trip, bridge error reporting); lint 0 violations.
- Commit: ab5d26a
