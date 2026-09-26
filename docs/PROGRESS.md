# Progress log

Claude Code: read this first in every session. Keep "Current state" accurate; append stage entries below it (newest first). Keep entries short: what changed, test status, key numbers, open issues.

## Current state

- Chapter: 1 — A World That Lives Without You
- Stage: 1.3–1.5 in progress (worldgen terrain done; climate/water and vegetation/fire/biome implemented and tuned headless; settlement, volcano sites and tanker lanes come with 1.7)
- Plan for the rest of Chapter 1: ocean life + oil + fauna (1.6) → villages/people/ships + worldgen settle + detectors (1.7) → rendering to docs/ART_DIRECTION.md (1.8) → camera/HUD/inspector (1.9) → hardening + playtest report (1.10).
- Blockers needing the human: none
- Last human feedback read: none yet (Arjun shared the target look mid-session → docs/ART_DIRECTION.md)

## Environment (verified 27 Sep)

- Windows 11, Git Bash. Node 22.14, npm 10.9, Java 26 (Oracle), Git 2.46.
- LuaJIT 2.1 installed with `winget install DEVCOM.LuaJIT` to `C:\Users\aswin\AppData\Local\Programs\LuaJIT\bin\luajit.exe` (on the user PATH for new shells; `scripts/lua.mjs` resolves it anyway). No lua5.1 (optional).
- Defold 1.13.1 bob in `tools/bob/bob.jar` (`npm run setup` re-downloads). Bob platform flag is `wasm-web` (not `js-web`). The bundle lands in `dist/web/app/`.
- Python is `python` (3.13), not `python3`; set `PYTHONUTF8=1` when writing files with it.

## Key numbers (update as they change)

- SIM_LEVEL: 3 (provisional, D-004) — 642 cells
- Detailed tick, weather+veg+fire+biome only (HTML5 release, headless Chromium): L3 4.6 ms median, L4 18.9 ms. LuaJIT headless L3 ≈ 1 ms.
- Epoch tick (HTML5 release): —
- Baseline bands (level 3, 4 seeds × 200 y, no villages yet): forest 27–40%, grassland 27–36%, desert 4–15% (seed 1 low), ice 12.6–15%, g_temp +0.1…+0.16, fires 117–175 per 200 y, wildfires (≥3 cells ≈ 10 L4 cells) 22–30 per 200 y
- Bundle size: 2.66 MB release

## Stage entries

### [ch1.s2] Sim foundation — DONE 2026-09-27 02:20 IST
- What: `sim/` core in pure Lua 5.1: rng (counter-based, 16-bit-split mul32), grid (icosphere + neighbours sorted CCW, areas, nearest, BFS), calendar, fields (catalogue, SoA, prev buffers, year-boundary 16-bit snap with a sqrt mode for small values), events (records + flat parent/root arrays so chains survive GC, aggregation, cooldowns, descendants), blame (fixed-arity step with fast path, inflow tags), ledger (global co2/aerosol/g_temp ledgers), world (tick orchestration, commands queue/log, state_hash, profiling hook), save (text-safe format, capture at year boundary, restore + replay). Tools: run_sim (summary/series/events/maps), map_dump + pure-Lua PNG writer, zonal diagnostics, bench (LuaJIT) + bench:web (real HTML5 runtime via bridge).
- Tests: `npm run test:sim` 33/33 (rng, grid L2–L4, calendar, events, blame, save round trip bit-identical mid-year with a command, names, P01 determinism). lint clean.
- Numbers: see Key numbers; SIM_LEVEL decided provisionally from the browser benchmark (D-004).
- Also done early: 1.3 terrain, 1.4 climate & water, 1.5 vegetation/fire/biome (see Current state).

### [ch1.s1] Toolchain & skeleton — DONE 2026-09-27 02:05 IST
- What: Defold project, custom render script, input bindings, test bridge (`?test=1`, `window.__TC`, `__TC_CMDS`), speed controller skeleton, npm scripts (setup/build/serve/test:web/shots/play + sim wrappers), lint:lua (Lua 5.1 + sim purity rules, selftest), Playwright config with SwiftShader flags.
- Tests: test:web 3/3 (W01 smoke, W02-lite speed round trip, bridge error reporting); lint 0 violations.
- Commit: ab5d26a
