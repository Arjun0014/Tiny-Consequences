# Session handoff - session 25, part 4 (5 Oct 2026, ~02:15-03:20 IST): Arjun's fourth playtest, the store media, the itch zip; D-221-D-226

## FINAL STATE (part 4)
- main, all committed (see `git log -1`). The deliverables (all in `dist/`, gitignored - rebuild with the commands below):
  - itch: `dist/tiny-consequences-web.zip` (`npm run package:itch`; index.html at the root, paths with forward slashes - D-226).
  - Wavedash: `dist/tiny-consequences-wavedash.zip` and the folder `dist/wavedash/` (`npm run build:wavedash`; `wavedash.toml` at the
    repo root has `upload_dir = "./dist/wavedash"` and a placeholder `game_id` Arjun must fill in). Arjun reports it runs on Wavedash.
  - Store media: `dist/store/` - `trailer_10s.mp4` (zoom into a village, the village on fire, the meteor, the title over the end),
    `1_planet_zoom_to_village.gif`, `2_village_fire.gif`, `3_meteor_strike.gif` (360 px, 8 fps, 2.1-2.8 MB), `cover_1920x1080.png`,
    `cover_630x500.png`. Made by `out/s25/gifs3.mjs` (zoom, fire) and `out/s25/gifs2.mjs` (meteor): one Playwright-recorded session,
    the camera driven every frame inside the game (`camera.update` wrapped, `__drive(t)` by `clock.now_ms`), cut with ffmpeg.
- What changed (DECISIONS):
  - D-221 rain puts fire out: map fires stop growing in rain (`fire_rain_grow` 0.3, none in a downpour) and are damped harder
    (`fire_rain1` 0.6, `fire_rain2` 2.0 an hour); a god's rain quenches the planet's burning cells (`rain_quench_v3` 0.9, v3 only).
    Probe `out/s25/probe_rain.lua`: ten trees alight - all out within the hour under the god's rain, two still burning after 6 h dry.
  - D-222 "two planes outward from the world" (Arjun's screenshots: opaque, dark-water colour, behind the planet, tip on a village map,
    the Little Leelee flood receding): NOT reproduced here (18 zoomed-out views, the tutorial's path, skips, a resize). The rivers, the
    flood water and the paths (the only meshes drawn there) now put any vertex off the planet at the centre (`onground`) and never
    shrink (unused vertices empty triangles). Unverified against his world.
  - D-223 phones: touch-action none, one finger = the engine's mouse, a pinch = wheel notches (its moves kept from the engine), a
    "Turn your phone sideways" screen when upright on a touch screen, landscape locked in fullscreen where allowed.
  - D-224 Wavedash: one bundle; `window.tcWD` in `game/html/engine_template.html` finds `window.Wavedash` at run time; the game calls
    `tcWD.init()` once ready (main.script), shows "Playing as NAME" top right of the title (`menu.player`), keeps `menu.prefs` in Remote
    Storage (`tiny-consequences/prefs.json`, on every save; read back at the start). No world save exists anywhere (post-jam).
  - D-225 an IndexedDB probe before `EngineLoader.load` (storage blocked in a cross-site frame would stall Defold's save-folder sync);
    harmless, not the itch cause.
  - D-226 THE ITCH CAUSE: PowerShell's Compress-Archive (Windows PowerShell 5.1) stored `archive\game0.arcd` with backslashes; itch's
    unpacker kept them as root file names, the engine's archive was 404 and only the Defold splash showed. `scripts/package.mjs` now
    zips with Windows' bsdtar (`tar.exe -a -c -f`). Python's zipfile hides this on Windows (it turns backslashes into slashes when it
    reads): check the raw bytes (`out/s25/zipnames.py`) or `tar -tf`.
- Checks (part 4): sim sets `--only fire` 12/12, `--only power` 14/14, `--only world` 8/8; lint clean (316 files); release build OK;
  browser = LuaJIT (release) `--premise fire` identical; an emulated phone (`out/s25/mobile_check.mjs`: rotate screen upright only, a
  drag turned the world, a pinch zoomed 1.25 -> 1.75); the Wavedash glue against a stand-in SDK (`out/s25/wd_check.mjs`: init only
  after ready, the name with a quote and a backslash, prefs down and up as valid JSON); the zip unzipped and loaded in a cross-origin
  iframe (`out/s25/itch_iframe2.mjs`). NOT run since part 3: the full quick suite (211) and the web suite (11).

## Next (part 4)
1. Confirm the itch page runs the bsdtar zip (Arjun uploads it); then Arjun's feedback on the submitted builds.
2. The planes: if Arjun sees them again, catch the world (a save at that moment) and find the mesh; the guard is a guess.
3. Run the full quick suite and `npm run test:web` (not run after D-221-D-226).
4. Wavedash: the real SDK's identity call (`getUser`/`getUsername` are guesses) and cloud calls are untested on Wavedash itself;
   a world save in the cloud is post-jam.
5. A real phone (only emulated); the skip GIF (captures need `?menu=1` - skipcam is off under `?test=1`).

## Pitfalls (part 4)
- Never zip with Compress-Archive; never trust Python's zipfile names on Windows for separators.
- Under video capture the game's frame dt runs slower than real time: drive camera moves by `clock.now_ms`, not by summed dt.
- `game/view/skipcam.lua` runs only without `?test=1` (or with `?menu=1`).

# Session handoff — session 25, part 3 (5 Oct 2026, ~01:20-02:20 IST): Arjun's third playtest; D-220

- The planet's herds (sim/systems/fauna.lua, drawn as puppets) now answer fire and the god's bolt at once: they run to the safest
  neighbouring cell (the view runs them in 0.4 h) and a share dies (fire 0.25 x intensity, bolt 0.08), carrying the cause; the hook is
  `fire.on_burn` (set by fauna.lua; v3 only). Seen in the browser: a bolt on a herd of 59 - 50 head on another cell, gone in seconds.
- Play on the title always opens the tutorial ("Free play (no tutorial)" skips it); the tutorial gains "Why?" and "Your tools".
- Next: Arjun's next playtest; the post-jam list below.

# Session handoff — session 25, part 2 (5 Oct 2026, 00:20-01:30 IST): Arjun's second playtest of the submitted build; D-214-D-219

## FINAL STATE (part 2)
- Arjun's list (`docs/v3/86_S25_PLAYTEST.md` section 6): the guide's layout, the tutorial's step 5, the skip menu's overlaps, the game
  opening close in, no sense of a skip, "Your actions" not a chain, the tutorial's chain too small, oil with no substance, the meteor
  with no crater seen and a poor fall, blight with no effect, time controls not taught, a file path in the credits.
- Done: the game opens on the whole planet; a skip rises, turns the planet through its days and comes back (`game/view/skipcam.lua`,
  D-214); the tutorial rebuilt around a meteor at the woods' edge beside the village, with the speeds and two skips taught and every
  number read from the world (D-215) - played in the real game (`out/s25/ui_check3.mjs tut`): 16-21 consequences at once, 19-25 a day
  on ("4 people died, 4 households lost their roofs, hunger in a household..."), 26-30 a season on; the meteor's long fall with a trail
  (scaled by the eye's distance), three shock rings, and its crater-and-scorch mark on the planet at every height (D-216); oil as black
  blobs drifting on the current and a spill bursting out (D-217); blight 0.35 at once, rotting in days (D-218); "Your actions" with the
  chain's size, icons by kind, new links glowing; the guide re-laid by measure with a proper footer and arrow keys; credits (D-219).
- Checks (part 2): quick suite 211/211; web 11/11; browser = LuaJIT (release) `--premise powers2` identical; the zip rebuilt and smoke-tested.

# Session handoff — session 25 (4-5 Oct 2026, THE SUBMISSION SESSION): Arjun's playtest made the game's own authority; the meteor, fire, oil, Go there, cooldowns, menus, the tutorial, the guide; D-205-D-213

## FINAL STATE (end of session 25)
- main, all committed (see `git log -1`). The submission build: `npm run package:itch` (a release build, zipped) ->
  `dist/tiny-consequences-web.zip` (8.3 MB, index.html at its root; built from 7ed202e and smoke-tested unzipped from a clean static
  server: title, Play, the tutorial, the guide, fullscreen, no console errors - `out/s25/final_smoke.mjs`).
- Checks: quick suite 211/211; lint clean (315 files); web 11/11; browser = LuaJIT (release) identical untouched 72 h and with
  `--premise powers2|fire|conflict` 96 h; 8 untouched years: births 43, deaths illness 15 / old age 4 / drowned 2 / hunger 0;
  release benchmark lower envelope 41.2 ms of sim a game hour, a 12-day season 11.9 s (S24 12.1 s).
- **What Arjun's playtest found** (reproduced in the real game first): the meteor read as fire + quake (a 3-tile crater, people
  answering the quake and the fires, not the strike; animals near it; an island = some burning trees); animals stood in flames
  (an hour to die); "a forest of trees with permanent flames" after a season (the planet's fire on a cell a village map covers in
  part burnt a week and the view drew it over the map's standing trees); oil invisible (drawn as deep water's colour); Go there to
  the village square; powers spammable; the skip dropdown under the news; fullscreen dead (`Module.toggleFullscreen`); no menu, no
  tutorial, no guide.
- **What changed** (DECISIONS D-205-D-213): game-time cooldowns (meteor 7 days) shown on the bar; the meteor at a god's size with
  the strike itself the danger (people run from it, beasts bolt 3.5x as far, the planet's cover thrown flat and scarred), a big crater
  with a double rim, a settling dust column; beasts die in minutes in flames; the planet's fire burns out in 2-3 days beside a map and
  is never drawn over the map's trees; the fire story's length by when it went out; Go there at the event (cast point, crater, the
  flood's water, the place of a death, the person on the move); a black glossy slick with a rainbow film (oil 54); title / pause /
  settings / credits; "How the World Works" (8 illustrated pages); the opening tutorial (one bolt, its real chain); aiming at the drawn
  tree and the drawn ground; fish moving together along the sea's current; a view crash fixed (`trade` global in `update_travellers`,
  any boat on a sea journey to a site).
- **Evidence in the real game** (`out/s25/`, not committed): `play_cat.mjs village` - Tekheim's square: at once 11 of 31 dead, 16 ->
  3 buildings, 291 fires, 14 running from the blast; 6 h on 3 fires; a season on 0 fires, the crater, 8 camps, houses going up again,
  41 consequences; `forest` (Hottoodingle Hop's woods): 209 trees thrown flat, wild animals within 0.5 of it 7 -> 2 -> 0, a season on no
  fire, 240 snags, "the fire is out after 29 hours"; `island` (a one-cell island): forest 0.86 -> 0.09, grass 0.50 -> 0.15, ash; `oil`
  (off Zurug): 11 units over 11 cells at once, fish 7.98 -> 6.52 (2 d) -> 0.87 (8 d), the shore fouled, 10 gulls oiled, Go there to the
  fouled shore; `tut_full.mjs`: the tutorial from Play to "Your one lightning bolt now has N consequences" (the bolt struck the marked
  tree after D-212); `ui_check2.mjs`: skip tutorial, the dropdown over the news, Esc, the pause menu, a second meteor refused "ready in 7
  d"; `fs_check.mjs`: fullscreen entered and left, the pseudo-fullscreen when refused; `probe_goto.lua`: the flood's Go there on 0.55 m
  of water; `probe_fire.lua`: map fires out by 16 h, the planet's by 48 h (was 192).

## Next (after the jam)
1. Arjun's next playtest of this build - the tutorial with a human hand (the strike step, the news step), the guide's wording.
2. R6: captives as persistent people; the palisade's gates at night; traded grain carrying blight's taint (left out by the freeze).
3. The herd neighbour scan's cost in grown worlds (exact changes only).
4. The tutorial's dust/ring visuals and the dust puffs' shape (they read as soft hexagons close in).

## Pitfalls (this session)
- A bash heredoc whose text holds an apostrophe fails here: write files with the editor tool (or a Python patch file) instead.
- The Lua lint does not catch an unfinished string in `game/`; bob does (`npm run build` after GUI edits).
- `sim/tutorial.lua` is the LEGACY v2 tutorial helper (tests use it); the v3 tutorial's command is `sim/tutorial3.lua`.
- A headless click or key must be held a few frames to reach Defold (press/hold/release with waits).
- The title screen pauses the world; harnesses pass `?test=1` (no title) - `?menu=1` to see it with the bridge on.

# Session handoff — session 24 (4 Oct 2026, R7 ACCEPTANCE AUDIT): every god power played in the real game and made to work; herds, fish, scars, what the world tells; D-188-D-204

## FINAL STATE (end of session 24)
- main, all committed (last: see `git log -1`). Worktrees made for long runs are removed.
- **The acceptance audit**: `docs/v3/85_R7_ACCEPTANCE.md` - what was broken at the start (reproduced in the browser), why, what
  changed physically and visually, every power in its contexts (a table, with the browser's readings), the play matrix A-I (contact
  sheets in `docs/v3/accept/`). The tool: `node scripts/play_accept.mjs --case NAME[,NAME] --tag T [--no-build] [--sett K] [--lh H]`
  (the real game in headless Chromium on the GPU, ANGLE D3D11; shots and `report.json` in `out/accept/<tag>/`).
- Tests: quick sim suite **211/211** on the final sim code (three emergent tests widened, D-204); lint clean (312 files); debug and
  release builds OK; web tests **11/11** (incl. W10: the v3 world in the browser equals LuaJIT).
- Browser = LuaJIT (release): **identical** - untouched 72 h, and `--premise powers2|fire|flood5|conflict` 96 h.
- 8 untouched years (`luajit tools/v3_chronicle.lua --days 384`, final sim): births 44; deaths illness 16, old age 4, **hunger 0**;
  no door cut off; 167 people at the end (the checkpoint code: births 45, illness 12, old age 4, drowned 2 - a boat lost in a squall).
- Cost (LuaJIT instructions a game hour, `luajit -joff tools/v3_profile.lua`): day 0 **4.29 M**, day 192 **4.40 M**, year 8 **5.02 M**
  (S23 4.06 / 4.02 / 4.79); stock.lua 36-38% in grown worlds - the herd neighbour scan.
- Release benchmark (`node scripts/prof_web.mjs --release --hours 288 --runs 3`): lower envelope **42.1 ms of sim a game hour, a
  12-day season 12.1 s** (S23 11.8 s; 43.4 ms / 12.5 s before the last exact change: `v - v % 1` for the herd's hot `floor` calls).

## What session 24 did (D-188-D-204)
Arjun's playtest: the god powers weak or broken in real play; clouds hiding the village; herds as blobs. The brief: the playable
game is the authority; no new systems; every power must hit where aimed, change physical state, respond visibly at once, affect
what is exposed, make real consequences and tell them in News / Why / Your actions.
- **Clouds** (D-193): the layer lifts as the eye comes down - the village clear from zoom 1.1 (the cloud hid >= 90% to 1.35).
- **Meteor** (D-188): crater, blast by overpressure, thermal flash, shaking - by distance on people, beasts, buildings, trees,
  fields; the planet's part by the same physics; its toll told. On Tekheim's square: 4-9 killed, every house down, 62-95 fires,
  households camping, hunger two days on traced to it (before: nobody died).
- **The god's bolt** (D-189): aimed; a superbolt's current through the ground by stride; fire by dryness; scorch; every beast
  near bolts. Into a flock: 6 dead (before: 1); a cow; a deer; 3 wolves; a dog; a person in the open (killed); a dry tree; a house.
- **One fire at two scales** (D-190, D-200): a map's cells burn by the map's fire; the planet's burns in patches by intensity.
- **Scars** (D-191, D-203): ruins, snags, thrown trunks, crater (and a planet crater ring off the maps), scorch, dead livestock,
  flood mud, blight drawn on the rows (D-201).
- **Herds** (D-192, D-197): each follows where the one ahead is going; grazing each its own patch; making room from the whole
  press; a fold passed only at its gate (the flock leaves as a stream: elongation 3.1, alignment 0.80 at the gate); spacing at
  grazing 0.031-0.036 (sep 0.034; before 0.010-0.019).
- **Fish** (D-195): drawn from the sea's own fish (`F.fish`), the same field the fishers' nets draw on.
- **Honest maps** (D-196): a map shows one in five of its herds' wild animals - a cull now shows (28 -> 7).
- **Told right** (D-198, D-201): fires carry their planet cause; a village's sea is two rings out; a stirred mountain's pressure;
  hunger's cause; rain putting a fire out; a flood the rain spread; fewer fish; a later bolt is its own story.
- **Bugs the audit found** (D-194, D-199, D-197): a village with no roof starved beside its granary (camps); deaths skipped the
  next person; a burnt fold was never rebuilt (a lot's stumps never dug); the bolt on a pen's hurdles; a jolt before the queue;
  the gate jam (one beast 3,900 decisions a day); lambs shut out of the fold at night; the herd's random keys repeating.
- **Cost** (D-202): exact - three draws from one hash, the turn's trig by series; the gate jam fixed: day 0 4.29 M.

## Next (in order)
1. Play it as Arjun would - each power once, close in and from orbit; a flock going out at dawn; the sea off a fishing village; a
   meteor on a village and its aftermath over days - and fix what reads wrong (the playable game is the authority).
2. The herd neighbour scan's cost in grown worlds (exact changes only): day 192 4.40 M instructions a game hour (stock 36%), year 8
   5.02 M (stock 38%); `stock.scan` is O(herd) per decision and herds grow - an index that keeps the same answers.
3. R6: captives only as persistent people; the palisade's gates kept at night; traded grain carrying blight's taint.
4. Smaller: a steam plume seen from straight above reads as a grey cloud; cold or warm air at mild temperatures shows nothing (told
   only); the planet's crater is small as its physics says.

## Pitfalls
- `goto` is LuaJIT-only (the browser runs Lua 5.1); a view module must not require back into a module that requires it (bob);
  the grid's `spacing` is on the unit sphere (world units = spacing x R); JS arrays are 0-based (a Lua `{x, y}` reaches JS as
  `[x, y]` - return `{ x =, y = }`); running `node -e "import('./scripts/play_accept.mjs')"` RUNS the harness (it rebuilt the app
  mid-batch) - use `node --check`; Python writes to repo files sometimes fail with Errno 22 while a test run reads them (retry);
  `v3_goto{lh}` is the local hour of `sett`; a test that asserts one emergent outcome in a window breaks when the world's course
  changes - assert the mechanism and accept the outcomes its rules allow (D-204).

## CHECKPOINT (mid-session 24, kept for its record of Arjun's brief and the first half's state)
**Arjun's brief (session 24 prompt, kept in this section):** the playable game is the authority; no new systems (no captives,
no more powers, no social systems, nothing decorative) until the R7 acceptance audit is convincing. Every god power must (1) hit
where aimed, (2) change physical state clearly, (3) show an immediate visual response, (4) affect what is exposed, (5) make real
downstream consequences, (6) show them in News / Why / Your actions. Priorities in order: clouds, meteor, lightning, planet/local
consistency, fire, every power audited in 2-3 contexts with before/after browser shots, herd movement (watch the live herd -
shared intent, no puppetry, no cosmetic jitter), fish (render the real fish field honestly as schools; fishers answer the same
state), visible aftermath, then performance (exact optimisations only). Final report must begin: which powers were broken, why,
what changed physically, what changed visually, exact browser evidence; plus a play matrix A-I (meteor on a village, bolt into a
herd, fire beside a herd, rain on a fire, rain on saturated ground, oil by a fishing village, warm sea, blight, volcano/quake) each
with screenshots before / at once / after, sim readings, News, Why, Your actions. End checks: full suite, web tests, lint/build,
browser = LuaJIT for the premises, 8-year untouched run, day-0/day-192/year-8 profiles, release benchmark, the matrix, herd
diagnostic, fish audit, docs (PROGRESS, DECISIONS, HANDOFF, NEXT_SESSION_PROMPT, roadmap, R7 docs), commit, clean tree. No
subagents. On compaction: re-read CLAUDE.md, this section, newest PROGRESS/DECISIONS (D-188+), 65/70/75/77/80 docs, roadmap,
`git log`, then continue.

**The tool of this session**: `node scripts/play_accept.mjs --case NAME[,NAME] --tag T [--no-build] [--sett K] ...` - the real
game in headless Chromium on the GPU (ANGLE D3D11, `scripts/lib/play.mjs`), bridge `lua{code}` reads the sim
(`game/platform/accept.lua`: village / herd / herd_fire / fire_scales / herd_shape / env), shots and report in
`out/accept/<tag>/`. Cases: clouds, meteor_village, lightning_herd, fire_herd, rain_fire (--no-rain twin), fire_scales,
herd_watch, herd_move, and the power specs PSPECS (lightning_tree_dry, lightning_house, lightning_cattle, rain_drought,
rain_soaked, dry_spell, cold_crops, warm_snow, stir_mountain, strain_coast, strain_inland, cull_wolves, cull_deer, bring_wolves,
bring_fish, sickness_flock, blight_field, sea_warm, oil_shore, meteor_land, meteor_forest, meteor_ocean). Headless LuaJIT probes:
`out/s24/probe_meteor.lua`, `probe_bolt.lua`, `probe_herdout.lua`.

**Done (D-188-D-194; details in DECISIONS)**: clouds lift above the village view as the camera descends (D-193); the meteor is
crater + blast + flash by distance (`sim/agents/impact.lua`) with its toll told (D-188); the god's bolt aimed, a superbolt's ground
current by stride, fire by dryness, scorch, and every beast near bolting (`jolt`) (D-189); a planet cell a village's map owns burns
by the map's fire, flames and tint only where things burn (D-190); ruins, charred snags, thrown trunks, crater, scorch, dead
livestock drawn (D-191); herds follow the one ahead and graze their own patches (D-192); roofless households camp, lodgers
re-lodged, the member-list skip on deaths fixed (D-194/D-188).
**Seen in the browser so far**: rain on a drought soaked the ground (and flash-flooded a little), rain on a soaked floodplain
flooded Little Leelee (3,040 tiles - but the flood was already there: its deed is nature's, the chain shows only the river), the
dry spell dried the soil and warmed the air, lightning on a dry tree burnt it alone, on a house burnt it down (household lodged).
**Next (in order)**: run the rest of PSPECS (`--tag p2`): cold_crops, warm_snow, stir_mountain, strain_coast, strain_inland,
cull_wolves, cull_deer, bring_wolves, bring_fish, sickness_flock, blight_field, sea_warm, oil_shore, meteor_land, meteor_forest,
meteor_ocean - look at every shot, fix what is weak (blight visible on the field? oil slick on the map's sea? fish? planet crater
on empty land?); then fish (render schools from F.fish; fishers read the same); herds again (the fold's fence only open at its
gate so a flock leaves as a stream); the A-I matrix; performance; the end checks.
**Pitfalls**: `goto` is LuaJIT-only (Lua 5.1 in the browser); Python writes to repo files sometimes fail with Errno 22 while a
test run reads them - retry; a heredoc with apostrophes breaks - write patch scripts to files; `v3_goto{lh}` is the local hour of
`sett` (default 1); the harness's flush_view jumps real-time effects (shoot(..., false) for the fireball).

# Session handoff — session 23 (4 Oct 2026, extended autonomous run): the hydrology fix verified; clouds v5; the village mix; CHAIN REACTION ("Your actions"); R7 v2 powers; the second 8-year run's famine followed to four mechanisms; aim previews; the palisade; D-179-D-187

## FINAL STATE (end of session 23)
- main, all committed (last: 248db37, the palisade, and this docs commit). Nothing half-done in the tree. Worktrees made this session for snapshots and long runs (`../tc-snap`,
  `../tc-run`, `../tc-before`) are removed at the end.
- Tests: quick sim suite **211/211** on the final code (D-187); web **11/11** (on the settled palisade code); lint clean (309
  files, new rule `pow`); debug and release builds OK.
- Browser = LuaJIT (release): on the final code untouched 72 h and `--premise conflict|palisade|powers2` 96 h identical; earlier
  in the session flood5, fire, powers, pasture_fire too. New premises `powers2` (oil, warm sea, sickness, blight, meteor) and
  `palisade` (raids remembered).
- 8 untouched years on the final code (`luajit tools/v3_chronicle.lua --days 384`): births 45; deaths illness 14, old age 4,
  **hunger 0**; no door cut off; Krikeivorn, raided, raised a 13-section palisade from day 157 (run 2 at the checkpoint: hunger 11;
  run 5 on the D-184 code: births 46, illness 13, old age 4, hunger 0).
- Cost: LuaJIT 4.06 M instructions a game hour at day 0 (S22 3.91 M), 4.02 M at day 192 (S22 4.41 M), 4.79 M at year 8 (172 people; S22 5.13 M; livestock 35.7%, the herd scan the hot spot)
- Audio playtest (`scripts/play_audio.mjs`): the herd bed 0.14 close in / 0.10 at the square / 0.03 mid zoom, under the village
  bed (0.54 / 0.17); one-shots work and village; beast calls rare (none in these 45 s windows - by design a few a minute, near).
- Release benchmark (`node scripts/prof_web.mjs --release --hours 288 --runs 3`): lower envelope 40.9 ms of sim a game hour, a 12-day season 11.8 s (S20: 10.8 s).

## What session 23 built (D-179-D-187)
1. **Hydrology** (D-179): the rain the ground cannot take in runs off without leaving the soil twice; the drifting tests followed
   (wolves eating across a river; hunting counted by spears thrown).
2. **Clouds v5** (D-180): one cloud layer from the villages' own weather that the camera flies through; shadows from the same field.
3. **The village mix** (D-181): `audio.lua` `M.MIX`; the herds are texture.
4. **CHAIN REACTION** (D-182, `docs/v3/77_CHAIN_REACTION.md`): `sim/chain.lua` (an observer), "Your actions", "chain N" on news cards.
5. **R7 v2 powers** (D-183): sickness in a herd, blight, warm/cool sea, oil, meteor.
6. **The second 8-year run's famine** (D-184): a pit that sealed a house (every site keeps a walkable yard; `map.cut_doors`; the
   chronicle prints DOORS CUT OFF), play loops that carried children through it (`nav.los_last`), toddlers who never got their
   strength back (they recover when fed), a crier who gave birth on the road (`journey_ok`: no journeys near term or nursing; babies
   fed on trips).
7. **Oil on a coast** (D-185): the shore fouled, the gulls oiled; **a household with no roof** (D-186): no plan needs the lost house;
   **R6f the palisade** (D-187): laid out from remembered harm, sections as sites, never shutting a door or a field off.
8. **Polish** (D-184): aim previews (`powers3.preview`), the flood's edge by marching squares, the news so far, harvest and taken-in
   stories, the chain's taken-in and fed-by-neighbours nodes, the Sound pill, no `^` in `sim/`.

## Next
1. R6: captives only as persistent people (a household, a destination, a way home); the palisade's gates kept at night, a
   palisade mended, or burnt in a raid (D-187 built the ring itself).
2. Traded grain carrying blight's taint; oil fouling shore food and the gulls on the maps; relief to a village joined to its harm in
   the chain.
3. The year-8 cost: the herd neighbour scan (`stock.lua` `scan`) is O(herd) per beast decision (the herd's middle needs every
   member at the beast's own time) - only an exact change is acceptable (no loss of agency).
4. Play: the aim previews and "Your actions" with Arjun's eyes; the flood edge and the clouds at different times of day.

## Pitfalls found this session
- Every planner must keep a walkable yard round what it lays: a ring of free tiles is what guarantees no way is lost. Check new
  placements with `map.cut_doors` (tests: `test_v3_sites`).
- A work step's straight loop (`x1, y1`) is not a path: it must stop at the first closed tile (`nav.los_last`).
- Carried infants never run `act.body`: anything the body does for grown-ups (recovery) must be done in `kin.infant_tick` too.
- `think` returns a trip's own plan before weighing care: whatever a trip does not plan for (nursing) must happen in the tick or be
  ruled out before setting out (`journey_ok`).
- `^` is pow() in the browser's Lua 5.1: products in `sim/` (the linter now says so).
- A test that asserts where a fire went (or who happened to be near) drifts with any change; assert the mechanism (the senses,
  within reach) and add a direct premise for it.
- Headless rAF timing under SwiftShader is not a measure of anything; measure the GPU with ANGLE D3D11 (`out/s23/probe_gpu.mjs`)
  and the sim with render_off / instruction counts.
- A deed's `answers` is a causal claim: link a death to a hurt only when the hurt was grave or is the cause of death.
- `flush()` of a view module must use the world's clock (`speed.v3_t`), not the last frame's (`state.hour_f`).
- A cast aimed by the screen centre may land beside the village: shots aim at the village's own cell (`v3_look` returns `cell`).
- `tools/v3_chronicle.lua --save` writes a year0 save (only at a year boundary, days a multiple of 48): following from it shows the
  same mechanisms but is not an exact continuation of the long run.

## (earlier in session 23)

## CHECKPOINT (~880K context, session 23) - read this first after a compaction
**If you are resuming after a compaction: re-read this section, CLAUDE.md, `docs/v3/77_CHAIN_REACTION.md`, `70_R7_GOD_POWERS.md`
§9, `75_PLAYER_EXPERIENCE.md` §10, `80_WORLD_AUDIO.md` §9, `docs/world/16_CAUSALITY_VISUALS.md` §5b, D-179-D-183 and
`git log --oneline -12` before any change.** Rules as before (no subagents; one heavy process at a time for anything timed;
browser = LuaJIT; bob forbids circular requires; the view, the news, the audio and the chain never write sim state; premises are
states the world could reach; Bash heredocs with apostrophes break - write patch scripts to files).

Done this session (all committed at this checkpoint):
1. **Hydrology fix** (D-179) applied and followed: the drifting wolves test found wolves eating at a kill from across a river
   (`wild.lua`: a way round, or "Cut off from the kill"); the hunting test now counts spears thrown (kills are a small number).
   Untouched planet numbers in D-179.
2. **Clouds v5** (D-180): `game/view/clouds.lua`, `game/render/shaders/cloudvol.fp/.vp`, `cloudshell.fp/.vp`, materials, render
   script (cloud volume drawn last of the world; the shell in the shadow pass); the cover from `sim/agents/weather.lua`
   `cell_day` (pure; village hashes identical); shots `scripts/shots_clouds.mjs` (`out/shots/clouds/before_after.png`);
   GPU cost measured with `out/s23/probe_gpu.mjs` (ANGLE D3D11 headless: free). Bridge `clouds{}` (cover at each village).
3. **Audio** (D-181): `audio.lua` `M.MIX`, herd bed 0.44 -> 0.14, calls rare and near; `scripts/play_audio.mjs`.
4. **Chain reaction** (D-182): `sim/chain.lua`, `game/ui/actions.lua`, newsfeed "chain N", bridge `actions{open, ev}`,
   `tools/v3_chain.lua --case ...`, `tools/v3_chain_dump.lua`, `scripts/shots_chain.mjs`, test `test_v3_chain` x3.
5. **R7 v2 powers** (D-183): `sim/powers3.lua` (sickness, sea, oil, meteor, blight), `sim/agents/blight.lua`, stock incubation and
   `sick_ev`, deeds `sickness`/`blight`/`impact`, news stories, chain polls; test `test_v3_powers2` x4; DEFS_V3 has 14 powers.
6. **The 8-year run found** a nursing mother walking to her kin for food while her newborn starved: `ask_kin_candidate` and the fair
   crier now check `p.nursing`.

Checks done: full quick suite 197/197 (phase-1 code), `test_v3_powers2` 4/4, web 11/11 (W02-lite rerun with `gfx=low`), lint,
debug build. **Pending (do these next, in order)**: the v3 unit tests again (the herd sickness change touches every herd),
`npm run test:sim` full; `node scripts/v3_hash_web.mjs --release` and `--premise flood5|conflict|fire|powers|pasture_fire`;
the 8-year untouched run again (`luajit tools/v3_chronicle.lua --days 384 --save out/s23/chron384b.tcsave`: no hunger deaths
expected now; watch the herd sickness with incubation); profiles (`luajit -joff tools/v3_profile.lua --days 4` at day 0 and
`--load` the day-384 save; S22: 3.91 M / 5.13 M instructions an hour, stock 39%); `node scripts/prof_web.mjs --release --hours 288
--runs 3`; the chain UI shots again with a new power (`scripts/shots_chain.mjs`); then PROGRESS/handoff numbers.

## After the checkpoint (D-184) - read before anything else
The pending checks were run; the second 8-year run starved 11 beside a full granary, followed to four mechanisms and fixed (a pit
that sealed a house - every site keeps a walkable yard, `map.cut_doors`; play loops that carried children through it -
`nav.los_last`; toddlers who never recovered - `kin.infant_tick`; a crier who gave birth on the road - `journey_ok`). Then aim
previews (`powers3.preview`), the flood's edge by marching squares, the news so far, the Sound pill, no `^` in `sim/`. Checks: quick
suite 206/206, browser = LuaJIT on all six, run 5 hunger 0. Details: D-184, PROGRESS session 23.

## Next (after the pending checks)
1. The news: a history list; relief / orphans / harvest stories; everyday aggregation.
2. A volume settings panel; per-power aim previews (what each writes, on the ring).
3. Traded grain carrying blight's taint; oil fouling shore food and gulls on the maps; relief and kin food joined to the harm in
   the chain.
4. R6: the palisade; captives only as persistent people.
5. Watch the year-8 livestock cost (stock was 39% of the sim at year 8) without reducing animal agency.

## Pitfalls found this session
- Headless rAF timing under SwiftShader is not a measure of anything; measure the GPU with ANGLE D3D11 (`out/s23/probe_gpu.mjs`)
  and the sim with render_off / instruction counts.
- A deed's `answers` is a causal claim: link a death to a hurt only when the hurt was grave or is the cause of death.
- `ctx.babe` is only the baby in one's arms; a nursing mother is `p.nursing > now` - every journey must check it.
- `flush()` of a view module must use the world's clock (`speed.v3_t`), not the last frame's (`state.hour_f`).
- A cast aimed by the screen centre may land beside the village: shots aim at the village's own cell (`v3_look` returns `cell`).

# Session handoff — session 22 (4 Oct 2026, ENDED on Arjun's word near the ~850K context checkpoint): the validation Session 21 left; the frozen children; animals and fire; lightning on beasts; the powers played; D-176-D-178

## FINAL STATE (end of session 22)
- main, all committed (47b2cac + the closing commit after it). Nothing half-done in the tree.
- Tests: quick sim suite **194/194** at 47b2cac (190 + `test_v3_beasts_fire` x4); after it every v3 unit test green
  again on the final code; web **11/11** (at the session start); lint clean; release build OK.
- Browser = LuaJIT (release): untouched 72 h, `--premise flood5|conflict|fire|powers|pasture_fire` 96 h (powers
  re-checked on the final code).
- Cost: LuaJIT 3.91 M instructions a game hour at day 0, 4.41 M at day 192 (old code); the 8-year world with the
  children now living 5.13 M (stock 39%: flocks grow over the years - watch it).
- 8 untouched years on the final sim (`out/s22/chron384n`, before the D-178 news-only changes): births 46, deaths
  illness 16, old age 4, natural lightning 1, **hunger 1** (a man of 69 in a fisher household that had lost its
  boat, helped by neighbours - a lean time, not a mechanism failure); **0 people frozen** (was 37 of 168).

## FIRST THING NEXT SESSION: apply and verify the hydrology fix (D-178 7)
`sim/systems/hydrology.lua` around line 102, in the land loop (daily steps, v3): the rain the ground cannot take in
(`quick`) runs off but is ALSO subtracted from the soil, so every downpour dries the ground by its excess. Fix:
```lua
      local ro = 0                      -- was: local ro = quick
      if s > fc then ro = ro + (s - fc) * k_run end
      local base = s * k_base
      ro = ro + base
      s = s - ro
      if s > 1 then ro = ro + s - 1; s = 1 end
      ro = ro + quick                   -- new: the excess runs off without leaving the soil twice
```
Measured: under the rain power soil 0.46 -> 0.33 with the bug, stays ~0.46 (runs off) with the fix; a cloudburst on a
drought 0.10 -> 0.13 with the bug, 0.10 -> 0.22 with it. It changes the untouched world (wetter soils after storms):
two statistical tests drifted with it (`wild_animals_belong_to_their_herds`: animals hunted 1 < 2 in the window;
`animals_are_individuals`: wolves' simultaneous stops 25% > 20%) - follow those, then the full suite, the flood
tests and premise `flood5`, an 8-year untouched run (`luajit tools/v3_chronicle.lua --days 384`: no hunger deaths
expected), `node scripts/v3_hash_web.mjs --release` (+ premises), and a DECISIONS entry. Re-measure the untouched
4-year flows/soil against D-169's numbers (they were taken with the bug).

## What happened (in order)
1. **Validation** (S21 left it undone): suite 190/190, web 11/11, browser = LuaJIT untouched + flood5/conflict/fire
   + a new `powers` premise (all v1 powers cast at once), profiles day 0 / day 192, 8 untouched years without hunger.
2. **A split 8-year run starved 14 people** (save at day 192, load, go on). `tools/v3_save_mid.lua` (save at a year
   boundary, restore a copy, advance both side by side, diff the first state that differs) found: **every person
   born in a running world was never scheduled** (`life.birth`; 37 of 168 alive after 8 years had never taken a
   step - children of 4-8 never walked, ate or played), a queue rebuilt in the past (a loaded save replayed years of
   a child's life in a minute), dogs never re-queued (every premise that rebuilt the queue froze the dogs), stale
   trade/wild caches, households unable to kill a beast for the pot while the village's other larders held food, and
   v3 commands applied up to an hour after the click. All fixed (D-176). Residual: a restored world still parts from
   the live one after ~6 h (a claim count on a charcoal clamp: `tools/v3_save_mid.lua --fine 7`); players cannot save
   v3 worlds yet.
3. **Animals and fire** (Arjun's playtest): `tools/v3_fire_animals.lua` + `tests/sim/support/fire_audit.lua`; fire by
   its extent (`danger.fire_near`), noticed by each in its own time, run from its own way, kept away from while it
   burns, burns what is in its flames; a flock grazing over its fold's ground was "shut in"; planet fire no longer
   painted over a village map close in (D-177). Watched in the game (`scripts/shots_beasts_fire.mjs`): the flock
   spreads across the open pasture within 25 minutes and keeps there while the stand burns.
4. **Lightning on beasts** (D-177): beasts by height (buildings by theirs), the ground's current, carcass, unwilling
   loss, the owners remember, a story rooted at YOU; the click lands on the terrain's ground or on the entity under the
   pointer; played through the UI (`scripts/play_powers.mjs`): a goat and a cow in the open killed, a sheep under a
   tree spared (the tree took it and caught fire), a house struck.
5. **The powers played** (D-178, `tools/v3_power_matrix.lua`): the god's rain begins at the cast (a downpour, rain to
   the day's end); every cast has its own story of what it changed (rooted at YOU, merged when repeated); culled
   animals leave a deed; lightning stories name what was struck; at most two everyday cards; audio deeds read at every
   zoom, fires louder close up, herds call less often. The hydrology bug (above) found and left for next session.

## Next (in order)
1. The hydrology fix and its verification (above).
2. R7 v2 powers (doc 70 §2), designed this session from the code (not built):
   - **Sickness in a herd**: the herd already has a daily sickness (catching by the share sick, crowding, wet, hunger;
     recovery; deaths) - add an incubation (`a.sick_inc`: infected, not yet showing, still sold or droven - so trade
     and droving carry it), a cause (`a.sick_ev`, passed on to those it infects; the herd's source), and a deed per
     herd-day of deaths with that event (the news: "Sickness among the sheep at X", rooted at YOU when the power
     seeded it). The power infects a few beasts of the herd nearest the click.
   - **Blight** on v3 plots: `pl.blight` severity with its event; grows with warm wet days, cuts the yield, spreads to
     plots near by wind (by severity, nearness, wetness), and in seed grain (a taint on the granary's grain sown next,
     carried with traded grain); owners notice it past a share.
   - **Warm / cool sea**: a heat anomaly over ocean cells only, full strength, ~16 days (`powers.update` heat branch
     restricted to the sea); SST follows (temperature.lua), plankton/fish answer by ocean.lua's window (10-26 C,
     heat kill > 30): a temperate sea warmed may bring more fish, a warm one more die-off; fishers (act.lua 755) and
     gulls read F.fish; a story from the fish at a fishing village's sea cells against their reference.
   - **Oil**: v2's oil (`F.oil`, currents, beaching) on v3: the catch already reads F.fish (oil kills fish); add the
     shore: oil on a village map's sea and shore tiles fouls shore gathering, gulls; stories.
   - **Meteor**: a compound physical event at the impact: planet fire on the cells, a quake (`shake.quake`), aerosol
     (`G.aerosol`), heat; on a village map fire and hurt within the blast; at sea `tsunami.raise`. Rooted at YOU.
3. The news: a history list, relief / orphans / harvest / hunting stories; settings panel for volumes; per-power aim
   previews; then palisade and captives.
4. Watch the cost of herds in long runs (stock 39% at year 8).

## Pitfalls found this session
- **People created at runtime must be queued** (`life.push`); a queue rebuilt (`W.v3rt = nil`) must never schedule in
  the past and must include dogs.
- **Runtime caches that read mutable state** (routes priced by woods, partners, docks, the border land) must be reset
  at year boundaries (`sys.year`) or a restored world diverges.
- `world.advance(W, W.hour + 0.5)` never moves past the half hour (`W.hour` counts closed hours): carry your own clock.
- In Lua `local a, b = x and f()` keeps only f's first value.
- v3 commands given mid-hour now apply at their own moment (`cmd.at`); tests that queue a command then advance get it
  at once.
- Bash heredocs with apostrophes break in this harness - write patch scripts to files (again).

# Session handoff — session 21 (3-4 Oct 2026, extended autonomous run, ENDED at ~900K context on Arjun's word): flood water, arms and kin help, climate extremes, R7 rethought and its v1 powers built, the world news, world audio; D-169-D-175

## FINAL STATE (end of session 21)
- main, all committed (last: the handoff commit after 371605a); `.env` (the ElevenLabs key) gitignored and
  checked absent from every commit.
- Tests: quick sim suite 186/186 (run on the code up to the world-audio commit; the last two commits - the v3 powers and the news' everyday life - had their own tests green alone; rerun the full suite first); new this session and green when run alone:
  `test_v3_flood` x2, `test_v3_kin`, `test_v3_news` x3, `test_v3_powers` x4 (and `test_v3_namespace`,
  `test_v3_conflict` x10, `test_v3_year`); lint clean; debug build OK (11.3 MB with audio). Browser = LuaJIT
  (release) checked untouched 72 h and through a flood (`--premise flood5`, 96 h) **before** the news/audio/
  powers code landed - the view code was then audited for writes to sim state (one found and fixed: the news
  marked `s.raid`); **re-run the hash check first next session**.
- Not run at the end (do them first next session, in this order): `npm run test:sim` (full rerun on the final
  code), `npm run test:web` (11/11 expected), `node scripts/v3_hash_web.mjs --release` (+ `--premise
  flood5|conflict|fire`), `luajit -joff tools/v3_profile.lua --days 4` and the day-192 profile
  (`tools/v3_chronicle.lua --days 192 --save F` then `--load F`), an 8-year untouched chronicle
  (`--days 384`: no hunger deaths expected), `node scripts/prof_web.mjs --release --hours 288 --runs 3`.
- Watched this session: untouched 192 days - no hunger deaths (illness 6, old age 3), 420 exchanges, natural
  floods only on bottomland; ice age / hothouse 192 days - no hunger deaths.

## What the game is now (the R7 loop, playable)
WATCH (the planet; world audio from the state at the camera's focus) -> NOTICE (World News: five cards under the
clock - disasters, conflict, pressure and everyday life, each one story per incident growing in place) -> GO THERE
(click a card: the camera flies close in, the person at the heart of it selected, an active danger slows time to
Watch) -> UNDERSTAND (the card's lines, the inspector's whys, the badge -> "Why?": real causal links to NATURE /
PEOPLE / YOU) -> INTERVENE (the v3 power bar: lightning at a point, rain, dry spell, warm/cold air, stir the
mountain, strain the fault, cull, bring) -> WATCH THE CONSEQUENCES (stories rooted at YOU, a shimmer cue).
Keys: M mutes the world's sound.

## Next (in order)
1. The validation list above (and fix whatever it finds).
2. Inspect the game as a player for 10-15 minutes (`npm run play`): the feed at watch speeds, clicks, powers,
   sounds - tune weights, gains and cue frequency from what is seen and heard (`scripts/shots_news.mjs`,
   `shots_powers.mjs`, `shots_flood.mjs`; bridge `news`, `audio`).
3. R7 v2 powers (doc 70 §2): sickness in a herd, blight on a v3 field, sea warmth, oil, meteor on the v3 world.
4. The news: a history list, more kinds (relief asked and given, orphans taken in, hunters' kills, harvests),
   aggregation of everyday stories per village-day when busy.
5. R6 depth still open: the palisade round the houses (ring footprint, gates), captives only as persons.
6. Polish: flood water edges, the aim ring per power, a settings panel (volumes).

## CHECKPOINT (~850K context, session 21) - kept for the record
**If you are resuming after a compaction: re-read this section, CLAUDE.md, `docs/v3/70_R7_GOD_POWERS.md`,
`75_PLAYER_EXPERIENCE.md`, `80_WORLD_AUDIO.md`, `65_R6_5_LIVING_PLANET.md` §12-13, D-169-D-173 and
`git log --oneline -12` before any change.** Rules as before (no subagents; one heavy process at a time;
visible action = simulation; premises not forced outcomes; browser = LuaJIT; bob forbids circular requires;
the view never writes sim state - not even lazily: `volcano.list` makes the sites, the audio reads `V.volc`).

Done (commits 7bf35ba .. 6e2586b on main):
1. **R6.5e flood water** (D-169) - hydrology (infiltration excess, daily steps only), `sim/agents/flood.lua`,
   the urgent choice, dry roofs, drawn water; `tools/v3_flood.lua --premise flood5 [--map]`.
2. **Arms** (people.arm_for/might; conflict.strike/party_might) and **kin help** (think.help_kin_candidate,
   PLAN/WORK help_kin, `help` deed).
3. **R6.5g** (D-170) ice age / hothouse premises watched; heat stress (`heat_t` 24 C); `settle.dock_point`.
4. **R7 designed** (D-171): `docs/v3/70_R7_GOD_POWERS.md` (v1 set; never outcome powers).
5. **World news** (D-172): `sim/news.lua` + `game/ui/newsfeed.lua`; bridge `news{n, go}`; tools/tests above.
6. **World audio** (D-173): `scripts/gen_audio.mjs`, `assets/audio/*`, `game/audio/audio.lua`, bridge `audio`.

Next (in order):
1. **R7 v1 powers on the v3 world** (doc 70 §3): replace the v2 power list in `game/ui/powers.lua` for v3
   worlds; `sim/powers3.lua` (or v3 branches in `sim/powers.lua`) - each a few lines: lightning ->
   `blaze.strike(W, s, x, y, now, nil, ev)` at the map point (else the planet cell); rain/dry -> forcings with
   the power's tag for N days; warm/cold air -> `heat_anom` forcing; stir -> volcano pressure; strain ->
   boundary stress; cull/bring -> W4 groups (+ map individuals die where they stand). Aiming close in on a
   map point. News: stories rooted at YOU (the chain already follows events' parents and the planet's tags).
2. Polish if time: the news history list, a mute key, flood water edges, the palisade (R6f), captives (only as
   persons).
3. Before the handoff: quick suite, namespace, web 11/11, lint/build, `node scripts/v3_hash_web.mjs --release`
   (+ `--premise flood5|conflict|fire`), the day-192 profile, an 8-year untouched chronicle (`--days 384`),
   inspect the game (shots), then PROGRESS/DECISIONS/SESSION_HANDOFF/NEXT_SESSION_PROMPT/roadmap/CLAUDE.md.

Pitfalls found this session: a headless click inside one frame never reaches Defold (press with frames held);
the swim field opened the map's border (A* crash) - closed now; a door in front of a footprint on the
floodplain (households cut off by in-channel high water) - `people.cut_off`; a trader home to a lost dock
crashed - `settle.dock_point`; the island move test's window is 48 days now (an island famine's timing is
chaotic); a kin of Tekheim cannot have married into Zurug (44 h road: beyond courting reach) - premises must
be states the world could reach.

# Session handoff — session 20 (3 Oct 2026, extended autonomous run, ENDED): R6 and R6.5 designed; R6a-R6g first pass and gaps; R6.5b-d, f built, g watched; D-148-D-168

## FINAL STATE (end of session 20)
- main, all committed; quick sim suite **180** (179 + `test_v3_namespace`), web **11/11** (at 0001555),
  lint clean, release build OK; browser = LuaJIT untouched and with the conflict, fire, cold_snap, quake,
  eruption, tsunami and hard_winter premises.
- Cost: LuaJIT 3.78 M instr/h at day 0, ~4.5 M at day 192; release season 10.8 s (37.5 ms/h, 4 runs).
- Untouched 8 years (384 days, after the `lift` fix): deaths illness 15, old age 4, **no hunger**;
  839 exchanges; herds bounded (pigs cycle with the autumn kill).
- **First thing next session**: read this section and the CHECKPOINT below; then R6.5e flood water
  (hydrology first), R6 palisade/arms/kin who walk over to help, R6.5g longer range - see
  NEXT_SESSION_PROMPT.md. After any new work or plan kind: `test_v3_namespace`; before handing off:
  an 8-year untouched chronicle (`--days 384`) and the day-192 cost.


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
- **R6.5c done** (eca2268, D-155): `blaze.planet` (planet fire -> map trees at the edge; a local fire that
  takes hold -> planet fire on its cell); people remember fire seen; premise `named("wildfire")`.
- **R6.5f part 1 DONE** (a32dfdc, D-156): earthquakes - `sim/agents/shake.lua` (boundary stress, quakes,
  felt by distance, buildings by construction, trapped dug out, lodging), tests `test_v3_quake` x2,
  `--premise quake` browser = LuaJIT. Night quake under Tekheim: 3 houses fell, 7 trapped, all dug out.
- **After the checkpoint (resumed after a compaction)**: volcanoes done (9f60b6b, D-157; `--premise
  eruption` browser = LuaJIT); no burst of quakes on a world's first day (913a2c3); **R6.5d snow and
  frost on the maps** (411ea2b, D-158: `sim/agents/snow.lua`, `weather.walk_k/reach`, `shake.roof_load`
  shared with ash, starving beasts fail in days, premises `cold_snap`/`hard_winter`, `test_v3_snow` x4,
  `scripts/shots_snow.mjs`; `--premise cold_snap` browser = LuaJIT); **beasts and fire + late alarms**
  (D-159: beasts cry out, run, are let out of a burning pen or burn; lost pens planned again; an
  interrupt falling after a step's end now interrupts the next step - touches every reaction).
- **R6 gaps** (D-160): fear of a place (`deeds.fear` -> `migrate.push`, need-only push for taking),
  restocking (`stock.herd_worth`/`lose`: unwilling losses), the band told at its hour and waiting at
  the gate (`conflict.raid_call`/`coming`). Two conflict tests' premises were made exact (takers who
  lived at the hungry village; the raiders know only Zurug's fold) - the old wording assumed outcomes
  the world no longer gives (a household moved away restocks from elsewhere).
- **Fields burning, blood costs trust** (D-161), **tsunami, the failing killed everywhere, lost
  buildings planned again** (D-162). Found in a 192-day run: with starvation slowed (D-158) herds grew
  without bound (pigs 13 -> 280) - the daily `plan_failing` is now what keeps herds to their feed;
  re-check herd sizes in every long run (`tools/v3_chronicle.lua` "villages at the end").
- **Herds kept to their feed** (D-163): starvation 0.012/h, `stock.pig_core` (a sow per household and a
  boar, replacements kept), `plan_pig_keep` (3 growers a household), marked beasts not bred, the herd
  scan over the living only (exact). Day-192 world: 4.52 M instr/h (6.2 M before; 4.77 M at the
  checkpoint's code). Measure long-run cost with `tools/v3_chronicle.lua --days 192 --save F` then
  `luajit -joff tools/v3_profile.lua --load F --days 4` - a day-0 profile does not show herd growth.
- **Hard winter watched** (D-164): `tools/v3_chronicle.lua --premise hard_winter`; a toddler carried on a
  trade trip starved (fed only from provisions; handed over as the father set out) - fixed.
- **STATE at 5e09af4**: all green as at 2c588da plus aftershocks, helped home, leaf fodder, ice crust
  (quick 178, browser = LuaJIT untouched/cold_snap/fire/quake); D-148-D-167.
- **STATE at 2c588da** (all green): quick 178/178, web 11/11, lint, release build; browser = LuaJIT
  (untouched and conflict/fire/cold_snap/quake/eruption/tsunami/hard_winter premises); LuaJIT 3.78 M
  instr/h day 0, 4.52 M at day 192; release season 10.8 s (37.5 ms/h, 4 runs).
- **Aftershocks** (D-165), **the badly hurt helped home** (D-166: `hurt_down` 0.75; two people walking
  the same way at the same pace instead of a boat's `riding`).
- **8-year untouched run** (384 days): found 15 deaths from hunger after year 5 (none with the
  checkpoint's code) - the helper's `WORK.lift` had replaced the tan pit's (fixed: `lift_hurt`;
  `test_v3_namespace` guards it); then 0 hunger deaths, 839 exchanges, deaths illness 15 / old age 4.
  Also: someone cut off by risen water swims toward where they were going (`cost_swim`).
- **Alliances** (D-168): `deeds.kin_houses`; grievance counts wrongs to close kin elsewhere at
  `deed_ally_k` 0.5 (test `a_wrong_to_kin_elsewhere_is_felt`, ~1 min: runs until a bride moves).
- **Leaf fodder** (D-167): boughs lopped from trees by the pen when the hay is gone and the beasts hungry.
- **R6.5e flood water deferred**: a season of the deluge power over Krikeivorn raises its river's flow
  0.004 -> 0.006 (map flood factor 1.1): the planet's hydrology is not flashy enough at village rivers
  for water beyond the channel to arise; needs a hydrology look first (soil water rises ~0.01 a day
  under a deluge). The map has no tile heights (flood reach would be distance from the channel).
- **Older: R6.5f part 2 - volcanoes (done)**: generate volcano sites in v3 worlds lazily from convergent
  boundaries (+ a hotspot) like `worldgen/settle.lua volcanoes()` (W.ent.volcanoes is nil in v3); pressure
  per volcano gathering daily, eruption past a noisy threshold with a VEI; effects: `G.aerosol += f(VEI)`
  (already cools every cell -> v3 crops/pasture), local fire/heat near the vent (planet `fire.ignite` on
  the cell and neighbours), ash fall downwind on village maps (an ash depth per map: grass under ash ->
  pasture lost for days, roofs loaded -> damage like a quake's, fertility later), shaking felt (shake).
  Premise named("eruption") at the volcano nearest a village; tests. Then: the R6.5 doc status, a long
  untouched run with natural quakes/strikes, the final checks.
- **Earlier plan text (done)**: earthquakes (`sim/agents/shake.lua` planned): planet boundary stress
  (`static.boundary`/`bstrength`: convergent and transform cells accumulate, release at a noisy threshold
  -> a quake at a cell with a magnitude); each village map within reach feels an intensity by distance;
  each building is damaged by intensity x its construction's vulnerability (log house 1, on stone
  footings 0.7, hut 0.6, granary 1.2) x (1.3 - condition): cracked (cond down, repair) or collapsed (a
  ruin: those inside hurt, the badly hurt `trapped` until dug out by neighbours - the urgent choice
  "dig", hours of work; untended trapped may die); people outdoors thrown down (small hurt); everyone
  alarmed (shaking): out of buildings, parents to children; afterwards the homeless lodge with kin
  (`migrate.lodge`) and houses are planned (timber wanted). Premise + tests; then volcanoes (sites in v3
  worlds from boundaries; eruption: vent, ash, `G.aerosol`).
- **Older next list**: (1) R6.5c wildfire between planet and map (the planet's `F.fire` on a map's
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
