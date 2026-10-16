# TC2 — Session 22 report (16 Oct 2026, 22:19 IST – 17 Oct, ~02:15 IST)

Control Room run #51, session 2 (a context continuation of session 21). Stopped by Arjun at ~02:10 IST. Session 21's job queue
(`tc2/out/tools/s21/runq.sh`) kept running throughout: one job at a time, gated on RAM and CPU, at below-normal priority. At the stop it was told to
end after its current job (the full test suite); the jobs it had not started were held (`$Q/hold`). Copies of every job of this session:
`tc2/out/tools/s22/`; results: `tc2/out/tools/s22/results/`.

## What was done

**1. C3.7d the herds on the stubble - merged (D2-155, sim version 80).**
- The twenty-seed comparison (`_u7` against the control `_c6`): births and deaths a person-year at par (x1.002, x1.006); people over years 200-299
  x1.04; hunger x1.00; famines x0.97. Cattle-years x1.64. **Pig-years x0.98 (t -0.2).** Pigs gone by year 300 in 2 of the 19 worlds that kept them;
  the control lost 1.
- **Pigs at the end are chance.** Two runs of the same rules (`_u4` and `_u7`) differ only in the first lived day's walks. Yet their pigs at the end
  differ by x0.46 (t -1.9): a 300-year run lives its first day before it goes COARSE, so a rule about lived walks alone still sends the whole history
  another way. This placebo is now the yardstick for what ten seeds can and cannot show.
- **Sickness from animals rises with the herds.** New sicknesses came over 61 times against 36, and sickness deaths were 10,338 against 4,505 over
  twenty worlds. This is D2-070's spillover, whose daily chance grows with the beasts a village keeps: a real consequence, small in the vital rates.

**2. D2-156 nobody walks through the water - merged (sim version 81).**
- A lived walk whose straight line meets a lake or a bay bends once round it, on the shortest dry way found. The bend is part of the person's state
  (saved and hashed) and the view faces along it. Every lived spot is moved out of the water (the well's spot under a lake's edge, idling, threshing,
  herding). Crofts are not dug into the water. The woods are chosen by the way their cutters actually walk.
- **The same year-300 save, two lived days: 0.178 % → 0.013 % of person-hours under the drawn water** (0.195 % → 0.021 % on another day of the
  year). Woodcutters in the water fell 162 → 3, people walking home to eat 138 → 8, idling 110 → 0, at the well 96 → 12, crofts 44 → 0.
- **Found and fixed during evaluation:** the first version left villages whose only woods lie across water with no woodlot at all (18 villages
  against 6 over ten worlds). With the wood went their plank boats: fish from plank boats x0.14, relief by sea x0.16. Where no woods can be walked
  to dry-shod, the old straight-line choice is used again. Ten seeds afterwards: vital rates at par.
- **Left open:** two island villages in ten worlds still walk to their woods across the water, and one town on two shores (Gaby) walks across its own
  water. Both need boats that carry people across water (C9.5).

**3. C10.4 bread and dependence - built and evaluated, NOT merged** (branch `wip/c104-bread`, worktree `C:/Game Dev/tc-bread`, sim version 82).
- **Measured first** with a new tool, `polity --load SAVE`, over ten worlds at year 300:
  - Polities were nearly absent: 0-3 lords a world, each with 1-2 tributaries. Lordship came only by conquest.
  - Kin does not mark mother and daughter villages. After decades of marriages every pair of neighbours is kin, and no daughter's first household is
    close kin of its mother's (0 of 106 pairs). A founders' lordship over daughters would have to be written in, not read from the state.
  - Relief does mark them. 38 % of neighbour pairs have famine grain still owed between them, and only 15 % of relief ever comes back.
- **The rule.** At its harvest, a village that still owes the neighbour that fed it most in its famines more than a month of its food (beyond all
  its new harvest can spare) bows to that neighbour, if the neighbour is within a day's walk and the stronger. It pays a tenth of each harvest. The
  record has this road into dependence: Attica's hektemoroi, early medieval commendation in famine, chiefs' storehouses.
- **What the bond does.** The tribute gives the grain back, and a lord feeds its own in a famine whatever they owe. Everything else is the existing
  lordship machinery: the lord's call, the lord's answer, the standards in its colour. The settlement page says why a village pays and what it owes.
- **Ten seeds:**
  - Vital rates untouched: people x0.99; births, deaths and hunger a person-year at par.
  - Tribute agreed 9 → 35 (21 for bread). Lords at year 300 8 → 21. The largest lordship grew from 1 to 3 villages. Tribute paid x6.7.
  - **The new lordships fight as lordships.** Tributaries' men answered the lord's call in 28 bands (471 men; none before), and lords raised 11 bands
    over their tributaries' wrongs. Raids rose 79 → 118 and loot x3.6, in the worlds where lordships formed.

**4. D2-158 - the full suite's only failure, fixed on the same branch.**
- The first full suite since session 19 passed 402 of 403. `MigrationTests.AHomeIsRaisedOnlyByLabourAndWood` had been failing since C3.7c the muck,
  under main's earlier rules too.
- The cause: a COARSE day gave the yards' muck its hours before the household's own roof, so a camp household had no house after sixty days. Fixed
  in the lived urges' order, as D2-146 did for the garden (`--oldmuckorder` is the control).
- Ten seeds: vital rates at par; foundings x1.14 (new villages begin in camps and now roof themselves first).
- **The full suite on the final code (the branch tip: main + C10.4 + D2-158): 403 passed, 0 failed** (finished 02:14, after the stop was called;
  `tc2/out/tools/s22/results/fullsuite_b4.txt`).

**5. Tools.**
- `polity --load SAVE`: the ties a polity could be made of.
- `woodways --load SAVE`: what D2-156's woods and croft rules change in a world at a moment.
- `wet --minutes M` and its `walks:` line.
- `--waytally`, `--breadtally`, `--nobread`, `--oldmuckorder`, `--wetways`.
- Shots `--village tributary|lord`. The Unity C# compile check caught a name clash (CS0136) in it, now fixed.

## State at the stop
- **main** (22369c1 + this report): C3.7d, D2-155, D2-156 and its decision; sim version 81.
- **Branch `wip/c104-bread`** (tip 08e16b2): C10.4 + D2-158, sim version 82.
  - Decision drafts: `tc2/out/tools/s22/d2157.md` and `d2158.md`, with their evidence. The full suite passed (403/403), so the branch is ready to merge.
- **Not done:** the Web build and test (`tc2/out/tools/s22/h_a_web.sh`); real shots of a bread lordship and of D2-156; the 900-year check on the
  final rules (`j_l9_s2.sh`, `j_l9_s4.sh`); the Unity compile check on the final tip (08e16b2; de22762 passed, and D2-158 touches Core only).

## Next session
1. The full suite passed (403/403), so:
   - merge `wip/c104-bread` into main (`--no-ff`);
   - write D2-157 and D2-158 into DECISIONS from the drafts;
   - update PROGRESS and `25` (C10.4 done);
   - run `unity.mjs cc` on main.
2. The Web build and test on the final main (saves v82).
3. Real shots: `--village tributary` and `--village lord` with `standard,standardlow,village` on `tc2/out/civ/s2_y3/world_y300.tc2`.
4. The 900-year check (seeds 2 and 4 of `_y3`): do lordships snowball over long runs?
5. Then by evidence: C9.5 boats across the water (island woodcutters, towns on two shores), or the lordships' next mechanism.
