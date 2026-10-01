# Session handoff — session 9 end (1 Oct 2026): the v3 direction reset

`docs/PROGRESS.md` (session 9 entry) and **D-078** in `docs/DECISIONS.md` are the source of truth.
Arjun's rules for this work: no subagents; the living world first, jams secondary; one long
simulation at a time (shared laptop).

## State
- **Branch `wip/agentic-world-v3`** (from main `0d76765`; not merged). main is unchanged.
- v3 design: `docs/v3/00_DIRECTION_AUDIT.md`, `10_AGENTS.md`, `20_CHARACTER_PIPELINE.md`,
  `30_ROADMAP_V3.md` (R0 done this session; **R1 next**).
- v3 sim: `sim/agents/` (init = the system and event heap; act = steps and effects; think =
  choices and plans; settle = buildings, plots, the hourly board, the daily round, growth;
  life = stages, births, deaths, marriage; found = site and founding; map, nav (A*), weather
  (hourly rain), clock (48-day year, local seasons), people, worldgen). v2 code untouched except
  hpt-awareness in `sim/world.lua` and `fauna.lua` (v2 hashes identical: quick suite 109/109).
- v3 view: `game/view/agents_view.lua` (+ hooks in propgen, view, planet, camera, picking,
  inspect, hudstats, speed, main.script, render script); villager models `assets/characters/`,
  materials `game/render/villager.material`, `shadow_villager.material`.
- World asset: `assets/worlds/v3.tcsave` (seed 22, one settlement of 32; regenerate with
  `luajit tools/make_world.lua --v3 --seed 22 --out assets/worlds/v3.tcsave` after any change to
  founding or world generation; behaviour changes do not need it — the save is the founding state).
- Verified at the last commit: quick sim suite 114/114 (v2 109 + v3 5), web suite 8/8, lint clean,
  `npm run build` OK, `npm run shots:v3` OK (out/shots/v3/). The settlement is now Tekheim (seed 22,
  cell 903, lat 13.7: the north, so day 0 is its spring).

## How to look at it
- Game: `npm run build`, `npm run serve`, open `http://localhost:8080/?world=v3` (keys 1-3 watch
  speeds, 4-6 catch-up; click a villager for the inspector, "Follow" in it). The default (no
  `?world=`) is still the v2 world.
- Headless shots: `npm run shots:v3 [-- --hours 6.2,8.5,12.5,18.2 --person 12]`.
- Headless follow: `luajit tools/v3_run.lua --seed 22 --days 3 --follow 3,12 [--hist] [--all]`.
- Cost: `luajit tools/v3_scale.lua --settlements 6 --days 3` (and `luajit -joff ...`).
- Character pipeline: `blender -b --factory-startup -P tools/blender/build_villager.py -- --out
  assets/characters`; preview `... -P tools/blender/preview_villagers.py -- out/s9/villagers.png 0 6.5`.

## Next (R1, in order)
1. Watch a whole year (48 days) headless and in the game; follow 5 people of different stages
   for 3 days each; fix what reads wrong (see "Known oddities").
2. Infants drawn in arms (a "babe" part on the carer); children following a parent; apprentices
   beside their mentor.
3. Lit windows at night (people indoors and awake) and chimney smoke from burning logs.
4. Far LOD to ~150-250 triangles (drop minor parts, merge limbs); facing work targets.
5. Catch-up UI (⏭ to dawn / a season), HUD speed buttons for the v3 ladder.
6. Performance for catch-up: lazy why-strings, fewer hashes, a coarse grid for long A* trips,
   the planet's daily step (≈30% of cost) amortised. Target in roadmap v3 R1.
7. Then R2: several settlements across the world (worldgen `settlements` exists), coasts/rivers on
   the local map, fishing boats.

## Findings and pitfalls (session 9)
- The engine passes a fixed `dt` per frame: v3 time uses real elapsed time (`speed.step_v3`).
- A hidden browser pane throttles rendering (the in-app Browser pane loads in ~60 s and runs at a
  few frames): use `npm run shots:v3` (headless Chromium) for checks.
- Defold models create a game object per bone unless `create_go_bones: false`.
- Per-instance vertex attributes on GPU-skinned instanced models abort Defold 1.13.1
  (`size <= Capacity()`): use per-component constants.
- The bridge's command objects use `id` for the command id: name parameters otherwise (`pid`).
- Saves copy tables: aliases (`s.granary` = a building) must be re-linked on load (`sys.init`).
- Seasons are local: day 0 is the northern spring; the south is 24 days on (`clock.local_doy`).
  The first settlement is placed in the north so its founders begin at their spring.
- Git Bash heredocs with apostrophes break: write patch scripts to the scratchpad and run them.

## Known oddities (not yet fixed)
- People carry spare seed back to the granary between sowing and other work.
- Two gatherers can choose the same tree; the woodcutters' felling target is the nearest tree to
  the woodstore (clearings grow from there).
- Evening gatherings and afternoon rests are still a little uniform on quiet days.
- The far LOD still carries every part (808 triangles); the near mesh with all parts 1,468.

## Questions for Arjun (non-blocking)
- Is the watch ladder right (▶ 1 game-min/s, ▶▶ 4, ▶▶▶ 12 = a day in 2 minutes)? Should the
  default start speed be ▶▶?
- Make v3 the default world on this branch now, or keep `?world=v3` until R1 is done?
