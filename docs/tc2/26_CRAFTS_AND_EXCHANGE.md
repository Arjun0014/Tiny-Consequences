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

## 1. C3.1 The weaver's workshop and the village's wares (first slice)

- **Wares** (`Settlement.Wares`, person-equivalents of goods, as `Household.Gear`): goods made for others and kept in the village's store.
  A household short of goods takes from the store before its own hands make more (`Goods.Draw`); what it takes is counted (the village's
  wares given out: a demand known from real use).
- **The workshop** (structure kind: a weaver's shed - looms, a warping frame, a dye vat; Zone Edge): raised when a competent weaver lives in
  the village (`KLoom` >= competent) and the village's yearly wear of goods is more than about three full-time makers' output (so a workshop
  and a maker's year pay back: larger places only). Cause: the weaver's technique event and the village's size.
- **Making**: at the workshop a weaver works `Pace` (1.5) times as fast as at home (the big loom, the warping frame) on the store's fibre
  and hides (`Goods.Materials`), into the wares store, up to a target (half a goods a head, plus what was taken lately); lived days walk them
  there (an activity with a place and a why); COARSE/DEEP gives the same hours by the same rule (the smithy's law, D2-093/D2-102).
- **Trade**: wares become a trade good valued by each village's goods short a head (`Trade.Value`), spared beyond a buffer - so a village
  with a weaver's shed has something its neighbours lack, traders carry it, and markets (C1.6) and their market places (C13.3) grow where
  the wares are dealt.
- **Pages**: the building page tells the makers and the wares made; the person page "Known for: weaver" from the hours at the workshop;
  the settlement page the wares in store, made and given out.
- **Done when**: workshops stand in larger villages only; their wares are taken by households that did not make them (hours freed for the
  fields); wares are traded; ten seeds at par in people and food or better; shots of the workshop; tests at both resolutions.

## 2. Later slices
- **C3.2 the potter's kiln**: pots and storage jars (Pottery: the store keeps better - D2-063's rule read from pots made, not a village's best
  potter), charcoal at a kiln; clay dug (a clay pit at the river's clay).
- **C3.3 exchange between households**: a household's holdings (its own grain, wares, beasts) and payment in kind for what it takes - the
  makers paid; real household wealth replacing D2-114's `Means`.
- **C3.4 the mill** (water or animal): grinding for the village, the miller's toll; a quern's hours freed.
- **C3.5 apprenticeship**: skill passed by working beside a master at a workshop (not only by being near: D2-062).
- **C3.6 workshops of other crafts** (carpenter, tanner, cooper) as their goods are needed.
Then C9.4 (cargo by sea) and C10.2 can be weighed again against the traffic.
