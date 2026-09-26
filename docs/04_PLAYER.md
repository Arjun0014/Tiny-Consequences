# 04 — Player: powers, controls, UI, goals, saving

---

## 1. The loop

1. Look at the planet. Notice something (a village, a herd, a tanker, a storm).
2. Use a power on it, or don't.
3. Speed up time.
4. Get told what happened and why (chain popups, chronicle, villagers' lines).
5. Ask "What if I hadn't?", try to fix it, or cause something worse.
6. Leave. Come back later to a world that kept going.

---

## 2. Powers

Ten buttons (two have a +/− toggle). Every use creates a **player root event** (03_CAUSALITY.md) and plays a clear VFX + SFX. Hovering a power over the planet shows its radius as a ring decal and a one-line tooltip of what it will do.

| # | Power | Chapter | Target | Effect in sim terms | Real-time cooldown |
|---|---|---|---|---|---|
| 1 | ⚡ Lightning | 2 | cell, person, animal, boat, tanker | Ignites fire if fuel; kills the targeted person/animal; sinks a boat (small oil) or tanker (large oil) | 1 s |
| 2 | 🌧/☀ Weather (Deluge / Drought) | 2 | cell, radius 2 | For one season: Deluge adds moisture and forces rain; Drought blocks rain and raises evaporation | 5 s |
| 3 | 🔥/❄ Temperature (Heat / Chill) | 2 | cell, radius 2 | For one season: `heat_anom` ±6 °C (ocean cells hold it longer) | 5 s |
| 4 | 💀 Cull | 2 | cell, radius 2, choose species | Removes 90% of grazers, predators or fish in the area | 5 s |
| 5 | 🐾 Introduce | 2 | any land/ocean cell, choose species | Adds a breeding population of grazers, predators or fish (works across oceans: invasive species). Option **Settlers**: 5 people arrive by boat and found a village (always available, including after human extinction) | 5 s |
| 6 | 🦠 Blight | 2 | a farm cell | Infects the farm: crop die-off, spreads to adjacent farms and to villages within 6 cells via trade; burns out after two winters | 20 s |
| 7 | 🛢 Oil Spill | 2 | ocean cell or tanker | Releases an oil pulse (large if a tanker) | 10 s |
| 8 | ☄ Meteor | 2 | any cell | Impact: crater, fire radius, deaths, impact-winter aerosols, tsunami if ocean | 60 s |
| 9 | 🌋 Wake Volcano | 3 | a volcano | Raises pressure: eruption within ~1 year, VEI biased +1 | 45 s |
| 10 | 🌐 Quake | 3 | a plate-boundary cell | Releases stored stress now (minimum moderate magnitude); may cause a tsunami | 30 s |

Design notes:
- Powers are *nudges*. Their direct effect is modest; the interesting part is downstream. Meteor is the one big red button, deliberately on a long cooldown.
- A power never directly triggers a detector-level consequence (famine, abandonment, extinction). Those must emerge.
- Cooldowns are generous because this is a sandbox. Goals (§7) may restrict powers.
- Blight uses a sparse infected-farm set (like the active-fire list), not a new dense field.

Controls for powers: click a power (or press its number key while holding Shift: Shift+1…Shift+0), hover the planet to preview, click to cast, right-click or Esc to cancel. Touch: tap power, tap target, tap again to confirm.

---

## 3. Time controls

| Control | Key | Rate | Meaning |
|---|---|---|---|
| ⏸ Pause | Space | 0 | everything frozen, UI works |
| ▶ Normal | 1 | 2 ticks/s | a year in ~24 s; people and animals visibly move |
| ▶▶ Fast | 2 | 8 ticks/s | a year in ~6 s |
| ▶▶▶ Faster | 3 | 48 ticks/s | a year per second |
| ⏩ Max | 4 | as many ticks as fit the frame budget (≥ 96 ticks/s target) | ≥ 2 years per second |
| 🌀 Epoch | 5 | Chapter 3: 1 tick = 1 year, target ≥ 50 years/s (floor 40) | millennia in a minute |

- At ▶▶▶ and above, individual animation fades; the terrain colours, clouds, ice and props carry the story. Camera and UI stay responsive: 60 fps at ▶ to ▶▶▶, at least 30 fps at ⏩ and 🌀.
- "Pause on my consequences" (default on) pauses when a player-rooted headline chain with ≥ 3 links appears.
- The HUD clock shows "Year 112 · Summer". In Epoch mode: "Year 4,300 · Epoch".

---

## 4. Camera & selection

- **Rotate**: drag (trackball, no pole lock). **Zoom**: wheel / pinch, from "whole planet with margin" to "villagers readable". **Fly to**: double-click a cell or click any chain link. **Follow**: F on a selected person/animal/tanker.
- Idle on the title screen: slow auto-rotate.
- **Picking priority** within a small screen radius: person > village > tanker/boat > animal > volcano > cell.
- The far side of the planet is invisible, so off-screen headline events show an **edge-of-screen indicator** (icon + arrow) that flies the camera there when clicked.

---

## 5. Screens & panels

**HUD (top)**: Year · season · speed | population | species icons with counts (grazers, predators, fish as trend arrows) | global temp anomaly | CO₂ | ice % | sea level. Each item clickable → Planet inspector.

**Power bar (bottom)**: 10 buttons with cooldown rings; +/− toggles for Weather and Temperature; species chooser for Cull/Introduce.

**Chronicle ticker (bottom edge)**: newspaper headlines scroll; click one → flies to it and opens its chain.

**Inspector (right, collapsible)**:
- *Cell*: biome, temperature, rain, soil water, fertility, grass/forest, animals, fish, oil, ice. Unusual values highlighted. **Why?**
- *Village*: name, population, food (months), farms, fishing, founded year, mood, recent events, residents list. **Why?**
- *Person*: little portrait (coloured icon), name, age, job, family (alive/dead), traits, hunger/health, a dialogue line (click **Talk** for another), memories. **Why?**
- *Planet*: era, g_temp, CO₂, sea level, ice, totals, and the blame ledger ("Who warmed the planet?": You 38% · Villagers 51% · Volcanoes 11%).

**Chain popup**: 03_CAUSALITY.md §5.

**Event log**: a filterable list (mine / nature / headlines), each opens a chain.

**Menu (☰)**: New world (random or typed seed; confirm overwrite), Save now, Settings, How to play, Credits.

**Settings**: music volume, SFX volume, pause on my consequences, tutorial hints, prop density (quality), reduce motion.

Readability: never rely on colour alone; every status has an icon and text.

---

## 6. First 60 seconds (onboarding)

1. **Title**: the (pre-spun, instantly loaded) planet spinning in a bright sky, the title, a generated god name ("Hello, Humble Thunder ✎"), a **Begin** button. If a save exists: **Continue** (shows "Your world kept turning…").
2. **Guided mischief** (skippable, hint bubbles, not modal walls):
   1. Camera flies to a fishing village with a tanker offshore. "This is Rivermouth. They love fish. That is a tanker. Try ⚡ on it."
   2. Tanker sinks, oil blooms. "Now speed up time: ▶▶▶."
   3. Within ~30 real seconds: oil spreads, fish collapse, famine; time pauses on the chain popup. "You did that. Everything here is connected. Try anything, or leave the world alone and see what it does without you."
3. The tutorial uses a fixed seed that is tested to produce this chain within 3 years (scenario `tutorial_chain`). After it, the player can keep playing that world or roll a new one.

---

## 7. Goals: "Divine Mischief" (Chapter 4)

An optional checklist in the menu, auto-detected from events and chains, with a toast on completion. Saved with the world.

1. **First Domino** — cause a chain of 3 links.
2. **Butterfly** — cause a chain of 7 links from one action.
3. **Hands Clean** — cause a famine without using any power within 3 cells of that village's farms.
4. **Wolf Whistle** — make a fishing village's catch collapse using only Cull/Introduce on land animals.
5. **Big Chill** — push the planet into an Ice Age.
6. **Rising Tide** — a village is flooded by rising seas.
7. **New Horizons** — a new village is founded because of something you did.
8. **Island Plague** — turn an island into desert using Introduce.
9. **Guardian** — keep humanity (≥ 50 people) alive for 500 years.
10. **Oops** — a single action reaches 100 consequences.

---

## 8. Saving, leaving and coming back

- **Autosave**: at most once every 30 real seconds, plus when the tab loses focus and on "Save now".
- **What a save is**: the world state at the most recent **year boundary** (the sim snaps its fields to save precision at every year boundary, so that state is exact) plus the commands issued since then and the current tick. Loading restores the snapshot and replays those commands up to the saved tick. Result: a reloaded world is bit-identical to one that never stopped. Details in 05_ARCHITECTURE.md §8.
- **Also saved**: recent command log (for counterfactuals), goals, settings, real-world timestamp, save version. "What if?" snapshots are session-only.
- **Where**: Wavedash cloud save when running on Wavedash; local storage otherwise (05_ARCHITECTURE.md §8).
- **Coming back**: elapsed real time → world years at **1 hour = 1 year**, capped at 1,000 years (20 before Chapter 3's epoch mode). Show the catch-up as a fast time-lapse of the planet with a progress bar, then a **"While you were away"** card: top 5 headlines by magnitude, plus any consequences still traceable to your old actions ("Your meteor in Year 40 is still causing trouble").
- **Save version mismatch**: tell the player kindly, start a new world.
