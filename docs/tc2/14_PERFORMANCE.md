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
- **Know which side bounds the frame (D2-109)**: the bridge posts `cpuMs` (the main thread from the first Update to the end of
  rendering) and `draws` (instanced draws); `webperf` prints both. CPU near the frame's interval means submission bounds it - on WebGPU
  each draw (and each shadow cascade it falls in) costs main-thread time. Instanced views draw per-instance colours in batches of
  1,023 with their own bounds (34-37 draws a frame in the near views), never one draw per colour group with the world as bounds.
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

## 7. Great worlds in the Web build (session 24, D2-169)

Measured with `tc2/scripts/realmsperf.mjs` (a headless save put into the game's store as saved a moment ago, paused; frames counted by
requestAnimationFrame; the wasm heap read from `Module.HEAPU32` - the framework does not export `HEAPU8`, so earlier heap traces read -1) on seed 2's
worlds of the integrated rules (sim 90), on a machine loaded by other work:

| World | Saved / state | Heap loaded | With the view | Through autosaves | Frame rate (region / village) |
|---|---|---|---|---|---|
| year 300, 5,991 people, 28 villages | 8.7 / 35.5 MB | 513 MB | 513 MB | 513 MB, flat | 8-11 / 5-7 fps |
| year 900, 9,532 people, 32 villages | 20.9 / 79.0 MB | 858 MB | 955 MB | 1,053 MB after the first, then flat | 8 / 6 fps |

- **The autosave's buffers** (D2-169): each save of the year-900 world made ~460 MB of short-lived buffers and the heap - which only grows - reached
  the 2 GB ceiling after three autosaves (a minute apart; 20 s at the fastest speeds, 30 s in a catch-up). Saves now go into buffers the host keeps,
  sized at each load for the world the save holds: the heap is flat through autosaves.
- **Open**: a 9,500-person world holds ~1.05 GB (past the 1 GB budget; ~3.6x its headless managed heap of 238 MB) and draws a region view at ~8 fps -
  the same machine's Web test of a 145-person world draws the region at ~30 fps (51-62 on a quiet machine, `s23web2`-`s23web4`), and a 569-person world
  after a day away 19-24 fps at the village view on a quiet machine. Great worlds want a memory and frame pass of their own (what holds the heap: the
  world's state, the history ledger, the views' meshes; what bounds the frame: draw submission, as D2-109 found, against the people and buildings drawn).

**The frame pass (session 25, fourth context; D2-182 draft `tc2/out/tools/s25/frame/d2182.md`)**, measured by paired layer counts on seed 2's year-1,000 world
(`_ac3`: 12,698 people in 62 places; sim 98) in the Web build:

| Frame (ms) | Region view | Village view |
|---|---|---|
| before (`s25web3`) | ~155 (6.4 fps) | ~225 (4.4 fps) |
| layer costs then | buildings x0.53, people x0.75, shadows x0.73, terrain x0.91 | buildings x0.54, people x0.75, shadows x0.63, terrain x0.90 |
| people beyond the limb or outside the view unposed | x1.22 | x1.57 |
| villages beyond the limb unbuilt (2.5 km margin; rebuilt as the camera moves) | x1.21 | x1.51 |
| both (`s25web4`) | ~108 (9.3 fps) | ~126 (7.9 fps) |
| layer costs now | buildings x0.44 (~60 ms), shadows x0.69, people x0.87 | buildings x0.46 (~67 ms), shadows x0.53, people x0.92 |

- Next: villages in front of the limb but outside the view (the buildings' works reach 2.5 km, their homes ~0.4 km), the buildings' draws by colour group
  (each group a draw a camera and a shadow cascade: D2-109), shadows cast only near. The memory pass: a save streamed both ways (no whole state kept or
  inflated at once; D2-182), its effect on the heap measured by age (`0_x` before, `0_z` after).
