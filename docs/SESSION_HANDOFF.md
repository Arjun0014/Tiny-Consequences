# Session handoff — session 12 end (2 Oct 2026): animals alive, rivers in use, the first trade

`docs/PROGRESS.md` (session 12 entry) and **D-091 … D-097** in `docs/DECISIONS.md` are the source
of truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop, ~2.3 GB free); visible action = simulation; fix mechanisms, not appearances; hand off near
~900K context.

## State
- **main** holds everything. A normal launch opens the v3 world (`assets/worlds/v3.tcsave`: seed
  22, 6 settlements, 145 people, ~330 livestock, ~140 wild animals on the maps; Tekheim in the
  north alone on its landmass; Zurug, Moomoo, Hottoodingle Hop on one southern landmass, Hunuvale
  and Upper Linamist on another). `?world=v2` legacy; `?seed=N` generates a v3 world.
- Regenerate after any change to founding/worldgen:
  `luajit tools/make_world.lua --v3 --seed 22 --settlements 6 --out assets/worlds/v3.tcsave`
  (state hash 3676537373 at the last commit).

## What became agentic this session
- **Herds** (D-092, `sim/agents/stock.lua`): sheep and goats in the fold, cattle in the byre yard,
  pigs in the sty; every animal its own micro-agent (graze/root/browse, rest, walk, the young at
  their mothers, its place in a daily-reshuffled sunflower spread round the herder, the pen at
  night). Children (then apprentices, elders, adults) drive each herd out and home; dams are milked
  where they stand (held while milked), milk goes home, cheese from the surplus; shearing; hay mown
  and racked; dung carried from the pens to fields waiting for the plough (fertility); scraps and
  whey to the pigs; hungry pigs nobody takes out root about the village's edge themselves; sickness
  spreads in pens; culls by the hay in autumn, pigs at the start of winter.
- **Animated farm animals**: `tools/blender/build_animals.py` (+ `preview_animals.py`,
  `tools/gen_animals.py`): Quaternius rigs with `graze` and `lie` clips derived here, colours in UV,
  instanced, 3 LODs, 10 variants (shorn sheep, goats made from the sheep).
- **Wild animals** (D-093, `sim/agents/wild.lua`): the W4 herds' and packs' members on each map
  as individuals (count = the group's time on the map's cells), walking in and out at the border;
  bands flee people and wolves; wolves hunt by night (one ground a pack a night), kill a real animal
  (wild kills banked into the macro predation; beasts only when the pack is hungry and wild prey is
  scarce), eat, leave before dawn; after a loss the village keeps a night watch at the pens.
- **Hunting** (D-094): a household short of meat sends a hunter after one animal: tracked, stalked,
  a spear thrown (by skill); the quarry may bolt; carried home whole (meat and a hide).
- **Rivers** (D-095): crossings measured as walked; bridges laid out and built where the need is
  (spaced, straight across); water from the river when far nearer than the well; angling.
- **Fishing** (D-096): the net wants two (a mate rides in the skipper's boat), squalls, drying
  racks by the shore, boats drawn keel to hull.
- **Trade** (D-097, `sim/agents/trade.lua`): a fishing household's porter carries smoked fish and
  provisions to a reachable village with grain to spare and little fish: the map's gate, a road
  leg drawn on the planet, the buyer's granary, a guest meal, grain home. `p.at` = the map a person
  is on (steps run there; drawn there; `s.visitors`).

## Performance (release HTML5)
- **A 12-day season ≈ 9.4 s** (32.7 ms of sim per game hour) for 150 people, ~430 livestock, ~140
  wild animals: `node scripts/prof_web.mjs --release --hours 288 --runs 2` (the lower envelope of
  runs; this laptop's load swings single runs 2-3x — never trust one run). Before the animals:
  6.0 s (20.9 ms/h).
- By function (`--time sim.agents.think:decide,sim.agents.nav:path,sim.agents.stock:step,...`):
  decide ~8 ms/h, A* paths ~6, act.start ~6, stock.step ~4, wild.step ~4, wild.daily ~1.
- HTML5 Lua prices (`node scripts/microbench_web.mjs --release`): VM step 13 ns, table read ~21,
  C call ~40, Lua call ~70, string.format ~925, rng.fbase ~855, rng.hash ~4,900.
- Identity of refactors: `luajit tools/v3_hash.lua --days 8` before and after.

## How to look at it
- Year watch: `luajit tools/v3_year.lua --load assets/worlds/v3.tcsave --days 36 --sett K
  [--ids 12,21 --follow-days 3,15] --out out/year` → summary.txt (food ledger every day; herds line;
  budgets by stage and season), follow.txt.
- Shots: `node scripts/shots_look.mjs --sett K --days D --hour H --zoom Z --what centre|dock|ford|
  flock|herd|wild|wolves|bridge [--herd cattle|swine] [--pid N]` (it now rebakes the settlement's
  parts before the shot — earlier shots could show stale fields and buildings).
- Profiling and benches: above. Maps: `luajit tools/v3_map.lua`.

## Verified at the last commit
- Quick sim suite **126/126** (v2 109 + v3 17), web **10/10**, lint clean, debug build 7.1 MB.
- Two game years headless (96 days): people 145-153, livestock 330-480, wild 141-144, food
  stores rising, 7 trade round trips, 2 bridges; the food ledger closes to 0.0 FU every day in the
  watched villages (including both ends of a trade).

## Next (in order)
1. **R4 generalised**: more goods (wool, cloth, logs, livestock) by the same rule (surplus at A,
   shortage and value at B, a road); the return good chosen by the seller's own shortage; **sea
   trade** by boat between coastal villages (the two landmasses: boats carrying cargo, crews);
   prices remembered; standing relations (v2 D-055/D-062 re-expressed).
2. **Animals**: animated deer and wolves (Horse/Pug rigs via build_animals.py: the Pug's armature
   is scaled 0.395 — apply it first), dogs (herding, guarding), oxen at the plough, hides used
   (leather), wolves driven off by people at the fold, small game.
3. **Rivers and water**: bridges swept away by big floods, river water making people ill, boats on
   rivers.
4. Watch more: children under nine still play most of the day (historically plausible but worth a
   look), grain stores rising year on year (the planner may sow more than it needs).

## Findings and pitfalls (session 12)
- **Measure the browser as a lower envelope of several runs**; a hook sampler with short stretches
  misattributes (use wrapped timers, `--time`).
- `#s.buildings` is not the next id: building ids are world-wide (`V.next_building`).
- `cond and f() or g()` keeps one return value — never for positions.
- A long action's hunger is charged at once at its end: long actions need their own body rule
  (road legs: provisions, resting rate).
- After a step that changes the map a person is on, re-resolve the settlement (`p.at`).
- Interrupting a person re-plans them; to end one step and go on with the plan use
  `init.skip_step` (injected into act.lua; bob forbids circular requires, even inside functions:
  inject functions like `stock.person_pos`, `wild.push`, `act.people`).
- `s.wild` is the woods' forage (a number); the wild animals are `s.wildlife`.
- Runtime caches that change behaviour must be rebuilt from saved state identically after a load
  (the wild animals' index by number, the flight check reads people's stored positions, not an
  hourly cache).
- Heredocs with apostrophes break in Bash here: write patch scripts with the Write tool; never
  `cat > file` without input (it blocks).

## Known oddities
- Children under nine play ~55-70% of their waking day; nine-year-olds herd only when an older
  herder has not already taken the herd out.
- Wild animals cap at 24 a map; deer and wolves are still the rigid procedural movers.
- Trade is only smoked fish for grain, and only seven round trips in two years in the shipped world.
- Two adjacent bridge sites can still appear if two crossings far apart both pass the need on one
  day (spacing is checked against existing bridges only).
