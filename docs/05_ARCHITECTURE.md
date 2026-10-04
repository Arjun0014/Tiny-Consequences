# 05 — Architecture

How the code is organised, how the sim and the Defold view talk, and the hard technical rules. Verify every Defold API against the official docs before using it (§12).

---

## 1. Shape of the system

```
            player input / test bridge
                     │ commands (queued, applied at next tick start, logged)
                     ▼
┌──────────────── sim/ (pure Lua 5.1, no Defold) ────────────────┐
│ world state (struct-of-arrays) · systems · causality · worldgen │
│ deterministic, fixed tick, testable headless with LuaJIT        │
└──────────────┬──────────────────────────────────────────────────┘
               │ read-only access after ticks + dirty flags + event feed
               ▼
┌──────────────── game/ (Defold) ────────────────────────────────┐
│ speed controller · view managers (terrain, ocean, clouds, props,│
│ puppets, fx) · camera/picking · GUI · platform (save, clock,    │
│ Wavedash, test bridge)                                          │
└─────────────────────────────────────────────────────────────────┘
```

Rules:
- `sim/` and `content/` never call Defold APIs (`go`, `msg`, `gui`, `vmath`, `sys`, `resource`, `html5`, ...). They must run under plain LuaJIT from the repo root.
- The view **never writes** sim state. Everything the player does is a **command** (`{tick, type, params}`) pushed into the command queue, applied at the start of the next tick, and appended to the command log (needed for counterfactuals and replays).

---

## 2. Repository layout

```
tiny-consequences/
├── CLAUDE.md  README.md  SETUP_HUMAN.md  STARTER_PROMPT.md
├── HUMAN_INPUTS.md            values only Arjun can provide (Wavedash game_id, ...)
├── game.project               Defold project file
├── wavedash.toml              Wavedash CLI config (game_id from HUMAN_INPUTS.md)
├── package.json               npm scripts (all tooling is Node, cross-platform)
├── .claude/settings.json
├── docs/                      design docs, PROGRESS.md, DECISIONS.md, playtests/
├── sim/                       PURE LUA
│   ├── config.lua             every tunable constant
│   ├── calendar.lua  rng.lua  grid.lua  fields.lua  world.lua  commands.lua
│   ├── metrics.lua  save.lua  powers.lua
│   ├── worldgen/              plates, terrain, climate_init, settle, names
│   ├── systems/               temperature, moisture, ice, hydrology, vegetation, fire,
│   │                          ocean, fauna, villages, people, ships, geology,
│   │                          globalclimate, biome, epoch
│   └── causality/             blame, events, event_kinds, detectors, chains, counterfactual
├── content/                   PURE LUA data: dialogue, names, chronicle templates
├── game/                      DEFOLD
│   ├── main.collection  main.script  game.input_binding
│   ├── render/                render script, materials, shaders
│   ├── view/                  terrain, ocean, clouds, props, puppets, fx, camera, picking
│   ├── ui/                    hud, powers bar, inspector, chain popup, chronicle, menus
│   └── platform/              clock, speed controller, save_local, save_wavedash, wavedash, bridge
├── assets/                    models (generated), textures, fonts, audio, worlds/ (pre-spun saves), CREDITS.md
├── assets_inbox/              files Arjun drops in (music etc.)
├── tools/                     run_sim.lua, map_dump.lua, bench.lua, modelgen/, sfxgen/,
│                              defold-version.json, bob/ (gitignored)
├── tests/                     sim/unit, sim/scenarios, web (Playwright specs), bench-baseline.json
├── scripts/                   setup.mjs, build.mjs, serve.mjs, shots.mjs, package.mjs
├── out/                       (gitignored) reports, maps, screenshots, logs
└── dist/                      (gitignored) bundles
```

Module paths: Defold resolves `require("sim.world")` to `/sim/world.lua`. The headless runner sets `package.path = "./?.lua;" .. package.path` and runs from the repo root, so the same `require` strings work in both.

---

## 3. npm scripts (the interface Claude Code and the human use)

| Script | Does |
|---|---|
| `npm run setup` | Checks Java/Node/LuaJIT, downloads the pinned `bob.jar`, installs Playwright Chromium |
| `npm run sim -- --seed 42 --years 100 [--map]` | Headless run; prints summary; writes `out/sim/<run>/` (summary.json, series.csv, events.txt, map PNGs) |
| `npm run lint:lua` | Rejects non-Lua-5.1 constructs in `sim/`, `content/`, `game/` (see §4) |
| `npm run test:sim` | Lint + unit + scenario + stability tests (quick profile: 3 seeds); `-- --full` = 10 seeds. Runs unit tests under `lua5.1` too when it's installed |
| `npm run bench` | Headless tick timing per system |
| `npm run build` / `build:release` | Defold HTML5 bundle (debug / release) into `dist/web/` |
| `npm run build:wavedash` | The release bundle as `dist/wavedash/` (wavedash.toml's upload_dir) and `dist/tiny-consequences-wavedash.zip`; the Wavedash SDK is found on the page at run time (D-224) |
| `npm run serve` | Static server for `dist/web` on http://localhost:8080 |
| `npm run test:web` | Build (if stale) + Playwright suite. Playwright's `webServer` option starts and stops the static server, so no server is left running |
| `npm run shots` | Capture the standard screenshot set to `out/shots/` |
| `npm run play` | Release build + serve + open the browser (for the human) |
| `npm run package:itch` | Zip the release bundle for itch.io |
| `npm run deploy:wavedash` | Upload via the Wavedash CLI (needs the human to be logged in) |

Scripts are Node (`.mjs`) so they work on Windows, macOS and Linux.

Test selection: every scenario and web test declares the chapter it belongs to; `test:sim` and `test:web` run only tests whose chapter ≤ the current chapter (read from PROGRESS.md or a `--chapter N` flag).

---

## 4. Sim core rules

- **Lua 5.1 only.** HTML5 runs Lua 5.1.4, not LuaJIT. The `bit` library is available in Defold on all platforms and in LuaJIT. LuaJIT silently accepts things Lua 5.1 doesn't, so `npm run lint:lua` must reject: `goto` and labels, `//`, `\x`/`\z`/`\u{}` string escapes, `ffi`, `load("string")` (use `loadstring`), two-argument `math.log(x, base)` (in 5.1 the base is ignored — use `math.log(x)/math.log(b)`), `table.unpack` (use `unpack`). Also avoid `#` on tables with holes (behaviour differs; can't be linted, so just don't). The in-browser self-test (§10) is the final check.
- **Struct-of-arrays**: each field is a flat array indexed by cell (`fields.forest[c]`). No per-cell tables.
- **Zero allocations per tick** in hot paths: preallocate scratch arrays; no closures, no string building, no `vmath` (not available anyway), no varargs in inner loops.
- **Localise** globals and field arrays at the top of each system update (`local forest = F.forest`).
- **No `pairs()`** in sim logic where order could affect results. Arrays and `for i = 1, n`.
- **Config**: every tunable in `sim/config.lua`, grouped by system, with a comment giving units and the intended effect.
- **Sparse sets** for rare things: active fires, blight, active storms, active oil cells (skip clean ocean).
- **Neighbour reads use last tick's values** (02_WORLD_MODEL.md §0). Keep `prev_` copies only for fields with spatial terms; swap references, don't copy element-wise, where possible.
- **Metrics**: `sim/metrics.lua` computes the tuning-band metrics (02_WORLD_MODEL.md §10) cheaply on demand and once per year into a rolling series.

---

## 5. Determinism & randomness

- All randomness goes through `sim/rng.lua`: a **counter-based hash** `rng.hash(seed, stream, a, b, c)` returning a 32-bit unsigned integer, and `rng.float(...)` in [0,1).
  - Implement with `bit.bxor/bit.band/bit.lshift/bit.rshift` and 16-bit-split multiplications so every intermediate product stays below 2^53 (exact in doubles). Normalise signed results from `bit` ops.
  - Key by purpose: `stream` constants (WEATHER_WOBBLE, FIRE_IGNITE, FIRE_SPREAD, BIRTH, DEATH, PAIRING, MIGRATION, GEOLOGY, WORLDGEN, NAMES, ...), plus cell or entity key, tick, and a small counter.
  - **Entity keys must come from where and when the entity was created, never from global counters.** A person's key = (village founding cell, birth tick, index among that village's births that tick); a village's key = (founding cell, founding tick). Otherwise one extra birth anywhere would shift every later id and reshuffle unrelated rolls.
  - This keeps randomness **local**: an action in one place doesn't reshuffle rolls elsewhere. Required for fair counterfactuals.
- Forbidden in `sim/`: `math.random`, `os.time`, `os.clock`, reading real time, iteration over hash keys, table-address order.
- Floating point is identical only within one runtime. Tests compare state hashes within LuaJIT; the in-browser self-test (§10) checks determinism under the real HTML5 runtime separately.
- `sim.world:state_hash()` hashes quantised field values + entity state + clock, for tests.

---

## 6. Time stepping & speed control (game/platform/speed.lua)

```
ticks_due += dt * TICKS_PER_SECOND[speed]         -- 0, 2, 8, 48, ∞ (max)
t0 = now_ms()
while ticks_due >= 1 and now_ms() - t0 < SIM_BUDGET_MS do
  world:tick(); ticks_due -= 1
end
ticks_due = min(ticks_due, MAX_BACKLOG)          -- never spiral
```

- `SIM_BUDGET_MS` = 8 at max speed, lower at slow speeds. Use a millisecond clock available in Defold (e.g. `socket.gettime()`); verify it behaves in HTML5.
- Visual interpolation: the view interpolates movers between their last two sim positions using the fractional `ticks_due`.
- Epoch mode swaps the world into the epoch regime (02_WORLD_MODEL.md §5.14) with its own ticks-per-second.
- Offline catch-up runs the same loop with a large budget per frame behind a progress screen, so the browser stays responsive.

---

## 7. View layer (Defold)

- **Bootstrap** (`main.script`): generate or load the world, create view managers, start speed controller, init platform (Wavedash, bridge).
- **Terrain** (`view/terrain.lua`): a mesh component backed by a runtime buffer (render icosphere level 5). Per render vertex: position (elevation-displaced), normal, colour, plus a precomputed mapping to the nearest 1–3 sim cells with blend weights. When the sim marks cells dirty, rewrite only the affected vertices' colours (and positions if elevation changed), at most ~4 times a second, amortised across frames. Confirm the buffer/mesh update API in the Defold docs (buffer streams + resource update).
- **Ocean** (`view/ocean.lua`): a second mesh sphere at sea-level radius with per-vertex depth/oil/ice values and a water shader.
- **Clouds** (`view/clouds.lua`): pooled cloud puffs placed where moisture is high, drifting along the wind.
- **Props** (`view/props.lua`): pooled game objects created once from factories; assigned to cells according to sim values (trees ∝ forest, crop patches on farms, houses ∝ village size, ruins, volcanoes, boats, tankers). Only the **visible hemisphere** (dot(cell_normal, camera_dir) > threshold) gets props; distance LOD reduces density when zoomed out. Reassign props only when a cell's prop-relevant values change significantly.
- **Puppets** (`view/puppets.lua`): animals and people shown as wandering puppets near their cells/villages; only when zoomed in; hard cap ~300 Lua-driven movers per frame. Everything else animates in shaders (sway, bob, waves).
- **FX** (`view/fx.lua`): consumes the sim's event feed each frame to spawn effects (fire, lightning, tsunami ring following arrival order, etc.).
- **Camera & picking** (`view/camera.lua`, `view/picking.lua`): trackball orbit using quaternions, zoom, fly-to tweens, follow. Picking = ray vs planet sphere → nearest cell via a precomputed lookup, then entity picking by screen-space distance among nearby entities.
- **GUI**: Defold GUI scenes for HUD, powers bar, inspector, chain popup, chronicle ticker, menus, toasts; world-anchored icons are positioned by projecting world points to screen.
- **Render script**: perspective camera for the 3D world, sky gradient pass, then GUI. Keep materials few so the engine can batch.

---

## 8. Saving

**Year-boundary snap.** At every year boundary the sim quantises all fields to save precision (16-bit) as part of the normal tick. This is deterministic and happens whether or not anyone saves, so the live world and a saved/reloaded world never diverge.

**A save** = the snapped world state at the most recent year boundary + the commands issued since then + the current tick. Loading restores the snapshot and replays those commands up to the saved tick (≤ 48 ticks). The result is bit-identical to a world that never stopped (tested by P02).

- `sim/save.lua` serialises to a **string**: header (magic, save version, sim version, seed, snapshot tick, current tick, real timestamp) + quantised field arrays + entities + pinned events + commands-since-snapshot + recent command log + goals + settings.
- Compress (Defold's zlib API if available; otherwise a simple RLE) and keep the payload well under 256 KB. Check the size limits of whichever storage API is used.
- Autosave at most once every 30 real seconds, plus on focus loss and "Save now" (04_PLAYER.md §8).
- Platform adapters in `game/platform/`:
  - `save_local.lua`: Defold's save-file API (`sys.get_save_file` + `sys.save`/`sys.load`), storing the payload string. Verify it persists in the HTML5 build (W05 reload test).
  - `save_wavedash.lua`: Wavedash Remote Storage. The Defold binding's `*_async` calls must run inside a coroutine: `write_local_file_async(path, string)` then `upload_remote_file_async(path)`; load with `download_remote_file_async(path)` then `read_local_file_async(path)`, which currently returns a table of byte values keyed "0".."n-1" that must be converted back to a string. Base64-encode the compressed payload before writing (text-safe). Read docs.wavedash.com/sdk/cloud-saves before implementing; it's the source of truth.
- Pre-spun worlds (default and tutorial) are ordinary save files in `assets/worlds/`, loaded as custom resources.
- Offline progress: on load, `elapsed_hours = (now − saved_timestamp)/3600`, `years = min(elapsed_hours, CAP)`, catch up (04_PLAYER.md §8). Real time is read only in `game/platform/clock.lua`, never in `sim/`.
- Save version mismatch → start a new world with a friendly message.

---

## 9. Performance budgets (HTML5 release build, dev machine)

| Item | Budget |
|---|---|
| Frame | 60 fps at ▶ with the full planet in view; ≥ 30 fps at ⏩ |
| Detailed sim tick | ≤ 4 ms median, ≤ 8 ms p95 (decides SIM_LEVEL) |
| Sim time per frame at ⏩ | ≤ 8 ms |
| Epoch tick | ≤ 8 ms (one per frame ≈ 60 years/s; floor 40 years/s) |
| Blame overhead | ≤ 25% of tick time |
| View updates (terrain/props/puppets) | ≤ 4 ms per frame amortised |
| Props drawn | ≤ 2,500 instances; ≤ 100 draw calls |
| Lua-animated movers | ≤ 300 per frame |
| Bundle size | ≤ 25 MB; first frame ≤ 5 s on broadband |

A debug overlay (F3, or always in `?test=1`) shows fps, sim ms (avg/max), ticks per second, view ms, draw calls if available, prop counts, event count, heap. The bridge (§10) exposes the same numbers to Playwright.

Headless Chromium usually renders WebGL on the CPU (SwiftShader), so **fps in Playwright is not representative**; assert sim ms there, and leave fps judgement to the human playtest. If WebGL fails to initialise headless, try Chromium flags like `--use-angle=swiftshader --enable-unsafe-swiftshader`, or run headed.

---

## 10. Test bridge (game/platform/bridge.lua)

Enabled only when the page URL has `?test=1`.

- **Lua → JS**: every ~0.5 s (and on request) `html5.run("window.__TC = " .. json)` with `{ready, seed, tick, year, speed, fps, sim_ms_avg, sim_ms_max, view_ms, population, villages, events, last_headline, errors, state_hash}`.
- **JS → Lua**: each frame in test mode, pop one command from `window.__TC_CMDS` via `html5.run(...)` and apply it: `new_world{seed}`, `set_speed{n}`, `run_ticks{n}` (chunked fast-forward, then report), `power{type, cell, opts}`, `camera{preset | lat, lon, zoom}`, `select{cell|village|person}`, `open_chain{event}`, `ui{...}`, `save`, `load`, `freeze_visuals{on}` (stop waves/particles for stable screenshots), `selftest` (runs a short scenario in the real HTML5 runtime and reports state hashes to check determinism there).
- Query params: `seed`, `skip_intro=1`, `cam=<preset>`, `speed`, `freeze=1`.
- Lua errors are caught and forwarded into `errors` so Playwright fails loudly.

---

## 11. Build & deploy

- **Pinned engine**: `tools/defold-version.json` holds the stable version and sha1 (read from `http://d.defold.com/stable/info.json`). `npm run setup` downloads `http://d.defold.com/archive/stable/<sha1>/bob/bob.jar` into `tools/bob/`. The human's Defold editor should be the same version.
- **Build**: `java -jar tools/bob/bob.jar --platform js-web --architectures wasm-web --archive --variant <debug|release> --bundle-output dist/web resolve build bundle` (run from the repo root; check where bob places `index.html` inside the output folder and make `serve` point there).
- **Java**: JDK 21 or newer.
- **Wavedash build (as built, D-224)**: no Lua dependency - `window.tcWD` in `game/html/engine_template.html` uses the `window.Wavedash` SDK the platform puts on the page (init when the game is ready, the player's name, `menu.prefs` in Remote Storage); the same bundle runs on itch. Zips are made with bsdtar, never Compress-Archive (D-226). The original plan: add the Wavedash Defold SDK as a project dependency (URL from docs.wavedash.com/engines/defold) and call `wavedash.init()` once the game is ready; the game stays hidden behind Wavedash's loading screen until then. Keep the itch build working without Wavedash (feature-detect at runtime, or build variants with a settings override file; decide and log in DECISIONS.md).
- **itch.io**: zip of the release bundle, "played in the browser", viewport 1280×720 with fullscreen button.
- Bundle `.gitignore`: `dist/`, `out/`, `tools/bob/`, `build/`, `.internal/`, `node_modules/`.

---

## 12. Defold references and gotchas

- LLM-friendly docs index: **https://defold.com/llms.txt** → manuals index `https://defold.com/llms/manuals.md`, API index `https://defold.com/llms/apis.md`, examples `https://defold.com/llms/examples.md`. Read the relevant manual before writing unfamiliar Defold code (meshes and buffers, materials and shaders, render scripts, camera, model components, factories, GUI, input, HTML5, save files).
- Game objects are expensive compared to arrays: pool them, create once, reuse; never create/destroy per frame.
- Every `vmath.vector3()` allocates; reuse vectors in per-frame view code.
- Raise component limits in `game.project` (max instances, models, factories, GUI nodes, particles, sounds) to fit the budgets.
- Message passing is asynchronous; don't rely on same-frame replies.
- Use the Defold build (bob) as the compiler: any Lua syntax error or missing resource shows up there. Always build after changing Defold files.
