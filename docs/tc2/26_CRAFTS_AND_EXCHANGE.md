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

## 3. Later slices
- ~~**C3.1b more looms for towns**~~ (done, D2-120): another shed where the households drain the store most of a year with fibre to weave and
  the people in wares would outwear the looms; eight more makers a shed, each at the shed nearest home; towns of 500+ carry 2-4.
- ~~**C3.2 the potter's kiln**~~ (built, §2, D2-121).
- **C3.3 exchange between households** - **surveyed in session 13, deferred as a phase of its own**: a household's holdings (its own grain,
  wares, beasts) and payment in kind for what it takes - the makers paid; real household wealth replacing D2-114's `Means`. Measured with a
  tools ledger (`run --ledger 250`: each household's share of the harvest by its crop work, its wild food and herd produce, against what its
  larder drew from the store; seed 4, years 250-300, 5,861 households; the run is the same run): **the village's store moves 31 % of all the
  food households draw** from the 43 % who bring in more than they eat to the 57 % who bring in less (net a person-year by decile -29/-17/-5/
  +12/+46 FU against ~40-48 eaten); the givers' surplus is ~69 FU a year each and most of it feeds the takers; workshop makers' households are
  takers (the village feeds its craftsmen). So a household's own grain without the village's sharing would starve most households: C3.3 needs
  a **moral economy** - sharing by need kept in shortage, and gifts or debts recorded for what the takers drew (young families in debt, older
  households with grown children wealthy: the household's cycle) - and, under it, household fields (who works which ground), inheritance of
  land and claims, labour for others. That is a phase (land, property, debt, labour), not a slice; it is weighed again with C10 (lords' rents).
- **C3.4 the mill** (water or animal): grinding for the village, the miller's toll; a quern's hours freed.
- **C3.5 apprenticeship**: skill passed by working beside a master at a workshop (not only by being near: D2-062).
- **C3.6 workshops of other crafts** (carpenter, tanner, cooper) as their goods are needed.
Then C9.4 (cargo by sea) and C10.2 can be weighed again against the traffic.
