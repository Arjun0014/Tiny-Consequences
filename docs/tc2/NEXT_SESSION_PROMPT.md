# TC2 — next session prompt (restore direction after a total loss of context)

You are continuing **Tiny Consequences 2.0**, a Unity 6.6 (6000.6.4f1) Web game in
`C:\Game Dev\tiny-consequences\unity\TinyConsequences2`, beside the legacy Defold game (TC1, untouched,
research material only). Arjun is the director; work autonomously; **no subagents**; do not stop at plans
or documents; research -> design -> decide -> smallest real slice -> compile -> test -> run -> inspect ->
benchmark -> fix -> commit -> update docs -> next slice. Continue until told to stop. Blockers needing a
human (UAC, payment, credentials, licence acceptance) go to `docs/tc2/PROGRESS.md` "Blockers" with a
workaround; keep working on everything else.

**Read in order**: `docs/tc2/NEXT_SESSION_PROMPT.md` (this), `SESSION_HANDOFF.md`, `PROGRESS.md` (CURRENT
STATE), the newest entries of `DECISIONS.md`, then the design doc for the area you touch (`03`
architecture, `04` time/scheduler, `11` history, `12` save/offline, `05`-`10` domains, `13` rendering,
`14` performance, `15` testing, `17` roadmap). Then `git status` and `git log --oneline -15`.

**After a context compaction (mandatory): DO NOT CODE YET.** Re-read this file, `SESSION_HANDOFF.md`, `PROGRESS.md`
CURRENT STATE, the newest `DECISIONS.md` entries and the domain docs in play; run `git status` and
`git log --oneline -20`; check every running process and output file the handoff names (and
`tc2/out/compact/last_precompact.md`); reconstruct the exact step; then continue autonomously. The repo's hooks
(`.claude/settings.json` -> `.claude/hooks/tc2_compact.mjs`; Claude Code 2.1.289: `PreCompact` stdout joins the
compaction instructions, `SessionStart` matcher `compact` stdout is shown to Claude) inject this automatically; the
root `CLAUDE.md` carries the same protocol in case they fail. Before a compaction is likely, checkpoint: commit, then
DECISIONS, PROGRESS, SESSION_HANDOFF, this prompt, measurements and the exact next step.

**The end goal**: a beautiful miniature planet that lives on its own for thousands of years (geology,
climate, ecology, evolution, persistent people, settlements, knowledge, conflict, polities, collapse,
ruins); zoom from orbit to one person; accelerate; leave and come back to the same world aged by the
offline mode (a day away ~ a century in Living mode), simulated for real; a History Book where every
story happened and every event links to its causes (NATURE / PEOPLE / YOU). No scripted consequences,
no unlock ladders: world rules, not stories.

**Settled architecture** (do not reopen without new evidence): pure C# 9 core `TC2.Core` (no UnityEngine;
also compiled by `tc2/headless` with the editor's bundled dotnet), Burst kernels only where measured, no
Entities (not on the Web); one ordered event queue `(time, phase, seq)` with lazy cancellation, serialized
exactly; int64 seconds, 24 h days, 48-day years; resolution layers PROCESS / ACTIVITY / COARSE /
ELABORATION - watching never changes history; equiangular cubed sphere N32/N64, R = 6 km (D2-085: the ground in geo-lengths of a 10 km sphere, living laws per hectare); causal history
ledger with tiers, roots, importance, compaction; exact binary saves with an IndexedDB store; offline modes
Gentle/Living/Ancient/Deep time; URP with WebGPU first and WebGL2 fallback, instancing without BRG;
determinism bit-identical across dotnet / Mono / Web IL2CPP (DMath, keyed RNG, double arithmetic, ordered
iteration).

**Commands** (see `SESSION_HANDOFF.md` for the current list): headless tests and runs via
`node tc2/scripts/headless.mjs ...`; Unity batch via `node tc2/scripts/unity.mjs ...`; Web build, serve
(COOP/COEP) and Playwright via `tc2/scripts/*.mjs`. One heavy process at a time (11 GB RAM).

**Direction from Arjun (5 Oct 2026)**: the world must be a *really beautiful miniature* with a high level of detail
and high performance - look-dev is a first-class track beside the simulation (D2-026). Judge every visual change in
real game screenshots (`node tc2/scripts/unity.mjs shots`), not in debug maps; keep 60 fps in the Web build.
Headless Blender (`tc2/art/kit.py`) makes the asset kit. The god powers that exist (eruption, quake, storm, lightning)
are logged commands and double as screenshot tools (`unity.mjs shots --erupt/--quake/--storm`).

**Direction from Arjun (6 Oct 2026, D2-079)**: "the entire world looks so empty and boring" - the planet's plants and animals are
simulated but barely drawn. Make the living world visible (forests at their true density, woodland seen from afar, ground cover by
biome, animals readable), read from the simulation's state and judged in real screenshots, before F8.

**Where we are (session 2, 6 Oct 2026)**: F1 to F7 are closed (D2-043, D2-046, D2-060, D2-065, D2-068, D2-076, D2-078).
Villages of persistent people live on their own, carry their technology as knowledge, fall sick and recover, trade and send
relief, and - when scarcity bites - take from each other at night, strike and are struck, ask amends, take back, raise bands,
raid, wall themselves in palisades and bow to stronger neighbours as tributaries; every one of these is a deed known per village
and every event traces to its causes. The History Book (D2-077) reads the world's ages after the fact from its annals - chapters,
places and lives whose every line is a record with its causes. Determinism holds across dotnet, Mono and the Web build and is
linted (D2-057). Next (SESSION_HANDOFF "Next steps"): the living world made visible (D2-079), then F8 - deep time. Run long
headless jobs from a scratch copy of the binaries so the repo's DLL stays free; another Claude session may share the machine -
never kill its processes, and measure only when it is quiet.

**Where we are (session 3, 6 Oct 2026, evening)**: the living world made visible (D2-080-D2-082: woods drawn from the simulation,
the far woods as masses, ground cover by biome, villages' signs; the Web build at 65-69 fps near the ground). F8 started: DEEP (04
§4.3b, calm villages live up to twelve days at a step) and its calibration against COARSE (D2-083, in progress): six rules of "what
a step must keep of its days" fixed every bias found; COARSE hashes unchanged; the final multi-seed run is pending (PROGRESS
"Blockers"). Then: DEEP's early growth on small worlds, speed (per-step costs, the planet), Ancient and Deep time in the browser.

**Direction from Arjun (7 Oct 2026, D2-084) - the current priority**: "the simulation exists, but almost none of it is legible from
looking at the world." Make the world legible from the picture at the orbit, region, village and close views, and let it carry its
history (`docs/tc2/18_WORLD_READABILITY.md`, roadmap phase W): audit what the simulation knows against what is drawn, then landscape
composition, forests, rivers and river corridors, biomes and ground cover, terrain and geology, settlements that read as places (and
whether founding fragments people too much - fixed in the simulation if so), wildlife and activity, persistent scars, orbit-scale
legibility, a multi-age acceptance (the same seed at years 0, 25, 100, 300, 1,000: "can I see history without reading the date?"),
browser performance at 60 fps. **No simulation theatre**: presentation may elaborate and exaggerate real state, never invent it; add the
smallest real state where a meaningful difference cannot be drawn. Judge across views and ages, never one cherry-picked angle. The UI
gets a full art pass later (no debug panels at ship). Invisible simulation features wait until this is convincing.

**Where we are (session 4, 7 Oct 2026)**: D2-083 (DEEP calibration) closed - six seeds at par in people and per-person rates, residual
leans in the wild choice recorded. Visual slice 1 of D2-084 in progress (stands and glades, palette, relief, rivers, tree kinds). Machine
rule: total CPU and RAM ~50 %, one heavy job at a time, low priority, never kill processes you did not start (the classifier also refuses
to stop this session's own leftover runs - let them finish).

**Direction from Arjun (7 Oct 2026, D2-085) - before D2-084's polish**: reconsider the planet's physical size. D2-007's R = 10 km was
chosen before any playtest; the world feels enormous and empty. Study TC1's real scale, test smaller TC2 planets (N64 kept) really
simulated - population, interaction density, travel, performance, comparable shots - decide the radius and change it now if the evidence
supports it (old saves may be invalidated), then redo D2-084 on the chosen scale. Fewer people is fine (2,000-8,000 mature), more detail
per person, settlement and feature; never a world where everyone reaches everyone. The study: `docs/tc2/19_PLANET_SCALE.md`.

**Where we are (session 5, 7 Oct 2026)**: D2-083 closed (22cad8d). **D2-085 made: the planet is 6 km** - the same ground at any size
(geophysics in geo-lengths of a 10 km sphere), living laws per hectare and real-size patches, the view relative to R; a village with its
fields is 0.17 of the globe (TC1's 0.16); mature worlds of 2,000-7,100 people in 10-30 villages; 177 tests and the Mono probe at 6 km.
D2-084 on 6 km began: villages keep their shape (D2-086), homes the sea takes are lost and one shore serves sim and view (D2-087,
D2-088), people readable at the village view. Next: SESSION_HANDOFF "Next" (farmland, rivers, settlements as places, wildlife, scars).

**Direction from Arjun (7 Oct 2026, D2-089) - the standing direction**: TC2 is an *autonomous medieval civilisation inside a living
miniature planet* - the system breadth and readability of a game like Age of Empires (towns, buildings with purposes, workers, roads,
mines, farms, docks, fighters, walls, kingdoms, wars, trade) with no player giving orders: the agents play the civilisation game.
Fewer, deeper people (dev worlds 100-500); every structure, road, army, ship, mine, wall and kingdom arises from simulated need,
knowledge, resources, labour, politics and history (the ten questions, `20` §2); the person page is non-negotiable; presentation develops
with the simulation (D2-084). Read `20_CIVILISATION_DEPTH.md` (audit, architecture), `21`-`24` (settlement economy, materials, force,
transport and sea) and `25_CIVILISATION_ROADMAP.md` (C1-C13; **the current slice is C1 "the working town"**, §2).

**Where we are (session 6, 7 Oct 2026)**: D2-089 written up (`20`-`25`) and **C1 "the working town" closed** (D2-090 to D2-096): structures by
kind with works by need and their causes (well, granaries and barns with real capacity, quarries, smithies, training grounds, markets),
timber felled from real woods (woodlots recede; fields clear what they farm), stone known by seeing it and carried home (houses on footings),
arms forged only at a smithy by villages that fear their neighbours, fighting skill from practice and fights survived, markets made by
traders, rings on dry ground; building page in the HUD. Since: C4.1 roads by use (D2-097), C7.1 rings that grow (D2-098), C12.1 the
person page (D2-099: `Core/History/Biography`, the HUD's Life page, headless `person`), C5.1 copper from the ground (D2-100: finite ore
bodies seen, mined, carried and dug out - `Core/Places/Mining.cs`), C5.2a charcoal (D2-101: burnt at a hearth by the woodlot from real
forest; smelting burns it), C5.2b iron (D2-102: bog ore, the bloomery found by copper smiths, iron tools and arms; a COARSE smith's labour
day), C5.3a places by their ore (D2-103), C5.3b iron traded (D2-104), C5.4 whose ore (D2-105: digging another village's worked ore is a taking), C12.2 the settlement page (D2-106; the
person page checked in a Web build), C10.1 the lord's call (D2-107). Next: SESSION_HANDOFF "Next" (the C10.1 population drops, the Web
frame rate, then C5.3c)
(SESSION_HANDOFF "Next").

**Where we are (session 7, 7 Oct 2026, evening)**: the C10.1 population drops followed (D2-108): over ten seeds there is no systematic
loss (seed 2's drop was an epidemic, seed 7's Voton's famines - judge mechanisms by century means, never the last year), but the lord's
half-share was a second wrong: now a lord answers its tributaries' wrongs (at half weight) and its band's loot settles the tributary's
wrong, so a wrong is avenged once. The Web frame rate (D2-109): the frame was bounded by the main thread's draw submission - per-instance
colours and draws with their own bounds took the near views from 60-70 to 90-102 fps. C9.1 boats from the woods (D2-110, sim version 49,
252 tests): dugouts made by fishing households, rowed out to the open sea's shoals, lost in rising wind - committed, but its final checks
(ten seeds after the landing fix, Web build, shots) are open: SESSION_HANDOFF "Next".

**Where we are (session 8, 7-8 Oct 2026)**: C9.1 closed (D2-110: boats on the sand by the water, ten seeds clean; Web build 5/5). **C9.2a the landing and the
crew** (D2-111): one landing a village on the drawn waterline, boats in a row a metre above the sea; a boat goes out with its crew (lived days had caught a third
less from boats than COARSE ones); nobody stands in the sea (shore fishers at the waterline; the green where the sea took the square). **C9.2b boats of
planks** (D2-112): a technique found by woodworkers at a dugout, a boat shed at the landing, plank boats built there (four aboard, harder weather, 25 years);
sim version 51 (Web test 5/5). **C9.3 voyages** (D2-113, IN PROGRESS, sim version 52): ways over the sea, a boat's load in trade, grain by sea
to famines - its relief raises famines x1.18 over ten seeds: find why first (D2-113 "Open", SESSION_HANDOFF "Next").

**Where we are (session 9, 8 Oct 2026)**: **C9.3 closed** (D2-113): relief by sea's extra famines traced and fixed by **social storage** (relief owed back - a giver gives less to a village that has not returned its gifts; given back when the harvest allows); **voyages lived** by real crews along the sea ways, at par with COARSE. **Direction from Arjun (8 Oct 2026, D2-114) - a standing visual requirement**: building variety - homes vary by household size, means, material, age, history and tradition (families of plans, roofs, materials, extensions, chimneys, yards, wear; never random colours); functional buildings recognisable at a glance; richer, larger, older places denser and more substantial; judged in real shots at close, village and region views (root `NEXT_SESSION_PROMPT.md`, SESSION_HANDOFF "Session 9"). **D2-114 in progress** (committed as work in progress at the session 9 handoff): forms recorded when buildings are raised, traditions carried by founders, rebuilding by households of means, the kit's house families and new works - next: Unity compile, a cap on hall-houses, the ten-seed reading, real shots, the pages, Web.

**Where we are (session 10, 8 Oct 2026)**: **D2-114 building variety closed** - judged in real shots: the first towns were one cottage repeated under one dull thatch or one orange tile on a bare disc, because the household's "means" separated nobody (larders hold days; the grain is the village's). Now means read beasts, boats, goods beyond need, working hands and the years of grain in the granary; one hall a village (one per 150 people); plans spread over ten worlds 37/28/13/21/1 % (one-room/cottage/long/two/hall); roofs are kept and weathered house by house, kitchen gardens, doorstep yards, beaten training grounds, a sooted smithy; the pages tell how a house was built and why. Ten seeds inside the noise; Web 6/6. Then **C9.4 surveyed and deferred** (D2-115: no demand for cargo boats in a barter economy - it waits for C3 exchange) and **C13.1 streets** (D2-116, sim version 55): ways worn to paths become durable streets, frontage is sought, villages grow along their roads and houses face them; conflict falls by a traced mechanism (more homes near the store, takers likelier seen); Web 6/6. Then **C13.2 back lanes** (D2-117): a lane either side of each street, filled behind its frontage - big villages read as towns of streets and lanes; Web 6/6. **Next**: C13.3 (rows in a town's core, the market place) or C10.2 (the political centre: needs lordships that hold) - SESSION_HANDOFF "Session 10".

**Where we are (session 11, 8 Oct 2026)**: **C13.3 a town's ground (D2-118)** - every building covers real ground (`Footprints`: plans' rectangles
turned to their recorded bearing `Building.Face`; works as discs); no house ever stands in another (C13.2 had 90-312 overlapping pairs a world: 0 now
in all ten seeds); a town's street frontage near its green is divided into narrow plots with rows of the new narrow town house (gable to the
street); a market place widens with the traders who come and is kept open; streets, lanes and rings laid between houses; houses drawn in the
village's own frame (they had stood off their streets). **Open W task W-FOREST** (Arjun): forests/biomes still read as the same trees recoloured -
not solved (`18` §8). **Next**: Web test, page fps on a quiet machine, then **C3 exchange** (SESSION_HANDOFF "Session 11").

**Where we are (session 12, 8 Oct 2026)**: **C3.1 the weaver's shed and the village's wares (D2-119)** - measured first: goods sit at target, people
idle ~8 h a day, home making costs only spare hours, fibre is what is short - so wares last (0.6 of homespun's wear), a shed stands where a village's
wear keeps six makers busy (larger places), its eight best weavers make wares there from real fibre, households take them before making their own,
and wares are traded, valued by the hours they save: trade x3, home making hours x0.28, market places x1.35, people at par (wares go
mostly for fibre - the flax chain; little grain). **A lived save and load
continue exactly again** (pre-existing: caches of the day's lakes and flows - the green, the drawn shore, the river lines - went stale in a running
world): never cache what moves daily without keying it to that day (`PlanetState.WaterDay`) or deriving it from the ground's shape only
(`PlanetState.InBasin`); test `SaveLoadContinuesIdenticallyWhileLakesRiseAndFall`. Works on the main road are laid beside it. **C3.1b (D2-120)**: more sheds where a town's people outrun its
looms (towns of 500+ carry 2-4). **Next**: SESSION_HANDOFF "Session 12" (C3.2 the potter's kiln, or C3.3 exchange between households).

**Where we are (session 13, 8 Oct 2026)**: **C3.2 the potter's kiln, the village's jars and its hearths' pots (D2-121)** - measured first: D2-063 gave
every village that knew pottery a sealed store and every household with a potter its cooking pots, also the villages with no clay within reach. Now
pots are made of real clay with real fire and break: clay by the ground's shape (a river draining 200 ha or a lake's bed within 1.2 km); jars in the
granaries keep the grain they seal, only from a kiln; a household that cannot fire its own cooks in kiln pots or on the full wood; a kiln at the clay
where the store's grain lacks two months of jars, its four best potters at both resolutions, ware from the clay and the village's wood; ware a trade
good valued by the grain it keeps; the potter's role from practice. Ten seeds against the same binary without kilns: 84 kilns in 169 villages,
2,000 t of ware traded, trades x5.4, **markets x3.6**, people and rot at par. C3.3 (exchange between households) was surveyed and deferred as a phase
(the village's store moves 31 % of the food households draw: household property needs a moral economy). **C4.2 carts is in progress** (households'
handcarts and ox carts where their backs are too small; famines x0.83 but people x0.955 over ten seeds - to be explained first). **Next**:
SESSION_HANDOFF "Session 13".

**Where we are (session 14, 8 Oct 2026, evening; a short session stopped by Arjun)**: C4.2 carts is still in progress. The people x0.955 was
investigated: the per-person rates are identical with and without carts. The loss in level comes from chance (one larger epidemic in seed 4, an
early split in seed 42 with almost no cart trade), and a ten-seed placebo spreads as widely (carts against it x0.972, t -1.06): noise, no fault.
The carts' real effects: famines x0.83, hunger deaths x0.93, trades x0.70. **Next**: close C4.2 (D2-122, shots, the full suite, Web) -
SESSION_HANDOFF "Session 14".

**Where we are (session 15, 8-9 Oct 2026)**: **C4.2 carts closed (D2-122)** after its shots found three faults, each measured and fixed: carts were begun
while hunger sent the village to the wild and never worked (15.5 % given up -> 4.9 %); **nobody works in the water (D2-123)** - a new audit (`wet`)
found 0.64 % of all person-hours spent standing in the sea or a lake (field spots the water had come over, coastal woodlots, hunters): lived work
spots now go ashore and woodlots are chosen above the shore (0.05-0.08 % left); the yard camera framed a roof. **River crossings surveyed (D2-124)**:
every stream a worn way crosses is a fordable brook (0.08-0.23 m3/s at most) - bridges deferred. **C1.4b the village smith (D2-125, sim version 61)**:
the person page had called 5,730 people "a smith" and "a boatwright" (all who knew the craft) and smithies had no smiths (every copper-competent
person worked at one in turn, up to 355 hands a year); now a smithy's four places go to its best smiths by craft and practice, practice quickens the
hand, another smithy rises only where the steady need outruns the places, and smiths and boatwrights are read from their work - people and food at
par over ten seeds; iron exports shrink where a town cannot smelt beyond its places (open: the bloomery at the ore, C5.3c). **Next**:
SESSION_HANDOFF "Session 15".

**Where we are (session 16, 9 Oct 2026)**: **C3.3 exchange between households (D2-126, 889263b)** - measured first: dividing the harvest by each
household's field work would have turned the COARSE scheduler's order (oldest residents to the fields first) into property; so the commune's harvest is
shared by mouths into each household's own grain in the village's granary (`Core/Places/Holdings.cs`), beside the village's common grain; larders draw
own grain, the common grain, kin's spare (gifts), then loans recorded as debts and repaid from surplus; makers are paid in kind (the food of the
maker-days their make took) as far as the taker's grain pays, the village paying jars and smiths from its common grain then a levy; in hard times
sharing contracts to the household and its kin (famine amid plenty). **C10.2 the village's first household (D2-127, 78ac1ee)**: followings of kin,
debtors and clients (`Core/Society/Leaders.cs`); the household most follow leads, its eldest is first among the people; it feeds the hungry in hard
times; tribute goes to the lord's first household. **C10.3 a contested succession splits a village (D2-128)**: at a vacancy an unbound rival's
following (half the first's, six households) leads them away to the best place the village knows, or looks for land and gives up after three years;
42 splits over ten worlds, people at par; land decides how many villages are founded (splits take 35 % of foundings); towns of 400+ keep large
unbound factions a full land cannot shed. The pages tell splits and headmen (a page-only index of political events). Then measured, not assumed:
**there is no famine amid plenty** (D2-126 corrected: hard-times hunger deaths are real shortage, in the last days before the harvest) and nothing
answered scarcity before death - so **C3.5 household formation (D2-129, sim version 65)**: a couple marries when what it brings of its households'
grain keeps it to the next harvest; else the match waits, brides marry where the grain is, some never marry. Hunger deaths x0.69, famines x0.70,
births x0.88, people at par over years 200-299. **W-FOREST pass 1 (D2-130, presentation)**: each tree's kind of wood is read from the
simulation's climate and water (rain forest, dry tropics, warm temperate, broadleaf, mixed, boreal, treeline, carr, gallery), with ten new forms,
stand ages, palettes, far forms and the far canopy by kind - still OPEN for Arjun's judgement in real shots. Then **C3.6a working the land harder
(D2-131, sim version 66)**: measured first - where land bound, labour never limited a crop stage and the hungry gathered at 0.02-0.08 FU an hour
against ~1.7 in the fields; now a village whose fields are short of its want plans extra ploughing and hoeing a hectare at sowing (at most +80 % of
the season's hours for +26 % of the yield, at falling returns; only work done counts). Ten seeds: yields a hectare +16-24 % where land binds, then
the people grew into it (x1.17 over years 200-299, x1.27 at year 300) with hunger a person-year unchanged - Boserup and Malthus together; run time
unchanged. Then **the first sheaves (D2-132, sim version 67)**: 66 % of hunger deaths fell in the ripe stage beside the reaped crop (the harvest
reached the store on its last day); now the grain comes in as it is reaped and villages ration to their next new grain - the gap falls before the
grain ripens, as the year's did, and hunger a person-year is not lower (Malthus again). **Next**: SESSION_HANDOFF "Session 16, third context" (the
preventive check: household land and marriage on a holding). Then **C3.6b household land (D2-133)** - each household holds hectares of its
village's fields (founders by heads, portions of spare land, the last out, heirs, land given out at sowing to the least landed) - and **C3.6c the
harvest by land and labour (D2-134, sim version 69)**: half by the land held, half by the hands that worked; the landless live by their hands and
marry later or never (C3.5's rule) - hunger a person-year x0.74, people at par: the preventive check from the households' economy. **Next**:
SESSION_HANDOFF "Session 16, third context". Since: **C3.6d who leaves (D2-135)** - each household weighs leaving by its own land (in full lands
the land concentrates) - and **C3.6e the old hand on their holding (D2-136, sim version 71)** - an old landholding household goes to live with a
child's household (else a land-short one with hands) with its land and grain: the decades-long hoards and most landlessness were the old's. Its Web
build's catch-up ran out of memory: **traced (D2-137)** - a Living catch-up of a small world is ACTIVITY (headless `run --seed 42 --years 96` is the
browser's world), two parties had founded villages on one cell, and visitors walked the way of no length between them forever; a party now joins a
village standing at its place, a way of no length arrives where it goes, the travel index sweeps stale legs. Then **D2-138**: a headman gone to live
with their child stays first (no vacancy to contest), and a household's followers follow the household that took it in. Then **C3.6f the soil
(D2-139, work in progress)**: the fields tire, rest and the herds' dung give back; worlds carry ~0.6 of their people - but births do not answer
the poorer land and hunger a person-year rises x1.9: the marriage rule (C3.5) does not bind. Next: **C3.5b the niche** (a couple marries when its
land and hands keep it; measured code in place, off by default) - SESSION_HANDOFF "Session 16, fourth context", "Next, in order".

**Direction from Arjun (10 Oct 2026, session 17) - presentation and simulation advance together**: he opened the game and saw people standing
still, houses on roads, a world that does not show its simulation, forests still alike. A real-game audit first (`27_VISUAL_TRUTH.md`), then
W-FOREST pass 2, then the roadmap - and from now on every new system answers "what changes in the world picture?" (nothing visible: is that right,
or is an inspector line enough, or is a truthful representation missing). No simulation theatre: presentation elaborates a real activity, never
invents one.

**Where we are (session 17, 10 Oct 2026)**: **D2-139 the soil closed** and **D2-140 the household's niche adopted** (sim version 75: a couple
marries when its land and hands keep it a year at the last harvest, or its grain carries the shortfall; hunger a person-year back to before the
soil, births answer the land). **The visual truth audit** (`27`): 98.4 % of drawn people stood still - now `Core/Agents/Presence.cs` places and
poses every person from what they do (sixteen poses in the person shader); ways were drawn across lots (286 homes cut in a big world) - now
`Core/Agents/WayLines.cs` brings each way in along a street, round the village's edge, through passed villages by their streets; pens, gates,
far enlargement, lanes, fallow fixed; rings clear of works and works off streets (simulation, with controls). W-FOREST pass 2 begun: the
simulation's cool woods are thin (tree suitability by the annual mean) - SESSION_HANDOFF "Session 17".

**Where we are (session 17, second context, 10 Oct 2026, to 12:35 IST)**: the Unity side compiled and shot for the first time - a lived morning
shows 2,155 of 3,001 people drawn at work or on the move (shots of a loaded save must wait for its lived day: `ShotRunner`); **D2-141** (the audit's
view helpers and two simulation rules; the street clearance's population wave attributed, mechanism not found); **D2-142** trees by the growing
season's warmth (W-FOREST pass 2: boreal woods +50 %; the dry tropics still read like wet woods - the rain has no seasons; pass 2b prepared in
`tc2/out/tools/s17b/p_drytropics*.py`); **D2-143 C3.7a the household's garden** (sim version 76: the croft behind each house dug, weeded and
gathered - the gardens the view drew are now real work and real food; a lived year: 0.3 of 12 daylight hours in them, from idling). Tools:
`node tc2/scripts/unity.mjs cc` (the Unity C# without the editor), `timeuse --daylight --stages`, `place --crofts/--housing/--seasons/--sheds`.
Next: the full suite on 93ad626, real shots of the gardens, the 900-year check, the Web test, W-FOREST pass 2b - SESSION_HANDOFF "Session 17,
second context".

**Where we are (session 18, 11 Oct 2026)**: **D2-144** - the gardens kept the north's calendar in southern villages; one `SettlementSystem.DayOfYear`
for every use of a village's calendar (gardens' food x2.05, north and south alike). **D2-145** - the first real shots of the gardens: beds drawn by
their season (dug, rough, in leaf, cleared), a croft's rectangles never overlap (a 2 m end yard), one wattle fence along each croft. Arjun's resource
rule (D-1, D-2): one low-priority job at a time while RAM < 86 % and CPU < 85 %. Then the first full suite since the garden found the garden's
COARSE day out of order with lived days - **D2-146** (by the lived urges: after the wild when hungry, after one's own roof) - and its seeds a latent
hang - **D2-147** (a drowned village's households walked its drowned streets forever). The 900-year check passed; Web 6/6. Next: W-FOREST pass 2b
(`tc2/out/tools/s18/pass2b.diff`) - SESSION_HANDOFF "Session 18", `SESSION_18_REPORT.md`.

**Where we are (session 19, 13 Oct 2026)**: **D2-148 W-FOREST pass 2b** - the rain belt follows the sun in the outer tropics (the dry tropics' soil
dries to ~0.1 and wets to ~0.4), the seasonal tropics drawn from that water (grass flush and straw, leaves withering and falling tree by tree), and
every conifer drawn tinted at last (`ProcMeshes.Builder.Dim`); **D2-149 C3.7b the threshing floor** (sheaves held in the store until threshed by
flail; ricks in the stackyard); **D2-150** stumps where the woods were felled (`PlanetState.Felled`); **D2-151** the burial ground (graves from real
deaths); **D2-152** the first household's standard in its polity's colour; C3.7c the muck in progress (worktree `C:/Game Dev/tc-muck`). Sim version
78 on main (79 on the muck branch). See SESSION_HANDOFF "Session 19".

**Where we are (session 20, 13 Oct 2026)**: real shots judged and fixed - **D2-154** the village builds round its burial ground (25 of 29 grounds had
been built over) and graves are drawn as graves; stumps drawn on felled-bare ground; the threshing floor drawn in use; shot views `floorbusy` and
`act:<Activity>`. **C3.7c the muck merged** (D2-153, sim version 79, ten seeds at par). **C3.7d the herds on the stubble and the fallow** is built on
branch `wip/c37d-stubble` (sim version 80) with each household's own field cell (`FieldSpots`) and a food's cull by what the spares eat - **not merged:
pigs die out in 3 of 10 worlds** (people at par, fields x1.017, cattle x1.84). See SESSION_HANDOFF "Session 20", `SESSION_20_REPORT.md`.

**Where we are (session 21, 16 Oct 2026)**: C3.7d's pigs traced on `wip/c37d-stubble` (tip 161fa63, still NOT merged): the households keeping pigs
dwindled because **the hands' cull took the kind with the most spares** (after a farrowing, the pigs) once the stubble's cattle filled the households'
hands - now short hands are eased from the kind whose spares take most of them (`--oldhands`); ten seeds `_u4` at par in people, pigs x0.80 (n.s.).
Real shots (`27` §9) found **a carter in the sea** - a household's field cell is now one its hands walk to dry-shod (`FieldSpots.DryWay`) - and muck
heaps through the croft's fence (placed clear of it). A 20-seed comparison with head-years was running in a job queue at the handoff; then merge,
D2-155, the full suite and the Web build. Next after that: nobody walks through the water (woodlots, the well's spot, crofts), then a governance
slice (a 30-village world at year 300 has one lordship of two villages). See SESSION_HANDOFF "Session 21".

**Where we are (session 22, 16-17 Oct 2026)**: **C3.7d merged** (D2-155: twenty seeds at par; the pigs' end counts are chance - a placebo of the same
rules apart only by the first lived day, since a 300-year run lives its first day before it goes COARSE; sickness from animals rises with the herds).
**D2-156 nobody walks through the water merged**: a lived walk bends once round a lake or a bay across its way (`Core/Places/Afoot.cs`, the bend in the
person's state), every lived spot out of the water, crofts not dug into it, the woods by the way their cutters walk (the straight line's choice where
none can be walked to - the first version stranded island villages); the same save 0.178 % -> 0.013 % of person-hours in the water; left: island
woodcutters and towns on two shores (boats across water, C9.5). **C10.4 bread and dependence** (branch `wip/c104-bread`, D2-157): measured first with
`polity --load SAVE` - kin does not mark mother and daughter villages, relief does - a village that cannot give back a month of the famine grain a
stronger neighbour within a day's walk gave it bows to that neighbour; the tribute gives it back; a lord feeds its own: lords 8 -> 21 over ten worlds,
the lord's call answered, vital rates untouched. **D2-158**: the full suite's one failure (since C3.7c) - a COARSE day's muck before a camp's own roof.
See SESSION_HANDOFF "Session 22".

**Where we are (session 23, 19 Oct 2026; context handoff with a job queue running)**: **C10.4 merged** (ca3b4c8, D2-157, D2-158) and validated: Unity C#
compiles, Web 6/6, the browser's probe = headless dotnet = the editor's Mono bit for bit. **The 900-year check** (seeds 2 and 4): no snowball and no
growth - petty lordships of 1-4 villages, every bond ended because "the lord was no longer the stronger", weighed by the fit people at home on one day and
a lord by its own village (a mild epidemic ended five lordships in one year); real shots: the bond reads only up close. Built and merged together on
**`wip/s23-int`** (worktree `C:/Game Dev/tc-int`), not yet on main: **C10.5 a lordship's might** (D2-159, sim 83: people of fighting age, a lord with the
men its call brings), **D2-160 the ways' woods** (sim 84, VERIFIED: a route's forest cost read the woods of the moment it was first built, so a load or
the view changed history; now the woods at the last turn of the plates - the probe `run --load SAVE --warmroutes` gives equal hashes; rule `03` §4: a
derived cache gives the same answer whenever it is filled), **C10.6 tribute carried** (D2-161, sim 85: the tenth carried to the lord's store by the
tributary's people; the ways of a lordship converge on its lord; standards enlarged with distance). The queue's results decide the landing - SESSION_HANDOFF
"Session 23" (the queue, the drafts `tc2/out/tools/s23/d2159.md`, `d2160.md`, `d2161.md`, the next steps). Wall-clock timings on this busy machine are
not evidence.

**Where we are (session 23, second context, 19 Oct 2026, night)**: **C10.5, D2-160 and C10.6 landed** (a19c077; DECISIONS D2-159-D2-161; sim 85) after a
review found two C10.6 faults and amended them: a bond for bread needs only a day's walk as the crow flies, so **a fifth of all tribute (ten worlds) had no
way on foot to its lord** - it is now kept at home instead of set aside for ever - and one cart now goes one way at a time. Validated: full suite 419/419,
control byte-identical, ten seeds at par, Web `s23web2` 6/6 with three runtimes bit-identical; real shots: carriers with handcarts on the road to the lord
(`s23k`); the standards read up close, not from the village view (a polity there wants a map layer). C10.5's 900-year checks: bonds hold longer, no snowball.
**In evaluation: C9.5a grain across water goes by boat** (branch `wip/c95-across`, sim 86; D2-162 draft `tc2/out/tools/s23/d2162.md`): within a day's walk
grain moves at once only along a way on foot; relief, give-back and the tenth cross water in boats or not at all. Its survey on the landed code: ~604,000 FU
of relief crossed water at once in ten worlds; tributaries across water never had a plank boat (their lords did) - next, the lord's boats fetch the tenth,
then C9.5a's ten seeds. SESSION_HANDOFF "Session 23, second context" has the queue (running at the handoff) and the next steps.

**Where we are (session 23, third context, 20 Oct 2026, 00:04-02:45 IST)**: **C9.5a grain across water goes by boat landed** (c8f6f5d, D2-162, sim
86) - within a day's walk grain moves at once and traders walk only along a way on foot; across water relief, grain given back and the lord's tenth go
by boat (the tributary's own, or the lord's come to fetch it) or not at all; validated: full suite 427/427, Unity C# compiles, Web `s23web3` 6/6 with
three runtimes bit-identical, ten seeds at par. Its seeds showed the cost: boats were made only for the sea's fish, so an archipelago (seed 5) lost its
trade and relief. **C9.5b a boat to cross landed** (0a8b0b0, D2-163, sim 87): a village whose neighbour across the water is worth reaching makes a boat
for that, a dugout carries 150 kg across a short water; ten seeds at par, voyages x2.4, the archipelago made 11 dugouts and its hunger deaths fell;
main's build and Unity compile clean after the merge - its full suite, the real shots of boats crossing (`s23m5`, `s23m4`) and Web `s23web4` were in the
queue at the handoff. The 900-year check of the tribute code: no snowball. **Next: C10.7a a lordship's might by lasting strength** (arms, iron, fighting
skill; drafted, NOT built, on `wip/c107a-arms`, worktree `C:/Game Dev/tc-arms`; design `tc2/out/tools/s23/c107_design.md`), then C10.7b sworn men fed
from the tribute, C9.5c people across water. SESSION_HANDOFF "Session 23, third context" has the queue and the steps.

**Where we are (session 23, fourth context, 20 Oct 2026, 02:54-05:10 IST)**: **C9.5b's checks completed** - Web `s23web4` 6/6 (three runtimes
bit-identical), and real shots aimed by a new probe (`voyages --load SAVE --days N`: a save lived as the game loads it, every voyage with its village's
day and hour) show dugouts crossing at 45 m (`s23n1`, `s23n2`); lived and COARSE dugout crossings agree. **Three slices built, in evaluation, not on
main**: **C10.7a** might by lasting strength (years, tools and arms, fighting skill; `wip/c107a-arms`, sim 88; ten seeds at par, D2-164 draft);
**C10.7b** sworn men - the one first among a village's people keeps young men sworn to them, fed from their household's grain (a lord's from its
tribute), practising daily and going with every band; the oath is personal (a household-id first version churned) (`wip/c107b-sworn`, sim 89; D2-165
draft); **C9.5c** a COARSE day's visits, their sickness, news and the ways' wear go only where a way on foot leads, keeping a lived day's odds
(`wip/c95c-visits`; D2-166 draft). The landing branch `wip/s23b-int` (all three, sim 90) waits for the queue's evaluations, its full suite and the
900-year checks - SESSION_HANDOFF "Session 23, fourth context" has the queue and the steps.

**Where we are (session 23, fifth context, 20 Oct 2026, 05:06-~05:45 IST; Arjun stopped the session)**: nothing new on main but a tool (`unity.mjs cc
--src DIR`: a worktree's Unity sources compiled against main's references). The landing branch `wip/s23b-int` (C10.7a, C10.7b, C9.5c; sim 90) was reviewed
(no fault found) and its Unity C# compiles; C9.5c is evaluated (control identical; ten seeds at par in the vital rates; sickness x0.64, epidemics x0.81, palisades x0.95 (the mechanism); hunger deaths x1.06 (t 2.2) with all deaths a person-year at par - to explain before landing); C10.7b's SwornTests retest 5/5 after a stale assertion on the settlement page's wording was fixed (tc-sworn 71bb313, tc-int2 ca25d71).
The job queue was stopped: the landing branch's full suite, C10.7b's control and ten seeds, the 900-year checks of C10.7a and C10.7b and the profile wait
in `$Q/todo` - restart it first (SESSION_HANDOFF "Session 23, fifth context"). Started: **the realms map layer** (`wip/realms`, presentation only) - the
land of each realm's villages washed in its colour, chevrons from tributary to lord, a legend; `RealmsView` written, not wired yet (the steps in the
handoff).

**Where we are (session 24, 20-21 Oct 2026, 21:56-~01:25 IST; context handoff with the job queue running)**: nothing of C10.7a, C10.7b or C9.5c is
on main yet. The stopped queue was restarted in a new scratchpad (every binary rebuilt and checked); **the landing branch's full suite passes (441/441,
0 skipped)**; C10.7b's ten seeds are at par in the per-person rates (its mid-run people dip inside the noise against a matched placebo twin; the mechanism
is war - raids, loot, tribute up); C9.5c's hunger lean is chaos at a land's ceiling by its matched placebo, but a replication on new seeds leaned in
people (x0.96) - a third seed set in the queue decides whether C9.5c lands with the others. **The realms map layer is on main** (D2-167 draft:
each lordship's land in its colour with a band at its edge, chevrons to the lord, a legend; two rounds of real shots). **The next governance slice was
measured first**: bonds outlive their lords but hold by strength, so succession changes little; what limits lordships is that they rarely form (514
pairs where one could make the other bow, against 26 bonds) - **C10.8 the asking** is built on `wip/c108-demand` (DemandTests 4/4; its seeds queued).
SESSION_HANDOFF "Session 24" has the queue and the steps; the drafts are `tc2/out/tools/s23/d2164.md`-`d2166.md`, `tc2/out/tools/s24/d2167.md`,
`d2168.md`, `c108_design.md`.

**Where we are (session 24, second context, 21 Oct 2026, 01:45-~06:05 IST; stopped at Arjun's word)**: **C10.7a, C10.7b and C9.5c are on main**
(3b236ee, sim 90; D2-164-D2-166) - C9.5c's third seed set decided it (pooled hunger x0.994; the people lean not repeated); the integrated rules' 900
years grow realms (a three-tier overkingship of 13 villages) without a snowball; full suite 441/441, Web 6/6. **The realms map layer is finished**
(D2-167): real shots of the year-900 overkingship found sky-blue realms reading as lakes and white ones as salt flats, then magenta beside rose reading as
one realm - realms now take only ground-safe dyes, kept at least 60 delta E apart when seen together; its frame cost is unmeasurable (new
`tc2/scripts/realmsperf.mjs`: a headless save put into the browser's store, paired off/on frame counts). Measuring found **a browser OOM of great worlds**
(D2-169): each autosave made ~460 MB of short-lived buffers and the wasm heap only grows - saves now go into buffers kept and sized at each load (the same
bytes): flat through autosaves, Web 6/6. Great worlds still hold ~1 GB and draw ~8 fps at the region view (`14` §7). **C10.8 the asking**
(`wip/c108-demand`, sim 91, D2-168 draft) is in evaluation: per-person rates at par, realms of 8-16 villages by year 300, but bonds churn (ended x21) with
war (raids x6), and over 900 years one realm comes to hold most of each world (seed 2: 18 of 31 villages; seed 4: 24 of 35, depth 4) - its matched
placebo and the bonds' lives (queued) decide whether it lands as it is or, as this leans, with a counterweight first (rising together, renewal at
succession). SESSION_HANDOFF "Session 24, second context - END" has the queue (stopping; how to restart) and the steps.

**Direction from Arjun (24 Oct 2026, session 25)**: forward progress, not over-testing; the governance/civilisation slices; a strong "finished core
simulation" within two weeks (`25` §8); the planet like Earth from space; places, houses and people identifiable; a real History Book (sections,
rulers, realm changes, wars, disasters, long causality); then god powers and player-facing gameplay.

**Where we are (session 25, 24 Oct 2026, 22:14-~23:40 IST; context handoff with the job queue running)**: on main (sim 90, no simulation change) **the
planet from space** (D2-170: single-scattering air - a thin limb, a veil toward it, the terminator's penumbra; clouds from the weather as rain decks and
cumulus fields; `Look.cs`), **the History Book as a book** (D2-171: `HistoryBook` writes eight parts after the fact - Ages, Realms and Rulers with the
houses that led each seat, Wars and Feuds, Famine/Plague/Disaster, Places, Lives, Threads of Cause, A Chronology - and a Timeline page; a two-page
reader; headless `book`), **places, works and people named** (D2-172); the compile check `unity.mjs cc` repaired (it had compiled nothing since session 23).
Web `s25web1` 6/6. **C10.8 the asking decided: land** (its matched placebo: a real lean, people x0.90 while the worlds grow, explained by its war -
D2-168 draft complete); its branch suite was running at the handoff. **C10.9 the lord's death** (bonds for peace lapse at the death of the first at a
lord's seat; heirs win them back by asking; sim 92) built on `wip/c109-lapse` after measuring that C10.8's great realms are held by the call (22 of 28);
its ten seeds queued. SESSION_HANDOFF "Session 25" has the queue and the steps.

**Where we are (session 25, second context, 25 Oct 2026, 23:56-~02:45 IST; context handoff with the 1,000-year acceptance running in the queue)**: on main (sim 94) **C10.8 the asking** (D2-168), **C10.9 the lord's death** (D2-173: bonds for peace lapse at the death of the first at a lord's seat; heirs win them back by asking - raids x0.64), **C10.10 the lord's peace** (D2-174: villages of one realm raise no bands against each other; blood between them is judged by their common lord - chosen over C8.1 because measuring found the war to be feud, 30-88 % of it inside realms; war moves to the realms' edges, famines a person-year x0.82, people x1.054 against its matched placebo twin, explained), **C11.1a a people's tongue** (D2-175: each first people's own speech, founders' tongues changed a sound at a time, places, families and the born named in it - names that read by descent; the book's Peoples) and **the wars on the realms map layer** (the last ten years' raids as dark spearheads). A fault in the job tooling was found and fixed (the gate helper clobbered a loop variable: test gates had passed on "0 passed"). In the queue: main's full suite, three 1,000-year worlds and their books, the stone-walls measure (C7.2), the Web build over them, the shots of every age - SESSION_HANDOFF "Session 25, second context".

**Where we are (session 25, third context, 25 Oct 2026, from 07:34 IST)**: the 1,000-year acceptance (25 §6 "First run") found the map frozen after the
land fills. Measured (`place --margins`): not poorer land - the room lay **between villages** (15-20 % of the arable land inside the spacing, beyond every
village's reach). **C3.6g the land between villages landed** (D2-176, main b516bb1, sim 95): leavers who know no free good place and find no village to join
found a daughter village on their own village's margins; joiners now count those already on their way. The map is settled twice as densely by ~300 and feeds
16-33 % more people; after the infill it still changes slowly (25 §6 "Second run"). Next by that evidence: villages deserted in crises and refounded - measured
first (do small villages live worse?) - measured: they live better; no desertion rule. Then **C8.0 the burning-in** (D2-177, sim 96) and **F9.1-F9.3 the
god's powers as play + F11.1 the opening** (D2-178, sim 97: the power bar, Your Actions, a sickness and a blight, the title), **F9.4 the meteor and
the drought + F10.1 the world's sound** (D2-179, sim 98; full suite 473/0/0). On branches, to land after the queue's final run: F9.5 the rain (sim 99)
and C8.1a the harrying (sim 100). SESSION_HANDOFF "Session 25, third context - HANDOFF" has the queue and the steps.

**Where we are (session 25, fourth context, 25 Oct 2026, 14:10-~17:45 IST; stopped at Arjun's word)**: **C8.1b the siege measured and set aside** (no band the
walls held off could stand a sally: the world raises no host - `tc2/out/tools/s25/siege/design.md`). **On main (sim 98, no sim change; D2-182)**: the frame
pass (people beyond the limb or outside the view unposed, villages beyond the limb unbuilt: a year-1,000 world x1.4-1.8), a save streamed both ways (its heap
-243 MB), each save answered by its own completion; the Web test's reload fixed (the harness read a stale `saveOk`) - Web `s25web6` 8/8. **Built, ready to
land in order**: F9.5 the rain (sim 99), C8.1a the harrying (100), **C11.1b a people's ways carried and copied** (101: households build as they were raised
to, couples mostly as their village, tributaries' first households toward their lords). The third acceptance run: seed 2's map set by ~300, seed 4's keeps
changing. SESSION_HANDOFF "Session 25, fourth context - END" has the steps.

**Where we are (session 26, 25 Oct 2026, 21:20 - ~23:25 IST; context handoff with the queue running)**: on main (sim 101) **F9.5 the rain**, **C8.1a the
harrying** (its people lean was chaos by its matched placebo) and **C11.1b a people's ways**; one full suite 484/0/0. Built and in the job queue's checks, each
branch carrying the one before, to land in order: **C7.2 stone walls** (102: walled villages with rock at hand that lose much lay dry-stone curtains; stone does
not rot and triples the defenders' ground), **the memory pass** (103: the ordinary past 200 years kept only for what it led to; names nothing kept can show
let go - must change no history), **F9.6-F9.7 the god's ore and bounty** (104), **C10.11 the lord's household** (105: lords' first households feed landless
households of their realms, who come to the seat - measured first: surplus beside want). Measured, not built: C8.2 flight from war. Then the fourth 1,000-year
acceptance run on the final rules with real shots and the Web heap (`tc2/out/tools/s26/make_ac4.sh`). SESSION_HANDOFF "Session 26 - HANDOFF".

**Where we are (session 26, second context, 26 Oct 2026, 23:43-~03:10 IST; Control Room run #80 session 2)**: **everything built LANDED on main (sim 107)**:
C7.2 stone walls (D2-183), the memory pass (D2-184), F9.6-F9.7 the god's ore and bounty (D2-185), C10.11 the lord's household (D2-186 - its one lean was chaos
by its matched placebo), F9.8-F9.9 the god's cull, bringing, warmth and cold (D2-187: F9's powers complete), C13.4 ruins reused (D2-189, measured first:
living villages kept centuries of ruins in their hearts), F11.2 the player's settings (D2-188), the book's bonds ending with an abandoned village and its
realm maps. **The fourth 1,000-year acceptance run** was generated at 03:06 and runs in the job queue (~4 hours): judge it in its shots, books and measures
and write 25 §6 "Fourth run" with the verdict on the finished core (25 §8). SESSION_HANDOFF "Session 26, second context".

**Direction from Arjun (26 Oct 2026, session 27) - THE CURRENT PRIORITY**: about two weeks remain. **Implementation and game quality, not endless
validation**: no more replications, placebos or seed batteries unless a real unexplained regression appears; no new 1,000-year acceptance run because a
presentation feature changed. The majority of the work is now visible and player-facing, in this order (25 §8 "Next"): **A** the planet from orbit as a
beautiful small Earth (limb, oceans, land, clouds of real form and coverage from the weather, shadows, storms, readable weather - never faked climate);
**B** people and places recognisable (heads, lords, sworn men, families, households, settlements, buildings; hover and click; not RTS icons); **C** the
History Book as an evolving chronicle (eras and sections, rulers, wars, the rise and fall of realms, great places, disasters, works, the player's
interventions, maps and timeline; major events above minor ones; all from recorded history); **D** W-FOREST's remaining sameness; **E** game feel for
the god's powers (targeting feedback, visual and sound response, immediate and long-term consequences in Your Actions and the book). Each slice: implement
-> compile -> focused tests -> real shots/playtest -> Web smoke when needed -> fix -> commit -> next. **The machine: never draw on the RX 5600M** - it
restarts the PC under load (it did at 10:45 on 26 Oct during Unity shots). `tc2/scripts/gpusafe.mjs` sets Unity and Playwright's Chromium to Windows'
power-saving GPU and refuses every drawing job while the card is present (`TC2_ALLOW_DGPU=1` overrides - do not use it); `unity.mjs` prints the GPU each
run drew on (it must read "AMD Radeon(TM) Graphics (ID=0x1636)", the integrated GPU). Shots and the Web build are slower on it: fps numbers are not
comparable with earlier sessions. All runs are silent (`--mute-audio`; the listener at 0 in shots; the game pauses its sound when not in front).

**Where we are (session 27, 26 Oct 2026, 10:40 - ~12:10 IST; context handoff with the job queue running)**: the fourth acceptance is finished (25 §6: Web `s26web1` 9/9, the year-1,000 heap 870 MB, the verdict on the finished core) and 25 §8 is the ledger of what is built. On main (sim 107, no simulation rule changed): **A the planet from orbit** (D2-191: clouds read four ways from the weather - the day's air `PlanetSystems.DayAir`, an observer - with a 3-D noise's forms and shadows, the night side dark but for its homes' lights), **B people recognisable** (household liveries; the first's mantle and circlet; sworn men's caps and shields; family rings) and **C the book's content** (weighted lines, age stories and notes, IX Discoveries and Works, X Your Actions). On branches, to merge after the running Web build: the book's open-book reader (`wip/chronicle` abf6eb7) and **E the powers as play** (`wip/feel` 3e8ae68: flames, the aim marker, the cast's burst and shake, Your hand, the HUD skin). Prospecting (C5.3c) is in its seeds. SESSION_HANDOFF "Session 27" has the exact state, the queue and the next steps.

**Where we are (session 27, context 2, 26 Oct 2026, 12:12 - 14:50 IST)**: all of A-E on main, judged in real shots and Web builds `s27web2`/`s27web3` (9/9)
- the open-book reader, the timeline's eras and the ages' years (D2-193); house and family rings on hover, flags for a realm's villages, `Biography.Standing`, the
inspector as a card in words, "N sick"/"famine" on the map (D2-192); the meteor's fall and scar, lightning bolts, flames, tooltips, "Your hand" (D2-194); snow on
the boughs, bare crowns over snow, the rain forest's tones; lumpy soft cumulus. **The frame on the integrated GPU** (D2-195): a great world's region view 12.4 ->
~30 fps and village view 8.4 -> ~16, from far villages plain and shadowless and graded light shadows; the terrain is the next cost. **Prospecting's ten seeds are
inside the noise**: land it from `wip/s27-land` once the queued sim-107 shots and Web build `s27web4` are done (D2-190 then). The integration branch is
`wip/s27-int` (worktree `C:/Game Dev/tc-s27int`). SESSION_HANDOFF "Session 27, context 2" has the queue and the steps.

**Where we are (session 27, context 3, 26 Oct 2026, 14:52 - 16:45 IST)**: **prospecting landed** (C5.3c, D2-190, main sim 108; seed 2's saves remade as
`tc2/out/civ/s2_ac5`, years 300/600/1,000 - sim-107 saves no longer load). Judged in real shots (`s27c`, `s27d*`, `s27e*`, 0 shader errors) and Web `s27web5`, and
fixed: rivers far off, the meteor (one fall, grey dust, its fall heard), the regions' cell staircase (a warp), night clouds by the moon, the glint, no road over a
lake, pools in waterlogged woods (never under snow), the palisade (stakes; its shore stretches), the night lights at 512; the book's repeated ages named by
their realm, Your Actions folding a cast's dead, the person and place pages as leaves of the book and in words. **The frame**: Terrain.shader branched (gain
only x1.02/x1.06); every terrain measure had been taken paused while the ground's colouring ran on for ever - stopped on a still world; `s27perf4` measures it.
D2-196 written (frame numbers pending). Rebuild main's headless before a webtest after a sim version change. SESSION_HANDOFF "Session 27, context 3".

**Where we are (session 27, context 4, 26 Oct 2026, from 16:40 IST)**: the frame is now known by function - `realmsperf --profile` on a `--symbols` Web
build names the wasm's functions (`tc2/scripts/cpuprof.mjs` re-reads a saved profile): the main thread is busy the whole frame (the CPU's, not the GPU's), and
its costs were the people set on the fine relief every frame, the hover's scan of the whole world, the roads' lookup and ribbons, instanced arrays sent at
full length, names laid out on every event - all fixed on main (d4057ed); their measure `s27prof2` is held for the next session (Arjun ended session 27 at ~17:50). The night from above, the regions' warp, the palisade
faced in the village's frame (`place --ring`), the book holding the screen and the pages' words judged in shots and Web builds (D2-197). SESSION_HANDOFF
"Session 27, context 4" has the exact state and the queue.

**Where we are (session 28, 27 Oct 2026)**: housekeeping only, no development (Arjun) - D2-198: about 33 GB of ignored output deleted (runaway Unity
logs, saves below sim 107, pre-25-Oct images, TC1's media) and 62 merged worktrees removed; nothing tracked changed. The work resumes exactly where
session 27 stopped (SESSION_HANDOFF "Session 28" then "Session 27, context 4"). A Unity log past ~100 MB is a bug signal: let the job finish, read
its first repeated message and fix the cause, then delete the log.

**Direction from Arjun (28 Oct 2026, session 29) - THE CURRENT PRIORITY**: "the jam must clearly read as a real simulation to the player" - every feature answers what is happening, who, why, from what resources/relationships/history, what changed, how the player can inspect and follow the chain, and how the picture shows it; the player's own casts traceable from the power to its long-term history; never fabricated explanations (add the smallest real state if the truth is not kept). In order: the long skip as an orbital timelapse; the person page as a person; place/realm/building readability with progressive disclosure (world -> realm -> settlement -> household -> person -> life/causes); "Why?" chains from the ledger; the History Book as the world's memory (cross-links, Your Actions' consequences); the powers as play; the visual world where it is evidence of the simulation. Presentation is validated by compile -> focused tests -> real Web play/shots -> fix -> commit; no seed batteries or 1,000-year runs for presentation.

**Where we are (session 29, 28 Oct 2026, 22:23 - ~23:45 IST; context handoff with the queue running)**: on main (sim 108 unchanged) D2-199 the timelapse, D2-200 the person page, D2-201 Why?, D2-202 roads read / a village from afar / the world now / the building leaf / the book's order, Peoples, wars' leaders and lives linked / the powers' word and threads. Seen in Web builds: the timelapse, the person and place pages, Your Actions' thread; D2-202's pages are next to see. Then the visual world as evidence (the brief's section 7; shots `s29v1` queued). SESSION_HANDOFF "Session 29" has the exact state and the steps.

**Where we are (session 29, second context, 29 Oct 2026, 00:00 - ~01:10 IST; context handoff with the queue running)**: on the branch `wip/s27-int` (main fast-forwarded by each job; sim 108 unchanged) D2-203 - the visual world judged as evidence in real shots and Web builds: a burned crop and burned homes drawn as fire left them (from state and the ledger), a ruin's tag and page saying how it fell, the rivers' far glint, the fields ending furlong by furlong, the timelapse's seasonal edge found and softened; works, roads and the History panel answering why; the aim's words, no names under panels; the book's and pages' words. SESSION_HANDOFF "Session 29, context 2" has the exact state and the steps.

**Rules**: commit prefix `[tc2]`; never commit Library/Temp/Logs/builds/secrets; never damage the legacy
project; record decisions (D2-NNN) and measurements; update PROGRESS, SESSION_HANDOFF and this prompt at
milestones. After a context compaction: stop, re-read the files above, reconstruct state, continue.

**Where we are (session 30, 29 Oct 2026, from ~02:00 IST; Control Room run #86 session 3)**: on the branch `wip/s27-int` (main fast-forwarded by each job; sim 108 unchanged) D2-204 - two villages' fields at different stages meet furlong by furlong (both villages in a vertex, `TerrainLod.Slots`; the shader chooses per furlong); every patch coloured in a cycle shows the cycle's world (the skip's gold blocks gone); the sea drawn on its true sphere a quarter metre down (the shore's faces gone, an even swash); the cast's word beside Your hand; no map name under the lapse's cards; the years' leaf beside a page; the ticker on one line; a fire's ground in hectares. Tools: camera views `fieldmeet`, `shore`; `Look.SeaTrue`. SESSION_HANDOFF "Session 30" has the exact state and the steps.
