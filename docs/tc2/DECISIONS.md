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
**Superseded in part (7 Oct 2026, D2-085):** the radius is 6 km (cells 137 m); the planet's laws keep geo-lengths of a 10 km sphere.

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

## D2-025 — 2026-10-05 — Plates in time: one crust sample per cell, handed over every 160 years

**Context.** F1 needs drift, ranges rising and wearing, and coasts that move over millennia (01 §4). Resampling relief
on the grid every step blurs it away; carrying samples that never change hands drifts out of the grid.
**Alternatives.** (a) Semi-Lagrangian advection of elevation per plate (diffusive); (b) a free sample cloud with
triangulation and periodic global resampling (Cortial et al. 2019; heavy for the Web); (c) one sample per cell with an
exact sub-cell (gnomonic) offset; samples move every step and cells change hands only when the plates have moved about
a cell.
**Decision.** (c). State per cell: plate, crust kind, age, offset; per plate: Euler pole and angular speed. Every 10
years the offsets move with their plates and boundary forcing acts (collision +2.2, arc +1.5, trench -0.35 offshore
only, rift -0.4 m per m/yr of convergence, spread over a belt); every 16 steps the cells are handed over:
nearest-sample resampling (no averaging), a gap where plates clearly part is new ocean floor at -650 m, a gap inside a
plate takes the plate's nearest sample, two plates in one cell count as convergence only if they close on each other
there (continents override oceans, younger oceans override older, colliding continents keep half of the loser's bulk
as height), 35 % of subducted samples weld crust onto the overriding continent. The sea floor cools and sinks with age;
continental crust above -300 m floats back to a shelf at -40 m over ~3,000 years; land creeps among land cells only
(no flux across the coast); ranges above 2,600 m spread to their flanks. Colliding plates drag each other toward one
motion and every plate's motion wanders on a 3,000-year scale (keyed). Villages ride their plate (anchor and frame
rotate; routes and the travel index rebuilt as a load would). The drainage routes over rock plus the ice it was built
with (a saved snapshot, so loads rebuild it exactly).
**Evidence.** Tests: a rigidly turning crust keeps its continents (2,000 years, within 3 %); a plate's interior is where
its motion carried it; new sea floor forms only at ridges; villages move exactly with their ground and stay indexed.
Bugs found by measurement: hillslope diffusion across the coast drained land into the sea floor (-80 m per millennium
at every coast); aliasing at plate boundaries counted as subduction and collision and grew continents 64 % under rigid
motion; continents always won aliasing conflicts against oceanic plates; averaging refills blurred margins; per-step
nearest-sample refills speckled coastlines (fixed by the 160-year hand-over). Seed 42, 4,000 years with climate: land
36.4 % -> 35.6 %, continental crust 48.6 % -> 50.9 %, peaks to ~3-4 km and back, 28 world years/s headless (planet only).
**Consequences.** Erosion by rivers (stream power) and sediment are next; quakes and volcanoes read the boundary forcing.
The view rebuilds the relief on `ReliefVersion`.
**Invalidation.** If coast speckle or crust drift returns in 10,000-year runs, move to (b) with resampling every few
centuries.

## D2-026 — 2026-10-05 — The miniature look: one ground model, chunked LOD terrain, farmland in the shader, a Blender kit

**Context.** Arjun: the world must be a really beautiful miniature with a high level of detail and high performance.
The F0 view (six face meshes at 82 m, objects as procedural boxes, colours converted to linear twice, no
post-processing because the generated renderer had no post-process resources) looked crude.
**Decision.**
- **Ground model** (`Game/View/Ground.cs`, presentation only): the simulation's surface interpolated by a smooth
  kernel over the containing cell and its 8 neighbours (exact at cell centres - coastal villages never sink;
  seamless across cube faces), plus fine relief the 245 m cells cannot hold (rolling hills +-15 m, gullies, ridged
  crags up to ~230 m on mountains, quiet at the shore and over lakes). The simulation's Earth-tall relief is drawn
  at 0.4x about sea level (a globe from orbit, not a spiky ball); the sea floor gets a gentle shelf. Every object is
  placed on this function, so nothing floats or sinks.
- **Terrain** (`TerrainLod`): a quadtree of 32x32-quad patches per cube face, split near the camera to ~4 m quads,
  built on the CPU within 3 ms a frame (parents shown until all four children are ready), skirts against cracks,
  per-vertex colour (biome blend, beaches, slope rock, snow by the cell's climate and season, sea-floor sand),
  recoloured in turn every 2.5 s. GPU-side displacement was rejected for now: WebGL2 limits (no structured buffers,
  vertex texture fetch filtering of float textures) and the need for identical heights on the CPU for placement.
- **Farmland** is drawn in the terrain shader in each village's own frame (uv0): furlongs with their own lie, strips,
  furrows faded with distance, hedges, fallow where the village works less than its land; village ground (square,
  yards, footpaths) in the vertex colours.
- **Water**: a translucent sea over the depth-coloured sea floor (no depth texture: cheaper on the Web, and the
  scene depth sampled was unreliable), surf in the terrain shader just below sea level; meandering rivers.
- **Air and grade**: aerial haze by camera height, a sky background below the atmosphere, URP post-processing
  (neutral tonemapping, +14 saturation, +9 contrast, soft bloom, vignette) with the renderer's post-process data
  assigned by `ProjectSetup`.
- **Asset kit**: models generated in headless Blender from `tc2/art/kit.py` (no external assets): four house types,
  granary, round hut, well, haystack, conifers, broadleaf, birch, bush, person, fence; flat-shaded with smooth
  canopies, linear vertex colours, vertex alpha = tint mask (clothes, roofs), metres, Y up, FBX in
  `Resources/TC2Models`. Instanced with `Graphics.RenderMeshInstanced` as before.
- **Look-dev loop**: `node tc2/scripts/unity.mjs shots --tag T [--views ...] [--hour H]` plays the real game in the
  batch editor and writes PNGs in ~40 s (vs 5-9 min per Web build).
**Bugs found on the way.** Mesh winding test reversed; shots rendered before the views queued their instanced draws;
a new world kept running the previous world's catch-up (26 days ahead in a shot, and in the game after "New world"
during a catch-up); Unity's `Mathf.SmoothStep(from, to, t)` is not GLSL `smoothstep` (beaches, rock and yards were
wrong); a material asset keeps its first-saved property values (the sea stayed opaque).
**Consequences.** Next: cloud layer from the weather, tilt-shift depth of field, grass and flower detail close in,
night lights, people animation, more kit (boats, fields' haystacks, roads). Performance to be measured in the Web
build (patch build time in wasm, draw calls, frame time at each view).
**Invalidation.** If patch building costs too much in wasm or vertex-colour detail is too coarse, move displacement
to the GPU on WebGPU (keep the CPU path for WebGL2) or bake per-patch colour textures.

## D2-027 — 2026-10-05 — Look-dev v2-v4: weather clouds, tilt-shift, lamplight, ground cover, roads, seasons, walking, a slim HUD

**Context.** Look-dev v1 (D2-026) gave a coherent ground; the miniature still lacked the cues that make a diorama read
as small and alive: depth of field, clouds you can fly through, lights at night, grass at your feet, paths between
villages, the colour of the year, people who move.
**Decision.**
- **Clouds from the world's weather** (`CloudView`, `Clouds.shader`): a 64² coverage cubemap filled one face per frame
  from each cell's rain today and moisture (kernel-smoothed across cells), eased toward each day's weather, drawn as a
  noise-warped shell 650 m up with shadows on the ground. The view reads state only.
- **Smooth weather spells** in the simulation: a cell's daily draw blends per-block normal draws with a 9-tap kernel
  (normalised to unit variance, then a logistic CDF back to a uniform), so rain comes in soft-edged spells with the same
  per-cell statistics; square blocks were visible as square clouds. The logistic is tabulated (basic IEEE operations,
  identical on every runtime) and the per-cell hash folds the day into one key: weather was 86 % of a planet-only
  coarse year (1.25 ms a day), now 0.82 ms (14.3 -> 19.4 world years/s coarse).
- **Tilt-shift** (`TiltShiftFeature`, RenderGraph, two blur passes before post-processing), strength 1 below 120 m
  falling to 0 at 2.6 km: the camera height decides how small the world feels; skipped when off.
- **Window lamplight**: a window mask in the kit (vertex alpha 0.5); a house glows after dark when someone of its
  household is home and awake (state read by the view).
- **Ground cover** on the finest terrain patches: grass clumps (tinted by the ground's own greenness), wildflowers in
  spring, reeds by lakes and streams, boulders on rocky slopes; placed on the patch surface (no extra height lookups),
  never on fields, paths, sand or snow; drawn within 260 m below 600 m altitude.
- **Roads** between neighbouring villages along their own travel routes (`Routes`), Chaikin-smoothed and indexed per
  cell into the ground colours.
- **Seasons** in deciduous canopies (fresh spring, gold and rust autumn, bare winter; the south has the opposite year).
- **Walking**: an animation flag per instance group swings legs and arms and bobs bodies of people who travel; they
  face where they go.
- **HUD**: a slim bar (date and speeds), Info / History / World panels behind toggles, a fading one-line ticker of the
  latest history, H hides everything.
**Evidence.** Chrome/WebGPU, 1280x720: look-dev v2 119-121 fps at every view; v4 (with grass, roads, walking, HUD)
111-122 fps from orbit to the low view, 91 fps at the close village view; browser = headless bit-identical each time.
**Consequences.** Next for the look: volcanoes drawn (cones, craters, plumes, lava glow), night ground light, chimney
smoke, boats, fences and gardens; the HUD date must fit on one line.
**Invalidation.** If grass costs more than ~2 ms a frame on the Web, instance it from a GPU buffer on WebGPU or thin it
with distance.

## D2-028 — 2026-10-05 — Volcanoes on the moving plates, eruptions as events, and the carbon cycle

**Context.** F1 asks for volcanoes whose eruptions have consequences people live with, and a long-term climate that
regulates itself (05 §3, 01 laws). Plates already move (D2-025) and know where ocean crust dives under another plate.
**Decision.**
- **Magma** (`P.Magma`, per cell, state) rises at arcs (overriding side of subduction: 0.9 per m/yr of convergence per
  boundary edge, from the tectonic step's forcing pass `Tectonics.Forcing`), a little at continental rifts, and over
  2-4 fixed mantle **plumes** (4-12 units a year); undrawn magma cools away over 400 years.
- **Vents** (`P.Vents`: position, plate, kind, pressure, seal, supply, flags, pending cone) draw 60 % of the magma within
  5.5 cells each decade; where magma gathers to 100 beyond any vent's reach, a new vent opens (spacing emerges from the
  reach: ~30 vents on seed 42). Vents ride their plates like villages; a vent whose crust went under another plate, or
  one fed nothing for 600 years, dies (history: fell silent). A plume under a moving plate therefore leaves a chain.
- **Effusion and pressure**: hotspot vents pour 60 % of their magma out quietly, arc and rift vents 20 %; poured lava
  builds the mountain (0.3 m per unit at the vent, 0.4x round it) and a sea vent that reaches the surface is an island
  (history: a new island rose). The rest is pressure held by the vent's **seal** (lognormal sigma 0.9; plume vents x0.3).
- **Eruptions**: hazard `0.0069 x³` per year with `x = pressure / (600 seal)`, drawn each decade with the moment inside
  it; VEI = `floor(2 + 3.1 log10(pressure / 720))` clamped 1-7 (a vent at its typical critical pressure gives VEI 2,
  four times that VEI 4, twenty times VEI 6). An eruption (`World.Erupt`) is a history event (Local below VEI 4,
  Historical from 4; importance grows ~2.2x per VEI) and every consequence has it as its cause: lava down the steepest way
  (1 + 1.5 VEI cells) sets the land alight and clears it; ash within 280 m x 2^(VEI-2) thins pasture and woods and costs
  villages under it 12 % of the standing crop per VEI above 1 (AshFall); from VEI 4 people within 350 m x (VEI-3) die
  (55 %, "in the eruption"); the stratospheric veil adds 0.15 e^(1.61 (VEI-6)) aerosol (VEI 6 ~0.15, VEI 7 ~0.75: a
  year or two of cold summers through the energy balance). The cone (22 m x VEI) is laid on the relief at the next
  tectonic step: relief changes only where the drainage is rebuilt, so loads derive exactly what the live world has.
- **Carbon thermostat** (`Climate.Carbon`): outgassing `0.28 (0.6 + 0.4 supply / supplyRef)` ppm/yr against weathering
  `0.28 sqrt(CO2/280) e^(0.06 (T - Tref))`: CO2 relaxes over ~2,000 years toward the balance of the planet's volcanism
  (more subduction, a warmer world). Off during the climate spin-up.
- **Worldgen**: 1,000 years of quiet gathering (no eruptions, history or rock) give the planet its volcanoes; each vent
  starts with a random share of its critical pressure; their supply is `supplyRef`. Volcanoes have names (`Names.Mountain`).
- Saves: format sim version 2 (older saves start a new world with a message); counters for eruptions by VEI, vents born
  and died, islands and deaths in `Metrics`.
**Evidence.** Tests: a plume under a plate moving 4 m/yr opens new vents as the plate carries the old ones away, the
chain lying along the motion (dot > 0.8); arc vents all have a subducting boundary within reach; a VEI 5 under a village
records ash on the standing crop, deaths "in the eruption", fires and a veil, every later event rooted in the eruption,
and the cone joins the relief at the next tectonic step; CO2 560 relaxes to 290-360 ppm in 4,000 years, doubled supply
holds 450-560; save/load through pending eruptions and a tectonic step continues bit-identically. Seed 42, 4,000 years
planet only: 28-42 vents (arc 23-35, hotspot 4-7), 650 eruptions (574 VEI 1 - mostly plume vents every ~25-40 years -
65 VEI 2, 7 VEI 3, 1 VEI 4), CO2 280 -> 336 ppm as arcs lengthened. A first design opened 200+ vents (8-cell draw) and
piled magma under plumes beyond any vent's reach (VEI 6 from accumulated magma); per-eruption drainage rebuilds halved
the speed.
**Consequences.** The view must draw volcanoes (cone relief comes through `ReliefVersion`; plumes and lava glow from
vent state). Quakes read the same boundary forcing next.
**Invalidation.** If eruptions crowd the history, raise the Local threshold for the news (VEI 1 is ~90 % of eruptions);
if CO2 drifts beyond 200-600 ppm in 10,000-year runs, tie outgassing to subduction length rather than vent supply.

## D2-029 — 2026-10-05 — Volcanoes drawn, puffs, gentle shores, moonlight; the god's eruption

**Context.** D2-028 made volcanoes in the simulation; the miniature must show them (cones, plumes, eruptions) and the
first look exposed two old faults: subduction coasts drew as sawtooth cliffs with a grey band of slope rock, and
nights were nearly black with round objects lit from below by a sun under the horizon.
**Decision.**
- **Cones in the ground model** (`VolcanoShape`, presentation only): one cone per vent found through a per-cell index
  (vents stand > 5 cells apart): arc volcano `H (1-t)^1.7`, H = 175 m x size (<= 340 m), base 1.9 cells; plume shield
  `H (1-t²)(1-0.35t)`, 70 m x size, base 3.4 cells; size `0.35 + 0.65 age + 0.22 ln(1 + eruptions)` (age over 2,000
  years; worldgen volcanoes are mature); crater bowl and radial gullies; rebuilt with the relief (`ReliefVersion`), so
  terrain patches and the cone always agree. Summits bare (dark lava rock, cinders), lower flanks keep their green.
- **Puffs** (`Puff.shader` + `VolcanoView`): billboards need no rotation, so the instance matrix's 3x3 carries size,
  opacity, ash, glow, seed and sun share (no per-instance buffers: identical on WebGPU and WebGL2); culling bounds given
  in `RenderParams`; sorted far to near; lit as balls with their own shadow side; glow strongest at night. Shows: steam
  from vents fed or active in the last three centuries; a plume vent's lava-lake fire; after an eruption its column
  (half a day of world time or 14 real seconds, whichever is longer) and lava glow along the steepest way (4 days or
  25 s). All read from vent state and the eruption's history event (VEI).
- **Gentle shores**: the ground model interpolates a drawn elevation per cell: land within d cells of the sea no higher
  than sea + 45 + 110 d m, sea within d cells of land no deeper than sea - 60 - 400 d m (sim metres); lake cells keep
  theirs. The coastline is unchanged; cliffs and the grey band of slope rock along trench coasts are gone.
- **Night**: a moon opposite the sun (rotated 150 degrees, raised) lights terrain and objects softly blue; the night
  horizon is bluer; every lit shader fades the sun by its height (`saturate(4 dot(up, sun) + 0.35)`).
- **The god's eruption** (`CommandKind.GodEruption`, R7): the nearest vent within 2.5 km erupts now with at least twice
  its critical pressure (or the size asked for); with none near, a new vent opens. Logged as a command; every
  consequence roots in "You". Natural eruptions scheduled for a vent woken early are skipped (the queued event checks
  the vent's `Next`). Screenshots: `unity.mjs shots --erupt VEI` uses this command.
**Evidence.** Batch-editor shots at day, dusk and night: cones with craters and plumes on seed 42's arc coast, a VEI 4
column with an orange root; no curtains or grey band on the coasts; moonlit village with lamplit windows. Test: the
god's eruption opens a vent where none is, and every event it causes roots in You (54 tests).
**Invalidation.** If puffs cost more than ~1 ms a frame on the Web (sorting, overdraw near a column), cap puff counts by
distance or drop the sort for steam.

## D2-030 — 2026-10-05 — Earthquakes: faults that load with the plates and break in cascades; slides and waves

**Context.** F1 asks for quakes from plate stress with consequences people live with (05 §3b). Plates move (D2-025);
their boundaries know how fast the plates slide past each other.
**Decision.**
- **Faults** are the boundary cells: each loads with the plates' relative motion across it (`Tectonics.Slip`: closing
  and sliding plates fully, parting plates a third) into a slip deficit `P.Stress` (state, saved, hashed). Cells no
  plate loads any more heal over 400 years and leave the network below 2 m.
- **Ruptures**: a patch past 250 m is scheduled to break at a moment inside the tectonic step; a breaking patch keeps
  2-10 % and passes 22 % of what it released to each fault neighbour (Olami-Feder-Christensen), which may break in turn.
  Sizes follow from the cascade (no drawn magnitude): seed 42 over 3,000 years planet-only: 4,741 quakes of M 5, 1,753
  of M 6, 93 of M 7, none of M 8 (a pass of 0.25 is near-critical: an M 8 every ~26 years).
- **Magnitude** `5.5 + 1.4 log10(released / threshold)`; **intensity** (Mercalli-like) `6.42 + 1.333 M - 2.5 log10(d +
  500)` (d in m: 9 over an M 7, 8 a kilometre away, 7 at 3 km; a small world feels its quakes across much of itself),
  reach of intensity 6 at most 15 km.
- **Consequences** (each caused by the quake event, recorded at once from M 6.5 or at the first harm): people die
  where houses fall (indoors 90 % at night, 35 % by day; risk `0.04 (I-7.5)²` indoors, a fifth outdoors); stores spill
  (`min(0.5, 0.08 (I-6))` of the granary from I 7); steep shaken slopes slide (sim slope > 0.8, chance `0.08 (I-6.5)`,
  up to 40 m of rock moved to the lowest neighbour, woods and pasture stripped; the drainage is rebuilt at once, so a
  slide can dam a river); a great quake mostly under the sea (M 7+) sends a wave: run-up 15 m (sim) at M 7, x3.2 per
  magnitude, weakening as `sqrt(1500/(d+1500))`, flooding low land within two cells of the sea (woods and pasture
  spoiled, the standing crop of villages whose fields lie there lost, half the people there drown).
- **Worldgen**: every fault patch starts somewhere in its cycle (keyed). The god may shake the earth
  (`CommandKind.GodQuake`, magnitude asked for: a fault line loaded to the brink through the cell and broken at once),
  every consequence rooted in You.
- **For the view** an `FxRing` of the latest happenings (quakes, slides, waves, lava cells) is written by the
  simulation and never read by it, saved or hashed; the view shakes the camera when a quake is felt where it looks and
  raises dust, foam and lava glow. Save sim version 3.
**Evidence.** Tests: stress only on faults; 6,000 years of loading give >1,000 ruptures, most one patch, big ones
rarer, the biggest rarest; magnitudes of one and ten patches in band; a great quake under a village kills people
indoors and spills stores, every event rooted in it; a great quake under the sea floods a low coast with the quake as
the wave's cause; save/load through pending quakes and tectonic steps continues bit-identically (58 tests). Seed 42's
villages stand 3-5 km from active faults (sites follow water and soil), so M 7 quakes reach them at intensity ~6.6:
in 3,000 years no quake deaths, slides on the arc mountains (~1 a year), small waves on empty coasts. 20 world
years/s coarse with quakes.
**Invalidation.** If villages drift onto faults and quakes empty them every century, revisit the indoor risk; if
slides strip arc mountains bare, lower their chance or let them regrow faster (F2 ecology).

## D2-031 — 2026-10-05 — Rivers cut and carry: stream-power erosion with sediment, lakes that fill, deltas

**Context.** F1 asks for relief that rises and wears (05 §3b): plates raise ranges and a wear term smoothed them, but
rivers did not shape the land, and nothing moved rock to the sea.
**Decision.** Every tectonic step, on a drainage tree rebuilt for the moved relief (`Erosion.Step`):
- **Incision** (Braun & Willett 2013, n = 1, implicit in stack order: stable for any step):
  `h' = (h + F h_r') / (1 + F)`, `F = K dt sqrt(Q) / L`, K = 1.1e-3, Q the river's own smoothed flow (m³/day) - a
  wetter climate cuts faster, a lower sea in an age of ice deepens the lower valleys; only where Q >= 300 m³/day
  (smaller trickles are the hillslopes' work), never under ice sheets or inside lakes, never below the sea.
- **Sediment** travels down the same tree (tributaries first) and settles where the water slows: lake beds rise toward
  their surface (lakes fill over millennia), flats (slope < 0.004) keep a third (floodplains), and at the mouth the
  load fills the shallows to 0.6 m under the surface, spilling outward down the shelf, until the mouth rises just above
  the sea (a delta becomes land). Mass is kept: cut = settled + carried to the deep.
- Land and sea are judged by the mask the tree was built with (a delta that rises during the pass must not swallow
  later loads), and the drainage is rebuilt after: rivers follow their new valleys.
**Evidence.** Test: 1,000 years of erosion cut river cells metres down, leave hillslopes away from rivers alone, keep
mass to 1e-6, and fill the shallows at river mouths (a leak of 2e-5 found by this test: a delta rising mid-pass). Seed
42, 3,000 years planet-only: 0.7-1.5 million m³ moved per decade, nearly all settling; valleys ~6 m (sim) below their
banks; 20 world years/s unchanged.
**Invalidation.** If deltas grow faster than a cell a century at many mouths, raise MouthDepth or let part of the load
pass to the deep; if valleys stay too shallow to see, raise K (and check uplift keeps ranges).

## D2-032 — 2026-10-05 — Storms: cyclones and winter storms as moving entities

**Context.** F1 asks for weather and storms (05 §3): the daily weather had spells but no storms with a life, paths and
harm.
**Decision.**
- **Storms** (`P.Storms`: centre, heading, speed, strongest wind, radius, kind, flags, peak, birth, event, cause) step
  every three hours as one process. **Cyclones** are born over warm seas (8-20 degrees, sea >= 26 C) in late summer
  (0.08 a day per hemisphere at the peak, more in a warmer world), drift west and curve poleward, then east past the
  subtropics; they grow toward the sea's potential (`20 + 14 (SST - 24.5)` m/s, at most 78) and die over land
  (e-folding 0.6 days) or cool water. **Winter storms** form on the polar front (38-56 degrees) in the cold half of the
  year (0.07 a day), run east, grow for a day toward 34 m/s and fade. A storm dies below 14 m/s or after 10 days.
- **Winds** are a Rankine vortex with a calm eye (strongest at a fifth of the radius). Consequences, each caused by the
  storm's history event (recorded at 33 m/s, or at its first harm or landfall): rain for the next weather day
  (`P.StormRain`, state; heavy near the eyewall: rivers flood through the bucket's quick runoff), blowdowns in woods
  above 32 m/s, crops flattened above 25 m/s (one record per storm and village, later loss added quietly), people
  caught out above 40 m/s (rare), and at landfall above 35 m/s a surge of 0.6 m per m/s above 30 over low coastal
  land (woods and pasture spoiled, fields flooded, a quarter of the people there drown).
- The god may raise a storm (`CommandKind.GodStorm`, winds asked for): its consequences root in You.
- The view draws a storm in the cloud cover: a dense ring round a clear eye and spiral bands turning with the
  hemisphere. Save sim version 4.
**Evidence.** Tests: a 29 C sea makes a great storm in two days, a day over land makes a 50 m/s storm a gale; a 58 m/s
eyewall over a village records the storm first and every later event (crops flattened) roots in it, and its rain
reaches the next weather day; save/load with storms in flight continues bit-identically (62 tests). Seed 42, 300
years: ~2.8 storms a year (most winter storms 20-30 m/s), 20 above 40 m/s, ~0.55 landfalls a year. Shots: a 60 m/s
god storm reads from orbit as a spiral with an eye.
**Invalidation.** If great storms are too rare to matter in play (this planet's tropics are ~26 C), let the potential
follow the planet's own tropical mean rather than Earth's; if storms cost more than ~10 % of a coarse year, step them
every six hours.

## D2-033 — 2026-10-05 — Look-dev v6: chimney smoke, rain and snow, the sea's glint, one puff renderer

**Context.** The miniature needed the small signs of life and weather that read at a glance: smoke from lived-in
houses, rain falling where it rains, the sun's glint on the oceans from orbit.
**Decision.**
- **One puff renderer** (`Puffs` + `PuffRenderer`, execution order 900): every view adds soft puffs during the frame
  (volcano steam and columns, lava glow, slide dust, wave foam, chimney smoke); the renderer sorts them far to near and
  draws them last in batches of 1,023.
- **Chimney smoke**: each house kind's chimney top found from its mesh (the centroid of its highest vertices; the
  small house has none); a house smokes when someone of the household is home and awake and it is a cooking hour
  (5:30-9, 16:30-21 local) or cold (< 11 C at the village); eight puffs rising ~9 m and drifting, within 1.1 km of the
  camera.
- **Rain and snow** (`RainView`, `Rain.shader`): a cylinder of procedural streaks (flakes below 0.5 C) round the
  camera below ~700 m, as strong as the day's rain in the camera's cell, at least as strong as a storm's winds there
  call for, leaning with them; it comes and goes over two seconds.
- **The sea's glint**: from afar a broad soft sun glint (a rough sea) and broad wind patches that vary the colour; close
  in the sharp sparkle as before; the moon's path on the water at night.
**Evidence.** Batch-editor shots (village by day and dusk, a storm landfall, orbit): smoke over lived-in houses at
breakfast, rain streaks under a storm, a sun glint on the oceans from orbit. Web build measured in the session log.
**Invalidation.** If puffs exceed ~1 ms a frame on the Web with many villages in view, cap smoke by distance rank or
drop sorting for smoke.

## D2-034 — 2026-10-05 — Coasts that stay coasts; a faster carbon thermostat; vegetation in coarse time

**Context.** The first 10,000-year run of seed 42 (F1 done-when) ended in speckled archipelagos: nearest-sample
hand-overs left single cells on the wrong side of the sea and the noise accumulated (D2-025 named this risk). A rerun
with coast smoothing slid into an ice age it never left (sea -98 m, ice on 39 % of land, 9.6 C), while the first run
had warmed ice-free (17.9 C): the climate was bistable and the carbon thermostat (weathering e^(0.06 dT), ~2,000-year
memory) too weak to end either state.
**Decision.**
- **The sea shapes its coasts** (`Coast.Step`, each tectonic step): a land cell with six or more sea neighbours (a
  headland, an islet) wears down up to 3 m a decade toward the sea floor, its rock spread over the shallows round it; a
  sea cell with six or more land neighbours (a narrow inlet) fills from its shores. Mass kept. High volcanic islands
  stand for millennia (their vents build faster than the waves wear).
- **Anti-aliasing of the hand-over** (`Tectonics.Despeckle`): at each 160-year hand-over a lone cell with six or more of
  its eight neighbours on the other side of the sea level takes their median elevation (read from a copy; never under a
  volcano).
- **Carbon thermostat**: outgassing 0.5 ppm/yr at rest (~1,100-year memory) and silicate weathering e^(0.1 dT) (~10 % a
  degree: faster reactions, more runoff) - a cold world builds CO2 faster, a hot one draws it down faster.
- **Vegetation in coarse time**: the vegetation step ran every 12 days in COARSE but grew 4 days' worth (plants grew at a
  third of the rate in every coarse run); it now grows by the step it covers.
**Evidence.** Test: a low islet is gone and a narrow inlet filled within 120 years, rock volume kept. The 10,000-year
reruns with these changes are in the session log (PROGRESS).
**Invalidation.** If coasts still speckle after millennia, hand over more often with smaller offsets; if the climate
still locks into either extreme for millennia, tie ice-sheet growth to snowfall more tightly (a cold world is dry).

## D2-035 — 2026-10-05 — Wildlife as abundance fields (F2, first slice); animals in the miniature

**Context.** F2 asks for species as data, abundance fields, predation and recovery (06, D2-013); the miniature needs
visible animals.
**Decision.**
- **Species** (`P.Species`): eight templates varied per world (+-15 % size, fecundity, lifespan; cold tolerance +-2 C):
  deer (browser), hare, aurochs, ibex (grazers), boar, fox, bear (omnivores), wolf (predator); each with diet shares
  over four foods, habitat (cover, slope, cold), dispersal, and a territory cap for hunters. Names registered.
- **Fields** `P.Abund` (animals per cell) and `P.Cond` (body condition) per species, saved and hashed. Each vegetation
  step (4 days; 12 in COARSE): foods available - grass above a floor of 0.25 cover (at most 30 % of it a step),
  shoots (renewed yearly), mast and roots in the woods, small fare in the grass (voles, beetles: the fox's other food) -
  all lean in the cold (20 % at freezing; mast half); each animal eats its preferred mix and makes up a shortfall from
  what is left; hunters take prey by a Holling II response on reachable prey biomass (a quarter always out of reach,
  more in woods; half sated at 120 kg x (mass/35)^0.75; large prey less vulnerable, `(4 hunter/prey)^0.8`); condition
  follows how well each ate; deaths by age, hunger (below 0.45) and cold; young in spring only above condition 0.5,
  hunters only where their range has room; a share wanders to the better neighbouring cells. A bear sleeps through
  the cold (15 % of its needs). Grazed grass grows back from less (the vegetation step); browse does not kill trees.
- **Worldgen**: 20 years of the animals and the plants together before people (+1 s of world creation headless).
- **View** (`WildlifeView`): within 2.4 km of the camera's focus (below 1.8 km), each cell shows the animals its fields
  hold (the fraction as a keyed chance), in groups that wander the cell on slow loops of the world's time and face where
  they go, kept 350 m from villages (hares and foxes slip in), 1.4-3x life size; Blender kit models for the eight kinds.
**Evidence.** Tests: animals spread over the land they suit and never at sea or in the deep cold; after a deer crash,
deer recover faster without wolves, and wolves without prey starve; grazed land holds less grass (66 tests). Seed 42,
100 coarse years: deer ~35,000, boar ~38,000, hares ~60,000, wolves ~1,550, foxes ~2,400, bears recovering from a
spin-up crash (~7), aurochs and ibex rare (~20 each); grass held at ~0.25 cover. Bugs found by these numbers: a
spin-up that ate for 40 years with no regrowth (bare, treeless land), rigid diets (deer starving beside shoots),
predators that found prey at any density (prey extinct), hunters started nine times above their ranges (bears
unable to breed), fast breeders winning every pasture until breeding needed good condition.
**Consequences.** Next in F2: trait evolution per regional population, speciation in isolation, extinction events in
the history, herds as agents near people (hunting), fish in the sea.
**Invalidation.** If herbivores keep the grass at its floor across most land (a brown world), lower forage or raise
predation; if a species dies out in most worlds within centuries, revisit its niche.

## D2-036 — 2026-10-05 — Rock under ice does not weather; crags shelter climbers

**Context.** With the faster thermostat (D2-034) seed 42 still slid into a glaciation: ice locks the poles while the
global mean falls only ~1.4 C, so a temperature-driven thermostat barely answers (CO2 +25 ppm a millennium). In the
same run the ibex died out (wolves reached them on any ground) and aurochs dwindled.
**Decision.**
- **Silicate weathering scales with ice-free land** against the world's start (`P.FreeLandRef`, saved): as ice sheets
  spread, weathering falls and CO2 builds until they melt; as they retreat, weathering resumes. Earth's own brake on
  glaciations (with the temperature and CO2 terms of D2-034).
- **Crags shelter climbers**: a prey's reachable share falls by 0.6 x steepness x its slope preference (ibex on their
  cliffs), on top of the cover of woods.
- Large prey are less vulnerable still (`(4 hunter/prey)^1.2`: a wolf rarely takes a grown aurochs); aurochs breed a
  little faster (0.45 a year).
**Evidence.** The 10,000-year reruns are recorded in PROGRESS.
**Invalidation.** If worlds now oscillate too fast between ice and hothouse (cycles of a few centuries), weaken the ice
term (weathering ~ free land^0.5).

## D2-037 — 2026-10-05 — Coats that follow the climate; new species from isolation; extinction in the history

**Context.** F2 asks for evolution that is mechanism, not decoration: populations should adapt to where they live,
kinds should split only when something really separates them, and the loss of a kind should be an event of the
history. A trait per animal is far beyond the budget; a trait per species is too coarse to diverge.
**Decision.**
- **One trait, per species and cell**: `P.Coat` (C, the shift of that population's cold tolerance from its kind's),
  saved and hashed with the abundance fields. The cold that hurts a population and the cold that bounds where it
  thrives use its own coat (`ColdTol + Coat`).
- **Selection**: each ecology step a population's coat moves toward what its place asks (`TMean - 7 C - ColdTol`,
  clamped -14..12) at `0.0015 x (fecundity + 0.5)` a year (fast breeders adapt faster; an e-fold of ~250-600 years);
  a coat more than 3 C from that costs `0.01 x (off - 3)` of the population a year. Wanderers carry their coats and
  the cell's coat becomes the mix of residents and newcomers (gene flow, so a connected range stays one kind).
- **Speciation at tectonic steps** (every 10 years; superseded by D2-038): the occupied cells of each species are split into groups with no
  occupied path between them; the most diverged sizeable group (>= 12 cells, >= 20 animals) whose mean coat differs
  from the largest group's by more than 5 C becomes a species of its own (its coat folded into its `ColdTol`, its
  animals, condition and re-centred coats moved over, `Parent` the old kind; named "snow" or "sand" + kind). At most
  16 species. A species with fewer than 0.3 animals left is extinct (`Gone`), and both are Epochal history events
  (kinds 42, 43) with Nature at the root.
- **Also**: bears eat a broader diet (browse 0.2, mast 0.45, small fare 0.35) so they no longer starve where boar take
  the mast; the ecology step costs 6 ms instead of 10.5 (per-species intake and half-saturation precomputed, a
  vulnerability table per step, suitability once per cell, a rational e^-x for deaths) and in ACTIVITY it runs every
  third vegetation tick with three times the step (as COARSE already did). Save sim version 7.
**Evidence.** Tests (68): a coat far from its place moves a good way toward it in a century but not all the way, and
costs lives meanwhile; two cut-off groups of hares 7 C apart become two species with the history told, and a kind
with no animals is recorded extinct. The long runs are in PROGRESS.
**Consequences.** New species appear only where ranges are broken (sea, ice, desert, people later) and climates
differ - on an ice-age planet, the snow hares of a cold island. Other traits (size, diet) can follow the same field
pattern if the budget allows. The view does not yet show lineages apart (next: tint by species, the history card).
**Invalidation.** If worlds fill the species cap within a few thousand years, raise `Split` or the group minimum; if no
world ever speciates over 10,000 years with real isolation, lower `Split` to 4 C.

## D2-038 — 2026-10-05 — Crags feed climbers; adaptation costs less; spin-up refuges; new kinds need time apart

**Context.** Death-cause counters (`planet --ecodiag`, tools only: `Ecology.Diag`) showed the ibex of seed 42 dying of
hunger and of D2-037's coat cost - wolves took none: on mountain grass it competed with every grazer, and every
population began with its kind's coat, so cold-hardy kinds in warm valleys paid up to 9 % a year. The spin-up booms and
busts (hares 88,000 -> 8,000 in four years, foxes 3,975 -> 4), so a kind could die before history began. And the
coat alone split species within two centuries: hares re-adapt ~9 C in 200 years, tectonic steps come every 10 years
(not 50 as D2-037 assumed), and fragments at range edges were many candidates.
**Decision.**
- **Crag fare**: a fifth food, herbs and lichens of steep open ground (`CragMax` 150 kg/ha/yr x smoothstep(0.3, 0.8,
  steepness) x open share, lean in winter), reached only by climbers (not shared in the second helping). Ibex eat
  grass 0.45, shoots 0.15, crag 0.4. Crags are ~5-10 % of land (`planet --steep`).
- **Adaptation load** 0.002 a year per C beyond 3 C of mismatch (was 0.01).
- **Spin-up refuges**: during the 20-year spin-up only, no kind falls below a hundredth of its first numbers in a cell.
- **Lineage drift**: a neutral marker per species and cell (`P.Drift`, saved and hashed), carried and mixed by wanderers
  like the coat. At each tectonic step each population (group of occupied cells with no occupied path to the rest)
  wanders by a keyed uniform step, sd `0.0117 sqrt(10 y) (10/lifespan)^0.25`; one range wanders together. A population
  of at least 30 cells and 30 animals becomes a species when `|lineage difference| x (1 + 0.05 |coat difference|) > 1`:
  time apart makes kinds, different climates only hasten it (two deer ranges ~9,000 years; hares 10 C apart ~2,000).
- Save sim version 8.
**Evidence.** Tests (69): a cut-off group differing only in coat stays one kind; with its lineage apart it becomes one;
two ranges cut off wander apart while a range wanders together. Seeds 42, 7, 1234, 200-300 coarse years: all eight
kinds alive (ibex 1,500-5,000 on the crags, aurochs 50-290, bears 160-230, foxes ~2,000), no new species yet.
**Invalidation.** If no long isolated run ever speciates in 10,000 years, raise `DriftRate`; if worlds fill the
16-species cap, lower it.

## D2-039 — 2026-10-05 — The sea's life: plankton, shoals, their hunters, seals; hunters follow their prey

**Context.** F2 (06 §5) asks for plankton and fish fields by temperature, nutrients, light and ice, for fishing and for
chains through the sea (a warm sea, ash, oil later). The 10,000-year run of seed 7 (D2-038 build) also showed new species
forming too early: eight in the first 2,000 years, then the cap - every isolated population starts identical, so the
first centuries are a burst of divergence from the initial condition.
**Decision.**
- **Plankton** is derived each step for every sea cell (never stored): light (the noon sun's height from latitude and
  the season's declination - none in the polar night) x nutrients (0.15 everywhere, +0.5 in cold seas that mix deep,
  +0.35 on shelves, + river mouths: each river's flow over 20,000 m3/day, spread two rings out to sea) x growth with
  warmth x open water (sea ice shuts out the light). `PlanktonMax` 400 kg/ha/yr for the richest sea.
- **Five sea species** in the same fields as the land's (`Species.Habitat` 1): herring (cold shoals) and sardine
  (warm) eat plankton; cod hunt herring; tuna hunt sardines and herring far offshore; seals hunt herring and cod
  from the coasts (suitability needs a shore within ~3 cells). Sea suitability: warmth within each kind's band
  (`ColdTol`..`HeatTol`), the depth it keeps to (`Offshore`), a coast for seals. Fish eat less for their size
  (`Metab` 2.2-2.5 against 9 for warm-blooded animals); wide-ranging hunters are half sated at less prey per cell
  (`Range`: tuna 3, seals 12). Each cell iterates only its habitat's species.
- **Hunters wander toward their prey**: a predator's dispersal weight is its suitability x (0.3 + 0.7 x its reachable
  prey against twice its half-satiation) - no pack (or seal) drifts into land or sea its prey has left.
- Sea hunters start nearer what their prey can feed (1.5 x the prey's start share); the spin-up is 40 half-year steps
  (was 80 quarter-year: -0.6 s of world creation).
- Lineage drift slowed to 0.003 per sqrt-year (D2-038's 0.0117 split ranges within centuries; 0.007 still split the
  patchy fox range seven ways by year 1,200), and only populations of at least 40 cells and 80 animals drift or become
  kinds: passing fragments at a range's edge took their own steps and mixed them back as noise (the fox's lineage
  spread grew to 0.4 across one range). Seed 7: no new kind in 3,000 years; the long runs decide.
- Speed: sparse river mouths (a cached list), cached steepness (by relief version), the sea's geometry cached by
  relief, coastline **and sea level** (shelf depth reads it: a cache that missed it would differ after a load),
  interleaved dispersal arrays zeroed as they are read. The ecology step costs ~14 ms (8 before the sea).
- Save sim version 9; `MaxSpecies` 20 (`Ecology.MaxK` 24 sizes the arrays).
**Evidence.** Tests (72): fish never on land nor land animals at sea; plankton at river mouths > 1.5x the deep
ocean's, none in the polar night; sardines in seas >8 C warmer than herring's; seals only near shores; without cod
and seals the herring grow (+8 % over years 6-10) until they go hungry. Seeds 42 and 7, 200 coarse years: herring
~2.5M, sardines 14-20M, cod ~25,000, tuna 6,000-9,000, seals 70-220, the land unchanged. Coarse years cost ~35 %
more than before the sea (mostly the doubled occupied species-cells).
**Invalidation.** If the sea's cost blocks the Web budgets, step the sea every other ecology step; if seals die out
in most worlds, widen their prey (more kinds of fish).

## D2-040 — 2026-10-05 — Water coloured by its depth on the sea floor; the sea's life in the view

**Context.** Shoals drawn a metre under the sea vanished: the sea surface was 60-90 % opaque everywhere (one colour,
one alpha) because the scene depth texture is off (D2-026: unreliable and a cost on the Web), so the shallows read as
flat grey-green and nothing under the water showed. The world's coasts should look like a miniature's: clear water
over sand, turquoise shelves, deep blue.
**Decision.**
- **The sea floor carries the water's colour** (`Terrain.shader`): below the sea surface each fragment knows its own
  depth (`_TC2SeaRadius`), so it is seen through that much water by Beer-Lambert (the floor shows through
  `exp(-depth / 3.2 m)`, the rest is water whose colour runs from turquoise to deep blue over 28 m). The sea surface
  thins to 30 % (+ fresnel): sky, sun glint, ripples and the moon's path - its body colour now matters little.
- **Things under the water** use the same veil (`Underwater.shader`, opaque, instanced): the fish of a shoal are drawn
  as solid objects veiled by their depth, the thin surface over them.
- **Sea life view** (`SeaLifeView`): near the camera, sea cells show one shoal per ~500 shoaling fish (herring,
  sardines and their descendants; up to ten a cell), 15-40 fish swirling about a centre that drifts on a slow loop of
  the world's time, swimming higher (and drawn smaller) in the shallows, never into the sand; fish 6-9x life size with
  dark backs and the flash of a turning flank; now and then a fish leaps and splashes (a tuna where tuna hunt); far
  away fewer shoals and no leaps. Seals (Blender kit `animal_seal`) rest at the water's edge beside the sea cells that
  hold them, side by side looking out to sea, and swim off it by turns (heads and backs above the water). All keyed
  and drawn from the fields: watching never changes the world. Camera presets `coast`/`coastfar`.
**Evidence.** Shots `tc2/out/shots/sea9`-`sea11` (coast, village, region, orbit): clear shallows over sand to
turquoise and deep blue, shoals glinting in the bay by the first village, leaps and splashes; 34 shoals (900 fish)
near the shore, 90-120 shoals (2,500-3,300 fish) from the village view.
**Invalidation.** If the Web frame budget suffers, cap the fish per frame (now up to ~3,300).

## D2-041 — 2026-10-05 — New kinds come one at a time; extinct kinds leave the fields; fish within the frame budget

**Context.** Seed 42's 10,000-year run (D2-039 build) formed no new kind for 9,000 years and then seven "sand foxes" in
one millennium: the fox's many glacial refuges all began diverging at year 0 at one rate, so they crossed the line
together, filled the 20-species cap, and extinct kinds kept their slots. In the browser the village view fell from
~100 to 83 fps with ~3,300 fish built every frame.
**Decision.**
- **A split is a chance, not a line**: past a lineage distance of 1 a population becomes a kind with a keyed chance per
  tectonic step `0.004 + 0.12 (d - 1)^2` (certain by d = 4) - refuges that parted together split at different times.
- **At most four living kinds from one first kind.**
- **Extinct kinds leave the fields** at the tectonic step that records them (the history keeps them by id): their
  arrays are compacted and their slots are free. Species are found by kind or id, never by a remembered index.
- **Fish budget**: 2,400 a frame (nearest shoals first); each fish turns from its shoal's upright frame (one quaternion
  multiply), its place from two small tables (no hashing a frame); `Instancer.Add` skips its dictionaries for runs of
  one group (every view benefits).
**Evidence.** Tests (73), among them the split chance (none below 1, rare just past it, certain at 4) and an extinct kind
gone from the fields. Web (WebGPU): start 6.1 s, orbit/region 120 fps, village 88 fps (83 before the budget), close
98-103, a day away catches up 96 years in 27 s (22 s before the sea's life). The long runs are in PROGRESS.
**Invalidation.** If no long run forms a kind, lower the chance's floor; if bursts remain, add a per-kind cooldown.

## D2-042 — 2026-10-05 — Villages live off the land: wild food from the same fields, body reserves, remembered famine

**Status.** Built (session 2).
**Context.** Session 1's 300-year coarse run of seed 42 lost 4 of 6 villages. Souhope's chain: field fire -> harvest
failed -> granary (under a year) empty -> 40+ hunger deaths in 2-12 days -> abandoned. Mechanism gaps: no food but the
fields; foraging fed ~1 person per forager from an infinite source (`0.28 x wild` per hour, nothing taken from the land);
starving health fell 10 %/day (dead in 5-17 days; a fasting adult lives 30-70 days); no reserve beyond the next harvest
and no memory of famine. Measured this session: people eat 2.7 meals a day in ACTIVITY at fixed 1/3-FU portions, so
daily intake already runs ~10 % under need - any physiology on intake must first fix portions.
**Alternatives.** (a) A generic anti-collapse rule (relief appears, deaths capped) - rejected: scripted protection.
(b) Hunting and fishing as abstract settlement yields - rejected: not the ecosystem's animals, not visible agency.
(c) People take whole animals and fish from the same abundance fields the ecology steps, by activities they choose,
with the same functions in ACTIVITY and COARSE; bodies carry reserves; villages remember famine through the people who
lived it. Chosen.
**Decision (design).**
- `Core/Places/WildFood.cs`: one ground-picking and one catch function per livelihood, shared by ACTIVITY and COARSE.
  *Game* = land species that do not hunt (role not predator, no prey mask), 2 kg and up (deer, hares, aurochs, boar,
  ibex and their descendant kinds). A hunter picks a ground (a land cell within reach of the village, weighted by the
  game it holds, nearer better) and hunts that cell and its neighbours: expected live kg = hours x rate x (0.5 + skill)
  x B / (B + half) over the reachable biomass B (woods and crags hide game, as from wolves); kills are whole animals
  (keyed Poisson per species and cell, never more than half of what is reachable in a cell), removed from `P.Abund`.
  *Fish* = cold-blooded sea kinds (herring, sardines, cod, tuna and descendants) in sea cells within reach of the shore,
  continuous kg (shoals are many), offshore kinds hard to reach from the shore, removed from `P.Abund`. *Gathering*
  (the old forage) takes mast, roots and berries from the woods: a new per-cell field `P.Gathered` (kg taken since the
  animals last ate, saved) caps what people find and is subtracted from the mast the boar and bears eat at the next
  ecology step - people and animals compete for the same food.
- Food from the wild enters the household larder (a few days of its need), the rest the village store (big game is
  shared). Edible share 0.5 (game) / 0.6 (fish); ~0.65 FU per kg of meat or fish, ~0.9 per kg of mast.
- People: `ActivityKind` Hunt 16, Fish 17, Gather 18, Haul 19 (bringing a catch home: the food arrives when they do);
  skills `SkHunt`, `SkFish` (keyed by person id, learned by practice); `WhyCode` FoodShort, Provide. Decisions: wild food
  scores by need (the ration's shortfall, empty stores) times the expected return of the best ground, plus a small
  ordinary share by boldness and skill (coastal villages fish); nursing mothers and those near term keep to short
  gathering near home. COARSE gives each able adult the same expected hours by the same rule and calls the same catch
  functions (comparison test of hours and catch per adult-day).
- Bodies: portions follow hunger (the share of a day's need since the last meal; more while reserves refill); each person
  carries `Reserve`, days of full food in the body (adults ~40 x a keyed body factor, children 12-34, infants ~8, elders
  less); each day reserve += eaten/needed - 1; health follows the reserve below half of it; death when it is spent. A
  fasting adult dies in 30-50 days, a child in 2-4 weeks; a nursing infant lives on its mother's reserve.
- Villages: rationing to the next harvest as before, plus prudence (keep 6 + 30 x memory days beyond it, rations no lower
  than 0.8 for prudence alone) and more fields (x (1 + 0.5 memory)); memory = the share of today's adults who lived through
  the last famine (were 8 or older then) - it fades as they die. History: Rationing (Local, caused by the failed
  harvest), WildFood (Local, in response to it), Famine (Historical, at the first hunger death of an episode; hunger
  deaths caused by it). Save sim version 10.
**Acceptance (mechanism, not outcome).** With game, fish or mast in reach, people physically take them; the fields
fall and recover; stores and larders rise by exactly the edible catch; starvation responds to stores; a village with
wild food in reach survives one failed harvest that kills a village without; two bad years in a row can still empty one.
**Evidence (built, commit 321bcf9 and after).** Tests (83; `WildFoodTests`): hunters take whole animals only from their
ground, the meat exactly the edible share; fishers take only fish, from the water; gatherers leave less mast and the boar
of those woods eat worse at the next step; two years of hard hunting thin the game within reach to 55 % of a control world,
and eight years after it stops the hunted land is back above the control; with nothing to eat at all nobody dies in the first
9 days, no grown body before day 28, children die first, and every hunger death is caused by the Famine; a village of 18
with its harvest lost and stores bare loses no one in 70 days on the wild's food while its barren twin loses everyone; famine
memory raises prudence and fields and fades; save/load mid-hunt continues identically; COARSE and ACTIVITY give the wild
similar hours and food in ordinary times and in a shortage. Measured: ACTIVITY ~0.2 trips and ~1.1 hours of wild work per
adult-day in ordinary times (habit, damped by full granaries), ~8.5 hours in a shortage; 0.6-0.7 FU/h on fresh grounds.
Bugs found on the way: founders' mothers did not nurse their babies (they starved in 6 days); a prudent cut recorded a
WildFood event with no cause; the minimal headless runner has no NUnit TestContext. A 300-year coarse run of seed 42: 145 ->
5,597 people, all six villages alive, the largest (870-1,300 on land for ~300) deplete their game by ~95 % and live in
recurring famine (4,267 hunger deaths, 43 famines from year 216).
**Consequences.** The wild is ~10x too rich (densities): the next ecology decision rescales it. Villages need to move:
migration and founding come next in F3.

## D2-043 — 2026-10-05 — The land wears as fast as the plates raise it; thin ice sheets; ages of ice against the planet's own normal

**Context.** The 10,000-year reruns of the current build (seeds 42, 7) failed F1: mean land elevation 160 -> 621 m and
105 -> 302 m, continental crust 47.8 -> 60.9 % and 61 -> 67 %, peaks to 3,500-4,300 m; seed 42 glaciated at year 188 for
good (sea down to -100 m, global mean 15 -> 10 C) while 01 promises seas within about +-40 m. Erosion moved ~0.1 million m3
a year against ~23 million of net uplift, and every cubic metre it cut settled on land or the shelf (none reached the deep).
**Investigation (planet-only 3,000-year experiments of seed 42 and 7, `tc2/out/relief/`; a per-process budget of the
continents' elevation, `planet --budget`).** (1) Erosion strong enough to balance uplift stops the drift, but a slope
failure at a gradient near 1 cuts every peak to ~1,000 m - the planet carries Earth-tall relief on 245 m cells, so its
gradients are steep by design. (2) River mouths that keep 40 % of their load build shallow shelves that a small fall of the
sea turns into bright land, strengthening the ice-albedo feedback (sea -95 m). (3) Every run, with or without the drift,
glaciated early and deeply: ice sheets were ~480 m thick on 5-30 km of land where a plastic sheet stands ~150-250 m
(3.4 sqrt(half-width)) - the shallow-ice flow was ~9x too slow. With nine times the flow the ice breathes with the tilt
(15-29 % of land, sea 0 to -18 m, 13.6-15.2 C, a ~1,300-year rhythm). (4) The budget then showed boundary forcing, ocean
floor raised into new land, volcanoes and shelf buoyancy adding ~10 m per thousand years to the continents and only river
cells eroding; hillslope creep only moved rock between land cells.
**Decision.** Erosion scaled to the fast plates: stream power K 1.1e-3 -> 5.5e-3; **every slope also creeps and washes
toward the way the water goes** (`Erosion.Kh` 2e-4 a year, implicit with the rivers' term, into the load); slopes past a
gradient of 2.5 fail into the load; river mouths keep 15 % of their load on the shelf and the rest goes down the slope to the
deep. Thickened crust floats (collision thickening 0.5 -> 0.15 of the loser's column) and accretion welds less (0.35 -> 0.1).
Ice flows nine times faster (`Climate.IceFlowPerM` 540, cap 810,000 m2/yr, sub-steps short enough for the thickest ice).
Ages of ice are named against a running mean of the last ~1,500 years of ice (from the ice the world was born with), entering
at 1.35x and ending at 1.1x - not against the one moment of the orbit the world was born at. Tools: `planet --budget`
(continental elevation change by process), `--noeco` (no animals: relief experiments), `--csv` (series).
**Evidence.** 3,000-year planet runs: seed 42 continental mean elevation 86 -> 78 m (was 98 -> 157), continental crust 47.8 ->
48.1 %, peaks 1,200-1,700 m, sea 0 to -18 m with glacial cycles on the tilt; seed 7 continental mean 6 -> 17 m (a shelf
transient: the buoyancy input falls 2.8 -> 1.1 m per 300 years), sea 0 to -5 m. Tests: rivers cut ~13 m and slopes wear ~26 m
in 1,000 years from worldgen's unrelaxed relief, every cubic metre settles or reaches the deep; a -8 C world grows ice, moves
its coast and names an age of ice. The 10,000-year reruns are in PROGRESS.
**Consequences.** The sea-level law of 01 (+-40 m) stands: seed 42's -60 to -100 m came from ice sheets three times too
thick, not from a stronger law. Peaks now stand ~1,200-1,800 m (01 said "to +2,500 m": revised to 1,000-2,500).
**Invalidation.** If the reruns still drift beyond ~+-15 % of mean continental elevation in 10,000 years, raise Kh; if
mountain ranges vanish from the view, raise the critical gradient or the forcing.

## D2-044 — 2026-10-05 — Wild animals at real densities; lineages part at the world law's pace

**Context.** People hunting the wild (D2-042) exposed the fields' densities: deer and boar ~1.2 a hectare (120 a km2), game
200-285 kg/ha against 5-30 for real temperate ungulates, wolves ~3 a km2 against 0.01-0.1; the woods fed people more per
hectare than fields; only villages of ~1,000 could deplete their game. Separately, neither 10,000-year rerun formed a new
kind: lineage drift of 0.003 per sqrt-year needs ~25,000-55,000 years of isolation to reach the line (01: 500-1,500).
**Decision.** `Ecology.Density` = 0.15, the share of the plants' growth wild animals can use as food (most of what grows is
out of reach, too poor to digest, or lost to the lean season), applied to every food (grass, browse, mast, small fare, crag
fare, plankton); hunting's half-satiation, the hunters' territories, the first numbers and the speciation threshold of a
population (80 animals -> 12) scale with it, so the food web keeps its shape at a seventh of the numbers. People's grounds
scale too (HuntHalf, FishHalf, GatherHalf; the woods' mast for people is Density of the old). A kill drawn where a cell holds
less than one animal takes what is there (the field is the truth: whole-animal kills only had made sparse game untouchable).
`Evolution.DriftRate` 0.003 -> 0.02 (two populations cut off reach the line in ~1,500-2,500 years at a 10-year life; D2-041's
keyed split chance and four kinds per first kind keep the refuges from splitting together).
**Evidence.** Tests: all kinds start alive at the new numbers; hard hunting (3 % of the game within reach a day) leaves 18 % of
a control world's game and eight years later 98 %; ACTIVITY's ordinary wild hours and returns unchanged (half-satiation
scaled). The 10,000-year reruns (speciation, extinction, cycles at the new numbers) are in PROGRESS.
**Invalidation.** If rare kinds (seals, aurochs, bears) die out in most worlds within centuries, widen their niches; if new
kinds flood the cap within a few thousand years, lower the drift rate.

## D2-045 — 2026-10-05 — Building by labour and wood; knowing places by going there; households leave, join and found

**Context.** Session 2's 300-year run with wild food (D2-042) grew villages of 900-1,300 on land for ~300 that sat in
recurring famine with nowhere to go; F3 asks for households that move and found, never a settlement that spawns finished,
never omniscient site choice. Houses, wells and granaries were drawn for every household and village by fiat.
**Decision.**
- **Construction** (`Core/Places/Construction.cs`): a household lives in a camp, then a hut (30 labour-hours, 8 wood), then
  a house (240 h, 35 wood); a village has a well (60 h) and a granary (120 h, 25 wood) only once built. The order: a camp's
  first roof, the village's well (water before comfort), a hut made a house, the granary. Wood leaves the store as the work
  goes - none, no progress; wood gathering turns urgent when building waits for it. ACTIVITY: a `Build` step scored by need
  (camp 0.85, works 0.45, house 0.4); COARSE: the same order with the day's free hours (hunger's need first when stronger).
  The first villages begin built; newly married couples and every founder begin in a camp. History `HomeBuilt` (personal),
  `WorkBuilt` (local). The view draws camps (a small hide shelter), huts and houses, and wells/granaries only when built.
- **Knowing places** (`Sites.cs`, `KnownSites` in settlement state): a village knows at most three sites, each seen by one of
  its people. A pressed village (`Migration.Pressure` = the share of its food its claimed land cannot grow + 0.6 x the ration
  cut + 0.4 x famine memory, at least 0.12) that knows no good place sends someone to look: in ACTIVITY a bold adult walks
  2-4 km out over land (a route to a cell, `RouteCache.GetToCell`), looks round (500 m), and walks home; in COARSE the same
  look on the same day. Site quality: worldgen's habitability x arable land within the fields' reach that no village holds,
  with water (fresh, or the shore) and no village within 1.9 km. History `Scouted`.
- **Leaving** (`Migration.cs`, once a year after the harvest, local day 41, both resolutions, villages older than 3 years):
  each household with young enough heads and no birth near decides by a keyed chance `1.1 x pressure - 0.12` (0..0.45; x1.4
  for households under six years old). Two or more households (five or more people) go together to the best known place;
  otherwise each joins a village its people know (within a day's walk) with arable land to spare and less pressure, kin there
  making it likelier. A **party** (saved state) sets out the next morning with the larders and up to 20 days of the village's
  store, walks at a party's pace (ACTIVITY: every member a travel step to the same arrival), and on arrival either founds a
  village - a camp: no houses, no well, no granary, no fields until it claims them in spring - or re-homes into the village it
  joined (a new lot, a camp). History `PartySetOut` (historical; in response to the famine, the hunger or the rationing, else
  the people's own choice), `SettlementFounded` (caused by the setting out), `HouseholdMoved` (personal).
**Evidence.** Tests (88; `MigrationTests`): a home is raised only by labour and wood (hut day 8, house day 27 in COARSE; no
wood, no house); a village knows no place until someone stands there, and the place is clear of villages; a pressed village
founds an unfinished village at the place it knows, the founding caused by the setting out, and a year on it has dug its
well and claimed fields; households with no place to found join a village with land; a party walks together and a save on the
road continues identically. Seed 42, 300 coarse years: 6 villages -> 35, 145 -> 11,744 people, 29 parties, famines 56 -> 3
(the expansion era: free land is being filled; behaviour at saturation is next to measure). Bugs found: a new camp measured
its land by its first year without fields (crowding 1) and left again at once; every household upgraded its hut before anyone
dug the well; COARSE gave building a fifth of a lived day's hours.
**Invalidation.** If worlds fill their land and then never shrink, or no village is ever abandoned in long runs with hard
times, the push or the coping is too strong; if parties found villages that die within a decade most of the time, the site
quality or the carried food is too poor.

## D2-047 — 2026-10-05 — Herds near people: identities over the field, never copies of it

**Context.** F2/F3 ask for wild herds and packs near settlements as meaningful agents that share the ecology's truth: a hunted
animal must come out of the same abundance the ecology steps, and agents returning or merging must not create or lose animals.
**Alternatives.** (a) Take animals out of the field into agents and put them back later - two truths, reconciled at every
ecology step, and every ecology rule needed again for agents; (b) a herd as an identity over the field: a name, a place and a
size, re-read from the field each day. Chosen (b).
**Decision.** `Core/Places/Herds.cs` (`Herd`, saved; `HerdSystem`): within a village's hunting reach, each herding kind (deer,
aurochs, boar, ibex; wolf packs) forms herds of 3-14 (aurochs to 24, packs 2-9) where the field round a cell holds that many
that no other herd counts. Once a day (with the village's survey) each herd moves toward where its kind is thickest round it,
is re-read from the field (never more than the animals round its cell; it grows only from animals no herd counts), merges with
another herd of its kind that comes within a cell (the elder keeps its name), and is dissolved when it falls below one - told as
"hunted out" (Local) only when people killed there in the last ten days, it was a herd, and the land round it holds less than
one of its kind. Hunters go to the largest herd of game (seven times in ten) in both resolutions; what they kill comes out of
the field there, and the herd is smaller the next day. A village's herds go back to being the land's when it is abandoned.
Also: `PagedStore.SetMaxId` now grows its page tables (a store loaded with a high max id walked past them: herds draw many
process ids, and the fire store crashed on its first hash after a load - three save/load tests caught it).
**Evidence.** Tests (89): no herd counts more than the field holds round it; taking nine tenths of a herd's kind from its land
makes it smaller the next day; save/load with herds continues identically. Seed 42, 30 years: 15-25 herds round each village
(deer to 14, boar to 12), none hunted out (game is plentiful near small villages). COARSE ordinary-time wild hours recalibrated
(0.6 of the mapping: building, water and company take free slots in a lived day): 0.96 against ACTIVITY's 1.03 hours per
adult-day; in a shortage 7.3 against 8.4.
**Consequences.** The view can now draw herds near villages from these identities (next visual slice) and hunters walking to
them; the stag that was hunted belongs to a herd with a history.
**Addendum (D2-048's session).** At scale the herd lookups cost more than the rest of a village's day: `BestGame` and
`MarkHunted` walked every herd in the world for every COARSE hunter, and each village's update walked all herds (a 300-year
run of seed 42: 2.9 ms per COARSE village-day at 29 villages, 34 us before herds). Each village now indexes its herds
(`World.HerdsOf`, derived, rebuilt on load in id order); hunters mark their own village's herds; the reach membership is a
stamp array. Same run: 28 us per COARSE village-day; 200 coarse years in 51 s (was ~110 s).

## D2-048 — 2026-10-05 — Livestock: a household's animals as individuals on the same grass as the wild

**Context.** F3 asks for livestock that shares the planet's truth: animals that eat the grass the wild grazers eat, breed,
give food, can be lost, and outlive their keepers - not a food multiplier.
**Alternatives.** (a) A herd size per household (a number that grows): cheap, but no mothers, no sires, no animals to draw or
follow; (b) every animal an individual in a store indexed by household. Chosen (b): a village keeps tens to hundreds; the
daily cost is ~18 us per village-day in COARSE.
**Decision.** `Core/Places/Livestock.cs` (`Beast`, saved; `Livestock` system), daily with the village in both resolutions
(see 08 §5d). Laws: intake by the wild animals' law (9 x mass^0.75 kg a year, as `Ecology.LandMetab`); cattle 350 kg, calf
after 36 days (0.75 year), grown at 2.5 years; goats 45 kg, 1.6 kids after 18 days; pigs 90 kg, ~6 piglets after 14 days;
meat as game (half the live weight, 0.65 FU/kg); milk 0.9 (cow) / 0.25 (goat) FU a day while the young are small. Grazing
takes a day's share of the standing grass above its floor (2 % warm, 0.4 % in the cold, by the day's mean temperature: the
village's day turns at midnight, in the night's chill) out of `P.Grass`; pigs take 1/6 a day of the mast left in reach into
`P.Gathered`, and 0.4 kg of scraps a day per villager feed their last share. Herders keep a breeding core and stock to
their hands (50 kg of fodder a day per grown hand) and to 0.6 of the day's offer; a hungry household eats its herd to a pair;
a marriage endows the couple; an abandoned village's animals join the wild field of their kind. No herding labour activity
yet (the hands bound the herd; the work itself is a later slice).
**Evidence.** Tests (11, `LivestockTests`; the shared audit checks every live animal is in its household's list and only
there): the young are born to their mothers' households in season; no buck, no kids (the other kinds still breed); grazing
lowers the pasture's grass and the wild grazers round it ate no better; a hungry household slaughters and keeps a pair; wolves
take animals only where wolves are (20 in 30 days with packs, 0 without); a household that dies out leaves its animals to the
living; an abandoned village's animals go wild; a herd is cut to what its hands can tend; save/load continues identically;
counting the herds changes nothing; ACTIVITY and COARSE 248 / 250 animals and 287 / 287 births over three years. Seed 42,
200 coarse years: all three kinds live (38 / 1,128 / 103 for 3,539 people). Found on the way: herds grew to the pasture
and crashed (destocking spared every pregnant or nursing female: all of them); goats displaced cattle on free browse; a fire
on the pasture left herds with no male that never recovered (neighbours' sires); the youngest-first cull ended the pig lines
(replacements are kept now); new households had no animals and herds died out as the people multiplied (marriage endowment);
the herd's day ran in a village whose people had all died (it waits for the abandonment); tool counts moved animals between
households (counting is now a pure read).
**Invalidation.** If herds of a kind vanish from most villages of a long run with land and people to keep them, or the
slaughter feeds more than about a fifth of a farming village's food, the breeding core, the stocking or the endowment is
wrong.

## D2-049 — 2026-10-05 — Farm animals, herds and camps in the miniature

**Context.** D2-045/047/048 gave the simulation camps, wild herds near villages and every household's animals; the view drew
camps as shrunken huts, wild animals only from the abundance fields, and no farm animals.
**Decision.** A `LivestockView` elaborates each village's animals from the simulation (positions from keys and the world's
time only; never written back): by day on the household's own ground among the nearest pasture cells, at night in a fenced pen
behind its home, walking between at dawn and dusk; pigs round the yard. The wild herds of D2-047 are drawn where the simulation
keeps them, the field's animals they gather left out, kept to a village's edge. New kit models in the kit's own style: cow (dun:
coats tinted), goat, pig, hide tent. See 13 §7.
**Evidence.** Screenshots `tc2/out/shots/stock_h*` and `stock2_h*` (seed 42, day 3: 64 animals near the village; 11 herds
drawn): pens full at 19.6-20.5 h, pigs at the yards and empty pens at noon, herds 250-570 m out on the outfield beyond the
fields (the village's fields ring it), no deer among the houses after the edge rule.
**Consequences.** To check: the field patchwork in the village shot spans ~20 cells while each of seed 42's villages claims
4-6 (claims are whole 5-ha cells weighted by arability: ~30 ha drawn for ~12 ha of crops) - probably the neighbours' fields
too (six villages within ~4 km); a top-down shot will tell (`run --places` now prints each village's claimed cells).

## D2-050 — 2026-10-05 — The village herd and its herders; herders keep a mixed herd and cull in hard times

**Context.** D2-048 bounded herds by a number (hands x 50 kg of fodder) with no one spending any time on them; long runs lost
their cattle as villages grew (the biggest eaters were culled first and, on grass alone, starved first when the fields took
the pasture). A first try sent one herder from every household with animals: 4.5 hours a day per household, enough to delay a
house in the construction test - not how villages kept animals.
**Decision.** The village's cattle and goats graze together each day on a ground of the day (`Livestock.GroundOf(sid, day)`,
shared by the simulation and the view), with one herder for every 60 head (`HerdersWanted`): children of 8-15 first (a lived
child takes the herd from 6:30 to evening; a grown member only after 9:00 when no child has, for a 4-5 hour stint; COARSE by
turns among the same ages, youths and adults giving 4.5 hours), state `Settlement.Tended`/`HerdersOut` (saved; sim version 14),
`ActivityKind.Tend`, `WhyCode.Herd`. Wolves take a third as many from a herd watched the day before (`WolfRisk`). Culling keeps
a mixed herd (the kind with most to spare first, males before females, the most grown first) and the hands cap counts all a
herd eats (a goat's browse too); in hard times the herders kill into the breeding cores (the village's most numerous kind,
keeping a sire and two females of each kind in the village) rather than let the herd starve.
**Evidence.** Tests (104): the village herd goes out every day with its herders on the day's ground, children taking it where
the village has them (4 of 4 herder-days); no more herders than the herd wants; ACTIVITY 0.97 and COARSE 0.99 herder-days per
herder wanted; a watched herd's wolf risk is 0.35 of an open one's. Seed 42, 200 coarse years: all three kinds held (cattle
139, goats 355, pigs 234 at year 200, no starvation), wolves' toll 4,570 (was 17,402).
**Invalidation.** If herds in crowded villages collapse to their nuclei for good while land is free nearby, or children in
villages with herds never herd, the herding or the culling rules are wrong.

## D2-046 — 2026-10-05 — New kinds spread over millennia; a remnant of a handful dies out (F2 closed)

(Numbered when the tuning began in session 2; recorded here once its 10,000-year runs were in.)
**Context.** On D2-043/044's laws both seeds formed six or seven kinds in a burst (seed 42: three sand wolves on one range by
year ~830), hit the 20-kind cap and then nothing for 9,000 years, with no extinction: sister kinds shared nothing, so every
refuge population split at once, and a continuous field never empties (seed 42 kept 3 warm-water tuna in 7 cells for
millennia, holding a slot).
**Decision.** (1) Sister kinds share territory (a kind's young find room only where its kin of the same template kind leave it);
lineage drift 0.015 (two populations cut off part in ~2,000-4,000 years); the split chance gentle just past the line
(0.002 + 0.03 (d - 1)^2 a tectonic step). (2) **A handful dies out**: at a tectonic step a kind with fewer than 5 animals in all
the world dies out with chance 1 - N/5 (too few to find mates; `Evolution.MinViable`). Not 50 real animals: the fields hold real
numbers since D2-044, and at 7.5 seed 42 lost its seals (~11-15 in all the world, a small but whole population) in the first
century (`run_42m_mv75.*`).
**Evidence.** Planet + ecology, no people, 10,000 years (`tc2/out/f1/run_42k|m.*`, `run_7k|m.*`; the first k runs had people
by a `--nopeople` bug, kept as `run_42k_people.*`): new kinds one or two at a time - seed 42 at 900, 1,200, 2,100-2,400; seed 7
at 1,500, 1,700, 2,000, 2,300-2,500 - the cap by ~2,500; then with the handful law turnover in the middle millennia (seed 42:
19 at 3,200, 20 at 3,600, 19 at 3,800, 20 at 4,200; seed 7: 19, 18, 19, 20 between 2,900 and 4,200) and the cap held to 10,000.
Both seeds keep all thirteen first kinds (seed 42: 12 seals, 47 bears, 60 aurochs; seed 7: 63 seals, 47 bears, 174 aurochs);
the remnants go. Tests: `EvolutionTests.AKindTooFewDiesOutByChanceAThrivingKindNever` (two bears die out within decades, a kind
of thousands never), the speciation test's premise keeps every kind thriving.
**Verdict.** F2's done-when is met (predator-prey cycles, recovery after catastrophes, adaptation over centuries, speciation in
long isolated runs) and **F2 is closed**. Known limits: the 20-kind cap binds after ~2,500 years (turnover needs kinds to
dwindle, which a stable planet rarely asks); the sea's predators start ~10x below their level (the spin-up) and the
miniature's seals are few (food-limited) - an ecology calibration to revisit with the look.

## D2-051 — 2026-10-05 — The WebGL2 fallback draws one shadow cascade

**Context.** The WebGL2 fallback (`?gfx=webgl2`, browsers without WebGPU) had never been measured at village scale: 6-12 fps
in the Web test's village and close views. A layer-by-layer probe (`tc2/scripts/webperf.mjs`, the bridge's `off NAME`) showed
two things: the first seconds at a new view stall (shader and buffer creation in the driver; 5 fps), and in steady state the
village view ran at 37 fps, 60 with shadows off - the shadow pass of the woods in four cascades.
**Decision.** On OpenGLES3 the URP asset draws one cascade over 1.6 x the camera's height (200-3,000 m) instead of four over
3.2 x (WebGPU unchanged). `Shader.WarmupAllShaders` was tried for the stall and removed: it took 36 s at start. Tools: the
bridge toggles layers (`off/on` terrain, trees, people, buildings, shadows, tilt, any view by type) and WebGL2 shadows
(`glshadow N R`); the Web test reads a view's frame rate 4.5 s after framing (past a new view's first second).
**Evidence.** WebGL2 village 37 -> 60 fps (two cascades at half reach gave 49-50), close 43 -> 60; Web test 6/6 on both paths,
bit-identical. **Open**: the first-entry stall on WebGL2 (1-5 s); the fix is a traced shader-variant collection warmed at start
(only the variants the views use), not all variants.

## D2-052 — 2026-10-05 — An abandoned village gives back its land and leaves ruins

**Context.** An abandoned village kept its field claims forever (the land locked against every other village and still drawn
as tended farmland) and vanished from the view the day its last person died - the planet forgot its history at a glance.
**Decision.** At abandonment (`World.ReleaseSettlementLand`) its field cells lose their owner and its ground leaves the
village-cell index; the index (rebuilt on load and after relief changes) holds only inhabited villages, so a loaded world sees
the same land. The view (`WorldObjectsView.Ruins`) draws the houses of its last households as roofless ruins (kit `ruin`)
lowering and greening with the years (house ~300, hut ~80; camps nothing) and its well's ring (~600). Look-dev premise
`unity.mjs shots --abandon YEARS` (everyone of the first village dies, the world goes on).
**Evidence.** `MigrationTests.AnAbandonedVillagesLandGoesBackToTheLand` (no cell keeps its owner, its ground is free, a save
after it loads and lives on identically); shots `tc2/out/shots/ruins1` (40 years after: five house ruins and the well in
pasture, the fields grassed over, young trees). 105 tests.

## D2-053 — 2026-10-05 — Seals haul out together (view)

**Context.** The sea's seals live in the ecology's fields as fractions of an animal per cell (seed 42 holds ~10-20 in all the
world); the view drew each cell's share at its own shore, so the coasts showed a lone seal here and there, or none.
**Decision.** `SeaLifeView.HaulOut(P, c)` (reads only): a shore sea cell whose seals, with its eight neighbours', are the most of
any shore cell within two cells' reach is a haul-out and gathers them - the whole part of that count drawn, the fraction as a
stable keyed chance of the cell (so the field's count is kept on average), at most 18, side by side at the water's edge looking
out to sea; other cells show none. Haul-outs are three cells apart or more, so no seal is drawn twice. Seal shots choose the
haul-out after the world has run and go on to its own local daylight hour; the shot log counts the world's seals and haul-outs.
**Evidence.** Seal shots (`unity.mjs shots --views seals,sealsfar`): a group of seals on one shore instead of scattered singles.
**Found.** Seed 42 holds ~9.5 seals at year 10: the sea's fish fields hold ~1 % of real biomass, and a seal may take at most
a quarter of one cell's fish a step while it needs ~66 kg - wide-ranging hunters must hunt over their range. That is an
ecology calibration (SESSION_HANDOFF next steps), not a view rule.

## D2-054 — 2026-10-05 — The ledger keeps what matters to the living

**Context.** 11 §4 keeps personal events while their subject matters. Two holes made the ledger grow without bound in long
runs with people: `SubjectKept` answered "kept" for any subject that was not a person, so every household's events (homes
built, moves, formations) stayed for ever (38,769 of 95,615 events at year 300 of seed 42); and `BaseImportance` raised every
event's importance by its magnitude, which for a person's events is an age or a count - a death at sixty scored 6.1, over the
keep line of 5, so every adult's death stayed (196,100 of 288,953 events at year 1,000).
**Decision.** (1) A household's events are kept while it has living members and for thirty years after it dissolves, as a
person's are while they live and thirty years after death. (2) Personal kinds (`PersonDied`, `PersonBorn`, `Marriage`,
`HouseholdFormed`, `HouseholdMoved`, `HomeBuilt`) keep their base importance: magnitude raises importance only where it is a
size (cells burnt, deaths, VEI). Importance propagated from descendants (11 §5) still keeps any of them that led somewhere.
**Evidence.** Seed 42, COARSE with people: year 300, 95,615 -> 76,318 -> 69,179 events kept; year 1,000, 780,282 events before
(1) and 288,953 after (1) alone, most of those adult deaths that (2) now lets go. `run` prints the ledger by kind and tier and
why deaths stay.
**Consequence.** The personal part of the ledger follows the living population, not the world's age; what grows with time is
the Historical tier (famines, foundings, wildfires: a few thousand a millennium) and the Local tier's last 200 years.

## D2-055 — 2026-10-05 — Stores free the pages of sparse ids (fires cost what they burn)

**Context.** In the 1,000-year run with people fire steps took 35 % of the time (1.15 ms a step), against 3.9 us a step in the
same 1,000 years without people - the same number of steps (~395,000) and the same fires (planet-only tally: ~400 fires a
century, ~15 cells each, the largest 300-580 cells, ~54 hours). A first fix (40a4ee9: a step's ignitions deduplicated by a
stamp, burnt-out cells compacted out of the burning list once, new ones merged in once) changed nothing measurable. The cause
was the store: fires take their ids from the process counter that the herds (D2-047) and parties share - 887,000 ids in 150
years of seed 42 - so nearly every fire lands in a page of its own; `PagedStore` freed an emptied page only if a later id
already existed, so the newest fire's page was kept when it burnt out, for ever, and every pass over the store (each fire step)
read every kept page row by row.
**Decision.** A page is also freed when ids move past it empty (the next `Add` beyond it), and iteration skips pages with no
live rows whole. Allocation and iteration order are unchanged (ascending ids); `PagesHeld` reports pages for diagnostics. The
fire step's batching stays (it removes list work that grows with a big fire's square).
**Evidence.** Seed 42, 150 coarse years with people: 379 fire pages held -> 1, managed heap 65 -> 41 MB, fire step 131 -> 2.6 us,
the run 29.8 -> 23.0 s, state hash identical (a1c1a40d08f49a31). Over 1,000 years the pages would have reached thousands
(hundreds of MB, fatal in a browser's heap). Test `StoreTests.ASparseStoreHoldsOnlyThePagesOfItsLiveRows`; `run` prints process
ids used, fire pages held, the managed heap and the fires ended by century.
**Note.** Process ids are an `int`: ~6,000 a year at 8 villages, ~30,000 at 40 - about 300 million in 10,000 years of a full
world, within range but worth watching (herds churn ids as they form and dissolve each day).

## D2-056 — 2026-10-06 — The household's own work and warmth: meals made, water carried, the hearth fed, a roof against the cold

**Context.** The F3 review followed people through their days (new tools `follow`, `timeuse`): grown people rested at home for
most of the day once the village's fields and woodstore were seen to (seed 42, sowing: 7.3 h idle and 3.6 h on the square to
4.9 h of work), children of 6-11 played ten hours, and two things the view showed meant nothing to the simulation - a house (a
household in a hide tent through a frost lived as well as one in a timber house) and chimney smoke (no hearth burnt any wood:
the woodstore went only into building). 07 §3's Household generator (water, firewood, food from the store, cooking) and the
Body generator's warmth were never built.
**Decision.** (1) **Meals are made ready**: the larder holds stores; meals are eaten from `Household.Ready`, food ground and
cooked by `Cook` (`ActivityKind.Cook`, `WhyCode.MealsToMake`) at ~1 FU an hour of a grown person's work (a saddle quern grinds
~1 kg of meal an hour, then the cooking; a child of 8-11 half as fast; half again with no wood), from the larder or else grain
fetched from the village's store, burning 0.05 wood units (~1.2 kg) a FU. A hungry person of eight or more with nothing cooked
makes the meal first (a meal is at least 0.4 of their portion, not scraps); a member at home takes the next meals when less
than one is cooked (urgency 0.5-0.85). (2) **Water is carried**: the household's pots (`Household.Water`) empty with the day's
food (8 l a grown person, 5 a child, 1.5 an infant: drink, cooking, washing) and are filled at the well, or the stream before
one is dug - a grown person's pot 15 l, a child's 6; the coin-toss water trip is gone. (3) **The hearth and the roof**: each
day (`SettlementSystem.Hearths`, both resolutions) the village's hearths burn its woodstore against the day's mean outdoor
temperature: a roof keeps 4 / 7 / 9 C over the open air (camp, hut, house), a fed fire adds what the home wants to reach 14 C
(up to 8 C in a camp, 14 under a roof), ~1 kg of wood a degree a day; a short store is shared alike (`Household.Indoor`).
Below 8 C indoors the cold takes health by the degree (0.012 a day; x2.5 infants, x1.5 under five, x1.8 over 65), which raises
the day's risk of death - the part of the risk the cold added is the cold's - and a body it wears to nothing dies of exposure
(`PersonDied` why 9, caused by the village's `ColdHomes` spell, Local). `Settlement.FuelDay` is what the hearths and the
cooking burn a day; wood is cut first when fewer than six days of it are in store (ACTIVITY urgency up to 0.85; COARSE hours
before any other work). (4) **Children's chores**: the herd first (D2-050), then water when the pots are low (from six) and the
quern when nothing is cooked (from eight), then play. (5) **COARSE** charges the same work to the same people as a share of
the day before other work (`AgentSystem.Chores`: a child of 6-11 a pot every other day, of 8-11 up to 1.5 h at the quern, the
grown members the rest; trips 0.2 h); the ordinary-day wild factor refitted 0.6 -> 0.9 (water and meals were in it). A wood
unit is ~25 kg (a load cut and carried in about an hour). The view's chimney smoke reads the simulation: someone cooking, or
the hearth's fire today. Sim version 15.
**Evidence.** `HouseholdWorkTests` (111 tests): hungry people with nothing cooked make the meal first (15 of 15 fed within six
hours, 1.0 FU an hour); children and grown people carry water when the pots are low (every home watered within four hours);
a roof and a fire keep the cold out (twelve days at -12 C: houses with wood keep health 1.00 and lose nobody, camps without
fall to 0.38 - under five 0.02 - and three die of the cold); COARSE and ACTIVITY keep house alike (0.75 / 0.77 hours and
0.74 / 0.75 FU made ready per person-day, water 0.070 / 0.087 hours); the wild-food fit holds (0.82 / 0.92 hours,
0.90 / 0.94 FU per adult-day); the herd still goes out with children first. Time use, seed 42, sowing: grown people cook 1.3 h
a day, cut wood 0.6, carry water 0.1, children of 6-11 herd 2.9 h, help at the quern 0.7, carry water 0.1. 200 coarse years:
seed 42 3,492 people in 15 places as before (warm villages: no cold); seed 1 (villages at 6-10 C) 1,206 people in 6, 1.08 hours
a FU made ready, no cold homes - houses with wood stay warm at -2 C. The hearths cost 2.8 us a village-day.
**Open.** Grown people still idle 7-9 hours a day outside sowing and harvest: crafts and the making and mending of clothes and
gear (textiles took farming households hours every day), hay for the animals' winter and weeding labour are the slack's
missing work; minding infants and toddlers (07 §2's carer) is not yet a task.

## D2-057 — 2026-10-06 — No arithmetic typed float in the core; a lint keeps it out

**Context.** Determinism across dotnet, Mono and IL2CPP is settled architecture (D2-003; 03 §4: "double in all sim logic,
storage may be float"). The Unity editor's Mono had quietly parted from dotnet: the cross-runtime probe gave other hashes from
the newly generated world on (dmath and rng equal). A new probe, `CrossRuntime.Parts()` (`headless.mjs hashtest --parts`; the
editor's hashtest writes it first), hashed the world part by part: elevation, crust, moisture and water agreed; the climate's
zonal temperatures, each cell's mean temperature, ice, vegetation, animals and the settlements' placing did not. The cause: float
arithmetic - `P.Elev[c] + P.Ice[c]` in the climate, `p.ToX - p.FromX` in people's positions, 63 operations in 15 files. CoreCLR
and IL2CPP round each float step to single precision; Mono may carry it at double precision (and keep float locals in double
registers), so a sum widened into a double expression differs in its last bits, and the world grows apart from there.
**Decision.** Floats are for storage only: every operation is done in double and cast to float when stored. The 63 operations
were rewritten so (compound updates of stored floats too, for one simple rule). `tc2/tools/floatcheck` (Roslyn from the editor's
SDK, semantic model over TC2.Core) lists every arithmetic operation typed float; `headless.mjs test` runs it first and fails on
any (`headless.mjs floatcheck` alone).
**Evidence.** After the rewrite the editor's probe equals dotnet's from generation through day three (gen e3a59d5f8bde854d, every
part equal); 111 tests green. The world's state hash changed (operations that fed double expressions now keep their full
precision).

## D2-058 — 2026-10-06 — Goods made and mended, a day's labour, weeding that keeps pace

**Context.** After D2-056, followed grown people still rested 7-9 hours a day outside sowing and harvest (seed 42, growing
season: 8.7 h idle, 4.1 h on the square, ~3 h of work). Farming households spent hours every day on textiles (spinning above
all) and on making and mending tools, vessels, baskets and nets, and the growing season's weeding was heavy work (hand weeding
of a cereal took 50-100 hours a hectare a season) where the model asked 8 hours a hectare.
**Decision.** (1) **Goods** (`Core/Places/Goods.cs`): a household holds `Household.Gear`, person-equivalents of clothes, tools
and vessels (1 = one person clothed and equipped); each member wears through 1/300 of a person's goods a day; making one
person's goods takes ~250 hours (`ActivityKind.Craft`, `WhyCode.GoodsWorn`, at home; a child of 8-11 at half pace); households
keep 1.2 a head when they can. Worn goods matter: the chill line rises up to 6 C in rags, and hands work at 0.7-1.0 of their
pace with worn tools (fields, wood, building, the wild's catch). Goods go with a person into a new household as the larder's
share does; a newborn brings none. Materials are taken as found with the work - F4's chains will ask for fibre, hides, clay
and wood. (2) **A day's labour**: `Person.Worked` counts the hours of work since waking (sleep resets it); the slack's work
(making and mending) loses its pull past five hours of work (a fifth of it past eight), so rest and company keep their place;
COARSE fills making into the rest of a 9.5-hour labour day after its chores, herding and other work (`LabourDay`, fitted to the
lived day). (3) **Weeding**: the growing stage asks ~24 hours a hectare (was 8), and the crop thrives while its weeding keeps
pace with the season so far (the tending factor reads the work against the need to date, so the larger need costs no yield
where it is done). (4) The inspector gives rest its why (a long day's work behind them; the harvest in, the crop weeded and
nothing at home pressing). Sim version 16.
**Evidence.** `GoodsTests` (115 tests): worn goods are made again by the household's own hands (0.30 -> 0.41 a head in four
days, 250 hours each); worn clothes let the cold into a warm house (ten days at -12 C in houses with wood: clothed health 1.00,
in rags 0.92); worn tools work at 0.70 of good ones; COARSE and ACTIVITY make alike from worn goods (2.86 / 2.60 hours a
person-day; 0.71 / 0.69 a head after twelve days; lived making while goods are short: 8-11 0.7 h, 12-59 5.2, 60+ 5.4 a day).
A whole year of seed 42 (`timeuse --days 48`): grown people work ~6.5 hours a day (making 2.1, fields 1.8, cooking 1.3, hunting
and fishing 0.9, wood, water and herding 0.3) with ~9 hours of rest and company; youths ~8, elders ~5.4, children of 6-11 ~3
(herding most) - inside the ranges of time-allocation studies of farming peoples (grown people 6-9 h, children of 7-11 2-5 h),
peaking at sowing and harvest. 200 coarse years: seed 42 3,570 people in 14 places (3,492 in 15 before), no hunger; seed 1
1,084 in 6 (1,206), land-bound famines as before (1,001 hunger deaths, 50 famines); making 250 hours a good in both.

## D2-059 — 2026-10-06 — Homes stand on their own; a household that dies out leaves its home and its goods to its kin

**Context.** A household's shelter was a level on the household (camp, hut, house), so a home existed only while its household
did: newlyweds always camped on a new lot and raised a hut beside empty houses, and a household moving away took its house
with it. Worse, a household whose last member died was never dissolved: it lingered in its village's list with no members -
its house still drawn, never taken, its hearth still burning wood (D2-056), its animals owned by no one - and every daily loop
over households grew with the dead.
**Decision.** (1) A hut or house is a `Building` on its lot (store `World.Buildings`, ids of `EntityKind.Building`, per-village
index), raised by a household (`Homes.Raised` when its first stage completes, and at worldgen), lived in, left, taken again;
`Household.Building` points to it (0: a camp) and the shelter level follows it. (2) A household leaves its home when it dies out
or moves away (`Homes.Leave`): the building stands empty. Decay is read from the time it was left (`Homes.Condition`): an empty
hut falls in after ~20 years, a house after ~60; past 0.35 nobody moves in (the roof is gone); its ruin then fades (~80 / ~300
years, D2-052) and leaves the table at the yearly housekeeping. A home lived in is kept sound by its people (upkeep implicit).
(3) A household without a home - a married couple, families arriving from elsewhere, a camp already in the village (checked
daily) - first takes the best standing empty home of its village, a house before a hut, the soundest first (`Homes.TakeEmpty`;
history `HomeTaken`, whose it was); only where none stands does it camp on a new lot and raise its own. (4) The last death of a
household dissolves it (`SettlementSystem.DieOut`): its larder, cooked food, goods and animals go to its nearest kin in the
village (a child's household, else a parent's, else a sibling's; else the household with the fewest animals), and with nobody
left the animals go wild. (5) The view draws buildings (lived in, standing empty and weathering, roofless, fallen and sinking);
D2-052's per-household ruins are gone - an abandoned village's homes simply decay. Sim version 17.
**Evidence.** `HomesTests` (118 tests): a home outlives the household that lived in it (its larder and its six animals to the
heir's household); a couple takes an empty home before raising their own (no new lot; "The Barnkreilds made their home in the
house the Serndres had left in Vulstead"); an empty home falls in and fades while a lived-in one stands, one past mending is
not taken, a gone ruin leaves the table. 200 coarse years: seed 42 3,588 people in 15 places (1,078 homes lived in, 62 empty,
1,340 empty homes taken, 566 households died out); seed 1 1,120 in 6 (359 lived in, 116 empty, 16 fallen); a village-day 127 us.
Shots `tc2/out/shots/homes40|50|100`: a village whose people all died stands empty at 40 years, roofless at 50, fallen at 100.
Mono equals dotnet.

## D2-060 — 2026-10-06 — F3 closed: villages of persistent people live on their own

**Context.** F3's done-when: "a village lives 100 years untouched without scripted help, every visible action is the
simulation, and following any person shows a believable life" - in headless runs and in the Web build (17).
**Evidence.** (1) *Untouched centuries*: 200 coarse years on the final build - seed 42 grows from 145 people in 6 villages to
3,588 in 15, with no hunger; seed 1 (cool, land-bound) keeps its 6 villages at ~1,100 people through recurring famines;
1,000 years of seed 42 (before D2-056) saturated at 12,000-14,800 people in 37-42 places without collapse. No event in any of
them is scripted: founding, famine, moving, dying out, homes taken and left all come from the same laws. (2) *The visible
action is the simulation*: what people are seen doing is their activity step (fields, wood, building, herding, hunting,
fishing from boats, gathering, cooking, carrying water, making and mending, company, play, scouting, moving house); homes
are the simulation's buildings (lived in, standing empty, roofless, fallen); chimney smoke is someone cooking or a hearth
burning against the cold; herds, livestock and pens are the simulation's animals; fields are claimed cells; seals are the
fields' counts gathered at a haul-out; haystacks read the granary after the harvest. (3) *Believable lives*: a followed day
(`follow`) is meals made and eaten, water carried, work by the season and the household's need, rest with its reason, the
evening on the square; a year's time use (`timeuse`) puts grown people at ~6.5 h of work a day and children of 6-11 at ~3 -
inside time-allocation studies of farming peoples; whole lives from the ledger (`follow --life`) are birth, marriage at
20-23, six or seven children three or four years apart, a home raised or taken from those who left, death in the fifties or
earlier. (4) *The Web build*: 6/6, bit-identical with headless and the editor's Mono (`tc2/out/webtest/s2webd`).
**Verdict.** F3 is closed. Carried forward: minding infants and toddlers (07 §2's carer: babies stay at home and are fed
there; nursing mothers stay near), hay and winter fodder, the materials of goods and specialised crafts (F4), re-roofing and
quarrying ruins and the upkeep of homes, threshing, famine relief between villages (F5).

## D2-061 — 2026-10-06 — Catch-up at scale: Living holds; Ancient and Deep time wait for an aggregate layer of people

**Context.** The handoff named offline catch-up at scale the biggest open risk (a saturated world costs ~1 s a world year). D2-055
to D2-059 added a great deal to a person's day (meals, water, warmth, goods, homes).
**Measured.** A saturated world (`bench --name poc --settlements 40 --founders 300 --coarse`: 13,316 people in 50 places) lives
1.26 world years a second headless, 1.24 us a person-day - unchanged by D2-056 to D2-059 (1.28 / 1.22 before); the browser kept
pace with headless before (1.37 years a second). Profile (`run --profile`): the village day is 71 % of the time (252 us at ~266
people: the COARSE day 105 - its household chores 21, the meals ~19, each person's work ~0.3 us -, the survey 44 of which herds
34, demography 43, livestock 29), the planet's ticks ~15 %; no single part dominates. So a day away in Living (~96 years) at
saturation takes ~75 s, which the catch-up screen (the world's date, the planet, the history as it is made) carries; Ancient
(~600 years a day away) would take ~8 minutes and Deep time (~4,800) about an hour - and both grow with the world's people
once seafaring fills other lands (F5).
**Decision.** Living stays as it is (D2-023: COARSE for many lives). Ancient and Deep time need an aggregate layer of people - a
village's year lived as one step, its people still individuals (births, marriages, deaths, homes) - fitted to COARSE as COARSE
was to ACTIVITY. It is built once the people model is settled (after F4 and F5), not now: every slice since D2-042 changed a
person's day, and a year-step fitted today would be refitted at each. Until then Ancient and Deep time are lived at COARSE and
are slow at scale. The COARSE day's household chores are timed in the profiler (`coarse.chores`).

## D2-062 — 2026-10-06 — Techniques carried by people: copied from the best, found by practice, lost by small groups (F4 slice 1)

**Context.** F4 (09, 17): technology is knowledge carried by real people - no tree to unlock; techniques spread by contact and
die with their last practitioners; done when "a technique spreads by contact and is lost in an isolated shrinking group in
tests". The research base: Henrich (2004) - learners copy the most skilled model imperfectly, so a population keeps a technique
only if enough learners copy (the best of many copies may surpass the master, the best of a few falls short); Kline and Boyd
(2010) - toolkits follow the size of the connected population; Arthur and Polak (2006) - invention as combination in practice.
**Decision.** (1) Three techniques with effects in the living model (`Core/Knowledge/Techniques.cs`): fishing nets (a fisher's
catch up to 1.6x), the warp-weighted loom (goods made up to 1.6x faster) and the rotary quern (a household with someone who
can dress its stones grinds up to 1.8x faster) - simple to complex: copy loss alpha 0.15 / 0.20 / 0.30 against spread beta
0.08 / 0.07 / 0.06, so a village keeps them with ~4 / ~10 / ~80 learners (exp(alpha/beta - 0.58)). Each person carries a
proficiency 0..1 for each (`Person.KNets/KLoom/KQuern`, saved). (2) Each day (`Learning.Day`, the village's step at both
resolutions) everyone of eight or more copies the village's best practitioner toward a target - the best less the technique's
loss plus their own scatter, a standard Gumbel draw fixed by their id (Henrich's epsilon) - the young (8-19) closing 1 % of the
gap a day, grown people a quarter of that. Masters keep their skill until they die; a village's best falls only as the masters'
generation goes. (3) A village knows a technique while a competent practitioner (0.5) lives there; the day it first does
(`TechniqueKnown`, Local, who taught it) and the day its best falls under 0.25 (`TechniqueLost`, Historical) are history. (4)
Practice may find what nobody in the village knows (`Learning.Practice`): fishing hours to nets, making and mending to the loom,
grinding to the quern where rock shows near the village (a cell within two rings 40 m above or below a neighbour) - a keyed chance
an hour by curiosity (2e-5 / 4e-7 / 2e-7); the finder starts at 0.4 (`TechniqueFound`, Historical). (5) Knowledge travels with
whoever moves (brides, households, parties) and takes root where they settle. (6) Founders know nets in 80 % of villages by the
sea and 20 % inland, the loom in 60 %, the quern in 35 % (keyed by village). Sim version 18.
**Evidence.** `KnowledgeTests` (121 tests): the law alone - from 0.8, six generations of six learners leave the quern at 0.06,
of three hundred at 1.00, and nets hold among six; a weaver who marries into a village without the loom - "The people of
Helerby now weave on the loom (taught by Dronaa Limvaend)" - and twelve years on eleven of its people weave; one master of the
quern (0.6) in an isolated village of 14 - lost after 150 years (0.12; history tells it) - and in a village of ~150 founders kept
and bettered (0.97 after 60 years). In seed 42's world the six villages, a few km apart and marrying among themselves, all know
the three by year 50 and have them at mastery by year 150 (the connected population is large); none found or lost in 200 years.
The learning step costs ~20 us a village-day. Editor, dotnet and headless agree (gen 0c2e8c8cc4c38bad).
**Next (F4 slice 2).** Materials from the planet: clay where rivers and lakes lay it, stone where rock shows, ore from the crust
(D2-058's goods ask for fibre and hides; pottery, the ard with oxen, metals); techniques with prerequisites among themselves.

## D2-063 — 2026-10-06 — Materials from the planet; pottery and the ard (F4 slice 2)

**Decision.** (1) Goods (D2-058) draw materials from the village's store: 2.5 a person's goods, kg of flax fibre or hides counting
two (`Goods.Materials`); what the store cannot give is improvised at 0.4 pace (bark, grass, reeds). Fibre comes with the harvest
(1 kg a hectare of the fields a year, by the harvest's success: a corner sown with flax); hides with the hunt's kills (one an
animal of 60 kg or more, less for smaller game) and the slaughter (cattle 1, goats and pigs 0.5). Stored hides rot (~20 % a year),
fibre less (~5 %). Founders start with 1.5 kg fibre and 0.5 hides a head. (2) Two techniques join D2-062: pottery - found by
cooks where clay is near (a stream or a lake within 1.2 km: ~60 % of villages), a village's pots keep its grain (spoilage down to
0.65) and a household's cooking burns down to 0.8 of the wood; founders know it in 75 % of villages near clay, 20 % else - and the
ard - found by field workers of a household with a grown ox, up to 1.8x their field work while the household keeps one;
founders know it in 15 % of villages keeping cattle. The quern's stone is rock showing within two rings (most villages). Sim
version 19.
**Evidence.** `MaterialsTests` (125 tests): the store's law (fibre first, a hide as two, improvised without); a year with
nothing made fills a village's store from its harvest's flax (27 -> 33 kg from 9 ha) and its hunters' and herders' hides
(9 -> 11 after spoilage); pottery is never found far from clay however long the cooking ("Krisluth Matham of Farvounstead found
how to fire pots of clay" near it), the ard never without an ox; the effect laws. 200 coarse years: seed 42 knows nets, the loom,
the quern and pottery everywhere (its founders' and spread by marriage) and never the ard (few households keep a grown ox: none
found it); its stores hold 10-125 kg of fibre; seed 1 (cold, famine-struck) knows all five but its failed harvests leave many
villages without fibre - their goods improvised, their clothes thinner in the cold. Mono equals dotnet (gen 02b1010e12e4a49d
before the clay and spoilage tuning).

## D2-064 — 2026-10-06 — Copper from the ground: a technique that needs a technique (F4 slice 3)

**Decision.** (1) Copper ore shows where rock shows within 3 km of a volcano, living or gone quiet (arcs lay copper:
`Learning.Ore`) - near some villages in some worlds (seeds 2, 7, 11: two to four villages of six), none in others. (2) The
technique "smelt copper and work it" (`Tech.Metal`: alpha 0.28, beta 0.065 - kept by ~40 learners) is found only by a maker who
already fires pots (a furnace, a crucible: proficiency 0.5 in pottery) where the ore shows. (3) A maker who can work copper, in a
village with ore near and short of metal tools, gives half their making to the furnace and the anvil (`AgentSystem.Smith`,
both resolutions): 0.08 kg an hour at full proficiency into the village's `Metal`, ~10 kg of wood a kg as charcoal out of its
woodstore. (4) A toolkit is ~0.5 kg a head; the share of its people a village's metal equips quickens all hands up to 1.25x
(fields, wood, building, the wild: `Tools`) and slows the wear of goods to 0.75; metal is lost and broken ~3 % a year. (5)
Founders know it in 25 % of villages with ore. Knowledge travels by marriage as any technique; metal itself does not move
until trade (F5). Sim version 20.
**Evidence.** `MetalTests` (128 tests): the laws; copper never found without pots or without ore, found by a potter by the ore
("Fel Mogrund of Staernby found how to smelt copper and work it"); a village of smiths equips its 17 people in a year (9 kg)
and its hands quicken 1.00 -> 1.25. Seed 7, 200 coarse years with no premise: a small village loses the loom in year 8;
copper is found at Haesmere in year 147 and again, independently, at Taekdal in 151; by year 200 every village knows it (by
marriage) but only the two with ore near hold metal (114.5 and 99 kg - full toolkits); 2,554 kg smelted in all. Mono equals
dotnet (gen fba9c10ad37a945f).

## D2-065 — 2026-10-06 — F4 closed: technology is knowledge carried by people

**Context.** F4's done-when: "a technique spreads by contact and is lost in an isolated shrinking group in tests" (17).
**Evidence.** `KnowledgeTests`: a bride brings the loom to a village without it and its own people weave; one master of the
rotary quern is lost in an isolated village of 14 within 150 years and kept and bettered in one of ~150 (Henrich's mechanism,
from the copying law alone). Beyond it: six techniques with prerequisites in the ground (rock, clay, ore), in the household (an
ox) and in other techniques (pots before copper); discovery by practice; loss as history; materials from the harvest, the hunt
and the slaughter (D2-063); copper and its charcoal (D2-064); all at both resolutions, Mono equal to dotnet. In free worlds the
history writes itself: losses in small villages, independent discoveries, knowledge outrunning the ore it needs.
**Verdict.** F4 is closed. Carried forward: iron (bog iron, the bloomery) and bronze (tin, rarely near: trade), knowledge of
places, apprenticeship as a relationship, the hint a traded object carries (F5's trade), techniques of building (masonry) and
war (F6).

## D2-066 — 2026-10-06 — Sickness among persistent people: carried by whoever walks, traceable to the animals (F5 slice 1)

**Context.** F5 (17): pathogens with SEIR in settlements and parties, transmission along routes and at gatherings, mutation;
done when an epidemic jumps along trade and travel and is traceable. 02's research: metapopulation SEIR with travel coupling,
pathogens as lineages that mutate (the virulence-transmission trade-off); in small populations crowd sicknesses burn out (the
critical community size of measles is a quarter of a million people), so a miniature of a few thousand sees epidemics come and go.
**Decision.** (1) Pathogens are lineages (`Core/Health/Disease.cs`, store `World.Pathogens`): transmissibility (infecting contacts a
day), latent and infectious days, virulence, years of immunity, an antigen; new ones come over from animals (a keyed chance a
village-day, more with its herds and its crowding: ~1 in 20 villages a century), with traits drawn from the ranges of farming
peoples' sicknesses (0.15-1.5 contacts a day, 2-12 latent and 4-16 infectious days - shorter the deadlier -, virulence
0.002-0.4, immunity for life or 2-42 years); they mutate as they spread (1 in 2,000 infections; a tenth of mutations shift the
antigen past old immunity); people name them ("the shaking spots"). (2) Each person carries their own sickness (`Person.Sick`,
latent then infectious, `SickCause` the epidemic they caught it in) and two remembered immunities. Each day in each village
(`Disease.Day`, both resolutions) the infectious at home infect the susceptible at home at beta x I / N; at an infection's end
the body dies by virulence x frailty (infants, the small, the old, the hungry most: `PersonDied` why 10, "of the cattle cough")
or recovers immune. The infectious keep to their beds (`ActivityKind.Ill`; COARSE: no work, no chores). (3) Between villages: a
lived visitor catches what their host village's infectious have, over the hours of the stay (`Disease.Arrive`), and carries it
home; brides and households moving carry theirs; a COARSE day sends a village's healthy grown people visiting its neighbours
within 6 km at the lived rate (0.025 a day each, measured: `TheCoarseVisitRateIsTheLivedOne`) and some bring it home. (4)
History: `PathogenAppeared` (from its animals), `EpidemicArrived` (it broke out where it appeared; elsewhere it came with a
carrier - its cause the epidemic the carrier caught it in), `EpidemicEnded` (days, the dead). Every arrival leads back by its
causes to the animals. Sim version 21.
**Evidence.** `DiseaseTests` (132 tests): a sickness runs its course in a village (18 of 20 caught it, the recovered immune,
"The shaking sweat left Krendhope after 22 days"); people of a village visiting a stricken one carry it home - "The shaking flux
came to Landton with Nuse Baka <- broke out in Naerkrornby, Dornbreith Vanvaern the first to fall ill <- A new sickness, the
shaking flux, appeared in Naerkrornby"; a COARSE world carries it to two neighbours at the lived rate; lived visits 0.027 a
grown person-day against COARSE's 0.025. Seed 42, 200 coarse years with no premise: four sicknesses from the animals (years 129,
149, 158, 169), fifteen village epidemics jumping between the six villages within a year or two, 4,443 infections, 248 deaths.
Mono equals dotnet (gen 28858362a12e24c7).
**Next (F5 slice 2).** Trade by want between villages (metal from the ore villages, salt from the coast, grain to a famine:
relief), walked by traders who carry sickness too; gatherings.

## D2-067 — 2026-10-06 — Trade by want and relief: what one village spares and another lacks, carried by people (F5 slice 2)

**Context.** 08 §2: exchange by want (worth = what one lacks), gifts and relief between settlements; F5 (17): trade by want,
famine coping sequences, an epidemic jumping along trade. Research: social storage - neighbours buffering bad years by giving
and exchanging food (Halstead & O'Shea 1989); balanced reciprocity between neighbouring communities, generalised giving when
they starve (Sahlins 1972). Under D2-064 copper stayed where the ore is: only the villages near it held metal.
**Decision.** (1) Each village values each good by its own want (`Core/Places/Trade.cs`, `Trade.Value`): grain 1-4 by how far
its store falls short of 60 days of food; metal 0.5-6.5 by the share of its people its tools leave unequipped; fibre 0.5-2.5
and hides 1-4 by its store a head. It spares what it holds beyond 60 days of grain, full toolkits, 2 kg of fibre and a hide a
head. (2) A deal (`Trade.Plan`): a load (25 kg; a FU of grain 0.75 kg, a hide 3 kg) of what a village spares and a neighbour
within a day's walk (6 km) values more, for what the neighbour spares and the village values more, priced at the middle of
the two villages' values - both gain or nobody goes; worth a walk only at a gain of 4. (3) Lived traders: in the morning a
healthy grown person (18-55, not pregnant, nursing or sick) at a chance of 0.004 x (0.5 + boldness) with a deal to be had sets
out with the load from the village's store (`Person.Cargo`), exchanges it in the other village as the deal stands when they
arrive (nothing wanted for it there: it comes home again) and brings the return load to the store. Traders are visitors:
they catch what their hosts have and carry it home (D2-066). The inspector: "walking to Kubroford with 10.7 kg of copper to
trade: Gouthorpe spares it and they want it". (4) A COARSE day makes the same deals as trips at the lived rate (0.01 trips a
healthy grown person-day with a deal: lived 0.0083), each trader 3 hours in the other village for the sickness. (5) Relief: a
village with grain to spare sends each neighbour within 6 km in famine (a hunger death within a quarter year) a twentieth of
its spare a day, at most three days of their need, while their store holds less than 30 days - relief feeds the hungry, it
does not fill a granary (nor reach a village whose dead starved beside a full one); history tells it once a famine
(`ReliefSent`, in response to the famine): "In the famine Neldthorpe sent grain to Lortharndal". (6) The COARSE visit rate
refit on five test worlds (0.032 lived, pooled over 147 visits): 0.03 (D2-066's 0.027 was one small sample). (7) Goods are never
lost: a trader the change to COARSE puts home unloads there (the player's speed-up can come mid-trip). Sim version 22.
**Evidence.** `TradeTests` (138 tests): a deal follows want both ways (a village with metal to spare and no fibre gives 10.7 kg
of copper for fibre, gain 114 in the two villages' values); a trader carried it (metal A 48.0 -> 37.3 kg, B 0 -> 10.7; fibre
home 0 -> 24.9 kg); relief reached a famine within a day and was told once; **an epidemic jumped along a trade route and is
traceable**: "The shaking flux came to Gouthorpe with Krukfald Haerfas <- broke out in Kubroford <- appeared in Kubroford";
lived trading trips 0.0083 against COARSE's 0.01; a load on the road when the world goes COARSE reaches the store (10.7 kg; before
the fix it stayed on the trader, to be overwritten on their next trip). Seed 7, 200 coarse years with no premise (`tc2/out/trade/trade7_200.txt`):
4,383 people in 18 places; 521 trades, 509 kg of copper carried from the ore villages - **all eight villages without ore hold
metal (6.7-99 kg)**; 16,504 FU of relief in four famines (5 hunger deaths in 200 years; 65,581 FU before the 30-day cap);
sickness 31 epidemics, 297 deaths. Seed 42 (no ore, no famine): 22 trades in 200 years, no metal, no relief. Trade costs 1.5 us a
village-day. Mono equals dotnet (gen dd0c2fd7a147d1d2).

## D2-068 — 2026-10-06 — F5 closed: exchange, movement and disease

**Context.** F5's done-when: "an epidemic jumps along trade and travel and is traceable; migrations found new places" (17).
**Evidence.** Travel: a visitor carries a village's epidemic home and the arrival leads back by its causes to the epidemic it
was caught in and to the animals (`DiseaseTests`, D2-066). Trade: a trader brings it home from the village they traded with,
traceable the same way (`TradeTests`, D2-067). Migrations found new places: households leave pressed villages after the
harvest and found at places their scouts know (D2-045, `MigrationTests`; seed 42 6 -> 35 villages in 300 years). The famine
coping sequence is lived: rations cut (D2-042), the wild (D2-042), the herd eaten down to its core (D2-048), relief from
neighbours (D2-067), leaving (D2-045). In free worlds sicknesses come from the animals, jump between villages along visits,
marriages and trade, and burn out; copper spreads by trade from the villages near the ore.
**Verdict.** F5 is closed. Carried forward: boats between coasts and seafaring (the sea's routes), roads worn by use,
gatherings and fairs (where epidemics jump and marriages are made), salt from the coast, trade passed on through villages and
credit, relief weighed by kinship and trust (F6's deeds), and the hint a traded object carries (copying a technique from it).

## D2-069 — 2026-10-06 — Deeds known per village; taking by need (F6 slice 1)

**Context.** F6 (17, 10): society, conflict and polities built from shared primitives - deeds known per settlement - never war
flags, hostility meters or scripted retaliation (10 §1; TC1's `docs/v3/50_PRIMITIVES.md` and R6). Research (TC1
`docs/v3/40_RESEARCH_CONFLICT_PLANET.md` §A): people take from neighbours when hungry (Turkana raiders name hunger first;
Hanawalt's medieval thefts rose with the price of grain, mostly food from neighbours, most in winter when the stores were full);
ties restrain (Gluckman); a wrong is remembered as a specific deed - settled, answered or forgotten (Boehm, Kelly); refused help
and harm cost trust (Halstead & O'Shea). The plan for F6 is 10 §6.
**Decision.** (1) Deeds (`Core/Society/Deeds.cs`, store `World.Deeds`): who did what to whom - the doer and their village, the
household or village that suffered or gained, the good and its worth in FU, its history event. Each village keeps the deeds it
knows and how (its own people's, seen, found with the doer unknown; told comes with news, slice 3), sorted by id (`DeedBook`).
Grievance and gratitude are sums read when a choice needs them - the worth of the harms (help) a village knows another's
people did (gave) it, halved every 2 (4) years - never stored as scores; a deed whose weight has faded below half a day's food is
forgotten by every village (yearly). (2) Taking by need (`Core/Society/Conflict.cs`): a grown person (16-55, health above 0.4,
not pregnant, nursing or sick) whose last day fed them less than half their need (the body's day, kept the same at both
resolutions: a lived meal-time flag clears at the first bite) weighs, going to bed (once a night, before 2 am), a load of grain
(25 kg, 33 FU) from a neighbour within 6 km whose store holds more than a month of its own need: a chance of 0.03 x (0.4 + 1.2
boldness) x (0.5 + how near their body is to starving), times how freely they would go - (1 - restraint) x (1 - the chance of
being seen). Restraint: kin living there (parents, partner, children, brothers and sisters) 0.85, plus up to half for help that
village gave (100 FU), less a wrong it did them (30 FU lifts it all: taking back is not stealing to the taker). Seen: each home
within 50 m of the store may wake, 1 - e^(-0.05 n), at most 0.8. Lived, they walk there in the dark, take or are seen and run,
and walk home with the load to their household's larder (`Person.CargoTo`); a COARSE night makes the same choice with the same
chances. (3) Seen: a deed (worth half the load) the store's village knows by sight - "Krukfald Haerfas of Gouthorpe was seen at
the store of Kubroford in the night and ran, leaving the grain". Unseen: a deed their own village knows; the store's village
finds the loss when the load was 2 % of the store or more, the doer unknown - "Krukfald Haerfas of Gouthorpe took 33 days' grain
from the store of Kubroford in the night", caused by the hunger at home. (4) What deeds change now: two villages either of which
holds a grudge (more than 10 FU of known harm) against the other do not trade; a village sends no relief to those who wronged
it; the next taker's restraint. One seen taker estranges two villages for about a year and a half. (5) The store's place
(`Construction.GranarySpot`) is one rule for the builders, the takers and the view. Sim version 23.
**Evidence.** `TakingTests` (145 tests): unseen, a load leaves a full store for the taker's larder, their village knows it as its
own and the store's village does not know who; seen, the grain stays, Kubroford's grievance against Gouthorpe is 16.7 FU - no
trade either way and no relief in a famine (the same famine without the wrong: Kubroford sends grain) - and two years on 8.2;
restraint 0.00 at a stranger's store, 0.85 with kin there, 0.50 with help received, 0.00 once a wrong is known; seen 0.00 with no
home near the store, 0.30 with 7 round it; a lived taker "walking to Kubroford in the dark to take grain from its store: there is
nothing left to eat at home, and its store is full", then "hurrying home to Gouthorpe in the dark with 33 days' grain taken
from a neighbour's store"; a starving month in four worlds takes 18 times lived and 24 at COARSE (a lived night weighs at bedtime
with the body of the night before, a COARSE night with the day's account; instrumented, the expected takings were 22.6 lived and
22.4 COARSE over five worlds); an untouched month takes nothing. Seed 7, 200 coarse years (`tc2/out/society/take7_200.txt`):
11 loads taken, 3 takers seen, 31 deeds kept, five famines met by relief. Seed 42, 400 coarse years (`take42_400.txt`): 289
loads taken (9,633 FU), 160 takers seen, 36 famines; taking costs 0.7-1.1 us a village-day. **That run also found the sickness
model failing at scale**: 10,031 people at year 350, 1,963 at year 400 with full granaries - the black spots of year 362 and the
strains that shifted from it reinfected everyone (D2-070). Mono equals dotnet (gen 964f7af16bd09bf4).
**Next.** D2-070 (sickness at scale), then F6 slice 2: the watch, blows and injury.

## D2-070 — 2026-10-06 — Sickness at scale: immunity remembered, strains of one sickness, spillover bounded

**Context.** D2-066 was validated to 200 years (at most 2,640 people). A 400-year run of seed 42 (D2-069) grew to 10,031 people in
30 places and fell to 1,963 in 50 years with full granaries: the black spots of year 362 (virulence 0.05, immunity 35 years) and
the strains that shifted from it infected ~220,000 cases among ~10,000 people. Three faults: a person remembered two immunities
and forgot the older as new antigens came, so they caught the same sickness again and again; a tenth of mutations shifted the
antigen past all old immunity (one in 20,000 infections: a great epidemic made several new kinds of itself); the chance of a
sickness coming from the animals grew without bound with herds and crowding (18x in a village of 500 with a hundred beasts:
28 sicknesses in 400 years, most after year 250).
**Decision.** (1) A person remembers six immunities (`Disease.Remember`: its own slot renewed, else a free or spent one, else the
one ending soonest). (2) Strains belong to their sickness (`Pathogen.Family`, the lineage that came from the animals): immunity to
one strain keeps off a strain of the same sickness with a shifted antigen 60 % of the time; a drifted strain meets the whole
immunity. (3) A fiftieth of mutations shift the antigen (was a tenth). (4) The chance of a sickness from the animals is 6e-6 a
village-day x herds (up to 3x) x crowding (up to 2x): a new sickness every ~20 years among thirty crowded villages, every few
centuries among a few small ones (`Disease.SpillChance`). (5) A shifted strain is named for its sickness and the year it turned
("the black spots (the new kind of year 364)"). (6) The run report lists the deadliest sicknesses with their traits. Sim
version 24.
**Evidence.** `DiseaseTests` (148 tests): six immunities remembered, a seventh in place of the one ending soonest, a renewed one
kept; of 73 people immune to the old strain, a drifted strain took none and a shifted one 35 (0.48; 0.4 expected); spillover
grows with herds and crowds and is bounded. Seed 42, 400 coarse years (`tc2/out/society/sick42_400.txt`): 15,984 people in 35
places at year 400 - 12,000 to 16,000 from year 270, famine-regulated as before sickness existed (17,089 hunger deaths, 426
famines); five sicknesses from the animals, 122 village epidemics, 3,376 deaths: the summer spots (year 338) and the sweating
spots (342) took 1,734 and 1,526 (14,151 -> 12,189 people, made good in twenty years). Seed 7 (`sick7_400.txt`): 4,600-5,400
people in 18 places from year 200; the cattle ague of year 232 (virulence 0.2) killed 1,363 of ~5,000 and the villages recovered.
Taking in these saturated worlds: seed 42 11,107 loads (5,779 takers seen), seed 7 6,472 (3,158). A village-day's sickness costs
11.3 us (5 before: six slots, kindred checks); 400 years of seed 42 take 245 s, knowledge copying (D2-062) the largest part of the
village day after the COARSE day (94 us at ~450 people a village). Mono equals dotnet (gen cb0333f12013579a). **1,000 years of
seed 42** (`tc2/out/society/sick42_1000.txt`, 1,046 s): 16,000-18,500 people in 35-36 places from year 300 (12,000-14,800 before
sickness existed), famine-regulated (116 hunger deaths a year); 33 sicknesses from the animals, 1,090 village epidemics, 34,018
deaths - three great plagues (the winter sickness of year 445, the sweating pox of 757, the cattle pox of 949, virulence
0.28-0.36) each killed 6,300-7,000 (a third of the people: 15,984 -> 10,156 in the first) and were made good within fifty years;
the ledger keeps 268,881 events at year 1,000.

## D2-071 — 2026-10-06 — The watch, blows and injury (F6 slice 2)

**Context.** 10 §6 slice 2, on TC1's primitives (`docs/v3/50_PRIMITIVES.md`): injury that heals or kills, the urgent choice at a
danger (come out, run, turn on them), defence from remembered harm. Research (TC1 `40_RESEARCH_CONFLICT_PLANET.md` §A): most
small raids are stealth and flight is common, death rare and enormous for a small group (Mathew & Boyd); each blow is a fresh
decision (Boehm); defence - watching, gathering in, walls - is weighed against labour and the losses remembered, never a count.
**Decision.** (1) Injury (`Person.Hurt` 0..1 and the wound behind it): abed above 0.4 (lived `Ill` with its own why; COARSE no
work), off the roads above 0.25 (no visits, trading or taking); heals 0.03 a day x (0.5 + the last day's share eaten); above 0.5
a daily chance of 0.3 x 4 (hurt - 0.5)^2 of dying of the wounds - the death a killing, the striker's deed ("died of the wounds of
a fight in the night", caused by the blow). (2) What follows the shout (`Conflict.Confront`, both resolutions): when a taker is
seen someone comes out 70 % of the time (the watcher always) - the watcher or the boldest fit grown person at home; the taker
turns on them by boldness and desperation (at most 30 %) or runs and is caught 35 % of the time; blows go by strength (health,
years, hurt, and the metal the village holds - knives and axes, up to 1.3x) and the neighbours who come (a third of the homes
round the store, at most three, each half a defender more): the loser takes 0.3-0.9 of hurt, the winner up to 0.15. (3) A blow
is a wound deed (100 FU a whole hurt; a taker struck in the act weighs half) known to the struck one's village by sight and to
the striker's as its own; a killing weighs 600 FU (halved likewise); blood halves every 10 years. (4) The watch
(`Conflict.Watch`, a village's choice each day): kept when what it knows it lost to takers in the last year (losses found count,
the doer unknown) comes to more a night than a watcher's lost sleep (1 FU); the watcher the boldest fit grown person at home, by
turns; lived, they stand at the store from their bedtime to two in the morning (`ActivityKind.Watch`); a COARSE day takes 3
hours off their work; a taker at a watched store is seen 85 % of the time, and the next taker weighs that. History: `Struck`
("Krukfald Haerfas was caught at the store of Kubroford in the night and badly beaten by Grend Drousaern"), `WatchSet` when a
watch begins ("The people of Kubroford began to keep a watch at their store at night: grain has been taken from it"). Sim
version 25.
**Evidence.** `TakingTests` (151 tests): of 12 takers seen, 6 blows - the wound a deed the struck one's village knows by sight and
holds against the striker's; of five people wounded deep (0.95) four died within 40 days, each death a killing known to their
village (its grievance 2,877 FU), five wounded lightly (0.35) healed; a village that found two loads gone this year keeps a watch
(told once), seen at its store 0.26 -> 0.85, the watcher "keeping watch at Kubroford's store through the night: grain has been
taken from it", the watch let go a year on with no more losses. 400 coarse years: seed 42 (`tc2/out/society/blows42_400.txt`):
4,126 loads taken (11,107 without the watch), 3,789 takers seen, 2,428 blows, 214 dead of their wounds (~9 a year for 100,000
people at saturation - medieval England's homicide rates were 20-30), watches on 41,827 village-nights; 14,378 people in 36
places, 14,954 hunger deaths; seed 7 (`blows7_400.txt`): 3,587 taken, 1,635 blows, 135 dead of wounds, 4,779 people in 18
places. Conflict costs 7.8 us a village-day. Mono equals dotnet (gen 6444494b25d45671).
**Next (F6 slice 3).** Answers and news: claims (return, pay in kind, deny, refuse), taking back as a choice, compensation;
ties pressing for settlement; deeds carried as news by visitors, traders and brides (reputation).

## D2-072 — 2026-10-06 — Answers, news and taking back (F6 slice 3); grievance kept as ageing sums

**Context.** 10 §6 slice 3. Research (TC1 `40_RESEARCH_CONFLICT_PLANET.md` §A2, A4): a wrong is settled by return or payment,
answered by a blow in kind, outweighed by ties, or forgotten slowly; compensation (wergild) exists in almost all feuding
societies and buys peace; cross-cutting ties - kin across the line, help received - press for settlement (Gluckman); a
household known to answer harm is less often robbed: reputation is knowledge of deeds carried as news.
**Decision.** (1) Claims: once a day a village asks amends for the newest unanswered blood its people suffered in the last year
from known doers (not struck taking), worth 10 FU or more; someone of the wronged household (else the boldest) walks over the
next morning (`Person.Errand`) - COARSE: at once. (2) The answer (`Conflict.Answer`): the doers' village pays the wrong's weight
in grain from its store when it can spare it and is willing - 0.2 + 0.3 x the evidence (seen 1, told 0.6) + its ties (close
kin across the line 0.4, help received up to 0.3) - 0.6 x what it holds against the claimants (50 FU and more) - and the wrong
is made good (it leaves the grievance); otherwise it refuses, or denies a claim that rests on hearsay: a new wrong of half (a
quarter) the claim. History: "The people of Gouthorpe made amends to Kubroford for a wounding (60 days' grain), asked by Krarne
Gairnstaer" - "... would make no amends to Kubroford for a wounding when Krarne Gairnstaer came to ask". (3) News
(`DeedBook.Meet`): a visit, a trade or a claim between two villages tells each what was done to the other - what it saw itself,
never hearsay, never the hearer's own people's deeds; a COARSE day has each village meet each neighbour within a day's walk at
the lived rate of visits. A village known (seen or told) to beat takers keeps takers off: their freedom falls with the hurt it
dealt (two hard beatings remembered keep them away). (4) Taking back: a fed, bold grown person whose village holds a heavy wrong
(30 FU or more) against a neighbour may go at night to take from its store - 0.01 a night x boldness x the wrong's weight up to
200 FU, restraint lifted by the wrong - the taking caused by the wrong ("to take back for the wrong Kubroford did Gouthorpe").
(5) Grievance and gratitude are kept as sums that age as the deeds do - per pair of villages and kind (goods, blood, help),
brought to the present when they change - read at once instead of scanning every known deed (which cost a dense world 87 + 127
us a village-day in conflict and trade); saved as state, so a save continues exactly; the news told so far is a cache.
Settlement is a flag on the deed. Sim version 26.
**Evidence.** `TakingTests` (156 tests): amends made where ties and means allow (Kubroford's grievance 60 -> 0) and refused where
Gouthorpe had nothing to spare (the grievance grows); a claimant "walking to Gouthorpe to ask amends for blood its people shed:
Kubroford holds them to it" and the answer in history naming them; Kubroford learns from Gouthorpe's account that a third village
beats takers, and does not pass it on; a fed village holding a killing against its neighbour takes back (2 loads in 40 days); a
save with deeds, grudges, a claim and news continues identically. 400 coarse years (`tc2/out/society/answers42_400.txt`,
`answers7_400.txt`): seed 42 1,493 amends asked, 285 made, 1,208 refused, 3,587 loads taken back, 4,971 blows, 294 dead of
wounds, 14,776 people in 36 places; seed 7 2,027 asked, 250 made, 1,777 refused, 5,607 taken back, 6,744 blows, 373 dead of
wounds, 5,537 people. With the sums the same history costs conflict 26 us and trade 4.2 us a village-day (400 years 221 s, 271
before). Mono equals dotnet (gen 4b39f29b8ca834d3).
**Next (F6 slice 4).** Raids: a band of kin and neighbours raised by a heavy wrong or hunger, each deciding; it walks together at
night, takes several loads, meets the defenders as a group.

## D2-073 — 2026-10-06 — Raids: bands of people who each chose to go (F6 slice 4)

**Context.** 10 §6 slice 4. Research (TC1 `40_RESEARCH_CONFLICT_PLANET.md` §A1, A3): raiding parties form along kin and friendship
(Glowacki et al. 2016), each member deciding for their own reasons; most small raids are stealth raids, flight is common and
death rare (Mathew & Boyd); revenge raids follow losses; need and grievance are the motives. TC1's lesson (D-151): bands are
real people walking together, never a counter or a war flag.
**Decision.** (1) A village's day may raise a band (`Conflict.Raids`, both resolutions): when it holds a heavy wrong against its
foe (150 FU, more readily up to 600) or half its fit grown people go hungry beside a full store a walk away; a chance of 0.02 a day
at the fullest, less the more its takers and bands were beaten back at that village (the deterrence of known answers). Its boldest
fit grown person without kin there leads; each fit grown person decides to go - by boldness, half again for the leader's close
kin, never against a village where they have close kin; three to twelve go. A band dares only what it expects to win: its
strength against the defence it knows (the neighbours round the store, the watch, and the help a village of that size rallies).
(2) Lived, the band's people set out at their bedtime the next night and come to the village together at one in the morning
(`Band`, one pace, as migration parties walk); COARSE, it falls at once. (3) The fall (`Conflict.Fall`, shared): a band is seen
more readily than one taker (1 - (1 - seen)^(1 + 0.3 (n - 1))); unseen, each carries off a load; seen, the watcher, the first to
wake and the neighbours round the store come out and, each round, more of the village wakes (two and 15 % of its fit grown
people); blows go round by numbers and strength (health, years, hurt, metal) and the defenders' home ground (1.2), until one side
breaks (half the other's strength) - a band that holds the field carries off half its loads. Every blow a wound deed; the raid a
deed the raided village holds against the raiders' (the loot and 30 FU of fright); what the band carries home answers the oldest
of the wrongs it went for, each wholly (they are made good). History: "Bae Drousaern raised a band of 7 in Kubroford to fall on
Gouthorpe: they hold a heavy wrong against it" - "A band of 7 from Kubroford, led by Bae Drousaern, fell on the store of
Gouthorpe in the night, fought off its people (4 blows) and carried off 117 days' grain". Sim version 27.
**Evidence.** `TakingTests` (159 tests): a killing unanswered raises a band whose people walk there together and fall on the store,
its cause the band and the band's the wrong, and all come home; the COARSE day raises bands at the lived rate (5 / 3 in 60 days,
three worlds); a band of 8 beats a village's defence 12 times in 12, a band of 3 rarely. Tuning on 400-year runs: with a share of
the whole village coming out, 5,461 raids were nearly all beaten (no band weighed its chances); with only the first to wake, 725
nearly all won; with the village rallying round by round and bands daring only what they can win: seed 42 (villages of ~400) 18
raids in 400 years, 2 beaten off; seed 7 (villages of ~270) 161 raids, 71 beaten off, 16,426 FU carried off
(`tc2/out/society/raids42_400.txt`, `raids7_400.txt`) - large villages are safe from a lone band, small ones are prey. Mono equals
dotnet (gen 1e8c1954f8ae3dce).
**Next (F6 slice 5).** Defence that is built: a palisade from remembered losses against its labour and timber.

## D2-074 — 2026-10-06 — Defence that is built: the palisade (F6 slice 5)

**Context.** 10 §3, §6: settlements weigh defences from remembered harm against labour, materials and knowledge - a ditch, a
palisade, later walls - never "N raids, then a wall" (TC1 D-187). Research (TC1 40 §A5): warfare drives palisaded and walled
villages and aggregation (LeBlanc; Allen & Arkush); defence is a choice weighed against labour and the losses remembered.
**Decision.** (1) A village's day (`Conflict.Defence`, both resolutions) weighs a palisade: what other villages' people took and
did to it in three years (`HarmSince`: losses found count, the doer unknown), a year, half of it saved over the fifteen years a
sound wall serves, against the labour and timber of a ring round its homes and yards (2.5 hours and 0.8 wood units a metre, worth
0.25 FU each; the ring 15 m beyond its farthest home, 25-170 m from the square). When the losses outweigh it, it begins one
(`WallPlanned`, caused by the latest loss) and builds it as a village work after its well and granary (`Construction`: its
people's hours and its woodstore; lived, at the ring: "setting posts in Kubroford's palisade: other villages' people have cost it
dear"). Finished (`WallBuilt`, caused by its beginning): "The people of Kubroford finished their palisade (302 m round): Kubroford
is a walled place". (2) The timber rots in thirty years unmended; a walled village mends it (60 % of the work, past 40 % rot) while
what it guards outweighs 30 % of its cost; spent, it falls (`WallFallen`). (3) A sound palisade sees takers (0.6 x its soundness:
they climb it or come by a gate) and doubles its defenders' ground (home ground x (1 + soundness)); bands weigh it before they go.
(4) The view draws it: stretches of sharpened posts round the ring with gates where the roads to the neighbours leave, as many
as the work has set, greying and leaning as the timber rots (`ProcMeshes.Palisade`; `unity.mjs shots --wall 1`). Sim version 28.
**Evidence.** `DefenceTests` (163 tests): a village that lost a raid and two loads this year begins a palisade the next day
("other villages' people have cost them 82 days' food a year"), its people finish it in 19 days, its cause the raid; seen at its
store 0.85, its defenders' ground x2.4; its people set the posts themselves; a band of 6 is beaten off 9 times in 12 at a walled
village, 4 at an open one; an unkept palisade rots and falls, told. Emergent in a test world: the village that suffered a killing
walled itself, its bold took back from the killers' store, the killers walled themselves too, and no band dared either wall.
400 coarse years (`tc2/out/society/walls42_400.txt`, `walls7_400.txt`): seed 42 began 35 palisades (20 standing, 15 still being
built at year 400) and saw 6 raids (18 without walls); seed 7 began 19 (9 standing, 8 building, 1 fallen), 70 raids (161 without).
Shots `tc2/out/shots/wall1`. Mono equals dotnet (gen db59e3c26d81b966).
**Next.** A 1,000-year run to judge F6's done-when; then leaders and tribute (polities).

## D2-075 — 2026-10-06 — Tribute and lordship (F6 slice 6)

**Context.** 10 §4: tribute after defeats where flight is hard (Carneiro's circumscription); a polity is a connected component of
the tribute graph, its depth what historians call a chiefdom or a state; secession, conquest and collapse come from the graph and
its strains (Turchin). Never a polity as a switch.
**Decision.** (1) A raided village weighs bowing to its raiders (`Conflict.Submit`, both resolutions): when a band of theirs held
the field there within a month (a raid it saw), they are stronger (a fifth more fit grown people), it knows no good land to go to,
and what they cost it a year outweighs a tenth of its harvest, it agrees to pay them a tenth of every harvest (`Settlement.Lord`;
history, caused by the raid: "The people of Thestford bowed to Taekdal and agreed to pay it a tenth of every harvest: they could
neither stand against its bands nor go elsewhere"). A lord raises no band against its own tributary. (2) At each harvest a
tributary pays (grain to the lord's store; the tribute answers the lord's wrongs against it) - unless the lord is gone or no longer
the stronger: then it pays no more, a refusal the lord holds against it ("Voton would pay Rirndrondmere tribute no more:
Rirndrondmere is no longer the stronger"). (3) A lordship is the villages whose lords lead to it, directly or through their own
lords (`Conflict.Root`, `PolitySize`): a tributary's tributaries are its lord's too (depth). The run report lists lordships and
traces why the first tribute was agreed and why the first ended. Sim version 29.
**Evidence.** `DefenceTests` (166 tests): a village beaten by a stronger neighbour's band, hemmed in with nowhere to go, bows the
next day, its cause the raid; it pays a tenth of its harvest; its lord raises no band against it though it holds a killing
against it; when the lord's people are weak and its own mended, it pays no more and the lord holds the refusal against it; a
lordship counts its tributaries' tributaries. Seed 7, 1,000 coarse years (`tc2/out/society/lords7_1000.txt`): 47 tributes agreed
and 47 ended, 450,019 FU paid; the first, Thestford to Taekdal in year 168, traced through Taekdal's band of 12 to a feud of takings
and beatings; the first ended, Voton's to Rirndrondmere in year 178, five years after it bowed. Seed 42 (`lords42_1000.txt`): 31 agreed and 29 ended, 306,078 FU paid, one lordship standing at year 1,000 (Mildlouby, one tributary); the first, Stoukgathby to Faemford in year 433 after a band of 12 fought off its people, ended in 441 when Faemford was no longer the stronger - lordships stay small and short (a tributary or two, years not generations) while a lord's strength is only its own village's. Mono equals dotnet (gen
8dea367f3e835612).

## D2-076 — 2026-10-06 — F6 closed: society, conflict and polities

**Context.** F6's done-when: "a fortified town or a polity can arise and fall in long runs, and its history explains it" (17).
**Evidence.** 1,000 coarse years of seeds 7 and 42 with no premise (`tc2/out/society/f6_7_1000.txt`, `f6_42_1000.txt`,
`lords7_1000.txt`, `lords42_1000.txt`). Fortified places: seed 7 began 21 palisades - the first, Pairmaithby's (year 169, 499 m
round), traced back through takings, beatings and refused amends to Traiskulton's famine of year 166; the first to fall,
Traiskulton's (begun 185 after a feud, rotted and fallen 347); seed 42 began 42, 4 fallen - Gouthorpe's (229) after Kubroford's
takers were beaten, Hostead's (begun 291, fallen 550). Lordships: 47 tributes agreed and ended in seed 7, each caused by a raid
and ended by the lord's weakness, told. The chain from scarcity to walls and tribute is lived by persistent people at both
resolutions: hunger -> taking (seen or not) -> blows, wounds and deaths as deeds -> claims answered or refused -> taking back ->
bands raised by heavy wrongs -> raids (beaten off or carried) -> palisades from remembered losses, and tribute where flight is
hard. All of it is deeds known per village, read as grievance and gratitude; nothing is a war flag, a hostility score or a count.
**Verdict.** F6 is closed. Carried forward: lordships that hold (a lord's strength the whole lordship's fighters; protection of tributaries; depth beyond one), prestige and leaders (a village's head from known deeds, calling work and bands),
alliances (help sent to kin under attack, shared enemies), captives, gates kept at night and walls of stone (masonry), bands
from several villages, beasts as loot and restocking, the violence rate (~37 deaths of wounds a year for 100,000 at saturation:
on the high side of medieval homicide) to calibrate, the view of lordships (who pays whom) and of bands on the roads.

## D2-077 — 2026-10-06 — The History Book: annals, ages found after the fact, every line its record (F7)

**Context.** F7 (17): the History Book - eras after the fact, settlement and polity histories, biographies, the why of every
event; done when a player can read 500 years and every claim links to records. 11 §7-8: historians are observers; eras are
named templates over measured features, found by change points with a minimum length; the ongoing segment is the present age.
**Decision.** (1) The annals (`Core/History/Annals.cs`, world state, saved): at each year's end the world's own counters as the
year's numbers - people and places, births and deaths by cause (hunger, sickness, wounds, disaster), famines, epidemics, raids,
walls begun, tributes, foundings and abandonments, techniques found and lost, the planet's mean temperature. Nothing in the
simulation reads them. (2) The Chronicle (`Core/History/Chronicle.cs`, an observer): ages are found in the annals by binary
segmentation of nine indicators (smoothed over nine years and standardized: hunger, sickness, wounds and raids a head, growth,
foundings less abandonments, walls, tributes, disaster deaths, warmth; running sums make any span's spread a constant-time
sum, so 5,000 years read as fast as 500) - a cut where it lowers the spread by more than a BIC-like penalty, each age at least
thirty years and at most 120; each age is named for what marked it most, people's marks before the planet's ("The Age of
Founders", "The Hungry Years", "The Years of the Black Pox" - the sickness that killed most -, "The Years of Feuds and Raids",
"The Building of the Walls", "The Years of the Lords", "The Coming of the Ice"), the first "The First Years" unless strongly
marked, a repeated name counted ("The Second Building of the Walls"). (3) A chapter is the age's numbers from the annals and its
events - half the most important of what marked it, half the most important of all, in order - each line standing on its
record: an event the ledger holds (the record's own words under its date, "Year 206, late spring: famine in Lortharndal",
its causes walkable to the root) or the annals' years. A place's history and a person's life likewise. (4) The book in the
game (`Hud`, the Book toggle or B, `?book=1`, the web bridge's `book`), on its own page: ages or places on the left, the chosen
chapter on the right, each line a link to its causes - each cause a record to follow in turn - and to the place it happened
(Go there). (5) `headless.mjs chronicle --seed N --years Y --coarse [--why] [--places]` writes the book and fails if a line
lacks its record. Sim version 30 (the annals in the save).
**Evidence.** `ChronicleTests` (169 tests): a record a year from the world's own counters; a hundred years quiet then hungry
give two ages, the cut at year 50 +- 6, the second "The Hungry Years"; every line of a 90-year book is its record under its
date and its causes can be walked; reading the book changes no state. 500-year books (`tc2/out/society/book7_500.txt` with
each line's causes, `book42_500.txt` with every place's history): seed 7 - The First Years (0-88), The Age of Founders
(88-119), The Years of Plenty (119-168), The Building of the Walls (168-198), The Years of the Lords (198-304), The Years of
the Shaking Flux (304-334), The Years of the Spotted Pox (334-370), The Hungry Years (370-414), The Years of Feuds and Raids
(414-), 99 lines, none without its record, every chain walkable ("Famine in Lortharndal <- hunger came to Lortharndal: the
granary is empty <- the harvest failed at Lortharndal (88% short) <- the fields of Lortharndal burned <- a fire started near
the land beyond Lortharndal <- lightning struck"; "a band of 12 from Kraistosthope fell on the store of Pairmaithby <- raised
for a heavy wrong <- Kraistosthope's takers caught and beaten at Pairmaithby's store, night after night"); seed 42 - ten ages
from "The Years of Plenty" to "The Years of the Black Pox" (470-), 420 lines with its 31 places' histories, none without its
record; each book written in 0.3-0.7 s. In the browser the book reads the same (`tc2/out/webtest/s2webg`: 6/6, bit-identical
with headless and the editor's Mono at gen 9b6482ca9f9f606f; `8_book.png`) - its timings were taken while another session's
headless browser held ~7 of 12 threads and are not measurements.

## D2-078 — 2026-10-06 — F7 closed: the History Book

**Context.** F7's done-when: "a player can read 500 years and every claim links to records" (17).
**Evidence.** D2-077: the book of a 500-year world reads as ages, chapters and places whose every line is a record - an event with
its causes back to NATURE, PEOPLE or YOU, or the annals' numbers - in the game (Book, B) and headless (`chronicle`, which fails
on a line without its record). The why of every line is walkable, each cause a record of its own; the inspector keeps a person's
events; the catch-up feed and the ticker keep the news.
**Verdict.** F7 is closed. Carried forward: Your Actions (what the player's own commands led to: the events rooted in each
command, counted, still unfolding while a fire or storm it began lives - with F9, as the game has no power a player can cast yet;
the ledger's roots already answer it), timelines and maps of an age, news sifted by incident (one story per process and place),
biographies and polity histories as their own pages (lordships now show in the places' lines), ages named by what changed
between neighbours of one mark (seed 42 reads "The Years of Plenty", "The Second Years of Plenty", "The Third Years of Plenty"
in a row), era names that tell their place ("The Hungry Years of Voton").

## D2-079 — 2026-10-06 — Direction: the living world must be seen (before F8)

**Context.** Arjun (6 Oct 2026), playing the Web build: "why is the world looking so bland, like trees and forest nothing is
there, the entire world looks so empty and boring, is ecological life and plants not implemented yet?" The planet does carry
its life - every cell's forest and grass grow with the climate, burn, regrow and are cleared for fields; 13 wild kinds live as
populations; villages keep herds - but the view barely shows it: a cell fully forested is drawn with ~23 trees (~4-5 a hectare,
where a forest has hundreds) and an average cell (26 % forest) with ~3; no trees beyond 3.2 km of the camera (further out the
forest is a tint of the ground's colour, so region and orbit views cannot tell wood from meadow); the ground is one smooth colour
with sparse grass tufts; animals are drawn true to scale (a deer is a pixel at village distance). D2-026 made look-dev a
first-class track; the vegetation fell behind it.
**Decision.** Before F8, the living world made visible, every part read from the simulation's own state (the forest, grass,
species and herds of each cell - nothing invented) and judged in real game screenshots: forests drawn at their true density near
the camera (instanced, with cheap distant forms); woodland visible from afar (a canopy on the ground by the forest's share, so the
region and orbit views show woods, meadows, scrub and tundra); ground cover by biome (grass, shrubs, flowers, rocks, undergrowth
under trees); tree species by climate; animals readable at the views they belong to. The Web build keeps 60 fps (measured with
`webperf.mjs` on a quiet machine).

## D2-080 — 2026-10-06 — The woods drawn from the simulation (D2-079, slice 1)

**Context.** D2-079: the planet's forests were simulated but drawn as ~4-5 trees a hectare near the camera and as a tint of the
ground beyond 3.2 km, so the world looked empty.
**Decision.** (1) One layout of tree crowns (`Game/View/Canopy.cs`: 24 x 24 jittered crowns on a 156 m tile, radius 0.60-0.82 of
their spacing, each with its number) shared by the ground's shader and the trees: a crown stands where its number is below the
threshold of the forest share there, -ln(1 - share) x 0.62, so the standing discs cover that share of the ground; it is a conifer
where a hash of its number is below the conifers' share. (2) Each ground vertex carries its woods (`TerrainLod`, uv2): the
simulation's forest share blended over the cells, less fields, villages, roads, sand, rock, lava, ice and fire, times glades of
~170 m; the conifers' share by the cell's mean temperature; the leaves' season where there is an autumn; snow. (3) The terrain
shader draws the canopy of standing crowns (three projections blended by the up vector): domes and pointed conifers, each lit by a
bump from its height (a surface gradient, no tangents) and casting a short shadow sunward, greens from dark to yellow by its
number, autumn's gold and rust, bare boughs in winter, snow on conifers, the floor between them leaf litter out of the sky's light;
a crown under ~3 pixels gives way to the canopy's mean. The block is branch-free (WebGPU wants derivatives and filtered samples in
uniform control flow). (4) Within 440 m of the camera, on the finest ground patches in view, every standing crown is a tree at its
crown (`TerrainLod.PlaceForest`, `DrawForest`), the canopy fading to them from 400 m to 300 m: broadleaves of three lumps of leaves
(~250 triangles) within 190 m and one (~90) beyond, bare boughs in winter, conifers in tiers (~57) or one cone (~24), coloured as
their crowns (`ProcMeshes.ForestBroad`, `ForestBare`, `ForestConifer`). (5) The old sparse woods (`WorldObjectsView`, per cell to
3.2 km) are gone; the villages' yard trees stay. The bridge's `view NAME` frames the camera's named views (forest, forestlow,
forestfar: the most wooded land near the first village); the Web test shoots the woods (4c, 4d); `webperf.mjs --view forest`.
**Evidence.** Editor shots `tc2/out/shots/canopy1`, `canopy2`, `forest3`, `forest4`: the village view a clearing of fields and
pastures in woodland with copses and hedgerow trees; the forest views a wood's edge of trees with trunks, shadows and mixed greens
over leaf litter. **Not yet measured in the browser**: the Web build and its test were stopped when the machine ran out of memory
(other sessions shared it); the frame cost of the trees and the canopy is the next check.


## D2-081 — 2026-10-06 — The woods as masses, the land's cover by what it is, the villages' signs (D2-079, slice 2)

**Context.** D2-080 drew the woods as a canopy of crowns near the camera and as a flat tint beyond: from the region view the crowns
aliased into a faint speckle (1-4 m a pixel) and then vanished into one green, so woods and meadows could not be told apart; the
ground near the camera was one flat colour; every tree was the same round crown; the beach was baked into vertex colours.
**Decision.** (1) **The far woods are the near canopy averaged** (`Canopy.MakeCover`): a texture keeps, at four crown thresholds,
whether a standing crown covers each texel; its mips are the share of ground covered, so where a crown is under a few pixels
(1.3-3 m a pixel) the woods are drawn as their crowns' mean colour over the ground they cover, the floor between them shaded by
the crowns sunward, the clumps lit by a bump from their cover. (2) **Nothing in the repeating tile may make a pattern**: crown
numbers that were ranks of a smooth noise (clumped crowns) showed the 156 m tile as a grid from orbit (`woods1`), so the numbers
are plain chance again and the woods' pattern lives in the forest share, which never repeats - glades of ~170 m (D2-080) and now
clumps of 20-45 m where the woods are open (a closed wood stays closed; the share's mean is kept). Past 3.5-7 m a pixel the cover is
read a second time turned 37 degrees and scaled 2.4x (clumps of crowns ~20 m, the same share covered, a period that never lines up
with the tile), and past 10-20 m a pixel from its exact mean (`Canopy.CoverMean`). The share-to-threshold factor K is fitted on the
layout (0.532). (3) **Stands**: the far woods take a grain of a few hundred metres (older and darker, younger and lighter) - the
woods' texture from orbit. (4) **The climate's dryness** (the moisture index: desert < 0.16, savanna < 0.45) turns leaves olive, in
the canopy and in the trees (two dryness bits in the trees' colour groups). (5) A fourth vertex stream carries the land's own state
(dryness, meadow, bare earth or rock, the beach's width). (6) **The ground's cover by what the land is**, near the camera (to
260 m; shrubs and boulders to 480 m): meadow grass patchy in density and tone, wildflowers in their season, a shrub here and there;
the woods' floor with ferns and undergrowth in the gaps; shrubs thick at the woods' edges; heather on cool, wet, open land (purple
in late summer); cushions, lichen and stones on the tundra; straw tufts and stones where the climate is dry; dune grass on the sand
above the tide; reeds by fresh water; boulders on rocky slopes; re-placed as the seasons turn (`ProcMeshes` Shrub, Fern, Cushion,
Stone: 20-60 triangles each). (7) **Kinds of trees** near the camera, by each crown's number: round, tall and narrow, broad and
spreading, and birches where the woods are mixed with conifers; where a fire burnt the woods the trees are young and short,
regrowing over ~40 years (`Fire.BurnedAt`). (8) **Beaches per pixel** from the ground's own height over the sea (a vertex colour
followed the triangles of coarse patches as a staircase along every coast). (9) **The villages' signs**, each read from the
simulation: smoke from huts and camps (cooking, the hearth against the cold) as from chimneys; woodpiles by the lived-in homes, as
big as the days of the hearths' wood the village holds (`Woodstore / FuelDay`; none when it is nearly out); a fire by the store while
someone keeps the watch there at night (D2-071); at night, seen from high above, a warm glow over each village as wide and bright as
its homes whose people are awake. (10) Wild herds are drawn up to 3 km of altitude and up to 4x their size far off, so they read at
the village view as groups of moving dots. (11) Tools: `unity.mjs shots --views view+D@H` lives D days and to local hour H before a
view (seasons and nights in one round); `unity.mjs build-web --jobs N` caps IL2CPP's parallel jobs (a shared machine).
**Evidence.** Editor shot rounds `tc2/out/shots/woods1` to `woods4` (see 13 §9). The Web frame cost is measured next (14).

## D2-082 — 2026-10-06 — The woods in the browser: near views gathered once, the catch-up seen from orbit, builds that share the machine

**Context.** The Web build of D2-081 (`tc2/out/webtest/s3weba`, WebGPU, RX 5600M, 1280x720) was 6/6 and bit-identical with headless
(gen 9b6482ca9f9f606f), the village view faster than before (96.5 fps, 81 at s2webf), but the close view fell to 39.5 fps (66) and
the low view to 49 (74). Layer by layer (`webperf`, noisy): close all ~44, without the ground cover 66, without shadows 100; low all
~38, without the cover 68, without shadows 75. A day away caught up in 56-57 s with the camera at 1.4 km and 46.6-49.3 s with it at
22 km (`webcatchup`). And a Web build took every core twice: Bee compiled the C++ one job a thread, and binaryen's wasm-opt took
every core at the end, with available memory down to ~0.4 GB on a machine other people's work shares.
**Decision.** (1) The ground cover's and the woods' instances are gathered into the instancers only when the patches shown or what
stands on them change, when the camera moves more than 12 m or turns more than ~3 degrees, or every 15 frames; between, the
gathered lists are drawn again (gathering them every frame - walking every tuft of every near patch - was the near views' cost).
Cover patches out of the view are left out; a patch's cover is kept sorted by kind and colour so a gathering adds long runs of one
group; trees and cover are placed again at most once a world day per patch (seasons, growth and fire move no faster); tools can
switch the shrubs' and boulders' shadows (`off covershadow`). (2) While the world catches up an absence the camera rises to the
planet (`CameraRig.CatchUpAltitude`, 22 km) - 12 §5's "slowly updating planet" - and returns to the view the player left; the
catch-up's slice of each frame is 90 ms (was 40): the catch-up screen needs ~10 frames a second. (3) `unity.mjs build-web --jobs N`
caps IL2CPP (`--jobs`), Bee (`BEE_BUILD_THREADS`), emscripten (`EMCC_CORES`) and binaryen (`BINARYEN_CORES`); builds on this machine
use 3.
**Evidence.** The rebuilt Web build (`tc2/out/webtest/s3webb`): 6/6, bit-identical with headless and the earlier builds (gen 9b6482ca9f9f606f,
d1-d3 unchanged); settled frame rates (`webperf`, after 30 s: shaders compiled, patches built) close 65, low 69, forest 68 fps (the test's
readings 4.5 s after framing are lower: close 55.8, low 61.7, forest 55); shadows are now the largest single cost (close 81, low 82,
forest 91 without them). A day away: 48.2 s with the camera at 22 km and 48.9 s held at 1.4 km (was 56-57 s there; 54 s at s2webf) -
at orbit the time is the simulation's own (this young world's 96 years are lived at ACTIVITY). The build with `--jobs 3`: 14.8 min,
CPU at most ~47 %; available memory still touched ~0.34 GB at the link while other work held the machine.

## D2-083 — 2026-10-06/07 — DEEP calibration: a step keeps its days (closed 7 Oct: six seeds at par in people and per-person rates)

**Context.** F8 slice 1 (committed with D2-082) gave COARSE a DEEP resolution: calm villages live up to twelve days at a step through
the same laws. Lived 400 years beside COARSE on seeds 42 and 7 (`tc2/out/deep/`), the first DEEP runs were biased beyond the seeds'
own scatter: people -15..-37 % at year 200 (seed 7: 4,607 against 2,925), cattle x3 and pigs x0.2 at the end, relief x0.5, wild hours
+10 % for less food an hour, scouts x0.75, raids x6 on seed 42 (6 against 35). Tools made to find why: headless `run --annals F.csv`
(the annals, yearly), and tools-only tallies printed by `run --places` - why villages were not calm (`SettlementSystem.Unrests`), a
COARSE person-day's hours by use and the wild choice by kind and by day of the plant cycle (`AgentSystem.Hours`), the harvests
(`SettlementSystem.Harvests`), the yearly weighing of leaving (`Migration.Weighs`); `scratchpad/annals.mjs` (rates per 1000
person-years by century), `calsum.mjs` (paired totals), `calib.sh`/`calib2.sh` (runs).
**Decision.** A step is its days. Each bias was a law that read the step's start for all its days, or a daily limit applied once a
step; each is now kept as days lived keep it (04 §4.3b, "what a step must keep of its days", rules 1-6):
1. *Gates open on their day*: a mother may conceive from the day her nursing ends (it waited for the next step: ~5 days a birth), a
   girl from her 16th birthday, marry from her 18th (`SettlementSystem.DaysUntil`); a beast's maturity and its kind's twelve-day
   breeding season counted day by day; scouts every six days from the last (`Migration.ScoutsDue`); claims, one a day of the step.
2. *What changes through the step is walked through it*: the crop grows each day at that day's place on the season's curve (the
   fields' temperature marched by `DayMean`), and the step's later days grow it after the step's work (grown before its weeding, a
   growing step was three-quarters tended - seed 1's crop grew 0.853 against 0.884, its rations ran short four times as often and
   three times as many households left); a beast's condition day by day, its hazard, milk and conception on each day's condition
   (`Livestock.DeepBeast`); a body's chance of death each day on that day's health, conception on the step's mean health
   (`Person.StepHealth`); the step's work reads its first day's ration - its foraging, milk and meat hold the store through the step
   as days lived would (a mean of rations walked down by the step's need alone, tried in round 3, marked whole steps short for a dip
   at their end: out for need on 43.5 % of person-days against 32.8 % lived, seed 7 - removed); hearths and pasture take the step's
   middle day; the step's one choice of hunting, fishing or gathering is made on the woods as its middle day finds them, its pigs
   having rooted half the step's mast (`Livestock.Rooting`, `WildFood.MidStep`), and **each worker of a step chooses on the ground as
   the step's earlier workers left it** (`WildFood.Retake` after each wild trip): days lived re-choose each day as the village's own
   take empties a ground; chosen on the step's first woods, twelve-day steps kept gathering where days lived turn to hunting (seed 7:
   hunting 0.088 of weighed person-days against 0.156 lived, the woods overdrawn to 0.05 FU an hour against 0.08).
3. *A day's cap stays a day's*: a trip takes at most a share s of a ground - over a step 1 - (1 - s)^K (`WildFood.Most`); the least
   wild trip is a quarter-hour a day; grazing takes each cell's daily share for each day (capped at one day's offer a twelve-day step
   grazed less than two and cattle tripled); pigs root day by day, a sixth at most of what is left each day (a step's days at the
   first day's find took up to twice the woods, left them bare, and the pigs were culled to their core each step: pigs x0.07 on six
   seeds); herds are kept to the pasture as the step leaves it (days lived cull at its leanest); a smith stops when the village has
   its metal; and **an allowance meant for a day is given each day** - a pressing need keeps people out up to nine hours a day, each
   day of the step (given once a step, a short step foraged a day's need-hours less a day and stayed short longer).
4. *What the step moves at its start is seen as days lived would hold it*: the store's lead (`Settlement.Ahead/AheadFrom/AheadTo`,
   `StoreSeen`; saved, hashed only when set, so other worlds hash as before), read by trade, relief, amends, the hungry choosing a
   store, the step's own urge to the wild and whether its store is empty (a step's larders and decay at once could empty a lean store
   and record a hunger).
5. *Chances that come once a day come once a day* (claims; Over(p, K)).
6. *A step never spans a plant pulse* (`World.NextPlantTick`, derived from the queue): the plants and animals take their step every
   twelve days, charging what people and herds took from the woods in that window and forgetting it; a village step that spanned one
   charged its whole rooting before the pulse, which forgot it - the woods looked full all the next window and a third fewer hunted.
Sim version 32. One-day arithmetic unchanged: COARSE's 400-year state hashes of seeds 42 and 7 are identical before and after
(55cfd96b471db300, 8c3e14c324165015); 176 tests pass. An exact speedup on the way: a person's body build and copying scatter are kept
with them (`Person.Derive`, filled when made or loaded, never saved).
Found and left for its own slice (it changes COARSE): COARSE never improves hunting skill nor counts hunting trips - the line has sat
inside a comment in `CoarseDay` since D2-063.
**Evidence (so far).** Round 2 (seeds 42, 7; `*_400d`): relief at par (0.98), herds closer, but hunger and famine up (+10..39 %), wild
hours +33..50 %, conflict up on seed 42. Round 3a (`*_400e`, six seeds 42, 7, 1-4, built before the pulse, middle-woods, tending and
pig-rooting rules): no bias in early growth (people at years 100 and 200 higher on three seeds, lower on three) and births and deaths
per person at par (1.02, 1.03), but the population at year 400 lower on all six (about -15 %) with less food than days lived: pigs
x0.07 (0/4), fish 1/5, herd births and slaughter -16 %, relief -24 % (1/5), and more households leaving (x1.6, 5/1). Small-world checks
after all rules: seed 33 (60 years) DEEP wild work matches COARSE (0.141 against 0.147 h a working person-day, was 0.115; wild food 49.4k
against 47.8k FU), harvests 120.4 against 120.0 FU a hectare; seed 1 (100 years): crop 0.871 against 0.884 (0.853 before the tending
rule), pigs no longer lost (born 6,069 against 4,878), deaths 26.9 against 27.0 a 1000 person-years, conceptions 0.0216 against 0.0221
a weighted woman-day - but its rations ran short far more often (shortfall 0.011 against 0.0007; out for need on 8.6 against 1.2 % of
person-days) and households left three times as often: its people were 9 % more by year 100 (an early lead, not a rate: on the six
seeds early growth goes both ways), and a threshold crossed by more mouths on the same fields.
Round f (`*_400f`, commit 6ad57fb, six seeds; `scratchpad/verdict.mjs`: paired log-ratios, t over the seeds, df 5): people at par at
every century (year 400: 1.03, t 0.7; year 300: 0.88, t -2.1, mostly seed 2, whose COARSE run grew larger and hungrier - 616 famines
against 261), births 1.00 and deaths 0.99 per person-year, hunger 0.88 and famines 1.02, wild food 1.03, herds (born 0.99, slaughtered
1.01; pigs no longer lost), relief 1.00, households leaving 0.78 (t -0.8), raids 1.16 (t 0.5); leans not significant: wild hours per
person-year +17 % (t 2.0), smelting (chance discoveries of metal: +25 %, -33 % by seed), infections -25 % (one seed); flagged:
foundings per person-year +12 % (t 3.0; totals 1.07, t 1.2 - with ~50 metrics, ~2 such flags are expected by chance). The wild-hours
lean had a cause: COARSE 7 rerun with the tallies (`coarse7_400g`, hash unchanged) against DEEP 7 - harvests (103.6 against 101.5
FU a hectare), deaths (27.9, 28.5 a 1000 person-years), conceptions (0.0220, 0.0220) at par, but out for need on 43.5 % of person-days
against 32.8 % and an urge to the wild twice COARSE's: the step's mean ration, walked down by the need alone, marked whole steps short
for a dip at their end; removed (rule 2).
**Final rounds (7 Oct).** Round h (no step ration, seed 7): harvests, deaths, conceptions and pigs at par, but out for need on 39.6 %
of person-days against 32.8 %, hunting chosen 0.088 against 0.156 and the free hours of a short step 10 % fewer - a pressing need's
nine hours given once a step. Round i (+ the need-hours each day): wild hours 2.17 against 1.40 a working person-day for the same food
(0.05 FU an hour against 0.08) - a step's workers all chose to gather on the woods its first day found and overdrew them. Round j
(+ each worker choosing on the ground the step's earlier workers left, `WildFood.Retake`; tc2bin20; six seeds, `scratchpad/verdict_j.txt`):
**at par** - people at years 100 and 200 1.00 and 0.96, at 300 and 400 0.85 and 0.87 (t -1.8, -1.7; df 5, critical 2.57: one seed, 4,
expanded more slowly - 27 places against 41 at year 300, almost no epidemics; the same seed in round f tracked COARSE), births and deaths
per person-year 1.00 and 1.00, deaths a 1000 person-years 1.00, conceptions a weighted woman-day 1.00, harvests a hectare 0.99, hunger
deaths and famines per person-year 1.14 and 1.18 (t 0.9, 1.9), wild food 1.02 and wild hours 1.05 per person-year, the urge to the wild
1.04, rations short 1.02, herds born 1.07, relief 0.89, households leaving 1.15 and foundings 1.04 per person-year (all n.s.). **Known
residual leans** (systematic over the seeds, not moving the outcomes above): the wild choice - hunting chosen 1.34x (t 3.5), gathering
0.94x, fish caught 0.81x (totals), out for need 1.09x (t 7.1) - a step chooses once a worker where days lived re-choose daily (a round k
that read a worker's own take on the step's middle-day ground cut DEEP's food and made every lean worse - reverted); smelting +18 % and
tributes -10 % per person-year (t 3.8, -2.9); marriages a weighted woman-day -3 % (t -9.8, under the 5 % bar). A finer fix (a step's wild
work chosen day by day) waits until the leans matter for something seen.
**Closing checks (7 Oct, session 5, on the committed tree).** 176 of 177 tests passed; the one failure was the small-world herd check of
`DeepTimeTests.DeepLivesTheWorldAsCoarseDoes` (seed 33 at 60 years: goats 269 COARSE against 117 DEEP, outside +-(half the larger +
15)). A beast count at one moment on a four-village world swings with a few herds' culls (on the six-seed 400-year comparison goats are
1.11 and pigs 0.73, both n.s.; seed 34 in the same test: goats 708 against 1,041), so the **test threshold is changed**: each kind's
count must be within a factor of 2.5 of COARSE's ((DEEP + 15) / (COARSE + 15) in [0.4, 2.5]) - it catches the biases the rules fixed
(cattle x3, pigs x0.07) and not one world's culling. All 177 pass. COARSE's 400-year state hashes on the final tree are unchanged
(seed 42 55cfd96b471db300, seed 7 8c3e14c324165015). Speed on the final tree (400 years, no profiler, the machine shared and loaded
by other sessions - 50-80 % CPU - so the absolute numbers are low): DEEP seed 7 3.20 world years a second against COARSE 2.42 under the same load (1.3x), DEEP seed 42 2.23 (round f on a quiet machine: 3.17 against COARSE 1.80, 1.8x; the hash runs, with the profiler and under heavier load, 0.93-1.06).

## D2-084 — 2026-10-07 — Direction: the world made legible (a standing requirement, after D2-083)

**Context.** Arjun (7 Oct 2026), having played the build again: "the simulation exists, but almost none of it is legible from
looking at the world". The player can read about forests, animals, rivers, famine, settlements, climate, migration, disease,
conflict, livestock, geology and disasters in the History Book and the inspector, but the planet itself reads as one huge
yellow-green surface of near-identical round trees and procedural blobs, weak biome distinction, invisible rivers, little
landscape structure, tiny settlements that barely read, no visible history - and from orbit "a dark coloured sphere". D2-079 to
D2-082 (the woods drawn from the simulation) were only the beginning; "we added more trees" is not an answer. The UI (top buttons,
History Book, inspector) still looks like development panels.
**Decision.** (1) A standing project requirement, documented in `docs/tc2/18_WORLD_READABILITY.md`: the world must be understood
to be alive - and historical - from the picture, at the orbit, region, village and close views, before any panel is opened; over
centuries it must visibly transform. (2) **No simulation theatre** (absolute): everything drawn corresponds to state; presentation
may elaborate (a forest share -> thousands of deterministic trees) and exaggerate for legibility (a 70 L/s river drawn wide, objects
scaled with distance, a herd drawn as a readable group), never invent. Where a meaningful difference cannot be drawn because the
simulation keeps no state for it, the smallest real state is added and tested (candidates: disturbance kind and time per cell - fire,
clearing, abandonment, landslide, lava - for scars and regrowth stages and the woods' stand age; roads by use). (3) **Order after
D2-083** (Arjun's): audit what the simulation knows against what is drawn (18 §3); landscape composition; forests beyond D2-080;
rivers and river corridors; biome and ground-cover differentiation; terrain and geology; settlements that read as places (with an
analysis of founding - fragmentation is fixed in the simulation's incentives if it is real, never by drawing bigger villages);
wildlife, livestock and activity; persistent scars and history; orbit-scale legibility; the multi-age acceptance; browser
performance (60 fps in the normal Web views without gutting the world: instancing, LOD, aggregates, impostors, shader detail).
Invisible simulation features wait until this is convincing; F8's remaining work (speed, Ancient and Deep time in the browser) and
the COARSE hunting-skill fix (its own calibrated change) follow. (4) **Acceptance**: the same seed shot at years 0, 25, 100, 300 and
1,000 from the same views (headless `run --snap`, editor `shots --load`) - "can I see history without reading the date?"; success is
never declared from code or from one cherry-picked angle. (5) Year 0 is already a natural world worth watching (geology, climate,
rivers, vegetation and wildlife spun up before people). (6) Art direction: a beautiful miniature diorama - not a satellite image,
not a plastic toy, not a procedural demo: colour hierarchy, controlled vegetation palettes, light, silhouettes, readable shadows,
depth and atmospheric perspective, real water, seasonal palettes; tilt-shift never smears the subject. (7) The UI gets a full art
pass once the world's legibility stabilises; it must not ship as debug panels. (8) Every slice updates 18, DECISIONS, PROGRESS
CURRENT STATE, SESSION_HANDOFF (and NEXT_SESSION_PROMPT when direction changes), the roadmap, benchmarks and shot locations.

## D2-085 — 2026-10-07 — The planet's size: R = 6 km, the same ground at any size (supersedes D2-007's radius)

**Context.** Arjun (7 Oct 2026), after D2-083: the world feels enormous and empty, settlements are visually insignificant, rivers
barely read, people and animals are microscopic, history occupies too little of the visible world - "a dense, intimate, beautiful
miniature planet where individual valleys, forests, rivers, villages, animals and people are visually meaningful", fewer people
acceptable, more detail per person, settlement and feature. D2-007 had fixed R = 10 km (N64, 228 m cells, ~20,000 people) before any
playtest. The study is `19_PLANET_SCALE.md`.
**Alternatives.** Keep 10 km and exaggerate; R = 7, 6, 5, 4 km with N64 kept (finer cells); a smaller N at the old cell size (no
recalibration, coarser ground - rejected: it buys nothing in detail per feature).
**Decision.** (1) **R = 6 km** (`WorldConfig.Radius` default; surface 452 km2, land ~165 km2, cells 137 m, 1.8 ha). (2) **The ground is
the same ground at any size**: the planet's geophysics (plates, uplift and wear, coasts, ice, drainage and river classes, quakes,
slides, tsunamis, storms, eruptions, copper's extent, the ground's steepness) keeps its per-cell tuning in geo-lengths of a 10 km
sphere (`PlanetState.GeoRadius`, `GeoScale`, `GeoAreaHa`, `GeoCellSize`); heights stay metres. (3) **What lives is in real units**:
walking, reaches, fields, yields, pasture and game per hectare; laws written per cell scale by the cell's real area (`AreaScale`:
predators' half-sated prey and territories, hunters' half-sated grounds, a cell's occupancy for dispersal, speciation and herds); a
"cell and its eight neighbours" is a real-size patch (`Patch`, `PatchRings`: hunting grounds, herds, stone for querns); the first
villages 1.2-3.9 km apart in metres; new homes on dry lots (`Settlement.NextDryLot` - on smaller cells the spiral of lots reached the
sea). (4) The view follows the size: orbit and region framing, the drawn relief (`Ground.ScaleTo`: 0.4 x GeoScale), the clouds' shell,
terrain LOD depth (the same finest quads) and haze are relative to R; village and nearer views stay metres. (5) Headless `--radius`,
`run --scale` (`ScaleReport`, `ScaleTally`) and shots `--radius` stay as tools. Old saves keep their own radius (it is saved per world)
and 10 km worlds live as before (every new factor is exactly 1 there; only a new home that would have stood in the water now takes the
next dry lot); new worlds are 6 km.
**Reason (measured; 400 years DEEP, seeds 42, 7, 1, 2 at 10/7/6/5/4 km, `tc2/out/scale/`).** At 6 km a village with its fields (~2 km)
is 0.17 of the globe's diameter - TC1's village map was 0.16 - against 0.10 at 10 km: every human mark is 1.67x wider (2.8x the
area) at the orbit and region framings, and the planet's curve shows from village height. Mature people: 5,471 / 1,995 / 1,116 / 7,110
(10 km: 13,546 / 4,876 / 963 / 19,043) in 18 / 10 / 11 / 30 villages - the 2,000-8,000 Arjun asked for on every normal seed (seed 1
is climate-limited at any size). The first century is the same at any size (~800 people at year 100); small worlds fill sooner
(6 km by year ~200-250; 10 km still growing at 400), so scarcity, conflict and politics start earlier. A village's own social world
is unchanged - ~8-10 villages within a day's walk at every size, marriages ~3.1-3.7 km, about two in five grown people living in
another village than their mother at ~3-4.8 km - while the world becomes one connected region (1.1-2.1 hops across, 46-98 % of
people reachable in a day, against 1.5-2.5 and 36-70 % at 10 km): intimate, not one village. 5 km and 4 km thin the weaker seeds to
1,000-1,250 people in 7-8 villages with boom-and-bust after year 200, and lose wolves at 4 km; 7 km is safer in numbers but gains
only 1.43x and less than TC1's proportion. Cost: 264 against 413 ms a world year on seed 42 (-36 %); per person-day it rises (the
planet's fixed share: ~150-180 ms a world year is now most of a small world's cost, F8's speed target).
**Consequences.** The great beasts are few on a small world: seals and bears die out on some seeds at 6 km (seed 42; seed 7 keeps
its seals), aurochs ~20-40 - viability (MinViable 5) is about numbers; whether the miniature should carry denser great beasts is a
D2-084 follow-up decided with the wildlife slice. People and animals are no larger on screen: their size depends on the camera, not
the planet (TC1's near views showed a person 32-98 px tall, TC2's 2-20 px) - D2-084's near views and readable exaggeration. Rivers
are brooks at any size (the largest carries < 0.3 m3/s): their reading is presentation (D2-084). Tests run at the new default (some
premises restated). D2-084's readability pass is redone on 6 km.
**Invalidation.** Playtests of the 6 km world finding it cramped (then 7 km) or still empty (then 5 km with denser contacts decay);
a planet cost that makes Ancient and Deep time miss their targets.

## D2-086 — 2026-10-07 — Villages keep their shape: a new home takes the innermost free lot (D2-084 slice, settlements)

**Context.** The 6 km multi-age baseline (`tc2/out/shots/ages6`, 1,000 years of seeds 42 and 7) showed old villages sprawling: every
household that found no empty home took the next lot of an ever-longer spiral (`Settlement.LotPosition(Lots++)`), while the lots of
homes whose ruins had gone into the land lay empty; by year 1,000 a village of ~110 households had its homes on average 212 m from the
square (farthest 276 m; compact, they fit within ~115 m), ruins outnumbered homes, and the palisade - sized from the farthest home - ran
round a field of scattered houses.
**Decision.** `Homes.FreeLot(w, sid, forHid)`: the innermost lot of the village's spiral that is on dry land and where no home, ruin or
other household's camp stands within 6 m (lots lie ~8-9 m apart); the spiral grows only when every inner lot is taken. A ruin keeps its
lot until it has gone into the land (80-300 years, D2-052). Used by marriages, households arriving and worldgen (replacing D2-085's
`NextDryLot`). Headless `run --scale` reports the villages' shape (mean and farthest lived-in home from the square, ruins).
**Evidence.** 1,000 years on 6 km (`tc2/out/scale/r6000_s{42,7}_ages2`): homes 128 m from the square on average, the farthest 208 m
(seed 42; was 212 / 276), 92 / 141 m (seed 7; was 171 / 234); people 5,823 / 2,315 (5,823 vs 6,178 and 2,315 vs 1,957 before - the seeds'
own scatter). Test `HomesTests.ANewHomeTakesTheInnermostFreeLotSoAVillageKeepsItsShape`; 178 tests pass.

## D2-087 — 2026-10-07 — A home the sea takes is lost (D2-084 slice: history must be true where it is seen)

**Context.** The 6 km season round (`tc2/out/shots/ages6b`, seed 42 at year 1,000, the street view) showed half of the first village
standing in open water: the sea had risen over the lots of lived-in homes (a coast's change: `World.LandChanged` drowned their cells,
removed the fields there and counted the village, but never touched its homes), and households lived on in houses in the sea. On a
6 km planet the coast lies nearer a village's anchor in metres, so the gap showed sooner, but it held at any size.
**Decision.** `Homes.SeaTook(w, cause)`, called by `LandChanged` whenever cells drowned: a standing home whose lot is now sea is lost
(left at once as a drowned ruin, which fades as ruins do); its household takes an empty home of the village or the innermost free lot
(D2-086); camps on drowned lots move too; one history line a village - "The sea took N homes of X: their people made new homes on
higher ground." (`HistoryKind.HomesDrowned`, Local tier, caused by the coast's change event; 0, rooted in nature, when the plates or the
waves moved the coast). The village's own cell drowning (its square and well) is left to the existing pressures of leaving (no
arable, no water) and to a later slice if it shows.
**Evidence.** Test `HomesTests.AHomeTheSeaTakesIsLostAndItsPeopleMoveToDryLand`; the 1,000-year world shot again (below).
**Evidence (7 Oct).** 179 tests; the 1,000-year seed 42 world (`tc2/out/scale/r6000_s42_ages3`) shot again (`tc2/out/shots/drown1`):
the drowned half of the first village is ruins under the water and its living homes stand inland. The same shots found the rest of the
problem - houses drawn in the shallows at year 300 before any rise: D2-088.

## D2-088 — 2026-10-07 — One shore for the simulation and the view (`Core/Planet/Shore.cs`)

**Context.** At year 300 the first village of seed 42 had lived-in houses and palisade posts drawn standing in the sea although no
coast had moved: the simulation judged a lot dry by its cell (land if the cell's centre is above the sea) while the view draws the
ground between cell centres by a kernel over the cell and its neighbours, with the shore ramped - so within about half a cell of a sea
cell (~70 m on the 6 km world) the drawn ground lies under the drawn water. Two rules for one coast (the pitfall: "any rule the
simulation and the view must share lives in Core").
**Decision.** The shore-ramped per-cell elevation and its kernel move from the view (`Ground`) to Core (`Planet.Shore`: `Drawn`,
`DrawnAll`, `At`, `Dry`; cached on the planet by relief, coast and sea level, never saved); the view draws `Shore.At` (plus its ice,
detail and exaggeration as before) and the simulation asks `Shore.Dry` - the drawn ground at least 2 m (sim) above the sea and no lake -
for every new home's lot (`Homes.FreeLot`) and for the homes and camps the sea takes (`Homes.SeaTook`).
**Evidence.** 179 tests; fresh 300-year worlds of seeds 42 and 7 (`tc2/out/scale/r6000_s{42,7}_shore`, shots `tc2/out/shots/shore1`): no
lived-in home in the water; what stands in the shallows are the ruins of homes the sea took.

## D2-089 — 2026-10-07 — Direction: an autonomous medieval civilisation inside a living miniature planet (a standing direction)

**Context.** Arjun (7 Oct 2026), a major direction update: TC2 is not "mostly complete simulation systems that now need prettier
rendering". The required scope is an autonomous medieval civilisation game played by its own agents inside a living geological and
ecological planet, with the *system breadth and visual readability* of a game like Age of Empires as the quality bar (settlements to
towns, buildings with distinct purposes, workers gathering, roads, mines, farms, docks and boats, fighters training, fortifications
evolving, kingdoms, armies, wars, damage and rebuilding, trade, resources constraining expansion) - with no player giving orders:
every building, road, army, ship, mine, wall, workshop and kingdom arises from simulated need, knowledge, resources, labour, politics
and history. Not a clone: no assets, names, factions, tech trees, UI or exact mechanics copied.
**Audit (`20` §4).** TC2 has strong primitives - persistent people and households, calibrated ACTIVITY/COARSE/DEEP layers, techniques
carried by people, construction by labour and wood, deeds, the watch, blows, raids, the palisade, tribute, history with causes - and
almost no economic geography: one store per village, three village works kept as bits, no workplaces, no transport, no roads (roads are
drawn between every pair of villages within 4.5 km whatever their use), no deposits but copper, and a land that people use without
changing it (**woodcutting removes no forest and fields clear none** - the view hides trees under fields; timber has no source and no
shortage is possible). The person inspector is one text box; no structure can be clicked.
**Decision.** (1) A standing direction, documented in `20_CIVILISATION_DEPTH.md` (target, the anti-theatre test of ten questions,
population tiers, the audit, the architecture of shared primitives) with `21_SETTLEMENT_ECONOMY.md` (structures, works by need,
workplaces and roles, morphology, specialisation read after the fact, culture in the built form, inspectors),
`22_MATERIALS_AND_RESOURCES.md` (source -> extraction -> transport -> processing -> use -> scarcity for every material; deposits from the
planet's geology, known only by seeing; stores with capacity; human impact on the land as real state), `23_MILITARY_AND_WAR.md`
(fighters are persistent people, arms as goods, roles that emerge, training, the cost of service, escalation from state, campaigns as
logistics, fortification progression, sieges) and `24_TRANSPORT_AND_MARITIME.md` (carrying cost in every choice, animals, sleds,
carts, roads by use, crossings, boats as built objects, ports, communication by people walking). (2) **Population philosophy**: fewer,
deeper people; development at tier A (100-500), testing at B (500-1,500), the first mature target C (1,500-3,000), more where land,
resources, history and performance allow; no artificial cap. (3) **Architecture: primitives, not features** - structures as one table
of kinds with one state machine; works chosen by need with their causes; materials in real units with stores; deposits discovered by
people; workplaces and roles read from hours; transport and route use; force as people, arms and skill; authority from prestige and
tribute; transmitted culture; land use as planet state; inspection pages - each lived at both resolutions, saved, hashed,
deterministic, traced in history (`20` §5). (4) **Names describe what exists** (hamlet ... capital; tribute ... kingdom); they unlock
nothing; a kingdom holds while its mechanisms do. (5) **Scarcity is physical** (`20` §7): conflict must become a rational possibility
and peace must stay possible; never manufactured. (6) **Roadmap** `25_CIVILISATION_ROADMAP.md`: C1 structures and workplaces -> C2
materials from the land -> C3 professions and chains, C4 transport and roads -> C5 mining and metallurgy; C6 force -> C7 fortification ->
C8 war and logistics; C9 maritime; C10 governance; C11 culture; C12 inspectors; C13 historical city evolution; W (legibility) and P
(performance tiers) alongside; F9-F11 after. (7) **The first slice is C1 "the working town"** (`25` §2): structures with function (well,
granary with real capacity), timber from real woods, stone quarried and carried, the smithy, the training ground, market and gates, then
a pressure premise - each sub-slice committed with tests, runs, shots and docs. (8) D2-084 stands and grows: every system is shown in
the picture with distinct kit models (headless Blender) in one miniature art direction; activity drawn only when the simulation says it
happens. (9) Method unchanged: no design bible without code; no subagents; the machine at ~50 %; docs are the memory.
**Consequences.** The world's laws change in ways that move COARSE baselines (timber from the forest, workplaces taking labour): each
such change is its own decision with long-run evidence on several seeds and both resolutions. The remaining look slices of D2-084
(farmland, rivers, wildlife, scars, orbit) interleave with the C-slices instead of preceding them. The person, building and settlement
pages are built slice by slice (the person page is non-negotiable).
**Invalidation.** None expected for the direction; individual numbers (loads, rates, capacities) are tuned with evidence.

## D2-090 — 2026-10-07 — Structures by kind: one primitive for everything built; works by need with their causes; stores with capacity (C1.1)

**Context.** D2-089's first slice (`25` §2). Before: homes were rows of `Buildings` (D2-059) but a village's well and granary were bits
in `Settlement.Works`, its palisade a ring, and nothing else could be built; no structure had a cause, a builder, materials recorded or a
page; the granary kept any amount of grain.
**Decision.** (1) **`Core/Places/Structures.cs`**: a kinds table (`KindInfo`: name, `Function`, `Zone`, footprint, labour, wood, fall and
ruin years, capacity) - homes (hut, house), works (well, granary, barn) and the kinds of the coming slices (smithy, quarry, training
ground, market; in the table, planned when their slices land). (2) **`Building` extended** (saved, hashed): condition, standing or being
raised, labour and wood (and stone) put in, its form (timber/footings/stone), the person who led it (the first to work on it), the event
that moved it, its completion event, stock and use. Homes keep D2-059's rules. (3) **Works by need** (`Structures.Plan`, daily and at
founding; `AfterHarvest`): no well -> a well (cause: the founding); a harvest to keep and no granary -> a granary (cause: the harvest);
at harvest, a store past 90 % of its capacity -> another store, a **barn** (40,000 FU, 360 h, 90 wood) where the harvest alone would
fill a granary (10,000 FU, ~7.5 t) or the overflow exceeds one, else a granary - so small villages keep granaries and big ones raise
barns, and a town's stores show its growth. The palisade and its mending stay ring works after the planned structure.
(4) **`Construction.Structure`**: the village work is the planned structure (`Settlement.Project`); hours and wood go into its row;
it stands when its hours are in (`WorkBegun`/`WorkBuilt` history under the building, the leader as Other). (5) **Capacity is real**:
grain within the stores rots at the store's rate (D2-042, D2-063), grain beyond it at 4x (heaps, pits, sacks: `Structures.Rot`, a step
of days as the days lived). (6) **Placement by zone** (`Structures.Place`): the first well and granary where the village drew water and
kept its heap (unchanged spots), later ones round the square (Core), barns on the main road past the homes (Edge), clear of every
structure, lot and camp, on dry ground; homes keep clear of works (`Homes.FreeLot` by footprint). (7) Works are kept while the village
lives and decay from its abandonment as homes do (`Structures.Abandon`); the sea takes a work as it takes a home (`Lost`: the Works
cache and the capacity follow). (8) **The building page** (Hud: click a structure) - kind, standing/being raised/fallen, condition,
what it is for (a store's capacity and fill, a well's people), hours and wood put in, who led it, **why** (its cause's history line),
its history. (9) The view draws works from the table (wells, granaries, barns; new timber rising while being raised; ruins); kit
models for barn, smithy, quarry face, stone pile, training ground and market stalls (`tc2/art/kit.py`). (10) Sim version 33.
**Evidence.** 185 tests (`StructuresTests` 6: worldgen works as structures with homes clear of them, grain beyond capacity rots
HeapRot times faster and a step of days as days lived, an overfilled harvest begins another store with the harvest as its cause and its
leader recorded, no wood no work, the planned granary raised in COARSE (7 days) and in lived days (4), a granary the sea takes takes its
capacity and the village plans another). 300 years DEEP on 6 km (`tc2/out/civ/s42_c11c`, `s7_c11c`): seed 42 765 / 3,660 / 5,295 people
at years 100/200/300 against 765 / 3,729 / 5,611 before (the first century identical; the rest the seed's scatter), 17 barns and 56
granaries standing in 18 villages; seed 7 738 / 1,795 / 1,732 against 738 / 1,936 / 1,648, 4 barns and 24 granaries in 10 villages.
4.5 world years a second (seed 42) against 4.6 before.

## D2-091 — 2026-10-07 — Timber from the woods: felling takes real forest at a woodlot that moves; fields clear what they farm (C1.2)

**Context.** The D2-089 audit: woodcutting added to `Woodstore` and removed nothing from the planet; claimed fields left the forest
standing in the simulation (the view hid trees under fields). Timber had no source, no shortage was possible, and - found in this slice
- the land's own law already discounted a cell's arability under woods (`Arability` x (1 - 0.6 forest)) while fields never cleared them:
farmland was held down by woods nobody felled.
**Decision.** (1) **`Core/Places/Woods.cs`**: a village fells at a **woodlot** - among the cells within 1.5 km of its own, land not under
ice or fields with a forest share of at least 0.12, the one nearest for the woods it holds ((d + 200 m) / (forest + 0.2)); its woodcutters
walk there (`WoodX/WoodY`). Each 25 kg load takes its mass from that cell's forest share (a full cover stands ~120 t a hectare); felling
leaves 0.02 (stumps, scrub); a woodlot cut below 0.12 is left for the next. (2) **The walk is paid**: a load from beyond the near woods
(210 m, the old woodlot's distance) costs the carry home both ways (`Woods.Rate`); a COARSE share of a day also pays the walk out and home
once a 3.25 h stint (`AfterWalk`), as a lived stint walks it; lived woodcutting, the COARSE hearths' wood and the COARSE store's top-up all
fell through `Woods.Cut`. (3) **Fields clear what they farm** (`PlanetSystems.GrowVegetation`): a field cell keeps at most the cover its
fields leave - the fixed point F = (1 - a) / (1 - 0.6 a), a = the village's used share x the cell's arability once cleared
(`PlanetState.ArabilityOpen`) - and released fields regrow by the same vegetation law. (4) History `WoodsReceded` (Local) when a
village's woodlot first lies past 600 m, 1 km, 1.5 km, or none is within reach (it then looks again every twelve days); metrics
`WoodFelled`, `WoodsReceded`; the run report's `woods:` line. (5) Sim version 34.
**Evidence.** 190 tests (`WoodsTests` 5: a load's mass comes off the woodlot's forest; farther woods cost more hours a load; the woodlot
moves on as the woods are cut and history tells it; fields keep the woods off and released fields grow back - 0.22 -> 0.45 in 40 years;
a village burns and builds from its own woods lived and COARSE). Three COARSE-vs-lived rate checks drifted out of their bands on single
worlds (visits, trades, night takings: ~3-20 events each); pooled over four to eight worlds with the same bands they pass, and the same
pooled checks on the pre-C1.2 code give the same rates within noise (trades 0.0062 both; visits 0.025 vs 0.022; takings 28 vs 20 lived) -
the time use of a lived day is unchanged (woodcutting 0.5 h a grown person-day). 300 years DEEP (`tc2/out/civ/s42_c12a`, `s7_c12a`): seed
42 felled 41,600 t, its woods receded past a mark 24 times, woodlots 766 m out on average at year 300 (farthest 1.45 km); **arable land
1,854 -> 2,570 ha and people 5,295 -> 7,481** (seed 7 1,732 -> 2,034): cleared fields are fully arable. Mast gathered fell 3.5x (seed 42) as
the woods near villages went, hares rose (open land), pigs fell (195 -> 68; seed 7 128 -> 0) with the pannage. 4.2 world years a second.
Shots `tc2/out/shots/civ2`: the year-300 coastal town of 612 in an open farmed heartland, the woods pushed back to the west and the coast.
**Consequences.** Mature 6 km worlds now carry more people than D2-085 measured (its 2,000-7,100 was held down by uncleared fields): no cap
is added (D2-089: land decides). Clearing is not yet labour (fields claimed in spring are cleared by the next vegetation step) - a C2
item; nor does clearing yield timber. Pigs depend on woods (pannage) as they did: a pig-poor heartland is a consequence, not a bug.

## D2-092 — 2026-10-07 — Stone from the ground: rock known by seeing it, a quarry at the rock, every load carried home, houses on footings (C1.3)

**Context.** D2-089 (`22` §1-§2): stone needs a source, discovery, extraction, transport, use and scarcity. Before, "rock showing within two
rings" only gated the quern technique; no stone was cut, carried or built with.
**Decision.** (1) **`Core/Places/Stone.cs`**: rock shows where land breaks **80 m or more from a neighbouring land cell** (`Outcrop`: ~9-16 %
of the land on the 6 km worlds; a coast's drop to the sea bed is no rock - the quern's 40 m rule counting sea neighbours marked 44 % of the
land and put rock beside every village) and above the drawn shore. (2) **Known only by seeing it** (`Saw`): where a village's people go -
the hunting, fishing and gathering grounds they walk to (lived and COARSE), its woodlot, a scout's look-out - the nearest dry rock within
**1 km** ("at hand") is kept (`StoneCell`, `StoneDist`); the first sight is history (`StoneFound`, with who saw it). (3) **Want**: footings
for its next houses (40 loads a house, up to three houses ahead) while it knows rock (`StoneWant`, read daily). (4) **The quarry** (`Structures`
kind Quarry, Zone.Site) is planned at the rock it knows - after water and a store, never while walls rise; its cause the rock's finding; a
quarry the sea takes makes the village forget that rock (one village planned 39 quarries on drowning rock before this). (5) **Quarrying**
(ActivityKind.Quarry, WhyCode.Stone; lived choice at a village work's urgency, COARSE in the free hours after building): ~3 loads an hour of
rubble (body and tools), every load carried home both ways (`Stone.Rate`), COARSE paying the walk out once a stint; stone into
`Settlement.Stone`, the quarry's face (`Stock`, `Use`) growing. (6) **Footings**: a house raised with 40 loads in store stands on them
(`Building.Mat` 1): twice as long before it falls once left, its ruin half again (`Homes.FallYears/RuinYears(in Building)`); without, a house on
earth-fast posts. (7) **Work order**: a palisade being raised goes before every work but water and the first store (`ProjectKind`): a quarry
begun earlier no longer delays the walls. (8) View: houses on posts (kit `house_*_posts`, no plinth) vs on stone footings; the quarry face
scaled by what was cut; cut rock under it (no fields, plots or woods drawn over a quarry); stone stacked by the store; the building page
(quarry: rock distance, loads cut, hours, store and want; house: footings or posts); shot views `work<kind>`/`work<kind>far`. (9) Sim
version 35.
**Evidence.** 195 tests (`StoneTests` 5: rock known where its people saw it, first sight history with the seer; a village that wants stone
opens a quarry at the rock with the finding as cause; quarrying cuts and carries, the walk costs; a house raised with stone stands on
footings and lasts twice as long, without stone on posts; quarrying at both resolutions - 6 days 146 loads lived, 108 COARSE). 300 years DEEP
(`tc2/out/civ/s42_c13d`, `s7_c13d`): seed 42 - 13 of 19 villages know rock, 13 quarries 355 m out on average, 88,600 loads (2,200 t) cut in
45,400 h, **2,079 of 3,278 houses on footings**, 7,037 people; seed 7 - 6 of 10 know rock, 5 quarries, 926 of 1,192 houses on footings, 2,369
people (populations at par with C1.2's). Shots `tc2/out/shots/civ3`: Taekdal (seed 7, 298 people) - stone stacked by the green's well and
granaries, its quarry ~350 m out among the fields on cut rock.
**Consequences / open.** The benefit of footings to a household is only the house's longer life once left: lived-in homes are kept
implicitly (D2-059), so the cheaper upkeep of a house on a stone sill is not yet simulated (C13: aging and rebuilding). The choice to quarry
is the local tradition where rock is at hand, not a reckoning of cost (C11 makes such traditions explicit). Lived quarrying overshoots the
want by parallel starts (the store fills past it); COARSE stays under: timing differs, not the long-run use. The drawn relief is smoothed,
so a quarry's crag shows only in its model and the rock under it.

## D2-093 — 2026-10-07 — The smithy: a smith's workshop, arms forged only there, fighters who strike harder with them (C1.4)

**Context.** D2-064's smiths smelted copper at an open hearth at home (half their making hours while the village was short of tools and ore
was near); metal tools were the only metal good and they doubled as arms (`Conflict.Strength` x (1 + 0.3 x the tools' share)). D2-089: a
smithy must be a real system - a real smith, fuel, ore or metal, a building that is the only place certain work happens, and that takes the
work with it when it goes.
**Decision.** (1) **`Core/Places/Forge.cs`** and the kind `Structures.Smithy` (Zone.Edge: on the main road past the homes; 160 h, 20 wood;
a hearth of 16 loads of stone where the village has them). It is planned when the village knows metal (a competent smith; its cause the
`TechniqueKnown` event, now kept as `Settlement.SmithEvent`) and has use for it - tools short of metal with ore near, or arms wanted - after
water and a store, never while walls rise. (2) **At the smithy** a smith works at twice the open hearth's pace on three-quarters of its
fuel a kilo; with a smithy the open hearth is no longer used. Lived: `ActivityKind.Smith` (WhyCode.Forge) - the smith walks to the smithy;
COARSE: up to 0.6 of a smith's free hours there. (3) **Arms** (`Settlement.Arms`): forged only at a smithy, a kilo of metal and two hours each
- from smelted ore, or by reforging the village's tools where none is near (arms cost tools: a real trade-off) - by a village that **fears
its neighbours** (a palisade raised or rising, a wrong of 50 FU or more held against a neighbour, a night watch kept), up to 0.35 a head;
lost at 3 % a year; history `ArmsMade` (the first arms of an arming, in response to what it fears). (4) **Fighters strike harder**:
`Conflict.Strength` x (1 + 0.3 x the share of the village's fighters it can arm) - so bands weigh armed defenders and armed raiders dare
more. (5) **Rings enclose works**: the palisade's radius counts the green's and the edge's works (stores, barns, the smithy) as well as homes;
no work or new home is laid astride a standing or rising ring (`Structures.OnRing`). (6) View: smoke from the smithy's chimney only while a
smith works there; the building page (pace, hearth, hours, the village's tools and arms and what it fears); shot view `work12`. (7) Sim
version 36.
**Evidence.** 200 tests (`ForgeTests` 6: a smithy where a smith has use for metal, its cause the smith, at the edge of the homes, none
without a smith; twice the metal on three-quarters of the fuel; a threatened village forges arms to its want, history tells it, its
fighters strike 1.3x, arms wear ~3 % a year; no smithy no arms; smiths at the smithy lived (seen "forging arms: Tothindby fears its
neighbours") and COARSE). 300 years DEEP (`tc2/out/civ/s42_c14c`, `s7_c14c`): every village has a smithy by year 300 (19 of 19, 10 of 10 - most
know metal by then and nearly all keep a watch or hold a grudge), 18 of 19 hold arms (2,589 for ~7,200 people); raids seed 42 8 (4 driven
off) against 19 (6) at C1.3, seed 7 14 (10 driven off) against 21 (11); people 7,200 / 2,315 (C1.3: 7,037 / 2,369). Shots
`tc2/out/shots/civ4*`: Souhope's smithy at the edge of its homes, smoke from its chimney, its smiths at the open front.
**Consequences / open.** A smithy in every village by year 300 is historically ordinary; arms almost everywhere follow from the watch being
kept nearly everywhere in these scarce worlds (a measure of fear to refine with C6's training and C10's leaders). The ring does not grow
with the town (C7): homes now stand outside old palisades and some rings cross the shore.

## D2-094 — 2026-10-07 — Training grounds and fighting skill: fighters made by practice and by fights survived (C1.5)

**Context.** D2-089 (`23` §1, §4): every fighter is a persistent person; training consumes time and improves real people; veterans carry
experience and can die with it. Before, strength in a blow came from health, age, wounds and the village's metal only.
**Decision.** (1) **`Person.SkFight`** (0..1, saved, hashed) and **`Person.Fights`** (fights lived through). (2) **`Core/Places/Training.cs`**: a
village that fears its neighbours (`Forge.Threatened`: a palisade, a heavy wrong held, a watch kept) lays out a **training ground**
(`Structures.Ground`, Zone.Outer beyond the homes away from the fields; 30 h, 4 wood; its cause what it fears) once it has water and a
store. (3) Its willing people - 16-45, fit, boldness 90 or more, not near a birth or nursing a young child, not hurt - practise a two-hour
session every third day (by their own turn) in the afternoon: lived `ActivityKind.Train` (WhyCode.Train) at the ground; COARSE the same
sessions from the free hours. (4) Skill: 0.004 an hour of what is still to learn, x (1 + 0.5 x the village's best fighter's skill: a
trainer); a blow given or taken teaches 0.04 of what is left (`Veteran`: raids and fights at stores); it fades 5 % a year unused (10 % past
fifty) and dies with the person. (5) **The trained strike harder**: `Conflict.Strength` x (1 + 0.6 skill). (6) Building page (practice hours,
how many have skill, the best, whether it is used); person page (fighting skill, fights lived through); run report `training:` line. (7) Sim
version 37.
**Evidence.** 204 tests (`TrainingTests` 4: a fearing village lays out a ground beyond its homes, not astride its ring, in response to what
it fears; practice toward the ceiling, a trainer lifts it, unused it fades ~5 % a year, the village's best is known; a blow teaches as much
as ten hours' practice, half-trained strike 1.3x; the bold practise lived (20 h in six days) and COARSE (16 h)). 300 years DEEP
(`tc2/out/civ/s42_c15a`, `s7_c15a`): seed 42 - 16 grounds, 1.7 M hours of practice (~0.5 % of all labour), 2,378 living people with some skill
(the best 0.80), 647 who have fought; raids 18 (10 driven off); 7,350 people. Seed 7 - 8 grounds, 714 with skill, raids 27 (20 driven off);
2,354 people (at par). Shots `tc2/out/shots/civ5`: Souhope's people drilling at its ground by the shore in the afternoon.
**Open.** No arms or skill carried into a band's choice yet beyond strength (C6: who goes, retainers, roles); training is not seen as
movement (no drill animation); the ground's posts are too thin to read from the village view (a beaten-earth patch is the next look item).

## D2-095 — 2026-10-07 — Markets made by traders; palisades on dry ground (C1.6)

**Context.** D2-089 (`21` §2, `24`): a market must be actual exchange. Trade (D2-067) was a trader walking a load to a neighbour within a
day's walk (6 km) and dealing at the middle of the two villages' values. The shots since C1.1 showed palisade rings drawn through the sea.
**Decision.** (1) **Traders' coming is counted**: each deal made at a village adds one to its `TradeIn`, a count halving each year. (2) A
village with water and a store, 100 people or more, and **10 or more traders a year** lays out a **market** on its green (`Structures.Market`,
Core zone, 60 h, 15 wood; the building keeps how many came the year it was laid out). (3) **What a market does**: traders walk to a market
from up to **9 km** (a long day's walk; others 6 km) and weigh a deal there **1.2x** (more buyers and sellers to match a load with)
(`Trade.BestPartner`) - so trade concentrates on market villages and a central place is made by its traders; visitors gather at the stalls
rather than the green's middle. (4) **Palisades on dry ground**: the share of a ring standing on dry ground (`Structures.DryShare`, 72
points against the drawn shore) is kept when a palisade is planned (`Settlement.WallDry`); its labour and timber are for the dry stretches
only, and the view sets no posts in the sea or a lake. (5) The palisade's gates stay drawn gaps on the roads (structures with C7). (6)
Building page (traders a year, the reach); run report `markets:` line. (7) Sim version 38.
**Evidence.** 207 tests (`MarketTests` 3: a village traders come to often lays out a market, the count halves in a year; a market 8.8 km off
draws a trader who had no partner within a day's walk (gain 0 -> 137); a coastal ring 60 % on dry ground costs 60 %). 300 years DEEP
(`tc2/out/civ/s42_c16a`, `s7_c16a`): seed 42 - **2 markets** (Heldholm, 658 people, ~10 traders a year), trades 1,257 (1,054 at C1.5); seed 7
- none (its busiest village sees one trader a year: trade there is thin and grudges close it); people 7,241 / 2,410 (at par). Shots
`tc2/out/shots/civ6`: Heldholm's green - well, granaries, a barn, the market's stalls and a midday crowd.
**Open.** Trade volume is low everywhere (~4 deals a year in a world of thousands: `Trade.TripsPerAdultDay` and grudges); fairs, more goods
(stone, arms, salt, pots), carts and boats (C4, C9) will make markets matter more. Market days (a weekly gathering) and stalls kept by
households are later.

## D2-096 — 2026-10-07 — C1.7 the pressure premise, observed; C1 "the working town" closed

**Context.** `25` §2: after the working town's structures, create pressure - settlements competing for scarce useful things - and observe
whether trade, negotiation, migration, taking, raids, fortification or conflict emerge through the rules, never scripted.
**What was run.** Tier-A worlds (D2-089 §3): `run --settlements 3 --founders 40` (~120 people at founding) for 200 years DEEP on seeds 42,
7 and 11 (`tc2/out/civ/s{42,7,11}_tierA.txt`, saves at years 25, 100, 200), read through the new `run --civ N` report: each village's
civilisation in the order it happened, every line with its cause.
**What emerged.** (1) **Seed 42 - peace**: land was plentiful; villages budded daughters (Velhope, 43 people of 10 households of Heldholm,
year 159) instead of fighting; Heldholm's own chain: rock seen 0.8 km off (year 4) -> a quarry -> granaries at each overfilled harvest ->
barns (from year 103, harvests of 12,800-37,000 FU) -> its woods cut back to 0.7 km (114) and 1.1 km (147) -> copper and the ard learnt late
(194, 198); Gouthorpe on its coast lost homes, its well and its granary to the rising sea and rebuilt them (well again at 110, granary at 140).
(2) **Seed 7 - famine into walls**: Taekdal's woods receded (89, 117); famine at 165 -> takings at night between three neighbours -> a
watch (166) -> a training ground the next day -> a palisade (166, its cost "125 days' food a year") -> years of famine, refused amends,
blows at stores; Thestford likewise (watch 170, ground 170, palisade 175 at 285 days' food a year lost). No smiths (no copper): no arms.
(3) **Seed 11 - an armed feud**: Greithstead had smiths from its founding (copper near; a smithy in year 0); famine from 166 -> takings ->
a watch, a training ground and **arms forged from its ore** (167) -> a palisade (170) -> raided by armed, walled Skornskouton (a band of 12
won at 179, beaten off at 183 and 188) -> it raised its own bands against Kreiskrouldby over a death that village would make no amends for
(184, 189, 194); 13 raids in the world, 4 tributes agreed and ended. In all three the chain runs from physical scarcity through deeds -
never from a war flag. **What did not emerge** (and why): competition over a deposit itself - rock, ore and woods are used by whoever is
near, with no claims, no exclusion and (but woods) no depletion, so they are not yet worth fighting over; that needs claims on sites and
deposits that run out (C5, C10).
**C1 closed** (D2-090 to D2-096): structures by kind with works by need and their causes; timber from real woods; stone quarried and
carried; the smithy and arms; training grounds and fighting skill; markets; rings on dry ground. Acceptance shot `tc2/out/shots/civA_sheet.png`
(seed 11, Greithstead #2, years 25 and 200, village/close/street views): a hamlet of 84 by its woods becomes a walled town of 358 in a
cleared farmed land, its old shore lots drowned ruins, its green kept by granaries and a well. 207 tests; sim version 38. Structures keep
their history while they stand (local events of a building are no longer dropped after 200 years). Seen, left for later: people walking a
route across cells the sea has since drowned (routes after coast changes); rings that do not grow with the town (C7); works hard to tell
apart from homes at the village view (look slice).

## D2-097 — 2026-10-07 — Roads by use: the ground between villages worn by their people's walking (C4.1)

**Context.** The D2-084 audit and D2-089: the view drew a road between every pair of villages within 4.5 km whatever their use (theatre),
and only within ~48 m a vertex (no roads at the region view). `24` §3: roads emerge from use - repeated travel makes paths, heavy use
improves them, abandonment lets nature take them back.
**Decision.** (1) **`Core/Agents/Ways.cs`** (`World.Ways`, saved and hashed in key order): every pair of villages keeps the **wear** of the way
between them - walks counted, halving each year it is not renewed, read with its decay at any moment (no daily pass; pruned yearly when
worn to nothing). (2) **What wears it**: every lived travel leg between two villages (`AgentSystem.BeginTravel`); COARSE visits (`Disease.Visits`:
there and back, spread over the neighbours by nearness as the visits are), COARSE trading trips (2 a trip), raids (2 for each of the band).
(3) **States**: a **track** from 4, a **path** from 40 (walked some 28 times a year), a **road** from 250 (beaten broad by some 170 walks a year);
history `WayWorn` (Local) the first time a way becomes a path and a road. (4) **Quicker underfoot**: a lived walker on a path goes 1.08x, on a
road 1.15x. (5) **The view draws ways by use only**: the ground's roads (TerrainLod) by how worn they are - narrow and faint to broad and dark
- and a ribbon along each worn way (`WaterView.BuildRoads`, `Road.shader`: the river's ribbon with packed earth; the beaten roads kept ~2 px
wide far off so the network reads from the region and orbit views); never over the sea; a way nobody walks fades and is gone. (6) Palisade
builders and menders stand on dry stretches of the ring (they stood in the sea where it crossed the shore). (7) Sim version 39; `Road.mat`
in ProjectSetup (run `unity.mjs configure` before a Web build). Cleared and kept roads, bridges and carts are C4.2.
**Evidence.** 210 tests (`WaysTests` 3: walking wears a way, it halves each year unwalked and is pruned, a path is history; a path 1.08x and a
road 1.15x; villages wear their ways lived (45 walks in 24 days) and COARSE (49)). 300 years DEEP (`tc2/out/civ/s{42,7}_c41a`): COARSE outcomes
unchanged (7,241 / 2,410 people - the ways only record and speed lived walkers); seed 42 at year 300: 98 ways walked, 74 paths and 17 roads;
seed 7: 37 paths, 2 roads. Shots `tc2/out/shots/ways1`, `ways2`: roads leave Souhope west across its river and south through the fields at the
village view; at the region view thin earth lines link the villages; from orbit a faint network.
**Open.** Nearly every neighbour pair is a path by year 300 (the COARSE visit rate, 0.03 a grown person-day, is twice the lived one: D2-091's
pooled checks); roads are the arteries between big near neighbours. Routes are recomputed as cells; the ribbon follows the route's cells.

## D2-098 — 2026-10-07 — Rings that grow with the town; walls before hut upgrades; works no longer stall a hair short (C7.1)

**Context.** `23` §8: as towns grow, old walls may become internal structures and larger walls may be built. The C1 shots showed towns spread
far beyond palisades laid out when they were villages. Investigating why so many palisades stood "rising" for a century found two faults:
everyone living in a hut made it a house before any village work (D2-045's order), and - the real stall - a work's hours are kept as floats:
a palisade 0.000016 h short of its (non-round) need added that much and rounded back to the same float, for ever (latent since D2-074; likelier
once C1.6 made ring costs non-round by their dry share).
**Decision.** (1) **A larger ring** (`Conflict.Defence`): a walled village whose lived-in homes beyond its ring are at least four and a fifth of
its homes weighs a ring round them (`WallRadius`, enclosing homes and the green's and edge's works) - begun when the losses that walled it,
saved for a wall's years, still outweigh the new ring's labour and timber (its dry stretches). History `WallPlanned` (ArgB 1: "a larger
palisade: N of their homes stood outside the old one"); metric `RingsGrown`. (2) **The old ring stays inside** (`Settlement.OldR`, `OldCond`):
unmended, it rots in its thirty years and is gone; while the new ring rises it still guards (`Conflict.Walled`: a taker is seen, defenders
hold their ground); nothing new is built astride it. (3) **Walls before comfort**: a household in a hut makes it a house only after the well
and **not while a palisade is rising** (a roof over a camp still comes first); a lived builder walks to where the hours go. (4) **A work is
done within 0.01 h of its need** (`Construction.Done`: homes, village works, palisades, structures). (5) The view draws both rings. Sim
version 40.
**Evidence.** 211 tests (`DefenceTests.ATownThatOutgrowsItsPalisadeRaisesALargerRingAndTheOldOneRotsInside`: Kubroford, five homes outside,
begins a larger ring; the old still guards; thirty years on it is gone). The stall: seed 7's Voton had 2,268.1536 h of 2,268.15358 needed at
years 250 and 300; with the fix its wall stands. 300 years DEEP (`tc2/out/civ/s{42,7}_c71d`): seed 42 - **18 walled villages, none stuck
rising** (before: 8 walled, 9 rising), 5 larger rings begun, one old ring still standing inside at year 300; seed 7 - 10 walled (3), 2 larger
rings; tributes 7 agreed (3 ended), 3 lords at year 300. People 7,137 / 2,063. Shots `tc2/out/shots/rings1`: Thathastwick (417 people) - the
old palisade through its houses, the larger ring beyond, roads leaving its green.

## D2-099 — 2026-10-07 — The person page: a life read from state and the ledger (C12.1)

**Context.** `07` §9 (D2-089, non-negotiable): clicking a person must show a person, and one life must be followable end to end. Until now a
person was one small card (name, what they do, health). Some of §9 needs state the world does not keep: roles "from this year's hours"
(no per-person hour tallies), places and deposits known through a person (knowledge of places is per village), teachers and pupils, friends
and rivals, per-person debts and trades.
**Decision.** (1) **`Core/History/Biography`** builds the page from state and the ledger, reading only: *identity* (born when, where - the
birth's record - and to whom; home and household), *now* (where, doing what and why, until when; health, food, the body's reserve, wounds,
sickness, a child due; what they carry), *family* (parents, partner, children living and dead, brothers and sisters - half-siblings marked -
grandchildren), *work* (skills, crafts and how well they carry them, the works they led from `WorkBuilt`), *deeds and force* (fighting skill,
fights lived through, strength only for one who could come out to a fight - `Conflict.Fit`, now public; the band they are with; the deeds the
villages still weigh; tribute and grievance of their village), *home* (the household's home and its year, larder, water, warmth, clothes and
tools a head, animals), *life* (every record they are part of, in order). (2) **Roles are read from practice**, never assigned: "known for"
a smith at the smithy, a seasoned farmer, a hunter, a fisher, a woodcutter and builder, a potter, a weaver, a fighter - from skills and crafts
(practice accumulated), not from hours; per-person hour tallies wait until a role needs them (C10/C11). Skills are worded as a knack grown by
practice (newborns start with an aptitude). (3) Warmth indoors is shown only when it is below comfort (the roof's degrees are added on hot
days too, a simplification that matters only in the cold). (4) **HUD**: "Life page" beside Follow opens a page with tabs Now, Family, Work,
Deeds, Home, Life, rebuilt once a second; WebBridge `person PID|oldest [TAB]`; the Web test shoots `9_person` and `9_person_life`. (5)
**Headless** `person --load SAVE [--pid N | --pick oldest|smith|fighter|elder|child] [--village ID]` prints the same page. No state change
(sim version 40).
**Not yet** (needs state first): places and deposits known through a person, teachers and pupils, friends and rivals, debts and trades per
person, roles by hours. Each comes with the slice that makes the state (C5 deposits, C10 governance, C11 culture).
**Evidence.** 214 tests (`BiographyTests`: the page from state and the ledger - the mother's page counts the child, every line of a life is one
of their records in order and nothing else; roles appear and vanish with practice; reading every page of a lived world leaves its state hash
and ledger unchanged). Unity compiles (shot `person1`). Year 300 of `tc2/out/civ/s42_c71d`: Sothskei Deldho of Velhope, 39, farmer, smith at
the smithy, potter and weaver, 14 fights lived through, strikes at 2.48; nine deeds still weighed (two killings and seven woundings of night
takers from Thathastwick, Heldholm and Draton); Velhope holds a wrong against Thathastwick; his life lists every beating at the store between
his children's births. The page's layout is checked in the next Web build (IMGUI is not drawn in batch-mode shots).

## D2-100 — 2026-10-07 — Copper from the ground: finite ore bodies, seen, mined, carried and dug out (C5.1)

**Context.** `25` §4 (the resource town) and `22` §2: deposits come from geology, are known by seeing, deplete, and make history. Until now
copper was a flag on a village's cell (D2-064: rock within two rings and a volcano within 3 km): its smiths smelted metal from nothing but
hours and wood, forever, with no ore, no mine, no carrying and no end. Deposits were not worth walking to, trading for or fighting over.
**Decision.** (1) **Ore bodies** (`Core/Places/Mining.cs`, `World.Deposits`): copper lies on crags (the quarry's outcrop: land breaking 80 m
from a land neighbour, above the shore) within **2 km (geo) of a volcano**, living or quiet, on **15 %** of such crags (keyed by the cell,
`RngStream.Geology`), each body holding **1-20 t** of workable ore (log-even, keyed). A body enters the world's state the first time anyone
sees it and is counted down by what is dug, whoever digs it (two villages working one body share it). (2) **Seeing**: wherever the quarry's
rule looks (grounds, woodlot) a village sees copper too (`Stone.Saw` -> `Mining.Saw`); it keeps the nearest body with ore left within
**2 km** of its anchor; the first villages' founders saw what lay within 1 km of their site (`Mining.Look` before `Learning.Founders`); copper
known again after a body gave out is history (`OreFound`). Finding the technique needs copper the village has seen (was: ore near its cell).
(3) **The mine** (structure kind 17, at the body; 40 h, 6 loads of wood): planned when the village knows copper, someone of it can smelt
(best skill 0.3, the hearth's rule) and its ore in store is short of what its smiths want - metal for its tools (0.5 kg a head) and arms when
it fears its neighbours, 5 kg of ore a kilo, up to 1.5 t ahead (`Settlement.OreWant`, read daily). (4) **Digging** (`ActivityKind.Mine`,
`WhyCode.Ore`; COARSE in the free hours like the quarry): 25 kg an hour at the face, every load carried home (Stone's law: the walk both ways
per load); a lived stint is as long as the ore still wanted takes and stops at it (others dig too). (5) **Smelting takes ore**: at the hearth
and the smithy 5 kg of ore a kilo of copper - no ore in store, no metal; D2-064's rates and fuel stand. (6) **A body gives out** when its last
ore is dug: history `OreGaveOut` (caused by the find), its mines are left, every village that knew it forgets it and looks again. (7) The view
draws the mine (kit `mine_pit`: a shaft with a windlass, a spoil heap growing with the ore dug, green and blue ore sorted on a floor, a
lean-to; drawn 1.5x life to read at the village view) on broken ground; the building page tells the body's ore left or when it gave out;
headless `ores:` report line; civ timelines show finds and give-outs. Sim version 41.
**Tuning.** First try (half the crags within 3 km geo of a volcano, dug from 3 km): every village knew copper (17 of 18 on seed 42, 9 of 9 on
seed 7), metal was everywhere and trade fell to a third (markets 7 -> 1). On the 6 km planet a 3 km walk is half a radian of arc - several
villages' lands - and the arcs' ground covers much of these volcanic worlds. Tightened to 2 km (geo) round the arcs, 15 % of crags and a 2 km
reach (25 % gave 14 of 19 and 8 of 10).
**Evidence.** 220 tests (`MiningTests` 6: copper only on crags near arcs in finite keyed bodies; known only by seeing, one body however often
seen and shared between villages; a village short of metal opens a mine at the body, the walk costs per load, the body loses what is dug;
no ore no copper, five kilos a kilo; a body gives out, the mine is left, the village forgets it and does not know it again; both resolutions
dig at par - COARSE 49 kg in 5.5 h, lived 52 kg in 5.7 h at 552 m). 300 years DEEP (`tc2/out/civ/s{42,7,11}_c51d`): seed 42 - 12 of 19
villages know copper, 12 mines ~970 m out, 23 bodies seen, **7 dug out**, 64 t of ore dug in 4,141 h, metal smelted 12.7 t (C7.1: 15.6 t),
people 6,556 (7,137), markets 3 (7), trades 696 (1,592); seed 7 - 7 of 9, 15 bodies seen, 5 dug out, 51 t, metal 10.1 t (9.0), people 2,044
(2,063); seed 11 - 4 of 15 know copper but nobody learnt to smelt: no mines, no metal. Chains in the annals: Tousholm saw copper 1.9 km off
(year 222), dug it (226), got smiths and a smithy (242), forged arms from its ore when it feared its neighbours (244); the body gave out after
9.7 t (294). Velhope's gave out after 2.4 t (265) and it saw another 1.8 km off the next year. Shots `tc2/out/shots/mine1`, `mine2`
(Heldholm's mine on its broken ground among the fields).
**Seen and left.** COARSE smelts at the hearth only in craft hours when a household's goods are short, a lived day more often (D2-064,
pre-existing: 1.8 vs 9.2 kg in six days with ore in store); fields are drawn over the crag the copper shows on; ore is not traded yet.
**Next (C5).** C5.2 iron: bog ore where water stands on flat ground, charcoal from a clamp at the woodlot, the bloomery, iron's larger use a
head (tools, nails, arms). C5.3 distant work: smelting at a far mine (carry metal, not ore), stints of days with a camp at the site, households
moving to the work (a mining hamlet founded by work). C5.4 claims: a body a village works is known as theirs; others digging it is a deed;
news of bodies through traders; a deposit worth holding (tribute, raids).

## D2-101 — 2026-10-07 — Charcoal burnt at the woods: smelting burns charcoal, made from felled forest at a hearth that follows the woods (C5.2a)

**Context.** `22` §1: charcoal is the link between the woods and metal (a clamp: ~5 kg of wood a kg of charcoal, days of watching); iron
(C5.2b) will need ~15 kg of charcoal a kilo. Until now smelting took "charcoal" straight out of the village's woodstore (D2-064:
`Tech.MetalFuel`, 0.4 loads a kilo) - no burners, no clamp, no place, nothing to see.
**Decision.** (1) `Core/Places/Charcoal.cs`: a village whose people can smelt and that has ore or knows copper wants charcoal for the metal
its tools and arms still want (2 kg a kilo of copper at an open hearth - the same 10 kg of wood D2-064 counted - three-quarters at a smithy;
up to 400 kg ahead; `Settlement.CharcoalWant`, read daily). (2) **The charcoal hearth** (structure kind 18, `Structures.CharHearth`; 12 h,
no wood): laid out at the woodlot (`WoodX/WoodY`); when the woods have receded more than 300 m past it the old hearth is left and the next is
laid out at the woods - charcoal follows the forest. (3) **Burning** (`ActivityKind.Burn` 32, `WhyCode.Charcoal` 40; COARSE in the free
hours): the burners fell at the woodlot by the woods' own law (`Woods.Fell`: each load's mass off the forest share, the walk per load) and
stack, earth over, fire and watch - 0.6 h a load beyond the felling; a load makes 5 kg; nothing goes to the homes' woodstore; the clamp
smoulders three days (`Settlement.ClampUntil`); a lived stint is as long as the charcoal still wanted takes. (4) **Smelting burns charcoal**
at the hearth and the smithy - none in store, no smelting (`Forge.Use` wants ore and charcoal); `Tech.MetalFuel` is gone. (5) The view: kit
`charcoal_clamp` (a blackened floor, the earthed clamp with turf and its vent, billets, a rake), 1.4x life; smoke from the vent while the
clamp smoulders; the building page; headless `charcoal:` line. Sim version 42.
**Evidence.** 225 tests (`CharcoalTests` 5: a hearth laid out at the woodlot when smiths want charcoal, none without a smelter; burning
fells the woods and a load makes five kilos, none into the woodstore, the clamp smoulders; no charcoal, no copper - as far as the charcoal
goes; the next hearth at the woods when they recede, the old one left; both resolutions at par - COARSE 18 kg in 5.4 h, lived 19 kg in
5.3 h; `MetalTests`: a village of smiths now digs its ore and burns its charcoal itself in its first year). 300 years DEEP
(`tc2/out/civ/s{42,7}_c52a`): seed 42 - 14 hearths at the woods, 21.9 t of charcoal from 109 t of wood in 8,070 h (against 40,600 t felled
for homes and hearths: copper is a small trade), metal 13.6 t (C5.1 12.7 t), 8 copper bodies dug out, people 7,629; seed 7 - 8 hearths,
16.7 t from 83 t of wood, metal 10.9 t, people 2,329. Shots `tc2/out/shots/char1` (Heldholm's hearth at its thinned woodlot).

## D2-102 — 2026-10-07 — Iron: bog ore, the bloomery found by copper smiths, iron tools and arms; a COARSE smith's labour day (C5.2b)

**Context.** `22` §1-§2 and `25` C5: iron is the metal of a medieval economy - bog ore where water stands, the bloomery (~8 kg of ore and
10-15 kg of charcoal a kilo of bar iron), tools for every household and better arms. It needs the charcoal of C5.2a, and it is what turns
the woods into a resource worth fighting over.
**Decision.** (1) **Bog ore** (`Mining.Iron`, a second deposit kind): beds where the ground lies wet - a lake, a brook draining through (river
flow 300 m3 a day or more), or a wet climate's fen (moisture 0.7 or more) - on flat land (no neighbour 12 m above or below), above the shore,
in 35 % of such places; 0.5-8 t a bed (keyed); **a bed dug out forms again in forty years** (iron carried in by the water). Known by seeing
as copper is (`Settlement.FeCell/FeDist/FeEvent`); **bog-ore pits** (structure kind 19, 16 h) at the bed; digging is the mine's activity with
`WhyCode.BogOre`; the store `FeOre`. (2) **The bloomery** (`Tech.Iron`, the seventh technique; `Person.KIron`, `Settlement.BestIron`): found
at a smithy (2e-5 an hour, by curiosity) only by a competent copper smith whose village has seen bog ore; copied and carried as every
technique is; nobody begins knowing it. (3) **Iron at the smithy** only (`Forge.Work`): arms first while wanted (of iron where this smith
smelts it, else copper, else reforged tools), then iron for tools, then copper for tools iron does not yet serve; 0.03 kg an hour per skill
(0.06 at a smithy) - 8 kg of bog ore and 15 kg of charcoal a kilo. (4) **Iron's use**: 2 kg of iron tools a head (`Tech.IronPerHead`;
`Settlement.Iron`, rusting 3 % a year); tools quicken hands up to 1.4x and cut the wear of goods to 0.6, replacing copper's 1.25x / 0.75
(`Tech.ToolPace`, `ToolWear`); copper is wanted only for those iron does not serve (`Tech.CopperWanted`) and is worth less in trade where iron
serves; iron arms (`Settlement.IronArms`) strike 1.15x more (`Conflict.Strength`). Charcoal wanted grows with iron (15 kg a kilo; up to 2 t
ahead). (5) **A COARSE smith's labour day**: a year's comparison showed a lived iron smith at the forge ~7.4 h a day and a COARSE one, given
0.6 of the hours left after the household's work and the fields, 2.6 h (iron made 1.7 vs 16.7 kg a year). A COARSE smith with work at the
smithy now gives it the labour day (9.5 h) less the household's own work, before the village's shared field work (others take it up, as in a
lived village); counted as worked, so the evening's making shrinks. Year-long parity now: bog ore 356 vs 354 kg, charcoal 640 vs 666 kg,
iron 18.9 vs 17.3 kg (COARSE vs lived). This also speeds copper smithing and arming in COARSE/DEEP worlds. (6) View: kit `bog_pits` (pits
with brown water, turf stacked, ore drying on boards, a roasting fire, a spade), 1.4x life, on broken ground; the building page; HUD skills
and the person page show the bloomery and "an ironsmith"; headless `iron:` line. Sim version 43.
**Also seen.** In a sowing season a COARSE day gives all free hours to the fields before any village work (a lived day interleaves): a
six-day window can differ several-fold while a year is at par; tests of village works compare years.
**Evidence.** 232 tests (`IronTests` 7: beds only on wet flats, a share of them; known by seeing and formed again forty years after being dug
out; the bloomery found only by a copper smith who has seen bog ore; 8 kg of ore and 15 of charcoal a kilo, none without ore; iron tools 1.4x
and replacing copper; iron arms 1.15x; a year's bog ore and iron at par between resolutions). 300 years DEEP (`tc2/out/civ/s{42,7}_c52c`): seed
42 - the bloomery found three times and known in all 19 villages, 23 beds seen and 20 dug out, 51 t of bog ore, **6.4 t of iron**: iron tools
for 16 % of the people, 1,205 of 2,493 arms iron; charcoal 133 t from 666 t of wood; copper 11.0 t; people 7,570; seed 7 - 14 beds seen, 12
dug out, 98.7 t of bog ore, **12.3 t of iron**: iron tools for 61 %, 715 of 813 arms iron; charcoal 199 t from 994 t of wood (5 % of all
felling); people 2,278. Seed 11 (no copper smiths): beds known, no iron. Voton (seed 7) saw bog ore in year 11, opened pits only when the
bloomery reached it (117) and has dug out bed after bed since, each 1.4-1.9 km out. Shots `tc2/out/shots/iron1` (Voton's pits by the shore).

## D2-103 — 2026-10-07 — Places by their ore: scouts value land by ore their village lacks; founders know the ore round their site (C5.3a)

**Context.** `25` §4 (the resource town): a deposit away from the farming centre should draw people - workers travel, a camp, a settlement.
Until now a village knew ore only within 2 km of its anchor; scouts looked at land only for warmth, water and free arable land; a new
village learnt the ore round it only as its people happened to walk there.
**Decision.** (1) **A village lacks ore** of a kind when someone of it smelts that metal, its tools or arms want it, and it knows no body
within reach (`Mining.Lacks`). (2) **Scouts see ore**: when a scout of such a village looks round where they stand, ore of the lacking kind
in sight is seen (a deposit kept from then) and a site within 1 km of it is worth half as much again (`Mining.SitePull`, `SiteBonus` 1.5);
the place is told as "good land ..., by bog ore their smiths want" (`Scouted`, ArgA the kind) - households leave for the best place known
by the existing rules (D2-045): no forced founding, no "mining town" flag. (3) **Founders look**: every new village's founders look over the
land within 1 km of their site, as the first villages' do (`Mining.Look` in `Migration.FoundAt`), so a place founded by its ore knows it from
the first day. Counters `ScoutedForOre`, `FoundedByOre`; headless `ore sites:` line. Sim version 44.
**Evidence.** 234 tests (`OreSitesTests` 2: wanting iron raises the same site from 0.41 to 0.62 and is told as land by the ore; a new
village's founders know the bog ore 0.4 km from their site). Eight seeds, 300 years DEEP, before (C5.2b) and after: seeds 1, 3, 5, 6 identical
(no smelting there), seed 2 8,621 -> 8,658, seed 4 10,255 -> 10,581, seed 42 7,570 -> 6,360 and seed 7 2,278 -> 1,835 (more famines on their
own trajectories: 49 -> 98 and 80 -> 90). Which seeds get iron is contingent: seed 2 found the bloomery before and not after (9.1 t -> 0),
seed 4 the reverse (0 -> 20.6 t, iron tools for 32 % of its people). Land was told as by the ore 7 times on seed 4 and 9 on seed 7; 13 of 23
villages founded on seed 4 knew ore round their site.
**Not yet (C5.3b).** Work at a distance: smelting at the bed or the woods (carry iron, not ore and charcoal), stints with a camp at a far
site, households moving to steady work; iron and charcoal as trade goods (the hamlet's living).

## D2-104 — 2026-10-07 — Iron traded, and made for buyers known from real sales (C5.3b)

**Context.** A place that has bog ore and smiths can only live by its iron if iron travels: until now it was not a trade good, and smiths
stopped once their own village's people were equipped, so no village ever had iron to spare.
**Decision.** (1) **Iron is a trade good** (`Trade.Iron`): valued by each village by the share of its people its iron leaves unequipped
(8 x (1 - share) + 0.5 a kilo, above copper's 6), spared beyond its own toolkits; traded by the existing want-and-spare deals (D2-067),
both sides gaining or nobody goes. (2) **Made for buyers**: a village's smiths make iron up to its people's toolkits, a tenth more (a few
pieces ahead for whoever comes), and as much again as it parted with in trade over the past year (`Settlement.IronSold`, halving each year)
- demand known only from real sales (`Forge.IronTarget`; bog ore and charcoal wanted follow it). Copper keeps its D2-064 rule. Headless
`iron:` line (traded, the biggest seller lately), `run --civ-of ID` (one village's timeline). Sim version 45.
**Evidence.** 235 tests (`IronTests`: a village with iron to spare and little grain deals iron for grain with one that has grain and no
iron; the buyers' store gets it, the sellers know what they sold and their smiths' target rises by it). 300 years DEEP against C5.3a: seed
7 - 3,390 kg of iron traded, trade gain 8,014 -> 28,777, markets 0 -> 2, people 1,835 -> 2,219, iron arms 666 -> 952; seed 4 - 4,510 kg
traded, gain 22,181 -> 46,393, markets 2 -> 8; seed 42 - 78 kg (its bog beds are dug out: iron is scarce there). The biggest seller on seed
7 is Steldwick (114 people, founded in year 133, walled, three bands driven off its store), 51 kg a year.

## D2-105 — 2026-10-07 — Whose ore: worked bodies belong to their workers; digging another's is a taking (C5.4)

**Context.** `25` §4 ends the resource town with "strategic importance -> conflict or political interest", and C1.7 found deposits were
never worth fighting over: two villages digging one body simply shared it. The conflict machinery (deeds known per village, grievance,
amends asked and refused, taking back, bands, palisades: D2-069 to D2-074) already turns harms into history; ore needed to become something
a village can be wronged over - without a war flag or a "contested" state.
**Decision.** (1) **Worked ore**: a body or bed is worked by the village whose standing mine (or pits) there was raised first
(`Mining.WorkedBy`, read from the structures - no owner field). (2) **The right to dig** (`Mining.MayDig`): freely where nobody works it or
the village works it first; at another's worked ore only when the village's need presses (fewer than half its people with metal tools,
`Mining.Pressing`) or it holds a wrong against the owners (taking back is not taking to them, as D2-069). Mines are planned and stints
chosen only where a village may dig. (3) **A taking**: every stint dug at another's worked ore is a `DeedKind.Take` of the metal it would
have made (good `Trade.Metal`/`Iron`), worth that metal in the owners' own values against their grain, known to the owners (they work beside
the diggers); stints within twelve days add to the same deed; history `OreTaken` (83), caused by the owners' find. Everything after is the
existing machinery. (4) Knowing ore prefers free bodies (another's worked body counts as twice as far); scouts see only free ore (C5.3).
(5) The person page says "dug ore at the works of"; headless `claims:` line and `run --events KIND` (every kept record of a kind with its
cause). Sim version 46.
**Evidence.** 238 tests (`ClaimsTests` 3: the first mine's village works the body; a neighbour with most tools of metal leaves it alone,
with its need pressing or a wrong held it digs there; each stint is a taking the owners know, adding to one deed within days; the owners'
own digging takes from nobody). 300 years DEEP (`tc2/out/civ/s{42,7,4,2}_c54a`): takings of ore on seed 7 (8, 0.8 t), seed 4 (7, 10.8 t),
seed 2 (11, 2.7 t), none on seed 42; people 6,392 / 2,445 / 10,108 / 8,221 (C5.3b: 6,375 / 2,219 / 10,418 / -). On seed 7, Thestford's people
dug at Neldthorpe's copper mine four times in years 281-285; nine days after the last, "Keithvel Drostlaes of Neldthorpe took 33 days' grain
from the store of Thestford in the night", its recorded cause the ore taking - one strand of a feud of night takings, beatings and amends
between them. Drikkawick dug at Brernthorpe's bog pits (seed 4), Taildvehope at Mestskouford's mine (seed 2).

## D2-106 — 2026-10-07 — The settlement page: a place read from state and the ledger (C12.2)

**Context.** `20` §7 and `25` C12 ask for building, settlement and polity inspectors beside the person page. C5 made a village's life much
richer (ore known and who works it, mines, pits, hearths, charcoal, copper and iron, trade, wrongs over ore) but none of it could be read
in one place: a village was a name on the map and a list of records.
**Decision.** `Core/History/Gazetteer` builds a place's page, reading only: its title with a size word read from its people (a hamlet under
50, a village, a town from 300; "an abandoned place"); its founding (by whom, and the record that sent them); its people, households and
homes (houses, on footings, huts, camps) and its palisade; its food (fields, the last harvest, the store and its days, granaries and barns,
rationing, the last famine and how many lived through it); its works standing with the year each was raised, the one being raised, those
left; its land and making (wood and the woodlot, stone, copper and bog ore - the body known, how much is left, who works it - charcoal,
metal tools of copper and iron for what share of its people, arms and how many are iron, iron sold lately); the techniques its people carry;
its neighbours (paths and roads worn to them, traders coming, tribute paid and received, the wrongs it holds and is held to, by weight);
its history. HUD: "Their village" on a person's card and "Its village" on a structure's page open it (tabs Place, Food, Works, Land,
Neighbours, History; rebuilt once a second); WebBridge `place SID|big [TAB]`; the Web test shoots `5e_place`, `5f_place_land`. Headless
`place --load SAVE [--sid N | --pick big|iron|claims]`. No state change.
**Evidence.** 240 tests (`GazetteerTests` 2: the page from state and the ledger - its size word, its standing well, its copper and "worked by
its people" once its mine stands, its history its records only; reading every place's page leaves the state hash and ledger unchanged).
Seed 7 at year 300 (`tc2/out/civ/s7_c54a`): Thestford, a village of 238 - walled, its copper body worked by Neldthorpe, iron tools for most
of its people and 93 iron arms, and the wrongs between it and Neldthorpe weighing 540 and 709 days' food.
**The Web check (person page, C5 works).** A Web build (WebGPU, `tc2/out/webtest/c5web`) shows the person page in the browser as designed
(tabs, close, the card beside it; 57-121 fps through the test's views); the first people now read "born N years before the land's first
year". It also caught a fault only a player shows: the smithy's chimney top was read from its mesh, which a player cannot read unless the
model is imported readable - console errors in the Web build since C1.4. `smithy.fbx` is now imported readable and `PartTop` falls back to
the mesh's top centre for any mesh that is not. The settlement page joins the Web test (`5e_place`, `5f_place_land`) for the next build.

## D2-107 — 2026-10-07 — The lord's call and the lord's protection (C10.1)

**Context.** `20` §5.8 and `23` §6: lords and obligations (a share of the harvest, men for a band); a lordship (D2-075) was only a tribute
flow - its tributaries owed no men, and a raid on a tributary meant nothing to its lord.
**Decision.** (1) **The call**: when a lord raises a band over a wrong, each fit grown person of its direct tributaries within a day's walk
of the foe (by a route) goes as owed - a quarter even of the meek, more of the bold (`Conflict.CallOwed` 0.25 + `CallBold` 0.5 x boldness),
never against their own kin - up to 36 in all (`CallAtMost`). The band remembers it called (`Band.Called`); arrival, disbanding and the
COARSE return gather members from the lord's village and its tributaries (`Conflict.Members`); each goes home with their own share to their
household. History `CallAnswered` (84): "7 people of Kubroford answered the call of Gouthorpe, whom they pay tribute, and went with its band
against Heldholm", caused by the band's raising. (2) **Protection**: a seen raid on a tributary is a wrong to its lord too, at half its
weight, told to it at once (`LordsShare`); the lord's grievance, bands and the call follow. Metrics `CalledBands/CalledMen/LordsWronged`;
headless raids line. Sim version 47.
**Evidence.** 243 tests (`LordshipTests` 3: a lord's band of 12 - 5 of its own and 7 of its tributary - and none called without the tie;
a raid on a tributary weighed by its lord at half; every member home and unloaded after a COARSE fall). 300 years DEEP against C5.4
(`tc2/out/civ/s{42,7,4,2}_c101a`): seed 7 - 15 bands joined by tributaries (128 men), 14 raids taken up by lords, tribute agreed 11 times
(4), people 2,445 -> 1,856, blows 2,187 -> 2,645, wound deaths 144 -> 191; seed 2 - no calls, 19 raids taken up by lords, people 8,221 ->
6,119 although hunger deaths fell 2,794 -> 436 and blows 2,111 -> 1,656; seed 42 6,392 -> 6,590; seed 4 identical (no lords).
**Open (first next step).** Two of four seeds lost a quarter of their people. Seed 7's loss follows more fighting; seed 2's does not
(fewer hungry deaths, fewer blows) and needs following (abandonments, migration, births: `run --civ`, `--events`) before LordsShare and the
call are trusted. Check also whether a raid seen is now weighed twice in the escalation (the tributary's and the lord's bands both answer it). **Followed in D2-108**: no systematic loss over ten seeds; the share was a second wrong (one raid avenged twice) - replaced by the lord answering its tributaries' wrongs.

## D2-108 — 2026-10-07 — The lord answers for its tributaries: one wrong, avenged once (C10.1 followed)

**Context.** D2-107 left open why seeds 7 and 2 ended 300 years with a quarter fewer people. Followed (`tc2/out/civ/s*_c54a` against
`s*_c101a`, ten seeds 42, 7, 4, 2, 1, 3, 5, 6, 8, 11; `scratchpad cmp.py`: paired log-ratios): **no systematic loss** - people averaged
over years 200-299 x0.992 (t -1.2), at year 300 x0.934 (t -1.7, three seeds down at the last year, two up), births, deaths, hunger, blows
and wound deaths per seed at par. The two drops were the trajectories' own regulators, reached on different paths once the worlds
diverged (seed 2 at year 260, seed 7 at 180): on seed 2 two new sicknesses caught from animals (the cattle pox, year 281, 1,640 dead; the
summer sickness, 286, 2,660) where C5.4's world met famine instead (hunger deaths 2,776 against 418); on seed 7 the famines of Voton in
years 285-291 (466 hungry deaths in year 290). A last-year count is too volatile to judge a mechanism by in these famine- and
epidemic-regulated worlds: compare century means. Following the histories found two faults of the lord's share itself (`run --events`):
1. **One raid avenged twice.** The share was a second wrong, the lord's own (a raid deed against the lord's store at half weight): the
   lord's band settled the lord's deed and the tributary's band the tributary's, so each answered the same raid. Seed 7: Steldwick raided
   Askrenstead (279, 281); Askrenstead's lord Gaethdal answered twice with bands of 22 and 21 (ten of them Askrenstead's own people) - and
   in 294 Askrenstead still raised its own band against Steldwick.
2. **The lord's store treated as harmed.** The share counted wherever a village weighs harm done to it: the lord kept a watch and weighed
   a palisade against it (`HarmSince`), and its fed and bold went "taking back" grain at night from a village that had taken nothing of
   theirs.
**Decision.** The lord no longer holds a wrong of its own for a raid on its tributary; it **answers the wrongs its tributaries hold**.
(1) A lord's day weighs, beside its own foe, the heaviest wrong each direct tributary holds (the tributary's own reading of its deeds)
at `LordsShare` 0.5 - so a wrong moves the lord only when twice as heavy as would move the tributary itself (the protection tribute buys
falls where the tributary cannot answer alone); never against another of its own tributaries, never against itself. (2) When that weight
is the heavier, its band goes for it (`Band.Motive` 2, `Band.For` the tributary; the call as D2-107); history: "Nair Douskak raised a band
of 23 in Haesmere to fall on Thestford: they answer the wrongs it did Heiby, which pays them tribute", caused by the tributary's latest
wrong. (3) **Once**: what the band carries home answers the tributary's wrong (`DeedBook.SettleBy` for the tributary); a wrong the
tributary answered itself no longer moves its lord. (4) The lord's watch, walls, taking back and its own grievances read only what was done
to it. Metric `LordsAnswered` (was `LordsWronged`); headless raids line "bands lords raised over their tributaries' wrongs". Sim version 48.
**Evidence.** 244 tests (`LordshipTests` 4: a raid on a tributary leaves its lord with no grievance and nothing its watch or walls would
guard against; a tributary's wrong below twice its own threshold leaves the lord at home, above it the lord's band goes against the foe
naming the tributary, its people answering the call, and the loot answers the tributary's wrong). Ten seeds, 300 years DEEP
(`tc2/out/civ/s*_c108a`) against C5.4: people averaged over years 200-299 x1.002 (t 0.8), at year 300 x0.982 (t -1.2), bands x0.92
(t -1.5), blows, wound deaths, takings, watches and palisades at par; against C10.1 bands x0.92, people x1.01. Seed 7 bands 61 (C10.1)
-> 30 (C5.4: 47), people 2,398 (C5.4 2,445); seed 2 8,173 (C5.4 8,221). Lords answered for tributaries three times in the ten worlds
(Haesmere for Heiby twice on seed 7, Drildbrahope's band of 36 for Uskorthorpe on seed 3); tributaries' people answered calls on six of them (8 bands, 86 men).
Protection is rare because most tributaries answer their own wrongs first - honest to the rule, and the ground C10.2 (leaders,
obligations kept or refused) builds on.

## D2-109 — 2026-10-07 — The Web frame bounded by draw submission: per-instance colours, draws with their own bounds

**Context.** Session 6 read the Web build at 35-56 fps near the ground (target 60). Measured again on a quiet machine
(`webperf`, WebGPU, Chrome, RX 5600M, 1280x720, the C12.2 build): settled, the close view ran 60-70 fps and the forest 65-66, and the
Web test's readings (1.5 s after each camera move: patches built, cover and trees gathered) 34-56. Shadows were half the frame (119-120
fps without them), the woods' trees a quarter. A new reading of the main thread's part of a frame (`FrameClock`: from the first script's
Update to the end of rendering; the bridge's `cpuMs`, printed by `webperf`) showed 11-14 ms of CPU against a 12-15 ms frame: **the CPU
bounded the frame, in draw submission**. Every `Instancer` drew one call per colour group (the woods' trees have 512 tints: shade x the
leaves' season x snow x dryness; people one per dye, houses one per tint), each with the whole world as its bounds, so every call was also
drawn into each of the four shadow cascades.
**Decision.** (1) **Per-instance colour, emission and walk**: `TC2/Instanced` and `TC2/Underwater` read `_BaseColor`, `_Emission` and
`_Anim` from the instancing buffer (passed to the fragment stage), the `Instancer` keeps them per instance (colours converted to linear as
`SetColor` did) and draws its instances in batches of up to 1,023 in the order they were added, whatever their colours - the group key
callers pass orders nothing now. (2) **Each draw carries its own bounds** (its instances' positions padded by the mesh at their largest
scale), so the camera and each cascade cull batches they cannot see; views add instances patch by patch or village by village, so batches
are compact. (3) Tools: the bridge's `cull on|off`, `shadow CASCADES REACH` (WebGPU/desktop; `Sky.GpuCascades/GpuShadowReach`),
posted `cpuMs` and `draws`; `webperf --sets "cmd;cmd|cmd" --reset "..."` tries command sets in turn. Tried and dropped: a low-poly
shadow proxy for the trees (a second draw a batch: more CPU, and the GPU was not the bound - proxy off read 89 fps against 84 with it).
Shadows keep four cascades.
**Evidence.** Instanced draws a frame: 34-37 at the close and forest views. `webperf` settled (4 cascades): close 60-70 -> 90-102 fps
(cpu 9-10 ms), forest 66 -> 100-103; with draws given the world's bounds again (`cull off`) 79-99. Web test (`tc2/out/webtest/s7webb`,
6/6, bit-identical probe, no console errors): village 56.5 -> 101, close 54.6 -> 83, low 56 -> 114, forest 35.5 -> 87.5, forest low
38.6 -> 80.7, close running 39.4 -> 82, the person page 34 -> 75, the place page 47 -> 80, after a century 88 fps (s3webb 47.6); a day
away catches up 96 years in 55 s. Editor shots (`tc2/out/shots/pi1`) and the browser's (`4c_forest` against `c122web`) look the same.
What is left of the CPU's frame (~9.5 ms; ~6 ms without shadows): the terrain's patch renderers drawn into each cascade, the views' own
work - the next places to look if a frame falls short again.

## D2-110 — 2026-10-07 — Boats from the woods: dugouts made, owned and rowed out to the open sea's shoals (C9.1)

**Context.** `24` §5-6 and `25` §5 (the coastal proof: fish shortage or opportunity -> boats -> dock -> fishing -> boatbuilding -> cargo ->
coastal trade -> port wealth). Until now fishing was from the shore only (900 m reach, the shelf's shoals); the view drew "one boat for two
skilled fishers" rowed a little way out (D2-053) - an elaboration of shore fishing with no boat in state.
**Decision.** (1) **A boat is a structure that floats**: a row of `World.Buildings` of kind `Structures.Boat` (20, "dugout", Function.Boat,
Zone.Shore), owned by a household (`Household.Boat`), lying at the village's landing - the beach of its best fishing ground, on dry sand
(`Boats.Landing`). (2) **Wanted from state** (`Boats.Wants`, a village's day: `Boats.Plan`, one begun a day): the village fishes from its
shore and its **sea grounds** - the day's survey of open water within 2.5 km (`WildFood.SurveySea`, `Settlement.Sea`), where a boat
reaches the offshore shoals too (reach max(0.3, 1 - 0.45 offshore) against the shore's max(0.1, 1 - 0.85 offshore)) - promise 1.25 times
the shore's catch an hour, the row paid (`Boats.AfterRow`: a 4-hour trip against the extra row out and back); a grown member of the
household is a fisher (skill 0.5: practice beyond what anyone is born with); the household is housed and not leaving; its woods still
stand with trunks to hollow (the woodlot's forest share at least 0.3) and the store holds the trunk's timber. (3) **Made by its people**:
90 labour-hours (the builder's pace and tools) and 30 loads of the store's timber (`Boats.Work`; lived: a choice like raising a roof,
`WhyCode.Boat`; COARSE: free hours after the village's works); history `BoatBuilt` (85). (4) **Rowed out**: up to two of the household at
once (lived: those out in it; COARSE: the day's seats) take it to one of the sea's grounds when it promises them more than the shore -
`ActivityKind.Sail` lived (rowed out over the water, 3.5-5 h at the ground), the same catch function at both resolutions
(`WildFood.Fish(.., boat: true)`, `Metrics.BoatFishKg`). (5) **The sea's danger**: nobody rows out when the wind over the sea's grounds
reaches 12 m/s; a trip begun under a rising wind (5-12 m/s) overturns the boat with a chance up to 8 % (`Boats.Risk`; a DEEP step's
days are its trips); the boat is lost (`BoatLost` 86, its cause the boat's making) and each aboard drowns with 0.4 (death 12, "at sea,
when their boat overturned"; COARSE kills after its day's loop). (6) **Wear**: a dugout splits in 12 years from its making (read from
time, no daily update) and lies as a hulk for two; an heir in the village takes a boat; a household that moves leaves it; when the sea
rises over a landing its boats are drawn up the new shore. (7) **Seen and read**: the view draws the real boats (being hollowed, at the
landing, old hulks) and people out in them at sea; shore fishers stand at the water's edge (D2-053's elaboration removed); the person page
(the household's dugout), the place page (its fishing: shore and boats, the sea's worth against the shore), the building page and the
headless `boats:` line. Sim version 49.
**Evidence.** 251 tests (`BoatTests` 7: a household that fishes makes a dugout from its woods and the store's timber, history telling who
led it; no trunk to hollow or nobody who fishes, no boat; the same ground fished from a boat gives at least the shore's catch, counted as
the boat's; nobody rows out in a storm's wind, the risk grows with the wind and a calm sea is safe; a dugout splits with age and the
household would make another; an overturned boat is lost with its people in the water, the drowned dead of it; both resolutions fish from
the boat - lived 5 trips / 87 kg, COARSE 6 / 89 kg in the same premise). `CharcoalTests.AVillageBurnsCharcoalAtBothResolutions` restated:
its households have made the boats they wanted (the six days measured were spent hollowing trunks). First tries: with a fisher's skill at
0.2 (below what worldgen hands out at random) every household that ever fished made boats - seed 4 made 6,868 in 300 years (595 standing,
65 at one village), ~620,000 hours and 5,000 t of timber, people x0.95 and runs 9 % slower; a fisher's skill 0.5 brought it to 2-6 boats a
fishing village. Then households began boats with no timber in the store and stalled (79 "being hollowed" on seed 6): the trunk must be at
hand. Then households in hungry villages began boats they never had hands for (seed 6: 2,486 begun, 1,953 given up): no long work is begun
while rations are short. DEEP planned one boat a step, not one a day (fixed: one per day of the step). Ten seeds, 300 years DEEP
(`tc2/out/civ/s*_c91h`, `scratchpad cmp.py`) against D2-108: people over years 200-299 x1.015 (t 0.7), at year 300 x1.005, births and
deaths at par; fish caught x1.65 (t 12), the wild's food x1.09 (t 3.6); at year 300 0-71 dugouts a world (2-6 at a fishing village),
117-999 made in 300 years, 1-23 lost at sea (0-10 drowned); 55-73 % of the fish from boats. COARSE against DEEP (seeds 42, 7, 4, 2,
`_c91i_coarse` vs `_c91i`): boats made at par (0.78-1.38), the boats' share of the catch at par (63-72 % against 66-78 %), DEEP's fish
x0.79 (D2-083's known lean, x0.81). Cost: 1.71 against 1.69 us a person-day (a cache of the sea's worth and wind per village and moment;
DEEP's Retake recomputes only the ground fished). `DeepTimeTests.DeepLivesTheWorldAsCoarseDoes` reads the herds' mix at five moments
over the last eight years (one moment's pigs on a four-village world swung past the band, x2.65, when boats moved the trajectory; mean
x2.15). Headless `run --load S --years 0 --boats SID` lists a village's boats.
**Closed (session 8).** The rerun found the landing still wrong: ten seeds' saves at years 100, 200 and 300 (headless `boats:` line now
counts boats "lying in the water" and "away from it" - no water within 40 m - and names their villages) showed 2-8 boats a world laid in the
square, and in two worlds in the sea where the square had become sea: the ground a village fishes may lie two or more cells out from the
shore, where `Landing` found no land among its eight neighbours, searched only 300 m round the ground's centre and fell back to the
square. Fixed: the shore nearest the ground is found ring by ring of neighbours out from it (`Boats.ShoreOf`, the nearest the village of
the first ring with land; identical to before whenever a neighbour is land), and `Landing` says when it failed. Boats now also follow the
coast both ways (`Boats.ShoreMoved` from `World.LandChanged`: a kept boat the sea rose over, or one it drew back from, is drawn up at the
landing on the new shore - before, only drowned ones moved). A boat is "in the water" only where the ground is at the sea's surface or a
lake covers it (`Boats.Afloat`): one counted wet lay 1.9 m above a sea risen a few centimetres - the 2 m a home keeps (`Shore.Dry`) is not
asked of a boat on its beach. Test `BoatTests.BoatsLieOnDrySandByTheWater` (a ground with no land round it; a boat left inland drawn down).
Ten seeds rerun (`tc2/out/civ/s*_c91k`): every save at years 100, 200 and 300 has no boat in the water and none away from it (four seeds
again on the final rule, `_c91m`: bit-identical to `_c91k`); against D2-108 people over years 200-299 x1.012 (t 0.4), at year 300 x1.020, births and
deaths a thousand person-years x0.998 and x0.999, fish x1.57 (t 11.7), the wild's food x1.08 (t 2.2); seven of the ten worlds are
bit-identical to the run before the fix (only the three that hit the square changed). At year 300: 0-48 dugouts a world, 108-870 made in
300 years, 1-18 lost at sea (0-7 drowned), 46-73 % of the fish from boats. Web build of 9a2ceb7 (`tc2/web-build/web`): Web test 5/5 (`tc2/out/webtest/c91webb`; the bit-identical probe holds; every view 68-120 fps, the person page 82, the place page 92 - a first run, `c91web`, read 53-58 on the pages while headless tests ran beside it). Shots `tc2/out/shots/boats2` (seed 3, year 300, Lostton #20, nine dugouts): the boats lie on the dry sand above the waterline in a row, the walled village beyond; but three lay on one spot (each boat took the slot of how many the village had, so slots of worn boats were taken twice) and about fifteen shore fishers stood out in the sea, up to 100 m off the beach - `WildFood.GroundPoint` had the same fault as the landing (the ground's centre when no neighbour is land). Both are fixed in C9.2a (D2-111). Known, for C9.2: a village's
boats lie at the beach of the day's best fishing ground, so they scatter along its shores (one village's lay 200-700 m from the square on
three beaches; a landing can be ~1 km out) - C9.2 gives a village one landing, kept; boats are begun where the sea is worth 1.25 times the
shore even when both are nearly empty (Toularndal, seed 4: 0.02 against 0.01 FU an hour, eight dugouts in two years - its boats had
emptied the shoals).

## D2-111 — 2026-10-08 — The landing and the crew: a village's boats lie at one beach, a boat goes out with its crew, nobody stands in the sea (C9.2a)

**Context.** C9.1 (D2-110) left three things that read wrong in the game and one that was wrong in the simulation. Each boat lay at the
beach of the day's best fishing ground, so a village's boats scattered along its shores (one village's on three beaches 200-700 m from its
square, some a kilometre out) and, when a slot was reused, on top of each other. In lived days "up to two of the household at once" took
the boat out at their own moments, hours apart: two people at two places in one boat, drawn as two hulls. Shore fishers stood out in the
sea, up to 100 m off the beach (`WildFood.GroundPoint` put them at a ground's centre when no neighbour was land - the fault D2-110 fixed
for the landing). And people at leisure gathered at their village's square when the sea had risen over it (0-5 villages a world by year
300). Measured while fixing the second: C9.1's lived days caught less from boats than COARSE days did (below).
**Decision.** (1) **A village keeps one landing** (`Settlement.LandX/LandY`, `LandUx/LandUy` up the beach, `LandAt`; `Boats.EnsureLanding`,
`Beach`, `Waterline`): the beach nearest its square - the sea nearest it by cells, then the drawn waterline on the way from the square to that
sea (`Shore.At`, the ground the view draws: a cell edge's midpoint lay a hundred metres from the water on a low coast), the landing drawn up
the beach to a metre above the sea (`UpTheBeach`, at most 25 m). Chosen when the village first looks to its boats (`Boats.Plan` asks for a
landing first: no beach, no boats), kept while the sea is within 40 m of it (`AtWater`: the sea's surface itself, `AtSea` - "below a home's
two-metre margin" let a coastal plain pass). (2) **Boats lie in a row along it** (`Landing`: slots a boat's length and a half apart either
side, each drawn down to the waterline there and up a metre above the sea, `Drawn`; none within a boat's width of another, `Taken`). (3)
**The coast moves, the boats follow**: a landing the sea has left or risen over gives way to the beach nearest then, its boats drawn up there
(`ShoreMoved`); and each village-day boats the sea has risen over are drawn up again (`Plan`; the sea's level moves in steps without cells
changing). (4) **A boat goes out with its crew** (lived; `AgentSystem.Crew`, `Outing`): the one who takes the household's boat out is joined
by those of the household at leisure or at their mending (Idle, Social, Craft) who could go to sea - the same ground, back together
(`BeginLocal(.., endAt)`); one who chooses the sea within half an hour of its putting out catches it (`CrewWait`); a boat out at sea is not
taken out again; never more than its seats. COARSE keeps its day's seats (the first a day is the boat's outing). Metrics `BoatOutings`,
`CrewSeats`. (5) **Nobody stands in the sea**: a shore fisher stands at the waterline of the shore nearest the ground (`Boats.Edge` ring
search, cached per village and ground by the coast; `Boats.OutOfTheWater`); people gather at the village's **green** - its square, or where
the sea took the square the dry ground nearest it (`AgentSystem.Green`, cached by the coast and the sea's level) - for leisure, children's
play, visits and arrivals, and a spot near it that falls in the water is moved ashore. (6) **Seen**: the view draws a boat's hull once, at the
first of its crew on the water, the others seated in it; boats on the beach lie bows down the landing's slope; the place page tells the
landing ("the landing N m from the square, since year Y"); headless `boats:` reports landings (range and mean distance from their squares),
boats in the water / away from it / their mean height above the sea, outings and crew seats, and `squares under the sea`. Sim version 50.
**Evidence.** 258 tests (`BoatTests` 14): `AVillageKeepsOneLandingItsBoatsInARow` (chosen with the first boat, the beach nearest the square,
kept; a dozen boats in a row, each by the water, none on another), `ABoatGoesOutWithItsCrew` (one trip, the same ground and return, never
over its seats), `ShoreFishersStandOnTheShoreNeverInTheSea` (8 worlds, 32 fishing villages, 1,860 spots, none in the sea; 14 of their grounds
had no land round them), `WhereTheSeaTookTheSquarePeopleGatherOnDryGround` (a square moved onto the sea: 227 person-hours of leisure, play,
visits and shore fishing, none in the sea), `BoatsGoOutAlikeAtBothResolutions` (below). **Lived against COARSE** (5 worlds x 24 days, every
fishing household given a dugout): fish from boats a grown person-day COARSE/lived x0.90 (C9.1's rule x1.23), all fish x1.22 (x1.50); over
12 days x1.21 and x1.68 (x1.92 and x2.19) - crews brought lived days near COARSE ones (35 outings and 24 crew seats a thousand grown
person-days lived). Ten seeds 300 years DEEP (`tc2/out/civ/s*_c92c`) against C9.1 (`_c91k`): people over years 200-299 x1.001 (t 0.04),
births and deaths a thousand person-years x1.001 and x1.001, fish x1.02 (t 1.7), the wild's food x1.02; at years 100, 200 and 300 no boat
away from the water, one in it in 30 saves (seed 5, year 100: the sea had risen that day, its boat drawn up the next village-day); boats 1.1-3.2 m
above the sea on average; landings 0-805 m from their squares (world means 39-263 m: some villages' nearest sea is far). Earlier tries: the
landing at a cell edge's midpoint and "by the water" as ground below two metres put a village's boats in a column on the grass 35-40 m and
15 m above the sea (`tc2/out/shots/boats3`); slots two metres from the edge left boats a few centimetres above a sea that later rose (16
"in the water" in one world). Shots: `boats4` (Haisthorpe, seed 4, year 300: eight boats in a row on the sand above the waterline, bows to the
sea), `boats5` (Drildbrahope, seed 3: the dugouts in a row; its people at leisure out in the sea at its drowned square - fixed by the green).
**Open.** Crews are of the household only (a plank boat's four seats may want neighbours). The shots after the green (`boats6`): the drowned
town's people gather on the beach at the water's edge, none in the sea.

## D2-112 — 2026-10-08 — Boats of planks: found at the dugout, built in a boat shed, four aboard, out in harder weather (C9.2b)

**Context.** `24` §5 (the plank fishing boat), `25` §5 (the coastal proof: boats -> dock -> boatbuilding knowledge -> cargo). Plank boats grew out
of dugouts in the north: a hollowed trunk's sides were raised with planks fitted along it and spread with heat and frames, until the trunk was
only a keel (the expanded logboats behind the clinker tradition). A small clinker boat took a traditional builder a few hundred hours.
**Decision.** (1) **A technique, not an unlock**: "build boats of planks" (`Tech.Boat`, the eighth; `Person.KBoat`, `Settlement.BestBoat`,
copied from the village's best like any other: kept by ~13 learners, alpha 0.22, beta 0.07); found by a woodworker (wood skill 0.3,
`Learning.BoatWright`) hollowing a dugout where nobody knows it (`Boats.Work` calls `Learning.Practice` for every hour at a dugout, both
resolutions), 1e-4 an hour by curiosity, times 0.3 + 0.7 of the village's share of metal tools; nobody begins knowing it; its knowing is a
record (`Settlement.BoatEvent`). (2) **The boat shed** (`Structures.BoatShed` 22, "boat shed", `Function.Boatyard`; 80 h, 24 loads; falls in 30
years unkept): a village that knows the craft raises one at the head of its landing when one of its households would make a boat and a plank
boat would promise it the sea's worth (`Boats.ShedWanted`, `ShedSpot`: a dozen metres up the beach, dry, clear; after water and a store,
never while walls rise; caused by its knowing). (3) **The plank boat** (`Structures.PlankBoat` 21): 240 labour-hours and 40 loads (the logs
taken to the shed as it is laid down), built on the shed's stocks one at a time at the pace of the builder's knowledge of the craft
(`Boats.Craft`: a learner a fifth of a competent builder), then down to the landing; four aboard (a crew of the household), out in winds to
15 m/s (dugouts 12) and caught out half as often at the same wind (`Risk` by kind), rowed out half as fast again as a dugout is paddled
(`RowPace` 1.5: the row costs less of a trip, both resolutions), reaching more of the offshore shoals (`WildFood.ReachP`: max(0.5, 1 - 0.25
offshore) against a dugout's max(0.3, 1 - 0.45); `Settlement.SeaP`), lasting 25 years. A household of a village with the craft and its shed
makes a plank boat when its dugout is gone (waiting for the stocks); elsewhere dugouts as before. (4) **The timber goes with the boat**: a
boat takes its trunk or logs from the store as it is laid down (both kinds; drawn in step with the work, a plank boat's 40 loads stalled when
fuel and houses emptied the store under it); a plank boat stands two years half-made before it is given up, a dugout one (`PatienceOf`: given one year, plank boats were abandoned with 100-210 of their 240 hours in them - three and a half times the given-up hulls of C9.2a). (5) **Seen and read**: kit models `dugout` (a hollowed log, solid ends, a paddle
in it) and `boat_shed` (a naust: plank walls on a stone footing, a thatched roof, open to the sea with a slipway); `boat` is the plank boat;
the shed drawn with its mouth to the water, a plank boat rising on its stocks; history (built a boat of planks in the boat shed, caused by
the village's knowing; a lost boat named by its kind), the person page (crafts, "a boatwright at the boat shed", their boat by kind), the
place page (dugouts and plank boats, the shed), the building page (the shed's stocks and its best boatwright), headless `boats:` (plank
boats, villages that build them, sheds, plank boats made, their fish). Sim version 51.
**Evidence.** 262 tests (`BoatTests`: `AWoodworkerHollowingADugoutFindsHowToBuildWithPlanks` - a woodworker finds it, someone without the hand
does not, the record told; `AVillageThatBuildsWithPlanksRaisesABoatShedAndBuildsThemThere` - a dugout wanted before the shed, the shed planned at
the head of the landing on dry ground, then a plank boat on its stocks while the others wait, a learner at a fifth of the pace, built and down
to the landing, its record caused by the village's knowing; `APlankBoatSeatsFourRidesOutHarderWeatherLastsLongerAndReachesMoreOfTheShoals`;
`PlankBoatsGoOutAlikeAtBothResolutions` - 5 worlds x 24 days with a plank boat for every fishing household: fish from them a grown person-day
COARSE/lived x0.92, all fish x1.23, crew seats 28 lived and 39 COARSE a thousand grown person-days). Ten seeds 300 years DEEP
(`tc2/out/civ/s*_c92r`) against C9.2a (`_c92c`): the craft found in 7 of 10 worlds (first in years 0, 94, 123, 178, 193, 208, 245; never on
seeds 1, 5, 6) and carried by those who moved: by year 300 9-26 villages build boats of planks with 7-22 sheds and 20-47 plank boats (seed 3:
found at Lonmere in year 178, it stayed there - one village, 5 plank boats among 52 dugouts); 24-223 plank boats made; dugouts at year 300
x0.25; people over years 200-299 x0.993 (t 0.6), fish x1.04 (t 1.6), drowned x0.90, lost at sea at par, given-up hulls x1.24 (t 1.1), the
run's cost x1.01; births and deaths a thousand person-years both a little lower (x0.990, x0.983, t -2.4 and -2.3: the same turnover slowed,
people at par). Shots `tc2/out/shots/planks1`, `planks2` (seed 4, year 300, village #29: the boat shed at the head of the landing, its slipway
running out of its open mouth to the water, four plank boats in a row beside it; the first shots had the shed's mouth inland - the kit's +y
comes into the game as -z).
**Open.** Crews are of the household (a plank boat's four seats are often half used: crews of neighbours, shares of the catch); a village's
sea is often fished nearly empty by year 300 (0.0-0.1 FU an hour at several villages of seed 4) - boats are still begun where the sea promises
1.25 times a nearly empty shore; plank boats reach the same grounds as dugouts (2.5 km) - farther grounds for them would spread the
pressure (C9.3 or later). Web build of 1808e81: Web test 5/5 (`tc2/out/webtest/c92web`: the browser bit-identical to headless, every view
73-120 fps, no console errors).

## D2-113 — 2026-10-08 — Voyages: ways over the sea, a boat's load in trade, grain by sea to a famine; relief owed back; voyages lived (C9.3)

**Context.** `25` §5 (boatbuilding -> cargo capacity -> coastal trade -> port wealth). Trade (D2-067) is barter carried on people's backs within
a day's walk (25 kg); famine relief is grain carried within a day's walk.
**Decision.** (1) **Ways over the sea** (`Places/SeaRoutes.cs`, `World.SeaRoutes`): from one village's
landing out to the nearest sea cell, A* over the sea's cells to the sea cell nearest the other landing, and in - a `Route` polyline; cached by
the pair, rebuilt when the coast moves or a landing is chosen again; none past `Most` (16 km). (2) **A boat's load**: `Trade.Plan/Load/Exchange`
take a load (`BoatLoadKg` 500, twenty walkers' loads); `BestSeaPartner` - the coastal village with the best deal for a boat's load, reached by
sea, worth a crew's day (`WorthAVoyage` 12). (3) **Voyages** (`Trade.Voyages`, at both resolutions, abstract like relief - no crew is seen
rowing yet): a village's plank boats put out at `VoyagesPerBoatDay` 0.04 while a deal stands, a crew of the household (two at least), the sea's
chance on the way (wind over the way's middle; lost with the load), the load out and the return load home, the crew visitors there. (4)
**Relief by sea** (`Trade.Relief`): a famine beyond a day's walk and within a way by sea gets a boatload of grain a day for each of the
giver's plank boats, in a calm sea (`LoadBoats`; history `ReliefSent` ArgA 1, "by sea, a boatload along the coast"); metrics `Voyages`,
`SeaCargoKg`, `ReliefBySeaFu`; headless `sea trade:` line. Sim version 52.
**Evidence (the first commit).** 265 tests; `VoyageTests` 3 (the way keeps to the water and is no shorter than the straight line, cached and rebuilt when the coast
moves; a boat's load is twenty walkers' loads - iron 5 kg against 93 kg, gain 62 against 1,241; grain by sea to a famine 7 km off only with a
plank boat and only in a calm sea, at most a boatload, told). Probe at year 300 (seed 4): trade by barter finds almost no deals (grain-rich
villages spare grain, the hungry spare nothing back) - trade voyages are rare (seed 4: 40 in 300 years, 2.9 t); relief is where the boats
matter. Ten seeds 300 years DEEP (`tc2/out/civ/s*_c93c`) against C9.2b (`_c92r`): seeds 1, 5, 6 (no plank boats) identical; relief by sea
0-388k FU a world (seed 4: 254k, 59 % of its relief), total relief x1.10; people over years 200-299 x0.987 (t -0.5); **but famines x1.18
(t 2.1) and hunger x1.12**: seed 2 famines 123 -> 227 and hunger deaths 1,704 -> 4,791 (people +6 %), seed 11 famines 238 -> 403, seed 8
206 -> 272; seed 4 fewer famines and hunger deaths yet people over years 200-299 -22 % (6,865 -> 5,372).
**Why relief by sea raised famines (traced, session 9).** One binary ran both worlds (headless `--nosea`: no voyages, no relief by sea - it
reproduces `_c92r` and `_c93c` bit for bit) with a trace of each village a year (`--vtrace`: people, store, famine, hunger deaths, relief in and
out by land and by sea, who left). **Seed 2 was chance**: the worlds part at year 219 (the first boatload of relief, 2,248 FU, year 222); the
no-sea world then had a 1,450-death epidemic in years 262-268 that the sea world did not, so the sea world reached its food ceiling ~25 years
sooner (9,495 people against 7,532 at year 278) and starved 3,000 in years 270-300 - the no-sea world began the same crisis at 295 (1,049 hunger
deaths in five years). **Seeds 11 and 8 were a mechanism**: relief was an unconditional subsidy, and the sea multiplied the givers in reach.
Villages whose land cannot feed them were held over it for ever - seed 11's Boldal (20 ha) kept ~100 people where the no-sea world kept ~50,
famine 6-7 years a decade, 62,000 FU by sea and 32,000 by land over 200 years, nobody leaving; Krounpelthor 69 famine-years in 200 - and hunger
deaths a thousand person-years were *higher* with sea relief (8.6 against 6.9 on seed 11, 10.1 against 9.4 on seed 8): relief prolonged famine
instead of ending it. Land relief does the same with fewer givers in reach. Givers were never drained (they keep 60 days, a year and a quarter).
**Decision (added).** (5) **Relief is owed back - social storage** (Halstead and O'Shea's "bad year economics": food given in another's bad year
is a claim on theirs). A giver gives the less the more of what it gave the receiver stands unreturned - `DeedBook.Owed(receiver, giver)`, the
weight now of its relief deeds as the receiver saw them (help's half-life, 4 years), against `Trade.CreditDays` (60) of the receiver's need -
so famine after famine of the same village it gives less (27.6, 14.9, 8.1 days of their need in the test). A village with grain to spare gives
back what it was given, the oldest gift first, a twentieth of its spare a day, by land within a day's walk or by sea (its own boat, or the
giver's come to fetch it), and the giver's hands open again (`Trade.GiveBack`, `DeedBook.GiveBack`, deed flag `Returned`; history
`ReliefReturned` 87, caused by the gift, told at the first grain back; metric `ReliefReturnedFu`). Each giver's first gift in a famine is told
(`ReliefSent` a giver and a famine, so each gift can be owed back on its own). (6) **Voyages lived** (ACTIVITY): the village's day decides as
COARSE does (the Poisson rate of trade voyages, grain to a famine, grain given back) but books each on a free boat (`Trade.Booking`, saved and
hashed); in the morning one of the household takes it out (choice 21, `AgentSystem.StartVoyage`) with all of it who can take an oar, the load
aboard (`Trade.LoadAboard`), the sea's chance as they put out; the crew walk down to the landing (20 min), put out together (`PutOut`: one boat,
one arrival - never people on their own times), row the way at a walker's pace times the boat's (`ActivityKind.Voyage` 34 along the `SeaRoutes`
way, `AgentSystem.RouteOf`), and at the other landing the one with the load deals it (`Trade.Deliver`: the trade for the return load, grain
given to the famine, grain given back, or grain fetched back aboard), the crew visitors for three hours (what they catch, the news); then down to
the boat and home together, the load into the store. `Person.Voyage` (the boat) marks the crew; bookings nobody sailed lapse at the next village
day (`VoyagesLapsed`); EnterCoarse clears them. (7) **One rule at both resolutions**: a boat carries a load only if its household has two who
can take an oar (`Trade.Crewed`, `CanSail`: grown 16-55, healthy, not hurt, not near term nor nursing a young baby - COARSE had shipped relief
from boats nobody could man); grain goes by sea only in a worthwhile load (`MinSeaLoadFu`, a fifth of a boatload: a boat had rowed 15 km to
fetch 13 FU). (8) **Seen and read**: the view draws a voyage's hull at its crew, bow along the way, and the boat drawn up at the other landing
during the stay (`WorldObjectsView.Away`, `DrawVisitingBoat`); the person page ("rowing over the sea to X with grain for its famine", "at X,
come over the sea in the household's boat"); headless `sea trade:` (voyages, trade crew seats, booked and not sailed), `trade:` (relief, given
back), `run --load S --lived` (a saved world back to lived days), `--voyagers` (each boat on a way), `--vtrace`, `--nudge Y` (a placebo), shot
views `voyage`, `voyagenear`, `voyagefar`, `voyagestay` (the world lives on until a real voyage is under way). Sim version 53.
**Evidence (closing).** 269 tests: `TradeTests.ReliefIsOwedBackAndGivenBackWhenTheHarvestAllows` (less each famine; given back in full, told
once, caused by the gift, into the giver's store; given again after); `VoyageTests.ALivedVoyageIsRowedOverTheSeaByItsCrewAndComesHomeWithTheReturnLoad`
(a crew of two rows 7.5 km together - never more than 30 m apart, over the water - deals the iron for hides, stays, rows home with them);
`VoyagesGoAlikeAtBothResolutions` (6 worlds x 24 days: lived 0.027 voyages a boat-day and 574 kg a voyage, COARSE 0.029 and 592);
`GrainBySeaReachesAFamineAlikeAtBothResolutions` (4 worlds x 12 days: lived 2,898 FU by sea, COARSE 2,750). A placebo - one FU more in one store
at year 150, no sea (`_d2p`, 7 seeds) - moves single seeds' famines by up to 34 % (mean x1.045, t 0.8): ten chaotic worlds judge only large,
consistent effects. Ten seeds 300 years DEEP with every C9.3 rule (`tc2/out/civ/s*_d5s`) against C9.2b (`_c92r`): **famines x0.971 (t -0.4;
before x1.18), hunger deaths x0.955, a thousand person-years x0.999**, births and deaths at par, relief x0.91 with 4,000-113,000 FU a world given
back, people over years 200-299 x0.955 (t -1.8) - social storage alone (`_d5n`, no sea, against `_c92r`) people x0.954 (t -2.2), relief x0.74: the
villages that lived on their neighbours' grain shrink to their land. Sea against no sea under the new rules (`_d5n` vs `_d5s`): famines x1.046
(t 1.4), hunger deaths x1.06 (t 1.2), people x1.001, relief x1.24 - inside the placebo's spread; what remains is ordinary: with grain by sea fewer
households leave their famine villages (seed 11: 5,794 left against 10,497). At year 300: 0-659 voyages a world (none on seeds 1, 5, 6, 7 - no plank
boats or no deals; seed 2 seven), 60-289 t carried, 79,000-353,000 FU of relief by sea in five worlds. Lived from a year-300 save (seed 4, 10 days
`--lived --voyagers`): boats from Tovaldton (384 FU) and Stairby (259 FU) rowed grain 13-15.5 km to Kreingrithwick's famine, crews of 2-3, a leg
1.5-2.3 h, home together. Run cost x1.02. Unity editor compiles; shots `tc2/out/shots/voyage1` (a boat mid-way in the rain; a crew's boat drawn up
at Kreingrithwick's landing during the stay) and `voyage2` (a relief boat nearing a wooded coast). Web build of the C9.3 code: Web test 6/6 (`tc2/out/webtest/c93web`: the browser bit-identical to headless; views 55-120 fps - the forest at low altitude 55, the person page 48-58, below C9.2b's 73-120: to be measured again on a quiet machine with the next build).
**Open.** Crews are of the household only; plank boats reach the same fishing grounds as dugouts; a voyage's risk is drawn once as it puts out
(not on the way back), and a storm that rises after it put out does not meet it; cargo boats, docks and coastal trade at the sea's cost ratio
are C9.4.

## D2-114 — 2026-10-08 — Building variety: buildings built as their builders could, traditions carried by founders, houses rebuilt as means grow

**Context.** Arjun (8 Oct 2026), a standing visual requirement: settlements must not look like collections of repeated same-colour,
same-shape models. Houses vary by real circumstances (household size, means, material at hand, age, the settlement's history, its
building tradition) - families of plans and deterministic variants (footprints, one or two storeys, roof shapes, roof and wall
materials, timber/stone/plaster, extensions, chimneys, yards and fences, wear), never random colours; functional buildings are
recognisable at a glance (smithy, training ground, granary, market, boathouse, mine and quarry, hall; later towers, gates and keeps
dominating the silhouette); richer, larger, older places get denser and more substantial - a hamlet, a village, a walled town and a political
centre must not read as the same houses at different counts. Judged in real shots at close, village and region views. Before: four house
models (cottage, timber, long, small) picked by a hash of the household's id in fixed shares, on posts or footings, one tint; the barn and
the smithy were houses in other sizes (`tc2/out/shots`... kit render in the session 9 scratchpad).
**Decision (so far).** (1) **How a building is built is real state, recorded when it is raised** (`Building.Form`, `Core/Places/Architecture.cs`;
sim version 54): a home's plan (a one-room cottage, a cottage, a longhouse with its byre, a two-storey house, a hall-house with its cross wing),
its roof form, cover (straw or reed thatch, wooden shingles, fired tiles, stone slates), walls (wattle and daub, limewashed daub, timber framing,
logs, stone), chimney and shade - from the household's size, means (`Architecture.Means`: beasts, larder against a month's need, goods) and
cattle (a byre), its rank in the village (the best off of a village of 120 builds a hall), the village's size, its ring and how near the centre the
lot lies (up a storey where central lots are dear), and what the ground and knowledge give (rock and a quarry or stone in store, clay, pottery,
iron tools, the woods it fells - conifers in the cold -, wet ground for reed, the climate). A house raised early keeps its early form while the
village changes: the town's history stands in its houses. Works take their village's cover and walls (`WorkForm`). The view draws what is
recorded; nothing in the simulation reads it (warmth, fire and decay by material are later). (2) **Building traditions** (`Settlement.Tradition`,
C11's first trait): the first peoples each their own; a founded village carries the tradition of the village its founders came from, with a
founder's drift (`Drift` 0.3: one trait, the roof's form or the walls' shade, changes) - regions of one descent look alike and drift apart.
(3) **Rebuilding** (C13's first piece; real labour and material): at the yearly weighing a household of means (`Means` 1.5) rebuilds its house
when what it would build now stands well above it (`Architecture.Gain` 1.0: a bigger plan, better walls and roof, a chimney) and the house is 20
years old, or when the house is 70 years old (`Homes.RenewYears`) - if its village is not short, not walling itself and has the wood, a tenth of
its households at most at once (`Homes.WeighRebuilding`, `Household.Rebuild`); the hours and wood are its own building's after the village's
works (`Construction.OwnWork`/`OwnStage`: a cottage's 240 h and 35 loads times the plan's size, `Architecture.Size`), stone walls take 30 loads of
the store's stone; history `HomeRebuilt` 88, `WhyCode.Rebuild` 44, metric `HomesRebuilt`. (4) **The kit** (`tc2/art/kit.py`, exported to
`Game/Resources/TC2Models`; the old `house_*` models and `market_stalls` removed): `home_<small|cottage|long|two|hall>_<g|h|j>` (five plans x
gable, hipped, half-hipped), `frame_<plan>` (timber framing overlays), `chimney`, `plinth` (stone footings), `outshut` (a lean-to); walls painted
for a second tint channel (vertex alpha 0.25), roofs for the first (alpha 1); new works with their own silhouettes - `barn` (aisled, a huge
half-hipped roof over low tarred-board walls, a tall cart porch), `granary` (a boarded box raised on staddle stones, steps to its door),
`smithy` (an open forge shed dominated by a great stone chimney, anvil, trough, grindstone), `market_stall` (a dyed awning) and `market_cross`.
(5) **The view** (`WorldObjectsView`, `Instancer`, `TC2/Instanced` shader): a per-instance wall colour (`_WallColor`); a home drawn from its
form - its model by plan and roof form, its cover weathered by its years (thatch greys, shingles silver, tiles darken), its walls by material and
shade, framing, a plinth where it stands on footings (C1.3), a chimney (smoke from it, or from a smoke-hole in the roof), a lean-to when its
household outgrows the plan (`PlanRoom`), a fenced pen behind when it keeps beasts; barns, granaries and smithies roofed and walled as their
village builds; the market as its cross with as many dyed stalls as traders come (`TradeIn`). Headless `forms:` lines (plans, roofs, covers,
walls, chimneys, rebuilt, by village size).
**Evidence so far.** `ArchitectureTests` 5 pass (every home and work has its form, roofed in its village's way; a plan follows the household
and its village - the poor and few a one-room cottage, a household with cattle a longhouse, the best off of 150 a hall-house with a chimney, tiles
where pottery and clay are known; traditions kept by line, 283 of 400 daughters as their mother; a household of means rebuilds its old poor house
with 336 h and 49 loads of the village's wood, told, the poor do not; short rations stop rebuilding, a tenth at most at once). Without rebuilding
the simulation's trajectory is untouched (seed 4, 300 years: annals bit-identical to `_d5s`); seed 4 at year 300 then had 3,555 houses - covers
straw 1,996, reed 1,211, tile 294, shingle 54; walls limewash 2,217, framed 626, daub 623, logs 46, stone 43; 14 hall-houses, 196 longhouses; roofs
gable 1,661, hipped 986, half-hipped 908 in 11 traditions of 6 lines (before drift: 3,500 of 3,555 hipped) - but only 36 two-storey houses: inner
lots are taken while a village is small, so its core stays cottages; rebuilding is what densifies it (a first rule had put half of all houses in two
storeys by village size alone). Ten seeds with rebuilding are `tc2/out/civ/s*_a3` (being read at the handoff).
**Decision (session 10, judged in real shots).** (6) **One hall a village** (`Architecture.HallRoom`): a hall-house is its leading household's -
one lived-in hall a village, one for each `PeoplePerHall` (150) of a town's people; empty halls (taken by whoever needs a home) and the hall being
rebuilt are not counted; a hall set out while another was raised meanwhile is built a storey high (session 9's runs had 54-129 halls a world: each
year's best-off rebuilder raised its own). (7) **Means that set households apart** (`Architecture.Means`, see `21` §6): beasts (cattle 1, small stock a
quarter, four at most), a boat (plank 0.75, dugout 0.25), goods held beyond need (two at most), working hands beyond the first (0.3 each, 0.9 at
most) and the years of food in the village's granary (1.5 at most). The first measure (beasts, the larder against a month, clothes against people)
put 94 % of a world's households between 1.0 and 1.5 - larders hold days (the grain is the village's), clothes were capped at need, herds are rare
late - and the plans came out 2/81/7/9/1 % (one-room/cottage/long/two/hall over ten worlds): a town was one cottage repeated. Plans now: a hall for
the best off of 120+ (means 2 among the top tenth, `RichMeans`) where there is room; two storeys on a town's dear lots (inside a standing ring of a
village of 200, or the inner half of a village of 250) for all but the poorest (means 0.5 or three people); a longhouse for cattle, four small stock or
seven people; a one-room cottage for two people under means 1 or three under 0.6; else a cottage. (8) **Roofs kept and weathered** (view,
`WorldObjectsView.RoofColour`): a cover's colour is its material weathered by the years since it was last laid; a lived-in house or a work in use is
kept - straw re-laid every ~28 years, reed ~45, shingles ~40, each house on its own turn (keyed by its id) - so a village shows fresh golden thatch
beside greyed and mossy; tiles and slates come in batches (a kiln's firing, a quarry's bed) and darken over a century; a building left empty weathers
from the day it was left. Daub walls lighter and warmer. (9) **Ground and yards** (view): a house's earth yard shrinks from 8.5 m to a doorstep
(3.5-6.5 m) - in a town the yards had run together into one bare disc; a **kitchen garden** (kit `garden`: raised beds of greens, wattle hurdles)
behind each house whose household has working hands, as wide as its hands (beside the house where the beasts' pen is), none in a town's two-storey
core; a training ground's ground beaten pale by the hours of practice put in there (`b.Use`, `TerrainLod._grounds`); the smithy drawn 1.2 times
life, its walls sooted by its forge's hours. (10) **The pages**: the building page tells how a house or a barn, granary or smithy was built and why
(`Architecture.Describe` + `Why`: each reason is the condition the rule required); the person page names the home ("a longhouse of logs under fired
tiles on stone footings", `Architecture.Short`). Headless `place --list` (each village: plans, covers, walls, means, works with their hours),
`--households` (each household's people, larder, gear, beasts, means, house), `--standing` (quantiles of what could set households apart).
**Evidence (closing).** 275 headless tests (`ArchitectureTests` 6: + `AVillageKeepsOneHallForEachHundredAndFiftyOfItsPeople`; the plan test
follows the new means; a year of grain in the granary adds exactly one). Ten seeds 300 years DEEP: `_a4` (the hall cap, the first means) and `_a5`
(every rule) against C9.3 (`_d5s`). `_a5`: people over years 200-299 x1.023 (t 0.6), famines x1.065 (t 0.7), hunger deaths a thousand person-years
x1.15 (t 1.0), births and deaths a thousand person-years x1.001 and x1.006 - inside a placebo's spread; raids x1.94 (t 2.45), wounds x1.30 (t 2.4),
relief given back x0.80 (t -2.4) - but a placebo on the same binary (`_a5p`: one FU more in one store at year 150) moves raids x0.66 (t -2.0),
wounds x0.85 (t -1.9), palisades x0.88 (t -2.0): conflict over two centuries is chaotic and these are not effects of building. `_a4` (and session 9's
`_a3`) raised people x1.06 (t 2.6): rebuilding's timber is felled from the woodlots, and felled woods raise the cells' arability, so villages claim more
field (arable at year 300 +4 % to +14 % on the seeds that grew, s4 4,223 against 3,896 ha) - a real coupling (the great clearances); with the new
means the effect is x1.02. Plans over ten worlds now 37/28/13/21/1 %, covers straw/reed/shingle/tile 59/20/1/20 %, walls daub/limewash/framed/logs/stone
25/53/9/3/10 %; 10,700 houses rebuilt in 300 years (5,533 with the first means); halls 242 standing among 19,926 houses (1.2 %). Run cost: seed 7, 300
years, sequential on a quiet machine, 53.4 s against 51.3 s (+4 %; a first reading of +30 % was the machine's noise). Unity compiles; shots
`tc2/out/shots/arch1`, `arch1y50`, `arch1open` (the first means and colours: towns of one cottage under one brown thatch or one orange tile on a
bare disc), `arch2lord`, `arch2open`, `arch2y100`, `arch2works`, `arch2works2`, `arch3works` (the lord's walled seat Tovaldton at year 300: two-storey
log and framed houses in its core, fresh and old thatch, tile batches, chimneys, gardens, its market and granaries on staddles; the open village
Rainthorpe of 689 spread with gardens; Stairby at year 100 a village of tiled and framed longhouses; barns, the boat shed with its boats and the quarry
read at a glance; `arch4` the training ground beaten pale and the smithy sooted). Web build of this code: Web test 6/6 (`tc2/out/webtest/d2114web`:
the browser bit-identical to headless; views 59-122 fps - close 78, village 96, the place page on the land 59; C9.3's were 55-120).
**Open.** Streets: lots still follow the village's spiral, so a town's houses do not line streets or share walls (C13 morphology); a real household
wealth (land held, crafts sold, rents) is C3/C10 and will replace `Means`; the lord's hall, towers, gates and keeps as real structures are C7/C10.2;
warmth, fire and decay by material are not simulated.

## D2-115 — 2026-10-08 — C9.4 surveyed: no demand for cargo boats yet; docks wait for the traffic (deferred behind exchange, C3)

**Context.** C9.4 (`24` §5-6, `25` §5): cargo boats (~80 loads, a sail, 400 h, crew 3-5, 2-3 t) and docks, coastal trade at the sea's cost ratio.
No structure without a need (D2-089): a cargo boat must be wanted because the village's sea cargo outgrows its plank boats, a jetty because its boats
and what they land make one worth its labour.
**Evidence (the ten `_a5` worlds at year 300, and a survey).** Sea traffic is thin: 0-318 voyages a world in 300 years (seeds 42, 2, 1, 5 none; 3, 11, 8
the most), 6-130 t carried, nothing ever booked and left unsailed. Nearly all of it is famine relief. A survey (`run --seashort`: the relief givers would
have sent by sea beyond their free boats' room - a tally, not state; bit-identical to `_a5`) over the five seafaring worlds: relief by sea was bound by
the boats' room in 0-19 of 69-538 gifts, 0-8,316 FU more would have gone against 47,000-157,000 FU shipped. Trade voyages are rare because barter needs
a double coincidence of wants - a grain-rich village spares grain, a hungry one spares nothing back (D2-113); timber and stone, valued at the hours a
load costs to fetch, differ between coastal villages by less than 2x on a 6 km planet where every village has woods within 1.5 km, so even 100 loads
in a hold would rarely be worth a crew's day. A jetty's gain - sparing crews the haul up and down the beach - is not modelled (launching is free).
**Decision.** C9.4 is deferred: cargo boats and docks wait until exchange gives the sea a trade - C3 (professions, workshops, exchange within a
settlement) and credit between villages (relief owed back, D2-113, is the first credit), so that goods move one way now and are paid later. The
headless survey stays (`--seashort`, `Trade.SeaShort`, null in the game). Next: C13.1 streets (D2-116) - the D2-114 "Open" that every town shows.

## D2-116 — 2026-10-08 — Streets: villages grow along their roads (C13.1)

**Context.** D2-114's "Open": lots followed a golden-angle spiral round the square (D2-084), so every village and town was an even scatter of
houses each facing the square - a hamlet, a village and a walled town read as the same scatter at different counts (shots `arch2lord`,
`arch2open`). C13 (historical city evolution) begins here. Medieval villages and towns grew along their roads - street villages
(Strassendorf), market streets, suburbs along the roads outside the gates - because frontage on the way the traffic takes is worth having.
**Decision.** (1) **Streets are durable village state** (`Settlement.StreetN`, `Street0..3`; sim version 55): when a way of a village's people
first wears to a path (C4.1, `Ways.PathsOf`: ever a path or a road), it is laid as a street at the bearing it actually leaves by (the way's route
read 80 m out, `Places/Streets.cs` `Refresh`, run when a lot is sought); ways leaving within 35 degrees are one street; four at most; a street
outlives its way (houses line it after the traffic has gone). (2) **Frontage is sought** (`Homes.FreeLot`): beside the spiral's innermost free lot
the nearest free frontage lot along the streets is weighed - lots 13 m apart, 8 m back from the street's line, from 22 m out - and taken when it
lies within `Streets.Lean` (0.7) of the spiral lot's distance; so a village with roads grows along them while its core still fills round the
green, and a hamlet whose people wear no way to a neighbour keeps its houses round its green. (3) **Nothing is built in a street**: lots and works
keep 4 m either side of its line clear (`Streets.On`; `Structures.Clear`). (4) **The view**: a house that fronts a street faces it, square to it
(`Streets.Facing`; garden and pen behind, woodpile beside); its door path goes to the street; each street is beaten earth from the green out past
the last house that fronts it (`TerrainLod.VillageGround`). (5) Headless: `streets:` line (villages with streets, houses fronting one, huts lived
in, homes within 50 m of the store and the chance a taker is seen there), `place --layout` (a village's plan for plotting), `--nostreets` (the
same binary without streets: bit-identical to D2-114's worlds).
**Evidence.** `StreetsTests` 2 (a track is not a street, a path is, at the bearing the way leaves by to 1e-6, kept after the way fades; 7 of 12 new
lots front a fresh street, each 8 m back from its line, none in it; no work laid out in it). Layout plots from saves (`place --layout`): by year 100
Tovaldton, Stairby and Tesford grow along one to three roads; by year 300 Vumthorpe and Veidasford are dense cores with four streets lined beyond
them. Ten seeds 300 years DEEP (`_b2`) against D2-114 (`_a5`; `--nostreets` on the same binary reproduces `_a5` bit for bit): 5-31 villages a world
have streets, 19-52 % of houses front one; people over years 200-299 x1.025 (t 0.9), famines x1.07 (t 1.1), hunger deaths x0.98, births and deaths a
thousand person-years at par; huts lived in at year 300 not raised (excluding seed 4, 138 with streets against 170 without; seed 4 197 against 34, a
crowded late century). **Conflict falls, by a mechanism**: raids x0.56 (t -3.1), wounds x0.88 (t -2.1), palisades x0.86 (t -1.5), loads taken down on
8 of 10 seeds - frontage lots from 22 m out put more homes near the store (homes within 50 m of it, seeds 4 and 8: 11.1 -> 16.6 and 7.0 -> 12.1 a
village), so a taker at the store in the night is likelier seen (`Conflict.SeenChance`: 0.42 -> 0.51 and 0.28 -> 0.44), fewer take, fewer wrongs are
held, fewer bands go out - eyes on the street. A placebo on D2-114's binary moved raids x0.66 (t -2.0): the fall is beyond it and explained. Rings:
145 walled villages with streets against 143 without, mean ring 147 m against 146, at the 170 m most 65 against 50 - the street tails do not
distort the palisades' economics (the ring rule is kept). Shots `tc2/out/shots/street1` (Vumthorpe at year 300: a long street to the north lined both
sides, another to the west, the core round the green) and `street1y100` (Tesford at year 100: rows of houses facing the river road, gardens
behind, a second street). 277 headless tests. Web build of 7b17902: Web test 6/6 (`tc2/out/webtest/c131web`: the browser bit-identical; views
75-123 fps, close 86 - the village ground now read once a terrain patch instead of every household's street at every vertex).
**Open.** Back lanes and plots behind the frontage (burgage plots), houses sharing walls in a town's core, a market street or place widened
where traders come, the green kept open as the village grows round it, suburbs outside a gate - C13's next pieces.

## D2-117 — 2026-10-08 — Back lanes: a growing village fills its lanes behind the street frontage (C13.2)

**Context.** D2-116 lined the streets, but the lots behind the frontage still came from the spiral round the green, so a big town's body stayed a
scatter between its streets (shot `street1`: Vumthorpe's core). Medieval two-row villages had back lanes behind their crofts; town blocks lay
between a street and its back lane.
**Decision.** (1) **Back lanes** (`Places/Streets.cs`, derived from the streets: no new state): each street has a lane on either side, parallel to
it `LaneOff` (36 m) out - so a frontage house's garden and a lane house's garden meet back to back -, lined from `LaneStart` (30 m) along it. A
village's lines are its streets and their lanes (`Lines`, `Line`, `IsLane`; line 3k street k, 3k+1 and 3k+2 its lanes). (2) **Lots**: a lane's lots
(13 m apart, 8 m back, both sides) are weighed at `LaneLean` (0.85) of their distance from the green (`Weigh`) - less sought than a street's
frontage (0.7), more than the scatter round the green -, so a village fills its street near the green first, then its lanes, while its core
still fills round the green. Nothing is built in a lane (`On`). (3) **The view**: houses face the street or lane they front (`Facing` with its
line); each lane is beaten earth from its first house out past its last (`TerrainLod.Ground1`: lines' origins and reach).
**Evidence.** `StreetsTests` 3 (+ `BackLanesAreLinedOnceTheStreetsFrontageNearTheGreenIsTaken`: of 60 new lots with one street, 13 on the street, 18 on
its lanes from the 6th on, each 8 m back from its line, none in a street or lane). Seed 4 (`_c1`): 2,554 of 3,192 houses front a street or a lane
(D2-116: 1,949 of 3,896 a street); layout plots - Vumthorpe at year 300 rows along four streets and their lanes. Shots `tc2/out/shots/lanes1`
(Vumthorpe, 681 people, year 300: a main street north lined both sides with its lanes behind, the crossing road lined east and west, houses facing
them, gardens behind, the green in the middle with its granaries, well and sheep). Ten seeds 300 years DEEP (`_c2`) against D2-116 (`_b2`): houses
fronting a street or a lane 48-80 % a world (streets alone 19-52 %); people over years 200-299 x0.974 (t -1.2), famines x0.98, hunger deaths
x1.01, births and deaths a thousand person-years at par, raids x0.91, wounds x0.92, palisades x0.98 - inside the noise; homes within 50 m of the
store 9.9-17.6 a village. 278 headless tests. Web build of 0478ddc: Web test 6/6 (`tc2/out/webtest/c132web`: the browser bit-identical; views
54-121 fps - close 105, village 87; the person and place pages 54-59 where the C13.1 build read 75-85: page views have read 48-85 from build to
build on this shared machine - to be measured again on a quiet one).
**Open.** Lanes are straight and parallel to their street (no lane follows the ground or joins two streets); a town's core still fills round
its green; plots are not owned (a house's croft is drawn, not held); shared walls and rows in a town's core, the market place, suburbs outside
the gates - C13's next pieces.


## D2-118 — 2026-10-08 — A town's ground: footprints, divided plots and rows in its core, the market place (C13.3)

**Context.** C13.3 (Arjun, 8 Oct 2026): dense medieval town-core rows, divided/infill frontage lots, narrow and tall houses where the physical lot
requires them, **absolutely no overlapping houses**, a market place that grows from actual trade, the green and market kept open. An audit of the
C13.2 worlds first: lots were points kept 6 m apart (`Homes.Taken`, D2-086), but a longhouse is 12 m long and a hall 15 m with its wing - on seed 4
at year 300 **280 pairs of homes overlapped among 3,234** (seed 7: 90/1,040; seed 2: 312/3,826) and 366 homes reached into a street, the green or
across a ring (`place --overlaps` on `_c2`; plot `tc2/out/c133/before_s4.png`). A house's bearing was not state: the view turned houses to their
street or the square each frame (so a street laid later turned old houses round), and turned them about a world-fixed forward instead of the
village's own frame.
**Decision.**
(1) **Footprints** (`Core/Places/Footprints.cs`). A home's ground is its plan's rectangles as the kit builds them - walls and eaves (one-room
4.8 x 3.8, cottage 7.0 x 5.0, longhouse 12.0 x 5.4, two-storey 6.6 x 5.4 jettied 0.5 to the front, hall 10.0 x 6.2 with its porch and its 5.2 x 8.6
cross wing, the narrow town house 4.6 x 6.2 jettied 0.4, a hut 2.3 m round) - turned to its bearing; a work's ground is a disc holding all of it
as drawn (a barn 9.4 m, a smithy drawn larger 6.0, a market's cross 2.6). Separating-axis tests (`Boxes`, `BoxDisc`). **`Building.Face`** (state,
sim version 56): the bearing a building faces, recorded when it is raised or rebuilt (square to the street or lane it fronts, a couple of degrees
off; else toward the square, up to ten off; a work toward the square), so a house never turns on its lot.
(2) **Every lot, plan and work by its ground.** A lot is free where the ground it keeps (a hut's, which holds a one-room cottage turned any way; a
narrow house's on a divided plot) meets no building, ruin, camp, street, lane, ring, green or market place (`HomeFits`; `FreeLot`'s `Free`). A
house is built to the plan its household wants if that stands clear where it is, else the next smaller of its kind (a hall a two-storey house, a
longhouse a cottage...; `Architecture.Fit`); a rebuild that no plan fits does not happen. A camp whose lot was built over meanwhile moves before
its home is raised. Works are laid out by their discs (`WorkFits`), never in the green or market place but for the market itself; works at their
sites (quarry, mine, bog pits, charcoal hearth) are moved out along their bearing where the site lies inside the village, and a worked-out work of
the same kind there gives way.
(3) **A town's core divided** (`Streets.Grow`, `CorePlot`; `Settlement.CoreR`, state): once a village is a town (250 people) the frontage of its
streets out to `CoreR` = 25 m + people / 12 (at most 110 m; never given back) is plotted at half the step - 6.5 m plots - and a house raised or
rebuilt on a divided plot must keep within it: **the narrow town house** (`Architecture.Row`, new kit plan `home_row_g/h/j`, `frame_row`: 4.6 m of
frontage, 6.2 deep, two storeys and an attic jettied over the street, gable to the street, door and shop window in the gable) or, for the poorest,
a one-room cottage. Old wide houses keep standing until rebuilt; then they too are narrow, and the half-step plots between them fill - rows of
tall narrow houses along the main streets near the green, as burgage plots were divided in medieval towns.
(4) **The market place** (`Settlement.MarketR`, state; `Structures.MarketDealt`): when a village's market stands its green becomes a market
place 20 m round, widened by every trader who comes to deal (16 m2 each, up to 36 m); never narrowed. The market's cross is laid out in the green.
Nothing new is built in it; camps on lots it takes move; homes standing empty in it are taken down.
(5) **The open kept open.** A home standing in a street or lane, the green or market place, or across a ring (laid after it was built) is never
renewed there, and when it is left it is taken down - gone at once, half its timber back in the woodstore, `HistoryKind.HomeCleared` 89 - so the
ways and the market place open as their households leave or die out. A street is laid at the bearing within 20 degrees of its way's that runs
through fewest standing buildings and camps (none where there is a gap; at most three: `Streets.MostCuts`); a lane that would run through one stays
shut (`Settlement.LanesShut`, state) until its line is clear (`OpenLanes`). A ring is laid at the radius inward from its reach with no home or camp
across it (else the fewest); camps on its line move.
(6) **Rivers** (`Core/Planet/RiverLines.cs`): the river's drawn line moved from the view into the core - one line serves the simulation and the view,
as one shore does (D2-088). A reach ends where it meets the drawn water (its mouth at the waterline, 2 m in - the ribbon had run 65 % of the way
into the sea cell, out over the water) or runs on across a wide beach to it. Homes and works keep their ground `RiverMargin` (1.5 m) off a river's
bank (half its width by its flow); a house a shifting river reaches counts as in the open (`Homes.InOpenOrWet` 4). Before, only a lot's middle
was kept on dry ground: 4-28 homes a world stood on a river's ribbon (Vodrothorpe, seed 2: 18).
(7) **The view** draws every building by its recorded bearing in the village's exact frame (`PlaceFaced`: the village's east and north carried onto
the ground); far off a building is drawn larger only as far as its neighbours leave room (`Footprints.Room`); kitchen gardens, pens and woodpiles
only on free ground (`YardFree`); the market's stalls in rings round the cross and along the market place's edge on free ground; stone and hay
stacked round the granary on free ground (they stood 22-35 m out, among the homes); the market place trodden bare; the narrow town houses with
their framing, chimney and plinth. Headless: `place --overlaps [--sid]`, `--layout` with each building's bearing and rectangles
(`tc2/out/tools/plotfoot.py`), the `town:` line of `run`; the settlement page tells the streets, the divided core and the market place.
**Evidence.** `FootprintsTests` 6 (rectangles meet only where they overlap; a town of 110 new houses on two streets: none overlapping, none in a
street, the green or a ring, every house on a divided plot narrow - 12 narrow town houses, 10 plots at the half step; a village below 250 keeps
whole lots; the market place widens 20 -> 36 m over 176 traders and nothing new is built in it; a home left in the market place is taken down,
its timber kept, history tells it, and is never renewed there; a street is laid along the bearing that runs through fewest buildings); two
charcoal tests updated to the site rule (a hearth whose woods lie in the village's middle is laid just past the homes). 284 headless tests.
Seed 4 alone, rule by rule (`tc2/out/civ/s4_d1`..`_d5`): footprint lots and plans 280 -> 0 overlapping pairs; the open (streets laid through
houses, rings across camps, the market place widening over houses) 366 -> 169 -> 33 -> 4 homes in the open; a street laid only through a clear
corridor cut the streets from 94 to 39 (old villages grown round their green have none), so up to three houses (`MostCuts`) - 82 streets.
**Ten seeds 300 years DEEP (`_d7`, the final rules) against C13.2 (`_c2`)**: overlapping homes **0 in every world** (C13.2: 90-312 a world),
home/work 0-1, work/work 0-1 (works at their sites), homes in the open 0-30 (houses a later street or market place reached, waiting to be left;
none on a river); 0-23 towns a world with a divided core (reach 58-79 m), 5-243 narrow town houses standing (9-345 raised); 0-6 market places a
world (radius 25-30 m, the most 36 m). People over years 200-299 x1.003 (t 0.1), births x1.005, deaths x1.006, famines x0.92, hunger x0.97, wounds
x0.87, raids x0.99, tributes x0.91 - inside the placebo's spread (D2-114's `_a5p`: wounds x0.85, raids x0.66, tributes x0.71). Run cost a
person-day x1.2 against `_c2`'s (a seed-42 profile on the `_d6`/`_d7` binaries puts most of it in noise: systems this slice does not touch ran
20 % slower in the same run; no ground check shows among the costs). The river rule alone (`_d6` -> `_d7`): 4-28 homes a world had stood on a
river's ribbon; 0 after. Web build of e6ab82f: Web test 6/6 (`tc2/out/webtest/c133web`: the browser bit-identical to headless; views 64-120 fps;
the person and place pages 67-83 fps - C13.2's 54-59 was the shared machine's load, C13.1 read 75-85). The view: the kit's frame measured in the game (bounds: front +z, x mirrored - the hall's wing at -x, `WingSide`
set from it after a `--ground` shot showed its rectangle on the porch side). Shots `tc2/out/shots/c133a` (Rainthorpe, 860 people: the north street
lined both sides with narrow gable-fronted town houses, the market place with its cross, stalls and granaries, the rows at street level),
`c133g` (the ground plates under every house: no plate meets another), `c133b`-`c133d` (the final worlds). Layout plots `tc2/out/c133/*.png`
(`before_s4.png`: C13.2's overlaps outlined red).
**Open.** Market places are rare because trade is: 0-5 traders a year at most villages (barter's double coincidence, D2-115) - exchange (C3) will
widen them where it brings traders. Houses a market place, street or ring reaches stay until their household leaves (decades); no household
moves out to rebuild elsewhere. Rows are of the one narrow plan (built in a town's tradition and materials) - no merged plots for a rich
household's wide town house, no shops below. Lanes stay straight; streets are straight rays bent at most 20 degrees from their way. Plots are not
owned (a household's croft is drawn, not held). The run costs 9 % more (no spatial index for the ground checks yet).

## D2-119 — 2026-10-08 — The weaver's shed and the village's wares (C3.1); a lived save and load continue exactly again

**Context.** C3 (`26`): exchange needs specialisation - things one village makes that its neighbours lack. The first C3.1 design (session 11) had
wares valued by "goods short a head" and a workshop raised where wares run short. Measured before any code (the `_d7` saves at year 300:
`place --standing`/`--list`, `timeuse`): households' goods sit at their target (1.17-1.20 a head in three quarters of households against 1.2),
grown people idle ~8 hours a day outside the harvest, making and mending takes ~0.5 h a person-day - goods are never short and home making costs
only spare hours, so that design would have made neither a need nor a trade. What is short is **fibre**: 0.1-1.2 kg a head in store against
2.5 kg a person's goods take (1 kg a field-hectare a harvest, used as it comes); and every village knows the loom (best 1.00 everywhere).
**Decision.**
(1) **Wares last.** Goods woven close on a broad loom and fulled, finished by practised hands, wear at `Weaving.WaresWear` (0.6) of homespun's rate -
the same flax clothes 1.7 times the people. A household holds wares as part of its goods (`Household.Wares` <= `Gear`; inherited and shared out
with them); its members wear the mix (`Goods.Wear`; bit-identical to before where no wares are held).
(2) **The weaver's shed** (`Structures.Workshop` 23, Function Craft, Zone Edge, 220 h and 35 loads, falls in 40 years; kit `weavers_shed`): raised
by a village that knows the loom where its people's wear of goods (people x 1/300 x tool wear) would keep six makers at its looms all year
(`MakersFrom` 6 x a maker's day at the village's best loom: ~165 people with stone tools, more with metal ones) - larger places only - after
water and a store, never while walls rise; its cause the loom's technique event (`Settlement.LoomEvent`), else the founding.
(3) **Its makers** are the village's eight best weavers (`Looms`; `Settlement.LoomBar`/`LoomBarId`, the eighth best by loom + practice, read
daily and when the shed is raised; ties to the lower id): the same people from day to day, the practised keeping their looms. Lived, a choice by
day (u 0.5, as the smithy's) to `ActivityKind.Weave` (35, `WhyCode.Weave` 45) at the shed; COARSE/DEEP, a labour day there before the shared
field work (the smithy's law, D2-093). At the shed: `Pace` 1.5 x the maker's home rate x (0.8 + 0.4 `Person.SkCraft`), practice growing 0.0015 an
hour; wares take real fibre, 2.5 kg each (no cloth from bark and grass); made up to `Target` = 0.15 a head + what was sold lately
(`Settlement.WaresSold`, halving each year - demand known from real sales, as the smiths' iron, C5.3b).
(4) **Households take wares** from the store before their own hands make more - in the hearths' day after the wear and before any making choice
at both resolutions (`Weaving.Draw`; `Settlement.WaresOut` halving each year; `WaresHeld` daily).
(5) **Trade**: `Trade.Wares` (6; 3 kg a ware) valued by each village at what a ware saves a village whose people wear none - ~300 hours of home
making over its life at the wild's ~0.09 days' food an hour (the marginal product of spare hands in these worlds): `0.5 + 25 x (1 - share in
wares)`; spared beyond 0.1 a head.
(6) **Pages**: the building page (its looms by name, wares made in hours, store against target, taken, sold, fibre), the person page ("a weaver at
the weaver's shed" read from practice - before, every competent loom-weaver, i.e. nearly every adult, was "a weaver"; the household's goods and
how many are wares), the settlement page ("Cloth and goods"). The view: the shed tinted as its village builds, cloth on tenters behind it while the
village holds wares (a second length when it holds wares to sell) - kit `tenter`; camera view `work23near`. Headless: `wares:` line, `--noweave`.
Sim version 57.
(7) **A lived save and load continue exactly again** (pre-existing; found by C3.1's save test). A world saved and loaded mid-day went another way
within days (seed 615, day 6: a child sent to play 4 m elsewhere). Cause: a village whose square lies in the water has its green on the shore of a
lake; lake depths move every day (`Drainage.Derive` from the day's routing) with no version, but the agents cached the green for good - a loaded
world read the lake afresh. The same flaw lay in the drawn shore (its lake beds by the day's depths) and the river lines (D2-118: streams and
mouths by the day's flows and lake levels, rebuilt only with the drainage). Now: the green is cached for the day's water routing
(`PlanetState.WaterDay`); the drawn shore and the river lines follow only the ground's shape - depressions from the drainage
(`PlanetState.InBasin`, `Shore.DryLand`); river lines are laid for every reach draining 10 ha or more and asked daily whether it is a stream
(`RiverLines.Stream`) by homes and the view. A first fix keyed the caches on every lake or stream flip (29 a day on seed 4): +15 % run time;
the structural lines cost nothing day to day.
(8) **Works on the main road are laid beside it**, never on its line: an Edge work (smithy, barn, weaver's shed) keeps `Structures.RoadClear` (4 m)
off the bearing of the nearest village beyond the homes, where the way out runs (`Structures.Place`).
**Evidence.** `WeavingTests` 7: a shed only where the wear keeps its looms busy (from ~152 people at a 0.80 loom with stone tools; none for a
village of 18, none without the loom, none while walls rise; laid past the homes); the eight best weavers have the looms, ties to the lower id, and a
practised hand takes the weakest one's place; at the shed wares come at 1.5 x the home rate x the hand's practice from 2.5 kg of fibre each, none
without fibre, none past the target; wares wear at 0.6 (and the mix in proportion); households take wares before making their own (home making 24 h
against 251 h in three days); makers work at the shed at both resolutions (COARSE 9.0 h a maker-day, lived 6.8) and a save in the middle of it
continues as the world does; wares go where they are lacked, for grain, and what is sold raises the target. `SaveLoadContinuesIdenticallyWhileLakesRiseAndFall`
(seed 615, four saves over a fortnight); 293 headless tests. **Ten seeds 300 years DEEP (`_w4`) against the same binary without sheds
(`--noweave`, `_w4n`)**: at year 300 sheds stand in 1-27 villages a world (the largest without one 29-245 people; the smallest with one 8-201: a shed
outlives its village's shrinking), 8-223 makers; every world's households wear wares, bought ones too (villages without a shed wearing them 0-7 a
world); home making hours x0.283 (t -8.9) - with the sheds' hours, the labour of goods about halved; trades x3.03 (t 5.6), trade gain x3.78 (t 4.0),
market places x1.35 (t 1.4), markets 22 -> 24; people over years 200-299 x1.037 (t 1.3), births and deaths a thousand person-years x1.003 / x0.995,
hunger a thousand person-years x0.96, famines x1.03; households setting out, foundings and relief at par (x1.31 / 0.99 / 1.00 - a first run, `_w3`,
before the road rule, had shown 0.88 / 0.91 / 0.82: noise, not a mechanism); run cost a person-day x1.005. **Where the cloth goes** (seed 4,
`run --waresflow`, a tools tally that changes nothing): 4,688 wares traded in 300 years (2,789 from shed villages to villages without) for 6,364 kg of
fibre, 5,446 days' grain, 929 hides and 331 kg of metal - the flax chain is the main return; the grain is small beside relief (298,515 FU): in these
worlds cloth does not feed a town (an hour of spare hands is worth the wild's 0.09 days' food). Shots on the first run showed a shed standing in the
road out of its village (Edge works were laid on the bearing of the nearest village, where the way runs): Edge works now keep `RoadClear` (4 m) off
that line, beside the road (`_w3` -> `_w4`: people x1.02, hunger x0.94 - inside the noise). The save/load fix and the road rule together (`_d7` ->
`_w4n`): people x0.99, famines x1.06, palisades x0.96 - inside the noise. Shots `tc2/out/shots/c31v11` (Toularndal: a tiled shed, weavers in its open
bays, the dye vat, the yarn rack), `c31c5` (Stairby: the shed and the smithy beside the road at the village's edge, madder-red cloth on the tenter),
`c31b14` (Demtrerhope: the shed among the town's houses, a barn, the smithy out by the fields), `c31a` (Brernthorpe), `c31v5` (the shed in the road,
before the rule). Kit preview `weavers_shed`/`tenter`. Web build of this code: Web test 6/6 (`tc2/out/webtest/c31web`: the browser bit-identical
to headless; views 74-121 fps, the person and place pages 76-85 fps; a day away catches up 96 years in 57 s); the settlement page shows "Cloth and goods".
**Open.** A town's eight looms cannot keep up (towns of 500+ keep no wares and wear them for 67-96 % of their people; their households still make
homespun) - more looms or a second shed where the makers work full while the store stays short (C3.1b). A shed outlives a shrinking village. A COARSE
maker-day (9.0 h) is a third longer than a lived one (6.8 h): the target binds most days; calibrate when capacity binds (C3.1b). Wares are the
village's and free to its households - payment in kind and household holdings are C3.3. Roles other than the weaver's are still read from what a
person knows, so at year 300 one person is "known for" every craft: read them from practice as the weaver's is. The tenters stand at the shed's end and
behind it; from some angles a house hides them, and the looms inside are dim from above.

## D2-120 — 2026-10-08 — More weaver's sheds where a town's looms cannot keep up with its people (C3.1b)

**Context.** D2-119's Open: towns of 500+ kept no wares and wore them for 67-96 % of their people while their households still made homespun -
eight looms make ~0.7 wares a day, a town of 800 wears through ~1.6 of homespun's worth (~1.0 of wares). Medieval towns had as many weavers as their
cloth called for.
**Decision.** (1) **Another shed where its people outrun its looms** (`Weaving.Tally`/`Year`/`MoreWanted`; `Settlement.Sheds`, `ShedShort`,
`ShedMore`; sim version 58): each day a village's own households have drawn its store below half what it keeps back from trade (`Kept`, 0.05 a head)
while it holds fibre for a day's work at every loom is a day short - its looms, not its flax, could not keep up; at the yearly weighing (the day
households weigh leaving) a village short on `ShortDays` (30 of 48) or more wants another shed - eight more looms for its next best weavers - laid
out when no work is under way, after water and a store, never while walls rise, its cause the first shed's raising; up to `MaxSheds` (4). A first
rule counted a day short when the store was under half its target: traders take every ware above `Kept`, so a village that sold kept its store at
`Kept` under a target raised by those sales and was "short" every day - most villages of 200+ raised two to four sheds and hamlets of 20-50 kept
two (`_w5`); sales say nothing of the looms. And the shortfall alone is not enough: while a village's people change over from homespun they draw
at homespun's rate, so villages of 150-270 raised a second shed they would not need once all wore wares (`_w6`) - another shed is wanted only where
its people, all in wares, would wear through more than its looms make (`NeedsMore`: ~340-490 people for a second shed by the loom and the tools,
~730 a third). (2) **Makers**: the village's `Looms` x sheds best weavers (`LoomsOf`; the bar read daily); each works at
the working shed nearest their home (`ShedFor`; lived, the shed's id rides on the activity). (3) A shed lost (the sea) hands the village's first-shed
role to another standing one (`Structures.Lost`). (4) Pages: the settlement page counts the sheds, their wares and hours and the short days; the
building page names the makers of that shed. Headless: `wares:` counts sheds and villages with more than one, `place --list` "N WEAVER'S SHEDS".
**Calibration checked**: a maker's day at the shed over a whole year (two worlds, makers always wanted, seasons natural): COARSE 7.8-7.9 h, lived
6.8-7.3 h (x1.07-1.17, as near as the two worlds to each other) - the smithy's law kept.
**Evidence.** `WeavingTests` 8 (+ `ATownWhoseLoomsCannotKeepUpRaisesAnotherShed`: no short day without fibre, none for a store drained by sales to
what it keeps back; a village short most of a year but whose people in wares would wear less than its looms make wants no shed; past that (337
people at a 0.8 loom, stone tools) it does, begun for the first shed's raising; sixteen looms; makers to the nearer shed (2 and 7); the first lost,
the second is the village's shed). Ten seeds 300 years DEEP (`_w7`) against C3.1 (`_w4`): 1-45 sheds a world, villages with more than one 0-15 - the
towns (500-1,025 people: 2-4 sheds) and places that were larger once (sheds outlive a village's shrinking: Kubroford, 277 people, three); makers
8-360; home making hours x0.67 (t -4.2), market places x1.18 (t 2.3), markets x1.22 (t 2.1), trades x1.07, people over years 200-299 x0.99, births
and deaths a thousand at par, famines x0.97; run cost a person-day x0.94. Against the same worlds without sheds (`_w4n`): trades x3.18 (t 4.8),
trade gain x3.6, home making hours x0.19 (t -8.9), market places x1.48 (t 2.2), markets x1.38, people over years 200-299 x1.03 (t 1.1), famines
x1.00. Web build of this code: Web test 6/6 (`tc2/out/webtest/c31bweb`: the browser bit-identical; views 81-120 fps, the pages
71-84 fps). Shot `tc2/out/shots/c31d` (Dahope, seed 42, 866 people, three sheds: a walled market town of streets).
**Open.** No looms for buyers: a village that sells all it spares does not raise looms for them (sales raise its target, never its sheds) - export
crafts wait for exchange that pays the makers (C3.3). Sheds outlive their village's shrinking (a second shed in a village of 200 that was 500).
The COARSE maker-day stays ~1.1 times a lived one over a year.


## D2-121 — 2026-10-08 — The potter's kiln, the village's jars and its hearths' pots of real clay (C3.2); clay by the ground's shape

**Context.** C3's second craft (`26` §2). Measured first (the `_w7` saves at years 100 and 300, `place --stores`, `run --rot`): D2-063 kept a
village's store at 0.65 of the open store's rot because its *best potter* could fire jars, and a household cooked on 0.8 of the wood because one
of its people could fire pots - with no clay, fire or hours. Pottery is known in nearly every village (its best 1.00; 70 % of people competent, 98 %
of households holding one), so the 25-60 % of villages with **no clay within reach** kept sealed stores and cooking pots they could not have made,
and every adult was "a potter" on the person page. Stores are large (3-7 years of need at year 100, 0.4-1.4 at year 300; 14.6-17.3 % of every
harvest rots); cooking burns 72-91 % of a village's wood. The clay test itself read the day's streams and lakes: a dry spell took a village's clay.
**Decision.**
(1) **Clay by the ground's shape** (`Learning.Clay`/`ClayCell`): ground within 1.2 km where a reach drains `ClayAreaHa` (200 ha) or more - a river
with a floodplain - or a depression (a lake's bed), wet or dry this season; from the drainage only (cached by its version - D2-119's rule; a loaded
world rebuilds the drainage as it was built). Over six worlds it gives about as many clay villages as the daily test did (s42 15/15, s3 7/7, s1
2/2, s6 12/10, s4 26/20, s2 22/15). Used by discovery, founders, building materials, kilns and hearths alike.
(2) **Jars** (`Settlement.Jars`, kg standing in its granaries and barns; a kg seals `Pottery.JarHolds` 4 FU): the store rots at `1 - Seal x sealed`
of the open store's rate (`Seal` 0.35: D2-063's 0.65 when all is sealed), sealed = jars x 4 / the grain its granaries hold (heaps beyond them are
never in jars); jars break at `JarBreak` 5 % a year. Large jars want a kiln's even fire: they come only from a kiln, the village's own or bought.
Ware in store is stood as jars as far as the grain lacks them, after the hearths have drawn (`Pottery.Day`).
(3) **Hearths**: a household one of whose people can fire pots where its village has clay fires its cooking pots among its goods (D2-058's hours;
D2-063's 0.8 of the wood by its best potter); one that cannot cooks in **kiln pots** it holds (`Household.Pots`, kg; `PerHead` 2 kg a member;
broken with use, `PotDays` 400), drawn daily from its village's ware - or on the full wood (`Pottery.CookFactor`); what its hearths still lack is
known to its potters and its traders (`Settlement.HearthShort`).
(4) **The potter's kiln** (`Structures.Kiln` 24, Function Craft, Zone Site, 120 h and 15 loads, falls in 40 years): raised at its clay - the nearest
clay ground, off the river's bank and dry, or the village's edge where the clay is in it - by a village that can fire pots, with clay within reach,
a store, no walls rising, where the jars its granaries' grain lacks would keep a potter at work two months (`KilnFrom` 1,440 kg, ~5,800 FU
unsealed). At a month's work (720 kg) hamlets of 25 raised kilns and spent their few grown hands' free days at them (a test world's lived band
went with them); a firing wants its crew and loads through a season, and a hamlet fires its few pots in the open and buys its jars. Cause: the
pottery technique's event (`Settlement.PotEvent`), else the founding.
(5) **Its potters** are the village's `Hands` (4) best by skill and practice (`Settlement.PotBar`/`PotBarId` daily; `Person.SkPot` grows 0.0015
an hour) - lived, a choice by day (u 0.5) to `ActivityKind.Pot` (36, `WhyCode.Pot` 46) at the kiln; COARSE/DEEP, a labour day there less the walk
out to the clay (`Stone.AfterWalk`) before the shared field work (the smithy's law). `KgPerHour` 2.5 x (1 + 0.6 skill) x (0.8 + 0.4 practice)
x health of fired ware - clay dug by it and worked, thrown and coiled, dried and fired - on the village's wood (`WoodPerKg` 0.05 loads; none in
store, no firing; the woodcutters cut for it, `Pottery.WoodDay` in `FuelDay`), up to `Target`: the jars its grain lacks + the pots its hearths lack
+ `Keep` 1 kg a head + what it sold lately (halving each year).
(6) **Trade**: `Trade.Pots` (7, kg), valued `0.3 + 2.5 x unsealed x (a store worth sealing) + 0.6 x its hearths' lack` - a kg seals 4 FU and keeps
~3 FU over its twenty years - spared beyond `Kept` 0.5 kg a head. A sale raises the seller's target (its potters make for the next buyers).
(7) **Roles from practice**: "a potter at the kiln" (or "a potter") from hours at a kiln (`SkPot` >= 0.3) - no longer everyone who can fire a pot.
(8) **Pages and view**: the building page (its potters by name, ware fired in hours, its jars and the grain they keep sealed, ware against target,
pots its hearths took, sold), the person page (the role; how the household cooks and on how much wood), the settlement page ("Pots and jars").
Kit `kiln` (an updraught kiln - a beehive dome of daubed clay on a stone footing, its stoke-hole sooted, a vent - beside an open drying shed of
unfired pots, the wheel, the fuel), `clay_pit` (wider as the ware fired there grows), `storage_jars` (by a granary while its village's jars keep
its grain), `ware_stack` (in straw by the kiln while it holds ware to sell); smoke from the vent while a potter works at it; camera `work24`. Yard
trees are no longer drawn on a work's ground (one grew out of the kiln's dome). Headless: `pots:` line, `--nokiln` (D2-063's rules on the same
binary), `--potsflow` (what ware was traded for), `place --stores` (the survey), `place --list` jars. Sim version 59.
**Evidence.** `PotteryTests` 8: clay is the nearest ground the rule names and stays through a dry season (24 villages over six worlds: 17 with
clay, 7 without); a kiln is wanted only with clay, the craft, a store and grain enough unsealed, never while walls rise, laid at the clay on dry
ground and caused by the craft; the four best potters work it and a practised hand takes a place (the role read from practice); ware comes at the
hand's rate on the village's wood, less the walk in COARSE, none without wood or past the target; jars keep 0.65 sealed and in proportion, never
heaps, and break a twentieth a year (Off: D2-063's rule, no ware traded); a hearth without clay draws kiln pots, cooks on 0.8 of the wood, breaks
them with use, and in a running world burns less wood than without; potters work at the kiln at both resolutions (COARSE 7.6 h a maker-day, lived
6.8 - the weaver's ratio) and a save in the middle of it continues as the world does; ware goes for grain to a village whose grain lies unsealed,
and the sale raises the seller's target. `BiographyTests` follows the role to practice; `TheCoarseDayRaisesBandsAtTheLivedRate` now counts six
worlds (bands are one in three worlds: jars moved the one lived band of the first three to the next three by a store a few per cent lower - with
D2-063's rules it came back; six worlds: lived 1 / COARSE 2 with kilns, 2 / 2 without). 302 headless tests. **Ten seeds 300 years DEEP (`_k1`)
against the same binary without kilns (`--nokiln`, `_k1n`)**: at year 300 84 kilns in 169 villages (113 with clay), 334 potters; 8,366 t fired
(95 % stood as jars), 1,988 t traded (mostly for grain: seed 4 367 t for 115,703 FU); jars seal 80-100 % of the grain in granaries, 68 villages
half sealed or more without a kiln (a town buys its jars from the potters' villages round it: seed 4's largest, 842 people, clay and no kiln, 100 %
sealed); hearths 11,677 firing their own pots, 3,663 in kiln pots, 968 without (6 %). Trades x5.4 (t 13.9), trade gain x5.4 (t 12.3), **markets x3.6
(23 -> 99, t 3.9)**, market places x3.2; home making x0.94; wood felled x1.03 (the kilns' firing); grain rotted against harvested at par (x0.98: the
jars seal what D2-063 sealed for nothing); people over years 200-299 x0.99 (t -0.8), births and deaths a thousand x1.006 / x1.012, hunger a
thousand person-years x1.07 (t 1.2), famines x1.06 (t 0.7), takings x1.17 (t 1.5) - inside the noise; run cost a person-day x1.05. The clay rule
alone (`_w7` -> `_k1n`): people x1.03, famines x1.05 - inside the noise. Shots `tc2/out/shots/c32a`/`c32b2` (Krumere, seed 4 year 300: the kiln at
the village's edge with its drying shed, wood and clay pit, storage jars by the granaries at the market), `c32c21` (Krailddal: ware set out in
straw for sale - it sold 2 t lately). Web build of this code: Web test 6/6 (`tc2/out/webtest/c32web`: the browser bit-identical to headless; views
86-120 fps, the person and place pages 68-84 fps - the place page tells "Pots and jars"; a day away catches up 96 years in 57 s).
**Open.** Ware is the village's and free to its households (payment in kind: C3.3). Pottery makes most of the world's trade now (2,000 t over ten
worlds): market places come where potters' villages sell - re-weigh C9.4 (cargo by sea) with this traffic. Kilns do not multiply in towns (one a
village); a kiln's crew is four. Clay-less villages without traders in reach rot at the full rate.

## D2-122 — 2026-10-09 — Carts: a household's handcart or ox cart where its backs are too small (C4.2)

**Context.** C4's second slice (`24` §2; roadmap `25` C4). Measured first on the C3.2 worlds (seed 4, `run --loads`, `place --haul`): **83 % of the
trading loads set out were bound by what one person carries** (`Trade.LoadKg` 25 kg) - more was spared at home and wanted at the other end; grown
oxen are scarce in mature worlds (1-2 % of households at year 300, 13 % at year 100: fields have taken the pasture), so ox carts alone would change
little; the ways between villages are mostly paths and roads by year 300 (seed 4: 84 paths, 34 roads, 8 tracks); woodlots lie 350-1,130 m out.
Carrying, not cutting, was most of the work of wood and stone from far off (`24` §1).
**Decision.** `Core/Places/Carts.cs` - carts are household property (`Household.CartKind/CartState/CartHours/CartAt/CartNeed`), nothing on the
village's ground.
(1) **Two carts**: a **handcart** (two wheels, a bed of planks, shafts; pushed or pulled by its person: `HandKg` 120 kg at `HandPace` 0.85 of a
walker's pace) or, where the household keeps a grown ox, an **ox cart** (`OxKg` 500 kg at `OxPace` 0.7) - which stands in the yard while the
household keeps no grown ox.
(2) **Wanted** when the household's backs were too small `NeedTrips` (3) times, none `NeedYears` (2) apart: a trader of it carried a back's load
where more was spared and wanted along a way worn to a path or a road (`Carts.Want` from `Trade.Load`; a track takes no wheels), or a stint of it
carried wood or stone home from `HaulFrom` (400 m) or more (`WantHaul` from `Woods.Cut`/`Stone.Cut`) - and one of its able people (16-59, well)
works wood well enough to shape a wheel and an axle (`Wright`, `SkWood` 0.4). After one trip, carts were made that then stood in the yard.
(3) **Made** in the household's hours like a boat (C9.1): begun by the village's day when the store's wood allows (3 loads, an ox cart 10, after
two days of the hearths' wood) - **never while hunger sends the village to the wild** (`AgentSystem.WildNeed` above `ShortBound` 0.42, the bound
at which its people forage instead of making it): begun so, 66 % of a hungry world's carts were begun short and 95-97 % of those given up were given
up short, most with no hour in them; it waits, wanted, for a fed season. `HandHours` 30 (`OxHours` 80) at the maker's building pace; lived, a choice
in the yard (u 0.5, `WhyCode.Cart` 47, the maker's woodworking grows); COARSE/DEEP, up to `HoursADay` 2 h of a grown member's day **before** the
village's building (given only what building left, half the carts begun were never made). Given up when not made in `PatienceYears` (2): its
timber goes back to the woodpile.
History `CartMade` (90) under the household with its maker. **Worn** after `HandYears` 12 (`OxYears` 20), left in the yard; the household may
want another. The heir takes the cart when a household ends, and the last to leave a household takes it along.
(4) **Used** - one law at both resolutions: a trader of a household with a usable cart loads a cart's load on a worn way (`Carts.LoadFor` in
`Trade.Load`; the return load in `Trade.Exchange`) and travels at its pace (`AgentSystem.BeginTravel`, `Carts.Pace`: a cart's pace only while the
load is more than a back's); wood and stone come home by the cartload - the walk home shared by a trip's loads (`Woods.Rate`/`Stone.Rate` with
`trip` loads at `pace`, `Carts.Haul`). Over the sea nothing changes (a boat's load). `--nocarts` (`Carts.Off`) gives the world before carts on the
same binary - the rates reduce exactly to the old ones (`_c1n` equals `_k1` bit for bit).
(5) **Pages and view**: the person page (the household keeps a handcart made in year N; is making one, hours of 30; wants one; worn past mending;
"no grown ox to draw it now"), the settlement page ("Carts": handcarts, ox carts, being made), cargo "on the household's handcart", the history
line (who shaped its wheels), the activity "shaping wheels and an axle for the household's handcart in the yard: what they carry is more than their
backs can take". The view: carts parked in the yards by the end wall away from the woodpile where the ground is free (`Plot.CartEnd/OxEnd`), a
handcart pulled behind its trader on the road (a load of sacks while it carries more than a back's load), an ox cart behind its ox with the person
at the ox's head, a cart beside its people at the woods or a quarry. Kit `handcart`, `ox_cart`, `cart_load`. Camera `cart`, `cartfar`,
`cartyard`. Headless `carts:` line, `--nocarts`, `--loads` (the survey), `--carttally` (why carts are given up), `place --haul`. Sim version 60.
**Evidence.** `CartsTests` 6: a trader whose back was too small three times along a path brings a cart home (an ox cart where the household keeps a
grown ox; from the store's wood); needs years apart never add up; no wheels on a track, no cart without a wright; far woods want one, near woods do
not; a cart is made in the household's hours at both resolutions (COARSE 11 days, lived 4, seen in the yard, told in history) and a save with one
half made continues as the world does; on a path a handcart's load at its pace, a back's on a track, an ox cart only while it keeps a grown ox, and
`Off` is the world before carts; wood and stone come home by the cartload (woods 800 m: 0.91 loads an hour on the back, 1.11 by handcart, 1.17 by ox
cart; 200 m: no gain); worn carts are left and the heir takes one; **no cart is begun while hunger sends the village to the wild, and one given up
goes back to the woodpile**. **Ten seeds, 300 years DEEP (`_c2`) against the same binary without carts (`_c1n`, `--nocarts`, which reproduces the
C3.2 reference `_k1` exactly)**: at year 300 6,119 handcarts and 133 ox carts among 15,322 households (41 %); over 300 years 58,072 carts begun,
54,766 made, 2,824 given up (4.9 %); 12,758 trading trips with a cart, 861 t carried out. **Trades x0.67 (t -4.8)** - fewer trips, bigger loads -,
trade gain x0.90 (t -1.2), market places x0.87 (t -1.7); people over years 200-299 x0.97 (t -1.1), births and deaths a thousand person-years
x0.999 / x1.003, hunger deaths a thousand person-years x0.99, **famines x0.89 (t -1.5)**, wood felled x0.98, run cost a person-day x1.00. The first
run (`_c1`, carts begun in hunger): famines x0.83 (t -3.4), hunger deaths x0.93 (t -2.3), trades x0.70 (t -4.9) and people over years 200-299 x0.955
(t -2.4) - **investigated (session 14)**: the per-person rates were identical (births 43.9 vs 43.8 a 1,000 person-years, net growth 7.20 vs 7.21,
hunger deaths lower with carts); seed 4's loss was one bigger epidemic at year 180 (1,289 sickness deaths against 734), seed 42's an early chance
split (years 20-40, with no hunger, no sickness and 4 cart trading trips in 100 years); a placebo over the ten seeds (`--nocarts --nudge 20`,
`_c1p`: one FU more in one store at year 20) spreads as widely (people x0.982, the sd of the log ratio 0.069 against the carts' 0.061; seed 1
x0.857, seed 5 x0.889) - carts against the placebo x0.972 (t -1.06): noise. The placebo moves blows (x0.88, t -3.2) and watch nights (x0.90, t -3.5)
as far as carts do: conflict counts are noise at this spread. The hunger rule (`_c1` -> `_c2`): carts given up 15.5 % -> 4.9 % (`run --carttally`:
in the hungry worlds 34-66 % of carts had been begun short and 95-97 % of those given up were given up short, most with no hour in them), carts made
x1.01, every outcome inside the noise (people x1.01, famines x1.07, t 0.8). Over two runs the carts' effects that a placebo does not reproduce are
trades x0.67-0.70 and famines x0.83-0.89.
Shots: `tc2/out/shots/c42a` (seed 4 year 300: a handcart with a trader on a market green; the yard camera framed a roof; woodcutters with their
handcarts standing out in the sea - D2-123), `c42c` (Tovaldton, `_c2` year 300: a handcart parked by a hut's end wall beside its garden and woodpile;
woodcutters with their handcarts on the sand by the water). Full suite 308/308 (carts, the hunger rule, D2-123's spots). Web build of this code (with D2-123 to D2-125; commit 96e38ab): Web test 6/6 (`tc2/out/webtest/s15web`: WebGPU, the browser bit-identical to headless, views 77-121 fps, the person and place pages 69-88 fps, a day away catches up 96 years in 60 s, no console errors).
**Open.** Carts serve their own household only (no lending, no village carting of the harvest, a palisade's stone or a lord's tribute); no pack
animals or sleds; no road works or upkeep (C4.1's wear is the only road); the cart's ox works no field the day it draws (the ard is separate).
Crossings were surveyed (D2-124): every stream a worn way crosses is fordable.

## D2-123 — 2026-10-09 — Nobody works in the water: lived work spots on dry ground (the `wet` audit)

**Context.** C4.2's first shots showed people with their handcarts standing out in the sea. D2-111 had kept shore fishers and a drowned square's
people out of the water; the other work spots were never checked. A new headless audit, `wet --load SAVE [--days N]`, lives a saved world from its
next midnight hour by hour and counts every person not out in a boat whose place is under the drawn water (`Boats.Afloat`: the sea, or a lake
deeper than 0.3 m) by activity and village, with the standing buildings on wet ground. Seed 4 at year 300 (`_c2`): **0.64 % of all person-hours
were spent in the water** - field work 2,078 of 2,535 (four villages - Veidasford, Deirokton, Tildalstead, Vumthorpe - whose field spot, set at the
founding 170 m out toward its arable land, now lies in a lake or the sea), hunting 193, woodcutting 120 (coastal woodlots: the cell's centre
+-50 m), then people eating and idling where they stood. No standing home or work was on wet ground.
**Decision.** (1) Every lived work spot scattered round a place is moved to the nearest dry ground (`AgentSystem.Ashore` = `Boats.OutOfTheWater`,
rings out to 300 m): the field hands' spot, the woodcutters' spot, and every wild ground's point (`WildFood.GroundPoint`: before, fishers only). The
stint's work is unchanged (a lived stint is timed from arrival); the walk moves by metres. COARSE and DEEP do not place people. (2) **Woods above
the shore** (as a quarry's rock, C1.3): a woodlot is chosen only on a cell whose middle stands on dry ground (`Shore.Dry`: above the sea's
two-metre margin, no lake over it) - 0-3 woodlots a world lay on the shore, drawn as a bare beach, their cutters and carts on the sand felling
woods nobody sees; a woodlot the sea has taken is chosen again (`Woods.Ensure`). This one changes every resolution.
**Evidence.** `wet` on the same save: 0.637 % -> **0.051 %** of person-hours (2,535 -> 204 in two days); what is left is people walking a straight
line across a lake between two dry spots, and an evening when a day's lake rose over Gaby's works (a flood the simulation does not yet answer).
Tests: `FieldHandsWoodcuttersAndHuntersWorkOnDryGround` (a coastal village's field and woodlot spots moved out over the sea: three lived days,
294 person-hours at the fields, the woods and the wild, none heading into the water - and it fails with the field spot's `Ashore` removed);
`WoodsAreFelledAboveTheShore` (the densest woods within reach on a shore cell, sparser woods on dry ground farther off: the dry ones chosen, three
villages; every woodlot of five worlds on dry ground). Ten seeds with the woodlot rule (`_c3`) against `_c2`: people over years 200-299 x1.04 (t 1.3), births and
deaths a thousand at par, famines x1.09 (t 1.2), foundings x1.07, wood felled x1.04 - inside the noise. `wet` on the year-300 save of `_c3`: 0.078 % (367 of 472,317 person-hours) - people within 4 m of a waterline and walks drawn as straight lines across a lake (a cook 104 m out on the way home).
Full suite 314/314 on the code of D2-122 to D2-125.
**Open.** Local walks are straight lines (across a lake between two dry spots); a lake risen over works or homes is not yet a flood people answer
(they stay and work in it) - a flood slice (C2/E) with the water's own rise.

## D2-124 — 2026-10-09 — River crossings surveyed: every stream a way crosses is fordable (C4.3 deferred)

**Context.** `24` §4 plans fords, ferries and bridges where heavily used ways cross rivers - and C4.2's carts need wheeled crossings. Measured first
(`place --load SAVE --crossings`: each way worn to a track or more, the streams its route crosses - runs of cells whose smoothed flow makes a stream,
`PlanetState.IsStream` - the largest flow crossed and the drainage above it, and the world's largest flows), five worlds at year 300 (`_c1`,
seeds 4, 42, 2, 7, 3): **the largest flow on any of them is 0.08-0.23 m3/s** (7,300-19,600 m3 a day) - a brook a person steps or wades across;
13-140 stream crossings on the worn ways a world, all of them brooks (most under 0.07 m3/s, a few up to 0.2). The 6 km planet's catchments
(D2-085: 166 km2 of land in all) make no river a walker or a cart cannot ford.
**Decision.** No crossings slice now: a ford, a ferry or a bridge would carry nothing the simulation needs (theatre). C4.3 waits for a river a
walker cannot ford - a flood (D2-123 "Open") or a larger world. The survey tool stays (`place --crossings`).

## D2-125 — 2026-10-09 — The village smith: a smithy's four places, a smith's practice, roles read from practice (C1.4b)

**Context.** Measured first (`run --crafttally`: hours each person spends at each craft, by person and by village-year; seeds 4 and 7, 300 years,
the `_c3` binary): (1) the person page called **5,730 of seed 4's people "a smith" and "a boatwright"** at year 300 - everyone competent in copper
or in the planks' craft - where 175 had worked at a smithy in two years (22 for 50 h or more) and 10 had built a plank boat (weavers and potters
were already read from practice, D2-119/D2-121); (2) **smithies had no smiths**: every copper-competent person worked at the village's smithy in
turn, in the order its people happened to come (`Forge.Wanted`: competent and work left) - smithy-years had a median of 35 hours and 3 hands, but
p99 2,900-11,100 hours and **up to 24,000 hours by 355 different people in one year** (a town's iron tools at the bloomery: 2 kg a head at 0.06
kg an hour), the busiest hand doing 6 % of a smithy's hours; a lived day sent a crowd to the forge.
**Decision.** (1) **A smithy's places** (`Forge.Places` 4: the hearth and bellows, the anvil, the bloomery's furnace) are its village's best smiths
by `Key` = copper + iron + practice (`Settlement.SmithBar/SmithBarId`, read daily and when a smithy is raised; able smiths only: copper competent,
16-59); only they work there, at both resolutions (lived, a smith's id of smithy rides in `WhyArg`). (2) **Practice** (`Person.SkSmith`, 0.0015 an
hour at the smithy or the open hearth) quickens the hand: `Forge.Hand` = 0.8 + 0.4 x practice on copper and iron (the potter's and the weaver's
law). (3) **More smithies where the smiths cannot keep up** (C3.1b's pattern): a day the work the village wants at its smithies - copper and iron
still wanted as far as its ore and charcoal go, at its best smiths' pace, and the forging of the arms it wants (`Forge.Backlog`) - is more than all
its places' labour day is a short day (`Tally`; ore or charcoal short is not the smiths' shortfall); at the yearly weighing a village short on 30
or more days, whose steady need outruns its places' year (`NeedsMore`: the tools that wear out - `Tech.ToolLoss` 3 % a year -, the iron it sold
over the year and its arms' upkeep, at its best smiths' pace; a first equipping is work done once, over some years), and with more able smiths
than places wants another smithy (`Year`, `SmithMore`, up to `MaxSmithies` 4), raised as the first was (caused
by the first's event); each smith works at the smithy nearest home (`SmithyFor`); a lost first smithy hands its role on. (4) **Roles from
practice**: "a smith" / "an ironsmith" (one who knows the bloomery) from 0.3 of practice (~200 hours), "at the smithy" while they have a place
there; "a boatwright (N plank boats)" from the plank boats they led the building of (history keeps the leader as the boat's record's Other,
`Biography.PlankBoats`). The building page names a smithy's smiths ("practised" / "new to it"); the settlement page counts its smithies, smiths and
short days; the view's smoke rises only from the smithy a smith is at. `--nosmithcrew` (`Forge.CrewOff`) is the law before on the same binary
(it reproduces `_c3` exactly, annals identical). Sim version 61 (`Person.SkSmith`; `Settlement.SmithBar/SmithBarId/Smithies/SmithShort/SmithMore`).
**Evidence.** `ForgeTests` 8: the smithy's four places go to its four best smiths and a practised hand outranks a better-taught one, a fifth smith
finds no place, practice quickens the hand (0.8 -> 1.2) and comes by the hour, `CrewOff` is the law before; at both resolutions only its four best
smiths worked at it (COARSE 223 h, lived 186 h in six days); ore or charcoal short is no smiths' shortfall; a first equipping raises no second
smithy, a village selling iron beyond its places' year does - caused by the first smithy's event, eight places for its eight best, each smith at
the smithy nearest home, a lost first smithy hands its role on, too few smiths want no more places; the exact-output tests (`ForgeTests`,
`IronTests`) take the hand. `BiographyTests`: knowing copper, iron and the planks is no role; practice makes "an ironsmith", a plank boat led "a
boatwright (1 plank boat)", a dugout does not. **Ten seeds, 300 years DEEP** (three of the ten worlds work metal: seeds 4, 2, 7): `_s1` (four places
alone) - people x0.994, famines x0.92, but iron smelted x0.83 (seed 4 x0.60) and iron traded x0.35 (seed 4's exporter x0.13); `_s2` (another smithy on
short days alone) - standing smithies 68 -> 218, every iron village raising three or four for its first equipping: rejected. **`_s3` (the slice)
against `_c3`** (the same binary with `--nosmithcrew` reproduces `_c3` exactly): people over years 200-299 x0.993 (t -0.6), births and deaths a
thousand at par, famines x0.93 (t -0.9), hunger a thousand person-years x0.98, trades x0.98, conflict at par (blows x1.03, raids x1.03); standing
smithies 68 -> 80 (seed 4: Fomthaenstead, Veidasford and Toularndal keep two); smiths read from practice 116 / 80 / 24 in the three metal worlds,
all of whom worked, the busiest hand 38-49 % of a smithy's hours (6 % before), smithy-years up to 23 hands (355 before); iron smelted x0.59 / x1.05 /
x2.08 by world, iron traded 22.4 -> 8.7 t (seed 4's exporter 14.4 -> 2.0 t), iron tools for 39 / 32 / 45 % of the people against 52 / 28 / 32 %, copper
x0.88; boatwrights 15-51 a world. Shots `tc2/out/shots/c14b` (Fomthaenstead #20, seed 4 `_s3` year 300: its two smithies side by side at the village edge, smoke from the one a smith works at). Web build of this code (with D2-123 to D2-125; commit 96e38ab): Web test 6/6 (`tc2/out/webtest/s15web`: WebGPU, the browser bit-identical to headless, views 77-121 fps, the person and place pages 69-88 fps, a day away catches up 96 years in 60 s, no console errors).
**Open.** A village sells iron only from what its places spare beyond its own people: demand it cannot meet is not seen (no unmet ask), so an
exporter's smiths do not grow with their buyers - the bloomery at the ore with its own crew (C5.3c, work at a distance) is where iron towns
belong. The open hearth (a village without a smithy) still lets every smith smelt at home; apprenticeship beside a master (C3.5).

## D2-126 — 2026-10-09 — The household's grain, what households owe, makers paid in kind, sharing in hard times (C3.3)

**Context.** C3.3 (exchange between households) was surveyed in session 13 and deferred as a phase: the village's store moved 31 % of the food
households drew. Measured again before any code (session 16; the tools ledger extended to household-years with their kin households, `run
--ledger Y --ledgeryears F`, seed 4 `_s3` from year 200, 56,240 household-years): (1) dividing the harvest by each household's crop work - the
survey's rule - is an artefact of the COARSE labour schedule (residents walked in id order: the oldest fill the crop need, households with no member
of 16-59 "brought in" 64 FU a member against 40 for young couples; net takers had more workers a member than givers, 0.60 against 0.52) - property
built on it would make the scheduler's order into wealth; (2) divided by mouths (the commune's law for fields worked in common) households are
mostly self-sufficient year to year (deficits 8 % of what they eat, mostly covered by their own carry-over); differences come from their own beasts
and catches, household formation and growth, and work for others; (3) 82 % of households have close kin households in their village; kin could
cover 43 % of the survey's deficits.
**Decision.** One physical granary as before; whose grain it holds and what is owed (`Core/Places/Holdings.cs`, `Household.Grain/WaresDue/PotsDue`,
`Settlement.Common`, `World.Holdings` with the debts table, sim version 62):
(a) **Bins and the account**: the harvest shared among a village's households by their mouths as it comes in; what a household brings home beyond
its larder (its beasts' milk and meat, its catch) is its own; the first store by mouths; a married child takes their mouth's share of the parents'
grain; heirs take grain, debts and claims (no heir: the village's grain, the debts lapse); parties carry their own grain first (the old amount) and
their debts lapse; a party's food is its households' at the new home. Daily `Reconcile`: outflows nobody drew (rot, theft, sales, relief and
tribute given, visitors' meals) are borne by all holders by their shares; inflows nobody brought (bought, given, paid, returned, tribute) are the
village's common grain.
(b) **The moral economy** (`Draw`, the larder fill at the village's ration as before): own grain; the common grain (unreckoned); close kin's spare
beyond their own needs to the next harvest and six days (a gift, unreckoned); a loan from those it owes already and the households with the most
to spare (`Debt`, repaid after each harvest from what the debtor holds beyond a year's needs, oldest first); what is still wanting falls on all by
their shares. Meals straight from the store: own grain, then the common grain (`Take`).
(c) **Makers paid in kind**: a maker's make is theirs in the store until taken; a household that takes wares or kiln pots pays their makers the food of
the maker-days it took (`Price`: a day's bread for a day's work at the workshop) from its own grain - and takes no more than its grain pays
(`CanBuy`: no credit; a household short of grain makes its own goods or cooks on the full wood); goods bought in were
the village's (their worth to its common grain); the village pays for what it uses in common (jars, a smith's hours for its tools and arms at the
smithy or the open hearth, wares and ware it sells abroad) from its common grain, then by a **levy** on every household's grain by its share (the
smith's own excepted). Common duties anyone does (fields, wood, building, quarrying, mining, charcoal) are the commune's, unpaid.
(d) **Hard times** (C3.3c; Dirks 1980): below a village ration of 0.7 (`Contract`) sharing contracts - a larder and a meal from the store get only
the household's own grain, the common grain, what its kin spare down to the village's ration, and loans of true spare; nobody's need is taken.
(e) `Architecture.Means` reads the household's own grain on D2-114's scale (365 FU a member: kept so the plans keep their calibration).
Pages: the person page's home section, the settlement page's "Grain:" line. Headless: `hold:`, `makers paid:`, `hunger by household:` lines;
`place --households` (grain, owes, owed), `place --debts`; `--nohold` (reproduces `_s3` exactly), `--holdmeansoff`, `--nopay`, `--nohard`.
**Evidence.** `HoldingsTests` 12 (the first store and the harvest by mouths, a catch its own; the larder's order own -> common -> kin -> loan, a loan
only from spare; repayment oldest first from a year's surplus; losses borne by shares, inflows the village's; an heir's grain, debts and claims;
**the accounting moves nothing** - lived and COARSE, with each village's grain premised into one household so kin gifts and loans flow, the same
world as `--nohold`; a save with debts continues the same; makers paid by their shares, no grain no wares, bought-in goods the village's; the
village's jars and smith's hours from its common grain then a levy with the smith excepted; hard times: kin down to the ration, others keep their
needs, the rest unshared). Seed 4, 100 years: with `--holdmeansoff` every report line equals `--nohold`, which reproduces `_s3` exactly. **Ten
seeds, 300 years DEEP**, each step against the last on the same mechanics: `_h1` (bins, moral economy, own-grain means) vs `_s3`: people x0.997
(t -0.07), births and deaths at par; `_h2` (+ makers paid, takers owing) at par, but 205,835 debts in the table (a taker owed every maker a sliver,
79,601 in one world); `_h4` (makers paid as far as the taker's grain pays, `--nohard`) vs `_h2`: debts 37,398, home making x1.19 (t 4.2), ware
traded x1.38 (t 3.8: unsold at home, it goes abroad), trades x1.22, hunger deaths x0.92 (t -3.1), people at par; `_h3`/`_h5` (+ hard times) vs
`_h2`/`_h4`: people x0.995/x0.999, famines x1.25 (t 3.8)/x1.22 (t 2.4), hunger deaths x0.90/x0.95, takings x0.83/x0.87 (t -2), raids x0.48/x0.69,
wounds x0.74/x0.79, relief x1.61 (t 5.2)/x1.11 - more villages lose someone to hunger while their store still holds others' grain ("famine amid
plenty", Sen 1981), neighbours' relief comes to them, and fewer villages run dry and raid. **The slice (`_h5`) against `_s3`**: people over years
200-299 x0.976 (t -0.6), births and deaths a thousand at par, hunger deaths x0.96, famines x1.28 (t 2.5), blows x0.71 (t -2.6), takings x0.83, watch
nights x0.73, trades x1.24, ware traded x1.28, relief x1.37, foundings x1.07, markets 98 -> 102; house plans small/cottage/long/two/hall/row
34/27/7/26/1/5 -> 37/28/8/22/0/6 % (means by the household's own grain, not its village's). At year 300 over ten worlds: 15,979 households hold 2.36M
FU, the villages 170k; grain a member p10/p50/p90 0-10/8-59/34-211 FU, Gini 0.51-0.77, the richest tenth 38-69 %; 45,947 debts (942k FU) of 6,458
households to 3,126, one household owed by up to 159; over the run 5.76M FU fed from common grain, 1.84M given by kin, 4.09M lent, 1.72M repaid,
1.34M lapsed (households that left or died out without heirs); households paid makers 4.87M FU, makers earned 5.63M (owed 17k), smiths 84k; the
villages paid 511k from common grain and 327k by levies; practised makers' households hold more grain a member than the others in all ten worlds
(1.1-2.7x). Hunger deaths (36,580): 98 % in households with no grain of their own, 92 % in households in debt, 32 % with no close kin in their
village. Seed 4's debts at year 300 are mostly a season's (median 0.8 years old, a debtor owing a median 30 FU); creditors are small households of
elders with grain and beasts. **Cost**: from the same year-200 save, 24 years four times each: 8,151-8,565 ms without the accounting, 8,180-8,437
ms with it (+0.4 % at the lower envelope); the 300-year runs cost more a person-day (x1.18 against `_s3`) because the worlds diverge (more villages)
and they ran beside other jobs.
**Open.** Debts have no consequence yet beyond repayment (no work for a creditor, no flight from debt, no standing); land is the commune's (the
harvest by mouths) - household fields, inheritance of land and labour for others wait until land binds; trade between villages is still the
village's own (grain sold falls on every bin by its share); the hungry poor never take from their own village's store at night (only neighbours').
**Corrected by D2-129.** The reading "famine amid plenty" above is wrong: measured at each hunger death, what the households with grain could spare
beyond their own needs at the village's ration covered under a quarter of the grainless households' needs in 95 % of the deaths; hard times decide
who goes hungry in a real shortage, not whether there is one.

## D2-127 — 2026-10-09 — Standing and the village's first household: followings, headmen, the lord's tribute (C10.2)

**Context.** C3.3 (D2-126) made claims real - households owe each other grain, one household owed by up to 159 others - but nothing read them;
villages had no leaders (only the boldest leading a band) and lordships rarely held (0-2 a world). `10` §4/§6.6 plans leaders from prestige
(generosity in famine, kin, deeds, age). Research (session 16): big men turn surplus into followers through gifts and loans, rarely holding more
than ~80 core followers, their successions contested (Sahlins 1963; Henrich, Chudek & Boyd 2015); villages split at ~127-158 (Alberti 2014,
Hutterite fission data) or ~200 people (Chagnon) unless integrated, with elites and formal ritual above ~500 (Bandy 2004).
**Decision.** `Core/Society/Leaders.cs` (sim version 63). (a) **A household's following** = the village's households bound to it: its close kin
households, those that owe it grain (the debts table), those it fed in hard times in the last eight years (`Household.Patron/PatronAt`) - read,
never stored. (b) **The first household and the headman**, weighed yearly after the harvest and at a vacancy: the household with the largest
following (ties: more grain, then the lower id) - unless the incumbent's following is still 0.67 of it (`Hold`); its eldest grown, able member
(18+) is first among the people (`Settlement.Head/HeadHh/HeadSince/HeadEvent`); at a death the place passes within the household while it still
qualifies; `HistoryKind.HeadmanRose` (91: the first; within the household after the dead; another household rose). (c) **What it does**: in hard
times (D2-126 c) the first household's grain opens to the village's hungry down to the village's ration, after kin and before loans - a gift, and
those fed follow it; **tribute** paid to a lord village is its first household's grain (the lord's own store), not the village's common grain.
Pages: the settlement overview ("Its first household: the Xs, followed by N households (K of kin, D owing them grain, F fed by them in hard
times); first among its people Name, age, since year Y"), the person identity ("First among the people of V since year Y"). Headless: `leaders:`
line, `place --leaders`, `--nohead` (reproduces `_h5` exactly), `--notribhead`.
(d) A loan makes the lender the borrower's patron (`Holdings.Lend` sets `Patron`): the patron-client tie outlives the debt; `Hold` 0.5.
**Evidence.** `LeadersTests` 5 (the household most follow gives the first and its eldest; an incumbent is not unseated by a hair but is by more
than twice; at a death the place passes within the household; in hard times the first household feeds the hungry down to the village's ration as
a gift and they follow it, nothing opens with `--nohead`; tribute to the lord village's first household, the common grain with `--notribhead`; a
lived and a COARSE village find their first and save it). Seed 4, 100 years: `--nohead` reproduces `_h5` report for report. **Ten seeds, 300 years
DEEP** against `_h5`: `_l1` (headmen, `--notribhead`, `Hold` 0.67): people x0.971 (t -0.5), hunger deaths a thousand person-years x1.07 (t 2.2:
the first household spreads its grain thinly in long hard times, partly undoing the triage of D2-126 c), raids x0.61 (t -2.3: fewer fit people
below the hunger line that raises bands); headship changed hands every 4-15 years, 83 % of the time to another household - the season's debts,
repaid at each harvest just before the yearly weighing, made the following swing. `_l2` (+ tribute to the lord's household): lord households
rich (Bristead's first household 428 days of grain, followed by 41 of 69 households, 35 of them fed by it). `_l3` (the slice: patron ties from
loans, `Hold` 0.5): people x1.033 (t 1.0), births and deaths a thousand at par, hunger deaths x1.07 (t 0.9), famines x1.10 (t 1.6), raids x0.82
(t -0.6); 3,762 headmen risen over ten worlds (19 % within the household after a death, against 13 %; 77 % another household), the place held 4-12
years on average at year 300 - headmen are the eldest (often 60-75) and a patriarch's death breaks his children's households' kin link to his
(lineage segmentation; big men's places are not inherited); first households fed the hungry 294k FU. **Binding falls with size** (`place
--binding`, the `_l3` saves at years 100/200/300, 402 village-ages): households bound to the first household (it and its following) median 43 %
in villages under 100 people, 24 % at 100-200, 20 % at 200-400, 8 % at 400+; the largest following among the unbound p90 6/14/23/34 households,
up to 90 - scalar stress, the evidence for C10.3. Cost: none measurable (a person-day x0.90 in the noise).
**Open.** C10.3: a contested succession (an unbound rival's following near the new first's) splits the village - the rival leads its following
away to a known site; the headman does not yet decide tribute, walls or bands; the hungry poor do not take from their own village's store.

## D2-128 — 2026-10-09 — A contested succession splits a village (C10.3)

**Context.** D2-127 measured scalar stress (`place --binding`, the `_l3` saves at years 100/200/300, 402 village-ages): the households bound to a
village's first household (it and its following) fall with size - median 43 % under 100 people, 24 % at 100-200, 20 % at 200-400, 8 % at 400+ - and
the largest following among the unbound reaches 34 households (p90) and up to 90 in towns. Big men's successions are contested and villages fission
past ~130-200 people unless their people are bound together (Sahlins 1963; Alberti 2014, Hutterite fission data; Chagnon); fission needs somewhere
to go (Carneiro 1970: where land is circumscribed, factions cannot leave).
**Decision.** `Core/Society/Leaders.cs` `Contest`/`Pending`, `Core/Places/Migration.cs` `Split`/`KnowsSite` (sim version 64). (a) **The trigger is an
event, not a rate**: at a vacancy (the first among the people died or left), once the new first is weighed, the household with the largest following
among those not bound to it - counting itself and only its unbound followers, none of them setting out - will not follow when that following is at
least half the first's (`Hold`) and a village's worth (`SplitHouseholds` 6, `SplitPeople` 20). (b) **It leads them away to found a village at the best
place the village knows** (`Migration.Split`: one party with the split's record as its cause; households with a birth near stay; the place taken from
the village's lore). History: `VillageSplit` (92), its cause the death that opened the succession; the party's setting out and the founding follow it.
(c) **No place known: the faction looks for land** (`Settlement.SplitHh/SplitCause/SplitSince`, saved and hashed): the village sends scouts for it as
for land pressure (`Migration.WantsScout`/`ScoutsDue`); it is weighed again the day a place is known (`Leaders.Pending`) and goes if it still
qualifies; it gives up after `SplitYears` 3 and stays; it lapses if its household is gone or has become the first. (d) **Pages**: a village founded by
a split says why its founders left ("Founded in year 254 by people of Rakwick (14 households ... set out to make a new home): Igresta Sarnfair and the
Gustneins would not follow Dold Vastbruth: 16 households, 61 people of Rakwick, bound to them by kin, by grain owed or given, went with them to make a
village of their own"); the village left says "Split in year Y: ... - Reirndal"; its history tab now carries its headmen and splits
(`HistoryLedger.PlaceEventsOf`, a page-only index of political events by the village they happened in: they name people and households, so the
entity index never found them, and the entity index is not widened because the storm code reads a village's last twelve indexed events).
Headless: `place --splits` (each split, the village it founded and both villages now; foundings by a split or not; raids between villages a split made
kin), the `contests:` line (`--contests`: why successions did not split, and what became of factions that looked for land), `--nosplit` (C10.2 alone
on the same binary: reproduces `_l3` exactly).
**Evidence.** `LeadersTests` 7 (a rival followed by six unbound households leads them away and they found a village; the first's following stays and
keeps the place; a rival of two stays; `--nosplit` nothing; no place known: they stay and look for land, the village sends a scout, a save keeps the
faction, land found they go, three years without they stay; the pages of both villages tell the split). **The first version** (`_l4`: a split only to a
place already known) was at par (people x1.02, foundings x0.975) but made 10 splits in 3,000 world-years: on seed 6, of 205 vacancies 107 had no unbound
rival of six households, 42 a rival under half the first's following, and of ~50 that qualified 49 failed only for want of a known place (villages
scouted only when land was short). **The slice** (`_l5`, ten seeds, 300 years DEEP, against `_l3` = the same mechanics without splits): 42 splits
(0-12 a world; 19 in the first century, 18 in the second, 5 in the third), factions of 6-16 households (median 7) and 20-61 people (median 27); of
1,848 weighings 970 found no unbound rival of six households, 347 a rival under half the first's following, 72 too few people, 377 a faction that knew no
place - on seeds 4 and 6, of 99 factions that looked for land 64 gave up after three years, 19 dissolved into the village or rose to be its first, 13
found a place. **Land decides, not the thresholds**: foundings 123 -> 119 (x0.94, t -1.2; parties x0.91) - splits take the good places land-pressure
parties would have taken and make 35 % of all foundings; 41 of the 42 villages they founded stand at year 300 (311 people on average, the others 260 -
they are older). People x0.992 (t -0.3), births and deaths a thousand at par (x1.005/x1.010), hunger deaths x1.03 (t 0.5), famines x0.98, blows x1.13
(t 0.9), raids x0.99, relief x0.87 (t -1.5); 2 of 91 raids fell between villages a split made kin; stores a person (years 100-300) x1.08 (t 2.3).
**Villages stay smaller for longer while land is open**: at year 100 94 villages against 83 (median 68 people against 94) and 2,553 people in villages
under 90 people against 1,498 (2,227 against 1,825 at year 200) - too small for a weaver's shed, so home making rises x1.10 (t 4.3, in all eight
worlds with splits); at year 300 172 villages against 178, 23 towns of 500+ in both. **Towns that hold**: in towns of 400+ the share bound to the
first household rises from 8 % to 15 % (bound households median 12 -> 24), but their largest unbound followings grow too (median 8 -> 21, p90 34 -> 40):
the factions a full land cannot shed stay inside. Cost: none (simulated time x0.97, t -1.0). Headmen risen 3,762 -> 3,680.
**Open.** A faction that cannot leave contests from within (it could take the first place, or quarrel - wrongs between households of one village); the
headman does not yet decide tribute, walls or bands; debts have no consequence beyond repayment; the hungry do not take from their own village's store.

## D2-129 — 2026-10-09 — Household formation: a couple marries when it can keep a household to the next harvest (C3.5)

**Context.** After C10.3 the evidence pointed at famine amid plenty (D2-126: 98 % of hunger deaths in households with no grain of their own;
sharing contracts in hard times) and at the hungry taking from their own village's store. Measured before any design (session 16, ten seeds,
the `hunger by household:` diagnostic, `_d2`): at each hard-times hunger death, what the households with grain could spare beyond their own
needs at the village's ration to the harvest (and the common grain) covered under a quarter of what all the grainless households needed in 95 %
of ~40,000 deaths (mean 0.04-0.11 of it; enough for all of them in 31); the store held under a day of the village's food in 69 %; the deaths fell
5-12 days before the harvest on average. **There is no famine amid plenty: D2-126's reading is corrected** - contracted sharing decides who goes
hungry in a real shortage, and theft within the village would move hunger, not relieve it (that slice was dropped). The shortage was chronic:
hunger deaths were 9-27 % of all deaths (19 % over ten worlds, 7.1 a thousand person-years), in 120-177 of 200 years in half the worlds. And
nothing in a household's life answered scarcity before death did: women married at 20.3 on average in every world, 99 % of them by 26-29, at
the same rate in every season (`place --marriage`, new); under the old rule half of all couples (49 %, up to 70 % in the starving worlds)
married bringing under a year's bread, a quarter to a third under three months' (`marriages by what the couple brought`, new).
**Decision.** `SettlementSystem` (sim version 65). (a) **A couple marries when it can keep a household of its own to the next harvest**: on
her day to look for a match (the courting rate by age, unchanged) a woman's suitors are only the men with whom what the two of them bring - each
one's mouth's share of their household's grain and larder (C3.3: what `MoveTo` moves; a household of another village keeps its grain in its own
store) - feeds them to the next harvest of the village they will live in (his) and `Holdings.Reserve` (6) days beyond (`CoupleDays`,
`ToHarvest`, `HarvestDay` 39). None: her match waits; the clock runs only while the means are there. Where the harvest feeds a village through
its year nothing changes; where it does not, marriages wait for the harvest's grain, brides marry into villages with grain, and those who never
find the means by 36 stay unmarried (Hajnal 1965, 1982; Razi 1980; Wrigley & Schofield 1981; Kussmaul 1990). Marital fertility is untouched
(it already falls with the body's health). (b) Pages: the person page's family section (an unmarried person of an age to marry: their share's
days of bread, the harvest's distance, whether a match waits), the settlement overview ("Marriages wait: W of its U unmarried women of an age
to marry could not yet keep a household - its households' own grain keeps a mouth N days, the harvest is H days off"), the marriage's record
("... setting up their household with N days' bread of their own"). Headless: `place --marriage`, the `marriages by what the couple brought`
line (with matches that waited), the hunger diagnostic's spare-against-need line; `--nomeans` (reproduces `_d3` exactly - checked on seed 4
with the final binary).
**Evidence.** `HouseholdFormationTests` 3 (no grain of their own: the match waits, with `--nomeans` they marry; given a year's grain they marry
and the record tells their bread; what a couple brings is each one's mouth's share of grain and larder, nothing from a household of another
village; a widow of a village with no grain marries into a neighbour with grain). **Ten seeds, 300 years DEEP**, against `_d3` (the same mechanics
without the rule): `_m2` (the final binary): hunger deaths x0.69 (t -2.0; 7.1 -> 5.6 a thousand person-years over years 100-299, 19 % -> 17 % of
deaths), famines x0.70 (t -2.5), births x0.88 (t -3.2; 44.2 -> 40.6 a thousand) and deaths x0.87 (36.9 -> 33.8 a thousand), relief x0.63 (t -2.4),
sickness deaths x0.15 and epidemics x0.24 (rare events: three worlds missed their great epidemic; `_m1` x0.53 - not claimed as an effect), people over years 200-299 at par
(x0.998) and at year 300 x0.935 (t -2.1); blows, raids and walls at par; foundings at par. `_m1` (the first build, the harvest of the bride's
village) gave the same picture with more weight on conflict: hunger x0.63 (t -3.4), famines x0.65, births x0.87, people x0.96, blows x0.76 (t -3.0),
takings x0.79, watch nights x0.70; the two builds differ only for couples across the equator, so single worlds diverge (seed 4's hunger deaths
1,832 in one, 5,425 in the other) - judge the aggregate. **Marriage at year 300** (`_m2` against `_d3`): age at first marriage 20.3 -> 21.4
(22.3-23.6 in the poor worlds 1, 5, 11, 6), ever married by 26-29 99 % -> 93 %, by 45-59 100 % -> 96 % (76-93 % in the poor worlds); matches that
waited 1,564 (seed 4) to 3,974 (seed 11). **Cost**: none measurable - from the same year-200 save of seed 4, 24 years three times each, 11.0-12.0 s
with the rule and 11.4-11.9 s without (the runs' x1.07 a person-day is the worlds' difference). Full suite 336/336. Seasonality is mixed (marriages by days after the harvest 24/20/25/31 % against 27/23/26/24 %;
`_m1` 35/35/16/15 %): villages short of a year's bread marry in the last days before the harvest, because in this commune a new household shares
the next harvest by its mouths, so a little bread suffices then - the incentive of a commune that shares by "eaters" (the Russian mir, with early
and universal marriage); the NW European pattern (late marriage after the harvest, a holding to marry on) needs household land.
**Open.** Household land and holdings (marriage on a holding; the NW European pattern); life-cycle service (the unmarried young working in other
households); hunger is still 17 % of deaths - the hungry gap before the harvest (famine foods, the ration's floor of 0.45, the store's prudence)
is the next thing to measure; the base courting rate gives a younger marriage age (20-21) than medieval NW Europe's (mid-20s).

## D2-130 — 2026-10-09 — W-FOREST pass 1: kinds of wood read from the climate, their forms, ages and colours (presentation)

**Context.** Arjun (8 Oct, W-FOREST, `18` §8): forests and biomes read as the same trees recoloured; open since, deferred behind the civilisation
slices, which the roadmap says it follows (`25` §1). Measured first (`place --woods`, five worlds at year 300): the wooded land is mostly hot (22-30
degC annual mean: 40-75 % of wooded cells) or cool (2-10 degC: 25-55 %), little between; moisture 0.3-0.9; true drylands rare; wet ground common
(8-35 %). The woods were drawn as seven forms chosen by a conifer share from temperature, a river's corridor and a mixed-woods birch rule - seed
42's tropical forest as temperate broadleaf lumps of two greens, all of one height.
**Decision.** Presentation only (the woods' share and where they stand stay `PlanetState.Forest`; nothing in Core changes). (a) **The kind of wood**
(`Game/View/ForestTypes.cs`) at each crown from the simulation's climate and water: the cell's mean temperature at the point's height (the lapse
rate over its height above the cell's ground), the moisture index, soil and standing water, a stream's corridor - treeline (under -1.5 degC), river
gallery, wet carr or swamp, rain forest (20+ degC, moisture 0.55+), dry tropical woodland, warm temperate (14-20), temperate broadleaf (8-14), cool
mixed (3-8), boreal. (b) **Ten new forms** (`ProcMeshes.ForestTree`, 17 kinds x 3 variants): fir, krummholz, rain forest emergent and canopy tree,
palm, umbrella tree, alder, poplar, mangrove, snag; each kind of wood its own make-up of forms (`TerrainLod.TreeKindFor`). (c) **Stand structure**:
saplings, poles, mature trees and veterans by the crown's number; snags (more in the boreal woods; many for 30 years after a fire - the fire is real
state); lower trees at a stand's edge and glades; openness by kind (dry woodland 45 % of crowns out, the treeline 55 %, a carr 20 %); stature by kind
(the rain forest 1.45x, its emergents 1.25x above that; the treeline 0.32x). (d) **Palettes by kind** (eight; 4,096 colour groups): the rain forest's
deep glossy greens, the dry tropics' olive, the carr's grey-greens, warm evergreens' dark olive, palms, birches' light leaves golden in autumn.
(e) **Far forms**: every form has its own far mesh (fewer, plainer lumps), so a palm, an umbrella or a spire keeps its silhouette to 440 m (the old
far forms were one round crown and one cone). (f) Camera views `forest:KIND[:low|:far]` (the most wooded ground of a kind in the first village's
half of the day) for the four-climates test.
**Evidence.** Shots `tc2/out/shots/wf2/sheet4.png` (rain forest, dry tropical woodland, cool mixed, boreal side by side - no longer confusable),
`wf3` (umbrella trees, snags), `wf4/forest.png` (seed 42's forest: tropical to its far trees). Two faults found in the shots and fixed: umbrella
trees read as beige mushrooms (a short trunk, tan crowns: now a forked trunk twice as tall under an olive crown); boreal pines (a bare bole under a
flat crown) read as palms in the snow (pines now only on dry boreal ground). Web: Web test 6/6 (`tc2/out/webtest/s16web4`, bit-identical; seed 42's forest views 80-83 fps, against 60-67 before the pass - the machine's load varies: no measurable cost).
(g) **The far canopy by kind** (later the same day): the terrain patches carry a kind channel (UV4: how tropical, how seasonally dry - ForestTypes'
thresholds smoothed) and the shader's canopy takes the rain forest's deep greens and the dry tropics' olive (the near palettes in linear), the rain
forest a lumpier relief from afar; shots `wf6/before_after_far.png`; Web test 6/6 (`s16web5`, WebGPU, forest views 69-82 fps).
**Open (W-FOREST stays open for Arjun's judgement in real shots).** ground cover by kind of wood; stand age as state (only fire's is real; felling and windthrow leave none); the dry
tropics' ground drawn lush where moisture is 0.5; a straight snow/no-snow seam on one boreal patch; perhaps too many bare boles between the rain
forest's flat crowns.

## D2-131 — 2026-10-09 — Working the land harder: more ploughing and hoeing where the land within reach is short (C3.6a)

**Context.** C3.6 was left open between two answers to land binding (`26` §3): working the land harder (Boserup 1965) or household land
(who holds it). Measured first (session 16 third context; a tools-only tally `--landtally`: harvests and COARSE working days by whether the
village's arable within reach is short of its people's need, by crop stage; seeds 4 and 5, years 200-223 from the `_m2` saves): land bound
80 % and 62 % of harvests; in bound villages **labour never limited a crop stage** - sown 1.000 and brought in 1.000 - and a working day had
2.7-4.0 hours left after the crops in every stage (sowing 3.3, hoeing 4.0, reaping 2.7); the hungry spent 0.6-3.1 of them in the wild for
0.02-0.08 FU an hour, against ~1.7 FU an hour in the fields - nothing let them put an hour into their own hectares (the crop's work had a
fixed need: 40 h a ha to sow, 24 to hoe, 45 to reap, and work beyond it gave nothing). D2-129 had shown the hunger is a real shortage, not
its sharing: who holds the land changes who eats, not how much. So the missing margin was the intensive one.
**Decision.** `SettlementSystem` (sim version 66). (a) **At sowing a village whose fields are short of what its people want to sow** (its
heads x 0.5 ha, more where famine is remembered - the want it already claims fields for) **plans extra ploughing and hoeing a hectare**:
enough to bring the harvest of the hectares it has up to what the wanted ones would give, as far as falling returns allow (`TilthFor`;
`Settlement.Tilth`, a share of the ordinary season's 109 work-hours a hectare; none where its fields are enough - Boserup: people do not take
on more work an hour returns less for until they must). (b) **Falling returns**: what the extra work done adds to the yield is
`GainOf(e)` = 0.30 x (1 - exp(-e / 0.4)) - the first extra hour returns three quarters of an ordinary hour's harvest, an hour at the most a
village plans (`TilthMax` 0.8: +80 % of the hours, +26 % of the yield) a tenth of it; the harvest an hour falls with every hour (field
trials: a second and third weeding of a weedy cereal +29-42 % each for ever more hours; marled and manured land in medieval demesnes +20-55 %;
Brunt's off-farm manures +20 %). (c) **Only work done counts**: half the extra hours go to the sowing (a second ploughing, the harrowing),
half to the hoeing (two more hoeings); the crop's need in each stage rises by them; the ordinary work is done first, and at the harvest the
yield is multiplied by `1 + GainOf(min(planned, done))` (`TilthDone`, `TilthGain`); the ordinary weeding's effect on growth is as before. Both
resolutions draw the hours where they did: COARSE days put them before building and the wild (as the crops were); lived days choose the
fields while their need is unmet. (d) **History**: `FieldsWorkedHarder` (93, Local) once when a village begins - "the N ha of arable within
their reach no longer fed their M people at their old work (X % more hours a hectare)" - its cause the famine it remembers if any, else its
people; told again only after its land has covered its want with a tenth to spare; indexed for the settlement page's history. (e) Pages: the
settlement page's food section ("Its arable within reach (A ha) is short of the W ha its people would sow: they plough and hoe each hectare
harder this year, X % more hours (the last harvest Y % larger for it)"). Tools: `--notilth` (reproduces `_m2` exactly: seed 4's annals
byte-identical), `--landtally` (the survey lines and the extra work planned, done and what it added).
**Evidence.** `TilthTests` 4 (the plan follows the shortfall and stops at the most; returns and the harvest an hour fall with every hour; a
cramped village plans the extra work, records it, its page and history tell it, the ordinary sowing is done first, the extra is done and the
harvest gains what the work done gives - by about that much against the same world with `--notilth`; a village with land enough works as
before; a save in the hoeing continues identically through the harvest). **Ten seeds, 300 years DEEP**, `_t1` against `_t0` (the same binary
with `--notilth` = `_m2`): in land-bound villages the extra work planned was 0.41-0.63 of the season's hours and all of it was done; the yield
a hectare rose 106-125 -> 124-150 FU (+16-24 %); the harvest went from 0.83-0.97 to 0.91-1.02 of their people's bread; the work added 14-18.5 %
of the harvests; crop hours a working day rose 0.4-0.8 h in the sowing and hoeing stages (seed 4's wild hours 34 -> 16 million). Lived days do
it too (seed 5, a lived year from year 200: planned 0.508, done 0.508). **Then the people grew into it**: people over years 200-299 x1.17
(t 4.2), at year 300 x1.27 (t 10.9; every world x1.19-1.45), births x1.16, deaths x1.11; the land binds harder again (bound villages' arable
a head 0.68-0.82 -> 0.61-0.79 of the need) and **hunger a person-year is unchanged** (x0.98), famines a person-year x0.78 (t -2.3), raids
x0.37 (t -3.1), wounds a person-year x0.77, takings x0.90; relief by sea x2.0 (more coastal people), epidemics x1.7 (rare events, t 0.9).
**Cost**: none measurable - the ten runs took the same time (x0.998) with a fifth more people (fewer hungry villages, more calm DEEP steps).
**This is Boserup's and Malthus's world together**: the land is worked harder where it binds, at a falling return an hour, and the people
multiply until it binds again; the gain is density and towns, not welfare. It raises the worlds' scale (D2-085): a fifth to a quarter more
people at year 300 (seed 4 9,400 -> 11,189), the run's cost unchanged; the browser's (more people drawn in larger villages) is checked in the
Web test.
**Open.** The herds' dung (manure carried to the fields: a real flow, the medieval manure gap - bound villages keep 0.3-0.6 beasts a field
hectare, ~1.5-3 kg of fodder a day: a tenth of a manuring) - C3.6b; household land and marriage on a holding (the preventive check that would
turn the gain into welfare rather than numbers: the NW European pattern, D2-129 "Open"); fallow and the soil's fertility (fields are cropped
every year at no cost today: a generous base).

## D2-132 — 2026-10-09 — The first sheaves: a ripe crop's grain feeds the village as it is reaped (the hungry gap)

**Context.** After C3.6a hunger a person-year was unchanged (D2-131): 17 % of deaths. Measured where in the year they fall (`--gaptally`, new:
hunger deaths by days to the village's harvest, its crop's stage, age, its store, how long the dead had been hungry; ten worlds, years 200-299
from the `_t1` saves): **66 % fell in the last nine days before the harvest's day - the ripe stage, the grown crop standing or reaped in the
fields** (half within five days; by world 50-98 % in the ripe stage), with under a day of the village's food in its store in 61-97 % of them; the
dead were children (2-11: 35-71 %; under 2: 13-35 %), the ration at its floor (0.45). The model brought the whole harvest into the store on its
last day (day 39) while villages reap through the ripe stage: they starved beside their sheaves. The year's hungry gap ended with the harvest's
beginning - the first sheaves were threshed and eaten (Lammas, the loaf-mass) - not with its end.
**Decision.** `SettlementSystem` (sim version 67). (a) **The grown crop's yield is known when it ripens** (`Settlement.CropYield`: fields x
yield x growth x sown x the extra work's gain) and **the grain comes in as it is reaped** (`Sheaves`): at each day's start in the ripe stage the
reaped share of it, less what fire took standing, enters the store and the households' bins by their mouths (`Holdings.Harvest(sid, fu, false)`:
no debts settled); on the harvest's day the rest, the year's debts, the harvest's record, tribute and fibre on the whole (`Reaped`); sheaves
already in the store are not burnt with the stubble. A calm village living a step through the ripe stage takes its grain at the step's end as
before (it is not hungry). (b) **Rationed to the next new grain**: the village rations its store to its first sheaves (the day after the crop
ripens, `SheafDay` 31) rather than to the harvest's last day, and once the crop is ripe it counts the crop still standing and rations the whole
to next year's first sheaves (`ToBread`; the kin's spare and the marriage rule read the same horizon through `ToHarvest`). Both were needed:
the first build (sheaves alone, rationed to the harvest's last day) let villages eat the first sheaves at full meals and starve the next
summer - hunger x1.65, famines x1.34, raids x2.7 (stores filled in the ripe stage while the ration rule saw nine days to feed); rationing the
ripe stage alone (`_s2`) removed the raids (wounds x0.6) but left every other day of the year rationed a week short. (c) Pages: the settlement
page's food section during the reaping ("The harvest is coming in: N days' food reaped of the M its fields bear"). Tools: `--nosheaves` (the
whole harvest on its last day: reproduces `_t1` exactly - seed 4's annals byte-identical with the final binary), `--gaptally` (the survey
lines and the share of the grain in before the harvest's day).
**Evidence.** `SheavesTests` 5 (the sheaves come in as they are reaped, never more than the reaped share, the harvest all of it, the households'
bins take their shares; a sheaf's share settles no debt, the harvest's day does; a village with an empty store at ripening holds grain and eats
better within four days, against the same world with `--nosheaves`; sheaves in the store are not burnt with the stubble; a save in the reaping
continues identically). **Ten seeds, 300 years DEEP**, `_s3` (the final binary) against `_s0` (= `--nosheaves` = `_t1`): 6-23 % of each world's
grain came in before the harvest's day (the villages living day by day - the hungry); **the deaths beside the sheaves are gone** (within five days
of the harvest's day 49 % -> 1 % of hunger deaths; in the ripe stage 50-93 % -> 12-40 % by world) **and the hungry gap now falls where the
year's did, in the last days before the grain ripens** (the growing stage 59-77 % of them; 52 % fall 10-19 days before the harvest's day). **Hunger a person-year is not
lower**: by world it fell in four and rose in six (median 3.9 -> 4.7, mean 4.2 -> 5.1 a 1,000 person-years; t 1.45), people over years 100-199
x1.08 and 200-299 x1.05, births a person-year x1.013, deaths a person-year x1.034 (t 3.2), famines x1.26 (t 1.8; one a village-year: deaths
spread over more village-years). The children who starved beside the sheaves live, the villages grow by them, and the same year's food brings
the check back a fortnight earlier - the positive check of a Malthusian world, as with C3.6a: neither the timing of the gap nor the land's
yield lowers hunger a person-year; only a check on births before the food fails (marriage on a holding, the NW European pattern) or more food
than people would.
**Open.** The preventive check: household land and marriage on a holding (D2-129 "Open"); famine foods and the green harvest in the last
fortnight (no early crops - peas, beans - are sown).

## D2-133 — 2026-10-09 — Household land, first slice: each household holds hectares of its village's fields (C3.6b)

**Context.** D2-131 and D2-132 both ended in Malthus: more food a hectare or a better-timed harvest grew the villages, and hunger a person-year
came back to where it was - it is set by births against food. What holds births back in the medieval north-west is the household's own land:
no holding, no marriage (Hajnal 1965; Razi 1980; Ohlin's "niche"); TC2's land is the commune's, its harvest shared by mouths (D2-126). Measured
first (`place --heirs`, new; five worlds at year 300): men marry at a median 19-23, women 20-21; at marriage a man's father is already dead in
34-41 % of cases; a man's father dies when he is a median 22-24; a family raises 1.55-1.93 sons to 20 (a third one son, 18-29 % three or more);
households are small (2.7-3.2 people) and every village is land-bound (~1 ha of fields a household against ~1.5 ha of want). Holdings passed to
one heir would make heirs of men in their early twenties and leave about two in five landless where land binds.
**Decision.** `Holdings` (sim version 68), the first of three steps (as C3.3a: the accounting first, moving nothing). (a) **Each household holds
hectares of its village's fields** (`Household.Land`): the founders by their heads (`LandFound`); **a child marrying out takes a portion** - what
the family's land can spare beyond what its remaining people would sow, up to the child's own want; where the family's land is short, nothing
(`Portion`, in `MoveTo`); **the last to leave a household for a new one in the village takes what it held** (`Close`); **an heir takes the land
of a household that died out** (`Inherit`, D2-059's heir: a child's household, else a parent's, else a sibling's), none - the village's;
**those who set out leave theirs to the village**. (b) **At sowing**, after the village has claimed its fields (`LandYear`): land it lost
(released, gone to wood or water) is lost from every holding by the same share; unheld land - taken in for its people's want, left by those who
went or died without heirs - is given out to the households short of what they would sow, the least landed a head first. The custom is one heir
(impartible) and portions only of spare land: the north-western custom of a family's holding kept whole; partible customs could later come
with a village's tradition (C11). (c) Pages: the person page's home section ("It holds N ha of the village's fields, where its people would sow
M ha" / "It holds no land"), the settlement page ("Land: H households hold N ha of its fields (the most the Xs, M ha); L households hold none;
U ha unheld"). Tools: the `land:` line (households with and without land, ha a head and its Gini, against the want), `--landholds` (where land
went), `--noland`, `place --heirs`. Nothing else reads the holdings yet: the harvest is still shared by mouths.
**Evidence.** `LandTests` 6 (founders hold all the arable by their heads; a portion only of what the land can spare; the last out, the heir and
those who go; unheld land to the least landed first, land lost from all by the same share; holdings move nothing else; a save keeps them).
Ten seeds (`_h1`): **every world's annals byte-identical to `_s3`** (the bookkeeping moves nothing). At year 300 the fields are held very unevenly:
32-60 % of households hold no land, 15-35 % under half their want; land a head p50 0.00-0.17 ha (p90 0.5-1.0) against a want of ~0.5; Gini 0.74-0.92;
over 300 years the largest flow is inheritance (seed 4: heirs took 7,486 ha, portions 2,604 ha in 5,255 marriages, the last out 1,598 ha, unheld
land given out 8,362 ha to 6,815 households; those who went left 2,629 ha). The largest holders are the villages' creditors (seed 4's largest
town: the Nouskeirns hold 19 ha of 278 and are owed grain by 91 of 348 households).
**Next (C3.6c).** The harvest shared by land and labour - the land's half to those who hold it, the labour's half to the hands that worked
(the half-share of the métayer; the commune still works its fields together) - so that the landless live by their hands, poorer, and the
C3.5 rule (a couple marries when it can keep a household to the next grain) delays their marriages: the preventive check, from economics.

## D2-134 — 2026-10-09 — The harvest shared by land and labour: the landless live by their hands, and marriages wait (C3.6c)

**Context.** D2-133 gave each household a holding but nothing read it: the harvest was still the commune's, shared by mouths (D2-126). D2-131
and D2-132 had shown that in these worlds hunger a person-year is set by births against food; the check the medieval north-west had on births
was economic - a couple married when it could keep a household, and the landless could seldom do so young (Hajnal 1965, 1982; Razi 1980;
Kussmaul 1981). TC2 already has the rule (C3.5, D2-129: a couple marries when what it brings keeps it to the next new grain); what it lacked was
households that are poor because they hold no land.
**Decision.** `Holdings` (sim version 69). **A harvest - and each day's sheaves - is shared half by land and half by labour** (`ByLandAndHands`):
the land's half by the hectares each household holds of the village's arable, the unheld land's part the village's own grain; the labour's
half by the hands each household gives the fields (`Hands`: as the village's working day weighs its people - grown people a hand, youths of
12-15 six tenths, the old half, all by their health; the sick and the badly hurt abed none). The village still works its fields together, so
every hand is paid alike: the half-share of the metayer, the commonest medieval share between land and labour (`LandShare` 0.5). Nothing else
changes: the moral economy (common grain, kin's gifts, loans, hard times) and the marriage rule read the households' grain as before.
`--bymouths` keeps the old sharing (reproduces `_h1` exactly - seed 4's annals byte-identical). Tools: the `land:` line's "of the harvests the
landless took F of their mouths' share, those with their want G".
**Evidence.** `LandTests` 7 (each household's share is its land's part and its hands' part, the unheld land's part the village's; `--bymouths`
shares by mouths). Ten seeds, 300 years DEEP, `_c1` against `_h1` (the same holdings, the harvest by mouths): **hunger deaths a person-year
x0.74 (t -2.8)**, hunger deaths x0.74 (t -2.7), births a person-year x0.98 (t -1.8), deaths a person-year x0.96 (t -2.3), people over years
200-299 at par (x1.01), famines x0.98, wounds x0.75, walls x0.77, foundings x0.90. The landless took 0.50-0.52 of their mouths' share of the
harvests, the households holding their want 1.6-2.3 of theirs; and the landless married later or never: women's age at first marriage
20.6-22.0 -> 20.5-22.8, ever married by 45-59 94-100 % -> 67-100 % (seed 7 67 %, seeds 1 and 6 76-84 %). Fewer landless households were formed
(landless at year 300 32-60 % -> 13-46 %, the land's Gini 0.74-0.92 -> 0.62-0.88). **The same people, fewer of them born and fewer dying -
the preventive check where the positive check was** - and it came from the households' own economy, no rule about land and marriage written.
**Open.** Who leaves: a village's leavers are drawn by its pressure, not by their own land - the landless and the younger sons would go first
(C3.6d); service (the young of the landless working in landed households); a land market (letting, buying, engrossing); partible customs with
a village's tradition (C11); the land's half as lords' rents (C10).

## D2-135 — 2026-10-09 — Who leaves: each household weighs its own land (C3.6d)

**Context.** Since D2-045 a village's households weighed leaving by the village's pressure - its crowding (its arable against its people's want),
its cut rations, its famine memory - each with the same keyed chance (young households x1.4): a household holding all the land it would sow
left as readily as a landless one, its holding then given out to others at sowing. With holdings (D2-133, D2-134) that is untrue: holders kept
their land, the landless and the younger sons went (to found villages, or to join one with land to spare).
**Decision.** `Migration` (sim version 70): **each household's pressure reads its own land** (`Pressure(s, hid, unheldEach)`): the share of
its people's want that its holding - with its part of the village's unheld land, given out at sowing to those short of it - does not cover,
in place of the village's crowding; the village's cut rations and famine memory as before. A village whose land is enough but held by others
now sends its landless away; a household holding its want feels no crowding. The place they go (`BestSite`, the villages to join) is chosen as
before, after the choices. `--villageleave` keeps the old weighing (reproduces `_c1` exactly - seed 4's annals byte-identical). Tools: the
`land:` line counts the households that chose to leave by their land.
**Evidence.** `LandTests` 8 (the landless feel all the crowding, a household holding its want none, its part of the unheld land counts as its
own). Ten seeds, `_d1` against `_c1`: those who chose to go were landless or land-short (seed 4 over 300 years: 58,382 and 13,918 choices, 2,180 by
households holding their want) but in full lands most found nowhere to go; **every aggregate at par** (people x1.00, hunger x1.10 t 0.5, births
x0.99, foundings x1.14 t 0.7); with the landed no longer leaving their holdings to be shared out, **land concentrates**: landless households at
year 300 13-46 % -> 17-66 %, the land's Gini 0.62-0.88 -> 0.70-0.93 (seeds 4, 2, 8, 42: three in five households hold no land). The DEEP guard
(`DeepLivesTheWorldAsCoarseDoes`) failed once on seed 33's cattle (x2.81: COARSE and DEEP founded 7 and 9 villages): over twelve small worlds DEEP
against COARSE gives cattle x0.96, goats x0.92, pigs x1.23 with this rule (x0.90, x1.05, x1.05 without), single worlds x0.3-x3.7 either way -
the guard now reads the two worlds' herds together (same x2.5 band; D2-083's six-seed comparison remains the calibration).
**Open.** A full world gives the landless nowhere to go: service in landed households, a land market (letting, buying, engrossing - the
concentration has no counterweight), the land's half as rents (C10), towns that take the landless (C13).

## D2-136 — 2026-10-09 — The old hand on their holding for their keep (C3.6e)

**Context.** Over 900 years (D2-135's land economy, `s4_l9`, `s42_l9`) the households holding their want kept decades of grain (800-2,975 days'
food) while 54-77 % of households held no land. Measured (`place --landless`, extended): the land-rich were mostly the old - households whose
youngest member is 55 or more held 63-89 % of the land-rich households' land and most of their grain (seed 4 at year 600: 212 such households,
3,689 of 4,124 ha, 1.15 million FU), many with a child's household in the village. Nothing in their lives handed land on before death; a holding
passed only when its last member died or left. In medieval England the old surrendered their holdings to a child, or by contract to a younger
household, in return for their keep - board, a room, fuel (Homans 1941; E. Clark 1982; R. Smith 1984).
**Decision.** `SettlementSystem.Retire` (sim version 71), at the yearly weighing before households weigh leaving: **a household that holds land
and whose youngest member is past working it (`RetireAge` 60, the COARSE day's half-hand) goes to live with a child's household in the village**
(the one with the least land a head), **else, by an agreement for its keep, with the village's household short of land that has the most hands**
(at least a grown hand); none, it stays. Its people move in one by one, the eldest last (`MoveTo`: their mouths' share of its grain, its larder,
goods, beasts; the last out takes the land - C3.6b's `Close`), its home stands empty. History: `Retired` (94, Personal): "The Xs of P, too old to
work their land, went to live with their child's household, the Ys, who took their N ha and keep them". `--noretire` keeps the old as before
(reproduces `_d1` exactly - seed 4's annals byte-identical). Tools: the `land:` line's retirements (to a child's household, ha, FU, nobody to go to).
**Evidence.** `LandTests` 10 (an old household goes to its child's household with its land and grain, recorded; with no child, to the land-short
household with the most hands; `--noretire`: they keep it). Ten seeds, `_e1` against `_d1`: 947-3,584 retirements a world over 300 years; **the
land spreads and the hoards go**: landless households at year 300 17-66 % -> 6-44 %, the land's Gini 0.70-0.93 -> 0.50-0.65; the land-rich now
ordinary households (seeds 4, 42: 2.5-2.9 people, 1.6x their want, 168-224 days of grain - before 1.5-1.8 people, 2.6-5.7x, 305-1,594 days).
The heirs' children marry sooner: births a person-year x1.06 (t 4.7), people at year 300 x1.09 (t 1.5), hunger a person-year at par (x1.05, t 0.2);
tributes x1.6 (t 3.7), raids x1.9 (t 1.7) - more people in villages that bound sooner.
**Open.** The old's home left empty (the young might move into the larger house); retirement within the household (stem families) rather
than moving; the elderly landless (no holding to hand on) living on their kin as before.

## D2-137 — 2026-10-09 — Two villages on one spot: a founding party joins the village standing at its place; a way of no length arrives where it goes (the Web catch-up's out-of-memory)

**Context.** The Web build of C3.6e (5dd947a) failed the Web test's "a day away" catch-up, deterministically: year 35 of 96, then the browser
aborted out of memory. Headless runs were healthy, and the browser's world (17 places at year 35) looked unlike a headless `run --seed 42
--coarse` (9-10), which was read as the browser diverging. **It does not**: a Living catch-up of a small world is not COARSE at all
(`PlanCatchUp` goes COARSE above 40,000 person-years; seed 42's 96 years x ~145 people are ~14,000), so the browser lives the century in
ACTIVITY, and a headless `run --seed 42 --years 96` with the same binary is the same world - 17 places at year 28, then out of memory under a 512 MB
heap cap (`--hot`, new, names who began the most activities between reports). The browser's own saves, read out of its IndexedDB store
(`webtest.mjs --export`, new: the save before the day away and every half minute of the catch-up), replayed headless, gave the cause: four
people of Skolmere visiting kin in Ustwick took ~17,000 trips a day between the two. **Both villages had been founded in year 27 on the same
cell** - a party arriving at its place founds there "all the same" even when another party's village was founded on it meanwhile - so their
anchors coincide and the way between them has no length; a traveller's arrival was read from the arc length (`S1 <= 0.5 ? A : B`), so on a way
of no length a trip home ended where it began, one second later, again and again. Each trip's leg went into the travel index, which drops stale
entries only when a cell is queried (by a fire near it): 120,000-186,000 entries a day, never freed - the out-of-memory. C3.6e only changed the
trajectory that met it: the fault is D2-045's (parties), the loop the arrival rule's. `place --near` (new) over ten seeds' saves at years 100 and
300: villages closer than the spacing occur (most from world generation), on one cell never.
**Decision.** (sim version 72) (a) **A founding party that finds a village standing at its place - within `Sites.Spacing` (1,900 m), where no
village is founded - makes its home in the nearest one** (`Migration.Arrive`, `VillageNear`), as a party joining a village does; recorded
`PartyJoined` (95, Local: "N households of X (M people), come to make a new home, found Y standing where they meant to settle and made their home
among its people", caused by its setting out). (b) **A traveller arrives where they were going**: on a way of a metre or less the arc length
tells nothing, the trip's destination does (`AgentSystem.OnTravelEnd`, `VoyageLanded`). (c) **The travel index sweeps its stale entries before
it grows** (`TravelIndex.Full`/`Sweep`, from `RegisterLeg`): the entries a query would drop, so queries read the same in the same order;
derived state. `--nojoin` founds as before.
**Evidence.** Tests (MigrationTests): `APartyFindingAVillageAtItsPlaceMakesItsHomeThere` (no founding; its households in the village standing
there; `PartyJoined` once, caused by the setting out), `ATravellerOnAWayOfNoLengthArrivesWhereTheyWereGoing` (the bug's premise: a village founded
on its own village's cell, someone walks across - fails with the old rule: they never get home), `TheTravelIndexDropsStaleLegsBeforeItGrows`
(the same queries, room again; a pool of live legs doubles at once). The browser's year-34 save replayed with the fix: ~1,000-1,500 events a half
day (54,000 before), the travel index in the hundreds. Headless ACTIVITY `run --seed 42 --years 96` (the browser's catch-up): 96 years in 43 s,
488 people in 16 places. **Ten seeds DEEP 300 years (`_j1`): every world's annals byte-identical to `_e1`** - in 3,000 village-years no party
arrived at a taken place, so (a) and (b) change nothing else, and (c) nothing at all. Full suite 358/358 (C3.6e's first full run too).
**Open.** Parties of different villages weigh leaving on the same day and may choose one place; the second now joins the first's village -
whether scouts' news of a founding should reach a party before it sets out (lore of places founded) is left to the knowledge of places (C11).
