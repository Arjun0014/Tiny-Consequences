# Session handoff — sessions 15-16 (2 Oct 2026): the browser cost found, animals as individuals, R4 nearly done, R5 begun

`docs/PROGRESS.md` (sessions 15-16 entry) and **D-114 … D-122** in `docs/DECISIONS.md` are the source
of truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop); visible action = simulation; fix mechanisms, not appearances; never trade agency for the
season budget (100 believable animals beat 500 synchronised puppets); no forced trade; measure the
release browser by the lower envelope of several runs with rendering off; hand off near ~900K context.

## What happened (two sessions)
- **Session 15 died** (a dropped connection) before its handoff. Its transcript is `last_session.md`
  (untracked, Arjun's file). It had committed `617587b` (the browser-only first-hours cost was
  headless Chromium's CPU rasteriser - SwiftShader - competing with the sim; probes now draw nothing
  while measuring: bridge `render_off`, `prof_web.mjs`'s default, `--render` to keep drawing) and
  `4766bdc` (animals as individuals, first pass: livestock and wild bands within their own range,
  own pace/alertness/side, grazing headings, rouse delays, alarm waves; tools `v3_animals`,
  `v3_tracks`, `shots_herd`) - but that commit broke `test_v3_year` (a child starved) - and left R4
  droving (D-115) and an R5 draft (`sim/agents/migrate.lua`) uncommitted.
- **Session 16** recovered all of it (nothing reset), wrote D-114/D-115 into history, and:
  - **Animals (D-116, D-121)**: a richer audit (velocity correlation between members, mass
    starts/stops/rests, offset change, centre spread, young-mother distance; `tools/v3_pack.lua`
    frame by frame); measured pre-S15 / S15 / now on the same world; replaced what was still a
    formation: wolves follow the leader's **trail** at drifting places (sniff stops fall back,
    catch-ups pass, scent detours), flank from where they are, lie down while the leader waits, wait
    their turn at a kill, arrive one after another; dogs hold their own **posts** at the herd's edge,
    work the **balance point** when the herd is moved, go round the herd (never through), see off
    wild animals (which flee running dogs), keep after a straggler until it is back; the **drove as
    individuals** (no rope line), the drover waiting at the gate for it; **natural spacing**
    (`stock_range`, `stock_room`, `stock_bend`; a moving flock tightens); grazing whys that say
    something; **every animal clickable** in the game with its why.
  - **D-117**: a remarrying widow had moved into her new house alone, her children (10 and 2)
    starved beside the grain - parents now bring their children; children with no grown-up are
    taken in by kin or a neighbour. (This was S15's year-test failure.)
  - **R4**: relief by sea (D-118); kin visits across villages carrying the news (D-122); courting at a neighbouring village (D-120) - a man with nobody to
    marry at home walks to a trusted neighbour, marries there, the bride walks back with him (the
    first kin across villages; a chain follows: the village he took her from now has an unwed man
    who goes courting in turn).
  - **R5 begun (D-119)**: households move to another village physically (push lasting days, pull,
    packing, beasts led, gathering at the gate, the road together, babes carried, lodging, a house
    built for them, empty houses reused).

## State
- **main** at the final commit (see PROGRESS). Shipped world unchanged (`assets/worlds/v3.tcsave`,
  seed 22, 6 settlements, 143 people at founding). `?world=v2` legacy.
- The before/after animal measurements ran in temporary worktrees (removed): to repeat them,
  `git worktree add --detach ../tc-before 617587b` (pre-S15) or `... ../tc-s15 4766bdc`, copy
  `tests/sim/support/animals_audit.lua` and `tools/v3_animals.lua` in, run
  `luajit tools/v3_animals.lua --hours 48` there and here.

## Tests and measurements (final)
- Quick sim **142/142** (v2 109 + v3 33), web **11/11** (W10 determinism), lint clean, `npm run build`
  OK; `node scripts/v3_hash_web.mjs --release`: browser = LuaJIT at 0, 24, 48, 72 h.
- Release browser (`node scripts/prof_web.mjs --release --hours 288 --runs 3`, rendering off):
  runs 37.9-39.2 ms/h, envelope **37.6 ms/h = a 12-day season 10.8 s** (target 10 s; kept over for
  the agents). Before the animal work 30.2-30.9 ms/h (8.3 s); S15 end 36.9 (10.6 s).
- LuaJIT (`luajit -joff tools/v3_profile.lua --days 4`): **4.06 M instructions a game hour**
  (pre-S15 2.88 M, S15 3.76 M): nav (A*) 29%, map 13%, stock 12%, wild 12%, think 11%, event
  queue 9%.
- Animal synchronisation (shipped world, first dawn + 48 h, pre-S15 / S15 / final): wolves blob
  92% / 63% / 28-46%, vcorr 0.81 / 0.68 / 0.58-0.63, mass stops 7% / 29% / 0-4%; dogs moving 91% /
  33% / 19%; herded flocks align 0.69 / 0.35 / 0.19; young within 0.5 spacings of their mothers
  (was 0.9); left-out herds no longer frozen; Tekheim's flock spread 0.17 -> 0.29 units (screens
  `out/shots/look/s16_flock_later.png` vs `s16_flock_spaced.png`, tracks `out/tracks/`).
- R4: livestock sold alive and droved as individuals, relief by sea, courting and brides, kin
  visits carrying news - done (tested by created want). Open: sea droves, beasts as payment, larger
  cargo boats. R5: households moving to existing villages - done; founding - next.

## Next (in order)
1. **R5 founding** - the plan is in `docs/v3/30_ROADMAP_V3.md` R5 ("Next - founding a new place"):
   site knowledge from travel, `settle.new` for the empty map, the party's journey, camp -> shelter
   -> well/water -> field -> granary, every system tolerant of a village without its buildings.
2. **Kin across villages** now exist (brides): visits (news carried both ways - the shipped world
   has no travel at all in 12 days, so villages never hear of each other's wants), pull of kin in
   migration (already counted), help asked of kin.
3. Moving by sea (a household with a boat), sea droves, larger cargo boats only when volume
   justifies them (no trade volume yet).
4. Performance: the season is over 10 s in the release browser (see above); the sim is ~4.0 M
   LuaJIT instructions a game hour (pre-S15 2.88 M): people's A* is 22% (91 searches an hour, cached
   per map version), the animals' decisions most of the rise. Cut exactly (no agency lost): fewer
   herd scans per dog decision, a spatial index for herd scans, path reuse for repeated walks.
5. Birds beyond gulls (rooks/crows at fields and carcasses, herons, raptors, songbirds), lakes,
   local-map valleys, passes - below R5.

## Findings and pitfalls (sessions 15-16)
- **Bob forbids circular requires even inside functions**: LuaJIT tests pass, `npm run build`
  fails ("Circular dependency detected"). Inject modules through `sim/agents/init.lua` (as
  `settle.econ`, `settle.migrate`) and build after adding a `require` between agent modules.
- Measure the browser with rendering off (`prof_web.mjs` default); compare code changes by LuaJIT
  instruction counts (`luajit -joff tools/v3_profile.lua --days 4`), not wall time.
- `migrate.daily` / `settle.daily` get `day * 24` (UTC midnight), not the hour it is: use
  `W.S.v3.now or W.hour` for anything scheduled from it.
- Memories are a ring of 8 (`people.remember`): a busy hunter's courting memory is gone in a day;
  trace with scripts, not memories.
- An interrupt during a road leg restarts the leg (`WORK.travel` needs f >= 0.99): do not interrupt
  travellers (a hungry baby is now fed on the road).
- The audit's numbers move run to run when the world changes (wolf visits vary); the regression
  test thresholds have margin.

## Known oddities
- The shipped world is economically quiet: no trade, no relief, no migration in 12 days (every
  village has grain to spare, nobody travels, news never refreshes). Courting is the first thing
  that sends people between villages on their own (Tib of Hottoodingle Hop goes when the harvest
  work lets him).
- A village everyone leaves (the migration test empties Zurug by wiping its food daily) stays as
  standing buildings with no people.
- Night scenes are very dark (wolves at the pens are hard to see).
