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

## 2d. C3.6a Working the land harder: more ploughing and hoeing where the land within reach is short (D2-131)

**What the worlds said before any code** (session 16, `--landtally`: harvests and COARSE working days by whether the village's arable within
reach is short of its people's need, by crop stage; seeds 4 and 5, years 200-223): land bound 80 % and 62 % of harvests, and there labour
never limited a stage (sown 1.000, brought in 1.000) - a working day kept 2.7-4.0 hours after the crops in every stage, and the hungry spent
0.6-3.1 of them in the wild at 0.02-0.08 FU an hour against ~1.7 in the fields. The crop's work had a fixed need (40 h a ha to sow, 24 to hoe,
45 to reap): an hour beyond it gave nothing. Who holds the land would change who eats (D2-129: the hunger is a real shortage), not how much.

**Design** (`SettlementSystem`, sim version 66):
- **The plan at sowing** (`TilthFor`): a village whose fields are short of what its people want to sow (heads x 0.5 ha, more where famine is
  remembered) plans enough extra work a hectare to bring the harvest of its hectares up to what the wanted ones would give, up to `TilthMax` 0.8
  (+80 % of the ordinary season's 109 work-hours a hectare). None while its fields are enough (Boserup: the extra hour returns less, so people
  take it on only under pressure). `Settlement.Tilth`.
- **Falling returns** (`GainOf`): the work done adds 0.30 x (1 - exp(-e / 0.4)) of the yield (e: extra hours as a share of the season's) - the
  first extra hour three quarters of an ordinary hour's harvest, the last a tenth; at the most +26 %. Half the extra goes to the sowing (a second
  ploughing, the harrowing), half to the hoeing (two more hoeings): each stage's need rises; the ordinary work is done first; only the extra done
  counts (`TilthDone`; `TilthGain` the last harvest's share). The ordinary weeding's effect on growth is unchanged.
- **History and pages**: `FieldsWorkedHarder` once when a village begins (cause: the famine it remembers, else its people), told again only after
  its land covered its want with a tenth to spare; the settlement page's food section tells the land against the want, the extra hours and what
  the last harvest gained. Tools: `--notilth` (= `_m2` exactly), `--landtally`.
- **Done when**: the extra work is planned only where land binds, done in both resolutions, and the harvest gains only what the work done gives
  at falling returns; tests; ten seeds against `--notilth`.

**Result (D2-131, ten seeds, `_t1` against `_t0`):** in land-bound villages the extra planned was 0.41-0.63 of the season's hours and all of it
was done (lived days too); yields a hectare +16-24 %; the harvest 0.83-0.97 -> 0.91-1.02 of the people's bread; 14-18.5 % of the harvests came of
the extra work; crop hours +0.4-0.8 h a working day in sowing and hoeing. Then the people grew into it: years 200-299 x1.17, year 300 x1.27 (every
world x1.19-1.45), the land bound harder again, **hunger a person-year unchanged** (x0.98), famines a person-year x0.78, raids x0.37, wounds a
person-year x0.77; run time unchanged. Boserup and Malthus together: density and towns, not welfare - the preventive check (marriage on a
holding) or the manure flow would change that (C3.6b, §3).

## 2e. The first sheaves: the hungry gap (D2-132)

**What the worlds said** (`--gaptally`, ten worlds, years 200-299 after C3.6a): 66 % of hunger deaths fell in the last nine days before the
harvest's day - the ripe stage - with the store under a day's food; mostly children. The whole harvest came into the store on its last day while
villages reaped through the stage: they starved beside their sheaves.

**Design** (`SettlementSystem`, sim version 67): the grown crop's yield is known when it ripens (`CropYield`); at each day's start in the ripe
stage the reaped share of it comes into the store and the households' bins by mouths (`Sheaves`; debts wait for the harvest's day; sheaves in the
store are not burnt with the stubble); the village rations to its next new grain - the first sheaves on day 31, and once ripe, the crop still
standing counted and rationed to next year's (`ToBread`, read by the kin's spare and the marriage rule too). The settlement page tells the
reaping. Tools `--nosheaves` (= `_t1` exactly), `--gaptally`.

**Result (D2-132):** the deaths beside the sheaves are gone and the gap falls where the year's did, before the grain ripens; hunger a
person-year is not lower (median 3.9 -> 4.7 a 1,000 person-years, mixed by world), people x1.05-1.08 - the children saved grow the villages and
the same food brings the check back a fortnight earlier. With D2-131 this closes the question the hunger kept asking: in this world hunger a
person-year is set by births against food, and the next lever is a check on births before the food fails (household land, marriage on a holding).

## 2f. C3.6b Household land: each household's holding of its village's fields (D2-133)

**Why** (D2-131, D2-132): more food a hectare or a better-timed harvest grew the villages until hunger a person-year was back - it is set by
births against food; what held births back in the medieval north-west was the household's land (no holding, no marriage). Measured first
(`place --heirs`): men marry at 19-23, a man's father dies when he is 22-24, a family raises 1.55-1.93 sons to 20; one heir would leave about
two in five men landless where land binds.

**Design, three steps** (`Holdings`, sim version 68):
- **C3.6b holdings (built, D2-133)** - `Household.Land`: founders by heads; a marrying child a portion of what the family's land spares beyond
  its remaining people's want; the last out of a household the rest; an heir the land of a household that died out (else the village); those who
  set out leave theirs; at sowing land lost is lost from all by the same share and unheld land goes to the least landed first. One heir, portions
  of spare land: the north-western custom (partible customs later with a village's tradition, C11). Pages: the household's land, the village's
  "Land:" line. Nothing else reads it: the annals of ten worlds are byte-identical. At year 300: 32-60 % of households landless, Gini 0.74-0.92;
  the largest holders are the villages' creditors.
- **C3.6c the harvest by land and labour (built, D2-134)**: the land's half to those who hold it (the unheld land's part the village's), the
  labour's half to the hands that worked (`Hands`: grown people a hand, youths 0.6, the old half, by health; the commune still works its fields
  together; the half-share of the metayer, `LandShare` 0.5). Ten seeds against the same holdings by mouths: hunger a person-year x0.74 (t -2.8),
  births and deaths a person-year x0.98 and x0.96, people at par; the landless took half their mouths' share, those with their want 1.6-2.3; women
  married later (20.6-22.0 -> 20.5-22.8) and more never (by 45-59 94-100 % -> 67-100 %) - the preventive check from the households' economy.
- **C3.6d who leaves (built, D2-135)**: each household weighs leaving by its own land (its holding and its part of the unheld, against its want)
  in place of the village's crowding - the landless and land-short choose to go, the landed stay. In full lands most find nowhere: every aggregate
  at par, and the land concentrates (landless 17-66 % at year 300, Gini 0.70-0.93).
- **C3.6e the old hand on their holding (built, D2-136)**: over 900 years the land-rich were the old (households whose youngest is 55+ held 63-89 %
  of the land-rich's land) keeping decades of grain; now a landholding household whose youngest is 60+ goes to live with a child's household (else
  a land-short household with hands, for its keep), its land and grain with it. Landless at year 300 17-66 % -> 6-44 %, Gini 0.70-0.93 -> 0.50-0.65,
  the hoards gone; births a person-year x1.06 (heirs marry as the land comes), hunger at par.
- **Next of the phase**: service in landed households (needs each household to work its own holding), a land market, partible customs (C11), the
  land's half as rents (C10), towns (C13).

## 2g. C3.6f The soil: fallow and the herds' dung (D2-139, built in session 16's fourth context)

**Built (D2-139, sim version 74)**: `Settlement.Tired` (the fields' tiredness), `TiredSeen`, `Rest`, `Grazed`; `SettlementSystem.Heart`, `SoilYear`,
`HaPerHead` (the land judged by what it bears), `Rested` (history `FieldsRested`); the settlement page's soil line; `--nosoil` (= the worlds before,
byte-identical), `--norest`, `--nodung`. Ten worlds: they carry ~0.6-0.7 of their people (full lands at ~0.6 of the output a hectare of arable);
bound villages rest 13-17 % of their arable, a sown hectare bearing 0.73-0.79 of a fresh crop - **but births did not answer the poorer land** (42 a
1,000 person-years before and after) and hunger a person-year rose ~x1.9: the marriage rule (C3.5) does not bind (couples bring two years of bread
on average). Next: the household's niche (a couple marries when it has a living). The research and design below are as written before the build.

**Why.** The fields have no fertility: a harvest is `FieldHa x FieldYieldPerHa x growth x sown x (1 + tilth)` - every hectare bears a fresh
field's crop every year, cropped without rest, forever. So nothing rests, a beast is worth its milk and meat but nothing to the land, and a
household's surplus has no productive use (C3.6e). Measured at year 300 (`_k2`, the `land open/bound:` lines): in villages whose land binds,
arable is 0.68-0.75 of their want, every arable hectare is sown, working harder adds 10-15 % of their harvests, and they keep 0.34-0.42 beasts a
field hectare (1.96-2.31 kg of their intake a field hectare a day) against 0.69-1.11 (4.2-6.6 kg) where land is open; 98 % of hunger deaths fall
on landless and land-short households.

**What the medieval record says.** Pre-modern arable needed rest: fallow rebuilt nitrates and cleared weeds (Overton); only a half to two-thirds
of the arable was ploughed in a year (two- and three-field systems). Postan: arable spreading over pasture under pressure meant fewer beasts, less
dung and falling yields (Titow's Winchester yields fell in the later 13th century) - disputed by Clark (1992: cultivators knew pasture restored the
land, their horizon was short). Beasts make no nitrogen: they move it from the pasture and the waste onto the fields (sheep folded on the fallow;
stubble grazed after harvest). Continuous unmanured wheat does not fall to nothing: Broadbalk's unmanured plots settled at about half the yield of
nitrogen-fed ones (rain and free fixation feed a floor).

**Design sketch (to be measured and decided).** One number a village, `Settlement.Soil` (the fields' heart, 1 = land first broken; fresh land
claimed comes in at 1 and mixes by area - an assart is fertile): a harvest takes from it by the crop it bore; a floor of rain and free fixation
gives back a little each year; land at rest recovers toward 1; the herds give back by the grass they ate off the waste and the stubble (dung, a
share of their intake), so beasts carry the waste's fertility onto the fields. A hectare bears `a + (1 - a) x Soil` of a fresh field's crop
(`a` ~0.4: the floor with old varieties and weeds). With that, per arable hectare: cropped every year ~0.4 of a fresh crop, two fields ~0.48,
three fields ~0.59 - so under pressure the three-field rest wins, as it spread in the 12th-13th centuries. **Rest is the village's choice from its
own harvests** (as famine memory is): where land is open, what lies beyond the want rests anyway; where it binds, a village rests a share when its
remembered yields a hectare fall below what rested land gave - no rotation is written in. The cells resting each year follow from the village's
rest share and the year (derived, never stored), so the view can draw stubble, fallow grazed by the herds and the sown strips from state.
**The crux is the world's scale (D2-085)**: today's constant is a fresh field's crop on every hectare every year; with a soil, the output a
hectare of arable falls to ~0.5-0.6 of it - either the constant becomes "a fresh hectare's crop" and is raised so the rested regime keeps the
worlds' scale, or the worlds shrink. Measure first (the 900-year check; a survey of what each village crops and what its herds eat), then decide.
**What it would open**: beasts worth buying with grain (households' own trade, D2-126 "Open"; the commons' grazing; the landed manure their
holdings), the Postan squeeze where land binds, legumes and the three-field course as knowledge (C11), fields that read as a landscape.

## 2h. The household's own year: the garden, the threshing floor, the muck (session 17 - C3.7a the garden built, D2-143)

**Built (D2-143, sim version 76)**: `Core/Places/Gardens.cs` - the croft measured from the layout (columns of 2 m rows behind, 4 m columns beside;
~0.76 of the free ground the 2 m cells find), dug, weeded and gathered by lived and COARSE days, its produce into the larder; the view's beds on
the croft's own rectangles; `--nogarden`. Ten seeds at par; at year 300 83-86 % of households have a garden (mean ~130 m2); a lived year: 0.3 of
the 12 daylight hours in the garden, nearly all from idling; under 1 % of the food. The research and design below are as written before the
build (its figures for the croft's yield, 2-4 %, assumed the record's croft; the layout's crofts and the season's growth give less).

**Why (measured).** Arjun's playtest saw people standing still (`27`). Presence now draws what they do, but much of what they do is nothing:
`timeuse --load s4_v3/world_y300.tc2 --days 4 --daylight --stages` (seed 4, year 300, the sowing and growing stages) - grown women and men are
**idle about 7 of the 12 daylight hours** (Idle 7.0 / 6.8), in the fields 0.4-0.9 h, building 0.6-0.7, charcoal 0.7, cooking 0.5, weaving 0.3;
in the ripe stage the fields take 10 of the 12. The 48-day year gives a grown person ~576 daylight hours; field work at 109 h a hectare fills
about a fifth of them, nearly all at the harvest. The household's own work of a medieval year is missing from the scheduler - and one of it is
already drawn: **every lived house has a kitchen garden in the picture (D2-114) that nobody works and that feeds nobody** (a visual-truth fault).

**The record.** Peasant houses stood on tofts with crofts and gardens: leeks, cabbages, onions, garlic, peas and beans, herbs, fruit, hemp and
flax; Dyer (*Food in Medieval England*, and *Everyday Life in Medieval England*) treats garden produce as an integral part of the economy and
of the pottage, relatively more important to the poorest households. Threshing by flail was the sheaves' long tail: 4-8 bushels of wheat a day
(110-220 kg), more of barley and oats (Britannica; Leicestershire archive); early-14th-century manors paid ~3d a quarter of wheat threshed
(Merton, SAL MS 555) - about two days' wage. Dung was carted from the yards and byres to the fields before the ploughing.

**The scale in this world.** A food unit is a person-day and the year is 48 days: work paced by eating (cooking, water, threshing) runs at real
daily rates, work paced by the year (sowing, reaping, a garden's digging and weeding) is compressed ~7.6x into the short year. So threshing a
household's grain is ~0.03-0.06 h a FU - ~0.2 h a day for five; **a garden is ~100 work-hours a household-year (~2 h a day in the growing
season) for ~5-10 % of its food** - the larger, the more visible, and the one the picture already promises.

**The garden's size is the crux.** The bed the view draws (25-45 m2) would feed a household under 1 % of its year (a mixed garden of pulses,
cabbage and leeks: ~500 kcal a m2 a year) for a few hours' work - a token. The record's gardens were the croft behind the toft (a tenth to
half an acre). In these villages the free ground behind a house runs about its frontage (~15 m) by 15-20 m to the back lane (C13.2) - **a croft
of ~150-300 m2: 4-8 % of a household's food for ~45-110 work-hours a year** (spade work, ~0.2-0.5 h a m2). So the garden must be measured from
the village's own layout - the free ground behind and beside the home (sampled with `Footprints.YardFree`, up to a croft's size; none in a town's
built-up core) - kept as state, and drawn at its true size: strips of garden behind each house, the medieval village from above.

**Design sketch (C3.7a the garden; to be measured and decided).** The garden is the household's own: its ground the croft behind or beside its
home where the layout leaves it free (above), measured when the year turns and kept with the household. Each year: digging and sowing in the sowing
stage (hours a m2), weeding and watering in the growing stage, the gathering from mid-growing into the ripe stage; what it bears is its area x
a yield a m2 x the weather's growth (the village's crop growth) x the share of its work done - the produce comes into the household's larder
as it is gathered (pottage greens and pulses in summer, keeping roots and cabbages later). Its work is a new activity (`Garden`), the
household's own, scored as the household's work is (by what the season still wants of it; the grown first, children helping), drawn by
`Presence` as hoeing and stooping in the garden bed (not at the door). The view's bed shows its state (dug, sown, green, gathered). Tools
`--nogarden` (the control: byte-identical to the worlds before), a tally (hours, produce, households with and without ground). Tests: a
household works its garden in season and its larder gains; a household with no free ground has none; the weather's growth reaches it.
**What it should change**: idle daylight hours fall where it is real work (measured with `timeuse --daylight --stages`), the landless'
hunger (D2-134) falls a little where they have a garden, and the picture shows people at work behind their houses. **Later**: the threshing
floor (C3.7b: sheaves threshed at the barn or the granary through the year, ~0.2 h a household-day - the first sheaves threshed at once,
D2-132) and the muck (C3.7c: the yards' dung carted onto the holdings before the ploughing - the herds' return to the soil, D2-139, by labour
and carts instead of free).

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
- ~~**C3.5 household formation**~~ (done, D2-129, `07` §7e): a couple marries when what it brings keeps it to the next harvest.
- **C3.6 household land, or working the land harder - measured in session 16; C3.6a working the land harder built (§2d, D2-131)** (`place --land`, `_m2` saves, ten worlds): land
  binds almost everywhere once worlds fill - at year 100 43 % of people live in villages whose arable within reach is under their need (0.5 ha a
  head), at year 200 84 %, at year 300 91 % (149 of 171 villages; arable/need median 0.79; the median last harvest 0.86 of a year's bread, 0.76 in
  villages under 0.8 of their need). Yet labour is idle: from the year-200 saves of seeds 5 and 4, a working person-day has 4.4-4.7 hours after the
  household's chores, crops take 0.8-0.9 of them and 4.5-5 hours stay free. Two answers, each a phase: **working the land harder** (Boserup 1965:
  under pressure more labour a hectare - tillage, weeding, manuring with the herds' dung - for yields at falling returns; it raises the land's
  ceiling, so it changes the worlds' scale, D2-085) and **household land** (holdings inherited, landless households and labour for others, lords'
  land and rents - C10; marriage on a holding, the NW European pattern, D2-129 "Open"; it changes who eats, not how much).
  **Chosen by measurement (D2-131)**: in bound villages labour never limited a stage and the hungry gathered at 0.02-0.08 FU an hour - the
  intensive margin was missing, so C3.6a came first. Next of the phase: **C3.6b the herds' dung** (manure carried from the pens to the fields:
  bound villages keep 0.3-0.6 beasts a field hectare, ~1.5-3 kg of fodder a day - a tenth of a medieval manuring; the manure gap), and
  **household land** (holdings, the landless, marriage on a holding - the preventive check that would turn the land's gain into welfare).
- **C3.7 apprenticeship**: skill passed by working beside a master at a workshop (not only by being near: D2-062).
- **C3.8 workshops of other crafts** (carpenter, tanner, cooper) as their goods are needed.
Then C9.4 (cargo by sea) and C10.2 can be weighed again against the traffic.
