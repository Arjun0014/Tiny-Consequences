# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism (Control Room writes this file), not a stop condition. Stop only
for a real external blocker (credentials, payment, UAC, an irreversible product decision).

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun; "Where we are (session 16)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 16, third context"**: its **"OPEN BLOCKER FIRST"** paragraph, "Exact state", "Next, in order".
3. `docs/tc2/DECISIONS.md` newest entries **D2-136** (the old hand on their holding - the build that fails in the browser), **D2-135** (who leaves),
   **D2-134** (the harvest by land and labour), **D2-133** (household land), **D2-132** (the first sheaves), **D2-131** (working the land harder).
4. Domain doc: `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2d-§2f (C3.6a-e, the first sheaves); `docs/tc2/PROGRESS.md` CURRENT STATE (the "OPEN" entry first).
5. `git status` (the modified `.claude/launch.json` and untracked `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them),
   `git log --oneline -14` (HEAD 7abbaa5 or a handoff commit on it).

## Where things stand (9 Oct 2026, 21:35 IST, end of session 16's third context)
- **Committed and verified** (sim version 70, 26127b5 + cc1f436): C3.6a working the land harder (D2-131), the first sheaves (D2-132), C3.6b household
  land (D2-133), C3.6c the harvest shared half by land, half by labour (D2-134: **hunger a person-year x0.74** - the preventive check from the
  households' economy), C3.6d each household weighs leaving by its own land (D2-135). Full suite 353/353; Web test `s16web8` 6/6 (bit-identical).
  A 900-year check of that economy (`tc2/out/civ/s4_l9`, `s42_l9`): stable; seed 42 hunger 0.2-0.8 a 1,000 person-years for six centuries.
- **Committed, NOT verified in the browser** (sim version 71, 5dd947a): **C3.6e the old hand on their holding** (D2-136: a landholding household whose
  youngest is 60+ goes to live with a child's household, else a land-short one with hands, its land and grain with it; headless ten seeds: landless
  17-66 % -> 6-44 %, the decades-long hoards gone, births x1.06, hunger at par). **Its Web build fails the Web test's catch-up, deterministically**
  (`tc2/out/webtest/s16web9`, `s16web9b`: year 35 of 96, 243 people in 17 places, then the browser aborts out of memory and "memory access out of
  bounds"). Headless is healthy (ten seeds DEEP 300 years; six seeds COARSE 100 years at 4-5 years/s; managed heap at par with `--noretire`). The
  full suite has not been run on 5dd947a; targeted tests pass (Land 10, Sheaves 5, Tilth 4, DeepTime 8, HouseholdFormation 3).
- **Waiting on Arjun**: W-FOREST (D2-130) stays open until he judges the shots (`tc2/out/shots/wf2/sheet4.png`, `wf4/forest.png`, `wf6/before_after_far.png`).

## Do next, in order
1. **Resolve the browser catch-up failure of C3.6e first.** The browser's path: a new seed-42 world at 00:38 of day 1, lived to 12:21, saved,
   reloaded, then `OfflineProgress.PlanCatchUp` (Living mode: COARSE from the next midnight, ACTIVITY for the last two days) toward year 96, run by
   `Game/Host/SimHost.cs`'s catch-up loop. Its year-35 world (17 places) differs from a headless `run --seed 42 --coarse` (9-10 places at year 40):
   reproduce the browser's catch-up headless exactly (the lived opening, the save/load, the same plan and step budgets; `scratchpad/web42/world.tc2`
   holds the opening state lived headless), look for a runaway (households, parties, history, events) or an unbounded loop in `Retire`/`MoveTo`
   (a household moving into one that retires the same day? a party household? the eldest-last ordering?), and confirm with a Web build of 5dd947a
   with `Retire` switched off. Fix and re-verify (Web test 6/6, full suite) - or revert C3.6e if no cause is found, and record it either way (D2-136).
2. Then the next slice from the evidence (SESSION_HANDOFF items 6-8): what the landless do with their days and where a surplus goes - work for grain
   (households with grain hire the hungry for their own projects at a day's bread a day), alms by standing in hard times, or each household working
   its own holding (service's economic base); then debts' consequences, partible customs by tradition (C11), a land market; the 900-year check again.
3. Web builds: `node tc2/scripts/unity.mjs build-web --jobs 3` (~6-8 min; RAM peaks ~95 % - run nothing beside it, edit no Core source while it
   runs), then `node tc2/scripts/webtest.mjs --tag NAME`.

## Tools (local, `tc2/out/tools/`; copy to the session scratchpad and set `SCR=`/`OLD=` in `wrun.sh`, `loadrun.sh`, `coarsescan.sh`)
- `wrun.sh` (ten-seed runs; `EXTRA=` switches), `loadrun.sh` (saves run forward: `BIN FROM TAG Y0 YEARS EXTRA`), `cmp.py A B seeds` (tags without
  the underscore), `gap.py TAG seeds`, `pt.py` (line-ending-aware patching: `exec(open(pt).read()); patch(path, [(old, new)])`).
- Headless switches added this context: `--landtally`, `--gaptally`, `--landholds`; controls `--notilth`, `--nosheaves`, `--noland`, `--bymouths`,
  `--villageleave`, `--noretire`; `place --heirs`, `place --landless`.
- Control runs on disk (`tc2/out/civ`): `s*_t0`/`_t1` (C3.6a), `_s0`/`_s3` (sheaves), `_h1` (holdings), `_c1` (land and labour), `_d1` (who leaves),
  `_e1` (retirement), `s4_l9`/`s42_l9` (900 years, sim 70).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push. New `.cs` files
  need their Unity `.meta` committed (a Unity build makes them).
- **No subagents. Resource Governor MEDIUM: CPU 70 %, RAM 85 %, at most two heavy jobs** (two ten-seed runs fit; a Unity build runs alone and its
  pressure notices are the build itself; the user's Edge windows hold ~2.6 GB). Never kill processes you did not start.
- Line endings differ by file (docs and most Core files CRLF; tests and some files LF): patch with `pt.py` or read/write bytes. **Never `sed -i`
  in Git Bash - it strips CRs from CRLF files.** Bash heredocs mangle backslashes and quotes: write patch scripts with the Write tool.
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it); judge simulation changes by ten seeds against a control on the
  same binary (a `--no...` switch that reproduces the previous runs' annals byte-identically); aggregate counts move x0.8-x1.3 between reruns.
  Presentation never invents state.
