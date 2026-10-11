# Session 18 report (11 Oct 2026, 06:48 to ~16:00 IST)

Arjun resumed the stopped session 17 with one condition: low-CPU work only and proper development. The rules, set in the Decision Inbox, were one
job at a time at below-normal priority, first while RAM < 86 % and CPU < 50 % (D-1), then CPU < 85 % (D-2). Other applications kept the machine
at 60-100 % CPU for hours, mostly a Chrome tab drawing WebGL in software. Jobs waited at the gate for long spells and then ran slowly: a test class
took 13 minutes instead of 24 seconds, and the Web build took 73 minutes on one job. That, not the work, is why the session moved slowly.

## What changed in the game (all committed, each verified)

| Decision | What | Evidence |
|---|---|---|
| **D2-144** | Southern villages' gardens ran on the north's calendar: dug while the village reaped, gathered while the crop grew. Now every use of a village's calendar goes through one function (`SettlementSystem.DayOfYear`). | A new test over both hemispheres, which fails on the old code; control byte-identical; ten seeds at par; the gardens' food x2.05, north and south alike. |
| **D2-145** | The first real shots of the gardens. Beds are now drawn by season: fresh dug earth, a weedy rough where undug, green in leaf, cleared when gathered. Each croft has one wattle fence instead of hurdles round every bed, which read as stacks of crates. A croft's side strips no longer overlap the strips behind (ground counted twice, and the woodpile's yard). | Tests (no overlapping croft ground); control byte-identical; ten seeds at par; shots `tc2/out/shots/s18a-d`, `s18_gardens_sheet.png`. |
| **D2-146** | The first full suite since the gardens: 369/371. Both failures were the garden's COARSE day taking hours before the household's own roof and before foraging when hungry. It now follows the same urges as lived days. | Both tests pass (COARSE and lived wild hours in a shortage x0.82, was x0.59); neighbours pass; control byte-identical; ten seeds at par. |
| **D2-147** | D2-146's seeds hung (seed 6, year 160). Traced with a new tool (`run --trace FILE`): a tectonic step drowned a village and the search for a free lot walked its drowned streets forever. A latent bug since C13.1; the search is now bounded. | A regression test (a drowned village finds a lot at once); seeds 4, 5 and 1 byte-identical before and after; seed 6 runs. |

## Checks

- **900 years** (seeds 4 and 42, gardens and soil): stable. Seed 4 holds 8,000-11,000 people from year 350 (11,149 at year 900); seed 42 holds
  4,300-6,000 (5,667). Hunger is 0.3-14 per 1,000 person-years, epidemics come and recover, the soil settles (15 % resting, a sown hectare bears 0.76
  of a fresh crop), and 83 % of households keep a garden. No collapse and no runaway.
- **Web build and Web test `s18web1`: 6/6.** WebGPU; browser and headless bit-identical; a reload continues the saved world; a day away catches up
  about 96 years (531 people in 17 places); no console errors.
- **Full suite**: on d224365, 369/371 (both fixed by D2-146). On c7edc68 it was still running at the stop (about 85 % through, no failures). It was
  stopped for the handoff, so it must be run once next session.

## Prepared, not applied

**W-FOREST pass 2b** (`tc2/out/tools/s18/pass2b.diff`, queue `q10.sh`, draft `d2148.md`). The rain belt follows the sun in the outer tropics, so
the dry tropics get a real dry season in their soil, and their woodland drops its leaves as that soil dries. Today the dry and wet tropics read alike
because the state has no dry season. The patch applies cleanly; it was held back so the Web build tested only verified code.

## Next

1. The full suite once on HEAD.
2. W-FOREST pass 2b: apply, validate (control `--oldrain`, a seasons survey before and after, ten seeds, shots of four climates in two seasons), then
   Arjun's judgement in real shots.
3. C3.7b threshing (sheaves held until threshed: real off-season work at the granary) and C3.7c the muck; the remaining visual-truth items.
