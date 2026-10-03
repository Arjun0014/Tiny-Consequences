# V3-60 — R6 Conflict: designed from primitives (session 20, D-149)

Shared primitives: `50_PRIMITIVES.md` (deeds, danger noticed, urgent choice, injury, condition,
boldness/arms). Research: `40_RESEARCH_CONFLICT_PLANET.md` §A. The v2 conflict system
(`sim/society/conflict.lua`: monthly village decisions, Lanchester outcomes, grievance per village
pair) is **not** ported: v3 conflict is people deciding, walking, taking, being seen, remembering.

## 1. The smallest conflict properties (beyond `50_PRIMITIVES.md`)
1. **Opportunity is knowledge**: a household can only covet beasts it knows of - its village's
   snapshot of another village (`econ.learn`) gains the beasts seen there and whether dogs keep
   the pens (`k.beasts`, `k.dogs`), from real visits only.
2. **Taking is a choice weighed by a person for their household** (A1): `gain` (the beast's worth
   to the household now: meat for the hungry; the herd for one that lost its own) against `risk`
   (being seen: dogs, watches, people up at night, how far the pen is from the houses; what follows:
   hurt, the beast lost, a claim, a counter-raid) and `restraint` (kin living there, trust,
   friends there, debts kept, how many deals pass between the villages - Gluckman's ties; the
   person's boldness lowers it). A **grievance** against that village (its deeds known) adds a
   motive and lowers restraint (taking back is not stealing to the taker). Nothing forces it.
3. **Being seen** is perception: a dog at its post notices a stranger near the pen at night as it
   notices a wolf (`dogs.lua`), barks and wakes its household; people awake near see; a beast
   handled may bleat. A witness knows the thief by name only if the thief has been to their
   village before (`p.been`); otherwise "a stranger", his village told by the road he fled along.
4. **Discovery and tracks**: an owner finds a beast missing at the morning count; if the ground was
   wet (rain in the last day or wet soil) the tracks of a man leading a beast are found and show
   the road taken.
5. **Answers are choices from state, later, by whoever holds the grievance**: watch the pens (the
   night watch already exists for wolves; losses to thieves are losses too); **claim** (walk to the
   culprit's village and ask for the beast back or its worth: the accused household returns it,
   pays in kind, denies or refuses - weighed by its need, the evidence, its ties and its village's
   wish for peace); **take back** (the same taking choice, now with a motive); trade and trust
   change by themselves (`econ.rel`); forgetting is time.
6. **Settlement ends a deed**: a return or payment is itself a deed that settles the first; trust
   recovers partly; a refusal is a new harm.

## 2. The first vertical slice (R6a): a stolen beast
**Premise** (`premise.lua`, never `start_raid`): a village with a failed harvest and a murrain (its
households have no beasts left and little bread), its trusted neighbours far or with nothing to
spare, and a village within a day's walk that its people have visited (trade, a fair) whose pens
hold beasts, with low trust between them. Everything after is decided.

1. **Considering** (`conflict.take_candidate`, from `think.work_candidates`' time of day: morning):
   an adult of a household in hardship (`migrate.push` high: no bread for a few days, no beasts of
   its own) or holding a grievance, fit to walk (not hurt, no babe, not with child) weighs each
   known village within reach: what beast it could lead (sheep/goat first: easy to lead; cattle
   only by the bold; pigs not), the household's gain, the risk seen from what is known (dogs, a
   watch they have heard of, village size), restraint (kin there, trust, friends, deals, debts) and
   their boldness. The best, if above what the household's other answers to hunger offer, becomes
   a **take trip** (`p.trip = { take = true, to, from, kind, why, t0 }`). The why names it: "Nothing
   left to eat and our beasts are dead; Zurug's fold is full and nobody there is kin of ours".
2. **Going** (`think.take_legs`): provisions from home, the road to the village (by day, the
   existing road legs), then off the road near the map's edge to lie up among the trees out of
   sight ("Waiting for dark in the woods above Zurug"), until the village sleeps (past the latest
   bedtime of its people).
3. **Taking** (WORK `take`): to the pen; one beast taken by the halter (the existing drover's
   halter: the beast follows); a deed `take` is written (by, to, what, worth); cues: the beast
   handled (a small sound), the man himself (sight) to dogs and anyone awake; a dog that notices
   barks and wakes its household (`dogs.alarm`, kind `stranger`).
4. **Answered at once** (urgent choice): the woken owners come out to the fold (`confront`); the
   thief, noticing them (barking, shouting, people coming), decides: let the beast go and run, or
   keep leading it (bold, nobody close). In R6a a defender who reaches the thief makes him let go;
   blows come in R6c. The witnesses remember what they saw.
5. **Away**: to the gate, the road home leading the beast (`stock.leave_map`; the drove's road
   pace), into the household's herd at home (`stock.join_map`; `a.owner` = the taker's household,
   `a.why_owner` = "taken from the Ulfs of Zurug in the night"), where hunger does the rest (the
   household's hunger cull kills it for the pot: D-140).
6. **Discovered**: at the owners' morning count the beast is missing (not dead, not sold); the
   owner village learns the deed (`deeds.learn`, how: "found missing" / "tracks led to the road to
   Hottoodingle Hop" / "Lolo saw him"); trust toward the culprit's village falls if it is known.
7. **Later, from state** (each a decision on its own day): the night watch at the fold
   (`guard_herd`, now reading losses to thieves as well as wolves); a **claim** trip by a man of
   the robbed household when the culprit is known (to the culprit's house; the accused household
   returns the beast if it lives, pays in goods, denies, or refuses); **taking back** by the same
   choice as in step 1 with the grievance as motive; news of the deed travels wherever people of
   either village go (`econ.on_learn`).

**Follow** (headless, `tools/v3_follow.lua`): the taker, the owner, the witness, the dog, the
claimant, the accused - each answers why, who, how they know, what they want, why this target.

## 3. Sequence for R6 (each a whole loop, watched before the next)
| Stage | What | Built from |
|---|---|---|
| **R6a** | deeds, boldness, opportunity in the snapshot, the take trip, dogs and witnesses, discovery and tracks, deeds learned through contact, trust, the night watch after thefts | §2 |
| **R6b** | claims and settlement (return, pay in kind, deny, refuse) with the accused village's ties pressing; taking back as a choice | deeds, econ baskets (D-143), trip legs |
| **R6c** | confrontation and **injury**: chase, a blow, flight; hurt slows, keeps to bed, heals with care or kills; arms from the household's iron | danger, urgent choice, injury, arms |
| **R6d** | **sudden death aftermath** as one shared path: kin (not only the spouse) remember, a burial with kin gathering, blood deeds weigh long; widow, orphans, heirs, mentors already handled | deeds, life.kill, take_in, estates |
| **R6e** | **raids**: a leader recruits kin and friends (each deciding), the party gathers at an agreed hour, walks together, takes several beasts, meets defenders | take trip x party (migration's gather logic), confrontation |
| **R6f** | **defence that is built**: penning beasts close, arms carried at night, a stout fold or a palisade planned when remembered losses x expected loss outweigh timber and labour - felled, carried, built in sections, a gate | condition, settle.add_building, deeds |
| **R6g** | **ending**: compensation as a household's choice, marriages and kin across the line, shared danger, exhaustion, trade dependence - measured, not scripted | deeds, econ.rel, fairs, matches |
| later | captives (only as specific people with households, a destination and a way home), alliances (help sent because kin or a debt), migration for insecurity (fear of a place from deeds read by `migrate.push`) | all above |

## 4. Tests (premises, never forced outcomes)
- `test_v3_conflict.a_hungry_household_takes_a_beast`: the premise of §2; within N days someone of
  the hungry village decides to take, walks to the village whose beasts they know, takes one and
  leads it home (or is seen and lets go); the deed exists with its witnesses; the beast is where the
  deed says (in the taker's herd, eaten, or back with its owner).
- The untouched world: no theft in 192 days unless a real hardship arises (measured).
- A kin village is never robbed by its kin's household (restraint), a trusted neighbour rarely.
- Later stages add claims, a counter-take chosen from state, injury healing and death, raids.

## 5. Emergence targets (R6 as a whole)
One stolen sheep may become: nothing (never discovered, or forgiven: kin across the line), a
restitution (claimed, returned), a friendship damaged (trust down, no more trade), a feud (taking
back, claims refused), a raid (a party gathers), migration (a household afraid of a place leaves),
depending on who saw it, kin, scarcity, the road, trust and what happens next.

## 6. What not to do in R6
No `relations.war`, no raid counter, no retaliation fired by a theft, no palisade by count, no
loot teleported, no off-map fight, no raiding party with one position, no captive counter, no
"criminal" trait, no hostility number standing in for deeds.

## 7. Status (session 20, D-150 - D-153)
First pass of every stage built and tested by premises (`tests/sim/unit/test_v3_conflict.lua`, 9 tests):
- **R6a** taking beasts (`conflict.take_weigh`/`weigh_at`, the take trip, dogs and witnesses, the
  morning count, tracks, news of deeds) - D-150.
- **R6b** claims (`claim_weigh`/`claim_answer`, WORK.claim: return, pay and owe, refuse, deny) - D-150.
- **R6c** injury and blows (`people.hurt`, healing, tending, `conflict.strike/blows`, "deserved") - D-151.
- **R6d** every death a deed, kin mourning on learning, the burial gathering - D-151.
- **R6e** raids raised from lone failures, joining by one's own weighing, the band (`trip.band`) - D-151.
- **R6f** a fold wall from remembered losses (thieves and wolves alike) - D-153.
- **R6g** amends unasked (`amends_weigh`, WORK.amends, the rest owed) - D-152.
Observed: untouched 192 days - no taking (the world must be in want for it); hungry premise - lone men
given away by dogs, then a party raised (Kotumbo and Plock), a man caught and struck hard (Gedag of
Zurug), tended at home; with Zurug's dogs dead, 3 sheep taken, tracks found, a claim met with "nobody
would own to it"; a household whose man raided Zurug moving to Zurug meanwhile. Browser = LuaJIT on the
conflict premise (`node scripts/v3_hash_web.mjs --release --premise conflict --hours 264`).
Next for R6 (not done): a palisade round the houses (a ring footprint, gates), arms carried at night
(spears from wood and iron), captives (only as specific people), alliances (help sent to kin under
attack), moving for insecurity (fear of a place read from deeds by `migrate.push`), grievance shaping
marriage and trade choices, restocking raids after a murrain by herders who are not hungry.
