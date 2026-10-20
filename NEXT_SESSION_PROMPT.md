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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 24)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 24"**: the exact state, the RUNNING job queue (where it is, what is left in it), what the evidence
   says, "Next, in order".
3. The drafts to land, each with its FILL marks (what the queue still owes): `tc2/out/tools/s23/d2164.md` (C10.7a), `d2165.md` (C10.7b), `d2166.md` (C9.5c),
   `tc2/out/tools/s24/d2167.md` (the realms map layer), `d2168.md` (C10.8 the asking); the next slice's design and measurements
   `tc2/out/tools/s24/c108_design.md`; the PROGRESS draft for the landing `tc2/out/tools/s24/progress_s24.md`.
4. `docs/tc2/DECISIONS.md` newest (D2-163), `docs/tc2/PROGRESS.md` CURRENT STATE (the session 24 entry first), `docs/tc2/10_CONFLICT_AND_POLITICS.md`
   §12i-§12k **on the branches** (`C:/Game Dev/tc-int2` has §12i-j; `C:/Game Dev/tc-demand` has §12k).
5. `git status`, `git log --oneline -12`, `git worktree list`, and the queue: `cat $Q/runq.log` (path below) - which jobs ended, with what.

## Session 24 (20 Oct 21:56 - 21 Oct ~01:25 IST) - context handoff; the job queue is RUNNING
- **The queue**: `bash tc2/out/tools/s23/runq.sh`, background task **bcbn62z2b** (started 22:04 in session 24; it keeps running across the context clear).
  `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`; binaries in its parent
  (`bin_i2` the landing branch, `bin_s2` C10.7b, `bin_c1` C9.5c, `bin_dm1` C10.8, `bin_lt` the tools branch, `bin_lm` the measure-only tools; `bin_a9`,
  `bin_m2` to be rebuilt by queued jobs). Left at 01:25 (~3 h): the integrated rules' seeds 2 and 4 and their 900 years with the lordship measures
  (`4_e_*`), C9.5c's third seed set (`4_f_*`, `4_g`), C10.7a's 900 years (`5_*`), C10.7b's 900 years and the profile (`6_*`), the integrated rules' other
  eight seeds and C10.8's control and ten seeds (`7_*`), D2-161's attribution (`9_*`). A monitor (task b2hef5r43) reports failed jobs; re-arm one if needed.
- **On main (cd4f054 + the handoff docs; sim 87)**: the realms map layer (merges of `wip/realms`, presentation only) - judged in two rounds of real shots
  (`tc2/out/shots/s24r3`, `s24r8`, then `s24s3`, `s24s8`); its Web frame cost waits for the landing's Web build.
- **Verified, not landed**: the landing branch `wip/s23b-int` (C10.7a + C10.7b + C9.5c, sim 90): full suite 441/441, 0 skipped; C10.7b's ten seeds at par
  in the per-person rates (its people dip inside the noise against a matched placebo twin; more war); C9.5c's hunger lean explained by a matched placebo -
  but a replication leaned in people (x0.96): **the third seed set decides** (`python tc2/out/tools/s24/lean_pool.py` pools all three).
- **Built on a branch**: C10.8 the asking (`wip/c108-demand`, `C:/Game Dev/tc-demand`, from the landing branch; sim 91; DemandTests 4/4) - chosen by
  measuring first (bonds hold by strength, so succession changes little; formation limits lordships: 514 pairs where one could make the other bow, 26 bonds).

## Do next, in order
1. Read the queue's results as they land and fill the drafts (SESSION_HANDOFF "Session 24", "Next, in order", step 1).
2. Land: `git merge --no-ff wip/s23b-int`, then `git merge --no-ff wip/s24-tools`; FILLs; `python tc2/out/tools/s24/append_decisions.py` (D2-164-D2-167);
   PROGRESS, `25`, `18` §9, `10` §12h; queue `tc2/out/tools/s24/0_a_land_checks.sh`, `0_b_fullsuite_m5.sh`, `0_c_shots_m5.sh`, `0_d_web.sh`; judge the shots
   (sworn men at practice; realms on a v90 world) and the Web test (`s24web1`: realms frame cost). If C9.5c's lean holds in the third set, land C10.7a and
   C10.7b without it (SESSION_HANDOFF step 2) and investigate first.
3. C10.8 the asking on the landed main: its ten seeds against `_i2` (queued), a placebo if a lean needs one, its 900 years (does the world partition into
   realms, how fast, how violently?), real shots of realms growing (`--realms` on a `_dm1` world), full suite, D2-168, Web. Then by evidence: commendation
   (24 wronged villages could put themselves under a protector), a lord's death once realms are held by the margin, rising together once realms are large.

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push; push only when asked
  (as Arjun0014, per the global CLAUDE.md).
- **Resource rule (Arjun D-1/D-2)**: one low-priority heavy job at a time while RAM < 86 % and CPU < 85 % - the job queue (`tc2/out/tools/s23/runq.sh`,
  `lib.sh`: `gate`, `low`, `world`); never kill processes you did not start (the user's Chrome and other Claude sessions share the machine). A queued
  build compiles the worktree's working tree: do not edit a worktree between its job's start and the copy of its binary.
- **The scratchpad can vanish between sessions** (session 24 found session 23's gone): `S`/`N` in `tc2/out/tools/s23/lib.sh` and `Q` in `runq.sh` are
  hard-coded - point them at the live scratchpad, rebuild each binary from its worktree (the build lines at the top of the rebuild jobs) and check each
  against one of its own earlier runs (annals and state hash) before using it. The saves the jobs read live in `tc2/out/civ` (ignored, persistent).
- Develop in a worktree per slice (`git worktree add -b wip/<slice> "C:/Game Dev/tc-<slice>" main`); keep the main checkout on main for the Unity editor
  jobs (`unity.mjs cc`, shots, the Web build run in the main checkout). New test files need a `.meta` (fileFormatVersion 2, a new guid).
- Core is pure C#, deterministic, no float arithmetic (floatcheck). A derived cache must give the same answer whenever it is filled (`03` §4). Judge
  simulation changes by ten seeds against a control on the same binary (a `--old...` switch reproducing the previous runs byte-identically - annals AND
  the state hash); **wall-clock time is not evidence** (a placebo on one binary ran x1.27 slower, t 5.5).
- **A lean past |t| 2 is judged against a matched placebo**: the old rules with `--nudge Y` (one FU in the first village's store) at the year each world
  first parts from its twin under the new rule (`tc2/out/tools/s24/lean.py A B seeds...` prints that year, per seed), then the new rule against its
  placebo twin. Twins that part late spread little; twins that part early spread widely (one FU at year 30 made a world found 23 villages instead of 4).
  Among sixty measures one or two pass |t| 2 by chance (a placebo did). Then replicate on new seeds (9-19, 20-29).
- **A test premise that changes the ground by hand** (sinks or raises cells outside the plates' turn) leaves the running world's waters drained for the old
  ground: **load it once** (`SaveFile.Load(SaveFile.Save(w, ...))`) before testing anything a save must continue (D2-162's finding).
- The queue's test gate requires `0 skipped` (an `Assume` that fails is not a pass). `--filter CrossTests` also runs AcrossTests (substring match).
  Test premises that set state directly must also tell it as the game would (C10.8's first test run: men sworn without a `Sworn` event left the asking
  causeless) and give a village a real harvest (a young test world's store is small).
- Bash heredocs pass through a layer that collapses `\\`: write patch scripts or text with backslashes with the Write tool.
- Saves carry a sim version; a save loads only in a binary of the same version (main is 87; the landing branch 90; C10.8 91; `_y1` saves 87, `_i2` 90).
- Test worlds (`TestWorlds.Make`) have not lived their first ticks: a premise that sinks or raises ground must `AdvanceTo(w.Now + SimTime.Hour)` first.
- The headless build does not compile Game code (`Game/`, the view, `ShotRunner`): after any Game edit run `node tc2/scripts/unity.mjs cc` (or `cc --src
  "C:/Game Dev/tc-<slice>/unity/TinyConsequences2"` for a branch, from the main checkout). The shader compiles only in the editor (the shots' log).
- Unity's batch editor logs an `ArgumentOutOfRangeException` from its own search indexer at start-up (`UnityEditor.Search.SearchDatabase`) in every shots
  run - not our code.
- On the 6 km planet 5 km is ~48 degrees of longitude: villages a day's walk apart keep local times ~3 hours apart - shoot a village's activity in its own
  hours.
- `Commands.cs`'s report is one long method: give a new tally's local a long, unique name (CS0128/CS0136), and build the headless app after merging tools.
  A tool copied from one branch to another can bring functions that need that branch's API (the measure-only copy on main broke on `MightNow`).
- Run tags in `tc2/out/civ` are reused across sessions (`k1`, `w1`, `v3`, `g2`, `g3`, `t1` held older runs): check `ls tc2/out/civ | grep _TAG` first.
- To shoot something rare, find it first: `voyages --load SAVE --days N` (and probes like it) live the save as the game loads it - the same world, the
  same minute - then `unity.mjs shots --village V --days D --hour H` frames it (D2-163). The realms layer: `unity.mjs shots --realms --village lord`.
- The queue runner exits while `$Q/stop` exists: delete it before restarting `runq.sh`; jobs run in name order (digits before `_` before letters), so a job
  named `0_...` runs next.
