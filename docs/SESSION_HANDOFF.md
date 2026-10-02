# Session handoff — session 14 end (2 Oct 2026): speed and determinism, the world watched, geography, birds

`docs/PROGRESS.md` (session 14 entry) and **D-110 … D-113** in `docs/DECISIONS.md` are the source of
truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop); visible action = simulation; fix mechanisms, not appearances; no forced trade; measure the
release browser by the lower envelope of several runs; hand off near ~900K context.

## State
- **main** holds everything. A normal launch opens the v3 world (`assets/worlds/v3.tcsave`,
  **regenerated this session** with drainage basins: `luajit tools/make_world.lua --v3 --seed 22
  --settlements 6 --out assets/worlds/v3.tcsave`, state hash 1614203617): seed 22, 6 settlements,
  143 people - **Tekheim** (north), **Zurug, Hottoodingle Hop, Krikeivorn** (one southern landmass,
  roads 11-19 h), **Dreik Knoll**, **Little Leelee** (a big river past its fields). Sea routes:
  Dreik Knoll-Krikeivorn 7.3, Zurug-Dreik Knoll 15.3, Tekheim now reaches the south (22+).
  `?world=v2` legacy.

## What changed this session
- **D-110 speed + determinism**: exact cuts (RNG multiplies inlined, bottom-up event heap, floor-free
  `tile_of`/wear, bank cache, children's generators skipped, hay counted in season): a season
  10.5 -> 8.5 s. **The browser had been living a different world from LuaJIT** (diverged within a
  game day): `sim/dmath.lua` (fdlibm sin/cos/exp/log/atan/acos/pow in + - * / only; every `^ 2` a
  multiplication), `sim/fmt.lua` (%.Nf rounded half up first: "%.0f" of 46.5 was 46 vs 47), wild
  arrivals in sorted order (a `pairs()` order). Web test **W10 determinism** checks it.
- **D-111 watched and fixed**: trade on faded news counts its hoped gain at `trade_guess` 0.4 (a
  22-hour walk for 1.3 leather was worth it before); honest trip whys; roads walked by day (night
  camps, lodging after late deals); **the ox team** (a yoked ox placed ahead of its ploughman,
  turning on arcs at the headlands, fetched only when worth the walk, kept in the yoke row after row;
  ploughing is men's work); a **plough** drawn.
- **D-112 geography**: **drainage basins** (`sim/geo.lua`: outlets, priority flood, carved beds
  falling to the sea) - rivers rise in the mountains, merge and widen (`w = 0.02 + 0.36 (Q/0.4)^0.6`,
  trunks ~0.4-0.5 world units); villages found on one bank (`map.home_bank`); valleys and floodplains
  carved into the rendered planet; mountains lifted with sharp crests, grey crags, snow and tree line
  by real height. Tests `test_v3_rivers` (6).
- **D-113 birds**: gull flocks are agents (`sim/agents/birds.lua`: roost, hunger, objective, target,
  why) following boats hauling nets, landings, racks, ploughs, carcasses, the shore; rafting when fed;
  dawn/dusk/downpour; curved flights; members drawn individually; flocks clickable in the inspector.
  The planet's circling puppet birds are off in v3. Test `test_v3_birds`.

## Performance (release HTML5, `node scripts/prof_web.mjs --release --hours 288 --runs 3`)
- Start of session 36.5 ms/h (a 12-day season 10.5 s). After D-110: **29.7 ms/h, 8.5 s**. With the
  regenerated world (D-112): 3-run envelope **34.5 ms/h, 9.9 s** on a loaded laptop (LuaJIT
  instructions for the same hours: 3.35 M/h against 3.48 M/h for the old world: the new world is not
  dearer). **Final, after the birds: 3-run envelope 37.2 ms/h, a season 10.7 s - over target.** The
  steady state is ~25-40 ms/h; game hours 12-30 after load cost 89-149 ms/h in every run. It is
  browser-only: LuaJIT executes fewer instructions in that window than on the old world, and the
  heap is flat (66-68 MB) with or without the GC (`v3_bench{nogc}`). Suspects: C-side work the
  instruction count does not see (table rehashes of the nav path cache refilled after each daily
  cost rebuild, string interning of whys, `table.sort`), the first daily rounds' runtime caches.
- Where the time goes (timers, whole season): think.decide ~10 ms/h, act.start+nav.path ~7, stock.step
  ~5.7, wild.step ~4.8 (22 us a step: its threat scan), settle.daily ~1.9 (7.5 ms a call).

## Tools added
- `scripts/v3_hash_web.mjs` (browser vs LuaJIT state hashes), `scripts/hash_drill.mjs` + bridge
  `hash_parts`/`get_path` (where two states differ), `scripts/mathcheck.mjs` + `tools/mathcheck.lua`,
  `scripts/probe_cmd.mjs` (run bridge commands), `scripts/shots_planet.mjs` (planet at zooms, noon),
  `scripts/shots_look.mjs --at H --world NAME --what birds --inspect-flock`.
- `tools/v3_watch.lua` (moments worth watching with absolute hours), `tools/v3_follow.lua` (a person
  step by step), `tools/v3_rivers.lua` (network report), `tools/v3_birds.lua` (flocks step by step).
- Bridge `v3_bench` returns the Lua heap per hour and takes `nogc`.

## Next (in order)
0. **Performance**: find the browser-only cost of game hours 12-30 after load (see above; time
   `nav.path`'s cache, `settle.daily`, `map.rivers`/`courses`, `wild.daily` in that window with
   `--warm 12 --hours 18 --time ...`) and bring the season back under 10 s.
1. **R4 left**: livestock traded alive (surplus young stock or spare males -> a neighbour short of
   beasts; a drover leads the specific animals - a "led" act like the yoke, removed from the herd at
   the gate, carried as the drover's party on the road, added to the buyer's herd at its gate, the
   ownership changing at the deal); relief asked by sea; relations steering the choice of partner
   (already weighted in best_trade) and beginning to shape marriage and migration; larger cargo boats
   only when the volume carried justifies them.
2. **R5**: migration and founding by real push and pull, physically (pack, walk or sail with family
   and beasts, camp, wood, shelter, food, first field) - no spawned villages.
3. Birds: rooks and crows at fields and carcasses, herons at the rivers, raptors over meadows,
   songbirds in the woods (each with its own reasons); people chasing gulls off the racks.
4. Geography: lakes (beds are carved, never filled), deltas, valleys on the settlements' local maps
   (flat today), a finer planet mesh near mountains if the peaks need it.
5. Keep the season <= 10 s: wild.step's threat scan, stock.step's draws, decide's generators.

## Findings and pitfalls (session 14)
- **Check the browser lives the same world**: `node scripts/v3_hash_web.mjs --release` after any sim
  change that might touch math or formatting. Use `dmath.*` for every transcendental in `sim/`, never
  `^` with a non-integer exponent or `x ^ 2` (write `x * x` or `dmath.sq`), and `sfmt` (sim/fmt.lua)
  for text kept in state.
- The browser starts the v3 world at its first dawn (hour 23.55 for the shipped world): compare with
  LuaJIT from that hour (`tools/v3_hash.lua --from H`).
- Single browser runs are useless on this laptop; even 3-run envelopes swing with load. Compare
  instruction counts (`luajit -joff` with a count hook) to tell code from noise.
- A view function must never write sim state (stock.pos had written `a.yfx` during development).
- Settlement ids in tests: pick by geography, not by number (worlds get regenerated).

## Known oddities
- The dock at a village whose own cell is inland but whose map reaches the sea now gives it sea
  routes (D-112); Tekheim trades south if it finds a reason.
- Night scenes are very dark (wolves at the pens are hard to see).
- Gulls do not yet avoid people at the racks; carcasses attract gulls only (no crows yet).
