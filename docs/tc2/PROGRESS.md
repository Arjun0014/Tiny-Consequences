# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

**Phases: F0 done; F1 closed (D2-043); F2 closed (D2-046); F3 closed (D2-060: D2-042, -045, -047 to -050, -052, -056, -058, -059); F4 closed (D2-065: D2-062 to D2-064); F5 closed (D2-068: D2-066, D2-067); F6 closed (D2-076: D2-069, D2-071 to D2-075); F7 closed (D2-078: D2-077); Web checked on WebGPU (s2webf, after F6) and WebGL2 (D2-051); Mono determinism repaired (D2-057).**

- **Session 26 (25 Oct 2026, from 21:20 IST; Control Room) - rain landed; the harrying in its placebo; C7.2 stone walls and the memory pass built.**
  (1) **F9.5 the rain landed** (main b9ab94e, sim 99): its checks (rn1 ok) and its uncast control rerun (cut off at the last stop) = `_pv1` byte for byte.
  (2) **C8.1a the harrying, ten seeds** (`results/cmp_bn1_hy1.txt`, `lean_*`): control `--oldharry` = `_rn1` byte for byte; hunger x0.80, raids x0.80,
  wounds x0.90, but people at 300 x0.91 (t -2.5) with sickness x3.9 (t 3.0) - the pooled rise is seeds 4 and 42's epidemics (+3,890, +3,318 of +8,612;
  deaths a person-year x1.018, t 1.1): a lottery of rare spillovers (`Disease.SpillPerDay` 6e-6) after the worlds part - its **matched placebo** (`_hyp`,
  `--oldharry --nudge` at each world's parting year) decides; queued. (3) **C7.2 stone walls built** (`wip/c72-stonewalls`, worktree `C:/Game Dev/tc-stonewall`,
  sim 102; draft `tc2/out/tools/s26/d2183.md`): a walled village with rock within a quarry's reach weighs a dry-stone curtain (three quarters of its losses
  kept over the century it stands, and the timber no longer felled and mended, against cutting, carrying and laying 150 loads a metre); its people lay it
  stretch by stretch after every other work from the quarry's rubble; stone does not rot and triples the defenders' ground; the stretches stand as an old ring
  when the timber falls or the town outgrows them; drawn as a curtain from the gates outward with towers at the gates and every ~45 m; `--oldstonewalls`,
  `--stonetally`, `place --stonewalls` tells the rings in stone, shots `--village stone`. StoneWallTests 5/5 and eight neighbouring classes 0 failed.
  (4) **The memory pass built** (`wip/memory`, worktree `C:/Game Dev/tc-memory`, sim 103; measured first in `tc2/out/tools/s26/memory.md`): at seed 2's
  year 1,000, 214k of the ledger's 509k events were the night's deeds and the ordinary kept for ever by their kinds' size (a sworn man's year of food scored
  14), 148k of them led to nothing; the registry named all 310k people who ever lived, 193k by nothing kept. Now past the Local window those kinds are kept
  for what they led to, and the names nothing kept can show leave the registry - behaviour-neutral (`--oldcompact`; MemoryPassTests 3/3: twin worlds with
  the pass off and on live the same 75 years, 4,579 names checked, none lost). (5) **The view**: a burning coastal cell's flames stood in the sea (the meteor
  shot) - flames on dry ground only (main ee11391); a shot view may set `ShadowLite` (main 4919446). Seen: the realms' war spears read (edged pale).
  The queue (`Q` as below): the ways' build, control and one full suite over everything to land (`0_zg_*`), the placebo (`0_zfa_*`, `0_zfb`), the stone
  walls (`0_zh_*`: build, control, ten seeds `_stw1` against `_hy1`), the memory pass (`0_zi_*`), seed 7's third run (`K_b_ac3_s7`); then the fourth acceptance
  run on the final rules (`tc2/out/tools/s26/make_ac4.sh`). SESSION_HANDOFF "Session 26".
- **Session 25, fourth context - END (~17:45 IST, stopped at Arjun's word)**: since the entry below - the streamed save landed (the heap of a year-1,000
  world 1,198/1,297 -> 955/1,054 MB loaded/after the views), the reload fault traced to the Web test's harness (it read a stale `saveOk` before the game had
  done its `save`; the bridge publishes `saves`, the test waits for it to rise) - **Web `s25web6` 8/8**; D2-182 appended; `shadowlite` measured x1.21 at the
  region view (off by default, to judge in shots); seed 4's third run (65 -> 81 places over 300-990); the state by part (`run --statesizes`: the history
  ledger 64 of 107 MB at year 1,000). Rain and the harrying carry main and wait to land (rain's checks were running at the stop); the queue stops after them.
  SESSION_HANDOFF "Session 25, fourth context - END".
- **Session 25, fourth context (25 Oct 2026, from 14:10 IST; Control Room run #78 session 3) - the siege measured and set aside; C11.1b built; the frame
  pass and two Web fixes on main (no sim change).** (1) **C8.1b the siege measured first** (`run --siegetally`, `wip/siege-tally`; `tc2/out/tools/s25/siege/design.md`):
  of the bands a walled village's palisade alone held off - kept home or broken on it - **not one could have stood a sally by the walled village's people
  on open ground** (bands of 13-35, strength 20-33, against towns of 150-330 with whole fit strength 109-310; seed 4 years 0-150, seed 2 at 300, 600, 1,000).
  Stores are years in the early world, 35-64 days in a full land. Set aside: a siege needs a host many times a raiding band, and this world raises none -
  the asking coerces (all askings bowed). (2) **The third acceptance run** (`_ac3`): seed 2 done (1,000 years in 29 min; 178 homes burnt in 35 villages;
  the wasting pox of 719-748 killed 3,737; Reaikford's realm 48 villages at 741; book `accept/book3_s2_y1000.md` reads as history) - **the map still freezes
  after ~300** (55-56 places 290-630, 62 at 1,000; founded/abandoned 0 in most ages); seeds 4 and 7 queued. (3) **C11.1b a people's ways carried and copied**
  (`wip/c111b-ways`, worktree `C:/Game Dev/tc-bways`, on `wip/harry`, **sim 101**; draft `tc2/out/tools/s25/ways/d2181.md`): measured first - every one of
  6,615 buildings of seed 2's year-1,000 world roofed in its village's founding way. Each household carries a way (the groom's house's; founders their new
  village's; joiners their own); a couple builds as its village builds (its first household's way) 0.6 of the time, a bride from elsewhere brings one of her
  ways 0.1; a tributary's first household takes one of its lord's a year 0.1; homes raised and rebuilt in their household's way, works as the first household
  builds; the book's Peoples count the roofs standing and a realm's chapter tells its houses; the building page tells a roof's way; `--oldhouseways`
  (`--oldways` is D2-160's - the first control ran both and differed). BuildingWaysTests 5/5 and eight classes 0 failed 0 skipped; with the ways on seed 4's
  annals = the harrying's byte for byte (ways change no history). A first marriage rule (one trait of the first household 0.3, every bride 0.15) made every
  village a mosaic (seed 4 at 300: 15 % of households as their founders, 62 of 63 mixed) - revised; its measure and second control queued (`0_y`).
  (4) **The frame pass** (`14` §7; main 3e3f64c, presentation): layer costs on the year-1,000 world (`s25web3`, 12,698 people, ~155 ms a frame at the region
  view, ~225 at the village): buildings x0.53 (~76-106 ms), people x0.75 (~40-66 ms), shadows x0.63-0.73, terrain x0.90, trees, cover and the HUD ~x1.
  People beyond the planet's limb or outside the view are no longer posed (all ~12,700 were, every frame); villages wholly beyond the limb are not built
  (rebuilt at once when the camera moves). **The heap**: 1,297 MB loaded, 1,491 MB after the layer pairs (headless managed 304 MB): the memory pass next.
  (5) **Two Web faults** (`s25web3` 6/8): the reload test loaded an older save - `SaveNow` never unset `saveOk` while a save was in flight; and the power
  click fell on the place page left open (a click over a page is the page's; the aim ring is now drawn only over open ground; the test closes the pages).
  Also committed: Unity's .meta files for the 71 sounds and for Hud.Powers/Hud.Title/AudioView. Queue: `0_w` (Web s25web4: test, cull on/off pairs),
  `0_x` (heap by age), `0_y` (C11.1b control 2), then K_b s4/s7, K_ba, K_d-K_f, L_a (rain), M_* (the harrying), N_a (ways over 1,000 years).
- **Session 25, third context (25 Oct 2026, from 07:34 IST; after a compaction) - C3.6g the land between villages landed on main (b516bb1, sim 95;
  D2-176).** (1) **Measured first** (`place --margins`, new): the handoff's marginal-land hypothesis is refuted - beyond the 1,900 m spacing the land is
  ice, cold or dry (lowering a site's bar adds 0-1 villages a world); **the room is between villages**: 1,164-1,425 ha of free arable (15-20 % of the
  world's), inside the spacing and beyond every 950 m fields' reach, room for 25-40 daughter villages in seeds 2 and 4. (2) **The rule**: leavers who know
  no free good place and find no village to join go together (2+ households, 5+ people) to found a daughter village on their own village's margins - houses
  beyond every village's fields (1,053 m), nearer the mother than any other, worth 0.25 with that room kept, a way on foot; told as a daughter's in the
  history, the gazetteer and the book; `--oldmargins`, `--margintally`. (3) **A flaw found and fixed on the way**: joiners never counted those already on
  their way to a village, so new daughters took in up to five times what their land fed (land/need 0.36-0.42) - a village's spare land now counts them
  (`Migration.Coming`). (4) **Evidence**: MarginsTests 4/4 and seven classes 0 failed 0 skipped, floatcheck clean; control `--oldmargins` = main byte for
  byte (annals and state hash, seed 4); ten seeds (`results/cmp_pc1_mg2.txt`): founded x2.8, people at 300 x1.19 (t 3.2), births a person-year x0.99,
  deaths x0.96, hunger x0.59, greatest realm x2.06, the sim's cost x1.34 - the lean explained by the land farmed (free arable between villages 1,164 ->
  139 ha); 1,000 years (`_ac2`, seeds 2/4/7): the infill done by ~300 (57/60/20 places against 28/31/10), people +25-33 % at 1,000, then slow change
  (12/21/2 foundings and 2/4/1 abandoned over 300-999); a 1,000-year world costs x2.2-2.5 the time. (5) Still lacking for 25 §6: villages deserted in
  crises and refounded, ruins - the next slice, measured first.
  (6) **Measured: small villages live better, not worse** (`run --sizetally`: hunger 0-0.7 a thousand person-years in villages under 25 against 4.1-4.4
  in 200-399; stores 1.9-7.5 years against 1.0-2.0) - no desertion rule (it would be scripting). (7) **C8.0 the burning-in landed** (39bfa3a, sim 96,
  D2-177; measured first with `run --burntally`: killings, not wounds, bound it to ~1.8 homes a raid): a band that holds the field fires the homes of
  those who killed its people; the vengeance answers the killing; ten seeds at par, 5-45 homes burnt a world in 300 years. (8) **F9.1-F9.3 and F11.1
  landed** (0b05e63, sim 97, D2-178): the power bar (lightning, storm, quake, eruption, a sickness, a blight - aimed at the ground, cast by a click,
  back in game time from the command log), Your Actions (the book's ninth part), a pestilence and a blight through the world's own systems, the opening
  title and guide (plain address; `?menu=1`), the war spears edged pale; PowersTests 4/4; with no cast main's rules byte for byte. The shots'
  `ArgumentOutOfRangeException` traced: Unity's own editor search indexing at startup (harmless). (9) Queued (`make_final.sh`): main's full suite,
  the third 1,000-year acceptance run `_ac3`, the Web build `s25web3` (its test casts a power by a click, shows Your Actions and the title), layer costs
  of a great world (people, buildings, trees, cover, shadows, terrain, HUD), shots of village #4 at 300/600/1,000 and the realms at 1,000.
  (10) **F9.4 the meteor and the drought** (be8ef8e, sim 98) and **F10.1 the world's sound** (f47ce59) landed - D2-179; main's full suite at sim 98
  **473/0/0**. War measured on `_ac2`: between realms now, half broken on walls. Built, waiting to land after the final run: **F9.5 the rain**
  (`wip/rain`, sim 99) and **C8.1a the harrying** (`wip/harry`, sim 100) - their checks queued (`L_a`, `M_*`). Handoff at ~14:10 with the final run
  running (SESSION_HANDOFF "Session 25, third context - HANDOFF").
- **Session 25, second context (25 Oct 2026, 23:56-~02:35 IST; Control Room run #78 session 2) - four slices landed on main (sim 94): C10.8 the asking, C10.9 the lord's death, C10.10 the lord's peace (chosen over C8.1 by measuring first), C11.1a a people's tongue; the wars drawn on the realms layer; the 1,000-year acceptance in the queue.** (1) **C10.8 landed** (731c75a, D2-168): its branch's one failing test was a premise the asking itself overtook (1b5e033). (2) **A fault in the job tooling**: `lib.sh`'s `gate` read the load into global `r` and `c`, so a job looping `for c in <classes>` ran filters like "4" and "2" and passed on "0 passed" - C10.9's first `lp1 ok` had run none of its classes; `gate` keeps them local, the gates need 1+ passed. (3) **C10.9 the lord's death landed** (aeb880b, D2-173): ten seeds against C10.8 at par in people and per-person rates, raids x0.64, wounds x0.82, bonds made x2.8 and ended x3.6, the greatest realm x1.04. (4) **C8.1 measured first and set aside** (`lordships --walls`, `run --raidtally`): refusals of the asking are rare and end in a bow; the war is feud on walled villages (raids answer blows, refused claims, takers seen; bonds' ends 1-6 %), 30-88 % of it between villages of one realm. (5) **C10.10 the lord's peace landed** (955b497, D2-174): no bands within a realm, blood judged by the common lord; ten seeds against C10.9 - per-person rates at par, famines a person-year x0.83, loot x0.65, people x1.085 over years 100-199; against its matched placebo twin x1.054 (t 2.69; the twins x1.029), explained: raids within realms gone (seed 4: 85 -> 0), war at the realms' edges. (6) **C11.1a a people's tongue landed** (33fd336, D2-175): measured first - every house stands as its village's way, all names from one speech; now each first people its own tongue, founders' tongues changed a sound at a time, places, first families and the born named in it (Stoby's line: Stoby, Finby, Tainnidal, Kothirhope...); the book's Places begins with The Peoples; seed 4's annals = C10.9's byte for byte. (7) **The wars on the realms map layer** (8098877): the last ten years' raids as dark spearheads toward the raided. (8) In the queue: main's full suite (`9_a`), three 1,000-year worlds `_ac1` (seeds 2, 4, 7; saved at 50/150/300/500/800/1,000, books at 300 and 1,000), the Web build `s25web2` over them, each age's real shots `s25m_*`.
- **Session 25 (24 Oct 2026, from 22:14 IST; Arjun's priorities: forward progress; the governance slice; a finished core simulation within two weeks;
  the planet like Earth from space; places and people identifiable; a real History Book; then god powers) - the orbit look, the History Book as a book
  and places named on the map landed on main (presentation and an observer: no sim change, sim 90); C10.8 in its last evaluation; the compile check
  repaired.** (1) **The full suite on main's tip** (e290217): 442 passed, 0 failed, 0 skipped. (2) **The planet from space** (D2-170, 3486681): the air as
  single scattering in an exponential atmosphere (a thin bright limb, the veil toward the limb, the terminator's soft penumbra and the ring of sunrise),
  clouds from the weather in two channels (rain decks with billowing edges and sun-side relief, drawn out along the bands' winds; fair-weather cumulus of
  the moist climate as puffs, a veil from afar), the cloud shell lowered and gone at the village view, a darker sea, villages' lights as points -
  judged in real shots `s25a`-`s25c` (variants per view: `Look.cs`, `"orbit~Name=value"`). (3) **The History Book as a book** (D2-171, merge 29c405c;
  `Core/History/HistoryBook.cs`): eight parts written after the fact - the Ages, Realms and Rulers (each lordship rebuilt year by year from its bonds,
  its tiers, the houses that led its seat), Wars and Feuds, Famine, Plague and Disaster (hungry times as the annals' peaks), Places, Lives, Threads of
  Cause, A Chronology; every line its record; the HUD's two-page reader; headless `book --load SAVE --md F`; HistoryBookTests 5/5; the year-900 world's
  book 85 chapters, 1,065 lines, 0.55 s. (4) **Places named on the map and hover tags** (D2-172, merge 5b91710). (5) **The compile check `unity.mjs cc`
  compiled nothing since session 23** (a shadowed variable: every glob matched no file) - found when Unity's own compile caught a broken string the check
  had passed; fixed (ea6b826): it compiles Game, Core, Editor and the tests again. (6) **C10.8's year-900 books**: realms rise and fall in turn (seed 2:
  Taildvehope 24 villages in five tiers by year 338, gone by 829; Elddal 19 for eleven years; Vaildwick, Skaenstead, Mestskouford 22 each) - a cycle of
  hegemonies; `wip/c108-demand` current with main, the book tells the asking (b808479). (7) 25 §8: the plan toward a finished core simulation.
  (8) **Web `s25web1` 6/6** (5b91710) and the HUD in the browser over the year-300 and year-900 worlds (`bookshots.mjs`: names at the views, the book's
  parts, each written in 2-271 ms); then the works named at the village view, people's given names at the closest views, names cheaper (repaint only),
  the book's buttons on its page, names clear of the HUD's bars, and **the book's Timeline page** (the realms' villages year by year, the ages, the
  people's line, the greatest moments; HistoryBookTests 5/5) - main 53cb512; DECISIONS D2-170..D2-172 appended. (9) **C10.8 decided: land** - its
  matched placebo (`7_h`, `7_i`): the twins do not lean (x1.005) but C10.8 does against its twin, people over 100-199 x0.904 (t -4.64), explained by
  century - war deaths (wounds +0.7 a thousand person-years) and fewer foundings while the worlds grow, x0.92 near the ceiling, per-person rates at
  par (`d2168.md` complete); its branch's full suite (`7_j`) was running at the handoff. (10) **C10.9 the lord's death** measured (`lordships
  --together`: 22 of 28 great realms held by the call; `--heirs`: the first at a seat dies about every 12 years) and built (`wip/c109-lapse`, sim 92,
  LapseTests 3; the book tells the lapses); its control and ten seeds queued (`8_c`-`8_f`).
- **Session 24, second context (21 Oct 2026, 01:45-~06:05 IST; Control Room run #73 session 2; stopped at Arjun's word) - C10.7a, C10.7b and C9.5c
  landed on main (3b236ee, sim version 90) with the lordships tools (3bf3d44); the realms map layer finished from real shots (D2-167); a browser OOM
  of great worlds found and fixed (D2-169); DECISIONS D2-164-D2-167 and D2-169 appended; C10.8 the asking in evaluation** (SESSION_HANDOFF "Session
  24, second context - END": the queue stopping, what is left, the next steps). (9) **C10.8's evaluation so far**: control `--olddemand` = `_i2` byte for
  byte; ten seeds - per-person rates at par, people over 100-199 x0.908 (t -2.85; twins part at years 6-30: a matched placebo is queued), bonds agreed
  x12, the greatest realm x4.2 (8-16 villages at year 300), bonds ended x21, raids x6, wounds a person-year x2.75; seed 2 over 900 years: 279 bonds made,
  250 ended, Mestskouford 18 of 31 villages at year 900, the vital rates at par - the churn and its war to judge before landing. (1) **C9.5c's third seed set** (20-29) decided the landing: pooled over the 21
  worlds that part, hunger a person-year x0.994 (t -0.16) - the first ten's lean was chance, as its matched placebo said; births and deaths a person-year
  at par; people over years 200-299 x0.982 (t -2.64), carried by seeds 9-19 and not repeated in 20-29 (x0.991, t -0.77; three worlds lower, three higher)
  - a level of the size twins spread by, not a rate; in 20-29 the C9.5c twins met larger epidemics and starved fewer (r -0.88): an island meets its
  neighbours' sickness by boat, later and larger or never. (2) **The integrated rules' 900 years** (seeds 2 and 4 lived from year 300; `lordships
  --heads --margins --could --wrongs` at years 300, 600, 900): realms grow, nothing snowballs - seed 2 built an overkingship (Lounstead took Grothstead,
  lord of five, after a raid in year 601 and Taildvehope, lord of three, for famine grain in 794: 13 of 32 villages in three tiers from year 800, unchanged
  at 900), seed 4 holds seven lordships of one to four; every bond still paid is the stronger by the odds of a first bow; successions change nothing (bond
  ends by the lord 5.0 a thousand bond-years within five years after its first person changed, 4.9 otherwise); formation still limits (80 and 135 pairs
  where one could make the other bow, against 16 and 14 bonds) - C10.8 the asking kept as the next slice. (3) A second reading of the landing branch's
  save format and hash, `Retinue`, bands and might, and of C10.8's `Conflict.Demanded`: no fault. (4) Docs: `18` §9 polities on the map (D2-167), `10`
  §12h done. (5) **The landing checked**: main = the landing branch byte for byte (seed 2, state hash 56fb1fe012804dd3), **full suite 441 passed, 0
  failed, 0 skipped**, Unity C# compiles, **Web `s24web1` 6/6**; real shots - sworn men at practice (`s24p`), Vaildwick's realm at year 300 (`s24q`). (6)
  **The realms layer from real shots of the year-900 overkingship** (`s24t`): a sky-blue realm read as lakes, a white one as a salt flat -> realms take
  only ground-safe dyes (no argent, sable, sky blue; azure blue-violet on the map); then magenta beside rose read as one realm (`s24u`) -> realms seen
  together at least 60 delta E apart (`s24v`: three realms read apart); **its frame cost unmeasurable** (new `realmsperf.mjs`: paired off/on frame
  counts on worlds with realms, x0.98-1.04). (7) **A browser OOM found and fixed (D2-169)**: measuring, the year-900 world (9,532 people) aborted at the
  2 GB ceiling - its heap grew ~500 MB at each autosave (~460 MB of short-lived save buffers a minute; the wasm heap only grows); saves now go into
  buffers kept and sized at each load (the same bytes): flat at 1,053 MB (year 900) and 513 MB (year 300) through autosaves; Web `s24web2` 6/6. Great
  worlds still hold ~1 GB and draw ~8 fps at the region view (`14` §7). (8) The profile: sworn men and lasting strength cost nothing measurable.
  DECISIONS D2-164-D2-167 and D2-169 wait for the per-slice 900-year lines (`8_*`); C10.8's ten seeds run.
- **Session 24 (20 Oct 21:56 - 21 Oct ~01:25 IST; Control Room run #73 session 1) - context handoff with the job queue running; nothing of C10.7a,
  C10.7b or C9.5c landed yet** (SESSION_HANDOFF "Session 24" has the queue and the steps). (1) **The stopped queue restarted** in a new scratchpad (the
  old one was gone: every binary rebuilt from its worktree, each checked against its own earlier runs). (2) **The landing branch's full suite**
  (`wip/s23b-int` ca25d71): 441 passed, 0 failed, 0 skipped; floatcheck clean. C10.7b's and C9.5c's binaries rebuilt and their test classes again 0
  skipped. (3) **C9.5c's hunger lean examined** (`d2166.md`): C9.5c acts late (twins identical until years 205-290), the hunger change is not the sickness
  change (r +0.08), seed 4's +907 is two crash famines of a world at its land's ceiling; a **matched placebo** (`_pvm`: the old rules nudged by one FU at
  each world's own parting year) spreads hunger as widely (sd 0.100 against 0.097) and itself leans past |t| 2 (relief by sea x1.22, t 2.09); against
  its placebo twin hunger x1.08 (t 1.6), palisades x0.99, epidemics x0.86 (t -1.95). A **replication** on seeds 9-19 did not repeat the hunger lean (x1.03,
  t 1.33) but leaned in people (x0.96 over years 200-299 in its six diverging seeds) - pooled over 15 diverging seeds people x0.979 (t -2.6), hunger a
  person-year x1.067 (t 2.7), deaths and births a person-year at par; no mechanism found; a third set (seeds 20-29) is in the queue to decide before
  landing. (4) **C10.7b's ten seeds** (`_sx1`; control `--oldsworn` = `_a7` byte for byte): births and deaths a person-year at par; 499 sworn men in 80
  villages against 28 lords at year 300 (great households keep men - kept); people over 100-199 x0.924 (t -2.64) against `_a7`, x0.939 (t -1.46) against
  its matched placebo twin (`_sxp`), whose spread is large; the mechanism against the twin: tributes x2.0, loot x3.7, raids x2.4, wounds a person-year
  x1.38 - war more frequent and more costly. (5) **The next governance slice measured first** (new tools `lordships --heads --margins --could --wrongs`
  on main's ten worlds at year 300): bonds outlive their lords (25 of the 26 still paid had outlived the person they were made with) but hold by strength
  (25 of 26 lords still stronger by the odds of a first bow) - succession at a lord's death would change little; **formation limits the lordships**: 514
  pairs within a day's walk where one could make the other bow, against 26 bonds. **C10.8 the asking** built on `wip/c108-demand` (sim 91: a free village
  is asked a tenth at its harvest by its nearest stronger neighbour whose first household keeps sworn men; it bows where its people could neither stand
  against them nor go elsewhere, else the refusal is a wrong the asker holds); DemandTests 4/4 and seven neighbour classes 0 skipped; its control and ten
  seeds queued. (6) **The realms map layer on main** (D2-167 draft; merges 4d40607, 12321eb, ae89777): the HUD's Realms (R) washes each lordship's land in
  its colour with a band at its edge, chevrons from tributary to lord along the way its tenth goes, a legend; real shots in two rounds (`s24r3`/`s24r8`
  found gold as ripe grain, dark dyes as earth, a faint edge, a mustard village up close, two realms in one colour; `s24s3`/`s24s8` read at the region and
  orbit views after map dyes, a 3-px band, a fade up close); dyes kept distinct planet-wide; the Web test's frame cost waits for the landing's Web build.
- **Session 23, fifth context (20 Oct 2026, 05:06-~05:45 IST; Control Room run #63 session 5) - Arjun stopped the session**: nothing landed on main but
  tools; the queue was stopped after C9.5c's evaluation and C10.7b's SwornTests retest (the rest - the landing branch's full suite, C10.7b's seeds, the
  900-year checks, the profile - waits in `$Q/todo`: SESSION_HANDOFF "Session 23, fifth context"). (1) **The landing branch reviewed** (its diff read
  before landing; no fault found: the sworn men's release order and reasons, bands with sworn men, might weighed only at the three harvest weighings, the
  oath saved and hashed only when set, lived and COARSE practice at par per day, C9.5c's kept odds in both the visits and the news, failed routes cached).
  (2) **The landing branch's Unity C# compiles** - its Game code checked before landing for the first time, by a new tool: `unity.mjs cc --src DIR`
  (f0e2dd4) compiles a worktree's sources against main's generated csproj files. (3) **C9.5c evaluated**: tests pass on the rebuilt binary (VisitsTests:
  along a way 9.6 visits as before, across water 0; news 8 and 0), control `--oldvisits` = `_y1` (state hash 78635b5066ea574c), ten seeds `_pv1`:
  at par in the vital rates; sickness x0.64, epidemics x0.81, palisades x0.95 (the mechanism); hunger deaths x1.06 (t 2.2) with all deaths a person-year at par - to explain before landing. (4) **C10.7b's SwornTests retest**: 5/5 after a stale assertion on the settlement page's wording was fixed (tc-sworn 71bb313, tc-int2 ca25d71). (5) **Started: the realms map layer** (`wip/realms`, worktree `tc-realms`,
  presentation only; D2-167 when it lands): `RealmsView` - each realm village's land washed in its realm's colour (the seat stronger), chevrons from each
  tributary toward its lord along the way its tenth is carried, a legend; committed 7d418d3, it compiles (`cc --src`); not wired into the game yet - the steps are in
  SESSION_HANDOFF.
- **Session 23, fourth context (20 Oct 2026, 02:54-05:10 IST; Control Room run #63 session 4; the same job queue)** - context handoff with the queue
  running; nothing of C10.7a, C10.7b or C9.5c landed on main yet (the landing branch `wip/s23b-int` waits for the queue's evaluations). (1) **Main
  re-checked** (the handoff's stale milestones): main's code was bc99679's (cc66770 changed docs only); full suite 433/433, 0 skipped
  (`results/fullsuite_m4.txt`), `unity.mjs cc` clean on bc99679, D2-159-D2-163 in DECISIONS as drafted. (2) **C9.5b's checks completed** (D2-163): Web
  `s23web4` 6/6 with C9.5b, browser = headless dotnet = editor Mono bit for bit; the first real shots (`s23m5`, `s23m4`) framed no dugout - a new probe
  (`voyages --load SAVE --days N [--dugouts]`: a save lived as the game loads it, every voyage logged with its village's day and hour; dugout bookings
  followed to their fate) found seed 2's lived crossings (19 voyages in 96 days, all dugouts) and aimed shots (`s23n1`, `s23n2`; views `voyagedugout...`,
  `voyageclose` at 45 m) framed them at the predicted minute: a dugout crossing reads at 45 m. Seeds 3, 4, 5, 11 crossed by dugout neither lived nor at
  COARSE over two years from year 300 - lived and COARSE agree; their crossings are earlier history. Tools merged into main (db4b8ba, a8ca460; db4b8ba
  broke the headless build with a local-name clash for an hour - fixed). (3) **C10.7a a lordship's might by lasting strength** (`wip/c107a-arms`,
  sim 88; D2-164 draft `tc2/out/tools/s23/d2164.md`): might weighs each person of fighting age by their years, their village's tools, arms and iron and
  their fighting skill (`Conflict.Lasting`, `LastingOf`, `Kit`, `Years`), not by heads; `--oldmightarms`, `--armstally`. Nine test classes pass (0
  skipped); control = `_y1` (annals and state hash); **ten seeds `_a7` at par** (people x1.018 t 1.24, births x1.003, deaths x1.000, hunger x0.993;
  tribute agreed x1.20 t 1.78, bonds for bread x1.22 t 1.82); the two weighings disagree 17 times in 2,479 over the ten worlds; armed worlds (seeds 2,
  4, 7) bow more to the armed. 900-year check queued. (4) **C10.7b sworn men** (`wip/c107b-sworn`, sim 89; D2-165 draft `d2165.md`): the one first
  among a village's people, whose household's grain runs beyond its own next year and three more, keeps young men of the village sworn to them (the
  landless and land-short first, then the boldest), a year's food each at the harvest from the household's bin into theirs; they practise every day
  (a ground laid out for them), go with every band beyond its twelve, and weigh in its might by their skill (C10.7a). Control (first version) = `_a7`.
  **Its first seeds found churn**: the oath was to a household id, and seed 4 released 2,956 men in 300 years as their oath household merged into
  another (the old gone to live with a child) and re-swore them the same harvest - **the oath is now to the person** (1557053; released at the lord's
  death, another's rising, a move, the man's fortieth year). Eight neighbour classes pass on the rebuilt binary; SwornTests' wording fixed (3e570e3) and
  retested in the queue. Open: seed 4 had sworn men in 9 of 29 villages at year 300, only 2 of them lords (any first household with grain keeps men -
  emergent thegns): judge by the seeds and the 900-year check. (5) **C9.5c people across water** (`wip/c95c-visits`, sim 88 on main's line; D2-166
  draft `d2166.md`): measured first (`--visittally`, four worlds byte-identical to `_y1`): in the archipelago 65 % of a COARSE day's visits, 81 % of the
  news and two thirds of the sickness caught on visits crossed water no lived visitor could cross; every world told of paths "worn" where no way on
  foot runs (6-14). Now a COARSE day's visits, their sickness, news and wear go only where a way on foot leads, with a lived day's odds - the visits to
  a village no way reaches are lost, as `World.PickNeighbour` loses them (a first version spread them over the rest: found in review, fixed 8a270c4;
  VisitsTests: visits along a way 9.6 as before, across water 0). Tests passing; control and ten seeds queued. (6) **The landing branch**
  `wip/s23b-int` (main + all three, sim 90 = 88 C10.7a, 89 C10.7b, 90 C9.5c; docs `10` §12i-j, `24` §5, `25`, `23` §3; shot view
  `act:<Activity>:sworn`): its full suite is queued (`6_9_d`). SESSION_HANDOFF "Session 23, fourth context" has the queue and the next steps.
- **Session 23, third context (20 Oct 2026, from 00:04 IST; Control Room run #63 session 3; the same job queue)** - **landed on main (c8f6f5d):
  C9.5a grain across water goes by boat; DECISIONS D2-162; sim version 86.** (1) **The 900-year check of the code as landed** (seed 2, `_l9g1`): no
  snowball (6-7 lords among 28-32 villages, the largest 2-4, depth 1-2); the tenth carried every year (1,334,050 FU in 18,940 loads, 70 % on carts,
  none left a year); 27 % of the tenths (484,105 FU) had no way on foot to the lord; a lord's first household holds up to 30 years of its need (the
  evidence for C10.7) - a line in D2-161. (2) **C9.5a finished and judged**: its first test run (39b7ce4) had two test-premise faults and no code
  fault (the relief test checked "told as by sea" after an at-once gift had told that famine; the tribute premise's lord was the weaker, so every
  tribute test was skipped - and the queue's gate had counted skips as passes: it no longer does). Added: **the lord's boats come for the tenth**
  where its tributary has none free (`ForTenth`: nothing aboard going out; the dues settled at the tributary's landing, `Conflict.Settle`; the grain
  the lord's when its boat is home, `Conflict.Stored`); **a trader walks only to a partner a way on foot reaches** (found in a sweep of every
  "within a day's walk" rule: a COARSE day's traders dealt across a strait at once while a lived trader found no road); the voyage text (the tenth
  rowed over had been told as a trade). **A save taken while the lord's boat is out did not continue** - a probe comparing the two worlds part by
  part found the planet alone diverging: the test premise had sunk ground by hand, outside the plates' turn and its `LandChanged`, so the running
  world's waters were drained for the old ground; loaded once, the premise continues identically (the game was right). Evidence: AcrossTests 8/8
  and ten neighbour classes; control `--oldacross` = `_g1` (annals and state hash); the old rule's survey: **91 % of seed 5's trades and 99.5 % of
  its relief crossed water at once** (seed 3: 9 % and 34 %); **ten seeds `_x2` at par in the vital rates** (people x0.993, births and deaths
  x1.010, x1.021), trade x0.60, relief x0.63; no tenth went by sea - bonds across water no longer arise (444 -> 66 harvests with no way on foot);
  **the archipelago (seed 5) pays**: trades 3,889 -> 174, hunger deaths 1,018 -> 1,733, people -14 % over years 200-299 - its boats are made only by
  fishers: C9.5b's case. (3) Tools: `--acrosstally` [5]-[6], `--carrytally` [16]-[17], `lordships --bonds` with ids and boats; shot views
  `voyagetenth...`/`voyagerelief...`; shots `--after tribute` across water. Left for C9.5b: a COARSE day's visits still carry news and sickness
  across water at once (`Conflict.News`, `Disease`). **Validated on main**: builds (`bin_m3`), floatcheck clean, Unity C# compiles (`unity.mjs cc`),
  **full suite 427/427**, **Web build and test `s23web3` 6/6** - browser (WebGPU) = headless dotnet = editor Mono bit for bit (frame rates 27-52 fps
  at the village and region views on a busy machine: not evidence). (4) **C9.5b a boat to cross** built (`wip/c95b-cross`, sim 87): CrossTests 6/6 and
  the neighbours pass; **landed (0a8b0b0; DECISIONS D2-163; sim version 87)**: control `--oldcross` = `_x2` (annals and state hash); **ten seeds
  `_y1` at par in the vital rates** (people x0.995, births and deaths x1.015, x1.018, hunger x0.996); voyages x2.4, trades x1.20, relief x1.54; boats
  made to cross in eight of ten worlds (72 in all); the archipelago (seed 5) made 11 dugouts to reach each other - trades 174 -> 618, relief
  1,273 -> 50,006 FU, hunger deaths 1,752 -> 1,283. No lordship of the ten C9.5a worlds is bound across water at year 300 (`lordships`): the shots
  of boats crossing are taken on C9.5b's worlds. **Validated on main after the merge**: build, floatcheck and Unity C# clean, **full suite 433/433**;
  the real shots (`s23m5`, `s23m4`) and Web `s23web4` were in the queue at the context handoff (SESSION_HANDOFF "Session 23, third context").
- **Session 23, second context (19 Oct 2026, from 21:26 IST; Control Room run #63 session 2; the same job queue)** - **landed on main (a19c077):
  C10.5, D2-160 and C10.6 as amended; DECISIONS D2-159, D2-160, D2-161 (464e72c); sim version 85.** (1) **C10.5's 900-year check** (seeds 2 and 4
  of `_z1` to year 900, against C10.4's `_y3`): no snowball and no growth - the largest lordship 2-3 villages, depth 1; bonds hold longer with less
  churn (the mean life of a bond 114 -> 214 years on seed 2, 166 -> 201 on seed 4; bonds 49 -> 32 and 30 -> 23; pairs bound again 10 -> 6 and
  3 -> 3); might ends a bond only where the old count also would (25 of 42, 14 of 29). (2) **Reviewing the integration before landing found two
  C10.6 faults** (amended on `wip/c106b-carry`, f993e39, 32dba4c): a bond for bread needs only a day's walk as the crow flies (C10.4) and relief
  within it moves at once, but the tenth is carried only along a way on foot - so a tributary across water set its tenth aside at every harvest
  and nobody could ever carry it. **Measured on the landed code: 444 harvests in five of ten worlds, 100,504 FU - a fifth of all the tribute - had
  no way on foot to the lord** (seed 5's only lordship for 107 years). Now the tenth is set aside only where a route on foot reaches the lord (else it
  stays in the store; the pages say why), what a year left uncarried is the village's again (133 FU in ten worlds), a load sent home by a switch to
  COARSE is set aside again, and one cart goes one way at a time (`Carts.Out`: two members had each taken the cart's load - the one cart and ox drawn
  twice). (3) Evidence: floatcheck clean; TributeCarried 9/9 and ten neighbour classes (two test premises corrected: a made world's first tectonic
  tick at time 0 relays the ground; Bread's and Leaders' pairs now a way on foot apart); **control** `--oldtribute --oldways` = C10.5's `_z1`
  byte-identical; **full suite 419/419**; **ten seeds** `_g1` against `_z1`: vital rates at par (people x1.000, births and deaths a person-year
  x1.003, x1.007), tribute agreed 40 -> 28 (x0.74, t -2.2) - the lean D2-160 alone showed (bonds for bread x0.65), being attributed by `_g2`
  (D2-160 alone on the landed binary); the tenth carried 419,286 FU in 7,909 loads (half on carts), carriers' 102,949 km; **the ways of a lordship
  do not stand out** - every pair of villages within a day's walk is worn to a path or a road by its traffic (bound 4 roads/19 paths, others
  89/500/10 tracks): the design's hope corrected in `10` §12h; the route probe on the landed world: equal hashes with and without the view's routes.
  (4) Tools: `lordships` prints the bonds with no way on foot and the ways of other neighbours; `--carrytally` [7]-[9]; shot view `why:<reason>`;
  shots `--after tribute`. (5) **Validated on main**: Unity C# compiles (a coroutine `ref` local in the new shot option caught and fixed, 420ef71); **Web
  build and test `s23web2` 6/6** - browser (WebGPU) = headless dotnet = editor Mono bit for bit; every view faster than `s23web1` (close 28 -> 37 fps,
  region 44 -> 62) on the same busy machine (a quiet-machine measurement still owed); profiled, might and carrying cost nothing per village-day.
  **Real shots**: the standards read up close and from the low view, not from the village view (8x tried and reverted; a polity at that scale wants a
  map layer - D2-161 (4) corrected); no green nor teal in the polities' colours; **the carriers read on the road** (`s23k`: Paerdal's handcarts with
  sacks on the path to Lounstead, the morning its village set the day's loads). (6) **Next slice built: C9.5a grain across water goes by boat** (branch
  `wip/c95-across`, sim 86): within a day's walk grain moves at once only along a way on foot; relief, give-back and the tenth cross water in boats or
  not at all. **The survey on the landed code** (`_a1`, annals byte-identical to `_g1`): 1,679 relief gifts (~604,000 FU) and 1,150 gifts back (~58,000
  FU) crossed water at once in ten worlds (seed 5, 1,500 people: 178,927 FU); of 444 no-way tenths 366 had a way by sea, but the tributary never had a
  plank boat (the lord at 76) - the lord's boats must fetch the tenth (next). C9.5a's first build failed on a test premise (fixed 39b7ce4); at the context
  handoff (20 Oct 00:05) its rebuild and control are queued and its ten seeds held - SESSION_HANDOFF "Session 23, second context".
- **Session 23 (19 Oct 2026, 18:54-~21:25 IST; Control Room run #63 session 1; one low-priority job queue `tc2/out/tools/s23/runq.sh`, gated RAM < 86 %,
  CPU < 85 %; context handoff with the queue still running - SESSION_HANDOFF "Session 23")** - (1) **C10.4 merged** (`wip/c104-bread` --no-ff, ca3b4c8;
  DECISIONS D2-157, D2-158; `25`; sim version 82); Unity C# compiles on main; **Web build and test `s23web1` 6/6** - the browser's probe (WebGPU, IL2CPP)
  = headless dotnet = the editor's Mono, bit for bit; its frame rates were half session 18's because the user's Chrome ran heavily throughout (headless
  run times unchanged) - to re-measure on a quiet machine. (2) Tools: `lordships --load SAVE [--every N] [--bonds]` (every bond of tribute from the history,
  the lordships every N years, the ways between the bound and their lords, each lord's first household's grain and the tribute in), `run --load SAVE
  --warmroutes` (a determinism probe). (3) **The 900-year check of C10.4** (`_y3` seeds 2 and 4 to year 900): no snowball, no growth - 4-9 lords among
  27-35 villages, the largest lordship never above 3-4 villages; all 63 ended bonds ended "the lord no longer the stronger", weighed by the fit people at
  home on one day and a lord by its own village (seed 2's year 464: a mild epidemic, 6 dead, ended five lordships). Real shots `s23a`/`s23b`: the bond
  reads only up close (the tributary flies its lord's standard). (4) Built on branches and merged together on **`wip/s23-int`** (worktree
  `C:/Game Dev/tc-int`; builds, floatcheck, the new fixtures pass): **C10.5 a lordship's might** (D2-159 draft, sim 83: people of fighting age, a lord with
  the men its call brings; control byte-identical, ten seeds at par, its 900-year check in the queue); **D2-160 the ways' woods** (sim 84; found reading
  the code: a route's forest cost read the woods of the moment it was first built, so a load or the view building routes first changed history - seed 2's
  year-300 save, three lived days, b5329437ba119770 alone and 8101ffe7abef7b57 with the view's routes built at load; now routes read the woods at the
  last turn of the plates: the probe's hashes equal over 3 and 6 lived days and 3 COARSE years, control byte-identical, ten seeds at par - VERIFIED);
  **C10.6 tribute carried** (D2-161 draft, sim 85: the tenth set aside at the harvest and carried to the lord's store by the tributary's people, the ways
  of a lordship converging on its lord; standards enlarged with distance; TributeCarriedTests 5/5; its ten seeds in the queue). Not yet on main: the
  queue's results (C10.5's 900-year check, C10.6's seeds, the integration's full suite and seeds) decide the landing.
- **Session 22 (16 Oct 22:19 - 17 Oct ~02:15 IST; Control Room run #51 session 2; stopped by Arjun; `SESSION_22_REPORT.md`)** - (1) **C3.7d
  merged** (9d68eaa, sim version 80) and **D2-155** written (a512839): twenty seeds `_u7` against `_c6` - births and deaths a person-year at par,
  pig-years x0.98 (t -0.2), cattle-years x1.64, pigs gone by year 300 in 2 of 19 worlds (the control 1); sickness from animals x1.7 with the herds
  (D2-070's spillover by beasts kept: 61 new sicknesses against 36, sickness deaths 10,338 against 4,505); a placebo (`_u4` against `_u7`, the same
  rules apart by the first lived day) shows end-of-run pig counts are chance (x0.46). Found on the way: a 300-year run lives its first day before it
  goes COARSE, so a rule of lived walks alone still sends the whole history another way. (2) **D2-156 nobody walks through the water** (branch
  `wip/dry-ways` **merged f200b9c**, DECISIONS 40f4a18; sim version 81): a lived walk bends once round a lake or a bay across its way
  (`Core/Places/Afoot.cs`, `Person.BendX/BendY/TBend`), every lived spot out of the water, crofts' beds not laid into it, the woods by the way their
  cutters walk; `--wetways` = `_u7` byte-identical; AfootTests 6/6 and 66 neighbour tests; **the same year-300 save: 0.178 % -> 0.013 % of
  person-hours under the drawn water**. Its first version stranded villages whose only woods lie across water (ten seeds `_x1`: 18 without woods
  against 6, plank boats and sea relief collapsed) - fixed by the straight line's choice where no woods can be walked to (`_x2`: vital rates a
  person-year at par; what is left is island woodcutters, 3 villages in ten worlds - boats across the water, C9.5). (3) Tools: `polity --load SAVE`
  (the ties a polity could be made of) and `woodways --load SAVE` (what D2-156 changes in a world at a moment). (4) **C10.4 bread and dependence**
  (branch `wip/c104-bread`, worktree `C:/Game Dev/tc-bread`; sim version 82; `10` §12f): measured first - kin does not mark mother and daughter
  villages, relief does (38 % of neighbour pairs, 57 villages owe at year 300 over ten worlds); a village that cannot give back a month of the famine
  grain a stronger neighbour within a day's walk gave it bows to that neighbour; the tribute gives it back, a lord feeds its own. BreadTests 5/5 and
  seven neighbour classes; ten seeds `_y2`: vital rates untouched, lords 8 -> 21 (tribute agreed 9 -> 35, 21 for bread), the lord's call answered (471
  men), raids 79 -> 118 - NOT merged (branch tip 08e16b2). (5) **D2-158**: the first full suite since session 19 (402/403) failed
  `MigrationTests.AHomeIsRaisedOnlyByLabourAndWood` since C3.7c - a COARSE day's muck before a camp's own roof; fixed in the lived urges' order on the
  branch (ten seeds at par, foundings x1.14). The full suite on the final code was running at the stop (`fullsuite_b4.txt`). Not done: the Web build
  and test, real shots, the 900-year check - SESSION_HANDOFF "Session 22".
- **Session 21 (16 Oct 2026, 19:12-22:05 IST; Control Room run #51 session 1; one low-priority job queue, `tc2/out/tools/s21/runq.sh`)** - (1) **C3.7d's
  pigs traced** (branch `wip/c37d-stubble`, still NOT merged): tools-only herd diagnostics (`--places`: births and ends by village, kind and cause, by
  the village's day of the year, hard times told apart, herds by household; `--stocktally`); where pigs vanished the land was plentiful and the sows fed
  and bred as the control's - the households keeping pigs dwindled because **the hands' cull took the kind with the most spares** (after a farrowing
  the pigs) when the stubble's cattle filled the households' hands; now **short hands are eased from the kind whose spares take most of them**
  (`--oldhands`). Ten seeds `_u4`: people at par, cattle x1.94, pigs x0.80 (n.s.) and lost in one world (seed 5; four with the old rule). (2) Real
  shots `s21a`-`s21t` (`27` §9): herds on the stubble, each household's field work (busiest spot 2-13 within 60 m, was 126), **a carter in the sea**
  -> a household's field cell is one its hands walk to dry-shod (`FieldSpots.DryWay`, `--wetfieldspot`; field work in the water 2-4 person-hours from
  55-68), the stubble drawn dun as eaten, muck heaps placed clear of the croft, pen, woodpile and cart (new shot views `muck`, `mucklow`), the
  woodlot framing (107c100) works, a crowd of hunters is a village outgrown its land (real). Tools: `wet --doy`, `crowds`, `unity.mjs shots --doy`.
  (3) Open at the handoff: the 20-seed comparison (`_c6` vs `_u7` with head-years) running in the queue; then merge, D2-155 (draft
  `tc2/out/tools/s21/d2155.md`), the full suite, the Web build and test - SESSION_HANDOFF "Session 21". Also removed the claude-mem plugin (its v13
  hooks called a v12 CLI: "Unknown command: hook" on every tool call).
- **Session 20 (13 Oct 2026, from 17:25 IST; Control Room run #55 session 2; Arjun's rule D-1/D-2 kept by one job queue,
  `tc2/out/tools/s20/runq.sh`: one job at a time, gated, below-normal priority; builds jump the seed runs)** - (1) **q19 read**: threshing at par
  in ten seeds; fish x0.94 follows fewer fishing villages (fish a person-year x0.95, t -1.5); foundings with the floor were never more than without
  in session 19's seeds - ten fresh pairs on one binary (`_m1`/`_m0` `--nothresh`, seeds 9-19) seeds 9, 10, 12: 15/15, 11/11, 26/27 - no deficit (D2-149). (2) **q20's real
  shots judged** (`tc2/out/shots/s19e20`, `s19f20`, `s19g4`, `s19g28`): the town's stackyard and the standards read; stumps nearly absent; graves read as
  a garden's rows over the fields; **the town's burial ground built over (25 of 29 grounds, 1,145 of 3,480 slots)** -> **D2-154** (homes, works and
  crofts keep off the ground, `--oldgraves` = `_m1`; graves side by side across their rows, jittered, headstones, sward without plots: 13 grounds and
  498 slots still covered, by streets and the square's earth), W-FELL view faults (stumps on felled-bare ground, sea vertices; the woodlot view framed
  a glade - 242 stumps were drawn at its edges; now framed where the woods stood thickest), the threshing floor drawn in use (the sheaves laid out
  between the threshers), `act:<Activity>` and `floorbusy` shot views. (3) **C3.7c the muck merged** (D2-153, sim version 79; tests repaired: the
  heaps while the crop stands, the leach e-folding, the water test over three worlds): `--nomuck --oldgraves` = `_m1` byte-identical; seed 4: 69,691 t
  made, 64,606 t carted in 193,771 h (3.0 h a tonne), 4,953 t leached; ten seeds `_n1` against `_m1` **at par** (92.5 % carted, 7.4 % leached, 3.2 h a tonne). (4) **C3.7d the herds on the stubble and
  the fallow** (worktree `C:/Game Dev/tc-stubble`, branch `wip/c37d-stubble`, sim version 80, D2-155): measured first - the herds are fed (grass short
  in 17 of 472 village-reports) because the herders keep them to the pasture's lean days (`Destock`: cattle's commonest end the `pasture` cull, 5,156
  of 9,548); the stubble from the harvest to the sowing and the rested arable grazed before the waste, their day's dung on the fields, each household on its own field cell (`FieldSpots`), a food's cull by what the spares eat; controls byte-identical; ten seeds `_u2`: people at par, fields x1.017, cattle x1.84 - **pigs gone from 3 of 10 worlds: not merged** (SESSION_HANDOFF "Session 20", `SESSION_20_REPORT.md`). Stopped by Arjun ~20:55 IST; nothing running.
- **Session 19 (13 Oct 2026, from 14:53 IST; Arjun's resource rule D-1/D-2: one low-priority job at a time while RAM < 86 % and CPU < 85 %)** -
  (1) **The full suite on 86b481d: 372/372** (14 min). (2) **D2-148 W-FOREST pass 2b** (b312f01): the rain belt follows the sun in the outer
  tropics (`--oldrain` = `_g4` byte-identical; the dry tropics' soil 0.09-0.43 through the year, was 0.15-0.25; ten seeds `_r1` at par in rates,
  people x1.04-1.08, the wild's food x0.90); the view reads the soil's water - the seasonal tropics' grass flush and straw, their woods withering
  and dropping leaves tree by tree, tall grass, the cool lands' duller turf; **every conifer had been drawn pale grey** (the mesh builder's
  shading scaled the tint alpha: `ProcMeshes.Builder.Dim`); forest views pick inland stands inside the woods. Real shots `s19b-s19d`
  (`tc2/out/shots/s19d_dry.png`: one stand at 16 N green in the rains, straw and bare in the dry season). (3) **C3.7b the threshing floor**
  (worktree `C:/Game Dev/tc-thresh`, branch `wip/c37b-thresh`, merged 14a9156; sim version 77): the harvest stands in the sheaf (`Settlement.
  Sheaves`, part of `Granary`) and only threshed grain is eaten; threshing at the floor (before the first barn's doors, else beside the granary)
  ahead of need - lived urge and stints (`Settlement.ThreshOut`), a hungry hand first, COARSE/DEEP before the day's work; the stackyard's ricks
  (kit `rick`), the flail pose (17), shot views `floor`/`floorlow`/`floorfar`; `--nothresh`, `--threshtally`. Threshing 5/5, neighbours pass,
  `--nothresh` = `_g4`; seed 4: 0.042 h a FU, 60 % threshed in the growing stage's slack. (4) **W-FELL** (same branch): `PlanetState.Felled` - the
  woods' felled share not grown back (a record the view draws as stumps; regrowth takes it back, stumps rot over 25 years); FellingTests 3/3.
  (5) In progress (q18): the stumps' view, **the burial ground** (D2-151: graves from real deaths, fresh for a year, sinking over decades; none for
  the drowned; `Burials.cs`, sim version 78) and **the first household's standard** (D2-152: a pole by the head household's door in its polity's
  colour - a tributary flies its lord's; the headman dressed in it); kit `grave`, `grave_old`, `standard`; the merge's control and ten seeds `_m1`.
- **Session 18 (11 Oct 2026, from 06:48 IST; Control Room run #48 session 3 continued; Resource Governor LOW, Arjun's rule D-1: one low-priority
  job at a time while RAM < 86 % and CPU < 50 % - other applications hold the machine at 76-96 % RAM)** - (1) **D2-144** (e23e5b7): the gardens
  kept the north's calendar in southern villages (dug at the reaping, gathered while the crop grew; found reading the code for its shots) - one
  `SettlementSystem.DayOfYear` for every use of a village's calendar; GardenTests 5/5 (a new test over both hemispheres that fails on the old
  calendar), neighbours, control `--nogarden` = `_g0`, ten seeds `_g2` at par, the gardens' food x2.05 (north and south alike), `--gardentally`.
  (2) **The first real shots of the gardens** (`s18a`/`s18b`): the beds by season read (fresh earth dug, olive rough undug, green in leaf) but every
  bed carried its own hurdles (crofts read as crates) and the croft beside an end overlapped the croft behind - **D2-145** (d224365): the croft
  beside an end begins beyond a 2 m end yard, one wattle fence along the croft's outline (kit `hurdle`, beds without hurdles); ten seeds `_g3` at
  par; shots `s18c`/`s18d` (`tc2/out/shots/s18_gardens_sheet.png`); the Unity `.meta` files of Gardens.cs and GardenTests.cs committed at last.
  (3) **The full suite on d224365: 369 of 371** - both failures the garden's COARSE day (D2-143): a camping household dug its garden before raising
  its house (`MigrationTests.AHomeIsRaisedOnlyByLabourAndWood`) and a short village gardened before it foraged (`WildFoodTests.CoarseAndActivity
  TurnToTheWildAlikeInAShortage`: COARSE 0.59 of lived) - **D2-146** a COARSE day's garden in the order a lived day's urges give it (after the wild
  when hunger pulls harder, after the household's own roof where that pulls harder): both pass (wild hours in a shortage COARSE/lived 0.82),
  neighbours pass, control `--oldgardenorder` = `_g3` byte-identical; ten seeds `_g4` at par. Its seeds found **a hang** (seed 6, year 159->160:
  the tectonic step drowned a village, `FreeLot` walked its drowned streets forever) - **D2-147**, the frontage search bounded by the spiral's reach
  (test: the drowned village; seeds 4, 5, 1 byte-identical before and after); tools `run --trace FILE`. **Web build and test `s18web1` 6/6** on c7edc68.
  The full suite on c7edc68 was running at the stop (Arjun stopped the session; the machine held at 60-100 % CPU by other applications for hours).
  W-FOREST pass 2b prepared, not applied (`tc2/out/tools/s18/pass2b.diff`, `q10.sh`, `d2148.md`). Report: `docs/tc2/SESSION_18_REPORT.md`.
  (4) **The 900-year check** (`_g3` year-300 saves lived 600 years more, bin_d): seed 4 4,488 -> 8,000-11,000 people from year 350 (11,149 at year 900,
  38 villages), hunger 0.3-14 a 1,000 person-years, epidemics at 650 and 800 knocked back and recovered; soil tired 0.37, 15 % resting, a sown ha 0.76
  of a fresh crop; 83 % of households with a garden (mean 153 m2). Seed 42 steady at 4,300-6,000 (5,667 at year 900, 18 villages), hunger 3-10, one
  epidemic near 800. No collapse, no runaway.
- **Session 17, second context (10 Oct 2026, from 06:46 IST; Control Room, Resource Governor LOW; the machine at 75-96 % RAM from other
  applications much of the time)** - (1) queue 3 closed: the controls byte-identical (`_c0` = `_s2`, `_c1`/`_c2` = `_n1`); ten seeds of the
  final binary against `_n1` at par in rates; boats lost x1.68 traced to `_n1`'s luck (the lowest of 65 run families; `place --sheds`: a shed fits
  at every landing either way). (2) **The Unity side compiled and shot for the first time**: `node tc2/scripts/unity.mjs cc` (new: the C# without
  the editor, half a minute), then the real game: shots of a loaded save had shown everyone standing at their doors ("at work or on the move 0")
  - the save lived its days only from its next midnight; `ShotRunner` now waits for the lived day (`--village town` for the town's own hour);
  on a lived morning 2,155 of 3,001 people drawn are busy, with poses, ways along streets, gardens and pens (`27` §8). (3) The full suite
  365/366, then 366/366: DefenceTests' palisade test traced year by year - the soil's leaner worlds (D2-139) make its town raise a third ring at year 25,
  legitimately; the test now checks the old ring's yearly rot (8/8); the determinism lint caught four float subtractions (fixed). (4) **D2-141's
  street clearance makes a people wave** (x1.02-1.07 over years 140-199, x0.95 over 280-299, t -3.7) - attributed with ten seeds of the ring rule
  alone (`_v5b`: none); the ring rule now passes a work only where that costs no home (`Conflict.PastWorks`, `_v4`). (5) **Adults idle ~7 of 12
  daylight hours** in the year's first days (3.9-4.6 over a lived year) and the kitchen gardens drawn behind every house were worked by nobody:
  **D2-143 C3.7a the household's garden (sim version 76)** - the croft measured from the layout (median ~130 m2), dug, weeded and gathered by
  lived and COARSE days into the larder, drawn as beds on the croft, people hoeing behind their houses; ten seeds at par, `--nogarden` = `_w1`; a
  lived year with and without: 0.3 of 12 daylight hours in the gardens, nearly all from idling; under 1 % of the food. (6) **D2-142 W-FOREST pass 2**:
  trees by the growing season's warmth - people x1.10, per-person rates at par, boreal woods +50 %, the boreal and mixed shots now find woods;
  the dry tropics and wet woods still read alike: the rain has no seasons at any latitude (`place --seasons`: the dry tropics' soil 0.15-0.25 all
  year) - pass 2b prepared (`tc2/out/tools/s17b/p_drytropics*.py`, not applied). **State at the handoff: main 93ad626 verified (build, Garden 4/4,
  Defence 8/8, Unity C# compiles); the full suite not yet run on the garden merge; no real shots of the gardens yet** (SESSION_HANDOFF "Session
  17, second context").
- **Session 17 (10 Oct 2026, from 05:04 IST; Control Room, Resource Governor LOW) - the soil and the niche closed; the visual truth audit
  (`docs/tc2/27_VISUAL_TRUTH.md`)**. (1) **D2-139 the soil closed**: HEAD had not compiled (the niche code shadowed `days`; a tools `sb`) - fixed;
  the last three seeds on the soil's binary; ten seeds against `_k2`: people at year 300 x0.59, births a person-year x0.99, hunger a person-year
  x1.80 (6.2 -> 9.2 a 1,000). (2) **D2-140 the household's niche adopted (sim version 75)**: a couple marries when its land and hands keep it a
  year at the last harvest, or its grain carries the shortfall four years; ten seeds against the soil alone (`_n1` vs `_s2`): hunger a person-year
  x0.68 (t -3.2: back to 6.5), births x0.94, deaths x0.93, famines x0.70, people at par; women's first marriage 20.4-22.7, ever married by 45-59
  74-100 %, marriages move to the weeks after the harvest. The niche test's premise (4.5 years of grain - a granary holds two) fixed. (3) **People
  standing still** (Arjun's playtest): measured with `place --still` - 98.4 % of the people drawn in daylight stood still (field hands for 3.75 h
  at one spot; cooks and eaters inside their walls); about half were working, a quarter idle at home, a fifth children at play. Fixed by
  `Core/Agents/Presence.cs` (where, facing where, doing what - one answer for the view, picking, following and the audit) and sixteen poses in the
  person shader: now 28-32 % neither move nor work (the idle seated at their doors, pauses), eating and cooking indoors. (4) **Houses on roads**:
  measured with `place --roads` (twenty saves) - the simulation's ground nearly clean, the drawing not: ways between villages drawn across the lots
  (seed 4 year 300: 286 homes cut), far enlargement ignoring streets, lanes drawn 6 m early, livestock pens laid over streets and neighbours (81 of
  168 into another home), gates toward neighbours' centres, rings astride training grounds, works 1 m off streets. Fixed: `WayLines` (ways in by
  the street, round the edge, through passed villages by their streets; corner anchors against Chaikin; no road over a square) - homes cut by a way
  286 -> 1 (seed 4), 136 -> 0 (seed 42); `Footprints.OpenScale` in `Room`; lanes from 26 m; pens in the yard pen or a free ring (0 collisions);
  gates at the drawn ways; **simulation changes**: rings clear of works (`--nowallworks`), works `Streets.Width` off a street's line
  (`--noworkclear`). (5) Fallow drawn at its true share, rotating by year. (6) W-FOREST pass 2 begun: the simulation itself holds little cool
  forest (median woods share of wooded boreal cells 0.08-0.16, broadleaf 0.09-0.24; none at the treeline) - tree suitability ramps on the annual
  mean (-1..8 degC), not summer warmth. **Unity side not yet compiled or shot** (the machine stayed at 76-89 % RAM).
- **Session 16, fourth context ended (10 Oct, ~04:00 IST, at Arjun's request)**: the soil (C3.6f) is committed as work in progress with seven of ten
  seeds measured; its last three seeds, the full suite and the niche runs (C3.5b) did not run - the machine's RAM stayed over the 75 % ceiling from
  other applications from 00:48 on. The niche's tests and a `PortionFor` fix are committed uncompiled. SESSION_HANDOFF "Next, in order" 1 resumes it.
- **Session 16, fourth context - C3.6f the soil (D2-139, sim version 74; work in progress: seven of ten seeds, full suite pending)**: the fields
  have a heart - they tire with the crops they bear, rest and the herds' dung give back, villages rest a share by what their crops have shown, new
  land comes in fresh, the people judge their land by what it bears. `--nosoil` reproduces `_k2` exactly. Worlds carry x0.55-0.7 of their people
  (D2-085's mature range again), bound villages rest 13-17 % with a sown hectare at 0.73-0.79 of a fresh crop - but **births do not answer** (42 a
  1,000 before and after) and hunger a person-year rose x1.9 (4.7 -> 9.3 over years 200-299), wounds x2: C3.5's rule never binds (couples bring two
  years of bread). Next: C3.5b the niche (a couple marries when its land and hands keep it; `--niche`, measured, off by default).
- **Session 16, fourth context - the land economy over 900 years with C3.6e (sim version 73), seed 4 (`s4_k2` + `s4_l9k`, against `s4_l9`, sim 70;
  one world - seed 42 not yet run)**: more people in centuries 300-600 (13,200-15,900 against 10,300-11,600: births 40-42 a 1,000 against 29-36 -
  heirs marry as the land comes) and **hunger 8.7-11.2 a 1,000 person-years against 1.0-5.1**: the old's grip on the land had been a preventive
  check (an artefact C3.6e removed); without it the villages fill to the positive check. A plague in years 746-753 killed ~9,000 (16,800 -> 8,100);
  after it no hunger and 2.4 million FU in the stores (post-plague plenty), 18,700 people by year 900. (The year-900 save was lost to a double
  rename; `s4_l9k.csv` is complete, `s4_l9k/world_y600.tc2` the year-600 save.) The fields' heart (C3.6f, in progress) changes this equilibrium:
  its own 900-year check follows it.
- **Session 16, fourth context - D2-138 a headman gone to live with their child stays first (sim version 73)**: C3.6e's retirements put the first
  household's headman in another household 37-127 times a world, read as a contested vacancy ("another household rose" for the same man; 4 of 20
  splits). Now a living headman's move is weighed again without a contest, the household that took the first household in is the incumbent, and a
  household's clients follow the household that took it in (a gap since C10.2). Ten seeds (`_k2` against `_j1`): successions at a retirement
  0-17 a world, headmen x0.83, splits at a living move 4 -> 2; people and vital rates at par; tributes 17 -> 8 (low counts, watched).
  `LeadersTests` 8; `place --heads`. **Web build of 4bf4888: Web test `s17web1` 6/6** (bit-identical probe; the day away to year 96 in 106 s, 487
  people in 16 places - headless 488 in 16; C3.6e, D2-137 and D2-138 verified in the browser).
- **Session 16, fourth context - the Web catch-up's out-of-memory traced and fixed (D2-137, sim version 72)**: a Living catch-up of a small world is
  ACTIVITY, not COARSE (under 40,000 person-years), so the browser's world is headless `run --seed 42 --years 96` - which ran out of memory the same way
  (17 places at year 28). The browser's own saves (`webtest.mjs --export`, new) replayed headless with `run --hot` (new): four people walking a way of
  no length ~17,000 times a day between two villages a party and another party had founded on the same cell in year 27; the arrival read from the arc
  length sent them back where they started; every leg filled the travel index, which only queries prune. Fixed: a founding party that finds a village
  within the spacing at its place joins it (`PartyJoined`, 95), a traveller on a way of no length arrives at their destination, the travel index sweeps
  stale legs before it grows. Ten seeds DEEP 300 years (`_j1`): annals byte-identical to `_e1` (the case never arose there); headless ACTIVITY seed 42:
  96 years in 43 s, 488 people in 16 places. Three MigrationTests (the loop test fails with the old rule); full suite 358/358. `place --near` (villages
  closer than the spacing; none on one cell in twenty saves).
- **Session 16, third context, later - C3.6e the old hand on their holding (D2-136, sim version 71)**: the land-rich were the old (youngest 55+:
  63-89 % of the land-rich's land, most of their grain); now a landholding household whose youngest is 60+ goes to live with a child's household
  (else a land-short household with hands, for its keep), its land and grain with it (`Retired` in history). Ten seeds against `--noretire` (= `_d1`
  exactly): 950-3,600 retirements a world; landless at year 300 17-66 % -> 6-44 %, Gini 0.70-0.93 -> 0.50-0.65, the hoards gone (168-224 days of
  grain in the land-rich households, from 305-1,594); births a person-year x1.06 (t 4.7), people x1.09, hunger at par. `LandTests` 10.
- **Session 16, third context, later - the land economy over 900 years** (seeds 4 and 42, `s*_l9`, saves 300/600/900, sim version 70): no collapse and
  no runaway. Seed 42 settles after the filling (years 200-299: hunger 6.6 a 1,000 person-years) at ~8,600-8,800 people for six centuries, births =
  deaths = 28-29 a 1,000, **hunger 0.2-0.8 a 1,000 person-years** - a low-pressure regime held by the preventive check; seed 4 is livelier (9-15k
  people, hunger 0.15-5.1 by century, raids 45-90 a century). Landless households 54-77 % at years 300/600/900 (die-outs without heirs keep giving
  land back), but **the households holding their want keep decades of grain** (800-2,975 days' food, seed 4 at year 600 62 years' worth for 1.4
  people): no sink but rot, the village's sales and loans - the next problem (D2-126 "Open": households' own trade). Cost: seed 4's 900 years in
  25 min (0.6 world years a second at 12-15k people).
- **Session 16, third context, later - C3.6d who leaves (D2-135, sim version 70)**: each household weighs leaving by its own land (its holding
  and its part of the unheld land against its want) instead of the village's crowding - the landless choose to go, the landed stay. Ten seeds
  against `--villageleave` (= `_c1` exactly): every aggregate at par; land concentrates (landless at year 300 13-46 % -> 17-66 %, Gini up to 0.93):
  in full lands the landless find nowhere to go. The DEEP guard now reads its two worlds' herds together (twelve small worlds: DEEP against COARSE
  cattle x0.96, goats x0.92, pigs x1.23; single worlds x0.3-x3.7 either way, with or without the rule). `LandTests` 8. Web build of 26127b5:
  Web test `s16web8` 6/6 (bit-identical; the Land tab shows each village's holdings); full suite 353/353 on 26127b5.
- **Session 16, third context, later - C3.6c the harvest by land and labour (D2-134, sim version 69)**: a harvest (and each day's sheaves) is
  shared half by the land each household holds (the unheld part the village's) and half by the hands it gives the fields (the metayer's half-share;
  the commune still works its fields together). Ten seeds against the same holdings with the harvest by mouths (`--bymouths` = `_h1` exactly):
  **hunger a person-year x0.74 (t -2.8)**, births a person-year x0.98, deaths x0.96, people at par; the landless took half their mouths' share and
  married later or never (women's first marriage 20.6-22.0 -> 20.5-22.8; ever married by 45-59 94-100 % -> 67-100 %): the preventive check, from
  the households' economy. `LandTests` 7; two C3.3 tests now run their checks with the harvest by mouths.
- **Session 16, third context, later - C3.6b household land, first step (D2-133, sim version 68)**: measured first (`place --heirs`): men marry
  at 19-23, a man's father dies when he is 22-24, a family raises 1.55-1.93 sons to 20. Built: each household holds hectares of its village's
  fields (founders by heads; a marrying child a portion of what the family's land spares; the last out the rest; an heir the land of a household
  that died out; those who go leave theirs; at sowing lost land from all, unheld land to the least landed). Pages: "It holds N ha of the
  village's fields", the settlement's "Land:" line. Ten seeds: annals byte-identical to `_s3` (the bookkeeping moves nothing); at year 300 32-60 %
  of households landless, Gini 0.74-0.92, the largest holders the villages' creditors. `LandTests` 6. Next: the harvest by land and labour (C3.6c).
- **Session 16, third context, later - the first sheaves (D2-132, sim version 67)**: measured the hungry gap (`--gaptally`, new): 66 % of hunger
  deaths fell in the ripe stage, the store empty, the dead mostly children - the whole harvest reached the store on its last day while villages
  reaped through the stage. Now the grown crop's yield is known when it ripens and the reaped share comes into the store and the households' bins
  each morning (debts on the harvest's day; sheaves in the store not burnt with the stubble), and villages ration to their next new grain (the
  first sheaves; once ripe, the standing crop counted, to next year's). The first build (sheaves only) made it worse - hunger x1.65, raids x2.7:
  the first sheaves were eaten at full meals; rationing the ripe stage alone left the rest of the year rationed a week short. Final, ten seeds
  against `--nosheaves` (= `_t1` exactly): the deaths beside the sheaves are gone (within 5 days of the harvest's day 49 % -> 1 %), the gap now
  falls before the grain ripens, and hunger a person-year is not lower (median 3.9 -> 4.7, mixed by world; people x1.05-1.08): the saved
  children grow the villages and the same food brings the check back earlier. `SheavesTests` 5; full suite 345/345 (3d0c346); Web
  test `s16web7` 6/6 (bit-identical; `tc2/web-build/web` is of 3d0c346).
- **Session 16, third context (9 Oct 2026, from 16:10 IST; Control Room run #39 session 3) - C3.6a working the land harder (D2-131, sim
  version 66)**: measured first (`--landtally`, new: harvests and COARSE working days by whether the village's arable within reach is short of its
  people's need, by crop stage): in land-bound villages labour never limited a stage (sown and brought in 1.000; 2.7-4.0 h a working day left after
  the crops in every stage) while the hungry gathered at 0.02-0.08 FU an hour against ~1.7 in the fields - the crop's work had a fixed need and an
  hour beyond it gave nothing. Built: at sowing a village whose fields are short of its want plans extra ploughing and hoeing a hectare (enough to
  bring its hectares' harvest up to the wanted ones', at most +80 % of the season's hours), at falling returns (+30 % at most; the first extra hour
  3/4 of an ordinary hour's harvest, the last a tenth); only the work done counts; history `FieldsWorkedHarder`, the settlement page's food section;
  `--notilth` (= `_m2` exactly), `TilthTests` 4. Ten seeds against `--notilth`: in bound villages the extra (0.41-0.63 of the season's hours) was all
  done, yields a hectare +16-24 %, harvests 0.83-0.97 -> 0.91-1.02 of the people's bread; lived days do it too. Then the people grew into it: years
  200-299 x1.17, year 300 x1.27, hunger a person-year unchanged (x0.98), famines a person-year x0.78, raids x0.37; run time unchanged. Full
  suite 340/340 (1000a8b). Web build of 1000a8b: Web test 6/6 twice (`s16web6`, `s16web6b`: WebGPU, bit-identical; the century-later world 472
  people in 13 places in both - the rule lives in the browser too); frame rates swung with the machine's load (the same build's two runs differ
  up to 2.6x in a view: village 98 / 55, person page 37 / 70 fps) - the views of the fresh world are untouched by the rule.
- **Session 16, second context, W-FOREST pass 1 (D2-130, presentation only)** - measured first: the wooded land is mostly hot (22-30 degC) or cool
  (2-10 degC), and seed 42's tropical forest was drawn as temperate lumps. Now each crown's kind of wood is read from the simulation's climate and
  water at its point (rain forest, dry tropical woodland, warm temperate, broadleaf, cool mixed, boreal, treeline, wet carr, river gallery); ten new
  forms (firs, krummholz, emergents, rain forest canopy, palms, umbrella trees, alders, poplars, mangroves, snags), stand ages (saplings, poles,
  veterans, snags after fire), openness and stature by kind, eight palettes, far forms per kind; camera views `forest:KIND`. Shots `wf2/sheet4.png`
  (four climates no longer confusable), Web test s16web4 6/6 (forest views 80-83 fps). Then the far canopy by kind (the terrain shader: a kind
  channel on the patches - the rain forest's deep greens and the dry tropics' olive from afar; `wf6/before_after_far.png`; Web test s16web5 6/6 on
  WebGPU). W-FOREST stays open for Arjun's judgement. Handoff checks: HouseholdFormation 3, Leaders 7, Gazetteer 2, Biography 4 pass.
- **Session 16, second context, later** - Web test s16web 6/6 on C10.3 (bit-identical; family plurals fixed in every page). Then the next slice
  chosen by measurement: **there is no famine amid plenty** (D2-126 corrected: at 95 % of ~40,000 hard-times hunger deaths the households with grain
  could spare under a quarter of what the grainless needed; the store held under a day's food in 69 %; deaths 5-12 days before the harvest) - hunger
  was chronic (19 % of deaths) and nothing answered scarcity before death: women married at 20.3 in every world and season. **C3.5 household
  formation (D2-129, sim version 65)**: a couple marries when what it brings of its households' grain keeps it to the next harvest of the village it
  will live in; otherwise the match waits, brides marry into villages with grain, some never marry. Pages (person family section, settlement
  overview "Marriages wait: ...", the marriage's record "setting up their household with N days' bread of their own"), `place --marriage`,
  `--nomeans` (= `_d3`). Ten seeds: hunger deaths x0.69, famines x0.70, births x0.88, deaths x0.87, people at par over years 200-299 (x0.935 at year
  300), conflict at par; age at first marriage 20.3 -> 21.4 (22-24 in poor worlds), never married by 45-59 0 -> 4 % (7-24 % in poor worlds).
  `HouseholdFormationTests` 3.
- **Session 16, second context (9 Oct 2026, from 12:22 IST; Control Room run #39 session 2)** - **C10.3 a contested succession splits a village
  closed (D2-128, sim version 64)**: at a vacancy the household with the largest following among those not bound to the new first leads them away to
  found a village at the best place the village knows when that following is half the first's and a village's worth; knowing no place, the faction
  looks for land (the village scouts for it) and goes when a place is known, or gives up after three years. Pages: a village founded by a split says
  why its founders left, the village left says when and where they went, its history tab carries its headmen and splits (a page-only index of
  political events: the storm code reads a village's last twelve indexed events, so the entity index stays as it was). Tools: `place --splits`, the
  `contests:` line (factions' outcomes). Ten seeds against `--nosplit`: 42 splits (the first version 10), factions of 6-16 households; land decides
  how many villages are founded (foundings 123 -> 119: splits take 35 % of them), factions in full lands give up (64 of 99); people, vital rates and
  conflict at par; villages stay smaller while land is open (home making x1.10, explained); towns of 400+ bind more households to the first (8 ->
  15 %) but keep large unbound factions. `LeadersTests` 7 (save of a faction looking for land; both villages' pages).
- **Session 16, later** - **C10.2 standing and the village's first household (D2-127, sim version 63)**: a household's following (its close kin
  households, those that owe it grain, those it fed in hard times or lent to lately - the patron tie outlives the debt); the household most follow is
  the village's first and its eldest grown member first among its people, kept until a rival is followed by more than twice as many, passing within
  the household at a death while it still qualifies; in hard times the first household's grain opens to the hungry (a gift; they follow it); tribute to
  a lord village is its first household's grain. Pages (settlement overview, person identity), `HeadmanRose` events, `leaders:` line, `place
  --leaders`, `place --binding`, `--nohead` (= `_h5`), `--notribhead`. Ten seeds against `_h5`: people x1.03 (t 1.0), vital rates at par; headship
  turns over every 4-12 years (the eldest lead; a patriarch's death breaks his children's households' links); binding to the first household falls
  with size (43 % of households under 100 people, 8 % at 400+): the evidence for C10.3 (a contested succession splits a village). `LeadersTests` 5.
- **Session 16 (9 Oct 2026, from 09:11 IST; Control Room, Resource Governor MEDIUM)** - **C3.3 exchange between households, first slices (D2-126,
  sim version 62)**: measured first - the session-13 rule (the harvest by each household's crop work) is an artefact of the COARSE schedule (residents
  walked in id order: households with no member of 16-59 "brought in" 64 FU a member against 40 for young couples); divided by mouths households are
  mostly self-sufficient year to year. Built (`Core/Places/Holdings.cs`): each household's grain in the village's granary (the harvest by mouths, its
  beasts' and catch's produce its own; heirs, married children, parties, arrivals), the village's common grain (what it was given, paid or sold), a daily
  account (outflows nobody drew borne by all by their shares); the moral economy (own grain, common grain, kin's spare as gifts, loans recorded as debts
  and repaid oldest first from a year's surplus); makers paid in kind - a household that takes wares or kiln pots pays their makers the food of the
  maker-days they took, as far as its grain pays (no credit: with credit the debts table grew to 206k slivers), the village pays its potters for jars and
  its smiths for their hours from its common grain, then by a levy on every bin; in hard times (a village ration under 0.7, Dirks 1980) sharing contracts
  to the household and its kin. Pages (person "home", settlement "Grain:"), headless `hold:`/`makers paid:`/`hunger by household:`, `place --debts`,
  `--nohold` (= `_s3` exactly), `--holdmeansoff`, `--nopay`, `--nohard`; tools ledger `--ledgeryears`. Ten seeds, each step against the last: the
  accounting moves nothing; purchasing power: home making x1.19, ware traded x1.38; hard times: famines x1.22-1.25, hunger deaths x0.90-0.95, takings/
  raids/blows down (fewer stores run dry: famine amid plenty), relief up; the slice against `_s3`: people x0.976 (t -0.6), vital rates at par. At year 300:
  Gini of grain a member 0.51-0.77, 6,458 of 15,979 households in debt (mostly a season's credit), creditors elders' households (one owed by up to 159),
  makers' households richer in all ten worlds, 98 % of hunger deaths in households with no grain of their own. Cost: +0.4 % from the same save.
  `HoldingsTests` 12; `ArchitectureTests` premise moved to the household's own grain.
- **Session 15 (8-9 Oct 2026, from 19:54 IST 8 Oct; Control Room run #31 session 5)** - **C4.2 carts closed (D2-122)**: the shots and the closing checks found three faults, each measured and fixed: (1) carts begun while hunger sent the village to the wild were never worked and given up (15.5 % of carts begun; 95-97 % of those given up in hungry worlds were given up short, `run --carttally`) - no cart is begun short now and a cart given up returns its timber: given up 4.9 %, outcomes at par; (2) **nobody works in the water (D2-123)**: the cart shots showed woodcutters with their handcarts out in the sea - a new audit (`wet --load S`: a lived world hour by hour, who stands under the drawn water) found 0.64 % of all person-hours in the water (field hands at a field spot the sea or a lake had covered in four villages, coastal woodlots, hunters); lived field, woodlot and wild-ground spots now go ashore (0.05-0.08 % left: straight walks across lakes) and woodlots are chosen above the shore, as quarries (ten seeds at par); (3) the yard camera framed a roof (now the parked cart with a clear line). Ten seeds `_c2` against no carts: trades x0.67, famines x0.89, people x0.97 (t -1.1), vital rates at par. **River crossings surveyed (D2-124)**: the largest flow on five worlds is 0.08-0.23 m3/s - every stream a way crosses is fordable; bridges would carry nothing (C4.3 deferred). **C1.4b the village smith (D2-125, sim version 61)**: measured - the person page called 5,730 people "a smith" and "a boatwright" (everyone who knew the craft) where 175 had worked at a smithy and 10 built a plank boat, and smithies had no smiths: every copper-competent person worked at one in turn (up to 355 hands and 24,000 hours in a smithy-year); now a smithy's four places go to its best smiths by craft and practice (`SmithBar`), practice (`Person.SkSmith`) quickens the hand, roles are read from practice (smith/ironsmith) and from the plank boats a person led (boatwright). A smithy with every smith working in turn meant iron towns smelted 40 % less with four places alone (`_s1`) and, with another smithy on short days alone, nearly every iron village raised three or four (`_s2`, rejected); the slice raises another only where the steady need (tool upkeep, iron sold, arms' upkeep) outruns the places. Ten seeds `_s3` against the same binary with `--nosmithcrew` (= `_c3`): people, famines, vital rates, trade and conflict at par; standing smithies 68 -> 80; smiths read from practice 24-116 in the three metal worlds, all of whom worked, at most 23 hands a smithy-year (355 before); iron smelted x0.59-x2.08 by world, iron traded 22 -> 9 t (seed 4's exporter cannot smelt beyond its places for buyers it cannot see: open, C5.3c). Tests: `ForgeTests` 8, `BiographyTests`, `CartsTests` 6, `WoodsTests`, `BoatTests`; full suite 314/314. Shots `c42a`-`c42c` (carts), `c14b` (Fomthaenstead's two smithies, carts at the woods). Web test 6/6 (`s15web`: browser = headless, views 77-121 fps, pages 69-88).
- **Session 14 (8 Oct 2026, 19:02-19:26 IST; Control Room run #31 session 4; stopped early by Arjun)** - investigated C4.2's open question (people over years 200-299 x0.955 with carts). No code changed. Per-person rates are identical (births 43.9 vs 43.8 a 1,000 person-years, net growth 7.20 vs 7.21; hunger deaths lower with carts); the loss is in the level: seed 4's comes from one bigger epidemic at year 180, seed 42's from an early chance split (no hunger, no sickness, 4 cart trade trips in 100 years; diagnostics `tc2/out/c42diag`). A placebo over all ten seeds (`--nocarts --nudge 20`, `_c1p`): people 200-299 x0.982 against `_c1n`, with a seed-to-seed spread as wide as the carts' (seed 1 x0.857, seed 5 x0.889); carts against the placebo x0.972 (t -1.06) - **inside the noise, no fault**. The carts' real effects are those the placebo does not reproduce: famines x0.83 (t -3.4), hunger deaths x0.93, trades x0.70. Left to close C4.2: D2-122, shots, the full suite and Web (SESSION_HANDOFF "Session 14").
- **Session 13 (8 Oct 2026, from 16:11 IST; Control Room run #31 session 3)** - **C3.2 the potter's kiln, the village's jars and its hearths' pots (D2-121, sim version 59)**: measured first - D2-063 gave every village that knew pottery (nearly all) a sealed store and every household with a potter cooking pots, also the 25-60 % of villages with no clay within reach; stores rot 6-57 % of harvests; cooking burns ~80 % of the wood. Now pots are made of real clay with real fire and break: clay by the ground's shape (a reach draining 200 ha or a lake's bed within 1.2 km - not the day's streams); jars (kg) in the granaries keep the grain they seal (0.65 rot), only from a kiln; a household that cannot fire its own cooks in kiln pots or on the full wood; a kiln at the clay where the store's grain lacks two months of a potter's jars, its four best potters (lived and COARSE, less the walk), ware from the clay and the village's wood; ware a trade good valued by the grain it keeps; the potter's role from practice. Kit `kiln`, `clay_pit`, `storage_jars`, `ware_stack`; pages; headless `pots:`, `--nokiln`, `--potsflow`, `place --stores`. Ten seeds `_k1` vs the same binary without kilns (`_k1n`): 84 kilns in 169 villages, 1,988 t of ware traded, jars sealing 80-100 % of the grain, trades x5.4, **markets x3.6 (23 -> 99)**, rot and people at par, famines inside the noise. `PotteryTests` 8; the bands rate test widened to six worlds (one lived band moved by a store a few per cent lower). Shots `c32a`, `c32b2`, `c32c21`. 302 tests; Web test 6/6 (`c32web`, views 86-120 fps, pages 68-84). Then **C3.3 surveyed and deferred as a phase** (a tools household ledger: the village's store moves 31 % of the food households draw - household property needs a moral economy, `26` §3). Then **C4.2 carts, IN PROGRESS** (sim version 60, committed d6c29cf): measured that 83 % of trading loads were bound by a back's load; households make handcarts (120 kg) or ox carts (500 kg) where their backs are too small three times within two years (trade on worn ways, wood or stone from 400 m+), in their own hours; cart loads on paths and roads, wood and stone home by the cartload; pages, view, kit, `CartsTests` 5/5. Ten seeds `_c1` vs `_c1n` (`--nocarts`): famines x0.83 (t -3.4), hunger deaths x0.93, trades x0.70, but people over years 200-299 x0.955 (t -2.4) - open (SESSION_HANDOFF Session 13).
- **Session 12 (8 Oct 2026, from 12:13 IST; Control Room run #31 session 2)** - **C3.1 the weaver's shed and the village's wares (D2-119, sim version 57)**: measured first - goods sit at target in three quarters of households, people idle ~8 h a day, home making costs only spare hours; fibre is the scarce input - so the slice was rebuilt: wares last (0.6 of homespun's wear: the same flax clothes 1.7x the people); a weaver's shed where a village's wear keeps six makers busy (larger places); its makers the village's eight best weavers (a labour day in COARSE, a lived choice), 1.5x pace and faster with practice, from real fibre only; households take wares before making their own; wares a trade good valued by the hours they save. Kit `weavers_shed` (open bays, looms, dye vat, yarn rack) and `tenter` (cloth drying while the village holds wares); pages; headless `wares:`, `--noweave`. Ten seeds against the same binary without sheds (`_w4` vs `_w4n`): trades x3.0, trade gain x3.8, home making hours x0.28, market places x1.35, people x1.04, births and deaths a thousand at par; wares are traded mostly for fibre (the flax chain), little grain (seed 4: 6,364 kg of fibre, 5,446 days' grain for 4,688 wares). **Lived save/load exact again** (pre-existing, found by the new tests): caches of the day's lakes and flows (the green, the drawn shore, the river lines) went stale in a running world; now keyed to the day's water or to the ground's shape. Works on the main road are laid beside it (a shed stood in the road). 293 tests; shots `c31v11`, `c31c5`, `c31b14`; Web test 6/6 (`c31web`, 74-121 fps, pages 76-85). Then **C3.1b more sheds (D2-120, sim version 58)**: another weaver's shed where a village's households drain its store most of a year with fibre to weave and its people in wares would outwear its looms - towns of 500+ now carry 2-4 sheds; ten seeds `_w7` vs `_w4`: home making hours x0.67, market places x1.18, markets x1.22, people at par (two rules rejected on the way: sales drained the store; the change-over from homespun); 294 tests; Web test 6/6 (`c31bweb`). Next: C3.2 the potter's kiln or C3.3 exchange between households.
- **Session 11 (8 Oct 2026, from 09:23 IST; Control Room run #23 session 3)** - **C13.3 a town's ground (D2-118, sim version 56)**: an audit found C13.2's lots were points 6 m apart - 90-312 pairs of houses overlapped a world and ~360 homes reached into a street, the green or a ring. Now every building covers real ground (`Core/Places/Footprints.cs`: a home its plan's rectangles turned to its recorded bearing `Building.Face`, a work a disc); lots, plans (the wanted plan or the next smaller that fits) and works are laid out only where their ground meets nothing; a town's (250+) street frontage near its green is divided into 6.5 m plots that take the new **narrow town house** (`Architecture.Row`, kit `home_row_g/h/j`: 4.6 m frontage, two storeys and an attic jettied over the street, gable to the street) - rows along the main streets; a **market place** widens with every trader who comes to a village's market (20 -> 36 m) and is kept open; homes left in a street, the market place or across a ring are taken down (`HomeCleared` 89); streets laid through as few houses as their way allows, lanes through houses shut until clear, rings between houses. The view draws by the recorded bearing in the village's own frame (houses had stood off their streets by a globe-dependent angle), exaggeration never pushes a house into its neighbour, gardens/pens/woodpiles/stacks/stalls on free ground, the market place trodden. **Rivers** (`Core/Planet/RiverLines.cs`, one line for sim and view): a river ends at the drawn waterline (the ribbon ran out over the sea) or runs on across a beach to it; homes and works keep 1.5 m off its bank (4-28 homes a world had stood on a river). Ten seeds `_d7` against `_c2`: **0 overlapping houses in every world**, none on a river, people x1.003, births/deaths at par, conflict inside the placebo's spread, run cost a person-day x1.2 (mostly machine noise by a profile); 285 tests; shots `c133a`..`c133f`; Web test 6/6 (`c133web`: views 64-120 fps, person/place pages 67-83 - the C13.2 dip was machine load). Next: C3.1 the weaver's workshop and wares (`26`). Forest/biome sameness recorded as the open **W-FOREST** task (`18` §8) - not solved.
- **Session 10 (8 Oct 2026, 06:15 IST - ; Control Room run #23 session 2)** - **D2-114 building variety closed**: one hall-house a village (one per 150 people); a household's means read from what does set households apart (beasts, a boat, goods beyond need, working hands, the years of grain in the village's granary - the first measure put 94 % of households in one band and every house was the same cottage); plans over ten worlds now one-room/cottage/long/two/hall 37/28/13/21/1 % (before 2/81/7/9/1); roofs kept and weathered (thatch re-laid on each house's own turn, tile and slate batches), kitchen gardens behind houses with working hands, doorstep yards instead of one bare disc, training grounds beaten by their hours, the smithy sooted by its forge's; the building and person pages tell how a house was built and why; headless `place --list/--households/--standing`. Ten seeds `_a5` against `_d5s`: people x1.023, famines x1.065, hunger x1.15 (all inside the noise); conflict counts move as much under a placebo (`_a5p`); run cost +4 %; 275 tests; Web test 6/6 (`d2114web`, 59-122 fps); shots `arch1`..`arch4`. Then **C9.4 surveyed and deferred** (D2-115: relief by sea bound by the boats' room in 0-19 of 69-538 gifts, no voyage ever left unsailed - cargo boats and docks wait for exchange, C3) and **C13.1 streets** (D2-116, sim version 55): a way worn to a path is laid as a durable street; frontage lots are sought, so villages grow along their roads and houses face them; nothing is built in a street; ten seeds `_b2` at par in people and famines, raids x0.56 by a traced mechanism (more homes near the store: takers likelier seen); 277 tests; Web test 6/6 (`c131web`, 75-123 fps); shots `street1`, `street1y100`. Then **C13.2 back lanes** (D2-117): each street has a lane either side 36 m out, lined after the street's frontage near the green; houses face their street or lane; ten seeds `_c2` at par with `_b2`, 48-80 % of houses front a street or lane; shots `lanes1` (Vumthorpe at year 300 reads as a town of streets and lanes). Next: C13.3 (rows and the market place) or C10.2 (the lord's seat, gates and towers).
- **Session 9 (8 Oct 2026, from 04:31 IST)** - **C9.3 closed (D2-113)**: relief by sea's extra famines traced (seed 2 chance - an epidemic; seeds 11 and 8 relief as an unconditional subsidy holding land-short villages over their land) and fixed by **social storage** - relief owed back, given back when the harvest allows; **lived voyages** (booked by the village day, rowed by the household's crew along the sea way, the load dealt at the other landing, home together), at par with COARSE; ten seeds `_d5s` against `_c92r`: famines x0.971, hunger per person-year x0.999, people x0.955; sim version 53, 269 tests; Web test 6/6 (`c93web`); shots `voyage1`, `voyage2`. **D2-114 building variety** (Arjun's new standing requirement) in progress: a building's form recorded when raised (`Architecture`: plan, roof, cover, walls, chimney from the household's size and means and the village's size, lots, ground, knowledge; traditions carried by founders with drift), households of means rebuild old or poor houses (labour and wood), the kit's house families (5 plans x 3 roofs, framing, chimney, plinth, lean-to) and new barn, granary, smithy, market; sim version 54; `ArchitectureTests` 5/5; Unity compile, shots, ten-seed reading and a cap on hall-houses still open (SESSION_HANDOFF "Session 9").
- **Session 8 (7-8 Oct 2026, from 22:40 IST; Control Room continuation)** - **C9.1 closed** (D2-110: ten seeds after the landing fix found boats in the square and in the sea - fixed, `_c91k` clean and at par; Web build 5/5 at 68-120 fps; shots `boats2`). **C9.2a the landing and the crew (D2-111)**: one landing a village, kept, boats in a row on its sand a metre above the sea; a boat goes out with its crew in lived days (C9.1's lived days caught a third less from boats than COARSE ones: x1.23 -> x0.90 COARSE/lived over 5 worlds x 24 days); shore fishers and people at a drowned square no longer stand in the sea; ten seeds `_c92c` at par with C9.1 (people x1.001); sim version 50; 258 tests (14 boat tests); shots `boats4`, `boats5`. **C9.2b boats of planks (D2-112)**: a technique found by woodworkers at a dugout and carried by people; a boat shed at the landing; plank boats built there one at a time by those who know the craft - four aboard, out in harder weather, rowed faster, reaching more of the shoals, lasting 25 years; ten seeds `_c92r`: the craft in 7 of 10 worlds, 9-26 villages and 20-47 plank boats by year 300, people at par; sim version 51; 262 tests; shots `planks2`; Web test 5/5 at 73-120 fps (`c92web`). Web test 5/5. **C9.3 voyages (D2-113, in progress, committed as such)**: ways over the sea between landings, a boat's load in trade (rare: barter), grain by sea to famines beyond a day's walk (large); open: famines x1.18 with relief by sea - find why (D2-113 "Open").
- **Session 6 (7 Oct 2026, from 10:56 IST, in progress)** - Arjun's **major direction update (D2-089)**: TC2 is an *autonomous medieval
  civilisation inside a living miniature planet* (Age-of-Empires breadth and readability, no player orders; fewer, deeper people; every
  structure, road, army, ship, mine, wall and kingdom from simulated need). Done: state restored (clean at 057420a, nothing
  half-finished); the audit (`20` §4 - one store per village, three works as bits, no workplaces, transport, roads by use or deposits
  but copper; **woodcutting and fields leave the simulated forest untouched**; one text box for a person, nothing else clickable);
  research (bloomery yields, transport cost ratios, trail formation, army rations, village layout, ship timber); the design docs
  `20_CIVILISATION_DEPTH.md`, `21_SETTLEMENT_ECONOMY.md`, `22_MATERIALS_AND_RESOURCES.md`, `23_MILITARY_AND_WAR.md`,
  `24_TRANSPORT_AND_MARITIME.md`, `25_CIVILISATION_ROADMAP.md`; pointers in 00, 07 (§9 the person page), 08, 09, 10, 13, 14, 17, 18.
  **C1 "the working town"** (`25` §2): **C1.1 structures done (D2-090)** - the kinds table, well/granary/barn as structures with
  capacity (grain beyond rots 4x), works by need with their causes (`WorkBegun`/`WorkBuilt` under the building), placement by zone, the
  building page (click a structure), kit models (barn, smithy, quarry face, stone pile, training ground, market stalls); 185 tests;
  300 years DEEP at par with the baselines (`tc2/out/civ/s42_c11c`, `s7_c11c`; 17 barns and 56 granaries in 18 villages on seed 42);
  shots `tc2/out/shots/civ1`. **C1.2 timber from the woods done (D2-091)**: felling takes forest at a woodlot that moves as the woods
  recede, the walk paid per load; fields clear what they farm (cleared fields fully arable: seed 42 arable +39 %, people 5,295 -> 7,481 at
  year 300; 41,600 t felled, woodlots ~770 m out); 190 tests (rate checks pooled over worlds); shots `civ2`. **C1.3 stone done (D2-092)**: rock known by
  seeing it (80 m land breaks, within 1 km, above the shore), quarries at the rock, every load carried home, houses on stone footings
  (seed 42: 13 of 19 villages quarry, 2,079 of 3,278 houses on footings); walls before non-essential works; 195 tests; shots `civ3`.
  **C1.4 the smithy done (D2-093)**: a smith's workshop (twice the open hearth's pace, less fuel), arms forged only there by villages
  that fear their neighbours, armed fighters strike 1.3x; rings enclose works; smoke while a smith works; 200 tests; every village has a
  smithy by year 300, 18 of 19 armed (seed 42); shots `civ4*`. **C1.5 training done (D2-094)**: fighting skill from practice (a
  two-hour session every third day for the bold of a fearing village) and fights survived, fading unused; strength x (1 + 0.6 skill); seed 42
  at year 300: 16 grounds, a third of the living with some skill, 647 who have fought; 204 tests; shots `civ5`. **C1.6 markets done (D2-095)**: markets made by traders
  (10 a year), drawing them from 9 km and dealing 1.2x better; palisades on dry ground only; seed 42 has 2 markets at year 300; 207 tests;
  shots `civ6` (Heldholm's green with its stalls). **C1.7 and C1 closed (D2-096)**: tier-A worlds read
  through `run --civ`: peace where land is plenty, famine -> takings -> watch -> training ground -> palisade where it is short, an armed feud
  where copper smiths forge arms (seed 11); acceptance sheet `tc2/out/shots/civA_sheet.png` (a hamlet of 84 -> a walled town of 358).
  **C4.1 roads by use done (D2-097)**: ways worn by walking (track/path/road, halving each year unwalked), quicker underfoot, drawn by use
  only (ribbons for the beaten roads at the region view); seed 42 at year 300: 74 paths, 17 roads; 210 tests; shots `ways1`, `ways2`.
  **C7.1 rings that grow done (D2-098)**: a larger ring when homes outgrow a sound palisade, the old one inside until it rots; walls before
  hut upgrades; **a float stall fixed** (works a hair short of a non-round need never finished: palisades stood "rising" for a century);
  seed 42 at year 300: 18 walled (was 8 + 9 stuck), 5 larger rings; 211 tests; shots `rings1` (Thathastwick's two rings).
  **C12.1 the person page done (D2-099)**: `Core/History/Biography` - identity, now and why, family, work (roles read from practice), deeds and
  force, home, life (every record, in order); the HUD's "Life page" (tabs), WebBridge `person`, headless `person --load S --pick fighter`; reads
  only; 214 tests. **C5.1 copper from the ground done (D2-100)**: ore bodies on crags near volcanoes (15 %, 1-20 t, kept from the first
  sight, counted down by what is dug), seen where people go, a mine at the body when someone can smelt and metal is short, every load carried
  home, smelting takes 5 kg of ore a kilo, bodies give out; tuned from "every village knows copper" to seed 42 12 of 19 (7 bodies dug out in
  300 years), seed 7 7 of 9; 220 tests; shots `mine1`, `mine2`. **C5.2a charcoal done (D2-101)**: a hearth at the woodlot (following the
  woods), burners felling real forest, 5 kg a load, clamps smouldering; smelting burns charcoal (none, no copper); seed 42: 14 hearths, 21.9 t
  from 109 t of wood; 225 tests; shots `char1`. **C5.2b iron done (D2-102)**: bog ore on wet flats (forming again in 40 years), pits, the
  bloomery found by copper smiths, 8 kg ore + 15 kg charcoal a kilo, iron tools (2 kg a head, 1.4x) replacing copper, iron arms 1.15x; a
  COARSE smith now works a labour day at the forge (year parity); seed 7: 12.3 t of iron, iron tools for 61 %; 232 tests; shots `iron1`.
  **C5.3a places by their ore done (D2-103)**: scouts of a village lacking ore value land by ore they see (1.5x), founders know the ore round
  their site; eight seeds compared (no systematic change; iron's arrival contingent: seed 2 lost it, seed 4 gained 20.6 t); 234 tests.
  **C5.3b iron traded done (D2-104)**: iron a trade good valued by each village's lack; smiths make for buyers known from real sales;
  seed 7: 3.4 t of iron traded, trade gain x3.6, markets 0 -> 2; 235 tests. **C5.4 whose ore done (D2-105)**: worked bodies belong to
  their workers; others dig only in pressing need or with a wrong held, and each stint is a taking the owners know; on seed 7 an ore taking
  caused a taking-back of grain within a feud; 238 tests; `run --events KIND`. **C12.2 the settlement page done (D2-106)**: `Core/History/
  Gazetteer` (founding, people and homes, food, works, land and making, knowledge, neighbours and wrongs, history), HUD from a person's card
  or a structure's page, headless `place`; 240 tests. **C10.1 the lord's call done (D2-107)**: tributaries' people answer their lord's call
  against those who wronged it; 243 tests. **D2-108 the lord answers for its tributaries** (the C10.1 drops followed - session 7): ten seeds show
  no systematic loss (people over years 200-299 x0.992; seed 2's drop an epidemic, seed 7's Voton's famines), but the lord's share was a
  second wrong (one raid avenged twice; the lord's watch, walls and taking back moved by harm not done to it) - now a lord weighs its
  tributaries' wrongs at half and its band's loot answers the tributary's wrong; 244 tests; ten seeds at par with C5.4.
  **D2-109 the Web frame rate**: the frame was bounded by the main thread's draw submission (one draw per colour group, the whole world as
  bounds, into every cascade); per-instance colours and draws with their own bounds - 34-37 instanced draws a frame; settled close
  60-70 -> 90-102 fps, forest 66 -> 100; Web test 6/6 with every view 73-120 fps (after a century 88).
  **C9.1 boats from the woods (D2-110)**: dugouts made by fishing households from their woods, rowed out to the open sea's shoals, lost in
  rising wind. **Closed in session 8**: the ten-seed rerun found boats laid in the square (and twice in the sea) where the ground fished lay
  cells out from the shore - fixed (`Boats.ShoreOf` ring search; boats follow the coast both ways, `ShoreMoved`; in the water only below the
  waterline, `Afloat`); ten seeds `_c91k`: no boat in the water or away from it at years 100/200/300, people x1.01 (t 0.4), fish x1.57
  against D2-108; 253 tests. Web build: test 5/5, every view 68-120 fps (`c91webb`); shots `boats2` (boats on the sand; two faults seen - boats on one spot, shore fishers standing in the sea - fixed in C9.2a).
  **Browser**: the settlement page renders; the true Web frame rate is 35-56 fps near the ground (below 60: earlier 57-121 fps readings
  since C1.4 were inflated by the smithy error stopping the view's setup). **Next**: SESSION_HANDOFF "Next".
- **Session 5 (7 Oct 2026, from 06:40 IST, in progress)** - Arjun (7 Oct, D2-085): after D2-083, **stop before D2-084's polish and
  reconsider the planet's physical size** (D2-007's 10 km reopened). D2-083 closed and committed (22cad8d: 177 tests, COARSE hashes
  unchanged).
  - **D2-085 decided and made: R = 6 km** (`19_PLANET_SCALE.md`). TC1 was a diorama (1,442 cells, radius 10 units, a person R/137, a
    village's map 0.32 R); a TC2 village with its fields is 0.17 of a 6 km globe (TC1's map 0.16, 0.10 at 10 km). Four seeds at 10/7/6/5/4
    km, 400 years DEEP, the same ground on each: 6 km keeps 2,000-7,100 people in 10-30 villages on the normal seeds (10 km 4,900-19,000;
    5 km falls to ~1,250 in 8 on the weaker seed), a village's neighbourhood unchanged (~9 villages a day's walk; kin, brides, trade at
    3-4 km), the world ~2 hops across instead of 2.5-3.7, about half of it reachable in a day; -25..-36 % a world year; great beasts few
    (bears and seals lost on some seeds). The same ground at any size: the geophysics in geo-lengths of a 10 km sphere; living laws per
    hectare (`AreaScale`) and real-size patches (`Patch`); first villages in metres; new homes on dry lots; the view relative to R
    (framing, relief, clouds, LOD, haze). Tools: headless `--radius`, `run --scale`; shots `--radius`. Shots: `tc2/out/shots/scale1`,
    `scale2` (contact sheets). Tests at 6 km: eight 10 km premises restated, no law loosened.
  - **D2-084 on 6 km, the multi-age baseline** (`tc2/out/shots/ages6`, sheets `sheet_s42.png`, `sheet_s7.png`; seeds 42 and 7 at years
    0/25/100/300/1,000): history reads without the date at orbit and region (woods -> clearings -> a farmed heartland; seed 7's woods
    down to a copse by 1,000). Failing: villages sprawled over centuries (fixed, D2-086: homes 128 m from the square at year 1,000, was
    212), the mature land a brown quilt at the year's first day (shots at other seasons next), people ~3 px at the village view, rivers
    hairlines, far views at their own local time (a scar shot at night).
  - **D2-087, D2-088** (shots `ages6b`, `drown1`, `shore1`): seasons read (summer green, harvest gold: the brown was the year's first
    day); homes the sea takes are lost (`Homes.SeaTook`: drowned ruins, the household to dry land, "The sea took N homes of X"); one
    shore for the simulation and the view (`Core/Planet/Shore.cs`): no home is drawn in the water. View: people at least ~4 px tall
    (at most 4x) so activity reads at the village view; a `street` view (32 m). 179 tests.
  - **Known bug, its own calibrated slice** (it changes COARSE): a COARSE disease catch that falls on a child is dropped
    (`Disease.Visits` picks among everyone present), halving the sickness visits carry home.
- **Session 4 (7 Oct 2026, from 00:16 IST, closed into session 5)** - Arjun (7 Oct): finish D2-083, then **the world made legible** is the
  priority (D2-084, `18_WORLD_READABILITY.md`, roadmap phase W - "the simulation exists, but almost none of it is legible from looking
  at the world"; no simulation theatre; the order audit -> composition -> forests -> rivers -> biomes -> terrain -> settlements -> life
  -> scars -> orbit -> multi-age acceptance -> browser performance; the UI art pass recorded for later). Same machine rule (~50 % CPU
  and RAM; ordinary low-priority runs need no permission).
  - **D2-083 closed (DEEP calibration)**: the step ration removed (the step's work reads its first day's ration), a pressing need's
    hours given each day of a step, each worker of a step choosing on the ground the step's earlier workers left (`WildFood.Retake`).
    Six seeds (round j): people at par at every century (0.85-1.00, n.s.), births, deaths, conceptions, harvests per person at par;
    known residual leans in the wild choice (hunting 1.34x, fish 0.81x, out for need 1.09x), smelting and tributes per person-year.
    A tried "walked take" (round k) made it worse and was reverted.
  - **Visual slice 1 (D2-084)** written, being shot: stands and glades, the open land's palette, relief from afar, rivers and their
    corridors, seven tree kinds in variants, less blur and haze (13 §10).
- **Session 3 (6 Oct 2026, from 17:57 IST, closed into session 4)** - Arjun's direction: the living world made visible first (D2-079), then
  F8. **Operating constraint: keep the shared machine under ~50 % CPU and RAM** - it already sits at ~66-70 % RAM from other
  people's work (Chrome, Oracle, another Claude session with a headless Chromium on SwiftShader at up to ~76 % of all threads), so:
  one heavy TC2 job at a time, launched only when `scratchpad/quiet.ps1` finds the machine quiet (CPU < 40 %, >= 3 GB available),
  at below-normal priority (`scratchpad/lowrun.cmd`: children inherit it); the batch editor's shot round takes ~2 GB and paged the
  machine once (available 0.45 GB) - never start it with less than ~3 GB available. Compaction hooks re-verified on Claude Code
  2.1.291 (PreCompact exit 0 stdout joins the compaction instructions; SessionStart `compact` stdout reaches Claude).
  - **The woods as masses, the land's cover, the villages' signs (D2-081, slice 2 of D2-079)** - seen in six editor shot rounds
    (`tc2/out/shots/woods1`-`woods6`): the far woods are the near canopy averaged (a cover texture whose mips are the share covered),
    with clumps lit and floors shaded, a second turned and scaled lookup past 3.5-7 m a pixel and the exact mean past 10-20 m, stands
    of a few hundred metres - region and orbit read as forest (and savanna, where the share thins) without speckle or tiling (the first
    try, clumps baked into the 156 m tile, showed a grid from orbit: now nothing in the tile may pattern; clumps live in the share);
    leaves olive by the climate's dryness; ground cover by the land (meadow grass patchy in density and tone, flowers in their season,
    shrubs, ferns under the woods, heather, tundra cushions and stones, dune grass, reeds, boulders); round, tall, spreading and birch
    trees near the camera, young trees where fire passed; beaches and the sea floor per pixel (the coast's staircase gone); smoke from
    huts and camps, woodpiles by the days of wood, a watch fire, villages glowing at night from orbit; herds at the village view.
    Tools: `shots --views view+D@H`, `build-web --jobs N`, the bridge's `off cover`.
  - **The woods in the browser (D2-082)**: the D2-081 build dropped the close and low views to 40-49 fps - gathering the ground
    cover's instances every frame; now gathered only when patches, content or the camera change (and every 15 frames), patches out of
    view left out, cover sorted for long runs, trees and cover re-placed at most once a world day: settled close 65, low 69, forest 68
    fps (`s3webb`, 6/6, probe unchanged). The catch-up holds the camera at orbit and takes 90 ms of each frame: a day away 48-49 s at any
    camera (was 56-57 s from a village view, 54 s at s2webf). Web builds share the machine (`--jobs 3`: IL2CPP, Bee, emscripten,
    binaryen capped).
  - **F8 started - DEEP (04 §4.3b)**: in COARSE a calm village (fed, nobody sick or hurt, no band, no party leaving) lives up to K = 12
    days at a step, never across a day of its calendar (sowing 0, growing 12, ripe 30, harvest 39, leaving 41); every part of the
    village day takes the step (crop calendar walked day by day; larders for the step; decay (1 - r)^K; bodies K x (share - 1) with
    health day by day; deaths, marriages, conceptions, raids, trips, visits, spillover, taking back at 1 - (1 - p)^K; labour, chores,
    making and herding for the step; learning's closed form); one-day arithmetic unchanged (all 169 tests, and the Web probe, identical);
    `World.StepDays` (a logged command `SetStepDays`, saved, hashed only when > 1; sim version 31); Ancient and Deep time catch-ups plan
    DEEP (`OfflineProgress.PlanCatchUp(.., mode)`, ending 13 days before full activity); `headless run|bench|chronicle --deep K`;
    `DeepTimeTests` (6). Saturated world: 1.8x COARSE (villages 2.3x; events still happen at their rate, and the planet now matters).
    **Calibration (D2-083, in progress; 400 years, `tc2/out/deep/`)**: the first DEEP runs were biased - people -15..-37 % at year
    200, cattle x3 and pigs x0.2, relief x0.5, wild hours +10 %, scouts x0.75, raids and claims up. Each bias was a step forgetting
    its days; six rules now keep them (04 §4.3b): gates open on their day (nursing's end, 16th/18th birthdays, a beast's maturity and
    season, scouts every six days), changes walked through the step (a beast's condition, the crop's season curve and its later days
    grown after the step's weeding, death on each day's health, the step's rations, the woods at its middle), day caps kept a day's
    (catch shares, grazing, culling at the leanest pasture, the smith), the store seen as days lived hold it (`StoreSeen`, sim version
    32), chances once a day, and no step across a plant pulse (`World.NextPlantTick`). COARSE hashes unchanged; 176 tests pass.
    Small worlds now match on wild work and harvests; DEEP's early growth still runs ahead (seed 1 +9 % people at year 100, three
    times the households leaving). **The multi-seed run with the newest build is pending** (see Blockers).
  - **Known COARSE bug, its own calibrated change after D2-083** (Arjun: not inside D2-083, since changing COARSE moves DEEP's
    reference): COARSE hunters never improve their skill and their trips are not counted - the line sits inside a comment in
    `AgentSystem.CoarseDay` since D2-063. Fix it after D2-083 closes, recalibrated against ACTIVITY, with new COARSE baselines.

- **Session 2 (5 Oct 2026, in progress)** - compaction-recovery hooks (`.claude/settings.json`, `.claude/hooks/tc2_compact.mjs`;
  Claude Code 2.1.289: PreCompact stdout joins the compaction instructions, SessionStart matcher `compact` stdout reaches Claude;
  validated with `claude doctor`). **F1/F2 10,000-year reruns on the current build (D2-041) done - F1 NOT closed**:
  - seed 42 (`tc2/out/f1/run_42i.*`): glaciation from year 188 that never ends and deepens (global mean 15.0 -> 10.1 C, ice 19 ->
    37 % of land, sea down to -100 m at 4,000-5,000 then -55..-62 m, CO2 rises to 537 and holds ~460 ppm); **relief drift**: mean
    land elevation 160 -> 621 m, continental crust 47.8 -> 60.9 % of cells, peaks 2,165 -> 3,456 m.
  - seed 7 (`run_7i.*`): a long glaciation (117-7,854), then two shorter cycles (8,015-8,955; 9,380-); sea +0.6..-25 m; forests
    0.18 -> 0.27; the same drift (mean land 105 -> 302 m, continental crust 61 -> 67 %).
  - Diagnosis: plates move ~100x Earth's rate (a world law) but erosion was not scaled with them: net uplift over all land is
    ~30-46 mm a year against ~0.3 mm of denudation, and accretion welds 35 % of subducted samples onto continents. Higher land is
    colder land: seed 42's permanent, deepening ice age and its -60..-100 m seas follow from the drift (seed 7, with less uplift,
    stays inside 01's +-40 m). **Next: an erosion/uplift balance (relief at dynamic equilibrium) and slower continental growth,
    then rerun.** F2: no new kind in 10,000 years on either seed under D2-041's rule (done-when unmet); all 13 kinds survive.
  - **F3 slice 1 built (D2-042)**: hunting, fishing and gathering from the same fields (`Core/Places/WildFood.cs`; daily ground
    survey in settlement state; whole animals by keyed Poisson; fish continuous; mast tallied in `P.Gathered` and taken from the
    boar's and bears' share), carrying the catch home (`Haul`), skills `SkHunt`/`SkFish`, body reserves (`Person.Reserve`, days of
    full food; fasting adults live 36-60 days, children 2-5 weeks, babies on their mother's milk), hunger-sized portions (people ate
    2.7 meals a day at fixed 1/3 FU: ~10 % under need), prudence and famine memory (share of adults who lived the last famine),
    history Rationing / WildFood / Famine; COARSE uses the same choice and catch functions (hours fitted to ACTIVITY: ordinary
    1.6 vs 1.1, shortage 7.3 vs 8.5 h per adult-day); save sim version 10; 83 tests.
  - Evidence: a 300-year coarse run of seed 42 now grows 145 -> 5,597 people (session 1: 1,428 by 200, then 388 with 4 villages
    lost); all six villages live; villages of ~900-1,300 on land for ~300 deplete the game within reach by ~95 % (hunting
    0.7 -> 0.1 FU/h) and sit in recurring famine (4,267 hunger deaths, 43 famines from year 216) - nowhere to go yet.
  - **D2-043 (relief and ice)**: relief experiments (`tc2/out/relief/`, planet-only 3,000 years, `planet --budget`): erosion scaled
    to the fast plates (K 5.5e-3, slopes creep and wash toward their rivers Kh 2e-4/yr, failure past gradient 2.5, mouths keep
    15 %, the rest to the deep), floating thickened crust (0.15), fewer welds (0.1), **ice flowing 9x faster** (thin sheets) -
    seed 42: continental mean 86 -> 78 m in 3,000 years (was 98 -> 157), sea 0 to -18 m with glacial cycles on the tilt (was a
    permanent ice age at -60..-100 m): the +-40 m law stands; ages of ice named against a 1,500-year running mean.
  - **D2-044 (densities, drift)**: `Ecology.Density` 0.15 (game ~30-45 kg/ha, deer/boar ~15-20 a km2), lineage drift 0.02.
    Hard hunting now leaves 18 % of a control's game; eight years later 98 %.
  - Save sim version 11. **10,000-year reruns on the corrected laws (planet + ecology, no people)**: seed 42 (`run_42j.*`) -
    **F1 passes**: continental mean 94 -> 78 -> 89 m (+-10 %), crust 47.7 -> 47.3 %, peaks ~1,350-1,500 m; four ages of ice
    (100-2,400, 2,900-3,700, 4,100-4,900, 5,500-6,100) with interglacials; ice 14-29 % of land; sea 0 to -20.5 m; 12.5-15.5 C;
    CO2 280-409. F2: six new kinds in years 430-830 (three sand wolves on the same range, two sand foxes, warm-water sardine
    and tuna), the 20-kind cap, then none for 9,000 years, no extinction: sister kinds shared nothing and splits came in a
    burst - D2-046 (sister kinds share territory; drift 0.015; gentler split chance) in progress. Seed 7 (`run_7j.*`): continental
    mean 5 -> 16 -> 3 m, crust 61 -> 60 %, peaks ~950-1,300 m; seven ages of ice (100-1,100 ... 9,600-) with interglacials, sea
    +0.5..-7.8 m, ice 5-22 % of land, forests 0.17 -> 0.25 as CO2 drifts 332 -> 256 ppm; the same F2 burst (seven kinds by
    1,150, one extinct). Maps (`tc2/out/f1/maps42j`): the continents rearranged over 10,000 years (the inland sea closed, three
    continents apart), new coasts and rivers. **F1 CLOSED** (drift, ranges rising and wearing in balance, glacial cycles, coasts,
    forests, stable over seeds 42 and 7; the orbit look to re-shoot with the current build). F2: D2-046 validation running
    (`run_42k.*`, `run_7k.*`).
  - **F3 slice 2 (D2-045)**: construction by labour and wood (camp -> hut -> house; well, granary; the view draws what stands),
    knowledge of places by scouting, households leaving after the harvest (founding parties to known places; joining villages
    with land), parties walking and founding unfinished villages. Seed 42, 300 coarse years: 6 -> 35 villages, 145 -> 11,744
    people, famines 56 -> 3. 88 tests.
  - **Herds near people (D2-047)**: wild herds and packs as identities over the abundance field (89 tests). At scale their
    lookups dominated (2.9 ms per COARSE village-day at 29 villages): a per-village herd index brought it back to 28 us.
  - **Livestock (D2-048)**: cattle, goats and pigs as individuals of households, grazing the same `P.Grass` as the wild (pigs:
    the same mast, and scraps), breeding with a sire near, milk and meat home like the hunters' catch, wolves, a breeding core,
    stocking to hands and pasture (the autumn slaughter), endowment at marriage, inheritance, feral when a village is abandoned.
    Seed 42, 200 coarse years: 38 cattle, 1,128 goats, 103 pigs for 3,539 people (~8 % of food); 100 tests (`LivestockTests` 11).
    Save sim version 13.
  - `planet --nopeople` made one village anyway (`Math.Max(1, ...)`): with migration (D2-045) it grew to ~13,000 people in 36
    villages over 2,000 years - the first k runs were not planet-only (kept as `run_42k_people.*`: one village of 24 fills its
    land in ~500 years and holds ~12-14,000 people for 1,600 years, no collapse). Fixed (no settlement at 0).
  - **F2 verdict on the clean D2-046 runs** (`run_42k.*`, `run_7k.*`, planet + ecology, 10,000 years): new kinds one or two at a
    time from ~850 (seed 42: 14 at 900, 15 at 1,200, 17-20 at 2,100-2,400; seed 7: 15-20 between ~1,400 and ~2,500) - no longer
    a burst - then the 20-kind cap and no change for 7,500 years: no kind ever empties in a continuous field (seed 42 keeps 3
    warm-water tuna, 14 seals for millennia). F2's done-when is met (cycles, recovery, adaptation, speciation); for turnover a
    **minimum viable population** (a kind under ~50 real animals may die out by chance each decade) is in validation
    (`run_42m.*`, `run_7m.*`). At 50 x Density (7.5) seed 42 lost its seals in the first century (`run_42m_mv75.*`): the fields
    hold real numbers since D2-044, and the miniature's seals (~11-15) and bears (~26-45) are small whole populations - the law
    is now **a handful (5 animals)**: seed 42 keeps all 13 first kinds and its seals for 10,000 years, new kinds at 900, 1,200,
    2,100-2,400, two kinds lost and replaced at 3,200-4,200, then the cap for 5,800 years; seed 7 likewise (cap by 2,500,
    turnover 2,900-4,200, all first kinds kept, 63 seals). **F2 CLOSED (D2-046).**
  - **Farm animals, herds and camps in the view (D2-049)**: `LivestockView` (by day on the pasture, at night in fenced pens
    behind the homes, walking out and home; pigs at the yards), wild herds drawn from the simulation's herds (kept to a
    village's edge), kit cow/goat/pig and a hide tent for camps. Shots `tc2/out/shots/stock*`, `herd1`.
  - **The village herd (D2-050)**: the households' cattle and goats graze together on a ground of the day with one herder per
    60 head, children of 8-15 first (`ActivityKind.Tend`, `WhyCode.Herd`; COARSE by turns); a watched herd a third as exposed
    to wolves; herders keep a mixed herd and kill into the breeding cores in hard times. Seed 42, 200 coarse years: cattle 139,
    goats 355, pigs 234 (before: cattle 6 as the fields took the pasture); wolves 4,570 (was 17,402). Sim version 14; 104 tests.
  - Speed: herd claims in a flat array (survey 74 -> 55 us per COARSE village-day, hash identical); the profiler splits the
    survey (grounds 13, herds 41 us) and shows livestock (~32-37 us).
  - **Web verified on both paths (D2-051)**: WebGPU 6/6 bit-identical (village 83, close 79 fps; a day away 39 s); the WebGL2
    fallback measured for the first time: its village view ran at 37 fps (the woods' shadows in four cascades) - now one
    cascade there: 60 fps, 6/6 bit-identical; open: WebGL2's first-entry stall at a new view (1-5 s; `tc2/scripts/webperf.mjs`
    measures views layer by layer).
  - **Ruins (D2-052)**: an abandoned village gives back its fields and its ground (any village may claim or found there) and its
    last households' houses stand as roofless ruins that sink and green over the centuries (`shots --abandon YEARS`).
  - **1,000 coarse years of seed 42 with people** (`tc2/out/long/run42_1000*.txt`): one cluster of 6 villages grows to ~11,600
    people in 35 places by year 300 and holds 12,000-14,800 in 37-42 places to year 1,000 - saturation, no runaway, no
    collapse of the whole; but famine-regulated: ~140 hunger deaths a year (1 % of the people) and 1,791 famines (each village
    about one in 22 years) once the land is full - no relief or trade between villages yet (F5). All three kinds of livestock
    kept (113 cattle, 1,327 goats, 470 pigs at year 1,000); 84 households move a year, 41 foundings in all; 220 scouting trips
    a year. Cost: ~1.2 us per person-day in COARSE (a village-day 415 us at ~350 people) plus fires (35 % of the run: big
    wildfires stepped hourly); the history ledger grew without bound (household events kept for ever: fixed, 780k -> bounded).
    **Consequence for offline catch-up**: a saturated world (~14,000 people) costs ~1 s a world year headless, several times
    that in the browser - "a day away" (a century) would take minutes; the catch-up must scale (cheaper person-days, or the
    Living mode's span adapting to the world's size).
  - **The fire store (D2-055)**: fire steps cost 1.15 ms in the 1,000-year run against 3.9 us without people - not big fires
    (planet-only tally: ~400 fires a century, ~15 cells, ~54 hours) but `PagedStore` keeping every emptied newest page of
    fire ids, which are sparse among the herds' process ids (887,000 ids in 150 years): 379 pages walked row by row each step.
    Fixed (pages freed once ids move on, empty pages skipped): 131 -> 2.6 us a fire step, 65 -> 41 MB heap at year 150, hash
    identical. DECISIONS also recorded D2-053 (seal haul-outs) and D2-054 (the ledger keeps what matters to the living).
  - **The household's own work and warmth (D2-056)**: following people (`follow`, `timeuse`) showed grown people idle most of
    the day and houses and chimney smoke meaning nothing to the simulation. Now meals are made ready (grinding and cooking, ~1
    FU an hour, on the village's wood), water is carried from the well as the pots empty, and the hearths burn the woodstore
    against the day's cold: a roof and a fire keep a home warm, a cold home takes health (infants and the old most) and can kill
    (`ColdHomes`); children herd, carry water and help at the quern; COARSE charges the same work to the same people. 111 tests.
    Time use (seed 42, sowing): grown people cook 1.3 h, cut wood 0.6 h, field 2.5 h; still 7-9 h idle outside sowing and harvest
    - crafts, clothes, hay and infant care are the missing work (next).
  - **Goods, a day's labour, weeding (D2-058)**: households make and mend their clothes, tools and vessels (worn goods let the
    cold in and slow hands); a day's labour bounds the slack's work; weeding asks 24 h a hectare and keeps pace with the season.
    A whole year of seed 42: grown people work ~6.5 h a day, children of 6-11 ~3 h - inside time-allocation ranges. 115 tests.
  - **The woods drawn (D2-080, slice 1 of D2-079)**: a canopy of crowns on the ground from the simulation's forest share, at every
    view (lit domes and conifers with their shadows, the seasons' colours, the far woods as their mean), and within 440 m of the
    camera a tree at every standing crown (light meshes, two distances); the old sparse trees gone. Seen in editor shots
    (`tc2/out/shots/forest4`); the Web build's frame rate with them is not measured yet (the build was stopped for lack of memory).
  - **Direction (D2-079)**: Arjun found the Web build's world bland and empty ("trees and forest nothing is there"): the planet's
    life is simulated but barely drawn (a full forest ~4-5 trees a hectare, none beyond 3.2 km, a flat ground colour). Next: the
    living world made visible - forests at their true density, woodland seen from afar, ground cover by biome - before F8.
  - **F7 CLOSED (D2-078)** with the History Book (D2-077): yearly annals (state); ages found after the fact by change points and
    named for what marked them; chapters, places and lives whose every line is a record (an event with its walkable causes, or
    the annals' numbers); the Book in the game (B) with each line's causes and Go there; `headless.mjs chronicle` (500-year books
    of seeds 7 and 42: 9 and 10 ages, 99 and 420 lines, none without its record). 169 tests.
  - **F6 CLOSED (D2-076)** with slice 6, tribute and lordship (D2-075): a village beaten by a stronger band with nowhere to go
    bows and pays a tenth of its harvest until its lord is gone or no longer the stronger. In 1,000-year runs fortified places
    and lordships arise and fall, their history traced from famine through taking, blows, refused amends, bands and raids
    (seed 7: 21 palisades, 47 tributes). 166 tests. Next: F7, the History Book.
  - **F6 slice 5 - the palisade (D2-074)**: weighed from three years of losses against its labour and timber, built by the
    village's people, rotting unmended and falling; it sees takers and doubles the defenders' ground; drawn as a ring of posts
    with gates on the roads (`shots --wall 1`, `tc2/out/shots/wall1`). 400 years: seed 42 35 begun, raids 18 -> 6; seed 7 19
    begun, 1 fallen. 163 tests.
  - **F6 slice 4 - raids (D2-073)**: bands raised by a heavy wrong or hunger, each member deciding, walking together at night;
    unseen they carry loads off, seen the village rallies round by round and one side breaks; bands dare only what they can win.
    400 years: seed 42 18 raids (villages of ~400 are safe), seed 7 161 (44 % beaten off). 159 tests.
  - **F6 slice 3 - answers, news and taking back (D2-072)**: claims for blood answered with amends (the wrong made good) or a
    refusal (a new wrong); news of deeds on visits, trades and claims (a village known to beat takers is avoided); the fed and
    bold take back from the village theirs holds most against. Grievance kept as ageing sums: conflict 87 -> 26 us, trade 127 ->
    4 us a village-day, the same history. 156 tests.
  - **F6 slice 2 - the watch, blows and injury (D2-071)**: a seen taker may be caught and struck (or turn on those who come out);
    wounds heal or kill, each a deed; a village that remembers its losses keeps a watch at its store. 400 years: seed 42 2,428
    blows and 214 dead of wounds, takings 11,107 -> 4,126 with the watch; seed 7 1,635 and 135. 151 tests.
  - **Sickness at scale (D2-070)**: six remembered immunities, strains of one sickness sharing part of their immunity, rare
    shifts, spillover bounded: seed 42 now holds 12,000-16,000 people from year 270 to 400 (plagues of ~14 % made good in twenty
    years), seed 7 4,600-5,400; famine regulates them as before. 148 tests.
  - **F6 slice 1 - deeds and taking by need (D2-069)**: deeds known per village (own, seen, found) with grievance and gratitude
    read from them; the hungry take grain from a neighbour's full store in the night, seen or not by the homes round it; no trade
    and no relief across a grudge; restraint by kin and help received. 145 tests. **A 400-year run of seed 42 found sickness
    collapsing a dense world** (10,031 -> 1,963 people in 50 years: strains that shifted reinfect everyone; two immunities a
    person) - D2-070 next.
  - **F5 CLOSED (D2-068)** with slice 2, trade by want and relief (D2-067): villages value goods by their own want and their
    traders carry what one spares to a neighbour that lacks it, both gaining; grain goes to a neighbour in famine while its store
    is short (told once a famine); traders carry sickness home, traceable along the trade. Seed 7 free: 521 trades, copper from
    the ore villages held by all eight villages without ore by year 200, four famines met by relief. 138 tests. Next: F6.
  - **F5 slice 1 - sickness (D2-066)**: pathogens come over from animals and mutate; each person carries their own infection;
    villages infect at beta x I / N; visitors carry it home, COARSE couples at the lived visit rate; every epidemic traceable to
    the animals. Seed 42 free: four sicknesses and fifteen village epidemics in 200 years. 132 tests. Next: trade by want.
  - **F4 CLOSED (D2-065)** with slice 3, copper (D2-064): ore near volcanoes, found only by potters by the ore, smiths filling
    the village's store from charcoal, metal tools quickening hands and outlasting goods. Seed 7 free: a loom lost in year 8,
    copper found twice (years 147, 151), known everywhere by year 200 but held only where the ore is. 128 tests.
  - **F4 slice 2 - materials, pottery, the ard (D2-063)**: goods draw fibre and hides from the village's store (from the
    harvest's flax, the hunt and the slaughter) and are improvised without; pottery near clay keeps grain and saves wood; the ard
    with an ox speeds the fields. 125 tests. Next: metals and techniques that need techniques, then close F4.
  - **F4 slice 1 - techniques carried by people (D2-062)**: nets, the loom and the rotary quern held as proficiencies, copied
    daily from the village's best with Henrich's loss and scatter, found by practice, carried by whoever moves, lost when the
    masters' line thins: a bride brings the loom and her new village weaves; one master of the quern in an isolated village of
    14 is lost in 150 years, kept and bettered in a village of ~150. 121 tests. Next: materials from geology (slice 2).
  - **F3 CLOSED (D2-060)**: villages of persistent people live untouched for centuries, every visible action is the
    simulation, followed days and lives are believable; Web 6/6 bit-identical (`tc2/out/webtest/s2webd`: village 89, close 84,
    low 110 fps; a day away 38 s).
  - **Homes stand on their own (D2-059)**: huts and houses are buildings that outlive their households - left empty when a
    household dies out (kin inherit) or moves away, taken by newlyweds and arriving families before they raise their own,
    decaying to roofless walls and ruins that fade; households that died out no longer linger. 118 tests.
  - **Determinism repaired (D2-057)**: the editor's Mono had parted from dotnet at world generation (float sums in the climate
    and in people's positions: 63 float operations); rewritten in double, `tc2/tools/floatcheck` fails the tests on any float
    arithmetic; Mono equals dotnet again (`hashtest --parts` compares part by part). The Web build (IL2CPP) to recheck.
  - Seal shots: seal views take the seal shore's own hour and frame a shore where the view shows seals. **Found**: seed 42
    starts with ~6 seals, 411 cod and 91 tuna (the spin-up seeds the sea's predators ~10x below their level; they take a
    decade to grow into it) and its seals level at ~11-20 in all the world (food-limited, not territory: a colony cap of 4
    instead of 0.5 gave 17-23) - an ecology calibration to revisit (the look wants seal colonies).

- **F1 nearly done (verdict from the 10,000-year runs below), F2 nearly done (ecology, evolution, the sea's life), look-dev
  v1-v7 (Arjun: "a really beautiful miniature world, high detail, high performance"). Session 1 (5 Oct 2026), continuing.**
  - Docs: `docs/tc2/` 00-17, LEGACY_AUDIT, DECISIONS D2-001..041.
  - Core (pure C# 9, headless-tested, 73 tests): F0 plus hydrology (D2-022), catch-up plan (D2-023), climate (D2-024),
    tectonics (D2-025), volcanoes + carbon cycle (D2-028), quakes (D2-030), erosion (D2-031), storms (D2-032), coasts and
    a faster thermostat (D2-034), ice-aware weathering (D2-036); **ecology** (D2-035: eight land kinds as abundance
    fields, five foods, Holling II hunting with refuges, territories, seasons, dispersal), crag fare and spin-up refuges
    (D2-038), **evolution** (D2-037/038/041: a coat per species and cell that follows the climate; a neutral lineage
    marker per isolated population; new kinds by a keyed chance once lineages have parted, at most four per first kind;
    extinct kinds leave the fields; both Epochal history events), **the sea's life** (D2-039: plankton from light,
    nutrients, warmth and ice; herring, sardines, cod, tuna, seals; hunters wander toward their prey).
  - Look (Game code): v1 (D2-026) terrain/farmland/water/sky/kit; v2-v4 (D2-027) clouds, tilt-shift, lamplight, ground
    cover, roads, seasons, walking; v5 (D2-029) volcanoes, puffs, gentle shores, moonlight, quake effects; v6 (D2-033)
    chimney smoke, rain and snow, the sea's glint, the moon's path; wildlife (D2-035); **v7 (D2-040)**: the sea floor
    carries the water's colour by depth (clear shallows over sand, turquoise shelves, deep blue), a thin sea surface,
    shoals swirling under it, leaping fish and splashes, seals on the shore (`SeaLifeView`, kit `animal_seal`,
    `fish_shoal`, `Underwater.shader`).
    Batch-editor screenshots: `node tc2/scripts/unity.mjs shots --tag T [--views coast,coastfar,seals,wild,...] [--days D]`.
  - Web (last: `tc2/out/webtest/sea2`): 6/6, bit-identical, WebGPU; start 6.1 s; orbit/region 120, village 88, close 98,
    low 103 fps; a day away catches up 96 years in 27 s.
  - F1 evidence: seed 7 (D2-038 build) had a full glacial cycle in 10,000 years (ice 12 -> 25 % of land, sea -21 m, held
    ~7,000 years, melted back to 9 % and sea -2 m by year 9,000 as forests spread 0.20 -> 0.29, a new glaciation by
    10,000); seed 42 glaciated by year 3,000 (ice ~33 %, sea -60 to -78 m) and stayed glacial through 10,000 (CO2 rose
    to 525 ppm, then fell as the exposed shelves weathered) - stable, no runaway. Drift, rising and wearing ranges,
    coastline change in both. The rerun on the final build (D2-041) was stopped at the session's end at year 4,000 of
    seed 42 (`tc2/out/f1/run_42h.txt`: the same glaciation, ice 35 % by year 3,000, sea -79 m, no new kind yet - as
    D2-041 intends); seed 7 was not rerun. Rerun both to close F1/F2 (commands in SESSION_HANDOFF).
  - F2 evidence: predator-prey quasi-cycles (hares 44-92k on a ~4-year period, foxes 2-3 years behind, r 0.41; wolves
    2 years behind deer, r 0.81); recovery after a crash (test); adaptation over centuries (mean cold tolerances shift
    5-9 C in 200 years as coats follow the climate); speciation in a long isolated run (seed 42: sand foxes in glacial
    refuges by year 9,000-10,000 - one-at-a-time rule D2-041 now in the reruns).
  - **F3 started (investigation only, nothing built yet)**: a 300-year coarse run of seed 42 (`run --coarse --places`)
    grew 145 -> 1,428 people by year 200, then fell to 388 with 4 of 6 villages abandoned. Souhope (63 people, full
    rations) died at year 47: its fields burned at year 46 -> the harvest failed (0 food) -> the granary (under a year
    of food) emptied -> 40+ hunger deaths within 2-12 days -> abandoned. Causes in the model: no reserve beyond the next
    harvest, starving health falls 10 %/day (dead in 5-17 days; real starvation takes weeks), foraging feeds about one
    person per forager and takes nothing from the land, no hunting or fishing, no relief or migration (F5). The next
    slice (proposed D2-042, see SESSION_HANDOFF "Next steps") is hunting and fishing from the wildlife fields, reserves
    from remembered hunger, and slower starvation.
  - Next: rerun the 10,000-year runs and close F1/F2; F3 D2-042 (above); herds as agents near people; look follow-ups
    (seals in daylight shots, coast shapes off the cell grid, boats, gardens, orbit detail, WebGL2 check).
  - Headless tools: `planet` (`--tect`, `--volc`, `--quake`, `--storms`, `--erosion`, `--eco`, `--ecodiag` deaths by
    cause, `--ecocsv FILE` series, `--steep`, `--map DIR`), `climate`, `run --profile`.

## Measurements (session 7)

| What | Host | Result |
|---|---|---|
| C10.1 vs C5.4, ten seeds 300 years DEEP (`tc2/out/civ/s*_c54a`, `_c101a`; `scratchpad/cmp.py`) | dotnet | people over years 200-299 x0.992 (t -1.2), at year 300 x0.934 (t -1.7); seed 2's drop two new sicknesses from animals, seed 7's Voton's famines |
| D2-108 vs C5.4, the same ten seeds (`s*_c108a`) | dotnet | people over years 200-299 x1.002, at year 300 x0.982 (n.s.); bands x0.92; lords answered for tributaries 3 times |
| Full headless tests | dotnet | 244 pass (382 s) |
| Web frame by layer, C12.2 build (`webperf`, settled) | Chrome/WebGPU, RX 5600M, 1280x720 | close 60.4 fps (without shadows 119, trees 97, buildings 88); forest 66 (without shadows 120, trees 103) |
| Web frame after D2-109 (`webperf` sets, `tc2/out/webperf/s7perf3`) | Chrome/WebGPU | close 90-102 fps, cpu 9.2-10.2 ms, 37 draws (cull off 98.6; 3 cascades 111; 2 cascades 117; no shadows 121, cpu 5.7); forest 100-103, cpu 9.5, 34 draws (cull off 79) |
| Web test after D2-109 (`tc2/out/webtest/s7webb`) | Chrome/WebGPU | 6/6, bit-identical (gen 9294339ab8f5d2da); start 7.7 s; orbit 120, region 120, village 101, close 83, low 114, forest 88, forest low 81, close running 82, after a century 88 fps; a day away (96 years) 55 s |

## Measurements (session 5)

| What | Host | Result |
|---|---|---|
| D2-083 closed, the final tree: 400 years, no profiler (the machine shared, 50-80 % CPU from other sessions) | dotnet | DEEP 7 3.20 world years/s, COARSE 7 2.42 (same load), DEEP 42 2.23; COARSE hashes 42 55cfd96b471db300, 7 8c3e14c324165015 (unchanged) |
| Full headless tests | dotnet | 177 pass (324 s; the DEEP-vs-COARSE herd check now a factor-2.5 bound) |
| D2-085 scale study, 400 years DEEP, seeds 42/7/1/2 at R = 10/7/6/5/4 km (`tc2/out/scale/table.txt`) | dotnet | people at year 400: 10 km 13,546/4,876/963/19,043; 7 km 6,439/2,471/816/8,197; **6 km 5,471/1,995/1,116/7,110**; 5 km 2,564/1,238/450/5,121; 4 km 3,774/1,010/2,164/3,062; ms a world year 10 km 179-413, 6 km 188-319 (seed 42: 413 -> 264); the planet ~150-180 ms a world year at any size |
| Planet only, seed 42, 600 years at 10 and 5 km (`tc2/out/scale/ground42_r*.txt`) | dotnet | the same tectonic history step for step (collisions, subductions, ridges, peaks, eruptions); 99 s against 82 s; at 5 km seals extinct at year 0, bears at 180 |
| Editor shot rounds `scale1` (6 worlds x 7 views) and `scale2` (9 x 6) | editor | ~6-8 min each with the machine shared; contact sheets `sheet_*.png` |
| 6 km acceptance worlds, 1,000 years DEEP, saves at 0/25/100/300/1,000 (`tc2/out/scale/r6000_s{42,7}_ages/`) | dotnet | seed 42: 4,950-6,180 people in 18-21 villages from year 400 to 1,000 (324 epidemics, 87 raids, 20 tributes, 30 palisades, 8,295 trades; 469 ms a world year); seed 7: 1,957 in 11 (282 ms); **new kinds appeared** (seed 42: boar twice; seed 7: hare, wolf) where 10 km's planet-only 10,000-year runs made none - and the 10 km seed 42 run with people (1,000 years, `r10000_s42_1000.txt`) also split its boar into three kinds: people fragmenting the woods make kinds (F2's done-when met with people), not the planet's size |
| Tests and determinism at 6 km (D2-085) | dotnet, editor Mono | 177 pass (351 s); probe identical on both: gen 392cf8631e08dc36, d1 67ad8d195d58819d, d2 4260ca6bd3b0f492, d3 46da96b46a778f32 |

## Measurements (session 3)

| What | Host | Result |
|---|---|---|
| Editor shot round (batch editor Play Mode, 8-12 views) | editor | ~2-4 min; the editor takes ~2 GB (on this shared machine available memory fell to ~0.45 GB once: start it only with >= 3 GB available) |
| Web build, IL2CPP `--jobs 3` + `BEE_BUILD_THREADS=3` (D2-081 build) | editor | 8 min (part of the C++ cached from an aborted run), 11.2 MB; CPU ~35-48 % while compiling, but the final wasm-opt took every core (89-100 %) - now capped with `BINARYEN_CORES`/`EMCC_CORES`; available memory ~0.5-0.7 GB at the link |
| Web (WebGPU) after D2-081 (`tc2/out/webtest/s3weba`) | Chrome, RX 5600M, 1280x720 | 6/6, bit-identical (gen 9b6482ca9f9f606f); start 7.3 s; orbit 114, region 119, **village 96.5** (81 before), **close 39.5** (66), **low 49** (74), forest 72, forestlow 50, close running 39, after a century 49 fps; a day away 49 s |
| Web close/low by layer (`webperf`, D2-081 build; noisy) | Chrome | close: all ~44, without cover 66, without shadows 100, without terrain (+trees +cover) 123; low: all ~38, without cover 68, without shadows 75, without terrain 94 - gathering the cover's instances every frame was the cost (now cached: D2-082) |
| Web catch-up of a day away (`webcatchup`, D2-081 build) | Chrome | camera at 1,400 m: 57.3 / 56.0 s; at 22 km: 46.6 / 49.3 s (with ~7 s of page load); ~17-19 fps during |
| Web (WebGPU) after D2-082 (`tc2/out/webtest/s3webb`) | Chrome | 6/6, bit-identical (probe unchanged); settled (`webperf` after 30 s): close 65, low 69, forest 68 fps; quick readings orbit 108, region 104, village 75, close 56, low 62; a day away 48.2 s (camera held at 22 km) / 48.9 s (held at 1.4 km) |
| Saturated world, 40 x 300 founders, 5 years (`bench --name poc`) | dotnet | COARSE 0.85 world years/s (1.76 us a person-day; 1.26 at D2-061 before F4-F7); **DEEP (K = 12) 1.54** (0.99 us) |
| Same world profiled, 5 years (`run --profile`) | dotnet | COARSE: 10,235 village days at 448 us; DEEP: 1,863 village steps at 1,055 us (7.9 a village-year; the calendar forces 6) - villages 2.3x cheaper; the planet ~157 ms a world year (vegetation + ecology 67, weather 52) is now a quarter of DEEP |

## Measurements (session 2)

| What | Host | Result |
|---|---|---|
| 10,000 years planet + ecology (no people), seeds 42 / 7 | dotnet (scratch copy) | 1,298-1,537 s / 1,422-1,512 s (6.5-7.7 years/s) |
| 300 coarse years, seed 42, people (before the herd index) | dotnet | 10,592 people in 29 places at year 300; 3,248 us per village-day (coarse-day 2,916: herd lookups) |
| COARSE village-day, seed 42 (after the herd index, 100-200 years) | dotnet | 124-162 us: survey 55 (grounds 13, herds 41), livestock 32-37, coarse-day 17-28, demography 6-19 |
| 200 coarse years, seed 42, with livestock and herding | dotnet | 51-63 s (3,500 people, 14-15 places) |
| Livestock, ACTIVITY vs COARSE, 3 years | dotnet | 248 / 250 animals, 287 / 287 births; herders per herder wanted 0.97 / 0.99 |
| Web (WebGPU, Chrome, RX 5600M, 1280x720; `tc2/out/webtest/s2webb`) | Chrome | 6/6, bit-identical; start 7.3 s; orbit 120, region 121, village 83, close 79, low 111, close running 86, a century on 77 fps; a day away (96 years) 39 s; build 11.1 MB |
| Web (WebGL2 fallback, Chromium; `s2webgl2b`) | Chromium | 6/6, bit-identical; start 6.4 s; orbit 59, region 60, close 59, low 60, running 60 fps (Chromium's cap; the same build in Chrome ~120); a day away 37 s; **first entry to a village view stalls 1-5 s** (driver shader/buffer creation) |
| WebGL2 village view by layer (`tc2/scripts/webperf.mjs`) | Chromium | 4 cascades: 37 fps, 60 without shadows (trees' shadows the cost); 2 cascades at half reach 49; **1 cascade 60** (D2-051); `Shader.WarmupAllShaders` costs 36 s at start (removed) |
| A full world in COARSE (bench 40 x 300 founders, ~13,300 people, 5 years) | dotnet / Chrome (WebGPU build) | 1.28 / 1.37 world years/s (1.22 us per person-day): the browser keeps pace with headless - a Living day away (96 years) in a full world ~70 s; Ancient (600) ~7 min, Deep time (4,800) ~1 h: those need a cheaper human layer |
| Village-day cost at ~360 people (COARSE, year 320) | dotnet | 206 us (coarse-day 69, survey 42 of which herds 31, food 37 of which needs 26, demography 35, stock 19) |
| Saturated world after F3 (40 x 300 founders, 13,316 people in 50 places, 5 COARSE years) | dotnet | 1.26 world years/s, 1.24 us a person-day (unchanged by D2-056 to D2-059); village-day 252 us (COARSE day 105, survey 44, demography 43, livestock 29); a Living day away ~75 s (D2-061) |
| Web (WebGPU) after D2-059, F3 closed (`tc2/out/webtest/s2webd`) | Chrome | 6/6, bit-identical (gen ae01d33f79af8525); start 6.1 s; orbit 119, region 120, village 89, close 84, low 110, close running 92, a century on 79 fps; a day away (96 years) 38 s; build 11.2 MB |
| Web (WebGPU) after D2-056 to D2-058 (`tc2/out/webtest/s2webc`) | Chrome | 6/6, bit-identical with headless and the editor's Mono (gen 2c8c6fad6b24a0b9); start 7.1 s; orbit 119, region 119, village 90, close 80, low 91, close running 82, a century on 76 fps; a day away (96 years) 38 s; build 11.2 MB; cooks' smoke at breakfast in the close view |
| Fire steps, seed 42 COARSE with people, 150 years (D2-055) | dotnet | 131 -> 2.6 us a step (379 -> 1 fire page held; heap 65 -> 41 MB; run 29.8 -> 23.0 s) |
| 200 coarse years with household work (D2-056) | dotnet | seed 42: 32.9 s, 3,492 people in 15 places; seed 1 (cool): 31.2 s, 1,206 in 6; hearths 2.8 us a village-day |
| Web (WebGPU) with the History Book (D2-077; `tc2/out/webtest/s2webg`) | Chrome | 6/6, bit-identical with headless and the editor's Mono (gen 9b6482ca9f9f606f); the book reads in the browser (`8_book.png`); timings not comparable - another session's headless Chromium (SwiftShader WebGL) held ~7 of 12 threads (build 27 min, fps ~25, a day away 108 s) |
| The History Book of 500 coarse years (D2-077; `chronicle`) | dotnet | seed 7: 9 ages, 99 lines with their causes, 0.28 s (the world 500 s, shared machine); seed 42: 10 ages, 420 lines with 31 places, 0.65 s (the world 761 s); no line without its record |
| Web (WebGPU) after F6 (D2-069 to D2-076; `tc2/out/webtest/s2webf`) | Chrome | 6/6, bit-identical with headless and the editor's Mono (gen 8dea367f3e835612); start 6.7 s; orbit 119, region 120, village 81, close 66, low 74, close running 84, after a century 43 fps; **a day away (96 years) 54 s** (42 at s2webe; headless shows no slowdown at year 100 - a browser-side cost to find); 481 people in 20 places; build 11.2 MB |
| Web (WebGPU) after D2-062 to D2-068, F5 closed (`tc2/out/webtest/s2webe`, `s2webe2`) | Chrome | 6/6 twice, bit-identical with headless and the editor's Mono (gen dd0c2fd7a147d1d2); start 7.4 / 6.7 s; orbit 119 / 121, region 119 / 118, village 91 / 91, close 75 / 74, low 87 / 92, close running 93 / 79, **after a century 48 / 68 fps** (s2webd 79; the reading right after a catch-up varies run to run - its second sample 70 / 71, s2webc's 12 - but both are lower: to measure layer by layer); a day away (96 years) 42 / 41 s, 503 people in 16 places; build 11.2 MB |
| 1,000 coarse years with F6 (D2-074, D2-075), seeds 7 / 42 | dotnet | seed 7 (`lords7_1000.txt`): ~9 min, 4,300-5,800 people in 17-18 places, 302 raids, 23 palisades begun, 47 tributes agreed and ended; seed 42 (`f6_42_1000.txt`): 10,000-14,500 in 36-37, 530 raids, 42 palisades (4 fallen); its red spots (two years' immunity) endemic: 5.1 million infections |
| 400 coarse years with palisades (D2-074), seeds 42 / 7 | dotnet | seed 42: 14,549 people in 35 places, 35 palisades begun (20 standing), 6 raids; seed 7: 5,463 in 18, 19 begun (9 standing, 1 fallen), 70 raids; conflict 37 us a village-day |
| 400 coarse years with raids (D2-073), seeds 42 / 7 | dotnet | seed 42: 15,088 people in 33 places, 18 raids (2 beaten off); seed 7: 5,095 in 18, 161 raids (71 beaten off), 16,426 FU carried off; conflict 29 us a village-day |
| 400 coarse years with answers and taking back (D2-072), seeds 42 / 7 | dotnet | seed 42: 221 s (271 before the ageing sums: conflict 87 -> 26, trade 127 -> 4 us a village-day), 14,776 people in 36 places, 1,493 claims (285 made good), 3,587 taken back; seed 7: 5,537 in 19, 2,027 claims (250), 5,607 |
| 400 coarse years with the watch and blows (D2-071), seeds 42 / 7 | dotnet | seed 42: 14,378 people in 36 places, 4,126 takings, 2,428 blows, 214 dead of wounds; seed 7: 4,779 in 18, 3,587, 1,635, 135; conflict 7.8 us a village-day |
| 1,000 coarse years after D2-070, seed 42 (`tc2/out/society/sick42_1000.txt`) | dotnet | 1,046 s; 16,000-18,500 people in 35-36 places from year 300; three great plagues (445, 757, 949) of a third each, made good in fifty years; 116 hunger deaths a year; ledger 268,881 kept |
| 400 coarse years after D2-070, seeds 42 / 7 | dotnet | seed 42: 245 s, 15,984 people in 35 places (12,000-16,000 from year 270); seed 7: 5,392 in 18; village-day: COARSE day 201, knowledge copying 94, demography 55, food 53, sickness 11 us |
| 400 coarse years with taking (D2-069), seed 42 | dotnet | 134 s; 10,031 people in 30 places at year 350, **1,963 at 400** (the black spots and its strains: D2-070); taking 1.1 us, trade 2.9 us a village-day |
| 200 coarse years with sickness and trade (D2-066, D2-067) | dotnet | seed 7: 40.9 s, 4,383 people in 18 places; seed 42: 41.0 s, 2,640 in 14; a village-day's sickness 4.5-5.3 us, trade 1.3-1.5 us |
| History ledger, seed 42 COARSE | dotnet | year 300: 95,615 -> 69,179 kept after two fixes (households' events kept for ever; a death's age counted as its importance); at year 1,000 before them 780,282 |

## Measurements (session 1)

| What | Host | Result |
|---|---|---|
| Event queue pop+push | dotnet | see `tc2 bench --name queue` |
| POC world, 6 villages, ~145 -> ~380 people, 30 years | dotnet | 2.2-2.3 s (13-14 world years/s); 3 seeds stable, no starvation |
| Agent event cost (steady state) | dotnet | ~0.8 us per activity event; ~9 events per person-day |
| Planet systems fixed cost | dotnet | ~40 ms per world year (weather 21, climate 13, vegetation 6) |
| Save (1 year, ~170 people) | dotnet | see `tc2 bench --name save` |
| Cross-runtime probe | dotnet vs Unity Mono | identical: `dmath 387e8102.. rng dab34ace.. gen 3d8c0c4d.. d1 fb641c50.. d2 a907e40e.. d3 0d12bcbc..` |
| Unity first import + configure | editor batch | 155 s; later batch starts ~22-28 s |
| ACTIVITY 100 years, 6 villages, 145 -> 567 people (seed 42) | dotnet | 12 world years/s; hunger episodes, childbirth deaths, an abandoned village |
| COARSE 100 years, same worlds (2 seeds) | dotnet | 34-36 world years/s; agent events 66K vs 15M per century; planet systems dominate |
| Cross-runtime probe (current code) | dotnet == Mono == IL2CPP wasm | `dmath 387e8102.. rng dab34ace.. gen 559e64de.. d1 2c8f4aad.. d2 d5fb790f.. d3 9765782f..` |
| Web build (release, Brotli) | Unity 6.6 IL2CPP | 10.1 MB (wasm 6.0 MB, data 3.8 MB); incremental rebuild 8-9 min |
| Web start to a running world | Chrome, WebGPU | 5.7 s |
| Web catch-up, a day away (Living = 96 years), ACTIVITY | Chrome | 14.6 s (6.6 world years/s; 17M events, ~0.9 us/event) for 145 -> 739 people |
| Web save | Chrome | 356 KB / 46 ms at year 0; 646 KB / 80 ms at year 96 (739 people, 5,640 history entries) |
| Web frame rate | Chrome, RX 5600M, 1280x720 | orbit 120 fps; village/close 49-116 fps |
| Planet only (climate + plates + weather), COARSE | dotnet | 25-28 world years/s (seed 42, 4,000 years) |
| Planet + people COARSE, smooth weather (before / after tabulated logistic + one-mix hash) | dotnet | 14.3 / 19.4 world years/s (weather 1.25 / 0.82 ms a day) |
| Volcanoes, seed 42, 4,000 years planet only | dotnet | 28-42 vents, 650 eruptions (574 VEI 1, 65 VEI 2, 7 VEI 3, 1 VEI 4), CO2 280 -> 336 ppm |
| Web frame rate, look-dev v4 | Chrome/WebGPU, RX 5600M, 1280x720 | orbit 111, region 120, village 118, close 91, low 123 fps |
| Web frame rate, volcanoes (v5 part 1) | Chrome/WebGPU, RX 5600M, 1280x720 | orbit 119, region 121, village 119, close 89, low 119 fps |
| Quakes, seed 42, 3,000 years planet only | dotnet | M5 4,741, M6 1,753, M7 93, M8 0; ~1 landslide a year; 20.5 world years/s coarse |
| Erosion, seed 42, 3,000 years | dotnet | 0.7-1.5 million m³ moved a decade, nearly all settling; valleys ~6 m (sim) below banks |
| Storms, seed 42, 300 years | dotnet | ~2.8 a year, 20 above 40 m/s, ~0.55 landfalls a year; storm step 58 us (7 % of a coarse year) |
| Web, quakes build | Chrome/WebGPU | 6/6, bit-identical; 89-124 fps by view |
| Web, erosion + storms build | Chrome/WebGPU (10,000-year runs on the CPU meanwhile) | 6/6, bit-identical; 89-121 fps by view |
| Slow climate alone | dotnet | ~3 ms per world year (0.75 ms per season step) |
| Look-dev shot round (batch editor Play Mode, 5 views) | editor | ~40-45 s |
| Ecology step, 8 land kinds (before / after precomputed intake, tables) | dotnet | 10.5 / 6 ms |
| Ecology step with the sea (13 kinds; before / after sparse mouths, caches, interleaved arrays) | dotnet | 16.8 / ~14 ms |
| COARSE century, seed 42, planet + eco (8 land kinds / + the sea) | dotnet | 9.1 s / 12.3 s |
| Worldgen seed 42 (80-step / 40-step spin-up, with the sea) | dotnet | 2.7 s / 1.5 s |
| 10,000 years planet + eco (seed 42, coarse, maps every 1,000) | dotnet | 1,367 s (7.3 years/s) |
| Web, sea life + water by depth | Chrome/WebGPU | 6/6; start 6.1 s; village 88 fps; a day away (96 years) 27 s |

## Blockers
- **Memory pressure stop (6 Oct 2026, 23:56; resolved 7 Oct 00:16 - Arjun: resume, run the retests and the six seeds yourself, no
  server)**: Claude Code had stopped the local game server, the COARSE 7/42 tally chain and the queued DEEP 7 retest. Standing rule
  from Arjun (7 Oct): ordinary low-priority runs within the ~50 % CPU/RAM rule need no further permission; start the game server only
  when inspecting the game, then shut it down. Headless runs gate at CPU < 40 % and >= 2 GB available (`calib2.sh` `MEMMIN`; a 400-year
  run's working set is ~0.1-0.3 GB); the editor and the Web build keep the 3 GB gate.
- **Note (7 Oct 00:24)**: the auto-mode classifier refused ("Interfere With Workloads") to stop this session's own leftover waiting
  chain (a `calib2.sh` of TAG i waiting in `quiet.ps1`, started 00:21 by a task already stopped). Left alone it reran the same
  deterministic DEEP 7 test into the same file when the machine was quiet and exited - harmless; recorded so a later session does not
  mistake a second dotnet run for someone else's.
- **F8 calibration runs (6 Oct 2026, session 3; resolved 23:06 - Arjun allowed the batch)**: Claude Code's auto-mode classifier refused ("Interfere With Workloads") to stop two
  of the session's own leftover calibration workers (`scratchpad/calib2.sh`, started 21:15) and then to start the final DEEP batch
  (seeds 42, 7, 1-4 with the newest build) while they ran. The leftover workers have since finished (22:04; their COARSE 1-4 runs are
  the baselines, their DEEP runs round 3a in D2-083). To proceed: allow the batch (or a Bash permission rule for it), or run it by hand
  (about 20 minutes single-threaded): `BIN=tc2bin17 TAG=f bash scratchpad/calib2.sh 42:deep 7:deep 1:deep 3:deep 2:deep 4:deep`
  (`tc2bin17` = the commit after bc52088). Workaround meanwhile: small single-world checks (seed 33, seed 1 for 60-100 years).
- Wavedash cloud saves and itch upload need Arjun later; not needed for F0.

## Log

### 2026-10-05 — session 1
- Environment: Ryzen 5 4600H 6C/12T, 11.4 GB RAM, RX 5600M; Unity 6000.6.4f1 (Web + Windows Mono + docs),
  bundled .NET SDK 8.0.318, Node 22, Python 3.13, Playwright browsers.
- Legacy audit and research; architecture decisions D2-001..014; docs committed (a2283fa).
- Core foundation + POC (18cd0cc): first run found people sleeping 24 h because a fractional wake time
  truncated one second short of morning (fixed with integer seconds); per-cell transcendentals in the climate
  tick made a day cost 31 ms (precomputed: 2 ms).
- Activity layer (next commit): one event per activity (walk+stay analytic), longer blocks: 210K -> 74K events a
  year. Calibration: a 48-day year needs 48 FU a person (yield 130 FU/ha); marriage across neighbouring villages
  (3 -> 51 marriages in 30 years); fires that burn out in days (median 37 h); rationing to the next harvest,
  foraging, travellers who give up waiting (a traveller had waited on a burning road until he starved), starvation
  by each body (17 villagers had died in the same hour).
- Unity integration (c478f96): project configured from code; Core compiles in Unity; EditMode 23/23; Mono probe
  equals dotnet.
- Land and people (4d7705a): settlements claim arable cells within reach each spring, crops grow on their fields'
  weather, fire burns a field's share; pre-modern mortality, childbirth deaths, marriage peaking in the early twenties,
  fertility by age: 100 years 145 -> 567 people with hunger episodes and an abandoned village.
- COARSE resolution (D2-021) with tests (enter/leave audit, determinism, save/load in COARSE); ~3x faster centuries.
- Web: Playwright's Chromium could not create a WebGPU device and Unity crashed instead of falling back: a template
  probe hides WebGPU when no device can be made (D2-019); tests use system Chrome. Instanced objects were invisible:
  materials became assets (D2-020). A rebuild once "succeeded" with compile errors (CS0104 `Resolution` ambiguous with
  UnityEngine.Resolution): the wrapper now fails on `error CS` in the log. Web test 5/6 (a 404, the favicon).
- F1 hydrology (a5c5c50), climate (30464c4), tectonics (19d7079): see D2-022..025; bugs found by tests and long
  runs listed there (lake depth derived from double vs float, sea level vs coastline masks, aliasing biases in plates).
- Look-dev v1 (2fbf6d4, D2-026): the terrain, farmland, village ground, water, sky, grade and the Blender kit; bugs:
  reversed winding test, shots before instanced draws, a new world inheriting a catch-up, Mathf.SmoothStep semantics,
  stale material values.
- Look-dev v2-v4 (b8f1593, c870730, 6848601; D2-027): clouds from the weather, smooth weather spells, tilt-shift,
  lamplight, ground cover, roads, seasons, walking, slim HUD; Web 6/6 each round.
- Volcanoes and the carbon cycle (D2-028): the first design opened 200+ vents and piled magma under plumes beyond any
  vent's reach (a VEI 6 from stored magma); each eruption rebuilt the drainage (half the speed, and a load would have
  derived drainage from relief the live world had not routed: cones now wait for the tectonic step). Profiling showed
  the smooth weather costing 86 % of a coarse planet year (exp per cell and a 4-mix hash per cell): tabulated logistic
  and a day key, 14.3 -> 19.4 world years/s.
- Volcanoes drawn and quakes (D2-029, D2-030): the first cone draft drew sea points under a clamp - cliffs at every
  coast near a vent; subduction coasts drew as sawtooth cliffs with a grey band of slope rock (coarse cells against a
  trench) - fixed by a drawn elevation that ramps shores; round canopies were lit at night by the sun under the
  horizon; quake cascades at pass 0.25 were near-critical and old faults kept stress for 2,000 years (an M 8 every 26
  years); dust was added to the puff list before the frame cleared it.
- Erosion and storms (D2-031, D2-032): the mass-balance test caught a delta rising above the sea mid-pass and
  swallowing later loads (land/sea now judged by the tree's built mask); first storms never passed 30 m/s (Earth's
  potential-intensity curve on a planet whose tropics are ~26 C); the storm step looped every cell per storm (BFS now).
- F1 long runs and F2 wildlife (D2-034, D2-035): the first 10,000-year run speckled its coasts and warmed ice-free
  (17.9 C); with coast smoothing it fell into an ice age it never left (9.6 C, sea -98 m) - the thermostat was too weak;
  the vegetation step had grown plants at a third of their rate in every coarse run. The wildlife's numbers found five
  model bugs (see D2-035).
- Ecology and evolution (D2-036..041): ice-aware weathering (rock under ice does not weather); death-cause counters
  (`--ecodiag`) showed the ibex starving on shared mountain grass and paying a harsh coat cost (crag fare, load 0.002);
  the spin-up boom-bust (hares 88k -> 8k, foxes 3,975 -> 4) needed refuges; coats alone split hares within 200 years and
  tectonic steps are every 10 years, not 50 (lineage drift); fragments at patchy range edges drifted as noise (only
  sizeable populations drift); seven fox refuges crossed the line together after 9,000 years (a keyed split chance, a
  per-kind cap, extinct kinds compacted). The `--ecodiag` arrays were sized for 16 species and crashed a 10,000-year
  run at the 17th.
- The sea's life (D2-039): tuna and seals starved on per-cell satiation tuned for wolves (hunting range, cold-blooded
  appetites); seals spread along polar coasts with no fish (prey-aware dispersal, narrower band); the sea geometry cache
  missed the sea level (a load would have differed).
- Look v7 (D2-040): shoals were invisible under a 60-90 % opaque sea; the depth texture is off (D2-026), so the sea
  floor now carries the water's colour by its own depth and the surface thinned to 30 %; shoals sat outside the 65 m
  of water in a close view (one per ~500 fish now); far shoals read as confetti (falloff).
