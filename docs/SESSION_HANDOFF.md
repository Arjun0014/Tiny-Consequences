# Session handoff — session 17 (2 Oct 2026): R5 founding, roads over the land, kin help, the economy woken

`docs/PROGRESS.md` (session 17 entry) and **D-123 … D-128** in `docs/DECISIONS.md` are the source of
truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop); visible action = simulation; nothing appears that nobody built; fix mechanisms, not
appearances; never trade agency for the season budget; no forced trade or migration (test by
creating a real premise); measure the release browser by the lower envelope of several runs with
rendering off; hand off near ~900K context.

## What happened
- **R5 founding (D-123, `sim/agents/founding.lua`)**, built from scratch and followed end to end:
  - *Knowledge of places* (`s.lore[cell] = { t, how }`): the country within 4 rings (hunting,
    herding), the land along every road walked (`p.seen`, told at home or where they come to live),
    the shore along every sea way sailed, what visitors and hosts tell each other (`econ.on_learn`),
    a bride's village's knowledge. Never the planet itself.
  - *Who and why* (`founding.daily` after `migrate.daily`): hardship that lasted with no known village
    drawing them (`hh.no_refuge`), a couple waiting `found_lot_days` with no lot (`s.no_lot`), no room
    for the fields wanted (`s.no_field_room`); against what holds them, the season's risk and the
    food they can take (starving people do not found). Others in the same plight (kin first) join.
  - *The road*: making ready the morning after; provisions shared; packs from the larder and the
    granary share; beasts led; the dog goes with them; the land route (D-124) at the party's pace.
  - *The place*: made when the first of them walks onto it (empty map: land, rivers, untouched woods);
    laid out by them; camps (home and larder; herds kept by them; people round the fire); water from
    the river or a well; huts on lots cleared by felling and grubbing (`settle.plan_lot`); the camp
    carried in and struck; fields; granary once sown; woodstore; pens. Every system tolerates it.
  - *View*: added at runtime (`agents_view.add_one`), camps/huts/fires drawn, travellers and their
    beasts on the land route, settlement picking and inspector (founding story, why here, what stands
    and is being made, what it lacks, food, beasts, knowledge, news).
  - Watched: Zurug crowded (`premise.crowd`: its ground built over by families of the founding
    templates), a couple that waited 13 days founds a place 31 h away known from Hottoodingle Hop's
    people; camp within hours, huts in ~4 days, woodstore, fields, granary when sown; a second couple
    follows. Earlier drafts starved founders (fixed: lodging couples had no larder; gather counted as
    work; a layout on the wrong bank of a river; nothing to eat without a fire or a home).
- **Roads follow the land (D-124)**: `trade.land_route` over the planet's cells; all roads use it.
- **Kin help (D-125)**: asking kin elsewhere for food; kin offering a place to live.
- **The quiet economy (D-126)**: diagnosed with `tools/v3_market.lua`; reachable stock targets, the
  road's cost as the work given up, work for exchange, visit gifts both ways, news visits. 0 -> 21
  exchanges in the untouched year (first on day 31, with the southern wool and leather).
- **D-127**: a hunter cut off by water starved; now people wade back after an hour of failed plans.
- **Performance (D-128)**: exact cuts only (path cache kept while costs are unchanged; ring-only
  free_spot), proven by identical 4-day hashes.

## State
- **main** at the final commit (see PROGRESS). Shipped world unchanged (`assets/worlds/v3.tcsave`).
- Quick sim **143/143** (v2 109 + v3 34), web **11/11** (W10), lint clean, `npm run build` OK;
  `node scripts/v3_hash_web.mjs --release`: browser = LuaJIT at 0, 24, 48, 72 h.
- LuaJIT `luajit -joff tools/v3_profile.lua --days 4`: **3.93 M instructions a game hour** (S16 4.06):
  nav (A*) 28.6%, stock 12.3%, map 12.0%, think 11.8%, wild 11.3%, event queue 8.6%.
- Release browser (`node scripts/prof_web.mjs --release --hours 288 --runs 3`, rendering off): runs
  40.7 / 38.6 / 38.3 ms/h, envelope **37.9 ms/h = a 12-day season 10.9 s** (target 10 s; kept).
- Economy, untouched world, 48 days (`luajit tools/v3_econ.lua --days 48 --every 12`): 21 exchanges,
  4 news visits, trips Dreik Knoll 17, Hottoodingle Hop 17, Krikeivorn 10, Zurug 6, Tekheim/Little
  Leelee 0; no founding, no migration; deaths only old age and three small children's illnesses.

## Next (in order)
1. **Moving by sea** (not done; design): in `migrate.daily`, a household owning a sound boat
   (`econ.own_boat`) with at most ~6 members also weighs the sea partners (`trade.sea_partners`,
   hours = route len / sail_speed); `hh.migrate.sea = boat id`. `migrate_legs`: pack, all to the dock,
   gather; the skipper (its fisher, else the eldest grown-up) boards and sails (`voyage` with
   `migrate = true`), the others ride (`p.riding = skipper`; generalise `WORK.voyage` to move every
   rider of the boat, not only the mate, and `WORK.land` to set every rider ashore); at the other
   dock `migrate.arrive` for each; the boat moves from `home.boats` to `o.boats` (renumbered; owner
   kept). Small stock only (a few sheep/goats as cargo), the rest left with kin. Then founding by sea
   (`founding.daily` sea sites: the shore known from voyages, `note_sea`), sea droves, and larger
   cargo boats only when trade volume by sea (now real: Krikeivorn-Dreik Knoll) asks for them.
2. **The economy, deeper**: goods only some places have (salt from coastal pans for curing fish, meat,
   cheese and hides; stone or metal for tools), fairs after the harvest (a reason for neighbours to
   meet, court and barter), credit for what cannot be carried. Keep measuring with `v3_market.lua`
   and `v3_econ.lua`; never force.
3. **Founding, polish**: hunters refreshing knowledge at the map's edge; a hut rebuilt as a house
   later; planet markers for small places; a forest site's camp is hidden under the canopy until its
   people have cut about it (honest, but the camera could help); the founding generator's waiting
   couples have no parents (no kin to ask).
4. **Performance** (exact only): wild.threat scans a village's members per wild decision (an index of
   people outdoors by tile block, kept on step ends, with member order for ties); incremental cost
   rebuilds for building changes; the herd scan's centre is only needed when nobody is in sight (the
   nearest/count part could use a block index with index-order ties).
5. Birds beyond gulls, lakes, local-map valleys, passes - below the above.

## Findings and pitfalls (session 17)
- **Runtime-created people must enter the event queue**: `people.new` after the queue exists does not
  schedule them; `premise.crowd` sets `W.v3rt = nil` (rebuilt like after a load). Births do their own.
- Heredocs with apostrophes still break in this harness's bash: write patch scripts to the
  scratchpad with the Write tool and run them with python.
- `render_off` had been erroring every frame (the clear used `render.BUFFER_COLOR_BIT`, gone in this
  Defold); fixed with `graphics.BUFFER_TYPE_*`. Earlier render_off measurements drew nothing anyway.
- The path cache cannot live across days (~560 tile costs change a day per settlement).
- A village's knowledge of places is lazily made (`founding.lore`); a new place copies its founders'.
- `trade.forget(W)` must be called when settlements appear (partner caches).
- Lua's 60-upvalue limit hit `think.decide`: call new helpers through `M.` (e.g. `M.found_legs`).
- The browser's world starts at the first dawn, not hour 0: probes that apply premises after load
  must not assume hour 0 (and must rebuild the event queue).

## Known oddities
- Founding needs real pressure: the shipped world founds nothing in a year (healthy, uncrowded).
- The first month of the shipped world is still quiet in trade (seasonal goods not in yet).
- Tekheim and Little Leelee never trade (one 31 h road, little to exchange).
- Night scenes are very dark.
