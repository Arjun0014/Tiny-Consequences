# W17 — Roadmap v2: build the living world first

Supersedes the order of `08_ROADMAP.md` from Stage 2.3 on (D-050). The jams (Defold Game Jam
2026, Game Gauntlet Sim Jam; themes *Chain Reaction* + *Simulation*) are secondary: if building the
world properly misses a deadline, it misses it. Chapters of 08 after the world phases (chain popup,
chronicle, dialogue, saving UX, onboarding, audio, deep time, polish, release) remain the plan,
re-sequenced after W6.

Method for every phase: implement one complete vertical loop → run it headless → watch it (tools,
maps, traces, and the game) → profile → fix → expand. Every phase ends with tests green, PROGRESS
updated, a commit.

## Phases

### W0 — Reset (done 27 Sep)
Research (`00`), audit (`01`), design (`10`–`16`), this roadmap, D-050.

### W1 — Time
- `W.hour` master clock; `world.step` (hour) / `world.tick` (week = 168 steps) / `run_years`;
  commands and events on hours; year snap every 8,064 h; saves and replay on hours.
- `sim/society/daylight.lua` (declination, day length, sunrise/sunset, local solar time),
  `sim/society/weather_day.lua` (WGEN downscaling, storms).
- Speed ladder (hours per second), view time of day, sun direction from the sim, night lighting
  (basic), HUD clock with the hour.
- **Done when**: P01/P02/P03 green on the hour clock; unit tests for day length (equator ≈ 12 h,
  ±60° solstices, polar day/night), opposite hemispheres, daily weather conserving weekly totals;
  scenarios unchanged; game builds, W01/W02 pass, day/night visible in shots.

### W2 — Settlements (vertical loops 1–3)
- **W2a** entities and the wood → house → household loop: `sim/society/` (village, household,
  person, building, labour allocator, forestry/fuel, construction, housing-gated marriage, births,
  weekly health and mortality), settle at worldgen, retire `villages.lua`/`people.lua`.
- **W2b** food: fields with sowing/tending/harvest labour and seed; fishing boats with trip plans;
  rationing and coping; migration as walking parties; founding; abandonment and ruins.
- **W2c** port the rest: detectors (society events), powers (Lightning on people/boats, Blight,
  Settlers, Meteor), test hooks, tools (`sim:villages` trace), inspector; the view draws buildings,
  people from schedules, boats from trips.
- **Done when**: behaviour tests B01, B04–B10, B12, B13 pass; S01 (new society bands), S03, S11
  pass; P01–P03, P06 green; a 100-year run shows houses built from wood, clearings, births,
  deaths, migration, at least one founding; perf measured.

### W3 — Transport and trade
- Merchant ships (cargo, ports, circuits, passengers) replace tankers; import accounting and
  `import_tag`; `shipment_lost`; sea passage for migrants.
- **Done when**: B02, B03, B15 pass; S04 and S15 pass on the new ships (tutorial re-scouted);
  chains like "ship sunk → shipment lost → rationing → migration → labour shortage → crop failure"
  appear in runs.

### W4 — Ecology agents
- Herds and packs (`14_ECOLOGY.md`), fields derived from them, hunting from herds, spawning season.
- **Done when**: S02 and S07 pass on herds; baseline bands for animals hold; herds visible.

### W5 — The visible world
- Clouds rebuilt (`16` §5); day/night polish, lit windows, smoke ∝ fuel; construction stages; field
  states; stock props; stumps; succession props; herds; screenshot review against 06 §6 and ART.
- **Done when**: the shot set reviewed; the Watch speed shows a village day from dawn to night.

### W6 — Profile and resolution
- Browser profile of planet + society at the target speeds; per-system costs; decide on grid
  resolution / multi-rate / hierarchy changes with evidence; P07 re-baselined.

### W7 — Villages that know each other (`18_DEEP_WORLD.md` §2.2–2.3) — done (D-055, D-056)
- Relations graph (kin, reciprocity ledger, grievance, distance); famine relief carried by real
  parties; barter trade between complementary villages (porters overland, boats along coasts);
  imports in the field plan (specialisation); trails where people walk; spouses from friendly
  villages.
- **Done when**: a bad harvest in one village draws grain from a kin village in runs (chain
  `crop_failure > relief`), trade flows appear in the ledgers, parties with cargo are visible,
  seed-sweep populations are stabler (fewer famines at the land limit), tests green.

### W8 — Status, leaders and conflict (§2.1, §2.4) — first vertical done (D-057)
- Lineages, household wealth and prestige, leaders and succession; raids as parties with real
  casualties, loot and fire; grievance and feuds; palisades; defensive aggregation.
- Done: lineages, prestige, leaders with temper, contested succession and scalar-stress splits,
  grievances (raids, refused help, encroachment), raids with Lanchester battles, casualties, loot,
  burned houses and driven-off livestock, palisades. Still to do: household wealth (own stores),
  defensive aggregation in migration choices, allies joining a fight.

### W9 — Polities (§2.5) — first vertical done (D-058)
- Tribute after defeat or from daughter villages; paramount redistribution; the tribute graph's
  tiers named by the chronicle (chiefdom, complex chiefdom, state only with ≥ 4 tiers); territory.
- Done: submission after repeated defeats by a hemmed-in or outmatched village, yearly tribute
  carried by porters, lords protect (grievance for raids on tributaries), relieve first and never
  raid their tributaries, secession when the lord weakens; `conflict.polity/tiers`. Still to do:
  territory drawn, the chronicle's names, paramount redistribution beyond relief, daughter
  villages' gifts.

### W10 — Households' own work and animals (§2.6) — crafts and village herds done (D-056, D-058)
- Livestock herds (milk, meat, manure, wool, wealth), crafts in the slack season, wild herds and
  packs as agents (old W4).
- Done: crafts (tools/pots/cloth from local sources, make-or-buy, trade), village herds (sheep,
  goats, cattle: pasture, herders, spring births, milk, slaughter, manure, wool for cloth, wolves
  on unguarded pastures, raiders drive beasts off). Still to do: household ownership, wild herds
  and packs as agents.

### Then
Chain popup and "Why?" (old 2.3), chronicle (2.4), dialogue (2.5), saving and coming back (2.6),
onboarding and audio (2.7), playtest; deep time (Chapter 3, with an aggregate society mode for
epochs); ship (Chapter 4).

## Behaviour tests (new layer, `tests/sim/behaviour/`)

Deterministic tests on real worlds with targeted interventions; each checks a mechanism *and* its
causal trace, not only final numbers.

| id | Behaviour |
|---|---|
| B01 | A fishing boat leaves, reaches its ground, fishes, returns; the village's fresh stock rises by the catch and the ground's fish falls by the same amount |
| B02 | A merchant shipment changes the source's and destination's stocks by exactly the cargo moved |
| B03 | Sinking a loaded ship: the destination receives nothing; its stock falls below a control run; `tanker_sunk → shipment_lost →` chain present |
| B04 | A house site with no wood makes no progress; with wood it progresses and consumes exactly its timber |
| B05 | Population growth creates housing pressure: betrothed couples wait and house sites are queued |
| B06 | Woodcutting reduces the forest of the cells cut by exactly the wood gathered (in forest units) |
| B07 | Births create child entities with parents and a household; children become adults with occupations at 14 |
| B08 | Famine changes behaviour: rations fall, conceptions drop, mortality rises, households emigrate |
| B09 | Losing workers (migration) reduces the area sown and the harvest |
| B10 | Winter: crops don't grow below temperature; sowing waits for the local spring |
| B11 | Northern and southern seasons (temperature, day length, sowing dates) are opposite |
| B12 | Day/night: at local noon workers are at their sites; at local 02:00 everyone is at home; local time follows longitude |
| B13 | Villagers sleep in their own household's home |
| B14 | Abandoned farmland undergoes succession (grass, then forest) over decades |
| B15 | A trade disruption propagates into another system (rationing, emigration, fewer fields) with no scripted famine logic |

Scenario tests S01–S15 and properties P01–P10 continue; their mechanics are re-expressed on the new
systems as each phase lands. S01 gains society bands (population range, houses per household,
births/deaths per year, at least one house built per village per decade, forest cleared around
villages but not all).
