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

## Session 27, context 2 (26 Oct 2026, 12:12 - 14:50 IST) - all of A-E on main, the frame doubled, prospecting ready to land
- **main** = `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, the integration branch; main is fast-forwarded to it only while no Unity job holds main's
  checkout), sim 107 unchanged (presentation and observers only). Merged the open-book reader and E; then, judged in real shots and Web builds
  `s27web2`/`s27web3` (9/9 each): house rings and family rings on hover, flags for a realm's villages, `Biography.Standing` on the tag/page/inspector,
  the inspector as a card in words, pages dated as the book, "N sick"/"famine" on the map, the timeline's eras under the pointer, the ages' years in the
  contents, an age's great events capped by kind, the book's foot clear at 720 px, the meteor's fall and scar, lightning bolts for every recorded strike,
  red-orange flames, power tooltips, "Your hand" sized to its lines, the top bar measured from its words, the opening in the book's type, snow on the
  boughs and bare crowns over snow, the rain forest's tones, lumpy soft cumulus of many sizes, a fire's smoke capped. **D2-192-D2-195 written.**
- **The frame** (integrated GPU, seed 2 year 300, 11,272 people; `tc2/out/webtest/s27perf`, `s27perf2`): region 12.4 -> ~30 fps, village 8.4 -> ~16,
  from far villages plain and shadowless (`WorldObjectsView.DetailFar` 2.2 km), the light shadows graded by height and the default (`Sky.ShadowLite`),
  the air's march halved over the ground. **The terrain is the next cost** (x0.59 / x0.66) - the plan is in SESSION_HANDOFF "Session 27, context 2".
- **Prospecting (C5.3c, sim 108)**: ten seeds inside the noise (people x1.02 t 0.38, hunger x0.73 t -1.29, famines x0.76 t -1.48, raids x1.13 t 1.38,
  walls x1.14 t 1.81) - **land it**. Merged onto main in the landing worktree `C:/Game Dev/tc-s27land` (`wip/s27-land`); its tests were running at the
  handoff. Land only after the queued sim-107 jobs (`P_a`, `P_b`) finish, then regenerate the shots' saves with the sim-108 binary.
- **Running at the handoff (14:50)**: the job queue (runner `tc2/out/tools/s23/runq.sh`, background task `bbgjl1nqj` - reuse it; `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`): `N_d_land_tests` (the
  landing branch's tests), then `P_a_shots_s27c` (Unity shots on main: `s27p7` cumulus, `s27f6` bare crowns over snow, `s27e8` a meteor's smoke), then
  `P_b_web_s27web4` (a Web build of main, webtest `s27web4`, bookshots `s27book4 --play --meteor-play`). Follow it with
  `tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |runner stopped"`.
- Tools: `gcommit.sh` in the context-2 scratchpad (`C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/5e178fba-891f-4eea-9925-5eedb5b9c075/scratchpad/gcommit.sh`):
  compiles the worktree's Game/Editor/Tests against main's references (`unity.mjs cc --src`) and commits only when clean - copy it if the scratchpad is
  gone. **Python patch scripts through the Bash tool lose a backslash** (a doubled backslash before n arrives single, so Python writes a real newline into
  the C# string) - write such scripts with the Write tool (it keeps escapes literal), or use Edit.

## Do next, in order
1. `N_d_land_tests` must pass (`$Q/log/N_d_land_tests.log` ends "s27land ok"). Judge `P_a`'s shots and `P_b` (`tc2/out/webtest/s27web4.log` 9/9,
   `s27book4/play_*`); after the build `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset`; fix what the pictures say in
   `tc-s27int`.
2. Land prospecting: merge `wip/s27-int` into `wip/s27-land` if it moved, fast-forward main to `wip/s27-land` (sim 108), write D2-190, 25 §8; regenerate
   seed 2's year-300 and year-1,000 saves with the sim-108 binary (as `tc2/out/civ/s2_ac4`; `tc2/out/tools/s26/make_ac4.sh` has the flags; a new tag)
   and point later shots/bookshots/realmsperf jobs at them.
3. The terrain's cost (SESSION_HANDOFF "Session 27, context 2" -> Next 3: the woods block branched on wood with `SAMPLE_TEXTURE2D_GRAD`).
4. Then by the shots: night clouds, the sea's broad sheen, age names that repeat, the faint straight line through Raimstead at the village view, a sound
   for the meteor's fall, the person/place pages in the book's paper style.

## Rules that bite
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
