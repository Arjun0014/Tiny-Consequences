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

## Where things stand (9 Oct 2026, ~23:30 IST, session 16's fourth context)
- **Resolved**: the Web build of C3.6e failed the Web test's catch-up (out of memory). A Living catch-up of a small world is lived in ACTIVITY (not
  COARSE: under 40,000 person-years), so headless `run --seed 42 --years 96` is the browser's world. Two parties had founded villages on one cell; their
  visitors walked the way of no length between them forever and filled the travel index. **D2-137** (sim version 72, 741565c): a founding party joins a
  village standing at its place; a way of no length arrives where it goes; the travel index sweeps stale legs. Ten seeds byte-identical to before.
  Full suite 358/358.
- **D2-138** (sim version 73, 4bf4888): a headman gone to live with their child (C3.6e) is no contested vacancy; the household that took the first
  household in is the incumbent; a household's clients follow the household that took it in. Ten seeds (`_k2`): people at par, headmen x0.83.
- **Verified in the browser**: Web build of 4bf4888, Web test `s17web1` 6/6 (the day away to year 96 in 106 s, 487 people in 16 places = headless).
- **Waiting on Arjun**: W-FOREST (D2-130) until he judges the shots (`tc2/out/shots/wf2/sheet4.png`, `wf4/forest.png`, `wf6/before_after_far.png`).

## Do next, in order
1. **The land economy over 900 years again** (C3.6e with D2-137/138): `bash gate.sh bash l9k.sh 4`, then `... l9k.sh 42` (scratchpad scripts, copies
   in `tc2/out/tools/`; from the `_k2` year-300 saves, 600 more years, ~20 min each); compare with `s4_l9`, `s42_l9` (sim 70): hunger a 1,000 person-years
   by century, the landless share, the land-rich households' days of grain (`place --landless`), tributes (D2-138 measured 17 -> 8 over ten worlds).
2. **Choose the next slice from that evidence**. Measured at year 300 (`_k2`): land binds (bound villages' arable 0.68-0.75 of their need; working
   harder adds 10-15 % to their harvests), 98 % of hunger deaths are in landless or land-short households, and bound villages keep 0.34-0.42 beasts a
   field hectare against 0.69-1.11 in open ones. **There is no soil fertility in the model at all** (yield = FieldHa x a constant x growth x sown x
   tilth): no fallow, no manure - so beasts are worth their milk and meat but nothing to the land, and a surplus has no productive sink. Candidates:
   the herds' dung / fallow (a phase: it changes world scale - measure first), work for grain, each household working its own holding (service),
   a land market, partible customs (C11).
3. Run the full suite once at the end of the next slice (none since D2-138; targeted LeadersTests, LandTests, MigrationTests pass).

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
