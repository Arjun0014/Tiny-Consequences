# Session handoff — W2 finished, view drawn from the society, clouds v3, W7 started (28 Sep 2026, ~03:30 IST)

`docs/PROGRESS.md` is the source of truth for state and numbers. Spec: `docs/world/` (10–18).
Decisions this session: **D-054** (W2 finished: seed 9 mechanisms, weather recalibration, scenario
ports, contributing causes, the view from the society, clouds v3) and **D-055** (the deep world
W7–W10 and its first vertical, relations/relief/barter/brides). Arjun's rules: living world first,
jams secondary, **no subagents**, hand off before the context runs out (this session: at ~650K).

## State at handoff
- **Sim**: W2 is complete (D-054). Seed 9 no longer collapses (land-shared capacity, Boserup
  intensification `care`, closed doors for hungry hosts, fission of young families at the land
  limit, fish-only field planning). Daily weather recalibrated (wet ~2 days in 3 at the wettest
  sites, storms ~6%). Contributing causes (`co`) on events; P08 96%. W7's first vertical is in
  (`sim/society/relations.lua`, D-055): relief, wood-for-grain barter and brides carried by real
  porter parties.
- **View**: settlements from `W.ent.buildings` on their lots with construction stages, earth houses,
  ruins, weathered roofs, woodpiles/sacks/fish racks from stocks, stooks on harvested fields, stumps
  and logs on logged cells; people from `schedule.where` (walking to and working at their real
  sites, the well, visiting neighbours, asleep indoors); travellers along party paths; boats from
  trips; birds (gulls over boats hauling nets and rich coastal water, songbirds over woods by day);
  denser vegetation (11 near / 4 far slots, understory, rocky shores, driftwood); clouds from the
  daily weather at a safe altitude with a zoom cut-away; zoom to 1.35 (eye 1.5 units from the
  ground); new inspector (stocks, fields, boats, this week's labour, last year's flows, trade;
  a person's whereabouts, task, home, household).
- **Tests at handoff**: quick suite **93/95** (`out/quick6.txt`). Red:
  - `behaviour.b_food.b08_famine_changes_behaviour` **passes alone** (`node scripts/lua.mjs
    tests/sim/run.lua --behaviour --only b08`: conceptions 0.000 vs 0.016) but fails inside the full
    suite (0.111 vs 0.061): order-dependent — suspect the cached fresh world or a module-level
    scratch shared with earlier tests (the test switches relief/trade/brides off via config). Find
    the shared state; don't loosen the test.
  - the fauna band test below.
  Web: `out/web2.txt` (run started after the sim suite; W02/W06/W08/W09 fixes listed below were
  not yet verified in a web run). Known weak spots:
  - `props.fauna_stability.vegetation_bands_with_fauna_200y` is borderline on seed 1 (forest moved
    by grazing ~3.5 points near the 0.20 band edge while in band without animals): flaky with any
    society change; decide whether the 3-point rule or the 0.20 band should follow D-054's human
    pressure (S01's forest band is already 0.10).
  - W02/W06/W08/W09 were failing in the mid-session web run for known reasons, fixed after it:
    W09 label "People", W08 now `gfx=low` (the CPU rasteriser can't draw renderer v3 at speed),
    clouds capped at 40 and birds off under `gfx=low`, W06 was polluted by regenerating the
    default world while the test ran. Re-check `out/web2.txt`.

## First things to do next session (in order)
1. `npm run test:sim` and `npm run test:web`; fix what the handoff left red.
2. **Look at the game** (`npm run shots:village -- --weeks 20 --zoom 1.2`, `npm run shots`): the
   village is still dense (houses in rings 1-2 crowd the square; consider lot spacing by ring or
   houses facing lanes), rain streaks close up, cloud distribution (clusters on the rainy side).
3. **W7 continued** (`18_DEEP_WORLD.md` §2.3): imports in the field plan (specialisation), porters
   drawn carrying sacks, trails where parties walk, seed sweeps to measure relief's effect on
   famines at the land limit (`out/tmp_rel.lua <seed> <years>` counts relief/trade/brides).
4. **W6 performance before raising the soft cap** (900): monthly body updates for the healthy,
   `labour.assign` and `life.weekly` are the largest items (`out/tmp/prof_soc.lua`). Populations
   reach the cap on most seeds by year 50-70.
5. W8 (lineages, prestige, leaders, raids, palisades) on the relations graph.

## Tools
- `npm run shots:village [-- --weeks N --hours a,b --zoom Z --seed S]` — a village through its day
  (+ inspectors) in `out/shots/village/`.
- `node scripts/lua.mjs tools/society_ledger.lua --seed 9 --years 100 [--summary]`;
  `PYTHONUTF8=1 python <scratch>/claude_led.py` was a throwaway parser (per-village pop/cap/harvest
  table) — rewrite it into `tools/` if needed.
- `out/tmp_where.lua <local hour> <weeks>` (who is where in the biggest village of the default
  world), `out/tmp_storm.lua` (storm/wet day shares), `out/tmp_p08.lua <scenario> <seed>` (P08
  misses with chains), `out/tmp_ev.lua <seed> <y0> <y1> "<village ids>"` (events of villages).

## Pitfalls met this session
- Bob rejects require cycles even through lazy requires: shared helpers go in modules with no
  requires (`sim/society/cotag.lua`).
- `go.set_scale` rejects zero/negative components (bird wing-beats scale y between 0.12 and 1).
- `mesh.max_count` (now 2048) caps pooled movers + chunks + clouds.
- Don't regenerate `assets/worlds/*.tcsave` while a web test runs (W06 compares hashes).
- Headless (SwiftShader) fps collapses with dense geometry: web tests that measure speed use
  `gfx=low`.
- Git Bash heredocs with apostrophes break: write patch scripts with the Write tool.
