# 07 — Testing & self-verification

Claude Code builds this game mostly unsupervised. Tests are how it knows the world behaves, the game runs, and the chains are real. The human tests only what a machine can't judge (§6).

Principle: **emergent systems are tested statistically.** Scenarios run on several seeds and must pass on most of them, not all. Deterministic properties (determinism, save/load) must pass always.

---

## 1. Layers

| Layer | Tool | Runs | Speed |
|---|---|---|---|
| Unit | LuaJIT headless | `npm run test:sim` | seconds |
| Scenario (chains) | LuaJIT headless | `npm run test:sim` | < 2 min quick profile |
| Stability & properties | LuaJIT headless | `npm run test:sim` (`--full` for 10 seeds) | quick < 3 min, full < 15 min |
| Browser | Playwright + test bridge | `npm run test:web` | a few minutes |
| Visual review | Playwright screenshots + Claude reading the PNGs | `npm run shots` | a few minutes |
| Human playtest | the human | end of each chapter | — |

Every stage ends with all relevant layers green (08_ROADMAP.md says which).

---

## 2. Unit tests (examples)

- `rng`: distribution sanity, determinism, independence across streams/cells.
- `grid`: every cell has 5–6 neighbours, adjacency symmetric, 12 cells with 5, areas ≈ equal, lat/lon correct.
- `calendar`: tick ↔ year/season/month conversions.
- `blame`: dominant-term selection, 1.5× margin, idle reset.
- `events`: ring buffer, pinning, parent re-pointing on drop, aggregation.
- `save`: serialise → deserialise round trip; version mismatch handled.
- Each system: a tiny hand-built grid (e.g. 12 cells) with known inputs → expected direction of change (e.g. more rain → more soil water → more grass).

---

## 3. Scenario tests (the chains)

Location: `tests/sim/scenarios/*.lua`. Each file returns a table:

```lua
return {
  name = "wolves_removed",
  seeds = "standard",                -- quick: 3 seeds, full: 10
  pass_ratio = { quick = 2/3, full = 7/10 },
  run_years = 10,
  chapter = 1,
  actions = {
    -- cull_all issues Cull (radius 2) on every predator cluster of the target, like a determined player would
    { year = 1, helper = "cull_all", species = "predators", target = "continent:largest" },
  },
  expect = {
    { metric = "grazers@target", compare = "ratio_to_baseline", op = ">=", value = 2.0, within_years = 4 },
    { metric = "forest@target",  compare = "ratio_to_baseline", op = "<=", value = 0.85, within_years = 8 },
    { chain_from_action = 1, min_links = 3, must_include = { "population_boom" } },
  },
}
```

- `target` selectors are helpers: `continent:largest`, `village:fishing`, `village:inland`, `ocean_near:village:fishing`, `island:no_predators`, `tanker:1`, `volcano:largest`, `cell:<id>`.
- `ratio_to_baseline`: compare to the **same seed without the action** (run both). This isolates the action's effect from natural drift.
- Chain expectations walk the causality **event tree** from the action's root (all derived events, not only headlines): some descendant chain has ≥ N links and includes the listed kinds.
- **Test hooks** (before the real powers exist): `world:test_action(name, params)` applies a direct effect (remove predators, ignite cells, release oil...) and creates a root event `test_<name>` with actor `player`, writing its id into the cause tags of the fields it changes, exactly like a power will. When the real power exists (Chapter 2), switch the scenario to it.

### Required scenarios

| id | Scenario | Key expectations | Chapter |
|---|---|---|---|
| S01 | baseline_stability (no actions, 200 y) | all tuning bands in 02_WORLD_MODEL.md §10 | 1 |
| S02 | wolves_removed | grazers ×2 ≤ 4 y; forest ≤ 0.85× ≤ 8 y; chain ≥ 3 links incl. population_crash → population_boom | 1 (test hook) / 2 (Cull via cull_all) |
| S03 | farm_fire_famine (burn a village's farms before harvest) | crop_failure then famine ≤ 1 y; population down vs baseline; chain ≥ 3 | 1 (fire via test hook) / 2 (via power) |
| S04 | tanker_oil | fish in nearby sea ≤ 0.5× ≤ 1 y; village food down; oil_beached event; chain includes oil → fish → famine or food drop | 1 (oil via test hook) / 2 |
| S05 | ocean_heat_rain_shift | precipitation downwind changes ≥ 15% over the season vs baseline | 2 |
| S06 | drought_wildfire | a wildfire (≥ 10 cells) within 2 seasons in ≥ 2/3 seeds | 2 |
| S07 | invasive_island | grazers introduced to predator-free island → vegetation ≤ 0.5× ≤ 10 y; desert/barren biome shift persists 10 y after grazers crash | 2 |
| S08 | supervolcano_winter | forced VEI 6 → g_temp ≤ −3 °C within 2 y; crop_failure on ≥ 2 continents | 3 |
| S09 | human_climate_drift (300 y, no actions) | g_temp +1 … +4 °C; sea level up; at least one coastal flood/relocation in ≥ 3/10 seeds | 3 |
| S10 | meteor_ocean | tsunami event; ≥ 1 coastal village damaged; aerosol spike | 2 (meteor) / 3 (tsunami) |
| S11 | blight_spread | blight reaches ≥ 2 villages in ≥ 2/3 seeds; famine follows | 2 |
| S12 | chill_sea_ice | sea ice forms; fishing catch down; animals cross to new land in ≥ 1/3 seeds | 2 |
| S13 | snowball_and_recovery (epoch) | forced cold → Snowball era; recovers to Garden/Ice Age ≤ 20,000 y | 3 |
| S14 | hothouse_and_recovery (epoch) | forced CO₂ → Hothouse; recovers ≤ 10,000 y | 3 |
| S15 | tutorial_chain (fixed tutorial seed, chosen in Stage 2.1) | Lightning on tanker → headline chain with ≥ 3 links within 3 y. **Must pass every time.** | 2 |

"Test hooks": see above. Once the power exists, switch the scenario to use it (Stage 2.1).

---

## 4. Property tests (must always pass)

| id | Property |
|---|---|
| P01 | Determinism: same seed + same commands → same `state_hash` at years 10 and 50 |
| P02 | Save round trip: save at year 20, continue to 40 == load + continue to 40 (hash) |
| P03 | Command-log replay: replaying the log from a snapshot reproduces the live state |
| P04 | Counterfactual locality: a Lightning on one continent changes another continent's aggregates by < 1% after 1 year |
| P05 | Epoch consistency (Chapter 3): detailed vs epoch over 200 y, same seed: forest cover and desert within ±5 percentage points, ice fraction ±3 points, g_temp ±0.5 °C, population ±25% |
| P06 | No NaN/inf; every field within its declared range, checked yearly over 500 y |
| P07 | Perf regression: per-system tick ms vs `tests/bench-baseline.json` (committed); fail on > 30% regression (update the baseline intentionally, log in DECISIONS.md) |
| P08 | Blame coverage: in scenario runs, ≥ 90% of headline events have a complete chain to a root; player-caused chains are attributed to the player |
| P09 | Event rate: baseline headline events per year within [0.2, 3] — neither dead nor spammy |
| P10 | Worldgen constraints hold for 20 of 20 seeds (continents, islands, villages, volcano sites, lanes) |
| P11 | `npm run lint:lua` clean; unit tests also pass under `lua5.1` when it's installed |

---

## 5. Browser tests (Playwright, `tests/web/`)

All run against a local server with `?test=1&skip_intro=1`, driving the game through the bridge (05_ARCHITECTURE.md §10).

| id | Test | From chapter |
|---|---|---|
| W01 | Smoke: page loads, `__TC.ready` within 20 s, no console errors, no bridge errors | 1 |
| W02 | Speed: ticks advance at the expected rates per speed setting | 1 |
| W03 | Powers: each power creates its root event and a visible change near the target (screenshot diff around the target) | 2 |
| W04 | Chain popup: tutorial seed + Lightning on the tanker → popup with ≥ 3 links within the expected time | 2 |
| W05 | Save & reload: save → reload page → same tick and state hash | 2 |
| W06 | In-browser self-test: determinism hash identical across two runs in the HTML5 runtime | 1 |
| W07 | Screenshot set (see §7) | 1 |
| W08 | Performance: release build, max speed for 30 s, `sim_ms_avg` within budget | 1 |
| W09 | UI: inspector opens for cell, village and person; HUD panels don't overlap at 1280×720 and 1920×1080 (chain popup added to the check from Chapter 2) | 1 |
| W10 | Offline catch-up: a bridge debug command fakes an old save timestamp → catch-up runs → "While you were away" card appears | 2 |

---

## 6. What only the human tests

At the end of each chapter (08_ROADMAP.md), the human plays the release build and answers the playtest questions. Claude Code must not stop mid-chapter for things tests can check.

Human-only judgements:
- Is it fun? Does the world feel alive when left alone?
- Are chains understandable and surprising? Is the humour landing?
- Does it look good on a real GPU at real resolution? Does it run smoothly on their machine?
- Do controls feel right (camera, powers, speed)?
- Audio feel.
- Anything that feels broken in real play.

---

## 7. Screenshot protocol (Claude's visual self-review)

`npm run shots` captures, with `freeze=1` and fixed seeds (the chapter in brackets is when each item starts being required):

1. [2] Title screen.
2. [1] Full planet at year 0 from 3 presets (continent A, continent B, a pole).
3. [1] Mid zoom on a village; close zoom on villagers (camera set via the bridge).
4. [1] Same presets after 50 years untouched.
5. [2] After tanker oil + 2 years (oil visible, fish collapse), with chain popup open.
6. [2] After a meteor (crater, fire, dust).
7. [1] Inspector open on a person. [2] Event log open.
8. [1] 1280×720 and 1920×1080 variants of the HUD.
9. [3] Ice-age planet and hothouse planet.

Claude reads each PNG and checks it against 06_PRESENTATION.md §6. Any failure becomes a fix task before the stage is marked done. Keep the latest set in `out/shots/latest/`; mention notable before/after pairs in the playtest report.

Headless sim maps: `npm run sim -- --map` also writes equirectangular PNG maps (biomes, temperature, precipitation, population) per decade to `out/sim/<run>/maps/`. Use them to eyeball sim behaviour without Defold (deserts in the right places, rain shadows, fire scars, ice extent).

---

## 8. Reporting

- After each stage: append a short entry to `docs/PROGRESS.md` (what was done, test status, numbers that matter such as tick ms and band results, known issues).
- Keep the latest full test output in `out/reports/latest.txt` (not committed).
- At chapter end: write `docs/playtests/chapter-N.md` (format in `docs/playtests/README.md`).
