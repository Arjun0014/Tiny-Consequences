# W12 — People, households and villages

Supersedes 02 §5.10. Research basis: `00_RESEARCH.md` §1, §4, §5, §8. A village is not a
population number: it is its households, their homes, their fields, boats and stocks, and what its
people do each day.

## 1. Person

```
id, key                 key = hash(village founding cell, birth hour, index) — all rolls keyed by it
name, surname, sex, born (hour), village, household
occupation              "farmer" | "fisher" | "woodcutter" | "builder" | "child" | "elder"
task, site              today's assignment (code + field cell / boat id / building id / forest cell)
nutrition (0–1)         EMA of ration adequacy (weekly)
health (0–100)          falls with hunger, cold, crowding; recovers when fed and warm
partner, betrothed      person ids (0 = none); mother, father; children (count)
preg_due (hour|0)       pregnancy; nursing_until (hour|0) after a live birth
away                    party / ship id while travelling
traits, mem, mood       as before (dialogue, memories of events)
u                       fixed random fraction (Weyl offsets for cheap per-person rolls)
dead, died_of, death_tag
```

### 1.1 What a person does (the day)
Everyone's position at any hour is a pure function (`sim/society/schedule.lua`, used by the view)
of: occupation + today's task/site, home, the village's local sunrise/sunset, today's weather,
age and state.

| Local time | Adult with a task | Child | Elder | Sick / late pregnant |
|---|---|---|---|---|
| wake (sunrise − 0.5 h, clamped 04:00–08:00) | wake, eat at home | wake | wake | rest at home |
| work window (wake + 1 h → sunset, ≤ 10 h, meal break) | walk to the site, work, walk back | play near home / square; ≥ 10 help at harvest | light work at home, sit by the well | at home |
| evening (sunset → sleep) | eat at home, visit neighbours / gather at the well or hall | home | home | home |
| sleep (sunset + 3.3 h, clamped 19:00–23:30) → wake | asleep **in their household's home** | asleep | asleep | asleep |

Storm or heavy rain: outdoor work is cancelled or shortened — people stay home. Fishing crews
follow their boat's trip plan (depart, fish, return). Travellers follow their party or ship.
Leisure (no task because the village's needs are met) = visiting, sitting at the well, walking.

### 1.2 Needs and body
- **Food**: ration × need (child 0.6, adult 1.0, elder 0.8 FU/day). Nutrition moves weekly toward
  the ration (rate 0.35). Health: −(0.85 − nutrition) × 30 per week below 0.85, +4 per week when fed.
- **Warmth**: households without fuel on days below 5 °C lose health (5 − T) × 0.8 per week
  (× 2 for infants and elders).
- **Shelter**: a household without a home, or over its house's capacity, has a crowding factor
  (disease hazard × 1.3).
- **Work fitness** = (0.35 + 0.65 × nutrition) × min(1, health / 40).

### 1.3 Mortality (weekly hazard = annual / 48)
Annual base: < 1 y 0.12; 1–4 y 0.025; 5–14 y 0.005; 15–44 y 0.006; ≥ 45: 0.006 + 0.0012 ×
exp((age − 45) / 8). Plus frailty: health < 40 adds (40 − health) × 0.0006 per week (× 2 for the
young and old). Plus exposure events (fire in the village, flood, meteor, drowning when a boat
sinks). The death's cause is the largest hazard term; its tag is that term's tag (hunger → food
stock tag, cold → wood stock tag, exposure → the event). Rates are gentler than Model West level 3
so that tiny villages are not erased by noise; famine and cold still kill the weak first.

### 1.4 Births
Fecund women (16–44) who are married *and live in their own home*: daily conception probability
0.006 × age factor (1.0 to 30, 0.85 at 35, 0.5 at 40, 0.2 at 44) × nutrition factor (1 at ≥ 0.85
→ 0 at 0.55), not pregnant, not nursing. Evaluated weekly (7-day probability). 15% of pregnancies
are lost (no birth, no nursing). Pregnancy 252 days (9 months). A live birth creates a **child
entity** in the mother's household (surname, parents, key); maternal death 0.8%; nursing lasts a
year unless the infant dies. Famine therefore cuts conceptions at once (research: the prompt,
universal response) and births nine months later.

## 2. Household

```
id, key, village, home (building id | 0), members (person ids), formed (hour)
```
- **Formation (neolocal, Hajnal)**: a betrothed couple marries when the village assigns them a home
  — a vacant house, or a house built for them (construction demand 2). They leave their parents'
  households and form a new one. Until then they are betrothed and live apart: **no births**.
- **Growth**: births; widowed elders move in with a child's household when they can no longer live
  alone; orphans go to kin (grandparents, aunts/uncles) or the household with most room.
- **Dissolution**: last member dies or leaves → the home is vacant (it decays unless reused).
- **Overcrowding**: members > capacity (7 per house, 5 per hut) → crowding → construction demand 3.

## 3. Social processes

### 3.1 Occupations
At 14 a child takes the occupation the village is shortest of (fisher if boats lack crews, else
farmer, with a few woodcutters/builders as the village grows). Occupations persist (the same person
works the same boat or field), which makes people recognisable; the weekly allocator borrows them for
urgent tasks (everyone harvests), and a role chronically over/under-staffed re-trains one person a
month. At 60 an adult becomes an elder.

### 3.2 Courtship and marriage (monthly per village)
Singles: women ≥ 17, men ≥ 19, unpartnered (widows/widowers up to 45). Each month a single may
court a single of the other sex in their village or in a linked village (kin village, merchant
port on the same circuit, or the nearest on the landmass) with probability 0.15. The bride moves to
whichever village can house them sooner (vacant house / lots and wood). Betrothal waits for a home.

### 3.3 Migration (monthly per household)
- **Push** (sum): months of ration < 0.8 in the last year (0.15 each, cap 6), betrothed members
  waiting > 24 months for a home (0.4), homeless for > 3 months (0.4), cold months without fuel
  (0.1 each), fewer than 6 people left in the village (1.0), a disaster this year (0.3).
- **Pull** of a destination: its food security in months (cap 12) / 12 + housing (vacant house or
  free lot with wood in stock: 0.5) + kin living there (0.3) − travel days × 0.05. Destinations:
  villages on the same landmass within walking reach, or ports on a merchant circuit.
- Leave when push ≥ 1 and the best pull beats home's pull by 0.3 (monthly chance 0.3 per eligible
  household). Parties carry 14 days of their share of food. The parent tag of the `migration` event
  is the tag of the dominant push term.
- **Movement**: walk as a travel party along land cells (≈ 0.5 h per cell, daylight only, camping at
  night), or wait at the port for a merchant ship calling at the destination and board it. They are
  real entities in transit: if their ship sinks, they drown.

### 3.4 Founding (daughter villages and the frontier)
A group of ≥ 2 households (≥ 3 adults) founds a village when (a) a thriving village is crowded
(lots full or fields at the walking limit, betrothed waiting) — young couples prefer new land over
waiting; or (b) migrants find no destination that pulls. Site: best free site that can feed ≥ 20
(existing `pick_site`) within reach; departure in early spring by the site's calendar; they carry
60 days of food, seed for 2 cells and tools. On arrival: `village_founded`; huts first, fields
cleared, a pier if coastal, houses later. Abandoned village sites carry a memory penalty.

## 4. Village

### 4.1 State
```
id, key_cell, key_hour, name, cell, culture, continent, founded, from, alive
households, buildings, boats, fields (farm cells), members (derived list of person ids), pop
stocks: grain, fresh, wood, seed (+ one cause tag each)
ration, food_days, wood_days, state ("new"|"thriving"|"rationing"|"famine"|"declining"|"abandoned")
cause (the tag of its binding shortage), plan (assignments, demands, construction queue, field plan)
history: harvests (5 y), catch/import/production EMAs, births/deaths counters, import_tag, credit
lots (occupied lot ids), land cells (centre + expansions)
```

### 4.2 Rhythm
- **Local midnight (day step)**: settle the day that ended — production, consumption, spoilage,
  construction progress, births due — update stock tags, detect thresholds, then plan the new day
  (weather, daylight, group hours, boat trips).
- **Weekly** (weekday = id mod 7): labour assignments, construction queue, field plan, trade intent,
  per-person nutrition/health/mortality/conception, memories.
- **Monthly** (week = id mod 4): courtship/marriage/households, migration, occupations, founding.

### 4.3 Layout (physical settlement)
Pure function `sim/society/layout.lua` shared by sim and view. Lot 0 is the square (well); lots
sit on rings around the village centre in the cell's tangent plane (ring k at radius k × 0.13 world
units, 6k lots, per-village rotation and jitter from its key). Houses take the innermost free lots,
granary and woodshed near the square, the pier on the coast lot nearest the fishing water. When the
three rings of a cell fill, the village claims its best neighbouring land cell as village land and
continues there. Buildings keep their lot for life; ruins stay on their lots until they decay.

### 4.4 Founding and abandonment
- **Founded** by settle (worldgen), colonists, migrants or the Settlers power.
- **Declining**: population falling for 2 years, empty houses, fields unworked.
- **Abandoned** when nobody is left: `village_abandoned` (parent = last famine/disaster/shortage
  tag). Houses become ruins (decay over 20–40 years), fields return to the wild (succession through
  grass, shrubs and young forest over decades), boats rot at the pier, the lots may be resettled.

## 5. Why a village grows or declines (emergent, no thresholds on population)

Growth: fed and housed couples marry → births → children grow → more households → houses (wood,
labour) → more fields (walking farther, clearing forest) and more fuel (forest thinning) → until
labour, land within reach, wood or food bind. Then marriage waits (no house), rations fall (food),
or people go cold (wood): births slow, some migrate or found new villages. Decline: a supply stops
(harvest, fish, a merchant ship), stocks fall, rationing, seed eaten, hunger lowers work, fewer
fields sown, deaths and emigration remove workers, even fewer fields are worked — until the village
stabilises at a smaller size or empties.
