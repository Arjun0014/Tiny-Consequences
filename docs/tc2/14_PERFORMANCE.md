# TC2-14 — Performance

Performance is a design constraint: deep believable agents first, with substantial headroom; never
optimise by faking visible agency.

## 1. Targets (Web release build on the dev laptop, single-threaded unless noted)

| Measure | Target | Why |
|---|---|---|
| Watch at 1x-60x, whole world simulated | sim <= 4 ms/frame at 60 fps | room for rendering |
| Watch at 1 day/s | sim <= 8 ms/frame | a year in 48 s with people drawn as activity |
| **Catch-up (Living)**, 1,000 people, ACTIVITY | >= 1.5 world years / real second | a day away (~96 years) loads in about a minute |
| Catch-up, 5,000 people | >= 0.4 years/s | larger civilisations stay playable |
| Deep time (COARSE, later) | >= 20 years/s | Ancient / Deep time modes |
| Save (2,000 people, centuries of history) | < 5 MB, < 200 ms serialize | autosave every minute |
| Load + rebuild | < 1 s | |
| Heap | <= 1 GB (wasm32) | browser safety |
| Rendering | 60 fps with the planet, clouds, a town and its people at close zoom on a mid-range GPU | |

Derived budget: 1.5 years/s with 1,000 people is ~48,000 person-days per 0.67 s, i.e. about **14 us
per person-day** for everything (all systems included): with ~6-10 decisions a day, ~1-2 us per decision
and step. Planet systems must stay below ~25% of that.

## 2. Methodology

- **Deterministic counters first**: events processed per kind, decisions, candidates scored, path queries,
  field cells stepped - stable across machine load (TC1 used VM instruction counts for the same reason).
- **Wall time second**, as the lower envelope of several runs (TC1: the laptop is shared).
- **Three hosts**: dotnet headless (RyuJIT; fast iteration), Unity editor (Mono; sanity), Web release
  build in Chromium with rendering off for sim cost (headless SwiftShader rasterisation is not a GPU
  measure; GPU work is measured with ANGLE on the real GPU, as TC1 learned).
- **Grown worlds**: profile at year 0, 100 and 1,000 (TC1: a day-0 profile hid herd growth).
- Every benchmark result is recorded in `PROGRESS.md` with build, host, world, seed and settings.

## 3. Benchmarks (harness: `TC2.Headless bench`, and the Web build's `?bench=` mode)

| Name | Measures |
|---|---|
| `queue` | push/pop throughput of the event queue at 10K-1M pending events |
| `rng`, `dmath` | keyed draws and transcendental throughput |
| `planet` | one year of planet systems at N32/N64 without people |
| `poc-1k`, `poc-5k`, `poc-20k` | the proof-of-concept world: years/s, events/s, us per person-day |
| `save` | serialize/compress/deserialize sizes and times |
| `history` | ledger append, propagation, compaction at 1,000 years |

## 4. Known risks and levers

- Decision cost per person (candidate generators): cache settlement-level work boards; early-out by
  stage and state; score only reachable candidates.
- Paths: settlement graphs with cached distances (versioned); inter-settlement routes cached per pair
  and invalidated by terrain/road version; never A* per routine trip.
- Pairwise social structures: sparse (only pairs that met), bounded per entity.
- Planet systems: land-only loops; multi-rate; Burst + SIMD + threads (when cross-origin isolated).
- GC on the Web: allocation-free hot paths; pools; struct events.

## 5. Session 3 notes (D2-082, F8)

- **View work per frame scales with instances, not with what changed**: gathering the ground cover's and the trees' instances each
  frame cost the near views ~7-8 ms in the browser; gather on change (patches, content, camera) and redraw the gathered lists.
- **Measure settled frames**: a view's first seconds compile shaders and build patches; `webperf` reads at 5, 15 and 30 s - quote the
  30 s value.
- **A shared machine**: Web builds with `--jobs 3` (IL2CPP, Bee, emscripten, binaryen capped; ~15 min); heavy jobs one at a time at
  below-normal priority, launched only on a quiet machine (`scratchpad/quiet.ps1`).
- **DEEP (F8)**: a step's cost grows with what happens in it (trips, births, deaths, news are still made at their rate); only the
  per-day polling shrinks. At saturation villages became 2.3x cheaper at K = 12 and the planet (~157 ms a world year) became a quarter
  of the time: both must shrink for Ancient and Deep time.

## 6. Population tiers (D2-089)

Depth before numbers: build quality at tier A (~100-500 people), then B (~1,000), C (~3,000), then stress upward. Per tier measure world
years a second, cost per person-day, relationships, economy, military, structures, history size, rendering, memory and the Web build.
Structures never get a per-frame or per-day update of their own (decay is read from time; upkeep and damage are works and events); a
force marches as scheduled movement; new per-day work is indexed per settlement and profiled at 30 settlements.
