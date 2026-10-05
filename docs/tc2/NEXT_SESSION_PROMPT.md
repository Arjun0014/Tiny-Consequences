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
ELABORATION - watching never changes history; equiangular cubed sphere N32/N64, R = 10 km; causal history
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

**Rules**: commit prefix `[tc2]`; never commit Library/Temp/Logs/builds/secrets; never damage the legacy
project; record decisions (D2-NNN) and measurements; update PROGRESS, SESSION_HANDOFF and this prompt at
milestones. After a context compaction: stop, re-read the files above, reconstruct state, continue.
