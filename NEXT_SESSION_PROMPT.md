# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 14"** (the people x0.955 investigation and the placebo to finish), then **"Session 13"**
   (the full C4.2 build, runs, known issues).
3. `docs/tc2/DECISIONS.md` newest entry **D2-121** (C3.2 the potter's kiln, jars and hearths' pots; clay by the ground's shape).
4. `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` - C3's design (§1 C3.1, §2 C3.2 as built, §3 the next slices and the C3.3 survey); `docs/tc2/24_TRANSPORT_AND_MARITIME.md`
   §2 "Implemented (C4.2)" - the carts in progress (no D2-122 yet: write it when C4.2 closes).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (session 14 and 13 entries); `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` and the
   modified `.claude/launch.json` are not ours - leave them), `git log --oneline -10`.

## Session 14 (8 Oct 2026, 19:02-19:26 IST - stopped early by Arjun; no code changed)
- **C4.2's people x0.955 investigated** (SESSION_HANDOFF "Session 14"): per-person rates are identical with and without carts (births 43.9 vs 43.8 a
  1,000 person-years, net growth 7.20 vs 7.21, hunger deaths lower with carts); the loss is in the level. Seed 4's loss is one bigger epidemic at
  year 180 (1,289 sickness deaths against 734). Seed 42's is an early chance split (years 20-40) with no hunger, no sickness and only 4 cart trading
  trips in 100 years. Diagnostics: `tc2/out/c42diag`.
- **The ten-seed placebo `_c1p`** (`--nocarts --nudge 20`) finished at 19:24: people 200-299 x0.982 against `_c1n`, with a seed-to-seed spread as
  wide as the carts' (sd 0.069 vs 0.061; seed 1 x0.857, seed 5 x0.889); **carts against the placebo x0.972 (t -1.06): x0.955 is noise, no fault**.
  The carts' real effects are those the placebo does not reproduce: famines x0.83 (t -3.4), hunger deaths x0.93 (t -2.3), trades x0.70 (t -4.9).

## Where things stand (8 Oct 2026, session 13)
- **C3.2 is built** (D2-121, sim version 59; commit d21d09d): measured first - D2-063 gave every village that knew pottery a sealed store and every
  household with a potter its cooking pots, also the 25-60 % of villages with no clay within reach. Now: clay by the ground's shape (a reach draining
  200 ha or a lake's bed within 1.2 km; `Learning.Clay/ClayCell`); jars (`Settlement.Jars`, kg) in the granaries keep the grain they seal (0.65 rot;
  `Pottery.RotFactor`), only from a kiln; a household that cannot fire its own cooks in kiln pots (`Household.Pots`) or on the full wood
  (`Pottery.CookFactor`); a kiln (`Structures.Kiln` 24) at the clay where the store's grain lacks two months of a potter's jars; its four best
  potters (`PotBar`, `Person.SkPot`; lived `ActivityKind.Pot` 36, COARSE less the walk); ware from the clay and the village's wood; `Trade.Pots` 7
  valued by the grain it keeps; the potter's role from practice. Kit `kiln`, `clay_pit`, `storage_jars`, `ware_stack`; camera `work24`, `work24near`.
- **Ten seeds** `tc2/out/civ/s*_k1` (C3.2, the reference now; saves 50/100/200/300) against `s*_k1n` (same binary, `--nokiln`): 84 kilns in 169
  villages, 1,988 t of ware traded (mostly for grain), jars seal 80-100 %, trades x5.4, **markets x3.6 (23 -> 99)**, rot, people, births and deaths
  at par, famines x1.06 (t 0.7). A town buys its jars from the potters' villages round it.
- **Tests**: 302 headless, Web test 6/6 (`PotteryTests` 8; `TheCoarseDayRaisesBandsAtTheLivedRate` now six worlds - bands are rare). Shots `tc2/out/shots/c32b2`
  (Krumere), `c32c21` (Krailddal: ware in straw for sale).
- **C3.3 surveyed and deferred** (3db3cec, `26` §3): a tools household ledger (`run --ledger Y`) - the village's store moves 31 % of the food
  households draw from 43 % givers to 57 % takers; household property needs a moral economy (sharing by need, debts), household fields and
  inheritance: a phase, weighed with C10 (lords' rents).
- **C4.2 carts - IN PROGRESS** (d6c29cf, sim version 60; `24` §2 "Implemented (C4.2)"): `Core/Places/Carts.cs` - a household's handcart (120 kg,
  0.85 pace) or ox cart (500 kg, 0.7) made in its own hours where its backs were too small three times within two years (a trader's back's load on
  a path/road where more was spared and wanted; wood or stone from 400 m+), with an able wright; cart loads in `Trade.Load/Exchange`, pace in
  `BeginTravel`, `Woods.Rate`/`Stone.Rate` by the cartload; pages, view (parked and travelling carts, the ox, a load), camera `cart`/`cartyard`,
  kit `handcart`/`ox_cart`/`cart_load`; `CartsTests` 5/5. Ten seeds `tc2/out/civ/s*_c1` vs `s*_c1n` (`--nocarts`, which reproduces `_k1` exactly):
  famines x0.83 (t -3.4), hunger deaths x0.93, trades x0.70, **people over years 200-299 x0.955 (t -2.4) - noise, settled in session 14 by a placebo**; the leads then were: foundings
  x0.93 (t -1.6), households setting out x1.46, fewer villages at year 300 in 5 of 10 seeds.
- **Verified at the handoff**: carts 5/5, pottery 8/8, save/load 8/8, Unity 0 errors. **Not yet**: the full suite since carts, cart shots, the Web
  build/test (`tc2/web-build/web` is still C3.2's d21d09d).
- Open W task **W-FOREST** (Arjun, 8 Oct): forests/biomes still read as the same trees recoloured - not solved (`18` §8).

## Do next
0. **Close C4.2 carts** (SESSION_HANDOFF "Session 14" then "Session 13"). The people x0.955 is answered: noise (the placebo, above).
   Write **D2-122** (the measured state, the design, the evidence, this investigation), shots
   (`node tc2/scripts/unity.mjs shots --tag c42a --load "<abs>/tc2/out/civ/s4_c1/world_y300.tc2" --village 2 --views "cart@11,cartyard@11,cartfar@14"`),
   the full suite (`node tc2/scripts/headless.mjs test`, ~12-22 min), Web build and Web test, commit, docs (`24`, `25`, PROGRESS, handoff).
1. (done in session 13) Web build of d21d09d: Web test 6/6 (`tc2/out/webtest/c32web`, views 86-120 fps, pages 68-84); `tc2/web-build/web` is current.
2. **C3.3 exchange between households** (`26` §3): the weaver's and the potter's ware is the village's and free to its households - payment in kind
   (makers paid), households' own holdings replacing D2-114's `Means`; export crafts (looms and kilns for buyers) wait for it. Measure first.
3. Roles read from practice for smiths and boatwrights (weavers and potters are).
4. C9.4 cargo by sea re-weighed with the pottery traffic; C3.4 the mill. W-FOREST stays open for a dedicated art pass.

## Tools
- Copy `tc2/out/tools/wrun.sh`, `cmp.py`, `wares.py`, `pots.py`, `agg.py` to the session's scratchpad and set `SCR=` in `wrun.sh`; copy
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (sequential, ~15 min for ten).
- Reference runs: `tc2/out/civ/s*_k1` (C3.2, saves 50/100/200/300, `--rot --potsflow`), `s*_k1n` (same binary, `--nokiln`), `s*_w7` (C3.1b).
- Headless: `place --load S --haul` (oxen, wrights, woodlot/quarry distances, ways by state), `run --loads` (trading loads bound by a back's load),
  `--nocarts`, `carts:` line, `run --ledger Y` (households' harvest share against their draws, C3.3); `place --load S --stores` (stores, jars, clay, hearths, wood), `place --load S --list` (wares, WEAVER'S SHED, KILN, jars) `| --layout --sid N | --overlaps | --households | --standing`; `person --pick weaver`;
  run report lines `pots:`, `wares:`, `water caches:`, `town:`, `trade:`; `run --potsflow`/`--waresflow` (what ware/wares went for), `--nokiln`,
  `--noweave`, `--rot` (grain rotted vs harvested); `tc2/out/tools/agg.py A B [A2 B2]` (report quantities' ratios over the ten seeds), `wares.py TAG seeds`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "work23near@11,work23@11,village@11"` (`work23`: the
  weaver's shed, a standing one first; `row*`, `hall`, `mouth`, `work<kind>`; `--ground`).

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the two files above;
  never force-push. New `.cs` and model files need their Unity `.meta` committed (run `node tc2/scripts/unity.mjs configure` to make them).
- **No subagents. Keep the machine around 50 % CPU/RAM (11 GB, often <2 GB free); one heavy job at a time** (Unity build or shots, the full
  test suite 12-22 min, ten-seed runs ~15 min an arm). Never kill processes you did not start.
- Bash heredocs mangle backslashes in this harness (`\n` inside a Python string became a newline): write patch scripts with the Write tool into the
  scratchpad, then `python file.py`; long commit messages via a file.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - build it from the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions; judge by
  century means over ten seeds against a control on the same binary (a `--no...` switch) - aggregate counts move x0.8-x1.3 between reruns.
- The kit's models come into Unity with x mirrored (front +z): `Footprints.WingSide` = -1; check new asymmetric models with a `--ground` shot.
- A new structure kind needs a slot in `WorldObjectsView._works` (sized by kind; the kiln, 24, overflowed it and the view's Start threw) and a line in
  `HistoryText.WorkWhy`. In Python patch strings write a C# newline escape as a doubled backslash and n: a single one became a real newline
  inside a C# char literal (Hud.cs).

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (the filter is a substring, not a regex).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=50,100,200,300 bash wrun.sh 4 42 2 1 5 3 11 8 7 6`; `python cmp.py w4 <tag> 4 42 2 1 5 3 11 8 7 6`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~11 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--only a,b] [--preview <dir>]`.
