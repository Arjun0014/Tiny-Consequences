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
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 17, second context)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 17, second context"**: exact state, findings, "Next, in order".
3. `docs/tc2/DECISIONS.md` newest: **D2-143** (the household's garden), **D2-142** (trees by the growing season's warmth), **D2-141** (the visual
   truth audit's view helpers and simulation rules), D2-140, D2-139.
4. `docs/tc2/27_VISUAL_TRUTH.md` (§8 the real shots), `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2h (the household's own year: the garden built,
   the threshing floor and the muck designed), `docs/tc2/18_WORLD_READABILITY.md` §8 (W-FOREST pass 2 and the pass 2b design).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 17 second-context entry first).
6. `git status` (the modified `.claude/launch.json` and untracked `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them;
   the worktrees `C:/Game Dev/tc-g2`, `tc-g4`, `tc-wild` are earlier sessions' - leave them), `git log --oneline -6` (HEAD 93ad626 or a handoff
   commit on it).

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
