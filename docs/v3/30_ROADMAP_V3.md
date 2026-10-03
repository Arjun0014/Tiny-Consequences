# V3-30 — Roadmap v3: an automated miniature civilisation (D-078)

Supersedes `docs/world/17_ROADMAP.md` for v3 worlds. **v3 is the default game since session 10
(D-079)**, before parity, because it is the chosen direction; the v2 world stays playable as a
legacy/debug fallback (`?world=v2`) and its code is retired only when v3 reaches parity.

Method per phase: one complete vertical loop → run headless (`tools/v3_run.lua`, follow
people) → watch it (`npm run shots:v3`, the game with `?world=v3`) → profile (`tools/v3_scale.lua`)
→ fix → expand. Tests in `tests/sim/unit/test_v3_agents.lua` grow with each phase.

## R0 — Direction and slice (session 9, done)
Agents with continuous motion and A* on a settlement map, utility choice over a job board,
farming row by row, woodcutting tree by tree, carrying, building a house for a waiting couple,
clearing new fields, foraging, chores, rain reactions, life stages, births, marriage; the v3
calendar and speed ladder; skinned villagers from the Blender pipeline; inspector and follow.

## R1 — The slice made solid (session 10)
- Watch a full year headless and in the game: harvest, threshing, autumn wood, winter fuel and
  hunger, the second spring; fix what reads wrong when following people (logs per trip, seed
  round-trips, crowding at the same tree, evening gatherings).
- Infants drawn in arms; children follow a parent sometimes; apprentices work beside their mentor.
- Night: lit windows from people indoors and awake; chimney smoke from hearths that burn logs.
- The far LOD at ~150-250 triangles; animation blending per action; facing toward work targets.
- Catch-up mode in the UI (⏭: "to dawn", "a season") with the people hidden; HUD speed buttons
  relabelled for the v3 ladder; the inspector's memories and relationships. (D-079: the watch
  ladder stops at ▶▶▶; skips until dawn / evening / a day / the next season, keys 4-7.)
- Performance: why-strings built lazily (on inspection), fewer RNG hashes per decision; A* on a
  coarse grid for long trips; the planet's daily step amortised. Target: 200 agents at ▶▶▶ < 2 ms
  per frame in the HTML5 release, a season of catch-up in ≤ 10 s.

R1 status (session 10, D-079-D-082): done — a whole year watched and fixed by mechanism (rotation,
labour, cooking, hearths, tools, wild food, repairs, care across the generations, own fields,
spare hands clear land), lit windows and smoke, far LOD, skips. Open: winter work (textiles,
livestock), catch-up speed with several settlements (154 ms of sim per game hour for 103 people
in the debug build), the daily planet step amortised.

## R2 — Settlements across the world
- 4-8 settlements of 20-30 founders on different landmasses (worldgen `settlements`), each with
  its own culture's houses; sites near water preferred (coasts, rivers with fords).
- Coasts and rivers on the local map from the planet's render coast (a shared pure coast function)
  and the river network; bridges and fords as lane tiles people build.
- Fishing: piers, real boats (entities) rowed out to the grounds, catch landed and carried.
  (Session 10: 4 settlements of 18-36 founders, north only (D-083); docks, boats and the fishing
  loop in the sim (D-084). Open: the shared coast function so the local map's water matches the
  rendered coast; southern founding states; rivers and fords.)
- Storage and spoilage, the store a village wants (D-074's memory of hunger re-expressed).
  (Session 11, D-085-D-087: one shared surface for coasts and rivers with fords and floods; both
  hemispheres founded in their local season; sites by annual climate, drawn to shores and rivers;
  fishing as an occupation with household boats, smoking and boat building. Open: bridges, water
  from rivers, river fishing.)

## R3 — Animals as individuals
- Herds/packs (W4 fauna) stay the macro ecology; each visible animal is a micro-agent (graze,
  wander, separation/cohesion, rest, follow its mother, flee, chase) steered by the group's
  objective. Kills remove a specific animal; hunters stalk a specific animal.
- Livestock (sheep, goats, cattle, pigs, horses) from the Quaternius pack, herded by people.
(Session 11, D-088-D-089: textiles as winter work; sheep as individual micro-agents in the shared
event queue — herded by children, pasture, hay, lambing, shearing, slaughter — drawn as the
procedural movers. Open: animated Quaternius models, goats/cattle/pigs, milk, manure, predators,
wild herds as individuals, hunting a specific animal.)

(Session 12, D-092-D-096: herds of sheep, goats, cattle and pigs as individual animals with
milk, cheese, wool, dung to the fields, sickness and culls, drawn with the animated Quaternius
models; wild animals on the maps as members of the W4 herds and packs; wolves hunting at night and
taking penned beasts when hungry; night watches; hunters stalking a specific animal; rivers with
bridges built where crossings are wanted, river water and angling; fishing crews of two, drying
racks, squalls. Open: dogs, horses and oxen, foxes and bears, small game, animated deer and
wolves, hides used.)

(Session 13, D-104, D-105, D-108: hides to leather to shoes; animated wolves and dogs (dogs with the
herds and at the pens, wolves at a pen met by barking dogs and woken people); oxen at the plough.
Open: small game, bears and foxes, dogs breeding, a plough model.)

(Session 14, D-111, D-113: the ox team placed and turning as one, a plough drawn, ploughing men's
work; birds as agents: gull flocks with a roost, hunger, objective, target and why - boats hauling
nets, landings, racks, ploughs, carcasses, the shore, weather and the day; inspectable. Open: other
birds (rooks, crows, herons, raptors, songbirds), people chasing gulls.)

## R4 — Exchange and travel
- Paths between settlements, porters and traders walking with loads; trade boats; relations from
  v2 (D-055, D-062, D-066) re-expressed between settlements of agents; gifts, relief, feasts.
(Session 12, D-097: one complete land loop - a fishing household's porter walks smoked fish to a
reachable village with grain to spare and little fish, sells into its granary, walks home with
grain; gates, a timed road leg drawn on the planet, provisions, guest meals, both ledgers. Open:
more goods (wool, cloth, logs, livestock), sea trade by boat, prices remembered, relations.)

(Session 13, D-100-D-103, D-106, D-107: sowing to need; the balanced pot; worth, news that fades,
exchanges both sides gain from with the return good by the trader's own want, gifts in the village,
relations and debts; sea trade by boat, one complete loop; relief asked of a trusted neighbour;
floods sweeping bridges; river water and the flux. Open: livestock traded alive, help by sea,
relations shaping marriage and migration, larger trading boats.)

(Session 14, D-110-D-112: trade on faded news discounted (`trade_guess`), roads walked by day with
night camps and lodging; the browser and LuaJIT live the same world; drainage basins, rivers from
discharge, villages on one bank, sea routes from a map's sea. Open: livestock traded alive (drovers),
relief by sea, relations shaping marriage and migration, larger cargo boats.)

(Sessions 15-16, D-114-D-121: the browser-only first-hours cost was the CPU rasteriser (probes now
draw nothing while measuring); animals as individuals - no places in a herd, livestock and wild
bands within their own range of the herder or ground, wolves on the leader's trail with drifting
places, flanking from where they are, waiting their turn at a kill, dogs with posts and balance
points, the drove as individuals, natural spacing - with synchronisation measured (velocity
correlation, mass starts/stops/rests, offsets, young-mother distance) and regression-tested; every
animal clickable with its why; beasts sold alive and driven to the buyer; relief by sea; courting at
a neighbouring village (the first kin across villages). Open: sea droves, beasts as payment, larger
cargo boats (no trade volume yet to justify one), kin visits across villages, a groom moving to the
bride's village.)

(Session 17, D-124-D-126: roads follow the land (A* over the planet's cells: slopes, forest, high
ground, rivers forded); kin ask kin at other villages for food and offer a place to live; the quiet
economy diagnosed (`tools/v3_market.lua`: everyone short of the same textiles and leather, grain
worthless everywhere, trips gaining less than the road) and woken without forcing: reachable stock
targets, the road's cost as the work given up, work for exchange, gifts both ways on kin visits,
news visits - 21 exchanges in the untouched year, by road and sea, first on day 31 with the southern
wool and leather. Open: goods only some places have (salt, tools' stone or metal), fairs, credit,
larger cargo boats when sea volume asks.)

(Session 18, D-130, D-132, D-133: boats stay in for storms along the way, not rain (long crossings had
been possible ~4 days a year); small sea droves (a few sheep or goats lifted into the boat); salt, a
good only the coast makes - salt pans built by work, salt boiled with wood and sun, it cures meat and
fish without smoke and keeps cheese - traded inland by road: 34 exchanges in the untouched 48 days (19
before), the first on day 24, Tekheim and Little Leelee trading at last. Open: stone and metal from
geology, fairs, credit, larger cargo boats.)

(Session 19, D-138-D-144: trade planned on a snapshot of each household's worths and spares, seen
and guessed gains kept apart (unsold trips 64% -> ~25%); **stone** from outcrops placed by the
planet's slope and height, quarried and carried a stone at a time, querns (cooking quicker, traded to
villages without rock) and stone footings; **iron** whole - veins and bog iron known when seen, ore
dug and carried, charcoal burned in a clamp, blooms smelted and edges forged and fitted to households'
tools, traded where there is none; the sea's danger weighed; **baskets and credit** - a whole good paid
with a basket of spare goods, the rest owed between trusting households and collected in kind;
**fairs** after the harvest at the middle village of a road cluster, heard of through people and
criers, gone to for reasons, barter person to person, news, kin and matches. Untouched 192 days: 439
exchanges, deaths only old age and illness. Open: fairs by boat, querns and iron at fairs, larger cargo
boats, stone bridge piers.)

## Geography (session 14, D-112)
Drainage basins on the planet (outlets, a priority flood, beds falling to the sea), rivers widening
with discharge from mountain sources, valleys and floodplains carved into the drawn planet, mountains
with silhouettes, crags, snow and a tree line by real height; the same rivers on the settlements'
maps. Open: lakes, deltas, local-map valleys, a finer mesh near peaks.

## R5 — Migration and founding
- Households leave by land or boat when push factors (hunger, no home, crowding) beat staying;
  colonists found new settlements (the founding generator as a process: stakes, huts first).

(Session 16, D-119: households move to another village from real push (hunger lasting days with
each household's own patience, no house, wolves at the pens) against pull (grain heard spare, kin
living there, trust) less what holds them and the road; physically: packing, the household's beasts
led, gathering at the gate at an agreed hour, the road together, babes carried and fed on the way;
lodging with kin or the household with room, a house planned and built for them, houses left empty
given to waiting couples and households. A bride's move (D-120) uses the same road.)

(Session 17, D-123: **founding done**. Knowledge of places from real contact only (`s.lore`); who
founds and why (hardship with nowhere to go, couples with no lot, no room for fields; not without food
to take); the party packs its part of the larder and granary, leads its beasts and dog, walks the
land route; the place exists from the moment they walk onto it, with nothing built; camps, water from
the river or a well, huts on lots cleared by felling and grubbing, the camp struck, fields, a granary
once sown, a woodstore, pens; every system tolerant; the view adds the place at runtime; places,
founders and beasts inspectable. Test `test_v3_found.a_crowded_village_founds_a_new_place`. Open:
moving and founding by sea, sea droves, hunters refreshing knowledge, huts rebuilt as houses, planet
markers for small places.)

(Session 18, D-129, D-131, D-134, D-135: **moving and founding by sea done**. A household with its own
boat, a skipper and room for everyone moves over the sea to a village it knows: the larder shared, goods
and a few small beasts loaded, all gathered at the dock, storms or someone missing put the sailing off to
the next morning, the voyage carries every rider, the landing makes them the village's and moors the
boat there. A household founds on a shore its village saw from the sea: the place made as they sail into
its waters, a beach the sea reaches, camp and work as by land, a dock built later; weighed against land
on the hold's seed and bread vs the beasts left. Families share food on the road and in boats, gather
along the way; starving people do not found; huts become houses; hunters refresh the country. Tests
`test_v3_sea`. Open: a ferry for boatless households, planet markers for small places, founding
couples given parents.)

(Session 19, D-139, D-140, D-145, D-146: **the ferry done** - a household with no boat is sailed over
by a household of its village with one (kin, friends, partners, any neighbour for a fare); the skipper
keeps his word, packs his own provisions, takes them aboard, lands them and sails home in the same
boat. Hardship is judged by each household (its larder and part of the granary), so fishers with full
larders stay; leaving couples take no house; hunger answered by the people (beasts killed for the pot
and for neighbours, the shore, journeys put off); the generator's waiting couple has parents; a young
place is seen - the canopy opens close in, the selection ring marks it, the inspector says camp,
hamlet or young village; a famine is survivable (D-147: relief from need, no waiting with nothing
left, no moving into a camp with nothing, a day at the edge before death - inland total famine: hunger
deaths 24 -> 1). Tests `test_v3_ferry`, `test_v3_families`. Open: sailing together (built and removed: no
real premise - boat households fetch relief by sea), a ferry for founders, labels for small places.)

The plan as it was written (done, as above):
1. **Who and why**: a household (or kin together) pushed hard with no village that would take them
   or pull them enough, or a young couple with no lot at home (`find_lot` fails, `s.couples` waiting
   long), weighs empty sites it knows of (cells along the roads and coasts travellers have seen -
   site knowledge from trips, never omniscient) by water, soil, wood, safety and distance.
2. **The map first, empty**: `settle.new(W, cell, ...)` makes the terrain, rivers, coast and trees
   only (found.lua's buildings, fields and founders are what the party must make). The settlement
   exists with no buildings and no members until the party walks in at its edge.
3. **The party** walks (or sails) through the real geography (routes avoid mountains and wide
   rivers; passes and fords matter), camps at night, carries food, seed grain, tools, leads beasts.
4. **On arrival**: a camp (`hh.camp`, sleeping out by a fire), wood gathered, a first shelter (a low-
   work house site), water from the river or a well dug (a site), food from what they carried,
   foraging, hunting, fishing; a field cleared and sown in season; a granary and a woodstore when
   there is something to store; a pen when beasts came.
5. **Systems that assume a founded village** must tolerate one with no well, granary, woodstore,
   dock or pens (fetching water, the village's stock, trade partners, the view's settlement drawing,
   planet markers and local maps appearing at runtime).
6. Tests: a pushed household with no village to go to founds a place; nothing appears that nobody
   built; the first winter is survivable only if they worked for it.
- Also open: moving by sea (a household with a boat), pull from marriage ties (now possible).

## Phase order (session 20, D-148)
R4 Exchange and travel -> R5 Migration and founding -> **R6 Conflict** -> **R6.5 Living Planet &
Emergent Response** -> R7 Player and causal chains. R6 and R6.5 are built on one set of shared
primitive properties (`docs/v3/50_PRIMITIVES.md`): deeds (what happened, by whom, known by whom),
danger noticed by the senses, an urgent choice that puts dependants first, injury, the condition
and contents of things, boldness and arms, planet fields read on the maps. Research
`40_RESEARCH_CONFLICT_PLANET.md`; audit of what is live `41_PLANET_AUDIT.md`.

## R6 — Conflict (designed: `docs/v3/60_R6_CONFLICT.md`, D-149)
Not raids by decree: people weigh taking things (need, the thing's worth, being seen, ties to the
owners, boldness), walk, take a specific beast, are seen or not, remember; owners discover, track,
watch, claim, take back - each a choice from state. Stages, each a whole loop watched before the
next: **R6a** a stolen beast (deeds, boldness, opportunity in what visitors saw, the take trip,
dogs and witnesses, discovery and tracks, news of deeds, trust, the watch); **R6b** claims and
settlement, taking back; **R6c** confrontation and injury, arms from the household's iron; **R6d**
sudden-death aftermath for all kin; **R6e** raids (a party of kin and friends, each deciding);
**R6f** defence that is built (a stout fold, a palisade, by remembered losses against labour and
timber); **R6g** how conflict ends (compensation, marriages across the line, trade dependence).
Captives only as specific people with a way home; alliances and flight from insecurity later.

(Session 20, D-150-D-153: **first pass of R6a-R6g built**: taking beasts from a village one's people
have seen, weighed by need or an unredressed wrong against risk and ties; lying up until the village
sleeps; dogs bark at strangers, owners go out, chase, strike; tracks; claims answered by return,
payment and debt, refusal or denial; injury healed with care or fatal; every death a deed with kin
mourning and a burial; raids raised from lone failures, each man joining for his own reasons; amends
offered unasked; a fold wall built from remembered losses against thieves and wolves. Open: palisades
round the houses, arms carried, captives, alliances, moving for insecurity, restocking raids.)

## R6.5 — Living Planet & Emergent Response (designed: `docs/v3/65_R6_5_LIVING_PLANET.md`, D-148)
So that R7's god powers touch physical state and the world answers. **R6.5a** shared foundations
(from R6); **R6.5b** lightning on a village (strike at a point, injury, local fire, gathering,
firefighting, rescue, aftermath by existing systems) - the first environmental vertical; **R6.5c**
wildfire between planet and map, animals fleeing; **R6.5d** snow and cold on maps (the dzud chain);
**R6.5e** flood water beyond the channel; **R6.5f** geology on static plates (volcano sites and
pressure in v3 worlds, eruptions: vent, ash, aerosol - whose cooling already reaches v3 crops;
boundary stress and earthquakes; landslides; tsunami last); **R6.5g** climate extremes in the v3
calendar (coastline from sea level, an SST oscillation with fish, ice age and hothouse watched).
Acceptance: a god power is a few lines writing state; no outcome scripts.
(Session 20, D-154: **R6.5a foundations and R6.5b lightning on a village built** - strikes on the
tallest thing, fire lived continuously and spread by dryness and wind, out of the house with the babe,
water carried, houses lost and households lodged with kin; drought by day saved, drought at night
lost. Later, D-155-D-164: **R6.5c wildfire**, **R6.5d snow and frost**, **R6.5f quakes, volcanoes and
tsunami**, beasts and fields in fires, a first **R6.5g** watch (a hard winter). Not yet: **R6.5e flood
water** - the planet's rivers do not rise enough under heavy rain; hydrology first.)

(Session 21, D-169, D-170: **R6.5e flood water built** - infiltration excess where a planet step is a day,
water over the land by height above the river, houses, fields, beasts and people in it, the urgent choice, dry
roofs; **R6.5g watched** - an ice age and a hothouse for 192 days each, heat stress on grain and grass. R6 gaps:
arms carried when danger is known, kin who walk over to help kin who lost their house. Not yet: the coastline
following sea level (planet-century time scales), the palisade, captives.)

## R7 — Player and chains
- God powers on the v3 world (lightning on a tree or a person, rain, drought, cull, oil spill on
  the ocean (fishers respond), meteor) - each writing physical state that R6.5's systems answer;
  the chain of causes from any event back to Nature or You (deeds carry it); the chronicle from
  agents' memories and deeds.
(Session 21, D-171-D-173: **R7 rethought** from the state a god can touch (`70_R7_GOD_POWERS.md`: v1 lightning,
rain, dry spell, warm/cold air, stir the mountain, strain the fault, cull, bring; never outcome powers);
**the player experience** (`75_PLAYER_EXPERIENCE.md`: watch, notice, go there, understand, intervene, watch
the consequences) with **the world news built** (stories from deeds, events and live state; click to go there;
Why? from real links to NATURE / PEOPLE / YOU) and **world audio built** (`80_WORLD_AUDIO.md`); **the v1 powers
built on the v3 world** (D-174, `sim/powers3.lua`; consequences rooted at YOU); everyday life in the news (D-175).
Next: validation and play-tuning, R7 v2 powers (sickness in a herd, blight on a field, sea warmth, oil, meteor).)

## Never cut
Determinism, causality (every major change traceable), the planet layer, no scripted
consequences, persistent people with continuous positions.
