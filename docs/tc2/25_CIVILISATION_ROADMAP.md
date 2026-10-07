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
2. **C1.2 Timber from the woods**: felling removes forest share at the woodlot (which moves as the woods recede: walking time per load);
   fields keep the woods off what they farm; regrowth where people stop.
3. **C1.3 Stone**: outcrops known by people who saw them; the quarry as a work site; stone cut and carried (trip time); the stone store;
   footings (homes last longer), a well's lining, the smithy's hearth.
4. **C1.4 The smithy**: a workplace raised when a competent smith and a want of metal meet; smithing there (faster), arms forged only
   there; arms in fights.
5. **C1.5 Training ground**: practice hours, fighting skill, veterans; strength uses skill.
6. **C1.6 Market and gates**: a market place where trade is heavy; the palisade's gates as structures.
7. **C1.7 Pressure**: a premise with two settlements and one scarce outcrop or ore; observe what emerges (trade, claims, taking, raids,
   fortification, migration) - nothing scripted.
Acceptance: a tier-A world (one to three settlements, 100-500 people) shot at its founding and decades later at the village and street
views shows the differentiation; every structure is clickable; headless runs at both resolutions and DEEP agree within tolerance.

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
