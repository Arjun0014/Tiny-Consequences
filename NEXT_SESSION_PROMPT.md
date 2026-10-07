# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted; presentation
never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material only -
never modify or stage it.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun).
2. `docs/tc2/SESSION_HANDOFF.md` - top block "Exact state at handoff" (session 6), then its "Next".
3. `docs/tc2/PROGRESS.md` CURRENT STATE; the newest `docs/tc2/DECISIONS.md` entries (D2-099 .. D2-107).
4. `docs/tc2/25_CIVILISATION_ROADMAP.md` (C1-C13) and the domain doc of what you touch (`20`-`24`, `07` §9 person page).
5. `git status`, `git log --oneline -15`.

## Where things stand (7 Oct 2026, ~19:30 IST)
- Last commit **00ae0d6** `[tc2] C10.1 the lord's call (D2-107)`. Tree clean (ignore the untracked `Assets/Resources.meta`).
- **Sim version 47; 243 headless tests pass.** Unity compiles. The Web build in `tc2/web-build/web` is current to C12.2.
- Session 6 built: roads by use, rings that grow, the person page and the settlement page (HUD + headless), copper ore bodies and
  mines, charcoal hearths, bog iron and the bloomery, iron trade, scouts drawn to ore, claims on worked ore (digging another village's
  ore is a taking that feeds the feud machinery), and the lord's call (tributaries send people with their lord's bands; a raid on a
  tributary wrongs its lord).

## Do next, in order
1. **Follow C10.1's population drops** (D2-107 "Open"): 300-year DEEP runs `tc2/out/civ/s{7,2}_c101a` lost 24 % and 26 % of people
   against `_c54a`; seed 2 did so with *fewer* hunger deaths and blows. Find where the people went (`run --load <save> --years 0
   --events SettlementAbandoned|HouseholdMoved|Raid|CallAnswered`, `--civ 10`, `place --pick big`). Check whether one seen raid now
   drives revenge twice (the tributary's grievance and the lord's share). Fix with evidence over several seeds (42, 7, 4, 2 at least),
   then record it in D2-107 or a new decision.
2. **Web frame rate**: true cost is 35-56 fps near the ground (WebGPU), target 60. Readings since C1.4 were inflated by a now-fixed
   smithy mesh error that stopped the view's setup. Measure per layer on a quiet machine:
   `node tc2/scripts/webperf.mjs --view close --tag perf_close` and `--view forest`; rebuild first if Core/Game changed
   (`node tc2/scripts/unity.mjs build-web --jobs 4`, ~10 min; `unity.mjs configure` already done).
3. Then the roadmap: C5.3c (work at a distance) is of little economic weight in this model (the carry is ~5 % of a kilo of iron's
   labour) - prefer C8 war as logistics, C9 maritime (the coastal proof) or C10.2 (leaders, succession, rebellion) and the polity page.

## Rules that bite
- Commits `[tc2] ...`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never stage TC1 files; never force-push.
- **No subagents. Keep machine CPU/RAM around 50 %; one heavy job at a time** (Unity build, test suite, long runs never together).
- Bash heredocs collapse `\\` and mangle `\n` inside Python strings: write patch scripts, docs and commit messages with the Write tool
  into the scratchpad, then `python file.py` / `git commit -F file`.
- Core is pure C#, deterministic, **no float arithmetic** (`node tc2/scripts/headless.mjs floatcheck`); every mechanism at both
  resolutions (lived and COARSE/DEEP) and checked at par over a *year*, not days (a COARSE day gives fields every free hour in season).
- After any major slice: update DECISIONS, PROGRESS, SESSION_HANDOFF, `docs/tc2/NEXT_SESSION_PROMPT.md`, commit.

## Commands
- Tests: `node tc2/scripts/headless.mjs test [--filter Name] [--nobuild]` (full suite ~6-8 min).
- 300-year runs: scratchpad `wrun.sh` (`BIN=bin_x TAG=_x YEARS=300 SNAP=100,200,300 bash wrun.sh 42 7`; copy
  `tc2/headless/app/bin/Release/net8.0` to `bin_x` first) - the scratchpad path is in SESSION_HANDOFF; recreate the script from its
  description there if the scratchpad is gone.
- Pages: `node tc2/scripts/headless.mjs person|place --nobuild --load <abs save> --pick ...`.
- Shots: `node tc2/scripts/unity.mjs shots --tag T --load "C:/Game Dev/tiny-consequences/tc2/out/civ/<run>/world_y300.tc2" --village ID
  --views "work17@11,village@11"` (absolute paths). Browser: `node tc2/scripts/webtest.mjs --tag T --skip-away`.
