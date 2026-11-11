# Next session — start here

## FIRST - session 32 (4 Nov 2026): Arjun's FINAL SHIP PUSH (powers with real reactions, the butterfly tree, camera, look, opening). Read `docs/tc2/SESSION_HANDOFF.md` "Session 32" first - it overrides the session 31 lines below.
- **STANDING DIRECTION (Arjun, 4 Nov ~20:25): "dont wait for my reports, you do the final decision and do all properly, you can run up to 2 subagents"** - decide and ship autonomously; QA the game yourself with fresh eyes; up to two subagents in worktrees, merged by the main session after reading their diffs.
- **Arjun's ten-point report at ~19:13 (D2-207) was worked through** (speed as lived, reach shrunk, eruption fixed, shock crowds, the 23-step guide, A Tiny World, the book's wheel, the tree years on with a seal at the top right); then every power checked, people alive, the cull's refusal and the air's harm (D2-207 9-13); **THE SUBMISSION BUILD IS P (code 531cd34) in `tc2/web-build/web`** (M + rain at once, the studio's intro, phones in landscape) (`web.*`, "A Tiny World"; a copy at :8706; the itch zip `tc2/web-build/a-tiny-world-itch.zip`, store GIFs in `tc2/web-build/store_gifs/`) - SESSION_HANDOFF "Session 32" "Context 2" has its verification. Nothing is owed but Arjun's word; not pushed.
- **Context 2 (from 18:44 - final QA and the submission build; read SESSION_HANDOFF "Session 32" "Context 2" - it overrides the 18:05 lines below)**: QA on build g done (`s32first3` 8/8, `s32web2` 9/9); three presentation fixes (563f0b7 lone emergents, e5e9520 the cast's word above the guide, 77c8728 the tree's cards); candidate 1 (e5e9520) served at http://127.0.0.1:8701/, build g at :8700; **the final submission build is 77c8728, built straight into `tc2/web-build/web` (`web.*`)** - its verification and Arjun's play of it are in that section. Submission mode: no features, fix only blockers, do not push unless Arjun asks.
- **State at the 18:05 context handoff**: everything committed on `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, edits there), main fast-forwarded to it; Web build g FINISHED and is live in `tc2/web-build/web` (files named `web_next.*`; it lacks only the wound-wording fix f9cd929, merged after it started); not yet tested. All P0/P1 items of Arjun's brief are built and seen in Web builds b-f (powers' reactions, the tree, the grab, the look, the opening, the book). Build f: webtest `s32web1` 9/9, firstrun `s32first2` 8/8.
- **This session's brief (Arjun, 4 Nov)**: final ship push, ~7 h from 15:50 - submission ~22:50 IST. Now: STOP adding features; play the Web build, fix blockers and high-visibility problems only, commit continuously. Up to 2 subagents (worktrees `C:/Game Dev/tc-s32tree`, `tc-s32face`; branch per task, the main session merges). Never draw on the RX 5600M (gpusafe); runs silent; no long sim runs.
- **Do next**: SESSION_HANDOFF "Session 32" "Next, in order" - (1) on build g: `node tc2/scripts/firstrun.mjs --tag s32first3`, rebuild main's headless (`node tc2/scripts/headless.mjs floatcheck` in main) then `node tc2/scripts/webtest.mjs --tag s32web2`; judge `tc2/out/webtest/s32pages1`; (2) done - full suite 537/0/0, `wip/s32-qa` merged; (3) the final submission build straight into `tc2/web-build/web` (scratchpad `webbuild.sh`), then firstrun + webtest + `play.mjs --steps $SP/pow5.txt` on it; (4) docs final, commit; push only if Arjun asks (as Arjun0014).
- **Tools made this session**: `node tc2/scripts/play.mjs --steps FILE [--save F] --tag T` (scripted play: cmd/wait/shot/move/click/drag/wheel/key/until/state); bridge `tune NAME VALUE` (Look tunables live), `tree last|ID|off`; `unity.mjs cc --ccdir NAME`; scratchpad `$SP` = `C:/Users/<user>/AppData/Local/Temp/claude/C--Users-<user>--claude-spares-spare-teKswW/96506e8d-2507-4a95-8768-c2f876809edc/scratchpad` holds `webbuild.sh`, `webbuild2.sh`, the play scripts (pow1-5, look1-3, cam1, menu1, pages1). For a tools' year-300 save, run `speed 5` to Day 2 09:00 before casting in builds before f.

## FIRST - the state at the end of session 31 (1 Nov 2026; these lines override everything below)
- **Arjun stopped the session.** He played the build of f301037 (new interface, old world look) and said "so many issues" without listing them, then asked to stop the server and close every process the session started: done, nothing of ours runs. **Ask him what the issues were before doing anything else.** Do not start a build, a server or a heavy job on your own.
- **A Web build waits for his word**: the other game's session must have finished and RAM be about 60 % or less (the last build peaked at 98 % from a 65 % start, and Claude Code stopped its wrapper for low memory). He cannot be read from process lists: the other session's Claude processes stay open when idle.
- **What is verified**: main = `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, every edit there), committed, not pushed; sim 108, no file under `Assets/TC2/Core` changed. `unity.mjs cc` compiles everything. Only f301037 ran in the game, in `tc2/out/webtest/s31a` and `s31b` (bookshots: no exceptions) and Arjun's own play.
- **What is not verified**: everything after f301037 has never run (portraits, life story, initials, realm maps, links, follow chip, zoom toward the pointer is in f301037 but untried by hand, the world's look pass, stars, sky dome, clouds) and Unity has not compiled `SkyDome.shader`, `Stars.shader` or the changes to `Terrain.shader`, `Clouds.shader`, `TC2CloudForm.hlsl`. The guide has never been walked by real clicks (`tc2/scripts/firstrun.mjs`, needs the next build). Sound was levelled, never heard. `webtest.mjs` and the full suite did not run this session.
- **Then**: `docs/tc2/SESSION_HANDOFF.md` "Session 31" (exact state, steps) and D2-205. Work his list first, then its "Next, in order". The sections below are older: "FIRST" and "Do next" further down describe session 30.

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism, not a stop condition. Stop only for a real external blocker.
**No subagents.**

**Arjun's direction (1 Nov 2026, session 31) - CURRENT, over the one below: make TC2 look and feel like an absolutely beautiful, complete, polished game that happens to contain an extremely deep simulation.** Whenever there is a choice between another invisible mechanic and making an existing one beautifully readable, choose readability, presentation and game feel. Simulation changes only for a genuine broken rule, and the smallest real fix. Another game's session may share the machine: keep heavy work to a minimum, no seed batteries or 1,000-year runs; when the machine has no room, keep going with lightweight work and queue the validation. **A Web build peaks near 98 % RAM from a 65 % start: start one only when the other session has finished and RAM is about 60 % or less.** State and next steps: `docs/tc2/SESSION_HANDOFF.md` "Session 31", D2-205. **Start there**: the "FIRST" and "Do next" sections below describe session 30. Only f301037 has been seen in a Web build; everything later compiles but has never run in the game. Edits still go in the worktree `C:/Game Dev/tc-s27int` on `wip/s27-int`.

**Arjun's direction (28 Oct 2026, session 29): "THE JAM MUST CLEARLY READ AS A REAL SIMULATION TO THE PLAYER."** Every feature answers: what is
happening, who is doing it, why, from what resources/relationships/history, what changed, how the player can inspect and follow the chain, and how the
picture shows it. The player's own casts must be traceable: power -> immediate effect -> people/places affected -> secondary consequences -> long-term
history. Never fabricate an explanation: read the real state and the causal ledger; if a truth is not kept, add the smallest real state. Order of the
brief: (0) the held perf check - DONE; (1) the long skip as an orbital timelapse - DONE (D2-199); (2) the person page as a person - DONE (D2-200); (3)
readability with progressive disclosure world -> realm -> settlement -> household -> person -> life/causes - DONE in large part (D2-201, D2-202); (4)
"Why?" from the ledger - DONE (D2-201); (5) the History Book as the world's memory - its sections, links and Your Actions' threads DONE (D2-201, D2-202);
(6) the powers as play - their word and live threads DONE (D2-202), the aim's words in a box (D2-203), keep polishing; (7) the visual world where it is
evidence of the simulation (orbit, clouds, region edges, W-FOREST, settlements, people, important buildings, roads, political centres, war scars, ruins) -
IN PROGRESS (D2-203: war scars - burned crops and homes, ruins that say how they fell; the fields' edges; the rivers' glint; the timelapse's seasonal edge;
D2-204: two villages' fields furlong by furlong, a skip's colours of one moment, the sea's true edge, the HUD's overlaps), judged in real shots.
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
- **The queue** (`Q` = `C:/Users/<user>/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`,
  hard-coded in `tc2/out/tools/s23/runq.sh` and `lib.sh`). **Its runner (`runq.sh`, a bash process started 28 Oct 22:24) was never stopped**: at the end of
  session 29 Claude Code only dropped its task entry; the process and its job ran on (`0_qq_web_s29g` ended rc 0 at 02:15) and it has run every job of
  session 30. Add a job by writing `$Q/todo/0_<name>.sh` (source `lib.sh`; `gate`, `low`; `$Q/done/0_rf_web_s30c.sh` for a Web job,
  `$Q/done/0_rd_shots_s30v5.sh` for shots, `$Q/done/0_rg_book_s30d.sh` for bookshots on the last build); hold one in `$Q/hold/` until you can judge
  what runs before it (the runner starts the next job the moment one ends). `touch $Q/stop` stops it after its job.
- **Done in session 30** (DECISIONS **D2-204** (1)-(9); SESSION_HANDOFF "Session 30"): D2-203 closed (full suite 523/0/0; `s29web7` 9/9; the charred ruins
  measured dark in `s29v7`); two villages' fields meet furlong by furlong; a cycle's colours of one moment (the skip's gold blocks); the sea on its true
  sphere a quarter metre down (the shore's faces); the cast's word beside Your hand; no map name under the lapse's cards; the years' leaf beside a page;
  the ticker on one line; a fire's ground in hectares; the air near the ground (a warm veil over high fields from 1.1-1.4 km); the fields read (the
  pointer over a village's fields, a click to its land); floatcheck clean again. Seen: Web `s30web1`-`s30web4` 9/9, `s30book1`-`s30book3`, `s30lapse1`,
  `s30air`; shots `s30v1`-`s30v6`.

## Do next, in order
1. (done) The full suite 524/0/0 with the lint; Web `s30web1`-`s30web8` 9/9; the fields' tag; every power cast and read (`s30pow1`, `s30pow2`); D2-204
   (1)-(11) closed, nothing pending. The queue is idle and main = the branch at the end of session 30.
2. Then the brief's open looks (W-FOREST's sameness is a dedicated art pass awaiting Arjun's judgement - `18` W-FOREST) and readability gaps the pictures
   show (validate: compile -> focused tests -> real Web play/shots -> fix -> commit; no seed batteries for UI). Keep D2-204, PROGRESS, SESSION_HANDOFF and
   this note current.

## Rules that bite
- **Run `node tc2/scripts/headless.mjs floatcheck` (from the worktree) after any change to `Assets/TC2/Core`**: the queue's full-suite jobs call the test
  binary directly and never run the lint (session 29 left float arithmetic in `Whys.cs` through a 523/0/0 suite; fixed in b49bbe3). Focused tests:
  build `tc2/headless/app` with `$DN build -c Release -o DIR`, then `$DN DIR/tc2.dll test --filter NAME` (a substring of Fixture.Method).
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
