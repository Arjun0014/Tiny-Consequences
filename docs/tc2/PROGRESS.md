# TC2 Progress

## CURRENT STATE (keep accurate; newest first below)

**Phases: F0 done; F1 closed (D2-043); F2 closed (D2-046); F3 closed (D2-060: D2-042, -045, -047 to -050, -052, -056, -058, -059); F4 closed (D2-065: D2-062 to D2-064); F5 closed (D2-068: D2-066, D2-067); F6 closed (D2-076: D2-069, D2-071 to D2-075); F7 closed (D2-078: D2-077); Web checked on WebGPU (s2webf, after F6) and WebGL2 (D2-051); Mono determinism repaired (D2-057).**

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
