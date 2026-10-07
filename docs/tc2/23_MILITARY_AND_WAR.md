# TC2-23 — Force: fighters, arms, fortification, war and sieges

Part of the civilisation-depth direction (`20`, D2-089); builds on `10` (F6: deeds, taking, the watch, blows, injury, claims,
raids, the palisade, tribute). Never: spawned units, war flags, morale meters, "attackerPower > defenderPower", scripted retaliation,
castle ages, unlock buildings.

## 1. Every fighter is a persistent person

A fighter is a person with a body (health, age, wounds), a **fighting skill** (from practice and from fights survived), the **arms and
armour** they hold, their boldness, their kin and their loyalties. There are no units: a band, a guard, a levy, a host is a list of
people who each chose (or were obliged, C10) to be there. Veterans carry experience and can die with it - a village whose seasoned
fighters died in one bad raid is weaker for a generation.

Strength in a blow (extends `Conflict.Strength`, D2-071): health x years x (1 - hurt) x (1 + 0.6 x skill) x arms (none 1, a weapon
1.3, metal arms and a shield 1.5) x armour (later: helmet, mail), with ground (home ground, walls) and leadership (a leader present
holds a side longer before it breaks) applied to the side.

## 2. Arms and armour are goods

| Item | Made from | Made by, where | Effect |
|---|---|---|---|
| spear, axe, knife | wood + 0.5-1.5 kg metal | a smith at the smithy (C1.4) | arms 1.3 |
| shield | wood + a hide | anyone at home (making, D2-058) once arms are kept | + a share of blows turned |
| bow and arrows | wood, sinew; a bowyer's skill | at home (later) | strikes before the close fight |
| sword | 1-1.5 kg of good metal, a smith's high skill | the smithy | arms 1.5 |
| helmet, mail | 1.5 kg, ~10 kg metal | the smithy (iron, C5) | armour |

Arms are kept by the village (its store) and given to its fighters when a band forms or the village rallies (C1.4); later by
households and retainers. Arms wear and are lost with the fallen (taken by the victors).
**Implemented (C1.4, D2-093)**: `Settlement.Arms` - spear and axe heads forged only at a smithy (`Core/Places/Forge.cs`), a kilo of metal and
two hours each, from smelted ore or reforged tools, by a village that fears its neighbours (a palisade, a heavy wrong held, a watch kept),
up to 0.35 a head, lost at 3 % a year; `Conflict.Strength` x (1 + 0.3 x the share of its fighters armed). Not yet: arms carried by people,
lost with the fallen and taken as loot; shields, bows, armour.

## 3. Roles that emerge (only as economy and politics sustain them)

- **Militia**: every fit grown person who comes out when the village rallies (F6).
- **The watch** (D2-071): a night's labour at the store.
- **The trained**: people who practise at a training ground (C1.5).
- **Guards**: a watch kept by day at a gate or tower (C7).
- **Retainers**: fighters fed by a leader's household from tribute and harvest (C10) - possible only where surplus flows to a leader.
- **Mounted fighters**: need horses (no horse kind yet - added with livestock when the ecology has them), riding skill, fodder.
- **Professional soldiers**: fed by a polity's stores in exchange for service (C10) - the costliest.
Labels are read from what people do; nothing is assigned.

## 4. Training (C1.5)

A settlement under persistent threat (remembered harm from bands, its own feuds) or whose people raise bands lays out a **training
ground** (`21` §2: a cleared ground with posts and marks, outside the homes). Its fit people of 16-45 who choose to (boldness, the
threat, their kin in danger) give a few hours a week there; the most skilled present lifts the others' learning (a trainer); skill
grows with hours toward a ceiling and fades slowly without practice and with age; a fight survived teaches more than months of
practice. Training costs real hours - off the fields, the woods and the wild - so a village that trains eats into its harvest.
Barracks (living quarters for retainers), armouries (arms kept in one place) and mustering grounds come with C8/C10.

**Implemented (C1.5, D2-094)**: `Person.SkFight`, `Person.Fights`; `Core/Places/Training.cs` - a fearing village's training ground beyond its
homes; the willing (16-45, fit, bold) practise two hours every third day; 0.004 an hour of what is left, lifted by the village's best; a blow
given or taken teaches 0.04; fades 5 % a year unused; strength x (1 + 0.6 skill). Seed 42 at year 300: 16 grounds, ~0.5 % of all labour,
a third of the living with some skill.

## 5. The cost of service

Every hour of training, watch or campaign is an hour not farmed, cut, built or herded. A campaign's fighters are fed from the store
they leave or the food they carry. A 500-person society cannot keep 400 full-time fighters: its granary and its fields say so.
Measured in runs: the share of a village's hours spent on force, against its harvests and its hungry seasons.

## 6. From grievance to war (escalation from state)

personal grievance -> taking -> raid -> repeated raids -> retaliation (F6, D2-069-D2-075) -> **organised campaign** (C8): a leader or
lord (C10) raises a force from several villages (its own people who choose to go; tributaries who owe men), with supplies, for a
reason in state (a wrong held, a refused tribute, a deposit or valley wanted, a rival's growth). Peace is equally reachable: amends,
tribute accepted, marriages across the line, trade worth keeping, a common enemy, distance.

## 7. A campaign is logistics (C8)

A force needs people, arms, food (~2 FU a fighter a day, ~1.5 kg of grain), a route, leadership and time. Each fighter carries ~25 kg:
about twelve days of their own food; pack animals and carts carry more and slow the march; foraging the enemy's fields feeds a force and
starves its people. It walks as scheduled movement over the real routes (interrupted by contact, weather, rivers). On contact: a battle
in the field if the defenders come out; an assault on walls; or a blockade. A force can raid farms, take stores, burn structures and
fields, capture a place (its households flee, submit, or are driven out), retreat, suffer casualties, starve, split or surrender.
Battles are resolved by the individual exchange of F6 (rounds of blows by numbers, strength, ground) extended with arms, skill,
leadership, terrain (walls, river crossings), condition (hunger, fatigue); above a few hundred fighters by groups of the same people.
Visible battles elaborate the real result. Wars cause real death, burnt fields, famine after armies consume supplies, refugees,
depopulated regions and abandoned villages.

## 8. Fortification progression (C7; D2-074's palisade is the first form)

ditch -> earth bank -> wooden palisade -> reinforced timber wall -> stone curtain -> towers -> gates and gatehouses -> fortified inner
compound -> keep/citadel, each a construction project of real labour and material, chosen by remembered danger, wealth, manpower,
timber and stone at hand, masonry known, siege experience, political organisation and the place's importance (a lord's centre, a
deposit, a crossing). No fixed castle age; regions fortify differently because their ground and history differ. Walls physically ring
the parts of a place worth guarding (homes and stores; later the market); as a town grows past its wall a larger ring may rise and the
old one remains inside. A wall has state per stretch (condition, breach) and gates that are shut at night or under threat; towers add
sight and ground. Stone is expensive (~400 loads a metre of curtain): stone walls belong to places with surplus, labour and authority.

**Implemented (C7.1, D2-098)**: rings that grow - a walled village whose homes have spread beyond its ring (four or more, a fifth of its homes)
raises a larger one when its losses still outweigh the cost; the old ring stays inside, unmended, guarding while the new one rises, until it
rots; palisades go before hut upgrades. Seed 42 at year 300: 18 walled villages, 5 larger rings. Not yet: gates as structures, ditches and
banks, stronger timber, stone curtains, towers.

## 9. Sieges (C8, after the logistics are convincing)

A siege is time against stores: the defenders' granary and well against the besiegers' carried food and foraging; assaults on gates
and walls (ladders need carpentry; losses on walls three times the open field); fire in thatch; starvation; a relief force from a
lord or ally. Siege engines (rams, then more) only when carpentry and leadership make them possible.
