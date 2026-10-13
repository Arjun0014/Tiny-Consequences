# TC2 — Session 20 report (13 Oct 2026, 17:25–21:00 IST)

Control Room run #55, session 2 (a context continuation of session 19). Stopped by Arjun at ~20:55 IST. Arjun's resource rule (D-1/D-2) was kept
by one job queue (`tc2/out/tools/s20/runq.sh`): one job at a time, gated on RAM < 86 % and CPU < 85 %, at below-normal priority; builds jump ahead of
seed runs. Nothing is left running.

## What was done

**1. Session 19's queues read (q19-q21).**
- Threshing (C3.7b) over ten seeds was at par. The fish's x0.94 follows the villages, not the floor's hours: fish a person-year x0.95 (t -1.5); the
  threshing worlds ended with fewer fishing villages. Session 19's seeds never founded more villages with the floor than without (26->23, 12->9,
  20->16, 17->14, 13->11), so fresh pairs on one binary were run: foundings 15/15, 11/11, 26/27 - no deficit (D2-149).
- The muck worktree's two failing tests were test faults: one assumed the crop still growing six days after a world opened in the growing stage's
  last week; the other expected linear leaching where the law is exponential. A third (children carrying water) was a five-child sample tipped
  from one to none by the muck's extra draw: it now counts three worlds.

**2. Real shots judged, faults fixed.**
- **Graves** read as a garden's rows over the plough plots: the kit's length lay along the rows. **In the town the burial ground was built over** -
  over the whole world 25 of 29 grounds, 1,145 of 3,480 slots. **D2-154**: homes, works and crofts keep off a laid ground (`--oldgraves` the
  control, byte-identical to `_m1`); graves turned side by side across their rows, jittered, with a headstone and sward under them. Covered slots
  fell to 498 in seed 4; over ten seeds the rest is streets worn across the ground (2,126 of 2,159).
- **Stumps** were nearly absent from a half-felled woodlot. Three faults: the forest's placing skipped ground felled bare; a pooled patch's sea
  vertices kept stale felled shares; and the woodlot view framed a glade (242 stumps were drawn, at the frame's edges). The view now aims at the
  stands' felled band.
- **The threshing floor** stood empty in most shots (threshers are few at any moment). New shot views `floorbusy` and `act:<Activity>` find the
  busiest one; the floor in use is now drawn (the sheaves laid out between the threshers, the grain swept up). Six threshers in a ring read as
  threshing.
- **The standard** reads (a teal swallow-tailed cloth by the first household's door).
- **The rain-forest view's empty frame** was a one-off loading glitch: the next shot shows dense forest.

**3. C3.7c the muck merged (D2-153, sim version 79).** Controls `--nomuck` = `_m1` and, after the merge with D2-154, `--nomuck --oldgraves` = `_m1`
(both byte-identical). Ten seeds `_n1` against `_m1`: **at par** (people over years 200-299 x1.004; births and deaths a person-year x0.996 and x0.988;
hunger x0.97; famines x0.97). 477 kt of muck made, 92.5 % carted, 7.4 % leached, 3.2 h a tonne; the carting's hours are 18 % of the threshing
floor's. The real shot `act:Muck` showed carters with handcarts across the stubble - and 126 of them within 60 m of one spot.

**4. C3.7d the herds on the stubble and the fallow - built, NOT merged** (worktree `C:/Game Dev/tc-stubble`, branch `wip/c37d-stubble`, sim
version 80; draft D2-155 `tc2/out/tools/s20/d2155.md`).
- Measured first: the herds are fed because the herders keep them to the pasture's lean days. Cattle's commonest end is that cull (5,156 of 9,548).
- Built:
  - The stubble from the harvest to the sowing (300 kg a hectare) and the rested arable are grazed before the waste.
  - The herds' day dung there goes onto the fields with no carting.
  - The herd's ground is the stubble while it lasts.
  - Each household's field hands and carters work its own field cell (`FieldSpots`; before, a town's carters stood in one crowd).
  - A food's cull takes the kind whose spares eat most of it (`--oldspare`).
- Controls byte-identical (`--nostubble --onefieldspot --oldspare` = `_n1`). Tests pass (Stubble 3, Livestock 15, Muck 3 and seven neighbours).
- Ten seeds `_u2` against `_n1`:
  - People are at par (x0.99), the fields bear more (x1.017, t 2.7), cattle x1.84, goats x1.38.
  - **But pigs vanish from three of ten worlds** (seeds 5, 11, 7: 36, 62, 71 -> 0). The first version lost them in two (seeds 2, 8), so the cull
    change did not cure it.
  - Foundings x0.77 (t -2.4).
  - Not merged until the pigs are understood.

## Verified

- MuckTests 3/3 and HouseholdWork 4/4 on the merged main. BurialsTests 4/4.
- Footprints 8, Homes 5, Streets 3, Structures 6, Garden 5, Tilth 9, Livestock 14, Coarse 3, Determinism 6, SaveLoad 8, Threshing 5, Carts 6. No
  float arithmetic.
- The Unity C# compiles (`unity.mjs cc`, 633466f).
- Every control was byte-identical.
- Ten seeds `_n1` at par.

## Not done (the next session's work)

- **C3.7d's pigs**: find why they die out where the cattle grow, fix, ten seeds, merge; then shots of the herds on the stubble.
- **The full headless suite on HEAD** (last full run 372/372 on 86b481d, before threshing).
- **The Web build and test** (saves are now version 79: new worlds).
- `107c100`'s woodlot framing (aim at the felled band) compiles (`unity.mjs cc` on 4dd9b94, at the handoff) but is not yet shot.
- Muck heaps in the yards have not yet been seen in a shot (the `close` view at day ~30).
- The remaining threshing pairs (seeds 13-19, scripts in `tc2/out/tools/s20/q_todo`) are optional now.

## Commits

On main: 49e324a, 34215af, d601c24 (the muck merge), 633466f, 107c100, 04a8639 and the handoff commit.

On `wip/c37d-stubble`: 1f30e26, 4f8a573, 08a448f and the test premise commit.
