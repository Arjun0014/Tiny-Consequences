Continue Tiny Consequences: W2 of the living-world rewrite (docs/world/17_ROADMAP.md). The sim core of W2 is done (D-053); the view still draws the old theatre.

Rules: no subagents; the living-world simulation is the #1 priority; hand off at ~700K context (commit, PROGRESS, DECISIONS, SESSION_HANDOFF, this file).

Read, in order: CLAUDE.md; docs/PROGRESS.md; docs/SESSION_HANDOFF.md (exact next steps); docs/DECISIONS.md D-050..D-053; docs/world/10–17 as needed (16 §4 for the view).

Session checklist first (date IST, git status/log, node/java/luajit). Verify: `npm run test:sim` (quick) and `npm run test:web`.

Then, in order: port S03 (act on the village's crop state, not a calendar date), S15 (re-scout the tutorial world), P08 (decide with evidence how society divergence is attributed — don't just lower the threshold); investigate seed 9's chronic famine regime (tools/society_ledger.lua) and fix the mechanism; then W2c: draw the real simulation — buildings/sites/ruins from W.ent.buildings + sim/society/layout.lua, people from sim/society/schedule.lua, boats from their trips, migrant parties on their paths, fields from W.S.farm, construction progress, and the inspector (stocks, yearly flows v.acc_last, households, fields, boats, each person's task and home). Verify in the browser with screenshots; nothing may move without a sim reason. Then profile (multi-rate person updates) and re-baseline P07 with a DECISIONS entry.

Keep: no scripted consequences; everything blamed; saves bit-identical (tools/save_check.lua); regenerate assets/worlds after sim changes; commit with [w2] prefix.
