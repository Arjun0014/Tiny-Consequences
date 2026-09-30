# Session handoff — session 7 end (30 Sep 2026, stopped at Arjun's request)

`docs/PROGRESS.md` is the source of truth (session 7 entry). Decision this session: **D-073**
(written in `docs/DECISIONS.md` **on the branch** `wip/wild-agents`; main's copy ends at D-072 so the
merge stays conflict-free). Drafts, not applied: D-074 (memory of hunger with real levers) and
D-075 (standing from contribution) — patch scripts in `tools/wip/` on the branch. Arjun's rules:
living world first, jams secondary, **no subagents**, one long simulation at a time (the laptop
is shared: check `cmd //c systeminfo | grep -i "Available Physical"`).

## State
- **main** (f8e3a00, plus this handoff commit): behaviour unchanged since session 5. Sim 102/102 and
  web 8/8 were verified at the start of session 6; not re-run this session.
- **Branch `wip/wild-agents`** (4959cfa), worktree **`../tc-wild`**: main merged in (d203fd7), then
  - fb0d28b: fauna cost cut **hash-identically** (125 k -> 87 k VM instructions a tick on the P07
    world; detectors 29 k -> 16 k) + one behaviour fix (a herd moved into a dropped herd's slot
    judged its crowding by the dropped herd's range).
  - 4959cfa: **density-dependent fawn survival** and recruitment 1.0/head (D-073 item 7), B15b
    rebuilt from its premise, known import shortfalls booked at once, porters lost on the way out
    settle their expectation, S02/S04/S06 re-specified to their premises, tools.
- Test status on the branch (after 4959cfa unless noted):
  - fauna stability 3/3 (predator CV 0.25-0.28, no extinction); B15a, B15b, B18, B19, B20 pass.
  - S02 full **10/10** (grazers best x3.4-6.6, forest best x0.58-0.69).
  - S04: quick 3/3, full 8/10; S06: quick 3/3, full 7/10; S07 full passes — these three were
    measured **before** item 7 (the recruitment fix): re-run.
  - **S01 full: unknown.** Before item 7 it was **4/10** (wolves extinct on a continent in 4 worlds,
    browsed-out forests, desert/grass drift). After item 7 every continent of seeds 1-10 keeps its
    wolves at year 0 (20-102 wolves), but the 10-seed, 200-year run was stopped at the session's
    end (it takes ~57 min).
  - Last whole quick suite (before item 7): 100/105 — failures: stale default world (expected),
    P07 (fauna 113 k vs baseline 41 k: re-baseline), S02/S04/S06 (since re-specified).
  - lint clean (195 files).

## Next (in order)
1. Branch (`cd ../tc-wild`, run Lua there: `luajit tests/sim/run.lua ...`; it has no node_modules
   except for `node scripts/lint_lua.mjs`):
   - `luajit tests/sim/run.lua --full --only s01 > out/s01.txt` (background, ~1 h). Must reach 7/10.
     If a continent still loses its wolves, trace it: `luajit tools/wip/spintrace.lua <seed> <cont> 30`
     and `luajit tools/wip/wolfcensus.lua 1 0 10`.
   - `--full --only s04`, `s06`, `s07`, `p08` (item 7 changed the herds).
   - `luajit tools/bench.lua --check`, then `--update-baseline` with a DECISIONS line (fauna is
     legitimately ~2-2.8x the field model: groups x range cells; see 14_ECOLOGY §4).
   - Regenerate worlds: `luajit tools/make_world.lua --seed 22` and
     `--seed 8 --out assets/worlds/tutorial.tcsave`; whole quick suite green; commit.
   - Merge into main (`git merge wip/wild-agents` on main; D-073 comes with it), `npm run test:sim`,
     `npm run build`, `npm run test:web`, ONE `npm run shots:wild` (look at herds and packs), commit.
2. Goal 2 (D-074): `python tools/wip/g2_social_storage.py .` on main, review the diff, lint, B17,
   quick suite; then the A/B **sequentially**: for s in 3 9 22 5: `luajit tools/dread_ab.lua $s 150
   --cap 2400` and `... --nodread`; `python tools/dread_ab.py out/dread_*.csv` (now reports gifts,
   feasts, relief, granaries, and famine recurrence 3-30 years after a famine). Write D-074 with the
   numbers (does remembered famine change later history?).
3. Goal 3: `luajit tools/scale_run.lua 22 120 --cap 4800 --villages 60 --out out/w4800.tcsave`
   (one run; prints people, villages, heap MB, ms/week a year), then
   `luajit tools/line_profile.lua out/w4800.tcsave 0 8 society` and `tools/society_profile.lua`;
   save size is printed. Optimise mechanisms, not people.
4. Goal 4 (D-075): `python tools/wip/g4_standing.py .` after step 2; measure (who leads, lineage
   growth, marriage by standing).

## Pitfalls found this session
- `--only` is a plain substring (no patterns): run tests one name at a time.
- Git Bash heredocs mangle `\\n` and some quotes in inline Python: write patch scripts with the
  Write tool (the `tools/wip/*.py` scripts assert every anchor).
- Scenario and worldgen outcomes are chaotic: to judge a change, compare the *full* profile, and
  isolate a suspect change by reverting only it (that is how the herd-slot fix was found to carry
  S02/S06). Per-seed differences in the quick profile are not evidence.
- The fauna debug table (`W.scratch.wl_dbg`) now also counts looks, crowding, moves, splits, drops.
- Waiting on long runs: use one background command or Monitor, not repeated waiters.
