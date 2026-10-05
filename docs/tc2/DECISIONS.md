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
