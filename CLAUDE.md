# CLAUDE.md — Tiny Consequences

You are the sole engineer on **Tiny Consequences**, a Defold game for the **Defold Game Jam 2026** (theme: **Chain Reaction**). Arjun is the director; he playtests only at chapter boundaries. You build everything else autonomously.

**Deadline**: jam closes 5 Oct 2026, 03:29 IST. Internal submission target: **4 Oct, 20:00 IST**. Feature freeze: **3 Oct, 22:00 IST**. All times IST (UTC+5:30); run `date` to check.

## What the game is (one paragraph)

A cute tiny planet that lives on its own: weather, oceans, forests, animals, farms and individual villagers. The player is a god who nudges it (lightning, drought, culling wolves, sinking a tanker, meteors, volcanoes) and speeds up time to watch consequences cascade over years to millennia. Every major change is traceable through a visible chain of causes ending at **Nature** or **You**. Full vision: `docs/01_VISION.md`.

## Direction reset (D-050, 27 Sep 2026)

Arjun reset priorities: **build the genuinely living tiny world first**; the jams (Defold Game Jam 2026 and Game Gauntlet Sim Jam, themes Chain Reaction + Simulation) are secondary and a missed deadline is acceptable. The world-simulation spec is `docs/world/` (00 research, 01 audit, 10–16 design, 17 roadmap v2). It supersedes 02 §1/§5.10/§5.11 and 08 from Stage 2.3 on. Commit prefix for this work: `[wN]` (phase). No subagents unless Arjun allows them again.

## Direction reset v3 (D-078, 1 Oct 2026)

Arjun reset again: v2 people moved as scheduled groups. The target is an automated miniature
civilisation of persistent agents (small settlements that farm, cut wood, build, raise children,
trade, migrate and fight on their own); the visible action must be the simulation. Since session 10 (D-079)
**v3 is the default game and work happens on main** (the v3 branch is merged): spec `docs/v3/`
(00 audit, 10 agents, 20 character pipeline, 30 roadmap v3), code `sim/agents/` and
`game/view/agents_view.lua`. The legacy v2 world opens with `?world=v2` and stays until v3 reaches
parity. Commit prefix `[v3]`. Session 11 (D-085-D-090): `sim/surface.lua` is the one coast/river
function for the view and the sim; fishing, textiles and sheep are live systems. Session 12
(D-091-D-097): `sim/agents/stock.lua` (herds of sheep, goats, cattle, pigs), `wild.lua` (the W4
herds and packs as individuals on the maps; wolves, hunting), `trade.lua` (trade walked between
settlements; `p.at` is the map a person is on); measure the browser with `scripts/prof_web.mjs`
(the lower envelope of several release runs). Session 13 (D-100-D-109): `sim/agents/econ.lua` (worth,
news, exchanges, gifts, relief), `dogs.lua`, sea trade in `trade.lua`/`think.lua` (`sea_legs`);
watch the economy with `tools/v3_econ.lua`; trade is tested by creating real want, never expected.
Session 14 (D-110-D-113): the browser must live the same world as LuaJIT - `sim/dmath.lua` for every
transcendental in `sim/` (no `^`), `sim/fmt.lua` (`sfmt`) for text kept in state, check with
`scripts/v3_hash_web.mjs` (web test W10); drainage basins in `sim/geo.lua` (v3 worlds); birds are
agents in `sim/agents/birds.lua`; watch with `tools/v3_watch.lua`, `v3_follow.lua`, `v3_birds.lua`.
Sessions 15-16 (D-114-D-121; session 15 died before its handoff, `last_session.md`): measure the
browser's sim cost with probes that draw nothing (`render_off`, prof_web's default: headless
Chromium's CPU rasteriser competes with the sim) and compare LuaJIT instruction counts
(`luajit -joff tools/v3_profile.lua --days 4`); animals are individuals with no places in a group -
audit synchronisation with `tools/v3_animals.lua` (vcorr, mass starts/stops/rests, offsets),
`v3_pack.lua` (frame by frame), `v3_tracks.lua` (tracks from above), test `animals_are_individuals`;
every animal is clickable with its why; households migrate physically (`sim/agents/migrate.lua`),
brides walk to their husband's village, relief sails. **Bob forbids circular requires even inside
functions** (LuaJIT tests pass, the build fails): inject modules through `sim/agents/init.lua` and run
`npm run build` after adding a `require` between agent modules.
Session 17 (D-123-D-128): founding a new place (`sim/agents/founding.lua`: knowledge of places
`s.lore` from real contact only; camps, huts on cleared lots `settle.plan_lot`; the view adds
settlements at runtime), roads over the land (`trade.land_route`), kin help, the economy measured with
`tools/v3_market.lua` and `tools/v3_econ.lua --days 48` (trade by real want, never forced); premises
for tests and shots live in `sim/agents/premise.lua` (people created at runtime need `W.v3rt = nil`);
`scripts/shots_found.mjs` watches a founding in the game.
Session 18 (D-129-D-136): moving and founding by sea (`migrate.sea_plan`/`land`/`move_boat`,
`founding.best_sea_site`/`sea_place`/`beach`/`land_sea`, `think.sea_move_legs`; riders are
`boat.riders` + `p.riding`, passengers do `passage`); boats stay in for storms along the way
(`trade.sea_rough`), not rain; salt (`econ.GOODS.salt`, `saltpan`, WORK.salt) is made only on the
coast and traded inland; `s.beach` is a young place's landing (`s.landing` is a catch landed);
friends (`p.rel_id`) may live on other maps - check the map before any spatial use.
Session 19 (D-138-D-146): watch long runs with `tools/v3_chronicle.lua` (every memory in
`<out>/mem.txt`, states, deaths; `--premise crowd:2|fail_harvest:N|empty_stores:N`) and follow people
with `tools/v3_follow.lua --pid P --from-day D [--premise ...]`; trade is planned on a snapshot of
worths (`econ.view`, `econ.deal_plan`, shared with arrival); stone (`sim/agents/stone.lua`: outcrops
from slope and height, quarrying, querns, footings) and iron (`sim/agents/iron.lua`: veins, bog iron,
mine -> clamp -> forge -> `hh.iron`) are whole chains; baskets and credit between households
(`econ.debts`); fairs (`sim/agents/fair.lua`, news of them spread by `econ.on_learn`); the ferry
(`migrate.ferry_for`/`land_ferry`, the skipper's `p.ferry`); hardship is judged per household
(`migrate.push`: own larder + part of the granary); `settle.leaving` (no houses for those leaving);
work stints must be booked at the worker's pace (a quern was never finished); the canopy opens over the
close camera's target (`props.fp` `u_canopy`); famine (D-147): relief from need (`econ.relief_gift`),
`mig_desperate`, a day at the edge before death (`p.edge`, health never below 0); never compare building
ids or coordinates of someone on another map (`p.at`) with this map's (a toddler starved that way).
Session 20 (D-148, D-149): the roadmap is R4 -> R5 -> **R6 Conflict** -> **R6.5 Living Planet & Emergent
Response** -> R7. Both are built on shared primitive properties (`docs/v3/50_PRIMITIVES.md`: deeds known per
village, danger noticed by the senses, an urgent choice with dependants first, injury, condition of things,
boldness/arms, planet fields on maps); research `docs/v3/40_RESEARCH_CONFLICT_PLANET.md`, audit `41_PLANET_AUDIT.md`,
plans `60_R6_CONFLICT.md` (R6a-R6g) and `65_R6_5_LIVING_PLANET.md` (R6.5a-g). Never: war flags, hostility/panic/
grief numbers, per-hazard fears, aftermath controllers, scripted retaliation, palisades by count.
Built in session 20: `sim/agents/deeds.lua` (deeds, `s.dk` village knowledge, `own` secrets), `danger.lua`,
`conflict.lua` (taking, claims, blows, raids `trip.band`, walls, amends), `blaze.lua` (lightning, fire lived
continuously - never ahead of time), `people.hurt`, death deeds and burials; `think.decide` is at Lua's
60-upvalue limit (new branches as `M.*`); work/plan kinds and trip fields share one namespace (grep first:
`tend` = weeding, `trip.party` = a drove); probes need the test runner's `jit.opt.start(...)`; browser check
with premises: `node scripts/v3_hash_web.mjs --release --premise conflict|fire`.
Later in session 20 (D-155-D-164): the planet on the maps - wildfire (`blaze.planet`), quakes (`shake.lua`),
volcanoes (`volcano.lua`), tsunami (`tsunami.lua`; the map has no heights: low ground = near the sea),
snow and frost (`snow.lua`, `weather.walk_k/reach`), one roof-load rule (`shake.roof_load`); beasts and
fields in fires; fear of a place (`deeds.fear`), restocking, bands that wait. An interrupt whose reaction
falls after a step's end interrupts the next step (`p.int_late`, not a journey). A gone granary/dock/well/
woodstore/pen is forgotten and planned again (`founding.replan_lost`/`replan_pens`). Herds are kept to their
feed by people (the failing killed, a pig breeding stock, growers) - **re-check herd sizes and the cost of a
day-192 world** in long runs (`tools/v3_chronicle.lua --days 192 --save F`, then
`luajit -joff tools/v3_profile.lua --load F --days 4`): a day-0 profile hides growth. Run an 8-year untouched
world (`--days 384`) before handing off: a reused work kind (`lift`) starved villages only after year 5;
`test_v3_namespace` now fails on any WORK/PLAN name defined twice. Named premises
(`cold_snap`, `hard_winter`, `quake`, `eruption`, `tsunami`, `wildfire`, `fire`, `conflict`) work in tests,
the chronicle, `v3_follow`, the hash check and the `v3_premise` bridge.

Session 21 (D-169-D-173): flood water (`sim/agents/flood.lua`; infiltration excess in `sim/systems/hydrology.lua`
for daily steps only; `tools/v3_flood.lua`, premise `flood`/`floodN`); arms (`people.arm_for`) and kin help
(`help_kin`); climate premises `ice_age`/`hothouse`, heat stress; **R7 rethought** (`docs/v3/70_R7_GOD_POWERS.md`);
**the world news** (`sim/news.lua` - an observer, never writes W.S; `game/ui/newsfeed.lua`; bridge `news{go}`;
`tools/v3_news.lua`; stories only from deeds, events and live state; causes only from real links) and **world
audio** (`docs/v3/80_WORLD_AUDIO.md`; `scripts/gen_audio.mjs` regenerates/extends the library from
`assets/audio/manifest.json` with the ElevenLabs key in `.env` - gitignored, never print it; `game/audio/audio.lua`
reads state only - never call functions that create state lazily from the view, e.g. `volcano.list`). A headless
click must be held a few frames to reach Defold (`scripts/shots_news.mjs`). **R7 v1 powers** on the v3 world:
`sim/powers3.lua` wraps the `power` command (never require back into it); each power writes state with its root
event as the cause (tests `test_v3_powers`; `scripts/shots_powers.mjs`). The news, the feed and the audio never
write sim state (D-175). M mutes the world's sound.
Session 22 (D-176-D-178): **people created at runtime must be queued** (`life.push`; before, every child born in a
running world was frozen for years) and a rebuilt queue never schedules in the past (it includes dogs); runtime
caches that read mutable state reset at year boundaries (`sys.year`); v3 commands given mid-hour apply at their own
moment; animals face fire by its extent (`danger.fire_near`; tools `tools/v3_fire_animals.lua`, test
`test_v3_beasts_fire`); lightning takes exposed beasts; powers played with `tools/v3_power_matrix.lua` and
`scripts/play_powers.mjs`; a mid-life save round trip is checked with `tools/v3_save_mid.lua`. Open: the hydrology fix
in the session 22 handoff (verify first).
Session 23 (D-179-D-183): the hydrology fix applied and verified; **clouds v5** (`game/view/clouds.lua`, `cloudvol.fp`: a cover
map from the villages' own rain spells `weather.cell_day`, a full-screen march the camera flies through; `scripts/shots_clouds.mjs`;
SwiftShader pays for it - functional web specs use `gfx=low`); the village mix (`audio.lua` `M.MIX`, `scripts/play_audio.mjs`);
**chain reaction** - `sim/chain.lua` (an observer: what each cast caused, forward along real links; never writes W.S),
`game/ui/actions.lua` ("Your actions"), "chain N" on news cards, `tools/v3_chain.lua`; **R7 v2 powers** (sickness in a herd with
incubation and a carried cause, `sim/agents/blight.lua`, warm/cool sea, oil, meteor); every journey checks `p.nursing`.
Later in session 23 (D-184): **every site keeps a walkable yard** (a pit laid in a lane sealed a house; `map.cut_doors`, the
chronicle prints DOORS CUT OFF; test `test_v3_sites`); a work step's loop is walked only as far as nothing closed stands in the way
(`nav.los_last` - children were carried through walls into the sealed pocket); infants recover health when fed (they never did);
`journey_ok` keeps nursing mothers and women near term at home and babies are fed on trips; **no `^` in `sim/`** (lint rule `pow`);
aim previews (`powers3.preview`, reads only, the cast's own target choices); the flood's edge by marching squares; the news so far,
the Sound pill; oil on a coast fouls the shore and oils the gulls (D-185; premise `powers2` for the R7 v2 powers); no plan may need a
house its household has lost - a household with no host camps by the square (D-186); R6f the palisade
(`conflict.palisade_*`, D-187: laid out from remembered harm, every section checked with `map.cut_doors(s, true)`). Follow a long run's deaths from a year-boundary snapshot (`v3_chronicle --days N --save`, N a multiple of 48;
`tools/v3_household.lua` for a household's larder and people) - a year0 save is not an exact continuation.

Session 25 (D-205-D-213, the jam submission): Arjun's playtest is the authority - powers recharge in game time (`powers.cool_h`, meteor
7 days); the meteor's strike is its own danger (`impact` alarm, `think.impact_choice`, crater 0.2); beasts die in minutes in flames; v3
planet fire burns out in 2-3 days and is never drawn over a map's trees; Go there anchors (`data.at_sid/at_x/at_y` on cast events,
`news.focus` at_cell/follow); oil drawn from `F.oil` as a black slick; menus `game/ui/menu.lua` (title pauses the world; `?test=1` skips
it, `?menu=1` shows it), the tutorial `game/ui/tutorial.lua` + logged command `sim/tutorial3.lua` (`sim/tutorial.lua` is the LEGACY v2
helper); `docs/v3/86_S25_PLAYTEST.md`. Part 2 (D-214-D-219): the game opens on the whole planet (camera preset `title`); skips are seen
from above (`game/view/skipcam.lua`); the tutorial is a meteor at the woods' edge and its real chain; meteor/oil/crater visuals in
`game/view/fx.lua` (trail, crater marks, oil blobs); blight tuned to take hold (`blight_cast` 0.35). Part 4 (D-221-D-226): rain puts fire out (`fire_rain_grow`, `rain_quench_v3`); the
water/path meshes keep every vertex on the ground and never shrink; phones (touch, pinch, landscape only) and the Wavedash glue live in
`game/html/engine_template.html` (`window.tcWD`, `npm run build:wavedash`, `wavedash.toml`); **zip with bsdtar, never Compress-Archive**
(backslash paths broke itch; Python's zipfile hides them on Windows); store media in `dist/store/` (`out/s25/gifs3.mjs`).

## Read order

1. This file.
2. `docs/PROGRESS.md` — where we are. **Always read first in a new session.** Also glance at `HUMAN_INPUTS.md` (values only Arjun can provide).
3. Any `docs/playtests/chapter-N-feedback.md` not yet marked as read in PROGRESS.md — turn it into tasks before continuing.
4. `docs/world/17_ROADMAP.md` (roadmap v2, current phase) and `docs/08_ROADMAP.md` for the later chapters.
4b. `docs/world/10_WORLD_SYSTEMS.md` and the companion docs for the phase you are in.
5. The design docs relevant to the stage: `02_WORLD_MODEL.md` (sim), `03_CAUSALITY.md` (blame/events/chains), `04_PLAYER.md` (powers/UI/saving), `05_ARCHITECTURE.md` (code structure, rules, build), `06_PRESENTATION.md` (look/sound/writing), `07_TESTING.md` (tests).

In the very first session, read all of `docs/` completely.

## Autonomy rules

- Work through the **whole current chapter** without stopping. Make decisions yourself; record anything that changes design, scope, budgets or architecture in `docs/DECISIONS.md`.
- **Stop only when**:
  1. the chapter's playtest report is written (`docs/playtests/chapter-N.md`), or
  2. a human-only action blocks *all* remaining work (credentials, a system install needing admin rights, an account). Write exact instructions in PROGRESS.md "Blockers", do everything else first, then stop, or
  3. continuing would cut something from the roadmap's "Never cut" list or move the submission target.
- When stuck on the same problem for ~45 minutes: write it down in PROGRESS.md, try a simpler approach, or apply the roadmap cut order.
- Check the clock at every stage boundary against `docs/08_ROADMAP.md` targets. If behind, apply the cut order and log it.

## Session start checklist

1. `date` (IST), `git status`, `git log --oneline -5`.
2. Environment: `node --version` (≥ 20), `java -version` (≥ 21), `luajit -v`, `git --version`. If `tools/bob/bob.jar` is missing, run `npm run setup` (once it exists).
3. Read PROGRESS.md; confirm the current stage; continue.

## The stage loop

For each stage in the roadmap:

1. Write a 3–8 line plan under "Current state" in PROGRESS.md.
2. Implement in small steps. Build and test often:
   - sim work → `npm run test:sim` (includes `lint:lua`; and `npm run sim -- --seed N --years Y --map` to look at outcomes and maps)
   - Defold work → `npm run build` (bob is your compiler), then `npm run test:web`
   - visual work → `npm run shots`, then **open the PNGs and check them** against `docs/06_PRESENTATION.md` §6
3. Fix until the stage's **Done when** criteria pass.
4. Append a stage entry to PROGRESS.md (what, tests, numbers, issues), update "Current state".
5. Commit: `git add -A && git commit -m "[chX.sY] <summary>"`.

A stage is not done if the build is broken, a required test fails, or PROGRESS.md is stale.

## Hard rules

**Simulation**
- `sim/` and `content/` are **pure Lua 5.1**: no Defold APIs, no LuaJIT-only features (HTML5 runs Lua 5.1.4). They must run headless under LuaJIT from the repo root. LuaJIT accepts things Lua 5.1 rejects or computes differently (e.g. `math.log(x, 2)`), so keep `npm run lint:lua` clean (05_ARCHITECTURE.md §4).
- **Deterministic**: all randomness via `sim/rng.lua` (counter-based, keyed by stream/cell/tick). Never `math.random`, `os.time`, `os.clock`, or `pairs()`-order-dependent logic in `sim/`.
- **No scripted consequences.** Chains must emerge from field couplings (`02_WORLD_MODEL.md` §0). Never make a scenario pass by special-casing its outcome.
- Every tunable lives in `sim/config.lua`. Tune freely; changing a *tuning band* or *test threshold* needs a DECISIONS.md entry.
- Every system that writes a causal field reports blame terms (`03_CAUSALITY.md` §3). Build blame together with the system.
- Adding a per-cell field needs a DECISIONS.md entry (the field budget is deliberate).
- Zero allocations in per-tick hot paths; struct-of-arrays; sparse sets for rare things.

**Game/view**
- The view never writes sim state. All player actions are commands, applied at the next tick and logged.
- Pool game objects; never create/destroy per frame; respect the budgets in `05_ARCHITECTURE.md` §9.
- Before using an unfamiliar Defold API, read the docs: **https://defold.com/llms.txt** (manuals index `https://defold.com/llms/manuals.md`, API index `https://defold.com/llms/apis.md`, examples `https://defold.com/llms/examples.md`). Don't guess APIs; confirm by building.
- Wavedash: `docs.wavedash.com/engines/defold` and `docs.wavedash.com/sdk/cloud-saves` are the source of truth. `wavedash.init()` must be called in the Wavedash build or the game stays hidden.

**Assets & legal**
- **Never copy code or assets from Tiny Skies** (no licence). Visual reference only.
- Only use assets you generate, or ones with clear permissive licences (CC0, OFL, MIT). Record every third-party asset and its licence in `assets/CREDITS.md`.
- Arjun may drop assets (e.g. a music track) into `assets_inbox/`. Check it during polish stages; ask about the licence in the playtest report if it's unclear.
- No runtime network calls except the Wavedash SDK. No analytics.

**Docs**
- Docs 01–08 are the spec. If the design must change, update the doc *and* add a DECISIONS.md entry. Don't let code and docs silently diverge.

**Git**
- Commit at least at every stage end, message prefix `[chX.sY]`. Never force-push or rewrite history. Never commit secrets, `dist/`, `out/`, `tools/bob/`, `node_modules/`.

## Commands (see `05_ARCHITECTURE.md` §3)

`npm run setup` · `npm run sim -- --seed 42 --years 100 --map` · `npm run lint:lua` · `npm run test:sim [-- --full]` · `npm run bench` · `npm run build` · `npm run build:release` · `npm run build:wavedash` · `npm run serve` · `npm run test:web` · `npm run shots` · `npm run play` · `npm run package:itch` · `npm run deploy:wavedash`

You create these scripts in Stage 1.1 and keep them working.

## Working efficiently

- Keep context lean: grep instead of reading huge files; tail logs; write long outputs to `out/` and read summaries.
- Use subagents (if available) for side work that doesn't need the main thread's context: reading Defold manuals for a specific API, drafting dialogue batches in the documented voice, reviewing a batch of screenshots or event chains with fresh eyes.
- Prefer small, verifiable steps over big rewrites. Build/test after each meaningful change.
- PROGRESS.md must always let a brand-new session resume without guessing.

## Playtest report (end of chapter)

Write `docs/playtests/chapter-N.md` using the format in `docs/playtests/README.md`: build hash, how to run (`npm run play`), what's new, what to try (10–20 minutes), 5–8 specific questions, known issues, key numbers, interesting screenshots. Commit. Then stop and tell Arjun in ≤ 5 lines what to test.
