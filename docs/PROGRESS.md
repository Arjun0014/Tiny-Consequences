# Progress log

Claude Code: read this first in every session. Keep "Current state" accurate; append stage entries below it (newest first). Keep entries short: what changed, test status, key numbers, open issues.

## Current state

- Chapter: 1 — A World That Lives Without You — **COMPLETE (27 Sep, 14:45 IST), waiting for Arjun's playtest.** Report: `docs/playtests/chapter-1.md`.
- Next: read `docs/playtests/chapter-1-feedback.md` when it exists, turn it into tasks, then start Chapter 2 (Stage 2.1 Powers). Do not start Chapter 2 before the feedback.
- All Chapter-1 stages 1.1–1.10 done. Test status at the chapter commit: `npm run test:sim -- --full` 67/67, `npm run test:sim` (quick) 67/67, `npm run test:web` 8/8 (debug and `--release`), `npm run shots` 17 PNGs reviewed.
- Blockers needing the human: none.
- Last human feedback read: none yet (Arjun shared the target look mid-session → docs/ART_DIRECTION.md).
- Machine note: this laptop (12 threads, 11.6 GB RAM) also runs Arjun's other workloads; on 27 Sep a 12-process Python job exhausted the commit limit and process creation failed for a while. Run seed sweeps sequentially when memory is low (`cmd //c systeminfo | grep -i memory`).

## Environment (verified 27 Sep)

- Windows 11, Git Bash. Node 22.14, npm 10.9, Java 26 (Oracle), Git 2.46.
- LuaJIT 2.1 installed with `winget install DEVCOM.LuaJIT` to `C:\Users\aswin\AppData\Local\Programs\LuaJIT\bin\luajit.exe` (on the user PATH for new shells; `scripts/lua.mjs` resolves it anyway). No lua5.1 (optional; W06 now proves LuaJIT and the HTML5 Lua 5.1 runtime produce identical state hashes).
- Defold 1.13.1 bob in `tools/bob/bob.jar` (`npm run setup` re-downloads). Bob platform flag is `wasm-web` (not `js-web`). The bundle lands in `dist/web/app/`.
- Python is `python` (3.13), not `python3`; set `PYTHONUTF8=1` when writing files with it. Git Bash heredocs containing apostrophes break in this harness: write patch scripts with a file.

## Key numbers (update as they change)

- SIM_LEVEL: 3 (D-004, final D-030) — 642 cells
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
