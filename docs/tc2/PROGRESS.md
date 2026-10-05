# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

- **F1 in progress (hydrology, climate, tectonics, volcanoes + carbon cycle, quakes done) + look-dev v1-v5 (Arjun: "a really
  beautiful miniature world, high detail, high performance"). Session 1 (5 Oct 2026), continuing.**
  - Docs: `docs/tc2/` 00-17, LEGACY_AUDIT, DECISIONS D2-001..030.
  - Core (pure C# 9, headless-tested, 58 tests): everything of F0 (see the log) plus **hydrology** (D2-022: runoff
    down a priority-flood tree, depressions that fill/spill/evaporate/dry, smoothed river flow), **catch-up plan**
    (D2-023: COARSE for many lives, ACTIVITY on arrival), **climate** (D2-024: zonal energy balance, fictional fast
    orbit, ice-albedo, CO2/aerosol, land ice sheets, sea level, coastline in 1 m steps, ages of ice in the history),
    **tectonics** (D2-025: one crust sample per cell, moved every 10 y and handed over every 160 y; ridges,
    subduction, collisions, accretion, buoyant shelves, wandering plate motions; villages ride their plates),
    **volcanoes** (D2-028: magma at arcs, rifts and fixed plumes; vents that ride their plates, pour out lava, build
    islands and erupt by pressure against a per-vent seal; eruptions as events whose lava fires, ash on crops,
    deaths and veil carry them as cause; named volcanoes) and the **carbon cycle** (outgassing vs weathering, a
    ~2,000-year thermostat), **quakes** (D2-030: faults loaded by the plates' relative motion, Olami-Feder-Christensen
    cascades, intensity by distance; deaths indoors, spilled stores, landslides, waves over low coasts), god powers
    `GodEruption` and `GodQuake` (commands; consequences rooted in You).
  - Look (Game code): v1 (D2-026) ground model + chunked LOD terrain, farmland and village ground, translucent sea +
    surf, rivers, haze/sky/grade, Blender kit (`tc2/art/kit.py`); v2-v4 (D2-027) clouds from the weather with cloud
    shadows, tilt-shift, window lamplight, grass/flowers/reeds/boulders, roads, seasonal canopies, walking people, a
    slim HUD; v5 (D2-029) volcanoes drawn (cones, craters, steam, eruption columns, lava glow), puffs, gentle shores,
    moonlight, quake shake/dust/foam from the `FxRing`, roads and farmland antialiased at region scale.
    Batch-editor screenshots: `node tc2/scripts/unity.mjs shots --tag T [--erupt VEI] [--quake M]`.
  - Web: volcanoes (look-dev v5 part 1) verified 6/6 (bit-identical, WebGPU, 89-121 fps by view); the quake Core and
    the antialiasing are in the next build.
  - Next: stream-power erosion and sediment (river valleys, deltas), storms, the 10,000-year untouched run (F1
    done-when), then F2 ecology. Known: in coarse runs 2-3 of 6 villages are abandoned within 300 years (agent/economy
    fragility, pre-existing; look at it with F2/F3).
  - Headless tools: `planet` (stats + maps, `--tect`, `--volc`, `--zoom first:DEG`), `climate` (climate-only
    millennia), `run --profile` (time per event kind and system).

## Measurements (session 1)

| What | Host | Result |
|---|---|---|
| Event queue pop+push | dotnet | see `tc2 bench --name queue` |
| POC world, 6 villages, ~145 -> ~380 people, 30 years | dotnet | 2.2-2.3 s (13-14 world years/s); 3 seeds stable, no starvation |
| Agent event cost (steady state) | dotnet | ~0.8 us per activity event; ~9 events per person-day |
| Planet systems fixed cost | dotnet | ~40 ms per world year (weather 21, climate 13, vegetation 6) |
| Save (1 year, ~170 people) | dotnet | see `tc2 bench --name save` |
| Cross-runtime probe | dotnet vs Unity Mono | identical: `dmath 387e8102.. rng dab34ace.. gen 3d8c0c4d.. d1 fb641c50.. d2 a907e40e.. d3 0d12bcbc..` |
| Unity first import + configure | editor batch | 155 s; later batch starts ~22-28 s |
| ACTIVITY 100 years, 6 villages, 145 -> 567 people (seed 42) | dotnet | 12 world years/s; hunger episodes, childbirth deaths, an abandoned village |
| COARSE 100 years, same worlds (2 seeds) | dotnet | 34-36 world years/s; agent events 66K vs 15M per century; planet systems dominate |
| Cross-runtime probe (current code) | dotnet == Mono == IL2CPP wasm | `dmath 387e8102.. rng dab34ace.. gen 559e64de.. d1 2c8f4aad.. d2 d5fb790f.. d3 9765782f..` |
| Web build (release, Brotli) | Unity 6.6 IL2CPP | 10.1 MB (wasm 6.0 MB, data 3.8 MB); incremental rebuild 8-9 min |
| Web start to a running world | Chrome, WebGPU | 5.7 s |
| Web catch-up, a day away (Living = 96 years), ACTIVITY | Chrome | 14.6 s (6.6 world years/s; 17M events, ~0.9 us/event) for 145 -> 739 people |
| Web save | Chrome | 356 KB / 46 ms at year 0; 646 KB / 80 ms at year 96 (739 people, 5,640 history entries) |
| Web frame rate | Chrome, RX 5600M, 1280x720 | orbit 120 fps; village/close 49-116 fps |
| Planet only (climate + plates + weather), COARSE | dotnet | 25-28 world years/s (seed 42, 4,000 years) |
| Planet + people COARSE, smooth weather (before / after tabulated logistic + one-mix hash) | dotnet | 14.3 / 19.4 world years/s (weather 1.25 / 0.82 ms a day) |
| Volcanoes, seed 42, 4,000 years planet only | dotnet | 28-42 vents, 650 eruptions (574 VEI 1, 65 VEI 2, 7 VEI 3, 1 VEI 4), CO2 280 -> 336 ppm |
| Web frame rate, look-dev v4 | Chrome/WebGPU, RX 5600M, 1280x720 | orbit 111, region 120, village 118, close 91, low 123 fps |
| Web frame rate, volcanoes (v5 part 1) | Chrome/WebGPU, RX 5600M, 1280x720 | orbit 119, region 121, village 119, close 89, low 119 fps |
| Quakes, seed 42, 3,000 years planet only | dotnet | M5 4,741, M6 1,753, M7 93, M8 0; ~1 landslide a year; 20.5 world years/s coarse |
| Slow climate alone | dotnet | ~3 ms per world year (0.75 ms per season step) |
| Look-dev shot round (batch editor Play Mode, 5 views) | editor | ~40-45 s |

## Blockers
None. (Wavedash cloud saves and itch upload need Arjun later; not needed for F0.)

## Log

### 2026-10-05 — session 1
- Environment: Ryzen 5 4600H 6C/12T, 11.4 GB RAM, RX 5600M; Unity 6000.6.4f1 (Web + Windows Mono + docs),
  bundled .NET SDK 8.0.318, Node 22, Python 3.13, Playwright browsers.
- Legacy audit and research; architecture decisions D2-001..014; docs committed (a2283fa).
- Core foundation + POC (18cd0cc): first run found people sleeping 24 h because a fractional wake time
  truncated one second short of morning (fixed with integer seconds); per-cell transcendentals in the climate
  tick made a day cost 31 ms (precomputed: 2 ms).
- Activity layer (next commit): one event per activity (walk+stay analytic), longer blocks: 210K -> 74K events a
  year. Calibration: a 48-day year needs 48 FU a person (yield 130 FU/ha); marriage across neighbouring villages
  (3 -> 51 marriages in 30 years); fires that burn out in days (median 37 h); rationing to the next harvest,
  foraging, travellers who give up waiting (a traveller had waited on a burning road until he starved), starvation
  by each body (17 villagers had died in the same hour).
- Unity integration (c478f96): project configured from code; Core compiles in Unity; EditMode 23/23; Mono probe
  equals dotnet.
- Land and people (4d7705a): settlements claim arable cells within reach each spring, crops grow on their fields'
  weather, fire burns a field's share; pre-modern mortality, childbirth deaths, marriage peaking in the early twenties,
  fertility by age: 100 years 145 -> 567 people with hunger episodes and an abandoned village.
- COARSE resolution (D2-021) with tests (enter/leave audit, determinism, save/load in COARSE); ~3x faster centuries.
- Web: Playwright's Chromium could not create a WebGPU device and Unity crashed instead of falling back: a template
  probe hides WebGPU when no device can be made (D2-019); tests use system Chrome. Instanced objects were invisible:
  materials became assets (D2-020). A rebuild once "succeeded" with compile errors (CS0104 `Resolution` ambiguous with
  UnityEngine.Resolution): the wrapper now fails on `error CS` in the log. Web test 5/6 (a 404, the favicon).
- F1 hydrology (a5c5c50), climate (30464c4), tectonics (19d7079): see D2-022..025; bugs found by tests and long
  runs listed there (lake depth derived from double vs float, sea level vs coastline masks, aliasing biases in plates).
- Look-dev v1 (2fbf6d4, D2-026): the terrain, farmland, village ground, water, sky, grade and the Blender kit; bugs:
  reversed winding test, shots before instanced draws, a new world inheriting a catch-up, Mathf.SmoothStep semantics,
  stale material values.
- Look-dev v2-v4 (b8f1593, c870730, 6848601; D2-027): clouds from the weather, smooth weather spells, tilt-shift,
  lamplight, ground cover, roads, seasons, walking, slim HUD; Web 6/6 each round.
- Volcanoes and the carbon cycle (D2-028): the first design opened 200+ vents and piled magma under plumes beyond any
  vent's reach (a VEI 6 from stored magma); each eruption rebuilt the drainage (half the speed, and a load would have
  derived drainage from relief the live world had not routed: cones now wait for the tectonic step). Profiling showed
  the smooth weather costing 86 % of a coarse planet year (exp per cell and a 4-mix hash per cell): tabulated logistic
  and a day key, 14.3 -> 19.4 world years/s.
- Volcanoes drawn and quakes (D2-029, D2-030): the first cone draft drew sea points under a clamp - cliffs at every
  coast near a vent; subduction coasts drew as sawtooth cliffs with a grey band of slope rock (coarse cells against a
  trench) - fixed by a drawn elevation that ramps shores; round canopies were lit at night by the sun under the
  horizon; quake cascades at pass 0.25 were near-critical and old faults kept stress for 2,000 years (an M 8 every 26
  years); dust was added to the puff list before the frame cleared it.
