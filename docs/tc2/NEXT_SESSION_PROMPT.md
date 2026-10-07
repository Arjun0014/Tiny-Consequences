# TC2 — next session prompt (restore direction after a total loss of context)

You are continuing **Tiny Consequences 2.0**, a Unity 6.6 (6000.6.4f1) Web game in
`C:\Game Dev\tiny-consequences\unity\TinyConsequences2`, beside the legacy Defold game (TC1, untouched,
research material only). Arjun is the director; work autonomously; **no subagents**; do not stop at plans
or documents; research -> design -> decide -> smallest real slice -> compile -> test -> run -> inspect ->
benchmark -> fix -> commit -> update docs -> next slice. Continue until told to stop. Blockers needing a
human (UAC, payment, credentials, licence acceptance) go to `docs/tc2/PROGRESS.md` "Blockers" with a
workaround; keep working on everything else.

**Read in order**: `docs/tc2/NEXT_SESSION_PROMPT.md` (this), `SESSION_HANDOFF.md`, `PROGRESS.md` (CURRENT
STATE), the newest entries of `DECISIONS.md`, then the design doc for the area you touch (`03`
architecture, `04` time/scheduler, `11` history, `12` save/offline, `05`-`10` domains, `13` rendering,
`14` performance, `15` testing, `17` roadmap). Then `git status` and `git log --oneline -15`.

**After a context compaction (mandatory): DO NOT CODE YET.** Re-read this file, `SESSION_HANDOFF.md`, `PROGRESS.md`
CURRENT STATE, the newest `DECISIONS.md` entries and the domain docs in play; run `git status` and
`git log --oneline -20`; check every running process and output file the handoff names (and
`tc2/out/compact/last_precompact.md`); reconstruct the exact step; then continue autonomously. The repo's hooks
(`.claude/settings.json` -> `.claude/hooks/tc2_compact.mjs`; Claude Code 2.1.289: `PreCompact` stdout joins the
compaction instructions, `SessionStart` matcher `compact` stdout is shown to Claude) inject this automatically; the
root `CLAUDE.md` carries the same protocol in case they fail. Before a compaction is likely, checkpoint: commit, then
DECISIONS, PROGRESS, SESSION_HANDOFF, this prompt, measurements and the exact next step.

**The end goal**: a beautiful miniature planet that lives on its own for thousands of years (geology,
climate, ecology, evolution, persistent people, settlements, knowledge, conflict, polities, collapse,
ruins); zoom from orbit to one person; accelerate; leave and come back to the same world aged by the
offline mode (a day away ~ a century in Living mode), simulated for real; a History Book where every
story happened and every event links to its causes (NATURE / PEOPLE / YOU). No scripted consequences,
no unlock ladders: world rules, not stories.

**Settled architecture** (do not reopen without new evidence): pure C# 9 core `TC2.Core` (no UnityEngine;
also compiled by `tc2/headless` with the editor's bundled dotnet), Burst kernels only where measured, no
Entities (not on the Web); one ordered event queue `(time, phase, seq)` with lazy cancellation, serialized
exactly; int64 seconds, 24 h days, 48-day years; resolution layers PROCESS / ACTIVITY / COARSE /
ELABORATION - watching never changes history; equiangular cubed sphere N32/N64, R = 6 km (D2-085: the ground in geo-lengths of a 10 km sphere, living laws per hectare); causal history
ledger with tiers, roots, importance, compaction; exact binary saves with an IndexedDB store; offline modes
Gentle/Living/Ancient/Deep time; URP with WebGPU first and WebGL2 fallback, instancing without BRG;
determinism bit-identical across dotnet / Mono / Web IL2CPP (DMath, keyed RNG, double arithmetic, ordered
iteration).

**Commands** (see `SESSION_HANDOFF.md` for the current list): headless tests and runs via
`node tc2/scripts/headless.mjs ...`; Unity batch via `node tc2/scripts/unity.mjs ...`; Web build, serve
(COOP/COEP) and Playwright via `tc2/scripts/*.mjs`. One heavy process at a time (11 GB RAM).

**Direction from Arjun (5 Oct 2026)**: the world must be a *really beautiful miniature* with a high level of detail
and high performance - look-dev is a first-class track beside the simulation (D2-026). Judge every visual change in
real game screenshots (`node tc2/scripts/unity.mjs shots`), not in debug maps; keep 60 fps in the Web build.
Headless Blender (`tc2/art/kit.py`) makes the asset kit. The god powers that exist (eruption, quake, storm, lightning)
are logged commands and double as screenshot tools (`unity.mjs shots --erupt/--quake/--storm`).

**Direction from Arjun (6 Oct 2026, D2-079)**: "the entire world looks so empty and boring" - the planet's plants and animals are
simulated but barely drawn. Make the living world visible (forests at their true density, woodland seen from afar, ground cover by
biome, animals readable), read from the simulation's state and judged in real screenshots, before F8.

**Where we are (session 2, 6 Oct 2026)**: F1 to F7 are closed (D2-043, D2-046, D2-060, D2-065, D2-068, D2-076, D2-078).
Villages of persistent people live on their own, carry their technology as knowledge, fall sick and recover, trade and send
relief, and - when scarcity bites - take from each other at night, strike and are struck, ask amends, take back, raise bands,
raid, wall themselves in palisades and bow to stronger neighbours as tributaries; every one of these is a deed known per village
and every event traces to its causes. The History Book (D2-077) reads the world's ages after the fact from its annals - chapters,
places and lives whose every line is a record with its causes. Determinism holds across dotnet, Mono and the Web build and is
linted (D2-057). Next (SESSION_HANDOFF "Next steps"): the living world made visible (D2-079), then F8 - deep time. Run long
headless jobs from a scratch copy of the binaries so the repo's DLL stays free; another Claude session may share the machine -
never kill its processes, and measure only when it is quiet.

**Where we are (session 3, 6 Oct 2026, evening)**: the living world made visible (D2-080-D2-082: woods drawn from the simulation,
the far woods as masses, ground cover by biome, villages' signs; the Web build at 65-69 fps near the ground). F8 started: DEEP (04
§4.3b, calm villages live up to twelve days at a step) and its calibration against COARSE (D2-083, in progress): six rules of "what
a step must keep of its days" fixed every bias found; COARSE hashes unchanged; the final multi-seed run is pending (PROGRESS
"Blockers"). Then: DEEP's early growth on small worlds, speed (per-step costs, the planet), Ancient and Deep time in the browser.

**Direction from Arjun (7 Oct 2026, D2-084) - the current priority**: "the simulation exists, but almost none of it is legible from
looking at the world." Make the world legible from the picture at the orbit, region, village and close views, and let it carry its
history (`docs/tc2/18_WORLD_READABILITY.md`, roadmap phase W): audit what the simulation knows against what is drawn, then landscape
composition, forests, rivers and river corridors, biomes and ground cover, terrain and geology, settlements that read as places (and
whether founding fragments people too much - fixed in the simulation if so), wildlife and activity, persistent scars, orbit-scale
legibility, a multi-age acceptance (the same seed at years 0, 25, 100, 300, 1,000: "can I see history without reading the date?"),
browser performance at 60 fps. **No simulation theatre**: presentation may elaborate and exaggerate real state, never invent it; add the
smallest real state where a meaningful difference cannot be drawn. Judge across views and ages, never one cherry-picked angle. The UI
gets a full art pass later (no debug panels at ship). Invisible simulation features wait until this is convincing.

**Where we are (session 4, 7 Oct 2026)**: D2-083 (DEEP calibration) closed - six seeds at par in people and per-person rates, residual
leans in the wild choice recorded. Visual slice 1 of D2-084 in progress (stands and glades, palette, relief, rivers, tree kinds). Machine
rule: total CPU and RAM ~50 %, one heavy job at a time, low priority, never kill processes you did not start (the classifier also refuses
to stop this session's own leftover runs - let them finish).

**Direction from Arjun (7 Oct 2026, D2-085) - before D2-084's polish**: reconsider the planet's physical size. D2-007's R = 10 km was
chosen before any playtest; the world feels enormous and empty. Study TC1's real scale, test smaller TC2 planets (N64 kept) really
simulated - population, interaction density, travel, performance, comparable shots - decide the radius and change it now if the evidence
supports it (old saves may be invalidated), then redo D2-084 on the chosen scale. Fewer people is fine (2,000-8,000 mature), more detail
per person, settlement and feature; never a world where everyone reaches everyone. The study: `docs/tc2/19_PLANET_SCALE.md`.

**Where we are (session 5, 7 Oct 2026)**: D2-083 closed (22cad8d). **D2-085 made: the planet is 6 km** - the same ground at any size
(geophysics in geo-lengths of a 10 km sphere), living laws per hectare and real-size patches, the view relative to R; a village with its
fields is 0.17 of the globe (TC1's 0.16); mature worlds of 2,000-7,100 people in 10-30 villages; 177 tests and the Mono probe at 6 km.
Next: D2-084 on the 6 km world (SESSION_HANDOFF "Next").

**Rules**: commit prefix `[tc2]`; never commit Library/Temp/Logs/builds/secrets; never damage the legacy
project; record decisions (D2-NNN) and measurements; update PROGRESS, SESSION_HANDOFF and this prompt at
milestones. After a context compaction: stop, re-read the files above, reconstruct state, continue.
