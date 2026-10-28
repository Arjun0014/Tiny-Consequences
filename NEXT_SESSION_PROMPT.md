# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism, not a stop condition. Stop only for a real external blocker.
**No subagents.**

**Arjun's direction (28 Oct 2026, session 29) - CURRENT: "THE JAM MUST CLEARLY READ AS A REAL SIMULATION TO THE PLAYER."** Every feature answers: what is
happening, who is doing it, why, from what resources/relationships/history, what changed, how the player can inspect and follow the chain, and how the
picture shows it. The player's own casts must be traceable: power -> immediate effect -> people/places affected -> secondary consequences -> long-term
history. Never fabricate an explanation: read the real state and the causal ledger; if a truth is not kept, add the smallest real state. Order of the
brief: (0) the held perf check - DONE; (1) the long skip as an orbital timelapse - DONE (D2-199); (2) the person page as a person - DONE (D2-200); (3)
readability with progressive disclosure world -> realm -> settlement -> household -> person -> life/causes - DONE in large part (D2-201, D2-202); (4)
"Why?" from the ledger - DONE (D2-201); (5) the History Book as the world's memory - its sections, links and Your Actions' threads DONE (D2-201, D2-202);
(6) the powers as play - their word and live threads DONE (D2-202), the aim's words in a box (D2-203), keep polishing; (7) the visual world where it is
evidence of the simulation (orbit, clouds, region edges, W-FOREST, settlements, people, important buildings, roads, political centres, war scars, ruins) -
IN PROGRESS (D2-203: war scars - burned crops and homes, ruins that say how they fell; the fields' edges; the rivers' glint; the timelapse's seasonal edge),
judged in real shots.
Validation for presentation: compile -> focused tests -> real Web play/shots -> fix -> commit; no seed batteries or 1,000-year runs for UI.
Arjun is AFK: make normal design decisions; never ask "should I continue".

**THE MACHINE: never draw on the RX 5600M** - it restarts the PC under load. `tc2/scripts/gpusafe.mjs` (called by `unity.mjs` and every browser script)
sets Unity and Playwright's Chromium to the power-saving GPU and refuses to start while the card is present; never set `TC2_ALLOW_DGPU=1`. Each Unity run
prints `gpu ...` - it must be "AMD Radeon(TM) Graphics (ID=0x1636)". Runs are silent. RAM < 86 %, CPU < 85 %, one heavy job at a time; never kill a
process you did not start.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 29)").
2. `docs/tc2/SESSION_HANDOFF.md` - the top block **"Session 29"**: the exact state, the queue, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest: D2-199 (the timelapse; the frame), D2-200 (the person page), D2-201 (Why?), D2-202 (roads, a village from afar, the
   world now, the building leaf, the book's order/Peoples/links, the powers' word); `docs/tc2/PROGRESS.md` CURRENT STATE; `25_CIVILISATION_ROADMAP.md` §8.
4. `git status`, `git log --oneline -20`, `git worktree list`, and the queue: `cat $Q/runq.log` (`Q` below) - which jobs ended, with what; `ls $Q/todo`.

## FIRST - the state at the end of session 29's second context (29 Oct 2026, ~01:30 IST)
- **The branch `wip/s27-int` is the work** (worktree `C:/Game Dev/tc-s27int`, every edit there; tip = this note's commit). **Each queued job fast-forwards
  main to it at its start** (`git merge --ff-only wip/s27-int`); when the queue is idle and main is behind, fast-forward it yourself:
  `git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int` - never while a Unity job holds main's checkout. After any Web build:
  `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset` in main's checkout (the jobs do it). Sim **108** unchanged all session.
- **The queue** (`Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`,
  hard-coded in `tc2/out/tools/s23/runq.sh` and `lib.sh`; the runner (task `bp3fvcitn`, started in session 29's first context) keeps polling `$Q/todo`;
  `touch $Q/stop` stops it after its job; jobs run in name order). **At this handoff**: `0_qn_web_s29f` running (started 01:13: Web of 5ad5c7f -
  `s29web6`, `s29book6` (extras with the ruin last burned), `s29book7` (Kaihope at year 600 with names, its Now/Why? pages), `s29lapse5` (the skip from
  orbit after the stale-patch fix)); then `0_qo_shots_s29v7` (Paitaimwick's burned homes, the soot in the alpha) and `0_qp_fullsuite_s29c2` (the full
  headless suite on the branch, into `$N/bin_s29c2`; `$N/fs_s29c2_ok` when it passes). Add a job by writing `$Q/todo/0_<name>.sh` (source `lib.sh`;
  `gate`, `low`; `$Q/done/0_qm_shots_s29v6.sh` for a shots job, `0_qn_web_s29f.sh` for a Web job).
- **Done in session 29's second context** (DECISIONS **D2-203** (1)-(18); SESSION_HANDOFF "Session 29, context 2"): D2-202's pages seen in Web builds
  (`s29web3`-`s29web5` 9/9); the visual world judged in Unity shots `s29v1`-`s29v6`, `s29n1` (0 shader errors, read from Unity's own logs) and fixed: burned
  crops and burned homes drawn as fire left them, a ruin saying how it fell, the rivers' far glint, the fields ending furlong by furlong, the timelapse's
  straight seasonal edge (a steep colour ramp) and its out-of-step block (stale cached patches); readability: works' tags with why, the History panel's
  lines (newest first) and the news opening the book, the ticker on a loaded world, no names under panels, the aim's words in a box, crafts and works in
  words, the book's words, a village's name where its people live and its page saying so, the title's guide.

## LAST STATE (29 Oct 2026, ~02:00 IST) - read this first
- **The full headless suite passed**: `0_qp_fullsuite_s29c2` on the branch at 0fe9093 - **TESTS: 523 passed, 0 failed, 0 skipped** (1,142 s;
  `$N/fullsuite_s29c2.txt`, `$N/fs_s29c2_ok`; `N` = the queue's scratchpad, see `lib.sh`).
- **The queue's runner is STOPPED**: Claude Code stopped the background runner (task `bp3fvcitn`) at ~02:00 because the machine was low on memory, while
  `0_qq_web_s29g` was in its Unity Web build (started 01:59:38 at main 31a268a; two Unity.exe were still running at 02:00 - the build may finish on its
  own, but the job's later steps did not survive: no webtest `s29web7`, no bookshots `s29book8`/`s29lapse6`, no ProjectSettings restore). Its script is
  gone (the runner had taken it from `$Q/todo`); it was `$Q/done/0_qn_web_s29f.sh` with tags s29web7 / s29book8 (`--save s2_ac5/world_y600.tc2 --lat -51.4
  --lon 44.1 --views "region:6000" --extras --pages "place:1:1,place:2:1" --book ""`) / s29lapse6 (`--lapse --skip 10` on y300) and the check
  `merge-base --is-ancestor 0fe9093`. **Do not restart the runner on your own** (Claude Code's notice: memory may still be short) - check memory
  (`source tc2/out/tools/s23/lib.sh; load`) and that no Unity.exe of the stopped job still runs; ask Arjun before restarting it if in doubt. Once Unity
  has ended: `git -C "C:/Game Dev/tiny-consequences" checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset` (the build modifies it);
  `unity/TinyConsequences2/Assets/Resources/` (untracked) appeared during the build - leave it unless it is clearly the build's own output.

## Do next, in order
0. **At the handoff (~01:40)** `0_qn_web_s29f` and `0_qo_shots_s29v7` are DONE and judged (D2-203 (19)); `0_qp_fullsuite_s29c2` runs, `0_qq_web_s29g` is
   queued (verify the orbit recolour `s29lapse6`, Kaihope's burned-crop line `s29book8`). **Open first**: charred ruins still not dark in shots
   (`s29v6`/`s29v7`) - see D2-203 (19) and SESSION_HANDOFF "Session 29, context 2" step 0.
1. (done) When `0_qn_web_s29f` ends: judge `s29web6` (9/9); `tc2/out/webtest/s29book6/extra_ruin_page.png` and `extra_ruin_hover.png` (how it fell);
   `s29book7/village.png`, `region.png` ("Kaihope" over its lived homes), `page_place_1_1.png`/`page_place_2_1.png` ("Its people live 310 m from its first
   square ..." and its Why?); `s29lapse5/lapse_7_skip_orbit.png` and `lapse_8_skip_1.png` (no block out of step). Then `0_qo_shots_s29v7`
   (`tc2/out/shots/s29v7`: the burned homes black among grey ruins) and `0_qp_fullsuite_s29c2` (0 failed, 0 skipped). Fill D2-203's "Pending"; fix; commit.
2. Then the brief's open looks and readability: two villages' fields meeting along the cells (D2-203 "left"), W-FOREST's sameness (a dedicated art pass),
   any gap the pictures show (validate: compile -> focused tests -> real Web play/shots -> fix -> commit; no seed batteries for UI).

## Rules that bite
- **After a sim version change, rebuild main's headless (`tc2/headless/app`) before a webtest**: its determinism check compares the browser with
  `tc2/headless/app/bin/Release/net8.0/tc2.dll` (s27web5 failed against a stale sim-107 binary while the browser matched sim 108 bit for bit).
- **Never retag job files with a broad `sed`** (session 26: `s/_sw1"/_stw1"/` also renamed a build job's binary dir, so its tests ran a missing binary and the
  whole chain behind it would have skipped). Pick fresh tags before generating jobs (`ls tc2/out/civ | grep _TAG`), and after any edit grep every queued job for
  its binary, flag and run names.
- **Gate commits on the build**: `grep -c " error "` must be 0 before `git commit` in the same command (a commit went in with a compile error this session).
- **A premise that gives a household grain must put it in its village's granary too** (`Household.Grain` is a share of `Settlement.Granary`; the holdings
  reconcile shares to the store within a day).
- **Resolving a conflict by keeping both sides** duplicates whatever both branches inserted at the same anchor (session 25: two measures inserted above
  one method lost a brace and broke main's headless, fixed in 1b83b9e) - build after every resolved conflict before committing. Main's tip at the
  handoff: 1b83b9e (sim 94; the measure tools `place --stonewalls`, `--ceiling`, `--ways`, `lordships --walls` on it). The queue's runner idles (empty
  `todo`); `touch $Q/stop` stops it.
- **Job scripts: never name a loop variable `c` or `r`** before session 25's fix `gate` overwrote them; name test-class loops `cls`, and every test gate
  needs `[1-9][0-9]* passed, 0 failed, 0 skipped` ("0 passed" once passed a gate).
- **Each landing changes the sim version and old saves stop loading**: queued shots/Web jobs must point at a save of the version they run (make one
  with the landed binary: `9_b_*`); keep two branches' versions distinct (a save of one loads silently in the other's binary if they share a number).
- **`unity.mjs cc` compiled nothing from session 23 to session 25** (a shadowed variable made every glob miss): fixed in ea6b826 - check its
  `tc2/out/cc/Game.csproj` has `<Compile Include="C:/.../Assets/TC2/Game/**/*.cs" />` if a compile ever passes suspiciously. Unity's own compile (shots,
  Web build) is the final word; an ILPP pipe error (`ILPPTrigger: Can't find file \\.\pipe\unity-ilpp-...`) is Unity's glitch - rerun the job.
- **Python patch scripts that write C#**: a `\n` inside a normal Python string becomes a real newline inside a C# string literal (it broke the HUD) - write
  such scripts with the Write tool and keep C# escapes as `\\n` or use raw strings. Bash heredocs here also choke on some quoting: write scripts to files.
- **The book (`HistoryBook`) is an observer**: never let it write state; its lines must stand on records (`HistoryBookTests`). A part is written when the
  reader opens it (Places ~0.3 s in the browser on a year-900 world - the slowest).
- **The look's tunables** live in `Game/View/Look.cs`; a shot sets them per view: `unity.mjs shots --views "orbit~AtmoSun=8~CloudCu=1.2,region"`.
- **The HUD is IMGUI and camera shots do not show it**: judge names, the book and pages in the Web build (`tc2/scripts/bookshots.mjs`: a headless save
  put into the browser's store; `--views`, `--book "P:C,..."`, -1 the preface, -2 the timeline). On this shared machine frame rates are the load's: compare
  paired on/off counts in one page (`realmsperf.mjs --layer NAME`), never two runs' absolute rates.
- **The browser's wasm heap** is read from `window.__TC2_INSTANCE.Module.HEAPU32.buffer.byteLength`; it only grows.
- Real shots must cover every age and dye a presentation layer can meet (session 24: realms read as lakes on a year-900 world).
- Hold a queued build job (`mv $Q/todo/J $Q/hold/`) before editing the worktree it builds; a job named `0_...` runs next; the runner starts the next job the
  moment one ends; never edit main's Game code while a Unity job (shots, Web build) is running in the main checkout - work in a worktree.
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push; push only when asked
  (as Arjun0014, per the global CLAUDE.md).
- **Resource rule (Arjun D-1/D-2)**: one low-priority heavy job at a time while RAM < 86 % and CPU < 85 % - the job queue (`tc2/out/tools/s23/runq.sh`,
  `lib.sh`: `gate`, `low`, `world`); never kill processes you did not start (the user's Chrome and other Claude sessions share the machine).
- **The scratchpad can vanish between sessions**: `S`/`N` in `tc2/out/tools/s23/lib.sh` and `Q` in `runq.sh` are hard-coded - point them at a live one and
  rebuild each binary from its worktree before using it. Saves the jobs read live in `tc2/out/civ` (ignored, persistent; since D2-198 only sim 107/108
  and the saves queued jobs name - make any other with its binary). A Unity log past ~100 MB is a bug signal (D2-198: 6 GB logs of one repeated assertion) -
  let the job finish, read its first repeated message and fix the cause, then delete the log. Note: `lib.sh` sets `S` - do not
  name your own shell variables `S`/`N`/`O`/`R` after sourcing it.
- Develop in a worktree per slice (`git worktree add -b wip/<slice> "C:/Game Dev/tc-<slice>" main`); new test files need a `.meta` (fileFormatVersion 2, a
  new guid).
- Core is pure C#, deterministic, no float arithmetic (floatcheck); observers use `DMath` where an order depends on maths. Judge simulation changes by ten
  seeds against a control on the same binary (`--old...` reproducing the previous runs byte-identically - annals AND the state hash); **a lean past |t| 2
  is judged against a matched placebo** (the old rules with `--nudge Y` at each world's parting year; `tc2/out/tools/s24/lean.py`), then explained by
  its mechanism by century (as D2-168 did). Wall-clock time is not evidence.
- The queue's test gate requires `0 skipped`. Test premises that set state directly must also tell it as the game would (the record a bond stands on).
- Saves carry a sim version; a save loads only in a binary of the same version (the header: `TC2W`, u16 format, i32 sim version at byte 6; main is 108).
  The saves of sim 90-92 this rule once listed (`_i2`, `_m5`, `_l9i2`, `_dm1`, `_l9dm1`) were deleted in D2-198.
- Run tags in `tc2/out/civ` are reused across sessions: check `ls tc2/out/civ | grep _TAG` first (`_dmp`, `_lp0`, `_lp1` are this session's).
- To shoot something rare, find it first (`voyages --load SAVE --days N` and probes like it), then `unity.mjs shots --village V --days D --hour H`.
