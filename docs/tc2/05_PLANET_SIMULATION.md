# TC2-05 — Planet simulation

Grid and scale: D2-007 (equiangular cubed sphere; N32 climate, N64 surface; R = 10 km). Rates: `04` §3.
Laws: `01_WORLD_LAWS.md`. Research: `02_RESEARCH.md` §5.

## 1. The grid

- Face f in 0..5, cell (i, j) in 0..N-1; index `f*N*N + j*N + i`. Cell centre: the cube point
  `(tan a, tan b, 1)` rotated to face f, normalised, with `a, b` equally spaced angles in [-pi/4, pi/4]
  (equiangular). Precomputed per level: centre unit vectors, areas (exact spherical quadrilateral areas),
  4 edge neighbours + 4 corner neighbours across seams, and a lookup from a unit vector to its cell.
- Parent/child mapping between levels (N32 <-> N64: 2x2) for coupling climate and surface fields.

## 2. State (initial set; adding a field needs a DECISIONS entry)

| Field | Grid | Meaning |
|---|---|---|
| `elev` | N64 | elevation (m) relative to the reference sphere |
| `crust`, `crustThick`, `crustAge`, `plate` | N64 | continental/oceanic crust, thickness, age, plate id |
| `stress` | N64 | accumulated boundary stress (quakes) |
| `rock`, `soilDepth`, `fertility` | N64 | bedrock hardness/kind, soil, nutrients |
| `temp`, `tempMeanYear` | N32 | air temperature now; running annual mean |
| `moist`, `precip`, `cloud` | N32 | atmospheric water, precipitation in the last step, cloud cover |
| `ice` | N64 | snow/ice water equivalent (land and sea) |
| `soilWater`, `riverFlow`, `lakeWater` (state), `lakeDepth` (derived) | N64 | hydrology on the drainage tree (D2-022) |
| `veg[pft]` | N64 | biomass per plant functional type (grass, shrub, broadleaf, conifer, crop) |
| `fuel`, `burning` | N64 | dead fuel; active fire intensity (sparse set) |
| `magma` | N64 | magma gathered under a cell, not yet drawn by a vent (D2-028) |
| `stress` | N64 | slip deficit on fault patches (quakes, D2-030) |
| global | - | CO₂, aerosol, sea level, ice volume, orbital phase, ledgers of contributors; vents, plumes, the volcanic supply reference |

Cause tags (`11` §6) accompany the causal fields: burning, drought anomaly, flood, ash, ice.

## 3. Systems (foundation versions first, full versions in F1)

1. **Insolation and temperature** (N32, 6 h): daily-mean insolation by latitude, declination and orbital
   phase; energy balance `C dT/dt = S(1-a) - (A + B T) + G(CO₂) + D·Lap(T)` with land/ocean heat capacity,
   albedo from ice and vegetation; elevation lapse rate applied when sampling for N64.
2. **Moisture and precipitation** (N32): evaporation by temperature and surface; advection along
   prevailing wind bands (trades, westerlies, polar easterlies shifting with season); precipitation where
   moisture exceeds capacity (orographic lift from N64 relief); snow below 0 °C.
3. **Weather realisation** (daily): wet/dry spell Markov chain per climate cell keyed by (cell, day);
   storm entities spawned over warm seas, moving with the wind, carrying their own rain and wind.
4. **Hydrology** (N64, daily; implemented in F1, D2-022): soil bucket with a downpour's quick runoff,
   saturation excess above 85 % and a slow baseflow (TC1 D-179: excess runs off without leaving the soil twice);
   `Drainage` (priority flood: receivers, stack, drainage areas; rebuilt and versioned when relief or sea level
   change) routes the day's runoff; **depressions** (filled cells grouped by the cell they spill into) hold their
   water (`lakeWater`, kept at the deepest cell), lose evaporation from their wet area (~0.6 mm + 0.16 mm/°C a day,
   little under ice) and pass on only what overflows: lakes fill, spill, shrink in droughts and dry into flats.
   Rain on open water falls straight in. `riverFlow` is the discharge smoothed over ~30 days: a **stream**
   carries >= 1,000 m³/day (~12 l/s), a **river** >= 6,000 m³/day (~70 l/s); a **lake** is water deeper than
   0.3 m. Scale: 450 km² of land in many small catchments makes brooks and small rivers (largest ~0.1-0.4 m³/s)
   - physically consistent; the view widens them with distance.
5. **Vegetation and soil** (N64, 4 days): PFT growth by temperature, water, light and fertility;
   competition for space; succession grass -> shrub -> forest; seed rain from neighbours (no life from
   nothing); fire, grazing, clearing and blight as losses; fuel from litter.
6. **Fire** (PROCESS while active): ignition by lightning (storms x dryness x fuel), people, lava,
   impacts; spread to neighbours by fuel, dryness, wind and slope; burn-out; heat and ash; cause tags.
7. **Tectonics** (decadal): plate rotations; semi-Lagrangian advection of crust properties per plate;
   boundaries classified by relative motion (convergent / divergent / transform); uplift at collisions,
   trenches and arcs at subduction, new crust at ridges; stress accumulation -> quake events; volcano
   pressure -> eruption events.
8. **Erosion** (decadal): stream power incision (Braun-Willett implicit O(n) on the drainage DAG), hillslope
   diffusion, deposition in basins and at coasts, weathering to soil.
9. **Global climate** (daily): CO₂ sources (volcanoes, fires, burning, clearing) and sinks (growth,
   weathering, ocean), aerosol decay, ice volume -> sea level -> coastline reclassification events.

## 3b. F1 designs in detail

### Tectonics: crust samples that ride their plates (after Cortial et al. 2019)
- **Samples**: one crust sample per surface cell at the start `{pos (unit vector), plate, crust kind, thickness,
  elevation, age}`; samples move exactly with their plate (rotation about the plate's Euler pole by `Omega dt`), so
  relief travels without numerical diffusion (semi-Lagrangian resampling on a grid would blur mountains away).
- **Step** (every 10 world years): rotate samples; rasterise: each cell looks at the samples that landed in it -
  none: a gap (divergence) gets a new oceanic sample at ridge depth, age 0, plate of the nearest neighbour;
  one: the cell takes it; several plates: convergence - continental overrides oceanic, younger oceanic overrides
  older (the loser subducts and is consumed: a trench on its side, arc volcanism and uplift on the overriding
  side); continent against continent thickens and uplifts (collision ranges). Uplift rates accumulate in a field
  that erosion works against.
- **Stress** accumulates at boundary cells by relative velocity; past a noisy threshold it releases as a quake
  event (magnitude from the stress), scheduled at a time inside the step. **Volcano pressure** gathers at arcs
  and hotspots; eruptions are events (VEI by pressure), aerosol and ash written to fields.
- At ~1 m/year and 245 m cells a sample crosses a cell in ~250 years: coasts and ranges move over millennia.
- Cost: 24K rotations + cell lookups per 10 years: negligible.

### Volcanoes and the carbon cycle (implemented, D2-028)
- Magma rises on the overriding side of subduction (from the tectonic forcing pass), a little at continental rifts,
  and over 2-4 fixed mantle plumes; vents draw it from 5.5 cells around, new vents open where magma gathers beyond any
  vent's reach, and vents ride their plates (a plume under a moving plate leaves a chain of volcanoes and islands).
- Each vent pours part of its magma out quietly (plume vents most: shields and islands) and holds the rest against a
  lognormal seal: most erupt often and small, a few stay plugged and blow big. Hazard ~ pressure³; VEI from the
  pressure released.
- An eruption is an event whose consequences carry it as their cause: lava fires and cleared land, ash on pasture,
  woods and standing crops, deaths near great eruptions, a stratospheric veil (VEI 6 ~0.15 aerosol: a cold year or
  two); its cone joins the relief at the next tectonic step.
- CO₂: volcanic outgassing (by the vents' supply against the world's starting supply) against silicate weathering
  (faster when warm and rich): a ~2,000-year thermostat.

### Earthquakes (implemented, D2-030)
- Boundary cells load with the plates' relative motion into a slip deficit; past a threshold a patch breaks at a moment
  inside the decade and passes part of its stress to its fault neighbours (Olami-Feder-Christensen cascades: sizes
  follow a power law without being drawn from one); faults no plate loads heal.
- Magnitude from the stress released, intensity from magnitude and distance; consequences carry the quake as cause:
  deaths indoors where houses fall, spilled stores, landslides on steep shaken slopes (a slide can dam a river), and
  waves from great quakes under the sea over low coasts.

### Drainage and erosion
- **Priority-flood** (Barnes et al. 2014) fills depressions to give every land cell a receiver (lowest of 8
  neighbours after filling); filled depressions are lake basins (a lake where inflow exceeds evaporation).
- **Stack order** from outlets upstream; drainage area by accumulation in reverse order; discharge from the
  hydrology's runoff along the same tree (rivers = discharge above a threshold).
- **Stream power** (Braun & Willett 2013, n = 1): for cells in stack order
  `h_i' = (h_i + U dt + F h_r') / (1 + F)`, `F = K dt A^m / L` - implicit, O(n), stable for any dt; plus
  hillslope diffusion; eroded mass deposited in lakes and offshore (deltas). Rebuild drainage when relief or
  sea level changed enough (versioned; routes and settlements read the version).

### Climate: an energy balance (implemented as a zonal balance with per-cell ice and lapse, D2-024)
- `C dT/dt = Q(lat, t, orbit) (1 - albedo) - (A + B T) + G(CO2) - k_aer * aerosol + D Lap(T)` on N32, daily;
  heat capacity large over the sea (memory ~years), small on land; albedo from ice, snow, vegetation, sea.
- Orbit (fictional, fast): obliquity 18-28 deg over ~1,300 years, eccentricity and precession modulating the
  seasonal insolation over ~3,100 and ~600 years.
- Moisture: evaporation by sea temperature, advection by the season's wind bands, precipitation above a
  temperature-dependent capacity and on windward slopes (rain shadows), snow below freezing.
- Ice: snow accumulates by degree-days, ice sheets grow and flow slowly, sea level follows land ice volume and
  temperature; coastline reclassification is a historical event (land bridges, drowned plains).

## 4. Proof-of-concept scope (F0)

Grid with neighbours and lookup; worldgen of relief from a few plates (static in F0) and noise; a sea
level; a simple climate (latitude + elevation + season temperature; moisture from coasts and wind bands);
biomes for colour and fuel; dryness; the fire process with spread, cause tags and history; deterministic
lightning from a storm chance. The full systems follow in F1.
