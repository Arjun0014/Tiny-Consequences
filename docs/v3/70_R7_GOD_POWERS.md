# V3-70 — R7 god powers, rethought from the simulation outward (session 21, D-171)

The old power list (`docs/04_PLAYER.md` §2) was designed for the v2 world of village-level fields. The v3
world is a world of persistent people, beasts, buildings, fields, rivers, weather, knowledge and deeds.
This document asks first **what a god can meaningfully touch**, then judges candidate powers by what the
world can answer, and only then names the powers.

**The rule: a god power changes physical state; the world produces the consequence.** Never
`power -> famine`, `power -> migration`, `power -> war`, `power -> hatred`. Those are consequences.

## 1. What can a god touch? (audit of the state the systems already read)

| Layer | State | Read by (systems that would answer a change) |
|---|---|---|
| Sky, per cell | `precip` (moisture forcing), `temp`/`heat_anom`, wind band | soil water, crops, pasture, rivers (infiltration excess, floods - D-169), snow and frost on maps (D-158), fire dryness, boats (storms), birds |
| Planet ground | `soil_water`, `forest`, `grass`, `fertility`, `fire` | crops, pasture, fire spread (map and planet), map trees at the edge (D-155), infiltration (woods hold water) |
| Rivers | `flow` (from rain), bankfull | map rivers (fords, bridges swept), flood water (D-169), river fish, water fouling |
| Geology | plate boundary stress per cell, volcano pressure | quakes (buildings by construction, the trapped, aftershocks, tsunami), eruptions (ash, aerosol cooling every cell) |
| Sea | `sst`, `fish`, `oil` | fishers' catches, gulls, smoked fish trade, salt? |
| Wild life (W4) | herds and packs per cell, their members on maps | hunting, wolves at the pens, dogs, grazing, deer fleeing fire |
| Map things | a tree, a roof, a person at a point | lightning (the tallest thing), fire, injury, deeds |
| Livestock | beasts' health, sickness in pens | herds, culls, hunger, restocking raids, trade in beasts |
| Fields | a plot's crop, growth, condition | harvest, hunger, relief, trade, migration |
| Global | `G.aerosol`, CO2, `g_temp` | every cell's temperature: seasons, snow, crops everywhere |
| People's minds | knowledge, deeds, trust | **not a god's to touch** (no "make them hate", no "inspire"): minds change only by what they live |

## 2. Candidates judged

Columns: **systems** that answer it (more is better); **uncertain** (the outcome depends on state the
player cannot fully see); **both ways** (can help and harm); **traceable** (its cause tag survives into
deeds and events); **play** (invites experiments). ✓ strong, ~ partial, ✗ weak.

| Candidate | Writes | Systems | Uncertain | Both ways | Traceable | Play | Verdict |
|---|---|---|---|---|---|---|---|
| **Lightning** at a point | a strike at a map point (`blaze.strike`) or a planet cell | fire, injury, deeds, lodging, rebuilding, timber, kin help, wildfire | ✓ dryness, wind, hour, who is near | ~ (clears land, ash richness) | ✓ cause on the strike | ✓ | **v1** |
| **Rain** over cells for days ("cloudburst") | precip forcing (tagged) | soil, crops (drought relief!), rivers, floods, fords, bridges, fire (puts out a wildfire), snow in cold | ✓ soil, slope, woods, where the houses stand | ✓ | ✓ flow blame tag -> flood deed | ✓ | **v1** |
| **Withhold rain** (dry spell) | precip x0 + heat on cells | soil, harvest, pasture, culls, fire danger, fords open (trade easier), river fish | ✓ stores, season | ~ | ✓ | ✓ | **v1** |
| **Warm air / cold air** | `heat_anom` on cells for days | crops (frost, heat stress D-170), snow, roofs, beasts in snow, hay, melt floods on frozen ground | ✓ season, latitude | ✓ (warmth in a cold snap) | ✓ heat_anom tag | ✓ | **v1** |
| **Stir the mountain** | volcano pressure + | eruption when it crosses its own threshold (when? how big?), ash, aerosol cooling far away, quakes, tsunami (ocean volcano) | ✓✓ | ~ (ash fertility later) | ✓ cause on the eruption | ✓ | **v1** |
| **Strain the fault** | boundary stress + at a cell | quake when it gives (now or days), buildings by construction, trapped, aftershocks, tsunami | ✓✓ | ✗ | ✓ | ✓ | **v1** |
| **Cull** a wild species in an area | W4 groups thinned; the individuals on maps die where they stand (carcasses) | wolves culled -> pens safe, deer boom -> grazing; deer culled -> hungry wolves at the pens; hunters | ✓ | ✓ | ✓ cull tag on fauna | ✓ | **v1** |
| **Bring** a species (deer, wolves, fish) | W4 groups added | hunting, wolves at pens, fishers | ✓ | ✓ | ✓ | ✓ | **v1** |
| **Sickness in a herd** (murrain) | sickness seeded in one pen | spreads in pens, to beasts traded or droved, restocking raids, hunger, culls | ✓ who trades with whom | ✗ | ✓ deed with the god as cause | ✓ | **v2** (needs the sickness to carry its cause) |
| **Blight** on a field | a plant sickness on a v3 plot | spreads plot to plot by wind and by seed grain carried in trade | ✓ | ✗ | ✓ | ✓ | **v2** (new contained mechanism) |
| **Warm / cool the sea** | `sst` anomaly on ocean cells | fish move or thin, fishers, gulls | ~ | ✓ | ✓ | ~ | **v2** |
| **Oil on the sea** | `oil` on ocean cells | fish kill -> fishers; shore food fouled | ~ | ✗ | ✓ | ~ | **v2** |
| **Meteor** | heat, fire, shaking and aerosol at a point; a wave if at sea | everything above at once | ~ | ✗ | ✓ | ✓ the big red button | **v2** |
| **Seed the woods** | forest + on cells and new saplings on maps | timber, fuel, infiltration (fewer floods), deer | ~ | ✓ | ✓ | ~ | **v3** (map trees do not yet regrow from the planet) |
| **Raise ore** | an outcrop/vein near a village (geology) | found only when someone sees it; mining, iron, trade | ✓ who finds it | ✓ | ~ | ✓ | later |
| Wind / gale | storm state on cells | boats stay in or founder, fire spread, trees? | ~ | ~ | ✓ | ~ | later (the wind model is a band, not a field) |
| "Start war", "cause migration", "cause famine", "make X hate Y", "bless the harvest", "inspire" | outcomes / minds | - | - | - | - | - | **never** |

## 3. The v1 set (R7a)
1. **Lightning** - a bolt at the point clicked (a village map point, else a planet cell): the tallest thing
   near takes it (`blaze.strike(W, s, x, y, now, ev)`); on the planet a dry cell may catch (`fire.ignite`).
2. **Rain** - a cloudburst over the cells about the point for `rain_days` (3) planet days (a forcing with
   the power's tag): crops watered, rivers rise, floods where the ground is wet and the valley flat, fires
   doused, snow where it is cold.
3. **Dry spell** - no rain and a little heat over the cells for `dry_days` (6).
4. **Warm air / Cold air** - `heat_anom` +/- `air_k` over the cells for `air_days` (4), decaying after.
5. **Stir the mountain** - the nearest volcano's pressure raised by `stir_k` of its own threshold: it may
   erupt at once, in days, or not at all (its threshold is its own, noisy).
6. **Strain the fault** - the nearest boundary cell's stress raised by `strain_k` of its threshold: a quake
   when it gives.
7. **Cull** (wolves | deer) - the groups on the cells thinned by `cull_k`; their members on any village map
   in the area die where they stand (carcasses the wolves, dogs and people find).
8. **Bring** (wolves | deer | fish) - a group added on the cell.

Each cast: a root event (actor `player`, kind `power_*`) whose id is the **cause** of what the power writes
(planet blame tags via `blame.set`, `cause` on map hazards, `event` on deeds). Nothing else.

## 4. Targeting and feedback
- Click a power, then a place: on the planet a cell (an area ring shows its reach), close in a point on a
  village map (lightning). The aim ring and a one-line tip say **what it writes** ("Rain over these hills for
  three days"), never what will happen.
- Cooldowns in real seconds (sandbox); no faith points.
- The cast is a world event with a sound and a small effect (bolt, gathering cloud, shimmer, rumble).
- Consequences reach the player through the world news (`75_PLAYER_EXPERIENCE.md`): stories caused by a
  power carry **YOU** at the end of their chain.

## 5. Acceptance
- Every v1 power is a few lines writing state through functions the world already has (R6.5's acceptance).
- The same power, cast twice in different states, produces different stories (tests by premises, never by
  outcome): rain on dry ground in a drought helps the harvest; rain on wet ground in a flat valley floods.
- A story caused by a power traces to it through real links (deeds' `event`/`answers`, events' parents,
  planet blame tags) - never through text.

## 6. Not built (and why)
- Direct edits of people (health, mood, trust, beliefs): minds change only by what they live.
- Outcome powers (famine, war, migration, abandonment, a bumper crop).
- Wind and gale until the wind is a field, not a band.
- Seed the woods until map trees regrow from the planet's forest.

## 7. Status (session 21)
- **v1 built** (D-174): `sim/powers3.lua` (the `power` command wrapped for v3 worlds), the v3 power bar
  (`game/ui/powers.lua` DEFS_V3, built once the world is known), lightning aimed at the clicked point of a
  village's map (`game/view/controls.lua`; the ring as wide as a bolt reaches). Tests `test_v3_powers` x4.
- Each cast's root event (actor `player`) is the cause of what it writes: forcings' tags (rain, dry, warm,
  cold), the strike's `event`, the strained fault's and the stirred mountain's causes carried to the quake and
  the eruption (`shake` `Q.cause`, `volcano` `v.cause`), the cull's tag on fauna. The news roots their
  consequences at YOU.
- Not yet: v2 (sickness in a herd, blight on a field, sea warmth, oil, meteor), v3 (seed the woods).

## 8. Status (session 22, D-177, D-178)
- **Lightning** takes exposed beasts (by height; a fold is low, a byre's roof takes it), throws down beasts beside
  the bolt, and the click lands on the terrain's ground or on the person or beast under the pointer; commands given
  mid-hour apply at their own moment (the bolt fell up to an hour after the click before).
- **Rain / dry spell** begin over the villages under them when cast (a downpour for 3 hours, rain to the day's end;
  the dry spell stops the rain at once); the planet's days follow.
- **Every cast tells what it changed** in the news (rooted at YOU; repeated casts merged); the cull leaves a deed for
  the animals that died on a map.
- Played: `tools/v3_power_matrix.lua` (each power in different states), `scripts/play_powers.mjs` (lightning
  through the UI). The same power gave different stories: rain on a drought soaked in, on wet hills flooded
  Krikeivorn, in a cold snap fell as snow and flooded Tekheim's river; warm air melted lying snow; a strained fault
  near Tekheim gave a magnitude 5.6 quake, far from anyone nothing felt; a stirred mountain erupted.
- v2 designed from the code in `docs/SESSION_HANDOFF.md` (session 22, Next 2) - not built.
