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

## 10. Implemented: slice 4, raids (D2-073)
- A village holding a heavy wrong against its foe, or starving beside a full store, may raise a band: its boldest leads and each
  fit grown person decides to go (boldness, kin of the leader, never against their own kin); a band dares only what it expects
  to win. Lived, its people walk there together in the night; COARSE, it falls at once.
- At the store: unseen, a load each; seen, the village rallies round by round and blows go by numbers, strength and ground until
  one side breaks. Wounds are deeds; the raid is a deed; the loot answers the wrongs the band went for.
- Measured: large villages are safe from a lone band; small ones are raided (seed 7: 161 raids in 400 years, 44 % beaten off).

## 11. Implemented: slice 5, the palisade (D2-074)
- A village weighs a palisade from what other villages' people cost it these three years (losses found count) against the labour
  and timber of a ring round its homes; when the losses outweigh it, it builds one as a village work. A sound palisade sees
  takers and doubles its defenders' ground; bands weigh it. Unmended timber rots in thirty years and falls; it is mended while
  what it guards is worth it. The view draws the ring of posts with gates on the roads.
- Measured (400 years): walls rise where takings and raids are remembered and keep bands away (seed 42 raids 18 -> 6).

## 12. Implemented: slice 6, tribute and lordship (D2-075), and the close of F6 (D2-076)
- A village beaten by a stronger neighbour's band, with no good land known to go to, bows: a tenth of each harvest to the
  raiders, who raise no band against their tributary. It pays until its lord is gone or no longer the stronger; then the
  refusal is a wrong the lord holds. A lordship is the villages whose lords lead to it (tributaries' tributaries included).
- F6 closed: fortified places and lordships arise and fall in 1,000-year runs, their history traced from scarcity through
  taking, blows, refused amends, bands and raids. Carried forward: leaders, alliances, captives, gates and stone walls, multi-
  village bands, beasts as loot, the violence rate, the view of lordships and bands.


## 12b. Implemented: the lord's call and the lord's answer (C10.1: D2-107, D2-108)
- **The call**: a lord's band against those who wronged it is joined by fit grown people of its direct tributaries within a day's walk
  of the foe (owed: a quarter even of the meek, more of the bold; never against their kin), up to 36 in all; each goes home with their own
  share.
- **The answer**: a lord weighs the heaviest wrong each tributary holds at half - a wrong moves it only when twice as heavy as would move
  the tributary itself - and its band goes for the tributary ("they answer the wrongs it did Heiby, which pays them tribute"). What the
  band carries home answers the tributary's wrong, so a wrong is avenged once; the lord's own watch, walls and taking back read only what
  was done to it. Ten seeds: no change in people; lords answered three times in 3,000 world-years, as most tributaries answer first.

## 12c. Implemented: standing and the village's first household (C10.2: D2-127)
Built on C3.3 (D2-126: household grain, loans recorded as debts, gifts). `Core/Society/Leaders.cs`.
- **A household's following**: the village's households bound to it - its close kin households (parents', children's, siblings'), the
  households that owe it grain, and those it fed in hard times or lent to in the last eight years (Sahlins' big man, whose gifts and loans
  make followers; the elders' standing of kin; a patron-client tie outlives the debt - without it the season's debts, repaid at each
  harvest, made the following swing and the place change hands every four to fifteen years, 83 % of the time to another household). Read
  from the debts table, the kin links and `Household.Patron`/`PatronAt` (the household it last turned to); never stored.
- **The first household and the headman**: weighed every year after the harvest and when the place falls vacant (a death, a move): the
  household with the largest following (ties: more grain, then the lower id) is the village's first; its eldest grown member (18+, able)
  is first among the people (`Settlement.Head`, `HeadHh`, `HeadSince`, `HeadEvent`). An incumbent household keeps the place while its
  following is at least half the largest (`Hold` 0.5: a rival must be followed by more than twice as many; village headmen mostly held the
  place for life); at a death the place passes within the household while it still qualifies - standing
  that is inherited (claims pass to heirs) makes lines of headmen. History: `HeadmanRose` (the first; within the household after the dead;
  another household rose).
- **What it does**: in hard times (C3.3c) its grain opens to the village's hungry down to the village's ration, after their kin and
  before anyone's loans - a gift; those it fed follow it (`Holdings.Draw`). **Tribute** paid to a lord village is its first household's
  grain (the lord's own store), not the village's common grain (`Conflict.Harvested`): what a lordship takes makes its first household rich.
- Pages: the settlement page's overview ("Its first household: the Xs, followed by N households (K of kin, D owing them grain, F fed by
  them in hard times); first among its people Name, age, since year Y"), the person page ("First among the people of V since year Y").
  Headless: `leaders:` line, `--nohead` (reproduces `_h5` exactly), `--notribhead` (headmen without the tribute rule).
- Research grounding (session 16): big men rarely hold more than ~80 core followers and their successions are contested (Henrich, Chudek &
  Boyd 2015); villages split at ~127-158 (Alberti 2014, Hutterite fission data) or ~200 people (Chagnon) unless integrated, with elites and
  formal ritual above ~500 (Bandy 2004) - the basis for C10.3, fission along rival followings.

## 12d. Implemented: a contested succession splits a village (C10.3: D2-128)
- **Measured first** (`place --binding`, the `_l3` saves at years 100/200/300): the share of a village's households bound to its first household
  (it and its following) falls with size - median 43 % under 100 people, 24 % at 100-200, 20 % at 200-400, 8 % at 400+ - and the largest
  following among the unbound reaches 34 households (p90) and up to 90 in towns: scalar stress (Johnson 1982), the condition of fission.
- **The trigger is an event, not a rate**: at a vacancy (the first among the people died or left), once the new first is weighed, the household
  with the largest following among those not bound to it - counting itself and only its unbound followers, none of them setting out - will not
  follow when that following is at least half the first's (`Hold`) and a village's worth (6 households, 20 people): it leads them away to found
  a village at the best place the village knows (`Migration.Split`: a party with the split's record as its cause; households with a birth near
  stay). History: `VillageSplit` (92), its cause the death. Sahlins: conflict at a big man's passing; villages fission
  past ~130-200 people unless their people are bound together (Alberti 2014; Chagnon).
- **No place known: the faction looks for land** (`Settlement.SplitHh/SplitCause/SplitSince`): the village sends scouts for it as for land
  pressure; it is weighed again the day a place is known and goes if it still qualifies; after three years (`SplitYears`) it gives up and stays.
  (The first version split only to a place already known: 10 splits in ten worlds - 49 of ~50 qualifying contests on seed 6 had no place.)
- **Pages**: a village founded by a split says why its founders left; the village left says when it split and where they went; its history tab
  carries its headmen and splits (`HistoryLedger.PlaceEventsOf`, a page-only index). Headless `place --splits`, the `contests:` line.
- **Result (D2-128, ten seeds against `--nosplit`)**: 42 splits (0-12 a world), 37 of them in the first two centuries; factions of 6-16 households
  (median 7, 27 people). Land decides, not the thresholds: foundings 123 -> 119 - splits take the places land-pressure parties would have taken
  (35 % of foundings) - and once land is full factions look and give up (64 of 99 on seeds 4 and 6). People, vital rates and conflict at par; 2 of
  91 raids between villages a split made kin. Villages stay smaller while land is open (more people in villages too small for a weaver's shed: home
  making x1.10); towns of 400+ bind more of their households to the first (8 % -> 15 %) but keep large unbound factions (median 8 -> 21 households) -
  Carneiro's circumscription: the factions a full land cannot shed stay inside, the condition for contests from within (next).
- `--nosplit` gives C10.2 alone on the same binary.

## 12e. Implemented: a headman gone to live with their child; a household's followers follow it (D2-138)
- **Why**: C3.6e (D2-136) sends old landholding households to live with a child's household; a first household's headman then lived in another
  household, which `Leaders.Day` read as a vacancy - contested, and recorded "another household rose" for the same man (`place --heads`: 37-127 such
  successions a world over 300 years, 4 of 20 splits). Under it a gap since C10.2: a household's clients (`Patron`) lost their tie when it was
  absorbed (the last out's new household, an heir's), though its debts went over.
- **Now**: a headman alive and in the village but in another household is weighed again without a contest; **the household that took the first
  household in is the incumbent** (it keeps the place while followed by at least half the largest); the same person stays first and nothing is
  recorded; **a household's clients follow the household that took it in** (`Holdings.Clients`). A death or leaving the village is a vacancy as
  before. `--nostanding` keeps the old reading.
- **Result** (ten seeds against `_j1`): successions at a retirement 37-127 -> 0-17 a world, headmen x0.83 (the place changes hands less), splits at
  a living headman's move 4 -> 2; people and vital rates at par; conflict lower inside the noise; tribute agreements 17 -> 8 (low counts).

## 13. Civilisation depth (D2-089)

Force, fortification, war and sieges are designed in `23_MILITARY_AND_WAR.md` (fighters as persistent people, arms as goods, training,
the cost of service, campaigns as logistics, fortification progression, sieges); governance - leaders, succession, obligations,
administrative reach, rebellion, fragmentation - is roadmap phase C10 (`25`). This doc's §§1-12 remain the foundation.
