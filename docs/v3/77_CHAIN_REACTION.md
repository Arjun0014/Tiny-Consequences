# V3-77 — Chain reaction: what did my action cause? (session 23, D-182)

The game has three questions the player can ask of the world:

| Question | Interface | Built |
|---|---|---|
| WHAT IS HAPPENING? | World News (`75_PLAYER_EXPERIENCE.md`) | S21 |
| WHY DID THIS HAPPEN? | the card's "Why?" - real causes back to NATURE / PEOPLE / YOU | S21 |
| **WHAT DID I CAUSE?** | **Your actions** - each intervention and the consequences that came of it | **S23** |

The themes are SIMULATION + CHAIN REACTION: the third question is the chain reaction made legible.

## 1. Principle
Every node of a chain is a reading of a **real causal record** or of **live state joined to it by a real
link** - never text made from the power's kind. The same power cast in two states must show two different
chains (rain on a drought: the soil watered, the fields drinking it; rain on a wet floodplain: the river over
its banks, a field drowned, a household under a neighbour's roof). If two casts show a canned "rain affected
farming", the system is wrong.

## 2. The causal records that exist (S21-S22)
- **Events** (`W.E`): every event's parent and root are kept forever (`W.E.parent[id]`, `W.E.root[id]`); a
  god power is a root event with actor `player` (`sim/powers3.lua`).
- **Deeds** (`V.deeds`): `answers` (the deed it answered: a house fell <- the flood; help <- the burnt house),
  `cause` (a deed: a household's leaving <- its field drowned), `event`/`ev` (the event behind it: a strike's
  bolt, the flood's river tag, the cull).
- **Planet blame tags** (`W.C.<field>[cell]`): the event that dominated a field's latest change at a cell -
  soil water, river flow, rain, air temperature, heat anomaly, fire. The god's rain tags the soil and the
  rivers it fills; a flood deed carries the flow's tag.
- **Households' decisions** with a cause (`deeds.top_harm`): leaving, founding.

## 3. The index: `sim/chain.lua` (an observer)
Pure Lua, deterministic, **never writes W.S** (its runtime is `W.chainrt`, rebuilt from the records after a
load, like the news). Incremental by cursors:
- **Roots**: each new event with actor `player` and no parent is an intervention (kind, cell, village, point,
  time).
- **Records**: each new event whose root is an intervention, and each new deed whose chain (answers -> cause
  -> event -> the event's root) reaches one, becomes a node; its parent is the node of the record it links to.
- **Live state joined by a link** (once a game hour, per intervention still young): a village whose soil,
  river or air carries the intervention's tag (the planet's own blame) - "the soil at X watered: 0.10 ->
  0.22", "the river at X at 3.1x its mean", "the air at X 6 degrees colder"; the growing fields of a village
  whose soil carries it (the crops' yield is made of that water: `settle.grow_plots`) - "the corn of X grew on
  it (4 days)"; households leaving or founding whose cause is a node; people at the edge of hunger in a
  household whose heaviest harm (`deeds.top_harm`, the sim's own reading) is a node.
- **Incidents, not records**: nodes are grouped the way the news groups stories - all the burnt and doused
  deeds of one fire at one village are one "Fire at X" with counts; deaths per person; a flood per flood deed;
  beasts lost per village and kind; state effects per village and field. A record never counts twice.
- **Counting**: direct consequences (incidents whose parent is the cast) and downstream ones (all below);
  "still unfolding" while an incident is active (a fire burning, water standing, a household on the road) or
  a node was added in the last hours.

## 4. Significance
Shown: deaths and the badly hurt, buildings burnt or fallen, fire and flood on a village, fields drowned or
burnt, beasts killed, the soil/river/air changes at a village, crops grown on the god's water, hunger at
the edge, relief and kin help, households lodged, leaving and founding, takings and blows answering a harm,
quakes, eruptions and ash, wild animals culled or brought. Not shown: who saw or heard of it (knowledge),
memories, journeys - they are not consequences, only their carrying.

## 5. The player's view
- **Your actions** (a button in the HUD with the count): the casts, newest first - icon, power, place, day,
  "N consequences" (direct / downstream), "still unfolding".
- A cast opens its **chain**: the root line ("YOUR LIGHTNING at Tekheim, day 4 - go there") and the tree of
  incidents, indented, each line a reading ("Fire at Tekheim: 2 houses, 1 field", "Neya Tustrand died",
  "The Tustrands lodged with kin at Zurug"), each clickable: the camera flies there and selects what it is
  about.
- **News cards** whose chain ends at YOU carry "YOU · chain N": a click opens that cast's chain.

## 6. Acceptance (tests by premises, never by outcome)
Cast one power, live days, read the chain:
1. Lightning on a dry village: the strike, the fire, what burnt, who was hurt or lodged.
2. Rain on a drought: soil watered at the villages under it, fields growing on it - and no flood.
3. Rain on a wet floodplain: the river over its banks, flood, fields drowned, households moved.
4. Cull the wolves: the culled, then what followed (fewer beasts lost to wolves is an absence - shown only as
   the culled; the deer that grew is a state effect when the planet's fauna carries the cull's tag).
Every node's text names a real record; `test_v3_chain` checks the kinds differ between 2 and 3 and that every
node resolves to a record or a tagged state.
