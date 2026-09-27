# W13 — Transport and trade

Supersedes 02 §5.11. Every moving vessel or group is a real entity with an origin, a destination,
a reason, a cargo or catch, a route, timing, an arrival and consequences. If it is destroyed, what
it carried never arrives. Research basis: `00_RESEARCH.md` §6, §9.

## 1. Movement basics

- **Routes** are shortest paths on the cell graph: sea routes over ocean cells (sea ice ≥ 0.5 is
  impassable to boats), land routes over land cells. Computed at departure (BFS), cached per
  derived rebuild.
- **Speeds** (game time per cell spacing): walking with loads 0.5 h (daylight only; parties camp at
  night), fishing boat 0.3 h, merchant ship 0.25 h. The planet is ≈ 60 cells around: a merchant
  ship circles it in ≈ 15 hours; a walk across a continent takes a few days.
- **Positions are analytic**: an entity stores `path`, `depart_h`, `arrive_h`; its position at any
  time is interpolated along the path. The sim only acts on departures and arrivals (hour steps).
  The view draws exactly these positions.

## 2. Fishing boats (village-owned)

```
id, key, village, home (mooring ocean cell by the pier), state ("moored"|"out"|"lost"),
crew (person ids, 2–3), trip { depart_h, ground, at_ground_h, leave_ground_h, return_h, catch },
condition (0–1), built (hour)
```
- **Built** by the village (25 WU, 160 h, needs a pier). Condition falls 8%/yr; repaired with the
  village's repair jobs; a boat at condition 0 is wrecked.
- **Crews** come from the weekly allocator (fishers keep their boat).
- **Trip plan** at the village's day step: no trip on storm days, if sea ice blocks the mooring, or
  without a crew. Depart local sunrise + 0.5 h. Ground = the ocean cell within 5 cells that maximises
  `fish × fishing_hours × (1 − share already targeted today)`, where fishing hours = (sunset −
  depart) − 2 × sailing time − 0.5 h. Predicted catch = q × fish × crew fitness × hours, capped by
  the hold (150 FU); q = 6.7 FU per crew-hour at density 1 (a healthy ground gives ≈ 20 FU per
  fisher-day, enough for ~20 people-days).
- **Return** (hour event): catch removed from `fish[ground]` (Δ = catch / FISH_FU_CELL, 18,000 FU per
  cell at density 1), added to the village's `fresh`; blame: the ground's fish tag drives the catch
  term. Squalls: 0.2% of trips are caught by weather → `boat_lost` (nature), crew may drown.
- **Destroyed** by a power while out: catch lost, crew drown (60%) or swim home; `boat_lost` with the
  power as parent; the village must build a new boat (wood, labour) — a chain.
- **Overfishing** is emergent: near grounds thin, boats go farther, fishing hours fall, catches fall.

## 3. Merchant ships (replace tankers)

```
id, key, name, kind = "merchant", basin (connected sea), route (port village ids, circuit),
leg, path, depart_h, arrive_h, state ("sailing"|"docked"), dock_until,
cargo { grain, fresh, wood, oil }, passengers (party ids), capacity 3,000 units, alive, sunk
```
- **The Tanker Company** runs 1–2 ships per connected sea basin that has ≥ 2 ports (a port is a
  living village with a pier). Hulls carry lamp-oil barrels (`oil` cargo, the spill if sunk) plus
  whatever the ports trade. A lost ship is replaced after 3–5 years if ≥ 2 ports remain ("The Tanker
  Company regrets nothing").
- **Circuit**: at each departure the next port is the nearest unvisited port on the circuit (a
  nearest-neighbour tour over the basin's ports, recomputed so new and abandoned villages join or
  drop out naturally).
- **At port** (docked 6 h): unload each good the port lacks, up to its deficit (`11_ECONOMY.md` §8);
  load each good the port has in surplus, up to 60% of the surplus and the free hold (fresh fish only
  if the next leg is under 2 days; it spoils 4%/day in the hold). Passengers bound here disembark;
  waiting parties bound for a port on the circuit board. Every transfer is logged in the village's
  import/export history.
- **Sinking** (Lightning, Oil Spill, Meteor; later storms): `tanker_sunk` (kept for the tutorial) →
  `shipment_lost` (magnitude = cargo in need-days of its destinations) → oil spill from `oil` cargo;
  passengers drown. Every port that received goods from this ship in the last year gets
  `import_tag = shipment_lost` — its future import shortfall is blamed on the loss until imports
  recover or a replacement ship calls.

## 4. Travel parties (migrants and founders)

```
id, key, members, from (village), to (village id or site cell), mode ("walk"|"ship"),
path, depart_h, arrive_h, food (FU carried), reason (migration event id)
```
- Walkers leave at dawn, walk ~10 daylight hours a day, camp at night; they eat what they carry.
  Arrival: join the destination (homeless household → construction demand) or found the village.
- Sea crossings: the party waits in its village (members marked `away`, not working) until a merchant
  ship calls whose circuit includes the destination, boards, and rides as passengers.
- A party caught by a fire, meteor or Lightning suffers like a village would; a sunk ship drowns its
  passengers.

## 5. Why transport matters (chains it enables)

- *Boat destroyed* → no catch → fresh food falls → rationing in a fishing village → hunger lowers
  fitness → fewer fishing hours → … ; the village also needs wood and labour for a new boat.
- *Merchant ship sunk* → cargo never arrives → ports that relied on its grain run short at the
  next lean season → rationing → seed eaten → fewer fields sown → migration → labour shortage →
  harvest falls → abandonment → fields return to forest.
- *Sea ice / storms* → boats idle → fishing villages depend on stored grain → the merchant circuit
  matters more.
- *Overland migrants* carry food and labour to a destination: its housing and wood come under
  pressure → it cuts more forest → …
