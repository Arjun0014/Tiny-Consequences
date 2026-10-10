# Next session — start here

You are continuing **Tiny Consequences 2.0 (TC2)**: a Unity 6.6 Web game in `unity/TinyConsequences2` whose standing direction
(Arjun, D2-089) is *an autonomous medieval civilisation inside a living miniature planet* - no player orders; every structure,
road, mine, boat, market, army and polity arises from simulated need, labour, material, knowledge and history; nothing scripted;
presentation never invents state. The legacy Defold game (TC1: `game/`, `sim/`, `docs/` outside `docs/tc2/`) is research material
only - never modify or stage it. **Do not stop at a milestone**: after each slice choose the next highest-value dependency from the
evidence and keep going; context handoffs are a continuation mechanism (Control Room writes this file), not a stop condition. Stop only
for a real external blocker (credentials, payment, UAC, an irreversible product decision).

**Arjun's direction of 10 Oct 2026 (session 17) - presentation and simulation advance together**: he opened the game and saw people
standing still, houses on roads, a world that does not show its simulation, forests still alike. Order: finish the soil and the niche
(done) -> long/Web/visual checks -> a real-game visual truth audit (people, houses and roads, every system in the picture) -> W-FOREST
pass 2 (climates must not be interchangeable in shots) -> continue the civilisation roadmap. For every new system ask "what changes in the
world picture?". No simulation theatre: presentation elaborates a real activity, never invents one.

## Read first (this order), then work autonomously
1. `docs/tc2/NEXT_SESSION_PROMPT.md` - the standing prompt (architecture, rules, every direction; "Where we are (session 17)").
2. `docs/tc2/SESSION_HANDOFF.md` - top block **"Session 17"**: "Exact state", the state at the first context's end, "Next, in order".
3. `docs/tc2/27_VISUAL_TRUTH.md` - the audit: measurements, fixes, the systems table, what is open.
4. `docs/tc2/DECISIONS.md` newest: **D2-140** (the household's niche), **D2-139** (the soil - "Closed (session 17)"), D2-138, D2-137.
5. `docs/tc2/PROGRESS.md` CURRENT STATE (the session 17 entry first).
6. `git status` (the modified `.claude/launch.json` and untracked `unity/TinyConsequences2/Assets/Resources.meta` are not ours - leave them),
   `git log --oneline -6` (HEAD 13cccef or a handoff commit on it).

## Where things stand (10 Oct 2026, ~06:35 IST, end of session 17's first context)
- **Done and verified (headless)**: D2-139 the soil closed (ten seeds); **D2-140 the niche adopted** (sim version 75; ten seeds `_n1` vs `_s2`:
  hunger a person-year x0.68, births x0.94, people at par, plausible marriages); the visual truth audit's tools (`place --still`, `place --roads`),
  `Core/Agents/Presence.cs` (people placed and posed from what they do), `Core/Agents/WayLines.cs` (ways along streets: homes cut 286 -> 1 in seed 4),
  `Footprints.OpenScale/SquareEarth/DiscFree`, two simulation changes with controls (`--nowallworks`, `--noworkclear`). Targeted tests on the final
  binary: HouseholdFormationTests 5/5, TilthTests 9/9, LandTests 10/10. Control: `--noniche --nowallworks --noworkclear` reproduces `_s2`
  byte-identically (seeds 4, 42).
- **Running in the background (it may have finished - check `scratchpad/q3.log`)**: queue 3 (`C:/Users/aswin/AppData/Local/Temp/claude/
  C--Game-Dev-tiny-consequences/9bd81d71-36cd-4497-a09b-0b3d9ad0e072/scratchpad/q3.sh`): `_c1` (`--nowallworks --noworkclear` must = `_n1`), ten
  seeds `_v3` (the final binary; `cmp_n1_v3.txt` should be at par - only rings and works' clearance differ), then **the full suite**
  (`suite_v3.txt`, via `node tc2/scripts/headless.mjs test`). It was paused at its RAM gate: a 1.7-2.8 GB QEMU VM (not ours) held the machine at
  88-97 % RAM. If it never ran, rerun its steps.
- **NOT verified**: the Unity side (WorldObjectsView, Hud, CameraRig, LivestockView, TerrainLod, ForestTypes, ShotRunner, the Instanced and
  Terrain shaders) has never been compiled; no real shots of any session 17 change; the full suite on sim version 75; `.meta` files for
  `Presence.cs`/`WayLines.cs` (a Unity build makes them - commit them).
- **Prepared, not applied**: `scratchpad/p_summerwoods.py` - the ecology change for W-FOREST pass 2 (D2-142): tree suitability by
  growing-season warmth `SmoothStep(5, 10, TMean + 0.6 TAmp)` in `PlanetSystems.GrowVegetation` (was `SmoothStep(-1, 8, TMean)`), `--oldwoods`.
  The view's `ForestTypes.Classify` already takes the swing (treeline below a growing-season warmth of 7 degC).

## Do next, in order
1. Record queue 3's results (or rerun it): controls, `_v3` vs `_n1` parity, the full suite - fix failures; commit; close D2-139/140 in the
   docs if not already; write D2-141 (the visual truth audit's simulation changes: rings clear of works, works off streets) with `_v3`'s numbers.
2. **The Unity compile and real shots** (one heavy job alone, RAM gate ~72 %): copy a v75 save (`tc2/out/civ/s4_v3/world_y300.tc2`, or set an
   older save's header with `scratchpad/v75.py`), then `node tc2/scripts/unity.mjs shots --tag s17a --load <save> --hour 10 --views
   townlow,townstreet,low,close,village,forest:boreal,forest:rain,forest:dry,forest:mixed,forest:wet` (`scratchpad/shots.sh TAG SAVE VIEWS`);
   fix compile errors; look at the PNGs: people at work and in poses, ways along streets, pens, gates, fallow, the woods' floor. Commit the metas.
3. **W-FOREST pass 2**: apply `p_summerwoods.py`, measure `place --woods` (by kind and by summer), ten seeds against `_v3` (forest, wildlife,
   people at par?), D2-142; shots of four climates side by side; keep iterating until they are not interchangeable.
4. The soil's 900-year check (seeds 4, 42), the Web build and Web test (`webgate.sh TAG`).
5. B of the stillness audit (adults idle 21-24 % of daylight): `timeuse --load` by age; candidate slice **C3.6g the muck and the threshing** -
   real seasonal work the scheduler lacks (dung carted from the yards to the fields with the carts, feeding the soil; threshing over winter),
   visible in the picture.

## Tools (copies of this session's in `tc2/out/tools/s17/` - copy them into the new scratchpad and set `S=`/`SCR=` in the scripts)
- `bld.sh` (BIN, TESTS, GATE: build the headless host, copy to the scratchpad, run test filters), `gwrun.sh` (gated ten-seed runs; `EXTRA=`
  switches), `cmp.py A B seeds`, `rates.py TAG seeds` (hunger, births, deaths, wounds a 1,000 person-years over years 200-299), `v75.py SRC DST`,
  `shots.sh TAG SAVE VIEWS [HOUR]`, `pt.py` (line-ending-aware patching), `q3.sh`, `p_summerwoods.py`.
- Headless: `place --load SAVE --still [--days N]`, `--roads [--show K] [--feature street|lane|way|square|wall|home] [--works] [--rawways]`,
  `--woods`, `--marriage`; run switches `--noniche`, `--nicheyears N`, `--nichetally`, `--nowallworks`, `--noworkclear` (`--oldwoods` once applied).

## Rules that bite
- Commits `[tc2] ...` ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; stage only TC2 files; never force-push.
- **No subagents. Resource Governor LOW: CPU 50 %, RAM 75 %, ONE heavy job at a time**; other applications keep RAM at 75-97 % - gate heavy jobs on
  RAM (the scripts' GATE), never kill processes you did not start, and do light work (code, docs) while waiting. A Unity build runs alone.
- Line endings differ by file (docs and most Core files CRLF; tests and some files LF): patch with `pt.py`, write patch scripts with the Write
  tool (Bash heredocs mangle quotes). Never `sed -i` a CRLF file.
- Core is pure C#, deterministic, no float arithmetic (`headless.mjs test` lints it); judge simulation changes by ten seeds against a control on
  the same binary (a `--no...` switch reproducing the previous runs byte-identically). Saves carry the sim version (75 now): older saves need
  `v75.py` to load (the format is unchanged).
- `place --list` exists: the road audit's listing switch is `--show`, not `--list`.
