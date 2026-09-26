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
