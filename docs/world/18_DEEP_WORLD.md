# W18 — The deep world: from households to polities (architecture extension)

Written 28 Sep 2026 after W2 (D-054), in answer to Arjun's brief: individual people, households,
work, resources, trade, settlements, migration, social groups, clans, alliances and conflict,
territorial expansion, polities if they emerge — as coupled systems, not features. This extends
`10_WORLD_SYSTEMS.md`; phases are in `17_ROADMAP.md` (W7–W10). Decision record: D-055.

## 0. What W2 gives us, and what is missing

W2 made the village a real economy: people with homes, households, tasks and bodies; fields,
boats, woodlots and buildings that take labour, timber and seed; stocks with causes; migration as
walking parties; fission when land fills. Watching it (the ledgers, the village shots) shows the
next gaps plainly:

1. **Villages are islands.** Nothing moves between them except refugees and colonists: no gifts
   in a bad year, no trade, no marriage beyond the village, no memory of who helped or who took.
   Seed 9's collapse was the price: two neighbours at the edge of their land with no way to
   share a surplus. Real small-scale societies survive bad years by *social storage*: exchange
   and reciprocal obligations that turn a neighbour's abundance into one's own insurance
   (Halstead & O'Shea, *Bad Year Economics*, 1989).
2. **Nobody is anybody.** There is no status, no leader, no lineage beyond parents. Decisions are
   the village's; conflicts of interest between households do not exist.
3. **No conflict.** Land fills, and villages only split or starve. In the ethnographic record
   90-95% of societies fought, most of them every year (Keeley 1996); raiding for food and land is
   the classic response to shortage where people cannot move away.
4. **Idle hands.** Outside sowing and harvest most people have nothing to do (the schedule shows
   half a village at the well at noon): the missing work is husbandry (livestock), crafts
   (cloth, pots, tools), gardens — the household economy that also makes goods to trade.
5. **Animals are fields.** Deer and wolves are densities; there are no herds to hunt or to herd.

## 1. Principles for depth that stays computable

- **Tiny world, full population.** ~500 land cells can feed a few thousand people; the target is
  a real agent for every person up to ~3,000 (today's 900 soft cap is a performance guard).
- **Every new layer is a graph over existing entities, updated at the slowest rate that keeps it
  honest.** People: weekly bodies (monthly for the healthy far from events, W6). Households:
  monthly. Villages: daily work, weekly plans. Inter-village relations: monthly. Polities: yearly.
- **Sparse, bounded state.** A relation exists only between villages that have met (kin,
  neighbours, trade partners): O(villages × partners), at most a few hundred edges.
- **Everything physical travels.** Gifts, trade goods, tribute, raiders and brides walk (or sail)
  as parties with cargo on a real path; they can be caught by weather, fire, the player.
- **Every state has a cause.** Grievances remember the raid event, alliances the marriages and
  gifts, a chiefdom the defeat or the kin tie it grew from (03 + co-causes, D-054).
- **No labels without mechanisms.** A "chiefdom" or "kingdom" is a name the chronicle gives to a
  structure that already exists in the tribute graph; the sim never switches an entity into a
  "kingdom state".

## 2. New layers

### 2.1 Lineages and status (people and households)
- **Lineage** (clan): every person inherits a lineage id at birth — patrilineal or matrilineal by
  culture — rooted in a founding ancestor (settlers, colonists' founders). A lineage is a list of
  living members per village; its name is the founder's surname. Cheap: one integer per person.
- **Household wealth**: a household's own store share (households contribute to and draw from
  the village store by need; what they bring in beyond need raises their standing), its house,
  its boats and, later, its animals.
- **Prestige** (per household head): age, lineage seniority in the village (first founders),
  generosity (food given in shortages — to kin, to neighbours, to other villages), skill in a
  trade (big catches), success in raids. Prestige decays slowly. (Big-man and ranked-lineage
  models: status earned by giving, inherited through lineage.)
- **Leader**: each village's head is the household head with the highest prestige (the chief's
  lineage keeps an edge where culture ranks lineages). The leader decides the village-level
  questions below; when the leader dies, succession (lineage-ranked or competitive) can split
  the village along lineage lines (fission follows kin lines: *Causes, consequences and kin bias
  of human group fissions*, Walker & Hill 2014).

### 2.2 Relations between villages (the social graph)
Per pair of villages that know each other: `kin` (households with close kin there, updated when
people marry out or move), `given`/`received` (need-days of food and goods, the reciprocity
ledger), `grievance` (raids, refused help, encroached fields — decaying memory with the event ids),
`distance` (walking days). A relation score and its sign give *allied / friendly / neutral /
hostile*. It is read by every inter-village decision below and by marriages (spouses come from
friendly villages), migration (kin and friends open doors — D-054's "closed doors" become a
relation question) and the chronicle ("Why do they hate each other? Year 41: the raid on ...").

### 2.3 Exchange: gifts, trade, social storage (W7)
- **Relief**: a village rationing or in famine asks its friends and kin; a village with a secure
  store (beyond its own need to the next harvest + margin) sends a carrying party with grain.
  The giver's leader gains prestige; the pair's ledger records the debt; the famine's chain now
  has a relief link ("they survived because Upper Dumholm sent grain").
- **Barter trade**: complementary surpluses (grain ↔ fish, wood ↔ grain, later cloth, pots,
  tools, salt) between friendly villages within reach, at exchange ratios set by each side's
  need-days (no money). Overland: porters (a person carries ~20 FU a day's walk); along coasts:
  boats; later merchant ships (13 §3). Regular partners form routes; routes wear paths (a visible
  trail network grows where people walk: 16 §4).
- **Specialisation emerges** where trade is reliable: a fishing village that can count on grain
  plans fewer fields (the field plan already subtracts the reliable catch; imports join it).
  If the partner stops (war, sinking, famine), the dependency bites — chains through trade.

### 2.4 Conflict (W8)
- **Raids**: a hungry or land-short village with enough young adults, led by a leader whose
  prestige rewards war, may raid a *neutral or hostile* village within reach whose store is worth
  it. The raid is a party of real people walking there; the outcome follows numbers, surprise and
  defences (palisade, allies who come); losers lose people (deaths tagged to the raid), grain and
  sometimes houses (fire); the winners carry the loot home. Grievance and revenge follow — feuds
  are emergent cycles, not scripts.
- **Defence**: villages that are raided build palisades (a building: timber and labour, visible)
  and cluster (households move to larger or better-defended villages — defensive aggregation).
- **Land**: fields claimed by two villages' reach (the shared-land rule of D-054) become disputes;
  encroachment raises grievance.

### 2.5 Polities (W9): the tribute graph
- **Submission**: after repeated defeats a village can accept paying tribute (a share of its
  harvest carried each year to the victor) in exchange for peace and protection; daughter villages
  start as kin-allies of their parents and may pay "gifts" that become tribute. This is Carneiro's
  circumscription path: where land is full and people cannot move away, war produces
  subordination rather than flight; where land is open, the defeated disperse (Carneiro 1970).
- **The paramount** village receives tribute, redistributes in famine (staple finance, Earle),
  calls allies to war, and grows: its leader's prestige and its population rise, specialists
  (builders, boatwrights, later traders) can be fed without farming.
- **Tiers**: a polity is a connected component of the tribute graph; its depth (village → local
  centre → paramount) gives the chiefdom (2 tiers), complex chiefdom (3) and, only with ≥ 4 tiers
  and specialised administration, a state (Wright & Johnson 1975; Wright 1977). Territory is the
  union of member villages' land and fields plus the frontier they patrol. Borders, capitals,
  secessions (a strong tributary stops paying), conquests and collapses all come from the graph.

### 2.6 Household production and animals (W10)
- **Livestock**: goats/sheep/cattle as herds owned by households, grazing grass cells (the
  planet's grass field), giving milk and meat (fresh food), manure (fertility on the fields —
  Boserup intensification with animals), wool/hides (cloth), and wealth (bridewealth, prestige,
  raiding targets). Herders take animals to pasture by day and pen them at night.
- **Crafts**: spinning/weaving, pottery and tools in the slack season from materials (wool, clay,
  wood); goods wear out (demand) and trade; specialists appear where exchange is dense.
- **Wild herds and packs** (14 §3): hunting takes real animals; herds migrate seasonally; wolves
  follow herds.

## 3. How the layers couple (examples of chains that become possible)

- *Drought → crop failure in A → A asks kin village B → B sends grain (B's store falls) → B short
  next spring → B's fields sown late → ...*; or *B refuses (its own store is thin) → grievance →
  A raids B → deaths in B → B builds a palisade (timber → forest thinned) → B allies with C →
  A defeated → A pays tribute to B → B becomes a centre ...*
- *Player sinks the trading boat → C's grain imports stop → C (a fishing village that had given
  up fields) rations → C's young men raid D ...*
- *Fishing village overfishes → catch falls → trades less fish → partner's diet changes → ...*

## 4. Cost estimate (HTML5 release, ~7.4 ns per Lua VM instruction)

| Layer | Rate | Size | Cost / sim week |
|---|---|---|---|
| lineages, prestige | monthly per household | ~600 households | ~15k instr (amortised) |
| relations | monthly per village | ≤ 30 villages × ≤ 10 partners | ~3k |
| exchange decisions | weekly per village | ≤ 30 | ~10k |
| parties in transit | hourly, only departures/arrivals | tens | ~2k |
| raids | monthly decision, hourly resolution when they happen | rare | ~2k |
| tribute | yearly | ≤ 30 | negligible |

The social layers cost less than today's labour allocator. The binding cost remains per-person
bodies (W6: monthly updates for the healthy, event-driven for the sick and pregnant).

## 5. Sources
- Carneiro, R. (1970). A theory of the origin of the state. *Science* 169; summaries:
  https://www.sociostudies.org/journal/articles/148709/ ; cross-cultural test:
  https://escholarship.org/uc/item/1sj9n878
- Wright, H. & Johnson, G. (1975); Wright (1977) on settlement tiers and the state; Spencer (2010),
  *Territorial expansion and primary state formation*, PNAS: https://www.pnas.org/doi/10.1073/pnas.1002470107
- Earle, T. (1977), *A reappraisal of redistribution: complex Hawaiian chiefdoms*; staple vs wealth
  finance: https://www.researchgate.net/publication/297691770
- Halstead, P. & O'Shea, J., eds. (1989), *Bad Year Economics*, Cambridge UP:
  https://www.cambridge.org/core/books/abs/bad-year-economics/introduction-cultural-responses-to-risk-and-uncertainty/F988CB740798B34E3D8EF5673AEB16D8
- Keeley, L. (1996), *War Before Civilization*, OUP: https://en.wikipedia.org/wiki/War_Before_Civilization
- Walker, R. & Hill, K. (2014), *Causes, consequences, and kin bias of human group fissions*,
  Human Nature: https://link.springer.com/article/10.1007/s12110-014-9209-0 ; Yanomami village
  fission at ~70-100+ people: https://en.wikipedia.org/wiki/Yanomami
- Village Ecodynamics Project (Kohler et al.): household agents with reciprocal exchange networks
  on a dynamic landscape: http://village.anth.wsu.edu/ ; Cockburn, Crabtree et al. (2013),
  *Simulating social and economic specialization in small-scale agricultural societies*, JASSS
  16(4)4 (barter, balanced and generalized reciprocity between agents): https://www.jasss.org/16/4/4.html
