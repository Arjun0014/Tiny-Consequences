# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism, not a stop condition. Stop only for a real external blocker.
**No subagents.** Simulation and presentation advance together: every slice answers "what changes in the world picture?" and is judged
in real game shots.

**Arjun's priorities (24 Oct 2026, session 25)**: forward progress, not over-testing (the essential validation only); continue the
governance/civilisation slices; a strong "finished core simulation" within two weeks (plan: `docs/tc2/25_CIVILISATION_ROADMAP.md` §8);
world readability and presentation alongside (the planet like Earth from space, places and people identifiable, a real History Book);
then god powers and player-facing gameplay.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 25, second context)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 25, second context"**: the exact state, the job queue, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest (D2-168 the asking, D2-173 the lord's death, D2-174 the lord's peace, D2-175 a people's tongue); `docs/tc2/PROGRESS.md`
   CURRENT STATE (session 25, second context); `docs/tc2/25_CIVILISATION_ROADMAP.md` §8 (the plan to a finished core); the C7.2 measure in
   `tc2/out/tools/s25/results/stonewalls_*.txt` when it has run, and `tc2/out/tools/s25/c81_design.md` (why C8.1 waits).
4. `git status`, `git log --oneline -15`, `git worktree list`, and the queue: `cat $Q/runq.log` (`Q` below) - which jobs ended, with what.

## Session 25, second context (25 Oct 2026, 23:56 - ~02:45 IST) - four slices landed; the 1,000-year acceptance RUNNING in the queue
- **On main (2935c9a and after; sim 94)**: C10.8 the asking (D2-168), C10.9 the lord's death (D2-173: bonds for peace lapse with the first among the lord's
  people; heirs win them back by asking - raids x0.64), **C10.10 the lord's peace** (D2-174: no bands within a realm, blood judged by the common lord;
  chosen over C8.1 by measuring that the war was feud, 30-88 % of it inside realms; people x1.054 against its matched placebo twin, explained - raids
  within realms gone, war at the realms' edges, famines a person-year x0.82), **C11.1a a people's tongue** (D2-175: names in their founders' speech, a sound
  changed at a time; the book's Peoples; annals unchanged byte for byte), **the wars on the realms map layer** (the last ten years' raids as dark spearheads).
- **A fault in the job tooling, fixed**: `lib.sh`'s `gate` clobbered a loop variable `c`; gates now need 1+ passed (name test-class loop variables `cls`).
- **The queue** (background task brw7xh61a, runner `tc2/out/tools/s23/runq.sh`; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`):
  `9_a_fullsuite_m8` (main's suite on the final rules: `results/fullsuite_m8.txt`, `fs_m8_ok`), `9_b_ac1_s{2,4,7}` (1,000-year worlds `$O/s*_ac1/world_y{50,150,300,500,800,1000}.tc2`,
  books `tc2/out/tools/s25/accept/book_s*_y{300,1000}.md`), `9_bx_stone_st0` (the C7.2 measure), `9_bz_web` (Web `s25web2`: the test, bookshots over the
  year-1,000 and year-300 worlds - names in tongues, the book's Peoples/Timeline/Realms, `realmsperf --layer names|air|clouds`), `9_c_shots_s{2,4}_y*`
  (each age's real shots `tc2/out/shots/s25m_*`, realms with the war spears at 1,000). No monitor survives the clear: arm one
  (`tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |runner stopped"`).

## Results since (04:25): main's full suite 461/461 (0 skipped); the 1,000-year acceptance run - 25 §6 "First run", `tc2/out/tools/s25/accept/notes.md`
- **Holds**: wild land, villages, founders, walls, realms that rise and fall, a great plague and recovery (seed 2), peoples with their own speech, books
  that read as history (sixteen-seventeen ages a world; realms as reigns). Web `s25web2` 6/6 over the year-1,000 world (names by tongue on the map; layer
  costs names x0.99, clouds ~x1.0, air x0.92-0.97; the base frame 6-9 fps at region/village - `14` §7).
- **Does not hold**: after the land fills (~300) the map stops changing (2-3 villages abandoned in 1,000 years, almost no foundings) - years 300 and 1,000
  look alike in real shots (`s25m_s2_y300`, `_y1000`).
- Open: the war spears do not read yet in the realms shot (`s25m_s2_y1000r`: a rain deck and dark ink on the wash); one `ArgumentOutOfRangeException` per
  Unity shots run since session 24 (no TC2 frame in the log; trace it); the acceptance shots framed a different lord at each age (use a fixed village).
- **The book's realms as reigns merged** (9fac1c6; an observer, tests and `cc` ok): the realm chapters fold a village's re-bowings after lapses into one count.
- **All the queue's shots done** (`tc2/out/shots/s25m_s{2,4}_*`); the settlement-change measure `9_d_ceiling_cl0` (`place --ceiling`, worktree
  `C:/Game Dev/tc-ceiling`, `wip/ceiling` b9479f5) ran last: `tc2/out/tools/s25/results/ceiling_s*_y*.txt`.

## Do next, in order
1. Look at seed 4's age shots (`tc2/out/shots/s25m_s4_y*`) and the ceiling measure (`ceiling_s*_y*.txt`); the queue is empty after `9_d` (the runner
   idles; `touch $Q/stop` to stop it, or leave it for new jobs).
2. **The next slice by the acceptance's evidence: settlement change at the ceiling** - measure first why no village empties (even after the plague halves
   a world: each shrinks in place) and why none is founded after ~300 (no good land known, or none left), and which of the record's mechanisms has its
   state in the world: deserted villages after depopulation (survivors drawn to vacant holdings - C3.6d weighs leaving by a household's own land only,
   a push, never the pull of land left empty elsewhere), towns drawing the land-short (crafts, markets), war's destruction. Build the smallest real one;
   judge it in shots of one fixed village at years 300, 600, 1,000.
3. Then: C7.2 stone walls (measured: narrow room - a few heavy losers a world with stone at hand, `tc2/out/tools/s25/c72_design.md`); the war spears made
   legible; great worlds in the browser (`14` §7); F9 (god powers as play).

## Rules that bite
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
