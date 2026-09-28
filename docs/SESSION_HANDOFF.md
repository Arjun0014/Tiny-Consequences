# Session handoff — session 5 end (28 Sep 2026)

`docs/PROGRESS.md` is the source of truth for state and numbers (the stage entries marked
session 5). Decisions this session: **D-066** (ties that fade: kin from living parents both ways,
fading trades/debts/daughter ties; bad blood from contested splits; rivals for overgrazed pasture
and fished-out grounds; raid motives), **D-067** (the carbon books balance: grass at one weight in
every flux), **D-068** (expected imports; related blame terms merged; no ghost deliveries; B15a/b),
**D-069** (performance: the woodcutters move on alone; hash-checked inlining; one-round RNG draws),
**D-070** (wild herds and packs as group agents — on branch `wip/wild-agents`, not merged).
Arjun's rules: living world first, jams secondary, **no subagents**, hand off before the context
runs out.

## State
- **main**: everything of session 4 plus D-066..D-069. Seed 22 now has grain raids and a
  submission (none before); seeds 9/3: 63/44 raids, 5/2 submissions over 150 years; grazing and
  fishing disputes with causes (lightning > fire > grazing_dispute; crop_failure > fishing_dispute).
  An uninhabited world's CO₂ holds; seed 9 at year 200: 313 ppm, +0.56 °C (was 392, +1.79).
  B15a/b: a grain cargo lost at sea propagates (re-purchase at the partner's cost / rationing and
  relief: `power_lightning > boat_lost > rationing > relief`).
- **Branch `wip/wild-agents`** (bb37a24, on top of main's session-5 work minus D-069's final
  merge — rebase or merge main before continuing): herds and packs as entities, derived fields,
  migration, dispersal, real hunting, powers on groups, the view drawing real groups, the herd/pack
  inspector, `npm run shots:wild`. Fails only fauna stability (predator CV 0.05 < 0.15).
- **Tests**: the final lines of the session-5 commit message on main give the sim and web results.

## Next (in order)
1. `npm run test:sim` and `npm run test:web` on main.
2. **Wild herds and packs** (branch): restore predator-prey cycles through a real mechanism —
   synchrony from a shared winter severity (snow raises kill success and starvation everywhere at
   once), the pack's response to prey near its den rather than its territory's mean; keep S02/S07
   passing; then P08/P09 and the full profile; merge; regenerate worlds; shots.
3. **Dread evidence** (D-064): several seeds, longer runs, with and without dread
   (`economy.dread` → `farming.field_want`); if no effect, find out why (is the store the binding
   constraint? do famines come from causes a bigger store can't absorb?).
4. **Scale**: a 4,800-person run (soft_cap, max_villages) profiled with `tools/line_profile.lua`;
   next hot spots: labour.assign's weekly full pass (400 k), life.weekly (250 k), construction.plan.
5. Agent depth: prestige from contribution (work, catches, gifts) rather than lineage alone;
   leadership consequences; children's inherited social context.

## Tools (new this session)
- `luajit tools/raid_diag.lua <seed|save> <years> [every] [--quiet] [--save out.tcsave]` — why
  villages do or don't raid (score parts, reckoning, gate) and the political history with chains.
- `luajit tools/carbon_budget.lua <seed|save> [years] [every] [--no-society]` — sources, sinks,
  standing biomass per decade.
- `luajit tools/line_profile.lua <seed|save> [years] [weeks] [pattern] [top] [warm]` — VM
  instructions by source line and function (warm-up weeks first).
- `luajit tools/hash_check.lua <save|seed> [weeks] [every] [--nojit] [--warm N]` — state hashes
  for behaviour-neutral refactors, interpreter timing.
- Branch only: `tools/wild_report.lua`, `npm run shots:wild` (bridge `wild_spots`).

## Pitfalls
- VM instruction counts miss C-side costs (string concatenation, table creation, GC): check with
  `hash_check.lua --nojit`.
- Profiles right after loading a save include cache rebuilds (sea routes, landings): warm up first.
- The default world must be regenerated after any sim change (`luajit tools/make_world.lua --seed
  22` and `--seed 8 --out assets/worlds/tutorial.tcsave`), never while a test run is going.
- A test run loads sim modules once at start: editing files during a run is safe for it, but not
  for background tools started later.
- Bob rejects require cycles even through lazy requires; a `local` declared mid-file is nil in
  functions defined above it (conflict.lua's `ringset` sits before `M.encroach` for that reason).
- Git Bash heredocs with apostrophes break: write patch scripts with the Write tool.
- A second worktree for a clean baseline run works (`git worktree add ../tc-base HEAD`), but never
  junction `node_modules` / `tools/bob` into it: `git worktree remove --force` followed the junctions
  and emptied the main checkout's copies (restored with `npm install` and `npm run setup`). Copy
  them instead, or run the baseline with `git stash`.
