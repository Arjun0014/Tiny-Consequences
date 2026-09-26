# 01 — Vision

Working title: **Tiny Consequences**. Rename freely later; nothing in code should depend on the title.

## The pitch

A cute miniature planet that lives on its own: weather, oceans, forests, animals, farms and tiny villagers who are born, fall in love, starve and gossip. You are its god. Poke it: a lightning bolt, a drought, a missing wolf pack, a sunk tanker. Speed up time and watch one small act ripple through decades, sometimes millennia. Sometimes it's nothing. Sometimes it's the end of the world. The game always shows you exactly **why**, and exactly how much of it was **your fault**.

Jam: **Defold Game Jam 2026**. Theme: **CHAIN REACTION**. Engine: **Defold**. Target: browser (HTML5), on itch.io and on Wavedash.

## The player fantasy

"I am a slightly irresponsible god looking after a tiny, fragile, adorable world."

- The world does not wait for you. Leave it alone and it still changes: villages grow and fade, forests spread and burn, herds boom and crash, the climate slowly drifts.
- You can come back later. The world kept living while you were away (1 real hour = 1 world year), and it tells you what happened.
- Your interventions are small. Their consequences are not.
- You can always ask "why did this happen?" and get a chain of causes that ends at either **Nature** or **You**.

## Design pillars (in priority order)

1. **Legible consequences.** A chain reaction that the player can't see or trace doesn't exist. Every major change must be traceable through a readable chain of causes. When in doubt, spend effort on explaining rather than on adding more systems.
2. **A world that is genuinely interconnected.** No scripted "if X then catastrophe Y". Systems only read and write shared state. Chains emerge from those couplings. (See 02_WORLD_MODEL.md.)
3. **Alive but resilient.** Left alone, the world changes visibly but does not collapse within a human timescale. Collapse within ~10 years requires the god's push. On very long timescales (centuries, millennia) the world may drift into ice ages or hothouses by itself, and recover over deep time.
4. **Cute and dark.** Soft, toy-like visuals and a dark comedic voice. Villagers say funny, grim things. The tone makes the climate/butterfly-effect message land without preaching.
5. **Fast to delight.** A new player must cause a visible, explained chain reaction within the first 60 seconds.

## What "chain reaction" means in this game

- Small inputs, large outputs: a single power use can produce 5–15 downstream events.
- Visible propagation: consequences spread across the globe (fire fronts, oil slicks, famine migrations, colour changes of the terrain).
- Tipping points: gradual pressure followed by sudden shifts (a forest becomes grassland, a village is abandoned, the planet tips into an ice age).
- Traceability: every headline event carries a chain back to its root, displayed as linked icons with years.
- Counting: the game keeps score of your consequences ("Your lightning bolt in Year 3 has caused 41 consequences so far").

## Emotional beats we want players to report

- "I killed some wolves and 20 years later a fishing village on the other continent was gone."
- "I left it for a night and came back to an ice age."
- "The villager I clicked said 'Ever since the sky-fire the fish taste like coins' and I realised that was me."
- "I tried to fix it and made it worse."

## Anti-goals

- Not a realistic Earth simulator. Plausible and consistent beats accurate.
- Not a city builder. The player never manages villages directly.
- Not a menu-heavy strategy game. The planet is the interface.
- Not a scripted disaster showcase.

## Jam constraints

- Submission closes **4 Oct 2026, 23:59 CEST** (= 5 Oct, 03:29 IST). Internal target: **submitted by 4 Oct, 20:00 IST**.
- Solo developer (Arjun) directing Claude Code, full time.
- Rated by other participants in a browser, usually for 3–10 minutes each. Categories: Overall, Presentation, Game Design.
- Must also deploy to Wavedash for the Wavedash prize. The Wavedash SDK must be initialised or the game stays hidden behind its loading screen.
- AI use is allowed but must be described on the game page (see 08_ROADMAP.md, final stage).
- Tiny Skies (github.com/dannylimanseta/tinyskies) is a _visual reference only_. It has no licence: never copy its code or assets.

## What success looks like at submission

1. A player opens the page and sees a gorgeous tiny planet within, loading time can be as long as needed for initial load only.
2. Within 60 seconds they have caused and understood their first chain reaction.
3. Within 5 minutes they have discovered at least one consequence they didn't expect.
4. The untouched world, sped up for 100 years, looks meaningfully different but still alive.
5. It runs at 60 fps on a mid-range laptop and saves progress between visits.
