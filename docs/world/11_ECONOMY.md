# W11 — Resource & economic model

No money. The economy is **physical flows of food, wood and labour** between fields, forests,
seas, stores, people and places. Value appears only where it has to (trade priorities), measured in
*need-days* (how many days of someone's need a quantity covers). Research basis: `00_RESEARCH.md`
§1–3, §9–10. All constants live in `sim/config.lua` (`SOCIETY` groups); the numbers below are the
starting calibration.

## 1. Units and needs

| Unit | Meaning | Calibration |
|---|---|---|
| **FU** food unit | one adult-day of food (≈ 2,500 kcal ≈ 0.75 kg grain) | adult 1.0 FU/day, child (< 14) 0.6, elder (≥ 60) 0.8 |
| **WU** wood unit | one household-day of cooking fuel ≈ 1/4 of a cold day's fuel | cooking 0.25 WU/household/day + heating 0.045 WU per °C below 15 °C (daily mean) × house draughtiness |
| **h** person-hour | one hour of adult work at full fitness | fitness 0.35–1.0 from nutrition and health |

A temperate household (mean 8 °C) burns ≈ 200 WU a year, a tropical one ≈ 85, a subarctic one
≈ 340 (research: fuel 1–3 t/person/yr in temperate Europe, heating-dominated).

## 2. Resources and stocks

Each village keeps stocks (floats) with a **cause tag** each (like a field; `16_…` §2):

| Stock | Sources | Sinks | Spoilage | Storage |
|---|---|---|---|---|
| `grain` | harvest, imports | eating, seed, exports, fire | 0.05%/day (≈ 15%/yr) | 400 FU per house + 6,000 per granary; overflow spoils 5%/week |
| `fresh` (fish, meat) | catch, hunting, imports | eating (first), exports | 4%/day | unlimited (it rots) |
| `wood` | woodcutting, clearing forest for fields, imports | fuel, construction, repairs, boats, exports, fire | 0.1%/day outdoors beyond sheds | 150 WU per house + 1,500 per woodshed |
| `seed` (reserved grain) | set aside at harvest for next sowing | sowing, eaten in famine | as grain | inside the granary |

Lamp oil is ship cargo only (source of oil spills), not a village need.

## 3. Farming

A **field** is a farm cell owned by a village (`W.S.farm[cell]`, sparse):
`{ village, plan = "crop"|"fallow", sown (0–1), bio (0–1 crop biomass per sown area), ripe (bool),
harvested (0–1), weeds (0–1), gdd, sow_hour, year }`. The planet field `crop[cell] = sown × bio`
so fire, raids, floods, blight and oil keep working; the farming module syncs back
`bio = crop / sown` before it acts.

**Calendar from climate, per field** (no fixed dates): the sowing window opens when the cell's
weekly temperature has risen above 7 °C in the local spring and the ground is free of snow; it lasts
5 weeks. After sowing, crops accumulate growing degree days (7 × max(0, T − 5) per week) and grow
with the existing temperature/soil/fertility logistic. Ripe at `gdd ≥ 1,100`, or when the local
autumn cools below 6 °C with the crop past half-grown. Ripe crops left standing lose 2% per day
(4% on wet days) — harvest is urgent.

**Labour and inputs per f12 cell** (research: ≈ 25–45 person-days per person fed, labour return
≈ 10):

| Step | Hours per cell | Constraint |
|---|---|---|
| sow (tillage + sowing) | 900 | window 5 weeks; consumes 480 FU of seed per full cell |
| tend (weeding) | 500 over the season | undone tending accumulates `weeds`, each point costing yield |
| harvest (reap + thresh) | 1,400 | only when ripe; losses mount while waiting |

**Yield**: grain = harvested × sown × bio × 3,200 FU × (1 − 0.5 × weeds). A full cell feeds ≈ 10
people for a year. Harvest depletes fertility (existing `deplete`); a fallow year lets vegetation
rebuild it (existing soil model).

**Field plan** (weekly, village): target cropped cells = cells needed to feed the village with a
margin (projected need × 1.3 ÷ expected yield per cell), bounded by the labour the village can put
into sowing and harvest windows and by seed in store. Cells are chosen by expected yield minus the
cost of walking there (von Thünen); exhausted cells (fertility < 0.22) go fallow when the village
can afford it (food ≥ 6 months), otherwise they are cropped anyway (Boserup pressure; lower yields).
New fields on forest cells must be cleared first (clearing: 600 h per unit forest; the wood goes to
the stock, beyond storage it is burned → CO₂).

## 4. Forestry and fuel

- **Woodcutting**: a woodcutter in forest cell c gathers `3.0 × f/(f + 0.25)` WU per productive
  hour (f = forest density), after walking there and back. The cut is removed from `forest[c]`:
  Δforest = WU / FOREST_WU (4,250 WU per f12 cell at density 1). Blame: the village's cause.
- **Which cell**: best `f/(f+0.25)` × (productive hours after travel) among forest cells within 4
  cells; nearer is better. As near cells thin, woodcutters walk farther and bring less.
- **Fuel burn** (daily, per household): cooking + heating by today's local mean temperature ×
  (1.6 − 0.6 × house condition). Burned wood → CO₂ (ledger "villagers"). When the stock runs out,
  households go cold: health loss when T < 5 °C, doubled for infants and elders.
- **Sustainability check**: a forest cell at density 0.5 regrows ≈ 300 WU a year; a village of 10
  households needs ≈ 2,000 WU of fuel plus construction — ≈ 7 cells worth of regrowth. Villages
  therefore carve visible clearings; villages on open grassland travel far for wood.

## 5. Construction

| Building | Timber (WU) | Labour (h) | Effect |
|---|---|---|---|
| house | 60 | 240 | home for one household (capacity 7), 400 FU grain + 150 WU storage |
| hut (founders' shelter) | 15 | 60 | capacity 5, decays 25%/yr, replaced by a house later |
| granary | 50 | 200 | +6,000 FU grain storage |
| woodshed | 25 | 80 | +1,500 WU sheltered |
| pier | 30 | 150 | boats can moor; needed for fishing and merchant calls |
| fishing boat | 25 | 160 | a boat for a crew of 2–3 |
| repair | 12 | 40 | condition +0.4 (houses decay 6%/yr, faster in storms/fire) |

**Demand → queue** (weekly village plan, most urgent first): (1) households without a home
(founders, migrants, fire victims); (2) betrothed couples waiting for a home; (3) households over
capacity; (4) a pier if fishing waters and none; (5) boats if crews exceed boats; (6) repairs below
condition 0.5; (7) storage if stocks overflow. At most `1 + pop/25` sites at once.

**Sites**: a building entity with `progress = 0`, placed on the next free lot (`12_…` §4.3). Wood is
drawn from the stock as work proceeds (wood per hour = timber / labour); with no wood, builders
cannot work and the site stalls (its stall carries the wood stock's tag). Weather: rain ×0.4,
storm/snow 0. Completion emits `house_built` (not a headline) and assigns the waiting household.

## 6. Labour allocation (the village's daily brain)

**Workers**: adults 14–59 at fitness; children 10–13 and elders 60–69 at half fitness for light
tasks (tending, fuel, harvest help); the sick, late-pregnant, nursing mothers of infants (half), and
people away on voyages are not available.

**Weekly assignment** (village's staggered weekday, or at once when a harvest ripens or a storm
ends): list task demands in person-hours for the coming week with priorities, then assign workers —
first by occupation (a farmer keeps farming, a fisher keeps their boat), then the rest to the most
urgent unmet demand, in stable person order.

| Task | Demand (h / week) | Priority |
|---|---|---|
| harvest | remaining harvest hours on ripe or ripening fields | 100 |
| sow | remaining sow hours in the window (seed permitting) | 90 |
| fuel | hours to bring wood up to 60 days of fuel (90 in autumn/winter) | 80 if < 14 days, else 40 |
| fish | crews for available boats, sized to keep fresh food at its share of need | 70 if food < 60 days, else 45 |
| build | builders for sites with wood (≤ 4 per site) | 65 homes, 50 boats/pier, 35 repairs, 30 storage |
| timber | hours to cover queued sites' wood not in stock | build priority − 5 |
| tend | weeding hours on growing fields | 50 |
| clear | new fields the plan wants | 40 |
| hunt | (until W4 herds) meat where grazers are dense | 20 |

**Chayanov rule**: demands are *needs*, not maximums — when stocks are at their targets, fuel and
fishing demands shrink, and unassigned people have leisure (visible socialising). Hungry villages
put everyone to work.

**Daily execution** (village day step): each assigned group works `hours_today` = the work window
(dawn + 1 h to dusk, minus a meal hour, at most 10 h) − 2 × travel to its site; × weather factor;
× the group's summed fitness. Progress/production = hours × rate (tables above). The day's plan is
stored (task + site per person, trip plans per boat) so the view can show exactly it.

## 7. Consumption, rationing, famine coping

Daily at the village day step: households eat `fresh` first, then `grain` (never the seed reserve
unless coping). **Ration** r ∈ [0.55, 1]: 1 when food covers the projected need until the next
harvest + 30 days; otherwise food ÷ required, so the store lasts. **Coping sequence** (research
§4): r < 1 → `rationing` event; food < 14 days and a seed reserve exists → eat seed (`seed_eaten`,
fewer fields sown next spring); prolonged r < 0.7 → `famine`; migration decisions weigh months of
hunger (`12_…` §3.6). Hunger lowers work fitness (0.35 + 0.65 × nutrition): hungry villages produce
less — the poverty trap is emergent, not scripted.

## 8. Trade and value

- **Targets** per village and good: grain = need until next harvest + 60 days; wood = 90 days of fuel
  + queued construction; fresh = 7 days.
- **Surplus** = stock − 1.3 × target (> 0); **deficit** = target − stock (> 0), in need-days of the
  holder.
- **Merchant ships** (`13_…`) unload what a port lacks and load what it has in surplus (up to 60% of
  the surplus and the free hold). This is redistribution by circuit: grain from farming villages to
  fishing villages, wood from forest villages to grassland villages, fish the other way.
- **Reciprocity ledger** (per village pair, in need-days): given − received, for the chronicle and
  for kin gifts later (daughter villages help parents in famine).

## 9. Settlement expansion and contraction

Growth is physical: more households → more houses (timber, labour, lots) → more fuel and food →
more fields (clearing, walking farther) and more woodcutting (forest thins outward). A village whose
lots fill claims a neighbouring land cell as village land. Contraction is physical too: empty houses
decay into ruins, unworked fields fall fallow and then wild (planet succession), logged forests
regrow. Founding and abandonment: `12_PEOPLE_VILLAGES.md` §4.
