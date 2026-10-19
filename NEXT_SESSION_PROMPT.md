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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 23, third context)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 23, third context"**: exact state, the running job queue, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest: **D2-162** (C9.5a grain across water goes by boat) and **D2-163** (C9.5b a boat to cross) - both landed.
4. `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §5 (C9.5a, C9.5b), `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12g-§12h, `docs/tc2/25_CIVILISATION_ROADMAP.md`
   (C9 and C10 slices), and for the next slice `tc2/out/tools/s23/c107_design.md` (C10.7: evidence, the record, the design and its open question).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 23, third context entry first).
6. `git status`, `git log --oneline -12`, `git worktree list`, and the queue: `cat $Q/runq.log` (path below).

## Session 23, third context (20 Oct 2026, 00:04-02:55 IST) - C9.5a and C9.5b landed; C10.7a drafted; the job queue is running
- **On main (bc99679 and later; sim version 87)**: **C9.5a** (c8f6f5d, D2-162) - within a day's walk grain moves at once and traders walk only along a
  way on foot; across water relief, grain given back and the lord's tenth go by boat (the tributary's own or the lord's, come to fetch it) or not at
  all. **C9.5b** (0a8b0b0, D2-163) - a village whose neighbour across the water is worth reaching makes a boat for that (`Boats.Cross`, history
  `BoatToCross`), and a dugout carries 150 kg across a short water. Both: control byte-identical (annals and state hash), ten seeds at par in the
  vital rates; C9.5b: voyages x2.4, the archipelago (seed 5) made 11 dugouts to reach its neighbours and its hunger deaths fell 1,752 -> 1,283.
  Validated after C9.5a: full suite 427/427, Unity C# compiles, Web `s23web3` 6/6 (browser = headless = Mono bit for bit). After C9.5b: build,
  floatcheck and Unity C# clean, **full suite 433/433** - its real shots and Web `s23web4` were running at the handoff.
- **Drafted, NOT built: C10.7a a lordship's might by lasting strength** (`wip/c107a-arms`, worktree `C:/Game Dev/tc-arms`, tip d182cf2): might
  weighs each person of fighting age by the village's tools, arms and iron and their own fighting skill (as a fight weighs them), not the day's health;
  `--oldmightarms`. MightTests still encode C10.5's head count (to update); its measurement (`lordships --might`) is queued.
- **RUNNING: the job queue** - `bash tc2/out/tools/s23/runq.sh` (task bc2qpqxfq). `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q`
  (`runq.log`, `log/<job>.log`, `todo`, `done`; `touch $Q/stop` ends it after the current job). At the handoff, in order (the full suite done):
  `6_7_c5_might_measure`, `6_7_d_shots_cross` (`s23m5`, `s23m4`), `6_7_e_web` (`s23web4`), `6_a_g2_05..10`, `6_b_cmp_g2` - about 75 minutes.

## Do next, in order
1. Read the queue's results (SESSION_HANDOFF "Next" 1) and record them: PROGRESS; D2-163 (shots, Web, full suite); D2-161 (`_g2`, the tribute
   lean's attribution). If the full suite or Web fails on main, fix that first.
2. **C10.7a** on `wip/c107a-arms`: MightTests to the new rule, a test where arms and skill decide a bow, `--oldmightarms` in `Commands.cs`, sim 88;
   build and tests through the queue, control `--oldmightarms` = `_y1`, ten seeds, the 900-year check (no snowball?) -> D2-164, merge, Unity cc, suite.
3. Then by evidence: C10.7b sworn men fed from the tribute, C9.5c people across water, a polity map layer (the UI pass).

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
