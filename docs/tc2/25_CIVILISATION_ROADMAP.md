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
and loads moved) and cargo boats, coastal trade at the sea's cost ratio; **C9.5a grain across water goes by boat** (D2-162: within a day's walk grain moves at once and traders walk only along a way on foot; across water relief, grain given back and the lord's tenth - in the tributary's boats or the lord's, come for it - go by sea or not at all); **C9.5b a boat to cross** (D2-163: a village whose neighbour across the water is worth reaching makes a boat for that; a dugout carries a load across a short water - the archipelagos trade and feed each other again, voyages x2.4); **C9.5c people across water** (D2-166: a COARSE day's visits - the sickness and news they carry, the ways they wear - go only where a way on foot leads, as lived visitors do; in the archipelago two thirds of them had crossed water at once); visits by boat for themselves (island woodcutters, kin across a strait, a town on two shores); ferries; then the coastal proof (`§5`).

### C10 governance, by slices: ~~C10.1 the lord's call~~ (D2-107; the lord's answer D2-108); ~~C10.2 standing and the village's first household~~
(D2-127); ~~C10.3 a contested succession splits a village~~ (D2-128); ~~C10.4 bread and dependence~~ (D2-157); ~~C10.5 a lordship's might~~ (D2-159);
~~C10.6 tribute carried to the lord's store~~ (D2-161: its labour on the ways after each harvest; a fifth of the tribute has no way on foot to its lord - by boat since C9.5a, D2-162); ~~C10.7a a
lordship's might by lasting strength~~ (D2-164: villages weigh each other's people of fighting age by their years, tools and arms and fighting skill, not
by heads); ~~C10.7b sworn men~~ (D2-165: the one first among a village's people, whose household's grain runs years beyond its needs, keeps young men of the
village sworn to them, fed from that grain - a lord's from its tribute; they practise every day and go with every band; retainers, `23` §3); ~~C10.8 the asking~~
(D2-168, landed 25 Oct 2026, sim 91; `10` §12k: a free village is asked a tenth at its harvest by its nearest stronger neighbour whose first household keeps sworn men - it bows where its
people could neither stand against them nor go elsewhere, else the refusal is a wrong the asker holds; chosen by measuring first: bonds outlive their lords but
hold by strength, so succession at a death changes little - what limits the lordships is that they rarely form, 514 pairs where one could make the other bow
against 26 bonds; realms of 8-16 villages by year 300 and a cycle of hegemonies over 900 years, its war costing about a tenth of the people while the worlds grow); ~~C10.9 the lord's death~~ (D2-173, sim 92: bonds for peace lapse with the one first among the lord's people; heirs win their tributaries again by asking - raids x0.64, realms change hands at deaths); ~~C10.10 the lord's peace~~ (D2-174, sim 93: villages of one realm raise no bands against each other, their blood judged by their common lord - the measured war was feud, 30-88 % of raids between villages of one realm; war moves to the realms' edges, famines a person-year x0.82); then by evidence: war between realms (C8.1's room may open there); commendation (a wronged village that cannot answer its foe puts itself under a protector: 24 such at year 300 in ten
worlds), a lord's death and its tributaries once realms are held by the margin, rising together once realms are large. Done meanwhile: ~~a polity map layer~~ (D2-167: the realms map layer - each lordship's land in its
colour, chevrons from tributary to lord, a legend; HUD R).

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
D2-136: retirement to a child's household - the hoards go, landlessness falls); ~~C3.6g the land between villages~~ (done, D2-176: leavers who know no
free good place and find no village to join found a daughter village on their own village's margins, beyond every village's fields - the room measured
between villages, not on poorer land; the map settled twice as densely by year 300, people x1.16-1.33); ~~the household's own year~~ (done, `26` §2h-§2k: C3.7a the garden
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

**First run (session 25, 25 Oct 2026; main sim 94 - C10.8-C10.10, C11.1a; `tc2/out/tools/s25/accept/notes.md`)**: seeds 2, 4 and 7 for 1,000 years
(`_ac1`, saved at 50/150/300/500/800/1,000; books; real shots `s25m_*`; the Web build over year 1,000). **Holds**: a wild planet, first villages, founders,
larger settlements, walls, realms in tiers that rise and fall (seed 4: Touldandholm 30 of 34 villages at year 917, 18 at 1,000), a great plague (seed 2:
-52 % over 450-600 and recovered by 650), peoples with their own speech, and books that tell all of it as history (sixteen or seventeen ages a world, the
realms as reigns). **Does not hold**: after the land fills (~year 300) the map stops changing - 2-3 villages abandoned in 1,000 years, almost no foundings,
no ruins or rebuilding, no town drawing in its hinterland; years 300 and 1,000 cannot be told apart from the picture. The next slice by this evidence:
settlement change at the ceiling (measured first: why no village empties - the plague included - and why none is founded).
**Second run (C3.6g, D2-176; sim 95; `_ac2`)**: the land between villages - 15-20 % of the arable land, inside the spacing and beyond every village's
reach - fills with daughter villages before ~year 300 (seed 2 57 places at 300 against 28, seed 4 60 against 31, seed 7 20 against 10; 12,797 / 14,362 /
2,707 people at 1,000 against 10,083 / 11,494 / 2,131); after the infill the map still changes slowly (300-999: 12, 21 and 2 foundings, 2, 4 and 1
abandoned). Measured next: small villages live better, not worse (`run --sizetally`) - no rule empties them; war's burning (C8.0, D2-177) leaves
burnt homes, ruins and rebuilding (5-45 homes a world in 300 years).
**Third run (main's final rules, sim 98: C3.6g, C8.0, the powers uncast; `_ac3`; books `tc2/out/tools/s25/accept/book3_*`) - seed 2 judged, seeds 4 and 7 in
the queue**: 1,000 years in 29 min; people ~10,000 by 310, held 9,400-11,700 to 730, the wasting pox of 719-748 killed 3,737 (11,462 -> 7,784), recovered to
12,000-12,700 by 850; Reaikford's realm held 48 villages at 741; 178 homes burnt in 35 villages over the millennium; the book reads as history (eighteen
ages: feuds and raids, the walls, the lords, the pox, ruin - quakes and great waves). **Still**: places 55-56 from 290 to 630 and 62 at 1,000 - founded and
abandoned 0 in most ages after the infill: the map's settlement pattern stays as it was at ~300 (the realms and the record change; the villages do not).
The siege was measured on these worlds and set aside (`tc2/out/tools/s25/siege/design.md`: no band the walls held off could stand a sally).

## 7. After the C-phases
F9 (the god's powers beyond those that exist), F10 (art and presentation: animation of labour, UI art pass), F11 (platform).

## 8. Toward a finished core simulation (session 25, 24 Oct 2026 - Arjun: "push toward a strong finished core simulation within the next 2 weeks")
**What "finished core" means here**: every C-phase's "done when" (§1) met by systems that exist and were judged by the standing method (measured first;
ten seeds against a control; a matched placebo for any lean; real shots), and the 1,000-year acceptance (§6) passed on several seeds - judged in real
shots at the ages and in the History Book (D2-171), which now tells a world's realms, rulers, wars, disasters and threads of cause. Then F9 (the god's
powers as play) and the player-facing game. The order, each a slice that answers "what changes in the world picture?":
1. ~~**C10.8 the asking**~~ - landed (D2-168, 25 Oct 2026) after its matched placebo judged its one lean real and explained by its war. Its 900-year books (`book --load` on `_l9dm1`) show realms
   that rise and fall in turn - seed 2: Taildvehope's realm held 24 villages in five tiers by year 338 and was gone by 829, Elddal's 19 for eleven
   years, Vaildwick's, Skaenstead's and Mestskouford's 22 each - a cycle of hegemonies, not one realm that grows for ever.
2. ~~**C10.9**~~ landed as **the lord's death** (D2-173) - the measured counterweight; rising together not needed by the evidence. Then **C10.10 the lord's peace** (D2-174), chosen over C8.1 by measuring first. Was: **C10.9 rising together** - the tributaries of one lord that together outweigh it stop paying together (the record: Kent's rising under Eadberht
   Praen at Offa's death; the tributary kings' coalitions against overkings): the mechanism by which an overgrown realm breaks, beside the lord's weakness.
   Measured first (`lordships --margins` per realm: how often a realm's tributaries together outweigh their lord while each alone does not).
3. ~~**C8.0 the burning-in**~~ - landed (D2-177, 25 Oct 2026): a band that holds the field fires the homes of the raided village's people who killed its own;
   the vengeance answers the killing (measured first: small villages live better - no desertion rule; killings, not wounds, bound what is burnt).
   Then **C8.1 the campaign and the siege** - a lord's band with its tributaries' men (the call exists, C10.1) against a walled place: a blockade until its
   stores fail or the besiegers' do (C8's "wars change the map": a taken town becomes a tributary or is burnt).
4. **C7.2 stone walls** - a wealthy walled town that knows stone (C1.3) rebuilds its palisade's stretches in stone; towers at its gates: fortifications
   that differ by region and history.
5. **C11.1 a people's ways** - ~~C11.1a a people's tongue~~ (D2-175: names in their founders' speech, a sound changed at a time; the book's Peoples); next C11.1b households carrying their ways (marriage, migration; villages that blend). Was: - the household traditions that exist (D2-114's building forms, names) spread by founding, marriage and migration and drift:
   regions that look different with no factions (C11's done-when), drawn in the view.
6. **C13 the 1,000-year acceptance** on three seeds: shots at years 0, 50, 150, 300, 500, 800 and 1,000 (orbit, region, village) and each world's book.
Alongside: the orbit look (D2-170: done), places named on the map (D2-172: done), the book (D2-171: done; next its portraits of realms on a map and a
timeline page), great worlds in the browser (`14` §7: ~1 GB and ~8 fps at the region view - a memory and frame pass), W-FOREST pass 3.
**Then F9 - the god's powers as play**: the powers that exist (eruption, quake, storm, lightning) and new ones that act through the world's own systems
(a blessed or blighted harvest, a drought, a flood, a plague, a strange stone of ore), each a logged command whose consequences the book's "Your
Actions" thread follows (`11` §7) - the player-facing game.
