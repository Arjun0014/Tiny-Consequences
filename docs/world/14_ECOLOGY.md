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

~550 land cells at f12; ≈ 150–200 herds and 25–30 packs (the P07 world). The first estimate
(cheaper than the field model, ≈ 20 k) was wrong: the groups update monthly but each touches its
whole range several times (densities, forage and predation per cell, demography, behaviour, the
derived fields and their causes), and ranges overlap. Measured (D-073): ≈ 125 k VM instructions a
tick as first written, cut to ≈ 87 k with identical state hashes (ranges by reference, densities
reused when no group changed, a village-ground version key, a land-only scan, memoised neighbour
views); the field model cost ≈ 41 k. Per month: the ecology of ~500 touched cells and the
demography of ~200 groups (each over its 7-19 range cells) are most of it. On main (D-076, P07
re-baselined): ≈ 77 k a tick, 8% of the planet week, with a flat line profile (no line above
1.5 k): the cost of the groups themselves, ~1.9× the field model.

## 5. Implementation (D-070 groups, D-071 ecology v2)

`sim/systems/fauna.lua`: herds (deer, reindeer, antelope, boar) and packs (wolves, big cats) are
entities in `W.S.wild` (struct of arrays, saved): centre cell, head count, **body condition**
(fat reserves 0..1), cause tag, last move; packs also their natal pack (kin). A herd spends 40% of
its time on its centre cell and the rest on the ring about it; a pack ranges two rings from its
den. The planet fields `F.grazers`/`F.predators` are derived views (animals per cell / 100, / 25),
so vegetation pressure, detectors, release blame, powers and hunting keep their interfaces.
Monthly update (every 4 ticks):

- **Forage per head** on a cell = min(1.5, food / (0.6 H + 0.02)) + browse − 0.8 × snow, food =
  (grass + 0.5 crop) × (1 − 0.8 snow). **Browse** (D-076): what the grass lacks of their need
  (1 − food per head, up to half of it: a ruminant cannot live on woody browse alone) the herds
  make up from the trees as far as the woods reach (× forest / (forest + 0.08)); it nourishes them,
  and the forest loses it at twice the weight (browse is worth half as much as grass). The snow's
  energy cost is not made up. **Condition** += 0.2 × (forage − 1) a month: herds fatten in summer
  and live on their reserves and their trees through a snowy winter or a dry season.
- **Fawns** once a year in the local late spring (week 16): 1.0 a head × ramp(the winter's lowest
  condition 0.2..1.0; Verme 1977) × 1 / (1 + crowding²), crowding = the range's animals against
  what its grass carries (D-073: density acts through juvenile survival near carrying capacity;
  without it herds overshot their grass and starved almost to the last, taking the packs with
  them). A hard winter is paid for in the next spring's fawns. **Starvation** below condition 0.25; old
  age 1% a month. No logistic term: density dependence is food per head through condition.
- **Predation** (Holling II + refuge on the cell densities) with the attack rate × (1 + 1.5 snow)
  × (0.7 + 0.75 weakness): wolves kill 2-3× as often in deep snow and take the weak (Nelson & Mech
  1986; Post et al. 1999).
- **Hunger browsing**: what the grass lacks of a herd's need (crowding, snow, drought) it takes
  from the trees at 0.5 of grass's worth, and lives on (above); well-fed herds only nibble. How too
  many deer, or a hard winter, stop a forest regrowing — and how a herd on a predator-free island
  holds on after its grass is gone, strips the woods and then dies out (St Matthew Island).
- **Densities** are the sum of the groups' ranges, computed monthly; a cell no range reaches holds
  no animals (D-076: before, cells the herds had left kept their old density, and dispersing
  wolves settled on the ghost of prey long gone; a reloaded world diverged from the live one).
- **Packs**: condition by kills per wolf against need (m/e of the old calibration) plus a
  territory's **small game** (enough for 1.2 wolves / pack size); **one litter** a year (week 12,
  the breeding pair's): 5 × ramp(condition 0.25..0.75); starvation below 0.3; old age and strife
  1.5% a month. **Whole wolves**: births and deaths are drawn (FAUNA stream, keyed by pack and
  month).
- **Dispersal**: in the autumn (and whenever it is starving) a pack keeps 2 + 6 × condition
  wolves; the rest leave one by one; 60% live to settle, within 7 steps, where prey per wolf is
  best — a lone wolf that is no kin (a mate), free ground with prey (**territories are exclusive**:
  no new den within two steps of another), or a pack with room. A disperser that finds nothing
  stays home. **Lone wolves roam** 8 steps for a mate (no siblings). A starving pack moves its den
  toward prey.
- Herds move to better cells (food per head after the other herds, safety, habitat, away from
  villages and fields), migrate (reindeer, antelope), split past their size or when crowded, and
  **merge** with their kind on the same ground (fission-fusion); dwindled herds join one nearby.

Why the cycles are regional and real: the group model of D-070 had local cycles (a pack's yearly
CV 0.28) with no synchrony between packs (pairwise r ≈ 0.00), so ~24 packs a continent averaged
them away (continental CV 0.05). The field model's synchrony came from an unrealistically strong
cell-to-cell diffusion (patch synchrony 0.80-0.91 at every distance). Now: shared winters weaken
herds and feed packs across a region at once (the climate's wobble; B18), packs answer a spring
later (condition → litters), crashes leave lone wolves that pair up again, and dispersers carry
wolves from hungry to prey-rich country. Measured (seed 1, 100 years, `tools/wild_dyn.lua`):
continental predator CV 0.28-0.42 year to year (~8-year cycles: lag-4 autocorrelation −0.5 to
−0.6), prey CV 0.31-0.52, patch synchrony decaying with distance 0.50 → 0.39 → 0.33 (the shape of
real populations; Liebhold et al. 2004).

Tests (on main since D-076): fauna unit tests (conservation with rates off, no spawning, release
blame), B18 (a hard winter on the northern land: herd condition 0.86 vs 1.00, kills +37%, packs
0.59 vs 0.53, fawns a head in the herds that wintered there 0.74 vs 0.91, pups a pack 2.0 vs
1.4), B19 (a starving pack breaks up and settles toward prey), B20 (lone wolves pair; siblings
don't); full profile: fauna stability 10/10 (predator CV 0.22-0.37, no extinction in 200 years
at 2,400 people), S02 10/10, S07 10/10 (the island plays St Matthew), S01 7-8/10 (5/10 in the
last run: second-century wildfire gaps that come from the society's forest clearing, D-076). Tools: `tools/wild_dyn.lua` (CVs, cycles, synchrony by distance,
`--groups N` yearly flows, `--monthly Y`), `tools/wild_report.lua`, `npm run shots:wild`.
