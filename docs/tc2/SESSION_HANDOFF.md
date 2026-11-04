# TC2 Session handoff

## Session 32 (4 Nov 2026, from 15:50 IST; Control Room Frontier Max + Release/QA; Arjun's FINAL SHIP PUSH, ~7 h to submission) - powers that read in seconds, the butterfly tree, the first impression (D2-206)

### Context 2 (4 Nov, from 18:44 IST) - final QA and the submission build (keep current; overrides "Exact state" below)
- **THE SUBMISSION BUILD IS P (code 531cd34)** - M + the god's rain falling at once with the last of rain and drought ruling, the studio's intro, phones in landscape (D2-207 15-18); in `tc2/web-build/web`, a copy served at http://127.0.0.1:8706/ (`web_P`, the ONE server kept); the itch zip `tc2/web-build/a-tiny-world-itch.zip` remade from it (12 entries, the intro included, slashes checked); `s32mobile1` 8/8 (`tc2/scripts/mobiletest.mjs`). Before it: - **M = b7c8d35** (L + Arjun's last three: the card's and the book's links never wrap, the year-0 timeline says what it will hold, the interface's sounds small bells; LapseTests' wording), built straight into `tc2/web-build/web` (`web.*`, "A Tiny World"); a copy served at http://127.0.0.1:8706/ (`web_M`). Checks: firstrun `s32first9` 14/14, webtest `s32web6` 9/9 (browser = headless bit-identical), `s32check1` (the year-0 timeline and its foot). **The itch upload**: `tc2/web-build/a-tiny-world-itch.zip` (31.7 MB, made with Windows' bsdtar `tar.exe -a -c -f ... -C web_M index.html Build TemplateData`; its central directory read byte by byte: 11 entries, every name with forward slashes, none with `./` or `/`, `index.html` at the root; the build is not threaded - no SharedArrayBuffer needed). **Store media**: `tc2/web-build/store_gifs/` (GIFs under 3 MB, 800-1120 px, ~5 s: day and night over the planet, the first hamlet, lightning and fire, an eruption, years skipped from orbit, the year-300 town, a stone from the sky, a year-1,000 town, the realms, a quake; a still of the tree), made by `$SP2/make_gifs.py` from `play.mjs` bursts (`burst NAME N [MS]`, new). Arjun (21:40): no more long runs - the full suite was stopped (its one failure, LapseTests' stale wording, fixed). Build L (0e1c394) at :8705 is superseded. Earlier builds still served (this session's `serve.mjs`): g :8700 (`web_g`), c1 :8701, 77c8728 :8702, i :8703, j :8704 - superseded.
- **STANDING DIRECTION (Arjun, ~20:25)**: do not wait for his reports - make the final decisions and do everything properly; up to 2 subagents (worktrees, merged by the main session after reading their diffs). Not pushed: push only at his word (as Arjun0014).
- **Merged after 20:30**: `wip/s32-alive` (608a126: people alive - Presence and the person shader; build k = 608a126 compiled the shader with no errors) and `wip/s32-powers` (605702a: every power checked with `headless powers`; famines and rebuilt homes after a cast rooted in it, fires in the god's drought the drought's), then 14ac539 (a cull that would take nothing refused) and 0e1c394 (warm/cold air name the crops' harm only where they slow them). D2-207 items 9-13.
- **Brief (Arjun)**: final QA / submission mode - no new features; newest build served for his own play at once; finish session 32's QA; fix only real blockers and ugly obvious issues; the ACTUAL final build straight into `tc2/web-build/web` (`web.*` files); then serve it, first-run, Web regression, one complete powers playthrough, save/load, menu, tutorial, pages, book, tree; his feedback first; docs; commit; **do not push unless he asks**. No long sims; integrated Radeon only.
- **Servers for Arjun (node `serve.mjs`, this session's)**: build g (288051d) at http://127.0.0.1:8700/ from the copy `tc2/web-build/web_g`; candidate 1 (e5e9520) at http://127.0.0.1:8701/ from `tc2/web-build/web_c1`. The final build is served from its own copy too (so a rebuild into `web` never breaks his play).
- **QA on build g**: `firstrun.mjs --tag s32first3` 8/8; `headless.mjs floatcheck` (main rebuilt, clean) then `webtest.mjs --tag s32web2` 9/9 (browser = headless bit-identical; reload continues; settings kept; a day away ~96 years; the title holds still; no console errors). Judged `s32pages1` (card, person page + family, place Place/Now/Why?, a work, the book's ages - good; the magenta figures are a first household's livery), `s32first3`, `s32pow4` (the tree), `s32menu1`, `s32look3`.
- **Fixed (presentation only, `unity.mjs cc` clean)**: 563f0b7 lone rain forest emergents (40 m poles in the farmland) only over a closed canopy (share 0.5+); e5e9520 the cast's word above the guide's card; 77c8728 the tree's cards hold their words (92 px, word cut + ellipsis, place shortened). Checked, no change needed: "(M 7.1)" is already "(magnitude 7.1)" in the code; the menu's two markers were the script's parked pointer.
- **Builds**: candidate 1 = e5e9520 (`$SP2/final_build.sh final1`, 8:30, 0 shader/CS errors, integrated Radeon; the stale `web_next.*` of build g removed from `web/Build`); 77c8728 (`final2`) served at http://127.0.0.1:8702/ (`web_final2`) - superseded by Arjun's report. `$SP2` = `C:/Users/aswin/AppData/Local/Temp/claude/f68d9ed74c677f45/7a6732ee-583c-48a1-b217-1f4b0a7264be/scratchpad` (`final_build.sh TAG`; play scripts `final2.txt` every power on the year-300 town + a 5-year skip + the trees and Your Actions, `book1.txt` the book's wheel and the tree's seal, `erupt1.txt`, `still1.txt`).
- **Arjun's report at ~19:13 (ten points: D2-207)** - all worked: (1) default speed 1 (bc73f34); (2) reach shrunk (87118c6); (3) the eruption's cracks and lava (23626fc); (4)/(5) measured - no sim fault (stillness audit, `timeuse --skip`), shock crowds face what struck (d1ab665); (6) checked by tests now and by `final2.txt` on the build; (7) the 23-step guide (subagent, merged 4edf1ff; `firstrun.mjs --tag s32first5` 14/14 on build h); (8) "A Tiny World" (e7b21c4); (9) the book's wheel and last lines (subagent 8af854d); (10) the tree years and decades on, from the top, a seal at the top right (subagent 32984a1, d9de267; merged bd95918). Merged tip 7ee04f4: ConsequencesTests 4/4, HistoryBookTests 8/8, PowersTests 13/13, SaveLoad 8/8, Determinism 7/7, ShockTests 10/10, Quake 4/4, Volcanism 6/6, Storm 3/3, floatcheck clean. Known limit told to Arjun: a meteor's/quake's tree ends with its days (rebuilt homes and migrations are recorded without a cause).
- **Build h** (ab207f9) for the guide's first test (`s32first5` 14/14). **Build i** (7ee04f4): `s32first6` 14/14, `s32web3` 9/9, `s32final2` (every power + five years), `s32book1`, `s32erupt2`. **Build j** (01b40b4: the eruption felt within 1.5 km and split where pointed): `s32first7` 14/14, `s32web4` 9/9, `s32judge1` (a judge's first minutes and 30 years: nothing broken, no console errors). **Build k** (608a126): the shader compiled; `s32alive1`.
- **Build L (0e1c394)**: built 21:12-21:20 (7:42, integrated Radeon, 0 shader/CS errors); checks: see PROGRESS CURRENT STATE (first-run `s32first8`, Web regression `s32web5`, every power `s32final3`, the full headless suite).
- **Next**: nothing is owed for the submission but Arjun's word; if he reports anything: reproduce -> fix -> rebuild into `web` with `$SP2/final_build.sh TAG` (a copy served on a new port) -> firstrun + webtest. Known limits (D2-207): migrations carry no cause; the good powers' trees are short; the meteor's and plague's scale are the world's laws.

### Exact state (session 32, first context; keep current)
- **Direction (Arjun, 4 Nov)**: final ship push - P0 god powers readable with REAL reactions, a consequence (butterfly) tree for every cast, the camera drag fixed; P1 the world gorgeous, the opening (loading, menu, credits, guide); P2 the book, audio. Up to 2 subagents. Integrated Radeon only. No long sim runs.
- **Code**: edits in the worktree `C:/Game Dev/tc-s27int` on `wip/s27-int`; main fast-forwarded before each Web build. Subagent branches: `wip/s32-tree` (merged: the tree), `wip/s32-spells` (slow powers' visuals, worktree `C:/Game Dev/tc-s32tree`), `wip/s32-face` (loading/menu/credits/guide, worktree `C:/Game Dev/tc-s32face`) - merge each after reading its diff.
- **Sim changed (sim 108 kept, no save format change)**: a shock felt (meteor, quake felt >= 6, lightning within 260 m, eruption) interrupts everyone present (`Hazards.ShockAt`, `InterruptReason.ShockFelt`); `AgentSystem.ShockStep`: run (Flee, WhyCode.Shock) to open ground clear of it, keep together (Social) until `Hazards.ShockHold` passes; the hurt abed; a strong quake brings down buildings (`HistoryKind.HomesShaken` = 113, `Homes.Fell`), those under a falling roof die/hurt; homes taken/lots cleared after it carry the cause. Natural quakes and strikes alarm too (histories of saved worlds continue under the new rule). Tests: ShockTests 3/3, QuakeTests 5/5, PowersTests 13/13, ConsequencesTests (tree) pass; full suite on f55d773 was running at 16:25.
- **View**: drag = grab of the ground (CameraRig `GroundRay`); aim ring at the struck cell's heart; meteor flash/fireball/debris/dust wall; ShockView (quake wave dust, fallen buildings' dust); bolt bead column removed, flash; the dead of a stone/quake lie shrouded 8 h (WorldObjectsView); impact card after a cast (Hud.Powers `ImpactCard`); the tree (`Hud.Consequences.cs`, `Core/History/Consequences.cs`, bridge `tree last|ID|off`); bridge `tune NAME VALUE` (Look tunables live, incl. Grade*, LandVary, StandFar, StandGrain).
- **Tools**: `node tc2/scripts/play.mjs --steps FILE [--save F] --tag T` (a scripted play session: cmd/wait/shot/move/click/drag/wheel/key/state), `unity.mjs cc --ccdir NAME` (parallel compile checks).
- **Web builds** (side folder `tc2/web-build/web_next`, swapped into `web` when no play test holds `$SP/testing.lock`: scratchpad `webbuild2.sh TAG`): a (drag), b (shocks, VFX, tree: s32pow2), c (impact card, spells, face, book: s32pow3, s32first2 8/8 - "21 running for open ground" in a new world, s32look1, s32cam1), d (look: s32look2 - StandFar 8 chosen, s32look3, s32pow4 - the crater seen), e (audio, book2: s32menu1 - loading page, How to Play, Credits), f (the COARSE-load fix, review fixes, text QA, StandFar 8) building at 17:40.
- **Found and fixed this session beyond the brief**: a world loaded from a save made in deep time stayed COARSE for ever (SimHost continue path - no one out, no shock felt); the review subagent's 4 shock bugs (gale sleepers, the sick, quake re-homing into homes it toppled, the burnt roof's cause); text QA (species plurals, 0-values, jargon, tense).
- **Merged subagent branches**: wip/s32-tree, -spells, -face, -book, -audio, -book2, -review, -text. Running: wip/s32-qa (the full suite on the merged tip).
- **At 18:05 (context handoff)**: Web build **g** (main 7d86809+..: the names filter `wip/s32-names`, the spell wash fading near the ground, the news line giving way to the cast's word) FINISHED after the handoff (Succeeded in 8:47, integrated Radeon, swapped into `tc2/web-build/web`: its files are named `web_next.*`; it lacks only f9cd929) - built via scratchpad `webbuild2.sh g` (log `$SP/build_g.log`, `$SP/done_g` when finished; it swaps into `web` itself when `$SP/testing.lock` is absent). `SP` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Users-aswin--claude-spares-spare-teKswW/96506e8d-2507-4a95-8768-c2f876809edc/scratchpad` (play scripts pow1-5, look1-3, cam1, menu1, pages1 there). Build f verified: webtest `s32web1` 9/9 (browser = headless bit-identical), `s32pow5` (the year-300 town: "521 running for open ground - 86 keeping together"; drought/cold labels, the god's copper glint, plague tree), `s32pages1` (person/place/building pages, book realms/wars - shots taken, not yet judged). **Full headless suite on 8cb290c (the merged tip before the names filter): 537 passed, 0 failed, 0 skipped** (subagent, ~24 min), floatcheck clean; then `wip/s32-qa` f9cd929 (a wound's words told by what dealt it; ShockTests 10/10, Biography, Defence, Taking, Lives, Sworn pass) MERGED into wip/s27-int after the handoff (Unity C# compiles). Build g does not contain f9cd929 - the final build will.
- **Next, in order**: (1) when build g is in: `node tc2/scripts/firstrun.mjs --tag s32first3` (8/8), `node tc2/scripts/webtest.mjs --tag s32web2` (9/9; rebuild main's headless first: `node tc2/scripts/headless.mjs floatcheck` in main), judge `s32pages1`; (2) (done: suite 537/0/0, wip/s32-qa merged); (3) final submission build straight into `tc2/web-build/web` (`webbuild.sh` not `webbuild2.sh`, so the files are named `web.*`), then firstrun + webtest + one powers play (`pow5.txt`) on it; (4) PROGRESS/D2-206/handoff final; commit; do not push unless Arjun asks.
- **Test notes**: the tools' year-300 save is COARSE until its next midnight in builds before f (use `speed 5` to Day 2 09:00 before casting); `speed 6` lifts the camera into the lapse; `home big ALT` needs the first household housed (falls back to the anchor from build e on).

## Session 31 (1 Nov 2026, from 17:35 IST; Control Room Frontier Max + Release/QA; another game's session shares the machine) - a finished game's face (D2-205)

**END (1 Nov 2026): Arjun stopped the session.** Nothing is running. main = `wip/s27-int`, all of it committed; not pushed. Only the first commit of the session (f301037) has been seen in a Web build; everything after it compiles (`unity.mjs cc`) but has never run in the game, and Unity has not compiled its new and changed shaders. The next step is "Next, in order" 1 below, once the other session is done.

Arjun's last word (1 Nov 2026, session 31): he played the f301037 build (the first commit of the session: old world look, new interface) and said "so many issues" without listing them, then asked to stop the server and close every process this session started - done, nothing is running. **Do not start builds, servers or heavy jobs on your own: first ask him what the issues were, and whether the other game's session has finished.** His list comes before everything else in "Next, in order".

### Exact state (keep current)
- **Direction (Arjun, session 31; root `NEXT_SESSION_PROMPT.md`)**: make TC2 look and feel like a beautiful, complete, polished game that happens to contain a deep simulation; readability and game feel over hidden depth; heavy simulation runs only for a genuine regression; RAM < 86 %, CPU < 85 %, integrated Radeon only, never kill what you did not start; when the machine is busy, keep going with lightweight work and queue the heavy validation.
- **Code**: on `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, every edit there); fast-forward main before a build: `git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int`, never while a Unity job holds main's checkout. Sim **108** unchanged (presentation only).
- **Commits this session** (on the branch, oldest first): f301037 the interface kit, title, menu, guide, camera, sounds; 5a762b4 portraits, epitaphs, initials, a person's links, the first look pass; 343f340 stars, far grain, `firstrun.mjs`; d595cef realm maps, timeline marks, double click; 2586e77 fixes from `s31a`/`s31b`; 8693a5d the life told; 73ae81b docs; f5af13a a hint beside a page; then these docs.
- **The old queue is gone** (the machine restarted; `$Q` of session 30 no longer exists), and session 31's scratchpad script `webbuild.sh` may vanish too: it ran `cmd //c start "" //belownormal //wait //b node tc2/scripts/unity.mjs build-web --jobs 2` in main's checkout, logged RAM every 20 s, then restored `ProjectSettings.asset`. A build's own peak is ~95-97 % when the machine starts at 65 %: start them only from about 60 % or less, and run nothing beside them.
- **Web build `s31a` (f301037, 1 Nov 17:59-18:19): RAM peaked at 98 % (the machine started at 65 %, the other session's browsers beside it), so Claude Code stopped its wrapper shell for low memory; its Unity, emscripten and wasm-opt children ran on (the classifier refuses stopping them) and finished: "Build Finished, Result: Success". `tc2/web-build/web` is that build, playable. Do not start another Web build until the other session has finished and RAM is about 60 % or less (Arjun's word, 1 Nov). While a build runs `tc2/web-build/web/Build` holds a new data file beside an old wasm: not playable until it completes.**
- **New art tools**: `python tc2/art/ui_kit.py [--sheet F]` (icons, panels, logotype into `Game/Resources/UI`), `python tc2/art/ui_sounds.py` (interface and stillness sounds into `Game/Resources/Audio`). Bridge: `title on|off [PAGE]`, `menu PAGE|-1`, `guide STEP|-1`; bookshots `--menus [--guide "0,1,..."]`. `?dev=1` shows the tool numbers in The World Now.

### Next, in order (session 31)
1. **When the other session has finished and RAM is about 60 % or less** (Arjun's word, 1 Nov): fast-forward main (`git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int`, first reverting any local copies of the scripts in main), Web build, then `node tc2/scripts/firstrun.mjs --tag s31c` (the opening by real clicks), `webtest.mjs --tag s31web1` (must stay 9/9), bookshots `--menus` and `--views orbit,region,village` over `tc2/out/civ/s2_ac5/world_y300.tc2` and `world_y600.tc2`; afterwards `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset`.
2. Judge the world's look pass in those pictures (grade, land drift, softer fields, sky dome, stars, clouds, the shelf; `Look.GradeSat/GradeContrast/GradeExposure/GradeGreen/LandVary` are tunable per shot): new shaders `SkyDome`, `Stars`, changes in `Terrain`, `Clouds`, `TC2CloudForm` have never been compiled by Unity yet.
3. Judge in them: portraits in the page, illuminated initials, a person's links, the follow chip, the title's new composition, hints as chips. Sound can only be levelled here, not heard: Arjun's ear decides the mix.
4. Then the ten-minute judge pass and W-FOREST's sameness.

## Session 30 (29 Oct 2026, from ~02:00 IST; Control Room run #86 session 3; Arjun AFK) - fields between villages, a skip's colours of one moment, the sea's edge (D2-204)

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; Unity logs "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs; one heavy job at a time
  (a Web build takes RAM to ~77-90 %: gate `load` < 86 before even `unity.mjs cc`).
- **The queue's runner was never stopped**: Claude Code dropped its task `bp3fvcitn` from its list at ~02:00, but `runq.sh` (bash, started 22:24) and the
  job it was running kept going - `0_qq_web_s29g` ended rc 0 at 02:15 and the runner took the next job at once. Follow it with
  `tail -n 0 -F $Q/runq.log`; `touch $Q/stop` stops it after its job.
- **Code**: on `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`); every queued job fast-forwards main to it. Sim **108** unchanged (presentation only).
- **Done this session**: D2-203's pending closed (full suite 523/0/0; `s29web7` 9/9; Kaihope's burned crop told; the charred ruins WERE dark in `s29v7` -
  measured); D2-204 (1) two villages' fields meet furlong by furlong (bdf3e41, bf5ca22; shots `s30v1`, `s30v2`); (2) a cycle's colours of one moment (the
  skip's gold blocks); (3) the cast's word beside Your hand; (4) camera views `fieldmeet`, `shore`.

### Next, in order
0. (done) `0_rb_web_s30a` (s30web1 9/9, the cast's word, the skip's year 303), shots `s30v1`-`s30v6` (fields, shores), `0_re_web_s30b` (s30web2 9/9,
   Kaihope's shore) - all judged, D2-204.
1. (done) `0_rf_web_s30c` (s30web3 9/9: the HUD fixes seen), the tan veil found by elimination and fixed (the air near the ground, fbd708a;
   `0_rk_web_s30h`: s30web4 9/9, `s30air` clear at 1,100-1,800 m, the limb as before). D2-204 (1)-(8).
2. (done) The full suite 524/0/0 with the lint (f7de2f8); Web `0_rm_web_s30i` (s30web5 9/9; the fields' tag and click seen in `s30book4`/`s30book5`).
   Then b677e73 (the fields' words; no tag across a page): `s30web6` 9/9, both seen in `s30book6`. D2-204 (9).
3. (done) `0_ro_web_s30k`: `s30web7` 9/9, `s30book8` settled, every power cast and read (`s30pow1`) - D2-204 (10)-(11); fixed in 87e7eec. `0_rp_web_s30l`
   (87e7eec): `s30web8` 9/9, the tag clear of the bar, "within the hour", "yesterday" - all seen. The queue is idle; main = the branch.
4. Next: the brief's open looks (W-FOREST's sameness is a dedicated art pass awaiting Arjun's judgement) and readability gaps the pictures show -
   e.g. a building's page shot in bookshots is taken 3.5 s after the camera flies in from 6 km, before the finest ground and its trees are built (a
   script's timing, not the game's: `extra_building.png`).

## Session 29, context 2 (29 Oct 2026, 00:00 - ~01:10 IST; Control Room run #86 session 2; Arjun AFK) - the visual world as evidence (D2-203)

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; Unity logs "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs; one heavy job at a time
  (a Web build takes RAM to ~89-91 %: run nothing beside it, not even `unity.mjs cc` - gate on `load` < 86 first).
- **Code**: all on `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, every edit there), tip fec03b0 + these docs; the queued jobs fast-forward main to it
  themselves (`git merge --ff-only wip/s27-int` at their start; main was a453afe when `0_ql_web_s29e` began). Sim **108** unchanged (presentation,
  observers and text only).
- **The queue** (`Q` as in session 29's block below; runner task `bp3fvcitn` from context 1 still polling; follow with a Monitor on
  `tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |^start |runner"`). Done this context: `0_qf` (s29v2), `0_qg` (Web c1fffc6: s29web3 9/9,
  s29book2, s29book3), `0_qh` (s29v3), `0_qi` (s29v4), `0_qj` (Web 5464411: s29web4 9/9, s29book4, s29lapse3), `0_qk` (s29v5). **Running at the
  handoff**: `0_ql_web_s29e` (started 00:52, Web of a453afe: `s29web5`, `s29book5` with --play --meteor-play --extras --pages person:2 --book 7:0,
  `s29lapse4` = the timelapse with the softened seasonal ramp). **Queued behind it**: `0_qm_shots_s29v6` (Paitaimwick's burned homes with the forty-year
  soot, close/low) and `0_qn_web_s29f` (Web of 0e3f897+: `s29web6`; `s29book6` = the extras with the ruin last burned - extra_ruin_page/_hover; `s29book7`
  = Kaihope at year 600, village and region views with names - its name at its lived homes, and its Now/Why? pages `page_place_1_1`/`page_place_2_1`;
  `s29lapse5` = the skip from orbit after the stale-patch fix 5ad5c7f) - started 01:13; then `0_qo_shots_s29v7` (Paitaimwick's burned homes with the soot in
  the alpha, 3b66e4c). Done meanwhile: `0_qm` (s29v6: the soot did not show - fixed in 3b66e4c), `0_qmb` (s29n1: the night - fine). Then the queue idles.
- **Built and committed this context** (D2-203, all on the branch): the burned crop as charred strips; the rivers' far glint; works' tags with why; the
  History panel's lines opening the book, above the power bar; the ticker on a loaded world; no name under a panel; the book's words (XI, men sworn
  counted, no "0 days' grain"); charred ruins from the ledger and a ruin's tag/page saying how it fell (`Whys.FellTo`); the aim's words in a box; a
  person's crafts by name and works told once; the fields ending furlong by furlong (`TerrainLod.FieldFrames`); the seasonal tropics' ramp softened
  (the timelapse's straight edge); soot for forty years; a village's name, far tag and "go there" at its lived homes (`PlaceCentres`: Kaihope's homes
  stand 250-400 m from its ruined first square); the news ticker opening its why. Tools: `unity.mjs` forwards shader errors; bridge `way big`, `history`, `feedline`; bookshots
  `--extras` (road, work, History panel, a line) and `--pre`; headless `cells` (`--sid`, `--meridian`, `--burned`).

**At the handoff (~01:40 IST)**: `0_qp_fullsuite_s29c2` running (the full headless suite on the branch into `$N/bin_s29c2`; `$N/fs_s29c2_ok` when 0 failed,
0 skipped - read `$N/fullsuite_s29c2.txt`), then `0_qq_web_s29g` (Web of 0fe9093+: `s29web7`, `s29book8` = year 600 extras and Kaihope's Now/Why? with the
burned-crop line, `s29lapse6` = the skip after the orbit recolour). Everything since was judged: s29e (s29web5 9/9, s29book5, s29lapse4), s29f (s29web6 9/9,
s29book6, s29book7, s29lapse5), s29v6/s29v7, s29n1 - D2-203 (19). **OPEN, first**: the charred ruins do not show dark in Unity shots (s29v6, s29v7) -
D2-203 (19) "Open".

### Next, in order (older steps below are done)
0. Judge `0_qp_fullsuite_s29c2` and `0_qq_web_s29g`; fix the charred ruins (shoot Paitaimwick #51's burned homes at `--village 51 --views low` from over
   their spot ~65 m east of the square, or add a shot that frames a building; check `Instancer.Add`'s colour path and the ruin model's vertex alphas).
1. When `0_ql_web_s29e` ends: judge `s29web5` (9/9 expected); `tc2/out/webtest/s29book5`: play_aim / play_aimfar (the aim's words in a box, no road tag
   over them), extra_history (the panel above the bar), page_person_2 (crafts by name, "four times (years ...)"), book_7_0; `s29lapse4/lapse_3_orbit_later.png`
   and `lapse_7_skip_orbit.png` (the straw's edge soft, no straight line). Fill D2-203's "Pending".
2. Judge `0_qm_shots_s29v6` (`tc2/out/shots/s29v6`: the burned homes black among grey ruins) and `0_qn_web_s29f` (`s29web6` 9/9; `s29book6/extra_ruin_page.png`,
   `extra_ruin_hover.png`; `s29book7/village.png`, `region.png`: "Kaihope" over its lived homes, not over the ruins).
3. Then by the brief: the owner-to-owner crop boundary (two villages' fields at different stages meet along the cells - D2-203 "left"); W-FOREST's
   sameness; the night sea's moon veil; readability gaps the pictures show.

## Session 29 (28 Oct 2026, 22:23 - ~23:45 IST; Control Room run #86 session 1; Arjun AFK overnight, "the jam must clearly read as a real simulation") - the timelapse, the person page, Why?, every level readable

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; Unity logs "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs; one heavy job at a time.
- **Code**: all of it on `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, where every edit is made), tip = the handoff docs commit after 0c35860. **main
  = 92b75fd at the handoff, BEHIND the branch** (the Unity shots job `0_qe_shots_s29v` holds main's checkout from 23:39): once it ends, fast-forward -
  `git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int` - and verify `git log -1 main` = `git log -1 wip/s27-int`. Sim **108** unchanged all session (presentation and observers only).
- **The queue** (`Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`; the runner
  `bash tc2/out/tools/s23/runq.sh` started this session (task `bp3fvcitn`) keeps running; follow with a Monitor on
  `tail -n 0 -F $Q/runq.log $Q/log/<job>.log | grep --line-buffered -E "^end |^start |exit|FAIL|pictures"`). Done: `0_qb_web_s27prof2`, `0_qc_web_s29a`,
  `0_qd_web_s29b` (rc 0 at 23:39: `s29web2` 9/9, `s29book1`, `s29lapse2`, `s29perf2` - the road cache x1.115 on a running village view, RoadAt 4.04 ->
  0.43 ms). **Running at the handoff**: `0_qe_shots_s29v` (started 23:39) - Unity shots of seed 2 year 600 (orbit, region, village, close, standardlow,
  town, townlow) into `tc2/out/shots/s29v1`, its log `$Q/log/0_qe_shots_s29v.log` (shader errors, GPU line) - for the brief's section 7. Then the queue
  idles.
- **Built and committed this session** (DECISIONS D2-199 to D2-202): the timelapse (`CameraRig.Lapse.cs`, `Hud.Lapse.cs`, `Core/History/Headlines.cs`,
  `SimHost.BeginSkip/StopSkip`, the bar's Skip...); the frame (`WorldObjectsView.PeopleStale`, `TerrainLod.RoadsLaid`, `TerrainLod.RecolourTogether`);
  the person page (`Hud.Person.cs`, `Core/History/Lives.cs`); Why? (`Core/History/Whys.cs`, `Hud.Place.cs`, `HistoryLedger.RecordsAt`); roads read, a
  village from afar, the world now, the building leaf (`Hud.cs`); the book's order with a Peoples part, wars' leaders and costs, lives linked, casts'
  threads (`HistoryBook.cs`, `Hud.Book.cs`); the powers' word (`Hud.Powers.cs`); the feed in the book's words (`SimHost`).
- **Verified in Web builds**: `s27web8`, `s29web1`, `s29web2` 9/9; `s29lapse1`/`s29lapse2` (the timelapse played for real); `s29book1` (every person and
  place section, the card, Your Actions). **Not yet in a Web build**: roads read, the far village's tag/click, the world now, the building leaf, the book's
  new order/Peoples/wars/lives links, the cast's word and live thread, the hover's why, the feed's words, the followings, the tree's layout fix.
- **Tests** (headless, worktree): HeadlinesTests 3/3, LivesTests 4/4, WhysTests 3/3, GazetteerTests 3/3, HistoryBookTests 7/7, PowersTests 13/13,
  TongueTests 4/4, PeaceTests 3/3; `unity.mjs cc --src "C:/Game Dev/tc-s27int/unity/TinyConsequences2"` clean.
- **Tools**: bridge `skip YEARS`, `stopskip`, `follow PID|oldest`, `select PID|oldest|head`, `person head N`, `info on|off`, `building SID|big [KIND]`,
  toggles `peoplestale`, `roadcache`; state `lapse`, `lapseLight`, `skipping`, `camAlt`, `bigX/bigY/bigFront`; `bookshots --lapse [--flash N] [--skip Y]`,
  `--extras`, `--pages card:N[:who],person:N[:head]`. Book parts are now 0-10 (Your Actions 10).

### Next, in order
1. When `0_qe_shots_s29v` ends: fast-forward main (above); read `s29v1` (judge political centres, ruins, walls, roads, war scars, the orbit's planet; the
   job log's "Shader error" count and GPU line).
2. Queue a Web build of main (the tip) with webtest `s29web3`, bookshots `--extras --pages "card:0,person:3:head,place:0" --book "4:0,4:1,2:0,7:0,10:0"`
   and `--play` (the cast's word, Your hand's thread); judge the new pages; fix; commit; fill D2-202's "Pending".
3. Open: the one straight seasonal edge across the land from orbit during a skip (`s29lapse2` lapse_7: a patch outside `_shownLast` or the cells'
   colour layer - check `TerrainLod.RecolourSome`'s list against the patches drawn); the region view's jagged region edges and the night sea's broad moon
   veil (25 §8); W-FOREST's sameness.
4. Then by the brief: keep making the simulation readable where the pictures show a gap (a village's works named with why on hover; the realms layer's
   legend in the leaf style; the History panel as the book's recent pages), and the visual world where it is evidence (section 7).

## Session 28 (27 Oct 2026, ~16:00-17:15 IST) - housekeeping only: the disk audit and cleanup (D2-198); no development

### Exact state (keep current)
- **Arjun's word**: no development this session - find what fills the project's 40 GB, keep what development needs, delete the rest, update the docs,
  end. Nothing tracked changed but these docs; **main** = `wip/s27-int` (sim **108**), the docs commit after 872cb2c, fast-forwarded as before.
- **Measured before**: the folder 38 GB, drive C: 94 % full (29 GB free of 477 GB). `tc2/out/logs` 17.5 GB (three `unity-shots-2026-10-09T09-*` logs,
  6.3/6.2/6.1 GB, one URP assertion `'std::abs(det) > FLT_MIN'` with its stack every frame of `ShotRunner.Save`), `tc2/out/civ` 11.4 GB (2,864 saves,
  sim 33-108; sim 108's 13 = 0.11 GB), `tc2/out/shots` 1.8 GB, `tc2/out/webtest` 1.1 GB, `tc2/out/scale` 0.27 GB, TC1's `out/` 2.3 GB, Unity's `Library`
  2.1 GB, `tools/bob` 0.2 GB; tracked files 51 MB, `.git` 91 MB. 70 worktrees `C:/Game Dev/tc-*` 3.9 GB.
- **Measured after**: **the folder 4.2 GB; drive C: 68 GB free (86 %)**. `tc2/out/logs` 62 MB, `tc2/out/civ` 529 MB, `tc2/out/shots` 261 MB,
  `tc2/out/webtest` 494 MB, `out/` 147 MB; `git worktree list`: main, `tc-s27int` and the seven unmerged (`tc-g4`, `tc-s24m`, `tc-sgtally`, `tc-siege`,
  `tc-statesizes` - one uncommitted change, `tc-sworn`, `tc-tribute`).
- **What was deleted and kept**: DECISIONS D2-198 lists it (kept: sim 107/108 saves incl. `s2_ac5/world_y300|y600|y1000`; the older year-300 saves held
  jobs name - `s2_a7`, `s2_k0`, `s2_sx1`, `s2_lp1`, `s4_a7`, `s4_sx1`; every run's `.txt`/`.csv`; `s26*`/`s27*` shots and webtests incl. `s27prof1`;
  `tc2/out/tools`, `tc2/out/cc`; Unity's `Library`; TC1's `out/audio_cache`, scripts and text; `dist/`). Removed worktrees' branches are all still in git.
- **The queue**: untouched - still stopped (`$Q/stop`), `0_qb_web_s27prof2` held in `$Q/hold` with the older held jobs; `$Q` lives in session 24's
  scratchpad under `%TEMP%\claude` - do not clear `%TEMP%\claude` wholesale.
- **Next**: exactly as "Session 27, context 4" below ("Next, in order" 1: run the held `0_qb_web_s27prof2`).

## Session 27, context 4 (26 Oct 2026, from 16:40 IST; Control Room run #81 session 4) - the frame by function; the night from above; the regions' edges; the palisade faced; the pages' words

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; every Unity log "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs. **A Web build takes the
  machine from ~60 % to ~96 % of its memory** (Unity, its import worker, IL2CPP, Burst, then wasm-opt at 1.4 GB): Web build jobs gate on `GR=62 gate`
  before starting; nothing else heavy beside one.
- **The queue's runner survived the harness's stop** (session 27, context 3: the task shell was stopped, `runq.sh` pid 6256 and the q6 job lived on and
  finished; its jobs gate on RAM < 86 %). It is the runner in use; `Q` as before; follow with a Monitor on `tail -n 0 -F $Q/runq.log | grep -E "^end |^start "`.
- **main** = `wip/s27-int` = d4057ed (sim **108**; worktree `C:/Game Dev/tc-s27int`; fast-forward main between Unity jobs).
- **This context's slices (presentation and the book; on main)**: D2-196's frame numbers filled (s27perf4: the terrain paused x0.966 region / x0.871
  village, was x0.589 / x0.660; the colouring running x0.926 / x0.962); the night from above (river by night as the shallows, the lights amber in linear
  light with a town's glow from the lights' mips, the old glow sprite over villages retired, the sea's moon path broken by the wind); the book holds the
  screen (no card or page over it); the pages' words (founding party, homes counted once, crafts by their best hand, "at home" once, the first household's
  following without zeros); **the frame by function** - `realmsperf --profile MS` + `unity.mjs build-web --symbols` + `tc2/scripts/cpuprof.mjs` (re-summarise a
  saved .cpuprofile: `node tc2/scripts/cpuprof.mjs F.cpuprofile --frames N`): the main thread busy 99.9 % (the CPU's frame); fixes built from it (the ground
  remembered for the day, people on the drawn ground `TerrainLod.DrawnRadius`, the hover near the pointer, RoadAt, roads laid only on change, instanced
  arrays by size, names on repaint only); **the palisade faced in the village's frame** (PlaceFaced - the beach "slabs" were stretches turned up to a right
  angle off the ring; `place --ring` showed every dry stretch standing; D2-196's shore two-pass was not the cause); the regions' warp made to reach.
  D2-197 written (its pending numbers: s27prof2, the shots s27w1-s27w4).
- **Measured**: the running world's heap plateaus (637 MB loaded, 832 MB after 15 s, flat for 4 min: `s27heap1`); Web `s27web6` 9/9 (e748638), `s27web7`
  9/9 (ecd2f16); GazetteerTests 3/3, BiographyTests 4/4, LeadersTests 8/8, HistoryBookTests 7/7 (`s27c4`).
- **Judged at the end (17:45)**: `s27w4` - the palisade one continuous run of stakes down the beach (the fix: PlaceFaced); `s27w1`/`s27w2` - the regions'
  staircase gone at the village view, still a little jagged at the region view (open); `s27w3` - the night sea's moon path broken but still a broad pale veil
  from the region view (open). All shots 0 shader errors, on the integrated Radeon.
- **SESSION ENDED at Arjun's word (26 Oct, ~17:50 IST)**: the queue's runner was told to stop after `0_qa_shots_s27w` (`$Q/stop`; it ended rc 0 at 17:39 -
  main's headless rebuilt, GazetteerTests 3/3, LeadersTests 8/8, 0 skipped; `runner stopped 17:39:14`); **`0_qb_web_s27prof2` is HELD** in `$Q/hold` (not run: the frame's
  fixes are not yet measured). Next session: `rm $Q/stop`, start the runner (`bash tc2/out/tools/s23/runq.sh` in the background, only with memory to spare),
  `mv $Q/hold/0_qb_web_s27prof2.sh $Q/todo/`.

### Next, in order
1. Run `0_qb_web_s27prof2` (Web build d4057ed+ with symbols, webtest `s27web8` must be 9/9, `s27prof2` profiles, bookshots `s27book7`). Read the profiles
   with `node tc2/scripts/cpuprof.mjs tc2/out/webtest/s27prof2/prof_VIEW_STATE.cpuprofile --frames N` (N from that folder's report.json `profiles`) and
   compare with `s27prof1` (RebuildPeople 29 ms region / Hud_HoverTag 11.7 / TerrainLod_RoadAt 8.8 / writeBuffer 4.5 / WaterView_BuildRoads 3). Check
   `s27book7`: no card over the open book, the hover tag still found, houses coloured (the instanced arrays now go by size: 16/64/256/1023 - if any
   instanced thing draws black or wrong, that change is the suspect: Instancer.Draw). Fill D2-197's "Pending" and 25 §8.
2. The frame by the new profile: the draws' submission (bind groups, constant buffers, shadows), RebuildPlaces, Presence.Of/PositionAt in RebuildPeople.
3. Open looks: the region view's slightly jagged region edges (a wider colour blend far off?), the night sea's broad moon veil, the person page's sparse Now
   tab; keep judging A-E in real shots and Web builds.

## Session 27, context 3 (26 Oct 2026, from 14:52 IST; Control Room run #81 session 3) - prospecting LANDED (sim 108); the terrain's branches; rivers, ages, the meteor, regions, paper pages, night clouds, the glint, lake roads

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; every Unity log "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs.
- **main** = e748638 at the handoff (16:45), `wip/s27-int` = its tip (sim **108**); a background waiter (task `b2qjknhr7`) fast-forwards main to
  `wip/s27-int` and reverts ProjectSettings.asset once q6's Web build exits - check `git log -1 main` equals `git log -1 wip/s27-int`, else do it by hand. `wip/s27-land` (worktree `tc-s27land`) is merged and done; keep working in `tc-s27int` and fast-forward
  main between Unity jobs (`git -C "C:/Game Dev/tiny-consequences" merge --ff-only wip/s27-int`; never while a Unity job holds main's checkout).
- **Landed**: C5.3c prospecting (D2-190 written; 25 §8 updated). Landing checks `N_d` and `0_q1` (Prospect, Mining, Iron, OreSites, Forge, Determinism,
  SaveLoad, HistoryBook, Chronicle 4, Powers 13 - 0 failed, 0 skipped). Sim-107 saves (`s2_ac4`) no longer load in main's binaries: use `s2_ac5`.
- **This context's slices (presentation and the book; on main)**:
  - **The terrain's cost** (6cde55f): Terrain.shader's blocks run only where they draw - the woods block where `i.wood.x > 0.002` (inside it the near crowns
    only beyond 300 m and under 3 m a pixel, the far cover past 1.3 m a pixel, the turned cover and the tile's only where they show, StandRank where it
    steps), the finer detail within 3.5 km / 400 m, the night lights by night and far; every sample inside a branch takes gradients from outside
    (`SAMPLE_TEXTURE2D_GRAD`, `TriG`), the crowns' bump derived after the branch (`hm`, `bumpK`). Bridge `terrainbranch` (off = every block everywhere,
    as before; global `_TC2Unbranched`) and `terrainshadow` (`TerrainLod.PatchShadows`). **Not yet verified in Unity or measured** - jobs `0_q3`, `0_q4`.
  - Rivers far off their mean colour with the sky (River.shader `farK`); the meteor heard falling (AudioView.MakeFall: noise through a band 2.6 kHz -> 180 Hz,
    verified as a spectrogram in Python; landing sounds held until the cast stone lands); ages that share a name named by the realm that led them
    (`Chronicle.Eras(under:)`, `HistoryBook.Ctx.Leading`; "The Warm Years under Raimstead"; the age's opening names that realm and the one at its end;
    ChronicleTests 4/4 with a new test, HistoryBookTests 7/7); the meteor's dust grey-brown, smaller, fire-lit only low (VolcanoView FxKind.Stone) and one
    fall (`AimView.Thrown`: VolcanoView skips its own fall for a thrown stone and starts its flash when that stone lands); the ground's regions without the
    grid's staircase (`TerrainLod.WarpCells`: the cells' shares read ~half a cell off along a 450 m noise, never across the shore); the person and place
    pages as leaves of the book (`Hud.PaperPage`, `Dated`: years in the margin, rubric labels, IM FELL; bookshots `--pages person:N,place:N`); night clouds
    lit by the moon only (Clouds.shader, `_TC2MoonDir`, `_TC2NightLift`); the far glint broken by the wind's fields (Water.shader); no road over a lake
    (WaterView.BuildRoads - the "faint straight line through Raimstead" was a worn way along a grid column, straight for km, crossing the lake).
- **The queue** (runner `bbgjl1nqj`, `Q` as before; follow `tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |^start "`): `0_q2_ac5_s2` (seed 2 on
  sim 108 to 1,000 years, snaps 300/600/1,000 in `tc2/out/civ/s2_ac5`, the year-1,000 book `tc2/out/tools/s27/book5_s2_y1000_ac5.md`; touches `$N/ac5_ok`),
  then `0_q3_shots_s27d` (Unity shots on main: `s27d1` region/village/orbit/region with clouds/orbit@21 at y300; `s27d2` forest:broadleaf/mixed/boreal and
  forestfar; `s27d3` y1000 region/village; `s27d4` the meteor at the village/low views - **grep "Shader error"**), then `0_q4_web_s27web5` (Web build of main,
  webtest `s27web5`, realmsperf `--layer terrainbranch` and `terrainshadow` into `tc2/out/webtest/s27perf3/`, bookshots `s27book5` with `--play
  --meteor-play --pages ...`). Revert `ProjectSettings.asset` after the build.

- **Judged since (16:15)**: `s27d1`-`s27d4` (sim 108 saves, main 6d0d379..d48eb74): **0 shader errors** in every run (the branched Terrain.shader, Clouds,
  Water, River compile in Unity; the Web build of 836d6a9 compiled them for WebGPU too); no seams where a block starts; night side dark with the moon's
  clouds only; the region's river a light line; the glint broken into patches (still a little hazy). Found and fixed after: the pools laid dark water over
  the snowy boreal woods and fields' edges (836d6a9: only where the wood stands, never under snow); the village view's river still a dark teal (a647a8e:
  the shallows' tone); the palisade as separate dark slabs (6d94a8c: seven stakes over 2.1 m, weathered); the night lights as blobs (4d8b572: 512 a face,
  R8); the console's twelve warnings (de61f38). Also on main: Your Actions folds a cast's dead (654b79e, PowersTests 13/13), the person page in words
  (fe38185, d48eb74; BiographyTests 4/4).
- **Measured (s27perf3, Web of 836d6a9, s2_ac5 y300, paused, paired)**: terrainbranch x1.022 region / x1.063 village (the pixels are not most of the
  terrain's cost); terrainshadow x1.03 / x0.954; shadows (all) x0.928 / x0.930; buildings x0.838 / x0.877. **Found**: every terrain measure had been taken
  paused while the ground's colouring cycle ran on (every cell's colour + a patch or two a frame recoloured and re-uploaded, every 2.5 s, for ever) -
  6ae81de: a new cycle only when the world moved (time or a new record); the warp on a 9x9 lattice over fine patches; bridge `recolour`; realmsperf
  `--speed N` (5ae... 5460fb4). **s27web5's one FAIL** was the webtest's reference probe from a stale sim-107 headless binary in main's checkout: the
  browser's sim-108 probe = a sim-108 headless binary bit for bit. **Rule**: rebuild main's headless (`tc2/headless/app`) before a webtest after a sim
  version change (q6 does).
- **Judged since**: `s27book5`/`s27web5` paper pages (fixed after: until/due in words, crafts by hands, zero clauses, the year margin 88 px, the raised
  initial only on a page's first letter-led paragraph, Your hand hidden under a page, 'one lightning strike'); `s27e1` (pools fixed: clean snow; the
  village's river light blue), `s27e2` (night lights: no visible change from orbit), `s27e3` (palisade still slabs on the beach - **the cause was the
  shore**: `Shore.Dry` along the waterline flips dry/wet stretch by stretch; 5c62ace two passes: fill a lone wet stretch, drop a lone dry one).
- **Queue now**: `0_q6_web_s27web6` (headless rebuilt; Web build of main e748638 from 16:34; webtest `s27web6`; `s27perf4/terrain.txt` paused and
  `recolour_running.txt` at 1 h/s; bookshots `s27book6`), then `0_q7_shots_s27f` (main must hold 5c62ace: ff main after q6's "web build exit"; `s27f7`
  low/close - the shore palisade). D2-196 draft: `tc2/out/tools/s27/d2196.md` (its Evidence's TODO: `s27perf4`).

### Next, in order (at the handoff, 16:45)
1. Read q6 (`$Q/log/0_q6_web_s27web6.log`): webtest `s27web6` must be 9/9 (its reference now rebuilt); `tc2/out/webtest/s27perf4/terrain.txt` (the
   terrain paused, after the colouring fix - compare x0.589/x0.660 of `s27perf2`) and `recolour_running.txt` (the colouring's cost at 1 h/s); `s27book6`
   (`page_*`: words, margin, no Your hand over them; `play_meteor_*`). Fill D2-196's pending frame numbers in DECISIONS; 25 §8.
2. Read q7 `s27f7` (low/close at seed 2 y300 village 4): the shore palisade whole; if gaps remain, runs of two wet points - widen the fill.
3. The frame, by what `s27perf4` says: if the terrain is still a large share paused, its draw count/vertices (patches ~300 x 2,300 triangles, the
   shadow and any depth pre-pass) or the soft-shadow taps; if the colouring costs a running world much, spread it thinner (fewer patches a frame) or
   cache per patch what does not change with time (the warp, the villages near).
4. Then by the shots: the night lights from orbit (512 a face showed no visible change), the sea's glint (still a little hazy at the region view).

## Session 27, context 2 (26 Oct 2026, from 12:12 IST; Control Room run #81 session 2) - the reader and E merged; marks, flags, standing, the meteor's fall and scar, snow on the boughs, the timeline's eras; Web s27web2

### Exact state (keep current)
- **The machine**: never draw on the RX 5600M (gpusafe.mjs; every Unity log "AMD Radeon(TM) Graphics (ID=0x1636)"); silent runs.
- **main** = `wip/s27-int` (worktree `C:/Game Dev/tc-s27int`, the integration branch of this context; main is fast-forwarded to it between Unity jobs):
  (`git log -1 wip/s27-int`; main runs one fast-forward behind while a Unity job holds its checkout). Merged: the open-book reader (wip/chronicle abf6eb7) and E (wip/feel 3e8ae68), then this context's slices (all presentation or
  observers; sim 107 unchanged):
  - the aim's mark renewed each frame (never frozen); sized for the eye, not the reach; houses under the pointer/chosen marked round their footprint in
    their household's colour with the family ringed (`AimView.MarkAt`, `WorldObjectsView.PointedHome/HouseholdColour`); map names fly a flag in the
    polity's colour, a seat with a gold head; the top bar measured from its words (two rows on narrow screens);
  - `Biography.Standing` (observer, SwornTests): "first of X, lord of N villages, paying Y" / "sworn to ..." / "of the household of ..." on the hover tag
    and the person page; a home's tag says "the first household of X";
  - the book: `HistoryBook.Gloss` (a great founding says "today a town of N people"); the timeline's ages lit/named under the pointer, a click opens the
    age's chapter;
  - the planet: cumulus of many sizes (CloudNoise sizes, a 16-cell lattice) with lumpy outlines (TC2CloudForm); cloud shadows skipped at strength 0
    (bridge `off/on cloudshadow`);
  - E: flames red-orange with a small heart (Puff.shader, judged twice); the meteor's fall (a burning streak to the cell's heart, burst + shake on
    landing) and its scar from the `MeteorStruck` record (dark bowl 20 m, pale rim, rays to 75 m, fading over 200 years: TerrainLod `_impacts`);
  - W-FOREST: snow lies on the boughs' upper faces (Instanced.shader `_Anim < 0`, `TerrainLod.SnowOn`) - evergreens dark under snow; shrubs turn with
    the trees; no green crowns over snow;
  - standards enlarged at most 2.2x and the enlarged ones cast no shadow (the 100 m shadow bands of s27q1); shots view `chosen`/`chosen:low`.
  - after Web s27web2: the date in the UI type; the book's foot clear at 720 px; flags only for a realm's villages; an age's great events at most three
    of a kind; the aim's reach measured to a village's buildings; "Your hand" sized to its lines; power tooltips (drawn last); the pages above the power bar;
    the inspector a card in words and the pages' histories dated as the book (BiographyTests); "N sick" / "famine" on the map lines; the opening in the
    book's type; the air's march halved over the ground; bare crowns wherever snow lies; lumpy, soft cumulus. (The two long "shadow" bands of s27q1/q3 are
    field boundaries in perspective - not a fault.)
- Tests: integration runs `s27i` (HistoryBook 7, Powers 13, Gazetteer 2, DayAir 2, Determinism) and `s27j` (Sworn incl. Standing, Biography, Leaders,
  HistoryBook) all passed, 0 skipped.
- Tools (scratchpad of this context `5e178fba-...`): `gcommit.sh` - a gated commit (cc of the worktree, commit only when Game/Editor/Tests compile).
- **The frame on the integrated GPU** (`tc2/out/webtest/s27perf/*.txt`, Web s27web2, seed 2's year 300 - 11,272 people, 54 places - paired in one page):
  12.4 fps at the region view (6 km), 8.4 at the village view (1.4 km). Buildings x0.38 / x0.61 (drawn and cast), shadows x0.64 / x0.50, air x0.90 /
  x0.92, the villages' trees - / x0.89, people - / x0.91; clouds and cover ~1. Shadow set-ups against 4 cascades x3.2: 4 x1.6 -> x1.02/x1.02 (reach is not
  the cost), 2 x1.6 -> x1.12/x1.29, 2 x1.0 -> x1.22/x1.29, 1 x1.0 -> x1.25/x1.51 (each cascade is a pass over every caster). Built from it: villages beyond
  2.2 km of the camera without their homes' small details and casting no shadow (homes and yard trees: `WorldObjectsView.DetailFar`, bridge `fardetail`),
  and the light shadows graded by height and made the default (`Sky.ShadowLite`: 4 cascades below 300 m, 2 below 900 m, 1 above). Measured on s27web3
  (`s27perf2`): region ~30 fps (was 12.4), village ~16 (was 8.4); fardetail x1.89/x1.14, the graded shadows x1.06/x1.31, cloud shadows ~1, names ~1;
  **the terrain x0.59/x0.66 - the next cost** (Terrain.shader: canopy, plots, cover per pixel). D2-195 written.
- **The queue at the handoff (14:49 IST)** (runner `tc2/out/tools/s23/runq.sh`, background task `bbgjl1nqj` - reuse it, never start a second;
  `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`; follow with
  `tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |runner stopped"`): running **`N_d_land_tests`** (the landing worktree `tc-s27land`: build,
  floatcheck, cc, ProspectTests, Mining, Iron, OreSites, Forge, Determinism, SaveLoad, HistoryBook - passes when `$N/s27land_ok` exists and its log ends
  "s27land ok"); then **`P_a_shots_s27c`** (Unity shots on main's checkout: `s27p7` seed 42 region/orbit - the soft lumpy cumulus; `s27f6` seed 2 mixed
  woods - bare crowns over snow; `s27e8` a meteor's fires and the capped smoke at the village/low views), then **`P_b_web_s27web4`** (the Web build of
  main as it stands when it starts: webtest `s27web4` must be 9/9, bookshots `s27book4 --play --meteor-play`). Never edit main's checkout while P_a or P_b's build runs
  (work in `tc-s27int`); P_b's webtest/bookshots run from the built files, main is free again once its log says "web build exit 0".
- **Prospecting's compare (14:47) is inside the noise - LAND IT**: ten seeds `_pro1` against `_rui1` (`tc2/out/tools/s26/results/cmp_rui1_pro1.txt`):
  people x1.02 (t 0.38), hunger a person-year x0.73 (t -1.29), famines x0.76 (t -1.48), raids x1.13 (t 1.38), walls x1.14 (t 1.81), loot x1.27 (t 1.74),
  births x1.00, deaths x1.00; seeds with metal looked 2,836-7,464 seasons and found 5-115 bodies within reach (s4, s2, s7, s11); the five without metal
  never looked. Write D2-190 from it.
- **Prospecting** (`wip/prospect` 85acede, sim 108): merged onto main's tip in the landing worktree **`C:/Game Dev/tc-s27land` (`wip/s27-land` 142ffc5)**; its
  tests `N_d_land_tests` run after the compare `N_c_cmp_pro1` (`tc2/out/tools/s26/results/cmp_rui1_pro1.txt`). **Land only after `P_a_shots_s27c` and
  `P_b_web_s27web4` finish** (they load sim-107 saves): then fast-forward main to `wip/s27-land` (merge `wip/s27-int` into it first if it moved), write
  D2-190, and regenerate the shots' saves with the sim-108 binary (seed 2 to years 300 and 1,000 as `s2_ac4` was: `tc2/out/tools/s26/make_ac4.sh` shows
  how) before any later shots or bookshots job. Seeds done: s4 (7,464 seasons gone looking, 115 bodies found within reach), s5, s1, s6 (no metal), s11
  (2,836 / 5), s2 (7,174 / 18, 6 scouted).
- **Decisions written this context**: D2-192 (people), D2-193 (the book), D2-194 (the powers), D2-195 (the frame). Still to write: D2-190
  (prospecting) - from the compare above, when it lands.

### Next, in order
1. `N_d_land_tests` must pass (`$Q/log/N_d_land_tests.log`). Judge `P_a`'s shots (`tc2/out/shots/s27p7`, `s27f6`, `s27e8`) and `P_b` (`s27web4.log` 9/9,
   `tc2/out/webtest/s27book4/play_*`: the inspector card in words, "N sick" on the map, the opening in IM FELL, the meteor's smoke no longer a pink sky);
   `git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset` after the build; fix what the pictures say (in `tc-s27int`).
2. **Land prospecting**: when P_b's build has ended, merge `wip/s27-int` into `wip/s27-land` if it moved, `git merge --ff-only wip/s27-land` on main
   (sim 108), write D2-190 (the compare's numbers are above), update 25 §8 (prospecting built). Then regenerate the shots' saves with the sim-108 binary
   (seed 2 to years 300 and 1,000 like `tc2/out/civ/s2_ac4` - see `tc2/out/tools/s26/make_ac4.sh` for the run's flags; a new tag, e.g. `s2_ac5`) and point
   later shots/bookshots/realmsperf jobs at them: sim-107 saves no longer load.
3. **The terrain is the next cost** (x0.59 region, x0.66 village: `tc2/out/webtest/s27perf2/terrain.txt`). Plan: in `Terrain.shader` the woods block
   (from "the woods (D2-079)") runs for every pixel - 6 `Crowns` + 9 `CoverAt` samples and the stand rank - because implicit-derivative samples must stay
   in uniform control flow on WebGPU. Compute `ddx/ddy(i.positionWS)` and `pxs`/`pxw` before it, pass each projection's uv gradients into `Crowns`/`CoverAt`
   as `SAMPLE_TEXTURE2D_GRAD` (the `lt` offsets keep the gradients; the turned `mul(turnB, uv) * 0.413` scales them), then wrap the block in
   `if (i.wood.x > 0.002)` - with no wood it leaves the albedo as it was (dl 0: litter = albedo, mask 0, fc 0; the stand step keeps dens 0). The same
   for the plots/furrows block where `i.farm` is 0. Verify: Unity shots (no "Shader error"), a Web build (WebGPU compiles it), `realmsperf --layer terrain`.
4. Then by the shots: night clouds faintly grey and the sea's broad sheen (A), age names that repeat (C), the faint straight line through Raimstead at the
   village view (`s27q1`/`s27q3`: a worn way or a seam - check), a sound for the meteor's fall (E), the person/place pages in the book's paper style.

## Session 27 (26 Oct 2026, 10:40 - ~12:10 IST; Control Room run #81 session 1) - HANDOFF: the fourth acceptance finished; the player-facing slices A (planet), B (people), C (book) on main, E (powers' feel) on a branch; the queue RUNNING

### Exact state (verified at the handoff, ~12:09 IST)
- **The machine: never draw on the RX 5600M** (it restarted the PC at 10:45 during Unity shots). `tc2/scripts/gpusafe.mjs` (called by `unity.mjs` and every
  browser script) sets Unity and Playwright's Chromium to the power-saving GPU and refuses to start while the card is present; after the restart the card
  stands "phantom". Every Unity log since: "AMD Radeon(TM) Graphics (ID=0x1636)". All runs silent (`--mute-audio`, the listener at 0 in shots, the game pauses
  its sound when not in front).
- **main** (e0c8066, sim 107 - no simulation rule changed this session): the fourth acceptance's verdict (25 §6), the docs ledger (25 §8), and merged:
  **A the planet from orbit** (slices 1-1c, D2-191: clouds read four ways from the weather - `PlanetSystems.DayAir` an observer, `DayAirTests` 2/2 -, a 3-D
  noise's forms `CloudNoise`/`TC2CloudForm.hlsl` with their shadows on ground/sea/objects, the night side dark from orbit `Look.NightFar`, the homes' lights
  `LightsView`, no speckled glint, `Look.AtmoGround` 0.75; shots `s27p1`-`s27p3` judged); **B people recognisable** (household liveries, `person_lord` mantle +
  circlet, `person_sworn` cap + shield from `tc2/art/kit.py --only person_lord,person_sworn`, family rings `WorldObjectsView.Chosen/Pointed/ChosenHome`, shots
  `--choose head`; `s27q1`: sworn men read as a retinue in livery); **C the book's content** (`BookBlock.Weight`/`HistoryBook.Weigh`, age stories and notes
  `BookKind.Note`, part IX Discoveries and Works, Your Actions now part X `HistoryBook.YourActionsPart`; the book's fonts EB Garamond + IM FELL English
  under `Game/Resources/Fonts`, OFL texts in `tc2/art/fonts`, CREDITS). HistoryBookTests 7/7, PowersTests 13/13, Unity C# compiles (`unity.mjs cc`).
- **NOT on main yet (merge after the running Web build)**:
  - `wip/chronicle` **abf6eb7** (worktree `C:/Game Dev/tc-chronicle`): **the book's reader as an open book** (`Game/UI/Hud.Book.cs` + `Hud.cs` wiring: paper
    leaves, gutter, IM FELL titles, raised initial, years in the margin, weights, notes, folio; the timeline's legend/people line/axis fixed). It was left
    uncommitted when `wip/chronicle` was first merged (b2370ae), so main has the book's content but the OLD reader.
  - `wip/feel` **3e8ae68** (worktree `C:/Game Dev/tc-feel`): **E the powers as play** - fire as tongues of flame (Puff.shader flame mode, `Puffs.Flame`;
    the cones gone), the aim marker on the ground (`AimView` + `Overlay.shader`: a disc in the power's colour, a breathing band) naming the villages in reach,
    a burst + camera shake on casting, **Your hand** (the last three casts and what came of each, live: `HistoryBook.CameOf`, tested in PowersTests), the
    power bar with colours and recharge bars, **the HUD skin** (dark glass panels with a gold edge, gold-warming buttons, the date in IM FELL); shots
    `--aim R`. PowersTests 13/13, Unity C# compiles. Never shot or seen in the Web build yet.
- **Running: the job queue** (runner `bash tc2/out/tools/s23/runq.sh` started 11:11, `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-
  consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`; the session's background task `bbgjl1nqj`, a Monitor `baxiwgpkg` on `$Q/runq.log`).
  Now: `0_r1_web_s27web1` (the Web build of main b2370ae from 12:00, then `webtest --tag s27web1`, then `bookshots --tag s27book1` with parts
  `-1,0:3,0:9,1:0,4:0,8:0,8:1,9:0,-2`; old reader). Next in `$Q/todo`: `0_q2_shots_people` (`s27q2`: low/standard/street/standard@18 at seed 2's lord
  village, `--choose head`), `0_s1_shots_forest` (`s27f1`: forest:rain/dry/warm/broadleaf/mixed/boreal/wet on seed 4 year 300), then prospecting's seeds
  `N_b_pro1_05..10` and `N_c_cmp_pro1`. Unity jobs use main's checkout: do not edit main's Game code while one runs (work in a worktree).
- **Prospecting (C5.3c, `wip/prospect` 85acede, sim 108)**: control `N_a` - annals `s4_proc.csv` = `s4_rui1.csv` byte for byte, Y300 identical; the state hash
  differs only because the new `Settlement.ProspectAt` is always hashed (0 under `--oldprospect`); marked OK in `$N/pros_ctl_ok` with that note. Seeds done:
  s4 (7,464 seasons gone looking, 115 bodies found within reach), s5, s1, s6 (no metal: nothing to look for). Read `results/cmp_rui1_pro1.txt` when `N_c` ends;
  land it (merge `wip/prospect`, D2-190) if inside the noise.
- **Working tree**: `ProjectSettings.asset` (`additionalIl2CppArgs: --jobs=2`) and `Assets/Resources/` (Performance Testing files) are the Web build's
  writes - revert the first (`git checkout -- unity/TinyConsequences2/ProjectSettings/ProjectSettings.asset`) when the build ends and never commit
  either; `.claude/launch.json`, `Assets/Resources.meta` are not ours.

### Judged this session (real shots)
- The fourth run's last shots: Reford's ring of stone with towers (seed 4, 1,000), Vaestdal's two rings (seed 7), seed 2's great rose realm and its war
  spears; the meteor's fires were flat yellow cones (fixed on `wip/feel`). Web `s26web1` 9/9, heap 870 MB at year 1,000.
- The planet (`s27p3`): Earth-like from orbit - shaded cumulus, heaped decks, shadows, dusk-red clouds at the terminator, a dark night side lit by places.
  Left: lights read as one warm blob a place; the region view's sea shows the sun's broad sheen as a pale patch; night clouds faintly grey.
- The book's old reader (`s26book1`): a parchment panel with equal-weight lines and grey buttons (the new reader is abf6eb7).

### Next, in order
1. When `0_r1` ends: read `tc2/out/webtest/s27web1.log` (must be 9/9) and `s27book1`; revert ProjectSettings.asset. Then merge `wip/chronicle` (abf6eb7) and
   `wip/feel` (3e8ae68) into main (`git merge --no-ff`), `unity.mjs cc`, and queue: feel shots (`--load s2_ac4/world_y300.tc2 --village 4 --meteor 90 --aim 900
   --views close,village` for flames and the aim marker; a lightning `--views close` at night for the flames) and a Web build `s27web2` with bookshots of the
   new reader (`--book "-1,0:3,0:9,1:0,8:0,9:0,-2"`). Judge, fix, commit; D2-192 (people), D2-193 (the book), D2-194 (the powers' feel and the HUD).
2. Read `s27q2` (families, the head's mantle up close) and `s27f1` (W-FOREST: which kinds still read alike) - then W-FOREST's next pass where the shots say.
3. Prospecting's compare -> D2-190 and land.
4. Then: the book's remaining polish (portraits of great lives? the timeline's eras), the map names with a flag in the realm's colour (Hud.MapNames: a
   small square of `WorldObjectsView.PolityColour` beside the dot), hovering a house rings it and its family (`WorldObjectsView.Pointed` for buildings).

## Session 26, second context (26 Oct 2026, from 23:43 IST 25 Oct; Control Room run #80 session 2) - stone walls, the memory pass, the ore, the household, the cull and warmth, ruins reused, the settings and the realm maps LANDED (sim 107); the fourth acceptance RUNNING

### Exact state (keep current; ~02:25 IST 26 Oct)
- **main** (sim 107, 27e1bfc): landed this context - **C7.2 stone walls** (f95b233, D2-183), **the book's bonds end with an abandoned village**
  (77a5bae), **the memory pass** (8086ced, D2-184), **F9.6-F9.7 the ore and bounty** (fd4eae6, D2-185), **C10.11 the lord's household** (f176a84, D2-186:
  its one lean - the greatest realm x0.69 - was chaos by its matched placebo `_hlp`), **F9.8-F9.9 the cull, bringing, warmth and cold** (ecedf12, D2-187),
  **C13.4 ruins reused** (bdc0305, D2-189: measured first by `place --ruins`; ten seeds inside the noise; ruins inside living villages -22 %), **F11.2 the
  player's settings** (885e540, D2-188), **the book's realm maps** (1825a61), tools `place --ruins`, bookshots' timeline name.
- **RUNNING: the fourth 1,000-year acceptance run** (generated 03:06 by `make_ac4.sh`; ~4 hours): `M_a` (main's cc, build `bin_m14`, full suite), `M_b` x3
  (seeds 2/4/7 `_ac4` with books at 300/1,000, `lordships --dependants`, `place --ways/--stonewalls/--ruins` at 300/600/1,000 into
  `tc2/out/tools/s26/accept/`), `M_c` (village #4 of seed 2 at 300/600/1,000, region also with ShadowLite), `M_d` (the villages most walled in stone, realms,
  the meteor, the god's ore), `M_e` (Web `s26web1`: the test - now 9 checks with the settings' reload -, the heap at year 1,000 against 955 MB, the
  `sortplaces`/`shadowlite` pairs, the book's pages `s26book1` with the realm maps). **Unity jobs use main's checkout: do not edit main's Game code
  while `M_c`-`M_e` run** - work in a worktree.
- Then: judge the run in the shots, books and measures; write 25 §6 "Fourth run" and the verdict on the finished core (25 §8); the frame pass by its
  pairs (make `sortplaces`/`shadowlite` the default if they help and read right).
- **05:14 IST: Claude Code reaped the queue's background shell for low system memory** (RAM 85 %; it fell to 53 % a minute later). The runner itself lived on
  and finished `M_c` (village #4 of seed 2 at 300/600/1,000: judged, 25 §6); I set `$Q/stop` so it **stopped at 05:17:23** - restarting it waits on the
  person's answer to Decision Inbox **D-1** ("may I restart the runner?"). Done: `M_a` (full suite **507/0/0**), `M_b` x3 (the three worlds, books and measures
  judged in 25 §6), `M_b0` (`place --ore`), `M_b2` (prospecting `pros2 ok`), `M_c` x3. **Left in `$Q/todo`**: `M_d` (stone/realms/meteor/ore shots),
  `M_e` (the Web build `s26web1`: the test with the settings' reload, the heap at 1,000, the frame pass's pairs, the book's pages `s26book1` with the
  realm maps), `N_*` (prospecting's control, ten seeds `_pro1`, compare). To restart once allowed: `rm $Q/stop` and run `bash tc2/out/tools/s23/runq.sh` in the
  background.
- **C5.3c prospecting** (`wip/prospect` 85acede, worktree `C:/Game Dev/tc-prospect`, sim 108; measured first by `place --ore`, cb36a43 on main): a village
  whose smiths lack ore looks for it twice a year - within its reach (the nearest body becomes known), else farther as a scout. ProspectTests 2/2 and six
  classes 0 failed. Its evaluation is the `N_*` jobs; then land it (D2-190).
- `K_b_ac3_s7` is in `$Q/hold` (superseded).
## Session 26 - HANDOFF (25 Oct 2026, 21:20 - ~23:25 IST; Control Room run #80 session 1) - rain, the harrying and the ways on main (sim 101); stone walls, the memory pass, the god's ore and the lord's household built, their checks RUNNING in the queue

### Exact state (verified at the handoff, ~23:22)
- **main** 45d4461 + docs (sim 101): **rain** (b9ab94e), **the harrying and the ways** (merge 36fad20; D2-180, D2-181 in DECISIONS), the flames on dry ground only
  (ee11391), shot tunable `ShadowLite` (4919446), bridge `sortplaces` (4928499, off until measured). **One full suite over everything landed: 484 passed, 0 failed,
  0 skipped** (`tc2/out/tools/s25/results/fullsuite_bw3.txt`); main's Unity C# compiles after the merge (`unity.mjs cc` 0 errors). Not ours: `.claude/launch.json`,
  `Assets/Resources.meta`. Old sim-98 saves (`_ac3`) no longer load in main's Unity: shots need sim-matching saves (the fourth run makes them).
- **Branches built, each carrying the one before, to land in this order** (each changes the sim version; land with `git merge --no-ff wip/<branch>` into main):
  1. **C7.2 stone walls** - `wip/c72-stonewalls` 395d5fb (worktree `C:/Game Dev/tc-stonewall`, sim 102; draft `tc2/out/tools/s26/d2183.md`). StoneWallTests 5/5 +
     eight classes 0 failed. Checks: `0_zh_a2_tests_sw1` (its classes on `bin_sw1`), `0_zh_b` (control `--oldstonewalls` = `_bw3`), `0_zh_c` ten seeds `_stw1`,
     `0_zh_d` (`tc2/out/tools/s26/results/cmp_hy1_stw1.txt` + each seed's `stonewalls:` tally). Land if the seeds hold (a lean past |t| 2: a matched placebo).
  2. **The memory pass** - `wip/memory` f7c9218 (worktree `C:/Game Dev/tc-memory`, sim 103; `tc2/out/tools/s26/memory.md`, draft `d2184_185.md`). MemoryPassTests 3/3.
     Checks `0_zi_a` (build `bin_mem1`, tests), `0_zi_b` (control `--oldcompact` = `_stw1`; **the pass on: annals = `_stw1` byte for byte** - it must change no
     history; state sizes at 300 `results/statesizes_s4_mem{0,1}_y300.txt`).
  3. **F9.6-F9.7 the god's ore and bounty** - `wip/ore` f66c50e (worktree `C:/Game Dev/tc-ore`, sim 104; draft `d2184_185.md`). PowersTests 9/9, Mining 6, Iron 8.
     Check `0_zj_a_ore1` (build, tests, uncast control = `_mem1` byte for byte). Played: `run --castore 100` on seed 4 - a mine by year 160.
  4. **C10.11 the lord's household** - `wip/hall` d07e0ae (worktree `C:/Game Dev/tc-hall`, sim 105; `tc2/out/tools/s26/c1011_household.md`). HallTests 5/5.
     Measured first (`results/deps_s4_y{300,600}.txt`): lords' surplus for 1,260-1,907 people's food a year beside 312-1,387 landless people in their realms.
     Two first looks on seed 4 (`_hal0`, `_hal2`; one seed - no evidence of effect): the first let 3,156 of 4,157 go at each change of first household -
     fixed (a new first takes them over: 2,832 taken over in `_hal2`); now they are let go mostly when the grain runs short (2,730 of 3,334 taken in; ~2
     household-years each; 50 kept at year 300; 2,960 summoned from realms to seats). Checks `0_zk_a` (build `bin_hall1`, 7 classes), `0_zk_b` (control
     `--oldhall` = `_ore0`), `0_zk_c` ten seeds `_hal1`, `0_zk_d` (`results/cmp_stw1_hal1.txt`, `lean_stw1_hal1.txt`; the memory pass and the ore change no
     annals, so `_stw1` is its baseline). Judge by people, hunger, the seats' size (`lordships --dependants` on `s*_hal1/world_y300.tc2`); D2-186.
- **Measured, not built**: C8.2 flight from war (`tc2/out/tools/s26/c82_flight.md`: at the ceiling no village within reach has land to spare); a warmth power
  (needs a temperature overlay inside `PlanetSystems.DayMean`, called very often).
- **A fault of this session in the queue, repaired**: a retagging `sed` (`_sw1` -> `_stw1`) also renamed `0_zh_a`'s binary dir to `bin_stw1` while its tests
  ran `bin_sw1` - `0_zh_a` ended rc=1; `bin_sw1` is now a copy of `bin_stw1` (tc-stonewall 395d5fb) and `0_zh_a2_tests_sw1` reruns its classes. Every other
  queued job's binaries and runs were checked consistent at the handoff.

### Running at the handoff (leave them; read their results)
- **The job queue runner**: `bash tc2/out/tools/s23/runq.sh` (this session's background task `bj4o96476`), `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/
  C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q` (the old session's scratchpad, still live; `lib.sh`'s `S`/`N` point there).
  Order (todo): `0_zh_a2` (stone tests), `0_zh_b`, `0_zh_c` x10, `0_zh_d`, `0_zi_a`, `0_zi_b`, `0_zj_a`, `0_zk_a`, `0_zk_b`, `0_zk_c` x10, `0_zk_d`, `K_b_ac3_s7`
  (the third acceptance run's seed 7, sim 98, `bin_m12`). ~2 hours in all. Held: `K_ba_siege_s47`, `N_a_ways_s2`. Watch: `tail -n 0 -F $Q/runq.log | grep
  --line-buffered -E "^end |runner stopped"`. A Monitor on runq.log (`be17dmo21`) may still be armed; it expires by itself.
- No server, Unity editor or Web build is running.

### Next, in order
1. Read each check as it ends (`$Q/log/<job>.log`, the results files above) and land in order: stone walls (D2-183: append `tc2/out/tools/s26/d2183.md` with
   the seeds' numbers), the memory pass and the ore (D2-184, D2-185 from `d2184_185.md` with the state sizes), the lord's household (D2-186 if its seeds hold;
   a lean: its matched placebo `--oldhall --nudge Y`). Merge main into each branch first if main moved (docs only so far); build after resolving any conflict.
2. Then the fourth acceptance run: `bash tc2/out/tools/s26/make_ac4.sh $Q` - main's build and full suite (`bin_m14`), seeds 2/4/7 for 1,000 years (`_ac4`) with
   books, the ways and stone walls by century, real shots (`s26a_*`: village #4 at 300/600/1,000 with `ShadowLite` beside, the villages most walled in stone,
   realms, the meteor's flames, the god's ore), the Web build `s26web1` (the test; the heap at year 1,000 against `s25web5`'s 955 MB; `sortplaces` and
   `shadowlite` paired). Judge in the shots and books; write 25 §6 "Fourth run" and the acceptance's verdict on the finished core (25 §8).
3. Then: the frame pass by the Web measures (make `sortplaces`/`shadowlite` the default if they help and read right); F9's remaining powers and F11.

## Session 25, fourth context - END (25 Oct 2026, ~17:45 IST; stopped at Arjun's word) - main (sim 98) clean of ours; the queue stopping after rain's checks

### Exact state
- **main** (61da54f and the docs after it), **sim 98**, nothing of ours uncommitted (not ours: `.claude/launch.json`, `Assets/Resources.meta`). Landed this
  context, no sim change: **the frame pass** (people beyond the limb or outside the view unposed; villages beyond the limb unbuilt), **the streamed save**
  (the heap at year 1,000 -243 MB), **each save answered by its own completion** + the bridge's `saves` (the Web test reloaded on a stale `saveOk` - the
  harness, not the game), the power's aim only over open ground, `closepages`, the tools `run --statesizes`, `saveprobe.mjs`, the bridge's `peoplecull`,
  `placescull`, `shadowlite` (off by default); Unity's .meta files (the 71 sounds, Hud.Powers, Hud.Title, AudioView). **D2-182 appended.** **Web `s25web6` 8/8.**
- **Measured and set aside: C8.1b the siege** (`tc2/out/tools/s25/siege/design.md`: no band the walls held off could stand a sally, in any world measured).
- **Branches ready to land, in order** (each carries main; each changes the sim version):
  1. **F9.5 the rain** - `wip/rain` 1112a4d (worktree `C:/Game Dev/tc-rain`, sim 99): its checks `0_ze_rain_rn1` were RUNNING at the stop (from 17:43):
     read `$Q/log/0_ze_rain_rn1.log` - land with `git merge --no-ff wip/rain` if it says `rn1 ok` and `seed 4: F9.5 uncast = _pv1 byte-identical`.
  2. **C8.1a the harrying** - `wip/harry` 8bb3dca (`C:/Game Dev/tc-harry`, sim 100): its evaluation `0_zf_*` waits in `$Q/todo` (build, control vs `_rn1`,
     ten seeds `_hy1` against `_bn1`, `results/cmp_bn1_hy1.txt`); land after rain if the ten seeds hold (a lean past |t| 2: a matched placebo or a
     mechanism); write D2-180 (rain + the harrying + the siege set aside).
  3. **C11.1b a people's ways** - `wip/c111b-ways` 1069a6d (`C:/Game Dev/tc-bways`, on the old `wip/harry`, sim 101): verified (control `--oldhouseways` =
     `_hyr` byte for byte; ways on: annals identical; `ways/ways_s4_bw2_y300.txt`); merge `wip/harry` into it, rebuild, land; D2-181 from
     `tc2/out/tools/s25/ways/d2181.md` (open refinement: copy a lord only after the bond has stood a generation). `N_a_ways_s2` (1,000 years) in `todo`.
- **The queue** (`Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`, runner
  `tc2/out/tools/s23/runq.sh`): **`$Q/stop` is set** - the runner ends after `0_ze`. Left in `$Q/todo`: `0_zf_*` (the harrying), `K_b_ac3_s7` (the third
  run's seed 7), `K_ba_siege_s47`, `N_a_ways_s2`. To restart: `rm $Q/stop` and run `bash tc2/out/tools/s23/runq.sh` in the background (one heavy job at a time,
  RAM < 86 %, CPU < 85 %; Claude Code may stop idle background shells when memory is short - then check in the foreground).
- **Judged this context**: K_a (main's suite 473/0/0), J_a (war between realms, half broken on walls - `c81_design.md`), the third acceptance run (seed 2:
  the map's pattern set by ~300; seed 4: 65 -> 81 places over 300-990 - 25 §6), K_c/`s25web3` (layer costs), the shots of village #4 at 300/600/1,000
  (`tc2/out/shots/s25o_ac3_s2_v4_y*`: villages drawn to the limb with the culls on), the realms and the meteor/drought shots (`s25o_*`, not yet looked at).

### Verified at the handoff (~17:55)
- **Main's tip c47e9a3** built headless (`$N/bin_m13`): SaveStreamTests 3, SaveLoad 8, DeterminismTests 7, HistoryBookTests 5 - 0 failed, 0 skipped (the core
  changed by the streamed save after main's last full suite 473/0/0 on 9d3c1c2; a full suite on main is still due - queue one before the next landing).
- **Web `s25web6` 8/8** was built from d7e41e2 (all main's Game code but docs since); the frame pass is covered by it and by the shots `s25o_ac3_s2_v4_y*`.
- **Still running at the handoff: `0_ze_rain_rn1`** (from 17:43): build, floatcheck clean, cc 0 errors, PowersTests 7, DiseaseTests 7, BurnTests 2,
  HistoryBookTests 5 - all 0 failed; its remaining classes and the uncast control (= `_pv1` byte for byte) were running. The runner stops after it (`$Q/stop`).
- **Control Room's stale verifications, reviewed**: older milestones whose files changed since - `WorldObjectsView.cs` (the frame pass: compiled, Web 8/8,
  shots), `c81_design.md` (the war and siege notes appended: notes only), `make_c36g.sh` (a job generator; C3.6g landed and main's suites passed since) - none
  is a code change left unverified.

### Next, in order
1. Read `0_ze`'s log; land rain (sim 99). Restart the queue for the harrying (`0_zf_*`), land it (sim 100), D2-180; then C11.1b (sim 101), D2-181.
2. Look at the realms shot (the war spears edged pale) and the meteor and drought shots; judge `shadowlite` (x1.21 at the region view) in real shots.
3. The frame pass on: buildings are the largest layer (~60-67 ms on a year-1,000 world) - villages in front of the limb but outside the view, the draws by
   colour group, shadows (above). The memory pass on: the history ledger (64 MB of 107 MB serialized at year 1,000), dissolved households (68,066), the dead.
4. The acceptance's seed 7 (`K_b_ac3_s7`) and its write-up in 25 §6; why seed 2's map freezes after ~300 while seed 4's keeps changing.
5. Then F9's remaining powers (warmth, cull, bring) and F11 (packaging, settings).

## Session 25, fourth context (25 Oct 2026, from 14:10 IST; Control Room run #78 session 3) - main 31234dc (sim 98): the frame pass and two Web fixes landed; C11.1b built; the siege set aside

### Exact state
- **main 31234dc, sim 98** (not ours: `.claude/launch.json`, `Assets/Resources.meta`). Landed this context (no sim change): **the frame pass** (merge 3e3f64c of
  `wip/frame` b91a8c2: people beyond the limb or outside the view unposed - `WorldObjectsView.PeopleCull`; villages wholly beyond the limb unbuilt, rebuilt at
  once when the camera moves - `PlacesCull`, `PlacesReach` 2.5 km; bridge `on/off peoplecull|placescull`), **`SaveNow` unsets `LastSaveOk` while a save is in
  flight** (the reload test had loaded an older save), **the power's aim drawn only over open ground** + bridge `closepages` + the Web test closes the pages
  before it aims; Unity's .meta files for the 71 sounds (4a745a3) and for Hud.Powers/Hud.Title/AudioView (31234dc). Main's Game code compiled (`cc --src`);
  **not yet built for the Web** - job `0_w` does it.
- **Measured and set aside: C8.1b the siege** (`tc2/out/tools/s25/siege/design.md`; tool `run --siegetally` on `wip/siege-tally` ca6c3ec, main + a reading
  tally, worktree `C:/Game Dev/tc-sgtally`; results `tc2/out/tools/s25/results/siege_s*_ac3_y*.txt`): no band the walls held off could stand a sally by the
  walled village on open ground, in any world measured. `K_ba_siege_s47` adds seeds 4 and 7 (expected the same).
- **Built, in evaluation: C11.1b a people's ways carried and copied** - `wip/c111b-ways` 1069a6d (worktree `C:/Game Dev/tc-bways`, on `wip/harry`, **sim 101**;
  draft `tc2/out/tools/s25/ways/d2181.md` - update its rule text: Conform 0.6 replaced HeadCopy 0.3, BrideCopy 0.1 from another village only). Checks:
  `0_v` (first control) ran with the clashing `--oldways` (D2-160's) and differed - renamed `--oldhouseways`; ways on: annals = `_hyr` byte for byte;
  `0_y_ways_ctl2` reruns the control (`bw_ctl_ok`) and measures the revised rule (`ways/ways_s4_bw2_y300.txt`: want most households of a village in its way,
  tributaries' first households toward their lords); `N_a_ways_s2` (1,000 years, needs `bw_ctl_ok`) at the queue's end.
- **The queue** (runner PID 9632, `Q` as below; generators `tc2/out/tools/s25/siege/make_measure.sh`, `ways/make_ways.sh`): running `0_w_web_s25web4`, then
  `0_x_heap_s25web4` (the wasm heap loading seed 2's worlds of every age: engine vs per-person), `0_y_ways_ctl2`, `K_b_ac3_s4`, `K_b_ac3_s7`, `K_ba_siege_s47`,
  `K_d` shots (village #4 at 300/600/1,000), `K_e` realms, `K_f` meteor/drought, `L_a_rain_rn1`, `M_*` (the harrying), `N_a_ways_s2`.

**Update (~16:20)**: `0_w` (Web `s25web4`): the click casts; the culls x1.22/x1.57 (people) and x1.21/x1.51 (places) at region/village; with both the
year-1,000 frame ~108/126 ms (from ~155/225); buildings still x0.44-0.46 (~60-67 ms), shadows x0.53-0.69, people x0.87-0.92 (`14` §7). The reload still lost
the save - traced: `WebSaveStore` kept one answer slot a key, so an autosave's completion answered the test's later save; fixed (answers in order, `saveOk` set
by the latest save only) and **the save streamed both ways** (`wip/savestream`, SaveStreamTests 3/3; seed 2's year-1,000 save loads and lives two days to main's
hash) - both on main (8a83dd7, no state change). `0_x` the heap by age before it: 247/428 MB at year 50 ... 1,198/1,297 at 1,000 - it grows with the world's
record, not its people. **C11.1b's second control ok** (`--oldhouseways` = `_hyr` byte for byte, hash too; ways on: annals = `_hyr`); the rule revised by its
measure (`Conform` 0.6: households as their first household 55 %, tributaries' first households as their lord's 73 %) - `ways/d2181.md` complete but for
`N_a`. Drafts: `tc2/out/tools/s25/frame/d2182.md`. Main c013107. Running: `0_z_web_s25web5` (Web build of main with the save fixes: the test's reload, the heap at
50/300/1,000 against `0_x`).

**Update (~16:50)**: `0_z` (Web `s25web5`, main c013107 with the streamed save and the save-answer fixes): **the heap at year 1,000 955 MB loaded / 1,054
after the views (-243 MB against `0_x`)**, year 300 616 / 713 (-100). **The reload still fails (7/8)**: the reloaded game loaded a save written at day 1
06:11 that carried a catch-up target of day 2 01:57 (it caught up to 93,423 s) - not the explicit save at ~281,777 s, whose own answer did come (the seq fix).
`s25web2` "passed" with 22,359 -> 44,666, a world caught up past its saved time: probably the same fault, masked by the numbers. **Not yet understood** - the
save-answer fixes are right but not this fault's root. Next: in the Web test, right after `save` and its answer, read the store (`page.evaluate`: IndexedDB
'tiny-consequences-2', store 'saves', key 'world' -> the header's WorldTime at byte 18 and CatchUpTarget at byte 35, `realmsperf.mjs` reads the same header) and
again after the reload; find which save wrote a catch-up target and why the explicit save is not what is read (a later write? another key? the title?).

**Update (~17:00)**: **the reload fault traced - the test harness, not the game** (`tc2/scripts/saveprobe.mjs` reads the stored save's header at each step):
saves and reloads work; the test read the published state before the game had done its `save` command (commands run at the next frame) and saw the save
before's `saveOk`. The bridge publishes `saves` (stored), the test waits for it to rise (main 728bd0e). `0_za_web_s25web6` (queued next) should show 8/8.

**Queue reordered (~16:58)** so the landings come sooner (the shots need sim-98 saves on main; seed 7 and the siege measure run their own binaries):
running `K_b_ac3_s4`, then `0_za_web_s25web6` (Web build of main 3bfadd7: the test 8/8 expected; `--layer shadowlite` paired at the region and orbit views),
`0_zb` shots of village #4 at 300/600/1,000, `0_zc` realms at 1,000, `0_zd` meteor and drought, `0_ze_rain_rn1` (rain's checks: `rn1_ok`, control = `_pv1`),
`0_zf_*` (the harrying: build, control vs `_rn1`, ten seeds `_hy1`, `results/cmp_bn1_hy1.txt`), then `K_b_ac3_s7`, `K_ba_siege_s47`, `N_a_ways_s2`. Waiters on
`runq.log` must match exact job names: the log keeps every session's lines (an old `0_za_ways_wy0` matched `^end 0_za`).
**Update (~17:20)**: seed 4's `_ac3` done (places 65 -> 81 over 300-990, unlike seed 2's frozen map; 25 §6). Claude Code stopped this session's background
waiters on `runq.log` for low memory (RAM 87 % at 17:17; the queue's own gate holds `0_za` until it is under 86 %) - the queue runs on; re-arm a waiter only
when memory allows (one `until grep -q "^end <exact job> " runq.log` loop).
**The branches carry main now** (no sim change in main since 98): `wip/rain` 1112a4d, `wip/harry` 8bb3dca (= rain + the harrying); both compile (`cc --src`); their queued
checks (`0_ze`, `0_zf_*`) build these tips. Land: `git merge --no-ff wip/rain` when `0_ze` says `rn1 ok` and the control is byte-identical; then `wip/harry` by `0_zf_d`.

### Next, in order
1. Judge `0_za` (8/8 expected), then append D2-182 (`tc2/out/tools/s25/frame/d2182.md`) to DECISIONS. If a cull shows pop-in or missing villages in real shots
   (`K_d`), fix forward.
2. **The frame and memory passes, next steps**: buildings are still the largest layer - villages in front of the limb but outside the view, the buildings' draws
   by colour group (each a draw a camera and a shadow cascade), shadows cast only near (`Sky.GpuCascades` 4 and `GpuShadowReach` 3.2 x the camera's height,
   up to 7 km: at the region view every visible building is drawn into four cascades - try 2 cascades and ~2x above ~3 km, measured by a bridge toggle in
   `realmsperf` and judged in real shots); the heap grows with the record - **measured** (`run --load F
   --statesizes`, main 44b12bc; `tc2/out/tools/s25/frame/statesizes_s2_ac3_y{300,1000}.txt`): seed 2's state 33.7 MB at 300 -> 106.8 at 1,000; **the history
   ledger 12.8 -> 63.9 MB** (114k -> 509k events, 117-132 B each; Local-tier Struck, TakingSeen, Sworn, Taken, TributePaid lead), **households 7,005 -> 68,066
   rows** (dissolved ones kept: 10.7 MB), **people 25,461 -> 47,527** (12,698 living; 20.4 MB), the deed book 1.1 -> 3.6 MB. Levers, in order: compact old
   Local-tier events the book does not read (check `HistoryBook`'s readers first - it is written after the fact), drop dissolved households nothing refers to
   (check Building.Style/Last, biographies, the name registry), prune the long dead as compaction already does for some.
3. Land in order once `K_d`-`K_f` are done (they need sim-98 saves): rain (`L_a`), the harrying (`M_*`; D2-180), then C11.1b (`0_y` ok; D2-181; merge main
   into `wip/c111b-ways` first - the version line 99, 100, 101).
4. Judge the third acceptance run (25 §6 "Third run": `_ac3` seeds 2/4/7, books, `K_d` shots) and write it up.


## Session 25, third context - HANDOFF (25 Oct 2026, ~14:10 IST; Control Room run #78 session 2) - main 9bd2814 (sim 98); the final run RUNNING in the queue

### Exact state
- **main 9bd2814, sim 98, clean** (not ours: `.claude/launch.json`, `Assets/Resources.meta`). Landed this context: **C3.6g the land between villages**
  (D2-176), **C8.0 the burning-in** (D2-177), **F9.1-F9.4 the god's powers** (D2-178, D2-179: the power bar - lightning, storm, quake, eruption,
  sickness, blight, drought, meteor; Your Actions, the book's ninth part; the bolt and the falling stone drawn), **F11.1 the opening** (title and guide),
  **F10.1 the world's sound** (`AudioView`, 71 clips under `Game/Resources/Audio`, credits), a Sound toggle; tools `place --margins`, `run --sizetally`,
  `--burntally`, `--margintally`; shots `--meteor`, `--godrought`. **Main's full suite 473 passed, 0 failed, 0 skipped** (9d3c1c2 = main's code).
- **Measured, not built**: small villages live better, not worse (`run --sizetally`) - no desertion rule. The shots' `ArgumentOutOfRangeException` is
  Unity's own editor search indexing at startup - closed. War now (`results/war_s*_ac2_y*.txt`): between realms (898 raids a century at seed 4's year
  1,000 against 26 within a realm), half broken on walls - `tc2/out/tools/s25/c81_design.md`.
- **Two branches built, waiting to land in order** (each changes the sim version - never land while a queued job needs a sim-98 save):
  1. **F9.5 the rain** - `wip/rain` e1782f0 (worktree `C:/Game Dev/tc-rain`, sim 99; PowersTests 7/7, floatcheck, cc ok). Its checks and its uncast
     control (= `_pv1`) are job `L_a_rain_rn1`. Land: `git merge --no-ff wip/rain` when `L_a` says `rn1 ok` and `seed 4: F9.5 uncast = _pv1 byte-identical`.
  2. **C8.1a the harrying** - `wip/harry` 1090933 (worktree `C:/Game Dev/tc-harry`, on wip/rain, sim 100; HarryTests 2/2): a band driven off by a
     village's standing palisade burns 3 % of its standing crop a raider (at most 60 %), caused by the raid. Evaluation `M_a`-`M_d` (control `--oldharry`
     = `_rn1`, which also measures the old rules with `--harrytally`; ten seeds `_hy1` against `_bn1`; `results/cmp_bn1_hy1.txt`). Land after rain if
     the ten seeds hold (a lean past |t| 2 in people: a matched placebo or a mechanism); write D2-180.
- **The queue** (runner PID 9632, `tc2/out/tools/s23/runq.sh`; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`;
  generators in `tc2/out/tools/s25/c36g/`: `make_final.sh`, `make_c81a.sh`): running **`K_b_ac3_s2`** (since 14:03; ~40 min), then `K_b_ac3_s4`, `K_b_ac3_s7`
  (the third 1,000-year run on main's final rules: `$O/s*_ac3/world_y{50,150,300,500,600,800,1000}.tc2`, books `tc2/out/tools/s25/accept/book3_*`),
  `K_c_web_s25web3` (Web build of main; webtest with the power cast by a real click, Your Actions and the title; bookshots over `s2_ac3` y1,000; realmsperf
  `--layer` people/buildings/trees/cover/shadows/terrain/Hud -> `$N/perf_*_s25web3.txt`), `K_d` shots of village #4 at 300/600/1,000 (`tc2/out/shots/s25o_ac3_s2_v4_y*`),
  `K_e` realms at 1,000 (the spears edged pale), `K_f` a meteor and a drought (`s25o_meteor`, `s25o_drought`), then `L_a` (rain), `M_*` (the harrying).
  No monitor survives the clear: arm one (`tail -n 0 -F $Q/runq.log | grep --line-buffered -E "^end |runner stopped"`).
- **After Unity first opens main** (the `K_c` Web build), it will create `.meta` files for the 71 new `.ogg` under `Game/Resources/Audio`: commit them.

### Next, in order
1. Judge `K_b` (25 §6 on `_ac3`: villages, burnt homes, realms, the map after the infill; the books), `K_c` (webtest checks incl. "a power aimed and clicked
   is cast" and the title; the layer costs), `K_d`-`K_f` shots (fixed village by age; the spears; the meteor and the drought seen). Write 25 §6's third run.
2. Land rain (`L_a`), then the harrying (`M_*`) - D2-180, docs.
3. The frame pass by the layer costs (`14` §7): first candidate - `WorldObjectsView.RebuildPeople` computes every living person's position and height each
   frame within 6 km of the camera, before any culling.
4. Then: C8.1 the siege proper (a blockade - `c81_design.md`), more powers (warmth, cull, bring), F11 platform (packaging, settings), W-FOREST.

## Session 25, third context (25 Oct 2026, from 07:34 IST; after a compaction) - C3.6g the land between villages LANDED (sim 95)

### Exact state
- **main** b516bb1 (merge of `wip/marginal` 6da1ec5) + DECISIONS D2-176 (66a2362) + docs: **C3.6g the land between villages** (sim 95). Measured first
  with `place --margins` (new): the handoff's marginal-land hypothesis refuted - the room was between villages. Notes: `tc2/out/tools/s25/c36g/design.md`,
  draft `d2176.md`, job generators `make_c36g.sh`, `make_ac2.sh`, `make_land.sh`. Not ours: `.claude/launch.json`, `Assets/Resources.meta`.
- **Validated on the branch**: MarginsTests 4/4, MigrationTests, HoldingsTests, GazetteerTests, HistoryBookTests, TongueTests, DeterminismTests, SaveLoad -
  0 failed, 0 skipped; floatcheck clean; control `--oldmargins` = main's rules (`_m9`) byte for byte, annals and state hash; ten seeds `_mg2` against `_pc1`
  (`results/cmp_pc1_mg2.txt`); 1,000-year worlds `_ac2` (seeds 2, 4, 7: `$O/s*_ac2/world_y{50,150,300,500,600,800,1000}.tc2`, books
  `tc2/out/tools/s25/c36g/book_s*_ac2_y{600,1000}.md`, `results/margins_s*_ac2_y*.txt`). `_mg1` (before the join fix) dropped.
- **The queue** (runner PID 9632, `tc2/out/tools/s23/runq.sh`; `Q` as below): `D_a_land_m10` (main: `unity.mjs cc`, headless bin_m10, floatcheck, the full
  suite -> `fs_m10_ok`, `results/fullsuite_m10.txt`), then `D_b_shots_ac2_s2_v4_y{0300,0600,1000}` (real shots of the new world framed on village #4
  Vivaiwick: `tc2/out/shots/s25n_ac2_s2_v4_y*`) - to compare with the old rules' `s25n_ac1_s2_v4_y{300,1000}` (done). Do not edit main's Game code while
  a `D_b` Unity job runs.

**Update (~13:50)**: main's full suite (K_a, before F9.4) 471/0/0. **F9.4 the meteor and the drought landed** (be8ef8e, sim 98) and **F10.1 the
world's sound** (f47ce59; `AudioView`, the library under `Game/Resources/Audio`, credits) - D2-179; a Sound toggle (66402bb). Main 66402bb. The final
run was regenerated on 98 (`make_final.sh`, with `K_f` shots of a meteor and a drought); the 97 jobs dropped. Unity's first open will import the 71 .ogg
files: commit their new `.meta` files after it (untracked under `Game/Resources/Audio`).

**Update (~13:15)**: `D_a` main's full suite 465/0/0; the `D_b` shots judged (300 and 600 alike from above; 1,000 shows hamlets at the desert's edge).
Measured: small villages live better (`run --sizetally`) - no desertion rule. **C8.0 the burning-in landed** (39bfa3a, sim 96, D2-177; worktree
`tc-burn`). **F9.1-F9.3 + F11.1 landed** (0b05e63, sim 97, D2-178; worktree `tc-powers`): the power bar, Your Actions, a sickness and a blight, the
title. Main 67a2cd8. **The queue now** (`make_final.sh`): `K_a` full suite (bin_m12, `fs_m12_ok`), `K_b_ac3_s{2,4,7}` (1,000-year worlds `$O/s*_ac3`,
books `accept/book3_*`), `K_c_web_s25web3` (Web build, webtest with powers/title, bookshots over `s2_ac3` y1,000, `realmsperf --layer` people/buildings/
trees/cover/shadows/terrain/Hud), `K_d` shots of #4 at 300/600/1,000, `K_e` realms at 1,000. Then: judge them; the frame pass by the layer costs; more
powers (meteor, drought); 25 §6 judged on `_ac3`.

### Next, in order
1. Judge `D_a` (0 failed, 0 skipped) and the `D_b` shots against `s25n_ac1_*` (region view: villages between villages, fields closing the gaps).
2. **Next slice by the evidence: villages deserted in crises and refounded** (25 §6 still lacks ruins and rebuilding: after the infill 2-4 villages
   abandoned in 700 years; a hamlet of 11-12 people lived 400 years). Measure first: do small villages live worse (hunger, deaths, births, leaving a
   person-year by village size, from the `_ac2` saves' records)? If they do not, a desertion rule would be scripting; if they do, households should weigh it.
3. Then: the war spears made legible; the trace of the shots' `ArgumentOutOfRangeException`; C7.2; great worlds in the browser (`14` §7 - now x2.2 the
   villages' cost: a 1,000-year world 2,000-2,300 s headless); F9.

## Session 25, second context (25 Oct 2026, 23:56 - ~02:45 IST; Control Room run #78 session 2) - C10.8, C10.9, C10.10 and C11.1a LANDED (sim 94); the 1,000-year acceptance RUNNING in the queue

**Update (~02:45)**: the peace's lean was judged against its matched placebo (the twins x1.029; the peace against its twin x1.054, t 2.69) and explained by its mechanism (raids within realms gone: seed 4 85 -> 0; war at the realms' edges) - **C10.10 landed** (955b497, sim 93; D2-174 e2b41b0); **C11.1a landed** after it (33fd336, sim 94; D2-175 80a1eaa); PROGRESS 2935c9a; root and standing NEXT_SESSION_PROMPTs updated. The queue now: `9_a_fullsuite_m8` (running), `9_b_ac1_s{2,4,7}` (1,000-year worlds on main's final rules, saved at 50/150/300/500/800/1,000; books at 300 and 1,000), `9_bx_stone_st0` (`place --stonewalls` on them: C7.2 measured first, worktree `C:/Game Dev/tc-stone`, branch `wip/c72-stone`), `9_bz_web` (Web `s25web2` over the year-1,000 and year-300 worlds), `9_c_shots_s{2,4}_*` (every age's real shots `s25m_*`). Generator: `tc2/out/tools/s25/accept/make_accept.sh`. The rest of this block below is the state at ~02:10 (kept for the record).

**Update (~03:40)**: main's full suite on the final rules **461 passed, 0 failed, 0 skipped** (`9_a`); the three 1,000-year worlds and their books done (`9_b`; notes `tc2/out/tools/s25/accept/notes.md`: years 0-300 read as 25 §6 asks, a great plague in seed 2 (people -52 % over 450-600, recovered by 650), hegemonies rising and falling (seed 4: Touldandholm 30 of 34 villages at year 917) - but **after the land fills (~300) the map stops changing**: 2-3 villages abandoned in 1,000 years, almost no foundings: the next slice by evidence, to measure first); the C7.2 measure done (`tc2/out/tools/s25/c72_design.md`: stone walls have narrow room - a few heavy losers a world with stone at hand). **At ~03:35 Claude Code reported the queue's background task stopped for low memory** (during the Web build `9_bz_web`); its processes (runq.sh, the job, Unity's batch build) were still running afterwards and the queue went on - check `runq.log`: if the runner is gone, restart it only when Arjun asks (`bash tc2/out/tools/s23/runq.sh` in the background; move `$Q/run_9_bz_web.sh` back to `$Q/todo/` if it did not end). Left after the Web job: `9_c_shots_*` (16 runs).

**Update (~04:15)**: the queue ran on (its processes had survived the stop): **Web `s25web2` 6/6** (the year-1,000 world in the browser: names by tongue, the book as reigns; layer costs names x0.99, clouds x0.99-1.0, air x0.92-0.97; the base frame 6-9 fps at region/village - `14` §7); notes in `tc2/out/tools/s25/accept/notes.md`. **The book's realms as reigns** fixed on `wip/book-reigns` (worktree `C:/Game Dev/tc-reigns`, bb8f581; its classes 0 failed, 0 skipped, `cc --src` ok): a village won back after each lapse is told once and counted - **merge it into main once the `9_c` shots (Unity in the main checkout) have finished** (`git merge --no-ff wip/book-reigns`; an observer, no sim change). Then write the acceptance up (25 §6: what holds, what does not - the map frozen after ~300) and start the next slice by its evidence.

### Exact state (~02:10)
- **main** (040c897 and after): **C10.8 the asking landed** (731c75a, sim 91; D2-168 appended b5f299c) - its branch's one failing test was a premise the asking
  overtook (village 1 bowed to village 2 by its own asking a year before the hand-written bow): `HistoryBookTests` keeps the askings off for it (1b5e033).
  **C10.9 the lord's death landed** (aeb880b, **sim 92**; D2-173 appended 77fcef3, 040c897): ten seeds against C10.8 - people and per-person rates at par,
  raids x0.64, wounds x0.82, bonds made x2.8 and ended x3.6, the greatest realm x1.04; control `--oldlapse` = `_dm1` byte for byte. **The wars on the
  realms map layer** merged (8098877, presentation): the last ten years' raids as dark spearheads toward the raided (`RealmsView.Feuds`, the legend counts
  them and those of one realm); Unity C# compiled (`cc --src`), **not yet shot**. PROGRESS checkpoint ef742c5. Not ours: `.claude/launch.json`,
  `Assets/Resources.meta`.
- **A fault in the job tooling, fixed**: `lib.sh`'s `gate` read the machine's load into global `r` and `c`, so jobs looping `for c in <classes>` ran filters
  like "4" and "2", and "0 passed, 0 failed, 0 skipped" passed the gate (C10.9's first `lp1 ok` had run none of its classes). `gate` now keeps `r c` local;
  every test gate since needs `[1-9][0-9]* passed`. Loop variables in jobs: `cls`.
- **C8.1 the campaign and the siege - measured first and set aside** (`tc2/out/tools/s25/c81_design.md`; tools on `wip/peace`, worktree `C:/Game Dev/tc-siege`:
  `lordships --walls`, `run --raidtally`): refusals of the asking are rare (0-3 a world) and every one ended in a bow at the next asking with no band; the war
  is on walled villages (62-64 % of raids driven off) and it is **feud** - raids answer blows struck in earlier fights, refused claims, takers seen (bonds'
  ends 1-6 %); **30-88 % of raids fall between villages of one realm**.
- **C10.10 the lord's peace** (`wip/c1010-peace`, worktree `C:/Game Dev/tc-peace`, **sim 93**, on C10.9: 17d4968; D2-174 draft `tc2/out/tools/s25/d2174.md`):
  villages of one realm raise no bands against each other (a band turns to the heaviest foe beyond the realm); a claim for blood between them is judged by
  their common lord, whose judgment binds the doers where it is no weaker - amends paid willing or not (`AmendsMade` ArgB the judge); the book tells each
  realm's peace. PeaceTests 3/3 and its classes 0 failed, 0 skipped; control `--oldpeace` = `_lp1` byte for byte (and = `_dm1` on C10.8's rules). **Ten
  seeds against C10.9** (`results/cmp_lp1_pc1.txt`): per-person births and deaths at par (x1.010, x0.992), famines a person-year x0.83 (t -1.88), loot x0.65
  (t -2.34), raids x0.86, wounds x1.09 (bands turned beyond the realm), lords x1.31, greatest x0.90; **a lean in people: years 100-199 x1.085 (t 4.13), at
  year 300 x1.10 (t 2.14)** - its matched placebo `_pcp` (C10.9's rules nudged at each world's parting year 4:73 5:73 1:81 6:65 11:67 2:113 3:58 7:103 8:75
  42:21) and the raid classification of both rules' year-300 worlds are in the queue (`8_f8`, `8_f9`).
- **C11.1a a people's tongue** (`wip/c111-ways`, worktree `C:/Game Dev/tc-ways`, **sim 94**, on main 040c897: d3266b8; D2-175 draft `tc2/out/tools/s25/d2175.md`):
  measured first (`place --ways`): every house stands as its village's way, villages never change their way, all names came from one speech. Each first
  people speaks its own tongue (onsets, vowels, codas, place-name endings, habits of naming: `Names.NewTongue`); founders bring theirs, half the time changed
  in one thing (`Names.DriftTongue`); places, first families and the born are named in it (`Settlement.Tongue`); the book's Places begins with The Peoples.
  TongueTests 4/4, HistoryBookTests 5/5, Determinism, SaveLoad, `cc --src`; **seed 4's annals = `_lp1` byte for byte** (names change nothing else). Ready to
  land after the peace's decision (the peace 93 first, then the tongue 94; if the peace is not landed, the tongue lands as 94 anyway).
- **The queue** (task brw7xh61a, runner `tc2/out/tools/s23/runq.sh`; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`):
  running `8_f8_pcp_*` (ten placebo seeds), then `8_f9_cmp_pcp` (the lean judged: `results/cmp_lp1_pcp.txt`, `cmp_pcp_pc1.txt`; raids by realm in
  `tc2/out/tools/s25/results/peace_s*_{lp1,pc1}.txt`). **Held** in `$Q/hold`: `8_g_fullsuite_m7`, `8_h_shots_realms`, `8_i_web` (superseded by the
  post-landing checks below), `8_a*`, `8_b*`, `9_*` of session 24 (attribution only). `$Q/dropped`: the peace's first seeds against C10.8.

### Next, in order
1. Judge the peace's lean (`8_f9`): if the twins spread as wide (the placebo's people x1.0x with |t| < 2 and the peace against its twin inside it) or the
   lean is explained by its mechanism (within-realm raids gone, famines and loot down), land it: on main `git merge --no-ff wip/c1010-peace` (sim 93),
   D2-174 with its evidence; then merge main into `wip/c111-ways` (version line: 94 after 93) and land the tongue (D2-175).
2. Then the post-landing checks and the 1,000-year acceptance: `bash tc2/out/tools/s25/accept/make_accept.sh $Q` writes `9_a` (main's full suite, bin_m8),
   `9_b_ac1_s{2,4,7}` (1,000-year worlds saved at 50/150/300/500/800/1,000, books at 300 and 1,000), `9_c_shots_*` (each age's real shots, seeds 2 and 4;
   realms at 1,000 - the war spears' first shots); and a Web build (copy `$Q/hold/8_i_web.sh`, point it at `$O/s2_ac1/world_y300.tc2` and `world_y1000.tc2`:
   names in tongues on the map, the book's Peoples, Timeline and Realms with their peace, `realmsperf --layer names|air|clouds`).
3. Then by evidence: war between realms after the peace (C8.1's room may open there), C7.2 stone walls, C11.1b households carrying their ways (marriage,
   migration; villages that blend), great worlds in the browser (`14` §7), F9.

## Session 25 (24 Oct 2026, 22:14 - ~23:40 IST, first context) - context handoff with the job queue RUNNING; C10.8 decided (land); C10.9 built and queued

### Exact state at the handoff (~23:40)
- **main** (53cb512): the orbit look (D2-170), the History Book as a book with its Timeline page (D2-171), places, works and people named (D2-172),
  the repaired compile check (`unity.mjs cc` compiled nothing since session 23), 25 §8 (the plan toward a finished core), DECISIONS D2-170..D2-172
  appended. **No sim change on main: sim 90.** Not ours: `.claude/launch.json`, `Assets/Resources.meta`, and the Web build's `ProjectSettings.asset` /
  `Assets/Resources/` - leave them.
- **Validated on main**: full suite 442/442 (e290217, before this session's observer/presentation changes); HistoryBookTests 5/5 (with the timeline);
  real shots `s25a`-`s25d` judged; **Web `s25web1` 6/6** (5b91710) and the HUD over the year-300/900 worlds in the browser (`tc2/out/webtest/s25web1b900`,
  `s25web1b300`: names, the book's parts). Not yet in a Web build: works/people names, the book's buttons, names clear of the bars, the Timeline page.
- **The queue** (task brw7xh61a; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`):
  running **`7_j_fullsuite_dm3`** (C10.8's branch tip, rebuilt as `bin_dm3`: `fs_dm3_ok` only with 0 failed, 0 skipped; `results/fullsuite_dm3.txt`), then
  **C10.9's evaluation** `8_c_build_lp1` (bin_lp1 from `C:/Game Dev/tc-lapse`, its test classes -> `lp1_ok`), `8_d_ctl_lp0_s4` (control `--oldlapse` =
  `_dm1` byte for byte -> `lp_ctl_ok`), `8_e_lp1_*` (ten seeds `_lp1`, saved at year 300), `8_f_cmp_lp1` (`results/cmp_dm1_lp1.txt`). Held: `8_a*`, `8_b*`,
  `9_*` (attribution, not conditions). A monitor on `runq.log` (re-arm in a new context).
- **C10.8 decided: land it** (`wip/c108-demand`, worktree `C:/Game Dev/tc-demand`, sim 91; draft `tc2/out/tools/s24/d2168.md`, complete): its matched
  placebo showed the lean is real (people over 100-199 x0.904, t -4.64 against the twin; the twins themselves x1.005) and explained - war deaths (+0.7
  a thousand person-years from wounds) and fewer foundings while the worlds grow, about a tenth of their people; near the ceiling x0.92; per-person
  rates at par; realms rise and fall (its year-900 books). The branch is current with main up to ea6b826 (+ the book's asking b808479, the tools
  `--together`, `--heirs`); main has moved since (docs, names, the timeline): merge main into it again before landing.
- **C10.9 the lord's death** (`wip/c109-lapse`, worktree `C:/Game Dev/tc-lapse`, from C10.8; sim 92; bd27e74): at the death of the first at a lord's
  seat the bonds for peace lapse (bread bonds stand); heirs win them back by asking. Measured first: C10.8's great realms are held by the call (22 of 28:
  villages together outweigh the lord x1.3-x13.8, none alone can); the first at a seat dies about every 12 years. Design and measures:
  `tc2/out/tools/s25/c109_design.md`; D2-173 to be written from its evaluation.

### Next, in order
1. `7_j` (23:49): 450 passed, 1 failed - `HistoryBookTests.RaidsAreOneWarUntilABowOrTwelveQuietYears` (its hand-written raids and bow between villages 1
   and 2 meet C10.8's own askings between them; set `Conflict.DemandOff` for that test or choose villages the asking cannot join; rerun the class). Then
   land C10.8 - in `C:/Game Dev/tc-demand` `git merge main`, rebuild nothing else; on main `git merge --no-ff wip/c108-demand` (sim 91);
   append D2-168 (`d2168.md`) to DECISIONS; `25` C10 line; then queue on main: the full suite (bin from main), `cc`, real shots of realms grown
   (`unity.mjs shots --realms --load $O/s2_l9dm1/world_y600.tc2 --village lord`) and a Web build with `bookshots.mjs` (the Timeline page: `--book "-2,1:0"`)
   and `realmsperf.mjs --layer names|air|clouds` (paired costs, `14` §7).
2. C10.9: judge `8_f` (vital rates against `_dm1`; bonds lapsed and won back; the greatest realm's share); if sound, its 900 years from
   `$O/s{2,4}_lp1/world_y300.tc2` and the books (`book --load`); land after C10.8 (merge main into `wip/c109-lapse` first).
3. Then 25 §8: C8.1 the campaign and the siege, C7.2 stone walls, C11.1 a people's ways, the 1,000-year acceptance; great worlds in the browser.

## Session 24, second context - END (21 Oct 2026, ~06:05 IST; stopped at Arjun's word) - landed and validated; C10.8 in evaluation; the queue stopping

### Exact state at the end
- **main** (this commit and its parents since 88f04a6): the landing 3b236ee (C10.7a, C10.7b, C9.5c; **sim 90**), the lordships tools 3bf3d44, the realms
  layer's dyes for the land e210eca + 9d7c796 (D2-167), saves into kept buffers 29e85f8 (D2-169: the browser OOM), tools (`tc2/scripts/realmsperf.mjs`,
  webtest's heap from `Module.HEAPU32`), docs (`18` §9, `10` §12h, `14` §7, `25` C10), **DECISIONS D2-164, D2-165, D2-166, D2-167 and D2-169 appended**
  (D2-164/D2-165 say honestly that their own per-slice 900-year checks were not run - attribution, not a condition; D2-168, C10.8, is recorded when
  it lands). Not ours: `.claude/launch.json` (modified), `Assets/Resources.meta` (untracked) - leave them.
- **Validated**: the full suite **441/441, 0 skipped** on main e191331 (the landing; before the dye and save changes - those ran their own: the new
  `SaveIntoKeptBuffersIsTheSameSaveAndReallocatesNothing`, Determinism 7, SaveLoad 8, `cc` of every Game change); Web **`s24web1` 6/6** (e191331) and
  **`s24web2` 6/6** (3344bf0, with the save fix; Mono = dotnet; browser = headless); the year-900 world's heap flat at 1,053 MB through autosaves
  (was OOM); real shots `s24p` (sworn men at practice), `s24q`, `s24t` -> `s24u` -> `s24v` (realms; two faults fixed). **First thing next: the full
  suite on main's tip** (it has not run since the save fix and the dyes; ~18 min: build main's headless, `tc2.dll test`, 0 failed and 0 skipped).
- **The queue** (task bcbn62z2b; `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`;
  `touch $Q/stop` done at 06:01: the runner exits after the job running then): **`7_f_l9dm1_s4`** (C10.8's 900 years, seed 4) was at year 360 of 900 at
  06:01 - **it finished (06:18, rc 0) and the runner stopped cleanly** (`runner stopped 06:18:57`): seed 4 at year 900 - Krailddal 24 of 35
  villages paying, depth 4; 198 bonds made, 166 ended; raids 1,415 (the integrated rules' 681); 10,873 people (12,071) - in `d2168.md`. **Left in `$Q/todo`**: `7_g_lords_l9dm1` (C10.8's lordships at years 300/600/900 -> `results/lordships_s{2,4}_dm1_y*.txt`),
  `7_h_dmp_01..10` + `7_i_cmp_dmp` (**C10.8's matched placebo** `_dmp`: `bin_dm1 --olddemand --nudge Y` at each seed's parting year 4:14 5:11 1:8 6:21
  11:30 2:13 3:11 7:6 8:7 42:7 -> `results/cmp_i2_dmp.txt`, `cmp_dmp_dm1.txt`), `8_a1..3` (C10.7a's own 900 years), `8_b1..2` (C10.7b's), `9_a..c` (D2-161's
  attribution). **To restart**: `rm $Q/stop`, then `bash tc2/out/tools/s23/runq.sh` in the background. Binaries in `$Q/..`: `bin_dm1`, `bin_dm2` (C10.8 +
  tools; = `bin_dm1`'s rules, `_dm1` reproduced), `bin_i2`, `bin_lt`, `bin_s2`, `bin_a9`, `bin_m5`, `bin_sm1`. **If that scratchpad is gone**: point `S`/`N` in
  `tc2/out/tools/s23/lib.sh` and `Q` in `runq.sh` at a live one and rebuild each binary a job needs from its worktree (`7_e_build_dm2.sh` for `bin_dm2`/`bin_dm1`'s
  rules; `5_a_rebuild_a9.sh`, `1_b_rebuild_s2.sh`; the saves the jobs read stay in `tc2/out/civ`).
- **C10.8 the asking** (`wip/c108-demand`, worktree `C:/Game Dev/tc-demand` at ae63c5b = main 776980f + C10.8, merged clean - `bin_dm2` is dc36cd9's, the same rules; sim 91; D2-168 draft
  `tc2/out/tools/s24/d2168.md`): control `--olddemand` = `_i2` byte for byte; **ten seeds**: per-person rates at par (births x1.008, deaths x0.982 a
  person-year), one lean - people over years 100-199 x0.908 (t -2.85; the twins part at years 6-30: the placebo decides); the mechanism strong - bonds
  agreed x12, the greatest realm x4.2 (8-16 villages by year 300; seed 8's Skaldton 12 of 14 places), **bonds ended x21** and **raids x6, wounds a
  person-year x2.75** (each end is a refusal the lord holds); **900 years, seed 2**: 279 bonds made, 250 ended; at year 900 two lords, Mestskouford 18
  of 31 villages (depth 3); people 9,254 against 9,532, the per-person rates at par; **seed 4**: Krailddal 24 of 35 (depth 4), raids x2.1, people
  x0.90 - one realm comes to hold most of each world. Open: is the churn of bonds (and its war) the record's lapse at a
  lord's death, or a margin that wants a counterweight before landing?

### Next, in order
1. The full suite on main's tip (above). 2. Restart the queue; judge `7_f` seed 4, `7_g` (the bonds' lives and ends: why most end - the lord dead,
   weaker by the call's cascade, or the margin; do realms rise and fall?), `7_i` (the placebo: is x0.908 within twins' spread?). 3. Decide C10.8: land it
   as it is (the war is the record's - heirs winning back tributaries) or with a counterweight first (rising together; renewal at succession) - then
   merge main into `wip/c108-demand` again if main moved (it is current at 776980f), land (sim 91), D2-168, the full suite, real shots of realms grown (`unity.mjs shots --realms` on a
   `_dm1`/`_l9dm1` world; `--village lord`), Web. 4. When `8_*` run: an addendum line in D2-164/D2-165 (attribution). 5. Great worlds in the browser
   (`14` §7): ~1.05 GB heap and ~8 fps at the region view for 9,500 people - a memory and frame pass of their own.
- Worktrees merged and removable when convenient: `tc-int2`, `tc-s24tools`, `tc-realms`, `tc-savemem`, `tc-arms`, `tc-sworn`, `tc-visits` (and the
  older list below); `tc-s24m` must never be merged; `tc-demand` is live.

## Session 24, second context (21 Oct 2026, from 01:45 IST; Control Room run #73 session 2) - C10.7a, C10.7b, C9.5c LANDED on main; the job queue RUNNING

### Update at ~05:25 - the browser OOM found and fixed (D2-169 draft, complete): the autosave's buffers
- **Traced** (`0_i`, `0_j`; heap from `Module.HEAPU32`): the year-900 world's heap grew ~500 MB a minute with the realms layer off, held on or turned
  alike - the autosave (60 s; 20 s at the fastest speeds): `SaveFile.Save` made ~460 MB of short-lived buffers a save (a 79 MB state grown by doubling,
  copied, deflated through a growing stream, copied twice) and the wasm heap only grows. **Fixed on `wip/save-mem`** (ba8e108, merged on main 29e85f8):
  `SaveFile.Scratch` buffers kept by `SimHost` and sized at each load (`Scratch.For(header)`), the store takes a length, the load inflates in place - the
  same bytes. Checked: `SaveIntoKeptBuffersIsTheSameSaveAndReallocatesNothing`, Determinism 7, SaveLoad 8 (0 skipped), floatcheck, `cc --src`; **in the
  browser** (Web build of 3344bf0, `0_m`): the year-900 world 858 MB loaded, 955 with the view, 1,053 after the first autosave and flat to 210 s (before:
  -> 1,366 -> 1,820 -> OOM); the year-300 world flat at 513 MB; **Web test `s24web2` 6/6** (Mono = dotnet; the catch-up heap 247 -> 297 MB). `14` §7
  records great worlds' heap and frame rates (still ~1.05 GB and ~8 fps at the region view for 9,500 people: a memory and frame pass of their own).
- **DECISIONS order**: append D2-164..D2-167 (`append_decisions.py`) when `d2164.md`/`d2165.md` are filled (`8_a*`, `8_b*`), then D2-169
  (`tc2/out/tools/s24/d2169.md`, complete) - D2-168 (C10.8) is appended when C10.8 lands, after it.

### Update at ~04:30 - the landing checks passed; the realms layer's dyes fixed twice from real shots; a browser OOM under investigation
- **Landing checks (all passed)**: `0_a` bin_m5 (main e191331) floatcheck clean, `cc` ok, seed 2's `_m5` = `_i2` byte for byte (state hash 56fb1fe012804dd3);
  `0_b` **full suite 441 passed, 0 failed, 0 skipped**; `0_d` **Web `s24web1` 6/6** (built at e191331 in 22.5 min; Mono = dotnet hash parts; browser =
  headless; reload continues; a day away catches up 96 years) - its frame rates are about half of `s23web4`'s in every view (orbit 56 against 120), the
  machine loaded by other sessions (CPU 65 %): not evidence of a regression.
- **Shots judged**: `s24p` sworn men at practice at Vaildwick's ground (nine drilling, its afternoon) - reads; `s24q` Vaildwick's orange realm at year 300 -
  reads; **`s24t` (the year-900 overkingship) found the sky-blue realm reading as lakes and lagoons, the white one as a salt flat** -> realms take only
  ground-safe dyes (`MapSafe`: never argent/sable; magenta for the sky blue; azure blue-violet on the map: wip/realms ed74cb0, main e210eca); **`s24u`
  then found magenta beside rose reading as one realm** -> realms seen together at least 60 delta E apart (`DyeDelta`, `DyesApart`: c806b27, main
  9d7c796); `s24v`: magenta, orange, crimson read as three realms. Preview tool `tc2/out/tools/s24/dye_preview.py` (the shader's wash on a screenshot).
- **The realms layer's frame cost** (new tool `tc2/scripts/realmsperf.mjs`: a headless save injected into the game's IndexedDB as saved now, paused;
  paired off/on frame counts by requestAnimationFrame): year 300 (5,991 people) region x0.998, orbit x1.003, village x1.038; year 900 region x0.981, orbit
  x1.010 - **no measurable cost**. But absolute rates of great worlds are low (region 8-11 fps, village 5-7 fps on the loaded machine; `s23web4`'s 569-person
  world 24 fps at the village view): a performance gap of mature worlds in the browser, older than this session.
- **A browser OOM** (`s24perf900`): the year-900 world (9,532 people, 21 MB save, managed heap 238 MB headless) aborted on OOM (`abortOnCannotGrowMemory`,
  the 2 GB ceiling; `02` budgets 1 GB) at the village view after ~30 toggles of the layer across three views; with no toggling (`0_h` a) every view including
  close survived, and 30 toggles at one view (`0_h` b) too - but `0_h` read the heap as -1 (the framework exports `Module.HEAPU32`, not `HEAPU8`; webtest's
  `--export` trace was blind the same way - both fixed). `0_i_heap2` (queued) traces the heap for the views and for the sequence that aborted.
- **The profile** (`6_c`): sworn men and might by lasting strength cost nothing measurable (40.4 s against 40.0 s for 30 COARSE years; in `d2165.md`).

### Exact state (21 Oct ~02:55)
- **main** = 0e84d21: the landing merge 3b236ee (`wip/s23b-int` ca25d71: C10.7a, C10.7b, C9.5c; **sim version 90**), the tools merge 3bf3d44
  (`wip/s24-tools` 8bf5141: `lordships --heads --margins --could --wrongs`), docs `18` §9 (D2-167) and `10` §12h. Not ours: `.claude/launch.json`
  (modified), `Assets/Resources.meta` (untracked) - leave them. **DECISIONS D2-164-D2-167 are NOT appended yet**: `append_decisions.py` refuses FILLs, and
  `d2164.md` (C10.7a's own 900 years), `d2165.md` (C10.7b's own 900 years and the profile) and `s24/d2167.md` (the Web frame cost) still have one each;
  `d2166.md` is complete.
- **Why landed before the per-slice 900-year checks**: the integrated rules' 900 years (`4_e_d`) answered the snowball question for what lands (realms
  grow, nothing snowballs - `c108_design.md` top, `d2165.md`); the per-slice runs only attribute it.
- **The queue** (task bcbn62z2b, the same runner and `Q` as below): at 02:55 `5_a_rebuild_a9` (C10.7a's binary) running; then, in order: `0_a_land_checks`
  (bin_m5 from main, floatcheck, hashtest, `unity.mjs cc` -> `cc_main9.txt`, seed 2's v90 world `_m5` saved at year 300), `0_b_fullsuite_m5` (`fs_m5_ok`
  only with 0 failed, 0 skipped; `results/fullsuite_m5.txt`), `0_c2_shots_realm900` (`s24t`/`s24tn`: the year-900 overkingship of `_l9i2` with the realms
  layer at orbit, region, village; the region without it), `0_c_shots_m5` (`s24p` sworn men at practice, `s24q` realms on `_m5`), `0_d_web` (`s24web1`:
  Web build, Mono = dotnet hash parts, webtest with `3b_region_realms`), `6_c_profile_sx1`, `7_a_i2_*` (the integrated rules' other eight seeds), `7_b`
  (C10.8's control `--olddemand` = `_i2` seed 4 -> `dm_ctl_ok`), `7_c_dm1_*` (C10.8's ten seeds), `7_d` (`results/cmp_i2_dm1.txt`), **`8_a1..3`** (C10.7a's
  900 years: renamed from `5_b..d`), **`8_b1..2`** (C10.7b's: from `6_a..b`), `9_*` (D2-161's attribution).
- **C9.5c decided** (`d2166.md` complete): the third set leaned the other way in hunger (x0.895) with more sickness (x2.07, r -0.88); pooled over 21
  worlds that part hunger x0.994, births and deaths a person-year at par; people over 200-299 x0.982 (t -2.64) not repeated in 20-29 (t -0.77). Tools:
  `tc2/out/tools/s24/lean_pool.py` (three sets pooled), `lean_why.py` (per seed by cause from each pair's parting year; the placebo set too).

### Next, in order
1. Judge each landing check as it ends: `0_a` (build, cc, `_m5`), `0_b` (the full suite must be 0 failed, 0 skipped), the shots `s24t`/`s24tn`, `s24p`,
   `s24q` (open the PNGs in `tc2/out/shots/<tag>/`: does a three-tier realm read? sworn men at practice?), `0_d` (Web: 6/6, the realms frame cost
   `3b_region_realms` against `2_region` -> `s24/d2167.md`'s FILL).
2. When `8_a*`/`8_b*` and `6_c` are in: fill `d2164.md`, `d2165.md`; then `python tc2/out/tools/s24/append_decisions.py` (D2-164..D2-167); `25` needs no
   edit (C10.8's merge rewrites its C10 line).
3. C10.8 (`7_d`): judge the ten seeds against `_i2` (vital rates; raids, wounds; bonds, lordships; the askings: `demands:` lines); a matched placebo if a
   lean needs one; then merge main into `wip/c108-demand` (worktree `C:/Game Dev/tc-demand`; it gains the tools and the realms layer, no sim change),
   rebuild (`bin_dm2`; must reproduce `_dm1`), its 900 years (seeds 2, 4 from `s*_dm1/world_y300.tc2`) with the lordships measures (the greatest realm's
   share and tiers, not only the count of lords), land it (sim 91, D2-168), real shots of realms grown (`--realms`), full suite, Web.

## Session 24 (20 Oct 21:56 - 21 Oct ~01:25 IST; Control Room run #73 session 1) - context handoff with the job queue RUNNING; nothing of C10.7a/C10.7b/C9.5c landed yet; the realms map layer on main; C10.8 the asking built on a branch

### Exact state at the handoff (21 Oct 01:25)
- **main** = cd4f054 (+ this handoff's docs commit), sim 87: the realms map layer merged (4d40607, 12321eb, ae89777 of `wip/realms` fdd2572 - Game code
  only: `RealmsView`, `TerrainLod` uv5/uv6, `Terrain.shader`, `WorldObjectsView` map dyes and realm dyes kept apart, HUD Realms/R + legend, ShotRunner
  `-tc2realms`, `unity.mjs shots --realms`, WebBridge `on realms`, webtest `3b_region_realms`). Not ours: `.claude/launch.json` (modified),
  `Assets/Resources.meta` (untracked) - leave them.
- **RUNNING: the job queue** - `bash tc2/out/tools/s23/runq.sh` (background task **bcbn62z2b**, started 22:04; one low-priority job at a time, each gated
  RAM < 86 %, CPU < 85 %). `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/60b1de9e-5c98-4df9-9e8b-0a263e6d1792/scratchpad/q`
  (`runq.log`; `log/<job>.log`; `todo`, `done`; `touch $Q/stop` ends it after the current job). `lib.sh`/`runq.sh` point at that scratchpad (`S` = `N` = its
  parent; backups `*.bak_s23`). Job copies: `tc2/out/tools/s24/`; results: `tc2/out/tools/s23/results/`. A monitor (task b2hef5r43) reports failed jobs only.
  **Left in `$Q/todo` at 01:25, in order (~3 h)**: `4_e_b_i2_s2/s4` (the integrated rules - the landing branch, `bin_i2`, sim 90 - seeds 2 and 4, saved at
  year 300; s2 running), `4_e_c_l9i2_s2/s4` (their 900 years, `_l9i2`), `4_e_d_lords_l9i2` (`lordships --heads --margins --could --wrongs` at years 300/600/900
  -> `results/lordships_s{2,4}_i2_y*.txt`, on `bin_lt` = `wip/s24-tools`, its hash probe equal to `bin_i2`'s); `4_f_pw_*` (C9.5c's third seed set, 20-29,
  `_pw0`/`_pw1`) + `4_g_pool_pw`; `5_a` (C10.7a rebuilt as `bin_a9`; its tests and seed 5 must reproduce `_a7` -> `a9_ok`), `5_b..d` (C10.7a's 900 years:
  `_l9k0` seed 2, `_l9a7` seeds 2 and 4 -> `results/lordships_s*_l9*_y600.txt`); `6_a..b` (C10.7b's 900 years `_l9sx1`), `6_c` (the profile, `--oldsworn
  --oldmightarms` against as built); `7_a_i2_*` (the integrated rules' other eight seeds, `_i2`), `7_b` (C10.8's control: `bin_dm1 --olddemand` seed 4 =
  `_i2`), `7_c_dm1_*` (C10.8's ten seeds `_dm1`), `7_d` (`results/cmp_i2_dm1.txt`); `9_a` (`bin_m2` rebuilt in a new worktree `C:/Game Dev/tc-m2` at
  0b4540f; seed 4 must reproduce `_g2` -> `m2_ok`), `9_b_gq_*`, `9_c` (D2-161's attribution on tag `_gq` -> `results/cmp_z1_gq.txt`, `cmp_gq_g1.txt`).
- **Done in the queue (verified)**: the landing branch (`wip/s23b-int` ca25d71, `bin_i2`) **full suite 441 passed, 0 failed, 0 skipped**
  (`results/fullsuite_i2.txt`), floatcheck clean; C10.7b rebuilt (`bin_s2`, tc-sworn 71bb313) - Sworn 5, Training 4, Might 6, Lordship 4, Leaders 8,
  Defence 8, Taking 18, Determinism 6, SaveLoad 8, all 0 skipped; C9.5c rebuilt (`bin_c1`, tc-visits 8a270c4) - Visits 1 (9.6/0.0, news 8/0, as
  documented), Cross 14, Disease 7, Trade 7, Determinism 6, SaveLoad 8; **C10.7b's control** `--oldsworn` seed 2 = `_a7` byte for byte (`_sx0`); **C10.7b's
  ten seeds** `_sx1` (`results/cmp_a7_sx1.txt`) and its matched placebo `_sxp` (`cmp_a7_sxp.txt`, `cmp_sxp_sx1.txt`); **C9.5c's matched placebo** `_pvm`
  (`cmp_y1_pvm.txt`, `cmp_pvm_pv1.txt`) and replication on seeds 9-19 `_pw0`/`_pw1` (`cmp_pw0_pw1.txt`); the measurements for the next slice on main's ten
  worlds (`results/lordships_s*_y1_hm.txt`, `could_s*_y1.txt`, `wrongs_s*_y1.txt`, built from the measure-only worktree `C:/Game Dev/tc-s24m`, branch
  `wip/s24-tools87` - never to be merged: main's copy of the tool lands with `wip/s24-tools`); C10.8's build and tests (`bin_dm1`, db7e92e): **DemandTests
  4/4**, Lordship 4, Bread 5, Sworn 5, Might 6, Defence 8, Determinism 6, SaveLoad 8, all 0 skipped, floatcheck clean (`dm1_ok`); the realms layer's three
  `cc --src` compiles and two rounds of real shots.

### What the evidence says (written into the drafts)
- **C9.5c** (`tc2/out/tools/s23/d2166.md`, FILL: the third set): the hunger lean (x1.064, t 2.17 against `_y1`) is chaos at a land's ceiling - C9.5c acts late
  (twins identical until years 205-290), the hunger change is not the sickness change (r +0.08), seed 4's +907 is two crash famines in its last 15 years;
  the matched placebo spreads hunger as widely (sd 0.100 vs 0.097) and itself leans past |t| 2 (relief by sea x1.22); **against its placebo twin hunger x1.08
  (t 1.6), palisades x0.99**, epidemics x0.86 (t -1.95), sickness deaths x0.64 (t -2.02); seed 5's lost epidemic was chance. **The replication (9-19)**:
  hunger x1.03 (t 1.33, n.s.) but people over 200-299 x0.958 in the six diverging seeds (t -3.0) and deaths a person-year x1.015 (t 3.4, 6/6 up) - while
  the first ten showed x0.993 and x0.995. Pooled (15 diverging): people x0.979 (t -2.6), hunger a person-year x1.067 (t 2.7), deaths x1.003, births x0.995.
  No mechanism found (C9.5c removes false visits, news and wear across water; one candidate: island villages losing low-level exposure to sickness meet
  rarer, larger epidemics - seed 12's twin +1,643 sickness deaths). **The third set (20-29, `4_f`/`4_g`; then `python tc2/out/tools/s24/lean_pool.py`
  pools all three) decides**: if the population lean holds its sign and grows in t, investigate (sickness by set, immunity) before landing C9.5c - land
  C10.7a and C10.7b without it if need be (the handoff below, step 2); if it fades, land all three, the leans recorded.
- **C10.7b** (`d2165.md`, FILL: 900 years, profile): ten seeds at par in births and deaths a person-year; the open question answered (499 men in 80
  villages against 28 lords at year 300 - great households keep men: kept); people over 100-199 x0.924 (t -2.64) against `_a7` but **x0.939 (t -1.46)
  against its matched placebo twin**, whose own spread is large (one FU at year 30 made seed 7's twin found 23 villages and reach 6,441 people against
  ~2,000); the mechanism stronger against the twin - tributes x2.04, tribute agreed x1.77, loot x3.7, raids x2.4, wounds a person-year x1.38; the wall clock
  x1.11 was the machine's (the placebo on the same binary x1.27).
- **C10.7a** (`d2164.md`, FILL: the 900-year check `5_b..d`).
- **The next slice** (`tc2/out/tools/s24/c108_design.md`): measured on main's ten worlds, bonds outlive their lords (25 of 26) but hold by strength (25 of
  26 lords still stronger by the odds of a first bow) - succession at a death would change little; **formation is what limits the lordships**: 514 pairs
  within a day's walk where one could make the other bow, against 26 bonds; 24 free villages could commend themselves to a protector. **C10.8 the
  asking** chosen and built on `wip/c108-demand` (worktree `C:/Game Dev/tc-demand`, from the landing branch's tip; 3480b5e: rule `Conflict.Demanded`,
  `TributeDemanded` = 100, `TributeAgreed` ArgB 2, the page's reason, `--olddemand`, `--demandtally`, sim 91, DemandTests 4, docs `10` §12k and `25`);
  draft decision `tc2/out/tools/s24/d2168.md`; its control and ten seeds are queued (`7_b`..`7_d`) against the integrated rules' ten seeds `_i2`.
- **The realms layer** (`tc2/out/tools/s24/d2167.md`, FILL: the Web test's frame rates): first shots `tc2/out/shots/s24r3`, `s24r8` (and without the
  layer `s24r3n`, `s24r8n`) found gold read as ripe grain, dark dyes as earth, a faint edge, a mustard village up close, two realms 4.5 km apart in one
  colour; the second look `s24s3`, `s24s8` (map dyes, an even fill 0.72/0.43, a ~3 px band at the land's edge by the weight's derivative, a fade within
  450-1,300 m) reads at the region and orbit views; then realms' dyes made distinct planet-wide (fdd2572: orbit shows a hemisphere).

### Next, in order
1. Read the queue as it lands (`cat $Q/runq.log`; each job's log). `4_e_d` -> the integrated rules' 900 years (snowball? margins? formation?) - record in
   `c108_design.md` and D2-168's context; `4_g` + `lean_pool.py` -> C9.5c's decision; `5_*`, `6_*` -> D2-164's and D2-165's 900-year lines; `6_c` -> the
   profile line; `7_d` -> C10.8's ten seeds (judge by the vital rates, raids, wounds, bonds; then its own placebo and 900 years); `9_c` -> a line in D2-161.
2. **Land** (when the drafts have no FILL left): on main `git merge --no-ff wip/s23b-int` (C10.7a, C10.7b, C9.5c, sim 90) and `git merge --no-ff
   wip/s24-tools` (the lordships tools; it is the landing branch plus tools - merge after); fill the FILLs; `python tc2/out/tools/s24/append_decisions.py`
   (D2-164..D2-167, refuses any FILL); PROGRESS (draft `tc2/out/tools/s24/progress_s24.md`), `25` (C10.7a/b done strikes are on the branch), `18` §9 a
   paragraph for D2-167, `10` §12h "(done: D2-167)"; then queue `tc2/out/tools/s24/0_a_land_checks.sh` (bin_m5, floatcheck, `unity.mjs cc`, a v90 seed-2
   world `_m5`), `0_b_fullsuite_m5.sh`, `0_c_shots_m5.sh` (`s24p` sworn men at practice, `s24q` realms on the v90 world), `0_d_web.sh` (`s24web1`: also the
   realms layer's frame cost, `3b_region_realms` against `2_region`) - names starting `0_` jump the queue. If C9.5c is held back: build a landing branch of
   main + C10.7a + C10.7b (`wip/c107a-arms`, `wip/c107b-sworn`; sim versions renumber) - or land `wip/s23b-int` and revert C9.5c's commits on it.
3. **C10.8** after the landing: rebase or merge `wip/c108-demand` onto main (it already contains the landing branch), its ten seeds (`7_c`) against `_i2`,
   a matched placebo if a lean needs one, its 900 years (snowball?), real shots (realms growing; `--realms` on a `_dm1` world), full suite, D2-168, Web.

## Session 23, fifth context (20 Oct 2026, 05:06 - ~05:45 IST; Control Room run #63 session 5) - Arjun stopped the session; the queue stopped after C9.5c's evaluation and C10.7b's retest; nothing new on main but tools

### Exact state at the stop (every worktree committed; the queue runner stopped - nothing left running)
- **main** = f0e2dd4 (+ this handoff's docs commit): sim 87 - code as at bc99679 plus tools. New here: `unity.mjs cc --src DIR` (f0e2dd4) compiles another
  checkout's Unity sources (a worktree has no Library) against main's generated csproj files. `.claude/launch.json` (modified) and `Assets/Resources.meta`
  (untracked) are not ours - leave them.
- **Reviewed before landing** (a reading of the landing branch's diff, `git diff main...wip/s23b-int`; no fault found): `Retinue.Harvest` (release order and
  reasons, the oath to the person, the year's food between bins of one store; the lord's own sons may be sworn - fed from their own bin, no transfer:
  harmless), band formation with sworn men (`BandAtMost` still counts the unsworn, the band at most 36 + 12 < 255), `Might`/`Lasting` called only at the
  three harvest-time weighings (no daily cost), `Person.Sworn` saved and hashed only when set, the practice parity (the test's lived 34 h are six days, its
  COARSE 26.5 h five days and an evening: 0.94 against 0.88 sessions a man-day - at par), C9.5c's `Disease.Visited` keeps the lived odds in both
  `Disease.Visits` and `Conflict.News` (the denominators count every village within 6 km), a route that cannot be found is cached (`RouteCache._unreachable`:
  no search repeated daily).
- **The landing branch's Unity C# compiles** (`6_9_a2`: `cc --src` on tc-int2 bb2ec81 - Core, Game, Editor, Tests): its Game code (the shot view
  `act:<Activity>:sworn`) compiled for the first time before landing.
- **C9.5c's evaluation** (`bin_c1` = 8a270c4 rebuilt; D2-166 draft filled): tests pass, 0 skipped (VisitsTests: thirty COARSE days - visits along a way 9.6,
  across water 0.0, news 8 and 0; `--oldvisits` 9.6 and 24.8, news 8 and 18; Cross 14, Disease 7, Trade 7, Determinism 6, SaveLoad 8); **control
  `--oldvisits` seed 4 = `_y1`** (annals byte-identical, state hash 78635b5066ea574c); **ten seeds `_pv1`** against `_y1`: at par in the vital rates (people x0.996, t -0.10; years 200-299 x0.994; births x0.987; deaths x0.989; deaths a person-year x0.996); the mechanism shows - sickness x0.640 (t -2.01), epidemics x0.814 (t -2.35), sickness deaths x0.31 (seed 5, the archipelago: sickness 566 -> 1), palisades x0.946 (t -2.48: the news of deeds no longer crosses water), relief by sea x1.35 (t 1.89); **one lean to explain before landing: hunger deaths x1.064 (t 2.17; a person-year x1.071, t 2.25)** while all deaths a person-year stay at par - likely the deaths sickness no longer takes, taken by hunger instead, but unconfirmed (seeds 4 and 11 diverged early: people at the end x0.90 and x0.74; seed 4's hunger +907 with its sickness unchanged): check per seed and with a placebo (`results/cmp_y1_pv1.txt`, `d2166.md`).
- **C10.7b's SwornTests retest** (`6_9_d2`, bin_s2 rebuilt from tc-sworn 3e570e3): its first run failed one assertion - the settlement page's old wording ("3 men are sworn to the"; the page names the lord since the personal oath); fixed on both branches (tc-sworn 71bb313, tc-int2 ca25d71) and rerun directly after the queue stopped: **SwornTests 5/5**, with Training 4, Might 6, Lordship 4, Leaders 8, Defence 8, Taking 18, Determinism 6, SaveLoad 8 - all 0 skipped; `s2_ok` set (the 900-year jobs `6_d_*` are gated on it).
- **The queue** (task bc2qpqxfq) ran `6_9_a..c`, `6_9_d2` and stopped at `6_9_d3_stop` (`$Q/stop` exists). **Left in `$Q/todo`** (copies in
  `tc2/out/tools/s23/`): `6_9_d_int_fullsuite` (the landing branch's full suite: build `bin_i2`, `fs_i2_ok`), `6_9_e_ctl_sx0_s2` (C10.7b control `_sx0` =
  `_a7`), `6_9_f_sx1_01..10` (ten seeds `_sx1`), `6_9_g_cmp_sx1` (`results/cmp_a7_sx1.txt`), `6_a_g2_05..10` + `6_b_cmp_g2` (D2-161's attribution), `6_c_1..3`
  (C10.7a's 900 years on `bin_a9`: `_l9k0` from the control world `s2_k0`, `_l9a7` from `s2_a7` and `s4_a7` -> `results/lordships_s*_l9*_y600.txt`; the file
  names say `l9k1`, the tags inside `l9a7`), `6_d_1..2` (C10.7b's 900 years `_l9sx1` on `bin_s2`, gated on `s2_ok`), `6_e_profile_sx1` - about three hours. **To restart**: `rm $Q/stop`, then `bash tc2/out/tools/s23/runq.sh` in the background. `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q`; the binaries (`bin_a8`,
  `bin_s2`, `bin_c1`, `bin_m4`, ...) live in its parent. **If that scratchpad is gone**: rebuild each binary from its worktree (the `build` lines at the top
  of `6_8_x_rebuild_s2.sh`, `6_8_z_rebuild_c1.sh`, `6_9_d_int_fullsuite.sh` in `tc2/out/tools/s23/`), set `S`/`N`/`Q` in `lib.sh` and `runq.sh`, and
  requeue the jobs above - never the superseded first C10.7b version's copies there (`6_8_b_w1_*`, `6_d_*_l9w1_*`, `6_e_profile_sw1`: kept in `$Q/hold`). The saves the jobs read (`tc2/out/civ/s*_y1/`, `s*_a7/`) are in the repo's ignored `tc2/out`, not the scratchpad.
- **Worktrees live**: `tc-arms` (wip/c107a-arms 44808e5, C10.7a), `tc-sworn` (wip/c107b-sworn 71bb313, C10.7b), `tc-visits` (wip/c95c-visits 8a270c4,
  C9.5c), `tc-int2` (**wip/s23b-int ca25d71 - the landing branch: main a8ca460 + all three, sim 90**; ca25d71 = bb2ec81 + the SwornTests fix), `tc-realms` (wip/realms, below). Merged and
  removable: `tc-across`, `tc-bread`, `tc-carry`, `tc-cross`, `tc-dry`, `tc-fetch`, `tc-int`, `tc-might`, `tc-routes`, `tc-s23`, `tc-stubble`, `tc-tribute`,
  `tc-voy` (and older: `tc-g2`, `tc-g4`, `tc-merge20`, `tc-muck`, `tc-thresh`, `tc-wild` - check `git branch --merged main` before removing).
- **Started, work in progress: the realms map layer** (branch `wip/realms`, worktree `C:/Game Dev/tc-realms`, from main f0e2dd4; D2-167 when it lands;
  presentation only, no sim version): `Game/View/RealmsView.cs` (+ .meta) - the layer's state (`RealmsView.On`, `Version`), each realm village's wash
  (`WashOf`: its realm's colour, the seat 0.85, a tributary 0.62), the legend (each head of a lordship: villages, people), and chevrons in the realm's colour
  from each tributary toward its own lord along the way its tenth is carried (`Routes.Get` + `WayLines.Of`, Chaikin twice; straight over water), sized by
  the camera's distance (~16 px), spaced in steps of two, drifting toward the lord, none nearer than 450 m. Committed 7d418d3; it compiles (`6_9_a3`: `cc --src`, Core, Game, Editor, Tests). **Not wired yet** - to do,
  in order: (1) `Bootstrap`: add the component (Host, Rig); (2) `TerrainLod`: a `Realm` (Vector4) per vertex in `Colourise` - the field owner's wash
  (`owner`, weighted by the vertex's field share `farm`, smoothstepped) and a village's built ground (the `_near` loop's `clear`) by `RealmsView.WashOf`,
  premultiplied (rgb x a, a); zero when the layer is off; `SetUVs(5, p.Realm)` beside uv0-uv4 in the build and in `RecolourSome`; recolour every shown
  patch when `RealmsView.Version` changes; (3) `Terrain.shader`: `float4 uv5 : TEXCOORD5` in, `half4 realm : TEXCOORD6` out; after the woods, before the
  light: tint = rgb / max(a, 0.001), wash `albedo = lerp(albedo, tint * (0.45 + 1.1 lum), a * 0.75)`, an edge line where a crosses ~0.35 (`fwidth`; no
  branches - WebGPU's uniform control flow); (4) `Hud`: a "Realms" toggle (key R; the toggles' rect wider) and the legend (a swatch, the head's name,
  villages, people); (5) `ShotRunner`: `-tc2realms` sets `On` (and `unity.mjs shots --realms`); (6) `cc --src`, merge to main, real shots on a world with
  lordships (`--village lord`, views region, village, orbit), judge: do realms read from the village and region views? Then the Web build (60 fps?).

### Next, in order
1. Restart the queue (above) and read it: the landing branch's full suite must be 0 failed, 0 skipped; C10.7b's control, ten seeds (vital rates, raids,
   how many villages keep men - the open question: seed 4 had sworn men in 9 of 29 villages at year 300, only 2 of them lords), its 900 years and profile;
   C10.7a's 900 years; `_g2`. **C9.5c's hunger lean** (hunger deaths x1.06, t 2.2, all deaths at par): per seed, sickness deaths against hunger deaths,
   and a placebo (D2-155's: the same rules apart only by the first lived day) for the spread between twin worlds. Fill `d2164.md`, `d2165.md` (`d2166.md` is complete: filled with the ten seeds; its one lean - hunger deaths x1.06, t 2.2, with all deaths at par - to explain before landing).
2. If at par: on main `git merge --no-ff wip/s23b-int`; DECISIONS D2-164, D2-165, D2-166 from the drafts; PROGRESS; queue `7_a_land_checks.sh`,
   `7_b_shots_sworn.sh`, `7_c_web.sh` (s23web5) and judge the shots. If one slice is not at par, land the others (rebuild the landing branch without it;
   sim versions renumber).
3. The realms map layer (`wip/realms`, the steps above) - it does not wait for the landing (presentation only; its shots want a world with lordships:
   `_y1` saves are sim 87 = main now; after the landing use `_m5` from `7_a`).
4. Then by evidence: rising together (tributaries of one lord weighing together against it) if lordships snowball; a lord's death and its tributaries.

## Session 23, fourth context (20 Oct 2026, 02:54 - ~05:05 IST; Control Room run #63 session 4) - C9.5b checked; C10.7a, C10.7b, C9.5c built and in evaluation; context handoff with the queue running

### Exact state at the handoff (05:10; every worktree committed and clean)
- **Stale verification noted by Control Room** (re-checked at the handoff): main's code since bc99679 is tools-only (the survey's four worlds on the
  main-line build reproduce `_y1` byte for byte; `unity.mjs cc` ok on 874a27e, after the shot views); **the full suite has not run on main's exact
  code** since bc99679 - the landing branch's full suite (`6_9_d`) covers it.
- **main** = bc59303 (+ this handoff): sim 87; tools merged (`voyages` probe, voyage views `dugout`/`voyageclose`, `--visittally`; a8ca460 fixed
  db4b8ba's headless compile break). C9.5b complete: Web `s23web4` 6/6 (browser = dotnet = Mono), aimed shots `s23n1`/`s23n2` read at 45 m, lived and
  COARSE dugout crossings agree (D2-163 updated).
- **C10.7a might by lasting strength** (`wip/c107a-arms`, `C:/Game Dev/tc-arms`, sim 88): tests pass (0 skipped); control `--oldmightarms` = `_y1`
  (state hash 7354e64f798fe019); **ten seeds `_a7` at par** (people x1.018 t 1.24; tribute agreed x1.20 t 1.78; the two weighings disagree 17 times in
  2,479) - in `d2164.md`. Its 900-year check is queued (`6_c_*`: `_l9k0` baseline and `_l9a7`, seeds 2 and 4).
- **C10.7b sworn men** (`wip/c107b-sworn`, `C:/Game Dev/tc-sworn`, tip 3e570e3, sim 89): **the oath is now to the person** first among the village's
  people (1557053) - the household-id version released 2,956 men in seed 4 as their oath household merged and re-swore them the same harvest. The first
  version's control (`_sw0` = `_a7`) and four seeds `_sw1` ran (held: the rest in `$Q/hold`). The personal-oath build `bin_s2`: Training, Might,
  Lordship, Leaders, Defence, Taking, Determinism, SaveLoad pass; SwornTests' wording fixed (3e570e3) - **retest queued `6_9_d2`** (sets `s2_ok`), then
  control `6_9_e` (`_sx0` = `_a7`), ten seeds `6_9_f` (`_sx1`), `6_9_g` (`results/cmp_a7_sx1.txt`), 900 years `6_d_*_l9sx1`, profile `6_e_profile_sx1`.
  Open question for the evaluation: seed 4 had sworn men in 9 of 29 villages at year 300, only 2 of them lords - the rule lets any first household
  with grain keep men (emergent thegns); judge by the vital rates, raids and the 900-year check. Draft `d2165.md` (personal oath written in).
- **C9.5c people across water** (`wip/c95c-visits`, `C:/Game Dev/tc-visits`, tip 8a270c4, sim 88 on main's line): COARSE visits keep a lived day's
  odds and the visits to a village no way on foot reaches are lost (a first version spread them - found in review, fixed). Survey in `c95c_design.md`.
  Rebuild and tests `6_8_z` running at 05:03 (VisitsTests passed on it: visits along a way 9.6 - the old rule's 9.6 - across water 0; news 8 and 0),
  then control `6_9_a` (`_pv0` = `_y1`), ten seeds `6_9_b` (`_pv1`), `6_9_c` (`results/cmp_y1_pv1.txt`). Draft `d2166.md`.
- **The landing branch** `wip/s23b-int` (`C:/Game Dev/tc-int2`, tip bb2ec81): main a8ca460 + C10.7a + C10.7b (personal oath) + C9.5c (kept odds), sim
  90, docs (`10` §12i-j, `24` §5, `25`, `23` §3), shot view `act:<Activity>:sworn`. `6_9_d_int_fullsuite` (build `bin_i2`, full suite -> `fs_i2_ok`).
- **RUNNING: the queue** (task bc2qpqxfq; `Q` below): `6_8_z` -> `6_9_a..c` -> `6_9_d2` -> `6_9_d` -> `6_9_e..g` -> `6_a_g2_*`, `6_b` -> `6_c_*` ->
  `6_d_*` -> `6_e_profile_sx1` (about three hours from 05:05). Run tags were renamed to unused ones (`a7`, `sx0`/`sx1`, `pv0`/`pv1`).
- Written, not queued (after landing on main): `7_a_land_checks.sh` (bin_m5, cc, a v90 seed-2 world `_m5`), `7_b_shots_sworn.sh` (`s23p`:
  `act:Train:sworn` at the lord's afternoon), `7_c_web.sh` (`s23web5`).

### Next, in order
1. Read the queue: C9.5c's control and seeds; C10.7b's (personal oath) retest, control, seeds - the vital rates, raids, how many villages keep men and
   how many; the integration's full suite; C10.7a's and C10.7b's 900-year checks (snowball?); the profile; `_g2` (D2-161's attribution).
2. If at par: on main `git merge --no-ff wip/s23b-int`; DECISIONS D2-164, D2-165, D2-166 from the drafts; PROGRESS; queue `7_a`, `7_b`, `7_c`.
3. Then by evidence: rising together (tributaries of one lord weighing together) if lordships snowball; a lord's death and its tributaries; a polity
   map layer (UI).

## Session 23, third context (20 Oct 2026, 00:04 - ~02:55 IST; Control Room run #63 session 3) - C9.5a and C9.5b landed; C10.7a drafted; context handoff with the queue running

### Exact state at the handoff (20 Oct ~02:45)
- **main** = bc99679 (+ this handoff's docs commit): **C9.5a landed** c8f6f5d (`--no-ff` of `wip/c95-across`; the lord's fetch, the trade fix and the
  premise fix developed on `wip/c95-fetch` and fast-forwarded into it), shot runner `--after tribute` across water cbc99ab, DECISIONS D2-162 and
  D2-161's 900-year line 6482ab0; **C9.5b landed** 0a8b0b0 (`--no-ff` of `wip/c95b-cross`), DECISIONS D2-163 and `25` a2e244d, PROGRESS bc99679. **Sim
  version 87.** Not ours: `.claude/launch.json` (modified), `Assets/Resources.meta` (untracked) - leave them.
- **Verified on main**: after C9.5a (6482ab0) - builds (`bin_m3`), floatcheck clean, `unity.mjs cc` ok, **full suite 427/427**
  (`results/fullsuite_m3.txt`), **Web `s23web3` 6/6**, browser = headless dotnet = editor Mono bit for bit (frame rates 27-52 fps at the village and region
  views on a busy machine: not evidence). After C9.5b (bc99679) - builds (`bin_m4`), floatcheck clean, `unity.mjs cc` ok, **full suite 433/433**
  (`results/fullsuite_m4.txt`, 02:53); the shots and Web `s23web4` still running.
- **C9.5a's evidence** (`results/cmp_g1_x2.txt`): control `--oldacross` = `_g1` (annals and state hash); the old rule's survey (`--oldacross
  --acrosstally`, = `_g1` too): 91 % of seed 5's trades and 99.5 % of its relief crossed water at once; ten seeds `_x2` at par in the vital rates; no tenth
  went by sea (bonds across water no longer arise: their relief now crosses only in boats). **C9.5b's** (`results/cmp_x2_y1.txt`): control `--oldcross` =
  `_x2`; ten seeds `_y1` at par; voyages x2.4, trades x1.2, relief x1.5; boats made to cross in eight of ten worlds (72); the archipelago (seed 5) made 11
  dugouts, trades 174 -> 618, hunger deaths 1,752 -> 1,283. `lordships` on the `_x2` saves: no lordship bound across water at year 300 (no shot target
  for the tenth by boat; the shots frame any boat on a voyage).
- **RUNNING at the handoff: the session-23 job queue** (`bash tc2/out/tools/s23/runq.sh`, task bc2qpqxfq; `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q`, its log `runq.log`, job logs
  `log/<job>.log`; `touch $Q/stop` ends it after the current job; copies of every job in `tc2/out/tools/s23/`). In order (the full suite done: 433/433): `6_7_c5_might_measure` (C10.7a measured first: builds `tc-arms` - the WIP - to `bin_a7`, runs
  `lordships --might` on the ten `_y1` saves -> `results/might_s*_y1.txt`: villages' lasting strength a head, bonds held by heads and by lasting
  strength), `6_7_d_shots_cross` (real shots `s23m5` seed 5's archipelago, `s23m4` seed 4: `voyage`, `voyagenear`, `voyagestay`, `region`; the log tells
  each framed crew's doing - read `shots_s23m*.txt` in the scratchpad and the PNGs in `tc2/out/shots/`), `6_7_e_web` (Web `s23web4` on main with
  C9.5b), `6_a_g2_05..10` + `6_b_cmp_g2` (D2-160 alone on the landed binary: attributes C10.6's tribute lean -> `results/cmp_z1_g2.txt`,
  `cmp_g2_g1.txt`, a line in D2-161). About 75 minutes in all.
- **C10.7a a lordship's might by lasting strength - DRAFTED, NOT BUILT, NOT TESTED**: branch `wip/c107a-arms`, worktree `C:/Game Dev/tc-arms`, tip
  d182cf2 (from main bc99679). `Conflict.Kit` (the village's tools, arms, iron a head - as `Strength` weighs them), `Conflict.Lasting` (people of
  fighting age by kit x (1 + 0.6 x fighting skill); the day's health and wounds left out, C10.5's reason for counting heads), `Might` and the call's men
  weighed so; `--oldmightarms` (`Conflict.MightArmsOff`) keeps C10.5's heads; texts ("could bring the strength of N ... its own came to M"); `lordships
  --might`. Design and evidence: `tc2/out/tools/s23/c107_design.md`.
- Worktrees merged and removable: `tc-cross`, `tc-across`, `tc-fetch`, `tc-int`, `tc-carry`, `tc-might`, `tc-routes`, `tc-s23`, `tc-bread`, `tc-dry`,
  `tc-stubble` (`tc-tribute`: its one unmerged tools commit is on main in another form). Live: `tc-arms`.

### Next, in order
1. Read the queue: the shots (do boats crossing read? any dugout framed - the log says "paddling ... in the household's
   dugout"), `s23web4` (6/6, three runtimes identical?), `might_s*_y1.txt` (how far lasting strength parts from heads; how many bonds would hold by it),
   `_g2` (is the tribute lean D2-160's?). Record them in PROGRESS (and D2-161 for `_g2`, D2-163 for the shots and Web).
2. **C10.7a**: finish on `wip/c107a-arms` - MightTests to the new rule (premises `Triple`/`Pair` by `Lasting`; the expected call by `Kit` and skill;
   the text check "came to"), a test where arms and skill decide a bow that heads would not, `--oldmightarms` in `Commands.cs`, sim version 88; build,
   tests (Might, Bread, Defence, Leaders, Lordship, TributeCarried, Determinism, SaveLoad), control `--oldmightarms` = `_y1`, ten seeds, the 900-year
   check (does arming and training make lordships snowball?). The measure decides how much it matters.
3. Then **C10.7b sworn men** (the lord's first household feeds men of its own village who practise daily and go with every band - `c107_design.md`),
   **C9.5c people across water** (a COARSE day's visits still carry news and sickness across water at once - `Conflict.News`, `Disease` - while a lived
   visitor goes only along a way), a polity map layer (the standards do not read at the village view).

## Session 23, second context (19 Oct 21:26 - 20 Oct ~00:10 IST; Control Room run #63 session 2) - C10.5, D2-160, C10.6 landed and validated; C9.5a built, in evaluation; context handoff with the queue running

### Exact state at the handoff (20 Oct 00:05)
- **main** = 94c0700 (clean but `.claude/launch.json` modified and `Assets/Resources.meta` untracked - not ours, leave them). Landed: a19c077 (`--no-ff`
  merge of `wip/s23-int` = C10.5 + D2-160 + C10.6 as amended on `wip/c106b-carry`), DECISIONS D2-159, D2-160, D2-161 (464e72c; their evidence lines updated
  after: profile 7d5ea8b, shots 8b02684), view and tool fixes (420ef71, 12dc99c, 4e736cb), PROGRESS, this handoff, both NEXT_SESSION_PROMPTs. Sim version 85.
- **Verified on main** (rechecked at the handoff against the raw outputs): full suite 419/419 (`results/fullsuite_p1.txt`), control `--oldtribute --oldways` =
  C10.5's `_z1` byte-identical, ten seeds `_g1` vs `_z1` at par in the vital rates (`results/cmp_z1_g1.txt`), the route probe on the landed world equal hashes
  over 3/6 lived days and 3 COARSE years, `unity.mjs cc` clean (07c21b5), **Web `s23web2` 6/6** (browser = headless dotnet = editor Mono bit for bit; every
  view faster than `s23web1`; the machine was busy - a quiet-machine fps measurement is still owed), real shots `s23c`-`s23k` (carriers with handcarts on
  the road to the lord `s23k`; standards read up close, not from the village view - a map layer is wanted), C10.5's 900-year checks (no snowball, bonds
  hold longer), the profile (might and carrying cost nothing per village-day).
- **Open on main's record**: tribute agreed x0.74 (t -2.2) in `_g1` vs `_z1` - D2-160 alone leaned the same way; `6_a_g2_*` (ten seeds, D2-160 alone on
  `bin_m2`: `--oldtribute`) + `6_b_cmp_g2` attribute it -> `results/cmp_z1_g2.txt`, `cmp_g2_g1.txt` -> PROGRESS and a line in D2-161.
- **C9.5a grain across water goes by boat** - branch `wip/c95-across`, worktree `C:/Game Dev/tc-across`, tip 39b7ce4 (clean), sim 86, draft decision
  `tc2/out/tools/s23/d2162.md`, design note in `24` §5 on the branch. Within a day's walk grain moves at once only along a way on foot; relief and
  give-back across water go by sea (the giver's boats; give-back also the giver's come to fetch) or not at all; the tenth across water is set aside where
  a way by sea joins the landings and the tributary has a plank boat, rowed over by it (lived `ForTribute` bookings delivered at the lord's landing;
  COARSE at once). `--oldacross` restores the landed code; AcrossTests 3. **Survey on the landed code** (`_a1`, annals byte-identical to `_g1`): 1,679
  relief gifts (~604,000 FU) and 1,150 gifts back (~58,000 FU) crossed water at once in ten worlds; 4 bows across water; of 444 no-way tenths 366 had a
  sea way - **the tributary never had a plank boat (0), the lord at 76**: as built, C9.5a carries no tenth by sea. **Its first build failed** on a test
  premise (CS0177, fixed 39b7ce4) - NOT YET BUILT OR TESTED since; `5_e2_0` (build, floatcheck, ten test classes -> `x1_ok`) and `5_e2_1` (control
  `--oldacross` = `_g1`) are requeued; its ten seeds `5_e2_2_*` and `5_e2_3_cmp_x1` are held in `$Q/hold` for the lord's fetch.
- **RUNNING at the handoff: the session-23 job queue** - `bash tc2/out/tools/s23/runq.sh` (background task bc2qpqxfq; a monitor bh5wj1q6y on its log
  expires by itself). `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q`
  (`runq.log`, `log/<job>.log`, `todo`, `done`, `hold`; `touch $Q/stop` ends it after the current job; copies of the jobs in `tc2/out/tools/s23/`).
  Running: `5_e_l9g1_s2` (from 23:59: the landed code's 900-year check, seed 2 -> `results/lordships_s2_l9g1_y600.txt`, `tc2/out/civ/s2_l9g1.txt`; compare
  with C10.5's `lordships_s2_l9z1_y600.txt`: lordships with the carried tenth over centuries, the ways, the bonds with no way on foot). Then, in order:
  `5_e2_0`, `5_e2_1`, `6_a_g2_01..10`, `6_b_cmp_g2` (about an hour in all). Binaries in that scratchpad: `bin_m2` (main), `bin_p1` (main's code before
  the docs), `bin_a1` (main + the survey tallies), `bin_x1` (C9.5a, rebuilt by `5_e2_0`).
- Worktrees merged and removable: `tc-int`, `tc-carry`, `tc-might`, `tc-routes`, `tc-tribute`, `tc-s23`, `tc-bread`, `tc-dry`, `tc-stubble`; live: `tc-across`.

### Next, in order
1. Read the queue: `5_e2_0` (C9.5a builds and its tests pass? fix and requeue if not), `5_e2_1` (control byte-identical?), the 900-year check of the landed
   code, `_g2` (is the tribute lean D2-160's?) -> PROGRESS; a line in D2-161 for the attribution and the 900-year check.
2. **C9.5a: the lord's boats fetch the tenth** (on `wip/c95-across`): a fetch booking from the lord's landing (like give-back's `ForFetch`; nothing aboard
   going out), the tenth taken aboard at the tributary's landing from what is set aside, the dues settled at pickup (`Brought`'s `SettleBy` and `Repaid`
   parts) and the grain into the lord's store and first household at its own landing (`Unload`); COARSE: the lord's free boats at once; set aside where a
   way by sea joins the landings and either side has a plank boat; the pages ("the lord's boats come for it"); a test (COARSE and lived). Then rebuild
   (`5_e2_0`), control, and requeue the held `5_e2_2_*`, `5_e2_3_cmp_x1` -> judge against `_g1` (vital rates; relief by sea vs at once - island
   worlds will change: seed 5 had 178,927 FU of relief across water at once) -> D2-162 from the draft, merge, `unity.mjs cc`, full suite, Web.
3. Then by evidence: C9.5b people across water in boats (island woodcutters, a town on two shores: lived legs by boat), C10.7 the lord's household men
   (seed 2's lords hold 3-10 years of their need, seed 4's spends it), a polity map layer (the UI pass).

## Session 23 (19 Oct 2026, 18:54 - ~21:25 IST; Control Room run #63 session 1) - context handoff; ONE job queue still running

### Exact state at the handoff (21:25)
- **main** (docs only since 3712056): C10.4 merged (ca3b4c8, `--no-ff`), DECISIONS D2-157 and D2-158 (3712056), `25` C10.4 done, PROGRESS, this
  handoff, `docs/tc2/NEXT_SESSION_PROMPT.md`, the root `NEXT_SESSION_PROMPT.md`. Sim version 82. `.claude/launch.json` (modified) and
  `Assets/Resources.meta` (untracked) are not ours - leave them.
- **`wip/s23-int` (worktree `C:/Game Dev/tc-int`, tip c5258d2) = everything to land**: main 0402dba + `--no-ff` merges of **C10.5** (5a0c4f1, sim 83),
  **D2-160** (a5583f4, 84) and **C10.6** (dd81998, 85) + a tools commit. Builds clean (`bin_i1` = dd81998), floatcheck clean; on `bin_i1`
  RouteGroundTests 3/3, TributeCarriedTests 5/5, MightTests 4/4 (21:18). To land: on main `git merge --no-ff wip/s23-int` (main has docs commits only
  since 0402dba). Component branches (worktrees): `wip/s23-tools` (`tc-s23`), `wip/c105-might` (`tc-might`), `wip/route-ground` (`tc-routes`),
  `wip/c106-tribute` (`tc-tribute`); older merged worktrees (`tc-bread`, `tc-dry`, `tc-stubble`) may be removed.
- **Decision drafts with their evidence** (write them into DECISIONS when landing): `tc2/out/tools/s23/d2159.md` (C10.5 - FILL the 900-year check),
  `d2160.md` (D2-160 - complete), `d2161.md` (C10.6 - FILL the control, ten seeds, the ways, the shots).
- **RUNNING: the session-23 job queue** - `bash tc2/out/tools/s23/runq.sh` (background task bc2qpqxfq; a monitor bf5ldakdg on its log expires by itself).
  Queue dir `Q` = `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/db1b5af7-2c1c-4e6f-a5ff-c8b25b1a81c2/scratchpad/q` (`todo`,
  `done`, `log/<job>.log`, `runq.log`; `touch $Q/stop` ends it after the current job; copies of every job in `tc2/out/tools/s23/`). Binaries in that
  scratchpad: `bin_m1` (main), `bin_t2` (tools), `bin_c2`/`bin_c3` (C10.5; c3 with the indexed kin test), `bin_r2` (D2-160), `bin_i1` (integration).
  Remaining at 21:25, in order (about two hours): `3_d_l9z1_s2/s4` (C10.5's 900-year check -> `tc2/out/tools/s23/results/lordships_s*_l9z1_y600.txt`:
  do lordships now grow, hold or snowball?), `3_t_0..3` (C10.6: build `bin_t7` + tests, `--oldtribute` = `_z1`?, ten seeds `_t1`, `results/cmp_z1_t1.txt`,
  the ways between the bound and their lords and each lord's surplus), `4_a_fullsuite_i1` (`results/fullsuite_i1.txt`), `4_b_f1_*` (ten seeds of the
  integration), `4_c_cmp_probe_f1` (`results/cmp_t1_f1.txt`; the probe on `s2_f1` - equal hashes expected). Run outputs: `tc2/out/civ/s<seed>_<tag>.{txt,csv}`
  and `s<seed>_<tag>/`. **If the scratchpad is gone**: rebuild a binary from its worktree (`2_3_build_c2.sh`, `3_cr_0_build_r2.sh`, `3_t_0_build_t7.sh`;
  the integration: `dotnet build -c Release` in `C:/Game Dev/tc-int/tc2/headless/app`) and rerun the jobs not done (set `S`/`N` in `lib.sh`).
- Version caution: C10.6's branch alone also says sim version 85 (without D2-160's `WayForest`) - read `_t1` saves only with `bin_t7`, `_f1` with `bin_i1`.
- **Timing on this machine is not evidence**: the user's Chrome and this session's builds ran alongside the queue; `_z2` (the indexed kin test, byte-identical
  results) ran slower than `_z1`. Measure speed only on a quiet machine or by the profiler's counts (`run --profile`).

### Verified this session
- C10.4 on main: Unity C# compiles (`unity.mjs cc`); **Web build and test `s23web1` 6/6**; browser (WebGPU, IL2CPP) = headless dotnet = editor Mono, bit
  for bit. Its frame rates were half session 18's because the user's Chrome ran heavily throughout - re-measure on a quiet machine before claiming 60 fps.
- **The 900-year check of C10.4** (`_y3` seeds 2 and 4 to year 900; `lordships`): no snowball, no growth - 4-9 lords among 27-35 villages, the largest
  lordship never above 3-4 villages; all 63 ended bonds ended "the lord no longer the stronger" (a one-day headcount; a lord by its own village): seed 2's
  year 464, a mild epidemic (12 villages, 6 dead), ended five lordships; bonds made the day after a raid ended when the beaten village's wounded mended.
- **Real shots** `tc2/out/shots/s23a` (tributary Stevoumstead), `s23b` (lord Trimrornby), seed 2 `_y3`: the standard in the lord's purple reads up close
  (edge-on a thin strip); at the village and region views nothing shows the bond.
- **C10.5**: MightTests 4/4 (+ Bread, Defence, Leaders, Lordship, Determinism); `--oldmight` seed 4 = `_y3` byte-identical; ten seeds `_z1` vs `_y3` at par
  (people x1.006, births x1.003, deaths x1.002; raids x0.89 n.s.; lords x1.04); in the first 300 years old and new weighings mostly agree (seed 11: bowing
  to raiders the old count 15, might 4). `KinIndex` (5983f04): `_z2` = `_z1` byte-identical on seeds 8 and 2.
- **D2-160**: the probe on `s2_y3`: three lived days b5329437ba119770 alone, 8101ffe7abef7b57 with the view's 109 routes built at load. Fixed:
  RouteGroundTests 3/3, SaveLoad 8, Afoot 6, Determinism 6, Migration 9; `--oldways` = `_y3` byte-identical; **the probe on its own world: equal hashes
  over 3 and 6 lived days and 3 COARSE years**; ten seeds `_r2` vs `_y3` at par (`results/cmp_y3_r2.txt`).
- **C10.6**: TributeCarriedTests 5/5 (COARSE: 100 FU in three loads in three days, the way worn; lived carriers bring the loads; a bond's end gives back what
  is due; `--oldtribute` instant; a save mid-carry continues identically); Bread, Defence, Leaders premises carry the tenth in; neighbours pass.

### Next, in order
1. Read the queue's results. C10.5: if its 900-year check shows lordships snowballing, a limit grounded in the record (reach by travel time, a lord's
   death) comes before landing; if they hold or grow plausibly, land. C10.6: control byte-identical, vital rates at par, the ways between the bound and their
   lords. The integration: full suite 100 %, `_f1` at par with `_t1`, the probe's hashes equal.
2. Land: DECISIONS D2-159, D2-160, D2-161 from the drafts; `git merge --no-ff wip/s23-int` on main; `node tc2/scripts/unity.mjs cc` on main; PROGRESS.
3. A v85 seed-2 world (`run --seed 2 --years 300 ... --snap 300` with main's binary) for **real shots** (`unity.mjs shots --load ... --village tributary`
   and `--village lord`, views `standard,standardlow,village,region`; carriers on the way after the tributary's harvest via `--doy`): do the standards and
   the converging roads now read? Then the **Web build and test** (`tc2/out/tools/s23/1_a_web.sh` with a new TAG) on a quiet machine.
4. The next slice by evidence: the lord's household men (C10.7, `23` §3: `lordships` prints each lord's first household's grain against its needs and the
   tribute it took in) or C9.5 boats across the water (island woodcutters; Gaby on two shores - needs a lived "legs by boat" model: a boat outing is one
   out-and-stay activity and the walk home starts from the water point).

## Session 22 (16 Oct 22:19 - 17 Oct ~02:15 IST; Control Room run #51 session 2) - stopped by Arjun; report `SESSION_22_REPORT.md`

### Exact state at the stop
- **main**: C3.7d merged (9d68eaa) + **D2-155**; **D2-156 merged** (f200b9c) + its decision (40f4a18); PROGRESS, this handoff, the report. Sim
  version 81. `.claude/launch.json` (modified) and `Assets/Resources.meta` (untracked) are not ours - leave them.
- **Branch `wip/c104-bread`** (worktree `C:/Game Dev/tc-bread`, tip **08e16b2**, sim version 82, NOT merged): **C10.4 bread and dependence**
  (`10` §12f; 1f05c2b, f4bffc6), main merged in (b4dd299), shots `--village tributary|lord` (863d170, de22762), **D2-158** (08e16b2: a COARSE day's
  muck waits for the household's own roof). Decision drafts with their evidence: `tc2/out/tools/s22/d2157.md` (fill "Full suite" and "Unity C#"),
  `d2158.md` (fill "Full suite").
- **The queue stopped** (02:13:57) after its last job, **the full suite on `bin_b4`** (the branch tip's code: main + C10.4 + D2-158): **403 passed,
  0 failed** (`tc2/out/tools/s22/results/fullsuite_b4.txt`; bin_b3 had 402/403 - the one failure D2-158 fixes). Nothing is running. Held, not run: `i_g_cc_bread3.sh`
  (`unity.mjs cc` on the bread tip), `j_l9_s2.sh`, `j_l9_s4.sh` (the 900-year check) - in `$Q/hold` and `tc2/out/tools/s22/`. To run jobs again,
  start a fresh runner (copy `tc2/out/tools/s21/runq.sh` and `lib.sh`, set `Q`/`S`/`N` to the new scratchpad, copy the binaries `bin_b4`... or
  rebuild).
- Worktrees: `tc-dry` (wip/dry-ways, merged - may be removed), `tc-stubble` (merged), `tc-bread` (C10.4, to merge), older ones as before.
- Saves (v82): `tc2/out/civ/s*_y3/world_y300.tc2` (ten seeds, the final rules), `s*_y2` (C10.4 without D2-158). v81: `s*_x2`.

### Verified this session
- **D2-155**: twenty seeds at par a person-year; pig-years x0.98; a placebo (`_u4`/`_u7`, the same rules apart only by the first lived day - a
  300-year run lives its first day before going COARSE) shows end-of-run pig counts and sickness are noise at ten seeds.
- **D2-156**: AfootTests 6/6 and 66 neighbours, floatcheck; `--wetways` = `_u7` byte-identical; the same save 0.178 % -> 0.013 % of person-hours in
  the water; ten seeds `_x2` at par a person-year (seed 4's epidemic phase drives the people x0.91); `woodways`: 22 of 164 villages fetch other
  woods, crofts x0.99. Open: island woodcutters (3 villages in ten worlds), a town on two shores - boats across water (C9.5).
- **C10.4** (`_y2` against `_x2`): vital rates untouched; lords 8 -> 21, tribute agreed 9 -> 35 (21 for bread), the lord's call answered (471 men),
  raids 79 -> 118. BreadTests 5/5 and seven neighbour classes; `--nobread` = `_x2`; Unity cc ok on de22762.
- **D2-158** (`_y3` against `_y2`): MigrationTests 9/9 (fails again with `--oldmuckorder`), neighbours pass; control byte-identical; vital rates at
  par, foundings x1.14.

### Next, in order
1. The full suite passed (403/403): `git merge --no-ff wip/c104-bread` on main; DECISIONS D2-157 and D2-158 from the drafts (fill "Full suite:
   403/403 on the branch tip's code"); PROGRESS; `25` (C10.4 done); `node tc2/scripts/unity.mjs cc` on main (de22762 compiled; 08e16b2 changes Core only).
2. The Web build and test on the final main (`tc2/out/tools/s22/h_a_web.sh`: `unity.mjs build-web --jobs 2`, `webtest.mjs --tag s22web1
   --export`; saves v82).
3. Real shots of a bread lordship (`node tc2/scripts/unity.mjs shots --tag s23a --load "tc2/out/civ/s2_y3/world_y300.tc2" --village tributary --views
   standard,standardlow,village --hour 11`; then `--village lord`) - the standards in the lord's colour; D2-156 at a lakeside village.
4. The 900-year check on `_y3` seeds 2 and 4 (`j_l9_*.sh`): lordships over long runs (do they snowball or hold?).
5. Then by evidence: C9.5 boats across the water (island woodcutters, towns on two shores), or the lordships' next mechanism (C10.5).

## Session 21 (16 Oct 2026, 19:12-22:05 IST; Control Room run #51 session 1) - context handoff; ONE job queue still running

### Exact state
- **main c106378** + this handoff's docs commit (clean but for `.claude/launch.json`, `Assets/Resources.meta` - not ours). Nothing of session 21's
  code is on main yet.
- **Branch `wip/c37d-stubble`** (worktree `C:/Game Dev/tc-stubble`, tip **161fa63**, sim version 80, NOT merged; main c106378 merged into it as
  9b81854). Session 21's commits on it: 5193bb0 **the hands' cull by what loads the hands** (`Livestock.Spare` mode 0 by the spares' food,
  `--oldhands`; tools-only herd diagnostics), b837a30/c4f9347 the stubble drawn as its herds eat it (`TerrainLod.CropColour`), shots `--doy D`
  and the season log, d8bf37d **a household's field cell is one its hands walk to dry-shod** (`FieldSpots.DryWay`, `--wetfieldspot`), tools
  `wet --doy`, `crowds`, 905d3d6 Unity metas (FieldSpots/Stubble/StubbleTests), 0e93fb7-180ad85 shot views `muck`/`mucklow` and **the muck heap
  placed clear of the croft's beds, the pen and the woodpile/cart** (`WorldObjectsView`), 161fa63 docs (`26` §2k session 21, `08` culls, `27` §9).
- **RUNNING: the session-21 job queue** - `bash tc2/out/tools/s21/runq.sh` (background task `ba1sj2iw2`), queue dir `Q` =
  `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/4c5113ad-667d-4fde-9df2-13e8ffdf1a90/scratchpad/q` (jobs in `todo`,
  `done`, `log`, `runq.log`; `touch $Q/stop` ends it after the current job). At 22:10 29 jobs left (~55 min; the dry-way binary's tests
  `1_zz_tests_s6` passed), `6_u7_*` (18 seeds left, the branch's final rules, `--stocktally`),
  `7_zz_cmp_c6_r4`, `8_c6_*` (control seeds 9-19), `9_zz_cmp_c6_u7_10`, `9_zz_cmp_c6_u7_20`. Outputs: `tc2/out/civ/s*_{c6,u7,r4}.{txt,csv}`;
  comparisons `$N/cmp_*.txt` (`N` = the queue's scratchpad). **If the scratchpad is gone**: every run is in `tc2/out/civ` - redo a comparison with
  `cd tc2/out/civ && python ../tools/s21/cmp.py c6 u7 4 5 1 6 11 2 3 7 8 42 9 10 12 13 14 15 16 17 18 19`; missing runs: rebuild the branch's headless
  (`tc2/out/tools/s21/q_done/0_x_build_s6.sh` - Core unchanged since d8bf37d) and rerun the jobs in `tc2/out/tools/s21/q_todo` (set `N`/`S` in `lib.sh`).
  Copies of all jobs, logs and results: `tc2/out/tools/s21/{q_todo,q_done,q_log,results}`.
- Saves: `tc2/out/civ/s4_u4/world_y300.tc2`, `s4_u7/world_y300.tc2` (branch rules), `s4_c7/world_y300.tc2` (the control on the branch binary, v80).

### Verified
- **The pigs (session 20's open question) traced**: not grass, mast or hard times - where pigs vanished the land was plentiful, sows fed (cond 1.00,
  fed 0.997-0.999) and bred as often a pig-year as the control's (1.32 vs 1.33); **the households keeping pigs dwindled** (seed 5: 15 -> 8 in ten years,
  control 12) and the one stubble-specific end was **the hands' cull's share of pig ends doubling** (6.1 -> 14.6 %, 7.8 -> 11.8 %, 6.5 -> 14.8 % in
  seeds 5, 7, 11): D2-050's rule culled the kind with the most spares when hands were short - after a farrowing the pigs - so with the stubble's cattle
  filling the hands the young sows went before replacing the breeding ones. Fixed: short hands eased from the kind whose spares take most of them.
- Tests on bin_s3 (before the dry-way change): Livestock 16/16 (new `HandsThatAreShortCullWhatTakesThemNotTheLitter`; the short-pasture test now
  counts pasture and hands culls together), Stubble 3, Muck 3, HouseholdWork 4, Coarse 3, Determinism 6, SaveLoad 8 (`--filter SaveLoad`), Tilth 9,
  Threshing 5, Garden 5, Footprints 8 - **and the same eleven again on bin_s6 (the dry-way code, branch Core = d8bf37d): all pass** (22:10, `1_zz_tests_s6`).
- Controls byte-identical: `--nostubble --onefieldspot --oldspare` = `_n1` on all ten seeds (`_c6`) and on bin_s6 (`_c8`); `--oldhands` = `_u2`;
  `--wetfieldspot` = `_u4`; the diagnostics change nothing (`_u3q` = `_u2`, `_c3q` = `_n1`); yearly reports change nothing.
- **Ten seeds `_u4` (stubble + new culls) vs `_n1`**: people at par (x1.025 end, x1.020 years 200-299), births/deaths a person-year at par, hunger
  x1.10 (t 0.91), cattle x1.94 (t 5.7), goats x1.12, **pigs x0.80 (t -1.28) where kept; lost only in seed 5 (seed 1 has none in any arm)** (u2: lost in
  1, 5, 7, 11); foundings not fewer but earlier (by century 32/46/38 against 21/62/42; the run's total x0.94, t -1.4). `_r4` (the new culls without the
  stubble): pigs x0.83 (n.s.; seed 7 lost them), cattle x0.91, goats x1.18, people 200-299 x0.943 (t -1.95), sickness x2.6 (t 2.4) - noisy.
- Unity C# compiles (`unity.mjs cc`, 180ad85, in the main checkout detached at the branch tip). Real shots `tc2/out/shots/s21a`-`s21t` judged
  (`27` §9): herds on the stubble, household field work (busiest spot 2-13 within 60 m, was 126), a carter in the sea (fixed: field work in the water
  2-4 person-hours, was 55-68), the hunters' crowd (real: a village outgrown its land; the control has it too), the woodlot framing works, muck heaps.
- **Open, older than C3.7d**: 0.10-0.14 % of person-hours under the drawn water in the control world (0.22-0.28 % in the stubble world): woodcutters'
  straight walks across water to their woodlot, the water-drawing spot (`WellX/WellY` +-2 m, no `Ashore`) under a lake's edge, crofts into the water,
  the hungry walking home across a bay (`wet --doy D` lists them by village; `results/wet_*.txt`).

### Next, in order
1. Read the queue's results: `cmp_c6_u7_20.txt` (20 seeds: people, pig/cattle/goat-years `*_years`, extinctions per arm,
   foundings, sickness), `cmp_c6_r4.txt`. Merge when convincing (people at par, pig-years within noise, extinctions like the control's); if pig-years
   fall significantly, look first at hard times on mast (kills the most grown - the breeding cores - down to a village's nucleus) and the hunger cull
   (keeps "a pair" of the eldest, eats the young sows).
2. Merge: `git merge --no-ff wip/c37d-stubble` on main (message `[tc2] Merge the stubble branch: C3.7d ...`); DECISIONS **D2-155** from
   `tc2/out/tools/s21/d2155.md` (fill "Evidence"); PROGRESS; `25` (C3.7d done). Then the full suite (`node tc2/scripts/headless.mjs test`, ~17 min)
   and the Web build and test (`node tc2/scripts/unity.mjs build-web --jobs 2`, then `node tc2/scripts/webtest.mjs --tag s21web1 --export`; saves v80).
3. **Nobody walks through the water** (D2-123's rule for the ways, a new decision with its switch): woodlots reachable dry-shod (`Woods.Choose` with
   `FieldSpots.DryWay` from the square), the water-drawing spot ashore (`AgentSystem.BeginWater` + `Ashore`), crofts on dry ground (`Gardens.Strips`),
   wild-food spots reachable dry-shod; `wet --doy` before/after; ten seeds.
4. The next civilisation slice by evidence: **governance** - seed 4 at year 300 has 30 villages, 10,254 people and ONE lordship of 2 villages (6 raids
   in 300 years; lordship only by conquest, D2-075): measure polities over ten seeds, then lordship beyond conquest (daughter villages' kin allegiance,
   protection, marriage between first households - `10` §4). Or **holdings on the ground** (strips a household holds; needs a Core-defined strip layout
   the terrain shader follows - its strips are a GPU float hash now).

### Rules that bit this session
- A Unity coroutine holds no `ref` locals (CS8176 in `ShotRunner`) - caught only by `unity.mjs cc`, which runs in the **main checkout** (Unity's csproj
  and Library): queue jobs detach the main checkout at the branch tip and return; they refuse to run with untracked files - commit the editor's
  `.meta` files of new branch files to the branch.
- The claude-mem plugin (v13 hooks calling a v12 CLI: "Unknown command: hook") was uninstalled with `--keep-data`; its hooks may fire until Claude Code restarts.

## Session 20 (13 Oct 2026, 17:25-~21:00 IST; Control Room run #55 session 2) - stopped by Arjun; nothing running

### Exact state
- **main** (after the handoff commit): D2-154 the burial ground kept and graves drawn as graves; W-FELL view faults fixed; **C3.7c the muck merged**
  (d601c24, sim version 79, D2-153 - ten seeds `_n1` at par); the threshing floor in use drawn; shot views `floorbusy`, `floorbusylow`,
  `act:<Activity>[:low]`; the shots log stumps placed and drawn. `.claude/launch.json` and `Assets/Resources.meta` are not ours - leave them.
- **Branch `wip/c37d-stubble`** (worktree `C:/Game Dev/tc-stubble`, sim version 80; NOT merged): C3.7d the herds on the stubble and the fallow
  (`Core/Places/Stubble.cs`, `Livestock.FieldOffer`/`GrazeFallow`/`GroundOf`), each household on its own field cell (`Core/Places/FieldSpots.cs`),
  a food's cull by what the spares eat (`Livestock.Spare`, `--oldspare`); switches `--nostubble --onefieldspot --oldspare` (together = `_n1`
  byte-identical); `26` §2k; draft D2-155 in `tc2/out/tools/s20/d2155.md`.
- Other worktrees: `tc-merge20` (branch `wip/s20-merge`, fast-forwarded into main - may be removed), `tc-muck` (merged), `tc-thresh` (merged),
  `tc-g2`, `tc-g4`, `tc-wild` (earlier sessions') - leave them unless cleaning up deliberately.
- Saves: `tc2/out/civ/s4_m1` (v78), `s4_n0` (v78, graves kept), `s4_n1` (v79, main), `s4_u2` (v80, the stubble branch).

### Verified
- On merged main: Muck 3, HouseholdWork 4, Burials 4, Tilth 9, Livestock 14, Coarse 3, Determinism 6, SaveLoad 8, Threshing 5, Carts 6, Footprints 8,
  Garden 5, Homes 5; no float arithmetic; `unity.mjs cc` (633466f). Controls byte-identical: `--oldgraves` = `_m1`, `--nomuck --oldgraves` = `_m1`.
  Ten seeds `_n1` (muck + graves kept) against `_m1`: at par (`tc2/out/tools/s20/cmp_m1_n1.txt`).
- On the stubble branch: Stubble 3, Livestock 15 (new: a short pasture culls what eats it), Muck 3 and the seven neighbours; control
  `--nostubble --onefieldspot --oldspare` = `_n1`. Ten seeds `_u2` (`cmp_n1_u2.txt`): people at par, fields x1.017, cattle x1.84, goats x1.38 -
  **pigs gone from 3 of 10 worlds** (seeds 5, 11, 7), foundings x0.77 (t -2.4).

### Next, in order
1. **C3.7d's pigs.** Why do pigs die out where cattle grow? Two leads from the reports' end causes:
   - The hands cull (mode 0) still takes the kind with the most spares - a sow's litter.
   - Mast culls (cause 9).
   Trace with `--places` (village lines `stock cattle/goats/pigs` every 10 years; end causes by kind) on seed 5 or 7 `_u2` against `_n1`. Find the
   mechanism, fix it with its own switch, ten seeds again, then merge `wip/c37d-stubble` (the merge's Unity cc; then shots: `herd` views on a
   stubble day ~42, `act:Muck` spread over the fields, `close` at day ~30 for the muck heaps). Then write D2-155 from `tc2/out/tools/s20/d2155.md`.
2. The full suite on the final HEAD (`node tc2/scripts/headless.mjs test`, ~17 min; through the queue) and the Web build and test
   (`webgate.sh`, one Unity build alone).
3. Shot `107c100`'s woodlot framing (`woodlot,woodlotlow` on `s4_n1/world_y300.tc2`; the Unity C# compiles on 4dd9b94 - `unity.mjs cc` at the handoff).
4. Then the next civilisation slice (`27` §6 gaps: holdings by household not drawn; no held tools; crop variety not simulated).

### How the queue works (session 20)
Start `bash tc2/out/tools/s20/runq.sh` in the background with the scratchpad `Q` and `S19` paths set to this session's (it waits for session 19's
q21 to end: delete that line). Jobs are `P_name.sh` files in `$Q/todo` that source `lib.sh` (paths `S`, `N`, `R`, `O`, `DN`, `BL`, `gate`, `low`,
`world seed bin tag [switches]`); `touch $Q/stop` ends it after the current job. Copies of every job, log and comparison:
`tc2/out/tools/s20/{q_todo,q_done,q_log}`. Fix `N`/`S` in `lib.sh` to the new scratchpad and copy `bin_s0` (stubble) / `bin_n1` (main) builds
again (`0_p_stubblebuild.sh`, `0_i_mergebuild.sh` show how).

## Session 19 (13 Oct 2026, from 14:53 IST; one low-priority job at a time, RAM < 86 %, CPU < 85 %) - context handoff at ~17:10 IST

### Exact state
- **main 4caf9ee** (clean but for `.claude/launch.json`, `Assets/Resources.meta` - not ours): b312f01 **D2-148 W-FOREST pass 2b** (rain belt follows
  the sun, `--oldrain`; the seasonal tropics drawn from the soil's water; conifers tinted - `ProcMeshes.Builder.Dim`); 14a9156 merge of
  `wip/c37b-thresh` (**C3.7b the threshing floor** D2-149, **W-FELL** felling record D2-150 Core; sim version 77); 4caf9ee stumps drawn (D2-150),
  **D2-151 the burial ground** (`Core/Places/Burials.cs`, sim version 78), **D2-152 the first household's standard** in its polity's colour; kit
  `rick`, `grave`, `grave_old`, `standard` exported (their `.meta` files and those of Threshing.cs, Burials.cs, the new tests appear on Unity's next
  import - commit them).
- **Verified**: the full suite on 86b481d 372/372; pass 2b control/seasons/ten seeds (`_r1`); threshing tests and neighbours; after the merge
  Threshing 5, Felling 3, Burials 3, Sheaves 5, Determinism 6, SaveLoad 8, Coarse 3, HouseholdWork 4, Climate 7, lint, Unity C# compiles; the merge's
  control `--nothresh --oldrain` seed 4 = `_g4` byte-identical.
- **Running at the handoff (scratchpad `f50753be-.../scratchpad`, copies in `tc2/out/tools/s19/`)**: q19 - ten seeds `_m1` (bin_m, sim version 78)
  then `cmp_r1_m1.txt` (threshing's effect against pass 2b; graves and felling are records); q20 (waits for q19) - real shots `s19e20` (floor,
  woodlot, graves, standard, village views on `s4_m1/world_y300.tc2`), `s19f20` (`--village town`), `s19g4`/`s19g28` (the four climates after the
  last view tweaks: inland stands inside the woods, slighter grey dry-season thorn); q21 (waits for q20) - **C3.7c the muck** in worktree
  `C:/Game Dev/tc-muck` (branch `wip/c37c-muck`, c5429fd, sim version 79, NOT yet built): build, MuckTests and neighbours (Tilth, Livestock, ...),
  control `--nomuck` seed 4 = `_m1`, seed 4 `_k0` with `--mucktally`.
- DECISIONS D2-148 (final), D2-149/D2-150/D2-151 (evidence but for ten seeds and shots), D2-152 (shots pending) written; D2-153 (the muck) not yet.

### Next, in order
1. Read q19's `cmp_r1_m1.txt` (threshing at par? if hunger or famines move, find why); add it to D2-149.
2. Look at q20's shots (`tc2/out/shots/s19e20`, `s19f20`, `s19g4`, `s19g28`): ricks round the floor, threshers' flails, stumps in the woodlot,
   graves (fresh and old), standards; fix what reads wrong; record in D2-149..D2-152 and `27` §6/§8.
3. q21's muck results: fix the build/tests; control; then design text `26` §2j and D2-153; merge `wip/c37c-muck`; Blender `--only muck_heap`; ten
   seeds `_k1` against `_m1`; shots of heaps and carting (`--days 42` after the harvest, `--days 4` the sowing).
4. The full suite on the final HEAD; the Web build and test (sim version 79: new worlds); then the remaining `27` items (crop variety is not
   simulated - a real C-slice), SESSION_19_REPORT.md.

## Session 18 (11 Oct 2026, 06:48-~16:00 IST; Control Room run #48 session 3, resumed by Arjun; stopped by Arjun) - D2-144 to D2-147; Web 6/6

### Exact state at the stop
- **main**: e23e5b7 (**D2-144** the gardens keep their village's own calendar), d224365 (**D2-145** a croft counted once, one wattle fence, beds by
  season), 29f56df (docs), c7edc68 (**D2-146** a COARSE day's garden by the lived urges; **D2-147** FreeLot bounded when the sea took every lot; `run
  --trace`), and the stop's commit (DECISIONS D2-146/D2-147 with evidence, PROGRESS, this block, the prompts, `SESSION_18_REPORT.md`). Nothing uncommitted
  of ours (`.claude/launch.json` and `Assets/Resources.meta` are not ours).
- **Verified on c7edc68**: Footprints 8 (new: the drowned village), Migration 9, WildFood 11, Garden 5, HouseholdWork 4, Coarse 3, Determinism 6; lint;
  Unity C# (`unity.mjs cc`); controls `--oldgardenorder` = `_g3` byte-identical; ten seeds `_g4` at par (seeds 4, 5, 1 byte-identical before and after
  D2-147); **the Web build (one job, 1 h 13 min) and the Web test `s18web1` 6/6** (WebGPU, browser = headless bit-identical, reload continues, a day
  away catches up ~96 years: 531 people in 17 places, no console errors). The 900-year check passed (seeds 4 and 42, `_g3`).
- **The full suite on c7edc68 was running at the stop** (`scratchpad/suite_f.txt`, ~85 % through with no failure when the handoff was written; see
  SESSION_18_REPORT.md for whether it finished). The previous full run (d224365) was 369/371 - both failures fixed by D2-146 and passing on their own.
- **W-FOREST pass 2b prepared, NOT applied**: `tc2/out/tools/s18/pass2b.diff` (`git apply` it: the rain belt follows the sun in the outer tropics,
  `--oldrain`; the dry woodland's leaves fall with its soil's water; `place` applies the rule switches before loading), the queue that validates it
  `tc2/out/tools/s18/q10.sh` (build, planet/weather tests, control `--oldrain` = `_g4`, `place --seasons` old/new on seed 42, ten seeds `_r1`, cc, shots
  of four climates on days 20 and 44), the decision draft `tc2/out/tools/s18/d2148.md`.
- **Resource rules (Arjun)**: D-1 - one low-priority job at a time (`cmd //c start "" //belownormal //wait //b ...`); D-2 - run while CPU < 85 % and RAM
  < 86 % (`q*.sh`'s `gate`/`low`). The machine's other applications (a Chrome tab drawing WebGL in software, other sessions) held it at 60-100 % CPU
  for hours: jobs waited at the gate and ran slowly (a test class 13 min instead of 24 s). The Web build with `--jobs 1` takes ~75 min.
- Tools in `tc2/out/tools/s18/` (copy into the new scratchpad and fix the `S=` path): `q2-q10.sh` (the gated queues), `l9.sh`, `cmp.py`, `rates.py`,
  `sheet.py`, `shots.sh`, `p_drytropics*.py`, `pass2b.diff`, `d2144-d2148.md`. Runs: `tc2/out/civ/s*_g2` (D2-144), `_g3` (D2-145), `_g4` (D2-146/147),
  `s4_l9g3`, `s42_l9g3` (900 years). Shots: `tc2/out/shots/s18a-d`, `s18_gardens_sheet.png`.

### Next, in order
1. If the stop's full suite did not finish: run it once on HEAD (`node tc2/scripts/headless.mjs test`, one job; ~17 min on a quiet machine).
2. **W-FOREST pass 2b**: `git apply tc2/out/tools/s18/pass2b.diff`, then `q10.sh`'s steps; write D2-148 from `d2148.md`; keep iterating on the shots
   until the four climates are not interchangeable (Arjun judges W-FOREST).
3. C3.7b the threshing floor (the harvest held as sheaves until threshed - `SettlementSystem.Sheaves` puts the grain straight in the store today;
   ~0.3 h an adult-day through the off-season, at the granary or a barn) / C3.7c the muck (`26` §2h); the visual-truth items (`27` §6: headmen, famine,
   stumps - the woods keep no record of felling, so stumps need derived or new state; the beds' crops by kind).

## Session 17, third context (10 Oct 2026, 12:31-12:45 IST; Control Room run #48 session 3; Resource Governor HIGH, the machine at 94-97 % RAM from a QEMU VM and other applications) - stopped by Arjun

### Exact state at the stop - nothing running
- **main HEAD**: a8a45a9 + one WIP commit (this block): **uncompiled** view work - run `node tc2/scripts/unity.mjs cc` first.
  - `Gardens.Shown(in hh, doy, cropGrowth, out dug, out cleared)` (read only): the share of the croft dug this year and the share gathered and cleared;
    true from the growing on (the dug beds in leaf).
  - `WorldObjectsView.GardenBeds`: the beds drawn by season - dug in the gardener's order (middle column behind first, from the back wall out): fresh
    earth (`garden_bare` tinted `BedDug`) in the sowing and once gathered, in leaf (`garden`) from the growing on, a weedy rough (`BedRough`) where
    nobody dug this year; **huts' crofts drawn too** (they were worked in the simulation but drawn bare). `_gardensBare` instancer.
  - Kit: `garden_bare` in `tc2/art/kit.py` (beds, ridges and clods tintable; built with `--only garden_bare`; `garden_bare.fbx` committed, its `.meta`
    is made by Unity's import - commit it after the first editor run).
- **The full suite was stopped mid-run** (started 12:33 on a8a45a9; no result) - run it once on the next HEAD.
- **Found, NOT fixed - a real D2-143 bug**: the garden uses the unshifted day of the year (`AgentSystem.cs` ~577 `Gardens.Work`, ~1163 `Behindhand`,
  ~1989 the COARSE step's `gDoy`), but southern villages keep their crop calendar 24 days on (`if (s.Anchor.Y < 0) doy = (doy + 24) % 48`,
  SettlementSystem/Holdings) and `Gardens.Year` runs on that calendar - so southern gardens are dug, weeded and gathered on the wrong days (little
  borne). Fix: one helper for the village's own day of the year used by all three calls and by the view's `GardenBeds`; a test with a southern
  village; ten seeds against the HEAD binary (the change moves the worlds; sim version stays 76 - no format change).
- Tools: this context's copies in its scratchpad (`1a60904a-...`): `q1.sh` (suite, keep bin_h, `unity.mjs cc`, then shots `s18a` with
  `SHOTX="--days 8"` - the sowing, beds being dug - and `s18b` `--village town --days 20` - in leaf). **A year-300 save opens on the year's first
  day: nothing is dug yet** - shoot gardens with `--days 6-10` (digging) or `--days 14-28` (in leaf, weeding).

### Next, in order
1. `unity.mjs cc`; fix the southern garden calendar (above) with a test; the full suite once; ten seeds (`q8.sh` pattern) against `_g1`.
2. Real shots of the gardens (`--days 8` and `--days 20`; read "at work or on the move"); fix what they show; commit (DECISIONS D2-144).
3. Then as the second context's list below: the 900-year check, the Web build and test, W-FOREST pass 2b (note: pass 2b's wet season is by the
   hemisphere's summer - check the southern crop calendar's growing stage falls in it).

## Session 17, second context (10 Oct 2026, 06:46-12:35 IST; Control Room run #48 session 2; Resource Governor LOW, HIGH from ~12:00) - queue 3 closed, the Unity side compiled and shot, D2-141, D2-142 the woods, D2-143 the garden

### Exact state at the handoff (12:35 IST) - nothing running
- **main HEAD 93ad626** (clean but for `.claude/launch.json` and `Assets/Resources.meta`, not ours): a9571e0 (queue 3 closed, Unity compiled and shot,
  `Conflict.PastWorks`, `ShotRunner` waits for a loaded world's lived day and takes `--village town`, `unity.mjs cc`, `timeuse --daylight --stages`,
  `place --sheds`, `test` takes the rule switches), 6289fba (**D2-142** trees by the growing season's warmth, `--oldwoods`; DECISIONS D2-141/D2-142;
  probes `place --crofts/--housing/--seasons`), 1d5f37b + merge 93ad626 (**D2-143 C3.7a the household's garden**, sim version 76, `--nogarden`).
- **Verified on 93ad626**: the headless build, GardenTests 4/4, DefenceTests 8/8, `node tc2/scripts/unity.mjs cc` (Unity C# compiles). On the
  garden branch's identical code: HouseholdWork 4, Determinism 6, Coarse 3, CatchUpPlan 2, DeepTime 8; ten seeds `_g1` at par; control `--nogarden` =
  `_w1`. **The full suite last ran on the woods' source (366/366, before the garden) - run it once on 93ad626.** **No real shots of the gardens yet**
  (the view's beds on the croft are compiled, not seen). **The save format is v76**: v75 saves (all earlier runs) load only in an older binary.
- **Prepared, not applied**: W-FOREST pass 2b (`tc2/out/tools/s17b/p_drytropics.py` - the rain belt follows the sun in the outer tropics,
  `--oldrain`; `p_drytropics_view.py` - the dry woodland's leaf fall from the soil's own water). `place --seasons` (seed 42 `_w1`): the dry tropics'
  soil water sits at 0.15-0.25 in every season - no dry season in the state.
- Runs (`tc2/out/civ`): `_v4` (D2-141 final), `_v5b` (the ring rule alone), `_w0`/`_w1` (D2-142), `_g0`/`_g1`/`_g3` (D2-143; `_g1` saves at 100/200/300 are
  v76). Shots: `s17a` (a loaded save before its lived day), `s17b`/`s17c` (a lived morning: the town, the first village), `s17d` (forests after D2-142).
- **Tools copied to `tc2/out/tools/s17b/`** (copy them into the new scratchpad and set `S=`/`SCR=`): `bld.sh` (main headless; BIN, TESTS, GATE),
  `gwrun.sh` (seed runs; GATE, EXTRA), `cmp.py A B seeds`, `rates.py TAG seeds`, `decades.py A B` (people by 20-year spans), `shots.sh TAG SAVE VIEWS
  [HOUR]` (`SHOTX="--village town"`), `sheet.py` (contact sheets), `l9.sh` (the 900-year check: `BIN= TAG= bash l9.sh SEED`), `webgate.sh TAG`,
  `bench.sh`, `pt.py`, `v75.py`, `mkcc.py`, `q8.sh` (the pattern: build, control, ten seeds in three lanes), `p_drytropics*.py`, `d214x.md`.
- **Resource Governor HIGH since ~12:00**: CPU 90 %, RAM 92 %, up to three heavy jobs, never two builds or suites at once; the machine's other
  applications hold RAM at 65-96 %: gate heavy jobs on RAM < 88 %; seeds may run in three lanes (`q8.sh`).
- **Rules that bit this context**: a background `cd X && bash q.sh > log` task, when stopped, leaves the script running (and a stale copy
  re-reads an edited script) - start queues with `exec bash q.sh > log` and kill a chain's parent to detach its later steps; a local named like an
  enclosing one (`sid`, `gh`) fails the headless build (CS0136); write patch scripts with the Write tool (heredocs mangle quotes); a v76 binary
  cannot read v75 saves; wall time is not cost on this machine (compare the reports' `events` counts, or time back to back).

### Findings of this context
- D2-141 (DECISIONS): ten seeds `_v4` vs `_n1` at par in rates; the street clearance makes a people wave (x1.02-1.07 over years 140-199, x0.95
  over 280-299), attributed by elimination (`_v5b`), mechanism not found (`--housing`: no fewer households camping); boats lost x1.68 was `_n1`'s luck.
- D2-142: people x1.10, per-person rates at par, events x1.017; boreal woods +50 %; the boreal and mixed shot views now find woods; the dry tropics
  and the wet woods still read alike (no dry season in the state).
- D2-143: gardens real - 83-86 % of households at year 300 (mean ~130 m2), a lived year 0.3 of 12 daylight hours in them, nearly all from idling,
  under 1 % of the food; ten seeds at par. Adults idle 3.9-4.6 of 12 daylight hours over a lived year (the year's first days up to 7).
- Real shots (`27` §8): a lived morning shows 2,155 of 3,001 people drawn busy; poses, ways along streets, gardens and pens read.

### Next, in order
1. The full suite once on 93ad626; then **real shots of the gardens** on a lived morning (`shots.sh` with a v76 save, e.g. `s4_g1/world_y300.tc2`;
   town views with `SHOTX="--village town"`; read the log's "at work or on the move") - beds on the crofts, people hoeing behind their houses.
2. **The 900-year check** (`l9.sh` seeds 4 and 42 on the final binary, `TAG=g1`) and **the Web build and test** (`webgate.sh s17web2`; the Web game
   starts new worlds - v76).
3. **W-FOREST pass 2b**: apply both patches, `--oldrain` = the final binary (byte-identical), ten seeds, `--seasons` before and after, shots of the
   dry tropics in the wet and the dry season and of four climates side by side.
4. Then C3.7b the threshing floor and C3.7c the muck (`26` §2h), and the remaining visual-truth items (`27` §6-§8).

## Session 17 (10 Oct 2026, from 05:04 IST; Control Room, Resource Governor LOW: CPU 50 %, RAM 75 %, one heavy job) - the soil and the niche closed (D2-139, D2-140); the visual truth audit (`27_VISUAL_TRUTH.md`)

Arjun's prompt (10 Oct): finish the soil, test the niche, then a **real-game audit** - people standing still, houses on roads, can the world be read
without a panel - then **W-FOREST pass 2**, then the roadmap; presentation and simulation advance together from now on.

### Exact state (keep current)
- **Code (uncommitted at the time of writing - commit first if a context ends)**: HEAD f09da10 + the compile fixes (SettlementSystem `bring`,
  Commands `nsb`), the niche on by default (`SettlementSystem.NicheOn = true`, `--noniche`; sim version 75), `Core/Agents/Presence.cs`,
  `Core/Agents/WayLines.cs`, `Footprints.OpenScale/SquareEarth/DiscFree/DrawnLine`, `Footprints.WorkClearOff` (`--noworkclear`),
  `Conflict.WallWorksOff` (`--nowallworks`), the view (WorldObjectsView people via Presence, gates, `Instance`/`YardPen`; LivestockView pens;
  TerrainLod roads via WayLines, no road over a square, streets from the square's edge, lanes from 26 m, blocked streets, `_TC2Year`; Hud picking
  and follow via Presence; CameraRig `townlow`/`townstreet`; ShotRunner logs busy people), shaders (Instanced: `TC2Pose`, sixteen poses, shadow
  pass posed; Terrain: fallow at its true share, rotating), headless tools (`place --still`, `place --roads [--show K] [--feature F] [--works]
  [--rawways]`, `place --woods` by kind of wood and by summer warmth), HouseholdFormationTests fixed. **The Unity side has not been compiled.**
- **Runs** (`tc2/out/civ`): `s7/s8/s42_s2` (the soil's last seeds), `s4/s42_n0` (= `_s2`), `s*_n1` (the niche, ten seeds), `s5_v1` (= `_s2`: the
  view code changes nothing); queue 3 (`scratchpad/q3.sh`, log `q3.log`): controls `_c0` (`--noniche --nowallworks --noworkclear` must = `_s2`),
  `_c1` (`--nowallworks --noworkclear` must = `_n1`), ten seeds `_v3` (the final binary), `cmp_n1_v3.txt`, then the full suite (`suite_v3.txt`).
- Scratchpad: `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/9bd81d71-36cd-4497-a09b-0b3d9ad0e072/scratchpad` (`bld.sh`
  BIN/TESTS/GATE builds and tests; `gwrun.sh`; `q1-3.sh`; `v75.py` sets a save's sim version; `sv/` v75 copies of the `_s2` saves for the audits;
  `rates.py TAG seeds`; `p_*.py` patches).

**State at the first context's end (06:35 IST)**: committed b5beba2 (WIP) and a second commit with the view-only W-FOREST pass-2 work (the woods'
floor by kind of wood, the dry tropics' dry season, rain forest closure, boreal spires, ForestTypes' treeline by growing-season warmth tMean +
0.6 TAmp < 7) and the tools (`place --woods` by kind and by summer). **Queue 3 was STOPPED at the handoff (06:45) after `_c0`: nothing runs; rerun
its remaining steps (`tc2/out/tools/s17/q3.sh`).** It had been (`scratchpad/q3.sh`, log
`q3.log`): its first controls passed (`--noniche --nowallworks --noworkclear` = `_s2` byte-identical, seeds 4 and 42); it was waiting at its RAM gate
(the machine at 88-97 % from a 1.7-2.8 GB QEMU VM that is not ours) before `_c1` (`--nowallworks --noworkclear` must = `_n1`), the ten seeds `_v3`
and the full suite (`suite_v3.txt`; it builds the headless app from the source at that moment). **Not applied yet**: `scratchpad/p_summerwoods.py`
(the ecology change: tree suitability by growing-season warmth SmoothStep(5, 10, TMean + 0.6 TAmp), `--oldwoods` the control; D2-142) - apply it
after the suite's build has begun (or after q3), then measure with `place --woods` and ten seeds against `_v3`.

### Next, in order
1. Queue 3's results: the controls byte-identical, `_v3` against `_n1` at par (only rings and works' clearance differ), the full suite - then
   **commit** (D2-139 closed, D2-140, the audit tools, the view and shader changes, the docs).
2. **The Unity compile and real shots** (one heavy job, when RAM allows): `node tc2/scripts/unity.mjs shots --tag s17a --load <a v75 save>
   --hour 10 --views townlow,townstreet,low,close,village` - people at work, ways along streets, pens, gates, fallow; fix what the shots show.
3. **W-FOREST pass 2**: the simulation's cool woods are thin because tree suitability ramps on the annual mean (`PlanetSystems.GrowVegetation`:
   SmoothStep(-1, 8, TMean)); measure with `place --woods` (summer = TMean + TAmp) and consider suitability by summer warmth (a simulation change,
   ten seeds); then the presentation: ground cover by kind of wood, rain forest closure and layers, the dry tropics' dry season, edges.
4. The soil's 900-year check (seeds 4, 42), the Web build and Web test.
5. B of the stillness audit: adults idle 21-24 % of daylight - `timeuse` by age; the scheduler's work (service, own holdings, repairs) vs leisure.

## Session 16, fourth context (9 Oct 2026, from 21:33 IST; Control Room run #39 session 4, Resource Governor MEDIUM) - the Web catch-up's out-of-memory traced and fixed (D2-137); a headman gone to live with their child (D2-138)

**The blocker of the third context is resolved (D2-137).** The previous context compared the browser's catch-up with headless `run --coarse` and
read the difference as the browser diverging. Wrong premise: `PlanCatchUp` lives a span COARSE only above 40,000 person-years; seed 42's 96 years x
~145 people are ~14,000, so **the Web test's "a day away" is lived in ACTIVITY** and its world is headless `run --seed 42 --years 96` (C3.6e's binary
ran out of memory there too, at year 28 under a 512 MB heap cap - browser = headless). `webtest.mjs --export` (new) reads the game's own saves out of
IndexedDB (before the day away and every half minute of the catch-up) into the tag's folder; `run --load SAVE --hot N` (new) names who began the most
activities between reports. Cause: two parties founded villages on one cell in year 27 (Ustwick, Skolmere: a party "founds all the same" where
another party's village now stands); the way between them had no length; visitors' arrival was read from the arc length, so a trip home ended where
it began, ~17,000 times a day; each leg went into the travel index, pruned only by queries. Fixed (sim version 72, 741565c): a founding party that finds
a village within `Sites.Spacing` of its place joins the nearest (`PartyJoined` 95); on a way of a metre or less a traveller arrives at their
destination (both arrival sites, land and sea); `TravelIndex.Sweep` before the pool grows. Ten seeds DEEP 300 years `_j1`: annals byte-identical to
`_e1` (the case never arose in DEEP). Full suite 358/358 (C3.6e's first full run too). The browser's year-34 save, its header set to 72, replays
cleanly with the fix (`scratchpad/web_y34_v72.tc2`).

**Then D2-138** (`place --heads`, new): C3.6e's retirements made the first household's headman live in another household 37-127 times a world;
`Leaders.Day` read it as a vacancy (contested; 4 of 20 splits came from it) and recorded "another household rose" for the same man. Now (sim version
73): a living headman in another household of the village is weighed again without a contest, the household that absorbed the first household is the
incumbent, the same person stays first with nothing recorded; and a household's clients (`Patron`) follow the household that took it in
(`Holdings.Clients` from `Close`/`Inherit`) - a gap since C10.2. `--nostanding` keeps the old reading. LeadersTests 8 (one new).

### Exact state (keep current)
- HEAD: 741565c (D2-137), 4bf4888 (D2-138, sim version 73) + 4b6572f (Web result) + this handoff. Web build of 4bf4888 in `tc2/web-build/web`; **Web test
  `s17web1` 6/6** (the day away to year 96 in 106 s, 487 people in 16 places = headless ACTIVITY's 488 in 16).
- Runs (`tc2/out/civ`): `s*_j1` (D2-137, = `_e1`), `s*_k1` (D2-138 first form, clients and no contest), `s*_k2` (D2-138 final, the absorbing household
  the incumbent). Browser exports: `tc2/out/webtest/s17exp/` (away_start.tc2, catchup_y30/y34.tc2, sim version 71).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/6dc4a248-f0c6-410d-a245-27452f45cb7d/scratchpad`
  (`wrun.sh` pointed here; `j1batch.sh`, `k2batch.sh`, `k1wait.sh` - waits for RAM under 84 % then runs ten seeds one batch at a time; `p_*.py` patches;
  `d2137.md`, `d2138.md`). **Resource Governor LOW since 23:10 (CPU 50 %, RAM 75 %, one heavy job)**; the machine's RAM sits at 80-90 % from other
  applications (Chrome, WebView, other Claude sessions): `gate.sh CMD` runs a command once RAM is under 72 %; `webgate.sh TAG` (build + Web test).

**SESSION ENDED 10 Oct 2026 ~04:00 IST at Arjun's request** (Resource Governor LOW: CPU 50 %, RAM 75 %, one heavy job). From 00:48 to the end the
machine's RAM stayed at 74-90 % from other applications, so the gated queue below never started: **the last three `_s2` seeds (7, 8, 42), the full
suite on the soil and the niche runs were NOT run**; the queue was stopped. The niche's two new tests (`HouseholdFormationTests`) and a one-line
fix in `Holdings.PortionFor` (a household's last member takes all it holds) are committed but **not compiled or run** - build and run them first.

### Later in this context (10 Oct, 00:00-01:40 IST) - the 900-year check, the soil (C3.6f, D2-139, committed as work in progress), the niche measurement
- **900 years with C3.6e, seed 4** (`s4_l9k.csv`; PROGRESS): hunger 8.7-11.2 a 1,000 person-years in centuries 300-600 (sim 70: 1.0-5.1), births
  40-42 (29-36): the old's grip on the land had been a preventive check; a plague at 746-753 (~9,000 dead) then post-plague plenty. Seed 42 not run.
- **The soil, C3.6f (D2-139 in DECISIONS with seven seeds; sim version 74; committed as work in progress - the last three seeds and the full suite pending)**: `Settlement.Tired/TiredSeen/
  Rest/Grazed/RestEvent`; `SettlementSystem.Heart/SoilYear/HaPerHead/Rested` + constants; `Livestock.Day` tallies grazing; history `FieldsRested`
  (96); Gazetteer soil line; `--nosoil/--norest/--nodung`; land tally soil fields; TilthTests +5 (9 pass). Runs: `s*_s1` (first form), `s*_s2`
  (the land judged by what it bears; 7 of 10 seeds done at handoff time, 7/8/42 queued), `s5/s42_s1n`, `s5_s2n` (= `_k2` byte-identical with
  `--nosoil`). **Finding**: worlds x0.55-0.7 people (as designed), bound villages rest 13-17 %, a sown ha 0.73-0.79 of a fresh crop - **but births do
  not answer** (42 a 1,000 before and after): hunger a person-year x1.9 (4.7 -> 9.3), wounds x2 - the marriage rule (C3.5) does not bind (couples
  bring two years of bread: 83 of 8,635 matches ever waited).
- **The niche (C3.5b), measurement code in the working tree (off by default)**: `SettlementSystem.CoupleLiving` (land's half by the portions the
  couple would hold + labour's half by their hands, at the village's last harvest, against their year's need), `NicheOn`/`NicheYears` (`--niche`,
  `--nicheyears N`: marry when the living keeps them or their grain carries the shortfall N years), `NicheTally` (`--nichetally`), `Holdings.HandOf`,
  `Holdings.PortionFor`.
- **Was queued, never ran, stopped at the session's end** (copies in `tc2/out/tools/`): `q_s2.sh` (the last 3 `_s2` seeds) -> `q_suite.sh` (the full
  suite) -> `q_n1.sh` (`_n0`: seeds 4, 42 with `--nichetally`, must equal `_s2`; `_n1`: ten seeds `--niche`). Expected times when memory allows:
  ~5 min, ~15 min, ~20-22 min (one job at a time; gate each on RAM under ~66 % with `gwrun.sh`, which waits per seed).

### Next, in order
1. Build the headless host and run `HouseholdFormationTests` (the two niche tests are uncompiled), then the three missing `_s2` seeds
   (`BIN=<build> TAG=_s2 SNAP=100,200,300 EXTRA="--gaptally --landtally --landholds" bash gwrun.sh 7 8 42` - the binary must have the niche OFF: it
   is off by default), the full suite once, then `_n0`/`_n1` as in `q_n1.sh`; compare `python cmp.py s2 n1 <ten seeds>` and the absolute rates (years
   200-299: hunger, births, deaths a 1,000 person-years; `_k2` 4.7/42.2/32.8, `_s2` 9.3/42.3/38.1 on seven seeds).
2. **Close D2-139 (the soil)**: its final ten-seed numbers into DECISIONS and PROGRESS, the full suite's result; commit.
3. **C3.5b the niche** (`--niche`): if births answer the land (hunger a person-year back toward `_k2`'s, people at par or fewer), make it the rule
   (D2-140, `--noniche` the control), tests (a couple with a living marries; a landless couple waits unless its grain carries the shortfall), docs.
4. Then the soil's 900-year check (seeds 4, 42), the Web build and Web test, the soil's look in real shots (fallow plots: the view draws a village's
   plots by the share sown), and the remaining candidates (herds on the stubble and fallow, a land market, partible customs C11).
5. Earlier list (third context, 6-8): work for grain, alms by standing, households working their own holdings, debts' consequences.

## Session 16, third context (9 Oct 2026, from 16:10 IST; Control Room run #39 session 3, Resource Governor MEDIUM) - C3.6a working the land harder (D2-131), the first sheaves (D2-132), household land C3.6b-d (D2-133..135)

**OPEN BLOCKER FIRST (end of this context, 21:30 IST)**: HEAD 5dd947a (C3.6e retirement, sim version 71) + this handoff commit. **The Web build of
5dd947a FAILS the Web test's catch-up, deterministically** (`tc2/out/webtest/s16web9` and `s16web9b`, identical): checks 1-4 pass (bit-identical
probe), then "a day away" stalls - year 35, 243 people in 17 places, 620 s - and the browser aborts `OOM` (`abortOnCannotGrowMemory`) then
`memory access out of bounds` (console.txt). The previous build (26127b5, C3.6d) passed the same test (`s16web8`: year 96, 518 people, 14 places).
Headless is healthy: ten seeds DEEP 300 years (`_e1`), six worlds COARSE 100 years (`scratchpad/cscan_*.txt`, 4-5 years/s), the managed heap at
par with `--noretire`. The browser's path: new world (00:38), lived to 12:21 of day 1, saved, reloaded, then `OfflineProgress.PlanCatchUp` (Living:
COARSE from the next midnight, ACTIVITY for the last two days) to year 96 - the same as `run --coarse` in principle, but the browser's year-35 state
(17 places) differs from headless COARSE (9 places): find why first (reproduce the browser's catch-up headless exactly - the SimHost loop's
catch-up step, budgets, saves during the catch-up - or build 5dd947a with `Retire` switched off to confirm C3.6e is the cause). The full suite has
NOT been run on 5dd947a (LandTests 10, Holdings 12, Migration 6, Biography 4 pass). Do not build on C3.6e until this is resolved; revert it if the
cause cannot be found. Targeted tests on HEAD 7abbaa5 at handoff (21:35 IST): `LandTests` 10, `SheavesTests` 5, `TilthTests` 4,
`DeepTimeTests` 8, `HouseholdFormationTests` 3 pass. Tools of this context copied to `tc2/out/tools/` (`pt.py` line-ending-aware patching, `gap.py`,
`loadrun.sh` - runs saves forward, `coarsescan.sh`, `wrun.sh`); the seed-42 browser opening state lived headless: `scratchpad/web42/world.tc2`.
**State before C3.6e**: 26127b5 + cc1f436 verified (Web `s16web8` 6/6, full suite 353/353); sim version 70. Verified: Web build of 26127b5 (`tc2/web-build/web`),
Web test `s16web8` 6/6 (bit-identical; the settlement page's Land tab shows "Land: 9 households hold 14 ha of its fields (the most the Doldseinds,
2.2 ha)"); the full suite on 26127b5: 353/353. `LandTests.cs.meta` made by the Unity build (commit it). Nothing else running.
**The phase in one paragraph**: C3.6a (more work a hectare where land binds) and the first sheaves (the harvest's grain as it is reaped) each ended in
Malthus - the villages grew into the food; then holdings (C3.6b: each household's hectares, by founding shares, portions of spare land, the last out,
heirs, given out at sowing), the harvest by land and labour (C3.6c: the land's half to holders, the labour's half by hands - the landless get half
their mouths' share, marry later or never: **hunger a person-year x0.74**, the preventive check), and who leaves (C3.6d: by their own land - the
landless choose to go but in full lands find nowhere, and the land concentrates: 17-66 % landless at year 300).

### Exact state (keep current)
- **C3.6a working the land harder (D2-131, sim version 66; `26` §2d)**: `SettlementSystem` - `TilthFor` (the plan at sowing: fields short of the
  want -> extra ploughing and hoeing a hectare, a share of the season's 109 work-hours, at most `TilthMax` 0.8), `GainOf` (0.30 x (1 - exp(-e/0.4))),
  `SowHa/TendHa/ReapHa/SeasonHa`, `Tilled` (the history episode), `Settlement.Tilth/TilthDone/TilthGain/TilthEvent`; `HistoryKind.FieldsWorkedHarder`
  93 (Local, place-indexed); the settlement page's food section (`Gazetteer.Food`). Headless: `--notilth` (= `_m2` exactly, seed 4's annals
  byte-identical), `--landtally` (`land open/bound:` lines - harvests, arable and fields against need, FU a ha, harvest against bread, the extra
  planned/done/added - and `land hours` by crop stage; `AgentSystem.LandHours`, `SettlementSystem.LandTally`). `TilthTests` 4.
- Runs: `tc2/out/civ/s*_t1` (the slice, `--landtally`, saves 100/200/300) and `s*_t0` (`--notilth --landtally`, the control = `_m2`); lived year
  checks `scratchpad/lived_s5_t1.txt`/`_t0.txt`; the survey before the code `scratchpad/land_s4.txt`, `land_s5.txt` (bin_t1 on the `_m2` saves).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/75232637-109e-4256-9e55-bbecfdd2e0eb/scratchpad`
  (`wrun.sh` pointed here, `pt.py` - line-ending-aware patching: `exec(open(pt).read()); patch(path, [(old, new)])` with LF texts - patch scripts
  `p_*.py`, `d2131.md`). **Never `sed -i` a CRLF file here: Git Bash's sed strips the CRs** (SettlementSystem.cs had to be restored).

- **Verified**: full suite 340/340 on 1000a8b; Web build of 1000a8b (`tc2/web-build/web` is current): Web test 6/6 twice (`tc2/out/webtest/s16web6`,
  `s16web6b`; bit-identical; fps swung with the machine's load - up to 2.6x between two runs of the same build).

- **The first sheaves (D2-132, sim version 67; `26` §2e)**: `SettlementSystem` - `CropYield`/`Reaped` (Settlement), `Sheaves` (each morning of the ripe
  stage, `k == 0` only: a calm village's step takes its grain at the step's end), the harvest's day takes the rest (`max(Reaped, CropYield x (1 - burnt)
  x Harvested)`), `Holdings.Harvest(sid, fu, repay)`, the ration to the next new grain (`ToBread`, `SheafDay` 31; `ToHarvest` reads it for the
  marriage rule and the pages), `SheavesOff` (`--nosheaves` = `_t1` exactly), `GapTally`/`SheafTally` (`--gaptally`), the settlement page's "The harvest
  is coming in". `SheavesTests` 5; WorldGenerator sets `CropYield` for a world begun in the ripe stage.
- Runs: `s*_s0` (`--nosheaves`, = `_t1`, with the gap tally), `s*_s1` (sheaves alone - worse), `s*_s2` (+ the ripe stage's ration), `s*_s3` (final), `s4_s3n`
  (the final binary with `--nosheaves`: annals = `_t1`); `s*_g1` (the gap survey from the `_t1` year-200 saves). Scripts: `gap.py TAG seeds`, `loadrun.sh`.

### Next, in order
1. (done: C3.6a committed 1000a8b; Web test 6/6.)
2. (done: the hungry gap measured and the first sheaves built, D2-132; full suite 345/345.)
3. (done: **C3.6b household land** D2-133, 4f9b94b - holdings move nothing, annals = `_s3`; **C3.6c the harvest by land and labour** D2-134 - hunger
   a person-year x0.74, the preventive check; runs `s*_h1`, `s*_c1`, `s4_c1m` = `--bymouths`.)
4. (done: **C3.6d who leaves**, D2-135 - each household weighs its own land; aggregates at par, land concentrates; runs `s*_d1`, `s4_d1v` = `--villageleave`;
   the DEEP guard reads its two worlds' herds together.)
5. (done: Web build + Web test `s16web8` 6/6 of C3.6b-d; the full suite.)
6. **Measured for the next slice (no code yet; `place --landless`, the `hunger deaths by the household's land` line - runs `s4/42/7/5_g2`, years
   200-260 from the `_d1` saves)**: landless households are 26-63 % of all, ~3.0-3.4 people, 2.0-2.4 hands, 2-17 days of their own grain;
   households holding their want are small and old (1.5-1.8 people, ~1 hand) with 2.6-5.7x their want in land and 207-1,594 days of grain. Hunger
   falls on the landless and land-short (95-99 % of hunger deaths). **Service has no economic driver yet**: the commune works all fields together, so
   a land-rich household needs no hands (its land's half comes regardless) and a servant's keep (~48 FU a year) exceeds a hand's share (~40).
   **Famine amid plenty is partly emerging** (ten worlds, hard-times hunger deaths): by mouths (`_s3`) the holders' spare covered under a quarter of
   the grainless' need at 98 % of them (store under a day's food 80 %); with land (`_d1`) under a quarter at 77 %, a quarter or more at 23 %,
   store over a day's food at 64 %. Candidates, in order: (a) **work for grain** - households with grain to spare hire the hungry for their own
   projects (rebuilding, carts, boats) at a day's bread for a day's work (C3.3b's makers' price), so the landless earn in hard times and the rich
   build; (b) alms by standing in hard times (beyond the first household's, C10.2); (c) each household works its own holding and hires hands - the
   deep version that would give service an economic base; then debts' consequences, partible customs (C11), a land market.
8. (done: **C3.6e the old hand on their holding**, D2-136, sim version 71 - the hoards and most of the landlessness were the old's; runs `s*_e1`,
   `s4_e1n` = `--noretire`.) Next: the full suite and the Web build/test of C3.6e; then re-run the 900-year check (`_l9` with C3.6e) and choose
   from 6 (a)-(c) - the hoards are gone, so alms matter less; service needs households working their own holdings.
7. **The land economy over 900 years** (`s4_l9`, `s42_l9`; PROGRESS): stable, hunger 0.2-0.8 a 1,000 person-years in seed 42's last six centuries;
   landless 54-77 %; **the households holding their want keep decades of grain (800-2,975 days)** - grain with no sink but rot, the village's sales
   and loans. This outranks (a)-(c) above: measure where a rich household's surplus goes now (rot, the village's sales borne by bins, loans,
   gifts, makers paid) and what real sinks a medieval surplus had (beasts, land, buildings, goods, feasts, alms, rents, tithes), then the smallest
   real slice - e.g. households buying beasts and goods with grain (households' own trade, D2-126 "Open") or the rich feeding the hungry as
   patrons (C10.2's first-household rule for every household with years of grain, followings by it).

## Session 16, second context (9 Oct 2026, 12:22-16:00 IST; Control Room run #39 session 2, Resource Governor MEDIUM) - C10.3 closed (D2-128); C3.5 household formation (D2-129); W-FOREST pass 1 (D2-130)

**Handoff state (end of this context)**: HEAD 284471c (+ this handoff commit); everything committed; nothing running. Verified at handoff:
`HouseholdFormationTests` 3, `LeadersTests` 7, `GazetteerTests` 2, `BiographyTests` 4; the last full suite 336/336 was on C3.5 (b8f4830 - the commits
since touch only the view, page text and headless tools); Web test s16web5 6/6 on 284471c (`tc2/web-build/web` is of it). Open: W-FOREST waits for
Arjun's judgement; the next simulation slice is C3.6 land (`26` §3, measured, not chosen).

### Exact state (keep current)
- **C3.5 household formation (D2-129, sim version 65; `07` §7e)**: `SettlementSystem` marriage loop - a woman's suitors are only the men with whom
  what the couple brings (`CoupleDays`: each one's mouth's share of their household's grain and larder; nothing from a household of another
  village) keeps them to the next harvest of his village (`ToHarvest`, `HarvestDay` 39) and 6 days beyond; none: the match waits. `--nomeans` (=
  `_d3` exactly, checked on seed 4 with the final binary). Pages: `Biography.Family` (unmarried of an age to marry: their share's days, the harvest,
  a match waiting), `Gazetteer.Overview` ("Marriages wait: ..."), the Marriage record's ArgA (days of bread; "setting up their household with N
  days' bread of their own"). Tools: `place --marriage`, `marriages by what the couple brought` line (`SettlementSystem.MarriageTally`), the hunger
  line's spare-against-need part (`Holdings.HungerTally` 7-12). `HouseholdFormationTests` 3 (+ `.meta` from Unity configure).
  Runs: `tc2/out/civ/s*_d2` (hunger diagnostic, sim as `_l5`), `_d3` (+ marriage tally; the control), `_m1` (first build: the bride's village's
  harvest), `_m2` (final), `_m2n` (seed 4, `--nomeans`). Marriage tables: scratchpad `marriage_d3.txt`, `marriage_m1.txt` (read with bin_m1,
  v64), `marriage_m2.txt`; `marr.py A B YEAR`.
- **C10.3 a contested succession splits a village - closed (D2-128, sim version 64; `10` §12d)**. Ten seeds `tc2/out/civ/s*_l5` (bin_l5 = 78ac1ee's
  build, `--contests`, saves 100/200/300) against `_l3` (= `--nosplit`): 42 splits, foundings 123 -> 119, people x0.992, vital rates and conflict at
  par, home making x1.10 explained (more people in villages too small for a shed while land is open). Seeds 4 and 6 again on the final build (`_l5t`,
  annals byte-identical to `_l5`) for the factions' outcomes (64 of 99 gave up after three years, 19 gone, 13 found a place).
- Added this context (no change to the simulation): `Leaders.ContestTally` slots 6/9/10 (factions' outcomes), the `contests:` line; `place --splits`
  (headless `Place.cs`); the pages - `Gazetteer.Overview` (a village founded by a split says why; the village left says "Split in year Y ... - V"),
  `Gazetteer.Life` merges `HistoryLedger.PlaceEventsOf` (a page-only index of `HeadmanRose`/`VillageSplit` by village: not the entity index, which
  the storm code reads); `LeadersTests` 7 (a save keeps a faction looking for land; both villages' pages tell the split); `LeadersTests.cs.meta`
  (Unity configure).
- Scratchpad (this context): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/83fa9e96-395d-4137-b152-8cc71ac55fa5/scratchpad`
  (`wrun.sh`, `bind.sh TAG` -> `binding_TAG.csv`, `bindcmp.py A B` - village sizes and binding by size and year, `splits_l5.txt`, `d2128.md`,
  `p_gaz.py`; copies of the new tools in `tc2/out/tools/`).

- **Committed d891d16 (C10.3 closed; full suite 333/333). Web build of d891d16: Web test 6/6** (`tc2/out/webtest/s16web`: browser bit-identical,
  views 60-121 fps, pages 73-82 fps; the century-later life page shows the headman lines; the fresh world's pages at 06:07 of day 1 come before any
  village has weighed its first). Then family plurals fixed in every page and history line (`HistoryText.Plural`: "the Memvases", not "the
  Memvass"; Unity configure compiles the HUD).

- **W-FOREST pass 1 (D2-130, ee5044c + the far canopy commit)**: kinds of wood from the climate (`Game/View/ForestTypes.cs`), ten new forms, stand ages,
  palettes, far forms per kind, the far canopy by kind (UV4 in `TerrainLod`/`Terrain.shader`); views `forest:KIND[:low|:far]`; shots `tc2/out/shots/wf2`
  (sheet4.png), `wf4`, `wf6`; Web tests s16web4/s16web5 6/6. W-FOREST stays OPEN for Arjun's judgement (sheet sent to him).
- Unity shots: a view name may not contain ':' in a file name (ShotRunner fixed); `unity.mjs shots` works after a Web build (the earlier failure
  was the shot runner's own bug, not the build target).

### Next, in order
1. (done: C10.3 commit, Web build, Web test s16web 6/6; famine amid plenty measured and refuted; C3.5 built and measured; W-FOREST pass 1 and 1b.)
   If Arjun answers about W-FOREST, act on it first (pass 2 candidates: D2-130 "Open").
2. (done: C3.5 closed - full suite 336/336, no measurable cost, D2-129, b8f4830; Web build of b8f4830: Web test s16web2 6/6, bit-identical, the life
   page shows the marriage's bread; spans of bread now read in years past a year - "a year's", "21 years'" - `HistoryText.Span/Bread`.)
3. **C3.6 land** (`26` §3: land binds for 91 % of people by year 300 while grown people keep ~4.5-5 free hours a day): measure which binds harder -
   labour a hectare (Boserup: more work a hectare at falling returns - tillage, weeding, manure from the herds) or who holds the land (holdings,
   landless households, labour for others; marriage on a holding) - then build the smaller real slice.
4. From the evidence (D2-129 "Open"): hunger is still 17 % of deaths, in the hungry gap before the harvest - measure what a village does in its last
   weeks (the ration's floor 0.45, wild food, slaughter, relief) before choosing; household land and holdings (marriage on a holding, the NW
   European pattern) when land binds; life-cycle service; factions a full land cannot shed (D2-128); debts' consequences; C9.4 cargo; D2-125's iron
   ask; D2-123 floods; W-FOREST.

## Session 16 (9 Oct 2026, from 09:11 IST; Control Room, Resource Governor MEDIUM: CPU 70 %, RAM 85 %, two heavy jobs) - C3.3 the household's grain, debts, makers paid, hard times (D2-126); C10.2 the village's first household (D2-127)

### Exact state (keep current)
- **C3.3a-c committed 889263b (D2-126, sim version 62; `26` §2c)**: `Core/Places/Holdings.cs` - household bins and the village's common grain in one
  granary, the moral economy and debts, makers paid in kind as far as the taker's grain pays, the village's levy, sharing contracting in hard times.
  Headless: `hold:`, `makers paid:`, `hunger by household:`, `place --households/--debts`, `--nohold` (= `_s3`), `--holdmeansoff`, `--nopay`,
  `--nohard`, `--ledgeryears`. Runs `tc2/out/civ/s*_h1`..`_h5` (`_h5` the slice; saves 100/200/300).
- **C10.3 IN PROGRESS (committed as such with C10.2; D2-128 to write; sim version 64; `10` §12d)**: `Leaders.Contest` at a vacancy (`Weigh(...,
  vacancy: true)` from `Day`), `Migration.Split` (a faction's party to the best known site, the split's record as its cause), `HistoryKind.VillageSplit`
  92, metrics `Splits/SplitHouseholds`, `--nosplit` (reproduces `_l3` exactly, checked on seed 4), tools `--contests` (`contests:` line). First
  version measured (`_l4`, ten seeds vs `_l3`): at par (people x1.02, foundings x0.975) but only 10 splits in 3,000 world-years - the tally on seed 6:
  of 205 vacancies, 107 no unbound rival of 6+ households, 42 rival under half the first's following, and of ~50 that qualified **49 failed only for
  want of a known place** (villages scout only when land is short). **Then built, not yet run over ten seeds: the pending split** - a faction that
  would not follow and knows no place looks for land (`Settlement.SplitHh/SplitCause/SplitSince`; `Migration.WantsScout`/`ScoutsDue` also for a
  pending faction; `Migration.KnowsSite`; `Leaders.Pending` daily: goes when a place is known and it still qualifies, gives up after `SplitYears`
  3). `LeadersTests` 7/7, `HoldingsTests` 12/12; full suite NOT yet run on C10.3; `LeadersTests.cs.meta` NOT yet made (run Unity configure).
- **C10.2 built and measured (D2-127 written, sim version 63; `10` §12c); the full suite passed on it: 331/331**:
  `Core/Society/Leaders.cs` (followings of kin, debtors and clients - `Household.Patron/PatronAt`, set by gifts in hard times and by loans; the first
  household and headman `Settlement.Head/HeadHh/HeadSince/HeadEvent`, `Hold` 0.5, weighed yearly at `Migration.DecideDay` and at a vacancy
  (`Leaders.Day` daily); the first household's gifts in hard times in `Holdings.Draw`; tribute to the lord's first household in
  `Conflict.Harvested`; `HistoryKind.HeadmanRose` 91; pages in Gazetteer.Overview and Biography.Identity). Headless: `leaders:` line, `place
  --leaders`, `place --binding` (C10.3 survey), `--nohead` (= `_h5` exactly), `--notribhead`. `LeadersTests` 5/5, `HoldingsTests` 12/12 (its identity
  test now switches hard times and headmen off). Runs `_l1` (Hold 0.67, no tribute rule), `_l2` (+ tribute), `_l3` (the slice; saves 100/200/300).
  `Conflict.FitAt` made public (tests).
- **The Web build in `tc2/web-build/web` is of an intermediate state** (Unity imported C10.2 sources mid-build): rebuild before any Web test.
- Scratchpad (session 16): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/c30873bf-a161-4a50-b46c-76e150e4ddf3/scratchpad`
  (`wrun.sh`, `hold.py`, `forms.py`, `leaders.py`, `ly.py`, `lycf.py`, `patch.py` - a line-ending-aware replace: `python patch.py FILE OLD NEW`;
  copies in `tc2/out/tools/`). Shell heredocs with backslashes or nested quotes break here: write texts to files.

### Next, in order
1. Unity configure (`LeadersTests.cs.meta`), then ten seeds of the pending-split version (`BIN=bin_l5 TAG=_l5 ... --contests` in `EXTRA`), compare with
   `_l3` (= `--nosplit`): splits, foundings, village sizes (`bash sizes.sh _l5 300` - the repo build reads only saves of its own sim version), people
   at par; the full suite; D2-128 (draft the evidence from the numbers above and the run); docs; commit "C10.3 closed".
2. (done: C10.2 committed with C10.3 in progress) **C10.3 a contested succession splits a village** (evidence D2-127: binding to the first household falls with size, 43 % of households under 100
   people, 8 % at 400+; unbound rival followings up to 90 households): at a vacancy, if the largest following among households not bound to the new
   first household is at least `Hold` of the first's, at least a party's worth, and the village knows a good site, the rival household leads its
   unbound following away to found a village (Sahlins: conflict at leadership transitions; Alberti 2014, Chagnon - fission past ~130-200 people
   unless integrated). Reuse `Migration.NewParty` with the site; history: the split, caused by the death. Measure: foundings, village sizes (towns
   kept only where integrated?), people at par, conflict; `--nosplit` control.
3. Web build + Web test (C3.3 + C10.2 + C10.3): the person/place pages show the grain and first-household lines (shots 5b-5f).
4. Open: the headman deciding tribute, walls or bands; the hungry taking from their own village's store; debts' work and flight; household land;
   D2-125's unmet iron ask; D2-123 floods; W-FOREST.

## Session 15 (8 Oct 2026 19:54 IST - 9 Oct; Control Room run #31 session 5) - C4.2 carts closed (D2-122), nobody in the water (D2-123), crossings surveyed (D2-124), C1.4b the village smith (D2-125)

### Exact state (keep current)
- **C4.2 carts closed (D2-122)** - the closing checks found and fixed three faults: carts begun while hunger sent the village to the wild were never
  worked (15.5 % of carts given up -> 4.9 %: `Carts.ShortBound`, the timber of a cart given up back to the woodpile; `run --carttally`); woodcutters
  with their carts stood in the sea (D2-123); the yard camera framed a roof (`CameraRig` "cartyard": the parked cart with the fewest buildings on the
  camera's side, `Footprints.LineCuts`). Ten seeds `tc2/out/civ/s*_c2` (the hunger rule) vs `_c1n` (no carts): trades x0.67, famines x0.89, people
  x0.97 (t -1.1), vital rates at par; `_c1` -> `_c2` inside the noise.
- **D2-123 nobody works in the water**: headless `wet --load S [--days N]` (a lived world hour by hour: who stands under the drawn water, by
  activity and village, and standing buildings on wet ground). Seed 4 y300: 0.64 % of person-hours in the water -> 0.05-0.08 % after: lived field,
  woodlot and wild-ground spots go ashore (`AgentSystem.Ashore`, `WildFood.GroundPoint` for every kind) and woodlots are chosen above the shore
  (`Woods.Choose` `Shore.Dry`; `Ensure` re-chooses one the sea took). Ten seeds `_c3` (woodlot rule) vs `_c2`: inside the noise. What is left:
  straight local walks across lakes, spots within 4 m of a waterline, a day's lake over works (no flood response yet).
- **D2-124 crossings surveyed**: `place --crossings` - the largest flow on five worlds is 0.08-0.23 m3/s; every stream a worn way crosses is
  fordable; C4.3 (fords, bridges) deferred until a river cannot be forded.
- **C1.4b the village smith (D2-125, sim version 61)**: `Forge.Places` 4 (`SmithBar`, `Key` = copper + iron + practice, `Maker`, `Bar` daily),
  `Person.SkSmith` (0.0015 an hour; `Forge.Hand` 0.8-1.2), more smithies where the smiths fell short 30 days of a year (`Backlog`, `Tally`, `Year`,
  `SmithMore`, `MoreWanted`, `SmithyFor`, up to 4), roles from practice (smith/ironsmith from `SkSmith` >= 0.3; boatwright from plank boats led,
  `Biography.PlankBoats`), building page names its smiths, settlement page counts smithies/smiths/short days, view smoke per smithy,
  `--nosmithcrew` (reproduces `_c3` exactly). Tools: `run --crafttally` (hours by person and village-year per craft: smithy, plank boats, shed,
  kiln), `place --list` shows SMITHY / N SMITHIES. Runs `tc2/out/civ/s*_s1` (four places, no second smithy), `s*_s2` (the slice).
  Results (`_s3` vs `_c3` = the same binary with `--nosmithcrew`): people, famines, vital rates, trade, conflict at par; standing smithies 68 -> 80;
  smiths from practice 24-116 in the three metal worlds (seeds 4, 2, 7), at most 23 hands a smithy-year (355 before), busiest hand 38-49 %; iron
  smelted x0.59 / x1.05 / x2.08, iron traded 22 -> 9 t (seed 4's exporter cannot sell beyond its places: open). Rejected on the way: `_s1` (four
  places alone: iron x0.83, iron trade x0.35), `_s2` (another smithy on short days alone: 68 -> 218 smithies).
- **Verified**: full suite 314/314 (12 min) on the final code; ten seeds as above; shots `tc2/out/shots/c42a`/`c42b`/`c42c` (carts: on the green, in the yard, the earlier
  woodcutters in the sea), `c14b` (Fomthaenstead #20, seed 4 `_s3` y300: two smithies side by side, smoke from the worked one; woodcutters with handcarts
  at cut-over woods; handcarts at the market green). Web build of 96e38ab: Web test 6/6 (`tc2/out/webtest/s15web`, views 77-121 fps, pages 69-88 fps); `tc2/web-build/web` is current.
- **Machine**: Control Room's Resource Governor LOW (CPU 50 %, RAM 75 % for all work; one heavy job at a time). The machine's own baseline sits at
  ~73-75 % RAM (the Claude apps ~2.5 GB, an `oracle` process); a Unity run takes it to ~85-88 % while it runs. A `chrome-headless-shell` not started
  by this session took ~70 % CPU for an hour (left alone). `dotnet build-server shutdown` frees this session's compiler servers.
- Scratchpad (session 15): `C:/Users/aswin/AppData/Local/Temp/claude/C--Game-Dev-tiny-consequences/31c113f7-7357-46b4-9c76-c4b097feb4cb/scratchpad`
  (`wrun.sh`, `smith.py` - the metal chain between two tags; copies of both in `tc2/out/tools/`).

### Next, in order
1. **C5.3c re-weighed (measured at the end of session 15, no code)**: the premise "smelt at the ore to save carrying it" does not hold here - seed 4
   `_s3` dug 148 t of bog ore for 18 t of iron in 300 years: carried home in ~5,900 loads, at most ~2,600 hours, against **552,000 hours at the
   smithies** (bloomery iron at 0.06 kg an hour is the bottleneck; carrying is ~0.5 % of the chain). A bloomery at the bed saves almost nothing, so
   iron hamlets would be theatre unless something else draws them (charcoal from far woods? a crew camping out?). What D2-125 left open is smaller:
   an **unmet ask** (a trader who wanted iron and found none, counted at the seller) so a seller's `NeedsMore` sees its buyers. Iron trade is small
   beside pottery (9 t against ~2,000 t over ten worlds) - weigh it against item 2.
2. **C3.3 exchange between households** (`26` §3: a phase - household property, sharing by need, debts). **C3.4 the mill was surveyed at the end of
   session 15 and deferred** (`timeuse --load`: grown people spend 0.7 h a day preparing food and 7.9 h idle - a mill frees spare hours only; `26` §3).
3. D2-123 "Open": a lake risen over works or homes is not a flood people answer (they stay and work in it); local walks are straight lines across
   lakes. A flood slice with the water's own rise.
4. W-FOREST (Arjun, 8 Oct): forests and biomes still read as the same trees recoloured - an art pass.

## Session 14 (8 Oct 2026, 19:02-19:26 IST; Control Room run #31 session 4) - C4.2's people x0.955 explained as noise (stopped early by Arjun)

Short session: Arjun asked to stop and hand off at 19:13. No code changed; HEAD is still d6c29cf's code (sim version 60) plus docs.

### Exact state (keep current)
- **C4.2 carts is still IN PROGRESS** (built and committed in d6c29cf; Session 13 below has the full build). The one open question was why
  people over years 200-299 are x0.955 (t -2.4) with carts against `--nocarts` (`_c1` vs `_c1n`). **Findings this session:**
  1. **Per-person rates are unchanged.** Over years 100-299 and all ten seeds: births 43.91 vs 43.75 a 1,000 person-years, other (age) deaths
     28.84 vs 28.68, net growth 7.20 vs 7.21 a 1,000; hunger deaths 7.03 vs 6.76 (fewer with carts); sickness deaths 0.59 vs 0.86 (driven by two
     epidemics, below). The loss is in the *level* (person-years x0.954), not in how people live and die.
  2. **Seed 4** (the largest loss, 6,533 -> 5,605): both arms track together to year 179; the cart arm's year-180 epidemic killed 1,289 against
     734 (people 3,025 -> 2,357 against 2,954), and the gap stays at 15-20 % to year 300. Villages over years 200-299 at par (22.6 vs 22.9).
  3. **Seed 42** (6,517 -> 6,142): the arms split around years 20-40 (a few FU in one granary at year 20, people apart from year 40) and the cart arm
     grows a little slower, 1.42 % against 1.64 % a year over years 50-100, with **no hunger, no sickness and only 4 trading trips with a cart in
     100 years** (41 carts made, mostly for wood). Diagnostic runs (100 years, `--places`; both reproduce `_c1`/`_c1n` exactly) are in
     `tc2/out/c42diag/s42_n.*` and `s42_c.*`. Their demography lines: conceptions a weighted woman-day 0.02237 vs 0.02182, marriages 0.01168 vs 0.01120,
     infant deaths 213 vs 233 a 1,000 infant-years - all within chance at these counts. Arable land at year 100 384 vs 343 ha follows from the
     smaller population (fields clear the woods), it does not lead it.
  4. **The placebo, all ten seeds** (`--nocarts --nudge 20`: no carts, one FU more in one store at year 20; tag `_c1p`, `tc2/out/civ/s*_c1p`,
     same binary as `_c1`/`_c1n`, d6c29cf). People over years 200-299 against `_c1n`, seed by seed (4 42 2 1 5 3 11 8 7 6):
     placebo x1.007 0.963 1.004 0.857 0.889 0.998 1.075 1.032 1.016 0.999 (mean x0.982, t -0.8, sd of the log ratio 0.069);
     carts   x0.858 0.942 0.991 0.887 1.021 0.951 1.001 0.904 1.014 0.993 (mean x0.955, t -2.4, sd 0.061).
     **Carts against the placebo: x0.972, t -1.06.** The placebo moves single seeds as far as carts do (seed 1 x0.857, seed 5 x0.889).
     What the placebo does NOT reproduce (so these are the carts' real effects): famines x0.83 (t -3.4; placebo x0.98, t -0.2), hunger deaths x0.93
     (t -2.3; placebo x0.99), trades x0.70 (t -4.9; placebo x1.01). Foundings x0.93 (t -1.6) against the placebo's x1.01 (t 0.6) is weak.
  - **Verdict: the people x0.955 is inside the noise - no cart fault.** The vital rates are identical, the two big losses are an epidemic
    (seed 4) and an early chance split with almost no cart trade (seed 42), and the placebo's spread covers it. Nothing to fix; it goes into D2-122
    as evidence (the D2-113 / D2-114 pattern).

### Next, in order
0. (done) The `_c1p` placebo finished (19:24): x0.955 is noise (above).
1. **Close C4.2** - the investigation is finished, only the closing steps are left:
   **D2-122** (the measured state, the design in `24` §2 "Implemented (C4.2)", the ten seeds and this
   investigation), shots (`node tc2/scripts/unity.mjs shots --tag c42a --load "<abs>/tc2/out/civ/s4_c1/world_y300.tc2" --village 2 --views
   "cart@11,cartyard@11,cartfar@14"`), the full suite (`node tc2/scripts/headless.mjs test`, ~12-22 min; last full pass 302/302 was before carts),
   Web build (`node tc2/scripts/unity.mjs build-web --jobs 3`) and Web test (`node tc2/scripts/webtest.mjs --tag c42web`), commit, docs (`24`,
   `25`, PROGRESS, this file, both NEXT_SESSION_PROMPTs).
2. Then Session 13's "Next" items 2-4 (C3.3 exchange between households; roles from practice for smiths and boatwrights; C9.4 / C3.4).

## Session 13 (8 Oct 2026, from 16:11 IST; Control Room run #31 session 3) - C3.2 the potter's kiln, jars and hearths' pots (D2-121)

### Exact state (keep current)
- **C3.2 built (D2-121, sim version 59)**: `Core/Places/Pottery.cs` (jars `Settlement.Jars` seal the store's grain: `RotFactor`; `Day` stands ware as
  jars and breaks them; hearths `Household.Pots`, `Draw`, `Wear`, `CookFactor`, `FiresOwn`; the kiln `KilnWanted` (two months of a potter's jars,
  `KilnFrom` 1,440 kg), `Site` at the clay, `Bar`/`Maker` (4 `Hands`), `Work` (lived; COARSE less the walk), `WoodDay` in `FuelDay`; `Worth`/`Spare`
  for `Trade.Pots` 7), `Learning.Clay`/`ClayCell` by the ground's shape (`ClayAreaHa` 200 ha or a depression; cached by the drainage's version),
  `Structures.Kiln` 24, `ActivityKind.Pot` 36, `WhyCode.Pot` 46, `Person.SkPot`, `Settlement.Kiln/PotEvent/Pots/Jars/PotsOut/PotsSold/HearthShort/
  PotBar/PotBarId`, metrics `PotsMade/KilnHours/PotsDrawn/JarsSet/PotsTraded`. Pages: HUD building page, person page (role "a potter at the kiln"
  from practice; how the household cooks), settlement page "Pots and jars". View: kiln (x1.15), clay pit (grows with ware fired), ware in straw
  (ware to sell), storage jars by granaries (sealed >= 30 %), smoke while a potter works; yard trees kept off works. Kit `kiln`, `clay_pit`,
  `storage_jars`, `ware_stack` (+ metas). Headless `pots:` line, `--nokiln`, `--potsflow`, `place --stores` (the survey), `place --list` jars.
- **Runs**: `tc2/out/civ/s*_k1` (C3.2, saves 50/100/200/300, `--rot --potsflow`: the reference for the next slice), `s*_k1n` (same binary,
  `--nokiln`: D2-063's rules; saves 100/300). Smoke run `tc2/out/c32/s4_p1` (100 years, KilnFrom 720). Tools in `tc2/out/tools/`: `pots.py TAG
  [seeds]` (the pots: lines), `agg.py` (now with felled, rot_pct, pots_traded, kilns).
- **Results** (`_k1` vs `_k1n`): 84 kilns in 169 villages, 1,988 t traded, jars seal 80-100 %, trades x5.4, markets x3.6, rot/people at par,
  famines x1.06 (t 0.7). Tests: `PotteryTests` 8/8; `BiographyTests` (role from practice); `TheCoarseDayRaisesBandsAtTheLivedRate` six worlds.
- **Shots** `tc2/out/shots/c32a`, `c32b2` (Krumere #2, seed 4 y300), `c32c21` (Krailddal #21: ware in straw). Kit previews in the session scratchpad.
- **Found and fixed on the way**: the view's works table had 24 slots (the kiln is kind 24: Start threw and left later instancers null); a
  yard tree grew out of the kiln's dome (yard trees now keep off every work's ground).

- **C3.3 surveyed and deferred** (commit 3db3cec; `26` §3): a tools household ledger (`run --ledger Y`, `Core/Diagnostics/HouseLedger.cs`, null in the
  game) - the village's store moves 31 % of the food households draw from 43 % givers to 57 % takers; household property needs a moral economy
  (sharing by need, gifts or debts), household fields and inheritance: a phase, weighed with C10.
- **C4.2 carts - IN PROGRESS, uncommitted evaluation open (D2-122 not yet written; sim version 60)**. Built: `Core/Places/Carts.cs` (handcart 120 kg
  at 0.85 pace, ox cart 500 kg at 0.7 where the household keeps a grown ox; household fields `CartKind/CartState/CartHours/CartAt/CartNeed`;
  wanted after `NeedTrips` 3 back-bound needs within `NeedYears` 2 - a trader's back's load where more was spared and wanted on a path or road
  (`Trade.Load`), or a wood/stone stint from 400 m+ (`Woods.Cut`, `Stone.Cut`) - with an able wright (SkWood 0.4, 16-59, well); made in household
  hours (lived choice 24 `WhyCode.Cart` 47 u 0.5; COARSE up to `HoursADay` 2 h before the village's building - given only what building left, half
  the carts begun were never made); `Trade.Load/Exchange` cart loads on worn ways; `BeginTravel` at `Carts.Pace`; `Woods.Rate`/`Stone.Rate` take
  `trip` loads and `pace`; `HistoryKind.CartMade` 90; metrics `CartsBegun/Made/Worn/GivenUp/CartTrips/CartKg`; pages (household cart, settlement
  "Carts", cargo "on the household's handcart"); view (parked carts `_parked/_parkedOx` at the yard's end wall, carts with traders/haulers
  `DrawCart`, an ox before an ox cart, a load of sacks); camera `cart`, `cartfar`, `cartyard`; kit `handcart`, `ox_cart`, `cart_load`; headless
  `carts:` and `loads:` lines, `--nocarts`, `--loads`, `place --haul`. `CartsTests` 5/5; Unity compiles (0 errors). `TimeUse` counts all kinds.
  Measured first: 83 % of trading loads bound by a back's load; grown oxen in 1-2 % of households at year 300; ways mostly paths/roads.
- **C4.2 ten seeds** `tc2/out/civ/s*_c1` (carts; saves 100/200/300) vs `s*_c1n` (same binary `--nocarts`; equals `_k1` exactly - seed 4 10,791):
  at year 300 ~35-45 % of households keep a cart (seed 4: 1,098 handcarts, 45 ox carts; 3 % of carts begun given up); **famines x0.83 (t -3.4),
  hunger deaths x0.93 (t -2.3)**, trades x0.70 (fewer, bigger loads), trade gain x0.93, markets x0.93, wood felled x0.985, births/deaths a
  thousand at par - but **people over years 200-299 x0.955 (t -2.4)** (seed 4 9,655 vs 10,791; 42 7,138 vs 7,701). NOT YET EXPLAINED - find
  why first (villages fewer? foundings, households setting out, the cart hours taken from building homes so households camp longer, grain sold
  away in cart loads, a tally `cmp.py c1n c1` and `agg.py c1n c1` - parties/foundings/relief). **Leads** (read from the reports at the handoff):
  foundings x0.926 (t -1.6), households setting out x1.46 (t 1.5), relief x0.88, home making hours x1.02; villages at year 300 fewer in 5 of 10
  seeds (s4 28 vs 30, s42 18/19, s2 27/29, s1 7/8, s8 11/13) and people lower in 6 - fewer new villages founded may be the loss (land is what
  bounds people). Look at the parties that set out and did not found (`tools`: `run --places`, `vtrace`), and whether carts change who leaves
  (`Migration.Weigh`: hardship read from stores sold away in cart loads?).
- **Verified at the handoff (d6c29cf)**: `CartsTests` 5/5, `PotteryTests` 8/8, `SaveLoad` 8/8 on the committed code; Unity compiles with 0 errors.
  **Not verified since carts**: the full headless suite (last 302/302 at d21d09d, before carts and the C3.3 ledger hooks), shots of carts, the Web
  build (`tc2/web-build/web` is still d21d09d, C3.2) and the Web test.

### Next, in order
0. **Finish C4.2**: explain the people x0.955 (above) and fix it if it is a fault (not a mechanism), then D2-122, shots (`cart`, `cartyard`
   views on `s4_c1/world_y300.tc2`), full suite, Web build + Web test, commit, docs.
1. (done) Full suite 302/302; Web build of d21d09d: Web test 6/6 (`tc2/out/webtest/c32web`, views 86-120 fps, pages 68-84 fps); the Web build in
   `tc2/web-build/web` is current.
2. **C3.3 exchange between households** (payment in kind: makers paid; households' own holdings replacing D2-114's `Means`) - the weaver's and the
   potter's ware is the village's and free to its households; export crafts wait for it.
3. Roles from practice for smiths and boatwrights (the potter's and the weaver's are now from practice).
4. C9.4 (cargo by sea) re-weighed with the pottery traffic (markets x3.6); C3.4 the mill. W-FOREST stays open for an art pass.

## Session 12 (8 Oct 2026, from 12:13 IST; Control Room run #31 session 2) - C3.1 the weaver's shed and wares (D2-119); lived save/load exact again; C3.1b more sheds (D2-120)

### Exact state (keep current)
- **C3.1b DONE (D2-120, sim version 58)**: `Settlement.Sheds/ShedShort/ShedMore`; `Weaving.LoomsOf` (8 a shed), `ShedFor` (the nearest working shed;
  lived, the shed's id rides in `WhyArg`), `Tally` (a day the households drained the store below half `Kept` with fibre for every loom),
  `Year` (at `Migration.DecideDay`: >= 30 short days and `NeedsMore` - its people in wares would outwear its looms), `MoreWanted` -> another
  `Workshop` in `Structures.Plan`; `Structures.Lost` hands the first-shed role on. Ten seeds `tc2/out/civ/s*_w7` (the reference now) vs `_w4`:
  home making x0.67, market places x1.18, people at par; towns of 500+ carry 2-4 sheds. Rejected rules: `_w5` (target shortfall: sales
  drained the store), `_w6` (shortfall alone: the change-over from homespun). `WeavingTests` 8; 294 headless tests; **Web test 6/6**
  (`tc2/out/webtest/c31bweb`, views 81-120 fps, pages 71-84; the Web build in `tc2/web-build/web` is current); shot `c31d` (Dahope, 3 sheds).
- **C3.1 DONE (D2-119; commits f89d537, 3cbfcf3, a8fb590 and the closing commit; sim version 57)**: `Core/Places/Weaving.cs` (shed wanted where wear keeps 6 makers busy; makers
  = the village's 8 best weavers by `LoomBar`; `Work` 1.5x pace from real fibre; `Draw`; `Target`; `Share`), `Goods.Wear` (wares wear 0.6),
  `Household.Wares`, `Person.SkCraft`, `Settlement.Workshop/LoomEvent/Wares/WaresHeld/WaresOut/WaresSold/LoomBar/LoomBarId`, `Structures.Workshop`
  23, `ActivityKind.Weave` 35, `WhyCode.Weave` 45, `Trade.Wares` 6, metrics `WaresMade/WaresDrawn/WaresTraded/ShedHours`; pages (Gazetteer
  "Cloth and goods", Biography role from practice, HUD building page); kit `weavers_shed`, `tenter` (exported; **Unity .meta files not yet made**:
  done, committed); view (tinted shed, tenters
  while wares are in store; the first at the shed's free end), camera `work23near` (a standing shed first); headless `wares:` and `water caches:`
  lines, `place --list` wares, `person --pick weaver`, `run --noweave`, `run --waresflow` (tools tally: what wares were traded for).
  Edge works keep `Structures.RoadClear` off the main road's line (a shed stood in the road in the first shots).
  `WeavingTests` 7; 293 headless tests pass.
- **Save/load fix (D2-119 (7))**: lived worlds saved and loaded diverged (pre-existing): the green cache kept while lakes moved; shore and river
  lines cached from the day's lakes/flows. Now `PlanetState.WaterDay` (green), `PlanetState.InBasin`/`BasinVersion` (shore, `Shore.DryLand`),
  `RiverLines` for every reach >= 10 ha draining, filtered by `RiverLines.Stream` (homes, view). Test `SaveLoadContinuesIdenticallyWhileLakesRiseAndFall`.
- **Measured before the design** (`26` §1): goods at target everywhere, ~8 idle hours a day, fibre the scarce input.
- **Runs**: `tc2/out/civ/s*_w4` (ten seeds, C3.1 final: the reference for the next slice; saves 50/100/200/300), `s*_w4n` (same binary `--noweave`:
  the control), `_w3`/`_w3n` (before the road rule). Tools in `tc2/out/tools/`: `wrun.sh`, `cmp.py`, `wares.py` (the C3.1 lines a seed), `agg.py`
  (report quantities' ratios between tags). Results: trades x3.0 (t 5.6), gain x3.8, home making hours x0.28, market places x1.35, people x1.04,
  births/deaths a thousand at par; `_w3` had shown foundings x0.91 / relief x0.82 - gone in `_w4`: noise. Seed 4 `--waresflow`: 4,688 wares for
  6,364 kg fibre, 5,446 FU grain, 929 hides, 331 kg metal.
- **Shots** `tc2/out/shots/c31a` (Brernthorpe), `c31v5` (a shed in the road - before the rule), `c31v11` (Toularndal: open bays, weavers, vat, yarn),
  `c31c5` (Stairby: beside the road, cloth on the tenter), `c31b14` (Demtrerhope). **Web test 6/6** (`tc2/out/webtest/c31web`: views 74-121 fps,
  pages 76-85 fps); the Web build in `tc2/web-build/web` is current.
- Seed 4 first look (`_w3` vs `_d7`): sheds in 23 of 28 villages (smallest 167, largest without 154), home making -76 % hours, trades x2.0, markets
  6 -> 13, people 9,095 -> 10,158; big towns use all their wares (8 looms short), mid towns sell, small villages wear bought wares.

### Next, in order
1. **C3.2 the potter's kiln** (`26` §2) - **surveyed** (`run --rot`, a tools tally, trajectories unchanged): **14.6-17.3 % of the harvest rots in
   store** (seed 4: 6.78 M of 39.3 M FU, seed 7: 2.66 M of 18.3 M; heaps beyond the granaries add <0.5 %) under today's 12 %/year x the best
   potter's 0.65 - a real job for pots. Pots and storage jars made at a kiln by the village's potters, the store keeps better by pots made (not
   by the best potter: D2-063), charcoal at the kiln, clay dug at a clay pit - the weaver's shed's pattern (makers by skill, a need, real
   material, a product that lasts). Or C3.3 exchange between households (payment in kind: makers paid; export looms then pay).
2. Roles from practice for smiths, potters, boatwrights (a year-300 person is "known for" every craft).
3. C3.4 the mill; C9.4 / C10.2 re-weighed with the traffic.

## Session 11 (8 Oct 2026, 09:23 - ~12:15 IST; Control Room run #31 session 1) - C13.3 a town's ground (D2-118) closed; C3.1 designed

### Exact state (keep current)
- **C13.3 done (D2-118), sim version 56.** `Core/Places/Footprints.cs` (rectangles of each plan as the kit builds it, `Boxes`/`BoxDisc`, `HomeFits`,
  `WorkFits`, `Open`, `Audit`, `Room`, `YardFree`, `LineCuts`, `FaceAt`/`FaceOf`, `WingSide` = -1 measured in the game); `Building.Face`/`Faced`;
  `Settlement.MarketR`, `CoreR`, `LanesShut`; `Architecture.Row` (plan 5) and `Fit`/`Fallback`; `Homes.FreeLot` by ground with half-step plots in a
  town's core, `Homes.InOpen`/`Clear` (`HistoryKind.HomeCleared` 89), raising re-lots a camp whose lot was built over; `Streets.Grow`/`CorePlot`/
  `Shut`/`OpenLanes`/`StreetCuts`/`LaneCuts` (a street laid through at most 3 buildings, `Bend` 20 degrees); `Structures.MarketDealt`/`Opened`/
  `AtSite`/`Laid` (`Clear`/`Clearance` removed); `Conflict.WallRadius` between houses (`Astride` counts camps); Gazetteer tells streets, core, market
  place; metrics `HomesCleared`, `TownHouses`. View: `PlaceFaced` (the village's own frame), `Plot` cache (`ReadPlots`), stalls, store stacks,
  market ground (TerrainLod), the row-house overlays, `ShowGround` (shots `--ground`), camera views `rowclose/rowlow/rowstreet/rowmarket/hall/mouth/
  mouthfar`; `Rivers.Mouth` (the river-ribbon-over-the-sea fix). Kit: `home_row_g/h/j`, `frame_row` (+ metas). Tests `FootprintsTests` 6;
  `CharcoalTests` updated; 285 headless tests.
- **Rivers** (`Core/Planet/RiverLines.cs`): the river line moved from the view into the core (one line for sim and view); mouths at the drawn
  waterline or carried across a beach; `Footprints.Wet`/`WetDisc` keep homes and works 1.5 m off the bank; `Homes.InOpenOrWet` (4 a river).
  The view's `Rivers` reads it (widths, ranks); `WaterView` joints by reach.
- **Runs**: `tc2/out/civ/s*_d7` (ten seeds, final rules: the reference for the next slice; saves 50/100/200/300), `_d6` (before the river rule),
  `s4_d1`..`_d5` (rule by rule); profiles `tc2/out/c133/prof_bin_d6.txt`, `prof_bin_d7.txt`. Tools in
  `tc2/out/tools/` (`plotfoot.py` draws the ground; `wrun.sh`, `cmp.py`); this session's scratchpad holds `wrun.sh` with its path.
- **Shots**: `tc2/out/shots/c133a` (Rainthorpe), `c133g` (ground plates), `c133b` (Vodrothorpe, seed 2), `c133c` (Souhope's busy market, seed 42),
  `c133d` (hall with ground plates after the wing fix). Layout plots `tc2/out/c133/`.
- **W-FOREST** (Arjun 8 Oct): forests and biomes still read as the same trees recoloured - an open W task (`18` §8, `25`), NOT solved.
- Known (cosmetic): a river draining into a sea cell drawn as a wide beach can end on the sand short of the water (the sea cell's direction is
  not the water's: Vodrothorpe, seed 2); no ribbon runs over water any more.
- **C3.1 design** written (`26_CRAFTS_AND_EXCHANGE.md` §1: the weaver's workshop and the village's wares); no code yet. Hooks found: lived
  choice block in `AgentSystem` (~line 1009, the smithy's `Forge.Wanted` -> choice 15; add a weave choice and `ActivityKind`), COARSE labour
  day (~line 1803, `Forge.Work`), household wear in `SettlementSystem` (~line 215: draw wares before making), `Trade.Value/Spare` (a wares good).

### Next, in order
1. (done) Web build of e6ab82f: Web test 6/6 (`tc2/out/webtest/c133web`, views 64-120 fps); the person/place pages 67-83 fps - the C13.2 dip
   (54-59) was machine load, not a regression. The Web build in `tc2/web-build/web` is current.
2. **C3 exchange** (chosen over C10.2 - trade is 0-5 traders a year at most villages and markets are rare because barter needs a double coincidence
   of wants; lordships rarely hold): design in `21` / a new `26` doc, first slice workshops and wares (a skilled maker's workshop by need, wares made
   for the village and traded), then exchange within a village and between villages; it gives the market places (C13.3) and the sea (C9.4) a trade.
3. C13.4 suburbs and a second wall; C10.2 when lordships hold.

## Session 10 (8 Oct 2026, 06:15 - ~09:10 IST; Control Room run #23 session 2) - D2-114 closed, C9.4 deferred, C13.1 and C13.2 closed

### Exact state (keep current)
- **D2-114 building variety closed** (DECISIONS D2-114, closing evidence; `13` §13 "what the real shots taught", `21` §6 "a household's means").
  Commits: 5d4bbc2 (hall cap `Architecture.HallRoom`, Unity metas of the new kit), 5805423 (means v2, roofs kept and weathered, kitchen gardens,
  doorstep yards, training grounds beaten, the smithy sooted, the pages, headless `place --list/--households/--standing`), and the closing docs
  commit. Sim version 54 (unchanged: no format change since session 9). 275 headless tests pass. Web build of this code tested 6/6
  (`tc2/out/webtest/d2114web`, 59-122 fps); the Web build in `tc2/web-build/web` is current.
- **Runs**: `tc2/out/civ/s*_a4` (hall cap, first means), `_a5` (every D2-114 rule: the reference for the next slice), `_a5p` (placebo: `--nudge 150`
  on the `_a5` binary). Saves at 50/100/200/300 in `s*_a5/`. Tools in this session's scratchpad (`wrun.sh`, `cmp.py`; copies in `tc2/out/tools/`).
- **Shots**: `tc2/out/shots/arch1*` (before), `arch2lord`, `arch2open`, `arch2y100`, `arch2works`, `arch2works2`, `arch3works`, `arch4`.
- **Found, not ours**: a river mesh continues past the shore out over the sea (browser shot `d2114web/4_close.png`, `arch2lord/village_10.png`) -
  offered to Arjun as a separate task chip ("Fix the river ribbon drawn out into the sea").

- **C9.4 surveyed and deferred (D2-115)**: sea traffic is thin (0-318 voyages a world in 300 years, nothing left unsailed) and relief by sea was
  bound by the boats' room in 0-19 of 69-538 gifts (`run --seashort`): no demand for cargo boats; docks and cargo wait for exchange (C3).
- **C13.1 streets closed (D2-116)**, commit 7b17902, sim version 55: `Places/Streets.cs`, `Settlement.StreetN/Street0..3`, `Homes.FreeLot` frontage,
  `Structures.Clear`, the view (houses face their street, door paths, street ground; `TerrainLod.Ground1` read once a patch). Runs `_b1`, `_b2`
  (ten seeds: the reference for the next slice), `_b3` / `_b3n` (A/B in one binary, `--nostreets`). 277 tests; Web test 6/6 (`c131web`).
  Shots `street1`, `street1y100`. Layout plots: `place --layout` and the scratchpad's `plotlay.py` (copy in `tc2/out/tools/`).

- **C13.2 back lanes closed (D2-117)**: `Streets.Lines/Line/IsLane/Weigh` (lanes derived from streets, no new state), `Homes.FreeLot` over all
  lines, the view (`Facing` with its line; lanes' ground). Runs `_c1` (seed 4), `_c2` (ten seeds: the reference now). Shots `lanes1`.

### Next, in order
1. **C13.3 rows in a town's core** (designed, not begun): where a town's street frontage near its green is taken (a village of 250+, frontage
   within ~60 m of the green), the plots are divided - infill lots at the half-step between the 13 m frontage lots (`Streets.Lot` at j + 0.5;
   `Homes.Taken` keeps 6 m, so 6.5 m infill passes). A narrow plot forces a narrow, tall house: `Architecture.HomeForm` gives a lot narrower than
   10 m along its line (nearest home on the same line, `Streets.Facing` with its line) only `Two` (6.6 m long) or `Small` (4.8 m), never a longhouse
   or hall (12-15 m would overlap its neighbours) - so two-storey houses stand wall to wall in rows along the main streets, as burgage plots were
   divided in medieval towns. Check overlaps in `place --layout` plots (`plotlay.py` draws houses as points: add plan lengths), shots
   `close`/`low`/`street` of a town of 600+ (`s4_c2` Vumthorpe #19 or Rainthorpe #7), ten seeds against `_c2`, Web. Then the market place (the
   green widened where traders come: `Settlement.TradeIn`, lots kept clear within a radius growing with it) and the green kept open.
   Or **C10.2** the political centre (a lord's seat raised with tribute labour, gates where streets cross a ring, towers): lords are 0-2 a world,
   almost all with one tributary - lordships that hold come first; walled villages are common (up to 29 a world).
2. C9.4 waits for C3 exchange (D2-115).
3. Re-measure the Web person/place pages on a quiet machine (54-59 fps in `c132web` against 75-85 in `c131web`).

## Session 9 (8 Oct 2026, 04:31 - ~06:30 IST; Control Room run #23 session 1) - handed off (context full; D2-114 in progress)

### Exact state (keep current)
- **C9.3 closed (D2-113), commit 3900ec2**: relief by sea's extra famines traced village by village (seed 2: an epidemic one world had and the
  other not - chance; seeds 11 and 8: relief an unconditional subsidy holding land-short villages over their land for ever) and fixed by **social
  storage** (relief owed back: `DeedBook.Owed`, `Trade.CreditDays` 60, `Trade.GiveBack`, `ReliefReturned` 87); **lived voyages** (`Trade.Booking`,
  `AgentSystem.StartVoyage`/`VoyageStep`/`PutOut`/`VoyageLanded`, `Trade.Deliver`/`LoadAboard`, `ActivityKind.Voyage` 34, `Person.Voyage`); one rule
  at both resolutions (`Trade.Crewed`, `CanSail`, `MinSeaLoadFu`). Sim version 53; 269 tests at that commit. Ten seeds `tc2/out/civ/s*_d5s`
  (and `_d5n` no sea) against `_c92r`: famines x0.971, hunger per person-year x0.999, people x0.955; sea vs no sea inside the placebo's spread
  (`_d2p`). Web build of the C9.3 code: Web test 6/6 (`tc2/out/webtest/c93web`; views 55-120 fps - the forest at low altitude 55, the person page
  48-58: lower than C9.2b's 73-120, re-measure on a quiet machine). Shots `tc2/out/shots/voyage1`, `voyage2`.
- **D2-114 building variety - IN PROGRESS (Arjun's standing requirement, see the root `NEXT_SESSION_PROMPT.md`)**. DECISIONS D2-114 records the
  design and evidence so far; `13` §13, `18` (end), `21` §6 carry the requirement. In the tree (committed as work in progress at the handoff, see
  `git log`): `Core/Places/Architecture.cs` (forms, traditions with drift, `Means`, `Gain`, `Size`, `Describe`), `Building.Form`,
  `Settlement.Tradition`, `Household.Rebuild`, `Homes.WeighRebuilding`/`Rebuilt` (yearly, at `Migration.DecideDay`), `Construction.OwnWork`/
  `OwnStage`, `HomeRebuilt` 88, `WhyCode.Rebuild` 44, `Metrics.HomesRebuilt`, sim version 54; `Tests/Core/ArchitectureTests.cs` (5 pass); headless
  `forms:` lines; the kit `tc2/art/kit.py` (homes x15, frames x5, chimney, plinth, outshut, barn, granary, smithy, market_stall, market_cross),
  exported to `Game/Resources/TC2Models` (old `house_*` and `market_stalls` removed); the view (`WorldObjectsView`: homes from their form,
  overlays, pens, works tinted, the market; `Instancer.Add(.., wall, ..)`; `Instanced.shader` `_WallColor`).
- **Verified**: headless build, float lint, `ArchitectureTests` 5/5 (the full suite was NOT rerun after the rebuilding patch); without rebuilding
  the trajectory is bit-identical to `_d5s` (seed 4). **Not yet verified**: the Unity compile of the view/shader (a `unity.mjs configure` was
  started at the handoff - its log `tc2/out/unity_configure_d2114.txt`; it also makes the new models' and `ArchitectureTests.cs`'s `.meta` files,
  which must be committed); no in-game shot of the new houses yet; no Web build.
- **Runs**: `tc2/out/civ/s*_a3` (ten seeds with rebuilding; saves at 50/100/200/300). First three (seeds 4, 2, 7): 367-906 houses rebuilt in 300
  years, two-storey houses 125-302 (36 on seed 4 without rebuilding), but **hall-houses 54-129 - too many** (each rebuild by whoever is best off
  that year adds a hall: cap it - one standing hall a village, or one per ~150 people); people at year 200-299 +0-10 %, famines up on all three
  (74->99, 111->163, 107->131) - judge only with all ten against `_d5s` and the placebo's spread.
- Scratchpad (session 9): `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\9cd1c516-cc19-4358-8f6c-acaaf0252961\scratchpad`
  (`wrun.sh`, `cmp.py`; copies in `tc2/out/tools/`).

### Next, in order
1. Check the Unity compile log (`tc2/out/unity_configure_d2114.txt`); fix any error; commit the new `.meta` files.
2. Cap hall-houses (one standing hall a village, or by population) and re-read the ten `_a3` seeds after the fix against `_d5s` with `cmp.py`
   (people, famines, hunger, the wood store: rebuilding takes labour and wood) - a placebo if an effect looks small.
3. Shots: `node tc2/scripts/unity.mjs shots --tag arch1 --load "<abs>/tc2/out/civ/s4_a3/world_y300.tc2" --village big --views "close@10,village@10,region@10"`
   and a small young village (`world_y50`), a walled town, a lord's village; judge silhouettes and colours (zoom crops); tune the kit and colours.
4. The building and person pages: `Architecture.Describe(b.Form, home)`; then a Web build and its frame rate (more home models = more draws).
5. Then D2-114 closed in DECISIONS/PROGRESS/handoff; then C9.4 docks, cargo boats and coastal trade, or C10.2 leaders - halls, towers, gates and
   keeps as real structures.

## Session 8 (7 Oct 2026 22:40 - 8 Oct 2026 ~02:20 IST, Control Room run #7 session 3) - handed off (context full; C9.3 in progress)

### Exact state (keep current)
- **Commits this session**: 9a2ceb7 + 23ac1c9 **C9.1 closed** (D2-110: ten seeds after the landing fix found boats in the square and twice in
  the sea - `Boats.ShoreOf` ring search, `ShoreMoved`, `Afloat`; `_c91k` clean, at par with D2-108; Web build of 9a2ceb7 tested 5/5 at 68-120 fps
  (`tc2/out/webtest/c91webb`); shots `boats2`), c5fd15f **C9.2a the landing and the crew** (D2-111, sim version 50), then **C9.2b boats of
  planks** (D2-112, sim version 51; 262 tests).
- **C9.2a (D2-111)**: one landing a village (`Settlement.LandX/LandY/LandUx/LandUy/LandAt`, `Boats.EnsureLanding/Beach/Waterline/UpTheBeach/Drawn/
  Landing`, `AtWater`/`AtSea`/`Afloat`), boats in a row on the sand a metre above the sea, following the coast (`ShoreMoved`) and a risen sea
  (`Plan`); lived crews (`AgentSystem.Crew`, `Outing`, `CrewWait` 30 min, `BeginLocal(.., endAt, pace)`), metrics `BoatOutings`, `CrewSeats`;
  shore fishers at the waterline (`Boats.Edge`, `OutOfTheWater`, `WildFood._edges` cache); people gather at the green (`AgentSystem.Green`/
  `Ashore`) where the sea took the square; view: one hull per boat with its crew. C9.1's lived days caught less from boats than COARSE (x1.23);
  now x0.90 (`BoatsGoOutAlikeAtBothResolutions`, 5 worlds x 24 days).
- **C9.2b (D2-112)**: `Tech.Boat` (8th technique; `Person.KBoat`/`Sc7`, `Settlement.BestBoat`/`BoatEvent`), found by woodworkers at a dugout
  (`Learning.Practice` from `Boats.Work`, `Learning.BoatWright` 0.3, tools); `Structures.PlankBoat` 21 and `BoatShed` 22 (`Function.Boatyard`),
  `Settlement.Shed`/`SeaP`; `Boats` by kind (`IsPlank`, `YearsOf/SeatsOf/MaxOf/HoursOf/WoodOf/NameOf/RowPace/PatienceOf`, `Risk(wind, kind)`,
  `WantsKind`, `Ready`, `ShedStands`, `OnTheStocks`, `ShedWanted`, `ShedSpot`, `Craft`); timber taken at laying-down (both kinds);
  `WildFood.ReachP`, `ExpectSea(ground, plank)`, `Fish(.., plank)`; metrics `PlanksBuilt`, `PlankFishKg`; kit models `dugout`, `boat_shed`
  (exported to `Game/Resources/TC2Models`; `boat` is the plank boat); view `_beachedP/_afloatP`, the shed turned mouth to the water.
- **Runs**: `tc2/out/civ/s*_c91j` (pre-fix), `_c91k` (C9.1 final), `_c91m` (4 seeds, final rule), `_c92a/_c92b` (C9.2a tries), `_c92c` (C9.2a
  final), `_c92p/_c92q` (C9.2b tries), `_c92r` (C9.2b final). Shots `boats2`-`boats6`, `planks1`, `planks2`.
- **Scratchpad (session 8)**: `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\79f297a9-c9c6-41f5-a86c-efdd067a4516\scratchpad`
  (`wrun.sh`, `cmp.py` - the `boats_made` regex fixed: before it read "given up half-made"; `boats_given_up` added; plank columns). Copies of
  both in `tc2/out/tools/` (fix `SCR=` in `wrun.sh` to the new scratchpad).
- **Headless additions**: `boats:` line - plank boats, villages that build them, sheds, plank boats made and their fish, boats in the water /
  away from it / mean height above the sea (by village), landings (range, mean distance), outings and crew seats; `squares under the sea:`;
  `run --load S --years 0 --boats SID` prints the landing, the next boat's spot, each boat's height above the sea.
- **Known**: a village's sea is often fished nearly empty by year 300; boats are begun where the sea promises 1.25x even a nearly empty shore;
  crews are of the household only; births and deaths a thousand person-years ~1-2 % lower with plank boats (people at par); COARSE never
  counts hunting trips (D2-063 comment bug); DEEP's fish x0.8 of COARSE (D2-083 lean).

- **C9.3 voyages (D2-113) IN PROGRESS**, committed as work in progress (sim version 52): `Places/SeaRoutes.cs`, `Trade.BestSeaPartner/Voyages/LoadBoats`, relief by sea in `Trade.Relief`, `Plan/Load/Exchange(.., loadKg)`, metrics `Voyages/SeaCargoKg/ReliefBySeaFu`, `VoyageTests` 3. Runs `tc2/out/civ/s*_c93a` (seed 4 trade only), `_c93b` (seeds 4, 8), `_c93c` (ten seeds). Finding: famines x1.18 (t 2.1) with relief by sea.

### Next, in order
1. (done: the Web build of 1808e81 tested 5/5, every view 73-120 fps, `tc2/out/webtest/c92web`.)
1b. **First**: D2-113 "Open" - why relief by sea brings more famines (seeds 2, 11, 8); fix (likely a cap on what a giver sends by sea), rerun ten seeds against `_c92r`, then lived voyages (crews rowing along `SeaRoutes`, drawn), shots, Web build, close C9.3.
2. C9.3 the landing and the dock (by catches landed and boats kept: a jetty/quay where boats are many), or crews beyond the household
   (neighbours, shares), or farther grounds for plank boats; then C9.4 cargo boats and coastal trade (the coastal proof, `25` §5).
3. Or C10.2 leaders, succession and the polity page.

## Session 7 (7 Oct 2026, 18:50-~23:00 IST; Control Room continuation) - handed off (context full)

### Exact state (keep current)
- **D2-108 done** (the C10.1 drops followed): ten seeds (42, 7, 4, 2, 1, 3, 5, 6, 8, 11) of C5.4 vs C10.1 show no systematic loss (century
  means at par); the lord's half-share was a second wrong - replaced by the lord answering its tributaries' wrongs at half, its band's loot
  settling the tributary's wrong (`Band.Motive` 2, `Band.For`; metric `LordsAnswered`; history ArgB 2 "they answer the wrongs it did X").
  Sim version 48; 244 tests. Runs `tc2/out/civ/s*_c108a` (and `s{1,3,5,6,8,11}_c54a/_c101a` added for the comparison).
- **Scratchpad (session 7)**: `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\dd8879ca-9abf-4584-bd64-5ba72f80ce6a\scratchpad`:
  `wrun.sh` (BIN = a folder in this or the session-6 scratchpad, or a path), `cmp.py TAG_A TAG_B seeds...` (paired log-ratios of annals
  and report lines: judge mechanisms by century means, never the last year), `bin_c108a`.
- **D2-109 done** (the Web frame rate): the frame was CPU-bound in draw submission (one draw per colour group, world-sized bounds, into all
  four cascades). `Instancer` now keeps colour/emission/walk per instance (`TC2/Instanced`, `TC2/Underwater` read them from the instancing
  buffer) and gives each batch of 1,023 its own bounds: 34-37 instanced draws a frame; close 90-102 fps, forest 100; Web test 6/6
  (`tc2/out/webtest/s7webb`, every view 73-120 fps). Tools: `FrameClock` (bridge `cpuMs`, `draws`), bridge `cull`, `shadow C R`, `webperf
  --sets/--reset`. The Web build in `tc2/web-build/web` is current (D2-109).
- **C9.1 boats from the woods (D2-110) - code committed c674892, NOT yet closed**: `Core/Places/Boats.cs` (want, Begin, Work, Of/Worn,
  AfterRow, SeaExpect, WindAt/Risk/Trip/Overturned, Leave, Landing + NearestDry), `WildFood.SurveySea/ExpectSea/Fish(.., boat)`,
  `Settlement.Sea`, `Household.Boat`, `Structures.Boat` 20, `ActivityKind.Sail` 33, `WhyCode.Boat` 42, history BoatBuilt 85 / BoatLost 86,
  death 12 (drowned), metrics BoatsBegun/Built/Worn/Lost/GivenUp/BoatTrips/Drowned/BoatFishKg, AgentSystem (choice 20, SeaBoat with the
  `SeaHere` cache, Sail completion, COARSE seats and deferred drownings), view (`WorldObjectsView.DrawBoat`, sailors in boats), HUD building
  page, Biography/Gazetteer lines, headless `boats:` line and `run --load S --years 0 --boats SID`. Sim version 49; 252 tests at the commit
  and again on the final code at handoff (252 pass). D2-110 is in DECISIONS with its evidence and an "Open".
- **Next, in order**: (1) close C9.1: rerun ten seeds (`BIN=<copy of tc2/headless/app/bin/Release/net8.0> TAG=_c91j ... wrun.sh` in two
  chains, then `python cmp.py c108a c91j 42 7 4 2 1 3 5 6 8 11`), check `--boats SID` shows no WET boats on a few villages, Web build
  (`node tc2/scripts/unity.mjs build-web --jobs 3`, ~6-11 min), `webtest.mjs --tag c91web`, shots `unity.mjs shots --tag boats2 --load
  <abs save> --village <id with boats> --views "work20@10,work20far@10,close@10,village@10"` and judge them; update D2-110's Open, commit.
  (2) Then C9.2 plank boats and boatbuilding, or C10.2 (leaders, succession, the polity page).
- **Known**: COARSE never counts hunting trips (`wild: trips hunt 0` - the D2-063 comment bug); DEEP's fish x0.8 of COARSE (D2-083 lean).

## Session 6 (7 Oct 2026, from 10:56 IST) — handed off at ~19:30 IST (context full)

### Exact state at handoff (read first)
- **Last commit 00ae0d6** (C10.1). Working tree clean except an untracked `unity/TinyConsequences2/Assets/Resources.meta` (an empty
  `Assets/Resources` folder from 6 Oct - not ours; leave it). No background jobs running. **Sim version 47; 243 headless tests pass**
  (full suite at 00ae0d6). Unity compiles (shots `place1`); Web build in `tc2/web-build/web` is current up to C12.2 (not C10.1 - Core-only).
- **Done this session after C1**: C4.1 roads by use (D2-097), C7.1 rings that grow (D2-098), C12.1 person page (D2-099), C5.1 copper (D2-100),
  C5.2a charcoal (D2-101), C5.2b iron + the COARSE smith's labour day (D2-102), C5.3a places by their ore (D2-103), C5.3b iron traded
  (D2-104), C5.4 whose ore (D2-105), C12.2 settlement page + smithy mesh fix (D2-106), C10.1 the lord's call (D2-107).
- **Open, in order**: (1) C10.1's population drops (seeds 7 -24 %, 2 -26 %: D2-107 "Open"); (2) Web frame rate 35-56 fps near the ground
  (target 60; earlier readings since C1.4 were false - see the bullet below); (3) C5.3c work at a distance (low value: the carry is ~5 % of a
  kilo of iron's labour - consider skipping to C8/C9/C10.2).
- **New headless tools**: `person --load S [--pid N|--pick oldest|smith|fighter|elder|child]`, `place --load S [--sid N|--pick big|iron|claims]`,
  `run --load S --years 0 --civ-of ID`, `run --load S --years 0 --events KIND` (any `HistoryKind` name, with its cause); report lines
  `ores:`, `iron:`, `charcoal:`, `claims:`, `ore sites:`, raids line with the call. Shots views `work17` mine, `work18` charcoal hearth,
  `work19` bog pits.

### Exact state (session 6, earlier - kept for reference)
- **Arjun's direction (7 Oct, D2-089 - the standing direction)**: an autonomous medieval civilisation inside a living miniature planet
  (`20_CIVILISATION_DEPTH.md`): Age-of-Empires system breadth and readability with no player orders; fewer, deeper people (tiers A
  100-500 dev, B 500-1,500, C 1,500-3,000); every structure from need, labour, material and knowledge (the ten questions, `20` §2);
  presentation develops with the simulation (D2-084 stands). Docs `20`-`25`; roadmap `25_CIVILISATION_ROADMAP.md` (C1-C13, W and P alongside).
- **C1 "the working town" closed (D2-090 to D2-096)**, all committed (last: the C1 close commit after 08c2a56):
  C1.1 structures by kind (`Core/Places/Structures.cs`: kinds table, works by need with causes, store capacity, placement by zone, building
  page); C1.2 timber from the woods (`Woods.cs`); C1.3 stone (`Stone.cs`); C1.4 the smithy and arms (`Forge.cs`); C1.5 training grounds and
  `Person.SkFight` (`Training.cs`); C1.6 markets and rings on dry ground; C1.7 tier-A pressure worlds read through `run --civ`. Sim version
  38; 207 tests. Structures keep their history while they exist (HistoryLedger local-tier keep).
- **Tools**: scratchpad (session 6) `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\2b3f3bc2-f71a-43e5-9ab3-082850348dfd\scratchpad`:
  `wrun.sh` (BIN=bin_x TAG=_x YEARS SNAP MODE EXTRA; DEEP 12 runs with the scale report, annals and saves into `tc2/out/civ/s<seed><TAG>`),
  `sheet.py` (contact sheets), patch and doc scripts. Headless `run` report lines: `works:`, `woods:`, `stone:`, `markets:`, `training:`,
  `force:`; `run --civ N` prints the N largest villages' civilisation timelines with causes (`#id` for shots). Shots: `--village big|ID`,
  views `work<kind>` / `work<kind>far` frame a village's structure (kinds: 10 well, 11 granary, 12 smithy, 13 quarry, 14 ground, 15 market,
  16 barn); `--load` needs absolute paths.
- **Pitfalls found this session**: this Bash tool collapses `\\` and breaks on apostrophes inside heredocs - write patch scripts, docs and
  commit messages to scratchpad files with the Write tool, then `python file.py` / `git commit -F file`. COARSE-vs-lived rate tests need pooled
  worlds (single worlds give 3-20 events). A quarry on rock the sea later takes was re-planned forever (fixed: forget drowned rock).
- **Runs**: `tc2/out/civ/s{42,7}_c1{1c,2a,3d,4c,5a,6a}` (300 years, saves at 100 and 300), `s{42,7,11}_tierA` (200 years, saves at 25, 100,
  200). Shots `tc2/out/shots/civ1`-`civ6`, `civA_y25`, `civA_y200`, sheet `civA_sheet.png`.
- **C4.1 roads by use done (D2-097)**: runs `tc2/out/civ/s{42,7}_c41a`, shots `ways1`, `ways2`; `Road.mat` needs `unity.mjs configure`
  before a Web build (the editor builds it from the shader meanwhile).
- **C7.1 rings that grow done (D2-098)**: runs `tc2/out/civ/s{42,7}_c71d` (saves at 100, 200, 250, 300), shots `rings1` (`--village 15`). The
  civ header shows walls in progress, old rings, wood and the project. Found: works stalled by float rounding (`Construction.Done`).
- **C12.1 the person page done (D2-099)**: `Core/History/Biography.cs` (shared by the HUD and headless), HUD "Life page" beside Follow (tabs,
  `Hud.ShowPerson`), WebBridge `person PID|oldest [TAB]`, `webtest.mjs` shots `9_person*`, headless `person --load SAVE [--pid N | --pick
  oldest|smith|fighter|elder|child] [--village ID]` (`tc2/headless/app/src/Person.cs`). The page's look is not yet seen: IMGUI is not drawn in
  batch shots - check it in the next Web build (`unity.mjs configure` first for River.mat/Road.mat).
- **C5.1 copper from the ground done (D2-100)**: `Core/Places/Mining.cs` (`Deposit`, `Deposits` store in `World`, `Mining` law), structure
  kind 17 mine, `ActivityKind.Mine` 31, `WhyCode.Ore` 39, history `OreFound` 81 / `OreGaveOut` 82, `RngStream.Geology`, settlement fields
  `OreCell/OreDist/OreEvent/Ore/OreWant/Mine`; `TestWorlds.WithCopper` premise; kit `mine_pit`; headless `ores:` line. Runs
  `tc2/out/civ/s{42,7,11}_c51d` (saves 100/200/300; `_c51a`-`_c51c` are the tuning tries), shots `mine1`, `mine2` (`--village 3 --views work17`).
- **C5.2a charcoal done (D2-101)**: `Core/Places/Charcoal.cs`, structure kind 18 `CharHearth` (at `WoodX/WoodY`), `ActivityKind.Burn` 32,
  `WhyCode.Charcoal` 40, settlement `Charcoal/CharcoalWant/CharHearth/ClampUntil`, kit `charcoal_clamp`, smoke while `ClampUntil > now`,
  headless `charcoal:` line; `Tech.MetalFuel` removed. Runs `tc2/out/civ/s{42,7}_c52a`, shots `char1` (`--village big --views work18`).
- **C5.2b iron done (D2-102)**: `Mining` handles two kinds (`Copper`, `Iron`; `CellOf/DistOf/OreIn/WantOf`), structure kind 19 `BogPits`,
  `WhyCode.BogOre` 41 (digging stays `ActivityKind.Mine`), `Tech.Iron` (seventh technique; `Person.KIron`, `Sc6`), settlement
  `BestIron/Iron/IronArms/FeCell/FeDist/FeEvent/FeOre/FeOreWant/FeMine`, `Tech.ToolPace/ToolWear/IronShare/CopperWanted`, `Forge.IronWanted/
  CopperWanted/IronCan/CopperCan/IronArms`, `Charcoal.PerKgIron/IronFrom/BurntIron`; the COARSE smith's labour day (AgentSystem, before the
  fields); kit `bog_pits`; headless `iron:` line. Runs `tc2/out/civ/s{42,7}_c52c` (`_c52b` was the narrow stream-only bog rule), shots `iron1`
  (`--village 6 --views work19`).
- **C5.3a places by their ore done (D2-103)**: `Mining.Lacks/OreInSight/SitePull/SiteBonus`, `Migration.Look` (the pull, `Scouted` ArgA),
  `Migration.FoundAt` calls `Mining.Look`; metrics `ScoutedForOre/FoundedByOre`; headless `ore sites:` line. Runs `tc2/out/civ/s{1..7,42}_c53a`
  against `_c52c` (seeds 1-6 added for the comparison; `s{7,42}_c53x` = the pull switched off: identical).
- **C5.3b iron traded done (D2-104)**: `Trade.Iron` (goods 6), `Settlement.IronSold`, `Forge.IronTarget/ForSale`, metric `IronTraded`;
  headless `iron:` line (traded, biggest seller), `run --civ-of ID`. Runs `tc2/out/civ/s{42,7,4}_c53b`.
- **C5.4 whose ore done (D2-105)**: `Mining.WorkedBy/MayDig/Claim/Pressing/ClaimDays`, settlement `ClaimDeed/ClaimOn/ClaimAt`, history
  `OreTaken` 83, metrics `OreClaimedKg/OreClaims`; `Mining.Dig(..., pid)`; headless `claims:` line, `run --events KIND`. Runs
  `tc2/out/civ/s{42,7,4,2}_c54a`. Story: seed 7 Thestford/Neldthorpe (years 281-288; `--events OreTaken`, `--events Taken`).
- **C12.2 the settlement page done (D2-106)**: `Core/History/Gazetteer.cs`, HUD `ShowPlace` ("Their village" on a person's card, "Its village"
  on a structure's page), WebBridge `place SID|big [TAB]`, headless `place --load SAVE [--sid N | --pick big|iron|claims]` (`Place.cs`).
  **Web build checked** (`tc2/out/webtest/c5web`, WebGPU): the person page renders; fixed the smithy mesh read (console errors in players).
  The build in `tc2/web-build/web` predates the settlement page and the smithy fix - rebuild before the next browser check (10 min with
  `--jobs 4`); River.mat/Road.mat are now committed.
- **Web build checked again** (`tc2/out/webtest/c122web`): settlement page renders, console clean (smithy fix). **Frame rate below target**:
  35-56 fps at village/close/forest views (WebGPU). The c5web run's 57-121 fps was false: since C1.4 the smithy mesh read threw while
  WorldObjectsView set up, so trees, woodpiles and later works were never drawn in Web builds. Measure layers with
  `node tc2/scripts/webperf.mjs --view close` and `--view forest` on a quiet machine (web build in `tc2/web-build/web` is current up to C12.2).
- **C10.1 the lord's call done (D2-107)**: `Conflict` `_callers/Members/CallAtMost/CallOwed/CallBold/LordsShare`, `Band.Called`, history
  `CallAnswered` 84, metrics `CalledBands/CalledMen/LordsWronged`. Runs `tc2/out/civ/s{42,7,4,2}_c101a` against `_c54a`.
- **Next**: (1) **follow the C10.1 population drops** (seed 7 -24 %, seed 2 -26 % with fewer hungry deaths: D2-107 "Open"); (2) the Web
  frame rate (webperf layer costs; target 60 fps near the ground); (3) C5.3c work at a distance: when ore or wood is far, smelt where it is (a bloomery at the bed or the woods: carry iron, not ore and
  charcoal), stints of days with a camp at the site, households moving to steady work there (a work hamlet founded by its work); then C5.4
  claims (a worked bed or body known as a village's, digging it a deed; news of beds by traders). To check: whether the bloomery holds in
  small villages over long runs (a complex technique); copper ore is still dug where iron serves only some. Seen before: people walking routes across cells the
  sea has since drowned; works hard to tell from homes at the village view; the training ground's posts too thin to read; fields drawn over
  crags; COARSE hearth smelting only when goods are short (D2-100); the person page's look in a Web build.

## Session 5 (7 Oct 2026, from 06:40 IST) — in progress

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-085 - the current priority)**: after D2-083 is verified and committed, STOP before the D2-084 polish
  and **reconsider the planet's physical size** (D2-007's R = 10 km is reopened): study TC1's actual scale, test smaller TC2 planets
  (N64 kept, R = 10 / 7 / 6 / 5 / 4 km) really simulated, measure population, interaction density, travel, performance, take comparable
  screenshots (orbit, region, village, close), decide the radius myself and change it now if the evidence supports it (old saves may be
  invalidated); then redo D2-084 on the chosen scale. Fewer people is fine (2,000-8,000 mature); no world where everyone reaches everyone.
  Study and decision: `docs/tc2/19_PLANET_SCALE.md`.
- **D2-083 closed and committed** (22cad8d): 177 tests (the small-world herd check now a factor-2.5 bound - DECISIONS D2-083
  "Closing checks"), COARSE 400-year hashes unchanged (42 55cfd96b471db300, 7 8c3e14c324165015).
- **D2-085 decided, made and committed: R = 6 km** (`19_PLANET_SCALE.md`, DECISIONS D2-085, 01 §2, 13 §11, roadmap W0). The ground is
  the same ground at any size: the planet's geophysics in geo-lengths of a 10 km sphere (`PlanetState.GeoRadius`, `GeoScale`,
  `GeoAreaHa`, `GeoCellSize`); living laws per hectare (`AreaScale`) and real-size patches (`Patch`, `PatchRings`); first villages in
  metres; new homes on dry lots (`Settlement.NextDryLot`); the view relative to R (`Ground.ScaleTo`, CameraRig framing, TerrainLod
  MaxLevel, Sky haze, CloudView shell). 177 tests at 6 km (eight 10 km premises restated); dotnet and Mono probes identical (gen
  392cf8631e08dc36). Old saves keep their radius. Tools: headless `--radius M`, `run --scale` (`ScaleReport.cs`, `Core/Diagnostics/
  ScaleTally.cs`), shots `--radius`, multi-world shot folders named by save folder. Study data: `tc2/out/scale/` (`table.txt`, runs,
  saves `r<R>_s<seed>/world_y100|300.tc2`); shots `tc2/out/shots/scale1`, `scale2` (contact sheets `sheet_*.png`). Scratchpad (session 5):
  `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\a14b045d-19c2-46a3-b3ef-213aae01eceb\scratchpad`
  (`scale_runs.sh`, `scale_table.mjs`, `sheet.py` contact sheets, `runs.sh`, `bin_b`).
- **Visual slice 1 of D2-084** (session 4's code: stands, palette, relief, rivers, tree kinds) compiled for the first time in the scale
  shots and is committed with D2-085; it is not yet judged on its own - judge it on 6 km.
- **Known bugs, each its own calibrated slice** (they change COARSE): COARSE hunting skill and trips never updated (`CoarseDay`, since
  D2-063); a COARSE disease catch that falls on a child is dropped (`Disease.Visits` picks among everyone present), halving the sickness
  visits carry home.
- **D2-084 slices on 6 km, committed**: the multi-age baseline (`tc2/out/shots/ages6`: history reads at orbit and region); D2-086 villages
  keep their shape (`Homes.FreeLot`, d775460); D2-087 homes the sea takes are lost (`Homes.SeaTook`, `HistoryKind.HomesDrowned`) and D2-088
  one shore for sim and view (`Core/Planet/Shore.cs`) with the people pixel floor and a `street` view (1625d2f). Acceptance worlds:
  `tc2/out/scale/r6000_s{42,7}_ages2/` (0/25/100/300/1,000, compact lots), `_ages3` (seed 42 with drowning), `_shore` (300 years, all
  rules). Shots: `ages6b` (seasons: summer `regiond18_10`, harvest `regiond14_10`/`village_10`), `drown1`, `shore1`.
- **Next**: (1) farmland beyond the crop stage (all a village's fields share one stage; fallow and pasture where state allows - check
  what Core knows before drawing anything: no theatre); (2) rivers and corridors (hairlines now; the network is real); (3) settlements as
  places (palisade ring vs the compact village; the square, yards and paths); (4) wildlife and herds readable; (5) scars; (6) orbit;
  the great beasts' viability decision; far views at their own local time (ShotRunner: an hour at the view's place); then the Web build
  at 60 fps (run `unity.mjs configure` first: River.mat).

## Session 4 (7 Oct 2026, from 00:16 IST) — closed into session 5

### Exact state (keep current)
- **Arjun's direction (7 Oct, D2-084)**: finish D2-083, then **the world made legible** is the priority (`18_WORLD_READABILITY.md`,
  roadmap phase W): audit -> composition -> forests -> rivers -> biomes -> terrain -> settlements -> life -> scars -> orbit ->
  multi-age acceptance -> browser performance. No simulation theatre. Ordinary low-priority runs need no permission (CPU/RAM ~50 %).
- **D2-083 (DEEP calibration) - working tree, uncommitted**: the step's mean ration (RationStep) removed (first day's ration);
  a pressing need's hours given each day of a step (`AgentSystem.CoarseDay` free); each worker of a step chooses on the ground as the
  step's earlier workers left it (`WildFood.Retake`, after each wild trip when days > 1); a worker's own take walked through the step
  (`WildFood.MidTake` in Hunt, Fish, Gather when days > 1). Rounds: h (no RationStep; seed 7 only), i (+ need-hours: wild hours
  2.17 vs 1.40 - overshoot, the woods overdrawn), j (+ Retake; six seeds, `scratchpad/verdict_j.txt`: per-person rates at par,
  people 0.85/0.87 at years 300/400 n.s. - seed 4 founded fewer villages, few epidemics -, hunting +34 % t 3.5, out for need +9 % t 7.1,
  fish -19 %), k (+ MidTake: seed 7 worse - more hunting, more out for need, famines 387 vs 312 - **reverted**; the working tree's
  Core is round j). D2-083 is written up as closed in DECISIONS/04/PROGRESS with round j's evidence and residual leans.
  **SESSION STOPPED 7 Oct ~01:04 at Arjun's request (usage limit). NOTHING COMMITTED since 6ad57fb.** (Session 5 did all of the
  list below: D2-083 committed as 22cad8d, the visual slice with D2-085.) Still to do, in order:
  (1) `node tc2/scripts/headless.mjs test` (all tests + the new `DeepTimeTests.EachWorkerOfAStepChoosesOnTheGroundTheStepLeft`; the
  repo's bin holds the reverted round-k build until it rebuilds); (2) COARSE hashes of seeds 42/7 must still be 55cfd96b471db300 /
  8c3e14c324165015 (one-day arithmetic untouched); (3) DEEP speed on a quiet machine (no --profile, nothing else running; round f:
  DEEP 42 3.17 y/s vs COARSE 1.80, DEEP 7 4.28 vs 2.97) - add to D2-083 and PROGRESS measurements; (4) commit D2-083 (stage Core
  AgentSystem/Settlement/SettlementSystem/WildFood, Tests DeepTimeTests, docs 04, 17, 18, DECISIONS, PROGRESS, SESSION_HANDOFF,
  NEXT_SESSION_PROMPT, headless Commands.cs `--snap`); (5) then the visual slice below (compile in Unity via shots, fix, judge, commit
  with its .meta files). A leftover round-k run of seed 3 (`deep3_400k`, tc2bin21) may have finished after the stop - ignore it.
  Scratch files: `scratchpad/verdict.mjs` (now with mechanism tallies), `pairsj.json`, `verdict_j.txt`, binaries tc2bin19-21.
- **Visual slice 1 (D2-084), code written, NOT yet compiled in Unity or shot**: woods as stands and glades (`Canopy.Stands`,
  StandNoise/StandRank/StandCdf, the shader's StandRank; trees and cover follow), open-land palette from climate and soil water
  (`TerrainLod.CellColour`), `Ground.Relief` (valley/ridge index: wet valley floors, conifers to ridges), distant relief lit steeper
  (Terrain.shader normal), haze ~40 % thinner, tilt-shift a hint (Sky, TiltShift.shader), **rivers** (`Rivers.cs` shared shape,
  `River.shader` ribbons widened to >= ~2.6 px far off for larger rivers, wet banks and riparian strip in Colourise, Land.y = river
  nearness, woods keep to banks, reeds at the edge only), **trees** (`ProcMeshes.ForestTree` 7 kinds x 3 variants: round, tall,
  wide, birch, willow by water, spruce, pine on dry/coastal land; crowns of shaded lumps), shot tools (`ShotRunner -tc2load F1;F2`,
  `SimHost.LoadWorld`, views `river`, `town`, `scar`; headless `run --snap Y1,Y2` -> `tc2/out/ages/sS/world_yY.tc2`), River.mat in
  ProjectSetup (run `unity.mjs configure` before a Web build). Next: `unity.mjs shots --tag leg1`, fix compile errors, judge.

## Session 3 (6 Oct 2026, from 17:57 IST) — closed into session 4

### Exact state
- Commits: 1156d3d D2-081 (the woods as masses, cover, villages' signs) - then D2-082 (Web: cover gathering cached, catch-up at orbit,
  capped builds; `s3webb` 6/6) and F8 slice 1 (DEEP) committed together after it (see git log).
- **F8 in progress - DEEP calibration (UNCOMMITTED fixes in the working tree, all one-day paths unchanged - 175 tests passed after
  the first four)**: the first 400-year comparisons (`tc2/out/deep/coarse{42,7}_400.txt` vs `deep*_400.txt`, `deep*_400b.txt`) showed
  DEEP biased: population -15..-37 % at year 200, cattle x3 and pigs x0.2, relief x0.5, wild hours +10 %, scouts x0.75, raids/claims up.
  Fixed (each a "step must keep its days" rule, now in 04 §4.3b): claims one a day (Conflict.Claims), relief window, scouts every six
  days (`Migration.ScoutsDue`), beasts' condition/hazard/milk/conception walked day by day (`Livestock.DeepBeast`), grazing not capped
  at one day's offer, mast per cell capped, catch caps 1-(1-s)^K (`WildFood.Most`), the least wild trip a quarter-hour a day, the
  store seen as days lived hold it (`Settlement.Ahead/AheadFrom/AheadTo`, `StoreSeen`; sim version 32; hashed only when set), a
  mother may conceive from the day her nursing ends / her 16th birthday, a girl marry from her 18th (`DaysUntil`), crops grow on each
  day's place on the season curve (`CropDay(.., k)`), hearths and pasture take the step's middle day. Headless `run --annals F.csv`
  writes the annals (yearly people, births, deaths by cause...). Scripts: `scratchpad/calib.sh` (env SEEDS, MODES, TAG, BIN),
  `calsum.mjs DIR c d seeds` (paired totals), `annals.mjs DIR coarse deep _400TAG seeds` (rates per 1000 person-years by century).
  Round 2 (`*_400d`, COARSE hashes of 42 and 7 identical to before: 55cfd96b471db300, 8c3e14c324165015) still showed DEEP biased
  (hunger/famines up, wild hours +33-50 %, conflict up on 42); round 3 fixes (all in the working tree): culling to the pasture as
  the step leaves it, death hazard day by day and conception on `Person.StepHealth`, the store-empty check and the wild urge on the
  store seen (a provisional `Ahead` right after Food), the step's mean ration (`Settlement.RationStep`), the smith's cap, a calm
  step never spans a plant pulse (`World.NextPlantTick`, derived), the woods at the step's middle (`Livestock.Rooting`,
  `WildFood.MidStep`), per-person derived constants (`Person.Derive`: build, copying scatter - exact). Seed 33 (60 y, small):
  DEEP wild work now matches COARSE (0.141 vs 0.147 h a working person-day; was 0.115). Draft decision: `scratchpad/d2083_draft.md`.
  Tools-only tallies: `SettlementSystem.Unrests` (why not calm), `AgentSystem.Hours` (COARSE hours by use, the wild choice by kind
  and by day of the plant cycle), printed by headless `run --places`. `scratchpad/calib2.sh` runs "seed:mode" items (BIN, TAG).
  Later in round 3: a step's later days grow the crop after its weeding (seed 1 crop 0.853 -> 0.871 against COARSE 0.884), harvest and
  leaving tallies (`SettlementSystem.Harvests`, `Migration.Weighs`). D2-083 written (in progress); 176 tests pass (DeepTime 7 after the
  last rule). Round 3a results (`*_400e`, tc2bin9): seeds 42/7/1 summarised in D2-083's evidence.
  **Blocked** (PROGRESS "Blockers"): the classifier refused to stop two leftover workers of this session and to start the final batch;
  they have since finished (round 3a on six seeds, D2-083's evidence: no early-growth bias, per-person rates at par, but -15 % people at
  year 400 on all six with less food: pigs x0.07, fish, herd meat, relief). After it: pigs root day by day (`Livestock.Day`,
  `MidStepMast`), a demography tally (`SettlementSystem.Demo`) and the wild decision's shortage terms - seed 1 DEEP's rations ran short
  16x as often from 9 % more people (an early lead crossing a threshold, not a rate). 176 tests pass. **Next**: with permission, DEEP 42/7/1-4 with the newest scratch build (TAG f), compare
  against COARSE (42/7 `*_400d`, 1/2/3/4 `*_400e`) with `annals.mjs`/`calsum.mjs`; open question: DEEP's early growth runs ahead on
  small worlds (seed 1 +9 % people at year 100, seed 33 +17 % at year 60) with three times the households leaving - look at
  marriages and births per person-year by age next; then speed on a quiet machine (never with parallel runs: everything ran 2x
  slower). Known COARSE bug left for its own slice: hunting skill and trips are never updated in `CoarseDay` (the line sits inside a
  comment since D2-063).
- Tools added: `tc2/scripts/webcatchup.mjs` (a day away's catch-up time by camera height and layers), the bridge's `catchupalt`,
  `covershadow`; `scratchpad/perfviews.sh` (webperf view by view), `scratchpad/calib.sh`.
- The slice (for reference) - **D2-081 (the woods as masses, ground cover by biome)**: `Canopy.cs` (plain-chance
  crown numbers - the tile repeats, so nothing in it may pattern -, radius 0.52-0.94 with few broad old crowns, height 0.64-1, K
  fitted on the layout (`Fit`), the cover texture `MakeCover` - whether a standing crown covers each texel at four thresholds, its
  mips the share covered -, `CoverMean`); `Terrain.shader` (`CoverAt`/`CoverOf`: the far woods are the area-average of the near canopy -
  clumps lit by a bump from their cover, the floor shaded by clumps sunward -, the crowns hand over at 1.3-3 m a pixel, the tile
  hands over to its mean at 3.5-7 m a pixel, leaves olive by the climate's dryness `i.land.x`); `TerrainLod.cs` (a fourth vertex
  stream `Land` = dryness, meadow, bare earth or rock, beach; clumps of ~20-45 m in the forest share where woods are open; tree
  colour groups with two dryness bits (ushort groups, 512 tints); ground cover by what the land is: meadow grass, flowers in their
  season, shrubs and ferns at wood edges and under the woods, heather on cool wet open land, cushions and stones on tundra, straw
  and stones where dry, dune grass on sand above the tide, reeds, boulders; cover re-placed with the seasons); `ProcMeshes.cs`
  (Shrub, Fern, Cushion, Stone; low icosahedron blobs); `WildlifeView.cs` (herds up to 3 km altitude, up to 4x at distance);
  `Sky.cs` (cover texture, K, mean as globals).
  Later in the slice: the far cover read a second time turned and scaled (`fb`) and stands (`Tri(.., 1/1700)`); tree kinds
  (`ProcMeshes.ForestBroadKind`: tall, spreading, birch) and young trees after fire; beaches and the sea floor per pixel with a metre
  of noise in the height read (the coast staircase); `WorldObjectsView` hut and camp smoke, woodpiles (`ProcMeshes.Woodpile`), the
  watch fire, night lights (`EmitLights`); `ShotRunner` `view+D@H`; `BuildScript` `-tc2Jobs`; the bridge's `off cover`.
- Shot rounds: `tc2/out/shots/woods1` (tile clumps read as a grid from orbit) -> `woods2` (grid gone, far woods too smooth; night
  lights) -> `woods3` (far texture and stands; seasons: a tropical forest stays green - no autumn above ~20 C mean, right) ->
  `woods4` (beach per pixel, edge shrubs to 480 m, flowers readable) -> `woods5` (sea floor per pixel) -> `woods6` (waterline noise).
- Look items seen and left for later: the surf's foam repeats as V shapes near the camera; the water sphere's own edge against
  coarse terrain can still step far off; shrubs at a wood's edge are one size; flowers only from ~100 m; animals at the village view
  are still specks (herds read as groups only when white); kitchen gardens (not simulated: not drawn).
- Machine: shared and loaded by others (see PROGRESS session 3); wait with `scratchpad/quiet.ps1`, run with `scratchpad/lowrun.cmd`
  (scratchpad = `C:\Users\aswin\AppData\Local\Temp\claude\C--Game-Dev-tiny-consequences\1e4f3b75-2f3f-4d81-b3ff-5dffc75a3c54\scratchpad`).
- No TC2 process running between shot rounds.

## Session 2 (5 Oct 2026) — closed (see PROGRESS.md CURRENT STATE)

### Exact state
- Session 2 commits since the last checkpoint (newest last): 314fdd9 techniques (D2-062) · c8e9c23, 07b38be materials (D2-063) ·
  6bacfaa copper, F4 closed (D2-064/065) · 27d2a53 sickness (D2-066) · e01a933 trade and relief, F5 closed (D2-067/068) ·
  b78117d deeds and taking (D2-069, F6 slice 1) · 310ab38 sickness at scale (D2-070) · ceadd36 the watch, blows and injury
  (D2-071, F6 slice 2) · 0f2adde answers, news and taking back (D2-072, F6 slice 3) · 7121f7e raids (D2-073, F6 slice 4) · 41f5756 the palisade (D2-074, F6 slice 5) · ff6fab8 tribute and lordship (D2-075), F6 closed
  (D2-076) · 3d8f7b6 Web after F6 · the commit with the History Book (D2-077), F7 closed (D2-078) and the direction to make the
  living world visible (D2-079).
- Phases: F0 done; **F1 to F7 closed** (D2-043, D2-046, D2-060, D2-065, D2-068, D2-076, D2-078). 169
  headless tests green; `headless.mjs test` runs the float lint first. Sim version 30.
- Determinism: dotnet, the editor's Mono and the Web build (IL2CPP, WebGPU) are bit-identical at D2-077 (gen 9b6482ca9f9f606f,
  `tc2/out/webtest/s2webg`: 6/6).
- Sickness at scale is fixed (D2-070): seed 42 holds 12,000-16,000 people from year 270 to 400 and seed 7 4,600-5,400
  (`tc2/out/society/sick42_400.txt`, `sick7_400.txt`; the run report lists the deadliest sicknesses). Saturated worlds are
  famine-regulated (~100 hunger deaths a year) and take from each other's stores often (~30-75 loads a year): F6's pressure.
- Nothing running. Uncommitted legacy TC1 work may exist in the tree and is NOT ours: `game/html/engine_template.html` and
  `docs/DECISIONS.md` (D-227). Never stage them in [tc2] commits.

### How to run things
| Task | Command |
|---|---|
| Core tests (fast) | `node tc2/scripts/headless.mjs test [--filter X]` (~110 s for all 101) |
| A world run with history | `node tc2/scripts/headless.mjs run --seed 42 --years 30 --report-days 480 --tier 3 [--out DIR] [--why] [--profile] [--coarse]` |
| Villages over time | `run --seed 42 --years 200 --report-days 2400 --tier 9 --coarse --places --profile` (per village: people, fields, granary, ration, wild grounds, **stock and pasture** offered/wanted, herds; then the stock line, **deaths by kind and cause**, the profile with `daily.*` parts) |
| Planet over time | `planet --seed 42 --years 3000 --every 9600 --nopeople --coarse [--tect] [--volc] [--eco] [--csv F] [--ecocsv F] [--map DIR]` (each report line ends with elapsed seconds and the number of kinds) |
| 10,000-year runs | copy `tc2/headless/app/bin/Release/net8.0` to the scratchpad, then `dotnet <copy>/tc2.dll planet --seed 42 --years 10000 --every 4800 --coarse --nopeople --tect --volc --eco --csv tc2/out/f1/run_42X.csv --ecocsv tc2/out/f1/eco_42X.csv > tc2/out/f1/run_42X.txt` (~22-25 min each) |
| Climate alone (fast) | `node tc2/scripts/headless.mjs climate --seed 42 --years 4000 --every 200 [--co2 560]` |
| People diagnostics | `node tc2/scripts/headless.mjs probe --seed 42 --days 4 --rows 30` |
| Benchmarks | `node tc2/scripts/headless.mjs bench [--name queue|dmath|poc|save] [--coarse]` |
| Cross-runtime probe | `node tc2/scripts/headless.mjs hashtest`; `node tc2/scripts/unity.mjs hashtest` |
| Unity configure / tests | `node tc2/scripts/unity.mjs configure` / `node tc2/scripts/unity.mjs test` |
| **Look-dev screenshots** | `node tc2/scripts/unity.mjs shots --tag T [--views orbit,region,village,close,low,volcano,...,coast,seals] [--hour 8] [--days D] [--seed N] [--erupt VEI] [--quake M] [--storm WIND]` -> `tc2/out/shots/T/*.png` |
| Asset kit | `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models [--preview tc2/out/kit]` |
| Web build / serve / test | `node tc2/scripts/unity.mjs build-web [--webgl2]` (5-9 min) · `node tc2/scripts/serve.mjs` · `node tc2/scripts/webtest.mjs --tag T [--webgl2]` |
| URL params | `?fresh=1`, `?seed=N`, `?speed=I`, `?absence=24h`, `?view=close|village|region`, `?gfx=webgl2` |

### Important paths
- Unity editor 6000.6.4f1: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`; bundled dotnet in `.../Editor/Data/DotNetSdk/dotnet.exe`.
- Core: `unity/TinyConsequences2/Assets/TC2/Core/{Foundation,Scheduling,Simulation,Planet,Places,Agents,History,Persistence,Diagnostics,Worldgen}`;
  places: `Places/{Settlement,SettlementSystem,WildFood,Construction,Sites,Migration,Herds,Livestock}.cs`.
- Game view: `Game/View/{WorldObjectsView,WildlifeView,SeaLifeView,...}.cs`; kit models `Game/Resources/TC2Models/*.fbx` (from `tc2/art/kit.py`).
- Tests: `Tests/Core/*Tests.cs` (WildFood 11, Migration 5, Livestock 11, ...); `TestWorlds.Audit` checks people, queues and livestock lists.
- CC0 Quaternius farm animals (Blend/FBX) in `assets/3d-models/animals_quaternius/` (not used yet: the kit's own quadrupeds keep one style).

### Pitfalls found (session 2 additions; session 1's list still applies - see git history of this file)
- Grep build output for `error MSB` too: a locked DLL fails the copy with no `error CS`.
- A long headless run on the repo DLL locks it: run long jobs from a scratch copy.
- `planet --nopeople` used to found one village (`Math.Max(1, cfg.Settlements)`): runs before 6b0903d labelled planet-only may have people.
- Lookups that walk a whole store per agent-day (all herds, all settlements) are invisible at 6 villages and dominate at 30:
  index per village; profile a 200-300-year coarse run (`--profile`, `daily.*` parts) after adding any per-day lookup.
- `CellsWithin` keeps its answers per (cell, range) (the grid never changes); results are the same list in the same order.
- The village's day (`SettlementSystem.Daily`) runs at local midnight: `P.Temp` then is the night's (diurnal swing 5 C on land).
  Use `PlanetSystems.DayMean(c, PlanetSystems.Season(t))` for a day's mean.
- A test's premise can be undone by the world before the mechanism runs (hunters brought food home before the herd's day):
  call the system step directly when the test is about that step.
- A planet step (tectonic, evolution) can fall inside any test's hour: filter history checks by the kinds under test.
- Heredocs with apostrophes break: patch scripts go in `scratchpad/patch_*.py` written with the Write tool.
- `headless.mjs test --filter A|B` matches nothing (no alternation): run the filters one by one.
- Kit re-exports touch every FBX (new timestamps): restore the unchanged ones with
  `git diff --name-only -- .../TC2Models/ | xargs -r git checkout --` before committing; a glob also catches new untracked files.
- Any rule the simulation and the view must share (a herd's ground, the frost that pens the animals, which seals show) lives
  in Core or one static helper both call - never two copies.
- Test mechanisms, not demography: "mostly children herd" failed because only 1 of 19 households had a child of 8-15.
- **No float arithmetic in TC2.Core** (D2-057): `a + b` on two floats rounds differently under Mono; write `(double)a + b` and cast
  to float only when storing. `headless.mjs test` runs `tc2/tools/floatcheck` first and fails on any.
- A store whose ids come from a shared counter (process ids: herds, parties, fires, storms) is sparse: check `PagesHeld` and the
  cost of a pass over it at scale (D2-055).
- A new daily need (meals, water, fuel) changes what a "stocked" premise means in old tests: restate the premise (keep the store
  stocked), do not loosen the mechanism (D2-056).

- **Another Claude session may share this machine** (6 Oct: one on another project, with a gstack browse daemon whose headless
  Chromium renders WebGL with SwiftShader on ~7 cores). Its processes are not ours: never kill a process you did not start -
  check its parent first (`Get-CimInstance Win32_Process`) - and measure timings only when the machine is quiet.
- Claude Code stops background jobs when the machine runs critically low on memory (6 Oct: other sessions and their browsers
  shared it; the Web build and the local server were stopped). Check free memory before the Web build (it wants ~4-5 GB).
- The History Book in the game: B or the Book toggle; `?book=1` opens it; a fresh world has little history - reload a saved one
  with `?absence=24h` to live ~96 years first.

### Next steps
1. **F8 - DEEP calibration (D2-083, in progress)**: Arjun allowed the six-seed batch; round f (`*_400f`, tc2bin17 = commit 6ad57fb)
   done 23:51 - `scratchpad/verdict.mjs scratchpad/pairsf.json` (paired log-ratios, t over six seeds): population at every
   century, births and deaths per person-year, hunger and famines, wild food, herds, relief, households leaving and conflict at par;
   leans not significant (wild hours per person-year +17 %, t 2.0; smelting; infections -25 %); one metric flagged at 5 % (foundings
   per person-year, totals at par - ~2 false flags expected among ~50). COARSE 7 rerun with the tallies (`coarse7_400g`, hash
   unchanged) showed the one real remaining bias: the step's mean ration (`Settlement.RationStep`) walked the store down by the
   need alone - whole steps marked short (out for need on 43.5 % of person-days against 32.8 %; urge 0.072 against 0.036). Removed
   in the WORKING TREE (uncommitted; built as `scratchpad/tc2bin18`; not yet tested): the step's work reads its first day's ration.
   **Next**: retest DEEP 7 against `coarse7_400g` (`QUIET=1 BIN=tc2bin18 TAG=h calib2.sh 7:deep`), then the six seeds (TAG h),
   full tests, D2-083's rule 2 text (no mean ration), commit; then DEEP's speed on a quiet machine. The 23:56 memory-pressure stop
   (Claude Code) killed the local game server, the COARSE 7/42 tally chain (COARSE 42 kept running to finish `coarse42_400g`) and the
   DEEP 7 retest - restart only when Arjun asks.
2. **The COARSE hunting-skill bug - its own calibrated change after D2-083** (Arjun, 6 Oct: not part of D2-083 - changing COARSE
   moves DEEP's reference): in `AgentSystem.CoarseDay` the hunter's skill growth and `HuntTrips++` sit inside a comment since D2-063
   (`s.Hides += hides;   // (D2-063) p.SkHunt = ...; w.Metrics.HuntTrips++;`). Restore them, recalibrate COARSE against ACTIVITY
   (`CoarseAndActivityLiveOffTheWildAlike` and the wild-hours fits), record new COARSE baselines (hashes change), rerun DEEP vs COARSE.
3. **F8 - speed**: DEEP's per-step costs (coarse-day, learning, demography, conflict), the planet's share (weather, vegetation +
   ecology every 12 days are needed for seasons - look for exact speedups rather than coarser cadences), housekeeping; Ancient and
   Deep time measured in the browser (`webcatchup.mjs`); F8's done-when: 5,000 years in minutes.
4. **Web performance**: a day away catches up in 48-49 s (D2-082; 41-42 s before F5-F6) - profile the browser's catch-up.
5. **The look backlog (D2-081)**: the surf's foam repeating as V shapes near the camera, the water sphere's edge stepping far off,
   one size of shrub at wood edges, flowers only from ~100 m, animals specks at the village view, kitchen gardens (not simulated).
6. F7's carried-forward items (D2-078): Your Actions (with F9), timelines, news by incident, biography and polity pages, ages named
   apart. F6's (D2-076): lordships that hold, leaders, alliances, captives, gates and stone walls, bands of several villages,
   beasts as loot, the violence rate, the view of lordships and bands. F5's (D2-068): boats between coasts, roads worn by use,
   fairs, salt, credit, relief by kinship. The sea's calibration (D2-053).
