# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism, not a stop condition. Stop only for a real external blocker
(credentials, payment, UAC, an irreversible product decision). **No subagents.** Simulation and presentation advance together: every slice
answers "what changes in the world picture?" and is judged in real game shots.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 23)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 23"**: exact state, the running job queue, "Verified", "Next, in order".
3. `docs/tc2/DECISIONS.md` newest (D2-157, D2-158) and the drafts `tc2/out/tools/s23/d2159.md` (C10.5), `d2160.md` (D2-160), `d2161.md` (C10.6).
4. On the integration branch (`C:/Game Dev/tc-int`, `wip/s23-int`): `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12g (a lordship's might), §12h (tribute
   carried); `docs/tc2/03_ARCHITECTURE.md` §4 (derived caches); `docs/tc2/25_CIVILISATION_ROADMAP.md` "C10 governance, by slices".
5. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 23 entry first).
6. `git status`, `git log --oneline -10`, `git worktree list`, and the queue: `cat $Q/runq.log` (path below).

## Session 23 (19 Oct 2026, 18:54-~21:25 IST) - context handoff; the job queue is still running
- **On main**: C10.4 bread and dependence merged (ca3b4c8) with D2-157 and D2-158; validated - Unity C# compiles, Web build and test 6/6, the browser's
  probe = headless dotnet = the editor's Mono, bit for bit (frame rates were halved by the user's Chrome running during the test: re-measure quietly).
- **The 900-year check of C10.4** (seeds 2 and 4): no snowball and no growth - petty lordships of 1-4 villages; every one of 63 ended bonds ended because
  "the lord was no longer the stronger", weighed by the fit people at home on one day and a lord by its own village. Real shots `tc2/out/shots/s23a`,
  `s23b`: the bond reads only up close.
- **Built, merged together on `wip/s23-int` (`C:/Game Dev/tc-int`, tip c5258d2), NOT yet on main**:
  - **C10.5 a lordship's might** (sim 83): villages weigh each other by their people of fighting age, a lord with the men its call brings. Control
    `--oldmight` byte-identical; ten seeds at par; its 900-year check is in the queue.
  - **D2-160 the ways' woods** (sim 84) - VERIFIED: a route's forest cost read the woods of the moment it was first built, so a load or the view building
    routes first changed history (three lived days: different hashes); routes now read the woods at the last turn of the plates - equal hashes with and
    without the view's routes over 3 and 6 lived days and 3 COARSE years; control byte-identical; ten seeds at par.
  - **C10.6 tribute carried** (sim 85): the tenth set aside at the tributary's harvest and carried to the lord's store by its people (lived walkers and
    carts, COARSE the same loads), wearing the ways that converge on the lord; standards enlarged with distance up to 4x. TributeCarriedTests 5/5; its
    control and ten seeds are in the queue.
- **RUNNING: the job queue** `bash tc2/out/tools/s23/runq.sh` (background task bc2qpqxfq). `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q` (`runq.log`, `log/<job>.log`,
  `todo`, `done`; `touch $Q/stop` ends it after the current job). Remaining at the handoff (about two hours): `3_d_l9z1_s2/s4` (C10.5's 900-year check),
  `3_t_*` (C10.6's evaluation), `4_a_fullsuite_i1` (the full suite on the integration), `4_b_f1_*` (its ten seeds), `4_c_cmp_probe_f1`. Results land in
  `tc2/out/tools/s23/results/` and `tc2/out/civ/`. If the scratchpad is gone, rebuild from the worktrees (SESSION_HANDOFF says how).

## Do next, in order
1. Read the queue's results: C10.5's 900-year check (`results/lordships_s*_l9z1_y600.txt` - do lordships hold, grow plausibly, or snowball? a snowball
   needs a limit grounded in the record before landing), C10.6's control and `cmp_z1_t1.txt`, the integration's `fullsuite_i1.txt`, `cmp_t1_f1.txt` and
   the probe's hashes on `s2_f1`.
2. Land: write D2-159, D2-160, D2-161 into DECISIONS from the drafts (fill their FILL marks); `git merge --no-ff wip/s23-int` on main; `node
   tc2/scripts/unity.mjs cc`; PROGRESS.
3. A v85 seed-2 world (`run --seed 2 --years 300 ... --snap 300` with main's binary) for real shots of the tributary and the lord (`unity.mjs shots
   --village tributary|lord`, views `standard,standardlow,village,region`; carriers after the tributary's harvest via `--doy`): do the standards and the
   converging roads read now? Then the Web build and test (`tc2/out/tools/s23/1_a_web.sh`, a new tag) on a quiet machine.
4. Then the next slice by evidence: the lord's household men (C10.7, `23` §3 - `lordships` measures each lord's surplus) or C9.5 boats across the water
   (needs a lived "legs by boat" model).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push; push only when asked
  (as Arjun0014, per the global CLAUDE.md).
- **Resource rule (Arjun D-1/D-2)**: one low-priority heavy job at a time while RAM < 86 % and CPU < 85 % - the job queue (`tc2/out/tools/s23/runq.sh`,
  `lib.sh`: `gate`, `low`, `world`); never kill processes you did not start (the user's Chrome and other Claude sessions share the machine).
- Develop in a worktree per slice (`git worktree add -b wip/<slice> "C:/Game Dev/tc-<slice>" main`); keep the main checkout on main for the Unity editor
  jobs (`unity.mjs cc`, shots, the Web build run in the main checkout). New test files need a `.meta` (fileFormatVersion 2, a new guid).
- Core is pure C#, deterministic, no float arithmetic (floatcheck). A derived cache must give the same answer whenever it is filled (`03` §4; probe
  `run --load SAVE --warmroutes`). Judge simulation changes by ten seeds against a control on the same binary (a `--old...` switch reproducing the
  previous runs byte-identically); wall-clock time on this shared machine is not evidence.
- Bash heredocs pass through a layer that collapses `\\`: write patch scripts or text with backslashes with the Write tool.
- Saves carry a sim version; a save loads only in a binary of the same version (C10.6's branch alone is also 85 - read `_t1` saves with `bin_t7` only).
