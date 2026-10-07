# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted; presentation
never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material only - never
modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block "Session 8" (exact state, tools, runs, "Next").
3. `docs/tc2/DECISIONS.md` newest entries **D2-110 .. D2-113**; D2-113 (C9.3 voyages) is IN PROGRESS and its "Open" is your first step.
4. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 8 entry); `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §5; `docs/tc2/25_CIVILISATION_ROADMAP.md` (C9).
5. `git status` (only the untracked `unity/TinyConsequences2/Assets/Resources.meta` - not ours, leave it), `git log --oneline -10`.

## Where things stand (8 Oct 2026, ~02:20 IST; end of TC2 session 8)
- Session 8 commits: 9a2ceb7 + 23ac1c9 **C9.1 closed** (D2-110), c5fd15f **C9.2a the landing and the crew** (D2-111), 1808e81 + 9ae5a03
  **C9.2b boats of planks** (D2-112), 7ab7195 + c210b7d **C9.3 voyages - work in progress** (D2-113), then this handoff's docs commit.
- **Sim version 52; 265 headless tests pass** (full suite on 7ab7195); the Unity editor compiles everything (batch `configure`, no errors).
- Web build in `tc2/web-build/web` is of **1808e81** (C9.2b): Web test 5/5, every view 73-120 fps (`tc2/out/webtest/c92web`). C9.3 is not in it.
- What C9.3 has so far: `Places/SeaRoutes.cs` (A* over sea cells between two villages' landings, cached by pair + coast), `Trade.Plan/Load/
  Exchange(.., loadKg)` (a plank boat carries `BoatLoadKg` 500), `Trade.BestSeaPartner` and `Trade.Voyages` (abstract voyages by plank
  boats at both resolutions, called from `Trade.Day` beside relief), **relief by sea** in `Trade.Relief` (a famine beyond a day's walk and
  within a sea way gets a boatload a day per plank boat of the giver, calm sea only; `ReliefSent` ArgA 1), metrics `Voyages`, `SeaCargoKg`,
  `ReliefBySeaFu`, headless `sea trade:` line, `Tests/Core/VoyageTests.cs` (3 tests).

## Do next, in order
1. **D2-113 "Open" - relief by sea raises famines.** Ten seeds `tc2/out/civ/s*_c93c` against C9.2b `_c92r`: people over years 200-299
   x0.987 (at par) but famines x1.18 (t 2.1), hunger x1.12; seed 2 famines 123 -> 227 and hunger deaths 1,704 -> 4,791; seed 11 238 -> 403;
   seed 8 206 -> 272; seed 4 fewer famines yet people -22 %. Trade voyages themselves are rare (barter finds few deals); relief is the big
   flow (seed 4: 254k FU by sea, 59 % of its relief). Hypotheses: (a) givers drained to their 60-day floor (sea relief sends up to a
   boatload a day per boat to every famine in reach; land relief only 3 days of the receiver's need a day) then fail a harvest; (b) receivers
   kept from migrating (migration's push reads their stores) and starving again; (c) chance. Investigate with
   `node tc2/scripts/headless.mjs run --nobuild --load "<abs>/tc2/out/civ/s2_c93c/world_y300.tc2" --years 0 --events ReliefSent` (and
   `Famine`), compare with `s2_c92r`; find the first divergence. Likely fix: cap what a giver sends by sea like land relief (a share of
   what it spares, the receiver's few days' need a day). Rerun ten seeds and compare: `python cmp.py c92r <tag> 42 7 4 2 1 3 5 6 8 11`.
2. Finish C9.3: lived voyages (crews rowing along `SeaRoutes`, drawn in the view), a lived/COARSE parity test, shots of a boat at sea with
   cargo, the Web build and test; close D2-113; update `24` §5-6, `25`, PROGRESS, SESSION_HANDOFF, this file; commit.
3. Then the roadmap: C9.4 the dock (a jetty where boats are many and loads land), crews beyond the household, farther grounds for plank
   boats, cargo boats; or C10.2 leaders, succession and the polity page.

## Tools (session 8's scratchpad is gone with the context)
- Copy `tc2/out/tools/wrun.sh` and `cmp.py` to the new session's scratchpad and set `SCR=` in `wrun.sh` to it; copy the binaries
  `tc2/headless/app/bin/Release/net8.0` there as `bin_<tag>` before each ten-seed run (two chains of five seeds in parallel, ~7 min).
- Headless report lines to read: `boats:` (dugouts, plank boats, sheds, given up, lost, in the water / away from it, landings, outings,
  crew seats), `sea trade:`, `squares under the sea:`; `run --load S --years 0 --boats SID` lists a village's landing and boats.

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the untracked
  `unity/TinyConsequences2/Assets/Resources.meta`; never force-push. New `.cs` files need their Unity `.meta` committed (run
  `node tc2/scripts/unity.mjs configure` once to create them).
- **No subagents. Keep the machine around 50 % CPU/RAM; one heavy job at a time** (Unity build or shots, the full test suite; two headless
  300-year runs in parallel are fine). Web tests read low page frame rates if anything heavy runs beside them.
- Bash heredocs break on apostrophes and collapse `\\`: write patch scripts, docs and commit messages with the Write tool into the
  scratchpad, then `python file.py` / `git commit -F file`.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions (lived and
  COARSE/DEEP) and checked at par over pooled worlds; a DEEP step must do what its days would.
- Judge mechanisms by century means over ten seeds (`cmp.py`), never by one world's last year; look at real shots (`unity.mjs shots`),
  zooming crops - session 8's shots found boats in the square, fishers and whole villages standing in the sea, a shed facing inland.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (full suite ~10-11 min).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=100,200,300 bash wrun.sh 42 7 4 2 1` (and `3 5 6 8 11`); `MODE="--coarse"` for COARSE;
  `run --load S --years 0 --events KIND|--civ-of ID|--boats SID`; `place --load S --sid N`; `person --load S --pick ...`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "<abs save>" --village ID --views "work21@10,work22@10,close@12"` (work<kind>:
  20 dugout, 21 plank boat, 22 boat shed). Web: `node tc2/scripts/unity.mjs build-web --jobs 3` (~8-12 min), `node tc2/scripts/webtest.mjs
  --tag T --skip-away`.
