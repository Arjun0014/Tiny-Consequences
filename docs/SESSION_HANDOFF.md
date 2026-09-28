# Session handoff — session 6 end (28 Sep 2026, stopped early at Arjun's request)

`docs/PROGRESS.md` is the source of truth (session 6 stage entry). Decisions this session:
**D-071** (wild ecology v2 — on branch `wip/wild-agents`, not merged), **D-072** (memory of hunger:
the store it asks for cannot exist). Arjun's rules: living world first, jams secondary, **no
subagents**, hand off before the context runs out.

## State
- **main** (behaviour unchanged since session 5): sim 102/102, web 8/8 verified at session start.
  New on main: `tools/dread_ab.lua` + `tools/dread_ab.py` (A/B of dread), docs.
- **Branch `wip/wild-agents`**, checked out in the worktree **`../tc-wild`** (d5bae5c). Contains
  main plus the ecology v2 (D-071; `docs/world/14_ECOLOGY.md` §5 on the branch). The worktree has
  no `node_modules`/`tools/bob`: run Lua there (`luajit tests/sim/run.lua ...`); build and shoot
  from main after merging (or copy those folders — never junction them).
- Branch tests: B18/B19/B20, B04, B15a pass; S07 10/10 and S02 9/10 on the full profile earlier in
  the session. **Open**:
  - **P07**: fauna 154k vs 41k VM instructions a tick (+12% total). Cheap cuts: densities are
    computed twice a month (update and derive), each herd's range is copied from its cached table,
    the village-cell key is hashed over all land cells on every call. Cut, then re-baseline with a
    DECISIONS entry.
  - **B15b**: in the new worlds the only island village trading over the sea (seed 5, Bomox)
    fishes 62% of its diet and rations even with its cargo, so its rationing does not descend from
    the loss. Find a subject that depends on bought grain (search more seeds), or a store level at
    which the control does not ration.
  - **Worlds**: regenerate default and tutorial.
  - **Full profile on d5bae5c**: fauna stability, S02, S07, P08, P09, S01 (the pack-size
    interference, winter carry-over, snow cost 0.8 and kin pairing came after the last full runs).

## Next (in order)
1. Branch: optimise fauna; `luajit tests/sim/run.lua` (quick) and `--full --only s02` / `s07` /
   `fauna`; fix B15b; merge into main; regenerate worlds (`luajit tools/make_world.lua --seed 22`,
   `--seed 8 --out assets/worlds/tutorial.tcsave`); `npm run test:sim`; `npm run test:web`;
   `npm run shots:wild` (look at the herds and packs); update D-071; commit.
2. Dread A/B (D-072): 4+ seeds x on/off, 150 years, 2,400 cap, run **sequentially** (memory);
   then dread-scaled granaries and surplus-to-gifts/feasts (social storage and prestige).
3. The 4,800-person scale run with `tools/line_profile.lua` / `tools/society_profile.lua`.
4. Agent depth: prestige from contribution; leadership consequences; inherited social context.

## Tools (new this session)
- `luajit tools/wild_dyn.lua <seed> [years] [--no-society] [--series] [--groups N] [--monthly Y]`
  (branch; also runs on main's field model): continental CVs (monthly and yearly), cycle lags,
  patch synchrony by distance, yearly flows (pups, starvation, dispersal fates, fawns, predation,
  winter lows).
- `luajit tools/dread_ab.lua <seed> [years] [--nodread] [--cap N] [--out f.csv]`, then
  `python tools/dread_ab.py out/dread_*.csv`.

## Pitfalls
- The laptop had under 1 GB free for much of the session (other Python processes): run long seeds
  one at a time; check with `cmd //c systeminfo | grep -i "Available Physical"`.
- Behaviour tests tied to one world's layout (B15) break when worldgen changes: search seeds for a
  subject that fits the premise instead of hard-coding one.
- Fauna flow counters: set `W.scratch.wl_dbg = { ..., only = cellset }` (see `tools/wild_dyn.lua`
  and `tests/sim/behaviour/b_wild.lua`).
- Session 5 pitfalls still apply: VM counts miss C-side costs; warm up profiles; regenerate worlds
  after sim changes (never while a test runs); Git Bash heredocs containing apostrophes break —
  write files with the Write tool.
