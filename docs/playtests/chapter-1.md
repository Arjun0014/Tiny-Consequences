# Playtest 1 — A World That Lives Without You
Build: c487556 (code; the report commit after it changes docs only), 27 Sep 2026, ~14:45 IST
How to run: `npm run play` (release build, opens http://localhost:8080). Chrome or Edge recommended.

## What's new
- **A planet that runs by itself.** Weather with wet and dry years, rivers that flood, droughts, grass and forest, wildfires started by lightning, deer and wolves in predator–prey cycles, fish and plankton, and a slow human-made warming (CO₂ 280 → ~340 ppm and about +0.8 °C by Year 50).
- **Villages of individual people.** Every villager has a name, age, job, partner, children, health and mood. Villages farm, fish, hunt and burn wood. They starve in bad years, send colonists to found new villages when they grow big, and are abandoned when they fail. Southern-hemisphere villages farm in their own spring.
- **Every event has a cause, recorded underneath.** Flood → village flooded; lightning → fire → wildfire → deer boom on the regrowth; burned or failed harvest → famine → migration. You can't open chains in the UI yet (that is Chapter 2's chain popup and "Why?"), but the events are real.
- **The default world** (seed 27) is pre-spun, so it loads in a few seconds. It opens on its biggest village: two farming and fishing villages on a forested coast, with a tanker offshore.
- **Controls:** drag to rotate, mouse wheel to zoom, click something to inspect it, double-click to fly to it, **F** to follow the selection, **Esc** to deselect. **Space** pauses; **1–4** (or the top-left buttons) set the speed: ▶ normal, ▶▶ fast, ▶▶▶ faster, ⏭ max. **F3** toggles the performance overlay.

## Try this (10–20 minutes)
1. Let it load and watch the opening view at ▶ for a minute. Zoom in on a village until you can see the villagers walking between the houses, and click one.
2. Click a village. Read its card (population, food in months, farms, boats), then click a resident's name.
3. Click the population number in the top bar to open the planet card (temperature, CO₂, who changed the air).
4. Press **4** (max speed) and let about 50–100 years pass. Watch the forests, the ice caps and the HUD numbers. Look out for dark wildfire scars, new villages appearing, and ruins where a village died.
5. Rotate to the other continents: the icy north with its cold steppe, and the third continent. Click a desert, a forest and an ice cap to compare them.
6. Press **F3** at ⏭ max speed and note the fps and "sim ms".
7. Leave it running at ▶▶▶ for a few minutes while you do something else, then come back and look around.

## Questions for you
1. Alive? Left alone for 50–100 years, does the planet feel like it is living (fires, floods, villages growing, spreading or failing), or does it feel static?
2. Pretty? Does it match the look you wanted (ART_DIRECTION.md)? Is anything ugly, muddy or distracting? The cold tan steppe on the northern continent is one candidate.
3. Readable? At mid zoom, can you tell villages, farms, forests, deserts and ice apart at a glance? At close zoom, are the villagers readable, and are they too big or too small?
4. Smooth? On your machine, what fps do you see at ▶ and at ⏭ (F3 overlay)? Does anything stutter while forests change at high speed?
5. Speed: at ⏭ max, does time pass at a satisfying rate (years per second), and do ▶▶ / ▶▶▶ feel like useful steps?
6. The inspector: is the information useful, and is there too much or too little? What would you want to know about a villager?
7. Before Chapter 2 adds the chain popup: is there anything that happened on the planet where you wanted to know *why* but couldn't find out?

## Known issues (don't report these)
- No player powers, chain popup, "Why?" button, chronicle ticker, sound or saving yet. Those arrive in Chapter 2.
- Headline events are recorded but not shown on screen (Chapter 2: chain popups and chronicle). In the meantime, the planet card's "Who changed the air?" is the only causal view.
- The north pole region of the default world is a big cold steppe (tan, with rocks and a few trees). That is geography, not a bug; tell me if it looks bad.
- The HUD's trend arrows are simple up/down/flat markers.
- In automated (software-rendered) browsers the game runs at 10–30 fps. Real GPU performance is unknown until you try it.

## Numbers
- Sim tick (release build, the default world, ticked synchronously in the browser): **1.9 ms median, p95 3.3–4.0 ms** (budget 4 / 8 ms).
- Tests: `npm run test:sim -- --full` 67/67 (S01 baseline stability 7/10 seeds with village turnover 10/10, S02 wolves removed 9/10, S03 farm fire → famine 8/10, S04 tanker oil 9/10, save/replay bit-identical, 500-year range check, worldgen 20/20). `npm run test:web` 8/8 on debug and release builds. W06 shows the browser and the headless test runtime produce identical state hashes for the same world and actions.
- 10 seeds × 200 years untouched: population stays in 193–355 (target 120–400); forest 19–37% of natural land; desert 8–25%; ice 11–15%; +1.4 to +1.65 °C of human warming by Year 200; 0.17–0.45 headline events per year; 1–4 villages founded and 0–2 abandoned per world.
- Draw calls ≈ 45–60 at planet view; ~9–11k props (trees, fields, houses) baked into 80 chunks; release bundle 3.1 MB.

## Screenshots
- `out/shots/latest/hud_1280x720.png`: the opening view with the planet card.
- `out/shots/latest/y000_village.png` and `y000_close.png`: villages and farms at mid and close zoom (villagers among the houses).
- `out/shots/latest/inspector_person.png` and `inspector_village.png`: the inspector.
- `out/shots/latest/y000_continent_b.png` → `y050_continent_b.png`: the same view after 50 untouched years (wildfire scar, forest spread, CO₂ 341 ppm, +0.8 °C, +11 m sea level).
- `out/shots/latest/y000_pole.png`: the northern ice cap and steppe. The orange ice-coast artifact is fixed.
- Regenerate with `npm run shots -- --release`.
