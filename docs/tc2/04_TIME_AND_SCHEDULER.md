# TC2-04 — Time, the scheduler and resolution

Decisions: D2-004 (time units), D2-005 (scheduler), D2-006 (resolution layers), D2-010 (offline).

## 1. Time

- Sim time is a `long` count of **seconds** since world creation (`SimTime`). 1 year = 48 days =
  4,147,200 s; a million years fits easily in 64 bits.
- Calendar helpers derive year, season, day, hour, minute; local solar time per position (longitude);
  day length and sun elevation per latitude and day of year.
- No floating-point time inside Core. Durations computed in double are rounded once
  (`floor(x + 0.5)`) when an event is scheduled. The host's fractional time (for smooth rendering) lives
  only in the Game layer.

## 2. One ordered queue

Everything that happens is an **event** in one future event list:

```
struct ScheduledEvent { long Time; int Phase; long Seq; EventKind Kind; int Target; int Version; long A, B; }
order: (Time, Phase, Seq)      Seq = global insertion counter (serialized)
```

| Phase | Class (fixed order at equal time) |
|---|---|
| 0 | commands (player, god powers, test premises) |
| 10-19 | periodic planet systems, each its own phase (fixed system order) |
| 20 | processes (fire, flood, fights) |
| 30 | agent and animal activity steps, life events |
| 40 | housekeeping (yearly compaction, audits in debug) |

- **Lazy cancellation**: targets carry a version stamp (e.g. `Person.ActVer`); an interrupt increments it;
  stale events are discarded when popped. No heap deletions.
- **Structure**: a 4-ary min-heap of structs; events farther than a horizon (~1 year) wait in coarse
  buckets and are moved into the heap when their bucket comes due (keeps the heap small). Measured.
- **Creation schedules** (TC1 D-176): every API that creates an agent or a process schedules its first
  event in the same call. Audit: every live agent has exactly one pending non-stale step event.
- **The queue is state**: it is serialized exactly (with `Seq` and versions) and never rebuilt.

## 3. Periodic systems

A periodic system is a recurring event `SystemTick(systemId)`: on dispatch it steps its fields over
`dt = period` and reschedules itself. Initial rates (watched or caught-up alike; only the deep-time
policy may change them, at a logged world time):

| System | Grid | Period | Notes |
|---|---|---|---|
| Insolation + temperature + moisture + precipitation | N32 | 6 h | diurnal and synoptic weather texture |
| Daily weather realisation (spells, storms) | N32 + storm entities | 1 day | rain spells hour by hour from the day's state |
| Hydrology (soil water, runoff, rivers, lakes) | N64 land | 1 day | flow on the drainage DAG |
| Vegetation, soil, fuel | N64 land | 4 days | PFT biomass, succession |
| Wild populations (abundance, dispersal, condition) | N64 | 4 days | super-individual herds near people are agents |
| Ocean life | N64 sea | 4 days | plankton, fish |
| Global climate (CO₂, aerosol, sea level, ice volume) | global | 1 day | ledgers of contributors |
| Evolution (selection, drift, speciation checks) | populations | 1 year | per breeding season |
| Tectonics (plate motion, boundaries, stress) | N64 | 10 years | quakes and eruptions are scheduled events |
| Erosion and deposition | N64 | 10 years | Braun-Willett implicit stream power |
| History compaction, era detection | ledger | 1 year | observers run outside the queue |

## 4. The four resolution layers (D2-006)

### 4.1 PROCESS (authoritative, sparse, fine)
Fire spreading through a street or a wood, flood water rising, a fight, a rescue, a collapsing building.
A process exists only while active, steps every 1-15 minutes of world time (by kind and intensity), and
owns its phenomenon at its place. It runs whether or not anyone watches.

### 4.2 ACTIVITY (authoritative, everyone, event-driven)
Every person (and every livestock animal and every wild herd agent) always has a current activity:

```
Activity { kind, target, plan[step...], stepIndex, t0, t1, from, to, routeId, claims, why }
step kinds: Travel(route) | Work(duration, effect) | Use(building) | Wait | Sleep | ...
```

- An **activity step's end** is the person's next event. Effects apply at step ends (a row sown, a tree
  felled, logs added to a store), proportionally if interrupted.
- **Decisions** happen at step ends: candidate generators (body, care, household, occupation work from
  the settlement's work board, social, travel, urgent responses) score candidates by need x fit - cost;
  the best wins (deterministic ties, keyed small noise for individuality). Every choice keeps a compact
  *why* (the state that made it best), formatted only on inspection.
- **Travel is analytic**: a leg is a polyline with a start time and a speed profile; position at any t is
  a pure function. Inside settlements, legs follow the settlement's path graph; between settlements,
  routes over the N64 cells (A* with slope, water, forest and road costs).
- **Interrupts**: hazards, alarms, calls for help or news cut the current step at a computed moment;
  the person re-decides from where they stand (partial work kept).

### 4.3 COARSE (authoritative, deep time only; designed, not built in the foundation)
For catch-up beyond what ACTIVITY can do in the time available: households, not persons, are the
decision unit, integrated by season with the same production, consumption and demographic laws
(calibrated against ACTIVITY by distribution tests). Entering COARSE happens at a world midnight
(everyone home); leaving it instantiates activities at the next dawn. Persons, families, knowledge and
relations stay individual. Processes still run at their own resolution.

### 4.4 ELABORATION (presentation only, observed agents)
Footsteps through streets, gestures, the members of a herd around its agent: generated in the Game
layer from the authoritative activity (same endpoints, same times, same effects); never written back.
When the camera arrives, the elaboration is generated from the current activity ("alibi"): what you
see is consistent with everything that was simulated.

## 5. Journeys that meet hazards (the space-time index)

- Every travel leg registers `(agent, version, cell, t_enter, t_exit)` for each surface cell it crosses
  (`TravelIndex`, derived and rebuilt from activities on load).
- When a hazard appears or grows at cell `c` (fire, flood, a raiding party), the hazard queries the cells
  within its perception radius for legs with `t_exit > now`, and schedules for each traveller an
  `Interrupt` at the first moment they would perceive it (`max(now, t_enter of the perception ring) +
  personal reaction delay`).
- Travellers only know hazards they perceived or heard about: a journey begun in ignorance is a real
  journey; the knowledge arrives when the senses or a messenger deliver it.
- On interrupt the traveller chooses among continuing, waiting, detouring (route avoiding the hazard with
  a margin), turning back, or fleeing; the choice is recorded in history (personal tier) with the hazard
  as its cause.

## 6. Host time (Unity)

- Speeds (world seconds per real second): Pause 0 · 1x · 10x · 60x · 10 min/s · 1 h/s · 1 day/s ·
  10 days/s · Max. Above ~1 h/s agents are drawn as activity and density rather than individuals.
- Each frame: `target = now + realDt x speed` (clamped to a backlog cap); `AdvanceTo(target, budget)`.
  If the budget runs out, the host shows the achieved speed; events are never skipped.
- Budgets: watch 4-8 ms/frame; catch-up 30-60 ms/frame (the GC runs between frames on the Web).

## 7. Offline progression (summary; full design in `12_SAVE_OFFLINE_PROGRESS.md`)

On load: elapsed real time -> world time to simulate (by the player's offline mode, capped) -> catch-up
with the same `AdvanceTo` (no rendering of agents, a progress screen fed by the history ledger) -> the
present. A catch-up of N world years equals watching N years (same events, same hash), because the only
difference is how the host slices `AdvanceTo`.

## 8. Correctness invariants (tests and debug audits)

1. Slicing invariance (state hash equal for 1 call vs many).
2. Save/load equivalence at arbitrary event boundaries.
3. No event in the past; no event targeting a dead entity is dispatched non-stale.
4. Every live agent has exactly one live step event (or is in a process that owns it).
5. Conservation (food, wood, water, carbon) per system within tolerance.
6. Travel index consistent with activities (rebuild equals incremental).
7. Cross-runtime hash equality (dotnet / Mono / Web).
