# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 15"** (exact state, results, machine notes, Next).
3. `docs/tc2/DECISIONS.md` newest entries **D2-122** (carts), **D2-123** (nobody works in the water), **D2-124** (crossings surveyed), **D2-125**
   (the village smith).
4. `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2b (C1.4b as built) and §3 (the next slices, the C3.3 survey); `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §2
   (carts as built), §4 (the crossings survey); `docs/tc2/25_CIVILISATION_ROADMAP.md` §4 (the resource town proof).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (session 15 entry); `git status` (the untracked `unity/TinyConsequences2/Assets/Resources.meta` and the
   modified `.claude/launch.json` are not ours - leave them), `git log --oneline -10`.

## Where things stand (9 Oct 2026, end of session 15)
- **C4.2 carts closed (D2-122)**. Its closing checks found three faults, each measured and fixed: carts begun while hunger sent the village to the
  wild were never worked (15.5 % of carts given up -> 4.9 %: `Carts.ShortBound`, a cart given up returns its timber; `run --carttally`); woodcutters
  with their handcarts stood out in the sea (D2-123); the yard camera framed a roof. Carts against no carts over ten seeds: trades x0.67, famines
  x0.83-0.89, people and vital rates at par.
- **D2-123 nobody works in the water**: `wet --load S [--days N]` lives a save hour by hour and counts who stands under the drawn water. A year-300
  world spent 0.64 % of person-hours in the sea or a lake (field spots the water had covered, coastal woodlots, hunters); now lived field, woodlot
  and wild spots go ashore and woodlots are chosen above the shore: 0.05-0.08 % left (straight walks across lakes; a day's lake over works).
- **D2-124 crossings surveyed**: every stream a worn way crosses is a fordable brook (largest flow 0.08-0.23 m3/s on five worlds) - C4.3 bridges
  deferred.
- **C1.4b the village smith (D2-125, sim version 61)**: measured - 5,730 people read as "a smith"/"a boatwright" where 175 had worked at a smithy and
  10 built a plank boat; smithies had no smiths (up to 355 hands and 24,000 hours in a smithy-year). Now a smithy's four places go to its best
  smiths by craft and practice (`Forge.Places`, `SmithBar`), practice quickens the hand (`Person.SkSmith`, `Forge.Hand`), another smithy rises only
  where the steady need outruns the places (`Backlog`/`Tally`/`NeedsMore`/`Year`, up to 4, smiths at the nearest), smiths/ironsmiths and boatwrights
  (plank boats led) are read from their work. Ten seeds `_s3` vs `_c3` (= `--nosmithcrew`): people, famines, trade, conflict at par; smithies
  68 -> 80; at most 23 hands a smithy-year; iron traded 22 -> 9 t (a seller's smiths cannot see demand beyond their places: open).
- **Verified**: full suite 314/314; ten-seed runs `tc2/out/civ/s*_c2` (carts, hunger rule), `_c3` (woodlot rule), `_s1`/`_s2` (rejected smith
  variants), `_s3` (the smith slice, saves 100/200/300); shots `tc2/out/shots/c42a`-`c42c`, `c14b`. Web test 6/6 (`tc2/out/webtest/s15web`, views 77-121 fps, pages 69-88); `tc2/web-build/web` is current (96e38ab).
- Open W task **W-FOREST** (Arjun, 8 Oct): forests/biomes still read as the same trees recoloured - not solved (`18` §8).

## Do next
1. **C5.3c the bloomery at the ore** (D2-125 "Open"; roadmap `25` §4 the resource town): iron smelted where the bog ore lies by a crew of its own
   (stints with a camp, households moving to the work - an iron hamlet), and an **unmet ask** counted (a trader who wanted iron and found none) so a
   seller's smiths and bloomeries grow with their buyers. Measure first: `run --crafttally` (smithy-years, hands), iron by world
   (`tc2/out/tools/smith.py A B`), `place --list` (SMITHY / N SMITHIES), `iron:` and `force:` report lines.
2. **C3.3 exchange between households** (`26` §3: a phase - household property, sharing by need, debts) or **C3.4 the mill** (measure the quern's
   hours a person-day first).
3. D2-123 "Open": a lake risen over works or homes is not a flood people answer; local walks cross lakes in straight lines.
4. W-FOREST (art pass).

## Tools
- Copy `tc2/out/tools/wrun.sh`, `cmp.py`, `agg.py`, `smith.py`, `wares.py`, `pots.py` to the session's scratchpad and set `SCR=` in `wrun.sh`; copy
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (sequential, ~13-20 min for ten).
- Reference runs now: `tc2/out/civ/s*_s3` (the smith slice; saves 100/200/300); controls on the same binary: `--nosmithcrew` (= `_c3`), `--nocarts`.
- Headless: `wet --load S [--days N]` (who stands in the water), `place --load S --crossings` (streams the worn ways cross), `place --load S --haul`,
  `run --carttally`, `run --crafttally` (hours by person and village-year: smithy, plank boats, sheds, kilns), `run --loads`, `run --ledger Y`,
  `place --load S --list` (wares, sheds, KILN, SMITHY/N SMITHIES, jars) `| --stores | --layout --sid N | --overlaps | --households | --standing`;
  `person --pick smith|weaver|fighter`; report lines `carts:`, `cart tally:`, `craft ...`, `iron:`, `force:`, `pots:`, `wares:`, `trade:`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "work12near@11,work12@11,cart@11,cartyard@11,cartfar@14"`
  (`work<kind>` a structure of the framed village - 12 smithy, 23 weaver's shed, 24 kiln; `cart*` carts; `row*`, `hall`, `mouth`; `--ground`).

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the two files above;
  never force-push. New `.cs` and model files need their Unity `.meta` committed (a Unity run - shots or `configure` - makes them).
- **No subagents. Control Room's Resource Governor LOW: CPU 50 %, RAM 75 % for all work; one heavy job at a time** (Unity build or shots, the full
  suite ~12-18 min, a ten-seed run ~13-20 min). The machine's own baseline sits near 73-75 % RAM; a Unity run takes it to ~85 % while it runs - start
  heavy jobs when RAM reads <= 75 % and run nothing else beside them. `dotnet build-server shutdown` frees this session's compiler servers. Never kill
  processes you did not start (a `chrome-headless-shell` not ours took ~70 % CPU for an hour in session 15).
- Bash heredocs mangle backslashes in this harness (`\n` inside a Python string became a newline; Windows paths with `\U` broke a script): write patch
  scripts with the Write tool into the scratchpad (forward slashes in paths), then `python file.py`; long commit messages via a file.
- The Web test compares the browser with `tc2/headless/app/bin/Release/net8.0/tc2.dll` - build it from the same Core as the Web build.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions; judge by century
  means over ten seeds against a control on the same binary (a `--no...` switch) - aggregate counts move x0.8-x1.3 between reruns; a placebo
  (`--nudge Y`) shows the spread when an effect looks small.
- Lived work spots must be on dry ground (`AgentSystem.Ashore`); check new spots with `wet`.
- A new structure kind needs a slot in `WorldObjectsView._works` (sized by kind) and a line in `HistoryText.WorkWhy`. In Python patch strings write a C#
  newline escape as a doubled backslash and n.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (the filter is a substring, not a regex).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=100,200,300 bash wrun.sh 4 42 2 1 5 3 11 8 7 6`; `python cmp.py A B 4 42 2 1 5 3 11 8 7 6`; `python agg.py A B`;
  `python smith.py A B`.
- Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~11 min), `node tc2/scripts/webtest.mjs --tag T`.
- Kit: `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--only a,b] [--preview <dir>]`.
