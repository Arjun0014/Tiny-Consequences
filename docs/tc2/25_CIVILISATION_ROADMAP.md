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
mines done (D2-100). C12.2 the settlement page done (D2-106). C10.1 the lord's call done (D2-107); the lord answers for its tributaries, once (D2-108). C9.1 boats from the woods (D2-110, closed). C9.2a the landing
and the crew (D2-111). C9.2b boats of planks (D2-112). C9.3 voyages, relief owed back (D2-113).**

### C9 maritime, by slices (D2-110): C9.1 the dugout - made from the woods, owned, rowed out to the open sea's shoals, lost in rising wind
(closed in session 8: 9a2ceb7, 23ac1c9); **C9.2a the landing and the crew** (D2-111: one landing a village, boats in a row on it, a boat goes out with its crew, nobody stands in the sea); **C9.2b plank boats and boatbuilding** (D2-112: a technique found by
woodworkers at a dugout; a boat shed at the landing; plank boats built there); **C9.3 voyages** (D2-113, closed: ways over the sea, a boat's load in trade, grain by sea to famines, relief owed back - social storage -, voyages lived by their crews); C9.4 the dock (by catches landed
and loads moved) and cargo boats, coastal trade at the sea's cost ratio; C9.5 ferries; then the coastal proof (`§5`).

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
ten worlds, pottery most of the world's trade, markets x3.6); C3.3 exchange between households - surveyed and deferred as a phase (`26` §3: the
village's store moves 31 % of the food households draw; household property needs a moral economy, household fields, debt, inheritance); C3.4
the mill, C3.5 apprenticeship, C3.6 other workshops.

### C4 transport, by slices: ~~C4.1 roads by use~~ (D2-097); **C4.2 carts** (D2-122, `24` §2: a household's handcart or ox cart where its backs were
too small - 83 % of trading loads were bound by what one carries; loads on paths and roads, wood and stone home by the cartload); next: road
works and upkeep, fords and bridges, pack trains, carts for the lords' and armies' supplies (C8).

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
