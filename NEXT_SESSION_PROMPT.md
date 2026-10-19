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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 23, fourth context)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 23, fourth context"**: exact state, the running job queue, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest: D2-163 (C9.5b, its checks now complete). The three drafts to land: `tc2/out/tools/s23/d2164.md` (C10.7a),
   `d2165.md` (C10.7b, the personal oath), `d2166.md` (C9.5c) - their FILL marks are what the queue still owes.
4. Design: `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12i-§12j and `24_TRANSPORT_AND_MARITIME.md` §5 **on the landing branch** (`C:/Game Dev/tc-int2`);
   `tc2/out/tools/s23/c107_design.md` (C10.7b's design, last section), `c95c_design.md` (C9.5c's survey).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 23, fourth context entry first).
6. `git status`, `git log --oneline -12`, `git worktree list`, and the queue: `cat $Q/runq.log` (path below).

## Session 23, fourth context (20 Oct 2026, 02:54-05:10 IST) - three slices in evaluation; the job queue is running
- **On main (0947e14 + this handoff; sim 87)**: C9.5b's checks complete (Web `s23web4` 6/6, three runtimes bit-identical; aimed real shots `s23n1`,
  `s23n2` show dugouts crossing at 45 m; lived and COARSE crossings agree). Tools merged: `voyages --load SAVE --days N [--dugouts]` (a save lived as the
  game loads it, every voyage logged with its village's day and hour - find a rare thing, then shoot it), voyage views `dugout`/`voyageclose`,
  `--visittally`. Main's code since bc99679 is tools-only (its four survey worlds reproduce `_y1` byte for byte); the full suite last ran on bc99679.
- **Built, tested, in evaluation - NOT on main**:
  - **C10.7a** might by lasting strength (`wip/c107a-arms`, `C:/Game Dev/tc-arms`, sim 88): control = `_y1`; **ten seeds `_a7` at par** (people x1.018;
    tribute agreed x1.20 t 1.78); 900-year check queued (`6_c_*`).
  - **C10.7b** sworn men (`wip/c107b-sworn`, `C:/Game Dev/tc-sworn`, tip 3e570e3, sim 89): **the oath is to the person** first among the people
    (1557053; the household-id first version churned - seed 4 released 2,956 men as "gone"). Eight neighbour classes pass on `bin_s2`; SwornTests
    (wording fixed) retest `6_9_d2` sets `s2_ok`; then control `_sx0`, ten seeds `_sx1`, `results/cmp_a7_sx1.txt`, 900 years `_l9sx1`, a profile.
    **Open question**: seed 4 had sworn men in 9 of 29 villages at year 300, only 2 of them lords (any first household with grain keeps men) - the
    seeds and the 900-year check decide whether that stands or a lordship/size condition is wanted.
  - **C9.5c** people across water (`wip/c95c-visits`, `C:/Game Dev/tc-visits`, tip 8a270c4): COARSE visits only where a way on foot leads, keeping a
    lived day's odds (VisitsTests: along a way 9.6 as before, across water 0); control `_pv0`, ten seeds `_pv1`, `results/cmp_y1_pv1.txt` queued.
  - **The landing branch** `wip/s23b-int` (`C:/Game Dev/tc-int2`, tip bb2ec81): main + all three, sim 90, docs; its full suite `6_9_d` is queued.
- **RUNNING: the job queue** - `bash tc2/out/tools/s23/runq.sh` (task bc2qpqxfq; one heavy job at a time, each gated). `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q` (`runq.log`,
  `log/<job>.log`, `todo`, `done`, `hold`; `touch $Q/stop` ends it after the current job; copies of every job in `tc2/out/tools/s23/`). Order from 05:05:
  `6_8_z` (C9.5c rebuild and tests) -> `6_9_a..c` (C9.5c) -> `6_9_d2` (SwornTests) -> `6_9_d` (the landing branch's full suite) -> `6_9_e..g`
  (C10.7b) -> `6_a_g2_*`, `6_b` (D2-161's attribution) -> `6_c_*` (C10.7a 900 years) -> `6_d_*` (C10.7b 900 years) -> `6_e_profile_sx1` - about
  three hours. `$Q/hold` keeps the first C10.7b version's unrun jobs (superseded; do not requeue).

## Do next, in order
1. Read the queue's results as they land and fill the drafts: `d2166.md` (C9.5c control and ten seeds), `d2165.md` (C10.7b retest, control, ten
   seeds, 900 years, profile - and decide the open question), `d2164.md` (C10.7a's 900 years); `6_9_d`'s full suite must be 0 failed, 0 skipped.
2. If the three are at par and the full suite passes: on main `git merge --no-ff wip/s23b-int`; DECISIONS D2-164, D2-165, D2-166 from the drafts;
   PROGRESS; queue `tc2/out/tools/s23/7_a_land_checks.sh` (bin_m5, floatcheck, `unity.mjs cc`, a v90 seed-2 world `_m5`), `7_b_shots_sworn.sh`
   (`s23p`: a lord's sworn men at practice, `act:Train:sworn`), `7_c_web.sh` (`s23web5`) - judge the shots (do the sworn men read at the lord's
   ground?). If one slice is not at par, land the others (rebuild the landing branch without it; sim versions renumber).
3. Then by evidence: rising together (tributaries of one lord weighing together against it) if lordships snowball; a lord's death and its
   tributaries; a polity map layer (the standards read only up close).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push; push only when asked
  (as Arjun0014, per the global CLAUDE.md).
- **Resource rule (Arjun D-1/D-2)**: one low-priority heavy job at a time while RAM < 86 % and CPU < 85 % - the job queue (`tc2/out/tools/s23/runq.sh`,
  `lib.sh`: `gate`, `low`, `world`); never kill processes you did not start (the user's Chrome and other Claude sessions share the machine). A queued
  build compiles the worktree's working tree: do not edit a worktree between its job's start and the copy of its binary.
- Develop in a worktree per slice (`git worktree add -b wip/<slice> "C:/Game Dev/tc-<slice>" main`); keep the main checkout on main for the Unity editor
  jobs (`unity.mjs cc`, shots, the Web build run in the main checkout). New test files need a `.meta` (fileFormatVersion 2, a new guid).
- Core is pure C#, deterministic, no float arithmetic (floatcheck). A derived cache must give the same answer whenever it is filled (`03` §4). Judge
  simulation changes by ten seeds against a control on the same binary (a `--old...` switch reproducing the previous runs byte-identically - annals AND
  the state hash); wall-clock time on this shared machine is not evidence.
- **A test premise that changes the ground by hand** (sinks or raises cells outside the plates' turn) leaves the running world's waters drained for the old
  ground: **load it once** (`SaveFile.Load(SaveFile.Save(w, ...))`) before testing anything a save must continue (D2-162's finding).
- The queue's test gate requires `0 skipped` (an `Assume` that fails is not a pass). `--filter CrossTests` also runs AcrossTests (substring match).
- Bash heredocs pass through a layer that collapses `\\`: write patch scripts or text with backslashes with the Write tool.
- Saves carry a sim version; a save loads only in a binary of the same version (main is 87; the `_x2` saves are 86, `_y1` 87).
- Test worlds (`TestWorlds.Make`) have not lived their first ticks: a premise that sinks or raises ground must `AdvanceTo(w.Now + SimTime.Hour)` first.
- The headless build does not compile Game code (`Game/`, the view, `ShotRunner`): after any Game edit run `node tc2/scripts/unity.mjs cc`.
- On the 6 km planet 5 km is ~48 degrees of longitude: villages a day's walk apart keep local times ~3 hours apart - shoot a village's activity in its own
  hours.
- `Commands.cs`'s report is one long method: give a new tally's local a long, unique name (two builds broke on `at` and `vt`, CS0128/CS0136), and build
  the headless app after merging tools into main - `unity.mjs cc` does not compile it (main's headless build was broken for an hour by db4b8ba).
- Run tags in `tc2/out/civ` are reused across sessions (`k1`, `w1`, `v3` held older runs): check `ls tc2/out/civ | grep _TAG` before queuing a new tag.
- To shoot something rare, find it first: `voyages --load SAVE --days N` (and probes like it) live the save as the game loads it - the same world, the
  same minute - then `unity.mjs shots --village V --days D --hour H` frames it (D2-163).
