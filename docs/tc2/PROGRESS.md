# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

- **Phase F0 Foundation, session 1 (5 Oct 2026) - in progress.**
  - Docs: `docs/tc2/` 00-17, LEGACY_AUDIT, DECISIONS D2-001..014.
  - Core (`unity/TinyConsequences2/Assets/TC2/Core`, pure C# 9): ids, calendar, keyed RNG, DMath, binary I/O, paged
    stores, event queue (4-ary heap, lazy cancellation), World root with `AdvanceTo`, cubed-sphere planet with
    worldgen (plates, relief, sea level, moisture, vegetation), periodic planet systems (closed-form climate, daily
    weather with lightning, vegetation), fire as a process, settlements (crops, harvest, granary, rationing,
    demography, marriage across neighbouring villages), persistent people (one event per activity, analytic walks,
    route travel, foraging), the space-time travel index and perception of fire, causal history ledger (tiers,
    roots, importance, compaction), exact saves (deflate, hash-verified), offline progression math, cross-runtime probe.
  - Headless (`tc2/headless`, dotnet 8 from the editor): 23 tests green; `run`, `bench`, `probe`, `hash`, `hashtest`, `inspect`.
  - Unity: URP + Web settings from code (`TC2.EditorTools.ProjectSetup.Configure`), bootstrap scene, SimHost, planet /
    sea / atmosphere / instanced objects, orbit camera and sun, IMGUI HUD, IndexedDB store + browser bridge (jslib).
    23/23 Core tests pass in EditMode; **Unity Mono == .NET 8 bit for bit** (probe).
  - In progress: first Web build; then the Playwright web test (start, browser = headless probe, screenshots,
    IndexedDB save/reload, a day away = ~96 years of catch-up).

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
