Continue Tiny Consequences. Chapter 1 is complete (build c487556, report commit "[ch1] Chapter 1 complete"); Arjun has playtested it.

Read, in order: CLAUDE.md; docs/PROGRESS.md; docs/SESSION_HANDOFF.md; docs/playtests/chapter-1.md; docs/playtests/chapter-1-feedback.md (turn every point into a task in PROGRESS.md before anything else); docs/DECISIONS.md (D-036..D-046 are the latest); the Chapter 2 section of docs/08_ROADMAP.md; then 04_PLAYER.md and 03_CAUSALITY.md §5 (powers, chain popup, "Why?").

Start with the session checklist (date in IST, git status/log, node/java/luajit). Verify the baseline before changing anything: `npm run test:sim` (quick, 67/67), `npm run test:sim -- --full` (67/67), `npm run build` + `npm run test:web` (8/8).

Then address the playtest feedback, and continue with Chapter 2 from Stage 2.1 (Powers: command framework, powers 1–8, switch S02–S04 to real powers, add S05–S07/S10–S12, choose and pre-spin the tutorial world with `npm run sim:scout`, add S15).

Keep the rules: no scripted consequences; every change blamed; regenerate assets/worlds/default.tcsave after behaviour-changing sim edits; check both test profiles after sim changes; record decisions; commit per stage. Handoff at ~700K context.
