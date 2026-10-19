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

## Session 23 (19 Oct 2026; two contexts) - C10.5, D2-160, C10.6 landed; C9.5a in evaluation; the job queue may still be running
- **On main** (b7cc066 and later): C10.4; **C10.5 a lordship's might, D2-160 the ways' woods, C10.6 tribute carried - amended before landing** (a19c077;
  DECISIONS D2-159, D2-160, D2-161). The amendment: the tenth is set aside only where a way on foot reaches the lord (a fifth of all tribute in ten worlds
  had none - a bond for bread needs only 6 km as the crow flies); a year uncarried returns; one cart goes one way at a time. Validated: full suite
  419/419, control byte-identical, ten seeds at par, Web `s23web2` 6/6 (browser = dotnet = Mono), real shots (carriers on the road `s23k`; standards
  not legible from the village view - a map layer is wanted).
- **In evaluation: C9.5a grain across water goes by boat** (`wip/c95-across`, worktree `C:/Game Dev/tc-across`, sim 86; draft `tc2/out/tools/s23/d2162.md`).
- **The job queue** (`bash tc2/out/tools/s23/runq.sh`, `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q`):
  `5_e1_a1_*` (the survey: grain across water at once on the landed code), `5_e2_*` (C9.5a: build, tests, control `--oldacross` = `_g1`, ten seeds
  `_x1`, `results/cmp_g1_x1.txt`), `5_e_l9g1_s2` (the landed code's 900-year check), `6_*` (ten seeds with D2-160 alone: who caused tribute agreed
  x0.74?). `cat $Q/runq.log`; `touch $Q/stop` stops it after the current job.

## Do next, in order
1. Read the queue: C9.5a's tests, control and ten seeds against `_g1` (vital rates at par? relief across water by boat; the tenth by sea) with the survey
   `_a1` as the before; then land it (DECISIONS D2-162 from the draft, `git merge --no-ff wip/c95-across`, `unity.mjs cc`, the full suite, Web).
2. The 900-year check of the landed code and the `_g2` attribution -> PROGRESS, D2-161.
3. Next by evidence: C9.5b people across water in boats (island woodcutters, a town on two shores: lived legs by boat), or C10.7 the lord's household
   men (seed 2's lords hold 3-10 years of their own need; seed 4's spends it), or a map layer for polities (the UI pass).

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
