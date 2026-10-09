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
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 16, second context"** (then "Session 16") (exact state, the running/unfinished work, Next in order).
3. `docs/tc2/DECISIONS.md` newest entries **D2-129** (C3.5 household formation; it corrects D2-126), **D2-128** (C10.3 a contested succession splits a village), **D2-126** (C3.3 household grain, debts, makers paid, hard times) and **D2-127** (C10.2 the first household).
4. `docs/tc2/07_AGENTS_AND_LIFE.md` §7e (C3.5); `docs/tc2/26_CRAFTS_AND_EXCHANGE.md` §2c (C3.3 design, result and its correction); `docs/tc2/10_CONFLICT_AND_POLITICS.md` §12c (C10.2) and §12d (C10.3).
5. `docs/tc2/PROGRESS.md` CURRENT STATE (four session 16 entries); `git status` (the modified `.claude/launch.json` and untracked
   `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them), `git log --oneline -5`.

## Where things stand (9 Oct 2026, session 16's second context)
- **C3.3a-c committed (889263b, sim version 62)**: `Core/Places/Holdings.cs` - household grain in the village's granary, the moral economy and debts,
  makers paid in kind, sharing contracting in hard times (D2-126). **C10.2 committed (78ac1ee, sim version 63)**: `Core/Society/Leaders.cs` -
  followings, the first household and headman, its gifts in hard times, tribute to the lord's first household (D2-127).
- **C10.3 closed (D2-128, sim version 64)**: at a vacancy an unbound rival with half the first's following and a village's worth leads them away to the
  best place the village knows, or looks for land (scouting) and gives up after three years. Ten seeds `_l5` vs `_l3` (= `--nosplit`): 42 splits,
  people x0.992, foundings 123 -> 119 (land decides how many; splits take 35 %), conflict at par, home making x1.10 (more people in villages too small
  for a shed while land is open); towns of 400+ keep large unbound factions. Pages: both villages tell the split; the history tab carries headmen and
  splits (`HistoryLedger.PlaceEventsOf`). Tools: `place --splits`, the `contests:` line. Full suite 333/333.
- **Measured after C10.3 (Web test s16web 6/6 on it)**: there is no famine amid plenty - at 95 % of ~40,000 hard-times hunger deaths the households
  with grain could spare under a quarter of what the grainless needed; the store held under a day's food in 69 %; deaths 5-12 days before the harvest.
  Hunger was 19 % of deaths and chronic, and women married at 20.3 in every world and season whatever the bread.
- **C3.5 household formation (D2-129, sim version 65; `07` §7e)**: a woman's suitors are only the men with whom what the couple brings of their
  households' grain and larder keeps them to the next harvest of his village and six days beyond; none: the match waits. Pages (person family
  section, settlement "Marriages wait: ...", the marriage's record), `place --marriage`, `--nomeans` (= `_d3`). Ten seeds `_m2` vs `_d3`: hunger deaths
  x0.69, famines x0.70, births x0.88, people at par over years 200-299 (x0.935 at year 300); age at first marriage 20.3 -> 21.4, never married by
  45-59 0 -> 4 %. `HouseholdFormationTests` 3; full suite 336/336; no measurable cost.
- **The Web build in `tc2/web-build/web` is of b8f4830 (C3.5)**: Web test s16web2 6/6 (bit-identical).
- Open W task **W-FOREST** (Arjun, 8 Oct): forests/biomes still read as the same trees recoloured.

## Do next
1. (done: Web build and Web test s16web2 6/6 of C3.5.) Web builds: `node tc2/scripts/unity.mjs build-web --jobs 3` (~7 min; RAM peaks ~95 % - run
   nothing beside it), then `node tc2/scripts/webtest.mjs --tag NAME`.
2. From the evidence (D2-129 "Open"): hunger is still 17 % of deaths, in the hungry gap before the harvest - measure what a village does in its last
   weeks (the ration's floor 0.45, wild food, slaughter, relief) before choosing; household land and holdings (marriage on a holding) when land
   binds; life-cycle service; factions a full land cannot shed (D2-128); debts' consequences; C9.4 cargo; D2-125's iron ask; D2-123 floods; W-FOREST.

## Tools (all in `tc2/out/tools/`; copy to the session scratchpad and set `SCR=` in `wrun.sh`)
- `wrun.sh` (ten-seed runs; `EXTRA=` for switches), `cmp.py A B seeds` (paired annals/report ratios), `agg.py A B` (report quantities), `hold.py TAG`
  (C3.3 `hold:`/`makers paid:`), `leaders.py TAG` (C10.2 `leaders:`), `forms.py TAG...` (house plans, markets), `sizes.sh TAG YEAR` (village sizes and
  binding; needs a save of the repo build's sim version), `ly.py`/`lycf.py` (household-year ledger), `patch.py FILE OLD NEW` (line-ending-aware replace), `bind.sh TAG` (binding table of a tag's saves) and
  `bindcmp.py A.csv B.csv` (village sizes and binding by size and year).
- Headless switches: `--nohold`, `--holdmeansoff`, `--nopay`, `--nohard`, `--nohead`, `--notribhead`, `--nosplit`, `--contests`, `--ledger Y --ledgeryears F`;
  `place --load S --households | --debts | --leaders | --binding | --splits | --marriage`, `--nomeans`; scratchpad `marr.py A B YEAR` (marriage tables).

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
