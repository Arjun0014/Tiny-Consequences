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

## 6. Phase
F3 (layout, construction, economy basics), F5 (exchange, migration, founding). F0: settlements as a few
place nodes with granary and woodstore, fields by season, and trips between neighbours.
