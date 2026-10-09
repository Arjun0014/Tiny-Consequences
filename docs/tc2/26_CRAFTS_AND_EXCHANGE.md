# TC2-26 — Crafts, workshops and exchange (C3)

Part of the civilisation-depth direction (`20`, D2-089; roadmap `25` C3). Chosen after C13.3 (session 11) as the dependency that most
unlocks the rest, from the state of the worlds (`_d6`/`_d7`, ten seeds at year 300):

- **Trade is thin.** Most villages see 0-5 traders a year; 0-12 markets a world (C1.6's ten a year); D2-115 deferred cargo boats and docks
  (C9.4) because there is nothing to carry: barter between villages needs a double coincidence of wants - a grain-rich village spares grain, a
  hungry one spares nothing back - and timber, stone, fibre and hides differ little between neighbours on a 6 km world.
- **Every household makes everything itself.** Goods (clothes, tools, vessels, baskets, nets: `Goods`, D2-058) are made at home by each
  household's own hands, ~0.8 hours a person-day; a skilled weaver (`KLoom`) only works faster for their own household. Nobody makes for
  others but the smith (C1.4: metal into the village's store, arms) - so there is nothing to exchange within a village and little between them.
- **Lordships rarely hold** (0-2 a world, almost all with one tributary): C10.2 (the political centre) waits for polities that last.

So C3 begins where the smithy left off: **people of skill making for others at a workshop, their wares feeding their village and its
neighbours** - the specialisation that real exchange rests on. No profession is assigned: a maker is whoever has the skill and spends their
hours at the workshop because the village's wares run short; their role is read from their hours (the person page).

## 1. C3.1 The weaver's workshop and the village's wares (first slice; built in session 12, D2-119)

**What the worlds said before any code** (the `_d7` saves at year 300, `place --standing`/`--list`, `timeuse`): households' goods sit at their
target (1.17-1.20 a head against 1.2 in three quarters of households), grown people idle ~8 hours a day outside the harvest, and making and
mending takes ~0.5 h a person-day - **goods are never short and home making costs only spare hours**. So the first design (wares valued by
"goods short a head", a workshop where wares run short) would have made neither need nor trade. What is short is **fibre**: 0.1-1.2 kg a head
in store against 2.5 kg a person's goods (1 kg a field-hectare a harvest, used as fast as it comes) - and every village knows the loom.
The slice was rebuilt on that:

- **Wares last** (`Weaving.WaresWear` 0.6): close-woven, fulled cloth and a practised maker's goods wear at 0.6 of homespun's rate - the same
  flax clothes 1.7 times the people. A household holds wares as part of its goods (`Household.Wares` <= `Gear`); its members wear the mix
  (`Goods.Wear`).
- **The weaver's shed** (`Structures.Workshop` 23, Zone Edge; 220 h, 35 loads; kit `weavers_shed`): raised by a village that knows the loom
  where its people's wear of goods would keep six makers at its looms all year (`MakersFrom`; ~165 people with stone tools, more with metal) -
  larger places only - after water and a store, never while walls rise. Cause: the loom's technique event (or the founding).
- **Its makers** are the village's eight best weavers (`Looms`; `Settlement.LoomBar` read daily from loom + practice, ties to the lower id), so
  the same people work there from day to day; lived, a choice by day (u 0.5) to `ActivityKind.Weave` at the shed; COARSE/DEEP, a labour day
  there before the shared field work (the smithy's law). At the shed: `Pace` 1.5 x a home maker's rate x (0.8 + 0.4 practice); practice
  (`Person.SkCraft`) grows by the hour and their role is read from it ("a weaver at the weaver's shed"). Wares take real fibre (2.5 kg each; no
  improvised cloth). Makers make up to `Target` = 0.15 a head + what was sold lately (halving each year).
- **Households take wares** from the store before their own hands make more - in the hearths' day after the wear, and before any making
  choice at both resolutions (`Weaving.Draw`, counted in `Settlement.WaresOut`).
- **Trade**: `Trade.Wares` (3 kg a ware) valued at what a ware saves a village whose people wear none: ~300 hours of home making over its
  life at the wild's ~0.09 days' food an hour (the marginal product of spare hands) - `0.5 + 25 x (1 - share in wares)`; spared beyond 0.1 a
  head; sales counted (`WaresSold`) and made again.
- **Pages**: building (its looms by name, wares made, store against target, taken, sold, fibre), person ("a weaver at the weaver's shed" from
  practice; the household's goods and how many are wares), settlement ("Cloth and goods").
- **Done when**: sheds stand in larger villages only; their wares are worn by households that did not make them (home making hours fall);
  wares are traded to villages without sheds; ten seeds at par in people and food or better; shots of the shed; tests at both resolutions.

**Result (D2-119, ten seeds against the same binary without sheds):** trades x3.0, trade gain x3.8, home making hours x0.28, market places x1.35,
people at par; wares go mostly for fibre (the flax chain), little grain - cloth does not feed a town in these worlds. Towns of 500+ cannot keep
up with eight looms (C3.1b).

## 2. C3.2 The potter's kiln, the village's jars and its hearths' pots (built in session 13, D2-121)

**What the worlds said before any code** (the `_w7` saves at years 100 and 300, `place --stores`, `run --rot`):
- **Pottery was free wherever it was known.** D2-063 kept a village's store at 0.65 of the open store's rot because its *best potter* could
  fire jars, and a household cooked on 0.8 of the wood because *one of its people* could fire pots - with no clay, no fire, no hours. Pottery is
  known in nearly every village (all but a few in one world), its best 1.00; 70 % of people are competent potters, 98 % of households hold one -
  and so 25-60 % of villages, those with **no clay within reach**, kept sealed stores and cooking pots they could not have made. On the person page
  every adult was "a potter".
- **Stores are large and rot is real**: 3-7 years of need in store at year 100 (21-57 % of the harvest rots), 0.4-1.4 years at year 300 (6-12 %);
  14.6-17.3 % of every harvest over 300 years. **Cooking burns 72-91 % of the wood** a village burns; woodlots have receded past 1 km.
- **Clay by the day's flows flickered**: the test (a stream or lake within 1.2 km by the day's flow and depth) took a village's clay away in a dry
  spell. By the ground's shape - a reach draining 200 ha or more (a river with a floodplain) or a depression (a lake's bed) within 1.2 km - the
  counts agree with it (s42 15 vs 15, s3 7/7, s1 2/2, s6 12/10, s4 26/20, s2 22/15 villages), every day.

**Design** (`Core/Places/Pottery.cs`; the weaver's shed's pattern, C3.1):
- **Clay** (`Learning.Clay`/`ClayCell`): ground within `ClayReach` (1.2 km) where a reach drains `ClayAreaHa` (200 ha) or more, or a depression -
  from the drainage only (cached by its version; D2-119's rule), wet or dry this season. Used by discovery, founders, building materials, kilns and
  hearths alike.
- **Jars** (`Settlement.Jars`, kg standing in its granaries and barns; a kg seals `JarHolds` 4 FU): the store's rot is `1 - Seal x sealed` of the open
  store's (`Seal` 0.35 - D2-063's 0.65 when all is sealed), sealed = jars x 4 / the grain in its granaries (grain in heaps beyond them never in jars).
  Jars break at `JarBreak` 5 % a year. Large jars want a kiln's even fire: they come only from a kiln - made at home or bought.
- **Hearths**: a household one of whose people can fire pots (competent) where its village has clay fires its cooking pots among its goods (D2-058's
  hours) - D2-063's 0.8 of the wood by its best potter, as before; one that cannot cooks in **kiln pots** it holds (`Household.Pots`, kg; `PerHead` 2
  kg a member) - drawn daily from its village's ware (`Pottery.Draw`), broken with use (`PotDays` 400) - or on the full wood. What its hearths still
  lack is known (`Settlement.HearthShort`).
- **The kiln** (`Structures.Kiln` 24, Function Craft, Zone Site; 120 h and 15 loads; kit `kiln`, `clay_pit`): raised at its clay (the nearest clay
  ground, off the river's bank and dry, or the village's edge where the clay is in it) by a village that can fire pots, with clay within reach, a
  store, no walls rising, where the jars its granaries' grain lacks would keep a potter at work for a month (`KilnFrom` 720 kg, ~2,900 FU unsealed).
  Cause: the pottery technique's event (`Settlement.PotEvent`), else the founding.
- **Its potters** are the village's `Hands` (4) best by skill and practice (`Settlement.PotBar`, `Person.SkPot`) - lived, a choice by day (u 0.5)
  to `ActivityKind.Pot` (36, `WhyCode.Pot` 46) at the kiln; COARSE/DEEP, a labour day there less the walk out to the clay (`Stone.AfterWalk`).
  `KgPerHour` 2.5 x (1 + 0.6 skill) x (0.8 + 0.4 practice) x health: clay dug and worked, thrown and coiled, dried and fired, on the village's wood
  (`WoodPerKg` 0.05 loads; none in store, no firing; the cutters cut for it - `FuelDay`). Up to `Target`: the jars its grain lacks + the pots its
  hearths lack + `Keep` 1 kg a head + what it sold lately (halving each year). Ware in store stands in the granaries as jars as far as the grain
  lacks them, after the hearths have drawn (`Pottery.Day`).
- **Trade**: `Trade.Pots` (7, kg) valued `0.3 + 2.5 x unsealed x (a store worth sealing) + 0.6 x the hearths' lack` - a kg seals 4 FU, keeping
  ~3 FU over twenty years; spared beyond `Kept` 0.5 kg a head. Clay-less villages buy jars and pots from kiln villages.
- **Pages**: the building page (its potters by name, ware fired in hours, jars and the grain they keep sealed, ware against target, pots taken,
  sold), the person page ("a potter at the kiln" from practice - no longer everyone who can fire a pot; how the household cooks and on how much
  wood), the settlement page ("Pots and jars"). The view: the kiln (beehive dome, drying shed, wheel, fuel) with smoke from its vent while a potter
  works at it; the clay pit behind it, wider as the ware fired grows; ware in straw while it holds ware to sell; sealed storage jars by its
  granaries while its jars keep its grain. Headless: `pots:` line, `--nokiln` (D2-063's rules on the same binary), `--potsflow`, `place --stores`.
- **Done when**: kilns stand at the clay of villages with grain to seal; jars keep their stores; hearths without clay cook in bought pots; ware goes
  from clay villages to clay-less ones; ten seeds at par in people and food against `--nokiln`; shots; tests at both resolutions.

**Result (D2-121, ten seeds against the same binary with D2-063's rules, `--nokiln`):** at year 300 84 kilns in 169 villages (113 with clay), 334
potters; 8,366 t fired, 95 % of it stood as jars; jars seal 80-100 % of the grain in granaries; 68 villages sealed by bought jars (a town buys from
the potters' villages round it); 1,988 t of ware traded, mostly for grain; 6 % of hearths without pots. Trades x5.4, trade gain x5.4, **markets x3.6**
(23 -> 99), market places x3.2 - pottery is now most of the world's trade; rot at par (real jars seal what D2-063 sealed for nothing), wood felled
x1.03, people, births, deaths at par, famines inside the noise. A kiln at a month's work went to hamlets of 25: two months' (5,800 FU unsealed).

## 2b. The village smith: a smithy's places and roles from practice (C1.4b, D2-125)

**What the worlds said before any code** (`run --crafttally`: the hours each person spends at each craft, by village-year; seeds 4 and 7, 300
years): the person page called **5,730 of seed 4's people "a smith" and "a boatwright"** at year 300 - everyone who could work copper or knew the
planks' craft - where 175 had worked at a smithy in two years (22 for 50 hours or more) and 10 had built a plank boat. Weavers and potters were
already read from practice (C3.1, C3.2). Worse, the smithy itself had no smiths: every person of the village who could work copper worked at it in
turn, in the order its people happened to come - smithy-years had a median of 35 hours and 3 hands, but up to **24,000 hours and 355 different
people in one year** (iron for a town's tools at the bloomery), and the busiest hand did 6 % of a smithy's hours. A lived day sent a crowd to the
forge.

**Design** (`Core/Places/Forge.cs`; the kiln's pattern, C3.2):
- **A smithy's places** (`Forge.Places` 4: the hearth and its bellows, the anvil, the bloomery's furnace) are its village's four best smiths by
  `Key` = craft in copper + craft in iron + practice (`Settlement.SmithBar`/`SmithBarId`, read daily and when the smithy is raised; ties to the
  lower id) - able smiths only (copper competent, 16-59). Only they work there, at both resolutions; the work a village wants (tools, iron, arms)
  takes as long as four can do it.
- **More smithies where the smiths cannot keep up** (C3.1b's pattern; with four places alone, iron towns smelted 40 % less and sold a third of the
  iron): a day the work wanted at its smithies (`Forge.Backlog`: copper and iron still wanted as far as its ore and charcoal go, at its best
  smiths' pace, and the arms' forging) is more than all its places' labour day is a short day; a village short on 30 days of a year **whose steady
  need outruns its places' year** (`NeedsMore`: tools worn out at 3 % a year, the iron it sold, its arms' upkeep - a first equipping is work done
  once, over some years: counted on short days alone, nearly every iron village raised three or four smithies) and with more able smiths than
  places raises another smithy (up to 4), its next best four smiths there; each smith works at the smithy nearest home.
- **Practice** (`Person.SkSmith`, `SkillRate` 0.0015 an hour at the smithy or the open hearth) quickens the hand: `Hand` = 0.8 + 0.4 x practice (the
  potter's and the weaver's law) on copper and iron alike.
- **Roles from practice**: "a smith" or "an ironsmith" (one who knows the bloomery) from 0.3 of practice (some 200 hours), "at the smithy" while
  they have a place there; "a boatwright (N plank boats)" from the plank boats they led the building of (history keeps the leader as the boat's
  record's Other) - knowing a craft is not practising it. The building page names its smiths ("practised" or "new to it"); the settlement page
  counts them.
- `--nosmithcrew` (`Forge.CrewOff`) gives the smithy's law before on the same binary (it reproduces `_c3` exactly).

**Result (D2-125, ten seeds against the same binary with the smithy's law before, `--nosmithcrew`):** people, famines, vital rates, trade and
conflict at par; three of the ten worlds work metal - in them the smiths read from practice are 116, 80 and 24 people, all of whom worked, the
busiest hand does 38-49 % of a smithy's hours (6 % before) and a smithy-year has at most 23 hands (355 before); standing smithies 68 -> 80 (three
towns of seed 4 keep two); iron smelted x0.59 / x1.05 / x2.08 by world, iron traded 22 -> 9 t - seed 4's exporter can no longer smelt beyond its
places for buyers it cannot see (open: an unmet ask, and the bloomery at the ore with its own crew, C5.3c).

## 2c. C3.3 The household's grain, what households owe, makers paid in kind (D2-126)

**What the worlds said before any code** (session 16; the tools ledger extended to household-years with their kin households -
`run --ledger Y --ledgeryears F`, seed 4 `_s3` from year 200, 56,240 household-years; `ly.py`/`lycf.py` in the session scratchpad):
- **"Who brings in the harvest" is an artefact of the labour schedule.** Dividing the harvest by each household's crop work (the session-13 survey's
  rule) made households with *no* member of 16-59 bring in 64 FU a member against 40 for young couples, households of the old 2x what they eat and
  workshop makers 0.3-0.7x - because a COARSE day walks the village's residents in id order and the oldest fill the whole crop need before the
  young reach the fields (lived days spread it more evenly). Takers had *more* workers a member (0.60) than givers (0.52). Property built on that
  split would make an artefact into wealth.
- **Divided by mouths** (the commune's law for fields worked in common), households are mostly self-sufficient year to year: deficits fall to 8 %
  of what they eat, largely covered by their own carry-over; differences come from what each brings home of its own (milk and meat of its beasts,
  its catch), from household formation and growth, and from work for others. Kin households exist for 82 % of households; kin could cover 43 % of
  the survey's deficits.

**Design** (`Core/Places/Holdings.cs`; one physical granary as before - this is whose grain it holds and what is owed):
- **Bins and the common grain**: `Household.Grain` (FU) in the village's granary and `Settlement.Common` (the village's own). The harvest is shared
  among its households by their mouths as it comes in (households setting out take no share); what a household brings home beyond its larder - its
  beasts' milk and meat, its catch - is its own; the first store (world generation) by mouths; a married child takes their mouth's share of the
  parents' grain (same village); an heir takes the grain, the debts and the claims of a household that died out (none: the village's, debts lapse);
  households setting out carry their own grain first (the old rule's amount; what they cannot carry is left to the village; their debts lapse); a
  party's food is its households' at their new home.
- **The account** (`Reconcile`, daily before the larders and at a harvest): an outflow nobody drew (rot, theft, a sale, relief or tribute given, a
  visitor's meal) is borne by all who keep grain in the store by their shares; an inflow nobody brought (bought, given, paid, returned, tribute
  received) is the village's common grain.
- **The moral economy** (`Draw`, the daily larder fill - the village's ration as before): its own grain, then the common grain (unreckoned: the
  village feeds those who lack), then what its close kin can spare beyond their own needs until the next harvest and six days (a gift, unreckoned;
  kin = its members' parents', children's and siblings' households), then a loan from those it owes already and the households with most to spare
  (`Debt`: debtor, creditor, FU, since); what is still wanting the village's ration takes from every bin by its share. Repaid after each harvest
  from what a debtor holds beyond a year's needs and six days, the oldest debt first (`Repay`). A meal taken straight from the store (an empty
  larder) is its own grain, then the common grain (`Take`).
- **Makers paid in kind** (C3.3b): a maker's make (`Household.WaresDue`, `PotsDue`) is theirs in the village's store until someone takes it; a
  household that takes wares or kiln pots pays their makers, by their shares of what is theirs in store, **the food of the maker-days it took**
  (`Price`: a ware at the village's best loom's pace, a kg of ware at its best potter's - a day's bread for a day's work) from its own grain, and
  takes no more than its grain pays (`CanBuy`: no credit - with takers owing, every taker owed a sliver to every maker and the debts table held
  206k entries; a household short of grain makes its own goods or cooks on the full wood); goods nobody here made (bought in) were the village's and
  their worth goes to its common grain. What the village uses in
  common - jars stood in its granaries, a smith's hours at its tools and arms (`Commission`, at the smithy or the open hearth), wares and ware it
  sells abroad - it pays from its common grain, then by a **levy** on every household's grain by its share (the smith's own excepted): the first tax.
  Common duties anyone does (the fields, wood, building, quarrying, mining, charcoal) are not paid: the commune's.
- **Means** (D2-114) reads the household's own grain on D2-114's scale (FU against 365 a member - D2-114 read days of a 365-day year where a TC2
  year is 48; kept so the plans keep their calibration).
- Pages: the person page's home section ("Its own grain in the village's granary: N days' food...; It owes ... to the Xs and N other households,
  since year Y; It is owed ... by N households it lent to"), the settlement page's "Grain:" line (its households' and its own grain, the spread
  from least to most a household, its debts and to whom most). Headless: `hold:` and `makers paid:` lines, `place --households` (grain, owes,
  owed), `place --debts`, `--nohold` (the store as before: reproduces `_s3` exactly), `--holdmeansoff` (the accounting alone), `--nopay`.
- **Hard times** (C3.3c; Dirks 1980 - in a mild shortage sharing widens, in a deep and lasting one it contracts to the household and close kin):
  below a village ration of 0.7 (`Contract`), a larder and a meal from the store get only the household's own grain, the common grain, what its kin
  spare down to the village's ration and loans of true spare (`Provide`, `Available`); nobody's own need is taken.
- **Done when**: the accounting moves no grain (with D2-114's means it reproduces `--nohold`); debts are run up in need and repaid from surplus;
  makers' households are paid by those who take their make; ten seeds at par in people and food; tests at both resolutions.

**Result (D2-126, ten seeds, each step against the last on the same binary, the slice against `_s3`):** people x0.976 (t -0.6), vital rates at
par, hunger deaths x0.96; the accounting alone moves nothing; makers paid as far as takers' grain pays: home making x1.19, ware traded x1.38 (unsold at
home, it goes abroad); hard times: famines x1.22-1.25 (more villages lose someone to hunger while their store holds others' grain - Sen's famine
amid plenty), hunger deaths x0.90-0.95, takings, blows and raids x0.5-0.9 (fewer stores run dry), relief x1.1-1.6. At year 300 grain a member
spreads Gini 0.51-0.77; 6,458 of 15,979 households owe (mostly a season's credit: seed 4's median debt is 0.8 years old); creditors are small
households of elders with grain and beasts, one owed by up to 159; makers' households hold more grain a member than the others in all ten worlds;
98 % of hunger deaths fall in households with no grain of their own. Open: debts' consequences (work, flight, standing), household land, households'
own trade, taking from one's own village's store.
**Corrected (D2-129, measured in session 16):** "famine amid plenty" does not describe these deaths. At each hard-times hunger death the households
with grain could spare, beyond their own needs at the village's ration to the harvest, under a quarter of what all the grainless households needed in
95 % of ~40,000 deaths over ten worlds (mean 0.04-0.11 of it; enough for all of them in ~30); the store held under a day of the village's food in 69 %;
the deaths fall 5-12 days before the harvest. Contracted sharing decides who goes hungry in a real shortage, not whether there is one; hunger was
chronic (in 120-177 of 200 years in half the worlds) because nothing in the household's life answered scarcity before death did - C3.5 (marriage
waits for the means of a household, D2-129).

## 3. Later slices
- ~~**C3.1b more looms for towns**~~ (done, D2-120): another shed where the households drain the store most of a year with fibre to weave and
  the people in wares would outwear the looms; eight more makers a shed, each at the shed nearest home; towns of 500+ carry 2-4.
- ~~**C3.2 the potter's kiln**~~ (built, §2, D2-121).
- ~~**C3.3 exchange between households**~~ - **first slices built in session 16 (§2c, D2-126)**: household grain by mouths, the moral economy,
  debts, makers paid in kind, hard times. The session-13 survey below is kept for its numbers (its rule - the harvest by crop work - was found to be
  an artefact of the COARSE schedule). Still open of the phase: household land and labour for others, debts' consequences, households' own trade.
  Session 13's survey: **surveyed in session 13, deferred as a phase of its own**: a household's holdings (its own grain,
  wares, beasts) and payment in kind for what it takes - the makers paid; real household wealth replacing D2-114's `Means`. Measured with a
  tools ledger (`run --ledger 250`: each household's share of the harvest by its crop work, its wild food and herd produce, against what its
  larder drew from the store; seed 4, years 250-300, 5,861 households; the run is the same run): **the village's store moves 31 % of all the
  food households draw** from the 43 % who bring in more than they eat to the 57 % who bring in less (net a person-year by decile -29/-17/-5/
  +12/+46 FU against ~40-48 eaten); the givers' surplus is ~69 FU a year each and most of it feeds the takers; workshop makers' households are
  takers (the village feeds its craftsmen). So a household's own grain without the village's sharing would starve most households: C3.3 needs
  a **moral economy** - sharing by need kept in shortage, and gifts or debts recorded for what the takers drew (young families in debt, older
  households with grown children wealthy: the household's cycle) - and, under it, household fields (who works which ground), inheritance of
  land and claims, labour for others. That is a phase (land, property, debt, labour), not a slice; it is weighed again with C10 (lords' rents).
- **C3.4 the mill** (water or animal): grinding for the village, the miller's toll; a quern's hours freed. **Surveyed in session 15 and deferred**
  (`timeuse --load`, seed 4 `_s3` year 300, two lived days): grown people spend **0.7 h a day preparing food** - grinding and cooking together, the
  household quern already quickening it up to 1.8x (D2-062) - and **7.9 h idle**; a mill would free spare hours only (C3.1's first design fell into the
  same trap). It waits until labour binds (a town's hands short at harvest) or comes as the lord's toll-taking mill (C10). The streams could drive one:
  0.08-0.23 m3/s at most (D2-124) is a small horizontal mill's water.
- **C3.5 apprenticeship**: skill passed by working beside a master at a workshop (not only by being near: D2-062).
- **C3.6 workshops of other crafts** (carpenter, tanner, cooper) as their goods are needed.
Then C9.4 (cargo by sea) and C10.2 can be weighed again against the traffic.
