# TC2-24 — Transport and the sea: carrying, animals, roads by use, boats, ports

Part of the civilisation-depth direction (`20`, D2-089). Moving material must matter: a person cannot casually carry tonnes of stone.
Transport capacity shapes trade, construction, mining, armies and growth - and its progression comes from need and knowledge.

## 1. Carrying and its cost (C1 onward)

- A grown person carries a **load** of 25 kg (`Trade.LoadKg`), a youth of 12-15 about half; walking 1.25 m/s on open ground, slower
  up slopes and through woods (`Routes`, D2-012), slower loaded.
- Every extraction choice counts the walk: hours a load = the work at the site + the trip both ways (`2 d / v`) + loading. A quarry
  600 m away costs ~0.3 h a load in walking alone - carrying, not cutting, is most of the work, as in medieval accounts (carriage was
  among the largest items in building accounts; a five-ox team brought ~1.5 t of stone a day from a quarry).
- Historically: by sea ~1, by river ~5, by land ~28 times the cost a tonne-mile (Diocletian's edict, 301 CE) - a world where boats
  exist must find coasts and rivers valuable by the same arithmetic.

## 2. Animals, sleds, carts (C4)

| Means | Needs | Load | Notes |
|---|---|---|---|
| on the back | a person | 25 kg | everywhere |
| pack animal | a grown ox or cow (later horse, donkey) | ~100 kg | slower; eats grass on the way |
| sled | an ox, timber | ~250 kg | on grass, snow, mud; no road needed |
| cart | an ox pair, wheels (a technique: carpentry), timber, a road or firm track | 500-700 kg | fast on roads, stuck off them |
| boat | built (§5) | 0.2-20 t | by water only |
Animals working do not graze or give milk that day; a household with an ox may lend it (C4). Military wagons and pack trains are the
same means (`23` §7).

## 3. Roads emerge from use (C4)

Repeated travel creates paths; heavy, useful paths are improved; unused ones fade (Helbing's active walkers: people prefer ground
others have worn, so trails merge into minimal-detour systems).
- Each route keeps its **use**: people and loads that walked it, decaying (a half-life of a few years). Thresholds give its state:
  **track** -> **path** -> **cleared road** (a village work when use is heavy: felling a corridor, ditching) -> **maintained road**
  (upkeep as a work while used) -> **bridge or causeway** where it crosses a river or wet ground (a structure of timber, later stone).
- Road state changes travel speed (track 1.0, path 1.1, road 1.25, carts only on roads or firm dry tracks), and so trade, armies,
  migration and news.
- The view draws roads **by use** (the present drawing of a road between every pair of villages within 4.5 km is replaced: D2-084's
  audit flagged it). Unused roads green over and the woods return.
- Later, a per-cell wear field so new routes follow old trails (a field-budget decision).

**Implemented (C4.1, D2-097)**: `Core/Agents/Ways.cs` - the wear of the way between two villages (walks, halving each year unwalked; lived legs,
COARSE visits, trades and raids); track 4, path 40, road 250; a path 1.08x and a road 1.15x underfoot; `WayWorn` history; the view draws ways by
use only (ground roads by wear; ribbons kept visible far off for the beaten roads). Seed 42 at year 300: 74 paths, 17 roads. Not yet: road works
(clearing a corridor, ditching, upkeep), bridges and fords, carts, per-cell wear so new routes follow old trails.

## 4. River crossings (C4)

Fords where a stream is small (the drainage's discharge class); ferries (a boat and a ferryman) on larger rivers; a bridge when a
crossing is heavily used and timber or stone is at hand. Crossings become strategic places (C8).

## 5. Boats and ships (C9)

| Craft | Built from | Labour | Crew, load | Use |
|---|---|---|---|---|
| dugout | one large trunk | ~40 h | 1-2, 200 kg | river and shore fishing, ferries |
| plank fishing boat | ~40 loads of timber, treenails, cordage | ~120 h, boatbuilding skill | 2-4, 500 kg | fishing beyond the shore |
| cargo boat | ~80 loads, cordage, a sail (fibre, the loom) | ~400 h | 3-5, 2-3 t | coastal and river trade |
| sailing ship | hundreds of loads (a 30 m longship took ~20 large oaks, 250 crooks, 600 l of tar, 112 m2 of sail, 2 km of rope), iron rivets | thousands of hours, a master builder | 10-60, 10-25 t | long trade, war |
Boats are structures that move: owned by a household or a village, built at a landing or boat shed from real timber, crewed by real
people, maintained, damaged and lost in storms (`Storms`), drawn up in bad weather, captured later. Fishing boats fish the same fish
fields (D2-039, D2-042); cargo boats carry real loads between real stores; people aboard are real people.

**Implemented (C9.1, D2-110)**: the dugout, `Core/Places/Boats.cs` - a structure that floats (`Structures.Boat` 20), owned by a household
(`Household.Boat`), lying on dry sand at the landing of the village's best fishing ground (`Boats.Landing`: the shore nearest that ground, ring by
ring of cells out from it, then the nearest dry sand; boats follow the coast when it moves either way, `Boats.ShoreMoved`; a boat is in the water only below the
waterline, `Boats.Afloat`). Wanted when the village's **sea grounds** (open water within 2.5 km, `WildFood.SurveySea`: the offshore shoals
reachable from a boat) promise 1.25x the shore's catch an hour with the row paid, a grown member is a fisher (skill 0.5), the household is
housed, rations are not short, its woods hold trunks to hollow (forest 0.3 at the woodlot) and the store the trunk's timber; one begun a
village-day; 90 labour-hours and 30 loads; given up when not made within a year. Up to two of the household row out (`ActivityKind.Sail`;
COARSE: the day's seats); nobody goes out at 12 m/s of wind over the grounds; a trip under a rising wind may overturn the boat (up to 8 %),
each aboard drowning with 0.4; a dugout splits in 12 years. The view draws the real boats and the people out in them. Not yet: plank boats
and boatbuilding (a technique found by dugout owners), docks, cargo boats and coastal trade, ferries.

**Implemented (C9.2a, D2-111)**: a village keeps **one landing** - the beach nearest its square, found on the drawn waterline from the square toward the nearest sea, a metre above the sea (`Boats.EnsureLanding/Beach/Waterline/UpTheBeach`), kept while the sea is within 40 m; its boats lie in a row along it, each drawn down to the waterline and a metre up (`Landing`, `Drawn`); they follow the coast and a risen sea (`ShoreMoved`, `Plan`). **A boat goes out with its crew**: lived, the one who takes it out is joined by those of the household at leisure or mending, the same ground and return; one who chooses the sea within half an hour of its leaving catches it; a boat at sea is not taken out again (`AgentSystem.Crew`, `Outing`). Shore fishers stand at the waterline (`Boats.Edge`, `OutOfTheWater`); people gather at the village's green, its square or the dry ground nearest it where the sea took the square (`AgentSystem.Green`).

**Implemented (C9.2b, D2-112)**: **boats of planks** - a technique (`Tech.Boat`) found by a woodworker hollowing a dugout (likelier with metal tools),
carried and taught like the others; a village that knows it raises a **boat shed** at the head of its landing (`Structures.BoatShed`, when a household
would make a boat) and builds **plank boats** there one at a time (`Structures.PlankBoat`: 240 h at the builder's knowledge of the craft, 40 loads
taken to the shed as it is laid down, two years before a half-made one is given up): four aboard, out to 15 m/s of wind and caught out half as often,
rowed half as fast again as a dugout, reaching more of the offshore shoals, lasting 25 years.

**Implemented (C9.3, D2-113)**: **voyages** - ways over the sea between two villages' landings (`Places/SeaRoutes.cs`: A* over the sea's cells,
cached by the pair and the coast; none past 16 km); a plank boat carries a load of 500 kg (twenty walkers' loads) in trade with the coastal
village that offers the best deal for it (`Trade.BestSeaPartner`, worth a crew's day), and grain to a famine beyond a day's walk in a calm sea;
grain goes by sea only in a worthwhile load (a fifth of a boatload) and only in a boat its household can man (two who can take an oar). Relief is
**owed back** (social storage): a giver gives less to a village that has not returned its gifts; a village with grain to spare gives back what it
was given, by land or by sea (its own boat, or the giver's come to fetch it). **Lived**, a voyage is booked by the village's day on a free boat
and sailed in the morning by the household's crew: down to the landing together, rowed along the way at the boat's pace, the load dealt or given
at the other landing, a stay of three hours, home together (`AgentSystem.StartVoyage`, `ActivityKind.Voyage`); COARSE takes it whole at once, at
par (tests). Drawn: the hull at its crew on the way, drawn up at the other landing during the stay. Not yet: crews beyond the household, docks
and cargo boats, coastal trade at the sea's cost ratio (C9.4), ferries (C9.5).

## 6. Ports (C9)

landing (a beach: nothing built) -> fishing jetty and boat shed -> dock (timber quay) -> harbour (stone quay, warehouses) -> shipyard.
Each is a structure chosen by need (boats owned, catches landed, cargo moved). A port's wealth and strategic value follow from what
moves through it; no port is declared.

## 7. Communication

News, orders and tribute move with people (D2-072: visits, trades and claims carry news). A polity's reach is its people's travel time
(C10): distant settlements hear late, pay late and slip away first.
