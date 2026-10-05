# TC2-11 — History and causality

Decision: D2-008. History is a first-class system from day one: one causal record that serves
WHAT IS HAPPENING (World News), WHY (causes), WHAT DID I CAUSE (Your Actions) and WHAT HAPPENED
(the Chronicle / History Book).

## 1. Principles

1. **Systems write history at the moment things happen**, with the causes they actually acted on.
   Nobody writes history after the fact except historians (observers), who only *interpret* it.
2. **Causes are real links**: an event's causes are events (or entity states tagged with events) that the
   writing system read when it decided. No cause is ever inferred from text or kind.
3. **Every chain ends at NATURE, PEOPLE or YOU** (TC1's three roots). A root is an event with no cause:
   a natural trigger (lightning, an eruption, a mutation), a person's own choice (people), or a command.
4. **Names and ages come after the fact**: eras, wars and "the Age of X" are labels historians give to
   patterns that already happened.
5. **Bounded memory**: ordinary life is counted, not stored; personal events are kept while they matter;
   historical events are kept forever; importance can rise later and protect an old event.

## 2. Identity

- Every historical entity has a stable id from a per-kind monotonic counter (never reused) and a record
  in the **EntityRegistry**: kind, id, name, created/ended times, origin (place, parent entity), notable
  flag, importance. Kinds: Person, Household, Settlement, Building, Polity, Culture, Species, Population,
  Pathogen, Technique, Place (named regions: mountains, seas, rivers, islands), Process (a fire, a flood,
  an epidemic, a war - things with duration), Route.
- `EntityRef` packs (kind, id) into a `long`. Events reference entities only by `EntityRef`.
- Dead persons keep a compact `PersonRecord` (names, sex, birth, death, parents, partners, household,
  birth place, cause-of-death event) while referenced by a living person's kin within three generations,
  by a kept event, or by notability; then they are pruned (their kept events still name them).

## 3. The event record

```
struct HistoryEvent {
  long Id; long Time; HistoryKind Kind; Tier Tier; EventFlags Flags;
  int Cell; int Settlement;                 // where (either may be -1)
  long Subject, Object, Other;              // EntityRefs
  long Cause1, Cause2; CauseRel Rel1, Rel2; // up to two direct causes; more in an overflow table
  long Root; RootKind RootKind;             // the primary chain's root (cached at write time)
  float Magnitude; int ArgA, ArgB;          // kind-specific (deaths, cells burnt, a value)
  float Importance;                         // grows with descendants
}
```
Cause relations: `CausedBy` (direct physical/social cause), `EnabledBy` (a condition: drought enabling a
fire), `InResponseTo` (a choice answering something: fleeing a fire, avenging a raid), `PartOf` (an
episode of a larger process: a house burnt in the Great Fire).

## 4. Tiers

| Tier | Stored | Examples | Kept |
|---|---|---|---|
| 0 Ephemeral | counted only | walking, eating, ordinary work, a row sown | never stored; daily/seasonal counters and time series |
| 1 Personal | yes | birth, death, marriage, injury, apprenticeship, profession, a journey turned back, a move | while the person lives and 30 years after; a household's while it has members and 30 years after it ends; or importance >= 5 (base or propagated) (D2-054) |
| 2 Local | yes | a house built or burnt, a harvest failed, a theft, a local fire, a bridge washed away | ~200 years unless important |
| 3 Historical | yes | settlement founded or abandoned, battle, famine, epidemic, discovery, eruption, major migration, polity formed or collapsed, god intervention | forever |
| 4 Epochal | yes | climate regime change, extinction, a species born, the human age ended, a continent split | forever |

Aggregation: many similar events from one process become one event with a magnitude ("the fire burnt 41
cells and 6 houses") plus `PartOf` children only where they matter (deaths of persons).

## 5. Importance

- Base importance by kind, tier and magnitude (deaths, people affected, area, duration) - magnitude only where it is a size:
  a person's events carry an age or a count there and keep their base importance (D2-054).
- **Propagation**: when an event is appended with causes, a share of its base importance flows up its
  causal ancestry: `parent += b x 0.5^depth` for depth 1..8 or until the share < 0.01. A small fire whose
  descendants destroy a settlement, drive a migration and found a later kingdom becomes important
  *because* of them, years after it happened.
- Notability of persons and settlements follows the importance of the events they are subjects of.
- Compaction (yearly) never removes an event above the keep threshold for its age, and rewires the causes
  of survivors to the nearest kept ancestor of any removed event (chains stay connected).

## 6. Causes from the physical world (blame)

Fields and states carry **cause tags** (TC1 03 §3): the event id currently dominating a cell's anomaly
(burning, drought, flood, ash, ice, blight), a settlement's food shortfall, a person's hunger or injury.
Systems that act on tagged state pass the tag on as a cause. Re-tagging at detected threshold crossings
lengthens chains (drought -> crop failure -> famine -> migration).

## 7. Views (observers; never write World state)

| View | Question | Mechanism |
|---|---|---|
| World News | What is happening now? | incremental sifting of new events by incident (kind, place, process); importance + recency; breaking while a process is active |
| Why? | Why did this happen? | walk causes from an event, or from a state's cause tag, to the root |
| Your Actions | What did I cause? | children index of player-rooted events; counts direct and downstream; "still unfolding" while processes live |
| Chronicle / History Book | What happened over 10 / 100 / 1,000 years? | era detection over yearly indicators; top events per era by importance; biographies; settlement, polity and species histories |
| Catch-up feed | What happened while I was away? | the historical events of the caught-up span, streamed during catch-up and summarised after |

## 8. Eras (after the fact)

Yearly indicator vectors (global temperature anomaly, ice fraction, sea level, population, settlements,
largest polity share, conflict deaths, famine and epidemic deaths, techniques first used, settlements
founded/abandoned, fortifications built, extinctions) are segmented by change-point detection with a
minimum length (~30 years). A segment that ended at least ~20 years ago is named from its dominant
signature and its region ("The Long Winter", "The Age of Founders", "The Western Wars", "The Second
Coast"); the ongoing segment is "the present age". Names are templates over measured features; no era is
ever an input to the simulation.

## 9. Storage and cost

- Chunked append-only arrays of `HistoryEvent` (~80 bytes); per-entity event lists for biographies;
  a children index only for player-rooted chains and for events above an importance threshold.
- Budget: <= ~50 stored events per world day on average in a 2,000-person world (most are personal);
  compaction keeps the ledger at a few MB per thousand years.

## 10. Foundation scope (session 1)

Ledger, entity registry, tiers, causes with relations, roots, importance propagation, compaction with
rewiring, per-entity index, serialization, a text formatter for the catch-up feed and debug dumps, and the
events of the proof of concept (lightning -> fire -> fire spread -> journey interrupted / turned back;
births, deaths, marriages, settlement founded, harvests).
