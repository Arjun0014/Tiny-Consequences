# TC2-25 — Civilisation roadmap (D2-089)

Replaces the order of `17`'s later phases (F9-F11 move behind the C-phases where they depend on them). F0-F7 stay closed and are
built on; W0 is done; **W (legibility, D2-084) is a standing requirement that every C-slice satisfies** (its visible form ships with
it) and its remaining look slices interleave; F8's remaining speed work is phase **P** (performance tiers, `20` §8). Every phase is
vertical slices: research -> design -> decision -> smallest real slice -> compile -> tests at both resolutions -> real runs -> shots
-> benchmark -> fix -> commit -> docs.

## 1. Phases and dependencies

```
C1 structures & workplaces ──┬─> C2 materials from the land ──┬─> C3 professions & production chains ─> C5 mining & metallurgy
                             │                                ├─> C4 transport & roads ───────────────┬─> C9 maritime
                             │                                │                                       │
                             ├─> C6 force: arms, training ────┴─> C7 fortification ─────┬─> C8 war & logistics, sieges
                             │                                                          │
                             ├─> C10 governance (leaders, lords, succession) ───────────┘
                             ├─> C11 culture & identity
                             └─> C12 inspectors & biographies (a part of every slice; its own pages in C12)
C1-C11 ─> C13 historical city evolution (morphology over centuries, rebuilding, the 1,000-year acceptance)
P (performance tiers A/B/C) and W (legibility) run alongside; E (ecology richness, human impact) with C2 and after.
**Open W task, not solved: W-FOREST** (`18` §8, Arjun 8 Oct 2026) - forests and biomes still read as the same trees recoloured; a dedicated
ecology/art pass (biome-specific composition and silhouettes, species/height/age variation, interiors/edges/regrowth/old growth, wet/dry/
cold/warm/riparian vegetation, ground cover and palettes) follows the current civilisation slices unless a C-slice touches it.
```

| Phase | Content | Done when |
|---|---|---|
| **C1** structures & workplaces | the structure kind table and state machine; works by need with causes; placement by zone; well/granary as structures (capacity real); smithy; training ground; building inspector; kit models | a 100-300 person settlement visibly differentiates (several kinds standing, worked, inspectable) in a real run, at both resolutions |
| **C2** materials from the land | timber from real forest (felling, clearing, regrowth), stone from outcrops (quarries, carrying), clay; stores with capacity and spoilage; deposits known by seeing | woods recede round towns and regrow after them; quarries open where rock shows; a village without stone builds otherwise |
| **C3** professions & chains | roles read from hours; apprenticeship; kiln (pots, charcoal), mill (water/animal), workshops (carpenter, weaver, tanner), barns, pens; exchange within a settlement | specialists exist in larger places only; their output feeds others |
| **C4** transport & roads | loads, pack animals, sleds, carts (wheels), route use -> track/path/road, road works and upkeep, fords, ferries, bridges; roads drawn by use | the road network is the history of travel; carts move stone where roads are |
| **C5** mining & metallurgy | copper and iron deposits, mines and bog ore, depletion, mining camps that become settlements, charcoal, bloomeries, iron | the "resource town" proof (§4) |
| **C6** force | arms as goods from the smithy, skill and veterans, training grounds, guards, armouries; fights use them | trained, armed villages win more fights at a measured labour cost |
| **C7** fortification | ditch, bank, palisade by stretches with gates, reinforced timber, stone curtain, towers, gatehouse, keep; rings that enclose growth | fortifications differ by region and history; walls ring what is guarded |
| **C8** war & logistics | campaigns from several villages with supplies; battles, assaults, blockades, sieges, burning, capture, retreat, war famine, refugees | wars arise from state in long runs, cost real lives and stores, and change the map |
| **C9** maritime | dugouts, fishing boats, docks, boatbuilding, cargo boats, coastal trade, ferries, ports, ships in storms | the "coastal civilisation" proof (§5) |
| **C10** governance | prestige and leaders, councils, succession, lords and obligations (tribute, men, labour), administrative reach by travel time, rebellion, fragmentation | kingdoms form only with their mechanisms and fall when they fail |
| **C11** culture & identity | household traits (building tradition, dialect, customs), drift and spread; regional styles in the view | regions look and act differently with no factions |
| **C12** inspectors & biographies | person page (`07` §9), building, settlement and polity pages | any person can be followed for a whole life; any structure tells its history |
| **C13** historical city evolution | morphology over centuries (green, workshops on roads, denser centre, market, suburbs, second wall), rebuilding, ruins reused | the 1,000-year same-seed acceptance (§6) |

## 2. The first slice: C1 "the working town" (in progress)

The smallest proof of the architecture: a settlement of 100-300 people becomes economically differentiated through real work.
Sub-slices, each committed with tests, runs, shots and docs:
1. **C1.1 Structures** (D2-090, done 7 Oct): the kind table; the well and granary become structures (state, condition, builders, cause,
   history, capacity: grain beyond it rots faster; barns for big harvests); works by need with causes; placement by zone; the building
   inspector; drawn from the table.
2. **C1.2 Timber from the woods** (D2-091, done 7 Oct): felling removes forest share at the woodlot (which moves as the woods recede:
   walking time per load); fields keep the woods off what they farm; regrowth where people stop.
3. **C1.3 Stone** (D2-092, done 7 Oct): outcrops known by people who saw them; the quarry as a work site; stone cut and carried (trip
   time); the stone store; footings (homes last longer). (The well's lining and the smithy's hearth move to C1.4 and later.)
4. **C1.4 The smithy** (D2-093, done 7 Oct): a workplace raised when a competent smith and a want of metal meet; smithing there
   (twice as fast, less fuel), arms forged only there by villages that fear their neighbours (from ore, or reforged tools); arms in fights;
   rings enclose works.
5. **C1.5 Training ground** (D2-094, done 7 Oct): practice hours, fighting skill, veterans; strength uses skill.
6. **C1.6 Market** (D2-095, done 7 Oct): a market where traders come often (10 a year), drawing traders from 9 km and dealing better;
   palisades on dry ground only. (Gates as structures move to C7.)
7. **C1.7 Pressure** (D2-096, done 7 Oct): tier-A worlds (3 villages of 40 founders, 200 years) read through `run --civ`: peace where
   land is plenty (daughter villages), famine -> takings -> watch -> training ground -> palisade where it is short, an armed feud where
   copper smiths forge arms; deposits are not yet worth fighting over (no claims, no depletion: C5, C10).
**C1 closed 7 Oct 2026 (D2-096)**; acceptance sheet `tc2/out/shots/civA_sheet.png`.

**C4.1 roads by use done (D2-097). C7.1 rings that grow done (D2-098). C12.1 the person page done (D2-099). C5.1 copper ore bodies and
mines done (D2-100). C12.2 the settlement page done (D2-106). C10.1 the lord's call done (D2-107); the lord answers for its tributaries, once (D2-108). C10.2 standing and the village's first household done (D2-127, `10` §12c: followings of kin, debtors and clients; headmen; gifts in hard times; tribute to the lord's household). C10.3 a contested succession splits a village done (D2-128, `10` §12d: at a vacancy an unbound rival's following leads them away to found a village, or looks for land; land decides how many). C10.4 bread and dependence done (D2-157, `10` §12f: a village that cannot give back a month of the famine grain a stronger neighbour within a day's walk gave it bows to that neighbour; the tribute gives it back, a lord feeds its own - lords at year 300 8 -> 21 over ten worlds, the lord's call answered, vital rates untouched). C10.5 a lordship's might (D2-159, `10` §12g: villages weigh each other by their people of fighting age - a day's wounds, sickness and hunger pass - and a lord by the men its call brings from its tributaries near the village weighed; measured first in the 900-year check: every bond ended because "the lord was no longer the stronger" by a day's count, one mild epidemic ended five lordships in a year, the largest lordship never above four villages). C3.5 household formation done (D2-129, `07` §7e: a couple marries when it can keep a household to the next harvest - the preventive check; hunger deaths x0.69). C9.1 boats from the woods (D2-110, closed). C9.2a the landing
and the crew (D2-111). C9.2b boats of planks (D2-112). C9.3 voyages, relief owed back (D2-113).**

### C9 maritime, by slices (D2-110): C9.1 the dugout - made from the woods, owned, rowed out to the open sea's shoals, lost in rising wind
(closed in session 8: 9a2ceb7, 23ac1c9); **C9.2a the landing and the crew** (D2-111: one landing a village, boats in a row on it, a boat goes out with its crew, nobody stands in the sea); **C9.2b plank boats and boatbuilding** (D2-112: a technique found by
woodworkers at a dugout; a boat shed at the landing; plank boats built there); **C9.3 voyages** (D2-113, closed: ways over the sea, a boat's load in trade, grain by sea to famines, relief owed back - social storage -, voyages lived by their crews); C9.4 the dock (by catches landed
and loads moved) and cargo boats, coastal trade at the sea's cost ratio; C9.5 ferries; then the coastal proof (`§5`).

### C10 governance, by slices: ~~C10.1 the lord's call~~ (D2-107; the lord's answer D2-108); ~~C10.2 standing and the village's first household~~
(D2-127); ~~C10.3 a contested succession splits a village~~ (D2-128); ~~C10.4 bread and dependence~~ (D2-157); ~~C10.5 a lordship's might~~ (D2-159);
~~C10.6 tribute carried to the lord's store~~ (D2-161: its labour and its ways - the ways of a lordship converge on its lord); next by evidence: the
lord's household men fed from the tribute (retainers, `23` §3), a lord's death and its tributaries (succession in a lordship), rebellion by those who
rise together.

### C5 mining and metallurgy, by slices (D2-100): ~~C5.1 copper ore bodies, the mine, depletion~~ (done); ~~C5.2a charcoal~~ (done, D2-101); ~~C5.2b
iron~~ (done, D2-102: bog ore, bloomery, iron's use a head); ~~C5.3a places by their ore~~ (done, D2-103); ~~C5.3b iron traded~~
(done, D2-104); ~~C5.4 whose ore~~ (done, D2-105); C5.3c work at a distance (smelting at the bed or the woods, stints with a camp); C5.3 distant work (smelting at a far mine, stints with a camp, households moving to the work: a mining hamlet);
C5.4 claims (a worked body known as a village's, digging it a deed, news of bodies by traders, deposits worth holding). Then C2.x clearing as
labour, C3 kilns and workshops.

### Next (D2-096): ~~C4.1 roads by use~~, ~~C7.1 rings that grow with the town~~, ~~C12.1 the person page~~ (done), C2.x clearing and kilns, C5 claims on
deposits that deplete.
Acceptance: a tier-A world (one to three settlements, 100-500 people) shot at its founding and decades later at the village and street
views shows the differentiation; every structure is clickable; headless runs at both resolutions and DEEP agree within tolerance.

### C3 professions and exchange, by slices (`26`): ~~C3.1 the weaver's shed and the village's wares~~ (done, D2-119: wares last longer than homespun,
the village's best weavers make them at a shed in larger places, households take them, they are traded - trade x3, home making hours x0.28); ~~C3.1b more sheds where a town's
looms cannot keep up~~ (done, D2-120); ~~C3.2 the potter's kiln, jars and hearths' pots of real clay~~ (done, D2-121: 84 kilns in 169 villages over
ten worlds, pottery most of the world's trade, markets x3.6); ~~C3.3a-c exchange between households~~ (done, D2-126, `26` §2c: each household's
grain in the village's store - the harvest by mouths, its own produce its own -, the moral economy (own grain, the village's common grain, kin's
gifts, loans recorded as debts and repaid from surplus), makers paid in kind as far as the taker's grain pays, the village's levy for jars and the
smith's work, sharing contracting to the household and its kin in hard times - famines x1.25, hunger deaths x0.9-0.95, fewer takings and raids,
people at par; open: household land, debts' consequences, households' own trade);
~~C1.4b the village smith~~ (done, D2-125, `26` §2b: a smithy's four places for its best smiths by craft and practice, practice quickening the hand,
another smithy only where the steady need outruns the places, smiths and boatwrights read from their work - before, every copper-competent person
worked at the smithy in turn, up to 355 hands a smithy-year, and 5,730 people were "smiths"); ~~C3.5 household formation~~ (done, D2-129: a
couple marries when it can keep a household to the next harvest); ~~C3.6a working the land harder~~ (done, D2-131, `26` §2d: more ploughing and
hoeing a hectare where the land within reach is short, at falling returns - yields +16-24 % where land binds, then the people grow into it);
~~the first sheaves~~ (done, D2-132, `26` §2e: the harvest's grain feeds the village as it is reaped; the hungry gap falls before the grain
ripens); ~~C3.6b household land~~ (done, D2-133: each household holds hectares of its village's fields); ~~C3.6c the harvest by land and
labour~~ (done, D2-134: the landless live by their hands; hunger a person-year x0.74 - the preventive check from the households' economy); ~~C3.6d
who leaves~~ (done, D2-135: each household by its own land; in full lands the land concentrates); ~~C3.6e the old hand on their holding~~ (done,
D2-136: retirement to a child's household - the hoards go, landlessness falls); ~~the household's own year~~ (done, `26` §2h-§2k: C3.7a the garden
D2-143, C3.7b the threshing floor D2-149, C3.7c the muck D2-153, C3.7d the herds on the stubble and the fallow and each household's own part of the
fields D2-155); service, a land market; C3.4 the mill; apprenticeship; C3.8 other workshops.

### C4 transport, by slices: ~~C4.1 roads by use~~ (D2-097); ~~C4.2 carts~~ (closed, D2-122, `24` §2: a household's handcart or ox cart where its backs
were too small - 83 % of trading loads were bound by what one carries; loads on paths and roads, wood and stone home by the cartload; trades x0.67,
famines x0.83-0.89; none begun in hunger); C4.3 fords and bridges **surveyed and deferred** (D2-124: every stream a worn way crosses is a fordable
brook, 0.08-0.23 m3/s at most); next: road works and upkeep, pack trains, carts for the lords' and armies' supplies (C8). (D2-123, found by the cart
shots: nobody works in the water - lived work spots ashore, woodlots above the shore; `wet` audit.)

### C13 historical city evolution, by slices (D2-116): ~~C13.1 streets - villages grow along their roads, houses face them~~ (done); ~~C13.2 back lanes~~ (done, D2-117);
~~C13.3 a town's ground: footprints (no house in another), divided plots and rows of narrow town houses in its core, the market place~~ (done, D2-118);
C13.4 suburbs outside the gates, a second wall; later: merged plots for wide town houses, shops below, lanes that bend, plots held. (C9.4 docks and cargo boats deferred
behind exchange, D2-115.)

## 3. Later proofs (from the direction): resource town, coastal civilisation, the 1,000-year world

## 4. Resource town (C5)
A stone or ore deposit away from the agricultural centre: discovery -> workers travel -> extraction -> transport -> camp -> mining
settlement -> road -> trade -> wealth -> strategic importance -> conflict or political interest. Nothing like `BecomeMiningTown()`.

## 5. Coastal civilisation (C9)
Fish shortage or opportunity -> boats -> dock -> fishing -> boatbuilding knowledge -> cargo capacity -> coastal trade -> port wealth ->
political and military importance. No `BecomePortTown()`.

## 6. Long-term acceptance (C13, no scripted stages)
A 1,000-year same-seed world: year 0 a wild planet; ~50 small villages; ~150 larger settlements, specialised production, roads; ~300
regional structures, defended towns, real trade; ~500 perhaps kingdoms, larger wars, prosperous towns, ports; ~800 fragmentation, new
powers, ruins, rebuilding; ~1,000 a political, ecological and architectural landscape visibly the result of its history. Seeds differ;
some stay fragmented, some collapse, some never form a state - desirable.

## 7. After the C-phases
F9 (the god's powers beyond those that exist), F10 (art and presentation: animation of labour, UI art pass), F11 (platform).
