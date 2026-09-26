# Decisions log

Short records of decisions that change the design, scope, budgets or architecture. Newest at the bottom. Format:

```
## D-NNN · YYYY-MM-DD · Title
Context: why a decision was needed.
Decision: what we chose.
Consequences: what changes / what to watch.
```

Claude Code decides autonomously and records here instead of stopping to ask, except where CLAUDE.md says to stop.

---

## D-001 · 2026-09-27 · Scope and pillars fixed before building
Context: Design discussion between Arjun and Claude.
Decision: Living planet sim on an icosphere graph, statistical fields per cell, individual humans only, fixed weekly tick (48/year), blame-tag causality with chain popups, resilient baseline, 1 real hour = 1 world year offline, deep time via epoch mode, plates don't move.
Consequences: See docs 01–08.

## D-002 · 2026-09-27 · Tiny Skies is reference only
Context: The Tiny Skies repository has no licence.
Decision: No code or assets are copied from it. Visual mood only.
Consequences: All models are generated procedurally or come from clearly licensed CC0/OFL sources.

## D-003 · 2026-09-27 · Sim is pure Lua 5.1, tested headless with LuaJIT
Context: HTML5 runs Lua 5.1.4; Claude Code needs a fast loop that doesn't require the Defold editor.
Decision: `sim/` has no Defold dependencies, uses only Lua 5.1 features, runs under LuaJIT for tests, with an in-browser self-test for HTML5 determinism.
Consequences: View/platform code lives only in `game/`.

## D-004 · 2026-09-27 · SIM_LEVEL 3 (642 cells), provisional
Context: HTML5 release build benchmark (`npm run bench:web -- --release`, headless Chromium, dev machine) with weather + hydrology + vegetation + fire + biome implemented: level 4 = 18.9 ms/tick median, level 3 = 4.6 ms. The HTML5 runtime is interpreted Lua 5.1 in wasm (~25 ns/bytecode), so level 4 can't reach the 4 ms budget at weekly ticks even after optimisation (it would need ~60 bytecodes per cell per tick for *all* systems).
Decision: SIM_LEVEL = 3. Keep code lean and use multi-rate updates for slow systems so the full Chapter-1 tick fits ~4 ms at level 3. Re-benchmark after Stage 1.7; if a finer grid fits, an intermediate geodesic frequency (f=12, 1,442 cells) is possible via grid.lua. The coarse sim is hidden by the level 5–6 render mesh (interpolated elevation + noise for organic coastlines).
Consequences: headless runs are ~4× faster (200 y ≈ 6–10 s), which helps tuning. Spatial rates are scaled by cell spacing (D-005). Villages farm within 1 ring at level 3.

## D-005 · 2026-09-27 · Spatial rates scale with cell spacing
Context: 02_WORLD_MODEL.md §2 requires spatial rates per unit distance. Switching level 4 → 3 without scaling removed all deserts (moisture travelled twice as far per tick).
Decision: `W.sscale = spacing / spacing_L4`. Config values stay tuned per level-4 cell; systems divide advection Courant numbers and fire spread by sscale, diffusion by sscale², recolonisation by sscale, and multiply per-cell lightning by sscale² (per unit area). Cell-count thresholds (wildfire ≥ 10 cells) are converted to level-4 area equivalents (≥ 3 cells at level 3).
Consequences: tuning bands hold across levels (verified at level 4 and level 3 on 4 seeds × 200 y).

## D-006 · 2026-09-27 · Per-cell state outside the field budget
Context: floods need a "bankfull" reference; neighbour reads of grass/forest need last tick's values.
Decision: hydrology keeps `S.hydro.flow_mean` (EMA of flow, persistent system state, not a causal field). grass and forest get prev-tick buffers (spatial=true); that isn't a new field. Transient per-tick scratch arrays (evaporation, melt, runoff) live in `W.scratch` and are never saved.
Consequences: the field catalogue stays at the 24 fields of §3.

## D-007 · 2026-09-27 · Blame implementation details
Context: 03_CAUSALITY.md §3 blame per term per cell is too expensive in interpreted Lua if every cell pays.
Decision: (1) fast path: a system calls `blame.step` only when the cell's tag or any term's tag is non-zero, so quiet cells pay nothing; (2) each system blames its own terms when it writes a field (vegetation growth, fauna grazing and fire burning are separate steps on `grass`); (3) formula fields (`temp`, `precip`, `flow`, `silt`) take the tag of their dominant *anomaly driver* (heat_anom vs g_temp vs aerosol; moisture; the upstream contributor) instead of delta terms; (4) global-driven terms use the ledger's top root only if it explains ≥ 40% of the anomaly.
Consequences: cheap, deterministic, and chains still travel through every coupling. P08 (Chapter 2) measures coverage.

## D-008 · 2026-09-27 · Global climate self-calibrates during spin-up
Context: the no-human world must sit at CO₂ ≈ 280 regardless of seed.
Decision: during spin-up CO₂/g_temp are pinned while the world measures its own reference weathering index, ice fraction, ice volume and mean SST (years 10–30). Afterwards weathering = outgassing × (index / reference), so every seed balances at 280 with a working thermostat. Net vegetation growth absorbs CO₂ and burning emits it with the same coefficient, so a stationary biosphere is carbon-neutral.
Consequences: baseline drift comes only from real imbalances (fire regime settling, villagers).

## D-009 · 2026-09-27 · Art direction and batched props
Context: Arjun shared the target look (Tiny Skies screenshot), recorded in docs/ART_DIRECTION.md.
Decision: props (thousands of tiny trees, houses, crops) are baked into ~40–80 chunk meshes with runtime buffers, rebuilt only when a chunk's sim values change; moving things are pooled. This replaces "props as pooled game objects" from 05_ARCHITECTURE.md §7 for static props.
Consequences: 5k–15k trees at a few dozen draw calls; Stage 1.8 builds the chunk mesh pipeline.

## D-010 · 2026-09-27 · Worldgen shapes continents by growth, plates by Voronoi
Context: 2–3 continents ≥ 12% each and 4–8 islands must hold on 20/20 seeds.
Decision: continents grow from separated centres with a noisy priority flood that never touches another landmass. Islands are grown far from land. Plates (warped Voronoi + Euler-pole motion) only shape mountains, trenches, ridges and volcano sites. Attempts are keyed by (seed, attempt).
Consequences: constraints hold by construction (20/20 seeds pass on the first attempt at level 4 and level 3).

## D-011 · 2026-09-27 · Named regions: landmasses and 6–10 seas
Context: events need a place name (03 §2 `region`), and detectors need regional aggregates (boom/crash, drought).
Decision: `sim/worldgen/regions.lua` (worldgen only): one land region per landmass; a continent holding > 40% of all land is split in two (far-apart seeds, graph-distance Voronoi) and named "North X"/"South X" (or East/West). Seas: 6–10 (≈ ocean share × 12) from a seeded centre plus jittered farthest-point centres, assigned by multi-source BFS through the ocean (connected seas); the largest is kind "ocean". `W.static.region[c]` (persistent) + `W.names.regions[id] = {name, kind, continent, cells, centre, part}` + `W.names.continents`; names from `content/names.lua` with rng stream NAMES. `events.emit` reads `W.static.region`. Regions are not rebuilt when the coastline changes (Chapter 3 must do that at a coastline rebuild).
Consequences: at level 3 a sea is ~30–95 cells — too big for "fish collapse in the sea" (see D-014).

## D-012 · 2026-09-27 · Ocean & fauna are multi-rate; per-cell normals live in system state
Context: the HTML5 tick is already over budget (D-004); ocean + fauna + detectors had to stay lean.
Decision: ocean life runs each ocean cell every 8 ticks (dt = 8, staggered: 1/8 of the cells per tick); fauna local dynamics every 4 ticks (dt = 4, staggered); migration per species every 8 ticks with the rate × 8 (grazers and predators on alternate ticks); detectors monthly (floods every tick from hydrology's list). Oil is a sparse active set (ocean + beached shore cells), every tick. System state outside the field budget: `S.ocean.fish_ref` and `S.fauna.hnorm/pnorm` (per-cell 5-year EMAs = "normal", used by release blame and fish-collapse detection), `S.fauna.cull/hunt` (+ tags; village hooks for Stage 1.7), `S.ocean.oil` (active list, saved so its order is reproducible), detector memory in `W.D`. `fish` gets a prev buffer (spatial) for diffusion — no new field. Predator densities are in units 1/3 of grazer units (a×3, t_h/3, e/3), so wolves read as rarer than deer; dynamics are unchanged by the scaling.
Consequences: cost (LuaJIT -joff, isolated): ocean 0.044 + fauna 0.059 + detectors 0.011 = 0.114 ms/tick vs 1.31 ms for the rest (+9%, ≈ +0.4 ms in the HTML5 build). Stage 1.7 must re-benchmark.

## D-013 · 2026-09-27 · Release blame and tag hygiene
Context: with plain delta blame, grazers booming because predators vanished are attributed to baseline (the growth term is tag 0; the missing predation term is 0). Also tags lingered and got reused by unrelated changes.
Decision: (1) release: when a pressure is far below its local normal (predators < 0.5 × their 5-year normal, normal ≥ a minimum) and the pressure carries a tag, the growth it no longer holds back carries that tag; likewise predators starving because prey is < 0.5 × normal carry the prey's tag. (2) Pressure terms (grazing, browsing, raids, predation) carry the animals' tag only while the animals are > 1.25 × their normal. (3) Flows (migration, fish diffusion) pass their tag only when the net inflow is ≥ 5% of the population in that step (equilibrium exchange carries no cause). (4) An upstream cause never overwrites a more-derived event it produced (`blame.descends` on the flat parent array). (5) Natural (parent 0) species events re-tag only the cells that individually crossed. (6) grazers/predators tag hold 48 steps.
Consequences: S02 reads test_remove_predators → population_crash → extinction_regional → population_boom → biome_shift on 10/10 seeds; natural crashes are clean nature roots.

## D-014 · 2026-09-27 · Detector definitions for Stage 1.6
Context: 03 §4 thresholds need concrete measures at level 3.
Decision: population_boom/crash on land regions: area-weighted mean density vs a 5-year EMA of monthly samples (×2 / ÷2; exit ×1.4 / ÷0.75; 2-year cooldown; skipped below a minimum density). extinction_regional per land region (density < 1e-4 after ≥ 1e-3); extinction_global once when a species' planet total is 0. fish_collapse on connected fishing grounds: ocean cells whose fish fell below 50% of their own 5-year normal (exit 75%), cluster ≥ ceil(6/sscale²) (min 2) cells, named by sea region (a whole sea is far too big at level 3); a cluster whose dominant tag is already a fish_collapse only extends that tag. oil_spreading: connected slick (oil ≥ 0.01) ≥ ceil(10/sscale²) (min 3) cells, once per tag. oil_beached: oil ashore (≥ 0.005), once per tag, aggregated per region. drought: a region whose normal soil water is ≥ 0.2 drops below 0.2 and below 75% of normal for 3 months (deserts are not "in drought"). flood: hydrology's flood cells, per-cell 24-tick cooldown. overfishing is selected in Stage 1.7 (needs village catch). Averages are sampled during spin-up; events only when recording.
Consequences: natural population_crash headlines ≈ 0.05–0.1 per year per seed (predator–prey cycles), no spam.

## D-015 · 2026-09-27 · Ocean currents are mass-conserving push weights
Context: 02 §2 describes currents as upstream weights like the wind tables; pull-form advection doesn't conserve mass, and oil mass must be conserved (it only decays and beaches).
Decision: per season, the wind over each ocean cell, with components pointing into land neighbours removed and renormalised (coastal currents → gyres), becomes downstream push weights over ocean neighbours (plus diffusion); self weight = 1 − Σ out ≥ beach fraction. Current vectors are stored per season in `W.currents[s].vx/vy/vz` for the view.
Consequences: oil advection is exactly conservative (unit test); a pull table for other tracers can be derived later if needed.

## D-016 · 2026-09-27 · Derived tables are pinned to the sea level they were built for
Context: a real-world save round trip was not bit-identical (pre-existing; only the toy world was tested): lapse rates, upwind elevation and coast masks were rebuilt on load from the current `G.sea_level`, which drifts slightly every year, while the live world kept its worldgen-time tables.
Decision: persistent `G.sea_built` (0 at worldgen); geo/temperature/moisture rebuilds use it. A coastline rebuild (Chapter 3) sets `G.sea_built = G.sea_level` then calls `world.rebuild_derived`.
Consequences: real-world P02 (save mid-year with oil and culled predators → reload → continue) is bit-identical (unit test test_save_world).

## D-017 · 2026-09-27 · Browsing follows hunger; animals return eaten carbon
Context: removing predators must stop forest recovery (02 §5.9, S02 forest ≤ 0.85× in 8 years) without grazing wrecking the baseline bands; grazing silently removed biomass that vegetation regrowth then absorbed (CO₂ 280 → 240, −0.7 °C over 200 y).
Decision: browsing = b × H × (0.05 + hunger) × f/(f + 0.08), hunger = min(1, H/K_eff): per unit of forest strongest when forest is low, and hungry grazers (near their food limit, i.e. without predators) strip saplings. Eaten biomass (and oil-killed shore grass) is returned as CO₂ with the vegetation coefficient (D-008 carbon neutrality). Small islands start with no animals (they can't swim); predators are seeded on continents only.
Consequences: baseline bands hold (3 seeds × 200 y: forest 0.25–0.42, grassland 0.22–0.33 means); CO₂ drift back to +0.1 °C/200 y.

## D-018 · 2026-09-27 · Jungle soil-water exit threshold
Context: the jungle condition (soil ≥ 0.5) had no exit threshold (02 §0.3), so jungle↔forest flipped every season: ~270 biome_shift events per century on seed 42, forming long flip-flop "chains".
Decision: `BIOME.jungle_soil_exit = 0.35`.
Consequences: jungle↔forest shifts ≈ 8 per century; forest cover (forest + jungle) unchanged.

## D-019 · 2026-09-27 · Performance pass for the full Chapter-1 tick
Context: before villages the HTML5 tick was ≈ 5 ms against the 4 ms budget; wall-clock benchmarks on the dev machine swing 3× while bob builds run in parallel.
Decision: (1) vegetation runs each land cell every 2 ticks (dt = 2; a cell reads its own values from the live field and its neighbours' from the prev buffer, which it publishes after its update); (2) biome classifies a rotating quarter of the cells per tick (dwell counted in ticks); (3) moisture swaps its buffers instead of copying, precomputes the static capacity factor per season, and skips inflow-tag tracking when no moisture tag existed last tick (count kept in `W.S.moist_tags`, so a reloaded world takes the same path); (4) hydrology visits land cells + river mouths only; (5) ice skips cells that cannot change. Cost is measured deterministically with `tools/opcount.lua` (VM instructions per system; browser ≈ 15 ns per instruction, calibrated on the 4.6 ms D-004 measurement).
Consequences: 343k → 225k instructions/tick with villages and ships included (≈ 5.1 → 3.4 ms in the browser). Bands and scenarios unchanged within noise.

## D-020 · 2026-09-27 · Village economy and demography
Context: 02 §5.10 gives the shape; the numbers must give a population of 120–400 with occasional famines, not a famine every year.
Decision: food in person-months (adult 1/month, child 0.6). Farms ≈ farmers × 0.45 level-4 cells (1 ring at level 3), crops planted in spring, grown monthly (temperature bell 8–32 °C, soil water, fertility; drought, frost and beached-oil losses, fire from the fire system, raids from fauna), harvested once from tick-of-year 32 (≤ farmers × 30 person-months). Hungry villages clear extra fields as far as labour allows; exhausted fields (fertility < 0.22) return to the wild when a fresher ring cell is free (shifting cultivation); standing crops count as ground cover against erosion. A granary holds 6 months of needs; the excess spoils at 8% a month. Births are regulated by the depth of the hungry gap (`fs_low`, a minimum of food security that recovers over a year: 0 below 0.5 months, full above 2.5) plus a capacity backstop (1.6 × the land/sea estimate). Famine starts below 1 month of food and ends only when the hungry gap has closed (`fs_low` > 2); migration counts months with `fs_low` < 1. Fishing: catch = min(Σ fish × q, fishers × 2.5/month), removed from the fish (overfishing emerges). Hunting and predator culls go through the fauna hooks. Wood burning + clearing are the villagers' CO₂ source (ledger "villagers"; tuned for ≈ +1.2…+1.5 °C by year 200).
Consequences: S01 quick 3/3; full profile 5/10 (some worlds' populations dip under 120 or overshoot 400: the capacity estimate is the weak point, see PROGRESS).

## D-021 · 2026-09-27 · Entity schema and rng keys for people
Context: the renderer and inspector consume villages, people and ships; rolls must be local (05 §5).
Decision: `W.ent.villages` (array, ids = indices, abandoned villages stay with alive = false, `ruin` while ruins stand), `W.ent.people` (id → person; the dead kept 30 years), `W.ent.ships` (tankers, stable ids), `W.ent.boats` (rebuilt monthly), `W.ent.volcanoes`, `W.static.lanes`, `W.S.ruins` (cell → tick). Schemas are documented in the headers of villages.lua, people.lua and ships.lua. A person's rng key = hash(village founding cell, birth tick, index among that tick's births); monthly rolls are frac(village-month base + u × φᵏ) with u = the person's fixed random fraction (a Weyl offset: one hash per village-month instead of one per person per roll).
Consequences: an extra birth anywhere changes nobody else's fate; the village update costs ≈ 8k instructions/tick.

## D-022 · 2026-09-27 · Worldgen settle inside the spin-up
Context: 02 §2 requires villages, lanes and volcano sites, and a lived-in Year 0.
Decision: `sim/worldgen/settle.lua` runs at spin-up year 15 (15 years before Year 0): cultures per landmass, 3–5 villages (fishing, inland, second continent first, then the best free sites ≥ 3 level-4 steps apart), 150–250 people as households, tanker lanes as closed ocean loops A (next to a coastal village) → far B → far C → A with 1–2 tankers each, 5–10 arc volcano sites on convergent boundaries + 1–3 hotspots (≥ 1 in the ocean). P10 checks the settle report on 20/20 seeds (villages may later die or move — that is turnover, not a worldgen failure).
Consequences: spin-up costs ≈ +0.1 s; the settle report lives in `W.worldgen_report.settle` (not saved).

## D-023 · 2026-09-27 · Level-independent wildfire magnitude; small biome threshold nudges
Context: at level 3 wildfires (≥ 3 cells) never reached the 10-cell headline minimum, so P09 ran at ≈ 0.1 headlines/year; some worlds sat on knife edges of the desert/forest bands.
Decision: wildfire magnitude is reported in level-4-equivalent cells (cells × sscale²). Biome thresholds: desert entry at soil and veg < 0.17 (was 0.15), forest entry/exit 0.45/0.36 (was 0.5/0.4). Fire spread 0.46 (was 0.42), lightning 1.7e-3 (was 1.5e-3), prey refuge 0.02.
Consequences: headline rate 0.23–0.43/year on the S01 seeds; bands hold on the quick seeds.

## D-024 · 2026-09-27 · Test profile changes for Stage 1.7
Context: 07 §4 asks for P06 over 500 years; the quick profile must stay ≈ 3 minutes.
Decision: P06 quick = 1 seed × 200 years (shared with S01), full = 2 seeds × 500 years. S01 is a baseline-only scenario sharing the cached baseline runs (tests/sim/support/baseline.lua). S04 targets `ocean_near:village:fishing` and adds "village food security ≤ 0.7× baseline within 2 years". The S03 chain check only counts events within one year of the fire.
Consequences: quick `npm run test:sim` ≈ 40 s.

## D-025 · 2026-09-27 · Population regulated by each village's real food history
Context: D-020 regulated births with a crude land/sea capacity estimate: on the full profile populations either ran into the 500 cap or slowly died out on poorer worlds (S01 5/10).
Decision: each village keeps ~3-year EMAs of what it actually produces (harvest + catch + hunting) and eats; their ratio ρ is its food margin and `capacity = pop × ρ / comfort`. Births scale with ρ (10% floor below 1.05, full at 1.35) and with the depth of the hungry gap; a chronic deficit (ρ < 0.95 for 3 months) sends a family away (to a village with food, else founding on the best free land). Supporting changes: villages are sized at settle by their site's capacity; the settle happens at spin-up year 8; emigrating families are a village-month roll (not always the founders); granary 9 months; land yield 66 (was 80) and fish 340 person-months per unit (was 400); farm fertility depletion 0.003 (soils equilibrate instead of drifting down for a century); fishing takes at most 5% of the stock a month (fish_q 20: near the stock's best sustainable yield — at 12.5% catches collapsed over a century); hunger hurts more once stores are gone (health loss 0.4, frailty 0.006). The planet's hard cap of 500 is approached softly: births taper over the last 55% below it.
Evidence (10 seeds × 200 y, full profile): population stays within 120–400 after year 5 on 9/10 seeds (seed 7 dips to 119 in a churn of famines, migrations and refoundings); no runaway, no slow death. S01 7/10, S02 10/10, S03 9/10, S04 9/10.

## D-026 · 2026-09-27 · Compact saves
Context: a year-40 save was 354 KB (people 185 KB as keyed tables, system arrays and events at full precision).
Decision: SAVE_VERSION 2. People and event records travel as positional records in the save (the in-memory schema is unchanged); the per-cell system arrays (animal and fish normals, flow EMA) are snapped to 16 bits with the fields at the year boundary and saved as quantised arrays; people's continuous state (health, hunger, mood, memory salience) is rounded at the same snap (systems may define `snap(W)`, run after the field snap and before the capture); a person's random fraction is quantised at birth; the dead are kept 10 years (was 30). Grazers/predators lost their unused prev buffers.
Consequences: 187 KB at year 40 (seed 2), P02/P03 bit-identical.

## D-027 · 2026-09-27 · Pre-spun default world: seed 3
Context: generating + spinning up a world costs ~11 s in the browser; 02 §2 asks for a pre-spun default world.
Decision: `tools/make_world.lua --seed 3 --out assets/worlds/default.tcsave` saves the Year-0 world (the same year-boundary snap the first tick performs, then the capture); the game loads it with `require("sim.worlds").from_save_string(str)` (≈ 15× faster than generating; 158 KB). Seed 3 over 42: at Year 0 it has 5 villages (4 fishing) with both tanker lanes passing fishing villages, 36% forest, 12% desert, three continents; it keeps 4 villages at year 100 (seed 42: 4 villages, 27% forest, but only 2 villages left by year 100); seed 3 is also a quick-profile test seed with known S01–S04 behaviour. A unit test checks the file against fresh generation after 5 years, so a stale file fails loudly (regenerate after behaviour-changing sim edits).

## D-028 · 2026-09-27 · Vegetation bands on natural land; local population booms; threshold evidence
Context: villages, farms and ruins occupy 5–9% of the land at level 3 and count as neither forest nor grassland, pushing dry worlds under the 20% floors; population_boom only looked at whole-region averages, so hunting near villages could hide a real boom on part of a landmass (S02 seed 3).
Decision: S01 measures the §10 vegetation bands (forest, grassland, desert) as shares of natural land (land minus villages, farms, ruins; metrics forest_nat/grass_nat/desert_nat; human_land reported). population_boom also fires for a connected patch (≥ 10 level-4 cells) where a species exceeds 2.2× its own 5-year normal (sharing the regional cooldown). Classification: forest entry/exit 0.42/0.34 (D-023 had 0.45/0.36); lightning 1.9e-3; prey refuge 0.018 (headline rate ≥ 0.2/year on 10/10 seeds).
Evidence: natural-land forest on the 10 seeds 0.20–0.40, grassland 0.21–0.42, desert 0.06–0.24; seed 2 (17.5% desert) sits at forest 0.199 — geography, left failing.

## D-029 · 2026-09-27 · P07 measures VM instructions, not wall time
Context: 07 §4 P07 needs a regression gate on per-system tick cost. Wall-clock timings on the dev machine swing 3× with concurrent load (bob builds, browsers), which makes a 30% gate either flaky or blind.
Decision: P07 counts Lua VM instructions per tick per system (debug count hook, LuaJIT interpreter with the JIT switched off and flushed while measuring) on a fixed world (seed 42, level 3, one year after spin-up, 192 ticks), via `tools/perf.lua`. The committed `tests/bench-baseline.json` holds the per-system counts, date and calibration; the test fails when a system grows > 30% (and by > 2,000 instructions, so tiny systems don't trip on a few lines) or the whole tick > 15%. Intentional updates: `npm run bench -- --update-baseline` (plus a DECISIONS entry); `npm run bench -- --check` runs the gate alone. Calibration against the real target: the HTML5 release build ran the `sim/bench.lua` configuration at 1.70 ms median for 230,910 instructions/tick, i.e. ≈ 7.4 ns per instruction (6–10 ns per system). This supersedes the ≈ 15 ns estimate in D-019, which came from the older 4.6 ms (D-004) measurement.
Consequences: exact and repeatable (0.6 s); it counts LuaJIT bytecodes, not PUC Lua 5.1's, and C calls count as one instruction, so it tracks cost changes in Lua code, not constant-factor differences between runtimes — re-check the calibration with `npm run bench:web -- --release` at chapter ends.

## D-030 · 2026-09-27 · SIM_LEVEL 3 finalised
Context: 02 §2 asks to finalise SIM_LEVEL from a browser benchmark of the realistic tick (after 1.5). With all Chapter-1 systems (weather, hydrology, vegetation, fire, biome, ocean, fauna, villages, ships, detectors) after the 1.7 performance pass, `npm run bench:web -- --release` measures **1.70 ms median, 2.5 ms p95** per tick at level 3 in the HTML5 release build (headless Chromium). Level 4 would be ≈ 4× (≈ 6.8 ms), over the 4 ms budget.
Decision: SIM_LEVEL = 3 is final for the jam. The headroom (~2×) is reserved for Chapter 2–3 systems (powers, geology, epoch mode) and lets ⏩ run ~4 ticks per frame.
Consequences: P07 (D-029) guards the budget.

## D-031 · 2026-09-27 · Terrain and sea are one mesh coloured from a per-cell texture
Context: 05 §7 describes separate terrain and ocean meshes with per-vertex colours rewritten incrementally.
Decision: one level-5 render icosphere (61k vertices) carries land and sea. Each render vertex interpolates elevation from its 3 nearest sim cells, and land/sea comes from an interpolated coast value plus noise, so coasts are organic despite the 642-cell sim. Colours come from a small per-cell texture (2 px per sim cell) sampled in the vertex shader. A recolour is one pass over 642 cells (≤ 4×/s, amortised) plus one texture upload, not a rewrite of 61k vertices.
Consequences: cheap updates; sea-level changes (Chapter 3) rebuild the mesh in one step, which is acceptable because it's rare.

## D-032 · 2026-09-27 · Props: procedural Lua meshes in 80 chunks; budget raised
Context: D-009 chose batched chunk meshes. 06 §1 suggested a Node→glTF generator and 05 §9 budgeted ≤ 2,500 props.
Decision: prop geometry (tree species, bushes, tufts, rocks, stumps, fields, houses, huts, ruins) is generated procedurally in Lua at startup and baked into 80 chunk meshes. No glTF pipeline: the chunks need raw triangles anyway. Budget: ~12k props baked, ≤ ~9.5k visible, 37–50 real draw calls at the planet view. Near chunks switch to richer templates when the camera zooms in.
Consequences: the dense "miniature forest" look (ART_DIRECTION.md) within the draw-call budget.

## D-033 · 2026-09-27 · Movers, fire and batching
Decision: moving things (clouds, deer, wolves, people, boats, tankers, fire/smoke pieces) are pooled game objects in world-space-vertex materials, so the engine batches them into one draw call per material. They draw in their own unsorted render predicate, because depth sorting between chunks broke the batches. Fire and smoke use pooled unlit meshes, because Defold particles don't face the camera for this use.
Consequences: ≤ 300 movers at a handful of draw calls.

## D-034 · 2026-09-27 · UI built in code, fonts, HTML template
Decision: the HUD and inspector are built in code on a Defold GUI scene and batched by layers (119 → 47 draw calls). They use Fredoka and Nunito static instances cut from the Google Fonts variable fonts (OFL, `assets/fonts`; Fredoka has no "₂" glyph, so CO₂ uses Nunito), and a custom HTML template so the canvas fills the window with no footer strip. HUD animal numbers are density sums shown with fixed display scales (presentation only).

## D-035 · 2026-09-27 · Camera framing
Decision: a trackball orbit with no pole lock. The default zoom is closer than "whole planet with margin": the planet fills ~85% of the frame height, like the reference image. Zooming out still reaches the whole planet. The camera idles in a slow auto-rotate.
