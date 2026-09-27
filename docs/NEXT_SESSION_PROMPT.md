Continue Tiny Consequences: the living-world rewrite (docs/world/17_ROADMAP.md). W2 is finished and the view draws the real society (D-054); W7 "villages that know each other" has its first vertical (relief, barter, brides carried by porters; D-055). The deep-world architecture is docs/world/18_DEEP_WORLD.md (W7 exchange → W8 status/leaders/raids → W9 tribute polities → W10 livestock/crafts/herds).

Rules: no subagents; the living-world simulation is the #1 priority, jams secondary; hand off before the context runs out (commit, PROGRESS, DECISIONS, SESSION_HANDOFF, this file).

Read, in order: CLAUDE.md; docs/PROGRESS.md; docs/SESSION_HANDOFF.md (exact next steps and known weak spots); docs/DECISIONS.md D-054, D-055; docs/world/18_DEEP_WORLD.md; docs/world/10–17 as needed.

Session checklist first (date IST, git status/log, node/java/luajit). Verify: `npm run test:sim` and `npm run test:web`; fix what is red (see SESSION_HANDOFF "Tests at handoff").

Then, in order: look at the game (`npm run shots:village -- --weeks 20 --zoom 1.2`, `npm run shots`) and fix what reads badly (village density around the square, rain streaks close up, cloud distribution); continue W7 (imports in the field plan → specialisation, porters drawn with sacks, trails where parties walk, seed sweeps measuring relief's effect on famine at the land limit); W6 performance (monthly body updates for the healthy; labour.assign, life.weekly) so the 900-person soft cap can rise with evidence (DECISIONS entry, P07 re-baseline); then W8 (lineages, prestige, leaders, raids with real casualties and loot, grievance, palisades) on the relations graph.

Keep: no scripted consequences; everything blamed (co-causes where two causes meet); saves bit-identical (tools/save_check.lua); regenerate assets/worlds after sim changes (not while a web test runs); nothing moves in the view without a sim reason; commit with the [wN] prefix.
