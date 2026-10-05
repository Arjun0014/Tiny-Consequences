# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

- **F0 Foundation done; F1 (living planet) started. Session 1 (5 Oct 2026), continuing.**
  - Docs: `docs/tc2/` 00-17, LEGACY_AUDIT, DECISIONS D2-001..021.
  - Core (`unity/TinyConsequences2/Assets/TC2/Core`, pure C# 9): ids, calendar, keyed RNG, DMath, binary I/O, paged
    stores, event queue (4-ary heap, lazy cancellation), World root with `AdvanceTo`, cubed-sphere planet with
    worldgen (plates, relief, sea level, moisture, vegetation), periodic planet systems (closed-form climate, daily
    weather with lightning, vegetation), fire as a process, settlements (land claims, crops on their fields' weather,
    harvest, granary, rationing, pre-modern demography, marriage across neighbouring villages), persistent people (one
    event per activity, analytic walks, route travel, foraging), the space-time travel index and perception of fire,
    causal history ledger (tiers, roots, importance, compaction), exact saves (deflate, hash-verified), offline
    progression, **COARSE resolution** for deep-time catch-up (D2-021), cross-runtime probe. `Planet/Drainage.cs`
    (priority-flood drainage) is written; rivers/runoff integration is the current slice.
  - Headless (`tc2/headless`, dotnet 8 from the editor): 26 tests green; `run`, `bench` (`queue|dmath|poc [--coarse]|save`),
    `probe`, `hash`, `hashtest`, `inspect`.
  - Unity: URP + Web settings from code, bootstrap scene, SimHost, planet / sea / atmosphere / instanced objects
    (material assets so instancing variants ship, D2-020), orbit camera and sun, IMGUI HUD, IndexedDB store + bridge,
    custom Web template with a WebGPU device probe (D2-019), in-page benchmark (`bench S F Y activity|coarse`).
  - **Web verified** (system Chrome, WebGPU): starts in 5.7 s; **browser probe == headless probe bit for bit**
    (dotnet RyuJIT == Unity Mono == IL2CPP wasm); time runs; reload continues the IndexedDB save; a day away (Living)
    catches up 96 years in 14.6 s (145 -> 739 people, ACTIVITY all the way). Remaining web item: one 404 (favicon;
    fixed in the template, to confirm), WebGL2-path run, in-browser benchmark table.
  - Visual state (screenshots `tc2/out/webtest/chrome2`): instanced houses, trees, people and fire visible; morning
    start; open issues: sea glint aliasing, trees too dark, fields one dark green whatever the season, houses overlap
    in grown villages.

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
