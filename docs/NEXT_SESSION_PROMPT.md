Continue Tiny Consequences: the living-world rewrite (docs/world/17_ROADMAP.md, deep world docs/world/18_DEEP_WORLD.md). Done: W2 settlements, W3 sea passage, W5 visuals v4, W7 exchange, W8 status/conflict/people as agents (allies, cattle raids, truces, household herds and bride-wealth, friendship, skills, memory of hunger), W9 tribute polities (with ambition, D-065), W10 crafts and herds; scale 2,400 people.

Rules: no subagents; the living-world simulation is the #1 priority, jams secondary; hand off before the context runs out (commit, PROGRESS, DECISIONS, SESSION_HANDOFF, this file).

Read, in order: CLAUDE.md; docs/PROGRESS.md (session 4 stage entries); docs/SESSION_HANDOFF.md (next steps, tools, pitfalls); docs/DECISIONS.md D-059 … D-065.

Session checklist first (date IST, git status/log, node/java/luajit). Verify: `npm run test:sim` and `npm run test:web`; fix what is red without loosening behaviour tests.

Then: grain raids on kin-bound/island worlds (seed 22 has none); the dry world's climate response (seed 9 savanna, CO₂ ≈410 ppm); more seeds for the dread evidence; B15; wild herds and packs as agents; W6 per-person cost. Look at the game after every visual change (`npm run shots:village|voyage|raid`).

Keep: no scripted consequences; everything blamed (co-causes where two causes meet); saves bit-identical; regenerate assets/worlds after sim changes (not while a test runs); nothing moves in the view without a sim reason; commit with the [wN] prefix.
