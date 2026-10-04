# V3-85 — The R7 acceptance audit: every god power in real play (session 24, D-188-D-204)

The playable game is the authority. Every case below was played in the real game in headless Chromium drawing on the
GPU (ANGLE D3D11) with `node scripts/play_accept.mjs --case NAME --tag T`: the world brought to a state it could reach,
the power cast through the game's own power path (the bar's), the scene shot before, at once and after (close and from
afar), the sim read at each moment (`game/platform/accept.lua`, bridge `lua`), and the News, the Why and "Your actions"
read as the player sees them. Full shots and `report.json` in `out/accept/<tag>/` (not committed); contact sheets in
`docs/v3/accept/`.

## 1. What was broken at the start of the session (reproduced in the browser first, `out/accept/before`)

| Power / system | Broken how | Why |
|---|---|---|
| Clouds | Zooming to a village under weather, the cloud hid >= 90% of the frame from zoom 0.80 to 1.35 (the "go there" zoom is 1.42) | the deck's base sat at the village view's own height, storms towered 1.4 above it |
| Meteor | Straight onto Tekheim's square: **0 killed**, 0 beasts; houses burnt down over hours by the fire; the planet glowed orange round it | it wrote planet heat, fire, dust and a quake - on the village map only fire; no crater, no blast, no flash |
| God's lightning | Aimed into a grazing flock: **one sheep** killed, the rest grazed on | one target per bolt, no current through the ground, no beast reacted to the blow |
| Fire (two scales) | A burning planet cell painted orange over a whole village map; the map's own fire and the planet's burned separately | planet cells and maps burned independently; the planet's fire drawn as a wash over the cell |
| Warm / cold air | Over a coast the sea went to 30 C (warm) or froze, -0.7 C (cold), within the hour | the air forcing wrote the sea surface temperature directly |
| Cull deer | Nine in ten of the deer about Hottoodingle Hop killed (237): its map showed **24 deer a day later, as before** | every map stood at the cap of 24 wild grazers (77-200 uncapped): a cull, a die-off never showed |
| Bring fish / the sea's fish | No fish were drawn anywhere; "Your actions" said 0 consequences | nothing drew the planet's fish field; Tekheim's sea lies two cells off, the chain looked at the cells next to a village |
| Stir the mountain | Nothing to see until it blew, days later; "0 consequences" | the pressure was invisible state |
| Blight | Not drawn on the village's field at all (only a news card) | the map's crop rows had no blight state drawn |
| Herds | A flock was a packed ball (nearest neighbour under a body length), walked out of its fold through the hurdles and milled | a fold was open ground to a beast; followers stepped back from the one ahead; making room stepped into the next beast |
| Fire's cause | A fire the meteor started on the planet came onto the map as "Fire in Hottoodingle Hop - NATURE, why: (none)" | a fire entering from the planet carried no cause |
| Consequences told | Hunger after the meteor: "NATURE"; rain that put a fire out: nowhere; a flood the rain spread: nowhere; fish thinned by a warm sea: no story | the stories and the chain had no link for them |

## 2. What changed (physically / visually) - D-188-D-204

- **Meteor** (D-188, `sim/agents/impact.lua`): one energy, laws of distance: a crater (everything in it gone), a blast by
  overpressure (people, beasts, buildings by their strength; trees thrown flat), a thermal flash (burns, fires by
  dryness), the ground's shaking; the same physics for the planet's cells. **Visual**: the rock falling, the flash and blast
  rings at their physical radii, the crater bowl and rim, scorched ground, ruins, thrown trunks, charred snags, camps for
  the roofless; from orbit the crater and the fire; off a map, a crater ring of rock on the planet (D-200).
- **God's lightning** (D-189): aimed (pull 0.025), a superbolt's current (4x), the current through the ground by stride
  (a cow's long stride takes more), fire by dryness, a scorch mark; every beast, wild animal and dog near it bolts.
  **Visual**: the bolt where it went in, scorch, the herd scattering, the dead lying.
- **One fire at two scales** (D-190): a planet cell a map owns burns by the map's fire; the planet's fire comes in as a
  front; **visual** (D-200): the planet burns in patches by its fire's intensity (not a wash), flames only where things burn.
- **Clouds** (D-193): the layer lifts as the eye comes down; the village clear under it from zoom 1.1.
- **Air over the sea** (D-198): the sea follows the air by its own heat (0.12 of the change), never set by it.
- **The map's wild herds** (D-196): one in five of the herds' animals on the map's cells - a cull or a brought herd shows.
- **Fish** (D-195): schools from `F.fish` - as many as the sea holds, the same field the fishers' nets draw on.
- **Stir** (D-198, D-200): grey steam over a mountain past a third of its threshold; ash after an eruption; "Pressure under
  Mount Fizzle: 58% of what makes it erupt" at once.
- **Blight** (D-201): rows drawn brown and withered as the blight takes its share.
- **Herds** (D-192, D-197): a fold passed only at its gate (shut behind the herd), round a fold never through it; followers
  after where the one ahead is going; room made from the whole press.
- **Scars** (D-191, D-203): ruins, charred snags, thrown trunks, crater, scorch (36 days), dead livestock, flood mud (24 days).
- **Told right** (D-198, D-201): fires carry their planet cause; a village's sea is two rings out; hunger traces to the harm
  that took the household's home and food; "The rain put out the fire at Tekheim"; "The flood at Little Leelee spread";
  "Fewer fish off Zurug".

## 3. Every power in its contexts (browser)

PASS = all six: hits where aimed, a clear physical change, an immediate visual response, what is exposed is affected,
real downstream consequences, told in News / Why / Your actions. The "now" readings are from the final builds.

| Power | Context | Expected at once | Observed (sim) | Observed (visual) | News / Why | Your actions | Verdict |
|---|---|---|---|---|---|---|---|
| Meteor | Tekheim's square (A) | crater, blast, flash, fires, deaths | 4-9 killed, 8 houses + 2 buildings down, 3 beasts, 62-95 fires, granary emptied, 8 households camp; hunger 2 days on | rock, flash rings, crater, scorch, ruins; from orbit the crater | "Fire from the sky at Tekheim" YOU; "Hunger at Tekheim - a house fell in <- rock <- YOUR meteor" | 19-22 consequences: the dead by name, beasts, fire, camps, quake | PASS |
| Meteor | forest by Hottoodingle Hop | trees flattened and lit | 57 trees thrown flat, 149 alight, 10 hurt | thrown trunks, snags, crater rim | "57 trees thrown flat, 149 alight" YOU | 6-7: fire, quake | PASS |
| Meteor | open land off a map | planet fire and crater | the cell burns ~2 days; quake 6.2 | patches of fire on the planet, crater rim of rock | "Fire from the sky"; the map's fire "YOU <- YOUR meteor" (was NATURE) | 6: fire on the land, quake, dust | PASS |
| Meteor | the sea off Tekheim | a wave | the sea over the shore, 1 drowned, salt pan fell in | wave | "The sea came in at Tekheim" YOU | 4-5: wave, quake, dust | PASS |
| Lightning | a grazing flock (B) | the beast struck, current, panic | 6 dead (5 by the ground current), the flock scattered | bolt, scorch, flock running | "Lightning killed 6 beasts at Tekheim" YOU | "The bolt killed 6 beasts (5 by the current)" | PASS |
| Lightning | cattle | a cow | a cow killed | as above | "Lightning killed a cow at Tekheim" YOU | 1 | PASS |
| Lightning | a dry tree by a house | the tree burns | the tree alone burnt (4 h) | burning, then a snag | "Fire in Tekheim" YOU | bolt -> fire | PASS |
| Lightning | a house | the roof burns | the house alight; put out by neighbours (or burnt, household lodged) | fire on the roof, ruin | "A house caught fire: the neighbours put it out" YOU | bolt -> fire -> lodging | PASS |
| Lightning | deer, wolf, dog, person, dry field, wet ground | the target struck | see section 5 (matrix2) | | | | see 5 |
| Rain | a burning wood (D) | rain on the fire | out in 2 h (8 h untouched); the planet cell 0.31 -> 0.01 | rain, flames dying | "Fire in Tekheim - The rain put the fire out" | "The rain put out the fire at Tekheim", river high, flood, soil watered | PASS |
| Rain | a drought (Hottoodingle Hop) | soil wetter, rivers rise | soil 0.08 -> 0.18, river x6.1, a flash flood | rain, the river wider | "Flood at Hottoodingle Hop" YOU | soil, river, flood | PASS |
| Rain | a soaked floodplain (E) | runoff, the river up | river x16, flood 461 -> 3,040 tiles | rain, the flood wide over the land | the flood (nature's, older) | river x16; "The flood at Little Leelee spread" (new) | PASS |
| Dry spell | Little Leelee | soil drying, warmer | soil 0.46 -> 0.33, +2.4 C, its flood gone | the ground browner (slow) | at the cast only | air warmer, soil dried | PASS (slow by nature) |
| Cold air | Krikeivorn (15 C); Hottoodingle Hop at night (13.6 C) | 8 C colder | air 6.9 / 5.6 C; the sea 15.0 -> 14.5 (was -0.7) | nothing beyond the news card (no frost above freezing - physics) | "Cold air over Hottoodingle Hop - 8 degrees colder for 4 days" YOU | air colder | PASS (told, not seen) |
| Warm air | Krikeivorn; Tekheim under snow (cold snap, -0.9 C) | a warm air mass, 8 C above the season | 21.3 C; the sea 15.0 (was 30.7); on snow: melted in 12 h, the river x4.8 | the snow gone, white to green | "Warm air over Tekheim - 8 degrees above the season" YOU | air -> "The snow at Tekheim melted"; the river high | PASS |
| Stir | Mount Fizzle (I) | pressure up | 58% at once; twice: erupted within a day (VEI 1), a quake 3.6 | grey steam at once; dark ash after | "Mount Fizzle stirred"; "Mount Fizzle erupted" YOU | pressure node, eruption, quake | PASS |
| Strain | the fault under Tekheim (I) | stress up, a quake | magnitude 5.6 at Tekheim | the shake | "Quake at Tekheim" YOU | fault gave way -> ground shook | PASS |
| Cull | deer about Hottoodingle Hop | 9 in 10 killed | 236 killed; the map 28 -> 7 -> 4 (was 24 again a day later) | fewer deer, carcasses | "21 boar dead about Hottoodingle Hop" | herds thinned, the dead | PASS |
| Cull | wolves | 9 in 10 | 9 killed, 14 left | fewer wolves | at the cast | packs thinned | PASS |
| Bring | wolves to Tekheim | a pack | 21 wolves about; the wild herds' numbers answer | wolves on the map | at the cast | packs, herds | PASS |
| Bring | fish off Tekheim | a shoal | fish 0.75 -> 1.00 | more schools in the sea | at the cast | "A shoal brought off Tekheim" (was 0) | PASS |
| Sickness | Tekheim's flock | a few beasts carry it | 13 carrying it; 9 sick + 12 incubating by day 6, 1 dead | sick beasts lying | at the cast; deaths | "13 sheep carry the sickness" | PASS |
| Blight | a growing field (H) | the field spotted | worst 0.12 -> 0.27 over 10 days | rows withering brown (new) | "Blight in the corn at Tekheim" YOU | blight in a field (27% taken) | PASS |
| Warm sea | off Zurug (G) | the sea warmer | 28 -> 32 C; its fish 79% of before | fewer schools over days | at the cast; "Fewer fish off Zurug" (new) | sea warmer -> fish fewer | PASS |
| Oil | off Zurug (F) | a slick | oil 1.0; the shore 22.5 -> 1.3; 10 gulls oiled; fish 0.73 -> 0.61 | a dark slick over the bay | "Oil on the coast at Zurug - 10 gulls oiled" YOU | slick -> fish fewer, shore fouled, gulls | PASS |

## 4. The play matrix A-I (contact sheets in `docs/v3/accept/`; also `bolt_targets.jpg`, `bolt_fields.jpg`, `warm_on_snow.jpg`,
`herd_move.jpg`, `herd_close.jpg`, `fish.jpg`)

A meteor on a village (`A_meteor_village.jpg`, `A_meteor_planet.jpg`), B a bolt into a flock (`B_lightning_herd.jpg`),
C fire beside a flock (`C_fire_herd.jpg`: mean distance 0.20 -> 0.40 -> 0.53 in 18 min, none within 0.2 after, back when
it is out), D rain on a fire (`D_rain_fire.jpg`), E rain on a soaked floodplain (`E_rain_soaked.jpg`), F oil by a fishing
village (`F_oil_shore.jpg`), G warm sea (`G_sea_warm.jpg`), H blight (`H_blight.jpg`), I a volcano and a quake
(`I_volcano.jpg`, `I_quake.jpg`).

## 5. The god's bolt on each kind of target (`out/accept/matrix2`, aimed with `accept.aim_target`: a target the aimed bolt takes itself)

| Target | Preview | Observed | News / Why | Your actions | Verdict |
|---|---|---|---|---|---|
| A sheep in a grazing flock | "The bolt would take ..." | 6 dead (5 by the current in the ground), the flock scattered running | "Lightning killed 6 beasts at Tekheim" YOU | "The bolt killed 6 beasts (5 by the current)" | PASS |
| A cow | "one of the Stursons' cattle" | the cow killed | "Lightning killed a cow at Tekheim - Lost by the Stursons" YOU | 1 node | PASS |
| A wild deer (Hottoodingle Hop) | "a wild deer" | killed where it stood | "Lightning killed a deer at Hottoodingle Hop - A wild one, dead where it stood" YOU | 1 node | PASS |
| A wolf (a pack brought first) | "a wild wolf" | 3 wolves dead (2 by the current) | "Lightning killed 3 beasts at Tekheim" YOU | "The bolt killed 3 beasts (2 by the current)" | PASS |
| A dog | "a dog" | the dog killed | "Lightning killed a dog at Tekheim - Lost by the Tustrands" YOU | 1 node | PASS |
| A person out of doors | "Kirnor Tustrand, out of doors" | Kirnor killed | "Lightning struck Kirnor at Tekheim" YOU | "The bolt struck someone" -> "Kirnor died (lightning)" | PASS |
| A dry tree by a house | "a tree" | the tree burnt alone (4 h) | "Fire in Tekheim" YOU | bolt -> fire | PASS |
| The same tree on soaked ground | "a tree" | struck, no fire | "Lightning struck a tree at Tekheim" YOU | 1 node | PASS |
| A house | "the Tustrand house" | the roof alight; put out by neighbours, or burnt (household lodged) | "A house caught fire" YOU | bolt -> fire -> lodging | PASS |
| A green growing field | "the bare ground" | the ground struck; green corn does not burn | told | 1 node | PASS (physics) |
| Dry standing corn and stooks in a drought (Krikeivorn) | "the bare ground" | the field alight, burnt in an hour, 10 sheaves lost (lights about 2 times in 3 at this dryness: 12 of 20 headless) | "Fire in Krikeivorn" YOU | bolt -> "a field caught fire; a field burnt" | PASS |

The god's bolt never killed half a village: its lethal reach is its current's (`strike_gc x 4 x stride`), a few strides.

## 6. Herds and fish

- Herd diagnostic (`--case herd_move|herd_close`, `out/s24/probe_herdmove.lua`, `probe_decisions.lua`): see D-197. Grazing
  close in: nearest neighbour 0.031-0.036 (spacing 0.034; before 0.010-0.019), spread 0.115-0.137, 1-4 groups; the flock goes
  out at its gate; decisions an hour 470 (S23 437).
- Fish (`--case fish_look`): 13 schools over 5 sea cells off Tekheim at fish 0.75; a brought shoal shows more; a warm sea or
  oil fewer (the fishers' catch reads the same `F.fish`).

## 7. Known gaps

- The planet's crater is small, as its physics says (`imp_crater` 0.075); the large mark is the flattened, burnt ground.
- A steam plume seen from straight above reads as a grey cloud on the summit, not a column.
- Cold or warm air at mild temperatures changes nothing you can see (no frost at 7 C) - told in the news and the chain only.
- A flood's mud is kept for floods that begin after this session's code (a flood under way in an old save has no footprint).
