# TC2-20 — Civilisation depth: an autonomous medieval world inside a living planet

Standing direction from Arjun (7 Oct 2026, **D2-089**). It does not replace the world-legibility requirement (D2-084, `18`): it makes
it more important - every system below must become visible. Companion docs: `21_SETTLEMENT_ECONOMY.md` (structures, workplaces,
morphology), `22_MATERIALS_AND_RESOURCES.md` (resource chains, deposits, ecology impact), `23_MILITARY_AND_WAR.md` (arms, training,
fortification, war, sieges), `24_TRANSPORT_AND_MARITIME.md` (carrying, animals, roads by use, boats, ports),
`25_CIVILISATION_ROADMAP.md` (the C-phases and the first slices).

## 1. The target

Tiny Consequences 2.0 is an **autonomous medieval civilisation inside a living miniature planet**. A useful bar is the *system breadth
and visual readability* of a game like Age of Empires: settlements grow into towns, buildings have distinct purposes, workers gather
resources, roads form, mines operate, farms spread, docks and boats appear, fighters train, fortifications evolve, polities form,
armies move, wars are fought, towns are damaged and rebuilt, trade connects regions, resources constrain expansion. **There is no RTS
player issuing those orders: the agents themselves play the civilisation game.** Every building, road, army, ship, mine, wall,
workshop and kingdom arises from simulated need, knowledge, resources, labour, politics and history.

The reference is used for *readability* (resources, building purposes, silhouettes, roads, walls, armies, ports, production chains, a
place growing from small to formidable) - never copied: no assets, names, factions, tech trees, UI or exact mechanics. TC2 looks and
behaves like its own world.

The standard: *a person should not merely live in a simulated village; they live inside an evolving economy, ecology, society and
political world that they and thousands of other persistent agents are physically building* - and clicking any of them shows an
actual life behind what was just watched.

## 2. The core question for every feature (the anti-theatre test)

For every structure, good, road, force or polity:

1. **Who** wanted it? 2. **Why** (which state, which event)? 3. **What did it cost** (labour, materials, time, food)? 4. **Where did
the material come from** (which cell, deposit, store)? 5. **Who did the labour**? 6. **What knowledge** enabled it (carried by whom)?
7. **What physical object or state** resulted? 8. **What happens if it is destroyed**? 9. **How does it affect other systems**?
10. **Can its history be inspected**?

If those cannot be answered from state and history, the system is simulation theatre and is not built. Presentation may elaborate
and exaggerate real state (D2-084), never invent it.

## 3. Population philosophy (changed)

Depth before numbers. Development and tests start small and go up only as the depth holds:

| Tier | People | Use |
|---|---|---|
| A | 100-500 | high-detail development worlds and premises (one to three settlements) |
| B | 500-1,500 | ordinary civilisation testing |
| C | 1,500-3,000 | the rich mature world, the first target |
| D | 3,000+ | allowed when land, resources and history support it and performance permits |

No artificial cap. Arable land, food, fuel, timber, grazing, fresh water, fish, ore, disease, war, climate, migration and politics set
what the world sustains. The 6 km planet (D2-085) already carries 2,000-7,100 people mature: that is fine. If profiling later shows
5,000-7,000 people can be simulated deeply, excellent - but depth first.

## 4. Audit: what TC2 has vs what an organic medieval world needs (7 Oct 2026)

TC2 is not "mostly complete systems that need prettier rendering". It has strong primitives (persistent people, households,
calibrated time layers, knowledge carried by people, deeds, bands, walls, tribute, history with causes) and almost no *economic
geography*: one store per village, three village works, no workplaces, no transport, no roads, no deposits but copper, and a land that
people use without changing.

| Area | TC2 has (Core) | Missing for the target |
|---|---|---|
| People | persistent bodies, reserves, health, skills (farm, wood, hunt, fish), six techniques, kin, marriage, households, sickness, wounds, boldness/traits, an activity with a why | professions/roles, work history, more skills (mason, smith, carpenter, fighter, sailor), apprenticeship as a relationship, friends/rivals, loyalties, equipment, a full person page |
| Households | home building (camp -> hut -> house), larder, goods (gear), animals, inheritance | wealth tiers, property beyond home and herd, elite households, servants/retainers |
| Settlements | anchor + frame, square, field/wood/well spots, a granary (FU), woodstore, fibre, hides, metal (kg), crops, rationing, famine memory, lore of sites, watch, palisade ring, foe, lord | **functional buildings** (all but homes are bits or a ring), stores by material with capacity, workplaces, specialisation, morphology beyond a lot spiral, a market, leadership |
| Construction | labour + wood by stage (hut 30 h/8 wood, house 240 h/35, well 60/2, granary 120/25, palisade 2.5 h and 0.8 wood a metre) | a generic structure kind table, materials beyond wood (stone, clay, iron), condition and upkeep for every structure, damage, rebuilding, upgrades, builders recorded |
| Farming | fields claimed by cell, crop calendar, yields from climate/soil, ard and oxen, sowing more from famine memory | fallow and rotation, barns, mills, gardens, field clearing as work, field boundaries by household |
| Woods, timber, fuel | one `Woodstore` in 25 kg loads; fuel from cold and cooking (D2-056) | **woodcutting removes no forest and fields clear none** (the view hides trees under fields): timber has no source, no shortage is possible |
| Stone, clay, ore | "rock showing" within two rings gates the quern; clay within 1.2 km gates pottery; copper ore near volcanoes; metal kg as village tools | deposits as real places that are discovered, quarried and mined, depleted, carried; stone as a building material; iron; charcoal |
| Transport | people walk routes between villages (A* over cells, slope and forest cost); a load is 25 kg | pack animals, sleds, carts, roads by use, bridges, boats carrying cargo, transport time in every economic choice |
| Trade | loads of grain/metal/fibre/hides by want, relief in famine (D2-067) | markets and fairs, more goods, specialisation by exchange, trade routes that matter, ports, credit |
| Water | fishing from the shore and boats drawn (D2-053), the sea's fish | boats as built objects, docks, ferries, cargo boats, ships, coastal trade, maritime war |
| Conflict | deeds, taking, watch, blows (strength by health, age, village metal), injury, claims and amends, raids (bands of volunteers), palisade from remembered loss, tribute and lordship | arms and armour as goods, training and veterans, military roles, multi-village forces, campaigns with supply, battles, sieges, burning and capture, war famine and refugees |
| Polity | lord graph (tribute a tenth), lordship = tributaries' tributaries | rulers, succession, councils, obligations (men, labour), administrative reach and communication time, rebellion, fragmentation, capitals |
| Culture | family names; nothing else | transmitted traits (building tradition, dialect, customs) that make regions look and act differently |
| Ecology | climate-driven vegetation, abundance fields, herds and packs, fish, evolution; hunting and fishing deplete; grazing draws grass | human land use (clearing, logging, mining spoil, abandonment) as real state; richer kinds and niches drawn |
| History | causal ledger, annals, ages found after the fact, the History Book | building and settlement histories, biographies as pages, polity timelines |
| Inspectors | one text box for a person | person page, building page, settlement page, polity page |

## 5. Architecture: primitives, not features

Every new capability is a primitive shared by many systems, lived at both resolutions (ACTIVITY as people's steps, COARSE/DEEP as the
same choices at the lived rate, 04 §4), saved, hashed, deterministic, and traced in history.

1. **Structures** (`21` §1): every built thing - home, store, workplace, civic, military, fortification piece, landing - is one row of
   the `Buildings` table with a **kind** from a data table (function, footprint, placement zone, materials, labour, life, upkeep,
   capacity, techniques needed). One state machine (planned -> building -> standing -> worn -> damaged -> ruin -> gone), condition and
   upkeep, the materials actually put in, its builders, the event that moved its making, and its history. Nothing stands by a flag.
2. **Village works by need** (`21` §2): what a settlement builds next is read from real state by need tests, each with its cause event
   (no water -> well; a harvest to keep -> granary; a competent smith and a want of metal -> smithy; remembered harm -> palisade,
   training ground; heavy trade -> market). Who decides is the settlement's people today (as the palisade, D2-074) and its leader once
   leaders exist (C10).
3. **Materials and stores** (`22`): goods in real units (25 kg loads, FU, kg), each with a source cell, an extraction work, a carrying
   cost, a processing step, uses and scarcity. Stores have capacity; goods spoil or rot by kind and by the store that holds them.
4. **Deposits and their discovery** (`22` §2): stone, clay, copper, iron, salt from the planet's own geology (slope, crust, volcanoes,
   wet lowlands, coasts); a settlement knows a deposit only when its people have seen it (work grounds, scouts, travellers, news); a
   deposit depletes.
5. **Workplaces and roles** (`21` §3): work done *at* a structure or site (a smith at the smithy, a quarrier at the quarry, trainees at
   the training ground). Roles are what a person mostly does, read from their hours, never assigned.
6. **Transport** (`24`): every load is carried by someone (or an animal, sled, cart, boat) over a real distance: trip time is part
   of every extraction and trade choice. Routes keep their **use**; use wears tracks into paths and roads, which speed travel and
   fade when unused.
7. **Force** (`23`): fighters are persistent people; skill from training and battle; arms and armour are goods made at a smithy;
   service costs labour; war is people walking, eating carried food, meeting, fighting, besieging, retreating.
8. **Authority** (`10`, `23` §8): leaders from prestige (known deeds), lords from tribute, obligations (a share of harvest, men for a
   band), communication by people walking, succession at death; polities are graphs and fragment when their links fail.
9. **Culture** (`21` §6): a few transmitted traits per household (building tradition, dialect, customs) copied from parents and
   neighbours, carried by migration and marriage; regions look different because their people's traditions and materials differ.
10. **Land use** (`22` §4): clearing, logging, mining, grazing and abandonment change the planet's cells (forest share, a disturbance
    record), so the land shows its history and ecology responds.
11. **Inspection** (`21` §7, 07 §9): person, building, settlement and polity pages read state and history only.

Existing rules carried into all of these: no float arithmetic in Core (D2-057); keyed RNG; ascending-id iteration; one law for every
resolution (DEEP's "a step keeps its days", 04 §4.3b); per-day lookups indexed per settlement (D2-055 pitfall); the view never writes
state; rules shared by simulation and view live in Core.

## 6. From families to kingdoms: names for what exists

Continuum of places: families -> camp -> hamlet -> village -> large village -> town -> fortified town -> regional centre -> capital.
Continuum of polities: independent villages -> alliances and dependencies -> tribute -> lordship/chiefdom -> consolidated polity ->
kingdom -> multi-region state. **The names describe the resulting structure** (population, functions present, defences, dependants,
authority flows) and are read after the fact by historians (`11`); they unlock nothing. A kingdom exists while its mechanisms hold -
authority, loyalty or coercion, tribute flows, communication, transport, military capacity, administrators, defended centres,
dependent settlements - and collapses when they fail.

## 7. Scarcity and competition (built, never manufactured)

The 6 km planet is small on purpose. Conflict must be a rational emergent possibility and peace must be possible. The pressures that
make it so are physical: fertile valleys are finite; timber within reach is cut faster than it grows near big places; grazing is
overdrawn; game and fish deplete; deposits are rare, discovered by few and worth carrying from; river crossings and harbours shorten
real trips; harvests fail with the weather; armies eat and burn stores. Wars are never declared by a rule; they are what bands,
grievances, tribute and want become when the land is short (Carneiro's circumscription, Turchin's frontiers, `10`).

## 8. Performance architecture (D2-089 tiers)

Simulate quality at low population first (phase A ~300 people, B ~1,000, C ~3,000, then stress upward). For each tier measure world
years a second, cost per person-day, relationships, economy, military, structures, history size, rendering, memory and the Web build.
Keep the event-driven philosophy: a smith at four hours of work is one step; a structure has no per-frame update and no per-day pass
unless something happens to it (decay is read from time, upkeep is a work); an army marches as scheduled movement interrupted by
hazards and contact. New per-day work is indexed per settlement and profiled at 30 settlements (`14`).

## 9. Presentation is half of every slice

D2-084 stays a hard requirement: every system in this doc must become visible from the picture - lumber cut and woods thinned, quarry
faces and stone piles, smithy smoke, training grounds in use, roads worn by use, docks and boats, walls and gates, armies on the move,
ruins, battle damage, recovering forests. Structures get distinct, readable kit models (headless Blender, `tc2/art/kit.py`) in one
miniature art direction: low/moderate polygon counts, strong silhouettes, good materials, variants by culture and material (`13` §12).
Activity is drawn only when the simulation says it happens (no mining figure while the miner sleeps).

## 10. Working method (unchanged, restated)

Research -> architect -> roadmap -> **vertical slices**: the smallest real slice that proves a primitive, compiled, tested at both
resolutions, run in real worlds, shot and inspected, benchmarked, fixed, committed, documented - then the next. No design bible without
code. After any major implementation: update the durable docs and commit (the compaction protocol, `NEXT_SESSION_PROMPT.md`). No
subagents. Keep the shared machine's CPU and RAM around 50 %.
