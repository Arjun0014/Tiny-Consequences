# Progress log

Claude Code: read this first in every session. Keep "Current state" accurate; append stage entries below it (newest first). Keep entries short: what changed, test status, key numbers, open issues.

## Current state

- Chapter: 1 — A World That Lives Without You
- Stage: 1.9 (camera/HUD/inspector, game/) and 1.10 sim hardening (full-profile robustness: population regulation, S01 ≥ 7/10; pre-spun default world) in progress in parallel. Done: 1.1–1.8 (1.3–1.5 folded into the 1.2/1.6/1.7 commits; P10 20/20).
- Plan for the rest of Chapter 1: ocean life + oil + fauna (1.6) → villages/people/ships + worldgen settle + detectors (1.7) → rendering to docs/ART_DIRECTION.md (1.8) → camera/HUD/inspector (1.9) → hardening + playtest report (1.10).
- Blockers needing the human: none
- Last human feedback read: none yet (Arjun shared the target look mid-session → docs/ART_DIRECTION.md)

## Environment (verified 27 Sep)

- Windows 11, Git Bash. Node 22.14, npm 10.9, Java 26 (Oracle), Git 2.46.
- LuaJIT 2.1 installed with `winget install DEVCOM.LuaJIT` to `C:\Users\aswin\AppData\Local\Programs\LuaJIT\bin\luajit.exe` (on the user PATH for new shells; `scripts/lua.mjs` resolves it anyway). No lua5.1 (optional).
- Defold 1.13.1 bob in `tools/bob/bob.jar` (`npm run setup` re-downloads). Bob platform flag is `wasm-web` (not `js-web`). The bundle lands in `dist/web/app/`.
- Python is `python` (3.13), not `python3`; set `PYTHONUTF8=1` when writing files with it.

## Key numbers (update as they change)

- SIM_LEVEL: 3 (D-004) — 642 cells
- Detailed tick (HTML5 release, headless Chromium, level 3, bench config seed 42, 192 ticks, 2026-09-27): **1.70 ms median, p95 2.5 ms** (budget 4 ms / 8 ms). Per system: moisture 0.49, hydrology 0.31, temperature 0.19, vegetation 0.17, fauna 0.13, villages 0.11, biome 0.09, ocean 0.07, ice 0.06, fire 0.05, detectors 0.03 ms. L4 (pre-1.6 systems) was 18.9 ms.
- P07 baseline (`tests/bench-baseline.json`, D-029): 233,790 VM instructions/tick on the fixed world; browser ≈ 7.4 ns/instruction.
- Epoch tick (HTML5 release): —
- S01 baseline bands (level 3, 10 seeds × 200 y, full profile 7/10; vegetation shares of natural land, D-028):

  | seed | forest (varies) | grass | desert | ice | g_temp y200 | population y5+ | headlines/y | S01 |
  |---|---|---|---|---|---|---|---|---|
  | 1 | .40 (.11) | .29 | .06 | .14 | +1.48 | 231–324 | .49 | ok |
  | 2 | .199 (.09) | .37 | .18 | .13 | +1.73 | 293–379 | .38 | forest |
  | 3 | .36 (.08) | .23 | .14 | .11 | +1.45 | 247–382 | .34 | ok |
  | 4 | .25 (.04) | .21 | .21 | .15 | +1.41 | 245–329 | .22 | forest varies |
  | 5 | .26 (.10) | .24 | .20 | .13 | +1.02 | 157–271 | .20 | ok |
  | 6 | .32 (.09) | .22 | .18 | .15 | +1.44 | 241–383 | .45 | ok |
  | 7 | .27 (.09) | .42 | .18 | .12 | +0.86 | 119–286 | .93 | population |
  | 8 | .24 (.13) | .31 | .23 | .12 | +1.56 | 281–368 | .35 | ok |
  | 9 | .21 (.08) | .27 | .19 | .13 | +1.31 | 206–325 | .42 | ok |
  | 10 | .29 (.09) | .21 | .24 | .13 | +1.15 | 179–307 | .47 | ok |

  All seeds: predator CV ≥ 0.15, no extinctions, fish stable, fires 4.6–8.6/decade, a wildfire in every 50-year window.
- Save size: ≈ 187 KB at year 40 (D-026); default world `assets/worlds/default.tcsave` (seed 3, 158 KB, D-027)
- Bundle size: 2.66 MB release

## Stage entries

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
