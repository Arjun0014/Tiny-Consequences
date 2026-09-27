# Session handoff — end of Chapter 1 (27 Sep 2026, ~14:45 IST)

Chapter 1 is complete and committed; Arjun is playtesting. `docs/PROGRESS.md` is the source of truth for state and numbers; this file only adds what a fresh session would otherwise have to rediscover.

## State
- Stages 1.1–1.10 done. `npm run test:sim -- --full` 67/67, quick 67/67, `npm run test:web` 8/8 (debug and `--release`), shots reviewed.
- Next: wait for `docs/playtests/chapter-1-feedback.md`, turn it into tasks, then Chapter 2 Stage 2.1 (Powers).
- Default world: `assets/worlds/default.tcsave`, seed 27 (D-042). **Regenerate it after any behaviour-changing sim edit**: `node scripts/lua.mjs tools/make_world.lua` (the unit test `test_worlds.default_world_matches_fresh_generation` fails when it's stale; W06 then also compares browser and LuaJIT hashes of it).

## What this session changed (D-036 … D-046)
Farming calendars per hemisphere; growing-season and capacity-gated site choice; accelerated soil spin-up; fallow only when affordable; daughter villages (spring, provisions, fishing jobs, orphans, size fission); soft population ceiling 450; ocean-evaporation wobble (interannual weather); floods on bankfull = mean annual flood (crop damage, `flooded_village`); per-cell seasonal drought detector; crash trough tracking + natural-crash depth + regional news fatigue; hunger vulnerability for children/elders; W06 cross-runtime check; W08 on the real default world; view budget fixes; visual fixes (ice coasts, opening camera, villagers, inspector).

## Tools worth knowing
- `npm run sim:villages -- --seed N | --world default --years Y [--village NAME] [--from Y] [--summary]`: month-by-month village economy and event chains. This is how the Rialora and colony bugs were found.
- `npm run sim:scout -- --from A --to B --years 60`: how lively each seed's early decades are (for choosing pre-spun worlds, e.g. the Stage 2.1 tutorial world).
- `npm run perf:probe -- --release --viewport 640x360 [--all]`: in-game perf breakdown (sim/view/GC).
- `npm run bench:web -- --release --level 3 --default`: the real world's tick in the browser. Wall-clock is noisy here; P07 instruction counts are the stable measure.
- `node scripts/lua.mjs tools/selftest.lua`: LuaJIT reference hashes for W06.

## Pitfalls met this session
- Southern hemisphere: season labels are northern. Anything seasonal that is per-place (farming, colonist timing) must use `villages.season_shift`.
- Shaders: coast triangles interpolate between land and water vertices. Keep each `var_fx` channel meaning one thing on both sides.
- In Lua, `x and false or nil` is always nil.
- Statistical bands sit near their edges on a few seeds (S01: dry seeds 2 and 9 forest ≈ 0.19–0.20, seed 4 headlines ≈ 0.165; S03 on rich villages). Check both the quick and the full profile after sim changes; the quick seeds 1–3 decide `npm run test:sim`.
- Headless Chromium rasterises on the CPU: in-game ms are inflated 2–4×. Judge perf with the synchronous bench, P07 and the controller contract (W08), not fps.
- This laptop also runs Arjun's other heavy jobs: keep seed sweeps sequential when memory is low (one run once exhausted the commit limit and process creation failed). Git Bash heredocs with apostrophes break; write patch scripts to files.
