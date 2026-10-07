# TC2-00 — Vision: Tiny Consequences 2.0

> **Standing direction (D2-089, 7 Oct 2026)**: TC2 is an *autonomous medieval civilisation inside a living miniature planet* - the
> system breadth and readability of a game like Age of Empires, with no player giving orders: the agents themselves play the
> civilisation game, and every building, road, army, ship, mine, wall, workshop and kingdom arises from simulated need, knowledge,
> resources, labour, politics and history. Fewer, deeper people; richer settlements; more visible civilisation. See
> `20_CIVILISATION_DEPTH.md` and `25_CIVILISATION_ROADMAP.md`. The planet itself remains half the game.

## The promise (from the player's chair)

I see a beautiful tiny world turning in space: oceans, weather, forests, ice at the poles, the glow of
settlements at night. I zoom in and find real people living real simulated lives - a woman carrying
water, a boy herding goats, builders on a half-finished wall. I zoom out and speed up; decades pass and
the map changes: villages grow into towns, forests retreat, a coast floods, an empire forms and breaks.
I close the game. I come back the next day: a century has passed, simulated while I was away. The world
remembers. I open its **History Book**: every story in it actually happened. I click an event and see
what caused it, and I can still find its consequences in the world in front of me - the ruins, the
names, the people descended from the survivors.

I may intervene as a god - a bolt, a drought, a plague, a meteor - but the world never exists to react
to me. It lives on its own.

## What it is

A **miniature living planet**: a self-consistent world with its own physical and biological laws,
fast enough that hundreds to thousands of years of history (climate eras, tectonic change, evolution,
the rise and fall of civilisations) unfold within a player's attention, and detailed enough that any
moment of it can be watched at the scale of one person's day.

## Pillars (in priority order)

1. **Real history, never scripted.** Nothing happens because a story needed it. Castles, kingdoms,
   famines, ages and extinctions emerge from world rules and are recognised afterwards, the way
   historians recognise them.
2. **One world, many scales of time.** The same truth is simulated at the resolution each moment needs:
   seconds for a fire in a street, days for a harvest, centuries for a coastline. Watching never changes
   what happens; leaving never stops it.
3. **Every important change is explainable.** Every historical event carries its causes; WHY, WHAT IS
   HAPPENING, WHAT DID I CAUSE and WHAT HAPPENED are views over one causal record ending at NATURE,
   PEOPLE or YOU.
4. **The visible action is the simulation.** When you watch, what people do is what the world does: the
   wall rises because builders carried stone. No decorative agency.
5. **Beautiful and readable at every zoom.** A charming diorama from orbit, believable life up close.
6. **Persistence is a feature.** The same world lives for months of real time; it ages while you are away
   by an amount you choose; it is never silently reset.

## What a long history may contain (capabilities, not scripts)

Forests appearing and vanishing; rivers moving; droughts, floods, ice ages and hothouses; coastlines that
change; erosion, uplift, volcanic eras, quakes, drifting plates, islands born and drowned; booms, crashes,
predator-prey cycles, adaptation, speciation, extinction and recovery; foragers, farmers, herders and
fishers; settlements that grow, specialise, trade, fortify, federate, conquer, collapse and are reused as
ruins; plagues and famines; knowledge that spreads, is improved, and is forgotten; families, inheritance,
friendships, rivalries, reputations, leaders, alliances, polities, wars; the end of the human age and
the planet continuing without people, perhaps until new life fills the gap.

## Anti-goals

- Not Earth. Plausible and internally consistent beats accurate.
- Not a city builder or a strategy game: the player never commands people.
- Not a spreadsheet: the planet is the interface; numbers serve the picture.
- Not a disaster showcase: catastrophe has to come from the world's own state (or from you).
- Not protective: civilisation may fail, and the world keeps going.

## Success criteria for the foundation (session 1)

A small generated planet with persistent agents; scheduled movement; a hazard that interrupts a
traveller; normal, accelerated and headless catch-up simulation; structured history with causes;
save, load and continuation that equal an uninterrupted run; measured costs with headroom; a Unity Web
build that shows it.

Long-term success: the five-minute experience above, true in every claim.
