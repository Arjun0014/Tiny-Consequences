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

## 2. Later slices
- ~~**C3.1b more looms for towns**~~ (done, D2-120): another shed where the households drain the store most of a year with fibre to weave and
  the people in wares would outwear the looms; eight more makers a shed, each at the shed nearest home; towns of 500+ carry 2-4.
- **C3.2 the potter's kiln**: pots and storage jars (Pottery: the store keeps better - D2-063's rule read from pots made, not a village's best
  potter), charcoal at a kiln; clay dug (a clay pit at the river's clay).
- **C3.3 exchange between households**: a household's holdings (its own grain, wares, beasts) and payment in kind for what it takes - the
  makers paid; real household wealth replacing D2-114's `Means`.
- **C3.4 the mill** (water or animal): grinding for the village, the miller's toll; a quern's hours freed.
- **C3.5 apprenticeship**: skill passed by working beside a master at a workshop (not only by being near: D2-062).
- **C3.6 workshops of other crafts** (carpenter, tanner, cooper) as their goods are needed.
Then C9.4 (cargo by sea) and C10.2 can be weighed again against the traffic.
