# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted; presentation
never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material only - never
modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block "Session 9" (exact state, tools, runs, "Next").
3. `docs/tc2/DECISIONS.md` newest entries **D2-112 .. D2-114** (D2-113 C9.3 closed; D2-114 building variety, see the handoff for its state).
4. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 9 entry); `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §5; `docs/tc2/25_CIVILISATION_ROADMAP.md`;
   for D2-114 `docs/tc2/21_SETTLEMENT_ECONOMY.md` §6 and `docs/tc2/13_RENDERING_AND_ART.md` §12-13.
5. `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` is not ours - leave it), `git log --oneline -10`.

## Standing direction added by Arjun on 8 Oct 2026 (session 9): building variety
Settlements must not look like repeated same-colour, same-shape models. Houses vary by real circumstances (household size, means,
material at hand, age, the village's history, its building tradition); several house families with deterministic variants (footprints,
one/two storeys, roof shapes, roof and wall materials, timber/stone/plaster, extensions, chimneys, yards, wear) - never random RGB.
Functional buildings must be recognisable at a glance (smithy, training ground, granary, market, boathouse, mine/quarry, hall; later
towers, gates, keeps dominating the silhouette). Richer, larger, older places get denser and more substantial; a hamlet, a village, a
walled town and a political centre must not read as the same houses at different counts. Modular families + variants, Blender is free
to use; judge real screenshots at close, village and region views - never call it solved from code.

## Tools
- Copy `tc2/out/tools/wrun.sh` and `cmp.py` to the new session's scratchpad and set `SCR=` in `wrun.sh`; copy the binaries
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (two chains in parallel, ~15 min).
- Headless: `run --load S --lived` (a DEEP save back to lived days), `--voyagers` (each boat on a sea way), `--vtrace FILE` (each village
  each report: people, store, famine, hunger deaths, relief in/out by land and sea, who left), `--nosea` (no voyages or sea relief: A/B in one
  binary), `--nudge Y` (a placebo: one FU more in one store at year Y - the chaos of ten worlds measured); report lines `sea trade:`,
  `trade:` (relief, given back), `boats:`, `forms:` (D2-114: plans, roofs, covers, walls, chimneys, by village size).
- Shots: views `voyage`, `voyagenear`, `voyagefar`, `voyagestay` wait (lived) until a real voyage is in that state; `work<kind>`.

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the untracked
  `unity/TinyConsequences2/Assets/Resources.meta`; never force-push. New `.cs` files need their Unity `.meta` committed.
- **No subagents. Keep the machine around 50 % CPU/RAM; one heavy job at a time** (Unity build or shots, the full test suite; two headless
  300-year runs in parallel are fine). Web tests read low frame rates if anything heavy runs beside them.
- Bash heredocs break on backticks and apostrophes in this harness: write patch scripts, docs and commit messages with the Write tool into the
  scratchpad, then `python file.py` / `git commit -F file`.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - it must be a build of the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions and checked at
  par over pooled worlds; judge by century means over ten seeds, and size the noise with a placebo before calling an effect real.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (full suite ~12 min, 269 tests at C9.3's close).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=100,200,300 bash wrun.sh 42 7 4 2 1 3 5 6 8 11`; `python cmp.py TAG_A TAG_B seeds...`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "village@10,close@10"`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~16 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out <dir> [--only a,b] [--tint] [--grid N] [--preview <dir>]`.
