# TC2 — Audit of Tiny Consequences 1.x (the Defold prototype)

Written 5 Oct 2026 at the start of TC2, from a full read of `docs/` (vision, world model, causality,
architecture, the v2 world docs, the v3 docs 00-86, 226 decisions, 25 session handoffs, playtests) and a
survey of the code (`sim/` 51K lines of Lua, of which `sim/agents/` 29K; `game/` 19K; tests 10K).
TC1 is research material for TC2, not its specification.

## 1. What TC1 was, in three generations

| Gen | Period | Model | Why it was replaced |
|---|---|---|---|
| v1 "planet" | S1-S4 | 642-1,442 icosphere cells, weekly tick; fields (temp, moisture, soil, forest, grass, fish, oil, grazers, predators...) coupled by shared state; villages as monthly aggregates | people were numbers; nothing to watch close in |
| v2 "society" | S4-S8 | hourly clock; persons/households/villages; **positions as a pure function of task and hour** (`schedule.where`); a weekly central labour allocator; up to 4,800 people | Arjun: "people move as scheduled groups... animated statistics". Work was *accounted*, not done |
| v3 "agents" | S9-S25 | persistent individuals with continuous analytic motion on 128x128 tile maps per settlement, A*, a job board + individual utility choice, every effect done by an action (a felled tree, a carried sheaf) | the shipped jam game. Deep and honest, but **cannot do deep time** (below) |

## 2. What worked (carry the idea, not the code)

1. **Shared state, local rules** (02 §0): systems never call each other; they read and write fields.
   Chains (cull wolves -> deer boom -> saplings eaten -> erosion -> silt -> fish fall) emerged without scripts.
2. **Blame tags with a dominance margin** (03 §3): every causal field carries the event id dominating its
   latest change (a term must beat the runner-up by 1.5x to take the tag; tags expire after idle time).
   Detectors turn threshold crossings into events whose parent is the tag, and re-tag the field (chains
   longer than two links). Global fields kept a ledger of top contributors.
3. **The visible action is the simulation** (D-078): a woodcutter's trip *is* the wood entering the store.
   Players and the director could tell the difference immediately.
4. **Analytic motion + strict (time, id) event order**: positions are functions of time; the world is
   identical whether advanced in one call or a thousand (a test). No speed teleports anyone.
5. **Primitive properties instead of per-hazard code** (`docs/v3/50_PRIMITIVES.md`): things have places,
   owners and condition; dangers are physical things at places; people *notice* through senses, *weigh*
   an urgent choice with dependants first, *can be hurt*, *remember specific deeds* that travel only with
   people. Theft, feud, rescue, evacuation, orphan care, rebuilding and migration were then the existing
   economy and family reading those properties. The primitive test (§0 there) is worth keeping verbatim.
6. **Deeds as the social causal record**: `{by, to, what, worth, harm/help, answers, cause, event}` known
   per village with *how* it was learned; grievance, gratitude, reputation and fear are *read* from known
   deeds, never stored as scores.
7. **Observers never write state**: the news (`sim/news.lua`), the chain index (`sim/chain.lua`) and the
   audio read the world and keep their own runtime; stories only from deeds, events and live state.
8. **Three questions** for the player: WHAT IS HAPPENING (news), WHY (causal links to NATURE / PEOPLE /
   YOU), WHAT DID I CAUSE (forward chain of a cast). TC2 adds the fourth: WHAT HAPPENED (the History Book).
9. **Premises, not scripted outcomes, for tests**: a premise is a state the world could reach (a failed
   harvest, an empty store, a crowded village); the test asserts the *mechanism* (D-204: tests asserting
   one emergent outcome in a window broke whenever the world's course changed).
10. **Long-run audits**: 8-year untouched runs with every memory, death and account written out
    (`v3_chronicle`), `v3_follow` for one person's whole timeline. Most serious bugs were found this way,
    *never* by unit tests.
11. **Cross-runtime determinism was achieved** (D-110): LuaJIT (tests) and Lua 5.1 in the browser produced
    identical world hashes, after replacing every transcendental with `sim/dmath.lua`, banning `^`, and
    formatting kept text deterministically. This made headless tests trustworthy for the shipped game.
12. **Instruction counts** (`luajit -joff`) as the performance metric: stable on a shared laptop, unlike
    wall time. The browser was measured with rendering off (the headless CPU rasteriser lied).

## 3. What felt fake, or was fake

- v2 schedules and accounting (above): the root cause of the v3 reset.
- God powers at first wrote outcomes or did little; the acceptance audit (S24) rebuilt each as physics
  writing state (a meteor = crater + blast overpressure + thermal flash + shaking by distance).
- **Two scales disagreeing**: a planet cell burning for a week drawn over a village map whose fire was out
  in hours; flames over standing trees. One phenomenon must have one owner of truth at each place.
- Herds that started and stopped together until each animal decided at its own time; synchrony was then
  *measured* (velocity correlation, mass starts/stops) and regression-tested.
- Things standing in flames for an hour; fire that never ended; rain that did not quench it.

## 4. The bugs that matter for TC2's architecture

| Bug (decision) | Lesson -> TC2 rule |
|---|---|
| Everyone born in a running world was never scheduled; 37 of 168 alive after 8 years had never taken a step (D-176) | **Creation always schedules.** A creation API that cannot forget the queue; an invariant audit "every live agent has exactly one pending decision event" |
| A rebuilt queue scheduled in the past; dogs never re-queued; caches reading mutable state not reset (D-176) | **Serialize the queue as data. Never rebuild it.** Caches are derived, versioned and invalidated by state version, never by time |
| A restored world parted from the live one after ~6 h (a claim count) (D-176); players could never save v3 worlds | **Save/load equivalence is a day-one test**, and the only way state may exist is in serialized structures |
| A reused work kind (`lift`) starved villages after year 5 (D-164); `tend` meant two things | **Typed enums, one namespace, compile-time** (C# enums do this for free) |
| `think.decide` hit Lua's 60-upvalue limit; `think.lua` grew to 6,516 lines | Decisions as **composable candidate generators** with a shared scoring frame, not one function |
| A pit sealed a house; children carried through walls (D-184) | **Layout invariants audited** after every placement (every door reachable) |
| A nursing mother walked to kin while her newborn starved (D-164, D-184) | **Care obligations are constraints** checked by every journey planner, not special cases |
| Toddlers never recovered health: the body code ran only for grown-ups (D-184) | One body model for all ages; per-stage parameters, never per-stage code paths |
| Downpours dried the soil (hydrology subtracted runoff twice) (D-179) | Conservation audits (water, carbon, food, wood) as invariant tests |
| PowerShell zip with backslash paths broke itch (D-226) | Packaging verified by listing the archive's raw names |
| LuaJIT accepted what Lua 5.1 rejected (`goto`, `^` semantics) | Compile the core with the most restrictive toolchain it must run on (C# 9 / netstandard2.1) |

## 5. Performance: why TC1 cannot do deep time

- v3 in the browser: **~40 ms of simulation per game hour** for ~150-170 people + ~500 animals
  (a 12-day season in 10-12 s, a 48-day year in ~45 s, a century in ~75 min). Offline years and
  millennia were impossible by construction.
- Profile (LuaJIT, day 0): A* 27%, think 13.5%, stock (herd neighbour scan, O(herd) per decision) 12.6%,
  local map 12.5%, wild 10%, event queue 8%. At year 8 the herd scan reached 38%: costs grew with herds.
- Every routine footstep was authoritative simulation, everywhere, always; nothing was cheaper when
  unobserved. The 128x128 tile map and A* per trip were the price.
- v2's 4,800-person experiment: per-person cost doubled from 2,400 to 4,600 people because relations,
  partner lists and raids were per village *pair* (D-077). Pairwise structures must stay sparse.
- The interpreter (Lua 5.1 in wasm) was ~7.4 ns per VM instruction: the language was the ceiling.

## 6. The calendar and scale TC1 chose (and TC2 keeps the spirit of)

- One planet step per game day; a year of 48 days (4 seasons of 12); people age in real-feeling years
  (adult 16, elder 55); a crop grows in ~3 weeks of days; a child grows up in ~672 days. This rhythm read
  well in play: seasons are watchable and years still pass.
- Speeds: watch speeds 1/4/12 game-minutes per second; faster was an explicit *skip* with people hidden.
- Population: 50-200 meaningful people was the v3 target; ~170 at year 8.

## 7. What TC2 discards

Lua, Defold, the weekly-tick planet of v1 (replaced by multi-rate systems), v2 schedules-as-truth, tile A*
for every trip, the monolithic decision function, the 1,442-cell icosphere (too coarse for terrain change),
static plates, "one planet step = one day" for everything, the 48-tick ring buffer of events, saves that
were snapshots at year boundaries plus replayed commands (TC2 saves the exact state).

## 8. What TC2 must keep (non-negotiable lessons)

1. No scripted consequences; mechanisms read shared state.
2. The visible action is the simulation; nothing appears that nobody built, carried or learned.
3. Every major change traceable to NATURE, PEOPLE or YOU through real records.
4. Observers (news, chronicle, audio, history views) never write simulation state.
5. Determinism: keyed randomness, ordered iteration, no wall-clock, deterministic math.
6. Primitive properties reused by many systems; the primitive test before adding state.
7. Long-run audits, follow tools and premises are first-class development tools.
8. Measure honestly: deterministic cost metrics, release builds, rendering off for sim cost.
