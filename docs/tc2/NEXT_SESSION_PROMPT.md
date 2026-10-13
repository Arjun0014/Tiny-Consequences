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

**Rules**: commit prefix `[tc2]`; never commit Library/Temp/Logs/builds/secrets; never damage the legacy
project; record decisions (D2-NNN) and measurements; update PROGRESS, SESSION_HANDOFF and this prompt at
milestones. After a context compaction: stop, re-read the files above, reconstruct state, continue.
