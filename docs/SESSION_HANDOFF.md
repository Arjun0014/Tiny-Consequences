# Session handoff — Chapter 2, mid Stage 2.2 (27 Sep 2026, ~21:10 IST)

`docs/PROGRESS.md` is the source of truth for state and numbers; this file only adds what a fresh session would otherwise have to rediscover. The previous session ran to ~1M context before handing off: **hand off at ~700K.**

## State
- Playtest-1 feedback work committed in 74d9fe0 (f12 sim with 1,442 cells, D-047; renderer v2 with shadows, post chain, props and animated movers, D-048).
- Stage 2.1 Powers committed with this handoff: 8 powers in `sim/powers.lua` (logged `power` command), power bar + aiming ring + cast effects in the game, scenarios S02–S04 ported to powers, new S05/S06/S07/S10/S11/S12/S15, tutorial world (seed 8), unit `test_powers`, web W03. D-049.
- Stage 2.2 partly done: P08 (attribution 96%), P09 (headline rate 0.29/y), heatwave / cold_snap / sea_ice_formed detectors, volley attribution, blight_spread headline. Not done: reading a batch of `events.txt` chains for missing links (03 §5 review).
- Stage 2.3 started: `content/event_text.lua` (icon + one-line text per event kind) — nothing uses it yet.

## First things to do next session
1. `npm run test:sim` (quick) and `npm run test:sim -- --full` (last full run, 78/78, predates the heat/sea-ice detectors and the retag guard in `sim/powers.lua`).
2. Check the pre-spun worlds are current: the unit test `test_worlds` fails if `default.tcsave` is stale. If so: `node scripts/lua.mjs tools/make_world.lua` and `node scripts/lua.mjs tools/make_world.lua --seed 8 --out assets/worlds/tutorial.tcsave`, then re-run S15 and W06.
3. W03 (`tests/web/w03_powers.spec.mjs`): the "pulse visible" diff checks for Chill/Heat/Rain are borderline (chill diff 4.1 vs threshold 5). Either make the pulse disc brighter/longer or check the sim effect (e.g. via the bridge) instead of pixels. Test takes up to ~15 min.
4. Stage 2.3: chain popup, "Why?" on the inspector, event log, using `content/event_text.lua` and `events.chain`.

## Tools worth knowing
- `npm run sim:tutorial`: scouts tutorial seeds (tanker near a fishing village; S15 chain).
- `npm run test:sim -- --level 3`: run the sim tests at the old 642-cell level for comparison.
- Bridge (`game/platform/commands.lua`): `power`, `arm`, `look`, `gfx` in addition to the Chapter 1 set; `?gfx=low` in the URL turns off shadows/post for functional web tests.
- Earlier tools (`sim:villages`, `sim:scout`, `perf:probe`, `bench:web`, `tools/selftest.lua`) still work; see the Chapter 1 notes in PROGRESS.md.

## Pitfalls met this session
- `render_target` `sample_count` does not exist in Defold 1.13.1 (no MSAA; FXAA in the post chain instead).
- Material sampler order matters: a material's first sampler binds to texture unit 0. `props.material` must list `celltex` first or it samples the shadow map.
- World-space movers can't take an extra vertex stream (crash/hang); limb data is packed in the colour vec4. Every mover `.buffer` must declare `color` with count 4.
- Varyings must match between every vp/fp pair sharing a fragment program (props_ws.vp needed `var_mat`).
- `/tools` is in `.defignore`: anything the game needs at runtime lives in `sim/` or `content/`.
- Scenario selectors: specific branches (`village:farming`) must come before the generic `village` branch or they are shadowed.
- A test's `run_years` must exceed its action year or it gets no samples.
- Multi-cast powers (one cast per pack/farm) need "volley" attribution: detectors credit the player when player tags jointly dominate, and re-tag across the volley.
- Don't edit sim files while a web test is running (W06 compares hashes of the built world).
- Timestamps: this machine's `date` in Git Bash may print UTC; use `powershell -c "Get-Date"` or add 5:30. Earlier entries were stamped 28 Sep by mistake (fixed).

## Questions for Arjun (non-blocking)
- S07 (invasive grazers on an island) settles at vegetation ≈ 0.55–0.6 rather than a collapse; threshold set to 0.6 (D-049). OK, or should overgrazing bite harder?
- S12 (Chill over a cold coast) now tests sea ice only; the "fish catch drops" half was dropped because the catch recovers within the season. OK?
- Real-GPU frame rate on your machine with the new renderer (headless numbers are CPU-rasterised). The F3 overlay shows it.
