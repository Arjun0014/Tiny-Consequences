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
