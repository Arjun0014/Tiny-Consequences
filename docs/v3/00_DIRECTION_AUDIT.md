# V3-00 — Direction reset: an automated miniature civilisation (D-078)

Written 1 Oct 2026 (session 9) on branch `wip/agentic-world-v3`, from main `0d76765`. Supersedes
the human/activity parts of `docs/world/10`, `12`, `15` for v3 worlds. The planet layer docs
(`02`, `docs/world/14`) stay valid.

## 1. Why

Arjun, after watching the v2 world: it *looks* good, but people and animals behave like scheduled
groups. They move together, stop together, jump between activities and read as animated
statistics. That is not acceptable any more.

The cause is architectural, not a tuning problem:

- A v2 person's position is a **pure function** `schedule.where(task, hour)` (`sim/society/
  schedule.lua`). Nothing walks; the view evaluates "where would a farmer be at 10:00" and draws
  them there. Everyone with the same task and village shares the same wake hour, walk window and
  work window, so they move as a block.
- Work is **accounted**, not done: `labour.assign` hands out hours weekly; the day step adds
  `hours × rate` to stocks. The visible farmer is decoration over a formula.
- Villages are **large and shallow** (2,400 people, 30 villages; the D-077 run went to 4,800).

## 2. The target

An automated, Age-of-Empires-like miniature world that runs without the player. Small settlements
(20-30 people each at founding) across the planet gather, farm, fish, hunt, cut wood, build homes,
raise children, form households, trade, migrate by land and boat, expand, fight, raid, defend,
recover or collapse. The player is a god: they nudge the world; they never command villagers.

**The visible action is the simulation.** A woodcutter's trip *is* the wood entering the store; a
harvest *is* sheaves carried to the granary; a house appears because builders carried logs to a
site and worked there.

**Population target**: 50-200 meaningful people in the whole world (session 9 slice: one
settlement of ~30). Measured cost decides the ceiling, not the old caps (see §6).

## 3. Audit: what survives, what is rewritten, what is removed

| System (v2 module) | Verdict | Notes |
|---|---|---|
| `sim/rng.lua`, `grid.lua`, `fields.lua`, `ledger.lua`, command queue/log, state hash | **survives** | foundation |
| Planet: temperature, moisture, ice, hydrology, vegetation, fire, ocean, biome, global climate | **survives unchanged** | one planet step is still 1/48 year; in v3 it runs once per game day (§5) |
| Causality: events, blame, detectors framework | **survives** | v3 events (`house_built`, `tree_felled`…) carry causes; chains end at Nature or You |
| Wild herds/packs (`sim/systems/fauna.lua`, W4) | **survives** (ecology) | the validated predator-prey model stays the herd/pack *macro* layer; individual visible animals are a new micro layer (§V3-10 §8) |
| Powers (`sim/powers.lua`) | **survives** | Oil spill stays a god power on the ocean (fishing and ecology respond) |
| Daylight (`sim/society/daylight.lua`) | **survives** | sunrise/sunset per place; day-of-year mapped from the v3 calendar |
| Daily weather (`weather_day.lua`) | **rewritten** for v3 | a planet step is now a day: hourly rain spells come from that day's precipitation (`sim/agents/weather.lua`) |
| People/households/buildings entities (`sim/society/entities.lua`) | **rewritten** | v3 agents are persistent bodies with position, needs, objective, path (`sim/agents/`) |
| `schedule.lua` (positions as a function of task and hour) | **removed** in v3 | the root of the fake movement |
| `labour.lua` (weekly central assignment) | **removed** in v3 | replaced by a job board + individual utility choice |
| `farming.lua`, `construction.lua`, `fishing.lua` (hours × rate at the day step) | **rewritten** | production happens in the work actions themselves |
| Village layout as lot rings (`layout.lua`) | **rewritten** | a local tile map with footprints, lanes, plots, individual trees |
| Life/demography rates (`life.lua`), marriage/household rules (`12_PEOPLE_VILLAGES` §1-3) | **mechanisms kept, code rewritten** | Hajnal household formation (a couple needs a home), hunger and cold hazards, conception by nutrition — re-expressed per agent and per day |
| Memory of hunger (D-064/D-074), obligations, gifts, feasts, standing (D-075 draft) | **kept for later phases** | ported when several settlements exist (V3 roadmap R5-R6) |
| Relations, conflict, raids, polities (`relations.lua`, `conflict.lua`, `status.lua`) | **kept for later phases** | re-expressed through agents (raiders walk/sail, defenders fight) — R6 |
| Mobility/migration/founding by walking parties and boats (`mobility.lua`, `sea.lua`) | **kept for later phases** | parties become groups of real agents — R5 |
| Crafts, herds of livestock (`crafts.lua`, `herds.lua`) | **later** | livestock become visible animals tended by herders |
| Tanker shipping (`sim/systems/ships.lua`, `W.ent.ships`) | **removed** in v3 | modern; not part of a timeless world economy (§4) |
| v2 view puppets driven by `schedule.where` (`game/view/puppets.lua` people part) | **replaced** in v3 | `game/view/agents_view.lua` draws agents where the sim says they are |
| Old tests (109 quick) and scenarios | **kept** for v2 worlds | v3 has its own tests (`tests/sim/v3/`); v2 code is not deleted until v3 reaches parity |

The v2 simulation keeps running for v2 worlds (`W.v3` unset), so nothing is destroyed while v3
grows.

## 4. Medieval / timeless world cleanup

- **Tankers removed** from v3 worlds: no `ships` system, no tanker lanes, no "lamp oil" economy.
  Boats in v3: fishing boats, trade boats, migrant boats, raid boats — all built and crewed by
  settlements.
- **Oil spill remains a god power** (`powers.oil` on an ocean cell): the player pours it; plankton,
  fish, the fishers' catch and the chronicle respond. It no longer needs a tanker.
- Audit of content for modern elements (to clean in R1/R2): tanker names (`content/names.lua`
  `names.tanker`), tanker event texts (`tanker_sunk` in `content/event_text.lua`), HUD/inspector
  tanker strings, the "carbon ppm" framing in the HUD (keep the climate model; present it as
  weather lore rather than a modern dashboard later), "porters" and "polities" naming is fine.
  Nothing else modern was found in the sim (no engines, guns or industry).

## 5. Time: watchable, never teleporting

- **Calendar v3**: one planet step = one game day (24 h). A year is 48 days (4 seasons of 12
  days), like RimWorld's 60-day year. All planet tuning holds, because a planet step was already
  1/48 of a year. A crop grows in ~3 weeks of days; a child grows up in ~14 years = 672 days.
- **Speeds** (game time per real second; `game/platform/speed.lua`):

  | Level | Rate | A day takes | Use |
  |---|---|---|---|
  | ⏸ | 0 | — | inspect |
  | ▶ | 1 game-min/s | 24 min | walking reads at a natural miniature pace |
  | ▶▶ | 4 game-min/s | 6 min | default watch |
  | ▶▶▶ | 12 game-min/s | 2 min | fastest **watch**: walking is brisk but every trip is visible |
  | ⏭ Skip | catch-up | — | separate mode (D-079): until dawn / until evening / one day / the next season; the view hides the people and runs the sim unrendered as fast as the budget allows |

- Agents move along continuous analytic paths (start time, waypoints, speed), so any speed shows
  the same continuous motion; nothing jumps when a frame covers more time.

## 6. Population and performance target

- Deep agents are costlier per head than v2 rows; the budget is per *agent-day*: decisions,
  pathfinding and needs. Event-driven agents spend nothing between decisions.
- Target: 200 agents in the world at ▶▶▶ in the HTML5 release with < 2 ms sim per frame, and a
  catch-up rate of at least a season (12 days) per ~5 real seconds with 200 agents. The session 9
  slice measures one settlement (see `docs/PROGRESS.md` for numbers).
- No further work on the 4,800-person experiment (D-077 stands as a finding).
