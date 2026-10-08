# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted; presentation
never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material only - never
modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block "Session 10" (exact state, runs, "Next").
3. `docs/tc2/DECISIONS.md` newest entries **D2-113, D2-114** (D2-114 building variety closed in session 10).
4. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 10 entry); for C9.4 `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §5-6 and
   `docs/tc2/25_CIVILISATION_ROADMAP.md` §5; building variety is a standing requirement: `docs/tc2/13_RENDERING_AND_ART.md` §13, `21` §6.
5. `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` is not ours - leave it), `git log --oneline -10`.

## Standing direction (Arjun, 8 Oct 2026): building variety
Settlements must not look like repeated same-colour, same-shape models. Houses vary by real circumstances (household size, means,
material at hand, age, the village's history, its building tradition); functional buildings are recognisable at a glance; richer,
larger, older places get denser and more substantial. Every new structure ships with its own silhouette and is judged in real shots at
close, village and region views - never from code. (D2-114 built the first answer; streets and real household wealth are open.)

## Do next
1. **C9.4 docks, cargo boats and coastal trade** (SESSION_HANDOFF "Session 10" -> "Next"): goods worth carrying by sea and not by land
   (timber and stone valued at the hours a load costs a village to fetch itself), cargo boats built at the boat shed when sea trade outgrows
   the plank boats, a jetty/quay chosen by the traffic landed; at par at both resolutions; ten seeds against `_a5` with a placebo; shots; Web.
2. Then C10.2 leaders (halls, towers, gates, keeps as real structures) or C13 morphology (streets).

## Tools
- Copy `tc2/out/tools/wrun.sh` and `cmp.py` to the new session's scratchpad and set `SCR=` in `wrun.sh`; copy the binaries
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (two chains in parallel, ~10-15 min).
- Headless: `place --load S --list` (each village: plans, covers, walls, means, works with their hours), `--households [--sid N]`, `--standing`;
  `person --load S`; `run --load S --lived`, `--voyagers`, `--vtrace FILE`, `--nosea`, `--nudge Y` (a placebo); report lines `forms:`,
  `sea trade:`, `trade:`, `boats:`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "close@10,low@10,street@10,village@10,work12@10"`
  (`work<kind>`: the village's structure of that kind - 11 granary, 12 smithy, 13 quarry, 14 training ground, 15 market, 16 barn, 22 boat shed;
  `voyage*` views wait for a real voyage). About a minute each.

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the untracked
  `unity/TinyConsequences2/Assets/Resources.meta`; never force-push. New `.cs` and model files need their Unity `.meta` committed.
- **No subagents. Keep the machine around 50 % CPU/RAM; one heavy job at a time** (Unity build or shots, the full test suite; two headless
  300-year runs in parallel are fine). Time single runs sequentially on a quiet machine - parallel timings are noise (a +30 % reading was).
- Bash heredocs break on backticks and apostrophes in this harness: write patch scripts, docs and commit messages with the Write tool into the
  scratchpad, then `python file.py` / `git commit -F file`.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - it must be a build of the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions and checked at
  par over pooled worlds; judge by century means over ten seeds, and size the noise with a placebo (`--nudge 150` on the same binary) before
  calling an effect real - conflict counts (raids, wounds, palisades) move x0.66-x1.5 under a one-FU nudge.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (full suite ~12 min, 275 tests at D2-114's close).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=50,100,200,300 bash wrun.sh 42 4 11 3 8` and `... 2 7 1 5 6`; `python cmp.py TAG_A TAG_B seeds...`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~8-16 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out <dir> [--only a,b] [--tint] [--grid N] [--preview <dir>]`.
