# Session handoff — session 10 end (1 Oct 2026): v3 is the default game

`docs/PROGRESS.md` (session 10 entry) and **D-079 … D-084** in `docs/DECISIONS.md` are the source
of truth. Arjun's rules: no subagents; the living world first; one heavy process at a time
(shared laptop, ~4 GB free); hand off before the context runs out.

## State
- **main** holds everything (the v3 branch was merged at `d8cb549`; work continues on main).
  A normal launch opens the v3 agent world (`assets/worlds/v3.tcsave`: seed 22, **4 settlements,
  103 people**, Tekheim and Deztok coastal with docks) at its first dawn, at ▶▶. `?world=v2` opens
  the legacy v2 world; `?seed=N` generates a v3 world (`&world=v2` a v2 one).
- Time: ⏸ / ▶ 1 / ▶▶ 4 / ▶▶▶ 12 game-min per second; nothing faster is a watch speed. Skips (HUD
  ⏭ menu or keys 4-7): until dawn, until evening, one day, next season — people hidden, the view's
  rebuilds paused, progress and Stop; the world lands exactly where the sim put it.
- Sim (`sim/agents/`): + `kin.lua` (infants, carers, minders, mentors). The economy (D-080),
  life stages (D-081), several settlements (D-083) and fishing (D-084) are described in DECISIONS.
- Regenerate the world after any change to founding or world generation:
  `luajit tools/make_world.lua --v3 --seed 22 --settlements 6 --out assets/worlds/v3.tcsave`.

## How to look at it
- Game: `npm run build && npm run serve`, open `http://localhost:8080/` (v3). Click a villager for
  the inspector; "Follow" keeps the camera on them.
- A whole year headless: `luajit tools/v3_year.lua --days 54 --out out/year` (summary.txt: daily
  food/wood/fields with a conservation residual, budgets by stage and season, trips, crowding,
  stuck, synchrony; follow.txt: followed people's days; `--load assets/worlds/v3.tcsave` for the
  multi-settlement world; `--ids 12,21 --follow-days 14`).
- Decision traces: `think.trace = { [id] = true }` then `think.trace_out` (every candidate and its
  score; see `out/s10/trace2.lua` pattern).
- Shots: `npm run shots:v3 [-- --hours 6.2,20.6 --person 12 --zoom 1.45]`.
- Catch-up cost in the browser: `node scripts/skip_probe.mjs [--release] --hours 24 --skips day,season`.
- Blender: not on PATH — `"/c/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b
  --factory-startup -P tools/blender/build_villager.py -- --out assets/characters`.

## Verified at the last commit
- Quick sim suite **119/119** (`out/s10/quick_final.txt`; v2 109 + v3 10: slicing, save round trip, four
  days, families, no teleports, and `test_v3_year`: economy closes, nobody starves, infants held
  and fed, children never field hands, apprentices beside mentors, the rotation turns).
- Web **10/10** (`out/s10/web_final.txt`): W01-W09 (legacy v2, `world=v2`) and W10 (v3 default:
  ▶▶, watch ladder, skip to dawn; `?world=v2` still loads). Lint clean. `npm run build` OK (6.08 MB).

## Next (in order)
1. **Coast function** shared by the sim's local map and the view (the local map's water is the
   nearest sim cell's ocean flag; the rendered coast is the planet mesh's interpolated shoreline —
   boats can row over drawn forest). Then rivers and fords on the local map.
2. **Catch-up speed with several settlements**: 154 ms of sim per game hour for 103 people (debug
   HTML5); a season's skip ≈ 44 s. Profile in `luajit -joff` (interpreter ≈ the browser): A* and
   its heap, RNG hashing, the planet's daily step (amortise it across the day), settle.hourly's
   per-tree scans, `next_tree` O(trees) per felling decision. Measure the release build.
3. **Southern founders**: founding states for a settlement whose day 0 is its autumn (the rotation
   under way, a fresh harvest in store); then settlements on every landmass.
4. **Winter work**: textiles (flax or wool → yarn → cloth → clothes that wear) and livestock (R3)
   — winter is still ~70% social.
5. Fishers as a role (pick_role when a dock exists), boat building and repair, fish drying.
6. R3 animals as individuals; R4 trade between settlements (paths, porters, boats).

## Findings and pitfalls (session 10)
- **bob rejects circular requires, even lazy ones** (`require` inside a function): pass functions
  in (`act.interrupt_hook`, `kin.infant_tick(..., pos)`).
- **No `goto` anywhere** (the HTML5 runtime is Lua 5.1; `npm run lint:lua` only checks sim/ and
  content/).
- Local functions must be defined before the function that calls them (an `update_boats` defined
  after `M.update` was a nil global at runtime).
- Household claims (who cooks, fetches...) must be validated (`think.claim_free`): a claim held by
  someone who moved to a new house starved two people.
- Headless SwiftShader frames cost ~200 ms on their own: judge skip/catch-up speed by the sim's own
  cost (`v3_bench`, skip internals), not wall time.
- Heredocs with apostrophes break in Git Bash: write patch scripts to the scratchpad.
- The machine's timings swing ~2x with other load; compare counts, not milliseconds.

## Known oddities
- Winter is mostly talk, visits and rest (no textiles, livestock or winter crafts yet).
- Fallow ploughing, sowing and harvest still come in bursts per household when the window opens.
- The well gathers up to ~10 on fine evenings.
- Boats are drawn over coastal forest where the sim's water and the rendered coast disagree.
