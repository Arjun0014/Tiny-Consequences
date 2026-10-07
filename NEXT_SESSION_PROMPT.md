# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted; presentation
never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material only - never
modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block "Session 7", its "Next".
3. `docs/tc2/PROGRESS.md` CURRENT STATE; the newest `docs/tc2/DECISIONS.md` entries (D2-107 .. D2-110; D2-110 has an "Open").
4. `docs/tc2/24_TRANSPORT_AND_MARITIME.md` §5 (boats) and `docs/tc2/25_CIVILISATION_ROADMAP.md` (C1-C13, the C9 slices).
5. `git status`, `git log --oneline -10`.

## Where things stand (7 Oct 2026, ~23:00 IST)
- Commits this session: **957403c** D2-108 (the lord answers its tributaries' wrongs once; C10.1's "population drops" were chance - no
  systematic loss over ten seeds), **57499f3** D2-109 (Web frame rate: per-instance colours, draws with their own bounds; near views
  90-102 fps, Web test 6/6), **c674892** C9.1 boats from the woods (D2-110, code complete, **final checks open**), then the handoff docs.
- **Sim version 49. 252 headless tests pass on the final code** (full suite rerun at handoff, after the boats' landing fix).
- The Web build in `tc2/web-build/web` is current to D2-109 (before boats).

## Do next, in order
1. **Close C9.1 (D2-110 "Open")**: rerun ten seeds 300 years DEEP with a fresh copy of the binaries (tools copied to
   `tc2/out/tools/wrun.sh` and `cmp.py` - fix their scratchpad paths to the new session's scratchpad), compare with
   `python cmp.py c108a c91j 42 7 4 2 1 3 5 6 8 11`; check `node tc2/scripts/headless.mjs run --nobuild --load <abs save> --years 0
   --boats <sid>` shows no "WET" boat; Web build (`node tc2/scripts/unity.mjs build-web --jobs 3`), `node tc2/scripts/webtest.mjs --tag
   c91web --skip-away`, shots `node tc2/scripts/unity.mjs shots --tag boats2 --load "<abs save>" --village <id> --views
   "work20@10,work20far@10,close@10,village@10"` (a village with boats: the report's `boats:` line names the one with most); judge the
   boats on the beach and sailors at sea; update D2-110, 24, PROGRESS, SESSION_HANDOFF; commit.
2. Then the roadmap: C9.2 plank boats and boatbuilding (a technique found by dugout owners), or C10.2 leaders, succession and the polity
   page.

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files or the untracked
  `unity/TinyConsequences2/Assets/Resources.meta`; never force-push.
- **No subagents. Keep the machine around 50 % CPU/RAM; one heavy job at a time** (Unity build or shots, the full test suite; two
  headless 300-year runs in parallel are fine).
- Bash heredocs collapse `\\`: write patch scripts, docs and commit messages with the Write tool into the scratchpad, then
  `python file.py` / `git commit -F file`.
- Core is pure C#, deterministic, **no float arithmetic** (`headless.mjs test` lints it); every mechanism at both resolutions (lived and
  COARSE/DEEP) and checked at par over a year; a DEEP step must do what its days would (one boat a day -> one per day of the step).
- Judge mechanisms by century means over ten seeds (`cmp.py`), never by one world's last year.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (full suite ~9 min).
- Runs: `BIN=bin_x TAG=_x YEARS=300 SNAP=100,200,300 bash wrun.sh 42 7 ...` (copy `tc2/headless/app/bin/Release/net8.0` to the scratchpad
  as `bin_x` first; `MODE="--coarse"` for COARSE); `--profile` for parts; `run --load S --years 0 --events KIND|--civ-of ID|--boats SID`.
- Web: `node tc2/scripts/webperf.mjs --view close|forest [--sets "cmd;cmd|cmd" --reset "..."]` prints fps, CPU ms and draws.
