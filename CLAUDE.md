# CLAUDE.md — Tiny Consequences

You are the sole engineer on **Tiny Consequences**, a Defold game for the **Defold Game Jam 2026** (theme: **Chain Reaction**). Arjun is the director; he playtests only at chapter boundaries. You build everything else autonomously.

**Deadline**: jam closes 5 Oct 2026, 03:29 IST. Internal submission target: **4 Oct, 20:00 IST**. Feature freeze: **3 Oct, 22:00 IST**. All times IST (UTC+5:30); run `date` to check.

## What the game is (one paragraph)

A cute tiny planet that lives on its own: weather, oceans, forests, animals, farms and individual villagers. The player is a god who nudges it (lightning, drought, culling wolves, sinking a tanker, meteors, volcanoes) and speeds up time to watch consequences cascade over years to millennia. Every major change is traceable through a visible chain of causes ending at **Nature** or **You**. Full vision: `docs/01_VISION.md`.

## Direction reset (D-050, 27 Sep 2026)

Arjun reset priorities: **build the genuinely living tiny world first**; the jams (Defold Game Jam 2026 and Game Gauntlet Sim Jam, themes Chain Reaction + Simulation) are secondary and a missed deadline is acceptable. The world-simulation spec is `docs/world/` (00 research, 01 audit, 10–16 design, 17 roadmap v2). It supersedes 02 §1/§5.10/§5.11 and 08 from Stage 2.3 on. Commit prefix for this work: `[wN]` (phase). No subagents unless Arjun allows them again.

## Direction reset v3 (D-078, 1 Oct 2026)

Arjun reset again: v2 people moved as scheduled groups. The target is an automated miniature
civilisation of persistent agents (small settlements that farm, cut wood, build, raise children,
trade, migrate and fight on their own); the visible action must be the simulation. Since session 10 (D-079)
**v3 is the default game and work happens on main** (the v3 branch is merged): spec `docs/v3/`
(00 audit, 10 agents, 20 character pipeline, 30 roadmap v3), code `sim/agents/` and
`game/view/agents_view.lua`. The legacy v2 world opens with `?world=v2` and stays until v3 reaches
parity. Commit prefix `[v3]`. Session 11 (D-085-D-090): `sim/surface.lua` is the one coast/river
function for the view and the sim; fishing, textiles and sheep are live systems. Session 12
(D-091-D-097): `sim/agents/stock.lua` (herds of sheep, goats, cattle, pigs), `wild.lua` (the W4
herds and packs as individuals on the maps; wolves, hunting), `trade.lua` (trade walked between
settlements; `p.at` is the map a person is on); measure the browser with `scripts/prof_web.mjs`
(the lower envelope of several release runs). Session 13 (D-100-D-109): `sim/agents/econ.lua` (worth,
news, exchanges, gifts, relief), `dogs.lua`, sea trade in `trade.lua`/`think.lua` (`sea_legs`);
watch the economy with `tools/v3_econ.lua`; trade is tested by creating real want, never expected.
Session 14 (D-110-D-113): the browser must live the same world as LuaJIT - `sim/dmath.lua` for every
transcendental in `sim/` (no `^`), `sim/fmt.lua` (`sfmt`) for text kept in state, check with
`scripts/v3_hash_web.mjs` (web test W10); drainage basins in `sim/geo.lua` (v3 worlds); birds are
agents in `sim/agents/birds.lua`; watch with `tools/v3_watch.lua`, `v3_follow.lua`, `v3_birds.lua`.
Sessions 15-16 (D-114-D-121; session 15 died before its handoff, `last_session.md`): measure the
browser's sim cost with probes that draw nothing (`render_off`, prof_web's default: headless
Chromium's CPU rasteriser competes with the sim) and compare LuaJIT instruction counts
(`luajit -joff tools/v3_profile.lua --days 4`); animals are individuals with no places in a group -
audit synchronisation with `tools/v3_animals.lua` (vcorr, mass starts/stops/rests, offsets),
`v3_pack.lua` (frame by frame), `v3_tracks.lua` (tracks from above), test `animals_are_individuals`;
every animal is clickable with its why; households migrate physically (`sim/agents/migrate.lua`),
brides walk to their husband's village, relief sails. **Bob forbids circular requires even inside
functions** (LuaJIT tests pass, the build fails): inject modules through `sim/agents/init.lua` and run
`npm run build` after adding a `require` between agent modules.

## Read order

1. This file.
2. `docs/PROGRESS.md` — where we are. **Always read first in a new session.** Also glance at `HUMAN_INPUTS.md` (values only Arjun can provide).
3. Any `docs/playtests/chapter-N-feedback.md` not yet marked as read in PROGRESS.md — turn it into tasks before continuing.
4. `docs/world/17_ROADMAP.md` (roadmap v2, current phase) and `docs/08_ROADMAP.md` for the later chapters.
4b. `docs/world/10_WORLD_SYSTEMS.md` and the companion docs for the phase you are in.
5. The design docs relevant to the stage: `02_WORLD_MODEL.md` (sim), `03_CAUSALITY.md` (blame/events/chains), `04_PLAYER.md` (powers/UI/saving), `05_ARCHITECTURE.md` (code structure, rules, build), `06_PRESENTATION.md` (look/sound/writing), `07_TESTING.md` (tests).

In the very first session, read all of `docs/` completely.

## Autonomy rules

- Work through the **whole current chapter** without stopping. Make decisions yourself; record anything that changes design, scope, budgets or architecture in `docs/DECISIONS.md`.
- **Stop only when**:
  1. the chapter's playtest report is written (`docs/playtests/chapter-N.md`), or
  2. a human-only action blocks *all* remaining work (credentials, a system install needing admin rights, an account). Write exact instructions in PROGRESS.md "Blockers", do everything else first, then stop, or
  3. continuing would cut something from the roadmap's "Never cut" list or move the submission target.
- When stuck on the same problem for ~45 minutes: write it down in PROGRESS.md, try a simpler approach, or apply the roadmap cut order.
- Check the clock at every stage boundary against `docs/08_ROADMAP.md` targets. If behind, apply the cut order and log it.

## Session start checklist

1. `date` (IST), `git status`, `git log --oneline -5`.
2. Environment: `node --version` (≥ 20), `java -version` (≥ 21), `luajit -v`, `git --version`. If `tools/bob/bob.jar` is missing, run `npm run setup` (once it exists).
3. Read PROGRESS.md; confirm the current stage; continue.

## The stage loop

For each stage in the roadmap:

1. Write a 3–8 line plan under "Current state" in PROGRESS.md.
2. Implement in small steps. Build and test often:
   - sim work → `npm run test:sim` (includes `lint:lua`; and `npm run sim -- --seed N --years Y --map` to look at outcomes and maps)
   - Defold work → `npm run build` (bob is your compiler), then `npm run test:web`
   - visual work → `npm run shots`, then **open the PNGs and check them** against `docs/06_PRESENTATION.md` §6
3. Fix until the stage's **Done when** criteria pass.
4. Append a stage entry to PROGRESS.md (what, tests, numbers, issues), update "Current state".
5. Commit: `git add -A && git commit -m "[chX.sY] <summary>"`.

A stage is not done if the build is broken, a required test fails, or PROGRESS.md is stale.

## Hard rules

**Simulation**
- `sim/` and `content/` are **pure Lua 5.1**: no Defold APIs, no LuaJIT-only features (HTML5 runs Lua 5.1.4). They must run headless under LuaJIT from the repo root. LuaJIT accepts things Lua 5.1 rejects or computes differently (e.g. `math.log(x, 2)`), so keep `npm run lint:lua` clean (05_ARCHITECTURE.md §4).
- **Deterministic**: all randomness via `sim/rng.lua` (counter-based, keyed by stream/cell/tick). Never `math.random`, `os.time`, `os.clock`, or `pairs()`-order-dependent logic in `sim/`.
- **No scripted consequences.** Chains must emerge from field couplings (`02_WORLD_MODEL.md` §0). Never make a scenario pass by special-casing its outcome.
- Every tunable lives in `sim/config.lua`. Tune freely; changing a *tuning band* or *test threshold* needs a DECISIONS.md entry.
- Every system that writes a causal field reports blame terms (`03_CAUSALITY.md` §3). Build blame together with the system.
- Adding a per-cell field needs a DECISIONS.md entry (the field budget is deliberate).
- Zero allocations in per-tick hot paths; struct-of-arrays; sparse sets for rare things.

**Game/view**
- The view never writes sim state. All player actions are commands, applied at the next tick and logged.
- Pool game objects; never create/destroy per frame; respect the budgets in `05_ARCHITECTURE.md` §9.
- Before using an unfamiliar Defold API, read the docs: **https://defold.com/llms.txt** (manuals index `https://defold.com/llms/manuals.md`, API index `https://defold.com/llms/apis.md`, examples `https://defold.com/llms/examples.md`). Don't guess APIs; confirm by building.
- Wavedash: `docs.wavedash.com/engines/defold` and `docs.wavedash.com/sdk/cloud-saves` are the source of truth. `wavedash.init()` must be called in the Wavedash build or the game stays hidden.

**Assets & legal**
- **Never copy code or assets from Tiny Skies** (no licence). Visual reference only.
- Only use assets you generate, or ones with clear permissive licences (CC0, OFL, MIT). Record every third-party asset and its licence in `assets/CREDITS.md`.
- Arjun may drop assets (e.g. a music track) into `assets_inbox/`. Check it during polish stages; ask about the licence in the playtest report if it's unclear.
- No runtime network calls except the Wavedash SDK. No analytics.

**Docs**
- Docs 01–08 are the spec. If the design must change, update the doc *and* add a DECISIONS.md entry. Don't let code and docs silently diverge.

**Git**
- Commit at least at every stage end, message prefix `[chX.sY]`. Never force-push or rewrite history. Never commit secrets, `dist/`, `out/`, `tools/bob/`, `node_modules/`.

## Commands (see `05_ARCHITECTURE.md` §3)

`npm run setup` · `npm run sim -- --seed 42 --years 100 --map` · `npm run lint:lua` · `npm run test:sim [-- --full]` · `npm run bench` · `npm run build` · `npm run build:release` · `npm run build:wavedash` · `npm run serve` · `npm run test:web` · `npm run shots` · `npm run play` · `npm run package:itch` · `npm run deploy:wavedash`

You create these scripts in Stage 1.1 and keep them working.

## Working efficiently

- Keep context lean: grep instead of reading huge files; tail logs; write long outputs to `out/` and read summaries.
- Use subagents (if available) for side work that doesn't need the main thread's context: reading Defold manuals for a specific API, drafting dialogue batches in the documented voice, reviewing a batch of screenshots or event chains with fresh eyes.
- Prefer small, verifiable steps over big rewrites. Build/test after each meaningful change.
- PROGRESS.md must always let a brand-new session resume without guessing.

## Playtest report (end of chapter)

Write `docs/playtests/chapter-N.md` using the format in `docs/playtests/README.md`: build hash, how to run (`npm run play`), what's new, what to try (10–20 minutes), 5–8 specific questions, known issues, key numbers, interesting screenshots. Commit. Then stop and tell Arjun in ≤ 5 lines what to test.
