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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 25)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 25 ... first context"**: the exact state, the job queue, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest (D2-170 orbit look, D2-171 the History Book, D2-172 names); the C10.8 draft `tc2/out/tools/s24/d2168.md` (complete,
   to append when it lands); C10.9's design and measures `tc2/out/tools/s25/c109_design.md`; `docs/tc2/PROGRESS.md` CURRENT STATE (session 25);
   `docs/tc2/25_CIVILISATION_ROADMAP.md` §8 (the plan to a finished core).
4. `git status`, `git log --oneline -15`, `git worktree list`, and the queue: `cat $Q/runq.log` (`Q` below) - which jobs ended, with what.

## Session 25, first context (24 Oct 2026, 22:14 - ~23:40 IST) - handed off with the job queue RUNNING
- **On main (50f999f; sim 90 - no simulation change this session)**: the planet from space (D2-170: single-scattering air, clouds from the weather as
  rain decks and cumulus fields, a darker sea, villages' lights as points); the History Book as a book (D2-171: `Core/History/HistoryBook.cs`, eight
  parts written after the fact from the ledger - Ages, Realms and Rulers, Wars and Feuds, Famine/Plague/Disaster, Places, Lives, Threads of Cause, A
  Chronology - and a Timeline page; the HUD's two-page reader; headless `book --load SAVE --md F`); places, works and people named on the map and hover
  tags (D2-172); the compile check `unity.mjs cc` repaired (it compiled nothing since session 23); DECISIONS D2-170..D2-172; 25 §8.
- **Validated**: full suite 442/442 (before the observer/presentation changes); HistoryBookTests 5/5; shots `s25a`-`s25d`; **Web `s25web1` 6/6** and the HUD
  over the year-300/900 worlds in the browser (`tc2/out/webtest/s25web1b900`, `s25web1b300`). **Not yet in a Web build**: works/people names, the book's
  buttons, names clear of the bars, the Timeline page; the paired frame cost of names/air/clouds (`realmsperf.mjs --layer`).
- **The queue** (background task brw7xh61a, runner `tc2/out/tools/s23/runq.sh`; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`):
  at the handoff running `7_j_fullsuite_dm3` (C10.8's branch tip 11b5b0c as `bin_dm3`, from 23:33, ~17 min: `$Q/../fs_dm3_ok` only with 0 failed and 0
  skipped; `tc2/out/tools/s23/results/fullsuite_dm3.txt`), then C10.9's evaluation `8_c_build_lp1`, `8_d_ctl_lp0_s4`, `8_e_lp1_01..10`, `8_f_cmp_lp1`
  (`results/cmp_dm1_lp1.txt`), ~35 min. Held in `$Q/hold`: `8_a*`, `8_b*`, `9_*` (attribution only). No monitor survives the clear: arm one
  (`tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |runner stopped"`).
- **C10.8 the asking - decided: land it** (`wip/c108-demand`, worktree `C:/Game Dev/tc-demand`, sim 91). Its matched placebo: the lean is real (people
  over 100-199 x0.904, t -4.64 against the twin; the twins x1.005) and explained by its own war - wounds +0.7 deaths a thousand person-years and fewer
  foundings while the worlds grow (about a tenth of their people), x0.92 near the ceiling, per-person rates at par; realms rise and fall (the year-900
  books). D2-168's draft is complete.
- **C10.9 the lord's death - built, in evaluation** (`wip/c109-lapse`, worktree `C:/Game Dev/tc-lapse`, from C10.8; sim 92; bd27e74; LapseTests 3): at
  the death of the first at a lord's seat its bonds for peace lapse (bread bonds stand), and heirs win the villages back by asking. Measured first with
  new tools on C10.8's worlds: `lordships --together` (22 of 28 great realms held by the call - their villages together outweigh the lord x1.3-x13.8,
  none alone can) and `--heirs` (the first at a seat dies about every 12 years).

## Do next, in order
1. Read the queue. When `7_j` passes: land C10.8 - in `C:/Game Dev/tc-demand` `git merge main` (main moved: docs, names, the timeline), then on main
   `git merge --no-ff wip/c108-demand` (sim 91); append `tc2/out/tools/s24/d2168.md` to DECISIONS (D2-168); `25`'s C10 line; queue on main the full suite
   (a bin built from main), real shots of realms grown (`unity.mjs shots --realms --load $O/s2_l9dm1/world_y600.tc2 --village lord`), a Web build with
   `bookshots.mjs` (`--book "-2,1:0,6:0"`: the Timeline page) and `realmsperf.mjs --layer names|air|clouds` (paired costs).
2. C10.9: judge `8_f` against `_dm1` (vital rates, bonds lapsed and won back, the greatest realm's share); if sound, its 900 years (from
   `$O/s{2,4}_lp1/world_y300.tc2`, with `lordships --bonds --together --heirs` and `book --load`), D2-173, land after C10.8.
3. Then 25 §8: C8.1 the campaign and the siege, C7.2 stone walls, C11.1 a people's ways, the 1,000-year acceptance; great worlds in the browser
   (`14` §7: ~1 GB, ~8 fps at the region view); then F9 (god powers as play).

## Rules that bite
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
