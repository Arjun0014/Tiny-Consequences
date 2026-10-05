# TC2-10 — Conflict and politics

Basis: TC1's primitives (`docs/v3/50_PRIMITIVES.md`) and R6 design (`docs/v3/60_R6_CONFLICT.md`), research in
`docs/v3/40_RESEARCH_CONFLICT_PLANET.md` and `docs/world/18_DEEP_WORLD.md` (Carneiro, Wright & Johnson,
Earle, Keeley), plus Turchin et al. 2013. Never: war flags, hostility or morale meters, scripted retaliation,
"raid count >= 2 -> palisade", polities as switches.

## 1. Primitives (shared with disasters)
Deeds known per settlement (who did what to whom, how it was learned); danger noticed by the senses; an
urgent choice with dependants first; injury; condition of things; boldness and arms owned. Grievance,
gratitude, reputation and fear are *read* from known deeds (with half-lives and closeness of the victim).

## 2. From theft to war
Taking from others is weighed by need or an unredressed wrong against risk, ties and boldness; owners
discover, track, claim, take back; raids are parties of real people who each decided to join; fights are
PROCESSES (positions, blows, injuries, flight) whether or not watched; the dead have deeds and mourners.

## 3. Defence that is built
Settlements weigh defences from remembered harm (deeds of raids, losses), labour, materials, knowledge
(digging, timber framing, masonry) and coordination (a leader who can call work): a ditch, a palisade,
a stronger wall, towers, a gatehouse, a keep - each a construction project from a modular kit laid on the
settlement's terrain and layout, with gates that are kept at night. What historians call a castle is what
remains when these accumulate.

## 4. Leadership and polities
- Prestige of household heads from known deeds (generosity in famine, success in war, age, kin network,
  founding); a settlement's leader emerges from prestige and the culture's succession customs.
- Alliances from kin ties, gifts and shared enemies; tribute after defeats where flight is hard
  (Carneiro's circumscription); a polity is a connected component of the tribute/allegiance graph; its
  depth (settlement -> centre -> paramount) is what historians call a chiefdom or a state.
- Military techniques diffuse like other knowledge; frontiers and rugged land shape where large polities
  arise (Turchin et al. 2013). Secession, conquest and collapse come from the graph and its strains.

## 5. Phase
F6. F0 has none.

## 6. The plan for F6 in TC2 (D2-069)
Every mechanism lives at both resolutions: a lived act (people walk, take, are seen, strike) and a COARSE day that makes the
same choices at the lived rate (measured, as visits and trade are). Long runs are COARSE: F6's done-when is judged there.
1. **Deeds and taking by need** (slice 1): deeds as records (`Core/Society`): who did what to whom, its worth; what each
   village knows of them and how (saw, found, told, own); news carried by visitors, traders, brides and movers. The hungry take
   grain from a neighbour's full store at night - seen or not by the homes round the store; relief given is a deed too.
   Grievance and gratitude are read from known deeds (worth, age, settled or not), never stored as scores: relief and trade
   pass by those who wronged a village; restraint toward kin and those who helped; taking back is not stealing to the taker.
2. **Watch, blows and injury**: a village that remembers losses keeps a watch at night (labour, sleep); a thief met is
   struck or chased; injury (hurt that heals with care, or kills); death by violence is a deed with mourners.
3. **Answers**: claims (return, pay in kind, deny, refuse), taking back as a choice, compensation; ties (kin across the line,
   trade, help received) press for settlement (Gluckman); feuds that end or grow from present state.
4. **Raids**: a household with need or grievance raises a band of kin and friends (each deciding), which walks together, takes
   beasts and stores, meets defenders; fights as processes; loot carried home.
5. **Defence that is built**: a ditch and a palisade from remembered harm against timber and labour (gates; never a count),
   later walls and towers from masonry; aggregation into bigger places for safety.
6. **Prestige and leaders**: prestige from known deeds (generosity in famine, raids led, age, kin, founding); a village's
   leader calls work (walls) and bands.
7. **Tribute and polities**: a village raided where flight is hard (circumscription) yields a share of its harvest to the
   stronger; a polity is a component of the tribute graph, its depth the historians' chiefdom or state; secession and collapse
   from its strains. Done when a fortified town or a polity arises and falls in long runs and its history explains it.

## 7. Implemented: slice 1, deeds and taking by need (D2-069)
- **Deeds** (`Core/Society/Deeds.cs`): a record of who did what to whom (the doer and their village; the household or village
  that suffered or gained; the good and its worth in FU; its history event). Each village keeps the deeds it knows and how
  (its own people's, seen, found with the doer unknown; told comes with news in slice 3). Grievance and gratitude are sums
  over them read when a choice needs them (harms halve every 2 years, help every 4); nothing is stored as a score. A deed
  whose weight has faded below half a day's food is forgotten by every village.
- **Taking by need** (`Core/Society/Conflict.cs`): a grown person whose last day fed them less than half their need may, going
  to bed, go in the dark to a neighbour's store holding more than a month of its own need and take a load of grain - by their
  boldness and how near they are to starving, held back by kin living there and help that village gave, freed by a wrong it
  did them. The homes round the store may wake: seen, the taker runs and leaves it and the village knows who it was; unseen,
  the load goes home to the household's larder and a store visibly lower finds the loss. A COARSE night makes the same choice.
- **What deeds change**: no trade between villages either of which holds a grudge against the other (more than 10 FU of known
  harm); no relief for those who wronged a village; restraint read by the next taker. Next: the watch, blows and injury.

## 8. Implemented: slice 2, the watch, blows and injury (D2-071)
- **Injury**: a person's hurt keeps them abed above 0.4 and off the roads above 0.25; it heals by the day, faster fed; deep
  wounds may kill, and a death of wounds is a killing, the striker's deed.
- **What follows the shout**: a seen taker meets whoever comes out (the watcher, or the boldest of the village); they run and
  may be caught, or turn on them; blows go by strength, the metal a village holds and the neighbours who come running. Each
  blow is a wound deed both villages know; a taker struck in the act weighs half to their own.
- **The watch**: a village that knows it lost grain to takers in the last year keeps a watch at its store when the losses
  outweigh a watcher's lost sleep; the watcher stands there until two in the morning; takers are seen far more often there.
- Measured: in famine-regulated worlds the watch halves the takings; ~9 deaths of wounds a year for 100,000 people.

## 9. Implemented: slice 3, answers, news and taking back (D2-072)
- **Claims and answers**: a village asks amends for blood its people suffered from known doers; the doers' village pays from its
  store when it can and is willing (the evidence, close kin across the line and help received press for it; what it holds
  against the claimants presses against it) and the wrong is made good, or it refuses (or denies hearsay): a new wrong.
- **News**: visits, trades and claims carry what each village saw done to it; a village known to beat takers keeps them off.
- **Taking back**: the fed and bold of a village holding a heavy wrong against a neighbour go to take from its store.
- Grievance and gratitude are ageing sums per pair of villages (state), read at once.
- Measured (400 years): about four claims in five refused; feuds are carried on by taking back - raids are the next slice.

