# V3-10 — Agents, objectives, navigation, life stages, settlement growth

Code: `sim/agents/` (pure Lua 5.1, deterministic, no Defold). Decision: D-078.

## 1. Layers

| Layer | Clock | Contents |
|---|---|---|
| Planet | one step per game day (`W.hpt = 24`) | climate, water, vegetation, fire, ocean, herds/packs (unchanged systems) |
| Settlement map | continuous time; a planner tick each game hour | tiles, lanes (wear), trees, plots, buildings, stores, job board |
| Agents | continuous time, event-driven | people (and later visible animals, boats) |

The agent layer is the `agents` system (`sim/agents/init.lua`). It owns `W.S.v3` (settlements,
people, items) and advances with `agents.advance_to(W, t)` to any fractional hour, processing
events strictly in (time, id) order — the result is identical whether the host advances in one
call or a thousand. `world.step` (an hour) calls it to the hour boundary first.

## 2. The settlement map (`sim/agents/map.lua`)

- A square local map on the tangent plane at the settlement's sim cell: 128 × 128 tiles of 0.025
  world units (3.2 × 3.2 units, the village cell and its neighbours; the planet radius is 10).
  A local point (x, y) maps to the sphere by the gnomonic projection `normalize(R·n + x·e + y·q)`.
- Per tile: land/water, the sim cell it lies in, base cost, **wear** (traffic), occupant
  (building footprint).
- **Lanes are emergent**: every walk adds wear to the tiles crossed; wear decays daily. Wear above
  thresholds makes a *trail* then a *lane* (cheaper to walk), so the paths people use become the
  paths people prefer, and the view draws them.
- **Trees** are individual: position, size (grows), state (standing / felled with logs left /
  stump), claim. Seeded from the planet's `forest` field of each tile's cell; felling writes the
  planet field back (forest biomass, CO₂ to the ledger).
- **Plots** (fields): rectangles of 8 × 8 tiles worked row by row (sow → tend → harvest), owned by
  a household; growth from the plot cell's temperature and soil water each day.
- **Buildings**: footprint, door tile, kind (house, granary, woodstore, well), state (site →
  frame → walls → roof → standing; ruin), logs needed / delivered, work needed / done, stock.

## 3. Navigation and movement (`sim/agents/nav.lua`)

- A* on the tile grid, 8-connected, octile heuristic; costs: open land 1.0, under trees 1.5,
  across a plot 1.6 (people walk the edges), trail 0.75, lane 0.55; water and footprints blocked
  (except the origin/target doors). Each person has a **route temperament** (0-2): how much they
  prefer lanes over shortcuts, so two people from the same house do not walk identical lines.
- Paths are string-pulled into waypoints (line of sight over tiles no costlier than the path's),
  then walked at the person's speed: base 3.2 world units/hour × pace (0.85-1.15) × age factor
  (children 0.8, elders 0.7) × load factor (carrying 0.85) × night/rain factors.
- Motion is **analytic**: `pos(t) = path at (t − t0) × speed`. The view evaluates it every
  frame; the sim never has to tick a walking person.
- Path cache keyed by (from, to, temperament, map version).

## 4. Agents (`sim/agents/people.lua`)

Each person persists continuously:

| Group | Fields |
|---|---|
| identity | id, key, name, surname, sex, born (hour), mother, father, partner, children, household, home |
| stage | infant (< 2), child (2-11), adolescent/apprentice (12-15), adult (16-54), elder (55+) |
| work | role (farmer, woodcutter, builder, carer, none), skills (farm, wood, build, carry: 0-1, grow with practice), mentor (apprentices) |
| body | hunger (0 fed → 1 starving), energy (1 rested → 0 exhausted), health (0-1), fear |
| mind | traits: chronotype (wake offset −1.5..+1.5 h), pace, diligence, sociability, patience; relationships (≤ 8: id, affinity); memories (ring of 8 events) |
| now | x, y; action, t0, t1; path; objective {kind, target, why}; plan (step queue); carrying {kind, amount}; inside (building) |

## 5. Objectives and the job board (`sim/agents/think.lua`, `jobs.lua`)

No central scheduler assigns people. The settlement posts **needs as jobs** on a board; each
person, when their current step ends, scores what they could do and picks the best:

1. **Body**: sleep (night past their bedtime, or exhausted), eat (hungry; at home from the
   household pantry), shelter (rain, cold, danger).
2. **Family**: care for an infant (nursing mother, or an elder/older sibling minding), keep the
   household's pantry and woodpile filled (fetch grain from the granary, firewood from the
   woodstore).
3. **Jobs** (the board): sow row r of plot k; tend plot k; harvest row r; carry sheaves from plot
   k to the granary; fell tree t; carry logs from tree t to the woodstore; carry logs from the
   woodstore to site s; build at site s; clear land for a new plot.
4. **Leisure with a reason**: visit a friend or kin (relationship), sit at the well where others
   are, children play with friends near home.

Score = urgency(job) × fit(role, skill, stage) × (1 − fatigue) − distance cost − crowding (jobs
have slots; a claimed slot is taken). Jobs come from real state (a ripe plot, a low pantry, a
site missing logs), so every choice has a *why* that names that state ("the granary holds 9
days of food; plot 3 is ripe and the rain is coming").

**Plans** are step lists: walk → work (duration) → pick up → walk → drop. Effects apply when the
step completes (a sown row, a felled tree, logs in the store). Interrupts (rain, night, danger)
cut a step: partial work is kept, the person re-plans from where they stand.

**Variation is mechanical, not cosmetic**: chronotype and energy set wake times; pace sets
speed; temperament sets routes; diligence sets work duration and how long they stay out in the
rain; skill sets work speed; distance sets who takes which job; hunger and fatigue interleave
meals and rests differently for everyone.

## 6. Weather reaction

Hourly rain spells per settlement from that day's precipitation (`sim/agents/weather.lua`). When
rain starts, each person outdoors reacts after a delay from their temperament and situation:
people near home duck in within minutes; field workers finish the row they are on; the diligent
keep working in light rain; a downpour sends nearly everyone to shelter (home, or the nearest
building). When it stops they go back to what is still needed.

## 7. Life stages and demography (`sim/agents/life.lua`)

- **Infant** (0-1): a real entity; stays with the mother or carer (drawn carried / indoors).
- **Child** (2-11): plays near home and the square, follows a parent, from 8 fetches water and
  carries light loads; never does field work.
- **Adolescent / apprentice** (12-15): helps a mentor (a parent or kin adult in a role), learns
  their skill; light work only.
- **Adult** (16-54): full work; marries; forms a household when there is a home.
- **Elder** (55+): slower, light tasks, minds children, rests more.
- Births: married women 16-44 in their own home; conception per day scaled from the annual
  rate, reduced by hunger; pregnancy 36 days (¾ year); the infant is a new person entity with
  parents, household and home. Deaths: age hazard per day, hunger and cold through health.
- Households form when a married couple has a home (Hajnal, as v2): no home → a house is wanted.

## 8. Animals (later phase R3)

The W4 herds and packs remain the macro layer (range, territory, condition, births, predation).
Each visible animal of a group becomes a persistent micro-agent with its own position and action
(graze, wander, rest, follow mother, flee, chase) steered by separation/cohesion around the group's
current objective. A kill removes a specific visible animal; a hunter's quarry is a specific
animal.

## 9. Settlement growth (no thresholds on population)

A couple without a home → the settlement plans a house site (free lot near a lane) → logs are
needed → woodcutters fell trees (trees disappear, stumps remain) → logs are carried to the site →
builders work → the frame, walls and roof appear → the couple moves in → births become possible →
more mouths → more plots cleared (trees felled, ground broken) → longer walks → lanes wear in.

## 10. Inspector and follow mode

Click a person: WHO (name, age, stage), HOME, FAMILY (partner, parents, children), ROLE and
skills, CURRENT OBJECTIVE, CURRENT ACTION, DESTINATION, WHY (the state that made them choose it),
body (hunger, energy, health), recent memories. "Follow" keeps the camera on them.
`tools/v3_follow.lua` prints a person's whole timeline headless for verification.
