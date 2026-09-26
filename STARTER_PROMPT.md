# Prompts for Claude Code

Paste these into Claude Code from the project folder.

---

## Initial prompt (first session)

```
You are the sole engineer on "Tiny Consequences", a Defold game for the Defold Game Jam 2026 (theme: Chain Reaction). I'm Arjun, the director. I only playtest at chapter boundaries; everything else is yours.

1. Read CLAUDE.md fully, then every file in docs/ (01 to 08, PROGRESS.md, DECISIONS.md, playtests/README.md). They are the spec. Read 02_WORLD_MODEL.md and 03_CAUSALITY.md with particular care: the interconnected simulation and the "why did this happen" chains are the heart of the game.

2. Run the session-start checklist in CLAUDE.md. If something only I can do is missing (a tool that needs admin rights to install, an account), tell me exactly what to do, then carry on with everything that doesn't depend on it.

3. Execute Chapter 1 of docs/08_ROADMAP.md stage by stage, fully autonomously: plan, implement, test, look at the outputs (maps, screenshots, event logs), fix, update docs/PROGRESS.md, commit, next stage. Don't stop to ask me questions inside the chapter: make the call, log it in docs/DECISIONS.md, keep going.

4. When Stage 1.10 is done (all required tests green, screenshots reviewed, docs/playtests/chapter-1.md written and committed), stop and tell me in at most 5 lines what to test.

Quality bar: a genuinely interconnected, deterministic, tested simulation (no scripted consequences), and a planet that looks cute and readable. Be ambitious about depth, but protect the deadline: check the clock at every stage boundary and apply the roadmap's cut order if behind.
```

---

## Resume prompt (new session, mid-chapter)

```
Continue Tiny Consequences. Read CLAUDE.md, then docs/PROGRESS.md, then whichever design docs the current stage needs. Check for any docs/playtests/*-feedback.md not yet marked as read in PROGRESS.md and handle it first. Then continue the current chapter of docs/08_ROADMAP.md autonomously until its playtest report is written, and stop with a ≤ 5-line summary of what I should test.
```

---

## After-playtest prompt

```
I've written docs/playtests/chapter-N-feedback.md (replace N). Read it and mark it as read in PROGRESS.md. Fix anything broken or confusing that affects the core experience before moving on (log significant design changes in DECISIONS.md). Then start Chapter N+1 of docs/08_ROADMAP.md and work autonomously until its playtest report is written. Stop with a ≤ 5-line summary of what to test.
```

---

## Final-day prompt (4 Oct) — only if a session died during Stage 4.5

The After-playtest prompt after playtest 3 already runs all of Chapter 4. Use this one only to restart the release stage cleanly.

```
Final day. Read CLAUDE.md and docs/PROGRESS.md. Finish Stage 4.5: release builds (itch zip + Wavedash build), run the full web test suite against the release build, capture store screenshots and a short GIF, and draft docs/release/itch_page.md and docs/release/wavedash_page.md including the AI-use disclosure and credits. Then write docs/playtests/chapter-4.md with the final checklist and stop. Don't start new features.
```
