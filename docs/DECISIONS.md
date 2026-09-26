# Decisions log

Short records of decisions that change the design, scope, budgets or architecture. Newest at the bottom. Format:

```
## D-NNN · YYYY-MM-DD · Title
Context: why a decision was needed.
Decision: what we chose.
Consequences: what changes / what to watch.
```

Claude Code decides autonomously and records here instead of stopping to ask, except where CLAUDE.md says to stop.

---

## D-001 · 2026-09-27 · Scope and pillars fixed before building
Context: Design discussion between Arjun and Claude.
Decision: Living planet sim on an icosphere graph, statistical fields per cell, individual humans only, fixed weekly tick (48/year), blame-tag causality with chain popups, resilient baseline, 1 real hour = 1 world year offline, deep time via epoch mode, plates don't move.
Consequences: See docs 01–08.

## D-002 · 2026-09-27 · Tiny Skies is reference only
Context: The Tiny Skies repository has no licence.
Decision: No code or assets are copied from it. Visual mood only.
Consequences: All models are generated procedurally or come from clearly licensed CC0/OFL sources.

## D-003 · 2026-09-27 · Sim is pure Lua 5.1, tested headless with LuaJIT
Context: HTML5 runs Lua 5.1.4; Claude Code needs a fast loop that doesn't require the Defold editor.
Decision: `sim/` has no Defold dependencies, uses only Lua 5.1 features, runs under LuaJIT for tests, with an in-browser self-test for HTML5 determinism.
Consequences: View/platform code lives only in `game/`.
