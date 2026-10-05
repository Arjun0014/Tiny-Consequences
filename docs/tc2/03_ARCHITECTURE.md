# TC2-03 — Architecture

Decisions behind this document: D2-002 (no ECS; pure C# core + Burst kernels + Unity host), D2-003
(determinism), D2-006 (resolution layers), D2-012 (testing). Evidence: `02_RESEARCH.md` §2.

## 1. Shape of the system

```
                 player input / test bridge / offline catch-up
                          │ commands (timestamped, logged, applied as events)
                          ▼
┌──────────────────────────── TC2.Core (pure C# 9, no UnityEngine) ─────────────────────────────┐
│ World state (tables of structs, field arrays) · Scheduler (one ordered event queue)            │
│ Planet systems · Ecology · Agents (decisions + activities) · Places · Society · Knowledge       │
│ History ledger (events, causes, entities) · Persistence · Diagnostics (hash, audits, metrics)  │
│ deterministic: integer time, keyed RNG, DMath, ordered iteration                               │
└──────────────┬──────────────────────────────────────────────┬──────────────────────────────────┘
               │ read-only queries (positions at t, visible sets,│ kernel interfaces (fields)
               │ history views) after AdvanceTo()                ▼
               │                                   TC2.Kernels (Burst jobs, Unity only; optional)
               ▼
┌──────────────────────────── TC2.Game (Unity runtime) ─────────────────────────────────────────┐
│ SimHost (time-slicing, speeds, catch-up) · Views (planet, settlements, agents, fx)             │
│ Presentation elaboration (footpaths, animation) · Camera · UI Toolkit (HUD, inspector,         │
│ news, History Book) · Platform (save stores, JS bridge, Wavedash)                              │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
        TC2.Headless (dotnet console; compiles Core + Core tests): tests, runs, benchmarks, inspection
```

Rules:
1. **Core never references UnityEngine** (asmdef `noEngineReferences`, and it must also compile in the
   dotnet headless project against netstandard2.1 with C# 9).
2. **The view never writes simulation state.** Player actions are commands with a world time; they are
   applied as events and logged.
3. **All state that history depends on is in serializable structures**: tables, arrays, the event queue.
   No closures, coroutines, delegates, lambdas or iterator state may hold simulation state. Derived
   indexes are rebuilt from state, never serialized and never "remembered".
4. **No static mutable state in Core** (domain reload is off by default in 6.6; tests run many worlds).
5. **One owner of truth per phenomenon per place** (TC1's two-scales lesson): e.g. a fire on a settlement's
   ground is owned by the fire process; the planet cell reads it.

## 2. Why not Entities (ECS), and what instead (D2-002)

- Entities Graphics and BatchRendererGroup are not supported on the Web in 6.6 (local docs), so ECS gives
  no rendering path on our target.
- The core workload is **event-driven and strictly ordered** (an agent's activity ends at 14:03:27 and
  must be processed before anything later); ECS's strength is "update all X every frame" over chunks.
  Most agents are asleep between events; the cost is per event, not per entity per frame.
- Saves must be versioned and stable across code changes; ECS world serialization is tied to type layout.
- Determinism needs total control over iteration order (chunk order changes with structural changes).
- Testing and iteration speed: plain C# compiles and runs headless in seconds (dotnet), no editor needed.

What we use instead: **data-oriented plain C#** (arrays of structs addressed by stable ids, side tables
for variable-size data, explicit pools), and **Burst + Jobs + Collections + Mathematics** for bulk numeric
kernels (planet fields, instance buffers, culling) where profiling shows benefit, behind interfaces whose
managed reference implementations live in Core (equivalence-tested).

## 3. Assemblies and layout

```
unity/TinyConsequences2/                Unity 6.6 project (URP)
  Assets/TC2/Core/      TC2.Core        simulation (namespaces below)
  Assets/TC2/Kernels/   TC2.Kernels     Burst jobs implementing Core kernel interfaces (when needed)
  Assets/TC2/Game/      TC2.Game        host, views, UI, platform (MonoBehaviours, shaders, UXML/USS)
  Assets/TC2/Editor/    TC2.Editor      project setup, builds, batch tools, procedural asset generation
  Assets/TC2/Tests/Core/   TC2.Tests.Core   NUnit tests of Core (also compiled by tc2/headless)
  Assets/TC2/Tests/Game/   TC2.Tests.Game   Unity-specific tests
tc2/headless/   TC2.Headless.csproj     net8.0 exe; compiles Core + Core tests from the Unity folder
tc2/scripts/    *.mjs                   Unity batch wrappers, Web build, COOP/COEP server, Playwright
tc2/out/        (ignored)               logs, test results, benchmark output, maps, screenshots
docs/tc2/                               this documentation
```

Core namespaces: `Foundation` (ids, time, calendar, RNG, DMath, hashing, small containers),
`Scheduling` (event queue, scheduler, periodic systems), `Simulation` (World root, config, laws,
commands), `Planet` (grid, fields, geology, climate, hydrology, vegetation, fire), `Ecology` (species,
populations, herds), `Agents` (persons, households, body, decisions, activities, travel), `Places`
(settlements, layout, buildings, stores, roads), `Society` (relations, deeds, polities, cultures),
`Knowledge`, `History` (ledger, causes, importance, entity registry, sifting views), `Persistence`
(binary format, compression, save/load), `Diagnostics` (state hash, audits, metrics, counters),
`Worldgen`, `Views` (read-only query APIs for presentation).

## 4. Data model conventions

- **Stable ids** (D2-008): every entity kind has a monotonic 32-bit counter in `IdAllocator`, never reused
  (0 = none). Persons, households, settlements, buildings, polities, cultures, species, pathogens,
  techniques, places: `int`. History events: `long`. An `EntityRef` = (kind byte, id int).
- **Tables**: `Table<T> where T : struct` - a dense array of rows plus an id -> row index map, swap-remove on
  delete *only for live tables whose iteration order is not semantic*; iteration in sim logic is always
  in id order (a sorted id list maintained on insert, since ids are monotonic: append keeps it sorted).
- **Side tables** for variable-size data (relationships, knowledge, inventory, memories): pooled
  fixed-capacity slices or chunked lists keyed by owner id.
- **Fields**: `float[]`/`double[]` per quantity indexed by cell; neighbour reads use the previous step's
  buffer (double buffering) so update order cannot leak into results.
- **Arithmetic**: `double` in all sim logic (storage may be `float`: cast when stored); transcendentals only via `DMath`. No
  operation may be typed `float` - Mono carries float arithmetic at double precision where CoreCLR and IL2CPP round to single;
  `tc2/tools/floatcheck` fails the headless tests on any (D2-057).
- **Allocation discipline**: no allocation per event in hot paths (pools, preallocated scratch); the Web GC
  only runs between frames.

## 5. The world root and the main loop

`World` owns every table, field and the `Scheduler`. The only way time passes:

```
World.AdvanceTo(long target, ref Budget budget):
  while queue not empty and queue.PeekTime() <= target:
     if budget exhausted: return            // state is consistent between events
     ev = queue.Pop(); Now = ev.Time
     dispatch(ev)                            // systems, agent steps, processes, commands
  Now = target
```

Properties (tested): **slicing invariance** (any sequence of `AdvanceTo` calls reaching T gives the same
state hash), **save/load equivalence** (save at any event boundary, load, continue -> identical to never
stopping), **cross-runtime equality** (dotnet, Unity Mono, Web IL2CPP).

## 6. Resolution layers (D2-006) — see `04_TIME_AND_SCHEDULER.md`

| Layer | Authoritative? | Where | Time grain | Examples |
|---|---|---|---|---|
| PROCESS | yes | only where a process is active | seconds-minutes | fire in a street, flood water, a fight, a rescue |
| ACTIVITY | yes | everyone, always | event-driven (s) | decisions; walk to field 3, hoe 2 h, carry sheaves; a trip to the next village |
| COARSE | yes (deep time only) | households in COARSE mode | seasons | deep-time catch-up beyond the ACTIVITY budget (later) |
| ELABORATION | **no** | observed agents only | frames | footsteps along real streets, gestures, flocking of a herd's members |

Watching never changes history: elaboration is constrained by the authoritative activity (same
endpoints, same times, same effects) and is regenerated from state when the camera arrives ("alibi").

## 7. Planet and places

- Planet: equiangular cubed sphere; N64 surface cells (~245 m), N32 climate; render textures derived.
  Positions are unit vectors (double) + altitude; settlements carry a local tangent frame (metres) for
  their layouts.
- Systems are periodic events with rates by kind (`05_PLANET_SIMULATION.md`); rates are part of the world's
  resolution policy, never of the host's frame rate.

## 8. History

An append-only causal ledger written by systems at the moment things happen (`11_HISTORY_AND_CAUSALITY.md`).
Observers (news, chronicle, chain index, era detection) read it incrementally and keep their own runtime
state; they never write World state.

## 9. Host (Unity)

- `SimHost`: owns the World; each frame converts real dt x speed into a target time, advances with a
  per-frame budget (ms, measured in the Game layer), and never skips events: if the budget runs out the
  displayed speed falls ("the world is busy").
- Catch-up mode: same `AdvanceTo` with a large per-frame budget and a progress UI fed by the ledger.
- Views read Core through `Views` queries; agent positions are evaluated at the host's fractional time.
- Platform: `ISaveStore` (file store in editor/desktop; IndexedDB store via a `.jslib` on the Web; Wavedash
  remote storage later), a JS bridge for Playwright (`window.__TC2`), visibility-change autosave.

## 10. Build and run

- Unity batchmode for compile/tests/builds (`tc2/scripts/unity.mjs`); one Unity process at a time.
- `TC2.Headless` for fast tests, long runs and benchmarks (`tc2/scripts/headless.mjs`).
- Web build: URP, WebGPU first with WebGL2 fallback, Wasm32 with WebAssembly 2023, Brotli with
  decompression fallback, threads optional (served with COOP/COEP locally and on itch when enabled).
