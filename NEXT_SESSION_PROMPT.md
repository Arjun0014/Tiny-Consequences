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
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 16"** (exact state, the running/unfinished work, Next in order).
3. `docs/tc2/DECISIONS.md` newest entries **D2-126** (C3.3 household grain, debts, makers paid, hard times) and **D2-127** (C10.2 the first household).
4. `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2c (C3.3 design and result); `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12c (C10.2) and §12d (C10.3, in progress).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (two session 16 entries); `git status` (the modified `.claude/launch.json` and untracked
   `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them), `git log --oneline -5` (HEAD 78ac1ee).

## Where things stand (9 Oct 2026, end of session 16's first context)
- **C3.3a-c committed (889263b, sim version 62)**: `Core/Places/Holdings.cs` - each household's grain in the village's granary (the harvest by mouths;
  own beasts' and catch's produce own), the village's common grain, a daily account (losses nobody drew borne by all by their shares), the moral economy
  (own grain -> common grain -> kin's spare as gifts -> loans recorded as debts, repaid from surplus), makers paid the food of the maker-days their make
  took as far as the taker's grain pays, the village's levy for jars and smiths, sharing contracting to household and kin below a ration of 0.7. Ten
  seeds: people at par, famines x1.25 with hunger deaths x0.9-0.95 (famine amid plenty), takings/raids down. Full suite 326/326.
- **C10.2 committed (78ac1ee, sim version 63)**: `Core/Society/Leaders.cs` - followings (kin, debtors, clients via `Household.Patron`), the first household
  and headman (`Settlement.Head/HeadHh`, `Hold` 0.5, succession within the household), its gifts in hard times, tribute to the lord's first household.
  Ten seeds `_l3` vs `_h5`: people x1.03, vital rates at par. Full suite 331/331 on C10.2.
- **C10.3 IN PROGRESS (in 78ac1ee, sim version 64)**: at a vacancy an unbound rival with half the first's following and a village's worth leads its
  following away to a known place (`Leaders.Contest`, `Migration.Split`, `VillageSplit`, `--nosplit` = `_l3` exactly). The first version was at par but
  rare (49 of ~50 qualifying contests lacked a known place), so a faction that knows no place now looks for land (`Settlement.SplitHh`, scouting,
  `Leaders.Pending`, gives up after 3 years). Verified on 78ac1ee: `LeadersTests` 7/7, `HoldingsTests` 12/12, Migration 6/6, SaveLoad 8/8. **Not yet**:
  ten seeds of the pending-split version, the full suite on it, D2-128, `LeadersTests.cs.meta` (run Unity configure).
- **The Web build in `tc2/web-build/web` is stale/mixed** (C10.2 sources were edited during it): rebuild before any Web test. Last good Web test: s15web.
- Open W task **W-FOREST** (Arjun, 8 Oct): forests/biomes still read as the same trees recoloured.

## Do next
1. `node tc2/scripts/unity.mjs configure` (makes `LeadersTests.cs.meta`; commit it). Copy `tc2/headless/app/bin/Release/net8.0` to the scratchpad as
   `bin_l5` and run ten seeds: `BIN=bin_l5 TAG=_l5 YEARS=300 SNAP=100,200,300 EXTRA=--contests bash wrun.sh 4 42 2 1 5 3 11 8 7 6`; compare with `_l3`
   (`python cmp.py l3 l5 ...`, `agg.py l3 l5`, `leaders.py l5`, the `contests:` line, foundings and village sizes `bash sizes.sh _l5 300`). Accept if
   splits become real (tens a world) with people at par and towns that hold integrate (lords, large followings); else weigh the thresholds or reject.
2. Full suite (`node tc2/scripts/headless.mjs test`, ~13 min), D2-128 (`10` §12d has the design and the first version's numbers), PROGRESS, SESSION_HANDOFF,
   commit "C10.3 closed".
3. Web build (`node tc2/scripts/unity.mjs build-web --jobs 3`, ~12 min; RAM peaks ~95 % - run nothing beside it) and Web test (`node tc2/scripts/webtest.mjs
   --tag s16web`): the person/place pages (shots 5b-5f) should show "Its own grain in the village's granary..." and "Its first household...".
4. Then from the evidence: the headman deciding (tribute, walls, bands), debts' consequences (work for a creditor, flight), the hungry taking from their
   own village's store, household land when it binds; C9.4 cargo once exchange carries volume; D2-125's unmet iron ask; D2-123 floods; W-FOREST.

## Tools (all in `tc2/out/tools/`; copy to the session scratchpad and set `SCR=` in `wrun.sh`)
- `wrun.sh` (ten-seed runs; `EXTRA=` for switches), `cmp.py A B seeds` (paired annals/report ratios), `agg.py A B` (report quantities), `hold.py TAG`
  (C3.3 `hold:`/`makers paid:`), `leaders.py TAG` (C10.2 `leaders:`), `forms.py TAG...` (house plans, markets), `sizes.sh TAG YEAR` (village sizes and
  binding; needs a save of the repo build's sim version), `ly.py`/`lycf.py` (household-year ledger), `patch.py FILE OLD NEW` (line-ending-aware replace).
- Headless switches: `--nohold`, `--holdmeansoff`, `--nopay`, `--nohard`, `--nohead`, `--notribhead`, `--nosplit`, `--contests`, `--ledger Y --ledgeryears F`;
  `place --load S --households | --debts | --leaders | --binding`.

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push. New `.cs` files need
  their Unity `.meta` committed (a Unity configure makes them).
- **No subagents. Resource Governor MEDIUM: CPU 70 %, RAM 85 %, at most two heavy jobs** (two ten-seed runs fit; a Unity run must run alone - it takes RAM
  to ~95 %). The machine idles at ~70-78 % RAM. Never kill processes you did not start.
- Docs are CRLF: edit them with the Edit tool or `patch.py`, never with plain Python `replace` on `\n` text. Bash heredocs break on nested quotes and
  backslashes: write patch scripts and texts to files with the Write tool.
- Don't rebuild the headless binaries while a suite or run uses them (runs use scratchpad copies `bin_*`; the suite uses the repo build).
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it); judge by ten seeds against a control on the same binary (a
  `--no...` switch); aggregate counts move x0.8-x1.3 between reruns.
