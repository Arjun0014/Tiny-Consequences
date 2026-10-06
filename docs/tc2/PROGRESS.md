# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

**Phases: F0 done; F1 closed (D2-043); F2 closed (D2-046); F3 closed (D2-060: D2-042, -045, -047 to -050, -052, -056, -058, -059); F4 closed (D2-065: D2-062 to D2-064); F5 closed (D2-068: D2-066, D2-067); F6 closed (D2-076: D2-069, D2-071 to D2-075); F7 closed (D2-078: D2-077); Web checked on WebGPU (s2webf, after F6) and WebGL2 (D2-051); Mono determinism repaired (D2-057).**

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
None. (Wavedash cloud saves and itch upload need Arjun later; not needed for F0.)

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
