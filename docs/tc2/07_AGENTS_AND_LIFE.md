# TC2-07 — Agents and life

Resolution: ACTIVITY level for everyone (D2-006). TC1's v3 agent design (`docs/v3/10_AGENTS.md`) and its
primitives (`docs/v3/50_PRIMITIVES.md`) are the behavioural basis; TC2 changes the *execution* (activity
steps with analytic effects instead of tile-by-tile simulation) and the *structure* (candidate generators
instead of one decision function).

## 1. The person (one row of `People`)

| Group | Fields |
|---|---|
| identity | id, names (registry), sex, born, died, mother, father, partner, household, home settlement, culture |
| where | current settlement (or none while travelling), position (local metres in a settlement frame, or a route leg) |
| body | last meal time, energy anchor (time + level), health, hurt (+ kind, cause), pregnancy (conceived, due), nursing until |
| mind | traits from the id's keyed hash and heredity: chronotype, pace, diligence, sociability, boldness, curiosity, patience |
| skills | small fixed array (farming, wood, building, herding, fishing, crafting, healing, fighting) 0..1, practice-driven |
| knowledge | techniques with proficiency (side table, F4) |
| ties | relationships (side table: id, affinity, kind), memories of deeds (side table) |
| now | activity (kind, target, plan, step, t0, t1, from, to, route, claims, version), why (code + args) |
| rng | per-person draw counter |

Hunger and energy are **analytic between events**: `hunger(t)` from the last meal, `energy(t)` from the
last anchor and whether asleep; nothing ticks a person between decisions.

## 2. Households, homes and care

A household (members, home building, larder, woodpile, owned fields and beasts) is the unit of residence,
budgeting and inheritance (Hajnal: a couple forms a household when there is a home). **Care obligations
are constraints**, checked by every planner: an infant is always with a carer; nursing mothers and those
near term do not take long journeys; children are fed from the household's food; no plan may need a lost
house (TC1 D-164, D-184, D-186).

## 3. Decisions: candidate generators

At each step end the person's `Decide` asks a fixed list of generators for candidates and picks the best:

| Generator | Candidates | Urgency from |
|---|---|---|
| Urgent | flee, shelter, gather dependants, fight fire, rescue, turn back, detour, wait | perceived dangers (alarms) |
| Body | sleep, eat, rest, warm up | analytic hunger/energy, night, cold |
| Care | feed/mind infant, tend the hurt, fetch a child | dependants' states |
| Household | fetch water, firewood, food from the store, cook | larder, woodpile, water |
| Work | sow/tend/harvest a field, fell and haul wood, build at a site, herd, fish, craft | the settlement's work board (needs as jobs with slots) |
| Social | visit kin or friends, gather at the square, court | relationships, sociability, idle time |
| Travel | trip to another settlement (kin, trade, marriage, relief), migrate | knowledge of places and needs |

Score = urgency x fit (role, skill, stage, health) x temperament - cost (travel time, fatigue) + a small
keyed variation; claims stop two people taking the same row or tree. The winner's **why** (the state that
made it best) is stored as a code with arguments and formatted only when inspected. Generators are
separate classes over a shared scoring frame (TC1's 6,500-line decision function is the counter-example).

## 4. Activities and steps

An activity is a short plan: `Travel(to) -> Work(duration, effect) -> Travel(to) -> Deposit(effect)`.
Durations come from distances on the settlement's path graph (or the route between settlements), the
person's speed (age, load, health, terrain, light, weather) and work rates (skill, tool, conditions).
Effects apply at step ends; an interrupt applies the done fraction. The current step's end is the
person's only pending event.

## 5. Movement

- Inside a settlement: legs between place nodes (doors, field gates, woodlot edges, the square, the well)
  on the settlement's path graph; position at t is analytic along the leg.
- Between settlements: routes over N64 cells (A* with slope, water, forest and road costs; cached per pair
  and invalidated by version), walked by day with camps at night; every leg is in the `TravelIndex`.
- Elaboration (Game layer) draws footsteps along real streets for observed people, constrained to the
  leg's endpoints and times.

## 6. Perception and the urgent choice

Dangers (fire, water, a raiding party, a predator) are physical things at places. Noticing is by the
senses (sight range by light, hearing by loudness, smoke) at the moment the danger enters range
(space-time index for travellers; settlement member scan for people at home). Noticing sets an alarm
with its cause and interrupts after a personal reaction delay; the urgent generator then weighs actions
with dependants first (TC1 primitives B, C).

## 7. Life

- Stages: infant < 2, child 2-11, youth 12-15, adult 16-54, elder 55+ (from age; no stage events).
- **Demography** per settlement once a day (an event per settlement): death hazards (age Gompertz,
  starvation through health, injury, illness), conception for married fertile women at home (reduced by
  hunger, zero while nursing), marriages among eligible adults (partner choice by proximity, kin rules,
  affinity). Births are **scheduled events** at conception + 36 days (creation schedules).
- Deaths record their cause event; estates pass to heirs; orphans are taken in by kin first.

## 7b. Bodies, hunger and the wild's food (implemented, D2-042)

- **Body reserve**: each person carries `Reserve`, days of full food held in the body (a grown body ~48 x a build of
  0.75-1.25 keyed by id; a child 16-34; an infant 8; the old less with age). Meals are sized by hunger (the share of a day's
  need burned since the last meal, more while the reserve refills) and add to `Ate`; once a day the settlement's day keeps the
  account: `Reserve += eaten / needed - 1` (capped). Health follows the reserve once half of it is gone and mends slowly
  after; death by hunger when it is spent (caused by the village's Famine event). A fasting adult lives 36-60 days, a child
  2-5 weeks; a baby at the breast lives on its mother's milk while her reserve lasts (nursing mothers need a quarter more).
  `Nutrition` is now yesterday's share of need met.
- **The wild's food** (`Core/Places/WildFood.cs`): each midnight a village surveys its grounds - the three best hunting
  grounds (land within 1.3 km), fishing grounds (sea within 0.9 km) and woods to gather in (0.75 km) by what they hold and
  how near they are - into its state. Grown people choose the wild by need (the ration's shortfall, empty stores: 1.8 x the
  shortfall, 1.0 with the granary bare) plus habit (boldness and skill, worth less the fuller the granary), times the
  expected return of the best ground for them (skill); hunting needs a grown, healthy body free of a baby at the breast or
  one due. A hunt takes whole animals (keyed Poisson by species and cell) from the ground's abundance fields; fishing takes
  continuous kg from the sea's fields; gathering takes mast that the boar and bears then do not find (`P.Gathered`). The
  catch is carried home (`Haul`) into the larder (four days of the household's need), the rest to the village store.
  Skills `SkHunt`, `SkFish` grow with practice; specialisation emerges because skill raises both habit and return.
- **COARSE** uses the same choice and catch functions: a day's free hours times the odds that the wild beats idling, and
  under need a long day (two trips); fitted to ACTIVITY (ordinary 1.6 vs 1.1, shortage 7.3 vs 8.5 hours per adult-day).

## 8. Proof-of-concept subset (F0)

People with identity, households, homes, analytic hunger/energy, health; generators: urgent (fire),
body (sleep, eat), work (fields by season, wood), social (visit), travel (trips to a neighbouring
village and back); demography (deaths, marriages, conception, scheduled births); the space-time index
and the journey interrupt; why codes; history events for births, deaths, marriages, trips interrupted.
