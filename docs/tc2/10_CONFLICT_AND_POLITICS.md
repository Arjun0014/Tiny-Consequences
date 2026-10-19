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

## 12f. Bread and dependence: lordship from the famine grain a village cannot give back (C10.4, D2-157)
- **Measured first** (session 22). Polities were nearly absent: over twenty worlds at year 300, 0-3 lords a world, each with 1-2 tributaries, depth
  1 (seed 4: 30 villages, 10,000 people, one lordship of two) - lordship came only by conquest (D2-075: a raided village, hemmed in, bows to its
  stronger raiders), and raids are rare (6 in 300 years on seed 4). `polity --load SAVE` (the ties a polity could be made of) on seed 4 at year
  300: **kin does not mark mother and daughter villages** - 24 of 30 villages were founded by another's people, but after decades of marriages
  between villages every pair within 6 km is kin (99 of 100 other pairs; 43 kin links a mother-daughter pair, 36 an other pair) and no daughter's
  first household is close kin of its mother's - a lordship of founders over daughters would be written, not read from the state. **Relief is**:
  famines are common (110-257 a world in 300 years), neighbours with grain to spare send it (seed 4: 692,900 FU over the run, by 1,007 neighbours'
  hands), and only 15 % of it ever comes back (100,767 FU, D2-113) - half the villages owe a neighbour at any time (15 of 30 at year 300, up to
  9,740 FU).
- **The record**: famine was the commonest road into dependence before states and under weak ones - Attica's hektemoroi paid a sixth of their
  crop to the creditors of their bad years until Solon (Aristotle, *Ath. Pol.* 2); the early medieval poor commended themselves in famine to whoever
  would feed and clothe them (*Formulae Turonenses* 43: "since I have nothing to feed and clothe myself..."); chiefs' storehouses that fed the
  hungry bound them (Earle 1997, staple finance); debt from bad harvests as the ancient origin of bondage (Graeber 2011).
- **The rule** (`Conflict.Indebted`, both resolutions, at a village's harvest while it pays no lord): what it still owes the neighbour that fed it
  most in its famines (`Trade.MainCreditor`: the relief it saw given within 16 years, weighed now) beyond all its new harvest can spare is more than
  a month of its people's food (`BowDays` 30: a famine's whole relief unreturned), and that neighbour lies within a day's walk (6 km) and is the
  stronger by D2-075's odds (1.2 x its fit grown people), and does not pay it: it bows - a tenth of each harvest. History: `TributeAgreed` (ArgB 1),
  its cause the weightiest gift.
- **What the bond does** (all from what was there): the tribute (`Conflict.Harvested`) is grain to the lord's first household (C10.2) and **gives
  the relief back** (`Trade.Repaid`: the gifts weigh that much less, as given-back grain does); **a lord feeds its own** - its relief to a
  tributary in famine is not closed by what the tributary owes (`Trade.Relief`; D2-113's open hand applies to everyone else); the tributary answers
  its lord's call (C10.1), its lord answers its wrongs (D2-108), no band of its lord's falls on it, it flies its lord's standard (D2-152). It ends as
  D2-075's does: the lord gone, or no longer the stronger (a refusal the lord holds).
- **Pages**: the settlement page says why it pays ("its grain kept it through its famines, and it could not give it back" / "it could neither stand
  against its bands nor go elsewhere") and the famine grain it still owes each neighbour. Headless: `lordships:` counts the bonds for bread,
  `--breadtally` (village-harvests by the days still owed to the main giver; those whose giver was the stronger and near), `--nobread` (D2-075
  alone on the same binary); `polity` marks each lord for bread or for peace.

## 12g. A lordship's might: a lord is as strong as the men it can bring (C10.5, D2-159)
- **Measured first** (session 23, the 900-year check of C10.4; `lordships --load SAVE`, a new tool that reads every bond of tribute from the
  history). Seed 2, years 0-900: 49 bonds (31 for bread, 18 for peace); 39 ended, **every one because the lord was "no longer the stronger"** -
  none because it was gone; 4-9 lords at a time among 27-31 villages, the largest lordship never more than 4 villages (depth 1-2), no growth over
  600 years. Two faults in how "the stronger" was weighed (D2-075: the fit grown people at home in each village on the day of the tributary's
  harvest; a lord by its own village alone):
  - **a day's count**: in year 464 a mild epidemic reached 12 villages and killed 6 people - and five lordships ended that year, their lords'
    people abed on their tributaries' harvest days; bonds agreed the day after a raid (the beaten village's wounded abed) ended at its next
    harvest when they had mended - the same pair bound and freed again within 2-5 years (Naekthorpe and Krounpelthorpe three times in seed 11's
    first 300 years; 10 rebindings of a pair in seed 2's 900);
  - **a lord's own village only**: a lord with tributaries near a village is weighed as if they did not exist, though its call (C10.1) brings
    their men to its bands - against a refuser too (a refusal is a wrong the lord holds). D2-075's own evidence said it: "lordships stay small and
    short ... while a lord's strength is only its own village's".
- **The rule** (`Conflict.Hands`, `Conflict.Might`): a village is weighed by its **people of fighting age** - its grown people of 16 to 60 who live
  there, wherever they are that day: a day's wounds, sickness and hunger pass and do not count against it; the dead, the old and those who left do.
  A lord is weighed with **the men its call would bring** against the village weighed: of each of its tributaries within a day's walk of it, a
  quarter of the meek and more of the bold (C10.1's own odds, grown and under fifty), never those with close kin there; the village weighed is
  never its own caller. Used wherever two villages weigh who is the stronger for a bond: a beaten village bowing to its raiders (D2-075), a
  village bowing for bread (C10.4), a tributary that would pay no more at its harvest. The tactical counts of a night's raid (who wakes, who comes
  running) stay the day's: those abed cannot come.
- **The record**: a paramount's power lay in the fighters its subordinate villages owed it, not in its own village alone (Earle 1997 on the
  chief's military power; Carneiro 1981 on chiefdoms grown by conquest, the conquered owing men as well as tribute), and dependants broke away
  from a lord weakened in fact - a lost war, a dead lord, a plague that killed - not from its sickbeds.
- **Tells**: the weighing is recorded with each tribute (`TributePaid` ArgA/ArgB) and with its end (`TributeEnded` Magnitude/ArgB): the settlement
  page says "At its last harvest X could bring N against it (its own people and those its call brings); it had M of fighting age"; history says
  why a tributary paid no more ("...is no longer the stronger - it could bring N against them ..., and they have M of fighting age").
- **Tools**: `--oldmight` (D2-075's day count and a lord's own village, on the same binary), `--mighttally` (each weighing by the old count and by
  might), the `might:` report line; `lordships --load SAVE [--every N] [--bonds]` (session 23).

## 12h. Tribute carried: the lord's tenth goes to its store on its tributaries' backs (C10.6, D2-161)
- **Found** (session 23's real shots of seed 2's bread lordship, `s23a`/`s23b`): the bond read only at close range - the tributary flies its lord's
  standard by its first household's door - and not at all from the village view or the region; and the tenth went into the lord's granary at the
  harvest by no one's hands, a transfer between villages with no carrier, cost or road.
- **The rule**: at the tributary's harvest the tenth is set aside in its store for the lord (`Settlement.TributeDue`); its people carry it there
  (`Conflict.Carry`): as many loads a day as clear it in ten days, never more than one for every twenty of its fit grown people; a back's 25 kg or
  the household's cart's on a worn way (C4.2). Lived, the village's day sets the loads wanted (`CarryToday`) and grown members take them in the
  morning (`WhyCode.Tribute`), walk to the lord's village, stay a while, come home; a COARSE day carries the same loads at once. Each load wears the
  way (C4.1), carries sickness and news, and is **brought** (`Conflict.Brought`): into the lord's first household's grain (C10.2), answering the lord's
  wrongs (D2-075), giving back the lord's famine grain (C10.4). A bond that ends gives back what is still set aside; a load on the way goes home
  into the store - but a load sent home while the bond holds (everyone home at once when the world goes COARSE) is set aside again and carried.
- **Only what can be carried is set aside** (found reading the code before landing): a bond for bread needs only a day's walk as the crow flies
  (C10.4), and relief within it moves at once, so a village across water can owe - and bow to - a neighbour no way on foot leads to. Its tenth is
  then not set aside: it stays in its store, and the pages say why ("No way on foot leads from it to X: its people cannot carry the tenth there");
  carrying it by boat waits for boats across the water (C9.5). And what still stands set aside at the next harvest - carried by nobody in a year:
  no hands fit to carry it, or the way cut since - is the village's again before the new tenth is set aside, so no store keeps a pile for a lord
  its people cannot bring it to.
- **What it changes in the world picture**: carriers with sacks (and carts) on the way to the lord after each tributary's harvest; the way between a
  tributary and its lord walked two times a load - worn to a path or a road, so the ways of a lordship converge on its lord; the standards (real
  state) enlarged with the camera's distance up to 4x so that the polity's colours read from the village view.
- **The record**: food rents carried to the lord's hall - the *feorm* of the early English kings, a night's food for the king's household brought in
  kind; manorial dues carted to the lord's barn after the harvest.
- **Tools**: `--oldtribute` (the tenth into the lord's store at the harvest, carried by nobody), `--carrytally` (the `carried:` line: loads, FU,
  lived, by cart, carriers' km, given back at a bond's end or after a year uncarried, tenths not set aside for want of a way), `lordships` (the ways
  between the bound and their lords; the bonds with no way on foot and what stands set aside).

## 13. Civilisation depth (D2-089)

Force, fortification, war and sieges are designed in `23_MILITARY_AND_WAR.md` (fighters as persistent people, arms as goods, training,
the cost of service, campaigns as logistics, fortification progression, sieges); governance - leaders, succession, obligations,
administrative reach, rebellion, fragmentation - is roadmap phase C10 (`25`). This doc's §§1-12 remain the foundation.
