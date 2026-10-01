# V3-30 — Roadmap v3: an automated miniature civilisation (D-078)

Supersedes `docs/world/17_ROADMAP.md` for v3 worlds. **v3 is the default game since session 10
(D-079)**, before parity, because it is the chosen direction; the v2 world stays playable as a
legacy/debug fallback (`?world=v2`) and its code is retired only when v3 reaches parity.

Method per phase: one complete vertical loop → run headless (`tools/v3_run.lua`, follow
people) → watch it (`npm run shots:v3`, the game with `?world=v3`) → profile (`tools/v3_scale.lua`)
→ fix → expand. Tests in `tests/sim/unit/test_v3_agents.lua` grow with each phase.

## R0 — Direction and slice (session 9, done)
Agents with continuous motion and A* on a settlement map, utility choice over a job board,
farming row by row, woodcutting tree by tree, carrying, building a house for a waiting couple,
clearing new fields, foraging, chores, rain reactions, life stages, births, marriage; the v3
calendar and speed ladder; skinned villagers from the Blender pipeline; inspector and follow.

## R1 — The slice made solid (session 10)
- Watch a full year headless and in the game: harvest, threshing, autumn wood, winter fuel and
  hunger, the second spring; fix what reads wrong when following people (logs per trip, seed
  round-trips, crowding at the same tree, evening gatherings).
- Infants drawn in arms; children follow a parent sometimes; apprentices work beside their mentor.
- Night: lit windows from people indoors and awake; chimney smoke from hearths that burn logs.
- The far LOD at ~150-250 triangles; animation blending per action; facing toward work targets.
- Catch-up mode in the UI (⏭: "to dawn", "a season") with the people hidden; HUD speed buttons
  relabelled for the v3 ladder; the inspector's memories and relationships. (D-079: the watch
  ladder stops at ▶▶▶; skips until dawn / evening / a day / the next season, keys 4-7.)
- Performance: why-strings built lazily (on inspection), fewer RNG hashes per decision; A* on a
  coarse grid for long trips; the planet's daily step amortised. Target: 200 agents at ▶▶▶ < 2 ms
  per frame in the HTML5 release, a season of catch-up in ≤ 10 s.

R1 status (session 10, D-079-D-082): done — a whole year watched and fixed by mechanism (rotation,
labour, cooking, hearths, tools, wild food, repairs, care across the generations, own fields,
spare hands clear land), lit windows and smoke, far LOD, skips. Open: winter work (textiles,
livestock), catch-up speed with several settlements (154 ms of sim per game hour for 103 people
in the debug build), the daily planet step amortised.

## R2 — Settlements across the world
- 4-8 settlements of 20-30 founders on different landmasses (worldgen `settlements`), each with
  its own culture's houses; sites near water preferred (coasts, rivers with fords).
- Coasts and rivers on the local map from the planet's render coast (a shared pure coast function)
  and the river network; bridges and fords as lane tiles people build.
- Fishing: piers, real boats (entities) rowed out to the grounds, catch landed and carried.
  (Session 10: 4 settlements of 18-36 founders, north only (D-083); docks, boats and the fishing
  loop in the sim (D-084). Open: the shared coast function so the local map's water matches the
  rendered coast; southern founding states; rivers and fords.)
- Storage and spoilage, the store a village wants (D-074's memory of hunger re-expressed).

## R3 — Animals as individuals
- Herds/packs (W4 fauna) stay the macro ecology; each visible animal is a micro-agent (graze,
  wander, separation/cohesion, rest, follow its mother, flee, chase) steered by the group's
  objective. Kills remove a specific animal; hunters stalk a specific animal.
- Livestock (sheep, goats, cattle, pigs, horses) from the Quaternius pack, herded by people.

## R4 — Exchange and travel
- Paths between settlements, porters and traders walking with loads; trade boats; relations from
  v2 (D-055, D-062, D-066) re-expressed between settlements of agents; gifts, relief, feasts.

## R5 — Migration and founding
- Households leave by land or boat when push factors (hunger, no home, crowding) beat staying;
  colonists found new settlements (the founding generator as a process: stakes, huts first).

## R6 — Conflict
- Raids (walking or boat-borne raiders), defenders, palisades built by people, captives and
  grievances, truces (v2 D-060, D-066 re-expressed with agents).

## R7 — Player and chains
- God powers on the v3 world (lightning on a tree or a person, rain, drought, cull, oil spill on
  the ocean (fishers respond), meteor); the chain of causes from any event back to Nature or You;
  the chronicle from agents' memories.

## Never cut
Determinism, causality (every major change traceable), the planet layer, no scripted
consequences, persistent people with continuous positions.
