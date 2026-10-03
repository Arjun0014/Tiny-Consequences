# Session handoff — session 20 (3 Oct 2026, extended autonomous run, IN PROGRESS): R6 and R6.5 designed; R6a-R6g first pass; R6.5b lightning built; checkpoint at ~850K

**If you are resuming after a context compaction: re-read this section, `docs/v3/50_PRIMITIVES.md`,
`60_R6_CONFLICT.md`, `65_R6_5_LIVING_PLANET.md`, D-148/D-149 and `git log --oneline -10` before any
change.** Rules as in session 19 (no subagents; one heavy process at a time; visible action =
simulation; premises not forced outcomes; browser = LuaJIT; bob forbids circular requires).

## Done this session (in order)
1. Research (`docs/v3/40_RESEARCH_CONFLICT_PLANET.md`) and code audit (`41_PLANET_AUDIT.md`;
   probe: shipped world 96 days - 2 lightning fires planet-wide, none near villages; no snow at any
   village; flat climate; volcanoes not generated in v3; Tekheim and Zurug on convergent boundaries).
2. Design: `50_PRIMITIVES.md`, `60_R6_CONFLICT.md`, `65_R6_5_LIVING_PLANET.md`; roadmap R6/R6.5;
   D-148 (R6.5 phase + primitives), D-149 (R6 design).

## Done: R6a-R6g first pass (commits a58a06c, 8684a92, R6e, 1a94a5c, 1fbf5f3; D-150-D-153)
Code: `sim/agents/deeds.lua` (deeds `V.deeds`; village knowledge `s.dk` with how/who and `own` secrets;
grievance with half-lives and a filter; news via `econ.on_learn`; `deeds.mourn` for deaths learned),
`sim/agents/danger.lua` (cues by sight/sound, sleep, indoors, night; `p.alarm`; interrupt injected),
`sim/agents/conflict.lua` (take: `weigh_at`/`take_weigh`, `took`, `take_cues`, `seen`, `missing`,
`watchers_near`, `let_go`, `chase_target`, `goes_out`, `chased`; claims: `claim_weigh`, `claim_answer`,
`beasts_of`; blows: `strike`, `blows`, `carry_news`/`tell_home`; raids: `companions`, `raid_open`,
`join_weigh`, `join`, `raid_ready`, `party_here`; walls: `defence_daily`, `walled`; amends:
`amends_basket`, `amends_weigh`), `people.bold/came/known_at/hurt`, life (healing, death deeds,
`s.burials`), dogs.bark, wild (wolves' kills at pens are `lost` deeds; walled pens), think (candidates
via `M.*` because decide is at Lua's 60-upvalue limit: steal, raise_raid, join_raid, claim, amends,
wall_fold, tend_hurt, mend, mourn, chase), act (WORK lie_up/steal/chase/claim/amends/tend_hurt/mourn/
wall_fold; drop "wall"; owners changed/restored on arrival; `run` pace), premises (`knows`, `no_dogs`,
`wronged`, `wolf_losses`, `named("conflict")`), config block "(S20 ...)" at the end of `C.V3`, RNG
stream `V3_CONFLICT = 48`, view poses and the fold wall drawn as built.
Tests: `test_v3_conflict` 9 (takes a beast, kin not robbed, a wrong not made good is answered, hurt keeps
to bed and heals, a blow is a deed, men given away alone go together, a known wrong made good unasked, a
fold walled after losses); v3 54/54; browser = LuaJIT untouched 72 h and `--premise conflict` 264 h
(new option of `scripts/v3_hash_web.mjs` / `tools/v3_hash.lua`). LuaJIT 3.71 M instr/h (+1%).
Untouched 192 d (`out/s20/chron192b`): no taking, 426 exchanges, deaths old age 3 + illness 6, burials.
PITFALLS (this session): work/plan kind names and trip fields are one namespace - `tend` was field
weeding, `trip.party` is a drove's beasts (the raid's is `trip.band`); grep before naming. `think.decide`
is at the 60-upvalue limit: add new branches as `M.*` functions. Probes must set the JIT options of
`tests/sim/run.lua` (`jit.opt.start("maxtrace=8000", ...)`) or they run 10-20x slower. The harness's bash
heredocs break on apostrophes: write patch scripts with the Write tool.

## Done: R6.5b lightning on a village (commit 7efe5ba, D-154)
`sim/agents/blaze.lua` (strike at a point -> tallest thing: tree/roof/person; fire `s.burn` lived
continuously by `blaze.live`/`catch_up` - never ahead of the time asked; spread through the tile
grid by heat x dryness x wind; contents and anyone inside burn; burnt house -> ruin, `burnt` deed,
`migrate.lodge` with kin, house planned; natural strikes in downpours `strike_rate`), think
`M.fire_choice` (out with the babe / keep away / carry water at a run), PLAN `flee_fire`/`fight_fire`,
WORK `douse`, wild flee fire, view flames (fx.lua local fires) + smoke, `scripts/shots_fire.mjs`,
bridge `v3_premise` kinds fire/strike/burning, tests `test_v3_lightning` x3. Same code, different
outcomes: drought day saved, drought night lost (lodging with kin), moderate saved; a struck
ploughwoman burnt, bedridden, her husband takes the baby, she still nurses it.
Determinism: `node scripts/v3_hash_web.mjs --release --premise fire|conflict` (premise at an exact
start hour; the old 14-digit start made a strike land ulps apart - a harness artifact, not the sim);
`scripts/hash_drill.mjs --premise NAME --after H` drills a premise run. Browser = LuaJIT on both.

## CHECKPOINT (~850K context, session 20) - state for a resumed session
- main at 763b6b4 (+ this numbers commit); quick suite **166/166** (v3 57); lint clean; build OK;
  browser = LuaJIT (untouched 72 h; `--premise conflict` 264 h; `--premise fire` 48 h).
- LuaJIT **3.79 M instr/h** (3.67 M at the session's start; the new modules themselves are negligible -
  the rise is spread over walking/thinking/herding as days change shape). Release browser
  (`node scripts/prof_web.mjs --release --hours 288 --runs 3`, rendering off): runs 37.4/37.1/36.4,
  envelope **36.1 ms/h = a 12-day season 10.4 s** (35.3 ms/h, 10.2 s at the start).
- Untouched 192 d (R6e+lightning): no taking, no lightning casualties, 426 exchanges, deaths old
  age 3 + illness 6, burials.
- **Next (in order)**: (1) R6.5c wildfire between planet and map (the planet's `F.fire` on a map's
  cell lights trees at the map's edge; a big local fire writes `F.fire`/`F.forest` back); livestock
  led out of burning pens; fields and stacks burning; carrying the hurt out (rescue). (2) R6.5a gaps:
  bereavement done; a palisade (R6f) and arms carried at night; restocking raids. (3) R6.5d snow on
  maps (needs a cold premise), (4) R6.5f geology (volcano sites in v3 worlds, quakes on convergent
  boundaries - Tekheim and Zurug sit on them). (5) Performance exact cuts if time.
- Rejected this session: counting every known wrong as a motive to take back at once (claims first);
  a fire computed an hour ahead at the hour's start (people saw it frozen).

# Session handoff — session 19 (3 Oct 2026, extended autonomous run): the long-run audit, stone and iron, baskets and credit, fairs, the ferry, a famine people live through

`docs/PROGRESS.md` (session 19 bullet) and **D-138 … D-147** in `docs/DECISIONS.md` are the source of
truth. Arjun's rules held: no subagents; the living world first; one heavy process at a time; visible
action = simulation; nothing appears that nobody built, carried or learned; fix mechanisms, not
appearances; no forced trade, migration or founding (premises in `sim/agents/premise.lua`); browser
measured in release with rendering off; browser and LuaJIT identical.

## What happened (in order)
- **Long-run audit tools**: `tools/v3_chronicle.lua` (every memory to `<out>/mem.txt`, village states to
  `days.txt`, deaths and accounts to `summary.txt`; `--premise crowd:2 | fail_harvest:N | empty_stores:N |
  murrain:N`, several allowed) and `tools/v3_follow.lua --pid P --from-day D [--premise ...]`.
- **D-138 trade on a snapshot**: households' worths and spares (`econ.view`) planned with `econ.deal_plan`
  (shared by planning and arrival); seen and guessed gains apart (`trade_fade`); unsold trips 64% -> ~25%.
- **D-139 families**: a widow remarrying away takes her children; estates of empty households pass to kin;
  nobody sets out while the household is about to move; one left behind follows; hunters remember luck.
- **D-140 hunger answered**: households kill their own beasts, neighbours kill one for those with none, the
  herd counts before leaving, shore gathering, journeys put off while hungry, night feeds, relief on old news.
- **D-141 stone** (`sim/agents/stone.lua`): outcrops from planet slope/height (hard and soft rock),
  quarrying, stones carried one at a time, querns (cooking quicker; traded to villages without rock),
  stone footings (houses weather slower).
- **D-142 iron** (`sim/agents/iron.lua`): veins in hard rock and bog iron in wet lowland, known when seen;
  ore dug and carried, charcoal burned in a clamp, blooms smelted, iron edges forged and fitted
  (`hh.iron`: tool work 25% faster); traded where there is none. The sea's danger weighed in trade.
- **D-143 baskets and credit**: a whole good paid with a basket of spare goods; the rest owed between
  trusting households, collected in kind by the next trader, written off with lost trust after 48 days.
- **D-144 fairs** (`sim/agents/fair.lua`): called after the harvest by the middle village of a road cluster,
  heard of through people and criers, attended for reasons, barter person to person, news, kin, matches.
- **D-145 the ferry**: a household with no boat is sailed over by a household of its village with one
  (kin, friends, partners, any neighbour for a fare); the skipper keeps his word (`p.ferry`), packs his own
  provisions, takes them aboard, lands them (`migrate.land_ferry`) and sails home in the same boat.
  Hardship judged per household (`migrate.push`: own larder + part of the granary); couples leaving take
  no house (`settle.leaving`); fairs only by road, fit travellers, bread for the way home; the founding
  generator's waiting couple has parents.
- **D-146 seeing a young place**: close in, the canopy opens over the look target (`props.fp` `u_canopy`:
  foliage stippled away in a cone to the eye, trunks and shadows kept); the selection ring marks a v3 place;
  the inspector says camp / hamlet / young village, lists rock and bog as "In its ground", wraps long rows.
  A quern was never finished (stints booked bare hours at a pace below 1): fixed.
- **D-147 a famine people live through**: relief from need even without a surplus; with nothing left a
  household decides after a day; a young place with no granary draws only by a kin's offer; founding asks
  60% of its bread; health no lower than nothing and a day at the edge before death; starvation
  0.04 -> 0.015/h; minders and children following parents only on the same map.

## State at the end
- **main** at the final commit (see `git log`); shipped world unchanged (`assets/worlds/v3.tcsave`).
- Quick sim **155/155** (v3 46: new test_v3_families, test_v3_stone x3, test_v3_iron, test_v3_credit,
  test_v3_fair, test_v3_ferry); lint clean; `npm run build` OK; web 11/11 at the final build (W02 timed out
  on its last pause command twice under load, all its rate checks passing, and passed on rerun);
  `node scripts/v3_hash_web.mjs --release`: browser = LuaJIT at 0, 24, 48, 72 h (final state).
- LuaJIT `luajit -joff tools/v3_profile.lua --days 4`: **3.67 M instructions a game hour** (148 people;
  session start 3.93 M) - nav (A*) 27%, think 13.5%, stock 12.6%, map 12.5%, wild 10%, event queue 8%.
- Release browser (`node scripts/prof_web.mjs --release --hours 288 --runs 3`, rendering off): lower
  envelope **35.3 ms a game hour, a 12-day season 10.2 s** at the final state (37.0 ms/h at b99864c; start 39.2 ms/h, 11.3 s).
- Untouched 192 days: 399-439 exchanges (387 with fairs alone, 134 in 96 days at the start), unsold trips
  ~107-128, deaths only old age (3) and illness (6), no hunger, no drownings; Zurug, Dreik Knoll and
  Hottoodingle Hop quarry and forge; querns and iron travel to villages without rock or ore; Tekheim and
  Zurug hold a fair every local year; Little Leelee and Tekheim trade after meeting at the fair.
- Premises: crowded Zurug 96 d - Kodak founded day 3, huts day 12, no hunger deaths; island hardship -
  the boatless households ferried, fishers stay; inland total famine 48 d - hunger deaths 24 -> 1.

## Observations worth knowing
- Households with boats fetch relief by sea, so on islands they rarely leave; that is why "sailing
  together" was built and taken out (no real premise exercised it; D-147 Rejected).
- Performance: A* is already tight (unrolled, bucket queue) and its path cache is flushed only ~2 times a
  day per village with most queries unique; the event heap holds almost no stale entries. The remaining
  exact cuts are ~1-2% each (wild.threat block index 2.2%, herd scan centre ~0.5%, incremental cost
  rebuild 3%). Not done this session; measured and recorded.
- A loop scan (plans chosen 14+ times a day for 24 days) found only expected repeats (infants, play,
  one-log hauling, hourly fair steps). Useful again after any new work type: a stint must be booked at the
  worker's pace or it never finishes (the quern).
- Starvation is now ~3.5 days from the last meal (people eat three meals a day; two days had been too
  short for any help or move to arrive). Hunger push and relief are per household.

## Rejected / not done
- Sailing together (built, removed: see above). Fairs by boat (Dreik Knoll never goes to a fair). Querns and
  iron at fairs (whole goods sold by trips). Stone bridge piers. A ferry for founders. Labels for small
  places on the planet; camp smoke seen from afar. Exact performance cuts (see above). R6 conflict (not begun).

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
