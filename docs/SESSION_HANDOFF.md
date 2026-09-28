# Session handoff — session 4 end (28 Sep 2026, 13:50 IST)

`docs/PROGRESS.md` is the source of truth for state and numbers (the stage entries marked
session 4). Decisions this session: **D-059** (sea passage in the villages' own boats, weather at
sea, scalar-stress rival fix, strangers' brides, sea-level lag, the Why line), **D-060** (kin by
share, allies, fear, cattle raids, feuds and truces, towns), **D-061** (household herds and
bride-wealth, coppicing, B08's prediction, polity names and banners, perf caches), **D-062**
(friendship, Worries, Chronicle, inspector scrolling, view at speed), **D-063** (skills; P07
re-baselined), **D-064** (generational memory of hunger), **D-065** (ambition: chiefdoms without
scarcity). Arjun's rules: living world first, jams secondary, **no subagents**, hand off before the
context runs out.

## State
- **Sim** (up to 2,400 people / 30+ villages; quick tests at a 1,200 cap, `--full` 2,400):
  exchange and crafts, trails, status (lineages, prestige, leaders, splits), conflict (grain raids
  with allies, cattle raids, feuds and truces, palisades, fear), tribute polities (now reached by
  ambition as well as want), herds owned by households with bride-wealth, sea passage (voyages,
  squalls, `shipment_lost`), friendship, skills, memory of hunger (dread), coppicing, consistent
  carbon.
- **View/UI**: clouds v4, flora v1, instanced props, trails and village lanes, towns of local
  materials, rivers around villages, boats carrying parties overseas, raiders driving beasts home,
  polity banners, villagers hidden above 60 h/s. Inspector: person Why / Worries / Friends / Skills
  / family herd; village Society, remembered hunger, Chronicle; scrolls with the mouse wheel.
- **Tests at handoff**: sim quick suite **100/100** (`out/quick31.txt`; P07 +0.6% vs the baseline
  re-measured in D-063). Web **8/8** at the last full run (`out/web9.txt`), which predates
  D-063..D-065: since then only inspector text, the chronicle and scrolling changed in the view
  (W09 passed after the scrolling change). Save check identical (seeds 9 and 22).

## Next (in order)
1. `npm run test:sim` and `npm run test:web` (full web run after D-063..D-065).
2. **Grain raids on kin-bound / island worlds**: seed 22 has none in 150 years even with ambition
   (every neighbour is kin by marriage or overseas). Options: ambition against overseas non-kin
   (sea raids exist), weaker kin protection for big villages, or accept.
3. **Dry-world climate** (seed 9): savanna and CO₂ ≈410 ppm, +2 °C by year 200 at 2,400 people.
   Decide whether the small planet's carbon response needs calibrating or is the story
   (`exchange_report` prints mean forest cover).
4. **Dread evidence** is mixed (D-064 §4): measure over several seeds/longer runs.
5. B15 (a trade disruption propagating); wild herds and packs as agents (old W4); W6 per-person
   cost (labour.assign + life.weekly ≈ 33% of the society's cost).

## Tools
- `luajit tools/exchange_report.lua --seed N --years Y --every K`; `luajit out/tmp/polity_run.lua
  <seed | world.tcsave> <years>` (raids, submissions, polities per 20 years — copy into tools/ if
  kept); `luajit tools/society_profile.lua <seed> <years> [weeks]` or
  `<world.tcsave> 0 [weeks]`; `tools/geo_report.lua`; `tools/save_check.lua`.
- `npm run shots:village|voyage|raid|biomes|shots` with `--world dev_y100` (make it with
  `luajit tools/make_world.lua --seed 9 --years 100 --out assets/worlds/dev_y100.tcsave`; the
  current file predates D-063..D-065).
- `node scripts/perf_probe.mjs --release --world dev_y100 --gfx low` (wall-clock swings 5-8× on
  this laptop under memory pressure: compare instruction counts instead).
- Bridge: `voyage`, `voyage_pos`, `cattle_raid`, `submit`, `party_pos`, `gfx {slots, slots_far,
  props}`; test actions `voyage`, `cattle_raid`, `submit`.

## Pitfalls
- Bob rejects require cycles even through lazy requires (sea ↔ fishing, life ↔ mobility: use
  callbacks set in `sim/society/init.lua`).
- A `local` declared mid-file is nil in functions defined above it (friends/herds in life.lua).
- Shadowing a loop's index with a new `local` of the same name (fade_memories) silently breaks it.
- Don't rebuild `dist/` or regenerate `assets/worlds/*.tcsave` while a test run is going.
- New per-person fields are saved through the packed record's `extra` table (cheap but grows
  the save); caches go in `W.scratch` (not saved), never on villages as tables of references.
- Git Bash heredocs with apostrophes break: write patch scripts with the Write tool.
