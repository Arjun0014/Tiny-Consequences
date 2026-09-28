Continue Tiny Consequences: the living-world rewrite (docs/world/17_ROADMAP.md, deep world docs/world/18_DEEP_WORLD.md). Done: W2 settlements, W3 sea passage (D-059), W5 visuals v4, W7 exchange, W8 status/conflict (D-057, D-060: allies, fear, cattle raids, feuds and truces), W9 tribute polities (first vertical), W10 crafts and herds; scale 2,400 people.

Rules: no subagents; the living-world simulation is the #1 priority, jams secondary; hand off at ~900K context (commit, PROGRESS, DECISIONS, SESSION_HANDOFF, this file).

Read, in order: CLAUDE.md; docs/PROGRESS.md (latest stage entry); docs/SESSION_HANDOFF.md (next steps, tools, pitfalls); docs/DECISIONS.md D-056 … D-060.

Session checklist first (date IST, git status/log, node/java/luajit). Verify: `npm run test:sim` and `npm run test:web`; fix what is red without loosening behaviour tests.

Then: long runs at full scale (300+ years: raids, polities, climate, forests); B15; polity identity in the view and chronicle; household wealth; W6 per-person cost before raising the cap. Look at the game after every visual change (`npm run shots:village|voyage|raid`).

Keep: no scripted consequences; everything blamed (co-causes where two causes meet); saves bit-identical; regenerate assets/worlds after sim changes (not while a test runs); nothing moves in the view without a sim reason; commit with the [wN] prefix.
