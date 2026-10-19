# TC2 Session handoff

## Session 23 (19 Oct 2026, from 18:54 IST; Control Room) - IN PROGRESS; one job queue running

### Exact state (keep current)
- **main 3712056**: C10.4 merged (ca3b4c8, `--no-ff`) + DECISIONS D2-157, D2-158 + `25` (C10.4 done). Sim version 82. `.claude/launch.json` (modified)
  and `Assets/Resources.meta` (untracked) are not ours - leave them.
- **Branches (worktrees)**, none merged yet:
  - `wip/s23-tools` (`C:/Game Dev/tc-s23`, 8e06c4a): tools only - `lordships --load SAVE [--every N] [--bonds]`, `run --load SAVE --warmroutes`.
  - `wip/c105-might` (`C:/Game Dev/tc-might`, 7d426d3; contains the tools): **C10.5 a lordship's might** (D2-159 draft `tc2/out/tools/s23/d2159.md`,
    sim version 83, `10` §12g, `25` C10 by slices). Binary `bin_c2` (scratchpad), its tests pass.
  - `wip/route-ground` (`C:/Game Dev/tc-routes`, 2e4914c; contains the tools): **D2-160 the ways' woods** (draft `tc2/out/tools/s23/d2160.md`, sim
    version 84, `03` §4) - a determinism fix: watching changed history (see below).
  - `wip/c106-tribute` (`C:/Game Dev/tc-tribute`, cbb502c; on C10.5 incl. its indexed kin test 5983f04): **C10.6 tribute carried** (draft
    `tc2/out/tools/s23/d2161.md`, sim version 85): the tenth set aside at the harvest and carried to the lord's store by the tributary's people
    (lived walkers and carts; COARSE the same loads), each load wearing the way; standards enlarged with distance up to 4x (view). Tests 5/5 + neighbours.
- **Merge plan**: C10.5 (sim 83, D2-159) -> D2-160 (84; resolve the version line) -> C10.6 (85); then on main: full suite, `unity.mjs cc`, a v85
  seed-2 world for real shots (tributary, lord, the roads between them), the probe `--warmroutes` on it (equal hashes), the Web build and test, and a
  ten-seed run of the final main against `_t1`.
- **DONE: the integration branch `wip/s23-int`** (worktree `C:/Game Dev/tc-int`, tip c5258d2): main 0402dba + `--no-ff` merges of C10.5 (5a0c4f1),
  D2-160 (a5583f4, version line 84 over 83) and C10.6 (dd81998, version 85 over 84) + a tools commit. Builds (`bin_i1` = dd81998), floatcheck clean.
  Main has since gained docs commits only (2619bb0): to land, `git merge --no-ff wip/s23-int` on main (or merge main into it and fast-forward).
  NOTE: the C10.6 branch alone also says sim version 85 (without D2-160's `WayForest`): read `_t1` saves only with `bin_t7`, `_f1` with `bin_i*`.
- **Queue order at the context handoff (19 Oct ~21:12)**: `3_cr_3_cmp_probe` (D2-160: `cmp_y3_r2.txt`, the probe on `s2_r2` - expect equal hashes
  with and without `--warmroutes`), `3_cs_0_c3` (C10.5's indexed kin test: `_z2` = `_z1` byte-identical and the speed), `3_d_l9z1_s2/s4` (C10.5's own
  900-year check -> `results/lordships_s*_l9z1_y600.txt`: do lordships now grow, hold, or snowball?), `3_t_*` (C10.6: build `bin_t7` and tests, the
  `--oldtribute` control = `_z1`, ten seeds `_t1`, `cmp_z1_t1.txt`, the ways between the bound and their lords), `4_a_fullsuite_i1` (the full suite on
  the integration), `4_b_f1_*` (ten seeds `_f1` of the integration), `4_c_cmp_probe_f1` (`cmp_t1_f1.txt`, the probe on `s2_f1`). Verified so far:
  C10.5 control `--oldmight` = `_y3` byte-identical; C10.5 ten seeds `_z1` vs `_y3` at par (people x1.006, births x1.003, deaths x1.002; raids x0.89
  n.s.; lords x1.04) - in the first 300 years the old and new weighings mostly agree (seed 11: bowing to raiders the old count 15 times, might 4);
  its runs took 18 % longer (the kin scan; fixed by `KinIndex`, 5983f04, check pending); D2-160 tests pass, control `--oldways` = `_y3` byte-identical.
- **Next after the queue**: write D2-159, D2-160, D2-161 from the drafts with the evidence; land `wip/s23-int` on main; `unity.mjs cc`; a v85 seed-2
  300-year world (`--snap 300`) for real shots (`--village tributary|lord`, views standard, village, region: do the standards and the converging
  roads read?); the Web build and test; PROGRESS, `25`. Then the next slice by evidence: the lord's household men (C10.7, `23` §3: `lordships` now
  prints each lord's first household's grain against its needs and the tribute in - the measure), or C9.5 boats across the water (needs a lived
  "legs by boat" model: a boat outing is one out-and-stay activity today and the walk home starts from the water point).
- **The queue**: `bash tc2/out/tools/s23/runq.sh` (background), queue dir `Q` = this session's scratchpad `q` (`todo`, `done`, `log`, `runq.log`;
  `touch $Q/stop` ends it after the current job). Jobs (copies in `tc2/out/tools/s23/`): `2_a_shots_bread` (real shots s23a tributary, s23b lord on
  `s2_y3`), `3_a_ctl_z0_s4` (`--oldmight` = `_y3`?), `3_b_z1_*` (ten seeds C10.5), `3_c_cmp_y3_z1`, `3_cr_*` (D2-160: build `bin_r2` and tests,
  `--oldways` control, ten seeds `_r2`, comparison and the probe on a D2-160 world), `3_d_l9z1_s2/s4` (C10.5's own 900-year check). Results:
  `tc2/out/tools/s23/results/`. If the scratchpad is gone: rebuild a binary from its worktree (`2_3_build_c2.sh`, `3_cr_0_build_r2.sh`) and rerun.

### Verified this session
- Unity C# compiles on main (`unity.mjs cc`); **Web build and test `s23web1` 6/6**; cross-runtime determinism: browser (WebGPU, IL2CPP) = headless
  dotnet = editor Mono, bit for bit. Frame rates were half session 18's because the user's Chrome ran heavily during the test - re-measure quietly.
- **The 900-year check of C10.4** (`_y3` seeds 2 and 4, years 300-900; `lordships`): seed 2 - 49 bonds, 39 ended, every one "the lord no longer the
  stronger"; 4-9 lords among 27-31 villages; the largest lordship never above 4 villages; in year 464 a mild epidemic (12 villages, 6 dead) ended
  five lordships; flickers (bound and freed within 2-5 years, 10 rebindings). Seed 4 - 30 bonds, 24 ended, all "no longer the stronger"; the largest
  never above 3. **Verdict: no snowball and no growth - petty lordships capped by a seat-only, day's-count weighing** -> C10.5.
- **The route probe** (`run --load s2_y3/world_y300.tc2 --warmroutes`): three lived days b5329437ba119770 without, 8101ffe7abef7b57 with the view's
  109 routes built at load -> D2-160 (fix built, RouteGroundTests 3/3, SaveLoad/Afoot/Determinism/Trade/Voyage pass).

### Next, in order
1. Read the queue: shots s23a/s23b (judge: does the lordship read in the picture?), `cmp_y3_z1.txt` and `lordships_s*_z1.txt` (C10.5), the D2-160
   control, `cmp_y3_r2.txt` and the probe on the D2-160 world (equal hashes expected), C10.5's 900-year check.
2. Merge C10.5 (`--no-ff`, D2-159), then D2-160 (sim version 84 on top), the tools branch with them; then the full suite, Unity cc, a v84 world for
   shots, the Web build and test.
3. Then by evidence: tribute carried to the lord's seat (C10.6: makes the polity visible and its reach a cost), C9.5 boats across the water, the
   lord's household men.

## Session 22 (16 Oct 22:19 - 17 Oct ~02:15 IST; Control Room run #51 session 2) - stopped by Arjun; report `SESSION_22_REPORT.md`

### Exact state at the stop
- **main**: C3.7d merged (9d68eaa) + **D2-155**; **D2-156 merged** (f200b9c) + its decision (40f4a18); PROGRESS, this handoff, the report. Sim
  version 81. `.claude/launch.json` (modified) and `Assets/Resources.meta` (untracked) are not ours - leave them.
- **Branch `wip/c104-bread`** (worktree `C:/Game Dev/tc-bread`, tip **08e16b2**, sim version 82, NOT merged): **C10.4 bread and dependence**
  (`10` §12f; 1f05c2b, f4bffc6), main merged in (b4dd299), shots `--village tributary|lord` (863d170, de22762), **D2-158** (08e16b2: a COARSE day's
  muck waits for the household's own roof). Decision drafts with their evidence: `tc2/out/tools/s22/d2157.md` (fill "Full suite" and "Unity C#"),
  `d2158.md` (fill "Full suite").
- **The queue stopped** (02:13:57) after its last job, **the full suite on `bin_b4`** (the branch tip's code: main + C10.4 + D2-158): **403 passed,
  0 failed** (`tc2/out/tools/s22/results/fullsuite_b4.txt`; bin_b3 had 402/403 - the one failure D2-158 fixes). Nothing is running. Held, not run: `i_g_cc_bread3.sh`
  (`unity.mjs cc` on the bread tip), `j_l9_s2.sh`, `j_l9_s4.sh` (the 900-year check) - in `$Q/hold` and `tc2/out/tools/s22/`. To run jobs again,
  start a fresh runner (copy `tc2/out/tools/s21/runq.sh` and `lib.sh`, set `Q`/`S`/`N` to the new scratchpad, copy the binaries `bin_b4`... or
  rebuild).
- Worktrees: `tc-dry` (wip/dry-ways, merged - may be removed), `tc-stubble` (merged), `tc-bread` (C10.4, to merge), older ones as before.
- Saves (v82): `tc2/out/civ/s*_y3/world_y300.tc2` (ten seeds, the final rules), `s*_y2` (C10.4 without D2-158). v81: `s*_x2`.

### Verified this session
- **D2-155**: twenty seeds at par a person-year; pig-years x0.98; a placebo (`_u4`/`_u7`, the same rules apart only by the first lived day - a
  300-year run lives its first day before going COARSE) shows end-of-run pig counts and sickness are noise at ten seeds.
- **D2-156**: AfootTests 6/6 and 66 neighbours, floatcheck; `--wetways` = `_u7` byte-identical; the same save 0.178 % -> 0.013 % of person-hours in
  the water; ten seeds `_x2` at par a person-year (seed 4's epidemic phase drives the people x0.91); `woodways`: 22 of 164 villages fetch other
  woods, crofts x0.99. Open: island woodcutters (3 villages in ten worlds), a town on two shores - boats across water (C9.5).
- **C10.4** (`_y2` against `_x2`): vital rates untouched; lords 8 -> 21, tribute agreed 9 -> 35 (21 for bread), the lord's call answered (471 men),
  raids 79 -> 118. BreadTests 5/5 and seven neighbour classes; `--nobread` = `_x2`; Unity cc ok on de22762.
- **D2-158** (`_y3` against `_y2`): MigrationTests 9/9 (fails again with `--oldmuckorder`), neighbours pass; control byte-identical; vital rates at
  par, foundings x1.14.

### Next, in order
1. The full suite passed (403/403): `git merge --no-ff wip/c104-bread` on main; DECISIONS D2-157 and D2-158 from the drafts (fill "Full suite:
   403/403 on the branch tip's code"); PROGRESS; `25` (C10.4 done); `node tc2/scripts/unity.mjs cc` on main (de22762 compiled; 08e16b2 changes Core only).
2. The Web build and test on the final main (`tc2/out/tools/s22/h_a_web.sh`: `unity.mjs build-web --jobs 2`, `webtest.mjs --tag s22web1
   --export`; saves v82).
3. Real shots of a bread lordship (`node tc2/scripts/unity.mjs shots --tag s23a --load "tc2/out/civ/s2_y3/world_y300.tc2" --village tributary --views
   standard,standardlow,village --hour 11`; then `--village lord`) - the standards in the lord's colour; D2-156 at a lakeside village.
4. The 900-year check on `_y3` seeds 2 and 4 (`j_l9_*.sh`): lordships over long runs (do they snowball or hold?).
5. Then by evidence: C9.5 boats across the water (island woodcutters, towns on two shores), or the lordships' next mechanism (C10.5).

## Session 21 (16 Oct 2026, 19:12-22:05 IST; Control Room run #51 session 1) - context handoff; ONE job queue still running

### Exact state
- **main c106378** + this handoff's docs commit (clean but for `.claude/launch.json`, `Assets/Resources.meta` - not ours). Nothing of session 21's
  code is on main yet.
- **Branch `wip/c37d-stubble`** (worktree `C:/Game Dev/tc-stubble`, tip **161fa63**, sim version 80, NOT merged; main c106378 merged into it as
  9b81854). Session 21's commits on it: 5193bb0 **the hands' cull by what loads the hands** (`Livestock.Spare` mode 0 by the spares' food,
  `--oldhands`; tools-only herd diagnostics), b837a30/c4f9347 the stubble drawn as its herds eat it (`TerrainLod.CropColour`), shots `--doy D`
  and the season log, d8bf37d **a household's field cell is one its hands walk to dry-shod** (`FieldSpots.DryWay`, `--wetfieldspot`), tools
  `wet --doy`, `crowds`, 905d3d6 Unity metas (FieldSpots/Stubble/StubbleTests), 0e93fb7-180ad85 shot views `muck`/`mucklow` and **the muck heap
  placed clear of the croft's beds, the pen and the woodpile/cart** (`WorldObjectsView`), 161fa63 docs (`26` §2k session 21, `08` culls, `27` §9).
- **RUNNING: the session-21 job queue** - `bash tc2/out/tools/s21/runq.sh` (background task `ba1sj2iw2`), queue dir `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/4c5113ad-667d-4fde-9df2-13e8ffdf1a90/scratchpad/q` (jobs in `todo`,
  `done`, `log`, `runq.log`; `touch $Q/stop` ends it after the current job). At 22:10 29 jobs left (~55 min; the dry-way binary's tests
  `1_zz_tests_s6` passed), `6_u7_*` (18 seeds left, the branch's final rules, `--stocktally`),
  `7_zz_cmp_c6_r4`, `8_c6_*` (control seeds 9-19), `9_zz_cmp_c6_u7_10`, `9_zz_cmp_c6_u7_20`. Outputs: `tc2/out/civ/s*_{c6,u7,r4}.{txt,csv}`;
  comparisons `$N/cmp_*.txt` (`N` = the queue's scratchpad). **If the scratchpad is gone**: every run is in `tc2/out/civ` - redo a comparison with
  `cd tc2/out/civ && python ../tools/s21/cmp.py c6 u7 4 5 1 6 11 2 3 7 8 42 9 10 12 13 14 15 16 17 18 19`; missing runs: rebuild the branch's headless
  (`tc2/out/tools/s21/q_done/0_x_build_s6.sh` - Core unchanged since d8bf37d) and rerun the jobs in `tc2/out/tools/s21/q_todo` (set `N`/`S` in `lib.sh`).
  Copies of all jobs, logs and results: `tc2/out/tools/s21/{q_todo,q_done,q_log,results}`.
- Saves: `tc2/out/civ/s4_u4/world_y300.tc2`, `s4_u7/world_y300.tc2` (branch rules), `s4_c7/world_y300.tc2` (the control on the branch binary, v80).

### Verified
- **The pigs (session 20's open question) traced**: not grass, mast or hard times - where pigs vanished the land was plentiful, sows fed (cond 1.00,
  fed 0.997-0.999) and bred as often a pig-year as the control's (1.32 vs 1.33); **the households keeping pigs dwindled** (seed 5: 15 -> 8 in ten years,
  control 12) and the one stubble-specific end was **the hands' cull's share of pig ends doubling** (6.1 -> 14.6 %, 7.8 -> 11.8 %, 6.5 -> 14.8 % in
  seeds 5, 7, 11): D2-050's rule culled the kind with the most spares when hands were short - after a farrowing the pigs - so with the stubble's cattle
  filling the hands the young sows went before replacing the breeding ones. Fixed: short hands eased from the kind whose spares take most of them.
- Tests on bin_s3 (before the dry-way change): Livestock 16/16 (new `HandsThatAreShortCullWhatTakesThemNotTheLitter`; the short-pasture test now
  counts pasture and hands culls together), Stubble 3, Muck 3, HouseholdWork 4, Coarse 3, Determinism 6, SaveLoad 8 (`--filter SaveLoad`), Tilth 9,
  Threshing 5, Garden 5, Footprints 8 - **and the same eleven again on bin_s6 (the dry-way code, branch Core = d8bf37d): all pass** (22:10, `1_zz_tests_s6`).
- Controls byte-identical: `--nostubble --onefieldspot --oldspare` = `_n1` on all ten seeds (`_c6`) and on bin_s6 (`_c8`); `--oldhands` = `_u2`;
  `--wetfieldspot` = `_u4`; the diagnostics change nothing (`_u3q` = `_u2`, `_c3q` = `_n1`); yearly reports change nothing.
- **Ten seeds `_u4` (stubble + new culls) vs `_n1`**: people at par (x1.025 end, x1.020 years 200-299), births/deaths a person-year at par, hunger
  x1.10 (t 0.91), cattle x1.94 (t 5.7), goats x1.12, **pigs x0.80 (t -1.28) where kept; lost only in seed 5 (seed 1 has none in any arm)** (u2: lost in
  1, 5, 7, 11); foundings not fewer but earlier (by century 32/46/38 against 21/62/42; the run's total x0.94, t -1.4). `_r4` (the new culls without the
  stubble): pigs x0.83 (n.s.; seed 7 lost them), cattle x0.91, goats x1.18, people 200-299 x0.943 (t -1.95), sickness x2.6 (t 2.4) - noisy.
- Unity C# compiles (`unity.mjs cc`, 180ad85, in the main checkout detached at the branch tip). Real shots `tc2/out/shots/s21a`-`s21t` judged
  (`27` §9): herds on the stubble, household field work (busiest spot 2-13 within 60 m, was 126), a carter in the sea (fixed: field work in the water
  2-4 person-hours, was 55-68), the hunters' crowd (real: a village outgrown its land; the control has it too), the woodlot framing works, muck heaps.
- **Open, older than C3.7d**: 0.10-0.14 % of person-hours under the drawn water in the control world (0.22-0.28 % in the stubble world): woodcutters'
  straight walks across water to their woodlot, the water-drawing spot (`WellX/WellY` +-2 m, no `Ashore`) under a lake's edge, crofts into the water,
  the hungry walking home across a bay (`wet --doy D` lists them by village; `results/wet_*.txt`).

### Next, in order
1. Read the queue's results: `cmp_c6_u7_20.txt` (20 seeds: people, pig/cattle/goat-years `*_years`, extinctions per arm,
   foundings, sickness), `cmp_c6_r4.txt`. Merge when convincing (people at par, pig-years within noise, extinctions like the control's); if pig-years
   fall significantly, look first at hard times on mast (kills the most grown - the breeding cores - down to a village's nucleus) and the hunger cull
   (keeps "a pair" of the eldest, eats the young sows).
2. Merge: `git merge --no-ff wip/c37d-stubble` on main (message `[tc2] Merge the stubble branch: C3.7d ...`); DECISIONS **D2-155** from
   `tc2/out/tools/s21/d2155.md` (fill "Evidence"); PROGRESS; `25` (C3.7d done). Then the full suite (`node tc2/scripts/headless.mjs test`, ~17 min)
   and the Web build and test (`node tc2/scripts/unity.mjs build-web --jobs 2`, then `node tc2/scripts/webtest.mjs --tag s21web1 --export`; saves v80).
3. **Nobody walks through the water** (D2-123's rule for the ways, a new decision with its switch): woodlots reachable dry-shod (`Woods.Choose` with
   `FieldSpots.DryWay` from the square), the water-drawing spot ashore (`AgentSystem.BeginWater` + `Ashore`), crofts on dry ground (`Gardens.Strips`),
   wild-food spots reachable dry-shod; `wet --doy` before/after; ten seeds.
4. The next civilisation slice by evidence: **governance** - seed 4 at year 300 has 30 villages, 10,254 people and ONE lordship of 2 villages (6 raids
   in 300 years; lordship only by conquest, D2-075): measure polities over ten seeds, then lordship beyond conquest (daughter villages' kin allegiance,
   protection, marriage between first households - `10` §4). Or **holdings on the ground** (strips a household holds; needs a Core-defined strip layout
   the terrain shader follows - its strips are a GPU float hash now).

### Rules that bit this session
- A Unity coroutine holds no `ref` locals (CS8176 in `ShotRunner`) - caught only by `unity.mjs cc`, which runs in the **main checkout** (Unity's csproj
  and Library): queue jobs detach the main checkout at the branch tip and return; they refuse to run with untracked files - commit the editor's
  `.meta` files of new branch files to the branch.
- The claude-mem plugin (v13 hooks calling a v12 CLI: "Unknown command: hook") was uninstalled with `--keep-data`; its hooks may fire until Claude Code restarts.

## Session 20 (13 Oct 2026, 17:25-~21:00 IST; Control Room run #55 session 2) - stopped by Arjun; nothing running

### Exact state
- **main** (after the handoff commit): D2-154 the burial ground kept and graves drawn as graves; W-FELL view faults fixed; **C3.7c the muck merged**
  (d601c24, sim version 79, D2-153 - ten seeds `_n1` at par); the threshing floor in use drawn; shot views `floorbusy`, `floorbusylow`,
  `act:<Activity>[:low]`; the shots log stumps placed and drawn. `.claude/launch.json` and `Assets/Resources.meta` are not ours - leave them.
- **Branch `wip/c37d-stubble`** (worktree `C:/Game Dev/tc-stubble`, sim version 80; NOT merged): C3.7d the herds on the stubble and the fallow
  (`Core/Places/Stubble.cs`, `Livestock.FieldOffer`/`GrazeFallow`/`GroundOf`), each household on its own field cell (`Core/Places/FieldSpots.cs`),
  a food's cull by what the spares eat (`Livestock.Spare`, `--oldspare`); switches `--nostubble --onefieldspot --oldspare` (together = `_n1`
  byte-identical); `26` §2k; draft D2-155 in `tc2/out/tools/s20/d2155.md`.
- Other worktrees: `tc-merge20` (branch `wip/s20-merge`, fast-forwarded into main - may be removed), `tc-muck` (merged), `tc-thresh` (merged),
  `tc-g2`, `tc-g4`, `tc-wild` (earlier sessions') - leave them unless cleaning up deliberately.
- Saves: `tc2/out/civ/s4_m1` (v78), `s4_n0` (v78, graves kept), `s4_n1` (v79, main), `s4_u2` (v80, the stubble branch).

### Verified
- On merged main: Muck 3, HouseholdWork 4, Burials 4, Tilth 9, Livestock 14, Coarse 3, Determinism 6, SaveLoad 8, Threshing 5, Carts 6, Footprints 8,
  Garden 5, Homes 5; no float arithmetic; `unity.mjs cc` (633466f). Controls byte-identical: `--oldgraves` = `_m1`, `--nomuck --oldgraves` = `_m1`.
  Ten seeds `_n1` (muck + graves kept) against `_m1`: at par (`tc2/out/tools/s20/cmp_m1_n1.txt`).
- On the stubble branch: Stubble 3, Livestock 15 (new: a short pasture culls what eats it), Muck 3 and the seven neighbours; control
  `--nostubble --onefieldspot --oldspare` = `_n1`. Ten seeds `_u2` (`cmp_n1_u2.txt`): people at par, fields x1.017, cattle x1.84, goats x1.38 -
  **pigs gone from 3 of 10 worlds** (seeds 5, 11, 7), foundings x0.77 (t -2.4).

### Next, in order
1. **C3.7d's pigs.** Why do pigs die out where cattle grow? Two leads from the reports' end causes:
   - The hands cull (mode 0) still takes the kind with the most spares - a sow's litter.
   - Mast culls (cause 9).
   Trace with `--places` (village lines `stock cattle/goats/pigs` every 10 years; end causes by kind) on seed 5 or 7 `_u2` against `_n1`. Find the
   mechanism, fix it with its own switch, ten seeds again, then merge `wip/c37d-stubble` (the merge's Unity cc; then shots: `herd` views on a
   stubble day ~42, `act:Muck` spread over the fields, `close` at day ~30 for the muck heaps). Then write D2-155 from `tc2/out/tools/s20/d2155.md`.
2. The full suite on the final HEAD (`node tc2/scripts/headless.mjs test`, ~17 min; through the queue) and the Web build and test
   (`webgate.sh`, one Unity build alone).
3. Shot `107c100`'s woodlot framing (`woodlot,woodlotlow` on `s4_n1/world_y300.tc2`; the Unity C# compiles on 4dd9b94 - `unity.mjs cc` at the handoff).
4. Then the next civilisation slice (`27` §6 gaps: holdings by household not drawn; no held tools; crop variety not simulated).

### How the queue works (session 20)
Start `bash tc2/out/tools/s20/runq.sh` in the background with the scratchpad `Q` and `S19` paths set to this session's (it waits for session 19's
q21 to end: delete that line). Jobs are `P_name.sh` files in `$Q/todo` that source `lib.sh` (paths `S`, `N`, `R`, `O`, `DN`, `BL`, `gate`, `low`,
`world seed bin tag [switches]`); `touch $Q/stop` ends it after the current job. Copies of every job, log and comparison:
`tc2/out/tools/s20/{q_todo,q_done,q_log}`. Fix `N`/`S` in `lib.sh` to the new scratchpad and copy `bin_s0` (stubble) / `bin_n1` (main) builds
again (`0_p_stubblebuild.sh`, `0_i_mergebuild.sh` show how).

## Session 19 (13 Oct 2026, from 14:53 IST; one low-priority job at a time, RAM < 86 %, CPU < 85 %) - context handoff at ~17:10 IST

### Exact state
- **main 4caf9ee** (clean but for `.claude/launch.json`, `Assets/Resources.meta` - not ours): b312f01 **D2-148 W-FOREST pass 2b** (rain belt follows
  the sun, `--oldrain`; the seasonal tropics drawn from the soil's water; conifers tinted - `ProcMeshes.Builder.Dim`); 14a9156 merge of
  `wip/c37b-thresh` (**C3.7b the threshing floor** D2-149, **W-FELL** felling record D2-150 Core; sim version 77); 4caf9ee stumps drawn (D2-150),
  **D2-151 the burial ground** (`Core/Places/Burials.cs`, sim version 78), **D2-152 the first household's standard** in its polity's colour; kit
  `rick`, `grave`, `grave_old`, `standard` exported (their `.meta` files and those of Threshing.cs, Burials.cs, the new tests appear on Unity's next
  import - commit them).
- **Verified**: the full suite on 86b481d 372/372; pass 2b control/seasons/ten seeds (`_r1`); threshing tests and neighbours; after the merge
  Threshing 5, Felling 3, Burials 3, Sheaves 5, Determinism 6, SaveLoad 8, Coarse 3, HouseholdWork 4, Climate 7, lint, Unity C# compiles; the merge's
  control `--nothresh --oldrain` seed 4 = `_g4` byte-identical.
- **Running at the handoff (scratchpad `f50753be-.../scratchpad`, copies in `tc2/out/tools/s19/`)**: q19 - ten seeds `_m1` (bin_m, sim version 78)
  then `cmp_r1_m1.txt` (threshing's effect against pass 2b; graves and felling are records); q20 (waits for q19) - real shots `s19e20` (floor,
  woodlot, graves, standard, village views on `s4_m1/world_y300.tc2`), `s19f20` (`--village town`), `s19g4`/`s19g28` (the four climates after the
  last view tweaks: inland stands inside the woods, slighter grey dry-season thorn); q21 (waits for q20) - **C3.7c the muck** in worktree
  `C:/Game Dev/tc-muck` (branch `wip/c37c-muck`, c5429fd, sim version 79, NOT yet built): build, MuckTests and neighbours (Tilth, Livestock, ...),
  control `--nomuck` seed 4 = `_m1`, seed 4 `_k0` with `--mucktally`.
- DECISIONS D2-148 (final), D2-149/D2-150/D2-151 (evidence but for ten seeds and shots), D2-152 (shots pending) written; D2-153 (the muck) not yet.

### Next, in order
1. Read q19's `cmp_r1_m1.txt` (threshing at par? if hunger or famines move, find why); add it to D2-149.
2. Look at q20's shots (`tc2/out/shots/s19e20`, `s19f20`, `s19g4`, `s19g28`): ricks round the floor, threshers' flails, stumps in the woodlot,
   graves (fresh and old), standards; fix what reads wrong; record in D2-149..D2-152 and `27` §6/§8.
3. q21's muck results: fix the build/tests; control; then design text `26` §2j and D2-153; merge `wip/c37c-muck`; Blender `--only muck_heap`; ten
   seeds `_k1` against `_m1`; shots of heaps and carting (`--days 42` after the harvest, `--days 4` the sowing).
4. The full suite on the final HEAD; the Web build and test (sim version 79: new worlds); then the remaining `27` items (crop variety is not
   simulated - a real C-slice), SESSION_19_REPORT.md.

## Session 18 (11 Oct 2026, 06:48-~16:00 IST; Control Room run #48 session 3, resumed by Arjun; stopped by Arjun) - D2-144 to D2-147; Web 6/6

### Exact state at the stop
- **main**: e23e5b7 (**D2-144** the gardens keep their village's own calendar), d224365 (**D2-145** a croft counted once, one wattle fence, beds by
  season), 29f56df (docs), c7edc68 (**D2-146** a COARSE day's garden by the lived urges; **D2-147** FreeLot bounded when the sea took every lot; `run
  --trace`), and the stop's commit (DECISIONS D2-146/D2-147 with evidence, PROGRESS, this block, the prompts, `SESSION_18_REPORT.md`). Nothing uncommitted
  of ours (`.claude/launch.json` and `Assets/Resources.meta` are not ours).
- **Verified on c7edc68**: Footprints 8 (new: the drowned village), Migration 9, WildFood 11, Garden 5, HouseholdWork 4, Coarse 3, Determinism 6; lint;
  Unity C# (`unity.mjs cc`); controls `--oldgardenorder` = `_g3` byte-identical; ten seeds `_g4` at par (seeds 4, 5, 1 byte-identical before and after
  D2-147); **the Web build (one job, 1 h 13 min) and the Web test `s18web1` 6/6** (WebGPU, browser = headless bit-identical, reload continues, a day
  away catches up ~96 years: 531 people in 17 places, no console errors). The 900-year check passed (seeds 4 and 42, `_g3`).
- **The full suite on c7edc68 was running at the stop** (`scratchpad/suite_f.txt`, ~85 % through with no failure when the handoff was written; see
  SESSION_18_REPORT.md for whether it finished). The previous full run (d224365) was 369/371 - both failures fixed by D2-146 and passing on their own.
- **W-FOREST pass 2b prepared, NOT applied**: `tc2/out/tools/s18/pass2b.diff` (`git apply` it: the rain belt follows the sun in the outer tropics,
  `--oldrain`; the dry woodland's leaves fall with its soil's water; `place` applies the rule switches before loading), the queue that validates it
  `tc2/out/tools/s18/q10.sh` (build, planet/weather tests, control `--oldrain` = `_g4`, `place --seasons` old/new on seed 42, ten seeds `_r1`, cc, shots
  of four climates on days 20 and 44), the decision draft `tc2/out/tools/s18/d2148.md`.
- **Resource rules (Arjun)**: D-1 - one low-priority job at a time (`cmd //c start "" //belownormal //wait //b ...`); D-2 - run while CPU < 85 % and RAM
  < 86 % (`q*.sh`'s `gate`/`low`). The machine's other applications (a Chrome tab drawing WebGL in software, other sessions) held it at 60-100 % CPU
  for hours: jobs waited at the gate and ran slowly (a test class 13 min instead of 24 s). The Web build with `--jobs 1` takes ~75 min.
- Tools in `tc2/out/tools/s18/` (copy into the new scratchpad and fix the `S=` path): `q2-q10.sh` (the gated queues), `l9.sh`, `cmp.py`, `rates.py`,
  `sheet.py`, `shots.sh`, `p_drytropics*.py`, `pass2b.diff`, `d2144-d2148.md`. Runs: `tc2/out/civ/s*_g2` (D2-144), `_g3` (D2-145), `_g4` (D2-146/147),
  `s4_l9g3`, `s42_l9g3` (900 years). Shots: `tc2/out/shots/s18a-d`, `s18_gardens_sheet.png`.

### Next, in order
1. If the stop's full suite did not finish: run it once on HEAD (`node tc2/scripts/headless.mjs test`, one job; ~17 min on a quiet machine).
2. **W-FOREST pass 2b**: `git apply tc2/out/tools/s18/pass2b.diff`, then `q10.sh`'s steps; write D2-148 from `d2148.md`; keep iterating on the shots
   until the four climates are not interchangeable (Arjun judges W-FOREST).
3. C3.7b the threshing floor (the harvest held as sheaves until threshed - `SettlementSystem.Sheaves` puts the grain straight in the store today;
   ~0.3 h an adult-day through the off-season, at the granary or a barn) / C3.7c the muck (`26` §2h); the visual-truth items (`27` §6: headmen, famine,
   stumps - the woods keep no record of felling, so stumps need derived or new state; the beds' crops by kind).

## Session 17, third context (10 Oct 2026, 12:31-12:45 IST; Control Room run #48 session 3; Resource Governor HIGH, the machine at 94-97 % RAM from a QEMU VM and other applications) - stopped by Arjun

### Exact state at the stop - nothing running
- **main HEAD**: a8a45a9 + one WIP commit (this block): **uncompiled** view work - run `node tc2/scripts/unity.mjs cc` first.
  - `Gardens.Shown(in hh, doy, cropGrowth, out dug, out cleared)` (read only): the share of the croft dug this year and the share gathered and cleared;
    true from the growing on (the dug beds in leaf).
  - `WorldObjectsView.GardenBeds`: the beds drawn by season - dug in the gardener's order (middle column behind first, from the back wall out): fresh
    earth (`garden_bare` tinted `BedDug`) in the sowing and once gathered, in leaf (`garden`) from the growing on, a weedy rough (`BedRough`) where
    nobody dug this year; **huts' crofts drawn too** (they were worked in the simulation but drawn bare). `_gardensBare` instancer.
  - Kit: `garden_bare` in `tc2/art/kit.py` (beds, ridges and clods tintable; built with `--only garden_bare`; `garden_bare.fbx` committed, its `.meta`
    is made by Unity's import - commit it after the first editor run).
- **The full suite was stopped mid-run** (started 12:33 on a8a45a9; no result) - run it once on the next HEAD.
- **Found, NOT fixed - a real D2-143 bug**: the garden uses the unshifted day of the year (`AgentSystem.cs` ~577 `Gardens.Work`, ~1163 `Behindhand`,
  ~1989 the COARSE step's `gDoy`), but southern villages keep their crop calendar 24 days on (`if (s.Anchor.Y < 0) doy = (doy + 24) % 48`,
  SettlementSystem/Holdings) and `Gardens.Year` runs on that calendar - so southern gardens are dug, weeded and gathered on the wrong days (little
  borne). Fix: one helper for the village's own day of the year used by all three calls and by the view's `GardenBeds`; a test with a southern
  village; ten seeds against the HEAD binary (the change moves the worlds; sim version stays 76 - no format change).
- Tools: this context's copies in its scratchpad (`1a60904a-...`): `q1.sh` (suite, keep bin_h, `unity.mjs cc`, then shots `s18a` with
  `SHOTX="--days 8"` - the sowing, beds being dug - and `s18b` `--village town --days 20` - in leaf). **A year-300 save opens on the year's first
  day: nothing is dug yet** - shoot gardens with `--days 6-10` (digging) or `--days 14-28` (in leaf, weeding).

### Next, in order
1. `unity.mjs cc`; fix the southern garden calendar (above) with a test; the full suite once; ten seeds (`q8.sh` pattern) against `_g1`.
2. Real shots of the gardens (`--days 8` and `--days 20`; read "at work or on the move"); fix what they show; commit (DECISIONS D2-144).
3. Then as the second context's list below: the 900-year check, the Web build and test, W-FOREST pass 2b (note: pass 2b's wet season is by the
   hemisphere's summer - check the southern crop calendar's growing stage falls in it).

## Session 17, second context (10 Oct 2026, 06:46-12:35 IST; Control Room run #48 session 2; Resource Governor LOW, HIGH from ~12:00) - queue 3 closed, the Unity side compiled and shot, D2-141, D2-142 the woods, D2-143 the garden

### Exact state at the handoff (12:35 IST) - nothing running
- **main HEAD 93ad626** (clean but for `.claude/launch.json` and `Assets/Resources.meta`, not ours): a9571e0 (queue 3 closed, Unity compiled and shot,
  `Conflict.PastWorks`, `ShotRunner` waits for a loaded world's lived day and takes `--village town`, `unity.mjs cc`, `timeuse --daylight --stages`,
  `place --sheds`, `test` takes the rule switches), 6289fba (**D2-142** trees by the growing season's warmth, `--oldwoods`; DECISIONS D2-141/D2-142;
  probes `place --crofts/--housing/--seasons`), 1d5f37b + merge 93ad626 (**D2-143 C3.7a the household's garden**, sim version 76, `--nogarden`).
- **Verified on 93ad626**: the headless build, GardenTests 4/4, DefenceTests 8/8, `node tc2/scripts/unity.mjs cc` (Unity C# compiles). On the
  garden branch's identical code: HouseholdWork 4, Determinism 6, Coarse 3, CatchUpPlan 2, DeepTime 8; ten seeds `_g1` at par; control `--nogarden` =
  `_w1`. **The full suite last ran on the woods' source (366/366, before the garden) - run it once on 93ad626.** **No real shots of the gardens yet**
  (the view's beds on the croft are compiled, not seen). **The save format is v76**: v75 saves (all earlier runs) load only in an older binary.
- **Prepared, not applied**: W-FOREST pass 2b (`tc2/out/tools/s17b/p_drytropics.py` - the rain belt follows the sun in the outer tropics,
  `--oldrain`; `p_drytropics_view.py` - the dry woodland's leaf fall from the soil's own water). `place --seasons` (seed 42 `_w1`): the dry tropics'
  soil water sits at 0.15-0.25 in every season - no dry season in the state.
- Runs (`tc2/out/civ`): `_v4` (D2-141 final), `_v5b` (the ring rule alone), `_w0`/`_w1` (D2-142), `_g0`/`_g1`/`_g3` (D2-143; `_g1` saves at 100/200/300 are
  v76). Shots: `s17a` (a loaded save before its lived day), `s17b`/`s17c` (a lived morning: the town, the first village), `s17d` (forests after D2-142).
- **Tools copied to `tc2/out/tools/s17b/`** (copy them into the new scratchpad and set `S=`/`SCR=`): `bld.sh` (main headless; BIN, TESTS, GATE),
  `gwrun.sh` (seed runs; GATE, EXTRA), `cmp.py A B seeds`, `rates.py TAG seeds`, `decades.py A B` (people by 20-year spans), `shots.sh TAG SAVE VIEWS
  [HOUR]` (`SHOTX="--village town"`), `sheet.py` (contact sheets), `l9.sh` (the 900-year check: `BIN= TAG= bash l9.sh SEED`), `webgate.sh TAG`,
  `bench.sh`, `pt.py`, `v75.py`, `mkcc.py`, `q8.sh` (the pattern: build, control, ten seeds in three lanes), `p_drytropics*.py`, `d214x.md`.
- **Resource Governor HIGH since ~12:00**: CPU 90 %, RAM 92 %, up to three heavy jobs, never two builds or suites at once; the machine's other
  applications hold RAM at 65-96 %: gate heavy jobs on RAM < 88 %; seeds may run in three lanes (`q8.sh`).
- **Rules that bit this context**: a background `cd X && bash q.sh > log` task, when stopped, leaves the script running (and a stale copy
  re-reads an edited script) - start queues with `exec bash q.sh > log` and kill a chain's parent to detach its later steps; a local named like an
  enclosing one (`sid`, `gh`) fails the headless build (CS0136); write patch scripts with the Write tool (heredocs mangle quotes); a v76 binary
  cannot read v75 saves; wall time is not cost on this machine (compare the reports' `events` counts, or time back to back).

### Findings of this context
- D2-141 (DECISIONS): ten seeds `_v4` vs `_n1` at par in rates; the street clearance makes a people wave (x1.02-1.07 over years 140-199, x0.95
  over 280-299), attributed by elimination (`_v5b`), mechanism not found (`--housing`: no fewer households camping); boats lost x1.68 was `_n1`'s luck.
- D2-142: people x1.10, per-person rates at par, events x1.017; boreal woods +50 %; the boreal and mixed shot views now find woods; the dry tropics
  and the wet woods still read alike (no dry season in the state).
- D2-143: gardens real - 83-86 % of households at year 300 (mean ~130 m2), a lived year 0.3 of 12 daylight hours in them, nearly all from idling,
  under 1 % of the food; ten seeds at par. Adults idle 3.9-4.6 of 12 daylight hours over a lived year (the year's first days up to 7).
- Real shots (`27` §8): a lived morning shows 2,155 of 3,001 people drawn busy; poses, ways along streets, gardens and pens read.

### Next, in order
1. The full suite once on 93ad626; then **real shots of the gardens** on a lived morning (`shots.sh` with a v76 save, e.g. `s4_g1/world_y300.tc2`;
   town views with `SHOTX="--village town"`; read the log's "at work or on the move") - beds on the crofts, people hoeing behind their houses.
2. **The 900-year check** (`l9.sh` seeds 4 and 42 on the final binary, `TAG=g1`) and **the Web build and test** (`webgate.sh s17web2`; the Web game
   starts new worlds - v76).
3. **W-FOREST pass 2b**: apply both patches, `--oldrain` = the final binary (byte-identical), ten seeds, `--seasons` before and after, shots of the
   dry tropics in the wet and the dry season and of four climates side by side.
4. Then C3.7b the threshing floor and C3.7c the muck (`26` §2h), and the remaining visual-truth items (`27` §6-§8).

## Session 17 (10 Oct 2026, from 05:04 IST; Control Room, Resource Governor LOW: CPU 50 %, RAM 75 %, one heavy job) - the soil and the niche closed (D2-139, D2-140); the visual truth audit (`27_VISUAL_TRUTH.md`)

Arjun's prompt (10 Oct): finish the soil, test the niche, then a **real-game audit** - people standing still, houses on roads, can the world be read
without a panel - then **W-FOREST pass 2**, then the roadmap; presentation and simulation advance together from now on.

### Exact state (keep current)
- **Code (uncommitted at the time of writing - commit first if a context ends)**: HEAD f09da10 + the compile fixes (SettlementSystem `bring`,
  Commands `nsb`), the niche on by default (`SettlementSystem.NicheOn = true`, `--noniche`; sim version 75), `Core/Agents/Presence.cs`,
  `Core/Agents/WayLines.cs`, `Footprints.OpenScale/SquareEarth/DiscFree/DrawnLine`, `Footprints.WorkClearOff` (`--noworkclear`),
  `Conflict.WallWorksOff` (`--nowallworks`), the view (WorldObjectsView people via Presence, gates, `Instance`/`YardPen`; LivestockView pens;
  TerrainLod roads via WayLines, no road over a square, streets from the square's edge, lanes from 26 m, blocked streets, `_TC2Year`; Hud picking
  and follow via Presence; CameraRig `townlow`/`townstreet`; ShotRunner logs busy people), shaders (Instanced: `TC2Pose`, sixteen poses, shadow
  pass posed; Terrain: fallow at its true share, rotating), headless tools (`place --still`, `place --roads [--show K] [--feature F] [--works]
  [--rawways]`, `place --woods` by kind of wood and by summer warmth), HouseholdFormationTests fixed. **The Unity side has not been compiled.**
- **Runs** (`tc2/out/civ`): `s7/s8/s42_s2` (the soil's last seeds), `s4/s42_n0` (= `_s2`), `s*_n1` (the niche, ten seeds), `s5_v1` (= `_s2`: the
  view code changes nothing); queue 3 (`scratchpad/q3.sh`, log `q3.log`): controls `_c0` (`--noniche --nowallworks --noworkclear` must = `_s2`),
  `_c1` (`--nowallworks --noworkclear` must = `_n1`), ten seeds `_v3` (the final binary), `cmp_n1_v3.txt`, then the full suite (`suite_v3.txt`).
- Scratchpad: `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/9bd81d71-36cd-4497-a09b-0b3d9ad0e072/scratchpad` (`bld.sh`
  BIN/TESTS/GATE builds and tests; `gwrun.sh`; `q1-3.sh`; `v75.py` sets a save's sim version; `sv/` v75 copies of the `_s2` saves for the audits;
  `rates.py TAG seeds`; `p_*.py` patches).

**State at the first context's end (06:35 IST)**: committed b5beba2 (WIP) and a second commit with the view-only W-FOREST pass-2 work (the woods'
floor by kind of wood, the dry tropics' dry season, rain forest closure, boreal spires, ForestTypes' treeline by growing-season warmth tMean +
0.6 TAmp < 7) and the tools (`place --woods` by kind and by summer). **Queue 3 was STOPPED at the handoff (06:45) after `_c0`: nothing runs; rerun
its remaining steps (`tc2/out/tools/s17/q3.sh`).** It had been (`scratchpad/q3.sh`, log
`q3.log`): its first controls passed (`--noniche --nowallworks --noworkclear` = `_s2` byte-identical, seeds 4 and 42); it was waiting at its RAM gate
(the machine at 88-97 % from a 1.7-2.8 GB QEMU VM that is not ours) before `_c1` (`--nowallworks --noworkclear` must = `_n1`), the ten seeds `_v3`
and the full suite (`suite_v3.txt`; it builds the headless app from the source at that moment). **Not applied yet**: `scratchpad/p_summerwoods.py`
(the ecology change: tree suitability by growing-season warmth SmoothStep(5, 10, TMean + 0.6 TAmp), `--oldwoods` the control; D2-142) - apply it
after the suite's build has begun (or after q3), then measure with `place --woods` and ten seeds against `_v3`.

### Next, in order
1. Queue 3's results: the controls byte-identical, `_v3` against `_n1` at par (only rings and works' clearance differ), the full suite - then
   **commit** (D2-139 closed, D2-140, the audit tools, the view and shader changes, the docs).
2. **The Unity compile and real shots** (one heavy job, when RAM allows): `node tc2/scripts/unity.mjs shots --tag s17a --load <a v75 save>
   --hour 10 --views townlow,townstreet,low,close,village` - people at work, ways along streets, pens, gates, fallow; fix what the shots show.
3. **W-FOREST pass 2**: the simulation's cool woods are thin because tree suitability ramps on the annual mean (`PlanetSystems.GrowVegetation`:
   SmoothStep(-1, 8, TMean)); measure with `place --woods` (summer = TMean + TAmp) and consider suitability by summer warmth (a simulation change,
   ten seeds); then the presentation: ground cover by kind of wood, rain forest closure and layers, the dry tropics' dry season, edges.
4. The soil's 900-year check (seeds 4, 42), the Web build and Web test.
5. B of the stillness audit: adults idle 21-24 % of daylight - `timeuse` by age; the scheduler's work (service, own holdings, repairs) vs leisure.

## Session 16, fourth context (9 Oct 2026, from 21:33 IST; Control Room run #39 session 4, Resource Governor MEDIUM) - the Web catch-up's out-of-memory traced and fixed (D2-137); a headman gone to live with their child (D2-138)

**The blocker of the third context is resolved (D2-137).** The previous context compared the browser's catch-up with headless `run --coarse` and
read the difference as the browser diverging. Wrong premise: `PlanCatchUp` lives a span COARSE only above 40,000 person-years; seed 42's 96 years x
~145 people are ~14,000, so **the Web test's "a day away" is lived in ACTIVITY** and its world is headless `run --seed 42 --years 96` (C3.6e's binary
ran out of memory there too, at year 28 under a 512 MB heap cap - browser = headless). `webtest.mjs --export` (new) reads the game's own saves out of
IndexedDB (before the day away and every half minute of the catch-up) into the tag's folder; `run --load SAVE --hot N` (new) names who began the most
activities between reports. Cause: two parties founded villages on one cell in year 27 (Ustwick, Skolmere: a party "founds all the same" where
another party's village now stands); the way between them had no length; visitors' arrival was read from the arc length, so a trip home ended where
it began, ~17,000 times a day; each leg went into the travel index, pruned only by queries. Fixed (sim version 72, 741565c): a founding party that finds
a village within `Sites.Spacing` of its place joins the nearest (`PartyJoined` 95); on a way of a metre or less a traveller arrives at their
destination (both arrival sites, land and sea); `TravelIndex.Sweep` before the pool grows. Ten seeds DEEP 300 years `_j1`: annals byte-identical to
`_e1` (the case never arose in DEEP). Full suite 358/358 (C3.6e's first full run too). The browser's year-34 save, its header set to 72, replays
cleanly with the fix (`scratchpad/web_y34_v72.tc2`).

**Then D2-138** (`place --heads`, new): C3.6e's retirements made the first household's headman live in another household 37-127 times a world;
`Leaders.Day` read it as a vacancy (contested; 4 of 20 splits came from it) and recorded "another household rose" for the same man. Now (sim version
73): a living headman in another household of the village is weighed again without a contest, the household that absorbed the first household is the
incumbent, the same person stays first with nothing recorded; and a household's clients (`Patron`) follow the household that took it in
(`Holdings.Clients` from `Close`/`Inherit`) - a gap since C10.2. `--nostanding` keeps the old reading. LeadersTests 8 (one new).

### Exact state (keep current)
- HEAD: 741565c (D2-137), 4bf4888 (D2-138, sim version 73) + 4b6572f (Web result) + this handoff. Web build of 4bf4888 in `tc2/web-build/web`; **Web test
  `s17web1` 6/6** (the day away to year 96 in 106 s, 487 people in 16 places = headless ACTIVITY's 488 in 16).
- Runs (`tc2/out/civ`): `s*_j1` (D2-137, = `_e1`), `s*_k1` (D2-138 first form, clients and no contest), `s*_k2` (D2-138 final, the absorbing household
  the incumbent). Browser exports: `tc2/out/webtest/s17exp/` (away_start.tc2, catchup_y30/y34.tc2, sim version 71).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/6dc4a248-f0c6-410d-a245-27452f45cb7d/scratchpad`
  (`wrun.sh` pointed here; `j1batch.sh`, `k2batch.sh`, `k1wait.sh` - waits for RAM under 84 % then runs ten seeds one batch at a time; `p_*.py` patches;
  `d2137.md`, `d2138.md`). **Resource Governor LOW since 23:10 (CPU 50 %, RAM 75 %, one heavy job)**; the machine's RAM sits at 80-90 % from other
  applications (Chrome, WebView, other Claude sessions): `gate.sh CMD` runs a command once RAM is under 72 %; `webgate.sh TAG` (build + Web test).

**SESSION ENDED 10 Oct 2026 ~04:00 IST at Arjun's request** (Resource Governor LOW: CPU 50 %, RAM 75 %, one heavy job). From 00:48 to the end the
machine's RAM stayed at 74-90 % from other applications, so the gated queue below never started: **the last three `_s2` seeds (7, 8, 42), the full
suite on the soil and the niche runs were NOT run**; the queue was stopped. The niche's two new tests (`HouseholdFormationTests`) and a one-line
fix in `Holdings.PortionFor` (a household's last member takes all it holds) are committed but **not compiled or run** - build and run them first.

### Later in this context (10 Oct, 00:00-01:40 IST) - the 900-year check, the soil (C3.6f, D2-139, committed as work in progress), the niche measurement
- **900 years with C3.6e, seed 4** (`s4_l9k.csv`; PROGRESS): hunger 8.7-11.2 a 1,000 person-years in centuries 300-600 (sim 70: 1.0-5.1), births
  40-42 (29-36): the old's grip on the land had been a preventive check; a plague at 746-753 (~9,000 dead) then post-plague plenty. Seed 42 not run.
- **The soil, C3.6f (D2-139 in DECISIONS with seven seeds; sim version 74; committed as work in progress - the last three seeds and the full suite pending)**: `Settlement.Tired/TiredSeen/
  Rest/Grazed/RestEvent`; `SettlementSystem.Heart/SoilYear/HaPerHead/Rested` + constants; `Livestock.Day` tallies grazing; history `FieldsRested`
  (96); Gazetteer soil line; `--nosoil/--norest/--nodung`; land tally soil fields; TilthTests +5 (9 pass). Runs: `s*_s1` (first form), `s*_s2`
  (the land judged by what it bears; 7 of 10 seeds done at handoff time, 7/8/42 queued), `s5/s42_s1n`, `s5_s2n` (= `_k2` byte-identical with
  `--nosoil`). **Finding**: worlds x0.55-0.7 people (as designed), bound villages rest 13-17 %, a sown ha 0.73-0.79 of a fresh crop - **but births do
  not answer** (42 a 1,000 before and after): hunger a person-year x1.9 (4.7 -> 9.3), wounds x2 - the marriage rule (C3.5) does not bind (couples
  bring two years of bread: 83 of 8,635 matches ever waited).
- **The niche (C3.5b), measurement code in the working tree (off by default)**: `SettlementSystem.CoupleLiving` (land's half by the portions the
  couple would hold + labour's half by their hands, at the village's last harvest, against their year's need), `NicheOn`/`NicheYears` (`--niche`,
  `--nicheyears N`: marry when the living keeps them or their grain carries the shortfall N years), `NicheTally` (`--nichetally`), `Holdings.HandOf`,
  `Holdings.PortionFor`.
- **Was queued, never ran, stopped at the session's end** (copies in `tc2/out/tools/`): `q_s2.sh` (the last 3 `_s2` seeds) -> `q_suite.sh` (the full
  suite) -> `q_n1.sh` (`_n0`: seeds 4, 42 with `--nichetally`, must equal `_s2`; `_n1`: ten seeds `--niche`). Expected times when memory allows:
  ~5 min, ~15 min, ~20-22 min (one job at a time; gate each on RAM under ~66 % with `gwrun.sh`, which waits per seed).

### Next, in order
1. Build the headless host and run `HouseholdFormationTests` (the two niche tests are uncompiled), then the three missing `_s2` seeds
   (`BIN=<build> TAG=_s2 SNAP=100,200,300 EXTRA="--gaptally --landtally --landholds" bash gwrun.sh 7 8 42` - the binary must have the niche OFF: it
   is off by default), the full suite once, then `_n0`/`_n1` as in `q_n1.sh`; compare `python cmp.py s2 n1 <ten seeds>` and the absolute rates (years
   200-299: hunger, births, deaths a 1,000 person-years; `_k2` 4.7/42.2/32.8, `_s2` 9.3/42.3/38.1 on seven seeds).
2. **Close D2-139 (the soil)**: its final ten-seed numbers into DECISIONS and PROGRESS, the full suite's result; commit.
3. **C3.5b the niche** (`--niche`): if births answer the land (hunger a person-year back toward `_k2`'s, people at par or fewer), make it the rule
   (D2-140, `--noniche` the control), tests (a couple with a living marries; a landless couple waits unless its grain carries the shortfall), docs.
4. Then the soil's 900-year check (seeds 4, 42), the Web build and Web test, the soil's look in real shots (fallow plots: the view draws a village's
   plots by the share sown), and the remaining candidates (herds on the stubble and fallow, a land market, partible customs C11).
5. Earlier list (third context, 6-8): work for grain, alms by standing, households working their own holdings, debts' consequences.

## Session 16, third context (9 Oct 2026, from 16:10 IST; Control Room run #39 session 3, Resource Governor MEDIUM) - C3.6a working the land harder (D2-131), the first sheaves (D2-132), household land C3.6b-d (D2-133..135)

**OPEN BLOCKER FIRST (end of this context, 21:30 IST)**: HEAD 5dd947a (C3.6e retirement, sim version 71) + this handoff commit. **The Web build of
5dd947a FAILS the Web test's catch-up, deterministically** (`tc2/out/webtest/s16web9` and `s16web9b`, identical): checks 1-4 pass (bit-identical
probe), then "a day away" stalls - year 35, 243 people in 17 places, 620 s - and the browser aborts `OOM` (`abortOnCannotGrowMemory`) then
`memory access out of bounds` (console.txt). The previous build (26127b5, C3.6d) passed the same test (`s16web8`: year 96, 518 people, 14 places).
Headless is healthy: ten seeds DEEP 300 years (`_e1`), six worlds COARSE 100 years (`scratchpad/cscan_*.txt`, 4-5 years/s), the managed heap at
par with `--noretire`. The browser's path: new world (00:38), lived to 12:21 of day 1, saved, reloaded, then `OfflineProgress.PlanCatchUp` (Living:
COARSE from the next midnight, ACTIVITY for the last two days) to year 96 - the same as `run --coarse` in principle, but the browser's year-35 state
(17 places) differs from headless COARSE (9 places): find why first (reproduce the browser's catch-up headless exactly - the SimHost loop's
catch-up step, budgets, saves during the catch-up - or build 5dd947a with `Retire` switched off to confirm C3.6e is the cause). The full suite has
NOT been run on 5dd947a (LandTests 10, Holdings 12, Migration 6, Biography 4 pass). Do not build on C3.6e until this is resolved; revert it if the
cause cannot be found. Targeted tests on HEAD 7abbaa5 at handoff (21:35 IST): `LandTests` 10, `SheavesTests` 5, `TilthTests` 4,
`DeepTimeTests` 8, `HouseholdFormationTests` 3 pass. Tools of this context copied to `tc2/out/tools/` (`pt.py` line-ending-aware patching, `gap.py`,
`loadrun.sh` - runs saves forward, `coarsescan.sh`, `wrun.sh`); the seed-42 browser opening state lived headless: `scratchpad/web42/world.tc2`.
**State before C3.6e**: 26127b5 + cc1f436 verified (Web `s16web8` 6/6, full suite 353/353); sim version 70. Verified: Web build of 26127b5 (`tc2/web-build/web`),
Web test `s16web8` 6/6 (bit-identical; the settlement page's Land tab shows "Land: 9 households hold 14 ha of its fields (the most the Doldseinds,
2.2 ha)"); the full suite on 26127b5: 353/353. `LandTests.cs.meta` made by the Unity build (commit it). Nothing else running.
**The phase in one paragraph**: C3.6a (more work a hectare where land binds) and the first sheaves (the harvest's grain as it is reaped) each ended in
Malthus - the villages grew into the food; then holdings (C3.6b: each household's hectares, by founding shares, portions of spare land, the last out,
heirs, given out at sowing), the harvest by land and labour (C3.6c: the land's half to holders, the labour's half by hands - the landless get half
their mouths' share, marry later or never: **hunger a person-year x0.74**, the preventive check), and who leaves (C3.6d: by their own land - the
landless choose to go but in full lands find nowhere, and the land concentrates: 17-66 % landless at year 300).

### Exact state (keep current)
- **C3.6a working the land harder (D2-131, sim version 66; `26` §2d)**: `SettlementSystem` - `TilthFor` (the plan at sowing: fields short of the
  want -> extra ploughing and hoeing a hectare, a share of the season's 109 work-hours, at most `TilthMax` 0.8), `GainOf` (0.30 x (1 - exp(-e/0.4))),
  `SowHa/TendHa/ReapHa/SeasonHa`, `Tilled` (the history episode), `Settlement.Tilth/TilthDone/TilthGain/TilthEvent`; `HistoryKind.FieldsWorkedHarder`
  93 (Local, place-indexed); the settlement page's food section (`Gazetteer.Food`). Headless: `--notilth` (= `_m2` exactly, seed 4's annals
  byte-identical), `--landtally` (`land open/bound:` lines - harvests, arable and fields against need, FU a ha, harvest against bread, the extra
  planned/done/added - and `land hours` by crop stage; `AgentSystem.LandHours`, `SettlementSystem.LandTally`). `TilthTests` 4.
- Runs: `tc2/out/civ/s*_t1` (the slice, `--landtally`, saves 100/200/300) and `s*_t0` (`--notilth --landtally`, the control = `_m2`); lived year
  checks `scratchpad/lived_s5_t1.txt`/`_t0.txt`; the survey before the code `scratchpad/land_s4.txt`, `land_s5.txt` (bin_t1 on the `_m2` saves).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/75232637-109e-4256-9e55-bbecfdd2e0eb/scratchpad`
  (`wrun.sh` pointed here, `pt.py` - line-ending-aware patching: `exec(open(pt).read()); patch(path, [(old, new)])` with LF texts - patch scripts
  `p_*.py`, `d2131.md`). **Never `sed -i` a CRLF file here: Git Bash's sed strips the CRs** (SettlementSystem.cs had to be restored).

- **Verified**: full suite 340/340 on 1000a8b; Web build of 1000a8b (`tc2/web-build/web` is current): Web test 6/6 twice (`tc2/out/webtest/s16web6`,
  `s16web6b`; bit-identical; fps swung with the machine's load - up to 2.6x between two runs of the same build).

- **The first sheaves (D2-132, sim version 67; `26` §2e)**: `SettlementSystem` - `CropYield`/`Reaped` (Settlement), `Sheaves` (each morning of the ripe
  stage, `k == 0` only: a calm village's step takes its grain at the step's end), the harvest's day takes the rest (`max(Reaped, CropYield x (1 - burnt)
  x Harvested)`), `Holdings.Harvest(sid, fu, repay)`, the ration to the next new grain (`ToBread`, `SheafDay` 31; `ToHarvest` reads it for the
  marriage rule and the pages), `SheavesOff` (`--nosheaves` = `_t1` exactly), `GapTally`/`SheafTally` (`--gaptally`), the settlement page's "The harvest
  is coming in". `SheavesTests` 5; WorldGenerator sets `CropYield` for a world begun in the ripe stage.
- Runs: `s*_s0` (`--nosheaves`, = `_t1`, with the gap tally), `s*_s1` (sheaves alone - worse), `s*_s2` (+ the ripe stage's ration), `s*_s3` (final), `s4_s3n`
  (the final binary with `--nosheaves`: annals = `_t1`); `s*_g1` (the gap survey from the `_t1` year-200 saves). Scripts: `gap.py TAG seeds`, `loadrun.sh`.

### Next, in order
1. (done: C3.6a committed 1000a8b; Web test 6/6.)
2. (done: the hungry gap measured and the first sheaves built, D2-132; full suite 345/345.)
3. (done: **C3.6b household land** D2-133, 4f9b94b - holdings move nothing, annals = `_s3`; **C3.6c the harvest by land and labour** D2-134 - hunger
   a person-year x0.74, the preventive check; runs `s*_h1`, `s*_c1`, `s4_c1m` = `--bymouths`.)
4. (done: **C3.6d who leaves**, D2-135 - each household weighs its own land; aggregates at par, land concentrates; runs `s*_d1`, `s4_d1v` = `--villageleave`;
   the DEEP guard reads its two worlds' herds together.)
5. (done: Web build + Web test `s16web8` 6/6 of C3.6b-d; the full suite.)
6. **Measured for the next slice (no code yet; `place --landless`, the `hunger deaths by the household's land` line - runs `s4/42/7/5_g2`, years
   200-260 from the `_d1` saves)**: landless households are 26-63 % of all, ~3.0-3.4 people, 2.0-2.4 hands, 2-17 days of their own grain;
   households holding their want are small and old (1.5-1.8 people, ~1 hand) with 2.6-5.7x their want in land and 207-1,594 days of grain. Hunger
   falls on the landless and land-short (95-99 % of hunger deaths). **Service has no economic driver yet**: the commune works all fields together, so
   a land-rich household needs no hands (its land's half comes regardless) and a servant's keep (~48 FU a year) exceeds a hand's share (~40).
   **Famine amid plenty is partly emerging** (ten worlds, hard-times hunger deaths): by mouths (`_s3`) the holders' spare covered under a quarter of
   the grainless' need at 98 % of them (store under a day's food 80 %); with land (`_d1`) under a quarter at 77 %, a quarter or more at 23 %,
   store over a day's food at 64 %. Candidates, in order: (a) **work for grain** - households with grain to spare hire the hungry for their own
   projects (rebuilding, carts, boats) at a day's bread for a day's work (C3.3b's makers' price), so the landless earn in hard times and the rich
   build; (b) alms by standing in hard times (beyond the first household's, C10.2); (c) each household works its own holding and hires hands - the
   deep version that would give service an economic base; then debts' consequences, partible customs (C11), a land market.
8. (done: **C3.6e the old hand on their holding**, D2-136, sim version 71 - the hoards and most of the landlessness were the old's; runs `s*_e1`,
   `s4_e1n` = `--noretire`.) Next: the full suite and the Web build/test of C3.6e; then re-run the 900-year check (`_l9` with C3.6e) and choose
   from 6 (a)-(c) - the hoards are gone, so alms matter less; service needs households working their own holdings.
7. **The land economy over 900 years** (`s4_l9`, `s42_l9`; PROGRESS): stable, hunger 0.2-0.8 a 1,000 person-years in seed 42's last six centuries;
   landless 54-77 %; **the households holding their want keep decades of grain (800-2,975 days)** - grain with no sink but rot, the village's sales
   and loans. This outranks (a)-(c) above: measure where a rich household's surplus goes now (rot, the village's sales borne by bins, loans,
   gifts, makers paid) and what real sinks a medieval surplus had (beasts, land, buildings, goods, feasts, alms, rents, tithes), then the smallest
   real slice - e.g. households buying beasts and goods with grain (households' own trade, D2-126 "Open") or the rich feeding the hungry as
   patrons (C10.2's first-household rule for every household with years of grain, followings by it).

## Session 16, second context (9 Oct 2026, 12:22-16:00 IST; Control Room run #39 session 2, Resource Governor MEDIUM) - C10.3 closed (D2-128); C3.5 household formation (D2-129); W-FOREST pass 1 (D2-130)

**Handoff state (end of this context)**: HEAD 284471c (+ this handoff commit); everything committed; nothing running. Verified at handoff:
`HouseholdFormationTests` 3, `LeadersTests` 7, `GazetteerTests` 2, `BiographyTests` 4; the last full suite 336/336 was on C3.5 (b8f4830 - the commits
since touch only the view, page text and headless tools); Web test s16web5 6/6 on 284471c (`tc2/web-build/web` is of it). Open: W-FOREST waits for
Arjun's judgement; the next simulation slice is C3.6 land (`26` §3, measured, not chosen).

### Exact state (keep current)
- **C3.5 household formation (D2-129, sim version 65; `07` §7e)**: `SettlementSystem` marriage loop - a woman's suitors are only the men with whom
  what the couple brings (`CoupleDays`: each one's mouth's share of their household's grain and larder; nothing from a household of another
  village) keeps them to the next harvest of his village (`ToHarvest`, `HarvestDay` 39) and 6 days beyond; none: the match waits. `--nomeans` (=
  `_d3` exactly, checked on seed 4 with the final binary). Pages: `Biography.Family` (unmarried of an age to marry: their share's days, the harvest,
  a match waiting), `Gazetteer.Overview` ("Marriages wait: ..."), the Marriage record's ArgA (days of bread; "setting up their household with N
  days' bread of their own"). Tools: `place --marriage`, `marriages by what the couple brought` line (`SettlementSystem.MarriageTally`), the hunger
  line's spare-against-need part (`Holdings.HungerTally` 7-12). `HouseholdFormationTests` 3 (+ `.meta` from Unity configure).
  Runs: `tc2/out/civ/s*_d2` (hunger diagnostic, sim as `_l5`), `_d3` (+ marriage tally; the control), `_m1` (first build: the bride's village's
  harvest), `_m2` (final), `_m2n` (seed 4, `--nomeans`). Marriage tables: scratchpad `marriage_d3.txt`, `marriage_m1.txt` (read with bin_m1,
  v64), `marriage_m2.txt`; `marr.py A B YEAR`.
- **C10.3 a contested succession splits a village - closed (D2-128, sim version 64; `10` §12d)**. Ten seeds `tc2/out/civ/s*_l5` (bin_l5 = 78ac1ee's
  build, `--contests`, saves 100/200/300) against `_l3` (= `--nosplit`): 42 splits, foundings 123 -> 119, people x0.992, vital rates and conflict at
  par, home making x1.10 explained (more people in villages too small for a shed while land is open). Seeds 4 and 6 again on the final build (`_l5t`,
  annals byte-identical to `_l5`) for the factions' outcomes (64 of 99 gave up after three years, 19 gone, 13 found a place).
- Added this context (no change to the simulation): `Leaders.ContestTally` slots 6/9/10 (factions' outcomes), the `contests:` line; `place --splits`
  (headless `Place.cs`); the pages - `Gazetteer.Overview` (a village founded by a split says why; the village left says "Split in year Y ... - V"),
  `Gazetteer.Life` merges `HistoryLedger.PlaceEventsOf` (a page-only index of `HeadmanRose`/`VillageSplit` by village: not the entity index, which
  the storm code reads); `LeadersTests` 7 (a save keeps a faction looking for land; both villages' pages tell the split); `LeadersTests.cs.meta`
  (Unity configure).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/83fa9e96-395d-4137-b152-8cc71ac55fa5/scratchpad`
  (`wrun.sh`, `bind.sh TAG` -> `binding_TAG.csv`, `bindcmp.py A B` - village sizes and binding by size and year, `splits_l5.txt`, `d2128.md`,
  `p_gaz.py`; copies of the new tools in `tc2/out/tools/`).

- **Committed d891d16 (C10.3 closed; full suite 333/333). Web build of d891d16: Web test 6/6** (`tc2/out/webtest/s16web`: browser bit-identical,
  views 60-121 fps, pages 73-82 fps; the century-later life page shows the headman lines; the fresh world's pages at 06:07 of day 1 come before any
  village has weighed its first). Then family plurals fixed in every page and history line (`HistoryText.Plural`: "the Memvases", not "the
  Memvass"; Unity configure compiles the HUD).

- **W-FOREST pass 1 (D2-130, ee5044c + the far canopy commit)**: kinds of wood from the climate (`Game/View/ForestTypes.cs`), ten new forms, stand ages,
  palettes, far forms per kind, the far canopy by kind (UV4 in `TerrainLod`/`Terrain.shader`); views `forest:KIND[:low|:far]`; shots `tc2/out/shots/wf2`
  (sheet4.png), `wf4`, `wf6`; Web tests s16web4/s16web5 6/6. W-FOREST stays OPEN for Arjun's judgement (sheet sent to him).
- Unity shots: a view name may not contain ':' in a file name (ShotRunner fixed); `unity.mjs shots` works after a Web build (the earlier failure
  was the shot runner's own bug, not the build target).

### Next, in order
1. (done: C10.3 commit, Web build, Web test s16web 6/6; famine amid plenty measured and refuted; C3.5 built and measured; W-FOREST pass 1 and 1b.)
   If Arjun answers about W-FOREST, act on it first (pass 2 candidates: D2-130 "Open").
2. (done: C3.5 closed - full suite 336/336, no measurable cost, D2-129, b8f4830; Web build of b8f4830: Web test s16web2 6/6, bit-identical, the life
   page shows the marriage's bread; spans of bread now read in years past a year - "a year's", "21 years'" - `HistoryText.Span/Bread`.)
3. **C3.6 land** (`26` §3: land binds for 91 % of people by year 300 while grown people keep ~4.5-5 free hours a day): measure which binds harder -
   labour a hectare (Boserup: more work a hectare at falling returns - tillage, weeding, manure from the herds) or who holds the land (holdings,
   landless households, labour for others; marriage on a holding) - then build the smaller real slice.
4. From the evidence (D2-129 "Open"): hunger is still 17 % of deaths, in the hungry gap before the harvest - measure what a village does in its last
   weeks (the ration's floor 0.45, wild food, slaughter, relief) before choosing; household land and holdings (marriage on a holding, the NW
   European pattern) when land binds; life-cycle service; factions a full land cannot shed (D2-128); debts' consequences; C9.4 cargo; D2-125's iron
   ask; D2-123 floods; W-FOREST.

## Session 16 (9 Oct 2026, from 09:11 IST; Control Room, Resource Governor MEDIUM: CPU 70 %, RAM 85 %, two heavy jobs) - C3.3 the household's grain, debts, makers paid, hard times (D2-126); C10.2 the village's first household (D2-127)

### Exact state (keep current)
- **C3.3a-c committed 889263b (D2-126, sim version 62; `26` §2c)**: `Core/Places/Holdings.cs` - household bins and the village's common grain in one
  granary, the moral economy and debts, makers paid in kind as far as the taker's grain pays, the village's levy, sharing contracting in hard times.
  Headless: `hold:`, `makers paid:`, `hunger by household:`, `place --households/--debts`, `--nohold` (= `_s3`), `--holdmeansoff`, `--nopay`,
  `--nohard`, `--ledgeryears`. Runs `tc2/out/civ/s*_h1`..`_h5` (`_h5` the slice; saves 100/200/300).
- **C10.3 IN PROGRESS (committed as such with C10.2; D2-128 to write; sim version 64; `10` §12d)**: `Leaders.Contest` at a vacancy (`Weigh(...,
  vacancy: true)` from `Day`), `Migration.Split` (a faction's party to the best known site, the split's record as its cause), `HistoryKind.VillageSplit`
  92, metrics `Splits/SplitHouseholds`, `--nosplit` (reproduces `_l3` exactly, checked on seed 4), tools `--contests` (`contests:` line). First
  version measured (`_l4`, ten seeds vs `_l3`): at par (people x1.02, foundings x0.975) but only 10 splits in 3,000 world-years - the tally on seed 6:
  of 205 vacancies, 107 no unbound rival of 6+ households, 42 rival under half the first's following, and of ~50 that qualified **49 failed only for
  want of a known place** (villages scout only when land is short). **Then built, not yet run over ten seeds: the pending split** - a faction that
  would not follow and knows no place looks for land (`Settlement.SplitHh/SplitCause/SplitSince`; `Migration.WantsScout`/`ScoutsDue` also for a
  pending faction; `Migration.KnowsSite`; `Leaders.Pending` daily: goes when a place is known and it still qualifies, gives up after `SplitYears`
  3). `LeadersTests` 7/7, `HoldingsTests` 12/12; full suite NOT yet run on C10.3; `LeadersTests.cs.meta` NOT yet made (run Unity configure).
- **C10.2 built and measured (D2-127 written, sim version 63; `10` §12c); the full suite passed on it: 331/331**:
  `Core/Society/Leaders.cs` (followings of kin, debtors and clients - `Household.Patron/PatronAt`, set by gifts in hard times and by loans; the first
  household and headman `Settlement.Head/HeadHh/HeadSince/HeadEvent`, `Hold` 0.5, weighed yearly at `Migration.DecideDay` and at a vacancy
  (`Leaders.Day` daily); the first household's gifts in hard times in `Holdings.Draw`; tribute to the lord's first household in
  `Conflict.Harvested`; `HistoryKind.HeadmanRose` 91; pages in Gazetteer.Overview and Biography.Identity). Headless: `leaders:` line, `place
  --leaders`, `place --binding` (C10.3 survey), `--nohead` (= `_h5` exactly), `--notribhead`. `LeadersTests` 5/5, `HoldingsTests` 12/12 (its identity
  test now switches hard times and headmen off). Runs `_l1` (Hold 0.67, no tribute rule), `_l2` (+ tribute), `_l3` (the slice; saves 100/200/300).
  `Conflict.FitAt` made public (tests).
- **The Web build in `tc2/web-build/web` is of an intermediate state** (Unity imported C10.2 sources mid-build): rebuild before any Web test.
- Scratchpad (session 16): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/c30873bf-a161-4a50-b46c-76e150e4ddf3/scratchpad`
  (`wrun.sh`, `hold.py`, `forms.py`, `leaders.py`, `ly.py`, `lycf.py`, `patch.py` - a line-ending-aware replace: `python patch.py FILE OLD NEW`;
  copies in `tc2/out/tools/`). Shell heredocs with backslashes or nested quotes break here: write texts to files.

### Next, in order
1. Unity configure (`LeadersTests.cs.meta`), then ten seeds of the pending-split version (`BIN=bin_l5 TAG=_l5 ... --contests` in `EXTRA`), compare with
   `_l3` (= `--nosplit`): splits, foundings, village sizes (`bash sizes.sh _l5 300` - the repo build reads only saves of its own sim version), people
   at par; the full suite; D2-128 (draft the evidence from the numbers above and the run); docs; commit "C10.3 closed".
2. (done: C10.2 committed with C10.3 in progress) **C10.3 a contested succession splits a village** (evidence D2-127: binding to the first household falls with size, 43 % of households under 100
   people, 8 % at 400+; unbound rival followings up to 90 households): at a vacancy, if the largest following among households not bound to the new
   first household is at least `Hold` of the first's, at least a party's worth, and the village knows a good site, the rival household leads its
   unbound following away to found a village (Sahlins: conflict at leadership transitions; Alberti 2014, Chagnon - fission past ~130-200 people
   unless integrated). Reuse `Migration.NewParty` with the site; history: the split, caused by the death. Measure: foundings, village sizes (towns
   kept only where integrated?), people at par, conflict; `--nosplit` control.
3. Web build + Web test (C3.3 + C10.2 + C10.3): the person/place pages show the grain and first-household lines (shots 5b-5f).
4. Open: the headman deciding tribute, walls or bands; the hungry taking from their own village's store; debts' work and flight; household land;
   D2-125's unmet iron ask; D2-123 floods; W-FOREST.

## Session 15 (8 Oct 2026 19:54 IST - 9 Oct; Control Room run #31 session 5) - C4.2 carts closed (D2-122), nobody in the water (D2-123), crossings surveyed (D2-124), C1.4b the village smith (D2-125)

### Exact state (keep current)
- **C4.2 carts closed (D2-122)** - the closing checks found and fixed three faults: carts begun while hunger sent the village to the wild were never
  worked (15.5 % of carts given up -> 4.9 %: `Carts.ShortBound`, the timber of a cart given up back to the woodpile; `run --carttally`); woodcutters
  with their carts stood in the sea (D2-123); the yard camera framed a roof (`CameraRig` "cartyard": the parked cart with the fewest buildings on the
  camera's side, `Footprints.LineCuts`). Ten seeds `tc2/out/civ/s*_c2` (the hunger rule) vs `_c1n` (no carts): trades x0.67, famines x0.89, people
  x0.97 (t -1.1), vital rates at par; `_c1` -> `_c2` inside the noise.
- **D2-123 nobody works in the water**: headless `wet --load S [--days N]` (a lived world hour by hour: who stands under the drawn water, by
  activity and village, and standing buildings on wet ground). Seed 4 y300: 0.64 % of person-hours in the water -> 0.05-0.08 % after: lived field,
  woodlot and wild-ground spots go ashore (`AgentSystem.Ashore`, `WildFood.GroundPoint` for every kind) and woodlots are chosen above the shore
  (`Woods.Choose` `Shore.Dry`; `Ensure` re-chooses one the sea took). Ten seeds `_c3` (woodlot rule) vs `_c2`: inside the noise. What is left:
  straight local walks across lakes, spots within 4 m of a waterline, a day's lake over works (no flood response yet).
- **D2-124 crossings surveyed**: `place --crossings` - the largest flow on five worlds is 0.08-0.23 m3/s; every stream a worn way crosses is
  fordable; C4.3 (fords, bridges) deferred until a river cannot be forded.
- **C1.4b the village smith (D2-125, sim version 61)**: `Forge.Places` 4 (`SmithBar`, `Key` = copper + iron + practice, `Maker`, `Bar` daily),
  `Person.SkSmith` (0.0015 an hour; `Forge.Hand` 0.8-1.2), more smithies where the smiths fell short 30 days of a year (`Backlog`, `Tally`, `Year`,
  `SmithMore`, `MoreWanted`, `SmithyFor`, up to 4), roles from practice (smith/ironsmith from `SkSmith` >= 0.3; boatwright from plank boats led,
  `Biography.PlankBoats`), building page names its smiths, settlement page counts smithies/smiths/short days, view smoke per smithy,
  `--nosmithcrew` (reproduces `_c3` exactly). Tools: `run --crafttally` (hours by person and village-year per craft: smithy, plank boats, shed,
  kiln), `place --list` shows SMITHY / N SMITHIES. Runs `tc2/out/civ/s*_s1` (four places, no second smithy), `s*_s2` (the slice).
  Results (`_s3` vs `_c3` = the same binary with `--nosmithcrew`): people, famines, vital rates, trade, conflict at par; standing smithies 68 -> 80;
  smiths from practice 24-116 in the three metal worlds (seeds 4, 2, 7), at most 23 hands a smithy-year (355 before), busiest hand 38-49 %; iron
  smelted x0.59 / x1.05 / x2.08, iron traded 22 -> 9 t (seed 4's exporter cannot sell beyond its places: open). Rejected on the way: `_s1` (four
  places alone: iron x0.83, iron trade x0.35), `_s2` (another smithy on short days alone: 68 -> 218 smithies).
- **Verified**: full suite 314/314 (12 min) on the final code; ten seeds as above; shots `tc2/out/shots/c42a`/`c42b`/`c42c` (carts: on the green, in the yard, the earlier
  woodcutters in the sea), `c14b` (Fomthaenstead #20, seed 4 `_s3` y300: two smithies side by side, smoke from the worked one; woodcutters with handcarts
  at cut-over woods; handcarts at the market green). Web build of 96e38ab: Web test 6/6 (`tc2/out/webtest/s15web`, views 77-121 fps, pages 69-88 fps); `tc2/web-build/web` is current.
- **Machine**: Control Room's Resource Governor LOW (CPU 50 %, RAM 75 % for all work; one heavy job at a time). The machine's own baseline sits at
  ~73-75 % RAM (the Claude apps ~2.5 GB, an `oracle` process); a Unity run takes it to ~85-88 % while it runs. A `chrome-headless-shell` not started
  by this session took ~70 % CPU for an hour (left alone). `dotnet build-server shutdown` frees this session's compiler servers.
- Scratchpad (session 15): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/31c113f7-7357-46b4-9c76-c4b097feb4cb/scratchpad`
  (`wrun.sh`, `smith.py` - the metal chain between two tags; copies of both in `tc2/out/tools/`).

### Next, in order
1. **C5.3c re-weighed (measured at the end of session 15, no code)**: the premise "smelt at the ore to save carrying it" does not hold here - seed 4
   `_s3` dug 148 t of bog ore for 18 t of iron in 300 years: carried home in ~5,900 loads, at most ~2,600 hours, against **552,000 hours at the
   smithies** (bloomery iron at 0.06 kg an hour is the bottleneck; carrying is ~0.5 % of the chain). A bloomery at the bed saves almost nothing, so
   iron hamlets would be theatre unless something else draws them (charcoal from far woods? a crew camping out?). What D2-125 left open is smaller:
   an **unmet ask** (a trader who wanted iron and found none, counted at the seller) so a seller's `NeedsMore` sees its buyers. Iron trade is small
   beside pottery (9 t against ~2,000 t over ten worlds) - weigh it against item 2.
2. **C3.3 exchange between households** (`26` §3: a phase - household property, sharing by need, debts). **C3.4 the mill was surveyed at the end of
   session 15 and deferred** (`timeuse --load`: grown people spend 0.7 h a day preparing food and 7.9 h idle - a mill frees spare hours only; `26` §3).
3. D2-123 "Open": a lake risen over works or homes is not a flood people answer (they stay and work in it); local walks are straight lines across
   lakes. A flood slice with the water's own rise.
4. W-FOREST (Arjun, 8 Oct): forests and biomes still read as the same trees recoloured - an art pass.

## Session 14 (8 Oct 2026, 19:02-19:26 IST; Control Room run #31 session 4) - C4.2's people x0.955 explained as noise (stopped early by Arjun)

Short session: Arjun asked to stop and hand off at 19:13. No code changed; HEAD is still d6c29cf's code (sim version 60) plus docs.

### Exact state (keep current)
- **C4.2 carts is still IN PROGRESS** (built and committed in d6c29cf; Session 13 below has the full build). The one open question was why
  people over years 200-299 are x0.955 (t -2.4) with carts against `--nocarts` (`_c1` vs `_c1n`). **Findings this session:**
  1. **Per-person rates are unchanged.** Over years 100-299 and all ten seeds: births 43.91 vs 43.75 a 1,000 person-years, other (age) deaths
     28.84 vs 28.68, net growth 7.20 vs 7.21 a 1,000; hunger deaths 7.03 vs 6.76 (fewer with carts); sickness deaths 0.59 vs 0.86 (driven by two
     epidemics, below). The loss is in the *level* (person-years x0.954), not in how people live and die.
  2. **Seed 4** (the largest loss, 6,533 -> 5,605): both arms track together to year 179; the cart arm's year-180 epidemic killed 1,289 against
     734 (people 3,025 -> 2,357 against 2,954), and the gap stays at 15-20 % to year 300. Villages over years 200-299 at par (22.6 vs 22.9).
  3. **Seed 42** (6,517 -> 6,142): the arms split around years 20-40 (a few FU in one granary at year 20, people apart from year 40) and the cart arm
     grows a little slower, 1.42 % against 1.64 % a year over years 50-100, with **no hunger, no sickness and only 4 trading trips with a cart in
     100 years** (41 carts made, mostly for wood). Diagnostic runs (100 years, `--places`; both reproduce `_c1`/`_c1n` exactly) are in
     `tc2/out/c42diag/s42_n.*` and `s42_c.*`. Their demography lines: conceptions a weighted woman-day 0.02237 vs 0.02182, marriages 0.01168 vs 0.01120,
     infant deaths 213 vs 233 a 1,000 infant-years - all within chance at these counts. Arable land at year 100 384 vs 343 ha follows from the
     smaller population (fields clear the woods), it does not lead it.
  4. **The placebo, all ten seeds** (`--nocarts --nudge 20`: no carts, one FU more in one store at year 20; tag `_c1p`, `tc2/out/civ/s*_c1p`,
     same binary as `_c1`/`_c1n`, d6c29cf). People over years 200-299 against `_c1n`, seed by seed (4 42 2 1 5 3 11 8 7 6):
     placebo x1.007 0.963 1.004 0.857 0.889 0.998 1.075 1.032 1.016 0.999 (mean x0.982, t -0.8, sd of the log ratio 0.069);
     carts   x0.858 0.942 0.991 0.887 1.021 0.951 1.001 0.904 1.014 0.993 (mean x0.955, t -2.4, sd 0.061).
     **Carts against the placebo: x0.972, t -1.06.** The placebo moves single seeds as far as carts do (seed 1 x0.857, seed 5 x0.889).
     What the placebo does NOT reproduce (so these are the carts' real effects): famines x0.83 (t -3.4; placebo x0.98, t -0.2), hunger deaths x0.93
     (t -2.3; placebo x0.99), trades x0.70 (t -4.9; placebo x1.01). Foundings x0.93 (t -1.6) against the placebo's x1.01 (t 0.6) is weak.
  - **Verdict: the people x0.955 is inside the noise - no cart fault.** The vital rates are identical, the two big losses are an epidemic
    (seed 4) and an early chance split with almost no cart trade (seed 42), and the placebo's spread covers it. Nothing to fix; it goes into D2-122
    as evidence (the D2-113 / D2-114 pattern).

### Next, in order
0. (done) The `_c1p` placebo finished (19:24): x0.955 is noise (above).
1. **Close C4.2** - the investigation is finished, only the closing steps are left:
   **D2-122** (the measured state, the design in `24` §2 "Implemented (C4.2)", the ten seeds and this
   investigation), shots (`node tc2/scripts/unity.mjs shots --tag c42a --load "<abs>/tc2/out/civ/s4_c1/world_y300.tc2" --village 2 --views
   "cart@11,cartyard@11,cartfar@14"`), the full suite (`node tc2/scripts/headless.mjs test`, ~12-22 min; last full pass 302/302 was before carts),
   Web build (`node tc2/scripts/unity.mjs build-web --jobs 3`) and Web test (`node tc2/scripts/webtest.mjs --tag c42web`), commit, docs (`24`,
   `25`, PROGRESS, this file, both NEXT_SESSION_PROMPTs).
2. Then Session 13's "Next" items 2-4 (C3.3 exchange between households; roles from practice for smiths and boatwrights; C9.4 / C3.4).

## Session 13 (8 Oct 2026, from 16:11 IST; Control Room run #31 session 3) - C3.2 the potter's kiln, jars and hearths' pots (D2-121)

### Exact state (keep current)
- **C3.2 built (D2-121, sim version 59)**: `Core/Places/Pottery.cs` (jars `Settlement.Jars` seal the store's grain: `RotFactor`; `Day` stands ware as
  jars and breaks them; hearths `Household.Pots`, `Draw`, `Wear`, `CookFactor`, `FiresOwn`; the kiln `KilnWanted` (two months of a potter's jars,
  `KilnFrom` 1,440 kg), `Site` at the clay, `Bar`/`Maker` (4 `Hands`), `Work` (lived; COARSE less the walk), `WoodDay` in `FuelDay`; `Worth`/`Spare`
  for `Trade.Pots` 7), `Learning.Clay`/`ClayCell` by the ground's shape (`ClayAreaHa` 200 ha or a depression; cached by the drainage's version),
  `Structures.Kiln` 24, `ActivityKind.Pot` 36, `WhyCode.Pot` 46, `Person.SkPot`, `Settlement.Kiln/PotEvent/Pots/Jars/PotsOut/PotsSold/HearthShort/
  PotBar/PotBarId`, metrics `PotsMade/KilnHours/PotsDrawn/JarsSet/PotsTraded`. Pages: HUD building page, person page (role "a potter at the kiln"
  from practice; how the household cooks), settlement page "Pots and jars". View: kiln (x1.15), clay pit (grows with ware fired), ware in straw
  (ware to sell), storage jars by granaries (sealed >= 30 %), smoke while a potter works; yard trees kept off works. Kit `kiln`, `clay_pit`,
  `storage_jars`, `ware_stack` (+ metas). Headless `pots:` line, `--nokiln`, `--potsflow`, `place --stores` (the survey), `place --list` jars.
- **Runs**: `tc2/out/civ/s*_k1` (C3.2, saves 50/100/200/300, `--rot --potsflow`: the reference for the next slice), `s*_k1n` (same binary,
  `--nokiln`: D2-063's rules; saves 100/300). Smoke run `tc2/out/c32/s4_p1` (100 years, KilnFrom 720). Tools in `tc2/out/tools/`: `pots.py TAG
  [seeds]` (the pots: lines), `agg.py` (now with felled, rot_pct, pots_traded, kilns).
- **Results** (`_k1` vs `_k1n`): 84 kilns in 169 villages, 1,988 t traded, jars seal 80-100 %, trades x5.4, markets x3.6, rot/people at par,
  famines x1.06 (t 0.7). Tests: `PotteryTests` 8/8; `BiographyTests` (role from practice); `TheCoarseDayRaisesBandsAtTheLivedRate` six worlds.
- **Shots** `tc2/out/shots/c32a`, `c32b2` (Krumere #2, seed 4 y300), `c32c21` (Krailddal #21: ware in straw). Kit previews in the session scratchpad.
- **Found and fixed on the way**: the view's works table had 24 slots (the kiln is kind 24: Start threw and left later instancers null); a
  yard tree grew out of the kiln's dome (yard trees now keep off every work's ground).

- **C3.3 surveyed and deferred** (commit 3db3cec; `26` §3): a tools household ledger (`run --ledger Y`, `Core/Diagnostics/HouseLedger.cs`, null in the
  game) - the village's store moves 31 % of the food households draw from 43 % givers to 57 % takers; household property needs a moral economy
  (sharing by need, gifts or debts), household fields and inheritance: a phase, weighed with C10.
- **C4.2 carts - IN PROGRESS, uncommitted evaluation open (D2-122 not yet written; sim version 60)**. Built: `Core/Places/Carts.cs` (handcart 120 kg
  at 0.85 pace, ox cart 500 kg at 0.7 where the household keeps a grown ox; household fields `CartKind/CartState/CartHours/CartAt/CartNeed`;
  wanted after `NeedTrips` 3 back-bound needs within `NeedYears` 2 - a trader's back's load where more was spared and wanted on a path or road
  (`Trade.Load`), or a wood/stone stint from 400 m+ (`Woods.Cut`, `Stone.Cut`) - with an able wright (SkWood 0.4, 16-59, well); made in household
  hours (lived choice 24 `WhyCode.Cart` 47 u 0.5; COARSE up to `HoursADay` 2 h before the village's building - given only what building left, half
  the carts begun were never made); `Trade.Load/Exchange` cart loads on worn ways; `BeginTravel` at `Carts.Pace`; `Woods.Rate`/`Stone.Rate` take
  `trip` loads and `pace`; `HistoryKind.CartMade` 90; metrics `CartsBegun/Made/Worn/GivenUp/CartTrips/CartKg`; pages (household cart, settlement
  "Carts", cargo "on the household's handcart"); view (parked carts `_parked/_parkedOx` at the yard's end wall, carts with traders/haulers
  `DrawCart`, an ox before an ox cart, a load of sacks); camera `cart`, `cartfar`, `cartyard`; kit `handcart`, `ox_cart`, `cart_load`; headless
  `carts:` and `loads:` lines, `--nocarts`, `--loads`, `place --haul`. `CartsTests` 5/5; Unity compiles (0 errors). `TimeUse` counts all kinds.
  Measured first: 83 % of trading loads bound by a back's load; grown oxen in 1-2 % of households at year 300; ways mostly paths/roads.
- **C4.2 ten seeds** `tc2/out/civ/s*_c1` (carts; saves 100/200/300) vs `s*_c1n` (same binary `--nocarts`; equals `_k1` exactly - seed 4 10,791):
  at year 300 ~35-45 % of households keep a cart (seed 4: 1,098 handcarts, 45 ox carts; 3 % of carts begun given up); **famines x0.83 (t -3.4),
  hunger deaths x0.93 (t -2.3)**, trades x0.70 (fewer, bigger loads), trade gain x0.93, markets x0.93, wood felled x0.985, births/deaths a
  thousand at par - but **people over years 200-299 x0.955 (t -2.4)** (seed 4 9,655 vs 10,791; 42 7,138 vs 7,701). NOT YET EXPLAINED - find
  why first (villages fewer? foundings, households setting out, the cart hours taken from building homes so households camp longer, grain sold
  away in cart loads, a tally `cmp.py c1n c1` and `agg.py c1n c1` - parties/foundings/relief). **Leads** (read from the reports at the handoff):
  foundings x0.926 (t -1.6), households setting out x1.46 (t 1.5), relief x0.88, home making hours x1.02; villages at year 300 fewer in 5 of 10
  seeds (s4 28 vs 30, s42 18/19, s2 27/29, s1 7/8, s8 11/13) and people lower in 6 - fewer new villages founded may be the loss (land is what
  bounds people). Look at the parties that set out and did not found (`tools`: `run --places`, `vtrace`), and whether carts change who leaves
  (`Migration.Weigh`: hardship read from stores sold away in cart loads?).
- **Verified at the handoff (d6c29cf)**: `CartsTests` 5/5, `PotteryTests` 8/8, `SaveLoad` 8/8 on the committed code; Unity compiles with 0 errors.
  **Not verified since carts**: the full headless suite (last 302/302 at d21d09d, before carts and the C3.3 ledger hooks), shots of carts, the Web
  build (`tc2/web-build/web` is still d21d09d, C3.2) and the Web test.

### Next, in order
0. **Finish C4.2**: explain the people x0.955 (above) and fix it if it is a fault (not a mechanism), then D2-122, shots (`cart`, `cartyard`
   views on `s4_c1/world_y300.tc2`), full suite, Web build + Web test, commit, docs.
1. (done) Full suite 302/302; Web build of d21d09d: Web test 6/6 (`tc2/out/webtest/c32web`, views 86-120 fps, pages 68-84 fps); the Web build in
   `tc2/web-build/web` is current.
2. **C3.3 exchange between households** (payment in kind: makers paid; households' own holdings replacing D2-114's `Means`) - the weaver's and the
   potter's ware is the village's and free to its households; export crafts wait for it.
3. Roles from practice for smiths and boatwrights (the potter's and the weaver's are now from practice).
4. C9.4 (cargo by sea) re-weighed with the pottery traffic (markets x3.6); C3.4 the mill. W-FOREST stays open for an art pass.

## Session 12 (8 Oct 2026, from 12:13 IST; Control Room run #31 session 2) - C3.1 the weaver's shed and wares (D2-119); lived save/load exact again; C3.1b more sheds (D2-120)

### Exact state (keep current)
- **C3.1b DONE (D2-120, sim version 58)**: `Settlement.Sheds/ShedShort/ShedMore`; `Weaving.LoomsOf` (8 a shed), `ShedFor` (the nearest working shed;
  lived, the shed's id rides in `WhyArg`), `Tally` (a day the households drained the store below half `Kept` with fibre for every loom),
  `Year` (at `Migration.DecideDay`: >= 30 short days and `NeedsMore` - its people in wares would outwear its looms), `MoreWanted` -> another
  `Workshop` in `Structures.Plan`; `Structures.Lost` hands the first-shed role on. Ten seeds `tc2/out/civ/s*_w7` (the reference now) vs `_w4`:
  home making x0.67, market places x1.18, people at par; towns of 500+ carry 2-4 sheds. Rejected rules: `_w5` (target shortfall: sales
  drained the store), `_w6` (shortfall alone: the change-over from homespun). `WeavingTests` 8; 294 headless tests; **Web test 6/6**
  (`tc2/out/webtest/c31bweb`, views 81-120 fps, pages 71-84; the Web build in `tc2/web-build/web` is current); shot `c31d` (Dahope, 3 sheds).
- **C3.1 DONE (D2-119; commits f89d537, 3cbfcf3, a8fb590 and the closing commit; sim version 57)**: `Core/Places/Weaving.cs` (shed wanted where wear keeps 6 makers busy; makers
  = the village's 8 best weavers by `LoomBar`; `Work` 1.5x pace from real fibre; `Draw`; `Target`; `Share`), `Goods.Wear` (wares wear 0.6),
  `Household.Wares`, `Person.SkCraft`, `Settlement.Workshop/LoomEvent/Wares/WaresHeld/WaresOut/WaresSold/LoomBar/LoomBarId`, `Structures.Workshop`
  23, `ActivityKind.Weave` 35, `WhyCode.Weave` 45, `Trade.Wares` 6, metrics `WaresMade/WaresDrawn/WaresTraded/ShedHours`; pages (Gazetteer
  "Cloth and goods", Biography role from practice, HUD building page); kit `weavers_shed`, `tenter` (exported; **Unity .meta files not yet made**:
  done, committed); view (tinted shed, tenters
  while wares are in store; the first at the shed's free end), camera `work23near` (a standing shed first); headless `wares:` and `water caches:`
  lines, `place --list` wares, `person --pick weaver`, `run --noweave`, `run --waresflow` (tools tally: what wares were traded for).
  Edge works keep `Structures.RoadClear` off the main road's line (a shed stood in the road in the first shots).
  `WeavingTests` 7; 293 headless tests pass.
- **Save/load fix (D2-119 (7))**: lived worlds saved and loaded diverged (pre-existing): the green cache kept while lakes moved; shore and river
  lines cached from the day's lakes/flows. Now `PlanetState.WaterDay` (green), `PlanetState.InBasin`/`BasinVersion` (shore, `Shore.DryLand`),
  `RiverLines` for every reach >= 10 ha draining, filtered by `RiverLines.Stream` (homes, view). Test `SaveLoadContinuesIdenticallyWhileLakesRiseAndFall`.
- **Measured before the design** (`26` §1): goods at target everywhere, ~8 idle hours a day, fibre the scarce input.
- **Runs**: `tc2/out/civ/s*_w4` (ten seeds, C3.1 final: the reference for the next slice; saves 50/100/200/300), `s*_w4n` (same binary `--noweave`:
  the control), `_w3`/`_w3n` (before the road rule). Tools in `tc2/out/tools/`: `wrun.sh`, `cmp.py`, `wares.py` (the C3.1 lines a seed), `agg.py`
  (report quantities' ratios between tags). Results: trades x3.0 (t 5.6), gain x3.8, home making hours x0.28, market places x1.35, people x1.04,
  births/deaths a thousand at par; `_w3` had shown foundings x0.91 / relief x0.82 - gone in `_w4`: noise. Seed 4 `--waresflow`: 4,688 wares for
  6,364 kg fibre, 5,446 FU grain, 929 hides, 331 kg metal.
- **Shots** `tc2/out/shots/c31a` (Brernthorpe), `c31v5` (a shed in the road - before the rule), `c31v11` (Toularndal: open bays, weavers, vat, yarn),
  `c31c5` (Stairby: beside the road, cloth on the tenter), `c31b14` (Demtrerhope). **Web test 6/6** (`tc2/out/webtest/c31web`: views 74-121 fps,
  pages 76-85 fps); the Web build in `tc2/web-build/web` is current.
- Seed 4 first look (`_w3` vs `_d7`): sheds in 23 of 28 villages (smallest 167, largest without 154), home making -76 % hours, trades x2.0, markets
  6 -> 13, people 9,095 -> 10,158; big towns use all their wares (8 looms short), mid towns sell, small villages wear bought wares.

### Next, in order
1. **C3.2 the potter's kiln** (`26` §2) - **surveyed** (`run --rot`, a tools tally, trajectories unchanged): **14.6-17.3 % of the harvest rots in
   store** (seed 4: 6.78 M of 39.3 M FU, seed 7: 2.66 M of 18.3 M; heaps beyond the granaries add <0.5 %) under today's 12 %/year x the best
   potter's 0.65 - a real job for pots. Pots and storage jars made at a kiln by the village's potters, the store keeps better by pots made (not
   by the best potter: D2-063), charcoal at the kiln, clay dug at a clay pit - the weaver's shed's pattern (makers by skill, a need, real
   material, a product that lasts). Or C3.3 exchange between households (payment in kind: makers paid; export looms then pay).
2. Roles from practice for smiths, potters, boatwrights (a year-300 person is "known for" every craft).
3. C3.4 the mill; C9.4 / C10.2 re-weighed with the traffic.

## Session 11 (8 Oct 2026, 09:23 - ~12:15 IST; Control Room run #31 session 1) - C13.3 a town's ground (D2-118) closed; C3.1 designed

### Exact state (keep current)
- **C13.3 done (D2-118), sim version 56.** `Core/Places/Footprints.cs` (rectangles of each plan as the kit builds it, `Boxes`/`BoxDisc`, `HomeFits`,
  `WorkFits`, `Open`, `Audit`, `Room`, `YardFree`, `LineCuts`, `FaceAt`/`FaceOf`, `WingSide` = -1 measured in the game); `Building.Face`/`Faced`;
  `Settlement.MarketR`, `CoreR`, `LanesShut`; `Architecture.Row` (plan 5) and `Fit`/`Fallback`; `Homes.FreeLot` by ground with half-step plots in a
  town's core, `Homes.InOpen`/`Clear` (`HistoryKind.HomeCleared` 89), raising re-lots a camp whose lot was built over; `Streets.Grow`/`CorePlot`/
  `Shut`/`OpenLanes`/`StreetCuts`/`LaneCuts` (a street laid through at most 3 buildings, `Bend` 20 degrees); `Structures.MarketDealt`/`Opened`/
  `AtSite`/`Laid` (`Clear`/`Clearance` removed); `Conflict.WallRadius` between houses (`Astride` counts camps); Gazetteer tells streets, core, market
  place; metrics `HomesCleared`, `TownHouses`. View: `PlaceFaced` (the village's own frame), `Plot` cache (`ReadPlots`), stalls, store stacks,
  market ground (TerrainLod), the row-house overlays, `ShowGround` (shots `--ground`), camera views `rowclose/rowlow/rowstreet/rowmarket/hall/mouth/
  mouthfar`; `Rivers.Mouth` (the river-ribbon-over-the-sea fix). Kit: `home_row_g/h/j`, `frame_row` (+ metas). Tests `FootprintsTests` 6;
  `CharcoalTests` updated; 285 headless tests.
- **Rivers** (`Core/Planet/RiverLines.cs`): the river line moved from the view into the core (one line for sim and view); mouths at the drawn
  waterline or carried across a beach; `Footprints.Wet`/`WetDisc` keep homes and works 1.5 m off the bank; `Homes.InOpenOrWet` (4 a river).
  The view's `Rivers` reads it (widths, ranks); `WaterView` joints by reach.
- **Runs**: `tc2/out/civ/s*_d7` (ten seeds, final rules: the reference for the next slice; saves 50/100/200/300), `_d6` (before the river rule),
  `s4_d1`..`_d5` (rule by rule); profiles `tc2/out/c133/prof_bin_d6.txt`, `prof_bin_d7.txt`. Tools in
  `tc2/out/tools/` (`plotfoot.py` draws the ground; `wrun.sh`, `cmp.py`); this session's scratchpad holds `wrun.sh` with its path.
- **Shots**: `tc2/out/shots/c133a` (Rainthorpe), `c133g` (ground plates), `c133b` (Vodrothorpe, seed 2), `c133c` (Souhope's busy market, seed 42),
  `c133d` (hall with ground plates after the wing fix). Layout plots `tc2/out/c133/`.
- **W-FOREST** (Arjun 8 Oct): forests and biomes still read as the same trees recoloured - an open W task (`18` §8, `25`), NOT solved.
- Known (cosmetic): a river draining into a sea cell drawn as a wide beach can end on the sand short of the water (the sea cell's direction is
  not the water's: Vodrothorpe, seed 2); no ribbon runs over water any more.
- **C3.1 design** written (`26_CRAFTS_AND_EXCHANGE.md` §1: the weaver's workshop and the village's wares); no code yet. Hooks found: lived
  choice block in `AgentSystem` (~line 1009, the smithy's `Forge.Wanted` -> choice 15; add a weave choice and `ActivityKind`), COARSE labour
  day (~line 1803, `Forge.Work`), household wear in `SettlementSystem` (~line 215: draw wares before making), `Trade.Value/Spare` (a wares good).

### Next, in order
1. (done) Web build of e6ab82f: Web test 6/6 (`tc2/out/webtest/c133web`, views 64-120 fps); the person/place pages 67-83 fps - the C13.2 dip
   (54-59) was machine load, not a regression. The Web build in `tc2/web-build/web` is current.
2. **C3 exchange** (chosen over C10.2 - trade is 0-5 traders a year at most villages and markets are rare because barter needs a double coincidence
   of wants; lordships rarely hold): design in `21` / a new `26` doc, first slice workshops and wares (a skilled maker's workshop by need, wares made
   for the village and traded), then exchange within a village and between villages; it gives the market places (C13.3) and the sea (C9.4) a trade.
3. C13.4 suburbs and a second wall; C10.2 when lordships hold.

## Session 10 (8 Oct 2026, 06:15 - ~09:10 IST; Control Room run #23 session 2) - D2-114 closed, C9.4 deferred, C13.1 and C13.2 closed

### Exact state (keep current)
- **D2-114 building variety closed** (DECISIONS D2-114, closing evidence; `13` §13 "what the real shots taught", `21` §6 "a household's means").
  Commits: 5d4bbc2 (hall cap `Architecture.HallRoom`, Unity metas of the new kit), 5805423 (means v2, roofs kept and weathered, kitchen gardens,
  doorstep yards, training grounds beaten, the smithy sooted, the pages, headless `place --list/--households/--standing`), and the closing docs
  commit. Sim version 54 (unchanged: no format change since session 9). 275 headless tests pass. Web build of this code tested 6/6
  (`tc2/out/webtest/d2114web`, 59-122 fps); the Web build in `tc2/web-build/web` is current.
- **Runs**: `tc2/out/civ/s*_a4` (hall cap, first means), `_a5` (every D2-114 rule: the reference for the next slice), `_a5p` (placebo: `--nudge 150`
  on the `_a5` binary). Saves at 50/100/200/300 in `s*_a5/`. Tools in this session's scratchpad (`wrun.sh`, `cmp.py`; copies in `tc2/out/tools/`).
- **Shots**: `tc2/out/shots/arch1*` (before), `arch2lord`, `arch2open`, `arch2y100`, `arch2works`, `arch2works2`, `arch3works`, `arch4`.
- **Found, not ours**: a river mesh continues past the shore out over the sea (browser shot `d2114web/4_close.png`, `arch2lord/village_10.png`) -
  offered to Arjun as a separate task chip ("Fix the river ribbon drawn out into the sea").

- **C9.4 surveyed and deferred (D2-115)**: sea traffic is thin (0-318 voyages a world in 300 years, nothing left unsailed) and relief by sea was
  bound by the boats' room in 0-19 of 69-538 gifts (`run --seashort`): no demand for cargo boats; docks and cargo wait for exchange (C3).
- **C13.1 streets closed (D2-116)**, commit 7b17902, sim version 55: `Places/Streets.cs`, `Settlement.StreetN/Street0..3`, `Homes.FreeLot` frontage,
  `Structures.Clear`, the view (houses face their street, door paths, street ground; `TerrainLod.Ground1` read once a patch). Runs `_b1`, `_b2`
  (ten seeds: the reference for the next slice), `_b3` / `_b3n` (A/B in one binary, `--nostreets`). 277 tests; Web test 6/6 (`c131web`).
  Shots `street1`, `street1y100`. Layout plots: `place --layout` and the scratchpad's `plotlay.py` (copy in `tc2/out/tools/`).

- **C13.2 back lanes closed (D2-117)**: `Streets.Lines/Line/IsLane/Weigh` (lanes derived from streets, no new state), `Homes.FreeLot` over all
  lines, the view (`Facing` with its line; lanes' ground). Runs `_c1` (seed 4), `_c2` (ten seeds: the reference now). Shots `lanes1`.

### Next, in order
1. **C13.3 rows in a town's core** (designed, not begun): where a town's street frontage near its green is taken (a village of 250+, frontage
   within ~60 m of the green), the plots are divided - infill lots at the half-step between the 13 m frontage lots (`Streets.Lot` at j + 0.5;
   `Homes.Taken` keeps 6 m, so 6.5 m infill passes). A narrow plot forces a narrow, tall house: `Architecture.HomeForm` gives a lot narrower than
   10 m along its line (nearest home on the same line, `Streets.Facing` with its line) only `Two` (6.6 m long) or `Small` (4.8 m), never a longhouse
   or hall (12-15 m would overlap its neighbours) - so two-storey houses stand wall to wall in rows along the main streets, as burgage plots were
   divided in medieval towns. Check overlaps in `place --layout` plots (`plotlay.py` draws houses as points: add plan lengths), shots
   `close`/`low`/`street` of a town of 600+ (`s4_c2` Vumthorpe #19 or Rainthorpe #7), ten seeds against `_c2`, Web. Then the market place (the
   green widened where traders come: `Settlement.TradeIn`, lots kept clear within a radius growing with it) and the green kept open.
   Or **C10.2** the political centre (a lord's seat raised with tribute labour, gates where streets cross a ring, towers): lords are 0-2 a world,
   almost all with one tributary - lordships that hold come first; walled villages are common (up to 29 a world).
2. C9.4 waits for C3 exchange (D2-115).
3. Re-measure the Web person/place pages on a quiet machine (54-59 fps in `c132web` against 75-85 in `c131web`).

## Session 9 (8 Oct 2026, 04:31 - ~06:30 IST; Control Room run #23 session 1) - handed off (context full; D2-114 in progress)

### Exact state (keep current)
- **C9.3 closed (D2-113), commit 3900ec2**: relief by sea's extra famines traced village by village (seed 2: an epidemic one world had and the
  other not - chance; seeds 11 and 8: relief an unconditional subsidy holding land-short villages over their land for ever) and fixed by **social
  storage** (relief owed back: `DeedBook.Owed`, `Trade.CreditDays` 60, `Trade.GiveBack`, `ReliefReturned` 87); **lived voyages** (`Trade.Booking`,
  `AgentSystem.StartVoyage`/`VoyageStep`/`PutOut`/`VoyageLanded`, `Trade.Deliver`/`LoadAboard`, `ActivityKind.Voyage` 34, `Person.Voyage`); one rule
  at both resolutions (`Trade.Crewed`, `CanSail`, `MinSeaLoadFu`). Sim version 53; 269 tests at that commit. Ten seeds `tc2/out/civ/s*_d5s`
  (and `_d5n` no sea) against `_c92r`: famines x0.971, hunger per person-year x0.999, people x0.955; sea vs no sea inside the placebo's spread
  (`_d2p`). Web build of the C9.3 code: Web test 6/6 (`tc2/out/webtest/c93web`; views 55-120 fps - the forest at low altitude 55, the person page
  48-58: lower than C9.2b's 73-120, re-measure on a quiet machine). Shots `tc2/out/shots/voyage1`, `voyage2`.
- **D2-114 building variety - IN PROGRESS (Arjun's standing requirement, see the root `NEXT_SESSION_PROMPT.md`)**. DECISIONS D2-114 records the
  design and evidence so far; `13` §13, `18` (end), `21` §6 carry the requirement. In the tree (committed as work in progress at the handoff, see
  `git log`): `Core/Places/Architecture.cs` (forms, traditions with drift, `Means`, `Gain`, `Size`, `Describe`), `Building.Form`,
  `Settlement.Tradition`, `Household.Rebuild`, `Homes.WeighRebuilding`/`Rebuilt` (yearly, at `Migration.DecideDay`), `Construction.OwnWork`/
  `OwnStage`, `HomeRebuilt` 88, `WhyCode.Rebuild` 44, `Metrics.HomesRebuilt`, sim version 54; `Tests/Core/ArchitectureTests.cs` (5 pass); headless
  `forms:` lines; the kit `tc2/art/kit.py` (homes x15, frames x5, chimney, plinth, outshut, barn, granary, smithy, market_stall, market_cross),
  exported to `Game/Resources/TC2Models` (old `house_*` and `market_stalls` removed); the view (`WorldObjectsView`: homes from their form,
  overlays, pens, works tinted, the market; `Instancer.Add(.., wall, ..)`; `Instanced.shader` `_WallColor`).
- **Verified**: headless build, float lint, `ArchitectureTests` 5/5 (the full suite was NOT rerun after the rebuilding patch); without rebuilding
  the trajectory is bit-identical to `_d5s` (seed 4). **Not yet verified**: the Unity compile of the view/shader (a `unity.mjs configure` was
  started at the handoff - its log `tc2/out/unity_configure_d2114.txt`; it also makes the new models' and `ArchitectureTests.cs`'s `.meta` files,
  which must be committed); no in-game shot of the new houses yet; no Web build.
- **Runs**: `tc2/out/civ/s*_a3` (ten seeds with rebuilding; saves at 50/100/200/300). First three (seeds 4, 2, 7): 367-906 houses rebuilt in 300
  years, two-storey houses 125-302 (36 on seed 4 without rebuilding), but **hall-houses 54-129 - too many** (each rebuild by whoever is best off
  that year adds a hall: cap it - one standing hall a village, or one per ~150 people); people at year 200-299 +0-10 %, famines up on all three
  (74->99, 111->163, 107->131) - judge only with all ten against `_d5s` and the placebo's spread.
- Scratchpad (session 9): `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\9cd1c516-cc19-4358-8f6c-acaaf0252961\scratchpad`
  (`wrun.sh`, `cmp.py`; copies in `tc2/out/tools/`).

### Next, in order
1. Check the Unity compile log (`tc2/out/unity_configure_d2114.txt`); fix any error; commit the new `.meta` files.
2. Cap hall-houses (one standing hall a village, or by population) and re-read the ten `_a3` seeds after the fix against `_d5s` with `cmp.py`
   (people, famines, hunger, the wood store: rebuilding takes labour and wood) - a placebo if an effect looks small.
3. Shots: `node tc2/scripts/unity.mjs shots --tag arch1 --load "<abs>/tc2/out/civ/s4_a3/world_y300.tc2" --village big --views "close@10,village@10,region@10"`
   and a small young village (`world_y50`), a walled town, a lord's village; judge silhouettes and colours (zoom crops); tune the kit and colours.
4. The building and person pages: `Architecture.Describe(b.Form, home)`; then a Web build and its frame rate (more home models = more draws).
5. Then D2-114 closed in DECISIONS/PROGRESS/handoff; then C9.4 docks, cargo boats and coastal trade, or C10.2 leaders - halls, towers, gates and
   keeps as real structures.

## Session 8 (7 Oct 2026 22:40 - 8 Oct 2026 ~02:20 IST, Control Room run #7 session 3) - handed off (context full; C9.3 in progress)

### Exact state (keep current)
- **Commits this session**: 9a2ceb7 + 23ac1c9 **C9.1 closed** (D2-110: ten seeds after the landing fix found boats in the square and twice in
  the sea - `Boats.ShoreOf` ring search, `ShoreMoved`, `Afloat`; `_c91k` clean, at par with D2-108; Web build of 9a2ceb7 tested 5/5 at 68-120 fps
  (`tc2/out/webtest/c91webb`); shots `boats2`), c5fd15f **C9.2a the landing and the crew** (D2-111, sim version 50), then **C9.2b boats of
  planks** (D2-112, sim version 51; 262 tests).
- **C9.2a (D2-111)**: one landing a village (`Settlement.LandX/LandY/LandUx/LandUy/LandAt`, `Boats.EnsureLanding/Beach/Waterline/UpTheBeach/Drawn/
  Landing`, `AtWater`/`AtSea`/`Afloat`), boats in a row on the sand a metre above the sea, following the coast (`ShoreMoved`) and a risen sea
  (`Plan`); lived crews (`AgentSystem.Crew`, `Outing`, `CrewWait` 30 min, `BeginLocal(.., endAt, pace)`), metrics `BoatOutings`, `CrewSeats`;
  shore fishers at the waterline (`Boats.Edge`, `OutOfTheWater`, `WildFood._edges` cache); people gather at the green (`AgentSystem.Green`/
  `Ashore`) where the sea took the square; view: one hull per boat with its crew. C9.1's lived days caught less from boats than COARSE (x1.23);
  now x0.90 (`BoatsGoOutAlikeAtBothResolutions`, 5 worlds x 24 days).
- **C9.2b (D2-112)**: `Tech.Boat` (8th technique; `Person.KBoat`/`Sc7`, `Settlement.BestBoat`/`BoatEvent`), found by woodworkers at a dugout
  (`Learning.Practice` from `Boats.Work`, `Learning.BoatWright` 0.3, tools); `Structures.PlankBoat` 21 and `BoatShed` 22 (`Function.Boatyard`),
  `Settlement.Shed`/`SeaP`; `Boats` by kind (`IsPlank`, `YearsOf/SeatsOf/MaxOf/HoursOf/WoodOf/NameOf/RowPace/PatienceOf`, `Risk(wind, kind)`,
  `WantsKind`, `Ready`, `ShedStands`, `OnTheStocks`, `ShedWanted`, `ShedSpot`, `Craft`); timber taken at laying-down (both kinds);
  `WildFood.ReachP`, `ExpectSea(ground, plank)`, `Fish(.., plank)`; metrics `PlanksBuilt`, `PlankFishKg`; kit models `dugout`, `boat_shed`
  (exported to `Game/Resources/TC2Models`; `boat` is the plank boat); view `_beachedP/_afloatP`, the shed turned mouth to the water.
- **Runs**: `tc2/out/civ/s*_c91j` (pre-fix), `_c91k` (C9.1 final), `_c91m` (4 seeds, final rule), `_c92a/_c92b` (C9.2a tries), `_c92c` (C9.2a
  final), `_c92p/_c92q` (C9.2b tries), `_c92r` (C9.2b final). Shots `boats2`-`boats6`, `planks1`, `planks2`.
- **Scratchpad (session 8)**: `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\79f297a9-c9c6-41f5-a86c-efdd067a4516\scratchpad`
  (`wrun.sh`, `cmp.py` - the `boats_made` regex fixed: before it read "given up half-made"; `boats_given_up` added; plank columns). Copies of
  both in `tc2/out/tools/` (fix `SCR=` in `wrun.sh` to the new scratchpad).
- **Headless additions**: `boats:` line - plank boats, villages that build them, sheds, plank boats made and their fish, boats in the water /
  away from it / mean height above the sea (by village), landings (range, mean distance), outings and crew seats; `squares under the sea:`;
  `run --load S --years 0 --boats SID` prints the landing, the next boat's spot, each boat's height above the sea.
- **Known**: a village's sea is often fished nearly empty by year 300; boats are begun where the sea promises 1.25x even a nearly empty shore;
  crews are of the household only; births and deaths a thousand person-years ~1-2 % lower with plank boats (people at par); COARSE never
  counts hunting trips (D2-063 comment bug); DEEP's fish x0.8 of COARSE (D2-083 lean).

- **C9.3 voyages (D2-113) IN PROGRESS**, committed as work in progress (sim version 52): `Places/SeaRoutes.cs`, `Trade.BestSeaPartner/Voyages/LoadBoats`, relief by sea in `Trade.Relief`, `Plan/Load/Exchange(.., loadKg)`, metrics `Voyages/SeaCargoKg/ReliefBySeaFu`, `VoyageTests` 3. Runs `tc2/out/civ/s*_c93a` (seed 4 trade only), `_c93b` (seeds 4, 8), `_c93c` (ten seeds). Finding: famines x1.18 (t 2.1) with relief by sea.

### Next, in order
1. (done: the Web build of 1808e81 tested 5/5, every view 73-120 fps, `tc2/out/webtest/c92web`.)
1b. **First**: D2-113 "Open" - why relief by sea brings more famines (seeds 2, 11, 8); fix (likely a cap on what a giver sends by sea), rerun ten seeds against `_c92r`, then lived voyages (crews rowing along `SeaRoutes`, drawn), shots, Web build, close C9.3.
2. C9.3 the landing and the dock (by catches landed and boats kept: a jetty/quay where boats are many), or crews beyond the household
   (neighbours, shares), or farther grounds for plank boats; then C9.4 cargo boats and coastal trade (the coastal proof, `25` §5).
3. Or C10.2 leaders, succession and the polity page.

## Session 7 (7 Oct 2026, 18:50-~23:00 IST; Control Room continuation) - handed off (context full)

### Exact state (keep current)
- **D2-108 done** (the C10.1 drops followed): ten seeds (42, 7, 4, 2, 1, 3, 5, 6, 8, 11) of C5.4 vs C10.1 show no systematic loss (century
  means at par); the lord's half-share was a second wrong - replaced by the lord answering its tributaries' wrongs at half, its band's loot
  settling the tributary's wrong (`Band.Motive` 2, `Band.For`; metric `LordsAnswered`; history ArgB 2 "they answer the wrongs it did X").
  Sim version 48; 244 tests. Runs `tc2/out/civ/s*_c108a` (and `s{1,3,5,6,8,11}_c54a/_c101a` added for the comparison).
- **Scratchpad (session 7)**: `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\dd8879ca-9abf-4584-bd64-5ba72f80ce6a\scratchpad`:
  `wrun.sh` (BIN = a folder in this or the session-6 scratchpad, or a path), `cmp.py TAG_A TAG_B seeds...` (paired log-ratios of annals
  and report lines: judge mechanisms by century means, never the last year), `bin_c108a`.
- **D2-109 done** (the Web frame rate): the frame was CPU-bound in draw submission (one draw per colour group, world-sized bounds, into all
  four cascades). `Instancer` now keeps colour/emission/walk per instance (`TC2/Instanced`, `TC2/Underwater` read them from the instancing
  buffer) and gives each batch of 1,023 its own bounds: 34-37 instanced draws a frame; close 90-102 fps, forest 100; Web test 6/6
  (`tc2/out/webtest/s7webb`, every view 73-120 fps). Tools: `FrameClock` (bridge `cpuMs`, `draws`), bridge `cull`, `shadow C R`, `webperf
  --sets/--reset`. The Web build in `tc2/web-build/web` is current (D2-109).
- **C9.1 boats from the woods (D2-110) - code committed c674892, NOT yet closed**: `Core/Places/Boats.cs` (want, Begin, Work, Of/Worn,
  AfterRow, SeaExpect, WindAt/Risk/Trip/Overturned, Leave, Landing + NearestDry), `WildFood.SurveySea/ExpectSea/Fish(.., boat)`,
  `Settlement.Sea`, `Household.Boat`, `Structures.Boat` 20, `ActivityKind.Sail` 33, `WhyCode.Boat` 42, history BoatBuilt 85 / BoatLost 86,
  death 12 (drowned), metrics BoatsBegun/Built/Worn/Lost/GivenUp/BoatTrips/Drowned/BoatFishKg, AgentSystem (choice 20, SeaBoat with the
  `SeaHere` cache, Sail completion, COARSE seats and deferred drownings), view (`WorldObjectsView.DrawBoat`, sailors in boats), HUD building
  page, Biography/Gazetteer lines, headless `boats:` line and `run --load S --years 0 --boats SID`. Sim version 49; 252 tests at the commit
  and again on the final code at handoff (252 pass). D2-110 is in DECISIONS with its evidence and an "Open".
- **Next, in order**: (1) close C9.1: rerun ten seeds (`BIN=<copy of tc2/headless/app/bin/Release/net8.0> TAG=_c91j ... wrun.sh` in two
  chains, then `python cmp.py c108a c91j 42 7 4 2 1 3 5 6 8 11`), check `--boats SID` shows no WET boats on a few villages, Web build
  (`node tc2/scripts/unity.mjs build-web --jobs 3`, ~6-11 min), `webtest.mjs --tag c91web`, shots `unity.mjs shots --tag boats2 --load
  <abs save> --village <id with boats> --views "work20@10,work20far@10,close@10,village@10"` and judge them; update D2-110's Open, commit.
  (2) Then C9.2 plank boats and boatbuilding, or C10.2 (leaders, succession, the polity page).
- **Known**: COARSE never counts hunting trips (`wild: trips hunt 0` - the D2-063 comment bug); DEEP's fish x0.8 of COARSE (D2-083 lean).

## Session 6 (7 Oct 2026, from 10:56 IST) — handed off at ~19:30 IST (context full)

### Exact state at handoff (read first)
- **Last commit 00ae0d6** (C10.1). Working tree clean except an untracked `unity/TinyConsequences2/Assets/Resources.meta` (an empty
  `Assets/Resources` folder from 6 Oct - not ours; leave it). No background jobs running. **Sim version 47; 243 headless tests pass**
  (full suite at 00ae0d6). Unity compiles (shots `place1`); Web build in `tc2/web-build/web` is current up to C12.2 (not C10.1 - Core-only).
- **Done this session after C1**: C4.1 roads by use (D2-097), C7.1 rings that grow (D2-098), C12.1 person page (D2-099), C5.1 copper (D2-100),
  C5.2a charcoal (D2-101), C5.2b iron + the COARSE smith's labour day (D2-102), C5.3a places by their ore (D2-103), C5.3b iron traded
  (D2-104), C5.4 whose ore (D2-105), C12.2 settlement page + smithy mesh fix (D2-106), C10.1 the lord's call (D2-107).
- **Open, in order**: (1) C10.1's population drops (seeds 7 -24 %, 2 -26 %: D2-107 "Open"); (2) Web frame rate 35-56 fps near the ground
  (target 60; earlier readings since C1.4 were false - see the bullet below); (3) C5.3c work at a distance (low value: the carry is ~5 % of a
  kilo of iron's labour - consider skipping to C8/C9/C10.2).
- **New headless tools**: `person --load S [--pid N|--pick oldest|smith|fighter|elder|child]`, `place --load S [--sid N|--pick big|iron|claims]`,
  `run --load S --years 0 --civ-of ID`, `run --load S --years 0 --events KIND` (any `HistoryKind` name, with its cause); report lines
  `ores:`, `iron:`, `charcoal:`, `claims:`, `ore sites:`, raids line with the call. Shots views `work17` mine, `work18` charcoal hearth,
  `work19` bog pits.

### Exact state (session 6, earlier - kept for reference)
- **Arjun's direction (7 Oct, D2-089 - the standing direction)**: an autonomous medieval civilisation inside a living miniature planet
  (`20_CIVILISATION_DEPTH.md`): Age-of-Empires system breadth and readability with no player orders; fewer, deeper people (tiers A
  100-500 dev, B 500-1,500, C 1,500-3,000); every structure from need, labour, material and knowledge (the ten questions, `20` §2);
  presentation develops with the simulation (D2-084 stands). Docs `20`-`25`; roadmap `25_CIVILISATION_ROADMAP.md` (C1-C13, W and P alongside).
- **C1 "the working town" closed (D2-090 to D2-096)**, all committed (last: the C1 close commit after 08c2a56):
  C1.1 structures by kind (`Core/Places/Structures.cs`: kinds table, works by need with causes, store capacity, placement by zone, building
  page); C1.2 timber from the woods (`Woods.cs`); C1.3 stone (`Stone.cs`); C1.4 the smithy and arms (`Forge.cs`); C1.5 training grounds and
  `Person.SkFight` (`Training.cs`); C1.6 markets and rings on dry ground; C1.7 tier-A pressure worlds read through `run --civ`. Sim version
  38; 207 tests. Structures keep their history while they exist (HistoryLedger local-tier keep).
- **Tools**: scratchpad (session 6) `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\2b3f3bc2-f71a-43e5-9ab3-082850348dfd\scratchpad`:
  `wrun.sh` (BIN=bin_x TAG=_x YEARS SNAP MODE EXTRA; DEEP 12 runs with the scale report, annals and saves into `tc2/out/civ/s<seed><TAG>`),
  `sheet.py` (contact sheets), patch and doc scripts. Headless `run` report lines: `works:`, `woods:`, `stone:`, `markets:`, `training:`,
  `force:`; `run --civ N` prints the N largest villages' civilisation timelines with causes (`#id` for shots). Shots: `--village big|ID`,
  views `work<kind>` / `work<kind>far` frame a village's structure (kinds: 10 well, 11 granary, 12 smithy, 13 quarry, 14 ground, 15 market,
  16 barn); `--load` needs absolute paths.
- **Pitfalls found this session**: this Bash tool collapses `\\` and breaks on apostrophes inside heredocs - write patch scripts, docs and
  commit messages to scratchpad files with the Write tool, then `python file.py` / `git commit -F file`. COARSE-vs-lived rate tests need pooled
  worlds (single worlds give 3-20 events). A quarry on rock the sea later takes was re-planned forever (fixed: forget drowned rock).
- **Runs**: `tc2/out/civ/s{42,7}_c1{1c,2a,3d,4c,5a,6a}` (300 years, saves at 100 and 300), `s{42,7,11}_tierA` (200 years, saves at 25, 100,
  200). Shots `tc2/out/shots/civ1`-`civ6`, `civA_y25`, `civA_y200`, sheet `civA_sheet.png`.
- **C4.1 roads by use done (D2-097)**: runs `tc2/out/civ/s{42,7}_c41a`, shots `ways1`, `ways2`; `Road.mat` needs `unity.mjs configure`
  before a Web build (the editor builds it from the shader meanwhile).
- **C7.1 rings that grow done (D2-098)**: runs `tc2/out/civ/s{42,7}_c71d` (saves at 100, 200, 250, 300), shots `rings1` (`--village 15`). The
  civ header shows walls in progress, old rings, wood and the project. Found: works stalled by float rounding (`Construction.Done`).
- **C12.1 the person page done (D2-099)**: `Core/History/Biography.cs` (shared by the HUD and headless), HUD "Life page" beside Follow (tabs,
  `Hud.ShowPerson`), WebBridge `person PID|oldest [TAB]`, `webtest.mjs` shots `9_person*`, headless `person --load SAVE [--pid N | --pick
  oldest|smith|fighter|elder|child] [--village ID]` (`tc2/headless/app/src/Person.cs`). The page's look is not yet seen: IMGUI is not drawn in
  batch shots - check it in the next Web build (`unity.mjs configure` first for River.mat/Road.mat).
- **C5.1 copper from the ground done (D2-100)**: `Core/Places/Mining.cs` (`Deposit`, `Deposits` store in `World`, `Mining` law), structure
  kind 17 mine, `ActivityKind.Mine` 31, `WhyCode.Ore` 39, history `OreFound` 81 / `OreGaveOut` 82, `RngStream.Geology`, settlement fields
  `OreCell/OreDist/OreEvent/Ore/OreWant/Mine`; `TestWorlds.WithCopper` premise; kit `mine_pit`; headless `ores:` line. Runs
  `tc2/out/civ/s{42,7,11}_c51d` (saves 100/200/300; `_c51a`-`_c51c` are the tuning tries), shots `mine1`, `mine2` (`--village 3 --views work17`).
- **C5.2a charcoal done (D2-101)**: `Core/Places/Charcoal.cs`, structure kind 18 `CharHearth` (at `WoodX/WoodY`), `ActivityKind.Burn` 32,
  `WhyCode.Charcoal` 40, settlement `Charcoal/CharcoalWant/CharHearth/ClampUntil`, kit `charcoal_clamp`, smoke while `ClampUntil > now`,
  headless `charcoal:` line; `Tech.MetalFuel` removed. Runs `tc2/out/civ/s{42,7}_c52a`, shots `char1` (`--village big --views work18`).
- **C5.2b iron done (D2-102)**: `Mining` handles two kinds (`Copper`, `Iron`; `CellOf/DistOf/OreIn/WantOf`), structure kind 19 `BogPits`,
  `WhyCode.BogOre` 41 (digging stays `ActivityKind.Mine`), `Tech.Iron` (seventh technique; `Person.KIron`, `Sc6`), settlement
  `BestIron/Iron/IronArms/FeCell/FeDist/FeEvent/FeOre/FeOreWant/FeMine`, `Tech.ToolPace/ToolWear/IronShare/CopperWanted`, `Forge.IronWanted/
  CopperWanted/IronCan/CopperCan/IronArms`, `Charcoal.PerKgIron/IronFrom/BurntIron`; the COARSE smith's labour day (AgentSystem, before the
  fields); kit `bog_pits`; headless `iron:` line. Runs `tc2/out/civ/s{42,7}_c52c` (`_c52b` was the narrow stream-only bog rule), shots `iron1`
  (`--village 6 --views work19`).
- **C5.3a places by their ore done (D2-103)**: `Mining.Lacks/OreInSight/SitePull/SiteBonus`, `Migration.Look` (the pull, `Scouted` ArgA),
  `Migration.FoundAt` calls `Mining.Look`; metrics `ScoutedForOre/FoundedByOre`; headless `ore sites:` line. Runs `tc2/out/civ/s{1..7,42}_c53a`
  against `_c52c` (seeds 1-6 added for the comparison; `s{7,42}_c53x` = the pull switched off: identical).
- **C5.3b iron traded done (D2-104)**: `Trade.Iron` (goods 6), `Settlement.IronSold`, `Forge.IronTarget/ForSale`, metric `IronTraded`;
  headless `iron:` line (traded, biggest seller), `run --civ-of ID`. Runs `tc2/out/civ/s{42,7,4}_c53b`.
- **C5.4 whose ore done (D2-105)**: `Mining.WorkedBy/MayDig/Claim/Pressing/ClaimDays`, settlement `ClaimDeed/ClaimOn/ClaimAt`, history
  `OreTaken` 83, metrics `OreClaimedKg/OreClaims`; `Mining.Dig(..., pid)`; headless `claims:` line, `run --events KIND`. Runs
  `tc2/out/civ/s{42,7,4,2}_c54a`. Story: seed 7 Thestford/Neldthorpe (years 281-288; `--events OreTaken`, `--events Taken`).
- **C12.2 the settlement page done (D2-106)**: `Core/History/Gazetteer.cs`, HUD `ShowPlace` ("Their village" on a person's card, "Its village"
  on a structure's page), WebBridge `place SID|big [TAB]`, headless `place --load SAVE [--sid N | --pick big|iron|claims]` (`Place.cs`).
  **Web build checked** (`tc2/out/webtest/c5web`, WebGPU): the person page renders; fixed the smithy mesh read (console errors in players).
  The build in `tc2/web-build/web` predates the settlement page and the smithy fix - rebuild before the next browser check (10 min with
  `--jobs 4`); River.mat/Road.mat are now committed.
- **Web build checked again** (`tc2/out/webtest/c122web`): settlement page renders, console clean (smithy fix). **Frame rate below target**:
  35-56 fps at village/close/forest views (WebGPU). The c5web run's 57-121 fps was false: since C1.4 the smithy mesh read threw while
  WorldObjectsView set up, so trees, woodpiles and later works were never drawn in Web builds. Measure layers with
  `node tc2/scripts/webperf.mjs --view close` and `--view forest` on a quiet machine (web build in `tc2/web-build/web` is current up to C12.2).
- **C10.1 the lord's call done (D2-107)**: `Conflict` `_callers/Members/CallAtMost/CallOwed/CallBold/LordsShare`, `Band.Called`, history
  `CallAnswered` 84, metrics `CalledBands/CalledMen/LordsWronged`. Runs `tc2/out/civ/s{42,7,4,2}_c101a` against `_c54a`.
- **Next**: (1) **follow the C10.1 population drops** (seed 7 -24 %, seed 2 -26 % with fewer hungry deaths: D2-107 "Open"); (2) the Web
  frame rate (webperf layer costs; target 60 fps near the ground); (3) C5.3c work at a distance: when ore or wood is far, smelt where it is (a bloomery at the bed or the woods: carry iron, not ore and
  charcoal), stints of days with a camp at the site, households moving to steady work there (a work hamlet founded by its work); then C5.4
  claims (a worked bed or body known as a village's, digging it a deed; news of beds by traders). To check: whether the bloomery holds in
  small villages over long runs (a complex technique); copper ore is still dug where iron serves only some. Seen before: people walking routes across cells the
  sea has since drowned; works hard to tell from homes at the village view; the training ground's posts too thin to read; fields drawn over
  crags; COARSE hearth smelting only when goods are short (D2-100); the person page's look in a Web build.

## Session 5 (7 Oct 2026, from 06:40 IST) — in progress

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-085 - the current priority)**: after D2-083 is verified and committed, STOP before the D2-084 polish
  and **reconsider the planet's physical size** (D2-007's R = 10 km is reopened): study TC1's actual scale, test smaller TC2 planets
  (N64 kept, R = 10 / 7 / 6 / 5 / 4 km) really simulated, measure population, interaction density, travel, performance, take comparable
  screenshots (orbit, region, village, close), decide the radius myself and change it now if the evidence supports it (old saves may be
  invalidated); then redo D2-084 on the chosen scale. Fewer people is fine (2,000-8,000 mature); no world where everyone reaches everyone.
  Study and decision: `docs/tc2/19_PLANET_SCALE.md`.
- **D2-083 closed and committed** (22cad8d): 177 tests (the small-world herd check now a factor-2.5 bound - DECISIONS D2-083
  "Closing checks"), COARSE 400-year hashes unchanged (42 55cfd96b471db300, 7 8c3e14c324165015).
- **D2-085 decided, made and committed: R = 6 km** (`19_PLANET_SCALE.md`, DECISIONS D2-085, 01 §2, 13 §11, roadmap W0). The ground is
  the same ground at any size: the planet's geophysics in geo-lengths of a 10 km sphere (`PlanetState.GeoRadius`, `GeoScale`,
  `GeoAreaHa`, `GeoCellSize`); living laws per hectare (`AreaScale`) and real-size patches (`Patch`, `PatchRings`); first villages in
  metres; new homes on dry lots (`Settlement.NextDryLot`); the view relative to R (`Ground.ScaleTo`, CameraRig framing, TerrainLod
  MaxLevel, Sky haze, CloudView shell). 177 tests at 6 km (eight 10 km premises restated); dotnet and Mono probes identical (gen
  392cf8631e08dc36). Old saves keep their radius. Tools: headless `--radius M`, `run --scale` (`ScaleReport.cs`, `Core/Diagnostics/
  ScaleTally.cs`), shots `--radius`, multi-world shot folders named by save folder. Study data: `tc2/out/scale/` (`table.txt`, runs,
  saves `r<R>_s<seed>/world_y100|300.tc2`); shots `tc2/out/shots/scale1`, `scale2` (contact sheets `sheet_*.png`). Scratchpad (session 5):
  `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\a14b045d-19c2-46a3-b3ef-213aae01eceb\scratchpad`
  (`scale_runs.sh`, `scale_table.mjs`, `sheet.py` contact sheets, `runs.sh`, `bin_b`).
- **Visual slice 1 of D2-084** (session 4's code: stands, palette, relief, rivers, tree kinds) compiled for the first time in the scale
  shots and is committed with D2-085; it is not yet judged on its own - judge it on 6 km.
- **Known bugs, each its own calibrated slice** (they change COARSE): COARSE hunting skill and trips never updated (`CoarseDay`, since
  D2-063); a COARSE disease catch that falls on a child is dropped (`Disease.Visits` picks among everyone present), halving the sickness
  visits carry home.
- **D2-084 slices on 6 km, committed**: the multi-age baseline (`tc2/out/shots/ages6`: history reads at orbit and region); D2-086 villages
  keep their shape (`Homes.FreeLot`, d775460); D2-087 homes the sea takes are lost (`Homes.SeaTook`, `HistoryKind.HomesDrowned`) and D2-088
  one shore for sim and view (`Core/Planet/Shore.cs`) with the people pixel floor and a `street` view (1625d2f). Acceptance worlds:
  `tc2/out/scale/r6000_s{42,7}_ages2/` (0/25/100/300/1,000, compact lots), `_ages3` (seed 42 with drowning), `_shore` (300 years, all
  rules). Shots: `ages6b` (seasons: summer `regiond18_10`, harvest `regiond14_10`/`village_10`), `drown1`, `shore1`.
- **Next**: (1) farmland beyond the crop stage (all a village's fields share one stage; fallow and pasture where state allows - check
  what Core knows before drawing anything: no theatre); (2) rivers and corridors (hairlines now; the network is real); (3) settlements as
  places (palisade ring vs the compact village; the square, yards and paths); (4) wildlife and herds readable; (5) scars; (6) orbit;
  the great beasts' viability decision; far views at their own local time (ShotRunner: an hour at the view's place); then the Web build
  at 60 fps (run `unity.mjs configure` first: River.mat).

## Session 4 (7 Oct 2026, from 00:16 IST) — closed into session 5

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-084)**: finish D2-083, then **the world made legible** is the priority (`18_WORLD_READABILITY.md`,
  roadmap phase W): audit -> composition -> forests -> rivers -> biomes -> terrain -> settlements -> life -> scars -> orbit ->
  multi-age acceptance -> browser performance. No simulation theatre. Ordinary low-priority runs need no permission (CPU/RAM ~50 %).
- **D2-083 (DEEP calibration) - working tree, uncommitted**: the step's mean ration (RationStep) removed (first day's ration);
  a pressing need's hours given each day of a step (`AgentSystem.CoarseDay` free); each worker of a step chooses on the ground as the
  step's earlier workers left it (`WildFood.Retake`, after each wild trip when days > 1); a worker's own take walked through the step
  (`WildFood.MidTake` in Hunt, Fish, Gather when days > 1). Rounds: h (no RationStep; seed 7 only), i (+ need-hours: wild hours
  2.17 vs 1.40 - overshoot, the woods overdrawn), j (+ Retake; six seeds, `scratchpad/verdict_j.txt`: per-person rates at par,
  people 0.85/0.87 at years 300/400 n.s. - seed 4 founded fewer villages, few epidemics -, hunting +34 % t 3.5, out for need +9 % t 7.1,
  fish -19 %), k (+ MidTake: seed 7 worse - more hunting, more out for need, famines 387 vs 312 - **reverted**; the working tree's
  Core is round j). D2-083 is written up as closed in DECISIONS/04/PROGRESS with round j's evidence and residual leans.
  **SESSION STOPPED 7 Oct ~01:04 at Arjun's request (usage limit). NOTHING COMMITTED since 6ad57fb.** (Session 5 did all of the
  list below: D2-083 committed as 22cad8d, the visual slice with D2-085.) Still to do, in order:
  (1) `node tc2/scripts/headless.mjs test` (all tests + the new `DeepTimeTests.EachWorkerOfAStepChoosesOnTheGroundTheStepLeft`; the
  repo's bin holds the reverted round-k build until it rebuilds); (2) COARSE hashes of seeds 42/7 must still be 55cfd96b471db300 /
  8c3e14c324165015 (one-day arithmetic untouched); (3) DEEP speed on a quiet machine (no --profile, nothing else running; round f:
  DEEP 42 3.17 y/s vs COARSE 1.80, DEEP 7 4.28 vs 2.97) - add to D2-083 and PROGRESS measurements; (4) commit D2-083 (stage Core
  AgentSystem/Settlement/SettlementSystem/WildFood, Tests DeepTimeTests, docs 04, 17, 18, DECISIONS, PROGRESS, SESSION_HANDOFF,
  NEXT_SESSION_PROMPT, headless Commands.cs `--snap`); (5) then the visual slice below (compile in Unity via shots, fix, judge, commit
  with its .meta files). A leftover round-k run of seed 3 (`deep3_400k`, tc2bin21) may have finished after the stop - ignore it.
  Scratch files: `scratchpad/verdict.mjs` (now with mechanism tallies), `pairsj.json`, `verdict_j.txt`, binaries tc2bin19-21.
- **Visual slice 1 (D2-084), code written, NOT yet compiled in Unity or shot**: woods as stands and glades (`Canopy.Stands`,
  StandNoise/StandRank/StandCdf, the shader's StandRank; trees and cover follow), open-land palette from climate and soil water
  (`TerrainLod.CellColour`), `Ground.Relief` (valley/ridge index: wet valley floors, conifers to ridges), distant relief lit steeper
  (Terrain.shader normal), haze ~40 % thinner, tilt-shift a hint (Sky, TiltShift.shader), **rivers** (`Rivers.cs` shared shape,
  `River.shader` ribbons widened to >= ~2.6 px far off for larger rivers, wet banks and riparian strip in Colourise, Land.y = river
  nearness, woods keep to banks, reeds at the edge only), **trees** (`ProcMeshes.ForestTree` 7 kinds x 3 variants: round, tall,
  wide, birch, willow by water, spruce, pine on dry/coastal land; crowns of shaded lumps), shot tools (`ShotRunner -tc2load F1;F2`,
  `SimHost.LoadWorld`, views `river`, `town`, `scar`; headless `run --snap Y1,Y2` -> `tc2/out/ages/sS/world_yY.tc2`), River.mat in
  ProjectSetup (run `unity.mjs configure` before a Web build). Next: `unity.mjs shots --tag leg1`, fix compile errors, judge.

## Session 3 (6 Oct 2026, from 17:57 IST) — closed into session 4

### Exact state
- Commits: 1156d3d D2-081 (the woods as masses, cover, villages' signs) - then D2-082 (Web: cover gathering cached, catch-up at orbit,
  capped builds; `s3webb` 6/6) and F8 slice 1 (DEEP) committed together after it (see git log).
- **F8 in progress - DEEP calibration (UNCOMMITTED fixes in the working tree, all one-day paths unchanged - 175 tests passed after
  the first four)**: the first 400-year comparisons (`tc2/out/deep/coarse{42,7}_400.txt` vs `deep*_400.txt`, `deep*_400b.txt`) showed
  DEEP biased: population -15..-37 % at year 200, cattle x3 and pigs x0.2, relief x0.5, wild hours +10 %, scouts x0.75, raids/claims up.
  Fixed (each a "step must keep its days" rule, now in 04 §4.3b): claims one a day (Conflict.Claims), relief window, scouts every six
  days (`Migration.ScoutsDue`), beasts' condition/hazard/milk/conception walked day by day (`Livestock.DeepBeast`), grazing not capped
  at one day's offer, mast per cell capped, catch caps 1-(1-s)^K (`WildFood.Most`), the least wild trip a quarter-hour a day, the
  store seen as days lived hold it (`Settlement.Ahead/AheadFrom/AheadTo`, `StoreSeen`; sim version 32; hashed only when set), a
  mother may conceive from the day her nursing ends / her 16th birthday, a girl marry from her 18th (`DaysUntil`), crops grow on each
  day's place on the season curve (`CropDay(.., k)`), hearths and pasture take the step's middle day. Headless `run --annals F.csv`
  writes the annals (yearly people, births, deaths by cause...). Scripts: `scratchpad/calib.sh` (env SEEDS, MODES, TAG, BIN),
  `calsum.mjs DIR c d seeds` (paired totals), `annals.mjs DIR coarse deep _400TAG seeds` (rates per 1000 person-years by century).
  Round 2 (`*_400d`, COARSE hashes of 42 and 7 identical to before: 55cfd96b471db300, 8c3e14c324165015) still showed DEEP biased
  (hunger/famines up, wild hours +33-50 %, conflict up on 42); round 3 fixes (all in the working tree): culling to the pasture as
  the step leaves it, death hazard day by day and conception on `Person.StepHealth`, the store-empty check and the wild urge on the
  store seen (a provisional `Ahead` right after Food), the step's mean ration (`Settlement.RationStep`), the smith's cap, a calm
  step never spans a plant pulse (`World.NextPlantTick`, derived), the woods at the step's middle (`Livestock.Rooting`,
  `WildFood.MidStep`), per-person derived constants (`Person.Derive`: build, copying scatter - exact). Seed 33 (60 y, small):
  DEEP wild work now matches COARSE (0.141 vs 0.147 h a working person-day; was 0.115). Draft decision: `scratchpad/d2083_draft.md`.
  Tools-only tallies: `SettlementSystem.Unrests` (why not calm), `AgentSystem.Hours` (COARSE hours by use, the wild choice by kind
  and by day of the plant cycle), printed by headless `run --places`. `scratchpad/calib2.sh` runs "seed:mode" items (BIN, TAG).
  Later in round 3: a step's later days grow the crop after its weeding (seed 1 crop 0.853 -> 0.871 against COARSE 0.884), harvest and
  leaving tallies (`SettlementSystem.Harvests`, `Migration.Weighs`). D2-083 written (in progress); 176 tests pass (DeepTime 7 after the
  last rule). Round 3a results (`*_400e`, tc2bin9): seeds 42/7/1 summarised in D2-083's evidence.
  **Blocked** (PROGRESS "Blockers"): the classifier refused to stop two leftover workers of this session and to start the final batch;
  they have since finished (round 3a on six seeds, D2-083's evidence: no early-growth bias, per-person rates at par, but -15 % people at
  year 400 on all six with less food: pigs x0.07, fish, herd meat, relief). After it: pigs root day by day (`Livestock.Day`,
  `MidStepMast`), a demography tally (`SettlementSystem.Demo`) and the wild decision's shortage terms - seed 1 DEEP's rations ran short
  16x as often from 9 % more people (an early lead crossing a threshold, not a rate). 176 tests pass. **Next**: with permission, DEEP 42/7/1-4 with the newest scratch build (TAG f), compare
  against COARSE (42/7 `*_400d`, 1/2/3/4 `*_400e`) with `annals.mjs`/`calsum.mjs`; open question: DEEP's early growth runs ahead on
  small worlds (seed 1 +9 % people at year 100, seed 33 +17 % at year 60) with three times the households leaving - look at
  marriages and births per person-year by age next; then speed on a quiet machine (never with parallel runs: everything ran 2x
  slower). Known COARSE bug left for its own slice: hunting skill and trips are never updated in `CoarseDay` (the line sits inside a
  comment since D2-063).
- Tools added: `tc2/scripts/webcatchup.mjs` (a day away's catch-up time by camera height and layers), the bridge's `catchupalt`,
  `covershadow`; `scratchpad/perfviews.sh` (webperf view by view), `scratchpad/calib.sh`.
- The slice (for reference) - **D2-081 (the woods as masses, ground cover by biome)**: `Canopy.cs` (plain-chance
  crown numbers - the tile repeats, so nothing in it may pattern -, radius 0.52-0.94 with few broad old crowns, height 0.64-1, K
  fitted on the layout (`Fit`), the cover texture `MakeCover` - whether a standing crown covers each texel at four thresholds, its
  mips the share covered -, `CoverMean`); `Terrain.shader` (`CoverAt`/`CoverOf`: the far woods are the area-average of the near canopy -
  clumps lit by a bump from their cover, the floor shaded by clumps sunward -, the crowns hand over at 1.3-3 m a pixel, the tile
  hands over to its mean at 3.5-7 m a pixel, leaves olive by the climate's dryness `i.land.x`); `TerrainLod.cs` (a fourth vertex
  stream `Land` = dryness, meadow, bare earth or rock, beach; clumps of ~20-45 m in the forest share where woods are open; tree
  colour groups with two dryness bits (ushort groups, 512 tints); ground cover by what the land is: meadow grass, flowers in their
  season, shrubs and ferns at wood edges and under the woods, heather on cool wet open land, cushions and stones on tundra, straw
  and stones where dry, dune grass on sand above the tide, reeds, boulders; cover re-placed with the seasons); `ProcMeshes.cs`
  (Shrub, Fern, Cushion, Stone; low icosahedron blobs); `WildlifeView.cs` (herds up to 3 km altitude, up to 4x at distance);
  `Sky.cs` (cover texture, K, mean as globals).
  Later in the slice: the far cover read a second time turned and scaled (`fb`) and stands (`Tri(.., 1/1700)`); tree kinds
  (`ProcMeshes.ForestBroadKind`: tall, spreading, birch) and young trees after fire; beaches and the sea floor per pixel with a metre
  of noise in the height read (the coast staircase); `WorldObjectsView` hut and camp smoke, woodpiles (`ProcMeshes.Woodpile`), the
  watch fire, night lights (`EmitLights`); `ShotRunner` `view+D@H`; `BuildScript` `-tc2Jobs`; the bridge's `off cover`.
- Shot rounds: `tc2/out/shots/woods1` (tile clumps read as a grid from orbit) -> `woods2` (grid gone, far woods too smooth; night
  lights) -> `woods3` (far texture and stands; seasons: a tropical forest stays green - no autumn above ~20 C mean, right) ->
  `woods4` (beach per pixel, edge shrubs to 480 m, flowers readable) -> `woods5` (sea floor per pixel) -> `woods6` (waterline noise).
- Look items seen and left for later: the surf's foam repeats as V shapes near the camera; the water sphere's own edge against
  coarse terrain can still step far off; shrubs at a wood's edge are one size; flowers only from ~100 m; animals at the village view
  are still specks (herds read as groups only when white); kitchen gardens (not simulated: not drawn).
- Machine: shared and loaded by others (see PROGRESS session 3); wait with `scratchpad/quiet.ps1`, run with `scratchpad/lowrun.cmd`
  (scratchpad = `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\1e4f3b75-2f3f-4d81-b3ff-5dffc75a3c54\scratchpad`).
- No TC2 process running between shot rounds.

## Session 2 (5 Oct 2026) — closed (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits since the last checkpoint (newest last): 314fdd9 techniques (D2-062) · c8e9c23, 07b38be materials (D2-063) ·
  6bacfaa copper, F4 closed (D2-064/065) · 27d2a53 sickness (D2-066) · e01a933 trade and relief, F5 closed (D2-067/068) ·
  b78117d deeds and taking (D2-069, F6 slice 1) · 310ab38 sickness at scale (D2-070) · ceadd36 the watch, blows and injury
  (D2-071, F6 slice 2) · 0f2adde answers, news and taking back (D2-072, F6 slice 3) · 7121f7e raids (D2-073, F6 slice 4) · 41f5756 the palisade (D2-074, F6 slice 5) · ff6fab8 tribute and lordship (D2-075), F6 closed
  (D2-076) · 3d8f7b6 Web after F6 · the commit with the History Book (D2-077), F7 closed (D2-078) and the direction to make the
  living world visible (D2-079).
- Phases: F0 done; **F1 to F7 closed** (D2-043, D2-046, D2-060, D2-065, D2-068, D2-076, D2-078). 169
  headless tests green; `headless.mjs test` runs the float lint first. Sim version 30.
- Determinism: dotnet, the editor's Mono and the Web build (IL2CPP, WebGPU) are bit-identical at D2-077 (gen 9b6482ca9f9f606f,
  `tc2/out/webtest/s2webg`: 6/6).
- Sickness at scale is fixed (D2-070): seed 42 holds 12,000-16,000 people from year 270 to 400 and seed 7 4,600-5,400
  (`tc2/out/society/sick42_400.txt`, `sick7_400.txt`; the run report lists the deadliest sicknesses). Saturated worlds are
  famine-regulated (~100 hunger deaths a year) and take from each other's stores often (~30-75 loads a year): F6's pressure.
- Nothing running. Uncommitted legacy TC1 work may exist in the tree and is NOT ours: `game/html/engine_template.html` and
  `docs/DECISIONS.md` (D-227). Never stage them in [tc2] commits.

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` (~110 s for all 101) |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out DIR] [--why] [--profile] [--coarse]` |
| Villages over time | `run --seed 42 --years 200 --report-days 2400 --tier 9 --coarse --places --profile` (per village: people, fields, granary, ration, wild grounds, **stock and pasture** offered/wanted, herds; then the stock line, **deaths by kind and cause**, the profile with `daily.*` parts) |
| Planet over time | `planet --seed 42 --years 3000 --every 9600 --nopeople --coarse [--tect] [--volc] [--eco] [--csv F] [--ecocsv F] [--map DIR]` (each report line ends with elapsed seconds and the number of kinds) |
| 10,000-year runs | copy `tc2/headless/app/bin/Release/net8.0` to the scratchpad, then `dotnet <copy>/tc2.dll planet --seed 42 --years 10000 --every 4800 --coarse --nopeople --tect --volc --eco --csv tc2/out/f1/run_42X.csv --ecocsv tc2/out/f1/eco_42X.csv > tc2/out/f1/run_42X.txt` (~22-25 min each) |
| Climate alone (fast) | `node tc2/scripts/headless.mjs climate --seed 42 --years 4000 --every 200 [--co2 560]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save] [--coarse]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` / `node tc2/scripts/unity.mjs test` |
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low,volcano,...,coast,seals] [--hour 8] [--days D] [--seed N] [--erupt VEI] [--quake M] [--storm WIND]` -> `tc2/out/shots/T/*.png` |
| Asset kit | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--preview tc2/out/kit]` |
| Web build / serve / test | `node tc2/scripts/unity.mjs build-web [--webgl2]` (5-9 min) · `node tc2/scripts/serve.mjs` · `node tc2/scripts/webtest.mjs --tag T [--webgl2]` |
| URL params | `?fresh=1`, `?seed=N`, `?speed=I`, `?absence=24h`, `?view=close|village|region`, `?gfx=webgl2` |

### Important paths
- Unity editor 6000.6.4f1: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`; bundled dotnet in `.../Editor/Data/DotNetSdk/dotnet.exe`.
- Core: `unity/TinyConsequences2/Assets/TC2/Core/{Foundation,Scheduling,Simulation,Planet,Places,Agents,History,Persistence,Diagnostics,Worldgen}`;
  places: `Places/{Settlement,SettlementSystem,WildFood,Construction,Sites,Migration,Herds,Livestock}.cs`.
- Game view: `Game/View/{WorldObjectsView,WildlifeView,SeaLifeView,...}.cs`; kit models `Game/Resources/TC2Models/*.fbx` (from `tc2/art/kit.py`).
- Tests: `Tests/Core/*Tests.cs` (WildFood 11, Migration 5, Livestock 11, ...); `TestWorlds.Audit` checks people, queues and livestock lists.
- CC0 Quaternius farm animals (Blend/FBX) in `assets/3d-models/animals_quaternius/` (not used yet: the kit's own quadrupeds keep one style).

### Pitfalls found (session 2 additions; session 1's list still applies - see git history of this file)
- Grep build output for `error MSB` too: a locked DLL fails the copy with no `error CS`.
- A long headless run on the repo DLL locks it: run long jobs from a scratch copy.
- `planet --nopeople` used to found one village (`Math.Max(1, cfg.Settlements)`): runs before 6b0903d labelled planet-only may have people.
- Lookups that walk a whole store per agent-day (all herds, all settlements) are invisible at 6 villages and dominate at 30:
  index per village; profile a 200-300-year coarse run (`--profile`, `daily.*` parts) after adding any per-day lookup.
- `CellsWithin` keeps its answers per (cell, range) (the grid never changes); results are the same list in the same order.
- The village's day (`SettlementSystem.Daily`) runs at local midnight: `P.Temp` then is the night's (diurnal swing 5 C on land).
  Use `PlanetSystems.DayMean(c, PlanetSystems.Season(t))` for a day's mean.
- A test's premise can be undone by the world before the mechanism runs (hunters brought food home before the herd's day):
  call the system step directly when the test is about that step.
- A planet step (tectonic, evolution) can fall inside any test's hour: filter history checks by the kinds under test.
- Heredocs with apostrophes break: patch scripts go in `scratchpad/patch_*.py` written with the Write tool.
- `headless.mjs test --filter A|B` matches nothing (no alternation): run the filters one by one.
- Kit re-exports touch every FBX (new timestamps): restore the unchanged ones with
  `git diff --name-only -- .../TC2Models/ | xargs -r git checkout --` before committing; a glob also catches new untracked files.
- Any rule the simulation and the view must share (a herd's ground, the frost that pens the animals, which seals show) lives
  in Core or one static helper both call - never two copies.
- Test mechanisms, not demography: "mostly children herd" failed because only 1 of 19 households had a child of 8-15.
- **No float arithmetic in TC2.Core** (D2-057): `a + b` on two floats rounds differently under Mono; write `(double)a + b` and cast
  to float only when storing. `headless.mjs test` runs `tc2/tools/floatcheck` first and fails on any.
- A store whose ids come from a shared counter (process ids: herds, parties, fires, storms) is sparse: check `PagesHeld` and the
  cost of a pass over it at scale (D2-055).
- A new daily need (meals, water, fuel) changes what a "stocked" premise means in old tests: restate the premise (keep the store
  stocked), do not loosen the mechanism (D2-056).

- **Another Claude session may share this machine** (6 Oct: one on another project, with a gstack browse daemon whose headless
  Chromium renders WebGL with SwiftShader on ~7 cores). Its processes are not ours: never kill a process you did not start -
  check its parent first (`Get-CimInstance Win32_Process`) - and measure timings only when the machine is quiet.
- Claude Code stops background jobs when the machine runs critically low on memory (6 Oct: other sessions and their browsers
  shared it; the Web build and the local server were stopped). Check free memory before the Web build (it wants ~4-5 GB).
- The History Book in the game: B or the Book toggle; `?book=1` opens it; a fresh world has little history - reload a saved one
  with `?absence=24h` to live ~96 years first.

### Next steps
1. **F8 - DEEP calibration (D2-083, in progress)**: Arjun allowed the six-seed batch; round f (`*_400f`, tc2bin17 = commit 6ad57fb)
   done 23:51 - `scratchpad/verdict.mjs scratchpad/pairsf.json` (paired log-ratios, t over six seeds): population at every
   century, births and deaths per person-year, hunger and famines, wild food, herds, relief, households leaving and conflict at par;
   leans not significant (wild hours per person-year +17 %, t 2.0; smelting; infections -25 %); one metric flagged at 5 % (foundings
   per person-year, totals at par - ~2 false flags expected among ~50). COARSE 7 rerun with the tallies (`coarse7_400g`, hash
   unchanged) showed the one real remaining bias: the step's mean ration (`Settlement.RationStep`) walked the store down by the
   need alone - whole steps marked short (out for need on 43.5 % of person-days against 32.8 %; urge 0.072 against 0.036). Removed
   in the WORKING TREE (uncommitted; built as `scratchpad/tc2bin18`; not yet tested): the step's work reads its first day's ration.
   **Next**: retest DEEP 7 against `coarse7_400g` (`QUIET=1 BIN=tc2bin18 TAG=h calib2.sh 7:deep`), then the six seeds (TAG h),
   full tests, D2-083's rule 2 text (no mean ration), commit; then DEEP's speed on a quiet machine. The 23:56 memory-pressure stop
   (Claude Code) killed the local game server, the COARSE 7/42 tally chain (COARSE 42 kept running to finish `coarse42_400g`) and the
   DEEP 7 retest - restart only when Arjun asks.
2. **The COARSE hunting-skill bug - its own calibrated change after D2-083** (Arjun, 6 Oct: not part of D2-083 - changing COARSE
   moves DEEP's reference): in `AgentSystem.CoarseDay` the hunter's skill growth and `HuntTrips++` sit inside a comment since D2-063
   (`s.Hides += hides;   // (D2-063) p.SkHunt = ...; w.Metrics.HuntTrips++;`). Restore them, recalibrate COARSE against ACTIVITY
   (`CoarseAndActivityLiveOffTheWildAlike` and the wild-hours fits), record new COARSE baselines (hashes change), rerun DEEP vs COARSE.
3. **F8 - speed**: DEEP's per-step costs (coarse-day, learning, demography, conflict), the planet's share (weather, vegetation +
   ecology every 12 days are needed for seasons - look for exact speedups rather than coarser cadences), housekeeping; Ancient and
   Deep time measured in the browser (`webcatchup.mjs`); F8's done-when: 5,000 years in minutes.
4. **Web performance**: a day away catches up in 48-49 s (D2-082; 41-42 s before F5-F6) - profile the browser's catch-up.
5. **The look backlog (D2-081)**: the surf's foam repeating as V shapes near the camera, the water sphere's edge stepping far off,
   one size of shrub at wood edges, flowers only from ~100 m, animals specks at the village view, kitchen gardens (not simulated).
6. F7's carried-forward items (D2-078): Your Actions (with F9), timelines, news by incident, biography and polity pages, ages named
   apart. F6's (D2-076): lordships that hold, leaders, alliances, captives, gates and stone walls, bands of several villages,
   beasts as loot, the violence rate, the view of lordships and bands. F5's (D2-068): boats between coasts, roads worn by use,
   fairs, salt, credit, relief by kinship. The sea's calibration (D2-053).
