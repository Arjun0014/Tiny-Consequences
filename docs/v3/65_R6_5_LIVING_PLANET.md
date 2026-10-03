# V3-65 — R6.5 Living Planet & Emergent Response (session 20, D-148)

A phase between R6 (conflict) and R7 (player and causal chains). Not polish: it exists so that a
god power in R7 touches **the world's state** and the existing world answers, instead of each power
carrying its own scripted consequences.

Shared primitives: `50_PRIMITIVES.md`. Audit of what is live: `41_PLANET_AUDIT.md`. Research:
`40_RESEARCH_CONFLICT_PLANET.md` §B.

## 1. The central question
*What small set of inherent properties lets the existing world react naturally to many different
environmental shocks?* Answer (§50): dangers as physical things at places; exposure by being there;
noticing by the senses; an urgent choice that puts dependants first; injury; the condition and
contents of things; deeds remembered and carried as news; planet fields read on the maps. The rest
is the economy, the family, travel and the ecology already built.

## 2. Design principle
A god power changes physical state (a stroke of heat at a point, rain withheld, a cell's
temperature, a herd culled, a mountain's pressure released). The world then answers: who is
exposed, who notices, who is endangered, who helps whom, what is destroyed, what becomes scarce,
what routes close, what animals move, what work stops, who inherits, who rebuilds, who trades, who
leaves, who remembers, who blames whom. Each of these questions already has a system or gets one
primitive - never a per-power script.

## 3. Emergence, shown on paper (not scripts to implement)
Each chain below uses only primitives and systems that exist or are planned in §50. Different
inputs (where, when, weather, families, stores, landscape) change the result.

**A. Lightning -> a carer dies -> kin take the child -> a late harvest -> a gift of grain.**
A storm day (planet precip; daily weather) puts a strike near the tallest exposed thing within its
reach of a point (a man harvesting in the open). Exposure: he is there; injury (`hurt` from the
strike) above the lethal line -> death with a deed (`death`, by nature, the storm's event). Life:
his widow remembers; his kin remember (R6d); his child is in the household of a lone mother with a
babe -> `kin.minder` already lets her leave the babe to work, or her kin take the child in
(`life.take_in` if no grown-up remains). Labour: the household's fields are its own (`plots` by
household) and harvest is a candidate for its own people first: the harvest lags; ripe crops
spoil left standing (`settle.grow_plots`): a thin store -> need (`migrate.push`) -> kin elsewhere
send food (`ask_kin`, D-125) or relief from a trusted neighbour (D-147). *Change one input* - the
strike in the woods, not the field; a rainy morning that kept him home; a grown son in the
household - and none of it follows.

**B. Lightning -> a dry wood burns -> deer flee -> wolves follow -> sheep lost -> wool dear.**
A strike on a tree in a dry week (soil water low, days without rain) ignites it (R6.5b local fire:
fuel, dryness, wind); fire spreads tree to tree; the planet cell's `F.fire` is written; the W4
herds already avoid fire cells and range to others (`fauna.lua`), so the deer band leaves this map
and turns up on a neighbour's map (`wild.lua` presence follows the herd's range); packs follow prey
(`fauna` packs by prey density) -> a pack hungry with wild prey scarce takes penned beasts (`wild
pick_prey`) -> a household loses ewes -> less wool at shearing -> its cloth want rises -> wool
worth rises (`econ.worth`) -> traders carry wool where it is dear. *Change one input*: a wet week
(no spread), dogs at the fold (the pack turns), no deer on that map (nothing moves).

**C. Eruption -> aerosol -> a cold growing season far away -> grain fails -> trade -> migration.**
A volcano's pressure releases (R6.5f geology on static plates): local destruction near the vent;
`G.aerosol` rises (already in the temperature formula for every cell); far villages' `F.temp`
falls -> crops grow slower (`grow_plots` reads `F.temp`) and pasture less (`daily_pasture`) ->
thinner harvests and hay -> culls before winter (`plan_cull`), need -> worths move, relief and
trade by want (D-138/D-143/D-147), households pushed below their patience leave (`migrate`). **This
chain is already live from `G.aerosol` onward** (audit §3); only the source is missing.

**D. Flood -> bridge lost -> the salt road is long -> salt late -> meat spoils -> a household
leaves.** High flow re-lays the map's river (D-106): fords close, the bridge's condition falls and
it is swept away (already live). The road to the salt village now fords elsewhere or detours
(`trade.land_route`/A* costs): the trip's cost rises; the trader's weighing (`best_trade`) puts it
off; without salt, meat is not cured (`saltpan`, D-133) and spoils; the larder thins -> need ->
the household's push rises over days -> it leaves. *Change one input*: a stone bridge pier (R5 gap),
a second road, smoking instead of salt.

**E. Earthquake -> houses cracked -> families lodge with kin -> timber wanted -> woods cut back ->
grazing shifts.** A boundary cell's stress releases (R6.5f): shaking at each map tile by distance;
each building's condition falls by its construction (log walls on stone footings hold, rubble
fails); a ruined house leaves its household without a home -> lodging with kin (existing:
households lodge when moving, D-119) or the house planned anew (settle house sites); logs are
wanted -> felling (trees disappear, `F.forest` written back) -> open ground grows (`pasture.open`
from tree cover) -> the herds' grazing blocks move. *Change one input*: timber framing, a quake at
night (everyone inside: more hurt) vs midday (in the fields), no kin to lodge with (migration).

## 4. What the planet must still learn (from the audit)
| Need | Why | Shape |
|---|---|---|
| A place on the map where a planet hazard acts | lightning, fire, ash, shaking, flood water have no local footprint | each hazard system writes its own local state (burning tiles/objects, water tiles, ash depth, a strike point) |
| Fire on maps | trees, fields, thatch, stores burn; animals and people flee; planet `F.fire` and map fire agree | burning objects with intensity, spread by fuel x dryness x wind, put out by rain or by people with water |
| Snow on maps | the dzud and winter chains (B4) | snow depth from the planet's snow cover and snowfall; read by walking, forage, hay, roofs |
| Volcanoes in v3 worlds | `W.ent.volcanoes` is never generated in v3 | generate sites from static boundaries (existing worldgen code), pressure, eruption |
| Earthquakes | static plates are data only | stress on boundary cells (rate by type and strength), release as shaking |
| Coastline rebuild from sea level | spec-only | rare, heavy (geo/map rebuild); only after the rest |
| v3 power paths | lightning and meteor reach only v2 people | powers write state; exposure finds v3 people |

## 5. R6.5 sequence (after R6 has a complete loop and long-run checks)
| Stage | What | Notes |
|---|---|---|
| **R6.5a** | Shared foundations, if not already built in R6: danger cues and noticing, urgent choice (gather/flee/shelter/confront/rescue/save), injury, deeds for nature's harms, building damage and ruins, bereavement for all kin | most come from R6a-R6d |
| **R6.5b** | **Lightning on a village** (the first environmental vertical): strikes on storm days at points (tall/exposed things favoured), a person/tree/house hit; a tree or thatch may catch; local fire (one burning object spreading to neighbours by dryness and wind; rain puts it out; people carry water); people notice (flash, thunder, fire, shouting), gather children, fight the fire, rescue; beasts flee pens that burn; deeds; aftermath by existing systems | run with different weather, stores and family layouts; same code, different outcomes |
| **R6.5c** | **Wildfire**: planet fire entering a map from its edge (the planet cell burns) and local fire spreading out to the planet; animals flee (wild threat by fire), herders move flocks, fields and stacks burn; succession already planetary | |
| **R6.5d** | **Snow and cold on maps** (dzud chain): snow depth, walking, forage under snow, hay, roof load, cold exposure | needs a premise at high latitude or a chill |
| **R6.5e** | **Flood water** beyond the river channel on low tiles: fields and low houses wet, people move up, beasts driven up, bridges (live) | |
| **R6.5f** | **Geology**: volcano sites and pressure in v3 worlds, eruptions (vent damage, ash downwind on maps and fields, `G.aerosol`), boundary stress and earthquakes (shaking -> building condition by construction -> injury -> rescue), landslides on steep tiles closing ways; tsunami on coastal maps last | the aerosol chain already reaches crops |
| **R6.5g** | **Climate at the extremes in the v3 calendar**: sea-level coastline rebuild (rare), SST oscillation with fish (ENSO-like, B8), ice age / hothouse runs watched for v3 villages (growing season, hay, fuel, ranges, migration) | long headless runs |

## 6. The lightning vertical in detail (R6.5b)
- **Where and when**: the planet's daily weather gives storm days per cell (`weather_day` storm);
  a strike per storm-hour on a map with probability from storminess; the point favours tall things
  (a standing tree's height, a person in an open field, a house roof) within a short radius of a
  random point (`rng` by cell, day, hour). Powers in R7 choose the point instead.
- **Hit**: a person -> injury (burn, often lethal) and those within a few metres hurt less; a tree ->
  splits, may ignite (dryness); a house -> thatch may catch (dryness, rain falling lowers it); a
  beast -> killed.
- **Noticed**: flash and thunder (everyone on the map hears thunder; the strike nearby is a sight);
  a fire is a sight and a smell; shouting is a sound.
- **Answered** (urgent choice): parents gather children; the able near a burning house carry water
  from the well/river (existing fetch-water, now to the fire), pull people out (rescue: kin and the
  near), lead beasts from a burning byre (save); others keep away; the hurt are carried home and
  tended (injury care).
- **After**: a ruined house -> the household lodges with kin or neighbours and a house is planned
  (existing); the stores burnt -> need -> relief, kin, trade, migration (existing); the dead -> kin
  remember, orphans taken in, heirs, mentors (existing + R6d); deeds of rescue -> gratitude
  (affinity).
- **Tests**: a premise places a storm over a village at harvest with different weather (dry/wet),
  stores (full/empty), families (lone parent/large household); the same code must give a scare, a
  house fire with no deaths, a death and orphan care, or a burnt granary and a hungry winter.

## 7. Performance and determinism
- Hazards cost only while they burn/flow/shake: local fire is a sparse list of burning objects,
  updated as events in the agents' queue (like animals) or hourly while any burns.
- Snow depth and ash are per-map arrays updated daily (like `riv`), not per tick.
- Every random choice through `rng` keyed by (cell, day, hour, object); no `pairs()` order; `dmath`
  for any transcendental; kept text via `sfmt`; browser = LuaJIT checked after each stage.

## 8. Acceptance (R6.5 as a whole)
When R7 adds a god power, the power's code is a few lines writing physical state; everything that
follows is answered by systems that would answer the same state arising naturally. If a power needs
an outcome script, the architecture is wrong.

## 9. Status (session 20)
- **R6.5a foundations**: built with R6 - deeds (`deeds.lua`), danger noticed (`danger.lua`), the
  urgent choice (fire, thieves; wolves had their own), injury (`people.hurt`, care, healing, death),
  sudden-death aftermath (death deeds, kin mourning on learning, burials), building condition (burnt
  houses as ruins; put-out fires as damage), lodging (`migrate.lodge`).
- **R6.5b lightning on a village**: built (D-154, `sim/agents/blaze.lua`, `test_v3_lightning`).
- Next: R6.5c wildfire between planet and map; then d snow, e flood water, f geology, g climate.
