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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 26)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 26 - HANDOFF"**: the exact state, the branches to land in order, the queue and what it runs, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest (D2-180 the rain, the harrying, the siege set aside; D2-181 a people's ways; D2-182 great worlds in the browser; before them
   D2-176-D2-179); `docs/tc2/PROGRESS.md` CURRENT STATE (session 26); `docs/tc2/25_CIVILISATION_ROADMAP.md` §6 (the acceptance runs) and §8 (the plan to a
   finished core); the drafts and measures in `tc2/out/tools/s26/` (`d2183.md` stone walls, `d2184_185.md` the memory pass and the ore, `memory.md`,
   `c1011_household.md`, `c82_flight.md`, `make_ac4.sh`, `make_hall.sh`, `results/`).
4. `git status`, `git log --oneline -15`, `git worktree list`, and the queue: `cat $Q/runq.log` (`Q` below) - which jobs ended, with what; `ls $Q/todo`.

## Session 26 (25 Oct 2026, 21:20 - ~23:25 IST; Control Room run #80) - rain, the harrying and the ways on main (sim 101); four slices built, their checks RUNNING
- **On main** (45d4461 + docs): F9.5 the rain; C8.1a the harrying - its ten seeds leaned (people x0.91, sickness x3.9) and **its matched placebo showed chaos**
  (a one-FU nudge of the old rules gives the same pattern: main's ten worlds were a draw with few epidemics; the harrying against its placebo twin people x1.04,
  t 0.5); C11.1b a people's ways. **One full suite over all of it: 484/0/0.** View: flames on dry ground only (the meteor's stood in the sea), `ShadowLite` per
  shot, bridge `sortplaces` (off until measured).
- **Built, to land in order** (worktrees; each carries the one before; each changes the sim version): **C7.2 stone walls** (`wip/c72-stonewalls`, sim 102),
  **the memory pass** (`wip/memory`, 103 - must change no history: its check requires annals byte for byte), **F9.6-F9.7 the god's ore and bounty** (`wip/ore`,
  104), **C10.11 the lord's household** (`wip/hall`, 105 - measured first: lords' surplus for 1,260-1,907 people's food a year beside 312-1,387 landless
  people in their realms; a first household keeps landless households from its grain, a lord's realm's come to its seat; a new first takes them over).
- **The queue was running at the handoff** (runner = this session's background task `bj4o96476`, `bash tc2/out/tools/s23/runq.sh`; `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`, the scratchpad `lib.sh`'s `S`/`N`
  point at; if the runner has stopped and jobs remain in `$Q/todo`, restart it in the background): stone walls' tests
  (`0_zh_a2`), control and ten seeds; the memory pass's control and annals check (`0_zi_*`); the ore's control (`0_zj_a`); the household's build, control and
  ten seeds (`0_zk_*`); then seed 7's third acceptance run (`K_b_ac3_s7`). ~2 hours from 23:22.
- **Measured, not built**: C8.2 flight from war - harm concentrates on small frontier hamlets, but at the ceiling no village within reach has land to spare
  (`tc2/out/tools/s26/c82_flight.md`); the household slice is the first answer (the landless drawn to a lord's surplus, not to land).

## Do next, in order
1. Read each check's result (`$Q/log/<job>.log`, `tc2/out/tools/s26/results/`) and land in order: stone walls (D2-183), the memory pass and the ore (D2-184,
   D2-185), the lord's household (D2-186 if its ten seeds hold - a lean past |t| 2: its matched placebo `--oldhall --nudge Y` at each world's parting year,
   `tc2/out/tools/s24/lean.py`). Append the drafts to `docs/tc2/DECISIONS.md` with the numbers; build after any merge.
2. The fourth 1,000-year acceptance run on the final rules: `bash tc2/out/tools/s26/make_ac4.sh $Q` (main's build and full suite `bin_m14`; seeds 2/4/7 `_ac4`
   with books, the ways and stone walls by century; real shots `s26a_*` incl. `ShadowLite` beside, the villages most walled in stone, realms, the meteor's
   flames, the god's ore; the Web build `s26web1`: test, heap at year 1,000 against 955 MB, `sortplaces`/`shadowlite` paired). Regenerate it after the household
   lands (its `M_*` jobs build main at run time). Judge in the shots and books; write 25 §6 "Fourth run" and whether the core is finished (25 §8).
3. Then: the frame pass by the Web measures; F9's remaining powers (warmth needs a temperature overlay in `PlanetSystems.DayMean`) and F11 (packaging, settings).

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
