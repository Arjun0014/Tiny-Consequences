# TC2-21 — Settlements as economies: structures, workplaces, morphology

Part of the civilisation-depth direction (`20`, D2-089). Principle (08): civilisation is what households and settlements do with land,
labour, knowledge and each other. A settlement stops being "houses in a circle": it is a place whose structures each exist because
they perform a function someone wanted, built from material someone fetched with labour someone gave.

## 1. Structures: one primitive for everything built

Every built thing is a row of `World.Buildings` (`Core/Places/Buildings.cs`, `Structures.cs`): homes since D2-059, and from C1 every
store, workplace, civic, military and maritime structure and work site. A **kind** is a row of a data table (`Structures.Kinds`):

| Field | Meaning |
|---|---|
| function | Dwelling, Water, Store, Smith, Quarry, Train, Market, Gate, Kiln, Mill, Barn, Pen, Landing, Hall, ... |
| zone | where it is laid out (§4): Core, Homes, Edge (on the main road, away from thatch), Outer, Site (at a deposit), Shore |
| footprint | clearance radius (m) on the ground |
| labour, wood, stone | hours and 25 kg loads to raise it (stone only where the village knows stone; otherwise the timber/clay form) |
| life | years until an unused or unkept structure falls in; years its ruin lasts |
| capacity | grain a store keeps, workers a workplace takes, trainees a ground holds |
| knows | the technique a builder or user must carry (masonry for stone walls, metal for a smithy) |

Per structure (state, saved and hashed): kind, settlement, lot (local metres), facing, the household that lives in or keeps it, when
built, when last left or used, **condition** (0..1), the labour and the wood and stone actually put in, its **material** (timber,
stone footings, stone), the person who led its making, the history event that moved it and the event of its completion, what it
holds (stock), and its use (hours worked there). Homes keep their D2-059 rules (decay read from when they were left).

**One state machine**: planned (laid out, a history line with its cause) -> building (hours and materials accumulate; drawn as a
frame, then walls, then a roof) -> standing -> worn (unkept or unused: condition falls by its life) -> damaged (fire, raid, quake,
flood, war: condition cut, cause recorded) -> ruin (fallen in; quarriable, re-roofable later) -> gone into the land.
Upkeep: a structure in use is kept by its users (as a lived-in home is); unused, it decays from when it was last used, with no
per-day pass (read from time, D2-059); a damaged one is mended as a village work while still wanted.

**What a structure does is real**: a granary's capacity limits what keeps well (grain beyond it rots faster); a smithy multiplies a
smith's output and is the only place arms are forged; a quarry is where stone is cut (its face grows with what was taken); a training
ground is where fighting skill is practised; a well is where water is drawn; a gate is where a palisade can be shut. Destroyed, the
function goes with it until rebuilt.

## 2. Village works by need (who wanted it, why)

`Construction.NextWork` (well -> granary -> palisade -> mend) becomes a planner over need tests read from state. Each test names its
cause (a history event) so the History Book and the building page answer "why is this here?":

| Structure | Need test (state) | Cause recorded |
|---|---|---|
| well | no well | the village's founding |
| granary | a harvest to keep and no store; later: the last harvest filled the stores past capacity | the harvest |
| smithy | a competent smith (metal >= 0.5) lives here and the village is short of metal tools, or has arms to make | the smith's technique event |
| quarry | a planned or wanted structure needs stone and stone is known within reach | the plan that needs it |
| stone footings for homes | stone known and cheap (a quarry stands) and masonry known | the quarry |
| training ground | remembered harm from bands (the palisade's reckoning, D2-074), or the village's own band raised in the last years | the raid/loss deeds |
| market | traders arriving per year above a threshold (its place on the roads) | the trades |
| palisade, mend | D2-074 (unchanged) | the losses |

Order is by urgency (water, a roof, storage, then the rest). One village work at a time today (as now); several when a leader can
call labour (C10). A work waits while its materials cannot be had; it is dropped when its need passes before it starts.

## 3. Workplaces and roles

Work done at a structure or site is an activity with a place: ACTIVITY walks the worker there and back (the trip time is real),
COARSE/DEEP gives the same hours from the day's labour by the same priority. Workers are whoever chooses the work by need, skill and
technique; a **role** (smith, quarrier, trainer, herder, fisher) is what a person mostly did this year, read from their hours (the
person page shows it), never assigned. Specialists eat from the shared store like everyone (the village granary, D2-042); exchange
within a village (household larders against the store) comes with markets (C2/C4).

First workplaces (C1): smithy (the smith's furnace and anvil: D2-064's work moved there, faster, and arms), quarry (stone cut and
carried), training ground (practice). Later: kiln (pots, charcoal), mill (water or animal), workshop (carpenter, weaver, tanner), barn,
pens and stables, landing and boat shed, hall.

## 4. Morphology: where things go

The settlement planner places each structure by its zone, on dry ground (`Shore.Dry`), clear of lots, ruins and other footprints, on
gentle slope, facing the square or the road:

- **Core**: the square (village green) at the anchor; the well, granaries and later market and hall round it.
- **Homes**: the lot spiral (D2-086: the innermost free lot first) - compact, so a village keeps its shape.
- **Edge**: workplaces with fire or traffic (smithy, kiln, later inn and workshops) at the homes' outer edge on the main road (the
  road to the neighbour its people walk to most), clear of thatch.
- **Outer**: training ground, pens, barns, at the fields' edge on open ground.
- **Site**: quarries, mines, charcoal clamps at the deposit; a camp there when it is far (C5).
- **Shore**: landings, boat sheds, docks on the coast or the river nearest the square.

Growth history then shows: a hamlet's irregular lots -> a green with its well and store -> workshops on the road -> fields outside ->
a defensive ring -> a denser centre and a market -> houses outside the ring along the roads -> a second, larger ring (C7, C13). Old
walls left inside a grown town stay as structures until quarried.

## 5. Specialisation, read after the fact

A settlement's hours by work kind (annals, per year) and what it trades name it retrospectively: farming village, fishing port, mining
settlement, timber settlement, craft centre, trade centre, fortified frontier town, political capital. No settlement class exists.

## 6. Culture in the built form (C11)

Each household carries a few traits (building tradition: roof form and pitch, wall material preference, plan; a dialect index;
customs) copied from its founders, drifting slowly, spread by marriage and migration and, more slowly, by copying respected neighbours
(Axelrod's dissemination of culture). A structure is raised in its builders' tradition with the materials at hand, so regions look
different because their people and their ground differ - never by faction.

## 7. Inspectors (C12; part of every slice)

- **Building page**: kind and function, age, who raised it (household, the person who led) and why (the cause event), material,
  condition, what it holds, who works there now and how many hours it has seen, its history (built, mended, damaged, rebuilt,
  abandoned), the events it took part in.
- **Settlement page**: population and households, stores by material, structures by kind, production (hours by work), trade, known
  places and deposits, culture, leadership, military (fighters, arms, trained), defences, dependencies (lord, tributaries), problems
  (rations, cold, sickness, shortages), recent history.
- **Person page**: see `07` §9.
All read state and history; none writes.

## 8. Implemented

- **C1.1 structures (D2-090)**: `Core/Places/Structures.cs` - the kinds table (hut, house, well, granary, barn; smithy, quarry,
  training ground and market waiting for their slices), `Building` extended (condition, being raised, labour/wood/stone put in, form,
  leader, cause, completion event, stock, use), works by need (`Plan`: a well from the founding, a granary from the harvest;
  `AfterHarvest`: a store past 90 % of capacity raises a barn where the harvest alone fills a granary, else a granary), the village work
  as its planned structure (`Construction.Structure`, `Settlement.Project`), capacity real (grain beyond the stores rots at 4x),
  placement by zone (first well and granary at their old spots; Core round the green; Edge on the main road past the homes), decay from
  abandonment, the sea taking works, the building page (click a structure), works drawn from the table with kit models. Shots
  `tc2/out/shots/civ1` (seed 42, year 300, Heldholm 602 people): the green with its well and five granaries reads at the street view;
  at the village view the works are lost among the homes (a legibility item: distinct silhouettes, works outside the home ring).
