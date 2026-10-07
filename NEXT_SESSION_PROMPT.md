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

## Where things stand (8 Oct 2026, session 8 of TC2 work; Control Room run #7 session 3)
- Commits this session: 9a2ceb7 + 23ac1c9 **C9.1 closed** (D2-110), c5fd15f **C9.2a the landing and the crew** (D2-111), then **C9.2b boats of
  planks** (D2-112). Sim version 51; 262 headless tests pass on the final code.
- The Web build in `tc2/web-build/web` is of 1808e81 (C9.2b): Web test 5/5, every view 73-120 fps.

## Do next, in order
1. The roadmap (`docs/tc2/SESSION_HANDOFF.md` session 8 "Next"): C9.3 the dock (by catches landed and boats kept), crews beyond the
   household, farther grounds for plank boats, then C9.4 cargo boats and coastal trade; or C10.2 leaders, succession and the polity page.
   Tools: `tc2/out/tools/wrun.sh` (fix its `SCR=` to the new scratchpad) and `cmp.py` (`python cmp.py c92c c92r 42 7 4 2 1 3 5 6 8 11`).

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
