# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism (Control Room writes this file), not a stop condition. Stop only
for a real external blocker (credentials, payment, UAC, an irreversible product decision).

**Arjun's direction of 10 Oct 2026 (session 17) - presentation and simulation advance together**: he opened the game and saw people
standing still, houses on roads, a world that does not show its simulation, forests still alike. Order: the soil and the niche (done) ->
long/Web/visual checks -> a real-game visual truth audit (people, houses and roads, every system in the picture) -> W-FOREST pass 2
(climates must not be interchangeable in shots) -> continue the civilisation roadmap. For every new system ask "what changes in the world
picture?". No simulation theatre: presentation elaborates a real activity, never invents one.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 21)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 21"**: exact state, the job queue still running, "Next, in order".
3. `docs/tc2/SESSION_20_REPORT.md` - session 20 (session 21 has no report: its handoff block is the record).
4. `docs/tc2/DECISIONS.md` newest: **D2-154**, **D2-153**; the draft **D2-155** (the stubble, the culls by what loads the shortage, each household's
   field work dry-shod) is `tc2/out/tools/s21/d2155.md` - its "Evidence" waits for the 20-seed comparison.
5. On the branch (`C:/Game Dev/tc-stubble`): `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2k (the stubble; session 21's paragraph on the pigs),
   `docs/tc2/27_VISUAL_TRUTH.md` §9 (session 21's real shots), `docs/tc2/08_CIVILIZATION.md` (the culls).
6. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 21 entry first).
7. `git status`, `git log --oneline -8`, `git worktree list` (the stubble work is branch `wip/c37d-stubble` in `C:/Game Dev/tc-stubble`, tip 161fa63; the
   modified `.claude/launch.json` and untracked `Assets/Resources.meta` are not ours - leave them).

## Session 21 (16 Oct 2026, 19:12-22:05 IST) - context handoff; the job queue is still running
- **Running**: `tc2/out/tools/s21/runq.sh` (background task `ba1sj2iw2`), queue `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/
  4c5113ad-667d-4fde-9df2-13e8ffdf1a90/scratchpad/q` (`todo`, `done`, `log`, `runq.log`; `touch .../q/stop` ends it after the current job): at 22:00, 31
  jobs (~60 min) - 19 seeds `_u7` (the branch's final rules; the dry-way binary's eleven test fixtures passed at 22:10), control seeds 9-19 `_c6`, the comparisons
  `cmp_c6_r4`, `cmp_c6_u7_10`, `cmp_c6_u7_20` (in that scratchpad; runs in `tc2/out/civ`). One job at a time - start no other heavy job until it ends.
- **Done on the branch, verified**: the pigs traced and fixed (the hands' cull by what loads the hands, `--oldhands`); a household's field cell walked
  to dry-shod (`--wetfieldspot`); the stubble drawn dun as eaten; muck heaps clear of croft, pen, woodpile, cart; shot tools (`--doy`, `muck` views);
  herd diagnostics, `wet --doy`, `crowds`. Controls byte-identical; ten seeds `_u4` at par in people (pigs x0.80 n.s., lost in 1 world of 9 that had
  them); Unity C# compiles; real shots judged (`27` §9).
- **Next**: (1) read the queue's results (20 seeds: people at par, pig-years within noise) and merge the branch (`--no-ff`), write
  D2-155; (2) the full suite and the Web build and test; (3) nobody walks through the water (woodlots, the well's spot, crofts, wild-food spots - the
  `wet` audit's 0.10-0.14 % in the control); (4) a governance slice (one lordship of two villages in a 30-village world at year 300) or holdings on
  the ground. Details: `docs/tc2/SESSION_HANDOFF.md` "Session 21".

## Session 20 (13 Oct 2026, 17:25-~21:00 IST) - stopped by Arjun; nothing running
- Done and verified on main: **D2-154** the village builds round its burial ground (`--oldgraves` = `_m1`) and graves drawn as graves; stumps on
  felled-bare ground; the threshing floor drawn in use; shot views `floorbusy`, `act:<Activity>`; **C3.7c the muck merged** (sim version 79; ten
  seeds `_n1` at par).
- **On branch `wip/c37d-stubble`, NOT merged**: C3.7d the herds on the stubble and the fallow, each household's own field cell (`FieldSpots`), a food's
  cull by what the spares eat; controls byte-identical; ten seeds: people at par, fields x1.017, cattle x1.84 - **but pigs die out in 3 of 10 worlds**.
- **Next**: (1) find why the pigs die out where cattle grow (end causes and `stock` lines with `--places`, seeds 5/7/11 `_u2` vs `_n1`), fix with its
  own switch, ten seeds, merge, shots of the herds on the stubble; (2) the full suite on the final HEAD and the Web build and test (saves v79/v80:
  new worlds); (3) shoot 107c100's woodlot framing (it compiles: `unity.mjs cc` on 4dd9b94) and the yards' muck heaps; (4) the next civilisation slice.
- **Resource rule (Arjun D-1/D-2)**: one low-priority job at a time while RAM < 86 % and CPU < 85 % - use the queue in `tc2/out/tools/s20/`
  (`runq.sh`, `lib.sh`; set `N`/`S` to the new scratchpad). No subagents.

## Session 19 (13 Oct 2026, 14:53-~17:10 IST) - context handoff; queues still running
- Done and verified: the full suite on 86b481d 372/372 (**stale for HEAD**: run it once on the final HEAD); **D2-148** W-FOREST pass 2b (b312f01);
  **C3.7b the threshing floor** (merged 14a9156; sim version 77); **W-FELL** stumps, **D2-151** burial grounds (sim version 78), **D2-152** standards
  (4caf9ee) - tests, Unity C#, the merge's control `--nothresh --oldrain` seed 4 = `_g4` byte-identical.
- **Running at the handoff** (detached bash queues in the scratchpad `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/
  f50753be-dec6-4e7f-8c7e-01e86c05c761/scratchpad`, logs `q19.log`, `q20.log`, `q21.log`; copies of the scripts in `tc2/out/tools/s19/`; do not
  start another heavy job until they end): q19 ten seeds `_m1` (sim version 78) -> `cmp_r1_m1.txt`; q20 real shots `tc2/out/shots/s19e20`,
  `s19f20`, `s19g4`, `s19g28` (threshing floor and ricks, stumps, graves, standards, the town, the four climates); q21 the muck worktree's build,
  tests, control `--nomuck` = `_m1`, seed 4 `_k0`. If the logs are gone, rerun from `tc2/out/tools/s19/q19.sh`-`q21.sh` (fix `S=`).
- q19 so far (17:10): seed 4 `_m1` 38.2 M FU threshed in 1.60 M h (0.042 h a FU), 35,687 burials, 2 dead with no grave; seed 5 10.3 M FU in
  0.44 M h (0.042), 8,967 burials, 1 with no grave; seed 1 5.7 M FU in 0.24 M h (0.043), 5,276 burials, 3 with no grave; seed 6 15.7 M FU in 0.66 M h (0.042), 14,459 burials, 1 with no grave; seed 11 18.8 M FU in 0.79 M h (0.042), 16,861 burials, 0 with no grave; seed 2 22.7 M FU in 0.95 M h (0.042), 19,861 burials, 3 with no grave; seed 3 20.0 M FU in 0.84 M h (0.042), 18,808 burials, 5 with no grave; seed 7 14.6 M FU in 0.62 M h (0.042), 13,767 burials, 0 with no grave; seed 8 16.3 M FU in 0.69 M h (0.042), 15,210 burials, 5 with no grave; seed 42 28.8 M FU in 1.21 M h (0.042), 27,045 burials, 4 with no grave.
  **q19 done (17:22): ten seeds `_m1` against `_r1` (`cmp_r1_m1.txt`) - threshing at par**: people over years 200-299 x1.000, births and deaths a
  person-year x0.995 / x1.000, hunger a person-year x1.03 (t 0.45), famines x0.96, relief x0.87 (t -1.6); **fish caught x0.94 (t -3.1)** - the
  floor's hours come out of the free hours the wild and the shore took (to confirm in `timeuse`, then record in D2-149). The comparison `cmp_r1_m1.txt` comes when all ten seeds end.
- Next: read q19-q21's results, judge the shots and fix what reads wrong, finish C3.7c (D2-153), the full suite on the final HEAD, the Web build
  and test, SESSION_19_REPORT.md - details in `docs/tc2/SESSION_HANDOFF.md` "Session 19".
- **Resource rules (Arjun, Decision Inbox D-1, D-2)**: one low-priority job at a time (`cmd //c start "" //belownormal //wait //b ...`) while CPU < 85 %
  and RAM < 86 %; gate every heavy job (`tc2/out/tools/s19/q*.sh`'s `gate`/`low`). Pitfalls hit this session: a local named like one in an
  enclosing scope fails the build (CS0136 - `grounds`, `alt`); `place --seasons` and the shot views' logs give latitude and soil water.

## Session 18 (11 Oct 2026) - stopped by Arjun at ~16:00 IST; nothing of ours running
- Done and verified: **D2-144** (the gardens keep their village's own calendar), **D2-145** (a croft counted once, one wattle fence, beds by season),
  **D2-146** (a COARSE day's garden by the lived urges - the full suite's two failures), **D2-147** (a drowned village's households find a lot at once -
  a latent hang its seeds found), the 900-year check (seeds 4 and 42), the Web build and test `s18web1` 6/6. The full suite on c7edc68 was running at
  the stop - see `docs/tc2/SESSION_18_REPORT.md`; run it once if it did not finish.
- Next: **W-FOREST pass 2b** - `git apply tc2/out/tools/s18/pass2b.diff`, validate with `tc2/out/tools/s18/q10.sh`'s steps, write D2-148 from
  `tc2/out/tools/s18/d2148.md`. Then C3.7b threshing / C3.7c muck and the visual-truth items. Details: `docs/tc2/SESSION_HANDOFF.md` "Session 18".
- **Resource rules (Arjun, Decision Inbox D-1, D-2)**: one low-priority job at a time (`cmd //c start "" //belownormal //wait //b ...`) while CPU < 85 %
  and RAM < 86 %; gate every heavy job (`tc2/out/tools/s18/q*.sh`'s `gate`/`low`).

## Stopped by Arjun (10 Oct 2026, 12:45 IST, session 17's third context) - nothing running
- HEAD = a8a45a9 + a WIP commit: **uncompiled** seasonal garden beds (`Gardens.Shown`, `WorldObjectsView.GardenBeds`, kit `garden_bare`; huts' crofts
  drawn) - run `node tc2/scripts/unity.mjs cc` first. The full suite was stopped mid-run (no result).
- **Bug found, not fixed**: the garden's work uses the unshifted day of the year (`AgentSystem.cs` ~577, ~1163, ~1989) while southern villages keep
  their crop calendar 24 days on - southern gardens run on the wrong days. Fix it first (one helper, a test, ten seeds). Details and the next steps:
  `docs/tc2/SESSION_HANDOFF.md` top block "Session 17, third context". Shots of gardens need `--days 6-10` or `--days 14-28` (a year-300 save opens
  on the year's first day, nothing dug).

## Where things stand (10 Oct 2026, ~12:35 IST, end of session 17's second context) - nothing running
- **main 93ad626**, verified: headless build, GardenTests 4/4, DefenceTests 8/8, `node tc2/scripts/unity.mjs cc` (the Unity C# compiles).
  Contains D2-141, **D2-142** (W-FOREST pass 2's simulation side: `--oldwoods`), **D2-143 C3.7a the garden** (sim version 76, `--nogarden`).
  Ten seeds each: `_v4` (D2-141), `_w1` (D2-142, at par per person, people x1.10), `_g1` (D2-143, at par); controls byte-identical.
- **Not yet done**: the full suite on 93ad626 (last full run 366/366 on the woods' source, before the garden); **real shots of the gardens**
  (beds on the crofts and people hoeing behind their houses are compiled, never seen); the 900-year check; the Web build and test (the save
  format is v76 - earlier saves load only in older binaries).
- **Prepared, not applied**: W-FOREST pass 2b - `tc2/out/tools/s17b/p_drytropics.py` (the rain belt follows the sun in the outer tropics,
  `--oldrain`) and `p_drytropics_view.py` (the dry woodland's leaf fall from the soil's own water). Why: `place --seasons` shows the dry
  tropics' soil water at 0.15-0.25 in every season - the shots' dry tropics and wet woods read alike because there is no dry season in the state.

## Do next, in order
1. **The full suite once** on 93ad626 (`node tc2/scripts/headless.mjs test`; ~17 min; fix failures). Then **real shots of the gardens**:
   copy `tc2/out/tools/s17b/*` into the new scratchpad (set `S=`), then `bash shots.sh s18a "<repo>/tc2/out/civ/s4_g1/world_y300.tc2"
   low,close,village,street 10` and `SHOTX="--village town" bash shots.sh s18b <same save> townlow,townstreet,townclose 10`; read the Unity log's
   "people drawn N (at work or on the move M)" (a loaded save is shown only on its lived day); look at the PNGs: beds behind houses, people
   hoeing in them, nothing drawn over streets. Fix what the shots show; commit.
2. **The 900-year check** (`l9.sh`, `BIN=` a fresh build of HEAD, `TAG=g1`, seeds 4 and 42 in parallel) and **the Web build and test**
   (`webgate.sh s18web1` - one Unity build alone).
3. **W-FOREST pass 2b**: apply both patches (`python p_drytropics.py`, `python p_drytropics_view.py`), build, the control `--oldrain` (seed 4) must
   equal the HEAD binary's run byte-identically, ten seeds (three lanes, `q8.sh`'s pattern), `place --seasons` before/after (seed 42), shots of the
   dry tropics in the wet and the dry season and of four climates side by side (`sheet.py`) - DECISIONS D2-144; keep iterating until the climates
   are not interchangeable (Arjun judges W-FOREST - never mark it done without his judgement in real shots).
4. Then C3.7b the threshing floor and C3.7c the muck (`26` §2h), and the remaining visual-truth items (`27` §6-§8: the bed's season drawn,
   headmen not drawn, famine weak, stumps of felled woods).

## Tools (`tc2/out/tools/s17b/`; copies of this session's - set `S=`/`SCR=` to the new scratchpad)
- `bld.sh` (BIN, TESTS, GATE: build the headless host, copy to the scratchpad, run test filters), `gwrun.sh` (gated seed runs; `EXTRA=` switches),
  `cmp.py A B seeds`, `rates.py TAG seeds`, `decades.py A B` (people by 20-year spans), `shots.sh TAG SAVE VIEWS [HOUR]` (`SHOTX=` extra args),
  `sheet.py OUT COLS W H imgs` (contact sheets), `l9.sh`, `webgate.sh TAG`, `bench.sh` (back-to-back timing), `q8.sh` (build, control, ten seeds
  in three lanes, cmp, time use), `pt.py` (line-ending-aware patching), `v75.py`, `mkcc.py`.
- Headless: `timeuse --load SAVE --days 48 --daylight --stages [--nogarden]`, `place --load SAVE --crofts | --housing | --seasons | --sheds |
  --woods | --roads | --still`, `test --filter X [--noniche --nowallworks --noworkclear]`; run switches `--noniche --nowallworks --noworkclear
  --oldwoods --nogarden`. `node tc2/scripts/unity.mjs cc` compiles the Unity C# without the editor (shaders need the real editor: `shots`).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push.
- **No subagents.** Resource Governor (Control Room) was **HIGH** at the handoff: CPU 90 %, RAM 92 %, up to three heavy jobs, never two builds or
  suites at once; other applications hold RAM at 65-96 % - gate heavy jobs (RAM < 88 %), never kill processes you did not start. Check the active
  policy in the session's Control Room notices.
- Start background queues with `exec bash q.sh > log` (stopping a `cd X && bash q.sh` task leaves the script running) and never edit a running
  script above its current line. Write patch scripts with the Write tool (Bash heredocs mangle quotes); never `sed -i` a CRLF file.
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it - cast floats to double before subtracting). A local named
  like an enclosing one fails the headless build (CS0136). Judge simulation changes by ten seeds against a control on the same binary
  (a `--no...` switch reproducing the previous runs byte-identically); wall time is not cost on this machine - compare the reports' `events`.
- The save format is v76: v75 saves (`_w1` and older) load only in a v75 binary (build an older commit for tools on them).
