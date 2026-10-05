# TC2 Session handoff

## Session 1 (5 Oct 2026) — in progress (see PROGRESS.md CURRENT STATE)

### Exact state
- Commits: a2283fa (docs), 18cd0cc (Core + POC), next (activity layer/calibration), c478f96 (Unity host).
- Core simulation and headless runner work; 23 tests green in dotnet and in Unity EditMode; Unity Mono and
  .NET 8 produce the same probe hashes.
- Web build: first build running/landing; Playwright test `tc2/scripts/webtest.mjs` written (not yet run).

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out tc2/out/run42] [--why] [--profile]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| History stats | `python tc2/scripts/histstats.py tc2/out/run42` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` / `node tc2/scripts/unity.mjs test` |
| Web build | `node tc2/scripts/unity.mjs build-web [--dev] [--threads] [--webgl2]` -> `tc2/web-build/web` |
| Serve | `node tc2/scripts/serve.mjs` (http://127.0.0.1:8642, COOP/COEP on) |
| Web test | `node tc2/scripts/webtest.mjs [--webgl2] [--no-gpu] [--tag T] [--skip-away]` |
| URL params | `?fresh=1` new world, `?seed=N`, `?speed=I`, `?absence=24h` pretend an absence, `?view=close|village|region` |

### Important paths
- Unity editor: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`; bundled dotnet:
  `.../Editor/Data/DotNetSdk/dotnet.exe`; local manuals `.../Editor/Data/Documentation/en/Manual/` (authoritative).
- Core: `unity/TinyConsequences2/Assets/TC2/Core/{Foundation,Scheduling,Simulation,Planet,Places,Agents,History,Persistence,Diagnostics,Worldgen}`.
- Game: `.../Assets/TC2/Game/{Host,View,UI,Platform}`, shaders in `Game/Resources/TC2Shaders`, jslib in `Game/Platform/Plugins`.
- Editor: `.../Assets/TC2/Editor/{ProjectSetup,BuildScript,BatchTools}.cs`.
- Tests: `.../Assets/TC2/Tests/Core/{FoundationTests,SimulationTests}.cs` (also compiled by `tc2/headless/tests`).

### Pitfalls found
- Bash heredocs that contain quotes/apostrophes break: write patch scripts with the Write tool (scratchpad `patch_*.py`, idempotent `rep`).
- C# 9 forbids struct field initializers: the headless netstandard2.1/C# 9 build catches what Unity would reject.
- Never use fractional seconds for wake/bed times: integer seconds only (people slept 24 h).
- Per-cell transcendentals in a 6-hourly tick dominate cost: precompute per-cell constants.
- A traveller told to wait on a blocked road needs a limit (`Until`) or he starves there.
- `cd` inside a Bash call changes the displayed primary directory; prefer absolute paths.
- Unity's first batch import takes ~2.5 min; a target switch to WebGL re-imports (slow first Web build).

### Next steps
1. Land the Web build; run `webtest.mjs`; look at every screenshot; fix rendering problems.
2. Browser benchmark of catch-up (years/s in wasm) vs dotnet; record.
3. Decisions D2-015+ (activity granularity, food laws, verified cross-runtime determinism, IMGUI F0 HUD).
4. Commit; then the roadmap: F1 living planet (tectonics, erosion, hydrology, energy-balance climate) and the
   planet's look; COARSE design deepening when catch-up numbers demand it.
