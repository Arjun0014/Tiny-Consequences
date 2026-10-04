# W16 — Causality for stocks and entities; the world drawn from the sim

Extends `03_CAUSALITY.md`. Everything there still holds (events, roots, blame terms, re-tagging,
detectors, chains, headlines). This document adds how *economic* state is blamed and how every
simulated thing is shown.

## 1. What carries a cause tag

| Holder | Tag meaning | Set by |
|---|---|---|
| planet fields (existing) | dominant driver of the field's anomaly | systems' blame terms |
| village stock (`grain`, `fresh`, `wood`) | dominant driver of the stock's deviation from its normal trajectory | daily stock blame (§2) |
| village `labour_tag` | why the workforce is below its 2-year normal | deaths_spike / migration / exposure events |
| village `import_tag` | why imports stopped | `shipment_lost` |
| village `cause` | the tag of its binding shortage (food or wood), as before | stock tags + detectors |
| herd / pack (W4) | why its numbers are abnormal | cull, fire, starvation events; flows |
| person death | the dominant hazard term's tag | mortality |
| building / boat loss | the event that destroyed it | fire, flood, power |

## 2. Stock blame (daily, per village and stock)

Each stock has EMAs of its normal inflows and outflows. At the day step the *anomaly* of the
change is split into terms and `blame.step` picks the dominant one (1.5× margin, holds, idle
reset — the same rules as fields):

| Term | Magnitude | Tag |
|---|---|---|
| production anomaly | today's production − normal production | harvest tag (crop tag ∪ labour tag ∪ seed tag, whichever explains the shortfall), fish ground tag, forest/logging tag |
| import anomaly | imports − normal imports | `import_tag` (a lost ship), else 0 |
| consumption anomaly | −(consumption − normal) | the tag of what raised it (migrants arriving, cold snap) |
| losses | −(fire, flood, spoilage beyond normal) | the fire/flood tag |

A harvest's tag: shortfall from lower crop biomass → the dominant crop tag among the fields
(existing); shortfall from fields not sown or not harvested → `labour_tag` if workers were missing,
the grain stock's tag if seed was missing. So "the harvest failed because the workers had left
because of the famine because the ship sank" is one chain.

## 3. New derived event kinds (society)

| Kind | Fires when | Parent | Headline |
|---|---|---|---|
| `rationing` | ration < 0.95 for 3 days | food stock tag | no (village inspector, memories) |
| `seed_eaten` | the seed reserve is eaten | grain tag | yes (small) |
| `famine` (existing) | ration < 0.7 for 4 weeks or stocks gone | village cause | yes |
| `wood_shortage` | wood < 3 days of fuel in the cold season | wood tag | no |
| `housing_shortage` | ≥ 2 couples waiting > 12 months | wood tag or labour tag, else 0 | no |
| `labour_shortage` | a sowing or harvest window closes ≥ 30% undone | labour tag | yes if it follows a player chain |
| `crop_failure` (existing) | harvest < 50% of its 5-year mean | harvest tag | yes |
| `boat_lost` | a fishing boat sinks or is wrecked | the cause | no |
| `tanker_sunk` (existing) | a merchant ship sinks | the cause | yes |
| `shipment_lost` | a sunk ship had cargo | the `tanker_sunk` | yes |
| `house_built`, `village_expanded` | construction done / new village cell | demand's cause | no / small |
| `fields_abandoned` | cropped fields left unsown for a season | labour tag | no |
| `migration`, `village_founded`, `village_abandoned`, `deaths_spike` (existing) | as before, now from households | push / cause | as before |

Re-tagging: a detector writes its id into the stock/village tag it crossed, so downstream terms
carry it (as fields do).

## 4. The world drawn from the sim (no view-side invention)

| Sim state | What you see |
|---|---|
| buildings (kind, lot, progress, condition, occupancy) | houses on their lots; staked lots → frames → walls → roofs while built; weathered/damaged by condition; ruins when abandoned, decaying |
| households awake at home at night | lit windows; chimney smoke ∝ fuel burned today |
| field state (plan, sown, bio, ripe, harvested, weeds) | furrows, green → gold, a harvest front sweeping the cell, stubble, weedy fallow, then grass and young trees when abandoned |
| stocks | granary sacks/haystacks ∝ grain; woodpiles ∝ wood; fish racks ∝ fresh fish |
| people (task, site, home, schedule, state) | exactly where the schedule puts them: asleep at home, walking to their field/boat/woodlot, working, eating, gathering at the well, children playing; fewer out working in a famine; mourners |
| boats (trip plan) | leave the pier after dawn, sail to their ground, fish, return at dusk; moored at night and in storms |
| merchant ships (path, times, cargo) | sailing between ports, docking, crates on deck |
| travel parties | columns walking along their path; passengers on a ship |
| logging (forest loss, recent cuts) | fewer trees, stumps, woodcutters at the edge of the woods |
| herds / packs (W4) | the actual animals: herd size, fawns in spring, wolves shadowing herds |
| daily weather | clouds where it is cloudy today, rain/snow where it is wet today, storms, puddled fields |
| sun | real day/night and seasons; polar day/night |

## 5. Clouds (diagnosis and target)

**Diagnosis (renderer v2)**: 56 puffs chosen by view-side randomness from the wettest cells, drifting
at a visual speed unrelated to sim time; hard cover cap 12%; clouds within 3–7 units of the eye and
over the focus are *deliberately shrunk away* when zoomed in (so they vanish exactly when you look
closely), and they are small single puffs with little shading, so they read as decorations.

**Target**:
- Cloud cover is derived from the sim's **daily weather** per cell (moisture vs capacity, wet/dry
  day, storm), placed by a deterministic hash of (cell, day) and advected with the wind through the
  day — so clouds are where it rains and move with the weather.
- Each cloud is a **cluster** of lit lumps (model-kit style mesh): bright sunlit tops, grey bases,
  rim light, soft dithered edges; types: fair-weather cumulus (small, white), rain decks (wide, flat,
  grey), storm towers (tall, dark base, rain curtain, lightning).
- **Zoom**: no shrinking. Clouds between the camera and the focus fade per pixel (a screen-space
  cut-away circle), so the view stays readable while surrounding clouds, their shadows and rain
  curtains remain. At planet view all visible-hemisphere clouds show; close up, nearby clusters use
  a richer mesh.
- Shadows: keep the cloud-shadow channel of the shadow map, now from the real cloud positions.
- Day/night: clouds are lit by the real sun, silver-lined at the terminator, dark at night.

## 5b. Clouds v5: one cloud layer the camera flies through (S23, D-180)

**Why v4 failed** (Arjun's playtest): every puff was an opaque ray-traced spheroid, so banks read as cotton
balls laid on the world; close in, a hole was cut through them around the focus (the camera never entered a
cloud); and the cover came from the v2 weather days, not from the rain the v3 villages live - a village in
the god's downpour stood under a clear sky.

**v5** (`game/view/clouds.lua`, `game/render/shaders/cloudvol.fp`, `cloudshell.fp`):
- **Where and how much** - a cover map (equirect 128 x 64, two time slices blended on the GPU) written hour by
  hour from each planet cell's weather: the same rain spells the villages live under
  (`sim/agents/weather.lua` `cell_day`, one pure draw for a village and its cell), the god's rain and dry
  spells from the moment of the cast (`W.S.powers.forcings`), the day's humidity between spells (fair-weather
  heaps where the air is damp, clear sky where it is dry). Cover gathers over the three hours before a spell,
  is full while it rains, thins after; a downpour is darker and taller. A clear cell never gets dramatic cloud.
- **The layer** - one full-screen pass marches each pixel's view ray through the cloud layer (24 samples
  between the lowest base and the highest top found at fixed distances along the ray): density = the cover
  eroding wind-carried 3D noise (iq's packed-slice texture noise), flat bases above the highest ground about
  each place, heaped tops that rise and fall with the noise (a full deck shows its lumps from above), thick and
  thin places inside; light from the sun through the cloud above the sample, sky light from above, storms
  blue-grey, night moonlit, a little warmth at the terminator, the limb haze. It writes the depth of the
  cloud's front, so solid things before it hide it; smoke and rain under a deck are hidden by it.
- **Flying through it** - from far above the clouds lie below; nearer, the deck fills the view; inside, the
  march starts at the eye (finer samples near it, the mist thinned close by) - soft grey mist, the ground
  fading in through the floor of the deck; under it, the village under its cloud (overcast light from the
  cloud shadow, rain).
- **Wind** - the noise is carried by the zonal wind of each latitude (two phases, each reset while its weight
  is nil, so the shear never builds up); the cover moves only as the weather does.
- **Shadows** - a sphere at the layer's middle drawn from the sun writes the same cloud into the shadow map's
  cover channel (soft cloud shadows on everything that reads shadow.glsl).
- **Cost** - headless SwiftShader frames with and without the layer are within noise of each other (0.5-1.8 s
  either way; the CPU rasteriser is dominated by the scene); `gfx=low` draws none (functional browser tests).
