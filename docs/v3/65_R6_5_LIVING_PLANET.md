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
- **R6.5c wildfire**: built (D-155, `blaze.planet`: a planet fire on a map's cell lights the trees at the
  map's edge; a local fire that takes hold writes the planet's fire on its cell; animals flee fire).
- **R6.5f geology**: earthquakes (D-156, `sim/agents/shake.lua`: stress on convergent and transform
  boundary cells, quakes felt by distance, buildings damaged by construction and condition, those
  inside hurt or trapped, dug out by neighbours, the homeless lodged) and volcanoes (D-157,
  `sim/agents/volcano.lua`: sites made lazily in v3 worlds, pressure, VEI eruptions - aerosol cooling
  every cell, fire at the vent, shaking, ash downwind on the villages: roofs, grass, crops). Tests
  `test_v3_quake` x3; browser = LuaJIT on `--premise quake` and `--premise eruption`.
- **R6.5d snow and frost on the maps**: built (D-158, `sim/agents/snow.lua`, §10; `test_v3_snow` x4).
- **Beasts and fire** (R6.5b gap): beasts cry out and run, are let out of a burning pen by those who
  hear them or burn in it; a lost pen is planned again (D-159).
- **R6.5e flood water**: built in session 21 (D-169, §12): infiltration excess on the planet (a
  day's rain beyond what the ground takes in runs off that day), flood water on the maps by height
  above the nearest river (HAND), the people's answer through the shared primitives.
- **Fields, stooks and the hay stack burn** (D-161); **tsunami** (D-162, §11: run-up from the sea,
  the sea drawing back seen and shouted, running inland, the shore taken); lost village buildings
  planned again (D-162).
- **R6.5g, first watch** (D-164): a 96-day hard winter (`--premise hard_winter`) - snow everywhere, frost
  on the flax, harvests thinned (stores halved), flocks thinned by the dzud; a toddler's death found and
  fixed (fed from the carer's pack; no child handed to one setting out); then no deaths from hunger.
- **Aftershocks** (D-165) and **the badly hurt helped home** (D-166: lying where they fell, lifted and
  walked home at a hobble by kin or whoever sees them).
- Not yet: landslides (map slope exists only per planet cell); g at
  longer range (ice age, hothouse, sea-level coastline, ENSO); ice crust, moving herds to open ground;
  people afraid to sleep under a roof after a quake. (Leaf fodder - boughs lopped when the hay is gone -
  built: D-167.)

## 10. R6.5d snow and cold on the maps (design, session 20)
The audit found no snow at any village (all lie between 37 S and 14 N: Krikeivorn's winter days
~3 C, nights a few degrees lower). Snow therefore comes only from a real cold: a cold air mass
(the planet's `heat_anom` on a cell - what R7's chill power will write) or a cold year (sulphur in
the sky after a great eruption, `G.aerosol`). Both are physical states the planet already has.
- **State**: `s.snow`, the depth of snow lying on the village's map (metres). Hourly, from the day's
  weather the people already live (`weather.lua` spells, `temp_at`): while a spell falls and the
  hour's air is below `snow_t` it falls as snow (`snow_fall` a spell-hour, twice in a downpour);
  above 0 C it melts `snow_melt` per degree-hour (rain on snow twice as fast). `weather.snowing`.
  Nothing else is stored; nothing reads a "winter" flag.
- **Read by** (each its own small rule):
  - walking - people's and beasts' pace x (1 - min(`snow_walk_max`, `snow_walk` x depth));
  - grazing - the grass in reach x max(0, 1 - depth / `snow_graze`) (goats paw deeper); the beasts'
    hunger rises out on the snow;
  - the herders - beasts are not driven out to grass under snow; hay goes into the racks when the
    penned beasts are hungry and the grass is buried;
  - the hay reckoning - while snow lies, a household whose stack will not feed its beasts for
    `snow_hay_days` kills the most dispensable for the larder while they still have flesh (the
    autumn cull's rule, run on the day the shortage is seen); beasts that are not killed and not fed
    starve (existing hunger and health) - the dzud;
  - roofs - snow deeper than `snow_roof` loads them as ash does (one roof-load rule shared with
    volcano ash: cracked, or fallen in on whoever is inside);
  - frost - a night below `frost_t` with bare ground (snow insulates) blackens part of a growing
    spring crop or flax (a winter crop only below `frost_t_winter`): its yield falls;
  - cold - the existing `s.cold` (hearth wood, cold meals, rags and bare feet cost health); bare feet
    in snow cost more.
- **View**: the map's ground and trees whiten with depth (the cell texture's snow channel takes the
  larger of the planet's cover and the map's snow), flakes instead of rain.
- **Premises** (state, never outcomes): `named("cold_snap")` - a cold air mass on a village's cell
  and its neighbours (`heat_anom`), `named("hard_winter")` - sulphur in the sky (`G.aerosol`).
- **Tests**: snow falls and lies in a cold snap on a wet day; people walk slower in it; beasts are
  kept in and fed hay; a long hard winter: the hay runs short, beasts are killed for the larder,
  those left unfed starve; a frost night costs a young crop its yield, not under snow.

## 11. Tsunami (R6.5f, last; design, session 20)
- **Source**: a strong quake on a boundary cell under the sea (`shake.quake` at an ocean cell, magnitude
  from `tsu_mag`) or a large eruption of an ocean volcano (VEI from `tsu_vei`); R7's meteor into the sea
  later. Nothing else starts one.
- **The wave at a village**: each coastal village (a map with sea tiles) within reach gets a run-up by
  distance (`tsu_k` x (mag - tsu_mag + 1) / (1 + steps^2)), arriving after a delay by distance (the wave
  crosses the sea: `tsu_speed` cell steps an hour). On the map the inundation is the land within
  run-up of the sea (a distance-to-sea per tile, from `s.land`, worked out once per map) - the map has
  no heights, so the low ground is the ground near the sea, as the floodplain is the ground near the river.
- **Noticed**: the shaking (if felt) and, minutes before the wave, the sea drawing back from the shore
  - a sight cue at the shore for those who can see the water; those who notice run inland (the urgent
  choice: away from the sea, the babe snatched up, as from fire).
- **What it does**: buildings in the run-up zone fall in (`shake.fall_in`, cause "the sea") - those inside
  hurt or trapped; people still in the zone are swept (badly hurt, the worst drowned: `people.hurt`
  "drowned"); boats at the dock and moored are lost (`boats` gone, the catch landed with them); the
  fields in the zone are salted (fertility down by `tsu_salt`); the saltpan and the dock go. Deeds and
  memories; the homeless lodge, the lost boats are built again - all existing.
- **Premise**: `named("tsunami")` - a magnitude 8 quake on the ocean cell nearest a coastal village.
- **Test**: by day most see the sea draw back and run; by night more are swept; the dock and boats lost.

## 12. R6.5e flood water (session 21, D-169)
**Hydrology first.** The planet's soil filled to field capacity before any rain ran off, and a v3 planet
step is one day: a season's deluge over Krikeivorn raised its river 1.2x. Now, where a step is a day
(`W.hpt` 24), the ground takes in at most `inf_cap` x (1 - soil water) x (`inf_bare` + (1 - `inf_bare`)
x cover) of a day's rain (cover = woods + 0.6 grass; frozen ground x `inf_frozen`): the rest runs off
that day (infiltration excess, Horton). A wet spell's first day sends the river to 5x its mean; it
recedes over days. Woods cut upstream make the river flashier; snowmelt on frozen ground floods. A
weekly step (v2) spreads its rain over seven days, below any soil's capacity: v2 unchanged. Untouched
four planet years: mean flows at the villages and soil water as before (within 1-4%).

**Water on the map** (`sim/agents/flood.lua`). The map has no heights, so each tile's height above its
nearest river (HAND - height above nearest drainage, the method of inundation mapping) is its distance
beyond the channel's usual bank x the valley's cross gradient (`fl_g` x (0.25 + the planet cell's slope):
a flat plain floods wide, a steep valley little) + a gentle unevenness (`fl_rough` on a lattice of
`fl_cell` tiles: hollows and low rises). A river whose flow passes the planet's own flood line
(`HYDRO.flood_mult` x bankfull, the mean annual flood) stands `fl_k` x min(excess, `fl_cap`)^0.6 m above
its bank; the stage rises `fl_rise` m an hour after the day's water reaches it and falls `fl_fall`. The
water spreads from the channel over tiles lower than the stage, only through tiles under water
(a rise behind a hollow keeps it dry). Saved: the stages and the channels/gradients at the flood's
start (`s.fl`); runtime: HAND and the depth per tile (`mapm.FLOOD[s]`).

**What lies in it** (each a small rule on existing state): the ways (map costs: waded to `fl_wade`,
closed deeper, swum when cut off); houses (water over the footprint: `b.wet`; the stores on the floor
spoil, a granary on its posts only deeper; walls soften; a hut or a weak building falls in -
`shake.fall_in` "flood"); fields (a share under water drowns a growing crop after `fl_crop_h` hours,
rots ripe corn and sheaves; silt when it goes down); beasts (in water above their own depth they lose
health by the hour; shut in a flooded pen they cry out, loose they wade out to ground above the
water); people (in deep water swept: hurt "drowned").

**The people's answer** (D-148 primitives, nothing per flood): the water is noticed - felt in the house
even asleep, seen at one's feet or the river over its banks near those awake out of doors, shouted;
the urgent choice (`think.flood_choice`): out of the water and up to ground `fl_safe` m above it with
the babe in arms; the able open a flooded pen with their beasts shut in (`loose_flood`); a household
whose house has water in it or whose door is under water too deep to wade sleeps under a kin's roof,
else a neighbour's (`people.home` / `dry_roof`); the ways round the water take everyone else past it.
After: repairs, lodging, the drowned field's hunger, relief - all existing.

**Deeds and causes**: a `flood` deed (nature) with the event driving the river on the planet
(`W.C.flow`, the hydrology's blame tag: a god's deluge carried down the rivers), its peak and hours; a
`flooded_village` event; a house brought down answers the flood deed.

**Numbers** (shipped world): untouched 96 days - floods at Dreik Knoll (1, 27 tiles), Little Leelee (2,
up to 726 tiles) and Krikeivorn (2, up to 1,454 tiles), on the bottomland only. A five-day wet spell over
Little Leelee's hills (`--premise flood5`): the river over its banks at hour 4-9, 4,075 tiles under
water at the peak (2,660 too deep to walk), the byre in 0.95 m of water opened, a field drowned, 19
people saw it, the houses (built above the floodplain) dry; the water gone within five days, 9 fields
silted. `tools/v3_flood.lua` watches one hour by hour with a map.

## 13. R6.5g climate at the extremes, watched (session 21, D-170)
The global climate integrates once a planet year (48 v3 days) and relaxes over ~10 of them (tau_years):
a natural ice age or hothouse takes thousands of v3 days to arrive. The premises therefore set the state
of another era - `named("ice_age")` (global -6 C, CO2 190 ppm) and `named("hothouse")` (+6 C, 1,100 ppm) -
and the planet's own systems (ice, seas, rains, each village's seasons) carry it.
- **What the villages feel**: their daily means run 3-23 C now (they are temperate despite low
  latitudes); 9-29 C in the hothouse, colder by 6 C in the ice age (snow every winter at Dreik Knoll and
  Krikeivorn, some at Hottoodingle Hop; frost on the flax and spring grain).
- **Heat stress, new** (`heat_t` 24 C daily mean, `heat_span` 10): a hotter day spoils the filling grain in
  proportion (none at 34 C) and browns the grass; owners remember "The heat is withering X". Before it, a
  hothouse only lengthened the growing season (every granary fuller than untouched).
- **192 days each** (`tools/v3_chronicle.lua --premise ice_age|hothouse --days 192`): no deaths from hunger
  in either (illness 6, old age 3-4, as untouched); granaries at day 192 - untouched 696/212/163/159/404/282,
  hothouse 541/443/240/269/373/592 (the cool villages gain, Little Leelee's corn withers), ice age
  677/135/181/180/353/83 (Krikeivorn and Zurug thin); trade 336-458 exchanges.
- **Found by it**: a trader sailing home to a village whose dock was lost meanwhile crashed the planner
  (the dock assumed): boats now land, board and tie up at the shore where the dock stood
  (`settle.dock_point`).
- **Not done**: the coastline following sea level (sea level follows temperature over planet-centuries:
  never reached on v3 time scales without forcing; the coast rebuild is heavy - deferred), an SST
  oscillation with fish, migration for climate (none arose: no village was pushed hard enough).
