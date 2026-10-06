# TC2-17 — Roadmap

Each phase is a sequence of vertical slices: research -> design -> decision -> smallest real slice ->
compile -> test -> run -> inspect -> benchmark -> fix -> commit -> docs. A phase is done when its "done
when" holds in headless runs **and** in the Web build. Phases overlap where independent.

## F0 — Foundation (session 1)
Environment audit; legacy audit; research; TC2 docs; Unity 6.6 project; headless runner; batch workflows
(compile, tests, Web build, COOP/COEP server, Playwright); Core foundation: ids, time and calendar, keyed
RNG, DMath, event queue + scheduler, World, history ledger, causes, persistence, planet grid; **proof of
concept**: a generated planet, a few villages of persistent people, activities and journeys, a wildfire
that interrupts a traveller, normal / accelerated / headless catch-up, history with causes, save -> load ->
continue (equivalent), offline catch-up; a Unity view of it on the Web; benchmarks.
**Done when**: all of the above run and are tested; equivalence and cross-runtime hash tests pass; numbers
recorded.

## F1 — The living planet  (CLOSED 5 Oct 2026, session 2: D2-043; seeds 42 and 7, 10,000 years)
Plates and tectonics; relief, uplift, erosion (Braun-Willett), drainage, rivers and lakes; climate energy
balance with seasons, orbital cycles, CO₂, aerosols, ice-albedo, sea level and coastlines; weather and
storms; vegetation by plant functional types, succession, fire regime, soils; biome classification;
planet rendering v1 (cube-sphere patches, water, atmosphere, clouds, biome materials).
**Done when**: 10,000 untouched years show drift, ranges rising and wearing, a glacial cycle, coastline
change, forests moving; stable over seeds; beautiful enough from orbit to judge.

## F2 — Ecology and evolution (CLOSED 5 Oct 2026, D2-046)
Species as data (diet, size, generation, traits); abundance fields; dispersal; food webs (Holling II with
refuges); herds and packs as super-individual agents near people; trait evolution; speciation and
extinction as historical events; fish and plankton; visible animals elaborated from herds.
**Done when**: predator-prey cycles, recovery after catastrophes, adaptation measurable over centuries,
at least one speciation in a long isolated run.

## F3 — People and settlements (CLOSED 6 Oct 2026, session 2: D2-060)
Households, bodies and needs, care obligations, life stages, marriage, births, deaths, inheritance;
the work board; farming (fields, seasons, yields from climate and soil), foraging, hunting, herding
(livestock individuals), fishing, wood; construction of buildings by stage from materials and labour;
settlement layouts that grow (lots, roads); elaboration of movement in the Web view; the inspector
(who, what, why).
**Done when**: a village lives 100 years untouched without scripted help, every visible action is the
simulation, and following any person shows a believable life.

## F4 — Knowledge and technology (CLOSED 6 Oct 2026, session 2: D2-065; slices D2-062 to D2-064)
Techniques as data with prerequisites; proficiency carried by people; teaching, apprenticeship,
observation; discovery as combination in the adjacent possible; loss with dying teachers and small
populations (Henrich); tools and crafts; material chains (stone, clay, fibre, metal) from geology.
**Done when**: a technique spreads by contact and is lost in an isolated shrinking group in tests.

## F5 — Exchange, movement and disease (CLOSED 6 Oct 2026, session 2: D2-068; slices D2-066, D2-067; migration D2-045)
Roads and boats; trade by want; gatherings; migration and founding; pathogens with SEIR in settlements and
parties, transmission along routes and at gatherings, mutation; famine coping sequences.
**Done when**: an epidemic jumps along trade and travel and is traceable; migrations found new places.

## F6 — Society, conflict and polities (CLOSED 6 Oct 2026, session 2: D2-076; slices D2-069, D2-071 to D2-075)
Relationships, deeds known per settlement, reputation, leadership from prestige; raids as parties of real
people; defence that is built (ditch -> palisade -> wall -> towers -> gatehouse -> keep from a modular kit,
from remembered harm, labour, materials, knowledge, coordination); alliances, tribute and polities
(Carneiro, Wright & Johnson, Turchin); collapse, ruins and reuse.
**Done when**: a fortified town or a polity can arise and fall in long runs, and its history explains it.

## F7 — History Book, news and the four questions
World News, Why?, Your Actions, the History Book (eras after the fact, biographies, settlement and polity
histories, timelines, the catch-up feed "While you were away").
**Done when**: a player can read 500 years and every claim links to records.

## F8 — Deep time
COARSE household-season integration calibrated against ACTIVITY; Ancient and Deep time offline modes;
planet-only eras after human extinction; recovery; new life.
**Done when**: 5,000 years catch up in minutes with distributions matching ACTIVITY.

## F9 — The god
Powers that write physical state (lightning, rain, drought, warmth, cull, bring, plague, blight, meteor,
stir a mountain, strain a fault); chains rooted at YOU.

## F10 — Art and presentation
Art direction; modular building kits; characters and animals with vertex-animation instancing; vegetation;
weather and fire effects; audio; UI polish.

## F11 — Platform
Wavedash cloud saves and init; itch packaging (threads on); phones; settings.
