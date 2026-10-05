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
