# W14 — Ecology model

Research basis: `00_RESEARCH.md` §2, §6, §7. The planet layer already has a coherent vegetation
and ocean model; this document keeps those, and turns land animals from invisible densities into
herds and packs that feed, move, breed, starve, hunt and are hunted (phase W4).

## 1. Vegetation, soil, succession (kept; planet layer)

- Grass and forest logistic growth with temperature bells, soil water, fertility and light; fire
  resets; forests establish on grassland (`02` §5.6). Fertility relaxes toward a vegetation-set
  equilibrium; cropping depletes it; ash and silt enrich it.
- **Succession** after abandonment is the existing dynamic: grass returns in 1–3 years, forest
  canopy in 30–60 years — matching old-field succession (2–5 y grasses/forbs, 10–20 y pioneer
  thickets, 50–100 y forest).
- New couplings: logging removes forest biomass (`11` §4); clearing fields removes forest and grass;
  grazing herds read and eat grass (and browse forest seedlings), trampling fields raids crops.
- Visual additions: stumps and thinning where logging happened recently, shrub-stage props on
  young succession cells (forest 0.05–0.3 on formerly farmed land).

## 2. Ocean (kept; planet layer)

- Plankton (light, nutrients from upwelling and rivers, temperature, ice, oil) → fish (logistic in
  plankton-limited capacity, oil/heat/silt losses, diffusion) — a Schaefer-type biomass model.
- Add a **spawning season**: fish recruitment (the growth term) concentrates in the local spring
  and summer (× 1.6) and slows in winter (× 0.4), same annual total. Fishing grounds visibly
  "boil" (jumping fish, birds) where fish are dense in season.
- Removal only by real catches (boats), oil, heat and silt.

## 3. Herds and packs (W4)

### 3.1 Entities
```
herd  { id, key, species = "grazer", cell, n (animals), condition (0–1), tag, born (hour) }
pack  { id, key, species = "predator", cell, n, condition, territory (home cell), tag }
```
The planet fields become derived views: `grazers[c] = Σ herds in c: n / GRAZER_PER_CELL`,
`predators[c] = Σ packs in c: n / PRED_PER_CELL` (recomputed weekly after moves), so vegetation
pressure, detectors, blame release terms and powers keep their interfaces.

### 3.2 Grazers (weekly, herds staggered)
- **Forage**: intake = n × need × min(1, available / (n × need)); available = grass (+ 0.5 crop on
  farm cells: raids) × (1 − 0.8 × snow cover). Eaten biomass leaves the cell's grass (and crops,
  blamed on the herd's tag when abnormal). Browsing: hungry herds strip forest seedlings (existing
  D-017 rule, per herd).
- **Condition** += 0.15 × (intake / need − 0.85), clamped; winter snow and overgrazing drain it.
- **Movement**: stay while forage and safety are adequate; otherwise move one cell to the neighbour
  maximising forage × safety (fewer predators, not a village, not burning); in autumn herds drift
  toward lower, warmer cells, returning in spring (seasonal migration). Herds cannot cross open sea;
  sea ice ≥ 0.8 and new land are walkable.
- **Birth pulse** in the local spring: fawns = females (≈ n/2) × 1.5 × clamp(condition − 0.2, 0,
  0.8)/0.8. **Deaths**: winter starvation when condition < 0.25 (share ∝ deficit), old age 8%/yr,
  predation, hunting, fire. **Fission** when n > 40 (half form a new herd in the best neighbour).
  **Extinction** is real: a species gone from a landmass returns only by walking (ice bridges) or
  Introduce.

### 3.3 Predators (weekly)
- **Hunting**: a pack hunts herds in its cell and neighbours. Kills per week = n × 0.35 × success,
  success = h / (h + 0.3) × (1 + snow) × (1 + 0.5 × fawning season) (research: 15–20 deer per wolf
  per year; ~2× kill rates with snow; peaks at fawning), shared across herds by size; prey refuge
  (herds below 3 animals are rarely caught).
- **Condition** from kills vs need (≈ 0.35 per wolf per week); **pups** in spring (n_pairs × 4 ×
  condition survival); **dispersal** when n > 8 (young pair leaves to found a pack where prey is and
  packs aren't); starvation when condition < 0.2.
- **Movement**: toward prey (herds) within the territory, territory shifts slowly with prey.
- **Villagers** cull wolves near fields (existing hook → kills pack members).

### 3.4 Hunting by villagers
Hunters (a task) go to the nearest herd within 3 cells: kills = hours × 0.02 × herd density factor;
meat = kills × 40 FU into `fresh`. Removes animals from that herd, blamed with the village cause.

### 3.5 Why this matters
- Cull every pack → herds grow unchecked (release) → overgrazing and browsing → forest can't
  regenerate, erosion, silt → fish at river mouths decline → fishing village catch falls — the S02
  chain, now carried by animals you can watch.
- Hard winters starve herds; wolves eat better in snow; the spring birth pulse depends on the winter.
- Hunting provides meat when harvests fail — until the herds near the village are gone.

## 4. Numbers and cost

~500 land cells; GRAZER_PER_CELL = 20 animals per unit density; ≈ 60–150 herds and 10–25 packs.
Weekly updates of ~150 entities cost less than the current per-cell fauna pass over all land
cells (≈ 20k instructions), because empty cells cost nothing.
