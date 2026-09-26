# 03 — Causality: how the game knows "why"

The theme is Chain Reaction. A chain the player can't see doesn't exist. This document specifies how the sim records causes, how chains are built, and how they are shown. It is as important as the world model; build it alongside the systems, not at the end.

---

## 1. Concepts

- **Event**: something that happened, with a place, a time, a kind and a **parent** (the event that caused it). Stored in the event log.
- **Root event**: an event with no parent. Either a **player action** (actor = `player`) or a **natural trigger** (actor = `nature`: lightning, drought onset, eruption, quake, storm, heatwave).
- **Cause tag**: for every causal field in every cell (02_WORLD_MODEL.md §3), an integer event id describing *what is currently driving that value away from normal*. `0` means "baseline / nothing special".
- **Chain**: follow parents from any event up to its root. That list, reversed, is what the player sees.

---

## 2. Event record

```
id          int (monotonic)
kind        string enum (see §6)
tick        int
cell        int (0 for global events)
actor       "player" | "nature" | "derived"
parent      event id (0 for roots)
root        event id of the chain's root (cached for speed)
magnitude   number (kind-specific: cells affected, deaths, °C, VEI...)
data        small table of kind-specific fields (village id, species, person id...)
headline    bool (worth telling the player)
region      region-name id (for text)
descendants int (how many events trace back to this one; updated up the chain)
pinned      bool (referenced by an active cause tag, a headline, a memory, or a goal)
```

Storage: ring buffer of 8,192 events. Unpinned events older than the buffer are dropped. When an event is dropped, any cause tag still pointing to it is replaced by its nearest pinned ancestor (or 0).

---

## 3. Blame propagation (the core mechanism)

Every system that updates a causal field computes its change as a sum of **named terms**. For each cell, the system tells the blame module about each term and the tag that term carries:

```lua
-- inside a system update for one cell c
blame.begin(F_FISH, c)
blame.term(growth,        0)                      -- baseline dynamics carry no special cause
blame.term(-catch,        village_cause(c))       -- tagged with the village's current cause
blame.term(-oil_kill,     cause[F_OIL][c])        -- oil's own cause tag travels into fish
blame.term(-heat_stress,  cause[F_SST][c])
blame.term(migration_in,  inflow_tag)             -- flows carry the source cell's tag
blame.commit(new_value - old_value)
```

`blame.commit(delta)` rules:

1. If `|delta| < eps[field]`: no change to the tag; increment the tag's idle counter.
2. Otherwise find the term with the largest magnitude **in the direction of the change** (the dominant driver).
3. If the dominant term's tag differs from the current tag **and** it is at least 1.5× the second-largest same-direction term, the cell's tag becomes the dominant term's tag. (The 1.5× margin prevents flicker.)
4. If the dominant tag is 0 (baseline) and has dominated for 48 ticks, reset the tag to 0 — the anomaly is over.
5. Tags idle for 48 ticks reset to 0.

Implementation constraints:

- Zero allocations: keep per-field scratch arrays (`best_mag`, `best_tag`, `second_mag`) reused every tick.
- Only track the top two terms; never store the full term list.
- Budget: blame adds ≤ 25% to tick cost. Measure it.

### Release and tag hygiene (D-013)

- **Release**: when a pressure disappears (e.g. predators far below their normal and carrying a tag), the growth it no longer holds back carries the pressure's tag — so "wolves culled → deer boom" is blamed on the cull, not on baseline.
- Pressures carry their tag only while clearly above normal; flows pass a tag only when they really drive the change; an upstream cause never overwrites a more-derived event it already produced.

### Flows

Anything that moves between cells (moisture, oil, animals migrating, fire spreading, silt, people migrating) carries the **source cell's tag** as the tag of the inflow term. This is what lets a cause travel across the planet.

### Global fields

`co2`, `aerosol` and `g_temp` keep a **ledger**: the top 4 contributing root events by accumulated contribution, plus standing **categories** for diffuse sources that have no single root: *Villagers* (wood burning, clearing), *Wildfires (natural)*, *Volcanic outgassing*, *Oceans & forests* (sinks), and *Baseline*. The planet inspector shows both ("Who warmed the planet? You 38% · Villagers 51% · Volcanoes 11%"). Each year, contributions decay slightly so old causes fade. When a cell's temperature change is dominated by `g_temp`, the term's tag is the ledger's top contributor if that contributor explains ≥ 40% of the anomaly; otherwise 0 (it's "everyone's fault"). This is how "your volcano in Year 40 caused the ice age in Year 43" becomes traceable.

### Entities

- Villages keep a `cause` tag for their current state (e.g. famine) derived from their food sources: the tag of the dominant reason food fell (crop failure tag, fish collapse tag...).
- People keep memories of events that affected them (§7). Those are for dialogue, not blame.

---

## 4. From tags to events (detection)

Continuous fields don't make stories; **threshold crossings** do. Detectors run at the end of each tick (or month/year for slow ones). Each detector emits an event whose parent is the **cause tag of the field that crossed the threshold** at that moment.

**Re-tagging (this is what makes chains longer than two links).** When a detector fires, it writes the new event's id into the cause tags of the field(s) and cells that crossed (and into the entity's cause, e.g. the village). From then on, every downstream term driven by those fields carries the *derived* event, not the original root. Example: Cull (root) → predators fall, tagged with the root → `population_crash` fires and re-tags predators → grazers' growth (less predation) is tagged with the crash → `population_boom` fires and re-tags grazers → browsing damage to forest carries the boom → `biome_shift` (forest → grassland) → silt → `fish_collapse` → `famine`. Each detector adds one link.

Root events do the same at their origin: a power or natural trigger writes its id into the cause tags of the fields it directly changes (Cull → `predators` in the area; Lightning → `fire`; Oil Spill → `oil`).

Detectors (initial set; each has entry/exit thresholds and cooldowns to avoid spam):

| Detector | Fires when | Parent comes from |
|---|---|---|
| biome_shift | a cell's committed biome changes | tag of the field that drove the candidate (e.g. forest for forest→grassland) |
| fire_started | a cell ignites | the ignition source (lightning event, player, lava) |
| wildfire | a connected fire reaches ≥ 10 cells | the first fire_started in that cluster |
| crop_failure | a village harvest < 50% of its 5-year average | dominant tag among its farm cells' crop losses |
| famine | village food_security < 1 month | village cause tag |
| deaths_spike | ≥ 3 deaths in a village in a month from non-age causes | tag of the dominant death cause |
| migration | a family leaves a village | famine / disaster event |
| village_founded | a new village forms | the migration event that produced the settlers |
| village_abandoned | village population reaches 0 | last famine/disaster event |
| overfishing / fish_collapse | a connected fishing ground's fish −50% vs its 5-year average (named by sea, D-014) | fish cause tag |
| tanker_sunk | a tanker or boat sinks | the power or natural event that sank it |
| oil_spreading | a connected oil slick reaches ≥ 10 ocean cells | the oil's tag (tanker_sunk or the spill power) |
| oil_beached | oil reaches a coast | the oil's tag |
| population_boom / crash | regional grazers or predators ×2 or ÷2 vs 5-year average | the species' cause tag |
| extinction_regional / extinction_global | a species disappears from a continent / the planet | species cause tag |
| flood | the flood condition in 02_WORLD_MODEL.md §5.5 | precip/moisture tag |
| drought | a region's soil water stays < 0.2 for a season | moisture/precip tag |
| heatwave / cold_snap | regional temp anomaly beyond ±5 °C for a month | heat_anom / g_temp tag |
| sea_ice_formed / melted | sea ice connects/disconnects landmasses | g_temp or heat_anom tag |
| sea_level_change | coastline rebuild happened | g_temp ledger top contributor |
| era_change | planet era changes | ledger top contributor |
| eruption / quake / tsunami / new_island | geology | nature root (or player if triggered by a power) |

**Aggregation**: if the same detector would fire for many cells from the same parent within a short window (e.g. 40 forest cells burning from one lightning strike), merge them into one event with `magnitude = count` and a region name ("The Burning of the Ashen Wood").

---

## 5. Headlines, popups, and the chronicle

### Headline rules

An event is a **headline** if its kind is in the headline set (village_abandoned, famine, extinction_*, wildfire, fish_collapse, era_change, sea_level_change, eruption VEI≥3, tsunami, new_island, population_boom, population_crash, tanker_sunk, oil_beached, village_founded) and its magnitude passes the kind's minimum.

Note: chain *tests* (07_TESTING.md) walk the full event tree, not only headlines.

### Who gets told what

- **Player-rooted headline** (chain ends at a player action): a **Chain popup** (below). If several arrive together, queue them; show at most one per 3 real seconds. Setting "Pause on my consequences" (default ON) pauses time when the chain has ≥ 3 links.
- **Nature-rooted headline**: goes to the **chronicle ticker**. Only the biggest (era change, global extinction, VEI 6, tsunami) also get a popup.
- Everything else: visible in the event log panel and through "Why?" on any cell, village or person.

### The Chain popup

```
⚡ YOU · Year 3 · Lightning on the tanker "Good Intentions"
   ↓
⛴ Year 3 · The "Good Intentions" sinks
   ↓
🛢 Year 3 · Oil spreads through the Sapphire Sea (41 cells)
   ↓
🐟 Year 4 · Fish collapse in the Sapphire Sea (−78%)
   ↓
🍞 Year 4 · Famine in Rivermouth
   ↓
🏚 Year 6 · Rivermouth abandoned (pop 0)

CHAIN REACTION · 6 links · caused by you
[Show on planet]  [What if I hadn't?]  [Close]
```

- Each link is clickable: the camera flies to the event's cell and time freezes on it briefly.
- Long chains (> 7 links) collapse the middle ("… 4 more …") with an expand toggle.
- The chain's root shows a running counter: "This lightning bolt has caused 41 consequences so far."
- Colours: player links in the god colour (warm gold), nature links in a neutral colour.

### "Why?" inspector

Every selectable thing has a **Why?** button:
- **Cell**: for its biome and each unusual field (anything far from its 5-year average), show the tag's chain.
- **Village**: why is it growing/starving/gone?
- **Person**: why are they hungry/grieving/dead? ("Mara died of hunger ← famine ← crop failure ← your Drought in Year 7.")
- **Planet**: why is it this warm/cold? (ledger view: top contributors to co2/aerosol/g_temp.)

If the tag is 0: say so plainly and with personality ("Nothing unusual. The world is just being the world.").

### Chronicle ticker

A newspaper-style scrolling ticker at the bottom: "YEAR 12 — Rivermouth Gazette: Great Deer Boom Eats Everything; Wolves Blamed, Wolves Deny It". Templates per event kind with slots (region, village, species, numbers, person names). See 06_PRESENTATION.md for tone and templates.

---

## 6. Event kinds (initial list)

Player roots: `power_lightning`, `power_rain`, `power_drought`, `power_heat`, `power_chill`, `power_cull`, `power_introduce`, `power_blight`, `power_oil`, `power_meteor`, `power_volcano`, `power_quake`.

Test roots: `test_<name>` (actor = `player`), created by test hooks before the real powers exist (07_TESTING.md §3).

Nature roots: `lightning_strike`, `drought_onset`, `storm`, `heatwave_onset`, `eruption`, `quake`, `meteor_natural`.

Derived: every detector in §4 (`biome_shift`, `fire_started`, `wildfire`, `tanker_sunk`, `oil_spreading`, `crop_failure`, `famine`, `deaths_spike`, `migration`, `village_founded`, `village_abandoned`, `fish_collapse`, `oil_beached`, `population_boom`, `population_crash`, `extinction_regional`, `extinction_global`, `flood`, `drought`, `heatwave`, `cold_snap`, `sea_ice_formed`, `sea_ice_melted`, `sea_level_change`, `era_change`, `tsunami`, `new_island`, `person_died` (only pinned when someone is inspected or remembers it)).

Each kind has: icon, text template, headline minimum, cooldown, aggregation window. Put these in one data table (`sim/causality/event_kinds.lua`), not scattered in code.

---

## 7. Memories (for dialogue)

When an event affects a village (famine, fire, flood, tsunami, oil on its coast, a power used within 3 cells), every villager present gets a memory entry `{event_id, salience}`. Personal events (death of a partner/parent/child, own house burned) get high salience. Keep the 6 most salient; salience decays yearly. Dialogue lines can reference memories (06_PRESENTATION.md).

Memories are also the in-world narrators of chains: "Ever since the sky-fire in Year 4, the fish taste like coins."

---

## 8. "What if I hadn't?" (counterfactual) — Chapter 3

Because the sim is deterministic and randomness is keyed per cell per tick (05_ARCHITECTURE.md), the game can replay history without one of the player's actions.

- Keep a **snapshot** of the full sim state taken just before each of the player's last 3 powers (compressed in memory), plus the **command log** of all player actions.
- On request: clone the snapshot, replay the command log *minus the chosen action* up to the current tick, in the background (chunked across frames, progress bar, cancellable).
- Show a comparison card: population, villages alive, forest cover, species present, g_temp, and 2–3 largest differences found by comparing region aggregates ("Without you: Rivermouth thrives (212 people). With you: ruins.").
- Limit: available for actions within the last 100 world years (replay cost). Hide the button when unavailable.

Test: counterfactual locality (07_TESTING.md) — a tiny local action far away from a region must not change that region measurably within a year. If it does, randomness isn't properly keyed.

---

## 9. Performance and memory budget

- Cause arrays: one int array per causal field (~20 × 2,562 ints). Fine.
- Blame overhead ≤ 25% of tick time.
- Event creation: at most ~50 events per tick after aggregation; if more, raise aggregation windows.
- Chain building is on demand (walk parents), O(chain length).
