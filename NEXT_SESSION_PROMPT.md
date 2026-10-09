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
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 16, fourth context"**: "Exact state", "Next, in order".
3. `docs/tc2/DECISIONS.md` newest entries **D2-138** (a headman gone to live with their child), **D2-137** (the Web catch-up's out-of-memory: two
   villages on one spot), **D2-136** (the old hand on their holding), D2-135, D2-134, D2-133.
4. Domain docs: `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2d-§2f (the land phase C3.6), `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12c-§12e (headmen);
   `docs/tc2/PROGRESS.md` CURRENT STATE.
5. `git status` (the modified `.claude/launch.json` and untracked `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them),
   `git log --oneline -8` (HEAD 4b6572f or a handoff commit on it).

## Where things stand (10 Oct 2026, ~04:00 IST, end of session 16's fourth context - stopped by Arjun)
- **Resolved and verified**: the Web catch-up's out-of-memory (D2-137, 741565c: a Living catch-up of a small world is ACTIVITY - headless `run --seed 42
  --years 96` is the browser's world; two villages founded on one cell and visitors walking the way of no length between them forever), and a
  headman gone to live with their child (D2-138, 4bf4888). Full suite 358/358 on D2-137; Web test `s17web1` 6/6 on 4bf4888.
- **900 years with C3.6e** (seed 4 only, `s4_l9k.csv`): hunger 8.7-11.2 a 1,000 person-years in centuries 300-600 (before 1.0-5.1).
- **Work in progress - C3.6f the soil (D2-139, sim version 74, 976c54c)**: the fields tire, villages rest a share, the herds' dung gives back, the
  people judge their land by what it bears; `--nosoil` = the worlds before, byte-identical. Seven of ten seeds measured (`_s2`): worlds carry x0.55-0.7
  of their people, bound villages rest 13-17 % - **but births do not answer** (42 a 1,000 person-years before and after) and hunger a person-year rose
  x1.9: the C3.5 marriage rule does not bind (couples bring two years of bread). **Not done**: `_s2` seeds 7, 8, 42; the full suite on sim 74.
- **Started - C3.5b the niche** (off by default, `--niche`): `CoupleLiving` (the land and hands a couple would have, at the last harvest), its tally,
  two tests - **the tests and a `PortionFor` fix are committed but not compiled or run** (the machine stayed over its RAM ceiling).
- **Waiting on Arjun**: W-FOREST (D2-130) until he judges the shots (`tc2/out/shots/wf2/sheet4.png`, `wf4/forest.png`, `wf6/before_after_far.png`).

## Do next, in order
1. Build the headless host (`node tc2/scripts/headless.mjs ...` builds; or `dotnet build` in `tc2/headless/app` with `--disable-build-servers`) and run
   `HouseholdFormationTests`, `TilthTests`, `LandTests`, `HoldingsTests` - fix what fails.
2. The three missing `_s2` seeds (7, 8, 42; niche off), the full suite once; close D2-139 with its ten-seed numbers (DECISIONS, PROGRESS).
3. **C3.5b the niche**: `_n0` (seeds 4, 42 with `--nichetally`: must equal `_s2`) and `_n1` (ten seeds `--niche`) - see `tc2/out/tools/q_n1.sh`;
   adopt it (D2-140, `--noniche` control) if births answer the land (hunger a person-year back toward `_k2`'s 4.7).
4. The soil's 900-year check (seeds 4, 42), the Web build + Web test, the soil's fallow in real shots; then herds on the stubble and fallow, a land
   market, partible customs (C11), work for grain.

## Tools (local; `tc2/out/tools/` has copies - put them in the session scratchpad and set `S=`/`SCR=` in the scripts)
- `wrun.sh` (ten-seed runs; `EXTRA=` switches), `loadrun.sh` (saves run forward), `cmp.py A B seeds`, `gap.py`, `pt.py` (line-ending-aware patching:
  `exec(open(pt).read()); patch(path, [(old, new)])`), `gate.sh CMD` (runs once RAM is under 72 %), `webgate.sh TAG` (Web build + Web test),
  `l9k.sh SEED` (900 years from a `_k2` save).
- Headless switches new this context: `run --hot N` (who began the most activities between reports), `--nojoin`, `--nostanding`; `place --near`
  (villages closer than the spacing), `place --heads` (successions: after a death, a living move, a retirement). `webtest.mjs --export` writes the
  game's own saves from IndexedDB (before the day away, every half minute of the catch-up) for headless replay (`run --load F`; a save of another
  sim version can be replayed by setting the header's int at byte 6, the format being unchanged).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push. New `.cs` files
  need their Unity `.meta` committed (a Unity build makes them).
- **No subagents. Resource Governor LOW since 23:10 on 9 Oct: CPU 50 %, RAM 75 %, ONE heavy job at a time** (a ten-seed batch runs its seeds one after
  another; a Unity build runs alone). Other applications keep RAM at 80-90 %: gate heavy jobs on RAM (`gate.sh`); never kill processes you did not
  start; `dotnet build ... --disable-build-servers` leaves no compiler servers behind (`dotnet build-server shutdown` after `headless.mjs test`).
- Line endings differ by file (docs and most Core files CRLF; tests and some files LF): patch with `pt.py` or read/write bytes. **Never `sed -i`
  in Git Bash - it strips CRs from CRLF files.** Bash heredocs mangle backslashes and quotes: write patch scripts with the Write tool.
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it); judge simulation changes by ten seeds against a control on the
  same binary (a `--no...` switch that reproduces the previous runs' annals byte-identically); aggregate counts move x0.8-x1.3 between reruns.
  Presentation never invents state. The Web test's "day away" is ACTIVITY for small worlds: its headless twin is `run --seed 42 --years 96`.
