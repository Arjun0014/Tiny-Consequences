# TC2-06 — Ecology and evolution

Representation: D2-013. Research: `02_RESEARCH.md` §6; TC1's W4 herd/pack ecology (`docs/world/14_ECOLOGY.md`)
for predator-prey calibration (refuges, winter starvation, kill rates rising with snow).

## 1. Species as data
`Species { id, name, lineage parent, trophic role (producer/grazer/browser/omnivore/predator/scavenger/
fish/bird), body size, generation time, fecundity, lifespan, habitat preferences (temperature, moisture,
PFT), diet table, dispersal range, sociality (herd/pack/solitary), domesticable flag, trait vector }`.
Traits (examples): cold tolerance, heat tolerance, body size, speed, diet breadth, fecundity, wariness.

## 2. Populations
- **Abundance fields** per species per N64 cell, by age class (young, adult, old), with condition (body
  reserves). Updated every 4 days: births in the breeding season by condition, deaths by age, starvation,
  predation, cold, disease, hunting; dispersal to neighbours by attractiveness (food, few predators,
  habitat match), blocked by water and ice rules.
- **Regional populations** (connected habitat components) hold trait means and variances; each breeding
  season applies selection differentials from the season's mortality causes (cold killed the small-coated
  -> mean coat rises), the breeder's equation `R = h^2 S`, drift scaled by 1/Ne, and mutation variance.
- **Speciation**: when two populations of a species have been out of contact (gene flow below a threshold)
  long enough and their trait distance exceeds a threshold, the smaller becomes a new species (lineage
  child) - an epochal history event with its cause (the isolation event: sea rise, ice, a range).
- **Extinction**: abundance zero everywhere -> epochal event with the dominant cause tag.

## 3. Super-individual agents near people (and the camera)
Herds and packs near settlements or the camera are agents (count by age class, leader position,
activity: graze, rest, move, flee, hunt) created from the field and returned to it; hunters target a
specific animal (an instantiated individual with a stable id when it matters: the stag that was hunted,
the wolf that took the lamb). Members are elaborated visually (spacing, following, alertness).

## 4. Livestock
Persistent individuals owned by households (sheep, goats, cattle, pigs, dogs; later horses), with
age, sex, condition, pregnancy, owner, herd; daily activity (pasture, pen, milking); domestication is a
technique applied to a wild species (F4), not a given.

## 5. Ocean
Plankton and fish fields by temperature, nutrients (upwelling, river mouths), light and ice; fishing as
catch per effort; oil and ash as pollutants; visible schools elaborated from fish density.

## 6. Phase
F2. The F0 proof of concept keeps vegetation simple (fuel and biome) and has no animals.

## 7. Implemented (D2-035 - D2-039)
- **Species as data** (D2-035): eight land kinds (deer, hare, aurochs, boar, ibex, wolf, fox, bear) and five of the sea
  (herring, sardine, cod, tuna, seal; D2-039), varied per world; abundance and condition per species and cell.
- **Land**: five foods (grass above a floor, shoots, mast and roots, small fare, crag fare for climbers), lean winters,
  preferred mixes with substitution, Holling II hunting on reachable prey with refuges (woods, crags) and size-limited
  vulnerability, territories, condition-gated spring breeding, hunger and cold deaths, hibernation, grazing that the
  grass grows back from.
- **Sea** (D2-039): plankton derived from light, nutrients (cold mixing, shelves, river mouths), warmth and ice; fish
  eat it, cod/tuna/seals hunt them; wide-ranging hunters (`Range`), cold-blooded appetites (`Metab`).
- **Dispersal** by suitability; hunters toward their prey.
- **Evolution** (D2-037, D2-038): a coat per species and cell follows the climate at a breeding-paced rate (adaptation
  measurable over centuries); a neutral lineage marker wanders per isolated population; a sizeable population whose
  lineage has drifted far enough (sooner where climates made the coats differ) becomes a species of its own; empty
  kinds are extinct - both Epochal history events.
- **Worldgen**: a 20-year spin-up with the plants, refuges so no kind dies before history.
- **Evidence tools**: `planet --eco --ecodiag` (deaths by cause, births), `--ecocsv` (series), `--steep`.
- Predator-prey quasi-cycles measured (seed 42, 400 years): hares 44k-92k on a ~4-year quasi-period with foxes 2-3 years
  behind (r 0.41), wolves 2 years behind deer (r 0.81).
Not yet: age classes, herds as agents near people and hunting by people (with F3), livestock, fishing, the isolation
event named as a speciation's cause, visible shoals and seals in the view.
