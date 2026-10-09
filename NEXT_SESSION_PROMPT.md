# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism (Control Room writes this file), not a stop condition. Stop only
for a real external blocker (credentials, payment, UAC, an irreversible product decision).

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the full standing prompt (architecture, rules, every direction from Arjun; "Where we are (session 16)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 16, second context"** (exact state, Next in order), then "Session 16".
3. `docs/tc2/DECISIONS.md` newest entries **D2-130** (W-FOREST pass 1, presentation), **D2-129** (C3.5 household formation; it corrects D2-126),
   **D2-128** (C10.3 a contested succession splits a village), D2-127 (C10.2 the first household), D2-126 (C3.3 household grain).
4. Domain docs: `docs/tc2/18_WORLD_READABILITY.md` §8 (W-FOREST, pass 1 recorded), `07_AGENTS_AND_LIFE.md` §7e (C3.5), `26_CRAFTS_AND_EXCHANGE.md`
   §2c and §3 (C3.3, its correction, and the land measurement C3.6), `10_CONFLICT_AND_POLITICS.md` §12c-§12d (C10.2, C10.3).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (five session 16 entries); `git status` (the modified `.claude/launch.json` and untracked
   `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them), `git log --oneline -8` (HEAD 284471c or a handoff commit on it).

## Where things stand (9 Oct 2026, end of session 16's second context)
- **Committed and verified**: C10.3 (D2-128, d891d16; full suite 333/333), C3.5 household formation (D2-129, b8f4830, sim version 65: a couple marries
  when what it brings of its households' grain keeps it to the next harvest; hunger deaths x0.69, people at par; full suite 336/336), W-FOREST pass 1
  and 1b (D2-130, ee5044c + 284471c, presentation only: kinds of wood from the simulation's climate and water - rain forest, dry tropics, warm
  temperate, broadleaf, mixed, boreal, treeline, carr, gallery - ten new tree forms, stand ages, palettes, far forms, the far canopy by kind). Last
  checks at handoff: `HouseholdFormationTests` 3, `LeadersTests` 7, `GazetteerTests` 2, `BiographyTests` 4 pass; Web test **s16web5 6/6** (WebGPU,
  bit-identical, forest views 69-82 fps); `tc2/web-build/web` is of 284471c.
- **Measured, recorded, not built**: there is no famine amid plenty (D2-126 corrected); land binds for 91 % of people by year 300 while grown
  people keep ~4.5-5 free hours a day (`26` §3 C3.6: working the land harder vs household land - not yet chosen).
- **Waiting on Arjun**: W-FOREST stays OPEN until he judges the shots (`tc2/out/shots/wf2/sheet4.png` four climates, `wf4/forest.png`,
  `wf6/before_after_far.png` - sent to him). Do not mark it done without his judgement.

## Do next
1. If Arjun has answered about W-FOREST, act on it first (pass 2 candidates in D2-130 "Open": ground cover by kind of wood, stand age as real
   state, a straight snow/no-snow seam on one boreal patch, the dry tropics' ground drawn lush, perhaps too many bare boles in the rain forest).
2. Otherwise the next simulation slice from the evidence: **C3.6 land** (`26` §3) - measure first which binds harder: labour a hectare (Boserup:
   more work a hectare for falling returns - tillage, weeding, manuring with the herds' dung) or who holds the land (household holdings, landless
   households, labour for others; marriage on a holding). Then: the hungry gap before the harvest (hunger is still 17 % of deaths), factions a full
   land cannot shed (D2-128), debts' consequences, C9.4 cargo, D2-125's iron ask, D2-123 floods.
3. Web builds: `node tc2/scripts/unity.mjs build-web --jobs 3` (~6 min; RAM peaks ~95 % - run nothing beside it), then
   `node tc2/scripts/webtest.mjs --tag NAME`. Shots: `node tc2/scripts/unity.mjs shots --tag T --seed N --hour H --size 1280x720 --views a,b`
   (views include `forest:KIND[:low|:far]`, e.g. `forest:rain,forest:boreal:far`; seed 5 has every kind but the treeline).

## Tools (in `tc2/out/tools/`, local only; copy to the session scratchpad and set `SCR=`/`OLD=` in `wrun.sh`)
- `wrun.sh` (ten-seed runs; `EXTRA=` for switches), `cmp.py A B seeds`, `agg.py A B`, `hold.py TAG`, `leaders.py TAG`, `forms.py`, `sizes.sh`,
  `bind.sh TAG` + `bindcmp.py A.csv B.csv` (village sizes and binding), `marr.py A B YEAR` (marriage tables), `ly.py`/`lycf.py`, `patch.py FILE OLD NEW`.
- Headless switches: `--nohold`, `--holdmeansoff`, `--nopay`, `--nohard`, `--nohead`, `--notribhead`, `--nosplit`, `--nomeans`, `--contests`,
  `--ledger Y --ledgeryears F`; `place --load S --households | --debts | --leaders | --binding | --splits | --marriage | --land | --woods`.
- Control runs on disk: `tc2/out/civ/s*_m2` (C3.5 slice, saves 100/200/300, sim 65), `s*_d3` (the same without the rule), `s*_l5` (C10.3).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push. New `.cs` files need
  their Unity `.meta` committed (a Unity configure or shot run makes them).
- **No subagents. Resource Governor MEDIUM: CPU 70 %, RAM 85 %, at most two heavy jobs** (two ten-seed runs fit; a Unity run must run alone - it takes
  RAM to ~95 %, and Control Room's pressure notices during it are the run itself). Never kill processes you did not start.
- Line endings differ by file (docs CRLF; some .cs files LF, some CRLF): preserve each file's own - write patch scripts to files and read/write bytes.
  Bash heredocs mangle backslashes; write texts with the Write tool.
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it); judge simulation changes by ten seeds against a control on the same
  binary (a `--no...` switch); aggregate counts move x0.8-x1.3 between reruns. Presentation never invents state (the view reads the simulation only).
