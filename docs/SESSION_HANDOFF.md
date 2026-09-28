# Session handoff — sea passage, conflict deepened, towns (28 Sep 2026, session 4 checkpoint)

`docs/PROGRESS.md` is the source of truth for state and numbers (latest stage entries: session 4).
Later in session 4: **D-061** (household herds and bride-wealth, coppicing, polity names and banners),
**D-062** (friendship; view at speed), **D-063** (skills), **D-064** (generational memory of hunger).
Spec: `docs/world/` (10–18). Decisions since the last handoff: **D-056** (clouds v4, instanced bake,
flora v1, exchange & crafts, trails), **D-057** (status, leaders, conflict, scale 2,400), **D-058**
(tribute polities, herds, society carbon), **D-059** (sea passage in the villages' own boats, weather
at sea, scalar-stress rival fix, strangers' brides, sea-level lag, the Why line), **D-060** (kin by
share, allies, fear, cattle raids, feuds and truces, towns). Arjun's rules: living world first, jams
secondary, **no subagents**, hand off at ~900K context.

## State
- **Sim**: society at up to 2,400 people / 30 villages (quick tests use a 1,200 cap, `--full` 2,400).
  Exchange (goods trade, crafts from local stone/clay/fibre/wool, make-or-buy, trails), status
  (lineages, prestige, leaders, splits by succession and scalar stress), conflict (grain raids with
  Lanchester battles, cattle raids, allies, palisades, fear, feuds and truces), tribute polities,
  village herds, consistent carbon, sea passage (voyages in the villages' boats; squalls and
  Lightning sink crossings). Everything chains (`storm > boat_lost > shipment_lost`,
  `cattle_raid > cattle_raid > … > truce`, `rationing > raid > submission`).
- **View**: clouds v4, flora v1, instanced props, trails and village lanes, towns of local materials,
  porters/raiders with loads, boats carrying parties overseas, stolen beasts driven home,
  livestock, wildlife by biome, dolphins, the person inspector's "Why:" line.
- **Tests**: sim quick suite green (100 with B02/B03/B03b/B16); web suite 8/8 at the last run
  (`out/web7.txt`, before the town/conflict changes: re-run).

## Next (in order)
1. `npm run test:sim` and `npm run test:web`.
2. The dry world's climate (seed 9: savanna and CO₂ ≈410 ppm by year 200 at 2,400 people): decide
   whether the small planet's carbon response needs calibrating (k_co2 for society releases, ocean
   sink) or is the story; `exchange_report` prints mean forest cover now.
3. Grain raids and polities are rare within 300 years (allies defend kin, D-060): check 500-year
   runs; if they never come, revisit the friend threshold or land pressure.
4. B15 (a trade disruption propagating); wild herds and packs as agents (old W4).
5. W6: labour.assign and life.weekly are ~33% of the society's cost (per-person work).

## Tools
- `luajit tools/exchange_report.lua --seed N --years Y --every K` (trade, raids, cattle raids,
  truces, polities, climate per period); `tools/geo_report.lua` (landmasses, parties by sea);
  `tools/society_profile.lua`; `tools/viewbench.lua`.
- `npm run shots:village|voyage|raid|biomes` (`--world dev_y100` after
  `luajit tools/make_world.lua --seed 22 --years 100 --out assets/worlds/dev_y100.tcsave`).
- Bridge: `voyage`, `voyage_pos`, `cattle_raid`, `party_pos`, `gfx {slots, slots_far, props}`,
  `bench_view`, `biome_spots`; test actions `voyage`, `cattle_raid`.

## Pitfalls
- Bob rejects require cycles even through lazy requires (sea ↔ fishing: use a callback set by init).
- Don't regenerate `assets/worlds/*.tcsave` or edit sim files while a test run is going.
- SwiftShader draws ~16 M prop vertices/s: speed-measuring web tests use `gfx=low` (8/2 slots).
- Git Bash heredocs with apostrophes break: write patch scripts with the Write tool.
- Errand parties keep their members in the home village's list: anyone killed away must be compacted
  out of it (raids, drownings).
