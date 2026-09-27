# Session handoff — W2: sim core done, view not ported (28 Sep 2026, ~01:00 IST)

`docs/PROGRESS.md` is the source of truth for state and numbers. Spec: `docs/world/` (10–17).
Decisions: D-050–D-053 (D-053 is this session). Arjun's rules for this phase: **living world first,
jams secondary; no subagents; hand off at ~700K context.**

## State at handoff
- **W2 sim core**: done and committed. Saves reload bit-identically, worlds regenerated, unit +
  behaviour tests green (B01, B04–B10, B12, B13), the retired `villages.lua`/`people.lua` deleted.
  The society's mechanisms were reworked from what the ledger tools showed (D-053 lists them).
- **Quick suite** (`npm run test:sim`): 90/95 on the run before the last fixes; since then B07 and
  `test_society.save_keeps_every_person_field` fixed, S01 re-banded (check the S01/S15 re-run in
  `out/test_s01.txt`, `out/test_s15.txt` if they finished). Known failing:
  - **S03** farm fire → famine: the scenario strikes at `year = 1.55` of the village's calendar, but
    tropical villages now sow whenever it is warm (D-053), so the fields are already harvested when
    the lightning comes. Port: trigger the action on the village's state (fields ripening,
    `season.phase == "growing"` and a crop past half) instead of a calendar date (needs a small
    `when` hook in `tests/sim/scenarios/lib.lua`). Also check the famine really follows two burned
    harvests now that villages keep stores, fish, forage and emigrate.
  - **S15** tutorial chain (tanker sunk → fish collapse → famine): the regenerated tutorial world
    (seed 8) has different villages; re-scout with `npm run sim:tutorial` (port the tool to the new
    schema first if it breaks) and/or pick another seed; update `sim/tutorial.lua` and D-049.
  - **P08** attribution 82% (< 90%): in S02 (wolves removed) the treated and control worlds' societies
    diverge (famines/migrations/foundings in one and not the other) and those added headlines don't
    chain to the player. Decide with evidence whether (a) P08 should compare only headlines whose
    region the intervention touched, (b) society events should inherit the player's tag when their
    driver (hunting yield, grazers) carries it, or (c) both. Don't just lower the threshold.
- **Web**: `npm run build` OK (require cycles fixed with callbacks). W01 passes again (the opening
  camera now picks the biggest village in daylight), W09 passes. **W07 fails** "pole: centre vs sky":
  since W1's real sun the pole preset looks at polar night at hour 0 — make the check light-aware
  (or look at the sunlit pole). W02/W03/W06/W08 not re-run this session.
- **S01** passes 2/3 after re-banding (s2 forest_nat 0.10 sits exactly on the new floor — watch it;
  s3 g_temp@y200 2.01 > 2.0 from villagers' wood smoke and 1/2 wildfire windows). **S15** fails:
  the lightning sinks the tanker and the oil spreads/beaches, but the chain stops at `biome_shift`
  (no `fish_collapse > famine`): the fishing village no longer starves from one spill (it farms,
  forages, dries fish) — re-scout the tutorial world.
- **P07** re-baselined at 772,714 instr/tick (society 258,732 at ~400 people).

## First things to do next session (in order)
1. `npm run test:sim` (quick) and `npm run test:web`; fix what the handoff fixes broke.
2. Port S03, S15, P08 as above.
3. **Seed 9 chronic famine** (`node scripts/lua.mjs tools/society_ledger.lua --seed 9 --years 100
   --summary`, then `--from 70`; `out/tmp/mig.lua 9 70 90` for flows): after year 70 villages at or
   over their land capacity cycle through famine and migration (3,000 moves a decade) and lose half
   the population in years 90–100. Suspects: the capacity estimate (`farming.capacity`: all ring
   cells x learned yield + 8% of fish) is too generous, so marriages and in-migration continue past
   what the land feeds; soil exhaustion lowers yields later; and there is no relief between villages
   (W3 trade / kin gifts). Look at yields per field and fertility over time in that seed first.
4. **W2c view** (the user's section 5): houses/sites/ruins from `W.ent.buildings` via
   `sim/society/layout.lua` (replace `v.houses` counts in `game/view/view.lua`/`props.lua`/
   `propgen.lua`); people from `sim/society/schedule.lua` (replace the random targets in
   `game/view/puppets.lua` ~line 281); boats from `b.trip` (depart/at/leave/ret, ground; moored
   otherwise; `W.ent.boats` is a map id → boat, not an array — `game/view/ents.lua` `M.boats`);
   parties from `path`/`depart`/`arrive` via `schedule.party_pos`; fields from `W.S.farm[c]`
   (sown/bio/ripe/harvested); construction progress; the inspector (stocks, `v.acc_last`,
   households and homes, fields, boats, people's task/site). Then screenshots (`npm run shots`).
5. Performance (W6, with the view): multi-rate person updates (half the village each week),
   `labour.assign` (~14k/village-week) and `life.weekly` (~11.5k) are the largest items
   (`out/tmp/prof_soc.lua <seed> <years>`).

## Tools
- `node scripts/lua.mjs tools/society_ledger.lua --seed 22 --years 30 [--spinup] [--summary] [--village N] [--from Y]`
  — yearly flows per village from `v.acc_last`: births, deaths by cause, marriages, migrants,
  harvest/catch/forage/meat, eaten vs need, spoilage, wood, hours by task, jobs.
- `node scripts/lua.mjs tools/save_check.lua --seed N --weeks W --hours H` — save mid-year, reload,
  step both, diff the first divergence path.
- `tools/society_trace.lua` (village snapshots + event chains), `out/tmp/prof_soc.lua`,
  `out/tmp/mig.lua`, `out/tmp/field_dbg.lua` (a village's fields week by week in the spin-up).

## Pitfalls met
- Git Bash heredocs break on apostrophes: write Python patch scripts with the Write tool.
- Background jobs started with `( ... ) &` inside one Bash call may die with the shell: use the
  tool's `run_in_background`.
- A table with keys 1..n plus holes can pass a "count == #t" array test (fixed in `sim/save.lua`).
- Bob rejects require cycles even through lazy `require` inside functions: use callbacks.
- The yearly account (`v.acc`) rolls over at the UTC year boundary: a crisis straddling it is split
  between two ledger years (it looked like people "vanished").
- Behaviour changes change the spin-up, so the Year-0 worlds change: regenerate
  `assets/worlds/*.tcsave` after any sim change (`tools/make_world.lua`, `--seed 8 --out
  assets/worlds/tutorial.tcsave`), then `unit.test_worlds` passes.
