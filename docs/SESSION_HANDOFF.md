# Session handoff — session 11 end (1 Oct 2026): one surface, both hemispheres, fishing, textiles, sheep

`docs/PROGRESS.md` (session 11 entry) and **D-085 … D-090** in `docs/DECISIONS.md` are the source
of truth. Arjun's rules: no subagents; the living world first; one heavy process at a time
(shared laptop, ~2.5 GB free); visible action = simulation; hand off near ~900K context.

## State
- **main** holds everything. A normal launch opens the v3 world (`assets/worlds/v3.tcsave`: seed
  22, **6 settlements, 145 people, ~170 sheep** on 3 landmasses: Tekheim in the north at its first
  spring dawn; Zurug, Hunuvale, Upper Linamist, Moomoo, Hottoodingle Hop in the south, founded in
  their autumn; 5 coastal with docks, 5 with rivers on their maps). `?world=v2` legacy;
  `?seed=N` generates a v3 world; `?world=NAME` loads `assets/worlds/NAME.tcsave`.
- Regenerate after any change to founding/worldgen:
  `luajit tools/make_world.lua --v3 --seed 22 --settlements 6 --out assets/worlds/v3.tcsave`
  (state hash 1237856114 at the last commit).

## What became genuinely agentic this session
- **Geography** (D-085): `sim/surface.lua` is the one coast/river function for the view and the sim:
  boats row only on drawn water; rivers cross the local maps with fords (wadeable, slow) and deep
  water (closed); floods widen rivers and close fords day by day; people path across at fords.
- **Fishers** (D-087): a fisher household's day is traceable: wake → dock → board its own boat →
  row to a ground → catch taken from the sea cell's fish → row back → land → carry home → smoke
  the surplus at the hearth. Fishing follows need (fresh fish keeps a day or two; smoked fish for the
  winter); boats wear, are mended with a log, rot, and new ones are built on the stocks.
- **Both hemispheres** (D-087): founding states from the local season; crops develop with warmth
  and yield by water.
- **Textiles** (D-088): flax → pull → ret → dress → spin (at the well among neighbours on fair
  evenings) → weave → sew; clothes wear (children outgrow theirs); rags in the cold hurt.
- **Sheep** (D-089): individual animals in the shared event queue; children of owning households
  take the flock out, mind it, bring it home; lambing, shearing, hay, slaughter.

## Performance (release HTML5, `node scripts/skip_probe.mjs --release --skips day`)
- 146 people + ~170 sheep: **42.9 ms of sim per game hour → a 12-day season ≈ 12 s** (103 people
  without the new systems: 9.9 s → 7.2 s after D-086). LuaJIT interpreter 7.7 ms/h.
- The browser's Lua 5.1 is dear on `string.format`, `bit` calls (hashes) and C calls in general:
  **measure in the browser** (`--world NAME` to compare worlds; bisect by disabling a candidate
  function) — LuaJIT counts misled this session (D-090).
- Headless SwiftShader frames cost ~200-600 ms each: browser shot scripts step slowly (minutes).

## How to look at it
- Year watch: `luajit tools/v3_year.lua --load assets/worlds/v3.tcsave --days 54 --sett K
  [--ids 12,21 --follow-days 3,15] --out out/year` → summary.txt (per-settlement food, linen,
  fish, flock lines; budgets for settlement K), follow.txt.
- Maps: `luajit tools/v3_map.lua --load assets/worlds/v3.tcsave --out out/map` (sea, rivers, fords,
  floodplain, plots, buildings, trees, lanes, dock).
- Profile: `luajit -joff tools/v3_profile.lua [--load W] [--days 6] [--time] [--nohook]`.
- Shots: `node scripts/shots_look.mjs --sett K --days D --hour H --zoom Z [--what dock|ford]`,
  `node scripts/shots_geo.mjs` (every settlement, dock, ford, a fisher — slow), `npm run shots:v3`.
- Traces: `think.trace = { [id] = true }` → `think.trace_out` (candidates and utilities; `~` = an
  unevaluated lazy bound). Hash comparisons: `out/s11`-style scratch scripts (`world.advance` 8 days,
  hash positions) to prove refactors identical.

## Verified at the last commit
- Quick sim suite **123/123** (v2 109 + v3 14), web **10/10**, lint clean, debug build 6.1 MB.

## Next (in order)
1. **More browser cost**: eager formats left in PLAN objectives and the hourly board's
   `need.fell_why`; `settle.next_tree` (~11% of instructions: a spatial index or an hourly shortlist);
   A* (~26%). Target: ≤ 10 s a season at 150 people in the release build.
2. **R3 animals continued**: the animated Quaternius farm models through the skinned pipeline
   (`tools/blender/build_villager.py` pattern), goats/cattle/pigs, milk and cheese, manure to the
   fields, dogs and wolves (W4 packs) threatening flocks; then wild herds as individuals (hunters
   choosing a specific animal).
3. **Rivers in use**: water fetched from the river, bridges where traffic crosses deep water, river
   fishing; two-person boat crews; drying racks.
4. **R4 trade** between settlements (cloth, smoked fish, wool, grain) by paths and boats.

## Findings and pitfalls (session 11)
- bob rejects circular requires even inside functions: pass resources/functions in
  (`agents_view.sheep_res` set by view.lua).
- The year tool and tests keep a food ledger: every new food stock or source (fish, smoked, meat)
  must be added to both (`food_total`, `accs`).
- `cat > file` with no input in a Bash call blocks forever; heredocs with apostrophes break: write
  patch scripts to the scratchpad and run them with python.
- Timing in LuaJIT with the JIT on is erratic (slower than -joff here); never time while a headless
  browser runs.
- Lazy candidates (`add_lazy`) and lazy whys (`addw`) must stay exactly equivalent: any code that
  reads candidate utilities directly (the leave-the-baby branch) must resolve lazies the same way.

## Known oddities
- Children still play ~75% of winter days where they are under nine (herding and spinning start at
  9-10). The well still gathers crowds on fine evenings.
- Sheep are rigid procedural movers (no leg animation yet); the fold is drawn as stakes.
- Some infants die "of illness" (the hazard); clothes can reach rags in a household with no fibre.
- Flocks grow fast in the first year (culls start when the hay cannot winter them).
