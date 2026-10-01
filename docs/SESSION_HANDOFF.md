# Session handoff — session 8 end (1 Oct 2026, stopped at Arjun's request)

`docs/PROGRESS.md` is the source of truth (session 8 entries). Decisions this session: **D-076**
(W4 finished and merged), **D-074** (memory of hunger with real levers, measured), **D-077** (the
4,800-person run; caps unchanged). D-075 (standing from contribution) is drafted on a branch, not
measured, not merged. Arjun's rules: living world first, jams secondary, **no subagents**, one long
simulation at a time (shared laptop: `cmd //c systeminfo | grep -i "Available Physical"`).

## State
- **main** (`d3bebe4` + this handoff commit): W4 (wild herds and packs) and D-074 merged.
  - Quick sim suite **109/109** and lint clean at d3bebe4 (`out/s8/quick_d074d.txt`).
  - `npm run build` OK and `npm run test:web` **8/8** at 586f4ed (W4 merged, before D-074; D-074
    touched the sim and the chronicle text only). Re-run both next session.
  - Full profile **104/105** at 32a1b92 (before D-074; `out/s8/full_all2.txt`): S01 5/10 — second-
    century wildfire gaps that come from the society's forest clearing, not the herds (D-076 §10,
    `tools/wip/firecount.lua`); S01 was 7, 7, 8 of 10 in this session's earlier full runs. **The full
    profile has not been run after D-074** (S03/S04 depend on storage: quick passes 3/3 and 2/3+).
  - Worlds regenerated after the last sim change (default seed 22, tutorial seed 8).
- **Branch `wip/g4-standing`** (worktree `../tc-g4`, merged with main at d3bebe4, behaviour 23/23,
  lint clean): D-075 drafted — standing from the household's work against its mouths (weighed by
  hunger), great catches, defenders and the fallen (both raid kinds), porters; courtship prefers the
  most respected of the next three single men; `sim/society/standing.lua` (gains with a source,
  `W.scratch.st_dbg` sums them); the inspector's "Honoured for …"; `tools/standing_report.lua`
  (sources of prestige, who leads, marriage by standing tercile, lineage growth by standing).
  **Not measured, not merged.**
- Other worktrees: `../tc-wild` (wip/wild-agents, merged), `../tc-g2` (wip/g2-social-storage,
  merged) — safe to remove (`git worktree remove ../tc-wild`, `../tc-g2`).
- Scale worlds (ignored files): `out/w4800.tcsave` (year 120, 4,050 people), `out/w4800b.tcsave`
  (year 170, 4,484 people, 59 villages).

## Next (in order)
1. Re-verify main: `npm run test:sim`, `npm run build`, `npm run test:web`; then one full profile in
   the background (`luajit tests/sim/run.lua --full > out/full.txt`, ~50 min) — check S03/S04 under
   D-074's storage and S01's wildfire windows.
2. Goal 3 finish (D-077): `luajit tools/line_profile.lua out/w4800b.tcsave 0 8 society` and
   `luajit tools/society_profile.lua out/w4800b.tcsave 0 24` — instructions per person-week by
   function (labour.assign, life, construction, relations/conflict, wildlife). Look for costs that
   grow with village pairs (partners lists, raid targets, kin_monthly loops over all villages) and
   fix mechanisms, not people. Raise the normal cap only with evidence.
3. Goal 4 (D-075): in `../tc-g4`, `luajit tools/standing_report.lua 22 100 --cap 2400` (and 9, 3):
   where prestige comes from (elder/founder gains vs deeds: if the monthly gains still dominate,
   reduce them — "no arbitrary accumulation"), whether leaders are contributors, marriage by standing,
   lineage growth by standing. Quick suite, merge into main (`git merge wip/g4-standing`),
   regenerate worlds, write D-075.
4. Then: the chain popup / "Why?" (old 2.3) only when Arjun says the world is stable enough to freeze.

## Findings and pitfalls from this session
- Stored hours must be shifted after the spin-up (`shift_ticks`): fauna lacked it (packs of the
  spin-up could not move dens for 30 years). New hour fields (`v.harvest_end`) are shifted in
  `sim/society/init.lua`.
- Scratch caches that are only partly reset diverge live and reloaded worlds (the ghost of prey):
  check with `luajit tools/save_check.lua --seed N --weeks 1 --hours 60000` and
  `out/s8/fresh_vs_live2.lua`-style hash comparisons over years, not weeks.
- Lua `a and false or nil` is `nil` (the raid news bug): write the `if`.
- `--seedlist 2,5,9` runs chosen seeds of a scenario; `tools/wip/conttrace.lua`, `s07trace.lua`,
  `s04trace.lua`, `famine_trace.lua`, `firecount.lua`, `headline_mix.lua` trace the mechanisms.
- Behaviour tests built on a world's particular village broke when the worlds changed; restate the
  premise (set it up) rather than loosen the claim (B15, B21, B18 by herd id, B19 counts starvation).
- `npm run shots:wild` now shoots at zoom 1.3 and prints the view's puppet count; bridge
  `wild_debug` lists where each animal is drawn.

## Questions for Arjun (non-blocking)
- S01 asks for a big wildfire (≥ 10 cells) in every 100-year window. At 2,400 people the second
  century has none on many seeds, with or without the herds (forest cleared to ~0.15 in fragments);
  at 600 people fires continue. Keep the criterion (and accept S01 at 5-8/10), or judge the natural
  fire regime by ignitions / fire spread on uncleared land?
- Famine is now rare (0.16 per 100 village-years, mostly in new villages) because every village
  keeps 1.5 years at harvest plus social storage. Is that the world you want, or should hunger be
  more common (e.g. a smaller store target)?
