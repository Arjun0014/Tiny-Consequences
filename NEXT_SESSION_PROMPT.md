# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 12"** (exact state, runs, known issues, "Next").
3. `docs/tc2/DECISIONS.md` newest entry **D2-119** (C3.1 the weaver's shed and wares; the lived save/load fix; works beside the road).
4. `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` - C3's design (§1 C3.1 as built, §2 the next slices).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (session 12 entry); `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` and the
   modified `.claude/launch.json` are not ours - leave them), `git log --oneline -10`.

## Where things stand (8 Oct 2026, session 12)
- **C3.1 is built** (sim version 57): measured first - goods sit at target, people idle ~8 h a day, home making costs only spare hours, fibre is
  what is short. So: wares last (0.6 of homespun's wear); a weaver's shed (`Structures.Workshop` 23) where a village's wear keeps six makers
  busy (larger places); its makers are the village's eight best weavers (`Settlement.LoomBar`), 1.5x pace and practice (`Person.SkCraft`), real
  fibre only; households take wares before making their own; wares a trade good (`Trade.Wares`) valued by the hours they save. Ten seeds
  against the same binary without sheds: trades x3.0, trade gain x3.8, home making hours x0.28, market places x1.35, people at par. Wares are
  mostly traded for fibre (the flax chain); grain bought with cloth is small. Kit `weavers_shed` + `tenter`; pages; headless `wares:`, `--noweave`,
  `--waresflow`; camera `work23near`.
- **Lived save/load is exact again** (pre-existing bug found by C3.1's tests): caches of what moves daily (lake levels, river flows) went stale in a
  running world - the village green, the drawn shore, the river lines. Rule: **never cache state that changes daily unless keyed to that day
  (`PlanetState.WaterDay`) or derived only from the ground's shape (`PlanetState.InBasin`)**; river lines are laid for every reach of 10 ha or more
  and asked daily whether they are streams (`RiverLines.Stream`). Test `SaveLoadContinuesIdenticallyWhileLakesRiseAndFall`.
- **Works on the main road are laid beside it** (a shed stood in the road): `Structures.RoadClear`.
- **C3.1b more sheds (D2-120, sim version 58)**: another shed where the households drain the store most of a year with fibre to weave and the people
  in wares would outwear the looms (`Weaving.Tally/Year/NeedsMore`); towns of 500+ carry 2-4; makers at the shed nearest home. Reference runs
  `tc2/out/civ/s*_w7` (C3.1b), `s*_w4n` (no sheds).
- Open W task **W-FOREST** (Arjun, 8 Oct): forests/biomes still read as the same trees recoloured - not solved (`18` §8).

## Do next
Everything of session 12 is committed and verified (last commit 506d254; 294 headless tests at 14b5124, Weaving 8/8 and SaveLoad 8/8 at HEAD; Web
test 6/6 on the C3.1b build `tc2/out/webtest/c31bweb`; the Web build in `tc2/web-build/web` is current).
1. **C3.2 the potter's kiln** (`26` §2) - **surveyed, not designed in code yet**: `run --rot` (a tools tally, trajectories unchanged) shows
   **14.6-17.3 % of every harvest rots in store** (seed 4: 6.78 M of 39.3 M FU; seed 7: 2.66 M of 18.3 M; heaps beyond the granaries <0.5 %) under
   today's 12 %/year x the village's best potter's 0.65 (`Knowledge.Tech.PotteryKeeps`, `SettlementSystem` ~line 510) - pots have real work.
   Measure first as C3.1 did (what households and stores hold, labour, clay: `Learning.Clay`), then the weaver's shed's pattern: a kiln raised by
   need where a potter lives, makers by skill (`LoomBar`-style bar), jars from real clay and fuel (charcoal or wood), the store's rot read from the
   jars it holds (not the best potter), households' jars for cooking fuel; ten seeds against a `--no...` control on the same binary; shots; Web.
2. Or **C3.3 exchange between households** (payment in kind; export looms would then pay their makers - D2-120 Open).
3. Roles read from practice for smiths, potters, boatwrights (as the weaver's now) - a year-300 person is "known for" every craft.
4. Then C3.4 the mill; re-weigh C9.4 docks/cargo and C10.2 when trade and polities grow. W-FOREST stays open for a dedicated art pass.

## Tools
- Copy `tc2/out/tools/wrun.sh`, `cmp.py`, `wares.py` to the session's scratchpad and set `SCR=` in `wrun.sh`; copy
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (sequential, ~15 min for ten).
- Reference runs: `tc2/out/civ/s*_w7` (C3.1b, saves 50/100/200/300, `--waresflow`), `s*_w4` (C3.1) and `s*_w4n` (same binary as `_w4`, `--noweave`).
- Headless: `place --load S --list` (now with wares, WEAVER'S SHED) `| --layout --sid N | --overlaps | --households | --standing`; `person --pick weaver`;
  run report lines `wares:`, `water caches:`, `town:`, `trade:`; `run --waresflow` (what wares were traded for), `--noweave`, `--rot` (grain rotted vs
  harvested); `tc2/out/tools/agg.py A B [A2 B2]` (report quantities' ratios over the ten seeds), `wares.py TAG seeds`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "work23near@11,work23@11,village@11"` (`work23`: the
  weaver's shed, a standing one first; `row*`, `hall`, `mouth`, `work<kind>`; `--ground`).

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the two files above;
  never force-push. New `.cs` and model files need their Unity `.meta` committed (run `node tc2/scripts/unity.mjs configure` to make them).
- **No subagents. Keep the machine around 50 % CPU/RAM (11 GB, often <2 GB free); one heavy job at a time** (Unity build or shots, the full
  test suite ~12 min, ten-seed runs). Never kill processes you did not start.
- Bash heredocs mangle backslashes in this harness (`\n` inside a Python string became a newline): write patch scripts with the Write tool into the
  scratchpad, then `python file.py`; long commit messages via a file.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - build it from the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions; judge by
  century means over ten seeds against a control on the same binary (a `--no...` switch) - aggregate counts move x0.8-x1.3 between reruns.
- The kit's models come into Unity with x mirrored (front +z): `Footprints.WingSide` = -1; check new asymmetric models with a `--ground` shot.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (the filter is a substring, not a regex).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=50,100,200,300 bash wrun.sh 4 42 2 1 5 3 11 8 7 6`; `python cmp.py w4 <tag> 4 42 2 1 5 3 11 8 7 6`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~11 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--only a,b] [--preview <dir>]`.
