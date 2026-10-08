# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 11"** (exact state, runs, known issues, "Next").
3. `docs/tc2/DECISIONS.md` newest entry **D2-118** (C13.3 a town's ground) and D2-115 (why C9.4 waits for exchange).
4. `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` - **the C3 design; §1 C3.1 is the next slice** (the weaver's workshop and the village's wares).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (session 11 entry); `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` and the
   modified `.claude/launch.json` are not ours - leave them), `git log --oneline -10`.

## Where things stand (8 Oct 2026, end of session 11)
- **C13.3 is closed** (commits e6ab82f, bebbf39; sim version 56): every building covers real ground (`Core/Places/Footprints.cs`, a recorded
  bearing `Building.Face`) - **no house ever stands in another** (C13.2 had 90-312 overlapping pairs a world; 0 in all ten seeds now); a town's
  (250+) street frontage near its green is divided into 6.5 m plots with rows of the narrow town house (`Architecture.Row`, kit `home_row_*`);
  the market place widens with traders and is kept open; streets/lanes/rings laid between houses; the river line lives in Core
  (`Core/Planet/RiverLines.cs`) - homes keep off it, its ribbon ends at the drawn waterline. 285 tests; ten seeds `tc2/out/civ/s*_d7` at par;
  Web test 6/6 (`tc2/out/webtest/c133web`, 64-120 fps; person/place pages 67-83 fps).
- **Open W task W-FOREST** (Arjun, 8 Oct): forests and biomes still read as the same trees recoloured - NOT solved; recorded in
  `docs/tc2/18_WORLD_READABILITY.md` §8 and `25_CIVILISATION_ROADMAP.md`. A dedicated ecology/art pass later, unless a C-slice touches it.
- Known cosmetic: a river into a sea cell across a wide beach can end on the sand short of the water (Vodrothorpe, seed 2).

## Do next
1. **C3.1 the weaver's workshop and the village's wares** (`26` §1; chosen over C10.2 because trade is 0-5 traders a year at most villages
   and lordships rarely hold). Hooks: lived choice in `Core/Agents/AgentSystem.cs` (~line 1009: the smithy's `Forge.Wanted` -> choice 15 - add a
   workshop choice and an `ActivityKind`), COARSE labour day (~line 1803, `Forge.Work`), household goods wear in `SettlementSystem` (~line 215:
   draw from the village's wares before making), `Trade.Value`/`Spare` (wares as a good), a new structure kind (Zone Edge) with a kit model,
   pages (building/person/settlement). Both resolutions, tests, ten seeds against `_d7` with a placebo, shots, Web.
2. Then C3.2-C3.6 (`26` §2); re-weigh C9.4 docks/cargo (`run --seashort`) and C10.2 when trade and polities grow; C13.4 suburbs.

## Tools
- Copy `tc2/out/tools/wrun.sh` and `cmp.py` to the session's scratchpad and set `SCR=` in `wrun.sh`; copy
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (sequential, ~15 min for ten).
- Headless: `place --load S --list | --layout --sid N | --overlaps [--sid N] | --households | --standing`; plot a layout's ground with
  `python tc2/scripts/plotfoot.py out.png layout.txt ... [--zoom R]`; run report lines `town:` (overlaps, cores, rows, market places),
  `forms:`, `streets:`, `trade:`; `run --profile` for costs.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "rowclose@10,rowlow@10,rowstreet@10,rowmarket@10,close@10,village@10"`
  (`row*`: the street with most narrow town houses / the market place; `hall`; `mouth`/`mouthfar`; `work<kind>`; `--ground` lays each home's
  simulated ground under it as a red plate).

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the two files above;
  never force-push. New `.cs` and model files need their Unity `.meta` committed (run `node tc2/scripts/unity.mjs configure` to make them).
- **No subagents. Keep the machine around 50 % CPU/RAM (11 GB, often <2 GB free); one heavy job at a time** (Unity build or shots, the full
  test suite ~12 min, ten-seed runs). Never kill processes you did not start.
- Bash heredocs break on long scripts with backticks/apostrophes in this harness: write patch scripts with the Write tool into the scratchpad,
  then `python file.py`; long commit messages via a file.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - build it from the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions; judge by
  century means over ten seeds and a placebo (`--nudge 150`) - conflict counts move x0.66-x1.5 under a one-FU nudge.
- The kit's models come into Unity with x mirrored (front +z): `Footprints.WingSide` = -1; check new asymmetric models with a `--ground` shot.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]`.
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=50,100,200,300 bash wrun.sh 4 42 2 1 5 3 11 8 7 6`; `python cmp.py d7 <tag> 4 42 2 1 5 3 11 8 7 6`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~11 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--only a,b] [--preview <dir>]`.
