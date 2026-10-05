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

**Where we are (session 2, 6 Oct 2026)**: F1 to F5 are closed (D2-043, D2-046, D2-060, D2-065, D2-068). Villages of persistent
people live on their own (wild food, building, moving and founding, herds and livestock, the household's own work, warmth, homes
that outlive households), carry their technology as knowledge (nets, the loom, the rotary quern, pottery, the ard, copper), fall
sick with sicknesses that come from their animals and travel with visitors, brides and traders, and trade what they spare for
what they lack (copper from the ore villages; grain to a neighbour in famine as relief). F6 (society, conflict and polities)
has begun: deeds known per village, the hungry taking grain from a neighbour's store in the night, grudges that stop trade and
relief (D2-069); the watch, blows and injury (D2-071); sickness holds at scale (D2-070). Determinism holds across dotnet, Mono and the Web build and is linted (D2-057). Next (SESSION_HANDOFF "Next
steps"): F6's next slices (answers and news; raids; defence; leaders; polities). Run long headless jobs from a scratch copy of the binaries so the repo's DLL stays free.

**Rules**: commit prefix `[tc2]`; never commit Library/Temp/Logs/builds/secrets; never damage the legacy
project; record decisions (D2-NNN) and measurements; update PROGRESS, SESSION_HANDOFF and this prompt at
milestones. After a context compaction: stop, re-read the files above, reconstruct state, continue.
