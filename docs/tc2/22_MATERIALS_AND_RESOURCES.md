# TC2-22 — Materials and resources: from the ground to the hand

Part of the civilisation-depth direction (`20`, D2-089). No resource exists because strategy games have it. Each one needs a real
**source -> extraction -> transport -> processing -> use -> scarcity**, every step someone's labour, every quantity in real units.
TC2's year is 48 days (D2-004): yearly growth (forests, crops) happens in 48 days while daily needs (fuel, food) are daily - the
balance of a stock against its use is the world's own and is measured, not assumed.

Units: a **load** is 25 kg (one person's carry; `Trade.LoadKg`, Construction's wood units); food in FU (a grown person's day, ~0.75
kg of grain); metal in kg.

## 1. The chains

| Material | Source (planet state) | Extraction | Transport | Processing | Uses | Scarcity |
|---|---|---|---|---|---|---|
| Firewood, timber | `Forest` share per cell (full cover ~120 t/ha standing, regrowth by the vegetation law) | felling and splitting: ~1 h a load near the village, more as the woods recede | carried (1 load), sled/cart (C4) | split, dried; later sawn (sawpit), charcoal (clamp) | hearths and cooking (D2-056), every building (hut 8, house 35, granary 25, palisade 0.8/m), charcoal, boats | **C1.2: felling removes forest share; fields clear the cells they farm; the woods regrow by the vegetation law where people stop** |
| Stone | outcrops: rock showing (a cell 40 m above or below a neighbour, the quern's rule, D2-063), later scree and river cobbles | quarrying: ~4 loads an hour of rubble (metal tools faster) | 1 load a carry; a sled behind an ox ~8 (C4); boats | dressing (masonry, C7) | quern stones; footings (40 loads a house), a well's lining (20), a smithy's hearth (16), granary staddles (12); stone houses (~600), walls (~400 a metre), towers | distance: carrying dominates (a 600 m quarry ~0.3 h a load); few outcrops in lowlands |
| Clay | wet ground: a stream or lake within 1.2 km (D2-063) | digging | carried | pots (pottery), daub, later kilns and brick | storage (grain keeps better), cooking fuel, walls | wet ground only |
| Copper ore | rock showing within 3 km of a volcano (D2-064) | mining (C5): open pits at the outcrop, ~1 load an hour | carried | smelting with charcoal at a furnace | tools, arms (bronze with tin, later) | few volcanic arcs: worth trading and fighting for |
| Iron ore | bog iron in wet lowlands (stream/lake cells on flat ground) and ironstone in old high crust (C5) | digging bog ore, mining | carried | roast, bloomery: ~8 kg ore + ~10 kg charcoal -> ~1 kg bloom (experimental bloomeries: 10-27 % of ore as bloom, 5-15 kg charcoal a kg of ore) | tools, arms, armour, nails, rivets | charcoal (wood) and skill as much as ore |
| Charcoal | wood | a clamp: ~5 kg of wood a kg of charcoal, days of watching | carried | - | smelting and smithing | the woods |
| Fibre | flax from the fields (D2-063) | harvest, retting | stored | spinning, weaving (loom) | clothes, cordage, sails | field share |
| Hides | hunting and slaughter (D2-063) | - | stored | tanning (workshop, later) | clothes, shields, harness | game, herds |
| Wool | sheep (not yet a kind; goats give hair) | shearing | - | spinning | clothes, sails | pasture |
| Salt | sea water in warm dry climates (pans), later springs | pans | carried, traded | - | keeping meat and fish | coasts only |
| Grain, meat, fish, milk | fields, herds, the wild (D2-042, D2-048) | farming, herding, hunting, fishing | carried, stored | grinding (quern, mill), cooking, smoking, salting | food | land, weather, fish stocks |
| Tools (metal) | metal | smithing | - | - | work pace (D2-064) | metal |
| Arms | metal (+ wood, hides) | the smithy only (C1.4) | carried | - | fighting strength (`23`) | metal, a smith, a smithy |

## 2. Deposits and their discovery

The planet's geology decides where useful ground is - never a global ore map:
- **Stone**: rock showing (steep cells; the quern's `Learning.Rock` rule), quality by relief; later scree under cliffs and cobbles in
  river beds.
- **Clay**: wet cells (stream, lake).
- **Copper**: rock showing near a volcanic arc (D2-064).
- **Iron**: bog ore where water stands on flat land; ironstone in old, high continental crust (C5).
- **Salt**: warm, dry coasts (later).

A settlement knows a deposit only when one of its people has seen it: their work grounds (hunting, gathering and woodcutting cells),
a scout's look (D2-045), a quarrier or miner at work, a traveller's route, and later news from traders and visitors. Knowledge is
kept per settlement as a short list (kind, cell, quality, when seen) like `KnownSites`; it is copied with migration and founding.
A deposit's extraction is recorded (what was taken; a stone face is practically inexhaustible for centuries, ore veins and bog ore
deplete; bog ore regrows in decades).

Deposits then make history: a quarry or mine far from home makes a camp (C5), the camp becomes a hamlet when households move to the
work, roads wear to it, trade carries its output, it becomes worth holding (claims, raids, tribute; `23`), and its exhaustion leaves
an abandoned place.

## 3. Stores, capacity, spoilage

A settlement's goods sit in its stores: grain in its granaries (capacity per granary; grain kept beyond capacity, in heaps and pits,
rots several times faster), wood in stacks by the homes (D2-081's woodpiles), stone and ore in piles by the work, metal and arms with
their users. Stores rot by kind (grain 0.3 % a day kept well, D2-042; pottery slows it, D2-063). A burnt granary loses what it held.

## 4. Human impact on the land (real state)

- **Logging**: each load of wood felled removes its mass from the forest share of the cell it came from; woodcutters take the nearest
  standing woods within reach (the woodlot moves as the woods recede, so each load costs more walking); unused, the woods regrow by
  the vegetation law (`05`). Fuel scarcity follows where people outnumber the woods' growth.
- **Clearing**: a cell's farmed share keeps the woods off it (the vegetation's capacity there is what the fields leave); field cells
  released at abandonment (D2-052) regrow by the same law.
- **Mining and quarrying**: disturbed ground at the site (a disturbance record per cell: kind and time - the D2-084 candidate for
  scars), spoil heaps, a face that grows.
- **Grazing**: herds draw the grass (D2-048, D2-050); overgrazed pasture recovers slowly.
- **Hunting, fishing**: deplete the abundance fields (D2-042, D2-039).
- **War**: burnt fields, burnt structures, abandoned land.
- **Collapse**: farms disappear, the woods take them back, wildlife returns (all through the same laws).

## 5. Implemented

- **Timber from the woods (C1.2, D2-091)**: `Core/Places/Woods.cs` - the woodlot (nearest good woods within 1.5 km), each load's mass off
  its forest share (~120 t/ha at full cover; felling leaves 0.02), the walk paid per load and per stint, fields keep the woods off their
  farmed share (fixed point with arability), released fields regrow, `WoodsReceded` history. 300 years on seed 42: 41,600 t felled,
  woodlots ~770 m out at year 300; cleared fields fully arable (arable land +39 %, people +41 %). Not yet: clearing as labour and as timber,
  sawn timber vs firewood, charcoal.
- **Stone and the quarry (C1.3, D2-092)**: `Core/Places/Stone.cs` - outcrops where land breaks 80 m from a land neighbour, above the
  shore; known only where its people went (grounds, woodlot, scouts), within 1 km; footings wanted for the next houses; a quarry at the rock;
  ~3 loads an hour cut, every load carried home; houses on footings last twice as long once left; villages without rock at hand build on
  posts. Seed 42, 300 years: 13 of 19 villages quarry, 2,079 of 3,278 houses on footings. Not yet: masonry, stone walls, dressed stone,
  sleds and carts for the carry (C4), upkeep of lived-in homes (C13).
- **Copper ore bodies and the mine (C5.1, D2-100)**: `Core/Places/Mining.cs`, `World.Deposits` - copper on 15 % of the crags within 2 km (geo)
  of a volcano, 1-20 t a body (keyed by its cell), kept from the first sight and counted down by what is dug; seen where its people go, kept
  within 2 km; a mine at the body when someone can smelt and the tools or arms are short of metal; 25 kg an hour dug, every load carried home;
  smelting takes 5 kg of ore a kilo; a body gives out (history), its mine is left, the village looks again. Seed 42, 300 years: 12 of 19
  villages know copper, 7 of 23 bodies seen dug out, 64 t dug. Not yet: iron and bog ore, smelting at the mine, ore traded, claims.
- **Charcoal (C5.2a, D2-101)**: `Core/Places/Charcoal.cs` - a hearth at the woodlot (the next at the woods when they recede), burners felling
  by the woods' law and burning 5 kg a load, the clamp smouldering three days, 2 kg a kilo of copper (0.75 of that at a smithy); smelting
  stops without it. Seed 42, 300 years: 21.9 t of charcoal from 109 t of wood. Not yet: charcoal traded; iron's appetite (C5.2b).
