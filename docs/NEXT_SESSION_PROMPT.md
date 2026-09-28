Continue Tiny Consequences: the living-world rewrite (docs/world/17_ROADMAP.md, deep world docs/world/18_DEEP_WORLD.md). Done: W2 settlements, W3 sea passage, W5 visuals v4, W7 exchange, W8 status/conflict/people as agents, W9 tribute polities, W10 crafts and herds; session 5: ties that fade and resource rivalry (D-066), consistent carbon (D-067), expected imports and B15 (D-068), performance (D-069). Wild herds and packs as group agents are on branch `wip/wild-agents` (D-070), not merged: fauna stability's predator CV is 0.05 < 0.15.

Rules: no subagents; the living-world simulation is the #1 priority, jams secondary; hand off before the context runs out (commit, PROGRESS, DECISIONS, SESSION_HANDOFF, this file).

Read, in order: CLAUDE.md; docs/PROGRESS.md (session 5 stage entries); docs/SESSION_HANDOFF.md (next steps, tools, pitfalls); docs/DECISIONS.md D-066 … D-070.

Session checklist first (date IST, git status/log, node/java/luajit). Verify: `npm run test:sim` and `npm run test:web`; fix what is red without loosening behaviour tests.

Then: finish the wild herds and packs on the branch (restore the predator-prey cycles through a real mechanism — shared winter severity, the pack's local prey — keep S02/S07 green; merge); measure the memory of hunger over several seeds; a 4,800-person scale run with the profile tools; deeper agents (prestige from contribution, leadership consequences). Look at the game after every visual change (`npm run shots:village|voyage|raid`, `shots:wild` on the branch).

Keep: no scripted consequences; everything blamed (co-causes where two causes meet); saves bit-identical; regenerate assets/worlds after sim changes (not while a test runs); nothing moves in the view without a sim reason; commit with the [wN] prefix.
