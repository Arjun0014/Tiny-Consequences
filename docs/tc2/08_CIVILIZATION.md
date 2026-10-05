# TC2-08 — Civilisation: settlements, economy, growth and decline

Principle: civilisation is what households and settlements do with land, labour, knowledge and each
other; "village", "town" and "city" are names for what exists, never states that unlock anything.

## 1. Settlements
- A settlement = an anchor on the planet with a local tangent frame (metres), a **layout** (parcels,
  buildings, fields, roads/paths as a graph, open ground), stores (granary, woodstore, others), the
  households living there, and its relations with other settlements.
- Layout growth is local decisions (Emilien et al. 2012 as inspiration): a household needing a home picks
  a lot by access to a path, water, its fields, slope and safety; paths extend to new lots; heavily used
  routes become roads; parcels and fields are claimed from open ground by work (felling, clearing).
- Buildings are state machines of construction (site -> footing -> frame -> walls -> roof -> standing ->
  worn -> ruin) driven by delivered materials and labour; their kind and construction (timber, wattle,
  stone) come from knowledge and materials; condition decays and is repaired.

## 2. Economy
- Production at the activity level: fields (by climate, soil, labour, tools, seed), herds, fishing, wood,
  stone, clay, ore; crafts transform materials (techniques, tools, skill).
- Households budget (Chayanov's labour-consumer balance); villages keep social storage (granaries
  sized by remembered hunger, TC1 D-074); exchange by want (worth = what one lacks), gifts and relief
  between trusting households and settlements, credit; fairs as gatherings.
- Specialisation emerges where exchange is reliable; dependency makes chains (a severed trade route).

## 3. Growth, fission, migration, founding
- Growth needs homes (timber, labour), fields (land within walking reach), water.
- Push and pull per household (TC1 D-119, D-123): hunger, no home, crowding, fear vs kin, knowledge of
  better places, ties; founding by groups who know of a site (knowledge from contact only).

## 4. Decline and ruins
- Abandonment when the last household leaves or dies; buildings become ruins that decay over centuries
  and can be reoccupied, quarried for stone, or remembered (a place name, a legend in the History Book).
- **Implemented (D2-052)**: at abandonment the village's fields go back to the land (no owner: the grass and the woods take
  them back by the vegetation's own growth, and any village may claim them) and its ground is no longer a village's (a new
  village may be founded there: site choice already ignored abandoned villages). Its herds go wild (D2-048), its wild herds
  back to the land (D2-047). The view draws the houses of its last households as roofless ruins that sink and green over the
  centuries (a house ~300 years, a hut ~80; camps leave nothing) and its well's stone ring for ~600.
- **Homes stand on their own (D2-059)**: a hut or house is a building on its lot (`Building`, `Core/Places/Buildings.cs`) that
  outlives its household. A household that dies out (its last member dead: its kin in the village inherit its larder, goods and
  animals) or moves away leaves its home standing empty; an empty home decays (a hut falls in after ~20 years, a house after
  ~60; roofless below 0.35), then its ruin fades into the land (~80 / ~300 years) and leaves the table. A household without a
  home - newlyweds, arriving families, a camp in the village - takes the best standing empty home before it raises its own
  ("The Barnkreilds made their home in the house the Serndres had left"). An abandoned village's homes decay the same way. Not
  yet: re-roofing a ruin, quarrying it, a founding drawn to one; upkeep labour (homes lived in are kept sound implicitly).

## 5. Scale terms used by historians (labels only)
camp, hamlet, village, town, city - by population, buildings and functions present; "market town" if a
regular fair; "fortified" if defences stand.

## 5b. Storage, rationing and remembered famine (implemented, D2-042)
- Rationing to the next harvest (no lower than 45 %), and by prudence a reserve beyond it (6 days, + 30 x memory), rations no
  lower than 0.8 for prudence alone. **Memory** of famine is the share of today's adults who were eight or older when the last
  famine struck: it fades as they die, with no timer. A remembering village also sows more (fields x (1 + 0.5 memory), as far
  as its land allows).
- History: `Rationing` (a ration below 0.85; caused by the failed harvest of the last 1.5 years, else Nature), `WildFood`
  (the first need-driven trip of an episode, in response to the rationing or the hunger), `Famine` (the first hunger death in a
  year; the hunger deaths of that year are caused by it).
- Measured (seed 42, 300 coarse years): villages grow past their fields on the wild's food until the game within reach is
  gone, then live in recurring famine with nowhere to go - migration and founding are the next slice.

## 5c. Building, knowing places, moving and founding (implemented, D2-045)
- Camps, huts and houses raised by labour and wood; wells and granaries likewise (`Construction`). Nothing stands by fiat.
- Villages know places only by having stood there (scouts in pressed villages); households leave once a year after the harvest
  by a chance growing with the pressure (land too short, rations cut, famine remembered); together they found at a known place
  (a camp that builds its well, its homes and sows in spring), alone they join a known village with land to spare.

## 5d. Livestock (implemented, D2-048)
- Cattle, goats and pigs are individuals (`Beast`: kind, sex, mother, owner household, condition, pregnancy, milk), kept by
  households (`Core/Places/Livestock.cs`). Once a day with their village they graze its pasture - the open land within 900 m
  not under fields, the same `P.Grass` the wild grazers eat (out to 1,500 m when short; neighbours' herds may meet there) -
  pigs root for the same mast as boar and gatherers (`P.Gathered`) and live partly on the village's scraps. Intake by the
  wild animals' law (9 x mass^0.75 kg a year); lean in the cold (the day's mean temperature).
- Condition follows how well they ate; they breed in spring when well fed and a grown male of their kind is in the village or
  a neighbour's within 3.5 km; milk while the young are small; old age, hunger and wolves (by the wolves round the pasture,
  the young three times as exposed, grown cattle a fifth) end them.
- Households keep a breeding core per kind (the sire and his successor; the youngest females in their prime, then the eldest
  young ones, then the ageing) and slaughter beyond it what their hands cannot tend (50 kg of fodder a day per grown hand,
  children and the old half) and what the land does not offer with grass to spare (0.6 of a day's offer: the autumn
  slaughter as the grass grows lean); grown males beyond two, the old and the failing are killed; a hungry household eats its
  herd down to a pair of each kind. Milk and meat go to the larder first, the rest to the granary.
- Animals follow the generations: a child marrying out takes the youngest of each kind and sex the family keeps more than one
  of; a household that ends at a marriage passes its herd on, one that dies out leaves it to the village's household with the
  fewest; a household on the road has its herd with it; an abandoned village's animals join the wild of their kind there.
- **The village herd (D2-050)**: the households' cattle and goats go out together each day to one of the ten nearest pasture
  cells (a different one from day to day), with one herder for every sixty head - its children of eight to fifteen first
  (in a lived day a child takes the herd from 6:30 until evening; a grown member only when no child has by mid-morning, for a
  stint of four or five hours; COARSE: the same herders by turns, youths and grown ones giving 4.5 of their hours). A herd that
  had its herders the day before is a third as exposed to wolves. Pigs keep to the yards. The view draws the village's herd
  on the day's ground with its herders.
- Herders keep a mixed herd: what they kill to ease the pasture or their hands comes from the kind they have most to spare
  of (a male before a female, the most grown first); in hard times (the land cannot feed even the breeding cores) they kill
  into the cores from the kind the village has most of, keeping a sire and two females of each kind in the village.
- Measured (seed 42, 200 coarse years, D2-050): cattle 113 / 159 / 155 / 139, goats 789 / 252 / 165 / 355, pigs 108 / 78 / 81 /
  234 at years 50 / 100 / 150 / 200 for ~3,500 people; no animal starved; wolves took 4,570 in 200 years (17,402 before
  herding). Before the mixed-herd rule, cattle (the biggest eaters, grass only) were culled first and starved first as the
  growing villages' fields took the pasture: 6 left at year 200.

## 5e. Trade by want and relief (implemented, D2-067)
- Each village values each good by its own want (grain by how far its store falls short of 60 days, metal by the share of its
  people its tools leave unequipped, fibre and hides by its store a head) and spares what it holds beyond its own needs
  (`Core/Places/Trade.cs`). A trader takes a load of what their village spares to a neighbour within a day's walk that values
  it more and brings back what that neighbour spares and their own values more, at the middle of the two villages' values:
  both gain or nobody goes. Traders are people of the village (lived: they walk, exchange and come home with the load; COARSE:
  the same deals at the lived rate) and visitors who may carry a sickness home (D2-066).
- Relief: a village with grain to spare sends a share of it each day to a neighbour in famine while that neighbour's store
  holds less than a month; history tells it once a famine.
- Measured (seed 7, 200 coarse years): copper travels from the villages near the ore to all the others; four famines met by
  relief. Not yet: boats and the sea's routes, fairs, salt, credit, relief weighed by kinship and trust.

## 6. Phase
F3 (layout, construction, economy basics), F5 (exchange, migration, founding). F0: settlements as a few
place nodes with granary and woodstore, fields by season, and trips between neighbours.
