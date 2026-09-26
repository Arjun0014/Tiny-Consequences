# 08 — Roadmap: chapters and stages

- A **chapter** ends with a human playtest. Claude Code works through all stages of a chapter autonomously, then writes the playtest report and stops.
- A **stage** is a unit of work with a clear "done when". Finish it (tests green, committed, PROGRESS.md updated) before starting the next.
- Dates are IST (UTC+5:30). Jam deadline: **5 Oct 03:29 IST** (4 Oct 23:59 CEST). Internal submission target: **4 Oct 20:00 IST**.

| Chapter | Name | Target finish | Human playtest focus |
|---|---|---|---|
| 1 | A World That Lives Without You | 29 Sep, night | Does the untouched planet feel alive and look good? |
| 2 | Hand of God | 1 Oct, night | Are powers fun and chains understandable? |
| 3 | Deep Time | 2 Oct, night | Does the world feel alive over centuries/millennia? |
| 4 | Ship It | 4 Oct, 20:00 | Final polish check, then submit |

Feature freeze: **3 Oct, 22:00 IST**. After that: bug fixes, tuning, polish, builds and store pages only.

---

## Chapter 1 — A World That Lives Without You

The big one. By the end: a beautiful planet that runs by itself for centuries, with weather, water, vegetation, fire, animals, fish, villages and individual people; time controls; camera; basic inspector. No player powers yet (tests use direct sim hooks).

### 1.1 Toolchain & skeleton
- Verify Java 21+, Node 20+, LuaJIT, Git. Pin the Defold stable version and download `bob.jar` (`npm run setup`).
- Create the Defold project (`game.project`, `game/main.collection`, empty render script, input bindings), the repo layout from 05_ARCHITECTURE.md §2, `.gitignore`, npm scripts, Playwright config.
- Test bridge skeleton: `?test=1` publishes `window.__TC = {ready:true,...}`.
- **Done when**: `npm run build` produces an HTML5 bundle; `npm run test:web` W01 passes (page loads, ready flag, no console errors); first commit.

### 1.2 Sim foundation
- `grid` (icosphere level from config, neighbours, lat/lon, area), `fields` (struct-of-arrays + prev buffers), `calendar`, `rng` (counter-based), `config`, `world` (state + tick orchestration + command queue/log), `metrics`, `save` (string round trip), causality skeleton (`events`, `blame` API with no-op terms allowed), headless runner `tools/run_sim.lua` (+ `npm run sim`), map dump to PNG.
- `npm run lint:lua` (05_ARCHITECTURE.md §4).
- Benchmark a representative tick in LuaJIT and in the browser (via bridge); decide `SIM_LEVEL` **provisionally** (02_WORLD_MODEL.md §2) and log it.
- **Done when**: unit tests for rng/grid/calendar/save/events/blame pass; lint clean; P01 determinism passes on a trivial world; `npm run sim` writes summary/series/map files.

### 1.3 World generation
- Plates, elevation noise, continents/islands with constraints, sea level, drainage, lat-based initial climate, initial biomes, rivers, volcano sites, tanker lanes, 3–5 villages with 150–250 people, fauna/fish seeding, region & person names (content/names.lua), 30-year spin-up.
- **Done when**: P10 passes (20/20 seeds satisfy constraints); map PNGs for 3 seeds look like believable planets (Claude reviews them).

### 1.4 Climate & water
- Temperature & seasons, wind/current tables, sst, heat_anom, moisture/evaporation/advection/precipitation (incl. orographic), snow & ice, soil water, rivers, floods, erosion & silt; global climate integration (co2, aerosol, g_temp, sea level; no geology yet).
- Blame hooks for every field written.
- **Done when**: maps show wet windward coasts, rain shadows, deserts, polar ice, seasonal cycles in series.csv; no NaN over 500 y (P06 partial).

### 1.5 Vegetation, soil, fire, biomes
- Grass/forest dynamics, succession, fertility, fuel/dryness, fire (natural ignition, spread, burn, effects), biome classifier with hysteresis and dwell, biome_shift and wildfire detectors.
- **Done when**: 200-year baseline keeps vegetation bands; natural fires occur and forests recover; biome maps change over decades but stay plausible; `SIM_LEVEL` **finalised** from a browser benchmark of the now-realistic tick and logged in DECISIONS.md.

### 1.6 Oceans & animals
- Plankton, fish, oil field (spread/decay/beaching; spawned by a test hook), grazers, predators (prey refuge), migration with blame-carrying flows, ice-bridge crossing rule, population boom/crash/extinction detectors.
- **Done when**: S02 wolves_removed passes (via test hooks, 07_TESTING.md §3); predators oscillate without extinction in baseline; S04 oil portion (fish collapse) passes via test hook.

### 1.7 Villages & people
- Villages, farms/land use, crops & harvest, fishing, hunting, wood burning (CO₂), food store, individuals (aging, pairing, births, deaths, jobs, traits, memories), migration, founding, abandonment, ruins, predator culling near farms, tankers & boats moving on lanes.
- Detectors: crop_failure, famine, deaths_spike, migration, village_founded, village_abandoned, fish_collapse, oil_beached.
- **Done when**: S01 baseline_stability passes on the quick profile (3 seeds × 200 y) with all bands; S03 farm_fire_famine and S04 tanker_oil pass via test hooks; P02 save round trip passes.

### 1.8 Planet rendering
- Procedural model generator (Node → glTF) for all props in 06_PRESENTATION.md; palette texture; toon material/shaders; sky gradient; atmosphere glow.
- Terrain mesh from sim (render level 5, incremental colour updates), ocean sphere & shader (depth, foam, oil, sea ice), clouds from moisture, rain/snow, props with hemisphere culling and LOD, animal/people puppets, basic fire/smoke FX.
- A bridge-driven camera (presets, set position/zoom) is enough in this stage; interactive controls come in 1.9.
- **Done when**: Chapter-1 screenshot items 2–4 pass Claude's art checklist; `view_ms`, prop counts and draw calls within budget in the release build (bridge numbers; fps is judged by the human, since headless WebGL is CPU-rendered); W01 still green.

### 1.9 Camera, time, HUD, inspector
- Trackball camera, zoom, fly-to, follow, picking (cell/village/person/animal/ship); speed controller with ⏸ ▶ ▶▶ ▶▶▶ ⏩; HUD (clock, population, temp, CO₂, ice, sea level); inspector for cell/village/person/planet (facts only; "Why?" comes in Chapter 2); debug overlay (F3); edge-of-screen indicators stubbed.
- **Done when**: W02 speed and W09 UI (Chapter-1 scope) pass; feel reviewed via screenshots.

### 1.10 Chapter 1 hardening → PLAYTEST 1
- Full `npm run test:sim -- --full`, `npm run test:web`, `npm run shots` (Chapter ≤ 1 tests and shot items only); fix failures; perf pass (W08 sim budget); update `tests/bench-baseline.json`.
- Write `docs/playtests/chapter-1.md` (what to try: watch 100 years at ⏩, zoom into villages, click people, look at deserts/ice/rain; questions: alive? pretty? readable? smooth on your machine?). Commit. **Stop and wait for the human.**

---

## Chapter 2 — Hand of God

Powers, full causality UX, dialogue, saving, the first-minute experience.

### 2.1 Powers
- Command types and power framework (targeting, radius preview, cooldowns, VFX/SFX hooks, root events). Powers 1–8 from 04_PLAYER.md (Lightning, Weather ±, Temperature ±, Cull, Introduce incl. Settlers, Blight, Oil Spill, Meteor without tsunami).
- Switch S02 (via `cull_all`), S03 and S04 to real powers; add S05, S06, S07, S10 (meteor part), S11, S12.
- **Choose and freeze the tutorial world**: a seed where Lightning on the tanker near a fishing village reliably produces a ≥ 3-link chain within 3 years; ship it pre-spun in `assets/worlds/`; add S15.
- **Done when**: those scenarios pass the quick profile; S15 passes; W03 passes.

### 2.2 Causality complete
- Blame in every system verified, global ledgers with categories, all 03_CAUSALITY.md §4 detectors (with re-tagging, aggregation and cooldowns), headline rules, descendants counters, region naming for events.
- **Done when**: P08 blame coverage ≥ 90%; P09 event rate within bounds; chains in scenario outputs read correctly (Claude reviews events.txt samples).

### 2.3 Chain popup, "Why?", event log
- Chain popup (clickable links, fly-to, collapse long chains, consequence counter, "caused by you" styling), pause-on-my-consequences, "Why?" on cell/village/person/planet, event log panel, edge-of-screen indicators for off-screen headlines.
- **Done when**: W04 passes; screenshot item 5 passes review.

### 2.4 Chronicle & notifications
- Chronicle ticker with templates for every headline kind; toasts; notification sounds.
- **Done when**: 100-year run produces varied, correct, funny headlines (Claude reviews a sample of 30).

### 2.5 Dialogue
- `content/dialogue.lua` format, selection engine, ≥ 300 lines across ≥ 40 condition groups in the 06_PRESENTATION.md voice, memory references, Talk button, person inspector complete.
- **Done when**: a sampled report of 50 villagers' lines across different states reads as varied and on-tone (Claude reviews); no template slot left unfilled.

### 2.6 Saving & coming back
- Local save adapter, autosave rules, save versioning, "Continue" on title, offline catch-up (cap 20 years until Chapter 3), "While you were away" digest. Wavedash cloud-save adapter behind feature detection (can only be fully tested on Wavedash; test the code path with a mock).
- **Done when**: W05 and W10 pass; P02/P03 still pass.

### 2.7 Onboarding & audio v1
- Title screen with god name, guided first mischief on the tutorial world (S15 must keep passing); procedural SFX set v1; music hook.
- **Done when**: S15 passes every time; W04 on the tutorial flow passes.

### 2.8 Chapter 2 hardening → PLAYTEST 2
- Full suites, shots, perf. Write `docs/playtests/chapter-2.md` (try: the tutorial, then 5 minutes of mischief; questions: are chains clear? surprising? funny? which power is boring?). **Stop.**

---

## Chapter 3 — Deep Time

### 3.1 Epoch mode
- Epoch regime (02_WORLD_MODEL.md §5.14), speed 🌀, mode switching, P05 epoch consistency; offline cap raised to 1,000 years using epoch beyond 20 years.
- **Done when**: P05 passes; 1,000 years run in ≤ 25 s in the browser (≥ 40 years/s; target 50).

### 3.2 Geology
- Plates stress, earthquakes, volcanoes (pressure, VEI 1–6 effects, ash, aerosols, CO₂), ocean volcano island building + coastline/current/drainage rebuilds, tsunamis (instant resolution + ring animation), slow uplift (cut-able), powers 9–10 (Wake Volcano, Quake), meteor tsunami.
- **Done when**: S08 and S10 pass; eruption frequency within band in baseline; new_island appears in long runs.

### 3.3 Climate extremes & sea level
- Carbon cycle (weathering thermostat, ocean uptake), ice–albedo runaway, Snowball and Hothouse eras with recovery, sea-level coastline changes (flooded villages relocate/drown, land bridges), era change events and visuals (planet look for ice age/hothouse).
- **Done when**: S09, S13, S14 pass; screenshot item 9 passes review.

### 3.4 Extinction & recovery rules
- Global extinction permanence, human extinction handling (chronicle, Settlers option), Dead World era, refuge recolonisation.
- **Done when**: 5,000-year epoch runs on 5 seeds never crash, never NaN, and produce at least one era change in ≥ 2 seeds.

### 3.5 "What if I hadn't?"
- Snapshots before player actions (last 3), background counterfactual replay, comparison card. P04 must pass.
- **Done when**: works from the chain popup for the tutorial chain and one random chain; cancellable; ≤ 10 s for 20 years in the browser.

### 3.6 Chapter 3 hardening → PLAYTEST 3
- Full suites, shots, perf. Write `docs/playtests/chapter-3.md` (try: leave it at 🌀 for a few minutes; wake a volcano; try a snowball; use "What if?"). **Stop.**

---

## Chapter 4 — Ship It

### 4.1 Goals ("Divine Mischief")
- The 10 goals from 04_PLAYER.md §7, detection, toasts, menu list, saved.
- **Done when**: each goal has a headless test that triggers it (scripted actions on a fixed seed) and one that doesn't falsely trigger in a 100-year untouched run.

### 4.2 Polish pass
- Juice (camera shake, particles, pops), UI polish, readability, reduce-motion setting, mobile/touch sanity, loading screen, favicon/title, credits screen (fonts, tools, AI use).
- **Done when**: the full screenshot set passes the art checklist; playtest-3 feedback items marked done or consciously deferred in PROGRESS.md.

### 4.3 Audio pass
- Mix, loops, speed-scaled volumes, music from `assets_inbox/` if present and licensed.
- **Done when**: every power and headline kind has a sound; volume settings work; no clipping at ⏩ (spot-check recorded audio levels in a web test).

### 4.4 Performance & stability pass
- Profile release build; fix hotspots; 30-minute soak test at ⏩ via Playwright (no errors, memory stable).
- **Done when**: W08 passes; soak test passes; budgets in 05_ARCHITECTURE.md §9 met.

### 4.5 Release builds & store pages
- `npm run build:release`, `npm run package:itch`, `npm run build:wavedash` (game_id from `HUMAN_INPUTS.md`; if missing, list it as a blocker and finish everything else).
- Draft `docs/release/itch_page.md` and `docs/release/wavedash_page.md`: pitch, controls, how to play, screenshots/GIF captured via Playwright, credits, **AI disclosure** (what AI was used for: code by Claude Code directed by the developer, dialogue/text drafting, procedural SFX/model tooling; list any AI music if used), and the Wavedash link on the itch page (required for the Wavedash prize).
- **Done when**: release bundles pass W01–W10 against the release build.

### 4.6 FINAL PLAYTEST → submit
- Write `docs/playtests/chapter-4.md` with the final checklist. **Stop.** The human uploads (or tells Claude Code to run the upload scripts after logging in) and submits to the jam.

---

## Cut order (if behind schedule)

Check the clock at every stage boundary. If a chapter is more than ~half a day behind its target, cut from the top of this list until back on track, and log each cut in DECISIONS.md:

1. "What if I hadn't?" (3.5)
2. Slow uplift (3.2)
3. Tsunami animation (keep the sim effect, simple flash instead)
4. Goals reduced to 5
5. Dialogue reduced to 150 lines
6. Epoch mode (keep ⏩; offline cap 50 years)
7. Snowball/Hothouse extremes (keep CO₂, ice, sea level basics)
8. Quake power

Storms (02_WORLD_MODEL.md §5.3) are a stretch goal, built only if Chapter 3 finishes early.

Never cut: the living baseline world, chain popups with "Why?", time controls, saving, Lightning/Weather/Cull/Introduce/Oil/Meteor, onboarding, the Wavedash init.
