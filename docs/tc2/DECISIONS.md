# TC2 Decisions

Format: **D2-NNN — date — title** / Context / Alternatives considered / Decision / Reason and evidence /
Consequences / What could invalidate it. Settled decisions are not reopened without new evidence.
Legacy TC1 decisions are in `docs/DECISIONS.md` (D-001 ... D-226) and are research material only.

---

## D2-001 — 2026-10-05 — TC2 is a new Unity project beside the legacy game

**Context.** TC2 is a new generation, not a port. The Defold project (TC1) must stay intact and buildable.
**Alternatives.** A separate repository; a branch; a new folder in this repository.
**Decision.** A new folder in this repository: `unity/TinyConsequences2/` (the Unity 6.6 project),
`tc2/` (headless runner and scripts), `docs/tc2/` (documentation). Legacy files are not modified except
`.gitignore` (Unity and TC2 output ignored).
**Reason.** One history, the legacy research at hand, nothing of TC1 damaged.
**Consequences.** Commit prefix `[tc2]`. Unity's Library/Temp/Logs/UserSettings and builds are ignored.
**Invalidation.** Repository size or tooling conflicts that make a split cleaner.

## D2-002 — 2026-10-05 — No Entities (ECS): a pure C# core, Burst kernels where measured, Unity as host

**Context.** Unity 6.6 ships Entities 6.6 as a core package; the prompt asks for a deliberate choice.
**Alternatives.** (a) Entities for simulation and Entities Graphics for rendering; (b) Entities for
simulation, custom rendering; (c) plain data-oriented C# core + Burst/Jobs for bulk kernels; (d) all
MonoBehaviours.
**Decision.** (c). `TC2.Core` is pure C# 9 (no UnityEngine; also compiled by the dotnet headless
project); bulk field kernels get Burst job implementations behind Core interfaces when profiling shows
benefit; rendering through our own instanced drawing.
**Reason and evidence.** Entities Graphics: Web "Not supported" (built-in package docs); BRG has no Web
platform (Manual/batch-renderer-group-how); managed threads do not exist on the Web, but Burst jobs do run
in parallel there (Manual/web-multithreading-burst), so Burst is the only parallelism we can get and it
does not need ECS; our workload is ordered discrete events (TC1's v3: cost per event, most agents asleep
between decisions), not per-frame chunk iteration; saves must survive code changes (ECS serialization is
layout-bound); determinism needs control of iteration order; dotnet runs of the core take seconds versus
a minute per Unity batch start.
**Consequences.** No automatic parallelism for agent logic (single-threaded, must be lean); Burst kernels
need a managed twin and an equivalence test.
**Invalidation.** Entities Graphics on Web; a profile showing per-agent logic dominated by data that ECS
would lay out better and that can tolerate ECS ordering.

## D2-003 — 2026-10-05 — Determinism: bit-identical across runtimes as the target

**Context.** TC1 achieved browser = LuaJIT equality and relied on it. TC2 runs in dotnet (tests), the Unity
editor (Mono), and the Web (IL2CPP -> wasm).
**Alternatives.** (a) No guarantees; (b) same-binary determinism only; (c) cross-runtime bit equality.
**Decision.** Target (c), with (b) as the fallback if a runtime proves impossible (documented if so).
Rules: integer time; counter-based keyed RNG (SplitMix64 mixing of seed, stream, entity, per-entity draw
counter); `double` for sim arithmetic, `float` only for storage; transcendentals only via `DMath` (built
from + - * / sqrt floor); no iteration over hash containers in sim logic; total-order comparers for every
sort; no `GetHashCode()`, `System.Random`, wall clock or TPL in Core; range-checked float->int casts; Burst
kernels in `FloatMode.Strict` (or Deterministic) with the managed operation order.
**Reason.** Headless tests then validate exactly the world players run; bug reports reproduce from a save
anywhere; a future server could simulate absences.
**Consequences.** A lint script; cross-runtime hash tests (dotnet vs editor vs Web).
**Invalidation.** A measured runtime difference we cannot remove at acceptable cost.

## D2-004 — 2026-10-05 — Time: integer seconds; 24-hour day; 48-day year

**Context.** One clock must serve seconds-long fights and millennia.
**Alternatives.** Float hours (TC1); integer minutes; integer seconds. Year lengths 24-112 days.
**Decision.** `long` seconds; 24 h days; 48-day years of four 12-day seasons; human-like ages in years.
**Reason.** Integer time orders events exactly and serializes trivially; seconds resolve fights and
footsteps; 48 days was TC1's calibrated, watchable rhythm and keeps ACTIVITY cost per year bounded.
**Consequences.** `WorldLaws` holds the calendar; cost per world year is proportional to 48 days.
**Invalidation.** Playtests showing seasons too short to read, or catch-up cost dominated by days.

## D2-005 — 2026-10-05 — One ordered event queue for everything

**Context.** Agents, processes, planet systems and commands must interleave deterministically.
**Alternatives.** Fixed ticks per system (TC1 v1); separate queues per subsystem; one queue.
**Decision.** One future event list ordered by `(time, phase, seq)`; periodic systems are recurring
events; lazy cancellation by version stamps; a 4-ary heap with far-future buckets; the queue is
serialized, never rebuilt; creation APIs schedule.
**Reason.** Slicing invariance and save/load equivalence follow directly; TC1's frozen children and
rebuilt-in-the-past queues are impossible by construction.
**Invalidation.** Measured queue cost dominating (then a calendar queue behind the same interface).

## D2-006 — 2026-10-05 — Resolution layers; watching never changes history

**Context.** Deep time requires that unobserved life is cheap, yet TC1's lesson is that visible agency
must be real, and the prompt requires journeys to be interruptible by hazards they meet.
**Alternatives.** (a) Full micro simulation everywhere (TC1 v3: ~45 s per year in the browser);
(b) observer-dependent detail that writes back (an observer effect; determinism depends on the camera);
(c) authoritative ACTIVITY level for everyone + sparse authoritative PROCESSES + presentation-only
ELABORATION; COARSE household-season integration for deep time.
**Decision.** (c). Decisions, destinations, timings and effects are simulated for everyone always;
footsteps and gestures are elaborated only when watched, constrained to the activity; fires, floods and
fights are processes simulated at fine resolution whether or not watched; COARSE is designed now and built
when deep-time catch-up needs it, calibrated against ACTIVITY.
**Reason.** "Alibi generation" (Sunshine-Hill & Badler 2010) without the observer effect; journeys are
analytic legs in a space-time index, so hazards find the travellers they meet.
**Consequences.** Production functions at the activity level must be honest about time (walk times
from the settlement graph, work durations from skill and conditions).
**Invalidation.** Elaboration that cannot be made consistent with activity timings (would require
moving more detail into the authoritative level).

## D2-007 — 2026-10-05 — The planet: an equiangular cubed sphere; radius 10 km; plates that move

**Context.** TC1's 1,442-cell icosphere with static plates cannot show changing coastlines or ranges.
**Alternatives.** Icosphere/Goldberg grids; cubed sphere; HEALPix.
**Decision.** Equiangular cubed sphere: N32 climate (6,144 cells), N64 surface (24,576 cells, ~245 m at
R = 10 km), render textures at N256+; plates as rotations about Euler poles with semi-Lagrangian crust
advection; positions as unit vectors + altitude; settlements carry local tangent frames.
**Reason.** Texture-friendly faces (GPU and rendering), quadtree LOD, simple neighbour indexing, area
variation ~1.3 (equiangular), used by climate models; N64 resolves villages and fields; R = 10 km gives a
carrying capacity of ~20,000 pre-industrial people.
**Invalidation.** Corner artefacts in diffusion or rendering we cannot hide; scale playtests.

## D2-008 — 2026-10-05 — History: a causal ledger with tiers, roots, importance and compaction

**Context.** History must be first-class: news, why, your actions and the History Book over one truth.
**Alternatives.** Text logs; per-system event lists; a graph database; TC1's ring buffer.
**Decision.** `11_HISTORY_AND_CAUSALITY.md`: stable ids per kind; an append-only ledger of structs with up
to two direct causes (+ overflow), cause relations, cached roots (NATURE / PEOPLE / YOU), tiers 0-4,
importance propagated up the ancestry, yearly compaction that rewires causes to kept ancestors; observers
interpret.
**Reason.** Story sifting (Ryan; Kreminski's Felt/Winnow) shows interpretation belongs after the fact; TC1
showed causes must be real links written by the acting system.
**Invalidation.** Ledger growth beyond budget in long runs (then stronger aggregation).

## D2-009 — 2026-10-05 — Saves: exact state, sectioned binary, our own IndexedDB store

**Context.** TC1 never saved v3 worlds; restored worlds diverged.
**Alternatives.** Snapshot at year boundaries + replay (TC1 v1); JSON; Unity serialization; exact binary.
**Decision.** `12_SAVE_OFFLINE_PROGRESS.md`: exact state at an event boundary, sectioned and versioned,
compressed, hash-verified; file store and a `.jslib` IndexedDB store with fixed names; two slots.
**Reason.** Equivalence becomes testable; `persistentDataPath` on the Web depends on the page URL (itch
changes it per upload); Safari blocks IndexedDB in iframes (fallback needed).
**Invalidation.** Save sizes that exceed browser limits (then delta saves).

## D2-010 — 2026-10-05 — Offline progression: modes, caps, and catch-up as the real simulation

**Decision.** Modes Paused / Gentle (1 yr/h) / Living (4 yr/h, default) / Ancient (25 yr/h) / Deep time
(200 yr/h) with caps; catch-up runs `AdvanceTo` in frame slices with history streamed; the target is stored
so an interrupted catch-up resumes; ACTIVITY level now, COARSE for Ancient and Deep time later.
**Reason.** The prompt's example (a day away = ~92 years); the same code path guarantees "same truth".
**Invalidation.** Measured catch-up throughput too low for Living (then COARSE sooner).

## D2-011 — 2026-10-05 — Rendering: URP, WebGPU first with WebGL2 fallback, instancing without BRG

**Context.** Visual quality and performance matter more than load time; BRG and Entities Graphics are
unavailable on the Web.
**Decision.** URP; Graphics API list [WebGPU, WebGL2]; instanced drawing with `Graphics.RenderMeshInstanced`
/ `RenderMeshPrimitives` and instance data in buffers (WebGPU) or textures (WebGL2); the planet as
cube-sphere patches displaced from textures; vegetation and people as instances; agents animated by
vertex animation textures; compute-only features optional until WebGL2 is dropped.
**Reason.** WebGPU left experimental in 6.6 and is on by default in all major desktop browsers (2026).
**Invalidation.** WebGPU backend bugs we cannot work around; WebGL2 fallback too costly to maintain.

## D2-012 — 2026-10-05 — Testing and tooling: dotnet headless + Unity batchmode + Playwright

**Decision.** `tc2/headless` (net8.0 exe compiling Core and Core tests against netstandard2.1 rules, using
the editor's bundled .NET SDK) for unit/invariant/determinism/long-run tests and benchmarks; Unity Test
Framework in batchmode for Unity-specific tests; Playwright (installed) for Web smoke, cross-runtime hash
and in-browser benchmarks; node scripts in `tc2/scripts`. One heavy process at a time (11 GB RAM).
**Reason.** Seconds per test cycle instead of minutes; the same tests run inside Unity.

## D2-013 — 2026-10-05 — Representation by scale

| Thing | Representation |
|---|---|
| People | persistent individuals (activity level), households |
| Livestock | persistent individuals owned by households |
| Wildlife | abundance fields per species and cell (age classes, condition) + herd/pack super-individual agents near people and the camera, drawn from and returned to the fields |
| Fish | fields + visible schools elaborated from them |
| Plants | biomass per plant functional type per cell; trees as deterministic instances; individual tree slots only where people work wood |
| Disease | SEIR per settlement and travelling party; pathogen lineages with traits |
| Evolution | trait means and variances per regional population; speciation by divergence |
| Climate, hydrology | fields on N32 / N64 |
| Geology | plates, crust fields, stress; eruptions and quakes as events |
| History | the causal ledger |

## D2-014 — 2026-10-05 — Worlds begin before people

**Decision.** World generation forms the planet (plates, relief, climate, vegetation, fauna spun up for a
period without people), then founding groups of people appear in habitable regions with basic knowledge
(fire, foraging, simple tools, early cultivation and herding for the first worlds). The foundation proof of
concept starts with a few small villages.
**Reason.** "Extremely simple beginnings"; later worlds may begin with foragers only.

## D2-015 — 2026-10-05 — Activity granularity: one event per activity, meaningful blocks

**Context.** The first POC scheduled a walk and a stay as two events and many short leisure blocks: ~29 events
per person-day, 2.4 us each (dotnet), 1.3 world years/s for 145 people.
**Alternatives.** Keep fine steps and optimise code; COARSE now; merge steps analytically.
**Decision.** A local activity is one event: the walk part is analytic (`From -> To` over `[T0, TA]`), the activity
is done at `To` until `T1`; interrupts split the stay by elapsed fraction. Blocks are sized like a day is lived
(work 2.5-4.5 h, leisure 1.5-3 h, meals 25 min, children's play 2-3.5 h).
**Evidence.** 210K -> 74K events per world year for ~170 people; steady-state 0.8 us per event; 13-14 world years/s
for 300-400 people in dotnet, all tests (determinism, save/load equivalence) unchanged.
**Invalidation.** Visible behaviour that needs finer authoritative steps (then a PROCESS, not finer activities).

## D2-016 — 2026-10-05 — Food and land laws for a 48-day year

**Context.** Yields calibrated per real year made granaries grow without bound (a person eats 48 FU a year here).
**Decision.** World laws: 1 FU per adult-day; yields 130 FU/ha per good season; 0.5 ha per person; labour ~93 h/ha
per season (sowing 40, tending 8, harvest 45), so farming fills the seasons; grain spoils ~12 %/year; rationing to
the next harvest (minimum 45 %); foraging when stores are empty; health by nutrition and frailty per body.
**Evidence.** 3 seeds x 30 years: granaries ~1-3 years of need, no starvation, villages survive failed harvests.
**Consequences.** Land must become limiting (fields need real arable cells within reach) before long runs: next.

## D2-017 — 2026-10-05 — Cross-runtime determinism verified (dotnet == Unity Mono); Web pending

**Evidence.** `CrossRuntime.Probe()` (DMath, RNG, worldgen, 3 simulated days) gives identical hashes under .NET 8
RyuJIT (headless) and the Unity 6.6 editor's Mono. The Web build (IL2CPP -> wasm) is checked by `webtest.mjs`.
**Consequence.** Headless tests and long runs are evidence about the shipped world.

## D2-018 — 2026-10-05 — IMGUI for the foundation HUD

**Context.** UI Toolkit at runtime needs a theme style sheet and font assets created in the editor; the F0 HUD
is a debug instrument. **Decision.** IMGUI (`OnGUI`) for F0 (works on every platform with zero assets); the
player-facing UI (news, inspector, History Book) moves to UI Toolkit in F7 with assets made by an editor script.

## D2-019 — 2026-10-05 — A WebGPU device probe before Unity starts; WebGL2 when it fails

**Context.** In Playwright's Chromium, `navigator.gpu` exists but device creation fails (Dawn cannot load
`dxil.dll`); Unity 6.6 then initialised its WebGPU path with no device and crashed ("reading 'limits'") instead
of falling back to WebGL2. Real players can have the same (old drivers, blocklists, missing compilers).
**Decision.** A project web template (`Assets/WebGLTemplates/TC2`) requests an adapter *and a device* before
loading Unity; on any failure it hides `navigator.gpu`, so Unity takes the next API in the list (WebGL2).
`?gfx=webgl2` forces the fallback. The probe's verdict is in `window.__TC2_GFX`.
**Evidence.** System Chrome: WebGPU, 5.4 s to a running world, no console errors; Playwright Chromium crashed
before the probe existed.

## D2-020 — 2026-10-05 — Materials are assets (so their shader variants ship)

**Context.** Instanced objects drew nothing in the Web build: materials created at runtime meant the build kept
no `INSTANCING_ON` variant ("strip unused" instancing variants is Unity's default).
**Decision.** `ProjectSetup` creates `Assets/TC2/Game/Resources/TC2Materials/*.mat` (Instanced with GPU
instancing on); views load copies (`Materials.Load`). Any new shader feature used at runtime gets a material
asset with that keyword/feature enabled.

## D2-021 — 2026-10-05 — COARSE resolution implemented (deep-time catch-up)

**Decision.** `World.Resolution` (Activity | Coarse) changes only by a logged command at its time. Entering
COARSE ends every activity (people at home, no step events); the settlement's day then gives each able person
the day's work by the decision generators' priorities and rates and three rationed meals; brides relocate at
once; perception does not run; planet periods coarsen (climate daily, vegetation every 12 days). Leaving COARSE
gives everyone a fresh decision within the hour.
**Evidence.** Tests: enter/leave keeps the audit and determinism; save/load in COARSE continues identically;
people eat and harvest. 100-year runs, 2 seeds: ~3x faster in total (34-36 vs 12 world years/s headless; agent
events 66K vs 15M per century: the planet systems now dominate); population 531/512 vs 567/674, marriages and
births within ~20 %. COARSE showed more hunger deaths (foraging was capped at 3 h a day; ACTIVITY foragers go out
several times) - foraging aligned; to re-measure. Famine responses (relief, migration) are F5 for both.

## D2-022 — 2026-10-05 — Rivers and lakes as a water balance on the drainage tree

**Context.** F1 needs rivers and lakes that are consequences (droughts shrink lakes, wet years swell rivers,
villages choose fresh water). A first version marked every filled depression as a lake: 316 lake cells, many in
deserts and on ice.
**Alternatives.** (a) Every filled depression is a full lake (wrong in dry climates); (b) Fill-Spill-Merge with
the full depression hierarchy (Barnes 2020; more machinery than N64 needs now); (c) depressions grouped by their
spill cell, each holding a stored volume with a hypsometry from its cells (water fills the deepest beds first).
**Decision.** (c). New state fields: `RiverFlow` (discharge smoothed over ~30 days) and `LakeWater` (m³ per
depression, kept at its deepest cell); `LakeDepth` is derived (recomputed from `LakeWater` after a rebuild or load
- derivations always from the stored float, so loads are exact). Daily: runoff -> `Drainage.Route` (what enters a
depression stays in it; evaporation from the wet area; overflow to the spill cell) -> smoothed flow. Thresholds:
stream >= 1,000 m³/day, river >= 6,000 m³/day, lake deeper than 0.3 m. Worldgen spins the waters up for 400 days on
mean runoff, and site choice favours fresh water within a cell (x1.35). Physical scale is kept: the planet's
450 km² of land makes brooks and small rivers; presentation widens them.
**Evidence.** Tests: drainage reaches the sea downhill without cycles; routing conserves water; water is conserved
through the lakes (runoff = to sea + storage gained + evaporated, 1e-3); a fed basin fills monotonically, never
exceeds its capacity and spills only when full at its spill level; a lake without inflow loses exactly the
evaporation of yesterday's wet area and dries; streams end only in rivers, lakes, depressions or the sea; lakes
survive save/load and continue identically. Seed 42: 146-153 lake cells, 156-226 stream cells, 4.5 m³/s to the sea.
**Consequences.** Floods (daily discharge vs the smoothed flow), wells vs streams for villages, fishing and
irrigation can read these fields. Tectonics/erosion must call `Drainage.Rebuild` + `SettleStored` when relief changes.
**Invalidation.** If lakes must be separate inside one depression (nested basins at different levels matter for
play), move to Fill-Spill-Merge.

## D2-023 — 2026-10-05 — How an absence is lived: COARSE for many lives, ACTIVITY on arrival

**Context.** ACTIVITY catch-up in the browser runs ~6.6 world years/s with ~150-740 people (a Living day away:
15 s). Larger worlds and the Ancient / Deep time modes would take minutes.
**Decision.** `OfflineProgress.PlanCatchUp`: when a new catch-up's span holds more than 40,000 person-years, issue
two logged commands - COARSE from the next midnight, ACTIVITY two days before the target. A resumed catch-up keeps
its plan (the commands are in the saved queue). The rule reads only world state, so every host makes the same plan.
**Evidence.** Tests: a short absence stays in ACTIVITY; a long one is COARSE from the next midnight, back in
ACTIVITY before the target, and every person has a live activity on arrival (audit).
**Invalidation.** If COARSE histories diverge from ACTIVITY ones beyond the bands of D2-021, lower the use of
COARSE (higher threshold) until COARSE improves.

## D2-024 — 2026-10-05 — The slow climate: a zonal energy balance with a fast orbit, ice sheets and sea level

**Context.** F1 needs glacial cycles, coastline change and forests that move within playable history (01 §3), and
CO2/aerosol forcing for later causes (volcanoes, fires, people, god powers). The foundation climate was a closed form
with fixed means.
**Alternatives.** (a) Per-cell 2-D energy balance with diffusion on the sphere (Jacobi or multigrid for the
pole-to-equator scale; costly and slow to converge); (b) a zonal (1-D in sin latitude) Budyko-Sellers-North model with
albedo from the cells, solved implicitly (Thomas algorithm, unconditionally stable) plus per-cell lapse, land ice and
seasonal swing; (c) prescribed glacial cycles (scripted - rejected).
**Decision.** (b), stepped every season (12 days): `C dT/dt = Q s(x; obliquity) (1 - albedo) - (A - 5.35 ln(CO2/280) +
B T) - 25 x aerosol + d/dx[D (1 - x^2) dT/dx]`, 48 bands, A = 209, B = 2.09, D = 0.55 W/m2/K, heat capacity 0.25 (land)
to 10 (sea) W yr m-2 K-1; planetary albedo 0.20 + 0.62 x the cells' surface albedo (sea 0.07, sea ice 0.50, snow 0.55,
ice 0.62, forest 0.13, grass 0.20, bare 0.32). Orbit (fictional, fast, keyed by the seed): obliquity 23 +- 4 deg over
1,300 y (+ 1 deg over 870 y), eccentricity 0.007-0.05 over 3,100 / 1,900 y, precession 600 y; the summer factor
1 + 3 e sin(perihelion) per hemisphere scales the seasonal swing. Land ice per cell: accumulation 2.6 x precipitation x
the cold part of the year, melt 3.4 m per degC of mean positive temperature (a fast world law: sheets answer the orbit
within centuries), shallow-ice flow down the surface (calving at the sea). Sea level = reference - 0.9 x ice volume /
reference ocean area + 0.5 m/K of warming; the **coastline** (every land mask) follows it in steps of >= 1 m, which
rebuilds the drainage, drops drowned fields and records CoastChanged; ages of ice (ice volume > 1.6 x / < 1.2 x the
world's ice at creation) are Epochal events. Worldgen spins the climate (120 y) and ice (300 y) up with the generated
coast kept. New state: zonal temperatures, ice, CO2, aerosol, orbit, references, the continuous sea level and the
coastline level (serialized and hashed); per-cell annual mean, swing, global mean and the moisture factor (+5 %/K)
are derived.
**Evidence.** Tests: a generated world is temperate (8-20 degC) with 30-55 degC from equator to pole; higher obliquity
warms the poles and cools the tropics; doubled CO2 warms 1.5-6 degC; a veil cools within 1.5 years and the climate
returns as it fades; a colder world grows ice and the sea falls by exactly the water locked up; a moving coast
rebuilds the drainage, enters the history and leaves no field in the sea; save/load across climate steps continues
identically. Seed 42, climate alone 4,000 y: global 14.3-15.1 degC, poles -17 to -23, ice 17-27 % of land, sea +1 to
-11 m on the obliquity rhythm; with live vegetation 3,000 y: sea 0 to -20 m, two ages of ice, and the one village grew
to 475, fell to ~100 in the first glacial maximum, recovered to 456 and died out in the second. Cost ~3 ms per world
year (ice flow only over iced cells).
**Consequences.** Moisture follows warmth (+5 %/K) - a cold world is a dry one. Villages on drowned cells are only
recorded (relocation is F5's migration). Tectonics must rebuild the drainage and re-derive the climate when relief
moves. CO2 sources and sinks (volcanoes, fire, forests, weathering) are F1 next.
**Invalidation.** If regional climates matter beyond latitude and height (rain shadows, monsoons moving), move to a
2-D per-cell balance on N32 with a multigrid solve.
