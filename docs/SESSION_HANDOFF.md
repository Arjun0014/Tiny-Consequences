# Session handoff — session 18 (2 Oct 2026): moving and founding by sea, sea droves, salt, travellers who share

`docs/PROGRESS.md` (session 18 entry) and **D-129 … D-136** in `docs/DECISIONS.md` are the source of
truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop); visible action = simulation; nothing appears that nobody built or carried; fix mechanisms,
not appearances; never trade agency for the season budget; no forced trade, migration or founding
(test by creating a real premise); measure the release browser by the lower envelope of several runs
with rendering off; hand off near ~900K context.

## What happened
- **Moving by sea (D-129)**: a household with its own sound boat, a skipper and room for everyone
  (`boat_places` 7: grown-up 1, child 0.5, babe 0) weighs the villages over the sea it *knows* (news,
  kin, kin's offer) against the road; the larder is shared among the walkers (`think.move_share`, also
  by land); the skipper loads goods (dearest per weight first); a few small beasts go (ewes and does
  first; never cattle; the rest left with kin, `a.why_owner`); everyone gathers at the dock; a storm
  along the way or someone missing puts the sailing off to the next morning (home again, beasts back
  to the herd), given up after 4; WORK.board + WORK.embark (riders generalised: `boat.riders`,
  `p.riding`, the `passage` step); the voyage moves everyone; landing at the dock: `migrate.land`
  (arrive each, `move_boat`: the same boat moored at the new village, its old entry `moved`, the hold's
  food moved between the books), beasts to the herd, the hold carried up. Followed: Dreik Knoll ->
  Krikeivorn (Gakulf's household, 3 ewes, the dog), Tekheim -> Zurug 20 h (babe and two children).
- **Boats stay in for storms, not rain (D-130)**: "wet" was 39-47 days of 48 at sea, so long
  crossings (trade and moves) were possible ~4 days a year; now `trade.sea_rough` (a storm or gale at
  any of three cells of the way) keeps boats in 6-11 days; rain slows and triples the risk.
- **Founding by sea (D-131)**: `founding.best_sea_site` (coastal cells the village knows, reachable
  by `trade.sea_route_to`), compared with the land site on what each way means (the hold's seed and
  bread vs beasts left behind); `decide_sea` (one household; room kept for 3 loads of seed); the
  place made as they sail into its waters (`sea_place`), `founding.beach` (a dry shore tile the sea
  gate's water reaches), landing (`land_sea`), then camp/hut/fields/stores as by land; a dock site
  once all have roofs (a dock is the village's only when it stands). In the untouched world land
  usually wins (good land near): realistic. Followed: Tekheim -> "Rogthorp" 30.7 h (hut on day 13).
- **Small sea droves (D-132)**: sheep and goats by sea, at most 4, haltered, led to the dock, lifted
  in at boarding, led off at the other dock, bargained for at the pen; the unsold come home.
- **Salt (D-133)**: salt pans built by work on the coast (`saltpan`), seawater boiled over a fire
  (logs; the sun helps on warm dry days); salt cures meat and fish without smoke and keeps cheese
  (unsalted cheese keeps 0.6, salted 0.85); a house good traded like cloth. Untouched 48 days:
  **34 exchanges (19 before), first on day 24 (29-31 before)**; salt goes inland by road for cloth and
  hides; Tekheim and Little Leelee trade for the first time; Tekheim cures 515 FU with 31 measures.
- **Following travellers fixed (D-134)**: families share provisions on the way; hungry travellers with
  nothing left gather along the road (`road_forage`); babes fed in boats; carers of babes do not crew
  (a nursing mother had crewed a 30 h trading voyage and the baby starved - pre-existing); beasts never
  left owned by people gone (`migrate.leave_beast`); children no longer play with friends on another
  map, visits no longer use another village's building ids (pre-existing).
- **Founding polish (D-135)**: starving people do not found (`found_short_max` gate); huts replaced by
  proper houses after `found_hut_days` 24; hunters refresh the home country (`founding.roamed`);
  logs from the household's woodpile to a young place's sites; haulers check a site's real shortfall.
- **Wolves (D-136)**: each leaves for the den its own way; audit bound 0.75 -> 0.8 (a hunt in the window).

## State
- **main** at the final commit (see PROGRESS). Shipped world unchanged (`assets/worlds/v3.tcsave`).
- Quick sim **145/145** (v2 109 + v3 36: new `test_v3_sea` x2); web **11/11** (W02, a v2 wall-clock
  rate test, missed by 0.3% once under load and passed on rerun); lint clean; `npm run build` OK;
  `node scripts/v3_hash_web.mjs --release`: browser = LuaJIT at 0, 24, 48, 72 h.
- LuaJIT `luajit -joff tools/v3_profile.lua --days 4`: **3.93 M instructions a game hour** (148 people;
  S17 3.93 M): nav 28.5%, stock 12.4%, map 12.1%, think 11.7%, wild 11.3%, event queue 8.7%.
- Release browser (`node scripts/prof_web.mjs --release --hours 288 --runs 3`, rendering off): runs
  40.7 / 40.2 / 43.7 ms/h, envelope **39.2 ms/h = a 12-day season 11.3 s** (S17 37.9 ms/h, 10.9 s) with
  the LuaJIT instruction count unchanged - most likely this laptop's load; no exact cut was made this
  session (D-137). Target 10 s still open; never by cutting agency.

- Watched, untouched world, 96 days (`luajit tools/v3_econ.lua --days 96 --every 48`): **134 exchanges**
  (34 in the first 48 days); salt inland (Hottoodingle Hop got 37 measures, Little Leelee 20; Zurug sold
  34); brides moved between villages; boats rebuilt where they rotted; deaths only 3 of old age and 3
  infants' illness in the first month, none from hunger; no move or founding (no push: healthy).

## Next (in order)
1. **Watch a long run** (96+ days) of the untouched world and of premises: salt prices settling, sea
   trade volume (now possible on long ways), whether a household ever moves or founds by sea without a
   premise; inspect the founded places' docks and huts-to-houses in the game (`scripts/shots_found.mjs`).
2. **The economy, deeper**: stone and metal tied to geology (ore -> charcoal -> smelting -> smith ->
   tools; tools already wear: `hh.tools`), salt for livestock, fairs after the harvest (people
   physically gathering: barter, courting, news), credit only if a trade cannot balance in one load.
3. **A ferry**: a kin skipper sailing a couple without a boat to found or move, and sailing back
   (the passage pattern exists: riders, embark, land).
4. **Founding**: planet markers for small places; the founding generator's waiting couples have no
   parents; a forest site's camp under its canopy.
5. **Performance, exact only**: an index of people outdoors by tile block for `wild.threat` (it scans a
   village's members per grazer decision; ties in member order), incremental cost rebuilds for
   building changes, the herd scan's centre only when needed.

## Findings and pitfalls (session 18)
- `s.landing` was already "a catch landed" (gulls read it): the founders' beach is `s.beach`.
- A dock planned as a site must not become `s.dock` until it stands (`settle.add_building`).
- `settle.lua` cannot require `trade.lua` (bob: circular via wild/stock): injected as `settle.trade`.
- Friends (`p.rel_id`) span villages now (moves, brides, founders): any spatial use must check the map.
- The sea-founding test gives the decision (the site from the household's own knowledge): in this world
  land wins honestly; a premise that makes the sea win naturally is still to be found.
- Heredocs with apostrophes break in this harness's bash: patch scripts go through files + python.
