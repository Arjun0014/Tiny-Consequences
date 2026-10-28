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

## FIRST - the state in session 30 (29 Oct 2026, from ~02:00 IST; Control Room run #86 session 3)
- **The branch `wip/s27-int` is the work** (worktree `C:/Game Dev/tc-s27int`, every edit there). **Each queued Web/shots job fast-forwards main to it at
  its start** (`git merge --ff-only wip/s27-int`); when the queue is idle and main is behind, fast-forward it yourself:
  `git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int` - never while a Unity job holds main's checkout. After any Web build:
  `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset` in main's checkout (the jobs do it). Sim **108** unchanged.
- **The queue** (`Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`,
  hard-coded in `tc2/out/tools/s23/runq.sh` and `lib.sh`). **Its runner (`runq.sh`, a bash process started 28 Oct 22:24) was never stopped**: at the end of
  session 29 Claude Code only dropped its task entry; the process and its job ran on (`0_qq_web_s29g` ended rc 0 at 02:15) and it has run every job of
  session 30. Add a job by writing `$Q/todo/0_<name>.sh` (source `lib.sh`; `gate`, `low`; `$Q/done/0_rf_web_s30c.sh` for a Web job,
  `$Q/done/0_rd_shots_s30v5.sh` for shots, `$Q/done/0_rg_book_s30d.sh` for bookshots on the last build); hold one in `$Q/hold/` until you can judge
  what runs before it (the runner starts the next job the moment one ends). `touch $Q/stop` stops it after its job.
- **Done in session 30** (DECISIONS **D2-204** (1)-(6); SESSION_HANDOFF "Session 30"): D2-203 closed (full suite 523/0/0; `s29web7` 9/9; the charred ruins
  measured dark in `s29v7`); two villages' fields meet furlong by furlong; a cycle's colours of one moment (the skip's gold blocks); the sea on its true
  sphere a quarter metre down (the shore's faces); the cast's word beside Your hand; no map name under the lapse's cards; the years' leaf beside a page;
  the ticker on one line; a fire's ground in hectares. Seen: Web `s30web1`, `s30web2` 9/9, `s30book1`-`s30book3`, `s30lapse1`; shots `s30v1`-`s30v6`.

## Do next, in order
1. Judge Web `0_rf_web_s30c` (6b0896a; `tc2/out/webtest/s30web3`: 7_after_a_century - no name under the headline cards; 9_person_life - the years' leaf
   not over the page; the ticker one line; 5h_your_actions - "having burned about N ha"), then `0_rg_book_s30d` (`s30cloud0` vs `s30cloud1` village.png:
   year 300's tan veil with a straight edge and vertical streaks at the left of the village view - is it the clouds?). Fix what they show; commit.
2. Then the brief's open looks (W-FOREST's sameness is a dedicated art pass awaiting Arjun's judgement - `18` W-FOREST) and readability gaps the pictures
   show (validate: compile -> focused tests -> real Web play/shots -> fix -> commit; no seed batteries for UI). Keep D2-204, PROGRESS, SESSION_HANDOFF and
   this note current.

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
