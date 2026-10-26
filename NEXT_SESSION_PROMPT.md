# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism, not a stop condition. Stop only for a real external blocker.
**No subagents.** Simulation and presentation advance together: every slice answers "what changes in the world picture?" and is judged
in real game shots.

**Arjun's direction (26 Oct 2026, session 27) - CURRENT**: about two weeks remain; **implementation and game quality, not endless validation** (no
new replications, placebos or seed batteries without a real unexplained regression; no new 1,000-year run for a presentation change). The majority of the
work is the visible, player-facing game, in order: **A** the planet from orbit (a beautiful small Earth: limb, oceans, land, clouds from the weather, storms,
shadows - never faked climate); **B** people and places recognisable (lords, heads, sworn men, families, households, settlements, buildings; hover/click);
**C** the History Book as an evolving chronicle with eras, sections and visual hierarchy; **D** W-FOREST; **E** game feel for the god's powers (targeting,
response, consequences in Your Actions and the book). Each slice: implement -> compile -> focused tests -> real shots/playtest -> Web smoke when needed ->
fix -> commit -> next.

**THE MACHINE: never draw on the RX 5600M** - it restarts the PC under load (26 Oct, 10:45, during Unity shots). `tc2/scripts/gpusafe.mjs` (called by
`unity.mjs` and every browser script) sets Unity and Playwright's Chromium to the power-saving GPU and refuses to start while the card is present; never set
`TC2_ALLOW_DGPU=1`. Each Unity run prints `gpu ...` from its log - it must be "AMD Radeon(TM) Graphics (ID=0x1636)". Runs are silent (`--mute-audio`, the
listener at 0 in shots).

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 27)").
2. `docs/tc2/SESSION_HANDOFF.md` - the top block **"Session 27, context 2"** (and "Session 27" under it): the exact state, the queue, "Next, in order".
3. `docs/tc2/25_CIVILISATION_ROADMAP.md` §8 - **the status ledger** (what is built, measured-only, in evaluation; the next slices A-E) and §6 (the acceptance
   runs); `docs/tc2/DECISIONS.md` newest (D2-183-D2-189, D2-191 the planet from orbit); `docs/tc2/PROGRESS.md` CURRENT STATE.
4. `git status`, `git log --oneline -15`, `git worktree list`, and the queue: `cat $Q/runq.log` (`Q` below) - which jobs ended, with what; `ls $Q/todo`.

## FIRST - the state at the very end of session 27, context 3 (16:40 IST)
- **The job queue's runner (task `bbgjl1nqj`) and the main fast-forward waiter (task `b2qjknhr7`) were stopped by Claude Code: the machine ran critically
  low on memory.** Nothing failed in them. The harness asked that they not be restarted unasked: report it to Arjun and restart the runner only when
  he agrees or memory clearly allows (`gate` in `tc2/out/tools/s23/lib.sh`; one heavy job at a time).
- The Web build of q6 (main **e748638**) was left running on its own (Unity, log `tc2/out/logs/unity-build-web-2026-10-26T11-04-19-927Z.log`, linking
  the wasm at 16:39); **q6's later steps did not run** (no webtest `s27web6`, no `s27perf4`, no bookshots `s27book6`). `$Q/run_0_q6_web_s27web6.sh` is
  stale (move it to `$Q/done`); `$Q/todo/0_q7_shots_s27f.sh` still waits.
- **main is behind**: main = e748638, `wip/s27-int` = the docs and fixes after it (the shore palisade 5c62ace, D2-196, this prompt). Once no Unity job
  runs in main's checkout (the build log ends with its result): `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset
  NEXT_SESSION_PROMPT.md` (this file was copied there uncommitted), then `git merge --ff-only wip/s27-int` in `C:/Game Dev/tiny-consequences`.
- Then (with the runner restarted): the webtest and the measures against main's build - if the orphaned build finished, run q6's remaining steps
  (webtest `--tag s27web6`, `realmsperf --layer terrain` paused and `--layer recolour --speed 5`, bookshots `s27book6`) from it; better, rebuild from main
  after the fast-forward so the shore palisade is in it; then q7 (`s27f7`).

## Session 27, context 3 (26 Oct 2026, 14:52 - 16:45 IST) - prospecting landed (sim 108); the shots' findings fixed; the frame's real waste found
- **main** = `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`; work there, fast-forward main between Unity jobs). At the handoff main was e748638 and a
  background waiter (task `b2qjknhr7`) fast-forwards it to `wip/s27-int` (c16b86c or later) once the running Web build exits - verify
  `git log -1 main` = `git log -1 wip/s27-int`; if not, `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset` and
  `git merge --ff-only wip/s27-int` in `C:/Game Dev/tiny-consequences` (never while a Unity job runs there).
- **Landed**: C5.3c prospecting (D2-190; sim 108). Saves for shots/Web: `tc2/out/civ/s2_ac5/world_y300.tc2`, `world_y600`, `world_y1000` (sim 108).
- **Fixed in real shots and Web builds** (D2-196): rivers far off; the meteor (one fall, grey dust, its fall heard); the regions' cell staircase
  (`TerrainLod.WarpCells`, on a lattice); night clouds by the moon; the far glint; no road over a lake; pools in waterlogged woods (never under snow);
  the palisade (stakes; along a shore two passes - pending shots `s27f7`); night lights at 512; the book's repeated ages named by their realm; Your
  Actions folds a cast's dead; the person and place pages as leaves of the book and in words; Your hand never over a page.
- **The frame**: Terrain.shader branched (gain only x1.02 region / x1.06 village, `s27perf3`); shadows x0.93, buildings x0.84/x0.88. Every terrain
  measure had been taken paused while the ground's colouring cycle ran on for ever - now only when the world moved (6ae81de; bridge `recolour`,
  realmsperf `--speed`). `s27perf4` (in q6) measures the terrain paused again and the colouring at 1 h/s.
- **Running at the handoff (16:45)**: the queue (runner task `bbgjl1nqj`; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/
  60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`; follow `tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |^start "`): `0_q6_web_s27web6`
  (headless rebuilt, Web build of main e748638, webtest `s27web6`, `tc2/out/webtest/s27perf4/`, bookshots `s27book6`), then `0_q7_shots_s27f` (needs main
  to hold 5c62ace; shots `s27f7` - the shore palisade).

## Do next, in order
1. Read q6: webtest `s27web6` 9/9; `s27perf4/terrain.txt` (compare `s27perf2`'s x0.589/x0.660) and `recolour_running.txt`; `s27book6` pages and meteor.
   Fill D2-196's pending numbers (DECISIONS, the paragraph ending "pending at the context handoff"). Read q7 `s27f7`.
2. The frame by those numbers (SESSION_HANDOFF "Session 27, context 3" -> Next 3): the terrain's draws/vertices or soft-shadow taps if it is still large
   paused; the colouring spread thinner or cached per patch if it costs a running world much. Then buildings (x0.84-0.88).
3. Then by the shots: night lights from orbit, the sea's glint at the region view; keep judging A-E in real shots and Web builds.

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
  rebuild each binary from its worktree before using it. Saves the jobs read live in `tc2/out/civ` (ignored, persistent). Note: `lib.sh` sets `S` - do not
  name your own shell variables `S`/`N`/`O`/`R` after sourcing it.
- Develop in a worktree per slice (`git worktree add -b wip/<slice> "C:/Game Dev/tc-<slice>" main`); new test files need a `.meta` (fileFormatVersion 2, a
  new guid).
- Core is pure C#, deterministic, no float arithmetic (floatcheck); observers use `DMath` where an order depends on maths. Judge simulation changes by ten
  seeds against a control on the same binary (`--old...` reproducing the previous runs byte-identically - annals AND the state hash); **a lean past |t| 2
  is judged against a matched placebo** (the old rules with `--nudge Y` at each world's parting year; `tc2/out/tools/s24/lean.py`), then explained by
  its mechanism by century (as D2-168 did). Wall-clock time is not evidence.
- The queue's test gate requires `0 skipped`. Test premises that set state directly must also tell it as the game would (the record a bond stands on).
- Saves carry a sim version; a save loads only in a binary of the same version (main 90, C10.8 91, C10.9 92; `_i2`/`_m5`/`_l9i2` saves 90, `_dm1`/`_l9dm1` 91).
- Run tags in `tc2/out/civ` are reused across sessions: check `ls tc2/out/civ | grep _TAG` first (`_dmp`, `_lp0`, `_lp1` are this session's).
- To shoot something rare, find it first (`voyages --load SAVE --days N` and probes like it), then `unity.mjs shots --village V --days D --hour H`.
