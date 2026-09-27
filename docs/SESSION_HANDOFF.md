# Session handoff — World reset, W1 done, W2 in progress (27–28 Sep 2026)

`docs/PROGRESS.md` is the source of truth for state and numbers. The design spec for this work is
`docs/world/` (00 research, 01 audit, 10–16 design, 17 roadmap v2). Decisions D-050–D-052.
Arjun's instructions for this phase: **build the living world first, jams secondary; no
subagents; hand off at ~700K context.**

## State at handoff
- **W0 (reset)**: committed c80a47e — research with sources, audit, design docs, roadmap v2.
- **W1 (time)**: committed 4318145 — hourly master clock, local solar time, day length, daily
  weather, speed ladder, real day/night in the view. Quick suite was 85/85 at that commit.
- **W2 (settlements)**: committed as **WIP** at handoff (see the commit after 2a62d26). The new
  settlement layer runs (`sim/society/*`), replacing `sim/systems/villages.lua` and
  `people.lua` in the systems list (the old files are still in the tree, unused except by old
  tests). Headless, six seeds survive 80 years with 180–630 people, daughter villages,
  abandonments, recoveries, and emergent chains such as
  `lightning_strike > fire_started > wildfire > crop_failure > famine > deaths_spike > seed_eaten >
  migration > village_abandoned`.
- **Not yet done in W2**: tests ported, saves bit-identical, pre-spun worlds regenerated, the view
  and inspector reading the new schema, tools (`sim:villages`, `sim:scout`, `sim:tutorial`)
  ported. **The game build probably breaks** where the view reads old fields (`v.boats` is now a
  list of boat ids; `W.ent.boats` is a map of real boats; people have `born` in hours).

## First things to do next session (in order)
1. `npm run lint:lua` (clean at handoff) and `node scripts/lua.mjs tests/sim/run.lua --unit`
   (58/63 at handoff). Fix:
   - **Save determinism** (`test_save_world`, `test_worlds`): derived caches that change behaviour
     must be rebuilt identically after a load. Culprit found: `fishing.rank` fills
     `W.vgeo[id].grounds` weekly, but after a restore it is rebuilt lazily inside `fishing.plan`
     with a different hours argument. Fix: rank in `sys.rebuild`/on load with the same inputs,
     or store the ranked list in saved state (`v.grounds`). Audit the other caches (`G.woods`,
     `G.walkd`, `G.fishd` are pure distance caches — fine; `v.fuel_n`, `v.need_pop` are saved —
     fine).
   - `test_worlds`: regenerate `assets/worlds/default.tcsave` and `tutorial.tcsave`
     (`node scripts/lua.mjs tools/make_world.lua` and `... --seed 8 --out
     assets/worlds/tutorial.tcsave`) after the save fix. `init.lua:49` "table index is nil" is the
     stale save (villages without `bucket`).
   - Rewrite `tests/sim/unit/test_villages.lua` for the new schema, then add the behaviour tests
     B01, B04–B10, B12, B13 (`docs/world/17_ROADMAP.md`).
2. Run the scenario/property suites (`--props`, `--scenarios`) and port what breaks (S03 uses
   village farms/pop@community, S04 food_security, S11 blight on `v.farms`, S15 the tutorial
   chain, P10 settle report, P08). Old fields kept for compatibility: `pop`, `members`, `farms`,
   `fishing`, `food_security` (months), `houses` (count, weekly), `state`, `cause`, `alive`.
3. Tune toward bands (see PROGRESS): village churn is high (12–18 founded, 7–14 abandoned per 80
   years); populations grow ~1%/yr on rich seeds; refugee colonies often fail. Then set the new
   S01 society bands (D-052 lists candidates).
4. Performance: society ≈ 390k VM instructions per week at ~375 people (planet ≈ 500k). Hot:
   `labour.assign` (~16k per call), `life.weekly` (~160 per person), `fishing.settle` (a hash per
   boat per day for squalls — use one hash per village-day). Re-baseline P07 with a DECISIONS
   entry once stable (P07 now profiles hourly systems as `villages:hour`).
5. W2c view: buildings from `W.ent.buildings` (lots via `sim/society/layout.lua`), people from
   their task/site + the day rhythm (write `sim/society/schedule.lua`), boats from `trip`,
   parties walking their `path`. Then the inspector (village stocks, `v.acc` yearly flows,
   households, fields).

## Tools worth knowing (new)
- `node scripts/lua.mjs tools/society_trace.lua --seed 22 --years 30 --every 12 [--events] [--village N]`
  — generates a world and prints every village (pop, households, homes/sites, couples waiting,
  boats, fields/sown, season phase, food days with grain/fresh/seed, wood days, ration, labour
  groups) plus society events with their chains.
- Debug scripts in `out/tmp/` (gitignored): `bal_trace.lua` (yearly flows per village from `v.acc`
  through the spin-up), `spin_trace.lua`, `ev_trace.lua`, `plan_dbg.lua`, `prof_soc.lua`
  (instruction counts per society function).
- Bridge: `run_hours {n | to_local}`; `__TC.hour`, `hour_f`.

## Pitfalls met
- Git Bash heredocs mangle backslashes (`\194` became a control byte) — write Python patch
  scripts to files with the Write tool and run them.
- `local fx` in `view.lua` shadowed the effects module `fx` — watch local names in big modules.
- Loops over `v.boats` must go backwards when a boat can be lost (removed) inside the loop.
- A profiler wrapper must return all results (`unpack` of a packed table).
- The Browser pane stops rendering frames while hidden; use `scripts/bridge_cmd.mjs --shot` for
  screenshots instead.
- Units: weekly EMAs (`v.norm.*_in`) vs daily needs — multiply weekly by 48 for a year.
